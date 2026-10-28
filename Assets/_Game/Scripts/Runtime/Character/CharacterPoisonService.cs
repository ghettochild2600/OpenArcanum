using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.World;

namespace Arcanum.Runtime.Character
{
    public enum PoisonApplicationFailure
    {
        None,
        InvalidTarget,
        DeadTarget,
    }

    public readonly struct PoisonApplicationResult
    {
        public PoisonApplicationFailure Failure { get; }
        public int RequestedAmount { get; }
        public int ResistedAmount { get; }
        public int AppliedAmount { get; }
        public int ResultingLevel { get; }
        public bool Succeeded => Failure == PoisonApplicationFailure.None;

        internal PoisonApplicationResult(PoisonApplicationFailure failure, int requestedAmount,
            int resistedAmount, int appliedAmount, int resultingLevel)
        {
            Failure = failure;
            RequestedAmount = requestedAmount;
            ResistedAmount = resistedAmount;
            AppliedAmount = appliedAmount;
            ResultingLevel = resultingLevel;
        }
    }

    public readonly struct CharacterPoisonChange
    {
        public ArcanumObjectId Identity { get; }
        public int PreviousLevel { get; }
        public int CurrentLevel { get; }

        internal CharacterPoisonChange(ArcanumObjectId identity, int previousLevel, int currentLevel)
        {
            Identity = identity;
            PreviousLevel = previousLevel;
            CurrentLevel = currentLevel;
        }
    }

    /// <summary>
    /// Authoritative source poison stat and its two game-time events. Source stat.c schedules damage every
    /// 15 seconds and recovery every 120 seconds without resetting an already scheduled event on accumulation.
    /// </summary>
    public sealed class CharacterPoisonService : IDisposable
    {
        public const int MaximumLevel = 1000;
        public const int DamageIntervalMilliseconds = 15_000;
        public const int RecoveryIntervalMilliseconds = 120_000;
        private const int ExtraordinaryConstitution = 20;
        private const string ContributorOwner = "status:poison";

        private sealed class State
        {
            internal int Level;
            internal long NextDamageAtMilliseconds;
            internal long NextRecoveryAtMilliseconds;
            internal int? RestoredCurrentHitPoints;
        }

        private readonly CharacterStatService _characters;
        private readonly CharacterDerivedStatService _derived;
        private readonly CharacterVitalityService _vitality;
        private readonly SourceTimeService _time;
        private readonly Dictionary<ArcanumObjectId, State> _states = new();
        private bool _disposed;

        public event Action<CharacterPoisonChange> Changed;

        public CharacterPoisonService(CharacterStatService characters, CharacterDerivedStatService derived,
            CharacterVitalityService vitality, SourceTimeService time)
        {
            _characters = characters ?? throw new ArgumentNullException(nameof(characters));
            _derived = derived ?? throw new ArgumentNullException(nameof(derived));
            _vitality = vitality ?? throw new ArgumentNullException(nameof(vitality));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _time.Advanced += OnTimeAdvanced;
        }

        public int GetLevel(ArcanumObjectId identity)
            => _states.TryGetValue(identity, out State state) ? state.Level : 0;

        public bool IsPoisoned(ArcanumObjectId identity) => GetLevel(identity) > 0;

        public PoisonApplicationResult Apply(ArcanumObjectId identity, int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!_characters.TryGet(identity, out _) || !_vitality.TryGet(identity, out _))
                return new PoisonApplicationResult(PoisonApplicationFailure.InvalidTarget, amount, 0, 0, 0);
            if (_vitality.IsDead(identity))
                return new PoisonApplicationResult(PoisonApplicationFailure.DeadTarget, amount, 0, 0,
                    GetLevel(identity));

            int previous = GetLevel(identity);
            if (_characters.GetEffectiveAttribute(identity, CharacterAttribute.Constitution)
                >= ExtraordinaryConstitution)
            {
                SetLevel(identity, 0, 0, 0);
                return new PoisonApplicationResult(PoisonApplicationFailure.None, amount, amount, 0, 0);
            }

