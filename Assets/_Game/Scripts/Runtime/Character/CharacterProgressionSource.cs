using System;

namespace Arcanum.Runtime.Character
{
    /// <summary>Resolved source fields used to initialize one persistent character progression record.</summary>
    public sealed class CharacterProgressionSource : IEquatable<CharacterProgressionSource>
    {
        public const int LevelSourceSlot = 17;
        public const int ExperienceSourceSlot = 18;
        public const int UnspentPointsSourceSlot = 21;
        public const int MonstrousCritterFlags = 0x00000004 | 0x00000008 | 0x00008000 | 0x20000000;

        private readonly int[] _basicPacked;
        private readonly int[] _technicalPacked;

        public int Level { get; }
        public int Experience { get; }
        public int UnspentCharacterPoints { get; }
        public bool IsMonstrous { get; }
        public bool HasInstanceStatOverride { get; }
        public bool HasInstanceBasicSkillOverride { get; }
        public bool HasInstanceTechnicalSkillOverride { get; }

        public CharacterProgressionSource(int level, int experience, int unspentCharacterPoints,
            int[] basicPacked, int[] technicalPacked, bool isMonstrous = false,
            bool hasInstanceStatOverride = false, bool hasInstanceBasicSkillOverride = false,
            bool hasInstanceTechnicalSkillOverride = false)
        {
            if (level < 0 || level > CharacterVitalitySource.MaximumLevel)
                throw new ArgumentOutOfRangeException(nameof(level));
            if (experience < 0 || experience > CharacterProgressionService.MaximumExperience)
                throw new ArgumentOutOfRangeException(nameof(experience));
            if (unspentCharacterPoints < 0
                || unspentCharacterPoints > CharacterProgressionService.MaximumUnspentCharacterPoints)
                throw new ArgumentOutOfRangeException(nameof(unspentCharacterPoints));
            _basicPacked = CopyPacked(basicPacked, CharacterSkillRules.BasicSkillCount, nameof(basicPacked));
            _technicalPacked = CopyPacked(technicalPacked, CharacterSkillRules.TechnicalSkillCount,
                nameof(technicalPacked));
            Level = level;
            Experience = experience;
            UnspentCharacterPoints = unspentCharacterPoints;
            IsMonstrous = isMonstrous;
            HasInstanceStatOverride = hasInstanceStatOverride;
            HasInstanceBasicSkillOverride = hasInstanceBasicSkillOverride;
            HasInstanceTechnicalSkillOverride = hasInstanceTechnicalSkillOverride;
        }

        public static CharacterProgressionSource DevelopmentPlayer { get; } = new(1, 0, 5,
            new int[CharacterSkillRules.BasicSkillCount], new int[CharacterSkillRules.TechnicalSkillCount]);

        public static CharacterProgressionSource Resolve(int[] instanceStatBase, int[] prototypeStatBase,
            int[] instanceBasicSkills, int[] prototypeBasicSkills, int[] instanceTechnicalSkills,
            int[] prototypeTechnicalSkills, int critterFlags)
        {
            int[] stats = instanceStatBase ?? prototypeStatBase
                ?? throw new InvalidOperationException("Character progression has no source stat-base array.");
            if (stats.Length < CharacterAttributeSet.SourceStatArrayCount)
                throw new InvalidOperationException("Character progression source stat-base array is incomplete.");
            int level = Math.Max(0, Math.Min(CharacterVitalitySource.MaximumLevel, stats[LevelSourceSlot]));
            int experience = Math.Max(0, Math.Min(CharacterProgressionService.MaximumExperience,
                stats[ExperienceSourceSlot]));
            int unspent = Math.Max(0, Math.Min(CharacterProgressionService.MaximumUnspentCharacterPoints,
                stats[UnspentPointsSourceSlot]));
            return new CharacterProgressionSource(level, experience, unspent,
                instanceBasicSkills ?? prototypeBasicSkills ?? new int[CharacterSkillRules.BasicSkillCount],
                instanceTechnicalSkills ?? prototypeTechnicalSkills
                    ?? new int[CharacterSkillRules.TechnicalSkillCount],
                (critterFlags & MonstrousCritterFlags) != 0,
                instanceStatBase != null, instanceBasicSkills != null, instanceTechnicalSkills != null);
        }

        public int GetPacked(CharacterSkill skill)
        {
            CharacterSkillRules.ValidateSkill(skill);
            return CharacterSkillRules.IsTechnical(skill)
                ? _technicalPacked[CharacterSkillRules.SourceGroupIndex(skill)]
                : _basicPacked[CharacterSkillRules.SourceGroupIndex(skill)];
        }

        public bool Equals(CharacterProgressionSource other)
        {
            if (ReferenceEquals(other, null) || Level != other.Level || Experience != other.Experience
                || UnspentCharacterPoints != other.UnspentCharacterPoints || IsMonstrous != other.IsMonstrous
                || HasInstanceStatOverride != other.HasInstanceStatOverride
                || HasInstanceBasicSkillOverride != other.HasInstanceBasicSkillOverride
                || HasInstanceTechnicalSkillOverride != other.HasInstanceTechnicalSkillOverride)
                return false;
            foreach (CharacterSkill skill in CharacterSkillRules.AllSkills)
                if (GetPacked(skill) != other.GetPacked(skill)) return false;
            return true;
        }

        public override bool Equals(object obj) => Equals(obj as CharacterProgressionSource);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Level);
            hash.Add(Experience);
            hash.Add(UnspentCharacterPoints);
            hash.Add(IsMonstrous);
            foreach (CharacterSkill skill in CharacterSkillRules.AllSkills) hash.Add(GetPacked(skill));
            return hash.ToHashCode();
        }

        private static int[] CopyPacked(int[] source, int count, string parameter)
        {
            if (source == null) throw new ArgumentNullException(parameter);
            if (source.Length < count)
                throw new ArgumentException($"At least {count} source skill entries are required.", parameter);
            var copy = new int[count];
            for (int index = 0; index < count; index++) copy[index] = source[index] & 0xFF;
            return copy;
        }
    }
}
