using System.Collections.Generic;
using UnityEngine;

namespace Arcanum.Runtime.World
{
    /// <summary>Chooses the shortest deterministic reachable source-grid position within interaction range.</summary>
    public sealed class InteractionApproachPlanner
    {
        private readonly DeterministicTilePathfinder _pathfinder = new();

        public bool TryPlan(SectorNavigationMap map, Vector2Int start, Vector2Int target, int range,
            out Vector2Int destination, List<Vector2Int> result)
        {
            destination = default;
            result?.Clear();
            if (map == null || result == null || range < 0 || !map.Contains(start) || !map.Contains(target))
                return false;
            if (InteractionRangeRules.IsWithin(start, target, range))
            {
                destination = start;
                return true;
            }

            List<Vector2Int> best = null;
            int bestLength = int.MaxValue;
            int minX = Mathf.Max(0, target.x - range);
            int maxX = Mathf.Min(SectorCoordinate.Size - 1, target.x + range);
            int minY = Mathf.Max(0, target.y - range);
            int maxY = Mathf.Min(SectorCoordinate.Size - 1, target.y + range);

            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                var candidate = new Vector2Int(x, y);
                if (!map.IsWalkable(candidate)) continue;
                var route = new List<Vector2Int>();
                if (!_pathfinder.TryFindPath(map, start, candidate, route)) continue;
                if (route.Count >= bestLength) continue;
                bestLength = route.Count;
                destination = candidate;
                best = route;
            }

            if (best == null) return false;
            result.AddRange(best);
            return true;
        }
    }
}
