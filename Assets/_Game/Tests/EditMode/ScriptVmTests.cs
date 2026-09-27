using Arcanum.Formats.Script;
using Arcanum.Script;
using NUnit.Framework;

namespace Arcanum.Formats.Tests
{
    /// <summary>
    /// Exercises the script VM (<see cref="ScriptVm"/> + <see cref="ScriptGlobals"/>) — operand resolution,
    /// conditions, the value/flag/arithmetic actions, control flow, and the teleport host call. Scripts are
    /// built in-memory from <see cref="ScriptFile"/>/<see cref="ScriptCondition"/>/<see cref="ScriptAction"/>
    /// (no <c>.scr</c> bytes), so this validates execution semantics, not parsing. See Docs/Scripting.md.
    /// </summary>
    public sealed class ScriptVmTests
    {
        private sealed class StubHost : IScriptHost
        {
            public int Calls;
            public int Map = int.MinValue, X, Y;
            public void Teleport(int mapId, int x, int y) { Calls++; Map = mapId; X = x; Y = y; }

            // Phase 2: focus resolution returns a configurable object (or a set, for loops / any-every).
            public object Resolved;
            public object[] ResolvedSet;
            public object[] ResolveFocus(int sfoType, int sfoValue, ScriptContext ctx)
                => ResolvedSet ?? (Resolved != null ? new[] { Resolved } : System.Array.Empty<object>());

            public int ToggleCalls; public object Toggled;
            public void TogglePortal(object obj) { ToggleCalls++; Toggled = obj; }

            public object LockTarget; public bool LockState; public int LockCalls;
            public void SetLocked(object obj, bool locked) { LockCalls++; LockTarget = obj; LockState = locked; }

            public bool OpenResult; public bool IsOpen(object obj) => OpenResult;
            public bool DeadResult; public bool IsDead(object obj) => DeadResult;

            public int FloatCalls; public object FloatObj; public int FloatScript, FloatLineNum;
            public void FloatLine(object obj, int scriptNum, int lineNum) { FloatCalls++; FloatObj = obj; FloatScript = scriptNum; FloatLineNum = lineNum; }
            public int PrintCalls; public object PrintObj; public int PrintLineNum;
            public void PrintLine(object obj, int scriptNum, int lineNum) { PrintCalls++; PrintObj = obj; PrintLineNum = lineNum; }

            public int ToggleOffCalls; public object ToggledOff;
            public void ToggleOff(object obj) { ToggleOffCalls++; ToggledOff = obj; }
            public int KillCalls; public object Killed;
            public void Kill(object obj) { KillCalls++; Killed = obj; }

            // Object-state condition queries — configurable results.
            public int GoldResult; public int GetGold(object obj) => GoldResult;
            public bool SwitchedOffResult; public bool IsSwitchedOff(object obj) => SwitchedOffResult;
            public bool FollowingResult; public bool IsFollowingPc(object obj) => FollowingResult;
            public bool IsJilted(object obj) => false;
            public bool InCombatResult; public bool IsInCombat(object obj) => InCombatResult;
            public bool AtTileResult; public bool IsAtTile(object obj, int x, int y) => AtTileResult;
            public bool WithinRangeResult; public bool IsWithinRange(object obj, int x, int y, int range) => WithinRangeResult;
            public bool NamedResult; public bool IsNamed(object obj, int name) => NamedResult;
            public bool AnimalResult; public bool IsAnimal(object obj) => AnimalResult;
            public bool UndeadResult; public bool IsUndead(object obj) => UndeadResult;

            // Critter/world actions — record the calls.
            public int GoldDelta; public object GoldObj; public void AdjustGold(object obj, int amount) { GoldObj = obj; GoldDelta += amount; }
            public int FollowCalls; public object Followed; public void CritterFollow(object obj) { FollowCalls++; Followed = obj; }
            public int DisbandCalls; public object Disbanded; public void CritterDisband(object obj) { DisbandCalls++; Disbanded = obj; }
            public int StatResult; public int GetStat(object obj, int stat) => StatResult;
            public int SkillResult; public int GetSkill(object obj, int skill) => SkillResult;
            public bool KnowsSpellResult; public bool KnowsSpell(object obj, int spell) => KnowsSpellResult;
            public int DialogCalls; public object DialogObj; public int DialogLine; public int DialogScriptNum, DialogScriptLine;
            public bool MetPc, InDialog, Daytime = true; public int HasItemName = -1;
            public bool HasMetPc(object obj) => MetPc;
            public bool IsInDialog(object obj) => InDialog;
            public bool IsDaytime() => Daytime;
            public bool HasItemNamed(object obj, int nameId) => nameId == HasItemName;
            public int MonsterSpecie = -1; public bool IsMonsterOfType(object obj, int specie) => specie == MonsterSpecie;
            public int WieldingName = -1; public bool IsWieldingItemNamed(object obj, int nameId) => nameId == WieldingName;
            public int KnownRumor = -1; public bool RumorKnown(object obj, int rumorId) => rumorId == KnownRumor;
            public bool CanOpen = true; public bool CanOpenPortal(object a, object p, int dir) => CanOpen;
            public bool CanOpenContainer(object a, object c) => CanOpen;
            public bool CanSee = true; public bool CanSeeObj(object a, object b) => CanSee;
            public bool CanHearObj(object a, object b) => CanSee;
            public void StartDialog(object obj, int dialogLine, int scriptNum, int scriptLine) { DialogCalls++; DialogObj = obj; DialogLine = dialogLine; DialogScriptNum = scriptNum; DialogScriptLine = scriptLine; }

