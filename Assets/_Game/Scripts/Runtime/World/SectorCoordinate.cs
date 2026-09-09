using System;
using UnityEngine;

namespace Arcanum.Runtime.World
{
    /// <summary>Source-compatible 64x64 sector identity and map-global/local tile conversion.</summary>
    public readonly struct SectorCoordinate : IEquatable<SectorCoordinate>
    {
        public const int Size = 64;
        private const long CoordinateMask = 0x3FFFFFF;

        public string MapPath { get; }
        public int X { get; }
        public int Y { get; }
        public long Id => (long)X | ((long)Y << 26);
        public string Path => $"{MapPath}/{Id}.sec";
        public Vector2 Origin => new(X * Size, Y * Size);

        public SectorCoordinate(string mapPath, int x, int y)
        {
            if (string.IsNullOrWhiteSpace(mapPath)) throw new ArgumentException("Map path is required.", nameof(mapPath));
            if (x < 0 || y < 0 || x > CoordinateMask || y > CoordinateMask)
                throw new ArgumentOutOfRangeException(nameof(x), "Sector coordinates must fit the source 26-bit fields.");
            MapPath = NormalizeMapPath(mapPath);
            X = x;
            Y = y;
        }

        public static bool TryParse(string sectorPath, out SectorCoordinate coordinate)
        {
            coordinate = default;
            string normalized = WorldMapSessionCoordinator.NormalizeSector(sectorPath);
            int slash = normalized?.LastIndexOf('/') ?? -1;
            if (slash <= 0 || !long.TryParse(System.IO.Path.GetFileNameWithoutExtension(normalized), out long id) || id < 0)
                return false;
            long x = id & CoordinateMask;
            long y = (id >> 26) & CoordinateMask;
            if ((x | (y << 26)) != id) return false;
            coordinate = new SectorCoordinate(normalized.Substring(0, slash), (int)x, (int)y);
            return true;
        }

        public static SectorCoordinate FromGlobal(string mapPath, Vector2 mapPosition)
            => new(mapPath, Mathf.FloorToInt(mapPosition.x / Size), Mathf.FloorToInt(mapPosition.y / Size));

        public Vector2 ToGlobal(Vector2 localPosition) => Origin + localPosition;
        public Vector2 ToLocal(Vector2 mapPosition) => mapPosition - Origin;
        public Vector2Int ToLocal(Vector2Int mapTile) => mapTile - new Vector2Int(X * Size, Y * Size);
        public SectorCoordinate Neighbor(int dx, int dy) => new(MapPath, X + dx, Y + dy);

        public bool Equals(SectorCoordinate other)
            => X == other.X && Y == other.Y && string.Equals(MapPath, other.MapPath, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SectorCoordinate other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(MapPath, X, Y);
        public static bool operator ==(SectorCoordinate left, SectorCoordinate right) => left.Equals(right);
        public static bool operator !=(SectorCoordinate left, SectorCoordinate right) => !left.Equals(right);
        public override string ToString() => Path;

        private static string NormalizeMapPath(string path)
            => path.Replace('\\', '/').Trim().TrimEnd('/').ToLowerInvariant();
    }
}
