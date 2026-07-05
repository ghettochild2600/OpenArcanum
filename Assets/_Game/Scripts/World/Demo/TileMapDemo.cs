using System.Collections.Generic;
using Arcanum.Formats.Art;    // FacadeArtResolver
using Arcanum.Formats.Database;
using Arcanum.Formats.Text;
using Arcanum.Formats.Tiles;
using Arcanum.Formats.World;
using Arcanum.Runtime;        // GameDataLocator
using UnityEngine;

namespace Arcanum.World.Demo
{
    /// <summary>
    /// Standalone test scene for the terrain/tile generator. Loads one real Arcanum sector and renders it through
    /// the exact in-game <see cref="TileMapRenderer"/> — the same batched, blended, mirror-edged terrain mesh the
    /// full game builds — so you can eyeball that tiles resolve, blends route, and the mesh stitches correctly,
    /// without booting the rest of the game.
    /// <para>
    /// Drop this on a single GameObject in an otherwise empty scene and press Play. It mounts your own legitimate
    /// Arcanum install via <see cref="GameDataLocator"/> and bundles nothing. Drag to pan, scroll to zoom.
    /// </para>
    /// </summary>
    public sealed class TileMapDemo : MonoBehaviour
    {
        [Header("Source archives (auto-located if left at defaults)")]
        [Tooltip("Archive with tile art + art/tile/tilename.mes + art/facade/facadename.mes (default arcanum2.dat).")]
        [SerializeField]
        private string AssetsArchive = "arcanum2.dat";

        [Tooltip("Module archive holding the sectors (default modules/Arcanum.dat).")]
        [SerializeField]
        private string ModuleArchive = "modules/Arcanum.dat";

        [Tooltip("Sector to render, as a path inside the module archive (a real .sec under maps/<name>/).")]
        [SerializeField]
        private string SectorPath = "maps/arcanum1-024-fixed/101602821844.sec";

        [Header("Rendering")]
        [Tooltip("One batched mesh per sector (in-game path). Off = one SpriteRenderer per tile (heavy fallback).")]
        [SerializeField]
        private bool BatchTerrain = true;

        [SerializeField]
        private float PixelsPerUnit = 100f;

        [SerializeField]
        private Color Background = new Color(0.16f, 0.17f, 0.20f, 1f);

        private DatVirtualFileSystem _vfs;
        private TileMapRenderer _tileMap;
        private Camera _cam;
        private float _zoom = 1f;
        private Vector3 _dragOrigin;

        // WebGL: the player uploads their own .dat files (no host filesystem in a browser); desktop reads the
        // install directly via GameDataLocator. Both end up mounting into the same VFS.
        private WebGLFilePicker _picker;
        private string[] _uploadedNames = System.Array.Empty<string>();
        private bool _dataReady;
        private string _uploadStatus;

        // Runtime sector browser (the in-game equivalent of the editor Sector Browser window).
        private readonly List<string> _sectors = new List<string>();
        private bool _showSectors;
        private Vector2 _sectorScroll;
        private string _search = string.Empty;
        private GUIStyle _rich, _selected;

        /// <summary>The module archive sectors are read from (for editor tooling like the Sector Browser).</summary>
        public string ModuleArchiveName => ModuleArchive;

        /// <summary>The sector currently selected/rendered.</summary>
        public string CurrentSector => SectorPath;

        private void Start()
        {
            _dataReady = EnsureData();
            if (_dataReady) { RefreshSectorList(); RenderCurrentSector(); }
            // In a browser with no data yet, OnGUI shows the upload gate instead of a black screen.
        }

        /// <summary>
        /// Selects and renders a different sector at runtime, tearing down the current one. Called by the editor
        /// Sector Browser's per-row Load button. No-op with an error log if the data can't be mounted.
        /// </summary>
        public void LoadSector(string sectorPath)
        {
            if (!string.IsNullOrEmpty(sectorPath)) SectorPath = sectorPath;
            if (EnsureData()) RenderCurrentSector();
        }

