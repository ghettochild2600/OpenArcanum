using System;
using System.Collections.Generic;
using Arcanum.Formats.Objects;
using UnityEngine;

namespace Arcanum.Runtime.World
{
    public enum PlayerInteractionPhase { Idle, ApproachingTarget, Executing, Completed, Cancelled }

    /// <summary>Owns transient player approach intent; authoritative command execution remains in the session.</summary>
    [RequireComponent(typeof(WorldObjectSectorLoader), typeof(PlayerNavigationController))]
    public sealed class PlayerInteractionController : MonoBehaviour
    {
        private readonly InteractionApproachPlanner _approachPlanner = new();
        private readonly List<Vector2Int> _approachRoute = new();
        private WorldObjectSectorLoader _loader;
        private PlayerNavigationController _navigation;
        private bool _submittingApproach;
        private Vector2Int _approachDestination;

        public PlayerInteractionPhase Phase { get; private set; }
        public WorldInteractionCommand? PendingCommand { get; private set; }
        public WorldInteractionResult? LastResult { get; private set; }
        public event Action<WorldInteractionResult> Resolved;

        private void Awake()
        {
            _loader = GetComponent<WorldObjectSectorLoader>();
            _navigation = GetComponent<PlayerNavigationController>();
            _navigation.DestinationRequested += OnNavigationDestinationRequested;
        }

        private void OnDestroy()
        {
            if (_navigation != null) _navigation.DestinationRequested -= OnNavigationDestinationRequested;
        }

        private void Update() => AdvanceInteraction();

        internal void AdvanceInteraction()
        {
            if (Phase != PlayerInteractionPhase.ApproachingTarget || !PendingCommand.HasValue) return;
            if (!TryResolveDependencies())
            {
                Resolve(PendingCommand.Value, WorldInteractionResultCode.ActorNotFound, false);
                return;
            }
            WorldInteractionCommand command = PendingCommand.Value;
            WorldMapSessionCoordinator session = _loader.Session;
            if (session.PlayerState == null || session.PlayerState.Identity != command.Actor)
            {
                Resolve(command, WorldInteractionResultCode.ActorNotFound, true);
                return;
            }
            if (!session.TryGetObjectState(command.Target, out PersistentObjectState targetState)
                || !session.TryGetLoadedObject(command.Target, out _))
            {
                Resolve(command, WorldInteractionResultCode.TargetNotFound, true);
                return;
            }

            Vector2Int? destination = session.PlayerState.Destination;
            if (destination.HasValue)
            {
                if (destination.Value != _approachDestination)
                    Resolve(command, WorldInteractionResultCode.Cancelled, false);
                return;
            }

            if (!TryTargetMapPosition(targetState, out Vector2 targetPosition)
                || !InteractionRangeRules.IsWithin(session.PlayerState.MapPosition, targetPosition,
                    InteractionRangeRules.PortalUseRange))
            {
                Resolve(command, WorldInteractionResultCode.NoReachableInteractionPosition, true);
                return;
            }
            Execute(command);
        }

        public WorldInteractionResult TryUse(ArcanumObjectId target)
        {
            if (Phase == PlayerInteractionPhase.ApproachingTarget && PendingCommand.HasValue)
                Resolve(PendingCommand.Value, WorldInteractionResultCode.Cancelled, true);

            if (!TryResolveDependencies())
            {
                var unavailable = new WorldInteractionCommand(default, target, WorldInteractionCommandType.Use);
                return Complete(unavailable, WorldInteractionResultCode.ActorNotFound);
            }

            WorldMapSessionCoordinator session = _loader.Session;
            ArcanumObjectId actor = session.PlayerState?.Identity ?? default;
            var command = new WorldInteractionCommand(actor, target, WorldInteractionCommandType.Use);
            if (session.PlayerState == null || !actor.IsPersistent)
                return Complete(command, WorldInteractionResultCode.ActorNotFound);
            if (!session.TryGetObjectState(target, out PersistentObjectState targetState)
                || !session.TryGetLoadedObject(target, out WorldObject runtime))
                return Complete(command, WorldInteractionResultCode.TargetNotFound);
            if (targetState.Type != ObjectType.Portal || runtime.Type != ObjectType.Portal)
                return Complete(command, WorldInteractionResultCode.InvalidTarget);
            if (!TryTargetMapPosition(targetState, out Vector2 targetPosition))
                return Complete(command, WorldInteractionResultCode.TargetNotFound);
            if (InteractionRangeRules.IsWithin(session.PlayerState.MapPosition, targetPosition,
                    InteractionRangeRules.PortalUseRange))
                return Execute(command.At(Vector2Int.RoundToInt(session.PlayerState.MapPosition)));
            if (!SectorCoordinate.TryParse(session.SelectedSector, out SectorCoordinate sector)
                || targetState.SourceSector != sector.Path)
                return Complete(command, WorldInteractionResultCode.TargetNotFound);

            if (_navigation.Player == null || _loader.NavigationMap == null)
                return Complete(command, WorldInteractionResultCode.ActorNotFound);

            Vector2Int start = Vector2Int.RoundToInt(_navigation.Player.TilePosition);
            Vector2Int targetTile = Vector2Int.RoundToInt(targetState.TilePosition);
            if (!_approachPlanner.TryPlan(_loader.NavigationMap, start, targetTile,
                    InteractionRangeRules.PortalUseRange, out Vector2Int localDestination, _approachRoute))
                return Complete(command, WorldInteractionResultCode.NoReachableInteractionPosition);

            _approachDestination = Vector2Int.RoundToInt(sector.ToGlobal(localDestination));
            command = command.At(_approachDestination);
            PendingCommand = command;
            Phase = PlayerInteractionPhase.ApproachingTarget;
            var accepted = new WorldInteractionResult(command, WorldInteractionResultCode.Approaching);
            LastResult = accepted;
            _submittingApproach = true;
            bool routeAccepted;
            try { routeAccepted = _navigation.TrySetGlobalDestination(_approachDestination); }
            finally { _submittingApproach = false; }
            if (!routeAccepted)
                return Resolve(command, WorldInteractionResultCode.NoReachableInteractionPosition, false);
            return accepted;
        }

