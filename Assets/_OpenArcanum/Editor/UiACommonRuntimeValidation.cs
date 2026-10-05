using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using OpenArcanum.Rendering;
using OpenArcanum.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

internal static class UiACommonRuntimeValidation
{
    private static bool _running;
    private static int _warnings;
    private static int _errors;

    [MenuItem("OpenArcanum/UI-A/Run Physical PlayMode Validation", false, 2)]
    private static void Run()
    {
        if (!Application.isPlaying || _running)
            throw new InvalidOperationException("Enter TestTerrain Play mode and run only one UI-A validation.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>()
                                         ?? throw new InvalidOperationException("TestTerrain has no world loader.");
        loader.StartCoroutine(Validate(loader));
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        _running = true;
        _warnings = _errors = 0;
        Application.logMessageReceived += Track;
        GraphicsMode initialMode = OpenArcanumGraphicsSettings.Mode;
        string fixtureRoot = Path.Combine(Application.temporaryCachePath, "OpenArcanum-UIA-Physical");
        GameObject proofRoot = null;
        var diagnostics = new List<string>();
        RetailUiAssetResolver original = null;
        EnhancedUiAssetResolver enhanced = null;
        UiSkinResolver skin = null;
        try
        {
            ProductionGameUiPresenter presenter = Object.FindFirstObjectByType<ProductionGameUiPresenter>()
                                                  ?? throw new InvalidOperationException(
                                                      "Production placeholder presenter is unavailable.");
            GameUiController controller = presenter.Controller;
            GameUiScreen initialScreen = controller.Screen;
            object initialPlayer = loader.Session.PlayerState;
            string initialSector = loader.Session.SelectedSector;
            int initialStateCount = loader.Session.States.Count;
            int presenterCount = Object.FindObjectsByType<ProductionGameUiPresenter>(FindObjectsSortMode.None).Length;
            Check(presenter.enabled && controller != null,
                "existing placeholder production UI remains enabled and controller-backed");

            if (Directory.Exists(fixtureRoot)) Directory.Delete(fixtureRoot, recursive: true);
            Directory.CreateDirectory(fixtureRoot);
            original = RetailUiAssetResolver.CreateProduction(diagnostics.Add);
            enhanced = new EnhancedUiAssetResolver(fixtureRoot, diagnostics.Add);
            skin = new UiSkinResolver(original, enhanced);

            Check(original.TryResolve(new UiAssetKey(137), out UiResolvedAsset button)
                  && button.LogicalSize == new Vector2Int(23, 23),
                "real retail standard button resolves at native 23x23");
            Check(original.TryResolve(new UiAssetKey(3), out UiResolvedAsset hud)
                  && hud.LogicalSize == new Vector2Int(800, 600),
                "real retail HUD frame resolves at native 800x600");
            Check(original.TryResolve(new UiAssetKey(11), out UiResolvedAsset icon)
                  && icon.LogicalSize == new Vector2Int(24, 24),
                "real retail icon resolves at native 24x24");
            Check(original.TryResolve(new UiAssetKey(238), out UiResolvedAsset scrollbar)
                  && scrollbar.LogicalSize == new Vector2Int(11, 5),
                "real retail scrollbar cap resolves at native 11x5");
            Check(original.TryResolve(new UiAssetKey(1), out UiResolvedAsset cursor)
                  && cursor.LogicalSize == new Vector2Int(16, 22),
                "real retail cursor resolves with source hotspot metadata");

            WriteGeneratedFixture(enhanced.GetReplacementPath(button), button.LogicalSize * 4);

            proofRoot = new GameObject(
                "UI-A Physical Proof",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(SourceUiPresentationRoot));
            SourceUiPresentationRoot layers = proofRoot.GetComponent<SourceUiPresentationRoot>();
            var proof = new GameObject("Source Button Proof", typeof(RectTransform), typeof(Image), typeof(SourceUiImage));
            RectTransform proofRect = proof.GetComponent<RectTransform>();
            proofRect.SetParent(layers.GetLayer(SourceUiLayer.Tooltip), worldPositionStays: false);
            proofRect.anchorMin = proofRect.anchorMax = new Vector2(.5f, .5f);
            proofRect.anchoredPosition = new Vector2(300f, 200f);
            proofRect.sizeDelta = button.LogicalSize;
            SourceUiImage image = proof.GetComponent<SourceUiImage>();
            image.Bind(skin, button.Key);
            Vector2 geometry = proofRect.sizeDelta;

            OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Original);
            yield return null;
            Texture2D originalTexture = image.CurrentAsset.Texture;
            Check(image.CurrentAsset.ResolvedSkin == UiAssetSkin.Original,
                "source image binds Original retail presentation");

            OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Enhanced);
            yield return null;
            Texture2D enhancedTexture = image.CurrentAsset.Texture;
            Check(image.CurrentAsset.ResolvedSkin == UiAssetSkin.Enhanced
                  && enhancedTexture != originalTexture
                  && enhancedTexture.width == originalTexture.width * 4,
                "generated exact-4x fixture binds through Enhanced presentation");
            Check(proofRect.sizeDelta == geometry,
                "Enhanced pixels retain identical logical component geometry");
            Check(skin.TryResolve(new UiAssetKey(11, frame: 1), out UiResolvedAsset missingFrame)
                  && missingFrame.IsFallback
                  && missingFrame.ResolvedSkin == UiAssetSkin.Original,
                "missing Enhanced frame falls back independently to Original");

            OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Original);
            yield return null;
            Check(image.CurrentAsset.ResolvedSkin == UiAssetSkin.Original
                  && proofRect.sizeDelta == geometry,
                "Original presentation rebinds without geometry drift");

