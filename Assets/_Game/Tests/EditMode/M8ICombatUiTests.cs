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
    [Category("M8ICombatUI")]
    public sealed class M8ICombatUiTests
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
        private CombatUiController _controller;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M8ICombatUiTests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new Owner(_session));
            Assert.That(_session.SelectSector(Sector), Is.True);
            _pc = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(1, 1), 0x28100000u);
            _pcRuntime = Runtime("Player", ObjectType.Pc, new Vector2Int(1, 1));
            _session.BindPlayer(Sector, _pc, _pcRuntime);
            _target = AddNpc(new Vector2Int(2, 1));
            _session.GetOrCreate(Item(BowKey, ObjectType.Weapon, 6055,
                    (int)WornLocation.Weapon), Sector, 0x50000000u, false, false,
                weaponFlags: 0x0E, weaponData: Bow());
            _arrows = _session.GetOrCreate(Item(ArrowKey, ObjectType.Ammo, 7058, 0),
                Sector, 0x60000001u, false, false, stackQuantity: 70, ammoItemType: 0);
            _map = Map();
            _map.Register(_pcRuntime, 0);
            _map.Register(_targetRuntime, 0);
            _map.SetControlledObject(_pcRuntime);
            _session.Combat.BindNavigationMap(_map);
            _session.Combat.BindRealTimeTimingSource(new FixedTiming());
            _controller = new CombatUiController(_session);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void PreviewUsesAuthoritativeLedgerWithoutMutatingTransactionState()
        {
            GivePcTurn();
            int ap = _session.Combat.CurrentActionPoints;
            int ammo = _arrows.StackQuantity.Value;
            int hp = _session.Vitality.GetCurrentHitPoints(_target);
            var request = new CombatAttackRequest(_pc.Identity, _target,
                CombatAttackMode.BasicRanged, CombatCalledLocation.Arm);

            CombatAttackPreview preview = _session.Combat.PreviewAttack(request);

            Assert.That(preview.Succeeded, Is.True);
            Assert.That(preview.FinalEffectiveAttackValue, Is.EqualTo(preview.Chance.AttackChance));
            Assert.That(preview.ModifierLedger.Entries.Single(value =>
                value.Reason == CombatAttackModifierReason.CalledLocation).Value, Is.EqualTo(-30));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(ap));
            Assert.That(_arrows.StackQuantity, Is.EqualTo(ammo));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_target), Is.EqualTo(hp));
        }

        [Test]
        public void SelectionRejectsNonparticipantsAndAcceptsActiveHostileExactlyOnce()
        {
            GivePcTurn();
            ArcanumObjectId missing = ArcanumObjectId.CreateGuid(Guid.Parse(
                "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));

            Assert.That(_controller.SelectTarget(missing), Is.False);
            Assert.That(_controller.LastFailure, Is.EqualTo(CombatFailure.ParticipantNotRegistered));
            Assert.That(_controller.SelectTarget(_target), Is.True);
            Assert.That(_controller.SelectedTarget, Is.EqualTo(_target));
            Assert.That(_controller.Preview.Succeeded, Is.False,
                "default melee faithfully reports the equipped Bow as unsupported");
        }

        [TestCase(CombatCalledLocation.Torso, 0)]
        [TestCase(CombatCalledLocation.Head, -50)]
        [TestCase(CombatCalledLocation.Arm, -30)]
        [TestCase(CombatCalledLocation.Leg, -30)]
        public void FourCalledLocationsProjectExactAuthoritativeModifier(
            CombatCalledLocation location, int expected)
        {
            GivePcTurn();
            _controller.SelectTarget(_target);
            _controller.SetAttackMode(CombatAttackMode.BasicRanged);
            _controller.SetCalledLocation(location);

            CombatAttackModifier called = _controller.Preview.ModifierLedger.Entries.Single(value =>
                value.Reason == CombatAttackModifierReason.CalledLocation);
            Assert.That(called.Value, Is.EqualTo(expected));
            Assert.That(called.Applied, Is.True);
            Assert.That(_controller.Preview.FinalEffectiveAttackValue,
                Is.EqualTo(_controller.Preview.Chance.AttackChance));
        }

        [Test]
        public void TurnBasedProjectionShowsCurrentActorAndActionPoints()
        {
            Start(CombatMode.TurnBased);
            _controller.Refresh();

            Assert.That(_controller.Mode, Is.EqualTo(CombatMode.TurnBased));
            Assert.That(_controller.CurrentActor, Is.EqualTo(_target));
            Assert.That(_controller.CurrentActionPoints, Is.EqualTo(_session.Combat.CurrentActionPoints));
            Assert.That(_controller.MaximumActionPoints, Is.EqualTo(_session.Combat.MaximumActionPoints));
            Assert.That(_controller.IsPlayerTurn, Is.False);
        }

        [Test]
        public void MeleeCommandUsesAuthorityAndReportsHit()
        {
            Assert.That(_session.UnequipItem(_pc.Identity, WornLocation.Weapon).Succeeded, Is.True);
            GivePcTurn();
            _controller.SelectTarget(_target);
            _controller.SetAttackMode(CombatAttackMode.BasicMelee);
            _session.Combat.SetRandomSource(new SequenceRandom(1, 100, 4, 4));
            int ap = _session.Combat.CurrentActionPoints;

            Assert.That(_controller.SubmitAttack(), Is.EqualTo(CombatFailure.None));

            Assert.That(_controller.LastResult?.Outcome, Is.EqualTo(CombatAttackOutcome.Hit));
            Assert.That(_controller.Feedback, Is.EqualTo("Hit"));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(ap - 5));
        }

        [Test]
        public void BowCommandReportsMissAndRefreshesAmmo()
        {
            GivePcTurn();
            _controller.SelectTarget(_target);
            _controller.SetAttackMode(CombatAttackMode.BasicRanged);
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));

            Assert.That(_controller.SubmitAttack(), Is.EqualTo(CombatFailure.None));

            Assert.That(_controller.Feedback, Is.EqualTo("Miss"));
            Assert.That(_controller.AmmoQuantity, Is.EqualTo(69));
            Assert.That(_arrows.StackQuantity, Is.EqualTo(69));
        }

        [Test]
        public void CriticalSuccessAndCriticalFailureHaveDistinctFeedback()
        {
            Assert.That(_session.UnequipItem(_pc.Identity, WornLocation.Weapon).Succeeded, Is.True);
            GivePcTurn();
            _controller.SelectTarget(_target);
            _controller.SetAttackMode(CombatAttackMode.BasicMelee);
            _session.Combat.SetRandomSource(new SequenceRandom(1, 1, 4, 4, 100, 100, 1));
            Assert.That(_controller.SubmitAttack(), Is.EqualTo(CombatFailure.None));
            Assert.That(_controller.Feedback, Is.EqualTo("Critical Success"));

            while (_session.Combat.CurrentParticipant != _pc.Identity)
                _session.Combat.EndCurrentTurn(_session.Combat.CurrentParticipant);
            _session.Combat.SetRandomSource(new SequenceRandom(100, 10, 51, 4, 4));
            Assert.That(_controller.SubmitAttack(), Is.EqualTo(CombatFailure.None));
            Assert.That(_controller.Feedback, Is.EqualTo("Critical Failure"));
        }

        [Test]
        public void CriticalDodgeConsumesExistingResultAsDistinctPresentation()
        {
            SetTraining(_target, CharacterSkill.Dodge, SkillTrainingLevel.Expert);
            Assert.That(_session.UnequipItem(_pc.Identity, WornLocation.Weapon).Succeeded, Is.True);
            GivePcTurn();
            _controller.SelectTarget(_target);
            _controller.SetAttackMode(CombatAttackMode.BasicMelee);
            _session.Combat.SetRandomSource(new SequenceRandom(1, 100, 1, 1, 50, 51, 4, 4));

            Assert.That(_controller.SubmitAttack(), Is.EqualTo(CombatFailure.None));

            Assert.That(_controller.LastResult?.CriticalDodge, Is.True);
            Assert.That(_controller.Feedback, Is.EqualTo("Critical Dodge"));
        }

        [Test]
        public void EndTurnDelegatesToAuthoritativeTurnProgression()
        {
            GivePcTurn();
            Assert.That(_controller.EndTurn(), Is.EqualTo(CombatFailure.None));
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_target));
            Assert.That(_controller.Feedback, Is.EqualTo("Turn ended."));
        }

        [Test]
        public void RealTimeReadinessBusyAndRejectedBusyCommandComeFromScheduler()
        {
            Start(CombatMode.RealTime);
            _controller.SelectTarget(_target);
            _controller.SetAttackMode(CombatAttackMode.BasicRanged);
            _controller.Refresh();
            Assert.That(_controller.IsRealTimeReady, Is.True);

            Assert.That(_controller.SubmitAttack(), Is.EqualTo(CombatFailure.None));
            _controller.Refresh();
            Assert.That(_controller.IsRealTimeBusy, Is.True);
            Assert.That(_controller.SubmitAttack(), Is.EqualTo(CombatFailure.ActorBusy));
            Assert.That(_controller.Feedback, Is.EqualTo("Actor is busy."));
        }

        [Test]
        public void FailedPreviewAndCommandLeaveApAmmoVitalityAndTurnUntouched()
        {
            GivePcTurn();
            _targetRuntime.Tile = new Vector2Int(30, 30);
            _targetRuntime.TilePosition = _targetRuntime.Tile;
            _session.SetMovementState(_target, _targetRuntime.Tile, _targetRuntime.ArtId, false);
            _controller.SelectTarget(_target);
            _controller.SetAttackMode(CombatAttackMode.BasicRanged);
            int ap = _session.Combat.CurrentActionPoints;
            int ammo = _arrows.StackQuantity.Value;
            int hp = _session.Vitality.GetCurrentHitPoints(_target);
            ArcanumObjectId turn = _session.Combat.CurrentParticipant;

            Assert.That(_controller.Preview.Failure, Is.EqualTo(CombatFailure.OutOfRange));
            Assert.That(_controller.SubmitAttack(), Is.EqualTo(CombatFailure.OutOfRange));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(ap));
            Assert.That(_arrows.StackQuantity, Is.EqualTo(ammo));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_target), Is.EqualTo(hp));
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(turn));
        }

        [Test]
        public void PresentationRefreshDoesNotMutateOrLoseTransientSelection()
        {
            GivePcTurn();
            _controller.SelectTarget(_target);
            _controller.SetAttackMode(CombatAttackMode.BasicRanged);
            _controller.SetCalledLocation(CombatCalledLocation.Head);
            int ap = _session.Combat.CurrentActionPoints;

            _controller.Refresh();
            _controller.Refresh();

            Assert.That(_controller.SelectedTarget, Is.EqualTo(_target));
            Assert.That(_controller.AttackMode, Is.EqualTo(CombatAttackMode.BasicRanged));
            Assert.That(_controller.CalledLocation, Is.EqualTo(CombatCalledLocation.Head));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(ap));
        }

        [Test]
        public void CombatEndAndSaveLoadNormalizeAllUiTransientState()
        {
            GivePcTurn();
            _controller.SelectTarget(_target);
            string json = _session.SaveGames.SerializeCurrentSession();

            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);
            _controller.Refresh();

            Assert.That(_session.Combat.IsActive, Is.False);
            Assert.That(_controller.HasSelectedTarget, Is.False);
            Assert.That(_controller.LastResult, Is.Null);
            Assert.That(_controller.Feedback, Is.Empty);
        }

        [Test]
        public void PresenterIsAddedByProductionCompositionWithoutOwningAuthority()
        {
            WorldObjectSectorLoader loader = _root.AddComponent<WorldObjectSectorLoader>();
            loader.EnsureProductionPresentationComponents();
            ProductionCombatPresenter presenter = _root.GetComponent<ProductionCombatPresenter>();

            Assert.That(presenter, Is.Not.Null);
            Assert.That(_root.GetComponents<ProductionCombatPresenter>(), Has.Length.EqualTo(1));
            Assert.That(_session.Combat.IsActive, Is.False);
        }

        private void Start(CombatMode mode)
            => Assert.That(_session.Combat.StartCombat(_pc.Identity, _target, mode).Succeeded, Is.True);

        private void GivePcTurn()
        {
            Start(CombatMode.TurnBased);
            while (_session.Combat.CurrentParticipant != _pc.Identity)
                Assert.That(_session.Combat.EndCurrentTurn(
                    _session.Combat.CurrentParticipant).Succeeded, Is.True);
            _controller.Refresh();
        }

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

        private ArcanumObjectId AddNpc(Vector2Int tile)
        {
            ArcanumObjectId identity = ParseIdentity(TargetKey);
            int[] stats = Stats();
            var source = new ObjectInstance(ObjectType.Npc, 28422, Location(tile.x, tile.y),
                0x28100000u, 0, 0, oid: GuidBytes(TargetKey));
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
            _targetRuntime = Runtime("Target", ObjectType.Npc, tile);
            _session.Bind(Sector, state, _targetRuntime);
            _session.Combat.RegisterActorSource(new CombatActorSource(identity, ObjectType.Npc, 28422,
                Sector, 10, OnfKos, 0, 0, new[] { 3, 6, 0, 0, 0, 0, 0, 0, 0, 0 }));
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
            public bool TryGetTiming(CombatRealTimeTimingRequest request,
                out CombatRealTimeTiming timing)
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
