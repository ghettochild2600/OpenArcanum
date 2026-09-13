using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Arcanum.Formats.Database;
using Arcanum.Formats.Dialog;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Quest;
using Arcanum.Formats.Script;
using Arcanum.Formats.Text;
using Arcanum.Formats.Tiles;
using Arcanum.Formats.World;
using Arcanum.Runtime;
using Arcanum.Runtime.Campaign;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Dialogue;
using Arcanum.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M5B")]
    public sealed class M5BQuestJournalTests
    {
        private const string Sector = "maps/arcanum1-024-fixed/96636765255.sec";
        private const int DialogNumber = 1009;
        private const int QuestNumber = 1005;
        private const int DaggerNameIndex = 2002;
        private const int DaggerPrototype = 6071;
        private static readonly ArcanumObjectId Mayor = SourceGuid(
            "787ad4ab-9061-2b4e-a691-f582800b2bb3");
        private static readonly ArcanumObjectId Dagger = SourceGuid(
            "52e2ac87-1a3b-6842-8c2e-5247c9571d11");

        private static DatVirtualFileSystem _vfs;
        private static ScriptDatabase _scripts;
        private static DialogScript _authenticDialogue;
        private static QuestLog _quests;
        private static ProtoLibrary _prototypes;
        private static ObjectInstance _mayorSource;
        private static ObjectInstance _daggerSource;
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private WorldObjectSectorLoader _loader;
        private PersistentPlayerState _pc;
        private PersistentObjectState _mayor;
        private WorldObject _mayorRuntime;
        private PersistentObjectState _dagger;

        [OneTimeSetUp]
        public void LoadAuthenticResources()
        {
            string module = GameDataLocator.Find("modules/Arcanum.dat");
            if (string.IsNullOrEmpty(module)) Assert.Ignore("The local clean Arcanum module archive is unavailable.");
            _vfs = new DatVirtualFileSystem();
            _vfs.MountFile(module);
            foreach (string archive in new[] { "arcanum1.dat", "arcanum2.dat", "arcanum3.dat", "arcanum4.dat" })
            {
                string path = GameDataLocator.Find(archive);
                if (!string.IsNullOrEmpty(path)) _vfs.MountFile(path);
            }
            _scripts = ScriptDatabase.Load(_vfs);
            _authenticDialogue = DialogLocator.Load(_vfs, DialogNumber);
            _quests = QuestLog.FromMes(ReadMes("mes/gamequestlog.mes"), ReadMes("rules/xp_quest.mes"),
                ReadMes("rules/gamequest.mes"), ReadMes("mes/gamequestlogdumb.mes"));
            _prototypes = new ProtoLibrary(GameDataLocator.FindDirectory("data/proto"));
            byte[] mayorBytes = _vfs.ReadAllBytes(
                "maps/arcanum1-024-fixed/g_abd47a78_6190_4e2b_a691_f582800b2bb3.mob");
            int mayorOffset = 0;
            _mayorSource = ObjectInstanceReader.Read(mayorBytes, ref mayorOffset);
            byte[] daggerBytes = _vfs.ReadAllBytes(
                "maps/arcanum1-024-fixed/g_87ace252_3b1a_4268_8c2e_5247c9571d11.mob");
            int daggerOffset = 0;
            _daggerSource = ObjectInstanceReader.Read(daggerBytes, ref daggerOffset);
            Assert.That(_scripts.Get(DialogNumber), Is.Not.Null);
            Assert.That(_authenticDialogue, Is.Not.Null);
            Assert.That(_prototypes.Get(DaggerPrototype)?.Type, Is.EqualTo(ObjectType.Weapon));
            Assert.That(_prototypes.Get(9056)?.Type, Is.EqualTo(ObjectType.Gold));
            Assert.That(_mayorSource.Identity, Is.EqualTo(Mayor));
            Assert.That(_mayorSource.PrototypeNumber, Is.EqualTo(17088));
            Assert.That(_daggerSource.Identity, Is.EqualTo(Dagger));
            Assert.That(_daggerSource.PrototypeNumber, Is.EqualTo(DaggerPrototype));
            Assert.That(_daggerSource.NameIndex, Is.EqualTo(DaggerNameIndex));
        }

        [OneTimeTearDown] public void ReleaseResources() => _vfs?.Dispose();

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M5BQuestJournalTests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _root.AddComponent<ProductionDialoguePresenter>();
            _root.AddComponent<ProductionJournalPresenter>();
            _loader = _root.AddComponent<WorldObjectSectorLoader>();
            _root.AddComponent<PlayerNavigationController>();
            _root.AddComponent<PlayerInteractionController>();
            _session.BeginSector(Sector);
            SetProperty(_session, nameof(WorldMapSessionCoordinator.SelectedSector), Sector);
            SetProperty(_loader, nameof(WorldObjectSectorLoader.NavigationMap), Map());

            _pc = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(1, 1), 0x28100000u);
            _session.BindPlayer(Sector, _pc, Runtime("Player", ObjectType.Pc));

            ObjectProtoInfo mayorPrototype = _prototypes.Get(17088);
            _mayor = _session.GetOrCreate(_mayorSource, Sector,
                _mayorSource.CurrentArtId ?? mayorPrototype.CurrentArtId, false, false,
                _mayorSource.ItemFlags ?? mayorPrototype.ItemFlags ?? 0,
                _mayorSource.InvAid ?? mayorPrototype.InvAid,
                _mayorSource.WeaponFlags ?? mayorPrototype.Weapon?.Flags ?? 0,
                _mayorSource.GenericFlags ?? mayorPrototype.GenericFlags ?? 0,
                unitWeight: _mayorSource.Weight ?? mayorPrototype.Weight,
                inventoryFootprint: InventoryFootprint.OneCell,
                inventoryLocation: _mayorSource.InvLocation,
                nameIndex: _mayorSource.NameIndex ?? mayorPrototype.NameIndex);
            CreateMayorCharacter(mayorPrototype);
            _mayorRuntime = Runtime("Black Root Mayor", ObjectType.Npc);
            _session.Bind(Sector, _mayor, _mayorRuntime);
            _session.BindQuestSource(_quests);
            _session.BindDialogueSource(_scripts.Get,
                number => number == DialogNumber ? _authenticDialogue : null);
            var generated = new GeneratedDialogText(ReadMes("mes/gd_pc2m.mes"), new System.Random(1005));
            _session.BindGeneratedDialogueText((_, token) => generated.For(token));
            _session.BindPrototypeSource(_prototypes.Get);
            _session.BindInventoryFootprintSource(_ => InventoryFootprint.OneCell);

            ObjectProtoInfo daggerPrototype = _prototypes.Get(DaggerPrototype);
            _dagger = _session.GetOrCreate(_daggerSource, Dagger, Sector,
                _daggerSource.CurrentArtId ?? daggerPrototype.CurrentArtId, false, false,
                _daggerSource.ItemFlags ?? daggerPrototype.ItemFlags ?? 0,
                _daggerSource.InvAid ?? daggerPrototype.InvAid,
                _daggerSource.WeaponFlags ?? daggerPrototype.Weapon?.Flags ?? 0,
                _daggerSource.GenericFlags ?? daggerPrototype.GenericFlags ?? 0,
                unitWeight: _daggerSource.Weight ?? daggerPrototype.Weight,
                inventoryFootprint: InventoryFootprint.OneCell,
                inventoryLocation: 0,
                nameIndex: _daggerSource.NameIndex ?? daggerPrototype.NameIndex);
            _dagger.Placement = ObjectPlacement.ContainedBy(_pc.Identity);
            _dagger.InventoryLocation = 0;
            _session.Campaign.SetPcQuestState(QuestNumber, (int)QuestState.Mentioned);
            StartAndChoose(3);
            Assert.That(_session.Campaign.GetPcQuestState(QuestNumber), Is.EqualTo((int)QuestState.Accepted));
            _session.Dialogue.Cancel("authentic accepted-state fixture");
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void AuthenticQuestMetadataAndStableNpcIdentityAreBound()
        {
            Assert.That(_mayor.Identity, Is.EqualTo(Mayor));
            Assert.That(_mayor.PrototypeNumber, Is.EqualTo(17088));
            Assert.That(_mayor.SourceSector, Is.EqualTo(Sector));
            Assert.That(_mayor.DialogNum, Is.EqualTo(DialogNumber));
            Assert.That(_dagger.Identity, Is.EqualTo(Dagger));
            Assert.That(_dagger.PrototypeNumber, Is.EqualTo(DaggerPrototype));
            Assert.That(_dagger.NameIndex, Is.EqualTo(DaggerNameIndex));
            Assert.That(_quests.QuestXp(QuestNumber), Is.EqualTo(800));
            Assert.That(_quests.AlignmentAdjustment(QuestNumber), Is.EqualTo(50));
            Assert.That(_quests.Description(QuestNumber), Is.Not.Empty);
        }

        [Test]
        public void AuthenticAcceptanceRaisesMayorReactionToSourceFloor()
        {
            Assert.That(_session.Campaign.GetPcQuestState(QuestNumber), Is.EqualTo((int)QuestState.Accepted));
            Assert.That(_session.DerivedStats.GetReaction(Mayor, _pc.Identity), Is.GreaterThanOrEqualTo(41));
        }

        [Test]
        public void QuestTransitionStoresSourceBotchBitAndExposesBotchedState()
        {
            _session.Campaign.SetPcQuestState(QuestNumber, (int)QuestState.Botched);
            Assert.That(_session.Campaign.GetPcQuestState(QuestNumber), Is.EqualTo((int)QuestState.Botched));
            Assert.That(_session.Campaign.GetRawPcQuestState(QuestNumber),
                Is.EqualTo((int)QuestState.Accepted | QuestLog.BotchedModifier));
        }

        [Test]
        public void QuestTimestampChangesOnlyForAcceptedMutations()
        {
            QuestTimestamp accepted = _session.Campaign.GetPcQuestTimestamp(QuestNumber);
            Assert.That(accepted, Is.Not.EqualTo(default(QuestTimestamp)));
            Assert.That(_session.Campaign.TryAdvancePcQuest(QuestNumber, (int)QuestState.Accepted,
                out _, out _), Is.True);
            Assert.That(_session.Campaign.GetPcQuestTimestamp(QuestNumber), Is.EqualTo(accepted));
            _session.Campaign.SetPcQuestState(QuestNumber, (int)QuestState.Achieved);
            Assert.That(_session.Campaign.GetPcQuestTimestamp(QuestNumber).CompareTo(accepted), Is.GreaterThan(0));
        }

        [Test]
        public void TerminalQuestRejectsRegressionWithoutChangingTimestamp()
        {
            _session.Campaign.SetPcQuestState(QuestNumber, (int)QuestState.Completed);
            QuestTimestamp completed = _session.Campaign.GetPcQuestTimestamp(QuestNumber);
            Assert.That(_session.Campaign.TryAdvancePcQuest(QuestNumber, (int)QuestState.Accepted,
                out _, out CampaignStateFailure failure), Is.False);
            Assert.That(failure, Is.EqualTo(CampaignStateFailure.QuestAlreadyTerminal));
            Assert.That(_session.Campaign.GetPcQuestTimestamp(QuestNumber), Is.EqualTo(completed));
        }

        [Test]
        public void JournalProjectsSourceDescriptionStateLabelAndTimestampReadOnly()
        {
            Assert.That(_session.Journal.TryProject(QuestNumber, false, out QuestJournalEntry entry), Is.True);
            Assert.That(entry.QuestId, Is.EqualTo(QuestNumber));
            Assert.That(entry.State, Is.EqualTo(QuestState.Accepted));
            Assert.That(entry.StateLabel, Is.EqualTo("Accepted"));
            Assert.That(entry.Description, Is.EqualTo(_quests.Description(QuestNumber)));
            Assert.That(entry.Timestamp, Is.EqualTo(_session.Campaign.GetPcQuestTimestamp(QuestNumber)));
            Assert.That(_session.Campaign.GetPcQuestState(QuestNumber), Is.EqualTo((int)QuestState.Accepted));
        }

        [Test]
        public void JournalProjectsCompletedAndBotchedTerminalStates()
        {
            _session.Campaign.SetPcQuestState(QuestNumber, (int)QuestState.Completed);
            Assert.That(_session.Journal.TryProject(QuestNumber, false, out QuestJournalEntry completed), Is.True);
            Assert.That(completed.State, Is.EqualTo(QuestState.Completed));
            Assert.That(completed.StateLabel, Is.EqualTo("Completed"));

            const int secondQuest = 1006;
            _session.Campaign.SetPcQuestState(secondQuest, (int)QuestState.Mentioned);
            _session.Campaign.SetPcQuestState(secondQuest, (int)QuestState.Botched);
            Assert.That(_session.Journal.TryProject(secondQuest, false, out QuestJournalEntry botched), Is.True);
            Assert.That(botched.State, Is.EqualTo(QuestState.Botched));
            Assert.That(botched.StateLabel, Is.EqualTo("Botched"));
        }

        [Test]
        public void JournalSortsByEarliestCurrentTimestamp()
        {
            const int secondQuest = 1006;
            _session.Campaign.SetPcQuestState(secondQuest, (int)QuestState.Mentioned);
            int[] relevant = _session.Journal.ProjectAll(false)
                .Where(entry => entry.QuestId == QuestNumber || entry.QuestId == secondQuest)
                .Select(entry => entry.QuestId).ToArray();
            Assert.That(relevant, Is.EqualTo(new[] { QuestNumber, secondQuest }));
        }

        [Test]
        public void AuthenticAcceptedEntryExposesOnlyTheDaggerCompletionBranch()
        {
            Start();
            Assert.That(_session.Dialogue.CurrentLine, Is.EqualTo(1));
            Assert.That(_session.Dialogue.AvailableResponses.Select(line => line.Num), Does.Contain(4));
            Assert.That(_session.Dialogue.AvailableResponses.Select(line => line.Num), Has.None.EqualTo(5));
        }

        [Test]
        public void AuthenticCompletionIsOneAtomicQuestInventoryProgressionTransaction()
        {
            int reaction = _session.DerivedStats.GetReaction(Mayor, _pc.Identity);
            StartAndChoose(4);
            Assert.That(_session.Dialogue.CurrentLine, Is.EqualTo(100));
            Choose(102);
            Assert.That(_session.Dialogue.CurrentLine, Is.EqualTo(430));
            Assert.That(_session.Campaign.GetPcQuestState(QuestNumber), Is.EqualTo((int)QuestState.Completed));
            Assert.That(_session.Progression.GetExperience(_pc.Identity), Is.EqualTo(800));
            Assert.That(_session.DerivedStats.GetAlignment(_pc.Identity), Is.EqualTo(50));
            Assert.That(_session.DerivedStats.GetReaction(Mayor, _pc.Identity), Is.EqualTo(reaction + 10));
            Assert.That(_dagger.Placement.ParentIdentity, Is.EqualTo(Mayor));
            Assert.That(_session.Journal.TryProject(QuestNumber, false, out QuestJournalEntry entry), Is.True);
            Assert.That(entry.State, Is.EqualTo(QuestState.Completed));
        }

        [Test]
        public void MissingDaggerFailsBeforeAnyCompletionSideEffect()
        {
            StartAndChoose(4);
            Assert.That(_session.TransferItem(_dagger.Identity, _dagger.Placement,
                ObjectPlacement.InWorld(Sector, new Vector2(5, 5))).Succeeded, Is.True);
            int reaction = _session.DerivedStats.GetReaction(Mayor, _pc.Identity);
            QuestTimestamp timestamp = _session.Campaign.GetPcQuestTimestamp(QuestNumber);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "OpenArcanum dialogue compatibility:.*UnsupportedEffect.*dialogue=1009"));
            Assert.That(_session.Dialogue.SelectResponse(ResponseIndex(102)),
                Is.EqualTo(DialogueChoiceStatus.UnsupportedEffect));
            Assert.That(_session.Campaign.GetPcQuestState(QuestNumber), Is.EqualTo((int)QuestState.Accepted));
            Assert.That(_session.Campaign.GetPcQuestTimestamp(QuestNumber), Is.EqualTo(timestamp));
            Assert.That(_session.Progression.GetExperience(_pc.Identity), Is.Zero);
            Assert.That(_session.DerivedStats.GetAlignment(_pc.Identity), Is.Zero);
            Assert.That(_session.DerivedStats.GetReaction(Mayor, _pc.Identity), Is.EqualTo(reaction));
        }

        [Test]
        public void OptionalAuthenticRewardUsesSourceGoldStack()
        {
            CompleteQuest();
            Choose(431);
            Assert.That(_session.Dialogue.CurrentLine, Is.EqualTo(440));
            Assert.That(_session.GetGold(_pc.Identity), Is.EqualTo(100));
            Assert.That(_session.TryFindContainedItem(_pc.Identity, 9056, out PersistentObjectState gold), Is.True);
            Assert.That(gold.Type, Is.EqualTo(ObjectType.Gold));
            Assert.That(gold.StackQuantity, Is.EqualTo(100));
        }

        [Test]
        public void TerminalReengagementCannotRepeatQuestSideEffects()
        {
            CompleteQuest();
            Choose(431);
            int xp = _session.Progression.GetExperience(_pc.Identity);
            int gold = _session.GetGold(_pc.Identity);
            int alignment = _session.DerivedStats.GetAlignment(_pc.Identity);
            int reaction = _session.DerivedStats.GetReaction(Mayor, _pc.Identity);
            QuestTimestamp timestamp = _session.Campaign.GetPcQuestTimestamp(QuestNumber);
            Assert.That(_session.Campaign.GetPcQuestState(QuestNumber), Is.EqualTo((int)QuestState.Completed));
            Assert.That(reaction, Is.GreaterThanOrEqualTo(41));
            _session.Dialogue.Cancel("re-engage terminal test");
            Start();
            Assert.That(_session.Dialogue.CurrentLine, Is.EqualTo(1));
            Assert.That(_session.Dialogue.AvailableResponses.Select(line => line.Num), Does.Contain(5));
            Assert.That(_session.Dialogue.AvailableResponses.Select(line => line.Num), Has.None.EqualTo(4));
            Assert.That(_session.Progression.GetExperience(_pc.Identity), Is.EqualTo(xp));
            Assert.That(_session.GetGold(_pc.Identity), Is.EqualTo(gold));
            Assert.That(_session.DerivedStats.GetAlignment(_pc.Identity), Is.EqualTo(alignment));
            Assert.That(_session.DerivedStats.GetReaction(Mayor, _pc.Identity), Is.EqualTo(reaction));
            Assert.That(_session.Campaign.GetPcQuestTimestamp(QuestNumber), Is.EqualTo(timestamp));
        }

        [Test]
        public void CompletedTrainingTokenIsVisibleButItsOutOfScopeHandlerFailsClosed()
        {
            CompleteQuest();
            Choose(431);
            _session.Dialogue.Cancel("training-token guard");
            Start();
            int response = ResponseIndex(5);
            int xp = _session.Progression.GetExperience(_pc.Identity);
            int gold = _session.GetGold(_pc.Identity);
            int alignment = _session.DerivedStats.GetAlignment(_pc.Identity);
            int reaction = _session.DerivedStats.GetReaction(Mayor, _pc.Identity);
            QuestTimestamp timestamp = _session.Campaign.GetPcQuestTimestamp(QuestNumber);
            Assert.That(_session.Dialogue.AvailableResponses[response].TokenCode, Is.EqualTo('t'));
            Assert.That(_session.Dialogue.AvailableResponses[response].Text, Is.Not.Empty);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "OpenArcanum dialogue compatibility:.*UnsupportedEffect.*dialogue=1009.*line=5.*token 't'"));
            Assert.That(_session.Dialogue.SelectResponse(response), Is.EqualTo(DialogueChoiceStatus.UnsupportedEffect));
            Assert.That(_session.Dialogue.Phase, Is.EqualTo(DialogueSessionPhase.AwaitingPlayerChoice));
            Assert.That(_session.Campaign.GetPcQuestState(QuestNumber), Is.EqualTo((int)QuestState.Completed));
            Assert.That(_session.Campaign.GetPcQuestTimestamp(QuestNumber), Is.EqualTo(timestamp));
            Assert.That(_session.Progression.GetExperience(_pc.Identity), Is.EqualTo(xp));
            Assert.That(_session.GetGold(_pc.Identity), Is.EqualTo(gold));
            Assert.That(_session.DerivedStats.GetAlignment(_pc.Identity), Is.EqualTo(alignment));
            Assert.That(_session.DerivedStats.GetReaction(Mayor, _pc.Identity), Is.EqualTo(reaction));
        }

        [Test]
        public void CancellationAndTargetLossLeaveQuestAndInventoryUntouched()
        {
            Start();
            Assert.That(_session.UnbindPresentation(Sector, Mayor), Is.True);
            _session.Dialogue.ValidateActiveTarget();
            Assert.That(_session.Dialogue.Phase, Is.EqualTo(DialogueSessionPhase.Cancelled));
            Assert.That(_session.Campaign.GetPcQuestState(QuestNumber), Is.EqualTo((int)QuestState.Accepted));
            Assert.That(_dagger.Placement.ParentIdentity, Is.EqualTo(_pc.Identity));
            Assert.That(_session.Progression.GetExperience(_pc.Identity), Is.Zero);
        }

        [Test]
        public void JournalAndQuestStateSurviveVisualRebuildWithoutDuplicatePresenter()
        {
            CompleteQuest();
            QuestTimestamp timestamp = _session.Campaign.GetPcQuestTimestamp(QuestNumber);
            _loader.RebuildVisuals();
            Assert.That(_session.Campaign.GetPcQuestState(QuestNumber), Is.EqualTo((int)QuestState.Completed));
            Assert.That(_session.Campaign.GetPcQuestTimestamp(QuestNumber), Is.EqualTo(timestamp));
            Assert.That(_session.Journal.TryProject(QuestNumber, false, out QuestJournalEntry entry), Is.True);
            Assert.That(entry.State, Is.EqualTo(QuestState.Completed));
            Assert.That(_root.GetComponents<ProductionJournalPresenter>().Length, Is.EqualTo(1));
        }

        [Test]
        public void QuestJournalAndTransferredDaggerSurviveNpcUnloadReload()
        {
            CompleteQuest();
            _session.Dialogue.Cancel("reload test");
            Assert.That(_session.UnbindPresentation(Sector, Mayor), Is.True);
            Object.DestroyImmediate(_mayorRuntime.gameObject);
            _mayorRuntime = Runtime("Black Root Mayor Reloaded", ObjectType.Npc);
            _session.Bind(Sector, _mayor, _mayorRuntime);
            Assert.That(_session.States.Count(state => state.Key == Mayor), Is.EqualTo(1));
            Assert.That(_session.Campaign.GetPcQuestState(QuestNumber), Is.EqualTo((int)QuestState.Completed));
            Assert.That(_dagger.Placement.ParentIdentity, Is.EqualTo(Mayor));
            Assert.That(_session.Journal.TryProject(QuestNumber, false, out QuestJournalEntry entry), Is.True);
            Assert.That(entry.State, Is.EqualTo(QuestState.Completed));
        }

        [Test]
        public void CancelledSessionRetainsSourceIdentityAndCanRestart()
        {
            Assert.That(_session.Dialogue.Phase, Is.EqualTo(DialogueSessionPhase.Cancelled));
            Assert.That(_session.Dialogue.DialogueNumber, Is.EqualTo(DialogNumber));
            Start();
            Assert.That(_session.Dialogue.DialogueNumber, Is.EqualTo(DialogNumber));
            Assert.That(_session.Dialogue.AvailableResponses.Select(line => line.Num), Does.Contain(4));
        }

        private void CompleteQuest()
        {
            StartAndChoose(4);
            Choose(102);
        }

        private void StartAndChoose(int line)
        {
            Start();
            Choose(line);
        }

        private void Start()
            => Assert.That(_session.Dialogue.Start(_pc.Identity, Mayor), Is.EqualTo(DialogueStartStatus.Started));

        private void Choose(int line)
            => Assert.That(_session.Dialogue.SelectResponse(ResponseIndex(line)), Is.EqualTo(DialogueChoiceStatus.Advanced));

        private int ResponseIndex(int line)
        {
            for (int index = 0; index < _session.Dialogue.AvailableResponses.Count; index++)
                if (_session.Dialogue.AvailableResponses[index].Num == line) return index;
            Assert.Fail($"Response line {line} was unavailable at node {_session.Dialogue.CurrentLine}.");
            return -1;
        }

        private void CreateMayorCharacter(ObjectProtoInfo prototype)
        {
            _session.Characters.GetOrCreateSourceCharacter(Mayor, ObjectType.Npc, 17088,
                _mayorSource.StatBase, prototype.StatBase);
            CharacterProgressionSource progression = CharacterProgressionSource.Resolve(
                _mayorSource.StatBase, prototype.StatBase,
                _mayorSource.BasicSkills, prototype.BasicSkills,
                _mayorSource.TechSkills, prototype.TechSkills,
                _mayorSource.CritterFlags ?? prototype.CritterFlags ?? 0);
            _session.Progression.GetOrCreateSourceCharacter(Mayor, ObjectType.Npc, 17088,
                progression);
            CharacterDerivedSource derived = CharacterDerivedSource.Resolve(
                _mayorSource.StatBase, prototype.StatBase,
                _mayorSource.BaseArmorClass, prototype.BaseArmorClass,
                _mayorSource.Resistances, prototype.Resistances,
                _mayorSource.ReactionBase, prototype.ReactionBase,
                _mayorSource.NpcFlags ?? prototype.NpcFlags ?? 0,
                _mayorSource.CritterFlags ?? prototype.CritterFlags ?? 0);
            _session.DerivedStats.GetOrCreateSourceCharacter(Mayor, ObjectType.Npc, 17088,
                derived);
        }

        private WorldObject Runtime(string name, ObjectType type)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform);
            var runtime = go.AddComponent<WorldObject>();
            runtime.Type = type;
            return runtime;
        }

        private static Arcanum.Formats.Text.MesFile ReadMes(string path)
            => _vfs.Exists(path) ? MesReader.Read(_vfs.ReadAllBytes(path)) : null;

        private static byte[] GuidBytes(string value)
        {
            var bytes = new byte[ArcanumObjectId.SerializedSize];
            bytes[0] = (byte)ArcanumObjectIdType.Guid;
            string hex = value.Replace("-", string.Empty);
            for (int index = 0; index < 16; index++)
                bytes[8 + index] = Convert.ToByte(hex.Substring(index * 2, 2), 16);
            return bytes;
        }

        private static ArcanumObjectId SourceGuid(string value) => ArcanumObjectId.FromBytes(GuidBytes(value));

        private static SectorNavigationMap Map()
        {
            TileNameTable names = TileNameTable.FromMes(MesReader.Read("{300}{grs}"));
            return new SectorNavigationMap(new SectorTerrain(new uint[SectorTerrain.TileCount]),
                new bool[SectorTerrain.TileCount], names);
        }

        private static void SetProperty(object target, string name, object value)
        {
            PropertyInfo property = target.GetType().GetProperty(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            property.SetValue(target, value);
        }
    }
}
