using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Creation;
using Arcanum.Runtime.Magic;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.Technology;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using NUnit.Framework;
using OpenArcanum.Rendering;
using OpenArcanum.UI;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("UIDCharacterCreation")]
    public sealed class UiDCharacterCreationTests
    {
        private static RetailUiAssetResolver _retail;
        private string _fixtureRoot;
        private EnhancedUiAssetResolver _enhanced;
        private UiSkinResolver _skin;
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private GameUiController _controller;
        private RetailCharacterCreationView _view;

        [OneTimeSetUp]
        public void OneTimeSetUp() => _retail = RetailUiAssetResolver.CreateProduction();

        [OneTimeTearDown]
        public void OneTimeTearDown() { _retail?.Dispose(); _retail = null; }

        [SetUp]
        public void SetUp()
        {
            _fixtureRoot = Path.Combine(Path.GetTempPath(), "OpenArcanum-UID-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_fixtureRoot);
            _enhanced = new EnhancedUiAssetResolver(_fixtureRoot, _ => { });
            _skin = new UiSkinResolver(_retail, _enhanced);
            _root = new GameObject(nameof(UiDCharacterCreationTests));
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.BindCharacterCreationSource(CreateCatalog());
            _controller = new GameUiController(_session, new EmptySlots());
            Assert.That(_controller.BeginNewGame(), Is.True);
            _view = _root.AddComponent<RetailCharacterCreationView>();
            _view.Bind(_controller, _session, _skin);
            _view.Synchronize();
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            _skin?.Dispose();
            _enhanced?.Dispose();
            OpenArcanumGraphicsSettings.ClearRuntimeMode();
            if (Directory.Exists(_fixtureRoot)) Directory.Delete(_fixtureRoot, true);
        }

        [Test]
        public void RequiredIdentityAndEditorAssetsResolveFromRetailData()
        {
            foreach (int id in RetailCharacterCreationLayout.RequiredSourceIds)
                Assert.That(_retail.TryResolve(new UiAssetKey(id), out _), Is.True, $"source id {id}");
            Assert.That(_retail.TryResolve(new UiAssetKey(765), out UiResolvedAsset identity), Is.True);
            Assert.That(identity.LogicalSize, Is.EqualTo(new Vector2Int(800, 400)));
            Assert.That(_retail.TryResolve(new UiAssetKey(22), out UiResolvedAsset editor), Is.True);
            Assert.That(editor.LogicalSize, Is.EqualTo(new Vector2Int(800, 400)));
            Assert.That(_retail.TryResolvePortrait("HUM1", out RetailPortraitAsset portrait), Is.True);
            Assert.That(portrait.SourcePath, Does.StartWith("portrait/"));
        }

        [Test]
        public void ViewStartsOnOneFixedIdentityCompositionWithoutGlobalScrollRect()
        {
            Assert.That(_view.IsVisible, Is.True);
            Assert.That(_view.Stage, Is.EqualTo(RetailCharacterCreationStage.Identity));
            Assert.That(_view.IdentityRoot.sizeDelta, Is.EqualTo(new Vector2(800f, 400f)));
            Assert.That(_view.GetComponentsInChildren<ScrollRect>(true), Is.Empty);
            Assert.That(Object.FindObjectsByType<RetailCharacterCreationView>(FindObjectsSortMode.None),
                Has.Length.EqualTo(1));
        }

        [Test]
        public void IdentityGeometryMatchesRuntimeSourceRectangles()
        {
            Assert.That(RetailCharacterCreationLayout.Window, Is.EqualTo(new Rect(0, 100, 800, 400)));
            Assert.That(RetailCharacterCreationLayout.Portrait, Is.EqualTo(new Rect(95, 28, 64, 64)));
            Assert.That(RetailCharacterCreationLayout.Name, Is.EqualTo(new Rect(46, 183, 228, 18)));
            Assert.That(RetailCharacterCreationLayout.Gender, Is.EqualTo(new Rect(66, 243, 186, 19)));
            Assert.That(RetailCharacterCreationLayout.Race, Is.EqualTo(new Rect(66, 293, 186, 19)));
            Assert.That(RetailCharacterCreationLayout.Background, Is.EqualTo(new Rect(66, 343, 186, 19)));
        }

        [Test]
        public void NameEntryUsesRetailLimitAndUpdatesOnlyControllerDraft()
        {
            Assert.That(_view.NameInput.characterLimit, Is.EqualTo(23));
            _view.NameInput.text = "Ada";
            Assert.That(_controller.CreationDraft.Name, Is.EqualTo("Ada"));
            Assert.That(_session.PlayerState, Is.Null);
        }

        [Test]
        public void PortraitGenderAndRaceCyclingRemainCatalogAndBodyLegal()
        {
            int first = _controller.CreationDraft.PortraitId;
            Invoke("Next Portrait");
            Assert.That(_controller.CreationDraft.PortraitId, Is.Not.EqualTo(first));
            Invoke("Previous Gender");
            Assert.That(_controller.CreationDraft.Gender, Is.EqualTo(CharacterGender.Female));
            Assert.That(_session.CharacterCreation.Catalog.IsPortraitLegal(_controller.CreationDraft.PortraitId,
                _controller.CreationDraft.Race, _controller.CreationDraft.Gender), Is.True);
            Invoke("Next Race");
            Assert.That(_controller.CreationDraft.Race, Is.EqualTo(CharacterRace.Dwarf));
            Assert.That(_controller.CreationDraft.Gender, Is.EqualTo(CharacterGender.Male));
        }

        [Test]
        public void BackgroundCycleSkipsUnsupportedEffectWithoutInventingRepair()
        {
            Assert.That(_controller.CreationDraft.BackgroundId, Is.Zero);
            Invoke("Next Background");
            Assert.That(_controller.CreationDraft.BackgroundId, Is.EqualTo(1));
            Invoke("Next Background");
            Assert.That(_controller.CreationDraft.BackgroundId, Is.Zero);
        }

        [Test]
        public void ContinueTransitionsToSourceEditorWithoutCreatingPlayer()
        {
            _view.ContinueToEditor();
            Assert.That(_view.Stage, Is.EqualTo(RetailCharacterCreationStage.Editor));
            Assert.That(_view.EditorRoot.sizeDelta, Is.EqualTo(new Vector2(800f, 400f)));
            Assert.That(_view.EditorPanelRoot.sizeDelta, Is.EqualTo(new Vector2(235f, 325f)));
            Assert.That(_session.PlayerState, Is.Null);
        }

        [Test]
        public void InvalidNameCannotLeaveIdentityStage()
        {
            _view.NameInput.text = string.Empty;
            _view.ContinueToEditor();
            Assert.That(_view.Stage, Is.EqualTo(RetailCharacterCreationStage.Identity));
            Assert.That(_controller.CreationValidation.Failure, Is.EqualTo(CharacterCreationFailure.InvalidName));
        }

        [Test]
        public void AttributeButtonsRouteThroughControllerAndCharacterPointValidation()
        {
            _view.ContinueToEditor();
            Invoke("Increase Strength");
            Assert.That(_controller.CreationDraft.GetAttribute(CharacterAttribute.Strength), Is.EqualTo(9));
            Assert.That(_controller.CreationValidation.RemainingCharacterPoints, Is.EqualTo(4));
            Invoke("Decrease Strength");
            Assert.That(_controller.CreationDraft.GetAttribute(CharacterAttribute.Strength), Is.EqualTo(8));
        }

        [Test]
        public void SkillsPanelUsesFourSourceGroupsAndM12BMutation()
        {
            _view.ContinueToEditor();
            Invoke("Increase Bow");
            Assert.That(_controller.CreationDraft.GetSkillPoints(CharacterSkill.Bow), Is.EqualTo(1));
            Invoke("Skill Group 3");
            Assert.That(_view.SkillGroup, Is.EqualTo(3));
            Invoke("Increase Repair");
            Assert.That(_controller.CreationDraft.GetSkillPoints(CharacterSkill.Repair), Is.EqualTo(1));
        }

        [Test]
        public void MagicPanelUsesSourceCollegeIconsAndSequentialM12BRank()
        {
            _view.ContinueToEditor();
            Invoke("Magic");
            Assert.That(_view.EditorPage, Is.EqualTo(RetailCreationEditorPage.Magic));
            Invoke("Increase Spell");
            Assert.That(_controller.CreationDraft.GetSpellRank(SpellCollege.Conveyance), Is.EqualTo(1));
        }

        [Test]
        public void TechnologyPanelUsesEightSourceDisciplinesAndM12BRank()
        {
            _view.ContinueToEditor();
            Invoke("Technology");
            Assert.That(_view.EditorPage, Is.EqualTo(RetailCreationEditorPage.Technology));
            Invoke("Increase Technology");
            Assert.That(_controller.CreationDraft.GetTechnologyRank(TechnologyDiscipline.Herbology), Is.EqualTo(1));
        }

        [Test]
        public void EscapeReturnsEditorToIdentityThenMainMenuWithoutPartialPlayer()
        {
            _view.ContinueToEditor();
            _view.HandleEscape();
            Assert.That(_view.Stage, Is.EqualTo(RetailCharacterCreationStage.Identity));
            Assert.That(_controller.CreationDraft, Is.Not.Null);
            _view.HandleEscape();
            Assert.That(_controller.Screen, Is.EqualTo(GameUiScreen.MainMenu));
            Assert.That(_session.PlayerState, Is.Null);
        }

        [Test]
        public void OriginalEnhancedOriginalRebuildPreservesDraftStageAndPanelSelection()
        {
            _view.ContinueToEditor();
            Invoke("Magic");
            _controller.SetCreationName("Ada");
            OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Enhanced);
            _view.Synchronize();
            OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Original);
            _view.Synchronize();
            Assert.That(_view.Stage, Is.EqualTo(RetailCharacterCreationStage.Editor));
            Assert.That(_view.EditorPage, Is.EqualTo(RetailCreationEditorPage.Magic));
            Assert.That(_controller.CreationDraft.Name, Is.EqualTo("Ada"));
            Assert.That(_session.PlayerState, Is.Null);
        }

        [TestCase(800, 600)]
        [TestCase(1024, 768)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3840, 2160)]
        public void FixedCompositionRemainsCenteredAndAccessibleAtTargetResolution(int width, int height)
        {
            UiLogicalMapping mapping = UiLogicalMapping.ForResolution(width, height);
            Rect window = mapping.LogicalToScreen(RetailCharacterCreationLayout.Window);
            Assert.That(window.xMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(window.yMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(window.xMax, Is.LessThanOrEqualTo(width + .001f));
            Assert.That(window.yMax, Is.LessThanOrEqualTo(height + .001f));
            Rect accept = mapping.LogicalToScreen(new Rect(754f, 121f, 32f, 32f));
            Assert.That(accept.xMax, Is.LessThanOrEqualTo(width + .001f));
            Assert.That(accept.yMax, Is.LessThanOrEqualTo(height + .001f));
        }

        private void Invoke(string name)
        {
            Button button = _view.GetComponentsInChildren<Button>(true)
                .Single(value => value.name == name);
            Assert.That(button.interactable, Is.True, name);
            button.onClick.Invoke();
            _view.Synchronize();
        }

        private static CharacterCreationCatalog CreateCatalog()
        {
            MesFile rules = Mes(
                (0, "1000"), (1, "86"), (2, ""), (3, "400"), (4, ""),
                (10, "1010"), (11, "87"), (12, ""), (13, "400"), (14, ""),
                (20, "1020"), (21, "88"), (22, ""), (23, "400"), (24, ""));
            MesFile text = Mes(
                (1000, "No Significant Background\n\nNothing happened."),
                (1010, "Snake Handler\n\nPoison and beauty tradeoffs."),
                (1020, "Unsupported\n\nUnknown source effect."));
            MesFile effects = Mes((86, ""), (87, "resistpoison 20, be -1"), (88, "unknown 1"));
            MesFile portraits = Mes(
                (1000, "HUM1"), (1001, "HUM2"), (1002, "HUF1"),
                (1003, "DWM1"), (1004, "ELM1"), (1005, "ELF1"),
                (1006, "HAM1"), (1007, "HAF1"), (1008, "GNM1"),
                (1009, "HEM1"), (1010, "HOM1"), (1011, "HOF1"), (1012, "HGM1"));
            MapList maps = MapList.Read(Mes((5000,
                "Arcanum1-024-fixed, 92958, 82592, Type: START_MAP, WorldMap: 0")));
            return CharacterCreationCatalog.FromMes(rules, text, effects, portraits, maps);
        }

        private static MesFile Mes(params (int key, string value)[] values)
            => new(values.Select(value => new KeyValuePair<int, string>(value.key, value.value)).ToList());

        private sealed class EmptySlots : ISessionSaveSlotOperations
        {
            public SessionSaveSlotResult SaveSlot(string slotId) => new(SessionSaveSlotFailure.None);
            public SessionSaveSlotResult LoadSlot(string slotId) => new(SessionSaveSlotFailure.None);
            public SessionSaveSlotListResult ListSlots()
                => new(SessionSaveSlotFailure.None, Array.Empty<SessionSaveSlotInfo>());
            public SessionSaveSlotResult DeleteSlot(string slotId) => new(SessionSaveSlotFailure.None);
        }
    }
}
