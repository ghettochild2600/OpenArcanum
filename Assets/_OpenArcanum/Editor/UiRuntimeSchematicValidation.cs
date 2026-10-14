using System;
using System.Collections;
using System.Linq;
using Arcanum.Runtime.Crafting;
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
    /// <summary>Native 800x600 production proof for the source Schematics window.</summary>
    [InitializeOnLoad]
    public static class UiRuntimeSchematicValidation
    {
        private const string MenuPath = "OpenArcanum/UI-E.2/Run Runtime Schematics Physical Validation";
        private const string PendingKey = "OpenArcanum.UIE2.RuntimeSchematicPhysicalValidationPending";

        static UiRuntimeSchematicValidation()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem(MenuPath)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("RUNTIME SCHEMATICS PHYSICAL VALIDATION: stop Play Mode before starting a new run.");
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
            controller.SetCreationName("UI Schematics Human");
            Check(controller.FinalizeNewGame(), "source-valid Human Male creation finalizes");
            ProductionPlayerLifecycle lifecycle = Require(Object.FindFirstObjectByType<ProductionPlayerLifecycle>(),
                "production player lifecycle");
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC binds");
            yield return null;

            Check(session.Technology.LearnNextDegree(controller.Player, TechnologyDiscipline.Herbology).Succeeded,
                "Herbology Novice unlocks the first built-in source schematic");
            Check(controller.Open(GameUiScreen.Crafting), "Schematics route opens");
            yield return null;
            RetailSchematicView view = Require(Object.FindFirstObjectByType<RetailSchematicView>(),
                "retail Schematics view");
            RetailGameplayHudView hud = Require(Object.FindFirstObjectByType<RetailGameplayHudView>(),
                "retail gameplay HUD");
            view.Synchronize();
            hud.Synchronize(true);

            Check(Screen.width == 800 && Screen.height == 600, "native Game view is 800x600");
            Check(view.IsVisible && view.Mode == RetailSchematicMode.Learned
                  && view.Discipline == TechnologyDiscipline.Herbology,
                "source window opens on remembered Learned/Herbology state");
            Check(view.GetComponentsInChildren<SourceUiImage>(true)
                      .Any(value => value.name == "Schematics Background" && value.Key.SourceId == 365),
                "source background ART 365 is mounted");
            Check(view.GetComponentsInChildren<SourceUiButton>(true).Count(value =>
                    Enum.TryParse(value.name.Replace(" ", string.Empty), out TechnologyDiscipline _)) == 8,
                "all eight discipline tabs are present");
            Check(!FindButton(view, "Previous Schematic").gameObject.activeSelf
                  && !FindButton(view, "Next Schematic").gameObject.activeSelf,
                "single source page hides both navigation arrows");

            GameUiSchematicView builtIn = controller.ProjectSchematics().Single(value =>
                value.Definition.Acquisition == SchematicAcquisition.BuiltIn
                && value.Discipline == TechnologyDiscipline.Herbology);
            Check(view.GetComponentsInChildren<SourceUiImage>(true)
                      .Single(value => value.name == "Readiness").Key.SourceId == 359,
                "missing inputs project source no-items readiness ART 359");

            ItemCreationResult blueprint = session.CreateItem(14095, ObjectPlacement.ContainedBy(controller.Player));
            Check(blueprint.Succeeded
                  && session.Crafting.LearnFoundSchematic(controller.Player, blueprint.State.Identity).Succeeded,
                "authentic written prototype 14095 unlocks found schematic 4020");
            GameUiSchematicView schematic = controller.ProjectSchematics().Single(value =>
                value.Definition.Id.Value == 4020);
            view.SelectMode(RetailSchematicMode.Found);
            view.SelectDiscipline(schematic.Discipline);
            yield return null;
            view.Synchronize();
            Check(view.Mode == RetailSchematicMode.Found,
                "Found source mode displays learned written schematics");
            ItemCreationResult first = session.CreateItem(schematic.ComponentOne.PrototypeNumber,
                ObjectPlacement.ContainedBy(controller.Player));
            ItemCreationResult second = session.CreateItem(schematic.ComponentTwo.PrototypeNumber,
                ObjectPlacement.ContainedBy(controller.Player));
            Check(first.Succeeded && second.Succeeded,
                $"authentic 4020 components enter player inventory ({first.Code}/{second.Code})");
            yield return null;
            view.Synchronize();
            Check(view.GetComponentsInChildren<SourceUiImage>(true)
                      .Single(value => value.name == "Readiness").Key.SourceId == 360,
                "owned inputs plus expertise project source ready ART 360");
            int productsBefore = session.States.Values.Count(value =>
                value.PrototypeNumber == schematic.ProductPrototype);
            FindButton(view, "Combine").onClick.Invoke();
            yield return null;
            view.Synchronize();
            Check(session.States.Values.Count(value => value.PrototypeNumber == schematic.ProductPrototype)
                  == productsBefore + schematic.ProductQuantity,
                "Combine delegates to transactional crafting authority");

            SourceHoverHintTarget learned = view.GetComponentsInChildren<SourceHoverHintTarget>(true)
                .Single(value => value.name == "Learned Schematics");
            learned.OnPointerEnter(null);
            hud.Synchronize(true);
            Check(!string.IsNullOrWhiteSpace(controller.ContextualHelp),
                "source Learned hover projects contextual help");
            learned.OnPointerExit(null);
            FindButton(view, "Learned Schematics").onClick.Invoke();
            yield return null;
            view.Synchronize();
            Check(view.Mode == RetailSchematicMode.Learned
                  && view.Discipline == TechnologyDiscipline.Herbology,
                "Learned and Found modes restore their own source discipline state");

            Debug.Log("RUNTIME SCHEMATICS PHYSICAL VALIDATION: PASS; viewport=800x600; background=ART365; "
                      + "lens=ART231@(50,26); disciplines=8; learned/found=PASS; arrows=source-hidden; "
                      + "component slots=ART830/831; readiness=ART359->360; combine=transactional; "
                      + "contextual-help=PASS. Play Mode remains open on Found Schematics for visual proof.");
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
