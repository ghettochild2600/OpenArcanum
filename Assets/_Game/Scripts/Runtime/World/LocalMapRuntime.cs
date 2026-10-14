using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Art;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using Arcanum.Runtime.Party;
using Arcanum.Runtime.Save;
using UnityEngine;

namespace Arcanum.Runtime.World
{
    /// <summary>The serialized 0x30-byte TownMapInfo record consumed by townmap.c.</summary>
    public readonly struct TownMapDefinition
    {
        public const int SourceVersion = 2;
        public const int SerializedSize = 0x30;

        public int Id { get; }
        public string Name { get; }
        public bool Waitable { get; }
        public int MapId { get; }
        public int Width { get; }
        public int Height { get; }
        public int Columns { get; }
        public int Rows { get; }
        public long CenterLocation { get; }
        public float Scale { get; }

        public int TileCount => Columns * Rows;
        public int CenterTileX => unchecked((int)(uint)CenterLocation);
        public int CenterTileY => unchecked((int)(uint)(CenterLocation >> 32));

        public TownMapDefinition(int id, string name, bool waitable, int mapId, int width, int height,
            int columns, int rows, long centerLocation, float scale)
        {
            Id = id;
            Name = name;
            Waitable = waitable;
            MapId = mapId;
            Width = width;
            Height = height;
            Columns = columns;
            Rows = rows;
            CenterLocation = centerLocation;
            Scale = scale;
        }

        public static TownMapDefinition Parse(int id, string name, bool waitable, byte[] bytes)
        {
            if (bytes == null || bytes.Length < SerializedSize)
                throw new ArgumentException("Town-map info is shorter than the source 0x30-byte record.", nameof(bytes));
            int version = BitConverter.ToInt32(bytes, 0);
            if (version != SourceVersion)
                throw new ArgumentException($"Town-map info version {version} is not supported.", nameof(bytes));
            int map = BitConverter.ToInt32(bytes, 8);
            int width = BitConverter.ToInt32(bytes, 12);
            int height = BitConverter.ToInt32(bytes, 16);
            int columns = BitConverter.ToInt32(bytes, 20);
            int rows = BitConverter.ToInt32(bytes, 24);
            long center = BitConverter.ToInt64(bytes, 32);
            float scale = BitConverter.ToSingle(bytes, 40);
            if (string.IsNullOrWhiteSpace(name) || width <= 0 || height <= 0 || columns <= 0 || rows <= 0
                || scale <= 0f || columns > 256 || rows > 256)
                throw new ArgumentException("Town-map info has invalid source dimensions.", nameof(bytes));
            return new TownMapDefinition(id, name, waitable, map, width, height, columns, rows, center, scale);
        }

        /// <summary>townmap_loc_to_coords, with the global isometric origin terms cancelled.</summary>
        public bool TryProject(Vector2 mapTile, out Vector2 pixel)
        {
            Vector2 source = IsometricScreen(mapTile.x, mapTile.y);
            Vector2 center = IsometricScreen(CenterTileX, CenterTileY);
            float x = source.x + Width * .5f - center.x;
            float y = source.y + Height * .5f - center.y;
            if (x < 0f || x >= Width || y < 0f || y >= Height)
            {
                pixel = default;
                return false;
            }
            pixel = new Vector2(Mathf.Floor(x * Scale), Mathf.Floor(y * Scale));
            return true;
        }

        public int TileIndex(Vector2 mapTile)
        {
            Vector2 source = IsometricScreen(mapTile.x, mapTile.y);
            Vector2 center = IsometricScreen(CenterTileX, CenterTileY);
            float x = source.x + Width * .5f - center.x;
            float y = source.y + Height * .5f - center.y;
            if (x < 0f || x >= Width || y < 0f || y >= Height) return -1;
            int column = Mathf.FloorToInt(Columns * x / Width);
            int row = Mathf.FloorToInt(Rows * y / Height);
            return column + Columns * row;
        }

        private static Vector2 IsometricScreen(float x, float y)
            => new(40f * (y - x - 1f), 20f * (y + x));
    }

    /// <summary>Read-only retail town-map catalog and bitmap access.</summary>
    public sealed class LocalMapSource
    {
        private readonly MesFile _townMaps;
        private readonly MapList _maps;
        private readonly MesFile _worldMaps;
        private readonly Func<string, bool> _exists;
        private readonly Func<string, byte[]> _read;

        public LocalMapSource(MesFile townMaps, Func<string, bool> exists, Func<string, byte[]> read)
            : this(townMaps, null, null, exists, read)
        {
        }

