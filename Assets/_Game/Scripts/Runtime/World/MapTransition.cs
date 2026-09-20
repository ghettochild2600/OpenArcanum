using System;
using System.Collections.Generic;
using Arcanum.Formats.World;
using UnityEngine;

namespace Arcanum.Runtime.World
{
    /// <summary>Stable identity of one passive source jump tile.</summary>
    public readonly struct MapTransitionSourceId : IEquatable<MapTransitionSourceId>
    {
        public int MapId { get; }
        public Vector2Int GlobalTile { get; }

        public MapTransitionSourceId(int mapId, Vector2Int globalTile)
        {
            MapId = mapId;
            GlobalTile = globalTile;
        }

        public bool Equals(MapTransitionSourceId other)
            => MapId == other.MapId && GlobalTile == other.GlobalTile;
        public override bool Equals(object obj) => obj is MapTransitionSourceId other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(MapId, GlobalTile);
        public static bool operator ==(MapTransitionSourceId left, MapTransitionSourceId right) => left.Equals(right);
        public static bool operator !=(MapTransitionSourceId left, MapTransitionSourceId right) => !left.Equals(right);
        public override string ToString() => $"jump:{MapId}:{GlobalTile.x}:{GlobalTile.y}";
    }

    /// <summary>Validated gameplay destination. Presentation derives its sector and local tile from this value.</summary>
    public readonly struct MapTransitionDestination
    {
        public MapTransitionSourceId Source { get; }
        public int MapId { get; }
        public string MapPath { get; }
        public Vector2Int GlobalTile { get; }
        public int? Facing { get; }
        public SectorCoordinate Sector => SectorCoordinate.FromGlobal(MapPath, GlobalTile);
        public Vector2Int LocalTile => Sector.ToLocal(GlobalTile);

        public MapTransitionDestination(MapTransitionSourceId source, int mapId, string mapPath,
            Vector2Int globalTile, int? facing = null)
        {
            Source = source;
            MapId = mapId;
            MapPath = mapPath;
            GlobalTile = globalTile;
            Facing = facing;
        }
    }

    public enum MapTransitionFailure
    {
        None,
        Busy,
        NoProductionPlayer,
        InvalidActor,
        NoSourceData,
        SourceMapMissing,
        SourceMapMismatch,
        SourceTileMismatch,
        JumpTableMissing,
        JumpTableMalformed,
        JumpPointMissing,
        AmbiguousJumpPoint,
        UnsupportedDestinationMap,
        DestinationMapMissing,
        DestinationPropertiesMissing,
        DestinationPropertiesMalformed,
        DestinationTileInvalid,
        DestinationSectorMissing,
        PresentationFailed,
        RollbackFailed,
    }

    public readonly struct MapTransitionResult
    {
        public MapTransitionFailure Failure { get; }
        public MapTransitionDestination Destination { get; }
        public string Detail { get; }
        public bool Succeeded => Failure == MapTransitionFailure.None;

        public MapTransitionResult(MapTransitionFailure failure,
            MapTransitionDestination destination = default, string detail = null)
        {
            Failure = failure;
            Destination = destination;
            Detail = detail;
        }
    }

    /// <summary>Strict read-only resolver for retail MapList and map.jmp sources.</summary>
    public sealed class MapTransitionResolver
    {
        private readonly MapList _maps;
        private readonly Func<string, bool> _exists;
        private readonly Func<string, byte[]> _read;
        private readonly Dictionary<int, IReadOnlyList<JumpPoint>> _jumpCache = new();
        private readonly Dictionary<int, MapTransitionResult> _jumpFailures = new();

        public MapTransitionResolver(MapList maps, Func<string, bool> exists, Func<string, byte[]> read)
        {
            _maps = maps ?? throw new ArgumentNullException(nameof(maps));
            _exists = exists ?? throw new ArgumentNullException(nameof(exists));
            _read = read ?? throw new ArgumentNullException(nameof(read));
        }

        public bool TryGetMapId(string mapPath, out int mapId)
        {
            string name = MapName(mapPath);
            if (_maps.TryGetByName(name, out MapListEntry entry))
            {
                mapId = entry.MapId;
                return true;
            }
            mapId = 0;
            return false;
        }

