using System.Collections.Generic;
using Arcanum.Formats.Script;

namespace Arcanum.Script
{
    /// <summary>
    /// Executes a parsed <c>.scr</c> script. Ports the engine's execution model (arcanum-ce
    /// <c>script_execute</c> / <c>script_execute_condition</c> / <c>script_execute_action</c>): a flat list of
    /// entries run from a start line, each evaluating a condition and running its <c>action</c> (true) or
    /// <c>els</c> (false); the action returns a control code that advances, jumps, or returns.
    /// <para><b>Implemented:</b> control flow; operand resolution (number, global/local var, global flag,
    /// counter); conditions <c>TRUE/EQ/LE/GLOBAL_FLAG/LOCAL_FLAG</c>; actions for assignment/arithmetic,
    /// global/local flags, story state, and teleport. <b>Not yet:</b> object-typed opcodes (doors, messages,
    /// quests, PC vars/flags) — those need the host and are logged once + treated as default. See
    /// Docs/Scripting.md.</para>
    /// </summary>
    public sealed class ScriptVm
    {
        // Control-flow sentinels (engine script.c).
        private const int Next = -1;
        private const int ReturnAndSkipDefault = -2;
        private const int ReturnAndRunDefault = -3;
        private const int MaxIterations = 1000; // engine's runaway guard

        private object[] _loopSet;   // SAT_LOOP_FOR — the focus set being iterated (null = no active loop)
        private int _loopIndex, _loopForLine;
        private int _callDepth;              // SAT_CALL_SCRIPT nesting (guards runaway recursion)
        private const int MaxCallDepth = 16;

        // The file + line currently executing, so control-flow actions (LOOP_FOR) can scan for their LOOP_END.
        private ScriptFile _exFile;
        private int _exLine;

        private readonly IScriptHost _host;
        private readonly IScriptGlobals _globals;
        private readonly HashSet<string> _warned = new HashSet<string>(); // log each unimplemented opcode once
        private bool _strictExecution;

        private sealed class UnsupportedOpcodeException : System.Exception
        {
            public UnsupportedOpcodeException(string message) : base(message) { }
        }

        /// <summary>Warning sink for unimplemented opcodes (the runtime wires Debug.LogWarning). Static so the
        /// assembly stays engine-free (<c>noEngineReferences</c>); null ⇒ silent.</summary>
        public static System.Action<string> Log;

        /// <summary>The dice for <c>SAT_RANDOM</c> (engine <c>random_between</c>). Swap for a seeded instance in tests.</summary>
        public System.Random Rng = new System.Random();

        public ScriptVm(IScriptHost host, IScriptGlobals globals = null)
        {
            _host = host;
            _globals = globals ?? new ScriptGlobals();
        }

        /// <summary>Session-global script state (vars/flags/story/quests). The runtime backs this with the same
        /// GameState/CharacterStory the dialog system uses, so scripts and dialogs share one store.</summary>
        public IScriptGlobals Globals => _globals;

        /// <summary>Runs <paramref name="file"/> from <paramref name="startLine"/>. Returns whether the engine's
        /// <i>default</i> behaviour for the attachment point should still run afterwards (engine <c>run_default</c>)
        /// — e.g. a door whose use-script didn't teleport should still toggle open/closed.</summary>
        public bool Execute(ScriptFile file, ScriptContext ctx, int startLine = 0)
        {
            if (file == null || file.Entries.Count == 0) return true;

            return ExecuteCore(file, ctx, startLine, out _);
        }

        /// <summary>Production execution entry point. Missing/malformed scripts, unsupported opcodes,
        /// host exceptions, invalid flow and runaway execution are explicit failures and never permit default behavior.</summary>
        public ScriptExecutionResult ExecuteStrict(ScriptFile file, ScriptContext ctx, int startLine = 0)
        {
            if (file == null) return new ScriptExecutionResult(ScriptExecutionStatus.MissingScript);
            if (file.Entries.Count == 0) return new ScriptExecutionResult(ScriptExecutionStatus.EmptyScript);
            if (ctx == null) return new ScriptExecutionResult(ScriptExecutionStatus.InvalidContext);
            if ((uint)startLine >= (uint)file.Entries.Count)
                return new ScriptExecutionResult(ScriptExecutionStatus.InvalidStartLine);

            bool previous = _strictExecution;
            _strictExecution = true;
            try
            {
                bool runDefault = ExecuteCore(file, ctx, startLine, out ScriptExecutionStatus failure);
                return failure == ScriptExecutionStatus.Executed
                    ? new ScriptExecutionResult(ScriptExecutionStatus.Executed, runDefault)
                    : new ScriptExecutionResult(failure);
            }
            catch (UnsupportedOpcodeException ex)
            {
                return new ScriptExecutionResult(ScriptExecutionStatus.UnsupportedOpcode, detail: ex.Message);
            }
            catch (System.Exception ex)
            {
                return new ScriptExecutionResult(ScriptExecutionStatus.RuntimeError, detail: ex.Message);
            }
            finally
            {
                _strictExecution = previous;
            }
        }

