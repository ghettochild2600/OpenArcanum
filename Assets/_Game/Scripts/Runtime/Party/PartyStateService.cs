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

        public ArcanumObjectId Leader => _world.PlayerState?.Identity ?? default;
        public IReadOnlyList<PartyMember> Members => _members;
        public int Count => _members.Count;
        public int Capacity => Leader.IsPersistent
            ? _world.DerivedStats.GetDerivedStat(Leader, CharacterDerivedStat.MaximumFollowers)
            : 0;
        public event Action MembershipChanged;

        public PartyStateService(WorldMapSessionCoordinator world)
            => _world = world ?? throw new ArgumentNullException(nameof(world));

        public bool IsMember(ArcanumObjectId identity)
            => identity.IsPersistent && _members.Any(member => member.Identity == identity);

        public int IndexOf(ArcanumObjectId identity)
            => _members.FindIndex(member => member.Identity == identity);

        public bool IsPartyAlly(ArcanumObjectId identity)
            => identity.IsPersistent && (identity == Leader || IsMember(identity));

        public PartyMutationResult Join(ArcanumObjectId follower, bool forced = false)
        {
            PartyMutationResult preview = PreviewJoin(follower, forced);
            if (!preview.Succeeded) return preview;

            _members.Add(new PartyMember(follower, forced));
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
            if (members != null) _members.AddRange(members);
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
                || _world.Vitality.IsDead(follower)) return false;
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
