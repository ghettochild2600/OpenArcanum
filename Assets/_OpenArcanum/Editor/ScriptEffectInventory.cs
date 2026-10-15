using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Formats.Database;
using Arcanum.Formats.Script;
using Arcanum.Formats.Text;
using Arcanum.Runtime;
using Arcanum.Runtime.World;
using Arcanum.Script;
using UnityEditor;
using UnityEngine;

internal static class ScriptEffectInventory
{
    private const string OutputPath = "Library/OpenArcanum/ScriptEffectInventory.json";

    [Serializable]
    private sealed class Document
    {
        public int addOccurrences;
        public int removeOccurrences;
        public int uniqueEffects;
        public string permanence = "Persistent critter effect arrays; saved with the owning character.";
        public string duration = "No intrinsic duration; effects remain until a matching RemoveEffect or object destruction.";
        public string stacking = "Duplicate effect IDs append and stack through the shared source operator pipeline.";
        public string removal = "RemoveEffect removes the first matching effect instance and its paired cause.";
        public List<Row> effects = new();
    }

    [Serializable]
    private sealed class Row
    {
        public int effectId;
        public int addOccurrences;
        public int removeOccurrences;
        public List<int> causes = new();
        public List<string> targetOperands = new();
        public List<string> scripts = new();
        public string sourceDefinition;
        public string currentSupport;
        public List<string> supportedChanges = new();
        public List<string> unsupportedChanges = new();
    }

    private sealed class Mutable
    {
        public int Adds;
        public int Removes;
        public readonly SortedSet<int> Causes = new();
        public readonly SortedSet<string> Targets = new(StringComparer.Ordinal);
        public readonly SortedSet<string> Scripts = new(StringComparer.OrdinalIgnoreCase);
    }

    [MenuItem("OpenArcanum/Script Compatibility/Run Retail Effect Inventory", false, 2)]
    private static void Run()
    {
        using DatVirtualFileSystem vfs = MountSourceData();
        MesFile engine = MesReader.Read(vfs.ReadAllBytes("rules/effect.mes"));
        MesFile module = vfs.Exists("rules/gameeffect.mes")
            ? MesReader.Read(vfs.ReadAllBytes("rules/gameeffect.mes")) : null;
        RetailScriptEffectCatalog catalog = RetailScriptEffectCatalog.FromMes(engine, module);
        var values = new SortedDictionary<int, Mutable>();
        foreach (string path in vfs.EnumerateFiles("scr/")
                     .Where(value => value.EndsWith(".scr", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            ScriptFile script;
            try { script = ScriptReader.Read(vfs.ReadAllBytes(path)); }
            catch { continue; }
            foreach (ScriptAction action in script.Entries.SelectMany(entry => new[] { entry.Action, entry.Els }))
            {
                if (action == null || action.Type is not ((int)Sat.AddEffect) and not ((int)Sat.RemoveEffect))
                    continue;
                int effect = action.OpType[1] == (byte)Svt.Number ? action.OpValue[1] : -1;
                if (!values.TryGetValue(effect, out Mutable value)) values.Add(effect, value = new Mutable());
                if (action.Type == (int)Sat.AddEffect)
                {
                    value.Adds++;
                    if (action.OpType[2] == (byte)Svt.Number) value.Causes.Add(action.OpValue[2]);
                }
                else value.Removes++;
                value.Targets.Add(ObjectOperand(action.OpType[0], action.OpValue[0]));
                value.Scripts.Add(path);
            }
        }

        var document = new Document();
        foreach (KeyValuePair<int, Mutable> pair in values)
        {
            Mutable value = pair.Value;
            document.addOccurrences += value.Adds;
            document.removeOccurrences += value.Removes;
            RetailScriptEffectDefinition definition = pair.Key >= 0 && catalog.TryGet(pair.Key, out var parsed)
                ? parsed : null;
            document.effects.Add(new Row
            {
                effectId = pair.Key,
                addOccurrences = value.Adds,
                removeOccurrences = value.Removes,
                causes = value.Causes.ToList(),
                targetOperands = value.Targets.ToList(),
                scripts = value.Scripts.ToList(),
                sourceDefinition = pair.Key < 0 ? "<dynamic>"
                    : (pair.Key < 50 ? module?.Get(pair.Key) : engine.Get(pair.Key)) ?? "<missing>",
                currentSupport = pair.Key < 0 ? "runtime-resolved"
                    : definition == null ? "unsupported"
                    : definition.IsFullySupported ? "complete" : "partial",
                supportedChanges = definition?.Changes.Select(change =>
                    $"{change.SourceSymbol} {change.Operation} {change.Amount}").ToList() ?? new List<string>(),
                unsupportedChanges = definition?.UnsupportedChanges.ToList() ?? new List<string>(),
            });
        }
        document.uniqueEffects = document.effects.Count;
        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath)!);
        File.WriteAllText(OutputPath, JsonUtility.ToJson(document, true));
        Debug.Log($"SCRIPT EFFECT INVENTORY: effects={document.uniqueEffects}; adds={document.addOccurrences}; " +
                  $"removes={document.removeOccurrences}; output={Path.GetFullPath(OutputPath)}");
    }

    private static string ObjectOperand(byte type, int value)
        => Enum.IsDefined(typeof(Sfo), (int)type) ? $"{(Sfo)type}:{value}" : $"Unknown:{type}:{value}";

    private static DatVirtualFileSystem MountSourceData()
    {
        var vfs = new DatVirtualFileSystem();
        string module = GameDataLocator.Find("modules/Arcanum.dat");
        if (string.IsNullOrEmpty(module)) throw new FileNotFoundException("modules/Arcanum.dat");
        vfs.MountFile(module);
        foreach (string archive in new[] { "arcanum1.dat", "arcanum2.dat", "arcanum3.dat", "arcanum4.dat" })
        {
            string path = GameDataLocator.Find(archive);
            if (!string.IsNullOrEmpty(path)) vfs.MountFile(path);
        }
        return vfs;
    }
}
