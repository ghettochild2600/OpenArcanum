using System;
using System.Collections.Generic;
using Arcanum.Formats.Objects;

namespace Arcanum.Runtime.Character
{
    /// <summary>Authoritative session character registry and M4A effective-primary-stat calculator.</summary>
    public sealed class CharacterStatService
    {
        private readonly Dictionary<ArcanumObjectId, PersistentCharacterState> _states = new();

        public static IReadOnlyList<CharacterAttribute> AllAttributes { get; } = Array.AsReadOnly(new[]
            {
                CharacterAttribute.Strength,
                CharacterAttribute.Dexterity,
                CharacterAttribute.Constitution,
                CharacterAttribute.Beauty,
                CharacterAttribute.Intelligence,
                CharacterAttribute.Perception,
                CharacterAttribute.Willpower,
                CharacterAttribute.Charisma,
            });

        public IReadOnlyDictionary<ArcanumObjectId, PersistentCharacterState> States => _states;
        public event Action<ArcanumObjectId> EffectiveAttributesChanged;

        public PersistentCharacterState GetOrCreateDevelopmentPlayer(ArcanumObjectId identity)
        {
            CharacterRace race = CharacterRace.Human;
            CharacterGender gender = CharacterGender.Male;
            var attributes = CharacterAttributeSet.SourceDefaults(race);
            return GetOrCreate(identity, ObjectType.Pc, null, attributes, race, gender, false);
        }

        public PersistentCharacterState GetOrCreateSourceCharacter(ArcanumObjectId identity, ObjectType objectType,
            int prototypeNumber, int[] instanceStatBase, int[] prototypeStatBase)
        {
            if (objectType is not ObjectType.Pc and not ObjectType.Npc)
                throw new ArgumentException($"{objectType} cannot own character attributes.", nameof(objectType));
            int[] source = instanceStatBase ?? prototypeStatBase
                ?? throw new InvalidOperationException($"Character {identity} has no source stat-base array.");
            CharacterAttributeSet attributes = CharacterAttributeSet.FromSourceStatArray(source,
                out CharacterRace race, out CharacterGender gender);
            return GetOrCreate(identity, objectType, prototypeNumber, attributes, race, gender,
                instanceStatBase != null);
        }

        public PersistentCharacterState Get(ArcanumObjectId identity)
            => _states.TryGetValue(identity, out PersistentCharacterState state)
                ? state
                : throw new KeyNotFoundException($"No authoritative character state exists for {identity}.");

        public bool TryGet(ArcanumObjectId identity, out PersistentCharacterState state)
            => _states.TryGetValue(identity, out state);

        internal void AddRestored(PersistentCharacterState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            _states.Add(state.Identity, state);
        }

        public int GetBaseAttribute(ArcanumObjectId identity, CharacterAttribute attribute)
            => Get(identity).GetBase(attribute);

        public int GetEffectiveAttribute(ArcanumObjectId identity, CharacterAttribute attribute)
            => Get(identity).GetEffective(attribute);

        /// <summary>Mirrors source race replacement: remove the old Race-caused effect, then apply 64 + race.</summary>
        public void SetRace(ArcanumObjectId identity, CharacterRace race)
        {
            Get(identity).SetRace(race);
            EffectiveAttributesChanged?.Invoke(identity);
        }

        /// <summary>Mirrors source gender replacement: remove the old Gender effect, then apply 330 for Female.</summary>
        public void SetGender(ArcanumObjectId identity, CharacterGender gender)
        {
            Get(identity).SetGender(gender);
            EffectiveAttributesChanged?.Invoke(identity);
        }

        private PersistentCharacterState GetOrCreate(ArcanumObjectId identity, ObjectType objectType,
            int? prototypeNumber, CharacterAttributeSet attributes, CharacterRace race, CharacterGender gender,
            bool hasInstanceStatOverride)
        {
            if (_states.TryGetValue(identity, out PersistentCharacterState existing))
            {
                if (!existing.MatchesSource(objectType, prototypeNumber, attributes, race, gender,
                        hasInstanceStatOverride))
                    throw new InvalidOperationException($"Character ObjectID collision or changed source: {identity}.");
                return existing;
            }
            var state = new PersistentCharacterState(identity, objectType, prototypeNumber, attributes, race, gender,
                hasInstanceStatOverride);
            _states.Add(identity, state);
            return state;
        }
    }
}
