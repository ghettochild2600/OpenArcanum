using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OpenArcanum.Rendering;
using OpenArcanum.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using UiText = UnityEngine.UI.Text;

namespace Arcanum.Formats.Tests
{
    [Category("UIACommonRuntime")]
    public sealed class UiACommonRuntimeTests
    {
        private static RetailUiAssetResolver _retail;
        private static List<string> _originalDiagnostics;

        private string _fixtureRoot;
        private List<string> _enhancedDiagnostics;
        private EnhancedUiAssetResolver _enhanced;
        private UiSkinResolver _skin;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _originalDiagnostics = new List<string>();
            _retail = RetailUiAssetResolver.CreateProduction(_originalDiagnostics.Add);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _retail?.Dispose();
            _retail = null;
        }

        [SetUp]
        public void SetUp()
        {
            _fixtureRoot = Path.Combine(Path.GetTempPath(), "OpenArcanum-UIA-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_fixtureRoot);
            _enhancedDiagnostics = new List<string>();
            _enhanced = new EnhancedUiAssetResolver(_fixtureRoot, _enhancedDiagnostics.Add);
            _skin = new UiSkinResolver(_retail, _enhanced);
        }

        [TearDown]
        public void TearDown()
        {
            _skin?.Dispose();
            _enhanced?.Dispose();
            OpenArcanumGraphicsSettings.ClearRuntimeMode();
            if (Directory.Exists(_fixtureRoot)) Directory.Delete(_fixtureRoot, recursive: true);
        }

        [Test]
        public void AssetKeyHasDeterministicValueSemantics()
        {
            var key = new UiAssetKey(137, 1, 2, 3);
            var equal = new UiAssetKey(137, 1, 2, 3);
            Assert.That(equal, Is.EqualTo(key));
            Assert.That(equal.GetHashCode(), Is.EqualTo(key.GetHashCode()));
            Assert.That(key.ToString(), Is.EqualTo("ui:0137:p01:r02:f003"));
            Assert.That(new UiAssetKey(137, 0, 2, 3), Is.Not.EqualTo(key));
            Assert.That(new UiAssetKey(137, 1, 0, 3), Is.Not.EqualTo(key));
            Assert.That(new UiAssetKey(137, 1, 2, 0), Is.Not.EqualTo(key));
        }

        [Test]
        public void OriginalResolverLoadsRetailButtonAtNativeDimensions()
        {
            Assert.That(_retail.TryResolve(new UiAssetKey(137), out UiResolvedAsset asset), Is.True);
            Assert.That(asset.LogicalSize, Is.EqualTo(new Vector2Int(23, 23)));
            Assert.That((asset.Texture.width, asset.Texture.height), Is.EqualTo((23, 23)));
            Assert.That(asset.ResolvedSkin, Is.EqualTo(UiAssetSkin.Original));
            Assert.That(asset.SourcePath, Is.EqualTo("art/interface/lilgrnbut.art"));
        }

        [Test]
        public void OriginalResolverSelectsExactRetailFrame()
        {
            var first = new UiAssetKey(11, frame: 0);
            var second = new UiAssetKey(11, frame: 1);
            Assert.That(_retail.TryResolve(first, out UiResolvedAsset frame0), Is.True);
            Assert.That(_retail.TryResolve(second, out UiResolvedAsset frame1), Is.True);
            Assert.That(frame0.Key, Is.EqualTo(first));
            Assert.That(frame1.Key, Is.EqualTo(second));
            Assert.That(frame1.Sprite, Is.Not.SameAs(frame0.Sprite));
        }

        [Test]
        public void OriginalResolverMissingSourceFailsSafelyAndDeduplicatesDiagnostic()
        {
            int before = _originalDiagnostics.Count;
            var missing = new UiAssetKey(999999);
            Assert.That(_retail.TryResolve(missing, out _), Is.False);
            Assert.That(_retail.TryResolve(missing, out _), Is.False);
            Assert.That(_originalDiagnostics.Count, Is.EqualTo(before + 1));
        }

        [Test]
        public void EnhancedResolverAcceptsExactFourTimesFixture()
        {
            UiResolvedAsset original = Original(137);
            WriteFixture(original, original.LogicalSize.x * 4, original.LogicalSize.y * 4);
            Assert.That(_enhanced.TryResolve(original, out UiResolvedAsset enhanced), Is.True);
            Assert.That(enhanced.ResolvedSkin, Is.EqualTo(UiAssetSkin.Enhanced));
            Assert.That(enhanced.TextureScale, Is.EqualTo(4));
            Assert.That(enhanced.LogicalSize, Is.EqualTo(original.LogicalSize));
            Assert.That(enhanced.Sprite.pixelsPerUnit, Is.EqualTo(4f));
        }

        [Test]
        public void EnhancedMissingReplacementFallsBackAndReportsOnce()
        {
            var key = new UiAssetKey(137);
            Assert.That(_skin.TryResolve(key, UiAssetSkin.Enhanced, out UiResolvedAsset first), Is.True);
            Assert.That(_skin.TryResolve(key, UiAssetSkin.Enhanced, out UiResolvedAsset second), Is.True);
            Assert.That(first.IsFallback, Is.True);
            Assert.That(first.ResolvedSkin, Is.EqualTo(UiAssetSkin.Original));
            Assert.That(second.Sprite, Is.SameAs(first.Sprite));
            Assert.That(_enhancedDiagnostics.Count, Is.EqualTo(1));
        }

        [Test]
        public void EnhancedWrongWidthRejectsWithoutBreakingOriginal()
        {
            UiResolvedAsset original = Original(137);
            WriteFixture(original, original.LogicalSize.x * 4 - 1, original.LogicalSize.y * 4);
            Assert.That(_skin.TryResolve(original.Key, UiAssetSkin.Enhanced, out UiResolvedAsset result), Is.True);
            Assert.That(result.IsFallback, Is.True);
            StringAssert.Contains("expected width", _enhancedDiagnostics[0]);
        }

        [Test]
        public void EnhancedWrongHeightRejectsWithoutBreakingOriginal()
        {
            UiResolvedAsset original = Original(137);
            WriteFixture(original, original.LogicalSize.x * 4, original.LogicalSize.y * 4 - 1);
            Assert.That(_skin.TryResolve(original.Key, UiAssetSkin.Enhanced, out UiResolvedAsset result), Is.True);
            Assert.That(result.IsFallback, Is.True);
            StringAssert.Contains("expected height", _enhancedDiagnostics[0]);
        }

        [Test]
        public void EnhancedCorruptFileRejectsWithoutBreakingOriginal()
        {
            UiResolvedAsset original = Original(137);
            string path = _enhanced.GetReplacementPath(original);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "not a PNG");
            Assert.That(_skin.TryResolve(original.Key, UiAssetSkin.Enhanced, out UiResolvedAsset result), Is.True);
            Assert.That(result.IsFallback, Is.True);
            Assert.That(_enhancedDiagnostics.Count, Is.EqualTo(1));
        }

