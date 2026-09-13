using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Formats.Database;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using Arcanum.Runtime;
using UnityEditor;
using UnityEngine;

internal static class M4DSourceAudit
{
    private const string FixtureSector = "maps/arcanum1-024-fixed/101602821844.sec";
    private static readonly ArcanumObjectId FixtureIdentity = ArcanumObjectId.CreateGuid(
        Guid.Parse("065ece33-acf4-4b3a-b98e-9ab36c467e6f"));

    [MenuItem("OpenArcanum/M4D/Run Source Audit")]
    private static void Run()
    {
        using DatVirtualFileSystem vfs = MountSourceData();
        MesFile effects = MesReader.Read(vfs.ReadAllBytes("rules/effect.mes"));
        foreach (int effect in Enumerable.Range(64, 11).Append(330))
            Debug.Log($"M4D SOURCE EFFECT {effect}: {effects.Get(effect) ?? "<missing>"}");

        string protoDirectory = GameDataLocator.FindDirectory("data/proto");
        if (string.IsNullOrEmpty(protoDirectory))
            throw new DirectoryNotFoundException("The configured source data has no data/proto directory.");
        var prototypes = new ProtoLibrary(protoDirectory);
        List<(ObjectInstance Instance, string Source)> critters = ReadSectorCritters(vfs, FixtureSector).ToList();
        (ObjectInstance fixture, string fixtureSource) = critters.First(entry => entry.Instance.Identity == FixtureIdentity);
        LogCharacter("FIXTURE", fixture, prototypes.Get(fixture.PrototypeNumber), fixtureSource);

        int notable = 0;
        foreach ((ObjectInstance instance, string source) in critters)
        {
            ObjectProtoInfo prototype = prototypes.Get(instance.PrototypeNumber);
            int[] stats = instance.StatBase ?? prototype?.StatBase;
            int[] resistances = instance.Resistances ?? prototype?.Resistances;
            int reaction = instance.ReactionBase ?? prototype?.ReactionBase ?? 50;
            if (!(resistances?.Any(value => value != 0) ?? false)
                && At(stats, 19) == 0 && At(stats, 22) == 0 && At(stats, 23) == 0 && reaction == 50)
                continue;
            LogCharacter("NOTABLE", instance, prototype, source);
            notable++;
        }

        Debug.Log($"M4D SOURCE AUDIT PASS: fixture={FixtureIdentity}; critters={critters.Count}; notable={notable}.");
    }

    private static void LogCharacter(string label, ObjectInstance instance, ObjectProtoInfo prototype, string source)
    {
        int[] stats = instance.StatBase ?? prototype?.StatBase;
        int[] resistances = instance.Resistances ?? prototype?.Resistances;
        int reaction = instance.ReactionBase ?? prototype?.ReactionBase ?? 50;
        Debug.Log($"M4D SOURCE {label}: source={source}; oid={instance.Identity}; type={instance.Type}; " +
                  $"proto={instance.PrototypeNumber}; stats=[{Format(stats)}]; " +
                  $"alignment={At(stats, 19)}; magickPoints={At(stats, 22)}; techPoints={At(stats, 23)}; " +
                  $"reactionBase={reaction}; resistances=[{Format(resistances)}]; " +
                  $"critterFlags={instance.CritterFlags ?? prototype?.CritterFlags ?? 0}; " +
                  $"instanceStat={(instance.StatBase != null ? "yes" : "no")}; " +
                  $"instanceResistance={(instance.Resistances != null ? "yes" : "no")}; " +
                  $"instanceReaction={(instance.ReactionBase.HasValue ? "yes" : "no")}");
    }

    private static IEnumerable<(ObjectInstance Instance, string Source)> ReadSectorCritters(
        DatVirtualFileSystem vfs, string sector)
    {
        foreach (ObjectInstance instance in SectorReader.ReadObjects(vfs.ReadAllBytes(sector)))
            if (instance.Type is ObjectType.Pc or ObjectType.Npc) yield return (instance, sector);

        long sectorId = long.Parse(Path.GetFileNameWithoutExtension(sector));
        string mapPrefix = sector.Substring(0, sector.LastIndexOf('/') + 1);
        foreach (string mobilePath in vfs.EnumerateFiles(mapPrefix)
                     .Where(path => path.EndsWith(".mob", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            byte[] bytes = vfs.ReadAllBytes(mobilePath);
            int offset = 0;
            ObjectInstance instance;
            try { instance = ObjectInstanceReader.Read(bytes, ref offset); }
            catch { continue; }
            if (instance.Type is ObjectType.Pc or ObjectType.Npc && BelongsToSector(instance, sectorId))
                yield return (instance, mobilePath);
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

    private static bool BelongsToSector(ObjectInstance instance, long sectorId)
    {
        if (!instance.Location.HasValue) return false;
        long sectorX = (uint)instance.MapX >> 6;
        long sectorY = (uint)instance.MapY >> 6;
        return (sectorX | (sectorY << 26)) == sectorId;
    }

    private static int At(int[] values, int index) => values != null && values.Length > index ? values[index] : 0;
    private static string Format(int[] values) => values == null ? "<inherited>" : string.Join(",", values);
}
