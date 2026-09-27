using System;
using System.Collections;
using System.Collections.Generic;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.Magic;
using Arcanum.Runtime.World;
using Arcanum.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M10AMagicValidation
{
    private const string CombatSector = "maps/arcanum1-024-fixed/47781512457.sec";
    private const string BearKey = "G_9B807B01_A142_4949_80CE_5A085F3BEEB1";
    private static readonly ArcanumObjectId BearIdentity = ParseIdentity(BearKey);
    private static bool _running;
    private static int _warnings;
    private static int _errors;

    [MenuItem("OpenArcanum/M10A/Run Physical PlayMode Validation", false, 0)]
    private static void Run()
    {
        if (!Application.isPlaying || _running)
            throw new InvalidOperationException("Enter TestTerrain Play mode; run only one M10A harness.");
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
            Vector2Int castingTile = FindClearTile(loader.NavigationMap, bearRuntime.Tile, 3);
            MoveActor(session, loader, pc, pcRuntime, castingTile);

            session.Magic.SetKnownCollegeRank(pc, SpellCollege.Earth, 1);
            session.Magic.SetKnownCollegeRank(pc, SpellCollege.NecromanticBlack, 1);
            session.Magic.SetKnownCollegeRank(pc, SpellCollege.NecromanticWhite, 1);
            session.Magic.SetRandomSource(new SequenceMagicRandom(99, 99, 99, 99, 99));
            baseline = session.SaveGames.SerializeCurrentSession();

            int invalidFatigue = session.Vitality.GetCurrentFatigue(pc);
            int invalidHitPoints = session.Vitality.GetCurrentHitPoints(BearIdentity);
            Check(session.Magic.Cast(new SpellCastRequest(pc, 999, BearIdentity)).Failure
                  == SpellCastFailure.InvalidSpell, "unknown spell is rejected");
            Check(session.Vitality.GetCurrentFatigue(pc) == invalidFatigue
                  && session.Vitality.GetCurrentHitPoints(BearIdentity) == invalidHitPoints,
                "invalid cast rejects before fatigue and vitality mutation");

            Check(session.Combat.StartCombat(pc, BearIdentity).Succeeded,
                "turn-based combat starts with production actors");
            while (session.Combat.CurrentParticipant != pc)
                Check(session.Combat.EndCurrentTurn(session.Combat.CurrentParticipant).Succeeded,
                    "turn authority advances to the production PC");
            int turnAp = session.Combat.CurrentActionPoints;
            int turnFatigue = session.Vitality.GetCurrentFatigue(pc);
            int turnTargetHp = session.Vitality.GetCurrentHitPoints(BearIdentity);
            SpellCastResult turnHarm = session.Magic.Cast(
                new SpellCastRequest(pc, PhaseOneSpellCatalog.Harm, BearIdentity));
            Check(turnHarm.Succeeded && turnHarm.ActionPointCost == 4 && turnHarm.FatigueCost == 5
                  && session.Combat.CurrentActionPoints == turnAp - 4
                  && session.Combat.CurrentParticipant == pc,
                "turn-based Harm consumes exact source 4 AP and 5 fatigue without forcing turn end");
            Check(session.Vitality.GetCurrentFatigue(pc) == turnFatigue - 5
                  && session.Vitality.GetCurrentHitPoints(BearIdentity) == turnTargetHp - turnHarm.Magnitude,
                "turn-based Harm resolves through authoritative fatigue and vitality");
            EndCombatCleanly(session.Combat, pc, "turn-based proof ends cleanly");

            int damagedPc = session.Vitality.GetCurrentHitPoints(pc) - 8;
            session.Vitality.ApplyHitPointDamage(pc, 8);
            SpellCastResult healing = session.Magic.Cast(
                new SpellCastRequest(pc, PhaseOneSpellCatalog.MinorHealing, pc));
            Check(healing.Succeeded && healing.FatigueCost == 5
                  && session.Vitality.GetCurrentHitPoints(pc)
                  == Math.Min(session.Vitality.GetMaximumHitPoints(pc), damagedPc + healing.Magnitude),
                "authentic Minor Healing uses M4 vitality and caps at maximum");

            Check(session.Combat.StartCombat(pc, BearIdentity, CombatMode.RealTime).Succeeded,
                "real-time combat starts with production actors");
            var realTimeRequest = new SpellCastRequest(pc, PhaseOneSpellCatalog.Harm, BearIdentity);
            Check(loader.TryGetTiming(new CombatRealTimeTimingRequest(pc,
                      CombatRealTimeActionKind.SpellCast, spell: realTimeRequest), out CombatRealTimeTiming timing),
                "retail PC ART supplies production AG_THROW_SPELL timing");
            int realTimeFatigue = session.Vitality.GetCurrentFatigue(pc);
            int realTimeHp = session.Vitality.GetCurrentHitPoints(BearIdentity);
            Check(session.Combat.ScheduleRealTimeSpell(realTimeRequest).Succeeded,
                "Harm schedules through M8H");
            Check(session.Combat.TryGetRealTimeActorState(pc, out CombatRealTimeActorState busy)
                  && busy.HasPendingAction && !busy.IsReady
                  && busy.PendingAction.Kind == CombatRealTimeActionKind.SpellCast
                  && busy.PendingAction.EffectAtMilliseconds - busy.PendingAction.StartedAtMilliseconds
                  == timing.EffectDelayMilliseconds
                  && busy.PendingAction.ReadyAtMilliseconds - busy.PendingAction.StartedAtMilliseconds
                  == timing.ReadyDelayMilliseconds,
                "BUSY action exposes exact production effect and recovery timing");
            int toEffect = checked((int)(busy.PendingAction.EffectAtMilliseconds
                                         - session.Combat.ElapsedCombatTimeMilliseconds));
            if (toEffect > 0)
            {
                Check(session.Combat.AdvanceRealTime(toEffect - 1).Succeeded,
                    "real-time clock stops one millisecond before the spell effect");
                Check(session.Vitality.GetCurrentFatigue(pc) == realTimeFatigue
                      && session.Vitality.GetCurrentHitPoints(BearIdentity) == realTimeHp,
                    "real-time spell has no early mutation");
                Check(session.Combat.AdvanceRealTime(1).Succeeded,
                    "real-time clock reaches the exact source effect frame");
            }
            else Check(session.Combat.AdvanceRealTime(0).Succeeded,
                "zero action-frame spell resolves deterministically");
            Check(session.Vitality.GetCurrentFatigue(pc) == realTimeFatigue - 5
                  && session.Vitality.GetCurrentHitPoints(BearIdentity) == realTimeHp - 3
                  && session.Combat.LastRealTimeActionResolution?.SpellResult?.Succeeded == true,
                "real-time Harm resolves exactly once at the M8H effect boundary");
            int toReady = checked((int)(busy.PendingAction.ReadyAtMilliseconds
                                        - session.Combat.ElapsedCombatTimeMilliseconds));
            Check(session.Combat.AdvanceRealTime(toReady).Succeeded
                  && session.Combat.TryGetRealTimeActorState(pc, out CombatRealTimeActorState ready)
                  && ready.IsReady, "caster becomes READY at exact production recovery completion");
            EndCombatCleanly(session.Combat, pc, "real-time proof ends cleanly");

            int baseStrength = session.Characters.GetEffectiveAttribute(pc, CharacterAttribute.Strength);
            SpellCastResult earth = session.Magic.Cast(
                new SpellCastRequest(pc, PhaseOneSpellCatalog.StrengthOfEarth, pc));
            Check(earth.Succeeded && earth.ActiveEffectId.HasValue
                  && session.Magic.ActiveEffects.Count == 1
                  && session.Characters.GetEffectiveAttribute(pc, CharacterAttribute.Strength) == baseStrength + 4,
                "Strength of Earth creates one inspectable +4 authoritative modifier");
            int duplicateFatigue = session.Vitality.GetCurrentFatigue(pc);
            Check(session.Magic.Cast(new SpellCastRequest(pc,
                      PhaseOneSpellCatalog.StrengthOfEarth, pc)).Failure == SpellCastFailure.DuplicateEffect
                  && session.Vitality.GetCurrentFatigue(pc) == duplicateFatigue,
                "No_Stack duplicate rejects before mutation");

            Check(session.Combat.StartCombat(pc, BearIdentity, CombatMode.RealTime).Succeeded,
                "second real-time combat starts for persistence proof");
            Check(session.Combat.ScheduleRealTimeSpell(realTimeRequest).Succeeded,
                "one transient cast is pending before save");
            long elapsed = session.Magic.ElapsedMilliseconds;
            long effectId = earth.ActiveEffectId.Value;
            foreach (GraphicsMode mode in new[] { GraphicsMode.Original, GraphicsMode.Enhanced, GraphicsMode.Original })
            {
                OpenArcanumGraphicsSettings.SetRuntimeMode(mode);
                loader.RebuildVisuals();
                Check(session.Magic.ActiveEffects.Count == 1
                      && session.Magic.ActiveEffects[0].Id == effectId
                      && session.Magic.ElapsedMilliseconds == elapsed
                      && session.Characters.GetEffectiveAttribute(pc, CharacterAttribute.Strength) == baseStrength + 4,
                    $"{mode} presentation rebuild preserves magic authority");
            }

            string magicSave = session.SaveGames.SerializeCurrentSession();
            Check(session.SaveGames.LoadJson(magicSave).Succeeded, "magic V1 save reload succeeds");
            yield return null;
            Refresh(out loader, out lifecycle);
            Check(!session.Combat.IsActive && session.Magic.KnowsSpell(pc, PhaseOneSpellCatalog.Harm)
                  && session.Magic.ActiveEffects.Count == 1 && session.Magic.ActiveEffects[0].Id == effectId
                  && session.Characters.GetEffectiveAttribute(pc, CharacterAttribute.Strength) == baseStrength + 4,
                "V1 restores knowledge/effect but normalizes transient cast and combat state");

            int fatigueToUnconscious = session.Vitality.GetCurrentFatigue(pc);
            session.Vitality.ApplyFatigueDamage(pc, fatigueToUnconscious);
            session.Magic.AdvanceTime(1);
            Check(session.Magic.ActiveEffects.Count == 0
                  && session.Characters.GetEffectiveAttribute(pc, CharacterAttribute.Strength) == baseStrength,
                "maintained effect expires once when its caster becomes unavailable and reverts cleanly");

            Check(session.SaveGames.LoadJson(baseline).Succeeded,
                "living baseline restores for lethal hostile proof");
            yield return null;
            int remaining = session.Vitality.GetCurrentHitPoints(BearIdentity);
            session.Vitality.ApplyHitPointDamage(BearIdentity, remaining - 3);
            session.Magic.SetRandomSource(new SequenceMagicRandom(99));
            int xp = session.Progression.GetExperience(pc);
            SpellCastResult lethal = session.Magic.Cast(
                new SpellCastRequest(pc, PhaseOneSpellCatalog.Harm, BearIdentity));
            Check(lethal.Succeeded && session.Vitality.IsDead(BearIdentity)
                  && session.TryGetObjectState(BearIdentity, out PersistentObjectState corpse)
                  && corpse.DeathConsequencesProcessed
                  && session.Progression.GetExperience(pc) >= xp,
                "lethal Harm routes through M8D/M8E death and consequence authority");
            int committedXp = session.Progression.GetExperience(pc);
            Check(session.DeathConsequences.Process(pc, BearIdentity).Failure
                  == DeathConsequenceFailure.AlreadyProcessed
                  && session.Progression.GetExperience(pc) == committedXp,
                "lethal spell consequences cannot replay");

            Check(session.SaveGames.LoadJson(baseline).Succeeded,
                "validation cleanup restores authoritative baseline");
            yield return null;
            Application.logMessageReceived -= Track;
            Check(_warnings == 0 && _errors == 0, $"warnings={_warnings}; errors={_errors}");
            Debug.Log("M10A PHYSICAL VALIDATION PASS: spells=15,55,60; invalid=transactional; "
                      + "turnBased=AP4+fatigue5; realTime=productionART+BUSY/READY+exactlyOnce; "
                      + "beneficial=M4; hostile=M4+M8D+M8E; maintained=+4+NoStack+expiry; "
                      + "presentation=Original->Enhanced->Original-independent; "
                      + "saveV1=knowledge+effect-restored+pending-cast-normalized; "
                      + $"warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            session.Magic.ResetRandomSource();
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            Application.logMessageReceived -= Track;
            if (baseline != null && session.PlayerState != null && session.HasSelectedSector)
                session.SaveGames.LoadJson(baseline);
            _running = false;
        }
    }

    private static Vector2Int FindClearTile(SectorNavigationMap map, Vector2Int target, int distance)
    {
        for (int y = Math.Max(0, target.y - distance); y <= Math.Min(63, target.y + distance); y++)
        for (int x = Math.Max(0, target.x - distance); x <= Math.Min(63, target.x + distance); x++)
        {
            var candidate = new Vector2Int(x, y);
            if (InteractionRangeRules.Distance(candidate, target) == distance && map.IsWalkable(candidate)
                && map.HasProjectileLineOfFire(candidate, target)) return candidate;
        }
        throw new InvalidOperationException("M10A validation FAIL: no clear casting tile exists.");
    }

    private static void EndCombatCleanly(CombatStateService combat, ArcanumObjectId pc, string label)
    {
        var hostiles = new List<ArcanumObjectId>();
        foreach (CombatParticipant participant in combat.Participants)
            if (participant.Identity != pc) hostiles.Add(participant.Identity);
        foreach (ArcanumObjectId hostile in hostiles)
            Check(combat.RemoveParticipant(hostile).Succeeded, "validation hostile leaves combat authority");
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
        Check(loader != null && lifecycle != null, "production TestTerrain composition remains available");
    }

    private static ArcanumObjectId ParseIdentity(string key)
    {
        if (!ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId identity))
            throw new InvalidOperationException("Invalid validation ObjectID: " + key);
        return identity;
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("M10A validation FAIL: " + label);
    }

    private static void Track(string _, string __, LogType type)
    {
        if (type == LogType.Warning) _warnings++;
        else if (type is LogType.Error or LogType.Assert or LogType.Exception) _errors++;
    }

    private sealed class SequenceMagicRandom : IMagicRandom
    {
        private readonly Queue<int> _values;
        public SequenceMagicRandom(params int[] values) => _values = new Queue<int>(values);
        public int Next(int minimumInclusive, int maximumExclusive)
        {
            Check(_values.Count > 0, "magic requests only expected deterministic RNG samples");
            int value = _values.Dequeue();
            Check(value >= minimumInclusive && value < maximumExclusive,
                "deterministic magic RNG sample is within source bounds");
            return value;
        }
    }
}
