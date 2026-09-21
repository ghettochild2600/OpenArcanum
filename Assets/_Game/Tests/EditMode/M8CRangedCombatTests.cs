using System;
using System.Collections.Generic;
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
    [Category("M8CRangedCombat")]
    public sealed class M8CRangedCombatTests
    {
        private const string Sector = "maps/arcanum1-024-fixed/101602821844.sec";
        private const string BowKey = "G_1575DBCA_4990_C243_8184_524D51F7D533";
        private const string ArrowKey = "G_FBFA4631_D97D_D740_9636_F131B2FD9F7B";
        private const string TargetKey = "G_9B807B01_A142_4949_80CE_5A085F3BEEB1";
        private const int OnfKos = 0x00000100;

        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private PersistentPlayerState _pc;
        private WorldObject _pcRuntime;
        private ArcanumObjectId _target;
        private WorldObject _targetRuntime;
        private PersistentObjectState _bow;
        private PersistentObjectState _arrows;
        private SectorNavigationMap _map;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M8CRangedCombatTests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new Owner(_session));
            Assert.That(_session.SelectSector(Sector), Is.True);
            _pc = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(1, 1), 0x28100000u);
            _pcRuntime = Runtime("Player", ObjectType.Pc, new Vector2Int(1, 1));
            _session.BindPlayer(Sector, _pc, _pcRuntime);
            _target = AddNpc(TargetKey, 28422, new Vector2Int(4, 1), out _targetRuntime);
            _bow = _session.GetOrCreate(Item(BowKey, ObjectType.Weapon, 6055,
                    (int)WornLocation.Weapon), Sector, 0x50000000u, false, false,
                weaponFlags: 0x0000000E, weaponData: AuthenticBow());
            _arrows = AddAmmo(70, 0);
            _map = Map();
            _map.Register(_pcRuntime, 0);
            _map.Register(_targetRuntime, 0);
            _map.SetControlledObject(_pcRuntime);
            _session.Combat.BindNavigationMap(_map);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void AuthenticFixtureFactsRemainExact()
        {
            Assert.That(_bow.Identity.Key, Is.EqualTo(BowKey));
            Assert.That(_bow.PrototypeNumber, Is.EqualTo(6055));
            Assert.That(_bow.WeaponFlags, Is.EqualTo(0x0E));
            Assert.That(_bow.WeaponData.Skill, Is.EqualTo(WeaponSkill.Bow));
            Assert.That(_bow.WeaponData.Range, Is.EqualTo(15));
            Assert.That(_bow.WeaponData.SpeedFactor, Is.EqualTo(8));
            Assert.That(_bow.WeaponData.AttackActionPointCost, Is.EqualTo(6));
            Assert.That(_bow.WeaponData.DamageMin, Is.EqualTo(new[] { 1, 0, 0, 0, 2 }));
            Assert.That(_bow.WeaponData.DamageMax, Is.EqualTo(new[] { 10, 0, 0, 0, 5 }));
            Assert.That(_arrows.Identity.Key, Is.EqualTo(ArrowKey));
            Assert.That(_arrows.PrototypeNumber, Is.EqualTo(7058));
            Assert.That(_arrows.AmmoItemType, Is.Zero);
            Assert.That(_arrows.StackQuantity, Is.EqualTo(70));
        }

        [Test]
        public void SeededBowHitSpendsSixApConsumesArrowAndAppliesBothDamageChannels()
        {
            StartPcTurn();
            _session.Combat.SetRandomSource(new SequenceRandom(1, 10, 5));
            int hp = _session.Vitality.GetCurrentHitPoints(_target);
            int fatigue = _session.Vitality.GetCurrentFatigue(_target);

            CombatAttackResult result = Shoot();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Hit, Is.True);
            Assert.That(result.ActionPointCost, Is.EqualTo(6));
            Assert.That(result.ActionPointsSpent, Is.EqualTo(6));
            Assert.That(result.RawHitPointDamage, Is.EqualTo(10));
            Assert.That(result.RawFatigueDamage, Is.EqualTo(5));
            Assert.That(result.AmmoQuantityBefore, Is.EqualTo(70));
            Assert.That(result.AmmoQuantityAfter, Is.EqualTo(69));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_target), Is.EqualTo(hp - 10));
            Assert.That(_session.Vitality.GetCurrentFatigue(_target), Is.EqualTo(fatigue - 5));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(2));
        }

        [Test]
        public void MissStillSpendsApAndConsumesArrowButDoesNoDamage()
        {
            StartPcTurn();
            _session.Combat.SetRandomSource(new SequenceRandom(100));
            int hp = _session.Vitality.GetCurrentHitPoints(_target);

            CombatAttackResult result = Shoot();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Hit, Is.False);
            Assert.That(result.ActionPointsSpent, Is.EqualTo(6));
            Assert.That(_arrows.StackQuantity, Is.EqualTo(69));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_target), Is.EqualTo(hp));
        }

        [Test]
        public void HitChanceIncludesBowSkillMinimumStrengthAndDistance()
        {
            StartPcTurn();
            CombatHitChance chance = _session.Combat.GetBasicRangedHitChance(_pc.Identity, _target,
                _bow.WeaponData, 3);

            Assert.That(chance.MeleeEffectiveness, Is.EqualTo(25));
            Assert.That(chance.ArmorClass, Is.Zero);
            Assert.That(chance.ArmorDifficulty, Is.EqualTo(10));
            Assert.That(chance.AttackChance, Is.EqualTo(15));
        }

        [Test]
        public void OutOfRangeFailsBeforeApAmmoAndRandom()
        {
            var distantTile = new Vector2Int(20, 1);
            _targetRuntime.Tile = distantTile;
            _targetRuntime.TilePosition = distantTile;
            _session.SetMovementState(_target, distantTile, _targetRuntime.ArtId, false);
            StartPcTurn();
            _session.Combat.SetRandomSource(new SequenceRandom());

            AssertFailureUnchanged(Shoot(), CombatFailure.OutOfRange, 70);
        }

        [Test]
        public void BlockingSceneryFailsTransactionAndPortalOpenStateControlsProjectileLos()
        {
            WorldObject blocker = Runtime("Blocker", ObjectType.Scenery, new Vector2Int(2, 1));
            _map.Register(blocker, 0);
            StartPcTurn();
            _session.Combat.SetRandomSource(new SequenceRandom());

            AssertFailureUnchanged(Shoot(), CombatFailure.LineOfFireBlocked, 70);

            _map.Unregister(blocker);
            WorldObject portal = Runtime("Portal", ObjectType.Portal, new Vector2Int(2, 2));
            bool foundPortalLine = false;
            for (int rotation = 1; rotation < 8 && !foundPortalLine; rotation += 2)
            {
                portal.ArtId = CritterArtResolver.WithAnimRotation(0, 0, rotation);
                _map.Register(portal, 0);
                for (int sourceY = 0; sourceY < 6 && !foundPortalLine; sourceY++)
                for (int sourceX = 0; sourceX < 6 && !foundPortalLine; sourceX++)
                for (int targetY = 0; targetY < 6 && !foundPortalLine; targetY++)
                for (int targetX = 0; targetX < 6 && !foundPortalLine; targetX++)
                {
                    var source = new Vector2Int(sourceX, sourceY);
                    var target = new Vector2Int(targetX, targetY);
                    if (source == target) continue;
                    portal.IsOpen = false;
                    bool closed = _map.HasProjectileLineOfFire(source, target);
                    portal.IsOpen = true;
                    bool open = _map.HasProjectileLineOfFire(source, target);
                    foundPortalLine = !closed && open;
                }
                _map.Unregister(portal);
            }
            Assert.That(foundPortalLine, Is.True,
                "a closed source portal edge blocks while the same open edge permits projectile LOS");
        }

        [Test]
        public void ShootThroughSceneryAndInterveningCritterDoNotBlock()
        {
            _map.Register(Runtime("ShootThrough", ObjectType.Scenery, new Vector2Int(2, 1)), 0x20);
            _map.Register(Runtime("InterveningCritter", ObjectType.Npc, new Vector2Int(3, 1)), 0);
            StartPcTurn();
            _session.Combat.SetRandomSource(new SequenceRandom(100));

            Assert.That(Shoot().Succeeded, Is.True);
            Assert.That(_arrows.StackQuantity, Is.EqualTo(69));
        }

        [Test]
        public void WrongAmmoFailsBeforeApAndRandom()
        {
            Assert.That(_session.ConsumeAmmo(_arrows.Identity, 70, out _), Is.True);
            AddAmmo(20, 1, "G_AAAAAAAA_AAAA_AAAA_AAAA_AAAAAAAAAAAA");
            StartPcTurn();
            _session.Combat.SetRandomSource(new SequenceRandom());

            AssertFailureUnchanged(Shoot(), CombatFailure.IncompatibleAmmo, null);
        }

        [Test]
        public void MissingAmmoFailsBeforeApAndRandom()
        {
            Assert.That(_session.ConsumeAmmo(_arrows.Identity, 70, out _), Is.True);
            StartPcTurn();
            _session.Combat.SetRandomSource(new SequenceRandom());

            AssertFailureUnchanged(Shoot(), CombatFailure.NoAmmo, null);
        }

        [Test]
        public void LastArrowIsTombstonedAndNextShotIsTransactionalNoAmmo()
        {
            Assert.That(_session.ConsumeAmmo(_arrows.Identity, 69, out int remaining), Is.True);
            Assert.That(remaining, Is.EqualTo(1));
            StartPcTurn();
            _session.Combat.SetRandomSource(new SequenceRandom(100));

            CombatAttackResult first = Shoot();
            Assert.That(first.Succeeded, Is.True);
            Assert.That(first.AmmoQuantityAfter, Is.Zero);
            Assert.That(_session.IsObjectRemoved(_arrows.Identity), Is.True);
            int ap = _session.Combat.CurrentActionPoints;
            _session.Combat.SetRandomSource(new SequenceRandom());

            Assert.That(Shoot().Failure, Is.EqualTo(CombatFailure.NoAmmo));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(ap));
        }

        [Test]
        public void UnsupportedRangedFamilyFailsBeforeApAmmoAndRandom()
        {
            _bow.WeaponData.Skill = WeaponSkill.Firearms;
            StartPcTurn();
            _session.Combat.SetRandomSource(new SequenceRandom());

            AssertFailureUnchanged(Shoot(), CombatFailure.UnsupportedWeapon, 70);
        }

        [Test]
        public void BasicMeleeStillRejectsEquippedWeaponWithoutMutation()
        {
            StartPcTurn();
            _session.Combat.SetRandomSource(new SequenceRandom());

            CombatAttackResult result = _session.Combat.Attack(_pc.Identity, _target,
                CombatAttackMode.BasicMelee);

            AssertFailureUnchanged(result, CombatFailure.UnsupportedWeapon, 70);
        }

        private CombatAttackResult Shoot()
            => _session.Combat.Attack(_pc.Identity, _target, CombatAttackMode.BasicRanged);

        private void AssertFailureUnchanged(CombatAttackResult result, CombatFailure failure, int? arrows)
        {
            Assert.That(result.Failure, Is.EqualTo(failure));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(8));
            if (arrows.HasValue) Assert.That(_arrows.StackQuantity, Is.EqualTo(arrows.Value));
        }

        private void StartPcTurn()
        {
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _target).Succeeded, Is.True);
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_target));
            Assert.That(_session.Combat.EndCurrentTurn(_target).Succeeded, Is.True);
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_pc.Identity));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(8));
        }

        private PersistentObjectState AddAmmo(int quantity, int ammoType, string key = ArrowKey)
            => _session.GetOrCreate(Item(key, ObjectType.Ammo, ammoType == 0 ? 7058 : 7060, 0),
                Sector, 0x60000001u, false, false, stackQuantity: quantity, ammoItemType: ammoType);

        private ArcanumObjectId AddNpc(string key, int prototype, Vector2Int tile, out WorldObject runtime)
        {
            ArcanumObjectId identity = ParseIdentity(key);
            var source = new ObjectInstance(ObjectType.Npc, prototype, Location(tile.x, tile.y),
                0x28100000u, 0, 0, oid: GuidBytes(key));
            PersistentObjectState state = _session.GetOrCreate(source, Sector, source.CurrentArtId.Value,
                false, false);
            int[] stats = Stats();
            _session.Characters.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype, stats, null);
            _session.Progression.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype,
                CharacterProgressionSource.Resolve(stats, null, null, null, null, null, 0));
            _session.DerivedStats.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype,
                CharacterDerivedSource.Resolve(stats, null, null, 0, null, null, null, 50, OnfKos, 0));
            _session.Vitality.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype,
                CharacterVitalitySource.Resolve(stats, null, null, 0, null, 0, null, 0, null, 0, null, 0,
                    null, 0));
            runtime = Runtime(key, ObjectType.Npc, tile);
            _session.Bind(Sector, state, runtime);
            _session.Combat.RegisterActorSource(new CombatActorSource(identity, ObjectType.Npc, prototype,
                Sector, 10, OnfKos, 0, 0));
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
