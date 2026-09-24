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
    [Category("M8GStructuredAttackPhase2")]
    public sealed class M8GStructuredAttackPhase2Tests
    {
        private const string Sector = "maps/arcanum1-024-fixed/101602821844.sec";
        private const string BowKey = "G_1575DBCA_4990_C243_8184_524D51F7D533";
        private const string ArrowKey = "G_FBFA4631_D97D_D740_9636_F131B2FD9F7B";
        private const string TargetKey = "G_9B807B01_A142_4949_80CE_5A085F3BEEB1";
        private const int OnfKos = 0x00000100;
        private const int OcfAnimal = 0x00008000;
        private static readonly int[] BearDamage = { 3, 6, 0, 0, 0, 0, 0, 0, 0, 0 };

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
            _root = new GameObject("M8GStructuredAttackPhase2Tests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new Owner(_session));
            Assert.That(_session.SelectSector(Sector), Is.True);
            _pc = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(1, 1), 0x28100000u);
            _pcRuntime = Runtime("Player", ObjectType.Pc, new Vector2Int(1, 1));
            _session.BindPlayer(Sector, _pc, _pcRuntime);
            _target = AddBear(new Vector2Int(2, 1));
            _bow = _session.GetOrCreate(Item(BowKey, ObjectType.Weapon, 6055,
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
        public void StructuredMeleeRequestUsesExistingUnarmedTransaction()
        {
            Start();
            _session.Combat.SetRandomSource(new SequenceRandom(1, 100, 3, 0));
            var request = new CombatAttackRequest(_target, _pc.Identity);

            CombatAttackResult result = _session.Combat.Attack(request);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Request.Attacker, Is.EqualTo(_target));
            Assert.That(result.Request.Target, Is.EqualTo(_pc.Identity));
            Assert.That(result.Request.Mode, Is.EqualTo(CombatAttackMode.BasicMelee));
            Assert.That(result.ActionPointCost, Is.EqualTo(5));
            Assert.That(result.ActionPointsSpent, Is.EqualTo(5));
        }

        [Test]
        public void StructuredRangedRequestUsesExistingBowTransaction()
        {
            GivePcTurn();
            _session.Combat.SetRandomSource(new SequenceRandom(1, 100, 5, 2));
            var request = new CombatAttackRequest(_pc.Identity, _target,
                CombatAttackMode.BasicRanged);

            CombatAttackResult result = _session.Combat.Attack(request);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.WeaponIdentity, Is.EqualTo(_bow.Identity));
            Assert.That(result.AmmoIdentity, Is.EqualTo(_arrows.Identity));
            Assert.That(result.AmmoQuantityBefore, Is.EqualTo(70));
            Assert.That(result.AmmoQuantityAfter, Is.EqualTo(69));
        }

        [Test]
        public void RequestResolvesAuthoritativePositionAtExecutionTime()
        {
            var request = new CombatAttackRequest(_pc.Identity, _target,
                CombatAttackMode.BasicRanged);
            MoveTarget(new Vector2Int(20, 1));
            GivePcTurn();
            int ap = _session.Combat.CurrentActionPoints;

            CombatAttackResult result = _session.Combat.Attack(request);

            Assert.That(result.Failure, Is.EqualTo(CombatFailure.OutOfRange));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(ap));
            Assert.That(_arrows.StackQuantity, Is.EqualTo(70));
        }

        [Test]
        public void InvalidCalledLocationFailsBeforeEveryMutation()
        {
            GivePcTurn();
            int hp = _session.Vitality.GetCurrentHitPoints(_target);
            int ap = _session.Combat.CurrentActionPoints;
            var request = new CombatAttackRequest(_pc.Identity, _target,
                CombatAttackMode.BasicRanged, (CombatCalledLocation)99);

            CombatAttackResult result = _session.Combat.Attack(request);

            Assert.That(result.Failure, Is.EqualTo(CombatFailure.InvalidCalledLocation));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(ap));
            Assert.That(_arrows.StackQuantity, Is.EqualTo(70));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_target), Is.EqualTo(hp));
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_pc.Identity));
        }

        [Test]
        public void DeadTargetRequestFailsFromCurrentAuthoritativeState()
        {
            GivePcTurn();
            _session.Vitality.ApplyHitPointDamage(_target,
                _session.Vitality.GetCurrentHitPoints(_target));
            int ap = _session.Combat.CurrentActionPoints;

            CombatAttackResult result = _session.Combat.Attack(new CombatAttackRequest(
                _pc.Identity, _target, CombatAttackMode.BasicRanged));

            Assert.That(result.Failure, Is.EqualTo(CombatFailure.ParticipantNotRegistered));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(ap));
            Assert.That(_arrows.StackQuantity, Is.EqualTo(70));
        }

        [TestCase(CombatCalledLocation.Torso, 0, 40)]
        [TestCase(CombatCalledLocation.Head, -50, 0)]
        [TestCase(CombatCalledLocation.Arm, -30, 10)]
        [TestCase(CombatCalledLocation.Leg, -30, 10)]
        public void SourceCalledLocationIdsMapToExactEffectivenessModifier(
            CombatCalledLocation location, int expectedModifier, int expectedFinal)
        {
            Start();
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100, 3));

            CombatAttackResult result = _session.Combat.Attack(new CombatAttackRequest(
                _target, _pc.Identity, CombatAttackMode.BasicMelee, location));

            CombatAttackModifier entry = Entry(result, CombatAttackModifierReason.CalledLocation);
            Assert.That((int)location, Is.EqualTo(location switch
            {
                CombatCalledLocation.Torso => 0,
                CombatCalledLocation.Head => 1,
                CombatCalledLocation.Arm => 2,
                CombatCalledLocation.Leg => 3,
                _ => -1,
            }));
            Assert.That(entry.Value, Is.EqualTo(expectedModifier));
            Assert.That(entry.Applied, Is.True);
            Assert.That(result.FinalEffectiveAttackValue, Is.EqualTo(expectedFinal));
        }

        [Test]
        public void NoCalledLocationPreservesBaselineAndLedgerIsAuthoritative()
        {
            Start();
            CombatHitChance baseline = _session.Combat.GetBasicMeleeHitChance(_target, _pc.Identity);
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100, 3));

            CombatAttackResult result = _session.Combat.Attack(new CombatAttackRequest(
                _target, _pc.Identity));

            Assert.That(result.Request.CalledLocation, Is.EqualTo(CombatCalledLocation.None));
            Assert.That(result.Chance.AttackChance, Is.EqualTo(baseline.AttackChance));
            Assert.That(result.ModifierLedger.UnclampedTotal,
                Is.EqualTo(result.ModifierLedger.Entries.Where(value => value.Applied && !value.Suppressed)
                    .Sum(value => value.Value)));
            Assert.That(result.FinalEffectiveAttackValue, Is.EqualTo(result.Chance.AttackChance));
            Assert.That(result.ModifierLedger.Entries.Select(value => value.Stage), Is.Ordered);
        }

        [Test]
        public void RangedLedgerSeparatesSkillStrengthRangeWeaponAndLocation()
        {
            GivePcTurn();
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100, 3));

            CombatAttackResult result = _session.Combat.Attack(new CombatAttackRequest(
                _pc.Identity, _target, CombatAttackMode.BasicRanged, CombatCalledLocation.Arm));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ModifierLedger.Entries.Select(value => value.Reason), Is.EqualTo(new[]
            {
                CombatAttackModifierReason.BaseSkill,
                CombatAttackModifierReason.IntelligenceTwenty,
                CombatAttackModifierReason.ArmorClass,
                CombatAttackModifierReason.MinimumStrength,
                CombatAttackModifierReason.PerceptionRange,
                CombatAttackModifierReason.Cover,
                CombatAttackModifierReason.WeaponToHit,
                CombatAttackModifierReason.CalledLocation,
            }));
            Assert.That(Entry(result, CombatAttackModifierReason.MinimumStrength).Value, Is.EqualTo(-10));
            Assert.That(Entry(result, CombatAttackModifierReason.PerceptionRange).Value, Is.Zero);
            Assert.That(Entry(result, CombatAttackModifierReason.Cover).Value, Is.Zero);
            Assert.That(Entry(result, CombatAttackModifierReason.WeaponToHit).Value, Is.Zero);
            Assert.That(Entry(result, CombatAttackModifierReason.CalledLocation).Value, Is.EqualTo(-30));
            Assert.That(result.FinalEffectiveAttackValue, Is.EqualTo(result.Chance.AttackChance));
        }

        [Test]
        public void CalledArmCriticalSuccessUsesLocationBonusAndExistingDamagePath()
        {
            Assert.That(_session.Progression.IncreaseSkill(_pc.Identity, CharacterSkill.Bow),
                Is.EqualTo(SkillIncreaseResult.Success));
            GivePcTurn();
            _session.Combat.SetRandomSource(new SequenceRandom(1, 1, 5, 2, 100, 100, 100));

            CombatAttackResult result = _session.Combat.Attack(new CombatAttackRequest(
                _pc.Identity, _target, CombatAttackMode.BasicRanged, CombatCalledLocation.Arm));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Outcome, Is.EqualTo(CombatAttackOutcome.CriticalSuccess));
            Assert.That(result.CriticalChance, Is.EqualTo(8));
            Assert.That(result.CriticalEffect, Is.EqualTo(CombatCriticalEffect.BonusDamage50));
            Assert.That(result.MitigatedHitPointDamage, Is.EqualTo(7));
        }

        [Test]
        public void CalledMeleeCriticalFailureUsesExistingSelfHitPath()
        {
            Start();
            _session.Combat.SetRandomSource(new SequenceRandom(100, 1, 100, 3, 0));
            int before = _session.Vitality.GetCurrentHitPoints(_target);

            CombatAttackResult result = _session.Combat.Attack(new CombatAttackRequest(
                _target, _pc.Identity, CombatAttackMode.BasicMelee, CombatCalledLocation.Head));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Outcome, Is.EqualTo(CombatAttackOutcome.CriticalFailure));
            Assert.That(result.CriticalEffect, Is.EqualTo(CombatCriticalEffect.SelfHit));
            Assert.That(result.EffectTargetIdentity, Is.EqualTo(_target));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_target), Is.LessThan(before));
        }

        [Test]
        public void UnsupportedNpcToPcCalledCriticalRollsBackAtomically()
        {
            Start();
            _session.Combat.SetRandomSource(new SequenceRandom(1, 1, 100));
            int hp = _session.Vitality.GetCurrentHitPoints(_pc.Identity);
            int ap = _session.Combat.CurrentActionPoints;

            CombatAttackResult result = _session.Combat.Attack(new CombatAttackRequest(
                _target, _pc.Identity, CombatAttackMode.BasicMelee, CombatCalledLocation.Arm));

            Assert.That(result.Failure, Is.EqualTo(CombatFailure.UnsupportedCriticalEffect));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(ap));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_pc.Identity), Is.EqualTo(hp));
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_target));
        }

        [Test]
        public void GraphicsRebindCannotChangeRequestOrModifierAuthority()
        {
            GivePcTurn();
            var request = new CombatAttackRequest(_pc.Identity, _target,
                CombatAttackMode.BasicRanged, CombatCalledLocation.Leg);
            _session.Combat.BindNavigationMap(_map);
            _session.Combat.BindNavigationMap(_map);
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));

            CombatAttackResult result = _session.Combat.Attack(request);

            Assert.That(result.Request.CalledLocation, Is.EqualTo(CombatCalledLocation.Leg));
            Assert.That(Entry(result, CombatAttackModifierReason.CalledLocation).Value, Is.EqualTo(-30));
            Assert.That(result.FinalEffectiveAttackValue, Is.EqualTo(result.Chance.AttackChance));
        }

        [Test]
        public void SaveLoadDropsLastRequestAndLedgerButPreservesCommittedVitality()
        {
            GivePcTurn();
            _session.Combat.SetRandomSource(new SequenceRandom(1, 100, 5, 2));
            CombatAttackResult attack = _session.Combat.Attack(new CombatAttackRequest(
                _pc.Identity, _target, CombatAttackMode.BasicRanged));
            Assert.That(attack.Succeeded, Is.True);
            int hp = _session.Vitality.GetCurrentHitPoints(_target);
            Assert.That(_session.Combat.LastAttackResult.HasValue, Is.True);
            string json = _session.SaveGames.SerializeCurrentSession();

            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);

            Assert.That(_session.Combat.IsActive, Is.False);
            Assert.That(_session.Combat.LastAttackResult.HasValue, Is.False);
            Assert.That(_session.Vitality.GetCurrentHitPoints(_target), Is.EqualTo(hp));
        }

        [Test]
        public void StructuredAttackDoesNotDuplicatePhase1RoundBoundary()
        {
            int boundaries = 0;
            _session.Combat.RoundCompleted += _ => boundaries++;
            Start();
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100, 3));

            CombatAttackResult result = _session.Combat.Attack(new CombatAttackRequest(
                _target, _pc.Identity));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(boundaries, Is.Zero);
            Assert.That(_session.Combat.RoundNumber, Is.EqualTo(1));
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_pc.Identity));
        }

        private static CombatAttackModifier Entry(CombatAttackResult result,
            CombatAttackModifierReason reason)
            => result.ModifierLedger.Entries.Single(value => value.Reason == reason);

        private void Start() => Assert.That(_session.Combat.StartCombat(_pc.Identity, _target).Succeeded, Is.True);

        private void GivePcTurn()
        {
            Start();
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_target));
            Assert.That(_session.Combat.EndCurrentTurn(_target).Succeeded, Is.True);
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_pc.Identity));
        }

        private void MoveTarget(Vector2Int tile)
        {
            _targetRuntime.Tile = tile;
            _targetRuntime.TilePosition = tile;
            Assert.That(_session.SetMovementState(_target, tile, _targetRuntime.ArtId, false), Is.True);
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