        private bool ExecuteCore(ScriptFile file, ScriptContext ctx, int startLine,
            out ScriptExecutionStatus failure)
        {
            failure = ScriptExecutionStatus.Executed;

            int line = startLine;
            bool runDefault = false;

            for (int iter = 0; iter < MaxIterations; iter++)
            {
                if (line < 0 || line >= file.Entries.Count) break;

                _exFile = file; _exLine = line; // for control-flow actions that need to scan the file
                int next = ExecuteCondition(file.Entries[line], ctx);
                if (next == Next)
                {
                    if (line < file.Entries.Count - 1) line++;
                    else next = ReturnAndSkipDefault; // ran off the end → stop, skip default
                }
                else
                {
                    line = next;
                }

                if (next == ReturnAndSkipDefault || next == ReturnAndRunDefault)
                {
                    runDefault = next == ReturnAndRunDefault;
                    return runDefault;
                }
            }
            failure = ScriptExecutionStatus.Runaway;
            return runDefault;
        }

        private int ExecuteCondition(ScriptCondition c, ScriptContext ctx)
        {
            bool met = EvaluateCondition(c, ctx);
            return ExecuteAction(met ? c.Action : c.Els, ctx);
        }

        private bool EvaluateCondition(ScriptCondition c, ScriptContext ctx)
        {
            switch ((Sct)c.Type)
            {
                case Sct.True:
                    return true;
                case Sct.Eq:
                    return GetValue(c.OpType[0], c.OpValue[0], ctx) == GetValue(c.OpType[1], c.OpValue[1], ctx);
                case Sct.Le:
                    return GetValue(c.OpType[0], c.OpValue[0], ctx) <= GetValue(c.OpType[1], c.OpValue[1], ctx);
                case Sct.GlobalFlag:
                    return _globals.GetFlag(GetValue(c.OpType[0], c.OpValue[0], ctx)) != 0;
                case Sct.LocalFlag:
                    return (ctx.LocalFlags & (1u << GetValue(c.OpType[0], c.OpValue[0], ctx))) != 0;

                // Object state (op[0] = focus set). Resolve the set and count matches: true when ≥1 matches and
                // either all match or op[0] is an "any-of" SFO (engine: matched != 0 && (matched == cnt || sfo_is_any)).
                case Sct.ObjIsOpen:        return EvalObjSet(c, ctx, _host.IsOpen);
                case Sct.ObjIsDead:        return EvalObjSet(c, ctx, _host.IsDead);
                case Sct.ObjIsSwitchedOff: return EvalObjSet(c, ctx, _host.IsSwitchedOff);
                case Sct.ObjFollowingPc:   return EvalObjSet(c, ctx, _host.IsFollowingPc); // op1 = pc (single PC, not compared)
                case Sct.ObjIsInCombat:    return EvalObjSet(c, ctx, _host.IsInCombat);
                case Sct.ObjIsAnimal:      return EvalObjSet(c, ctx, _host.IsAnimal);
                case Sct.ObjIsUndead:      return EvalObjSet(c, ctx, _host.IsUndead);
                case Sct.HasGold: // op0=set, op1=amount
                {
                    int amount = GetValue(c.OpType[1], c.OpValue[1], ctx);
                    return EvalObjSet(c, ctx, o => _host.GetGold(o) >= amount);
                }
                case Sct.ObjIsAtLocation: // op0=set, op1=x, op2=y
                {
                    int x = GetValue(c.OpType[1], c.OpValue[1], ctx), y = GetValue(c.OpType[2], c.OpValue[2], ctx);
                    return EvalObjSet(c, ctx, o => _host.IsAtTile(o, x, y));
                }
                case Sct.ObjWithinRange: // op0=set, op1=range, op2=x, op3=y
                {
                    int range = GetValue(c.OpType[1], c.OpValue[1], ctx);
                    int x = GetValue(c.OpType[2], c.OpValue[2], ctx), y = GetValue(c.OpType[3], c.OpValue[3], ctx);
                    return EvalObjSet(c, ctx, o => _host.IsWithinRange(o, x, y, range));
                }
                case Sct.ObjIsNamed: // op0=set, op1=name index
                {
                    int name = GetValue(c.OpType[1], c.OpValue[1], ctx);
                    return EvalObjSet(c, ctx, o => _host.IsNamed(o, name));
                }
                case Sct.KnowsSpell: // op0=set, op1=spell id
                {
                    int spl = GetValue(c.OpType[1], c.OpValue[1], ctx);
                    return EvalObjSet(c, ctx, o => _host.KnowsSpell(o, spl));
                }
                case Sct.ObjMetPcBefore: return EvalObjSet(c, ctx, _host.HasMetPc); // reaction_met_before
                case Sct.ObjIsInDialog:  return EvalObjSet(c, ctx, _host.IsInDialog);
                case Sct.Daytime:        return _host.IsDaytime(); // game_time_is_day
                case Sct.ObjHasItemNamed: // op0=set, op1=item name index
                {
                    int name = GetValue(c.OpType[1], c.OpValue[1], ctx);
                    return EvalObjSet(c, ctx, o => _host.HasItemNamed(o, name));
                }
                case Sct.ObjIsMonsterOfType: // op0=set, op1=species
                {
                    int specie = GetValue(c.OpType[1], c.OpValue[1], ctx);
                    return EvalObjSet(c, ctx, o => _host.IsMonsterOfType(o, specie));
                }
                case Sct.ObjIsWieldingItem: // op0=set, op1=item name index (worn slots)
                {
                    int name = GetValue(c.OpType[1], c.OpValue[1], ctx);
                    return EvalObjSet(c, ctx, o => _host.IsWieldingItemNamed(o, name));
                }
                case Sct.RumorKnown: // op0=pc obj (single), op1=rumor
                {
                    int rumor = GetValue(c.OpType[1], c.OpValue[1], ctx);
                    object o = GetObj(c.OpType[0], c.OpValue[0], ctx);
                    return o != null && _host.RumorKnown(o, rumor);
                }
                // Two-object AI checks (single objects, not sets — engine uses script_get_obj).
                case Sct.ObjCanSeeObj:
                    return CheckPair(c, ctx, _host.CanSeeObj);
                case Sct.ObjCanHearObj:
                    return CheckPair(c, ctx, _host.CanHearObj);
                case Sct.ObjCanOpenContainer:
                    return CheckPair(c, ctx, _host.CanOpenContainer);
                case Sct.CanOpenPortal: // op0=actor, op1=portal, op2=direction
                {
                    object actor = GetObj(c.OpType[0], c.OpValue[0], ctx);
                    object portal = GetObj(c.OpType[1], c.OpValue[1], ctx);
                    int dir = GetValue(c.OpType[2], c.OpValue[2], ctx);
                    return actor != null && portal != null && _host.CanOpenPortal(actor, portal, dir);
                }

                // Quest state. PC quest (op0 = pc focus, ignored — single PC) op1=quest op2=state; global op0=quest op1=state.
                case Sct.PcQuestState:
                    return _globals.GetPcQuestState(GetValue(c.OpType[1], c.OpValue[1], ctx)) == GetValue(c.OpType[2], c.OpValue[2], ctx);
                case Sct.GlobalQuestState:
                    return _globals.GetGlobalQuestState(GetValue(c.OpType[0], c.OpValue[0], ctx)) == GetValue(c.OpType[1], c.OpValue[1], ctx);

                // Exceptional object states we don't model AT ALL — "not in that state" is the accurate
                // answer, not a guess, so these are FALSE. This matters: dialog scripts are chains of
                // "if special state → special dialog" entries with the unconditional default LAST, so an
                // unknown state defaulting true hijacks the conversation (Virgil's script 1324 line 2,
                // ObjJilted → the 'you abandoned me / can I rejoin' dialog at first meeting).
                case Sct.ObjHasBless:
                case Sct.ObjHasCurse:
                case Sct.ObjIsPolymorphed:
                case Sct.ObjIsShrunk:
                case Sct.ObjHasBodySpell:
                case Sct.ObjIsInvisible:
                case Sct.ObjHasMirrorImage:
                case Sct.ObjHasSurrendered:
                case Sct.ObjJilted:
                case Sct.ObjIsBusted:
                case Sct.ObjHasMaxFollowers:
                case Sct.ObjIsInfluencedBySpell:
                case Sct.ObjHasBadAssociates:
                case Sct.ObjIsInvulnerable:
                case Sct.ObjHasReputation:
                case Sct.Prowling:
                case Sct.WaitingForLeader:
                case Sct.ItemsAreBeingRewielded:
                case Sct.RumorQuelled:
                case Sct.MonstergenDisabled:
                case Sct.SectorIsBlocked:
                case Sct.Identified:
                case Sct.MasteredSpellCollege:
                    WarnOnce($"state condition {(Sct)c.Type} (assumed FALSE — state not modelled)");
                    return false;

                // Any remaining unimplemented condition defaults to true so the common unconditional
                // door/teleport scripts still fire; a guarded script may take a wrong branch until ported.
                // (The full SCT_* table is covered — this is a safety net for data that names an unknown code.)
                default:
                    WarnOnce($"condition {(Sct)c.Type} (assumed TRUE)");
                    return true;
            }
        }

