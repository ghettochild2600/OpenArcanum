using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Formats.Database;
using Arcanum.Formats.Script;
using Arcanum.Runtime;
using Arcanum.Script;
using UnityEditor;
using UnityEngine;

internal static class ScriptCompatibilityAudit
{
    private const string OutputPath = "Library/OpenArcanum/ScriptCompatibilityAudit.json";

    [Serializable]
    private sealed class AuditDocument
    {
        public int totalScripts;
        public int parseFailures;
        public int operationsDiscovered;
        public int fullySupportedScripts;
        public int partialScripts;
        public int blockedScripts;
        public int implementedOccurrences;
        public int partialOccurrences;
        public int stubOccurrences;
        public int unsupportedOccurrences;
        public List<OperationDocument> operations = new();
    }

    [Serializable]
    private sealed class OperationDocument
    {
        public string kind;
        public int opcode;
        public string sourceName;
        public int occurrenceCount;
        public int uniqueScripts;
        public List<string> representativeScripts = new();
        public string status;
        public string sourceImplementation;
        public string dependencies;
        public string risk;
        public string note;
    }

    private sealed class Count
    {
        public int Occurrences;
        public readonly SortedSet<string> Scripts = new(StringComparer.OrdinalIgnoreCase);
    }

    [MenuItem("OpenArcanum/Script Compatibility/Run Retail Opcode Audit", false, 1)]
    private static void Run()
    {
        using DatVirtualFileSystem vfs = MountSourceData();
        var counts = new Dictionary<(bool Action, int Opcode), Count>();
        var scriptOperations = new Dictionary<string, HashSet<(bool Action, int Opcode)>>(StringComparer.OrdinalIgnoreCase);
        int totalScripts = 0;
        int failures = 0;

        foreach (string path in vfs.EnumerateFiles("scr/")
                     .Where(value => value.EndsWith(".scr", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            ScriptFile script;
            try { script = ScriptReader.Read(vfs.ReadAllBytes(path)); }
            catch { failures++; continue; }
            totalScripts++;
            var used = new HashSet<(bool Action, int Opcode)>();
            foreach (ScriptCondition entry in script.Entries)
            {
                Add(false, entry.Type, path, counts, used);
                Add(true, entry.Action.Type, path, counts, used);
                Add(true, entry.Els.Type, path, counts, used);
            }
            scriptOperations[path] = used;
        }

        var result = new AuditDocument { totalScripts = totalScripts, parseFailures = failures };
        foreach (KeyValuePair<(bool Action, int Opcode), Count> pair in counts
                     .OrderByDescending(value => Severity(Profile(value.Key.Action, value.Key.Opcode).Status))
                     .ThenByDescending(value => value.Value.Occurrences)
                     .ThenBy(value => value.Key.Action)
                     .ThenBy(value => value.Key.Opcode))
        {
            ScriptOpcodeProfile profile = Profile(pair.Key.Action, pair.Key.Opcode);
            var row = new OperationDocument
            {
                kind = pair.Key.Action ? "action" : "condition",
                opcode = pair.Key.Opcode,
                sourceName = Name(pair.Key.Action, pair.Key.Opcode),
                occurrenceCount = pair.Value.Occurrences,
                uniqueScripts = pair.Value.Scripts.Count,
                representativeScripts = pair.Value.Scripts.Take(5).ToList(),
                status = profile.Status.ToString().ToLowerInvariant(),
                sourceImplementation = profile.SourceLocation,
                dependencies = profile.Dependency,
                risk = profile.Risk,
                note = profile.Note,
            };
            result.operations.Add(row);
            switch (profile.Status)
            {
                case ScriptOpcodeSupportStatus.Implemented: result.implementedOccurrences += row.occurrenceCount; break;
                case ScriptOpcodeSupportStatus.Partial: result.partialOccurrences += row.occurrenceCount; break;
                case ScriptOpcodeSupportStatus.Stub: result.stubOccurrences += row.occurrenceCount; break;
                case ScriptOpcodeSupportStatus.Unsupported: result.unsupportedOccurrences += row.occurrenceCount; break;
            }
        }
        result.operationsDiscovered = result.operations.Count;

        foreach (HashSet<(bool Action, int Opcode)> used in scriptOperations.Values)
        {
            ScriptOpcodeSupportStatus[] statuses = used.Select(value => Profile(value.Action, value.Opcode).Status).ToArray();
            if (statuses.Any(value => value is ScriptOpcodeSupportStatus.Stub or ScriptOpcodeSupportStatus.Unsupported))
                result.blockedScripts++;
            else if (statuses.Any(value => value == ScriptOpcodeSupportStatus.Partial))
                result.partialScripts++;
            else
                result.fullySupportedScripts++;
        }

        string directory = Path.GetDirectoryName(OutputPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(OutputPath, JsonUtility.ToJson(result, true));
        Debug.Log($"SCRIPT COMPATIBILITY AUDIT: scripts={result.totalScripts}; parseFailures={result.parseFailures}; " +
                  $"operations={result.operationsDiscovered}; fullySupported={result.fullySupportedScripts}; " +
                  $"partial={result.partialScripts}; blocked={result.blockedScripts}; implementedOccurrences={result.implementedOccurrences}; " +
                  $"partialOccurrences={result.partialOccurrences}; stubOccurrences={result.stubOccurrences}; " +
                  $"unsupportedOccurrences={result.unsupportedOccurrences}; output={Path.GetFullPath(OutputPath)}");
        foreach (OperationDocument row in result.operations
                     .Where(value => value.status is "unsupported" or "stub" or "partial")
                     .OrderByDescending(value => value.occurrenceCount).Take(20))
            Debug.Log($"SCRIPT COMPATIBILITY BLOCKER: {row.kind}={row.sourceName}({row.opcode}); " +
                      $"status={row.status}; occurrences={row.occurrenceCount}; scripts={row.uniqueScripts}; " +
                      $"dependency={row.dependencies}; examples=[{string.Join(",", row.representativeScripts)}]");
    }

    private static void Add(bool action, int opcode, string path,
        IDictionary<(bool Action, int Opcode), Count> counts,
        ISet<(bool Action, int Opcode)> used)
    {
        var key = (action, opcode);
        if (!counts.TryGetValue(key, out Count count)) counts[key] = count = new Count();
        count.Occurrences++;
        count.Scripts.Add(path);
        used.Add(key);
    }

    private static ScriptOpcodeProfile Profile(bool action, int opcode)
        => action ? ScriptOpcodeCompatibility.Action((Sat)opcode) : ScriptOpcodeCompatibility.Condition((Sct)opcode);

    private static string Name(bool action, int opcode)
        => action ? Enum.IsDefined(typeof(Sat), opcode) ? ((Sat)opcode).ToString() : $"UnknownAction{opcode}"
            : Enum.IsDefined(typeof(Sct), opcode) ? ((Sct)opcode).ToString() : $"UnknownCondition{opcode}";

    private static int Severity(ScriptOpcodeSupportStatus status)
        => status switch
        {
            ScriptOpcodeSupportStatus.Unsupported => 3,
            ScriptOpcodeSupportStatus.Stub => 2,
            ScriptOpcodeSupportStatus.Partial => 1,
            _ => 0,
        };

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
