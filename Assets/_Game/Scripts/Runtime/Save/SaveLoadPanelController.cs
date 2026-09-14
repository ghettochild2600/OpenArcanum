using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Arcanum.Runtime.Save
{
    public enum SaveLoadPanelMode { Save, Load }
    public enum SaveLoadConfirmation { None, Overwrite, Delete }

    /// <summary>Immutable display projection of slot-domain metadata.</summary>
    public sealed class SaveLoadSlotView
    {
        public string SlotId { get; }
        public bool IsValid { get; }
        public string Timestamp { get; }
        public string Location { get; }
        public string Level { get; }
        public string Version { get; }
        public string Error { get; }

        internal SaveLoadSlotView(SessionSaveSlotInfo info)
        {
            SlotId = info?.SlotId ?? string.Empty;
            IsValid = info?.IsValid == true;
            SessionSaveSlotMetadata metadata = info?.Metadata;
            Timestamp = FormatTimestamp(metadata?.SavedAtUtc);
            Location = !string.IsNullOrWhiteSpace(metadata?.SelectedSector)
                ? metadata.SelectedSector
                : metadata?.CurrentMap ?? "Unknown location";
            Level = metadata == null ? "—" : metadata.PcLevel.ToString(CultureInfo.InvariantCulture);
            Version = metadata == null
                ? "—"
                : $"slot 1 / session {metadata.SessionVersion}";
            Error = IsValid ? null : info?.Error ?? "This slot is not readable.";
        }

        private static string FormatTimestamp(string value)
            => DateTime.TryParseExact(value, "O", CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out DateTime timestamp)
                ? timestamp.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)
                : "Unknown time";
    }

    /// <summary>
    /// Presentation state and player intent only. All filesystem and gameplay work remains in the slot/session services.
    /// </summary>
    public sealed class SaveLoadPanelController
    {
        private readonly ISessionSaveSlotOperations _operations;
        private readonly List<SaveLoadSlotView> _slots = new();
        private string _pendingSlotId;

        public bool IsOpen { get; private set; }
        public SaveLoadPanelMode Mode { get; private set; }
        public IReadOnlyList<SaveLoadSlotView> Slots => _slots;
        public string SelectedSlotId { get; private set; }
        public SaveLoadConfirmation Confirmation { get; private set; }
        public string StatusMessage { get; private set; }
        public string ErrorMessage { get; private set; }
        public string SuggestedSlotId { get; private set; } = "manual01";
        public string ConfirmationMessage => Confirmation switch
        {
            SaveLoadConfirmation.Overwrite => $"Overwrite '{_pendingSlotId}'?",
            SaveLoadConfirmation.Delete => $"Delete '{_pendingSlotId}' permanently?",
            _ => string.Empty,
        };

        public SaveLoadPanelController(ISessionSaveSlotOperations operations)
            => _operations = operations ?? throw new ArgumentNullException(nameof(operations));

        public void Open(SaveLoadPanelMode mode)
        {
            IsOpen = true;
            Mode = mode;
            Confirmation = SaveLoadConfirmation.None;
            _pendingSlotId = null;
            StatusMessage = null;
            ErrorMessage = null;
            SelectedSlotId = null;
            Refresh();
            if (mode == SaveLoadPanelMode.Load) SelectDefaultLoadSlot();
        }

        public void Close()
        {
            IsOpen = false;
            Confirmation = SaveLoadConfirmation.None;
            _pendingSlotId = null;
        }

        public void SetMode(SaveLoadPanelMode mode)
        {
            if (Mode == mode) return;
            Mode = mode;
            Confirmation = SaveLoadConfirmation.None;
            _pendingSlotId = null;
            StatusMessage = null;
            ErrorMessage = null;
            SelectedSlotId = null;
            if (mode == SaveLoadPanelMode.Load) SelectDefaultLoadSlot();
        }

        public bool SelectSlot(string slotId)
        {
            if (_slots.All(slot => !string.Equals(slot.SlotId, slotId, StringComparison.Ordinal))) return false;
            SelectedSlotId = slotId;
            Confirmation = SaveLoadConfirmation.None;
            _pendingSlotId = null;
            StatusMessage = null;
            ErrorMessage = null;
            return true;
        }

        public void SelectNewSlot()
        {
            SelectedSlotId = null;
            Confirmation = SaveLoadConfirmation.None;
            _pendingSlotId = null;
            StatusMessage = null;
            ErrorMessage = null;
        }

        public void Refresh()
        {
            SessionSaveSlotListResult result = _operations.ListSlots();
            if (!result.Succeeded)
            {
                _slots.Clear();
                SelectedSlotId = null;
                ErrorMessage = Describe(result.Failure, result.Message);
                SuggestedSlotId = "manual01";
                return;
            }

            string selected = SelectedSlotId;
            _slots.Clear();
            foreach (SessionSaveSlotInfo info in result.Slots ?? Array.Empty<SessionSaveSlotInfo>())
                _slots.Add(new SaveLoadSlotView(info));
            SelectedSlotId = _slots.Any(slot => slot.SlotId == selected) ? selected : null;
            SuggestedSlotId = NextManualSlotId(_slots.Select(slot => slot.SlotId));
        }

        public void RequestSave()
        {
            ClearMessages();
            string slotId = SelectedSlotId ?? SuggestedSlotId;
            if (_slots.Any(slot => slot.SlotId == slotId))
            {
                Confirmation = SaveLoadConfirmation.Overwrite;
                _pendingSlotId = slotId;
                return;
            }
            ExecuteSave(slotId);
        }

        public void RequestLoad()
        {
            ClearMessages();
            if (string.IsNullOrEmpty(SelectedSlotId))
            {
                ErrorMessage = "Select a save slot to load.";
                return;
            }
            SessionSaveSlotResult result = _operations.LoadSlot(SelectedSlotId);
            if (result.Succeeded)
            {
                StatusMessage = $"Loaded '{SelectedSlotId}'.";
                Close();
                return;
            }
            ErrorMessage = Describe(result);
        }

        public void RequestDelete()
        {
            ClearMessages();
            if (string.IsNullOrEmpty(SelectedSlotId))
            {
                ErrorMessage = "Select a save slot to delete.";
                return;
            }
            Confirmation = SaveLoadConfirmation.Delete;
            _pendingSlotId = SelectedSlotId;
        }

        public void CancelConfirmation()
        {
            Confirmation = SaveLoadConfirmation.None;
            _pendingSlotId = null;
            StatusMessage = "Cancelled. No save data was changed.";
            ErrorMessage = null;
        }

        public void Confirm()
        {
            SaveLoadConfirmation action = Confirmation;
            string slotId = _pendingSlotId;
            Confirmation = SaveLoadConfirmation.None;
            _pendingSlotId = null;
            if (action == SaveLoadConfirmation.Overwrite) ExecuteSave(slotId);
            else if (action == SaveLoadConfirmation.Delete) ExecuteDelete(slotId);
        }

        private void ExecuteSave(string slotId)
        {
            SessionSaveSlotResult result = _operations.SaveSlot(slotId);
            if (!result.Succeeded)
            {
                ErrorMessage = Describe(result);
                return;
            }
            Refresh();
            SelectedSlotId = slotId;
            StatusMessage = $"Saved '{slotId}'.";
            ErrorMessage = null;
        }

        private void ExecuteDelete(string slotId)
        {
            SessionSaveSlotResult result = _operations.DeleteSlot(slotId);
            if (!result.Succeeded)
            {
                ErrorMessage = Describe(result);
                return;
            }
            SelectedSlotId = null;
            Refresh();
            if (Mode == SaveLoadPanelMode.Load) SelectDefaultLoadSlot();
            StatusMessage = $"Deleted '{slotId}'.";
            ErrorMessage = null;
        }

        private void SelectDefaultLoadSlot()
        {
            SaveLoadSlotView first = _slots.FirstOrDefault(slot => slot.IsValid) ?? _slots.FirstOrDefault();
            SelectedSlotId = first?.SlotId;
        }

        private void ClearMessages()
        {
            StatusMessage = null;
            ErrorMessage = null;
        }

        private static string NextManualSlotId(IEnumerable<string> existing)
        {
            var used = new HashSet<string>(existing ?? Array.Empty<string>(), StringComparer.Ordinal);
            for (int number = 1; number <= 999; number++)
            {
                string candidate = "manual" + number.ToString("00", CultureInfo.InvariantCulture);
                if (!used.Contains(candidate)) return candidate;
            }
            return "manual1000";
        }

        public static string Describe(SessionSaveSlotResult result)
        {
            if (result.Failure == SessionSaveSlotFailure.LoadFailed)
            {
                return result.SessionFailure switch
                {
                    SessionLoadFailure.MalformedJson => "The embedded save data is malformed.",
                    SessionLoadFailure.UnknownFormat => "The embedded save uses an unknown format.",
                    SessionLoadFailure.UnsupportedVersion => "The embedded save is from an unsupported version.",
                    SessionLoadFailure.InvalidIdentity or SessionLoadFailure.DuplicateIdentity
                        => "The save contains an invalid or duplicate object identity.",
                    SessionLoadFailure.InvalidReference or SessionLoadFailure.ContainmentCycle
                        => "The save contains an invalid object reference.",
                    _ => string.IsNullOrWhiteSpace(result.Message)
                        ? "The authoritative session could not be restored."
                        : result.Message,
                };
            }
            return Describe(result.Failure, result.Message);
        }

        private static string Describe(SessionSaveSlotFailure failure, string detail)
            => failure switch
            {
                SessionSaveSlotFailure.None => string.Empty,
                SessionSaveSlotFailure.InvalidSlotId => "The save-slot name is not valid.",
                SessionSaveSlotFailure.NoActiveSession => "There is no active game to save.",
                SessionSaveSlotFailure.MissingSlot => "The selected save slot no longer exists.",
                SessionSaveSlotFailure.ReadFailed => "The selected save slot could not be read.",
                SessionSaveSlotFailure.MalformedSlot => "The selected save slot is damaged or malformed.",
                SessionSaveSlotFailure.UnknownFormat => "The selected file is not an OpenArcanum save slot.",
                SessionSaveSlotFailure.UnsupportedVersion => "The selected save slot is from an unsupported version.",
                SessionSaveSlotFailure.InvalidMetadata => "The save-slot metadata is invalid or does not match its save.",
                SessionSaveSlotFailure.WriteFailed => "The save slot could not be written.",
                SessionSaveSlotFailure.DeleteFailed => "The save slot could not be deleted.",
                SessionSaveSlotFailure.EnumerateFailed => "The save-slot list could not be read.",
                _ => string.IsNullOrWhiteSpace(detail) ? "The save/load operation failed." : detail,
            };
    }
}
