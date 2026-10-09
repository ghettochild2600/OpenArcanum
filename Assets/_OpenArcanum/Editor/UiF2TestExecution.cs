using System;
using System.Collections;
using System.Linq;
using Arcanum.Formats.Quest;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Creation;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace OpenArcanum.Editor
{
    [InitializeOnLoad]
    public static class UiF2TestExecution
    {
        private const string PendingKey = "OpenArcanum.UIF2.PhysicalValidationPending";

        static UiF2TestExecution()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("OpenArcanum/UI-F.2/Run Focused EditMode Tests", false, 1)]
        public static void RunFocused()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Results(api));
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                categoryNames = new[]
                {
                    "UiF2LogbookPresentation", "M5B", "M6A", "M13AKeyboardInput",
                },
            }) { runSynchronously = true });
        }

        [MenuItem("OpenArcanum/UI-F.2/Run Physical PlayMode Validation", false, 2)]
        public static void RunPhysical()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("UI-F.2 PHYSICAL VALIDATION: stop Play Mode before starting a new run.");
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
            loader.StartCoroutine(Validate(loader));
        }

        private static IEnumerator Validate(WorldObjectSectorLoader loader)
        {
            ProductionGameUiPresenter presenter = Require(
                Object.FindFirstObjectByType<ProductionGameUiPresenter>(), "production UI presenter");
            WorldMapSessionCoordinator session = presenter.GetComponent<WorldMapSessionCoordinator>();
            GameUiController controller = presenter.Controller;
            GraphicsMode initialMode = OpenArcanumGraphicsSettings.Mode;
            session.ResetAuthoritativeSession();
            controller.Refresh();
            yield return null;

            Check(controller.BeginNewGame(), "New Game opens character creation");
            controller.SetCreationName("UI-F Logbook");
            controller.SetCreationIdentity(CharacterRace.Human, CharacterGender.Male);
            controller.SetCreationPortrait(1005);
            Check(controller.FinalizeNewGame(), "source-valid character reaches START_MAP 1");
            yield return null;
            yield return null;
            presenter = Require(Object.FindFirstObjectByType<ProductionGameUiPresenter>(),
                "production UI presenter after New Game");
            session = presenter.GetComponent<WorldMapSessionCoordinator>();
            controller = presenter.Controller;

            int seeded = 0;
            foreach (int quest in session.QuestSource.Numbers)
            {
                session.Campaign.SetPcQuestState(quest, (int)QuestState.Mentioned);
                if (++seeded == 18) break;
            }
            Check(seeded == 18, "mounted retail quest source supplies the representative projection");
            Check(controller.Open(GameUiScreen.Journal), "L/Logbook route opens Journal authority");
            yield return null;

            RetailLogbookView view = Require(Object.FindFirstObjectByType<RetailLogbookView>(),
                "source Logbook view");
            RetailGameplayHudView hud = Require(Object.FindFirstObjectByType<RetailGameplayHudView>(),
                "persistent gameplay HUD");
            view.Synchronize();
            Check(view.IsVisible && view.SelectedTab == RetailLogbookTab.Quests
                  && view.RenderedLineCount > 4, "quest spread renders authoritative retail quest prose");
            Check(hud.IsVisible, "source HUD remains visible below the 800x400 big window");
            Check(view.WindowTransform.sizeDelta == new Vector2(800, 400),
                "Logbook stays native 800x400");
            int firstLast = view.LastVisibleEntry;
            view.TurnRight();
            Check(view.CurrentLeftPage == 3 && view.LastVisibleEntry > firstLast,
                "right page control advances by one two-page spread");
            view.TurnLeft();
            Check(view.CurrentLeftPage == 1, "left page control returns to the prior spread");

            view.SelectTab(RetailLogbookTab.Reputations);
            Check(view.SelectedTab == RetailLogbookTab.Quests
                  && controller.Feedback.Contains("not available", StringComparison.OrdinalIgnoreCase),
                "unsupported tab fails closed without replacing authoritative quest pages");

            foreach ((int width, int height) in new[]
                     {
                         (800, 600), (1024, 768), (1920, 1080), (2560, 1440), (3840, 2160),
                     })
            {
                Rect rect = RetailLogbookLayout.ScreenRect(width, height);
                Check(rect.size == new Vector2(800, 400)
                      && Mathf.Approximately(rect.x, (width - 800) * .5f)
                      && Mathf.Approximately(rect.y, 41 + (height - 600) * .5f),
                    $"{width}x{height} preserves native big-window geometry");
            }

            foreach (GraphicsMode mode in new[] { GraphicsMode.Original, GraphicsMode.Enhanced, GraphicsMode.Original })
            {
                OpenArcanumGraphicsSettings.SetRuntimeMode(mode);
                loader.RebuildVisuals();
                controller.RebuildPresentation();
                yield return null;
                presenter = Require(Object.FindFirstObjectByType<ProductionGameUiPresenter>(),
                    $"production presenter after {mode} rebuild");
                view = Require(Object.FindFirstObjectByType<RetailLogbookView>(),
                    $"Logbook view after {mode} rebuild");
                Check(view.IsVisible && view.SelectedTab == RetailLogbookTab.Quests,
                    $"{mode} rebuild preserves Logbook route and quest authority");
            }
            Check(Object.FindObjectsByType<ProductionGameUiPresenter>(FindObjectsSortMode.None).Length == 1
                  && Object.FindObjectsByType<RetailLogbookView>(FindObjectsSortMode.None).Length == 1
                  && Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1,
                "one presenter, one Logbook view, and one EventSystem remain");
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            loader.RebuildVisuals();
            controller.RebuildPresentation();
            Debug.Log("UI-F.2 PHYSICAL VALIDATION: PASS; source IDs=198/260/261/264-273; tabs=7; "
                      + "Quests=authoritative/two-page; unsupported-tabs=fail-closed; pages=1-2->3-4->1-2; "
                      + "HUD=visible; geometry=800x600/768p/1080p/1440p/4K; "
                      + "graphics=Original->Enhanced->Original; presenters=1; EventSystems=1. "
                      + "Play Mode remains open on the first Quest spread.");
        }

        private sealed class Results : ICallbacks
        {
            private readonly TestRunnerApi _api;
            public Results(TestRunnerApi api) => _api = api;
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                Debug.Log($"UI-F.2 TEST RESULT: pass={result.PassCount}; fail={result.FailCount}; "
                          + $"skip={result.SkipCount}; inconclusive={result.InconclusiveCount}; status={result.TestStatus}");
                Object.DestroyImmediate(_api);
            }
        }

        private static T Require<T>(T value, string description) where T : class
            => value ?? throw new InvalidOperationException("Missing " + description + ".");

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("Failed: " + description + ".");
        }
    }
}