        public LocalMapSource(MesFile townMaps, MapList maps, MesFile worldMaps,
            Func<string, bool> exists, Func<string, byte[]> read)
        {
            _townMaps = townMaps ?? throw new ArgumentNullException(nameof(townMaps));
            _maps = maps;
            _worldMaps = worldMaps;
            _exists = exists ?? throw new ArgumentNullException(nameof(exists));
            _read = read ?? throw new ArgumentNullException(nameof(read));
        }

        public bool TryResolve(string sectorPath, out TownMapDefinition definition)
        {
            definition = default;
            string normalized = WorldMapSessionCoordinator.NormalizeSector(sectorPath);
            if (normalized == null || !_exists(normalized)) return false;
            int id;
            try { id = SectorReader.ReadSections(_read(normalized)).TownMapInfo; }
            catch { return false; }
            if (id <= 0 || !_townMaps.TryGet(id, out string authored)) return false;
            ParseName(authored, out string name, out bool waitable);
            string path = $"townmap/{name}/{name}.tmi";
            if (!_exists(path)) return false;
            try
            {
                definition = TownMapDefinition.Parse(id, name, waitable, _read(path));
                return true;
            }
            catch
            {
                definition = default;
                return false;
            }
        }

        public BmpImage ReadTile(TownMapDefinition definition, int index)
        {
            if (index < 0 || index >= definition.TileCount) throw new ArgumentOutOfRangeException(nameof(index));
            string path = TilePath(definition, index);
            if (!_exists(path)) path = TilePath(definition, 0);
            return BmpReader.Read(_read(path));
        }

        public bool TryReadContinent(string sectorPath, out WorldMapInfo info, out BmpImage image)
        {
            info = default;
            image = default;
            string normalized = WorldMapSessionCoordinator.NormalizeSector(sectorPath);
            if (_maps == null || _worldMaps == null || string.IsNullOrEmpty(normalized)) return false;
            string[] parts = normalized.Split('/');
            if (parts.Length < 3 || !parts[0].Equals("maps", StringComparison.OrdinalIgnoreCase)
                || !_maps.TryGetByName(parts[1], out MapListEntry map)
                || map.WorldMap < 0 || !WorldMapInfo.TryParse(_worldMaps, map.WorldMap, out info)
                || string.IsNullOrWhiteSpace(info.ZoomedName) || !_exists(info.ZoomedPath)) return false;
            try
            {
                image = BmpReader.Read(_read(info.ZoomedPath));
                return true;
            }
            catch
            {
                info = default;
                image = default;
                return false;
            }
        }

        public static string TilePath(TownMapDefinition definition, int index)
            => $"townmap/{definition.Name}/{definition.Name}{index:D6}.bmp";

        public static void ParseName(string authored, out string name, out bool waitable)
        {
            string value = authored?.Trim() ?? string.Empty;
            int flag = value.IndexOf("[w:", StringComparison.OrdinalIgnoreCase);
            waitable = false;
            if (flag >= 0)
            {
                int end = value.IndexOf(']', flag);
                string payload = end > flag ? value.Substring(flag + 3, end - flag - 3) : string.Empty;
                waitable = payload.TrimStart().StartsWith("1", StringComparison.Ordinal);
                value = value.Substring(0, flag).TrimEnd();
            }
            name = value;
        }
    }

    public enum LocalMapMarkerKind { Player, Follower, QuestNote }

    public readonly struct LocalMapMarker
    {
        public LocalMapMarkerKind Kind { get; }
        public ArcanumObjectId Identity { get; }
        public Vector2 Pixel { get; }
        public int SourceArtId => Kind == LocalMapMarkerKind.QuestNote ? 143 : 204;

        public LocalMapMarker(LocalMapMarkerKind kind, ArcanumObjectId identity, Vector2 pixel)
        {
            Kind = kind;
            Identity = identity;
            Pixel = pixel;
        }
    }

    public sealed class LocalMapProjection
    {
        public TownMapDefinition Definition { get; }
        public IReadOnlyCollection<int> KnownTiles { get; }
        public IReadOnlyList<LocalMapMarker> Markers { get; }

        public LocalMapProjection(TownMapDefinition definition, IReadOnlyCollection<int> knownTiles,
            IReadOnlyList<LocalMapMarker> markers)
        {
            Definition = definition;
            KnownTiles = knownTiles;
            Markers = markers;
        }

        public bool IsKnown(int index) => Definition.Waitable || KnownTiles.Contains(index);

