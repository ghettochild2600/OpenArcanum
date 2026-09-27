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
    public enum SpellEffectFamily { AttributeModifier, DirectDamage, Healing }
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

        public SpellDefinition(int id, string name, SpellTargetClass targetClass,
            SpellEffectFamily effectFamily, SpellDisposition disposition, int baseFatigueCost,
            int actionPointCost, int range, bool allowsSelf, int minimumMagnitude = 0,
            int maximumMagnitude = 0, bool maintained = false, int upkeepFatigueCost = 0,
            int upkeepPeriodMilliseconds = 0, CharacterAttribute? modifiedAttribute = null,
            int attributeMagnitude = 0)
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
        }
    }

    public static class PhaseOneSpellCatalog
    {
        public const int StrengthOfEarth = 15;
        public const int Harm = 55;
        public const int MinorHealing = 60;
        public const int SourceDefaultRange = 99;
        // AG_THROW_SPELL consumes four action points before beginning the spell.
        public const int SourceSpellActionPointCost = 4;

        private static readonly IReadOnlyDictionary<int, SpellDefinition> Definitions =
            new Dictionary<int, SpellDefinition>
            {
                [StrengthOfEarth] = new(StrengthOfEarth, "Strength of Earth",
                    SpellTargetClass.LivingCritter, SpellEffectFamily.AttributeModifier,
                    SpellDisposition.Friendly, 5, SourceSpellActionPointCost, SourceDefaultRange, true,
                    maintained: true, upkeepFatigueCost: 1, upkeepPeriodMilliseconds: 10_000,
                    modifiedAttribute: CharacterAttribute.Strength, attributeMagnitude: 4),
                [Harm] = new(Harm, "Harm", SpellTargetClass.LivingCritter,
                    SpellEffectFamily.DirectDamage, SpellDisposition.Aggressive, 5,
                    SourceSpellActionPointCost, SourceDefaultRange, false, 3, 40),
                [MinorHealing] = new(MinorHealing, "Minor Healing",
                    SpellTargetClass.DamagedLivingCritter, SpellEffectFamily.Healing,
                    SpellDisposition.Friendly, 5, SourceSpellActionPointCost,
                    SourceDefaultRange, true, 5, 30),
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
        public string ModifierIdentity => $"magic:{Id}";

        internal ActiveSpellEffect(long id, int spellId, ArcanumObjectId caster,
            ArcanumObjectId target, int magnitude, long startedAtMilliseconds,
            long nextUpkeepAtMilliseconds)
        {
            Id = id;
            SpellId = spellId;
            Caster = caster;
            Target = target;
            Magnitude = magnitude;
            StartedAtMilliseconds = startedAtMilliseconds;
            NextUpkeepAtMilliseconds = nextUpkeepAtMilliseconds;
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
        private long _elapsedMilliseconds;
        private long _nextEffectId = 1;

        public IReadOnlyList<ActiveSpellEffect> ActiveEffects => _activeEffects;
        public long ElapsedMilliseconds => _elapsedMilliseconds;

        public MagicStateService(WorldMapSessionCoordinator world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _world.Combat.RoundCompleted += boundary => AdvanceTime(boundary.ElapsedMilliseconds);
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
                ElapsedMilliseconds = _elapsedMilliseconds,
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
                    }).ToList(),
            };

        internal void RestoreSaveData(MagicSaveData data)
        {
            if (data == null) return;
            _elapsedMilliseconds = data.ElapsedMilliseconds;
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
                    value.StartedAtMilliseconds, value.NextUpkeepAtMilliseconds);
                PhaseOneSpellCatalog.TryGet(value.SpellId, out SpellDefinition spell);
                _world.Characters.SetEffectModifier(target, effect.ModifierIdentity,
                    spell.ModifiedAttribute.Value, effect.Magnitude);
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

            _world.Vitality.ApplyFatigueDamage(request.Caster, preview.FatigueCost);
            if (_world.Combat.IsActive && _world.Combat.Mode == CombatMode.TurnBased)
                _world.Combat.CommitTurnBasedSpellAction(request.Caster, spell.ActionPointCost);

            long? effectId = null;
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
                    var effect = new ActiveSpellEffect(_nextEffectId++, spell.Id, request.Caster,
                        request.Target, spell.AttributeMagnitude, _elapsedMilliseconds,
                        checked(_elapsedMilliseconds + spell.UpkeepPeriodMilliseconds));
                    _world.Characters.SetEffectModifier(request.Target, effect.ModifierIdentity,
                        spell.ModifiedAttribute.Value, effect.Magnitude);
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
            if (spell.Maintained && _activeEffects.Any(value => value.SpellId == spell.Id && value.Target == request.Target))
                return Fail(SpellCastFailure.DuplicateEffect, request);
            CombatFailure combat = _world.Combat.PreviewSpellAction(request.Caster, spell.ActionPointCost);
            if (combat != CombatFailure.None) return Fail(SpellCastFailure.CombatRejected, request);
            if (!scheduledRealTime && _world.Combat.IsActive && _world.Combat.Mode == CombatMode.RealTime)
                return Fail(SpellCastFailure.RealTimeSchedulingRequired, request);
            return new SpellCastResult(SpellCastFailure.None, request, cost);
        }

        public bool EndEffect(long effectId)
        {
            int index = _activeEffects.FindIndex(value => value.Id == effectId);
            if (index < 0) return false;
            ActiveSpellEffect effect = _activeEffects[index];
            _world.Characters.RemoveEffectModifier(effect.Target, effect.ModifierIdentity);
            _activeEffects.RemoveAt(index);
            return true;
        }

        public void AdvanceTime(int elapsedSourceMilliseconds)
        {
            if (elapsedSourceMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedSourceMilliseconds));
            _elapsedMilliseconds = checked(_elapsedMilliseconds + elapsedSourceMilliseconds);
            foreach (ActiveSpellEffect effect in _activeEffects.OrderBy(value => value.Id).ToArray())
            {
                if (!_world.Vitality.TryGet(effect.Caster, out _) || !_world.Vitality.IsConscious(effect.Caster))
                {
                    EndEffect(effect.Id);
                    continue;
                }
                PhaseOneSpellCatalog.TryGet(effect.SpellId, out SpellDefinition spell);
                while (_activeEffects.Contains(effect) && effect.NextUpkeepAtMilliseconds <= _elapsedMilliseconds)
                {
                    int cost = UpkeepCost(spell, effect.Caster);
                    if (_world.Vitality.GetCurrentFatigue(effect.Caster) - cost <= -15)
                    {
                        EndEffect(effect.Id);
                        break;
                    }
                    _world.Vitality.ApplyFatigueDamage(effect.Caster, cost);
                    effect.NextUpkeepAtMilliseconds = checked(effect.NextUpkeepAtMilliseconds
                                                              + spell.UpkeepPeriodMilliseconds);
                }
            }
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
            int chance = _random.Next(0, 100);
            return chance < resistance ? resistance - chance : 0;
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
            => _activeEffects.Count(value => value.Caster == caster);

        private static SpellCastResult Fail(SpellCastFailure failure, SpellCastRequest request)
            => new(failure, request);
    }
}