        // A two-object AI check (SEE/HEAR/OPEN — engine resolves each op to a single object via script_get_obj).
        private bool CheckPair(ScriptCondition c, ScriptContext ctx, System.Func<object, object, bool> pred)
        {
            object a = GetObj(c.OpType[0], c.OpValue[0], ctx);
            object b = GetObj(c.OpType[1], c.OpValue[1], ctx);
            return a != null && b != null && pred(a, b);
        }

        private int ExecuteAction(ScriptAction a, ScriptContext ctx)
        {
            switch ((Sat)a.Type)
            {
                case Sat.DoNothing: return Next;
                case Sat.ReturnAndSkipDefault: return ReturnAndSkipDefault;
                case Sat.ReturnAndRunDefault: return ReturnAndRunDefault;
                case Sat.Goto: return GetValue(a.OpType[0], a.OpValue[0], ctx); // op[0] = target line

                // ── assignment / arithmetic: set_value(op0, get(op1) OP get(op2)) ──
                case Sat.AssignNum:
                    SetValue(a.OpType[0], a.OpValue[0], ctx, GetValue(a.OpType[1], a.OpValue[1], ctx));
                    return Next;
                case Sat.Add:
                    SetValue(a.OpType[0], a.OpValue[0], ctx,
                        GetValue(a.OpType[1], a.OpValue[1], ctx) + GetValue(a.OpType[2], a.OpValue[2], ctx));
                    return Next;
                case Sat.Subtract:
                    SetValue(a.OpType[0], a.OpValue[0], ctx,
                        GetValue(a.OpType[1], a.OpValue[1], ctx) - GetValue(a.OpType[2], a.OpValue[2], ctx));
                    return Next;
                case Sat.Multiply:
                    SetValue(a.OpType[0], a.OpValue[0], ctx,
                        GetValue(a.OpType[1], a.OpValue[1], ctx) * GetValue(a.OpType[2], a.OpValue[2], ctx));
                    return Next;
                case Sat.Divide:
                {
                    int divisor = GetValue(a.OpType[2], a.OpValue[2], ctx); // (engine guards /0; its decompile adds, the intent divides)
                    if (divisor != 0)
                        SetValue(a.OpType[0], a.OpValue[0], ctx, GetValue(a.OpType[1], a.OpValue[1], ctx) / divisor);
                    return Next;
                }

                // ── critter / world side-effects (host-backed) ──
                case Sat.AdjustGold: // op0=obj, op1=amount (engine item_gold_transfer)
                    _host.AdjustGold(GetObj(a.OpType[0], a.OpValue[0], ctx), GetValue(a.OpType[1], a.OpValue[1], ctx));
                    return Next;
                case Sat.CritterFollow: // op0=critter → joins the PC's party
                    _host.CritterFollow(GetObj(a.OpType[0], a.OpValue[0], ctx));
                    return Next;
                case Sat.CritterDisband: // op0=critter → leaves the party
                    _host.CritterDisband(GetObj(a.OpType[0], a.OpValue[0], ctx));
                    return Next;
                case Sat.GetStat: // op0=stat, op1=obj, op2=dest var
                    SetValue(a.OpType[2], a.OpValue[2], ctx,
                        _host.GetStat(GetObj(a.OpType[1], a.OpValue[1], ctx), GetValue(a.OpType[0], a.OpValue[0], ctx)));
                    return Next;
                case Sat.GetSkill: // op0=skill, op1=obj, op2=dest var
                    SetValue(a.OpType[2], a.OpValue[2], ctx,
                        _host.GetSkill(GetObj(a.OpType[1], a.OpValue[1], ctx), GetValue(a.OpType[0], a.OpValue[0], ctx)));
                    return Next;
                case Sat.Random: // op0=lower, op1=upper (inclusive), op2=dest var
                {
                    int lo = GetValue(a.OpType[0], a.OpValue[0], ctx), hi = GetValue(a.OpType[1], a.OpValue[1], ctx);
                    SetValue(a.OpType[2], a.OpValue[2], ctx, hi >= lo ? Rng.Next(lo, hi + 1) : lo);
                    return Next;
                }

                // The attachee NPC opens its dialog with the triggerer (the PC); op0 = the .dlg starting line.
                // The current script line rides along so the dialog can hand control BACK to this script when
                // it ends (engine dialog_ui_execute_script: response 0 → script_line+1, negative → that line).
                // Engine returns RETURN_AND_SKIP_DEFAULT — the conversation takes over.
                case Sat.Dialog:
                    _host.StartDialog(GetObj((byte)Sfo.Attachee, 0, ctx), GetValue(a.OpType[0], a.OpValue[0], ctx),
                        ctx.ScriptNum, _exLine);
                    return ReturnAndSkipDefault;

                // ── combat / magick (host-backed) ──
                case Sat.Attack: // op0=attacker, op1=target
                    _host.Attack(GetObj(a.OpType[0], a.OpValue[0], ctx), GetObj(a.OpType[1], a.OpValue[1], ctx));
                    return Next;
                case Sat.Damage: // op0=obj, op1=amount, op2=damage type
                    _host.Damage(GetObj(a.OpType[0], a.OpValue[0], ctx),
                        GetValue(a.OpType[1], a.OpValue[1], ctx), GetValue(a.OpType[2], a.OpValue[2], ctx));
                    return Next;
                case Sat.HealHp: // op0=obj, op1=amount
                    _host.HealHp(GetObj(a.OpType[0], a.OpValue[0], ctx), GetValue(a.OpType[1], a.OpValue[1], ctx));
                    return Next;
                case Sat.HealFatigue: // op0=obj, op1=amount
                    _host.HealFatigue(GetObj(a.OpType[0], a.OpValue[0], ctx), GetValue(a.OpType[1], a.OpValue[1], ctx));
                    return Next;
                case Sat.CastSpell: // op0=source, op1=spell id, op2=target
                    _host.CastSpell(GetObj(a.OpType[0], a.OpValue[0], ctx),
                        GetValue(a.OpType[1], a.OpValue[1], ctx), GetObj(a.OpType[2], a.OpValue[2], ctx));
                    return Next;

                // ── world / story (host-backed) ──
                case Sat.MarkMapLocation: // op0=area, op1=pc obj → area_set_known
                    _host.MarkMapLocation(GetObj(a.OpType[1], a.OpValue[1], ctx), GetValue(a.OpType[0], a.OpValue[0], ctx));
                    return Next;
                case Sat.SetRumor: // op0=rumor, op1=obj → rumor_known_set
                    _host.SetRumor(GetObj(a.OpType[1], a.OpValue[1], ctx), GetValue(a.OpType[0], a.OpValue[0], ctx));
                    return Next;
                case Sat.UnfogTownmap: // op0=town map → townmap_set_known
                    _host.UnfogTownmap(GetValue(a.OpType[0], a.OpValue[0], ctx));
                    return Next;
                case Sat.QuellRumor: // op0=rumor, op1=obj → rumor_qstate_set (PC only)
                    _host.QuellRumor(GetObj(a.OpType[1], a.OpValue[1], ctx), GetValue(a.OpType[0], a.OpValue[0], ctx));
                    return Next;
                // Run another object's attachment-point script (engine sub_44B170): op0=attachee, op1=SAP,
                // op2=line, op3=triggerer. Used by tile/cutscene scripts to open a dialog (SAP_DIALOG @ line).
                case Sat.CallScriptEx:
                    _host.RunObjectScript(GetObj(a.OpType[3], a.OpValue[3], ctx), GetObj(a.OpType[0], a.OpValue[0], ctx),
                        GetValue(a.OpType[1], a.OpValue[1], ctx), GetValue(a.OpType[2], a.OpValue[2], ctx));
                    return Next;

                // ── flags ──
                case Sat.SetLocalFlag:
                    ctx.LocalFlags |= 1u << GetValue(a.OpType[0], a.OpValue[0], ctx);
                    return Next;
                case Sat.ClearLocalFlag:
                    ctx.LocalFlags &= ~(1u << GetValue(a.OpType[0], a.OpValue[0], ctx));
                    return Next;
                case Sat.SetGlobalFlag:
                    _globals.SetFlag(GetValue(a.OpType[0], a.OpValue[0], ctx), 1);
                    return Next;
                case Sat.ClearGlobalFlag:
                    _globals.SetFlag(GetValue(a.OpType[0], a.OpValue[0], ctx), 0);
                    return Next;

                // ── story state ──
                case Sat.GetStoryState:
                    SetValue(a.OpType[0], a.OpValue[0], ctx, _globals.StoryState);
                    return Next;
                case Sat.SetStoryState:
                    _globals.SetStoryState(GetValue(a.OpType[0], a.OpValue[0], ctx));
                    return Next;

                // ── object assignment + quest state ──
                case Sat.AssignObj: // op[0]=target focus slot, op[1]=source focus
                    SetObj(a.OpType[0], a.OpValue[0], ctx, GetObj(a.OpType[1], a.OpValue[1], ctx));
                    return Next;
                case Sat.SetPcQuestState: // op[0]=pc (ignored), op[1]=quest, op[2]=state
                    _globals.SetPcQuestState(GetValue(a.OpType[1], a.OpValue[1], ctx), GetValue(a.OpType[2], a.OpValue[2], ctx));
                    return Next;
                case Sat.SetQuestGlobalState: // op[0]=quest, op[1]=state
                    _globals.SetGlobalQuestState(GetValue(a.OpType[0], a.OpValue[0], ctx), GetValue(a.OpType[1], a.OpValue[1], ctx));
                    return Next;

                // ── object state (host) — op[0] = focus object ──
                case Sat.ToggleState: // toggle OF_OFF (self-gating NPCs hide themselves)
                    _host.ToggleOff(GetObj(a.OpType[0], a.OpValue[0], ctx));
                    return Next;
                case Sat.Kill:
                    _host.Kill(GetObj(a.OpType[0], a.OpValue[0], ctx));
                    return Next;
                case Sat.RemoveThisScript: // engine state->script_num = 0 — detach this script from the object
                    ctx.ScriptNum = 0;
                    ctx.RemoveScript = true;
                    return Next;

                // ── portals / locks (host) — op[0] = focus object ──
                case Sat.ToggleOpenClosed:
                    _host.TogglePortal(GetObj(a.OpType[0], a.OpValue[0], ctx));
                    return Next;
                case Sat.SetLockState:
                    _host.SetLocked(GetObj(a.OpType[0], a.OpValue[0], ctx),
                        GetValue(a.OpType[1], a.OpValue[1], ctx) != 0);
                    return Next;

                // ── messages (host): a line of the script's own .dlg ──
                case Sat.FloatLine: // op[0]=line, op[1]=npc focus
                    if (ctx.ScriptNum != 0)
                        _host.FloatLine(GetObj(a.OpType[1], a.OpValue[1], ctx), ctx.ScriptNum, GetValue(a.OpType[0], a.OpValue[0], ctx));
                    return Next;
                case Sat.PrintLine: // op[0]=line (op[1]=type, unused); attributed to the attachee
                    if (ctx.ScriptNum != 0)
                        _host.PrintLine(ctx.Attachee, ctx.ScriptNum, GetValue(a.OpType[0], a.OpValue[0], ctx));
                    return Next;

                // ── teleport (host) ──
                case Sat.Teleport:
                    // op[0]=obj, op[1]=map (5000+ key), op[2]=x, op[3]=y.
                    DoTeleport(GetValue(a.OpType[1], a.OpValue[1], ctx),
                        GetValue(a.OpType[2], a.OpValue[2], ctx), GetValue(a.OpType[3], a.OpValue[3], ctx));
                    return Next;
                case Sat.FadeAndTeleport:
                    // op[0..3]=fade fx (ignored), op[4]=map, op[5]=x, op[6]=y.
                    DoTeleport(GetValue(a.OpType[4], a.OpValue[4], ctx),
                        GetValue(a.OpType[5], a.OpValue[5], ctx), GetValue(a.OpType[6], a.OpValue[6], ctx));
                    return Next;

                // FOR-loops iterate a focus-object SET (engine script_resolve_focus_obj — nearby critters,
                // party members, …). We don't resolve that set yet (it needs world spatial queries), so we treat
                // LOOP_FOR iterates a focus-object set (engine sub_44BC60): each pass sets SFO_CURRENT_LOOPED_OBJECT
                // to the next element; LOOP_END back-jumps until the set is exhausted; LOOP_BREAK exits early. Loops
                // don't nest (the engine errors on that), so one loop state suffices. An empty set skips the body.
                case Sat.LoopFor:
                    _loopSet = _host.ResolveFocus(a.OpType[0], a.OpValue[0], ctx);
                    _loopForLine = _exLine;
                    _loopIndex = 0;
                    if (_loopSet == null || _loopSet.Length == 0) { _loopSet = null; ctx.CurrentLoopedObject = null; return SkipPastLoopEnd(); }
                    ctx.CurrentLoopedObject = _loopSet[0];
                    return Next;
                case Sat.LoopEnd:
                    if (_loopSet == null) return Next; // no active loop
                    _loopIndex++;
                    if (_loopIndex < _loopSet.Length) { ctx.CurrentLoopedObject = _loopSet[_loopIndex]; return _loopForLine + 1; }
                    _loopSet = null; ctx.CurrentLoopedObject = null;
                    return Next; // set exhausted → continue past LOOP_END
                case Sat.LoopBreak:
                    _loopSet = null; ctx.CurrentLoopedObject = null;
                    return SkipPastLoopEnd();

                // Run a sub-script now (engine sub_44B030): op0=script#, op1=start line, op2/op3=triggerer/attachee
                // sets — once per (triggerer × attachee) pair, with a fresh context. Re-entrant: save/restore the
                // execution + loop state, and guard nesting depth so a recursive script can't blow the stack.
                case Sat.CallScript:
                {
                    if (_callDepth >= MaxCallDepth) { WarnOnce("CALL_SCRIPT depth"); return Next; }
                    int subNum = GetValue(a.OpType[0], a.OpValue[0], ctx);
                    ScriptFile sub = _host.GetScript(subNum);
                    if (sub == null) return Next;
                    int startLine = GetValue(a.OpType[1], a.OpValue[1], ctx);
                    object[] trigs = _host.ResolveFocus(a.OpType[2], a.OpValue[2], ctx);
                    object[] atts = _host.ResolveFocus(a.OpType[3], a.OpValue[3], ctx);
                    ScriptFile sf = _exFile; int sl = _exLine; object[] lp = _loopSet; int li = _loopIndex, lf = _loopForLine;
                    _callDepth++;
                    try
                    {
                        if (trigs != null && atts != null)
                            foreach (object trig in trigs)
                                foreach (object att in atts)
                                {
                                    _loopSet = null;
                                    Execute(sub, new ScriptContext
                                    {
                                        Triggerer = trig, Attachee = att,
                                        AttachmentPoint = ctx.AttachmentPoint, ScriptNum = subNum,
                                    }, startLine);
                                }
                    }
                    finally
                    {
                        _callDepth--;
                        _exFile = sf; _exLine = sl; _loopSet = lp; _loopIndex = li; _loopForLine = lf;
                    }
                    return Next;
                }

                // Object-typed / game-system actions (doors, messages, quests, …) aren't ported yet.
                default:
                    WarnOnce($"action {(Sat)a.Type}");
                    return Next;
            }
        }

