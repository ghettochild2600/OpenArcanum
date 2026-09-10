namespace Arcanum.Script
{
    public enum ScriptExecutionStatus
    {
        Executed,
        MissingScript,
        EmptyScript,
        InvalidContext,
        InvalidStartLine,
        UnsupportedOpcode,
        Runaway,
        RuntimeError,
    }

    /// <summary>A fail-closed production execution result; unlike the legacy bool API it cannot confuse failure with default suppression.</summary>
    public readonly struct ScriptExecutionResult
    {
        public ScriptExecutionStatus Status { get; }
        public bool RunDefault { get; }
        public string Detail { get; }
        public bool Succeeded => Status == ScriptExecutionStatus.Executed;

        public ScriptExecutionResult(ScriptExecutionStatus status, bool runDefault = false, string detail = null)
        {
            Status = status;
            RunDefault = status == ScriptExecutionStatus.Executed && runDefault;
            Detail = detail;
        }
    }
}
