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
    [Category("M9APhase1CombatAI")]
    public sealed class M9APhase1CombatAiTests
    {
        private const string Sector = "maps/arcanum1-024-fixed/101602821844.sec";
        private const string ActorKey = "G_9B807B01_A142_4949_80CE_5A085F3BEEB1";
        private const string SecondNpcKey = "G_05B807B0_A142_4949_80CE_5A085F3BEEB2";
        private const string SecondPcKey = "G_15B807B0_A142_4949_80CE_5A085F3BEEB3";
        private const string BowKey = "G_1575DBCA_4990_C243_8184_524D51F7D533";
        private const string ArrowKey = "G_FBFA4631_D97D_D740_9636_F131B2FD9F7B";
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
            _root = new GameObject("M9APhase1CombatAiTests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new Owner(_session));
            Assert.That(_session.SelectSector(Sector), Is.True);
            _pc = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(1, 1), 0x28100000u);
            _pcRuntime = Runtime("Player", ObjectType.Pc, new Vector2Int(1, 1));
            _session.BindPlayer(Sector, _pc, _pcRuntime);
            _actor = AddNpc(ActorKey, new Vector2Int(2, 1), 10, out _actorRuntime);
            BindMap();
            _session.Combat.BindRealTimeTimingSource(new FixedTiming());
            _controller = new CombatAiController(_session);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void ChoosesNearestPlayerDeterministicallyAndRetainsValidFocus()
        {
            ArcanumObjectId second = AddPc(SecondPcKey, new Vector2Int(3, 1), 4, out WorldObject runtime);
            _map.Register(runtime, 0);
            Start(CombatMode.TurnBased);
            Assert.That(_session.Combat.RegisterParticipant(second).Succeeded, Is.True);
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));

            _controller.DecideAndSubmit(_actor);
            Assert.That(_controller.TryGetTarget(_actor, out ArcanumObjectId selected), Is.True);
            Assert.That(selected, Is.EqualTo(second));

            Move(second, runtime, new Vector2Int(8, 1));
            _controller.DecideAndSubmit(_actor);
            Assert.That(_controller.TryGetTarget(_actor, out selected), Is.True);
            Assert.That(selected, Is.EqualTo(second), "source-valid combat focus is retained before reselection");
        }

        [Test]
        public void DoesNotActOutsideItsTurn()
        {
            Start(CombatMode.TurnBased);
            Assert.That(_session.Combat.EndCurrentTurn(_actor).Succeeded, Is.True);
            int hp = _session.Vitality.GetCurrentHitPoints(_pc.Identity);

            CombatAiDecision decision = _controller.DecideAndSubmit(_actor);

            Assert.That(decision.Action, Is.EqualTo(CombatAiActionKind.Yield));
            Assert.That(decision.Failure, Is.EqualTo(CombatFailure.NotCurrentParticipant));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_pc.Identity), Is.EqualTo(hp));
        }

        [Test]
        public void TurnBasedMeleeUsesKernelApThenYieldsThroughTurnAuthority()
        {
            Start(CombatMode.TurnBased);
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            int maximum = _session.Combat.MaximumActionPoints;

            CombatAiTurnResult result = _controller.RunCurrentTurn();

            Assert.That(result.Attacks, Is.EqualTo(1));
            Assert.That(result.EndedTurn, Is.True);
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_pc.Identity));
            Assert.That(result.LastDecision.Failure, Is.EqualTo(CombatFailure.InsufficientActionPoints));
            Assert.That(maximum, Is.GreaterThanOrEqualTo(CombatStateService.UnarmedAttackActionPointCost));
        }

        [Test]
        public void OutOfRangeMeleeAdvancesOneAuthoritativeStepAndSpendsWalkingAp()
        {
            Move(_actor, _actorRuntime, new Vector2Int(5, 1));
            Start(CombatMode.TurnBased);
            int ap = _session.Combat.CurrentActionPoints;
            Vector2Int start = _actorRuntime.Tile;

            CombatAiDecision decision = _controller.DecideAndSubmit(_actor);

            Assert.That(decision.Action, Is.EqualTo(CombatAiActionKind.Move));
            Assert.That(decision.MoveResult?.Succeeded, Is.True);
            Assert.That(decision.MoveResult?.StepsMoved, Is.EqualTo(1));
            Assert.That(_actorRuntime.Tile, Is.Not.EqualTo(start));
            Assert.That(_session.Combat.CurrentActionPoints,
                Is.EqualTo(ap - CombatStateService.WalkingActionPointCostPerStep));
        }

        [Test]
        public void DecisionSafetyBoundEndsOwnedTurn()
        {
            Start(CombatMode.TurnBased);
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));

            CombatAiTurnResult result = _controller.RunCurrentTurn(1);

            Assert.That(result.SafetyBoundReached, Is.True);
            Assert.That(result.EndedTurn, Is.True);
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_pc.Identity));
        }

        [Test]
        public void LegalBowAttackUsesExistingAmmoLedgerAndAttackTransaction()
        {
            PersistentObjectState arrows = EquipBow(_actor, 3);
            Move(_actor, _actorRuntime, new Vector2Int(4, 1));
            Start(CombatMode.TurnBased);
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            int hp = _session.Vitality.GetCurrentHitPoints(_pc.Identity);

            CombatAiDecision decision = _controller.DecideAndSubmit(_actor);

            Assert.That(decision.Action, Is.EqualTo(CombatAiActionKind.Attack));
            Assert.That(decision.AttackResult?.Succeeded, Is.True);
            Assert.That(decision.AttackResult?.Request.Mode, Is.EqualTo(CombatAttackMode.BasicRanged));
            Assert.That(arrows.StackQuantity, Is.EqualTo(2));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_pc.Identity), Is.EqualTo(hp));
            Assert.That(decision.AttackResult?.ModifierLedger.FinalEffectiveValue,
                Is.EqualTo(decision.AttackResult?.Chance.AttackChance));
        }

        [Test]
        public void HardBlockedBowShotMovesWithoutSpendingAmmoOrMutatingVitality()
        {
            PersistentObjectState arrows = EquipBow(_actor, 3);
            Move(_actor, _actorRuntime, new Vector2Int(3, 1));
            BindMap(new Vector2Int(2, 1));
            Start(CombatMode.TurnBased);
            int hp = _session.Vitality.GetCurrentHitPoints(_pc.Identity);

            CombatAiDecision decision = _controller.DecideAndSubmit(_actor);

            Assert.That(decision.Action, Is.EqualTo(CombatAiActionKind.Move));
            Assert.That(decision.MoveResult?.Succeeded, Is.True);
            Assert.That(arrows.StackQuantity, Is.EqualTo(3));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_pc.Identity), Is.EqualTo(hp));
        }

        [Test]
        public void BowWithoutAmmoYieldsWithoutAnyCombatMutation()
        {
            EquipBow(_actor, 0);
            Start(CombatMode.TurnBased);
            int ap = _session.Combat.CurrentActionPoints;
            int hp = _session.Vitality.GetCurrentHitPoints(_pc.Identity);
            ArcanumObjectId turn = _session.Combat.CurrentParticipant;

            CombatAiDecision decision = _controller.DecideAndSubmit(_actor);

            Assert.That(decision.Action, Is.EqualTo(CombatAiActionKind.Yield));
            Assert.That(decision.Failure, Is.EqualTo(CombatFailure.NoAmmo));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(ap));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_pc.Identity), Is.EqualTo(hp));
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(turn));
        }

        [Test]
        public void RealTimeReadyActorSchedulesOnceAndBusyActorCannotIssueAgain()
        {
            Start(CombatMode.RealTime);
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100, 100, 100));

            CombatAiDecision first = _controller.DecideAndSubmit(_actor);
            CombatAiDecision busy = _controller.DecideAndSubmit(_actor);
            _session.Combat.AdvanceRealTime(100);
            CombatAiDecision recovered = _controller.DecideAndSubmit(_actor);

            Assert.That(first.Scheduled, Is.True);
            Assert.That(busy.Action, Is.EqualTo(CombatAiActionKind.Busy));
            Assert.That(busy.Failure, Is.EqualTo(CombatFailure.ActorBusy));
            Assert.That(recovered.Scheduled, Is.True);
        }

        [Test]
        public void DeadActorCannotActAndCombatTerminatesWithoutOpposition()
        {
            Start(CombatMode.TurnBased);
            _session.Vitality.ApplyHitPointDamage(_actor,
                _session.Vitality.GetCurrentHitPoints(_actor));

            CombatAiDecision decision = _controller.DecideAndSubmit(_actor);

            Assert.That(decision.Failure, Is.EqualTo(CombatFailure.ParticipantUnavailable));
            Assert.That(_session.Combat.IsActive, Is.False);
        }

        [Test]
        public void UnconsciousActorCannotActOrKeepEncounterAlive()
        {
            Start(CombatMode.TurnBased);
            _session.Vitality.ApplyFatigueDamage(_actor,
                _session.Vitality.GetCurrentFatigue(_actor));

            CombatAiDecision decision = _controller.DecideAndSubmit(_actor);

            Assert.That(decision.Failure, Is.EqualTo(CombatFailure.ParticipantUnavailable));
            Assert.That(_session.Combat.IsActive, Is.False);
        }

        [Test]
        public void TargetDeathInvalidatesFocusAndSelectsRemainingPlayer()
        {
            ArcanumObjectId second = AddPc(SecondPcKey, new Vector2Int(3, 1), 4, out WorldObject runtime);
            _map.Register(runtime, 0);
            Start(CombatMode.TurnBased);
            Assert.That(_session.Combat.RegisterParticipant(second).Succeeded, Is.True);
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            _controller.DecideAndSubmit(_actor);
            Assert.That(_controller.TryGetTarget(_actor, out ArcanumObjectId initial), Is.True);
            Assert.That(initial, Is.EqualTo(second));

            _session.Vitality.ApplyHitPointDamage(second,
                _session.Vitality.GetCurrentHitPoints(second));
            _controller.DecideAndSubmit(_actor);

            Assert.That(_controller.TryGetTarget(_actor, out ArcanumObjectId replacement), Is.True);
            Assert.That(replacement, Is.EqualTo(_pc.Identity));
        }

        [Test]
        public void RuntimeEnrollmentIsTrackedExactlyOnceInDeterministicRosterOrder()
        {
            Start(CombatMode.RealTime);
            ArcanumObjectId second = AddNpc(SecondNpcKey, new Vector2Int(3, 1), 5, out WorldObject runtime);
            _map.Register(runtime, 0);
            Assert.That(_session.Combat.EngageParticipant(second).Succeeded, Is.True);
            Assert.That(_session.Combat.EngageParticipant(second).Failure,
                Is.EqualTo(CombatFailure.AlreadyRegistered));

            Assert.That(_controller.TickReadyRealTimeActors(), Is.EqualTo(2));
            Assert.That(_controller.TrackedActorCount, Is.EqualTo(2));
            Assert.That(_controller.TickReadyRealTimeActors(), Is.Zero);
            Assert.That(_controller.TrackedActorCount, Is.EqualTo(2));
            Assert.That(_session.Combat.Participants.Take(2).Select(value => value.Identity),
                Is.EqualTo(new[] { second, _actor }));
        }

        [Test]
        public void SaveLoadNormalizesTransientAiIntentWithoutRestoringAction()
        {
            Start(CombatMode.RealTime);
            Assert.That(_controller.DecideAndSubmit(_actor).Scheduled, Is.True);
            Assert.That(_controller.TrackedActorCount, Is.EqualTo(1));
            string json = _session.SaveGames.SerializeCurrentSession();

            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);
            _controller.Refresh();

            Assert.That(_session.Combat.IsActive, Is.False);
            Assert.That(_controller.TrackedActorCount, Is.Zero);
            Assert.That(_controller.LastDecision.Action, Is.EqualTo(CombatAiActionKind.None));
        }

        [Test]
        public void ProductionCompositionAddsExactlyOneAiDriver()
        {
            WorldObjectSectorLoader loader = _root.AddComponent<WorldObjectSectorLoader>();
            loader.EnsureProductionPresentationComponents();
            loader.EnsureProductionPresentationComponents();

            Assert.That(_root.GetComponents<ProductionCombatAiDriver>(), Has.Length.EqualTo(1));
            Assert.That(_root.GetComponent<ProductionCombatAiDriver>().Controller, Is.Not.Null);
            Assert.That(_session.Combat.IsActive, Is.False);
        }

        private void Start(CombatMode mode)
            => Assert.That(_session.Combat.StartCombat(_pc.Identity, _actor, mode).Succeeded, Is.True);

        private void BindMap(params Vector2Int[] blockedTiles)
        {
            bool[] blocked = new bool[SectorTerrain.TileCount];
            foreach (Vector2Int tile in blockedTiles)
                blocked[tile.x + tile.y * SectorTerrain.Size] = true;
            TileNameTable names = TileNameTable.FromMes(MesReader.Read("{300}{grs}"));
            _map = new SectorNavigationMap(new SectorTerrain(new uint[SectorTerrain.TileCount]), blocked, names);
            _map.Register(_pcRuntime, 0);
            _map.Register(_actorRuntime, 0);
            _map.SetControlledObject(_pcRuntime);
            _session.Combat.BindNavigationMap(_map);
        }

        private ArcanumObjectId AddNpc(string key, Vector2Int tile, int sourceOrder,
            out WorldObject runtime)
        {
            ArcanumObjectId identity = ParseIdentity(key);
            int[] stats = Stats();
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

        private ArcanumObjectId AddPc(string key, Vector2Int tile, int sourceOrder,
            out WorldObject runtime)
        {
            ArcanumObjectId identity = ParseIdentity(key);
            int[] stats = Stats();
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

        private PersistentObjectState EquipBow(ArcanumObjectId actor, int arrows)
        {
            _session.GetOrCreate(new ObjectInstance(ObjectType.Weapon, 6055, null,
                    0x50000000u, 0, 0, oid: GuidBytes(BowKey), parentOid: GuidBytes(actor.Key),
                    invLocation: (int)WornLocation.Weapon), Sector, 0x50000000u, false, false,
                weaponFlags: 0x0E, weaponData: Bow());
            if (arrows <= 0)
                return null;
            return _session.GetOrCreate(new ObjectInstance(ObjectType.Ammo, 7058, null,
                    0x60000001u, 0, 0, oid: GuidBytes(ArrowKey), parentOid: GuidBytes(actor.Key),
                    invLocation: 0), Sector, 0x60000001u, false, false,
                stackQuantity: arrows, ammoItemType: 0);
        }

        private void Move(ArcanumObjectId identity, WorldObject runtime, Vector2Int tile)
        {
            _map?.Unregister(runtime);
            runtime.Tile = tile;
            runtime.TilePosition = tile;
            _session.SetMovementState(identity, tile, runtime.ArtId, false);
            _map?.Register(runtime, 0);
            if (identity == _pc.Identity)
                _map?.SetControlledObject(runtime);
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
