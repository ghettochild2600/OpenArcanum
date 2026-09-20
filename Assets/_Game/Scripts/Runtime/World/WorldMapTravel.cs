using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Formats.IO;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using UnityEngine;

namespace Arcanum.Runtime.World
{
    public readonly struct WorldMapSector : IEquatable<WorldMapSector>
    {
        public int X { get; }
        public int Y { get; }
        public long Id => (long)X | ((long)Y << 26);

        public WorldMapSector(int x, int y) { X = x; Y = y; }

        public static WorldMapSector FromTile(WorldMapTile tile)
            => new((int)(tile.X / SectorCoordinate.Size), (int)(tile.Y / SectorCoordinate.Size));

        public bool Equals(WorldMapSector other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is WorldMapSector other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public static bool operator ==(WorldMapSector left, WorldMapSector right) => left.Equals(right);
        public static bool operator !=(WorldMapSector left, WorldMapSector right) => !left.Equals(right);
        public override string ToString() => $"sector({X},{Y})";
    }

    /// <summary>Immutable source-backed route geometry. Sector zero is the source; each rotation enters the next.</summary>
    public sealed class WorldMapRoute
    {
        public WorldMapTile Source { get; }
        public AreaId DestinationArea { get; }
        public WorldMapTile Destination { get; }
        public IReadOnlyList<int> Rotations { get; }
        public IReadOnlyList<WorldMapSector> Sectors { get; }
        public int StepCount => Rotations.Count;

        internal WorldMapRoute(WorldMapTile source, AreaId destinationArea, WorldMapTile destination,
            List<int> rotations, List<WorldMapSector> sectors)
        {
            Source = source;
            DestinationArea = destinationArea;
            Destination = destination;
            Rotations = rotations.AsReadOnly();
            Sectors = sectors.AsReadOnly();
        }
    }

    /// <summary>Retail START_MAP terrain and hard-sector overrides used by the source world pathfinder.</summary>
    public sealed class WorldMapRouteTopology
    {
        private readonly ushort[] _terrain;
        private readonly bool[] _blockedTerrainTypes;
        private readonly HashSet<long> _hardBlocked;
        private readonly Func<string, bool> _exists;

        public string MapPath { get; }
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyCollection<long> HardBlockedSectors => _hardBlocked;

        public WorldMapRouteTopology(string mapPath, int width, int height, ushort[] terrain,
            bool[] blockedTerrainTypes, IEnumerable<long> hardBlockedSectors, Func<string, bool> exists)
        {
            if (string.IsNullOrWhiteSpace(mapPath)) throw new ArgumentException("Map path is required.", nameof(mapPath));
            if (width <= 0 || height <= 0 || terrain == null || terrain.Length != width * height)
                throw new ArgumentException("Terrain dimensions do not match its packed sector grid.", nameof(terrain));
            if (blockedTerrainTypes == null || blockedTerrainTypes.Length != 32)
                throw new ArgumentException("The source terrain type table must contain 32 flags.", nameof(blockedTerrainTypes));
            MapPath = mapPath.Replace('\\', '/').Trim().TrimEnd('/').ToLowerInvariant();
            Width = width;
            Height = height;
            _terrain = (ushort[])terrain.Clone();
            _blockedTerrainTypes = (bool[])blockedTerrainTypes.Clone();
            _hardBlocked = new HashSet<long>(hardBlockedSectors ?? Array.Empty<long>());
            _exists = exists ?? (_ => false);
        }

        public bool IsBlocked(WorldMapSector sector)
        {
            if (sector.X < 0 || sector.X >= Width || sector.Y < 0 || sector.Y >= Height) return true;
            if (_hardBlocked.Contains(sector.Id)) return true;
            if (_exists($"{MapPath}/{sector.Id}.sec")) return false;
            ushort value = _terrain[sector.X + sector.Y * Width];
            int primary = value >> 11 & 0x1F;
            int secondary = value >> 6 & 0x1F;
            return _blockedTerrainTypes[primary] || _blockedTerrainTypes[secondary];
        }
    }

    /// <summary>Port of the source straight-sector routing plus its bounded 16x16 detour search.</summary>
    public sealed class WorldMapRoutePlanner
    {
        public const int SourceMaximumSteps = 5000;
        private static readonly (int x, int y)[] DirectionDeltas =
        {
            (-1, -1), (-1, 0), (-1, 1), (0, 1),
            (1, 1), (1, 0), (1, -1), (0, -1),
        };

        public bool TryPlan(WorldMapRouteTopology topology, WorldMapTile source,
            AreaId destinationArea, WorldMapTile destination, out WorldMapRoute route)
        {
            route = null;
            if (topology == null || source.X < 0 || source.Y < 0 || destination.X < 0 || destination.Y < 0
                || source.X > int.MaxValue || source.Y > int.MaxValue
                || destination.X > int.MaxValue || destination.Y > int.MaxValue)
                return false;

            WorldMapSector current = WorldMapSector.FromTile(source);
            WorldMapSector target = WorldMapSector.FromTile(destination);
            if (current == target || topology.IsBlocked(current) || topology.IsBlocked(target)) return false;

            var rotations = new List<int>();
            var sectors = new List<WorldMapSector> { current };
            while (current != target && rotations.Count < SourceMaximumSteps)
            {
                int rotation = Rotation(current, target);
                if (!TryMove(current, rotation, topology, out WorldMapSector next)) return false;
                if (!topology.IsBlocked(next))
                {
                    rotations.Add(rotation);
                    sectors.Add(next);
                    current = next;
                    continue;
                }

                if (!TryFindDetour(topology, current, target,
                        SourceMaximumSteps - rotations.Count, out List<int> detour)) return false;
                foreach (int detourRotation in detour)
                {
                    if (!TryMove(current, detourRotation, topology, out next) || topology.IsBlocked(next)) return false;
                    rotations.Add(detourRotation);
                    sectors.Add(next);
                    current = next;
                }
            }

            if (current != target || rotations.Count == 0) return false;
            route = new WorldMapRoute(source, destinationArea, destination, rotations, sectors);
            return true;
        }

        private static bool TryFindDetour(WorldMapRouteTopology topology, WorldMapSector source,
            WorldMapSector finalTarget, int maximumSteps, out List<int> rotations)
        {
            rotations = null;
            WorldMapSector scan = source;
            while (scan != finalTarget)
            {
                int direction = Rotation(scan, finalTarget);
                if (!TryMove(scan, direction, topology, out WorldMapSector adjacent)) return false;
                if (!topology.IsBlocked(adjacent))
                    return TryLocalPath(topology, source, adjacent, maximumSteps, out rotations);
                scan = adjacent;
            }
            return false;
        }

        private static bool TryLocalPath(WorldMapRouteTopology topology, WorldMapSector source,
            WorldMapSector target, int maximumSteps, out List<int> rotations)
        {
            rotations = null;
            int dx = Math.Abs(source.X - target.X);
            int dy = Math.Abs(source.Y - target.Y);
            if (dx >= 8 || dy >= 8) return false;

            int originX = Math.Min(source.X, target.X) - (16 - dx) / 2;
            int originY = Math.Min(source.Y, target.Y) - (16 - dy) / 2;
            int startIndex = source.X - originX + (source.Y - originY) * 16;
            int targetIndex = target.X - originX + (target.Y - originY) * 16;
            if (startIndex < 0 || startIndex >= 256 || targetIndex < 0 || targetIndex >= 256) return false;

            var cost = new int[256];
            var previous = Enumerable.Repeat(-2, 256).ToArray();
            cost[startIndex] = 1;
            previous[startIndex] = -1;

            while (true)
            {
                int current = -1;
                int best = int.MaxValue;
                for (int i = 0; i < 256; i++)
                {
                    if (cost[i] <= 0) continue;
                    int estimate = cost[i] + Distance(i, targetIndex);
                    if (estimate / 10 <= maximumSteps && estimate < best)
                    {
                        best = estimate;
                        current = i;
                    }
                    else if (estimate / 10 > maximumSteps) cost[i] = -32768;
                }
                if (current < 0) return false;
                if (current == targetIndex) break;

                var currentSector = new WorldMapSector(originX + current % 16, originY + current / 16);
                for (int direction = 0; direction < 8; direction++)
                {
                    if (!TryMove(currentSector, direction, topology, out WorldMapSector neighbor)) continue;
                    int nx = neighbor.X - originX;
                    int ny = neighbor.Y - originY;
                    if (nx < 0 || nx >= 16 || ny < 0 || ny >= 16) continue;
                    int neighborIndex = nx + ny * 16;
                    if (cost[neighborIndex] == -32768 || topology.IsBlocked(neighbor))
                    {
                        cost[neighborIndex] = -32768;
                        continue;
                    }

                    int candidate = cost[current] + 10;
                    if (previous[current] != -1
                        && DirectionBetween(previous[current], current) != direction) candidate++;
                    int existing = cost[neighborIndex];
                    if (existing == 0 || existing > 0 && existing > candidate
                        || existing < 0 && existing != -32768 && -existing > candidate)
                    {
                        cost[neighborIndex] = candidate;
                        previous[neighborIndex] = current;
                    }
                }
                cost[current] = -cost[current];
            }

            var reversed = new List<int>();
            for (int current = targetIndex; previous[current] != -1; current = previous[current])
            {
                if (previous[current] < 0) return false;
                reversed.Add(DirectionBetween(previous[current], current));
            }
            reversed.Reverse();
            if (reversed.Count == 0 || reversed.Count > maximumSteps) return false;
            rotations = reversed;
            return true;
        }

        private static int Distance(int source, int target)
            => 10 * Math.Max(Math.Abs(source % 16 - target % 16), Math.Abs(source / 16 - target / 16));

        private static int DirectionBetween(int source, int target)
        {
            int dx = target % 16 - source % 16;
            int dy = target / 16 - source / 16;
            for (int i = 0; i < DirectionDeltas.Length; i++)
                if (DirectionDeltas[i].x == dx && DirectionDeltas[i].y == dy) return i;
            return -1;
        }

        private static int Rotation(WorldMapSector source, WorldMapSector target)
        {
            int dx = target.X - source.X;
            int dy = target.Y - source.Y;
            int sx = Math.Sign(dx);
            int sy = Math.Sign(dy);
            int ddx = 2 * Math.Abs(dx);
            int ddy = 2 * Math.Abs(dy);
            if (ddx > ddy)
            {
                if (ddy - ddx / 2 < 0) sy = 0;
            }
            else if (ddx - ddy / 2 < 0) sx = 0;

            for (int i = 0; i < DirectionDeltas.Length; i++)
                if (DirectionDeltas[i].x == sx && DirectionDeltas[i].y == sy) return i;
            throw new InvalidOperationException("A nonzero sector delta did not resolve to a source rotation.");
        }

        private static bool TryMove(WorldMapSector source, int rotation, WorldMapRouteTopology topology,
            out WorldMapSector destination)
        {
            destination = default;
            if (rotation < 0 || rotation >= DirectionDeltas.Length) return false;
            int x = source.X + DirectionDeltas[rotation].x;
            int y = source.Y + DirectionDeltas[rotation].y;
            if (x < 0 || x >= topology.Width || y < 0 || y >= topology.Height) return false;
            destination = new WorldMapSector(x, y);
            return true;
        }
    }

    public enum WorldMapTravelSourceFailure
    {
        None, MissingStartMap, AmbiguousStartMap, MissingTerrain, MalformedTerrain,
        MissingTerrainTypes, MalformedTerrainTypes, MalformedSectorBlocks,
    }

    /// <summary>Immutable retail source bundle shared by route planning and arrival preflight.</summary>
    public sealed class WorldMapTravelSource
    {
        private readonly Func<string, bool> _exists;
        private readonly Func<string, byte[]> _read;
        private WorldMapRouteTopology _topology;
        private WorldMapTravelSourceFailure _failure;
        private string _detail;
        private bool _resolved;

        public MapList Maps { get; }
        public MapTransitionResolver Transitions { get; }

        public WorldMapTravelSource(MapList maps, MapTransitionResolver transitions,
            Func<string, bool> exists, Func<string, byte[]> read)
        {
            Maps = maps ?? throw new ArgumentNullException(nameof(maps));
            Transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
            _exists = exists ?? throw new ArgumentNullException(nameof(exists));
            _read = read ?? throw new ArgumentNullException(nameof(read));
        }

        public bool TryResolve(out MapListEntry startMap, out WorldMapRouteTopology topology,
            out WorldMapTravelSourceFailure failure, out string detail)
        {
            MapListEntry[] starts = Maps.Entries.Where(value => value.Type == MapType.StartMap).ToArray();
            startMap = starts.Length == 1 ? starts[0] : default;
            if (starts.Length == 0)
            {
                topology = null; failure = WorldMapTravelSourceFailure.MissingStartMap;
                detail = "MapList has no START_MAP."; return false;
            }
            if (starts.Length != 1)
            {
                topology = null; failure = WorldMapTravelSourceFailure.AmbiguousStartMap;
                detail = "MapList has more than one START_MAP."; return false;
            }
            if (!_resolved) Resolve(startMap);
            topology = _topology; failure = _failure; detail = _detail;
            return failure == WorldMapTravelSourceFailure.None;
        }

        private void Resolve(MapListEntry startMap)
        {
            _resolved = true;
            string mapPath = "maps/" + startMap.Name.ToLowerInvariant();
            string terrainPath = mapPath + "/terrain.tdf";
            if (!_exists(terrainPath))
            { Fail(WorldMapTravelSourceFailure.MissingTerrain, $"Retail terrain '{terrainPath}' is missing."); return; }
            if (!_exists("terrain/terrain.mes"))
            { Fail(WorldMapTravelSourceFailure.MissingTerrainTypes, "Retail terrain/terrain.mes is missing."); return; }
            try
            {
                ReadTerrain(_read(terrainPath), out int width, out int height, out ushort[] terrain);
                bool[] blockedTypes = ReadBlockedTypes(MesReader.Read(_read("terrain/terrain.mes")));
                HashSet<long> hardBlocked = _exists(mapPath + "/map.sbf")
                    ? ReadHardBlocks(_read(mapPath + "/map.sbf")) : new HashSet<long>();
                _topology = new WorldMapRouteTopology(mapPath, width, height, terrain,
                    blockedTypes, hardBlocked, _exists);
                _failure = WorldMapTravelSourceFailure.None;
            }
            catch (InvalidDataException ex)
            {
                Fail(ex.Message.StartsWith("Terrain type", StringComparison.Ordinal)
                    ? WorldMapTravelSourceFailure.MalformedTerrainTypes
                    : ex.Message.StartsWith("Sector block", StringComparison.Ordinal)
                        ? WorldMapTravelSourceFailure.MalformedSectorBlocks
                        : WorldMapTravelSourceFailure.MalformedTerrain, ex.Message);
            }
            catch (Exception ex) { Fail(WorldMapTravelSourceFailure.MalformedTerrain, ex.Message); }
        }

        private void Fail(WorldMapTravelSourceFailure failure, string detail)
        { _failure = failure; _detail = detail; _topology = null; }

        private static void ReadTerrain(byte[] bytes, out int width, out int height, out ushort[] terrain)
        {
            if (bytes == null || bytes.Length < 32) throw new InvalidDataException("Terrain header is truncated.");
            using var stream = new MemoryStream(bytes, false);
            using var reader = new BinaryReader(stream);
            float version = reader.ReadSingle();
            uint flags = reader.ReadUInt32();
            long sourceWidth = reader.ReadInt64();
            long sourceHeight = reader.ReadInt64();
            reader.ReadInt32();
            reader.ReadInt32();
            if (Math.Abs(version - 1.2f) > 0.0001f || sourceWidth <= 0 || sourceHeight <= 0
                || sourceWidth > 10000 || sourceHeight > 10000 || sourceWidth * sourceHeight > 25_000_000)
                throw new InvalidDataException("Terrain header has an unsupported version or dimensions.");
            width = (int)sourceWidth;
            height = (int)sourceHeight;
            terrain = new ushort[width * height];
            if ((flags & 1) == 0)
            {
                if (stream.Length - stream.Position != terrain.Length * 2L)
                    throw new InvalidDataException("Terrain grid length does not match its header.");
                for (int i = 0; i < terrain.Length; i++) terrain[i] = reader.ReadUInt16();
                return;
            }

            for (int y = 0; y < height; y++)
            {
                if (stream.Length - stream.Position < 4) throw new InvalidDataException("Terrain row header is truncated.");
                int compressedSize = reader.ReadInt32();
                if (compressedSize <= 0 || compressedSize > stream.Length - stream.Position)
                    throw new InvalidDataException("Terrain row has an invalid compressed length.");
                byte[] compressed = reader.ReadBytes(compressedSize);
                byte[] row = ZlibInflate.Inflate(compressed, 0, compressed.Length, width * 2);
                if (row.Length != width * 2) throw new InvalidDataException("Terrain row inflates to the wrong length.");
                for (int x = 0; x < width; x++) terrain[x + y * width] = BitConverter.ToUInt16(row, x * 2);
            }
        }

        private static bool[] ReadBlockedTypes(MesFile source)
        {
            var blocked = new bool[32];
            int count = 0;
            for (int i = 0; i < blocked.Length; i++)
            {
                if (!source.TryGet(i, out string value)) break;
                blocked[i] = value.IndexOf("/b", StringComparison.OrdinalIgnoreCase) >= 0;
                count++;
            }
            if (count == 0) throw new InvalidDataException("Terrain type table has no contiguous source entries.");
            return blocked;
        }

        private static HashSet<long> ReadHardBlocks(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 4) throw new InvalidDataException("Sector block table is truncated.");
            using var stream = new MemoryStream(bytes, false);
            using var reader = new BinaryReader(stream);
            int count = reader.ReadInt32();
            if (count < 0 || count > 1_000_000 || stream.Length - stream.Position != count * 8L)
                throw new InvalidDataException("Sector block table has an invalid count or length.");
            var result = new HashSet<long>();
            for (int i = 0; i < count; i++) result.Add(reader.ReadInt64());
            return result;
        }
    }

