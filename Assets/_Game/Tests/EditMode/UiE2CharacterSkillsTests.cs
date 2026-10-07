using System;
using System.IO;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Save;
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
            Assert.That(_view.GetComponentsInChildren<ScrollRect>(true), Is.Empty);
            Assert.That(Object.FindObjectsByType<RetailCharacterSheetView>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
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
