using System;
using System.Collections.Generic;
using Arcanum.Formats.Objects;

namespace Arcanum.Runtime.Character
{
    /// <summary>Authoritative session character registry and M4A effective-primary-stat calculator.</summary>
    public sealed class CharacterStatService
    {
        private readonly Dictionary<ArcanumObjectId, PersistentCharacterState> _states = new();
        private const string RaceOwner = "identity:race";
        private const string SexOwner = "identity:sex";

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
        public CharacterContributorService Contributors { get; } = new();
        public event Action<ArcanumObjectId> EffectiveAttributesChanged;

        public CharacterStatService()
            => Contributors.Changed += identity => EffectiveAttributesChanged?.Invoke(identity);

        public PersistentCharacterState GetOrCreateDevelopmentPlayer(ArcanumObjectId identity)
        {
            CharacterRace race = CharacterRace.Human;
            CharacterGender gender = CharacterGender.Male;
            var attributes = CharacterAttributeSet.SourceDefaults(race);
            return GetOrCreate(identity, ObjectType.Pc, null, attributes, race, gender, false);
        }

        public PersistentCharacterState GetOrCreateCreatedPlayer(ArcanumObjectId identity,
            CharacterAttributeSet attributes, CharacterRace race, CharacterGender gender)
            => GetOrCreate(identity, ObjectType.Pc, null, attributes, race, gender, true);

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
            SyncIdentityContributors(state);
        }

        public int GetBaseAttribute(ArcanumObjectId identity, CharacterAttribute attribute)
            => Get(identity).GetBase(attribute);

        public int GetEffectiveAttribute(ArcanumObjectId identity, CharacterAttribute attribute)
        {
            PersistentCharacterState state = Get(identity);
            return GetEffectiveAttributeForBase(identity, attribute, state.GetBase(attribute));
        }

        internal int GetEffectiveAttributeForBase(ArcanumObjectId identity, CharacterAttribute attribute,
            int baseValue)
        {
            PersistentCharacterState state = Get(identity);
            CharacterAttributeRules.ValidateAttribute(attribute);
            int value = Contributors.Evaluate(identity, CharacterContributorTarget.Attribute(attribute), baseValue);
            int maximum = CharacterAttributeRules.SourceMaximum(state.Race, attribute);
            return Math.Max(CharacterAttributeSet.SourceMinimum, Math.Min(maximum, value));
        }

        internal void SetBaseAttribute(ArcanumObjectId identity, CharacterAttribute attribute, int value)
        {
            Get(identity).SetBase(attribute, value);
            EffectiveAttributesChanged?.Invoke(identity);
        }

        /// <summary>Registers one inspectable non-base modifier, keyed by its owning runtime effect.</summary>
        public void SetEffectModifier(ArcanumObjectId identity, string effectIdentity,
            CharacterAttribute attribute, int amount, CharacterContributorDomain domain = CharacterContributorDomain.Other,
            int sourceId = 0)
        {
            _ = Get(identity);
            CharacterAttributeRules.ValidateAttribute(attribute);
            if (string.IsNullOrWhiteSpace(effectIdentity))
                throw new ArgumentException("Effect identity is required.", nameof(effectIdentity));
            Contributors.SetOwnerContributors(identity, effectIdentity, new[]
            {
                new CharacterContributor(effectIdentity, domain, sourceId, effectIdentity,
                    CharacterContributorTarget.Attribute(attribute), CharacterContributorOperation.Add, amount),
            });
        }

        public bool RemoveEffectModifier(ArcanumObjectId identity, string effectIdentity)
            => Contributors.RemoveOwner(identity, effectIdentity);

        /// <summary>Mirrors source race replacement: remove the old Race-caused effect, then apply 64 + race.</summary>
        public void SetRace(ArcanumObjectId identity, CharacterRace race)
        {
            PersistentCharacterState state = Get(identity);
            state.SetRace(race);
            SyncRaceContributors(state);
        }

        /// <summary>Mirrors source gender replacement: remove the old Gender effect, then apply 330 for Female.</summary>
        public void SetGender(ArcanumObjectId identity, CharacterGender gender)
        {
            PersistentCharacterState state = Get(identity);
            state.SetGender(gender);
            SyncSexContributors(state);
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
            SyncIdentityContributors(state);
            return state;
        }

        private void SyncIdentityContributors(PersistentCharacterState state)
        {
            SyncRaceContributors(state);
            SyncSexContributors(state);
        }

        private void SyncRaceContributors(PersistentCharacterState state)
        {
            int sourceId = 64 + (int)state.Race;
            var values = new List<CharacterContributor>();
            foreach (CharacterAttribute attribute in AllAttributes)
            {
                int amount = CharacterAttributeRules.RaceAdjustment(state.Race, attribute);
                if (amount != 0)
                    values.Add(Add(RaceOwner, CharacterContributorDomain.Race, sourceId,
                        $"race:{(int)state.Race}:attribute:{(int)attribute}",
                        CharacterContributorTarget.Attribute(attribute), amount, (int)attribute));
            }
            foreach (CharacterResistance resistance in CharacterDerivedStatRules.AllResistances)
            {
                int amount = CharacterDerivedStatRules.ResistanceAdjustment(state.Race, resistance);
                if (amount != 0)
                    values.Add(Add(RaceOwner, CharacterContributorDomain.Race, sourceId,
                        $"race:{(int)state.Race}:resistance:{(int)resistance}",
                        CharacterContributorTarget.Resistance(resistance), amount, 100 + (int)resistance));
            }
            int magick = CharacterDerivedStatRules.AptitudePointAdjustment(state.Race, true);
            if (magick != 0)
                values.Add(Add(RaceOwner, CharacterContributorDomain.Race, sourceId,
                    $"race:{(int)state.Race}:magick-points", CharacterContributorTarget.MagickPoints, magick, 200));
            int technology = CharacterDerivedStatRules.AptitudePointAdjustment(state.Race, false);
            if (technology != 0)
                values.Add(Add(RaceOwner, CharacterContributorDomain.Race, sourceId,
                    $"race:{(int)state.Race}:technology-points", CharacterContributorTarget.TechnologyPoints,
                    technology, 201));
            Contributors.SetOwnerContributors(state.Identity, RaceOwner, values);
        }

        private void SyncSexContributors(PersistentCharacterState state)
        {
            var values = new List<CharacterContributor>();
            if (state.Gender == CharacterGender.Female)
                foreach (CharacterAttribute attribute in AllAttributes)
                {
                    int amount = CharacterAttributeRules.GenderAdjustment(state.Gender, attribute);
                    if (amount != 0)
                        values.Add(Add(SexOwner, CharacterContributorDomain.Sex, 330,
                            $"sex:female:attribute:{(int)attribute}",
                            CharacterContributorTarget.Attribute(attribute), amount, (int)attribute));
                }
            Contributors.SetOwnerContributors(state.Identity, SexOwner, values);
        }

        private static CharacterContributor Add(string owner, CharacterContributorDomain domain, int sourceId,
            string identity, CharacterContributorTarget target, int amount, int order)
            => new(owner, domain, sourceId, identity, target, CharacterContributorOperation.Add, amount, order);
    }
}