        [Test]
        public void EnhancedFallbackIsIndependentPerFrame()
        {
            UiResolvedAsset original0 = Original(11, frame: 0);
            WriteFixture(original0, original0.LogicalSize.x * 4, original0.LogicalSize.y * 4);
            Assert.That(_skin.TryResolve(original0.Key, UiAssetSkin.Enhanced, out UiResolvedAsset enhanced0), Is.True);
            Assert.That(_skin.TryResolve(new UiAssetKey(11, frame: 1), UiAssetSkin.Enhanced,
                out UiResolvedAsset fallback1), Is.True);
            Assert.That(enhanced0.ResolvedSkin, Is.EqualTo(UiAssetSkin.Enhanced));
            Assert.That(fallback1.ResolvedSkin, Is.EqualTo(UiAssetSkin.Original));
            Assert.That(fallback1.IsFallback, Is.True);
        }

        [Test]
        public void SkinResolverSelectsOriginalAndEnhancedWithoutChangingIdentity()
        {
            UiResolvedAsset original = Original(137);
            WriteFixture(original, 92, 92);
            Assert.That(_skin.TryResolve(original.Key, UiAssetSkin.Original, out UiResolvedAsset first), Is.True);
            Assert.That(_skin.TryResolve(original.Key, UiAssetSkin.Enhanced, out UiResolvedAsset second), Is.True);
            Assert.That(second.Key, Is.EqualTo(first.Key));
            Assert.That(second.LogicalSize, Is.EqualTo(first.LogicalSize));
            Assert.That(second.Texture, Is.Not.SameAs(first.Texture));
        }

