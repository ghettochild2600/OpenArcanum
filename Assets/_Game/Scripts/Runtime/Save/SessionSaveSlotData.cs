using System.Collections.Generic;

namespace Arcanum.Runtime.Save
{
    public sealed class SessionSaveSlotData
    {
        public string SlotFormat { get; set; }
        public int SlotVersion { get; set; }
        public SessionSaveSlotMetadata Metadata { get; set; }
        public SessionSaveData Session { get; set; }
    }

    public sealed class SessionSaveSlotMetadata
    {
        public string SlotId { get; set; }
        public string SavedAtUtc { get; set; }
        public string SessionFormat { get; set; }
        public int SessionVersion { get; set; }
        public string CurrentMap { get; set; }
        public string SelectedSector { get; set; }
        public string PcIdentity { get; set; }
        public int PcLevel { get; set; }
    }

    public sealed class SessionSaveSlotInfo
    {
        public string SlotId { get; internal set; }
        public bool IsValid { get; internal set; }
        public SessionSaveSlotMetadata Metadata { get; internal set; }
        public string Error { get; internal set; }
    }

    public enum SessionSaveSlotFailure
    {
        None,
        InvalidSlotId,
        NoActiveSession,
        MissingSlot,
        ReadFailed,
        MalformedSlot,
        UnknownFormat,
        UnsupportedVersion,
        InvalidMetadata,
        WriteFailed,
        LoadFailed,
        DeleteFailed,
        EnumerateFailed,
    }

    public readonly struct SessionSaveSlotResult
    {
        public SessionSaveSlotFailure Failure { get; }
        public SessionLoadFailure SessionFailure { get; }
        public string Message { get; }
        public bool Succeeded => Failure == SessionSaveSlotFailure.None;

        internal SessionSaveSlotResult(SessionSaveSlotFailure failure, string message = null,
            SessionLoadFailure sessionFailure = SessionLoadFailure.None)
        {
            Failure = failure;
            Message = message;
            SessionFailure = sessionFailure;
        }
    }

    public readonly struct SessionSaveSlotListResult
    {
        public SessionSaveSlotFailure Failure { get; }
        public string Message { get; }
        public IReadOnlyList<SessionSaveSlotInfo> Slots { get; }
        public bool Succeeded => Failure == SessionSaveSlotFailure.None;

        internal SessionSaveSlotListResult(SessionSaveSlotFailure failure,
            IReadOnlyList<SessionSaveSlotInfo> slots, string message = null)
        {
            Failure = failure;
            Slots = slots;
            Message = message;
        }
    }
}
