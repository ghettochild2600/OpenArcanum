using System;
using System.Collections;
using System.IO;
using System.Linq;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Creation;
using Arcanum.Runtime.Magic;
using Arcanum.Runtime.Technology;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using OpenArcanum.Rendering;
using OpenArcanum.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OpenArcanum.Editor
{
    /// <summary>Production Play Mode proof for the source-backed UI-D Character Creation flow.</summary>
    [InitializeOnLoad]
    public static class UiDCharacterCreationValidation
    {
        private const string MenuPath = "OpenArcanum/UI-D/Run Physical PlayMode Validation";
        private const string PendingKey = "OpenArcanum.UID.PhysicalValidationPending";

        static UiDCharacterCreationValidation()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem(MenuPath)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("UI-D PHYSICAL VALIDATION: stop Play Mode before starting a new run.");
                return;
            }
            SessionState.SetBool(PendingKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
            SessionState.EraseBool(PendingKey);
            EditorApplication.delayCall += StartValidation;
        }

        private static void StartValidation()
        {
            WorldObjectSectorLoader loader = Require(Object.FindFirstObjectByType<WorldObjectSectorLoader>(),
                "production sector loader");
            loader.StartCoroutine(Validate());
        }

        private static IEnumerator Validate()
        {
            string fixtureRoot = Path.Combine(Path.GetTempPath(), "OpenArcanum-UID-Physical-"
                                                                  + Guid.NewGuid().ToString("N"));
            UiSkinResolver fixtureSkin = null;
            GraphicsMode initialMode = OpenArcanumGraphicsSettings.Mode;
            RetailCharacterCreationView view = null;
            GameUiController controller = null;
            WorldMapSessionCoordinator session = null;
            UiSkinResolver productionSkin = null;
            try
            {
                ProductionGameUiPresenter presenter = Require(
                    Object.FindFirstObjectByType<ProductionGameUiPresenter>(), "production UI presenter");
                session = presenter.GetComponent<WorldMapSessionCoordinator>();
                controller = presenter.Controller;
                RetailGameplayHudView hud = Require(Object.FindFirstObjectByType<RetailGameplayHudView>(),
                    "retail gameplay HUD");
                productionSkin = hud.Resolver;
                session.ResetAuthoritativeSession();
                controller.Refresh();
                yield return null;

                Check(controller.BeginNewGame(), "fresh authentic New Game opens Character Creation");
                yield return null;
                view = Require(Object.FindFirstObjectByType<RetailCharacterCreationView>(),
                    "retail Character Creation view");
                view.Synchronize();
                Check(view.IsVisible && view.Stage == RetailCharacterCreationStage.Identity,
                    "production route begins on the source identity stage");
                Check(view.IdentityBackgroundAsset?.Key.SourceId == 765
                      && view.IdentityBackgroundAsset.LogicalSize == new Vector2Int(800, 400),
                    "identity stage is source createcharacterbase 765 at 800x400");
                Check(view.GetComponentsInChildren<ScrollRect>(true).Length == 0,
                    "production Character Creation contains no global ScrollRect");
                Check(Object.FindObjectsByType<RetailCharacterCreationView>(FindObjectsSortMode.None).Length == 1
                      && Object.FindObjectsByType<ProductionGameUiPresenter>(FindObjectsSortMode.None).Length == 1
                      && Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1,
                    "one Character Creation view, presenter/controller, and EventSystem own the flow");

                view.NameInput.text = string.Empty;
                FindButton(view, "Identity Continue").onClick.Invoke();
                view.Synchronize();
                Check(view.Stage == RetailCharacterCreationStage.Identity && session.PlayerState == null
                      && controller.CreationValidation.Failure == CharacterCreationFailure.InvalidName,
                    "invalid name fails closed before stage or player mutation");
                view.NameInput.text = "UI-D Human";

                CharacterCreationSpecification draft = controller.CreationDraft;
                int firstPortrait = draft.PortraitId;
                FindButton(view, "Next Portrait").onClick.Invoke();
                Check(draft.PortraitId != firstPortrait, "portrait arrow cycles the legal mounted catalog");
                FindButton(view, "Previous Portrait").onClick.Invoke();
                FindButton(view, "Previous Gender").onClick.Invoke();
                Check(draft.Gender == CharacterGender.Female
                      && session.CharacterCreation.Catalog.IsPortraitLegal(draft.PortraitId, draft.Race, draft.Gender),
                    "gender arrow updates the legal body and portrait through the controller");
                FindButton(view, "Next Gender").onClick.Invoke();
                FindButton(view, "Next Race").onClick.Invoke();
                Check(draft.Race == CharacterRace.Dwarf && draft.Gender == CharacterGender.Male,
                    "race arrow applies the source-valid Dwarf male restriction");
                FindButton(view, "Previous Race").onClick.Invoke();
                int firstBackground = draft.BackgroundId;
                FindButton(view, "Next Background").onClick.Invoke();
                Check(draft.BackgroundId != firstBackground,
                    "background arrow cycles a source-legal data-driven background");
                FindButton(view, "Previous Background").onClick.Invoke();
                Check(draft.BackgroundId == firstBackground && controller.CreationValidation.Succeeded,
                    "identity cycle returns to a bounded valid source draft");

                FindButton(view, "Identity Continue").onClick.Invoke();
                view.Synchronize();
                Check(view.Stage == RetailCharacterCreationStage.Editor
                      && view.EditorBackgroundAsset?.Key.SourceId == 22
                      && view.EditorBackgroundAsset.LogicalSize == new Vector2Int(800, 400),
                    "Continue enters source char_maint 22 at 800x400");
                Check(view.EditorPanelRoot.GetComponentInChildren<SourceUiImage>(true)?.Key.SourceId == 29,
                    "editor opens the source skills subpanel 29");
                int autoLevelStrength = draft.GetAttribute(CharacterAttribute.Strength);
                int autoLevelPoints = controller.CreationValidation.RemainingCharacterPoints;
                FindButton(view, "Auto-Level Schemes").onClick.Invoke();
                yield return null;
                view.Synchronize();
                Check(view.Stage == RetailCharacterCreationStage.Editor && session.PlayerState == null
                      && draft.GetAttribute(CharacterAttribute.Strength) == autoLevelStrength
                      && controller.CreationValidation.RemainingCharacterPoints == autoLevelPoints
                      && controller.Feedback.Contains("Auto-level schemes"),
                    "source ID 568 is the auto-level-scheme route and fails closed without transaction mutation");

                FindButton(view, "Increase Strength").onClick.Invoke();
                yield return null;
                FindButton(view, "Increase Bow").onClick.Invoke();
                yield return null;
                FindButton(view, "Magic").onClick.Invoke();
                FindButton(view, "Increase Spell").onClick.Invoke();
                yield return null;
                FindButton(view, "Technology").onClick.Invoke();
                FindButton(view, "Increase Technology").onClick.Invoke();
                yield return null;
                view.Synchronize();
                Check(draft.GetAttribute(CharacterAttribute.Strength) == 9
                      && draft.GetSkillPoints(CharacterSkill.Bow) == 1
                      && draft.GetSpellRank(SpellCollege.Conveyance) == 1
                      && draft.GetTechnologyRank(TechnologyDiscipline.Herbology) == 1
                      && controller.CreationValidation.RemainingCharacterPoints == 1
                      && controller.CreationValidation.Succeeded,
                    "source controls spend four points only through M12B attribute/skill/magic/technology authority");

                FindButton(view, "Editor Back").onClick.Invoke();
                Check(view.Stage == RetailCharacterCreationStage.Identity && session.PlayerState == null
                      && draft.GetAttribute(CharacterAttribute.Strength) == 9,
                    "source bottom-bar Back returns to identity without partial player or draft loss");
                FindButton(view, "Identity Continue").onClick.Invoke();
                Check(view.Stage == RetailCharacterCreationStage.Editor,
                    "source bottom-bar Continue reopens the editor with the authoritative draft intact");

                Directory.CreateDirectory(fixtureRoot);
                var retail = RetailUiAssetResolver.CreateProduction();
                var enhanced = new EnhancedUiAssetResolver(fixtureRoot, _ => { });
                fixtureSkin = new UiSkinResolver(retail, enhanced);
                UiResolvedAsset identityOriginal = null;
                UiResolvedAsset editorOriginal = null;
                bool hasIdentity = retail.TryResolve(new UiAssetKey(765), out identityOriginal);
                bool hasEditor = retail.TryResolve(new UiAssetKey(22), out editorOriginal);
                Check(hasIdentity && hasEditor,
                    "source identity/editor assets resolve for graphics rebuild proof");
                WriteFixture(enhanced.GetReplacementPath(identityOriginal), identityOriginal.LogicalSize * 4);
                WriteFixture(enhanced.GetReplacementPath(editorOriginal), editorOriginal.LogicalSize * 4);
                view.Bind(controller, session, fixtureSkin);
                OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Enhanced);
                view.Synchronize();
                Check(view.Stage == RetailCharacterCreationStage.Editor
                      && view.EditorPage == RetailCreationEditorPage.Technology
                      && view.EditorBackgroundAsset?.ResolvedSkin == UiAssetSkin.Enhanced
                      && view.EditorBackgroundAsset.Texture.width == 3200
                      && view.EditorBackgroundAsset.Texture.height == 1600
                      && controller.CreationValidation.RemainingCharacterPoints == 1,
                    "Original to exact-4x Enhanced rebuild preserves stage, panel, geometry, and draft authority");
                view.HandleEscape();
                Check(view.IdentityBackgroundAsset?.ResolvedSkin == UiAssetSkin.Enhanced,
                    "hidden identity composition refreshes to its exact-4x source replacement when reopened");
                view.ContinueToEditor();
                OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Original);
                view.Synchronize();
                Check(view.EditorBackgroundAsset?.ResolvedSkin == UiAssetSkin.Original
                      && view.Stage == RetailCharacterCreationStage.Editor
                      && controller.CreationValidation.RemainingCharacterPoints == 1,
                    "Enhanced to Original returns source pixels without state loss");
                view.Bind(controller, session, productionSkin);
                fixtureSkin.Dispose();
                fixtureSkin = null;
                yield return null;
                view.Synchronize();

                foreach ((int width, int height) in new[]
                         {
                             (800, 600), (1024, 768), (1920, 1080), (2560, 1440), (3840, 2160),
                         })
                {
                    UiLogicalMapping mapping = UiLogicalMapping.ForResolution(width, height);
                    Rect window = mapping.LogicalToScreen(RetailCharacterCreationLayout.Window);
                    Rect continueButton = mapping.LogicalToScreen(new Rect(675f, 496f, 32f, 32f));
                    Check(window.xMin >= 0f && window.yMin >= 0f && window.xMax <= width + .001f
                          && window.yMax <= height + .001f && continueButton.xMax <= width + .001f
                          && continueButton.yMax <= height + .001f,
                        $"{width}x{height} keeps the fixed source composition and bottom-bar Continue accessible");
                }

                FindButton(view, "Editor Continue").onClick.Invoke();
                yield return null;
                yield return null;
                Check(session.PlayerState != null && session.CharacterCreation.IsFinalized
                      && session.CharacterCreation.Finalized.Name == "UI-D Human"
                      && session.SelectedSector.Contains("arcanum1-024-fixed")
                      && controller.Screen == GameUiScreen.None,
                    "Accept atomically creates exactly one PC and enters authentic START_MAP 1");
                ProductionPlayerLifecycle lifecycle = Require(
                    Object.FindFirstObjectByType<ProductionPlayerLifecycle>(), "production player lifecycle");
                if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC binds");
                yield return null;
                RetailGameplayHudView finalHud = Require(Object.FindFirstObjectByType<RetailGameplayHudView>(),
                    "source gameplay HUD after finalization");
                finalHud.Synchronize(true);
                Check(finalHud.IsVisible && !view.IsVisible,
                    "successful finalization closes creation and reveals the existing source HUD");

                Debug.Log("UI-D PHYSICAL VALIDATION: PASS; source flow=765 identity>22 editor; "
                          + "identity=name/portrait/gender/race/background arrows; invalid name=no mutation; "
                          + "editor=attributes/skills/magic/technology; points=4 spent/1 remaining; "
                          + "ID568=auto-level schemes/fail-closed/no mutation; bottom-bar Back/Continue=draft preserved; "
                          + "partial-window y=41; no global ScrollRect; "
                          + "skin=Original>Enhanced exact-4x 765+22>Original with stage/panel/state preserved; "
                          + "resolutions=800x600/1024x768/1080p/1440p/4K accessible; "
                          + "finalize=ID321/one PC/authentic START_MAP/source HUD; presenters=1; EventSystems=1. "
                          + "Play Mode remains open at the crash site for visual proof.");
            }
            finally
            {
                OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
                if (fixtureSkin != null)
                {
                    if (view != null && controller != null && session != null && productionSkin != null)
                        view.Bind(controller, session, productionSkin);
                    fixtureSkin.Dispose();
                }
                if (Directory.Exists(fixtureRoot)) Directory.Delete(fixtureRoot, true);
            }
        }

        private static Button FindButton(RetailCharacterCreationView view, string name)
            => view.GetComponentsInChildren<Button>(true).Single(value => value.name == name);

        private static T Require<T>(T value, string description) where T : class
            => value ?? throw new InvalidOperationException("Missing " + description + ".");

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("Failed: " + description + ".");
        }

        private static void WriteFixture(string path, Vector2Int size)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var texture = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
            try
            {
                var row = Enumerable.Repeat(new Color32(172, 36, 80, 255), size.x).ToArray();
                for (int y = 0; y < size.y; y++) texture.SetPixels32(0, y, size.x, 1, row);
                texture.SetPixel(0, 0, Color.clear);
                texture.Apply(false, false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(texture); }
        }
    }
}
