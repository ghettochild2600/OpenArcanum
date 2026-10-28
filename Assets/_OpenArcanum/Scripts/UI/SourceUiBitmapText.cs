using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OpenArcanum.UI
{
    /// <summary>
    /// Renders source text from an ART bitmap-font family. TIG maps a single-byte character to frame
    /// <c>character - 31</c> and advances by the frame hotspot X; this component preserves that mapping and logical
    /// geometry while allowing the source's multi-pass tinting to be assembled by a screen presenter.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SourceUiBitmapText : MonoBehaviour
    {
        private readonly struct TextLayoutKey : IEquatable<TextLayoutKey>
        {
            private readonly int _fontSourceId;
            private readonly string _text;

            public TextLayoutKey(int fontSourceId, string text)
            {
                _fontSourceId = fontSourceId;
                _text = text;
            }

            public bool Equals(TextLayoutKey other)
                => _fontSourceId == other._fontSourceId && string.Equals(_text, other._text,
                    StringComparison.Ordinal);
            public override bool Equals(object obj) => obj is TextLayoutKey other && Equals(other);
            public override int GetHashCode() => (_fontSourceId * 397) ^ (_text?.GetHashCode() ?? 0);
        }

        private readonly struct PreparedGlyph
        {
            public readonly UiResolvedAsset Asset;
            public readonly Vector2 Position;

            public PreparedGlyph(UiResolvedAsset asset, Vector2 position)
            {
                Asset = asset;
                Position = position;
            }
        }

        private sealed class PreparedTextLayout
        {
            public PreparedGlyph[] Glyphs;
            public Vector2 Size;
            public int SourceMeasuredWidth;
        }

        public const string TigBitmapFontShaderName = "OpenArcanum/UI/TIG Bitmap Font";
        public const int Morph15FontSourceId = 27;
        public const int RollerFontSourceId = 25;
        public const int Pork12FontSourceId = 28;
        public const int Flare12FontSourceId = 229;
        public const int Cloister18FontSourceId = 171;
        public const int Nick16FontSourceId = 301;
        public const int Garmond9FontSourceId = 483;
        public const int InventoryStatFontSourceId = 486;
        public const int DefaultFontSourceId = 327;

        private static Material _tigBitmapFontMaterial;
        private readonly List<Image> _glyphs = new List<Image>();
        private readonly Dictionary<TextLayoutKey, PreparedTextLayout> _preparedLayouts = new();
        private UiSkinResolver _resolver;
        private string _text = string.Empty;
        private int _fontSourceId = DefaultFontSourceId;
        private Color _tint = Color.white;
        private bool _subscribed;
        private int _sourceMeasuredWidth;

        public string Text => _text;
        public int FontSourceId => _fontSourceId;
        public Vector2 LogicalSize => ((RectTransform)transform).sizeDelta;
        public int SourceMeasuredWidth => _sourceMeasuredWidth;
        public int GlyphPresenterCount => _glyphs.Count;
        public int PreparedLayoutCount => _preparedLayouts.Count;
        public int ActiveGlyphPresenterCount
        {
            get
            {
                int count = 0;
                foreach (Image glyph in _glyphs)
                    if (glyph != null && glyph.gameObject.activeSelf) count++;
                return count;
            }
        }

        public void EnsureGlyphPresenterCapacity(int capacity)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            for (int index = _glyphs.Count - 1; index >= 0; index--)
                if (_glyphs[index] == null) _glyphs.RemoveAt(index);
            while (_glyphs.Count < capacity)
                CreateGlyphPresenter().gameObject.SetActive(false);
        }

        /// <summary>
        /// Resolves and caches one immutable source-font layout without changing the currently displayed text.
        /// Screens with a finite hover vocabulary use this during presenter construction so pointer entry performs
        /// no glyph lookup, sprite resolution, or presenter growth.
        /// </summary>
        public void PrepareLayout(string text, int fontSourceId)
        {
            if (_resolver == null) throw new InvalidOperationException("Bind a resolver before preparing text.");
            GetOrCreateLayout(fontSourceId, text ?? string.Empty);
        }

        public void Bind(UiSkinResolver resolver, string text, Color tint, int fontSourceId = DefaultFontSourceId)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            string nextText = text ?? string.Empty;
            bool resolverChanged = !ReferenceEquals(_resolver, resolver);
            bool contentChanged = resolverChanged || _text != nextText || _fontSourceId != fontSourceId;
            if (!contentChanged)
            {
                if (_tint != tint) SetTint(tint);
                Subscribe();
                return;
            }
            if (resolverChanged) Unsubscribe();
            _resolver = resolver;
            _text = nextText;
            _fontSourceId = fontSourceId;
            _tint = tint;
            Rebuild();
            Subscribe();
        }

        public void SetTint(Color tint)
        {
            _tint = tint;
            ApplyTint();
        }

        private void OnDisable() => Unsubscribe();

        private void OnEnable()
        {
            if (_resolver != null) Subscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            _glyphs.Clear();
            _preparedLayouts.Clear();
        }

        private void Rebuild()
        {
            if (_resolver == null) return;
            PreparedTextLayout layout = GetOrCreateLayout(_fontSourceId, _text);

            EnsureGlyphPresenterCapacity(layout.Glyphs.Length);
            for (int index = 0; index < layout.Glyphs.Length; index++)
            {
                PreparedGlyph prepared = layout.Glyphs[index];
                Image image = _glyphs[index];
                RectTransform rect = (RectTransform)image.transform;
                SourceUiImage source = image.GetComponent<SourceUiImage>();
                if (!image.gameObject.activeSelf) image.gameObject.SetActive(true);
                if (rect.anchoredPosition != prepared.Position) rect.anchoredPosition = prepared.Position;
                if (rect.sizeDelta != prepared.Asset.LogicalSize) rect.sizeDelta = prepared.Asset.LogicalSize;
                if (!source.IsBoundTo(_resolver)) source.Bind(_resolver, prepared.Asset.Key);
                else
                {
                    source.PrepareAsset(prepared.Asset);
                    source.SetKey(prepared.Asset.Key);
                }
                image.raycastTarget = false;
                if (image.material != TigBitmapFontMaterial) image.material = TigBitmapFontMaterial;
                if (image.color != _tint) image.color = _tint;
            }

            for (int index = layout.Glyphs.Length; index < _glyphs.Count; index++)
                if (_glyphs[index] != null) _glyphs[index].gameObject.SetActive(false);

            RectTransform target = (RectTransform)transform;
            if (target.sizeDelta != layout.Size) target.sizeDelta = layout.Size;
            _sourceMeasuredWidth = layout.SourceMeasuredWidth;
        }

        private PreparedTextLayout GetOrCreateLayout(int fontSourceId, string text)
        {
            var key = new TextLayoutKey(fontSourceId, text);
            if (_preparedLayouts.TryGetValue(key, out PreparedTextLayout layout)) return layout;

            var prepared = new List<PreparedGlyph>(text.Length);
            float advance = 0f;
            float height = 0f;
            int measuredWidth = 0;
            for (int index = 0; index < text.Length; index++)
            {
                char character = text[index];
                if (character == '\n' || character == '\r') continue;
                int frame = (byte)character - 31;
                if (frame < 0 || !_resolver.TryResolve(new UiAssetKey(fontSourceId, frame: frame),
                        out UiResolvedAsset asset))
                    continue;
                prepared.Add(new PreparedGlyph(asset, new Vector2(advance, 0f)));
                float glyphAdvance = asset.Hotspot.x > 0 ? asset.Hotspot.x : asset.LogicalSize.x;
                advance += glyphAdvance;
                measuredWidth += index + 1 < text.Length
                    ? Mathf.RoundToInt(glyphAdvance)
                    : asset.LogicalSize.x;
                height = Mathf.Max(height, asset.LogicalSize.y);
            }

            layout = new PreparedTextLayout
            {
                Glyphs = prepared.ToArray(),
                Size = new Vector2(advance, height),
                SourceMeasuredWidth = measuredWidth,
            };
            _preparedLayouts.Add(key, layout);
            return layout;
        }

        private Image CreateGlyphPresenter()
        {
            var glyph = new GameObject("Glyph Pool",
                typeof(RectTransform), typeof(Image), typeof(SourceUiImage));
            RectTransform rect = glyph.GetComponent<RectTransform>();
            rect.SetParent(transform, worldPositionStays: false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            Image image = glyph.GetComponent<Image>();
            image.raycastTarget = false;
            image.material = TigBitmapFontMaterial;
            image.color = _tint;
            _glyphs.Add(image);
            return image;
        }

        private void ApplyTint()
        {
            foreach (Image glyph in _glyphs)
                if (glyph != null && glyph.color != _tint) glyph.color = _tint;
        }

        private static Material TigBitmapFontMaterial
        {
            get
            {
                if (_tigBitmapFontMaterial != null) return _tigBitmapFontMaterial;
                Shader shader = Resources.Load<Shader>("TigBitmapFont");
                if (shader == null || shader.name != TigBitmapFontShaderName)
                    throw new InvalidOperationException($"Missing source bitmap-font shader '{TigBitmapFontShaderName}'.");
                _tigBitmapFontMaterial = new Material(shader)
                {
                    name = "TIG Bitmap Font (Runtime)",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                return _tigBitmapFontMaterial;
            }
        }

        private void Subscribe()
        {
            if (_resolver == null || _subscribed) return;
            _resolver.PresentationInvalidated += HandlePresentationInvalidated;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (_resolver != null && _subscribed)
                _resolver.PresentationInvalidated -= HandlePresentationInvalidated;
            _subscribed = false;
        }

        private void HandlePresentationInvalidated()
        {
            // Parent presenters may rebuild their hierarchy while the resolver is walking a multicast delegate
            // snapshot. Unity has already retired this native component in that narrow window, even though this
            // managed callback is still present in the snapshot.
            if (this == null) return;
            try
            {
                _preparedLayouts.Clear();
                Rebuild();
            }
            catch (MissingReferenceException)
            {
                // A parent rebuild can retire this native component after the callback has entered but before its
                // glyph hierarchy is touched. The replacement hierarchy owns the new presentation.
            }
        }

    }
}
