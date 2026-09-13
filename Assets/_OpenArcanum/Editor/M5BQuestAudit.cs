using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Arcanum.Formats.Database;
using Arcanum.Formats.Dialog;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Quest;
using Arcanum.Formats.Script;
using Arcanum.Formats.Text;
using Arcanum.Runtime;
using UnityEditor;
using UnityEngine;

internal static class M5BQuestAudit
{
    private static readonly Regex QuestMutation = new(
        @"(?:^|[^A-Za-z])qu\s*(\d+)\s+([0-6])\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex QuestCondition = new(
        @"(?:^|[^A-Za-z])(qu|qa|qb)\s*(\d+)\s+([0-6])\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex DialogCode = new(
        @"(?:^|[\s,])([A-Za-z$]{2})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> MajorDeferredCodes = new(StringComparer.OrdinalIgnoreCase)
        { "co", "jo", "lv", "in", "$$", "mm", "nk", "rp", "ru", "fp" };

    [MenuItem("OpenArcanum/M5B/Run Quest 1130 Audit")]
    private static void RunQuest1130() => RunQuest(1130);

    [MenuItem("OpenArcanum/M5B/Run Quest 1040 Audit")]
    private static void RunQuest1040() => RunQuest(1040);

    [MenuItem("OpenArcanum/M5B/Run Quest 1081 Audit")]
    private static void RunQuest1081() => RunQuest(1081);

    [MenuItem("OpenArcanum/M5B/Run Quest 1136 Audit")]
    private static void RunQuest1136() => RunQuest(1136);

    [MenuItem("OpenArcanum/M5B/Run Quest 1005 Audit")]
    private static void RunQuest1005() => RunQuest(1005);

    [MenuItem("OpenArcanum/M5B/Resolve Quest 1005 Item Name")]
    private static void ResolveQuest1005ItemName()
    {
        const int sourceName = 2002;
        using DatVirtualFileSystem vfs = MountSourceData();
        string protoDirectory = GameDataLocator.FindDirectory("data/proto");
        var prototypes = new ProtoLibrary(protoDirectory);
        var matchingPrototypes = new List<int>();
        foreach (int number in prototypes.Numbers.OrderBy(value => value))
        {
            ObjectProtoInfo prototype = prototypes.Get(number);
            if (prototype?.NameIndex != sourceName) continue;
            matchingPrototypes.Add(number);
            Debug.Log($"M5B QUEST 1005 ITEM PROTOTYPE: name={sourceName}; proto={number}; " +
                      $"type={prototype.Type}; description={prototype.Description}; invAid={prototype.InvAid}");
        }

        int instances = 0;
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
            ObjectProtoInfo prototype = prototypes.Get(instance.PrototypeNumber);
            if ((instance.NameIndex ?? prototype?.NameIndex) != sourceName) continue;
            instances++;
            Debug.Log($"M5B QUEST 1005 ITEM INSTANCE: name={sourceName}; oid={instance.Identity}; " +
                      $"proto={instance.PrototypeNumber}; type={instance.Type}; parent={instance.ParentIdentity}; " +
                      $"source={path}");
        }
        Debug.Log($"M5B QUEST 1005 ITEM NAME PASS: name={sourceName}; " +
                  $"prototypes=[{string.Join(",", matchingPrototypes)}]; placedInstances={instances}");
    }

    [MenuItem("OpenArcanum/M5B/Run Quest 1119 Audit")]
    private static void RunQuest1119() => RunQuest(1119);

    [MenuItem("OpenArcanum/M5B/Run Quest 1137 Audit")]
    private static void RunQuest1137() => RunQuest(1137);

