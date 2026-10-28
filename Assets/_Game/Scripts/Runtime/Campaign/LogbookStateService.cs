using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;

namespace Arcanum.Runtime.Campaign
{
    public enum LogbookInjuryType { Blinded, CrippledArm, CrippledLeg, Scarred }

    public readonly struct LogbookEffectEntry
    {
        public int Id { get; }
        public long Timestamp { get; }
        public bool IsBlessing { get; }
        public LogbookEffectEntry(int id, long timestamp, bool isBlessing)
        { Id = id; Timestamp = timestamp; IsBlessing = isBlessing; }
    }

    public readonly struct LogbookInjuryEntry
    {
        public int SourceDescriptionId { get; }
        public LogbookInjuryType Type { get; }
        public bool Active { get; }
        public LogbookInjuryEntry(int sourceDescriptionId, LogbookInjuryType type, bool active)
        { SourceDescriptionId = sourceDescriptionId; Type = type; Active = active; }
    }

    public sealed class LogbookKillStatistics
    {
        public int TotalKills { get; internal set; }
        public int MostPowerfulDescriptionId { get; internal set; }
        public int MostPowerfulLevel { get; internal set; }
        public int LeastPowerfulDescriptionId { get; internal set; }
        public int LeastPowerfulLevel { get; internal set; }
        public int MostGoodDescriptionId { get; internal set; }
        public int MostGoodValue { get; internal set; }
        public int MostEvilDescriptionId { get; internal set; }
        public int MostEvilValue { get; internal set; }
        public int MostMagicalDescriptionId { get; internal set; }
        public int MostMagicalValue { get; internal set; }
        public int MostTechnicalDescriptionId { get; internal set; }
        public int MostTechnicalValue { get; internal set; }
    }

    /// <summary>Source PC Logbook arrays, kept independently of presentation lifetime.</summary>
    public sealed class LogbookStateService
    {
        private readonly WorldMapSessionCoordinator _world;
        private readonly Dictionary<ArcanumObjectId, CharacterState> _characters = new();

        internal LogbookStateService(WorldMapSessionCoordinator world)
            => _world = world ?? throw new ArgumentNullException(nameof(world));

        public bool AddBlessing(ArcanumObjectId pc, int id) => AddEffect(pc, id, true);
        public bool AddCurse(ArcanumObjectId pc, int id) => AddEffect(pc, id, false);
        public bool RemoveBlessing(ArcanumObjectId pc, int id) => RemoveEffect(pc, id, true);
        public bool RemoveCurse(ArcanumObjectId pc, int id) => RemoveEffect(pc, id, false);
        public bool HasBlessing(ArcanumObjectId pc, int id) => HasEffect(pc, id, true);
        public bool HasCurse(ArcanumObjectId pc, int id) => HasEffect(pc, id, false);

        public IReadOnlyList<LogbookEffectEntry> GetEffects(ArcanumObjectId pc)
            => _characters.TryGetValue(pc, out CharacterState state)
                ? state.Effects.OrderBy(value => value.Timestamp)
                    .ThenByDescending(value => value.IsBlessing).ThenBy(value => value.Id).ToArray()
                : Array.Empty<LogbookEffectEntry>();

        public LogbookKillStatistics GetKills(ArcanumObjectId pc)
            => _characters.TryGetValue(pc, out CharacterState state) ? state.Kills : new LogbookKillStatistics();

        public void RecordKill(ArcanumObjectId pc, int descriptionId, int level, int alignment, int aptitude)
        {
            RequirePc(pc);
            if (descriptionId < 0) throw new ArgumentOutOfRangeException(nameof(descriptionId));
            LogbookKillStatistics value = State(pc).Kills;
            value.TotalKills = checked(value.TotalKills + 1);
            if (descriptionId > 0 && (value.MostPowerfulDescriptionId == 0 || level >= value.MostPowerfulLevel))
            { value.MostPowerfulDescriptionId = descriptionId; value.MostPowerfulLevel = level; }
            if (descriptionId > 0 && (value.LeastPowerfulDescriptionId == 0 || level <= value.LeastPowerfulLevel))
            { value.LeastPowerfulDescriptionId = descriptionId; value.LeastPowerfulLevel = level; }
            if (descriptionId > 0 && alignment > 0
                && (value.MostGoodDescriptionId == 0 || alignment >= value.MostGoodValue))
            { value.MostGoodDescriptionId = descriptionId; value.MostGoodValue = alignment; }
            if (descriptionId > 0 && alignment < 0
                && (value.MostEvilDescriptionId == 0 || alignment <= value.MostEvilValue))
            { value.MostEvilDescriptionId = descriptionId; value.MostEvilValue = alignment; }
            if (descriptionId > 0 && aptitude > 0
                && (value.MostMagicalDescriptionId == 0 || aptitude >= value.MostMagicalValue))
            { value.MostMagicalDescriptionId = descriptionId; value.MostMagicalValue = aptitude; }
            if (descriptionId > 0 && aptitude < 0
                && (value.MostTechnicalDescriptionId == 0 || aptitude <= value.MostTechnicalValue))
            { value.MostTechnicalDescriptionId = descriptionId; value.MostTechnicalValue = aptitude; }
            Notify();
        }

