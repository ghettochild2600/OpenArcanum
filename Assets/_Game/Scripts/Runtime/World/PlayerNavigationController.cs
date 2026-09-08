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
        private readonly List<Vector2Int> _route = new();
        private readonly TileRouteFollower _follower = new();
        private WorldObjectSectorLoader _loader;
        private int _facing = 4;

        public WorldObject Player { get; private set; }
        public bool IsMoving => _follower.IsMoving;
        public bool LastPathSucceeded { get; private set; }
        public int RemainingWaypoints => _follower.RemainingWaypoints;
        public Vector2Int? Destination { get; private set; }
        public IReadOnlyList<Vector2Int> Route => _route;

        private void Awake() => _loader = GetComponent<WorldObjectSectorLoader>();

        private void Update()
        {
            if (Player == null) TryBindConfiguredPlayer();
            if (Player == null || !_follower.IsMoving) return;

            int facingBefore = _follower.Facing;
            if (facingBefore >= 0 && facingBefore != _facing)
            {
                _facing = facingBefore;
                ApplyState(_follower.Position, WalkAnimation, true);
            }

            bool moving = _follower.Advance(Time.deltaTime * walkSpeedTilesPerSecond);
            int facingAfter = _follower.Facing;
            if (facingAfter >= 0) _facing = facingAfter;
            ApplyState(_follower.Position, moving ? WalkAnimation : StandAnimation, moving);
            if (!moving) Destination = null;
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
            CancelRoute();
            Player = player;
            _facing = CritterArtResolver.RotationOf(player.ArtId);
            _follower.Cancel(player.TilePosition);
            _loader.NavigationMap?.SetControlledObject(player);
            ApplyState(player.TilePosition, StandAnimation, false);
            return true;
        }

        public bool TrySetDestination(Vector2Int destination)
        {
            if (Player == null || _loader?.NavigationMap == null) return false;
            CancelRoute();
            Vector2Int start = new Vector2Int(
                Mathf.RoundToInt(Player.TilePosition.x),
                Mathf.RoundToInt(Player.TilePosition.y));
            LastPathSucceeded = _pathfinder.TryFindPath(_loader.NavigationMap, start, destination, _route);
            if (!LastPathSucceeded) return false;

            Destination = destination;
            _follower.Replace(Player.TilePosition, _route);
            if (!_follower.IsMoving)
            {
                Destination = null;
                return true;
            }
            _facing = _follower.Facing;
            ApplyState(_follower.Position, WalkAnimation, true);
            return true;
        }

        public void CancelRoute()
        {
            Vector2 position = Player != null ? Player.TilePosition : _follower.Position;
            _follower.Cancel(position);
            Destination = null;
            if (Player != null) ApplyState(position, StandAnimation, false);
        }

        public void Unbind(WorldObject expected = null)
        {
            if (expected != null && Player != expected) return;
            _follower.Cancel(Player != null ? Player.TilePosition : _follower.Position);
            Destination = null;
            _loader?.NavigationMap?.SetControlledObject(null);
            Player = null;
            LastPathSucceeded = false;
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
}
