using System;
using System.Collections.Generic;
using Arcanum.Formats.Objects;

namespace Arcanum.Runtime.Character
{
    /// <summary>Exact source <c>Stat</c> identities for derived character values.</summary>
    public enum CharacterDerivedStat
    {
        CarryWeight = 8,
        MeleeDamageBonus = 9,
        ArmorClassAdjustment = 10,
        Speed = 11,
        HealRate = 12,
        PoisonRecovery = 13,
        BeautyReactionModifier = 14,
        MaximumFollowers = 15,
        MagickTechAptitude = 16,
    }

    /// <summary>Exact source <c>ResistanceType</c> identities.</summary>
    public enum CharacterResistance
    {
        Normal = 0,
        Fire = 1,
        Electrical = 2,
        Poison = 3,
        Magic = 4,
    }

    /// <summary>The bounded character-owned inputs to initial pairwise NPC reaction.</summary>
    public readonly struct CharacterReactionInputs
    {
        public int SourceBase { get; }
        public int BeautyModifier { get; }
        public int RaceModifier { get; }
        public int Subtotal { get; }
        public bool CharacterModifiersApply { get; }

        internal CharacterReactionInputs(int sourceBase, int beautyModifier, int raceModifier,
            bool characterModifiersApply)
        {
            SourceBase = sourceBase;
            BeautyModifier = beautyModifier;
            RaceModifier = raceModifier;
            CharacterModifiersApply = characterModifiersApply;
            Subtotal = checked(sourceBase + beautyModifier + raceModifier);
        }
    }

    public interface ICharacterCarryWeightProvider
    {
        int GetCarryWeight(ArcanumObjectId identity);
    }

    /// <summary>Audited identities, bounds, and innate race inputs for the M4D subset.</summary>
    public static class CharacterDerivedStatRules
    {
        public const int AlignmentSourceSlot = 19;
        public const int MagickPointsSourceSlot = 22;
        public const int TechPointsSourceSlot = 23;
        public const int MinimumAlignment = -1000;
        public const int MaximumAlignment = 1000;
        public const int MaximumAptitudePoints = 210;
        public const int ResistanceCount = 5;

        public static IReadOnlyList<CharacterDerivedStat> AllStats { get; } = Array.AsReadOnly(new[]
        {
            CharacterDerivedStat.CarryWeight, CharacterDerivedStat.MeleeDamageBonus,
            CharacterDerivedStat.ArmorClassAdjustment, CharacterDerivedStat.Speed,
            CharacterDerivedStat.HealRate, CharacterDerivedStat.PoisonRecovery,
            CharacterDerivedStat.BeautyReactionModifier, CharacterDerivedStat.MaximumFollowers,
            CharacterDerivedStat.MagickTechAptitude,
        });

        public static IReadOnlyList<CharacterResistance> AllResistances { get; } = Array.AsReadOnly(new[]
        {
            CharacterResistance.Normal, CharacterResistance.Fire, CharacterResistance.Electrical,
            CharacterResistance.Poison, CharacterResistance.Magic,
        });

        public static void ValidateStat(CharacterDerivedStat stat)
        {
            if ((int)stat < (int)CharacterDerivedStat.CarryWeight
                || (int)stat > (int)CharacterDerivedStat.MagickTechAptitude)
                throw new ArgumentOutOfRangeException(nameof(stat), stat, "Unknown source derived stat.");
        }

        public static void ValidateResistance(CharacterResistance resistance)
        {
            if ((int)resistance < 0 || (int)resistance >= ResistanceCount)
                throw new ArgumentOutOfRangeException(nameof(resistance), resistance,
                    "Unknown source resistance type.");
        }

        internal static int AptitudePointAdjustment(CharacterRace race, bool magick)
        {
            CharacterAttributeRules.ValidateRace(race);
            return race switch
            {
                CharacterRace.Dwarf when !magick => 3,
                CharacterRace.Elf or CharacterRace.DarkElf when magick => 3,
                CharacterRace.HalfElf when magick => 1,
                _ => 0,
            };
        }

        internal static int ResistanceAdjustment(CharacterRace race, CharacterResistance resistance)
        {
            CharacterAttributeRules.ValidateRace(race);
            ValidateResistance(resistance);
            return (race, resistance) switch
            {
                (CharacterRace.HalfOrc, CharacterResistance.Poison) => 10,
                (CharacterRace.HalfOgre, CharacterResistance.Normal) => 10,
                (CharacterRace.Orc, CharacterResistance.Poison) => 20,
                _ => 0,
            };
        }
    }
}
