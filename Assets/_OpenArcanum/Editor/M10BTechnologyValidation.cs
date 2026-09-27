using System;
using System.Collections;
using System.Collections.Generic;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.Technology;
using Arcanum.Runtime.World;
using Arcanum.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M10BTechnologyValidation
{
    private const string CombatSector = "maps/arcanum1-024-fixed/47781512457.sec";
    private const string BearKey = "G_9B807B01_A142_4949_80CE_5A085F3BEEB1";
    private static readonly ArcanumObjectId BearIdentity = ParseIdentity(BearKey);
    private static bool _running;
    private static int _warnings;
    private static int _errors;

    [MenuItem("OpenArcanum/M10B/Run Physical PlayMode Validation", false, 0)]
    private static void Run()
    {
        if (!Application.isPlaying || _running)
            throw new InvalidOperationException("Enter TestTerrain Play mode; run only one M10B harness.");
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
        string baseline = null;
        Application.logMessageReceived += Track;
        try
        {
            session.ResetAuthoritativeSession();
            yield return null;
            Check(session.SelectSector(CombatSector), "authentic Polar Bear Cub sector loads");
            yield return null;
            Refresh(out loader, out ProductionPlayerLifecycle lifecycle);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC binds");
            Check(session.TryGetLoadedObject(BearIdentity, out WorldObject bearRuntime),
                "authentic Polar Bear Cub presentation is loaded");
            ArcanumObjectId pc = session.PlayerState.Identity;
            WorldObject pcRuntime = lifecycle.Presentation;
            baseline = session.SaveGames.SerializeCurrentSession();
            MoveActor(session, loader, pc, pcRuntime, FindClearTile(loader.NavigationMap, bearRuntime.Tile, 1));

            Check(session.Technology.GetLearnedDegree(pc, TechnologyDiscipline.Herbology)
                  == TechnologyDegree.Layman
                  && !session.Technology.KnowsBuiltInSchematic(pc,
                      TechnologyDiscipline.Herbology, TechnologyDegree.Novice),
                "source PC begins at Layman without the Novice Herbology schematic");
            int points = session.Progression.GetUnspentCharacterPoints(pc);
            int aptitude = session.DerivedStats.GetDerivedStat(pc,
                CharacterDerivedStat.MagickTechAptitude);
            TechnologyLearningResult learned = session.Technology.LearnNextDegree(
                pc, TechnologyDiscipline.Herbology);
            Check(learned.Succeeded && learned.Degree == TechnologyDegree.Novice
                  && session.Progression.GetUnspentCharacterPoints(pc) == points - 1
                  && session.DerivedStats.GetDerivedStat(pc,
                      CharacterDerivedStat.MagickTechAptitude) < aptitude
                  && session.Technology.KnowsBuiltInSchematic(pc,
                      TechnologyDiscipline.Herbology, TechnologyDegree.Novice),
                "authentic sequential Novice eligibility spends one point and updates M4 aptitude");

            ItemCreationResult salve = session.CreateItem(PhaseOneTechnologyCatalog.HealingSalvePrototype,
                ObjectPlacement.ContainedBy(pc));
            Check(salve.Succeeded && salve.State.Type == ObjectType.Food,
                "retail Healing Salve prototype 10079 enters M3 containment");
            int fullHp = session.Vitality.GetCurrentHitPoints(BearIdentity);
            Check(session.Technology.Use(new TechnologyUseRequest(pc,
                      salve.State.Identity, BearIdentity)).Failure == TechnologyUseFailure.InvalidTarget
                  && session.TryGetObjectState(salve.State.Identity, out _)
                  && session.Vitality.GetCurrentHitPoints(BearIdentity) == fullHp,
                "full-health invalid use rejects before item or vitality mutation");

            session.Vitality.ApplyHitPointDamage(BearIdentity, 25);
            Check(session.Combat.StartCombat(pc, BearIdentity).Succeeded,
                "turn-based combat starts with production actors");
            AdvanceToPc(session.Combat, pc);
            int ap = session.Combat.CurrentActionPoints;
            int damagedHp = session.Vitality.GetCurrentHitPoints(BearIdentity);
            TechnologyUseResult healed = session.Technology.Use(new TechnologyUseRequest(pc,
                salve.State.Identity, BearIdentity));
            Check(healed.Succeeded && healed.ActionPointCost == 4 && healed.Magnitude == 20
                  && healed.ChargesBefore == 1 && healed.ChargesAfter == 0 && healed.ItemConsumed
                  && session.Combat.CurrentActionPoints == ap - 4
                  && session.Vitality.GetCurrentHitPoints(BearIdentity) == damagedHp + 20
                  && !session.TryGetObjectState(salve.State.Identity, out _),
                "Healing Salve heals exact 20 through M4, costs 4 AP, and depletes one source charge");
            EndCombatCleanly(session.Combat, pc, "turn-based technology proof ends cleanly");

            ItemCreationResult realTimeSalve = session.CreateItem(
                PhaseOneTechnologyCatalog.HealingSalvePrototype, ObjectPlacement.ContainedBy(pc));
            session.Vitality.ApplyHitPointDamage(BearIdentity, 20);
            Check(realTimeSalve.Succeeded
                  && session.Combat.StartCombat(pc, BearIdentity, CombatMode.RealTime).Succeeded,
                "real-time technology fixture and combat start through production authority");
            var realTimeRequest = new TechnologyUseRequest(pc,
                realTimeSalve.State.Identity, BearIdentity);
            Check(loader.TryGetTiming(new CombatRealTimeTimingRequest(pc,
                      CombatRealTimeActionKind.TechnologyUse, technology: realTimeRequest),
                      out CombatRealTimeTiming timing),
                "retail PC ART supplies production technology-use timing");
            int realTimeHp = session.Vitality.GetCurrentHitPoints(BearIdentity);
            Check(session.Combat.ScheduleRealTimeTechnology(realTimeRequest).Succeeded
                  && session.Combat.ScheduleRealTimeTechnology(realTimeRequest).Failure
                  == CombatFailure.ActorBusy,
                "real-time technology enters BUSY once and rejects duplicate scheduling");
            Check(session.Combat.TryGetRealTimeActorState(pc, out CombatRealTimeActorState busy)
                  && busy.HasPendingAction && !busy.IsReady
                  && busy.PendingAction.Kind == CombatRealTimeActionKind.TechnologyUse,
                "real-time technology enters BUSY once and rejects duplicate scheduling");
            int toEffect = checked((int)(busy.PendingAction.EffectAtMilliseconds
                                         - session.Combat.ElapsedCombatTimeMilliseconds));
            if (toEffect > 0)
            {
                Check(session.Combat.AdvanceRealTime(toEffect - 1).Succeeded
                      && session.Vitality.GetCurrentHitPoints(BearIdentity) == realTimeHp,
                    "real-time technology has no early mutation");
                Check(session.Combat.AdvanceRealTime(1).Succeeded,
                    "real-time technology reaches the exact source effect frame");
            }
            else Check(session.Combat.AdvanceRealTime(0).Succeeded,
                "zero effect-frame technology resolves deterministically");
            Check(session.Vitality.GetCurrentHitPoints(BearIdentity) == realTimeHp + 20
                  && !session.TryGetObjectState(realTimeSalve.State.Identity, out _)
                  && session.Combat.LastRealTimeActionResolution?.TechnologyResult?.Succeeded == true,
                "real-time technology resolves healing and depletion exactly once");
            int toReady = checked((int)(busy.PendingAction.ReadyAtMilliseconds
                                        - session.Combat.ElapsedCombatTimeMilliseconds));
            Check(session.Combat.AdvanceRealTime(toReady).Succeeded
                  && session.Combat.TryGetRealTimeActorState(pc, out CombatRealTimeActorState ready)
                  && ready.IsReady, "technology user becomes READY at production recovery completion");
            EndCombatCleanly(session.Combat, pc, "real-time technology proof ends cleanly");

            ItemCreationResult pendingSalve = session.CreateItem(
                PhaseOneTechnologyCatalog.HealingSalvePrototype, ObjectPlacement.ContainedBy(pc));
            session.Vitality.ApplyHitPointDamage(BearIdentity, 20);
            Check(pendingSalve.Succeeded
                  && session.Combat.StartCombat(pc, BearIdentity, CombatMode.RealTime).Succeeded,
                "pending save fixture enters real-time combat");
            var pendingRequest = new TechnologyUseRequest(pc,
                pendingSalve.State.Identity, BearIdentity);
            Check(session.Combat.ScheduleRealTimeTechnology(pendingRequest).Succeeded,
                "one transient technology use is pending before rebuild and save");
            int committedHp = session.Vitality.GetCurrentHitPoints(BearIdentity);
            foreach (GraphicsMode mode in new[]
                     { GraphicsMode.Original, GraphicsMode.Enhanced, GraphicsMode.Original })
            {
                OpenArcanumGraphicsSettings.SetRuntimeMode(mode);
                loader.RebuildVisuals();
                Check(session.Technology.GetLearnedDegree(pc, TechnologyDiscipline.Herbology)
                      == TechnologyDegree.Novice
                      && session.Combat.TryGetRealTimeActorState(pc, out CombatRealTimeActorState state)
                      && state.PendingAction.Kind == CombatRealTimeActionKind.TechnologyUse
                      && session.TryGetObjectState(pendingSalve.State.Identity, out _),
                    $"{mode} presentation rebuild preserves technology authority");
            }

            int savedTechPoints = session.DerivedStats.GetEffectiveTechPoints(pc);
            string technologySave = session.SaveGames.SerializeCurrentSession();
            Check(session.SaveGames.LoadJson(technologySave).Succeeded,
                "technology V1 save reload succeeds");
            yield return null;
            Refresh(out loader, out lifecycle);
            Check(!session.Combat.IsActive
                  && session.Technology.GetLearnedDegree(pc, TechnologyDiscipline.Herbology)
                  == TechnologyDegree.Novice
                  && session.DerivedStats.GetEffectiveTechPoints(pc) == savedTechPoints
                  && session.TryGetObjectState(pendingSalve.State.Identity, out _)
                  && session.Vitality.GetCurrentHitPoints(BearIdentity) == committedHp,
                "V1 restores rank, aptitude, item, and vitality but normalizes pending technology/combat");

            Check(session.TryGetLoadedObject(BearIdentity, out bearRuntime),
                "bear presentation is restored after save/load");
            pcRuntime = lifecycle.Presentation;
            MoveActor(session, loader, pc, pcRuntime, FindClearTile(loader.NavigationMap, bearRuntime.Tile, 1));
            session.Characters.SetRace(pc, CharacterRace.Elf);
            ItemCreationResult powerAxe = session.CreateItem(PhaseOneTechnologyCatalog.PowerAxePrototype,
                ObjectPlacement.ContainedBy(pc));
            Check(powerAxe.Succeeded
                  && session.EquipItem(pc, powerAxe.State.Identity, WornLocation.Weapon).Succeeded,
                "retail Power Axe prototype 6088 equips through M3");
            int failureChance = session.Technology.GetItemAptitudeCriticalFailureChance(
                powerAxe.State, pc);
            Check(failureChance > 0, "positive Magick aptitude exposes source technological malfunction chance");
            Check(session.Combat.StartCombat(pc, BearIdentity).Succeeded,
                "Power Axe proof starts turn-based combat");
            AdvanceToPc(session.Combat, pc);
            int pcHp = session.Vitality.GetCurrentHitPoints(pc);
            int bearHp = session.Vitality.GetCurrentHitPoints(BearIdentity);
            int axeAp = session.Combat.CurrentActionPoints;
            session.Combat.SetRandomSource(new SequenceRandom(1, 100, 1, 100));
            CombatAttackResult malfunction = session.Combat.Attack(pc, BearIdentity);
            Check(malfunction.Succeeded && malfunction.Outcome == CombatAttackOutcome.CriticalFailure
                  && malfunction.CriticalEffect == CombatCriticalEffect.SelfHit
                  && malfunction.EffectTargetIdentity == pc
                  && session.Combat.CurrentActionPoints == axeAp - malfunction.ActionPointCost
                  && session.Vitality.GetCurrentHitPoints(pc) < pcHp
                  && session.Vitality.GetCurrentHitPoints(BearIdentity) == bearHp,
                "Power Axe aptitude malfunction reuses the M8 critical-failure transaction");
            EndCombatCleanly(session.Combat, pc, "Power Axe technology proof ends cleanly");

            Check(session.SaveGames.LoadJson(baseline).Succeeded,
                "validation cleanup restores authoritative baseline");
            yield return null;
            Application.logMessageReceived -= Track;
            Check(_warnings == 0 && _errors == 0, $"warnings={_warnings}; errors={_errors}");
            Debug.Log("M10B PHYSICAL VALIDATION PASS: eligibility=Layman->Novice+point+M4-aptitude; "
                      + "HealingSalve=proto10079+invalid-transactional+heal20+AP4+charge1-depleted; "
                      + "realTime=productionART+BUSY/READY+exactlyOnce; PowerAxe=proto6088+M8-self-hit; "
                      + "presentation=Original->Enhanced->Original-independent; "
                      + $"saveV1=rank+aptitude+item+vitality-restored+pending-normalized; warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            session.Combat.ResetRandomSource();
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            Application.logMessageReceived -= Track;
            if (baseline != null && session.PlayerState != null && session.HasSelectedSector)
                session.SaveGames.LoadJson(baseline);
            _running = false;
        }
    }

    private static void AdvanceToPc(CombatStateService combat, ArcanumObjectId pc)
    {
        while (combat.CurrentParticipant != pc)
            Check(combat.EndCurrentTurn(combat.CurrentParticipant).Succeeded,
                "turn authority advances to production PC");
    }

    private static Vector2Int FindClearTile(SectorNavigationMap map, Vector2Int target, int distance)
    {
        for (int y = Math.Max(0, target.y - distance); y <= Math.Min(63, target.y + distance); y++)
        for (int x = Math.Max(0, target.x - distance); x <= Math.Min(63, target.x + distance); x++)
        {
            var candidate = new Vector2Int(x, y);
            if (InteractionRangeRules.Distance(candidate, target) == distance && map.IsWalkable(candidate))
                return candidate;
        }
        throw new InvalidOperationException("M10B validation FAIL: no adjacent clear tile exists.");
    }

    private static void EndCombatCleanly(CombatStateService combat, ArcanumObjectId pc, string label)
    {
        var hostiles = new List<ArcanumObjectId>();
        foreach (CombatParticipant participant in combat.Participants)
            if (participant.Identity != pc) hostiles.Add(participant.Identity);
        foreach (ArcanumObjectId hostile in hostiles)
            Check(combat.RemoveParticipant(hostile).Succeeded,
                "validation hostile leaves combat authority");
        Check(combat.EndCombat(pc).Succeeded, label);
    }

    private static void MoveActor(WorldMapSessionCoordinator session, WorldObjectSectorLoader loader,
        ArcanumObjectId identity, WorldObject runtime, Vector2Int tile)
    {
        loader.NavigationMap.Unregister(runtime);
        Check(session.SetMovementState(identity, tile, runtime.ArtId, false),
            "authoritative movement accepts validation placement");
        loader.NavigationMap.Register(runtime, runtime.SourceFlags);
        loader.NavigationMap.SetControlledObject(runtime);
    }

    private static void Refresh(out WorldObjectSectorLoader loader,
        out ProductionPlayerLifecycle lifecycle)
    {
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        Check(loader != null && lifecycle != null,
            "production TestTerrain composition remains available");
    }

    private static ArcanumObjectId ParseIdentity(string key)
    {
        if (!ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId identity))
            throw new InvalidOperationException("Invalid validation ObjectID: " + key);
        return identity;
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("M10B validation FAIL: " + label);
    }

    private static void Track(string _, string __, LogType type)
    {
        if (type == LogType.Warning) _warnings++;
        else if (type is LogType.Error or LogType.Assert or LogType.Exception) _errors++;
    }

    private sealed class SequenceRandom : ICombatRandom
    {
        private readonly Queue<int> _values;
        internal SequenceRandom(params int[] values) => _values = new Queue<int>(values);
        public int NextInclusive(int minimum, int maximum)
        {
            int value = _values.Count == 0 ? minimum : _values.Dequeue();
            Check(value >= minimum && value <= maximum,
                "deterministic combat RNG sample is within source bounds");
            return value;
        }
    }
}
