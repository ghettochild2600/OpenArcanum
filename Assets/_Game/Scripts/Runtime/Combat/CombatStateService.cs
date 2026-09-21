using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.World;

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
    }

    public readonly struct CombatResult
    {
        public bool Succeeded => Failure == CombatFailure.None;
        public CombatFailure Failure { get; }

        public CombatResult(CombatFailure failure) => Failure = failure;
    }

    /// <summary>Immutable source facts used by the transient combat coordinator.</summary>
    public readonly struct CombatActorSource : IEquatable<CombatActorSource>
    {
        public ArcanumObjectId Identity { get; }
        public ObjectType ObjectType { get; }
        public int? PrototypeNumber { get; }
        public string SourceSector { get; }
        public int SourceOrder { get; }
        public int NpcFlags { get; }
        public int CritterFlags { get; }
        public int WillKosScriptNum { get; }

        public CombatActorSource(ArcanumObjectId identity, ObjectType objectType, int? prototypeNumber,
            string sourceSector, int sourceOrder, int npcFlags, int critterFlags, int willKosScriptNum)
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
        }

        public bool Equals(CombatActorSource other)
            => Identity == other.Identity && ObjectType == other.ObjectType
               && PrototypeNumber == other.PrototypeNumber && SourceSector == other.SourceSector
               && SourceOrder == other.SourceOrder && NpcFlags == other.NpcFlags
               && CritterFlags == other.CritterFlags && WillKosScriptNum == other.WillKosScriptNum;

        public override bool Equals(object obj) => obj is CombatActorSource other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Identity, ObjectType, PrototypeNumber, SourceSector,
            SourceOrder, NpcFlags, CritterFlags, WillKosScriptNum);
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
        internal const int OnfKos = 0x00000100;
        internal const int OnfNoAttack = 0x20000000;
        internal const int OcfUndead = 0x00000004;
        internal const int OcfStunned = 0x00000020;
        internal const int OcfParalyzed = 0x00000040;

        private readonly WorldMapSessionCoordinator _world;
        private readonly Dictionary<ArcanumObjectId, CombatActorSource> _sources = new();
        private readonly List<CombatParticipant> _participants = new();

        public CombatLifecycle Lifecycle { get; private set; }
        public CombatMode Mode { get; private set; } = CombatMode.TurnBased;
        public bool IsActive => Lifecycle == CombatLifecycle.Active;
        public IReadOnlyList<CombatParticipant> Participants => _participants;
        public ArcanumObjectId CurrentParticipant { get; private set; }
        public int RoundNumber { get; private set; }
        public int CurrentActionPoints { get; private set; }
        public int MaximumActionPoints { get; private set; }

        public CombatStateService(WorldMapSessionCoordinator world)
            => _world = world ?? throw new ArgumentNullException(nameof(world));

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
            int currentIndex = _participants.FindIndex(value => value.Identity == actor);
            if (currentIndex < 0) return Fail(CombatFailure.ParticipantNotRegistered);
            CurrentActionPoints = 0;
            int next = currentIndex + 1;
            if (next >= _participants.Count)
            {
                next = 0;
                RoundNumber++;
            }
            BeginParticipantTurn(_participants[next].Identity, requireActive: false);
            return Success();
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
    }
}
