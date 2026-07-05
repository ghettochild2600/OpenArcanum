using System.Collections.Generic;
using System.Text;
using Arcanum.Formats.Database;
using Arcanum.Formats.Dialog;
using Arcanum.Formats.Script;
using Arcanum.Formats.Text;
using Arcanum.Script;
using UnityEngine;

namespace Arcanum.Runtime.Demo
{
    /// <summary>
    /// Standalone dialog + script test bench — drop on a GameObject in an empty scene and press Play (reads
    /// your own Arcanum install via <see cref="GameDataLocator"/>, bundles nothing). It wires the REAL
    /// <c>Arcanum.Formats.Dialog</c> + <c>Arcanum.Script</c> assemblies with NO world dependency, so it also
    /// doubles as the public-repo smoke test for those layers.
    ///
    /// Enter an NPC's dialog number (e.g. Virgil = 1324) and press <b>Talk</b>: it runs that NPC's
    /// <c>SAP_DIALOG</c> script through the real VM, which decides the opening dialog line via its
    /// <c>SAT_DIALOG</c> action — exactly the engine flow — then opens the conversation. Left panel: the live
    /// PC/NPC state every <c>.dlg</c> test reads and effect mutates, all editable (IQ, gender, race, gold,
    /// alignment, reaction, met-before, following, quest/flag toggles). Change a value and the option list
    /// re-filters instantly — that's the point: you SEE the gates. Right panel: the conversation + a running
    /// log of which conditions the SAP_DIALOG script evaluated and which effects each pick fired. <b>Decode</b>
    /// dumps the SAP_DIALOG script's entries in readable form so you can trace the chain by eye.
    /// </summary>
    public sealed class DialogScriptGallery : MonoBehaviour
    {
        [SerializeField] private string[] Archives = { "arcanum1.dat", "arcanum2.dat", "arcanum3.dat", "arcanum4.dat" };
        [Tooltip("Dialog number to open on Play (Virgil = 1324).")]
        [SerializeField] private int StartDialogNum = 1324;

        private DatVirtualFileSystem _vfs;
        private ScriptDatabase _scripts;
        private MesFile _gdMale, _gdFemale;

        private Harness _harness;
        private DialogConversation _convo;
        private string _dialogField = "1324";
        private string _npcName = "";
        private int _resumeScriptLine = -1, _scriptNum;
        private readonly List<string> _log = new List<string>();
        private Vector2 _stateScroll, _convoScroll, _logScroll;
        private string _decode;
        private Vector2 _decodeScroll;

        private void Start()
        {
            _vfs = new DatVirtualFileSystem();
            int mounted = 0;
            foreach (string a in Archives)
            {
                string p = GameDataLocator.Find(a);
                if (!string.IsNullOrEmpty(p)) { _vfs.MountFile(p); mounted++; }
            }
            if (mounted == 0) { Debug.LogError("DialogScriptGallery: no archives found — point the data locator at your Arcanum install."); return; }

            _scripts = ScriptDatabase.Load(_vfs);
            _gdMale = ReadMes("mes/gd_pc2m.mes");
            _gdFemale = ReadMes("mes/gd_pc2f.mes");
            ScriptVm.Log = s => Log(s); // the VM assembly is engine-free — route its warnings into our log
            Debug.Log($"DialogScriptGallery: {_scripts.Count} scripts loaded ({_scripts.FailedCount} failed).");

            _harness = new Harness(Log);
            _dialogField = StartDialogNum.ToString();
            Talk(StartDialogNum);
            ConfigureCamera();
        }

        private MesFile ReadMes(string path) => _vfs.Exists(path) ? MesReader.Read(_vfs.ReadAllBytes(path)) : null;

        /// <summary>The archive names this bench mounts — used by the editor Dialog Browser to enumerate the
        /// same install without a second config.</summary>
        public string[] ArchiveNames => Archives;

        /// <summary>The dialog number currently open (for the browser's "◀ current" marker).</summary>
        public int CurrentDialog => _scriptNum;

