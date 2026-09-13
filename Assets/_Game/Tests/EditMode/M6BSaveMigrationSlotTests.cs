using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Quest;
using Arcanum.Formats.Text;
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
    [Category("M6B")]
    public sealed class M6BSaveMigrationSlotTests
    {
        private const string SectorA = "maps/test/1.sec";
        private const string SectorB = "maps/test/2.sec";
        private static readonly DateTime FixedUtc = new(2026, 9, 13, 20, 15, 30, DateTimeKind.Utc);
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private PersistentPlayerState _pc;
        private PersistentObjectState _container;
        private PersistentObjectState _child;
        private PersistentObjectState _portal;
        private PersistentObjectState _npcA;
        private PersistentObjectState _npcB;
        private string _directory;
        private SessionSaveSlotService _slots;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M6BSaveMigrationSlotTests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new FakeOwner(_session));
            Assert.That(_session.SelectSector(SectorA), Is.True);
            _pc = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                SectorA, new Vector2(7, 8), 0x28100000u);
            var prototypes = new Dictionary<int, ObjectProtoInfo>
            {
                [9056] = new ObjectProtoInfo(9056, ObjectType.Gold, 0x60000003u, invAid: 3),
                [10078] = new ObjectProtoInfo(10078, ObjectType.Food, 0x90000000u, invAid: 2, weight: 50),
            };
            prototypes[9056].GoldQuantity = 1;
            _session.BindPrototypeSource(number => prototypes.GetValueOrDefault(number));
            _session.BindInventoryFootprintSource(_ => InventoryFootprint.OneCell);
            _session.BindQuestSource(QuestLog.FromMes(new MesFile(new List<KeyValuePair<int, string>>
                { new(1005, "Synthetic quest 1005") })));
            _container = AddAuthored(1, ObjectType.Container, 3052, 2, null);
            _child = AddAuthored(2, ObjectType.Food, 10078, 3, 1);
            _portal = AddAuthored(3, ObjectType.Portal, 2001, 4, null);
            _npcA = AddAuthored(4, ObjectType.Npc, 17088, 5, null);
            _npcB = AddAuthored(5, ObjectType.Npc, 17101, 6, null);
            AddNpcCharacter(_npcA, 3, CharacterGender.Male, 10);
            AddNpcCharacter(_npcB, 21, CharacterGender.Female, -10);
            _directory = Path.Combine(Path.GetTempPath(), "OpenArcanum-M6B-" + Guid.NewGuid().ToString("N"));
            _slots = new SessionSaveSlotService(_session, _directory, () => FixedUtc);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        [Test]
        public void CurrentV1RunsThroughExplicitMigrationBoundary()
        {
            string json = _session.SaveGames.SerializeCurrentSession();
            SessionLoadResult result = SessionSaveMigrator.TryMigrateToCurrent(json, out SessionSaveData current);
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(current.Version, Is.EqualTo(1));
            Assert.That(current.World.Player.Identity, Is.EqualTo(_pc.Identity.Key));
        }

        [Test]
        public void TinySyntheticV1FixtureMigratesAndRestoresDeterministically()
        {
            SessionSaveData fixture = _session.SaveGames.CaptureData();
            fixture.Objects.RemoveAll(value => value.Identity != _portal.Identity.Key);
            fixture.Characters.RemoveAll(value => value.Identity != _pc.Identity.Key);
            fixture.Campaign.Attachments.Clear();
            fixture.Campaign.Reactions.Clear();
            string json = _session.SaveGames.SerializeData(fixture);
            Assert.That(SessionSaveMigrator.TryMigrateToCurrent(json, out SessionSaveData migrated).Succeeded, Is.True);
            Assert.That(_session.SaveGames.LoadJson(_session.SaveGames.SerializeData(migrated)).Succeeded, Is.True);
            Assert.That(_session.States.Keys, Is.EquivalentTo(new[] { _portal.Identity }));
        }

        [TestCase("{broken", SessionLoadFailure.MalformedJson)]
        [TestCase("{}", SessionLoadFailure.UnknownFormat)]
        [TestCase("{\"format\":\"OpenArcanum.SessionSave\"}", SessionLoadFailure.MissingRequiredField)]
        public void MalformedMigrationInputsFailWithoutMutation(string json, SessionLoadFailure expected)
        {
            PersistentPlayerState before = _session.PlayerState;
            Assert.That(_session.SaveGames.LoadJson(json).Failure, Is.EqualTo(expected));
            Assert.That(_session.PlayerState, Is.SameAs(before));
        }

        [Test]
        public void FutureVersionIsExplicitlyRejectedWithoutMutation()
        {
            SessionSaveData data = _session.SaveGames.CaptureData();
            data.Version = SessionSaveService.CurrentVersion + 1;
            PersistentPlayerState before = _session.PlayerState;
            SessionLoadResult result = _session.SaveGames.LoadJson(_session.SaveGames.SerializeData(data));
            Assert.That(result.Failure, Is.EqualTo(SessionLoadFailure.UnsupportedVersion));
            Assert.That(_session.PlayerState, Is.SameAs(before));
        }

        [Test]
        public void CreateSlotWritesAtomicWrapperAndDeterministicMetadata()
        {
            Assert.That(_slots.SaveSlot("alpha").Succeeded, Is.True);
            string json = File.ReadAllText(_slots.ResolveSlotPathForTests("alpha"));
            Assert.That(json, Does.Contain("\"slotFormat\": \"OpenArcanum.SaveSlot\""));
            Assert.That(json, Does.Contain("\"format\": \"OpenArcanum.SessionSave\""));
            Assert.That(json, Does.Not.Contain("GameObject"));
            Assert.That(_slots.TryReadSlot(json, "alpha", out SessionSaveSlotData slot).Succeeded, Is.True);
            Assert.That(slot.Metadata.SavedAtUtc, Is.EqualTo("2026-09-13T20:15:30.0000000Z"));
            Assert.That(slot.Metadata.CurrentMap, Is.EqualTo("maps/test"));
            Assert.That(slot.Metadata.SelectedSector, Is.EqualTo(SectorA));
            Assert.That(slot.Metadata.PcIdentity, Is.EqualTo(_pc.Identity.Key));
            Assert.That(slot.Metadata.PcLevel, Is.EqualTo(_session.Progression.GetLevel(_pc.Identity)));
        }

        [Test]
        public void ListSlotsIsSortedAndMissingDirectoryIsEmpty()
        {
            Assert.That(_slots.ListSlots().Slots, Is.Empty);
            Assert.That(_slots.SaveSlot("zeta").Succeeded, Is.True);
            Assert.That(_slots.SaveSlot("alpha").Succeeded, Is.True);
            SessionSaveSlotListResult listed = _slots.ListSlots();
            Assert.That(listed.Succeeded, Is.True);
            Assert.That(listed.Slots.Select(value => value.SlotId), Is.EqualTo(new[] { "alpha", "zeta" }));
            Assert.That(listed.Slots.All(value => value.IsValid), Is.True);
        }

        [Test]
        public void OverwriteSlotAtomicallyReplacesPriorSnapshot()
        {
            _session.Campaign.SetVar(10, 10);
            Assert.That(_slots.SaveSlot("alpha").Succeeded, Is.True);
            _session.Campaign.SetVar(10, 20);
            Assert.That(_slots.SaveSlot("alpha").Succeeded, Is.True);
            _session.Campaign.SetVar(10, 30);
            Assert.That(_slots.LoadSlot("alpha").Succeeded, Is.True);
            Assert.That(_session.Campaign.GetVar(10), Is.EqualTo(20));
            Assert.That(Directory.EnumerateFiles(_directory, "*.tmp").Any(), Is.False);
        }

        [Test]
        public void DeleteAndMissingSlotHaveTypedResults()
        {
            Assert.That(_slots.LoadSlot("missing").Failure, Is.EqualTo(SessionSaveSlotFailure.MissingSlot));
            Assert.That(_slots.SaveSlot("alpha").Succeeded, Is.True);
            Assert.That(_slots.DeleteSlot("alpha").Succeeded, Is.True);
            Assert.That(_slots.DeleteSlot("alpha").Failure, Is.EqualTo(SessionSaveSlotFailure.MissingSlot));
        }

        [TestCase("../escape")]
        [TestCase("..")]
        [TestCase("A")]
        [TestCase("bad name")]
        [TestCase("-leading")]
        [TestCase("a/b")]
        [TestCase("")]
        public void UnsafeOrNoncanonicalSlotIdsAreRejected(string slotId)
        {
            Assert.That(_slots.SaveSlot(slotId).Failure, Is.EqualTo(SessionSaveSlotFailure.InvalidSlotId));
            Assert.That(_slots.LoadSlot(slotId).Failure, Is.EqualTo(SessionSaveSlotFailure.InvalidSlotId));
            Assert.That(_slots.ResolveSlotPathForTests(slotId), Is.Null);
        }

        [Test]
        public void UnwritableDirectoryReturnsTypedWriteFailure()
        {
            Directory.CreateDirectory(_directory);
            string blocker = Path.Combine(_directory, "not-a-directory");
            File.WriteAllText(blocker, "block");
            var slots = new SessionSaveSlotService(_session, blocker, () => FixedUtc);
            Assert.That(slots.SaveSlot("alpha").Failure, Is.EqualTo(SessionSaveSlotFailure.WriteFailed));
        }

        [Test]
        public void StaleOwnedTemporaryFilesAreCleanedDuringEnumeration()
        {
            Directory.CreateDirectory(_directory);
            string stale = Path.Combine(_directory, "alpha.oaslot.0123456789abcdef.tmp");
            File.WriteAllText(stale, "stale");
            Assert.That(_slots.ListSlots().Succeeded, Is.True);
            Assert.That(File.Exists(stale), Is.False);
        }

        [Test]
        public void MetadataMismatchIsRejectedWithoutLiveMutation()
        {
            Assert.That(_slots.SaveSlot("alpha").Succeeded, Is.True);
            string path = _slots.ResolveSlotPathForTests("alpha");
            Assert.That(_slots.TryReadSlot(File.ReadAllText(path), "alpha", out SessionSaveSlotData slot).Succeeded,
                Is.True);
            slot.Metadata.CurrentMap = "maps/other";
            File.WriteAllText(path, _slots.SerializeSlotData(slot));
            PersistentPlayerState before = _session.PlayerState;
            Assert.That(_slots.LoadSlot("alpha").Failure, Is.EqualTo(SessionSaveSlotFailure.InvalidMetadata));
            Assert.That(_session.PlayerState, Is.SameAs(before));
        }

        [Test]
        public void SlotSwitchingAtoBtoAHasNoStateLeakage()
        {
            SetSlotState(new Vector2(3, 4), true, 101, false);
            Assert.That(_slots.SaveSlot("alpha").Succeeded, Is.True);
            SetSlotState(new Vector2(20, 21), false, 202, true);
            Assert.That(_slots.SaveSlot("beta").Succeeded, Is.True);
            AssertSlot("alpha", new Vector2(3, 4), true, 101, false);
            AssertSlot("beta", new Vector2(20, 21), false, 202, true);
            AssertSlot("alpha", new Vector2(3, 4), true, 101, false);
        }

        [Test]
        public void CorruptSlotLoadLeavesActiveSlotReferenceAndValuesUnchanged()
        {
            _session.Campaign.SetVar(10, 101);
            Assert.That(_slots.SaveSlot("alpha").Succeeded, Is.True);
            Assert.That(_slots.SaveSlot("beta").Succeeded, Is.True);
            Assert.That(_slots.LoadSlot("alpha").Succeeded, Is.True);
            PersistentPlayerState before = _session.PlayerState;
            File.WriteAllText(_slots.ResolveSlotPathForTests("beta"), "{corrupt");
            Assert.That(_slots.LoadSlot("beta").Failure, Is.EqualTo(SessionSaveSlotFailure.MalformedSlot));
            Assert.That(_session.PlayerState, Is.SameAs(before));
            Assert.That(_session.Campaign.GetVar(10), Is.EqualTo(101));
        }

        [Test]
        public void UnresolvedAuthoredParentSurvivesMigrationAndLoad()
        {
            SessionSaveData data = _session.SaveGames.CaptureData();
            data.Objects.RemoveAll(value => value.Identity == _container.Identity.Key);
            Assert.That(_session.SaveGames.LoadJson(_session.SaveGames.SerializeData(data)).Succeeded, Is.True);
            Assert.That(_session.States[_child.Identity].AuthoredParentIdentity, Is.EqualTo(_container.Identity));
            Assert.That(_session.States[_child.Identity].ParentIdentity, Is.EqualTo(_container.Identity));
        }

        [Test]
        public void ChangedContainmentWinsOverAuthoredParentAfterSlotLoad()
        {
            Assert.That(_session.TransferItem(_child.Identity, _child.Placement,
                ObjectPlacement.ContainedBy(_pc.Identity)).Succeeded, Is.True);
            Assert.That(_slots.SaveSlot("alpha").Succeeded, Is.True);
            Assert.That(_session.TransferItem(_child.Identity, _child.Placement,
                ObjectPlacement.ContainedBy(_container.Identity)).Succeeded, Is.True);
            Assert.That(_slots.LoadSlot("alpha").Succeeded, Is.True);
            PersistentObjectState restored = _session.States[_child.Identity];
            Assert.That(restored.AuthoredParentIdentity, Is.EqualTo(_container.Identity));
            Assert.That(restored.Placement, Is.EqualTo(ObjectPlacement.ContainedBy(_pc.Identity)));
        }

        [Test]
        public void ChangedContainerStateAndItsAuthoredChildRoundTripWithoutReparenting()
        {
            _container.Off = true;
            _container.Locked = true;
            _container.ArtId = 0x20018000u;
            Assert.That(_slots.SaveSlot("alpha").Succeeded, Is.True);
            _container.Off = false;
            _container.Locked = false;
            _container.ArtId = 0x20000000u;
            Assert.That(_slots.LoadSlot("alpha").Succeeded, Is.True);
            Assert.That(_session.States[_container.Identity].Off, Is.True);
            Assert.That(_session.States[_container.Identity].Locked, Is.True);
            Assert.That(_session.States[_container.Identity].ArtId, Is.EqualTo(0x20018000u));
            Assert.That(_session.States[_child.Identity].Placement,
                Is.EqualTo(ObjectPlacement.ContainedBy(_container.Identity)));
            Assert.That(_session.States[_child.Identity].AuthoredParentIdentity, Is.EqualTo(_container.Identity));
        }

        [Test]
        public void RelocatedAuthoredObjectKeepsForeignWorldPlacementAndOriginalIdentity()
        {
            ObjectPlacement foreign = ObjectPlacement.InWorld(SectorB, new Vector2(11, 12));
            Assert.That(_session.TransferItem(_child.Identity, _child.Placement, foreign).Succeeded, Is.True);
            Assert.That(_slots.SaveSlot("alpha").Succeeded, Is.True);
            Assert.That(_slots.LoadSlot("alpha").Succeeded, Is.True);
            PersistentObjectState restored = _session.States[_child.Identity];
            Assert.That(restored.Identity, Is.EqualTo(_child.Identity));
            Assert.That(restored.SourceSector, Is.EqualTo(SectorA));
            Assert.That(restored.Placement, Is.EqualTo(foreign));
            Assert.That(_session.SelectSector(SectorA), Is.True);
            Assert.That(_session.States[_child.Identity].Placement, Is.EqualTo(foreign));
        }

        [Test]
        public void DynamicChildTombstoneAndAllocatorRoundTripThroughSlot()
        {
            _session.AddGold(_pc.Identity, 2);
            PersistentObjectState gold = _session.States.Values.Single(value => value.Type == ObjectType.Gold);
            StackSplitResult split = _session.SplitStack(gold.Identity, 1);
            Assert.That(split.Succeeded, Is.True);
            ArcanumObjectId tombstone = split.CreatedState.Identity;
            Assert.That(_session.MergeStacks(tombstone, gold.Identity).Succeeded, Is.True);
            Assert.That(_session.IsObjectRemoved(tombstone), Is.True);

            ItemCreationResult created = _session.CreateItem(10078,
                ObjectPlacement.ContainedBy(_pc.Identity));
            Assert.That(created.Succeeded, Is.True);
            ArcanumObjectId dynamicChild = created.State.Identity;
            ulong expectedNext = _session.NextDynamicIdentity;
            Assert.That(_slots.SaveSlot("alpha").Succeeded, Is.True);
            Assert.That(_slots.LoadSlot("alpha").Succeeded, Is.True);

            Assert.That(_session.States[dynamicChild].Placement,
                Is.EqualTo(ObjectPlacement.ContainedBy(_pc.Identity)));
            Assert.That(_session.IsObjectRemoved(tombstone), Is.True);
            Assert.That(_session.States.ContainsKey(tombstone), Is.False);
            ItemCreationResult next = _session.CreateItem(10078,
                ObjectPlacement.ContainedBy(_pc.Identity));
            Assert.That(next.Succeeded, Is.True);
            Assert.That(next.State.Identity, Is.EqualTo(ArcanumObjectId.CreateSessionDynamic(expectedNext)));
        }

        [Test]
        public void PortalMutableStateRoundTripsWithoutAnimationInternals()
        {
            _portal.PortalOpen = true;
            _portal.Locked = true;
            _portal.Off = true;
            _portal.ArtId = 0x30018000u;
            Assert.That(_slots.SaveSlot("alpha").Succeeded, Is.True);
            _portal.PortalOpen = false;
            _portal.Locked = false;
            _portal.Off = false;
            _portal.ArtId = 0x30000000u;
            Assert.That(_slots.LoadSlot("alpha").Succeeded, Is.True);
            PersistentObjectState portal = _session.States[_portal.Identity];
            Assert.That(portal.PortalOpen, Is.True);
            Assert.That(portal.Locked, Is.True);
            Assert.That(portal.Off, Is.True);
            Assert.That(portal.ArtId, Is.EqualTo(0x30018000u));
        }

        [Test]
        public void MultipleNpcRegistriesRoundTripWithoutDuplicates()
        {
            _session.Vitality.ApplyHitPointDamage(_npcA.Identity, 2);
            _session.DerivedStats.SetAlignment(_npcB.Identity, 25);
            Assert.That(_slots.SaveSlot("alpha").Succeeded, Is.True);
            Assert.That(_slots.LoadSlot("alpha").Succeeded, Is.True);
            Assert.That(_session.Characters.States.Keys,
                Is.EquivalentTo(new[] { _pc.Identity, _npcA.Identity, _npcB.Identity }));
            Assert.That(_session.Vitality.Get(_npcA.Identity).HitPointDamage, Is.EqualTo(2));
            Assert.That(_session.DerivedStats.GetAlignment(_npcB.Identity), Is.EqualTo(25));
            Assert.That(_session.Characters.States.Count, Is.EqualTo(3));
        }

        [Test]
        public void CampaignQuestTrainingAndGoldRemainIdempotentAcrossRepeatedLoads()
        {
            _session.Campaign.SetFlag(42, 1);
            _session.Campaign.SetPcVar(43, -7);
            _session.Campaign.SetPcFlag(44, 1);
            _session.Campaign.SetLocalFlag(_npcA.Identity, 1, 2, 1);
            _session.Campaign.SetLocalCounter(_npcA.Identity, 1, 1, 9);
            CompleteQuest1005();
            QuestTimestamp timestamp = _session.Campaign.GetPcQuestTimestamp(1005);
            Assert.That(_session.Progression.IncreaseSkill(_pc.Identity, CharacterSkill.Persuasion),
                Is.EqualTo(SkillIncreaseResult.Success));
            Assert.That(_session.Progression.SetTrainingLevel(_pc.Identity, CharacterSkill.Persuasion,
                SkillTrainingLevel.Apprentice), Is.EqualTo(TrainingAssignmentResult.Success));
            _session.AddGold(_pc.Identity, 100);
            Assert.That(_session.TryTransferGold(_pc.Identity, _npcA.Identity, 99, out _), Is.True);
            Assert.That(_slots.SaveSlot("alpha").Succeeded, Is.True);
            for (int count = 0; count < 2; count++)
            {
                Assert.That(_slots.LoadSlot("alpha").Succeeded, Is.True);
                Assert.That(_session.Campaign.GetPcQuestState(1005), Is.EqualTo((int)QuestState.Completed));
                Assert.That(_session.Campaign.GetPcQuestTimestamp(1005), Is.EqualTo(timestamp));
                Assert.That(_session.Campaign.GetFlag(42), Is.EqualTo(1));
                Assert.That(_session.Campaign.GetPcVar(43), Is.EqualTo(-7));
                Assert.That(_session.Campaign.GetPcFlag(44), Is.EqualTo(1));
                Assert.That(_session.Campaign.GetLocalFlag(_npcA.Identity, 1, 2), Is.EqualTo(1));
                Assert.That(_session.Campaign.GetLocalCounter(_npcA.Identity, 1, 1), Is.EqualTo(9));
                Assert.That(_session.Journal.TryProject(1005, false, out QuestJournalEntry journal), Is.True);
                Assert.That(journal.State, Is.EqualTo(QuestState.Completed));
                Assert.That(_session.Progression.GetTrainingLevel(_pc.Identity, CharacterSkill.Persuasion),
                    Is.EqualTo(SkillTrainingLevel.Apprentice));
                Assert.That(_session.Progression.SetTrainingLevel(_pc.Identity, CharacterSkill.Persuasion,
                    SkillTrainingLevel.Apprentice), Is.EqualTo(TrainingAssignmentResult.Unchanged));
                Assert.That(_session.GetGold(_pc.Identity), Is.EqualTo(1));
                Assert.That(_session.GetGold(_npcA.Identity), Is.EqualTo(99));
            }
        }

        [Test]
        public void DuplicateIdentityInsideSlotIsTransactional()
        {
            Assert.That(_slots.SaveSlot("alpha").Succeeded, Is.True);
            string path = _slots.ResolveSlotPathForTests("alpha");
            Assert.That(_slots.TryReadSlot(File.ReadAllText(path), "alpha", out SessionSaveSlotData slot).Succeeded,
                Is.True);
            slot.Session.Objects.Add(slot.Session.Objects[0]);
            File.WriteAllText(path, _slots.SerializeSlotData(slot));
            PersistentPlayerState before = _session.PlayerState;
            int stateCount = _session.States.Count;
            SessionSaveSlotResult result = _slots.LoadSlot("alpha");
            Assert.That(result.Failure, Is.EqualTo(SessionSaveSlotFailure.LoadFailed));
            Assert.That(result.SessionFailure, Is.EqualTo(SessionLoadFailure.DuplicateIdentity));
            Assert.That(_session.PlayerState, Is.SameAs(before));
            Assert.That(_session.States.Count, Is.EqualTo(stateCount));
        }

        [Test]
        public void InvalidSessionReferenceInsideSlotIsTransactional()
        {
            Assert.That(_slots.SaveSlot("alpha").Succeeded, Is.True);
            string path = _slots.ResolveSlotPathForTests("alpha");
            Assert.That(_slots.TryReadSlot(File.ReadAllText(path), "alpha", out SessionSaveSlotData slot).Succeeded,
                Is.True);
            slot.Session.Objects.Single(value => value.Identity == _child.Identity.Key).Placement.ParentIdentity =
                ArcanumObjectId.CreateAuthored(999).Key;
            File.WriteAllText(path, _slots.SerializeSlotData(slot));
            PersistentPlayerState before = _session.PlayerState;
            SessionSaveSlotResult result = _slots.LoadSlot("alpha");
            Assert.That(result.Failure, Is.EqualTo(SessionSaveSlotFailure.LoadFailed));
            Assert.That(result.SessionFailure, Is.EqualTo(SessionLoadFailure.InvalidReference));
            Assert.That(_session.PlayerState, Is.SameAs(before));
        }

        private void SetSlotState(Vector2 tile, bool portalOpen, int variable, bool childWithPc)
        {
            _session.PlayerState.SetLocalPosition(SectorA, tile);
            _session.States[_portal.Identity].PortalOpen = portalOpen;
            _session.Campaign.SetVar(10, variable);
            PersistentObjectState child = _session.States[_child.Identity];
            ObjectPlacement destination = childWithPc
                ? ObjectPlacement.ContainedBy(_pc.Identity)
                : ObjectPlacement.ContainedBy(_container.Identity);
            if (child.Placement != destination)
                Assert.That(_session.TransferItem(child.Identity, child.Placement, destination).Succeeded, Is.True);
        }

        private void AssertSlot(string slotId, Vector2 tile, bool portalOpen, int variable, bool childWithPc)
        {
            Assert.That(_slots.LoadSlot(slotId).Succeeded, Is.True);
            Assert.That(_session.PlayerState.TilePosition, Is.EqualTo(tile));
            Assert.That(_session.States[_portal.Identity].PortalOpen, Is.EqualTo(portalOpen));
            Assert.That(_session.Campaign.GetVar(10), Is.EqualTo(variable));
            ArcanumObjectId parent = childWithPc ? _pc.Identity : _container.Identity;
            Assert.That(_session.States[_child.Identity].ParentIdentity, Is.EqualTo(parent));
        }

        private void CompleteQuest1005()
        {
            _session.Campaign.SetPcQuestState(1005, (int)QuestState.Mentioned);
            _session.Campaign.SetPcQuestState(1005, (int)QuestState.Accepted);
            _session.Campaign.SetPcQuestState(1005, (int)QuestState.Completed);
        }

        private PersistentObjectState AddAuthored(int id, ObjectType type, int prototype, int tile, int? parent)
        {
            var source = new ObjectInstance(type, prototype, ((long)tile << 32) | (uint)tile, 0x40000000u,
                0, 0, oid: AuthoredBytes(id), parentOid: parent.HasValue ? AuthoredBytes(parent.Value) : null);
            return _session.GetOrCreate(source, SectorA, source.CurrentArtId.GetValueOrDefault(), false, false,
                inventoryFootprint: InventoryFootprint.OneCell);
        }

        private void AddNpcCharacter(PersistentObjectState npc, int level, CharacterGender gender, int alignment)
        {
            int[] stats = Enumerable.Repeat(8, CharacterAttributeSet.SourceStatArrayCount).ToArray();
            stats[CharacterProgressionSource.LevelSourceSlot] = level;
            stats[CharacterDerivedStatRules.AlignmentSourceSlot] = alignment;
            stats[CharacterDerivedStatRules.MagickPointsSourceSlot] = 0;
            stats[CharacterDerivedStatRules.TechPointsSourceSlot] = 0;
            stats[CharacterAttributeSet.RaceSourceSlot] = (int)CharacterRace.Human;
            stats[CharacterAttributeSet.GenderSourceSlot] = (int)gender;
            _session.Characters.GetOrCreateSourceCharacter(npc.Identity, ObjectType.Npc, npc.PrototypeNumber, null, stats);
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
            public string ConfiguredSector => SectorA;
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
