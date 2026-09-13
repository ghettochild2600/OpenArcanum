using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Arcanum.Runtime.World;
using Newtonsoft.Json;

namespace Arcanum.Runtime.Save
{
    /// <summary>Owns safe domain slot naming, metadata, enumeration, and single-file atomic lifecycle.</summary>
    public sealed class SessionSaveSlotService
    {
        public const string SlotFormatIdentifier = "OpenArcanum.SaveSlot";
        public const int CurrentSlotVersion = 1;
        public const string SlotFileExtension = ".oaslot";
        public const int MaximumSlotIdLength = 32;

        private readonly WorldMapSessionCoordinator _session;
        private readonly SessionSaveService _saveGames;
        private readonly string _saveDirectory;
        private readonly Func<DateTime> _utcNow;

        public string SaveDirectory => _saveDirectory;

        public SessionSaveSlotService(WorldMapSessionCoordinator session)
            : this(session, Path.Combine(UnityEngine.Application.persistentDataPath, "Saves"),
                () => DateTime.UtcNow)
        {
        }

        public SessionSaveSlotService(WorldMapSessionCoordinator session, string saveDirectory)
            : this(session, saveDirectory, () => DateTime.UtcNow)
        {
        }

        internal SessionSaveSlotService(WorldMapSessionCoordinator session, string saveDirectory,
            Func<DateTime> utcNow)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _saveGames = session.SaveGames;
            if (string.IsNullOrWhiteSpace(saveDirectory))
                throw new ArgumentException("A save directory is required.", nameof(saveDirectory));
            _saveDirectory = Path.GetFullPath(saveDirectory);
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
        }

        public SessionSaveSlotResult SaveSlot(string slotId)
        {
            if (!TryResolveSlotPath(slotId, out string path)) return InvalidSlot(slotId);
            if (_session.PlayerState == null || !_session.HasSelectedSector)
                return Failure(SessionSaveSlotFailure.NoActiveSession, "An active production session is required.");

            try
            {
                Directory.CreateDirectory(_saveDirectory);
                CleanupStaleTemporaryFiles();
                SessionSaveData snapshot = _saveGames.CaptureData();
                var slot = new SessionSaveSlotData
                {
                    SlotFormat = SlotFormatIdentifier,
                    SlotVersion = CurrentSlotVersion,
                    Metadata = CreateMetadata(slotId, snapshot),
                    Session = snapshot,
                };
                string json = SerializeSlotData(slot);
                SessionSaveSlotResult validation = TryReadSlot(json, slotId, out _);
                if (!validation.Succeeded) return validation;
                return WriteAtomic(path, json);
            }
            catch (Exception ex)
            {
                return Failure(SessionSaveSlotFailure.WriteFailed, ex.Message);
            }
        }

        public SessionSaveSlotResult LoadSlot(string slotId)
        {
            if (!TryResolveSlotPath(slotId, out string path)) return InvalidSlot(slotId);
            if (!File.Exists(path)) return Failure(SessionSaveSlotFailure.MissingSlot, $"Slot '{slotId}' does not exist.");
            string json;
            try
            {
                json = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                return Failure(SessionSaveSlotFailure.ReadFailed, ex.Message);
            }

            SessionSaveSlotResult parsed = TryReadSlot(json, slotId, out SessionSaveSlotData slot);
            if (!parsed.Succeeded) return parsed;
            SessionLoadResult loaded = _saveGames.LoadJson(_saveGames.SerializeData(slot.Session));
            return loaded.Succeeded
                ? new SessionSaveSlotResult(SessionSaveSlotFailure.None)
                : new SessionSaveSlotResult(SessionSaveSlotFailure.LoadFailed, loaded.Message, loaded.Failure);
        }

