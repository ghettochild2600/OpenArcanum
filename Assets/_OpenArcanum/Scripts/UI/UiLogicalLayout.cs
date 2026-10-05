using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OpenArcanum.UI
{
    /// <summary>
    /// Immutable mapping from the source-authentic top-left 800x600 coordinate surface to a physical target.
    /// Rect results also use top-left physical coordinates so source evidence can be compared without Y inversion.
    /// </summary>
    public readonly struct UiLogicalMapping
    {
        public const float ReferenceWidth = 800f;
        public const float ReferenceHeight = 600f;
        public const float TopInterfaceHeight = 41f;
        public const float ManagementHeight = 400f;
        public const float BottomInterfaceHeight = 159f;

        public Vector2Int TargetSize { get; }
        public float Scale { get; }
        public Vector2 Origin { get; }
        public Rect ReferenceScreenRect { get; }
        public Rect GameplayWorldScreenRect { get; }

        private UiLogicalMapping(Vector2Int targetSize, float scale, Vector2 origin)
        {
            TargetSize = targetSize;
            Scale = scale;
            Origin = origin;
            ReferenceScreenRect = new Rect(
                origin.x,
                origin.y,
                ReferenceWidth * scale,
                ReferenceHeight * scale);
            GameplayWorldScreenRect = new Rect(
                0f,
                origin.y + TopInterfaceHeight * scale,
                targetSize.x,
                ManagementHeight * scale);
        }

        public static UiLogicalMapping ForResolution(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            float scale = Mathf.Min(width / ReferenceWidth, height / ReferenceHeight);
            var origin = new Vector2(
                (width - ReferenceWidth * scale) * .5f,
                (height - ReferenceHeight * scale) * .5f);
            return new UiLogicalMapping(new Vector2Int(width, height), scale, origin);
        }

        public Vector2 LogicalToScreen(Vector2 sourceTopLeft)
            => Origin + sourceTopLeft * Scale;

        public Vector2 ScreenToLogical(Vector2 screenTopLeft)
            => (screenTopLeft - Origin) / Scale;

        public Rect LogicalToScreen(Rect sourceTopLeft)
            => new Rect(
                Origin.x + sourceTopLeft.x * Scale,
                Origin.y + sourceTopLeft.y * Scale,
                sourceTopLeft.width * Scale,
                sourceTopLeft.height * Scale);
    }

    public enum SourceUiLayer
    {
        World,
        Hud,
        Modal,
        Dialogue,
        Tooltip,
        Cursor,
    }

    /// <summary>
    /// Presentation hierarchy shared by later screens. It owns no screen/modal state; controllers decide which views
    /// are active. The world layer fills the target while source UI layers share the centered logical surface.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler))]
    public sealed class SourceUiPresentationRoot : MonoBehaviour
    {
        private readonly Dictionary<SourceUiLayer, RectTransform> _layers =
            new Dictionary<SourceUiLayer, RectTransform>();
        private RectTransform _referenceSurface;
        private Vector2Int _lastResolution;

        public UiLogicalMapping Mapping { get; private set; }
        public RectTransform ReferenceSurface => _referenceSurface;

        private void Awake()
        {
            ConfigureCanvas();
            EnsureHierarchy();
            ApplyResolution(new Vector2Int(Screen.width, Screen.height));
        }

        private void LateUpdate()
        {
            var current = new Vector2Int(Screen.width, Screen.height);
            if (current != _lastResolution) ApplyResolution(current);
        }

        public RectTransform GetLayer(SourceUiLayer layer)
        {
            EnsureHierarchy();
            return _layers[layer];
        }

        public void ApplyResolution(Vector2Int targetSize)
        {
            EnsureHierarchy();
            Mapping = UiLogicalMapping.ForResolution(targetSize.x, targetSize.y);
            _lastResolution = targetSize;
            _referenceSurface.sizeDelta = new Vector2(
                UiLogicalMapping.ReferenceWidth,
                UiLogicalMapping.ReferenceHeight);
            _referenceSurface.localScale = Vector3.one * Mapping.Scale;
            _referenceSurface.anchoredPosition = Vector2.zero;
        }

        private void ConfigureCanvas()
        {
            Canvas canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 1f;
        }

        private void EnsureHierarchy()
        {
            if (_referenceSurface != null && _layers.Count == 6) return;
            RectTransform root = (RectTransform)transform;
            Stretch(root);

            RectTransform world = FindOrCreate(root, "WorldLayer");
            Stretch(world);
            _layers[SourceUiLayer.World] = world;

            _referenceSurface = FindOrCreate(root, "SourceReference800x600");
            _referenceSurface.anchorMin = _referenceSurface.anchorMax = new Vector2(.5f, .5f);
            _referenceSurface.pivot = new Vector2(.5f, .5f);

            CreateReferenceLayer(SourceUiLayer.Hud, "HudLayer");
            CreateReferenceLayer(SourceUiLayer.Modal, "ModalLayer");
            CreateReferenceLayer(SourceUiLayer.Dialogue, "DialogueLayer");
            CreateReferenceLayer(SourceUiLayer.Tooltip, "TooltipLayer");
            CreateReferenceLayer(SourceUiLayer.Cursor, "CursorLayer");
        }

        private void CreateReferenceLayer(SourceUiLayer layer, string name)
        {
            RectTransform value = FindOrCreate(_referenceSurface, name);
            Stretch(value);
            _layers[layer] = value;
        }

        private static RectTransform FindOrCreate(RectTransform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing is RectTransform rect) return rect;
            var child = new GameObject(name, typeof(RectTransform));
            RectTransform created = child.GetComponent<RectTransform>();
            created.SetParent(parent, worldPositionStays: false);
            return created;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}
