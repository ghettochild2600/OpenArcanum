using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Arcanum.Formats.Database;
using Arcanum.Formats.Dialog;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Script;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using Arcanum.Runtime;
using UnityEditor;
using UnityEngine;

internal static class M7CAreaDiscoveryAudit
{
    private static readonly Regex MarkArea = new(@"(?i)(?:^|[^a-z])mm\s*(-?\d+)", RegexOptions.Compiled);
    private static readonly Regex AreaTest = new(@"(?i)(?:^|[^a-z])(?:ar|ia)\s*(-?\d+)", RegexOptions.Compiled);

    [MenuItem("OpenArcanum/M7C/Run Area Discovery Source Audit")]
    private static void Run()
    {
        using DatVirtualFileSystem vfs = MountSourceData();
        AreaList areas = AreaList.FromMes(MesReader.Read(vfs.ReadAllBytes("mes/gamearea.mes")));
        var dialogMarks = new List<DialogMark>();
        var scriptMarks = new List<ScriptMark>();
        var placed = new Dictionary<int, List<PlacedNpc>>();

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
                if (!dialog.TryGet(lineNumber, out DialogLine line)) continue;
                foreach (Match match in MarkArea.Matches(line.Effect ?? string.Empty))
                    if (int.TryParse(match.Groups[1].Value, out int area))
                        dialogMarks.Add(new DialogMark(number, path, line, area));
            }
        }

        foreach (string path in vfs.EnumerateFiles("scr/")
                     .Where(value => value.EndsWith(".scr", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            ScriptFile script;
            try { script = ScriptReader.Read(vfs.ReadAllBytes(path)); }
            catch { continue; }
            int number = NumberFromPath(path);
            for (int line = 0; line < script.Entries.Count; line++)
            {
                ScriptCondition condition = script.Entries[line];
                AddScriptMark(scriptMarks, number, path, line, condition, condition.Action, false);
                AddScriptMark(scriptMarks, number, path, line, condition, condition.Els, true);
            }
        }

        string protoDirectory = GameDataLocator.FindDirectory("data/proto");
        var prototypes = new ProtoLibrary(protoDirectory);
        var relevantDialogs = dialogMarks.Select(value => value.Dialog).ToHashSet();
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
            if (!relevantDialogs.Contains(dialog)) continue;
            if (!placed.TryGetValue(dialog, out List<PlacedNpc> values))
                placed.Add(dialog, values = new List<PlacedNpc>());
            values.Add(new PlacedNpc(instance.Identity.ToString(), instance.PrototypeNumber,
                SectorPath(path, instance), path));
        }

        int invalidDialogAreas = dialogMarks.Count(value => !areas.TryGet(value.Area, out _));
        int invalidScriptAreas = scriptMarks.Count(value => value.AreaType == (byte)Svt.Number
                                                             && !areas.TryGet(value.Area, out _));
        Debug.Log($"M7C AUDIT SUMMARY: areas={areas.Count}; dialogMarks={dialogMarks.Count}; "
                  + $"dialogResources={dialogMarks.Select(value => value.Dialog).Distinct().Count()}; "
                  + $"placedDialogNpcs={placed.Values.Sum(value => value.Count)}; scriptMarks={scriptMarks.Count}; "
                  + $"invalidDialogAreas={invalidDialogAreas}; invalidConstantScriptAreas={invalidScriptAreas}");
        foreach (Area area in areas.Areas.OrderBy(value => value.Id))
            Debug.Log($"M7C AREA: id={area.Id}; name={area.Name}; tile=({area.TileX},{area.TileY}); "
                      + $"labelOffset=({area.LabelXOff},{area.LabelYOff}); radiusTiles={area.RadiusTiles}; "
                      + $"description={area.Description}");
        foreach (DialogMark mark in dialogMarks.OrderBy(value => value.Area).ThenBy(value => value.Dialog)
                     .ThenBy(value => value.Line.Num))
        {
            areas.TryGet(mark.Area, out Area area);
            string tests = string.Join("|", AreaTest.Matches(mark.Line.Test ?? string.Empty)
                .Cast<Match>().Select(value => value.Value.Trim()));
            Debug.Log($"M7C DIALOG MARK: area={mark.Area}; areaName={area.Name}; dialog={mark.Dialog}; "
                      + $"line={mark.Line.Num}; target={mark.Line.Target}; test={Empty(mark.Line.Test)}; "
                      + $"areaTests={Empty(tests)}; effect={mark.Line.Effect}; resource={mark.Resource}; "
                      + $"placedNpcs={(placed.TryGetValue(mark.Dialog, out List<PlacedNpc> values) ? values.Count : 0)}");
            if (placed.TryGetValue(mark.Dialog, out values))
                foreach (PlacedNpc npc in values)
                    Debug.Log($"M7C DIALOG NPC: area={mark.Area}; dialog={mark.Dialog}; oid={npc.Identity}; "
                              + $"proto={npc.Prototype}; sector={npc.Sector}; source={npc.Source}");
        }
        foreach (ScriptMark mark in scriptMarks.OrderBy(value => value.Area).ThenBy(value => value.Script)
                     .ThenBy(value => value.Line))
        {
            areas.TryGet(mark.Area, out Area area);
            Debug.Log($"M7C SCRIPT MARK: area={mark.Area}; areaName={area.Name}; script={mark.Script}; "
                      + $"line={mark.Line}; else={mark.Else}; condition={(Sct)mark.Condition}; "
                      + $"areaType={mark.AreaType}; pcType={mark.PcType}; pcValue={mark.PcValue}; resource={mark.Resource}");
        }
        Debug.Log("M7C AREA DISCOVERY SOURCE AUDIT PASS");
    }

    [MenuItem("OpenArcanum/M7C/Run Discovery Candidate Detail Audit")]
    private static void RunCandidateDetails()
    {
        using DatVirtualFileSystem vfs = MountSourceData();
        foreach (int number in new[] { 1000, 1041, 1077, 1497, 2789 })
        {
            ScriptFile script = ScriptReader.Read(vfs.ReadAllBytes(vfs.EnumerateFiles("scr/")
                .Single(path => NumberFromPath(path) == number)));
            string dialogPath = vfs.EnumerateFiles("dlg/").Single(path => NumberFromPath(path) == number);
            DialogScript dialog = DlgReader.Read(vfs.ReadAllBytes(dialogPath));
            Debug.Log($"M7C DETAIL START: number={number}; scriptEntries={script.Entries.Count}; dialog={dialogPath}");
            for (int line = 0; line < script.Entries.Count; line++)
            {
                ScriptCondition entry = script.Entries[line];
                Debug.Log($"M7C DETAIL SCRIPT: number={number}; line={line}; condition={(Sct)entry.Type}; "
                          + $"conditionOps={Operands(entry.OpType, entry.OpValue)}; "
                          + $"action={Action(entry.Action)}; else={Action(entry.Els)}");
            }
            foreach (int lineNumber in dialog.LineNumbers)
            {
                dialog.TryGet(lineNumber, out DialogLine line);
                Debug.Log($"M7C DETAIL DIALOG: number={number}; line={line.Num}; npc={line.IsNpcSpeech}; "
                          + $"text={line.Text}; text2={line.Text2}; iq={line.Iq}; test={Empty(line.Test)}; "
                          + $"target={line.Target}; effect={Empty(line.Effect)}");
            }
        }
        Debug.Log("M7C DISCOVERY CANDIDATE DETAIL AUDIT PASS");
    }

    private static void AddScriptMark(List<ScriptMark> values, int number, string path, int line,
        ScriptCondition condition, ScriptAction action, bool isElse)
    {
        if (action?.Type != (int)Sat.MarkMapLocation) return;
        values.Add(new ScriptMark(number, path, line, isElse, condition.Type,
            action.OpType[0], action.OpValue[0], action.OpType[1], action.OpValue[1]));
    }

    private static string Action(ScriptAction action)
        => action == null ? "<null>" : $"{(Sat)action.Type}[{Operands(action.OpType, action.OpValue)}]";
    private static string Operands(byte[] types, int[] values)
        => string.Join("|", Enumerable.Range(0, 4).Select(index => $"{types[index]}:{values[index]}"));

    private readonly struct DialogMark
    {
        public readonly int Dialog;
        public readonly string Resource;
        public readonly DialogLine Line;
        public readonly int Area;
        public DialogMark(int dialog, string resource, DialogLine line, int area)
        { Dialog = dialog; Resource = resource; Line = line; Area = area; }
    }
    private readonly struct ScriptMark
    {
        public readonly int Script;
        public readonly string Resource;
        public readonly int Line;
        public readonly bool Else;
        public readonly int Condition;
        public readonly byte AreaType;
        public readonly int Area;
        public readonly byte PcType;
        public readonly int PcValue;
        public ScriptMark(int script, string resource, int line, bool isElse, int condition,
            byte areaType, int area, byte pcType, int pcValue)
        {
            Script = script; Resource = resource; Line = line; Else = isElse; Condition = condition;
            AreaType = areaType; Area = area; PcType = pcType; PcValue = pcValue;
        }
    }
    private readonly struct PlacedNpc
    {
        public readonly string Identity;
        public readonly int Prototype;
        public readonly string Sector;
        public readonly string Source;
        public PlacedNpc(string identity, int prototype, string sector, string source)
        { Identity = identity; Prototype = prototype; Sector = sector; Source = source; }
    }

    private static string Empty(string value) => string.IsNullOrWhiteSpace(value) ? "<none>" : value;
    private static int NumberFromPath(string path)
    {
        int start = path.LastIndexOf('/') + 1;
        return start + 5 <= path.Length && int.TryParse(path.Substring(start, 5), out int value) ? value : 0;
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
