using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using UnityEngine;

namespace Arcanum.Runtime.Combat
{
    public enum CombatRealTimeActionKind
    {
        Move,
        MeleeAttack,
        RangedAttack,
    }

    /// <summary>
    /// Source-animation timing needed by combat authority. Implementations read original ART metadata;
    /// presentation components are deliberately not part of this contract.
    /// </summary>
    public readonly struct CombatRealTimeTimingRequest
    {
        public ArcanumObjectId Actor { get; }
        public CombatRealTimeActionKind Kind { get; }
        public int RouteSteps { get; }
        public bool Running { get; }
        public CombatAttackRequest Attack { get; }

        public CombatRealTimeTimingRequest(ArcanumObjectId actor, CombatRealTimeActionKind kind,
            int routeSteps = 0, bool running = false, CombatAttackRequest attack = default)
        {
            Actor = actor;
            Kind = kind;
            RouteSteps = routeSteps;
            Running = running;
            Attack = attack;
        }
    }

    public readonly struct CombatRealTimeTiming
    {
        public int EffectDelayMilliseconds { get; }
        public int ReadyDelayMilliseconds { get; }
        public int SourceFrameIntervalMilliseconds { get; }
        public int SourceActionFrame { get; }
        public int SourceFrameCount { get; }

        public CombatRealTimeTiming(int effectDelayMilliseconds, int readyDelayMilliseconds,
            int sourceFrameIntervalMilliseconds, int sourceActionFrame, int sourceFrameCount)
        {
            if (effectDelayMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(effectDelayMilliseconds));
            if (readyDelayMilliseconds < effectDelayMilliseconds)
                throw new ArgumentOutOfRangeException(nameof(readyDelayMilliseconds));
            if (sourceFrameIntervalMilliseconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(sourceFrameIntervalMilliseconds));
            if (sourceActionFrame < 0) throw new ArgumentOutOfRangeException(nameof(sourceActionFrame));
            if (sourceFrameCount <= 0) throw new ArgumentOutOfRangeException(nameof(sourceFrameCount));
            EffectDelayMilliseconds = effectDelayMilliseconds;
            ReadyDelayMilliseconds = readyDelayMilliseconds;
            SourceFrameIntervalMilliseconds = sourceFrameIntervalMilliseconds;
            SourceActionFrame = sourceActionFrame;
            SourceFrameCount = sourceFrameCount;
        }
    }

    public interface ICombatRealTimeTimingSource
    {
        bool TryGetTiming(CombatRealTimeTimingRequest request, out CombatRealTimeTiming timing);
    }

    internal enum CombatRealTimeSourceTimingProfile
    {
        Walk,
        Run,
        UnarmedAttack,
        BowAttack,
    }

    /// <summary>Exact bounded timing arithmetic audited from the original animation goals.</summary>
    internal static class CombatRealTimeSourceTiming
    {
        public static int AdjustedFramesPerSecond(CombatRealTimeSourceTimingProfile profile,
            int speed, bool smallBody = false)
        {
            int normal;
            int low;
            int high;
            int bodyOffset = 0;
            switch (profile)
            {
                case CombatRealTimeSourceTimingProfile.Walk:
                    normal = 17;
                    low = 6;
                    high = 30;
                    bodyOffset = smallBody ? 4 : 0;
                    break;
                case CombatRealTimeSourceTimingProfile.Run:
                    normal = 20;
                    low = 8;
                    high = 30;
                    bodyOffset = smallBody ? 4 : 0;
                    break;
                case CombatRealTimeSourceTimingProfile.UnarmedAttack:
                    normal = 15;
                    low = 7;
                    high = 23;
                    break;
                case CombatRealTimeSourceTimingProfile.BowAttack:
                    normal = 10;
                    low = 6;
                    high = 14;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(profile));
            }

            normal += bodyOffset;
            low += bodyOffset;
            high += bodyOffset;
            if (speed < 8) return low + speed * (normal - low) / 8;
            if (speed > 8) return speed < 30
                ? normal + speed * (high - normal) / 30
                : high;
            return normal;
        }

