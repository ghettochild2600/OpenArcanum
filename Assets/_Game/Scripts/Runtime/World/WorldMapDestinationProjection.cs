using System;
using System.Collections.Generic;
using Arcanum.Formats.World;
using Arcanum.Runtime.Campaign;

namespace Arcanum.Runtime.World
{
    public readonly struct WorldMapTile : IEquatable<WorldMapTile>
    {
        public long X { get; }
        public long Y { get; }
        public WorldMapTile(long x, long y) { X = x; Y = y; }
        public bool Equals(WorldMapTile other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is WorldMapTile other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X},{Y})";
        public static bool operator ==(WorldMapTile left, WorldMapTile right) => left.Equals(right);
        public static bool operator !=(WorldMapTile left, WorldMapTile right) => !left.Equals(right);
    }

    /// <summary>Immutable, source-backed destination view. It owns no discovery or travel state.</summary>
    public readonly struct WorldMapDestination
    {
        public AreaId AreaId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public WorldMapTile WorldTile { get; }
        public int LabelXOffset { get; }
        public int LabelYOffset { get; }
        public int DiscoveryRadiusTiles { get; }
        public bool IsKnown { get; }
        public bool IsSelectable { get; }

        internal WorldMapDestination(Area area, bool isKnown)
        {
            AreaId = new AreaId(area.Id);
            DisplayName = area.Name;
            Description = area.Description;
            WorldTile = new WorldMapTile(area.TileX, area.TileY);
            LabelXOffset = area.LabelXOff;
            LabelYOffset = area.LabelYOff;
            DiscoveryRadiusTiles = area.RadiusTiles;
            IsKnown = isKnown;
            IsSelectable = isKnown;
        }
    }

    /// <summary>Typed destination intent consumed by the authoritative world-travel service.</summary>
    public readonly struct WorldMapTravelRequest : IEquatable<WorldMapTravelRequest>
    {
        public AreaId AreaId { get; }
        public WorldMapTile Destination { get; }
        public WorldMapTravelRequest(AreaId areaId, WorldMapTile destination)
        { AreaId = areaId; Destination = destination; }
        public bool Equals(WorldMapTravelRequest other)
            => AreaId == other.AreaId && Destination.Equals(other.Destination);
        public override bool Equals(object obj) => obj is WorldMapTravelRequest other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(AreaId, Destination);
    }

    public enum WorldMapDestinationFailure
    {
        None,
        AreaSourceUnavailable,
        InvalidArea,
        DuplicateAreaIdentity,
        InvalidDestinationRecord,
        Unavailable,
    }

    public readonly struct WorldMapSelectionResult
    {
        public WorldMapDestinationFailure Failure { get; }
        public WorldMapDestination Destination { get; }
        public WorldMapTravelRequest Request { get; }
        public string Detail { get; }
        public bool Succeeded => Failure == WorldMapDestinationFailure.None;

        internal WorldMapSelectionResult(WorldMapDestinationFailure failure,
            WorldMapDestination destination = default, WorldMapTravelRequest request = default,
            string detail = null)
        { Failure = failure; Destination = destination; Request = request; Detail = detail; }
    }

    /// <summary>
    /// Recomputes source-order destination views from immutable AreaList metadata and current campaign discovery.
    /// It never owns known state and selection produces an intent only.
    /// </summary>
    public sealed class WorldMapDestinationProjection
    {
        private static readonly IReadOnlyList<WorldMapDestination> Empty =
            Array.AsReadOnly(Array.Empty<WorldMapDestination>());

        private readonly AreaList _areas;
        private readonly CampaignStateService _campaign;

        public WorldMapDestinationProjection(AreaList areas, CampaignStateService campaign)
        { _areas = areas; _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign)); }

        public bool HasSource => _areas != null;

        /// <summary>All valid nonzero destinations in AreaList source order, including unavailable ones.</summary>
        public bool TryProjectAll(out IReadOnlyList<WorldMapDestination> destinations,
            out WorldMapDestinationFailure failure)
            => TryProject(knownOnly: false, out destinations, out failure);

        /// <summary>Source UI semantics: unknown and invalid records do not receive player-facing markers.</summary>
        public bool TryProjectVisible(out IReadOnlyList<WorldMapDestination> destinations,
            out WorldMapDestinationFailure failure)
            => TryProject(knownOnly: true, out destinations, out failure);

        public bool TryGetDestination(AreaId id, out WorldMapDestination destination,
            out WorldMapDestinationFailure failure)
        {
            destination = default;
            if (_areas == null)
            {
                failure = WorldMapDestinationFailure.AreaSourceUnavailable;
                return false;
            }
            if (id.Value <= 0 || !_areas.TryGet(id, out Area area))
            {
                failure = WorldMapDestinationFailure.InvalidArea;
                return false;
            }
            if (HasDuplicateIdentity(id))
            {
                failure = WorldMapDestinationFailure.DuplicateAreaIdentity;
                return false;
            }
            if (!IsDestination(area))
            {
                failure = WorldMapDestinationFailure.InvalidDestinationRecord;
                return false;
            }
            bool known = _campaign.CanSelectWorldArea(id);
            destination = new WorldMapDestination(area, known);
            failure = WorldMapDestinationFailure.None;
            return true;
        }

        public WorldMapSelectionResult TrySelectWorldArea(AreaId id)
        {
            if (!TryGetDestination(id, out WorldMapDestination destination,
                    out WorldMapDestinationFailure failure))
                return new WorldMapSelectionResult(failure, detail: $"Area {id.Value} is not a valid destination.");
            if (!destination.IsSelectable)
                return new WorldMapSelectionResult(WorldMapDestinationFailure.Unavailable, destination,
                    detail: $"Area {id.Value} is not known.");
            var request = new WorldMapTravelRequest(id, destination.WorldTile);
            return new WorldMapSelectionResult(WorldMapDestinationFailure.None, destination, request);
        }

        private bool TryProject(bool knownOnly, out IReadOnlyList<WorldMapDestination> destinations,
            out WorldMapDestinationFailure failure)
        {
            destinations = Empty;
            if (_areas == null)
            {
                failure = WorldMapDestinationFailure.AreaSourceUnavailable;
                return false;
            }
            var seen = new HashSet<AreaId>();
            var projected = new List<WorldMapDestination>();
            foreach (Area area in _areas.Areas)
            {
                var id = new AreaId(area.Id);
                if (!seen.Add(id))
                {
                    failure = WorldMapDestinationFailure.DuplicateAreaIdentity;
                    return false;
                }
                if (id.Value == 0 || !IsDestination(area)) continue;
                bool known = _campaign.CanSelectWorldArea(id);
                if (!knownOnly || known) projected.Add(new WorldMapDestination(area, known));
            }
            destinations = projected.AsReadOnly();
            failure = WorldMapDestinationFailure.None;
            return true;
        }

        private bool HasDuplicateIdentity(AreaId id)
        {
            int count = 0;
            foreach (Area area in _areas.Areas)
                if (area.Id == id.Value && ++count > 1) return true;
            return false;
        }

        private static bool IsDestination(Area area)
            => area.Id > 0 && area.TileX > 0 && area.TileY > 0
               && !string.IsNullOrWhiteSpace(area.Name);
    }
}