        public WorldInteractionResult CancelPending()
        {
            if (Phase == PlayerInteractionPhase.ApproachingTarget && PendingCommand.HasValue)
                return Resolve(PendingCommand.Value, WorldInteractionResultCode.Cancelled, true);
            Phase = PlayerInteractionPhase.Idle;
            var command = PendingCommand ?? default;
            PendingCommand = null;
            var result = new WorldInteractionResult(command, WorldInteractionResultCode.Cancelled);
            LastResult = result;
            return result;
        }

        private WorldInteractionResult Execute(WorldInteractionCommand command)
        {
            Phase = PlayerInteractionPhase.Executing;
            WorldInteractionResult result = _loader.Session.ExecuteInteraction(command);
            PendingCommand = null;
            LastResult = result;
            Phase = result.IsSuccess ? PlayerInteractionPhase.Completed : PlayerInteractionPhase.Cancelled;
            Resolved?.Invoke(result);
            return result;
        }

        private WorldInteractionResult Complete(WorldInteractionCommand command, WorldInteractionResultCode code)
        {
            PendingCommand = null;
            var result = new WorldInteractionResult(command, code);
            LastResult = result;
            Phase = code == WorldInteractionResultCode.Success
                ? PlayerInteractionPhase.Completed : PlayerInteractionPhase.Cancelled;
            Resolved?.Invoke(result);
            return result;
        }

        private WorldInteractionResult Resolve(WorldInteractionCommand command, WorldInteractionResultCode code,
            bool cancelNavigation)
        {
            PendingCommand = null;
            if (cancelNavigation) _navigation.CancelRoute();
            var result = new WorldInteractionResult(command, code);
            LastResult = result;
            Phase = code == WorldInteractionResultCode.Success
                ? PlayerInteractionPhase.Completed : PlayerInteractionPhase.Cancelled;
            Resolved?.Invoke(result);
            return result;
        }

        private void OnNavigationDestinationRequested()
        {
            if (_submittingApproach) return;
            if (Phase == PlayerInteractionPhase.ApproachingTarget && PendingCommand.HasValue)
                Resolve(PendingCommand.Value, WorldInteractionResultCode.Cancelled, false);
            else if (Phase != PlayerInteractionPhase.Executing)
                Phase = PlayerInteractionPhase.Idle;
        }

        private static bool TryTargetMapPosition(PersistentObjectState state, out Vector2 position)
        {
            if (state != null && SectorCoordinate.TryParse(state.SourceSector, out SectorCoordinate sector))
            {
                position = sector.ToGlobal(state.TilePosition);
                return true;
            }
            position = default;
            return false;
        }

        private bool TryResolveDependencies()
        {
            if (_loader == null) _loader = GetComponent<WorldObjectSectorLoader>();
            if (_navigation == null)
            {
                _navigation = GetComponent<PlayerNavigationController>();
                if (_navigation != null)
                    _navigation.DestinationRequested += OnNavigationDestinationRequested;
            }
            return _loader != null && _navigation != null;
        }
    }
}