        // Mounts the archives and builds the resolvers once; cached across reloads. False on failure (missing
        // install on desktop, or no upload yet in a browser — the caller then shows the upload gate).
        private bool EnsureData()
        {
            if (_tileMap != null) return true;

            var vfs = new DatVirtualFileSystem();
            if (MountData(vfs) == 0) { vfs.Dispose(); return false; }
            if (!vfs.Exists("art/tile/tilename.mes"))
            {
                Debug.LogError("TileMapDemo: 'art/tile/tilename.mes' not found — the tile-art archive (arcanum2.dat) " +
                               "wasn't mounted.", this);
                vfs.Dispose();
                return false;
            }

            _vfs = vfs;
            TileArtPathResolver tileResolver =
                TileArtPathResolver.FromMes(MesReader.Read(_vfs.ReadAllBytes("art/tile/tilename.mes")));
            FacadeArtResolver facades = _vfs.Exists("art/facade/facadename.mes")
                ? FacadeArtResolver.FromMes(MesReader.Read(_vfs.ReadAllBytes("art/facade/facadename.mes")))
                : null;
            _tileMap = new TileMapRenderer(_vfs, tileResolver, facades, PixelsPerUnit);
            return true;
        }

        // Mounts the source archives into <paramref name="vfs"/>; returns how many mounted (0 ⇒ no data yet).
        // Desktop reads the install via GameDataLocator; WebGL mounts whatever .dat the player uploaded.
        private int MountData(DatVirtualFileSystem vfs)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            int n = 0;
            foreach (string name in _uploadedNames)
            {
                try { vfs.MountFile(WebGLFilePicker.PathFor(name)); n++; }
                catch (System.Exception e) { Debug.LogWarning($"TileMapDemo: could not mount '{name}': {e.Message}", this); }
            }
            return n;
#else
            string assetsPath = GameDataLocator.Find(AssetsArchive);
            string modulePath = GameDataLocator.Find(ModuleArchive);
            if (string.IsNullOrEmpty(assetsPath) || string.IsNullOrEmpty(modulePath))
            {
                Debug.LogError($"TileMapDemo: could not locate '{AssetsArchive}' and/or '{ModuleArchive}'. " +
                               "Point the data locator at your Arcanum install (or copy the .dat files into <project>/GameData/).", this);
                return 0;
            }
            Debug.Log($"TileMapDemo: assets='{assetsPath}', module='{modulePath}'.", this);
            vfs.MountFile(modulePath);  // sectors
            vfs.MountFile(assetsPath);  // tile art + .mes tables
            return 2;
#endif
        }

        // Enumerate the mounted module's sectors for the runtime browser (maps/<name>/<id>.sec).
        private void RefreshSectorList()
        {
            _sectors.Clear();
            if (_vfs == null) return;
            foreach (string p in _vfs.EnumerateFiles("maps/"))
                if (p.EndsWith(".sec", System.StringComparison.OrdinalIgnoreCase)) _sectors.Add(p);
            _sectors.Sort(System.StringComparer.OrdinalIgnoreCase);
        }

        // Tears down any previously rendered geometry and renders the current SectorPath, then frames the camera.
        private void RenderCurrentSector()
        {
            if (_tileMap == null) return;

            if (!_vfs.Exists(SectorPath))
            {
                Debug.LogError($"TileMapDemo: sector '{SectorPath}' not found in '{ModuleArchive}'.", this);
                return;
            }

            SectorTerrain terrain;
            try { terrain = SectorReader.ReadTerrain(_vfs.ReadAllBytes(SectorPath)); }
            catch (System.Exception ex) { Debug.LogError($"TileMapDemo: terrain read failed: {ex.Message}", this); return; }

            ClearRendered();
            // Render at the world origin (the global tile offset only matters when stitching adjacent sectors),
            // so keep the host object's transform identity — the batched mesh positions its own quads.
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;

            _tileMap.RenderSector(terrain, 0, 0, transform, DefaultSpriteMaterial(), sortingOrder: 0, batch: BatchTerrain,
                placeTile: PlaceTile);

            Debug.Log($"TileMapDemo: rendered '{SectorPath}' ({SectorTerrain.Size}×{SectorTerrain.Size} tiles, " +
                      $"{_tileMap.BlendMisses} cumulative blend miss(es)).", this);
            ConfigureCamera();
        }

