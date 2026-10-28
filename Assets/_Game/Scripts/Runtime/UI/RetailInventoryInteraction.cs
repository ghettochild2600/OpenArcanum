using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Economy;
using Arcanum.Runtime.World;
using OpenArcanum.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Arcanum.Runtime.UI
{
    /// <summary>Source rectangles from inven_ui.c for barter, container and corpse inventory modes.</summary>
    public static class RetailInventoryInteractionLayout
    {
        public const int LootBackgroundSourceId = 234;
        public const int BarterBackgroundSourceId = 235;
        public const int PlayerInventorySourceId = 221;
        public const int TakeAllSourceId = 808;
        public const int GridColumns = 10;
        public const float CellSize = 32f;
        public static readonly Rect Window = new(0f, 0f, 800f, 400f);
        public static readonly Rect PlayerInventory = new(358f, 0f, 442f, 400f);
        public static readonly Rect PlayerGrid = new(368f, 8f, 320f, 384f);
        public static readonly Rect LootGrid = new(8f, 136f, 320f, 256f);
        public static readonly Rect LootScrollbar = new(330f, 136f, 17f, 256f);
        public static readonly Rect BarterGrid = new(8f, 168f, 320f, 224f);
        public static readonly Rect BarterScrollbar = new(330f, 168f, 17f, 224f);
        public static readonly Rect TakeAll = new(301f, 47f, 32f, 32f);

        public static Rect ScreenRect(int width, int height)
            => new((width - 800f) * .5f, 41f + (height - 600f) * .5f, 800f, 400f);

        public static Rect ItemRect(Rect grid, int location, InventoryFootprint footprint, int rowOffset = 0)
        {
            int bounded = Mathf.Max(0, location);
            return new Rect(grid.x + CellSize * (bounded % GridColumns),
                grid.y + CellSize * (bounded / GridColumns - rowOffset),
                CellSize * Mathf.Max(1, footprint.Width), CellSize * Mathf.Max(1, footprint.Height));
        }
    }

    /// <summary>
    /// Runtime source inventory modes. The view owns no inventory or economy state; every transfer and quote
    /// is submitted to GameUiController and its existing authoritative services.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RetailInventoryInteractionView : MonoBehaviour
    {
        private static readonly Color SourceText = new Color32(231, 217, 174, 255);
        private GameUiController _controller;
        private UiSkinResolver _resolver;
        private GameObject _canvasRoot;
        private RectTransform _window;
        private RectTransform _dynamic;
        private RectTransform _targetDrop;
        private RectTransform _playerDrop;
        private SourceUiImage _background;
        private Scrollbar _scrollbar;
        private GameObject _takeAll;
        private int _scrollRow;
        private int _maximumScrollRow;
        private int _signature = int.MinValue;
        private bool _dirty = true;

        public bool IsAvailable => _canvasRoot != null && _resolver != null;
        public bool IsVisible => IsAvailable && _canvasRoot.activeSelf;
        public RectTransform WindowTransform => _window;
        public int DynamicItemCount { get; private set; }
        public int ScrollRow => _scrollRow;

        public void Bind(GameUiController controller, UiSkinResolver resolver)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            if (_controller == controller && ReferenceEquals(_resolver, resolver) && IsAvailable) return;
            Cleanup();
            _controller = controller;
            _resolver = resolver;
            _resolver.PresentationInvalidated += Invalidate;
            BuildPresentation();
            Synchronize();
        }

        public void Synchronize()
        {
            if (!IsAvailable || _controller == null) return;
            bool visible = _controller.HasPlayer && _controller.Screen is GameUiScreen.Container
                or GameUiScreen.Corpse or GameUiScreen.Merchant or GameUiScreen.FollowerInventory;
            if (_canvasRoot.activeSelf != visible) _canvasRoot.SetActive(visible);
            if (!visible) return;

            bool merchantMode = _controller.Screen == GameUiScreen.Merchant;
            bool barterPresentation = merchantMode || _controller.Screen == GameUiScreen.FollowerInventory;
            _takeAll.SetActive(!barterPresentation);
            _background.SetKey(new UiAssetKey(barterPresentation
                ? RetailInventoryInteractionLayout.BarterBackgroundSourceId
                : RetailInventoryInteractionLayout.LootBackgroundSourceId));
            Rect targetGrid = barterPresentation ? RetailInventoryInteractionLayout.BarterGrid
                : RetailInventoryInteractionLayout.LootGrid;
            Place(_targetDrop, targetGrid);
            Place((RectTransform)_scrollbar.transform, barterPresentation
                ? RetailInventoryInteractionLayout.BarterScrollbar
                : RetailInventoryInteractionLayout.LootScrollbar);

            IReadOnlyList<GameUiMerchantItemView> merchant = merchantMode
                ? _controller.ProjectMerchantWares() : Array.Empty<GameUiMerchantItemView>();
            IReadOnlyList<GameUiItemView> target = merchantMode
                ? merchant.Select(value => value.Item).ToArray()
                : _controller.ProjectOpenInventoryTarget();
            IReadOnlyList<GameUiItemView> player = merchantMode
                ? _controller.ProjectPlayerSaleItems().Select(value => value.Item).ToArray()
                : _controller.ProjectInventory().Where(value => !value.IsEquipped).ToArray();
            _maximumScrollRow = MaximumScrollRow(target, Mathf.FloorToInt(targetGrid.height / 32f));
            _scrollRow = Mathf.Clamp(_scrollRow, 0, _maximumScrollRow);
            _scrollbar.gameObject.SetActive(_maximumScrollRow > 0);
            _scrollbar.SetValueWithoutNotify(_maximumScrollRow == 0 ? 1f
                : 1f - (float)_scrollRow / _maximumScrollRow);
            _scrollbar.size = _maximumScrollRow == 0 ? 1f
                : Mathf.Clamp01((targetGrid.height / 32f) / (_maximumScrollRow + targetGrid.height / 32f));

            int signature = ProjectionSignature(merchantMode, target, player, merchant);
            if (_dirty || signature != _signature)
            {
                _dirty = false;
                _signature = signature;
                Rebuild(merchantMode, targetGrid, target, player, merchant);
            }
        }

        public bool ContainsScreenPoint(Vector2 screenBottomLeft)
        {
            if (!IsVisible) return false;
            Vector2 topLeft = new(screenBottomLeft.x, Screen.height - screenBottomLeft.y);
            return RetailInventoryInteractionLayout.ScreenRect(Screen.width, Screen.height).Contains(topLeft);
        }

        private void BuildPresentation()
        {
            _canvasRoot = new GameObject("Source-Faithful Inventory Interaction",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
                typeof(SourceUiRuntime));
            _canvasRoot.transform.SetParent(transform, false);
            _canvasRoot.GetComponent<SourceUiRuntime>().BindResolver(_resolver);
            Canvas canvas = _canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 451;
            CanvasScaler scaler = _canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 1f;

            _window = AddRect("Retail Inventory Interaction Window", (RectTransform)_canvasRoot.transform,
                RetailInventoryInteractionLayout.Window);
            _window.anchorMin = _window.anchorMax = new Vector2(.5f, .5f);
            _window.pivot = new Vector2(.5f, .5f);
            _window.anchoredPosition = new Vector2(0f, 59f);
            _background = AddSourceImage("Mode Background", _window,
                RetailInventoryInteractionLayout.LootBackgroundSourceId,
                RetailInventoryInteractionLayout.Window);
            AddSourceImage("Player Inventory", _window,
                RetailInventoryInteractionLayout.PlayerInventorySourceId,
                RetailInventoryInteractionLayout.PlayerInventory);

            _targetDrop = AddInputRegion("Target Inventory", _window,
                RetailInventoryInteractionLayout.LootGrid);
            _targetDrop.gameObject.AddComponent<RetailStartingEquipmentScrollTarget>().Bind(Scroll);
            _playerDrop = AddInputRegion("Player Inventory", _window,
                RetailInventoryInteractionLayout.PlayerGrid);
            _dynamic = AddRect("Inventory Mode Items", _window, RetailInventoryInteractionLayout.Window);
            _scrollbar = AddScrollbar(_window, RetailInventoryInteractionLayout.LootScrollbar);
            _takeAll = AddSourceButton("Take All", _window, RetailInventoryInteractionLayout.TakeAllSourceId,
                RetailInventoryInteractionLayout.TakeAll, () =>
                {
                    if (_controller.Screen is GameUiScreen.Container or GameUiScreen.Corpse
                        or GameUiScreen.FollowerInventory)
                        _controller.TakeAllOpenInventory();
                    _dirty = true;
                    Synchronize();
                }).gameObject;
        }

        private void Rebuild(bool barter, Rect targetGrid, IReadOnlyList<GameUiItemView> target,
            IReadOnlyList<GameUiItemView> player, IReadOnlyList<GameUiMerchantItemView> merchant)
        {
            ClearDynamic();
            DynamicItemCount = 0;
            RectTransform targetLayer = AddClippedLayer("Target Items", targetGrid);
            RectTransform playerLayer = AddClippedLayer("Player Items", RetailInventoryInteractionLayout.PlayerGrid);
            var merchantById = merchant.ToDictionary(value => value.Item.Identity);
            foreach (GameUiItemView item in target)
            {
                Rect rect = LocalItemRect(targetGrid, item, _scrollRow);
                if (rect.yMax <= 0f || rect.y >= targetGrid.height) continue;
                AddItem(targetLayer, item, rect, () =>
                {
                    if (barter) _controller.Trade(EconomyTransactionDirection.BuyFromMerchant, item.Identity);
                    else _controller.TransferOpenInventoryItem(item.Identity);
                    _dirty = true;
                    Synchronize();
                }, _playerDrop, barter ? merchantById[item.Identity] : null);
                DynamicItemCount++;
            }
            IReadOnlyDictionary<ArcanumObjectId, GameUiMerchantItemView> playerQuotes = barter
                ? _controller.ProjectPlayerSaleItems().ToDictionary(value => value.Item.Identity)
                : new Dictionary<ArcanumObjectId, GameUiMerchantItemView>();
            foreach (GameUiItemView item in player)
            {
                Rect rect = LocalItemRect(RetailInventoryInteractionLayout.PlayerGrid, item, 0);
                AddItem(playerLayer, item, rect, () =>
                {
                    if (barter) _controller.Trade(EconomyTransactionDirection.SellToMerchant, item.Identity);
                    else _controller.TransferPlayerItemToOpenInventory(item.Identity);
                    _dirty = true;
                    Synchronize();
                }, _targetDrop, barter && playerQuotes.TryGetValue(item.Identity, out GameUiMerchantItemView quote)
                    ? quote : null);
                DynamicItemCount++;
            }
        }

        private void AddItem(RectTransform layer, GameUiItemView item, Rect rect, Action submit,
            RectTransform destination, GameUiMerchantItemView quote)
        {
            RectTransform root = AddRect($"Item {item.Identity.Key}", layer, rect);
            Image hit = root.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            Button button = root.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => submit());
            RetailInventoryDragPresenter.AddDragTarget<RetailStartingEquipmentDragTarget>(root.gameObject)
                .Bind(destination, submit);
            SourceHoverHintTarget hint = root.gameObject.AddComponent<SourceHoverHintTarget>();
            hint.Bind(() =>
            {
                GameUiItemInspectionView inspection = _controller.ProjectItemInspection(item.Identity);
                string price = quote == null ? string.Empty : $" — {quote.UnitPrice} Gold";
                string key = $"inventory:{item.Identity.Key}";
                _controller.ShowContextualHelp(key, inspection?.Summary ?? string.Empty,
                    item.Name + price, RetailGameplayHudLayout.ContextualHelpIconSourceId);
            }, () => _controller.ClearContextualHelp());
            if (item.InventoryArtId.HasValue
                && _resolver.Original.TryResolveInventoryItem(item.InventoryArtId.Value,
                    out RetailItemArtAsset asset))
            {
                Vector2 size = asset.LogicalSize;
                float scale = Mathf.Min(1f, Mathf.Min(rect.width / Mathf.Max(1f, size.x),
                    rect.height / Mathf.Max(1f, size.y)));
                size *= scale;
                RectTransform icon = AddRect("Source Item Art", root,
                    new Rect((rect.width - size.x) * .5f, (rect.height - size.y) * .5f, size.x, size.y));
                Image image = icon.gameObject.AddComponent<Image>();
                image.sprite = asset.Sprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }
            if (item.Quantity > 1)
            {
                SourceUiBitmapText quantity = AddText("Quantity", root,
                    new Rect(Mathf.Max(0f, rect.width - 18f), Mathf.Max(0f, rect.height - 14f), 18f, 12f));
                quantity.Bind(_resolver, item.Quantity.ToString(), SourceText, quantity.FontSourceId);
            }
        }

        private void Scroll(float delta)
        {
            if (_maximumScrollRow == 0 || Mathf.Approximately(delta, 0f)) return;
            _scrollRow = Mathf.Clamp(_scrollRow - (int)Mathf.Sign(delta), 0, _maximumScrollRow);
            _dirty = true;
            Synchronize();
        }

        private Scrollbar AddScrollbar(RectTransform parent, Rect rect)
        {
            RectTransform root = AddRect("Target Inventory Scrollbar", parent, rect);
            Image track = root.gameObject.AddComponent<Image>();
            track.color = new Color32(35, 27, 18, 180);
            Scrollbar scrollbar = root.gameObject.AddComponent<Scrollbar>();
            RectTransform handle = AddRect("Handle", root, new Rect(2f, 2f, rect.width - 4f, 40f));
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = SourceText;
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.onValueChanged.AddListener(value =>
            {
                _scrollRow = Mathf.RoundToInt((1f - value) * _maximumScrollRow);
                _dirty = true;
                Synchronize();
            });
            return scrollbar;
        }

        private RectTransform AddClippedLayer(string name, Rect bounds)
        {
            RectTransform layer = AddRect(name, _dynamic, bounds);
            layer.gameObject.AddComponent<RectMask2D>();
            return layer;
        }

        private static Rect LocalItemRect(Rect grid, GameUiItemView item, int rowOffset)
        {
            Rect absolute = RetailInventoryInteractionLayout.ItemRect(grid, item.InventoryLocation,
                item.InventoryFootprint, rowOffset);
            return new Rect(absolute.x - grid.x, absolute.y - grid.y, absolute.width, absolute.height);
        }

        private static int MaximumScrollRow(IReadOnlyList<GameUiItemView> items, int visibleRows)
        {
            int rows = 0;
            foreach (GameUiItemView item in items)
                rows = Math.Max(rows, item.InventoryLocation / 10 + Math.Max(1, item.InventoryFootprint.Height));
            return Math.Max(0, rows - visibleRows);
        }

        private static int ProjectionSignature(bool barter, IEnumerable<GameUiItemView> target,
            IEnumerable<GameUiItemView> player, IEnumerable<GameUiMerchantItemView> merchant)
        {
            unchecked
            {
                int hash = barter ? 31 : 17;
                foreach (GameUiItemView item in target.Concat(player))
                {
                    hash = hash * 31 + item.Identity.GetHashCode();
                    hash = hash * 31 + item.Quantity;
                    hash = hash * 31 + item.InventoryLocation;
                }
                foreach (GameUiMerchantItemView value in merchant)
                    hash = hash * 31 + value.UnitPrice;
                return hash;
            }
        }

        private RectTransform AddInputRegion(string name, RectTransform parent, Rect rect)
        {
            RectTransform target = AddRect(name, parent, rect);
            Image image = target.gameObject.AddComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
            return target;
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

        private SourceUiButton AddSourceButton(string name, RectTransform parent, int sourceId, Rect rect,
            Action clicked)
        {
            Vector2 size = _resolver.TryResolve(new UiAssetKey(sourceId), out UiResolvedAsset asset)
                ? asset.LogicalSize : rect.size;
            RectTransform target = AddRect(name, parent, new Rect(rect.position, size));
            target.gameObject.AddComponent<Image>();
            target.gameObject.AddComponent<Button>();
            target.gameObject.AddComponent<SourceUiImage>();
            SourceUiButton source = target.gameObject.AddComponent<SourceUiButton>();
            RetailInventorySourceButtonState.Bind(source, _resolver, sourceId);
            source.Button.onClick.AddListener(() => clicked());
            return source;
        }

        private SourceUiBitmapText AddText(string name, RectTransform parent, Rect rect)
        {
            RectTransform target = AddRect(name, parent, rect);
            SourceUiBitmapText text = target.gameObject.AddComponent<SourceUiBitmapText>();
            text.Bind(_resolver, string.Empty, SourceText, RetailGameplayHudLayout.TextFontSourceId);
            return text;
        }

        private static RectTransform AddRect(string name, RectTransform parent, Rect rect)
        {
            GameObject go = new(name, typeof(RectTransform));
            RectTransform target = go.GetComponent<RectTransform>();
            target.SetParent(parent, false);
            target.anchorMin = target.anchorMax = new Vector2(0f, 1f);
            target.pivot = new Vector2(0f, 1f);
            target.anchoredPosition = new Vector2(rect.x, -rect.y);
            target.sizeDelta = rect.size;
            return target;
        }

        private static void Place(RectTransform target, Rect rect)
        {
            target.anchoredPosition = new Vector2(rect.x, -rect.y);
            target.sizeDelta = rect.size;
        }

        private void ClearDynamic()
        {
            for (int index = _dynamic.childCount - 1; index >= 0; index--)
            {
                GameObject child = _dynamic.GetChild(index).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Object.Destroy(child); else Object.DestroyImmediate(child);
            }
        }

        private void Invalidate()
        {
            _dirty = true;
            if (this != null && IsVisible) Synchronize();
        }

        private void OnDestroy() => Cleanup();

        private void Cleanup()
        {
            if (_resolver != null) _resolver.PresentationInvalidated -= Invalidate;
            if (_canvasRoot != null)
            {
                _canvasRoot.SetActive(false);
                if (Application.isPlaying) Object.Destroy(_canvasRoot); else Object.DestroyImmediate(_canvasRoot);
            }
            _canvasRoot = null;
            _window = null;
            _dynamic = null;
            _targetDrop = null;
            _playerDrop = null;
            _background = null;
            _scrollbar = null;
            _takeAll = null;
            _resolver = null;
            _controller = null;
            _dirty = true;
            _signature = int.MinValue;
            _scrollRow = 0;
            _maximumScrollRow = 0;
            DynamicItemCount = 0;
        }
    }
}
