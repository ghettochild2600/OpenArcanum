using System;
using System.Collections;
using System.Linq;
using Arcanum.Runtime.Magic;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using OpenArcanum.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OpenArcanum.Editor
{
    /// <summary>Native 800x600 production proof for the source Character Editor Magic subwindow.</summary>
    [InitializeOnLoad]
    public static class UiRuntimeMagicValidation
    {
        private const string MenuPath = "OpenArcanum/UI-E.2/Run Runtime Magic Physical Validation";
        private const string PendingKey = "OpenArcanum.UIE2.RuntimeMagicPhysicalValidationPending";

        static UiRuntimeMagicValidation()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem(MenuPath)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("RUNTIME MAGIC PHYSICAL VALIDATION: stop Play Mode before starting a new run.");
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
            controller.SetCreationName("UI Magic Human");
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
            Check(controller.Open(GameUiScreen.Magic), "Magic route opens the active Character Editor");
            view.SelectCollege(SpellCollege.Earth);
            yield return null;
            view.Synchronize();
            hud.Synchronize(true);

            Check(view.IsVisible && view.BackgroundAsset?.Key.SourceId == 22,
                "Magic shares the source active Character Editor shell ID 22");
            SourceUiImage magicWindow = view.GetComponentsInChildren<SourceUiImage>(true)
                .Single(value => value.name == "Magic Window");
            Check(magicWindow.Key.SourceId == 31, "Magic mounts source subwindow ART 31");
            Check(view.GetComponentsInChildren<SourceUiButton>(true).Count(value =>
                    Enum.TryParse(value.name, out SpellCollege _)) == 16,
                "all sixteen source college controls are present");
            Check(view.GetComponentsInChildren<SourceUiImage>(true).Count(value =>
                    value.name.StartsWith("Spell ", StringComparison.Ordinal)
                    && value.name.EndsWith(" Icon", StringComparison.Ordinal)) == 5,
                "selected college projects five source spell icons");
            Check(view.GetComponentsInChildren<SourceUiBitmapText>(true).Count(value =>
                    value.name.StartsWith("Spell Level ", StringComparison.Ordinal)) == 5,
                "selected college projects five source minimum-level rows");

            int points = session.Progression.GetUnspentCharacterPoints(controller.Player);
            FindButton(view, "Increase Spell").onClick.Invoke();
            yield return null;
            view.Synchronize();
            Check(session.Magic.GetKnownCollegeRank(controller.Player, SpellCollege.Earth) == 1
                  && session.Progression.GetUnspentCharacterPoints(controller.Player) == points - 1,
                "source plus learns the next sequential spell through existing Magic/Progression authority");

            SourceHoverHintTarget spellHint = view.GetComponentsInChildren<SourceHoverHintTarget>(true)
                .Single(value => value.name == "Spell 15 Hover");
            spellHint.OnPointerEnter(null);
            hud.Synchronize(true);
            Check(controller.ContextualHelpTitle == session.SpellPresentation.SpellName(15)
                  && controller.ContextualHelp.Contains(session.SpellPresentation.SpellDescription(15))
                  && controller.ContextualHelpIconSourceId == 449,
                "spell hover projects source title, description, and Earth college icon into the HUD panel");
            spellHint.OnPointerExit(null);

            FindButton(view, nameof(SpellCollege.Nature)).onClick.Invoke();
            yield return null;
            view.Synchronize();
            Check(view.SelectedCollege == SpellCollege.Nature
                  && view.GetComponentsInChildren<SourceUiBitmapText>(true)
                      .Single(value => value.name == "College Name").Text
                  == session.SpellPresentation.CollegeName(SpellCollege.Nature),
                "clicking a college changes only the selected source presentation");

            Debug.Log("RUNTIME MAGIC PHYSICAL VALIDATION: PASS; viewport=800x600; shell=ART22; "
                      + "subwindow=ART31@(503,104 desktop); colleges=16; selected rows=5; "
                      + "spell plus=sequential Magic/Progression transaction; source hover title/body/icon=PASS; "
                      + "college click=PASS. Play Mode remains open on the Magic screen for visual proof.");
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
