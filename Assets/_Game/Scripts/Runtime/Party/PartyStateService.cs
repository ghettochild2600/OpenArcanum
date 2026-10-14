using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.World;
using UnityEngine;

namespace Arcanum.Runtime.Party
{
    public enum PartyOrder
    {
        Follow = -1,
        Walk = 0,
        Attack = 1,
        StayClose = 2,
        SpreadOut = 3,
        BackOff = 4,
        Wait = 5,
    }

    public readonly struct PartyFollowerOrderState
    {
        public PartyOrder LastOrder { get; }
        public bool FollowingEnabled { get; }
        public int DesiredFollowRange { get; }
        public Vector2Int? OrderedLocation { get; }
        public ArcanumObjectId OrderedTarget { get; }

        internal PartyFollowerOrderState(PartyOrder lastOrder, bool followingEnabled,
            int desiredFollowRange, Vector2Int? orderedLocation, ArcanumObjectId orderedTarget)
        {
            LastOrder = lastOrder;
            FollowingEnabled = followingEnabled;
            DesiredFollowRange = desiredFollowRange;
            OrderedLocation = orderedLocation;
            OrderedTarget = orderedTarget;
        }

        internal static PartyFollowerOrderState SourceDefault => new(PartyOrder.Follow, true,
            PartyFollowerMovementService.SourceFollowRange, null, default);
    }

    public enum PartyMutationFailure
    {
        None,
        NoLeader,
        InvalidFollower,
        FollowerUnavailable,
        AlreadyFollowing,
        NotFollowing,
        CapacityReached,
    }

