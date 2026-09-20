using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

internal sealed class M7ETestExecution : ScriptableObject, ICallbacks
{
    private static M7ETestExecution _runner;
    private TestRunnerApi _api;
    private Filter[] _filters;
    private string[] _labels;
    private int _index;
    private string _label;

    [MenuItem("OpenArcanum/M7E/Run Focused EditMode Tests")]
    private static void RunFocused() => RunCategory("M7EWorldMapTravel", "focused EditMode");

    [MenuItem("OpenArcanum/M7E/Run Required Regression Tests")]
    private static void RunRequired()
    {
        RunMany(
            new[]
            {
                Category("M7DWorldMapDestination"), Category("M7CAreaDiscovery"),
                Category("M7BAreaEntrance"), Category("M7ALocalTransition"), Category("M6C"),
                Category("M6B"), Category("M6A"), Category("PlayerNavigation"),
                Test("Arcanum.Formats.Tests.WorldSessionStateTests"),
            },
            new[]
            {
                "M7D destination selection", "M7C area discovery", "M7B area entrance",
                "M7A local transition", "M6C manual save/load", "M6B slots/migration",
                "M6A session save/load", "player navigation", "world session state",
            });
    }

    [MenuItem("OpenArcanum/M7E/Run Complete EditMode Tests")]
    private static void RunAll() => Run(new Filter { testMode = TestMode.EditMode }, "complete EditMode");

    private static void RunCategory(string category, string label)
        => Run(Category(category), label);

    private static void RunTest(string testName, string label)
        => Run(Test(testName), label);

    private static Filter Category(string category)
        => new() { testMode = TestMode.EditMode, categoryNames = new[] { category } };

    private static Filter Test(string testName)
        => new() { testMode = TestMode.EditMode, testNames = new[] { testName } };

    private static void Run(Filter filter, string label)
    {
        RunMany(new[] { filter }, new[] { label });
    }

    private static void RunMany(Filter[] filters, string[] labels)
    {
        if (_runner != null)
        {
            Debug.LogWarning("An M7E EditMode test run is already active.");
            return;
        }
        _runner = CreateInstance<M7ETestExecution>();
        _runner._filters = filters;
        _runner._labels = labels;
        _runner.RunNext();
    }

    private void RunNext()
    {
        if (_index >= _filters.Length)
        {
            if (_runner == this) _runner = null;
            DestroyImmediate(this);
            return;
        }

        _label = _labels[_index];
        _api = CreateInstance<TestRunnerApi>();
        _api.RegisterCallbacks(this);
        _api.Execute(new ExecutionSettings(_filters[_index]) { runSynchronously = true });
    }

    public void RunStarted(ITestAdaptor testsToRun) { }
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result)
    {
        if (result.HasChildren || result.TestStatus != TestStatus.Failed) return;
        Debug.LogError($"M7E TEST FAILURE [{_label}]: {result.FullName}\n{result.Message}\n{result.StackTrace}");
    }

    public void RunFinished(ITestResultAdaptor result)
    {
        Debug.Log($"M7E TEST RESULT [{_label}]: pass={result.PassCount}; fail={result.FailCount}; "
                  + $"skip={result.SkipCount}; inconclusive={result.InconclusiveCount}; status={result.ResultState}");
        if (_api != null) _api.UnregisterCallbacks(this);
        if (_api != null) DestroyImmediate(_api);
        _api = null;
        _index++;
        EditorApplication.delayCall += RunNext;
    }
}
