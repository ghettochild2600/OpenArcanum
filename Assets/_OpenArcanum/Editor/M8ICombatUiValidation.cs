using System;
using System.Collections;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M8ICombatUiValidation
{
    private const string EquipmentSector = "maps/arcanum1-024-fixed/101602821844.sec";
    private const string CombatSector = "maps/arcanum1-024-fixed/47781512457.sec";
    private const string BowKey = "G_1575DBCA_4990_C243_8184_524D51F7D533";
    private const string ArrowKey = "G_FBFA4631_D97D_D740_9636_F131B2FD9F7B";
    private const string BearKey = "G_9B807B01_A142_4949_80CE_5A085F3BEEB1";
    private static readonly ArcanumObjectId BowIdentity = ParseIdentity(BowKey);
    private static readonly ArcanumObjectId ArrowIdentity = ParseIdentity(ArrowKey);
    private static readonly ArcanumObjectId BearIdentity = ParseIdentity(BearKey);
    private static bool _running;
    private static int _warnings;
    private static int _errors;

    [MenuItem("OpenArcanum/M8I/Run Physical PlayMode Validation")]
    private static void Run()
    {
        if (!Application.isPlaying || _running)
            throw new InvalidOperationException("Enter TestTerrain Play mode; run only one M8I harness.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>()
                                         ?? throw new InvalidOperationException("TestTerrain has no world-object loader.");
        loader.StartCoroutine(Validate(loader));
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        _running = true;
        _warnings = _errors = 0;
        GraphicsMode initialMode = OpenArcanumGraphicsSettings.Mode;
        WorldMapSessionCoordinator session = loader.Session;
        string baselineJson = null;
        Application.logMessageReceived += Track;
        try
        {
            session.ResetAuthoritativeSession();
            yield return null;
            Check(session.SelectSector(EquipmentSector), "authentic equipment sector loads");
            yield return null;
            Refresh(out loader, out ProductionPlayerLifecycle lifecycle,
                out ProductionCombatPresenter presenter);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC binds");
            ArcanumObjectId pc = session.PlayerState.Identity;
            PersistentObjectState bow = RequireState(session, BowIdentity, ObjectType.Weapon, 6055);
            PersistentObjectState arrows = RequireState(session, ArrowIdentity, ObjectType.Ammo, 7058);
            MoveOwnedItem(session, bow, pc);
            MoveOwnedItem(session, arrows, pc);
            Check(session.EquipItem(pc, BowIdentity, WornLocation.Weapon).Succeeded,
                "authentic Bow equips through production authority");
            Check(session.SelectSector(CombatSector), "authentic Polar Bear Cub sector loads");
            yield return null;
            Refresh(out loader, out lifecycle, out presenter);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC rebinds");
            Check(session.TryGetLoadedObject(BearIdentity, out WorldObject bearRuntime),
                "authentic Polar Bear Cub is loaded");
            WorldObject pcRuntime = lifecycle.Presentation;
            baselineJson = session.SaveGames.SerializeCurrentSession();
            CombatUiController ui = presenter.Controller;
            CombatStateService combat = session.Combat;

            Vector2Int meleeTile = FindMeleeTile(loader.NavigationMap, bearRuntime.Tile);
            MoveActor(session, loader, pc, pcRuntime, meleeTile, true);
            Check(session.UnequipItem(pc, WornLocation.Weapon).Succeeded,
                "Bow unequips for production UI melee proof");
            Check(combat.StartCombat(pc, BearIdentity, CombatMode.TurnBased).Succeeded,
                "turn-based combat starts through production authority");
            while (combat.CurrentParticipant != pc)
                Check(combat.EndCurrentTurn(combat.CurrentParticipant).Succeeded,
                    "production turn authority reaches the PC");
            ui.Refresh();
            Check(ui.Mode == CombatMode.TurnBased && ui.CurrentActor == pc
                  && ui.CurrentActionPoints == combat.CurrentActionPoints,
                "UI projects authoritative current actor and AP");
            Check(ui.SelectTarget(BearIdentity) && ui.SelectedTarget == BearIdentity,
                "UI selects the authentic hostile by stable identity");
            ui.SetAttackMode(CombatAttackMode.BasicMelee);
            ui.SetCalledLocation(CombatCalledLocation.Torso);
            combat.SetRandomSource(new SequenceRandom(1, 100, 4, 4));
            int meleeAp = combat.CurrentActionPoints;
            Check(ui.SubmitAttack() == CombatFailure.None && ui.LastResult?.Succeeded == true
                  && ui.LastResult?.Request.Mode == CombatAttackMode.BasicMelee
                  && ui.Feedback == "Hit" && combat.CurrentActionPoints == meleeAp - 5,
                "UI melee command uses production authority and projects Hit/AP result");
            Check(ui.EndTurn() == CombatFailure.None && combat.CurrentParticipant != pc,
                "UI End Turn command delegates to authoritative turn progression");
            while (combat.CurrentParticipant != pc)
                Check(combat.EndCurrentTurn(combat.CurrentParticipant).Succeeded,
                    "production turn authority returns to the PC");

            Check(session.EquipItem(pc, BowIdentity, WornLocation.Weapon).Succeeded,
                "authentic Bow re-equips for UI ranged proof");
            Vector2Int rangedTile = FindClearRangedTile(loader.NavigationMap, bearRuntime.Tile, 3);
            MoveActor(session, loader, pc, pcRuntime, rangedTile, true);
            ui.SetAttackMode(CombatAttackMode.BasicRanged);
            ui.SetCalledLocation(CombatCalledLocation.Arm);
            ui.Refresh();
            Check(ui.Preview.Succeeded
                  && ui.Preview.ModifierLedger.Entries.Any(value =>
                      value.Reason == CombatAttackModifierReason.CalledLocation
                      && value.Value == -30)
                  && ui.Preview.FinalEffectiveAttackValue == ui.Preview.Chance.AttackChance,
                "UI effectiveness and chance are projected from the authoritative modifier ledger");
            int ammoBefore = arrows.StackQuantity.Value;
            combat.SetRandomSource(new SequenceRandom(100, 100));
            Check(ui.SubmitAttack() == CombatFailure.None
                  && ui.LastResult?.Request.Mode == CombatAttackMode.BasicRanged
                  && ui.LastResult?.RequestedLocation == CombatCalledLocation.Arm
                  && ui.AmmoQuantity == ammoBefore - 1 && ui.Feedback == "Miss",
                "UI Bow/called-location command consumes one arrow and projects Miss");

            ArcanumObjectId selected = ui.SelectedTarget;
            CombatAttackMode selectedMode = ui.AttackMode;
            CombatCalledLocation selectedLocation = ui.CalledLocation;
            foreach (GraphicsMode mode in new[] { GraphicsMode.Original, GraphicsMode.Enhanced, GraphicsMode.Original })
            {
                OpenArcanumGraphicsSettings.SetRuntimeMode(mode);
                loader.RebuildVisuals();
                ui.Refresh();
                Check(ui.SelectedTarget == selected && ui.AttackMode == selectedMode
                      && ui.CalledLocation == selectedLocation,
                    $"{mode} rebuild preserves UI selection and sends no combat command");
            }

            ArcanumObjectId[] hostiles = combat.Participants
                .Where(value => value.Identity != pc)
                .Select(value => value.Identity)
                .ToArray();
            foreach (ArcanumObjectId hostile in hostiles)
                Check(combat.RemoveParticipant(hostile).Succeeded,
                    "each active hostile leaves through roster authority before voluntary combat exit");
            Check(combat.EndCombat(pc).Succeeded, "turn-based combat ends cleanly");
            ui.Refresh();
            Check(!ui.HasSelectedTarget && ui.Feedback == string.Empty,
                "combat teardown clears transient UI state");

            Check(combat.StartCombat(pc, BearIdentity, CombatMode.RealTime).Succeeded,
                "real-time combat starts through production authority");
            ui.Refresh();
            Check(ui.Mode == CombatMode.RealTime && ui.IsRealTimeReady,
                "UI projects real-time READY state");
            Check(ui.SelectTarget(BearIdentity), "real-time UI reselects authentic target");
            ui.SetAttackMode(CombatAttackMode.BasicRanged);
            ui.SetCalledLocation(CombatCalledLocation.Torso);
            combat.SetRandomSource(new SequenceRandom(100, 100));
            Check(ui.SubmitAttack() == CombatFailure.None, "real-time UI schedules Bow action");
            ui.Refresh();
            Check(ui.IsRealTimeBusy && ui.SubmitAttack() == CombatFailure.ActorBusy
                  && ui.Feedback == "Actor is busy.",
                "UI projects BUSY and authority rejects a second command");
            Check(combat.TryGetRealTimeActorState(pc, out CombatRealTimeActorState pending),
                "real-time production scheduler exposes pending action");
            long remaining = pending.PendingAction.EffectAtMilliseconds
                             - combat.ElapsedCombatTimeMilliseconds;
            Check(combat.AdvanceRealTime((int)remaining).Succeeded,
                "real-time source clock reaches the scheduled effect");
            ui.Refresh();
            Check(ui.LastResult?.Succeeded == true && ui.Feedback == "Miss"
                  && ui.AmmoQuantity == ammoBefore - 2,
                "UI projects resolved real-time result and authoritative ammo update");

            string activeJson = session.SaveGames.SerializeCurrentSession();
            Check(session.SaveGames.LoadJson(activeJson).Succeeded, "Save V1 reload succeeds during combat");
            yield return null;
            Refresh(out loader, out lifecycle, out presenter);
            presenter.Controller.Refresh();
            Check(!session.Combat.IsActive && !presenter.Controller.HasSelectedTarget
                  && presenter.Controller.LastResult == null,
                "Save V1 normalizes combat and all transient UI selection/result state");

            Check(session.SaveGames.LoadJson(baselineJson).Succeeded,
                "pre-validation authoritative baseline restores");
            yield return null;
            Refresh(out loader, out lifecycle, out presenter);
            Check(!session.Combat.IsActive && !session.Vitality.IsDead(BearIdentity),
                "physical validation cleanup restores authentic noncombat state");

            Application.logMessageReceived -= Track;
            Check(_warnings == 0 && _errors == 0, $"warnings={_warnings}; errors={_errors}");
            Debug.Log("M8I PLAYMODE VALIDATION PASS: fixtures=production PC+Polar Bear Cub+Bow+arrows; "
                      + "turnBased=actor+AP+target+melee+Bow+calledLocation+ledger+ammo+EndTurn; "
                      + "results=Hit+Miss; realTime=READY+BUSY+authorityRejection+resolvedResult; "
                      + "presentation=Original->Enhanced->Original-independent; "
                      + "saveV1=transient-UI-normalized; teardown=clean; warnings=0; errors=0.");
        }
        finally
        {
            session.Combat.ResetRandomSource();
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            Application.logMessageReceived -= Track;
            if (baselineJson != null && session.Combat.IsActive)
                session.SaveGames.LoadJson(baselineJson);
            _running = false;
        }
    }

    private static Vector2Int FindMeleeTile(SectorNavigationMap map, Vector2Int target)
    {
        foreach (Vector2Int delta in IsoProjection.DirDelta)
        {
            Vector2Int candidate = target + delta;
            if (map.IsWalkable(candidate)) return candidate;
        }
        throw new InvalidOperationException("M8I validation FAIL: no melee tile exists.");
    }

    private static Vector2Int FindClearRangedTile(SectorNavigationMap map, Vector2Int target, int distance)
    {
        for (int y = Math.Max(0, target.y - distance); y <= Math.Min(63, target.y + distance); y++)
        for (int x = Math.Max(0, target.x - distance); x <= Math.Min(63, target.x + distance); x++)
        {
            var candidate = new Vector2Int(x, y);
            if (InteractionRangeRules.Distance(candidate, target) == distance && map.IsWalkable(candidate)
                && map.HasProjectileLineOfFire(candidate, target)) return candidate;
        }
        throw new InvalidOperationException("M8I validation FAIL: no clear ranged tile exists.");
    }

    private static void MoveOwnedItem(WorldMapSessionCoordinator session, PersistentObjectState item,
        ArcanumObjectId owner)
    {
        if (item.Placement.Kind == ObjectPlacementKind.Equipped)
            Check(session.UnequipItem(item.Placement.ParentIdentity, item.Placement.WornLocation).Succeeded,
                "equipped item unequips before transfer");
        if (item.Placement != ObjectPlacement.ContainedBy(owner))
            Check(session.TransferItem(item.Identity, item.Placement,
                ObjectPlacement.ContainedBy(owner)).Succeeded, "item transfers through authority");
    }

    private static void MoveActor(WorldMapSessionCoordinator session, WorldObjectSectorLoader loader,
        ArcanumObjectId identity, WorldObject runtime, Vector2Int tile, bool controlled)
    {
        loader.NavigationMap.Unregister(runtime);
        Check(session.SetMovementState(identity, tile, runtime.ArtId, false),
            "authoritative movement accepts validation placement");
        loader.NavigationMap.Register(runtime, runtime.SourceFlags);
        if (controlled) loader.NavigationMap.SetControlledObject(runtime);
    }

    private static PersistentObjectState RequireState(WorldMapSessionCoordinator session,
        ArcanumObjectId identity, ObjectType type, int prototype)
    {
        Check(session.States.TryGetValue(identity, out PersistentObjectState state)
              && state.Type == type && state.PrototypeNumber == prototype,
            $"authentic {type} prototype {prototype} resolves by ObjectID");
        return state;
    }

    private static ArcanumObjectId ParseIdentity(string key)
    {
        if (!ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId identity))
            throw new InvalidOperationException("Invalid validation ObjectID: " + key);
        return identity;
    }

    private static void Refresh(out WorldObjectSectorLoader loader,
        out ProductionPlayerLifecycle lifecycle, out ProductionCombatPresenter presenter)
    {
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        presenter = Object.FindFirstObjectByType<ProductionCombatPresenter>();
        Check(loader != null && lifecycle != null && presenter != null && presenter.Controller != null,
            "production TestTerrain composition includes the combat presenter");
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("M8I validation FAIL: " + label);
    }

    private static void Track(string _, string __, LogType type)
    {
        if (type == LogType.Warning) _warnings++;
        else if (type is LogType.Error or LogType.Assert or LogType.Exception) _errors++;
    }

    private sealed class SequenceRandom : ICombatRandom
    {
        private readonly System.Collections.Generic.Queue<int> _values;
        public SequenceRandom(params int[] values)
            => _values = new System.Collections.Generic.Queue<int>(values);

        public int NextInclusive(int minimum, int maximum)
        {
            Check(_values.Count > 0, "combat requests only expected deterministic RNG samples");
            int value = _values.Dequeue();
            Check(value >= minimum && value <= maximum, "RNG sample is inside source bounds");
            return value;
        }
    }
}
