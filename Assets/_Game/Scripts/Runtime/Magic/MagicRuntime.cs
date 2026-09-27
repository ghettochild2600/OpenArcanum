using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.World;
using UnityEngine;

namespace Arcanum.Runtime.Magic
{
    public enum SpellCollege
    {
        Conveyance, Divination, Air, Earth, Fire, Water, Force, Mental,
        Meta, Morph, Nature, NecromanticBlack, NecromanticWhite, Phantasm, Summoning, Temporal,
    }

    public enum SpellTargetClass { LivingCritter, DamagedLivingCritter }
    public enum SpellEffectFamily { AttributeModifier, DirectDamage, Healing, CritterFlag }
    public enum SpellDisposition { Friendly, Aggressive }

    public sealed class SpellDefinition
    {
        public int Id { get; }
        public string Name { get; }
        public SpellCollege College { get; }
        public int Rank { get; }
        public int MinimumLevel { get; }
        public int WillpowerRequirement { get; }
        public SpellTargetClass TargetClass { get; }
        public SpellEffectFamily EffectFamily { get; }
        public SpellDisposition Disposition { get; }
        public int BaseFatigueCost { get; }
        public int ActionPointCost { get; }
        public int Range { get; }
        public bool AllowsSelf { get; }
        public bool Maintained { get; }
        public int UpkeepFatigueCost { get; }
        public int UpkeepPeriodMilliseconds { get; }
        public int MinimumMagnitude { get; }
        public int MaximumMagnitude { get; }
        public CharacterAttribute? ModifiedAttribute { get; }
        public int AttributeMagnitude { get; }
        public int RuntimeCritterFlag { get; }
        public int DurationSourceMilliseconds { get; }
        public bool NoStack { get; }
        public CharacterAttribute? ResistanceAttribute { get; }
        public int ResistanceModifier { get; }

        public SpellDefinition(int id, string name, SpellTargetClass targetClass,
            SpellEffectFamily effectFamily, SpellDisposition disposition, int baseFatigueCost,
            int actionPointCost, int range, bool allowsSelf, int minimumMagnitude = 0,
            int maximumMagnitude = 0, bool maintained = false, int upkeepFatigueCost = 0,
            int upkeepPeriodMilliseconds = 0, CharacterAttribute? modifiedAttribute = null,
            int attributeMagnitude = 0, int runtimeCritterFlag = 0,
            int durationSourceMilliseconds = 0, bool noStack = false,
            CharacterAttribute? resistanceAttribute = null, int resistanceModifier = 0)
        {
            if (id < 0 || id >= 80) throw new ArgumentOutOfRangeException(nameof(id));
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            College = (SpellCollege)(id / 5);
            Rank = id % 5 + 1;
            MinimumLevel = new[] { 1, 1, 5, 10, 15 }[Rank - 1];
            WillpowerRequirement = new[] { 6, 9, 12, 15, 18 }[Rank - 1];
            TargetClass = targetClass;
            EffectFamily = effectFamily;
            Disposition = disposition;
            BaseFatigueCost = baseFatigueCost;
            ActionPointCost = actionPointCost;
            Range = range;
            AllowsSelf = allowsSelf;
            Maintained = maintained;
            UpkeepFatigueCost = upkeepFatigueCost;
            UpkeepPeriodMilliseconds = upkeepPeriodMilliseconds;
            MinimumMagnitude = minimumMagnitude;
            MaximumMagnitude = maximumMagnitude;
            ModifiedAttribute = modifiedAttribute;
            AttributeMagnitude = attributeMagnitude;
            RuntimeCritterFlag = runtimeCritterFlag;
            DurationSourceMilliseconds = durationSourceMilliseconds;
            NoStack = noStack;
            ResistanceAttribute = resistanceAttribute;
            ResistanceModifier = resistanceModifier;
        }
    }

