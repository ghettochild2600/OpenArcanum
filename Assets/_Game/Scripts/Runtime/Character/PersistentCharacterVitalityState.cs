using System;
using Arcanum.Formats.Objects;

namespace Arcanum.Runtime.Character
{
    /// <summary>Authoritative session-owned HP/fatigue state, independent of Unity presentation objects.</summary>
    public sealed class PersistentCharacterVitalityState
    {
        public ArcanumObjectId Identity { get; }
        public ObjectType ObjectType { get; }
        public int? PrototypeNumber { get; }
        public CharacterVitalitySource Source { get; }
        public int HitPointDamage { get; private set; }
        public int FatigueDamage { get; private set; }
        public int MaximumHitPoints { get; private set; }
        public int MaximumFatigue { get; private set; }
        public int CurrentHitPoints => checked(MaximumHitPoints - HitPointDamage);
        public int CurrentFatigue => checked(MaximumFatigue - FatigueDamage);

        internal PersistentCharacterVitalityState(ArcanumObjectId identity, ObjectType objectType,
            int? prototypeNumber, CharacterVitalitySource source, int maximumHitPoints, int maximumFatigue)
        {
            if (!identity.IsPersistent)
                throw new ArgumentException("Character vitality identity must be persistent.", nameof(identity));
            if (objectType is not ObjectType.Pc and not ObjectType.Npc)
                throw new ArgumentException($"{objectType} cannot own character vitality.", nameof(objectType));
            Identity = identity;
            ObjectType = objectType;
            PrototypeNumber = prototypeNumber;
            Source = source;
            HitPointDamage = source.HitPointDamage;
            FatigueDamage = source.FatigueDamage;
            MaximumHitPoints = maximumHitPoints;
            MaximumFatigue = maximumFatigue;
        }

        internal bool MatchesSource(ObjectType objectType, int? prototypeNumber, CharacterVitalitySource source)
            => ObjectType == objectType && PrototypeNumber == prototypeNumber && Source.Equals(source);

        internal void SetDamage(int hitPointDamage, int fatigueDamage)
        {
            HitPointDamage = hitPointDamage;
            FatigueDamage = fatigueDamage;
        }

        internal void SynchronizeMaxima(int maximumHitPoints, int maximumFatigue, int hitPointDamage,
            int fatigueDamage)
        {
            MaximumHitPoints = maximumHitPoints;
            MaximumFatigue = maximumFatigue;
            HitPointDamage = hitPointDamage;
            FatigueDamage = fatigueDamage;
        }
    }
}