        /// <summary>Open a dialog by number at runtime — the editor Dialog Browser calls this. Also updates the
        /// text field so the in-scene UI stays in sync.</summary>
        public void OpenDialog(int dialogNum)
        {
            if (_harness == null) return; // not mounted yet (called before Start)
            _dialogField = dialogNum.ToString();
            Talk(dialogNum);
        }

        // Open a conversation the engine way: run the NPC's SAP_DIALOG script at line 0; its SAT_DIALOG picks
        // the opening line (falling back to line 1 if there's no script or it opens nothing).
        private void Talk(int dialogNum)
        {
            _log.Clear();
            _convo = null;
            _scriptNum = dialogNum;
            _harness.ResetTranscript();
            _npcName = NameFromPath(DialogLocator.FindPath(_vfs, dialogNum)) ?? $"#{dialogNum}";

            _harness.RequestedDialogLine = -1;
            ScriptFile script = _scripts?.Get(dialogNum);
            if (script != null)
            {
                Log($"— running SAP_DIALOG script {dialogNum} @ line 0 —");
                RunDialogScript(script, dialogNum, 0);
            }
            int line = _harness.RequestedDialogLine > 0 ? _harness.RequestedDialogLine : 1;
            if (_harness.RequestedDialogLine <= 0) Log($"(no SAT_DIALOG fired → opening at line 1)");
            OpenConversation(dialogNum, line);
        }

        private void RunDialogScript(ScriptFile file, int scriptNum, int line)
        {
            var vm = new ScriptVm(_harness, _harness.Globals);
            var ctx = new ScriptContext { Triggerer = _harness.Pc, Attachee = _harness.Npc, AttachmentPoint = (int)Sap.Dialog, ScriptNum = scriptNum };
            vm.Execute(file, ctx, line);
        }

        private void OpenConversation(int dialogNum, int startLine)
        {
            DialogScript dlg = DialogLocator.Load(_vfs, dialogNum);
            if (dlg == null) { Log($"dialog {dialogNum} not found."); _convo = null; return; }

            MesFile gd = _harness.NpcIsMale ? _gdMale : _gdFemale; // gd file chosen by NPC gender (dialog.c)
            _harness.Generated = gd != null ? new GeneratedDialogText(gd) : null;
            _resumeScriptLine = 0; // a script opened us → resume it after the conversation
            _convo = new DialogConversation(dlg, startLine, _harness.Intelligence, _harness);
            _harness.InConversation = true;
            _optionSig = int.MinValue; // force the option cache to recompute for the new node
            Log($"opened '{_npcName}' at line {startLine}  (\"{Trunc(_convo.NpcText)}\")");
        }

        private void Pick(DialogLine opt)
        {
            Log($"» {Trunc(_convo.OptionText(opt))}   {(string.IsNullOrEmpty(opt.Effect) ? "" : $"[effect: {opt.Effect}]")}");
            bool more = _convo.Pick(opt);
            if (more) { Log($"→ line {_convo.CurrentLine}: \"{Trunc(_convo.NpcText)}\""); return; }

            int end = _convo.EndScriptLine;
            _convo = null;
            _harness.InConversation = false;
            Log($"— conversation ended (EndScriptLine {end}) —");
            // The engine resumes the SAP_DIALOG script (response 0 → scriptLine+1, negative → that line).
            if (_resumeScriptLine >= 0 && end >= 0)
            {
                ScriptFile script = _scripts?.Get(_scriptNum);
                if (script != null)
                {
                    int resume = end == 0 ? _resumeScriptLine + 1 : end;
                    Log($"— resuming SAP_DIALOG script @ line {resume} —");
                    _harness.RequestedDialogLine = -1;
                    RunDialogScript(script, _scriptNum, resume);
                    if (_harness.RequestedDialogLine > 0) OpenConversation(_scriptNum, _harness.RequestedDialogLine);
                }
            }
        }

        // The option list is recomputed only when the current line OR the editable state changes — NOT every
        // frame. Token options (e:/y:/s:…) pick a RANDOM generated line each time Options() runs, so calling it
        // per repaint would re-roll their text every frame (the flicker); caching pins the text for a node.
        private List<DialogLine> _optionCache = new List<DialogLine>();
        private int _optionSig = int.MinValue;

