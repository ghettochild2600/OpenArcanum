using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Database;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Runtime;
using Arcanum.Runtime.Economy;
using Arcanum.Runtime.World;
using UnityEditor;
using UnityEngine;

internal static class M11AEconomyValidation
{
    [MenuItem("OpenArcanum/M11A/Run Merchant Source Audit #&4")]
    private static void RunSourceAudit()
    {
        using DatVirtualFileSystem vfs = MountSource();
        string protoDirectory = GameDataLocator.FindDirectory("data/proto") ?? string.Empty;
        var prototypes = new ProtoLibrary(protoDirectory);
        var authored = new List<(string Path, ObjectInstance Instance)>();
        var byIdentity = new Dictionary<string, ObjectInstance>(StringComparer.OrdinalIgnoreCase);
        var matches = new List<string>();
        MesFile sourceRows = MesReader.Read(vfs.ReadAllBytes("rules/InvenSource.mes"));
        InventorySourceCatalog catalog = InventorySourceCatalog.FromMes(
            sourceRows,
            MesReader.Read(vfs.ReadAllBytes("rules/InvenSourceBuy.mes")));
        int scanned = 0;
        foreach (string path in vfs.EnumerateFiles("maps/")
                     .Where(value => value.EndsWith(".mob", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                byte[] bytes = vfs.ReadAllBytes(path);
                int offset = 0;
                ObjectInstance instance = ObjectInstanceReader.Read(bytes, ref offset);
                scanned++;
                if (instance == null) continue;
                authored.Add((path, instance));
                byIdentity[instance.Identity.Key] = instance;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"M11A source audit skipped {path}: {ex.Message}");
            }
        }
        foreach ((string path, ObjectInstance instance) in authored.Where(value => value.Instance.Type == ObjectType.Npc))
        {
            try
            {
                ObjectProtoInfo prototype = prototypes.Get(instance.PrototypeNumber);
                int multiplier = instance.RetailPriceMultiplier ?? prototype?.RetailPriceMultiplier ?? 0;
                int sourceId = instance.InventorySource ?? prototype?.InventorySource ?? 0;
                byte[] substitute = instance.SubstituteInventoryOid ?? prototype?.SubstituteInventoryOid;
                ArcanumObjectId substituteIdentity = ArcanumObjectId.FromBytes(substitute);
                if (substitute != null && byIdentity.TryGetValue(substituteIdentity.Key, out ObjectInstance store))
                {
                    ObjectProtoInfo storePrototype = prototypes.Get(store.PrototypeNumber);
                    sourceId = store.InventorySource ?? storePrototype?.InventorySource ?? sourceId;
                }
                int buyScript = instance.BuyObjectScriptNum != 0
                    ? instance.BuyObjectScriptNum : prototype?.BuyObjectScriptNum ?? 0;
                if (sourceId <= 0) continue;
                long sectorX = (uint)instance.MapX >> 6;
                long sectorY = (uint)instance.MapY >> 6;
                long sectorId = sectorX | (sectorY << 26);
                matches.Add($"path={path}; sector=maps/{path.Split('/')[1]}/{sectorId}.sec; "
                            + $"id={instance.Identity}; proto={instance.PrototypeNumber}; "
                            + $"tile=({instance.TileX},{instance.TileY}); multiplier={multiplier}; "
                            + $"inventorySource={sourceId}; substitute={substituteIdentity}; "
                            + $"buyScript={buyScript}; dialog={instance.DialogNum}; name={instance.NameIndex}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"M11A source audit skipped {path}: {ex.Message}");
            }
        }
        catalog.TryGet(7, out InventorySourceDefinition fixtureSource);
        string fixture = fixtureSource == null ? "missing" :
            $"name={fixtureSource.Name},all={fixtureSource.BuysAll},entries="
            + string.Join(",", fixtureSource.Entries.Take(8).Select(value => value.PrototypeNumber))
            + ",buys=" + string.Join(",", fixtureSource.AcceptedBasicPrototypes.Take(20));
        Debug.Log($"M11A MERCHANT SOURCE AUDIT: scanned={scanned}; matches={matches.Count}; "
                  + $"sourceDefinitions={catalog.Definitions.Count}; source7={fixture}\n"
                  + string.Join("\n", matches.Take(40)));
    }

    private static DatVirtualFileSystem MountSource()
    {
        var vfs = new DatVirtualFileSystem();
        string module = GameDataLocator.Find("modules/Arcanum.dat");
        if (!string.IsNullOrEmpty(module)) vfs.MountFile(module);
        foreach (string archive in new[] { "arcanum1.dat", "arcanum2.dat", "arcanum3.dat", "arcanum4.dat" })
        {
            string path = GameDataLocator.Find(archive);
            if (!string.IsNullOrEmpty(path)) vfs.MountFile(path);
        }
        return vfs;
    }
}
