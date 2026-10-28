using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

internal sealed class LocalMapTestExecution : ScriptableObject, ICallbacks
{
    private static TestRunnerApi _api;
    private static LocalMapTestExecution _callbacks;

    [MenuItem("OpenArcanum/Local Map/Run Focused EditMode Tests _F8")]
    private static void RunFocused()
    {
        _api = CreateInstance<TestRunnerApi>();
        _callbacks = CreateInstance<LocalMapTestExecution>();
        _api.RegisterCallbacks(_callbacks);
        _api.Execute(new ExecutionSettings(new Filter
        {
            testMode = TestMode.EditMode,
            categoryNames = new[] { "LocalMap" },
        }) { runSynchronously = true });
    }

    public void RunStarted(ITestAdaptor testsToRun) { }
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result)
    {
        if (result.FailCount > 0)
            Debug.LogError($"LOCAL MAP TEST FAILURE: {result.FullName}\n{result.Message}\n{result.StackTrace}");
    }

    public void RunFinished(ITestResultAdaptor result)
    {
        Debug.Log($"LOCAL MAP TEST RESULT: pass={result.PassCount}; fail={result.FailCount}; "
                  + $"skip={result.SkipCount}; inconclusive={result.InconclusiveCount}; status={result.ResultState}");
        _api.UnregisterCallbacks(_callbacks);
        DestroyImmediate(_callbacks);
        DestroyImmediate(_api);
        _callbacks = null;
        _api = null;
    }
}