        // Returns the line just after the matching LOOP_END (engine sub_44BC60), so a loop we can't iterate is
        // skipped cleanly. Loops don't nest (the engine errors on that), so the first LOOP_END is the match.
        // If none is found (malformed), stop the script safely rather than risk a runaway.
        private int SkipPastLoopEnd()
        {
            if (_exFile == null) return Next;
            for (int i = _exLine + 1; i < _exFile.Entries.Count; i++)
            {
                ScriptCondition e = _exFile.Entries[i];
                if ((e.Action != null && e.Action.Type == (int)Sat.LoopEnd) ||
                    (e.Els != null && e.Els.Type == (int)Sat.LoopEnd))
                    return i + 1;
            }
            return ReturnAndSkipDefault;
        }

        private void DoTeleport(int mapKey, int x, int y)
        {
            // The script stores the destination map as its MapList .mes key (5000+); the engine subtracts
            // 4999 to get the 1-based map id (script.c SAT_TELEPORT).
            _host.Teleport(mapKey - 4999, x, y);
        }

        // ── operand resolution (engine script_get_value / script_set_value) ──

        private int GetValue(byte opType, int opValue, ScriptContext ctx)
        {
            switch ((Svt)opType)
            {
                case Svt.Number: return opValue;
                case Svt.GlVar: return _globals.GetVar(opValue);
                case Svt.GlFlag: return _globals.GetFlag(opValue);
                case Svt.LcVar: return (uint)opValue < (uint)ctx.LocalVars.Length ? ctx.LocalVars[opValue] : 0;
                case Svt.Counter: return (int)((ctx.LocalCounters >> (8 * (opValue & 3))) & 0xFF);
                // The operand packs (SFO<<16)|index; single PC, so the focus is ignored and we use the low index.
                case Svt.PcVar: return _globals.GetPcVar(opValue & 0xFFFF);
                case Svt.PcFlag: return _globals.GetPcFlag(opValue & 0xFFFF);
                default:
                    return opValue;
            }
        }

