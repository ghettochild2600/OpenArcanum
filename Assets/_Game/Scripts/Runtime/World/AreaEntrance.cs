using System;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Script;
using Arcanum.Formats.World;
using UnityEngine;

namespace Arcanum.Runtime.World
{
    // These are gameplay tiles on two different maps, never world-map UI/image coordinates.
    public enum TravelLocationSpace { OverlandTile, LocalMapTile }

    public readonly struct TravelLocation
    {
        public int MapId { get; }
        public Vector2Int Tile { get; }
        public TravelLocationSpace Space { get; }
        public TravelLocation(int mapId, Vector2Int tile, TravelLocationSpace space)
        { MapId = mapId; Tile = tile; Space = space; }
    }

    public enum AreaEntranceFailure
    {
        None, NoSourceData, UnknownEntrance, UnsupportedEntranceType, SourceMapMismatch,
        UnknownArea, MissingEntry, MalformedEntry, InvalidDestinationMap, DestinationPreflightFailed,
        Busy, InvalidActor, TargetUnavailable, OutOfRange, PresentationFailed, RollbackFailed,
    }

    public readonly struct AreaEntranceResult
    {
        public AreaEntranceFailure Failure { get; }
        public ArcanumObjectId Entrance { get; }
        public int AreaId { get; }
        public TravelLocation Source { get; }
        public TravelLocation Destination { get; }
        public MapTransitionResult Transition { get; }
        public string Detail { get; }
        public bool Succeeded => Failure == AreaEntranceFailure.None;

        public AreaEntranceResult(AreaEntranceFailure failure, ArcanumObjectId entrance = default,
            int areaId = 0, TravelLocation source = default, TravelLocation destination = default,
            MapTransitionResult transition = default, string detail = null)
        {
            Failure = failure; Entrance = entrance; AreaId = areaId; Source = source;
            Destination = destination; Transition = transition; Detail = detail ?? transition.Detail;
        }

        internal AreaEntranceResult WithTransition(MapTransitionResult result)
            => new(result.Succeeded ? AreaEntranceFailure.None
                    : result.Failure == MapTransitionFailure.RollbackFailed ? AreaEntranceFailure.RollbackFailed
                    : AreaEntranceFailure.PresentationFailed,
                Entrance, AreaId, Source, Destination, result);
    }

    /// <summary>
    /// M7B admits one audited physical entrance, not arbitrary teleports or area-to-map guesses.
    /// Discovery gates apply to world-map destination selection, not this placed SAP_USE object.
    /// </summary>
    public sealed class AreaEntranceResolver
    {
        public const int BatesScriptNumber = 1267;
        public const string BatesSourceSector = "maps/arcanum1-024-fixed/68853695432.sec";
        public static readonly Vector2Int BatesSourceTile = new(61974, 65664);
        // Retail NULL record, zero-based index 0, normalized by the existing positional ObjectID contract.
        public static readonly ArcanumObjectId BatesEntranceIdentity = ArcanumObjectId.CreatePositional(
            ((long)65664 << 32) | 61974L, 0, 1);

        private readonly MapList _maps;
        private readonly AreaList _areas;
        private readonly MapTransitionResolver _transitions;
        private readonly Func<int, ScriptFile> _script;

        public AreaEntranceResolver(MapList maps, AreaList areas, MapTransitionResolver transitions,
            Func<int, ScriptFile> resolveScript)
        {
            _maps = maps ?? throw new ArgumentNullException(nameof(maps));
            _areas = areas ?? throw new ArgumentNullException(nameof(areas));
            _transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
            _script = resolveScript ?? throw new ArgumentNullException(nameof(resolveScript));
        }

        public static bool IsAdmittedTarget(PersistentObjectState state)
            => state != null && state.Identity == BatesEntranceIdentity
               && state.Type == ObjectType.Scenery && state.PrototypeNumber == 4036
               && state.UseScriptNum == BatesScriptNumber;

