using System;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Script;
using Arcanum.Script;

namespace Arcanum.Runtime.World
{
    /// <summary>An opaque script focus value carrying only a stable gameplay identity.</summary>
    public readonly struct WorldScriptObjectReference
    {
        public ArcanumObjectId Identity { get; }
        public WorldScriptObjectReference(ArcanumObjectId identity) => Identity = identity;
        public override string ToString() => Identity.ToString();
    }

    /// <summary>Production SAP_USE boundary. It owns invocation policy and never exposes Unity objects to the VM.</summary>
    public sealed class WorldUseScriptDispatcher
    {
        private readonly WorldMapSessionCoordinator _session;
        private readonly Func<int, ScriptFile> _resolveScript;
        private readonly ProductionUseScriptHost _host;

        public IScriptGlobals Globals { get; }

        public WorldUseScriptDispatcher(WorldMapSessionCoordinator session, Func<int, ScriptFile> resolveScript,
            IScriptGlobals globals)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _resolveScript = resolveScript ?? throw new ArgumentNullException(nameof(resolveScript));
            Globals = globals ?? throw new ArgumentNullException(nameof(globals));
            _host = new ProductionUseScriptHost(session, resolveScript);
        }

        public ScriptExecutionResult DispatchUse(ArcanumObjectId actor, ArcanumObjectId target, int scriptNum)
            => Dispatch(actor, target, scriptNum, bedUse: false);

        /// <summary>Runs the exact bounded opcode family used by shipped bed permission scripts. Source
        /// <c>sleep_ui_toggle</c> treats run-default as refusal and skip-default as permission.</summary>
        public ScriptExecutionResult DispatchBedUse(ArcanumObjectId actor, ArcanumObjectId target, int scriptNum)
            => Dispatch(actor, target, scriptNum, bedUse: true);

        internal ScriptExecutionResult DispatchScheduled(ScheduledScriptCall call)
        {
            ScriptFile file = _resolveScript(call.ScriptNumber);
            if (file == null) return new ScriptExecutionResult(ScriptExecutionStatus.MissingScript);
            var context = new ScriptContext
            {
                Triggerer = new WorldScriptObjectReference(call.Triggerer),
                Attachee = new WorldScriptObjectReference(call.Attachee),
                AttachmentPoint = (int)Sap.Use,
                ScriptNum = call.ScriptNumber,
            };
            return new ScriptVm(_host, Globals).ExecuteStrict(file, context, call.StartLine);
        }

        private ScriptExecutionResult Dispatch(ArcanumObjectId actor, ArcanumObjectId target, int scriptNum,
            bool bedUse)
        {
            if (_session.PlayerState == null || _session.PlayerState.Identity != actor
                || !_session.TryGetObjectState(target, out PersistentObjectState state)
                || state.Off || !_session.TryGetLoadedObject(target, out _))
                return new ScriptExecutionResult(ScriptExecutionStatus.InvalidContext);

            ScriptFile file = _resolveScript(scriptNum);
            if (file == null) return new ScriptExecutionResult(ScriptExecutionStatus.MissingScript);
            if (!ProductionUseScriptPolicy.Supports(file, bedUse, out string unsupported))
                return new ScriptExecutionResult(ScriptExecutionStatus.UnsupportedOpcode, detail: unsupported);

            var context = new ScriptContext
            {
                Triggerer = new WorldScriptObjectReference(actor),
                Attachee = new WorldScriptObjectReference(target),
                AttachmentPoint = (int)Sap.Use,
                ScriptNum = scriptNum,
                LocalFlags = state.UseScriptLocalFlags,
                LocalCounters = state.UseScriptLocalCounters,
            };
            ScriptExecutionResult result = new ScriptVm(_host, Globals).ExecuteStrict(file, context);
            state.UseScriptLocalFlags = context.LocalFlags;
            state.UseScriptLocalCounters = context.LocalCounters;
            if (context.ReplacementScriptNum.HasValue) state.UseScriptNum = context.ReplacementScriptNum.Value;
            return result;
        }

        private static class ProductionUseScriptPolicy
        {
            public static bool Supports(ScriptFile file, bool bedUse, out string detail)
            {
                if (file.Entries.Count == 0)
                {
                    detail = "empty SAP_USE script";
                    return false;
                }
                for (int i = 0; i < file.Entries.Count; i++)
                {
                    ScriptCondition entry = file.Entries[i];
                    if (!SupportedCondition((Sct)entry.Type, bedUse))
                    {
                        detail = $"condition {(Sct)entry.Type} at line {i}";
                        return false;
                    }
                    if (!SupportedAction(entry.Action, bedUse) || !SupportedAction(entry.Els, bedUse))
                    {
                        ScriptAction action = !SupportedAction(entry.Action, bedUse) ? entry.Action : entry.Els;
                        detail = $"action {(Sat)action.Type} at line {i}";
                        return false;
                    }
                }
                detail = null;
                return true;
            }

            private static bool SupportedCondition(Sct condition, bool bedUse)
                => condition == Sct.True || condition == Sct.Eq || condition == Sct.Le
                   || condition == Sct.GlobalFlag || condition == Sct.LocalFlag
                   || bedUse && condition == Sct.PcQuestState;

            private static bool SupportedAction(ScriptAction action, bool bedUse)
            {
                if (action == null) return false;
                Sat type = (Sat)action.Type;
                return type == Sat.DoNothing || type == Sat.ReturnAndSkipDefault
                       || type == Sat.ReturnAndRunDefault || type == Sat.Goto
                       || bedUse && (type is Sat.SetLocalFlag or Sat.ClearLocalFlag or Sat.AssignNum
                           or Sat.FloatLine or Sat.GetHitPoints);
            }
        }

        private sealed class ProductionUseScriptHost : WorldScriptCampaignHost
        {
            public ProductionUseScriptHost(WorldMapSessionCoordinator session, Func<int, ScriptFile> resolveScript)
                : base(session, resolveScript) { }

            public override void GetHitPoints(object obj, out int current, out int maximum)
            {
                current = 0;
                maximum = 0;
                if (obj is not WorldScriptObjectReference reference
                    || !Session.Vitality.TryGet(reference.Identity, out var vitality)) return;
                current = vitality.CurrentHitPoints;
                maximum = vitality.MaximumHitPoints;
            }

            // Bed scripts float an authored reason before sleep_ui emits its standard refusal. The bounded
            // world dispatcher does not own transient bubbles, so presentation retains the standard refusal.
            public override void FloatLine(object obj, int scriptNum, int lineNum) { }
        }
    }
}
