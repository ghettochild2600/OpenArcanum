using System;
using System.Collections.Generic;
using Arcanum.Formats.Objects;

namespace Arcanum.Runtime.Character
{
    /// <summary>Authoritative M4B HP/fatigue derivation and mutation service for persistent critters.</summary>
    public sealed class CharacterVitalityService
    {
        private readonly CharacterStatService _characters;
        private readonly Dictionary<ArcanumObjectId, PersistentCharacterVitalityState> _states = new();

        public IReadOnlyDictionary<ArcanumObjectId, PersistentCharacterVitalityState> States => _states;

        public CharacterVitalityService(CharacterStatService characters)
        {
            _characters = characters ?? throw new ArgumentNullException(nameof(characters));
            _characters.EffectiveAttributesChanged += SynchronizeAfterAttributeChange;
        }

        public PersistentCharacterVitalityState GetOrCreateDevelopmentPlayer(ArcanumObjectId identity)
            => GetOrCreate(identity, ObjectType.Pc, null, CharacterVitalitySource.DevelopmentPlayer);

        public PersistentCharacterVitalityState GetOrCreateSourceCharacter(ArcanumObjectId identity,
            ObjectType objectType, int prototypeNumber, CharacterVitalitySource source)
            => GetOrCreate(identity, objectType, prototypeNumber, source);

        public PersistentCharacterVitalityState Get(ArcanumObjectId identity)
        {
            if (!_states.TryGetValue(identity, out PersistentCharacterVitalityState state))
                throw new KeyNotFoundException($"No authoritative character vitality exists for {identity}.");
            Synchronize(state);
            return state;
        }

        public bool TryGet(ArcanumObjectId identity, out PersistentCharacterVitalityState state)
        {
            if (!_states.TryGetValue(identity, out state)) return false;
            Synchronize(state);
            return true;
        }

        public int GetMaximumHitPoints(ArcanumObjectId identity) => Get(identity).MaximumHitPoints;
        public int GetCurrentHitPoints(ArcanumObjectId identity) => Get(identity).CurrentHitPoints;
        public int GetMaximumFatigue(ArcanumObjectId identity) => Get(identity).MaximumFatigue;
        public int GetCurrentFatigue(ArcanumObjectId identity) => Get(identity).CurrentFatigue;

        public void ApplyHitPointDamage(ArcanumObjectId identity, int amount)
        {
            ValidateAmount(amount);
            PersistentCharacterVitalityState state = Get(identity);
            int damage = checked(state.HitPointDamage + amount);
            state.SetDamage(damage, state.FatigueDamage);
        }

        public void RestoreHitPoints(ArcanumObjectId identity, int amount)
        {
            ValidateAmount(amount);
            PersistentCharacterVitalityState state = Get(identity);
            int damage = amount >= state.HitPointDamage ? 0 : state.HitPointDamage - amount;
            state.SetDamage(damage, state.FatigueDamage);
        }

        public void ApplyFatigueDamage(ArcanumObjectId identity, int amount)
        {
            ValidateAmount(amount);
            PersistentCharacterVitalityState state = Get(identity);
            int damage = checked(state.FatigueDamage + amount);
            state.SetDamage(state.HitPointDamage, damage);
        }

        public void RestoreFatigue(ArcanumObjectId identity, int amount)
        {
            ValidateAmount(amount);
            PersistentCharacterVitalityState state = Get(identity);
            int damage = amount >= state.FatigueDamage ? 0 : state.FatigueDamage - amount;
            state.SetDamage(state.HitPointDamage, damage);
        }

        private PersistentCharacterVitalityState GetOrCreate(ArcanumObjectId identity, ObjectType objectType,
            int? prototypeNumber, CharacterVitalitySource source)
        {
            PersistentCharacterState character = _characters.Get(identity);
            if (character.ObjectType != objectType || character.PrototypeNumber != prototypeNumber)
                throw new InvalidOperationException($"Character/vitality source mismatch for {identity}.");
            if (_states.TryGetValue(identity, out PersistentCharacterVitalityState existing))
            {
                if (!existing.MatchesSource(objectType, prototypeNumber, source))
                    throw new InvalidOperationException($"Character vitality ObjectID collision or changed source: {identity}.");
                Synchronize(existing);
                return existing;
            }

            int maximumHitPoints = CalculateMaximumHitPoints(identity, source);
            int maximumFatigue = CalculateMaximumFatigue(identity, source);
            var state = new PersistentCharacterVitalityState(identity, objectType, prototypeNumber, source,
                maximumHitPoints, maximumFatigue);
            _states.Add(identity, state);
            return state;
        }

        private int CalculateMaximumHitPoints(ArcanumObjectId identity, CharacterVitalitySource source)
        {
            int strength = _characters.GetEffectiveAttribute(identity, CharacterAttribute.Strength);
            int willpower = _characters.GetEffectiveAttribute(identity, CharacterAttribute.Willpower);
            return checked(4 * source.HitPointPoints + source.HitPointAdjustment + willpower
                + 2 * checked(strength + source.Level) + 4);
        }

        private int CalculateMaximumFatigue(ArcanumObjectId identity, CharacterVitalitySource source)
        {
            int constitution = _characters.GetEffectiveAttribute(identity, CharacterAttribute.Constitution);
            int willpower = _characters.GetEffectiveAttribute(identity, CharacterAttribute.Willpower);
            return checked(4 * source.FatiguePoints + source.FatigueAdjustment
                + 2 * checked(source.Level + constitution) + willpower + 4);
        }

        private void SynchronizeAfterAttributeChange(ArcanumObjectId identity)
        {
            if (_states.TryGetValue(identity, out PersistentCharacterVitalityState state)) Synchronize(state);
        }

        private void Synchronize(PersistentCharacterVitalityState state)
        {
            int maximumHitPoints = CalculateMaximumHitPoints(state.Identity, state.Source);
            int maximumFatigue = CalculateMaximumFatigue(state.Identity, state.Source);
            if (maximumHitPoints == state.MaximumHitPoints && maximumFatigue == state.MaximumFatigue) return;

            int hitPointDelta = checked(maximumHitPoints - state.MaximumHitPoints);
            int fatigueDelta = checked(maximumFatigue - state.MaximumFatigue);
            int hitPointDamage = checked(state.HitPointDamage + hitPointDelta);
            int fatigueDamage = checked(state.FatigueDamage + fatigueDelta);
            if (hitPointDamage < 0) hitPointDamage = 0;
            if (fatigueDamage < 0) fatigueDamage = 0;
            state.SynchronizeMaxima(maximumHitPoints, maximumFatigue, hitPointDamage, fatigueDamage);
        }

        private static void ValidateAmount(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        }
    }
}
