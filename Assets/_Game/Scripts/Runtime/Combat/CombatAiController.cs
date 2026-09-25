using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.World;
using UnityEngine;
using CombatActionFailure = Arcanum.Runtime.Combat.CombatFailure;
using PersistentObjectId = Arcanum.Formats.Objects.ArcanumObjectId;

namespace Arcanum.Runtime.Combat
{
    public enum CombatAiActionKind
    {
        None,
        Attack,
        Move,
        Yield,
        Busy,
        NoTarget
    }

    public readonly struct CombatAiApproachPreview
    {
        public CombatAiApproachPreview(
            bool succeeded,
            CombatActionFailure failure,
            PersistentObjectId actor,
            PersistentObjectId target,
            CombatAttackMode attackMode,
            Vector2Int start,
            Vector2Int destination,
            int fullRouteStepCount)
        {
            Succeeded = succeeded;
            Failure = failure;
            Actor = actor;
            Target = target;
            AttackMode = attackMode;
            Start = start;
            Destination = destination;
            FullRouteStepCount = fullRouteStepCount;
        }

        public bool Succeeded { get; }
        public CombatActionFailure Failure { get; }
        public PersistentObjectId Actor { get; }
        public PersistentObjectId Target { get; }
        public CombatAttackMode AttackMode { get; }
        public Vector2Int Start { get; }
        public Vector2Int Destination { get; }
        public int FullRouteStepCount { get; }
    }

    public readonly struct CombatAiDecision
    {
        public CombatAiDecision(
            CombatAiActionKind action,
            CombatActionFailure failure,
            PersistentObjectId actor,
            PersistentObjectId target,
            CombatAttackMode attackMode,
            CombatAttackResult? attackResult,
            CombatMoveResult? moveResult,
            bool scheduled)
        {
            Action = action;
            Failure = failure;
            Actor = actor;
            Target = target;
            AttackMode = attackMode;
            AttackResult = attackResult;
            MoveResult = moveResult;
            Scheduled = scheduled;
        }

        public CombatAiActionKind Action { get; }
        public CombatActionFailure Failure { get; }
        public PersistentObjectId Actor { get; }
        public PersistentObjectId Target { get; }
        public CombatAttackMode AttackMode { get; }
        public CombatAttackResult? AttackResult { get; }
        public CombatMoveResult? MoveResult { get; }
        public bool Scheduled { get; }
    }

    public readonly struct CombatAiTurnResult
    {
        public CombatAiTurnResult(
            PersistentObjectId actor,
            int attacks,
            int moves,
            bool endedTurn,
            bool safetyBoundReached,
            CombatAiDecision lastDecision)
        {
            Actor = actor;
            Attacks = attacks;
            Moves = moves;
            EndedTurn = endedTurn;
            SafetyBoundReached = safetyBoundReached;
            LastDecision = lastDecision;
        }

        public PersistentObjectId Actor { get; }
        public int Attacks { get; }
        public int Moves { get; }
        public bool EndedTurn { get; }
        public bool SafetyBoundReached { get; }
        public CombatAiDecision LastDecision { get; }
    }

    public sealed class CombatAiController
    {
        public const int SourceFocusTileLimit = 20;

        private sealed class ActorState
        {
            public PersistentObjectId Target;
        }

        private readonly WorldMapSessionCoordinator _world;
        private readonly Dictionary<PersistentObjectId, ActorState> _actors =
            new Dictionary<PersistentObjectId, ActorState>();