        public AreaEntranceResult Resolve(PersistentObjectState state)
        {
            if (state == null || state.Identity != BatesEntranceIdentity)
                return Fail(AreaEntranceFailure.UnknownEntrance, "No audited physical entrance has that identity.");
            if (!IsAdmittedTarget(state))
                return Fail(AreaEntranceFailure.UnsupportedEntranceType, "Entrance type/prototype/SAP_USE is not admitted.");
            if (state.Placement.Kind != ObjectPlacementKind.World || state.Placement.Sector != BatesSourceSector
                || !SectorCoordinate.TryParse(state.Placement.Sector, out SectorCoordinate sourceSector)
                || sourceSector.ToGlobal(state.Placement.TilePosition) != (Vector2)BatesSourceTile
                || !_maps.TryGet(1, out MapListEntry overland) || overland.Type != MapType.StartMap
                || !_transitions.TryGetMapId(sourceSector.MapPath, out int sourceId) || sourceId != overland.MapId)
                return Fail(AreaEntranceFailure.SourceMapMismatch, "Entrance is not at its authored START_MAP location.");

            ScriptFile file = _script(state.UseScriptNum);
            if (file == null) return Fail(AreaEntranceFailure.MissingEntry, "The source entrance script is missing.");
            // Audit: exactly TRUE/TELEPORT Triggerer with constant map-key/X/Y, then TRUE/skip-default.
            // Ignore unused operand bytes, which contain retail editor garbage; never evaluate them.
            if (file.HeaderFlags != 0 || file.Entries.Count != 2
                || file.Entries[0] == null || file.Entries[1] == null
                || file.Entries[0].Type != (int)Sct.True || file.Entries[1].Type != (int)Sct.True
                || file.Entries[0].Action?.Type != (int)Sat.Teleport
                || file.Entries[1].Action?.Type != (int)Sat.ReturnAndSkipDefault)
                return Fail(AreaEntranceFailure.UnsupportedEntranceType, "Entrance script control flow is outside M7B.");
            ScriptAction action = file.Entries[0].Action;
            if (action.OpType[0] != (byte)Sfo.Triggerer
                || action.OpType[1] != (byte)Svt.Number || action.OpType[2] != (byte)Svt.Number
                || action.OpType[3] != (byte)Svt.Number)
                return Fail(AreaEntranceFailure.MalformedEntry, "Entrance destination must be constant and PC-triggered.");
            long mapId = (long)action.OpValue[1] - 4999; // SAT_TELEPORT stores the MES key, not a runtime id.
            if (mapId != 12 || !_maps.TryGet((int)mapId, out MapListEntry local)
                || !_maps.TryGetByName("Bates Mansion Lev 1", out MapListEntry bates) || bates.MapId != local.MapId
                || local.Type != MapType.None || local.WorldMap != overland.WorldMap)
                return Fail(AreaEntranceFailure.InvalidDestinationMap, "Destination is not the audited Bates local map.");
            if (local.Area != 21 || !_areas.TryGet(local.Area, out _))
                return Fail(AreaEntranceFailure.UnknownArea, "Bates must resolve to the source Tarant area (21).");

            Vector2Int tile = new(action.OpValue[2], action.OpValue[3]);
            MapTransitionResult destination = _transitions.ResolveDestination(
                new MapTransitionSourceId(sourceId, BatesSourceTile), local.MapId, tile);
            if (!destination.Succeeded)
                return new AreaEntranceResult(AreaEntranceFailure.DestinationPreflightFailed,
                    state.Identity, local.Area, transition: destination);
            return new AreaEntranceResult(AreaEntranceFailure.None, state.Identity, local.Area,
                new TravelLocation(sourceId, BatesSourceTile, TravelLocationSpace.OverlandTile),
                new TravelLocation(local.MapId, tile, TravelLocationSpace.LocalMapTile), destination);
        }

        /// <summary>Return is the authored passive table, NOT the entrance or remembered entry position.</summary>
        public MapTransitionResult ResolveReturn(Vector2Int localGlobalTile)
        {
            MapTransitionResult result = _transitions.Resolve(new MapTransitionSourceId(12, localGlobalTile));
            if (!result.Succeeded) return result;
            if (!_maps.TryGet(result.Destination.MapId, out MapListEntry map) || map.MapId != 1
                || map.Type != MapType.StartMap)
                return new MapTransitionResult(MapTransitionFailure.UnsupportedDestinationMap,
                    detail: "The audited local return must reach START_MAP 1.");
            return result;
        }

        private static AreaEntranceResult Fail(AreaEntranceFailure failure, string detail)
            => new(failure, detail: detail);
    }
}