    public enum WorldMapTravelPhase { Idle, Planning, Travelling, Arriving, Completed, Failed }

    public readonly struct WorldMapTravelState
    {
        public WorldMapTravelPhase Phase { get; }
        public ArcanumObjectId Actor { get; }
        public WorldMapTravelRequest Request { get; }
        public WorldMapRoute Route { get; }
        public WorldMapTravelState(WorldMapTravelPhase phase, ArcanumObjectId actor = default,
            WorldMapTravelRequest request = default, WorldMapRoute route = null)
        { Phase = phase; Actor = actor; Request = request; Route = route; }
    }

    public enum WorldMapTravelFailure
    {
        None, Busy, NoSourceData, NoProductionPlayer, InvalidActor, InvalidContext,
        InvalidRequest, Unavailable, RouteUnavailable, ArrivalUnavailable,
        PresentationFailed, RollbackFailed,
    }

    public readonly struct WorldMapTravelResult
    {
        public WorldMapTravelFailure Failure { get; }
        public WorldMapRoute Route { get; }
        public MapTransitionResult Transition { get; }
        public string Detail { get; }
        public bool Succeeded => Failure == WorldMapTravelFailure.None;
        public WorldMapTravelResult(WorldMapTravelFailure failure, WorldMapRoute route = null,
            MapTransitionResult transition = default, string detail = null)
        { Failure = failure; Route = route; Transition = transition; Detail = detail ?? transition.Detail; }
    }