        [Test]
        public void GraphicsModeChangeRebindsPresentationWithoutChangingLogicalIdentity()
        {
            UiResolvedAsset original = Original(137);
            WriteFixture(original, 92, 92);
            int invalidations = 0;
            _skin.PresentationInvalidated += () => invalidations++;

            OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Original);
            Assert.That(_skin.TryResolve(original.Key, out UiResolvedAsset first), Is.True);
            OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Enhanced);
            Assert.That(_skin.TryResolve(original.Key, out UiResolvedAsset second), Is.True);

            Assert.That(invalidations, Is.GreaterThanOrEqualTo(1));
            Assert.That(second.Key, Is.EqualTo(first.Key));
            Assert.That(second.LogicalSize, Is.EqualTo(first.LogicalSize));
            Assert.That(second.ResolvedSkin, Is.EqualTo(UiAssetSkin.Enhanced));
        }

        [TestCase(1920, 1080, 1.8f, 240f)]
        [TestCase(2560, 1440, 2.4f, 320f)]
        [TestCase(3840, 2160, 3.6f, 480f)]
        public void LogicalMappingPreservesSourceGeometryAcrossTargets(int width, int height, float scale, float originX)
        {
            UiLogicalMapping mapping = UiLogicalMapping.ForResolution(width, height);
            var logical = new Rect(115f, 2f, 23f, 23f);
            Rect screen = mapping.LogicalToScreen(logical);
            Assert.That(mapping.Scale, Is.EqualTo(scale).Within(.0001f));
            Assert.That(mapping.Origin.x, Is.EqualTo(originX).Within(.0001f));
            Assert.That(mapping.Origin.y, Is.EqualTo(0f).Within(.0001f));
            Vector2 roundTrip = mapping.ScreenToLogical(screen.position);
            Assert.That(roundTrip.x, Is.EqualTo(logical.x).Within(.0001f));
            Assert.That(roundTrip.y, Is.EqualTo(logical.y).Within(.0001f));
            Assert.That(screen.width, Is.EqualTo(logical.width * scale).Within(.0001f));
            Assert.That(screen.height, Is.EqualTo(logical.height * scale).Within(.0001f));
            Assert.That(mapping.GameplayWorldScreenRect.width, Is.EqualTo(width));
            Assert.That(mapping.GameplayWorldScreenRect.height, Is.EqualTo(400f * scale));
        }

        [Test]
        public void EnhancedPixelsDoNotChangeLogicalRectTransformSize()
        {
            UiResolvedAsset original = Original(137);
            WriteFixture(original, 92, 92);
            _skin.TryResolve(original.Key, UiAssetSkin.Enhanced, out UiResolvedAsset enhanced);
            Assert.That(enhanced.Texture.width, Is.EqualTo(original.Texture.width * 4));
            Assert.That(enhanced.Texture.height, Is.EqualTo(original.Texture.height * 4));
            Assert.That(enhanced.LogicalSize, Is.EqualTo(original.LogicalSize));
            Assert.That(enhanced.Sprite.bounds.size, Is.EqualTo(original.Sprite.bounds.size));
        }

        [Test]
        public void SourceButtonStateChangesPixelsButNotInteractionRectangle()
        {
            var go = new GameObject("UIA Button", typeof(RectTransform), typeof(Image), typeof(Button),
                typeof(SourceUiImage), typeof(SourceUiButton));
            try
            {
                RectTransform rect = go.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(80f, 32f);
                SourceUiButton button = go.GetComponent<SourceUiButton>();
                button.Bind(_skin,
                    new UiAssetKey(9, frame: 0),
                    new UiAssetKey(9, frame: 1),
                    new UiAssetKey(9, frame: 2),
                    new UiAssetKey(9, frame: 2));
                Vector2 before = rect.sizeDelta;
                button.OnPointerEnter((PointerEventData)null);
                Assert.That(button.CurrentPresentationKey.Frame, Is.EqualTo(1));
                button.OnPointerDown((PointerEventData)null);
                Assert.That(button.CurrentPresentationKey.Frame, Is.EqualTo(2));
                button.SetInteractable(false);
                Assert.That(button.CurrentPresentationKey.Frame, Is.EqualTo(2));
                Assert.That(rect.sizeDelta, Is.EqualTo(before));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void RepeatedResolveReusesRuntimeResources()
        {
            var key = new UiAssetKey(137);
            int before = _retail.CachedFrameCount;
            Assert.That(_skin.TryResolve(key, UiAssetSkin.Original, out UiResolvedAsset first), Is.True);
            Assert.That(_skin.TryResolve(key, UiAssetSkin.Original, out UiResolvedAsset second), Is.True);
            Assert.That(second.Sprite, Is.SameAs(first.Sprite));
            Assert.That(_retail.CachedFrameCount, Is.InRange(before, before + 1));
            Assert.That(_skin.CachedResolutionCount, Is.EqualTo(1));
        }

        [Test]
        public void CursorPresenterPreservesRetailHotspot()
        {
            var go = new GameObject("UIA Cursor", typeof(SourceUiCursorPresenter));
            try
            {
                var key = new UiAssetKey(1);
                SourceUiCursorPresenter cursor = go.GetComponent<SourceUiCursorPresenter>();
                cursor.Bind(_skin, key, applySystemCursor: false);
                UiResolvedAsset original = Original(1);
                Assert.That(cursor.CurrentTexture, Is.SameAs(original.Texture));
                Assert.That(cursor.CurrentHotspotPixels, Is.EqualTo((Vector2)original.Hotspot));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ScaleMetadataDefaultsFixedAndExposesOnlyProvenExceptions()
        {
            Assert.That(SourceUiScaleMetadataCatalog.Get(329).Policy, Is.EqualTo(SourceUiScalePolicy.Fixed));
            Assert.That(SourceUiScaleMetadataCatalog.Get(354).Policy, Is.EqualTo(SourceUiScalePolicy.NineSlice));
            Assert.That(SourceUiScaleMetadataCatalog.Get(354).Border, Is.EqualTo(new Vector4(8, 8, 8, 8)));
            Assert.That(SourceUiScaleMetadataCatalog.Get(822).Policy,
                Is.EqualTo(SourceUiScalePolicy.HorizontalSlice));
            Assert.That(SourceUiScaleMetadataCatalog.Get(787).IsRepeatingTile, Is.True);
        }

        [Test]
        public void PresentationRootCreatesSeparatedLayersWithoutScreenAuthority()
        {
            var go = new GameObject("UIA Root", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(SourceUiPresentationRoot));
            try
            {
                SourceUiPresentationRoot root = go.GetComponent<SourceUiPresentationRoot>();
                root.ApplyResolution(new Vector2Int(3840, 2160));
                Assert.That(root.Mapping.Scale, Is.EqualTo(3.6f).Within(.0001f));
                Assert.That(root.GetLayer(SourceUiLayer.World).parent, Is.EqualTo(go.transform));
                Assert.That(root.GetLayer(SourceUiLayer.Hud).parent, Is.EqualTo(root.ReferenceSurface));
                Assert.That(root.GetLayer(SourceUiLayer.Modal), Is.Not.SameAs(root.GetLayer(SourceUiLayer.Dialogue)));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TileAssemblyKeepsFixedWidthCapsAndTiledMiddle()
        {
            var go = new GameObject("UIA Scrollbar", typeof(RectTransform), typeof(SourceUiVerticalTileAssembly));
            try
            {
                RectTransform root = go.GetComponent<RectTransform>();
                root.sizeDelta = new Vector2(11f, 42f);
                SourceUiVerticalTileAssembly assembly = go.GetComponent<SourceUiVerticalTileAssembly>();
                assembly.Bind(_skin, new UiAssetKey(238), new UiAssetKey(787), new UiAssetKey(240));
                Assert.That(((RectTransform)go.transform.Find("Top")).sizeDelta, Is.EqualTo(new Vector2(11, 5)));
                Assert.That(((RectTransform)go.transform.Find("Bottom")).sizeDelta, Is.EqualTo(new Vector2(11, 7)));
                Assert.That(((RectTransform)go.transform.Find("Middle")).sizeDelta, Is.EqualTo(new Vector2(11, 30)));
                Assert.That(go.transform.Find("Middle").GetComponent<Image>().type, Is.EqualTo(Image.Type.Tiled));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void DynamicTypographyUsesLogicalStyleWithoutBakedLabel()
        {
            var go = new GameObject("UIA Text", typeof(RectTransform), typeof(UiText), typeof(SourceUiText));
            try
            {
                SourceUiText sourceText = go.GetComponent<SourceUiText>();
                sourceText.Role = SourceUiTextRole.Title;
                UiText text = go.GetComponent<UiText>();
                text.text = "Dynamic label";
                Assert.That(text.fontSize, Is.EqualTo(18));
                Assert.That(text.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
                Assert.That(text.text, Is.EqualTo("Dynamic label"));
                Assert.That(go.GetComponent<Shadow>(), Is.Not.Null);
            }
            finally { Object.DestroyImmediate(go); }
        }

        private UiResolvedAsset Original(int sourceId, int frame = 0)
        {
            Assert.That(_retail.TryResolve(new UiAssetKey(sourceId, frame: frame), out UiResolvedAsset original),
                Is.True);
            return original;
        }

        private void WriteFixture(UiResolvedAsset original, int width, int height)
        {
            string path = _enhanced.GetReplacementPath(original);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
            try
            {
                var pixels = new Color32[width * height];
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color32(214, 45, 190, 255);
                if (pixels.Length > 0) pixels[0] = new Color32(0, 0, 0, 0);
                texture.SetPixels32(pixels);
                texture.Apply(updateMipmaps: false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(texture); }
        }
    }
}