        /// <summary>Corner mask used by townmap_tile_blit_info for the source fog-edge lerp.</summary>
        public int FogCornerMask(int index)
        {
            if (IsKnown(index)) return 0xF;
            int column = index % Definition.Columns;
            int row = index / Definition.Columns;
            int mask = 0;
            int[,] flags = { { 0x8, 0xC, 0x4 }, { 0x9, 0, 0x6 }, { 0x1, 0x3, 0x2 } };
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int x = column + dx;
                int y = row + dy;
                if (x >= 0 && x < Definition.Columns && y >= 0 && y < Definition.Rows
                    && IsKnown(x + Definition.Columns * y)) mask |= flags[dy + 1, dx + 1];
            }
            return mask;
        }
    }

    /// <summary>Session-owned town-map discovery and live marker projection.</summary>
    public sealed class LocalMapStateService
    {
        private readonly WorldMapSessionCoordinator _world;
        private readonly Dictionary<int, HashSet<int>> _known = new();
        private LocalMapSource _source;

        public LocalMapStateService(WorldMapSessionCoordinator world, LocalMapSource source = null)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _source = source;
        }

        public void BindSource(LocalMapSource source)
            => _source = source ?? throw new ArgumentNullException(nameof(source));

        public bool TryProject(out LocalMapProjection projection)
        {
            projection = null;
            if (_source == null || _world.PlayerState == null
                || !_source.TryResolve(_world.SelectedSector, out TownMapDefinition definition)) return false;
            MarkVisited(definition, _world.PlayerState.MapPosition);
            HashSet<int> known = Known(definition.Id);
            var markers = new List<LocalMapMarker>();
            AddMarker(markers, definition, LocalMapMarkerKind.Player, _world.PlayerState.Identity,
                _world.PlayerState.MapPosition);
            foreach (PartyMember member in _world.Party.Members)
            {
                if (!_world.TryGetObjectState(member.Identity, out PersistentObjectState state)
                    || state.Placement.Kind != ObjectPlacementKind.World
                    || !SectorCoordinate.TryParse(state.Placement.Sector, out SectorCoordinate sector)) continue;
                AddMarker(markers, definition, LocalMapMarkerKind.Follower, member.Identity,
                    sector.ToGlobal(state.Placement.TilePosition));
            }
            projection = new LocalMapProjection(definition, known.OrderBy(value => value).ToArray(), markers);
            return true;
        }

        public BmpImage ReadTile(TownMapDefinition definition, int index)
            => _source.ReadTile(definition, index);

        public bool TryReadContinent(out WorldMapInfo info, out BmpImage image)
        {
            info = default;
            image = default;
            return _source != null && _source.TryReadContinent(_world.SelectedSector, out info, out image);
        }

        public bool MarkCurrentVisited()
        {
            if (_source == null || _world.PlayerState == null
                || !_source.TryResolve(_world.SelectedSector, out TownMapDefinition definition)) return false;
            return MarkVisited(definition, _world.PlayerState.MapPosition);
        }

        public bool MarkVisited(TownMapDefinition definition, Vector2 position)
        {
            int index = definition.TileIndex(position);
            return index >= 0 && Known(definition.Id).Add(index);
        }

        public IReadOnlyCollection<int> KnownTiles(int townMapId)
            => Known(townMapId).OrderBy(value => value).ToArray();

        public LocalMapSaveData ExportSaveData()
            => new()
            {
                Maps = _known.OrderBy(pair => pair.Key).Select(pair => new LocalMapKnownTilesSaveData
                {
                    TownMapId = pair.Key,
                    TileIndices = pair.Value.OrderBy(value => value).ToList(),
                }).ToList(),
            };

        public void RestoreSaveData(LocalMapSaveData data)
        {
            _known.Clear();
            if (data?.Maps == null) return;
            foreach (LocalMapKnownTilesSaveData map in data.Maps)
                _known[map.TownMapId] = new HashSet<int>(map.TileIndices ?? new List<int>());
        }

        public static bool ValidateSaveData(LocalMapSaveData data, out string error)
        {
            error = null;
            if (data == null) return true;
            if (data.Maps == null) { error = "The local-map discovery state is incomplete."; return false; }
            var ids = new HashSet<int>();
            foreach (LocalMapKnownTilesSaveData map in data.Maps)
            {
                if (map == null || map.TownMapId <= 0 || !ids.Add(map.TownMapId) || map.TileIndices == null
                    || map.TileIndices.Any(value => value < 0) || map.TileIndices.Count != map.TileIndices.Distinct().Count())
                {
                    error = "A local-map discovered-tile record is invalid.";
                    return false;
                }
            }
            return true;
        }

        private HashSet<int> Known(int map)
        {
            if (!_known.TryGetValue(map, out HashSet<int> value)) _known.Add(map, value = new HashSet<int>());
            return value;
        }

        private static void AddMarker(ICollection<LocalMapMarker> markers, TownMapDefinition definition,
            LocalMapMarkerKind kind, ArcanumObjectId identity, Vector2 position)
        {
            if (definition.TryProject(position, out Vector2 pixel))
                markers.Add(new LocalMapMarker(kind, identity, pixel));
        }
    }
}
