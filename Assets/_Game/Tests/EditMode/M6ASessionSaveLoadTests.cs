using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Quest;
using Arcanum.Runtime.Campaign;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.World;
using Arcanum.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M6A")]
    public sealed class M6ASessionSaveLoadTests
    {
        private const string Sector = "maps/test/1.sec";
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private PersistentPlayerState _pc;
        private PersistentObjectState _npc;
        private PersistentObjectState _portal;
        private PersistentObjectState _container;
        private PersistentObjectState _food;
        private PersistentObjectState _armor;
        private PersistentObjectState _ammo;
        private string _temporaryDirectory;
        private Dictionary<int, ObjectProtoInfo> _prototypes;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M6ASessionSaveLoadTests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new FakeOwner(_session));
            Assert.That(_session.SelectSector(Sector), Is.True);
            _pc = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(7.5f, 8.25f), 0x28100000u);

            _prototypes = new Dictionary<int, ObjectProtoInfo>
            {
                [3001] = new ObjectProtoInfo(3001, ObjectType.Armor, 0x70000000u, invAid: 0),
                [7001] = new ObjectProtoInfo(7001, ObjectType.Ammo, 0x60000000u, invAid: 1, weight: 1),
                [10078] = new ObjectProtoInfo(10078, ObjectType.Food, 0x90000000u, invAid: 2, weight: 50),
                [9056] = new ObjectProtoInfo(9056, ObjectType.Gold, 0x60000003u, invAid: 3),
            };
            _prototypes[7001].AmmoQuantity = 10;
            _prototypes[9056].GoldQuantity = 1;
            _session.BindPrototypeSource(number => _prototypes.GetValueOrDefault(number));
            _session.BindInventoryFootprintSource(_ => InventoryFootprint.OneCell);

            _npc = AddAuthored(ObjectType.Npc, 17088, 2, 2, 0x28100000u);
            AddNpcCharacter(_npc);
            _portal = AddAuthored(ObjectType.Portal, 2001, 3, 3, 0x30000000u);
            _portal.Locked = false;
            _portal.PortalOpen = true;
            _portal.ArtId = 0x30018000u;
            _container = AddAuthored(ObjectType.Container, 3052, 4, 4, 0x20000000u);
            _food = AddAuthored(ObjectType.Food, 10078, 5, 5, 0x90000000u,
                itemFlags: 0, inventoryArtId: 2, unitWeight: 50);
            Assert.That(_session.TransferItem(_food.Identity, _food.Placement,
                ObjectPlacement.ContainedBy(_pc.Identity)).Succeeded, Is.True);

            _armor = _session.CreateItem(3001, ObjectPlacement.ContainedBy(_pc.Identity)).State;
            Assert.That(_session.EquipItem(_pc.Identity, _armor.Identity, WornLocation.Armor).Succeeded, Is.True);
            _ammo = _session.CreateItem(7001, ObjectPlacement.ContainedBy(_pc.Identity)).State;
            StackSplitResult split = _session.SplitStack(_ammo.Identity, 4);
            Assert.That(split.Succeeded, Is.True);
            Assert.That(_session.MergeStacks(split.CreatedState.Identity, _ammo.Identity).Succeeded, Is.True);
            Assert.That(_session.IsObjectRemoved(split.CreatedState.Identity), Is.True);

            Assert.That(_session.Progression.IncreaseSkill(_pc.Identity, CharacterSkill.Persuasion),
                Is.EqualTo(SkillIncreaseResult.Success));
            Assert.That(_session.Progression.SetTrainingLevel(_pc.Identity, CharacterSkill.Persuasion,
                SkillTrainingLevel.Apprentice), Is.EqualTo(TrainingAssignmentResult.Success));
            _session.Progression.AwardExperience(_pc.Identity, 3000);
            _session.Vitality.ApplyHitPointDamage(_pc.Identity, 4);
            _session.Vitality.ApplyFatigueDamage(_pc.Identity, 5);
            _session.DerivedStats.SetAlignment(_pc.Identity, 50);
            _session.DerivedStats.SetReaction(_npc.Identity, _pc.Identity, 53);

            _session.Campaign.SetVar(10, 123);
            _session.Campaign.SetFlag(11, 1);
            _session.Campaign.SetPcVar(12, -4);
            _session.Campaign.SetPcFlag(13, 1);
            _session.Campaign.SetStoryState(2);
            _session.Campaign.SetPcQuestState(1130, (int)QuestState.Mentioned);
            _session.Campaign.SetPcQuestState(1130, (int)QuestState.Accepted);
            _session.Campaign.SetPcQuestState(1005, (int)QuestState.Mentioned);
            _session.Campaign.SetPcQuestState(1005, (int)QuestState.Accepted);
            _session.Campaign.SetPcQuestState(1005, (int)QuestState.Completed);
            _session.Campaign.SetLocalFlag(_npc.Identity, 1, 7, 1);
            _session.Campaign.SetLocalCounter(_npc.Identity, 1, 2, 9);

            _session.AddGold(_pc.Identity, 100);
            Assert.That(_session.TryTransferGold(_pc.Identity, _npc.Identity, 99, out _), Is.True);
            _session.SetPlayerDestination(new Vector2Int(20, 20));
            _temporaryDirectory = Path.Combine(Path.GetTempPath(), "OpenArcanum-M6A-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporaryDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            if (Directory.Exists(_temporaryDirectory)) Directory.Delete(_temporaryDirectory, true);
        }

        [TestCase(1u)]
        [TestCase(uint.MaxValue)]
        public void AuthoredObjectIdRoundTripsCanonically(uint value)
            => RoundTripIdentity(ArcanumObjectId.CreateAuthored(value));

        [Test]
        public void GuidObjectIdRoundTripsCanonically()
            => RoundTripIdentity(ArcanumObjectId.CreateGuid(new Guid("25e7b7c9-1ae7-4af5-b1e4-0a62fa6bca01")));

        [Test]
        public void PositionalObjectIdRoundTripsCanonically()
            => RoundTripIdentity(ArcanumObjectId.CreatePositional(0x00017A850001B52A, 59, 1));

        [Test]
        public void DynamicObjectIdRoundTripsCanonically()
            => RoundTripIdentity(ArcanumObjectId.CreateSessionDynamic(0x0102030405060708UL));

        [TestCase("Handle_0000000000000001")]
        [TestCase("Blocked")]
        [TestCase("A_0000000a")]
        [TestCase("D_000000000000000")]
        [TestCase("G_787AD4AB_9061_2B4E_A691_F582800B2BBZ")]
        public void InvalidOrNoncanonicalObjectIdsAreRejected(string key)
            => Assert.That(ArcanumObjectId.TryParsePersistent(key, out _), Is.False);

        [Test]
        public void DeterministicJsonHasExplicitV1EnvelopeAndNoPresentationState()
        {
            string first = _session.SaveGames.SerializeCurrentSession();
            string second = _session.SaveGames.SerializeCurrentSession();
            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Does.Contain("\"format\": \"OpenArcanum.SessionSave\""));
            Assert.That(first, Does.Contain("\"version\": 1"));
            Assert.That(first, Does.Not.Contain("GameObject"));
            Assert.That(first, Does.Not.Contain("instanceId"));
            Assert.That(first, Does.Not.Contain("Transform"));
            Assert.That(first, Does.Not.Contain("D:\\OpenArcanum"));
        }

        [Test]
        public void UnknownFormatFutureVersionAndMalformedJsonFailClosed()
        {
            SessionSaveData data = _session.SaveGames.CaptureData();
            data.Format = "Other";
            Assert.That(_session.SaveGames.LoadJson(_session.SaveGames.SerializeData(data)).Failure,
                Is.EqualTo(SessionLoadFailure.UnknownFormat));
            data.Format = SessionSaveService.FormatIdentifier;
            data.Version = 2;
            Assert.That(_session.SaveGames.LoadJson(_session.SaveGames.SerializeData(data)).Failure,
                Is.EqualTo(SessionLoadFailure.UnsupportedVersion));
            Assert.That(_session.SaveGames.LoadJson("{broken").Failure,
                Is.EqualTo(SessionLoadFailure.MalformedJson));
        }

        [Test]
        public void GoldenSessionRoundTripsAllAuthoritativeDomainsAndNormalizesTransientState()
        {
            string originalPc = _pc.Identity.Key;
            Vector2 originalPosition = _pc.MapPosition;
            int speed = _session.DerivedStats.GetDerivedStat(_pc.Identity, CharacterDerivedStat.Speed);
            int capacity = _session.InventoryCapacity.GetCarryCapacity(_pc.Identity);
            // Level changes preserve current vitality by adjusting damage, so capture the complete pre-save value.
            int hitPointDamage = _session.Vitality.Get(_pc.Identity).HitPointDamage;
            int fatigueDamage = _session.Vitality.Get(_pc.Identity).FatigueDamage;
            string json = _session.SaveGames.SerializeCurrentSession();

            _session.SetMovementState(_pc.Identity, new Vector2(40, 40), 0x28100000u, false);
            _session.Campaign.SetVar(10, 999);
            _session.DerivedStats.SetAlignment(_pc.Identity, -500);
            _portal.PortalOpen = false;
            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);

            Assert.That(_session.PlayerState.Identity.Key, Is.EqualTo(originalPc));
            Assert.That(_session.PlayerState.MapPosition, Is.EqualTo(originalPosition));
            Assert.That(_session.PlayerState.Destination, Is.Null);
            Assert.That(_session.SelectedSector, Is.EqualTo(Sector));
            Assert.That(_session.States[_portal.Identity].PortalOpen, Is.True);
            Assert.That(_session.States[_food.Identity].Placement,
                Is.EqualTo(ObjectPlacement.ContainedBy(_session.PlayerState.Identity)));
            Assert.That(_session.States[_armor.Identity].Placement,
                Is.EqualTo(ObjectPlacement.EquippedBy(_session.PlayerState.Identity, WornLocation.Armor)));
            Assert.That(_session.States[_ammo.Identity].StackQuantity, Is.EqualTo(10));
            Assert.That(_session.Progression.GetTrainingLevel(_session.PlayerState.Identity,
                CharacterSkill.Persuasion), Is.EqualTo(SkillTrainingLevel.Apprentice));
            Assert.That(_session.Progression.GetExperience(_session.PlayerState.Identity), Is.EqualTo(3000));
            Assert.That(_session.Vitality.Get(_session.PlayerState.Identity).HitPointDamage,
                Is.EqualTo(hitPointDamage));
            Assert.That(_session.Vitality.Get(_session.PlayerState.Identity).FatigueDamage,
                Is.EqualTo(fatigueDamage));
            Assert.That(_session.DerivedStats.GetAlignment(_session.PlayerState.Identity), Is.EqualTo(50));
            Assert.That(_session.DerivedStats.GetReaction(_npc.Identity, _session.PlayerState.Identity), Is.EqualTo(53));
            Assert.That(_session.Campaign.GetVar(10), Is.EqualTo(123));
            Assert.That(_session.Campaign.GetPcQuestState(1005), Is.EqualTo((int)QuestState.Completed));
            Assert.That(_session.Campaign.GetLocalFlag(_npc.Identity, 1, 7), Is.EqualTo(1));
            Assert.That(_session.GetGold(_session.PlayerState.Identity), Is.EqualTo(1));
            Assert.That(_session.GetGold(_npc.Identity), Is.EqualTo(99));
            Assert.That(_session.DerivedStats.GetDerivedStat(_session.PlayerState.Identity,
                CharacterDerivedStat.Speed), Is.EqualTo(speed));
            Assert.That(_session.InventoryCapacity.GetCarryCapacity(_session.PlayerState.Identity),
                Is.EqualTo(capacity));
        }

        [Test]
        public void DynamicAllocatorContinuesAboveRestoredLiveAndTombstonedIds()
        {
            ulong expected = _session.NextDynamicIdentity;
            string json = _session.SaveGames.SerializeCurrentSession();
            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);
            ItemCreationResult next = _session.CreateItem(10078,
                ObjectPlacement.ContainedBy(_session.PlayerState.Identity));
            Assert.That(next.Succeeded, Is.True);
            Assert.That(next.State.Identity, Is.EqualTo(ArcanumObjectId.CreateSessionDynamic(expected)));
        }

        [Test]
        public void ResetThenLoadReplacesEveryAuthoritativeRootAndRebuildsTheSelectedSector()
        {
            string json = _session.SaveGames.SerializeCurrentSession();
            PersistentPlayerState previousPlayer = _session.PlayerState;
            PersistentObjectState previousFood = _food;
            CharacterStatService previousCharacters = _session.Characters;
            CharacterProgressionService previousProgression = _session.Progression;
            CharacterVitalityService previousVitality = _session.Vitality;
            CharacterDerivedStatService previousDerived = _session.DerivedStats;
            CampaignStateService previousCampaign = _session.Campaign;

            _session.ResetAuthoritativeSession();
            Assert.That(_session.PlayerState, Is.Null);
            Assert.That(_session.HasSelectedSector, Is.False);
            Assert.That(_session.States, Is.Empty);
            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);

            Assert.That(_session.SelectedSector, Is.EqualTo(Sector));
            Assert.That(_session.PlayerState, Is.Not.SameAs(previousPlayer));
            Assert.That(_session.States[_food.Identity], Is.Not.SameAs(previousFood));
            Assert.That(_session.Characters, Is.Not.SameAs(previousCharacters));
            Assert.That(_session.Progression, Is.Not.SameAs(previousProgression));
            Assert.That(_session.Vitality, Is.Not.SameAs(previousVitality));
            Assert.That(_session.DerivedStats, Is.Not.SameAs(previousDerived));
            Assert.That(_session.Campaign, Is.Not.SameAs(previousCampaign));
        }

        [Test]
        public void MalformedLoadLeavesLiveSessionUnchanged()
        {
            PersistentPlayerState before = _session.PlayerState;
            int beforeVar = _session.Campaign.GetVar(10);
            SessionLoadResult result = _session.SaveGames.LoadJson("{not-json");
            Assert.That(result.Succeeded, Is.False);
            Assert.That(_session.PlayerState, Is.SameAs(before));
            Assert.That(_session.Campaign.GetVar(10), Is.EqualTo(beforeVar));
        }

        [Test]
        public void MissingParentIsRejectedWithoutMutation()
        {
            SessionSaveData data = _session.SaveGames.CaptureData();
            ObjectSaveData food = data.Objects.Single(value => value.Identity == _food.Identity.Key);
            food.Placement.ParentIdentity = ArcanumObjectId.CreateAuthored(999).Key;
            AssertRejected(data, SessionLoadFailure.InvalidReference);
        }

        [Test]
        public void UnchangedAuthoredExternalParentMayResolveWhenItsSourceSectorLoadsLater()
        {
            SessionSaveData data = _session.SaveGames.CaptureData();
            ObjectSaveData food = data.Objects.Single(value => value.Identity == _food.Identity.Key);
            string externalParent = ArcanumObjectId.CreateGuid(
                Guid.Parse("822437c5-52e3-42e2-bbde-2835d95d7d91")).Key;
            food.AuthoredParentIdentity = externalParent;
            food.Placement.Kind = (int)ObjectPlacementKind.Contained;
            food.Placement.ParentIdentity = externalParent;

            Assert.That(_session.SaveGames.LoadJson(_session.SaveGames.SerializeData(data)).Succeeded, Is.True);
            Assert.That(_session.States[_food.Identity].ParentIdentity.Key, Is.EqualTo(externalParent));
        }

        [Test]
        public void DuplicateObjectIdIsRejectedWithoutMutation()
        {
            SessionSaveData data = _session.SaveGames.CaptureData();
            data.Objects.Add(data.Objects[0]);
            AssertRejected(data, SessionLoadFailure.DuplicateIdentity);
        }

        [Test]
        public void ContainmentCycleIsRejectedWithoutMutation()
        {
            SessionSaveData data = _session.SaveGames.CaptureData();
            ObjectSaveData container = data.Objects.Single(value => value.Identity == _container.Identity.Key);
            ObjectSaveData npc = data.Objects.Single(value => value.Identity == _npc.Identity.Key);
            container.Placement.Kind = (int)ObjectPlacementKind.Contained;
            container.Placement.ParentIdentity = npc.Identity;
            npc.Placement.Kind = (int)ObjectPlacementKind.Contained;
            npc.Placement.ParentIdentity = container.Identity;
            AssertRejected(data, SessionLoadFailure.ContainmentCycle);
        }

        [Test]
        public void InvalidEquipmentSlotIsRejectedWithoutMutation()
        {
            SessionSaveData data = _session.SaveGames.CaptureData();
            ObjectSaveData armor = data.Objects.Single(value => value.Identity == _armor.Identity.Key);
            armor.Placement.WornLocation = 999;
            AssertRejected(data, SessionLoadFailure.InvalidPlacement);
        }

        [Test]
        public void InvalidStackQuantityIsRejectedWithoutMutation()
        {
            SessionSaveData data = _session.SaveGames.CaptureData();
            data.Objects.Single(value => value.Identity == _ammo.Identity.Key).StackQuantity = 0;
            AssertRejected(data, SessionLoadFailure.InvalidStack);
        }

        [Test]
        public void InvalidQuestStateIsRejectedWithoutMutation()
        {
            SessionSaveData data = _session.SaveGames.CaptureData();
            data.Campaign.Quests.Single(value => value.Number == 1005).PcState = 99;
            AssertRejected(data, SessionLoadFailure.InvalidCampaign);
        }

        [Test]
        public void SaveWritesReadableValidatedFileAndLoadReadsIt()
        {
            string path = Path.Combine(_temporaryDirectory, "slot.json");
            Assert.That(_session.SaveGames.SaveSession(path).Succeeded, Is.True);
            string text = File.ReadAllText(path);
            Assert.That(text.Replace("\r\n", "\n"), Does.StartWith("{\n  \"format\""));
            Assert.That(_session.SaveGames.LoadSession(path).Succeeded, Is.True);
        }

        [Test]
        public void FailedSaveDoesNotReplacePreviousValidFile()
        {
            string path = Path.Combine(_temporaryDirectory, "slot.json");
            File.WriteAllText(path, "previous-valid-save");
            var emptyRoot = new GameObject("EmptySession");
            try
            {
                var empty = emptyRoot.AddComponent<WorldMapSessionCoordinator>();
                SessionSaveResult result = empty.SaveGames.SaveSession(path);
                Assert.That(result.Failure, Is.EqualTo(SessionSaveFailure.NoActiveSession));
                Assert.That(File.ReadAllText(path), Is.EqualTo("previous-valid-save"));
            }
            finally
            {
                Object.DestroyImmediate(emptyRoot);
            }
        }

        private void AssertRejected(SessionSaveData data, SessionLoadFailure expected)
        {
            PersistentPlayerState player = _session.PlayerState;
            int variable = _session.Campaign.GetVar(10);
            SessionLoadResult result = _session.SaveGames.LoadJson(_session.SaveGames.SerializeData(data));
            Assert.That(result.Failure, Is.EqualTo(expected), result.Message);
            Assert.That(_session.PlayerState, Is.SameAs(player));
            Assert.That(_session.Campaign.GetVar(10), Is.EqualTo(variable));
        }

        private PersistentObjectState AddAuthored(ObjectType type, int prototype, int id, int tile,
            uint artId, int itemFlags = 0, uint? inventoryArtId = null, int unitWeight = 0)
        {
            var source = new ObjectInstance(type, prototype, ((long)tile << 32) | (uint)tile, artId, 0, 0,
                oid: AuthoredBytes(id));
            return _session.GetOrCreate(source, Sector, artId, false, false, itemFlags,
                inventoryArtId, unitWeight: unitWeight, inventoryFootprint: InventoryFootprint.OneCell);
        }

        private void AddNpcCharacter(PersistentObjectState npc)
        {
            int[] stats = Enumerable.Repeat(8, CharacterAttributeSet.SourceStatArrayCount).ToArray();
            stats[CharacterProgressionSource.LevelSourceSlot] = 1;
            stats[CharacterDerivedStatRules.AlignmentSourceSlot] = 0;
            stats[CharacterDerivedStatRules.MagickPointsSourceSlot] = 0;
            stats[CharacterDerivedStatRules.TechPointsSourceSlot] = 0;
            stats[CharacterAttributeSet.RaceSourceSlot] = (int)CharacterRace.Human;
            stats[CharacterAttributeSet.GenderSourceSlot] = (int)CharacterGender.Male;
            _session.Characters.GetOrCreateSourceCharacter(npc.Identity, ObjectType.Npc,
                npc.PrototypeNumber, null, stats);
            _session.Progression.GetOrCreateSourceCharacter(npc.Identity, ObjectType.Npc, npc.PrototypeNumber,
                CharacterProgressionSource.Resolve(null, stats, null,
                    new int[CharacterSkillRules.BasicSkillCount], null,
                    new int[CharacterSkillRules.TechnicalSkillCount], 0));
            _session.DerivedStats.GetOrCreateSourceCharacter(npc.Identity, ObjectType.Npc, npc.PrototypeNumber,
                CharacterDerivedSource.Resolve(null, stats, null, 0, null,
                    new int[CharacterDerivedStatRules.ResistanceCount], null, 50, 0, 0));
            _session.Vitality.GetOrCreateSourceCharacter(npc.Identity, ObjectType.Npc, npc.PrototypeNumber,
                CharacterVitalitySource.Resolve(null, stats, null, 0, null, 0, null, 0,
                    null, 0, null, 0, null, 0));
        }

        private static void RoundTripIdentity(ArcanumObjectId identity)
        {
            Assert.That(ArcanumObjectId.TryParsePersistent(identity.Key, out ArcanumObjectId parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(identity));
            Assert.That(parsed.Key, Is.EqualTo(identity.Key));
        }

        private static byte[] AuthoredBytes(int value)
        {
            var bytes = new byte[ArcanumObjectId.SerializedSize];
            bytes[0] = (byte)ArcanumObjectIdType.Authored;
            Array.Copy(BitConverter.GetBytes(value), 0, bytes, 8, 4);
            return bytes;
        }

        private sealed class FakeOwner : ISectorPresentationOwner
        {
            private readonly WorldMapSessionCoordinator _session;
            public FakeOwner(WorldMapSessionCoordinator session) => _session = session;
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
