using System;
using System.Collections.Generic;

namespace Arcanum.Runtime.Character
{
    /// <summary>Immutable stored values for the eight source primary-stat slots.</summary>
    public sealed class CharacterAttributeSet
    {
        public const int Count = 8;
        public const int SourceStatArrayCount = 28;
        public const int GenderSourceSlot = 26;
        public const int RaceSourceSlot = 27;
        public const int SourceDefault = 8;
        public const int SourceMinimum = 1;
        public const int OrdinarySourceMaximum = 20;

        private readonly int[] _values;

        public CharacterAttributeSet(IReadOnlyList<int> values, CharacterRace race)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (values.Count != Count)
                throw new ArgumentException($"Exactly {Count} primary attributes are required.", nameof(values));
            CharacterAttributeRules.ValidateRace(race);
            _values = new int[Count];
            for (int index = 0; index < Count; index++)
            {
                CharacterAttribute attribute = (CharacterAttribute)index;
                int value = values[index];
                int maximum = CharacterAttributeRules.SourceMaximum(race, attribute);
                if (value < SourceMinimum || value > maximum)
                    throw new ArgumentOutOfRangeException(nameof(values), value,
                        $"{attribute} base must be in the source range {SourceMinimum}..{maximum} for {race}.");
                _values[index] = value;
            }
        }

        public int Get(CharacterAttribute attribute)
        {
            CharacterAttributeRules.ValidateAttribute(attribute);
            return _values[(int)attribute];
        }

        public int[] ToArray() => (int[])_values.Clone();

        public static CharacterAttributeSet FromSourceStatArray(int[] sourceValues, out CharacterRace race,
            out CharacterGender gender)
        {
            if (sourceValues == null) throw new ArgumentNullException(nameof(sourceValues));
            if (sourceValues.Length < SourceStatArrayCount)
                throw new ArgumentException($"A critter stat array must contain {SourceStatArrayCount} source slots.",
                    nameof(sourceValues));
            race = (CharacterRace)sourceValues[RaceSourceSlot];
            gender = (CharacterGender)sourceValues[GenderSourceSlot];
            CharacterAttributeRules.ValidateRace(race);
            CharacterAttributeRules.ValidateGender(gender);
            var primary = new int[Count];
            Array.Copy(sourceValues, primary, Count);
            return new CharacterAttributeSet(primary, race);
        }