        public static int FrameIntervalMilliseconds(int framesPerSecond)
        {
            if (framesPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
            return ClampFrameDelay(1000 / framesPerSecond);
        }

        public static int ApplyWeaponSpeed(int frameIntervalMilliseconds, int weaponSpeed)
            => ClampFrameDelay(frameIntervalMilliseconds - 10 * (weaponSpeed - 10));

        private static int ClampFrameDelay(int milliseconds)
            => Math.Min(800, Math.Max(30, milliseconds));
    }

    public readonly struct CombatRealTimeActionState
    {
        public ArcanumObjectId Actor { get; }
        public CombatRealTimeActionKind Kind { get; }
        public long StartedAtMilliseconds { get; }
        public long EffectAtMilliseconds { get; }
        public long ReadyAtMilliseconds { get; }
        public bool EffectResolved { get; }
        public CombatAttackRequest Attack { get; }
        public Vector2Int Destination { get; }
        public bool Running { get; }

        internal CombatRealTimeActionState(ArcanumObjectId actor, CombatRealTimeActionKind kind,
            long startedAtMilliseconds, long effectAtMilliseconds, long readyAtMilliseconds,
            bool effectResolved, CombatAttackRequest attack, Vector2Int destination, bool running)
        {
            Actor = actor;
            Kind = kind;
            StartedAtMilliseconds = startedAtMilliseconds;
            EffectAtMilliseconds = effectAtMilliseconds;
            ReadyAtMilliseconds = readyAtMilliseconds;
            EffectResolved = effectResolved;
            Attack = attack;
            Destination = destination;
            Running = running;
        }
    }

    public readonly struct CombatRealTimeActorState
    {
        public ArcanumObjectId Actor { get; }
        public long ReadyAtMilliseconds { get; }
        public bool IsReady { get; }
        public bool IsSuspended { get; }
        public bool HasPendingAction { get; }
        public CombatRealTimeActionState PendingAction { get; }

        internal CombatRealTimeActorState(ArcanumObjectId actor, long readyAtMilliseconds,
            bool isReady, bool isSuspended, bool hasPendingAction,
            CombatRealTimeActionState pendingAction)
        {
            Actor = actor;
            ReadyAtMilliseconds = readyAtMilliseconds;
            IsReady = isReady;
            IsSuspended = isSuspended;
            HasPendingAction = hasPendingAction;
            PendingAction = pendingAction;
        }
    }

    public readonly struct CombatRealTimeActionResolution
    {
        public CombatRealTimeActionState Action { get; }
        public CombatFailure Failure { get; }
        public bool Succeeded => Failure == CombatFailure.None;
        public CombatAttackResult? AttackResult { get; }
        public CombatMoveResult? MoveResult { get; }

        internal CombatRealTimeActionResolution(CombatRealTimeActionState action,
            CombatFailure failure, CombatAttackResult? attackResult = null,
            CombatMoveResult? moveResult = null)
        {
            Action = action;
            Failure = failure;
            AttackResult = attackResult;
            MoveResult = moveResult;
        }
    }

    public sealed partial class CombatStateService
    {
        private sealed class RealTimeActor
        {
            public long ReadyAt;
            public bool Suspended;
            public PendingAction Pending;
        }

        private sealed class PendingAction
        {
            public ArcanumObjectId Actor;
            public CombatRealTimeActionKind Kind;
            public long StartedAt;
            public long EffectAt;
            public long ReadyAt;
            public bool EffectResolved;
            public CombatAttackRequest Attack;
            public Vector2Int Destination;
            public bool Running;

            public CombatRealTimeActionState Snapshot => new(Actor, Kind, StartedAt, EffectAt,
                ReadyAt, EffectResolved, Attack, Destination, Running);
        }

        private readonly Dictionary<ArcanumObjectId, RealTimeActor> _realTimeActors = new();
        private ICombatRealTimeTimingSource _realTimeTimingSource;
        private long _nextRealTimeBoundaryMilliseconds;
        private bool _resolvingRealTimeAction;

        public CombatRealTimeActionResolution? LastRealTimeActionResolution { get; private set; }
        public event Action<CombatRealTimeActionResolution> RealTimeActionResolved;

        public void BindRealTimeTimingSource(ICombatRealTimeTimingSource source)
            => _realTimeTimingSource = source;