    public static class PhaseOneSpellCatalog
    {
        public const int StrengthOfEarth = 15;
        public const int Harm = 55;
        public const int MinorHealing = 60;
        public const int Flash = 66;
        public const int SourceDefaultRange = 99;
        // AG_THROW_SPELL consumes four action points before beginning the spell.
        public const int SourceSpellActionPointCost = 4;

        private static readonly IReadOnlyDictionary<int, SpellDefinition> Definitions =
            new Dictionary<int, SpellDefinition>
            {
                [StrengthOfEarth] = new(StrengthOfEarth, "Strength of Earth",
                    SpellTargetClass.LivingCritter, SpellEffectFamily.AttributeModifier,
                    SpellDisposition.Friendly, 5, SourceSpellActionPointCost, SourceDefaultRange, true,
                    maintained: true, upkeepFatigueCost: 1, upkeepPeriodMilliseconds: 80_000,
                    modifiedAttribute: CharacterAttribute.Strength, attributeMagnitude: 4),
                [Harm] = new(Harm, "Harm", SpellTargetClass.LivingCritter,
                    SpellEffectFamily.DirectDamage, SpellDisposition.Aggressive, 5,
                    SourceSpellActionPointCost, SourceDefaultRange, false, 3, 40),
                [MinorHealing] = new(MinorHealing, "Minor Healing",
                    SpellTargetClass.DamagedLivingCritter, SpellEffectFamily.Healing,
                    SpellDisposition.Friendly, 5, SourceSpellActionPointCost,
                    SourceDefaultRange, true, 5, 30),
                [Flash] = new(Flash, "Flash", SpellTargetClass.LivingCritter,
                    SpellEffectFamily.CritterFlag, SpellDisposition.Aggressive, 10,
                    SourceSpellActionPointCost, SourceDefaultRange, true,
                    runtimeCritterFlag: 0x00000080, durationSourceMilliseconds: 80_000,
                    noStack: true, resistanceAttribute: CharacterAttribute.Constitution,
                    resistanceModifier: -5),
            };

        public static IEnumerable<SpellDefinition> All => Definitions.Values;
        public static bool TryGet(int id, out SpellDefinition definition) => Definitions.TryGetValue(id, out definition);
    }

    public readonly struct SpellCastRequest
    {
        public ArcanumObjectId Caster { get; }
        public int SpellId { get; }
        public ArcanumObjectId Target { get; }

        public SpellCastRequest(ArcanumObjectId caster, int spellId, ArcanumObjectId target)
        {
            Caster = caster;
            SpellId = spellId;
            Target = target;
        }
    }

    public enum SpellCastFailure
    {
        None, InvalidSpell, InvalidCaster, SpellNotKnown, PrerequisiteNotMet,
        CasterUnavailable, InsufficientFatigue, MaintainSlotUnavailable,
        InvalidTarget, TargetUnavailable, OutOfRange, LineOfSightBlocked,
        DuplicateEffect, CombatRejected, RealTimeSchedulingRequired,
    }

    public readonly struct SpellCastResult
    {
        public bool Succeeded => Failure == SpellCastFailure.None;
        public SpellCastFailure Failure { get; }
        public SpellCastRequest Request { get; }
        public int FatigueCost { get; }
        public int ActionPointCost { get; }
        public int Magnitude { get; }
        public int ResistancePercent { get; }
        public long? ActiveEffectId { get; }

        internal SpellCastResult(SpellCastFailure failure, SpellCastRequest request,
            int fatigueCost = 0, int actionPointCost = 0, int magnitude = 0,
            int resistancePercent = 0, long? activeEffectId = null)
        {
            Failure = failure;
            Request = request;
            FatigueCost = fatigueCost;
            ActionPointCost = actionPointCost;
            Magnitude = magnitude;
            ResistancePercent = resistancePercent;
            ActiveEffectId = activeEffectId;
        }
    }

