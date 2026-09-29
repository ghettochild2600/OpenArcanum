using System;
using System.Collections.Generic;
using Arcanum.Formats.Objects;

namespace Arcanum.Runtime.Character
{
    public readonly struct CharacterReactionAdjustment
    {
        public ArcanumObjectId NpcIdentity { get; }
        public ArcanumObjectId PcIdentity { get; }
        public int Adjustment { get; }

        internal CharacterReactionAdjustment(ArcanumObjectId npcIdentity, ArcanumObjectId pcIdentity, int adjustment)
        {
            NpcIdentity = npcIdentity;
            PcIdentity = pcIdentity;
            Adjustment = adjustment;
        }
    }

    /// <summary>Authoritative deterministic M4D queries keyed by persistent character identity.</summary>
    public sealed class CharacterDerivedStatService
    {
        private static readonly int[] BeautyReactionModifiers =
            { -65, -52, -42, -33, -25, -18, -12, -7, -3, 0, 3, 7, 12, 18, 25, 33, 42, 52, 65, 75 };

        private static readonly int[,] RaceReactionModifiers =
        {
            { 0, 0, 5, 5, 0, 10, -5, -5 },
            { -5, 10, -10, -5, 0, 5, -10, -15 },
            { -10, -20, 0, 0, -10, 10, -15, -10 },
            { 0, 0, 0, 0, 0, 5, -5, -5 },
            { 0, 0, 0, 0, 5, 5, -10, 5 },
            { 0, 0, 20, 15, 0, 10, -10, -10 },
            { 0, -20, -15, -10, -10, 0, 10, 0 },
            { 0, -20, -15, -5, 20, 10, 0, 5 },
            { -30, -30, 5, -20, -30, -5, -30, -30 },
            { -10, -30, -20, -15, -20, -20, 0, -10 },
            { -20, -30, -40, -30, -20, -20, 10, 0 },
        };

        private readonly CharacterStatService _characters;
        private readonly CharacterProgressionService _progression;
        private readonly ICharacterCarryWeightProvider _carryWeight;
        private readonly Dictionary<ArcanumObjectId, PersistentCharacterDerivedState> _states = new();
        private readonly Dictionary<ReactionKey, int> _reactionAdjustments = new();

        public IReadOnlyDictionary<ArcanumObjectId, PersistentCharacterDerivedState> States => _states;
        public event Action<ArcanumObjectId, int, int> AlignmentChanged;

