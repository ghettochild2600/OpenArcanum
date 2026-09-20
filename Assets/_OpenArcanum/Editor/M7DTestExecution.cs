using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

internal sealed class M7DTestExecution : ScriptableObject, ICallbacks
{
    private static TestRunnerApi _api;
    private static M7DTestExecution _callbacks;
    private string _label;

    [MenuItem("OpenArcanum/M7D/Run Focused EditMode Tests")]
    private static void RunFocused() => RunCategory("M7DWorldMapDestination", "focused EditMode");

    [MenuItem("OpenArcanum/M7D/Run Required Regression Tests")]
    private static void RunRequired()
    {
        RunCategory("M7CAreaDiscovery", "M7C area discovery");
        RunCategory("M7BAreaEntrance", "M7B area entrance");
        RunCategory("M7ALocalTransition", "M7A local transition");
        RunCategory("M6C", "M6C manual save/load");
        RunCategory("M6B", "M6B slots/migration");
        RunCategory("M6A", "M6A session save/load");
        RunCategory("M5C", "M5C trainer dialogue");
        RunCategory("M5B", "M5B quest/journal");
        RunCategory("M5A", "M5A dialogue/quest");
        RunCategory("PlayerNavigation", "player navigation");
        RunTest("Arcanum.Formats.Tests.WorldSessionStateTests", "world session state");
    }

    [MenuItem("OpenArcanum/M7D/Run Complete EditMode Tests")]
    private static void RunAll() => Run(new Filter { testMode = TestMode.EditMode }, "complete EditMode");

    private static void RunCategory(string category, string label)
        => Run(new Filter { testMode = TestMode.EditMode, categoryNames = new[] { category } }, label);
    private static void RunTest(string testName, string label)
        => Run(new Filter { testMode = TestMode.EditMode, testNames = new[] { testName } }, label);
    private static void Run(Filter filter, string label)
    {
        _api = CreateInstance<TestRunnerApi>();
        _callbacks = CreateInstance<M7DTestExecution>();
        _callbacks._label = label;
        _api.RegisterCallbacks(_callbacks);
        _api.Execute(new ExecutionSettings(filter) { runSynchronously = true });
    }

    public void RunStarted(ITestAdaptor testsToRun) { }
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result)
    {
        if (result.HasChildren || result.TestStatus != TestStatus.Failed) return;
        Debug.LogError($"M7D TEST FAILURE [{_label}]: {result.FullName}\n{result.Message}\n{result.StackTrace}");
    }
    public void RunFinished(ITestResultAdaptor result)
    {
        Debug.Log($"M7D TEST RESULT [{_label}]: pass={result.PassCount}; fail={result.FailCount}; "
                  + $"skip={result.SkipCount}; inconclusive={result.InconclusiveCount}; status={result.ResultState}");
        _api.UnregisterCallbacks(_callbacks);
        DestroyImmediate(_callbacks);
        DestroyImmediate(_api);
        _api = null;
        _callbacks = null;
    }
}