    public sealed class ActiveSpellEffect
    {
        public long Id { get; }
        public int SpellId { get; }
        public ArcanumObjectId Caster { get; }
        public ArcanumObjectId Target { get; }
        public int Magnitude { get; }
        public long StartedAtMilliseconds { get; }
        public long NextUpkeepAtMilliseconds { get; internal set; }
        public long ExpiresAtMilliseconds { get; }
        public string ModifierIdentity => $"magic:{Id}";

        internal ActiveSpellEffect(long id, int spellId, ArcanumObjectId caster,
            ArcanumObjectId target, int magnitude, long startedAtMilliseconds,
            long nextUpkeepAtMilliseconds, long expiresAtMilliseconds = 0)
        {
            Id = id;
            SpellId = spellId;
            Caster = caster;
            Target = target;
            Magnitude = magnitude;
            StartedAtMilliseconds = startedAtMilliseconds;
            NextUpkeepAtMilliseconds = nextUpkeepAtMilliseconds;
            ExpiresAtMilliseconds = expiresAtMilliseconds;
        }
    }

    public enum SpellEffectTerminationReason
    {
        NaturalExpiration,
        CasterCancellation,
        Dispelled,
        UpkeepFailure,
        InvalidParticipant,
    }

    public readonly struct SpellEffectTermination
    {
        public long EffectId { get; }
        public int SpellId { get; }
        public SpellEffectTerminationReason Reason { get; }
        public long EndedAtMilliseconds { get; }

        internal SpellEffectTermination(long effectId, int spellId,
            SpellEffectTerminationReason reason, long endedAtMilliseconds)
        {
            EffectId = effectId;
            SpellId = spellId;
            Reason = reason;
            EndedAtMilliseconds = endedAtMilliseconds;
        }
    }

    public interface IMagicRandom { int Next(int minimumInclusive, int maximumExclusive); }

    public sealed class MagicStateService
    {
        private sealed class SystemMagicRandom : IMagicRandom
        {
            private readonly System.Random _random = new();
            public int Next(int minimumInclusive, int maximumExclusive) => _random.Next(minimumInclusive, maximumExclusive);
        }

        private readonly WorldMapSessionCoordinator _world;
        private readonly Dictionary<ArcanumObjectId, int[]> _collegeRanks = new();
        private readonly Dictionary<ArcanumObjectId, int> _masteries = new();
        private readonly HashSet<ArcanumObjectId> _restoredCharacters = new();
        private readonly List<ActiveSpellEffect> _activeEffects = new();
        private IMagicRandom _random = new SystemMagicRandom();
        private long _nextEffectId = 1;

        public IReadOnlyList<ActiveSpellEffect> ActiveEffects => _activeEffects;
        public long ElapsedMilliseconds => _world.SourceTime.ElapsedMilliseconds;
        public SpellEffectTermination? LastTermination { get; private set; }

        public MagicStateService(WorldMapSessionCoordinator world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _world.SourceTime.Advanced += OnSourceTimeAdvanced;
        }

        public void SetRandomSource(IMagicRandom random) => _random = random ?? throw new ArgumentNullException(nameof(random));
        public void ResetRandomSource() => _random = new SystemMagicRandom();

        public void RegisterSourceCharacter(ArcanumObjectId identity, int[] instanceSpellTech,
            int[] prototypeSpellTech)
        {
            // Sector presentation rebuilds re-register source objects. Restored authoritative
            // knowledge must win over those immutable source defaults.
            if (_restoredCharacters.Contains(identity)) return;
            int[] source = instanceSpellTech ?? prototypeSpellTech ?? new int[17];
            if (source.Length < 17) throw new InvalidOperationException($"Character {identity} spell-tech source is incomplete.");
            var ranks = new int[16];
            for (int index = 0; index < ranks.Length; index++) ranks[index] = Math.Max(0, Math.Min(5, source[index]));
            _collegeRanks[identity] = ranks;
            _masteries[identity] = source[16] >= 0 && source[16] < 16 ? source[16] : -1;
        }