        private List<DialogLine> CurrentOptions()
        {
            int sig = OptionSignature();
            if (sig != _optionSig) { _optionCache = _convo.Options(); _optionSig = sig; }
            return _optionCache;
        }

        // A cheap signature of everything that affects which options show (line + the edited PC/NPC state +
        // a revision bumped by quest/flag effects). Changes ⇒ recompute (and re-pick token text, which is what
        // we want when the state genuinely changed); unchanged ⇒ reuse the cached list, so nothing flickers.
        private int OptionSignature()
        {
            Harness h = _harness;
            unchecked
            {
                int s = _convo?.CurrentLine ?? -1;
                s = s * 31 + h.IntelligenceValue;
                s = s * 31 + (h.PcMale ? 1 : 0);
                s = s * 31 + h.PcRaceValue;
                s = s * 31 + h.GoldValue;
                s = s * 31 + h.AlignmentValue;
                s = s * 31 + h.ReactionValue;
                s = s * 31 + h.PersuasionValue;
                s = s * 31 + (h.Met ? 1 : 0);
                s = s * 31 + (h.Following ? 1 : 0);
                s = s * 31 + h.StoryStateValue;
                s = s * 31 + (h.NpcMale ? 1 : 0);
                s = s * 31 + h.Revision; // quest/flag effects bump this
                return s;
            }
        }

        private void Log(string s) { _log.Add(s); if (_log.Count > 200) _log.RemoveAt(0); }