        public CombatAiController(WorldMapSessionCoordinator world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public int TrackedActorCount => _actors.Count;
        public CombatAiDecision LastDecision { get; private set; }

        public bool TryGetTarget(PersistentObjectId actor, out PersistentObjectId target)
        {
            if (_actors.TryGetValue(actor, out var state) && !state.Target.IsNull)
            {
                target = state.Target;
                return true;
            }

            target = default;
            return false;
        }

        public void ResetTransient()
        {
            _actors.Clear();
            LastDecision = default;
        }

        public void Refresh()
        {
            var combat = _world.Combat;
            if (!combat.IsActive || combat.ResolveIfNoOpposition())
            {
                ResetTransient();
                return;
            }

            var retained = new HashSet<PersistentObjectId>(
                combat.Participants
                    .Where(combat.IsAutonomousHostileNpc)
                    .Select(participant => participant.Identity));

            var stale = _actors.Keys.Where(actor => !retained.Contains(actor)).ToArray();
            for (var i = 0; i < stale.Length; i++)
            {
                _actors.Remove(stale[i]);
            }
        }

        public CombatAiTurnResult RunCurrentTurn(int maximumDecisions = 32)
        {
            if (maximumDecisions <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumDecisions));

            Refresh();
            var combat = _world.Combat;
            if (!combat.IsActive || combat.Mode != CombatMode.TurnBased || combat.CurrentParticipant.IsNull)
                return default;

            var actor = combat.CurrentParticipant;
            if (!combat.IsAutonomousHostileNpc(combat.CurrentParticipant))
                return default;

            var attacks = 0;
            var moves = 0;
            var endedTurn = false;
            var safetyBoundReached = false;
            var last = default(CombatAiDecision);

            for (var decisionIndex = 0; decisionIndex < maximumDecisions; decisionIndex++)
            {
                if (!combat.IsActive || combat.Mode != CombatMode.TurnBased ||
                    combat.CurrentParticipant.IsNull || combat.CurrentParticipant != actor)
                    break;

                last = DecideAndSubmit(actor);
                if (last.Action == CombatAiActionKind.Attack && last.AttackResult?.Succeeded == true)
                    attacks++;
                else if (last.Action == CombatAiActionKind.Move && last.MoveResult?.Succeeded == true)
                    moves++;
                else
                {
                    endedTurn = EndTurnIfOwned(actor);
                    break;
                }

                if (combat.ResolveIfNoOpposition())
                {
                    ResetTransient();
                    break;
                }

                if (decisionIndex == maximumDecisions - 1 && combat.IsActive &&
                    !combat.CurrentParticipant.IsNull && combat.CurrentParticipant == actor)
                {
                    safetyBoundReached = true;
                    endedTurn = EndTurnIfOwned(actor);
                }
            }

            return new CombatAiTurnResult(actor, attacks, moves, endedTurn, safetyBoundReached, last);
        }

        public int TickReadyRealTimeActors()
        {
            Refresh();
            var combat = _world.Combat;
            if (!combat.IsActive || combat.Mode != CombatMode.RealTime)
                return 0;

            var actors = combat.Participants
                .Where(combat.IsAutonomousHostileNpc)
                .Select(participant => participant.Identity)
                .ToArray();
            var submitted = 0;
            for (var i = 0; i < actors.Length; i++)
            {
                var decision = DecideAndSubmit(actors[i]);
                if (decision.Scheduled)
                    submitted++;
            }

            return submitted;
        }

        public CombatAiDecision DecideAndSubmit(PersistentObjectId actor)
        {
            Refresh();
            var combat = _world.Combat;
            if (!combat.IsActive || !combat.TryGetParticipant(actor, out var participant) ||
                !combat.IsAutonomousHostileNpc(participant) || !combat.IsParticipantEligible(actor))
                return SetDecision(new CombatAiDecision(CombatAiActionKind.Yield,
                    CombatActionFailure.ParticipantUnavailable, actor, default, CombatAttackMode.None,
                    null, null, false));

            if (combat.Mode == CombatMode.TurnBased &&
                (combat.CurrentParticipant.IsNull || combat.CurrentParticipant != actor))
                return SetDecision(new CombatAiDecision(CombatAiActionKind.Yield,
                    CombatActionFailure.NotCurrentParticipant, actor, default, CombatAttackMode.None,
                    null, null, false));

            if (combat.Mode == CombatMode.RealTime)
            {
                if (!combat.TryGetRealTimeActorState(actor, out var timing))
                    return SetDecision(new CombatAiDecision(CombatAiActionKind.Yield,
                        CombatActionFailure.ParticipantUnavailable, actor, default, CombatAttackMode.None,
                        null, null, false));
                if (timing.HasPendingAction)
                    return SetDecision(new CombatAiDecision(CombatAiActionKind.Busy,
                        CombatActionFailure.ActorBusy, actor, default, CombatAttackMode.None,
                        null, null, false));
                if (!timing.IsReady)
                    return SetDecision(new CombatAiDecision(CombatAiActionKind.Yield,
                        CombatActionFailure.ActorNotReady, actor, default, CombatAttackMode.None,
                        null, null, false));
            }

            var state = GetOrCreate(actor);
            if (!IsValidTarget(actor, state.Target))
                state.Target = ChooseTarget(actor);

            if (state.Target.IsNull)
            {
                combat.ResolveIfNoOpposition();
                return SetDecision(new CombatAiDecision(CombatAiActionKind.NoTarget,
                    CombatActionFailure.ParticipantUnavailable, actor, default, CombatAttackMode.None,
                    null, null, false));
            }

            var attackMode = DetermineAttackMode(actor);
            if (attackMode == CombatAttackMode.None)
                return SetDecision(new CombatAiDecision(CombatAiActionKind.Yield,
                    CombatActionFailure.UnsupportedAttackMode, actor, state.Target, CombatAttackMode.None,
                    null, null, false));

            var request = new CombatAttackRequest(actor, state.Target, attackMode, CombatCalledLocation.None);
            var preview = combat.PreviewAttack(request);
            if (preview.Succeeded)
            {
                if (combat.Mode == CombatMode.TurnBased)
                {
                    var result = combat.Attack(request);
                    return SetDecision(new CombatAiDecision(CombatAiActionKind.Attack, result.Failure,
                        actor, state.Target, attackMode, result, null, false));
                }

                var scheduled = combat.ScheduleRealTimeAttack(request);
                return SetDecision(new CombatAiDecision(CombatAiActionKind.Attack, scheduled.Failure,
                    actor, state.Target, attackMode, null, null, scheduled.Succeeded));
            }

            if (preview.Failure == CombatActionFailure.OutOfRange ||
                preview.Failure == CombatActionFailure.LineOfFireBlocked)
            {
                var approach = combat.PreviewAiApproach(actor, state.Target, attackMode);
                if (approach.Succeeded)
                {
                    if (combat.Mode == CombatMode.TurnBased)
                    {
                        var move = combat.MoveInCombat(actor, approach.Destination);
                        return SetDecision(new CombatAiDecision(CombatAiActionKind.Move, move.Failure,
                            actor, state.Target, attackMode, null, move, false));
                    }

                    var scheduled = combat.ScheduleRealTimeMove(actor, approach.Destination);
                    return SetDecision(new CombatAiDecision(CombatAiActionKind.Move, scheduled.Failure,
                        actor, state.Target, attackMode, null, null, scheduled.Succeeded));
                }

                return SetDecision(new CombatAiDecision(CombatAiActionKind.Yield, approach.Failure,
                    actor, state.Target, attackMode, null, null, false));
            }

            return SetDecision(new CombatAiDecision(CombatAiActionKind.Yield, preview.Failure,
                actor, state.Target, attackMode, null, null, false));
        }