        public SessionSaveSlotListResult ListSlots()
        {
            if (!Directory.Exists(_saveDirectory))
                return new SessionSaveSlotListResult(SessionSaveSlotFailure.None,
                    Array.Empty<SessionSaveSlotInfo>());
            try
            {
                CleanupStaleTemporaryFiles();
                var slots = new List<SessionSaveSlotInfo>();
                foreach (string path in Directory.EnumerateFiles(_saveDirectory, "*" + SlotFileExtension,
                             SearchOption.TopDirectoryOnly).OrderBy(value => value, StringComparer.Ordinal))
                {
                    string slotId = Path.GetFileNameWithoutExtension(path);
                    try
                    {
                        string json = File.ReadAllText(path, Encoding.UTF8);
                        SessionSaveSlotResult result = TryReadSlot(json, slotId, out SessionSaveSlotData slot);
                        slots.Add(new SessionSaveSlotInfo
                        {
                            SlotId = slotId,
                            IsValid = result.Succeeded,
                            Metadata = result.Succeeded ? slot.Metadata : null,
                            Error = result.Succeeded ? null : result.Message,
                        });
                    }
                    catch (Exception ex)
                    {
                        slots.Add(new SessionSaveSlotInfo
                            { SlotId = slotId, IsValid = false, Error = ex.Message });
                    }
                }
                return new SessionSaveSlotListResult(SessionSaveSlotFailure.None, slots);
            }
            catch (Exception ex)
            {
                return new SessionSaveSlotListResult(SessionSaveSlotFailure.EnumerateFailed,
                    Array.Empty<SessionSaveSlotInfo>(), ex.Message);
            }
        }

        public SessionSaveSlotResult DeleteSlot(string slotId)
        {
            if (!TryResolveSlotPath(slotId, out string path)) return InvalidSlot(slotId);
            if (!File.Exists(path)) return Failure(SessionSaveSlotFailure.MissingSlot, $"Slot '{slotId}' does not exist.");
            try
            {
                File.Delete(path);
                CleanupStaleTemporaryFiles();
                return new SessionSaveSlotResult(SessionSaveSlotFailure.None);
            }
            catch (Exception ex)
            {
                return Failure(SessionSaveSlotFailure.DeleteFailed, ex.Message);
            }
        }

        internal string ResolveSlotPathForTests(string slotId)
            => TryResolveSlotPath(slotId, out string path) ? path : null;

        internal string SerializeSlotData(SessionSaveSlotData slot)
            => JsonConvert.SerializeObject(slot, SessionSaveService.JsonSettings) + "\n";

        internal SessionSaveSlotResult TryReadSlot(string json, string expectedSlotId,
            out SessionSaveSlotData slot)
        {
            slot = null;
            try
            {
                slot = JsonConvert.DeserializeObject<SessionSaveSlotData>(json, SessionSaveService.JsonSettings);
            }
            catch (JsonException ex)
            {
                return Failure(SessionSaveSlotFailure.MalformedSlot, ex.Message);
            }
            if (slot == null) return Failure(SessionSaveSlotFailure.MalformedSlot, "The slot is empty.");
            if (!string.Equals(slot.SlotFormat, SlotFormatIdentifier, StringComparison.Ordinal))
                return Failure(SessionSaveSlotFailure.UnknownFormat, "Unknown save-slot format.");
            if (slot.SlotVersion != CurrentSlotVersion)
                return Failure(SessionSaveSlotFailure.UnsupportedVersion,
                    $"Save-slot version {slot.SlotVersion} is unsupported.");
            if (slot.Metadata == null || slot.Session == null)
                return Failure(SessionSaveSlotFailure.InvalidMetadata, "Slot metadata or session data is missing.");
            if (!ValidateMetadata(slot.Metadata, expectedSlotId, slot.Session, out string error))
                return Failure(SessionSaveSlotFailure.InvalidMetadata, error);

            SessionLoadResult sessionResult = _saveGames.TryBuildPlan(_saveGames.SerializeData(slot.Session), out _);
            return sessionResult.Succeeded
                ? new SessionSaveSlotResult(SessionSaveSlotFailure.None)
                : new SessionSaveSlotResult(SessionSaveSlotFailure.LoadFailed, sessionResult.Message,
                    sessionResult.Failure);
        }