        private static string Trunc(string s, int n = 80)
            => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n) + "…");

        private static string NameFromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string f = System.IO.Path.GetFileNameWithoutExtension(path);
            int i = 0; while (i < f.Length && char.IsDigit(f[i])) i++;
            return i < f.Length ? f.Substring(i) : f;
        }

        // ── IMGUI ────────────────────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (_harness == null) { GUILayout.Label("  (no data mounted — see console)"); return; }
            GUILayout.BeginHorizontal();
            DrawStatePanel();
            DrawConversationPanel();
            GUILayout.EndHorizontal();
        }

        private void DrawStatePanel()
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(340), GUILayout.Height(Screen.height - 10));
            GUILayout.Label("<b>Dialog / Script test bench</b>", Rich());

            GUILayout.BeginHorizontal();
            GUILayout.Label("Dialog #", GUILayout.Width(60));
            _dialogField = GUILayout.TextField(_dialogField, GUILayout.Width(80));
            if (GUILayout.Button("Talk", GUILayout.Width(60)) && int.TryParse(_dialogField, out int dn)) Talk(dn);
            if (GUILayout.Button("Decode", GUILayout.Width(70)) && int.TryParse(_dialogField, out int dn2)) _decode = DecodeScript(dn2);
            GUILayout.EndHorizontal();

            _stateScroll = GUILayout.BeginScrollView(_stateScroll);
            Harness h = _harness;

            GUILayout.Label("<b>Player</b>", Rich());
            h.IntelligenceValue = IntField("Intelligence (IQ gate)", h.IntelligenceValue);
            h.PcMale = GUILayout.Toggle(h.PcMale, " PC is male (gender gate / speech variant)");
            h.PcRaceValue = IntField("Race (ra gate: 0=human…)", h.PcRaceValue);
            h.GoldValue = IntField("Gold ($$)", h.GoldValue);
            h.AlignmentValue = IntField("Alignment (al/na)", h.AlignmentValue);
            h.PersuasionValue = IntField("Persuasion (ps)", h.PersuasionValue);

            GUILayout.Space(6);
            GUILayout.Label("<b>NPC</b>", Rich());
            h.ReactionValue = IntField("Reaction (re)", h.ReactionValue);
            GUILayout.BeginHorizontal();
            h.Met = GUILayout.Toggle(h.Met, " met before (me)", GUILayout.Width(150));
            h.Following = GUILayout.Toggle(h.Following, " following (fo)", GUILayout.Width(150));
            GUILayout.EndHorizontal();
            GUILayout.Label("NPC gender selects gd_pc2m/f + speech variant:", Rich());
            h.NpcMale = GUILayout.Toggle(h.NpcMale, " NPC is male");

            GUILayout.Space(6);
            GUILayout.Label("<b>Quests / flags</b>  <i>(set by dialog qu/gf/pf)</i>", Rich());
            GUILayout.BeginHorizontal();
            _questField = GUILayout.TextField(_questField, GUILayout.Width(50));
            _questState = GUILayout.TextField(_questState, GUILayout.Width(40));
            if (GUILayout.Button("set quest", GUILayout.Width(90)) &&
                int.TryParse(_questField, out int q) && int.TryParse(_questState, out int st))
                h.SetQuest(q, st);
            GUILayout.EndHorizontal();
            foreach (KeyValuePair<int, int> kv in h.QuestSnapshot()) GUILayout.Label($"   quest {kv.Key} = {kv.Value}");
            foreach (string f in h.FlagsSnapshot()) GUILayout.Label($"   {f} set");
            if (h.StoryState != 0) GUILayout.Label($"   story state = {h.StoryState}");

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private string _questField = "0", _questState = "1";

        private void DrawConversationPanel()
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(Screen.width - 360), GUILayout.Height(Screen.height - 10));

            if (_decode != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>Decoded SAP_DIALOG script</b>", Rich());
                if (GUILayout.Button("close", GUILayout.Width(60))) _decode = null;
                GUILayout.EndHorizontal();
                _decodeScroll = GUILayout.BeginScrollView(_decodeScroll);
                GUILayout.Label(_decode, Mono());
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
                return;
            }

            GUILayout.Label($"<b>{_npcName}</b>", Rich());
            _convoScroll = GUILayout.BeginScrollView(_convoScroll, GUILayout.Height(Screen.height * 0.5f));
            if (_convo != null)
            {
                GUILayout.Label($"<i>{_convo.NpcText}</i>", Rich());
                GUILayout.Space(6);
                List<DialogLine> options = CurrentOptions();
                for (int i = 0; i < options.Count; i++)
                {
                    DialogLine opt = options[i];
                    string tag = opt.Iq < 0 ? " <color=#888>[dumb]</color>" : opt.Iq > 1 ? $" <color=#888>[IQ≥{opt.Iq}]</color>" : "";
                    if (GUILayout.Button($"{i + 1}. {_convo.OptionText(opt)}{tag}", Rich(GUI.skin.button)))
                        { Pick(opt); break; }
                }
            }
            else GUILayout.Label("<i>(conversation ended — Talk to reopen)</i>", Rich());
            GUILayout.EndScrollView();

            GUILayout.Label("<b>Trace</b>  <i>(script conditions + dialog effects)</i>", Rich());
            _logScroll = GUILayout.BeginScrollView(_logScroll);
            for (int i = 0; i < _log.Count; i++) GUILayout.Label(_log[i], Mono());
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private int IntField(string label, int value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(200));
            string s = GUILayout.TextField(value.ToString(), GUILayout.Width(70));
            GUILayout.EndHorizontal();
            return int.TryParse(s, out int v) ? v : value;
        }

        // Decode a SAP_DIALOG script into readable condition→action/else lines (the Python decoder, in C#).
        private string DecodeScript(int num)
        {
            ScriptFile f = _scripts?.Get(num);
            if (f == null) return $"script {num} not loaded.";
            var sb = new StringBuilder();
            sb.AppendLine($"script {num}  \"{f.Description}\"  flags={f.Flags:x}  entries={f.Entries.Count}\n");
            for (int i = 0; i < f.Entries.Count; i++)
            {
                ScriptCondition c = f.Entries[i];
                sb.Append($"{i,3}: if {(Sct)c.Type}{Ops(c.OpType, c.OpValue, IsObjCond((Sct)c.Type))}");
                sb.Append($"  THEN {ActionStr(c.Action)}");
                if (c.Els != null && (Sat)c.Els.Type != Sat.DoNothing) sb.Append($"  ELSE {ActionStr(c.Els)}");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static bool IsObjCond(Sct t) => t.ToString().StartsWith("Obj") || t == Sct.HasGold || t == Sct.KnowsSpell;

        private static string ActionStr(ScriptAction a)
        {
            var t = (Sat)a.Type;
            int n = t == Sat.Goto || t == Sat.Dialog ? 1 : (t == Sat.DoNothing || t == Sat.ReturnAndSkipDefault || t == Sat.ReturnAndRunDefault ? 0 : 4);
            return $"{t}{Ops(a.OpType, a.OpValue, false, n)}";
        }

        private static string Ops(byte[] types, int[] values, bool firstIsObj, int max = 4)
        {
            var parts = new List<string>();
            for (int i = 0; i < max && i < 8; i++)
            {
                if ((Svt)types[i] == Svt.Number && values[i] == 0 && i >= 1) continue;
                string tag = firstIsObj && i == 0 && types[i] < 24 ? ((Sfo)types[i]).ToString() : ((Svt)types[i]).ToString();
                parts.Add((Svt)types[i] == Svt.Number ? values[i].ToString() : $"{tag}:{values[i]}");
            }
            return parts.Count == 0 ? "" : "(" + string.Join(", ", parts) + ")";
        }

        private void ConfigureCamera()
        {
            Camera cam = Camera.main;
            if (!cam) cam = new GameObject("GalleryCamera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.09f, 0.11f);
        }

        private static GUIStyle _rich, _richBtn, _mono;
        private static GUIStyle Rich() => _rich ??= new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
        private static GUIStyle Rich(GUIStyle b) => _richBtn ??= new GUIStyle(b) { richText = true, alignment = TextAnchor.MiddleLeft, wordWrap = true };
        private static GUIStyle Mono() => _mono ??= new GUIStyle(GUI.skin.label) { richText = true, fontSize = 10, wordWrap = false };

        /// <summary>
        /// One editable game-state, implementing BOTH the dialog context and the script host over the same
        /// backing values — so a dialog effect and a script condition see one store. Everything a
        /// <c>.dlg</c>/<c>.scr</c> can read or write is a public field the IMGUI panel edits; every gate/effect
        /// is echoed to the log so you can watch the flow. Unmodelled world side-effects (teleport, spawn,
        /// combat) are logged, not performed — this is a pure logic bench.
        /// </summary>
        private sealed class Harness : IDialogContext, IScriptHost
        {
            private readonly System.Action<string> _log;
            public readonly ScriptGlobals Globals = new ScriptGlobals();
            public readonly object Pc = new object();
            public readonly object Npc = new object();
            public GeneratedDialogText Generated;
            public int Revision; // bumped by state-changing effects so the option cache recomputes
            public int RequestedDialogLine = -1; // set by SAT_DIALOG / dialog effects handled here

            // Editable state.
            public int IntelligenceValue = 12, PcRaceValue = 0, GoldValue = 100, AlignmentValue = 0, PersuasionValue = 5;
            public int ReactionValue = 50, StoryStateValue;
            public bool PcMale = true, NpcMale = true, Met, Following;

            // Quest states + the set of touched flag ids, kept only so the panel can list them; the flag/quest
            // VALUES live in Globals so the SAP_DIALOG script's conditions and the dialog effects agree (what
            // the runtime's shared RuntimeScriptGlobals does — a script gf and a dialog gf are one store).
            private readonly Dictionary<int, int> _quests = new Dictionary<int, int>();
            private readonly SortedSet<int> _pcFlags = new SortedSet<int>();
            private readonly SortedSet<int> _globalFlags = new SortedSet<int>();

            public Harness(System.Action<string> log) => _log = log;
            public void ResetTranscript() { }
            public bool NpcIsMale => NpcMale;

            public IEnumerable<KeyValuePair<int, int>> QuestSnapshot() => _quests;
            public IEnumerable<string> FlagsSnapshot()
            {
                foreach (int f in _globalFlags) yield return $"global flag {f}";
                foreach (int f in _pcFlags) yield return $"pc flag {f}";
            }

            // ── IDialogContext ──
            public int Intelligence => IntelligenceValue;
            public int Charisma => 10;
            public int Perception => 10;
            public int Level => 5;
            public int Gold { get => GoldValue; set => GoldValue = value; }
            public int PersuasionSkill => PersuasionValue;
            public int HaggleSkill => 5;
            public int BasicSkillLevel(int skill) => 5;
            public int TechSkillLevel(int skill) => 5;
            public bool PcIsMale => PcMale;
            public string PcName => "Player";
            public string NpcName => "NPC";
            public int PcRace => PcRaceValue;
            public int PcFlag(int i) => Globals.GetPcFlag(i);
            public void SetPcFlag(int i, int v) { Globals.SetPcFlag(i, v); Touch(_pcFlags, i, v); Revision++; _log?.Invoke($"  pf {i} = {v}"); }
            public int PcVar(int i) => Globals.GetPcVar(i);
            public void SetPcVar(int i, int v) { Globals.SetPcVar(i, v); Revision++; _log?.Invoke($"  pv {i} = {v}"); }
            public int Quest(int n) => Globals.GetPcQuestState(n);
            public void SetQuest(int n, int s) { _quests[n] = s; Globals.SetPcQuestState(n, s); Revision++; _log?.Invoke($"  qu {n} → {s}"); }
            public int GlobalFlag(int i) => Globals.GetFlag(i);
            public void SetGlobalFlag(int i, int v) { Globals.SetFlag(i, v); Touch(_globalFlags, i, v); Revision++; _log?.Invoke($"  gf {i} = {v}"); }
            public int GlobalVar(int i) => Globals.GetVar(i);
            public void SetGlobalVar(int i, int v) { Globals.SetVar(i, v); Revision++; _log?.Invoke($"  gv {i} = {v}"); }
            public int Alignment => AlignmentValue;
            public void AdjustAlignment(int d) { AlignmentValue += d; _log?.Invoke($"  al {d:+0;-0}"); }
            public void SetAlignment(int v) { AlignmentValue = v; _log?.Invoke($"  al = {v}"); }
            public int StoryState => StoryStateValue;
            public void SetStoryState(int v) { StoryStateValue = v; _log?.Invoke($"  ss = {v}"); }
            public bool RumorKnown(int id) => false;
            public void SetRumorKnown(int id) { Revision++; _log?.Invoke($"  ru {id}"); }
            public bool HasReputation(int id) => false;
            public void AddReputation(int id) { Revision++; _log?.Invoke($"  rp +{id}"); }
            public void RemoveReputation(int id) { Revision++; _log?.Invoke($"  rp -{id}"); }
            public void MarkAreaKnown(int id) { Revision++; _log?.Invoke($"  mm {id}"); }
            public bool HasMetNpc => Met;
            public int NpcReaction => ReactionValue;
            public void AdjustReaction(int d) { ReactionValue = Mathf.Clamp(ReactionValue + d, 0, 100); _log?.Invoke($"  re {d:+0;-0}"); }
            public void SetReaction(int v) { ReactionValue = Mathf.Clamp(v, 0, 100); _log?.Invoke($"  re = {v}"); }
            public int LocalFlag(int i) => 0;
            public void SetLocalFlag(int i, int v) => _log?.Invoke($"  lf {i} = {v}");
            public int LocalCounter(int i) => 0;
            public void SetLocalCounter(int i, int v) => _log?.Invoke($"  lc {i} = {v}");
            public bool HasItem(int proto, bool pcSide) => false;
            public void TransferItem(int proto, bool pcToNpc) => _log?.Invoke($"  in {proto} {(pcToNpc ? "→NPC" : "→PC")}");
            public void GiveXp(int questId) => _log?.Invoke($"  xp (quest {questId})");
            public void GiveFatePoint() => _log?.Invoke("  fp +1");
            public void StartCombat() => _log?.Invoke("  co (combat!)");
            public void RecruitNpc() => _log?.Invoke("  jo (NPC joins)");
            public bool IsNpcFollowingPc => Following;
            public bool AreaKnown(int id) => false;
            public void DisbandNpc() => _log?.Invoke("  lv (NPC leaves)");
            public void KillNpc() => _log?.Invoke("  nk (NPC killed)");
            public string GeneratedText(char token) => Generated?.For(token);

            // ── IScriptHost (the SAP_DIALOG script side) ──
            public void StartDialog(object obj, int dialogLine, int scriptNum, int scriptLine)
            {
                RequestedDialogLine = dialogLine;
                _log?.Invoke($"  SAT_DIALOG → open dialog line {dialogLine}");
            }
            public object[] ResolveFocus(int sfoType, int sfoValue, ScriptContext ctx)
            {
                var sfo = (Sfo)sfoType;
                if (sfo == Sfo.Triggerer || sfo == Sfo.Player) return new[] { Pc };
                if (sfo == Sfo.Attachee) return new[] { Npc };
                return new object[0];
            }
            public bool IsFollowingPc(object obj) => Following;
            public bool HasMetPc(object obj) => Met;
            public bool InConversation;                    // set by the gallery while a conversation is open
            public bool IsInDialog(object obj) => InConversation;
            public bool IsDaytime() => true;
            public int GetGold(object obj) => GoldValue;
            public bool KnowsSpell(object obj, int spell) => false;
            public bool IsNamed(object obj, int name) => false;
            public bool HasItemNamed(object obj, int nameId) => false;
            public bool IsMonsterOfType(object obj, int specie) => false;
            public bool IsWieldingItemNamed(object obj, int nameId) => false;
            public bool RumorKnown(object obj, int rumorId) => false;
            public bool CanOpenPortal(object actor, object portal, int direction) => true;
            public bool CanOpenContainer(object actor, object container) => true;
            public bool CanSeeObj(object observer, object target) => true;
            public bool CanHearObj(object listener, object source) => true;
            public bool IsDead(object obj) => false;
            public bool IsSwitchedOff(object obj) => false;
            public bool IsInCombat(object obj) => false;
            public bool IsAnimal(object obj) => false;
            public bool IsUndead(object obj) => false;
            public bool IsOpen(object obj) => false;
            public bool IsAtTile(object obj, int x, int y) => false;
            public bool IsWithinRange(object obj, int x, int y, int range) => false;
            public int GetStat(object obj, int stat) => stat == 4 ? IntelligenceValue : 8;
            public int GetSkill(object obj, int skill) => 5;
            public void Teleport(int mapId, int x, int y) => _log?.Invoke($"  SAT_TELEPORT map {mapId} ({x},{y})");
            public void TogglePortal(object obj) { }
            public void SetLocked(object obj, bool locked) { }
            public void FloatLine(object obj, int scriptNum, int lineNum) => _log?.Invoke($"  float line {lineNum}");
            public void PrintLine(object obj, int scriptNum, int lineNum) => _log?.Invoke($"  print line {lineNum}");
            public void ToggleOff(object obj) { }
            public void Kill(object obj) => _log?.Invoke("  SAT_KILL");
            public void AdjustGold(object obj, int amount) { GoldValue += amount; _log?.Invoke($"  gold {amount:+0;-0}"); }
            public void CritterFollow(object obj) => _log?.Invoke("  SAT_CRITTER_FOLLOW");
            public void CritterDisband(object obj) => _log?.Invoke("  SAT_CRITTER_DISBAND");
            public void Attack(object attacker, object target) => _log?.Invoke("  SAT_ATTACK");
            public void Damage(object obj, int amount, int type) => _log?.Invoke($"  SAT_DAMAGE {amount}");
            public void HealHp(object obj, int amount) => _log?.Invoke($"  SAT_HEAL {amount}");
            public void HealFatigue(object obj, int amount) { }
            public void CastSpell(object source, int spell, object target) => _log?.Invoke($"  SAT_CAST_SPELL {spell}");
            public void MarkMapLocation(object pc, int area) => _log?.Invoke($"  mark map location {area}");
            public void SetRumor(object pc, int rumor) => _log?.Invoke($"  set rumor {rumor}");
            public void UnfogTownmap(int map) => _log?.Invoke($"  unfog townmap {map}");
            public void QuellRumor(object pc, int rumor) => _log?.Invoke($"  quell rumor {rumor}");
            public void RunObjectScript(object triggerer, object attachee, int sap, int line) => _log?.Invoke($"  call-script-ex sap {sap} line {line}");
            public ScriptFile GetScript(int num) => null;

            private static void Touch(SortedSet<int> set, int i, int v) { if (v != 0) set.Add(i); else set.Remove(i); }
        }
    }
}
