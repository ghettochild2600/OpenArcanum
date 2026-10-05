using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Runtime.UI;
using OpenArcanum.Rendering;
using OpenArcanum.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace OpenArcanum.Editor
{
    /// <summary>Production Play Mode proof for UI-B. Generated Enhanced pixels live only in a GUID temp folder.</summary>
    [InitializeOnLoad]
    public static class UiBMainMenuValidation
    {
        private const string MenuPath = "OpenArcanum/UI-B/Run Physical PlayMode Validation";
        private const string PendingKey = "OpenArcanum.UIB.PhysicalValidationPending";

        static UiBMainMenuValidation()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem(MenuPath)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("UI-B PHYSICAL VALIDATION: stop Play Mode before starting a new run.");
                return;
            }

            SessionState.SetBool(PendingKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
            SessionState.EraseBool(PendingKey);
            EditorApplication.delayCall += Validate;
        }

        private static void Validate()
        {
            string fixtureRoot = Path.Combine(Path.GetTempPath(), "OpenArcanum-UIB-Physical-" + Guid.NewGuid().ToString("N"));
            UiSkinResolver fixtureSkin = null;
            try
            {
                ProductionGameUiPresenter presenter = Require(Object.FindFirstObjectByType<ProductionGameUiPresenter>(),
                    "production UI presenter");
                RetailMainMenuView view = Require(Object.FindFirstObjectByType<RetailMainMenuView>(),
                    "retail Main Menu view");
                GameUiController controller = Require(presenter.Controller, "production UI controller");

                Check(Object.FindObjectsByType<ProductionGameUiPresenter>(FindObjectsSortMode.None).Length == 1,
                    "exactly one production presenter");
                Check(Object.FindObjectsByType<RetailMainMenuView>(FindObjectsSortMode.None).Length == 1,
                    "exactly one retail Main Menu view");
                Check(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1,
                    "exactly one EventSystem");
                Check(controller.Screen == GameUiScreen.MainMenu && view.IsVisible,
                    "source Main Menu is the active production screen");
                Check(view.BackgroundAsset?.Key.SourceId == 329
                      && view.BackgroundAsset.LogicalSize == new Vector2Int(800, 600),
                    "retail source 329 supplies the 800x600 composition");
                Check(view.Model.Entries.Select(entry => entry.Label).SequenceEqual(new[]
                    { "Single Player", "Multiplayer", "Options", "Credits", "Exit Game" }),
                    "top-level retail order");

                object playerBefore = presenter.GetComponent<Arcanum.Runtime.World.WorldMapSessionCoordinator>().PlayerState;
                Rect buttonGeometry = view.Buttons[0].SourceRect;
                view.Buttons[0].OnPointerEnter(null);
                Check(view.Buttons[0].State == RetailMainMenuButtonState.Hover, "source hover state");
                view.Buttons[0].OnPointerDown(null);
                Check(view.Buttons[0].State == RetailMainMenuButtonState.Pressed, "source pressed state");
                view.Buttons[0].OnPointerUp(null);
                view.Buttons[0].OnPointerExit(null);
                Check(view.Buttons[0].SourceRect == buttonGeometry, "button geometry is state-invariant");

                view.Model.Submit(RetailMainMenuCommand.SinglePlayer);
                Check(view.Model.State == RetailMainMenuState.SinglePlayer
                      && view.BackgroundAsset?.Key.SourceId == 331, "source Single Player submenu");
                view.Model.Escape();
                Check(view.Model.State == RetailMainMenuState.TopLevel, "Esc returns from submenu");

                view.Model.Submit(RetailMainMenuCommand.SinglePlayer);
                view.Model.Submit(RetailMainMenuCommand.NewGame);
                Check(view.Model.State == RetailMainMenuState.NewGameChoice, "authentic New Game choice");
                view.Model.Submit(RetailMainMenuCommand.NewCharacter);
                Check(controller.Screen == GameUiScreen.CharacterCreation && controller.CreationDraft != null,
                    "New Character reuses production M12B/M12C authority");
                controller.Open(GameUiScreen.MainMenu);
                view.Synchronize();

                view.Model.Submit(RetailMainMenuCommand.SinglePlayer);
                view.Model.Submit(RetailMainMenuCommand.LoadGame);
                Check(controller.Screen == GameUiScreen.SaveLoad
                      && controller.SaveLoad.Mode == Arcanum.Runtime.Save.SaveLoadPanelMode.Load,
                    "Load Game reuses production M6 authority");
                controller.Open(GameUiScreen.MainMenu);
                view.Synchronize();

                view.Model.Submit(RetailMainMenuCommand.Options);
                Check(controller.Screen == GameUiScreen.Options, "Options reuses production controller");
                controller.Open(GameUiScreen.MainMenu);
                view.Synchronize();
                view.Model.Submit(RetailMainMenuCommand.Credits);
                Check(view.Model.State == RetailMainMenuState.Notice, "Credits fails closed through bounded notice");
                view.Model.Escape();
                view.Model.Submit(RetailMainMenuCommand.Multiplayer);
                Check(view.Model.State == RetailMainMenuState.Notice,
                    "Multiplayer remains present and does not fabricate gameplay");
                view.Model.Escape();
                view.Model.Submit(RetailMainMenuCommand.ExitGame);
                Check(view.Model.State == RetailMainMenuState.QuitConfirmation, "Exit uses source confirmation");
                view.Model.Submit(RetailMainMenuCommand.CancelQuit);
                Check(ReferenceEquals(playerBefore,
                        presenter.GetComponent<Arcanum.Runtime.World.WorldMapSessionCoordinator>().PlayerState),
                    "menu presentation does not replace session authority");

                Directory.CreateDirectory(fixtureRoot);
                var retail = RetailUiAssetResolver.CreateProduction();
                var enhanced = new EnhancedUiAssetResolver(fixtureRoot, _ => { });
                fixtureSkin = new UiSkinResolver(retail, enhanced);
                Check(retail.TryResolve(new UiAssetKey(329), out UiResolvedAsset original),
                    "retail Main Menu asset resolves for Enhanced proof");
                WriteFixture(enhanced.GetReplacementPath(original), original.LogicalSize * 4);
                view.Bind(controller, () => { }, fixtureSkin);
                Rect[] beforeSkinSwitch = view.Buttons.Select(button => button.SourceRect).ToArray();
                OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Enhanced);
                Check(view.BackgroundAsset?.ResolvedSkin == UiAssetSkin.Enhanced
                      && view.BackgroundAsset.Texture.width == 3200
                      && view.BackgroundAsset.Texture.height == 2400,
                    "generated exact-4x Enhanced Main Menu replacement appears");
                Check(view.Model.State == RetailMainMenuState.TopLevel
                      && view.Buttons.Select(button => button.SourceRect).SequenceEqual(beforeSkinSwitch),
                    "Enhanced keeps menu state and logical geometry");
                OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Original);
                Check(view.BackgroundAsset?.ResolvedSkin == UiAssetSkin.Original
                      && view.Model.State == RetailMainMenuState.TopLevel,
                    "Original retail pixels return without state loss");

                foreach ((int width, int height) in new[] { (1920, 1080), (2560, 1440), (3840, 2160) })
                {
                    UiLogicalMapping mapping = UiLogicalMapping.ForResolution(width, height);
                    Rect composition = mapping.LogicalToScreen(new Rect(0f, 0f, 800f, 600f));
                    Check(Mathf.Approximately(composition.width / composition.height, 4f / 3f)
                          && composition.width <= width && composition.height <= height,
                        $"{width}x{height} preserves centered 4:3 composition");
                }

                view.Bind(controller, () => { });
                controller.Open(GameUiScreen.MainMenu);
                view.Synchronize();
                RetailMainMenuCursorView cursor = Require(Object.FindFirstObjectByType<RetailMainMenuCursorView>(),
                    "source cursor presenter");
                Check(cursor.CurrentAsset?.Key.SourceId == 0
                      && cursor.CurrentAsset.LogicalSize == new Vector2Int(24, 27),
                    "retail cursor 0 and hotspot presentation");

                Debug.Log("UI-B PHYSICAL VALIDATION: PASS; source=329/331; flow=Main>Single Player>New Game>"
                          + "Character Creation and Main>Single Player>Load; options/credits/multiplayer/exit=PASS; "
                          + "skin=Original>Enhanced exact-4x>Original; resolutions=1080p/1440p/4K; "
                          + "duplicates=0; authority=preserved. Play Mode remains open for visual comparison.");
            }
            catch (Exception exception)
            {
                Debug.LogError("UI-B PHYSICAL VALIDATION: FAIL: " + exception);
            }
            finally
            {
                OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Original);
                fixtureSkin?.Dispose();
                if (Directory.Exists(fixtureRoot)) Directory.Delete(fixtureRoot, recursive: true);
            }
        }

        private static T Require<T>(T value, string description) where T : class
        {
            if (value == null) throw new InvalidOperationException("Missing " + description + ".");
            return value;
        }

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("Failed: " + description + ".");
        }

        private static void WriteFixture(string path, Vector2Int size)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var texture = new Texture2D(size.x, size.y, TextureFormat.RGBA32, mipChain: false);
            try
            {
                var row = Enumerable.Repeat(new Color32(172, 36, 80, 255), size.x).ToArray();
                for (int y = 0; y < size.y; y++) texture.SetPixels32(0, y, size.x, 1, row);
                texture.SetPixel(0, 0, Color.clear);
                texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(texture); }
        }
    }
}
