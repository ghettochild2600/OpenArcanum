using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Formats;
using Arcanum.Formats.Database;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using Arcanum.Runtime;
using UnityEditor;
using UnityEngine;

internal static class M7DWorldMapDestinationAudit
{
    [MenuItem("OpenArcanum/M7D/Run Destination Source Audit")]
    private static void Run()
    {
        using DatVirtualFileSystem vfs = MountSourceData();
        AreaList areas = AreaList.FromMes(MesReader.Read(vfs.ReadAllBytes("mes/gamearea.mes")));
        MapList maps = MapList.Read(vfs.ReadAllBytes("rules/MapList.mes"));

        Area[] ordered = areas.Areas.ToArray();
        int duplicateIds = ordered.GroupBy(value => value.Id).Count(group => group.Count() > 1);
        int duplicateNames = ordered.Where(value => value.Id > 0)
            .GroupBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
            .Count(group => group.Count() > 1);
        int duplicateCoordinates = ordered.Where(value => value.Id > 0)
            .GroupBy(value => (value.TileX, value.TileY)).Count(group => group.Count() > 1);
        int nonCanonicalOrder = ordered.Select((value, index) => (value, index))
            .Count(pair => pair.value.Id != pair.index);
        int invalidTravelRecords = ordered.Count(value => value.Id > 0
            && (string.IsNullOrWhiteSpace(value.Name) || value.TileX <= 0 || value.TileY <= 0));
        int mapAssociations = maps.Entries.Count(value => value.Area > 0);
        int mappedAreas = maps.Entries.Where(value => value.Area > 0)
            .Select(value => value.Area).Distinct().Count();

        Require(ordered.Length == 82, "retail area count");
        Require(ordered.Length > 0 && ordered[0].Id == 0, "area zero is the source unknown sentinel");
        Require(duplicateIds == 0, "canonical area identities are unique");
        Require(nonCanonicalOrder == 0, "MES iteration order is canonical contiguous ID order");
        Require(areas.TryGet(58, out Area knaTha), "K'na Tha exists");
        Require(areas.TryGet(21, out Area tarant), "Tarant exists");

        Debug.Log($"M7D AUDIT SUMMARY: areas={ordered.Length}; sourceOrder=0..{ordered.Length - 1}; "
                  + $"duplicateIds={duplicateIds}; duplicateNames={duplicateNames}; "
                  + $"duplicateCoordinates={duplicateCoordinates}; nonCanonicalOrder={nonCanonicalOrder}; "
                  + $"invalidTravelRecords={invalidTravelRecords}; mapAssociations={mapAssociations}; "
                  + $"mappedAreas={mappedAreas}");
        Debug.Log($"M7D FIXTURE: knownCandidate={Describe(knaTha)}; unknownCandidate={Describe(tarant)}");
        foreach (Area area in ordered.Where(value => value.Id > 0
                     && (string.IsNullOrWhiteSpace(value.Name) || value.TileX <= 0 || value.TileY <= 0)))
            Debug.Log($"M7D INVALID DESTINATION RECORD: {Describe(area)}; description={area.Description}");
        Debug.Log("M7D SOURCE CONTRACT: world-map markers enumerate area_count-1 down to 1 and add only "
                  + "area_is_known entries; area metadata has no local-map identity, entrance, category, or "
                  + "disabled flag; Radius:-1 suppresses proximity discovery and does not disable known selection.");
        Debug.Log("M7D WORLD-MAP DESTINATION SOURCE AUDIT PASS");
    }

    private static string Describe(Area area)
        => $"id={area.Id},name={area.Name},tile=({area.TileX},{area.TileY}),"
           + $"label=({area.LabelXOff},{area.LabelYOff}),radiusTiles={area.RadiusTiles}";

    private static void Require(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("M7D audit failed: " + description);
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
