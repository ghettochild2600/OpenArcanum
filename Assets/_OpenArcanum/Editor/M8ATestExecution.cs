using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

internal sealed class M8ATestExecution : ScriptableObject, ICallbacks
{
    private static M8ATestExecution _runner;
    private TestRunnerApi _api;
    private Filter[] _filters;
    private string[] _labels;
    private int _index;
    private string _label;
    private bool _active;

    [MenuItem("OpenArcanum/M8A/Run Focused EditMode Tests")]
    private static void RunFocused() => Run(Category("M8ACoreCombatState"), "focused EditMode");

    [MenuItem("OpenArcanum/M8B/Run Focused EditMode Tests")]
    private static void RunM8BFocused() => Run(Category("M8BTurnBasedCombat"), "M8B focused EditMode");

    [MenuItem("OpenArcanum/M8C/Run Focused EditMode Tests")]
    private static void RunM8CFocused() => Run(Category("M8CRangedCombat"), "M8C focused EditMode");

    [MenuItem("OpenArcanum/M8D/Run Focused EditMode Tests")]
    private static void RunM8DFocused() => Run(Category("M8DDefeatState"), "M8D focused EditMode");

    [MenuItem("OpenArcanum/M8D/Run Required Regression Tests")]
    private static void RunM8DRequired()
    {
        RunMany(
            new[]
            {
                Category("M8CRangedCombat"), Category("M8BTurnBasedCombat"),
                Category("M8ACoreCombatState"), Category("M4BCharacterVitality"),
                Category("PlayerNavigation"), Category("M6A"),
                Test("Arcanum.Formats.Tests.WorldSessionStateTests"),
            },
            new[]
            {
                "M8C ranged combat", "M8B turn-based combat", "M8A core combat",
                "M4B vitality", "player navigation", "M6A session save/load",
                "world session state",
            });
    }

    [MenuItem("OpenArcanum/M8D/Run Complete EditMode Tests")]
    private static void RunM8DAll() => Run(new Filter { testMode = TestMode.EditMode }, "M8D complete EditMode");

    [MenuItem("OpenArcanum/M8C/Run M8B Regression Tests")]
    private static void RunM8CRegression() => Run(Category("M8BTurnBasedCombat"), "M8B regression EditMode");

    [MenuItem("OpenArcanum/M8C/Run Required Regression Tests")]
    private static void RunM8CRequired()
    {
        RunMany(
            new[]
            {
                Category("M8BTurnBasedCombat"), Category("M8ACoreCombatState"),
                Category("M7EWorldMapTravel"), Category("M7DWorldMapDestination"),
                Category("M7CAreaDiscovery"), Category("M7BAreaEntrance"),
                Category("M7ALocalTransition"), Category("M6C"), Category("M6B"), Category("M6A"),
                Category("M4BCharacterVitality"), Category("M4CCharacterProgression"),
                Category("M4DDerivedCharacterStats"), Category("M3C"), Category("M3D"),
                Category("PlayerNavigation"), Test("Arcanum.Formats.Tests.WorldSessionStateTests"),
            },
            new[]
            {
                "M8B turn-based combat", "M8A core combat", "M7E world-map travel",
                "M7D destination selection", "M7C area discovery", "M7B area entrance",
                "M7A local transition", "M6C manual save/load", "M6B slots/migration",
                "M6A session save/load", "M4B vitality", "M4C progression", "M4D derived stats",
                "M3C equipment", "M3D stacks", "player navigation", "world session state",
            });
    }

    [MenuItem("OpenArcanum/M8C/Run Complete EditMode Tests")]
    private static void RunM8CAll() => Run(new Filter { testMode = TestMode.EditMode }, "M8C complete EditMode");

