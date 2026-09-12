using System;

namespace Arcanum.Runtime.Character
{
    /// <summary>Resolved source inputs used to initialize one critter's HP and fatigue state.</summary>
    public readonly struct CharacterVitalitySource : IEquatable<CharacterVitalitySource>
    {
        public const int LevelSourceSlot = 17;
        public const int MinimumLevel = 0;
        public const int MaximumLevel = 51;

        public int Level { get; }
        public int HitPointPoints { get; }
        public int HitPointAdjustment { get; }
        public int HitPointDamage { get; }
        public int FatiguePoints { get; }
        public int FatigueAdjustment { get; }
        public int FatigueDamage { get; }

        public CharacterVitalitySource(int level, int hitPointPoints, int hitPointAdjustment, int hitPointDamage,
            int fatiguePoints, int fatigueAdjustment, int fatigueDamage)
        {
            if (level < MinimumLevel || level > MaximumLevel)
                throw new ArgumentOutOfRangeException(nameof(level));
            if (hitPointPoints < 0) throw new ArgumentOutOfRangeException(nameof(hitPointPoints));
            if (hitPointDamage < 0) throw new ArgumentOutOfRangeException(nameof(hitPointDamage));
            if (fatiguePoints < 0) throw new ArgumentOutOfRangeException(nameof(fatiguePoints));
            if (fatigueDamage < 0) throw new ArgumentOutOfRangeException(nameof(fatigueDamage));
            Level = level;
            HitPointPoints = hitPointPoints;
            HitPointAdjustment = hitPointAdjustment;
            HitPointDamage = hitPointDamage;
            FatiguePoints = fatiguePoints;
            FatigueAdjustment = fatigueAdjustment;
            FatigueDamage = fatigueDamage;
        }

        public static CharacterVitalitySource DevelopmentPlayer => new(1, 0, 0, 0, 0, 0, 0);

        public static CharacterVitalitySource Resolve(int[] instanceStatBase, int[] prototypeStatBase,
            int? instanceHpPoints, int? prototypeHpPoints, int? instanceHpAdjustment,
            int? prototypeHpAdjustment, int? instanceHpDamage, int? prototypeHpDamage,
            int? instanceFatiguePoints, int? prototypeFatiguePoints, int? instanceFatigueAdjustment,
            int? prototypeFatigueAdjustment, int? instanceFatigueDamage, int? prototypeFatigueDamage)
        {
            int[] stats = instanceStatBase ?? prototypeStatBase
                ?? throw new InvalidOperationException("Character vitality has no source stat-base array.");
            if (stats.Length <= LevelSourceSlot)
                throw new InvalidOperationException("Character vitality source stat-base array has no level slot.");
            int level = Math.Max(MinimumLevel, Math.Min(MaximumLevel, stats[LevelSourceSlot]));
            return new CharacterVitalitySource(
                level,
                NonNegative(instanceHpPoints ?? prototypeHpPoints ?? 0),
                instanceHpAdjustment ?? prototypeHpAdjustment ?? 0,
                NonNegative(instanceHpDamage ?? prototypeHpDamage ?? 0),
                NonNegative(instanceFatiguePoints ?? prototypeFatiguePoints ?? 0),
                instanceFatigueAdjustment ?? prototypeFatigueAdjustment ?? 0,
                NonNegative(instanceFatigueDamage ?? prototypeFatigueDamage ?? 0));
        }

        public bool Equals(CharacterVitalitySource other)
            => Level == other.Level && HitPointPoints == other.HitPointPoints
                && HitPointAdjustment == other.HitPointAdjustment && HitPointDamage == other.HitPointDamage
                && FatiguePoints == other.FatiguePoints && FatigueAdjustment == other.FatigueAdjustment
                && FatigueDamage == other.FatigueDamage;

        public override bool Equals(object obj) => obj is CharacterVitalitySource other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(Level, HitPointPoints, HitPointAdjustment, HitPointDamage, FatiguePoints,
                FatigueAdjustment, FatigueDamage);

        private static int NonNegative(int value) => Math.Max(0, value);
    }
}
