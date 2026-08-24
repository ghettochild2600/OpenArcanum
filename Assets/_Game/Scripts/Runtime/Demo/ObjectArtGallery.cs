using System;
using System.Collections.Generic;
using Arcanum.Formats.Art;
using Arcanum.Formats.Database;
using Arcanum.Formats.Text;
using Arcanum.Runtime.Art;
using Arcanum.Runtime.World;
using UnityEngine;

namespace Arcanum.Runtime.Demo
{
    /// <summary>
    /// Standalone test scene — browses the static-object art tables in one place, one labelled section per
    /// object family: WALLS (one base piece per structure.mes entry), DOORS + WINDOWS (portal.mes, animated
    /// so doors swing), CONTAINERS (chests/barrels), ROOFS, FACADES (one representative piece — a facade
    /// frame is the 78×40 sliver drawn per tile), and SCENERY. Every entry is decoded from your install's
    /// name tables, so a missing/undecodable art shows up as a ✗-labelled empty cell (counts are logged).
    /// Drop this on a single GameObject in an otherwise empty scene and press Play: it mounts the data via
    /// <see cref="GameDataLocator"/> and creates a free-flying camera — WASD/arrows or middle/right-drag to
    /// pan, scroll to zoom, L toggles labels. Reads from your own legitimate install; bundles nothing.
    /// </summary>
    public sealed class ObjectArtGallery : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("Art is split across arcanum1..4.dat; all found are mounted.")]
        [SerializeField]
        private string[] Archives = { "arcanum1.dat", "arcanum2.dat", "arcanum3.dat", "arcanum4.dat" };

        [Header("Layout")]
        [Range(4, 40)]
        [SerializeField]
        private int Columns = 16;

        [SerializeField] private float CellWidth = 1.5f;
        [SerializeField] private float CellHeight = 1.7f;
        [SerializeField] private float SectionGap = 1.4f;
        [SerializeField] private float PixelsPerUnit = 100f;

        [Tooltip("Cap per section (0 = show everything); anything dropped is logged, never silent.")]
        [SerializeField]
        private int MaxPerSection = 0;

        [Tooltip("Scenery is by far the biggest table — turn off to focus on the structural families.")]
        [SerializeField]
        private bool ShowScenery = true;

        [SerializeField] private Color Background = new Color(0.10f, 0.10f, 0.12f, 1f);

        [Header("Camera")]
        [SerializeField] private float PanSpeed = 12f;   // world units/sec at zoom 10 (scales with zoom)
        [SerializeField] private float ZoomStep = 1.15f; // scroll multiplier per notch
        [SerializeField] private float MinZoom = 2f;
        [SerializeField] private float MaxZoom = 80f;

        private DatVirtualFileSystem _vfs;
        private Camera _cam;
        private bool _showLabels = true;
        private float _yCursor; // layout cursor — sections stack downward from 0

        private readonly List<(Vector3 pos, string text, bool header)> _labels = new List<(Vector3, string, bool)>();

        private void Start()
        {
            _vfs = new DatVirtualFileSystem();
            int mounted = 0;
            foreach (string archive in Archives)
            {
                string path = GameDataLocator.Find(archive);
                if (path != null) { _vfs.MountFile(path); mounted++; }
            }
            if (mounted == 0)
            {
                Debug.LogError("ObjectArtGallery: no arcanum*.dat found. Point the data locator at your Arcanum install.");
                return;
            }

            BuildWalls();
            BuildPortals();
            BuildSection("CONTAINERS", EntriesOf("art/container/container.mes", "art/container/", ""), animate: false);
            BuildSection("ROOFS", EntriesOf("art/roof/roofname.mes", "art/roof/", ".art"), animate: false);
            BuildSection("FACADES", EntriesOf("art/facade/facadename.mes", "art/facade/", ".art"),
                animate: false, useMiddleFrame: true); // frame 0 is often an edge sliver; the middle reads better
            if (ShowScenery)
                BuildSection("SCENERY", EntriesOf("art/scenery/scenery.mes", "art/scenery/", ""), animate: false);

            ConfigureCamera();
        }

        // ── Sections ────────────────────────────────────────────────────────────────────────────────

        // One representative piece per wall structure, resolved through the real WallArtResolver. A
        // structure doesn't ship art for every piece (many have no "bse" base at all, or files only under
        // their EXTERIOR name), so probe the pieces — and both name variants via rotation 0 (interior) /
        // 2 (exterior) — until an existing file turns up. A structure with no file at all stays a ✗ cell.
        private void BuildWalls()
        {
            MesFile wallName = ReadMes("art/wall/wallname.mes");
            MesFile structure = ReadMes("art/wall/structure.mes");
            if (wallName == null || structure == null)
            {
                Debug.LogWarning("ObjectArtGallery: art/wall/wallname.mes or structure.mes missing — WALLS skipped.");
                return;
            }
            var resolver = WallArtResolver.FromMes(wallName, structure);

            var items = new List<(string label, string path)>();
            int num = 0; // structure index — matches the resolver's file-order table (keys < 1000 only)
            foreach (KeyValuePair<int, string> e in structure.Entries)
            {
                if (e.Key >= 1000) continue; // the 1000+ block is display names, not structures
                string found = null;
                for (int rot = 0; rot <= 2 && found == null; rot += 2)
                    for (int piece = 0; piece < 46 && found == null; piece++)
                    {
                        // tig wall art id: type(28)=WALL, num(20), piece(14), rotation(11), variation(8)=0.
                        uint id = ((uint)ArtId.TypeWall << 28) | ((uint)num << 20) | ((uint)piece << 14) | ((uint)rot << 11);
                        string p = resolver.Resolve(id);
                        if (p != null && _vfs.Exists(p)) found = p;
                    }

                // The friendly name lives at key 1000+n ("<n> <Display Name>", a_name.c:1772).
                string display = structure.Get(1000 + e.Key);
                int sp = display != null ? display.IndexOf(' ') : -1;
                items.Add(($"{e.Key} {(sp >= 0 ? display.Substring(sp + 1).Trim() : display ?? e.Value)}", found));
                num++;
            }
            BuildSection("WALLS (one piece per structure)", items, animate: false);
        }

        // Doors + windows from portal.mes ("<file> <n>"; windows keyed 1001+). Animated at the art's fps so
        // the swing frames (doors 0..6, windows 0..1) actually play.
        private void BuildPortals()
        {
            MesFile portal = ReadMes("art/portal/portal.mes");
            if (portal == null) { Debug.LogWarning("ObjectArtGallery: art/portal/portal.mes missing — DOORS skipped."); return; }

            var doors = new List<(string label, string path)>();
            var windows = new List<(string label, string path)>();
            foreach (KeyValuePair<int, string> e in portal.Entries)
            {
                int sp = e.Value.IndexOf(' ');
                string file = (sp >= 0 ? e.Value.Substring(0, sp) : e.Value).Trim();
                if (file.Length == 0) continue;
                (e.Key >= 1001 ? windows : doors).Add(($"{e.Key} {ShortName(file)}", ("art/portal/" + file).ToLowerInvariant()));
            }
            BuildSection("DOORS (animated)", doors, animate: true);
            BuildSection("WINDOWS (animated)", windows, animate: true);
        }

        // A generic mes-driven section: key → "<file>" (suffix appended when the table stores bare names).
        private List<(string label, string path)> EntriesOf(string mesPath, string artDir, string suffix)
        {
            MesFile mes = ReadMes(mesPath);
            if (mes == null)
            {
                Debug.LogWarning($"ObjectArtGallery: {mesPath} missing — section skipped.");
                return new List<(string, string)>();
            }

            var items = new List<(string, string)>();
            foreach (KeyValuePair<int, string> e in mes.Entries)
            {
                string file = e.Value.Trim();
                if (file.Length == 0) continue;
                items.Add(($"{e.Key} {ShortName(file)}", (artDir + file + suffix).ToLowerInvariant()));
            }
            return items;
        }

        // ── Layout / placement ──────────────────────────────────────────────────────────────────────

        private void BuildSection(string title, List<(string label, string path)> items, bool animate,
            bool useMiddleFrame = false)
        {
            if (items.Count == 0) return;
            int shown = items.Count;
            if (MaxPerSection > 0 && shown > MaxPerSection)
            {
                shown = MaxPerSection;
                Debug.Log($"[ObjectArtGallery] {title}: showing {shown} of {items.Count} (MaxPerSection) — the rest are NOT displayed.");
            }

            _labels.Add((new Vector3(-(Columns * CellWidth) * 0.5f, _yCursor, 0f), title, true));
            _yCursor -= CellHeight * 0.6f; // headroom under the header

            int missing = 0;
            for (int i = 0; i < shown; i++)
            {
                var pos = new Vector3(
                    (i % Columns - (Columns - 1) * 0.5f) * CellWidth,
                    _yCursor - (i / Columns) * CellHeight - CellHeight * 0.5f, 0f);
                if (!Place(items[i].path, pos, items[i].label, animate, useMiddleFrame)) missing++;
            }

            int rows = (shown + Columns - 1) / Columns;
            _yCursor -= rows * CellHeight + SectionGap;
            Debug.Log($"[ObjectArtGallery] {title}: {shown - missing} decoded, {missing} missing/undecodable.");
        }

        private bool Place(string path, Vector3 pos, string label, bool animate, bool useMiddleFrame)
        {
            (Sprite[] frames, int fps) = LoadFrames(path, useMiddleFrame);
            if (frames == null)
            {
                _labels.Add((pos, "✗ " + label, false));
                return false;
            }

            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];

            // Down-scale anything larger than its cell so tall scenery/wide facades don't bury the grid.
            Bounds b = sr.sprite.bounds;
            float scale = Mathf.Min(1f, (CellWidth * 0.92f) / Mathf.Max(0.01f, b.size.x),
                                        (CellHeight * 0.80f) / Mathf.Max(0.01f, b.size.y));
            go.transform.localScale = Vector3.one * scale;

            if (animate && frames.Length > 1)
                go.AddComponent<SpriteFrameAnimator>().Init(frames, fps > 0 ? fps : 4);

            _labels.Add((new Vector3(pos.x, pos.y - CellHeight * 0.44f, 0f), label, false));
            return true;
        }

        // Rotation 0's frames (or just the middle frame), decoded with a centred pivot for grid placement.
        private (Sprite[] frames, int fps) LoadFrames(string path, bool useMiddleFrame)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !_vfs.Exists(path)) return (null, 0);
                ArtFile art = ArtReader.Read(_vfs.ReadAllBytes(path));
                if (art.Rotations.Count == 0 || art.Rotations[0].Frames.Length == 0) return (null, 0);

                ArtFrame[] src = art.Rotations[0].Frames;
                var centre = new Vector2(0.5f, 0.5f);
                if (useMiddleFrame)
                {
                    int frameIndex = src.Length / 2;
                    return (new[]
                    {
                        ArtTextureFactory.CreateSprite(
                            src[frameIndex],
                            art.PrimaryPalette,
                            path,
                            rotation: 0,
                            frameIndex: frameIndex,
                            pixelsPerUnit: PixelsPerUnit,
                            pivotOverride: centre)
                    }, 0);
                }

                var frames = new Sprite[src.Length];
                for (int f = 0; f < src.Length; f++)
                    frames[f] = ArtTextureFactory.CreateSprite(
                        src[f],
                        art.PrimaryPalette,
                        path,
                        rotation: 0,
                        frameIndex: f,
                        pixelsPerUnit: PixelsPerUnit,
                        pivotOverride: centre);
                return (frames, art.Fps);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"ObjectArtGallery: decode failed for '{path}': {ex.Message}");
                return (null, 0);
            }
        }

        private MesFile ReadMes(string path) => _vfs.Exists(path) ? MesReader.Read(_vfs.ReadAllBytes(path)) : null;

        private static string ShortName(string file)
        {
            int dot = file.LastIndexOf('.');
            return dot > 0 ? file.Substring(0, dot) : file;
        }

        // ── Camera (free-flying) ────────────────────────────────────────────────────────────────────

        private void ConfigureCamera()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                var camGo = new GameObject("GalleryCamera") { tag = "MainCamera" };
                _cam = camGo.AddComponent<Camera>();
            }

            _cam.orthographic = true;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Background;
            _cam.transform.position = new Vector3(0f, -6f, -10f); // start at the top of the stack
            float aspect = _cam.aspect <= 0f ? 16f / 9f : _cam.aspect;
            _cam.orthographicSize = Mathf.Clamp(Columns * CellWidth * 0.5f / aspect + 1f, MinZoom, MaxZoom);
        }

        private Vector3 _lastMouse;

        private void Update()
        {
            if (_cam == null) return;
            if (Input.GetKeyDown(KeyCode.L)) _showLabels = !_showLabels;

            float speed = PanSpeed * (_cam.orthographicSize / 10f);
            var pan = new Vector3(
                (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f),
                (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f), 0f);
            _cam.transform.position += pan * (speed * Time.deltaTime);

            // Middle/right-mouse drag pans (screen delta → world units at the current zoom).
            if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)) _lastMouse = Input.mousePosition;
            if (Input.GetMouseButton(1) || Input.GetMouseButton(2))
            {
                Vector3 d = Input.mousePosition - _lastMouse;
                _lastMouse = Input.mousePosition;
                float unitsPerPixel = _cam.orthographicSize * 2f / Screen.height;
                _cam.transform.position -= new Vector3(d.x, d.y, 0f) * unitsPerPixel;
            }

            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0f)
                _cam.orthographicSize = Mathf.Clamp(
                    _cam.orthographicSize * Mathf.Pow(ZoomStep, -scroll), MinZoom, MaxZoom);
        }

        // Labels via IMGUI so the scene needs no font asset. Cell labels hide when zoomed far out (they'd
        // overlap into noise); section headers always draw.
        private void OnGUI()
        {
            if (_cam == null) return;
            bool cellLabels = _showLabels && _cam.orthographicSize < 26f;

            var cell = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 10 };
            cell.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
            var header = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontSize = 18, fontStyle = FontStyle.Bold };
            header.normal.textColor = Color.white;

            foreach ((Vector3 pos, string text, bool isHeader) in _labels)
            {
                if (!isHeader && !cellLabels) continue;
                Vector3 sp = _cam.WorldToScreenPoint(pos);
                if (sp.z < 0f || sp.x < -200f || sp.x > Screen.width + 200f || sp.y < -50f || sp.y > Screen.height + 50f) continue;
                if (isHeader) GUI.Label(new Rect(sp.x, Screen.height - sp.y - 12f, 500f, 24f), text, header);
                else GUI.Label(new Rect(sp.x - 70f, Screen.height - sp.y - 8f, 140f, 16f), text, cell);
            }
        }
    }
}
