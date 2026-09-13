using System;
using System.Collections.Generic;
using Arcanum.Formats.Objects;

namespace Arcanum.Runtime.Character
{
    public interface ICharacterLevelProvider
    {
        event Action<ArcanumObjectId> LevelChanged;
        bool TryGetLevel(ArcanumObjectId identity, out int level);
    }

    public readonly struct ExperienceAwardResult
    {
        public int PreviousExperience { get; }
        public int Experience { get; }
        public int PreviousLevel { get; }
        public int Level { get; }
        public int CharacterPointsAwarded { get; }

        internal ExperienceAwardResult(int previousExperience, int experience, int previousLevel, int level,
            int characterPointsAwarded)
        {
            PreviousExperience = previousExperience;
            Experience = experience;
            PreviousLevel = previousLevel;
            Level = level;
            CharacterPointsAwarded = characterPointsAwarded;
        }
    }

    /// <summary>Authoritative M4C progression and permanent-skill domain keyed by persistent ObjectID.</summary>
    public sealed class CharacterProgressionService : ICharacterLevelProvider
    {
        public const int MaximumPlayableLevel = 50;
        public const int MaximumExperience = 2_000_000_000;
        public const int MaximumUnspentCharacterPoints = 56;

        private static readonly int[] ExperienceThresholds =
        {
            0,
            0, 2100, 4600, 7700, 11400, 15500, 20300, 25600, 31600, 38300,
            45600, 53600, 62400, 71900, 82200, 93300, 105300, 118200, 132000, 146700,
            162500, 179300, 197200, 216300, 236500, 257900, 280600, 304600, 330000, 356800,
            385100, 414900, 446300, 479500, 514300, 551000, 589500, 630000, 672500, 717100,
            764000, 813100, 864600, 918500, 975000, 1034200, 1096200, 1161100, 1229000, 1300000,
        };

        private readonly CharacterStatService _characters;
        private readonly Dictionary<ArcanumObjectId, PersistentCharacterProgressionState> _states = new();

        public IReadOnlyDictionary<ArcanumObjectId, PersistentCharacterProgressionState> States => _states;
        public event Action<ArcanumObjectId> LevelChanged;

        public CharacterProgressionService(CharacterStatService characters)
            => _characters = characters ?? throw new ArgumentNullException(nameof(characters));

        public PersistentCharacterProgressionState GetOrCreateDevelopmentPlayer(ArcanumObjectId identity)
            => GetOrCreate(identity, ObjectType.Pc, null, CharacterProgressionSource.DevelopmentPlayer);

        public PersistentCharacterProgressionState GetOrCreateSourceCharacter(ArcanumObjectId identity,
            ObjectType objectType, int prototypeNumber, CharacterProgressionSource source)
            => GetOrCreate(identity, objectType, prototypeNumber, source);

        public PersistentCharacterProgressionState Get(ArcanumObjectId identity)
            => _states.TryGetValue(identity, out PersistentCharacterProgressionState state)
                ? state
                : throw new KeyNotFoundException($"No authoritative character progression exists for {identity}.");

        public bool TryGet(ArcanumObjectId identity, out PersistentCharacterProgressionState state)
            => _states.TryGetValue(identity, out state);

        public bool TryGetLevel(ArcanumObjectId identity, out int level)
        {
            if (_states.TryGetValue(identity, out PersistentCharacterProgressionState state))
            {
                level = state.Level;
                return true;
            }
            level = 0;
            return false;
        }

        public int GetExperience(ArcanumObjectId identity) => Get(identity).Experience;
        public int GetLevel(ArcanumObjectId identity) => Get(identity).Level;
        public int GetUnspentCharacterPoints(ArcanumObjectId identity) => Get(identity).UnspentCharacterPoints;

        public static int GetExperienceForLevel(int level)
        {
            if (level < 1 || level > MaximumPlayableLevel)
                throw new ArgumentOutOfRangeException(nameof(level));
            return ExperienceThresholds[level];
        }

        public int GetPurchasedSkillPoints(ArcanumObjectId identity, CharacterSkill skill)
            => Get(identity).GetPurchasedPoints(skill);

        public int GetBaseSkillRank(ArcanumObjectId identity, CharacterSkill skill)
            => checked(CharacterSkillRules.SkillUnitsPerPoint * GetPurchasedSkillPoints(identity, skill));

        public int GetEffectiveSkillRank(ArcanumObjectId identity, CharacterSkill skill)
        {
            CharacterSkillRules.ValidateSkill(skill);
            PersistentCharacterProgressionState state = Get(identity);
            int rank = state.Source.IsMonstrous && skill == CharacterSkill.Melee
                ? CharacterSkillRules.MaximumEffectiveRank
                : GetBaseSkillRank(identity, skill);
            int governing = _characters.GetEffectiveAttribute(identity,
                CharacterSkillRules.GoverningAttribute(skill));
            return Math.Min(CharacterSkillRules.MaximumRankForAttribute(governing),
                Math.Min(CharacterSkillRules.MaximumEffectiveRank, Math.Max(0, rank)));
        }

