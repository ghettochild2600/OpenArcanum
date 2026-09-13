using System;
using Arcanum.Formats.Objects;

namespace Arcanum.Runtime.Character
{
    /// <summary>Session-owned source inputs and mutable alignment, independent of Unity presentation.</summary>
    public sealed class PersistentCharacterDerivedState
    {
        public ArcanumObjectId Identity { get; }
        public ObjectType ObjectType { get; }
        public int? PrototypeNumber { get; }
        public CharacterDerivedSource Source { get; }
        public int Alignment { get; private set; }

        internal PersistentCharacterDerivedState(ArcanumObjectId identity, ObjectType objectType,
            int? prototypeNumber, CharacterDerivedSource source)
        {
            if (!identity.IsPersistent)
                throw new ArgumentException("Character derived-state identity must be persistent.", nameof(identity));
            if (objectType is not ObjectType.Pc and not ObjectType.Npc)
                throw new ArgumentException($"{objectType} cannot own character derived state.", nameof(objectType));
            Identity = identity;
            ObjectType = objectType;
            PrototypeNumber = prototypeNumber;
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Alignment = source.Alignment;
        }

        internal bool MatchesSource(ObjectType objectType, int? prototypeNumber, CharacterDerivedSource source)
            => ObjectType == objectType && PrototypeNumber == prototypeNumber && Source.Equals(source);

        internal void SetAlignment(int alignment) => Alignment = alignment;
    }
}
