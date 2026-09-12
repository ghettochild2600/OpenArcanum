using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Formats.Art;
using Arcanum.Formats.Database;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using Arcanum.Runtime;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M3ECapacityValidation
{
    private const string FoodSector = "maps/arcanum1-024-fixed/101602821845.sec";
    private const string ContainerSector = "maps/arcanum1-024-fixed/101602821844.sec";
    private const string FoodIdentity = "G_8781D726_74FE_0846_AD0A_88EE591B6383";
    private const string AmmoIdentity = "G_9239E097_A8D2_C147_9F58_76077340C60E";
    private const string ContainerIdentity = "G_8F454608_E327_1341_B85B_E7A5402D4758";
    private const string NpcIdentity = "G_33CE5E06_F4AC_3A4B_B98E_9AB36C467E6F";
    private static PersistentObjectState _physicalItem;
    private static Vector2 _physicalApproachStart;
    private static readonly List<ArcanumObjectId> BoundaryFillers = new();
    private static int _warnings;
    private static int _errors;
    private static bool _tracking;

    [MenuItem("OpenArcanum/M3E/Run Source Audit")]
    private static void RunSourceAudit()
    {
        using var vfs = MountSourceData();
        string protoDirectory = GameDataLocator.FindDirectory("data/proto");
        if (string.IsNullOrEmpty(protoDirectory))
            throw new DirectoryNotFoundException("The configured source data has no data/proto directory.");
        var prototypes = new ProtoLibrary(protoDirectory);
        var itemArt = new ItemArtResolver(
            MesReader.Read(vfs.ReadAllBytes("art/item/item_ground.mes")),
            MesReader.Read(vfs.ReadAllBytes("art/item/item_inven.mes")),
            MesReader.Read(vfs.ReadAllBytes("art/item/item_paper.mes")),
            MesReader.Read(vfs.ReadAllBytes("art/item/item_schematic.mes")));

        IReadOnlyList<ObjectInstance> foodRecords = ReadSectorRecords(vfs, FoodSector);
        IReadOnlyList<ObjectInstance> containerRecords = ReadSectorRecords(vfs, ContainerSector);
        LogItem("food", Find(foodRecords, FoodIdentity), prototypes, vfs, itemArt);
        LogItem("ammo", Find(containerRecords, AmmoIdentity), prototypes, vfs, itemArt);

        ObjectInstance container = Find(containerRecords, ContainerIdentity);
        ObjectProtoInfo containerProto = prototypes.Get(container.PrototypeNumber);
        int children = containerRecords.Count(record => record.ParentIdentity.Key == ContainerIdentity);
        Debug.Log($"M3E SOURCE CONTAINER: oid={container.Identity}; proto={container.PrototypeNumber}; " +
                  $"type={container.Type}; flags={container.ContainerFlags ?? containerProto?.ContainerFlags ?? 0}; " +
                  $"children={children}; grid=10x96; capacityCells=960; weightCapacityField=absent");
        var largest = containerRecords.Where(record => record.ParentIdentity.Key == ContainerIdentity)
            .Select(record =>
            {
                ObjectProtoInfo proto = prototypes.Get(record.PrototypeNumber);
                uint? aid = record.InvAid ?? proto?.InvAid;
                string path = aid.HasValue ? itemArt.Resolve(aid.Value) : null;
                return (Record: record, Aid: aid, Path: path, Footprint: ResolveFootprint(vfs, path));
            })
            .OrderByDescending(entry => entry.Footprint.x * entry.Footprint.y)
            .ThenBy(entry => entry.Record.Identity.Key)
            .First();
        Debug.Log($"M3E SOURCE LARGE CONTAINER ITEM: oid={largest.Record.Identity}; " +
                  $"proto={largest.Record.PrototypeNumber}; type={largest.Record.Type}; " +
                  $"invAid={(largest.Aid.HasValue ? $"0x{largest.Aid.Value:X8}" : "<none>")}; " +
                  $"art={largest.Path ?? "<none>"}; footprint={largest.Footprint.x}x{largest.Footprint.y}");
    }

    [MenuItem("OpenArcanum/M3E/Prepare Literal Capacity Pickup")]
    private static void PrepareLiteralCapacityPickup()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode in TestTerrain first.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        if (loader == null) throw new InvalidOperationException("TestTerrain has no world-object loader.");
        loader.StartCoroutine(PreparePickup(loader));
    }

    [MenuItem("OpenArcanum/M3E/Validate Capacity Lifecycle")]
    private static void ValidateCapacityLifecycle()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode in TestTerrain first.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        if (loader == null || _physicalItem == null)
            throw new InvalidOperationException("Run Prepare Literal Capacity Pickup first.");
        loader.StartCoroutine(ValidateLifecycle(loader));
    }

    private static IEnumerator PreparePickup(WorldObjectSectorLoader loader)
    {
        BeginTracking();
        BoundaryFillers.Clear();
        WorldMapSessionCoordinator session = loader.Session;
        ProductionPlayerLifecycle lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        PlayerNavigationController navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
        Camera camera = Camera.main;
        Check(lifecycle != null && navigation != null && camera != null,
            "production lifecycle, navigation, and camera exist");
        if (session.PlayerState == null || lifecycle.Presentation == null)
            Check(lifecycle.SpawnAndBind(), "production PC is created and bound");

        ArcanumObjectId playerId = session.PlayerState.Identity;
        session.Characters.SetRace(playerId, CharacterRace.Human);
        session.Characters.SetGender(playerId, CharacterGender.Male);
        Check(session.Characters.GetEffectiveAttribute(playerId, CharacterAttribute.Strength) == 8,
            "production PC effective Strength is 8");
        Check(session.InventoryCapacity.GetCarryCapacity(playerId) == 4000,
            "production PC carry capacity is 4000 source units");
        Check(session.TryTransitionPlayer(FoodSector, new Vector2(10, 46), session.PlayerState.ArtId),
            "coordinator loads the real food fixture sector");
        yield return null;

        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
        Check(loader != null && lifecycle != null && navigation != null && lifecycle.Presentation != null,
            "production presentation rebinds in the food sector");
        _physicalItem = session.States.Values.Single(state => state.Identity.Key == FoodIdentity);
        Check(_physicalItem.Type == ObjectType.Food && _physicalItem.PrototypeNumber == 10078,
            "exact real weighted food fixture");
        Check(_physicalItem.UnitWeight == 50
              && session.InventoryCapacity.GetUnitWeight(_physicalItem.Identity) == 50
              && session.InventoryCapacity.GetTotalWeight(_physicalItem.Identity) == 50,
            "real food source and session weight are exactly 50 units");
        Check(_physicalItem.InventoryFootprint == new InventoryFootprint(1, 2),
            "real food inventory footprint is 1x2");
        Check(session.InventoryCapacity.GetInventoryLoad(playerId) == 0, "production PC begins with zero load");

        // A source-supported race/gender change creates a compact exact boundary fixture without inventing weight.
        session.Characters.SetRace(playerId, CharacterRace.Halfling);
        session.Characters.SetGender(playerId, CharacterGender.Female);
        Check(session.Characters.GetEffectiveAttribute(playerId, CharacterAttribute.Strength) == 4
              && session.InventoryCapacity.GetCarryCapacity(playerId) == 2000,
            "Halfling/Female effective Strength creates exact 2000-unit boundary");
        for (int index = 0; index < 39; index++)
        {
            ItemCreationResult filler = session.CreateItem(10078, ObjectPlacement.ContainedBy(playerId));
            Check(filler.Succeeded, $"boundary filler {index + 1} is accepted");
            BoundaryFillers.Add(filler.State.Identity);
        }
        Check(session.InventoryCapacity.GetInventoryLoad(playerId) == 1950,
            "boundary fixture load is exactly 1950 before the 50-unit pickup");

        Check(session.TryGetLoadedObject(_physicalItem.Identity, out WorldObject runtime),
            "real food has one world presentation");
        WorldObjectSpriteOwner owner = loader.SpriteOwners.Single(candidate => candidate != null
            && candidate.WorldObject != null && candidate.WorldObject.Identity == _physicalItem.Identity);
        Check(TryFindTargetPoint(loader, owner, out Vector3 worldPoint),
            "real food exposes a selectable visible pixel");
        Vector2Int far = FindWalkable(loader, runtime.Tile, 6, 24);
        Check(far.x >= 0 && session.SetMovementState(playerId, far, navigation.Player.ArtId, false),
            "place production PC at a reachable out-of-range start");
        _physicalApproachStart = session.PlayerState.MapPosition;
        camera.transform.position = new Vector3(worldPoint.x, worldPoint.y, camera.transform.position.z);
        if (camera.orthographic) camera.orthographicSize = 3f;
        yield return null;
        Debug.Log($"M3E PHYSICAL READY: sector={session.SelectedSector}; item={_physicalItem.Identity}; " +
                  $"proto={_physicalItem.PrototypeNumber}; unitWeight={_physicalItem.UnitWeight}; " +
                  $"footprint={_physicalItem.InventoryFootprint}; player={playerId}; effectiveStrength=4; " +
                  $"load=1950; capacity=2000; click=center of Game view, then run Validate Capacity Lifecycle.");
    }

    private static IEnumerator ValidateLifecycle(WorldObjectSectorLoader loader)
    {
        WorldMapSessionCoordinator session = loader.Session;
        GraphicsMode initialMode = OpenArcanumGraphicsSettings.Mode;
        try
        {
            PlayerClickMoveInput input = Object.FindFirstObjectByType<PlayerClickMoveInput>();
            PlayerInteractionController interaction = Object.FindFirstObjectByType<PlayerInteractionController>();
            PlayerNavigationController navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
            ProductionPlayerLifecycle lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            Check(input != null && interaction != null && navigation != null && lifecycle != null,
                "production input, interaction, navigation, and PC lifecycle exist");
            Check(input.LastClickedObject == _physicalItem.Identity && input.LastClickAccepted,
                "literal Game-view click selected the exact food ObjectID");

            float deadline = Time.realtimeSinceStartup + 20f;
            while ((interaction.Phase == PlayerInteractionPhase.ApproachingTarget || navigation.IsMoving)
                   && Time.realtimeSinceStartup < deadline) yield return null;
            Check(Time.realtimeSinceStartup < deadline, "literal pickup approach completes before timeout");
            Check(interaction.LastResult.HasValue && interaction.LastResult.Value.IsSuccess
                  && interaction.LastResult.Value.Command.Target == _physicalItem.Identity,
                "arrival executes one successful authoritative pickup");
            Check(session.PlayerState.MapPosition != _physicalApproachStart,
                "literal click moves the production PC");
            Check(_physicalItem.Placement == ObjectPlacement.ContainedBy(session.PlayerState.Identity)
                  && session.InventoryCapacity.GetInventoryLoad(session.PlayerState.Identity) == 2000,
                "exact-boundary pickup succeeds and raises load by exactly 50");
            Check(ProjectionCount(loader, _physicalItem.Identity) == 0,
                "successful pickup removes exactly the world presentation");

            Vector2 playerTile = Vector2Int.RoundToInt(session.PlayerState.TilePosition);
            ItemCreationResult overweight = session.CreateItem(10078,
                ObjectPlacement.InWorld(FoodSector, playerTile));
            Check(overweight.Succeeded && overweight.State.Identity.Type == ArcanumObjectIdType.SessionDynamic,
                "controlled over-capacity item uses a normal D_ identity and source prototype");
            ArcanumObjectId overweightId = overweight.State.Identity;
            ObjectPlacement overweightPlacement = overweight.State.Placement;
            Check(ProjectionCount(loader, overweightId) == 1, "over-capacity fixture has one presentation");
            WorldInteractionResult overweightResult = interaction.TryPickUp(overweightId);
            Check(overweightResult.Code == WorldInteractionResultCode.TooHeavy
                  && overweightResult.InventoryStatus == InventoryResultCode.TooHeavy,
                "pickup returns explicit TooHeavy result");
            Check(overweight.State.Identity == overweightId && overweight.State.Placement == overweightPlacement
                  && session.InventoryCapacity.GetInventoryLoad(session.PlayerState.Identity) == 2000
                  && ProjectionCount(loader, overweightId) == 1,
                "TooHeavy rejection preserves identity, placement, load, and one presentation");
            yield return null;
            Check(interaction.Phase == PlayerInteractionPhase.Cancelled && !interaction.PendingCommand.HasValue
                  && overweight.State.Placement == overweightPlacement,
                "capacity failure leaves no stale pending interaction");

            session.Characters.SetRace(session.PlayerState.Identity, CharacterRace.Human);
            session.Characters.SetGender(session.PlayerState.Identity, CharacterGender.Male);
            Check(session.InventoryCapacity.GetCarryCapacity(session.PlayerState.Identity) == 4000,
                "production Human Male capacity restores to 4000");
            long foodSectorLoad = session.InventoryCapacity.GetInventoryLoad(session.PlayerState.Identity);
            Check(session.TryTransitionPlayer(ContainerSector, new Vector2(36, 58), session.PlayerState.ArtId),
                "PC crosses A to B with authoritative load");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
            Check(session.InventoryCapacity.GetInventoryLoad(session.PlayerState.Identity) == foodSectorLoad,
                "A to B preserves carried load");

            PersistentObjectState npcObject = session.States.Values.Single(state => state.Identity.Key == NpcIdentity);
            Check(npcObject.PrototypeNumber == 17101
                  && session.Characters.GetEffectiveAttribute(npcObject.Identity, CharacterAttribute.Strength) == 9
                  && session.InventoryCapacity.GetCarryCapacity(npcObject.Identity) == 4500,
                "real female Human NPC has effective Strength 9 and 4500 capacity");
            PersistentObjectState container = session.States.Values.Single(state => state.Identity.Key == ContainerIdentity);
            Check(container.Type == ObjectType.Container && container.PrototypeNumber == 3052
                  && session.InventoryCapacity.GetContainerCapacity(container.Identity) == 960,
                "real container uses the fixed 960-cell source grid");
            int containerLoadBefore = session.InventoryCapacity.GetContainerLoad(container.Identity);
            Check(session.ExecuteInteraction(WorldInteractionCommand.Transfer(session.PlayerState.Identity,
                      _physicalItem.Identity, container.Identity)).IsSuccess,
                "PC to real-container transfer succeeds");
            Check(session.InventoryCapacity.GetContainerLoad(container.Identity)
                  == containerLoadBefore + _physicalItem.InventoryFootprint.Cells,
                "real-container load increases by the food's exact two cells");
            Check(session.ExecuteInteraction(WorldInteractionCommand.Transfer(session.PlayerState.Identity,
                      _physicalItem.Identity, session.PlayerState.Identity)).IsSuccess,
                "real-container to PC transfer succeeds");

            Vector2 foreignTile = Vector2Int.RoundToInt(session.PlayerState.TilePosition);
            Check(session.ExecuteInteraction(WorldInteractionCommand.Drop(session.PlayerState.Identity,
                      _physicalItem.Identity, ContainerSector, foreignTile)).IsSuccess,
                "food relocates from its source sector to foreign world placement");
            yield return null;
            Check(ProjectionCount(loader, _physicalItem.Identity) == 1,
                "foreign-sector food projects exactly once");

            foreach (ArcanumObjectId fillerId in BoundaryFillers)
            {
                PersistentObjectState filler = session.States[fillerId];
                Check(session.TransferItem(fillerId, filler.Placement,
                    ObjectPlacement.ContainedBy(container.Identity)).Succeeded,
                    "boundary filler transfers to real container");
            }
            Check(session.InventoryCapacity.GetInventoryLoad(session.PlayerState.Identity) == 0,
                "moving all fillers out derives zero PC load without a mutable counter");

            PersistentObjectState ammo = session.States.Values.Single(state => state.Identity.Key == AmmoIdentity);
            Check(ammo.Type == ObjectType.Ammo && ammo.PrototypeNumber == 7059 && ammo.StackQuantity == 60
                  && ammo.UnitWeight == 1 && ammo.InventoryFootprint == new InventoryFootprint(2, 1)
                  && session.InventoryCapacity.GetTotalWeight(ammo.Identity) == 15,
                "real Ammo fixture is quantity 60, weight 1, total 15, footprint 2x1");
            int containerBeforeAmmo = session.InventoryCapacity.GetContainerLoad(container.Identity);
            Check(session.TransferItem(ammo.Identity, ammo.Placement,
                ObjectPlacement.ContainedBy(session.PlayerState.Identity)).Succeeded,
                "real Ammo transfers from container to PC");
            Check(session.InventoryCapacity.GetInventoryLoad(session.PlayerState.Identity) == 15
                  && session.InventoryCapacity.GetContainerLoad(container.Identity)
                  == containerBeforeAmmo - ammo.InventoryFootprint.Cells,
                "Ammo transfer updates weight and grid load exactly");
            StackSplitResult split = session.SplitStack(ammo.Identity, 20);
            Check(split.Succeeded && session.InventoryCapacity.GetInventoryLoad(session.PlayerState.Identity) == 15,
                "Ammo split preserves exact floor-based total for divisible quantities");
            ArcanumObjectId splitId = split.CreatedState.Identity;
            Check(session.MergeStacks(splitId, ammo.Identity).Succeeded
                  && session.IsObjectRemoved(splitId)
                  && session.InventoryCapacity.GetInventoryLoad(session.PlayerState.Identity) == 15,
                "Ammo merge restores quantity and tombstone contributes no duplicate load");

            PersistentObjectState equipment = session.States.Values
                .Where(state => !state.IsRuntimeCreated && state.ParentIdentity == container.Identity
                                && state.UnitWeight > 0 && (state.ItemFlags & 0x20) == 0)
                .FirstOrDefault(state => WorldMapSessionCoordinator.TryGetNaturalWornLocation(state, out _));
            WornLocation wornLocation = default;
            Check(equipment != null && WorldMapSessionCoordinator.TryGetNaturalWornLocation(equipment,
                out wornLocation), "real container has a removable weighted equipment fixture");
            Check(session.TransferItem(equipment.Identity, equipment.Placement,
                ObjectPlacement.ContainedBy(session.PlayerState.Identity)).Succeeded,
                "weighted equipment transfers to PC");
            long equipmentLoad = session.InventoryCapacity.GetInventoryLoad(session.PlayerState.Identity);
            Check(session.EquipItem(session.PlayerState.Identity, equipment.Identity, wornLocation).Succeeded
                  && session.InventoryCapacity.GetInventoryLoad(session.PlayerState.Identity) == equipmentLoad,
                "equip keeps carried load unchanged");
            Check(session.UnequipItem(session.PlayerState.Identity, wornLocation).Succeeded
                  && session.InventoryCapacity.GetInventoryLoad(session.PlayerState.Identity) == equipmentLoad,
                "unequip keeps carried load unchanged");
            Check(session.TransferItem(equipment.Identity, equipment.Placement,
                ObjectPlacement.ContainedBy(container.Identity)).Succeeded,
                "equipment returns to the real container");

            PersistentObjectState largest = session.States.Values
                .Where(state => !state.IsRuntimeCreated && state.ParentIdentity == container.Identity
                                && WorldMapSessionCoordinator.IsItemType(state.Type)
                                && !state.StackQuantity.HasValue)
                .OrderByDescending(state => state.InventoryFootprint.Cells)
                .ThenBy(state => state.Identity.Key)
                .First();
            int fillCount = 0;
            ItemCreationResult fillResult;
            do
            {
                fillResult = session.CreateItem(largest.PrototypeNumber,
                    ObjectPlacement.ContainedBy(container.Identity));
                if (fillResult.Succeeded) fillCount++;
            } while (fillResult.Succeeded && fillCount < 1000);
            Check(fillResult.Code == InventoryResultCode.NoRoom && fillCount < 1000,
                "finite real-container grid reaches explicit NoRoom");
            int fullContainerLoad = session.InventoryCapacity.GetContainerLoad(container.Identity);
            ItemCreationResult rejectedTransferItem = session.CreateItem(largest.PrototypeNumber,
                ObjectPlacement.InWorld(ContainerSector, foreignTile));
            Check(rejectedTransferItem.Succeeded, "normal dynamic item is created outside the full container");
            ArcanumObjectId rejectedId = rejectedTransferItem.State.Identity;
            ObjectPlacement rejectedPlacement = rejectedTransferItem.State.Placement;
            InventoryTransferResult noRoom = session.TransferItem(rejectedId, rejectedPlacement,
                ObjectPlacement.ContainedBy(container.Identity));
            Check(noRoom.Code == InventoryResultCode.NoRoom
                  && rejectedTransferItem.State.Identity == rejectedId
                  && rejectedTransferItem.State.Placement == rejectedPlacement
                  && session.InventoryCapacity.GetContainerLoad(container.Identity) == fullContainerLoad
                  && ProjectionCount(loader, rejectedId) == 1,
                "NoRoom transfer is atomic for a D_ item and preserves its one presentation");

            long stablePcLoad = session.InventoryCapacity.GetInventoryLoad(session.PlayerState.Identity);
            int stableContainerLoad = session.InventoryCapacity.GetContainerLoad(container.Identity);
            RebuildBothModes(loader, initialMode);
            yield return null;
            Check(session.InventoryCapacity.GetInventoryLoad(session.PlayerState.Identity) == stablePcLoad
                  && session.InventoryCapacity.GetContainerLoad(container.Identity) == stableContainerLoad
                  && ProjectionCount(loader, _physicalItem.Identity) == 1
                  && ProjectionCount(loader, rejectedId) == 1,
                "Original/Enhanced rebuild preserves capacity state and one presentation per world item");
            Check(session.ReloadSelectedSector(), "container sector unload/reload succeeds");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
            Check(session.InventoryCapacity.GetInventoryLoad(session.PlayerState.Identity) == stablePcLoad
                  && session.InventoryCapacity.GetContainerLoad(container.Identity) == stableContainerLoad,
                "unload/reload preserves exact load and capacity queries");
            Check(session.TryTransitionPlayer(FoodSector, new Vector2(63, 46), session.PlayerState.ArtId),
                "PC crosses B to A");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            Check(ProjectionCount(loader, _physicalItem.Identity) == 0,
                "source sector does not respawn the relocated food");
            Check(session.InventoryCapacity.GetInventoryLoad(session.PlayerState.Identity) == stablePcLoad,
                "B to A preserves PC load");
            Check(session.TryTransitionPlayer(ContainerSector, new Vector2(0, 46), session.PlayerState.ArtId),
                "PC crosses A to B");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
            Check(ProjectionCount(loader, _physicalItem.Identity) == 1
                  && session.InventoryCapacity.GetContainerLoad(container.Identity) == stableContainerLoad,
                "A to B restores relocated food once and preserves container load");
            CheckUnique(loader, lifecycle, navigation, session);

            StopTracking();
            Check(_warnings == 0 && _errors == 0,
                $"no new Unity warnings/errors (warnings={_warnings}; errors={_errors})");
            Debug.Log($"M3E PLAYMODE VALIDATION PASS: food={_physicalItem.Identity}; foodProto=10078; " +
                      $"foodWeight=50; foodFootprint=1x2; pcStrength=8; pcCapacity=4000; " +
                      $"npc={npcObject.Identity}; npcStrength=9; npcCapacity=4500; " +
                      $"container={container.Identity}; containerProto=3052; capacityCells=960; " +
                      $"fullContainerLoad={stableContainerLoad}; ammo={ammo.Identity}; ammoQuantity=60; " +
                      $"ammoTotalWeight=15; equipment={equipment.Identity}; dynamicRejected={rejectedId}; " +
                      $"containerFillCount={fillCount}; warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            StopTracking();
        }
    }

    private static void LogItem(string label, ObjectInstance instance, ProtoLibrary prototypes,
        DatVirtualFileSystem vfs, ItemArtResolver itemArt)
    {
        ObjectProtoInfo prototype = prototypes.Get(instance.PrototypeNumber)
            ?? throw new InvalidOperationException($"Missing prototype {instance.PrototypeNumber}.");
        int sourceWeight = instance.Weight ?? prototype.Weight;
        int quantity = instance.Type switch
        {
            ObjectType.Ammo => instance.AmmoQuantity ?? prototype.AmmoQuantity ?? 0,
            ObjectType.Gold => instance.GoldQuantity ?? prototype.GoldQuantity ?? 0,
            _ => 1,
        };
        int totalWeight = instance.Type == ObjectType.Gold ? 0
            : instance.Type == ObjectType.Ammo ? sourceWeight * (quantity / 4)
            : sourceWeight;
        uint? invAid = instance.InvAid ?? prototype.InvAid;
        string artPath = invAid.HasValue ? itemArt.Resolve(invAid.Value) : null;
        Vector2Int footprint = ResolveFootprint(vfs, artPath);
        Debug.Log($"M3E SOURCE ITEM: label={label}; oid={instance.Identity}; proto={instance.PrototypeNumber}; " +
                  $"type={instance.Type}; instanceWeight={Format(instance.Weight)}; prototypeWeight={prototype.Weight}; " +
                  $"sourceWeight={sourceWeight}; quantity={quantity}; totalWeight={totalWeight}; " +
                  $"invAid={(invAid.HasValue ? $"0x{invAid.Value:X8}" : "<none>")}; art={artPath ?? "<none>"}; " +
                  $"footprint={footprint.x}x{footprint.y}; invLocation={instance.InvLocation}; " +
                  $"parent={instance.ParentIdentity}");
    }

    private static Vector2Int ResolveFootprint(DatVirtualFileSystem vfs, string artPath)
    {
        if (string.IsNullOrEmpty(artPath) || !vfs.Exists(artPath)) return Vector2Int.one;
        ArtFile art = ArtReader.Read(vfs.ReadAllBytes(artPath));
        if (art.Rotations.Count == 0 || art.Rotations[0].Frames.Length == 0) return Vector2Int.one;
        ArtFrame frame = art.Rotations[0].Frames[0];
        return new Vector2Int(Math.Max(1, (frame.Width + 31) / 32), Math.Max(1, (frame.Height + 31) / 32));
    }

    private static ObjectInstance Find(IEnumerable<ObjectInstance> records, string identity)
        => records.FirstOrDefault(record => record.Identity.Key == identity)
           ?? throw new InvalidOperationException($"Fixture {identity} was not found.");

    private static IReadOnlyList<ObjectInstance> ReadSectorRecords(DatVirtualFileSystem vfs, string sector)
    {
        long sectorId = long.Parse(Path.GetFileNameWithoutExtension(sector));
        var records = SectorReader.ReadObjects(vfs.ReadAllBytes(sector));
        string prefix = sector.Substring(0, sector.LastIndexOf('/') + 1);
        foreach (string mobilePath in vfs.EnumerateFiles(prefix)
                     .Where(path => path.EndsWith(".mob", StringComparison.OrdinalIgnoreCase)))
        {
            byte[] bytes = vfs.ReadAllBytes(mobilePath);
            int offset = 0;
            ObjectInstance mobile;
            try { mobile = ObjectInstanceReader.Read(bytes, ref offset); }
            catch { continue; }
            if (BelongsToSector(mobile, sectorId)) records.Add(mobile);
        }
        return records;
    }

    private static bool BelongsToSector(ObjectInstance instance, long sectorId)
    {
        if (!instance.Location.HasValue) return false;
        long sectorX = (uint)instance.MapX >> 6;
        long sectorY = (uint)instance.MapY >> 6;
        return (sectorX | (sectorY << 26)) == sectorId;
    }

    private static DatVirtualFileSystem MountSourceData()
    {
        var vfs = new DatVirtualFileSystem();
        string module = GameDataLocator.Find("modules/Arcanum.dat");
        if (string.IsNullOrEmpty(module))
            throw new FileNotFoundException("The configured source data has no modules/Arcanum.dat.");
        vfs.MountFile(module);
        foreach (string archive in new[] { "arcanum1.dat", "arcanum2.dat", "arcanum3.dat", "arcanum4.dat" })
        {
            string path = GameDataLocator.Find(archive);
            if (!string.IsNullOrEmpty(path)) vfs.MountFile(path);
        }
        return vfs;
    }

    private static int ProjectionCount(WorldObjectSectorLoader loader, ArcanumObjectId identity)
        => loader.SpriteOwners.Count(owner => owner != null && owner.WorldObject != null
                                             && owner.WorldObject.Identity == identity);

    private static void RebuildBothModes(WorldObjectSectorLoader loader, GraphicsMode initial)
    {
        OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Original);
        loader.RebuildVisuals();
        OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Enhanced);
        loader.RebuildVisuals();
        OpenArcanumGraphicsSettings.SetRuntimeMode(initial);
        loader.RebuildVisuals();
    }

    private static bool TryFindTargetPoint(WorldObjectSectorLoader loader, WorldObjectSpriteOwner owner,
        out Vector3 worldPoint)
    {
        SpriteRenderer renderer = owner.WorldObject.View;
        Sprite sprite = renderer != null ? renderer.sprite : null;
        if (sprite == null) { worldPoint = default; return false; }
        Texture2D texture = sprite.texture;
        Rect rect = sprite.textureRect;
        Bounds bounds = sprite.bounds;
        int step = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(rect.width, rect.height) / 32f));
        bool found = false;
        float bestDistance = float.PositiveInfinity;
        Vector3 bestPoint = default;
        for (int y = Mathf.FloorToInt(rect.y); y < Mathf.CeilToInt(rect.yMax); y += step)
        for (int x = Mathf.FloorToInt(rect.x); x < Mathf.CeilToInt(rect.xMax); x += step)
        {
            if (texture != null && texture.isReadable && texture.GetPixel(x, y).a <= 1f / 255f) continue;
            Vector3 local = new(Mathf.Lerp(bounds.min.x, bounds.max.x, (x + .5f - rect.x) / rect.width),
                Mathf.Lerp(bounds.min.y, bounds.max.y, (y + .5f - rect.y) / rect.height), 0);
            Vector3 point = renderer.transform.TransformPoint(local);
            if (WorldObjectTargetSelector.TrySelectInteractionTarget(loader.SpriteOwners, point,
                    out ArcanumObjectId id, out ObjectType type)
                && id == owner.WorldObject.Identity && type == owner.WorldObject.Type)
            {
                float dx = x + .5f - rect.center.x;
                float dy = y + .5f - rect.center.y;
                float distance = dx * dx + dy * dy;
                if (distance < bestDistance)
                {
                    found = true;
                    bestDistance = distance;
                    bestPoint = point;
                }
            }
        }
        worldPoint = bestPoint;
        return found;
    }

    private static Vector2Int FindWalkable(WorldObjectSectorLoader loader, Vector2Int target, int min, int max)
    {
        var planner = new InteractionApproachPlanner();
        var route = new List<Vector2Int>();
        for (int y = 0; y < SectorCoordinate.Size; y++)
        for (int x = 0; x < SectorCoordinate.Size; x++)
        {
            var candidate = new Vector2Int(x, y);
            int distance = InteractionRangeRules.Distance(candidate, target);
            if (distance < min || distance > max || !loader.NavigationMap.IsWalkable(candidate)) continue;
            if (planner.TryPlan(loader.NavigationMap, candidate, target,
                    InteractionRangeRules.ItemPickupRange, out _, route)) return candidate;
        }
        return new Vector2Int(-1, -1);
    }

    private static void CheckUnique(WorldObjectSectorLoader loader, ProductionPlayerLifecycle lifecycle,
        PlayerNavigationController navigation, WorldMapSessionCoordinator session)
    {
        Check(Object.FindObjectsByType<WorldMapSessionCoordinator>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one coordinator");
        Check(Object.FindObjectsByType<WorldObjectSectorLoader>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one WorldObjects owner");
        Check(Object.FindObjectsByType<PlayerNavigationController>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one navigation controller");
        Check(Object.FindObjectsByType<PlayerInteractionController>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one interaction controller");
        Check(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                  .Count(root => root.name == "WorldObjects") == 1, "one sector object root");
        Check(Object.FindObjectsByType<WorldObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                  .Count(runtime => runtime.Identity == session.PlayerState.Identity) == 1,
            "one production PC runtime");
        Check(loader.SpriteOwners.Count(owner => owner != null && owner.WorldObject != null
                                               && owner.WorldObject.Identity == session.PlayerState.Identity) == 1,
            "one production PC sprite owner");
        Check(navigation.Player == lifecycle.Presentation, "navigation binds only to production PC");
        Check(loader.GetComponentsInChildren<WorldObjectSpriteOwner>(true).Length == loader.SpriteOwners.Count,
            "no duplicate or orphan sprite owners");
        Check(loader.SpriteOwners.Where(owner => owner != null && owner.WorldObject != null)
                  .GroupBy(owner => owner.WorldObject.Identity).All(group => group.Count() == 1),
            "one presentation per persistent identity");
    }

    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("M3E validation FAIL: " + label);
    }

    private static void BeginTracking()
    {
        StopTracking();
        _warnings = 0;
        _errors = 0;
        Application.logMessageReceived += Track;
        _tracking = true;
    }

    private static void StopTracking()
    {
        if (!_tracking) return;
        Application.logMessageReceived -= Track;
        _tracking = false;
    }

    private static void Track(string _, string __, LogType type)
    {
        if (type == LogType.Warning) _warnings++;
        else if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception) _errors++;
    }

    private static string Format(int? value) => value.HasValue ? value.Value.ToString() : "<inherited>";
}
