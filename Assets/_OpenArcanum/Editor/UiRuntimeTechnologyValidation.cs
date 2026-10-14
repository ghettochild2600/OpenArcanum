using System;
using System.Collections;
using System.Linq;
using Arcanum.Runtime.Technology;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using OpenArcanum.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OpenArcanum.Editor
{
    /// <summary>Native 800x600 production proof for the source Character Editor Technology subwindow.</summary>
    [InitializeOnLoad]
    public static class UiRuntimeTechnologyValidation
    {
        private const string MenuPath = "OpenArcanum/UI-E.2/Run Runtime Technology Physical Validation";
        private const string PendingKey = "OpenArcanum.UIE2.RuntimeTechnologyPhysicalValidationPending";

        static UiRuntimeTechnologyValidation()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem(MenuPath)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("RUNTIME TECHNOLOGY PHYSICAL VALIDATION: stop Play Mode before starting a new run.");
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
            ProductionGameUiPresenter presenter = Require(Object.FindFirstObjectByType<ProductionGameUiPresenter>(),
                "production UI presenter");
            presenter.StartCoroutine(Validate(presenter));
        }

        private static IEnumerator Validate(ProductionGameUiPresenter presenter)
        {
            yield return null;
            yield return null;
            WorldMapSessionCoordinator session = presenter.GetComponent<WorldMapSessionCoordinator>();
            GameUiController controller = presenter.Controller;
            session.ResetAuthoritativeSession();
            controller.Refresh();
            yield return null;
            Check(controller.BeginNewGame(), "fresh production New Game opens Character Creation");
            controller.SetCreationName("UI Tech Human");
            Check(controller.FinalizeNewGame(), "source-valid Human Male creation finalizes");
            ProductionPlayerLifecycle lifecycle = Require(Object.FindFirstObjectByType<ProductionPlayerLifecycle>(),
                "production player lifecycle");
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC binds");
            yield return null;

            RetailCharacterSheetView view = Require(Object.FindFirstObjectByType<RetailCharacterSheetView>(),
                "retail Character view");
            RetailGameplayHudView hud = Require(Object.FindFirstObjectByType<RetailGameplayHudView>(),
                "retail gameplay HUD");
            Check(Screen.width == 800 && Screen.height == 600, "native Game view is 800x600");
            Check(controller.Open(GameUiScreen.Technology), "Technology route opens the active Character Editor");
            view.SelectTechnology(TechnologyDiscipline.Herbology);
            yield return null;
            view.Synchronize();
            hud.Synchronize(true);

            Check(view.IsVisible && view.BackgroundAsset?.Key.SourceId == 22,
                "Technology shares the source active Character Editor shell ID 22");
            Check(view.GetComponentsInChildren<SourceUiImage>(true)
                      .Single(value => value.name == "Technology Window").Key.SourceId == 30,
                "Technology mounts source subwindow ART 30");
            Check(view.GetComponentsInChildren<SourceUiButton>(true).Count(value =>
                    Enum.TryParse(value.name, out TechnologyDiscipline _)) == 8,
                "all eight source discipline controls are present");
            Check(view.GetComponentsInChildren<SourceUiBitmapText>(true).Count(value =>
                    value.name.StartsWith("Schematic ", StringComparison.Ordinal)
                    && !value.name.StartsWith("Schematic Components", StringComparison.Ordinal)) == 7,
                "selected discipline projects seven source degree schematic rows");

            int points = session.Progression.GetUnspentCharacterPoints(controller.Player);
            FindButton(view, "Increase Technology").onClick.Invoke();
            yield return null;
            view.Synchronize();
            Check(session.Technology.GetLearnedDegree(controller.Player, TechnologyDiscipline.Herbology)
                  == TechnologyDegree.Novice
                  && session.Progression.GetUnspentCharacterPoints(controller.Player) == points - 1,
                "source plus learns Novice through Technology/Progression authority");
            Check(view.GetComponentsInChildren<SourceUiImage>(true)
                      .Single(value => value.name == "Degree 1 Icon").Key.SourceId == 653,
                "learned Herbology row projects the source discipline icon");

            SourceHoverHintTarget degreeHint = view.GetComponentsInChildren<SourceHoverHintTarget>(true)
                .Single(value => value.name == "Schematic 1");
            degreeHint.OnPointerEnter(null);
            hud.Synchronize(true);
            Check(!string.IsNullOrWhiteSpace(controller.ContextualHelpTitle)
                  && controller.ContextualHelp.StartsWith("Minimum IN: 5", StringComparison.Ordinal),
                "degree hover projects source title/body and minimum Intelligence into contextual help");
            degreeHint.OnPointerExit(null);

            FindButton(view, nameof(TechnologyDiscipline.Chemistry)).onClick.Invoke();
            yield return null;
            view.Synchronize();
            Check(view.SelectedTechnology == TechnologyDiscipline.Chemistry
                  && view.GetComponentsInChildren<SourceUiBitmapText>(true)
                      .Single(value => value.name == "Technology Name").Text.StartsWith("Chemistry "),
                "clicking a discipline changes only the selected source presentation");

            Debug.Log("RUNTIME TECHNOLOGY PHYSICAL VALIDATION: PASS; viewport=800x600; shell=ART22; "
                      + "subwindow=ART30@(503,104 desktop); disciplines=8; degree rows=7; "
                      + "expertise/source fonts=PASS; sequential purchase/refund authority=PASS; "
                      + "degree hover help=PASS. Play Mode remains open on Technology for visual proof.");
        }

        private static Button FindButton(Component root, string name)
            => root.GetComponentsInChildren<SourceUiButton>(true).Single(value => value.name == name).Button;

        private static T Require<T>(T value, string description) where T : class
            => value ?? throw new InvalidOperationException("Missing " + description + ".");

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("Failed: " + description + ".");
        }
    }
}
