using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

internal sealed class UiETestExecution : ScriptableObject, ICallbacks
{
    private static TestRunnerApi _api;
    private static UiETestExecution _callbacks;
    private string _label;

    [MenuItem("OpenArcanum/UI-E/Run Focused EditMode Tests", false, 1)]
    private static void RunFocused()
        => Run(new Filter { testMode = TestMode.EditMode, categoryNames = new[] { "UIEInventoryEquipment" } },
            "UI-E focused EditMode");

    [MenuItem("OpenArcanum/UI-E/Run UI-C + UI-E Tests", false, 2)]
    private static void RunUiCAndUiE()
        => Run(new Filter { testMode = TestMode.EditMode,
                categoryNames = new[] { "UICGameplayHud", "UIEInventoryEquipment" } },
            "UI-C/UI-E focused EditMode");

    [MenuItem("OpenArcanum/UI-E/Run M3C Equipment Regression", false, 3)]
    private static void RunM3C()
        => Run(new Filter { testMode = TestMode.EditMode, categoryNames = new[] { "M3C" } },
            "M3C equipment regression");

    [MenuItem("OpenArcanum/UI-E/Run M3E Capacity Regression", false, 4)]
    private static void RunM3E()
        => Run(new Filter { testMode = TestMode.EditMode, categoryNames = new[] { "M3ECapacity" } },
            "M3E inventory-capacity regression");

    [MenuItem("OpenArcanum/UI-E/Run M12C UI Regression", false, 5)]
    private static void RunM12C()
        => Run(new Filter { testMode = TestMode.EditMode, categoryNames = new[] { "M12CFullGameUi" } },
            "M12C controller regression");

    [MenuItem("OpenArcanum/UI-E/Run Complete EditMode Tests", false, 6)]
    private static void RunComplete()
        => Run(new Filter { testMode = TestMode.EditMode }, "UI-E complete EditMode");

    private static void Run(Filter filter, string label)
    {
        _api = CreateInstance<TestRunnerApi>();
        _callbacks = CreateInstance<UiETestExecution>();
        _callbacks._label = label;
        _api.RegisterCallbacks(_callbacks);
        _api.Execute(new ExecutionSettings(filter) { runSynchronously = true });
    }

    public void RunStarted(ITestAdaptor testsToRun) { }
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result) { }

    public void RunFinished(ITestResultAdaptor result)
    {
        Debug.Log($"UI-E TEST RESULT [{_label}]: pass={result.PassCount}; fail={result.FailCount}; "
                  + $"skip={result.SkipCount}; inconclusive={result.InconclusiveCount}; status={result.ResultState}");
        _api.UnregisterCallbacks(_callbacks);
        DestroyImmediate(_callbacks);
        DestroyImmediate(_api);
        _callbacks = null;
        _api = null;
    }
}
