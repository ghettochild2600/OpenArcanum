using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arcanum.Runtime.World
{
    /// <summary>Allocation-free smooth traversal of an already validated tile route.</summary>
    public sealed class TileRouteFollower
    {
        private readonly List<Vector2Int> _route = new();
        private int _next;

        public Vector2 Position { get; private set; }
        public bool IsMoving => _next < _route.Count;
        public int RemainingWaypoints => _route.Count - _next;

        public int Facing
        {
            get
            {
                if (!IsMoving) return -1;
                Vector2 delta = (Vector2)_route[_next] - Position;
                return IsoProjection.DirFromDelta(Math.Sign(delta.x), Math.Sign(delta.y));
            }
        }

        public void Replace(Vector2 start, IReadOnlyList<Vector2Int> route)
        {
            Position = start;
            _route.Clear();
            if (route != null)
                for (int i = 0; i < route.Count; i++) _route.Add(route[i]);
            _next = 0;
        }

        public void Cancel(Vector2 position)
        {
            Position = position;
            _route.Clear();
            _next = 0;
        }

        /// <summary>Advances by source-grid steps; all eight directions consume one step per tile.</summary>
        public bool Advance(float tileSteps, Func<Vector2Int, int, bool> onTileEntered = null)
        {
            tileSteps = Mathf.Max(0f, tileSteps);
            while (IsMoving && tileSteps > 0f)
            {
                Vector2 target = _route[_next];
                Vector2 delta = target - Position;
                float remaining = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
                if (remaining <= 0.00001f)
                {
                    Position = target;
                    _next++;
                    continue;
                }

                float used = Mathf.Min(tileSteps, remaining);
                Position += delta * (used / remaining);
                tileSteps -= used;
                if (used >= remaining - 0.00001f)
                {
                    Position = target;
                    Vector2Int entered = _route[_next];
                    _next++;
                    int facing = IsoProjection.DirFromDelta(Math.Sign(delta.x), Math.Sign(delta.y));
                    // Observe every crossed source tile, even when one frame consumes several waypoints.
                    // A successful lifecycle change stops this old route before any residual movement is spent.
                    if (onTileEntered?.Invoke(entered, facing) == true) return IsMoving;
                }
            }
            return IsMoving;
        }
    }
}
