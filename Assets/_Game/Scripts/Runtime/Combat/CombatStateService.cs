using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Art;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.World;
using UnityEngine;

namespace Arcanum.Runtime.Combat
{
    public enum CombatMode
    {
        RealTime,
        TurnBased,
    }

    public enum CombatLifecycle
    {
        Inactive,
        Starting,
        Active,
        Ending,
    }

    public enum CombatFailure
    {
        None,
        Inactive,
        AlreadyActive,
        UnsupportedMode,
        ActorNotFound,
        TargetNotFound,
        InvalidActor,
        InvalidTarget,
        SameParticipant,
        ParticipantUnavailable,
        TargetNotHostile,
        UnresolvedHostilityScript,
        AlreadyRegistered,
        ParticipantNotRegistered,
        NotCurrentParticipant,
        HostileParticipantActive,
        NavigationUnavailable,
        InvalidDestination,
        Unreachable,
        InsufficientActionPoints,
        OutOfRange,
        LineOfFireBlocked,
        UnsupportedAttackMode,
        UnsupportedWeapon,
        NoAmmo,
        IncompatibleAmmo,
        UnsupportedDamageProfile,
        InvalidCalledLocation,
        UnsupportedCriticalEffect,
        UnresolvedDeathScript,
        PresentationUnavailable,
    }

    public readonly struct CombatResult
    {
        public bool Succeeded => Failure == CombatFailure.None;
        public CombatFailure Failure { get; }

        public CombatResult(CombatFailure failure) => Failure = failure;
    }

    public enum CombatAttackMode
    {
        BasicMelee,
        BasicRanged,
    }

    /// <summary>Source hit-location ids. None preserves the bounded ordinary-attack path.</summary>
    public enum CombatCalledLocation
    {
        None = -1,
        Torso = 0,
        Head = 1,
        Arm = 2,
        Leg = 3,
    }

    public readonly struct CombatAttackRequest
    {
        public ArcanumObjectId Attacker { get; }
        public ArcanumObjectId Target { get; }
        public CombatAttackMode Mode { get; }
        public CombatCalledLocation CalledLocation { get; }

        public CombatAttackRequest(ArcanumObjectId attacker, ArcanumObjectId target,
            CombatAttackMode mode = CombatAttackMode.BasicMelee,
            CombatCalledLocation calledLocation = CombatCalledLocation.None)
        {
            Attacker = attacker;
            Target = target;
            Mode = mode;
            CalledLocation = calledLocation;
        }
    }

    public enum CombatAttackModifierStage
    {
        BaseEffectiveness,
        Attribute,
        TargetDefense,
        WeaponRequirement,
        Distance,
        Cover,
        Weapon,
        CalledLocation,
    }

    public enum CombatAttackModifierReason
    {
        BaseSkill,
        IntelligenceTwenty,
        ArmorClass,
        MinimumStrength,
        PerceptionRange,
        Cover,
        WeaponToHit,
        CalledLocation,
    }

    public readonly struct CombatAttackModifier
    {
        public CombatAttackModifierStage Stage { get; }
        public CombatAttackModifierReason Reason { get; }
        public int Value { get; }
        public bool Applied { get; }
        public bool Suppressed { get; }
        public int SourceValue { get; }

        internal CombatAttackModifier(CombatAttackModifierStage stage,
            CombatAttackModifierReason reason, int value, bool applied, bool suppressed = false,
            int sourceValue = 0)
        {
            Stage = stage;
            Reason = reason;
            Value = value;
            Applied = applied;
            Suppressed = suppressed;
            SourceValue = sourceValue;
        }
    }

    public sealed class CombatAttackModifierLedger
    {
        private readonly IReadOnlyList<CombatAttackModifier> _entries;

        public static CombatAttackModifierLedger Empty { get; } = new(Array.Empty<CombatAttackModifier>());
        public IReadOnlyList<CombatAttackModifier> Entries => _entries;
        public int UnclampedTotal { get; }
        public int FinalEffectiveValue { get; }

        internal CombatAttackModifierLedger(IEnumerable<CombatAttackModifier> entries)
        {
            CombatAttackModifier[] copy = entries?.ToArray() ?? Array.Empty<CombatAttackModifier>();
            _entries = Array.AsReadOnly(copy);
            UnclampedTotal = copy.Where(value => value.Applied && !value.Suppressed)
                .Sum(value => value.Value);
            FinalEffectiveValue = Math.Min(100, Math.Max(0, UnclampedTotal));
        }
    }

    public enum CombatAttackOutcome
    {
        Miss,
        Hit,
        CriticalSuccess,
        CriticalFailure,
    }

    public enum CombatCriticalEffect
    {
        None,
        BonusDamage50,
        BonusDamage100,
        BonusDamage200,
        SelfHit,
    }

    public interface ICombatRandom
    {
        int NextInclusive(int minimum, int maximum);
    }

    public readonly struct CombatMoveResult
    {
        public bool Succeeded => Failure == CombatFailure.None;
        public CombatFailure Failure { get; }
        public Vector2Int Start { get; }
        public Vector2Int RequestedDestination { get; }
        public Vector2Int FinalPosition { get; }
        public int RouteSteps { get; }
        public int StepsMoved { get; }
        public int ActionPointsSpent { get; }
        public int FatigueDamage { get; }
        public bool ReachedDestination => Succeeded && FinalPosition == RequestedDestination;

        internal CombatMoveResult(CombatFailure failure, Vector2Int start, Vector2Int destination,
            Vector2Int finalPosition, int routeSteps = 0, int stepsMoved = 0,
            int actionPointsSpent = 0, int fatigueDamage = 0)
        {
            Failure = failure;
            Start = start;
            RequestedDestination = destination;
            FinalPosition = finalPosition;
            RouteSteps = routeSteps;
            StepsMoved = stepsMoved;
            ActionPointsSpent = actionPointsSpent;
            FatigueDamage = fatigueDamage;
        }
    }

    public readonly struct CombatHitChance
    {
        public int MeleeEffectiveness { get; }
        public int ArmorClass { get; }
        public int ArmorDifficulty { get; }
        public int AttackChance { get; }
        public int DodgeChance { get; }
        public int FinalChance { get; }

        internal CombatHitChance(int meleeEffectiveness, int armorClass, int armorDifficulty,
            int attackChance, int dodgeChance)
        {
            MeleeEffectiveness = meleeEffectiveness;
            ArmorClass = armorClass;
            ArmorDifficulty = armorDifficulty;
            AttackChance = attackChance;
            DodgeChance = dodgeChance;
            FinalChance = attackChance * (100 - dodgeChance) / 100;
        }
    }