        public SkillTrainingLevel GetTrainingLevel(ArcanumObjectId identity, CharacterSkill skill)
        {
            CharacterSkillRules.ValidateSkill(skill);
            PersistentCharacterProgressionState state = Get(identity);
            if (!state.Source.IsMonstrous || skill != CharacterSkill.Melee) return state.GetStoredTraining(skill);
            int melee = GetEffectiveSkillRank(identity, skill);
            int level = state.Level;
            int training = 0;
            int[] skillRequirements = { 1, 9, 18 };
            int[] levelRequirements = { 10, 20, 30 };
            while (training < 3 && melee >= skillRequirements[training] && level >= levelRequirements[training])
                training++;
            return (SkillTrainingLevel)training;
        }

        public ExperienceAwardResult AwardExperience(ArcanumObjectId identity, int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            PersistentCharacterProgressionState state = Get(identity);
            if (state.ObjectType != ObjectType.Pc)
                throw new InvalidOperationException("Source XP awards recalculate levels only for PCs.");

            int previousExperience = state.Experience;
            int previousLevel = state.Level;
            int experience = (int)Math.Min(MaximumExperience, (long)state.Experience + amount);
            int level = state.Level;
            int awarded = 0;
            while (level < MaximumPlayableLevel && experience >= GetExperienceForLevel(Math.Max(1, level + 1)))
            {
                level++;
                awarded++;
                if (level % 5 == 0) awarded++;
            }
            int unspent = Math.Min(MaximumUnspentCharacterPoints, state.UnspentCharacterPoints + awarded);
            int actualAward = unspent - state.UnspentCharacterPoints;
            state.SetProgression(experience, level, unspent);
            if (level != previousLevel) LevelChanged?.Invoke(identity);
            return new ExperienceAwardResult(previousExperience, experience, previousLevel, level, actualAward);
        }

        public SkillIncreaseResult IncreaseSkill(ArcanumObjectId identity, CharacterSkill skill)
        {
            CharacterSkillRules.ValidateSkill(skill);
            PersistentCharacterProgressionState state = Get(identity);
            int current = state.GetPurchasedPoints(skill);
            if (current >= CharacterSkillRules.MaximumPurchasedPoints) return SkillIncreaseResult.MaximumRank;
            if (state.UnspentCharacterPoints < 1) return SkillIncreaseResult.InsufficientCharacterPoints;
            int nextRank = checked((current + 1) * CharacterSkillRules.SkillUnitsPerPoint);
            int governing = _characters.GetEffectiveAttribute(identity,
                CharacterSkillRules.GoverningAttribute(skill));
            if (nextRank > CharacterSkillRules.MaximumRankForAttribute(governing))
                return SkillIncreaseResult.GoverningAttributeTooLow;
            state.IncreaseSkill(skill);
            return SkillIncreaseResult.Success;
        }

        public TrainingAssignmentResult SetTrainingLevel(ArcanumObjectId identity, CharacterSkill skill,
            SkillTrainingLevel training)
        {
            CharacterSkillRules.ValidateSkill(skill);
            CharacterSkillRules.ValidateTraining(training);
            PersistentCharacterProgressionState state = Get(identity);
            if (state.Source.IsMonstrous && skill == CharacterSkill.Melee)
                return TrainingAssignmentResult.DerivedMonstrousMelee;
            SkillTrainingLevel current = state.GetStoredTraining(skill);
            if (training == current) return TrainingAssignmentResult.Unchanged;
            if (training > current && (int)training != (int)current + 1)
                return TrainingAssignmentResult.NonSequentialIncrease;
            if (GetEffectiveSkillRank(identity, skill) < CharacterSkillRules.MinimumRankForTraining(training))
                return TrainingAssignmentResult.InsufficientSkillRank;
            state.SetTraining(skill, training);
            return TrainingAssignmentResult.Success;
        }

        private PersistentCharacterProgressionState GetOrCreate(ArcanumObjectId identity, ObjectType objectType,
            int? prototypeNumber, CharacterProgressionSource source)
        {
            PersistentCharacterState character = _characters.Get(identity);
            if (character.ObjectType != objectType || character.PrototypeNumber != prototypeNumber)
                throw new InvalidOperationException($"Character/progression source mismatch for {identity}.");
            if (_states.TryGetValue(identity, out PersistentCharacterProgressionState existing))
            {
                if (!existing.MatchesSource(objectType, prototypeNumber, source))
                    throw new InvalidOperationException($"Character progression ObjectID collision or changed source: {identity}.");
                return existing;
            }
            var state = new PersistentCharacterProgressionState(identity, objectType, prototypeNumber, source);
            _states.Add(identity, state);
            return state;
        }
    }
}
