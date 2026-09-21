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
        UnsupportedAttackMode,
        UnsupportedWeapon,
        UnsupportedDamageProfile,
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
        public CombatHitChance Chance { get; }

        internal CombatAttackResult(CombatFailure failure, CombatHitChance chance = default,
            bool hit = false, bool dodged = false, int attackRoll = 0, int dodgeRoll = 0,
            int rawHitPointDamage = 0, int mitigatedHitPointDamage = 0,
            int rawFatigueDamage = 0, int mitigatedFatigueDamage = 0,
            int resultingHitPoints = 0, int resultingFatigue = 0,
            int actionPointCost = 0, int actionPointsSpent = 0, int overdrawFatigueDamage = 0)
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

        public CombatActorSource(ArcanumObjectId identity, ObjectType objectType, int? prototypeNumber,
            string sourceSector, int sourceOrder, int npcFlags, int critterFlags, int willKosScriptNum,
            int[] naturalDamage = null)
        {
            if (!identity.IsPersistent)
                throw new ArgumentException("Combat actors require a persistent ObjectID.", nameof(identity));
            if (objectType is not (ObjectType.Pc or ObjectType.Npc))
                throw new ArgumentOutOfRangeException(nameof(objectType));
            if (sourceOrder < 0) throw new ArgumentOutOfRangeException(nameof(sourceOrder));
            Identity = identity;
            ObjectType = objectType;
            PrototypeNumber = prototypeNumber;
            SourceSector = WorldMapSessionCoordinator.NormalizeSector(sourceSector);
            SourceOrder = sourceOrder;
            NpcFlags = npcFlags;
            CritterFlags = critterFlags;
            WillKosScriptNum = willKosScriptNum;
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

    /// <summary>
    /// Session-owned M8A combat state. It owns only transient participants, order, current turn and AP;
    /// character HP/fatigue remain authoritative in <see cref="CharacterVitalityService"/>.
    /// </summary>
    public sealed class CombatStateService
    {
        public const int UnarmedAttackActionPointCost = 5;
        public const int WalkingActionPointCostPerStep = 2;
        public const int RunningActionPointCostPerStep = 1;

        internal const int OnfKos = 0x00000100;
        internal const int OnfNoAttack = 0x20000000;
        internal const int OcfUndead = 0x00000004;
        internal const int OcfStunned = 0x00000020;
        internal const int OcfParalyzed = 0x00000040;

        private readonly WorldMapSessionCoordinator _world;
        private readonly Dictionary<ArcanumObjectId, CombatActorSource> _sources = new();
        private readonly List<CombatParticipant> _participants = new();
        private readonly DeterministicTilePathfinder _pathfinder = new();
        private readonly List<Vector2Int> _route = new();
        private SectorNavigationMap _navigationMap;
        private ICombatRandom _random;

        public CombatLifecycle Lifecycle { get; private set; }
        public CombatMode Mode { get; private set; } = CombatMode.TurnBased;
        public bool IsActive => Lifecycle == CombatLifecycle.Active;
        public IReadOnlyList<CombatParticipant> Participants => _participants;
        public ArcanumObjectId CurrentParticipant { get; private set; }
        public int RoundNumber { get; private set; }
        public int CurrentActionPoints { get; private set; }
        public int MaximumActionPoints { get; private set; }

        public CombatStateService(WorldMapSessionCoordinator world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _random = new SystemCombatRandom();
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
                    && existing.WillKosScriptNum == source.WillKosScriptNum)
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
            SortSourceOrder(pending);

            Lifecycle = CombatLifecycle.Starting;
            _world.Dialogue.Cancel("Combat started.");
            _participants.AddRange(pending);
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
            SortSourceOrder(_participants);
            return Success();
        }

        public CombatResult RemoveParticipant(ArcanumObjectId identity)
        {
            if (!IsActive) return Fail(CombatFailure.Inactive);
            int index = _participants.FindIndex(value => value.Identity == identity);
            if (index < 0) return Fail(CombatFailure.ParticipantNotRegistered);
            bool wasCurrent = CurrentParticipant == identity;
            _participants.RemoveAt(index);
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
            if (CurrentActionPoints == 0) AdvanceToNextEligibleParticipant(actor);
            return new CombatMoveResult(CombatFailure.None, start, destination, finalPosition,
                _route.Count, steps, spent, fatigueDamage);
        }

        public CombatHitChance GetBasicMeleeHitChance(ArcanumObjectId actor, ArcanumObjectId target)
        {
            int melee = _world.Progression.GetEffectiveSkillRank(actor, CharacterSkill.Melee);
            int effectiveness = checked(5 * melee + 25);
            if (_world.Characters.GetEffectiveAttribute(actor, CharacterAttribute.Intelligence) >= 20)
                effectiveness += 10;
            int armorClass = _world.DerivedStats.GetArmorClass(target);
            int difficulty = effectiveness * (armorClass / 2) / 100;
            int attackChance = ClampPercent(effectiveness - difficulty);
            int dodge = 5 * _world.Progression.GetEffectiveSkillRank(target, CharacterSkill.Dodge);
            if (_world.Characters.GetEffectiveAttribute(target, CharacterAttribute.Intelligence) >= 20)
                dodge += 10;
            dodge = Math.Min(95, Math.Max(0, dodge));
            return new CombatHitChance(effectiveness, armorClass, difficulty, attackChance, dodge);
        }

        public CombatAttackResult Attack(ArcanumObjectId actor, ArcanumObjectId target,
            CombatAttackMode mode = CombatAttackMode.BasicMelee)
        {
            if (!TryValidateActionActor(actor, out CombatActorSource source, out CombatFailure failure))
                return AttackFailure(failure);
            if (!Enum.IsDefined(typeof(CombatAttackMode), mode) || mode != CombatAttackMode.BasicMelee)
                return AttackFailure(CombatFailure.UnsupportedAttackMode);
            if (actor == target) return AttackFailure(CombatFailure.SameParticipant);
            if (!_participants.Any(value => value.Identity == target))
                return AttackFailure(CombatFailure.ParticipantNotRegistered);
            if (!_sources.TryGetValue(target, out CombatActorSource targetSource))
                return AttackFailure(CombatFailure.TargetNotFound);
            if (!IsEligible(targetSource)) return AttackFailure(CombatFailure.ParticipantUnavailable);
            if (_world.TryGetEquippedItem(actor, WornLocation.Weapon, out _))
                return AttackFailure(CombatFailure.UnsupportedWeapon);
            if (!TryGetCombatPosition(actor, out Vector2Int actorPosition)
                || !TryGetCombatPosition(target, out Vector2Int targetPosition))
                return AttackFailure(CombatFailure.PresentationUnavailable);
            if (InteractionRangeRules.Distance(actorPosition, targetPosition) > 1)
                return AttackFailure(CombatFailure.OutOfRange);

            bool overdraw = CurrentActionPoints < UnarmedAttackActionPointCost;
            if (overdraw && (source.ObjectType != ObjectType.Pc || CurrentActionPoints <= 0
                             || _world.Vitality.GetCurrentFatigue(actor) <= 1))
                return AttackFailure(CombatFailure.InsufficientActionPoints);
            if (!TryGetUnarmedDamageRange(source, DamageType.Normal, out int normalMinimum,
                    out int normalMaximum)
                || !TryGetUnarmedDamageRange(source, DamageType.Fatigue, out int fatigueMinimum,
                    out int fatigueMaximum)
                || HasUnsupportedNaturalDamage(source))
                return AttackFailure(CombatFailure.UnsupportedDamageProfile);

            CombatHitChance chance = GetBasicMeleeHitChance(actor, target);
            int attackRoll = _random.NextInclusive(1, 100);
            bool hit = attackRoll <= chance.AttackChance;
            int dodgeRoll = 0;
            bool dodged = false;
            if (hit && chance.DodgeChance > 0)
            {
                dodgeRoll = _random.NextInclusive(1, 100);
                dodged = dodgeRoll <= chance.DodgeChance;
                hit = !dodged;
            }

            int rawNormal = 0;
            int mitigatedNormal = 0;
            int rawFatigue = 0;
            int mitigatedFatigue = 0;
            if (hit)
            {
                rawNormal = RollDamage(normalMinimum, normalMaximum);
                rawFatigue = RollDamage(fatigueMinimum, fatigueMaximum);
                int resistance = _world.DerivedStats.GetResistance(target, CharacterResistance.Normal);
                mitigatedNormal = ApplyResistance(rawNormal, resistance);
                mitigatedFatigue = ApplyResistance(rawFatigue, 3 * resistance / 4);
            }

            int spent = Math.Min(CurrentActionPoints, UnarmedAttackActionPointCost);
            int overdrawFatigue = overdraw ? 2 : 0;
            CurrentActionPoints -= spent;
            if (overdrawFatigue > 0) _world.Vitality.ApplyFatigueDamage(actor, overdrawFatigue);
            if (mitigatedNormal > 0) _world.Vitality.ApplyHitPointDamage(target, mitigatedNormal);
            if (mitigatedFatigue > 0) _world.Vitality.ApplyFatigueDamage(target, mitigatedFatigue);
            int resultingHitPoints = _world.Vitality.GetCurrentHitPoints(target);
            int resultingFatigue = _world.Vitality.GetCurrentFatigue(target);
            if (CurrentActionPoints == 0) AdvanceToNextEligibleParticipant(actor);

            return new CombatAttackResult(CombatFailure.None, chance, hit, dodged, attackRoll, dodgeRoll,
                rawNormal, mitigatedNormal, rawFatigue, mitigatedFatigue,
                resultingHitPoints, resultingFatigue, UnarmedAttackActionPointCost, spent, overdrawFatigue);
        }

        public CombatResult EndCombat(ArcanumObjectId actor)
        {
            if (!IsActive) return Fail(CombatFailure.Inactive);
            if (_world.PlayerState == null || actor != _world.PlayerState.Identity)
                return Fail(CombatFailure.InvalidActor);
            foreach (CombatParticipant participant in _participants)
            {
                if (participant.Identity == actor || participant.ObjectType != ObjectType.Npc) continue;
                if (_sources.TryGetValue(participant.Identity, out CombatActorSource source)
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

        private static int AdjustDamageBound(int value, int bonus)
            => value <= 0 || value + bonus > 0 ? value + bonus : 1;

        private static bool HasUnsupportedNaturalDamage(CombatActorSource source)
            => source.GetNaturalDamageMaximum(DamageType.Poison) != 0
               || source.GetNaturalDamageMaximum(DamageType.Electrical) != 0
               || source.GetNaturalDamageMaximum(DamageType.Fire) != 0;

        private int RollDamage(int minimum, int maximum)
            => maximum <= 0 ? 0 : _random.NextInclusive(minimum, maximum);

        private static int ApplyResistance(int damage, int resistance)
            => resistance > 0 ? damage - resistance * damage / 100 : damage;

        private void AdvanceToNextEligibleParticipant(ArcanumObjectId actor)
        {
            int current = _participants.FindIndex(value => value.Identity == actor);
            if (current < 0 || _participants.Count == 0) return;
            for (int offset = 1; offset <= _participants.Count; offset++)
            {
                int absolute = current + offset;
                int next = absolute % _participants.Count;
                CombatParticipant candidate = _participants[next];
                if (_sources.TryGetValue(candidate.Identity, out CombatActorSource source) && IsEligible(source))
                {
                    if (absolute >= _participants.Count) RoundNumber++;
                    BeginParticipantTurn(candidate.Identity, requireActive: false);
                    return;
                }
            }
            ClearTransient();
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
            if (!_world.Vitality.TryGet(source.Identity, out PersistentCharacterVitalityState vitality)
                || vitality.CurrentHitPoints <= 0) return false;
            if ((source.CritterFlags & (OcfStunned | OcfParalyzed)) != 0) return false;
            return (source.CritterFlags & OcfUndead) != 0 || vitality.CurrentFatigue > 0;
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
            CurrentParticipant = default;
            RoundNumber = 0;
            CurrentActionPoints = 0;
            MaximumActionPoints = 0;
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

        private static CombatAttackResult AttackFailure(CombatFailure failure)
            => new(failure);

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