        public MapTransitionResult Resolve(MapTransitionSourceId source)
        {
            if (!_maps.TryGet(source.MapId, out MapListEntry sourceMap))
                return Failure(MapTransitionFailure.SourceMapMissing, $"MapList id {source.MapId} is missing.");
            if (!TryLoadJumps(source.MapId, sourceMap, out IReadOnlyList<JumpPoint> points,
                    out MapTransitionResult loadFailure))
                return loadFailure;

            JumpPoint? match = null;
            foreach (JumpPoint point in points)
            {
                if (point.SrcX != source.GlobalTile.x || point.SrcY != source.GlobalTile.y) continue;
                if (match.HasValue && (match.Value.DstMap != point.DstMap || match.Value.DstLoc != point.DstLoc))
                    return Failure(MapTransitionFailure.AmbiguousJumpPoint,
                        $"{source} resolves to conflicting retail destinations.");
                match = point;
            }
            if (!match.HasValue)
                return Failure(MapTransitionFailure.JumpPointMissing, $"{source} is absent from map.jmp.");

            JumpPoint jump = match.Value;
            return ResolveDestination(source, jump.DstMap, new Vector2Int(jump.DstX, jump.DstY));
        }

        /// <summary>Shared strict preflight for a source-decoded jump or admitted physical entrance.</summary>
        public MapTransitionResult ResolveDestination(MapTransitionSourceId source, int destinationMapId,
            Vector2Int tile)
        {
            if (destinationMapId <= 0)
                return Failure(MapTransitionFailure.UnsupportedDestinationMap,
                    $"{source} uses non-positive destination map {destinationMapId}; no sentinel is inferred.");
            if (!_maps.TryGet(destinationMapId, out MapListEntry destinationMap))
                return Failure(MapTransitionFailure.DestinationMapMissing,
                    $"Destination MapList id {destinationMapId} is missing.");
            if (tile.x < 0 || tile.y < 0)
                return Failure(MapTransitionFailure.DestinationTileInvalid,
                    $"Destination tile {tile} is negative.");

            string mapPath = "maps/" + destinationMap.Name.ToLowerInvariant();
            string propertiesPath = mapPath + "/map.prp";
            if (!_exists(propertiesPath))
                return Failure(MapTransitionFailure.DestinationPropertiesMissing,
                    $"Destination properties '{propertiesPath}' are missing.");
            MapProperties properties;
            try { properties = MapPropertiesReader.Read(_read(propertiesPath)); }
            catch (Exception ex)
            {
                return Failure(MapTransitionFailure.DestinationPropertiesMalformed,
                    $"Destination properties '{propertiesPath}' are malformed: {ex.Message}");
            }
            if (tile.x >= properties.Width || tile.y >= properties.Height)
                return Failure(MapTransitionFailure.DestinationTileInvalid,
                    $"Destination tile {tile} exceeds {properties.Width}x{properties.Height}.");

            var destination = new MapTransitionDestination(source, destinationMap.MapId, mapPath,
                tile);
            if (!_exists(destination.Sector.Path))
                return Failure(MapTransitionFailure.DestinationSectorMissing,
                    $"Destination sector '{destination.Sector.Path}' is missing.");
            return new MapTransitionResult(MapTransitionFailure.None, destination);
        }

        private bool TryLoadJumps(int mapId, MapListEntry map, out IReadOnlyList<JumpPoint> points,
            out MapTransitionResult failure)
        {
            if (_jumpCache.TryGetValue(mapId, out points))
            {
                failure = default;
                return true;
            }
            if (_jumpFailures.TryGetValue(mapId, out failure)) return false;

            string path = "maps/" + map.Name.ToLowerInvariant() + "/map.jmp";
            if (!_exists(path))
            {
                failure = Failure(MapTransitionFailure.JumpTableMissing, $"Jump table '{path}' is missing.");
                _jumpFailures.Add(mapId, failure);
                points = null;
                return false;
            }
            try
            {
                points = JumpPointReader.Read(_read(path));
                _jumpCache.Add(mapId, points);
                failure = default;
                return true;
            }
            catch (Exception ex)
            {
                failure = Failure(MapTransitionFailure.JumpTableMalformed,
                    $"Jump table '{path}' is malformed: {ex.Message}");
                _jumpFailures.Add(mapId, failure);
                points = null;
                return false;
            }
        }

        private static string MapName(string mapPath)
        {
            string normalized = mapPath?.Replace('\\', '/').Trim().TrimEnd('/');
            int slash = normalized?.LastIndexOf('/') ?? -1;
            return slash >= 0 ? normalized.Substring(slash + 1) : normalized ?? string.Empty;
        }

        private static MapTransitionResult Failure(MapTransitionFailure failure, string detail)
            => new(failure, detail: detail);
    }
}
