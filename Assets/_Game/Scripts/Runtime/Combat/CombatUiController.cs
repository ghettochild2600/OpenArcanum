using System;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.World;

namespace Arcanum.Runtime.Combat
{
    /// <summary>
    /// Transient M8I command/projection boundary. It owns selection and feedback only; combat authority,
    /// legality, costs, timing, random resolution, damage, and turn progression remain in CombatStateService.
    /// </summary>
    public sealed class CombatUiController
    {
        private readonly WorldMapSessionCoordinator _session;
        private bool _wasActive;

        public CombatAttackMode AttackMode { get; private set; } = CombatAttackMode.BasicMelee;
        public CombatCalledLocation CalledLocation { get; private set; } = CombatCalledLocation.Torso;
        public ArcanumObjectId SelectedTarget { get; private set; }
        public bool HasSelectedTarget => !SelectedTarget.IsNull;
        public CombatAttackPreview Preview { get; private set; }
        public CombatAttackResult? LastResult { get; private set; }
        public CombatFailure LastFailure { get; private set; }
        public string Feedback { get; private set; } = string.Empty;
        public int AmmoQuantity { get; private set; }

        public CombatStateService Combat => _session.Combat;
        public bool IsVisible => Combat.IsActive;
        public CombatMode Mode => Combat.Mode;
        public ArcanumObjectId Player => _session.PlayerState?.Identity ?? default;
        public ArcanumObjectId CurrentActor => Combat.Mode == CombatMode.TurnBased
            ? Combat.CurrentParticipant : Player;
        public bool IsPlayerTurn => Combat.Mode == CombatMode.RealTime
                                    || Combat.CurrentParticipant == Player;
        public int CurrentActionPoints => Combat.CurrentActionPoints;
        public int MaximumActionPoints => Combat.MaximumActionPoints;

        public bool IsRealTimeReady
            => Combat.Mode == CombatMode.RealTime
               && Combat.TryGetRealTimeActorState(Player, out CombatRealTimeActorState state)
               && state.IsReady;

        public bool IsRealTimeBusy
            => Combat.Mode == CombatMode.RealTime
               && Combat.TryGetRealTimeActorState(Player, out CombatRealTimeActorState state)
               && state.HasPendingAction;

        public CombatUiController(WorldMapSessionCoordinator session)
            => _session = session ?? throw new ArgumentNullException(nameof(session));

        public void Refresh()
        {
            if (!Combat.IsActive)
            {
                if (_wasActive) ResetTransient();
                _wasActive = false;
                return;
            }

            _wasActive = true;
            if (HasSelectedTarget && Combat.Participants.All(value => value.Identity != SelectedTarget))
            {
                SelectedTarget = default;
                Preview = default;
                Feedback = "Selected target is no longer available.";
                LastFailure = CombatFailure.ParticipantUnavailable;
            }
            RefreshPreview();
            RefreshAmmo();
            RefreshResolvedResult();
        }

        public bool SelectTarget(ArcanumObjectId target)
        {
            if (!Combat.IsActive)
                return RejectSelection(CombatFailure.Inactive, "Combat is inactive.");
            if (target.IsNull || target == Player)
                return RejectSelection(CombatFailure.InvalidTarget, "Select an opposing combatant.");
            if (Combat.Participants.All(value => value.Identity != target))
                return RejectSelection(CombatFailure.ParticipantNotRegistered,
                    "That object is not an active combat participant.");

            SelectedTarget = target;
            LastFailure = CombatFailure.None;
            Feedback = $"Target: {target}";
            RefreshPreview();
            RefreshAmmo();
            return true;
        }

        public void ClearTarget()
        {
            SelectedTarget = default;
            Preview = default;
            Feedback = "Target cleared.";
            LastFailure = CombatFailure.None;
        }

        public void SetAttackMode(CombatAttackMode mode)
        {
            if (!Enum.IsDefined(typeof(CombatAttackMode), mode))
            {
                LastFailure = CombatFailure.UnsupportedAttackMode;
                Feedback = "Unsupported attack mode.";
                return;
            }
            AttackMode = mode;
            LastFailure = CombatFailure.None;
            Feedback = mode == CombatAttackMode.BasicRanged ? "Bow selected." : "Melee selected.";
            RefreshPreview();
            RefreshAmmo();
        }

        public void SetCalledLocation(CombatCalledLocation location)
        {
            if (location is not (CombatCalledLocation.Torso or CombatCalledLocation.Head
                or CombatCalledLocation.Arm or CombatCalledLocation.Leg))
            {
                LastFailure = CombatFailure.InvalidCalledLocation;
                Feedback = "Unsupported called location.";
                return;
            }
            CalledLocation = location;
            LastFailure = CombatFailure.None;
            Feedback = $"Called location: {location}.";
            RefreshPreview();
        }

