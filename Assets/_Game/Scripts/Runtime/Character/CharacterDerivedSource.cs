using System;

namespace Arcanum.Runtime.Character
{
    /// <summary>Resolved immutable source inputs for one character's M4D state.</summary>
    public sealed class CharacterDerivedSource : IEquatable<CharacterDerivedSource>
    {
        public const int AloofNpcFlag = 0x00020000;

        private readonly int[] _resistances;

        public int BaseArmorClass { get; }
        public int Alignment { get; }
        public int MagickPoints { get; }
        public int TechPoints { get; }
        public int ReactionBase { get; }
        public bool IsAloof { get; }
        public bool IsMonstrous { get; }
        public bool HasInstanceStatOverride { get; }
        public bool HasInstanceArmorClassOverride { get; }
        public bool HasInstanceResistanceOverride { get; }
        public bool HasInstanceReactionOverride { get; }

        public CharacterDerivedSource(int baseArmorClass, int[] resistances, int alignment, int magickPoints,
            int techPoints, int reactionBase = 50, bool isAloof = false, bool isMonstrous = false,
            bool hasInstanceStatOverride = false, bool hasInstanceArmorClassOverride = false,
            bool hasInstanceResistanceOverride = false, bool hasInstanceReactionOverride = false)
        {
            if (resistances == null) throw new ArgumentNullException(nameof(resistances));
            if (resistances.Length < CharacterDerivedStatRules.ResistanceCount)
                throw new ArgumentException($"At least {CharacterDerivedStatRules.ResistanceCount} resistance values are required.",
                    nameof(resistances));
            if (alignment < CharacterDerivedStatRules.MinimumAlignment
                || alignment > CharacterDerivedStatRules.MaximumAlignment)
                throw new ArgumentOutOfRangeException(nameof(alignment));
            if (magickPoints < 0 || magickPoints > CharacterDerivedStatRules.MaximumAptitudePoints)
                throw new ArgumentOutOfRangeException(nameof(magickPoints));
            if (techPoints < 0 || techPoints > CharacterDerivedStatRules.MaximumAptitudePoints)
                throw new ArgumentOutOfRangeException(nameof(techPoints));

            BaseArmorClass = baseArmorClass;
            _resistances = new int[CharacterDerivedStatRules.ResistanceCount];
            Array.Copy(resistances, _resistances, _resistances.Length);
            Alignment = alignment;
            MagickPoints = magickPoints;
            TechPoints = techPoints;
            ReactionBase = reactionBase;
            IsAloof = isAloof;
            IsMonstrous = isMonstrous;
            HasInstanceStatOverride = hasInstanceStatOverride;
            HasInstanceArmorClassOverride = hasInstanceArmorClassOverride;
            HasInstanceResistanceOverride = hasInstanceResistanceOverride;
            HasInstanceReactionOverride = hasInstanceReactionOverride;
        }

        public static CharacterDerivedSource DevelopmentPlayer { get; } = new(0,
            new int[CharacterDerivedStatRules.ResistanceCount], 0, 0, 0);

        public static CharacterDerivedSource Resolve(int[] instanceStatBase, int[] prototypeStatBase,
            int? instanceBaseArmorClass, int? prototypeBaseArmorClass, int[] instanceResistances,
            int[] prototypeResistances, int? instanceReactionBase, int? prototypeReactionBase,
            int npcFlags, int critterFlags)
        {
            int[] stats = instanceStatBase ?? prototypeStatBase
                ?? throw new InvalidOperationException("Character derived state has no source stat-base array.");
            if (stats.Length < CharacterAttributeSet.SourceStatArrayCount)
                throw new InvalidOperationException("Character derived source stat-base array is incomplete.");
            int[] resistances = instanceResistances ?? prototypeResistances
                ?? new int[CharacterDerivedStatRules.ResistanceCount];
            return new CharacterDerivedSource(
                instanceBaseArmorClass ?? prototypeBaseArmorClass ?? 0,
                resistances,
                Clamp(stats[CharacterDerivedStatRules.AlignmentSourceSlot],
                    CharacterDerivedStatRules.MinimumAlignment, CharacterDerivedStatRules.MaximumAlignment),
                Clamp(stats[CharacterDerivedStatRules.MagickPointsSourceSlot], 0,
                    CharacterDerivedStatRules.MaximumAptitudePoints),
                Clamp(stats[CharacterDerivedStatRules.TechPointsSourceSlot], 0,
                    CharacterDerivedStatRules.MaximumAptitudePoints),
                instanceReactionBase ?? prototypeReactionBase ?? 50,
                (npcFlags & AloofNpcFlag) != 0,
                (critterFlags & CharacterProgressionSource.MonstrousCritterFlags) != 0,
                instanceStatBase != null, instanceBaseArmorClass.HasValue, instanceResistances != null,
                instanceReactionBase.HasValue);
        }

        public int GetResistance(CharacterResistance resistance)
        {
            CharacterDerivedStatRules.ValidateResistance(resistance);
            return _resistances[(int)resistance];
        }

        public bool Equals(CharacterDerivedSource other)
        {
            if (ReferenceEquals(other, null) || BaseArmorClass != other.BaseArmorClass
                || Alignment != other.Alignment || MagickPoints != other.MagickPoints
                || TechPoints != other.TechPoints || ReactionBase != other.ReactionBase
                || IsAloof != other.IsAloof || IsMonstrous != other.IsMonstrous
                || HasInstanceStatOverride != other.HasInstanceStatOverride
                || HasInstanceArmorClassOverride != other.HasInstanceArmorClassOverride
                || HasInstanceResistanceOverride != other.HasInstanceResistanceOverride
                || HasInstanceReactionOverride != other.HasInstanceReactionOverride)
                return false;
            foreach (CharacterResistance resistance in CharacterDerivedStatRules.AllResistances)
                if (GetResistance(resistance) != other.GetResistance(resistance)) return false;
            return true;
        }

        public override bool Equals(object obj) => Equals(obj as CharacterDerivedSource);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(BaseArmorClass);
            hash.Add(Alignment);
            hash.Add(MagickPoints);
            hash.Add(TechPoints);
            hash.Add(ReactionBase);
            hash.Add(IsAloof);
            hash.Add(IsMonstrous);
            foreach (CharacterResistance resistance in CharacterDerivedStatRules.AllResistances)
                hash.Add(GetResistance(resistance));
            return hash.ToHashCode();
        }

        private static int Clamp(int value, int minimum, int maximum) => Math.Max(minimum, Math.Min(maximum, value));
    }
}
