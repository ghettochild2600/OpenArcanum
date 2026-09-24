using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.World;
using Arcanum.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M8GCombatLoopPhase1")]
    public sealed class M8GCombatLoopPhase1Tests
    {
        private const string Sector = "maps/arcanum1-024-fixed/47781512457.sec";
        private const int OnfKos = 0x00000100;
        private const int OcfAnimal = 0x00008000;
        private static readonly ArcanumObjectId InitialHostile = ParseIdentity(
            "G_11111111_1111_1111_1111_111111111111");

        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private PersistentPlayerState _pc;
        private WorldObject _initialHostileRuntime;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M8GCombatLoopPhase1Tests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new Owner(_session));
            Assert.That(_session.SelectSector(Sector), Is.True);
            _pc = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(10, 10), 0x28100000u);
            _session.BindPlayer(Sector, _pc, Runtime("Player", ObjectType.Pc, new Vector2Int(10, 10)));
            _initialHostileRuntime = AddNpc(InitialHostile, new Vector2Int(11, 10), 10, OnfKos);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void StartDiscoversEligibleNearbyHostilesAndPreservesStablePcTailOrder()
        {
            ArcanumObjectId earlier = Identity(2);
            AddNpc(earlier, new Vector2Int(9, 10), 2, OnfKos);

            Assert.That(Start().Succeeded, Is.True);

            Assert.That(_session.Combat.Participants.Select(value => value.Identity),
                Is.EqualTo(new[] { earlier, InitialHostile, _pc.Identity }));
            Assert.That(_session.Combat.EngagedParticipants,
                Is.EquivalentTo(new[] { earlier, InitialHostile, _pc.Identity }));
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(earlier));
        }

        [Test]
        public void HostileEnteringSourcePerceptionSquareJoinsAtNextRoundExactlyOnce()
        {
            ArcanumObjectId newcomer = Identity(3);
            WorldObject runtime = AddNpc(newcomer, new Vector2Int(30, 30), 3, OnfKos);
            Assert.That(Start().Succeeded, Is.True);
            Assert.That(_session.Combat.Participants.Any(value => value.Identity == newcomer), Is.False);
            Assert.That(_session.SetMovementState(newcomer, new Vector2(12, 10), runtime.ArtId, false), Is.True);

            CompleteRound();

            Assert.That(_session.Combat.RoundNumber, Is.EqualTo(2));
            Assert.That(_session.Combat.Participants.Select(value => value.Identity),
                Is.EqualTo(new[] { newcomer, InitialHostile, _pc.Identity }));
            Assert.That(_session.Combat.Participants.Count(value => value.Identity == newcomer), Is.EqualTo(1));
            Assert.That(_session.Combat.IsParticipantEngaged(newcomer), Is.True);

            CompleteRound();
            Assert.That(_session.Combat.Participants.Count(value => value.Identity == newcomer), Is.EqualTo(1));
        }

        [Test]
        public void EnrolledActorLeavingDiscoverySquareRemainsInStableRoster()
        {
            Assert.That(Start().Succeeded, Is.True);
            Assert.That(_session.SetMovementState(InitialHostile, new Vector2(40, 40),
                _initialHostileRuntime.ArtId, false), Is.True);

            CompleteRound();

            Assert.That(_session.Combat.Participants.Select(value => value.Identity),
                Is.EqualTo(new[] { InitialHostile, _pc.Identity }));
            Assert.That(_session.Combat.IsParticipantEngaged(InitialHostile), Is.True);
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(InitialHostile));
        }

        [Test]
        public void DiscoveryRejectsNeutralScriptedAndOutOfRangeActors()
        {
            ArcanumObjectId neutral = Identity(4);
            ArcanumObjectId scripted = Identity(5);
            ArcanumObjectId distant = Identity(6);
            AddNpc(neutral, new Vector2Int(12, 10), 4, 0);
            AddNpc(scripted, new Vector2Int(12, 11), 5, OnfKos, willKosScript: 9000);
            AddNpc(distant, new Vector2Int(30, 30), 6, OnfKos);

            Assert.That(Start().Succeeded, Is.True);
            CompleteRound();

            Assert.That(_session.Combat.Participants.Select(value => value.Identity),
                Is.EqualTo(new[] { InitialHostile, _pc.Identity }));
            Assert.That(_session.Combat.EngageParticipant(neutral).Failure,
                Is.EqualTo(CombatFailure.TargetNotHostile));
            Assert.That(_session.Combat.EngageParticipant(scripted).Failure,
                Is.EqualTo(CombatFailure.TargetNotHostile));
        }

        [Test]
        public void DeadAndNewlyUnconsciousActorsAreNotDiscovered()
        {
            ArcanumObjectId dead = Identity(7);
            ArcanumObjectId unconscious = Identity(8);
            AddNpc(dead, new Vector2Int(12, 10), 7, OnfKos);
            AddNpc(unconscious, new Vector2Int(12, 11), 8, OnfKos);
            _session.Vitality.ApplyHitPointDamage(dead, _session.Vitality.GetCurrentHitPoints(dead));
            _session.Vitality.ApplyFatigueDamage(unconscious,
                _session.Vitality.GetCurrentFatigue(unconscious));

            Assert.That(Start().Succeeded, Is.True);

            Assert.That(_session.Combat.Participants.Any(value => value.Identity == dead), Is.False);
            Assert.That(_session.Combat.Participants.Any(value => value.Identity == unconscious), Is.False);
            Assert.That(_session.Combat.EngageParticipant(dead).Failure,
                Is.EqualTo(CombatFailure.ParticipantUnavailable));
            Assert.That(_session.Combat.EngageParticipant(unconscious).Failure,
                Is.EqualTo(CombatFailure.ParticipantUnavailable));
        }

        [Test]
        public void ExistingParticipantBecomingUnconsciousIsRetainedAndSkipped()
        {
            Assert.That(Start().Succeeded, Is.True);
            _session.Vitality.ApplyFatigueDamage(InitialHostile,
                _session.Vitality.GetCurrentFatigue(InitialHostile));

            Assert.That(_session.Combat.Participants.Any(value => value.Identity == InitialHostile), Is.True);
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_pc.Identity));
            Assert.That(_session.Combat.EndCurrentTurn(_pc.Identity).Succeeded, Is.True);
            Assert.That(_session.Combat.RoundNumber, Is.EqualTo(2));
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_pc.Identity));
        }

        [Test]
        public void ExplicitRuntimeEngagementEnrollsOnceWithoutStealingCurrentTurn()
        {
            Assert.That(Start().Succeeded, Is.True);
            ArcanumObjectId newcomer = Identity(9);
            AddNpc(newcomer, new Vector2Int(12, 10), 2, OnfKos);
            ArcanumObjectId current = _session.Combat.CurrentParticipant;
            int actionPoints = _session.Combat.CurrentActionPoints;

            Assert.That(_session.Combat.EngageParticipant(newcomer).Succeeded, Is.True);
            Assert.That(_session.Combat.EngageParticipant(newcomer).Failure,
                Is.EqualTo(CombatFailure.AlreadyRegistered));

            Assert.That(_session.Combat.Participants.Select(value => value.Identity),
                Is.EqualTo(new[] { newcomer, InitialHostile, _pc.Identity }));
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(current));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(actionPoints));
        }

        [Test]
        public void RuntimeEngagedHostileBlocksTerminationUntilRemoved()
        {
            Assert.That(Start().Succeeded, Is.True);
            ArcanumObjectId newcomer = Identity(10);
            AddNpc(newcomer, new Vector2Int(12, 10), 2, OnfKos);
            Assert.That(_session.Combat.EngageParticipant(newcomer).Succeeded, Is.True);
            Assert.That(_session.Combat.RemoveParticipant(InitialHostile).Succeeded, Is.True);

            Assert.That(_session.Combat.EndCombat(_pc.Identity).Failure,
                Is.EqualTo(CombatFailure.HostileParticipantActive));
            Assert.That(_session.Combat.RemoveParticipant(newcomer).Succeeded, Is.True);
            Assert.That(_session.Combat.EndCombat(_pc.Identity).Succeeded, Is.True);
        }

        [Test]
        public void IncompleteRoundAndCombatTerminationEmitNoBoundary()
        {
            var events = new List<CombatRoundBoundary>();
            _session.Combat.RoundCompleted += events.Add;
            Assert.That(Start().Succeeded, Is.True);
            Assert.That(_session.Combat.EndCurrentTurn(InitialHostile).Succeeded, Is.True);
            Assert.That(events, Is.Empty);
            Assert.That(_session.Combat.RemoveParticipant(InitialHostile).Succeeded, Is.True);
            Assert.That(_session.Combat.EndCombat(_pc.Identity).Succeeded, Is.True);
            Assert.That(events, Is.Empty);
        }

        [Test]
        public void OneCompletedRoundEmitsOneThousandMillisecondsExactlyOnce()
        {
            var events = new List<CombatRoundBoundary>();
            _session.Combat.RoundCompleted += events.Add;
            Assert.That(Start().Succeeded, Is.True);

            CompleteRound();

            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(events[0].CompletedRoundNumber, Is.EqualTo(1));
            Assert.That(events[0].ElapsedMilliseconds, Is.EqualTo(1000));
            Assert.That(events[0].TotalElapsedMilliseconds, Is.EqualTo(1000));
            Assert.That(_session.Combat.ElapsedCombatTimeMilliseconds, Is.EqualTo(1000));
        }

        [Test]
        public void MultipleCompletedRoundsEmitExactlyNBoundaries()
        {
            var events = new List<CombatRoundBoundary>();
            _session.Combat.RoundCompleted += events.Add;
            Assert.That(Start().Succeeded, Is.True);

            CompleteRound();
            CompleteRound();
            CompleteRound();

            Assert.That(events.Select(value => value.CompletedRoundNumber), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(events.Select(value => value.TotalElapsedMilliseconds),
                Is.EqualTo(new long[] { 1000, 2000, 3000 }));
            Assert.That(_session.Combat.RoundNumber, Is.EqualTo(4));
        }

        [Test]
        public void EnrollmentAtBoundaryDoesNotDoubleFireOrCorruptNextOwner()
        {
            var events = new List<CombatRoundBoundary>();
            _session.Combat.RoundCompleted += events.Add;
            ArcanumObjectId newcomer = Identity(11);
            WorldObject runtime = AddNpc(newcomer, new Vector2Int(30, 30), 2, OnfKos);
            Assert.That(Start().Succeeded, Is.True);
            Assert.That(_session.SetMovementState(newcomer, new Vector2(12, 10), runtime.ArtId, false), Is.True);

            CompleteRound();

            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(newcomer));
            Assert.That(_session.Combat.Participants.Count(value => value.Identity == newcomer), Is.EqualTo(1));
        }

        [Test]
        public void CurrentActorDeathAdvancesWithoutDoubleFiringBoundary()
        {
            var events = new List<CombatRoundBoundary>();
            _session.Combat.RoundCompleted += events.Add;
            Assert.That(Start().Succeeded, Is.True);

            _session.Vitality.ApplyHitPointDamage(InitialHostile,
                _session.Vitality.GetCurrentHitPoints(InitialHostile));
            Assert.That(events, Is.Empty);
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_pc.Identity));
            Assert.That(_session.Combat.EndCurrentTurn(_pc.Identity).Succeeded, Is.True);

            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(_session.Combat.RoundNumber, Is.EqualTo(2));
        }

        [Test]
        public void PresentationRebindDoesNotEmitBoundary()
        {
            var events = new List<CombatRoundBoundary>();
            _session.Combat.RoundCompleted += events.Add;
            Assert.That(Start().Succeeded, Is.True);

            _session.Combat.BindNavigationMap(null);
            _session.Combat.BindNavigationMap(null);

            Assert.That(events, Is.Empty);
            Assert.That(_session.Combat.RoundNumber, Is.EqualTo(1));
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(InitialHostile));
        }

        [Test]
        public void SaveLoadNormalizationDoesNotReplayBoundaryOrEngagement()
        {
            var events = new List<CombatRoundBoundary>();
            CombatStateService original = _session.Combat;
            original.RoundCompleted += events.Add;
            Assert.That(Start().Succeeded, Is.True);
            string json = _session.SaveGames.SerializeCurrentSession();

            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);

            Assert.That(events, Is.Empty);
            Assert.That(_session.Combat, Is.Not.SameAs(original));
            Assert.That(_session.Combat.IsActive, Is.False);
            Assert.That(_session.Combat.EngagedParticipants, Is.Empty);
            Assert.That(_session.Combat.ElapsedCombatTimeMilliseconds, Is.Zero);
        }

        [Test]
        public void NewCombatStartsWithCleanRoundBoundaryState()
        {
            int events = 0;
            _session.Combat.RoundCompleted += _ => events++;
            Assert.That(Start().Succeeded, Is.True);
            CompleteRound();
            Assert.That(events, Is.EqualTo(1));
            Assert.That(_session.Combat.RemoveParticipant(InitialHostile).Succeeded, Is.True);
            Assert.That(_session.Combat.EndCombat(_pc.Identity).Succeeded, Is.True);
            Assert.That(_session.Combat.ElapsedCombatTimeMilliseconds, Is.Zero);

            Assert.That(Start().Succeeded, Is.True);

            Assert.That(_session.Combat.RoundNumber, Is.EqualTo(1));
            Assert.That(_session.Combat.ElapsedCombatTimeMilliseconds, Is.Zero);
        }

        private CombatResult Start() => _session.Combat.StartCombat(_pc.Identity, InitialHostile);

        private void CompleteRound()
        {
            int round = _session.Combat.RoundNumber;
            int guard = _session.Combat.Participants.Count + 2;
            while (_session.Combat.IsActive && _session.Combat.RoundNumber == round && guard-- > 0)
            {
                ArcanumObjectId current = _session.Combat.CurrentParticipant;
                Assert.That(_session.Combat.EndCurrentTurn(current).Succeeded, Is.True);
            }
            Assert.That(guard, Is.GreaterThanOrEqualTo(0), "round completion guard exhausted");
        }

        private WorldObject AddNpc(ArcanumObjectId identity, Vector2Int tile, int sourceOrder,
            int npcFlags, int critterFlags = OcfAnimal, int willKosScript = 0)
        {
            const int prototype = 28001;
            int[] stats = Stats();
            var source = new ObjectInstance(ObjectType.Npc, prototype, Location(tile.x, tile.y),
                0x28100000u, 0, 0, oid: GuidBytes(identity));
            PersistentObjectState state = _session.GetOrCreate(source, Sector, source.CurrentArtId.Value,
                false, false);
            _session.Characters.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype, stats, null);
            _session.Progression.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype,
                CharacterProgressionSource.Resolve(stats, null, null, null, null, null, critterFlags));
            _session.DerivedStats.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype,
                CharacterDerivedSource.Resolve(stats, null, null, 0, null, null, null, 50,
                    npcFlags, critterFlags));
            _session.Vitality.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype,
                CharacterVitalitySource.Resolve(stats, null, null, 0, null, 0, null, 0,
                    null, 0, null, 0, null, 0));
            WorldObject runtime = Runtime(identity.Key, ObjectType.Npc, tile);
            _session.Bind(Sector, state, runtime);
            _session.Combat.RegisterActorSource(new CombatActorSource(identity, ObjectType.Npc, prototype,
                Sector, sourceOrder, npcFlags, critterFlags, willKosScript));
            return runtime;
        }

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

        private static int[] Stats()
        {
            var result = new int[CharacterAttributeSet.SourceStatArrayCount];
            for (int index = 0; index < CharacterAttributeSet.Count; index++) result[index] = 8;
            result[CharacterProgressionSource.LevelSourceSlot] = 1;
            result[CharacterAttributeSet.GenderSourceSlot] = (int)CharacterGender.Male;
            result[CharacterAttributeSet.RaceSourceSlot] = (int)CharacterRace.Human;
            return result;
        }

        private static ArcanumObjectId Identity(int value)
            => ArcanumObjectId.CreateGuid(new Guid(value, 0, 0, new byte[8]));

        private static ArcanumObjectId ParseIdentity(string key)
        {
            if (!ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId identity))
                throw new InvalidOperationException("Invalid test ObjectID: " + key);
            return identity;
        }

        private static byte[] GuidBytes(ArcanumObjectId identity)
        {
            string compact = identity.Key.Substring(2).Replace("_", string.Empty);
            var bytes = new byte[ArcanumObjectId.SerializedSize];
            bytes[0] = (byte)ArcanumObjectIdType.Guid;
            for (int index = 0; index < 16; index++)
                bytes[8 + index] = Convert.ToByte(compact.Substring(index * 2, 2), 16);
            return bytes;
        }

        private static long Location(int x, int y) => (uint)x | ((long)(uint)y << 32);

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
