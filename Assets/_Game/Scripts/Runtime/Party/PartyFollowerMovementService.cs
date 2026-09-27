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
        public const int DesiredRange = 4;
        private readonly WorldMapSessionCoordinator _world;
        private readonly DeterministicTilePathfinder _pathfinder = new();
        private readonly List<Vector2Int> _route = new();

        public PartyFollowerMovementService(WorldMapSessionCoordinator world)
            => _world = world ?? throw new ArgumentNullException(nameof(world));

        public FollowerMoveResult AdvanceOneStep(ArcanumObjectId follower, SectorNavigationMap map)
        {
            if (_world.Combat.IsActive) return FollowerMoveResult.CombatActive;
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
            if (Distance(start, leader) <= DesiredRange) return FollowerMoveResult.InRange;

            var candidates = new List<Vector2Int>();
            for (int y = leader.y - DesiredRange; y <= leader.y + DesiredRange; y++)
            for (int x = leader.x - DesiredRange; x <= leader.x + DesiredRange; x++)
            {
                var tile = new Vector2Int(x, y);
                if (Distance(tile, leader) > DesiredRange || !map.Contains(tile) || !map.IsWalkable(tile)
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
                Vector2Int next = _route[0];
                if (!map.MoveRegisteredObject(follower, next)) continue;
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
            return FollowerMoveResult.Blocked;
        }

        private static int Distance(Vector2Int left, Vector2Int right)
            => Math.Max(Math.Abs(left.x - right.x), Math.Abs(left.y - right.y));
    }
}
