using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M3CEquipmentValidation
{
    private const string FixtureSector = "maps/arcanum1-024-fixed/101602821844.sec";
    private const string AdjacentSector = "maps/arcanum1-024-fixed/101602821845.sec";
    private const string ContainerKey = "G_8F454608_E327_1341_B85B_E7A5402D4758";
    private const string ItemKey = "G_0435F503_6600_6342_97B2_6D9E1A85A2F2";
    private static readonly Vector2 FixtureWorldTile = new(12, 18);

    private static PersistentObjectState _item;
    private static ArcanumObjectId _itemIdentity;
    private static WornLocation _location;
    private static Vector2 _approachStart;
    private static int _warnings;
    private static int _errors;
    private static bool _tracking;

    [MenuItem("OpenArcanum/M3C/Prepare Real Equipment Click")]
    private static void PreparePhysicalClick()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        if (loader == null) throw new InvalidOperationException("Open the TestTerrain scene first.");
        loader.StartCoroutine(Prepare(loader));
    }

    [MenuItem("OpenArcanum/M3C/Validate Real Equipment Lifecycle")]
    private static void ValidatePhysicalClick()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        if (loader == null || !_itemIdentity.IsPersistent)
            throw new InvalidOperationException("Run Prepare Real Equipment Click first.");
        loader.StartCoroutine(Validate(loader));
    }

    private static IEnumerator Prepare(WorldObjectSectorLoader loader)
    {
        BeginTracking();
        WorldMapSessionCoordinator session = loader.Session;
        ProductionPlayerLifecycle lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        PlayerNavigationController navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
        PlayerInteractionController interaction = Object.FindFirstObjectByType<PlayerInteractionController>();
        Camera camera = Camera.main;
        Check(lifecycle != null && navigation != null && interaction != null && camera != null,
            "production PC, navigation, interaction, and camera exist");
        Check(session.PlayerState != null, "production PC state exists");
        if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC binds");

        Check(session.TryTransitionPlayer(FixtureSector, FixtureWorldTile, session.PlayerState.ArtId),
            "coordinator selects the real fixture sector");
        yield return null;
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
        lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        Check(loader != null && navigation != null && lifecycle != null && lifecycle.Presentation != null,
            "production presentation rebinds in the fixture sector");

        PersistentObjectState container = session.States.Values.Single(state => state.Identity.Key == ContainerKey);
        _item = session.States.Values.Single(state => state.Identity.Key == ItemKey);
        _itemIdentity = _item.Identity;
        Check(container.Type == ObjectType.Container && container.PrototypeNumber == 3052,
            "exact real source container");
        Check(_item.Type == ObjectType.Armor && _item.PrototypeNumber == 8127,
            "exact real source-authored equipment item");
        Check(_item.AuthoredParentIdentity == container.Identity
              && _item.Placement == ObjectPlacement.ContainedBy(container.Identity),
            "fixture begins in its exact authored container");
        Check(WorldMapSessionCoordinator.TryGetNaturalWornLocation(_item, out _location),
            "source inventory ART resolves a typed worn location");
        Check((_item.ItemFlags & 0x20) == 0, "fixture is removable/droppable");
        Check(session.TransferItem(_itemIdentity, _item.Placement,
            ObjectPlacement.InWorld(FixtureSector, FixtureWorldTile)).Succeeded,
            "fixture setup moves the authentic item into the world through M3A");
        yield return null;
        Check(session.TryGetLoadedObject(_itemIdentity, out WorldObject runtime), "fixture has one world presentation");
        WorldObjectSpriteOwner owner = loader.SpriteOwners.Single(candidate => candidate != null
            && candidate.WorldObject != null && candidate.WorldObject.Identity == _itemIdentity);
        Check(TryFindTargetPoint(loader, owner, out Vector3 worldPoint), "fixture has a selectable visible pixel");

        Check(session.SetMovementState(session.PlayerState.Identity, runtime.Tile,
            navigation.Player.ArtId, false), "place PC at source pickup range zero");
        _approachStart = session.PlayerState.MapPosition;
        camera.transform.position = new Vector3(worldPoint.x, worldPoint.y, camera.transform.position.z);
        if (camera.orthographic) camera.orthographicSize = 3f;
        yield return null;
        Vector3 screenPoint = camera.WorldToScreenPoint(worldPoint);
        Debug.Log($"M3C PHYSICAL READY: sector={FixtureSector}; oid={_itemIdentity}; type={_item.Type}; " +
                  $"proto={_item.PrototypeNumber}; art=0x{_item.ArtId:X8}; " +
                  $"invAid=0x{_item.InventoryArtId.GetValueOrDefault():X8}; " +
                  $"itemFlags=0x{_item.ItemFlags:X8}; worn={_location}:{(int)_location}; " +
                  $"start={_approachStart}; screen={screenPoint}; click=center of Game view, then press F11.");
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        WorldMapSessionCoordinator session = loader.Session;
        PlayerClickMoveInput input = Object.FindFirstObjectByType<PlayerClickMoveInput>();
        PlayerInteractionController interaction = Object.FindFirstObjectByType<PlayerInteractionController>();
        PlayerNavigationController navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
        ProductionPlayerLifecycle lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        Check(input != null && interaction != null && navigation != null && lifecycle != null,
            "production M3C composition exists");
        Check(input.LastClickedObject == _itemIdentity && input.LastClickAccepted,
            $"literal click selects the stable item ObjectID (actual={input.LastClickedObject?.ToString() ?? "<ground>"})");

        float deadline = Time.realtimeSinceStartup + 20f;
        while ((interaction.Phase == PlayerInteractionPhase.ApproachingTarget || navigation.IsMoving)
               && Time.realtimeSinceStartup < deadline) yield return null;
        Check(Time.realtimeSinceStartup < deadline, "physical pickup approach completes before timeout");
        Check(interaction.LastResult.HasValue && interaction.LastResult.Value.IsSuccess
              && interaction.LastResult.Value.Command.Type == WorldInteractionCommandType.PickUp,
            "arrival executes authoritative pickup");
        Check(session.PlayerState.MapPosition == _approachStart,
            "literal click executes at the prepared source range-zero tile");
        PersistentPlayerState player = session.PlayerState;
        PersistentObjectState item = _item;
        ArcanumObjectId identity = item.Identity;
        Check(item.Placement == ObjectPlacement.ContainedBy(player.Identity) && ProjectionCount(loader, item) == 0,
            "pickup creates ordinary PC containment and removes world presentation");

        EquipmentTransactionResult equipped = session.EquipItem(player.Identity, identity, _location);
        Check(equipped.Succeeded && item.Placement == ObjectPlacement.EquippedBy(player.Identity, _location),
            "authoritative Equip commits typed PC equipment state");
        Check(session.TryGetEquippedItem(player.Identity, _location, out PersistentObjectState occupant)
              && ReferenceEquals(occupant, item) && session.ChildrenOf(player.Identity).All(id => id != identity),
            "equipped and ordinary-contained membership are distinct");
        Check(ProjectionCount(loader, item) == 0 && !session.TryGetLoadedObject(identity, out _),
            "equipped item is never independently world-presented");

        ItemCreationResult dynamic = session.CreateItem(item.PrototypeNumber,
            ObjectPlacement.ContainedBy(player.Identity));
        Check(dynamic.Succeeded && dynamic.State.Identity.Type == ArcanumObjectIdType.SessionDynamic,
            "source-valid dynamic equipment uses deterministic D_ identity");
        ArcanumObjectId dynamicIdentity = dynamic.State.Identity;
        EquipmentTransactionResult dynamicSwap = session.EquipItem(player.Identity, dynamicIdentity, _location);
        Check(dynamicSwap.Succeeded && dynamicSwap.DisplacedItemIdentity == identity
              && dynamic.State.Placement == ObjectPlacement.EquippedBy(player.Identity, _location)
              && item.Placement == ObjectPlacement.ContainedBy(player.Identity),
            "occupied-slot swap atomically equips dynamic item and contains authored item");
        EquipmentTransactionResult authoredSwap = session.EquipItem(player.Identity, identity, _location);
        Check(authoredSwap.Succeeded && authoredSwap.DisplacedItemIdentity == dynamicIdentity
              && item.Placement == ObjectPlacement.EquippedBy(player.Identity, _location)
              && dynamic.State.Placement == ObjectPlacement.ContainedBy(player.Identity),
            "reverse occupied-slot swap atomically restores authored equipment");

        RebuildBothModes(loader);
        yield return null;
        Check(item.Identity == identity && item.Placement == ObjectPlacement.EquippedBy(player.Identity, _location)
              && ProjectionCount(loader, item) == 0,
            "Original/Enhanced rebuild preserves equipment identity and state");
        Check(session.TryTransitionPlayer(AdjacentSector, new Vector2(0, 18), player.ArtId),
            "production PC crosses A to B while equipped");
        yield return null;
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        Check(item.Placement == ObjectPlacement.EquippedBy(player.Identity, _location)
              && session.TryGetEquippedItem(player.Identity, _location, out occupant) && occupant.Identity == identity,
            "A to B traversal preserves same equipment membership");
        Check(session.TryTransitionPlayer(FixtureSector, new Vector2(63, 18), player.ArtId),
            "production PC crosses B to A while equipped");
        yield return null;
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        Check(session.ReloadSelectedSector(), "reload equipped item source sector");
        yield return null;
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        Check(item.Identity == identity && item.Placement == ObjectPlacement.EquippedBy(player.Identity, _location)
              && ProjectionCount(loader, item) == 0,
            "reload preserves equipment and never resurrects authored placement");

        EquipmentTransactionResult unequipped = session.UnequipItem(player.Identity, _location);
        Check(unequipped.Succeeded && unequipped.ItemIdentity == identity
              && item.Placement == ObjectPlacement.ContainedBy(player.Identity)
              && session.ChildrenOf(player.Identity).Contains(identity),
            "Unequip preserves identity and returns item to ordinary PC containment");
        Vector2 dropTile = Vector2Int.RoundToInt(player.TilePosition);
        Check(session.ExecuteInteraction(WorldInteractionCommand.Drop(player.Identity, identity,
            FixtureSector, dropTile)).IsSuccess, "ordinary M3B Drop accepts the unequipped item");
        yield return null;
        Check(item.Identity == identity && item.Placement == ObjectPlacement.InWorld(FixtureSector, dropTile)
              && ProjectionCount(loader, item) == 1,
            "same authentic item returns to world exactly once");
        Check(session.ReloadSelectedSector(), "reload final world placement");
        yield return null;
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        Check(ProjectionCount(loader, item) == 1 && session.States.Values.Count(state => state.Identity == identity) == 1,
            "final reload retains one state and one presentation without authored-position resurrection");

        Check(session.EquipItem(player.Identity, dynamicIdentity, _location).Succeeded,
            "dynamic item still uses identical equipment API after authored lifecycle");
        RebuildBothModes(loader);
        Check(dynamic.State.Identity == dynamicIdentity
              && dynamic.State.Placement == ObjectPlacement.EquippedBy(player.Identity, _location)
              && ProjectionCount(loader, dynamic.State) == 0,
            "dynamic equipment identity/state survives graphics rebuild");
        Check(session.ReloadSelectedSector(), "reload dynamic equipped state");
        yield return null;
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        Check(dynamic.State.Identity == dynamicIdentity
              && dynamic.State.Placement == ObjectPlacement.EquippedBy(player.Identity, _location),
            "dynamic equipment survives sector reload");
        Check(session.UnequipItem(player.Identity, _location).Succeeded
              && dynamic.State.Placement == ObjectPlacement.ContainedBy(player.Identity),
            "dynamic Unequip returns to ordinary containment");

        CheckUnique(loader, navigation, lifecycle, session, item, dynamic.State);
        StopTracking();
        Check(_warnings == 0 && _errors == 0,
            $"no new Unity warnings/errors (warnings={_warnings}; errors={_errors})");
        Debug.Log($"M3C equipment validation PASS: sector={FixtureSector}; item={identity}; " +
                  $"proto={item.PrototypeNumber}; type={item.Type}; art=0x{item.ArtId:X8}; " +
                  $"invAid=0x{item.InventoryArtId.GetValueOrDefault():X8}; " +
                  $"itemFlags=0x{item.ItemFlags:X8}; worn={_location}:{(int)_location}; dynamic={dynamicIdentity}; " +
                  $"warnings={_warnings}; errors={_errors}.");
    }

    private static int ProjectionCount(WorldObjectSectorLoader loader, PersistentObjectState state)
        => loader.SpriteOwners.Count(owner => owner != null && owner.WorldObject != null
                                             && owner.WorldObject.Identity == state.Identity);

    private static void RebuildBothModes(WorldObjectSectorLoader loader)
    {
        GraphicsMode initial = OpenArcanumGraphicsSettings.Mode;
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

    private static void CheckUnique(WorldObjectSectorLoader loader, PlayerNavigationController navigation,
        ProductionPlayerLifecycle lifecycle, WorldMapSessionCoordinator session,
        PersistentObjectState authored, PersistentObjectState dynamic)
    {
        Check(Object.FindObjectsByType<WorldMapSessionCoordinator>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one coordinator");
        Check(Object.FindObjectsByType<WorldObjectSectorLoader>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one world-object owner");
        Check(Object.FindObjectsByType<PlayerNavigationController>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one navigation controller");
        Check(Object.FindObjectsByType<PlayerInteractionController>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one interaction controller");
        Check(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                  .Count(root => root.name == "WorldObjects") == 1, "one sector object root");
        Check(Object.FindObjectsByType<WorldObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                  .Count(runtime => runtime.Identity == session.PlayerState.Identity) == 1, "one production PC runtime");
        Check(loader.SpriteOwners.Count(owner => owner != null && owner.WorldObject != null
                                               && owner.WorldObject.Identity == session.PlayerState.Identity) == 1,
            "one production PC sprite owner");
        Check(navigation.Player == lifecycle.Presentation, "navigation binds only to production PC");
        Check(session.States.Values.Count(state => state.Identity == authored.Identity) == 1
              && session.States.Values.Count(state => state.Identity == dynamic.Identity) == 1,
            "one state per authored/dynamic equipment identity");
        Check(session.ChildrenOf(session.PlayerState.Identity).Distinct().Count()
              == session.ChildrenOf(session.PlayerState.Identity).Count,
            "no duplicate ordinary inventory membership");
        Check(session.EquippedItems(session.PlayerState.Identity).Select(state => state.Identity).Distinct().Count()
              == session.EquippedItems(session.PlayerState.Identity).Count,
            "no duplicate equipment membership");
        Check(loader.GetComponentsInChildren<WorldObjectSpriteOwner>(true).Length == loader.SpriteOwners.Count,
            "no duplicate or orphan sprite owners");
        Check(loader.SpriteOwners.Where(owner => owner != null && owner.WorldObject != null)
            .GroupBy(owner => owner.WorldObject.Identity).All(group => group.Count() == 1),
            "one presentation per persistent identity");
    }

    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("M3C validation FAIL: " + label);
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
        else if (type is LogType.Error or LogType.Assert or LogType.Exception) _errors++;
    }
}
