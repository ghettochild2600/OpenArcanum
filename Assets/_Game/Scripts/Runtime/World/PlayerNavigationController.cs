using System;
using System.Collections.Generic;
using Arcanum.Formats.Art;
using Arcanum.Formats.Objects;
using UnityEngine;

namespace Arcanum.Runtime.World
{
    /// <summary>
    /// Owns local-player movement intent and route following. The selected player remains a
    /// production WorldObject; session state owns position/art state and the sprite owner renders it.
    /// </summary>
    [RequireComponent(typeof(WorldObjectSectorLoader))]
    public sealed class PlayerNavigationController : MonoBehaviour
    {
        private const int StandAnimation = 0;
        private const int WalkAnimation = 1;

        [SerializeField, Min(0.1f)] private float walkSpeedTilesPerSecond = 4f;
        [SerializeField] private string playerObjectId;

        private readonly DeterministicTilePathfinder _pathfinder = new();
        private readonly CrossSectorBoundaryPlanner _boundaryPlanner = new();
        private readonly List<Vector2Int> _route = new();
        private readonly TileRouteFollower _follower = new();
        private readonly SectorEntryFrameHold _entryFrameHold = new();
        private readonly HashSet<string> _rejectedBoundaryExits = new(StringComparer.Ordinal);
        private WorldObjectSectorLoader _loader;
        private int _facing = 4;
        private CrossSectorBoundaryPlanner.Plan? _pendingBoundary;
        private bool _passiveTransitionTriggered;

        public WorldObject Player { get; private set; }
        public bool IsMoving => _follower.IsMoving;
        public bool LastPathSucceeded { get; private set; }
        public int RemainingWaypoints => _follower.RemainingWaypoints;
        public Vector2Int? Destination { get; private set; }
        public Vector2Int? GlobalDestination => _loader?.Session.PlayerState?.Destination;
        public IReadOnlyList<Vector2Int> Route => _route;
        public event Action DestinationRequested;

        private void Awake() => _loader = GetComponent<WorldObjectSectorLoader>();

        private void Update()
        {
            if (Player == null) TryBindConfiguredPlayer();
            if (_entryFrameHold.Consume()) return;
            AdvanceNavigation(Time.deltaTime * walkSpeedTilesPerSecond);
        }

        internal void AdvanceNavigation(float tileSteps)
        {
            if (Player == null || !_follower.IsMoving) return;

            int facingBefore = _follower.Facing;
            if (facingBefore >= 0 && facingBefore != _facing)
            {
                _facing = facingBefore;
                ApplyState(_follower.Position, WalkAnimation, true);
            }

            _passiveTransitionTriggered = false;
            bool moving = _follower.Advance(tileSteps, OnRouteTileEntered);
            if (_passiveTransitionTriggered) return;
            int facingAfter = _follower.Facing;
            if (facingAfter >= 0) _facing = facingAfter;
            if (moving)
            {
                ApplyState(_follower.Position, WalkAnimation, true);
                return;
            }
            if (_pendingBoundary.HasValue)
            {
                ApplyState(_follower.Position, WalkAnimation, true);
                CompleteBoundaryTransition();
                return;
            }
            ApplyState(_follower.Position, StandAnimation, false);
            Destination = null;
            _loader.Session.ClearPlayerDestination();
        }

        private bool OnRouteTileEntered(Vector2Int tile, int facing)
        {
            if (facing >= 0) _facing = facing;
            ApplyState(tile, WalkAnimation, true);
            _passiveTransitionTriggered = TryActivatePassiveJump();
            return _passiveTransitionTriggered;
        }

        private bool TryActivatePassiveJump()
        {
            if (Player == null || _loader?.Session == null) return false;
            MapTransitionResult result = _loader.Session.RequestCurrentJumpPoint(Player.Identity);
            if (result.Succeeded)
            {
                _entryFrameHold.Arm();
                return true;
            }
            return false;
        }

        public bool TryBindConfiguredPlayer()
        {
            if (_loader == null) _loader = GetComponent<WorldObjectSectorLoader>();
            if (_loader == null || !_loader.IsLoaded) return false;

            var candidates = new List<WorldObject>();
            foreach (WorldObjectSpriteOwner owner in _loader.SpriteOwners)
                if (owner != null && owner.WorldObject != null) candidates.Add(owner.WorldObject);
            string identity = _loader.Session.PlayerState?.Identity.Key ?? playerObjectId;
            WorldObject selected = SelectProductionPlayer(candidates, identity);
            return TryBind(selected);
        }