    public readonly struct CombatAttackResult
    {
        public bool Succeeded => Failure == CombatFailure.None;
        public CombatFailure Failure { get; }
        public bool Hit { get; }
        public bool Dodged { get; }
        public int AttackRoll { get; }
        public int DodgeRoll { get; }
        public int RawHitPointDamage { get; }
        public int MitigatedHitPointDamage { get; }
        public int RawFatigueDamage { get; }
        public int MitigatedFatigueDamage { get; }
        public int ResultingHitPoints { get; }
        public int ResultingFatigue { get; }
        public int ActionPointCost { get; }
        public int ActionPointsSpent { get; }
        public int OverdrawFatigueDamage { get; }
        public ArcanumObjectId WeaponIdentity { get; }
        public ArcanumObjectId AmmoIdentity { get; }
        public int AmmoQuantityBefore { get; }
        public int AmmoQuantityAfter { get; }
        public CombatHitChance Chance { get; }
        public CombatAttackOutcome Outcome { get; }
        public int CriticalRoll { get; }
        public int CriticalChance { get; }
        public CombatCriticalEffect CriticalEffect { get; }
        public int CriticalEffectRoll { get; }
        public int SecondaryCriticalEffectRoll { get; }
        public int TertiaryCriticalEffectRoll { get; }
        public ArcanumObjectId EffectTargetIdentity { get; }
        public CombatAttackRequest Request { get; }
        public CombatAttackModifierLedger ModifierLedger { get; }
        public CombatCalledLocation RequestedLocation => Request.CalledLocation;
        public int FinalEffectiveAttackValue => ModifierLedger?.FinalEffectiveValue ?? Chance.AttackChance;

        internal CombatAttackResult(CombatFailure failure, CombatHitChance chance = default,
            bool hit = false, bool dodged = false, int attackRoll = 0, int dodgeRoll = 0,
            int rawHitPointDamage = 0, int mitigatedHitPointDamage = 0,
            int rawFatigueDamage = 0, int mitigatedFatigueDamage = 0,
            int resultingHitPoints = 0, int resultingFatigue = 0,
            int actionPointCost = 0, int actionPointsSpent = 0, int overdrawFatigueDamage = 0,
            ArcanumObjectId weaponIdentity = default, ArcanumObjectId ammoIdentity = default,
            int ammoQuantityBefore = 0, int ammoQuantityAfter = 0,
            CombatAttackOutcome outcome = CombatAttackOutcome.Miss,
            int criticalRoll = 0, int criticalChance = 0,
            CombatCriticalEffect criticalEffect = CombatCriticalEffect.None,
            int criticalEffectRoll = 0, int secondaryCriticalEffectRoll = 0,
            int tertiaryCriticalEffectRoll = 0, ArcanumObjectId effectTargetIdentity = default,
            CombatAttackRequest request = default, CombatAttackModifierLedger modifierLedger = null)
        {
            Failure = failure;
            Chance = chance;
            Hit = hit;
            Dodged = dodged;
            AttackRoll = attackRoll;
            DodgeRoll = dodgeRoll;
            RawHitPointDamage = rawHitPointDamage;
            MitigatedHitPointDamage = mitigatedHitPointDamage;
            RawFatigueDamage = rawFatigueDamage;
            MitigatedFatigueDamage = mitigatedFatigueDamage;
            ResultingHitPoints = resultingHitPoints;
            ResultingFatigue = resultingFatigue;
            ActionPointCost = actionPointCost;
            ActionPointsSpent = actionPointsSpent;
            OverdrawFatigueDamage = overdrawFatigueDamage;
            WeaponIdentity = weaponIdentity;
            AmmoIdentity = ammoIdentity;
            AmmoQuantityBefore = ammoQuantityBefore;
            AmmoQuantityAfter = ammoQuantityAfter;
            Outcome = outcome;
            CriticalRoll = criticalRoll;
            CriticalChance = criticalChance;
            CriticalEffect = criticalEffect;
            CriticalEffectRoll = criticalEffectRoll;
            SecondaryCriticalEffectRoll = secondaryCriticalEffectRoll;
            TertiaryCriticalEffectRoll = tertiaryCriticalEffectRoll;
            EffectTargetIdentity = effectTargetIdentity;
            Request = request;
            ModifierLedger = modifierLedger ?? CombatAttackModifierLedger.Empty;
        }
    }

