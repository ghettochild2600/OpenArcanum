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

internal static class M4CSourceAudit
{
    private const string FixtureSector = "maps/arcanum1-024-fixed/101602821844.sec";
    private static readonly ArcanumObjectId FixtureIdentity = ArcanumObjectId.CreateGuid(
        Guid.Parse("065ece33-acf4-4b3a-b98e-9ab36c467e6f"));

    [MenuItem("OpenArcanum/M4C/Run Source Audit")]
    private static void Run()
    {
        using DatVirtualFileSystem vfs = MountSourceData();
        MesFile xp = MesReader.Read(vfs.ReadAllBytes("rules/xp_level.mes"));
        Debug.Log("M4C SOURCE XP: " + string.Join(",", Enumerable.Range(1, 51)
            .Select(level => $"{level}:{xp.Get(level) ?? "<missing>"}")));

        string protoDirectory = GameDataLocator.FindDirectory("data/proto");
        if (string.IsNullOrEmpty(protoDirectory))
            throw new DirectoryNotFoundException("The configured source data has no data/proto directory.");
        var prototypes = new ProtoLibrary(protoDirectory);
        ObjectInstance fixture = ReadFixture(vfs);
        ObjectProtoInfo fixturePrototype = prototypes.Get(fixture.PrototypeNumber)
            ?? throw new InvalidOperationException($"Missing fixture prototype {fixture.PrototypeNumber}.");
        LogCharacter("FIXTURE", fixture, fixturePrototype, FixtureSector);

        int nonZero = 0;
        foreach ((ObjectInstance instance, string source) in ReadSectorCritters(vfs, FixtureSector))
        {
            ObjectProtoInfo prototype = prototypes.Get(instance.PrototypeNumber);
            int[] basic = instance.BasicSkills ?? prototype?.BasicSkills;
            int[] tech = instance.TechSkills ?? prototype?.TechSkills;
            if (!(basic?.Any(value => value != 0) ?? false) && !(tech?.Any(value => value != 0) ?? false))
                continue;
            LogCharacter("NONZERO", instance, prototype, source);
            nonZero++;
        }
        Debug.Log($"M4C SOURCE AUDIT PASS: nonZeroCritters={nonZero}; fixture={FixtureIdentity}; proto={fixture.PrototypeNumber}.");
    }

    private static void LogCharacter(string label, ObjectInstance instance, ObjectProtoInfo prototype, string source)
    {
        int[] stats = instance.StatBase ?? prototype?.StatBase;
        int[] basic = instance.BasicSkills ?? prototype?.BasicSkills;
        int[] tech = instance.TechSkills ?? prototype?.TechSkills;
        Debug.Log($"M4C SOURCE {label}: source={source}; oid={instance.Identity}; type={instance.Type}; " +
                  $"proto={instance.PrototypeNumber}; level={At(stats, 17)}; xp={At(stats, 18)}; " +
                  $"unspent={At(stats, 21)}; critterFlags={instance.CritterFlags ?? prototype?.CritterFlags ?? 0}; " +
                  $"instanceStat={(instance.StatBase != null ? "yes" : "no")}; " +
                  $"instanceBasic=[{Format(instance.BasicSkills)}]; prototypeBasic=[{Format(prototype?.BasicSkills)}]; " +
                  $"resolvedBasic=[{Format(basic)}]; instanceTech=[{Format(instance.TechSkills)}]; " +
                  $"prototypeTech=[{Format(prototype?.TechSkills)}]; resolvedTech=[{Format(tech)}]");
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

    private static ObjectInstance ReadFixture(DatVirtualFileSystem vfs)
        => ReadSectorCritters(vfs, FixtureSector).Select(entry => entry.Instance)
            .FirstOrDefault(instance => instance.Identity == FixtureIdentity)
           ?? throw new InvalidOperationException($"Source fixture {FixtureIdentity} was not found in {FixtureSector}.");

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
