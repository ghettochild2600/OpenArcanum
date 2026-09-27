using System;
using System.IO;
using System.Linq;
using Arcanum.Formats.Database;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Runtime;
using UnityEditor;
using UnityEngine;

internal static class M10BTechnologySourceAudit
{
    private const int ItemFlagCanUse = 0x80;
    private const int ItemFlagNeedsTarget = 0x100;
    private const int GenericHealingItem = 0x8;
    private const int GenericGrenade = 0x10;

    [MenuItem("OpenArcanum/M10B/Run Technology Source Audit")]
    private static void Run()
    {
        using DatVirtualFileSystem vfs = MountSourceData();
        MesFile technology = ReadOptional(vfs, "mes/tech.mes");
        MesFile spellNames = MesReader.Read(vfs.ReadAllBytes("mes/spell.mes"));
        MesFile spellRules = MesReader.Read(vfs.ReadAllBytes("rules/spelllist.mes"));
        MesFile descriptions = MesReader.Read(vfs.ReadAllBytes("mes/description.mes"));
        MesFile schematics = MesReader.Read(vfs.ReadAllBytes("rules/schematic.mes"));
        MesFile schematicText = MesReader.Read(vfs.ReadAllBytes("mes/schematic_text.mes"));

        if (technology != null)
            foreach (var entry in technology.Entries)
                Debug.Log($"M10B SOURCE TECH: key={entry.Key}; value={entry.Value}");

        foreach (int id in Enumerable.Range(140, 83))
        {
            int ruleBase = 1000 + 50 * id + 2000;
            var entries = spellRules.Entries
                .Where(entry => entry.Key >= ruleBase && entry.Key < ruleBase + 50)
                .ToArray();
            if (entries.Length == 0 && string.IsNullOrEmpty(spellNames.Get(id))) continue;
            Debug.Log($"M10B SOURCE EFFECT: id={id}; name={spellNames.Get(id) ?? "<missing>"}");
            foreach (var entry in entries)
                Debug.Log($"M10B SOURCE EFFECT RULE: effect={id}; key={entry.Key}; offset={entry.Key - ruleBase}; value={entry.Value}");
        }

        for (int discipline = 0; discipline < 8; discipline++)
            for (int degree = 1; degree < 8; degree++)
            {
                int schematic = 1990 + 200 * discipline + 10 * degree;
                string nameKey = schematics.Get(schematic);
                Debug.Log($"M10B SOURCE SCHEMATIC: id={schematic}; discipline={discipline}; degree={degree}; nameKey={nameKey ?? "<missing>"}; name={(int.TryParse(nameKey, out int key) ? schematicText.Get(key) : null) ?? "<missing>"}; item1={schematics.Get(schematic + 3) ?? "<missing>"}; item2={schematics.Get(schematic + 4) ?? "<missing>"}; product={schematics.Get(schematic + 5) ?? "<missing>"}; quantity={schematics.Get(schematic + 6) ?? "<missing>"}");
            }

        int parsed = 0;
        int candidates = 0;
        string protoDirectory = GameDataLocator.FindDirectory("data/proto");
        if (string.IsNullOrEmpty(protoDirectory))
            throw new DirectoryNotFoundException("The configured source data has no loose data/proto directory.");
        foreach (string path in Directory.EnumerateFiles(protoDirectory, "*.pro")
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            if (!ObjectProtoReader.TryReadInfo(File.ReadAllBytes(path), out ObjectProtoInfo proto)) continue;
            parsed++;
            int flags = proto.ItemFlags ?? 0;
            int genericFlags = proto.GenericFlags ?? 0;
            bool techComplexity = proto.ItemComplexity.GetValueOrDefault() < 0;
            bool techEffect = proto.ItemSpell is >= 140 and <= 222;
            bool usable = (flags & (ItemFlagCanUse | ItemFlagNeedsTarget)) != 0;
            bool specialGeneric = (genericFlags & (GenericHealingItem | GenericGrenade)) != 0;
            if (!techComplexity && !techEffect && !specialGeneric) continue;

            candidates++;
            string name = proto.Description.HasValue
                ? descriptions.Get(proto.Description.Value) ?? "<missing description>"
                : "<no description>";
            string weapon = proto.Weapon == null
                ? string.Empty
                : $"; weaponFlags=0x{proto.Weapon.Flags:X}; range={proto.Weapon.Range}; ammoType={proto.Weapon.AmmoType}; ammoUse={proto.Weapon.AmmoConsumption}; magicTech={proto.Weapon.MagicTechComplexity}";
            Debug.Log($"M10B SOURCE ITEM: proto={proto.ProtoNumber}; name={name}; type={proto.Type}; path={path}; itemFlags=0x{flags:X}; genericFlags=0x{genericFlags:X}; complexity={proto.ItemComplexity?.ToString() ?? "<unset>"}; discipline={proto.ItemDiscipline?.ToString() ?? "<unset>"}; effect={proto.ItemSpell?.ToString() ?? "<unset>"}; charges={proto.SpellMana?.ToString() ?? "<unset>"}; useScript={proto.UseScriptNum}; usable={usable}{weapon}");
        }

        Debug.Log($"M10B TECHNOLOGY SOURCE AUDIT PASS: parsed={parsed}; candidates={candidates}");
    }

    private static MesFile ReadOptional(DatVirtualFileSystem vfs, string path)
        => vfs.Exists(path) ? MesReader.Read(vfs.ReadAllBytes(path)) : null;

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