    /// <summary>Immutable source facts used by the transient combat coordinator.</summary>
    public readonly struct CombatActorSource : IEquatable<CombatActorSource>
    {
        private const int DamageTypeCount = 5;
        private readonly int[] _naturalDamage;

        public ArcanumObjectId Identity { get; }
        public ObjectType ObjectType { get; }
        public int? PrototypeNumber { get; }
        public string SourceSector { get; }
        public int SourceOrder { get; }
        public int NpcFlags { get; }
        public int CritterFlags { get; }
        public int WillKosScriptNum { get; }
        public int DyingScriptNum { get; }
        public int ExperienceWorth { get; }

        public CombatActorSource(ArcanumObjectId identity, ObjectType objectType, int? prototypeNumber,
            string sourceSector, int sourceOrder, int npcFlags, int critterFlags, int willKosScriptNum,
            int[] naturalDamage = null, int dyingScriptNum = 0, int experienceWorth = 0)
        {
            if (!identity.IsPersistent)
                throw new ArgumentException("Combat actors require a persistent ObjectID.", nameof(identity));
            if (objectType is not (ObjectType.Pc or ObjectType.Npc))
                throw new ArgumentOutOfRangeException(nameof(objectType));
            if (sourceOrder < 0) throw new ArgumentOutOfRangeException(nameof(sourceOrder));
            if (dyingScriptNum < 0) throw new ArgumentOutOfRangeException(nameof(dyingScriptNum));
            if (experienceWorth < 0) throw new ArgumentOutOfRangeException(nameof(experienceWorth));
            Identity = identity;
            ObjectType = objectType;
            PrototypeNumber = prototypeNumber;
            SourceSector = WorldMapSessionCoordinator.NormalizeSector(sourceSector);
            SourceOrder = sourceOrder;
            NpcFlags = npcFlags;
            CritterFlags = critterFlags;
            WillKosScriptNum = willKosScriptNum;
            DyingScriptNum = dyingScriptNum;
            ExperienceWorth = experienceWorth;
            if (naturalDamage != null && naturalDamage.Length < DamageTypeCount * 2)
                throw new ArgumentException("Natural damage requires five min/max source pairs.",
                    nameof(naturalDamage));
            _naturalDamage = new int[DamageTypeCount * 2];
            if (naturalDamage != null) Array.Copy(naturalDamage, _naturalDamage, _naturalDamage.Length);
        }

        public int GetNaturalDamageMinimum(DamageType type)
            => _naturalDamage?[(int)type * 2] ?? 0;

        public int GetNaturalDamageMaximum(DamageType type)
            => _naturalDamage?[(int)type * 2 + 1] ?? 0;

        public bool Equals(CombatActorSource other)
            => Identity == other.Identity && ObjectType == other.ObjectType
               && PrototypeNumber == other.PrototypeNumber && SourceSector == other.SourceSector
               && SourceOrder == other.SourceOrder && NpcFlags == other.NpcFlags
               && CritterFlags == other.CritterFlags && WillKosScriptNum == other.WillKosScriptNum
               && DyingScriptNum == other.DyingScriptNum && ExperienceWorth == other.ExperienceWorth
               && NaturalDamageEquals(other);

        public override bool Equals(object obj) => obj is CombatActorSource other && Equals(other);
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Identity);
            hash.Add(ObjectType);
            hash.Add(PrototypeNumber);
            hash.Add(SourceSector);
            hash.Add(SourceOrder);
            hash.Add(NpcFlags);
            hash.Add(CritterFlags);
            hash.Add(WillKosScriptNum);
            hash.Add(DyingScriptNum);
            hash.Add(ExperienceWorth);
            for (int index = 0; index < DamageTypeCount * 2; index++)
                hash.Add(_naturalDamage?[index] ?? 0);
            return hash.ToHashCode();
        }

        private bool NaturalDamageEquals(CombatActorSource other)
        {
            for (int index = 0; index < DamageTypeCount * 2; index++)
                if ((_naturalDamage?[index] ?? 0) != (other._naturalDamage?[index] ?? 0)) return false;
            return true;
        }
    }

    public readonly struct CombatParticipant
    {
        public ArcanumObjectId Identity { get; }
        public ObjectType ObjectType { get; }
        public int? PrototypeNumber { get; }
        public int SourceOrder { get; }

        internal CombatParticipant(CombatActorSource source)
        {
            Identity = source.Identity;
            ObjectType = source.ObjectType;
            PrototypeNumber = source.PrototypeNumber;
            SourceOrder = source.SourceOrder;
        }
    }

    /// <summary>Authoritative source-time increment emitted once after a completed turn-based round.</summary>
    public readonly struct CombatRoundBoundary
    {
        public int CompletedRoundNumber { get; }
        public int ElapsedMilliseconds { get; }
        public long TotalElapsedMilliseconds { get; }

        internal CombatRoundBoundary(int completedRoundNumber, int elapsedMilliseconds,
            long totalElapsedMilliseconds)
        {
            CompletedRoundNumber = completedRoundNumber;
            ElapsedMilliseconds = elapsedMilliseconds;
            TotalElapsedMilliseconds = totalElapsedMilliseconds;
        }
    }

    /// <summary>
    /// Session-owned M8A combat state. It owns only transient participants, order, current turn and AP;
    /// character HP/fatigue remain authoritative in <see cref="CharacterVitalityService"/>.
    /// </summary>
    public sealed class CombatStateService
    {
        public const int UnarmedAttackActionPointCost = 5;
        public const int WalkingActionPointCostPerStep = 2;
        public const int RunningActionPointCostPerStep = 1;
        public const int RoundBoundaryMilliseconds = 1000;

        internal const int OnfKos = 0x00000100;
        internal const int OnfNoAttack = 0x20000000;
        internal const int OcfUndead = 0x00000004;
        internal const int OcfFatigueImmune = 0x04000000;
        internal const int OcfStunned = 0x00000020;
        internal const int OcfParalyzed = 0x00000040;

        private readonly WorldMapSessionCoordinator _world;
        private readonly Dictionary<ArcanumObjectId, CombatActorSource> _sources = new();
        private readonly List<CombatParticipant> _participants = new();
        private readonly HashSet<ArcanumObjectId> _engaged = new();
        private readonly DeterministicTilePathfinder _pathfinder = new();
        private readonly List<Vector2Int> _route = new();
        private SectorNavigationMap _navigationMap;
        private ICombatRandom _random;

        public CombatLifecycle Lifecycle { get; private set; }
        public CombatMode Mode { get; private set; } = CombatMode.TurnBased;
        public bool IsActive => Lifecycle == CombatLifecycle.Active;
        public IReadOnlyList<CombatParticipant> Participants => _participants;
        public IReadOnlyCollection<ArcanumObjectId> EngagedParticipants => _engaged;
        public ArcanumObjectId CurrentParticipant { get; private set; }
        public int RoundNumber { get; private set; }
        public long ElapsedCombatTimeMilliseconds { get; private set; }
        public int CurrentActionPoints { get; private set; }
        public int MaximumActionPoints { get; private set; }
        public CombatAttackResult? LastAttackResult { get; private set; }
        public event Action<CombatRoundBoundary> RoundCompleted;

        public CombatStateService(WorldMapSessionCoordinator world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _random = new SystemCombatRandom();
            _world.Vitality.Changed += OnVitalityChanged;
        }

        internal void BindNavigationMap(SectorNavigationMap map) => _navigationMap = map;

        public void SetRandomSource(ICombatRandom random)
            => _random = random ?? throw new ArgumentNullException(nameof(random));

        public void ResetRandomSource() => _random = new SystemCombatRandom();

        public void RegisterActorSource(CombatActorSource source)
        {
            if (_sources.TryGetValue(source.Identity, out CombatActorSource existing))
            {
                if (existing.Equals(source)) return;
                if (Lifecycle == CombatLifecycle.Inactive && existing.ObjectType == ObjectType.Pc
                    && source.ObjectType == ObjectType.Pc
                    && existing.PrototypeNumber == source.PrototypeNumber
                    && existing.SourceOrder == source.SourceOrder
                    && existing.NpcFlags == source.NpcFlags
                    && existing.CritterFlags == source.CritterFlags
                    && existing.WillKosScriptNum == source.WillKosScriptNum
                    && existing.DyingScriptNum == source.DyingScriptNum
                    && existing.ExperienceWorth == source.ExperienceWorth)
                {
                    _sources[source.Identity] = source;
                    return;
                }
                if (!existing.Equals(source))
                    throw new InvalidOperationException($"Combat source collision or changed source: {source.Identity}.");
                return;
            }
            _sources.Add(source.Identity, source);
        }

        public bool TryGetActorSource(ArcanumObjectId identity, out CombatActorSource source)
            => _sources.TryGetValue(identity, out source);

        public CombatResult StartCombat(ArcanumObjectId actor, ArcanumObjectId target,
            CombatMode mode = CombatMode.TurnBased)
        {
            if (Lifecycle != CombatLifecycle.Inactive) return Fail(CombatFailure.AlreadyActive);
            if (mode != CombatMode.TurnBased) return Fail(CombatFailure.UnsupportedMode);
            if (actor == target) return Fail(CombatFailure.SameParticipant);
            if (!TryValidateActor(actor, out CombatActorSource actorSource, out CombatFailure actorFailure))
                return Fail(actorFailure);
            if (!TryValidateTarget(target, out CombatActorSource targetSource, out CombatFailure targetFailure))
                return Fail(targetFailure);
            if (targetSource.WillKosScriptNum != 0) return Fail(CombatFailure.UnresolvedHostilityScript);
            if ((targetSource.NpcFlags & OnfNoAttack) != 0
                || (targetSource.NpcFlags & OnfKos) == 0)
                return Fail(CombatFailure.TargetNotHostile);

            var pending = new List<CombatParticipant>
            {
                new(targetSource),
                new(actorSource),
            };
            var pendingEngagement = new HashSet<ArcanumObjectId> { actor, target };
            DiscoverNearbyHostiles(actor, pending, pendingEngagement);
            SortSourceOrder(pending);

            Lifecycle = CombatLifecycle.Starting;
            _world.Dialogue.Cancel("Combat started.");
            _participants.AddRange(pending);
            _engaged.UnionWith(pendingEngagement);
            Mode = mode;
            RoundNumber = 1;
            BeginParticipantTurn(_participants[0].Identity, requireActive: false);
            Lifecycle = CombatLifecycle.Active;
            return Success();
        }

        public CombatResult RegisterParticipant(ArcanumObjectId identity)
        {
            if (!IsActive) return Fail(CombatFailure.Inactive);
            if (_participants.Any(value => value.Identity == identity))
                return Fail(CombatFailure.AlreadyRegistered);
            if (!_sources.TryGetValue(identity, out CombatActorSource source))
                return Fail(CombatFailure.TargetNotFound);
            if (!IsEligible(source)) return Fail(CombatFailure.ParticipantUnavailable);
            _participants.Add(new CombatParticipant(source));
            _engaged.Add(identity);
            SortSourceOrder(_participants);
            return Success();
        }

        /// <summary>
        /// Enrolls a loaded, active source-hostile NPC in the current encounter exactly once.
        /// Later AI may call this authority without owning the participant roster.
        /// </summary>
        public CombatResult EngageParticipant(ArcanumObjectId identity)
        {
            if (!IsActive) return Fail(CombatFailure.Inactive);
            if (!_sources.TryGetValue(identity, out CombatActorSource source))
                return Fail(CombatFailure.TargetNotFound);
            if (source.ObjectType != ObjectType.Npc) return Fail(CombatFailure.InvalidTarget);
            if (!IsSourceHostile(source)) return Fail(CombatFailure.TargetNotHostile);
            if (!IsEligible(source)) return Fail(CombatFailure.ParticipantUnavailable);
            if (_engaged.Contains(identity)) return Fail(CombatFailure.AlreadyRegistered);
            _engaged.Add(identity);
            if (_participants.All(value => value.Identity != identity))
            {
                _participants.Add(new CombatParticipant(source));
                SortSourceOrder(_participants);
            }
            return Success();
        }

        public bool IsParticipantEngaged(ArcanumObjectId identity) => _engaged.Contains(identity);

        public CombatResult RemoveParticipant(ArcanumObjectId identity)
        {
            if (!IsActive) return Fail(CombatFailure.Inactive);
            int index = _participants.FindIndex(value => value.Identity == identity);
            if (index < 0) return Fail(CombatFailure.ParticipantNotRegistered);
            bool wasCurrent = CurrentParticipant == identity;
            _participants.RemoveAt(index);
            _engaged.Remove(identity);
            if (_participants.Count == 0)
            {
                ClearTransient();
                return Success();
            }
            if (wasCurrent)
            {
                int next = Math.Min(index, _participants.Count - 1);
                BeginParticipantTurn(_participants[next].Identity, requireActive: false);
            }
            return Success();
        }

        public CombatResult EndCurrentTurn(ArcanumObjectId actor)
        {
            if (!IsActive) return Fail(CombatFailure.Inactive);
            if (CurrentParticipant != actor) return Fail(CombatFailure.NotCurrentParticipant);
            if (_participants.All(value => value.Identity != actor))
                return Fail(CombatFailure.ParticipantNotRegistered);
            CurrentActionPoints = 0;
            AdvanceToNextEligibleParticipant(actor);
            return Success();
        }

        public CombatMoveResult MoveInCombat(ArcanumObjectId actor, Vector2Int destination,
            bool pcAlwaysRun = false)
        {
            if (!TryValidateActionActor(actor, out CombatActorSource source, out CombatFailure failure))
                return MoveFailure(failure, destination);
            if (_navigationMap == null) return MoveFailure(CombatFailure.NavigationUnavailable, destination);
            if (pcAlwaysRun && source.ObjectType != ObjectType.Pc)
                return MoveFailure(CombatFailure.InvalidActor, destination);
            if (!TryGetCombatPosition(actor, out Vector2Int start))
                return MoveFailure(CombatFailure.PresentationUnavailable, destination);
            if (!_navigationMap.Contains(destination) || !_navigationMap.IsWalkable(destination)
                || _navigationMap.IsOccupiedByOther(actor, destination))
                return new CombatMoveResult(CombatFailure.InvalidDestination, start, destination, start);

            _route.Clear();
            if (!_pathfinder.TryFindPath(_navigationMap, start, destination, _route,
                    tile => !_navigationMap.IsOccupiedByOther(actor, tile)))
                return new CombatMoveResult(CombatFailure.Unreachable, start, destination, start);
            if (_route.Count == 0)
                return new CombatMoveResult(CombatFailure.None, start, destination, start);

            int perStep = pcAlwaysRun ? RunningActionPointCostPerStep : WalkingActionPointCostPerStep;
            int fullCost = checked(_route.Count * perStep);
            int steps = _route.Count;
            int fatigueDamage = 0;
            if (CurrentActionPoints < fullCost)
            {
                if (source.ObjectType != ObjectType.Pc)
                    return new CombatMoveResult(CombatFailure.InsufficientActionPoints, start, destination, start,
                        _route.Count);
                int affordable = CurrentActionPoints / perStep;
                int remainder = CurrentActionPoints % perStep;
                if (remainder > 0 && _world.Vitality.GetCurrentFatigue(actor) > 1)
                {
                    affordable++;
                    fatigueDamage = 2;
                }
                steps = Math.Min(_route.Count, affordable);
                if (steps == 0)
                    return new CombatMoveResult(CombatFailure.InsufficientActionPoints, start, destination, start,
                        _route.Count);
            }

            Vector2Int finalPosition = _route[steps - 1];
            int spent = Math.Min(CurrentActionPoints, checked(steps * perStep));
            if (!_navigationMap.MoveRegisteredObject(actor, finalPosition))
                return new CombatMoveResult(CombatFailure.PresentationUnavailable, start, destination, start,
                    _route.Count);

            int facing = IsoProjection.DirFromDelta(finalPosition.x - (steps > 1 ? _route[steps - 2].x : start.x),
                finalPosition.y - (steps > 1 ? _route[steps - 2].y : start.y));
            if (!_world.TryGetLoadedObject(actor, out WorldObject runtime))
            {
                _navigationMap.MoveRegisteredObject(actor, start);
                return new CombatMoveResult(CombatFailure.PresentationUnavailable, start, destination, start,
                    _route.Count);
            }
            uint originalArt = runtime.ArtId;
            uint walkArt = CritterArtResolver.WithAnimRotation(runtime.ArtId, 1, facing) & ~(0x1Fu << 14);
            uint standArt = CritterArtResolver.WithAnimRotation(walkArt, 0, facing) & ~(0x1Fu << 14);
            if (!_world.SetMovementState(actor, finalPosition, walkArt, true)
                || !_world.SetMovementState(actor, finalPosition, standArt, false))
            {
                _navigationMap.MoveRegisteredObject(actor, start);
                _world.SetMovementState(actor, start, originalArt, false);
                return new CombatMoveResult(CombatFailure.PresentationUnavailable, start, destination, start,
                    _route.Count);
            }

            CurrentActionPoints -= spent;
            if (fatigueDamage > 0) _world.Vitality.ApplyFatigueDamage(actor, fatigueDamage);
            if (CurrentParticipant == actor && CurrentActionPoints == 0)
                AdvanceToNextEligibleParticipant(actor);
            return new CombatMoveResult(CombatFailure.None, start, destination, finalPosition,
                _route.Count, steps, spent, fatigueDamage);
        }

        public CombatHitChance GetBasicMeleeHitChance(ArcanumObjectId actor, ArcanumObjectId target)
            => BuildMeleeHitChance(actor, target, CombatCalledLocation.None, out _);

        private CombatHitChance BuildMeleeHitChance(ArcanumObjectId actor, ArcanumObjectId target,
            CombatCalledLocation calledLocation, out CombatAttackModifierLedger ledger)
        {
            int melee = _world.Progression.GetEffectiveSkillRank(actor, CharacterSkill.Melee);
            int effectiveness = checked(5 * melee + 25);
            bool intelligenceBonus = _world.Characters.GetEffectiveAttribute(actor,
                CharacterAttribute.Intelligence) >= 20;
            if (intelligenceBonus) effectiveness += 10;
            int armorClass = _world.DerivedStats.GetArmorClass(target);
            int difficulty = effectiveness * (armorClass / 2) / 100;
            int calledPenalty = GetCalledLocationPenalty(calledLocation);
            var modifiers = new[]
            {
                new CombatAttackModifier(CombatAttackModifierStage.BaseEffectiveness,
                    CombatAttackModifierReason.BaseSkill, checked(5 * melee + 25), true,
                    sourceValue: melee),
                new CombatAttackModifier(CombatAttackModifierStage.Attribute,
                    CombatAttackModifierReason.IntelligenceTwenty, intelligenceBonus ? 10 : 0,
                    intelligenceBonus,
                    sourceValue: _world.Characters.GetEffectiveAttribute(actor,
                        CharacterAttribute.Intelligence)),
                new CombatAttackModifier(CombatAttackModifierStage.TargetDefense,
                    CombatAttackModifierReason.ArmorClass, -difficulty, true,
                    sourceValue: armorClass),
                new CombatAttackModifier(CombatAttackModifierStage.WeaponRequirement,
                    CombatAttackModifierReason.MinimumStrength, 0, false),
                new CombatAttackModifier(CombatAttackModifierStage.Distance,
                    CombatAttackModifierReason.PerceptionRange, 0, false),
                new CombatAttackModifier(CombatAttackModifierStage.Weapon,
                    CombatAttackModifierReason.WeaponToHit, 0, false),
                new CombatAttackModifier(CombatAttackModifierStage.CalledLocation,
                    CombatAttackModifierReason.CalledLocation, calledPenalty,
                    IsCalledLocation(calledLocation), sourceValue: (int)calledLocation),
            };
            ledger = new CombatAttackModifierLedger(modifiers);
            int attackChance = ledger.FinalEffectiveValue;
            int dodge = 5 * _world.Progression.GetEffectiveSkillRank(target, CharacterSkill.Dodge);
            if (_world.Characters.GetEffectiveAttribute(target, CharacterAttribute.Intelligence) >= 20)
                dodge += 10;
            dodge = Math.Min(95, Math.Max(0, dodge));
            return new CombatHitChance(effectiveness, armorClass, difficulty, attackChance, dodge);
        }

        public CombatAttackResult Attack(ArcanumObjectId actor, ArcanumObjectId target,
            CombatAttackMode mode = CombatAttackMode.BasicMelee)
            => Attack(new CombatAttackRequest(actor, target, mode));

        public CombatAttackResult Attack(CombatAttackRequest request)
        {
            CombatAttackResult result = ResolveAttack(request);
            LastAttackResult = result;
            return result;
        }

        private CombatAttackResult ResolveAttack(CombatAttackRequest request)
        {
            ArcanumObjectId actor = request.Attacker;
            ArcanumObjectId target = request.Target;
            CombatAttackMode mode = request.Mode;
            if (!TryValidateActionActor(actor, out CombatActorSource source, out CombatFailure failure))
                return AttackFailure(failure, request);
            if (!Enum.IsDefined(typeof(CombatAttackMode), mode))
                return AttackFailure(CombatFailure.UnsupportedAttackMode, request);
            if (!Enum.IsDefined(typeof(CombatCalledLocation), request.CalledLocation))
                return AttackFailure(CombatFailure.InvalidCalledLocation, request);
            if (actor == target) return AttackFailure(CombatFailure.SameParticipant, request);
            if (!_participants.Any(value => value.Identity == target))
                return AttackFailure(CombatFailure.ParticipantNotRegistered, request);
            if (!_sources.TryGetValue(target, out CombatActorSource targetSource))
                return AttackFailure(CombatFailure.TargetNotFound, request);
            if (!IsEligible(targetSource)) return AttackFailure(CombatFailure.ParticipantUnavailable, request);
            if (!TryGetCombatPosition(actor, out Vector2Int actorPosition)
                || !TryGetCombatPosition(target, out Vector2Int targetPosition))
                return AttackFailure(CombatFailure.PresentationUnavailable, request);
            if (mode == CombatAttackMode.BasicRanged)
                return AttackRanged(request, source, targetSource, actorPosition, targetPosition);
            if (_world.TryGetEquippedItem(actor, WornLocation.Weapon, out _))
                return AttackFailure(CombatFailure.UnsupportedWeapon, request);
            if (InteractionRangeRules.Distance(actorPosition, targetPosition) > 1)
                return AttackFailure(CombatFailure.OutOfRange, request);

            bool overdraw = CurrentActionPoints < UnarmedAttackActionPointCost;
            if (overdraw && (source.ObjectType != ObjectType.Pc || CurrentActionPoints <= 0
                             || _world.Vitality.GetCurrentFatigue(actor) <= 1))
                return AttackFailure(CombatFailure.InsufficientActionPoints, request);
            if (!TryGetUnarmedDamageRange(source, DamageType.Normal, out int normalMinimum,
                    out int normalMaximum)
                || !TryGetUnarmedDamageRange(source, DamageType.Fatigue, out int fatigueMinimum,
                    out int fatigueMaximum)
                || HasUnsupportedNaturalDamage(source))
                return AttackFailure(CombatFailure.UnsupportedDamageProfile, request);

            CombatHitChance chance = BuildMeleeHitChance(actor, target, request.CalledLocation,
                out CombatAttackModifierLedger modifiers);
            int attackRoll = _random.NextInclusive(1, 100);
            bool ordinaryHit = attackRoll <= chance.AttackChance;
            int criticalRoll = _random.NextInclusive(1, 100);
            bool masterMelee = _world.Progression.GetTrainingLevel(actor, CharacterSkill.Melee)
                               == SkillTrainingLevel.Master;
            int criticalChance = GetCriticalChance(chance.MeleeEffectiveness, ordinaryHit, masterMelee,
                GetCalledLocationCriticalBonus(request.CalledLocation));
            CombatAttackOutcome outcome = ClassifyAttack(ordinaryHit, criticalRoll, criticalChance);
            bool hit = ordinaryHit;
            int dodgeRoll = 0;
            bool dodged = false;
            if (hit && chance.DodgeChance > 0)
            {
                dodgeRoll = _random.NextInclusive(1, 100);
                dodged = dodgeRoll <= chance.DodgeChance;
                hit = !dodged;
                if (dodged) outcome = CombatAttackOutcome.Miss;
            }
            if (outcome == CombatAttackOutcome.CriticalSuccess
                && source.ObjectType == ObjectType.Npc && targetSource.ObjectType == ObjectType.Pc)
                return AttackFailure(CombatFailure.UnsupportedCriticalEffect, request);

            int rawNormal = 0;
            int mitigatedNormal = 0;
            int rawFatigue = 0;
            int mitigatedFatigue = 0;
            CombatCriticalEffect criticalEffect = CombatCriticalEffect.None;
            int criticalEffectRoll = 0;
            int secondaryCriticalEffectRoll = 0;
            int tertiaryCriticalEffectRoll = 0;
            ArcanumObjectId effectTarget = target;
            CombatActorSource effectTargetSource = targetSource;
            if (outcome == CombatAttackOutcome.CriticalFailure)
            {
                criticalEffectRoll = _random.NextInclusive(1, 100);
                if (criticalEffectRoll <= 50)
                    return AttackFailure(CombatFailure.UnsupportedCriticalEffect, request);
                criticalEffect = CombatCriticalEffect.SelfHit;
                effectTarget = actor;
                effectTargetSource = source;
            }

            bool resolvesDamage = hit || outcome == CombatAttackOutcome.CriticalFailure;
            if (resolvesDamage)
            {
                rawNormal = RollDamage(normalMinimum, normalMaximum);
                rawFatigue = RollDamage(fatigueMinimum, fatigueMaximum);
                int resistance = _world.DerivedStats.GetResistance(effectTarget, CharacterResistance.Normal);
                mitigatedNormal = ApplyResistance(rawNormal, resistance);
                mitigatedFatigue = ApplyResistance(rawFatigue, 3 * resistance / 4);
                if (outcome == CombatAttackOutcome.CriticalSuccess)
                {
                    criticalEffect = ResolveDamageOnlyCritical(out criticalEffectRoll,
                        out secondaryCriticalEffectRoll, out tertiaryCriticalEffectRoll);
                    mitigatedNormal = ApplyCriticalDamageBonus(mitigatedNormal, criticalEffect);
                }
            }

            int hitPointsBefore = _world.Vitality.GetCurrentHitPoints(effectTarget);
            if (resolvesDamage && mitigatedNormal >= hitPointsBefore && effectTargetSource.DyingScriptNum != 0)
                return AttackFailure(CombatFailure.UnresolvedDeathScript, request);

            int spent = Math.Min(CurrentActionPoints, UnarmedAttackActionPointCost);
            int overdrawFatigue = overdraw ? 2 : 0;
            CurrentActionPoints -= spent;
            if (overdrawFatigue > 0) _world.Vitality.ApplyFatigueDamage(actor, overdrawFatigue);
            if (mitigatedNormal > 0) _world.Vitality.ApplyHitPointDamage(effectTarget, mitigatedNormal);
            if (mitigatedFatigue > 0) _world.Vitality.ApplyFatigueDamage(effectTarget, mitigatedFatigue);
            int resultingHitPoints = _world.Vitality.GetCurrentHitPoints(effectTarget);
            int resultingFatigue = _world.Vitality.GetCurrentFatigue(effectTarget);
            if (hitPointsBefore > 0 && resultingHitPoints <= 0)
                _world.DeathConsequences.Process(actor, effectTarget);
            if (CurrentParticipant == actor && CurrentActionPoints == 0)
                AdvanceToNextEligibleParticipant(actor);

            return new CombatAttackResult(CombatFailure.None, chance, hit, dodged, attackRoll, dodgeRoll,
                rawNormal, mitigatedNormal, rawFatigue, mitigatedFatigue,
                resultingHitPoints, resultingFatigue, UnarmedAttackActionPointCost, spent, overdrawFatigue,
                outcome: outcome, criticalRoll: criticalRoll, criticalChance: criticalChance,
                criticalEffect: criticalEffect, criticalEffectRoll: criticalEffectRoll,
                secondaryCriticalEffectRoll: secondaryCriticalEffectRoll,
                tertiaryCriticalEffectRoll: tertiaryCriticalEffectRoll,
                effectTargetIdentity: effectTarget, request: request, modifierLedger: modifiers);
        }

        public CombatHitChance GetBasicRangedHitChance(ArcanumObjectId actor, ArcanumObjectId target,
            Weapon weapon, int distance)
            => BuildRangedHitChance(actor, target, weapon, distance,
                0, CombatCalledLocation.None, out _);

        private CombatHitChance BuildRangedHitChance(ArcanumObjectId actor, ArcanumObjectId target,
            Weapon weapon, int distance, int coverPenalty, CombatCalledLocation calledLocation,
            out CombatAttackModifierLedger ledger)
        {
            if (weapon == null) throw new ArgumentNullException(nameof(weapon));
            CharacterSkill skill = weapon.Skill == WeaponSkill.Bow ? CharacterSkill.Bow : CharacterSkill.Firearms;
            int skillRank = _world.Progression.GetEffectiveSkillRank(actor, skill);
            int effectiveness = checked(5 * skillRank + 25);
            int intelligence = _world.Characters.GetEffectiveAttribute(actor, CharacterAttribute.Intelligence);
            bool intelligenceBonus = intelligence >= 20;
            if (intelligenceBonus) effectiveness += 10;
            int armorClass = _world.DerivedStats.GetArmorClass(target);
            int difficulty = effectiveness * (armorClass / 2) / 100;
            int strength = _world.Characters.GetEffectiveAttribute(actor, CharacterAttribute.Strength);
            int strengthPenalty = strength < weapon.MinStrength
                ? checked(5 * (weapon.MinStrength - strength)) : 0;
            difficulty += strengthPenalty;
            int perception = _world.Characters.GetEffectiveAttribute(actor, CharacterAttribute.Perception);
            int sourceRangePenalty = checked(5 * Math.Max(0, distance - perception / 2));
            bool bowMaster = skill == CharacterSkill.Bow
                             && _world.Progression.GetTrainingLevel(actor, CharacterSkill.Bow)
                             == SkillTrainingLevel.Master;
            int rangePenalty = bowMaster ? 0 : sourceRangePenalty;
            difficulty += rangePenalty;
            difficulty += coverPenalty;
            difficulty -= weapon.BonusToHit;
            int armorDifficulty = effectiveness * (armorClass / 2) / 100;
            int calledPenalty = GetCalledLocationPenalty(calledLocation);
            var modifiers = new[]
            {
                new CombatAttackModifier(CombatAttackModifierStage.BaseEffectiveness,
                    CombatAttackModifierReason.BaseSkill, checked(5 * skillRank + 25), true,
                    sourceValue: skillRank),
                new CombatAttackModifier(CombatAttackModifierStage.Attribute,
                    CombatAttackModifierReason.IntelligenceTwenty, intelligenceBonus ? 10 : 0,
                    intelligenceBonus, sourceValue: intelligence),
                new CombatAttackModifier(CombatAttackModifierStage.TargetDefense,
                    CombatAttackModifierReason.ArmorClass, -armorDifficulty, true,
                    sourceValue: armorClass),
                new CombatAttackModifier(CombatAttackModifierStage.WeaponRequirement,
                    CombatAttackModifierReason.MinimumStrength, -strengthPenalty,
                    strengthPenalty > 0, sourceValue: weapon.MinStrength),
                new CombatAttackModifier(CombatAttackModifierStage.Distance,
                    CombatAttackModifierReason.PerceptionRange, -sourceRangePenalty,
                    sourceRangePenalty > 0, bowMaster && sourceRangePenalty > 0, sourceValue: distance),
                new CombatAttackModifier(CombatAttackModifierStage.Cover,
                    CombatAttackModifierReason.Cover, -coverPenalty,
                    coverPenalty > 0, sourceValue: coverPenalty),
                new CombatAttackModifier(CombatAttackModifierStage.Weapon,
                    CombatAttackModifierReason.WeaponToHit, weapon.BonusToHit,
                    weapon.BonusToHit != 0, sourceValue: weapon.BonusToHit),
                new CombatAttackModifier(CombatAttackModifierStage.CalledLocation,
                    CombatAttackModifierReason.CalledLocation, calledPenalty,
                    IsCalledLocation(calledLocation), sourceValue: (int)calledLocation),
            };
            ledger = new CombatAttackModifierLedger(modifiers);
            int attackChance = ledger.FinalEffectiveValue;
            int dodge = 5 * _world.Progression.GetEffectiveSkillRank(target, CharacterSkill.Dodge);
            if (_world.Characters.GetEffectiveAttribute(target, CharacterAttribute.Intelligence) >= 20)
                dodge += 10;
            dodge = Math.Min(95, Math.Max(0, dodge));
            return new CombatHitChance(effectiveness, armorClass, difficulty, attackChance, dodge);
        }

        private CombatAttackResult AttackRanged(CombatAttackRequest request, CombatActorSource source,
            CombatActorSource targetSource, Vector2Int actorPosition, Vector2Int targetPosition)
        {
            ArcanumObjectId actor = request.Attacker;
            ArcanumObjectId target = request.Target;
            if (!_world.TryGetEquippedItem(actor, WornLocation.Weapon, out PersistentObjectState equipped)
                || equipped.Type != ObjectType.Weapon || equipped.WeaponData == null)
                return AttackFailure(CombatFailure.UnsupportedWeapon, request);
            Weapon weapon = equipped.WeaponData;
            if (weapon.Skill != WeaponSkill.Bow || !weapon.UsesAmmo || weapon.AmmoConsumption < 1)
                return AttackFailure(CombatFailure.UnsupportedWeapon, request);
            int distance = InteractionRangeRules.Distance(actorPosition, targetPosition);
            if (distance > weapon.Range) return AttackFailure(CombatFailure.OutOfRange, request);
            if (_navigationMap == null) return AttackFailure(CombatFailure.NavigationUnavailable, request);
            ProjectileTraversalResult traversal = _navigationMap.GetProjectileTraversal(actorPosition,
                targetPosition);
            if (traversal.IsBlocked)
                return AttackFailure(CombatFailure.LineOfFireBlocked, request);
            if (!_world.TryGetAmmo(actor, weapon.AmmoType, weapon.AmmoConsumption,
                    out PersistentObjectState ammo))
            {
                bool hasOtherAmmo = _world.ChildrenOf(actor).Any(identity =>
                    _world.TryGetObjectState(identity, out PersistentObjectState item)
                    && item.Type == ObjectType.Ammo && item.StackQuantity.GetValueOrDefault() > 0);
                return AttackFailure(hasOtherAmmo ? CombatFailure.IncompatibleAmmo : CombatFailure.NoAmmo, request);
            }

            int actionPointCost = weapon.AttackActionPointCost;
            bool overdraw = CurrentActionPoints < actionPointCost;
            if (overdraw && (source.ObjectType != ObjectType.Pc || CurrentActionPoints <= 0
                             || _world.Vitality.GetCurrentFatigue(actor) <= 1))
                return AttackFailure(CombatFailure.InsufficientActionPoints, request);
            if (!TryGetWeaponDamageRange(weapon, DamageType.Normal, out int normalMinimum,
                    out int normalMaximum)
                || !TryGetWeaponDamageRange(weapon, DamageType.Fatigue, out int fatigueMinimum,
                    out int fatigueMaximum)
                || weapon.DamageMax[(int)DamageType.Poison] != 0
                || weapon.DamageMax[(int)DamageType.Electrical] != 0
                || weapon.DamageMax[(int)DamageType.Fire] != 0)
                return AttackFailure(CombatFailure.UnsupportedDamageProfile, request);

            CombatHitChance chance = BuildRangedHitChance(actor, target, weapon, distance,
                traversal.CoverPenalty, request.CalledLocation, out CombatAttackModifierLedger modifiers);
            int ammoBefore = ammo.StackQuantity.Value;

            int attackRoll = _random.NextInclusive(1, 100);
            bool ordinaryHit = attackRoll <= chance.AttackChance;
            int criticalRoll = _random.NextInclusive(1, 100);
            int criticalChance = GetCriticalChance(chance.MeleeEffectiveness, ordinaryHit,
                calledLocationBonus: GetCalledLocationCriticalBonus(request.CalledLocation));
            CombatAttackOutcome outcome = ClassifyAttack(ordinaryHit, criticalRoll, criticalChance);
            if (outcome == CombatAttackOutcome.CriticalFailure)
                return AttackFailure(CombatFailure.UnsupportedCriticalEffect, request);
            bool hit = ordinaryHit;
            int dodgeRoll = 0;
            bool dodged = false;
            if (hit && chance.DodgeChance > 0)
            {
                dodgeRoll = _random.NextInclusive(1, 100);
                dodged = dodgeRoll <= chance.DodgeChance;
                hit = !dodged;
                if (dodged) outcome = CombatAttackOutcome.Miss;
            }

            int rawNormal = 0;
            int mitigatedNormal = 0;
            int rawFatigue = 0;
            int mitigatedFatigue = 0;
            CombatCriticalEffect criticalEffect = CombatCriticalEffect.None;
            int criticalEffectRoll = 0;
            int secondaryCriticalEffectRoll = 0;
            int tertiaryCriticalEffectRoll = 0;
            if (hit)
            {
                rawNormal = RollDamage(normalMinimum, normalMaximum);
                rawFatigue = RollDamage(fatigueMinimum, fatigueMaximum);
                int resistance = _world.DerivedStats.GetResistance(target, CharacterResistance.Normal);
                mitigatedNormal = ApplyResistance(rawNormal, resistance);
                mitigatedFatigue = ApplyResistance(rawFatigue, 3 * resistance / 4);
                if (outcome == CombatAttackOutcome.CriticalSuccess)
                {
                    criticalEffect = ResolveDamageOnlyCritical(out criticalEffectRoll,
                        out secondaryCriticalEffectRoll, out tertiaryCriticalEffectRoll);
                    mitigatedNormal = ApplyCriticalDamageBonus(mitigatedNormal, criticalEffect);
                }
            }

            int hitPointsBefore = _world.Vitality.GetCurrentHitPoints(target);
            if (hit && mitigatedNormal >= hitPointsBefore && targetSource.DyingScriptNum != 0)
                return AttackFailure(CombatFailure.UnresolvedDeathScript, request);

            int spent = Math.Min(CurrentActionPoints, actionPointCost);
            int previousActionPoints = CurrentActionPoints;
            CurrentActionPoints -= spent;
            if (!_world.ConsumeAmmo(ammo.Identity, weapon.AmmoConsumption, out int ammoAfter))
            {
                CurrentActionPoints = previousActionPoints;
                return AttackFailure(CombatFailure.NoAmmo, request);
            }
            int overdrawFatigue = overdraw ? 2 : 0;
            if (overdrawFatigue > 0) _world.Vitality.ApplyFatigueDamage(actor, overdrawFatigue);
            if (mitigatedNormal > 0) _world.Vitality.ApplyHitPointDamage(target, mitigatedNormal);
            if (mitigatedFatigue > 0) _world.Vitality.ApplyFatigueDamage(target, mitigatedFatigue);
            int resultingHitPoints = _world.Vitality.GetCurrentHitPoints(target);
            int resultingFatigue = _world.Vitality.GetCurrentFatigue(target);
            if (hitPointsBefore > 0 && resultingHitPoints <= 0)
                _world.DeathConsequences.Process(actor, target);
            if (CurrentParticipant == actor && CurrentActionPoints == 0)
                AdvanceToNextEligibleParticipant(actor);

            return new CombatAttackResult(CombatFailure.None, chance, hit, dodged, attackRoll, dodgeRoll,
                rawNormal, mitigatedNormal, rawFatigue, mitigatedFatigue,
                resultingHitPoints, resultingFatigue, actionPointCost, spent, overdrawFatigue,
                equipped.Identity, ammo.Identity, ammoBefore, ammoAfter,
                outcome, criticalRoll, criticalChance, criticalEffect, criticalEffectRoll,
                secondaryCriticalEffectRoll, tertiaryCriticalEffectRoll, target, request, modifiers);
        }

        public CombatResult EndCombat(ArcanumObjectId actor)
        {
            if (!IsActive) return Fail(CombatFailure.Inactive);
            if (_world.PlayerState == null || actor != _world.PlayerState.Identity)
                return Fail(CombatFailure.InvalidActor);
            foreach (CombatParticipant participant in _participants)
            {
                if (participant.Identity == actor || participant.ObjectType != ObjectType.Npc) continue;
                if (_engaged.Contains(participant.Identity)
                    && _sources.TryGetValue(participant.Identity, out CombatActorSource source)
                    && IsSourceHostile(source) && IsEligible(source))
                    return Fail(CombatFailure.HostileParticipantActive);
            }

            Lifecycle = CombatLifecycle.Ending;
            ClearTransient();
            return Success();
        }

        /// <summary>Ordinary navigation must yield to the future AP-aware combat movement consumer.</summary>
        public bool CanUseOrdinaryMovement(ArcanumObjectId actor)
            => !IsActive && _world.PlayerState != null && actor == _world.PlayerState.Identity;

        internal void ResetForWorldChange()
        {
            ClearTransient();
            _sources.Clear();
            _navigationMap = null;
        }

        private bool TryValidateActionActor(ArcanumObjectId actor, out CombatActorSource source,
            out CombatFailure failure)
        {
            source = default;
            if (!IsActive)
            {
                failure = CombatFailure.Inactive;
                return false;
            }
            if (CurrentParticipant != actor)
            {
                failure = CombatFailure.NotCurrentParticipant;
                return false;
            }
            if (!_participants.Any(value => value.Identity == actor)
                || !_sources.TryGetValue(actor, out source))
            {
                failure = CombatFailure.ParticipantNotRegistered;
                return false;
            }
            if (!IsEligible(source))
            {
                failure = CombatFailure.ParticipantUnavailable;
                return false;
            }
            failure = CombatFailure.None;
            return true;
        }

        private bool TryGetCombatPosition(ArcanumObjectId identity, out Vector2Int position)
        {
            position = default;
            if (!_world.TryGetPlacement(identity, out ObjectPlacement placement)
                || placement.Kind != ObjectPlacementKind.World
                || !string.Equals(placement.Sector, _world.SelectedSector, StringComparison.Ordinal))
                return false;
            Vector2 tile = placement.TilePosition;
            if (!Mathf.Approximately(tile.x, Mathf.Round(tile.x))
                || !Mathf.Approximately(tile.y, Mathf.Round(tile.y))) return false;
            position = Vector2Int.RoundToInt(tile);
            return true;
        }

        private bool TryGetUnarmedDamageRange(CombatActorSource source, DamageType type,
            out int minimum, out int maximum)
        {
            bool monstrous = _world.Progression.Get(source.Identity).Source.IsMonstrous;
            if (monstrous)
            {
                minimum = source.GetNaturalDamageMinimum(type);
                maximum = source.GetNaturalDamageMaximum(type);
            }
            else if (type is DamageType.Normal or DamageType.Fatigue)
            {
                minimum = 1;
                maximum = 5;
            }
            else
            {
                minimum = 0;
                maximum = 0;
            }
            if (minimum < 0 || maximum < minimum) return false;

            int massiveDamage = checked(2 * maximum);
            if (type is DamageType.Normal or DamageType.Fatigue)
            {
                int bonus = _world.DerivedStats.GetDerivedStat(source.Identity,
                    CharacterDerivedStat.MeleeDamageBonus);
                minimum = AdjustDamageBound(minimum, bonus);
                maximum = AdjustDamageBound(maximum, bonus);
            }
            minimum = Math.Min(Math.Max(0, minimum), massiveDamage);
            maximum = Math.Min(Math.Max(0, maximum), massiveDamage);
            return maximum >= minimum;
        }

        private static bool TryGetWeaponDamageRange(Weapon weapon, DamageType type,
            out int minimum, out int maximum)
        {
            minimum = weapon.DamageMin[(int)type];
            maximum = weapon.DamageMax[(int)type];
            if (minimum < 0 || maximum < minimum) return false;
            int cap = checked(3 * maximum);
            minimum = Math.Min(Math.Max(0, minimum), cap);
            maximum = Math.Min(Math.Max(0, maximum), cap);
            return maximum >= minimum;
        }

        private static int AdjustDamageBound(int value, int bonus)
            => value <= 0 || value + bonus > 0 ? value + bonus : 1;

        private static bool HasUnsupportedNaturalDamage(CombatActorSource source)
            => source.GetNaturalDamageMaximum(DamageType.Poison) != 0
               || source.GetNaturalDamageMaximum(DamageType.Electrical) != 0
               || source.GetNaturalDamageMaximum(DamageType.Fire) != 0;

        private int RollDamage(int minimum, int maximum)
            => maximum <= 0 ? 0 : _random.NextInclusive(minimum, maximum);

        private static int GetCriticalChance(int effectiveness, bool ordinaryHit,
            bool suppressCriticalFailure = false, int calledLocationBonus = 0)
            => ordinaryHit ? Math.Max(0, effectiveness / 20 + calledLocationBonus)
                : suppressCriticalFailure ? 0 : Math.Max(2, (100 - effectiveness) / 7);

        private static bool IsCalledLocation(CombatCalledLocation location)
            => location != CombatCalledLocation.None;

        private static int GetCalledLocationPenalty(CombatCalledLocation location)
            => location switch
            {
                CombatCalledLocation.None or CombatCalledLocation.Torso => 0,
                CombatCalledLocation.Head => -50,
                CombatCalledLocation.Arm or CombatCalledLocation.Leg => -30,
                _ => throw new ArgumentOutOfRangeException(nameof(location)),
            };

        private static int GetCalledLocationCriticalBonus(CombatCalledLocation location)
            => location switch
            {
                CombatCalledLocation.None or CombatCalledLocation.Torso => 0,
                CombatCalledLocation.Head => 10,
                CombatCalledLocation.Arm or CombatCalledLocation.Leg => 6,
                _ => throw new ArgumentOutOfRangeException(nameof(location)),
            };

        private static CombatAttackOutcome ClassifyAttack(bool ordinaryHit, int criticalRoll,
            int criticalChance)
            => ordinaryHit
                ? criticalRoll <= criticalChance
                    ? CombatAttackOutcome.CriticalSuccess
                    : CombatAttackOutcome.Hit
                : criticalRoll <= criticalChance
                    ? CombatAttackOutcome.CriticalFailure
                    : CombatAttackOutcome.Miss;

        private CombatCriticalEffect ResolveDamageOnlyCritical(out int firstRoll, out int secondRoll,
            out int thirdRoll)
        {
            firstRoll = _random.NextInclusive(1, 100);
            secondRoll = 0;
            thirdRoll = 0;
            if (firstRoll <= 10) return CombatCriticalEffect.BonusDamage200;
            secondRoll = _random.NextInclusive(1, 100);
            if (secondRoll <= 30) return CombatCriticalEffect.BonusDamage100;
            thirdRoll = _random.NextInclusive(1, 100);
            return CombatCriticalEffect.BonusDamage50;
        }

        private static int ApplyCriticalDamageBonus(int damage, CombatCriticalEffect effect)
            => effect switch
            {
                CombatCriticalEffect.BonusDamage200 => checked(damage * 3),
                CombatCriticalEffect.BonusDamage100 => checked(damage * 2),
                CombatCriticalEffect.BonusDamage50 => checked(damage * 3 / 2),
                _ => damage,
            };

        private static int ApplyResistance(int damage, int resistance)
            => resistance > 0 ? damage - resistance * damage / 100 : damage;

        private void AdvanceToNextEligibleParticipant(ArcanumObjectId actor)
        {
            int current = _participants.FindIndex(value => value.Identity == actor);
            if (current < 0 || _participants.Count == 0) return;
            for (int next = current + 1; next < _participants.Count; next++)
            {
                CombatParticipant candidate = _participants[next];
                if (_sources.TryGetValue(candidate.Identity, out CombatActorSource source) && IsEligible(source))
                {
                    BeginParticipantTurn(candidate.Identity, requireActive: false);
                    return;
                }
            }
            CompleteRoundAndBeginNext();
        }

        private bool TryValidateActor(ArcanumObjectId identity, out CombatActorSource source,
            out CombatFailure failure)
        {
            source = default;
            if (_world.PlayerState == null || identity != _world.PlayerState.Identity
                || !_sources.TryGetValue(identity, out source))
            {
                failure = CombatFailure.ActorNotFound;
                return false;
            }
            if (source.ObjectType != ObjectType.Pc)
            {
                failure = CombatFailure.InvalidActor;
                return false;
            }
            if (!IsEligible(source))
            {
                failure = CombatFailure.ParticipantUnavailable;
                return false;
            }
            failure = CombatFailure.None;
            return true;
        }

        private bool TryValidateTarget(ArcanumObjectId identity, out CombatActorSource source,
            out CombatFailure failure)
        {
            if (!_sources.TryGetValue(identity, out source))
            {
                failure = CombatFailure.TargetNotFound;
                return false;
            }
            if (source.ObjectType != ObjectType.Npc)
            {
                failure = CombatFailure.InvalidTarget;
                return false;
            }
            if (!IsEligible(source))
            {
                failure = CombatFailure.ParticipantUnavailable;
                return false;
            }
            failure = CombatFailure.None;
            return true;
        }

        private bool IsEligible(CombatActorSource source)
        {
            if (!_world.TryGetLoadedObject(source.Identity, out WorldObject runtime)
                || runtime.Type != source.ObjectType) return false;
            if (source.ObjectType == ObjectType.Npc
                && (!_world.TryGetObjectState(source.Identity, out PersistentObjectState state) || state.Off))
                return false;
            if (!_world.Vitality.TryGet(source.Identity, out _)
                || _world.Vitality.IsDead(source.Identity)) return false;
            if ((source.CritterFlags & (OcfStunned | OcfParalyzed)) != 0) return false;
            bool fatigueImmune = (source.CritterFlags & (OcfUndead | OcfFatigueImmune)) != 0;
            return !_world.Vitality.IsUnconscious(source.Identity, fatigueImmune);
        }

        private void OnVitalityChanged(CharacterVitalityChange change)
        {
            if (!_sources.TryGetValue(change.Identity, out CombatActorSource source)) return;
            bool fatigueImmune = (source.CritterFlags & (OcfUndead | OcfFatigueImmune)) != 0;
            bool becameDead = change.PreviousHitPoints > 0 && change.CurrentHitPoints <= 0;
            bool becameUnconscious = !fatigueImmune && change.PreviousHitPoints > 0
                && change.CurrentHitPoints > 0 && change.PreviousFatigue > 0 && change.CurrentFatigue <= 0;
            if (!becameDead && !becameUnconscious) return;

            ProjectDefeat(change.Identity, becameDead);
            if (!IsActive) return;
            int index = _participants.FindIndex(value => value.Identity == change.Identity);
            if (index < 0) return;
            if (becameDead)
            {
                bool wasCurrent = CurrentParticipant == change.Identity;
                _participants.RemoveAt(index);
                _engaged.Remove(change.Identity);
                if (_participants.Count == 0)
                {
                    ClearTransient();
                    return;
                }
                if (wasCurrent) BeginNextEligibleFromRemovedIndex(index);
            }
            else if (CurrentParticipant == change.Identity)
            {
                CurrentActionPoints = 0;
                AdvanceToNextEligibleParticipant(change.Identity);
            }
        }

        private void ProjectDefeat(ArcanumObjectId identity, bool dead)
        {
            if (!_world.TryGetLoadedObject(identity, out WorldObject runtime)) return;
            int facing = CritterArtResolver.RotationOf(runtime.ArtId);
            uint fallenArt = CritterArtResolver.WithAnimRotation(runtime.ArtId, 7, facing) & ~(0x1Fu << 14);
            runtime.IsDead = dead;
            runtime.SetArt(fallenArt);
            if (!dead) return;
            runtime.Blocks = false;
            _navigationMap?.SetRegisteredObjectBlocking(identity, false);
        }

        private void BeginNextEligibleFromRemovedIndex(int removedIndex)
        {
            for (int index = removedIndex; index < _participants.Count; index++)
            {
                CombatParticipant candidate = _participants[index];
                if (!_sources.TryGetValue(candidate.Identity, out CombatActorSource source) || !IsEligible(source))
                    continue;
                BeginParticipantTurn(candidate.Identity, requireActive: false);
                return;
            }
            CompleteRoundAndBeginNext();
        }

        private void CompleteRoundAndBeginNext()
        {
            if (!IsActive || _participants.Count == 0)
            {
                ClearTransient();
                return;
            }

            int completedRound = RoundNumber;
            ElapsedCombatTimeMilliseconds = checked(ElapsedCombatTimeMilliseconds
                                                    + RoundBoundaryMilliseconds);
            RoundCompleted?.Invoke(new CombatRoundBoundary(completedRound,
                RoundBoundaryMilliseconds, ElapsedCombatTimeMilliseconds));
            if (!IsActive) return;

            RoundNumber = checked(RoundNumber + 1);
            DiscoverNearbyHostiles();
            foreach (CombatParticipant candidate in _participants)
            {
                if (_sources.TryGetValue(candidate.Identity, out CombatActorSource source) && IsEligible(source))
                {
                    BeginParticipantTurn(candidate.Identity, requireActive: false);
                    return;
                }
            }
            ClearTransient();
        }

        private void DiscoverNearbyHostiles()
        {
            if (_world.PlayerState == null) return;
            DiscoverNearbyHostiles(_world.PlayerState.Identity, _participants, _engaged);
            SortSourceOrder(_participants);
        }

        private void DiscoverNearbyHostiles(ArcanumObjectId pc, List<CombatParticipant> participants,
            HashSet<ArcanumObjectId> engaged)
        {
            if (!TryGetCombatPosition(pc, out Vector2Int pcPosition)) return;
            int perception = _world.Characters.GetEffectiveAttribute(pc, CharacterAttribute.Perception);
            int range = Math.Max(10, perception / 2 + 5);
            foreach (CombatActorSource source in _sources.Values)
            {
                if (source.ObjectType != ObjectType.Npc || !IsSourceHostile(source) || !IsEligible(source)
                    || !TryGetCombatPosition(source.Identity, out Vector2Int position)
                    || Math.Abs(position.x - pcPosition.x) > range
                    || Math.Abs(position.y - pcPosition.y) > range)
                    continue;
                engaged.Add(source.Identity);
                if (participants.All(value => value.Identity != source.Identity))
                    participants.Add(new CombatParticipant(source));
            }
        }

        private static bool IsSourceHostile(CombatActorSource source)
            => source.WillKosScriptNum == 0 && (source.NpcFlags & OnfKos) != 0
               && (source.NpcFlags & OnfNoAttack) == 0;

        private void BeginParticipantTurn(ArcanumObjectId identity, bool requireActive)
        {
            if (requireActive && !IsActive) throw new InvalidOperationException("Combat is inactive.");
            CurrentParticipant = identity;
            int speed = _world.DerivedStats.GetDerivedStat(identity, CharacterDerivedStat.Speed);
            MaximumActionPoints = Math.Max(5, speed);
            CurrentActionPoints = MaximumActionPoints;
        }

        private void ClearTransient()
        {
            _participants.Clear();
            _engaged.Clear();
            CurrentParticipant = default;
            RoundNumber = 0;
            ElapsedCombatTimeMilliseconds = 0;
            CurrentActionPoints = 0;
            MaximumActionPoints = 0;
            LastAttackResult = null;
            Mode = CombatMode.TurnBased;
            Lifecycle = CombatLifecycle.Inactive;
        }

        private static void SortSourceOrder(List<CombatParticipant> participants)
            => participants.Sort((left, right) =>
            {
                int pcOrder = (left.ObjectType == ObjectType.Pc ? 1 : 0)
                    .CompareTo(right.ObjectType == ObjectType.Pc ? 1 : 0);
                if (pcOrder != 0) return pcOrder;
                int sourceOrder = left.SourceOrder.CompareTo(right.SourceOrder);
                return sourceOrder != 0 ? sourceOrder
                    : string.CompareOrdinal(left.Identity.Key, right.Identity.Key);
            });

        private static CombatResult Success() => new(CombatFailure.None);
        private static CombatResult Fail(CombatFailure failure) => new(failure);

        private CombatMoveResult MoveFailure(CombatFailure failure, Vector2Int destination)
        {
            Vector2Int start = TryGetCombatPosition(CurrentParticipant, out Vector2Int current)
                ? current
                : default;
            return new CombatMoveResult(failure, start, destination, start);
        }

        private static CombatAttackResult AttackFailure(CombatFailure failure,
            CombatAttackRequest request = default)
            => new(failure, request: request);

        private static int ClampPercent(int value) => Math.Max(0, Math.Min(100, value));

        private sealed class SystemCombatRandom : ICombatRandom
        {
            private readonly System.Random _random = new();

            public int NextInclusive(int minimum, int maximum)
            {
                if (minimum > maximum) throw new ArgumentOutOfRangeException(nameof(minimum));
                return _random.Next(minimum, checked(maximum + 1));
            }
        }
    }
}