            foreach (Vector2Int resolution in new[]
                     {
                         new Vector2Int(1920, 1080),
                         new Vector2Int(2560, 1440),
                         new Vector2Int(3840, 2160),
                     })
            {
                UiLogicalMapping mapping = UiLogicalMapping.ForResolution(resolution.x, resolution.y);
                Rect mapped = mapping.LogicalToScreen(new Rect(115f, 2f, 23f, 23f));
                Check(mapping.ScreenToLogical(mapped.position) == new Vector2(115f, 2f)
                      && mapped.size == new Vector2(23f, 23f) * mapping.Scale
                      && mapping.GameplayWorldScreenRect.width == resolution.x,
                    $"{resolution.x}x{resolution.y} retains logical geometry and expands only the world width");
            }

            Check(ReferenceEquals(loader.Session.PlayerState, initialPlayer)
                  && loader.Session.SelectedSector == initialSector
                  && loader.Session.States.Count == initialStateCount
                  && controller.Screen == initialScreen,
                "skin switching leaves gameplay and modal/screen state unchanged");
            Check(Object.FindObjectsByType<ProductionGameUiPresenter>(FindObjectsSortMode.None).Length == presenterCount
                  && presenter.Controller == controller,
                "skin switching creates no duplicate UI presenter or controller");
            Check(_warnings == 0 && _errors == 0,
                "physical validation produced no warning or error diagnostics");

            Debug.Log(
                "UI-A PHYSICAL VALIDATION PASS: placeholder UI intact; retail button/HUD/icon/scrollbar/cursor resolved; " +
                "Original->Enhanced->Original rebound one source component with stable geometry; missing frame fell " +
                "back; 1080p/1440p/4K mapping passed; gameplay/controller state unchanged.");
        }
        finally
        {
            if (initialMode != OpenArcanumGraphicsSettings.Mode)
                OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            skin?.Dispose();
            enhanced?.Dispose();
            original?.Dispose();
            if (proofRoot != null) Object.Destroy(proofRoot);
            if (Directory.Exists(fixtureRoot)) Directory.Delete(fixtureRoot, recursive: true);
            Application.logMessageReceived -= Track;
            _running = false;
        }
    }

    private static void WriteGeneratedFixture(string path, Vector2Int size)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var texture = new Texture2D(size.x, size.y, TextureFormat.RGBA32, mipChain: false);
        try
        {
            var pixels = new Color32[size.x * size.y];
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
                pixels[y * size.x + x] = ((x / 8 + y / 8) & 1) == 0
                    ? new Color32(214, 45, 190, 255)
                    : new Color32(44, 220, 188, 255);
            pixels[0] = new Color32(0, 0, 0, 0);
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally { Object.Destroy(texture); }
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("UI-A physical validation FAIL: " + label);
        Debug.Log("UI-A physical proof: " + label + ".");
    }

    private static void Track(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Warning) _warnings++;
        else if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errors++;
    }
}