        private SessionSaveSlotMetadata CreateMetadata(string slotId, SessionSaveData snapshot)
        {
            CharacterSaveData pc = snapshot.Characters.Single(value =>
                string.Equals(value.Identity, snapshot.World.Player.Identity, StringComparison.Ordinal));
            return new SessionSaveSlotMetadata
            {
                SlotId = slotId,
                SavedAtUtc = _utcNow().ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                SessionFormat = snapshot.Format,
                SessionVersion = snapshot.Version,
                CurrentMap = snapshot.World.CurrentMap,
                SelectedSector = snapshot.World.SelectedSector,
                PcIdentity = snapshot.World.Player.Identity,
                PcLevel = pc.Progression.Level,
            };
        }

        private static bool ValidateMetadata(SessionSaveSlotMetadata metadata, string expectedSlotId,
            SessionSaveData snapshot, out string error)
        {
            error = null;
            if (!string.Equals(metadata.SlotId, expectedSlotId, StringComparison.Ordinal)
                || !IsValidSlotId(metadata.SlotId))
            {
                error = "The slot ID does not match its file name.";
                return false;
            }
            if (!DateTime.TryParseExact(metadata.SavedAtUtc, "O", CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out DateTime savedAt) || savedAt.Kind != DateTimeKind.Utc)
            {
                error = "The save timestamp is not canonical UTC.";
                return false;
            }
            if (snapshot?.World?.Player == null || snapshot.Characters == null)
            {
                error = "The authoritative snapshot is missing world or character metadata inputs.";
                return false;
            }
            CharacterSaveData[] pcMatches = snapshot.Characters.Where(value =>
                    string.Equals(value?.Identity, snapshot.World.Player.Identity, StringComparison.Ordinal))
                .Take(2).ToArray();
            CharacterSaveData pc = pcMatches.Length == 1 ? pcMatches[0] : null;
            if (pc?.Progression == null
                || metadata.SessionFormat != snapshot.Format || metadata.SessionVersion != snapshot.Version
                || metadata.CurrentMap != snapshot.World.CurrentMap
                || metadata.SelectedSector != snapshot.World.SelectedSector
                || metadata.PcIdentity != snapshot.World.Player.Identity
                || metadata.PcLevel != pc.Progression.Level)
            {
                error = "Slot metadata does not match the authoritative snapshot.";
                return false;
            }
            return true;
        }

        private SessionSaveSlotResult WriteAtomic(string path, string json)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                           FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                string written = File.ReadAllText(temporary, Encoding.UTF8);
                string slotId = Path.GetFileNameWithoutExtension(path);
                SessionSaveSlotResult validation = TryReadSlot(written, slotId, out _);
                if (!validation.Succeeded) return validation;
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                return new SessionSaveSlotResult(SessionSaveSlotFailure.None);
            }
            catch (Exception ex)
            {
                return Failure(SessionSaveSlotFailure.WriteFailed, ex.Message);
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch { }
            }
        }

        private void CleanupStaleTemporaryFiles()
        {
            if (!Directory.Exists(_saveDirectory)) return;
            foreach (string path in Directory.EnumerateFiles(_saveDirectory,
                         "*" + SlotFileExtension + ".*.tmp", SearchOption.TopDirectoryOnly))
            {
                try { File.Delete(path); }
                catch { }
            }
        }

        private bool TryResolveSlotPath(string slotId, out string path)
        {
            path = null;
            if (!IsValidSlotId(slotId)) return false;
            string candidate = Path.GetFullPath(Path.Combine(_saveDirectory, slotId + SlotFileExtension));
            string prefix = _saveDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                            + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
            path = candidate;
            return true;
        }

        private static bool IsValidSlotId(string slotId)
        {
            if (string.IsNullOrEmpty(slotId) || slotId.Length > MaximumSlotIdLength) return false;
            for (int index = 0; index < slotId.Length; index++)
            {
                char value = slotId[index];
                if (value is >= 'a' and <= 'z' || value is >= '0' and <= '9'
                    || index > 0 && value is '_' or '-') continue;
                return false;
            }
            return true;
        }

        private static SessionSaveSlotResult InvalidSlot(string slotId)
            => Failure(SessionSaveSlotFailure.InvalidSlotId,
                $"Slot ID '{slotId}' must be 1-{MaximumSlotIdLength} lowercase ASCII letters/digits with internal '-' or '_'.");

        private static SessionSaveSlotResult Failure(SessionSaveSlotFailure failure, string message)
            => new(failure, message);
    }
}