            // Combat / magick actions — record the calls.
            public int AttackCalls; public object Attacker, AttackTarget; public void Attack(object a, object t) { AttackCalls++; Attacker = a; AttackTarget = t; }
            public int DamageAmount, DamageType; public object DamageObj; public void Damage(object obj, int amount, int type) { DamageObj = obj; DamageAmount = amount; DamageType = type; }
            public int HealHpAmount; public void HealHp(object obj, int amount) { HealHpAmount += amount; }
            public int HealFatigueAmount; public void HealFatigue(object obj, int amount) { HealFatigueAmount += amount; }
            public int CastSpellNum; public object CastSource, CastTarget; public void CastSpell(object s, int spell, object t) { CastSpellNum = spell; CastSource = s; CastTarget = t; }
            public int MarkedArea = int.MinValue; public void MarkMapLocation(object pc, int area) => MarkedArea = area;
            public int SetRumorId = int.MinValue; public void SetRumor(object pc, int rumor) => SetRumorId = rumor;
            public int UnfoggedMap = int.MinValue; public void UnfogTownmap(int map) => UnfoggedMap = map;
            public int QuelledRumor = int.MinValue; public void QuellRumor(object pc, int rumor) => QuelledRumor = rumor;
            public int CallExSap = int.MinValue, CallExLine; public object CallExAttachee;
            public void RunObjectScript(object triggerer, object attachee, int sap, int line) { CallExSap = sap; CallExLine = line; CallExAttachee = attachee; }
            public Arcanum.Formats.Script.ScriptFile ScriptResult; public Arcanum.Formats.Script.ScriptFile GetScript(int num) => ScriptResult;
        }

        private static readonly (Svt t, int v) Focus = ((Svt)0, 0); // an object operand (op_type read as Sfo, not Svt)
        private static (Svt t, int v) Obj(Sfo sfo, int v = 0) => ((Svt)(int)sfo, v); // object operand of a specific Sfo

        // ── builders ──
        private static ScriptAction Act(Sat type, params (Svt t, int v)[] ops)
        {
            var a = new ScriptAction { Type = (int)type };
            for (int i = 0; i < ops.Length && i < 8; i++) { a.OpType[i] = (byte)ops[i].t; a.OpValue[i] = ops[i].v; }
            return a;
        }

        private static ScriptAction Nop() => new ScriptAction { Type = (int)Sat.DoNothing };

        private static ScriptCondition Cond(Sct type, ScriptAction action, ScriptAction els, params (Svt t, int v)[] ops)
        {
            var c = new ScriptCondition { Type = (int)type, Action = action, Els = els };
            for (int i = 0; i < ops.Length && i < 8; i++) { c.OpType[i] = (byte)ops[i].t; c.OpValue[i] = ops[i].v; }
            return c;
        }

        private static ScriptFile File(params ScriptCondition[] entries)
        {
            var f = new ScriptFile();
            f.Entries.AddRange(entries);
            return f;
        }

        // Run a file on a fresh VM/ctx; expose the globals it mutated.
        private static (bool runDefault, ScriptGlobals globals, ScriptContext ctx, StubHost host) Run(ScriptFile file)
        {
            var host = new StubHost();
            var globals = new ScriptGlobals();
            var ctx = new ScriptContext();
            bool rd = new ScriptVm(host, globals).Execute(file, ctx);
            return (rd, globals, ctx, host);
        }

        // Run a file on a VM with a pre-configured host (so tests can set condition results / focus sets).
        private static (ScriptGlobals globals, ScriptContext ctx) RunOn(ScriptFile file, StubHost host)
        {
            var globals = new ScriptGlobals();
            var ctx = new ScriptContext();
            new ScriptVm(host, globals).Execute(file, ctx);
            return (globals, ctx);
        }

        // ── ScriptGlobals ──

        [Test]
        public void Globals_VarsRoundTrip_AndIgnoreOutOfRange()
        {
            var g = new ScriptGlobals();
            g.SetVar(5, 42);
            Assert.That(g.GetVar(5), Is.EqualTo(42));
            Assert.That(g.GetVar(1), Is.EqualTo(0));
            Assert.DoesNotThrow(() => { g.SetVar(-1, 1); g.SetVar(999999, 1); });
            Assert.That(g.GetVar(-1), Is.EqualTo(0));
            Assert.That(g.GetVar(999999), Is.EqualTo(0));
        }

        [Test]
        public void Globals_FlagsAreBitPackedAcrossWords()
        {
            var g = new ScriptGlobals();
            g.SetFlag(0, 1);
            g.SetFlag(33, 1);   // second 32-bit word
            g.SetFlag(3199, 1); // last flag (word 99)
            Assert.That(g.GetFlag(0), Is.EqualTo(1));
            Assert.That(g.GetFlag(33), Is.EqualTo(1));
            Assert.That(g.GetFlag(3199), Is.EqualTo(1));
            Assert.That(g.GetFlag(1), Is.EqualTo(0));
            g.SetFlag(33, 0);
            Assert.That(g.GetFlag(33), Is.EqualTo(0));
            Assert.That(g.GetFlag(0), Is.EqualTo(1)); // unrelated flag untouched
        }

        [Test]
        public void Globals_StoryStateOnlyAdvances()
        {
            var g = new ScriptGlobals();
            g.SetStoryState(5);
            Assert.That(g.StoryState, Is.EqualTo(5));
            g.SetStoryState(3); // lower is ignored
            Assert.That(g.StoryState, Is.EqualTo(5));
            g.SetStoryState(7);
            Assert.That(g.StoryState, Is.EqualTo(7));
        }