        public void RecordInjury(ArcanumObjectId pc, int attackerDescriptionId, LogbookInjuryType type)
        {
            RequirePc(pc);
            if (attackerDescriptionId <= 0) return;
            State(pc).Injuries.Add(new LogbookInjuryEntry(attackerDescriptionId, type, true));
            Notify();
        }

        public bool HealInjury(ArcanumObjectId pc, LogbookInjuryType type)
        {
            RequirePc(pc);
            CharacterState state = State(pc);
            for (int index = state.Injuries.Count - 1; index >= 0; index--)
            {
                LogbookInjuryEntry value = state.Injuries[index];
                if (value.Type != type || !value.Active) continue;
                state.Injuries[index] = new LogbookInjuryEntry(value.SourceDescriptionId, type, false);
                Notify();
                return true;
            }
            return false;
        }

        public IReadOnlyList<LogbookInjuryEntry> GetInjuries(ArcanumObjectId pc)
            => _characters.TryGetValue(pc, out CharacterState state)
                ? state.Injuries.ToArray() : Array.Empty<LogbookInjuryEntry>();

        public bool AcquireKey(ArcanumObjectId pc, int keyId)
        {
            RequirePc(pc);
            if (keyId <= 0) throw new ArgumentOutOfRangeException(nameof(keyId));
            CharacterState state = State(pc);
            if (state.Keys.Contains(keyId)) return false;
            state.Keys.Add(keyId);
            Notify();
            return true;
        }

        public IReadOnlyList<int> GetKeys(ArcanumObjectId pc)
            => _characters.TryGetValue(pc, out CharacterState state)
                ? state.Keys.ToArray() : Array.Empty<int>();

        public LogbookSaveData ExportSaveData()
            => new()
            {
                Characters = _characters.OrderBy(value => value.Key.Key, StringComparer.Ordinal)
                    .Select(value => value.Value.Save(value.Key)).ToList(),
            };

        internal void RestoreSaveData(LogbookSaveData data)
        {
            if (data == null) return;
            foreach (LogbookCharacterSaveData value in data.Characters)
            {
                ArcanumObjectId.TryParsePersistent(value.Identity, out ArcanumObjectId pc);
                _characters.Add(pc, CharacterState.Restore(value));
            }
        }

        internal static bool ValidateSaveData(LogbookSaveData data, ArcanumObjectId player, out string error)
        {
            error = null;
            if (data == null) return true;
            if (data.Characters == null) { error = "The Logbook state is incomplete."; return false; }
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (LogbookCharacterSaveData value in data.Characters)
            {
                if (value == null || !ArcanumObjectId.TryParsePersistent(value.Identity, out ArcanumObjectId pc)
                    || pc != player || !identities.Add(value.Identity) || value.Effects == null
                    || value.Injuries == null || value.Keys == null || value.Kills == null
                    || value.Effects.Any(effect => effect == null || effect.Id < 50 || effect.Timestamp < 0)
                    || value.Injuries.Any(injury => injury == null || injury.SourceDescriptionId <= 0
                        || !Enum.IsDefined(typeof(LogbookInjuryType), injury.Type))
                    || value.Keys.Any(key => key <= 0) || value.Keys.Distinct().Count() != value.Keys.Count
                    || value.Kills.TotalKills < 0)
                { error = "A saved Logbook character is invalid."; return false; }
            }
            return true;
        }

        private bool AddEffect(ArcanumObjectId pc, int id, bool blessing)
        {
            RequirePc(pc);
            if (id < 50) throw new ArgumentOutOfRangeException(nameof(id));
            CharacterState state = State(pc);
            if (state.Effects.Any(value => value.Id == id && value.IsBlessing == blessing)) return false;
            state.Effects.Add(new LogbookEffectEntry(id, _world.SourceTime.CampaignMilliseconds, blessing));
            Notify();
            return true;
        }

