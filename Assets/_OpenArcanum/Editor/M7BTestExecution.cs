using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

internal sealed class M7BTestExecution : ScriptableObject, ICallbacks
{
    private static TestRunnerApi _api;
    private static M7BTestExecution _callbacks;
    private string _label;
    [MenuItem("OpenArcanum/M7B/Run Focused EditMode Tests")]
    private static void RunFocused() => RunCategory("M7BAreaEntrance", "focused EditMode");

    [MenuItem("OpenArcanum/M7B/Run Required Regression Tests")]
    private static void RunRequired()
    {
        RunCategory("M7ALocalTransition", "M7A local transition");
        RunCategory("M6C", "M6C manual save/load");
        RunCategory("M6B", "M6B slots/migration");
        RunCategory("M6A", "M6A session save/load");
        RunCategory("PlayerNavigation", "player navigation");
        RunCategory("M2BUseScript", "interaction SAP_USE");
        RunCategory("M2AInteraction", "interaction kernel");
        RunTest("Arcanum.Formats.Tests.WorldSessionStateTests", "world session state");
    }

    [MenuItem("OpenArcanum/M7B/Run Complete EditMode Tests")]
    private static void RunAll() => Run(new Filter { testMode = TestMode.EditMode }, "complete EditMode");

    private static void RunCategory(string category, string label)
        => Run(new Filter { testMode = TestMode.EditMode, categoryNames = new[] { category } }, label);
    private static void RunTest(string testName, string label)
        => Run(new Filter { testMode = TestMode.EditMode, testNames = new[] { testName } }, label);
    private static void Run(Filter filter, string label)
    {
        _api = CreateInstance<TestRunnerApi>(); _callbacks = CreateInstance<M7BTestExecution>();
        _callbacks._label = label;
        _api.RegisterCallbacks(_callbacks);
        _api.Execute(new ExecutionSettings(filter) { runSynchronously = true });
    }
    public void RunStarted(ITestAdaptor testsToRun) { }
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result) { }
    public void RunFinished(ITestResultAdaptor result)
    {
        Debug.Log($"M7B TEST RESULT [{_label}]: pass={result.PassCount}; fail={result.FailCount}; "
            + $"skip={result.SkipCount}; inconclusive={result.InconclusiveCount}; status={result.ResultState}");
        _api.UnregisterCallbacks(_callbacks); DestroyImmediate(_callbacks); DestroyImmediate(_api);
        _api = null; _callbacks = null;
    }
}
