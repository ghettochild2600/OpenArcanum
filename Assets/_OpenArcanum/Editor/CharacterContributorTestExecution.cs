using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

internal sealed class CharacterContributorTestExecution : ScriptableObject, ICallbacks
{
    private static TestRunnerApi _api;
    private static CharacterContributorTestExecution _callbacks;
    private string _label;

    [MenuItem("OpenArcanum/Contributors/Run Focused EditMode Tests %#F11")]
    private static void RunFocused()
        => Run(new Filter
        {
            testMode = TestMode.EditMode,
            categoryNames = new[]
            {
                "CharacterContributors", "M4ACharacterAttributes", "M4CCharacterProgression",
                "M4DDerivedCharacterStats", "M6A", "M10AMagicRuntime", "M10BTechnologyRuntime",
                "M12BCharacterCreationNewGame",
            },
        }, "contributor focused EditMode");

    [MenuItem("OpenArcanum/Contributors/Run Complete EditMode Tests %#F12")]
    private static void RunComplete()
        => Run(new Filter { testMode = TestMode.EditMode }, "contributor complete EditMode");

    private static void Run(Filter filter, string label)
    {
        _api = CreateInstance<TestRunnerApi>();
        _callbacks = CreateInstance<CharacterContributorTestExecution>();
        _callbacks._label = label;
        _api.RegisterCallbacks(_callbacks);
        _api.Execute(new ExecutionSettings(filter) { runSynchronously = true });
    }

    public void RunStarted(ITestAdaptor testsToRun) { }
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result) { }

    public void RunFinished(ITestResultAdaptor result)
    {
        Debug.Log($"CHARACTER CONTRIBUTOR TEST RESULT [{_label}]: pass={result.PassCount}; "
                  + $"fail={result.FailCount}; skip={result.SkipCount}; "
                  + $"inconclusive={result.InconclusiveCount}; status={result.ResultState}");
        _api.UnregisterCallbacks(_callbacks);
        DestroyImmediate(_callbacks);
        DestroyImmediate(_api);
        _callbacks = null;
        _api = null;
    }
}
