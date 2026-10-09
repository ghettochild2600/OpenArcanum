using System;
using System.Collections;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Creation;
using Arcanum.Runtime.Dialogue;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using OpenArcanum.UI;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace OpenArcanum.Editor
{
    [InitializeOnLoad]
    public static class UiF1TestExecution
    {
        private const string PendingKey = "OpenArcanum.UIF1.PhysicalValidationPending";
        private static readonly ArcanumObjectId Virgil = Parse(
            "G_A09DCD63_7A15_D411_8F1D_00E02920220C");

        static UiF1TestExecution()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("OpenArcanum/UI-F.1/Run Focused EditMode Tests", false, 1)]
        public static void RunFocused()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Results(api));
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                categoryNames = new[]
                {
                    "UiF1DialoguePresentation", "M5A", "M5B", "M5C", "M13AKeyboardInput",
                },
            }) { runSynchronously = true });
        }

        [MenuItem("OpenArcanum/UI-F.1/Run Physical PlayMode Validation", false, 2)]
        public static void RunPhysical()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("UI-F.1 PHYSICAL VALIDATION: stop Play Mode before starting a new run.");
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
            session.ResetAuthoritativeSession();
            controller.Refresh();
            yield return null;

            Check(controller.BeginNewGame(), "authentic New Game opens character creation");
            controller.SetCreationName("UI-F Dialogue");
            controller.SetCreationIdentity(CharacterRace.Human, CharacterGender.Male);
            controller.SetCreationPortrait(1005);
            controller.SetCreationBackground(3);
            Check(controller.CreationValidation.Succeeded, "source-valid character is ready");
            Check(controller.FinalizeNewGame(), "source-valid character reaches START_MAP 1");
            yield return null;
            yield return null;

            presenter = Require(Object.FindFirstObjectByType<ProductionGameUiPresenter>(),
                "production UI presenter after New Game");
            session = presenter.GetComponent<WorldMapSessionCoordinator>();
            controller = presenter.Controller;
            Check(session.TryGetObjectState(Virgil, out PersistentObjectState virgil)
                  && virgil.DialogNum == 1324 && session.TryGetLoadedObject(Virgil, out _),
                "authentic loaded Virgil 1324 resolves");
            Check(session.Dialogue.Start(session.PlayerState.Identity, Virgil) == DialogueStartStatus.Started,
                "authentic Virgil conversation starts through M5 authority");
            controller.Refresh();
            yield return null;
            yield return null;

            RetailDialogueView view = Require(Object.FindFirstObjectByType<RetailDialogueView>(),
                "source dialogue view");
            RetailGameplayHudView hud = Require(Object.FindFirstObjectByType<RetailGameplayHudView>(),
                "persistent gameplay HUD");
            PlayerInputGate gate = presenter.GetComponent<PlayerInputGate>();
            view.Synchronize();
            Check(view.IsVisible && view.NpcBubbleVisible,
                "ordinary dialogue presents Virgil speech as a world bubble over the live scene");
            Check(view.CurrentResponseCount > 0
                  && view.CurrentResponseCount <= RetailDialogueLayout.MaximumResponses
                  && !view.OverflowDetected,
                "authentic responses fit the source five-row tc boundary");
            Check(controller.Screen == GameUiScreen.Dialogue && controller.IsModalOpen
                  && !controller.BlocksWorldInput && !gate.IsBlocked,
                "dialogue owns response precedence without blocking source world movement");
            Check(hud.IsVisible, "source top/bottom gameplay HUD remains present during ordinary dialogue");
            Check(view.ResponseRect.width == 400f && view.ResponseRect.height == 100f
                  && Mathf.Approximately(view.ResponseRect.yMax, Screen.height - 159f),
                "response strip is native 400x100 immediately above the native bottom HUD");
            Check(Object.FindObjectsByType<RetailDialogueView>(FindObjectsSortMode.None).Length == 1
                  && Object.FindObjectsByType<ProductionGameUiPresenter>(FindObjectsSortMode.None).Length == 1
                  && Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1,
                "one dialogue view, one production presenter, and one EventSystem remain active");

            foreach ((int width, int height) in new[]
                     {
                         (800, 600), (1024, 768), (1920, 1080), (2560, 1440), (3840, 2160),
                     })
            {
                Rect rect = RetailDialogueLayout.ResponseScreenRect(width, height);
                Check(rect.width == 400f && rect.height == 100f
                      && Mathf.Approximately(rect.x, (width - 400f) * .5f)
                      && Mathf.Approximately(rect.yMax, height - 159f),
                    $"{width}x{height} preserves native response/HUD geometry");
            }

            Debug.Log("UI-F.1 PHYSICAL VALIDATION: PASS; authentic Virgil 1324; NPC world bubble=visible; "
                      + $"responses={view.CurrentResponseCount}/5; strip=400x100 above HUD; "
                      + "world movement gate=open; source HUD=visible; resolutions=800x600/768p/1080p/1440p/4K; "
                      + "presenters=1; EventSystems=1. Play Mode remains open on the first Virgil response set.");
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
                Debug.Log($"UI-F.1 TEST RESULT: pass={result.PassCount}; fail={result.FailCount}; "
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

        private static ArcanumObjectId Parse(string value)
            => ArcanumObjectId.TryParsePersistent(value, out ArcanumObjectId parsed)
                ? parsed : throw new InvalidOperationException("Invalid source identity: " + value);
    }
}
