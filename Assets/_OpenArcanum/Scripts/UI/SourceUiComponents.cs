using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

namespace OpenArcanum.UI
{
    /// <summary>Source palette operations applied before a UI frame is presented.</summary>
    public enum SourceUiPaletteTransform
    {
        None,
        TigGrayscale,
    }

    /// <summary>Scene lifetime owner for a production skin resolver. It carries presentation resources only.</summary>
    [DisallowMultipleComponent]
    public sealed class SourceUiRuntime : MonoBehaviour
    {
        private UiSkinResolver _resolver;
        private bool _ownsResolver;

        public UiSkinResolver Resolver
        {
            get
            {
                if (_resolver == null)
                {
                    _resolver = UiSkinResolver.CreateProduction();
                    _ownsResolver = true;
                }
                return _resolver;
            }
        }

        public void BindResolver(UiSkinResolver resolver, bool takeOwnership = false)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            if (_ownsResolver) _resolver?.Dispose();
            _resolver = resolver;
            _ownsResolver = takeOwnership;
        }

        private void OnDestroy()
        {
            if (_ownsResolver) _resolver?.Dispose();
            _resolver = null;
            _ownsResolver = false;
        }
    }

    /// <summary>Common source-backed image. Geometry changes only when native logical sizing is explicitly enabled.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    public sealed class SourceUiImage : MonoBehaviour
    {
        [SerializeField] private UiAssetReference asset;
        [SerializeField] private bool applyNativeLogicalSize;

        private Image _image;
        private UiSkinResolver _resolver;
        private bool _subscribed;
        private SourceUiPaletteTransform _paletteTransform;
        private Texture2D _paletteTexture;
        private Sprite _paletteSprite;
        private readonly Dictionary<UiAssetKey, UiResolvedAsset> _preparedAssets = new();

        public UiAssetKey Key => asset.Key;
        public UiResolvedAsset CurrentAsset { get; private set; }
        public SourceUiPaletteTransform PaletteTransform => _paletteTransform;
        public Image Image => _image != null ? _image : (_image = GetComponent<Image>());
        public int PreparedAssetCount => _preparedAssets.Count;
        public bool IsBoundTo(UiSkinResolver resolver) => ReferenceEquals(_resolver, resolver);

        private void OnEnable()
        {
            if (_resolver == null)
            {
                SourceUiRuntime runtime = GetComponentInParent<SourceUiRuntime>();
                if (runtime != null) Bind(runtime.Resolver, asset.Key, applyNativeLogicalSize);
            }
            else
            {
                Subscribe();
                Refresh();
            }
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy()
        {
            Unsubscribe();
            ReleasePaletteResources();
        }

        public void Bind(UiSkinResolver resolver, UiAssetKey key, bool useNativeLogicalSize = false)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            if (ReferenceEquals(_resolver, resolver) && asset.Key.Equals(key)
                && applyNativeLogicalSize == useNativeLogicalSize)
            {
                if (CurrentAsset == null) Refresh();
                return;
            }
            Unsubscribe();
            if (!ReferenceEquals(_resolver, resolver)) _preparedAssets.Clear();
            _resolver = resolver;
            asset = new UiAssetReference(key);
            applyNativeLogicalSize = useNativeLogicalSize;
            Subscribe();
            Refresh();
        }

        public void SetKey(UiAssetKey key)
        {
            if (asset.Key.Equals(key) && CurrentAsset != null) return;
            asset = new UiAssetReference(key);
            Refresh();
        }

        public bool PrepareKey(UiAssetKey key)
        {
            if (_resolver == null) return false;
            if (_preparedAssets.TryGetValue(key, out UiResolvedAsset prepared)
                && prepared?.Sprite != null)
                return true;
            if (!_resolver.TryResolve(key, out prepared)) return false;
            _preparedAssets[key] = prepared;
            return true;
        }

        public void PrepareAsset(UiResolvedAsset prepared)
        {
            if (prepared?.Sprite == null) throw new ArgumentNullException(nameof(prepared));
            _preparedAssets[prepared.Key] = prepared;
        }

        public void SetPaletteTransform(SourceUiPaletteTransform transform)
        {
            if (_paletteTransform == transform) return;
            _paletteTransform = transform;
            Refresh();
        }

        public bool Refresh()
        {
            ReleasePaletteResources();
            if (_resolver == null)
            {
                CurrentAsset = null;
                Image.sprite = null;
                return false;
            }

            UiResolvedAsset resolved;
            if (!_preparedAssets.TryGetValue(asset.Key, out resolved)
                && !_resolver.TryResolve(asset.Key, out resolved))
            {
                CurrentAsset = null;
                Image.sprite = null;
                return false;
            }

            CurrentAsset = resolved;
            Image.sprite = _paletteTransform == SourceUiPaletteTransform.TigGrayscale
                ? CreateTigGrayscaleSprite(resolved)
                : resolved.Sprite;
            // Tint belongs to the presenter (bitmap glyph coverage, disabled icons, status overlays).
            // Refreshing or re-enabling the source asset must not silently erase that presentation state.
            ApplyScalePolicy(SourceUiScaleMetadataCatalog.Get(resolved.Key.SourceId));
            if (applyNativeLogicalSize)
                ((RectTransform)transform).sizeDelta = resolved.LogicalSize;
            return true;
        }

        private Sprite CreateTigGrayscaleSprite(UiResolvedAsset resolved)
        {
            Color32[] pixels = resolved.Texture.GetPixels32();
            for (int index = 0; index < pixels.Length; index++)
            {
                Color32 pixel = pixels[index];
                byte gray = (byte)((pixel.r * 77u + pixel.g * 150u + pixel.b * 28u) / 255u);
                pixels[index] = new Color32(gray, gray, gray, pixel.a);
            }

            _paletteTexture = new Texture2D(
                resolved.Texture.width,
                resolved.Texture.height,
                TextureFormat.RGBA32,
                mipChain: false,
                linear: false)
            {
                name = $"{resolved.Texture.name}_TigGrayscale",
                filterMode = resolved.Texture.filterMode,
                wrapMode = resolved.Texture.wrapMode,
                anisoLevel = resolved.Texture.anisoLevel,
            };
            _paletteTexture.SetPixels32(pixels);
            _paletteTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            _paletteSprite = Sprite.Create(
                _paletteTexture,
                new Rect(0f, 0f, _paletteTexture.width, _paletteTexture.height),
                resolved.Pivot,
                resolved.Sprite.pixelsPerUnit,
                extrude: 0,
                meshType: SpriteMeshType.FullRect,
                border: resolved.Sprite.border);
            _paletteSprite.name = $"{resolved.Sprite.name}_TigGrayscale";
            return _paletteSprite;
        }

        private void ReleasePaletteResources()
        {
            RetailUiAssetResolver.Destroy(_paletteSprite);
            RetailUiAssetResolver.Destroy(_paletteTexture);
            _paletteSprite = null;
            _paletteTexture = null;
        }

        private void ApplyScalePolicy(SourceUiScaleMetadata metadata)
        {
            Image.preserveAspect = metadata.Policy == SourceUiScalePolicy.Fixed;
            if (metadata.IsRepeatingTile)
                Image.type = Image.Type.Tiled;
            else if (metadata.Policy == SourceUiScalePolicy.HorizontalSlice
                     || metadata.Policy == SourceUiScalePolicy.NineSlice)
                Image.type = Image.Type.Sliced;
            else
                Image.type = Image.Type.Simple;
        }

        private void Subscribe()
        {
            if (_resolver == null || _subscribed) return;
            _resolver.PresentationInvalidated += RefreshFromInvalidation;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (_resolver != null && _subscribed)
                _resolver.PresentationInvalidated -= RefreshFromInvalidation;
            _subscribed = false;
        }

        private void RefreshFromInvalidation()
        {
            // A presentation invalidation can retain its delegate snapshot while a parent rebuild destroys this
            // glyph. Unity's native object is already gone in that narrow window, so fail closed instead of
            // touching the retired component.
            if (this == null) return;
            _preparedAssets.Clear();
            Refresh();
        }
    }

    /// <summary>
    /// Source-state button primitive. Unity Button remains the intent/callback boundary; this class selects pixels
    /// only and never owns the command which a click invokes.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(Image), typeof(Button))]
    [RequireComponent(typeof(SourceUiImage))]
    public sealed class SourceUiButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        [SerializeField] private UiAssetReference normal;
        [SerializeField] private UiAssetReference hover;
        [SerializeField] private UiAssetReference pressed;
        [SerializeField] private UiAssetReference disabled;
        [SerializeField] private UiAssetReference selected;
        [SerializeField] private bool hasSelectedState;

        private SourceUiImage _sourceImage;
        private Button _button;
        private UiSkinResolver _resolver;
        private bool _hovered;
        private bool _pressed;
        private bool _selected;

        public Button Button => _button != null ? _button : (_button = GetComponent<Button>());
        public UiAssetKey CurrentPresentationKey => SourceImage.Key;
        private SourceUiImage SourceImage =>
            _sourceImage != null ? _sourceImage : (_sourceImage = GetComponent<SourceUiImage>());

        private void Awake()
        {
            Button.transition = Selectable.Transition.None;
            Button.targetGraphic = GetComponent<Image>();
        }

        private void OnEnable() => RefreshPresentation();

        public void Bind(
            UiSkinResolver resolver,
            UiAssetKey normalKey,
            UiAssetKey hoverKey,
            UiAssetKey pressedKey,
            UiAssetKey disabledKey,
            UiAssetKey? selectedKey = null)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            normal = new UiAssetReference(normalKey);
            hover = new UiAssetReference(hoverKey);
            // Some source buttons expose only normal/active frames. TIG keeps their active frame while pressed;
            // probing a conventional third frame both warns and creates work on the hover path.
            pressed = new UiAssetReference(_resolver.Original.IsFrameAvailable(pressedKey)
                ? pressedKey
                : hoverKey);
            disabled = new UiAssetReference(disabledKey);
            hasSelectedState = selectedKey.HasValue;
            selected = new UiAssetReference(selectedKey ?? normalKey);
            SourceImage.Bind(_resolver, normalKey);
            PreparePresentationKeys();
            RefreshPresentation();
        }

        public void SetInteractable(bool value)
        {
            if (Button.interactable == value) return;
            Button.interactable = value;
            RefreshPresentation();
        }

        public void SetSelected(bool value)
        {
            if (_selected == value) return;
            _selected = value;
            RefreshPresentation();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            RefreshPresentation();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            _pressed = false;
            RefreshPresentation();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!Button.interactable) return;
            _pressed = true;
            RefreshPresentation();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
            // TIG momentary buttons return to their authored up frame when the mouse is released.
            // A later pointer-enter can show hover again; Unity keyboard/event focus must not latch
            // the visual active frame after a click.
            _hovered = false;
            RefreshPresentation();
        }

        public void OnSelect(BaseEventData eventData) { }

        public void OnDeselect(BaseEventData eventData) { }

        private void RefreshPresentation()
        {
            if (_resolver == null) return;
            UiAssetKey key;
            if (!Button.interactable) key = disabled.Key;
            else if (_pressed) key = pressed.Key;
            else if (_selected && hasSelectedState) key = selected.Key;
            else if (_hovered) key = hover.Key;
            else key = normal.Key;

            SourceImage.SetKey(key);
        }

        private void PreparePresentationKeys()
        {
            SourceImage.PrepareKey(normal.Key);
            SourceImage.PrepareKey(hover.Key);
            SourceImage.PrepareKey(pressed.Key);
            SourceImage.PrepareKey(disabled.Key);
            if (hasSelectedState) SourceImage.PrepareKey(selected.Key);
        }
    }

    /// <summary>Cursor-presentation foundation. The ART hotspot is preserved independently from pointer authority.</summary>
    [DisallowMultipleComponent]
    public sealed class SourceUiCursorPresenter : MonoBehaviour
    {
        [SerializeField] private UiAssetReference asset;
        [SerializeField] private bool applyToSystemCursor;

        private UiSkinResolver _resolver;
        private bool _subscribed;

        public Texture2D CurrentTexture { get; private set; }
        public Vector2 CurrentHotspotPixels { get; private set; }
        public UiResolvedAsset CurrentAsset { get; private set; }

        private void OnDisable()
        {
            Unsubscribe();
            if (applyToSystemCursor) Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        private void OnDestroy() => Unsubscribe();

        public void Bind(UiSkinResolver resolver, UiAssetKey key, bool applySystemCursor = false)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            Unsubscribe();
            _resolver = resolver;
            asset = new UiAssetReference(key);
            applyToSystemCursor = applySystemCursor;
            Subscribe();
            Refresh();
        }

        public bool Refresh()
        {
            if (_resolver == null || !_resolver.TryResolve(asset.Key, out UiResolvedAsset resolved)) return false;
            CurrentAsset = resolved;
            CurrentTexture = resolved.Texture;
            CurrentHotspotPixels = (Vector2)resolved.Hotspot * resolved.TextureScale;
            if (applyToSystemCursor)
                Cursor.SetCursor(CurrentTexture, CurrentHotspotPixels, CursorMode.Auto);
            return true;
        }

        private void Subscribe()
        {
            if (_resolver == null || _subscribed) return;
            _resolver.PresentationInvalidated += RefreshFromInvalidation;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (_resolver != null && _subscribed)
                _resolver.PresentationInvalidated -= RefreshFromInvalidation;
            _subscribed = false;
        }

        private void RefreshFromInvalidation() => Refresh();
    }

    /// <summary>Source-authentic 11-pixel scrollbar assembly: fixed top/bottom caps and tiled middle.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SourceUiVerticalTileAssembly : MonoBehaviour
    {
        private RectTransform _top;
        private RectTransform _middle;
        private RectTransform _bottom;

        public void Bind(UiSkinResolver resolver, UiAssetKey top, UiAssetKey middle, UiAssetKey bottom)
        {
            EnsureChildren();
            _top.GetComponent<SourceUiImage>().Bind(resolver, top);
            _middle.GetComponent<SourceUiImage>().Bind(resolver, middle);
            _bottom.GetComponent<SourceUiImage>().Bind(resolver, bottom);
            _middle.GetComponent<Image>().type = Image.Type.Tiled;
            Layout();
        }

        public void Layout()
        {
            EnsureChildren();
            RectTransform root = (RectTransform)transform;
            float height = root.rect.height > 0f ? root.rect.height : root.sizeDelta.y;
            const float topHeight = 5f;
            const float bottomHeight = 7f;
            float middleHeight = Mathf.Max(0f, height - topHeight - bottomHeight);
            Place(_top, new Vector2(11f, topHeight), new Vector2(0f, 1f), new Vector2(0f, -topHeight * .5f));
            Place(_bottom, new Vector2(11f, bottomHeight), new Vector2(0f, 0f), new Vector2(0f, bottomHeight * .5f));
            Place(_middle, new Vector2(11f, middleHeight), new Vector2(0f, .5f), Vector2.zero);
        }

        private void EnsureChildren()
        {
            if (_top == null) _top = Child("Top");
            if (_middle == null) _middle = Child("Middle");
            if (_bottom == null) _bottom = Child("Bottom");
        }

        private RectTransform Child(string name)
        {
            Transform existing = transform.Find(name);
            if (existing is RectTransform rect) return rect;
            var child = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(SourceUiImage));
            RectTransform value = child.GetComponent<RectTransform>();
            value.SetParent(transform, worldPositionStays: false);
            return value;
        }

        private static void Place(RectTransform rect, Vector2 size, Vector2 anchor, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }
    }
}
