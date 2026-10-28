using System;
using System.Collections.Generic;
using Arcanum.Formats.Art;
using Arcanum.Runtime.World;
using OpenArcanum.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Arcanum.Runtime.UI
{
    /// <summary>Source rectangles and interface ART ids from wmap_ui.c.</summary>
    public static class RetailLocalMapLayout
    {
        public const int BackgroundSourceId = 138;
        public const int PcLensFrameSourceId = 198;
        public const int TownMapButtonSourceId = 811;
        public const int WorldMapButtonSourceId = 812;
        public const int ScrollButtonSourceId = 139;
        public const int TravelButtonSourceId = 813;
        public const int NavigationCoverSourceId = 217;
        public const int CrossMarkerSourceId = 204;

        public static readonly Rect Window = new(0f, 41f, 800f, 400f);
        public static readonly Rect Canvas = new(150f, 11f, 501f, 365f);
        public static readonly Rect ContinentCanvas = new(218f, 11f, 365f, 365f);
        public static readonly Rect PcLens = new(25f, 24f, 89f, 89f);
        public static readonly Vector2 TownMapButton = new(20f, 165f);
        public static readonly Vector2 WorldMapButton = new(19f, 267f);
        public static readonly Vector2 ScrollButton = new(375f, 347f);
        public static readonly Vector2 TravelButton = new(698f, 312f);

        public static Rect ScreenRect(int width, int height)
            => new((width - Window.width) * .5f, 41f + (height - 600f) * .5f,
                Window.width, Window.height);

        public static Vector2 ClampPan(Vector2 desired, Vector2 mapSize)
            => new(Mathf.Clamp(desired.x, 0f, Mathf.Max(0f, mapSize.x - Canvas.width)),
                Mathf.Clamp(desired.y, 0f, Mathf.Max(0f, mapSize.y - Canvas.height)));

        public static Vector2 CenterOn(Vector2 point, Vector2 mapSize)
            => ClampPan(new Vector2(
                Mathf.Floor(point.x - Canvas.width * .5f),
                Mathf.Floor(point.y - Canvas.height * .5f)), mapSize);
    }

    [DisallowMultipleComponent]
    public sealed class RetailLocalMapView : MonoBehaviour
    {
        private GameUiController _controller;
        private WorldMapSessionCoordinator _session;
        private UiSkinResolver _resolver;
        private GameObject _canvasRoot;
        private RectTransform _window;
        private RectTransform _viewport;
        private RectTransform _mapLayer;
        private RetailPcLensView _pcLens;
        private SourceUiButton _townButton;
        private SourceUiButton _continentButton;
        private readonly List<Object> _runtimeAssets = new();
        private LocalMapProjection _projection;
        private Vector2 _pan;
        private int _signature = int.MinValue;
        private bool _centeringRequired = true;
        private bool _continentView;

        public bool IsAvailable => _canvasRoot != null && _resolver != null;
        public bool IsVisible => IsAvailable && _canvasRoot.activeSelf;
        public RectTransform WindowTransform => _window;
        public RectTransform ViewportTransform => _viewport;
        public Vector2 Pan => _pan;
        public LocalMapProjection Projection => _projection;
        public bool IsContinentView => _continentView;

        public void Bind(GameUiController controller, WorldMapSessionCoordinator session, UiSkinResolver resolver)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            if (_controller == controller && ReferenceEquals(_resolver, resolver) && IsAvailable) return;
            Cleanup();
            _controller = controller;
            _session = session;
            _resolver = resolver;
            BuildPresentation();
            Synchronize();
        }

        public void Synchronize()
        {
            if (!IsAvailable || _controller == null) return;
            LocalMapProjection projection = null;
            bool visible = _controller.HasPlayer && _controller.Screen == GameUiScreen.Map
                           && _session.LocalMap.TryProject(out projection);
            if (_canvasRoot.activeSelf != visible)
            {
                _canvasRoot.SetActive(visible);
                if (visible) _centeringRequired = true;
            }
            if (!visible) return;
            int signature = Signature(projection);
            if (_continentView) signature = HashCode.Combine(signature, 1);
            _projection = projection;
            if (signature != _signature)
            {
                _signature = signature;
                if (_continentView) RebuildContinent(); else RebuildMap();
            }
            if (_centeringRequired)
            {
                LocalMapMarker player = default;
                bool found = false;
                foreach (LocalMapMarker marker in projection.Markers)
                    if (marker.Kind == LocalMapMarkerKind.Player) { player = marker; found = true; break; }
                Vector2 mapSize = MapSize();
                _pan = found ? RetailLocalMapLayout.CenterOn(player.Pixel, mapSize) : Vector2.zero;
                _centeringRequired = false;
                ApplyPan();
            }
        }

        public bool ContainsScreenPoint(Vector2 screenBottomLeft)
        {
            if (!IsVisible) return false;
            Vector2 topLeft = new(screenBottomLeft.x, Screen.height - screenBottomLeft.y);
            return RetailLocalMapLayout.ScreenRect(Screen.width, Screen.height).Contains(topLeft);
        }

        public void PanBy(Vector2 delta)
        {
            if (!IsVisible || _projection == null) return;
            _pan = RetailLocalMapLayout.ClampPan(_pan + delta, MapSize());
            ApplyPan();
        }

        private void Update()
        {
            if (!IsVisible) return;
            Vector2 delta = Vector2.zero;
            if (Input.GetKey(KeyCode.LeftArrow)) delta.x -= 20f;
            if (Input.GetKey(KeyCode.RightArrow)) delta.x += 20f;
            if (Input.GetKey(KeyCode.UpArrow)) delta.y -= 20f;
            if (Input.GetKey(KeyCode.DownArrow)) delta.y += 20f;
            Vector2 mouse = new(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            Rect window = RetailLocalMapLayout.ScreenRect(Screen.width, Screen.height);
            Rect canvas = new(window.x + RetailLocalMapLayout.Canvas.x,
                window.y + RetailLocalMapLayout.Canvas.y,
                RetailLocalMapLayout.Canvas.width, RetailLocalMapLayout.Canvas.height);
            if (canvas.Contains(mouse))
            {
                const float edge = 8f;
                if (mouse.x < canvas.xMin + edge) delta.x -= 8f;
                else if (mouse.x > canvas.xMax - edge) delta.x += 8f;
                if (mouse.y < canvas.yMin + edge) delta.y -= 8f;
                else if (mouse.y > canvas.yMax - edge) delta.y += 8f;
            }
            if (delta != Vector2.zero) PanBy(delta * Time.unscaledDeltaTime * 6f);
        }

        private void BuildPresentation()
        {
            _canvasRoot = new GameObject("Source-Faithful Local Map", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(SourceUiRuntime));
            _canvasRoot.transform.SetParent(transform, false);
            _canvasRoot.GetComponent<SourceUiRuntime>().BindResolver(_resolver);
            Canvas canvas = _canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 450;
            CanvasScaler scaler = _canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 1f;

            _window = AddRect("Retail Local Map Window", (RectTransform)_canvasRoot.transform,
                new Rect(0f, 0f, 800f, 400f));
            _window.anchorMin = _window.anchorMax = new Vector2(.5f, .5f);
            _window.pivot = new Vector2(.5f, .5f);
            _window.anchoredPosition = new Vector2(0f, 59f);
            AddSourceImage("World Map Interface", _window, RetailLocalMapLayout.BackgroundSourceId,
                new Rect(0f, 0f, 800f, 400f));

            RectTransform lens = AddRect("Live PC Lens", _window, RetailLocalMapLayout.PcLens);
            _pcLens = lens.gameObject.AddComponent<RetailPcLensView>();
            _pcLens.Bind(_session);
            AddSourceImage("PC Lens Frame", _window, RetailLocalMapLayout.PcLensFrameSourceId,
                RetailLocalMapLayout.PcLens);
            AddTransparentButton("Close Local Map", _window, RetailLocalMapLayout.PcLens, _controller.Close);

            _viewport = AddRect("Town Map Viewport", _window, RetailLocalMapLayout.Canvas);
            _viewport.gameObject.AddComponent<RectMask2D>();
            Image black = _viewport.gameObject.AddComponent<Image>();
            black.color = Color.black;
            black.raycastTarget = true;
            LocalMapPanTarget pan = _viewport.gameObject.AddComponent<LocalMapPanTarget>();
            pan.Bind(PanBy);
            _mapLayer = AddRect("Town Map Tiles", _viewport, new Rect(0f, 0f, 1f, 1f));

            _townButton = AddSourceButton("Map View", _window,
                RetailLocalMapLayout.TownMapButtonSourceId, RetailLocalMapLayout.TownMapButton, () =>
                {
                    if (_continentView)
                    {
                        _continentView = false;
                        _signature = int.MinValue;
                    }
                    _centeringRequired = true;
                    Synchronize();
                });
            _townButton.SetSelected(true);
            BindHint(_townButton.gameObject, 501, "Map View");
            _continentButton = AddSourceButton("Continent View", _window,
                RetailLocalMapLayout.WorldMapButtonSourceId, RetailLocalMapLayout.WorldMapButton,
                () =>
                {
                    if (!_session.LocalMap.TryReadContinent(out _, out _))
                    {
                        string unavailable = _resolver.Original.TryReadMessage("mes/WorldMap.mes", 605,
                            out string source) ? source : "The World Map is not available.";
                        _controller.ShowFeedback(unavailable);
                        return;
                    }
                    _continentView = true;
                    _centeringRequired = false;
                    _signature = int.MinValue;
                    Synchronize();
                });
            BindHint(_continentButton.gameObject, 502, "Continent View");
            SourceUiButton scroll = AddSourceButton("Scroll Map", _window,
                RetailLocalMapLayout.ScrollButtonSourceId, RetailLocalMapLayout.ScrollButton, () => { });
            scroll.SetSelected(true);
            BindHint(scroll.gameObject, 510, "Scroll Map");
            SourceUiButton travel = AddSourceButton("Travel", _window,
                RetailLocalMapLayout.TravelButtonSourceId, RetailLocalMapLayout.TravelButton, () => { });
            travel.SetInteractable(false);
            BindHint(travel.gameObject, 512, "Toggle Walking Waypoint Path");
            _canvasRoot.SetActive(false);
        }

        private void RebuildMap()
        {
            ClearMap();
            if (_projection == null) return;
            SetRect(_viewport, RetailLocalMapLayout.Canvas);
            _townButton.SetSelected(true);
            _continentButton.SetSelected(false);
            TownMapDefinition definition = _projection.Definition;
            BmpImage first;
            try { first = _session.LocalMap.ReadTile(definition, 0); }
            catch { return; }
            float tileWidth = first.Width;
            float tileHeight = first.Height;
            Vector2 mapSize = new(tileWidth * definition.Columns, tileHeight * definition.Rows);
            _mapLayer.sizeDelta = mapSize;
            for (int index = 0; index < definition.TileCount; index++)
            {
                int mask = _projection.FogCornerMask(index);
                if (mask == 0) continue;
                BmpImage bmp;
                try { bmp = index == 0 ? first : _session.LocalMap.ReadTile(definition, index); }
                catch { continue; }
                Texture2D texture = BuildTexture(bmp, mask);
                Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, bmp.Width, bmp.Height),
                    new Vector2(.5f, .5f), 1f, 0, SpriteMeshType.FullRect);
                _runtimeAssets.Add(texture);
                _runtimeAssets.Add(sprite);
                int column = index % definition.Columns;
                int row = index / definition.Columns;
                RectTransform tile = AddRect($"Town Map Tile {index:D6}", _mapLayer,
                    new Rect(column * tileWidth, row * tileHeight, tileWidth, tileHeight));
                Image image = tile.gameObject.AddComponent<Image>();
                image.sprite = sprite;
                image.raycastTarget = false;
            }
            foreach (LocalMapMarker marker in _projection.Markers)
            {
                Vector2 size = NativeSize(marker.SourceArtId, new Vector2(12f, 12f));
                AddSourceImage(marker.Kind + " Marker", _mapLayer, marker.SourceArtId,
                    new Rect(marker.Pixel - size * .5f, size));
            }
            ApplyPan();
        }

        private void RebuildContinent()
        {
            ClearMap();
            SetRect(_viewport, RetailLocalMapLayout.ContinentCanvas);
            _townButton.SetSelected(false);
            _continentButton.SetSelected(true);
            if (!_session.LocalMap.TryReadContinent(out _, out BmpImage bmp)) return;
            Texture2D texture = BuildTexture(bmp, 0xF);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, bmp.Width, bmp.Height),
                new Vector2(.5f, .5f), 1f, 0, SpriteMeshType.FullRect);
            _runtimeAssets.Add(texture);
            _runtimeAssets.Add(sprite);
            _mapLayer.sizeDelta = new Vector2(bmp.Width, bmp.Height);
            RectTransform map = AddRect("Continent Overview", _mapLayer,
                new Rect(0f, 0f, bmp.Width, bmp.Height));
            Image image = map.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            _pan = Vector2.zero;
            ApplyPan();
        }

        private static Texture2D BuildTexture(BmpImage image, int mask)
        {
            byte[] rgba = (byte[])image.Rgba.Clone();
            if (mask != 0xF)
            {
                float topLeft = (mask & 0x8) != 0 ? 1f : 0f;
                float topRight = (mask & 0x4) != 0 ? 1f : 0f;
                float bottomRight = (mask & 0x2) != 0 ? 1f : 0f;
                float bottomLeft = (mask & 0x1) != 0 ? 1f : 0f;
                for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    float tx = image.Width <= 1 ? 0f : x / (float)(image.Width - 1);
                    float ty = image.Height <= 1 ? 0f : y / (float)(image.Height - 1);
                    float bottom = Mathf.Lerp(bottomLeft, bottomRight, tx);
                    float top = Mathf.Lerp(topLeft, topRight, tx);
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Lerp(bottom, top, ty) * 255f);
                    int offset = (y * image.Width + x) * 4;
                    rgba[offset + 3] = alpha;
                }
            }
            var texture = new Texture2D(image.Width, image.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.LoadRawTextureData(rgba);
            texture.Apply(false, true);
            return texture;
        }

        private void ApplyPan()
        {
            if (_mapLayer != null) _mapLayer.anchoredPosition = new Vector2(-_pan.x, _pan.y);
        }

        private Vector2 MapSize()
            => _mapLayer != null ? _mapLayer.sizeDelta : Vector2.zero;

        private void ClearMap()
        {
            if (_mapLayer != null)
                for (int index = _mapLayer.childCount - 1; index >= 0; index--)
                {
                    GameObject child = _mapLayer.GetChild(index).gameObject;
                    child.SetActive(false);
                    if (Application.isPlaying) Object.Destroy(child); else Object.DestroyImmediate(child);
                }
            foreach (Object asset in _runtimeAssets)
                if (asset != null)
                {
                    if (Application.isPlaying) Object.Destroy(asset); else Object.DestroyImmediate(asset);
                }
            _runtimeAssets.Clear();
        }

        private void BindHint(GameObject target, int key, string fallback)
        {
            string value = _resolver.Original.TryReadMessage("mes/WorldMap.mes", key, out string source)
                ? source : fallback;
            string owner = "local-map-help-" + key;
            target.AddComponent<SourceHoverHintTarget>().Bind(
                () => _controller.ShowContextualHelp(owner, value),
                () => _controller.ClearContextualHelp(owner));
        }

        private SourceUiImage AddSourceImage(string name, RectTransform parent, int sourceId, Rect rect)
        {
            RectTransform target = AddRect(name, parent, rect);
            Image image = target.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            SourceUiImage source = target.gameObject.AddComponent<SourceUiImage>();
            source.Bind(_resolver, new UiAssetKey(sourceId));
            return source;
        }

        private SourceUiButton AddSourceButton(string name, RectTransform parent, int sourceId,
            Vector2 topLeft, Action clicked)
        {
            Vector2 size = NativeSize(sourceId, new Vector2(32f, 32f));
            RectTransform target = AddRect(name, parent, new Rect(topLeft, size));
            target.gameObject.AddComponent<Image>();
            target.gameObject.AddComponent<Button>();
            target.gameObject.AddComponent<SourceUiImage>();
            SourceUiButton source = target.gameObject.AddComponent<SourceUiButton>();
            source.Bind(_resolver, new UiAssetKey(sourceId, frame: 0), new UiAssetKey(sourceId, frame: 1),
                new UiAssetKey(sourceId, frame: 1), new UiAssetKey(sourceId, frame: 0),
                new UiAssetKey(sourceId, frame: 1));
            source.Button.onClick.AddListener(() => clicked());
            return source;
        }

        private Button AddTransparentButton(string name, RectTransform parent, Rect rect, Action clicked)
        {
            RectTransform target = AddRect(name, parent, rect);
            Image image = target.gameObject.AddComponent<Image>();
            image.color = Color.clear;
            Button button = target.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => clicked());
            return button;
        }

        private Vector2 NativeSize(int sourceId, Vector2 fallback)
            => _resolver.TryResolve(new UiAssetKey(sourceId), out UiResolvedAsset asset)
                ? asset.LogicalSize : fallback;

        private static RectTransform AddRect(string name, RectTransform parent, Rect rect)
        {
            var go = new GameObject(name, typeof(RectTransform));
            RectTransform target = go.GetComponent<RectTransform>();
            target.SetParent(parent, false);
            target.anchorMin = target.anchorMax = new Vector2(0f, 1f);
            target.pivot = new Vector2(0f, 1f);
            target.anchoredPosition = new Vector2(rect.x, -rect.y);
            target.sizeDelta = rect.size;
            return target;
        }

        private static void SetRect(RectTransform target, Rect rect)
        {
            target.anchoredPosition = new Vector2(rect.x, -rect.y);
            target.sizeDelta = rect.size;
        }

        private static int Signature(LocalMapProjection projection)
        {
            unchecked
            {
                int hash = HashCode.Combine(projection.Definition.Id, projection.Definition.Columns,
                    projection.Definition.Rows, projection.KnownTiles.Count, projection.Markers.Count);
                foreach (int value in projection.KnownTiles) hash = hash * 31 + value;
                foreach (LocalMapMarker marker in projection.Markers)
                    hash = hash * 31 + HashCode.Combine(marker.Kind, marker.Identity, marker.Pixel);
                return hash;
            }
        }

        private void OnDestroy() => Cleanup();

        private void Cleanup()
        {
            ClearMap();
            if (_canvasRoot != null)
            {
                _canvasRoot.SetActive(false);
                if (Application.isPlaying) Object.Destroy(_canvasRoot); else Object.DestroyImmediate(_canvasRoot);
            }
            _canvasRoot = null;
            _window = null;
            _viewport = null;
            _mapLayer = null;
            _projection = null;
            _townButton = null;
            _continentButton = null;
            _continentView = false;
        }
    }

    public sealed class LocalMapPanTarget : MonoBehaviour, IDragHandler
    {
        private Action<Vector2> _pan;
        public void Bind(Action<Vector2> pan) => _pan = pan;
        public void OnDrag(PointerEventData eventData) => _pan?.Invoke(-eventData.delta);
    }
}
