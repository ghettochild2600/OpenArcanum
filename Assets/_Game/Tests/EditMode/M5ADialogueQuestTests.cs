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
    [Category("M5A")]
    public sealed class M5ADialogueQuestTests
    {
        private const string Sector = "maps/arcanum1-024-fixed/68786586569.sec";
        private const int DialogNumber = 1760;
        private const int QuestNumber = 1130;
        private static readonly ArcanumObjectId Thomgrak = ArcanumObjectId.CreateGuid(
            Guid.Parse("8f3c75df-55b6-11d4-8f1d-00a0cc6511c6"));

        private static DatVirtualFileSystem _vfs;
        private static ScriptDatabase _scripts;
        private static DialogScript _authenticDialogue;
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private WorldObjectSectorLoader _loader;
        private PlayerNavigationController _navigation;
        private PlayerInteractionController _interaction;
        private PersistentPlayerState _pc;
        private PersistentObjectState _npc;
        private WorldObject _npcRuntime;

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
            Assert.That(_scripts.Get(DialogNumber), Is.Not.Null);
            Assert.That(_authenticDialogue, Is.Not.Null);
        }

        [OneTimeTearDown] public void ReleaseResources() => _vfs?.Dispose();

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M5ADialogueQuestTests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _root.AddComponent<ProductionDialoguePresenter>();
            _loader = _root.AddComponent<WorldObjectSectorLoader>();
            _navigation = _root.AddComponent<PlayerNavigationController>();
            _interaction = _root.AddComponent<PlayerInteractionController>();
            _session.BeginSector(Sector);
            SetProperty(_session, nameof(WorldMapSessionCoordinator.SelectedSector), Sector);
            SetProperty(_loader, nameof(WorldObjectSectorLoader.NavigationMap), Map());

            _pc = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(1, 1), 0x28100000u);
            WorldObject player = Runtime("Player", ObjectType.Pc);
            _session.BindPlayer(Sector, _pc, player);
            Assert.That(_navigation.TryBind(player), Is.True);

            var source = new ObjectInstance(ObjectType.Npc, 17232, Location(3, 1), 0x28100000u, 0, 0,
                oid: GuidBytes("8f3c75df-55b6-11d4-8f1d-00a0cc6511c6"), dialogNum: DialogNumber);
            _npc = _session.GetOrCreate(source, Sector, source.CurrentArtId.Value, false, false);
            CreateNpcCharacter();
            _npcRuntime = Runtime("Thomgrak", ObjectType.Npc);
            _session.Bind(Sector, _npc, _npcRuntime);
            _session.BindDialogueSource(_scripts.Get,
                number => number == DialogNumber ? _authenticDialogue : null);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void CampaignStateUsesSourceBoundsAndSharedFlagVariableStorage()
        {
            _session.Campaign.SetFlag(2438, 1);
            _session.Campaign.SetVar(1014, -42);
            Assert.That(_session.ScriptGlobals.GetFlag(2438), Is.EqualTo(1));
            Assert.That(_session.ScriptGlobals.GetVar(1014), Is.EqualTo(-42));
            Assert.Throws<CampaignStateService.CampaignStateException>(() => _session.Campaign.GetFlag(3200));
            Assert.Throws<CampaignStateService.CampaignStateException>(() => _session.Campaign.SetVar(-1, 0));
        }

        [Test]
        public void QuestStateInitializesUnknownAndGlobalStateInitializesAccepted()
        {
            Assert.That(_session.Campaign.GetPcQuestState(QuestNumber), Is.EqualTo((int)QuestState.Unknown));
            Assert.That(_session.Campaign.GetGlobalQuestState(QuestNumber), Is.EqualTo((int)QuestState.Accepted));
        }

        [Test]
        public void QuestTransitionAdvancesAndInvalidRegressionRollsBack()
        {
            Assert.That(_session.Campaign.TryAdvancePcQuest(QuestNumber, (int)QuestState.Accepted,
                out QuestState state, out CampaignStateFailure failure), Is.True);
            Assert.That(state, Is.EqualTo(QuestState.Accepted));
            Assert.That(failure, Is.EqualTo(CampaignStateFailure.None));
            Assert.That(_session.Campaign.TryAdvancePcQuest(QuestNumber, (int)QuestState.Mentioned,
                out _, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(CampaignStateFailure.QuestCannotRegress));
            Assert.That(_session.Campaign.GetPcQuestState(QuestNumber), Is.EqualTo((int)QuestState.Accepted));
        }

        [Test]
        public void DialogLocalStateIsKeyedByStableNpcAndSap()
        {
            _session.Campaign.SetLocalFlag(Thomgrak, (int)Sap.Dialog, 1, 1);
            _session.Campaign.SetLocalCounter(Thomgrak, (int)Sap.Dialog, 2, 73);
            Assert.That(_session.Campaign.GetLocalFlag(Thomgrak, (int)Sap.Dialog, 1), Is.EqualTo(1));
            Assert.That(_session.Campaign.GetLocalFlag(Thomgrak, (int)Sap.Use, 1), Is.Zero);
            Assert.That(_session.Campaign.GetLocalCounter(Thomgrak, (int)Sap.Dialog, 2), Is.EqualTo(73));
        }

        [Test]
        public void AuthenticResourceAndNpcIdentityAreBound()
        {
            Assert.That(_npc.Identity, Is.EqualTo(Thomgrak));
            Assert.That(_npc.PrototypeNumber, Is.EqualTo(17232));
            Assert.That(_npc.SourceSector, Is.EqualTo(Sector));
            Assert.That(_npc.DialogNum, Is.EqualTo(DialogNumber));
            Assert.That(_scripts.Get(DialogNumber).Entries.Count, Is.EqualTo(5));
            Assert.That(_authenticDialogue.TryGet(1, out DialogLine line), Is.True);
            Assert.That(line.IsNpcSpeech, Is.True);
        }

        [Test]
        public void AuthenticInitialNodeFiltersDeterministicallyForHumanPc()
        {
            Assert.That(_session.Dialogue.Start(_pc.Identity, Thomgrak), Is.EqualTo(DialogueStartStatus.Started));
            Assert.That(_session.Dialogue.CurrentLine, Is.EqualTo(1));
            Assert.That(_session.Dialogue.PcIdentity, Is.EqualTo(_pc.Identity));
            Assert.That(_session.Dialogue.NpcIdentity, Is.EqualTo(Thomgrak));
            Assert.That(_session.Dialogue.AvailableResponses.Select(line => line.Num),
                Is.EqualTo(new[] { 2, 11, 12, 19 }));
        }

        [Test]
        public void AuthenticChoiceExecutesLocalAndNpcEntryEffectsExactlyOnce()
        {
            StartAndChoose(11);
            Assert.That(_session.Dialogue.CurrentLine, Is.EqualTo(60));
            Assert.That(_session.Campaign.GetLocalFlag(Thomgrak, (int)Sap.Dialog, 1), Is.EqualTo(1));
            Assert.That(_session.Campaign.GetPcQuestState(QuestNumber), Is.EqualTo((int)QuestState.Mentioned));
            Assert.That(_session.Dialogue.SelectResponse(99), Is.EqualTo(DialogueChoiceStatus.InvalidChoice));
            Assert.That(_session.Campaign.GetPcQuestState(QuestNumber), Is.EqualTo((int)QuestState.Mentioned));
        }

        [Test]
        public void AuthenticClosedLoopSelectsChangedSecondConversationBranch()
        {
            StartAndChoose(11);
            Choose(61);
            Assert.That(_session.Campaign.GetPcQuestState(QuestNumber), Is.EqualTo((int)QuestState.Accepted));
            Assert.That(_session.Dialogue.CurrentLine, Is.EqualTo(70));
            Assert.That(_session.Dialogue.SelectResponse(0), Is.EqualTo(DialogueChoiceStatus.Completed));

            Assert.That(_session.Dialogue.Start(_pc.Identity, Thomgrak), Is.EqualTo(DialogueStartStatus.Started));
            Assert.That(_session.Dialogue.CurrentLine, Is.EqualTo(140));
            Assert.That(_session.Dialogue.AvailableResponses.Select(line => line.Num), Does.Contain(143));
            Assert.That(_session.Dialogue.AvailableResponses.Select(line => line.Num).ToArray(),
                Has.None.EqualTo(145));
        }

        [Test]
        public void UnsupportedAuthenticEffectFailsBeforePartialMutation()
        {
            Assert.That(_session.Dialogue.Start(_pc.Identity, Thomgrak), Is.EqualTo(DialogueStartStatus.Started));
            int index = ResponseIndex(12);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "OpenArcanum dialogue compatibility:.*UnsupportedEffect.*dialogue=1760"));
            Assert.That(_session.Dialogue.SelectResponse(index), Is.EqualTo(DialogueChoiceStatus.UnsupportedEffect));
            Assert.That(_session.Campaign.GetLocalFlag(Thomgrak, (int)Sap.Dialog, 1), Is.Zero);
            Assert.That(_session.Campaign.GetPcQuestState(QuestNumber), Is.Zero);
            Assert.That(_session.Dialogue.Phase, Is.EqualTo(DialogueSessionPhase.AwaitingPlayerChoice));
        }

        [Test]
        public void MissingAndUnsupportedScriptsFailClosed()
        {
            _session.BindDialogueSource(_ => null, _ => _authenticDialogue);
            Assert.That(_session.Dialogue.Start(_pc.Identity, Thomgrak), Is.EqualTo(DialogueStartStatus.MissingScript));

            ScriptFile unsupported = File(Cond(Sct.True, Act(Sat.Attack), Act(Sat.DoNothing)));
            _session.BindDialogueSource(_ => unsupported, _ => _authenticDialogue);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "OpenArcanum dialogue compatibility:.*UnsupportedScriptOpcode"));
            Assert.That(_session.Dialogue.Start(_pc.Identity, Thomgrak), Is.EqualTo(DialogueStartStatus.UnsupportedScript));
            Assert.That(_session.Campaign.GetPcQuestState(QuestNumber), Is.Zero);
        }

        [Test]
        public void MissingDialogueAndInvalidParticipantsFailExplicitly()
        {
            _session.BindDialogueSource(_scripts.Get, _ => null);
            Assert.That(_session.Dialogue.Start(_pc.Identity, Thomgrak), Is.EqualTo(DialogueStartStatus.MissingDialogue));
            Assert.That(_session.Dialogue.Start(Thomgrak, _pc.Identity), Is.EqualTo(DialogueStartStatus.InvalidPc));
            Assert.That(_session.Dialogue.Start(_pc.Identity, AuthoredId(99)), Is.EqualTo(DialogueStartStatus.InvalidNpc));
        }

        [Test]
        public void CancellationTargetLossAndDuplicateStartAreDeterministic()
        {
            Assert.That(_session.Dialogue.Start(_pc.Identity, Thomgrak), Is.EqualTo(DialogueStartStatus.Started));
            Assert.That(_session.Dialogue.Start(_pc.Identity, Thomgrak), Is.EqualTo(DialogueStartStatus.Busy));
            Assert.That(_session.UnbindPresentation(Sector, Thomgrak), Is.True);
            _session.Dialogue.ValidateActiveTarget();
            Assert.That(_session.Dialogue.Phase, Is.EqualTo(DialogueSessionPhase.Cancelled));
            Assert.That(_session.Campaign.GetPcQuestState(QuestNumber), Is.Zero);
        }

        [Test]
        public void CampaignAndConversationSurvivePresentationOnlyRebuild()
        {
            StartAndChoose(11);
            int line = _session.Dialogue.CurrentLine;
            _loader.RebuildVisuals();
            Assert.That(_session.Dialogue.CurrentLine, Is.EqualTo(line));
            Assert.That(_session.Campaign.GetPcQuestState(QuestNumber), Is.EqualTo((int)QuestState.Mentioned));
            Assert.That(_root.GetComponents<ProductionDialoguePresenter>().Length, Is.EqualTo(1));
        }

        [Test]
        public void CampaignStateSurvivesNpcUnloadAndReloadWithoutDuplicateState()
        {
            StartAndChoose(11);
            _session.Dialogue.Cancel("test unload");
            Assert.That(_session.UnbindPresentation(Sector, Thomgrak), Is.True);
            Object.DestroyImmediate(_npcRuntime.gameObject);
            _npcRuntime = Runtime("ThomgrakReloaded", ObjectType.Npc);
            _session.Bind(Sector, _npc, _npcRuntime);
            Assert.That(_session.States.Count(state => state.Key == Thomgrak), Is.EqualTo(1));
            Assert.That(_session.Campaign.GetLocalFlag(Thomgrak, (int)Sap.Dialog, 1), Is.EqualTo(1));
            Assert.That(_session.Campaign.GetPcQuestState(QuestNumber), Is.EqualTo((int)QuestState.Mentioned));
            Assert.That(_session.Dialogue.Start(_pc.Identity, Thomgrak), Is.EqualTo(DialogueStartStatus.Started));
            Assert.That(_session.Dialogue.CurrentLine, Is.EqualTo(140));
        }

        [Test]
        public void TalkIntentUsesSourceStartAndApproachRangesAndCanCancel()
        {
            Assert.That(InteractionRangeRules.TalkStartRange, Is.EqualTo(4));
            Assert.That(InteractionRangeRules.TalkApproachRange, Is.EqualTo(1));
            _npc.Placement = ObjectPlacement.InWorld(Sector, new Vector2(8, 1));
            _npc.TilePosition = new Vector2(8, 1);
            _npc.Restore(_npcRuntime);
            Assert.That(_interaction.TryTalk(Thomgrak).Code, Is.EqualTo(WorldInteractionResultCode.Approaching));
            Assert.That(_interaction.CancelPending().Code, Is.EqualTo(WorldInteractionResultCode.Cancelled));
            Assert.That(_session.Dialogue.Phase, Is.EqualTo(DialogueSessionPhase.Idle));
        }

        [Test]
        public void AuthoritativeTalkCommandStartsOnlyTheNpcDialogue()
        {
            WorldInteractionResult result = _session.ExecuteInteraction(new WorldInteractionCommand(
                _pc.Identity, Thomgrak, WorldInteractionCommandType.Talk));
            Assert.That(result.Code, Is.EqualTo(WorldInteractionResultCode.Success));
            Assert.That(result.ScriptNum, Is.EqualTo(DialogNumber));
            Assert.That(_session.Dialogue.Phase, Is.EqualTo(DialogueSessionPhase.AwaitingPlayerChoice));
        }

        private void StartAndChoose(int line)
        {
            Assert.That(_session.Dialogue.Start(_pc.Identity, Thomgrak), Is.EqualTo(DialogueStartStatus.Started));
            Assert.That(_session.Dialogue.SelectResponse(ResponseIndex(line)), Is.EqualTo(DialogueChoiceStatus.Advanced));
        }

        private void Choose(int line)
            => Assert.That(_session.Dialogue.SelectResponse(ResponseIndex(line)), Is.EqualTo(DialogueChoiceStatus.Advanced));

        private int ResponseIndex(int line)
        {
            for (int index = 0; index < _session.Dialogue.AvailableResponses.Count; index++)
                if (_session.Dialogue.AvailableResponses[index].Num == line) return index;
            Assert.Fail($"Response line {line} was unavailable at node {_session.Dialogue.CurrentLine}.");
            return -1;
        }

        private void CreateNpcCharacter()
        {
            var stats = new int[CharacterAttributeSet.SourceStatArrayCount];
            for (int index = 0; index < CharacterAttributeSet.Count; index++) stats[index] = 8;
            stats[CharacterAttributeSet.GenderSourceSlot] = (int)CharacterGender.Male;
            stats[CharacterAttributeSet.RaceSourceSlot] = (int)CharacterRace.Human;
            _session.Characters.GetOrCreateSourceCharacter(Thomgrak, ObjectType.Npc, 17232, stats, null);
            _session.Progression.GetOrCreateSourceCharacter(Thomgrak, ObjectType.Npc, 17232,
                new CharacterProgressionSource(1, 0, 0, new int[12], new int[4]));
            _session.DerivedStats.GetOrCreateSourceCharacter(Thomgrak, ObjectType.Npc, 17232,
                new CharacterDerivedSource(0, new int[5], 0, 0, 0));
        }

        private WorldObject Runtime(string name, ObjectType type)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform);
            var runtime = go.AddComponent<WorldObject>();
            runtime.Type = type;
            return runtime;
        }

        private static long Location(int x, int y) => (uint)x | ((long)(uint)y << 32);
        private static byte[] GuidBytes(string value)
        {
            var bytes = new byte[ArcanumObjectId.SerializedSize];
            bytes[0] = (byte)ArcanumObjectIdType.Guid;
            Array.Copy(Guid.Parse(value).ToByteArray(), 0, bytes, 8, 16);
            return bytes;
        }

        private static ArcanumObjectId AuthoredId(int number)
        {
            var bytes = new byte[24];
            bytes[0] = (byte)ArcanumObjectIdType.Authored;
            Array.Copy(BitConverter.GetBytes(number), 0, bytes, 8, 4);
            return ArcanumObjectId.FromBytes(bytes);
        }

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

        private static ScriptFile File(params ScriptCondition[] entries)
        {
            var file = new ScriptFile();
            file.Entries.AddRange(entries);
            return file;
        }

        private static ScriptCondition Cond(Sct type, ScriptAction action, ScriptAction els)
            => new() { Type = (int)type, Action = action, Els = els };
        private static ScriptAction Act(Sat type) => new() { Type = (int)type };
    }
}