        public bool TryGetRealTimeActorState(ArcanumObjectId actor,
            out CombatRealTimeActorState state)
        {
            if (_realTimeActors.TryGetValue(actor, out RealTimeActor mutable))
            {
                state = new CombatRealTimeActorState(actor, mutable.ReadyAt,
                    IsActive && Mode == CombatMode.RealTime && !mutable.Suspended
                    && mutable.Pending == null && ElapsedCombatTimeMilliseconds >= mutable.ReadyAt,
                    mutable.Suspended, mutable.Pending != null,
                    mutable.Pending?.Snapshot ?? default);
                return true;
            }
            state = default;
            return false;
        }

        public CombatResult ScheduleRealTimeAttack(CombatAttackRequest request)
        {
            if (!TryPrepareRealTimeActor(request.Attacker, out RealTimeActor actor,
                    out CombatResult failure)) return failure;
            if (!Enum.IsDefined(typeof(CombatAttackMode), request.Mode))
                return Fail(CombatFailure.UnsupportedAttackMode);
            if (!Enum.IsDefined(typeof(CombatCalledLocation), request.CalledLocation))
                return Fail(CombatFailure.InvalidCalledLocation);
            if (request.Attacker == request.Target) return Fail(CombatFailure.SameParticipant);
            if (!_participants.Any(value => value.Identity == request.Target))
                return Fail(CombatFailure.ParticipantNotRegistered);
            if (!_sources.TryGetValue(request.Target, out CombatActorSource target))
                return Fail(CombatFailure.TargetNotFound);
            if (!IsEligible(target)) return Fail(CombatFailure.ParticipantUnavailable);

            CombatRealTimeActionKind kind = request.Mode == CombatAttackMode.BasicRanged
                ? CombatRealTimeActionKind.RangedAttack
                : CombatRealTimeActionKind.MeleeAttack;
            var timingRequest = new CombatRealTimeTimingRequest(request.Attacker, kind,
                attack: request);
            if (_realTimeTimingSource == null
                || !_realTimeTimingSource.TryGetTiming(timingRequest, out CombatRealTimeTiming timing))
                return Fail(CombatFailure.TimingUnavailable);
            StartRealTimeAction(actor, request.Attacker, kind, timing, request, default, false);
            return Success();
        }

        public CombatResult ScheduleRealTimeMove(ArcanumObjectId actorIdentity,
            Vector2Int destination, bool pcAlwaysRun = false)
        {
            if (!TryPrepareRealTimeActor(actorIdentity, out RealTimeActor actor,
                    out CombatResult failure)) return failure;
            if (!_sources.TryGetValue(actorIdentity, out CombatActorSource source))
                return Fail(CombatFailure.ParticipantNotRegistered);
            if (pcAlwaysRun && source.ObjectType != ObjectType.Pc)
                return Fail(CombatFailure.InvalidActor);
            if (_navigationMap == null) return Fail(CombatFailure.NavigationUnavailable);
            if (!TryGetCombatPosition(actorIdentity, out Vector2Int start))
                return Fail(CombatFailure.PresentationUnavailable);
            if (!_navigationMap.Contains(destination) || !_navigationMap.IsWalkable(destination)
                || _navigationMap.IsOccupiedByOther(actorIdentity, destination))
                return Fail(CombatFailure.InvalidDestination);
            _route.Clear();
            if (!_pathfinder.TryFindPath(_navigationMap, start, destination, _route,
                    tile => !_navigationMap.IsOccupiedByOther(actorIdentity, tile)))
                return Fail(CombatFailure.Unreachable);
            if (_route.Count == 0) return Success();

            var timingRequest = new CombatRealTimeTimingRequest(actorIdentity,
                CombatRealTimeActionKind.Move, _route.Count, pcAlwaysRun);
            if (_realTimeTimingSource == null
                || !_realTimeTimingSource.TryGetTiming(timingRequest, out CombatRealTimeTiming timing))
                return Fail(CombatFailure.TimingUnavailable);
            StartRealTimeAction(actor, actorIdentity, CombatRealTimeActionKind.Move, timing,
                default, destination, pcAlwaysRun);
            return Success();
        }