        // ── operands + arithmetic ──

        [Test]
        public void AssignNum_WritesGlobalVarFromLiteral()
        {
            var (_, g, _, _) = Run(File(
                Cond(Sct.True, Act(Sat.AssignNum, (Svt.GlVar, 3), (Svt.Number, 42)), Nop())));
            Assert.That(g.GetVar(3), Is.EqualTo(42));
        }

        [Test]
        public void Arithmetic_AddSubtractMultiplyDivide_OverGlobalVars()
        {
            var (_, g, _, _) = Run(File(
                Cond(Sct.True, Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 10)), Nop()),
                Cond(Sct.True, Act(Sat.AssignNum, (Svt.GlVar, 1), (Svt.Number, 3)), Nop()),
                Cond(Sct.True, Act(Sat.Add, (Svt.GlVar, 2), (Svt.GlVar, 0), (Svt.GlVar, 1)), Nop()),
                Cond(Sct.True, Act(Sat.Subtract, (Svt.GlVar, 3), (Svt.GlVar, 0), (Svt.GlVar, 1)), Nop()),
                Cond(Sct.True, Act(Sat.Multiply, (Svt.GlVar, 4), (Svt.GlVar, 0), (Svt.GlVar, 1)), Nop()),
                Cond(Sct.True, Act(Sat.Divide, (Svt.GlVar, 5), (Svt.GlVar, 0), (Svt.GlVar, 1)), Nop())));
            Assert.That(g.GetVar(2), Is.EqualTo(13));
            Assert.That(g.GetVar(3), Is.EqualTo(7));
            Assert.That(g.GetVar(4), Is.EqualTo(30));
            Assert.That(g.GetVar(5), Is.EqualTo(3)); // integer 10/3
        }

        [Test]
        public void Divide_ByZero_IsGuarded_LeavesTargetUnchanged()
        {
            var (_, g, _, _) = Run(File(
                Cond(Sct.True, Act(Sat.AssignNum, (Svt.GlVar, 2), (Svt.Number, 99)), Nop()),
                Cond(Sct.True, Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 5)), Nop()),
                Cond(Sct.True, Act(Sat.Divide, (Svt.GlVar, 2), (Svt.GlVar, 0), (Svt.GlVar, 1)), Nop()))); // GlVar1 == 0
            Assert.That(g.GetVar(2), Is.EqualTo(99));
        }

        [Test]
        public void LoopFor_SkipsBody_AndResumesAfterLoopEnd()
        {
            // The focus-object set isn't resolved yet, so LOOP_FOR must skip its body (zero iterations) and
            // continue past LOOP_END — never running the body on a wrong/absent loop object.
            var (_, g, _, _) = Run(File(
                Cond(Sct.True, Act(Sat.LoopFor), Nop()),                                    // 0: loop start
                Cond(Sct.True, Act(Sat.AssignNum, (Svt.GlVar, 1), (Svt.Number, 7)), Nop()), // 1: body (skipped)
                Cond(Sct.True, Act(Sat.LoopEnd), Nop()),                                    // 2: loop end
                Cond(Sct.True, Act(Sat.AssignNum, (Svt.GlVar, 2), (Svt.Number, 99)), Nop()))); // 3: after loop

            Assert.That(g.GetVar(1), Is.EqualTo(0));  // body skipped
            Assert.That(g.GetVar(2), Is.EqualTo(99)); // resumed after LOOP_END
        }

        [Test]
        public void Counters_PackInto8BitSlices()
        {
            var (_, _, ctx, _) = Run(File(
                Cond(Sct.True, Act(Sat.AssignNum, (Svt.Counter, 0), (Svt.Number, 200)), Nop()),
                Cond(Sct.True, Act(Sat.AssignNum, (Svt.Counter, 1), (Svt.Number, 5)), Nop())));
            Assert.That((int)(ctx.LocalCounters & 0xFF), Is.EqualTo(200));
            Assert.That((int)((ctx.LocalCounters >> 8) & 0xFF), Is.EqualTo(5));
        }

        // ── conditions ──

        [Test]
        public void Eq_RunsActionWhenEqual_ElseEls()
        {
            var (_, gt, _, _) = Run(File(Cond(Sct.Eq,
                Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 1)),
                Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 2)),
                (Svt.Number, 5), (Svt.Number, 5))));
            Assert.That(gt.GetVar(0), Is.EqualTo(1));

            var (_, gf, _, _) = Run(File(Cond(Sct.Eq,
                Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 1)),
                Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 2)),
                (Svt.Number, 5), (Svt.Number, 6))));
            Assert.That(gf.GetVar(0), Is.EqualTo(2));
        }

        [Test]
        public void Le_ComparesOperandsInOrder()
        {
            var (_, le, _, _) = Run(File(Cond(Sct.Le,
                Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 1)),
                Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 2)),
                (Svt.Number, 3), (Svt.Number, 5))));
            Assert.That(le.GetVar(0), Is.EqualTo(1)); // 3 <= 5

            var (_, gt, _, _) = Run(File(Cond(Sct.Le,
                Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 1)),
                Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 2)),
                (Svt.Number, 6), (Svt.Number, 5))));
            Assert.That(gt.GetVar(0), Is.EqualTo(2)); // 6 <= 5 is false → els
        }

        [Test]
        public void GlobalFlag_SetThenTested_ThenCleared()
        {
            var (_, g, _, _) = Run(File(
                Cond(Sct.True, Act(Sat.SetGlobalFlag, (Svt.Number, 50)), Nop()),
                Cond(Sct.GlobalFlag,
                    Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 1)),
                    Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 2)),
                    (Svt.Number, 50))));
            Assert.That(g.GetFlag(50), Is.EqualTo(1));
            Assert.That(g.GetVar(0), Is.EqualTo(1));

            var (_, g2, _, _) = Run(File(
                Cond(Sct.True, Act(Sat.SetGlobalFlag, (Svt.Number, 50)), Nop()),
                Cond(Sct.True, Act(Sat.ClearGlobalFlag, (Svt.Number, 50)), Nop()),
                Cond(Sct.GlobalFlag,
                    Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 1)),
                    Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 2)),
                    (Svt.Number, 50))));
            Assert.That(g2.GetFlag(50), Is.EqualTo(0));
            Assert.That(g2.GetVar(0), Is.EqualTo(2));
        }

        [Test]
        public void LocalFlag_SetThenTested()
        {
            var (_, g, ctx, _) = Run(File(
                Cond(Sct.True, Act(Sat.SetLocalFlag, (Svt.Number, 3)), Nop()),
                Cond(Sct.LocalFlag,
                    Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 1)),
                    Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 2)),
                    (Svt.Number, 3))));
            Assert.That(ctx.LocalFlags & (1u << 3), Is.Not.EqualTo(0u));
            Assert.That(g.GetVar(0), Is.EqualTo(1));
        }

        // ── story state actions ──

        [Test]
        public void StoryState_SetThenGetIntoVar()
        {
            var (_, g, _, _) = Run(File(
                Cond(Sct.True, Act(Sat.SetStoryState, (Svt.Number, 7)), Nop()),
                Cond(Sct.True, Act(Sat.GetStoryState, (Svt.GlVar, 0)), Nop())));
            Assert.That(g.StoryState, Is.EqualTo(7));
            Assert.That(g.GetVar(0), Is.EqualTo(7));
        }

        // ── object-state conditions / critter+combat actions / loops / call-script ──

        [Test]
        public void ObjIsInCombat_BranchesOnHostResult()
        {
            var host = new StubHost { Resolved = new object(), InCombatResult = true };
            var (g, _) = RunOn(File(Cond(Sct.ObjIsInCombat,
                Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 1)),
                Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 2)),
                Obj(Sfo.Triggerer))), host);
            Assert.That(g.GetVar(0), Is.EqualTo(1)); // in combat → true branch
        }

        [Test]
        public void HasGold_TrueWhenAtLeastAmount()
        {
            var host = new StubHost { Resolved = new object(), GoldResult = 100 };
            var (g, _) = RunOn(File(Cond(Sct.HasGold,
                Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 1)), Nop(),
                Obj(Sfo.Triggerer), (Svt.Number, 50))), host);
            Assert.That(g.GetVar(0), Is.EqualTo(1)); // 100 >= 50
        }

        [Test]
        public void AdjustGold_PassesAmountToHost()
        {
            var host = new StubHost { Resolved = new object() };
            RunOn(File(Cond(Sct.True, Act(Sat.AdjustGold, Obj(Sfo.Triggerer), (Svt.Number, 25)), Nop())), host);
            Assert.That(host.GoldDelta, Is.EqualTo(25));
        }

        [Test]
        public void GetStat_WritesHostStatToVar()
        {
            var host = new StubHost { Resolved = new object(), StatResult = 14 };
            var (g, _) = RunOn(File(Cond(Sct.True,
                Act(Sat.GetStat, (Svt.Number, 0), Obj(Sfo.Triggerer), (Svt.GlVar, 0)), Nop())), host);
            Assert.That(g.GetVar(0), Is.EqualTo(14));
        }

        [Test]
        public void Random_WritesValueInRange()
        {
            var (g, _) = RunOn(File(Cond(Sct.True,
                Act(Sat.Random, (Svt.Number, 5), (Svt.Number, 9), (Svt.GlVar, 0)), Nop())), new StubHost());
            Assert.That(g.GetVar(0), Is.InRange(5, 9));
        }

        [Test]
        public void CritterFollow_CallsHost()
        {
            var host = new StubHost { Resolved = new object() };
            RunOn(File(Cond(Sct.True, Act(Sat.CritterFollow, Obj(Sfo.Triggerer)), Nop())), host);
            Assert.That(host.FollowCalls, Is.EqualTo(1));
        }

        [Test]
        public void Damage_PassesAmountAndTypeToHost()
        {
            var host = new StubHost { Resolved = new object() };
            RunOn(File(Cond(Sct.True,
                Act(Sat.Damage, Obj(Sfo.Triggerer), (Svt.Number, 12), (Svt.Number, 3)), Nop())), host);
            Assert.That(host.DamageAmount, Is.EqualTo(12));
            Assert.That(host.DamageType, Is.EqualTo(3));
        }

        [Test]
        public void CastSpell_PassesSpellIdToHost()
        {
            var host = new StubHost { Resolved = new object() };
            RunOn(File(Cond(Sct.True,
                Act(Sat.CastSpell, Obj(Sfo.Triggerer), (Svt.Number, 7), Obj(Sfo.Attachee)), Nop())), host);
            Assert.That(host.CastSpellNum, Is.EqualTo(7));
        }

        [Test]
        public void Dialog_OpensAttacheeDialog()
        {
            var host = new StubHost { Resolved = new object() };
            RunOn(File(Cond(Sct.True, Act(Sat.Dialog, (Svt.Number, 0)), Nop())), host);
            Assert.That(host.DialogCalls, Is.EqualTo(1));
        }

        [Test]
        public void UnmodelledStateCondition_IsFalse_SoTheDialogChainFallsThrough()
        {
            // Virgil's script 1324 shape: a chain of "special state → special dialog" checks ending in the
            // unconditional first-meeting Dialog(1). ObjJilted is an unmodelled state and MUST evaluate false
            // (fall through) — defaulting it true hijacked the chain (Virgil offered to rejoin on first talk).
            var host = new StubHost { Resolved = new object() }; // not following, not jilted
            var file = File(
                Cond(Sct.ObjFollowingPc, Act(Sat.Goto, (Svt.Number, 3)), Nop(), ((Svt)Sfo.Attachee, 0)),
                Cond(Sct.ObjJilted, Act(Sat.Goto, (Svt.Number, 5)), Nop(), ((Svt)Sfo.Attachee, 0)),
                Cond(Sct.True, Act(Sat.Dialog, (Svt.Number, 1)), Nop()),                       // first meeting
                Cond(Sct.True, Act(Sat.Dialog, (Svt.Number, 385)), Nop()),  // follower menu (goto 3)
                Cond(Sct.True, Act(Sat.ReturnAndSkipDefault), Nop()),
                Cond(Sct.True, Act(Sat.Dialog, (Svt.Number, 538)), Nop())); // jilted rejoin (goto 5)
            new ScriptVm(host).Execute(file, new ScriptContext { ScriptNum = 1324 });
            Assert.That(host.DialogLine, Is.EqualTo(1), "a fresh NPC must open the first-meeting dialog");
        }

        [Test]
        public void MonsterType_Wielding_Rumor_UseTheHost()
        {
            // The formerly assumed-TRUE conditions now consult the host — verify each routes THEN vs ELSE.
            var host = new StubHost { Resolved = new object(), MonsterSpecie = 7, WieldingName = 42, KnownRumor = 3 };

            var (g1, _) = RunOn(File(Cond(Sct.ObjIsMonsterOfType,
                Act(Sat.SetGlobalFlag, (Svt.Number, 1)), Nop(), Obj(Sfo.Attachee), (Svt.Number, 7))), host);
            Assert.That(g1.GetFlag(1), Is.EqualTo(1), "matching species → THEN");

            var (g2, _) = RunOn(File(Cond(Sct.ObjIsMonsterOfType,
                Act(Sat.SetGlobalFlag, (Svt.Number, 2)), Nop(), Obj(Sfo.Attachee), (Svt.Number, 9))), host);
            Assert.That(g2.GetFlag(2), Is.EqualTo(0), "wrong species → ELSE");

            var (g3, _) = RunOn(File(Cond(Sct.ObjIsWieldingItem,
                Act(Sat.SetGlobalFlag, (Svt.Number, 3)), Nop(), Obj(Sfo.Attachee), (Svt.Number, 42))), host);
            Assert.That(g3.GetFlag(3), Is.EqualTo(1), "wielding named item → THEN");

            var (g4, _) = RunOn(File(Cond(Sct.RumorKnown,
                Act(Sat.SetGlobalFlag, (Svt.Number, 4)), Nop(), Obj(Sfo.Triggerer), (Svt.Number, 3))), host);
            Assert.That(g4.GetFlag(4), Is.EqualTo(1), "known rumor → THEN");
        }

        [Test]
        public void CanSeeAndOpen_UseTheHost_ForTwoObjectChecks()
        {
            var host = new StubHost { Resolved = new object(), CanSee = false, CanOpen = true };
            var (g1, _) = RunOn(File(Cond(Sct.ObjCanSeeObj,
                Act(Sat.SetGlobalFlag, (Svt.Number, 1)), Nop(), Obj(Sfo.Attachee), Obj(Sfo.Triggerer))), host);
            Assert.That(g1.GetFlag(1), Is.EqualTo(0), "can't see → ELSE");

            var (g2, _) = RunOn(File(Cond(Sct.CanOpenPortal,
                Act(Sat.SetGlobalFlag, (Svt.Number, 2)), Nop(), Obj(Sfo.Triggerer), Obj(Sfo.Attachee), (Svt.Number, 0))), host);
            Assert.That(g2.GetFlag(2), Is.EqualTo(1), "can open portal → THEN");
        }

        [Test]
        public void MetPcBefore_And_Daytime_UseTheHost()
        {
            var host = new StubHost { Resolved = new object(), MetPc = true, Daytime = false };
            var file = File(
                Cond(Sct.ObjMetPcBefore, Act(Sat.Dialog, (Svt.Number, 235)), Nop(), ((Svt)Sfo.Attachee, 0)),
                Cond(Sct.True, Act(Sat.Dialog, (Svt.Number, 1)), Nop()));
            new ScriptVm(host).Execute(file, new ScriptContext { ScriptNum = 1 });
            Assert.That(host.DialogLine, Is.EqualTo(235), "met-before must route to the repeat greeting");

            var vm = new ScriptVm(host);
            var g = new ScriptContext();
            var day = File(Cond(Sct.Daytime, Act(Sat.SetGlobalFlag, (Svt.Number, 5)), Nop()));
            vm.Execute(day, g);
            Assert.That(vm.Globals.GetFlag(5), Is.EqualTo(0), "night per the host clock → flag untouched");
        }

        [Test]
        public void Dialog_PassesStartLine_ScriptNum_AndScriptLine()
        {
            // Engine SAT_DIALOG: op0 = the .dlg line to open at; the running script's number + entry line ride
            // along so the ended conversation can resume the script (dialog_ui_execute_script).
            var host = new StubHost { Resolved = new object() };
            var file = File(
                Cond(Sct.True, Act(Sat.Goto, (Svt.Number, 1)), Nop()),           // line 0 → goto 1
                Cond(Sct.True, Act(Sat.Dialog, (Svt.Number, 90)), Nop()));       // line 1 → open dialog at 90
            var ctx = new ScriptContext { ScriptNum = 1848 };
            new ScriptVm(host).Execute(file, ctx);
            Assert.That(host.DialogLine, Is.EqualTo(90));
            Assert.That(host.DialogScriptNum, Is.EqualTo(1848));
            Assert.That(host.DialogScriptLine, Is.EqualTo(1)); // the entry the SAT_DIALOG sat on
        }

        [Test]
        public void WorldActions_MarkMap_Rumors_AndCallScriptEx()
        {
            var host = new StubHost { Resolved = new object() };
            // MARK_MAP_LOCATION: op0 = area, op1 = pc obj.
            RunOn(File(Cond(Sct.True, Act(Sat.MarkMapLocation, (Svt.Number, 320), Obj(Sfo.Triggerer)), Nop())), host);
            Assert.That(host.MarkedArea, Is.EqualTo(320));

            RunOn(File(Cond(Sct.True, Act(Sat.SetRumor, (Svt.Number, 5), Obj(Sfo.Triggerer)), Nop())), host);
            Assert.That(host.SetRumorId, Is.EqualTo(5));

            RunOn(File(Cond(Sct.True, Act(Sat.QuellRumor, (Svt.Number, 7), Obj(Sfo.Triggerer)), Nop())), host);
            Assert.That(host.QuelledRumor, Is.EqualTo(7));

            RunOn(File(Cond(Sct.True, Act(Sat.UnfogTownmap, (Svt.Number, 4)), Nop())), host);
            Assert.That(host.UnfoggedMap, Is.EqualTo(4));

            // CALL_SCRIPT_EX: op0 = attachee, op1 = SAP, op2 = line, op3 = triggerer.
            host.Resolved = new object();
            RunOn(File(Cond(Sct.True, Act(Sat.CallScriptEx,
                Obj(Sfo.Attachee), (Svt.Number, 9), (Svt.Number, 14), Obj(Sfo.Triggerer)), Nop())), host);
            Assert.That(host.CallExSap, Is.EqualTo(9));
            Assert.That(host.CallExLine, Is.EqualTo(14));
            Assert.That(host.CallExAttachee, Is.Not.Null);
        }

        [Test]
        public void LoopFor_RunsBodyOncePerSetElement()
        {
            var host = new StubHost { ResolvedSet = new[] { new object(), new object(), new object() } };
            var (g, _) = RunOn(File(
                Cond(Sct.True, Act(Sat.LoopFor, Obj(Sfo.EveryFollower)), Nop()),
                Cond(Sct.True, Act(Sat.Add, (Svt.GlVar, 0), (Svt.GlVar, 0), (Svt.Number, 1)), Nop()),
                Cond(Sct.True, Act(Sat.LoopEnd), Nop())), host);
            Assert.That(g.GetVar(0), Is.EqualTo(3)); // body ran once per element
        }

        [Test]
        public void LoopFor_EmptySetSkipsBody()
        {
            var host = new StubHost { ResolvedSet = System.Array.Empty<object>() };
            var (g, _) = RunOn(File(
                Cond(Sct.True, Act(Sat.LoopFor, Obj(Sfo.EveryFollower)), Nop()),
                Cond(Sct.True, Act(Sat.Add, (Svt.GlVar, 0), (Svt.GlVar, 0), (Svt.Number, 1)), Nop()),
                Cond(Sct.True, Act(Sat.LoopEnd), Nop())), host);
            Assert.That(g.GetVar(0), Is.EqualTo(0)); // empty set → body skipped
        }

        [Test]
        public void CallScript_RunsSubScript()
        {
            var sub = File(Cond(Sct.True, Act(Sat.SetGlobalFlag, (Svt.Number, 5)), Nop()));
            var host = new StubHost { ScriptResult = sub, Resolved = new object() };
            var (g, _) = RunOn(File(Cond(Sct.True,
                Act(Sat.CallScript, (Svt.Number, 42), (Svt.Number, 0), Obj(Sfo.Triggerer), Obj(Sfo.Attachee)), Nop())), host);
            Assert.That(g.GetFlag(5), Is.Not.EqualTo(0)); // the sub-script set global flag 5
        }

        // ── teleport host call ──

        [Test]
        public void Teleport_CallsHostWithMapIdMinus4999()
        {
            var (_, _, _, host) = Run(File(Cond(Sct.True,
                Act(Sat.Teleport, (Svt.Counter, 0), (Svt.Number, 5011), (Svt.Number, 104), (Svt.Number, 92)),
                Nop())));
            Assert.That(host.Calls, Is.EqualTo(1));
            Assert.That(host.Map, Is.EqualTo(12)); // 5011 − 4999
            Assert.That(host.X, Is.EqualTo(104));
            Assert.That(host.Y, Is.EqualTo(92));
        }

        // ── control flow ──

        [Test]
        public void Goto_SkipsInterveningEntries()
        {
            var (_, g, _, _) = Run(File(
                Cond(Sct.True, Act(Sat.Goto, (Svt.Number, 2)), Nop()),
                Cond(Sct.True, Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 1)), Nop()), // skipped
                Cond(Sct.True, Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 2)), Nop())));
            Assert.That(g.GetVar(0), Is.EqualTo(2));
        }

        [Test]
        public void Returns_DriveRunDefault()
        {
            Assert.That(Run(File(Cond(Sct.True, Act(Sat.ReturnAndRunDefault), Nop()))).runDefault, Is.True);
            Assert.That(Run(File(Cond(Sct.True, Act(Sat.ReturnAndSkipDefault), Nop()))).runDefault, Is.False);
            Assert.That(Run(File(Cond(Sct.True, Nop(), Nop()))).runDefault, Is.False); // ran off the end
        }

        [Test]
        public void EmptyOrNullFile_RunsDefault()
        {
            var vm = new ScriptVm(new StubHost());
            Assert.That(vm.Execute(null, new ScriptContext()), Is.True);
            Assert.That(vm.Execute(new ScriptFile(), new ScriptContext()), Is.True);
        }

        // ── Phase 2: object opcodes (portals/levers) ──

        [Test]
        public void ToggleOpenClosed_TogglesResolvedObject()
        {
            var host = new StubHost { Resolved = "GATE" };
            new ScriptVm(host).Execute(File(Cond(Sct.True, Act(Sat.ToggleOpenClosed, Focus), Nop())), new ScriptContext());
            Assert.That(host.ToggleCalls, Is.EqualTo(1));
            Assert.That(host.Toggled, Is.EqualTo("GATE"));
        }

        [Test]
        public void SetLockState_SetsLockedFromValue()
        {
            var locked = new StubHost { Resolved = "DOOR" };
            new ScriptVm(locked).Execute(File(Cond(Sct.True, Act(Sat.SetLockState, Focus, (Svt.Number, 1)), Nop())), new ScriptContext());
            Assert.That(locked.LockCalls, Is.EqualTo(1));
            Assert.That(locked.LockTarget, Is.EqualTo("DOOR"));
            Assert.That(locked.LockState, Is.True);

            var unlocked = new StubHost { Resolved = "DOOR" };
            new ScriptVm(unlocked).Execute(File(Cond(Sct.True, Act(Sat.SetLockState, Focus, (Svt.Number, 0)), Nop())), new ScriptContext());
            Assert.That(unlocked.LockState, Is.False);
        }

        [Test]
        public void ObjIsOpen_BranchesOnHostResult()
        {
            var open = new StubHost { Resolved = "P", OpenResult = true };
            var g1 = new ScriptGlobals();
            new ScriptVm(open, g1).Execute(File(Cond(Sct.ObjIsOpen,
                Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 1)),
                Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 2)), Focus)), new ScriptContext());
            Assert.That(g1.GetVar(0), Is.EqualTo(1));

            var shut = new StubHost { Resolved = "P", OpenResult = false };
            var g2 = new ScriptGlobals();
            new ScriptVm(shut, g2).Execute(File(Cond(Sct.ObjIsOpen,
                Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 1)),
                Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 2)), Focus)), new ScriptContext());
            Assert.That(g2.GetVar(0), Is.EqualTo(2));
        }

        [Test]
        public void ObjIsDead_BranchesOnHostResult()
        {
            var dead = new StubHost { Resolved = "NPC", DeadResult = true };
            var g = new ScriptGlobals();
            new ScriptVm(dead, g).Execute(File(Cond(Sct.ObjIsDead,
                Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 1)),
                Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 2)), Focus)), new ScriptContext());
            Assert.That(g.GetVar(0), Is.EqualTo(1));
        }

        [Test]
        public void AssignObj_StoresResolvedSourceIntoLocalSlot()
        {
            var host = new StubHost { Resolved = "OBJ" };
            var ctx = new ScriptContext();
            // AssignObj(target = LocalObject[2], source = Attachee → host resolves to "OBJ")
            new ScriptVm(host).Execute(File(Cond(Sct.True, Act(Sat.AssignObj, Obj(Sfo.LocalObject, 2), Obj(Sfo.Attachee)), Nop())), ctx);
            Assert.That(ctx.LocalObjects[2], Is.EqualTo("OBJ"));
        }

        [Test]
        public void GlobalQuestState_SetThenTested()
        {
            var (_, g, _, _) = Run(File(
                Cond(Sct.True, Act(Sat.SetQuestGlobalState, (Svt.Number, 5), (Svt.Number, 3)), Nop()),
                Cond(Sct.GlobalQuestState,
                    Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 1)),
                    Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 2)),
                    (Svt.Number, 5), (Svt.Number, 3))));
            Assert.That(g.GetGlobalQuestState(5), Is.EqualTo(3));
            Assert.That(g.GetVar(0), Is.EqualTo(1)); // quest 5 in state 3 → action
        }

        [Test]
        public void PcQuestState_SetThenTested()
        {
            var (_, g, _, _) = Run(File(
                Cond(Sct.True, Act(Sat.SetPcQuestState, Focus, (Svt.Number, 7), (Svt.Number, 2)), Nop()),
                Cond(Sct.PcQuestState,
                    Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 1)),
                    Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.Number, 2)),
                    Focus, (Svt.Number, 7), (Svt.Number, 2))));
            Assert.That(g.GetPcQuestState(7), Is.EqualTo(2));
            Assert.That(g.GetVar(0), Is.EqualTo(1));
        }

        [Test]
        public void PcVar_RoundTripsThroughOperand()
        {
            var (_, g, _, _) = Run(File(
                Cond(Sct.True, Act(Sat.AssignNum, (Svt.PcVar, 7), (Svt.Number, 42)), Nop()),
                Cond(Sct.True, Act(Sat.AssignNum, (Svt.GlVar, 0), (Svt.PcVar, 7)), Nop())));
            Assert.That(g.GetPcVar(7), Is.EqualTo(42));
            Assert.That(g.GetVar(0), Is.EqualTo(42)); // read back through the operand
        }

        [Test]
        public void PcFlag_SetThroughOperand()
        {
            var (_, g, _, _) = Run(File(
                Cond(Sct.True, Act(Sat.AssignNum, (Svt.PcFlag, 40), (Svt.Number, 1)), Nop())));
            Assert.That(g.GetPcFlag(40), Is.EqualTo(1)); // word 1, bit 8
        }

        // ── heartbeat opcodes (self-gating NPCs) ──

        [Test]
        public void ToggleState_TogglesResolvedObjectOff()
        {
            var host = new StubHost { Resolved = "NPC" };
            new ScriptVm(host).Execute(File(Cond(Sct.True, Act(Sat.ToggleState, Obj(Sfo.Attachee)), Nop())), new ScriptContext());
            Assert.That(host.ToggleOffCalls, Is.EqualTo(1));
            Assert.That(host.ToggledOff, Is.EqualTo("NPC"));
        }

        [Test]
        public void Kill_KillsResolvedObject()
        {
            var host = new StubHost { Resolved = "NPC" };
            new ScriptVm(host).Execute(File(Cond(Sct.True, Act(Sat.Kill, Obj(Sfo.Attachee)), Nop())), new ScriptContext());
            Assert.That(host.KillCalls, Is.EqualTo(1));
            Assert.That(host.Killed, Is.EqualTo("NPC"));
        }

        [Test]
        public void RemoveThisScript_SignalsRemovalAndZeroesScriptNum()
        {
            var ctx = new ScriptContext { ScriptNum = 1781 };
            new ScriptVm(new StubHost()).Execute(File(Cond(Sct.True, Act(Sat.RemoveThisScript), Nop())), ctx);
            Assert.That(ctx.RemoveScript, Is.True);
            Assert.That(ctx.ScriptNum, Is.EqualTo(0));
        }

        // The pc_chooses gate: a heartbeat that kills the attachee unless GlobalFlag[N] is set.
        [Test]
        public void HeartbeatGate_KillsWhenFlagUnset()
        {
            var host = new StubHost { Resolved = "TEMPLATE_NPC" };
            var g = new ScriptGlobals();
            // if GlobalFlag[2350]==1 -> ReturnAndSkipDefault else Goto(line 1);  line1: Kill(attachee)
            new ScriptVm(host, g).Execute(File(
                Cond(Sct.Eq, Act(Sat.ReturnAndSkipDefault), Act(Sat.Goto, (Svt.Number, 1)), (Svt.GlFlag, 2350), (Svt.Number, 1)),
                Cond(Sct.True, Act(Sat.Kill, Obj(Sfo.Attachee)), Nop())), new ScriptContext());
            Assert.That(host.KillCalls, Is.EqualTo(1)); // flag unset → killed

            var host2 = new StubHost { Resolved = "TEMPLATE_NPC" };
            var g2 = new ScriptGlobals(); g2.SetFlag(2350, 1);
            new ScriptVm(host2, g2).Execute(File(
                Cond(Sct.Eq, Act(Sat.ReturnAndSkipDefault), Act(Sat.Goto, (Svt.Number, 1)), (Svt.GlFlag, 2350), (Svt.Number, 1)),
                Cond(Sct.True, Act(Sat.Kill, Obj(Sfo.Attachee)), Nop())), new ScriptContext());
            Assert.That(host2.KillCalls, Is.EqualTo(0)); // flag set → not killed
        }

        [Test]
        public void FloatLine_CallsHostWithLineAndResolvedNpc()
        {
            var host = new StubHost { Resolved = "NPC" };
            var ctx = new ScriptContext { ScriptNum = 1234 };
            // op[0] = line (5), op[1] = npc focus
            new ScriptVm(host).Execute(File(Cond(Sct.True, Act(Sat.FloatLine, (Svt.Number, 5), Focus), Nop())), ctx);
            Assert.That(host.FloatCalls, Is.EqualTo(1));
            Assert.That(host.FloatObj, Is.EqualTo("NPC"));
            Assert.That(host.FloatScript, Is.EqualTo(1234));
            Assert.That(host.FloatLineNum, Is.EqualTo(5));
        }

        [Test]
        public void PrintLine_UsesAttachee_AndSkipsWhenScriptNumZero()
        {
            var noNum = new StubHost();
            new ScriptVm(noNum).Execute(File(Cond(Sct.True, Act(Sat.PrintLine, (Svt.Number, 3)), Nop())), new ScriptContext { ScriptNum = 0 });
            Assert.That(noNum.PrintCalls, Is.EqualTo(0)); // engine guards on script->num != 0

            var host = new StubHost();
            new ScriptVm(host).Execute(File(Cond(Sct.True, Act(Sat.PrintLine, (Svt.Number, 3)), Nop())), new ScriptContext { ScriptNum = 99, Attachee = "DOOR" });
            Assert.That(host.PrintCalls, Is.EqualTo(1));
            Assert.That(host.PrintObj, Is.EqualTo("DOOR"));
            Assert.That(host.PrintLineNum, Is.EqualTo(3));
        }
    }
}
