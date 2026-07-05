using System;
using System.Collections.Generic;
using Arcanum.Formats.Database;
using Arcanum.Runtime;            // GameDataLocator
using Arcanum.Runtime.Demo;       // DialogScriptGallery
using UnityEditor;
using UnityEngine;

namespace Arcanum.Editor
{
    /// <summary>
    /// Browse every NPC dialog (<c>dlg/*.dlg</c>) in the install, filter by name/number, and open one in the
    /// running <see cref="DialogScriptGallery"/>. The list is enumerated straight from the archives (works in
    /// edit mode too); <b>Talk</b> only drives the bench in Play mode. Mirrors the Sector Browser pattern.
    /// </summary>
    public sealed class DialogBrowserWindow : EditorWindow
    {
        private struct Entry { public int Num; public string Name, Path; }

        private DialogScriptGallery _target;
        private readonly List<Entry> _dialogs = new List<Entry>();
        private string _search = string.Empty;
        private Vector2 _scroll;
        private string _status;

        public static void Open(DialogScriptGallery target)
        {
            var window = GetWindow<DialogBrowserWindow>(utility: true, title: "Dialog Browser", focus: true);
            window._target = target;
            window.minSize = new Vector2(420f, 460f);
            window.Refresh();
            window.Show();
        }

        [MenuItem("Arcanum/Test/Dialog Browser")]
        private static void OpenFromMenu()
        {
            var gallery = FindFirstObjectByType<DialogScriptGallery>();
            if (!gallery) { EditorUtility.DisplayDialog("Dialog Browser", "No DialogScriptGallery in the scene. Add one to a GameObject and press Play.", "OK"); return; }
            Open(gallery);
        }

        private void Refresh()
        {
            _dialogs.Clear();
            _status = null;

            // Mount the same archives the bench uses (defaults if it isn't in the scene yet).
            string[] archives = _target ? _target.ArchiveNames : new[] { "arcanum1.dat", "arcanum2.dat", "arcanum3.dat", "arcanum4.dat" };
            int mounted = 0;
            try
            {
                using var vfs = new DatVirtualFileSystem();
                foreach (string a in archives)
                {
                    string p = GameDataLocator.Find(a);
                    if (!string.IsNullOrEmpty(p)) { vfs.MountFile(p); mounted++; }
                }
                if (mounted == 0) { _status = "No archives found — configure your game data (GameDataConfig)."; return; }

                var seen = new HashSet<int>();
                foreach (string path in vfs.EnumerateFiles("dlg/"))
                {
                    if (!path.EndsWith(".dlg", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!TryParse(path, out Entry e) || !seen.Add(e.Num)) continue;
                    _dialogs.Add(e);
                }
                _dialogs.Sort((a, b) => a.Num.CompareTo(b.Num));
                _status = $"{_dialogs.Count} dialog(s).";
            }
            catch (Exception ex) { _status = $"Enumeration failed: {ex.Message}"; }
        }

        // "dlg/01324virgil.dlg" → { Num=1324, Name="virgil" }.
        private static bool TryParse(string path, out Entry e)
        {
            e = default;
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            int i = 0; while (i < file.Length && char.IsDigit(file[i])) i++;
            if (i == 0 || !int.TryParse(file.Substring(0, i), out int num)) return false;
            e = new Entry { Num = num, Name = file.Substring(i).Replace('_', ' ').Trim(), Path = path };
            return true;
        }

        private void OnGUI()
        {
            if (!_target)
            {
                EditorGUILayout.HelpBox("Open from a DialogScriptGallery inspector (Browse dialogs…), or add one and use Arcanum ▸ Test ▸ Dialog Browser.", MessageType.Warning);
                if (GUILayout.Button("Find gallery in scene")) { _target = FindFirstObjectByType<DialogScriptGallery>(); Refresh(); }
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Target", _target.name);
                if (GUILayout.Button("Refresh", GUILayout.Width(70f))) Refresh();
            }
            _search = EditorGUILayout.TextField("Search (name or #)", _search);
            if (!string.IsNullOrEmpty(_status)) EditorGUILayout.LabelField(_status, EditorStyles.miniLabel);
            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("Enter Play mode to open a dialog in the bench.", MessageType.Info);

            int current = Application.isPlaying ? _target.CurrentDialog : -1;
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            int shown = 0;
            foreach (Entry e in _dialogs)
            {
                if (!Matches(e, _search)) continue;
                shown++;
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!Application.isPlaying))
                        if (GUILayout.Button("Talk", GUILayout.Width(50f))) _target.OpenDialog(e.Num);
                    bool isCurrent = e.Num == current;
                    if (isCurrent) GUI.color = new Color(0.55f, 1f, 0.55f);
                    EditorGUILayout.LabelField($"{e.Num,5}  {e.Name}{(isCurrent ? "  ◀ open" : "")}");
                    GUI.color = Color.white;
                }
            }
            EditorGUILayout.EndScrollView();
            if (shown == 0 && _dialogs.Count > 0) EditorGUILayout.LabelField("No matches.", EditorStyles.miniLabel);
        }

        private static bool Matches(Entry e, string search)
        {
            if (string.IsNullOrEmpty(search)) return true;
            return e.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                   || e.Num.ToString().StartsWith(search, StringComparison.Ordinal);
        }
    }
}
