using System;
using System.Collections;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M8ACombatValidation
{
    private const string FixtureSector = "maps/arcanum1-024-fixed/47781512457.sec";
    private const string OtherMapSector = "maps/bates mansion lev 1/67108865.sec";
    private const string FixtureKey = "G_9B807B01_A142_4949_80CE_5A085F3BEEB1";
    private const int FixturePrototype = 28422;
    private static readonly ArcanumObjectId FixtureIdentity = ParseIdentity(FixtureKey);
    private static readonly ArcanumObjectId MissingIdentity = ArcanumObjectId.CreateGuid(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

    private static bool _running;
    private static int _warnings;
    private static int _errors;
    private static string _temporarySlot;

    [MenuItem("OpenArcanum/M8A/Run Physical PlayMode Validation")]
    private static void Run()
    {
        if (!Application.isPlaying || _running)
            throw new InvalidOperationException("Enter TestTerrain Play mode; run only one M8A harness.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>()
                                         ?? throw new InvalidOperationException("TestTerrain has no world-object loader.");
        loader.StartCoroutine(Validate(loader));
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        _running = true;
        _warnings = _errors = 0;
        _temporarySlot = null;
        GraphicsMode initialMode = OpenArcanumGraphicsSettings.Mode;
        Application.logMessageReceived += Track;
        WorldMapSessionCoordinator session = loader.Session;
        try
        {
            session.ResetAuthoritativeSession();
            yield return null;
            Check(session.SelectSector(FixtureSector), "real Polar Bear Cub sector loads");
            yield return null;
            Refresh(out loader, out ProductionPlayerLifecycle lifecycle,
                out PlayerNavigationController navigation, out PlayerInteractionController interaction,
                out ProductionSaveLoadPresenter savePresenter);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC binds");

            PersistentPlayerState player = session.PlayerState;
            Check(player != null && player.Identity.IsPersistent, "production PC has a stable ObjectID");
            ArcanumObjectId pc = player.Identity;
            PersistentObjectState bear = RequireBear(session);
            Check(session.TryGetLoadedObject(FixtureIdentity, out WorldObject bearRuntime)
                  && bearRuntime.Type == ObjectType.Npc, "exact bear has one live production WorldObject");
            Check(session.Combat.TryGetActorSource(FixtureIdentity, out CombatActorSource bearSource)
                  && bearSource.PrototypeNumber == FixturePrototype && bearSource.ObjectType == ObjectType.Npc,
                "loader registers exact combat source facts");
            Check(session.DerivedStats.GetDerivedStat(FixtureIdentity, CharacterDerivedStat.Speed) == 4,
                "authentic bear Speed is 4");
            Check(session.Vitality.GetCurrentHitPoints(FixtureIdentity) == 48
                  && session.Vitality.GetCurrentFatigue(FixtureIdentity) == 29,
                "authentic bear vitality is 48/29");

            int initialPcHp = session.Vitality.GetCurrentHitPoints(pc);
            int initialPcFatigue = session.Vitality.GetCurrentFatigue(pc);
            int initialBearHp = session.Vitality.GetCurrentHitPoints(FixtureIdentity);
            int initialBearFatigue = session.Vitality.GetCurrentFatigue(FixtureIdentity);

            Check(session.Combat.EndCombat(pc).Failure == CombatFailure.Inactive,
                "EndCombat while inactive fails without state");
            Check(session.Combat.StartCombat(MissingIdentity, FixtureIdentity).Failure == CombatFailure.ActorNotFound
                  && !session.Combat.IsActive && session.Combat.Participants.Count == 0,
                "invalid actor fails transactionally");
            Check(session.Combat.StartCombat(pc, MissingIdentity).Failure == CombatFailure.TargetNotFound
                  && !session.Combat.IsActive && session.Combat.Participants.Count == 0,
                "invalid target fails transactionally");

            session.Vitality.ApplyHitPointDamage(FixtureIdentity, initialBearHp);
            Check(session.Combat.StartCombat(pc, FixtureIdentity).Failure == CombatFailure.ParticipantUnavailable
                  && session.Combat.Participants.Count == 0, "dead bear is unavailable without partial registration");
            session.Vitality.RestoreHitPoints(FixtureIdentity, initialBearHp);
            session.Vitality.ApplyFatigueDamage(FixtureIdentity, initialBearFatigue);
            Check(session.Combat.StartCombat(pc, FixtureIdentity).Failure == CombatFailure.ParticipantUnavailable
                  && session.Combat.Participants.Count == 0,
                "unconscious living bear is unavailable without partial registration");
            session.Vitality.RestoreFatigue(FixtureIdentity, initialBearFatigue);

            ArcanumObjectId pendingTarget = BeginPendingApproach(session, loader, interaction);
            Check(!pendingTarget.IsNull && interaction.Phase == PlayerInteractionPhase.ApproachingTarget
                  && navigation.IsMoving, "real pending approach interaction is established before combat");
            int initialWorldObjects = Count<WorldObject>();
            string authoritativeBaseline = session.SaveGames.SerializeCurrentSession();

            StartAndCheck(session, pc, initialWorldObjects);
            Check(session.Combat.StartCombat(pc, FixtureIdentity).Failure == CombatFailure.AlreadyActive,
                "reentrant StartCombat fails without changing the active session");
            Check(session.Combat.RegisterParticipant(FixtureIdentity).Failure == CombatFailure.AlreadyRegistered
                  && session.Combat.Participants.Count == 2, "duplicate participant is rejected");
            yield return null;
            Check(interaction.Phase == PlayerInteractionPhase.Cancelled && !interaction.PendingCommand.HasValue,
                "pending approach cancels when combat becomes active");
            Check(!navigation.IsMoving && !session.PlayerState.Destination.HasValue,
                "pre-combat ordinary route is cancelled without stale movement");
            Check(!navigation.TrySetDestination(FindNearbyWalkable(loader, navigation.Player.TilePosition)),
                "ordinary ground navigation is blocked during combat");
            Check(session.ExecuteInteraction(new WorldInteractionCommand(pc, FixtureIdentity,
                      WorldInteractionCommandType.Use)).Code == WorldInteractionResultCode.Blocked,
                "ordinary object interaction is blocked during combat");
            Check(session.ExecuteInteraction(new WorldInteractionCommand(pc, FixtureIdentity,
                      WorldInteractionCommandType.Talk)).Code == WorldInteractionResultCode.Blocked
                  && !session.Dialogue.IsBusy, "dialogue cannot bypass active combat");
            CheckUnique(loader, lifecycle, navigation, pc, initialWorldObjects);

            ArcanumObjectId initialCurrent = session.Combat.CurrentParticipant;
            int initialRound = session.Combat.RoundNumber;
            int initialAp = session.Combat.CurrentActionPoints;
            foreach (GraphicsMode mode in new[] { GraphicsMode.Original, GraphicsMode.Enhanced, GraphicsMode.Original })
            {
                OpenArcanumGraphicsSettings.SetRuntimeMode(mode);
                loader.RebuildVisuals();
                Check(session.Combat.IsActive && session.Combat.CurrentParticipant == initialCurrent
                      && session.Combat.RoundNumber == initialRound
                      && session.Combat.CurrentActionPoints == initialAp,
                    $"{mode} rebuild preserves combat lifecycle/current actor/round/AP");
                CheckOrder(session, pc);
                Check(session.SaveGames.SerializeCurrentSession() == authoritativeBaseline,
                    $"{mode} rebuild preserves non-combat authoritative state");
                CheckUnique(loader, lifecycle, navigation, pc, initialWorldObjects);
            }

            Check(session.Combat.EndCurrentTurn(FixtureIdentity).Succeeded
                  && session.Combat.CurrentParticipant == pc,
                "turn advances deterministically from bear to PC");
            int pcSpeed = session.DerivedStats.GetDerivedStat(pc, CharacterDerivedStat.Speed);
            Check(session.Combat.CurrentActionPoints == Math.Max(5, pcSpeed)
                  && session.Combat.MaximumActionPoints == Math.Max(5, pcSpeed),
                "PC AP derives from authoritative M4D Speed");
            Check(session.Combat.EndCurrentTurn(pc).Succeeded
                  && session.Combat.CurrentParticipant == FixtureIdentity
                  && session.Combat.RoundNumber == 2 && session.Combat.CurrentActionPoints == 5,
                "round rollover returns to bear with source-minimum AP");

            Check(session.Combat.EndCombat(pc).Failure == CombatFailure.HostileParticipantActive,
                "active hostile bear blocks premature EndCombat");
            Check(session.Combat.RemoveParticipant(FixtureIdentity).Succeeded,
                "resolved hostile leaves the participant list");
            Check(session.Combat.EndCombat(pc).Succeeded, "production EndCombat completes");
            CheckInactive(session);
            CheckStateUnchanged(session, pc, authoritativeBaseline, initialPcHp, initialPcFatigue,
                initialBearHp, initialBearFatigue);
            Check(navigation.TrySetDestination(FindNearbyWalkable(loader, navigation.Player.TilePosition)),
                "ordinary navigation resumes after combat");
            navigation.CancelRoute();
            Check(session.ExecuteInteraction(new WorldInteractionCommand(pc, FixtureIdentity,
                      WorldInteractionCommandType.Use)).Code != WorldInteractionResultCode.Blocked,
                "ordinary interaction is no longer combat-blocked");

            StartAndCheck(session, pc, initialWorldObjects);
            Check(session.Combat.RemoveParticipant(FixtureIdentity).Succeeded
                  && session.Combat.EndCombat(pc).Succeeded, "second complete StartCombat/EndCombat cycle succeeds");
            CheckInactive(session);
            CheckStateUnchanged(session, pc, authoritativeBaseline, initialPcHp, initialPcFatigue,
                initialBearHp, initialBearFatigue);

            StartAndCheck(session, pc, initialWorldObjects);
            session.ClearSelectedSector();
            yield return null;
            Check(!session.Combat.IsActive && !session.HasSelectedSector
                  && !session.Combat.TryGetActorSource(FixtureIdentity, out _),
                "sector unload normalizes transient combat and source registrations");
            Check(session.SelectSector(FixtureSector), "fixture sector reloads after combat unload");
            yield return null;
            Refresh(out loader, out lifecycle, out navigation, out interaction, out savePresenter);
            Check(session.PlayerState.Identity == pc && RequireBear(session).Identity == FixtureIdentity,
                "sector reload retains exact PC and bear identities");

            StartAndCheck(session, pc, Count<WorldObject>());
            Check(session.SelectSector(OtherMapSector), "cross-map sector selection succeeds during combat");
            yield return null;
            Check(!session.Combat.IsActive && session.SelectedSector == OtherMapSector,
                "map change normalizes transient combat before rebuilding presentation");
            Check(session.SelectSector(FixtureSector), "fixture sector restores after map change");
            yield return null;
            Refresh(out loader, out lifecycle, out navigation, out interaction, out savePresenter);
            bear = RequireBear(session);
            Check(session.PlayerState.Identity == pc && bear.Identity == FixtureIdentity,
                "map round-trip retains non-combat identities");

            StartAndCheck(session, pc, Count<WorldObject>());
            savePresenter.Open(SaveLoadPanelMode.Save);
            _temporarySlot = savePresenter.Controller.SuggestedSlotId;
            savePresenter.Controller.SelectNewSlot();
            savePresenter.Controller.RequestSave();
            Check(string.IsNullOrEmpty(savePresenter.Controller.ErrorMessage)
                  && savePresenter.Controller.SelectedSlotId == _temporarySlot
                  && session.Combat.IsActive, "save UI stores V1 while leaving live transient combat active");
            string activeJson = session.SaveGames.SerializeCurrentSession();
            Check(!activeJson.Contains("combat", StringComparison.OrdinalIgnoreCase),
                "active combat is absent from the V1 session payload");
            savePresenter.Open(SaveLoadPanelMode.Load);
            Check(savePresenter.Controller.SelectSlot(_temporarySlot), "save UI selects the M8A validation slot");
            savePresenter.Controller.RequestLoad();
            yield return null;
            Refresh(out loader, out lifecycle, out navigation, out interaction, out savePresenter);
            Check(!session.Combat.IsActive && session.Combat.Participants.Count == 0,
                "load normalizes transient combat to inactive");
            Check(session.SaveGames.SerializeCurrentSession() == activeJson,
                "load restores the exact saved non-combat V1 state without stale combat");
            Check(session.PlayerState.Identity == pc && RequireBear(session).Identity == FixtureIdentity,
                "save/load retains exact PC and bear identities");
            Check(session.SaveSlots.DeleteSlot(_temporarySlot).Succeeded, "temporary validation slot is removed");
            _temporarySlot = null;
            Check(navigation.TrySetDestination(FindNearbyWalkable(loader, navigation.Player.TilePosition)),
                "ordinary navigation works after combat-normalizing load");
            navigation.CancelRoute();
            Check(session.ExecuteInteraction(new WorldInteractionCommand(pc, FixtureIdentity,
                      WorldInteractionCommandType.Use)).Code != WorldInteractionResultCode.Blocked,
                "ordinary interaction works after combat-normalizing load");
            CheckUnique(loader, lifecycle, navigation, pc, Count<WorldObject>());

            Application.logMessageReceived -= Track;
            Check(_warnings == 0 && _errors == 0, $"warnings={_warnings}; errors={_errors}");
            Debug.Log($"M8A PLAYMODE VALIDATION PASS: sector={FixtureSector}; bear={FixtureIdentity}; "
                      + $"proto={FixturePrototype}; order=bear,pc; bearSpeed=4; bearAP=5; pcSpeed={pcSpeed}; "
                      + "cycles=2; lockout=True; staleInteraction=False; graphics=Original->Enhanced->Original; "
                      + "unload=Inactive; mapChange=Inactive; saveV1=TransientExcluded; load=Inactive; "
                      + $"warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            Application.logMessageReceived -= Track;
            if (!string.IsNullOrEmpty(_temporarySlot))
                session.SaveSlots.DeleteSlot(_temporarySlot);
            _temporarySlot = null;
            _running = false;
        }
    }

    private static void StartAndCheck(WorldMapSessionCoordinator session, ArcanumObjectId pc,
        int expectedWorldObjects)
    {
        Check(session.Combat.StartCombat(pc, FixtureIdentity).Succeeded, "production StartCombat succeeds");
        Check(session.Combat.IsActive && session.Combat.Lifecycle == CombatLifecycle.Active
              && session.Combat.Mode == CombatMode.TurnBased, "combat becomes authoritatively active");
        CheckOrder(session, pc);
        Check(session.Combat.CurrentParticipant == FixtureIdentity && session.Combat.RoundNumber == 1
              && session.Combat.CurrentActionPoints == 5 && session.Combat.MaximumActionPoints == 5,
            "bear owns deterministic first turn with AP 5");
        Check(Count<WorldObject>() == expectedWorldObjects,
            "StartCombat creates no combat-only WorldObject clones");
    }

    private static void CheckOrder(WorldMapSessionCoordinator session, ArcanumObjectId pc)
    {
        Check(session.Combat.Participants.Count == 2
              && session.Combat.Participants[0].Identity == FixtureIdentity
              && session.Combat.Participants[1].Identity == pc
              && session.Combat.Participants.Select(value => value.Identity).Distinct().Count() == 2,
            "participants are exactly bear then source-tail PC without duplicates");
    }

    private static void CheckInactive(WorldMapSessionCoordinator session)
    {
        Check(session.Combat.Lifecycle == CombatLifecycle.Inactive && !session.Combat.IsActive
              && session.Combat.Participants.Count == 0 && session.Combat.CurrentParticipant.IsNull
              && session.Combat.RoundNumber == 0 && session.Combat.CurrentActionPoints == 0
              && session.Combat.MaximumActionPoints == 0, "EndCombat clears all transient combat state");
    }

    private static void CheckStateUnchanged(WorldMapSessionCoordinator session, ArcanumObjectId pc,
        string baseline, int pcHp, int pcFatigue, int bearHp, int bearFatigue)
    {
        Check(session.PlayerState.Identity == pc && RequireBear(session).Identity == FixtureIdentity,
            "PC and bear identities remain unchanged");
        Check(session.Vitality.GetCurrentHitPoints(pc) == pcHp
              && session.Vitality.GetCurrentFatigue(pc) == pcFatigue
              && session.Vitality.GetCurrentHitPoints(FixtureIdentity) == bearHp
              && session.Vitality.GetCurrentFatigue(FixtureIdentity) == bearFatigue,
            "M4B HP/Fatigue remain unchanged");
        Check(session.SaveGames.SerializeCurrentSession() == baseline,
            "inventory/equipment/campaign and all non-combat state remain unchanged");
    }

    private static PersistentObjectState RequireBear(WorldMapSessionCoordinator session)
    {
        Check(session.States.TryGetValue(FixtureIdentity, out PersistentObjectState bear),
            "exact Polar Bear Cub ObjectID resolves");
        Check(bear.Type == ObjectType.Npc && bear.PrototypeNumber == FixturePrototype,
            "exact fixture is NPC prototype 28422");
        return bear;
    }

    private static ArcanumObjectId BeginPendingApproach(WorldMapSessionCoordinator session,
        WorldObjectSectorLoader loader, PlayerInteractionController interaction)
    {
        foreach (PersistentObjectState state in session.States.Values
                     .Where(value => !value.Off && value.Placement.Kind == ObjectPlacementKind.World
                                     && WorldMapSessionCoordinator.IsItemType(value.Type))
                     .OrderBy(value => value.Identity.Key, StringComparer.Ordinal))
        {
            if (!session.TryGetLoadedObject(state.Identity, out _)) continue;
            if (!TryGlobalPosition(state, out Vector2 target)
                || InteractionRangeRules.IsWithin(session.PlayerState.MapPosition, target,
                    InteractionRangeRules.ItemPickupRange)) continue;
            WorldInteractionResult result = interaction.TryPickUp(state.Identity);
            if (result.Code == WorldInteractionResultCode.Approaching) return state.Identity;
        }
        foreach (PersistentObjectState state in session.States.Values
                     .Where(value => !value.Off && value.Type == ObjectType.Portal
                                     && value.Placement.Kind == ObjectPlacementKind.World)
                     .OrderBy(value => value.Identity.Key, StringComparer.Ordinal))
        {
            if (!session.TryGetLoadedObject(state.Identity, out _)) continue;
            if (!TryGlobalPosition(state, out Vector2 target)
                || InteractionRangeRules.IsWithin(session.PlayerState.MapPosition, target,
                    InteractionRangeRules.PortalUseRange)) continue;
            WorldInteractionResult result = interaction.TryUse(state.Identity);
            if (result.Code == WorldInteractionResultCode.Approaching) return state.Identity;
        }

        Vector2Int destination = FindNearbyWalkable(loader, session.PlayerState.TilePosition);
        ItemCreationResult created = session.CreateItem(10078,
            ObjectPlacement.InWorld(session.SelectedSector, destination));
        Check(created.Succeeded && session.TryGetLoadedObject(created.State.Identity, out _),
            "production session projects a temporary ordinary Food interaction target");
        WorldInteractionResult fallback = interaction.TryPickUp(created.State.Identity);
        Check(fallback.Code == WorldInteractionResultCode.Approaching,
            "temporary ordinary Food enters the production approach path");
        return created.State.Identity;
    }

    private static bool TryGlobalPosition(PersistentObjectState state, out Vector2 position)
    {
        if (SectorCoordinate.TryParse(state.Placement.Sector, out SectorCoordinate sector))
        {
            position = sector.ToGlobal(state.Placement.TilePosition);
            return true;
        }
        position = default;
        return false;
    }

    private static Vector2Int FindNearbyWalkable(WorldObjectSectorLoader loader, Vector2 current)
    {
        Vector2Int start = Vector2Int.RoundToInt(current);
        for (int radius = 1; radius <= 8; radius++)
        for (int y = Math.Max(0, start.y - radius); y <= Math.Min(SectorCoordinate.Size - 1, start.y + radius); y++)
        for (int x = Math.Max(0, start.x - radius); x <= Math.Min(SectorCoordinate.Size - 1, start.x + radius); x++)
        {
            var candidate = new Vector2Int(x, y);
            if (candidate != start && loader.NavigationMap.IsWalkable(candidate)) return candidate;
        }
        throw new InvalidOperationException("M8A validation FAIL: no nearby walkable navigation target.");
    }

    private static void CheckUnique(WorldObjectSectorLoader loader, ProductionPlayerLifecycle lifecycle,
        PlayerNavigationController navigation, ArcanumObjectId pc, int expectedWorldObjects)
    {
        Check(Count<WorldMapSessionCoordinator>() == 1 && Count<WorldObjectSectorLoader>() == 1
              && Count<ProductionPlayerLifecycle>() == 1 && Count<PlayerNavigationController>() == 1
              && Count<PlayerInteractionController>() == 1 && Count<ProductionSaveLoadPresenter>() == 1,
            "one coordinator/loader/lifecycle/navigation/interaction/save presenter");
        Check(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                  .Count(value => value.name == "WorldObjects") == 1, "one WorldObjects presentation root");
        Check(CountIdentity(pc) == 1 && CountIdentity(FixtureIdentity) == 1,
            "exactly one PC and one bear WorldObject presentation");
        Check(Count<WorldObject>() == expectedWorldObjects, "no duplicate WorldObject state appears");
        Check(loader.SpriteOwners.Where(owner => owner?.WorldObject != null)
            .GroupBy(owner => owner.WorldObject.Identity).All(group => group.Count() == 1),
            "one sprite presentation per persistent identity");
        Check(navigation.Player == lifecycle.Presentation, "navigation remains bound only to production PC");
    }

    private static int CountIdentity(ArcanumObjectId identity)
        => Object.FindObjectsByType<WorldObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Count(value => value.Identity == identity);

    private static int Count<T>() where T : Object
        => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

    private static ArcanumObjectId ParseIdentity(string key)
    {
        if (!ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId identity))
            throw new InvalidOperationException("Invalid validation ObjectID: " + key);
        return identity;
    }

    private static void Refresh(out WorldObjectSectorLoader loader, out ProductionPlayerLifecycle lifecycle,
        out PlayerNavigationController navigation, out PlayerInteractionController interaction,
        out ProductionSaveLoadPresenter savePresenter)
    {
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
        interaction = Object.FindFirstObjectByType<PlayerInteractionController>();
        savePresenter = Object.FindFirstObjectByType<ProductionSaveLoadPresenter>();
        Check(loader != null && lifecycle != null && navigation != null && interaction != null
              && savePresenter != null, "production TestTerrain composition remains available");
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("M8A validation FAIL: " + label);
    }

    private static void Track(string _, string __, LogType type)
    {
        if (type == LogType.Warning) _warnings++;
        else if (type is LogType.Error or LogType.Assert or LogType.Exception) _errors++;
    }
}
