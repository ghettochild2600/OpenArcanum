namespace Arcanum.Script
{
    /// <summary>
    /// The state of one script invocation — the engine's <c>ScriptInvocation</c> + <c>ScriptState</c> rolled
    /// into one. Holds the focus objects (who triggered it, what it's attached to) and the per-run local
    /// variables/flags/counters the VM reads and writes.
    /// <para>Focus objects are kept as opaque <see cref="object"/> so this module stays independent of the
    /// world types — the <see cref="IScriptHost"/> resolves and queries them when object-typed opcodes land
    /// (Phase 2). The value/flag/var opcodes (Phase 1) don't touch them.</para>
    /// </summary>
    public sealed class ScriptContext
    {
        /// <summary>The object that fired the script (usually the player who clicked/used the attachee).</summary>
        public object Triggerer;
        /// <summary>The object the script is attached to (the door/scenery/NPC being acted on).</summary>
        public object Attachee;
        /// <summary>An extra context object some attachment points pass (often null).</summary>
        public object Extra;
        /// <summary>The current element of a running <c>SAT_LOOP_FOR</c> (engine <c>SFO_CURRENT_LOOPED_OBJECT</c>);
        /// null outside a loop.</summary>
        public object CurrentLoopedObject;
        /// <summary>Which <c>SAP_*</c> fired this script.</summary>
        public int AttachmentPoint;
        /// <summary>The running script's number — used to locate its <c>.dlg</c> for <c>FLOAT_LINE</c>/<c>PRINT_LINE</c>
        /// (engine <c>state-&gt;invocation-&gt;script-&gt;num</c>); 0 means messages are skipped.</summary>
        public int ScriptNum;
        /// <summary>Set by <c>SAT_REMOVE_THIS_SCRIPT</c> (engine <c>state-&gt;script_num = 0</c>): the host should
        /// detach the running script from the object so it won't fire again.</summary>
        public bool RemoveScript;

        // Per-invocation locals (engine ScriptState.lc_vars[10] / lc_objs[10]; per-run scratch).
        public readonly int[] LocalVars = new int[10];
        public readonly object[] LocalObjects = new object[10]; // SFO_LOCAL_OBJECT slots (SAT_ASSIGN_OBJ)

        // Local flags + four packed 8-bit counters — the engine keeps these in the running script's header
        // (the OBJ_F_SCRIPTS slot). We hold them per-invocation; persisting them on the object is a later step
        // (see Docs/Scripting.md), so cross-invocation local state should use GLOBAL flags/vars for now.
        public uint LocalFlags;
        public uint LocalCounters;
    }
}
