using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

internal sealed class M7ATestExecution : ScriptableObject, ICallbacks
{
    private static TestRunnerApi _api;
    private static M7ATestExecution _callbacks;

    [MenuItem("OpenArcanum/M7A/Run Focused EditMode Tests")]
    private static void RunFocused()
    {
        _api = CreateInstance<TestRunnerApi>();
        _callbacks = CreateInstance<M7ATestExecution>();
        _api.RegisterCallbacks(_callbacks);
        _api.Execute(new ExecutionSettings(new Filter
        {
            testMode = TestMode.EditMode,
            categoryNames = new[] { "M7ALocalTransition" },
        }) { runSynchronously = true });
    }

    public void RunStarted(ITestAdaptor testsToRun) { }
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result) { }

    public void RunFinished(ITestResultAdaptor result)
    {
        Debug.Log($"M7A TEST RESULT [focused EditMode]: pass={result.PassCount}; fail={result.FailCount}; " +
                  $"skip={result.SkipCount}; inconclusive={result.InconclusiveCount}; status={result.ResultState}");
        _api.UnregisterCallbacks(_callbacks);
        DestroyImmediate(_callbacks);
        DestroyImmediate(_api);
        _callbacks = null;
        _api = null;
    }
}