        public static CharacterAttributeSet SourceDefaults(CharacterRace race)
            => new(new[]
            {
                SourceDefault, SourceDefault, SourceDefault, SourceDefault,
                SourceDefault, SourceDefault, SourceDefault, SourceDefault,
            }, race);
    }

    /// <summary>Source limits and the audited primary-attribute subset of retail effects 64..74 and 330.</summary>
    internal static class CharacterAttributeRules
    {
        internal static int SourceMaximum(CharacterRace race, CharacterAttribute attribute)
        {
            ValidateRace(race);
            ValidateAttribute(attribute);
            return race switch
            {
                CharacterRace.Dwarf or CharacterRace.HalfOrc
                    when attribute is CharacterAttribute.Strength or CharacterAttribute.Constitution => 21,
                CharacterRace.Elf or CharacterRace.DarkElf
                    when attribute is CharacterAttribute.Dexterity or CharacterAttribute.Beauty
                         or CharacterAttribute.Willpower => 21,
                CharacterRace.HalfElf when attribute == CharacterAttribute.Dexterity => 21,
                CharacterRace.Gnome when attribute == CharacterAttribute.Willpower => 22,
                CharacterRace.Halfling when attribute == CharacterAttribute.Dexterity => 22,
                CharacterRace.HalfOgre when attribute == CharacterAttribute.Strength => 24,
                CharacterRace.Ogre when attribute == CharacterAttribute.Strength => 26,
                CharacterRace.Orc when attribute is CharacterAttribute.Strength
                    or CharacterAttribute.Constitution => 22,
                _ => CharacterAttributeSet.OrdinarySourceMaximum,
            };
        }

        internal static int RaceAdjustment(CharacterRace race, CharacterAttribute attribute)
        {
            ValidateRace(race);
            ValidateAttribute(attribute);
            return race switch
            {
                CharacterRace.Dwarf => attribute switch
                {
                    CharacterAttribute.Strength or CharacterAttribute.Constitution => 1,
                    CharacterAttribute.Dexterity or CharacterAttribute.Charisma => -1,
                    _ => 0,
                },
                CharacterRace.Elf => attribute switch
                {
                    CharacterAttribute.Dexterity or CharacterAttribute.Beauty
                        or CharacterAttribute.Willpower => 1,
                    CharacterAttribute.Constitution => -2,
                    CharacterAttribute.Strength => -1,
                    _ => 0,
                },
                CharacterRace.HalfElf => attribute switch
                {
                    CharacterAttribute.Dexterity or CharacterAttribute.Beauty => 1,
                    CharacterAttribute.Constitution => -1,
                    _ => 0,
                },
                CharacterRace.Gnome => attribute switch
                {
                    CharacterAttribute.Willpower => 2,
                    CharacterAttribute.Constitution => -2,
                    _ => 0,
                },
                CharacterRace.Halfling => attribute switch
                {
                    CharacterAttribute.Dexterity => 2,
                    CharacterAttribute.Strength => -3,
                    _ => 0,
                },
                CharacterRace.HalfOrc => attribute switch
                {
                    CharacterAttribute.Strength or CharacterAttribute.Constitution => 1,
                    CharacterAttribute.Beauty or CharacterAttribute.Charisma => -2,
                    _ => 0,
                },
                CharacterRace.HalfOgre => attribute switch
                {
                    CharacterAttribute.Strength => 4,
                    CharacterAttribute.Beauty => -1,
                    CharacterAttribute.Intelligence => -4,
                    _ => 0,
                },
                CharacterRace.DarkElf => attribute switch
                {
                    CharacterAttribute.Dexterity or CharacterAttribute.Beauty => 1,
                    _ => 0,
                },
                CharacterRace.Ogre => attribute switch
                {
                    CharacterAttribute.Strength => 6,
                    CharacterAttribute.Beauty or CharacterAttribute.Intelligence => -6,
                    _ => 0,
                },
                CharacterRace.Orc => attribute switch
                {
                    CharacterAttribute.Strength or CharacterAttribute.Constitution => 2,
                    CharacterAttribute.Beauty or CharacterAttribute.Charisma => -4,
                    CharacterAttribute.Intelligence => -1,
                    _ => 0,
                },
                _ => 0,
            };
        }

        internal static int GenderAdjustment(CharacterGender gender, CharacterAttribute attribute)
        {
            ValidateGender(gender);
            ValidateAttribute(attribute);
            if (gender != CharacterGender.Female) return 0;
            return attribute switch
            {
                CharacterAttribute.Strength => -1,
                CharacterAttribute.Constitution => 1,
                _ => 0,
            };
        }

        internal static void ValidateAttribute(CharacterAttribute attribute)
        {
            if ((int)attribute < 0 || (int)attribute >= CharacterAttributeSet.Count)
                throw new ArgumentOutOfRangeException(nameof(attribute), attribute, "Unknown source primary attribute.");
        }

        internal static void ValidateRace(CharacterRace race)
        {
            if ((int)race < (int)CharacterRace.Human || (int)race > (int)CharacterRace.Orc)
                throw new ArgumentOutOfRangeException(nameof(race), race, "Unknown source race.");
        }

        internal static void ValidateGender(CharacterGender gender)
        {
            if (gender is not CharacterGender.Female and not CharacterGender.Male)
                throw new ArgumentOutOfRangeException(nameof(gender), gender, "Unknown source gender.");
        }
    }
}