        public bool TryBind(WorldObject player)
        {
            if (player == null || !IsCritterArt(player.ArtId) || !player.Identity.IsPersistent) return false;
            if (_loader == null) _loader = GetComponent<WorldObjectSectorLoader>();
            if (_loader == null) return false;
            CancelSegment(player.TilePosition);
            Player = player;
            _facing = CritterArtResolver.RotationOf(player.ArtId);
            _follower.Cancel(player.TilePosition);
            if (_loader.Session.IsMapTransitionActive) _entryFrameHold.Arm();
            _loader.NavigationMap?.SetControlledObject(player);
            ApplyState(player.TilePosition, StandAnimation, false);
            return true;
        }

        public bool TrySetDestination(Vector2Int destination)
        {
            if (Player == null || _loader?.NavigationMap == null
                || !SectorCoordinate.TryParse(_loader.Session.SelectedSector, out SectorCoordinate sector)) return false;
            return TrySetGlobalDestination(Vector2Int.RoundToInt(sector.ToGlobal(destination)));
        }

        /// <summary>Requests one final destination in source map-global tile coordinates.</summary>
        public bool TrySetGlobalDestination(Vector2Int destination)
        {
            if (Player == null || _loader?.NavigationMap == null) return false;
            DestinationRequested?.Invoke();
            CancelRoute();
            _rejectedBoundaryExits.Clear();
            _loader.Session.SetPlayerDestination(destination);
            return StartNextSegment();
        }

        private bool StartNextSegment()
        {
            if (Player == null || _loader?.NavigationMap == null || !GlobalDestination.HasValue
                || !SectorCoordinate.TryParse(_loader.Session.SelectedSector, out SectorCoordinate sector))
                return StopNavigation(false);

            Vector2Int start = new Vector2Int(
                Mathf.RoundToInt(Player.TilePosition.x),
                Mathf.RoundToInt(Player.TilePosition.y));
            Vector2Int final = GlobalDestination.Value;
            SectorCoordinate destinationSector = SectorCoordinate.FromGlobal(sector.MapPath, final);
            _pendingBoundary = null;

            if (destinationSector == sector)
            {
                Vector2Int localDestination = sector.ToLocal(final);
                LastPathSucceeded = _pathfinder.TryFindPath(_loader.NavigationMap, start, localDestination, _route);
                if (!LastPathSucceeded) return StopNavigation(false);
                return BeginSegment(localDestination, _route, true);
            }

            LastPathSucceeded = _boundaryPlanner.TryPlan(sector, _loader.NavigationMap, start, final,
                _loader.SectorExists, out CrossSectorBoundaryPlanner.Plan plan,
                candidate => !_rejectedBoundaryExits.Contains(BoundaryKey(sector, candidate)));
            if (!LastPathSucceeded) return StopNavigation(false);
            _pendingBoundary = plan;
            return BeginSegment(plan.ExitTile, plan.Route, false);
        }

        private bool BeginSegment(Vector2Int destination, IReadOnlyList<Vector2Int> route, bool finalArrival)
        {
            Destination = destination;
            if (!ReferenceEquals(route, _route))
            {
                _route.Clear();
                foreach (Vector2Int tile in route) _route.Add(tile);
            }
            _follower.Replace(Player.TilePosition, _route);
            if (!_follower.IsMoving)
            {
                if (!finalArrival && _pendingBoundary.HasValue) return CompleteBoundaryTransition();
                ApplyState(Player.TilePosition, StandAnimation, false);
                Destination = null;
                _loader.Session.ClearPlayerDestination();
                return true;
            }
            _facing = _follower.Facing;
            ApplyState(_follower.Position, WalkAnimation, true);
            return true;
        }

        private bool CompleteBoundaryTransition()
        {
            if (!_pendingBoundary.HasValue || Player == null) return StopNavigation(false);
            CrossSectorBoundaryPlanner.Plan plan = _pendingBoundary.Value;
            string previousSector = _loader.Session.SelectedSector;
            Vector2 previousLocal = Player.TilePosition;
            uint crossingArt = CritterArtResolver.WithAnimRotation(Player.ArtId, WalkAnimation, plan.Rotation)
                               & ~(0x1Fu << 14);

            if (!_loader.Session.TryTransitionPlayer(plan.TargetSector.Path, plan.EntryTile, crossingArt)
                || Player == null || _loader.NavigationMap == null)
                return StopNavigation(false);

            int opposite = (plan.Rotation + 4) & 7;
            if (!_loader.NavigationMap.CanExit(plan.EntryTile, opposite))
                return RejectBoundaryAndRetry(plan, previousSector, previousLocal);

            _pendingBoundary = null;
            bool continued;
            Vector2Int final = GlobalDestination.Value;
            SectorCoordinate destinationSector = SectorCoordinate.FromGlobal(plan.TargetSector.MapPath, final);
            if (destinationSector == plan.TargetSector)
            {
                Vector2Int localDestination = plan.TargetSector.ToLocal(final);
                if (!_pathfinder.TryFindPath(_loader.NavigationMap, plan.EntryTile, localDestination, _route))
                    return RejectBoundaryAndRetry(plan, previousSector, previousLocal);
                LastPathSucceeded = true;
                continued = BeginSegment(localDestination, _route, true);
            }
            else
            {
                continued = StartNextSegment();
            }
            if (continued) _entryFrameHold.Arm();
            return continued;
        }

