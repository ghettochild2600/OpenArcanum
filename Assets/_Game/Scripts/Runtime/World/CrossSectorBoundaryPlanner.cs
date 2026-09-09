using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arcanum.Runtime.World
{
    /// <summary>Chooses one deterministic legal cardinal exit toward a map-global destination.</summary>
    public sealed class CrossSectorBoundaryPlanner
    {
        public readonly struct Plan
        {
            public SectorCoordinate TargetSector { get; }
            public Vector2Int ExitTile { get; }
            public Vector2Int EntryTile { get; }
            public int Rotation { get; }
            public IReadOnlyList<Vector2Int> Route { get; }

            internal Plan(SectorCoordinate targetSector, Vector2Int exitTile, Vector2Int entryTile,
                int rotation, IReadOnlyList<Vector2Int> route)
            {
                TargetSector = targetSector;
                ExitTile = exitTile;
                EntryTile = entryTile;
                Rotation = rotation;
                Route = route;
            }
        }

        private readonly DeterministicTilePathfinder _pathfinder = new();

        public bool TryPlan(SectorCoordinate currentSector, SectorNavigationMap map, Vector2Int start,
            Vector2Int globalDestination, Func<string, bool> sectorExists, out Plan plan,
            Func<Plan, bool> accept = null)
        {
            plan = default;
            if (map == null || sectorExists == null || !map.Contains(start)) return false;
            SectorCoordinate destinationSector = SectorCoordinate.FromGlobal(currentSector.MapPath, globalDestination);
            int dx = Math.Sign(destinationSector.X - currentSector.X);
            int dy = Math.Sign(destinationSector.Y - currentSector.Y);
            if (dx == 0 && dy == 0) return false;

            var directions = new List<int>(2);
            int xDistance = Math.Abs(destinationSector.X - currentSector.X);
            int yDistance = Math.Abs(destinationSector.Y - currentSector.Y);
            int xRotation = dx < 0 ? 1 : 5;
            int yRotation = dy < 0 ? 7 : 3;
            if (dx != 0 && (xDistance >= yDistance || dy == 0)) directions.Add(xRotation);
            if (dy != 0) directions.Add(yRotation);
            if (dx != 0 && xDistance < yDistance) directions.Add(xRotation);

            int bestScore = int.MaxValue;
            int bestDirectionOrder = int.MaxValue;
            int bestCrossTrackDistance = int.MaxValue;
            int bestBoundaryOrder = int.MaxValue;
            List<Vector2Int> bestRoute = null;
            SectorCoordinate bestTarget = default;
            Vector2Int bestExit = default;
            Vector2Int bestEntry = default;
            int bestRotation = -1;

            for (int directionOrder = 0; directionOrder < directions.Count; directionOrder++)
            {
                int rotation = directions[directionOrder];
                Vector2Int delta = IsoProjection.DirDelta[rotation];
                SectorCoordinate target = currentSector.Neighbor(delta.x, delta.y);
                if (!sectorExists(target.Path)) continue;

                for (int boundaryOrder = 0; boundaryOrder < SectorCoordinate.Size; boundaryOrder++)
                {
                    Vector2Int exit = BoundaryTile(rotation, boundaryOrder);
                    if (!map.CanExit(exit, rotation)) continue;
                    var route = new List<Vector2Int>();
                    if (!_pathfinder.TryFindPath(map, start, exit, route)) continue;
                    Vector2Int entry = SectorNavigationMap.EntryTile(exit, rotation);
                    var candidate = new Plan(target, exit, entry, rotation, route);
                    if (accept != null && !accept(candidate)) continue;
                    Vector2Int entryGlobal = Vector2Int.RoundToInt(target.ToGlobal(entry));
                    int remaining = Math.Max(Math.Abs(globalDestination.x - entryGlobal.x),
                        Math.Abs(globalDestination.y - entryGlobal.y));
                    int destinationBoundaryCoordinate = rotation == 1 || rotation == 5
                        ? Mathf.FloorToInt(currentSector.ToLocal(globalDestination).y)
                        : Mathf.FloorToInt(currentSector.ToLocal(globalDestination).x);
                    int crossTrackDistance = Math.Abs(boundaryOrder - destinationBoundaryCoordinate);
                    int score = (route.Count + remaining) * SectorCoordinate.Size + crossTrackDistance;
                    if (score > bestScore
                        || score == bestScore && directionOrder > bestDirectionOrder
                        || score == bestScore && directionOrder == bestDirectionOrder
                        && crossTrackDistance > bestCrossTrackDistance
                        || score == bestScore && directionOrder == bestDirectionOrder
                        && crossTrackDistance == bestCrossTrackDistance && boundaryOrder >= bestBoundaryOrder)
                        continue;

                    bestScore = score;
                    bestDirectionOrder = directionOrder;
                    bestCrossTrackDistance = crossTrackDistance;
                    bestBoundaryOrder = boundaryOrder;
                    bestRoute = route;
                    bestTarget = target;
                    bestExit = exit;
                    bestEntry = entry;
                    bestRotation = rotation;
                }
            }

            if (bestRoute == null) return false;
            plan = new Plan(bestTarget, bestExit, bestEntry, bestRotation, bestRoute);
            return true;
        }

        private static Vector2Int BoundaryTile(int rotation, int index)
        {
            switch (rotation)
            {
                case 1: return new Vector2Int(0, index);
                case 3: return new Vector2Int(index, SectorCoordinate.Size - 1);
                case 5: return new Vector2Int(SectorCoordinate.Size - 1, index);
                case 7: return new Vector2Int(index, 0);
                default: throw new ArgumentOutOfRangeException(nameof(rotation));
            }
        }
    }
}
