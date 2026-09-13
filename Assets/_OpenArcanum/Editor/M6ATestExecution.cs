using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

internal sealed class M6ATestExecution : ScriptableObject, ICallbacks
{
    private static TestRunnerApi _api;
    private static M6ATestExecution _callbacks;
    private string _label;

    [MenuItem("OpenArcanum/M6A/Run Focused EditMode Tests")]
    private static void RunFocused() => RunCategory("M6A", "M6A focused");

    [MenuItem("OpenArcanum/M6A/Run Complete EditMode Tests")]
    private static void RunAll() => Run(new Filter { testMode = TestMode.EditMode }, "complete EditMode");

    [MenuItem("OpenArcanum/M6A/Run Required Regression Tests")]
    private static void RunRequiredRegressions()
    {
        RunCategory("M6A", "M6A focused");
        RunCategory("M5C", "M5C trainer dialogue");
        RunCategory("M5B", "M5B quest/journal");
        RunCategory("M5A", "M5A dialogue/quest");
        RunCategory("M4DDerivedCharacterStats", "M4D derived character stats");
        RunCategory("M4CCharacterProgression", "M4C character progression");
        RunCategory("M4BCharacterVitality", "M4B character vitality");
        RunCategory("M4ACharacterAttributes", "M4A character attributes");
        RunCategory("M3ECapacity", "M3E capacity");
        RunCategory("M3D", "M3D stack transactions");
        RunCategory("M3C", "M3C equipment state");
        RunCategory("M3B", "M3B inventory commands");
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
        _callbacks = CreateInstance<M6ATestExecution>();
        _callbacks._label = label;
        _api.RegisterCallbacks(_callbacks);
        _api.Execute(new ExecutionSettings(filter) { runSynchronously = true });
    }

    public void RunStarted(ITestAdaptor testsToRun) { }
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result) { }

    public void RunFinished(ITestResultAdaptor result)
    {
        Debug.Log($"M6A TEST RESULT [{_label}]: pass={result.PassCount}; fail={result.FailCount}; " +
                  $"skip={result.SkipCount}; inconclusive={result.InconclusiveCount}; status={result.ResultState}");
        _api.UnregisterCallbacks(_callbacks);
        DestroyImmediate(_callbacks);
        DestroyImmediate(_api);
        _callbacks = null;
        _api = null;
    }
}