        private void SetValue(byte opType, int opValue, ScriptContext ctx, int value)
        {
            switch ((Svt)opType)
            {
                case Svt.GlVar: _globals.SetVar(opValue, value); break;
                case Svt.GlFlag: _globals.SetFlag(opValue, value); break;
                case Svt.LcVar:
                    if ((uint)opValue < (uint)ctx.LocalVars.Length) ctx.LocalVars[opValue] = value;
                    break;
                case Svt.Counter:
                {
                    int shift = 8 * (opValue & 3);
                    ctx.LocalCounters = (ctx.LocalCounters & ~(0xFFu << shift)) | (((uint)value & 0xFF) << shift);
                    break;
                }
                case Svt.Number: break; // a literal target is unreachable (engine notes the same)
                case Svt.PcVar: _globals.SetPcVar(opValue & 0xFFFF, value); break;
                case Svt.PcFlag: _globals.SetPcFlag(opValue & 0xFFFF, value); break;
            }
        }

        // Resolve an object operand (op_type is an Sfo focus type, not a Svt) to a single object — engine
        // script_get_obj returns the first of the set. Local-object slots live in the context; everything else
        // (triggerer/attachee/extra/player/vicinity) the host resolves.
        private object GetObj(byte sfoType, int sfoValue, ScriptContext ctx)
        {
            if ((Sfo)sfoType == Sfo.LocalObject)
                return (uint)sfoValue < (uint)ctx.LocalObjects.Length ? ctx.LocalObjects[sfoValue] : null;
            if ((Sfo)sfoType == Sfo.CurrentLoopedObject) return ctx.CurrentLoopedObject;
            object[] objs = _host.ResolveFocus(sfoType, sfoValue, ctx);
            return objs != null && objs.Length > 0 ? objs[0] : null;
        }