        public CombatResult AdvanceRealTime(int elapsedSourceMilliseconds)
        {
            if (!IsActive) return Fail(CombatFailure.Inactive);
            if (Mode != CombatMode.RealTime) return Fail(CombatFailure.UnsupportedMode);
            if (elapsedSourceMilliseconds < 0) return Fail(CombatFailure.InvalidTimeAdvance);
            long target;
            try
            {
                target = checked(ElapsedCombatTimeMilliseconds + elapsedSourceMilliseconds);
            }
            catch (OverflowException)
            {
                return Fail(CombatFailure.InvalidTimeAdvance);
            }

            while (IsActive && Mode == CombatMode.RealTime)
            {
                long next = NextRealTimeEventAt(target);
                if (next > target) break;
                ElapsedCombatTimeMilliseconds = next;

                if (_nextRealTimeBoundaryMilliseconds == next)
                    ProcessRealTimeBoundary();
                ResolveDueRealTimeEffects(next);
                ReleaseReadyRealTimeActors(next);

                if (next == target && NextRealTimeEventAt(target) == next)
                    break;
            }
            if (IsActive && Mode == CombatMode.RealTime)
                ElapsedCombatTimeMilliseconds = target;
            return Success();
        }

        private void InitializeRealTimeCombat()
        {
            CurrentParticipant = default;
            CurrentActionPoints = 0;
            MaximumActionPoints = 0;
            ElapsedCombatTimeMilliseconds = 0;
            _nextRealTimeBoundaryMilliseconds = RoundBoundaryMilliseconds;
            SynchronizeRealTimeActors();
        }

        private void SynchronizeRealTimeActors()
        {
            if (Mode != CombatMode.RealTime) return;
            var enrolled = new HashSet<ArcanumObjectId>(_participants.Select(value => value.Identity));
            foreach (ArcanumObjectId removed in _realTimeActors.Keys.Where(value => !enrolled.Contains(value)).ToArray())
                _realTimeActors.Remove(removed);
            foreach (CombatParticipant participant in _participants)
            {
                if (_realTimeActors.ContainsKey(participant.Identity)) continue;
                if (!_sources.TryGetValue(participant.Identity, out CombatActorSource source)
                    || !IsEligible(source)) continue;
                _realTimeActors.Add(participant.Identity,
                    new RealTimeActor { ReadyAt = ElapsedCombatTimeMilliseconds });
            }
        }

        private bool TryPrepareRealTimeActor(ArcanumObjectId identity, out RealTimeActor actor,
            out CombatResult failure)
        {
            actor = null;
            if (!IsActive)
            {
                failure = Fail(CombatFailure.Inactive);
                return false;
            }
            if (Mode != CombatMode.RealTime)
            {
                failure = Fail(CombatFailure.UnsupportedMode);
                return false;
            }
            if (!_realTimeActors.TryGetValue(identity, out actor)
                || !_sources.TryGetValue(identity, out CombatActorSource source)
                || !IsEligible(source))
            {
                failure = Fail(CombatFailure.ParticipantUnavailable);
                return false;
            }
            if (actor.Suspended)
            {
                failure = Fail(CombatFailure.ParticipantUnavailable);
                return false;
            }
            if (actor.Pending != null)
            {
                failure = Fail(CombatFailure.ActorBusy);
                return false;
            }
            if (ElapsedCombatTimeMilliseconds < actor.ReadyAt)
            {
                failure = Fail(CombatFailure.ActorNotReady);
                return false;
            }
            failure = Success();
            return true;
        }

        private void StartRealTimeAction(RealTimeActor actor, ArcanumObjectId identity,
            CombatRealTimeActionKind kind, CombatRealTimeTiming timing, CombatAttackRequest attack,
            Vector2Int destination, bool running)
        {
            long effectAt = checked(ElapsedCombatTimeMilliseconds + timing.EffectDelayMilliseconds);
            long readyAt = checked(ElapsedCombatTimeMilliseconds + timing.ReadyDelayMilliseconds);
            actor.ReadyAt = readyAt;
            actor.Pending = new PendingAction
            {
                Actor = identity,
                Kind = kind,
                StartedAt = ElapsedCombatTimeMilliseconds,
                EffectAt = effectAt,
                ReadyAt = readyAt,
                Attack = attack,
                Destination = destination,
                Running = running,
            };
        }

        private long NextRealTimeEventAt(long target)
        {
            long next = _nextRealTimeBoundaryMilliseconds <= target
                ? _nextRealTimeBoundaryMilliseconds
                : checked(target + 1);
            foreach (RealTimeActor actor in _realTimeActors.Values)
            {
                PendingAction pending = actor.Pending;
                if (pending == null) continue;
                if (!pending.EffectResolved && pending.EffectAt <= target)
                    next = Math.Min(next, pending.EffectAt);
                if (pending.ReadyAt <= target)
                    next = Math.Min(next, pending.ReadyAt);
            }
            return next;
        }

