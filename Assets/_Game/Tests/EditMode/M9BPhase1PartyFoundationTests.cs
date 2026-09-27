using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Formats.Tiles;
using Arcanum.Formats.World;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.Party;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.World;
using Arcanum.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M9BPhase1PartyFoundation")]
    public sealed class M9BPhase1PartyFoundationTests
    {
        private const string Sector = "maps/arcanum1-024-fixed/101602821844.sec";
        private const string NextSector = "maps/arcanum1-024-fixed/101602821845.sec";
        private const int OnfKos = 0x00000100;
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private PersistentPlayerState _pc;
        private WorldObject _pcRuntime;
        private ArcanumObjectId _follower;
        private WorldObject _followerRuntime;
        private ArcanumObjectId _hostile;
        private WorldObject _hostileRuntime;
        private SectorNavigationMap _map;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M9BPhase1PartyFoundationTests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new Owner(_session));
            Assert.That(_session.SelectSector(Sector), Is.True);
            _pc = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(1, 1), 0x28100000u);
            _pcRuntime = Runtime("Player", ObjectType.Pc, new Vector2Int(1, 1));
            _session.BindPlayer(Sector, _pc, _pcRuntime);
            _follower = AddNpc("G_11111111_1111_1111_1111_111111111111",
                new Vector2Int(8, 1), 10, 0, out _followerRuntime);
            _hostile = AddNpc("G_22222222_2222_2222_2222_222222222222",
                new Vector2Int(3, 1), 20, OnfKos, out _hostileRuntime);
            BindMap();
            _session.Combat.BindRealTimeTimingSource(new FixedTiming());
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void JoinRejectDuplicateRemoveAndPreserveStableIdentity()
        {
            Assert.That(_session.Party.Join(_follower).Succeeded, Is.True);
            Assert.That(_session.Party.Join(_follower).Failure,
                Is.EqualTo(PartyMutationFailure.AlreadyFollowing));
            Assert.That(_session.Party.Members.Single().Identity, Is.EqualTo(_follower));
            Assert.That(_session.Party.Remove(_follower).Succeeded, Is.True);
            Assert.That(_session.Party.Remove(_follower).Failure,
                Is.EqualTo(PartyMutationFailure.NotFollowing));
            Assert.That(_session.TryGetObjectState(_follower, out _), Is.True);
        }

        [Test]
        public void MembershipOrderIsJoinOrderAndForcedFollowersBypassCapacity()
        {
            ArcanumObjectId second = AddNpc("G_33333333_3333_3333_3333_333333333333",
                new Vector2Int(9, 1), 30, 0, out _);
            ArcanumObjectId third = AddNpc("G_44444444_4444_4444_4444_444444444444",
                new Vector2Int(10, 1), 40, 0, out _);
            Assert.That(_session.Party.Capacity, Is.EqualTo(2));
            Assert.That(_session.Party.Join(second).Succeeded, Is.True);
            Assert.That(_session.Party.Join(_follower).Succeeded, Is.True);
            Assert.That(_session.Party.Join(third).Failure, Is.EqualTo(PartyMutationFailure.CapacityReached));
            Assert.That(_session.Party.Join(third, forced: true).Succeeded, Is.True);
            Assert.That(_session.Party.Members.Select(member => member.Identity),
                Is.EqualTo(new[] { second, _follower, third }));
            Assert.That(_session.Party.Members[2].Forced, Is.True);
        }

        [Test]
        public void InvalidDeadAndUnconsciousJoinFailClosed()
        {
            Assert.That(_session.Party.Join(default).Failure, Is.EqualTo(PartyMutationFailure.InvalidFollower));
            _session.Vitality.ApplyHitPointDamage(_follower,
                _session.Vitality.GetCurrentHitPoints(_follower));
            Assert.That(_session.Party.Join(_follower).Failure,
                Is.EqualTo(PartyMutationFailure.FollowerUnavailable));

            ArcanumObjectId second = AddNpc("G_55555555_5555_5555_5555_555555555555",
                new Vector2Int(9, 1), 30, 0, out _);
            _session.Vitality.ApplyFatigueDamage(second, _session.Vitality.GetCurrentFatigue(second));
            Assert.That(_session.Party.Join(second).Failure,
                Is.EqualTo(PartyMutationFailure.FollowerUnavailable));
        }

        [Test]
        public void DefeatDoesNotEraseCommittedMembership()
        {
            Assert.That(_session.Party.Join(_follower).Succeeded, Is.True);
            _session.Vitality.ApplyFatigueDamage(_follower,
                _session.Vitality.GetCurrentFatigue(_follower));
            Assert.That(_session.Party.IsMember(_follower), Is.True);
            Assert.That(_session.Party.CanAccompany(_follower), Is.False);
            _session.Vitality.ApplyHitPointDamage(_follower,
                _session.Vitality.GetCurrentHitPoints(_follower));
            Assert.That(_session.Party.IsMember(_follower), Is.True);
        }

        [Test]
        public void OrdinaryFollowingUsesOneNavigationStepAndDoesNotMoveLeader()
        {
            Assert.That(_session.Party.Join(_follower).Succeeded, Is.True);
            var movement = new PartyFollowerMovementService(_session);
            Vector2Int pcBefore = _pcRuntime.Tile;
            Vector2Int followerBefore = _followerRuntime.Tile;

            Assert.That(movement.AdvanceOneStep(_follower, _map), Is.EqualTo(FollowerMoveResult.Moved));
            Assert.That(InteractionRangeRules.Distance(followerBefore, _followerRuntime.Tile), Is.EqualTo(1));
            Assert.That(_pcRuntime.Tile, Is.EqualTo(pcBefore));
            Assert.That(_session.States[_follower].Placement.TilePosition,
                Is.EqualTo((Vector2)_followerRuntime.Tile));
        }

        [Test]
        public void BlockedFollowingIsSafeAndDoesNotTeleport()
        {
            Assert.That(_session.Party.Join(_follower).Succeeded, Is.True);
            Vector2Int start = _followerRuntime.Tile;
            var blocks = new List<Vector2Int>();
            for (int y = 0; y < SectorTerrain.Size; y++)
            for (int x = 0; x < SectorTerrain.Size; x++)
                if (Math.Max(Math.Abs(x - start.x), Math.Abs(y - start.y)) == 1)
                    blocks.Add(new Vector2Int(x, y));
            BindMap(blocks.ToArray());

            Assert.That(new PartyFollowerMovementService(_session).AdvanceOneStep(_follower, _map),
                Is.EqualTo(FollowerMoveResult.Blocked));
            Assert.That(_followerRuntime.Tile, Is.EqualTo(start));
        }

        [Test]
        public void CrossSectorTransitionPreservesMembershipAndRelocatesSameIdentity()
        {
            Assert.That(_session.Party.Join(_follower).Succeeded, Is.True);
            Assert.That(_session.TryTransitionPlayer(NextSector, new Vector2(2, 2), _pc.ArtId), Is.True);
            Assert.That(_session.Party.Members.Single().Identity, Is.EqualTo(_follower));
            Assert.That(_session.States[_follower].Placement,
                Is.EqualTo(ObjectPlacement.InWorld(NextSector, new Vector2(2, 2))));
            Assert.That(_session.States.Keys.Count(identity => identity == _follower), Is.EqualTo(1));
        }

        [Test]
        public void UnconsciousFollowerRemainsBehindDuringTransitionButMembershipPersists()
        {
            Assert.That(_session.Party.Join(_follower).Succeeded, Is.True);
            ObjectPlacement before = _session.States[_follower].Placement;
            _session.Vitality.ApplyFatigueDamage(_follower,
                _session.Vitality.GetCurrentFatigue(_follower));
            Assert.That(_session.TryTransitionPlayer(NextSector, new Vector2(2, 2), _pc.ArtId), Is.True);
            Assert.That(_session.States[_follower].Placement, Is.EqualTo(before));
            Assert.That(_session.Party.IsMember(_follower), Is.True);
        }

        [Test]
        public void CombatEnrollsFollowerInStablePartyOrderAndProjectsAllies()
        {
            Assert.That(_session.Party.Join(_follower).Succeeded, Is.True);
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _hostile).Succeeded, Is.True);
            Assert.That(_session.Combat.Participants.Select(value => value.Identity),
                Is.EqualTo(new[] { _hostile, _follower, _pc.Identity }));
            Assert.That(_session.Combat.AreAllies(_pc.Identity, _follower), Is.True);
            Assert.That(_session.Combat.AreOpponents(_follower, _hostile), Is.True);
            Assert.That(_session.Combat.AreOpponents(_follower, _pc.Identity), Is.False);
        }

        [Test]
        public void FollowerUsesM9AAgainstHostileAndNeverTargetsLeader()
        {
            Assert.That(_session.Party.Join(_follower).Succeeded, Is.True);
            Move(_follower, _followerRuntime, new Vector2Int(4, 1));
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _hostile).Succeeded, Is.True);
            Assert.That(_session.Combat.EndCurrentTurn(_hostile).Succeeded, Is.True);
            _session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            var ai = new CombatAiController(_session);

            CombatAiDecision decision = ai.DecideAndSubmit(_follower);

            Assert.That(decision.Target, Is.EqualTo(_hostile));
            Assert.That(decision.Target, Is.Not.EqualTo(_pc.Identity));
            Assert.That(decision.AttackResult?.Succeeded, Is.True);
        }

        [Test]
        public void FriendlyAttackRejectsBeforeApOrVitalityMutation()
        {
            Assert.That(_session.Party.Join(_follower).Succeeded, Is.True);
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _hostile).Succeeded, Is.True);
            Assert.That(_session.Combat.EndCurrentTurn(_hostile).Succeeded, Is.True);
            int ap = _session.Combat.CurrentActionPoints;
            int hp = _session.Vitality.GetCurrentHitPoints(_pc.Identity);
            CombatAttackResult result = _session.Combat.Attack(new CombatAttackRequest(
                _follower, _pc.Identity, CombatAttackMode.BasicMelee));
            Assert.That(result.Failure, Is.EqualTo(CombatFailure.TargetNotHostile));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(ap));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_pc.Identity), Is.EqualTo(hp));
        }

        [Test]
        public void RealTimeFollowerUsesExistingReadyBusyScheduler()
        {
            Assert.That(_session.Party.Join(_follower).Succeeded, Is.True);
            Move(_follower, _followerRuntime, new Vector2Int(4, 1));
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _hostile, CombatMode.RealTime).Succeeded, Is.True);
            var ai = new CombatAiController(_session);
            CombatAiDecision first = ai.DecideAndSubmit(_follower);
            CombatAiDecision busy = ai.DecideAndSubmit(_follower);
            Assert.That(first.Scheduled, Is.True);
            Assert.That(busy.Action, Is.EqualTo(CombatAiActionKind.Busy));
        }

        [Test]
        public void FollowerKillCreditsPcAndCannotReplayDeathConsequence()
        {
            Assert.That(_session.Party.Join(_follower).Succeeded, Is.True);
            int before = _session.Progression.GetExperience(_pc.Identity);
            _session.Vitality.ApplyHitPointDamage(_hostile,
                _session.Vitality.GetCurrentHitPoints(_hostile));
            DeathConsequenceResult first = _session.DeathConsequences.Process(_follower, _hostile);
            DeathConsequenceResult replay = _session.DeathConsequences.Process(_follower, _hostile);
            Assert.That(first.Succeeded, Is.True);
            Assert.That(first.ExperienceAwarded, Is.EqualTo(20));
            Assert.That(_session.Progression.GetExperience(_pc.Identity), Is.EqualTo(before + 20));
            Assert.That(replay.Failure, Is.EqualTo(DeathConsequenceFailure.AlreadyProcessed));
        }

        [Test]
        public void SaveV1RoundTripsMembershipAndNormalizesCombatAndFollowTransients()
        {
            Assert.That(_session.Party.Join(_follower, forced: true).Succeeded, Is.True);
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _hostile).Succeeded, Is.True);
            string json = _session.SaveGames.SerializeCurrentSession();
            Assert.That(json, Does.Contain("\"party\""));
            Assert.That(json, Does.Not.Contain("currentParticipant"));
            Assert.That(json, Does.Not.Contain("followPath"));
            Assert.That(_session.Party.Remove(_follower).Succeeded, Is.True);

            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);
            Assert.That(_session.Party.Members.Single(), Is.EqualTo(new PartyMember(_follower, true)));
            Assert.That(_session.Combat.IsActive, Is.False);
            Assert.That(_session.States.Keys.Count(identity => identity == _follower), Is.EqualTo(1));
        }

        [Test]
        public void LegacyV1WithoutPartyLoadsAsEmptyParty()
        {
            SessionSaveData data = _session.SaveGames.CaptureData();
            data.Party = null;
            Assert.That(_session.SaveGames.LoadJson(_session.SaveGames.SerializeData(data)).Succeeded, Is.True);
            Assert.That(_session.Party.Members, Is.Empty);
        }

        [Test]
        public void ProductionCompositionAddsExactlyOneFollowerDriver()
        {
            WorldObjectSectorLoader loader = _root.AddComponent<WorldObjectSectorLoader>();
            loader.EnsureProductionPresentationComponents();
            loader.EnsureProductionPresentationComponents();
            Assert.That(_root.GetComponents<ProductionPartyFollowerDriver>(), Has.Length.EqualTo(1));
        }

        private ArcanumObjectId AddNpc(string key, Vector2Int tile, int sourceOrder, int npcFlags,
            out WorldObject runtime)
        {
            ArcanumObjectId identity = Parse(key);
            int[] stats = Stats();
            var source = new ObjectInstance(ObjectType.Npc, 28422, Location(tile.x, tile.y),
                0x28100000u, 0, 0, oid: GuidBytes(key));
            PersistentObjectState state = _session.GetOrCreate(source, Sector,
                source.CurrentArtId.Value, false, false);
            RegisterCharacter(identity, ObjectType.Npc, 28422, stats, npcFlags);
            runtime = Runtime("Npc", ObjectType.Npc, tile);
            _session.Bind(Sector, state, runtime);
            _session.Combat.RegisterActorSource(new CombatActorSource(identity, ObjectType.Npc, 28422,
                Sector, sourceOrder, npcFlags, 0, 0,
                new[] { 3, 6, 0, 0, 0, 0, 0, 0, 0, 0 }, experienceWorth: 100));
            _map?.Register(runtime, 0);
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

        private void BindMap(params Vector2Int[] blockedTiles)
        {
            bool[] blocked = new bool[SectorTerrain.TileCount];
            foreach (Vector2Int tile in blockedTiles)
                if (tile.x >= 0 && tile.y >= 0 && tile.x < 64 && tile.y < 64)
                    blocked[tile.x + tile.y * SectorTerrain.Size] = true;
            TileNameTable names = TileNameTable.FromMes(MesReader.Read("{300}{grs}"));
            _map = new SectorNavigationMap(new SectorTerrain(new uint[SectorTerrain.TileCount]), blocked, names);
            _map.Register(_pcRuntime, 0);
            _map.Register(_followerRuntime, 0);
            _map.Register(_hostileRuntime, 0);
            _map.SetControlledObject(_pcRuntime);
            _session.Combat.BindNavigationMap(_map);
        }

        private void Move(ArcanumObjectId identity, WorldObject runtime, Vector2Int tile)
        {
            _map.Unregister(runtime);
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
        private static ArcanumObjectId Parse(string key)
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

        private sealed class SequenceRandom : ICombatRandom
        {
            private readonly Queue<int> _values;
            internal SequenceRandom(params int[] values) => _values = new Queue<int>(values);
            public int NextInclusive(int minimum, int maximum)
            {
                Assert.That(_values, Is.Not.Empty);
                int value = _values.Dequeue();
                Assert.That(value, Is.InRange(minimum, maximum));
                return value;
            }
        }

        private sealed class FixedTiming : ICombatRealTimeTimingSource
        {
            public bool TryGetTiming(CombatRealTimeTimingRequest request, out CombatRealTimeTiming timing)
            {
                timing = new CombatRealTimeTiming(50, 100, 50, 1, 2);
                return true;
            }
        }

        private sealed class Owner : ISectorPresentationOwner
        {
            private readonly WorldMapSessionCoordinator _session;
            internal Owner(WorldMapSessionCoordinator session) => _session = session;
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
