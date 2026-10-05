using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Art;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.World;
using UnityEngine;

namespace Arcanum.Runtime.Party
{
    public enum FollowerMoveResult
    {
        None,
        InRange,
        Moved,
        Blocked,
        Unavailable,
        CombatActive,
    }

    /// <summary>One deterministic source follow step over production sector navigation.</summary>
    public sealed class PartyFollowerMovementService
    {
        public const int SourceCloseRange = 2;
        public const int SourceFollowRange = 4;
        public const int SourceSpreadRange = 8;
        public const int DesiredRange = SourceFollowRange;
        private readonly WorldMapSessionCoordinator _world;
        private readonly DeterministicTilePathfinder _pathfinder = new();
        private readonly List<Vector2Int> _route = new();

        public PartyFollowerMovementService(WorldMapSessionCoordinator world)
            => _world = world ?? throw new ArgumentNullException(nameof(world));

        public FollowerMoveResult AdvanceOneStep(ArcanumObjectId follower, SectorNavigationMap map)
        {
            _route.Clear();
            FollowerMoveResult planned = TryPlanRoute(follower, map, _route);
            if (planned != FollowerMoveResult.Moved) return planned;
            WorldObject followerRuntime = null;
            _world.TryGetLoadedObject(follower, out followerRuntime);
            Vector2Int start = followerRuntime.Tile;
            Vector2Int next = _route[0];
            if (!map.MoveRegisteredObject(follower, next)) return FollowerMoveResult.Blocked;
            int facing = IsoProjection.DirFromDelta(next.x - start.x, next.y - start.y);
            uint original = followerRuntime.ArtId;
            uint walk = CritterArtResolver.WithAnimRotation(original, 1, facing) & ~(0x1Fu << 14);
            uint stand = CritterArtResolver.WithAnimRotation(walk, 0, facing) & ~(0x1Fu << 14);
            if (_world.SetMovementState(follower, next, walk, true)
                && _world.SetMovementState(follower, next, stand, false))
                return FollowerMoveResult.Moved;
            map.MoveRegisteredObject(follower, start);
            _world.SetMovementState(follower, start, original, false);
            return FollowerMoveResult.Unavailable;
        }

        /// <summary>Plans the ordinary source-follow route without moving authority or presentation.</summary>
        public FollowerMoveResult TryPlanRoute(ArcanumObjectId follower, SectorNavigationMap map,
            List<Vector2Int> route)
        {
            route?.Clear();
            if (_world.Combat.IsActive) return FollowerMoveResult.CombatActive;
            if (!_world.Party.FollowingEnabled) return FollowerMoveResult.Unavailable;
            if (!_world.Party.CanAccompany(follower) || map == null
                || !_world.TryGetLoadedObject(follower, out WorldObject followerRuntime)
                || _world.PlayerState == null
                || !_world.TryGetLoadedObject(_world.PlayerState.Identity, out WorldObject leaderRuntime)
                || !_world.TryGetObjectState(follower, out PersistentObjectState state)
                || state.Placement.Kind != ObjectPlacementKind.World
                || state.Placement.Sector != _world.SelectedSector)
                return FollowerMoveResult.Unavailable;

            Vector2Int start = followerRuntime.Tile;
            Vector2Int leader = leaderRuntime.Tile;
            int desiredRange = _world.Party.DesiredFollowRange;
            if (_world.Party.OrderedLocation.HasValue
                && SectorCoordinate.TryParse(_world.SelectedSector, out SectorCoordinate sector))
            {
                leader = sector.ToLocal(_world.Party.OrderedLocation.Value);
                desiredRange = 0;
            }
            if (Distance(start, leader) <= desiredRange) return FollowerMoveResult.InRange;

            var candidates = new List<Vector2Int>();
            for (int y = leader.y - desiredRange; y <= leader.y + desiredRange; y++)
            for (int x = leader.x - desiredRange; x <= leader.x + desiredRange; x++)
            {
                var tile = new Vector2Int(x, y);
                if (Distance(tile, leader) > desiredRange || !map.Contains(tile) || !map.IsWalkable(tile)
                    || map.IsOccupiedByOther(follower, tile)) continue;
                candidates.Add(tile);
            }
            foreach (Vector2Int destination in candidates.OrderBy(tile => Distance(start, tile))
                         .ThenBy(tile => tile.y).ThenBy(tile => tile.x))
            {
                _route.Clear();
                if (!_pathfinder.TryFindPath(map, start, destination, _route,
                        tile => !map.IsOccupiedByOther(follower, tile)) || _route.Count == 0)
                    continue;
                if (route != null && !ReferenceEquals(route, _route)) route.AddRange(_route);
                return FollowerMoveResult.Moved;
            }
            return FollowerMoveResult.Blocked;
        }

        private static int Distance(Vector2Int left, Vector2Int right)
            => Math.Max(Math.Abs(left.x - right.x), Math.Abs(left.y - right.y));
    }
}
