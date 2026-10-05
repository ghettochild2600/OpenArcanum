using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

internal sealed class UiCTestExecution : ScriptableObject, ICallbacks
{
    private static TestRunnerApi _api;
    private static UiCTestExecution _callbacks;
    private string _label;

    [MenuItem("OpenArcanum/UI-C/Run Focused EditMode Tests", false, 1)]
    private static void RunFocused()
        => Run(new Filter { testMode = TestMode.EditMode, categoryNames = new[] { "UICGameplayHud" } },
            "UI-C focused EditMode");

    [MenuItem("OpenArcanum/UI-C/Run UI-A Regression Tests", false, 2)]
    private static void RunUiA()
        => Run(new Filter { testMode = TestMode.EditMode, categoryNames = new[] { "UIACommonRuntime" } },
            "UI-A source-runtime regression");

    [MenuItem("OpenArcanum/UI-C/Run M12C Regression Tests", false, 3)]
    private static void RunM12C()
        => Run(new Filter { testMode = TestMode.EditMode, categoryNames = new[] { "M12CFullGameUi" } },
            "M12C controller regression");

    [MenuItem("OpenArcanum/UI-C/Run M8I Regression Tests", false, 4)]
    private static void RunM8I()
        => Run(new Filter { testMode = TestMode.EditMode, categoryNames = new[] { "M8ICombatUI" } },
            "M8I combat UI regression");

    [MenuItem("OpenArcanum/UI-C/Run Player Navigation Regression Tests", false, 5)]
    private static void RunNavigation()
        => Run(new Filter { testMode = TestMode.EditMode, categoryNames = new[] { "PlayerNavigation" } },
            "player navigation/input regression");

    [MenuItem("OpenArcanum/UI-C/Run Keyboard Regression Tests", false, 6)]
    private static void RunKeyboard()
        => Run(new Filter { testMode = TestMode.EditMode, categoryNames = new[] { "M13AKeyboardInput" } },
            "production keyboard regression");

    [MenuItem("OpenArcanum/UI-C/Run UI-B Regression Tests", false, 7)]
    private static void RunUiB()
        => Run(new Filter { testMode = TestMode.EditMode, categoryNames = new[] { "UIBMainMenu" } },
            "UI-B Main Menu regression");

    [MenuItem("OpenArcanum/UI-C/Run Complete EditMode Tests", false, 8)]
    private static void RunComplete()
        => Run(new Filter { testMode = TestMode.EditMode }, "UI-C complete EditMode");

    private static void Run(Filter filter, string label)
    {
        _api = CreateInstance<TestRunnerApi>();
        _callbacks = CreateInstance<UiCTestExecution>();
        _callbacks._label = label;
        _api.RegisterCallbacks(_callbacks);
        _api.Execute(new ExecutionSettings(filter) { runSynchronously = true });
    }

    public void RunStarted(ITestAdaptor testsToRun) { }
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result) { }

    public void RunFinished(ITestResultAdaptor result)
    {
        Debug.Log($"UI-C TEST RESULT [{_label}]: pass={result.PassCount}; fail={result.FailCount}; "
                  + $"skip={result.SkipCount}; inconclusive={result.InconclusiveCount}; status={result.ResultState}");
        _api.UnregisterCallbacks(_callbacks);
        DestroyImmediate(_callbacks);
        DestroyImmediate(_api);
        _callbacks = null;
        _api = null;
    }
}
