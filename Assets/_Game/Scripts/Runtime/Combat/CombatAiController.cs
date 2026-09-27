using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
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

    public enum CombatAiTargetSelectionReason
    {
        None,
        NearestEligible,
        RetainedFocus,
        NewDangerSource,
        DangerDistance,
        DangerLevelDistance
    }

    public readonly struct CombatAiTargetSelection
    {
        public CombatAiTargetSelection(PersistentObjectId current, PersistentObjectId candidate,
            PersistentObjectId selected, CombatAiTargetSelectionReason reason, int roll = 0,
            int currentDistance = 0, int candidateDistance = 0,
            int currentScore = 0, int candidateScore = 0)
        {
            Current = current;
            Candidate = candidate;
            Selected = selected;
            Reason = reason;
            Roll = roll;
            CurrentDistance = currentDistance;
            CandidateDistance = candidateDistance;
            CurrentScore = currentScore;
            CandidateScore = candidateScore;
        }

        public PersistentObjectId Current { get; }
        public PersistentObjectId Candidate { get; }
        public PersistentObjectId Selected { get; }
        public CombatAiTargetSelectionReason Reason { get; }
        public int Roll { get; }
        public int CurrentDistance { get; }
        public int CandidateDistance { get; }
        public int CurrentScore { get; }
        public int CandidateScore { get; }
    }

    public readonly struct CombatAiWeaponSelection
    {
        public CombatAiWeaponSelection(CombatActionFailure failure,
            PersistentObjectId selectedWeapon, CombatAttackMode attackMode,
            int effectiveDamageScore, bool reachesTarget, int candidateCount,
            bool changed = false)
        {
            Failure = failure;
            SelectedWeapon = selectedWeapon;
            AttackMode = attackMode;
            EffectiveDamageScore = effectiveDamageScore;
            ReachesTarget = reachesTarget;
            CandidateCount = candidateCount;
            Changed = changed;
        }

        public bool Succeeded => Failure == CombatActionFailure.None;
        public CombatActionFailure Failure { get; }
        public PersistentObjectId SelectedWeapon { get; }
        public CombatAttackMode AttackMode { get; }
        public int EffectiveDamageScore { get; }
        public bool ReachesTarget { get; }
        public int CandidateCount { get; }
        public bool Changed { get; }
        public bool UsesUnarmedFallback => Succeeded && SelectedWeapon.IsNull;

        internal CombatAiWeaponSelection WithChange(bool changed)
            => new(Failure, SelectedWeapon, AttackMode, EffectiveDamageScore,
                ReachesTarget, CandidateCount, changed);
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
            public bool WeaponSelectionRequired = true;
            public bool WeaponSelectionBlocked;
            public long ProcessedAttackResolutionSequence;
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
        public CombatAiWeaponSelection LastWeaponSelection { get; private set; }
        public CombatAiTargetSelection LastTargetSelection { get; private set; }

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
            LastWeaponSelection = default;
            LastTargetSelection = default;
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
                    .Where(combat.IsAutonomousCombatNpc)
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
            if (!combat.IsAutonomousCombatNpc(combat.CurrentParticipant))
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
                .Where(combat.IsAutonomousCombatNpc)
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
                !combat.IsAutonomousCombatNpc(participant) || !combat.IsParticipantEligible(actor))
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
            ProcessLatestDangerSource(actor, state);
            if (!IsValidTarget(actor, state.Target))
                state.Target = ChooseTarget(actor);
            else if (LastTargetSelection.Selected != state.Target)
                LastTargetSelection = new CombatAiTargetSelection(state.Target, state.Target,
                    state.Target, CombatAiTargetSelectionReason.RetainedFocus);

            if (state.Target.IsNull)
            {
                combat.ResolveIfNoOpposition();
                return SetDecision(new CombatAiDecision(CombatAiActionKind.NoTarget,
                    CombatActionFailure.ParticipantUnavailable, actor, default, CombatAttackMode.None,
                    null, null, false));
            }

            if (state.WeaponSelectionBlocked)
                return SetDecision(new CombatAiDecision(CombatAiActionKind.Yield,
                    CombatActionFailure.UnsupportedWeapon, actor, state.Target, CombatAttackMode.None,
                    null, null, false));

            if (state.WeaponSelectionRequired || !combat.IsAiEquippedWeaponUsable(actor))
            {
                CombatAiWeaponSelection selection = combat.SelectAiWeapon(actor, state.Target);
                if (!selection.Succeeded)
                {
                    state.WeaponSelectionRequired = false;
                    state.WeaponSelectionBlocked = true;
                    return SetDecision(new CombatAiDecision(CombatAiActionKind.Yield, selection.Failure,
                        actor, state.Target, CombatAttackMode.None, null, null, false));
                }
                LastWeaponSelection = selection;
                state.WeaponSelectionRequired = false;
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

        private void ProcessLatestDangerSource(PersistentObjectId actor, ActorState state)
        {
            var combat = _world.Combat;
            long sequence = combat.AttackResolutionSequence;
            if (sequence <= state.ProcessedAttackResolutionSequence)
                return;
            state.ProcessedAttackResolutionSequence = sequence;
            if (!combat.LastAttackResult.HasValue)
                return;
            CombatAttackResult result = combat.LastAttackResult.Value;
            PersistentObjectId candidate = result.Request.Attacker;
            if (!result.Succeeded || result.Request.Target != actor
                || !IsValidTarget(actor, candidate))
                return;
            if (!IsValidTarget(actor, state.Target))
            {
                state.Target = candidate;
                LastTargetSelection = new CombatAiTargetSelection(default, candidate, candidate,
                    CombatAiTargetSelectionReason.NewDangerSource);
                return;
            }
            if (state.Target == candidate)
            {
                LastTargetSelection = new CombatAiTargetSelection(state.Target, candidate, candidate,
                    CombatAiTargetSelectionReason.RetainedFocus);
                return;
            }

            LastTargetSelection = combat.CompareAiDangerTargets(actor, state.Target, candidate);
            state.Target = LastTargetSelection.Selected;
        }

        private bool IsValidTarget(PersistentObjectId actor, PersistentObjectId target)
        {
            if (target.IsNull || !_world.Combat.TryGetParticipant(target, out _) ||
                !_world.Combat.AreOpponents(actor, target) || !_world.Combat.IsParticipantEligible(target) ||
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
                if (!_world.Combat.AreOpponents(actor, candidate.Identity)
                    || !_world.Combat.IsParticipantEligible(candidate.Identity) ||
                    !_world.Combat.TryGetParticipantPosition(candidate.Identity, out var position))
                    continue;

                var distance = TileDistance(actorPosition, position);
                if (distance <= SourceFocusTileLimit)
                    candidates.Add((candidate.Identity, distance, candidate.SourceOrder));
            }

            var selected = candidates.OrderBy(candidate => candidate.Distance)
                .ThenBy(candidate => candidate.SourceOrder)
                .ThenBy(candidate => candidate.Identity.ToString(), StringComparer.Ordinal)
                .FirstOrDefault();
            if (selected.Identity.IsNull)
                return default;
            LastTargetSelection = new CombatAiTargetSelection(default, selected.Identity,
                selected.Identity, CombatAiTargetSelectionReason.NearestEligible,
                currentDistance: 0, candidateDistance: selected.Distance,
                currentScore: 0, candidateScore: -selected.Distance);
            return selected.Identity;
        }

        private CombatAttackMode DetermineAttackMode(PersistentObjectId actor)
        {
            if (!_world.TryGetEquippedItem(actor, WornLocation.Weapon, out var equipped))
                return CombatAttackMode.BasicMelee;
            if (equipped.Type != ObjectType.Weapon || equipped.WeaponData == null)
                return CombatAttackMode.None;
            return equipped.WeaponData.Skill switch
            {
                WeaponSkill.Melee when equipped.WeaponData.Range == 1
                    && !equipped.WeaponData.UsesAmmo => CombatAttackMode.BasicMelee,
                WeaponSkill.Bow => CombatAttackMode.BasicRanged,
                _ => CombatAttackMode.None,
            };
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

        public bool IsAutonomousCombatNpc(CombatParticipant participant)
            => IsAutonomousCombatNpc(participant.Identity);

        public bool IsAutonomousCombatNpc(PersistentObjectId identity)
            => _sources.TryGetValue(identity, out var source) && source.ObjectType == ObjectType.Npc
               && (IsSourceHostile(source) || _world.Party.IsMember(identity));

        public bool AreAllies(PersistentObjectId left, PersistentObjectId right)
        {
            bool leftParty = IsPlayerSide(left);
            bool rightParty = IsPlayerSide(right);
            if (leftParty || rightParty) return leftParty && rightParty;
            return left == right;
        }

        public bool AreOpponents(PersistentObjectId left, PersistentObjectId right)
        {
            if (left.IsNull || right.IsNull || left == right) return false;
            bool leftParty = IsPlayerSide(left);
            bool rightParty = IsPlayerSide(right);
            if (leftParty && rightParty) return false;
            if (!leftParty && !rightParty) return true;
            PersistentObjectId npc = leftParty ? right : left;
            return _sources.TryGetValue(npc, out CombatActorSource source) && IsSourceHostile(source);
        }

        private bool IsPlayerSide(PersistentObjectId identity)
            => _world.Party.IsMember(identity)
               || _sources.TryGetValue(identity, out CombatActorSource source)
               && source.ObjectType == ObjectType.Pc;

        public CombatAiTargetSelection CompareAiDangerTargets(PersistentObjectId actor,
            PersistentObjectId current, PersistentObjectId candidate)
        {
            if (!TryGetCombatPosition(actor, out Vector2Int actorPosition)
                || !TryGetCombatPosition(current, out Vector2Int currentPosition)
                || !TryGetCombatPosition(candidate, out Vector2Int candidatePosition))
                return new CombatAiTargetSelection(current, candidate, current,
                    CombatAiTargetSelectionReason.RetainedFocus);

            int currentDistance = Math.Max(Math.Abs(actorPosition.x - currentPosition.x),
                Math.Abs(actorPosition.y - currentPosition.y));
            int candidateDistance = Math.Max(Math.Abs(actorPosition.x - candidatePosition.x),
                Math.Abs(actorPosition.y - candidatePosition.y));
            if (candidateDistance > CombatAiController.SourceFocusTileLimit)
                return new CombatAiTargetSelection(current, candidate, current,
                    CombatAiTargetSelectionReason.RetainedFocus, currentDistance: currentDistance,
                    candidateDistance: candidateDistance);
            if (currentDistance > CombatAiController.SourceFocusTileLimit)
                return new CombatAiTargetSelection(current, candidate, candidate,
                    CombatAiTargetSelectionReason.NewDangerSource, currentDistance: currentDistance,
                    candidateDistance: candidateDistance);

            int roll = _random.NextInclusive(1, 100);
            bool useLevel = roll <= 50;
            int currentScore = useLevel
                ? _world.Progression.GetLevel(current) - currentDistance
                : -currentDistance;
            int candidateScore = useLevel
                ? _world.Progression.GetLevel(candidate) - candidateDistance
                : -candidateDistance;
            PersistentObjectId selected = candidateScore > currentScore ? candidate : current;
            return new CombatAiTargetSelection(current, candidate, selected,
                useLevel ? CombatAiTargetSelectionReason.DangerLevelDistance
                    : CombatAiTargetSelectionReason.DangerDistance,
                roll, currentDistance, candidateDistance, currentScore, candidateScore);
        }

        public CombatAiWeaponSelection PreviewAiWeaponSelection(PersistentObjectId actor,
            PersistentObjectId target)
        {
            List<AiWeaponCandidate> candidates = BuildAiWeaponCandidates(actor, target,
                out CombatFailure failure, out int distance);
            if (failure != CombatFailure.None)
                return new CombatAiWeaponSelection(CombatActionFailure.ParticipantUnavailable,
                    default, CombatAttackMode.None, 0, false, 0);
            if (candidates.Count == 0)
                return new CombatAiWeaponSelection(CombatActionFailure.None, default,
                    CombatAttackMode.BasicMelee, 0, distance <= 1, 0);
            AiWeaponCandidate selected = OrderedAiWeaponCandidates(candidates).First();
            return new CombatAiWeaponSelection(CombatActionFailure.None, selected.Identity,
                selected.AttackMode, selected.EffectiveDamageScore, selected.ReachesTarget,
                candidates.Count);
        }

        public CombatAiWeaponSelection SelectAiWeapon(PersistentObjectId actor,
            PersistentObjectId target)
        {
            List<AiWeaponCandidate> candidates = BuildAiWeaponCandidates(actor, target,
                out CombatFailure failure, out int distance);
            if (failure != CombatFailure.None)
                return new CombatAiWeaponSelection(failure, default, CombatAttackMode.None,
                    0, false, 0);
            bool hadEquipped = TryGetEquippedWeaponIdentity(actor, out PersistentObjectId equippedBefore);
            foreach (AiWeaponCandidate candidate in OrderedAiWeaponCandidates(candidates))
            {
                CombatResult applied = ApplyAiWeaponSelection(actor, candidate.Identity);
                if (!applied.Succeeded) continue;
                bool changed = hadEquipped
                    ? equippedBefore != candidate.Identity
                    : !candidate.Identity.IsNull;
                return new CombatAiWeaponSelection(CombatFailure.None, candidate.Identity,
                    candidate.AttackMode, candidate.EffectiveDamageScore, candidate.ReachesTarget,
                    candidates.Count, changed);
            }

            CombatResult fallback = ApplyAiWeaponSelection(actor, default);
            bool fallbackChanged = fallback.Succeeded && hadEquipped;
            return new CombatAiWeaponSelection(fallback.Failure, default,
                CombatAttackMode.BasicMelee, 0, distance <= 1, candidates.Count, fallbackChanged);
        }

        public bool IsAiEquippedWeaponUsable(PersistentObjectId actor)
        {
            if (!_world.TryGetEquippedItem(actor, WornLocation.Weapon, out PersistentObjectState equipped))
                return true;
            return TryEvaluateAiWeapon(actor, equipped, 1, out _);
        }

        public bool TryGetEquippedWeaponIdentity(PersistentObjectId actor,
            out PersistentObjectId identity)
        {
            if (_world.TryGetEquippedItem(actor, WornLocation.Weapon, out PersistentObjectState equipped))
            {
                identity = equipped.Identity;
                return true;
            }
            identity = default;
            return false;
        }

        public CombatResult ApplyAiWeaponSelection(PersistentObjectId actor,
            PersistentObjectId selectedWeapon)
        {
            CombatFailure validation = ValidateAiWeaponChangeActor(actor);
            if (validation != CombatFailure.None)
                return new CombatResult(validation);

            bool hasEquipped = _world.TryGetEquippedItem(actor, WornLocation.Weapon,
                out PersistentObjectState equipped);
            if (selectedWeapon.IsNull)
            {
                if (!hasEquipped) return new CombatResult(CombatFailure.None);
                EquipmentTransactionResult unequip = _world.UnequipItem(actor, WornLocation.Weapon);
                return new CombatResult(unequip.Succeeded
                    ? CombatFailure.None : CombatFailure.UnsupportedWeapon);
            }
            if (hasEquipped && equipped.Identity == selectedWeapon)
                return new CombatResult(CombatFailure.None);
            if (!_world.TryGetObjectState(selectedWeapon, out PersistentObjectState selected)
                || !TryEvaluateAiWeapon(actor, selected, 1, out _))
                return new CombatResult(CombatFailure.UnsupportedWeapon);
            EquipmentTransactionResult equip = _world.EquipItem(actor, selectedWeapon,
                WornLocation.Weapon);
            return new CombatResult(equip.Succeeded
                ? CombatFailure.None : CombatFailure.UnsupportedWeapon);
        }

        private CombatFailure ValidateAiWeaponChangeActor(PersistentObjectId actor)
        {
            if (!IsActive) return CombatFailure.Inactive;
            if (!IsAutonomousCombatNpc(actor) || !IsParticipantEligible(actor))
                return CombatFailure.ParticipantUnavailable;
            if (Mode == CombatMode.TurnBased)
                return CurrentParticipant == actor
                    ? CombatFailure.None : CombatFailure.NotCurrentParticipant;
            if (!TryGetRealTimeActorState(actor, out CombatRealTimeActorState timing))
                return CombatFailure.ParticipantUnavailable;
            if (timing.HasPendingAction) return CombatFailure.ActorBusy;
            return timing.IsReady ? CombatFailure.None : CombatFailure.ActorNotReady;
        }

        private void AddAiWeaponCandidate(PersistentObjectId actor, PersistentObjectState item,
            int distance, List<AiWeaponCandidate> candidates)
        {
            if (TryEvaluateAiWeapon(actor, item, distance, out AiWeaponCandidate candidate))
                candidates.Add(candidate);
        }

        private List<AiWeaponCandidate> BuildAiWeaponCandidates(PersistentObjectId actor,
            PersistentObjectId target, out CombatFailure failure, out int distance)
        {
            distance = 0;
            if (!IsActive || !IsAutonomousCombatNpc(actor) || !IsParticipantEligible(actor)
                || !IsParticipantEligible(target)
                || !TryGetCombatPosition(actor, out Vector2Int actorPosition)
                || !TryGetCombatPosition(target, out Vector2Int targetPosition))
            {
                failure = CombatFailure.ParticipantUnavailable;
                return new List<AiWeaponCandidate>();
            }

            failure = CombatFailure.None;
            distance = InteractionRangeRules.Distance(actorPosition, targetPosition);
            var candidates = new List<AiWeaponCandidate>();
            var seen = new HashSet<PersistentObjectId>();
            if (_world.TryGetEquippedItem(actor, WornLocation.Weapon, out PersistentObjectState equipped))
            {
                seen.Add(equipped.Identity);
                AddAiWeaponCandidate(actor, equipped, distance, candidates);
            }
            foreach (PersistentObjectId identity in _world.ChildrenOf(actor))
            {
                if (!seen.Add(identity) || !_world.TryGetObjectState(identity, out PersistentObjectState item))
                    continue;
                AddAiWeaponCandidate(actor, item, distance, candidates);
            }
            return candidates;
        }

        private static IEnumerable<AiWeaponCandidate> OrderedAiWeaponCandidates(
            List<AiWeaponCandidate> candidates)
        {
            IEnumerable<AiWeaponCandidate> pool = candidates.Any(value => value.ReachesTarget)
                ? candidates.Where(value => value.ReachesTarget)
                : candidates;
            return pool.OrderByDescending(value => value.EffectiveDamageScore)
                .ThenByDescending(value => value.Identity.Key, StringComparer.Ordinal);
        }

        private bool TryEvaluateAiWeapon(PersistentObjectId actor, PersistentObjectState item,
            int distance, out AiWeaponCandidate candidate)
        {
            candidate = default;
            if (item == null || item.Type != ObjectType.Weapon || item.WeaponData == null
                || (item.ArtId & 0x400u) != 0)
                return false;
            Weapon weapon = item.WeaponData;
            CombatAttackMode mode;
            CharacterSkill skill;
            if (weapon.Skill == WeaponSkill.Melee && weapon.Range == 1 && !weapon.UsesAmmo)
            {
                mode = CombatAttackMode.BasicMelee;
                skill = CharacterSkill.Melee;
            }
            else if (weapon.Skill == WeaponSkill.Bow && weapon.UsesAmmo
                     && weapon.AmmoConsumption > 0
                     && _world.TryGetAmmo(actor, weapon.AmmoType, weapon.AmmoConsumption, out _))
            {
                mode = CombatAttackMode.BasicRanged;
                skill = CharacterSkill.Bow;
            }
            else
            {
                return false;
            }
            if (!TryGetWeaponDamageRange(weapon, DamageType.Normal, out _, out _)
                || !TryGetWeaponDamageRange(weapon, DamageType.Fatigue, out _, out _)
                || weapon.DamageMax[(int)DamageType.Poison] != 0
                || weapon.DamageMax[(int)DamageType.Electrical] != 0
                || weapon.DamageMax[(int)DamageType.Fire] != 0)
                return false;

            int averageDamage = 0;
            for (int index = 0; index < Weapon.DamageTypeCount; index++)
                averageDamage = (weapon.DamageMin[index] + weapon.DamageMax[index]) / 2;
            int skillRank = _world.Progression.GetEffectiveSkillRank(actor, skill);
            int effectiveness = skill == CharacterSkill.Bow && skillRank == 0
                ? -1 : checked(5 * skillRank + 25);
            if (_world.Characters.GetEffectiveAttribute(actor, CharacterAttribute.Intelligence) >= 20)
                effectiveness += 10;
            if (effectiveness == 0) effectiveness = -1;
            int score = averageDamage * effectiveness / 100;
            candidate = new AiWeaponCandidate(item.Identity, mode, score, weapon.Range >= distance);
            return true;
        }

        private readonly struct AiWeaponCandidate
        {
            public AiWeaponCandidate(PersistentObjectId identity, CombatAttackMode attackMode,
                int effectiveDamageScore, bool reachesTarget)
            {
                Identity = identity;
                AttackMode = attackMode;
                EffectiveDamageScore = effectiveDamageScore;
                ReachesTarget = reachesTarget;
            }

            public PersistentObjectId Identity { get; }
            public CombatAttackMode AttackMode { get; }
            public int EffectiveDamageScore { get; }
            public bool ReachesTarget { get; }
        }

        public bool ResolveIfNoOpposition()
        {
            if (!IsActive)
                return false;
            var hasPlayer = _participants.Any(participant =>
                _world.Party.IsPartyAlly(participant.Identity) && IsParticipantEligible(participant.Identity));
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
                actorSource.ObjectType != ObjectType.Npc || !IsAutonomousCombatNpc(actor) ||
                !TryGetParticipant(actor, out _) || !IsEligible(actorSource) ||
                !TryGetParticipant(target, out _) || !_sources.TryGetValue(target, out var targetSource) ||
                !IsEligible(targetSource) || !AreOpponents(actor, target))
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