        public MagicSaveData ExportSaveData()
            => new()
            {
                ElapsedMilliseconds = ElapsedMilliseconds,
                NextEffectId = _nextEffectId,
                Characters = _collegeRanks.OrderBy(value => value.Key.Key, StringComparer.Ordinal)
                    .Select(value => new MagicCharacterSaveData
                    {
                        Identity = value.Key.Key,
                        CollegeRanks = (int[])value.Value.Clone(),
                        MasteryCollege = _masteries.TryGetValue(value.Key, out int mastery) ? mastery : -1,
                    }).ToList(),
                ActiveEffects = _activeEffects.OrderBy(value => value.Id)
                    .Select(value => new ActiveSpellEffectSaveData
                    {
                        Id = value.Id,
                        SpellId = value.SpellId,
                        CasterIdentity = value.Caster.Key,
                        TargetIdentity = value.Target.Key,
                        Magnitude = value.Magnitude,
                        StartedAtMilliseconds = value.StartedAtMilliseconds,
                        NextUpkeepAtMilliseconds = value.NextUpkeepAtMilliseconds,
                        ExpiresAtMilliseconds = value.ExpiresAtMilliseconds,
                    }).ToList(),
            };

        internal void RestoreSaveData(MagicSaveData data)
        {
            if (data == null) return;
            _nextEffectId = data.NextEffectId;
            foreach (MagicCharacterSaveData value in data.Characters)
            {
                ArcanumObjectId.TryParsePersistent(value.Identity, out ArcanumObjectId identity);
                _collegeRanks.Add(identity, (int[])value.CollegeRanks.Clone());
                _masteries.Add(identity, value.MasteryCollege);
                _restoredCharacters.Add(identity);
            }
            foreach (ActiveSpellEffectSaveData value in data.ActiveEffects)
            {
                ArcanumObjectId.TryParsePersistent(value.CasterIdentity, out ArcanumObjectId caster);
                ArcanumObjectId.TryParsePersistent(value.TargetIdentity, out ArcanumObjectId target);
                var effect = new ActiveSpellEffect(value.Id, value.SpellId, caster, target, value.Magnitude,
                    value.StartedAtMilliseconds, value.NextUpkeepAtMilliseconds,
                    value.ExpiresAtMilliseconds);
                PhaseOneSpellCatalog.TryGet(value.SpellId, out SpellDefinition spell);
                if (effect.ExpiresAtMilliseconds > 0 && effect.ExpiresAtMilliseconds <= ElapsedMilliseconds)
                    continue;
                ApplyEffect(effect, spell);
                _activeEffects.Add(effect);
            }
        }

        public void SetKnownCollegeRank(ArcanumObjectId identity, SpellCollege college, int rank)
        {
            if (!_world.Characters.TryGet(identity, out _)) throw new KeyNotFoundException($"No character {identity}.");
            if (rank < 0 || rank > 5) throw new ArgumentOutOfRangeException(nameof(rank));
            if (!_collegeRanks.TryGetValue(identity, out int[] ranks))
                _collegeRanks.Add(identity, ranks = new int[16]);
            ranks[(int)college] = rank;
        }

        public void SetMastery(ArcanumObjectId identity, SpellCollege? college)
            => _masteries[identity] = college.HasValue ? (int)college.Value : -1;

        public bool KnowsSpell(ArcanumObjectId identity, int spellId)
            => PhaseOneSpellCatalog.TryGet(spellId, out SpellDefinition spell)
               && _collegeRanks.TryGetValue(identity, out int[] ranks)
               && ranks[(int)spell.College] >= spell.Rank;

        public SpellCastResult Cast(SpellCastRequest request) => CastInternal(request, false);

        internal SpellCastResult ResolveScheduledCast(SpellCastRequest request) => CastInternal(request, true);