            int resistance = _derived.GetResistance(identity, CharacterResistance.Poison);
            int resisted = (int)Math.Min(amount, (long)amount * resistance / 100L);
            int applied = amount - resisted;
            int resulting = (int)Math.Min(MaximumLevel, (long)previous + applied);
            if (resulting > 0)
            {
                State state = GetOrCreate(identity);
                state.Level = resulting;
                if (state.NextDamageAtMilliseconds == 0)
                    state.NextDamageAtMilliseconds = checked(_time.ElapsedMilliseconds + DamageIntervalMilliseconds);
                if (state.NextRecoveryAtMilliseconds == 0)
                    state.NextRecoveryAtMilliseconds = checked(_time.ElapsedMilliseconds + RecoveryIntervalMilliseconds);
                SynchronizeContributors(identity, state.Level);
                RaiseChanged(identity, previous, state.Level);
            }
            return new PoisonApplicationResult(PoisonApplicationFailure.None, amount, resisted,
                resulting - previous, resulting);
        }

        public int Cure(ArcanumObjectId identity, int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!_characters.TryGet(identity, out _) || !_vitality.TryGet(identity, out _))
                throw new KeyNotFoundException($"No authoritative character vitality exists for {identity}.");
            int previous = GetLevel(identity);
            int resulting = Math.Max(0, previous - amount);
            SetLevel(identity, resulting,
                resulting == 0 ? 0 : _states[identity].NextDamageAtMilliseconds,
                resulting == 0 ? 0 : _states[identity].NextRecoveryAtMilliseconds);
            return previous - resulting;
        }

        public void Clear(ArcanumObjectId identity) => Cure(identity, MaximumLevel);

        public PoisonSaveData ExportSaveData()
            => new()
            {
                Characters = _states.Where(pair => pair.Value.Level > 0)
                    .OrderBy(pair => pair.Key.Key, StringComparer.Ordinal)
                    .Select(pair => new PoisonCharacterSaveData
                    {
                        Identity = pair.Key.Key,
                        Level = pair.Value.Level,
                        NextDamageAtMilliseconds = pair.Value.NextDamageAtMilliseconds,
                        NextRecoveryAtMilliseconds = pair.Value.NextRecoveryAtMilliseconds,
                        CurrentHitPoints = _vitality.GetCurrentHitPoints(pair.Key),
                    }).ToList(),
            };

        public void RestoreSaveData(PoisonSaveData data)
        {
            foreach (ArcanumObjectId identity in _states.Keys.ToArray())
                _characters.Contributors.RemoveOwner(identity, ContributorOwner);
            _states.Clear();
            if (data?.Characters == null) return;
            foreach (PoisonCharacterSaveData value in data.Characters)
            {
                ArcanumObjectId.TryParsePersistent(value.Identity, out ArcanumObjectId identity);
                SetLevel(identity, value.Level, value.NextDamageAtMilliseconds,
                    value.NextRecoveryAtMilliseconds, false);
                _states[identity].RestoredCurrentHitPoints = value.CurrentHitPoints;
            }
        }

        internal void FinalizeRestore()
        {
            foreach (var pair in _states)
            {
                if (!pair.Value.RestoredCurrentHitPoints.HasValue) continue;
                PersistentCharacterVitalityState vitality = _vitality.Get(pair.Key);
                int damage = checked(vitality.MaximumHitPoints - pair.Value.RestoredCurrentHitPoints.Value);
                if (damage < 0)
                    throw new InvalidOperationException($"Poison vitality for {pair.Key} exceeds its restored maximum.");
                vitality.SetDamage(damage, vitality.FatigueDamage);
                pair.Value.RestoredCurrentHitPoints = null;
            }
        }

        public static bool ValidateSaveData(PoisonSaveData data, CharacterStatService characters,
            CharacterVitalityService vitality, long elapsedMilliseconds, out string error)
        {
            error = null;
            if (data == null) return true;
            if (data.Characters == null) { error = "The poison state is incomplete."; return false; }
            var seen = new HashSet<ArcanumObjectId>();
            foreach (PoisonCharacterSaveData value in data.Characters)
            {
                if (value == null || !ArcanumObjectId.TryParsePersistent(value.Identity, out ArcanumObjectId identity)
                    || !characters.TryGet(identity, out _) || !vitality.TryGet(identity, out _) || !seen.Add(identity)
                    || value.Level <= 0 || value.Level > MaximumLevel
                    || value.NextDamageAtMilliseconds <= elapsedMilliseconds
                    || value.NextRecoveryAtMilliseconds <= elapsedMilliseconds
                    || (long)vitality.GetMaximumHitPoints(identity) - value.CurrentHitPoints > int.MaxValue)
                {
                    error = $"Poison state for '{value?.Identity}' is invalid.";
                    return false;
                }
            }
            return true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _time.Advanced -= OnTimeAdvanced;
        }

        private State GetOrCreate(ArcanumObjectId identity)
        {
            if (_states.TryGetValue(identity, out State state)) return state;
            state = new State();
            _states.Add(identity, state);
            return state;
        }

        private void OnTimeAdvanced(SourceTimeAdvance advance)
        {
            foreach (ArcanumObjectId identity in _states.Keys.ToArray())
            {
                State state = _states[identity];
                while (state.Level > 0)
                {
                    long next = Math.Min(state.NextDamageAtMilliseconds, state.NextRecoveryAtMilliseconds);
                    if (next > advance.TotalMilliseconds) break;
                    if (state.NextDamageAtMilliseconds == next)
                    {
                        if (_vitality.TryGet(identity, out _) && !_vitality.IsDead(identity))
                            _vitality.ApplyHitPointDamage(identity, DamageFor(state.Level));
                        state.NextDamageAtMilliseconds = checked(next + DamageIntervalMilliseconds);
                    }
                    if (state.Level > 0 && state.NextRecoveryAtMilliseconds == next)
                    {
                        int previous = state.Level;
                        int recovery = _derived.GetDerivedStat(identity, CharacterDerivedStat.PoisonRecovery);
                        state.Level = Math.Max(0, state.Level - recovery);
                        state.NextRecoveryAtMilliseconds = state.Level == 0
                            ? 0 : checked(next + RecoveryIntervalMilliseconds);
                        if (state.Level == 0) state.NextDamageAtMilliseconds = 0;
                        SynchronizeContributors(identity, state.Level);
                        RaiseChanged(identity, previous, state.Level);
                    }
                }
                if (state.Level == 0) _states.Remove(identity);
            }
        }

        private void SetLevel(ArcanumObjectId identity, int level, long nextDamage, long nextRecovery,
            bool raise = true)
        {
            int previous = GetLevel(identity);
            if (level == 0)
            {
                _states.Remove(identity);
                SynchronizeContributors(identity, 0);
            }
            else
            {
                State state = GetOrCreate(identity);
                state.Level = level;
                state.NextDamageAtMilliseconds = nextDamage;
                state.NextRecoveryAtMilliseconds = nextRecovery;
                SynchronizeContributors(identity, level);
            }
            if (raise) RaiseChanged(identity, previous, level);
        }

        private void SynchronizeContributors(ArcanumObjectId identity, int level)
        {
            int penalty = Math.Min(3, level / 100);
            if (penalty == 0)
            {
                _characters.Contributors.RemoveOwner(identity, ContributorOwner);
                return;
            }
            _characters.Contributors.SetOwnerContributors(identity, ContributorOwner, new[]
            {
                new CharacterContributor(ContributorOwner, CharacterContributorDomain.Status, 0,
                    "poison:strength", CharacterContributorTarget.Attribute(CharacterAttribute.Strength),
                    CharacterContributorOperation.Add, -penalty, (int)CharacterAttribute.Strength),
                new CharacterContributor(ContributorOwner, CharacterContributorDomain.Status, 0,
                    "poison:dexterity", CharacterContributorTarget.Attribute(CharacterAttribute.Dexterity),
                    CharacterContributorOperation.Add, -penalty, (int)CharacterAttribute.Dexterity),
            });
        }

        private void RaiseChanged(ArcanumObjectId identity, int previous, int current)
        {
            if (previous != current) Changed?.Invoke(new CharacterPoisonChange(identity, previous, current));
        }

        private static int DamageFor(int level) => level >= 550 ? 3 : level >= 200 ? 2 : 1;
    }
}