        private bool EndTurnIfOwned(PersistentObjectId actor)
        {
            var combat = _world.Combat;
            return combat.IsActive && combat.Mode == CombatMode.TurnBased &&
                   !combat.CurrentParticipant.IsNull && combat.CurrentParticipant == actor &&
                   combat.EndCurrentTurn(actor).Succeeded;
        }

        private ActorState GetOrCreate(PersistentObjectId actor)
        {
            if (_actors.TryGetValue(actor, out var state))
                return state;
            state = new ActorState();
            _actors.Add(actor, state);
            return state;
        }

        private bool IsValidTarget(PersistentObjectId actor, PersistentObjectId target)
        {
            if (target.IsNull || !_world.Combat.TryGetParticipant(target, out var participant) ||
                participant.ObjectType != ObjectType.Pc || !_world.Combat.IsParticipantEligible(target) ||
                !_world.Combat.TryGetParticipantPosition(actor, out var actorPosition) ||
                !_world.Combat.TryGetParticipantPosition(target, out var targetPosition))
                return false;

            return TileDistance(actorPosition, targetPosition) <= SourceFocusTileLimit;
        }

        private PersistentObjectId ChooseTarget(PersistentObjectId actor)
        {
            if (!_world.Combat.TryGetParticipantPosition(actor, out var actorPosition))
                return default;

            var candidates = new List<(PersistentObjectId Identity, int Distance, int SourceOrder)>();
            var participants = _world.Combat.Participants;
            for (var i = 0; i < participants.Count; i++)
            {
                var candidate = participants[i];
                if (candidate.ObjectType != ObjectType.Pc || !_world.Combat.IsParticipantEligible(candidate.Identity) ||
                    !_world.Combat.TryGetParticipantPosition(candidate.Identity, out var position))
                    continue;

                var distance = TileDistance(actorPosition, position);
                if (distance <= SourceFocusTileLimit)
                    candidates.Add((candidate.Identity, distance, candidate.SourceOrder));
            }

            return candidates.OrderBy(candidate => candidate.Distance)
                .ThenBy(candidate => candidate.SourceOrder)
                .ThenBy(candidate => candidate.Identity.ToString(), StringComparer.Ordinal)
                .Select(candidate => candidate.Identity)
                .FirstOrDefault();
        }

        private CombatAttackMode DetermineAttackMode(PersistentObjectId actor)
        {
            if (!_world.TryGetEquippedItem(actor, WornLocation.Weapon, out var equipped))
                return CombatAttackMode.BasicMelee;
            if (equipped.Type != ObjectType.Weapon || equipped.WeaponData == null)
                return CombatAttackMode.None;
            return equipped.WeaponData.Skill == WeaponSkill.Bow
                ? CombatAttackMode.BasicRanged
                : CombatAttackMode.None;
        }

        private static int TileDistance(Vector2Int left, Vector2Int right)
        {
            return Math.Max(Math.Abs(left.x - right.x), Math.Abs(left.y - right.y));
        }

        private CombatAiDecision SetDecision(CombatAiDecision decision)
        {
            LastDecision = decision;
            return decision;
        }
    }