        internal SpellCastResult PreviewScheduledCast(SpellCastRequest request) => PreviewInternal(request, true);

        private SpellCastResult CastInternal(SpellCastRequest request, bool scheduledRealTime)
        {
            SpellCastResult preview = PreviewInternal(request, scheduledRealTime);
            if (!preview.Succeeded) return preview;
            PhaseOneSpellCatalog.TryGet(request.SpellId, out SpellDefinition spell);
            int resistance = spell.Disposition == SpellDisposition.Aggressive
                ? ResolveResistance(request.Caster, request.Target) : 0;
            int magnitude = ScaledMagnitude(spell, request.Caster);
            if (spell.EffectFamily == SpellEffectFamily.DirectDamage && resistance > 0)
                magnitude = Math.Max(1, magnitude * (100 - resistance) / 100);
            bool resisted = spell.ResistanceAttribute.HasValue
                            && ResolveSavingThrow(spell, request.Caster, request.Target, resistance);

            _world.Vitality.ApplyFatigueDamage(request.Caster, preview.FatigueCost);
            if (_world.Combat.IsActive && _world.Combat.Mode == CombatMode.TurnBased)
                _world.Combat.CommitTurnBasedSpellAction(request.Caster, spell.ActionPointCost);

            long? effectId = null;
            if (resisted)
                return new SpellCastResult(SpellCastFailure.None, request, preview.FatigueCost,
                    _world.Combat.IsActive && _world.Combat.Mode == CombatMode.TurnBased ? spell.ActionPointCost : 0,
                    0, 100);
            switch (spell.EffectFamily)
            {
                case SpellEffectFamily.Healing:
                    _world.Vitality.RestoreHitPoints(request.Target, magnitude);
                    break;
                case SpellEffectFamily.DirectDamage:
                    _world.Vitality.ApplyHitPointDamage(request.Target, magnitude);
                    if (_world.Vitality.IsDead(request.Target))
                        _world.DeathConsequences.Process(request.Caster, request.Target);
                    break;
                case SpellEffectFamily.AttributeModifier:
                case SpellEffectFamily.CritterFlag:
                    long startedAt = ElapsedMilliseconds;
                    var effect = new ActiveSpellEffect(_nextEffectId++, spell.Id, request.Caster,
                        request.Target, spell.AttributeMagnitude, startedAt,
                        spell.Maintained ? checked(startedAt + spell.UpkeepPeriodMilliseconds) : 0,
                        spell.DurationSourceMilliseconds > 0
                            ? checked(startedAt + spell.DurationSourceMilliseconds) : 0);
                    ApplyEffect(effect, spell);
                    _activeEffects.Add(effect);
                    effectId = effect.Id;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return new SpellCastResult(SpellCastFailure.None, request, preview.FatigueCost,
                _world.Combat.IsActive && _world.Combat.Mode == CombatMode.TurnBased ? spell.ActionPointCost : 0,
                magnitude, resistance, effectId);
        }

        public SpellCastResult Preview(SpellCastRequest request) => PreviewInternal(request, false);

        private SpellCastResult PreviewInternal(SpellCastRequest request, bool scheduledRealTime)
        {
            if (!PhaseOneSpellCatalog.TryGet(request.SpellId, out SpellDefinition spell))
                return Fail(SpellCastFailure.InvalidSpell, request);
            if (!request.Caster.IsPersistent || !_world.Characters.TryGet(request.Caster, out _)
                || !_world.Vitality.TryGet(request.Caster, out _)) return Fail(SpellCastFailure.InvalidCaster, request);
            if (!KnowsSpell(request.Caster, request.SpellId)) return Fail(SpellCastFailure.SpellNotKnown, request);
            if (_world.Progression.GetLevel(request.Caster) < spell.MinimumLevel
                || _world.Characters.GetEffectiveAttribute(request.Caster, CharacterAttribute.Willpower)
                < spell.WillpowerRequirement) return Fail(SpellCastFailure.PrerequisiteNotMet, request);
            if (!_world.Vitality.IsConscious(request.Caster)) return Fail(SpellCastFailure.CasterUnavailable, request);
            int cost = CastCost(spell, request.Caster);
            int currentFatigue = _world.Vitality.GetCurrentFatigue(request.Caster);
            if (currentFatigue - cost <= -15 || spell.Maintained && currentFatigue - cost < 0)
                return Fail(SpellCastFailure.InsufficientFatigue, request);
            if (spell.Maintained && MaintainedCount(request.Caster) >= MaintainSlotCount(request.Caster))
                return Fail(SpellCastFailure.MaintainSlotUnavailable, request);
            SpellCastFailure targetFailure = ValidateTarget(spell, request);
            if (targetFailure != SpellCastFailure.None) return Fail(targetFailure, request);
            if ((spell.NoStack || spell.Maintained)
                && _activeEffects.Any(value => value.SpellId == spell.Id && value.Target == request.Target))
                return Fail(SpellCastFailure.DuplicateEffect, request);
            CombatFailure combat = _world.Combat.PreviewSpellAction(request.Caster, spell.ActionPointCost);
            if (combat != CombatFailure.None) return Fail(SpellCastFailure.CombatRejected, request);
            if (!scheduledRealTime && _world.Combat.IsActive && _world.Combat.Mode == CombatMode.RealTime)
                return Fail(SpellCastFailure.RealTimeSchedulingRequired, request);
            return new SpellCastResult(SpellCastFailure.None, request, cost);
        }

        public bool EndEffect(long effectId)
        {
            ActiveSpellEffect effect = _activeEffects.FirstOrDefault(value => value.Id == effectId);
            return effect != null
                   && PhaseOneSpellCatalog.TryGet(effect.SpellId, out SpellDefinition spell)
                   && spell.Maintained
                   && CancelMaintainedEffect(effect.Caster, effectId);
        }

        public bool CancelMaintainedEffect(ArcanumObjectId caster, long effectId)
        {
            int index = _activeEffects.FindIndex(value => value.Id == effectId);
            if (index < 0) return false;
            ActiveSpellEffect effect = _activeEffects[index];
            if (effect.Caster != caster || !PhaseOneSpellCatalog.TryGet(effect.SpellId, out SpellDefinition spell)
                                        || !spell.Maintained) return false;
            return TerminateEffect(effectId, SpellEffectTerminationReason.CasterCancellation);
        }

        public int DispelEffects(ArcanumObjectId subject)
        {
            if (!subject.IsPersistent || !_world.Characters.TryGet(subject, out _)) return 0;
            ActiveSpellEffect[] dispelled = _activeEffects
                .Where(value => value.Caster == subject || value.Target == subject)
                .OrderBy(value => value.Id).ToArray();
            foreach (ActiveSpellEffect effect in dispelled)
                TerminateEffect(effect.Id, SpellEffectTerminationReason.Dispelled);
            return dispelled.Length;
        }

        public void AdvanceTime(int elapsedSourceMilliseconds)
            => _world.SourceTime.Advance(elapsedSourceMilliseconds);

        public bool HasActiveCritterFlag(ArcanumObjectId identity, int flag)
            => _activeEffects.Any(value => value.Target == identity
                && PhaseOneSpellCatalog.TryGet(value.SpellId, out SpellDefinition spell)
                && spell.EffectFamily == SpellEffectFamily.CritterFlag
                && (spell.RuntimeCritterFlag & flag) != 0);

        private void OnSourceTimeAdvanced(SourceTimeAdvance advance)
        {
            foreach (ActiveSpellEffect effect in _activeEffects.OrderBy(value => value.Id).ToArray())
            {
                if (!_world.Vitality.TryGet(effect.Caster, out _) || !_world.Vitality.IsConscious(effect.Caster)
                    || !_world.Vitality.TryGet(effect.Target, out _) || !_world.Vitality.IsAlive(effect.Target))
                {
                    TerminateEffect(effect.Id, SpellEffectTerminationReason.InvalidParticipant);
                    continue;
                }
                PhaseOneSpellCatalog.TryGet(effect.SpellId, out SpellDefinition spell);
                if (effect.ExpiresAtMilliseconds > 0 && effect.ExpiresAtMilliseconds <= ElapsedMilliseconds)
                {
                    TerminateEffect(effect.Id, SpellEffectTerminationReason.NaturalExpiration);
                    continue;
                }
                while (spell.Maintained && _activeEffects.Contains(effect)
                       && effect.NextUpkeepAtMilliseconds <= ElapsedMilliseconds)
                {
                    int cost = UpkeepCost(spell, effect.Caster);
                    if (_world.Vitality.GetCurrentFatigue(effect.Caster) - cost <= -15)
                    {
                        TerminateEffect(effect.Id, SpellEffectTerminationReason.UpkeepFailure);
                        break;
                    }
                    _world.Vitality.ApplyFatigueDamage(effect.Caster, cost);
                    effect.NextUpkeepAtMilliseconds = checked(effect.NextUpkeepAtMilliseconds
                                                              + spell.UpkeepPeriodMilliseconds);
                }
            }
        }

        private bool TerminateEffect(long effectId, SpellEffectTerminationReason reason)
        {
            int index = _activeEffects.FindIndex(value => value.Id == effectId);
            if (index < 0) return false;
            ActiveSpellEffect effect = _activeEffects[index];
            PhaseOneSpellCatalog.TryGet(effect.SpellId, out SpellDefinition spell);
            RemoveEffect(effect, spell);
            _activeEffects.RemoveAt(index);
            LastTermination = new SpellEffectTermination(effect.Id, effect.SpellId, reason, ElapsedMilliseconds);
            return true;
        }

        private void ApplyEffect(ActiveSpellEffect effect, SpellDefinition spell)
        {
            if (spell.EffectFamily == SpellEffectFamily.AttributeModifier)
                _world.Characters.SetEffectModifier(effect.Target, effect.ModifierIdentity,
                    spell.ModifiedAttribute.Value, effect.Magnitude);
        }

        private void RemoveEffect(ActiveSpellEffect effect, SpellDefinition spell)
        {
            if (spell.EffectFamily == SpellEffectFamily.AttributeModifier)
                _world.Characters.RemoveEffectModifier(effect.Target, effect.ModifierIdentity);
        }

        private SpellCastFailure ValidateTarget(SpellDefinition spell, SpellCastRequest request)
        {
            if (!request.Target.IsPersistent || !_world.Characters.TryGet(request.Target, out _)
                || !_world.Vitality.TryGet(request.Target, out _)) return SpellCastFailure.InvalidTarget;
            if (!spell.AllowsSelf && request.Caster == request.Target) return SpellCastFailure.InvalidTarget;
            if (!_world.Vitality.IsAlive(request.Target)) return SpellCastFailure.TargetUnavailable;
            if (spell.TargetClass == SpellTargetClass.DamagedLivingCritter
                && _world.Vitality.GetCurrentHitPoints(request.Target) >= _world.Vitality.GetMaximumHitPoints(request.Target))
                return SpellCastFailure.InvalidTarget;
            if (spell.Id == PhaseOneSpellCatalog.MinorHealing
                && _world.Combat.HasCritterFlag(request.Target, unchecked((int)0x20000000)))
                return SpellCastFailure.InvalidTarget;
            if (spell.Id == PhaseOneSpellCatalog.Flash
                && _world.Combat.HasCritterFlag(request.Target, unchecked((int)0x20000000)))
                return SpellCastFailure.InvalidTarget;
            if (request.Caster == request.Target) return SpellCastFailure.None;
            if (!_world.TryGetPlacement(request.Caster, out ObjectPlacement casterPlacement)
                || !_world.TryGetPlacement(request.Target, out ObjectPlacement targetPlacement)
                || casterPlacement.Kind != ObjectPlacementKind.World
                || targetPlacement.Kind != ObjectPlacementKind.World
                || !string.Equals(casterPlacement.Sector, targetPlacement.Sector, StringComparison.Ordinal))
                return SpellCastFailure.TargetUnavailable;
            int distance;
            bool lineOfSight;
            if (!_world.Combat.TryGetSpellTraversal(request.Caster, request.Target, out distance, out lineOfSight))
            {
                Vector2 delta = casterPlacement.TilePosition - targetPlacement.TilePosition;
                distance = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)));
                lineOfSight = true;
            }
            if (distance > spell.Range) return SpellCastFailure.OutOfRange;
            return lineOfSight ? SpellCastFailure.None : SpellCastFailure.LineOfSightBlocked;
        }

        private int ResolveResistance(ArcanumObjectId caster, ArcanumObjectId target)
        {
            if (caster == target) return 0;
            int resistance = _world.DerivedStats.GetResistance(target, CharacterResistance.Magic);
            int aptitude = _world.DerivedStats.GetDerivedStat(target, CharacterDerivedStat.MagickTechAptitude);
            if (aptitude < 0) resistance = 100 - (100 - resistance) * (aptitude + 100) / 100;
            resistance = Math.Max(0, Math.Min(100, resistance));
            int chance = _random.Next(1, 101);
            return chance < resistance ? resistance - chance : 0;
        }

        private bool ResolveSavingThrow(SpellDefinition spell, ArcanumObjectId caster,
            ArcanumObjectId target, int resistance)
        {
            if (caster == target || !spell.ResistanceAttribute.HasValue) return false;
            int difficulty = _world.Characters.GetEffectiveAttribute(target, spell.ResistanceAttribute.Value)
                             + spell.ResistanceModifier - resistance / 10;
            return difficulty > 0 && _random.Next(1, 21) <= difficulty;
        }

        private int ScaledMagnitude(SpellDefinition spell, ArcanumObjectId caster)
        {
            if (spell.MaximumMagnitude <= spell.MinimumMagnitude) return spell.MinimumMagnitude;
            int aptitude = Math.Max(0, _world.DerivedStats.GetDerivedStat(caster,
                CharacterDerivedStat.MagickTechAptitude));
            return spell.MinimumMagnitude + aptitude * (spell.MaximumMagnitude - spell.MinimumMagnitude) / 100;
        }

        private int CastCost(SpellDefinition spell, ArcanumObjectId caster)
        {
            int cost = spell.BaseFatigueCost;
            if (_world.Characters.Get(caster).Race == CharacterRace.Dwarf) cost = checked(cost * 2);
            if (_masteries.TryGetValue(caster, out int mastery) && mastery == (int)spell.College) cost /= 2;
            return cost;
        }

        private int UpkeepCost(SpellDefinition spell, ArcanumObjectId caster)
            => _world.Characters.Get(caster).Race == CharacterRace.Dwarf
                ? checked(spell.UpkeepFatigueCost * 2) : spell.UpkeepFatigueCost;

        private int MaintainSlotCount(ArcanumObjectId caster)
            => Math.Min(5, _world.Characters.GetEffectiveAttribute(caster, CharacterAttribute.Intelligence) / 4);

        private int MaintainedCount(ArcanumObjectId caster)
            => _activeEffects.Count(value => value.Caster == caster
                && PhaseOneSpellCatalog.TryGet(value.SpellId, out SpellDefinition spell) && spell.Maintained);

        private static SpellCastResult Fail(SpellCastFailure failure, SpellCastRequest request)
            => new(failure, request);
    }
}
