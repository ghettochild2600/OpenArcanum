using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Formats.Database;
using Arcanum.Formats.Dialog;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Script;
using Arcanum.Runtime;
using UnityEditor;
using UnityEngine;

internal static class M5ACorpusAudit
{
    private static readonly HashSet<string> PersistentTests = new(StringComparer.OrdinalIgnoreCase)
        { "gf", "gv", "qu", "pf", "pv", "lf", "lc" };
    private static readonly HashSet<string> PersistentEffects = new(StringComparer.OrdinalIgnoreCase)
        { "gf", "gv", "qu", "pf", "pv", "lf", "lc" };
    private static readonly HashSet<string> DeferredEffects = new(StringComparer.OrdinalIgnoreCase)
        { "co", "nk", "jo", "lv", "in", "$$", "xp", "fp", "mm", "rp", "ru" };

    [MenuItem("OpenArcanum/M5A/Run Corpus Candidate Audit")]
    private static void Run()
    {
        using DatVirtualFileSystem vfs = MountSourceData();
        string protoDirectory = GameDataLocator.FindDirectory("data/proto");
        if (string.IsNullOrEmpty(protoDirectory))
            throw new DirectoryNotFoundException("The configured source data has no data/proto directory.");
        var prototypes = new ProtoLibrary(protoDirectory);
        ScriptDatabase scripts = ScriptDatabase.Load(vfs);

        var candidates = new Dictionary<string, Candidate>(StringComparer.Ordinal);
        int mobileCount = 0;
        int npcCount = 0;
        foreach (string path in vfs.EnumerateFiles("maps/")
                     .Where(value => value.EndsWith(".mob", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            mobileCount++;
            ObjectInstance instance;
            try
            {
                byte[] bytes = vfs.ReadAllBytes(path);
                int offset = 0;
                instance = ObjectInstanceReader.Read(bytes, ref offset);
            }
            catch
            {
                continue;
            }
            if (instance.Type != ObjectType.Npc || !instance.Location.HasValue || !instance.Identity.IsPersistent)
                continue;
            npcCount++;
            ObjectProtoInfo prototype = prototypes.Get(instance.PrototypeNumber);
            int dialogNum = instance.DialogNum != 0 ? instance.DialogNum : prototype?.DialogNum ?? 0;
            if (dialogNum <= 0) continue;
            string dialogPath = DialogLocator.FindPath(vfs, dialogNum);
            if (dialogPath == null) continue;
            DialogScript dialog = DialogLocator.Load(vfs, dialogNum);
            if (dialog == null) continue;

            string key = instance.Identity.ToString();
            if (candidates.ContainsKey(key)) continue;
            candidates.Add(key, Candidate.Build(instance, path, SectorPath(path, instance), dialogNum,
                dialogPath, dialog, scripts.Get(dialogNum)));
        }

        List<Candidate> ordered = candidates.Values
            .OrderByDescending(candidate => candidate.HasClosedStateLoop)
            .ThenBy(candidate => candidate.HasDeferredDialogEffects)
            .ThenBy(candidate => candidate.LineCount)
            .ThenBy(candidate => candidate.DialogNum)
            .ToList();
        Debug.Log($"M5A CORPUS SUMMARY: scripts={scripts.Count}; scriptParseFailures={scripts.FailedCount}; " +
                  $"mobileFiles={mobileCount}; npcs={npcCount}; dialogNpcs={ordered.Count}; " +
                  $"closedStateLoops={ordered.Count(candidate => candidate.HasClosedStateLoop)}.");

        foreach (Candidate candidate in ordered.Take(30))
        {
            Debug.Log(candidate.Summary());
            if (!candidate.HasClosedStateLoop || candidate.LineCount > 100) continue;
            foreach (string line in candidate.Structure.Take(40))
                Debug.Log($"M5A CANDIDATE STRUCTURE: dialog={candidate.DialogNum}; {line}");
        }
        foreach (Candidate candidate in ordered.Where(value => value.DirectOptionCampaignMutation)
                     .OrderBy(value => value.HasDeferredDialogEffects)
                     .ThenBy(value => value.LineCount)
                     .Take(30))
        {
            Debug.Log($"M5A DIRECT MUTATION: {candidate.Summary()}");
            foreach (string line in candidate.Structure.Where(value => value.Contains("role=option")
                                                                       && (value.Contains("effect=gf")
                                                                           || value.Contains("effect=gv")
                                                                           || value.Contains("effect=qu"))))
                Debug.Log($"M5A DIRECT MUTATION LINE: dialog={candidate.DialogNum}; {line}");
        }
        foreach (int dialogNum in new[] { 2074, 1639, 1520, 2291, 1168, 1035, 1760, 2762, 1921 })
        {
            Candidate candidate = ordered.FirstOrDefault(value => value.DialogNum == dialogNum);
            if (candidate == null) continue;
            Debug.Log($"M5A TARGET AUDIT: {candidate.Summary()}");
            foreach (string line in candidate.Structure)
                Debug.Log($"M5A TARGET DIALOG: dialog={dialogNum}; {line}");
            ScriptFile script = scripts.Get(dialogNum);
            if (script == null) continue;
            for (int index = 0; index < script.Entries.Count; index++)
            {
                ScriptCondition entry = script.Entries[index];
                Debug.Log($"M5A TARGET SCRIPT: dialog={dialogNum}; line={index}; " +
                          $"condition={(Sct)entry.Type}{Operands(entry.OpType, entry.OpValue)}; " +
                          $"then={Action(entry.Action)}; else={Action(entry.Els)}");
            }
        }
        Debug.Log("M5A CORPUS AUDIT PASS");
    }

    private sealed class Candidate
    {
        public ArcanumObjectId Identity;
        public int Prototype;
        public int DialogNum;
        public string Source;
        public string Sector;
        public string DialogPath;
        public int LineCount;
        public int NpcLineCount;
        public int OptionCount;
        public string[] TestCodes;
        public string[] EffectCodes;
        public string[] ScriptConditions;
        public string[] ScriptActions;
        public bool HasClosedStateLoop;
        public bool HasDeferredDialogEffects;
        public bool DirectOptionCampaignMutation;
        public List<string> Structure;

        public static Candidate Build(ObjectInstance instance, string source, string sector, int dialogNum,
            string dialogPath, DialogScript dialog, ScriptFile script)
        {
            var tests = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var effects = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var structure = new List<string>();
            int npcLines = 0;
            int options = 0;
            bool directOptionCampaignMutation = false;
            foreach (int number in dialog.LineNumbers)
            {
                if (!dialog.TryGet(number, out DialogLine line)) continue;
                if (line.IsNpcSpeech) npcLines++; else options++;
                foreach (string code in Codes(line.Test)) tests.Add(code);
                foreach (string code in Codes(line.Effect)) effects.Add(code);
                if (!line.IsNpcSpeech && Codes(line.Effect).Any(code =>
                        code.Equals("gf", StringComparison.OrdinalIgnoreCase)
                        || code.Equals("gv", StringComparison.OrdinalIgnoreCase)
                        || code.Equals("qu", StringComparison.OrdinalIgnoreCase)))
                    directOptionCampaignMutation = true;
                structure.Add($"line={line.Num}; role={(line.IsNpcSpeech ? "npc" : "option")}; iq={line.Iq}; " +
                              $"test={Empty(line.Test)}; target={line.Target}; effect={Empty(line.Effect)}");
            }

            string[] scriptConditions = script?.Entries
                .Select(entry => ((Sct)entry.Type).ToString()).Distinct().OrderBy(value => value).ToArray()
                ?? Array.Empty<string>();
            string[] scriptActions = script?.Entries
                .SelectMany(entry => new[] { entry.Action, entry.Els })
                .Where(action => action != null)
                .Select(action => ((Sat)action.Type).ToString()).Distinct().OrderBy(value => value).ToArray()
                ?? Array.Empty<string>();
            return new Candidate
            {
                Identity = instance.Identity,
                Prototype = instance.PrototypeNumber,
                DialogNum = dialogNum,
                Source = source,
                Sector = sector,
                DialogPath = dialogPath,
                LineCount = structure.Count,
                NpcLineCount = npcLines,
                OptionCount = options,
                TestCodes = tests.ToArray(),
                EffectCodes = effects.ToArray(),
                ScriptConditions = scriptConditions,
                ScriptActions = scriptActions,
                HasClosedStateLoop = tests.Any(PersistentTests.Contains) && effects.Any(PersistentEffects.Contains),
                HasDeferredDialogEffects = effects.Any(DeferredEffects.Contains),
                DirectOptionCampaignMutation = directOptionCampaignMutation,
                Structure = structure,
            };
        }

        public string Summary()
            => $"M5A CANDIDATE: oid={Identity}; proto={Prototype}; sector={Sector}; source={Source}; " +
               $"dialog={DialogNum}; resource={DialogPath}; lines={LineCount} ({NpcLineCount} npc/{OptionCount} options); " +
               $"tests=[{string.Join(",", TestCodes)}]; effects=[{string.Join(",", EffectCodes)}]; " +
               $"scriptConditions=[{string.Join(",", ScriptConditions)}]; " +
               $"scriptActions=[{string.Join(",", ScriptActions)}]; closedLoop={HasClosedStateLoop}; " +
               $"deferredDialogEffects={HasDeferredDialogEffects}";
    }

    private static IEnumerable<string> Codes(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) yield break;
        for (int index = 0; index + 1 < value.Length;)
        {
            while (index < value.Length && !IsCode(value[index])) index++;
            if (index + 1 >= value.Length || !IsCode(value[index + 1])) yield break;
            yield return value.Substring(index, 2).ToLowerInvariant();
            index += 2;
            while (index < value.Length && !IsCode(value[index])) index++;
        }
    }

    private static bool IsCode(char value) => char.IsLetter(value) || value == '$';
    private static string Empty(string value) => string.IsNullOrWhiteSpace(value) ? "<none>" : value;

    private static string Action(ScriptAction action)
        => action == null ? "<null>" : $"{(Sat)action.Type}{Operands(action.OpType, action.OpValue)}";

    private static string Operands(byte[] types, int[] values)
    {
        var entries = new List<string>();
        for (int index = 0; index < values.Length; index++)
        {
            if (index > 1 && types[index] == (byte)Svt.Number && values[index] == 0) continue;
            entries.Add($"{index}:{(Svt)types[index]}={values[index]}");
        }
        return entries.Count == 0 ? string.Empty : "(" + string.Join(",", entries) + ")";
    }

    private static string SectorPath(string mobilePath, ObjectInstance instance)
    {
        string prefix = mobilePath.Substring(0, mobilePath.LastIndexOf('/') + 1);
        long x = (uint)instance.MapX >> 6;
        long y = (uint)instance.MapY >> 6;
        return prefix + (x | (y << 26)) + ".sec";
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