    public readonly struct PartyMember : IEquatable<PartyMember>
    {
        public ArcanumObjectId Identity { get; }
        public bool Forced { get; }

        public PartyMember(ArcanumObjectId identity, bool forced)
        {
            Identity = identity;
            Forced = forced;
        }

        public bool Equals(PartyMember other) => Identity == other.Identity && Forced == other.Forced;
        public override bool Equals(object obj) => obj is PartyMember other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Identity, Forced);
    }

    public readonly struct PartyMutationResult
    {
        public PartyMutationFailure Failure { get; }
        public ArcanumObjectId Follower { get; }
        public bool Succeeded => Failure == PartyMutationFailure.None;

        internal PartyMutationResult(PartyMutationFailure failure, ArcanumObjectId follower)
        {
            Failure = failure;
            Follower = follower;
        }
    }

    /// <summary>Session-owned stable-ID follower relationship, independent of scene hierarchy.</summary>
    public sealed class PartyStateService
    {
        private readonly WorldMapSessionCoordinator _world;
        private readonly List<PartyMember> _members = new();
        private readonly Dictionary<ArcanumObjectId, PartyFollowerOrderState> _orders = new();

        public ArcanumObjectId Leader => _world.PlayerState?.Identity ?? default;
        public IReadOnlyList<PartyMember> Members => _members;
        public int Count => _members.Count;
        public PartyOrder LastOrder { get; private set; } = PartyOrder.Follow;
        public bool FollowingEnabled { get; private set; } = true;
        public int DesiredFollowRange { get; private set; } = PartyFollowerMovementService.SourceFollowRange;
        public Vector2Int? OrderedLocation { get; private set; }
        public ArcanumObjectId OrderedTarget { get; private set; }
        public int Capacity => Leader.IsPersistent
            ? _world.DerivedStats.GetDerivedStat(Leader, CharacterDerivedStat.MaximumFollowers)
            : 0;
        public event Action MembershipChanged;
        public event Action<PartyOrder> OrderChanged;

        public PartyStateService(WorldMapSessionCoordinator world)
            => _world = world ?? throw new ArgumentNullException(nameof(world));

        public bool IsMember(ArcanumObjectId identity)
            => identity.IsPersistent && _members.Any(member => member.Identity == identity);

        public int IndexOf(ArcanumObjectId identity)
            => _members.FindIndex(member => member.Identity == identity);

        public bool IsPartyAlly(ArcanumObjectId identity)
            => identity.IsPersistent && (identity == Leader || IsMember(identity));

        /// <summary>Records the six retail broadcast orders for every current follower.</summary>
        public bool IssueOrder(PartyOrder order, Vector2Int? location = null, ArcanumObjectId target = default)
        {
            if (!ValidateOrder(order, location, target) || !Leader.IsPersistent) return false;
            PartyFollowerOrderState next = ApplyOrder(CurrentAggregate(), order, location, target);
            ApplyAggregate(next);
            foreach (PartyMember member in _members)
                _orders[member.Identity] = ApplyOrder(GetOrderState(member.Identity), order, location, target);
            OrderChanged?.Invoke(order);
            return true;
        }

        /// <summary>Source follower-menu route: the named portrait receives the order, not the whole party.</summary>
        public bool IssueOrder(ArcanumObjectId follower, PartyOrder order,
            Vector2Int? location = null, ArcanumObjectId target = default)
        {
            if (!IsMember(follower) || !ValidateOrder(order, location, target)) return false;
            PartyFollowerOrderState next = ApplyOrder(GetOrderState(follower), order, location, target);
            _orders[follower] = next;
            ApplyAggregate(next);
            OrderChanged?.Invoke(order);
            return true;
        }

        public PartyFollowerOrderState GetOrderState(ArcanumObjectId follower)
            => _orders.TryGetValue(follower, out PartyFollowerOrderState state)
                ? state : PartyFollowerOrderState.SourceDefault;

        private bool ValidateOrder(PartyOrder order, Vector2Int? location, ArcanumObjectId target)
            => Enum.IsDefined(typeof(PartyOrder), order)
               && (order != PartyOrder.Walk || location.HasValue)
               && (order != PartyOrder.Attack || target.IsPersistent);

        private PartyFollowerOrderState CurrentAggregate()
            => new(LastOrder, FollowingEnabled, DesiredFollowRange, OrderedLocation, OrderedTarget);

        private void ApplyAggregate(PartyFollowerOrderState state)
        {
            LastOrder = state.LastOrder;
            FollowingEnabled = state.FollowingEnabled;
            DesiredFollowRange = state.DesiredFollowRange;
            OrderedLocation = state.OrderedLocation;
            OrderedTarget = state.OrderedTarget;
        }

        private static PartyFollowerOrderState ApplyOrder(PartyFollowerOrderState current,
            PartyOrder order, Vector2Int? location, ArcanumObjectId target)
        {
            bool following = current.FollowingEnabled;
            int range = current.DesiredFollowRange;
            switch (order)
            {
                case PartyOrder.Walk:
                case PartyOrder.Attack:
                    following = true;
                    break;
                case PartyOrder.Follow:
                    following = true;
                    range = PartyFollowerMovementService.SourceFollowRange;
                    break;
                case PartyOrder.StayClose:
                    following = true;
                    range = PartyFollowerMovementService.SourceCloseRange;
                    break;
                case PartyOrder.SpreadOut:
                    following = true;
                    range = PartyFollowerMovementService.SourceSpreadRange;
                    break;
                case PartyOrder.BackOff:
                    // Source only clears this follower's attack intent; it does not make them wait.
                    break;
                case PartyOrder.Wait:
                    following = false;
                    break;
            }
            return new PartyFollowerOrderState(order, following, range,
                order == PartyOrder.Walk ? location : null,
                order == PartyOrder.Attack ? target : default);
        }

        public PartyMutationResult Join(ArcanumObjectId follower, bool forced = false)
        {
            PartyMutationResult preview = PreviewJoin(follower, forced);
            if (!preview.Succeeded) return preview;

            _members.Add(new PartyMember(follower, forced));
            _orders[follower] = PartyFollowerOrderState.SourceDefault;
            MembershipChanged?.Invoke();
            return new PartyMutationResult(PartyMutationFailure.None, follower);
        }

        public PartyMutationResult PreviewJoin(ArcanumObjectId follower, bool forced = false)
        {
            if (!Leader.IsPersistent) return Fail(PartyMutationFailure.NoLeader, follower);
            if (IsMember(follower)) return Fail(PartyMutationFailure.AlreadyFollowing, follower);
            if (!TryValidateFollower(follower, true, out PartyMutationFailure failure))
                return Fail(failure, follower);
            int ordinaryCount = _members.Count(member => !member.Forced);
            return !forced && ordinaryCount >= Capacity
                ? Fail(PartyMutationFailure.CapacityReached, follower)
                : new PartyMutationResult(PartyMutationFailure.None, follower);
        }

        public PartyMutationResult Remove(ArcanumObjectId follower)
        {
            PartyMutationResult preview = PreviewRemove(follower);
            if (!preview.Succeeded) return preview;
            int index = IndexOf(follower);
            _members.RemoveAt(index);
            _orders.Remove(follower);
            MembershipChanged?.Invoke();
            return new PartyMutationResult(PartyMutationFailure.None, follower);
        }

        public PartyMutationResult PreviewRemove(ArcanumObjectId follower)
            => IndexOf(follower) < 0
                ? Fail(PartyMutationFailure.NotFollowing, follower)
                : new PartyMutationResult(PartyMutationFailure.None, follower);

        internal PartyMember[] CaptureMembership() => _members.ToArray();

        internal void RestoreMembership(IEnumerable<PartyMember> members)
        {
            _members.Clear();
            _orders.Clear();
            if (members != null) _members.AddRange(members);
            foreach (PartyMember member in _members)
                _orders[member.Identity] = PartyFollowerOrderState.SourceDefault;
            MembershipChanged?.Invoke();
        }

        internal bool TryAddRestored(PartyMember member, out string error)
        {
            error = null;
            if (IsMember(member.Identity))
            {
                error = $"Duplicate follower {member.Identity}.";
                return false;
            }
            if (!TryValidateFollower(member.Identity, false, out PartyMutationFailure failure))
            {
                error = $"Follower {member.Identity} is invalid: {failure}.";
                return false;
            }
            if (!member.Forced && _members.Count(value => !value.Forced) >= Capacity)
            {
                error = $"Follower {member.Identity} exceeds party capacity {Capacity}.";
                return false;
            }
            _members.Add(member);
            _orders[member.Identity] = PartyFollowerOrderState.SourceDefault;
            return true;
        }

        internal PartyTransitionSnapshot CaptureTransition()
        {
            var placements = new Dictionary<ArcanumObjectId, ObjectPlacement>();
            foreach (PartyMember member in _members)
                if (_world.TryGetObjectState(member.Identity, out PersistentObjectState state))
                    placements.Add(member.Identity, state.Placement);
            return new PartyTransitionSnapshot(placements);
        }

        internal void RelocateEligibleFollowers(string sector, Vector2 tile)
        {
            foreach (PartyMember member in _members)
                if (CanAccompany(member.Identity))
                    _world.RelocateWorldObject(member.Identity, sector, tile);
        }

        internal void RestoreTransition(PartyTransitionSnapshot snapshot)
        {
            if (snapshot == null) return;
            foreach (KeyValuePair<ArcanumObjectId, ObjectPlacement> pair in snapshot.Placements)
                if (pair.Value.Kind == ObjectPlacementKind.World)
                    _world.RelocateWorldObject(pair.Key, pair.Value.Sector, pair.Value.TilePosition);
        }

        public bool CanAccompany(ArcanumObjectId follower)
        {
            if (!IsMember(follower) || !_world.Vitality.TryGet(follower, out _)
                || _world.Vitality.IsDead(follower)
                || _world.TryGetObjectState(follower, out PersistentObjectState state)
                   && (state.NpcFlags & 0x00000008) != 0) return false;
            bool fatigueImmune = _world.Combat.TryGetActorSource(follower, out CombatActorSource source)
                                 && (source.CritterFlags & 0x04000004) != 0;
            return !_world.Vitality.IsUnconscious(follower, fatigueImmune);
        }

        private bool TryValidateFollower(ArcanumObjectId follower, bool requireConscious,
            out PartyMutationFailure failure)
        {
            if (!follower.IsPersistent || follower == Leader
                || !_world.TryGetObjectState(follower, out PersistentObjectState state)
                || state.Type != ObjectType.Npc || state.Off
                || !_world.Characters.TryGet(follower, out _)
                || !_world.Vitality.TryGet(follower, out _))
            {
                failure = PartyMutationFailure.InvalidFollower;
                return false;
            }
            bool fatigueImmune = _world.Combat.TryGetActorSource(follower, out CombatActorSource source)
                                 && (source.CritterFlags & 0x04000004) != 0;
            if (requireConscious && (_world.Vitality.IsDead(follower)
                                     || _world.Vitality.IsUnconscious(follower, fatigueImmune)))
            {
                failure = PartyMutationFailure.FollowerUnavailable;
                return false;
            }
            failure = PartyMutationFailure.None;
            return true;
        }

        private static PartyMutationResult Fail(PartyMutationFailure failure, ArcanumObjectId follower)
            => new(failure, follower);
    }

    internal sealed class PartyTransitionSnapshot
    {
        internal IReadOnlyDictionary<ArcanumObjectId, ObjectPlacement> Placements { get; }
        internal PartyTransitionSnapshot(IReadOnlyDictionary<ArcanumObjectId, ObjectPlacement> placements)
            => Placements = placements;
    }
}
