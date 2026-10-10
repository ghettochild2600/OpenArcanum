using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Runtime.Economy;
using Arcanum.Runtime.World;
using OpenArcanum.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Arcanum.Runtime.UI
{
    /// <summary>Source rectangles from mainmenu_ui.c and inven_ui.c's barter presentation.</summary>
    public static class RetailStartingEquipmentLayout
    {
        public const int BarterBackgroundSourceId = 235;
        public const int PlayerInventorySourceId = 221;
        public const int MerchantPaperDollSourceId = 337;
        public const int PlayerPaperDollSourceId = 341;
        public const int PaperDollToggleSourceId = 338;
        public const int InventoryToggleSourceId = 339;
        public const int ArrangeSourceId = 340;
        public const int FeedbackIconSourceId = 444;
        public const int GridColumns = 10;
        public const float CellSize = 32f;

        public static readonly Rect Window = new(0f, 0f, 800f, 400f);
        public static readonly Rect PlayerInventory = new(358f, 0f, 442f, 400f);
        public static readonly Rect MerchantPaperDoll = new(0f, 157f, 358f, 243f);
        public static readonly Rect PlayerPaperDoll = new(358f, 0f, 442f, 400f);
        public static readonly Rect MerchantPaperDollToggle = new(8f, 123f, 38f, 32f);
        public static readonly Rect MerchantInventoryToggle = new(46f, 123f, 38f, 32f);
        public static readonly Rect PlayerPaperDollToggle = new(713f, 37f, 38f, 32f);
        public static readonly Rect PlayerInventoryToggle = new(751f, 37f, 38f, 32f);
        public static readonly Rect ArrangeButton = new(763f, 197f, 32f, 32f);
        public static readonly Rect MerchantName = new(89f, 102f, 253f, 20f);
        public static readonly Rect MerchantQuote = new(89f, 128f, 253f, 30f);
        public static readonly Rect MerchantGrid = new(8f, 168f, 320f, 224f);
        public static readonly Rect PlayerGrid = new(368f, 8f, 320f, 384f);
        public static readonly Rect[] CounterRects =
        {
            new(729f, 70f, 58f, 20f), new(729f, 93f, 58f, 20f), new(729f, 116f, 58f, 20f),
            new(729f, 139f, 58f, 20f), new(729f, 162f, 58f, 20f),
        };
        public static readonly Rect MessageIcon = new(212f, 102f, 64f, 64f);
        public static readonly Rect MessageTitle = new(282f, 101f, 306f, 20f);
        public static readonly Rect MessageBody = new(282f, 125f, 306f, 34f);
        public static readonly int[] RequiredSourceIds =
        {
            BarterBackgroundSourceId, PlayerInventorySourceId, MerchantPaperDollSourceId,
            PlayerPaperDollSourceId, PaperDollToggleSourceId, InventoryToggleSourceId, ArrangeSourceId,
            FeedbackIconSourceId, 321, 323,
        };

        public static Rect ItemRect(Rect grid, int location, InventoryFootprint footprint)
        {
            int columns = Mathf.Max(1, Mathf.FloorToInt(grid.width / CellSize));
            int rows = Mathf.Max(1, Mathf.FloorToInt(grid.height / CellSize));
            int bounded = Mathf.Clamp(location, 0, columns * rows - 1);
            return new Rect(grid.x + CellSize * (bounded % columns),
                grid.y + CellSize * (bounded / columns),
                CellSize * Mathf.Max(1, footprint.Width), CellSize * Mathf.Max(1, footprint.Height));
        }
    }

    /// <summary>Source barter accepts either a normal click or a drag into the opposite inventory grid.</summary>
    public sealed class RetailStartingEquipmentDragTarget : MonoBehaviour, IBeginDragHandler, IDragHandler,
        IEndDragHandler
    {
        private RectTransform _destination;
        private Action _submit;

        public void Bind(RectTransform destination, Action submit)
        {
            _destination = destination ?? throw new ArgumentNullException(nameof(destination));
            _submit = submit ?? throw new ArgumentNullException(nameof(submit));
        }

        public void OnBeginDrag(PointerEventData eventData) { }
        public void OnDrag(PointerEventData eventData) { }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData != null && RectTransformUtility.RectangleContainsScreenPoint(
                    _destination, eventData.position, eventData.pressEventCamera))
                _submit?.Invoke();
        }
    }

    /// <summary>
    /// Source-backed Human Shopkeeper screen. Ownership, prices, Gold and transfers remain in the existing
    /// inventory/economy authorities; this component only projects barter state and submits commands.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RetailStartingEquipmentView : MonoBehaviour
    {
        private static readonly Color SourceText = new Color32(231, 217, 174, 255);
        private static readonly Color SourceWhite = new Color32(236, 229, 204, 255);

        private GameUiController _controller;
        private WorldMapSessionCoordinator _session;
        private UiSkinResolver _resolver;
        private GameObject _canvasRoot;
        private RectTransform _window;
        private RectTransform _bottom;
        private RectTransform _dynamic;
        private RectTransform _merchantDropTarget;
        private RectTransform _playerDropTarget;
        private SourceUiImage _merchantPaperDoll;
        private SourceUiImage _playerPaperDoll;
        private SourceUiButton _merchantPaperDollToggle;
        private SourceUiButton _merchantInventoryToggle;
        private SourceUiButton _playerPaperDollToggle;
        private SourceUiButton _playerInventoryToggle;
        private SourceUiBitmapText _merchantName;
        private SourceUiBitmapText _merchantQuote;
        private SourceUiBitmapText _messageTitle;
        private RectTransform _messageBody;
        private SourceUiImage _messageIcon;
        private readonly SourceUiBitmapText[] _counters = new SourceUiBitmapText[5];
        private int _signature = int.MinValue;
        private bool _dirty = true;
        private bool _merchantInventoryMode = true;
        private bool _playerInventoryMode = true;

        public bool IsAvailable => _canvasRoot != null && _resolver != null;
        public bool IsVisible => IsAvailable && _canvasRoot.activeSelf;
        public RectTransform WindowTransform => _window;
        public int DynamicItemCount { get; private set; }
        public string MerchantQuoteText => _merchantQuote?.Text ?? string.Empty;
        public string MessageTitleText => _messageTitle?.Text ?? string.Empty;

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
            _resolver.PresentationInvalidated += Invalidate;
            BuildPresentation();
            Synchronize();
        }

        public void Synchronize()
        {
            if (!IsAvailable || _controller == null) return;
            bool visible = _controller.Screen == GameUiScreen.StartingEquipment
                           && _session.CharacterCreation.StartingEquipmentActive;
            if (_canvasRoot.activeSelf != visible) _canvasRoot.SetActive(visible);
            if (!visible) return;

            IReadOnlyList<GameUiMerchantItemView> wares = _controller.ProjectMerchantWares();
            // Source barter presents Gold in its dedicated counter, never as a draggable inventory object.
            IReadOnlyList<GameUiMerchantItemView> player = _controller.ProjectPlayerSaleItems()
                .Where(value => value.Item.Type != Arcanum.Formats.Objects.ObjectType.Gold).ToArray();
            int signature = ProjectionSignature(wares, player);
            if (_dirty || signature != _signature)
            {
                _signature = signature;
                _dirty = false;
                RebuildItems(wares, player);
            }

            string name = _session.TryGetObjectState(_controller.MerchantTarget, out PersistentObjectState merchant)
                ? _session.ResolveDisplayName(merchant) : "Human Shopkeeper";
            SetCenteredText(_merchantName, string.IsNullOrWhiteSpace(name) ? "Human Shopkeeper" : name.ToUpperInvariant(),
                RetailStartingEquipmentLayout.MerchantName);
            GameUiInventorySummaryView summary = _controller.ProjectInventorySummary();
            if (summary != null)
            {
                SetText(_counters[0], summary.Gold.ToString());
                for (int index = 0; index < 4; index++) SetText(_counters[index + 1], summary.Ammunition[index].ToString());
            }
        }

        public void HandleEscape() => _controller.ReturnToCharacterEditor();

        private void BuildPresentation()
        {
            _canvasRoot = new GameObject("Source-Faithful Starting Equipment",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
                typeof(SourceUiRuntime));
            _canvasRoot.transform.SetParent(transform, false);
            RetailPartialMainMenuComposition.ConfigureNativeCanvas(_canvasRoot, _resolver, 995);
            RectTransform canvas = (RectTransform)_canvasRoot.transform;
            RetailPartialMainMenuComposition.CreateTop(canvas, _resolver, "Shopkeeper Top Region");
            RetailPartialMainMenuRegion center = RetailPartialMainMenuComposition.CreateCenter(canvas, _resolver,
                RetailStartingEquipmentLayout.BarterBackgroundSourceId, "Shopkeeper Center Region");
            RetailPartialMainMenuRegion bottom = RetailPartialMainMenuComposition.CreateBottom(canvas, _resolver,
                "Shopkeeper Bottom Region");
            _window = center.Root;
            _bottom = bottom.Root;

            AddSourceImage("Player Inventory Panel", _window,
                RetailStartingEquipmentLayout.PlayerInventorySourceId,
                RetailStartingEquipmentLayout.PlayerInventory);
            _merchantPaperDoll = AddSourceImage("Merchant Paper Doll Panel", _window,
                RetailStartingEquipmentLayout.MerchantPaperDollSourceId,
                RetailStartingEquipmentLayout.MerchantPaperDoll);
            _playerPaperDoll = AddSourceImage("Player Paper Doll Panel", _window,
                RetailStartingEquipmentLayout.PlayerPaperDollSourceId,
                RetailStartingEquipmentLayout.PlayerPaperDoll);
            _merchantPaperDoll.gameObject.SetActive(false);
            _playerPaperDoll.gameObject.SetActive(false);
            _merchantName = AddText("Human Shopkeeper Name", _window,
                RetailStartingEquipmentLayout.MerchantName, 27, SourceWhite);
            _merchantQuote = AddText("Human Shopkeeper Quote", _window,
                RetailStartingEquipmentLayout.MerchantQuote, 26, SourceText);
            for (int index = 0; index < _counters.Length; index++)
                _counters[index] = AddText($"Counter {index}", _window,
                    RetailStartingEquipmentLayout.CounterRects[index], 26, SourceText);

            _merchantPaperDollToggle = AddSourceToggle("Merchant Paper Doll", _window,
                RetailStartingEquipmentLayout.PaperDollToggleSourceId,
                RetailStartingEquipmentLayout.MerchantPaperDollToggle, () => SetMerchantInventoryMode(false));
            _merchantInventoryToggle = AddSourceToggle("Merchant Inventory", _window,
                RetailStartingEquipmentLayout.InventoryToggleSourceId,
                RetailStartingEquipmentLayout.MerchantInventoryToggle, () => SetMerchantInventoryMode(true));
            _playerPaperDollToggle = AddSourceToggle("Player Paper Doll", _window,
                RetailStartingEquipmentLayout.PaperDollToggleSourceId,
                RetailStartingEquipmentLayout.PlayerPaperDollToggle, () => SetPlayerInventoryMode(false));
            _playerInventoryToggle = AddSourceToggle("Player Inventory", _window,
                RetailStartingEquipmentLayout.InventoryToggleSourceId,
                RetailStartingEquipmentLayout.PlayerInventoryToggle, () => SetPlayerInventoryMode(true));
            AddSourceButton("Arrange Items", _window, RetailStartingEquipmentLayout.ArrangeSourceId,
                RetailStartingEquipmentLayout.ArrangeButton.position, () =>
                {
                    _controller.ArrangeInventory();
                    _dirty = true;
                    Synchronize();
                });
            RefreshPanelControls();

            SourceUiButton continueButton = AddSourceButton("Shopkeeper Continue", _bottom, 321,
                new Vector2(675f, 55f), () => _controller.CompleteStartingEquipment());
            SourceUiButton backButton = AddSourceButton("Shopkeeper Back", _bottom, 323,
                new Vector2(69f, 55f), () => _controller.ReturnToCharacterEditor());
            BindNavigationHint(continueButton.gameObject, true);
            BindNavigationHint(backButton.gameObject, false);

            _messageIcon = AddSourceImage("Shopkeeper Message Icon", _bottom,
                RetailStartingEquipmentLayout.FeedbackIconSourceId, RetailStartingEquipmentLayout.MessageIcon);
            _messageTitle = AddText("Shopkeeper Message Title", _bottom,
                RetailStartingEquipmentLayout.MessageTitle, 27, SourceWhite);
            _messageBody = AddRect("Shopkeeper Message Body", _bottom,
                RetailStartingEquipmentLayout.MessageBody);
            ClearMessage();
            _merchantDropTarget = AddRect("Merchant Inventory Drop Target", _window,
                RetailStartingEquipmentLayout.MerchantGrid);
            _playerDropTarget = AddRect("Player Inventory Drop Target", _window,
                RetailStartingEquipmentLayout.PlayerGrid);
            _dynamic = AddRect("Shopkeeper Items", _window, RetailStartingEquipmentLayout.Window);

            var cursor = new GameObject("Shopkeeper Cursor",
                typeof(RectTransform), typeof(Image), typeof(SourceUiImage), typeof(RetailMainMenuCursorView));
            cursor.transform.SetParent(_canvasRoot.transform, false);
            cursor.GetComponent<RetailMainMenuCursorView>().Bind((RectTransform)_canvasRoot.transform, _resolver);
        }

        private void RebuildItems(IReadOnlyList<GameUiMerchantItemView> wares,
            IReadOnlyList<GameUiMerchantItemView> player)
        {
            ClearDynamic();
            if (_merchantInventoryMode)
                foreach (GameUiMerchantItemView item in wares)
                    AddTradeItem(item, RetailStartingEquipmentLayout.ItemRect(
                        RetailStartingEquipmentLayout.MerchantGrid, item.Item.InventoryLocation,
                        item.Item.InventoryFootprint));
            if (_playerInventoryMode)
                foreach (GameUiMerchantItemView item in player)
                    AddTradeItem(item, RetailStartingEquipmentLayout.ItemRect(
                        RetailStartingEquipmentLayout.PlayerGrid, item.Item.InventoryLocation,
                        item.Item.InventoryFootprint));
            DynamicItemCount = (_merchantInventoryMode ? wares.Count : 0)
                               + (_playerInventoryMode ? player.Count : 0);
        }

        private void SetMerchantInventoryMode(bool inventory)
        {
            if (_merchantInventoryMode == inventory) return;
            _merchantInventoryMode = inventory;
            _dirty = true;
            RefreshPanelControls();
            Synchronize();
        }

        private void SetPlayerInventoryMode(bool inventory)
        {
            if (_playerInventoryMode == inventory) return;
            _playerInventoryMode = inventory;
            _dirty = true;
            RefreshPanelControls();
            Synchronize();
        }

        private void RefreshPanelControls()
        {
            if (_merchantPaperDoll != null) _merchantPaperDoll.gameObject.SetActive(!_merchantInventoryMode);
            if (_playerPaperDoll != null) _playerPaperDoll.gameObject.SetActive(!_playerInventoryMode);
            _merchantPaperDollToggle?.SetSelected(!_merchantInventoryMode);
            _merchantInventoryToggle?.SetSelected(_merchantInventoryMode);
            _playerPaperDollToggle?.SetSelected(!_playerInventoryMode);
            _playerInventoryToggle?.SetSelected(_playerInventoryMode);
            if (_merchantDropTarget != null) _merchantDropTarget.gameObject.SetActive(_merchantInventoryMode);
            if (_playerDropTarget != null) _playerDropTarget.gameObject.SetActive(_playerInventoryMode);
        }

        private void AddTradeItem(GameUiMerchantItemView view, Rect bounds)
        {
            RectTransform root = AddRect($"{view.Direction} {view.Item.Identity.Key}", _dynamic, bounds);
            Image hit = root.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            Button button = root.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            // Source barter still accepts the gesture when a transaction will fail so that the merchant can
            // present the authoritative reason (most notably insufficient Gold).
            button.interactable = true;
            button.onClick.AddListener(() => Execute(view));
            RetailStartingEquipmentDragTarget drag = root.gameObject.AddComponent<RetailStartingEquipmentDragTarget>();
            drag.Bind(view.Direction == EconomyTransactionDirection.BuyFromMerchant
                ? _playerDropTarget : _merchantDropTarget, () => Execute(view));
            SourceHoverHintTarget hint = root.gameObject.AddComponent<SourceHoverHintTarget>();
            hint.Bind(() => ShowItem(view), ClearMessage);

            if (view.Item.InventoryArtId.HasValue
                && _resolver.Original.TryResolveInventoryItem(view.Item.InventoryArtId.Value,
                    out RetailItemArtAsset asset))
            {
                Vector2 size = asset.LogicalSize;
                float scale = Mathf.Min(1f, Mathf.Min(bounds.width / Mathf.Max(1f, size.x),
                    bounds.height / Mathf.Max(1f, size.y)));
                size *= scale;
                RectTransform icon = AddRect("Source Item Art", root,
                    new Rect((bounds.width - size.x) * .5f, (bounds.height - size.y) * .5f, size.x, size.y));
                Image image = icon.gameObject.AddComponent<Image>();
                image.sprite = asset.Sprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }
            else
            {
                SourceUiBitmapText fallback = AddText("Item Label", root,
                    new Rect(2f, 2f, Mathf.Max(1f, bounds.width - 4f), 14f), 26, SourceText);
                SetText(fallback, string.IsNullOrEmpty(view.Item.Name) ? "?" : view.Item.Name[..1]);
            }
            if (view.Item.Quantity > 1)
            {
                SourceUiBitmapText quantity = AddText("Quantity", root,
                    new Rect(Mathf.Max(0f, bounds.width - 18f), Mathf.Max(0f, bounds.height - 14f), 18f, 12f),
                    26, SourceText);
                SetText(quantity, view.Item.Quantity.ToString());
            }
        }

        private void Execute(GameUiMerchantItemView view)
        {
            bool success = _controller.Trade(view.Direction, view.Item.Identity);
            _dirty = true;
            Synchronize();
            if (!success)
            {
                SetText(_merchantQuote, _controller.Feedback);
                return;
            }

            EconomyTransactionDirection reverse = view.Direction == EconomyTransactionDirection.BuyFromMerchant
                ? EconomyTransactionDirection.SellToMerchant
                : EconomyTransactionDirection.BuyFromMerchant;
            GameUiMerchantItemView reverseView = (reverse == EconomyTransactionDirection.SellToMerchant
                    ? _controller.ProjectPlayerSaleItems()
                    : _controller.ProjectMerchantWares())
                .FirstOrDefault(value => value.Item.Identity == view.Item.Identity);
            int reversePrice = reverseView?.UnitPrice ?? 0;
            SetText(_merchantQuote, reverse == EconomyTransactionDirection.SellToMerchant
                ? $"Yes, it is worth {reversePrice} coins. Do we have a deal?"
                : $"Consider it yours for a mere {reversePrice} coins.");
        }

        private void ShowItem(GameUiMerchantItemView view)
        {
            SetText(_merchantQuote, view.Direction == EconomyTransactionDirection.BuyFromMerchant
                ? $"Consider it yours for a mere {view.UnitPrice} coins."
                : $"Yes, it is worth {view.UnitPrice} coins. Do we have a deal?");
            SetText(_messageTitle, view.Item.Name?.ToUpperInvariant() ?? string.Empty, SourceWhite);
            string details = $"Price: {view.UnitPrice}   Weight: {view.Item.Weight}";
            if (_session.TryGetObjectState(view.Item.Identity, out PersistentObjectState state)
                && state.WeaponData != null)
            {
                int normal = (int)Combat.DamageType.Normal;
                int fatigue = (int)Combat.DamageType.Fatigue;
                details = $"D:{state.WeaponData.DamageMin[normal]}-{state.WeaponData.DamageMax[normal]} "
                          + $"FT:{state.WeaponData.DamageMin[fatigue]}-{state.WeaponData.DamageMax[fatigue]} "
                          + $"Speed:{state.WeaponData.SpeedFactor} Weight:{state.UnitWeight}";
                if (state.MaximumHitPoints > 0)
                    details += $" {Math.Max(0, state.MaximumHitPoints - state.HitPointDamage)}/{state.MaximumHitPoints}";
            }
            RebuildWrappedText(_messageBody, details, 42, 2);
            _messageIcon.gameObject.SetActive(true);
        }

        private void BindNavigationHint(GameObject target, bool next)
        {
            SourceHoverHintTarget hint = target.GetComponent<SourceHoverHintTarget>()
                                         ?? target.AddComponent<SourceHoverHintTarget>();
            hint.Bind(() =>
            {
                SetText(_messageTitle, ReadMainMenuMessage(6999, "Action:"), SourceWhite);
                RebuildWrappedText(_messageBody,
                    ReadMainMenuMessage(next ? 7003 : 7002, next ? "Next Screen" : "Previous Screen"), 42, 2);
                _messageIcon.gameObject.SetActive(true);
            }, ClearMessage);
        }

        private string ReadMainMenuMessage(int id, string fallback)
            => _resolver.Original.TryReadMessage(RetailMainMenuSource.MessageTable, id, out string value)
               && !string.IsNullOrWhiteSpace(value) ? value.Trim() : fallback;

        private void ClearMessage()
        {
            if (_messageIcon != null) _messageIcon.gameObject.SetActive(false);
            SetText(_messageTitle, string.Empty);
            if (_messageBody != null) RebuildWrappedText(_messageBody, string.Empty, 42, 2);
        }

        private void RebuildWrappedText(RectTransform parent, string value, int columns, int maxLines)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                GameObject child = parent.GetChild(index).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Object.Destroy(child); else Object.DestroyImmediate(child);
            }
            var words = (value ?? string.Empty).Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            var lines = new List<string>();
            string current = string.Empty;
            foreach (string word in words)
            {
                string next = current.Length == 0 ? word : current + " " + word;
                if (next.Length <= columns) current = next;
                else
                {
                    if (current.Length > 0) lines.Add(current);
                    current = word;
                    if (lines.Count >= maxLines) break;
                }
            }
            if (current.Length > 0 && lines.Count < maxLines) lines.Add(current);
            for (int index = 0; index < lines.Count; index++)
                AddText($"Line {index + 1}", parent, new Rect(0f, index * 15f, parent.rect.width, 15f),
                    26, SourceWhite).Bind(_resolver, lines[index], SourceWhite, 26);
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

        private SourceUiButton AddSourceButton(string name, RectTransform parent, int sourceId, Vector2 position,
            Action clicked)
        {
            Vector2 size = _resolver.TryResolve(new UiAssetKey(sourceId), out UiResolvedAsset asset)
                ? asset.LogicalSize : new Vector2(56f, 56f);
            RectTransform target = AddRect(name, parent, new Rect(position, size));
            target.gameObject.AddComponent<Image>();
            target.gameObject.AddComponent<Button>();
            target.gameObject.AddComponent<SourceUiImage>();
            SourceUiButton source = target.gameObject.AddComponent<SourceUiButton>();
            source.Bind(_resolver, new UiAssetKey(sourceId, frame: 0), new UiAssetKey(sourceId, frame: 1),
                new UiAssetKey(sourceId, frame: 2), new UiAssetKey(sourceId, frame: 0));
            source.Button.onClick.AddListener(() => clicked());
            return source;
        }

        private SourceUiButton AddSourceToggle(string name, RectTransform parent, int sourceId, Rect rect,
            Action clicked)
        {
            Vector2 size = _resolver.TryResolve(new UiAssetKey(sourceId), out UiResolvedAsset asset)
                ? asset.LogicalSize : rect.size;
            RectTransform target = AddRect(name, parent, new Rect(rect.position, size));
            target.gameObject.AddComponent<Image>();
            target.gameObject.AddComponent<Button>();
            target.gameObject.AddComponent<SourceUiImage>();
            SourceUiButton source = target.gameObject.AddComponent<SourceUiButton>();
            // The source toggle ARTs expose one bitmap frame; TIG supplies the latch state without selecting a
            // distinct frame. Reuse that frame for every Unity presentation state to avoid an invented fallback.
            UiAssetKey frame = new(sourceId, frame: 0);
            source.Bind(_resolver, frame, frame, frame, frame, frame);
            source.Button.onClick.AddListener(() => clicked());
            return source;
        }

        private SourceUiBitmapText AddText(string name, RectTransform parent, Rect rect, int font, Color color)
        {
            RectTransform target = AddRect(name, parent, rect);
            SourceUiBitmapText text = target.gameObject.AddComponent<SourceUiBitmapText>();
            text.Bind(_resolver, string.Empty, color, font);
            return text;
        }

        private void SetText(SourceUiBitmapText text, string value)
            => SetText(text, value, SourceText);

        private void SetText(SourceUiBitmapText text, string value, Color tint)
        {
            if (text == null || text.Text == (value ?? string.Empty)) return;
            text.Bind(_resolver, value ?? string.Empty, tint, text.FontSourceId);
        }

        private void SetCenteredText(SourceUiBitmapText text, string value, Rect bounds)
        {
            SetText(text, value, SourceWhite);
            RectTransform rect = (RectTransform)text.transform;
            rect.anchoredPosition = new Vector2(bounds.x + Mathf.Max(0f, (bounds.width - rect.sizeDelta.x) * .5f),
                -bounds.y);
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

        private static int ProjectionSignature(IEnumerable<GameUiMerchantItemView> wares,
            IEnumerable<GameUiMerchantItemView> player)
        {
            unchecked
            {
                int hash = 17;
                foreach (GameUiMerchantItemView value in wares.Concat(player))
                {
                    hash = hash * 31 + value.Item.Identity.GetHashCode();
                    hash = hash * 31 + value.Item.Quantity;
                    hash = hash * 31 + value.Item.InventoryLocation;
                    hash = hash * 31 + value.UnitPrice;
                }
                return hash;
            }
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
            _bottom = null;
            _dynamic = null;
            _merchantDropTarget = null;
            _playerDropTarget = null;
            _merchantPaperDoll = null;
            _playerPaperDoll = null;
            _merchantPaperDollToggle = null;
            _merchantInventoryToggle = null;
            _playerPaperDollToggle = null;
            _playerInventoryToggle = null;
            _merchantInventoryMode = true;
            _playerInventoryMode = true;
            _resolver = null;
        }
    }
}