        private void ProcessRealTimeBoundary()
        {
            int completedRound = RoundNumber;
            RoundCompleted?.Invoke(new CombatRoundBoundary(completedRound,
                RoundBoundaryMilliseconds, _nextRealTimeBoundaryMilliseconds));
            if (!IsActive || Mode != CombatMode.RealTime) return;
            RoundNumber = checked(RoundNumber + 1);
            DiscoverNearbyHostiles();
            SynchronizeRealTimeActors();
            _nextRealTimeBoundaryMilliseconds = checked(_nextRealTimeBoundaryMilliseconds
                                                        + RoundBoundaryMilliseconds);
        }

        private void ResolveDueRealTimeEffects(long now)
        {
            PendingAction[] due = _realTimeActors.Values
                .Select(value => value.Pending)
                .Where(value => value != null && !value.EffectResolved && value.EffectAt <= now)
                .OrderBy(value => ParticipantOrder(value.Actor))
                .ThenBy(value => value.Actor.Key, StringComparer.Ordinal)
                .ToArray();
            foreach (PendingAction pending in due)
            {
                if (!IsActive || Mode != CombatMode.RealTime) return;
                if (!_realTimeActors.TryGetValue(pending.Actor, out RealTimeActor actor)
                    || actor.Pending != pending) continue;
                ResolveRealTimeAction(actor, pending);
            }
        }

        private void ResolveRealTimeAction(RealTimeActor actor, PendingAction pending)
        {
            pending.EffectResolved = true;
            CombatFailure failure;
            CombatAttackResult? attackResult = null;
            CombatMoveResult? moveResult = null;
            if (!_sources.TryGetValue(pending.Actor, out CombatActorSource source) || !IsEligible(source))
            {
                failure = CombatFailure.ParticipantUnavailable;
            }
            else
            {
                _resolvingRealTimeAction = true;
                try
                {
                    if (pending.Kind == CombatRealTimeActionKind.Move)
                    {
                        CombatMoveResult result = MoveInCombat(pending.Actor, pending.Destination,
                            pending.Running);
                        moveResult = result;
                        failure = result.Failure;
                    }
                    else
                    {
                        CombatAttackResult result = Attack(pending.Attack);
                        attackResult = result;
                        failure = result.Failure;
                    }
                }
                finally
                {
                    _resolvingRealTimeAction = false;
                }
            }

            CombatRealTimeActionState snapshot = pending.Snapshot;
            var resolution = new CombatRealTimeActionResolution(snapshot, failure,
                attackResult, moveResult);
            LastRealTimeActionResolution = resolution;
            RealTimeActionResolved?.Invoke(resolution);
            if (failure != CombatFailure.None
                && _realTimeActors.TryGetValue(pending.Actor, out RealTimeActor current)
                && current.Pending == pending)
            {
                current.Pending = null;
                current.ReadyAt = ElapsedCombatTimeMilliseconds;
            }
        }

        private void ReleaseReadyRealTimeActors(long now)
        {
            foreach (RealTimeActor actor in _realTimeActors.Values)
            {
                if (actor.Pending == null || actor.Pending.ReadyAt > now) continue;
                if (!actor.Pending.EffectResolved) continue;
                actor.Pending = null;
            }
        }

        private int ParticipantOrder(ArcanumObjectId identity)
        {
            int index = _participants.FindIndex(value => value.Identity == identity);
            return index >= 0 ? index : int.MaxValue;
        }

        private void RemoveRealTimeActor(ArcanumObjectId identity)
            => _realTimeActors.Remove(identity);

        private void SuspendRealTimeActor(ArcanumObjectId identity)
        {
            if (!_realTimeActors.TryGetValue(identity, out RealTimeActor actor)) return;
            actor.Pending = null;
            actor.Suspended = true;
            actor.ReadyAt = long.MaxValue;
        }

        private void ClearRealTimeTransient()
        {
            _realTimeActors.Clear();
            _nextRealTimeBoundaryMilliseconds = 0;
            _resolvingRealTimeAction = false;
            LastRealTimeActionResolution = null;
        }
    }
}
