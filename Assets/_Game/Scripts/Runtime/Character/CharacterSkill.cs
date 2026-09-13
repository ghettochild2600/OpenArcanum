using System;
using System.Collections.Generic;

namespace Arcanum.Runtime.Character
{
    /// <summary>All source skills in exact <c>Skill</c> numeric order.</summary>
    public enum CharacterSkill
    {
        Bow = 0,
        Dodge = 1,
        Melee = 2,
        Throwing = 3,
        Backstab = 4,
        PickPocket = 5,
        Prowling = 6,
        SpotTrap = 7,
        Gambling = 8,
        Haggle = 9,
        Heal = 10,
        Persuasion = 11,
        Repair = 12,
        Firearms = 13,
        PickLocks = 14,
        DisarmTraps = 15,
    }

    /// <summary>Source training values packed into bits 6-7 of each skill entry.</summary>
    public enum SkillTrainingLevel
    {
        None = 0,
        Apprentice = 1,
        Expert = 2,
        Master = 3,
    }

    public enum SkillIncreaseResult
    {
        Success,
        InsufficientCharacterPoints,
        GoverningAttributeTooLow,
        MaximumRank,
    }

    public enum TrainingAssignmentResult
    {
        Success,
        Unchanged,
        NonSequentialIncrease,
        InsufficientSkillRank,
        DerivedMonstrousMelee,
    }

    /// <summary>Audited skill identity, governing-stat and rank rules shared by authoritative consumers.</summary>
    public static class CharacterSkillRules
    {
        public const int BasicSkillCount = 12;
        public const int TechnicalSkillCount = 4;
        public const int SkillCount = BasicSkillCount + TechnicalSkillCount;
        public const int SkillUnitsPerPoint = 4;
        public const int MaximumPurchasedPoints = 5;
        public const int MaximumEffectiveRank = 20;

        public static IReadOnlyList<CharacterSkill> AllSkills { get; } = Array.AsReadOnly(new[]
        {
            CharacterSkill.Bow, CharacterSkill.Dodge, CharacterSkill.Melee, CharacterSkill.Throwing,
            CharacterSkill.Backstab, CharacterSkill.PickPocket, CharacterSkill.Prowling,
            CharacterSkill.SpotTrap, CharacterSkill.Gambling, CharacterSkill.Haggle, CharacterSkill.Heal,
            CharacterSkill.Persuasion, CharacterSkill.Repair, CharacterSkill.Firearms,
            CharacterSkill.PickLocks, CharacterSkill.DisarmTraps,
        });

        public static bool IsTechnical(CharacterSkill skill)
        {
            ValidateSkill(skill);
            return (int)skill >= BasicSkillCount;
        }

        public static int SourceGroupIndex(CharacterSkill skill)
        {
            ValidateSkill(skill);
            return IsTechnical(skill) ? (int)skill - BasicSkillCount : (int)skill;
        }

        public static CharacterAttribute GoverningAttribute(CharacterSkill skill)
        {
            ValidateSkill(skill);
            return skill switch
            {
                CharacterSkill.Bow or CharacterSkill.Dodge or CharacterSkill.Melee
                    or CharacterSkill.Throwing or CharacterSkill.Backstab or CharacterSkill.PickPocket
                    or CharacterSkill.PickLocks => CharacterAttribute.Dexterity,
                CharacterSkill.Prowling or CharacterSkill.SpotTrap or CharacterSkill.Firearms
                    or CharacterSkill.DisarmTraps => CharacterAttribute.Perception,
                CharacterSkill.Gambling or CharacterSkill.Heal or CharacterSkill.Repair
                    => CharacterAttribute.Intelligence,
                CharacterSkill.Haggle => CharacterAttribute.Willpower,
                CharacterSkill.Persuasion => CharacterAttribute.Charisma,
                _ => throw new ArgumentOutOfRangeException(nameof(skill)),
            };
        }

        public static int MaximumRankForAttribute(int effectiveAttribute)
        {
            int value = Math.Max(1, Math.Min(20, effectiveAttribute));
            if (value <= 5) return 3;
            if (value <= 8) return 7;
            if (value <= 11) return 11;
            if (value <= 14) return 15;
            if (value <= 17) return 19;
            return 20;
        }

        public static int MinimumRankForTraining(SkillTrainingLevel training)
        {
            ValidateTraining(training);
            return training switch
            {
                SkillTrainingLevel.None => 0,
                SkillTrainingLevel.Apprentice => 1,
                SkillTrainingLevel.Expert => 9,
                SkillTrainingLevel.Master => 18,
                _ => throw new ArgumentOutOfRangeException(nameof(training)),
            };
        }

        public static void ValidateSkill(CharacterSkill skill)
        {
            if ((int)skill < 0 || (int)skill >= SkillCount)
                throw new ArgumentOutOfRangeException(nameof(skill), skill, "Unknown source skill.");
        }

        public static void ValidateTraining(SkillTrainingLevel training)
        {
            if ((int)training < 0 || (int)training > (int)SkillTrainingLevel.Master)
                throw new ArgumentOutOfRangeException(nameof(training), training, "Unknown source training level.");
        }
    }
}