        private bool RejectBoundaryAndRetry(CrossSectorBoundaryPlanner.Plan plan,
            string previousSector, Vector2 previousLocal)
        {
            _rejectedBoundaryExits.Add(BoundaryKey(new SectorCoordinate(
                plan.TargetSector.MapPath,
                plan.TargetSector.X - IsoProjection.DirDelta[plan.Rotation].x,
                plan.TargetSector.Y - IsoProjection.DirDelta[plan.Rotation].y), plan));
            uint standArt = CritterArtResolver.WithAnimRotation(Player.ArtId, StandAnimation, plan.Rotation)
                            & ~(0x1Fu << 14);
            if (!_loader.Session.TryTransitionPlayer(previousSector, previousLocal, standArt))
                return StopNavigation(false);
            _pendingBoundary = null;
            bool retried = StartNextSegment();
            if (retried) _entryFrameHold.Arm();
            return retried;
        }

        private static string BoundaryKey(SectorCoordinate source, CrossSectorBoundaryPlanner.Plan plan)
            => $"{source.Path}:{plan.ExitTile.x},{plan.ExitTile.y}:{plan.Rotation}";

        public void CancelRoute()
        {
            Vector2 position = Player != null ? Player.TilePosition : _follower.Position;
            CancelSegment(position);
            _pendingBoundary = null;
            _rejectedBoundaryExits.Clear();
            Destination = null;
            _loader?.Session.ClearPlayerDestination();
            if (Player != null) ApplyState(position, StandAnimation, false);
        }

        public void Unbind(WorldObject expected = null)
        {
            if (expected != null && Player != expected) return;
            CancelSegment(Player != null ? Player.TilePosition : _follower.Position);
            _loader?.NavigationMap?.SetControlledObject(null);
            Player = null;
            LastPathSucceeded = false;
        }

        private void CancelSegment(Vector2 position)
        {
            _follower.Cancel(position);
            _route.Clear();
            Destination = null;
        }

        private bool StopNavigation(bool result)
        {
            Vector2 position = Player != null ? Player.TilePosition : _follower.Position;
            CancelSegment(position);
            _pendingBoundary = null;
            _rejectedBoundaryExits.Clear();
            _loader?.Session.ClearPlayerDestination();
            if (Player != null) ApplyState(position, StandAnimation, false);
            LastPathSucceeded = result;
            return result;
        }

        internal static WorldObject SelectProductionPlayer(IEnumerable<WorldObject> objects, string objectId)
        {
            WorldObject firstPc = null;
            foreach (WorldObject candidate in objects)
            {
                if (candidate == null || candidate.Type != ObjectType.Pc) continue;
                if (!string.IsNullOrWhiteSpace(objectId)
                    && string.Equals(candidate.Oid, objectId, StringComparison.OrdinalIgnoreCase))
                    return candidate;
                firstPc ??= candidate;
            }
            return string.IsNullOrWhiteSpace(objectId) ? firstPc : null;
        }

        private void ApplyState(Vector2 position, int animation, bool moving)
        {
            if (Player == null) return;
            uint artId = CritterArtResolver.WithAnimRotation(Player.ArtId, animation, _facing);
            artId &= ~(0x1Fu << 14); // runtime frame is owned by SpriteFrameAnimator, not persistent ART bits
            _loader.Session.SetMovementState(Player.Identity, position, artId, moving);
        }

        private static bool IsCritterArt(uint artId)
        {
            int type = ArtId.Type(artId);
            return type == ArtId.TypeCritter || type == ArtId.TypeMonster || type == ArtId.TypeUniqueNpc;
        }
    }

    internal sealed class SectorEntryFrameHold
    {
        public bool Pending { get; private set; }

        public void Arm() => Pending = true;

        public bool Consume()
        {
            if (!Pending) return false;
            Pending = false;
            return true;
        }
    }
}