        public CombatFailure SubmitAttack()
        {
            Refresh();
            if (!HasSelectedTarget)
                return RejectCommand(CombatFailure.InvalidTarget, "Select a combat target first.");
            var request = new CombatAttackRequest(Player, SelectedTarget, AttackMode, CalledLocation);
            Preview = Combat.PreviewAttack(request);
            if (!Preview.Succeeded)
                return RejectCommand(Preview.Failure, DescribeFailure(Preview.Failure));

            if (Combat.Mode == CombatMode.RealTime)
            {
                CombatResult scheduled = Combat.ScheduleRealTimeAttack(request);
                if (!scheduled.Succeeded)
                    return RejectCommand(scheduled.Failure, DescribeFailure(scheduled.Failure));
                LastFailure = CombatFailure.None;
                Feedback = AttackMode == CombatAttackMode.BasicRanged
                    ? "Bow attack scheduled." : "Melee attack scheduled.";
                return CombatFailure.None;
            }

            CombatAttackResult result = Combat.Attack(request);
            if (!result.Succeeded)
                return RejectCommand(result.Failure, DescribeFailure(result.Failure));
            ApplyResult(result);
            RefreshPreview();
            RefreshAmmo();
            return CombatFailure.None;
        }

        public CombatFailure EndTurn()
        {
            CombatResult result = Combat.EndCurrentTurn(Player);
            if (!result.Succeeded)
                return RejectCommand(result.Failure, DescribeFailure(result.Failure));
            LastFailure = CombatFailure.None;
            Feedback = "Turn ended.";
            Refresh();
            return CombatFailure.None;
        }

        public void ResetTransient()
        {
            AttackMode = CombatAttackMode.BasicMelee;
            CalledLocation = CombatCalledLocation.Torso;
            SelectedTarget = default;
            Preview = default;
            LastResult = null;
            LastFailure = CombatFailure.None;
            Feedback = string.Empty;
            AmmoQuantity = 0;
        }

        private void RefreshPreview()
        {
            if (!Combat.IsActive || !HasSelectedTarget || Player.IsNull)
            {
                Preview = default;
                return;
            }
            Preview = Combat.PreviewAttack(new CombatAttackRequest(Player, SelectedTarget,
                AttackMode, CalledLocation));
        }

        private void RefreshAmmo()
        {
            AmmoQuantity = 0;
            if (Player.IsNull
                || !_session.TryGetEquippedItem(Player, WornLocation.Weapon,
                    out PersistentObjectState weaponState)
                || weaponState.WeaponData == null) return;
            Weapon weapon = weaponState.WeaponData;
            if (!weapon.UsesAmmo) return;
            if (_session.TryGetAmmo(Player, weapon.AmmoType, 1, out PersistentObjectState ammo))
                AmmoQuantity = ammo.StackQuantity.GetValueOrDefault();
        }

        private void RefreshResolvedResult()
        {
            if (!Combat.LastAttackResult.HasValue) return;
            CombatAttackResult result = Combat.LastAttackResult.Value;
            if (result.Request.Attacker == Player) ApplyResult(result);
        }

        private void ApplyResult(CombatAttackResult result)
        {
            LastResult = result;
            LastFailure = result.Failure;
            if (!result.Succeeded)
            {
                Feedback = DescribeFailure(result.Failure);
                return;
            }
            Feedback = result.CriticalDodge
                ? "Critical Dodge"
                : result.Outcome switch
                {
                    CombatAttackOutcome.Miss => "Miss",
                    CombatAttackOutcome.Hit => "Hit",
                    CombatAttackOutcome.CriticalSuccess => "Critical Success",
                    CombatAttackOutcome.CriticalFailure => "Critical Failure",
                    _ => result.Outcome.ToString(),
                };
        }

        private bool RejectSelection(CombatFailure failure, string message)
        {
            RejectCommand(failure, message);
            return false;
        }

        private CombatFailure RejectCommand(CombatFailure failure, string message)
        {
            LastFailure = failure;
            Feedback = message;
            return failure;
        }

        private static string DescribeFailure(CombatFailure failure)
            => failure switch
            {
                CombatFailure.ActorBusy => "Actor is busy.",
                CombatFailure.ActorNotReady => "Actor is not ready.",
                CombatFailure.NotCurrentParticipant => "It is not the player's turn.",
                CombatFailure.InvalidTarget or CombatFailure.TargetNotFound
                    or CombatFailure.ParticipantNotRegistered => "Select a valid combat target.",
                CombatFailure.ParticipantUnavailable => "The selected combatant is unavailable.",
                CombatFailure.InsufficientActionPoints => "Not enough action points.",
                CombatFailure.OutOfRange => "Target is out of range.",
                CombatFailure.LineOfFireBlocked => "Line of fire is blocked.",
                CombatFailure.NoAmmo => "No compatible ammunition.",
                CombatFailure.IncompatibleAmmo => "Equipped ammunition is incompatible.",
                CombatFailure.UnsupportedWeapon => "The selected attack is not supported by the equipped weapon.",
                _ => failure.ToString(),
            };
    }
}
