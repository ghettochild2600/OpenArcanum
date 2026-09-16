using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Formats.Database;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using Arcanum.Runtime;
using Arcanum.Runtime.World;
using UnityEditor;
using UnityEngine;

internal static class M7ASourceAudit
{
    [MenuItem("OpenArcanum/M7A/Run Local Transition Source Audit")]
    private static void Run()
    {
        using DatVirtualFileSystem vfs = MountSourceData();
        MapList maps = MapList.Read(MesReader.Read(vfs.ReadAllBytes("rules/maplist.mes")));
        var bySourceMap = new Dictionary<int, List<AuditedJump>>();
        int mapsWithJumpTables = 0;
        int mapsWithJumps = 0;
        int totalJumps = 0;
        int invalidDestinations = 0;
        int missingSourceSectors = 0;
        int missingDestinationSectors = 0;

        foreach (MapListEntry map in maps.Entries)
        {
            string mapPath = "maps/" + map.Name.ToLowerInvariant();
            string jumpPath = mapPath + "/map.jmp";
            if (!vfs.Exists(jumpPath)) continue;
            mapsWithJumpTables++;

            List<JumpPoint> points;
            try { points = JumpPointReader.Read(vfs.ReadAllBytes(jumpPath)); }
            catch (Exception ex)
            {
                Debug.Log($"M7A AUDIT MALFORMED: mapId={map.MapId}; map={map.Name}; resource={jumpPath}; error={ex.Message}");
                continue;
            }
            if (points.Count == 0) continue;
            mapsWithJumps++;
            totalJumps += points.Count;

            var audited = new List<AuditedJump>();
            foreach (JumpPoint point in points)
            {
                int destinationMapId = point.DstMap == 0 ? map.MapId : point.DstMap;
                bool validMap = maps.TryGet(destinationMapId, out MapListEntry destinationMap);
                string sourceSector = SectorCoordinate.FromGlobal(mapPath,
                    new Vector2(point.SrcX, point.SrcY)).Path;
                string destinationMapPath = validMap ? "maps/" + destinationMap.Name.ToLowerInvariant() : null;
                string destinationSector = validMap
                    ? SectorCoordinate.FromGlobal(destinationMapPath, new Vector2(point.DstX, point.DstY)).Path
                    : null;
                bool sourceExists = vfs.Exists(sourceSector);
                bool destinationExists = validMap && vfs.Exists(destinationSector);
                if (!validMap) invalidDestinations++;
                if (!sourceExists) missingSourceSectors++;
                if (!destinationExists) missingDestinationSectors++;
                audited.Add(new AuditedJump(map, point, destinationMapId, destinationMap,
                    sourceSector, destinationSector, sourceExists, destinationExists));
            }
            bySourceMap[map.MapId] = audited;
        }

        Debug.Log($"M7A AUDIT SUMMARY: mapList={maps.Entries.Count}; jumpTables={mapsWithJumpTables}; " +
                  $"mapsWithJumps={mapsWithJumps}; records={totalJumps}; invalidDestinations={invalidDestinations}; " +
                  $"missingSourceSectors={missingSourceSectors}; missingDestinationSectors={missingDestinationSectors}");

        foreach ((int sourceMapId, List<AuditedJump> records) in bySourceMap.OrderBy(pair => pair.Key))
        {
            foreach (IGrouping<string, AuditedJump> group in records.GroupBy(value => value.GroupKey)
                         .OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                AuditedJump first = group.First();
                bool reverse = first.ValidDestination
                               && bySourceMap.TryGetValue(first.DestinationMapId, out List<AuditedJump> destinationRecords)
                               && destinationRecords.Any(candidate => candidate.DestinationMapId == sourceMapId);
                string classification = first.SourceExists && first.DestinationExists && reverse
                    ? "GREEN"
                    : first.SourceExists && first.DestinationExists ? "YELLOW" : "RED";
                Debug.Log($"M7A AUDIT CANDIDATE: class={classification}; sourceMapId={first.SourceMap.MapId}; " +
                          $"sourceMap={first.SourceMap.Name}; sourceTiles={Tiles(group.Select(value => value.Point.SrcX), group.Select(value => value.Point.SrcY))}; " +
                          $"sourceSector={first.SourceSector}; flags={string.Join("|", group.Select(value => value.Point.Flags).Distinct())}; " +
                          $"destinationMapId={first.DestinationMapId}; destinationMap={(first.ValidDestination ? first.DestinationMap.Name : "<invalid>")}; " +
                          $"destinationTile={first.Point.DstX},{first.Point.DstY}; destinationSector={first.DestinationSector ?? "<none>"}; " +
                          $"sourceSectorExists={first.SourceExists}; destinationSectorExists={first.DestinationExists}; reverseMapJump={reverse}; records={group.Count()}");
            }
        }
        Debug.Log("M7A LOCAL TRANSITION SOURCE AUDIT PASS");
    }

    private static string Tiles(IEnumerable<int> xs, IEnumerable<int> ys)
    {
        int[] x = xs.ToArray();
        int[] y = ys.ToArray();
        return $"x[{x.Min()}..{x.Max()}],y[{y.Min()}..{y.Max()}]";
    }

    private sealed class AuditedJump
    {
        public MapListEntry SourceMap { get; }
        public JumpPoint Point { get; }
        public int DestinationMapId { get; }
        public MapListEntry DestinationMap { get; }
        public string SourceSector { get; }
        public string DestinationSector { get; }
        public bool SourceExists { get; }
        public bool DestinationExists { get; }
        public bool ValidDestination => DestinationMapId > 0 && !string.IsNullOrWhiteSpace(DestinationMap.Name);
        public string GroupKey => $"{DestinationMapId}:{Point.DstLoc}:{SourceSector}";

        public AuditedJump(MapListEntry sourceMap, JumpPoint point, int destinationMapId,
            MapListEntry destinationMap, string sourceSector, string destinationSector,
            bool sourceExists, bool destinationExists)
        {
            SourceMap = sourceMap;
            Point = point;
            DestinationMapId = destinationMapId;
            DestinationMap = destinationMap;
            SourceSector = sourceSector;
            DestinationSector = destinationSector;
            SourceExists = sourceExists;
            DestinationExists = destinationExists;
        }
    }

    private static DatVirtualFileSystem MountSourceData()
    {
        var vfs = new DatVirtualFileSystem();
        string module = GameDataLocator.Find("modules/Arcanum.dat");
        if (string.IsNullOrEmpty(module))
            throw new FileNotFoundException("The configured source data has no modules/Arcanum.dat.");
        vfs.MountFile(module);
        foreach (string archive in new[] { "arcanum1.dat", "arcanum2.dat", "arcanum3.dat", "arcanum4.dat" })
        {
            string path = GameDataLocator.Find(archive);
            if (!string.IsNullOrEmpty(path)) vfs.MountFile(path);
        }
        return vfs;
    }
}
