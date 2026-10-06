using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using Arcanum.World;
using NUnit.Framework;
using OpenArcanum.Rendering;
using OpenArcanum.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("UICGameplayHud")]
    public sealed class UiCGameplayHudTests
    {
        private const string Sector = "maps/test/1.sec";
        private static RetailUiAssetResolver _retail;
        private string _fixtureRoot;
        private EnhancedUiAssetResolver _enhanced;
        private UiSkinResolver _skin;
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private GameUiController _controller;
        private RetailGameplayHudView _view;

        [OneTimeSetUp]
        public void OneTimeSetUp() => _retail = RetailUiAssetResolver.CreateProduction();

        [OneTimeTearDown]
        public void OneTimeTearDown() { _retail?.Dispose(); _retail = null; }

        [SetUp]
        public void SetUp()
        {
            _fixtureRoot = Path.Combine(Path.GetTempPath(), "OpenArcanum-UIC-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_fixtureRoot);
            _enhanced = new EnhancedUiAssetResolver(_fixtureRoot, _ => { });
            _skin = new UiSkinResolver(_retail, _enhanced);
            _root = new GameObject(nameof(UiCGameplayHudTests));
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new Owner(_session));
            Assert.That(_session.SelectSector(Sector), Is.True);
            _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity, Sector, Vector2.one,
                0x28100000u);
            _controller = new GameUiController(_session, new EmptySlots());
            _view = _root.AddComponent<RetailGameplayHudView>();
            _view.Bind(_controller, _session, screen => _controller.Open(screen), _skin);
            _view.Synchronize(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            foreach (EventSystem events in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
                if (events.name == "OpenArcanum UI EventSystem") Object.DestroyImmediate(events.gameObject);
            _skin?.Dispose();
            _enhanced?.Dispose();
            OpenArcanumGraphicsSettings.ClearRuntimeMode();
            if (Directory.Exists(_fixtureRoot)) Directory.Delete(_fixtureRoot, recursive: true);
        }

        [Test]
        public void RetailHudAssetsResolveFromSourceData()
        {
            int[] ids = { 3, 17, 18, 19, 20, 169, 172, 173, 181, 186, 187, 193, 251, 354, 470, 471, 472, 473 };
            foreach (int id in ids)
                Assert.That(_retail.TryResolve(new UiAssetKey(id), out _), Is.True, $"source id {id}");
            Assert.That(_retail.TryResolve(new UiAssetKey(3), out UiResolvedAsset frame), Is.True);
            Assert.That(frame.LogicalSize, Is.EqualTo(new Vector2Int(800, 600)));
        }

        [Test]
        public void SourceGeometryPreservesTopWorldAndBottomPartition()
        {
            Assert.That(RetailGameplayHudLayout.TopInterface, Is.EqualTo(new Rect(0, 0, 800, 41)));
            Assert.That(RetailGameplayHudLayout.WorldViewport, Is.EqualTo(new Rect(0, 41, 800, 400)));
            Assert.That(RetailGameplayHudLayout.BottomInterface, Is.EqualTo(new Rect(0, 441, 800, 159)));
            Assert.That(RetailGameplayHudLayout.MessageWindow, Is.EqualTo(new Rect(196, 492, 410, 107)));
        }

        [Test]
        public void QuickSlotsUseExactTenSourcePositionsAndIds()
        {
            Assert.That(RetailGameplayHudLayout.QuickSlotX,
                Is.EqualTo(new[] { 198f, 237f, 276f, 315f, 354f, 418f, 456f, 495f, 534f, 573f }));
            Assert.That(RetailGameplayHudLayout.QuickSlotSourceIds,
                Is.EqualTo(new[] { 173, 174, 175, 176, 177, 178, 179, 180, 181, 172 }));
        }

        [TestCase(0, 100, 0)]
        [TestCase(1, 100, 8)]
        [TestCase(50, 100, 52)]
        [TestCase(99, 100, 88)]
        [TestCase(100, 100, 88)]
        [TestCase(20, 0, 0)]
        public void VialCropUsesSourceBoundedEightPixelOverlap(int current, int maximum, int expected)
            => Assert.That(RetailGameplayHudLayout.LiquidVisiblePixels(current, maximum), Is.EqualTo(expected));

        [Test]
        public void ViewBuildsOneSourcePresenterAndProjectsAuthoritativeVitals()
        {
            GameUiHudView projection = _controller.ProjectHud();
            Assert.That(_view.IsVisible, Is.True);
            Assert.That(_view.BackgroundAsset.Key.SourceId, Is.EqualTo(3));
            Assert.That(_view.LastProjection.HitPoints, Is.EqualTo(projection.HitPoints));
            Assert.That(_view.LastProjection.Fatigue, Is.EqualTo(projection.Fatigue));
            Assert.That(Object.FindObjectsByType<RetailGameplayHudView>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
        }

        [Test]
        public void ModalScreenHidesHudWithoutChangingControllerAuthority()
        {
            PersistentPlayerState player = _session.PlayerState;
            Assert.That(_controller.Open(GameUiScreen.Inventory), Is.True);
            _view.Synchronize(true);
            Assert.That(_view.IsVisible, Is.False);
            Assert.That(_session.PlayerState, Is.SameAs(player));
            _controller.Close();
            _view.Synchronize(true);
            Assert.That(_view.IsVisible, Is.True);
        }

        [Test]
        public void AuthenticInventoryControlReusesExistingControllerRoute()
        {
            Button button = _root.GetComponentsInChildren<Button>(true).Single(value => value.name == "Inventory");
            button.onClick.Invoke();
            Assert.That(_controller.Screen, Is.EqualTo(GameUiScreen.Inventory));
        }

        [TestCase("Character", GameUiScreen.Character)]
        [TestCase("Logbook", GameUiScreen.Journal)]
        [TestCase("Map", GameUiScreen.Map)]
        [TestCase("Skills", GameUiScreen.Skills)]
        [TestCase("Spells", GameUiScreen.Magic)]
        [TestCase("Schematics", GameUiScreen.Crafting)]
        public void SourceControlsReuseExistingScreenRoutes(string buttonName, GameUiScreen expected)
        {
            Button button = _root.GetComponentsInChildren<Button>(true).Single(value => value.name == buttonName);
            button.onClick.Invoke();
            Assert.That(_controller.Screen, Is.EqualTo(expected));
        }

        [Test]
        public void NonSourceConvenienceButtonsAreAbsentFromRetailComposition()
        {
            string[] forbidden = { "PARTY", "BARTER", "SAVE/LOAD" };
            string[] names = _root.GetComponentsInChildren<Button>(true).Select(value => value.name).ToArray();
            Assert.That(forbidden.Intersect(names), Is.Empty);
        }

        [Test]
        public void QuickSlotProjectionUsesCoordinatorOwnedBindingAndQuantity()
        {
            PersistentObjectState item = AddStack(1, 12);
            Assert.That(_session.Shortcuts.AssignItem(0, item.Identity), Is.True);
            _view.Synchronize(true);
            RetailQuickSlotPresentation slot = _view.QuickSlots[0];
            Assert.That((slot.Kind, slot.SourceId, slot.Quantity),
                Is.EqualTo((QuickSlotKind.Item, item.PrototypeNumber, 12)));
            Assert.That(_session.Shortcuts.Get(0).PreferredItem, Is.EqualTo(item.Identity));
        }

        [Test]
        public void EmptyQuickSlotClickUsesExistingControllerAndFailsWithoutMutation()
        {
            int states = _session.States.Count;
            Button button = _root.GetComponentsInChildren<Button>(true).Single(value => value.name == "Quick Slot 1");
            button.onClick.Invoke();
            Assert.That(_controller.Feedback, Does.Contain("empty"));
            Assert.That(_session.States.Count, Is.EqualTo(states));
        }

        [TestCase(800, 600, 1f, 0f, 0f)]
        [TestCase(1920, 1080, 1.8f, 240f, 0f)]
        [TestCase(2560, 1440, 2.4f, 320f, 0f)]
        [TestCase(3840, 2160, 3.6f, 480f, 0f)]
        [TestCase(1920, 1200, 2f, 160f, 0f)]
        [TestCase(3440, 1440, 2.4f, 760f, 0f)]
        [TestCase(600, 900, .75f, 0f, 225f)]
        [TestCase(1024, 512, .85333335f, 170.66667f, 0f)]
        public void HudRemainsCenteredVisibleAndUndistortedAtArbitraryResolutions(
            int width, int height, float scale, float originX, float originY)
        {
            UiLogicalMapping mapping = UiLogicalMapping.ForResolution(width, height);
            Rect hud = mapping.LogicalToScreen(new Rect(0, 0, 800, 600));
            Assert.That(mapping.Scale, Is.EqualTo(scale).Within(.0001f));
            Assert.That(hud.x, Is.EqualTo(originX).Within(.0001f));
            Assert.That(hud.y, Is.EqualTo(originY).Within(.0001f));
            Assert.That(hud.xMin, Is.GreaterThanOrEqualTo(-.0001f));
            Assert.That(hud.yMin, Is.GreaterThanOrEqualTo(-.0001f));
            Assert.That(hud.xMax, Is.LessThanOrEqualTo(width + .0001f));
            Assert.That(hud.yMax, Is.LessThanOrEqualTo(height + .0001f));
            Assert.That(hud.width / hud.height, Is.EqualTo(4f / 3f).Within(.0001f));
            Assert.That(mapping.GameplayWorldScreenRect.width, Is.EqualTo(width));
        }

        [TestCase(1920, 1080, -100f, 300f)]
        [TestCase(3440, 1440, -250f, 300f)]
        [TestCase(600, 900, 400f, -120f)]
        public void LogicalAndPhysicalCoordinatesRoundTripInsideAndOutsideTheSourceSurface(
            int width, int height, float logicalX, float logicalY)
        {
            UiLogicalMapping mapping = UiLogicalMapping.ForResolution(width, height);
            var logical = new Vector2(logicalX, logicalY);
            Vector2 physical = mapping.LogicalToScreen(logical);
            Assert.That(mapping.ScreenToLogical(physical).x, Is.EqualTo(logical.x).Within(.0001f));
            Assert.That(mapping.ScreenToLogical(physical).y, Is.EqualTo(logical.y).Within(.0001f));
        }

        [TestCase(800, 600)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3840, 2160)]
        public void CameraViewportMatchesSourceWorldAperture(int width, int height)
        {
            Rect viewport = RetailGameplayHudLayout.GameplayCameraViewport(width, height);
            Assert.That(viewport.x, Is.Zero);
            Assert.That(viewport.width, Is.EqualTo(1f));
            Assert.That(viewport.y, Is.Zero);
            Assert.That(viewport.height, Is.EqualTo(1f));
        }

        [Test]
        public void HudHitBandsBlockOnlyCenteredSourceComposition()
        {
            Assert.That(RetailGameplayHudLayout.ContainsInterfacePoint(new Vector2(960, 1070), 1920, 1080), Is.True);
            Assert.That(RetailGameplayHudLayout.ContainsInterfacePoint(new Vector2(960, 500), 1920, 1080), Is.False);
            Assert.That(RetailGameplayHudLayout.ContainsInterfacePoint(new Vector2(30, 20), 1920, 1080), Is.False);
        }

        [Test]
        public void GameplayCursorUsesTheSameScaledReferenceSurfaceAsTheHud()
        {
            SourceUiPresentationRoot presentation = _root.GetComponentInChildren<SourceUiPresentationRoot>(true);
            RetailMainMenuCursorView cursor = _root.GetComponentsInChildren<RetailMainMenuCursorView>(true)
                .Single(value => value.name == "Retail Gameplay Cursor");
            presentation.ApplyResolution(new Vector2Int(1920, 1080));

            Assert.That(cursor.transform.parent, Is.EqualTo(presentation.GetLayer(SourceUiLayer.Cursor)));
            Assert.That(cursor.transform.IsChildOf(presentation.ReferenceSurface), Is.True);
            Assert.That(cursor.transform.lossyScale.x, Is.EqualTo(1.8f).Within(.0001f));
            Assert.That(cursor.transform.lossyScale.y, Is.EqualTo(1.8f).Within(.0001f));
            Assert.That(cursor.CurrentAsset.LogicalSize, Is.EqualTo(new Vector2Int(24, 27)));
        }

        [Test]
        public void EnhancedFrameUsesExactFourTimesPixelsWithIdenticalLogicalGeometry()
        {
            UiResolvedAsset original = Original(3);
            WriteFixture(original, original.LogicalSize * 4);
            _skin.InvalidatePresentation();
            Assert.That(_skin.TryResolve(original.Key, UiAssetSkin.Enhanced, out UiResolvedAsset enhanced), Is.True);
            Assert.That(enhanced.ResolvedSkin, Is.EqualTo(UiAssetSkin.Enhanced));
            Assert.That(enhanced.Texture.width, Is.EqualTo(3200));
            Assert.That(enhanced.Texture.height, Is.EqualTo(2400));
            Assert.That(enhanced.LogicalSize, Is.EqualTo(original.LogicalSize));
        }

        [Test]
        public void EnhancedControlFixtureAndMissingAssetFallbackArePerAsset()
        {
            UiResolvedAsset control = Original(186);
            WriteFixture(control, control.LogicalSize * 4);
            _skin.InvalidatePresentation();
            Assert.That(_skin.TryResolve(control.Key, UiAssetSkin.Enhanced, out UiResolvedAsset enhanced), Is.True);
            Assert.That(enhanced.ResolvedSkin, Is.EqualTo(UiAssetSkin.Enhanced));
            Assert.That(_skin.TryResolve(new UiAssetKey(187), UiAssetSkin.Enhanced, out UiResolvedAsset fallback), Is.True);
            Assert.That(fallback.IsFallback, Is.True);
            Assert.That(fallback.LogicalSize, Is.EqualTo(Original(187).LogicalSize));
        }

        [Test]
        public void OriginalEnhancedOriginalSwitchPreservesStateSlotsAndHitGeometry()
        {
            PersistentObjectState item = AddStack(2, 3);
            Assert.That(_session.Shortcuts.AssignItem(1, item.Identity), Is.True);
            Rect before = RetailGameplayHudLayout.BottomInterface;
            OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Enhanced);
            _view.Synchronize(true);
            OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Original);
            _view.Synchronize(true);
            Assert.That(RetailGameplayHudLayout.BottomInterface, Is.EqualTo(before));
            Assert.That(_view.QuickSlots[1].SourceId, Is.EqualTo(item.PrototypeNumber));
            Assert.That(_session.PlayerState.Identity, Is.EqualTo(ProductionPlayerLifecycle.DefaultPlayerIdentity));
            Assert.That(Object.FindObjectsByType<RetailGameplayHudView>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
        }

        [Test]
        public void ExternalFixtureCanBeReboundToOwnedProductionResolver()
        {
            _view.Bind(_controller, _session, screen => _controller.Open(screen));
            _view.Synchronize(true);

            Assert.That(_view.IsAvailable, Is.True);
            Assert.That(_view.BackgroundAsset, Is.Not.Null);
            Assert.That(_view.BackgroundAsset.Key.SourceId, Is.EqualTo(3));
            Assert.That(_view.LastProjection, Is.Not.Null);
        }

        private UiResolvedAsset Original(int sourceId)
        {
            Assert.That(_retail.TryResolve(new UiAssetKey(sourceId), out UiResolvedAsset value), Is.True);
            return value;
        }

        private PersistentObjectState AddStack(int sequence, int quantity)
        {
            const int prototype = 9500;
            var source = new ObjectInstance(ObjectType.Ammo, prototype, null, 0x70000000u, 0, 0,
                oid: GuidBytes(sequence), parentOid: Bytes(_session.PlayerState.Identity), invLocation: 0);
            return _session.GetOrCreate(source, Sector, source.CurrentArtId.Value, false, false,
                inventoryArtId: 0u, stackQuantity: quantity, inventoryLocation: 0);
        }

        private void WriteFixture(UiResolvedAsset original, Vector2Int size)
        {
            string path = _enhanced.GetReplacementPath(original);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var texture = new Texture2D(size.x, size.y, TextureFormat.RGBA32, mipChain: false);
            try
            {
                texture.SetPixel(0, 0, new Color(1f, 0f, 1f, 1f));
                texture.Apply(false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(texture); }
        }

        private static byte[] GuidBytes(int sequence)
            => Bytes(Parse($"G_{sequence:X8}_0000_0000_0000_000000000000"));

        private static ArcanumObjectId Parse(string key)
        { ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId id); return id; }

        private static byte[] Bytes(ArcanumObjectId identity)
        {
            string compact = identity.Key.Substring(2).Replace("_", "");
            var bytes = new byte[24]; bytes[0] = (byte)ArcanumObjectIdType.Guid;
            for (int index = 0; index < 16; index++)
                bytes[8 + index] = Convert.ToByte(compact.Substring(index * 2, 2), 16);
            return bytes;
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