        private bool RemoveEffect(ArcanumObjectId pc, int id, bool blessing)
        {
            RequirePc(pc);
            CharacterState state = State(pc);
            int index = state.Effects.FindIndex(value => value.Id == id && value.IsBlessing == blessing);
            if (index < 0) return false;
            state.Effects.RemoveAt(index);
            Notify();
            return true;
        }

        private bool HasEffect(ArcanumObjectId pc, int id, bool blessing)
            => _characters.TryGetValue(pc, out CharacterState state)
               && state.Effects.Any(value => value.Id == id && value.IsBlessing == blessing);

        private CharacterState State(ArcanumObjectId pc)
        {
            if (!_characters.TryGetValue(pc, out CharacterState state))
                _characters.Add(pc, state = new CharacterState());
            return state;
        }

        private void RequirePc(ArcanumObjectId pc)
        {
            if (_world.PlayerState?.Identity != pc) throw new ArgumentException("Logbook state belongs to the PC.");
        }

        private void Notify() => _world.GameplayHud.Notify(HudPrimaryNotification.Logbook);

        private sealed class CharacterState
        {
            internal readonly List<LogbookEffectEntry> Effects = new();
            internal readonly List<LogbookInjuryEntry> Injuries = new();
            internal readonly List<int> Keys = new();
            internal LogbookKillStatistics Kills = new();

            internal LogbookCharacterSaveData Save(ArcanumObjectId identity) => new()
            {
                Identity = identity.Key,
                Effects = Effects.Select(value => new LogbookEffectSaveData
                { Id = value.Id, Timestamp = value.Timestamp, IsBlessing = value.IsBlessing }).ToList(),
                Injuries = Injuries.Select(value => new LogbookInjurySaveData
                { SourceDescriptionId = value.SourceDescriptionId, Type = (int)value.Type, Active = value.Active }).ToList(),
                Keys = Keys.ToList(),
                Kills = new LogbookKillSaveData
                {
                    TotalKills = Kills.TotalKills,
                    MostPowerfulDescriptionId = Kills.MostPowerfulDescriptionId,
                    MostPowerfulLevel = Kills.MostPowerfulLevel,
                    LeastPowerfulDescriptionId = Kills.LeastPowerfulDescriptionId,
                    LeastPowerfulLevel = Kills.LeastPowerfulLevel,
                    MostGoodDescriptionId = Kills.MostGoodDescriptionId,
                    MostGoodValue = Kills.MostGoodValue,
                    MostEvilDescriptionId = Kills.MostEvilDescriptionId,
                    MostEvilValue = Kills.MostEvilValue,
                    MostMagicalDescriptionId = Kills.MostMagicalDescriptionId,
                    MostMagicalValue = Kills.MostMagicalValue,
                    MostTechnicalDescriptionId = Kills.MostTechnicalDescriptionId,
                    MostTechnicalValue = Kills.MostTechnicalValue,
                },
            };

            internal static CharacterState Restore(LogbookCharacterSaveData value)
            {
                var state = new CharacterState();
                state.Effects.AddRange(value.Effects.Select(entry =>
                    new LogbookEffectEntry(entry.Id, entry.Timestamp, entry.IsBlessing)));
                state.Injuries.AddRange(value.Injuries.Select(entry =>
                    new LogbookInjuryEntry(entry.SourceDescriptionId, (LogbookInjuryType)entry.Type, entry.Active)));
                state.Keys.AddRange(value.Keys);
                LogbookKillSaveData kills = value.Kills;
                state.Kills = new LogbookKillStatistics
                {
                    TotalKills = kills.TotalKills,
                    MostPowerfulDescriptionId = kills.MostPowerfulDescriptionId,
                    MostPowerfulLevel = kills.MostPowerfulLevel,
                    LeastPowerfulDescriptionId = kills.LeastPowerfulDescriptionId,
                    LeastPowerfulLevel = kills.LeastPowerfulLevel,
                    MostGoodDescriptionId = kills.MostGoodDescriptionId,
                    MostGoodValue = kills.MostGoodValue,
                    MostEvilDescriptionId = kills.MostEvilDescriptionId,
                    MostEvilValue = kills.MostEvilValue,
                    MostMagicalDescriptionId = kills.MostMagicalDescriptionId,
                    MostMagicalValue = kills.MostMagicalValue,
                    MostTechnicalDescriptionId = kills.MostTechnicalDescriptionId,
                    MostTechnicalValue = kills.MostTechnicalValue,
                };
                return state;
            }
        }
    }
}
