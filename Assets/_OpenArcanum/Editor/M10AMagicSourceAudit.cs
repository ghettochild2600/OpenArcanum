using System;
using System.IO;
using System.Linq;
using Arcanum.Formats.Database;
using Arcanum.Formats.Text;
using Arcanum.Runtime;
using UnityEditor;
using UnityEngine;

internal static class M10AMagicSourceAudit
{
    private static readonly int[] PhaseOneSpellIds = { 15, 55, 60 };

    [MenuItem("OpenArcanum/M10A/Run Magic Source Audit")]
    private static void Run()
    {
        using DatVirtualFileSystem vfs = MountSourceData();
        MesFile names = MesReader.Read(vfs.ReadAllBytes("mes/spell.mes"));
        MesFile rules = MesReader.Read(vfs.ReadAllBytes("rules/spelllist.mes"));
        foreach (int id in PhaseOneSpellIds)
        {
            int ruleBase = 1000 + 50 * id;
            Debug.Log($"M10A SOURCE SPELL: id={id}; name={names.Get(id) ?? "<missing>"}");
            foreach (var entry in rules.Entries.Where(value => value.Key >= ruleBase && value.Key < ruleBase + 50))
                Debug.Log($"M10A SOURCE RULE: spell={id}; key={entry.Key}; offset={entry.Key - ruleBase}; value={entry.Value}");
        }
        Debug.Log("M10A MAGIC SOURCE AUDIT PASS");
    }

    [MenuItem("OpenArcanum/M10A/Run Finite Duration Source Audit %#d")]
    private static void RunFiniteDurationAudit()
    {
        using DatVirtualFileSystem vfs = MountSourceData();
        MesFile names = MesReader.Read(vfs.ReadAllBytes("mes/spell.mes"));
        MesFile rules = MesReader.Read(vfs.ReadAllBytes("rules/spelllist.mes"));
        foreach (int id in Enumerable.Range(0, 80))
        {
            int ruleBase = 1000 + 50 * id;
            var spellEntries = rules.Entries
                .Where(value => value.Key >= ruleBase && value.Key < ruleBase + 50)
                .ToArray();
            if (!spellEntries.Any(value => value.Value.IndexOf("Duration:",
                    StringComparison.OrdinalIgnoreCase) >= 0)) continue;
            Debug.Log($"M10A FINITE SPELL: id={id}; name={names.Get(id) ?? "<missing>"}");
            foreach (var entry in spellEntries)
                Debug.Log($"M10A FINITE RULE: spell={id}; key={entry.Key}; offset={entry.Key - ruleBase}; value={entry.Value}");
        }
        Debug.Log("M10A FINITE DURATION SOURCE AUDIT PASS");
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
