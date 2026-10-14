using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

internal sealed class OptionsTestExecution : ScriptableObject, ICallbacks
{
    private static TestRunnerApi _api;
    private static OptionsTestExecution _callbacks;

    [MenuItem("OpenArcanum/Options/Run Focused EditMode Tests")]
    private static void RunFocused()
    {
        _api = CreateInstance<TestRunnerApi>();
        _callbacks = CreateInstance<OptionsTestExecution>();
        _api.RegisterCallbacks(_callbacks);
        _api.Execute(new ExecutionSettings(new Filter
        {
            testMode = TestMode.EditMode,
            categoryNames = new[] { "UiGOptions" },
        }) { runSynchronously = true });
    }

    public void RunStarted(ITestAdaptor testsToRun) { }
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result) { }

    public void RunFinished(ITestResultAdaptor result)
    {
        Debug.Log($"OPTIONS TEST RESULT: pass={result.PassCount}; fail={result.FailCount}; "
                  + $"skip={result.SkipCount}; inconclusive={result.InconclusiveCount}; status={result.ResultState}");
        _api.UnregisterCallbacks(_callbacks);
        DestroyImmediate(_callbacks);
        DestroyImmediate(_api);
        _callbacks = null;
        _api = null;
    }
}
