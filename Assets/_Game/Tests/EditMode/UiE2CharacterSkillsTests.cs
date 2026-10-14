using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Crafting;
using Arcanum.Runtime.Magic;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.Technology;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using Arcanum.World;
using NUnit.Framework;
using OpenArcanum.Rendering;
using OpenArcanum.UI;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("UIE2CharacterSkills")]
    public sealed class UiE2CharacterSkillsTests
    {
        private const string Sector = "maps/test/1.sec";
        private static RetailUiAssetResolver _retail;
        private string _fixtureRoot;
        private EnhancedUiAssetResolver _enhanced;
        private UiSkinResolver _skin;
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private GameUiController _controller;
        private RetailCharacterSheetView _view;
        private ArcanumObjectId _pc;

        [OneTimeSetUp] public void OneTimeSetUp() => _retail = RetailUiAssetResolver.CreateProduction();
        [OneTimeTearDown] public void OneTimeTearDown() { _retail?.Dispose(); _retail = null; }

        [SetUp]
        public void SetUp()
        {
            _fixtureRoot = Path.Combine(Path.GetTempPath(), "OpenArcanum-UIE2-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_fixtureRoot);
            _enhanced = new EnhancedUiAssetResolver(_fixtureRoot, _ => { });
            _skin = new UiSkinResolver(_retail, _enhanced);
            _root = new GameObject(nameof(UiE2CharacterSkillsTests));
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new Owner(_session));
            Assert.That(_session.SelectSector(Sector), Is.True);
            _pc = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            _session.GetOrCreatePlayer(_pc, Sector, Vector2.one, 0x28100000u);
            _session.BindSpellPresentationSource(CreateSpellText());
            _session.BindCraftingSource(CreateSchematicCatalog());
            _controller = new GameUiController(_session, new EmptySlots());
            Assert.That(_controller.Open(GameUiScreen.Character), Is.True);
            _view = _root.AddComponent<RetailCharacterSheetView>();
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
        public void RuntimeCharacterEditorUsesSourceProvenAssetsAndOnePresenter()
        {
            foreach (int id in RetailCharacterSheetLayout.RequiredSourceIds)
                Assert.That(_retail.TryResolve(new UiAssetKey(id), out _), Is.True, $"source id {id}");
            Assert.That(_view.BackgroundAsset.Key.SourceId, Is.EqualTo(22));
            Assert.That(_view.WindowTransform.sizeDelta, Is.EqualTo(new Vector2(800, 400)));
            Assert.That(_view.WindowTransform.anchoredPosition, Is.EqualTo(new Vector2(0, 59)));
            Assert.That(RetailCharacterSheetLayout.SkillsPanel, Is.EqualTo(new Rect(503, 63, 235, 325)));
            Assert.That(_view.SkillsPanelTransform.anchoredPosition, Is.EqualTo(new Vector2(503, -63)));
            Assert.That(_view.GetComponentsInChildren<RectTransform>(true)
                .Single(value => value.name == "Name").anchoredPosition, Is.EqualTo(new Vector2(212, -15)));
            Assert.That(_view.GetComponentsInChildren<RectTransform>(true)
                .Single(value => value.name == "Hit Point Value").anchoredPosition,
                Is.EqualTo(new Vector2(440, -143)));
            Assert.That(_view.GetComponentsInChildren<RectTransform>(true)
                .Single(value => value.name == "Fatigue Value").anchoredPosition,
                Is.EqualTo(new Vector2(440, -213)));
            Assert.That(_view.GetComponentsInChildren<ScrollRect>(true), Is.Empty);
            Assert.That(Object.FindObjectsByType<RetailCharacterSheetView>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
        }

        [Test]
        public void RuntimeCharacterEditorRendersSourceLabelsAndButtonFrames()
        {
            SourceUiBitmapText[] text = _view.GetComponentsInChildren<SourceUiBitmapText>(true);
            Assert.That(text.Single(value => value.name == "Character Points").Text, Does.StartWith("POINTS: "));
            Assert.That(text.Single(value => value.name == "Level").Text, Does.StartWith("LEVEL: "));
            Assert.That(text.Single(value => value.name == "Experience").Text, Does.StartWith("XP NEXT: "));
            Assert.That(text.Single(value => value.name == "Race").Text, Does.StartWith("RACE: "));
            Assert.That(text.Single(value => value.name == "Gender").Text, Does.StartWith("GENDER: "));
            Assert.That(text.Single(value => value.name == "Age").Text,
                Is.EqualTo(_controller.ProjectCharacter().Age > 0
                    ? $"AGE: {_controller.ProjectCharacter().Age}"
                    : string.Empty));

            foreach (string name in new[]
                     {
                         "Skills", "Technology", "Magic", "Skill Group 0", "Skill Group 1",
                         "Skill Group 2", "Skill Group 3",
                     })
            {
                SourceUiImage image = _view.GetComponentsInChildren<SourceUiImage>(true)
                    .Single(value => value.name == name);
                Assert.That(image.CurrentAsset, Is.Not.Null, name);
                Assert.That(image.CurrentAsset.Key.Palette, Is.Zero, name);
            }

            Assert.That(text.Single(value => value.name == "Carry").FontSourceId, Is.EqualTo(26));
            Assert.That(text.Single(value => value.name == "Resistance Normal").FontSourceId, Is.EqualTo(26));
        }

        [TestCase(800, 600, 0, 41)]
        [TestCase(1024, 768, 112, 125)]
        [TestCase(1920, 1080, 560, 281)]
        [TestCase(2560, 1440, 880, 461)]
        [TestCase(3840, 2160, 1520, 821)]
        public void RuntimeBigWindowUsesSourceHrpPlacement(int width, int height, float x, float y)
            => Assert.That(RetailCharacterSheetLayout.ScreenRect(width, height),
                Is.EqualTo(new Rect(x, y, 800, 400)));

        [Test]
        public void ProjectionUsesM4DerivedVitalityResistanceAndSkillAuthorities()
        {
            GameUiCharacterView projection = _controller.ProjectCharacter();
            Assert.That(projection.Attributes.Count, Is.EqualTo(8));
            Assert.That(projection.Skills.Count, Is.EqualTo(16));
            Assert.That(projection.DerivedStats.Count, Is.EqualTo(9));
            Assert.That(projection.Resistances.Count, Is.EqualTo(5));
            Assert.That(projection.MaximumHitPoints, Is.EqualTo(_session.Vitality.GetMaximumHitPoints(_pc)));
            Assert.That(projection.MaximumFatigue, Is.EqualTo(_session.Vitality.GetMaximumFatigue(_pc)));
            Assert.That(projection.ArmorClass, Is.EqualTo(_session.DerivedStats.GetArmorClass(_pc)));
        }

        [Test]
        public void AttributeIncreaseAndSameSessionRefundAreAtomic()
        {
            int points = _session.Progression.GetUnspentCharacterPoints(_pc);
            Assert.That(_controller.AdjustAttribute(CharacterAttribute.Strength, 1), Is.True);
            Assert.That(_session.Characters.GetBaseAttribute(_pc, CharacterAttribute.Strength), Is.EqualTo(9));
            Assert.That(_session.Progression.GetUnspentCharacterPoints(_pc), Is.EqualTo(points - 1));
            Assert.That(_controller.AdjustAttribute(CharacterAttribute.Strength, -1), Is.True);
            Assert.That(_session.Characters.GetBaseAttribute(_pc, CharacterAttribute.Strength), Is.EqualTo(8));
            Assert.That(_session.Progression.GetUnspentCharacterPoints(_pc), Is.EqualTo(points));
        }

        [Test]
        public void AcceptedAttributeBaselineFailsClosedWithoutPointMutation()
        {
            int points = _session.Progression.GetUnspentCharacterPoints(_pc);
            Assert.That(_controller.AdjustAttribute(CharacterAttribute.Strength, -1), Is.False);
            Assert.That(_session.Characters.GetBaseAttribute(_pc, CharacterAttribute.Strength), Is.EqualTo(8));
            Assert.That(_session.Progression.GetUnspentCharacterPoints(_pc), Is.EqualTo(points));
            Assert.That(_controller.Feedback, Is.EqualTo(nameof(RuntimeCharacterAdjustmentResult.AcceptedBaseline)));
        }

        [Test]
        public void AttributeDecreaseRejectsNewSkillPrerequisiteWithoutPartialMutation()
        {
            Assert.That(_controller.AdjustAttribute(CharacterAttribute.Dexterity, 1), Is.True);
            Assert.That(_controller.IncreaseSkill(CharacterSkill.Dodge), Is.True);
            Assert.That(_controller.IncreaseSkill(CharacterSkill.Dodge), Is.True);
            int points = _session.Progression.GetUnspentCharacterPoints(_pc);
            Assert.That(_controller.AdjustAttribute(CharacterAttribute.Dexterity, -1), Is.False);
            Assert.That(_session.Characters.GetBaseAttribute(_pc, CharacterAttribute.Dexterity), Is.EqualTo(9));
            Assert.That(_session.Progression.GetUnspentCharacterPoints(_pc), Is.EqualTo(points));
            Assert.That(_controller.Feedback,
                Is.EqualTo(nameof(RuntimeCharacterAdjustmentResult.DependentSkillPrerequisite)));
        }

        [Test]
        public void SkillIncreaseAndSameSessionRefundUseM4C()
        {
            int points = _session.Progression.GetUnspentCharacterPoints(_pc);
            Assert.That(_controller.IncreaseSkill(CharacterSkill.Melee), Is.True);
            Assert.That(_session.Progression.GetPurchasedSkillPoints(_pc, CharacterSkill.Melee), Is.EqualTo(1));
            Assert.That(_controller.DecreaseSkill(CharacterSkill.Melee), Is.True);
            Assert.That(_session.Progression.GetPurchasedSkillPoints(_pc, CharacterSkill.Melee), Is.Zero);
            Assert.That(_session.Progression.GetUnspentCharacterPoints(_pc), Is.EqualTo(points));
        }

        [Test]
        public void ClosingAcceptsSkillBaselineAndReopenPreventsSellingItBack()
        {
            Assert.That(_controller.IncreaseSkill(CharacterSkill.Melee), Is.True);
            _controller.Close();
            Assert.That(_controller.Open(GameUiScreen.Character), Is.True);
            int points = _session.Progression.GetUnspentCharacterPoints(_pc);
            Assert.That(_controller.DecreaseSkill(CharacterSkill.Melee), Is.False);
            Assert.That(_session.Progression.GetPurchasedSkillPoints(_pc, CharacterSkill.Melee), Is.EqualTo(1));
            Assert.That(_session.Progression.GetUnspentCharacterPoints(_pc), Is.EqualTo(points));
        }

        [Test]
        public void TrainingIsProjectedFromExistingM4CAuthority()
        {
            Assert.That(_controller.IncreaseSkill(CharacterSkill.Melee), Is.True);
            Assert.That(_session.Progression.SetTrainingLevel(_pc, CharacterSkill.Melee,
                SkillTrainingLevel.Apprentice), Is.EqualTo(TrainingAssignmentResult.Success));
            Assert.That(_controller.ProjectCharacter().Skills.Single(value => value.Skill == CharacterSkill.Melee)
                .Training, Is.EqualTo(SkillTrainingLevel.Apprentice));
            _view.Synchronize();
            SourceUiBitmapText name = _view.GetComponentsInChildren<SourceUiBitmapText>(true)
                .Single(value => value.name == "Melee Name");
            Assert.That(name.Text, Is.EqualTo("Melee (Apprentice)"));
            Assert.That(_view.GetComponentsInChildren<SourceUiBitmapText>(true)
                .Any(value => value.name.EndsWith(" Rank", StringComparison.Ordinal)), Is.False,
                "source skill gauges must not be covered by synthetic purchased/rank/training strings");
        }

        [Test]
        public void CharacterFamilyControlsRouteToTheirAuthoritativeScreens()
        {
            Button technology = _view.GetComponentsInChildren<Button>(true)
                .Single(value => value.name == "Technology");
            technology.onClick.Invoke();
            Assert.That(_controller.Screen, Is.EqualTo(GameUiScreen.Technology));

            Assert.That(_controller.Open(GameUiScreen.Character), Is.True);
            _view.Synchronize();
            Button magic = _view.GetComponentsInChildren<Button>(true)
                .Single(value => value.name == "Magic");
            magic.onClick.Invoke();
            Assert.That(_controller.Screen, Is.EqualTo(GameUiScreen.Magic));
        }

        [Test]
        public void RuntimeMagicUsesSourcePanelCollegeAndSpellRows()
        {
            _session.Magic.SetKnownCollegeRank(_pc, SpellCollege.Earth, 2);
            Assert.That(_controller.Open(GameUiScreen.Magic), Is.True);
            _view.SelectCollege(SpellCollege.Earth);
            _view.Synchronize();

            Assert.That(_view.IsVisible, Is.True);
            Assert.That(_view.SelectedCollege, Is.EqualTo(SpellCollege.Earth));
            Assert.That(RetailCharacterSheetLayout.MagicPanel, Is.EqualTo(new Rect(503, 63, 235, 325)));
            Assert.That(_view.SkillsPanelTransform.anchoredPosition, Is.EqualTo(new Vector2(503, -63)));
            Assert.That(_view.GetComponentsInChildren<SourceUiImage>(true)
                .Single(value => value.name == "Magic Window").Key.SourceId, Is.EqualTo(31));

            SourceUiButton[] colleges = _view.GetComponentsInChildren<SourceUiButton>(true)
                .Where(value => Enum.TryParse(value.name, out SpellCollege _)).ToArray();
            Assert.That(colleges, Has.Length.EqualTo(16));
            for (int index = 0; index < colleges.Length; index++)
            {
                SourceUiButton button = colleges.Single(value => value.name == ((SpellCollege)index).ToString());
                RectTransform rect = (RectTransform)button.transform;
                Assert.That(rect.anchoredPosition,
                    Is.EqualTo(new Vector2(RetailCharacterSheetLayout.SpellCollegePositions[index].x,
                        -RetailCharacterSheetLayout.SpellCollegePositions[index].y)));
                Assert.That(button.CurrentPresentationKey.SourceId,
                    Is.EqualTo(RetailCharacterSheetLayout.SpellCollegeIcons[index]));
            }

            SourceUiBitmapText[] text = _view.GetComponentsInChildren<SourceUiBitmapText>(true);
            Assert.That(text.Single(value => value.name == "College Name").Text, Is.EqualTo("Earth Source"));
            for (int rank = 1; rank <= 5; rank++)
            {
                int spellId = (int)SpellCollege.Earth * 5 + rank - 1;
                Assert.That(text.Single(value => value.name == $"Spell {rank}").Text,
                    Is.EqualTo($"Source Spell {spellId}"));
                Assert.That(text.Single(value => value.name == $"Spell {rank}").FontSourceId, Is.EqualTo(27));
                Assert.That(text.Single(value => value.name == $"Spell Level {rank}").Text,
                    Is.EqualTo($"Minimum Level: {SpellPresentationCatalog.MinimumLevel(rank)}"));
                Assert.That(text.Single(value => value.name == $"Spell Level {rank}").FontSourceId, Is.EqualTo(28));
                Assert.That(_view.GetComponentsInChildren<SourceUiImage>(true)
                    .Single(value => value.name == $"Spell {spellId} Icon").Key.SourceId,
                    Is.EqualTo(SpellPresentationCatalog.IconSourceId(spellId)));
            }
        }

        [Test]
        public void RuntimeMagicHoverUsesSourceCollegeIconTitleAndDescriptions()
        {
            Assert.That(_controller.Open(GameUiScreen.Magic), Is.True);
            _view.SelectCollege(SpellCollege.Earth);
            _view.Synchronize();

            SourceHoverHintTarget college = _view.GetComponentsInChildren<SourceHoverHintTarget>(true)
                .Single(value => value.name == nameof(SpellCollege.Earth));
            college.OnPointerEnter(null);
            Assert.That(_controller.ContextualHelpTitle, Is.EqualTo("Earth Source"));
            Assert.That(_controller.ContextualHelp, Is.EqualTo("College Description 3"));
            Assert.That(_controller.ContextualHelpIconSourceId, Is.EqualTo(449));
            college.OnPointerExit(null);
            Assert.That(_controller.ContextualHelp, Is.Empty);

            SourceHoverHintTarget spell = _view.GetComponentsInChildren<SourceHoverHintTarget>(true)
                .Single(value => value.name == "Spell 15 Hover");
            spell.OnPointerEnter(null);
            Assert.That(_controller.ContextualHelpTitle, Is.EqualTo("Source Spell 15"));
            Assert.That(_controller.ContextualHelp, Does.StartWith("Willpower: 6    Fatigue: 5"));
            Assert.That(_controller.ContextualHelp, Does.Contain("Spell Description 15"));
            Assert.That(_controller.ContextualHelpIconSourceId, Is.EqualTo(449));
            spell.OnPointerExit(null);
            Assert.That(_controller.ContextualHelp, Is.Empty);
        }

        [Test]
        public void RuntimeMagicAdvancementUsesSourcePrerequisitesAndAcceptedBaseline()
        {
            Assert.That(_controller.Open(GameUiScreen.Magic), Is.True);
            int points = _session.Progression.GetUnspentCharacterPoints(_pc);

            Assert.That(_controller.AdjustSpellCollege(SpellCollege.Earth, 1), Is.True);
            Assert.That(_session.Magic.GetKnownCollegeRank(_pc, SpellCollege.Earth), Is.EqualTo(1));
            Assert.That(_session.Progression.GetUnspentCharacterPoints(_pc), Is.EqualTo(points - 1));
            Assert.That(_controller.AdjustSpellCollege(SpellCollege.Earth, 1), Is.False,
                "rank two requires source Willpower 9");
            Assert.That(_controller.Feedback, Is.EqualTo("SpellPrerequisiteNotMet"));
            Assert.That(_session.Progression.GetUnspentCharacterPoints(_pc), Is.EqualTo(points - 1));

            Assert.That(_controller.AdjustSpellCollege(SpellCollege.Earth, -1), Is.True);
            Assert.That(_session.Progression.GetUnspentCharacterPoints(_pc), Is.EqualTo(points));
            Assert.That(_controller.AdjustSpellCollege(SpellCollege.Earth, 1), Is.True);
            _controller.Close();
            Assert.That(_controller.Open(GameUiScreen.Magic), Is.True);
            int acceptedPoints = _session.Progression.GetUnspentCharacterPoints(_pc);
            Assert.That(_controller.AdjustSpellCollege(SpellCollege.Earth, -1), Is.False);
            Assert.That(_controller.Feedback,
                Is.EqualTo(nameof(RuntimeCharacterAdjustmentResult.AcceptedBaseline)));
            Assert.That(_session.Magic.GetKnownCollegeRank(_pc, SpellCollege.Earth), Is.EqualTo(1));
            Assert.That(_session.Progression.GetUnspentCharacterPoints(_pc), Is.EqualTo(acceptedPoints));
        }

        [Test]
        public void RuntimeTechnologyUsesSourcePanelDisciplinesAndDegreeRows()
        {
            _session.Technology.LearnNextDegree(_pc, TechnologyDiscipline.Herbology);
            Assert.That(_controller.Open(GameUiScreen.Technology), Is.True);
            _view.SelectTechnology(TechnologyDiscipline.Herbology);
            _view.Synchronize();

            Assert.That(_view.IsVisible, Is.True);
            Assert.That(_view.SelectedTechnology, Is.EqualTo(TechnologyDiscipline.Herbology));
            Assert.That(_view.GetComponentsInChildren<SourceUiImage>(true)
                .Single(value => value.name == "Technology Window").Key.SourceId, Is.EqualTo(30));
            SourceUiButton[] disciplines = _view.GetComponentsInChildren<SourceUiButton>(true)
                .Where(value => Enum.TryParse(value.name, out TechnologyDiscipline _)).ToArray();
            Assert.That(disciplines, Has.Length.EqualTo(8));
            for (int index = 0; index < disciplines.Length; index++)
            {
                SourceUiButton button = disciplines.Single(value =>
                    value.name == ((TechnologyDiscipline)index).ToString());
                Assert.That(button.CurrentPresentationKey.SourceId, Is.EqualTo(306 + index));
                RectTransform rect = (RectTransform)button.transform;
                Assert.That(rect.anchoredPosition,
                    Is.EqualTo(new Vector2(RetailCharacterSheetLayout.TechnologyPositions[index].x,
                        -RetailCharacterSheetLayout.TechnologyPositions[index].y)));
            }

            SourceUiBitmapText[] text = _view.GetComponentsInChildren<SourceUiBitmapText>(true);
            Assert.That(text.Single(value => value.name == "Technology Discipline Icon").FontSourceId,
                Is.EqualTo(300));
            Assert.That(text.Single(value => value.name == "Technology Name").Text,
                Is.EqualTo("Herbology Novice"));
            Assert.That(text.Single(value => value.name == "Technology Name").FontSourceId, Is.EqualTo(301));
            Assert.That(text.Single(value => value.name == "Technology Expertise").Text, Is.EqualTo("10"));
            Assert.That(text.Single(value => value.name == "Technology Expertise").FontSourceId, Is.EqualTo(229));
            for (int degree = 1; degree <= 7; degree++)
            {
                Assert.That(text.Single(value => value.name == $"Schematic {degree}").Text,
                    Is.EqualTo($"Herbology Schematic {degree}"));
                Assert.That(text.Single(value => value.name == $"Schematic {degree}").FontSourceId, Is.EqualTo(301));
                Assert.That(text.Single(value => value.name == $"Schematic Components {degree}").FontSourceId,
                    Is.EqualTo(483));
            }
            Assert.That(_view.GetComponentsInChildren<SourceUiImage>(true)
                .Single(value => value.name == "Degree 1 Icon").Key.SourceId, Is.EqualTo(653));
        }

        [Test]
        public void RuntimeTechnologyHoverUsesSourceDegreeHelpAndIntelligenceRequirement()
        {
            Assert.That(_controller.Open(GameUiScreen.Technology), Is.True);
            _view.SelectTechnology(TechnologyDiscipline.Herbology);
            _view.Synchronize();
            SourceHoverHintTarget degree = _view.GetComponentsInChildren<SourceHoverHintTarget>(true)
                .Single(value => value.name == "Schematic 1");
            degree.OnPointerEnter(null);
            Assert.That(_controller.ContextualHelpTitle, Is.Not.Empty);
            Assert.That(_controller.ContextualHelp, Does.StartWith("Minimum IN: 5"));
            degree.OnPointerExit(null);
            Assert.That(_controller.ContextualHelp, Is.Empty);
        }

        [Test]
        public void RuntimeTechnologyAdvancementUsesAuthorityAndAcceptedBaseline()
        {
            Assert.That(_controller.Open(GameUiScreen.Technology), Is.True);
            int points = _session.Progression.GetUnspentCharacterPoints(_pc);
            Assert.That(_controller.AdjustTechnologyDiscipline(TechnologyDiscipline.Herbology, 1), Is.True);
            Assert.That(_session.Technology.GetLearnedDegree(_pc, TechnologyDiscipline.Herbology),
                Is.EqualTo(TechnologyDegree.Novice));
            Assert.That(_session.Progression.GetUnspentCharacterPoints(_pc), Is.EqualTo(points - 1));
            Assert.That(_controller.AdjustTechnologyDiscipline(TechnologyDiscipline.Herbology, -1), Is.True);
            Assert.That(_session.Technology.GetLearnedDegree(_pc, TechnologyDiscipline.Herbology),
                Is.EqualTo(TechnologyDegree.Layman));
            Assert.That(_session.Progression.GetUnspentCharacterPoints(_pc), Is.EqualTo(points));

            Assert.That(_controller.AdjustTechnologyDiscipline(TechnologyDiscipline.Herbology, 1), Is.True);
            _controller.Close();
            Assert.That(_controller.Open(GameUiScreen.Technology), Is.True);
            int acceptedPoints = _session.Progression.GetUnspentCharacterPoints(_pc);
            Assert.That(_controller.AdjustTechnologyDiscipline(TechnologyDiscipline.Herbology, -1), Is.False);
            Assert.That(_controller.Feedback,
                Is.EqualTo(nameof(RuntimeCharacterAdjustmentResult.AcceptedBaseline)));
            Assert.That(_session.Technology.GetLearnedDegree(_pc, TechnologyDiscipline.Herbology),
                Is.EqualTo(TechnologyDegree.Novice));
            Assert.That(_session.Progression.GetUnspentCharacterPoints(_pc), Is.EqualTo(acceptedPoints));
        }

        [Test]
        public void SkillsHudRouteTogglesWithoutCreatingModalAuthority()
        {
            _controller.Close();
            Assert.That(_controller.ToggleSkillsPanel(), Is.True);
            Assert.That(_controller.Screen, Is.EqualTo(GameUiScreen.None));
            Assert.That(_controller.HudPanel, Is.EqualTo(GameUiHudPanel.Skills));
            Assert.That(_controller.ToggleSkillsPanel(), Is.True);
            Assert.That(_controller.HudPanel, Is.EqualTo(GameUiHudPanel.None));
        }

        [TestCase(0, "Prowling")]
        [TestCase(1, "Pick Pocket")]
        [TestCase(2, "Disarm Traps")]
        [TestCase(3, "Repair")]
        public void UnsupportedSkillActionsFailClosedWithoutCharacterMutation(int sourceIndex, string name)
        {
            _controller.Close();
            int[] attributes = _session.Characters.Get(_pc).BaseAttributes.ToArray();
            int points = _session.Progression.GetUnspentCharacterPoints(_pc);
            Assert.That(_controller.ActivateSourceSkillAction(sourceIndex), Is.False);
            Assert.That(_controller.Feedback, Does.Contain(name));
            Assert.That(_session.Characters.Get(_pc).BaseAttributes.ToArray(), Is.EqualTo(attributes));
            Assert.That(_session.Progression.GetUnspentCharacterPoints(_pc), Is.EqualTo(points));
        }

        [Test]
        public void OriginalEnhancedOriginalRebuildPreservesAuthorityAndSelectedGroup()
        {
            _view.SelectSkillGroup(2);
            Assert.That(_controller.AdjustAttribute(CharacterAttribute.Intelligence, 1), Is.True);
            OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Enhanced);
            _skin.InvalidatePresentation();
            _view.Synchronize();
            OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Original);
            _skin.InvalidatePresentation();
            _view.Synchronize();
            Assert.That(_view.SkillGroup, Is.EqualTo(2));
            Assert.That(_session.Characters.GetBaseAttribute(_pc, CharacterAttribute.Intelligence), Is.EqualTo(9));
            Assert.That(_view.LastProjection.Attributes.Single(value =>
                value.Attribute == CharacterAttribute.Intelligence).Base, Is.EqualTo(9));
        }

        [Test]
        public void ClosingHidesOnlyPresentationAndKeepsCommittedValues()
        {
            Assert.That(_controller.AdjustAttribute(CharacterAttribute.Charisma, 1), Is.True);
            _controller.Close();
            _view.Synchronize();
            Assert.That(_view.IsVisible, Is.False);
            Assert.That(_session.Characters.GetBaseAttribute(_pc, CharacterAttribute.Charisma), Is.EqualTo(9));
        }

        private sealed class EmptySlots : ISessionSaveSlotOperations
        {
            public SessionSaveSlotResult SaveSlot(string slotId) => new(SessionSaveSlotFailure.None);
            public SessionSaveSlotResult LoadSlot(string slotId) => new(SessionSaveSlotFailure.None);
            public SessionSaveSlotListResult ListSlots()
                => new(SessionSaveSlotFailure.None, Array.Empty<SessionSaveSlotInfo>());
            public SessionSaveSlotResult DeleteSlot(string slotId) => new(SessionSaveSlotFailure.None);
        }

        private static SpellPresentationCatalog CreateSpellText()
        {
            var rows = new List<KeyValuePair<int, string>>();
            for (int id = 0; id < 80; id++)
            {
                rows.Add(new KeyValuePair<int, string>(id, $"Source Spell {id}"));
                rows.Add(new KeyValuePair<int, string>(700 + id, $"Spell Description {id}"));
            }
            for (int id = 0; id < 16; id++)
            {
                rows.Add(new KeyValuePair<int, string>(500 + id,
                    id == (int)SpellCollege.Earth ? "Earth Source" : $"College {id}"));
                rows.Add(new KeyValuePair<int, string>(8000 + id, $"College Description {id}"));
            }
            return SpellPresentationCatalog.FromMes(new MesFile(rows));
        }

        private static SchematicCatalog CreateSchematicCatalog()
        {
            var rules = new List<KeyValuePair<int, string>>();
            var text = new List<KeyValuePair<int, string>>();
            for (int discipline = 0; discipline < 8; discipline++)
            for (int degree = 1; degree <= 7; degree++)
            {
                int id = 1990 + 200 * discipline + 10 * degree;
                int name = id + 10000;
                int description = id + 11000;
                rules.Add(new KeyValuePair<int, string>(id, name.ToString()));
                rules.Add(new KeyValuePair<int, string>(id + 1, description.ToString()));
                rules.Add(new KeyValuePair<int, string>(id + 2, (700 + degree).ToString()));
                rules.Add(new KeyValuePair<int, string>(id + 3, (6000 + degree).ToString()));
                rules.Add(new KeyValuePair<int, string>(id + 4, (6100 + degree).ToString()));
                rules.Add(new KeyValuePair<int, string>(id + 5, (6200 + degree).ToString()));
                rules.Add(new KeyValuePair<int, string>(id + 6, "1"));
                text.Add(new KeyValuePair<int, string>(name,
                    $"{FriendlyDiscipline((TechnologyDiscipline)discipline)} Schematic {degree}"));
                text.Add(new KeyValuePair<int, string>(description,
                    $"Source technology description {discipline}:{degree}"));
            }
            return SchematicCatalog.FromMes(new MesFile(rules), new MesFile(text));
        }

        private static string FriendlyDiscipline(TechnologyDiscipline value)
            => value == TechnologyDiscipline.GunSmithy ? "Gun Smithy" : value.ToString();

        private sealed class Owner : ISectorPresentationOwner
        {
            private readonly WorldMapSessionCoordinator _session;
            public Owner(WorldMapSessionCoordinator session) => _session = session;
            public string ConfiguredSector => Sector;
            public string PresentedSector { get; private set; }
            public bool IsSectorPresented => PresentedSector != null;
            public bool PresentSector(string path)
            { PresentedSector = WorldMapSessionCoordinator.NormalizeSector(path); _session.BeginSector(PresentedSector); return true; }
            public void ClearPresentedSector()
            { string path = PresentedSector; PresentedSector = null; if (path != null) _session.UnloadSector(path); }
        }
    }
}