    public sealed partial class CombatStateService
    {
        public bool TryGetParticipant(PersistentObjectId identity, out CombatParticipant participant)
        {
            for (var i = 0; i < _participants.Count; i++)
            {
                if (_participants[i].Identity != identity)
                    continue;
                participant = _participants[i];
                return true;
            }

            participant = default;
            return false;
        }

        public bool IsParticipantEligible(PersistentObjectId identity)
        {
            return TryGetParticipant(identity, out _) &&
                   _sources.TryGetValue(identity, out var source) && IsEligible(source);
        }

        public bool TryGetParticipantPosition(PersistentObjectId identity, out Vector2Int position)
        {
            return TryGetCombatPosition(identity, out position);
        }

        public bool IsAutonomousHostileNpc(CombatParticipant participant)
        {
            return IsAutonomousHostileNpc(participant.Identity);
        }

        public bool IsAutonomousHostileNpc(PersistentObjectId identity)
        {
            return _sources.TryGetValue(identity, out var source) && source.ObjectType == ObjectType.Npc &&
                   IsSourceHostile(source);
        }

        public bool ResolveIfNoOpposition()
        {
            if (!IsActive)
                return false;
            var hasPlayer = _participants.Any(participant =>
                participant.ObjectType == ObjectType.Pc && IsParticipantEligible(participant.Identity));
            var hasHostile = _participants.Any(participant =>
                IsAutonomousHostileNpc(participant) && IsParticipantEligible(participant.Identity));
            if (hasPlayer && hasHostile)
                return false;
            ClearTransient();
            return true;
        }

        public CombatAiApproachPreview PreviewAiApproach(
            PersistentObjectId actor,
            PersistentObjectId target,
            CombatAttackMode attackMode)
        {
            if (!IsActive || !_sources.TryGetValue(actor, out var actorSource) ||
                actorSource.ObjectType != ObjectType.Npc || !IsSourceHostile(actorSource) ||
                !TryGetParticipant(actor, out _) || !IsEligible(actorSource) ||
                !TryGetParticipant(target, out _) || !_sources.TryGetValue(target, out var targetSource) ||
                !IsEligible(targetSource))
                return FailedApproach(CombatActionFailure.ParticipantUnavailable, actor, target, attackMode);

            if (Mode == CombatMode.TurnBased)
            {
                if (CurrentParticipant.IsNull || CurrentParticipant != actor)
                    return FailedApproach(CombatActionFailure.NotCurrentParticipant, actor, target, attackMode);
                if (CurrentActionPoints < WalkingActionPointCostPerStep)
                    return FailedApproach(CombatActionFailure.InsufficientActionPoints, actor, target, attackMode);
            }
            else if (Mode == CombatMode.RealTime)
            {
                if (!TryGetRealTimeActorState(actor, out var timing))
                    return FailedApproach(CombatActionFailure.ParticipantUnavailable, actor, target, attackMode);
                if (timing.HasPendingAction)
                    return FailedApproach(CombatActionFailure.ActorBusy, actor, target, attackMode);
                if (!timing.IsReady)
                    return FailedApproach(CombatActionFailure.ActorNotReady, actor, target, attackMode);
            }

            if (_navigationMap == null || _pathfinder == null ||
                !TryGetCombatPosition(actor, out var start) || !TryGetCombatPosition(target, out var targetPosition))
                return FailedApproach(CombatActionFailure.NavigationUnavailable, actor, target, attackMode);

            List<Vector2Int> bestRoute = null;
            for (var i = 0; i < IsoProjection.DirDelta.Length; i++)
            {
                var destination = targetPosition + IsoProjection.DirDelta[i];
                if (destination == start || !_navigationMap.IsWalkable(destination) ||
                    _navigationMap.IsOccupiedByOther(actor, destination))
                    continue;
                if (attackMode == CombatAttackMode.BasicRanged &&
                    _navigationMap.GetProjectileTraversal(destination, targetPosition).IsBlocked)
                    continue;

                var route = new List<Vector2Int>();
                if (!_pathfinder.TryFindPath(_navigationMap, start, destination, route,
                        tile => tile == destination || !_navigationMap.IsOccupiedByOther(actor, tile)))
                    continue;
                if (route.Count > 0 && (bestRoute == null || route.Count < bestRoute.Count))
                    bestRoute = route;
            }

            if (bestRoute == null)
                return FailedApproach(CombatActionFailure.Unreachable, actor, target, attackMode);
            return new CombatAiApproachPreview(true, CombatActionFailure.None, actor, target, attackMode,
                start, bestRoute[0], bestRoute.Count);
        }

        private static CombatAiApproachPreview FailedApproach(
            CombatActionFailure failure,
            PersistentObjectId actor,
            PersistentObjectId target,
            CombatAttackMode attackMode)
        {
            return new CombatAiApproachPreview(false, failure, actor, target, attackMode,
                default, default, 0);
        }
    }
}
