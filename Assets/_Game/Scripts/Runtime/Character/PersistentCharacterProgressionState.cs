using System;
using Arcanum.Formats.Objects;

namespace Arcanum.Runtime.Character
{
    /// <summary>Authoritative session-owned XP, level, point, skill and training state.</summary>
    public sealed class PersistentCharacterProgressionState
    {
        private readonly int[] _purchasedPoints = new int[CharacterSkillRules.SkillCount];
        private readonly SkillTrainingLevel[] _training = new SkillTrainingLevel[CharacterSkillRules.SkillCount];

        public ArcanumObjectId Identity { get; }
        public ObjectType ObjectType { get; }
        public int? PrototypeNumber { get; }
        public CharacterProgressionSource Source { get; }
        public int Experience { get; private set; }
        public int Level { get; private set; }
        public int UnspentCharacterPoints { get; private set; }

        internal PersistentCharacterProgressionState(ArcanumObjectId identity, ObjectType objectType,
            int? prototypeNumber, CharacterProgressionSource source)
        {
            if (!identity.IsPersistent)
                throw new ArgumentException("Character progression identity must be persistent.", nameof(identity));
            if (objectType is not ObjectType.Pc and not ObjectType.Npc)
                throw new ArgumentException($"{objectType} cannot own character progression.", nameof(objectType));
            Identity = identity;
            ObjectType = objectType;
            PrototypeNumber = prototypeNumber;
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Experience = source.Experience;
            Level = source.Level;
            UnspentCharacterPoints = source.UnspentCharacterPoints;
            foreach (CharacterSkill skill in CharacterSkillRules.AllSkills)
            {
                int packed = source.GetPacked(skill);
                _purchasedPoints[(int)skill] = packed & 63;
                _training[(int)skill] = (SkillTrainingLevel)((packed >> 6) & 3);
            }
        }

        public int GetPurchasedPoints(CharacterSkill skill)
        {
            CharacterSkillRules.ValidateSkill(skill);
            return _purchasedPoints[(int)skill];
        }

        public SkillTrainingLevel GetStoredTraining(CharacterSkill skill)
        {
            CharacterSkillRules.ValidateSkill(skill);
            return _training[(int)skill];
        }

        internal bool MatchesSource(ObjectType objectType, int? prototypeNumber, CharacterProgressionSource source)
            => ObjectType == objectType && PrototypeNumber == prototypeNumber && Source.Equals(source);

        internal void SetProgression(int experience, int level, int unspentCharacterPoints)
        {
            Experience = experience;
            Level = level;
            UnspentCharacterPoints = unspentCharacterPoints;
        }

        internal void IncreaseSkill(CharacterSkill skill)
        {
            _purchasedPoints[(int)skill]++;
            UnspentCharacterPoints--;
        }

        internal void SetTraining(CharacterSkill skill, SkillTrainingLevel training)
            => _training[(int)skill] = training;

        internal int[] CopyPurchasedPoints() => (int[])_purchasedPoints.Clone();
        internal SkillTrainingLevel[] CopyTraining() => (SkillTrainingLevel[])_training.Clone();

        internal void RestoreSkills(int[] purchasedPoints, SkillTrainingLevel[] training)
        {
            Array.Copy(purchasedPoints, _purchasedPoints, _purchasedPoints.Length);
            Array.Copy(training, _training, _training.Length);
        }
    }
}
