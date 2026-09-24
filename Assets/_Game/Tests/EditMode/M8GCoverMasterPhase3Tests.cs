using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Art;
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
    [Category("M8GCoverMasterPhase3")]
    public sealed class M8GCoverMasterPhase3Tests
    {
        private const string Sector = "maps/arcanum1-024-fixed/101602821844.sec";
        private const string BowKey = "G_2575DBCA_4990_C243_8184_524D51F7D533";
        private const string ArrowKey = "G_2BFA4631_D97D_D740_9636_F131B2FD9F7B";
        private const string TargetKey = "G_2B807B01_A142_4949_80CE_5A085F3BEEB1";
        private const int OnfKos = 0x00000100;
        private const int OcfAnimal = 0x00008000;
        private const int ShootThrough = 0x00000020;
        private const int SeeThrough = 0x00000010;
        private const int ProvidesCover = 0x00004000;
        private static readonly int[] BearDamage = { 3, 6, 0, 0, 0, 0, 0, 0, 0, 0 };

        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private PersistentPlayerState _pc;
        private WorldObject _pcRuntime;
        private ArcanumObjectId _target;
        private WorldObject _targetRuntime;
        private PersistentObjectState _arrows;
        private SectorNavigationMap _map;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M8GCoverMasterPhase3Tests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new Owner(_session));
            Assert.That(_session.SelectSector(Sector), Is.True);
            _pc = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(1, 1), 0x28100000u);
            _pcRuntime = Runtime("Player", ObjectType.Pc, new Vector2Int(1, 1));
            _session.BindPlayer(Sector, _pc, _pcRuntime);
            _target = AddBear(new Vector2Int(3, 1));
            _session.GetOrCreate(Item(BowKey, ObjectType.Weapon, 6055,
                    (int)WornLocation.Weapon), Sector, 0x50000000u, false, false,
                weaponFlags: 0x0E, weaponData: AuthenticBow());
            _arrows = _session.GetOrCreate(Item(ArrowKey, ObjectType.Ammo, 7058, 0),
                Sector, 0x60000001u, false, false, stackQuantity: 70, ammoItemType: 0);
            _map = Map();
            _map.Register(_pcRuntime, 0);
            _map.Register(_targetRuntime, 0);
            _map.SetControlledObject(_pcRuntime);
            _session.Combat.BindNavigationMap(_map);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void ClearShotIsLegalAndHasNoCoverContribution()
        {
            ProjectileTraversalResult traversal = _map.GetProjectileTraversal(
                _pcRuntime.Tile, _targetRuntime.Tile);
            Assert.That(traversal.IsBlocked, Is.False);
            Assert.That(traversal.CoverPenalty, Is.Zero);

            GivePcTurn();
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            CombatAttackResult result = Ranged();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(Entry(result, CombatAttackModifierReason.Cover).Value, Is.Zero);
            Assert.That(Entry(result, CombatAttackModifierReason.Cover).Applied, Is.False);
        }

        [Test]
        public void HardBlockRejectsBeforeTransactionMutation()
        {
            WorldObject blocker = AddObstacle("Hard Block", ObjectType.Scenery, new Vector2Int(2, 1), 0);
            Assert.That(_map.GetProjectileTraversal(_pcRuntime.Tile, _targetRuntime.Tile).IsBlocked, Is.True);
            GivePcTurn();
            int ap = _session.Combat.CurrentActionPoints;
            int ammo = _arrows.StackQuantity.Value;
            int hp = _session.Vitality.GetCurrentHitPoints(_target);
            ArcanumObjectId turn = _session.Combat.CurrentParticipant;

            CombatAttackResult result = Ranged();

            Assert.That(result.Failure, Is.EqualTo(CombatFailure.LineOfFireBlocked));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(ap));
            Assert.That(_arrows.StackQuantity, Is.EqualTo(ammo));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_target), Is.EqualTo(hp));
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(turn));
            _map.Unregister(blocker);
        }

        [TestCase(ShootThrough | SeeThrough | ProvidesCover, 20)]
        [TestCase(ShootThrough, 50)]
        public void SourceCoverFlagsProduceExactSingleLedgerContribution(int flags, int expectedPenalty)
        {
            AddObstacle("Cover", ObjectType.Scenery, new Vector2Int(2, 1), flags);
            ProjectileTraversalResult traversal = _map.GetProjectileTraversal(
                _pcRuntime.Tile, _targetRuntime.Tile);
            Assert.That(traversal.IsBlocked, Is.False);
            Assert.That(traversal.CoverPenalty, Is.EqualTo(expectedPenalty));

            GivePcTurn();
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            CombatAttackResult result = Ranged();
            CombatAttackModifier cover = Entry(result, CombatAttackModifierReason.Cover);

            Assert.That(result.ModifierLedger.Entries.Count(value => value.Reason
                == CombatAttackModifierReason.Cover), Is.EqualTo(1));
            Assert.That(cover.Value, Is.EqualTo(-expectedPenalty));
            Assert.That(cover.SourceValue, Is.EqualTo(expectedPenalty));
            Assert.That(result.FinalEffectiveAttackValue,
                Is.EqualTo(result.ModifierLedger.FinalEffectiveValue));
            Assert.That(result.Chance.AttackChance, Is.EqualTo(result.FinalEffectiveAttackValue));
        }

        [Test]
        public void SourceCoverContributionsStackAndFinalEffectivenessAloneClamps()
        {
            AddObstacle("Light Cover", ObjectType.Scenery, new Vector2Int(2, 1),
                ShootThrough | SeeThrough | ProvidesCover);
            AddObstacle("Opaque Cover", ObjectType.Container, new Vector2Int(2, 1), ShootThrough);
            ProjectileTraversalResult traversal = _map.GetProjectileTraversal(
                _pcRuntime.Tile, _targetRuntime.Tile);
            Assert.That(traversal.CoverPenalty, Is.EqualTo(70));

            GivePcTurn();
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            CombatAttackResult result = Ranged();

            Assert.That(Entry(result, CombatAttackModifierReason.Cover).Value, Is.EqualTo(-70));
            Assert.That(result.ModifierLedger.UnclampedTotal, Is.EqualTo(-55));
            Assert.That(result.FinalEffectiveAttackValue, Is.Zero);
        }

        [Test]
        public void ShootThroughWallEdgeContributesSourceCoverOnce()
        {
            WorldObject wall = Runtime("Shoot-through Wall", ObjectType.Wall,
                new Vector2Int(2, 1));
            wall.ArtId = ((uint)ArtId.TypeWall << 28) | ((uint)1 << 11);
            int flags = ShootThrough | SeeThrough | ProvidesCover;
            _map.Register(wall, flags);

            ProjectileTraversalResult traversal = _map.GetProjectileTraversal(
                _pcRuntime.Tile, _targetRuntime.Tile);

            Assert.That(traversal.IsBlocked, Is.False);
            Assert.That(traversal.CoverPenalty, Is.EqualTo(20));
        }

        [Test]
        public void CalledLocationAndCoverComposeOnceInAuthoritativeLedger()
        {
            AddLightCover();
            GivePcTurn();
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));

            CombatAttackResult result = Ranged(CombatCalledLocation.Arm);

            Assert.That(Entry(result, CombatAttackModifierReason.Cover).Value, Is.EqualTo(-20));
            Assert.That(Entry(result, CombatAttackModifierReason.CalledLocation).Value, Is.EqualTo(-30));
            Assert.That(result.ModifierLedger.UnclampedTotal, Is.EqualTo(-35));
            Assert.That(result.FinalEffectiveAttackValue, Is.Zero);
        }

        [Test]
        public void RangeAndCoverComposeOnceInAuthoritativeLedger()
        {
            MoveTarget(new Vector2Int(7, 1));
            AddObstacle("Distant Cover", ObjectType.Scenery, new Vector2Int(4, 1),
                ShootThrough | SeeThrough | ProvidesCover);
            GivePcTurn();
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));

            CombatAttackResult result = Ranged();

            Assert.That(Entry(result, CombatAttackModifierReason.PerceptionRange).Value, Is.EqualTo(-10));
            Assert.That(Entry(result, CombatAttackModifierReason.Cover).Value, Is.EqualTo(-20));
            Assert.That(result.ModifierLedger.UnclampedTotal, Is.EqualTo(-15));
            Assert.That(result.FinalEffectiveAttackValue, Is.Zero);
        }

        [Test]
        public void BowMasterSuppressesOnlyRangeWhileCoverAndCalledLocationRemain()
        {
            SetBowMaster();
            MoveTarget(new Vector2Int(7, 1));
            AddObstacle("Master Cover", ObjectType.Scenery, new Vector2Int(4, 1),
                ShootThrough | SeeThrough | ProvidesCover);
            GivePcTurn();
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));

            CombatAttackResult result = Ranged(CombatCalledLocation.Arm);
            CombatAttackModifier range = Entry(result, CombatAttackModifierReason.PerceptionRange);

            Assert.That(range.Value, Is.EqualTo(-10));
            Assert.That(range.Applied, Is.True);
            Assert.That(range.Suppressed, Is.True);
            Assert.That(Entry(result, CombatAttackModifierReason.Cover).Value, Is.EqualTo(-20));
            Assert.That(Entry(result, CombatAttackModifierReason.Cover).Suppressed, Is.False);
            Assert.That(Entry(result, CombatAttackModifierReason.CalledLocation).Value, Is.EqualTo(-30));
            Assert.That(Entry(result, CombatAttackModifierReason.CalledLocation).Suppressed, Is.False);
            Assert.That(result.ModifierLedger.UnclampedTotal, Is.Zero);
        }

        [Test]
        public void BowMasterDoesNotSuppressNonBowRangePenalty()
        {
            SetBowMaster();
            Weapon firearm = AuthenticBow();
            firearm.Skill = WeaponSkill.Firearms;

            CombatHitChance chance = _session.Combat.GetBasicRangedHitChance(
                _pc.Identity, _target, firearm, 6);

            Assert.That(chance.AttackChance, Is.EqualTo(5));
        }

        [Test]
        public void M8FClassificationUsesLedgerFinalHitDecision()
        {
            AddLightCover();
            GivePcTurn();
            _session.Combat.SetRandomSource(new SequenceRandom(10, 100));

            CombatAttackResult result = Ranged();

            Assert.That(result.FinalEffectiveAttackValue, Is.Zero);
            Assert.That(result.Hit, Is.False);
            Assert.That(result.Outcome, Is.EqualTo(CombatAttackOutcome.Miss));
            Assert.That(result.CriticalRoll, Is.EqualTo(100));
        }

        [Test]
        public void NormalMeleePreservesExistingLedgerAndTransaction()
        {
            MoveTarget(new Vector2Int(2, 1));
            Start();
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100, 3));

            CombatAttackResult result = _session.Combat.Attack(
                new CombatAttackRequest(_target, _pc.Identity));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ModifierLedger.Entries.Count, Is.EqualTo(7));
            Assert.That(result.ModifierLedger.Entries.Any(value => value.Reason
                == CombatAttackModifierReason.Cover), Is.False);
            Assert.That(result.ActionPointsSpent, Is.EqualTo(5));
        }

        private CombatAttackResult Ranged(CombatCalledLocation location = CombatCalledLocation.None)
            => _session.Combat.Attack(new CombatAttackRequest(_pc.Identity, _target,
                CombatAttackMode.BasicRanged, location));

        private static CombatAttackModifier Entry(CombatAttackResult result,
            CombatAttackModifierReason reason)
            => result.ModifierLedger.Entries.Single(value => value.Reason == reason);

        private void SetBowMaster()
        {
            PersistentCharacterProgressionState state = _session.Progression.Get(_pc.Identity);
            int[] purchased = state.CopyPurchasedPoints();
            SkillTrainingLevel[] training = state.CopyTraining();
            purchased[(int)CharacterSkill.Bow] = 5;
            training[(int)CharacterSkill.Bow] = SkillTrainingLevel.Master;
            state.RestoreSkills(purchased, training);
            Assert.That(_session.Progression.GetTrainingLevel(_pc.Identity, CharacterSkill.Bow),
                Is.EqualTo(SkillTrainingLevel.Master));
        }

        private void AddLightCover() => AddObstacle("Light Cover", ObjectType.Scenery,
            new Vector2Int(2, 1), ShootThrough | SeeThrough | ProvidesCover);

        private WorldObject AddObstacle(string name, ObjectType type, Vector2Int tile, int flags)
        {
            WorldObject obstacle = Runtime(name, type, tile);
            obstacle.SourceFlags = flags;
            _map.Register(obstacle, flags);
            return obstacle;
        }

        private void Start()
            => Assert.That(_session.Combat.StartCombat(_pc.Identity, _target).Succeeded, Is.True);

        private void GivePcTurn()
        {
            Start();
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_target));
            Assert.That(_session.Combat.EndCurrentTurn(_target).Succeeded, Is.True);
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_pc.Identity));
        }

        private void MoveTarget(Vector2Int tile)
        {
            Assert.That(_session.SetMovementState(_target, tile, _targetRuntime.ArtId, false), Is.True);
            _targetRuntime.Tile = tile;
            _targetRuntime.TilePosition = tile;
        }

        private ArcanumObjectId AddBear(Vector2Int tile)
        {
            ArcanumObjectId identity = ParseIdentity(TargetKey);
            const int prototype = 28422;
            var source = new ObjectInstance(ObjectType.Npc, prototype, Location(tile.x, tile.y),
                0x28100000u, 0, 0, oid: GuidBytes(TargetKey));
            PersistentObjectState state = _session.GetOrCreate(source, Sector, source.CurrentArtId.Value,
                false, false);
            int[] stats = BearStats();
            _session.Characters.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype, stats, null);
            _session.Progression.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype,
                CharacterProgressionSource.Resolve(stats, null, null, null, null, null, OcfAnimal));
            _session.DerivedStats.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype,
                CharacterDerivedSource.Resolve(stats, null, null, 0, new int[5], null, null, 50,
                    OnfKos, OcfAnimal));
            _session.Vitality.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype,
                CharacterVitalitySource.Resolve(stats, null, null, 0, null, 15, null, 0,
                    null, 0, null, 0, null, 0));
            _targetRuntime = Runtime("Polar Bear Cub", ObjectType.Npc, tile);
            _session.Bind(Sector, state, _targetRuntime);
            _session.Combat.RegisterActorSource(new CombatActorSource(identity, ObjectType.Npc,
                prototype, Sector, 10, OnfKos, OcfAnimal, 0, BearDamage));
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
            return runtime;
        }

        private static Weapon AuthenticBow()
        {
            var weapon = new Weapon
            {
                Skill = WeaponSkill.Bow,
                Range = 15,
                SpeedFactor = 8,
                MinStrength = 10,
                AmmoType = 0,
                AmmoConsumption = 1,
                BonusToHit = 0,
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

        private static int[] BearStats()
            => new[] { 7, 4, 5, 17, 4, 5, 5, 14, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 0, 0, 0, 5, 0, 0, 0, 20, 1, 0 };

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
            Array.Copy(new Guid("25e7b7c9-1ae7-4af5-b1e4-0a62fa6bca01").ToByteArray(), 0, bytes, 8, 16);
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