        // Destroys previously rendered children (terrain mesh / per-tile sprites) before a reload.
        private void ClearRendered()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
        }

        // The material an actual SpriteRenderer uses — under URP's 2D renderer this is the correct default sprite
        // material (a raw "Sprites/Default" from Shader.Find can render black in a 2D pipeline). Matches the game.
        private static Material DefaultSpriteMaterial()
        {
            var probe = new GameObject("~spriteMatProbe") { hideFlags = HideFlags.HideAndDontSave };
            Material mat = probe.AddComponent<SpriteRenderer>().sharedMaterial;
            Destroy(probe);
            return mat;
        }

        // Per-tile placement for the non-batched fallback path; a plain sprite at a per-tile depth.
        private SpriteRenderer PlaceTile(string name, Transform parent, Sprite sprite, Vector3 localPos, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        private void ConfigureCamera()
        {
            _cam = Camera.main;
            if (!_cam)
            {
                var camGo = new GameObject("DemoCamera") { tag = "MainCamera" };
                _cam = camGo.AddComponent<Camera>();
            }

            _cam.orthographic = true;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Background;
            _cam.transform.rotation = Quaternion.identity;
            _cam.farClipPlane = Mathf.Max(_cam.farClipPlane, 100f);

            // Frame the ACTUAL rendered geometry (world-space renderer bounds), so framing can't drift from where
            // the mesh landed. If nothing rendered, say so loudly instead of leaving a silent black screen.
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogError("TileMapDemo: nothing was rendered (0 renderers under this object). The terrain " +
                               "read/resolve produced no geometry — check the logs above for data-path or sector errors.", this);
                return;
            }

            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);

            _cam.transform.position = new Vector3(b.center.x, b.center.y, -10f);
            float aspect = _cam.aspect <= 0f ? 16f / 9f : _cam.aspect;
            _zoom = Mathf.Max(b.extents.y + 1f, (b.extents.x + 1f) / aspect);
            _cam.orthographicSize = _zoom;
            Debug.Log($"TileMapDemo: framed {renderers.Length} renderer(s); bounds center {b.center}, size {b.size}.", this);
        }

        private void Update()
        {
            if (!_cam) return;

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                _zoom = Mathf.Clamp(_zoom * (1f - scroll * 0.1f), 1f, 200f);
                _cam.orthographicSize = _zoom;
            }

            if (Input.GetMouseButtonDown(0)) _dragOrigin = _cam.ScreenToWorldPoint(Input.mousePosition);
            else if (Input.GetMouseButton(0))
            {
                Vector3 now = _cam.ScreenToWorldPoint(Input.mousePosition);
                _cam.transform.position += _dragOrigin - now;
            }
        }

        // ── UI: upload gate (WebGL) + runtime sector browser ──────────────────────────────────────

        private void OnGUI()
        {
            _rich ??= new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };

            if (!_dataReady) { DrawUploadGate(); return; }
            DrawSectorBrowser();
        }

        private void DrawUploadGate()
        {
            const float w = 480f, h = 220f;
            GUILayout.BeginArea(new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h), GUI.skin.box);
            GUILayout.Label("<b>Arcanum terrain viewer</b>", _rich);
            GUILayout.Space(6f);

            if (WebGLFilePicker.Supported)
            {
                GUILayout.Label("This runs on <b>your own</b> Arcanum install. Choose your data archives — at least " +
                                "<b>arcanum2.dat</b> (tile art) and <b>Arcanum.dat</b> (from the install's <i>modules</i> " +
                                "folder — the maps). Nothing is uploaded to a server; the files stay in your browser.", _rich);
                GUILayout.Space(10f);
                if (!string.IsNullOrEmpty(_uploadStatus)) { GUILayout.Label(_uploadStatus, _rich); GUILayout.Space(6f); }
                if (GUILayout.Button("Choose .dat files…", GUILayout.Height(34f))) BeginUpload();
            }
            else
            {
                GUILayout.Label("Could not find your Arcanum install. Configure the <b>GameDataConfig</b> asset (its data " +
                                "roots), or drop the .dat files into <i>&lt;project&gt;/GameData/</i>, then press Play.", _rich);
            }
            GUILayout.EndArea();
        }

        private void DrawSectorBrowser()
        {
            GUILayout.BeginArea(new Rect(8f, 8f, 280f, Screen.height - 16f));
            if (GUILayout.Button(_showSectors ? "Sectors ▲" : $"Sectors ▾   ({_sectors.Count})"))
                _showSectors = !_showSectors;

            if (_showSectors)
            {
                _search = GUILayout.TextField(_search);
                _sectorScroll = GUILayout.BeginScrollView(_sectorScroll, GUI.skin.box);
                int shown = 0;
                foreach (string s in _sectors)
                {
                    if (!string.IsNullOrEmpty(_search) && s.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    shown++;
                    bool current = string.Equals(s, SectorPath, System.StringComparison.OrdinalIgnoreCase);
                    if (GUILayout.Button((current ? "◀ " : "") + ShortName(s), current ? Selected() : GUI.skin.button))
                    { _showSectors = false; LoadSector(s); }
                }
                if (shown == 0) GUILayout.Label("<i>no matches</i>", _rich);
                GUILayout.EndScrollView();
            }
            GUILayout.EndArea();
        }

        // Open the browser file picker (creating the bridge on first use), then mount + render on success.
        private void BeginUpload()
        {
            if (_picker == null)
            {
                var go = new GameObject("ArcanumFilePicker"); // unique name — SendMessage targets it
                go.transform.SetParent(transform, false);
                _picker = go.AddComponent<WebGLFilePicker>();
            }
            _uploadStatus = "Waiting for you to choose files…";
            _picker.Pick(
                names =>
                {
                    // Accumulate across picks so you can add the module after the tile art (or vice versa), then
                    // rebuild the VFS from scratch with the full set.
                    var all = new List<string>(_uploadedNames);
                    foreach (string n in names) if (!all.Contains(n)) all.Add(n);
                    _uploadedNames = all.ToArray();

                    _tileMap = null; _vfs?.Dispose(); _vfs = null; // force EnsureData to re-mount the full set
                    if (!EnsureData())
                    {
                        _uploadStatus = "No tile art found — select <b>arcanum2.dat</b>.";
                        return;
                    }
                    RefreshSectorList();
                    if (_sectors.Count == 0)
                    {
                        _uploadStatus = "Tile art loaded, but no maps — also select <b>Arcanum.dat</b> (the module).";
                        return; // keep the gate open until we have something to render
                    }
                    _dataReady = true;
                    if (!_vfs.Exists(SectorPath)) SectorPath = _sectors[0]; // default sector may not be in this module
                    RenderCurrentSector();
                },
                err => _uploadStatus = "Upload cancelled or failed: " + err);
        }

        // "maps/arcanum1-024-fixed/101602821844.sec" → "arcanum1-024-fixed / …821844" for the button label.
        private static string ShortName(string sectorPath)
        {
            string[] parts = sectorPath.Split('/');
            if (parts.Length < 2) return sectorPath;
            string map = parts[parts.Length - 2];
            string file = System.IO.Path.GetFileNameWithoutExtension(parts[parts.Length - 1]);
            if (file.Length > 6) file = "…" + file.Substring(file.Length - 6);
            return $"{map} / {file}";
        }

        private GUIStyle Selected() =>
            _selected ??= new GUIStyle(GUI.skin.button) { normal = { textColor = new Color(0.6f, 1f, 0.6f) } };

        private void OnDestroy() => _vfs?.Dispose();
    }
}
