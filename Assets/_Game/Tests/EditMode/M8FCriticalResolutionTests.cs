using System;
using System.Collections.Generic;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Formats.Tiles;
using Arcanum.Formats.World;
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
    [Category("M8FCriticalResolution")]
    public sealed class M8FCriticalResolutionTests
    {
        private const string Sector = "maps/arcanum1-024-fixed/47781512457.sec";
        private const string BearKey = "G_9B807B01_A142_4949_80CE_5A085F3BEEB1";
        private const int BearPrototype = 28422;
        private const int OnfKos = 0x00000100;
        private const int OcfAnimal = 0x00008000;
        private static readonly int[] BearStats =
            { 7, 4, 5, 17, 4, 5, 5, 14, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 0, 0, 0, 5, 0, 0, 0, 20, 1, 0 };
        private static readonly int[] BearDamage = { 3, 6, 0, 0, 0, 0, 0, 0, 0, 0 };

        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private PersistentPlayerState _pc;
        private ArcanumObjectId _bear;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M8FCriticalResolutionTests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new Owner(_session));
            Assert.That(_session.SelectSector(Sector), Is.True);
            _pc = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(1, 1), 0x28100000u);
            WorldObject pcRuntime = Runtime("Player", ObjectType.Pc, new Vector2Int(1, 1));
            _session.BindPlayer(Sector, _pc, pcRuntime);

            _bear = ParseIdentity(BearKey);
            var source = new ObjectInstance(ObjectType.Npc, BearPrototype, Location(2, 1),
                0x28100000u, 0, 0, oid: GuidBytes(BearKey));
            PersistentObjectState state = _session.GetOrCreate(source, Sector, source.CurrentArtId.Value,
                false, false);
            _session.Characters.GetOrCreateSourceCharacter(_bear, ObjectType.Npc, BearPrototype,
                BearStats, null);
            _session.Progression.GetOrCreateSourceCharacter(_bear, ObjectType.Npc, BearPrototype,
                CharacterProgressionSource.Resolve(BearStats, null, null, null, null, null, OcfAnimal));
            _session.DerivedStats.GetOrCreateSourceCharacter(_bear, ObjectType.Npc, BearPrototype,
                CharacterDerivedSource.Resolve(BearStats, null, null, 0, new int[5], null, null, 50,
                    OnfKos, OcfAnimal));
            _session.Vitality.GetOrCreateSourceCharacter(_bear, ObjectType.Npc, BearPrototype,
                CharacterVitalitySource.Resolve(BearStats, null, null, 0, null, 15, null, 0,
                    null, 0, null, 0, null, 0));
            WorldObject bearRuntime = Runtime("Polar Bear Cub", ObjectType.Npc, new Vector2Int(2, 1));
            _session.Bind(Sector, state, bearRuntime);
            _session.Combat.RegisterActorSource(new CombatActorSource(_bear, ObjectType.Npc,
                BearPrototype, Sector, 10, OnfKos, OcfAnimal, 0, BearDamage));

            SectorNavigationMap map = Map();
            map.Register(pcRuntime, 0);
            map.Register(bearRuntime, 0);
            map.SetControlledObject(pcRuntime);
            _session.Combat.BindNavigationMap(map);
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _bear).Succeeded, Is.True);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void OrdinaryHitHasExplicitOutcomeAtCriticalSuccessUpperBoundaryPlusOne()
        {
            _session.Combat.SetRandomSource(new SequenceRandom(1, 3, 5));
            int before = _session.Vitality.GetCurrentHitPoints(_pc.Identity);

            CombatAttackResult result = _session.Combat.Attack(_bear, _pc.Identity);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Outcome, Is.EqualTo(CombatAttackOutcome.Hit));
            Assert.That(result.CriticalChance, Is.EqualTo(2));
            Assert.That(result.CriticalRoll, Is.EqualTo(3));
            Assert.That(result.MitigatedHitPointDamage, Is.EqualTo(5));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_pc.Identity), Is.EqualTo(before - 5));
        }

        [Test]
        public void OrdinaryMissHasExplicitOutcomeAtCriticalFailureUpperBoundaryPlusOne()
        {
            _session.Combat.SetRandomSource(new SequenceRandom(100, 9));
            int before = _session.Vitality.GetCurrentHitPoints(_pc.Identity);

            CombatAttackResult result = _session.Combat.Attack(_bear, _pc.Identity);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Outcome, Is.EqualTo(CombatAttackOutcome.Miss));
            Assert.That(result.CriticalChance, Is.EqualTo(8));
            Assert.That(result.CriticalRoll, Is.EqualTo(9));
            Assert.That(result.ActionPointsSpent, Is.EqualTo(5));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_pc.Identity), Is.EqualTo(before));
        }

        [Test]
        public void CriticalSuccessAppliesSourceFiftyPercentBonusAfterResistance()
        {
            GivePcTurn();
            CombatHitChance chance = _session.Combat.GetBasicMeleeHitChance(_pc.Identity, _bear);
            _session.Combat.SetRandomSource(chance.DodgeChance > 0
                ? new SequenceRandom(1, 1, 100, 4, 4, 100, 100, 1)
                : new SequenceRandom(1, 1, 4, 4, 100, 100, 1));
            int hpBefore = _session.Vitality.GetCurrentHitPoints(_bear);

            CombatAttackResult result = _session.Combat.Attack(_pc.Identity, _bear);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Outcome, Is.EqualTo(CombatAttackOutcome.CriticalSuccess));
            Assert.That(result.CriticalChance, Is.EqualTo(1));
            Assert.That(result.CriticalRoll, Is.EqualTo(1));
            Assert.That(result.CriticalEffect, Is.EqualTo(CombatCriticalEffect.BonusDamage50));
            Assert.That(result.CriticalEffectRoll, Is.EqualTo(100));
            Assert.That(result.SecondaryCriticalEffectRoll, Is.EqualTo(100));
            Assert.That(result.TertiaryCriticalEffectRoll, Is.EqualTo(1));
            Assert.That(result.RawHitPointDamage, Is.EqualTo(4));
            Assert.That(result.MitigatedHitPointDamage, Is.EqualTo(6));
            Assert.That(result.MitigatedFatigueDamage, Is.EqualTo(4));
            Assert.That(result.EffectTargetIdentity, Is.EqualTo(_bear));
            Assert.That(result.ActionPointsSpent, Is.EqualTo(5));
            Assert.That(result.AmmoQuantityBefore, Is.Zero);
            Assert.That(result.AmmoQuantityAfter, Is.Zero);
            Assert.That(_session.Vitality.GetCurrentHitPoints(_bear), Is.EqualTo(hpBefore - 6));
        }

        [Test]
        public void CriticalSuccessDamageTableUsesInclusiveSourceThresholds()
        {
            GivePcTurn();
            CombatHitChance chance = _session.Combat.GetBasicMeleeHitChance(_pc.Identity, _bear);
            _session.Combat.SetRandomSource(chance.DodgeChance > 0
                ? new SequenceRandom(1, 1, 100, 4, 4, 10)
                : new SequenceRandom(1, 1, 4, 4, 10));

            CombatAttackResult result = _session.Combat.Attack(_pc.Identity, _bear);

            Assert.That(result.CriticalEffect, Is.EqualTo(CombatCriticalEffect.BonusDamage200));
            Assert.That(result.CriticalEffectRoll, Is.EqualTo(10));
            Assert.That(result.MitigatedHitPointDamage, Is.EqualTo(12));
        }

        [Test]
        public void CriticalFailureBoundaryRetargetsUnarmedDamageToAttacker()
        {
            int bearBefore = _session.Vitality.GetCurrentHitPoints(_bear);
            int pcBefore = _session.Vitality.GetCurrentHitPoints(_pc.Identity);
            _session.Combat.SetRandomSource(new SequenceRandom(100, 8, 51, 5));

            CombatAttackResult result = _session.Combat.Attack(_bear, _pc.Identity);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Hit, Is.False);
            Assert.That(result.Outcome, Is.EqualTo(CombatAttackOutcome.CriticalFailure));
            Assert.That(result.CriticalChance, Is.EqualTo(8));
            Assert.That(result.CriticalRoll, Is.EqualTo(8));
            Assert.That(result.CriticalEffect, Is.EqualTo(CombatCriticalEffect.SelfHit));
            Assert.That(result.CriticalEffectRoll, Is.EqualTo(51));
            Assert.That(result.EffectTargetIdentity, Is.EqualTo(_bear));
            Assert.That(result.RawHitPointDamage, Is.EqualTo(5));
            Assert.That(result.MitigatedHitPointDamage, Is.EqualTo(5));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_bear), Is.EqualTo(bearBefore - 5));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_pc.Identity), Is.EqualTo(pcBefore));
            Assert.That(result.ActionPointsSpent, Is.EqualTo(5));
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_pc.Identity));
        }

        [Test]
        public void DeferredCriticalFailureBranchRollsBackAllAuthoritativeState()
        {
            int bearHp = _session.Vitality.GetCurrentHitPoints(_bear);
            int pcHp = _session.Vitality.GetCurrentHitPoints(_pc.Identity);
            int actionPoints = _session.Combat.CurrentActionPoints;
            _session.Combat.SetRandomSource(new SequenceRandom(100, 8, 50));

            CombatAttackResult result = _session.Combat.Attack(_bear, _pc.Identity);

            Assert.That(result.Failure, Is.EqualTo(CombatFailure.UnsupportedCriticalEffect));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_bear), Is.EqualTo(bearHp));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_pc.Identity), Is.EqualTo(pcHp));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(actionPoints));
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_bear));
        }

        [Test]
        public void NpcToPcCriticalSuccessWithoutBoundedSourceEffectRollsBack()
        {
            int bearHp = _session.Vitality.GetCurrentHitPoints(_bear);
            int pcHp = _session.Vitality.GetCurrentHitPoints(_pc.Identity);
            int actionPoints = _session.Combat.CurrentActionPoints;
            _session.Combat.SetRandomSource(new SequenceRandom(1, 2));

            CombatAttackResult result = _session.Combat.Attack(_bear, _pc.Identity);

            Assert.That(result.Failure, Is.EqualTo(CombatFailure.UnsupportedCriticalEffect));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_bear), Is.EqualTo(bearHp));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_pc.Identity), Is.EqualTo(pcHp));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(actionPoints));
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_bear));
        }

        [Test]
        public void LethalCriticalReusesDeathConsequencesExactlyOnce()
        {
            GivePcTurn();
            _session.Vitality.ApplyHitPointDamage(_bear,
                _session.Vitality.GetCurrentHitPoints(_bear) - 1);
            CombatHitChance chance = _session.Combat.GetBasicMeleeHitChance(_pc.Identity, _bear);
            _session.Combat.SetRandomSource(chance.DodgeChance > 0
                ? new SequenceRandom(1, 1, 100, 4, 4, 10)
                : new SequenceRandom(1, 1, 4, 4, 10));

            CombatAttackResult result = _session.Combat.Attack(_pc.Identity, _bear);

            Assert.That(result.Outcome, Is.EqualTo(CombatAttackOutcome.CriticalSuccess));
            Assert.That(_session.Vitality.IsDead(_bear), Is.True);
            Assert.That(_session.TryGetObjectState(_bear, out PersistentObjectState corpse), Is.True);
            Assert.That(corpse.DeathConsequencesProcessed, Is.True);
            Assert.That(_session.DeathConsequences.Process(_pc.Identity, _bear).Failure,
                Is.EqualTo(DeathConsequenceFailure.AlreadyProcessed));
            Assert.That(_session.Combat.Attack(_pc.Identity, _bear).Failure,
                Is.EqualTo(CombatFailure.ParticipantNotRegistered));
        }

        [Test]
        public void SameInjectedSequenceReplaysTheSameCriticalFailureResult()
        {
            _session.Combat.SetRandomSource(new SequenceRandom(100, 8, 100, 4));
            CombatAttackResult result = _session.Combat.Attack(_bear, _pc.Identity);

            Assert.That(result.Outcome, Is.EqualTo(CombatAttackOutcome.CriticalFailure));
            Assert.That(result.CriticalEffect, Is.EqualTo(CombatCriticalEffect.SelfHit));
            Assert.That(result.RawHitPointDamage, Is.EqualTo(4));
            Assert.That(result.CriticalRoll, Is.EqualTo(8));
            Assert.That(result.CriticalEffectRoll, Is.EqualTo(100));
        }

        private void GivePcTurn()
            => Assert.That(_session.Combat.EndCurrentTurn(_bear).Succeeded, Is.True);

        private WorldObject Runtime(string name, ObjectType type, Vector2Int tile)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform);
            WorldObject runtime = go.AddComponent<WorldObject>();
            runtime.Type = type;
            runtime.Tile = tile;
            runtime.TilePosition = tile;
            return runtime;
        }

        private static SectorNavigationMap Map()
        {
            TileNameTable names = TileNameTable.FromMes(MesReader.Read("{300}{grs}"));
            return new SectorNavigationMap(new SectorTerrain(new uint[SectorTerrain.TileCount]),
                new bool[SectorTerrain.TileCount], names);
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
