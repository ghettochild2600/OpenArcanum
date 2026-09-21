using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arcanum.Runtime.World
{
    /// <summary>Deterministic eight-direction A* over one Arcanum sector.</summary>
    public sealed class DeterministicTilePathfinder
    {
        private const int Size = 64;
        private const int Count = Size * Size;
        private const int StepCost = 10;

        private readonly int[] _cost = new int[Count];
        private readonly int[] _parent = new int[Count];
        private readonly bool[] _closed = new bool[Count];

        public bool TryFindPath(SectorNavigationMap map, Vector2Int start, Vector2Int destination, List<Vector2Int> result)
            => TryFindPath(map, start, destination, result, null);

        internal bool TryFindPath(SectorNavigationMap map, Vector2Int start, Vector2Int destination,
            List<Vector2Int> result, Func<Vector2Int, bool> canEnter)
        {
            result?.Clear();
            if (map == null || result == null || !map.Contains(start) || !map.IsWalkable(destination)
                || canEnter != null && !canEnter(destination)) return false;
            if (start == destination) return true;

            for (int i = 0; i < Count; i++)
            {
                _cost[i] = int.MaxValue;
                _parent[i] = -1;
                _closed[i] = false;
            }

            int startIndex = Index(start);
            int destinationIndex = Index(destination);
            _cost[startIndex] = 0;

            while (true)
            {
                int current = -1;
                int best = int.MaxValue;
                for (int i = 0; i < Count; i++)
                {
                    if (_closed[i] || _cost[i] == int.MaxValue) continue;
                    int score = _cost[i] + Heuristic(Tile(i), destination);
                    if (score < best)
                    {
                        best = score;
                        current = i;
                    }
                }

                if (current < 0) return false;
                if (current == destinationIndex) break;
                _closed[current] = true;
                Vector2Int tile = Tile(current);

                for (int rotation = 0; rotation < 8; rotation++)
                {
                    if (!map.CanTraverse(tile, rotation)) continue;
                    Vector2Int next = tile + IsoProjection.DirDelta[rotation];
                    if (canEnter != null && !canEnter(next)) continue;
                    int nextIndex = Index(next);
                    if (_closed[nextIndex]) continue;

                    int turnCost = 0;
                    if (_parent[current] >= 0)
                    {
                        Vector2Int previous = Tile(_parent[current]);
                        int priorRotation = IsoProjection.DirFromDelta(tile.x - previous.x, tile.y - previous.y);
                        if (priorRotation != rotation) turnCost = 1;
                    }

                    int candidate = _cost[current] + StepCost + turnCost;
                    if (candidate >= _cost[nextIndex]) continue;
                    _cost[nextIndex] = candidate;
                    _parent[nextIndex] = current;
                }
            }

            int cursor = destinationIndex;
            while (cursor != startIndex)
            {
                result.Add(Tile(cursor));
                cursor = _parent[cursor];
                if (cursor < 0)
                {
                    result.Clear();
                    return false;
                }
            }
            result.Reverse();
            return true;
        }

        private static int Heuristic(Vector2Int a, Vector2Int b)
            => StepCost * Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

        private static int Index(Vector2Int tile) => tile.y * Size + tile.x;
        private static Vector2Int Tile(int index) => new Vector2Int(index % Size, index / Size);
    }
}