        public CharacterDerivedStatService(CharacterStatService characters, CharacterProgressionService progression,
            ICharacterCarryWeightProvider carryWeight = null)
        {
            _characters = characters ?? throw new ArgumentNullException(nameof(characters));
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));
            _carryWeight = carryWeight;
        }

        public PersistentCharacterDerivedState GetOrCreateDevelopmentPlayer(ArcanumObjectId identity)
            => GetOrCreate(identity, ObjectType.Pc, null, CharacterDerivedSource.DevelopmentPlayer);

        public PersistentCharacterDerivedState GetOrCreateCreatedPlayer(ArcanumObjectId identity,
            CharacterDerivedSource source)
            => GetOrCreate(identity, ObjectType.Pc, null, source);

        public PersistentCharacterDerivedState GetOrCreateSourceCharacter(ArcanumObjectId identity,
            ObjectType objectType, int prototypeNumber, CharacterDerivedSource source)
            => GetOrCreate(identity, objectType, prototypeNumber, source);

        public PersistentCharacterDerivedState Get(ArcanumObjectId identity)
            => _states.TryGetValue(identity, out PersistentCharacterDerivedState state) ? state
                : throw new KeyNotFoundException($"No authoritative character derived state exists for {identity}.");

        public bool TryGet(ArcanumObjectId identity, out PersistentCharacterDerivedState state)
            => _states.TryGetValue(identity, out state);

        internal PersistentCharacterDerivedState GetOrCreateRestored(ArcanumObjectId identity,
            ObjectType objectType, int? prototypeNumber, CharacterDerivedSource source)
            => GetOrCreate(identity, objectType, prototypeNumber, source);

        internal IReadOnlyList<CharacterReactionAdjustment> ExportReactionAdjustments()
        {
            var result = new List<CharacterReactionAdjustment>(_reactionAdjustments.Count);
            foreach (var pair in _reactionAdjustments)
                result.Add(new CharacterReactionAdjustment(pair.Key.Npc, pair.Key.Pc, pair.Value));
            result.Sort((left, right) =>
            {
                int npc = string.CompareOrdinal(left.NpcIdentity.Key, right.NpcIdentity.Key);
                return npc != 0 ? npc : string.CompareOrdinal(left.PcIdentity.Key, right.PcIdentity.Key);
            });
            return result;
        }

        internal void AddRestoredReactionAdjustment(ArcanumObjectId npc, ArcanumObjectId pc, int adjustment)
        {
            if (adjustment != 0) _reactionAdjustments.Add(new ReactionKey(npc, pc), adjustment);
        }

        public int GetDerivedStat(ArcanumObjectId identity, CharacterDerivedStat stat)
        {
            CharacterDerivedStatRules.ValidateStat(stat);
            PersistentCharacterDerivedState state = Get(identity);
            return stat switch
            {
                CharacterDerivedStat.CarryWeight => _carryWeight?.GetCarryWeight(identity)
                    ?? throw new InvalidOperationException("Carry weight requires the existing inventory-capacity authority."),
                CharacterDerivedStat.MeleeDamageBonus => GetMeleeDamageBonus(identity),
                CharacterDerivedStat.ArmorClassAdjustment => Clamp(
                    Effective(identity, CharacterAttribute.Dexterity) - 10, -9, 95),
                CharacterDerivedStat.Speed => Clamp(Effective(identity, CharacterAttribute.Dexterity)
                    + (Effective(identity, CharacterAttribute.Dexterity) >= 20 ? 5 : 0), 1, 100),
                CharacterDerivedStat.HealRate => Clamp((Effective(identity, CharacterAttribute.Constitution) + 1) / 3,
                    0, 6),
                CharacterDerivedStat.PoisonRecovery => Clamp(Effective(identity, CharacterAttribute.Constitution),
                    1, 20),
                CharacterDerivedStat.BeautyReactionModifier => GetBeautyReactionModifier(identity),
                CharacterDerivedStat.MaximumFollowers => Clamp(
                    Effective(identity, CharacterAttribute.Charisma) / 4
                    + (_progression.GetTrainingLevel(identity, CharacterSkill.Persuasion) >= SkillTrainingLevel.Expert
                        ? 1 : 0), 1, 7),
                CharacterDerivedStat.MagickTechAptitude => Clamp(
                    (50 * GetEffectiveMagickPoints(state) - 55 * GetEffectiveTechPoints(state)) / 10,
                    -100, 100),
                _ => throw new ArgumentOutOfRangeException(nameof(stat)),
            };
        }

        public int GetArmorClass(ArcanumObjectId identity)
            => Clamp(Get(identity).Source.BaseArmorClass
                     + GetDerivedStat(identity, CharacterDerivedStat.ArmorClassAdjustment), 0, 95);

        public int GetResistance(ArcanumObjectId identity, CharacterResistance resistance)
        {
            CharacterDerivedStatRules.ValidateResistance(resistance);
            PersistentCharacterDerivedState state = Get(identity);
            CharacterRace race = _characters.Get(identity).Race;
            int value = state.Source.GetResistance(resistance)
                        + CharacterDerivedStatRules.ResistanceAdjustment(race, resistance);
            if (resistance == CharacterResistance.Poison)
                value += Math.Max(0, 5 * (Effective(identity, CharacterAttribute.Constitution) - 4));
            return state.ObjectType == ObjectType.Npc && state.Source.IsMonstrous ? value : Clamp(value, 0, 95);
        }

        public int GetAlignment(ArcanumObjectId identity) => Get(identity).Alignment;

        public int SetAlignment(ArcanumObjectId identity, int alignment)
        {
            PersistentCharacterDerivedState state = Get(identity);
            int next = Clamp(alignment, CharacterDerivedStatRules.MinimumAlignment,
                CharacterDerivedStatRules.MaximumAlignment);
            int previous = state.Alignment;
            if (next == previous) return next;
            state.SetAlignment(next);
            AlignmentChanged?.Invoke(identity, previous, next);
            return next;
        }

        public int AdjustAlignment(ArcanumObjectId identity, int delta)
        {
            long next = (long)Get(identity).Alignment + delta;
            return SetAlignment(identity, next < int.MinValue ? int.MinValue
                : next > int.MaxValue ? int.MaxValue : (int)next);
        }

        public int GetStoredMagickPoints(ArcanumObjectId identity) => Get(identity).Source.MagickPoints;
        public int GetStoredTechPoints(ArcanumObjectId identity) => Get(identity).Source.TechPoints;
        public int GetEffectiveMagickPoints(ArcanumObjectId identity) => GetEffectiveMagickPoints(Get(identity));
        public int GetEffectiveTechPoints(ArcanumObjectId identity) => GetEffectiveTechPoints(Get(identity));

        internal void AddTechnologyPoint(ArcanumObjectId identity)
        {
            PersistentCharacterDerivedState state = Get(identity);
            state.SetTechnologyPointAdjustment(checked(state.TechnologyPointAdjustment + 1));
        }

        public CharacterReactionInputs GetReactionInputs(ArcanumObjectId npcIdentity, ArcanumObjectId pcIdentity)
        {
            PersistentCharacterDerivedState npc = Get(npcIdentity);
            PersistentCharacterDerivedState pc = Get(pcIdentity);
            if (npc.ObjectType != ObjectType.Npc) throw new ArgumentException("Reaction source must be an NPC.", nameof(npcIdentity));
            if (pc.ObjectType != ObjectType.Pc) throw new ArgumentException("Reaction target must be a PC.", nameof(pcIdentity));
            bool apply = !npc.Source.IsAloof && !npc.Source.IsMonstrous;
            if (!apply) return new CharacterReactionInputs(npc.Source.ReactionBase, 0, 0, false);
            CharacterRace npcRace = _characters.Get(npcIdentity).Race;
            CharacterRace pcRace = _characters.Get(pcIdentity).Race;
            if (pcRace > CharacterRace.HalfOgre)
                throw new InvalidOperationException($"{pcRace} is not a playable source reaction-matrix column.");
            return new CharacterReactionInputs(npc.Source.ReactionBase,
                GetDerivedStat(pcIdentity, CharacterDerivedStat.BeautyReactionModifier),
                RaceReactionModifiers[(int)npcRace, (int)pcRace], true);
        }

        public int GetReaction(ArcanumObjectId npcIdentity, ArcanumObjectId pcIdentity)
        {
            int initial = GetReactionInputs(npcIdentity, pcIdentity).Subtotal;
            return checked(initial + GetReactionAdjustment(npcIdentity, pcIdentity));
        }

        /// <summary>The stored pairwise source reaction level minus recomputed character-derived inputs.</summary>
        public int GetReactionAdjustment(ArcanumObjectId npcIdentity, ArcanumObjectId pcIdentity)
        {
            GetReactionInputs(npcIdentity, pcIdentity);
            return _reactionAdjustments.GetValueOrDefault(new ReactionKey(npcIdentity, pcIdentity));
        }

        public int AdjustReaction(ArcanumObjectId npcIdentity, ArcanumObjectId pcIdentity, int delta)
            => SetReaction(npcIdentity, pcIdentity, checked(GetReaction(npcIdentity, pcIdentity) + delta));

        public int SetReaction(ArcanumObjectId npcIdentity, ArcanumObjectId pcIdentity, int value)
        {
            int initial = GetReactionInputs(npcIdentity, pcIdentity).Subtotal;
            var key = new ReactionKey(npcIdentity, pcIdentity);
            int adjustment = checked(value - initial);
            if (adjustment == 0) _reactionAdjustments.Remove(key);
            else _reactionAdjustments[key] = adjustment;
            return value;
        }

        internal Snapshot CaptureSnapshot() => new(this);
        internal void RestoreSnapshot(Snapshot snapshot) => snapshot.Restore(this);

        internal sealed class Snapshot
        {
            private readonly Dictionary<ArcanumObjectId, int> _alignments = new();
            private readonly Dictionary<ReactionKey, int> _reactionAdjustments;

            internal Snapshot(CharacterDerivedStatService service)
            {
                foreach (var pair in service._states) _alignments.Add(pair.Key, pair.Value.Alignment);
                _reactionAdjustments = new Dictionary<ReactionKey, int>(service._reactionAdjustments);
            }

            internal void Restore(CharacterDerivedStatService service)
            {
                foreach (var pair in _alignments)
                    if (service._states.TryGetValue(pair.Key, out PersistentCharacterDerivedState state))
                        state.SetAlignment(pair.Value);
                service._reactionAdjustments.Clear();
                foreach (var pair in _reactionAdjustments) service._reactionAdjustments.Add(pair.Key, pair.Value);
            }
        }

        private readonly struct ReactionKey : IEquatable<ReactionKey>
        {
            private readonly ArcanumObjectId _npc;
            private readonly ArcanumObjectId _pc;

            public ReactionKey(ArcanumObjectId npc, ArcanumObjectId pc)
            {
                _npc = npc;
                _pc = pc;
            }

            public ArcanumObjectId Npc => _npc;
            public ArcanumObjectId Pc => _pc;

            public bool Equals(ReactionKey other) => _npc == other._npc && _pc == other._pc;
            public override bool Equals(object obj) => obj is ReactionKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(_npc, _pc);
        }

        private PersistentCharacterDerivedState GetOrCreate(ArcanumObjectId identity, ObjectType objectType,
            int? prototypeNumber, CharacterDerivedSource source)
        {
            PersistentCharacterState character = _characters.Get(identity);
            PersistentCharacterProgressionState progression = _progression.Get(identity);
            if (character.ObjectType != objectType || character.PrototypeNumber != prototypeNumber
                || progression.ObjectType != objectType || progression.PrototypeNumber != prototypeNumber)
                throw new InvalidOperationException($"Character/derived source mismatch for {identity}.");
            if (_states.TryGetValue(identity, out PersistentCharacterDerivedState existing))
            {
                if (!existing.MatchesSource(objectType, prototypeNumber, source))
                    throw new InvalidOperationException($"Character derived-state ObjectID collision or changed source: {identity}.");
                return existing;
            }
            var state = new PersistentCharacterDerivedState(identity, objectType, prototypeNumber, source);
            _states.Add(identity, state);
            return state;
        }

        private int Effective(ArcanumObjectId identity, CharacterAttribute attribute)
            => _characters.GetEffectiveAttribute(identity, attribute);

        private int GetMeleeDamageBonus(ArcanumObjectId identity)
        {
            int strength = Effective(identity, CharacterAttribute.Strength);
            int value = strength - 10;
            if (value < 0) value /= 2;
            if (strength >= 20) value *= 2;
            return Clamp(value, -50, 50);
        }

        private int GetBeautyReactionModifier(ArcanumObjectId identity)
        {
            int beauty = Effective(identity, CharacterAttribute.Beauty);
            int value = beauty >= 20 ? 2 * (5 * beauty - 50) : BeautyReactionModifiers[beauty - 1];
            return Clamp(value, -65, 200);
        }

        private int GetEffectiveMagickPoints(PersistentCharacterDerivedState state)
            => Clamp(state.Source.MagickPoints + CharacterDerivedStatRules.AptitudePointAdjustment(
                _characters.Get(state.Identity).Race, true), 0, CharacterDerivedStatRules.MaximumAptitudePoints);

        private int GetEffectiveTechPoints(PersistentCharacterDerivedState state)
            => Clamp(state.Source.TechPoints + state.TechnologyPointAdjustment
                + CharacterDerivedStatRules.AptitudePointAdjustment(
                _characters.Get(state.Identity).Race, false), 0, CharacterDerivedStatRules.MaximumAptitudePoints);

        private static int Clamp(int value, int minimum, int maximum) => Math.Max(minimum, Math.Min(maximum, value));
    }
}
