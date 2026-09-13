using System;
using System.Collections;
using System.IO;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Quest;
using Arcanum.Runtime.Campaign;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Dialogue;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M6ASessionSaveValidation
{
    private const string GroundSector = "maps/arcanum1-024-fixed/101602821845.sec";
    private const string GroundFoodKey = "G_8781D726_74FE_0846_AD0A_88EE591B6383";
    private const string InventorySector = "maps/arcanum1-024-fixed/101602821844.sec";
    private const string ContainerKey = "G_8F454608_E327_1341_B85B_E7A5402D4758";
    private const string ArmorKey = "G_0435F503_6600_6342_97B2_6D9E1A85A2F2";
    private const string AmmoKey = "G_9239E097_A8D2_C147_9F58_76077340C60E";
    private const string DoorSector = "maps/arcanum1-024-fixed/122473678402.sec";
    private const string MayorSector = "maps/arcanum1-024-fixed/96636765255.sec";
    private const string MayorKey = "G_787AD4AB_9061_2B4E_A691_F582800B2BB3";
    private const int QuestNumber = 1005;
    private static int _warnings;
    private static int _errors;
    private static bool _tracking;

    [MenuItem("OpenArcanum/M6A/Run Golden Save-Load PlayMode Validation")]
    private static void Run()
    {
        if (!Application.isPlaying)
            throw new InvalidOperationException("Enter Play mode in TestTerrain first.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        if (loader == null) throw new InvalidOperationException("TestTerrain has no world-object loader.");
        loader.StartCoroutine(Validate(loader));
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        GraphicsMode initialMode = OpenArcanumGraphicsSettings.Mode;
        string savePath = Path.Combine(Application.temporaryCachePath, "OpenArcanum-M6A-golden-validation.json");
        BeginTracking();
        try
        {
            WorldMapSessionCoordinator session = loader.Session;
            ProductionPlayerLifecycle lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            PlayerNavigationController navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
            PlayerInteractionController interaction = Object.FindFirstObjectByType<PlayerInteractionController>();
            Check(lifecycle != null && navigation != null && interaction != null,
                "production player/navigation/interaction composition exists");

            session.ResetAuthoritativeSession();
            yield return null;
            Check(session.SelectSector(GroundSector), "clean session loads the authentic pickup sector");
            yield return null;
            Refresh(out loader, out lifecycle, out navigation, out interaction);
            Check(session.PlayerState != null && lifecycle.Presentation != null
                  && navigation.Player == lifecycle.Presentation, "one production PC is spawned and bound");
            ArcanumObjectId pc = session.PlayerState.Identity;

            PersistentObjectState food = RequireState(session, GroundFoodKey, ObjectType.Food, 10078);
            Check(session.TryGetLoadedObject(food.Identity, out WorldObject foodRuntime),
                "real food fixture has a loaded presentation");
            Check(session.SetMovementState(pc, foodRuntime.Tile, session.PlayerState.ArtId, false),
                "PC moves to authentic pickup range");
            Check(session.ExecuteInteraction(WorldInteractionCommand.PickUp(pc, food.Identity)).IsSuccess
                  && food.Placement == ObjectPlacement.ContainedBy(pc),
                "real source-authored food is picked up through the production command");

            Check(session.SelectSector(InventorySector), "coordinator crosses to the authentic inventory sector");
            yield return null;
            Refresh(out loader, out lifecycle, out navigation, out interaction);
            PersistentObjectState container = RequireState(session, ContainerKey, ObjectType.Container, 3052);
            PersistentObjectState armor = RequireState(session, ArmorKey, ObjectType.Armor, 8127);
            PersistentObjectState ammo = RequireState(session, AmmoKey, ObjectType.Ammo, 7059);
            int ammoQuantity = ammo.StackQuantity.GetValueOrDefault();
            Check(ammoQuantity > 4, "authentic Ammo fixture has a splittable quantity");
            Check(session.TransferItem(armor.Identity, armor.Placement, ObjectPlacement.ContainedBy(pc)).Succeeded,
                "real armor transfers from the real container to PC inventory");
            Check(WorldMapSessionCoordinator.TryGetNaturalWornLocation(armor, out WornLocation armorLocation)
                  && session.EquipItem(pc, armor.Identity, armorLocation).Succeeded,
                "real armor equips in its source-valid worn slot");
            Check(session.TransferItem(ammo.Identity, ammo.Placement, ObjectPlacement.ContainedBy(pc)).Succeeded,
                "real Ammo stack transfers from the real container to PC inventory");
            StackSplitResult split = session.SplitStack(ammo.Identity, 4);
            Check(split.Succeeded, "real Ammo stack creates a dynamic split identity");
            ArcanumObjectId splitIdentity = split.CreatedState.Identity;
            Check(session.MergeStacks(splitIdentity, ammo.Identity).Succeeded
                  && ammo.StackQuantity == ammoQuantity && session.IsObjectRemoved(splitIdentity),
                "merge restores quantity and tombstones the consumed split identity");
            ItemCreationResult dynamic = session.CreateItem(10078, ObjectPlacement.ContainedBy(pc));
            Check(dynamic.Succeeded && dynamic.State.Identity.Type == ArcanumObjectIdType.SessionDynamic,
                "runtime item uses a persistent D_ identity");
            ArcanumObjectId dynamicIdentity = dynamic.State.Identity;

            Check(session.SelectSector(DoorSector), "coordinator loads a real animated-door sector");
            yield return null;
            Refresh(out loader, out lifecycle, out navigation, out interaction);
            WorldObjectSpriteOwner doorOwner = loader.SpriteOwners
                .Where(owner => owner != null && owner.WorldObject != null
                    && owner.WorldObject.Type == ObjectType.Portal && owner.WorldObject.Identity.IsPersistent
                    && !PortalTransitionScheduler.IsWindow(owner.WorldObject.ArtId)
                    && owner.FrameCount > PortalTransitionScheduler.OpenFrame(owner.WorldObject.ArtId))
                .OrderBy(owner => owner.WorldObject.Identity.Key, StringComparer.Ordinal)
                .FirstOrDefault();
            Check(doorOwner != null, "real source-authored animated door exists");
            ArcanumObjectId doorIdentity = doorOwner.WorldObject.Identity;
            PersistentObjectState door = session.States[doorIdentity];
            bool changedDoorState = !door.PortalOpen;
            Check(session.Portals.Request(doorIdentity, changedDoorState), "real door accepts a stable-state change");
            yield return WaitForPortal(session, doorIdentity);
            Check(door.PortalOpen == changedDoorState, "real door reaches the requested stable state");

            Check(session.SelectSector(MayorSector), "coordinator loads the authentic mayor sector");
            yield return null;
            Refresh(out loader, out lifecycle, out navigation, out interaction);
            PersistentObjectState mayor = RequireState(session, MayorKey, ObjectType.Npc, 17088);
            Vector2Int goldenTile = FindWalkable(loader, new Vector2Int(1, 1));
            Check(goldenTile.x >= 0 && session.SetMovementState(pc, goldenTile,
                    session.PlayerState.ArtId, false), "production PC receives a real moved position");

            session.Characters.SetGender(pc, CharacterGender.Female);
            int neededXp = Math.Max(0, 3000 - session.Progression.GetExperience(pc));
            session.Progression.AwardExperience(pc, neededXp);
            Check(session.Progression.IncreaseSkill(pc, CharacterSkill.Persuasion)
                  == SkillIncreaseResult.Success, "PC purchases one Persuasion point");
            Check(session.Progression.SetTrainingLevel(pc, CharacterSkill.Persuasion,
                    SkillTrainingLevel.Apprentice) == TrainingAssignmentResult.Success,
                "PC receives the M5C Apprentice training consequence");
            session.Vitality.ApplyHitPointDamage(pc, 6);
            session.Vitality.ApplyFatigueDamage(pc, 8);
            session.DerivedStats.SetAlignment(pc, 50);
            session.DerivedStats.SetReaction(mayor.Identity, pc, 53);
            SetCompletedQuest(session);
            session.Campaign.SetVar(10, 123);
            session.Campaign.SetFlag(11, 1);
            session.Campaign.SetPcVar(12, -4);
            session.Campaign.SetPcFlag(13, 1);
            session.Campaign.SetLocalFlag(mayor.Identity, 1, 7, 1);
            session.Campaign.SetLocalCounter(mayor.Identity, 1, 2, 9);
            session.AddGold(pc, 100);
            Check(session.TryTransferGold(pc, mayor.Identity, 99, out _)
                  && session.GetGold(pc) == 1 && session.GetGold(mayor.Identity) == 99,
                "M5C payment consequence transfers exactly 99 Gold");

            Check(session.Dialogue.Start(pc, mayor.Identity) == DialogueStartStatus.Started
                  && session.Dialogue.CurrentLine == 1
                  && session.Dialogue.AvailableResponses.Any(line => line.Num == 5
                      && line.TokenCode == 't' && line.TokenPayload == "11"),
                "completed quest drives the authentic mayor/trainer branch before save");
            session.SetPlayerDestination(new Vector2Int(12345, 67890));

            Vector2 savedPosition = session.PlayerState.MapPosition;
            uint savedArt = session.PlayerState.ArtId;
            int savedHpDamage = session.Vitality.Get(pc).HitPointDamage;
            int savedFatigueDamage = session.Vitality.Get(pc).FatigueDamage;
            int savedLevel = session.Progression.GetLevel(pc);
            int savedXp = session.Progression.GetExperience(pc);
            int savedPoints = session.Progression.GetUnspentCharacterPoints(pc);
            int savedSpeed = session.DerivedStats.GetDerivedStat(pc, CharacterDerivedStat.Speed);
            int savedCapacity = session.InventoryCapacity.GetCarryCapacity(pc);
            QuestTimestamp savedTimestamp = session.Campaign.GetPcQuestTimestamp(QuestNumber);
            ulong highestKnownDynamic = session.States.Keys
                .Select(Sequence).Append(Sequence(splitIdentity)).Max();

            if (File.Exists(savePath)) File.Delete(savePath);
            SessionSaveResult save = session.SaveGames.SaveSession(savePath);
            Check(save.Succeeded && File.Exists(savePath),
                $"atomic disk save succeeds outside GameData ({save.Failure}: {save.Message})");
            string json = File.ReadAllText(savePath);
            Check(json.Contains("\"format\": \"OpenArcanum.SessionSave\"")
                  && json.Contains("\"version\": 1")
                  && !json.Contains("GameObject") && !json.Contains("instanceId")
                  && !json.Contains(Application.dataPath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase),
                "generated save is versioned, readable, presentation-independent, and machine-path free");

            PersistentPlayerState oldPlayer = session.PlayerState;
            PersistentObjectState oldFood = food;
            CampaignStateService oldCampaign = session.Campaign;
            CharacterStatService oldCharacters = session.Characters;
            WorldObject oldPresentation = lifecycle.Presentation;
            session.ResetAuthoritativeSession();
            yield return null;
            Check(session.PlayerState == null && !session.HasSelectedSector && session.States.Count == 0,
                "authoritative runtime session is fully reset before load");
            Check(oldPresentation == null && Object.FindObjectsByType<WorldObject>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None).All(runtime => runtime.Identity != pc),
                "pre-save production presentation is destroyed before load");

            SessionLoadResult load = session.SaveGames.LoadSession(savePath);
            Check(load.Succeeded, "validated disk load succeeds after the full reset");
            yield return null;
            Refresh(out loader, out lifecycle, out navigation, out interaction);
            Check(session.SelectedSector == MayorSector && session.PlayerState.Identity == pc
                  && session.PlayerState.MapPosition == savedPosition && session.PlayerState.ArtId == savedArt,
                "same production PC identity, sector, map-global position, and ART restore");
            Check(session.PlayerState.Destination == null && session.Dialogue.Phase == DialogueSessionPhase.Idle,
                "navigation intent and active dialogue normalize to idle");
            Check(!ReferenceEquals(session.PlayerState, oldPlayer)
                  && !ReferenceEquals(session.States[food.Identity], oldFood)
                  && !ReferenceEquals(session.Campaign, oldCampaign)
                  && !ReferenceEquals(session.Characters, oldCharacters),
                "restored state comes from new authoritative roots, not stale dictionaries");

            food = session.States[food.Identity];
            armor = session.States[armor.Identity];
            ammo = session.States[ammo.Identity];
            door = session.States[doorIdentity];
            mayor = session.States[mayor.Identity];
            Check(food.Placement == ObjectPlacement.ContainedBy(pc), "real picked-up item remains in PC inventory");
            Check(armor.Placement == ObjectPlacement.EquippedBy(pc, armorLocation),
                "real armor and worn location restore");
            Check(ammo.Placement == ObjectPlacement.ContainedBy(pc) && ammo.StackQuantity == ammoQuantity,
                "real Ammo placement and merged quantity restore");
            Check(session.States[dynamicIdentity].Placement == ObjectPlacement.ContainedBy(pc)
                  && session.IsObjectRemoved(splitIdentity), "live D_ item and tombstoned split identity restore");
            Check(door.PortalOpen == changedDoorState, "real door stable state restores across maps");
            Check(session.Characters.Get(pc).Gender == CharacterGender.Female
                  && session.Vitality.Get(pc).HitPointDamage == savedHpDamage
                  && session.Vitality.Get(pc).FatigueDamage == savedFatigueDamage,
                "M4 attributes and exact HP/Fatigue damage restore");
            Check(session.Progression.GetLevel(pc) == savedLevel
                  && session.Progression.GetExperience(pc) == savedXp
                  && session.Progression.GetUnspentCharacterPoints(pc) == savedPoints
                  && session.Progression.GetPurchasedSkillPoints(pc, CharacterSkill.Persuasion) == 1
                  && session.Progression.GetTrainingLevel(pc, CharacterSkill.Persuasion)
                  == SkillTrainingLevel.Apprentice, "M4C progression, purchased skill, and training restore");
            Check(session.DerivedStats.GetAlignment(pc) == 50
                  && session.DerivedStats.GetReaction(mayor.Identity, pc) == 53
                  && session.DerivedStats.GetDerivedStat(pc, CharacterDerivedStat.Speed) == savedSpeed
                  && session.InventoryCapacity.GetCarryCapacity(pc) == savedCapacity,
                "persistent M4D inputs restore and speed/capacity recompute identically");
            Check(session.Campaign.GetVar(10) == 123 && session.Campaign.GetFlag(11) == 1
                  && session.Campaign.GetPcVar(12) == -4 && session.Campaign.GetPcFlag(13) == 1
                  && session.Campaign.GetPcQuestState(QuestNumber) == (int)QuestState.Completed
                  && session.Campaign.GetPcQuestTimestamp(QuestNumber).Equals(savedTimestamp)
                  && session.Campaign.GetLocalFlag(mayor.Identity, 1, 7) == 1
                  && session.Campaign.GetLocalCounter(mayor.Identity, 1, 2) == 9,
                "M5 campaign, quest timestamp, and object-local script state restore");
            Check(session.Journal.TryProject(QuestNumber, false, out QuestJournalEntry journal)
                  && journal.State == QuestState.Completed, "journal reprojects Completed from restored campaign state");
            Check(session.GetGold(pc) == 1 && session.GetGold(mayor.Identity) == 99,
                "M5C PC/trainer Gold balances restore");

            Check(session.Dialogue.Start(pc, mayor.Identity) == DialogueStartStatus.Started
                  && session.Dialogue.AvailableResponses.Any(line => line.Num == 5
                      && line.TokenCode == 't' && line.TokenPayload == "11"),
                "post-load authentic dialogue consults restored quest/training campaign state");
            session.Dialogue.Cancel("M6A post-load dialogue proof complete");

            Vector2Int dropTile = Vector2Int.RoundToInt(session.PlayerState.TilePosition);
            Check(session.ExecuteInteraction(WorldInteractionCommand.Drop(pc, food.Identity,
                    MayorSector, dropTile)).IsSuccess, "post-load item drop remains operational");
            yield return null;
            Check(session.ExecuteInteraction(WorldInteractionCommand.PickUp(pc, food.Identity)).IsSuccess,
                "post-load item pickup remains operational");

            Vector2 beforeNavigation = session.PlayerState.MapPosition;
            Check(TryStartNearbyRoute(loader, navigation), "post-load navigation accepts a real route");
            float navigationDeadline = Time.realtimeSinceStartup + 15f;
            while (navigation.IsMoving && Time.realtimeSinceStartup < navigationDeadline) yield return null;
            Check(Time.realtimeSinceStartup < navigationDeadline
                  && session.PlayerState.MapPosition != beforeNavigation && session.PlayerState.Destination == null,
                "post-load production PC completes navigation and clears transient intent");

            ItemCreationResult nextDynamic = session.CreateItem(10078, ObjectPlacement.ContainedBy(pc));
            Check(nextDynamic.Succeeded && Sequence(nextDynamic.State.Identity) == highestKnownDynamic + 1,
                "post-load D_ allocation continues monotonically without collision");

            Check(session.SelectSector(DoorSector), "post-load gameplay returns to the real door sector");
            yield return null;
            Refresh(out loader, out lifecycle, out navigation, out interaction);
            Check(session.TryGetLoadedObject(doorIdentity, out WorldObject loadedDoor),
                "restored real door has one rebuilt presentation");
            Check(loadedDoor.RequestPortalOpen(!changedDoorState), "post-load portal accepts the opposite transition");
            yield return WaitForPortal(session, doorIdentity);
            Check(loadedDoor.RequestPortalOpen(changedDoorState), "post-load portal accepts the return transition");
            yield return WaitForPortal(session, doorIdentity);

            RebuildAllModes(loader, initialMode);
            yield return null;
            Check(session.States[doorIdentity].PortalOpen == changedDoorState
                  && session.States[armor.Identity].Placement == ObjectPlacement.EquippedBy(pc, armorLocation),
                "Original/Enhanced/Original rebuild preserves restored authoritative state");
            CheckUnique(loader, lifecycle, navigation, session);

            StopTracking();
            Check(_warnings == 0 && _errors == 0,
                $"no new Unity warnings/errors (warnings={_warnings}; errors={_errors})");
            Debug.Log($"M6A PLAYMODE VALIDATION PASS: save={savePath}; pc={pc}; sector={MayorSector}; "
                      + $"food={food.Identity}; armor={armor.Identity}@{armorLocation}; ammo={ammo.Identity}x{ammoQuantity}; "
                      + $"dynamic={dynamicIdentity}; consumed={splitIdentity}; next={nextDynamic.State.Identity}; "
                      + $"door={doorIdentity}:{changedDoorState}; level={savedLevel}; xp={savedXp}; "
                      + $"quest1005=Completed; training=Persuasion Apprentice; gold=1/99; "
                      + $"warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            StopTracking();
        }
    }

    private static PersistentObjectState RequireState(WorldMapSessionCoordinator session, string key,
        ObjectType type, int prototype)
    {
        PersistentObjectState state = session.States.Values.Single(candidate => candidate.Identity.Key == key);
        Check(state.Type == type && state.PrototypeNumber == prototype,
            $"exact source fixture {key} has expected {type}/{prototype}");
        return state;
    }

    private static void SetCompletedQuest(WorldMapSessionCoordinator session)
    {
        int state = session.Campaign.GetPcQuestState(QuestNumber);
        if (state == (int)QuestState.Unknown)
            session.Campaign.SetPcQuestState(QuestNumber, (int)QuestState.Mentioned);
        state = session.Campaign.GetPcQuestState(QuestNumber);
        if (state == (int)QuestState.Mentioned)
            session.Campaign.SetPcQuestState(QuestNumber, (int)QuestState.Accepted);
        state = session.Campaign.GetPcQuestState(QuestNumber);
        if (state is (int)QuestState.Accepted or (int)QuestState.Achieved)
            session.Campaign.SetPcQuestState(QuestNumber, (int)QuestState.Completed);
        Check(session.Campaign.GetPcQuestState(QuestNumber) == (int)QuestState.Completed,
            "quest 1005 reaches its legal Completed state");
    }

    private static IEnumerator WaitForPortal(WorldMapSessionCoordinator session, ArcanumObjectId identity)
    {
        float deadline = Time.realtimeSinceStartup + 6f;
        while (session.Portals.TryGetPhase(identity, out PortalPhase phase)
               && phase is PortalPhase.Opening or PortalPhase.Closing
               && Time.realtimeSinceStartup < deadline) yield return null;
        Check(Time.realtimeSinceStartup < deadline, $"portal {identity} reaches a stable state");
    }

    private static Vector2Int FindWalkable(WorldObjectSectorLoader loader, Vector2Int preferred)
    {
        if (loader.NavigationMap.IsWalkable(preferred)) return preferred;
        for (int y = 0; y < SectorCoordinate.Size; y++)
        for (int x = 0; x < SectorCoordinate.Size; x++)
            if (loader.NavigationMap.IsWalkable(new Vector2Int(x, y))) return new Vector2Int(x, y);
        return new Vector2Int(-1, -1);
    }

    private static bool TryStartNearbyRoute(WorldObjectSectorLoader loader, PlayerNavigationController navigation)
    {
        Vector2Int start = Vector2Int.RoundToInt(navigation.Player.TilePosition);
        for (int radius = 1; radius <= 8; radius++)
        for (int y = Math.Max(0, start.y - radius); y <= Math.Min(SectorCoordinate.Size - 1, start.y + radius); y++)
        for (int x = Math.Max(0, start.x - radius); x <= Math.Min(SectorCoordinate.Size - 1, start.x + radius); x++)
        {
            var candidate = new Vector2Int(x, y);
            if (candidate == start || !loader.NavigationMap.IsWalkable(candidate)) continue;
            if (navigation.TrySetDestination(candidate)) return true;
        }
        return false;
    }

    private static ulong Sequence(ArcanumObjectId identity)
        => identity.TryGetSessionDynamicSequence(out ulong sequence) ? sequence : 0;

    private static void RebuildAllModes(WorldObjectSectorLoader loader, GraphicsMode initial)
    {
        OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Original);
        loader.RebuildVisuals();
        OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Enhanced);
        loader.RebuildVisuals();
        OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Original);
        loader.RebuildVisuals();
        OpenArcanumGraphicsSettings.SetRuntimeMode(initial);
        loader.RebuildVisuals();
    }

    private static void Refresh(out WorldObjectSectorLoader loader, out ProductionPlayerLifecycle lifecycle,
        out PlayerNavigationController navigation, out PlayerInteractionController interaction)
    {
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
        interaction = Object.FindFirstObjectByType<PlayerInteractionController>();
        Check(loader != null && lifecycle != null && navigation != null && interaction != null,
            "production composition remains available");
    }

    private static void CheckUnique(WorldObjectSectorLoader loader, ProductionPlayerLifecycle lifecycle,
        PlayerNavigationController navigation, WorldMapSessionCoordinator session)
    {
        Check(Object.FindObjectsByType<WorldMapSessionCoordinator>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one session coordinator");
        Check(Object.FindObjectsByType<WorldObjectSectorLoader>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one world-object sector owner");
        Check(Object.FindObjectsByType<PlayerNavigationController>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one navigation controller");
        Check(Object.FindObjectsByType<PlayerInteractionController>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one interaction controller");
        Check(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                  .Count(value => value.name == "WorldObjects") == 1, "one WorldObjects presentation root");
        Check(Object.FindObjectsByType<WorldObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                  .Count(value => value.Identity == session.PlayerState.Identity) == 1,
            "one production PC presentation");
        Check(loader.SpriteOwners.Where(owner => owner != null && owner.WorldObject != null)
            .GroupBy(owner => owner.WorldObject.Identity).All(group => group.Count() == 1),
            "one presentation per persistent identity");
        Check(navigation.Player == lifecycle.Presentation, "navigation binds only to the restored production PC");
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("M6A validation FAIL: " + label);
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
