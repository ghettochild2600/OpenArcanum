using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using Arcanum.Runtime.Creation;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using NUnit.Framework;
using OpenArcanum.Rendering;
using OpenArcanum.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("UIBMainMenu")]
    public sealed class UiBMainMenuTests
    {
        private static RetailUiAssetResolver _retail;
        private string _fixtureRoot;
        private EnhancedUiAssetResolver _enhanced;
        private UiSkinResolver _skin;
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private GameUiController _controller;
        private RetailMainMenuSource _source;

        [OneTimeSetUp]
        public void OneTimeSetUp() => _retail = RetailUiAssetResolver.CreateProduction();

        [OneTimeTearDown]
        public void OneTimeTearDown() { _retail?.Dispose(); _retail = null; }

        [SetUp]
        public void SetUp()
        {
            _fixtureRoot = Path.Combine(Path.GetTempPath(), "OpenArcanum-UIB-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_fixtureRoot);
            _enhanced = new EnhancedUiAssetResolver(_fixtureRoot, _ => { });
            _skin = new UiSkinResolver(_retail, _enhanced);
            _root = new GameObject(nameof(UiBMainMenuTests));
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.BindCharacterCreationSource(CreateCatalog());
            _controller = new GameUiController(_session, new EmptySlots());
            _source = RetailMainMenuSource.Read(_skin);
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
            if (Directory.Exists(_fixtureRoot)) Directory.Delete(_fixtureRoot, recursive: true);
        }

        [Test]
        public void RetailSourceResolvesMainSinglePlayerFontAndCursorAssets()
        {
            Assert.That(_retail.TryResolve(new UiAssetKey(329), out UiResolvedAsset main), Is.True);
            Assert.That(main.LogicalSize, Is.EqualTo(new Vector2Int(800, 600)));
            Assert.That(_retail.TryResolve(new UiAssetKey(331), out UiResolvedAsset single), Is.True);
            Assert.That(single.LogicalSize, Is.EqualTo(new Vector2Int(800, 600)));
            Assert.That(_retail.TryResolve(new UiAssetKey(327, frame: 'A' - 31), out _), Is.True);
            Assert.That(_retail.TryResolve(new UiAssetKey(0), out UiResolvedAsset cursor), Is.True);
            Assert.That(cursor.LogicalSize, Is.EqualTo(new Vector2Int(24, 27)));
        }

        [Test]
        public void RetailMessageRowsProvideSourceMenuHierarchy()
        {
            Assert.That(_source.IsSourceBacked(460), Is.True);
            Assert.That(_source.Get(460, null), Is.EqualTo("Single Player"));
            Assert.That(_source.Get(50, null), Is.EqualTo("New Game"));
            Assert.That(_source.Get(420, null), Is.EqualTo("Pick Character"));
            Assert.That(_source.Get(421, null), Is.EqualTo("New Character"));
            Assert.That(_source.Get(5100, null), Is.EqualTo("Are you sure you want to quit?"));
        }

        [Test]
        public void TopLevelContainsExactRequestedRetailEntries()
        {
            RetailMainMenuModel model = Model();
            Assert.That(model.Entries.Select(entry => entry.Label), Is.EqualTo(new[]
                { "Single Player", "Multiplayer", "Options", "Credits", "Exit Game" }));
            Assert.That(model.BackgroundSourceId, Is.EqualTo(329));
        }

        [Test]
        public void SinglePlayerUsesSourceBackgroundAndAuthenticFiveEntryFlow()
        {
            RetailMainMenuModel model = Model();
            model.Submit(RetailMainMenuCommand.SinglePlayer);
            Assert.That(model.State, Is.EqualTo(RetailMainMenuState.SinglePlayer));
            Assert.That(model.BackgroundSourceId, Is.EqualTo(331));
            Assert.That(model.Entries.Select(entry => entry.Label), Is.EqualTo(new[]
                { "New Game", "Load Game", "Last Save", "View Intro", "Cancel" }));
        }

        [Test]
        public void NewGameExposesSourceChoiceBeforeExistingCreationAuthority()
        {
            RetailMainMenuModel model = Model();
            model.Submit(RetailMainMenuCommand.SinglePlayer);
            model.Submit(RetailMainMenuCommand.NewGame);
            Assert.That(model.State, Is.EqualTo(RetailMainMenuState.NewGameChoice));
            Assert.That(model.Entries.Select(entry => entry.Label), Is.EqualTo(new[]
                { "Pick Character", "New Character", "Cancel" }));
            model.Submit(RetailMainMenuCommand.NewCharacter);
            Assert.That(_controller.Screen, Is.EqualTo(GameUiScreen.CharacterCreation));
            Assert.That(_controller.CreationDraft, Is.Not.Null);
            Assert.That(_session.PlayerState, Is.Null);
        }

        [Test]
        public void LoadAndLastSaveReuseExistingSaveLoadAuthority()
        {
            foreach (RetailMainMenuCommand command in new[]
                     { RetailMainMenuCommand.LoadGame, RetailMainMenuCommand.LastSave })
            {
                _controller.Open(GameUiScreen.MainMenu);
                RetailMainMenuModel model = Model();
                model.Submit(command);
                Assert.That(_controller.Screen, Is.EqualTo(GameUiScreen.SaveLoad));
                Assert.That(_controller.SaveLoad.Mode, Is.EqualTo(SaveLoadPanelMode.Load));
            }
        }

        [Test]
        public void OptionsIsLegalBeforePlayerCreationAndUsesExistingScreen()
        {
            RetailMainMenuModel model = Model();
            model.Submit(RetailMainMenuCommand.Options);
            Assert.That(_controller.Screen, Is.EqualTo(GameUiScreen.Options));
            Assert.That(_session.PlayerState, Is.Null);
        }

        [TestCase(RetailMainMenuCommand.Multiplayer, "MULTIPLAYER")]
        [TestCase(RetailMainMenuCommand.Credits, "CREDITS")]
        [TestCase(RetailMainMenuCommand.ViewIntro, "VIEW INTRO")]
        [TestCase(RetailMainMenuCommand.PickCharacter, "PICK CHARACTER")]
        public void UnsupportedRetailBranchesFailClosedAsBoundedNotices(
            RetailMainMenuCommand command, string title)
        {
            RetailMainMenuModel model = Model();
            object player = _session.PlayerState;
            int count = _session.States.Count;
            model.Submit(command);
            Assert.That(model.State, Is.EqualTo(RetailMainMenuState.Notice));
            Assert.That(model.NoticeTitle, Is.EqualTo(title));
            Assert.That(_session.PlayerState, Is.SameAs(player));
            Assert.That(_session.States.Count, Is.EqualTo(count));
            Assert.That(_controller.Screen, Is.EqualTo(GameUiScreen.MainMenu));
        }

        [Test]
        public void ExitRequiresConfirmationAndInvokesBoundQuitExactlyOnce()
        {
            int quits = 0;
            RetailMainMenuModel model = Model(() => quits++);
            model.Submit(RetailMainMenuCommand.ExitGame);
            Assert.That(model.State, Is.EqualTo(RetailMainMenuState.QuitConfirmation));
            Assert.That(quits, Is.Zero);
            model.Submit(RetailMainMenuCommand.CancelQuit);
            Assert.That(model.State, Is.EqualTo(RetailMainMenuState.TopLevel));
            model.Submit(RetailMainMenuCommand.ExitGame);
            model.Submit(RetailMainMenuCommand.ConfirmQuit);
            Assert.That(quits, Is.EqualTo(1));
        }

        [Test]
        public void EscapeReturnsSubmenusAndNoticesWithoutTouchingControllerAuthority()
        {
            RetailMainMenuModel model = Model();
            model.Submit(RetailMainMenuCommand.SinglePlayer);
            model.Escape();
            Assert.That(model.State, Is.EqualTo(RetailMainMenuState.TopLevel));
            model.Submit(RetailMainMenuCommand.Credits);
            model.Escape();
            Assert.That(model.State, Is.EqualTo(RetailMainMenuState.TopLevel));
            Assert.That(_controller.Screen, Is.EqualTo(GameUiScreen.MainMenu));
        }

        [Test]
        public void ViewBuildsOneSourcePresenterAtAuthenticLogicalRows()
        {
            RetailMainMenuView view = _root.AddComponent<RetailMainMenuView>();
            view.Bind(_controller, () => { }, _skin);
            view.Synchronize();
            Assert.That(view.IsVisible, Is.True);
            Assert.That(view.Buttons.Count, Is.EqualTo(5));
            Assert.That(view.Buttons.Select(value => value.SourceRect.center.x), Has.All.EqualTo(410f).Within(.001f));
            Assert.That(view.Buttons.Select(value => value.SourceRect.y),
                Is.EqualTo(new[] { 143f, 193f, 243f, 293f, 343f }));
            Assert.That(Object.FindObjectsByType<RetailMainMenuView>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
        }

        [Test]
        public void HoverPressedAndSelectedStatesKeepExactHitGeometry()
        {
            RetailMainMenuView view = _root.AddComponent<RetailMainMenuView>();
            view.Bind(_controller, () => { }, _skin);
            RetailMainMenuButtonView button = view.Buttons[0];
            Rect before = button.SourceRect;
            button.OnPointerEnter(null);
            Assert.That(button.State, Is.EqualTo(RetailMainMenuButtonState.Hover));
            button.OnPointerDown(null);
            Assert.That(button.State, Is.EqualTo(RetailMainMenuButtonState.Pressed));
            button.OnPointerUp(null);
            button.OnSelect(null);
            Assert.That(button.State, Is.EqualTo(RetailMainMenuButtonState.Selected));
            Assert.That(button.SourceRect, Is.EqualTo(before));
        }

        [Test]
        public void EnhancedMainBackgroundLoadsAtExactFourTimesWithoutGeometryChange()
        {
            UiResolvedAsset original = Original(329);
            WriteFixture(original, original.LogicalSize * 4);
            Assert.That(_skin.TryResolve(original.Key, UiAssetSkin.Enhanced, out UiResolvedAsset enhanced), Is.True);
            Assert.That(enhanced.ResolvedSkin, Is.EqualTo(UiAssetSkin.Enhanced));
            Assert.That(enhanced.Texture.width, Is.EqualTo(3200));
            Assert.That(enhanced.Texture.height, Is.EqualTo(2400));
            Assert.That(enhanced.LogicalSize, Is.EqualTo(new Vector2Int(800, 600)));
        }

        [Test]
        public void MissingEnhancedGlyphFallsBackPerFrame()
        {
            var key = new UiAssetKey(327, frame: 'S' - 31);
            Assert.That(_skin.TryResolve(key, UiAssetSkin.Enhanced, out UiResolvedAsset result), Is.True);
            Assert.That(result.IsFallback, Is.True);
            Assert.That(result.ResolvedSkin, Is.EqualTo(UiAssetSkin.Original));
        }

        [Test]
        public void InvalidEnhancedMainBackgroundFallsBackSafely()
        {
            UiResolvedAsset original = Original(329);
            WriteFixture(original, new Vector2Int(3199, 2400));
            Assert.That(_skin.TryResolve(original.Key, UiAssetSkin.Enhanced, out UiResolvedAsset result), Is.True);
            Assert.That(result.IsFallback, Is.True);
            Assert.That(result.ResolvedSkin, Is.EqualTo(UiAssetSkin.Original));
        }

        [Test]
        public void SkinSwitchPreservesMenuStateAndButtonGeometry()
        {
            UiResolvedAsset original = Original(329);
            WriteFixture(original, original.LogicalSize * 4);
            RetailMainMenuView view = _root.AddComponent<RetailMainMenuView>();
            view.Bind(_controller, () => { }, _skin);
            view.Model.Submit(RetailMainMenuCommand.SinglePlayer);
            Rect[] before = view.Buttons.Select(value => value.SourceRect).ToArray();
            OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Enhanced);
            OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Original);
            Assert.That(view.Model.State, Is.EqualTo(RetailMainMenuState.SinglePlayer));
            Assert.That(view.Buttons.Select(value => value.SourceRect), Is.EqualTo(before));
            Assert.That(_controller.Screen, Is.EqualTo(GameUiScreen.MainMenu));
        }

        [TestCase(1920, 1080, 1.8f, 240f)]
        [TestCase(2560, 1440, 2.4f, 320f)]
        [TestCase(3840, 2160, 3.6f, 480f)]
        public void MainMenuRemainsCenteredAndUnstretchedAtModernResolutions(
            int width, int height, float scale, float originX)
        {
            UiLogicalMapping mapping = UiLogicalMapping.ForResolution(width, height);
            Rect composition = mapping.LogicalToScreen(new Rect(0f, 0f, 800f, 600f));
            Assert.That(mapping.Scale, Is.EqualTo(scale).Within(.0001f));
            Assert.That(composition.x, Is.EqualTo(originX).Within(.0001f));
            Assert.That(composition.width / composition.height, Is.EqualTo(4f / 3f).Within(.0001f));
            Rect button = mapping.LogicalToScreen(new Rect(350f, 143f, 120f, 33f));
            Assert.That(mapping.ScreenToLogical(button.position), Is.EqualTo(new Vector2(350f, 143f)));
        }

        private RetailMainMenuModel Model(Action quit = null)
            => new RetailMainMenuModel(_controller, _source, quit ?? (() => { }));

        private UiResolvedAsset Original(int sourceId)
        {
            Assert.That(_retail.TryResolve(new UiAssetKey(sourceId), out UiResolvedAsset value), Is.True);
            return value;
        }

        private void WriteFixture(UiResolvedAsset original, Vector2Int size)
        {
            string path = _enhanced.GetReplacementPath(original);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var texture = new Texture2D(size.x, size.y, TextureFormat.RGBA32, mipChain: false);
            try
            {
                Color32[] pixels = Enumerable.Repeat(new Color32(172, 36, 80, 255), size.x * size.y).ToArray();
                pixels[0] = new Color32(0, 0, 0, 0);
                texture.SetPixels32(pixels);
                texture.Apply(false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(texture); }
        }

        private static CharacterCreationCatalog CreateCatalog()
        {
            MesFile rules = Mes((0, "1000"), (1, "86"), (2, ""), (3, "400"), (4, ""));
            MesFile text = Mes((1000, "No significant background\n\nNothing happened."));
            MesFile effects = Mes((86, ""));
            MesFile portraits = Mes((1005, "HUM1"));
            MapList maps = MapList.Read(Mes((5000,
                "Arcanum1-024-fixed, 92958, 82592, Type: START_MAP, WorldMap: 0")));
            return CharacterCreationCatalog.FromMes(rules, text, effects, portraits, maps);
        }

        private static MesFile Mes(params (int key, string value)[] values)
            => new MesFile(values.Select(value => new KeyValuePair<int, string>(value.key, value.value)).ToList());

        private sealed class EmptySlots : ISessionSaveSlotOperations
        {
            public SessionSaveSlotResult SaveSlot(string slotId) => new SessionSaveSlotResult(SessionSaveSlotFailure.None);
            public SessionSaveSlotResult LoadSlot(string slotId) => new SessionSaveSlotResult(SessionSaveSlotFailure.None);
            public SessionSaveSlotListResult ListSlots()
                => new SessionSaveSlotListResult(SessionSaveSlotFailure.None, Array.Empty<SessionSaveSlotInfo>());
            public SessionSaveSlotResult DeleteSlot(string slotId) => new SessionSaveSlotResult(SessionSaveSlotFailure.None);
        }
    }
}
