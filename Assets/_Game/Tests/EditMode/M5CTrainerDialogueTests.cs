using System;
using System.Collections.Generic;
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
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Dialogue;
using Arcanum.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M5C")]
    public sealed class M5CTrainerDialogueTests
    {
        private const string Sector = "maps/arcanum1-024-fixed/96636765255.sec";
        private const int DialogNumber = 1009;
        private const int QuestNumber = 1005;
        private static readonly ArcanumObjectId Mayor = SourceGuid(
            "787ad4ab-9061-2b4e-a691-f582800b2bb3");

        private static DatVirtualFileSystem _vfs;
        private static ScriptDatabase _scripts;
        private static DialogScript _dialogue;
        private static QuestLog _quests;
        private static ProtoLibrary _prototypes;
        private static ObjectInstance _mayorSource;

        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private WorldObjectSectorLoader _loader;
        private PersistentPlayerState _pc;
        private PersistentObjectState _mayor;
        private WorldObject _mayorRuntime;

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
            _dialogue = DialogLocator.Load(_vfs, DialogNumber);
            _quests = QuestLog.FromMes(ReadMes("mes/gamequestlog.mes"), ReadMes("rules/xp_quest.mes"),
                ReadMes("rules/gamequest.mes"), ReadMes("mes/gamequestlogdumb.mes"));
            _prototypes = new ProtoLibrary(GameDataLocator.FindDirectory("data/proto"));
            byte[] bytes = _vfs.ReadAllBytes(
                "maps/arcanum1-024-fixed/g_abd47a78_6190_4e2b_a691_f582800b2bb3.mob");
            int offset = 0;
            _mayorSource = ObjectInstanceReader.Read(bytes, ref offset);
            Assert.That(_mayorSource.Identity, Is.EqualTo(Mayor));
            Assert.That(_mayorSource.PrototypeNumber, Is.EqualTo(17088));
            Assert.That(_dialogue.TryGet(5, out DialogLine training), Is.True);
            Assert.That(training.TokenCode, Is.EqualTo('t'));
            Assert.That(training.TokenPayload, Is.EqualTo("11"));
        }

        [OneTimeTearDown] public void ReleaseResources() => _vfs?.Dispose();

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M5CTrainerDialogueTests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _root.AddComponent<ProductionDialoguePresenter>();
            _loader = _root.AddComponent<WorldObjectSectorLoader>();
            _root.AddComponent<PlayerNavigationController>();
            _root.AddComponent<PlayerInteractionController>();
            _session.BeginSector(Sector);
            SetProperty(_session, nameof(WorldMapSessionCoordinator.SelectedSector), Sector);
            SetProperty(_loader, nameof(WorldObjectSectorLoader.NavigationMap), Map());

            _pc = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(1, 1), 0x28100000u);
            _session.BindPlayer(Sector, _pc, Runtime("Player", ObjectType.Pc));

            ObjectProtoInfo prototype = _prototypes.Get(17088);
            _mayor = _session.GetOrCreate(_mayorSource, Sector,
                _mayorSource.CurrentArtId ?? prototype.CurrentArtId, false, false,
                _mayorSource.ItemFlags ?? prototype.ItemFlags ?? 0,
                _mayorSource.InvAid ?? prototype.InvAid,
                _mayorSource.WeaponFlags ?? prototype.Weapon?.Flags ?? 0,
                _mayorSource.GenericFlags ?? prototype.GenericFlags ?? 0,
                unitWeight: _mayorSource.Weight ?? prototype.Weight,
                inventoryFootprint: InventoryFootprint.OneCell,
                inventoryLocation: _mayorSource.InvLocation,
                nameIndex: _mayorSource.NameIndex ?? prototype.NameIndex,
                socialClass: _mayorSource.SocialClass ?? prototype.SocialClass);
            CreateMayorCharacter(prototype);
            _mayorRuntime = Runtime("Black Root Mayor", ObjectType.Npc);
            _session.Bind(Sector, _mayor, _mayorRuntime);
            _session.BindQuestSource(_quests);
            _session.BindDialogueSource(_scripts.Get, number => number == DialogNumber ? _dialogue : null);
            var generated = new GeneratedDialogText(ReadMes("mes/gd_pc2m.mes"), new System.Random(1005));
            _session.BindGeneratedDialogueText((_, token) => generated.For(token));
            _session.BindTrainingDialogueText(new SourceTrainingDialogueText(_session, ReadMes,
                new System.Random(1005)));
            _session.BindPrototypeSource(_prototypes.Get);
            _session.BindInventoryFootprintSource(_ => InventoryFootprint.OneCell);

            _session.Campaign.SetPcQuestState(QuestNumber, (int)QuestState.Mentioned);
            _session.Campaign.SetPcQuestState(QuestNumber, (int)QuestState.Accepted);
            _session.Campaign.SetPcQuestState(QuestNumber, (int)QuestState.Completed);
            _session.DerivedStats.SetReaction(Mayor, _pc.Identity, 53);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void AuthenticTrainingTokenRetainsSourcePayloadAcrossTextProjection()
        {
            Assert.That(_dialogue.TryGet(5, out DialogLine line), Is.True);
            DialogLine projected = line.WithText("Training");
            Assert.That(line.TokenCode, Is.EqualTo('t'));
            Assert.That(line.TokenPayload, Is.EqualTo("11"));
            Assert.That(projected.TokenCode, Is.EqualTo('t'));
            Assert.That(projected.TokenPayload, Is.EqualTo("11"));
        }

        [Test]
        public void SourcePayloadParserExpandsDecimalIdsAndInclusiveRanges()
        {
            Assert.That(ProductionDialogueSession.TryParseTrainingPayload("11, 12-15", out var skills,
                out string failure), Is.True, failure);
            Assert.That(skills, Is.EqualTo(new[]
            {
                CharacterSkill.Persuasion, CharacterSkill.Repair, CharacterSkill.Firearms,
                CharacterSkill.PickLocks, CharacterSkill.DisarmTraps,
            }));
        }

        [TestCase("")]
        [TestCase("11,")]
        [TestCase("11-9")]
        [TestCase("16")]
        [TestCase("eleven")]
        public void MalformedTrainingPayloadFailsExplicitly(string payload)
        {
            Assert.That(ProductionDialogueSession.TryParseTrainingPayload(payload, out var skills,
                out string failure), Is.False);
            Assert.That(skills, Is.Empty);
            Assert.That(failure, Is.Not.Empty);
        }

        [Test]
        public void SourcePriceFormulaMatchesReactionBandsAndMayorFixture()
        {
            Assert.That(DialogueTrainingService.CalculateCost(100, -1), Is.EqualTo(200));
            Assert.That(DialogueTrainingService.CalculateCost(100, 0), Is.EqualTo(200));
            Assert.That(DialogueTrainingService.CalculateCost(100, 49), Is.EqualTo(102));
            Assert.That(DialogueTrainingService.CalculateCost(100, 50), Is.EqualTo(100));
            Assert.That(DialogueTrainingService.CalculateCost(100, 53), Is.EqualTo(99));
            Assert.That(DialogueTrainingService.CalculateCost(100, 100), Is.EqualTo(80));
        }

        [Test]
        public void EligibilityRequiresAuthoredSkillAndSourceRank()
        {
            var service = new DialogueTrainingService(_session);
            DialogueTrainingRequest request = Request(CharacterSkill.Persuasion);
            Assert.That(service.Evaluate(request, new[] { CharacterSkill.Haggle }).Failure,
                Is.EqualTo(DialogueTrainingFailure.SkillNotOffered));
            Assert.That(service.Evaluate(request, new[] { CharacterSkill.Persuasion }).Failure,
                Is.EqualTo(DialogueTrainingFailure.InsufficientSkillRank));
            PreparePersuasion();
            DialogueTrainingResult valid = service.Evaluate(request, new[] { CharacterSkill.Persuasion });
            Assert.That(valid.Succeeded, Is.True);
            Assert.That(valid.Cost, Is.EqualTo(99));
        }

        [Test]
        public void OnlySourceTTokenApprenticeTierIsAdmitted()
        {
            PreparePersuasion();
            var service = new DialogueTrainingService(_session);
            var expert = new DialogueTrainingRequest(_pc.Identity, Mayor, CharacterSkill.Persuasion,
                SkillTrainingLevel.Expert);
            Assert.That(service.Evaluate(expert, new[] { CharacterSkill.Persuasion }).Failure,
                Is.EqualTo(DialogueTrainingFailure.UnsupportedTier));
        }

        [Test]
        public void InvalidTraineeAndTrainerFailBeforeAnyMutation()
        {
            PreparePersuasion();
            _session.AddGold(_pc.Identity, 100);
            var service = new DialogueTrainingService(_session);
            var wrongTrainee = new DialogueTrainingRequest(Mayor, Mayor, CharacterSkill.Persuasion,
                SkillTrainingLevel.Apprentice);
            var wrongTrainer = new DialogueTrainingRequest(_pc.Identity, _pc.Identity,
                CharacterSkill.Persuasion, SkillTrainingLevel.Apprentice);
            Assert.That(service.Train(wrongTrainee, new[] { CharacterSkill.Persuasion }).Failure,
                Is.EqualTo(DialogueTrainingFailure.InvalidTrainee));
            Assert.That(service.Train(wrongTrainer, new[] { CharacterSkill.Persuasion }).Failure,
                Is.EqualTo(DialogueTrainingFailure.InvalidTrainer));
            Assert.That(_session.Progression.GetTrainingLevel(_pc.Identity, CharacterSkill.Persuasion),
                Is.EqualTo(SkillTrainingLevel.None));
            Assert.That(_session.GetGold(_pc.Identity), Is.EqualTo(100));
            Assert.That(_session.GetGold(Mayor), Is.Zero);
        }

        [Test]
        public void ProgressionTransactionSnapshotRestoresPurchasedSkillsAndTraining()
        {
            PreparePersuasion();
            CharacterProgressionService.Snapshot snapshot = _session.Progression.CaptureSnapshot();
            Assert.That(_session.Progression.SetTrainingLevel(_pc.Identity, CharacterSkill.Persuasion,
                SkillTrainingLevel.Apprentice), Is.EqualTo(TrainingAssignmentResult.Success));
            Assert.That(_session.Progression.IncreaseSkill(_pc.Identity, CharacterSkill.Bow),
                Is.EqualTo(SkillIncreaseResult.Success));
            _session.Progression.RestoreSnapshot(snapshot);
            Assert.That(_session.Progression.GetTrainingLevel(_pc.Identity, CharacterSkill.Persuasion),
                Is.EqualTo(SkillTrainingLevel.None));
            Assert.That(_session.Progression.GetPurchasedSkillPoints(_pc.Identity, CharacterSkill.Persuasion),
                Is.EqualTo(1));
            Assert.That(_session.Progression.GetPurchasedSkillPoints(_pc.Identity, CharacterSkill.Bow), Is.Zero);
        }

        [Test]
        public void InsufficientGoldLeavesTrainingAndBothInventoriesUntouched()
        {
            PreparePersuasion();
            DialogueTrainingResult result = new DialogueTrainingService(_session)
                .Train(Request(CharacterSkill.Persuasion), new[] { CharacterSkill.Persuasion });
            Assert.That(result.Failure, Is.EqualTo(DialogueTrainingFailure.InsufficientGold));
            Assert.That(_session.Progression.GetTrainingLevel(_pc.Identity, CharacterSkill.Persuasion),
                Is.EqualTo(SkillTrainingLevel.None));
            Assert.That(_session.GetGold(_pc.Identity), Is.Zero);
            Assert.That(_session.GetGold(Mayor), Is.Zero);
        }

        [Test]
        public void SuccessfulTrainingAtomicallyTransfersGoldAndAssignsApprentice()
        {
            PreparePersuasion();
            _session.AddGold(_pc.Identity, 100);
            DialogueTrainingResult result = new DialogueTrainingService(_session)
                .Train(Request(CharacterSkill.Persuasion), new[] { CharacterSkill.Persuasion });
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Cost, Is.EqualTo(99));
            Assert.That(result.GoldBefore, Is.EqualTo(100));
            Assert.That(result.GoldAfter, Is.EqualTo(1));
            Assert.That(_session.GetGold(_pc.Identity), Is.EqualTo(1));
            Assert.That(_session.GetGold(Mayor), Is.EqualTo(99));
            Assert.That(_session.Progression.GetTrainingLevel(_pc.Identity, CharacterSkill.Persuasion),
                Is.EqualTo(SkillTrainingLevel.Apprentice));
        }

        [Test]
        public void AlreadyTrainedCannotChargeOrRepeatAssignment()
        {
            PreparePersuasion();
            _session.AddGold(_pc.Identity, 200);
            var service = new DialogueTrainingService(_session);
            Assert.That(service.Train(Request(CharacterSkill.Persuasion),
                new[] { CharacterSkill.Persuasion }).Succeeded, Is.True);
            int pcGold = _session.GetGold(_pc.Identity);
            int trainerGold = _session.GetGold(Mayor);
            DialogueTrainingResult repeat = service.Train(Request(CharacterSkill.Persuasion),
                new[] { CharacterSkill.Persuasion });
            Assert.That(repeat.Failure, Is.EqualTo(DialogueTrainingFailure.AlreadyTrained));
            Assert.That(_session.GetGold(_pc.Identity), Is.EqualTo(pcGold));
            Assert.That(_session.GetGold(Mayor), Is.EqualTo(trainerGold));
        }

        [Test]
        public void AuthenticMayorDialogueRunsSourceTrainingSubviewAndReturnsToTarget()
        {
            PreparePersuasion();
            _session.AddGold(_pc.Identity, 100);
            StartTraining();
            Assert.That(_session.Dialogue.TrainingView, Is.EqualTo(TrainingDialogueView.SkillSelection));
            Assert.That(_session.Dialogue.OfferedTrainingSkills, Is.EqualTo(new[] { CharacterSkill.Persuasion }));
            Assert.That(_session.Dialogue.AvailableResponses[0].Text, Is.EqualTo("Persuasion"));
            Assert.That(_session.Dialogue.SelectResponse(0), Is.EqualTo(DialogueChoiceStatus.Advanced));
            Assert.That(_session.Dialogue.TrainingView, Is.EqualTo(TrainingDialogueView.Payment));
            Assert.That(_session.Dialogue.NpcText, Does.Contain("99"));
            Assert.That(_session.Dialogue.SelectResponse(0), Is.EqualTo(DialogueChoiceStatus.Advanced));
            Assert.That(_session.Dialogue.TrainingView, Is.EqualTo(TrainingDialogueView.Result));
            Assert.That(_session.Dialogue.LastTrainingResult.Succeeded, Is.True);
            Assert.That(_session.Dialogue.SelectResponse(0), Is.EqualTo(DialogueChoiceStatus.Advanced));
            Assert.That(_session.Dialogue.TrainingView, Is.EqualTo(TrainingDialogueView.None));
            Assert.That(_session.Dialogue.CurrentLine, Is.EqualTo(20));
        }

        [Test]
        public void TrainingCancellationReturnsWithoutGoldOrSkillMutation()
        {
            PreparePersuasion();
            _session.AddGold(_pc.Identity, 100);
            StartTraining();
            int cancelIndex = _session.Dialogue.AvailableResponses.Count - 1;
            Assert.That(_session.Dialogue.SelectResponse(cancelIndex), Is.EqualTo(DialogueChoiceStatus.Advanced));
            Assert.That(_session.Dialogue.CurrentLine, Is.EqualTo(20));
            Assert.That(_session.GetGold(_pc.Identity), Is.EqualTo(100));
            Assert.That(_session.GetGold(Mayor), Is.Zero);
            Assert.That(_session.Progression.GetTrainingLevel(_pc.Identity, CharacterSkill.Persuasion),
                Is.EqualTo(SkillTrainingLevel.None));
        }

        [Test]
        public void PaymentDeclineReturnsWithoutMutation()
        {
            PreparePersuasion();
            _session.AddGold(_pc.Identity, 100);
            StartTraining();
            Assert.That(_session.Dialogue.SelectResponse(0), Is.EqualTo(DialogueChoiceStatus.Advanced));
            Assert.That(_session.Dialogue.SelectResponse(1), Is.EqualTo(DialogueChoiceStatus.Advanced));
            Assert.That(_session.Dialogue.CurrentLine, Is.EqualTo(20));
            Assert.That(_session.GetGold(_pc.Identity), Is.EqualTo(100));
            Assert.That(_session.GetGold(Mayor), Is.Zero);
        }

        [Test]
        public void SourceInsufficientRankResultReturnsWithoutPayment()
        {
            _session.AddGold(_pc.Identity, 100);
            StartTraining();
            Assert.That(_session.Dialogue.SelectResponse(0), Is.EqualTo(DialogueChoiceStatus.Advanced));
            Assert.That(_session.Dialogue.TrainingView, Is.EqualTo(TrainingDialogueView.Result));
            Assert.That(_session.Dialogue.LastTrainingResult.Failure,
                Is.EqualTo(DialogueTrainingFailure.InsufficientSkillRank));
            Assert.That(_session.GetGold(_pc.Identity), Is.EqualTo(100));
            Assert.That(_session.GetGold(Mayor), Is.Zero);
        }

        [Test]
        public void SourceInsufficientGoldResultReturnsWithoutAssignment()
        {
            PreparePersuasion();
            StartTraining();
            Assert.That(_session.Dialogue.SelectResponse(0), Is.EqualTo(DialogueChoiceStatus.Advanced));
            Assert.That(_session.Dialogue.SelectResponse(0), Is.EqualTo(DialogueChoiceStatus.Advanced));
            Assert.That(_session.Dialogue.TrainingView, Is.EqualTo(TrainingDialogueView.Result));
            Assert.That(_session.Dialogue.LastTrainingResult.Failure,
                Is.EqualTo(DialogueTrainingFailure.InsufficientGold));
            Assert.That(_session.Progression.GetTrainingLevel(_pc.Identity, CharacterSkill.Persuasion),
                Is.EqualTo(SkillTrainingLevel.None));
        }

        [Test]
        public void TrainingStateAndGoldSurviveVisualRebuildAndNpcRebindWithoutDuplicates()
        {
            PreparePersuasion();
            _session.AddGold(_pc.Identity, 100);
            DialogueTrainingResult result = new DialogueTrainingService(_session)
                .Train(Request(CharacterSkill.Persuasion), new[] { CharacterSkill.Persuasion });
            Assert.That(result.Succeeded, Is.True);
            _loader.RebuildVisuals();
            Assert.That(_session.Progression.GetTrainingLevel(_pc.Identity, CharacterSkill.Persuasion),
                Is.EqualTo(SkillTrainingLevel.Apprentice));
            Assert.That(_session.GetGold(_pc.Identity), Is.EqualTo(1));
            Assert.That(_session.GetGold(Mayor), Is.EqualTo(99));

            Assert.That(_session.UnbindPresentation(Sector, Mayor), Is.True);
            Object.DestroyImmediate(_mayorRuntime.gameObject);
            _mayorRuntime = Runtime("Black Root Mayor Reloaded", ObjectType.Npc);
            _session.Bind(Sector, _mayor, _mayorRuntime);
            Assert.That(_session.States.Count(pair => pair.Key == Mayor), Is.EqualTo(1));
            Assert.That(_session.Progression.GetTrainingLevel(_pc.Identity, CharacterSkill.Persuasion),
                Is.EqualTo(SkillTrainingLevel.Apprentice));
            Assert.That(_session.GetGold(_pc.Identity), Is.EqualTo(1));
            Assert.That(_session.GetGold(Mayor), Is.EqualTo(99));
        }

        private DialogueTrainingRequest Request(CharacterSkill skill)
            => new(_pc.Identity, Mayor, skill, SkillTrainingLevel.Apprentice);

        private void PreparePersuasion()
        {
            if (_session.Progression.GetEffectiveSkillRank(_pc.Identity, CharacterSkill.Persuasion) >= 1) return;
            _session.Progression.AwardExperience(_pc.Identity, 2100);
            Assert.That(_session.Progression.IncreaseSkill(_pc.Identity, CharacterSkill.Persuasion),
                Is.EqualTo(SkillIncreaseResult.Success));
        }

        private void StartTraining()
        {
            Assert.That(_session.Dialogue.Start(_pc.Identity, Mayor), Is.EqualTo(DialogueStartStatus.Started));
            int index = _session.Dialogue.AvailableResponses.ToList().FindIndex(response => response.Num == 5);
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            Assert.That(_session.Dialogue.SelectResponse(index), Is.EqualTo(DialogueChoiceStatus.Advanced));
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
            _session.Progression.GetOrCreateSourceCharacter(Mayor, ObjectType.Npc, 17088, progression);
            CharacterDerivedSource derived = CharacterDerivedSource.Resolve(
                _mayorSource.StatBase, prototype.StatBase,
                _mayorSource.BaseArmorClass, prototype.BaseArmorClass,
                _mayorSource.Resistances, prototype.Resistances,
                _mayorSource.ReactionBase, prototype.ReactionBase,
                _mayorSource.NpcFlags ?? prototype.NpcFlags ?? 0,
                _mayorSource.CritterFlags ?? prototype.CritterFlags ?? 0);
            _session.DerivedStats.GetOrCreateSourceCharacter(Mayor, ObjectType.Npc, 17088, derived);
        }

        private WorldObject Runtime(string name, ObjectType type)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform);
            var runtime = go.AddComponent<WorldObject>();
            runtime.Type = type;
            return runtime;
        }

        private static MesFile ReadMes(string path)
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
