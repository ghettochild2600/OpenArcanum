using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

internal sealed class M3BTestExecution : ScriptableObject, ICallbacks
{
    private static TestRunnerApi _api;
    private static M3BTestExecution _callbacks;

    [MenuItem("OpenArcanum/M3B/Run Focused EditMode Tests")]
    private static void RunFocused() => RunCategory("M3B", "M3B focused");

    [MenuItem("OpenArcanum/M3B/Run Complete EditMode Tests")]
    private static void RunAll() => Run(new Filter { testMode = TestMode.EditMode }, "complete EditMode");

    [MenuItem("OpenArcanum/M3B/Run Required Regression Tests")]
    private static void RunRequiredRegressions()
    {
        RunCategory("M3B", "M3B focused");
        RunTest("Arcanum.Formats.Tests.M3AInventoryStateTests", "M3A inventory state");
        RunCategory("M2BUseScript", "M2B SAP_USE");
        RunCategory("M2AInteraction", "M2A interaction");
        RunCategory("PlayerNavigation", "player navigation");
        RunCategory("M1Lifecycle", "M1A lifecycle");
        RunCategory("M1BNavigation", "M1B traversal");
        RunTest("Arcanum.Formats.Tests.WorldSessionStateTests", "world session state");
        RunTest("Arcanum.Formats.Tests.PortalArtResolverTests", "portal ART resolver");
    }

    private static void RunCategory(string category, string label)
        => Run(new Filter { testMode = TestMode.EditMode, categoryNames = new[] { category } }, label);

    private static void RunTest(string testName, string label)
        => Run(new Filter { testMode = TestMode.EditMode, testNames = new[] { testName } }, label);

    private static void Run(Filter filter, string label)
    {
        _api = CreateInstance<TestRunnerApi>();
        _callbacks = CreateInstance<M3BTestExecution>();
        _callbacks._label = label;
        _api.RegisterCallbacks(_callbacks);
        _api.Execute(new ExecutionSettings(filter) { runSynchronously = true });
    }

    private string _label;
    public void RunStarted(ITestAdaptor testsToRun) { }
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result) { }

    public void RunFinished(ITestResultAdaptor result)
    {
        Debug.Log($"M3B TEST RESULT [{_label}]: pass={result.PassCount}; fail={result.FailCount}; " +
                  $"skip={result.SkipCount}; inconclusive={result.InconclusiveCount}; status={result.ResultState}");
        _api.UnregisterCallbacks(_callbacks);
        DestroyImmediate(_callbacks);
        DestroyImmediate(_api);
        _callbacks = null;
        _api = null;
    }
}
