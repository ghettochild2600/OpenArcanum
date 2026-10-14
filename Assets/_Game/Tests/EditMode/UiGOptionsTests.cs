using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using NUnit.Framework;
using OpenArcanum.Rendering;
using OpenArcanum.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("UiGOptions")]
    public sealed class UiGOptionsTests
    {
        private sealed class MemoryStore : IRetailOptionsStore
        {
            public readonly Dictionary<string, int> Values = new();
            public int SaveCount { get; private set; }
            public int Read(string key, int fallback) => Values.TryGetValue(key, out int value) ? value : fallback;
            public void Write(string key, int value) => Values[key] = value;
            public void Save() => SaveCount++;
        }

        private static RetailUiAssetResolver _retail;
        private string _fixtureRoot;
        private EnhancedUiAssetResolver _enhanced;
        private UiSkinResolver _skin;
        private GameObject _root;

        [OneTimeSetUp]
        public void OneTimeSetUp() => _retail = RetailUiAssetResolver.CreateProduction();

        [OneTimeTearDown]
        public void OneTimeTearDown() { _retail?.Dispose(); _retail = null; }

        [SetUp]
        public void SetUp()
        {
            _fixtureRoot = Path.Combine(Path.GetTempPath(), "OpenArcanum-UiG-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_fixtureRoot);
            _enhanced = new EnhancedUiAssetResolver(_fixtureRoot, _ => { });
            _skin = new UiSkinResolver(_retail, _enhanced);
            _root = new GameObject(nameof(UiGOptionsTests));
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            EventSystem events = Object.FindFirstObjectByType<EventSystem>();
            if (events != null && events.name == "OpenArcanum UI EventSystem") Object.DestroyImmediate(events.gameObject);
            _skin?.Dispose();
            _enhanced?.Dispose();
            OpenArcanumGraphicsSettings.ClearRuntimeMode();
            if (Directory.Exists(_fixtureRoot)) Directory.Delete(_fixtureRoot, true);
        }

        [Test]
        public void SourceOptionsUsesFullScreenBackgroundLensAndCyclicArt()
        {
            Assert.That(_retail.TryResolve(new UiAssetKey(RetailOptionsLayout.BackgroundSourceId),
                out UiResolvedAsset background), Is.True);
            Assert.That(background.LogicalSize, Is.EqualTo(new Vector2Int(800, 600)));
            foreach (int id in new[] { 670, 671, 688, 667, 668, 623, 27, 229, 327 })
                Assert.That(_retail.TryResolve(new UiAssetKey(id), out _), Is.True, $"source ART {id}");
            Assert.That(RetailOptionsLayout.PcLens, Is.EqualTo(new Rect(84, 67, 89, 89)));
            Assert.That(RetailOptionsLayout.ScreenRect(800, 600), Is.EqualTo(new Rect(0, 0, 800, 600)));
        }

        [Test]
        public void SourcePagesAndExactControlSlotsMatchOptionsUiC()
        {
            Assert.That(RetailOptionsLayout.TabTextPositions, Is.EqualTo(new[]
            {
                new Vector2(130, 221), new Vector2(130, 261), new Vector2(130, 301),
                new Vector2(130, 341),
            }));
            Assert.That(RetailOptionsLayout.ForTab(RetailOptionsTab.Game).Select(value => value.Id),
                Is.EqualTo(new[]
                {
                    RetailOptionId.Difficulty, RetailOptionId.ViolenceFilter,
                    RetailOptionId.DefaultCombatMode, RetailOptionId.AutoAttack,
                    RetailOptionId.AutoSwitchWeapons, RetailOptionId.AlwaysRun,
                    RetailOptionId.FollowerSkills,
                }));
            Assert.That(RetailOptionsLayout.ForTab(RetailOptionsTab.Video).Select(value => value.Id),
                Is.EqualTo(new[]
                {
                    RetailOptionId.Brightness, RetailOptionId.TextDuration, RetailOptionId.Floats,
                    RetailOptionId.FloatSpeed, RetailOptionId.CombatTaunts,
                }));
            Assert.That(RetailOptionsLayout.ForTab(RetailOptionsTab.Sound).Select(value => value.Id),
                Is.EqualTo(new[]
                {
                    RetailOptionId.EffectsVolume, RetailOptionId.VoiceVolume, RetailOptionId.MusicVolume,
                }));
            Assert.That(RetailOptionsLayout.ForTab(RetailOptionsTab.Game).Select(value => value.Position),
                Is.EqualTo(new[]
                {
                    new Vector2(261, 197), new Vector2(261, 272), new Vector2(261, 347),
                    new Vector2(531, 122), new Vector2(531, 197), new Vector2(531, 272),
                    new Vector2(531, 347),
                }));
        }

        [Test]
        public void SourceMessageTablesOwnEveryPageTitleControlTitleAndChoiceValue()
        {
            foreach ((int key, string expected) in new[]
                     { (4000, "Game"), (4001, "Video"), (4002, "Sound"), (4003, "Done") })
            {
                Assert.That(_retail.TryReadMessage("mes/mainmenu.mes", key, out string value), Is.True);
                Assert.That(value, Is.EqualTo(expected));
            }
            foreach (RetailOptionDefinition definition in RetailOptionsLayout.All)
            {
                Assert.That(_retail.TryReadMessage("mes/options_ui.mes", definition.TitleMessageKey,
                    out string title), Is.True, definition.Id.ToString());
                Assert.That(title, Is.Not.Empty);
                if (definition.Kind == RetailOptionKind.Choice)
                    for (int value = 0; value <= definition.Maximum; value++)
                        Assert.That(_retail.TryReadMessage(definition.ValueMessagePath, value, out _), Is.True,
                            $"{definition.Id} value {value}");
            }
        }

        [Test]
        public void DefaultsMatchRetailConfigurationAndRemainInSourceRanges()
        {
            var model = new RetailOptionsModel(new MemoryStore());
            Assert.That(model.Value(RetailOptionId.Difficulty), Is.EqualTo(1));
            Assert.That(model.Value(RetailOptionId.DefaultCombatMode), Is.Zero);
            Assert.That(model.Value(RetailOptionId.AutoAttack), Is.EqualTo(1));
            Assert.That(model.Value(RetailOptionId.AlwaysRun), Is.Zero);
            Assert.That(model.Value(RetailOptionId.FollowerSkills), Is.EqualTo(1));
            Assert.That(model.Value(RetailOptionId.TextDuration), Is.EqualTo(6));
            Assert.That(model.Value(RetailOptionId.Floats), Is.EqualTo(1));
            Assert.That(model.Value(RetailOptionId.FloatSpeed), Is.EqualTo(2));
            Assert.That(model.Value(RetailOptionId.EffectsVolume), Is.EqualTo(5));
            Assert.That(model.Value(RetailOptionId.VoiceVolume), Is.EqualTo(5));
            Assert.That(model.Value(RetailOptionId.MusicVolume), Is.EqualTo(5));
            foreach (RetailOptionDefinition definition in RetailOptionsLayout.All)
                Assert.That(model.Value(definition.Id), Is.InRange(0, definition.Maximum));
        }

        [Test]
        public void ChoiceControlsWrapNumericControlsClampAndEveryPressPersistsImmediately()
        {
            var store = new MemoryStore();
            var changes = new List<(RetailOptionId Id, int Value)>();
            var model = new RetailOptionsModel(store, (id, value) => changes.Add((id, value)));
            changes.Clear();

            model.Set(RetailOptionId.Floats, 0);
            model.Cycle(RetailOptionId.Floats, -1);
            Assert.That(model.Value(RetailOptionId.Floats), Is.EqualTo(2));
            model.Cycle(RetailOptionId.Floats, 1);
            Assert.That(model.Value(RetailOptionId.Floats), Is.Zero);

            model.Set(RetailOptionId.Brightness, 0);
            model.Cycle(RetailOptionId.Brightness, -1);
            Assert.That(model.Value(RetailOptionId.Brightness), Is.Zero);
            model.Set(RetailOptionId.Brightness, 9);
            model.Cycle(RetailOptionId.Brightness, 1);
            Assert.That(model.Value(RetailOptionId.Brightness), Is.EqualTo(9));
            Assert.That(store.SaveCount, Is.GreaterThanOrEqualTo(4));
            Assert.That(changes, Does.Contain((RetailOptionId.Floats, 2)));
        }

        [Test]
        public void StoredValuesSurviveModelRecreationAndApplyToExistingAlwaysRunAuthority()
        {
            var store = new MemoryStore();
            var keyboard = new ProductionKeyboardController();
            var first = new RetailOptionsModel(store, (id, value) =>
            {
                if (id == RetailOptionId.AlwaysRun) keyboard.SetAlwaysRun(value != 0);
            });
            first.Set(RetailOptionId.AlwaysRun, 1);
            first.Set(RetailOptionId.MusicVolume, 8);
            Assert.That(keyboard.AlwaysRun, Is.True);

            var second = new RetailOptionsModel(store);
            Assert.That(second.Value(RetailOptionId.AlwaysRun), Is.EqualTo(1));
            Assert.That(second.Value(RetailOptionId.MusicVolume), Is.EqualTo(8));
        }

        [Test]
        public void SourceOptionsDoesNotFabricateRemappingApplyCancelOrDefaultsPages()
        {
            var model = new RetailOptionsModel(new MemoryStore());
            Assert.That(model.HasKeyRemapping, Is.False);
            Assert.That(model.HasApply, Is.False);
            Assert.That(model.HasCancel, Is.False);
            Assert.That(model.HasDefaults, Is.False);
            Assert.That(Enum.GetValues(typeof(RetailOptionsTab)),
                Is.EqualTo(new[] { RetailOptionsTab.Game, RetailOptionsTab.Video, RetailOptionsTab.Sound }));
        }

        [Test]
        public void ViewUsesSourceWindowTabsAndSevenInPlayGameControls()
        {
            WorldMapSessionCoordinator session = _root.AddComponent<WorldMapSessionCoordinator>();
            var controller = new GameUiController(session);
            var keyboard = new ProductionKeyboardController();
            RetailOptionsView view = _root.AddComponent<RetailOptionsView>();
            view.Bind(controller, session, _skin, keyboard, null, new MemoryStore());
            Assert.That(controller.Open(GameUiScreen.Options), Is.True);
            view.Synchronize();

            Assert.That(view.IsVisible, Is.True);
            Assert.That(view.WindowTransform.sizeDelta, Is.EqualTo(new Vector2(800, 600)));
            Assert.That(view.GetComponentsInChildren<SourceUiImage>(true)
                .Any(value => value.Key.SourceId == RetailOptionsLayout.BackgroundSourceId), Is.True);
            Assert.That(view.GetComponentsInChildren<SourceUiButton>(true)
                .Count(value => value.name.EndsWith(" Left") || value.name.EndsWith(" Right")), Is.EqualTo(14));
            Assert.That(view.GetComponentsInChildren<SourceUiBitmapText>(true)
                .Count(value => value.name.EndsWith(" Title")), Is.EqualTo(7));
            RetailOptionsTabView[] tabs = view.GetComponentsInChildren<RetailOptionsTabView>(true);
            Assert.That(tabs.Length, Is.EqualTo(4));
            Assert.That(tabs.Select(value => value.SourceRect.center.x), Is.All.EqualTo(130f).Within(.01f));
            Assert.That(tabs.Select(value => value.SourceRect.y), Is.EqualTo(new[] { 221f, 261f, 301f, 341f }));
            Assert.That(tabs, Is.All.Matches<RetailOptionsTabView>(value =>
                value.GetComponentsInChildren<SourceUiBitmapText>(true).Length == 3));

            var eventObject = new GameObject("Options Pointer Events", typeof(EventSystem));
            eventObject.transform.SetParent(_root.transform);
            var pointer = new PointerEventData(eventObject.GetComponent<EventSystem>())
            {
                button = PointerEventData.InputButton.Left,
            };
            RetailOptionsTabView sound = tabs.Single(value => value.name == "Sound");
            ExecuteEvents.Execute(sound.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Assert.That(view.Model.Tab, Is.EqualTo(RetailOptionsTab.Sound));
        }

        [Test]
        public void SourceTabAndChoiceTintsSurviveOptionsCanvasReactivation()
        {
            WorldMapSessionCoordinator session = _root.AddComponent<WorldMapSessionCoordinator>();
            var controller = new GameUiController(session);
            var keyboard = new ProductionKeyboardController();
            RetailOptionsView view = _root.AddComponent<RetailOptionsView>();
            view.Bind(controller, session, _skin, keyboard, null, new MemoryStore());
            Assert.That(controller.Open(GameUiScreen.Options), Is.True);
            view.Synchronize();

            SourceUiBitmapText tab = view.GetComponentsInChildren<SourceUiBitmapText>(true)
                .Single(value => value.name == "Red Pass" && value.Text == "Game");
            SourceUiBitmapText choice = view.GetComponentsInChildren<SourceUiBitmapText>(true)
                .Single(value => value.name == "Difficulty Value");
            AssertGlyphTint(tab, new Color32(100, 0, 0, 255));
            AssertGlyphTint(choice, new Color32(240, 15, 15, 255));

            controller.Close();
            view.Synchronize();
            Assert.That(controller.Open(GameUiScreen.Options), Is.True);
            view.Synchronize();
            AssertGlyphTint(tab, new Color32(100, 0, 0, 255));
            AssertGlyphTint(choice, new Color32(240, 15, 15, 255));
        }

        private static void AssertGlyphTint(SourceUiBitmapText text, Color32 expected)
        {
            Image[] glyphs = text.GetComponentsInChildren<Image>(true);
            Assert.That(glyphs, Is.Not.Empty, text.name);
            Assert.That(glyphs.Select(value => (Color32)value.color), Has.All.EqualTo(expected), text.name);
        }
    }
}
