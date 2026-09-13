using System;
using System.Collections.Generic;
using Arcanum.Formats.Quest;

namespace Arcanum.Runtime.Campaign
{
    /// <summary>One read-only quest-logbook row projected from authoritative campaign state.</summary>
    public readonly struct QuestJournalEntry
    {
        public int QuestId { get; }
        public QuestState State { get; }
        public string StateLabel { get; }
        public string Description { get; }
        public QuestTimestamp Timestamp { get; }

        internal QuestJournalEntry(int questId, QuestState state, string description, QuestTimestamp timestamp)
        {
            QuestId = questId;
            State = state;
            StateLabel = QuestLog.Label(state);
            Description = description;
            Timestamp = timestamp;
        }
    }

    /// <summary>
    /// Presentation-independent logbook projection. Quest state and timestamps remain owned only by
    /// <see cref="CampaignStateService"/>; source prose remains owned only by <see cref="QuestLog"/>.
    /// </summary>
    public sealed class JournalProjectionService
    {
        private readonly CampaignStateService _campaign;
        private QuestLog _source;

        public bool HasSource => _source != null;

        public JournalProjectionService(CampaignStateService campaign)
            => _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));

        public void BindSource(QuestLog source) => _source = source ?? throw new ArgumentNullException(nameof(source));

        public bool TryProject(int questId, bool lowIntelligence, out QuestJournalEntry entry)
        {
            entry = default;
            if (_source == null) return false;
            QuestState state = (QuestState)_campaign.GetPcQuestState(questId);
            if (state == QuestState.Unknown) return false;
            string description = _source.Description(questId, lowIntelligence);
            if (string.IsNullOrEmpty(description)) return false;
            entry = new QuestJournalEntry(questId, state, description,
                _campaign.GetPcQuestTimestamp(questId));
            return true;
        }

        public IReadOnlyList<QuestJournalEntry> ProjectAll(bool lowIntelligence)
        {
            var entries = new List<QuestJournalEntry>();
            if (_source == null) return entries;
            foreach (int questId in _source.Numbers)
                if (TryProject(questId, lowIntelligence, out QuestJournalEntry entry)) entries.Add(entry);
            entries.Sort((left, right) =>
            {
                int timestamp = left.Timestamp.CompareTo(right.Timestamp);
                return timestamp != 0 ? timestamp : left.QuestId.CompareTo(right.QuestId);
            });
            return entries;
        }
    }
}