    private static void RunQuest(int questNumber)
    {
        var questToken = new Regex(
            $@"(?:^|[^A-Za-z])qu\s*{questNumber}\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        using DatVirtualFileSystem vfs = MountSourceData();
        QuestLog quests = QuestLog.FromMes(
            ReadMes(vfs, "mes/gamequestlog.mes"),
            ReadMes(vfs, "rules/xp_quest.mes"),
            ReadMes(vfs, "rules/gamequest.mes"),
            ReadMes(vfs, "mes/gamequestlogdumb.mes"));
        QuestInfo meta = quests.Meta(questNumber);
        if (meta == null) throw new InvalidDataException($"Quest {questNumber} has no source metadata.");

        Debug.Log($"M5B QUEST META: quest={questNumber}; xpLevel={meta.ExperienceLevel}; " +
                  $"xp={quests.QuestXp(questNumber)}; alignment={meta.AlignmentAdjustment}; " +
                  $"normalDialog=[{string.Join(",", meta.NormalDialog)}]; " +
                  $"badReactionDialog=[{string.Join(",", meta.BadReactionDialog)}]; " +
                  $"dumbDialog=[{string.Join(",", meta.DumbDialog)}]; " +
                  $"normalDescriptionLength={quests.Description(questNumber)?.Length ?? 0}; " +
                  $"dumbDescriptionLength={quests.Description(questNumber, true)?.Length ?? 0}");
        Debug.Log($"M5B QUEST JOURNAL: quest={questNumber}; normal={quests.Description(questNumber)}; " +
                  $"dumb={quests.Description(questNumber, true)}");

        var referencedDialogs = new SortedDictionary<int, DialogScript>();
        foreach (string path in vfs.EnumerateFiles("dlg/")
                     .Where(value => value.EndsWith(".dlg", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            DialogScript dialog;
            try { dialog = DlgReader.Read(vfs.ReadAllBytes(path)); }
            catch { continue; }
            if (!dialog.LineNumbers.Any(number => dialog.TryGet(number, out DialogLine line)
                                                  && (ReferencesQuest(line.Test, questToken)
                                                      || ReferencesQuest(line.Effect, questToken))))
                continue;
            int number = NumberFromPath(path);
            referencedDialogs[number] = dialog;
            Debug.Log($"M5B QUEST DIALOG RESOURCE: quest={questNumber}; dialog={number}; resource={path}");
            foreach (int lineNumber in dialog.LineNumbers)
            {
                if (!dialog.TryGet(lineNumber, out DialogLine line)) continue;
                Debug.Log($"M5B QUEST DIALOG: dialog={number}; line={line.Num}; " +
                          $"role={(line.IsNpcSpeech ? "npc" : "option")}; iq={line.Iq}; gender={line.OptionGender}; " +
                          $"token={(line.IsToken ? line.TokenCode.ToString() : "none")}; " +
                          $"test={Empty(line.Test)}; target={line.Target}; effect={Empty(line.Effect)}");
            }
        }

        var referencedScripts = new SortedSet<int>();
        foreach (string path in vfs.EnumerateFiles("scr/")
                     .Where(value => value.EndsWith(".scr", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            ScriptFile script;
            try { script = ScriptReader.Read(vfs.ReadAllBytes(path)); }
            catch { continue; }
            int number = NumberFromPath(path);
            bool references = false;
            for (int line = 0; line < script.Entries.Count; line++)
            {
                ScriptCondition condition = script.Entries[line];
                if (ReferencesQuest(condition, questNumber)
                    || ReferencesQuest(condition.Action, questNumber)
                    || ReferencesQuest(condition.Els, questNumber))
                    references = true;
            }
            if (!references && !referencedDialogs.ContainsKey(number)) continue;
            referencedScripts.Add(number);
            Debug.Log($"M5B QUEST SCRIPT RESOURCE: quest={questNumber}; script={number}; resource={path}; " +
                      $"directQuestReference={references}");
            for (int line = 0; line < script.Entries.Count; line++)
            {
                ScriptCondition condition = script.Entries[line];
                Debug.Log($"M5B QUEST SCRIPT: script={number}; line={line}; " +
                          $"condition={(Sct)condition.Type}{Operands(condition.OpType, condition.OpValue)}; " +
                          $"then={Action(condition.Action)}; else={Action(condition.Els)}");
            }
        }

        string protoDirectory = GameDataLocator.FindDirectory("data/proto");
        var prototypes = new ProtoLibrary(protoDirectory);
        int participants = 0;
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
            if (!referencedDialogs.ContainsKey(dialog)) continue;
            participants++;
            Debug.Log($"M5B QUEST NPC: quest={questNumber}; oid={instance.Identity}; proto={instance.PrototypeNumber}; " +
                      $"dialog={dialog}; sector={SectorPath(path, instance)}; source={path}");
        }

        Debug.Log($"M5B QUEST {questNumber} AUDIT PASS: dialogs={referencedDialogs.Count}; " +
                  $"scripts={referencedScripts.Count}; placedParticipants={participants}");
    }

    [MenuItem("OpenArcanum/M5B/Run Completion Candidate Audit")]
    private static void RunCompletionCandidates()
    {
        using DatVirtualFileSystem vfs = MountSourceData();
        QuestLog quests = QuestLog.FromMes(
            ReadMes(vfs, "mes/gamequestlog.mes"),
            ReadMes(vfs, "rules/xp_quest.mes"),
            ReadMes(vfs, "rules/gamequest.mes"),
            ReadMes(vfs, "mes/gamequestlogdumb.mes"));

        var candidates = new Dictionary<string, CompletionCandidate>(StringComparer.Ordinal);
        foreach (string path in vfs.EnumerateFiles("dlg/")
                     .Where(value => value.EndsWith(".dlg", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            DialogScript dialog;
            try { dialog = DlgReader.Read(vfs.ReadAllBytes(path)); }
            catch { continue; }
            int dialogNumber = NumberFromPath(path);
            foreach (int lineNumber in dialog.LineNumbers)
            {
                if (!dialog.TryGet(lineNumber, out DialogLine line) || line.IsNpcSpeech) continue;
                foreach (Match match in QuestMutation.Matches(line.Effect ?? string.Empty))
                {
                    if (!int.TryParse(match.Groups[1].Value, out int quest)
                        || !int.TryParse(match.Groups[2].Value, out int state)
                        || state != (int)QuestState.Completed)
                        continue;
                    string key = $"{dialogNumber}:{quest}";
                    if (!candidates.TryGetValue(key, out CompletionCandidate candidate))
                    {
                        candidate = new CompletionCandidate
                        {
                            DialogNumber = dialogNumber,
                            Quest = quest,
                            Resource = path,
                            Dialog = dialog,
                            LineCount = dialog.LineNumbers.Count(),
                        };
                        candidates.Add(key, candidate);
                    }
                    candidate.CompletionLines.Add(line.Num);
                }
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
            int dialogNumber = instance.DialogNum != 0 ? instance.DialogNum : prototype?.DialogNum ?? 0;
            foreach (CompletionCandidate candidate in candidates.Values
                         .Where(value => value.DialogNumber == dialogNumber))
                candidate.Participants.Add($"oid={instance.Identity};proto={instance.PrototypeNumber};" +
                                           $"sector={SectorPath(path, instance)};source={path}");
        }

        foreach (CompletionCandidate candidate in candidates.Values)
        {
            foreach (int lineNumber in candidate.Dialog.LineNumbers)
            {
                if (!candidate.Dialog.TryGet(lineNumber, out DialogLine line)) continue;
                foreach (Match match in DialogCode.Matches(line.Test ?? string.Empty))
                    candidate.TestCodes.Add(match.Groups[1].Value.ToLowerInvariant());
                foreach (Match match in DialogCode.Matches(line.Effect ?? string.Empty))
                    candidate.EffectCodes.Add(match.Groups[1].Value.ToLowerInvariant());
                foreach (Match match in QuestCondition.Matches(line.Test ?? string.Empty))
                {
                    if (!int.TryParse(match.Groups[2].Value, out int quest) || quest != candidate.Quest
                        || !int.TryParse(match.Groups[3].Value, out int state))
                        continue;
                    string code = match.Groups[1].Value.ToLowerInvariant();
                    if (code == "qu" && state == (int)QuestState.Completed)
                        candidate.HasExactCompletedTest = true;
                    if (code == "qu" && state == (int)QuestState.Accepted)
                        candidate.HasExactAcceptedTest = true;
                }
            }
            candidate.MajorCodes.UnionWith(candidate.TestCodes.Where(MajorDeferredCodes.Contains));
            candidate.MajorCodes.UnionWith(candidate.EffectCodes.Where(MajorDeferredCodes.Contains));
            QuestInfo meta = quests.Meta(candidate.Quest);
            candidate.Xp = quests.QuestXp(candidate.Quest);
            candidate.Alignment = meta?.AlignmentAdjustment ?? 0;
            candidate.HasJournal = !string.IsNullOrEmpty(quests.Description(candidate.Quest));
        }

        List<CompletionCandidate> ordered = candidates.Values
            .Where(candidate => candidate.Participants.Count > 0)
            .OrderByDescending(candidate => candidate.HasExactCompletedTest)
            .ThenByDescending(candidate => candidate.HasExactAcceptedTest)
            .ThenBy(candidate => candidate.MajorCodes.Count)
            .ThenBy(candidate => candidate.LineCount)
            .ThenBy(candidate => candidate.Quest)
            .ToList();
        Debug.Log($"M5B COMPLETION CANDIDATE SUMMARY: total={candidates.Count}; placed={ordered.Count}; " +
                  $"terminalSpecific={ordered.Count(value => value.HasExactCompletedTest)}; " +
                  $"noMajorCodes={ordered.Count(value => value.MajorCodes.Count == 0)}");
        foreach (CompletionCandidate candidate in ordered.Take(50))
        {
            Debug.Log($"M5B COMPLETION CANDIDATE: quest={candidate.Quest}; dialog={candidate.DialogNumber}; " +
                      $"resource={candidate.Resource}; completionLines=[{string.Join(",", candidate.CompletionLines)}]; " +
                      $"lines={candidate.LineCount}; acceptedTest={candidate.HasExactAcceptedTest}; " +
                      $"completedTest={candidate.HasExactCompletedTest}; tests=[{string.Join(",", candidate.TestCodes)}]; " +
                      $"effects=[{string.Join(",", candidate.EffectCodes)}]; major=[{string.Join(",", candidate.MajorCodes)}]; " +
                      $"xp={candidate.Xp}; alignment={candidate.Alignment}; journal={candidate.HasJournal}; " +
                      $"participants={candidate.Participants.Count}; first={candidate.Participants[0]}");
            foreach (int lineNumber in candidate.Dialog.LineNumbers)
            {
                if (!candidate.Dialog.TryGet(lineNumber, out DialogLine line)) continue;
                if (!QuestMutation.IsMatch(line.Effect ?? string.Empty)
                    && !QuestCondition.IsMatch(line.Test ?? string.Empty))
                    continue;
                Debug.Log($"M5B COMPLETION LINE: quest={candidate.Quest}; dialog={candidate.DialogNumber}; " +
                          $"line={line.Num}; role={(line.IsNpcSpeech ? "npc" : "option")}; " +
                          $"test={Empty(line.Test)}; target={line.Target}; effect={Empty(line.Effect)}");
            }
        }
        Debug.Log("M5B COMPLETION CANDIDATE AUDIT PASS");
    }

    private sealed class CompletionCandidate
    {
        public int DialogNumber;
        public int Quest;
        public string Resource;
        public DialogScript Dialog;
        public int LineCount;
        public readonly List<int> CompletionLines = new();
        public readonly List<string> Participants = new();
        public readonly SortedSet<string> TestCodes = new(StringComparer.OrdinalIgnoreCase);
        public readonly SortedSet<string> EffectCodes = new(StringComparer.OrdinalIgnoreCase);
        public readonly SortedSet<string> MajorCodes = new(StringComparer.OrdinalIgnoreCase);
        public bool HasExactAcceptedTest;
        public bool HasExactCompletedTest;
        public bool HasJournal;
        public int Xp;
        public int Alignment;
    }

    private static bool ReferencesQuest(string value, Regex questToken)
        => !string.IsNullOrWhiteSpace(value) && questToken.IsMatch(value);

    private static bool ReferencesQuest(ScriptCondition condition, int questNumber)
    {
        Sct type = (Sct)condition.Type;
        return type switch
        {
            Sct.PcQuestState => IsNumber(condition.OpType[1], condition.OpValue[1], questNumber),
            Sct.GlobalQuestState => IsNumber(condition.OpType[0], condition.OpValue[0], questNumber),
            _ => false,
        };
    }

    private static bool ReferencesQuest(ScriptAction action, int questNumber)
    {
        if (action == null) return false;
        Sat type = (Sat)action.Type;
        return type switch
        {
            Sat.SetPcQuestState => IsNumber(action.OpType[1], action.OpValue[1], questNumber),
            Sat.SetQuestGlobalState => IsNumber(action.OpType[0], action.OpValue[0], questNumber),
            _ => false,
        };
    }

    private static bool IsNumber(byte type, int value, int expected)
        => type == (byte)Svt.Number && value == expected;

    private static MesFile ReadMes(DatVirtualFileSystem vfs, string path)
        => vfs.Exists(path) ? MesReader.Read(vfs.ReadAllBytes(path)) : null;

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
