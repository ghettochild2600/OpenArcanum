using System;
using OpenArcanum.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Arcanum.Runtime.UI
{
    /// <summary>
    /// Native-pixel placement shared by retail partial Main Menu windows. The project visual target keeps the
    /// source top cover at the physical top, the partial 800x400 window centered by its 800x600 source offset,
    /// and the source bottom cover at the physical bottom.
    /// </summary>
    public static class RetailPartialMainMenuLayout
    {
        public const int TopCoverSourceId = 336;
        public const int BottomCoverSourceId = 335;
        public static readonly Rect TopCover = new(0f, 0f, 800f, 41f);
        public static readonly Rect CenterContent = new(0f, 41f, 800f, 400f);
        public static readonly Rect BottomCover = new(0f, 441f, 800f, 159f);

        public static Rect TopScreenRect(int width, int height)
        {
            ValidateResolution(width, height);
            return new Rect((width - 800f) * .5f, 0f, 800f, 41f);
        }

        public static Rect CenterScreenRect(int width, int height)
        {
            ValidateResolution(width, height);
            return new Rect((width - 800f) * .5f, 41f + (height - 600f) * .5f, 800f, 400f);
        }

        public static Rect BottomScreenRect(int width, int height)
        {
            ValidateResolution(width, height);
            return new Rect((width - 800f) * .5f, height - 159f, 800f, 159f);
        }

        public static Vector2 CenterLocal(Vector2 sourceTopLeft)
            => new(sourceTopLeft.x - CenterContent.x, sourceTopLeft.y - CenterContent.y);

        // Bottom-routed MainMenuButtonInfo records already store y local to the source bottom cover. The source adds
        // 441 only while choosing the owning cover window and subtracts only the selected cover's x origin.
        public static Vector2 BottomLocal(Vector2 sourceTopLeft) => sourceTopLeft;

        private static void ValidateResolution(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        }
    }

    public readonly struct RetailPartialMainMenuRegion
    {
        public RectTransform Root { get; }
        public SourceUiImage Background { get; }

        public RetailPartialMainMenuRegion(RectTransform root, SourceUiImage background)
        {
            Root = root;
            Background = background;
        }
    }

    /// <summary>
    /// Builds the three independent source windows used by partial Main Menu screens. It owns presentation only;
    /// screen state and commands stay with the caller.
    /// </summary>
    public static class RetailPartialMainMenuComposition
    {
        public static void ConfigureNativeCanvas(GameObject canvasRoot, UiSkinResolver resolver, int sortingOrder)
        {
            if (canvasRoot == null) throw new ArgumentNullException(nameof(canvasRoot));
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            canvasRoot.GetComponent<SourceUiRuntime>().BindResolver(resolver);
            Canvas canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 1f;
        }

        public static RetailPartialMainMenuRegion CreateTop(
            RectTransform canvas, UiSkinResolver resolver, string name = "Partial Main Menu Top Region")
            => Create(name, canvas, resolver, RetailPartialMainMenuLayout.TopCoverSourceId,
                RetailPartialMainMenuLayout.TopCover.size, new Vector2(.5f, 1f), new Vector2(.5f, 1f), Vector2.zero);

        public static RetailPartialMainMenuRegion CreateCenter(RectTransform canvas, UiSkinResolver resolver,
            int sourceId, string name)
            => Create(name, canvas, resolver, sourceId, RetailPartialMainMenuLayout.CenterContent.size,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, 59f));

        public static RetailPartialMainMenuRegion CreateBottom(
            RectTransform canvas, UiSkinResolver resolver, string name)
            => Create(name, canvas, resolver, RetailPartialMainMenuLayout.BottomCoverSourceId,
                RetailPartialMainMenuLayout.BottomCover.size, new Vector2(.5f, 0f), new Vector2(.5f, 0f), Vector2.zero);

        private static RetailPartialMainMenuRegion Create(string name, RectTransform canvas,
            UiSkinResolver resolver, int sourceId, Vector2 size, Vector2 anchor, Vector2 pivot, Vector2 position)
        {
            var rootObject = new GameObject(name, typeof(RectTransform));
            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.SetParent(canvas, false);
            root.anchorMin = root.anchorMax = anchor;
            root.pivot = pivot;
            root.anchoredPosition = position;
            root.sizeDelta = size;

            var backgroundObject = new GameObject(name + " Background",
                typeof(RectTransform), typeof(Image), typeof(SourceUiImage));
            RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.SetParent(root, false);
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            backgroundObject.GetComponent<Image>().raycastTarget = false;
            SourceUiImage background = backgroundObject.GetComponent<SourceUiImage>();
            background.Bind(resolver, new UiAssetKey(sourceId));
            return new RetailPartialMainMenuRegion(root, background);
        }
    }
}
