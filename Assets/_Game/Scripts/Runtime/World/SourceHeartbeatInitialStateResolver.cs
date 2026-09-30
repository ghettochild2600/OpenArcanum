using System;
using Arcanum.Formats.Script;
using Arcanum.Runtime.Campaign;
using Arcanum.Script;

namespace Arcanum.Runtime.World
{
    /// <summary>
    /// Resolves the bounded, state-only heartbeat programs used by retail map records to establish
    /// whether an object initially exists or is already dead. It intentionally rejects every opcode
    /// capable of ordinary gameplay side effects.
    /// </summary>
    internal static class SourceHeartbeatInitialStateResolver
    {
        internal enum Result
        {
            None,
            Off,
            Dead,
        }

        public static bool TryResolve(ScriptFile script, CampaignStateService campaign, out Result result)
        {
            result = Result.None;
            if (script == null || campaign == null || script.Entries.Count == 0 || !IsStateOnly(script))
                return false;

            var host = new InitialStateHost();
            var attachee = new object();
            var context = new ScriptContext
            {
                Attachee = attachee,
                AttachmentPoint = (int)Sap.Heartbeat,
                ScriptNum = 1,
            };
            host.Bind(attachee);
            ScriptExecutionResult execution = new ScriptVm(host, campaign).ExecuteStrict(script, context);
            if (!execution.Succeeded || host.Invalid || host.Killed && host.Off) return false;

            result = host.Killed ? Result.Dead : host.Off ? Result.Off : Result.None;
            return true;
        }

        private static bool IsStateOnly(ScriptFile script)
        {
            for (int index = 0; index < script.Entries.Count; index++)
            {
                ScriptCondition condition = script.Entries[index];
                if ((Sct)condition.Type == Sct.Eq)
                {
                    if (!IsFlagNumberPair(condition.OpType[0], condition.OpType[1])) return false;
                }
                else if ((Sct)condition.Type != Sct.True) return false;

                if (!IsStateOnly(condition.Action, script.Entries.Count)
                    || !IsStateOnly(condition.Els, script.Entries.Count)) return false;
            }
            return true;
        }

        private static bool IsFlagNumberPair(byte first, byte second)
            => (Svt)first == Svt.GlFlag && (Svt)second == Svt.Number
               || (Svt)first == Svt.Number && (Svt)second == Svt.GlFlag;

        private static bool IsStateOnly(ScriptAction action, int lineCount)
        {
            if (action == null) return false;
            switch ((Sat)action.Type)
            {
                case Sat.DoNothing:
                case Sat.RemoveThisScript:
                case Sat.ReturnAndSkipDefault:
                case Sat.ReturnAndRunDefault:
                    return true;
                case Sat.Goto:
                    return (Svt)action.OpType[0] == Svt.Number
                           && action.OpValue[0] >= 0 && action.OpValue[0] < lineCount;
                case Sat.ToggleState:
                case Sat.Kill:
                    return (Sfo)action.OpType[0] == Sfo.Attachee;
                default:
                    return false;
            }
        }

        private sealed class InitialStateHost : ScriptHostAdapter
        {
            private object _attachee;
            public bool Off { get; private set; }
            public bool Killed { get; private set; }
            public bool Invalid { get; private set; }

            public void Bind(object attachee) => _attachee = attachee;

            public override object[] ResolveFocus(int sfoType, int sfoValue, ScriptContext context)
            {
                if ((Sfo)sfoType == Sfo.Attachee && ReferenceEquals(context.Attachee, _attachee))
                    return new[] { _attachee };
                Invalid = true;
                return Array.Empty<object>();
            }

            public override void ToggleOff(object obj)
            {
                if (!ReferenceEquals(obj, _attachee)) Invalid = true;
                else Off = !Off;
            }

            public override void Kill(object obj)
            {
                if (!ReferenceEquals(obj, _attachee)) Invalid = true;
                else Killed = true;
            }
        }
    }
}
