using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Formats.Database;
using Arcanum.Formats.Dialog;
using Arcanum.Formats.Objects;
using Arcanum.Runtime;
using UnityEditor;
using UnityEngine;

internal static class M5CTrainerAudit
{
    [MenuItem("OpenArcanum/M5C/Run Trainer Candidate Audit")]
    private static void Run()
    {
        using DatVirtualFileSystem vfs = MountSourceData();
        var candidates = new SortedDictionary<int, Candidate>();
        foreach (string path in vfs.EnumerateFiles("dlg/")
                     .Where(value => value.EndsWith(".dlg", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            DialogScript dialog;
            try { dialog = DlgReader.Read(vfs.ReadAllBytes(path)); }
            catch { continue; }
            int number = NumberFromPath(path);
            foreach (int lineNumber in dialog.LineNumbers)
            {
                if (!dialog.TryGet(lineNumber, out DialogLine line) || line.IsNpcSpeech
                    || !line.Text.TrimStart().StartsWith("t:", StringComparison.OrdinalIgnoreCase)) continue;
                if (!candidates.TryGetValue(number, out Candidate candidate))
                {
                    candidate = new Candidate(number, path, dialog.LineNumbers.Count());
                    candidates.Add(number, candidate);
                }
                candidate.Lines.Add(new TrainingLine(line.Num, line.Text.Trim(), line.Test, line.Target, line.Effect));
            }
        }

        string protoDirectory = GameDataLocator.FindDirectory("data/proto");
        var prototypes = new ProtoLibrary(protoDirectory);
        foreach (string path in vfs.EnumerateFiles("maps/")
                     .Where(value => value.EndsWith(".mob", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            ObjectInstance instance;
            try
            {
                byte[] bytes = vfs.ReadAllBytes(path);
                int offset = 0;
                instance = ObjectInstanceReader.Read(bytes, ref offset);
            }
            catch { continue; }
            if (instance.Type != ObjectType.Npc || !instance.Identity.IsPersistent || !instance.Location.HasValue)
                continue;
            ObjectProtoInfo prototype = prototypes.Get(instance.PrototypeNumber);
            int dialog = instance.DialogNum != 0 ? instance.DialogNum : prototype?.DialogNum ?? 0;
            if (!candidates.TryGetValue(dialog, out Candidate candidate)) continue;
            candidate.Npcs.Add(new Npc(instance.Identity.ToString(), instance.PrototypeNumber,
                SectorPath(path, instance), path));
        }

        Debug.Log($"M5C TRAINER AUDIT SUMMARY: dialogs={candidates.Count}; " +
                  $"placedDialogs={candidates.Values.Count(value => value.Npcs.Count > 0)}; " +
                  $"placedNpcs={candidates.Values.Sum(value => value.Npcs.Count)}");
        foreach (Candidate candidate in candidates.Values)
        {
            Debug.Log($"M5C TRAINER CANDIDATE: dialog={candidate.Number}; resource={candidate.Resource}; " +
                      $"dialogLines={candidate.DialogLineCount}; trainingLines={candidate.Lines.Count}; " +
                      $"placedNpcs={candidate.Npcs.Count}; " +
                      $"skills=[{string.Join("|", candidate.Lines.Select(value => value.Token))}]");
            foreach (TrainingLine line in candidate.Lines)
                Debug.Log($"M5C TRAINER LINE: dialog={candidate.Number}; line={line.Number}; token={line.Token}; " +
                          $"test={Empty(line.Test)}; target={line.Target}; effect={Empty(line.Effect)}");
            foreach (Npc npc in candidate.Npcs)
                Debug.Log($"M5C TRAINER NPC: dialog={candidate.Number}; oid={npc.Identity}; proto={npc.Prototype}; " +
                          $"sector={npc.Sector}; source={npc.Source}");
        }
        Debug.Log("M5C TRAINER CANDIDATE AUDIT PASS");
    }

    private sealed class Candidate
    {
        public readonly int Number;
        public readonly string Resource;
        public readonly int DialogLineCount;
        public readonly List<TrainingLine> Lines = new();
        public readonly List<Npc> Npcs = new();

        public Candidate(int number, string resource, int dialogLineCount)
        {
            Number = number;
            Resource = resource;
            DialogLineCount = dialogLineCount;
        }
    }

    private readonly struct TrainingLine
    {
        public readonly int Number;
        public readonly string Token;
        public readonly string Test;
        public readonly int Target;
        public readonly string Effect;

        public TrainingLine(int number, string token, string test, int target, string effect)
        {
            Number = number;
            Token = token;
            Test = test;
            Target = target;
            Effect = effect;
        }
    }

    private readonly struct Npc
    {
        public readonly string Identity;
        public readonly int Prototype;
        public readonly string Sector;
        public readonly string Source;

        public Npc(string identity, int prototype, string sector, string source)
        {
            Identity = identity;
            Prototype = prototype;
            Sector = sector;
            Source = source;
        }
    }

    private static string Empty(string value) => string.IsNullOrWhiteSpace(value) ? "<none>" : value;

    private static int NumberFromPath(string path)
    {
        int start = path.LastIndexOf('/') + 1;
        if (start + 5 > path.Length) return 0;
        return int.TryParse(path.Substring(start, 5), out int number) ? number : 0;
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
