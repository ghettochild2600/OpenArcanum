using System;
using Arcanum.Formats.Objects;

namespace Arcanum.Runtime.Character
{
    /// <summary>Session-owned character-domain state. Unity objects are disposable projections of this state.</summary>
    public sealed class PersistentCharacterState
    {
        private readonly CharacterRace _sourceRace;
        private readonly CharacterGender _sourceGender;

        public ArcanumObjectId Identity { get; }
        public ObjectType ObjectType { get; }
        public int? PrototypeNumber { get; }
        public bool HasInstanceStatOverride { get; }
        public CharacterAttributeSet BaseAttributes { get; }
        public CharacterRace Race { get; private set; }
        public CharacterGender Gender { get; private set; }

        internal PersistentCharacterState(ArcanumObjectId identity, ObjectType objectType, int? prototypeNumber,
            CharacterAttributeSet baseAttributes, CharacterRace race, CharacterGender gender,
            bool hasInstanceStatOverride)
        {
            if (!identity.IsPersistent)
                throw new ArgumentException("Character identity must be persistent.", nameof(identity));
            if (objectType is not ObjectType.Pc and not ObjectType.Npc)
                throw new ArgumentException($"{objectType} is not a source critter character type.", nameof(objectType));
            Identity = identity;
            ObjectType = objectType;
            PrototypeNumber = prototypeNumber;
            BaseAttributes = baseAttributes ?? throw new ArgumentNullException(nameof(baseAttributes));
            CharacterAttributeRules.ValidateRace(race);
            CharacterAttributeRules.ValidateGender(gender);
            _sourceRace = Race = race;
            _sourceGender = Gender = gender;
            HasInstanceStatOverride = hasInstanceStatOverride;
        }

        public int GetBase(CharacterAttribute attribute) => BaseAttributes.Get(attribute);

        public int GetEffective(CharacterAttribute attribute)
        {
            CharacterAttributeRules.ValidateAttribute(attribute);
            int value = GetBase(attribute)
                        + CharacterAttributeRules.RaceAdjustment(Race, attribute)
                        + CharacterAttributeRules.GenderAdjustment(Gender, attribute);
            int maximum = CharacterAttributeRules.SourceMaximum(Race, attribute);
            return Math.Max(CharacterAttributeSet.SourceMinimum, Math.Min(maximum, value));
        }

        internal void SetRace(CharacterRace race)
        {
            CharacterAttributeRules.ValidateRace(race);
            Race = race;
        }

        internal void SetGender(CharacterGender gender)
        {
            CharacterAttributeRules.ValidateGender(gender);
            Gender = gender;
        }

        internal bool MatchesSource(ObjectType objectType, int? prototypeNumber, CharacterAttributeSet attributes,
            CharacterRace race, CharacterGender gender, bool hasInstanceStatOverride)
        {
            if (ObjectType != objectType || PrototypeNumber != prototypeNumber || _sourceRace != race
                || _sourceGender != gender || HasInstanceStatOverride != hasInstanceStatOverride)
                return false;
            foreach (CharacterAttribute attribute in CharacterStatService.AllAttributes)
                if (BaseAttributes.Get(attribute) != attributes.Get(attribute)) return false;
            return true;
        }
    }
}
