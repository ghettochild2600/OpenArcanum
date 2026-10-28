using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

internal sealed class PoisonRuntimeTestExecution : ScriptableObject, ICallbacks
{
    private static TestRunnerApi _api;
    private static PoisonRuntimeTestExecution _callbacks;

    [MenuItem("OpenArcanum/Poison/Run Focused EditMode Tests %#F10")]
    private static void RunFocused()
    {
        _api = CreateInstance<TestRunnerApi>();
        _callbacks = CreateInstance<PoisonRuntimeTestExecution>();
        _api.RegisterCallbacks(_callbacks);
        _api.Execute(new ExecutionSettings(new Filter
        {
            testMode = TestMode.EditMode,
            categoryNames = new[] { "PoisonRuntime", "M8BTurnBasedCombat", "M8CRangedCombat" },
        }) { runSynchronously = true });
    }

    public void RunStarted(ITestAdaptor testsToRun) { }
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result) { }

    public void RunFinished(ITestResultAdaptor result)
    {
        Debug.Log($"POISON RUNTIME TEST RESULT: pass={result.PassCount}; fail={result.FailCount}; "
                  + $"skip={result.SkipCount}; inconclusive={result.InconclusiveCount}; status={result.ResultState}");
        _api.UnregisterCallbacks(_callbacks);
        DestroyImmediate(_callbacks);
        DestroyImmediate(_api);
        _callbacks = null;
        _api = null;
    }
}
