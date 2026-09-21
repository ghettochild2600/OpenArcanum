using System;
using System.Linq;
using Arcanum.Formats.Database;
using Arcanum.Formats.Objects;
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
    [Category("M8ACoreCombatState")]
    public sealed class M8ACoreCombatStateTests
    {
        private const string Sector = "maps/arcanum1-024-fixed/47781512457.sec";
        private const string FixtureKey = "G_9B807B01_A142_4949_80CE_5A085F3BEEB1";
        private const int FixturePrototype = 28422;
        private const int OnfKos = 0x00000100;
        private const int OcfUndead = 0x00000004;
        private static readonly int[] FixtureStats =
            { 7, 4, 5, 17, 4, 5, 5, 14, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 0, 0, 0, 5, 0, 0, 0, 20, 1, 0 };

        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private PersistentPlayerState _pc;
        private ArcanumObjectId _npc;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M8ACoreCombatStateTests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new Owner(_session));
            Assert.That(_session.SelectSector(Sector), Is.True);
            _pc = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(1, 1), 0x28100000u);
            _session.BindPlayer(Sector, _pc, Runtime("Player", ObjectType.Pc));
            _npc = AddNpc(FixtureKey, FixturePrototype, FixtureStats, 10, 0x00001102, 0, 0);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void InactiveStateHasNoTransientCombatValues()
        {
            Assert.That(_session.Combat.Lifecycle, Is.EqualTo(CombatLifecycle.Inactive));
            Assert.That(_session.Combat.IsActive, Is.False);
            Assert.That(_session.Combat.Participants, Is.Empty);
            Assert.That(_session.Combat.CurrentParticipant.IsNull, Is.True);
            Assert.That(_session.Combat.RoundNumber, Is.Zero);
            Assert.That(_session.Combat.CurrentActionPoints, Is.Zero);
            Assert.That(_session.Combat.MaximumActionPoints, Is.Zero);
        }

        [Test]
        public void AuthenticRetailFixtureHasStableHostileIdentityAndSourceValues()
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

            Assert.That(instance.Identity.Key, Is.EqualTo(FixtureKey));
            Assert.That(instance.PrototypeNumber, Is.EqualTo(FixturePrototype));
            Assert.That(instance.MapX, Is.EqualTo(82527));
            Assert.That(instance.MapY, Is.EqualTo(45623));
            Assert.That(instance.NpcFlags.Value & OnfKos, Is.Not.Zero);
            Assert.That(instance.WillKosScriptNum, Is.Zero);
            Assert.That(prototype.Description, Is.EqualTo(28402));
            Assert.That(prototype.StatBase, Is.EqualTo(FixtureStats));
            Assert.That(prototype.HpAdjustment, Is.EqualTo(15));
        }

        [Test]
        public void StartEstablishesAuthenticOrderAndMinimumActionPoints()
        {
            CombatResult result = _session.Combat.StartCombat(_pc.Identity, _npc);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(_session.Combat.Lifecycle, Is.EqualTo(CombatLifecycle.Active));
            Assert.That(_session.Combat.Mode, Is.EqualTo(CombatMode.TurnBased));
            Assert.That(_session.Combat.RoundNumber, Is.EqualTo(1));
            Assert.That(_session.Combat.Participants.Select(value => value.Identity),
                Is.EqualTo(new[] { _npc, _pc.Identity }));
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_npc));
            Assert.That(_session.DerivedStats.GetDerivedStat(_npc, CharacterDerivedStat.Speed), Is.EqualTo(4));
            Assert.That(_session.Combat.MaximumActionPoints, Is.EqualTo(5));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(5));
        }

        [Test]
        public void ReentrantStartFailsWithoutMutatingActiveState()
        {
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _npc).Succeeded, Is.True);
            CombatParticipant[] before = _session.Combat.Participants.ToArray();

            CombatResult second = _session.Combat.StartCombat(_pc.Identity, _npc);

            Assert.That(second.Failure, Is.EqualTo(CombatFailure.AlreadyActive));
            Assert.That(_session.Combat.Participants.Select(value => value.Identity),
                Is.EqualTo(before.Select(value => value.Identity)));
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_npc));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(5));
        }

        [Test]
        public void InvalidActorTargetAndSelfStartRemainTransactional()
        {
            ArcanumObjectId missing = ArcanumObjectId.CreateGuid(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
            Assert.That(_session.Combat.StartCombat(missing, _npc).Failure,
                Is.EqualTo(CombatFailure.ActorNotFound));
            Assert.That(_session.Combat.StartCombat(_pc.Identity, missing).Failure,
                Is.EqualTo(CombatFailure.TargetNotFound));
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _pc.Identity).Failure,
                Is.EqualTo(CombatFailure.SameParticipant));
            Assert.That(_session.Combat.Lifecycle, Is.EqualTo(CombatLifecycle.Inactive));
            Assert.That(_session.Combat.Participants, Is.Empty);
        }

        [Test]
        public void RealTimeStartIsAnExplicitDeferredBoundary()
        {
            CombatResult result = _session.Combat.StartCombat(_pc.Identity, _npc, CombatMode.RealTime);
            Assert.That(result.Failure, Is.EqualTo(CombatFailure.UnsupportedMode));
            Assert.That(_session.Combat.Lifecycle, Is.EqualTo(CombatLifecycle.Inactive));
        }

        [Test]
        public void NonHostileAndScriptVetoTargetsAreRejectedWithoutState()
        {
            ArcanumObjectId neutral = AddNpc("G_11111111_1111_1111_1111_111111111111", 28001,
                Stats(8, 8, 1), 20, 0, 0, 0);
            Assert.That(_session.Combat.StartCombat(_pc.Identity, neutral).Failure,
                Is.EqualTo(CombatFailure.TargetNotHostile));

            ArcanumObjectId scripted = AddNpc("G_22222222_2222_2222_2222_222222222222", 28002,
                Stats(8, 8, 1), 21, OnfKos, 0, 9000);
            Assert.That(_session.Combat.StartCombat(_pc.Identity, scripted).Failure,
                Is.EqualTo(CombatFailure.UnresolvedHostilityScript));
            Assert.That(_session.Combat.Participants, Is.Empty);
        }

        [Test]
        public void DeadTargetIsUnavailableThroughExistingVitalityAuthority()
        {
            int current = _session.Vitality.GetCurrentHitPoints(_npc);
            _session.Vitality.ApplyHitPointDamage(_npc, current);

            CombatResult result = _session.Combat.StartCombat(_pc.Identity, _npc);

            Assert.That(result.Failure, Is.EqualTo(CombatFailure.ParticipantUnavailable));
            Assert.That(_session.Combat.Participants, Is.Empty);
            Assert.That(_session.Vitality.GetCurrentHitPoints(_npc), Is.Zero);
        }

        [Test]
        public void FatigueRejectsLivingTargetButUndeadMayActAtZeroFatigue()
        {
            _session.Vitality.ApplyFatigueDamage(_npc, _session.Vitality.GetCurrentFatigue(_npc));
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _npc).Failure,
                Is.EqualTo(CombatFailure.ParticipantUnavailable));

            ArcanumObjectId undead = AddNpc("G_33333333_3333_3333_3333_333333333333", 28003,
                Stats(7, 6, 2), 22, OnfKos, OcfUndead, 0);
            _session.Vitality.ApplyFatigueDamage(undead, _session.Vitality.GetCurrentFatigue(undead));
            Assert.That(_session.Combat.StartCombat(_pc.Identity, undead).Succeeded, Is.True);
        }

        [Test]
        public void DuplicateParticipantRegistrationIsIdempotent()
        {
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _npc).Succeeded, Is.True);
            CombatResult duplicate = _session.Combat.RegisterParticipant(_npc);
            Assert.That(duplicate.Failure, Is.EqualTo(CombatFailure.AlreadyRegistered));
            Assert.That(_session.Combat.Participants.Count, Is.EqualTo(2));
        }

        [Test]
        public void AddedNpcUsesStableSourceOrderWhilePcRemainsLast()
        {
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _npc).Succeeded, Is.True);
            ArcanumObjectId earlier = AddNpc("G_44444444_4444_4444_4444_444444444444", 28004,
                Stats(6, 7, 3), 2, OnfKos, 0, 0);

            Assert.That(_session.Combat.RegisterParticipant(earlier).Succeeded, Is.True);
            Assert.That(_session.Combat.Participants.Select(value => value.Identity),
                Is.EqualTo(new[] { earlier, _npc, _pc.Identity }));
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_npc),
                "sorting the list must not steal the current subturn");
        }

        [Test]
        public void TurnAdvanceResetsActionPointsFromEachActorsDerivedSpeed()
        {
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _npc).Succeeded, Is.True);

            Assert.That(_session.Combat.EndCurrentTurn(_npc).Succeeded, Is.True);
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_pc.Identity));
            int pcSpeed = _session.DerivedStats.GetDerivedStat(_pc.Identity, CharacterDerivedStat.Speed);
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(Math.Max(5, pcSpeed)));
            Assert.That(_session.Combat.EndCurrentTurn(_pc.Identity).Succeeded, Is.True);
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_npc));
            Assert.That(_session.Combat.RoundNumber, Is.EqualTo(2));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(5));
        }

        [Test]
        public void RemovingCurrentParticipantSelectsAndResetsNextActor()
        {
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _npc).Succeeded, Is.True);

            Assert.That(_session.Combat.RemoveParticipant(_npc).Succeeded, Is.True);

            Assert.That(_session.Combat.Participants.Select(value => value.Identity),
                Is.EqualTo(new[] { _pc.Identity }));
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_pc.Identity));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(Math.Max(5,
                _session.DerivedStats.GetDerivedStat(_pc.Identity, CharacterDerivedStat.Speed))));
        }

        [Test]
        public void ActiveHostileBlocksExitThenEndClearsOnlyTransientState()
        {
            int hp = _session.Vitality.GetCurrentHitPoints(_npc);
            int fatigue = _session.Vitality.GetCurrentFatigue(_npc);
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _npc).Succeeded, Is.True);
            Assert.That(_session.Combat.EndCombat(_pc.Identity).Failure,
                Is.EqualTo(CombatFailure.HostileParticipantActive));
            Assert.That(_session.Combat.RemoveParticipant(_npc).Succeeded, Is.True);

            Assert.That(_session.Combat.EndCombat(_pc.Identity).Succeeded, Is.True);
            Assert.That(_session.Combat.Lifecycle, Is.EqualTo(CombatLifecycle.Inactive));
            Assert.That(_session.Combat.Participants, Is.Empty);
            Assert.That(_session.Combat.CurrentActionPoints, Is.Zero);
            Assert.That(_session.Vitality.GetCurrentHitPoints(_npc), Is.EqualTo(hp));
            Assert.That(_session.Vitality.GetCurrentFatigue(_npc), Is.EqualTo(fatigue));
            Assert.That(_session.Characters.TryGet(_npc, out _), Is.True);
        }

        [Test]
        public void OrdinaryMovementBoundaryClosesOnlyDuringActiveCombat()
        {
            Assert.That(_session.Combat.CanUseOrdinaryMovement(_pc.Identity), Is.True);
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _npc).Succeeded, Is.True);
            Assert.That(_session.Combat.CanUseOrdinaryMovement(_pc.Identity), Is.False);
            Assert.That(_session.Combat.CanUseOrdinaryMovement(_npc), Is.False);
        }

        [Test]
        public void OrdinaryWorldInteractionIsBlockedDuringActiveCombat()
        {
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _npc).Succeeded, Is.True);
            var command = new WorldInteractionCommand(_pc.Identity, _npc, WorldInteractionCommandType.Use);

            WorldInteractionResult result = _session.ExecuteInteraction(command);

            Assert.That(result.Code, Is.EqualTo(WorldInteractionResultCode.Blocked));
            Assert.That(_session.Combat.IsActive, Is.True);
            Assert.That(_session.Combat.Participants.Select(value => value.Identity),
                Is.EqualTo(new[] { _npc, _pc.Identity }));
        }

        [Test]
        public void SectorUnloadClearsTransientCombatAndSourceProfiles()
        {
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _npc).Succeeded, Is.True);
            CombatStateService service = _session.Combat;

            _session.ClearSelectedSector();

            Assert.That(service.Lifecycle, Is.EqualTo(CombatLifecycle.Inactive));
            Assert.That(service.Participants, Is.Empty);
            Assert.That(service.TryGetActorSource(_npc, out _), Is.False);
            Assert.That(service.TryGetActorSource(_pc.Identity, out _), Is.False);
        }

        [Test]
        public void SectorReselectionReRegistersProductionPlayerSource()
        {
            _session.ClearSelectedSector();
            Assert.That(_session.Combat.TryGetActorSource(_pc.Identity, out _), Is.False);

            Assert.That(_session.SelectSector(Sector), Is.True);

            Assert.That(_session.Combat.TryGetActorSource(_pc.Identity, out CombatActorSource source), Is.True);
            Assert.That(source.ObjectType, Is.EqualTo(ObjectType.Pc));
            Assert.That(source.SourceSector, Is.EqualTo(Sector));
        }

        [Test]
        public void CrossMapSelectionRegistersPlayerSourceAfterProductionRelocation()
        {
            const string destination = "maps/bates mansion lev 1/67108865.sec";
            _session.SectorSelected += selected => _session.GetOrCreatePlayer(_pc.Identity,
                selected, new Vector2(36, 58), _pc.ArtId);

            Assert.That(_session.SelectSector(destination), Is.True);

            Assert.That(_session.PlayerState.Sector, Is.EqualTo(destination));
            Assert.That(_session.Combat.TryGetActorSource(_pc.Identity, out CombatActorSource source), Is.True);
            Assert.That(source.SourceSector, Is.EqualTo(destination));
        }

        [Test]
        public void InactiveProductionPlayerSourceFollowsDirectCoordinatorTraversal()
        {
            const string destination = "maps/bates mansion lev 1/67108865.sec";

            _session.GetOrCreatePlayer(_pc.Identity, destination, new Vector2(36, 58), _pc.ArtId);

            Assert.That(_session.PlayerState.Sector, Is.EqualTo(destination));
            Assert.That(_session.Combat.TryGetActorSource(_pc.Identity, out CombatActorSource source), Is.True);
            Assert.That(source.SourceSector, Is.EqualTo(destination));
        }

        [Test]
        public void SaveV1DoesNotCaptureTransientCombatState()
        {
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _npc).Succeeded, Is.True);
            string json = _session.SaveGames.SerializeData(_session.SaveGames.CaptureData());
            Assert.That(json, Does.Not.Contain("Combat"));
            Assert.That(json, Does.Not.Contain("combat"));
            Assert.That(_session.Combat.IsActive, Is.True, "capture itself must not mutate the live session");
        }

        [Test]
        public void SourceRegistrationRejectsChangedIdentityFacts()
        {
            Assert.Throws<InvalidOperationException>(() => _session.Combat.RegisterActorSource(
                new CombatActorSource(_npc, ObjectType.Npc, FixturePrototype, Sector, 11,
                    0x00001102, 0, 0)));
            Assert.That(_session.Combat.Lifecycle, Is.EqualTo(CombatLifecycle.Inactive));
        }

        private ArcanumObjectId AddNpc(string key, int prototype, int[] stats, int sourceOrder,
            int npcFlags, int critterFlags, int willKosScript)
        {
            ArcanumObjectId identity = ParseIdentity(key);
            var source = new ObjectInstance(ObjectType.Npc, prototype, Location(3 + sourceOrder, 2),
                0x28100000u, 0, 0, oid: GuidBytes(key));
            PersistentObjectState state = _session.GetOrCreate(source, Sector, source.CurrentArtId.Value,
                false, false);
            _session.Characters.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype, stats, null);
            _session.Progression.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype,
                CharacterProgressionSource.Resolve(stats, null, null, null, null, null, critterFlags));
            _session.DerivedStats.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype,
                CharacterDerivedSource.Resolve(stats, null, null, 0, null, null, null, 50,
                    npcFlags, critterFlags));
            int hpAdjustment = prototype == FixturePrototype ? 15 : 0;
            _session.Vitality.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype,
                CharacterVitalitySource.Resolve(stats, null, null, 0, null, hpAdjustment, null, 0,
                    null, 0, null, 0, null, 0));
            _session.Bind(Sector, state, Runtime(key, ObjectType.Npc));
            _session.Combat.RegisterActorSource(new CombatActorSource(identity, ObjectType.Npc, prototype,
                Sector, sourceOrder, npcFlags, critterFlags, willKosScript));
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
