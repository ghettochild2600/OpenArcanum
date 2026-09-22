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
    [Category("M8DDefeatState")]
    public sealed class M8DDefeatStateTests
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
        private WorldObject _pcRuntime;
        private ArcanumObjectId _bear;
        private WorldObject _bearRuntime;
        private SectorNavigationMap _map;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M8DDefeatStateTests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new Owner(_session));
            Assert.That(_session.SelectSector(Sector), Is.True);
            _pc = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(1, 1), 0x28100000u);
            _pcRuntime = Runtime("Player", ObjectType.Pc, new Vector2Int(1, 1));
            _session.BindPlayer(Sector, _pc, _pcRuntime);
            _bear = AddNpc(BearKey, BearPrototype, BearStats, new Vector2Int(2, 1), 10,
                OnfKos, OcfAnimal, BearDamage, out _bearRuntime);
            _map = Map();
            _map.Register(_pcRuntime, 0);
            _map.Register(_bearRuntime, 0);
            _map.SetControlledObject(_pcRuntime);
            _session.Combat.BindNavigationMap(_map);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void HpThresholdDerivesDeadStateWithoutIndependentFlag()
        {
            int hp = _session.Vitality.GetCurrentHitPoints(_bear);
            _session.Vitality.ApplyHitPointDamage(_bear, hp - 1);
            Assert.That(_session.Vitality.IsAlive(_bear), Is.True);
            Assert.That(_session.Vitality.IsDead(_bear), Is.False);

            _session.Vitality.ApplyHitPointDamage(_bear, 1);
            Assert.That(_session.Vitality.IsDead(_bear), Is.True);
            Assert.That(_session.Vitality.GetCurrentHitPoints(_bear), Is.Zero);
        }

        [Test]
        public void FatigueThresholdDerivesUnconsciousnessAndIsIdempotent()
        {
            int transitions = 0;
            _session.Vitality.Changed += change =>
            {
                if (change.PreviousFatigue > 0 && change.CurrentFatigue <= 0) transitions++;
            };
            _session.Vitality.ApplyFatigueDamage(_bear, _session.Vitality.GetCurrentFatigue(_bear));
            _session.Vitality.ApplyFatigueDamage(_bear, 4);

            Assert.That(_session.Vitality.IsUnconscious(_bear), Is.True);
            Assert.That(_session.Vitality.IsConscious(_bear), Is.False);
            Assert.That(transitions, Is.EqualTo(1));
        }

        [Test]
        public void AuthenticLethalAttackUsesVitalityThenCreatesSameIdentityCorpse()
        {
            ArcanumObjectId victim = AddNpc("G_11111111_1111_1111_1111_111111111111", 28001,
                Stats(), new Vector2Int(3, 1), 20, 0, 0, new int[10], out WorldObject runtime);
            _map.Register(runtime, 0);
            int hp = _session.Vitality.GetCurrentHitPoints(victim);
            _session.Vitality.ApplyHitPointDamage(victim, hp - 1);
            Start();
            Assert.That(_session.Combat.RegisterParticipant(victim).Succeeded, Is.True);
            _session.Combat.SetRandomSource(new SequenceRandom(1, 3));

            CombatAttackResult result = _session.Combat.Attack(_bear, victim);

            Assert.That(result.Succeeded && result.Hit, Is.True);
            Assert.That(_session.Vitality.IsDead(victim), Is.True);
            Assert.That(runtime.Identity, Is.EqualTo(victim));
            Assert.That(runtime.IsDead, Is.True);
            Assert.That((runtime.ArtId >> 6) & 0x1F, Is.EqualTo(7));
            Assert.That(runtime.Blocks, Is.False);
            Assert.That(_map.IsWalkable(runtime.Tile), Is.True);
            Assert.That(_session.Combat.Participants.Any(value => value.Identity == victim), Is.False);
        }

        [Test]
        public void CurrentActorDeathAdvancesOnceWithoutIncrementingRound()
        {
            Start();
            _session.Vitality.ApplyHitPointDamage(_bear, _session.Vitality.GetCurrentHitPoints(_bear));

            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_pc.Identity));
            Assert.That(_session.Combat.RoundNumber, Is.EqualTo(1));
            Assert.That(_session.Combat.Participants.Select(value => value.Identity),
                Is.EqualTo(new[] { _pc.Identity }));
        }

        [Test]
        public void LaterParticipantDeathDoesNotConsumeCurrentTurn()
        {
            Start();
            int actionPoints = _session.Combat.CurrentActionPoints;
            _session.Vitality.ApplyHitPointDamage(_pc.Identity,
                _session.Vitality.GetCurrentHitPoints(_pc.Identity));

            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_bear));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(actionPoints));
            Assert.That(_session.Combat.RoundNumber, Is.EqualTo(1));
        }

        [Test]
        public void CurrentActorUnconsciousnessEndsTurnButRetainsParticipantAndBlocking()
        {
            Start();
            _session.Vitality.ApplyFatigueDamage(_bear, _session.Vitality.GetCurrentFatigue(_bear));

            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_pc.Identity));
            Assert.That(_session.Combat.Participants.Any(value => value.Identity == _bear), Is.True);
            Assert.That(_bearRuntime.IsDead, Is.False);
            Assert.That(_bearRuntime.Blocks, Is.True);
            Assert.That(_map.IsWalkable(_bearRuntime.Tile), Is.False);
        }

        [Test]
        public void ExplicitEndCombatSucceedsAfterHostileDies()
        {
            Start();
            _session.Vitality.ApplyHitPointDamage(_bear, _session.Vitality.GetCurrentHitPoints(_bear));
            Assert.That(_session.Combat.EndCombat(_pc.Identity).Succeeded, Is.True);
            Assert.That(_session.Combat.IsActive, Is.False);
        }

        [Test]
        public void PostDeathDamageDoesNotRepeatCorpseTransition()
        {
            int transitions = 0;
            _session.Vitality.Changed += change =>
            {
                if (change.PreviousHitPoints > 0 && change.CurrentHitPoints <= 0) transitions++;
            };
            _session.Vitality.ApplyHitPointDamage(_bear, _session.Vitality.GetCurrentHitPoints(_bear));
            uint art = _bearRuntime.ArtId;
            _session.Vitality.ApplyHitPointDamage(_bear, 10);

            Assert.That(transitions, Is.EqualTo(1));
            Assert.That(_bearRuntime.ArtId, Is.EqualTo(art));
            Assert.That(_map.IsWalkable(_bearRuntime.Tile), Is.True);
        }

        [Test]
        public void DeadActionFailurePreservesTurnAndVitality()
        {
            Start();
            _session.Vitality.ApplyHitPointDamage(_bear, _session.Vitality.GetCurrentHitPoints(_bear));
            int pcHp = _session.Vitality.GetCurrentHitPoints(_pc.Identity);
            int ap = _session.Combat.CurrentActionPoints;

            CombatAttackResult result = _session.Combat.Attack(_bear, _pc.Identity);

            Assert.That(result.Failure, Is.EqualTo(CombatFailure.NotCurrentParticipant));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_pc.Identity), Is.EqualTo(pcHp));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(ap));
        }

        [Test]
        public void DeathPreservesContainedAndEquippedRelationships()
        {
            PersistentObjectState carried = AddItem("G_22222222_2222_2222_2222_222222222222", 10078, 0);
            PersistentObjectState equipped = AddItem("G_33333333_3333_3333_3333_333333333333", 4000,
                (int)WornLocation.Weapon);
            ObjectPlacement carriedPlacement = carried.Placement;
            ObjectPlacement equippedPlacement = equipped.Placement;

            _session.Vitality.ApplyHitPointDamage(_bear, _session.Vitality.GetCurrentHitPoints(_bear));

            Assert.That(carried.Placement, Is.EqualTo(carriedPlacement));
            Assert.That(equipped.Placement, Is.EqualTo(equippedPlacement));
            Assert.That(carried.ParentIdentity, Is.EqualTo(_bear));
            Assert.That(equipped.ParentIdentity, Is.EqualTo(_bear));
        }

        [Test]
        public void SaveV1RoundTripKeepsDerivedDeathAndStableIdentity()
        {
            _session.Vitality.ApplyHitPointDamage(_bear, _session.Vitality.GetCurrentHitPoints(_bear));
            string json = _session.SaveGames.SerializeCurrentSession();

            Assert.That(json, Does.Contain("\"version\": 1"));
            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);
            Assert.That(_session.Vitality.IsDead(_bear), Is.True);
            Assert.That(_session.Vitality.Get(_bear).Identity, Is.EqualTo(_bear));
        }

        private void Start() => Assert.That(_session.Combat.StartCombat(_pc.Identity, _bear).Succeeded, Is.True);

        private PersistentObjectState AddItem(string key, int prototype, int inventoryLocation)
        {
            var source = new ObjectInstance(ObjectType.Weapon, prototype, null, 0x50000000u, 0, 0,
                oid: GuidBytes(key), parentOid: GuidBytes(BearKey), invLocation: inventoryLocation);
            return _session.GetOrCreate(source, Sector, source.CurrentArtId.Value, false, false);
        }

        private ArcanumObjectId AddNpc(string key, int prototype, int[] stats, Vector2Int tile,
            int sourceOrder, int npcFlags, int critterFlags, int[] naturalDamage, out WorldObject runtime)
        {
            ArcanumObjectId identity = ParseIdentity(key);
            var source = new ObjectInstance(ObjectType.Npc, prototype, Location(tile.x, tile.y),
                0x28100000u, 0, 0, oid: GuidBytes(key));
            PersistentObjectState state = _session.GetOrCreate(source, Sector, source.CurrentArtId.Value,
                false, false);
            _session.Characters.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype, stats, null);
            _session.Progression.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype,
                CharacterProgressionSource.Resolve(stats, null, null, null, null, null, critterFlags));
            _session.DerivedStats.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype,
                CharacterDerivedSource.Resolve(stats, null, null, 0, null, null, null, 50,
                    npcFlags, critterFlags));
            int hpAdjustment = prototype == BearPrototype ? 15 : 0;
            _session.Vitality.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype,
                CharacterVitalitySource.Resolve(stats, null, null, 0, null, hpAdjustment, null, 0,
                    null, 0, null, 0, null, 0));
            runtime = Runtime(key, ObjectType.Npc, tile);
            _session.Bind(Sector, state, runtime);
            _session.Combat.RegisterActorSource(new CombatActorSource(identity, ObjectType.Npc, prototype,
                Sector, sourceOrder, npcFlags, critterFlags, 0, naturalDamage));
            return identity;
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

        private sealed class SequenceRandom : ICombatRandom
        {
            private readonly Queue<int> _values;
            public SequenceRandom(params int[] values) => _values = new Queue<int>(values);
            public int NextInclusive(int minimum, int maximum)
            {
                Assert.That(_values, Is.Not.Empty, "combat requested unexpected RNG");
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
