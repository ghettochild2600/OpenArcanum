using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Art;
using Arcanum.Formats.Database;
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
    [Category("M8BTurnBasedCombat")]
    public sealed class M8BTurnBasedCombatTests
    {
        private const string Sector = "maps/arcanum1-024-fixed/47781512457.sec";
        private const string FixtureKey = "G_9B807B01_A142_4949_80CE_5A085F3BEEB1";
        private const int FixturePrototype = 28422;
        private const int OnfKos = 0x00000100;
        private const int OcfAnimal = 0x00008000;
        private static readonly int[] FixtureStats =
            { 7, 4, 5, 17, 4, 5, 5, 14, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 0, 0, 0, 5, 0, 0, 0, 20, 1, 0 };
        private static readonly int[] FixtureNaturalDamage = { 3, 6, 0, 0, 0, 0, 0, 0, 0, 0 };

        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private PersistentPlayerState _pc;
        private WorldObject _pcRuntime;
        private ArcanumObjectId _npc;
        private WorldObject _npcRuntime;
        private SectorNavigationMap _map;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M8BTurnBasedCombatTests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new Owner(_session));
            Assert.That(_session.SelectSector(Sector), Is.True);
            _pc = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(1, 1), 0x28100000u);
            _pcRuntime = Runtime("Player", ObjectType.Pc);
            _session.BindPlayer(Sector, _pc, _pcRuntime);
            _npc = AddNpc(FixtureKey, FixturePrototype, FixtureStats, new Vector2Int(2, 1), 10,
                OnfKos, OcfAnimal, 0, FixtureNaturalDamage, out _npcRuntime);
            _map = Map();
            _map.Register(_pcRuntime, 0);
            _map.Register(_npcRuntime, 0);
            _map.SetControlledObject(_pcRuntime);
            _session.Combat.BindNavigationMap(_map);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void AuthenticPolarBearFixtureExposesNaturalDamageSource()
        {
            string module = GameDataLocator.Find("modules/Arcanum.dat");
            string protoDirectory = GameDataLocator.FindDirectory("data/proto");
            if (string.IsNullOrEmpty(module) || string.IsNullOrEmpty(protoDirectory))
                Assert.Ignore("The local clean Arcanum data is unavailable.");
            using var vfs = new DatVirtualFileSystem();
            vfs.MountFile(module);
            byte[] bytes = vfs.ReadAllBytes(
                "maps/arcanum1-024-fixed/g_017b809b_42a1_4949_80ce_5a085f3beeb1.mob");
            int offset = 0;
            ObjectInstance instance = ObjectInstanceReader.Read(bytes, ref offset);
            ObjectProtoInfo prototype = new ProtoLibrary(protoDirectory).Get(instance.PrototypeNumber);
            int[] naturalDamage = instance.NpcDamage ?? prototype.NpcDamage;

            Assert.That(instance.Identity.Key, Is.EqualTo(FixtureKey));
            Assert.That(instance.PrototypeNumber, Is.EqualTo(FixturePrototype));
            Assert.That(naturalDamage, Is.Not.Null);
            Assert.That(naturalDamage, Has.Length.EqualTo(10));
            Assert.That(naturalDamage, Is.EqualTo(FixtureNaturalDamage));
            Debug.Log("M8B authentic natural damage: " + string.Join(",", naturalDamage));
        }

        [Test]
        public void AuthenticActorStartsWithFiveActionPointsAndBasicAttackCostsFive()
        {
            Start();
            Assert.That(_session.Combat.MaximumActionPoints, Is.EqualTo(5));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(5));
            Assert.That(CombatStateService.UnarmedAttackActionPointCost, Is.EqualTo(5));
        }

        [TestCase(3, 1)]
        [TestCase(3, 2)]
        public void CardinalAndDiagonalMovementSpendTwoActionPointsPerStep(int x, int y)
        {
            Start();
            CombatMoveResult result = _session.Combat.MoveInCombat(_npc, new Vector2Int(x, y));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.StepsMoved, Is.EqualTo(1));
            Assert.That(result.ActionPointsSpent, Is.EqualTo(2));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(3));
            Assert.That(_npcRuntime.Tile, Is.EqualTo(new Vector2Int(x, y)));
            Assert.That(_npcRuntime.IsMoving, Is.False);
        }

        [Test]
        public void NpcOverBudgetMovementRollsBackAllState()
        {
            Start();
            uint art = _npcRuntime.ArtId;
            CombatMoveResult result = _session.Combat.MoveInCombat(_npc, new Vector2Int(5, 1));

            Assert.That(result.Failure, Is.EqualTo(CombatFailure.InsufficientActionPoints));
            Assert.That(result.RouteSteps, Is.GreaterThan(2));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(5));
            Assert.That(_npcRuntime.Tile, Is.EqualTo(new Vector2Int(2, 1)));
            Assert.That(_npcRuntime.ArtId, Is.EqualTo(art));
            Assert.That(_session.States[_npc].TilePosition, Is.EqualTo(new Vector2(2, 1)));
        }

        [Test]
        public void BlockedMovementAndOutOfTurnMovementAreTransactional()
        {
            WorldObject blocker = Runtime("Blocker", ObjectType.Scenery);
            blocker.Tile = new Vector2Int(3, 1);
            blocker.TilePosition = blocker.Tile;
            _map.Register(blocker, 0);
            Start();

            Assert.That(_session.Combat.MoveInCombat(_npc, blocker.Tile).Failure,
                Is.EqualTo(CombatFailure.InvalidDestination));
            Assert.That(_session.Combat.MoveInCombat(_pc.Identity, new Vector2Int(1, 2)).Failure,
                Is.EqualTo(CombatFailure.NotCurrentParticipant));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(5));
            Assert.That(_npcRuntime.Tile, Is.EqualTo(new Vector2Int(2, 1)));
        }

        [Test]
        public void BlockedEdgesMakeRouteUnreachableWithoutMutation()
        {
            foreach (int rotation in new[] { 1, 3, 5, 7 })
            {
                WorldObject wall = Runtime("Wall" + rotation, ObjectType.Wall);
                wall.Tile = new Vector2Int(2, 1);
                wall.ArtId = ((uint)ArtId.TypeWall << 28) | ((uint)rotation << 11);
                _map.Register(wall, 0);
            }
            Start();

            CombatMoveResult result = _session.Combat.MoveInCombat(_npc, new Vector2Int(3, 1));

            Assert.That(result.Failure, Is.EqualTo(CombatFailure.Unreachable));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(5));
            Assert.That(_npcRuntime.Tile, Is.EqualTo(new Vector2Int(2, 1)));
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_npc));
        }

        [Test]
        public void OtherCombatActorTileIsNeverACombatDestination()
        {
            Start();

            CombatMoveResult result = _session.Combat.MoveInCombat(_npc, _pcRuntime.Tile);

            Assert.That(result.Failure, Is.EqualTo(CombatFailure.InvalidDestination));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(5));
            Assert.That(_npcRuntime.Tile, Is.EqualTo(new Vector2Int(2, 1)));
            Assert.That(_pcRuntime.Tile, Is.EqualTo(new Vector2Int(1, 1)));
        }

        [Test]
        public void SuccessfulMovementCommitsFacingAndStandingFrame()
        {
            Start();
            CombatMoveResult result = _session.Combat.MoveInCombat(_npc, new Vector2Int(3, 1));
            int facing = IsoProjection.DirFromDelta(1, 0);

            Assert.That(result.ReachedDestination, Is.True);
            Assert.That(CritterArtResolver.RotationOf(_npcRuntime.ArtId), Is.EqualTo(facing));
            Assert.That((_npcRuntime.ArtId >> 14) & 0x1F, Is.Zero);
            Assert.That(_session.States[_npc].TilePosition, Is.EqualTo(new Vector2(3, 1)));
        }

        [Test]
        public void AuthenticHitChanceUsesMonstrousDexterityCappedMelee()
        {
            Start();
            CombatHitChance chance = _session.Combat.GetBasicMeleeHitChance(_npc, _pc.Identity);

            Assert.That(_session.Progression.GetEffectiveSkillRank(_npc, CharacterSkill.Melee), Is.EqualTo(3));
            Assert.That(chance.MeleeEffectiveness, Is.EqualTo(40));
            Assert.That(chance.ArmorClass, Is.Zero);
            Assert.That(chance.AttackChance, Is.EqualTo(40));
            Assert.That(chance.DodgeChance, Is.Zero);
            Assert.That(chance.FinalChance, Is.EqualTo(40));
        }

        [Test]
        public void SeededBasicAttackHitSpendsApAndUsesM4BVitalityAuthority()
        {
            Start();
            _session.Combat.SetRandomSource(new SequenceRandom(1, 5));
            int beforeHp = _session.Vitality.GetCurrentHitPoints(_pc.Identity);
            int states = _session.States.Count;

            CombatAttackResult result = _session.Combat.Attack(_npc, _pc.Identity);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Hit, Is.True);
            Assert.That(result.RawHitPointDamage, Is.EqualTo(5));
            Assert.That(result.MitigatedHitPointDamage, Is.EqualTo(5));
            Assert.That(result.ActionPointsSpent, Is.EqualTo(5));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_pc.Identity), Is.EqualTo(beforeHp - 5));
            Assert.That(_session.States.Count, Is.EqualTo(states));
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_pc.Identity));
        }

        [Test]
        public void SeededBasicAttackMissStillSpendsApAndAdvancesTurn()
        {
            Start();
            _session.Combat.SetRandomSource(new SequenceRandom(100));
            int beforeHp = _session.Vitality.GetCurrentHitPoints(_pc.Identity);

            CombatAttackResult result = _session.Combat.Attack(_npc, _pc.Identity);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Hit, Is.False);
            Assert.That(result.RawHitPointDamage, Is.Zero);
            Assert.That(_session.Vitality.GetCurrentHitPoints(_pc.Identity), Is.EqualTo(beforeHp));
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_pc.Identity));
        }

        [Test]
        public void ResistanceReducesNormalDamageWithSourceIntegerFormula()
        {
            ArcanumObjectId defender = AddNpc("G_11111111_1111_1111_1111_111111111111", 28001,
                Stats(8, 8, 1), new Vector2Int(3, 1), 20, 0, 0, 0,
                new int[10], out WorldObject defenderRuntime, normalResistance: 50);
            _map.Register(defenderRuntime, 0);
            Start();
            Assert.That(_session.Combat.RegisterParticipant(defender).Succeeded, Is.True);
            _session.Combat.SetRandomSource(new SequenceRandom(1, 5));
            int before = _session.Vitality.GetCurrentHitPoints(defender);

            CombatAttackResult result = _session.Combat.Attack(_npc, defender);

            Assert.That(result.RawHitPointDamage, Is.EqualTo(5));
            Assert.That(result.MitigatedHitPointDamage, Is.EqualTo(3));
            Assert.That(_session.Vitality.GetCurrentHitPoints(defender), Is.EqualTo(before - 3));
        }

        [Test]
        public void InvalidAttackRequestsRollBackActionPointsAndVitality()
        {
            Start();
            int hp = _session.Vitality.GetCurrentHitPoints(_pc.Identity);

            Assert.That(_session.Combat.Attack(_pc.Identity, _npc).Failure,
                Is.EqualTo(CombatFailure.NotCurrentParticipant));
            Assert.That(_session.Combat.Attack(_npc, _npc).Failure, Is.EqualTo(CombatFailure.SameParticipant));
            Assert.That(_session.Combat.Attack(_npc, _pc.Identity, (CombatAttackMode)99).Failure,
                Is.EqualTo(CombatFailure.UnsupportedAttackMode));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(5));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_pc.Identity), Is.EqualTo(hp));
        }

        [Test]
        public void InactiveAndMissingTargetRequestsDoNotMutateCombatState()
        {
            ArcanumObjectId missing = ArcanumObjectId.CreateGuid(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
            Assert.That(_session.Combat.MoveInCombat(_npc, new Vector2Int(3, 1)).Failure,
                Is.EqualTo(CombatFailure.Inactive));
            Assert.That(_session.Combat.Attack(_npc, _pc.Identity).Failure,
                Is.EqualTo(CombatFailure.Inactive));
            Start();

            Assert.That(_session.Combat.Attack(_npc, missing).Failure,
                Is.EqualTo(CombatFailure.ParticipantNotRegistered));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(5));
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_npc));
        }

        [Test]
        public void DeadCurrentActorCannotAttackOrSpendActionPoints()
        {
            Start();
            _session.Vitality.ApplyHitPointDamage(_npc, _session.Vitality.GetCurrentHitPoints(_npc));

            CombatAttackResult result = _session.Combat.Attack(_npc, _pc.Identity);

            Assert.That(result.Failure, Is.EqualTo(CombatFailure.ParticipantUnavailable));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(5));
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_npc));
        }

        [Test]
        public void UnconsciousTargetCannotBeAttackedOrMutated()
        {
            Start();
            int hp = _session.Vitality.GetCurrentHitPoints(_pc.Identity);
            _session.Vitality.ApplyFatigueDamage(_pc.Identity,
                _session.Vitality.GetCurrentFatigue(_pc.Identity));

            CombatAttackResult result = _session.Combat.Attack(_npc, _pc.Identity);

            Assert.That(result.Failure, Is.EqualTo(CombatFailure.ParticipantUnavailable));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(5));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_pc.Identity), Is.EqualTo(hp));
            Assert.That(_session.Vitality.GetCurrentFatigue(_pc.Identity), Is.Zero);
        }

        [Test]
        public void OutOfRangeAndUnavailableTargetRollBackAttack()
        {
            _session.SetMovementState(_pc.Identity, new Vector2(8, 8), _pcRuntime.ArtId, false);
            Start();
            Assert.That(_session.Combat.Attack(_npc, _pc.Identity).Failure, Is.EqualTo(CombatFailure.OutOfRange));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(5));

            _session.SetMovementState(_pc.Identity, new Vector2(1, 1), _pcRuntime.ArtId, false);
            _session.Vitality.ApplyHitPointDamage(_pc.Identity,
                _session.Vitality.GetCurrentHitPoints(_pc.Identity));
            Assert.That(_session.Combat.Attack(_npc, _pc.Identity).Failure,
                Is.EqualTo(CombatFailure.ParticipantUnavailable));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(5));
        }

        [Test]
        public void InsufficientNpcAttackApRollsBackWithoutRandomOrDamage()
        {
            Start();
            Assert.That(_session.Combat.MoveInCombat(_npc, new Vector2Int(2, 2)).Succeeded, Is.True);
            _session.Combat.SetRandomSource(new SequenceRandom());
            int hp = _session.Vitality.GetCurrentHitPoints(_pc.Identity);

            CombatAttackResult result = _session.Combat.Attack(_npc, _pc.Identity);

            Assert.That(result.Failure, Is.EqualTo(CombatFailure.InsufficientActionPoints));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(3));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_pc.Identity), Is.EqualTo(hp));
        }

        [Test]
        public void PcMayOverdrawOneMovementStepForTwoFatigueThenTurnAdvances()
        {
            Start();
            Assert.That(_session.Combat.EndCurrentTurn(_npc).Succeeded, Is.True);
            _session.Combat.SetRandomSource(new SequenceRandom(100));
            Assert.That(_session.Combat.Attack(_pc.Identity, _npc).Succeeded, Is.True);
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(3));
            Assert.That(_session.Combat.MoveInCombat(_pc.Identity, new Vector2Int(1, 2)).Succeeded, Is.True);
            int fatigue = _session.Vitality.GetCurrentFatigue(_pc.Identity);

            CombatMoveResult result = _session.Combat.MoveInCombat(_pc.Identity, new Vector2Int(1, 3));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ActionPointsSpent, Is.EqualTo(1));
            Assert.That(result.FatigueDamage, Is.EqualTo(2));
            Assert.That(_session.Vitality.GetCurrentFatigue(_pc.Identity), Is.EqualTo(fatigue - 2));
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_npc));
            Assert.That(_session.Combat.RoundNumber, Is.EqualTo(2));
        }

        [Test]
        public void UnsupportedNaturalDamageProfileFailsBeforeApOrRng()
        {
            int[] poison = { 1, 2, 0, 1, 0, 0, 0, 0, 0, 0 };
            ArcanumObjectId attacker = AddNpc("G_22222222_2222_2222_2222_222222222222", 28002,
                Stats(8, 8, 1), new Vector2Int(2, 2), 1, OnfKos, OcfAnimal, 0, poison,
                out WorldObject attackerRuntime);
            _map.Register(attackerRuntime, 0);
            Assert.That(_session.Combat.StartCombat(_pc.Identity, attacker).Succeeded, Is.True);
            _session.Combat.SetRandomSource(new SequenceRandom());

            Assert.That(_session.Combat.Attack(attacker, _pc.Identity).Failure,
                Is.EqualTo(CombatFailure.UnsupportedDamageProfile));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(8));
        }

        [Test]
        public void ExactApExhaustionAdvancesAndNextRoundResetsActorAp()
        {
            Start();
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            Assert.That(_session.Combat.Attack(_npc, _pc.Identity).Succeeded, Is.True);
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_pc.Identity));
            Assert.That(_session.Combat.EndCurrentTurn(_pc.Identity).Succeeded, Is.True);
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_npc));
            Assert.That(_session.Combat.RoundNumber, Is.EqualTo(2));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(5));
        }

        [Test]
        public void TurnAdvanceSkipsUnavailableParticipantAndIncrementsRoundOnce()
        {
            Start();
            _session.Vitality.ApplyHitPointDamage(_pc.Identity,
                _session.Vitality.GetCurrentHitPoints(_pc.Identity));

            Assert.That(_session.Combat.EndCurrentTurn(_npc).Succeeded, Is.True);

            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_npc));
            Assert.That(_session.Combat.RoundNumber, Is.EqualTo(2));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(5));
        }

        private void Start() => Assert.That(_session.Combat.StartCombat(_pc.Identity, _npc).Succeeded, Is.True);

        private ArcanumObjectId AddNpc(string key, int prototype, int[] stats, Vector2Int tile,
            int sourceOrder, int npcFlags, int critterFlags, int willKosScript, int[] naturalDamage,
            out WorldObject runtime, int normalResistance = 0)
        {
            ArcanumObjectId identity = ParseIdentity(key);
            var source = new ObjectInstance(ObjectType.Npc, prototype, Location(tile.x, tile.y),
                0x28100000u, 0, 0, oid: GuidBytes(key));
            PersistentObjectState state = _session.GetOrCreate(source, Sector, source.CurrentArtId.Value,
                false, false);
            _session.Characters.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype, stats, null);
            _session.Progression.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype,
                CharacterProgressionSource.Resolve(stats, null, null, null, null, null, critterFlags));
            int[] resistance = new int[CharacterDerivedStatRules.ResistanceCount];
            resistance[(int)CharacterResistance.Normal] = normalResistance;
            _session.DerivedStats.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype,
                CharacterDerivedSource.Resolve(stats, null, null, 0, resistance, null, null, 50,
                    npcFlags, critterFlags));
            int hpAdjustment = prototype == FixturePrototype ? 15 : 0;
            _session.Vitality.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype,
                CharacterVitalitySource.Resolve(stats, null, null, 0, null, hpAdjustment, null, 0,
                    null, 0, null, 0, null, 0));
            runtime = Runtime(key, ObjectType.Npc);
            _session.Bind(Sector, state, runtime);
            _session.Combat.RegisterActorSource(new CombatActorSource(identity, ObjectType.Npc, prototype,
                Sector, sourceOrder, npcFlags, critterFlags, willKosScript, naturalDamage));
            return identity;
        }

        private WorldObject Runtime(string name, ObjectType type)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform);
            WorldObject runtime = go.AddComponent<WorldObject>();
            runtime.Type = type;
            return runtime;
        }

        private static SectorNavigationMap Map(params Vector2Int[] blocked)
        {
            var mask = new bool[SectorTerrain.TileCount];
            foreach (Vector2Int tile in blocked) mask[tile.y * 64 + tile.x] = true;
            TileNameTable names = TileNameTable.FromMes(MesReader.Read("{300}{grs}"));
            return new SectorNavigationMap(new SectorTerrain(new uint[SectorTerrain.TileCount]), mask, names);
        }

        private static int[] Stats(int dexterity, int constitution, int level)
        {
            var result = new int[CharacterAttributeSet.SourceStatArrayCount];
            for (int index = 0; index < CharacterAttributeSet.Count; index++) result[index] = 8;
            result[(int)CharacterAttribute.Dexterity] = dexterity;
            result[(int)CharacterAttribute.Constitution] = constitution;
            result[CharacterProgressionSource.LevelSourceSlot] = level;
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
