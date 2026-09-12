namespace Arcanum.Runtime.Character
{
    /// <summary>The eight primary <c>Stat</c> slots from the source engine. Numeric values are source IDs.</summary>
    public enum CharacterAttribute
    {
        Strength = 0,
        Dexterity = 1,
        Constitution = 2,
        Beauty = 3,
        Intelligence = 4,
        Perception = 5,
        Willpower = 6,
        Charisma = 7,
    }

    /// <summary>Source <c>Gender</c> values stored in stat slot 26.</summary>
    public enum CharacterGender
    {
        Female = 0,
        Male = 1,
    }

    /// <summary>All source <c>Race</c> values stored in stat slot 27, including NPC-only races.</summary>
    public enum CharacterRace
    {
        Human = 0,
        Dwarf = 1,
        Elf = 2,
        HalfElf = 3,
        Gnome = 4,
        Halfling = 5,
        HalfOrc = 6,
        HalfOgre = 7,
        DarkElf = 8,
        Ogre = 9,
        Orc = 10,
    }
}