    /// <summary>Synchronous M7E authority. Presentation can submit requests but never owns route or phase.</summary>
    public sealed class WorldMapTravelService
    {
        private readonly WorldMapSessionCoordinator _session;
        private readonly WorldMapTravelSource _source;
        private readonly WorldMapRoutePlanner _planner = new();

        public WorldMapTravelState State { get; private set; } = new(WorldMapTravelPhase.Idle);
        public WorldMapTravelResult LastResult { get; private set; }
        public event Action<WorldMapTravelState> PhaseChanged;

        internal WorldMapTravelService(WorldMapSessionCoordinator session, WorldMapTravelSource source)
        { _session = session ?? throw new ArgumentNullException(nameof(session)); _source = source; }

        public WorldMapTravelResult Execute(ArcanumObjectId actor, WorldMapTravelRequest request)
        {
            if (State.Phase != WorldMapTravelPhase.Idle)
                return Remember(new WorldMapTravelResult(WorldMapTravelFailure.Busy,
                    detail: "World-map travel is already active."));
            if (_source == null)
                return Remember(new WorldMapTravelResult(WorldMapTravelFailure.NoSourceData,
                    detail: "World-map travel source data has not been bound."));
            if (!_source.TryResolve(out MapListEntry startMap,
                    out WorldMapRouteTopology topology, out _, out string sourceDetail))
                return Remember(new WorldMapTravelResult(WorldMapTravelFailure.NoSourceData, detail: sourceDetail));
            if (_session.PlayerState == null || !_session.HasSelectedSector)
                return Remember(new WorldMapTravelResult(WorldMapTravelFailure.NoProductionPlayer));
            if (!actor.IsPersistent || _session.PlayerState.Identity != actor)
                return Remember(new WorldMapTravelResult(WorldMapTravelFailure.InvalidActor));

            WorldMapSelectionResult selection = _session.WorldMapDestinations.TrySelectWorldArea(request.AreaId);
            if (!selection.Succeeded)
                return Remember(new WorldMapTravelResult(
                    selection.Failure == WorldMapDestinationFailure.Unavailable
                        ? WorldMapTravelFailure.Unavailable : WorldMapTravelFailure.InvalidRequest,
                    detail: selection.Detail));
            if (!selection.Request.Equals(request))
                return Remember(new WorldMapTravelResult(WorldMapTravelFailure.InvalidRequest,
                    detail: "The request does not match the current source-backed destination."));

            if (!SectorCoordinate.TryParse(_session.SelectedSector, out SectorCoordinate selected)
                || !_source.Maps.TryGetByName(selected.MapPath.Split('/').Last(), out MapListEntry currentMap)
                || currentMap.MapId != startMap.MapId || currentMap.Type != MapType.StartMap)
                return Remember(new WorldMapTravelResult(WorldMapTravelFailure.InvalidContext,
                    detail: "Bounded world travel requires the production PC on the retail START_MAP."));
            Vector2 position = _session.PlayerState.MapPosition;
            Vector2Int sourceTile = Vector2Int.RoundToInt(position);
            if (Vector2.SqrMagnitude(position - sourceTile) > 0.0001f)
                return Remember(new WorldMapTravelResult(WorldMapTravelFailure.InvalidContext,
                    detail: "The production PC has not landed on an exact START_MAP tile."));

            var sourcePosition = new WorldMapTile(sourceTile.x, sourceTile.y);
            if (!_planner.TryPlan(topology, sourcePosition, request.AreaId, request.Destination,
                    out WorldMapRoute route))
                return Remember(new WorldMapTravelResult(WorldMapTravelFailure.RouteUnavailable,
                    detail: "The source sector pathfinder could not reach this destination."));
            if (request.Destination.X > int.MaxValue || request.Destination.Y > int.MaxValue)
                return Remember(new WorldMapTravelResult(WorldMapTravelFailure.ArrivalUnavailable, route,
                    detail: "The destination is outside the supported START_MAP tile range."));

            var arrivalTile = new Vector2Int((int)request.Destination.X, (int)request.Destination.Y);
            MapTransitionResult arrival = _source.Transitions.ResolveDestination(
                new MapTransitionSourceId(startMap.MapId, sourceTile), startMap.MapId, arrivalTile);
            if (!arrival.Succeeded)
                return Remember(new WorldMapTravelResult(WorldMapTravelFailure.ArrivalUnavailable, route, arrival));

            SetPhase(WorldMapTravelPhase.Planning, actor, request, route);
            SetPhase(WorldMapTravelPhase.Travelling, actor, request, route);
            SetPhase(WorldMapTravelPhase.Arriving, actor, request, route);
            MapTransitionResult transition = _session.ApplyResolvedWorldMapTravel(arrival.Destination);
            if (!transition.Succeeded)
            {
                SetPhase(WorldMapTravelPhase.Failed, actor, request, route);
                SetPhase(WorldMapTravelPhase.Idle);
                return Remember(new WorldMapTravelResult(
                    transition.Failure == MapTransitionFailure.RollbackFailed
                        ? WorldMapTravelFailure.RollbackFailed : WorldMapTravelFailure.PresentationFailed,
                    route, transition));
            }

            var result = new WorldMapTravelResult(WorldMapTravelFailure.None, route, transition);
            LastResult = result;
            SetPhase(WorldMapTravelPhase.Completed, actor, request, route);
            SetPhase(WorldMapTravelPhase.Idle);
            return result;
        }

        internal void Normalize() => SetPhase(WorldMapTravelPhase.Idle);

        private void SetPhase(WorldMapTravelPhase phase, ArcanumObjectId actor = default,
            WorldMapTravelRequest request = default, WorldMapRoute route = null)
        {
            State = new WorldMapTravelState(phase, actor, request, route);
            PhaseChanged?.Invoke(State);
        }

        private WorldMapTravelResult Remember(WorldMapTravelResult result)
        { LastResult = result; return result; }
    }
}
