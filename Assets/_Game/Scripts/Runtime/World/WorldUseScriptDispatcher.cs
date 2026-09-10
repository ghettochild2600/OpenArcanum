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

        public ScriptGlobals Globals { get; }

        public WorldUseScriptDispatcher(WorldMapSessionCoordinator session, Func<int, ScriptFile> resolveScript,
            ScriptGlobals globals)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _resolveScript = resolveScript ?? throw new ArgumentNullException(nameof(resolveScript));
            Globals = globals ?? throw new ArgumentNullException(nameof(globals));
            _host = new ProductionUseScriptHost(session, resolveScript);
        }

        public ScriptExecutionResult DispatchUse(ArcanumObjectId actor, ArcanumObjectId target, int scriptNum)
        {
            if (_session.PlayerState == null || _session.PlayerState.Identity != actor
                || !_session.TryGetObjectState(target, out PersistentObjectState state)
                || state.Off || !_session.TryGetLoadedObject(target, out _))
                return new ScriptExecutionResult(ScriptExecutionStatus.InvalidContext);

            ScriptFile file = _resolveScript(scriptNum);
            if (file == null) return new ScriptExecutionResult(ScriptExecutionStatus.MissingScript);
            if (!ProductionUseScriptPolicy.Supports(file, out string unsupported))
                return new ScriptExecutionResult(ScriptExecutionStatus.UnsupportedOpcode, detail: unsupported);

            var context = new ScriptContext
            {
                Triggerer = new WorldScriptObjectReference(actor),
                Attachee = new WorldScriptObjectReference(target),
                AttachmentPoint = (int)Sap.Use,
                ScriptNum = scriptNum,
            };
            return new ScriptVm(_host, Globals).ExecuteStrict(file, context);
        }

        private static class ProductionUseScriptPolicy
        {
            public static bool Supports(ScriptFile file, out string detail)
            {
                if (file.Entries.Count == 0)
                {
                    detail = "empty SAP_USE script";
                    return false;
                }
                for (int i = 0; i < file.Entries.Count; i++)
                {
                    ScriptCondition entry = file.Entries[i];
                    if (!SupportedCondition((Sct)entry.Type))
                    {
                        detail = $"condition {(Sct)entry.Type} at line {i}";
                        return false;
                    }
                    if (!SupportedAction(entry.Action) || !SupportedAction(entry.Els))
                    {
                        ScriptAction action = !SupportedAction(entry.Action) ? entry.Action : entry.Els;
                        detail = $"action {(Sat)action.Type} at line {i}";
                        return false;
                    }
                }
                detail = null;
                return true;
            }

            private static bool SupportedCondition(Sct condition)
                => condition == Sct.True || condition == Sct.Eq || condition == Sct.Le
                   || condition == Sct.GlobalFlag || condition == Sct.LocalFlag;

            private static bool SupportedAction(ScriptAction action)
            {
                if (action == null) return false;
                Sat type = (Sat)action.Type;
                return type == Sat.DoNothing || type == Sat.ReturnAndSkipDefault
                       || type == Sat.ReturnAndRunDefault || type == Sat.Goto;
            }
        }

        private sealed class ProductionUseScriptHost : ScriptHostAdapter
        {
            private readonly WorldMapSessionCoordinator _session;
            private readonly Func<int, ScriptFile> _resolveScript;

            public ProductionUseScriptHost(WorldMapSessionCoordinator session, Func<int, ScriptFile> resolveScript)
            {
                _session = session;
                _resolveScript = resolveScript;
            }

            public override object[] ResolveFocus(int sfoType, int sfoValue, ScriptContext context)
            {
                switch ((Sfo)sfoType)
                {
                    case Sfo.Triggerer: return One(context.Triggerer);
                    case Sfo.Attachee: return One(context.Attachee);
                    case Sfo.ExtraObject: return One(context.Extra);
                    case Sfo.Player:
                        return _session.PlayerState != null
                            ? One(new WorldScriptObjectReference(_session.PlayerState.Identity))
                            : Array.Empty<object>();
                    default: throw Unsupported($"ResolveFocus({(Sfo)sfoType})");
                }
            }

            public override ScriptFile GetScript(int num) => _resolveScript(num);

            private static object[] One(object value) => value != null ? new[] { value } : Array.Empty<object>();
        }
    }
}
