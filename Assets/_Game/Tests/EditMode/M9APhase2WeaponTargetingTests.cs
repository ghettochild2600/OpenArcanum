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
    [Category("M9APhase2WeaponTargeting")]
    public sealed class M9APhase2WeaponTargetingTests
    {
        private const string Sector = "maps/arcanum1-024-fixed/101602821844.sec";
        private const string ActorKey = "G_9B807B01_A142_4949_80CE_5A085F3BEEB1";
        private const string SecondPcKey = "G_15B807B0_A142_4949_80CE_5A085F3BEEB3";
        private const string SwordKey = "G_2EB46E07_6D15_AD4C_87A1_B8543AA54F91";
        private const string SecondSwordKey = "G_FEB46E07_6D15_AD4C_87A1_B8543AA54F92";
        private const string BowKey = "G_1575DBCA_4990_C243_8184_524D51F7D533";
        private const string ArrowKey = "G_FBFA4631_D97D_D740_9636_F131B2FD9F7B";
        private const string FirearmKey = "G_AEB46E07_6D15_AD4C_87A1_B8543AA54F93";
        private const int OnfKos = 0x00000100;

        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private PersistentPlayerState _pc;
        private WorldObject _pcRuntime;
        private ArcanumObjectId _actor;
        private WorldObject _actorRuntime;
        private SectorNavigationMap _map;
        private CombatAiController _controller;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M9APhase2WeaponTargetingTests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new Owner(_session));
            Assert.That(_session.SelectSector(Sector), Is.True);
            _pc = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(1, 1), 0x28100000u);
            _pcRuntime = Runtime("Player", ObjectType.Pc, new Vector2Int(1, 1));
            _session.BindPlayer(Sector, _pc, _pcRuntime);
            _actor = AddNpc(ActorKey, new Vector2Int(2, 1), 10, 1, out _actorRuntime);
            BindMap();
            _session.Combat.BindRealTimeTimingSource(new FixedTiming());
            _controller = new CombatAiController(_session);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void EquippedSupportedMeleeWeaponUsesProductionWeaponTransaction()
        {
            PersistentObjectState sword = AddWeapon(SwordKey, Sword(), true);
            Start(CombatMode.TurnBased);
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            int actionPoints = _session.Combat.CurrentActionPoints;

            CombatAiDecision decision = _controller.DecideAndSubmit(_actor);

            Assert.That(decision.Action, Is.EqualTo(CombatAiActionKind.Attack));
            Assert.That(decision.AttackResult?.Succeeded, Is.True);
            Assert.That(decision.AttackResult?.WeaponIdentity, Is.EqualTo(sword.Identity));
            Assert.That(decision.AttackResult?.ActionPointCost, Is.EqualTo(sword.WeaponData.AttackActionPointCost));
            Assert.That(_session.Combat.CurrentActionPoints,
                Is.EqualTo(actionPoints - sword.WeaponData.AttackActionPointCost));
            Assert.That(decision.AttackResult?.ModifierLedger.FinalEffectiveValue,
                Is.EqualTo(decision.AttackResult?.Chance.AttackChance));
        }

        [Test]
        public void InventoryMeleeSelectionUsesAuthoritativeEquipmentTransaction()
        {
            PersistentObjectState sword = AddWeapon(SwordKey, Sword(), false);
            Start(CombatMode.TurnBased);
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));

            CombatAiDecision decision = _controller.DecideAndSubmit(_actor);

            Assert.That(decision.AttackResult?.WeaponIdentity, Is.EqualTo(sword.Identity));
            Assert.That(_session.TryGetEquippedItem(_actor, WornLocation.Weapon, out var equipped), Is.True);
            Assert.That(equipped.Identity, Is.EqualTo(sword.Identity));
            Assert.That(_controller.LastWeaponSelection.Changed, Is.True);
        }

        [Test]
        public void ReachableBowWithCompatibleAmmoIsPreferredAtRange()
        {
            AddWeapon(SwordKey, Sword(), false);
            PersistentObjectState bow = AddWeapon(BowKey, Bow(), false);
            PersistentObjectState arrows = AddAmmo(2);
            Move(_actor, _actorRuntime, new Vector2Int(5, 1));
            Start(CombatMode.TurnBased);
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));

            CombatAiDecision decision = _controller.DecideAndSubmit(_actor);

            Assert.That(decision.AttackMode, Is.EqualTo(CombatAttackMode.BasicRanged));
            Assert.That(decision.AttackResult?.WeaponIdentity, Is.EqualTo(bow.Identity));
            Assert.That(arrows.StackQuantity, Is.EqualTo(1));
            Assert.That(_controller.LastWeaponSelection.ReachesTarget, Is.True);
        }

        [Test]
        public void BowWithoutCompatibleAmmoSelectsMeleeFallbackWithoutRetryingShot()
        {
            AddWeapon(BowKey, Bow(), true);
            PersistentObjectState sword = AddWeapon(SwordKey, Sword(), false);
            Start(CombatMode.TurnBased);
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));

            CombatAiDecision decision = _controller.DecideAndSubmit(_actor);

            Assert.That(decision.Failure, Is.EqualTo(CombatFailure.None));
            Assert.That(decision.AttackMode, Is.EqualTo(CombatAttackMode.BasicMelee));
            Assert.That(decision.AttackResult?.WeaponIdentity, Is.EqualTo(sword.Identity));
            Assert.That(_controller.LastWeaponSelection.SelectedWeapon, Is.EqualTo(sword.Identity));
        }

        [Test]
        public void DepletedAmmoTriggersMeleeFallbackBeforeAnotherAttackAttempt()
        {
            PersistentObjectState bow = AddWeapon(BowKey, Bow(), true);
            PersistentObjectState sword = AddWeapon(SwordKey, Sword(), false);
            AddAmmo(1);
            Move(_actor, _actorRuntime, new Vector2Int(5, 1));
            Start(CombatMode.TurnBased);
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));

            CombatAiDecision ranged = _controller.DecideAndSubmit(_actor);
            CombatAiDecision fallback = _controller.DecideAndSubmit(_actor);

            Assert.That(ranged.AttackResult?.WeaponIdentity, Is.EqualTo(bow.Identity));
            Assert.That(ranged.AttackResult?.AmmoQuantityAfter, Is.Zero);
            Assert.That(_session.TryGetEquippedItem(_actor, WornLocation.Weapon, out var equipped), Is.True);
            Assert.That(equipped.Identity, Is.EqualTo(sword.Identity));
            Assert.That(fallback.Failure, Is.Not.EqualTo(CombatFailure.NoAmmo));
            Assert.That(fallback.Action, Is.EqualTo(CombatAiActionKind.Move));
        }

        [Test]
        public void BrokenAndUnsupportedWeaponsAreSkippedForUnarmedFallback()
        {
            AddWeapon(SwordKey, Sword(), false, 0x500004C0u);
            AddWeapon(FirearmKey, Firearm(), false);
            Start(CombatMode.TurnBased);
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));

            CombatAiDecision decision = _controller.DecideAndSubmit(_actor);

            Assert.That(decision.AttackMode, Is.EqualTo(CombatAttackMode.BasicMelee));
            Assert.That(decision.AttackResult?.WeaponIdentity.IsNull, Is.True);
            Assert.That(_controller.LastWeaponSelection.UsesUnarmedFallback, Is.True);
            Assert.That(_controller.LastWeaponSelection.CandidateCount, Is.Zero);
        }

        [Test]
        public void EquivalentWeaponTieUsesStableDescendingObjectIdentity()
        {
            PersistentObjectState lower = AddWeapon(SwordKey, Sword(), false);
            PersistentObjectState higher = AddWeapon(SecondSwordKey, Sword(), false);
            Start(CombatMode.TurnBased);
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));

            _controller.DecideAndSubmit(_actor);

            ArcanumObjectId expected = string.CompareOrdinal(lower.Identity.Key, higher.Identity.Key) > 0
                ? lower.Identity : higher.Identity;
            Assert.That(_controller.LastWeaponSelection.SelectedWeapon, Is.EqualTo(expected));
        }

        [Test]
        public void WeaponSelectionAddsNoApCostBeyondSelectedAttack()
        {
            PersistentObjectState sword = AddWeapon(SwordKey, Sword(), false);
            Start(CombatMode.TurnBased);
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            int before = _session.Combat.CurrentActionPoints;

            CombatAiDecision decision = _controller.DecideAndSubmit(_actor);

            Assert.That(decision.AttackResult?.ActionPointsSpent,
                Is.EqualTo(sword.WeaponData.AttackActionPointCost));
            Assert.That(_session.Combat.CurrentActionPoints,
                Is.EqualTo(before - sword.WeaponData.AttackActionPointCost));
        }

        [Test]
        public void CommittedAttackDangerEventUsesSourceLevelDistanceScoring()
        {
            ArcanumObjectId challenger = AddPc(SecondPcKey, new Vector2Int(6, 1), 4, 10,
                out WorldObject challengerRuntime);
            _map.Register(challengerRuntime, 0);
            Start(CombatMode.TurnBased);
            Assert.That(_session.Combat.RegisterParticipant(challenger).Succeeded, Is.True);
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            _controller.DecideAndSubmit(_actor);
            Assert.That(_controller.TryGetTarget(_actor, out ArcanumObjectId initial), Is.True);
            Assert.That(initial, Is.EqualTo(_pc.Identity));

            Move(challenger, challengerRuntime, new Vector2Int(3, 1));
            AdvanceTurnTo(challenger);
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            Assert.That(_session.Combat.Attack(challenger, _actor).Succeeded, Is.True);
            AdvanceTurnTo(_actor);
            _session.Combat.SetRandomSource(new SequenceRandom(1, 100, 100));

            _controller.DecideAndSubmit(_actor);

            Assert.That(_controller.TryGetTarget(_actor, out ArcanumObjectId selected), Is.True);
            Assert.That(selected, Is.EqualTo(challenger));
            Assert.That(_controller.LastTargetSelection.Reason,
                Is.EqualTo(CombatAiTargetSelectionReason.DangerLevelDistance));
            Assert.That(_controller.LastTargetSelection.CandidateScore,
                Is.GreaterThan(_controller.LastTargetSelection.CurrentScore));
        }

        [Test]
        public void DangerDistanceBranchKeepsCloserFocusAndTieKeepsCurrent()
        {
            ArcanumObjectId challenger = AddPc(SecondPcKey, new Vector2Int(4, 1), 4, 20,
                out WorldObject runtime);
            _map.Register(runtime, 0);
            Start(CombatMode.TurnBased);
            Assert.That(_session.Combat.RegisterParticipant(challenger).Succeeded, Is.True);
            _session.Combat.SetRandomSource(new SequenceRandom(100));

            CombatAiTargetSelection farther = _session.Combat.CompareAiDangerTargets(
                _actor, _pc.Identity, challenger);
            Move(challenger, runtime, new Vector2Int(1, 2));
            _session.Combat.SetRandomSource(new SequenceRandom(100));
            CombatAiTargetSelection tie = _session.Combat.CompareAiDangerTargets(
                _actor, _pc.Identity, challenger);

            Assert.That(farther.Selected, Is.EqualTo(_pc.Identity));
            Assert.That(farther.Reason, Is.EqualTo(CombatAiTargetSelectionReason.DangerDistance));
            Assert.That(tie.CurrentDistance, Is.EqualTo(tie.CandidateDistance));
            Assert.That(tie.Selected, Is.EqualTo(_pc.Identity));
        }

        [Test]
        public void RealTimeSelectionRemainsReadyBusyGoverned()
        {
            PersistentObjectState sword = AddWeapon(SwordKey, Sword(), false);
            Start(CombatMode.RealTime);
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));

            CombatAiDecision ready = _controller.DecideAndSubmit(_actor);
            CombatAiDecision busy = _controller.DecideAndSubmit(_actor);

            Assert.That(ready.Scheduled, Is.True);
            Assert.That(_session.TryGetEquippedItem(_actor, WornLocation.Weapon, out var equipped), Is.True);
            Assert.That(equipped.Identity, Is.EqualTo(sword.Identity));
            Assert.That(busy.Action, Is.EqualTo(CombatAiActionKind.Busy));
            Assert.That(busy.Failure, Is.EqualTo(CombatFailure.ActorBusy));
        }

        [Test]
        public void SaveLoadKeepsCommittedEquipmentButNormalizesAiIntent()
        {
            PersistentObjectState sword = AddWeapon(SwordKey, Sword(), false);
            Start(CombatMode.TurnBased);
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            _controller.DecideAndSubmit(_actor);
            string json = _session.SaveGames.SerializeCurrentSession();

            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);
            _controller.Refresh();

            Assert.That(_session.Combat.IsActive, Is.False);
            Assert.That(_controller.TrackedActorCount, Is.Zero);
            Assert.That(_session.TryGetEquippedItem(_actor, WornLocation.Weapon, out var equipped), Is.True);
            Assert.That(equipped.Identity, Is.EqualTo(sword.Identity));
        }

        private void Start(CombatMode mode)
            => Assert.That(_session.Combat.StartCombat(_pc.Identity, _actor, mode).Succeeded, Is.True);

        private void AdvanceTurnTo(ArcanumObjectId expected)
        {
            for (int index = 0; index < 8 && _session.Combat.CurrentParticipant != expected; index++)
                Assert.That(_session.Combat.EndCurrentTurn(_session.Combat.CurrentParticipant).Succeeded, Is.True);
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(expected));
        }

        private void BindMap()
        {
            bool[] blocked = new bool[SectorTerrain.TileCount];
            TileNameTable names = TileNameTable.FromMes(MesReader.Read("{300}{grs}"));
            _map = new SectorNavigationMap(new SectorTerrain(new uint[SectorTerrain.TileCount]), blocked, names);
            _map.Register(_pcRuntime, 0);
            _map.Register(_actorRuntime, 0);
            _map.SetControlledObject(_pcRuntime);
            _session.Combat.BindNavigationMap(_map);
        }

        private ArcanumObjectId AddNpc(string key, Vector2Int tile, int sourceOrder, int level,
            out WorldObject runtime)
        {
            ArcanumObjectId identity = ParseIdentity(key);
            int[] stats = Stats(level);
            var source = new ObjectInstance(ObjectType.Npc, 28422, Location(tile.x, tile.y),
                0x28100000u, 0, 0, oid: GuidBytes(key));
            PersistentObjectState state = _session.GetOrCreate(source, Sector,
                source.CurrentArtId.Value, false, false);
            RegisterCharacter(identity, ObjectType.Npc, 28422, stats, OnfKos);
            runtime = Runtime("NPC", ObjectType.Npc, tile);
            _session.Bind(Sector, state, runtime);
            _session.Combat.RegisterActorSource(new CombatActorSource(identity, ObjectType.Npc, 28422,
                Sector, sourceOrder, OnfKos, 0, 0, new[] { 3, 6, 0, 0, 0, 0, 0, 0, 0, 0 }));
            return identity;
        }

        private ArcanumObjectId AddPc(string key, Vector2Int tile, int sourceOrder, int level,
            out WorldObject runtime)
        {
            ArcanumObjectId identity = ParseIdentity(key);
            int[] stats = Stats(level);
            var source = new ObjectInstance(ObjectType.Pc, 1, Location(tile.x, tile.y),
                0x28100000u, 0, 0, oid: GuidBytes(key));
            PersistentObjectState state = _session.GetOrCreate(source, Sector,
                source.CurrentArtId.Value, false, false);
            RegisterCharacter(identity, ObjectType.Pc, 1, stats, 0);
            runtime = Runtime("SecondPc", ObjectType.Pc, tile);
            _session.Bind(Sector, state, runtime);
            _session.Combat.RegisterActorSource(new CombatActorSource(identity, ObjectType.Pc, 1,
                Sector, sourceOrder, 0, 0, 0, new int[10]));
            return identity;
        }

        private void RegisterCharacter(ArcanumObjectId identity, ObjectType type, int prototype,
            int[] stats, int npcFlags)
        {
            _session.Characters.GetOrCreateSourceCharacter(identity, type, prototype, stats, null);
            _session.Progression.GetOrCreateSourceCharacter(identity, type, prototype,
                CharacterProgressionSource.Resolve(stats, null, null, null, null, null, 0));
            _session.DerivedStats.GetOrCreateSourceCharacter(identity, type, prototype,
                CharacterDerivedSource.Resolve(stats, null, null, 0, null, null, null, 50, npcFlags, 0));
            _session.Vitality.GetOrCreateSourceCharacter(identity, type, prototype,
                CharacterVitalitySource.Resolve(stats, null, null, 0, null, 15, null, 0,
                    null, 0, null, 0, null, 0));
        }

        private PersistentObjectState AddWeapon(string key, Weapon weapon, bool equipped,
            uint artId = 0x500000C0u)
            => _session.GetOrCreate(new ObjectInstance(ObjectType.Weapon, 6050, null,
                    artId, 0, 0, oid: GuidBytes(key), parentOid: GuidBytes(_actor.Key),
                    invLocation: equipped ? (int)WornLocation.Weapon : 0),
                Sector, artId, false, false, weaponFlags: 0x0E, weaponData: weapon);

        private PersistentObjectState AddAmmo(int quantity)
            => _session.GetOrCreate(new ObjectInstance(ObjectType.Ammo, 7058, null,
                    0x60000001u, 0, 0, oid: GuidBytes(ArrowKey), parentOid: GuidBytes(_actor.Key),
                    invLocation: 0), Sector, 0x60000001u, false, false,
                stackQuantity: quantity, ammoItemType: 0);

        private void Move(ArcanumObjectId identity, WorldObject runtime, Vector2Int tile)
        {
            _map.Unregister(runtime);
            runtime.Tile = tile;
            runtime.TilePosition = tile;
            _session.SetMovementState(identity, tile, runtime.ArtId, false);
            _map.Register(runtime, 0);
            if (identity == _pc.Identity) _map.SetControlledObject(runtime);
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

        private static Weapon Sword()
        {
            var weapon = new Weapon
            {
                Skill = WeaponSkill.Melee,
                Range = 1,
                SpeedFactor = 12,
                MinStrength = 6,
                BonusToHit = 5,
                AmmoType = 10000,
            };
            weapon.DamageMin[(int)DamageType.Normal] = 4;
            weapon.DamageMax[(int)DamageType.Normal] = 8;
            weapon.DamageMin[(int)DamageType.Fatigue] = 2;
            weapon.DamageMax[(int)DamageType.Fatigue] = 4;
            return weapon;
        }

        private static Weapon Bow()
        {
            var weapon = new Weapon
            {
                Skill = WeaponSkill.Bow,
                Range = 15,
                SpeedFactor = 8,
                MinStrength = 8,
                AmmoType = 0,
                AmmoConsumption = 1,
            };
            weapon.DamageMin[(int)DamageType.Normal] = 1;
            weapon.DamageMax[(int)DamageType.Normal] = 10;
            weapon.DamageMin[(int)DamageType.Fatigue] = 2;
            weapon.DamageMax[(int)DamageType.Fatigue] = 5;
            return weapon;
        }

        private static Weapon Firearm()
        {
            Weapon weapon = Bow();
            weapon.Skill = WeaponSkill.Firearms;
            weapon.AmmoType = 1;
            return weapon;
        }

        private static int[] Stats(int level)
        {
            var result = new int[CharacterAttributeSet.SourceStatArrayCount];
            for (int index = 0; index < CharacterAttributeSet.Count; index++) result[index] = 8;
            result[CharacterProgressionSource.LevelSourceSlot] = level;
            result[CharacterAttributeSet.GenderSourceSlot] = (int)CharacterGender.Male;
            result[CharacterAttributeSet.RaceSourceSlot] = (int)CharacterRace.Human;
            return result;
        }

        private static long Location(int x, int y) => (uint)x | ((long)(uint)y << 32);

        private static ArcanumObjectId ParseIdentity(string key)
        {
            Assert.That(ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId identity), Is.True);
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

        private sealed class FixedTiming : ICombatRealTimeTimingSource
        {
            public bool TryGetTiming(CombatRealTimeTimingRequest request, out CombatRealTimeTiming timing)
            {
                timing = new CombatRealTimeTiming(50, 100, 50, 1, 2);
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
