using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

internal sealed class UiBTestExecution : ScriptableObject, ICallbacks
{
    private static TestRunnerApi _api;
    private static UiBTestExecution _callbacks;
    private string _label;

    [MenuItem("OpenArcanum/UI-B/Run Focused EditMode Tests", false, 1)]
    private static void RunFocused()
        => Run(new Filter { testMode = TestMode.EditMode, categoryNames = new[] { "UIBMainMenu" } },
            "UI-B focused EditMode");

    [MenuItem("OpenArcanum/UI-B/Run UI-A Regression Tests", false, 2)]
    private static void RunUiA()
        => Run(new Filter { testMode = TestMode.EditMode, categoryNames = new[] { "UIACommonRuntime" } },
            "UI-A focused regression");

    [MenuItem("OpenArcanum/UI-B/Run M12C Regression Tests", false, 3)]
    private static void RunM12C()
        => Run(new Filter { testMode = TestMode.EditMode, categoryNames = new[] { "M12CFullGameUi" } },
            "M12C menu/controller regression");

    [MenuItem("OpenArcanum/UI-B/Run Player Startup Regression Tests", false, 4)]
    private static void RunStartup()
        => Run(new Filter { testMode = TestMode.EditMode, categoryNames = new[] { "PlayerStartup" } },
            "production player-startup regression");

    [MenuItem("OpenArcanum/UI-B/Run Complete EditMode Tests", false, 6)]
    private static void RunComplete()
        => Run(new Filter { testMode = TestMode.EditMode }, "UI-B complete EditMode");

    private static void Run(Filter filter, string label)
    {
        _api = CreateInstance<TestRunnerApi>();
        _callbacks = CreateInstance<UiBTestExecution>();
        _callbacks._label = label;
        _api.RegisterCallbacks(_callbacks);
        _api.Execute(new ExecutionSettings(filter) { runSynchronously = true });
    }

    public void RunStarted(ITestAdaptor testsToRun) { }
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result) { }

    public void RunFinished(ITestResultAdaptor result)
    {
        Debug.Log($"UI-B TEST RESULT [{_label}]: pass={result.PassCount}; fail={result.FailCount}; "
                  + $"skip={result.SkipCount}; inconclusive={result.InconclusiveCount}; status={result.ResultState}");
        _api.UnregisterCallbacks(_callbacks);
        DestroyImmediate(_callbacks);
        DestroyImmediate(_api);
        _callbacks = null;
        _api = null;
    }
}
