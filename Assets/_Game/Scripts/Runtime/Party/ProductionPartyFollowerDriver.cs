using Arcanum.Runtime.World;
using Arcanum.Formats.Art;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
using System.Collections.Generic;
using UnityEngine;

namespace Arcanum.Runtime.Party
{
    [DisallowMultipleComponent]
    public sealed class ProductionPartyFollowerDriver : MonoBehaviour
    {
        private WorldObjectSectorLoader _loader;
        private PartyFollowerMovementService _movement;
        private readonly Dictionary<ArcanumObjectId, ActiveFollowerRoute> _active = new();

        private void Awake() => Bind();
        private void OnEnable() => Bind();

        private void Bind()
        {
            _loader = GetComponent<WorldObjectSectorLoader>();
            _movement = _loader == null ? null : new PartyFollowerMovementService(_loader.Session);
            _active.Clear();
        }

        private void Update()
        {
            if (_loader == null || _movement == null || _loader.NavigationMap == null) return;
            var members = _loader.Session.Party.Members;
            if (_active.Count > members.Count)
            {
                var stale = new List<ArcanumObjectId>();
                foreach (ArcanumObjectId identity in _active.Keys)
                    if (!_loader.Session.Party.IsMember(identity)) stale.Add(identity);
                for (int index = 0; index < stale.Count; index++) _active.Remove(stale[index]);
            }
            for (int index = 0; index < members.Count; index++)
                Advance(members[index].Identity, Time.deltaTime);
        }

        private void Advance(ArcanumObjectId identity, float deltaSeconds)
        {
            if (!_active.TryGetValue(identity, out ActiveFollowerRoute active))
            {
                var route = new List<Vector2Int>();
                if (_movement.TryPlanRoute(identity, _loader.NavigationMap, route) != FollowerMoveResult.Moved
                    || !_loader.Session.TryGetLoadedObject(identity, out WorldObject runtime)) return;
                active = new ActiveFollowerRoute(runtime, route);
                _active.Add(identity, active);
                Present(active, active.Follower.Facing, true);
            }

            if (!_loader.Session.Party.CanAccompany(identity)
                || !_loader.Session.TryGetLoadedObject(identity, out WorldObject current)
                || current != active.Runtime)
            {
                _active.Remove(identity);
                return;
            }

            WorldObjectSpriteOwner owner = current.GetComponentInChildren<WorldObjectSpriteOwner>();
            if (owner == null) return;
            int speed = _loader.Session.DerivedStats.GetDerivedStat(identity, CharacterDerivedStat.Speed);
            float steps = owner.AdvanceLocomotion(deltaSeconds, speed);
            if (steps <= 0f) return;
            int facing = active.Follower.Facing;
            Vector2 rollback = current.TilePosition;
            bool blocked = false;
            bool moving = active.Follower.Advance(steps, (tile, enteredFacing) =>
            {
                blocked = !_loader.NavigationMap.MoveRegisteredObject(identity, tile);
                return blocked;
            });
            if (blocked)
            {
                active.Follower.Cancel(rollback);
                Present(active, facing, false);
                _active.Remove(identity);
                return;
            }
            if (moving)
            {
                int nextFacing = active.Follower.Facing;
                Present(active, nextFacing >= 0 ? nextFacing : facing, true);
                return;
            }
            Present(active, facing, false);
            _active.Remove(identity);
        }

        private void Present(ActiveFollowerRoute active, int facing, bool moving)
        {
            if (facing < 0) facing = CritterArtResolver.RotationOf(active.Runtime.ArtId);
            uint art = CritterArtResolver.WithAnimRotation(active.Runtime.ArtId, moving ? 1 : 0, facing)
                       & ~(0x1Fu << 14);
            _loader.Session.SetMovementState(active.Runtime.Identity, active.Follower.Position, art, moving);
        }

        private sealed class ActiveFollowerRoute
        {
            public WorldObject Runtime { get; }
            public TileRouteFollower Follower { get; } = new();
            public ActiveFollowerRoute(WorldObject runtime, IReadOnlyList<Vector2Int> route)
            {
                Runtime = runtime;
                Follower.Replace(runtime.TilePosition, route);
            }
        }
    }
}
