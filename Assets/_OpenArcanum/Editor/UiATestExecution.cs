using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

internal sealed class UiATestExecution : ScriptableObject, ICallbacks
{
    private static TestRunnerApi _api;
    private static UiATestExecution _callbacks;
    private string _label;

    [MenuItem("OpenArcanum/UI-A/Run Focused EditMode Tests", false, 1)]
    private static void RunFocused()
        => Run(new Filter
        {
            testMode = TestMode.EditMode,
            categoryNames = new[] { "UIACommonRuntime" },
        }, "UI-A focused EditMode");

    [MenuItem("OpenArcanum/UI-A/Run Complete EditMode Tests", false, 3)]
    private static void RunComplete()
        => Run(new Filter { testMode = TestMode.EditMode }, "UI-A complete EditMode");

    private static void Run(Filter filter, string label)
    {
        _api = CreateInstance<TestRunnerApi>();
        _callbacks = CreateInstance<UiATestExecution>();
        _callbacks._label = label;
        _api.RegisterCallbacks(_callbacks);
        _api.Execute(new ExecutionSettings(filter) { runSynchronously = true });
    }

    public void RunStarted(ITestAdaptor testsToRun) { }
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result) { }

    public void RunFinished(ITestResultAdaptor result)
    {
        Debug.Log($"UI-A TEST RESULT [{_label}]: pass={result.PassCount}; fail={result.FailCount}; " +
                  $"skip={result.SkipCount}; inconclusive={result.InconclusiveCount}; status={result.ResultState}");
        _api.UnregisterCallbacks(_callbacks);
        DestroyImmediate(_callbacks);
        DestroyImmediate(_api);
        _callbacks = null;
        _api = null;
    }
}