        // Resolves an object operand to its full focus SET (engine script_resolve_focus_obj).
        private object[] ResolveSet(byte sfoType, int sfoValue, ScriptContext ctx)
        {
            if ((Sfo)sfoType == Sfo.LocalObject)
            {
                object lo = (uint)sfoValue < (uint)ctx.LocalObjects.Length ? ctx.LocalObjects[sfoValue] : null;
                return lo != null ? new[] { lo } : System.Array.Empty<object>();
            }
            if ((Sfo)sfoType == Sfo.CurrentLoopedObject)
                return ctx.CurrentLoopedObject != null ? new[] { ctx.CurrentLoopedObject } : System.Array.Empty<object>();
            return _host.ResolveFocus(sfoType, sfoValue, ctx) ?? System.Array.Empty<object>();
        }

        // An object condition over op[0]'s focus set: true when ≥1 element matches and either all match or op[0] is
        // an "any-of" SFO (engine: matched != 0 && (matched == cnt || sfo_is_any)).
        private bool EvalObjSet(ScriptCondition c, ScriptContext ctx, System.Func<object, bool> pred)
        {
            object[] set = ResolveSet(c.OpType[0], c.OpValue[0], ctx);
            if (set.Length == 0) return false;
            int matched = 0;
            for (int i = 0; i < set.Length; i++) if (set[i] != null && pred(set[i])) matched++;
            return matched != 0 && (matched == set.Length || SfoIsAny(c.OpType[0]));
        }

