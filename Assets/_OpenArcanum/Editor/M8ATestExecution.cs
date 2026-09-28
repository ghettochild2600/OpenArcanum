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

    [MenuItem("OpenArcanum/M11A/Run Focused EditMode Tests #&1")]
    private static void RunM11AFocused()
        => Run(Category("M11AEconomyVendor"), "M11A focused EditMode");

    [MenuItem("OpenArcanum/M11A/Run Economy Regression Tests #&2")]
    private static void RunM11ARegression()
    {
        RunMany(
            new[] { Category("M3A"), Category("M3D"), Category("M3E"), Category("M4CCharacterProgression"),
                Category("M4DDerivedCharacterStats"), Category("M6A") },
            new[] { "M3A inventory", "M3D stacks", "M3E capacity", "M4C progression",
                "M4D derived stats", "M6A save/load" });
    }

    [MenuItem("OpenArcanum/M11A/Run Complete EditMode Tests #&3")]
    private static void RunM11AAll()
        => Run(new Filter { testMode = TestMode.EditMode }, "M11A complete EditMode");

    [MenuItem("OpenArcanum/M8A/Run Focused EditMode Tests")]
    private static void RunFocused() => Run(Category("M8ACoreCombatState"), "focused EditMode");

    [MenuItem("OpenArcanum/M8B/Run Focused EditMode Tests")]
    private static void RunM8BFocused() => Run(Category("M8BTurnBasedCombat"), "M8B focused EditMode");

    [MenuItem("OpenArcanum/M8C/Run Focused EditMode Tests")]
    private static void RunM8CFocused() => Run(Category("M8CRangedCombat"), "M8C focused EditMode");

    [MenuItem("OpenArcanum/M8D/Run Focused EditMode Tests")]
    private static void RunM8DFocused() => Run(Category("M8DDefeatState"), "M8D focused EditMode");

    [MenuItem("OpenArcanum/M8F/Run Focused EditMode Tests")]
    private static void RunM8FFocused()
        => Run(Category("M8FCriticalResolution"), "M8F focused EditMode");

    [MenuItem("OpenArcanum/M8G Phase 1/Run Focused EditMode Tests")]
    private static void RunM8GPhase1Focused()
        => Run(Category("M8GCombatLoopPhase1"), "M8G Phase 1 focused EditMode");

    [MenuItem("OpenArcanum/M8G Phase 1/Run M8A-M8F Combat Regression Tests")]
    private static void RunM8GPhase1CombatRegression()
    {
        RunMany(
            new[]
            {
                Category("M8FCriticalResolution"), Category("M8EDeathConsequences"),
                Category("M8DDefeatState"), Category("M8CRangedCombat"),
                Category("M8BTurnBasedCombat"), Category("M8ACoreCombatState"),
            },
            new[]
            {
                "M8F critical resolution", "M8E death consequences", "M8D defeat state",
                "M8C ranged combat", "M8B turn-based combat", "M8A core combat",
            });
    }

    [MenuItem("OpenArcanum/M8G Phase 2/Run Focused EditMode Tests")]
    private static void RunM8GPhase2Focused()
        => Run(Category("M8GStructuredAttackPhase2"), "M8G Phase 2 focused EditMode");

    [MenuItem("OpenArcanum/M8G Phase 3/Run Focused EditMode Tests")]
    private static void RunM8GPhase3Focused()
        => Run(Category("M8GCoverMasterPhase3"), "M8G Phase 3 focused EditMode");

    [MenuItem("OpenArcanum/M8G Phase 3/Run Phase 1-2 and M8A-M8F Regression Tests")]
    private static void RunM8GPhase3CombatRegression()
    {
        RunMany(
            new[]
            {
                Category("M8GStructuredAttackPhase2"), Category("M8GCombatLoopPhase1"),
                Category("M8FCriticalResolution"), Category("M8EDeathConsequences"),
                Category("M8DDefeatState"), Category("M8CRangedCombat"),
                Category("M8BTurnBasedCombat"), Category("M8ACoreCombatState"),
            },
            new[]
            {
                "M8G Phase 2 structured attacks", "M8G Phase 1 combat loop",
                "M8F critical resolution", "M8E death consequences", "M8D defeat state",
                "M8C ranged combat", "M8B turn-based combat", "M8A core combat",
            });
    }

    [MenuItem("OpenArcanum/M8G Phase 4/Run Focused EditMode Tests")]
    private static void RunM8GPhase4Focused()
        => Run(Category("M8GBowCriticalDodgePhase4"), "M8G Phase 4 focused EditMode");

    [MenuItem("OpenArcanum/M8G Phase 4/Run Phase 1-3 and M8A-M8F Regression Tests")]
    private static void RunM8GPhase4CombatRegression()
    {
        RunMany(
            new[]
            {
                Category("M8GCoverMasterPhase3"), Category("M8GStructuredAttackPhase2"),
                Category("M8GCombatLoopPhase1"), Category("M8FCriticalResolution"),
                Category("M8EDeathConsequences"), Category("M8DDefeatState"),
                Category("M8CRangedCombat"), Category("M8BTurnBasedCombat"),
                Category("M8ACoreCombatState"),
            },
            new[]
            {
                "M8G Phase 3 cover/Bow Master", "M8G Phase 2 structured attacks",
                "M8G Phase 1 combat loop", "M8F critical resolution",
                "M8E death consequences", "M8D defeat state", "M8C ranged combat",
                "M8B turn-based combat", "M8A core combat",
            });
    }

    [MenuItem("OpenArcanum/M8G Phase 4/Run Complete EditMode Tests")]
    private static void RunM8GPhase4All()
        => Run(new Filter { testMode = TestMode.EditMode }, "M8G Phase 4 complete EditMode");

    [MenuItem("OpenArcanum/M8H/Run Focused EditMode Tests")]
    private static void RunM8HFocused()
        => Run(Category("M8HRealTimeCombat"), "M8H focused EditMode");

    [MenuItem("OpenArcanum/M8H/Run M8A-M8G Combat Regression Tests")]
    private static void RunM8HCombatRegression()
    {
        RunMany(
            new[]
            {
                Category("M8GBowCriticalDodgePhase4"), Category("M8GCoverMasterPhase3"),
                Category("M8GStructuredAttackPhase2"), Category("M8GCombatLoopPhase1"),
                Category("M8FCriticalResolution"), Category("M8EDeathConsequences"),
                Category("M8DDefeatState"), Category("M8CRangedCombat"),
                Category("M8BTurnBasedCombat"), Category("M8ACoreCombatState"),
            },
            new[]
            {
                "M8G Phase 4 Bow/Critical Dodge", "M8G Phase 3 cover/Bow Master",
                "M8G Phase 2 structured attacks", "M8G Phase 1 combat loop",
                "M8F critical resolution", "M8E death consequences", "M8D defeat state",
                "M8C ranged combat", "M8B turn-based combat", "M8A core combat",
            });
    }

    [MenuItem("OpenArcanum/M8H/Run Complete EditMode Tests")]
    private static void RunM8HAll()
        => Run(new Filter { testMode = TestMode.EditMode }, "M8H complete EditMode");

    [MenuItem("OpenArcanum/M8I/Run Focused EditMode Tests")]
    private static void RunM8IFocused()
        => Run(Category("M8ICombatUI"), "M8I focused EditMode");

    [MenuItem("OpenArcanum/M8I/Run M8A-M8H Combat Regression Tests")]
    private static void RunM8ICombatRegression()
    {
        RunMany(
            new[]
            {
                Category("M8HRealTimeCombat"), Category("M8GBowCriticalDodgePhase4"),
                Category("M8GCoverMasterPhase3"), Category("M8GStructuredAttackPhase2"),
                Category("M8GCombatLoopPhase1"), Category("M8FCriticalResolution"),
                Category("M8EDeathConsequences"), Category("M8DDefeatState"),
                Category("M8CRangedCombat"), Category("M8BTurnBasedCombat"),
                Category("M8ACoreCombatState"),
            },
            new[]
            {
                "M8H real-time combat", "M8G Phase 4 Bow/Critical Dodge",
                "M8G Phase 3 cover/Bow Master", "M8G Phase 2 structured attacks",
                "M8G Phase 1 combat loop", "M8F critical resolution",
                "M8E death consequences", "M8D defeat state", "M8C ranged combat",
                "M8B turn-based combat", "M8A core combat",
            });
    }

    [MenuItem("OpenArcanum/M8I/Run Complete EditMode Tests")]
    private static void RunM8IAll()
        => Run(new Filter { testMode = TestMode.EditMode }, "M8I complete EditMode");

    [MenuItem("OpenArcanum/M9A Phase 1/Run Focused EditMode Tests")]
    private static void RunM9APhase1Focused()
        => Run(Category("M9APhase1CombatAI"), "M9A Phase 1 focused EditMode");

    [MenuItem("OpenArcanum/M9A Phase 1/Run M8A-M8I Combat Regression Tests")]
    private static void RunM9APhase1CombatRegression()
    {
        RunMany(
            new[]
            {
                Category("M8ICombatUI"), Category("M8HRealTimeCombat"),
                Category("M8GBowCriticalDodgePhase4"), Category("M8GCoverMasterPhase3"),
                Category("M8GStructuredAttackPhase2"), Category("M8GCombatLoopPhase1"),
                Category("M8FCriticalResolution"), Category("M8EDeathConsequences"),
                Category("M8DDefeatState"), Category("M8CRangedCombat"),
                Category("M8BTurnBasedCombat"), Category("M8ACoreCombatState"),
            },
            new[]
            {
                "M8I combat UI", "M8H real-time combat", "M8G Phase 4 Bow/Critical Dodge",
                "M8G Phase 3 cover/Bow Master", "M8G Phase 2 structured attacks",
                "M8G Phase 1 combat loop", "M8F critical resolution", "M8E death consequences",
                "M8D defeat state", "M8C ranged combat", "M8B turn-based combat",
                "M8A core combat",
            });
    }

    [MenuItem("OpenArcanum/M9A Phase 1/Run Complete EditMode Tests")]
    private static void RunM9APhase1All()
        => Run(new Filter { testMode = TestMode.EditMode }, "M9A Phase 1 complete EditMode");

    [MenuItem("OpenArcanum/M9A Phase 2/Run Focused EditMode Tests")]
    private static void RunM9APhase2Focused()
        => Run(Category("M9APhase2WeaponTargeting"), "M9A Phase 2 focused EditMode");

    [MenuItem("OpenArcanum/M9A Phase 2/Run Phase 1 and M8A-M8I Regression Tests")]
    private static void RunM9APhase2CombatRegression()
    {
        RunMany(
            new[]
            {
                Category("M9APhase1CombatAI"), Category("M8ICombatUI"),
                Category("M8HRealTimeCombat"), Category("M8GBowCriticalDodgePhase4"),
                Category("M8GCoverMasterPhase3"), Category("M8GStructuredAttackPhase2"),
                Category("M8GCombatLoopPhase1"), Category("M8FCriticalResolution"),
                Category("M8EDeathConsequences"), Category("M8DDefeatState"),
                Category("M8CRangedCombat"), Category("M8BTurnBasedCombat"),
                Category("M8ACoreCombatState"),
            },
            new[]
            {
                "M9A Phase 1 combat AI", "M8I combat UI", "M8H real-time combat",
                "M8G Phase 4 Bow/Critical Dodge", "M8G Phase 3 cover/Bow Master",
                "M8G Phase 2 structured attacks", "M8G Phase 1 combat loop",
                "M8F critical resolution", "M8E death consequences", "M8D defeat state",
                "M8C ranged combat", "M8B turn-based combat", "M8A core combat",
            });
    }

    [MenuItem("OpenArcanum/M9A Phase 2/Run Complete EditMode Tests")]
    private static void RunM9APhase2All()
        => Run(new Filter { testMode = TestMode.EditMode }, "M9A Phase 2 complete EditMode");

    [MenuItem("OpenArcanum/M9B Phase 1/Run Focused EditMode Tests")]
    private static void RunM9BPhase1Focused()
        => Run(Category("M9BPhase1PartyFoundation"), "M9B Phase 1 focused EditMode");

    [MenuItem("OpenArcanum/M9B Phase 1/Run M9A-M8 Regression Tests")]
    private static void RunM9BPhase1Regression()
    {
        RunMany(
            new[]
            {
                Category("M9APhase2WeaponTargeting"), Category("M9APhase1CombatAI"),
                Category("M8ICombatUI"), Category("M8HRealTimeCombat"),
                Category("M8GBowCriticalDodgePhase4"), Category("M8GCoverMasterPhase3"),
                Category("M8GStructuredAttackPhase2"), Category("M8GCombatLoopPhase1"),
                Category("M8FCriticalResolution"), Category("M8EDeathConsequences"),
                Category("M8DDefeatState"), Category("M8CRangedCombat"),
                Category("M8BTurnBasedCombat"), Category("M8ACoreCombatState"),
            },
            new[]
            {
                "M9A Phase 2 weapon targeting", "M9A Phase 1 combat AI", "M8I combat UI",
                "M8H real-time combat", "M8G Phase 4 Bow/Critical Dodge", "M8G Phase 3 cover/Bow Master",
                "M8G Phase 2 structured attacks", "M8G Phase 1 combat loop", "M8F critical resolution",
                "M8E death consequences", "M8D defeat state", "M8C ranged combat",
                "M8B turn-based combat", "M8A core combat",
            });
    }

    [MenuItem("OpenArcanum/M9B Phase 1/Run Complete EditMode Tests")]
    private static void RunM9BPhase1All()
        => Run(new Filter { testMode = TestMode.EditMode }, "M9B Phase 1 complete EditMode");

    [MenuItem("OpenArcanum/M9B Phase 2/Run Focused EditMode Tests", false, 0)]
    private static void RunM9BPhase2Focused()
        => Run(Category("M9BPhase2DialogueParty"), "M9B Phase 2 focused EditMode");

    [MenuItem("OpenArcanum/M9B Phase 2/Run M9B-M5-M9A-M8 Regression Tests", false, 0)]
    private static void RunM9BPhase2Regression()
    {
        RunMany(
            new[]
            {
                Category("M9BPhase1PartyFoundation"), Category("M5C"), Category("M5B"), Category("M5A"),
                Category("M9APhase2WeaponTargeting"), Category("M9APhase1CombatAI"),
                Category("M8ICombatUI"), Category("M8HRealTimeCombat"),
                Category("M8GBowCriticalDodgePhase4"), Category("M8GCoverMasterPhase3"),
                Category("M8GStructuredAttackPhase2"), Category("M8GCombatLoopPhase1"),
                Category("M8FCriticalResolution"), Category("M8EDeathConsequences"),
                Category("M8DDefeatState"), Category("M8CRangedCombat"),
                Category("M8BTurnBasedCombat"), Category("M8ACoreCombatState"),
            },
            new[]
            {
                "M9B Phase 1 party foundation", "M5C trainer dialogue", "M5B quest/journal", "M5A dialogue/quest",
                "M9A Phase 2 weapon targeting", "M9A Phase 1 combat AI", "M8I combat UI",
                "M8H real-time combat", "M8G Phase 4 Bow/Critical Dodge", "M8G Phase 3 cover/Bow Master",
                "M8G Phase 2 structured attacks", "M8G Phase 1 combat loop", "M8F critical resolution",
                "M8E death consequences", "M8D defeat state", "M8C ranged combat",
                "M8B turn-based combat", "M8A core combat",
            });
    }

    [MenuItem("OpenArcanum/M9B Phase 2/Run Complete EditMode Tests", false, 0)]
    private static void RunM9BPhase2All()
        => Run(new Filter { testMode = TestMode.EditMode }, "M9B Phase 2 complete EditMode");

    [MenuItem("OpenArcanum/M10A/Run Focused EditMode Tests", false, 0)]
    private static void RunM10AFocused()
        => Run(Category("M10AMagicRuntime"), "M10A focused EditMode");

    [MenuItem("OpenArcanum/M10A/Run Phase 2 Focused EditMode Tests", false, 0)]
    private static void RunM10APhase2Focused()
        => Run(Category("M10APhase2"), "M10A Phase 2 focused EditMode");

    [MenuItem("OpenArcanum/M10A/Run Targeted Regression Tests", false, 0)]
    private static void RunM10ATargeted()
    {
        RunMany(
            new[]
            {
                Category("M4ACharacterAttributes"), Category("M4BCharacterVitality"),
                Category("M4DDerivedCharacterStats"), Category("M6A"),
                Category("M6B"), Category("M8BTurnBasedCombat"),
                Category("M8DDefeatState"), Category("M8EDeathConsequences"),
                Category("M8HRealTimeCombat"),
            },
            new[]
            {
                "M4A attributes", "M4B vitality", "M4D derived stats",
                "M6A session save/load", "M6B save migration/slots", "M8B turn-based combat",
                "M8D defeat state", "M8E death consequences", "M8H real-time combat",
            });
    }

    [MenuItem("OpenArcanum/M10A/Run Complete EditMode Tests", false, 0)]
    private static void RunM10AAll()
        => Run(new Filter { testMode = TestMode.EditMode }, "M10A complete EditMode");

    [MenuItem("OpenArcanum/M10B/Run Focused EditMode Tests", false, 0)]
    private static void RunM10BFocused()
        => Run(Category("M10BTechnologyRuntime"), "M10B focused EditMode");

    [MenuItem("OpenArcanum/M10B/Run Targeted Regression Tests", false, 0)]
    private static void RunM10BTargeted()
    {
        RunMany(
            new[]
            {
                Test("Arcanum.Formats.Tests.M3AInventoryStateTests"), Category("M3C"),
                Category("M4BCharacterVitality"), Category("M4CCharacterProgression"),
                Category("M4DDerivedCharacterStats"), Category("M6A"), Category("M6B"),
                Category("M8BTurnBasedCombat"), Category("M8FCriticalResolution"),
                Category("M8HRealTimeCombat"),
            },
            new[]
            {
                "M3A inventory state", "M3C equipment", "M4B vitality", "M4C progression",
                "M4D derived stats", "M6A session save/load", "M6B migration/slots",
                "M8B turn-based combat", "M8F critical resolution", "M8H real-time combat",
            });
    }

    [MenuItem("OpenArcanum/M10B/Run Complete EditMode Tests", false, 0)]
    private static void RunM10BAll()
        => Run(new Filter { testMode = TestMode.EditMode }, "M10B complete EditMode");

    [MenuItem("OpenArcanum/M8G Phase 2/Run M8A-M8G Phase 1 Combat Regression Tests")]
    private static void RunM8GPhase2CombatRegression()
    {
        RunMany(
            new[]
            {
                Category("M8GCombatLoopPhase1"), Category("M8FCriticalResolution"),
                Category("M8EDeathConsequences"), Category("M8DDefeatState"),
                Category("M8CRangedCombat"), Category("M8BTurnBasedCombat"),
                Category("M8ACoreCombatState"),
            },
            new[]
            {
                "M8G Phase 1 combat loop", "M8F critical resolution",
                "M8E death consequences", "M8D defeat state", "M8C ranged combat",
                "M8B turn-based combat", "M8A core combat",
            });
    }

    [MenuItem("OpenArcanum/M8F/Run Required Combat Regression Tests")]
    private static void RunM8FRequired()
    {
        RunMany(
            new[]
            {
                Category("M8EDeathConsequences"), Category("M8DDefeatState"),
                Category("M8CRangedCombat"), Category("M8BTurnBasedCombat"),
            },
            new[]
            {
                "M8E death consequences", "M8D defeat state",
                "M8C ranged combat", "M8B turn-based combat",
            });
    }

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
