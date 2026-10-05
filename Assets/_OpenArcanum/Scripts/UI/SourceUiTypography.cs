using UnityEngine;
using UnityEngine.UI;

namespace OpenArcanum.UI
{
    /// <summary>
    /// Common dynamic-text roles. These are a bounded starting map, not final per-screen typography tuning and do not
    /// bundle or redistribute any retail font resource.
    /// </summary>
    public enum SourceUiTextRole
    {
        Body,
        Label,
        Title,
        Highlight,
        Disabled,
        Error,
    }

    public readonly struct SourceUiTextStyle
    {
        public int LogicalSize { get; }
        public Color Color { get; }
        public TextAnchor Alignment { get; }
        public FontStyle FontStyle { get; }
        public bool Shadow { get; }
        public Color ShadowColor { get; }
        public Vector2 ShadowDistance { get; }

        public SourceUiTextStyle(
            int logicalSize,
            Color color,
            TextAnchor alignment,
            FontStyle fontStyle,
            bool shadow,
            Color shadowColor,
            Vector2 shadowDistance)
        {
            LogicalSize = logicalSize;
            Color = color;
            Alignment = alignment;
            FontStyle = fontStyle;
            Shadow = shadow;
            ShadowColor = shadowColor;
            ShadowDistance = shadowDistance;
        }
    }

    public static class SourceUiTypographyCatalog
    {
        private static readonly Color SourceIvory = new Color32(231, 217, 174, 255);
        private static readonly Color SourceGold = new Color32(221, 179, 84, 255);
        private static readonly Color SourceDisabled = new Color32(117, 108, 88, 255);
        private static readonly Color SourceError = new Color32(215, 88, 72, 255);
        private static readonly Color SourceShadow = new Color32(20, 14, 10, 210);

        public static SourceUiTextStyle Get(SourceUiTextRole role)
        {
            switch (role)
            {
                case SourceUiTextRole.Title:
                    return Style(18, SourceGold, TextAnchor.MiddleCenter, FontStyle.Bold);
                case SourceUiTextRole.Highlight:
                    return Style(12, SourceGold, TextAnchor.MiddleLeft, FontStyle.Normal);
                case SourceUiTextRole.Disabled:
                    return Style(12, SourceDisabled, TextAnchor.MiddleLeft, FontStyle.Normal);
                case SourceUiTextRole.Error:
                    return Style(12, SourceError, TextAnchor.MiddleLeft, FontStyle.Bold);
                case SourceUiTextRole.Label:
                    return Style(12, SourceIvory, TextAnchor.MiddleLeft, FontStyle.Bold);
                default:
                    return Style(12, SourceIvory, TextAnchor.UpperLeft, FontStyle.Normal);
            }
        }

        private static SourceUiTextStyle Style(
            int size,
            Color color,
            TextAnchor alignment,
            FontStyle fontStyle)
            => new SourceUiTextStyle(
                size,
                color,
                alignment,
                fontStyle,
                shadow: true,
                shadowColor: SourceShadow,
                shadowDistance: new Vector2(1f, -1f));
    }

    /// <summary>Applies source-space dynamic typography without baking labels into artwork.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Text), typeof(Shadow))]
    public sealed class SourceUiText : MonoBehaviour
    {
        [SerializeField] private SourceUiTextRole role;

        public SourceUiTextRole Role
        {
            get => role;
            set
            {
                role = value;
                Apply();
            }
        }

        private void Awake() => Apply();
        private void OnValidate() => Apply();

        public void Apply()
        {
            Text text = GetComponent<Text>();
            if (text == null) return;
            SourceUiTextStyle style = SourceUiTypographyCatalog.Get(role);
            text.fontSize = style.LogicalSize;
            text.color = style.Color;
            text.alignment = style.Alignment;
            text.fontStyle = style.FontStyle;
            text.supportRichText = true;
            text.resizeTextForBestFit = false;

            Shadow shadow = GetComponent<Shadow>();
            if (shadow != null)
            {
                shadow.enabled = style.Shadow;
                shadow.effectColor = style.ShadowColor;
                shadow.effectDistance = style.ShadowDistance;
                shadow.useGraphicAlpha = true;
            }
        }
    }
}
