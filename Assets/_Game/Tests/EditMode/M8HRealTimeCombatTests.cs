using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Art;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Formats.Tiles;
using Arcanum.Formats.World;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.World;
using Arcanum.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M8HRealTimeCombat")]
    public sealed class M8HRealTimeCombatTests
    {
        private const string Sector = "maps/arcanum1-024-fixed/101602821844.sec";
        private const string TargetKey = "G_9B807B01_A142_4949_80CE_5A085F3BEEB1";
        private const string BowKey = "G_1575DBCA_4990_C243_8184_524D51F7D533";
        private const string ArrowKey = "G_FBFA4631_D97D_D740_9636_F131B2FD9F7B";
        private const int OnfKos = 0x00000100;

        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private PersistentPlayerState _pc;
        private WorldObject _pcRuntime;
        private ArcanumObjectId _target;
        private WorldObject _targetRuntime;
        private PersistentObjectState _arrows;
        private SectorNavigationMap _map;
        private FixedTiming _timing;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M8HRealTimeCombatTests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new Owner(_session));
            Assert.That(_session.SelectSector(Sector), Is.True);
            _pc = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(1, 1), 0x28100000u);
            _pcRuntime = Runtime("Player", ObjectType.Pc, new Vector2Int(1, 1));
            _session.BindPlayer(Sector, _pc, _pcRuntime);
            _target = AddNpc(TargetKey, new Vector2Int(2, 1), 10, out _targetRuntime);
            _session.GetOrCreate(Item(BowKey, ObjectType.Weapon, 6055,
                    (int)WornLocation.Weapon), Sector, 0x50000000u, false, false,
                weaponFlags: 0x0000000E, weaponData: Bow());
            _arrows = AddAmmo(70);
            _map = Map();
            _map.Register(_pcRuntime, 0);
            _map.Register(_targetRuntime, 0);
            _map.SetControlledObject(_pcRuntime);
            _session.Combat.BindNavigationMap(_map);
            _timing = new FixedTiming();
            _session.Combat.BindRealTimeTimingSource(_timing);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void RealTimeStartReusesRosterWithoutTurnOrActionPointAuthority()
        {
            Start();

            Assert.That(_session.Combat.Mode, Is.EqualTo(CombatMode.RealTime));
            Assert.That(_session.Combat.Participants.Select(value => value.Identity),
                Is.EqualTo(new[] { _target, _pc.Identity }));
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(default(ArcanumObjectId)));
            Assert.That(_session.Combat.CurrentActionPoints, Is.Zero);
            Assert.That(_session.Combat.TryGetRealTimeActorState(_pc.Identity, out var pcState)
                        && pcState.IsReady, Is.True);
            Assert.That(_session.Combat.TryGetRealTimeActorState(_target, out var targetState)
                        && targetState.IsReady, Is.True);
        }

        [Test]
        public void SourceSpeedTablesUseExactWalkRunUnarmedAndBowInterpolation()
        {
            Assert.That(CombatRealTimeSourceTiming.AdjustedFramesPerSecond(
                CombatRealTimeSourceTimingProfile.Walk, 8), Is.EqualTo(17));
            Assert.That(CombatRealTimeSourceTiming.AdjustedFramesPerSecond(
                CombatRealTimeSourceTimingProfile.Run, 8), Is.EqualTo(20));
            Assert.That(CombatRealTimeSourceTiming.AdjustedFramesPerSecond(
                CombatRealTimeSourceTimingProfile.UnarmedAttack, 8), Is.EqualTo(15));
            Assert.That(CombatRealTimeSourceTiming.AdjustedFramesPerSecond(
                CombatRealTimeSourceTimingProfile.BowAttack, 8), Is.EqualTo(10));
            Assert.That(CombatRealTimeSourceTiming.AdjustedFramesPerSecond(
                CombatRealTimeSourceTimingProfile.Walk, 4), Is.EqualTo(11));
            Assert.That(CombatRealTimeSourceTiming.AdjustedFramesPerSecond(
                CombatRealTimeSourceTimingProfile.BowAttack, 20), Is.EqualTo(12));
            Assert.That(CombatRealTimeSourceTiming.AdjustedFramesPerSecond(
                CombatRealTimeSourceTimingProfile.UnarmedAttack, 30), Is.EqualTo(23));
            Assert.That(CombatRealTimeSourceTiming.AdjustedFramesPerSecond(
                CombatRealTimeSourceTimingProfile.Walk, 8, smallBody: true), Is.EqualTo(21));
        }

        [Test]
        public void SourceFrameAndWeaponSpeedDelayUseExactThirtyToEightHundredMillisecondClamp()
        {
            Assert.That(CombatRealTimeSourceTiming.FrameIntervalMilliseconds(10), Is.EqualTo(100));
            Assert.That(CombatRealTimeSourceTiming.FrameIntervalMilliseconds(100), Is.EqualTo(30));
            Assert.That(CombatRealTimeSourceTiming.FrameIntervalMilliseconds(1), Is.EqualTo(800));
            Assert.That(CombatRealTimeSourceTiming.ApplyWeaponSpeed(100, 8), Is.EqualTo(120));
            Assert.That(CombatRealTimeSourceTiming.ApplyWeaponSpeed(100, 15), Is.EqualTo(50));
            Assert.That(CombatRealTimeSourceTiming.ApplyWeaponSpeed(100, 30), Is.EqualTo(30));
        }

        [Test]
        public void AttackWaitsForEffectTimeAndExecutesExactlyOnce()
        {
            Start();
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            var request = new CombatAttackRequest(_target, _pc.Identity);
            Assert.That(_session.Combat.ScheduleRealTimeAttack(request).Succeeded, Is.True);

            Assert.That(_session.Combat.AdvanceRealTime(49).Succeeded, Is.True);
            Assert.That(_session.Combat.LastAttackResult, Is.Null);
            Assert.That(_session.Combat.AdvanceRealTime(1).Succeeded, Is.True);
            Assert.That(_session.Combat.LastAttackResult?.Succeeded, Is.True);
            CombatAttackResult first = _session.Combat.LastAttackResult.Value;
            Assert.That(first.ActionPointCost, Is.EqualTo(5));
            Assert.That(first.ActionPointsSpent, Is.Zero);
            Assert.That(_session.Combat.AdvanceRealTime(500).Succeeded, Is.True);
            Assert.That(_session.Combat.LastAttackResult.Value.AttackRoll, Is.EqualTo(first.AttackRoll));
        }

        [Test]
        public void ActorRemainsBusyUntilSourceAnimationRecoveryCompletes()
        {
            Start();
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100, 100, 100));
            var request = new CombatAttackRequest(_target, _pc.Identity);
            Assert.That(_session.Combat.ScheduleRealTimeAttack(request).Succeeded, Is.True);
            Assert.That(_session.Combat.ScheduleRealTimeAttack(request).Failure,
                Is.EqualTo(CombatFailure.ActorBusy));
            _session.Combat.AdvanceRealTime(50);
            Assert.That(_session.Combat.ScheduleRealTimeAttack(request).Failure,
                Is.EqualTo(CombatFailure.ActorBusy));
            _session.Combat.AdvanceRealTime(50);
            Assert.That(_session.Combat.ScheduleRealTimeAttack(request).Succeeded, Is.True);
        }

        [Test]
        public void SimultaneousEffectsUseStableRosterOrdering()
        {
            Start();
            var resolved = new List<ArcanumObjectId>();
            _session.Combat.RealTimeActionResolved += value => resolved.Add(value.Action.Actor);
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100, 100, 100));
            Assert.That(_session.Combat.ScheduleRealTimeAttack(
                new CombatAttackRequest(_pc.Identity, _target, CombatAttackMode.BasicRanged)).Succeeded, Is.True);
            Assert.That(_session.Combat.ScheduleRealTimeAttack(
                new CombatAttackRequest(_target, _pc.Identity)).Succeeded, Is.True);

            _session.Combat.AdvanceRealTime(50);

            Assert.That(resolved, Is.EqualTo(new[] { _target, _pc.Identity }));
        }

        [Test]
        public void OneAndMultipleThousandMillisecondBoundariesAreExact()
        {
            Start();
            var boundaries = new List<CombatRoundBoundary>();
            _session.Combat.RoundCompleted += boundaries.Add;

            _session.Combat.AdvanceRealTime(999);
            Assert.That(boundaries, Is.Empty);
            _session.Combat.AdvanceRealTime(1);
            _session.Combat.AdvanceRealTime(2500);

            Assert.That(boundaries.Select(value => value.TotalElapsedMilliseconds),
                Is.EqualTo(new long[] { 1000, 2000, 3000 }));
            Assert.That(_session.Combat.ElapsedCombatTimeMilliseconds, Is.EqualTo(3500));
            Assert.That(_session.Combat.RoundNumber, Is.EqualTo(4));
        }

        [Test]
        public void ActionResolutionDoesNotManufactureRoundBoundaries()
        {
            Start();
            int boundaries = 0;
            _session.Combat.RoundCompleted += _ => boundaries++;
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            _session.Combat.ScheduleRealTimeAttack(new CombatAttackRequest(_target, _pc.Identity));

            _session.Combat.AdvanceRealTime(100);

            Assert.That(boundaries, Is.Zero);
        }

        [Test]
        public void MovementCommitsOnlyAtSourceTimedCompletion()
        {
            Start();
            Assert.That(_session.Combat.ScheduleRealTimeMove(_pc.Identity,
                new Vector2Int(1, 3), true).Succeeded, Is.True);
            Assert.That(_timing.LastRequest.RouteSteps, Is.EqualTo(2));
            _session.Combat.AdvanceRealTime(199);
            Assert.That(_pcRuntime.Tile, Is.EqualTo(new Vector2Int(1, 1)));

            _session.Combat.AdvanceRealTime(1);

            Assert.That(_pcRuntime.Tile, Is.EqualTo(new Vector2Int(1, 3)));
            Assert.That(_session.Combat.LastRealTimeActionResolution?.MoveResult?.ActionPointsSpent,
                Is.Zero);
        }

        [Test]
        public void RangedBowUsesExistingKernelAmmoLedgerAndDamagePath()
        {
            Start();
            _session.Combat.SetRandomSource(new SequenceRandom(1, 100, 10, 5));
            int hp = _session.Vitality.GetCurrentHitPoints(_target);
            var request = new CombatAttackRequest(_pc.Identity, _target,
                CombatAttackMode.BasicRanged, CombatCalledLocation.Torso);
            Assert.That(_session.Combat.ScheduleRealTimeAttack(request).Succeeded, Is.True);

            _session.Combat.AdvanceRealTime(50);

            CombatAttackResult result = _session.Combat.LastAttackResult.Value;
            Assert.That(result.Succeeded && result.Hit, Is.True);
            Assert.That(result.Request.CalledLocation, Is.EqualTo(CombatCalledLocation.Torso));
            Assert.That(result.ModifierLedger.Entries.Any(value =>
                value.Reason == CombatAttackModifierReason.CalledLocation), Is.True);
            Assert.That(result.ActionPointCost, Is.EqualTo(6));
            Assert.That(result.ActionPointsSpent, Is.Zero);
            Assert.That(_arrows.StackQuantity, Is.EqualTo(69));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_target), Is.EqualTo(hp - 10));
        }

        [Test]
        public void MeleeCriticalUsesExistingStructuredDamagePathWithoutActionPointSpending()
        {
            Assert.That(_session.UnequipItem(_pc.Identity, WornLocation.Weapon).Succeeded, Is.True);
            Start();
            CombatHitChance chance = _session.Combat.GetBasicMeleeHitChance(_pc.Identity, _target);
            _session.Combat.SetRandomSource(chance.DodgeChance > 0
                ? new SequenceRandom(1, 1, 100, 4, 4, 100, 100, 1)
                : new SequenceRandom(1, 1, 4, 4, 100, 100, 1));
            int hp = _session.Vitality.GetCurrentHitPoints(_target);
            var request = new CombatAttackRequest(_pc.Identity, _target,
                CombatAttackMode.BasicMelee, CombatCalledLocation.Torso);

            Assert.That(_session.Combat.ScheduleRealTimeAttack(request).Succeeded, Is.True);
            _session.Combat.AdvanceRealTime(50);

            CombatAttackResult result = _session.Combat.LastAttackResult.Value;
            Assert.That(result.Request, Is.EqualTo(request));
            Assert.That(result.Outcome, Is.EqualTo(CombatAttackOutcome.CriticalSuccess));
            Assert.That(result.CriticalEffect, Is.EqualTo(CombatCriticalEffect.BonusDamage50));
            Assert.That(result.ActionPointCost, Is.EqualTo(5));
            Assert.That(result.ActionPointsSpent, Is.Zero);
            Assert.That(result.MitigatedHitPointDamage, Is.EqualTo(6));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_target), Is.EqualTo(hp - 6));
        }

        [Test]
        public void RealTimeMasterBowReusesRangeCoverCalledLocationAndTwoImpactKernel()
        {
            SetTraining(_pc.Identity, CharacterSkill.Bow, SkillTrainingLevel.Master);
            Assert.That(_session.SetMovementState(_target, new Vector2(7, 1),
                _targetRuntime.ArtId, false), Is.True);
            _targetRuntime.Tile = new Vector2Int(7, 1);
            _targetRuntime.TilePosition = new Vector2Int(7, 1);
            WorldObject cover = Runtime("M8H Cover", ObjectType.Scenery, new Vector2Int(4, 1));
            cover.SourceFlags = 0x00000020 | 0x00000010 | 0x00004000;
            _map.Register(cover, cover.SourceFlags);
            Start();
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            var request = new CombatAttackRequest(_pc.Identity, _target,
                CombatAttackMode.BasicRanged, CombatCalledLocation.Arm);

            Assert.That(_session.Combat.ScheduleRealTimeAttack(request).Succeeded, Is.True);
            _session.Combat.AdvanceRealTime(50);

            CombatAttackResult result = _session.Combat.LastAttackResult.Value;
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ImpactCount, Is.EqualTo(2));
            Assert.That(result.Impacts.All(value => value.ModifierLedger == result.ModifierLedger), Is.True);
            CombatAttackModifier range = Entry(result, CombatAttackModifierReason.PerceptionRange);
            Assert.That(range.Value, Is.LessThan(0));
            Assert.That(range.Suppressed, Is.True);
            Assert.That(Entry(result, CombatAttackModifierReason.Cover).Value, Is.EqualTo(-20));
            Assert.That(Entry(result, CombatAttackModifierReason.CalledLocation).Value, Is.EqualTo(-30));
            Assert.That(result.ActionPointsSpent, Is.Zero);
            Assert.That(result.AmmoQuantityBefore - result.AmmoQuantityAfter, Is.EqualTo(1));
        }

        [Test]
        public void LethalScheduledBowAttackRemovesAuthorityAndProcessesConsequencesOnce()
        {
            SetTraining(_pc.Identity, CharacterSkill.Bow, SkillTrainingLevel.Expert);
            Start();
            _session.Vitality.ApplyHitPointDamage(_target,
                _session.Vitality.GetCurrentHitPoints(_target) - 1);
            int xp = _session.Progression.GetExperience(_pc.Identity);
            _session.Combat.SetRandomSource(new SequenceRandom(1, 100, 3, 2, 7, 5));
            Assert.That(_session.Combat.ScheduleRealTimeAttack(new CombatAttackRequest(
                _pc.Identity, _target, CombatAttackMode.BasicRanged)).Succeeded, Is.True);

            _session.Combat.AdvanceRealTime(50);

            CombatAttackResult result = _session.Combat.LastAttackResult.Value;
            Assert.That(result.Succeeded && result.ImpactCount == 2, Is.True);
            Assert.That(_session.Vitality.IsDead(_target), Is.True);
            Assert.That(_session.TryGetObjectState(_target, out PersistentObjectState corpse), Is.True);
            Assert.That(corpse.DeathConsequencesProcessed, Is.True);
            Assert.That(_session.Combat.Participants.Any(value => value.Identity == _target), Is.False);
            Assert.That(_session.Combat.TryGetRealTimeActorState(_target, out _), Is.False);
            Assert.That(_session.Progression.GetExperience(_pc.Identity), Is.EqualTo(xp + 88));
            Assert.That(_session.DeathConsequences.Process(_pc.Identity, _target).Failure,
                Is.EqualTo(DeathConsequenceFailure.AlreadyProcessed));
            Assert.That(_session.Progression.GetExperience(_pc.Identity), Is.EqualTo(xp + 88));
        }

        [Test]
        public void DynamicHostileEnrollmentCreatesOneReadySchedulerStateAtBoundary()
        {
            Start();
            ArcanumObjectId newcomer = AddNpc("G_11111111_1111_1111_1111_111111111111",
                new Vector2Int(30, 30), 2, out WorldObject runtime);
            _map.Register(runtime, 0);
            _session.SetMovementState(newcomer, new Vector2(2, 2), runtime.ArtId, false);

            _session.Combat.AdvanceRealTime(1000);

            Assert.That(_session.Combat.Participants.Count(value => value.Identity == newcomer), Is.EqualTo(1));
            Assert.That(_session.Combat.TryGetRealTimeActorState(newcomer, out var state)
                        && state.IsReady, Is.True);
        }

        [Test]
        public void ExplicitEngagementCreatesStateOnceWithoutChangingOtherReadiness()
        {
            Start();
            ArcanumObjectId newcomer = AddNpc("G_22222222_2222_2222_2222_222222222222",
                new Vector2Int(2, 2), 2, out WorldObject runtime);
            _map.Register(runtime, 0);

            Assert.That(_session.Combat.EngageParticipant(newcomer).Succeeded, Is.True);
            Assert.That(_session.Combat.EngageParticipant(newcomer).Failure,
                Is.EqualTo(CombatFailure.AlreadyRegistered));
            Assert.That(_session.Combat.TryGetRealTimeActorState(newcomer, out var state)
                        && state.IsReady, Is.True);
        }

        [Test]
        public void DeathRemovesPendingAndFutureActionAuthority()
        {
            Start();
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            Assert.That(_session.Combat.ScheduleRealTimeAttack(
                new CombatAttackRequest(_target, _pc.Identity)).Succeeded, Is.True);
            _session.Vitality.ApplyHitPointDamage(_target,
                _session.Vitality.GetCurrentHitPoints(_target));

            _session.Combat.AdvanceRealTime(100);

            Assert.That(_session.Combat.TryGetRealTimeActorState(_target, out _), Is.False);
            Assert.That(_session.Combat.LastAttackResult, Is.Null);
        }

        [Test]
        public void UnconsciousParticipantIsRetainedButSchedulingIsSuspended()
        {
            Start();
            _session.Vitality.ApplyFatigueDamage(_target,
                _session.Vitality.GetCurrentFatigue(_target));

            Assert.That(_session.Combat.Participants.Any(value => value.Identity == _target), Is.True);
            Assert.That(_session.Combat.TryGetRealTimeActorState(_target, out var state)
                        && state.IsSuspended && !state.IsReady, Is.True);
            Assert.That(_session.Combat.ScheduleRealTimeAttack(
                new CombatAttackRequest(_target, _pc.Identity)).Failure,
                Is.EqualTo(CombatFailure.ParticipantUnavailable));
        }

        [Test]
        public void KernelRejectionClearsCooldownWithoutTransactionMutation()
        {
            Start();
            int arrows = _arrows.StackQuantity.Value;
            int hp = _session.Vitality.GetCurrentHitPoints(_target);
            var invalid = new CombatAttackRequest(_pc.Identity, _target,
                CombatAttackMode.BasicMelee);
            Assert.That(_session.Combat.ScheduleRealTimeAttack(invalid).Succeeded, Is.True);

            _session.Combat.AdvanceRealTime(50);

            Assert.That(_session.Combat.LastRealTimeActionResolution?.Failure,
                Is.EqualTo(CombatFailure.UnsupportedWeapon));
            Assert.That(_session.Combat.TryGetRealTimeActorState(_pc.Identity, out var state)
                        && state.IsReady && !state.HasPendingAction, Is.True);
            Assert.That(_arrows.StackQuantity, Is.EqualTo(arrows));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_target), Is.EqualTo(hp));
        }

        [Test]
        public void DirectKernelCommandCannotBypassRealTimeScheduler()
        {
            Start();

            Assert.That(_session.Combat.Attack(_target, _pc.Identity).Failure,
                Is.EqualTo(CombatFailure.ActorNotReady));
            Assert.That(_session.Combat.MoveInCombat(_pc.Identity, new Vector2Int(1, 2)).Failure,
                Is.EqualTo(CombatFailure.ActorNotReady));
        }

        [Test]
        public void CombatTerminationClearsAllTransientSchedulerState()
        {
            Start();
            Assert.That(_session.Combat.RemoveParticipant(_target).Succeeded, Is.True);
            Assert.That(_session.Combat.EndCombat(_pc.Identity).Succeeded, Is.True);

            Assert.That(_session.Combat.IsActive, Is.False);
            Assert.That(_session.Combat.Mode, Is.EqualTo(CombatMode.TurnBased));
            Assert.That(_session.Combat.ElapsedCombatTimeMilliseconds, Is.Zero);
            Assert.That(_session.Combat.TryGetRealTimeActorState(_pc.Identity, out _), Is.False);
        }

        [Test]
        public void SaveLoadNormalizesPendingActionCooldownAndClock()
        {
            Start();
            _session.Combat.ScheduleRealTimeAttack(new CombatAttackRequest(_target, _pc.Identity));
            _session.Combat.AdvanceRealTime(25);
            string json = _session.SaveGames.SerializeCurrentSession();

            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);

            Assert.That(_session.Combat.IsActive, Is.False);
            Assert.That(_session.Combat.ElapsedCombatTimeMilliseconds, Is.Zero);
            Assert.That(_session.Combat.LastAttackResult, Is.Null);
            Assert.That(_session.Combat.LastRealTimeActionResolution, Is.Null);
        }

        [Test]
        public void InvalidScheduleAndTimeAdvanceAreTransactional()
        {
            Start();
            ArcanumObjectId missing = ArcanumObjectId.CreateGuid(Guid.Parse(
                "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

            Assert.That(_session.Combat.ScheduleRealTimeAttack(
                new CombatAttackRequest(_pc.Identity, missing, CombatAttackMode.BasicRanged)).Failure,
                Is.EqualTo(CombatFailure.ParticipantNotRegistered));
            Assert.That(_session.Combat.AdvanceRealTime(-1).Failure,
                Is.EqualTo(CombatFailure.InvalidTimeAdvance));
            Assert.That(_session.Combat.ElapsedCombatTimeMilliseconds, Is.Zero);
            Assert.That(_session.Combat.TryGetRealTimeActorState(_pc.Identity, out var state)
                        && state.IsReady, Is.True);
        }

        private void Start()
        {
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _target,
                CombatMode.RealTime).Succeeded, Is.True);
        }

        private PersistentObjectState AddAmmo(int quantity)
            => _session.GetOrCreate(Item(ArrowKey, ObjectType.Ammo, 7058, 0),
                Sector, 0x60000001u, false, false, stackQuantity: quantity, ammoItemType: 0);

        private static CombatAttackModifier Entry(CombatAttackResult result,
            CombatAttackModifierReason reason)
            => result.ModifierLedger.Entries.Single(value => value.Reason == reason);

        private void SetTraining(ArcanumObjectId identity, CharacterSkill skill,
            SkillTrainingLevel training)
        {
            PersistentCharacterProgressionState state = _session.Progression.Get(identity);
            int[] purchased = state.CopyPurchasedPoints();
            SkillTrainingLevel[] levels = state.CopyTraining();
            purchased[(int)skill] = 5;
            levels[(int)skill] = training;
            state.RestoreSkills(purchased, levels);
        }

        private ArcanumObjectId AddNpc(string key, Vector2Int tile, int order,
            out WorldObject runtime)
        {
            ArcanumObjectId identity = ParseIdentity(key);
            int[] stats = Stats();
            var source = new ObjectInstance(ObjectType.Npc, 28422, Location(tile.x, tile.y),
                0x28100000u, 0, 0, oid: GuidBytes(key));
            PersistentObjectState state = _session.GetOrCreate(source, Sector,
                source.CurrentArtId.Value, false, false);
            _session.Characters.GetOrCreateSourceCharacter(identity, ObjectType.Npc, 28422, stats, null);
            _session.Progression.GetOrCreateSourceCharacter(identity, ObjectType.Npc, 28422,
                CharacterProgressionSource.Resolve(stats, null, null, null, null, null, 0));
            _session.DerivedStats.GetOrCreateSourceCharacter(identity, ObjectType.Npc, 28422,
                CharacterDerivedSource.Resolve(stats, null, null, 0, null, null, null, 50, OnfKos, 0));
            _session.Vitality.GetOrCreateSourceCharacter(identity, ObjectType.Npc, 28422,
                CharacterVitalitySource.Resolve(stats, null, null, 0, null, 15, null, 0,
                    null, 0, null, 0, null, 0));
            runtime = Runtime(key, ObjectType.Npc, tile);
            _session.Bind(Sector, state, runtime);
            _session.Combat.RegisterActorSource(new CombatActorSource(identity, ObjectType.Npc, 28422,
                Sector, order, OnfKos, 0, 0, new[] { 3, 6, 0, 0, 0, 0, 0, 0, 0, 0 },
                experienceWorth: 440));
            return identity;
        }

        private ObjectInstance Item(string key, ObjectType type, int prototype, int inventoryLocation)
            => new(type, prototype, null, 0x50000000u, 0, 0, oid: GuidBytes(key),
                parentOid: PlayerBytes(), invLocation: inventoryLocation);

        private WorldObject Runtime(string name, ObjectType type, Vector2Int tile)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform);
            WorldObject runtime = go.AddComponent<WorldObject>();
            runtime.Type = type;
            runtime.Tile = tile;
            runtime.TilePosition = tile;
            runtime.ArtId = 0x28100000u;
            runtime.Blocks = true;
            return runtime;
        }

        private static Weapon Bow()
        {
            var weapon = new Weapon
            {
                Skill = WeaponSkill.Bow,
                Range = 15,
                SpeedFactor = 8,
                MinStrength = 10,
                AmmoType = 0,
                AmmoConsumption = 1,
            };
            weapon.DamageMin[(int)DamageType.Normal] = 1;
            weapon.DamageMax[(int)DamageType.Normal] = 10;
            weapon.DamageMin[(int)DamageType.Fatigue] = 2;
            weapon.DamageMax[(int)DamageType.Fatigue] = 5;
            return weapon;
        }

        private static SectorNavigationMap Map()
        {
            TileNameTable names = TileNameTable.FromMes(MesReader.Read("{300}{grs}"));
            return new SectorNavigationMap(new SectorTerrain(new uint[SectorTerrain.TileCount]),
                new bool[SectorTerrain.TileCount], names);
        }

        private static int[] Stats()
        {
            var result = new int[CharacterAttributeSet.SourceStatArrayCount];
            for (int index = 0; index < CharacterAttributeSet.Count; index++) result[index] = 8;
            result[CharacterProgressionSource.LevelSourceSlot] = 1;
            result[CharacterAttributeSet.GenderSourceSlot] = (int)CharacterGender.Male;
            result[CharacterAttributeSet.RaceSourceSlot] = (int)CharacterRace.Human;
            return result;
        }

        private static long Location(int x, int y) => (uint)x | ((long)(uint)y << 32);

        private static ArcanumObjectId ParseIdentity(string key)
        {
            if (!ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId identity))
                throw new InvalidOperationException("Invalid test ObjectID: " + key);
            return identity;
        }

        private static byte[] GuidBytes(string key)
        {
            string compact = key.Substring(2).Replace("_", string.Empty);
            var bytes = new byte[ArcanumObjectId.SerializedSize];
            bytes[0] = (byte)ArcanumObjectIdType.Guid;
            for (int index = 0; index < 16; index++)
                bytes[8 + index] = Convert.ToByte(compact.Substring(index * 2, 2), 16);
            return bytes;
        }

        private static byte[] PlayerBytes()
        {
            var bytes = new byte[ArcanumObjectId.SerializedSize];
            bytes[0] = (byte)ArcanumObjectIdType.Guid;
            Array.Copy(new Guid("25e7b7c9-1ae7-4af5-b1e4-0a62fa6bca01").ToByteArray(), 0,
                bytes, 8, 16);
            return bytes;
        }

        private sealed class FixedTiming : ICombatRealTimeTimingSource
        {
            public CombatRealTimeTimingRequest LastRequest { get; private set; }

            public bool TryGetTiming(CombatRealTimeTimingRequest request,
                out CombatRealTimeTiming timing)
            {
                LastRequest = request;
                int ready = request.Kind == CombatRealTimeActionKind.Move
                    ? Math.Max(1, request.RouteSteps) * 100
                    : 100;
                int effect = request.Kind == CombatRealTimeActionKind.Move ? ready : 50;
                timing = new CombatRealTimeTiming(effect, ready, 50, 1, 2);
                return true;
            }
        }

        private sealed class SequenceRandom : ICombatRandom
        {
            private readonly Queue<int> _values;
            public SequenceRandom(params int[] values) => _values = new Queue<int>(values);

            public int NextInclusive(int minimum, int maximum)
            {
                Assert.That(_values, Is.Not.Empty, "combat requested an unexpected random value");
                int value = _values.Dequeue();
                Assert.That(value, Is.InRange(minimum, maximum));
                return value;
            }
        }

        private sealed class Owner : ISectorPresentationOwner
        {
            private readonly WorldMapSessionCoordinator _session;
            public Owner(WorldMapSessionCoordinator session) => _session = session;
            public string ConfiguredSector => Sector;
            public string PresentedSector { get; private set; }
            public bool IsSectorPresented => PresentedSector != null;

            public bool PresentSector(string sectorPath)
            {
                PresentedSector = WorldMapSessionCoordinator.NormalizeSector(sectorPath);
                _session.BeginSector(PresentedSector);
                return true;
            }

            public void ClearPresentedSector()
            {
                string sector = PresentedSector;
                PresentedSector = null;
                if (sector != null) _session.UnloadSector(sector);
            }
        }
    }
}