    [MenuItem("OpenArcanum/M8B/Run Required Regression Tests")]
    private static void RunM8BRequired()
    {
        RunMany(
            new[]
            {
                Category("M8ACoreCombatState"), Category("M7EWorldMapTravel"),
                Category("M7DWorldMapDestination"), Category("M7CAreaDiscovery"),
                Category("M7BAreaEntrance"), Category("M7ALocalTransition"), Category("M6C"),
                Category("M6B"), Category("M6A"), Category("M4BCharacterVitality"),
                Category("M4CCharacterProgression"), Category("M4DDerivedCharacterStats"),
                Category("PlayerNavigation"), Category("M2AInteraction"), Category("M2BUseScript"),
                Test("Arcanum.Formats.Tests.WorldSessionStateTests"),
            },
            new[]
            {
                "M8A core combat", "M7E world-map travel", "M7D destination selection",
                "M7C area discovery", "M7B area entrance", "M7A local transition",
                "M6C manual save/load", "M6B slots/migration", "M6A session save/load",
                "M4B vitality", "M4C progression", "M4D derived stats", "player navigation",
                "M2A interaction", "M2B use-script interaction", "world session state",
            });
    }

    [MenuItem("OpenArcanum/M8B/Run Complete EditMode Tests")]
    private static void RunM8BAll() => Run(new Filter { testMode = TestMode.EditMode }, "M8B complete EditMode");

    [MenuItem("OpenArcanum/M8A/Run Required Regression Tests")]
    private static void RunRequired()
    {
        RunMany(
            new[]
            {
                Category("M7EWorldMapTravel"), Category("M7DWorldMapDestination"),
                Category("M7CAreaDiscovery"), Category("M7BAreaEntrance"),
                Category("M7ALocalTransition"), Category("M6C"), Category("M6B"), Category("M6A"),
                Category("M4BCharacterVitality"), Category("M4DDerivedCharacterStats"),
                Category("PlayerNavigation"), Category("M2AInteraction"), Category("M2BUseScript"),
                Test("Arcanum.Formats.Tests.WorldSessionStateTests"),
            },
            new[]
            {
                "M7E world-map travel", "M7D destination selection", "M7C area discovery",
                "M7B area entrance", "M7A local transition", "M6C manual save/load",
                "M6B slots/migration", "M6A session save/load", "M4B vitality",
                "M4D derived stats", "player navigation", "M2A interaction",
                "M2B use-script interaction", "world session state",
            });
    }

    [MenuItem("OpenArcanum/M8A/Run Complete EditMode Tests")]
    private static void RunAll() => Run(new Filter { testMode = TestMode.EditMode }, "complete EditMode");

    private static Filter Category(string category)
        => new() { testMode = TestMode.EditMode, categoryNames = new[] { category } };

    private static Filter Test(string testName)
        => new() { testMode = TestMode.EditMode, testNames = new[] { testName } };

    private static void Run(Filter filter, string label)
        => RunMany(new[] { filter }, new[] { label });

    private static void RunMany(Filter[] filters, string[] labels)
    {
        if (_runner != null && _runner._active)
        {
            Debug.LogWarning("An M8A EditMode test run is already active.");
            return;
        }
        if (_runner == null)
        {
            _runner = CreateInstance<M8ATestExecution>();
            _runner.hideFlags = HideFlags.HideAndDontSave;
            _runner._api = CreateInstance<TestRunnerApi>();
            _runner._api.hideFlags = HideFlags.HideAndDontSave;
            _runner._api.RegisterCallbacks(_runner);
        }
        _runner._active = true;
        _runner._index = 0;
        _runner._filters = filters;
        _runner._labels = labels;
        _runner.RunNext();
    }

    private void RunNext()
    {
        if (_index >= _filters.Length)
        {
            _active = false;
            _filters = null;
            _labels = null;
            _label = null;
            _index = 0;
            return;
        }
        _label = _labels[_index];
        _api.Execute(new ExecutionSettings(_filters[_index]) { runSynchronously = true });
    }

    public void RunStarted(ITestAdaptor testsToRun) { }
    public void TestStarted(ITestAdaptor test) { }

    public void TestFinished(ITestResultAdaptor result)
    {
        if (result.HasChildren || result.TestStatus != TestStatus.Failed) return;
        Debug.LogError($"M8A TEST FAILURE [{_label}]: {result.FullName}\n{result.Message}\n{result.StackTrace}");
    }

    public void RunFinished(ITestResultAdaptor result)
    {
        Debug.Log($"M8A TEST RESULT [{_label}]: pass={result.PassCount}; fail={result.FailCount}; "
                  + $"skip={result.SkipCount}; inconclusive={result.InconclusiveCount}; status={result.ResultState}");
        _index++;
        EditorApplication.delayCall += RunNext;
    }
}
