using System;
using System.Collections.Generic;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Quest;
using Arcanum.Formats.World;
using Arcanum.Script;
using Arcanum.Runtime.Save;

namespace Arcanum.Runtime.Campaign
{
    public enum CampaignStateFailure
    {
        None,
        InvalidGlobalVariable,
        InvalidGlobalFlag,
        InvalidPcVariable,
        InvalidPcFlag,
        InvalidQuest,
        InvalidQuestState,
        QuestAlreadyTerminal,
        QuestCannotRegress,
        InvalidScriptAttachment,
        InvalidLocalFlag,
        InvalidLocalCounter,
        AreaSourceUnavailable,
        InvalidArea,
    }

    public readonly struct ScriptAttachmentState
    {
        public uint Flags { get; }
        public uint Counters { get; }

        public ScriptAttachmentState(uint flags, uint counters)
        {
            Flags = flags;
            Counters = counters;
        }
    }

    /// <summary>Source-shaped quest game time. The runtime clock is currently a deterministic session clock.</summary>
    public readonly struct QuestTimestamp : IEquatable<QuestTimestamp>, IComparable<QuestTimestamp>
    {
        public uint Days { get; }
        public uint Milliseconds { get; }
        public ulong Value => ((ulong)Milliseconds << 32) | Days;

        public QuestTimestamp(uint days, uint milliseconds)
        {
            Days = days;
            Milliseconds = milliseconds;
        }

        public int CompareTo(QuestTimestamp other)
        {
            int days = Days.CompareTo(other.Days);
            return days != 0 ? days : Milliseconds.CompareTo(other.Milliseconds);
        }

        public bool Equals(QuestTimestamp other) => Days == other.Days && Milliseconds == other.Milliseconds;
        public override bool Equals(object obj) => obj is QuestTimestamp other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Days, Milliseconds);
        public static bool operator ==(QuestTimestamp left, QuestTimestamp right) => left.Equals(right);
        public static bool operator !=(QuestTimestamp left, QuestTimestamp right) => !left.Equals(right);
    }

    /// <summary>
    /// Session-owned, presentation-independent campaign state. Array sizes and quest behavior mirror
    /// script.c/quest.c; save-file serialization is intentionally outside M5A.
    /// </summary>
    public sealed class CampaignStateService : IScriptGlobals
    {
        public const int GlobalVariableCount = 2000;
        public const int GlobalFlagCount = 3200;
        public const int PcVariableCount = 2000;
        public const int PcFlagCount = 3200;
        public const int FirstQuestNumber = 1000;
        public const int QuestCount = 1000;

        private readonly int[] _globalVariables = new int[GlobalVariableCount];
        private readonly uint[] _globalFlags = new uint[GlobalFlagCount / 32];
        private readonly int[] _pcVariables = new int[PcVariableCount];
        private readonly uint[] _pcFlags = new uint[PcFlagCount / 32];
        private readonly int[] _pcQuestStates = new int[QuestCount];
        private readonly QuestState[] _globalQuestStates = new QuestState[QuestCount];
        private readonly QuestTimestamp[] _pcQuestTimestamps = new QuestTimestamp[QuestCount];
        private readonly Dictionary<ScriptAttachmentKey, ScriptAttachmentState> _attachments = new();
        private readonly HashSet<AreaId> _knownAreas = new();
        private AreaList _areaSource;
        private int _storyState;
        private ulong _questClock;

        public event Action<int, QuestState, QuestState> PcQuestStateChanged;
        public event Action<AreaId> AreaDiscovered;

        public CampaignStateService()
        {
            for (int index = 0; index < _globalQuestStates.Length; index++)
                _globalQuestStates[index] = QuestState.Accepted;
        }

        /// <summary>Binds immutable retail area metadata without transferring campaign-state ownership.</summary>
        public void BindAreaSource(AreaList source)
        {
            _areaSource = source ?? throw new ArgumentNullException(nameof(source));
            foreach (AreaId id in _knownAreas) ValidateArea(id);
        }

        public bool IsAreaKnown(AreaId id)
        {
            ValidateArea(id);
            return _knownAreas.Contains(id);
        }

        /// <summary>Read-only policy boundary for a future world-map destination presenter.</summary>
        public bool CanSelectWorldArea(AreaId id) => IsAreaKnown(id);

        public IReadOnlyList<AreaId> KnownAreas
        {
            get
            {
                var result = new List<AreaId>(_knownAreas);
                result.Sort();
                return result;
            }
        }

        public bool TryDiscoverArea(AreaId id, out bool changed, out CampaignStateFailure failure)
        {
            changed = false;
            try
            {
                ValidateArea(id);
            }
            catch (CampaignStateException ex)
            {
                failure = ex.Failure;
                return false;
            }
            if (!_knownAreas.Add(id))
            {
                failure = CampaignStateFailure.None;
                return true;
            }
            changed = true;
            failure = CampaignStateFailure.None;
            AreaDiscovered?.Invoke(id);
            return true;
        }

        public void DiscoverArea(AreaId id)
        {
            if (!TryDiscoverArea(id, out _, out CampaignStateFailure failure))
                throw Failure(failure, id.Value);
        }

        public int GetVar(int index) => _globalVariables[ValidateIndex(index, GlobalVariableCount,
            CampaignStateFailure.InvalidGlobalVariable)];

        public void SetVar(int index, int value) => _globalVariables[ValidateIndex(index, GlobalVariableCount,
            CampaignStateFailure.InvalidGlobalVariable)] = value;

        public int GetFlag(int index) => GetBit(_globalFlags, ValidateIndex(index, GlobalFlagCount,
            CampaignStateFailure.InvalidGlobalFlag));

        public void SetFlag(int index, int value) => SetBit(_globalFlags, ValidateIndex(index, GlobalFlagCount,
            CampaignStateFailure.InvalidGlobalFlag), value);

        public int GetPcVar(int index) => _pcVariables[ValidateIndex(index, PcVariableCount,
            CampaignStateFailure.InvalidPcVariable)];

        public void SetPcVar(int index, int value) => _pcVariables[ValidateIndex(index, PcVariableCount,
            CampaignStateFailure.InvalidPcVariable)] = value;

        public int GetPcFlag(int index) => GetBit(_pcFlags, ValidateIndex(index, PcFlagCount,
            CampaignStateFailure.InvalidPcFlag));

        public void SetPcFlag(int index, int value) => SetBit(_pcFlags, ValidateIndex(index, PcFlagCount,
            CampaignStateFailure.InvalidPcFlag), value);

        public int StoryState => _storyState;

        public void SetStoryState(int value)
        {
            if (value > _storyState) _storyState = value;
        }

        public int GetGlobalQuestState(int quest)
            => (int)_globalQuestStates[QuestIndex(quest)];

        public void SetGlobalQuestState(int quest, int state)
        {
            int index = QuestIndex(quest);
            QuestState requested = ValidateQuestState(state);
            QuestState current = _globalQuestStates[index];
            if (current is QuestState.Completed or QuestState.Botched)
            {
                if (requested != current) throw Failure(CampaignStateFailure.QuestAlreadyTerminal, quest);
                return;
            }
            _globalQuestStates[index] = requested is QuestState.Completed or QuestState.Botched
                ? requested : QuestState.Accepted;
        }

        public int GetPcQuestState(int quest)
        {
            int raw = _pcQuestStates[QuestIndex(quest)];
            return QuestLog.IsBotched(raw) ? (int)QuestState.Botched : (int)QuestLog.StripBotched(raw);
        }

        public int GetRawPcQuestState(int quest) => _pcQuestStates[QuestIndex(quest)];

        public QuestTimestamp GetPcQuestTimestamp(int quest) => _pcQuestTimestamps[QuestIndex(quest)];

        public void SetPcQuestState(int quest, int state)
        {
            if (!TryAdvancePcQuest(quest, state, out _, out CampaignStateFailure failure))
                throw Failure(failure, quest);
        }

        public bool TryAdvancePcQuest(int quest, int requestedState, out QuestState effective,
            out CampaignStateFailure failure)
        {
            if (!TryPreviewPcQuestTransition(quest, requestedState, out effective, out failure, out bool changes))
                return false;
            if (!changes) return true;

            int index = QuestIndex(quest);
            QuestState old = (QuestState)GetPcQuestState(quest);
            if (_globalQuestStates[index] == QuestState.Accepted)
            {
                if (effective is QuestState.Completed or QuestState.OtherCompleted)
                    _globalQuestStates[index] = QuestState.Completed;
                else if (effective == QuestState.Botched)
                    _globalQuestStates[index] = QuestState.Botched;
            }

            int previousRaw = _pcQuestStates[index];
            _pcQuestStates[index] = effective == QuestState.Botched
                ? QuestLog.WithBotched(previousRaw)
                : (int)effective;
            _pcQuestTimestamps[index] = NextQuestTimestamp();
            PcQuestStateChanged?.Invoke(quest, old, effective);
            failure = CampaignStateFailure.None;
            return true;
        }

        public bool TryPreviewPcQuestTransition(int quest, int requestedState, out QuestState effective,
            out CampaignStateFailure failure, out bool changes)
        {
            int index;
            QuestState requested;
            try
            {
                index = QuestIndex(quest);
                requested = ValidateQuestState(requestedState);
            }
            catch (CampaignStateException ex)
            {
                effective = QuestState.Unknown;
                failure = ex.Failure;
                changes = false;
                return false;
            }

            QuestState old = (QuestState)GetPcQuestState(quest);
            effective = old;
            changes = false;
            if (old is QuestState.Completed or QuestState.OtherCompleted or QuestState.Botched)
            {
                if (requested == old) { failure = CampaignStateFailure.None; return true; }
                failure = CampaignStateFailure.QuestAlreadyTerminal;
                return false;
            }
            if (requested < old)
            {
                failure = CampaignStateFailure.QuestCannotRegress;
                return false;
            }
            if (requested == old)
            {
                failure = CampaignStateFailure.None;
                return true;
            }

            QuestState global = _globalQuestStates[index];
            effective = global == QuestState.Accepted ? requested
                : global == QuestState.Completed ? QuestState.OtherCompleted : QuestState.Botched;
            changes = true;
            failure = CampaignStateFailure.None;
            return true;
        }

        public ScriptAttachmentState GetScriptAttachment(ArcanumObjectId identity, int attachmentPoint)
        {
            ValidateAttachment(identity, attachmentPoint);
            return _attachments.TryGetValue(new ScriptAttachmentKey(identity, attachmentPoint), out var state)
                ? state : default;
        }

        public void SetScriptAttachment(ArcanumObjectId identity, int attachmentPoint, uint flags, uint counters)
        {
            ValidateAttachment(identity, attachmentPoint);
            _attachments[new ScriptAttachmentKey(identity, attachmentPoint)] = new ScriptAttachmentState(flags, counters);
        }

        public int GetLocalFlag(ArcanumObjectId identity, int attachmentPoint, int index)
        {
            if ((uint)index >= 32u) throw Failure(CampaignStateFailure.InvalidLocalFlag, index);
            return (int)((GetScriptAttachment(identity, attachmentPoint).Flags >> index) & 1u);
        }

        public void SetLocalFlag(ArcanumObjectId identity, int attachmentPoint, int index, int value)
        {
            if ((uint)index >= 32u) throw Failure(CampaignStateFailure.InvalidLocalFlag, index);
            ScriptAttachmentState state = GetScriptAttachment(identity, attachmentPoint);
            uint bit = 1u << index;
            uint flags = (value & 1) != 0 ? state.Flags | bit : state.Flags & ~bit;
            SetScriptAttachment(identity, attachmentPoint, flags, state.Counters);
        }

        public int GetLocalCounter(ArcanumObjectId identity, int attachmentPoint, int index)
        {
            if ((uint)index >= 4u) throw Failure(CampaignStateFailure.InvalidLocalCounter, index);
            return (int)((GetScriptAttachment(identity, attachmentPoint).Counters >> (index * 8)) & 0xFFu);
        }

        public void SetLocalCounter(ArcanumObjectId identity, int attachmentPoint, int index, int value)
        {
            if ((uint)index >= 4u || (uint)value > byte.MaxValue)
                throw Failure(CampaignStateFailure.InvalidLocalCounter, index);
            ScriptAttachmentState state = GetScriptAttachment(identity, attachmentPoint);
            int shift = index * 8;
            uint mask = 0xFFu << shift;
            uint counters = (state.Counters & ~mask) | ((uint)value << shift);
            SetScriptAttachment(identity, attachmentPoint, state.Flags, counters);
        }

        internal CampaignSaveData ExportSaveData()
        {
            var data = new CampaignSaveData
            {
                StoryState = _storyState,
                QuestClock = _questClock,
                GlobalVariables = new List<IndexedIntSaveData>(),
                GlobalFlags = new List<int>(),
                PcVariables = new List<IndexedIntSaveData>(),
                PcFlags = new List<int>(),
                Quests = new List<QuestSaveData>(),
                Attachments = new List<ScriptAttachmentSaveData>(),
                KnownAreas = new List<int>(),
            };
            for (int index = 0; index < GlobalVariableCount; index++)
                if (_globalVariables[index] != 0)
                    data.GlobalVariables.Add(new IndexedIntSaveData { Index = index, Value = _globalVariables[index] });
            for (int index = 0; index < GlobalFlagCount; index++)
                if (GetBit(_globalFlags, index) != 0) data.GlobalFlags.Add(index);
            for (int index = 0; index < PcVariableCount; index++)
                if (_pcVariables[index] != 0)
                    data.PcVariables.Add(new IndexedIntSaveData { Index = index, Value = _pcVariables[index] });
            for (int index = 0; index < PcFlagCount; index++)
                if (GetBit(_pcFlags, index) != 0) data.PcFlags.Add(index);
            for (int index = 0; index < QuestCount; index++)
            {
                QuestTimestamp timestamp = _pcQuestTimestamps[index];
                if (_pcQuestStates[index] == 0 && _globalQuestStates[index] == QuestState.Accepted
                    && timestamp == default) continue;
                data.Quests.Add(new QuestSaveData
                {
                    Number = FirstQuestNumber + index,
                    PcState = _pcQuestStates[index],
                    GlobalState = (int)_globalQuestStates[index],
                    TimestampDays = timestamp.Days,
                    TimestampMilliseconds = timestamp.Milliseconds,
                });
            }
            foreach (var pair in _attachments)
                data.Attachments.Add(new ScriptAttachmentSaveData
                {
                    Identity = pair.Key.Identity.Key,
                    AttachmentPoint = pair.Key.AttachmentPoint,
                    Flags = pair.Value.Flags,
                    Counters = pair.Value.Counters,
                });
            data.Attachments.Sort((left, right) =>
            {
                int identity = string.CompareOrdinal(left.Identity, right.Identity);
                return identity != 0 ? identity : left.AttachmentPoint.CompareTo(right.AttachmentPoint);
            });
            foreach (AreaId id in KnownAreas) data.KnownAreas.Add(id.Value);
            return data;
        }

        internal void RestoreSaveData(CampaignSaveData data)
        {
            _storyState = data.StoryState;
            _questClock = data.QuestClock;
            foreach (int value in data.KnownAreas) DiscoverArea(new AreaId(value));
            foreach (IndexedIntSaveData entry in data.GlobalVariables) _globalVariables[entry.Index] = entry.Value;
            foreach (int index in data.GlobalFlags) SetBit(_globalFlags, index, 1);
            foreach (IndexedIntSaveData entry in data.PcVariables) _pcVariables[entry.Index] = entry.Value;
            foreach (int index in data.PcFlags) SetBit(_pcFlags, index, 1);
            foreach (QuestSaveData quest in data.Quests)
            {
                int index = quest.Number - FirstQuestNumber;
                _pcQuestStates[index] = quest.PcState;
                _globalQuestStates[index] = (QuestState)quest.GlobalState;
                _pcQuestTimestamps[index] = new QuestTimestamp(quest.TimestampDays, quest.TimestampMilliseconds);
            }
            foreach (ScriptAttachmentSaveData attachment in data.Attachments)
            {
                ArcanumObjectId.TryParsePersistent(attachment.Identity, out ArcanumObjectId identity);
                _attachments.Add(new ScriptAttachmentKey(identity, attachment.AttachmentPoint),
                    new ScriptAttachmentState(attachment.Flags, attachment.Counters));
            }
        }

        internal Snapshot CaptureSnapshot() => new(this);
        internal void RestoreSnapshot(Snapshot snapshot) => snapshot.Restore(this);

        internal sealed class Snapshot
        {
            private readonly int[] _globalVariables;
            private readonly uint[] _globalFlags;
            private readonly int[] _pcVariables;
            private readonly uint[] _pcFlags;
            private readonly int[] _pcQuestStates;
            private readonly QuestState[] _globalQuestStates;
            private readonly QuestTimestamp[] _pcQuestTimestamps;
            private readonly Dictionary<ScriptAttachmentKey, ScriptAttachmentState> _attachments;
            private readonly HashSet<AreaId> _knownAreas;
            private readonly int _storyState;
            private readonly ulong _questClock;

            internal Snapshot(CampaignStateService state)
            {
                _globalVariables = (int[])state._globalVariables.Clone();
                _globalFlags = (uint[])state._globalFlags.Clone();
                _pcVariables = (int[])state._pcVariables.Clone();
                _pcFlags = (uint[])state._pcFlags.Clone();
                _pcQuestStates = (int[])state._pcQuestStates.Clone();
                _globalQuestStates = (QuestState[])state._globalQuestStates.Clone();
                _pcQuestTimestamps = (QuestTimestamp[])state._pcQuestTimestamps.Clone();
                _attachments = new Dictionary<ScriptAttachmentKey, ScriptAttachmentState>(state._attachments);
                _knownAreas = new HashSet<AreaId>(state._knownAreas);
                _storyState = state._storyState;
                _questClock = state._questClock;
            }

            internal void Restore(CampaignStateService state)
            {
                Array.Copy(_globalVariables, state._globalVariables, _globalVariables.Length);
                Array.Copy(_globalFlags, state._globalFlags, _globalFlags.Length);
                Array.Copy(_pcVariables, state._pcVariables, _pcVariables.Length);
                Array.Copy(_pcFlags, state._pcFlags, _pcFlags.Length);
                Array.Copy(_pcQuestStates, state._pcQuestStates, _pcQuestStates.Length);
                Array.Copy(_globalQuestStates, state._globalQuestStates, _globalQuestStates.Length);
                Array.Copy(_pcQuestTimestamps, state._pcQuestTimestamps, _pcQuestTimestamps.Length);
                state._attachments.Clear();
                foreach (var pair in _attachments) state._attachments.Add(pair.Key, pair.Value);
                state._knownAreas.Clear();
                foreach (AreaId id in _knownAreas) state._knownAreas.Add(id);
                state._storyState = _storyState;
                state._questClock = _questClock;
            }
        }

        private QuestTimestamp NextQuestTimestamp()
        {
            _questClock++;
            return new QuestTimestamp((uint)(_questClock / 86400000UL), (uint)(_questClock % 86400000UL));
        }

        private void ValidateArea(AreaId id)
        {
            if (_areaSource == null)
                throw Failure(CampaignStateFailure.AreaSourceUnavailable, id.Value);
            if (id.Value <= 0 || !_areaSource.TryGet(id, out _))
                throw Failure(CampaignStateFailure.InvalidArea, id.Value);
        }

        private readonly struct ScriptAttachmentKey : IEquatable<ScriptAttachmentKey>
        {
            private readonly ArcanumObjectId _identity;
            private readonly int _attachmentPoint;

            public ScriptAttachmentKey(ArcanumObjectId identity, int attachmentPoint)
            {
                _identity = identity;
                _attachmentPoint = attachmentPoint;
            }

            public ArcanumObjectId Identity => _identity;
            public int AttachmentPoint => _attachmentPoint;

            public bool Equals(ScriptAttachmentKey other)
                => _identity == other._identity && _attachmentPoint == other._attachmentPoint;
            public override bool Equals(object obj) => obj is ScriptAttachmentKey other && Equals(other);
            public override int GetHashCode() => (_identity.GetHashCode() * 397) ^ _attachmentPoint;
        }

        public sealed class CampaignStateException : InvalidOperationException
        {
            public CampaignStateFailure Failure { get; }
            internal CampaignStateException(CampaignStateFailure failure, int value)
                : base($"Campaign state rejected {failure} ({value}).") => Failure = failure;
        }

        private static int ValidateIndex(int index, int count, CampaignStateFailure failure)
        {
            if ((uint)index >= (uint)count) throw Failure(failure, index);
            return index;
        }

        private static int QuestIndex(int quest)
        {
            if ((uint)(quest - FirstQuestNumber) >= QuestCount)
                throw Failure(CampaignStateFailure.InvalidQuest, quest);
            return quest - FirstQuestNumber;
        }

        private static QuestState ValidateQuestState(int value)
        {
            if ((uint)value > (uint)QuestState.Botched)
                throw Failure(CampaignStateFailure.InvalidQuestState, value);
            return (QuestState)value;
        }

        private static void ValidateAttachment(ArcanumObjectId identity, int attachmentPoint)
        {
            if (!identity.IsPersistent || (uint)attachmentPoint > (uint)Arcanum.Formats.Script.Sap.CriticalMiss)
                throw Failure(CampaignStateFailure.InvalidScriptAttachment, attachmentPoint);
        }

        private static int GetBit(uint[] words, int index)
            => (int)((words[index >> 5] >> (index & 31)) & 1u);

        private static void SetBit(uint[] words, int index, int value)
        {
            uint bit = 1u << (index & 31);
            if ((value & 1) != 0) words[index >> 5] |= bit;
            else words[index >> 5] &= ~bit;
        }

        private static CampaignStateException Failure(CampaignStateFailure failure, int value)
            => new(failure, value);
    }
}
