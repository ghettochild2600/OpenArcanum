using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

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
        public const int DefaultFontSourceId = 327;

        private readonly List<Image> _glyphs = new List<Image>();
        private UiSkinResolver _resolver;
        private string _text = string.Empty;
        private int _fontSourceId = DefaultFontSourceId;
        private Color _tint = Color.white;
        private bool _subscribed;

        public string Text => _text;
        public int FontSourceId => _fontSourceId;
        public Vector2 LogicalSize => ((RectTransform)transform).sizeDelta;

        public void Bind(UiSkinResolver resolver, string text, Color tint, int fontSourceId = DefaultFontSourceId)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            Unsubscribe();
            _resolver = resolver;
            _text = text ?? string.Empty;
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

        private void OnDestroy() => Unsubscribe();

        private void Rebuild()
        {
            ClearGlyphs();
            if (_resolver == null) return;

            float advance = 0f;
            float height = 0f;
            foreach (char character in _text)
            {
                if (character == '\n' || character == '\r') continue;
                int frame = (byte)character - 31;
                if (frame < 0 || !_resolver.TryResolve(new UiAssetKey(_fontSourceId, frame: frame),
                        out UiResolvedAsset asset))
                    continue;

                var glyph = new GameObject($"Glyph {(int)character:D3}",
                    typeof(RectTransform), typeof(Image), typeof(SourceUiImage));
                RectTransform rect = glyph.GetComponent<RectTransform>();
                rect.SetParent(transform, worldPositionStays: false);
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(advance, 0f);
                rect.sizeDelta = asset.LogicalSize;

                SourceUiImage source = glyph.GetComponent<SourceUiImage>();
                source.Bind(_resolver, asset.Key);
                Image image = glyph.GetComponent<Image>();
                image.raycastTarget = false;
                image.color = _tint;
                _glyphs.Add(image);

                float glyphAdvance = asset.Hotspot.x > 0 ? asset.Hotspot.x : asset.LogicalSize.x;
                advance += glyphAdvance;
                height = Mathf.Max(height, asset.LogicalSize.y);
            }

            ((RectTransform)transform).sizeDelta = new Vector2(advance, height);
            ApplyTint();
        }

        private void ApplyTint()
        {
            foreach (Image glyph in _glyphs)
                if (glyph != null) glyph.color = _tint;
        }

        private void Subscribe()
        {
            if (_resolver == null || _subscribed) return;
            _resolver.PresentationInvalidated += ApplyTint;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (_resolver != null && _subscribed)
                _resolver.PresentationInvalidated -= ApplyTint;
            _subscribed = false;
        }

        private void ClearGlyphs()
        {
            _glyphs.Clear();
            for (int index = transform.childCount - 1; index >= 0; index--)
            {
                GameObject child = transform.GetChild(index).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Object.Destroy(child); else Object.DestroyImmediate(child);
            }
        }
    }
}