        // The "any-of" focus types (engine script.h sfo_is_any): match ⇒ true if ≥1; the rest require all to match.
        private static bool SfoIsAny(byte sfoType)
        {
            switch ((Sfo)sfoType)
            {
                case Sfo.AnyFollower:
                case Sfo.AnyoneInParty:
                case Sfo.AnyoneInTeam:
                case Sfo.AnyoneInGroup:
                case Sfo.AnyoneInVicinity:
                case Sfo.AnySceneryInVicinity:
                case Sfo.AnyContainerInVicinity:
                case Sfo.AnyPortalInVicinity:
                case Sfo.AnyItemInVicinity:
                    return true;
                default:
                    return false;
            }
        }

        // Assign an object into a focus slot (engine script_set_obj). Only the context-side slots are settable.
        private void SetObj(byte sfoType, int sfoValue, ScriptContext ctx, object obj)
        {
            switch ((Sfo)sfoType)
            {
                case Sfo.Triggerer: ctx.Triggerer = obj; break;
                case Sfo.Attachee: ctx.Attachee = obj; break;
                case Sfo.ExtraObject: ctx.Extra = obj; break;
                case Sfo.LocalObject:
                    if ((uint)sfoValue < (uint)ctx.LocalObjects.Length) ctx.LocalObjects[sfoValue] = obj;
                    break;
                default:
                    WarnOnce($"set focus {(Sfo)sfoType}");
                    break;
            }
        }

        private void WarnOnce(string what)
        {
            if (_strictExecution) throw new UnsupportedOpcodeException(what);
            if (_warned.Add(what))
                Log?.Invoke($"[ScriptVm] unimplemented {what} — treated as default (see Docs/Scripting.md).");
        }
    }
}
