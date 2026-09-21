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
            _loader.Session.SectorUnloading += OnSectorUnloading;
        }

        private void OnDestroy()
        {
            if (_navigation != null) _navigation.DestinationRequested -= OnNavigationDestinationRequested;
            if (_loader != null && _loader.Session != null)
                _loader.Session.SectorUnloading -= OnSectorUnloading;
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
            if (session.Combat.IsActive)
            {
                Resolve(command, WorldInteractionResultCode.Cancelled, true);
                return;
            }
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
                    InteractionRangeRules.For(command.Type)))
            {
                Resolve(command, WorldInteractionResultCode.NoReachableInteractionPosition, true);
                return;
            }
            Execute(command);
        }

        public WorldInteractionResult TryUse(ArcanumObjectId target)
        {
            WorldInteractionCommand command = BeginCommand(target, WorldInteractionCommandType.Use);
            if (!TryResolveActor(command, out WorldMapSessionCoordinator session))
                return Complete(command, WorldInteractionResultCode.ActorNotFound);
            if (!session.TryGetObjectState(target, out PersistentObjectState targetState)
                || !session.TryGetLoadedObject(target, out WorldObject runtime))
                return Complete(command, WorldInteractionResultCode.TargetNotFound);
            if (targetState.Type != runtime.Type
                || targetState.Type != ObjectType.Portal && !session.IsAreaEntranceTarget(target))
                return Complete(command, WorldInteractionResultCode.InvalidTarget);
            return TryApproachOrExecute(command, targetState, InteractionRangeRules.PortalUseRange);
        }

        public WorldInteractionResult TryPickUp(ArcanumObjectId item)
        {
            WorldInteractionCommand command = BeginCommand(item, WorldInteractionCommandType.PickUp);
            if (!TryResolveActor(command, out WorldMapSessionCoordinator session))
                return Complete(command, WorldInteractionResultCode.ActorNotFound);
            if (!session.TryGetObjectState(item, out PersistentObjectState itemState))
                return Complete(command, WorldInteractionResultCode.ItemNotFound);
            if (!IsItemType(itemState.Type))
                return Complete(command, WorldInteractionResultCode.InvalidItem);
            if (itemState.Placement.Kind != ObjectPlacementKind.World)
                return Complete(command, WorldInteractionResultCode.AlreadyContained);
            if (itemState.Off || !session.TryGetLoadedObject(item, out WorldObject runtime)
                || runtime.Type != itemState.Type)
                return Complete(command, WorldInteractionResultCode.ItemNotInWorld);
            return TryApproachOrExecute(command, itemState, InteractionRangeRules.ItemPickupRange);
        }

        public WorldInteractionResult TryTalk(ArcanumObjectId npc)
        {
            WorldInteractionCommand command = BeginCommand(npc, WorldInteractionCommandType.Talk);
            if (!TryResolveActor(command, out WorldMapSessionCoordinator session))
                return Complete(command, WorldInteractionResultCode.ActorNotFound);
            if (!session.TryGetObjectState(npc, out PersistentObjectState npcState)
                || !session.TryGetLoadedObject(npc, out WorldObject runtime))
                return Complete(command, WorldInteractionResultCode.TargetNotFound);
            if (npcState.Type != ObjectType.Npc || runtime.Type != ObjectType.Npc || npcState.DialogNum <= 0)
                return Complete(command, WorldInteractionResultCode.InvalidTarget);
            return TryApproachOrExecute(command, npcState, InteractionRangeRules.TalkStartRange,
                InteractionRangeRules.TalkApproachRange);
        }

        private WorldInteractionResult TryApproachOrExecute(WorldInteractionCommand command,
            PersistentObjectState targetState, int range)
            => TryApproachOrExecute(command, targetState, range, range);

        private WorldInteractionResult TryApproachOrExecute(WorldInteractionCommand command,
            PersistentObjectState targetState, int executionRange, int approachRange)
        {
            WorldMapSessionCoordinator session = _loader.Session;
            if (!TryTargetMapPosition(targetState, out Vector2 targetPosition))
                return Complete(command, WorldInteractionResultCode.TargetNotFound);
            if (InteractionRangeRules.IsWithin(session.PlayerState.MapPosition, targetPosition,
                    executionRange))
                return Execute(command.At(Vector2Int.RoundToInt(session.PlayerState.MapPosition)));
            if (!SectorCoordinate.TryParse(session.SelectedSector, out SectorCoordinate sector)
                || targetState.Placement.Kind != ObjectPlacementKind.World
                || targetState.Placement.Sector != sector.Path)
                return Complete(command, WorldInteractionResultCode.TargetNotFound);

            if (_navigation.Player == null || _loader.NavigationMap == null)
                return Complete(command, WorldInteractionResultCode.ActorNotFound);

            Vector2Int start = Vector2Int.RoundToInt(_navigation.Player.TilePosition);
            Vector2Int targetTile = Vector2Int.RoundToInt(targetState.Placement.TilePosition);
            if (!_approachPlanner.TryPlan(_loader.NavigationMap, start, targetTile,
                    approachRange, out Vector2Int localDestination, _approachRoute))
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

        private WorldInteractionCommand BeginCommand(ArcanumObjectId target, WorldInteractionCommandType type)
        {
            if (Phase == PlayerInteractionPhase.ApproachingTarget && PendingCommand.HasValue)
                Resolve(PendingCommand.Value, WorldInteractionResultCode.Cancelled, true);
            TryResolveDependencies();
            ArcanumObjectId actor = _loader?.Session.PlayerState?.Identity ?? default;
            return new WorldInteractionCommand(actor, target, type);
        }

        private bool TryResolveActor(WorldInteractionCommand command, out WorldMapSessionCoordinator session)
        {
            session = TryResolveDependencies() ? _loader.Session : null;
            return session?.PlayerState != null && command.Actor.IsPersistent
                   && session.PlayerState.Identity == command.Actor;
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

        private void OnSectorUnloading(string _)
        {
            if (Phase == PlayerInteractionPhase.ApproachingTarget && PendingCommand.HasValue)
                Resolve(PendingCommand.Value, WorldInteractionResultCode.Cancelled, false);
            else if (Phase != PlayerInteractionPhase.Executing)
            {
                PendingCommand = null;
                Phase = PlayerInteractionPhase.Idle;
            }
        }

        private static bool TryTargetMapPosition(PersistentObjectState state, out Vector2 position)
        {
            if (state != null && state.Placement.Kind == ObjectPlacementKind.World
                && SectorCoordinate.TryParse(state.Placement.Sector, out SectorCoordinate sector))
            {
                position = sector.ToGlobal(state.Placement.TilePosition);
                return true;
            }
            position = default;
            return false;
        }

        private static bool IsItemType(ObjectType type)
            => type >= ObjectType.Weapon && type <= ObjectType.Generic;

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
