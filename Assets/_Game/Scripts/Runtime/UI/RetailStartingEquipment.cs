using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
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
        public const int DropBoxSourceId = 224;
        public const int UseBoxSourceId = 342;
        public const int GambleBoxSourceId = 345;
        public const int ScrollUpSourceId = 317;
        public const int ScrollDownSourceId = 318;
        public const int ScrollThumbTopSourceId = 238;
        public const int ScrollThumbMiddleSourceId = 787;
        public const int ScrollThumbBottomSourceId = 240;
        public const int FeedbackIconSourceId = 444;
        public const int GoldInspectionIconSourceId = 417;
        public const int QuantityPanelSourceId = 298;
        public const int QuantityTakeAllSourceId = 299;
        public const int QuantityPlusSourceId = 805;
        public const int QuantityMinusSourceId = 804;
        public const int QuantityOkSourceId = 33;
        public const int QuantityCancelSourceId = 32;
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
        // redraw_inven draws these at window-local y=236/290/344.  The companion frame rectangles in source
        // include the intgame big-window's screen y=41 offset and are input coordinates, not blit coordinates.
        public static readonly Rect GambleBox = new(703f, 236f, 89f, 54f);
        public static readonly Rect UseBox = new(703f, 290f, 89f, 54f);
        public static readonly Rect DropBox = new(703f, 344f, 89f, 54f);
        public static readonly Rect MerchantPortrait = new(183f, 33f, 64f, 64f);
        public static readonly Rect MerchantName = new(89f, 102f, 253f, 20f);
        public static readonly Rect MerchantQuote = new(89f, 128f, 253f, 30f);
        public static readonly Rect MerchantGrid = new(8f, 168f, 320f, 224f);
        public static readonly Rect MerchantScrollbar = new(330f, 168f, 17f, 224f);
        public static readonly Rect PlayerGrid = new(368f, 8f, 320f, 384f);
        public static readonly Rect PlayerWeight = new(428f, 32f, 185f, 15f);
        public static readonly Rect PlayerEncumbrance = new(428f, 47f, 185f, 15f);
        public static readonly Rect PlayerSpeed = new(428f, 62f, 185f, 15f);
        public static readonly Rect PlayerAttackValue = new(422f, 358f, 23f, 35f);
        public static readonly Rect PlayerDefenseValue = new(606f, 358f, 23f, 35f);
        public static readonly Rect MerchantAttackValue = new(105f, 354f, 22f, 16f);
        public static readonly Rect MerchantDefenseValue = new(227f, 354f, 22f, 16f);
        public static readonly Rect MerchantAttackHelp = new(75f, 329f, 55f, 40f);
        public static readonly Rect MerchantDefenseHelp = new(225f, 329f, 55f, 40f);
        public static readonly Rect PlayerAttackIcon = new(374f, 318f, 54f, 71f);
        public static readonly Rect PlayerDefenseIcon = new(599f, 319f, 83f, 71f);
        public static readonly Rect PlayerAttackHelp = new(374f, 318f, 71f, 75f);
        public static readonly Rect PlayerDefenseHelp = new(599f, 319f, 48f, 74f);
        public static readonly IReadOnlyDictionary<WornLocation, Rect> PlayerPaperDollSlots =
            new Dictionary<WornLocation, Rect>
            {
                [WornLocation.Helmet] = new(496f, 107f, 64f, 64f),
                [WornLocation.Ring1] = new(592f, 107f, 32f, 32f),
                [WornLocation.Ring2] = new(624f, 107f, 32f, 32f),
                [WornLocation.Medallion] = new(592f, 139f, 64f, 32f),
                [WornLocation.Weapon] = new(368f, 170f, 96f, 128f),
                [WornLocation.Shield] = new(592f, 171f, 96f, 128f),
                [WornLocation.Armor] = new(464f, 171f, 128f, 160f),
                [WornLocation.Gauntlet] = new(400f, 107f, 64f, 64f),
                [WornLocation.Boots] = new(495f, 331f, 64f, 64f),
            };
        public static readonly IReadOnlyDictionary<WornLocation, Rect> MerchantPaperDollSlots =
            new Dictionary<WornLocation, Rect>
            {
                [WornLocation.Helmet] = new(157f, 182f, 42f, 42f),
                [WornLocation.Ring1] = new(220f, 182f, 21f, 21f),
                [WornLocation.Ring2] = new(241f, 182f, 21f, 21f),
                [WornLocation.Medallion] = new(220f, 203f, 42f, 21f),
                [WornLocation.Weapon] = new(73f, 223f, 63f, 85f),
                [WornLocation.Shield] = new(220f, 224f, 63f, 85f),
                [WornLocation.Armor] = new(136f, 224f, 84f, 106f),
                [WornLocation.Gauntlet] = new(94f, 182f, 42f, 42f),
                [WornLocation.Boots] = new(156f, 330f, 42f, 42f),
            };
        public static readonly Rect[] CounterRects =
        {
            new(729f, 70f, 58f, 20f), new(729f, 93f, 58f, 20f), new(729f, 116f, 58f, 20f),
            new(729f, 139f, 58f, 20f), new(729f, 162f, 58f, 20f),
        };
        public static readonly Rect MessageIcon = new(217f, 69f, 64f, 64f);
        public static readonly Rect MessageTitle = new(290f, 63f, 291f, 19f);
        public static readonly Rect MessageBody = new(290f, 87f, 291f, 54f);
        public static readonly Rect MessageProperties = new(290f, 87f, 291f, 18f);
        public static readonly Rect MessageQuantity = new(290f, 105f, 291f, 18f);
        public static readonly Rect MessageFooter = new(290f, 123f, 291f, 18f);
        public static readonly Rect QuantityPanel = new(196f, 51f, 408f, 108f);
        public static readonly Rect QuantityItemIcon = new(269f, 93f, 64f, 64f);
        public static readonly Rect QuantityValue = new(404f, 104f, 72f, 18f);
        public static readonly Vector2 QuantityTakeAll = new(364f, 107f);
        public static readonly Vector2 QuantityPlus = new(479f, 106f);
        public static readonly Vector2 QuantityMinus = new(479f, 118f);
        public static readonly Vector2 QuantityOk = new(545f, 65f);
        public static readonly Vector2 QuantityCancel = new(545f, 116f);
        public static readonly int[] RequiredSourceIds =
        {
            BarterBackgroundSourceId, PlayerInventorySourceId, MerchantPaperDollSourceId,
            PlayerPaperDollSourceId, PaperDollToggleSourceId, InventoryToggleSourceId, ArrangeSourceId,
            DropBoxSourceId, UseBoxSourceId, GambleBoxSourceId, ScrollUpSourceId, ScrollDownSourceId,
            ScrollThumbTopSourceId, ScrollThumbMiddleSourceId, ScrollThumbBottomSourceId,
            FeedbackIconSourceId, GoldInspectionIconSourceId, QuantityPanelSourceId,
            QuantityTakeAllSourceId, QuantityPlusSourceId, QuantityMinusSourceId,
            QuantityOkSourceId, QuantityCancelSourceId, 375, 321, 323,
        };

        public static Rect ItemRect(Rect grid, int location, InventoryFootprint footprint)
            => ItemRect(grid, location, footprint, 0f);

        public static Rect ItemRect(Rect grid, int location, InventoryFootprint footprint, float rowOffset)
        {
            int columns = Mathf.Max(1, Mathf.FloorToInt(grid.width / CellSize));
            int bounded = Mathf.Max(0, location);
            return new Rect(grid.x + CellSize * (bounded % columns),
                grid.y + CellSize * (bounded / columns - rowOffset),
                CellSize * Mathf.Max(1, footprint.Width), CellSize * Mathf.Max(1, footprint.Height));
        }
    }

    public sealed class RetailStartingEquipmentScrollTarget : MonoBehaviour, IScrollHandler
    {
        private Action<float> _scroll;
        public void Bind(Action<float> scroll) => _scroll = scroll;
        public void OnScroll(PointerEventData eventData) => _scroll?.Invoke(eventData?.scrollDelta.y ?? 0f);
    }

    /// <summary>
    /// Identifies the authored barter inventory beneath a drag. Using the EventSystem's raycast result keeps
    /// the source-side decision correct inside the scaled Unity Game view as well as in a standalone player.
    /// </summary>
    public sealed class RetailStartingEquipmentInventorySideTarget : MonoBehaviour
    {
        public bool IsMerchant { get; private set; }
        public void Bind(bool isMerchant) => IsMerchant = isMerchant;
    }

    /// <summary>
    /// Shared construction invariant for every inventory presenter which owns drag behavior. Unity's destroyed
    /// component sentinel is not a CLR null, so callers must use Unity's overloaded null check instead of ?? here.
    /// </summary>
    public static class RetailInventoryDragPresenter
    {
        public static CanvasGroup EnsureCanvasGroup(GameObject presenter)
        {
            if (presenter == null) return null;
            CanvasGroup group = presenter.GetComponent<CanvasGroup>();
            return group != null ? group : presenter.AddComponent<CanvasGroup>();
        }

        public static T AddDragTarget<T>(GameObject presenter) where T : Component
        {
            if (presenter == null) return null;
            EnsureCanvasGroup(presenter);
            T target = presenter.GetComponent<T>();
            return target != null ? target : presenter.AddComponent<T>();
        }
    }

    /// <summary>Source state mapping shared by every inventory-mode button presenter.</summary>
    public static class RetailInventorySourceButtonState
    {
        public const int ArrangeItemsSourceId = 340;

        public static void Bind(SourceUiButton source, UiSkinResolver resolver, int sourceId)
        {
            UiAssetKey normal = new(sourceId, frame: 0);
            UiAssetKey active = new(sourceId, frame: 1);
            bool sourceMomentary = sourceId is ArrangeItemsSourceId or 321 or 323;
            // tiny_butt.art (ART 340) has two frames. It is a TIG_BUTTON_MOMENTARY control:
            // frame 0 is up and frame 1 is down. Hover never latches the down frame.
            UiAssetKey pressed = sourceId == ArrangeItemsSourceId
                ? active
                : new UiAssetKey(sourceId, frame: 2);
            source.Bind(resolver, normal, sourceMomentary ? normal : active, pressed, normal);
        }
    }

    /// <summary>Source barter accepts either a normal click or a drag into the opposite inventory grid.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class RetailStartingEquipmentDragTarget : MonoBehaviour, IBeginDragHandler, IDragHandler,
        IEndDragHandler, ICancelHandler, IPointerDownHandler
    {
        private RectTransform _dragLayer;
        private RectTransform _itemTransform;
        private Transform _originalParent;
        private int _originalSibling;
        private Vector2 _originalPosition;
        private Vector2 _pointerOffset;
        private Func<PointerEventData, bool> _submit;
        private Action<PointerEventData> _moved;
        private Action _finished;
        private Action _clicked;
        private bool _dragging;
        private bool _suppressClick;
        private CanvasGroup _dragCanvasGroup;
        private bool _originalBlocksRaycasts;

        public void Bind(RectTransform dragLayer, Func<PointerEventData, bool> submit,
            Action clicked = null, Action<PointerEventData> moved = null, Action finished = null)
        {
            _dragLayer = dragLayer ?? throw new ArgumentNullException(nameof(dragLayer));
            _submit = submit ?? throw new ArgumentNullException(nameof(submit));
            _moved = moved;
            _finished = finished;
            _clicked = clicked;
            _itemTransform = (RectTransform)transform;
            RetailInventoryDragPresenter.EnsureCanvasGroup(gameObject);
        }

        public void Bind(RectTransform destination, Action submit)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (submit == null) throw new ArgumentNullException(nameof(submit));
            Bind((RectTransform)transform.parent, eventData =>
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(
                        destination, eventData.position, eventData.pressEventCamera)) return false;
                submit();
                return true;
            }, submit);
        }

        public void OnPointerDown(PointerEventData eventData) => _suppressClick = false;

        public void SubmitClick()
        {
            if (_suppressClick)
            {
                _suppressClick = false;
                return;
            }
            _clicked?.Invoke();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_itemTransform == null || eventData == null) return;
            _originalParent = _itemTransform.parent;
            _originalSibling = _itemTransform.GetSiblingIndex();
            _originalPosition = _itemTransform.anchoredPosition;
            _suppressClick = true;
            _dragCanvasGroup = RetailInventoryDragPresenter.EnsureCanvasGroup(_itemTransform.gameObject);
            if (_dragCanvasGroup == null)
            {
                RestorePresentation();
                return;
            }
            _originalBlocksRaycasts = _dragCanvasGroup.blocksRaycasts;
            _dragCanvasGroup.blocksRaycasts = false;
            _itemTransform.SetParent(_dragLayer, true);
            _itemTransform.SetAsLastSibling();
            _dragging = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _dragLayer, eventData.position, eventData.pressEventCamera, out Vector2 pointer);
            if (!_dragging)
            {
                RestorePresentation();
                return;
            }
            _pointerOffset = _itemTransform.anchoredPosition - pointer;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || eventData == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _dragLayer, eventData.position, eventData.pressEventCamera, out Vector2 pointer))
                _itemTransform.anchoredPosition = pointer + _pointerOffset;
            _moved?.Invoke(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            RestorePresentation();
            if (eventData != null) _submit?.Invoke(eventData);
            _finished?.Invoke();
        }

        public void OnCancel(BaseEventData eventData)
        {
            RestorePresentation();
            _finished?.Invoke();
        }

        private void RestorePresentation()
        {
            if (_itemTransform != null && _originalParent != null)
            {
                _itemTransform.SetParent(_originalParent, false);
                _itemTransform.SetSiblingIndex(_originalSibling);
                _itemTransform.anchoredPosition = _originalPosition;
            }
            if (_dragCanvasGroup != null)
            {
                _dragCanvasGroup.blocksRaycasts = _originalBlocksRaycasts;
                _dragCanvasGroup = null;
            }
            _dragging = false;
        }
    }

    /// <summary>
    /// Source-backed Human Shopkeeper screen. Ownership, prices, Gold and transfers remain in the existing
    /// inventory/economy authorities; this component only projects barter state and submits commands.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RetailStartingEquipmentView : MonoBehaviour
    {
        private sealed class PreparedItemPresentation
        {
            public GameUiMerchantItemView View;
            public string MerchantQuote;
            public string Title;
            public string TitleRight;
            public string Properties;
            public string Quantity;
            public string Weight;
            public string Condition;
            public int MessageIconSourceId;
            public bool IsGold;
        }

        private static readonly Color SourceText = new Color32(231, 217, 174, 255);
        private static readonly Color SourceWhite = new Color32(236, 229, 204, 255);
        private static readonly Color SourceRed = new Color32(255, 50, 50, 255);

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
        private Image _merchantPortrait;
        private SourceUiButton _merchantPaperDollToggle;
        private SourceUiButton _merchantInventoryToggle;
        private SourceUiButton _playerPaperDollToggle;
        private SourceUiButton _playerInventoryToggle;
        private SourceUiBitmapText _merchantName;
        private SourceUiBitmapText _merchantQuote;
        private SourceUiBitmapText _messageTitle;
        private SourceUiBitmapText _messageTitleRight;
        private SourceUiBitmapText _playerAttack;
        private SourceUiBitmapText _playerDefense;
        private SourceUiBitmapText _playerWeight;
        private SourceUiBitmapText _playerEncumbrance;
        private SourceUiBitmapText _playerSpeed;
        private SourceUiBitmapText _merchantAttack;
        private SourceUiBitmapText _merchantDefense;
        private RectTransform _playerAttackHelp;
        private RectTransform _playerDefenseHelp;
        private RectTransform _merchantAttackHelp;
        private RectTransform _merchantDefenseHelp;
        private SourceUiImage _playerAttackIcon;
        private SourceUiImage _playerDefenseIcon;
        private SourceUiImage _gambleBox;
        private SourceUiImage _useBox;
        private SourceUiImage _dropBox;
        private Scrollbar _merchantScrollbar;
        private RectTransform _messageBody;
        private SourceUiImage _messageIcon;
        private Image _messageItemIcon;
        private RectTransform _quantityPanel;
        private RectTransform _quantityItemRect;
        private Image _quantityItemIcon;
        private SourceUiBitmapText _quantityValue;
        private GameUiMerchantItemView _pendingTransaction;
        private int _selectedQuantity;
        private int _maximumQuantity;
        private int _pendingDestinationLocation = -1;
        private readonly SourceUiBitmapText[] _counters = new SourceUiBitmapText[5];
        private readonly Dictionary<WornLocation, SourceUiImage> _merchantEmptySlots = new();
        private readonly Dictionary<WornLocation, SourceUiImage> _playerEmptySlots = new();
        private readonly List<SourceUiBitmapText> _messageLinePool = new List<SourceUiBitmapText>();
        private int _signature = int.MinValue;
        private bool _projectedMerchantInventoryMode = true;
        private bool _projectedPlayerInventoryMode = true;
        private int _projectedMerchantScrollRow = -1;
        private bool _dirty = true;
        private bool _merchantInventoryMode = true;
        private bool _playerInventoryMode = true;
        private InventoryArrangeMode _nextArrangeMode = InventoryArrangeMode.Horizontal;
        private float _merchantScrollRow;
        private int _merchantMaximumScrollRow;
        private bool _centeredShoppingWorld;

        public bool IsAvailable => _canvasRoot != null && _resolver != null;
        public bool IsVisible => IsAvailable && _canvasRoot.activeSelf;
        public RectTransform WindowTransform => _window;
        public int DynamicItemCount { get; private set; }
        public string MerchantQuoteText => _merchantQuote?.Text ?? string.Empty;
        public string MessageTitleText => _messageTitle?.Text ?? string.Empty;
        public bool QuantityPanelVisible => _quantityPanel != null && _quantityPanel.gameObject.activeSelf;
        public int SelectedQuantity => _selectedQuantity;
        public int MaximumQuantity => _maximumQuantity;

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
            _session.ObjectPlacementChanged += HandleInventoryChanged;
            _session.ObjectQuantityChanged += HandleInventoryChanged;
            _session.ObjectStateRemoved += HandleInventoryChanged;
            BuildPresentation();
            Synchronize();
        }

        public void Synchronize()
        {
            if (!IsAvailable || _controller == null) return;
            bool visible = _controller.Screen == GameUiScreen.StartingEquipment
                           && _session.CharacterCreation.StartingEquipmentActive;
            if (_canvasRoot.activeSelf != visible) _canvasRoot.SetActive(visible);
            if (!visible)
            {
                _centeredShoppingWorld = false;
                return;
            }
            CenterShoppingWorldOnPlayer();

            if (!_dirty) return;

            IReadOnlyList<GameUiMerchantItemView> wares = _controller.ProjectMerchantWares();
            // inven_ui.c temporarily clears OIF_NO_DISPLAY for Gold while drawing barter inventories. The same
            // authoritative stack therefore drives both the physical inventory icon and the dedicated counter.
            IReadOnlyList<GameUiMerchantItemView> player = _controller.ProjectPlayerSaleItems();
            int merchantScrollRow = Mathf.RoundToInt(_merchantScrollRow);
            int signature = ProjectionSignature(wares, player);
            if (signature != _signature
                || _merchantInventoryMode != _projectedMerchantInventoryMode
                || _playerInventoryMode != _projectedPlayerInventoryMode
                || merchantScrollRow != _projectedMerchantScrollRow)
            {
                _signature = signature;
                _projectedMerchantInventoryMode = _merchantInventoryMode;
                _projectedPlayerInventoryMode = _playerInventoryMode;
                _projectedMerchantScrollRow = merchantScrollRow;
                RebuildItems(wares, player);
            }

            string name = _session.TryGetObjectState(_controller.MerchantTarget, out PersistentObjectState merchant)
                ? _session.ResolveDisplayName(merchant) : "Human Shopkeeper";
            SetMerchantPortrait(merchant);
            SetCenteredText(_merchantName, string.IsNullOrWhiteSpace(name) ? "Human Shopkeeper" : name.ToUpperInvariant(),
                RetailStartingEquipmentLayout.MerchantName);
            RefreshInventorySummary(_controller.ProjectInventorySummary(), false);
            RefreshInventorySummary(_controller.ProjectInventorySummary(_controller.MerchantTarget), true);
            _dirty = false;
        }

        private void RefreshInventorySummary(GameUiInventorySummaryView summary, bool merchant)
        {
            if (summary == null) return;
            if (merchant)
            {
                SetRightAlignedText(_merchantAttack, summary.TotalAttack.ToString(),
                    RetailStartingEquipmentLayout.MerchantAttackValue, SourceWhite);
                SetText(_merchantDefense, summary.TotalDefense.ToString(), SourceWhite);
                return;
            }

            SetCenteredText(_counters[0], summary.Gold.ToString(), RetailStartingEquipmentLayout.CounterRects[0]);
            for (int index = 0; index < 4; index++)
                SetCenteredText(_counters[index + 1], summary.Ammunition[index].ToString(),
                    RetailStartingEquipmentLayout.CounterRects[index + 1]);
            SetRightAlignedText(_playerAttack, summary.TotalAttack.ToString(),
                RetailStartingEquipmentLayout.PlayerAttackValue, SourceWhite);
            SetText(_playerDefense, summary.TotalDefense.ToString(), SourceWhite);
            SetText(_playerWeight,
                $"{SourceMessage(9, "Total weight")}: {summary.Weight} {SourceMessage(6, "stone")}", SourceWhite);
            SetText(_playerEncumbrance,
                $"{SourceMessage(10, "Encumbrance")}: {EncumbranceName(summary.EncumbranceLevel)} "
                + $"({summary.EncumbranceThreshold})", SourceWhite);
            SetText(_playerSpeed, $"{SourceMessage(11, "Speed")}: {summary.Speed}", SourceWhite);
        }

        private string SourceMessage(int id, string fallback)
            => _resolver.Original.TryReadMessage("mes/inven_ui.mes", id, out string value)
                && !string.IsNullOrWhiteSpace(value) ? value : fallback;

        private string EncumbranceName(int level)
        {
            string[] fallback = { "Unencumbered", "Light", "Moderate", "Medium", "Significant", "Heavy", "Severe" };
            return _resolver.Original.TryReadMessage("mes/critter.mes", level + 18, out string value)
                   && !string.IsNullOrWhiteSpace(value) ? value : fallback[Mathf.Clamp(level, 0, 6)];
        }

        private void HandleInventoryChanged(PersistentObjectState state, ObjectPlacement previous,
            ObjectPlacement current) => _dirty = true;

        private void HandleInventoryChanged(PersistentObjectState state, int previous, int current)
            => _dirty = true;

        private void HandleInventoryChanged(PersistentObjectState state, ObjectPlacement previous)
            => _dirty = true;

        public void HandleEscape()
        {
            if (QuantityPanelVisible) CancelQuantity();
            else _controller.ReturnToCharacterEditor();
        }

        private void Update()
        {
            if (!QuantityPanelVisible) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { CancelQuantity(); return; }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            { ConfirmQuantity(); return; }
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                SetQuantity(_selectedQuantity / 10);
                return;
            }
            for (int digit = 0; digit <= 9; digit++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha0 + digit))
                    || Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad0 + digit)))
                {
                    SetQuantity((int)Math.Min(_maximumQuantity, (long)_selectedQuantity * 10 + digit));
                    return;
                }
        }

        private void CenterShoppingWorldOnPlayer()
        {
            if (_centeredShoppingWorld || Camera.main == null || _session.PlayerState == null
                || !_session.TryGetLoadedObject(_session.PlayerState.Identity, out WorldObject player)) return;
            Vector3 cameraPosition = Camera.main.transform.position;
            cameraPosition.x = player.transform.position.x;
            cameraPosition.y = player.transform.position.y;
            Camera.main.transform.position = cameraPosition;
            _centeredShoppingWorld = true;
        }

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
            ApplySourceBarterBackgroundGeometry(center.Background);
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
            AddPaperDollSilhouettes(_merchantPaperDoll, RetailStartingEquipmentLayout.MerchantPaperDoll,
                RetailStartingEquipmentLayout.MerchantPaperDollSlots, _merchantEmptySlots, "Merchant");
            AddPaperDollSilhouettes(_playerPaperDoll, RetailStartingEquipmentLayout.PlayerPaperDoll,
                RetailStartingEquipmentLayout.PlayerPaperDollSlots, _playerEmptySlots, "Player");
            _merchantPaperDoll.gameObject.SetActive(false);
            _playerPaperDoll.gameObject.SetActive(false);
            RectTransform merchantPortrait = AddRect("Human Shopkeeper Portrait", _window,
                RetailStartingEquipmentLayout.MerchantPortrait);
            _merchantPortrait = merchantPortrait.gameObject.AddComponent<Image>();
            _merchantPortrait.preserveAspect = true;
            _merchantPortrait.raycastTarget = false;
            _merchantName = AddText("Human Shopkeeper Name", _window,
                RetailStartingEquipmentLayout.MerchantName, 27, SourceWhite);
            _merchantQuote = AddText("Human Shopkeeper Quote", _window,
                RetailStartingEquipmentLayout.MerchantQuote, 229, SourceWhite);
            for (int index = 0; index < _counters.Length; index++)
                _counters[index] = AddText($"Counter {index}", _window,
                    RetailStartingEquipmentLayout.CounterRects[index], 171, SourceWhite);
            _playerAttackIcon = AddSourceImage("Player Total Attack", _window, 676,
                RetailStartingEquipmentLayout.PlayerAttackIcon);
            _playerDefenseIcon = AddSourceImage("Player Total Defense", _window, 677,
                RetailStartingEquipmentLayout.PlayerDefenseIcon);
            _playerAttack = AddText("Player Attack", _window,
                RetailStartingEquipmentLayout.PlayerAttackValue,
                SourceUiBitmapText.InventoryStatFontSourceId, SourceWhite);
            _playerDefense = AddText("Player Defense", _window,
                RetailStartingEquipmentLayout.PlayerDefenseValue,
                SourceUiBitmapText.InventoryStatFontSourceId, SourceWhite);
            _playerWeight = AddText("Player Total Weight", _window,
                RetailStartingEquipmentLayout.PlayerWeight, 229, SourceWhite);
            _playerEncumbrance = AddText("Player Encumbrance", _window,
                RetailStartingEquipmentLayout.PlayerEncumbrance, 229, SourceWhite);
            _playerSpeed = AddText("Player Speed", _window,
                RetailStartingEquipmentLayout.PlayerSpeed, 229, SourceWhite);
            _merchantAttack = AddText("Merchant Attack", _window,
                RetailStartingEquipmentLayout.MerchantAttackValue,
                SourceUiBitmapText.Flare12FontSourceId, SourceWhite);
            _merchantDefense = AddText("Merchant Defense", _window,
                RetailStartingEquipmentLayout.MerchantDefenseValue,
                SourceUiBitmapText.Flare12FontSourceId, SourceWhite);

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
                    _controller.ArrangeInventory(_nextArrangeMode);
                    _nextArrangeMode = _nextArrangeMode == InventoryArrangeMode.Horizontal
                        ? InventoryArrangeMode.Vertical
                        : InventoryArrangeMode.Horizontal;
                    _dirty = true;
                    Synchronize();
                });
            _gambleBox = AddSourceImage("Gamble Drop Box", _window, RetailStartingEquipmentLayout.GambleBoxSourceId,
                RetailStartingEquipmentLayout.GambleBox);
            _useBox = AddSourceImage("Use Drop Box", _window, RetailStartingEquipmentLayout.UseBoxSourceId,
                RetailStartingEquipmentLayout.UseBox);
            _dropBox = AddSourceImage("Drop Drop Box", _window, RetailStartingEquipmentLayout.DropBoxSourceId,
                RetailStartingEquipmentLayout.DropBox);
            RefreshPanelControls();

            SourceUiButton continueButton = AddSourceButton("Shopkeeper Continue", _bottom, 321,
                new Vector2(675f, 55f), () => _controller.CompleteStartingEquipment());
            SourceUiButton backButton = AddSourceButton("Shopkeeper Back", _bottom, 323,
                new Vector2(69f, 55f), () => _controller.ReturnToCharacterEditor());
            BindNavigationHint(continueButton.gameObject, true);
            BindNavigationHint(backButton.gameObject, false);

            _messageIcon = AddSourceImage("Shopkeeper Message Icon", _bottom,
                RetailStartingEquipmentLayout.FeedbackIconSourceId, RetailStartingEquipmentLayout.MessageIcon);
            RectTransform itemIconRoot = AddRect("Shopkeeper Item Icon", _bottom,
                RetailStartingEquipmentLayout.MessageIcon);
            _messageItemIcon = itemIconRoot.gameObject.AddComponent<Image>();
            _messageItemIcon.preserveAspect = true;
            _messageItemIcon.raycastTarget = false;
            _messageItemIcon.gameObject.SetActive(false);
            _messageTitle = AddText("Shopkeeper Message Title", _bottom,
                RetailStartingEquipmentLayout.MessageTitle, 27, SourceWhite);
            _messageTitleRight = AddText("Shopkeeper Message Title Right", _bottom,
                RetailStartingEquipmentLayout.MessageTitle,
                SourceUiBitmapText.Flare12FontSourceId, SourceRed);
            _messageBody = AddRect("Shopkeeper Message Body", _bottom,
                RetailStartingEquipmentLayout.MessageBody);
            PrepareMessageLinePool();
            ClearMessage();
            BuildQuantityPanel();
            _merchantDropTarget = AddRect("Merchant Inventory Drop Target", _window,
                RetailStartingEquipmentLayout.MerchantGrid);
            Image merchantScrollInput = _merchantDropTarget.gameObject.AddComponent<Image>();
            merchantScrollInput.color = Color.clear;
            merchantScrollInput.raycastTarget = true;
            _merchantDropTarget.gameObject.AddComponent<RetailStartingEquipmentScrollTarget>()
                .Bind(ScrollMerchant);
            _merchantDropTarget.gameObject.AddComponent<RetailStartingEquipmentInventorySideTarget>()
                .Bind(isMerchant: true);
            _playerDropTarget = AddRect("Player Inventory Drop Target", _window,
                RetailStartingEquipmentLayout.PlayerGrid);
            Image playerDropInput = _playerDropTarget.gameObject.AddComponent<Image>();
            playerDropInput.color = Color.clear;
            playerDropInput.raycastTarget = true;
            _playerDropTarget.gameObject.AddComponent<RetailStartingEquipmentInventorySideTarget>()
                .Bind(isMerchant: false);
            _dynamic = AddRect("Shopkeeper Items", _window, RetailStartingEquipmentLayout.Window);
            _playerAttackHelp = AddCombatSummaryHover("Player Attack Help",
                RetailStartingEquipmentLayout.PlayerAttackHelp, () => _controller.Player, true);
            _playerDefenseHelp = AddCombatSummaryHover("Player Defense Help",
                RetailStartingEquipmentLayout.PlayerDefenseHelp, () => _controller.Player, false);
            _merchantAttackHelp = AddCombatSummaryHover("Merchant Attack Help",
                RetailStartingEquipmentLayout.MerchantAttackHelp, () => _controller.MerchantTarget, true);
            _merchantDefenseHelp = AddCombatSummaryHover("Merchant Defense Help",
                RetailStartingEquipmentLayout.MerchantDefenseHelp, () => _controller.MerchantTarget, false);
            _merchantScrollbar = AddScrollbar(_window, RetailStartingEquipmentLayout.MerchantScrollbar);

            var cursor = new GameObject("Shopkeeper Cursor",
                typeof(RectTransform), typeof(Image), typeof(SourceUiImage), typeof(RetailMainMenuCursorView));
            cursor.transform.SetParent(_canvasRoot.transform, false);
            cursor.GetComponent<RetailMainMenuCursorView>().Bind((RectTransform)_canvasRoot.transform, _resolver);
        }

        private void BuildQuantityPanel()
        {
            _quantityPanel = AddRect("Shopkeeper Quantity Panel", _bottom, new Rect(0f, 0f, 800f, 159f));
            AddSourceImage("Quantity Panel Background", _quantityPanel,
                RetailStartingEquipmentLayout.QuantityPanelSourceId, RetailStartingEquipmentLayout.QuantityPanel);
            RectTransform icon = AddRect("Quantity Item Icon", _quantityPanel,
                RetailStartingEquipmentLayout.QuantityItemIcon);
            _quantityItemRect = icon;
            _quantityItemIcon = icon.gameObject.AddComponent<Image>();
            _quantityItemIcon.preserveAspect = true;
            _quantityItemIcon.raycastTarget = false;
            _quantityValue = AddText("Quantity Value", _quantityPanel,
                RetailStartingEquipmentLayout.QuantityValue, SourceUiBitmapText.RollerFontSourceId, Color.black);
            AddSourceButton("Quantity Take All", _quantityPanel,
                RetailStartingEquipmentLayout.QuantityTakeAllSourceId,
                RetailStartingEquipmentLayout.QuantityTakeAll, () => SetQuantity(_maximumQuantity));
            AddSourceButton("Quantity Plus", _quantityPanel,
                RetailStartingEquipmentLayout.QuantityPlusSourceId,
                RetailStartingEquipmentLayout.QuantityPlus, () => SetQuantity(_selectedQuantity + 1));
            AddSourceButton("Quantity Minus", _quantityPanel,
                RetailStartingEquipmentLayout.QuantityMinusSourceId,
                RetailStartingEquipmentLayout.QuantityMinus, () => SetQuantity(_selectedQuantity - 1));
            AddSourceButton("Quantity Confirm", _quantityPanel,
                RetailStartingEquipmentLayout.QuantityOkSourceId,
                RetailStartingEquipmentLayout.QuantityOk, ConfirmQuantity);
            AddSourceButton("Quantity Cancel", _quantityPanel,
                RetailStartingEquipmentLayout.QuantityCancelSourceId,
                RetailStartingEquipmentLayout.QuantityCancel, CancelQuantity);
            _quantityPanel.gameObject.SetActive(false);
        }

        private static void ApplySourceBarterBackgroundGeometry(SourceUiImage source)
        {
            if (source == null) return;
            // redraw_inven blits the 360x400 barter ART at (0, 0), then blits the 442x400 player-inventory
            // ART at x=358.  Treating ART 235 as a full-width centered region exposed the live SHOPPING_MAP
            // through 220 pixels on the left and misplaced the merchant panel.
            RectTransform rect = (RectTransform)source.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(360f, 400f);
        }

        private void SetMerchantPortrait(PersistentObjectState merchant)
        {
            if (_merchantPortrait == null) return;
            _merchantPortrait.sprite = null;
            CharacterRace race = CharacterRace.Human;
            if (merchant != null && _session.Characters.TryGet(merchant.Identity,
                    out PersistentCharacterState character)) race = character.Race;
            // intgame_examine_portrait only uses an NPC's authored portrait when that NPC follows the PC.
            // Ordinary merchants therefore use intgame_race_icons; the starting merchant is Human (375).
            int sourceId = race switch
            {
                CharacterRace.Human => 375,
                CharacterRace.Dwarf => 378,
                CharacterRace.Elf or CharacterRace.DarkElf => 376,
                CharacterRace.HalfElf => 377,
                CharacterRace.Gnome => 379,
                CharacterRace.Halfling => 380,
                CharacterRace.HalfOrc or CharacterRace.Orc => 381,
                CharacterRace.HalfOgre or CharacterRace.Ogre => 382,
                _ => 375,
            };
            if (_resolver.TryResolve(new UiAssetKey(sourceId), out UiResolvedAsset portrait))
                _merchantPortrait.sprite = portrait.Sprite;
        }

        private void RebuildItems(IReadOnlyList<GameUiMerchantItemView> wares,
            IReadOnlyList<GameUiMerchantItemView> player)
        {
            ClearDynamic();
            _merchantMaximumScrollRow = MaximumScrollRow(wares);
            _merchantScrollRow = Mathf.Clamp(_merchantScrollRow, 0f, _merchantMaximumScrollRow);
            RectTransform merchantItems = AddClippedItemLayer("Merchant Inventory Items",
                RetailStartingEquipmentLayout.MerchantGrid);
            merchantItems.gameObject.AddComponent<RetailStartingEquipmentScrollTarget>().Bind(ScrollMerchant);
            RectTransform playerItems = AddClippedItemLayer("Player Inventory Items",
                RetailStartingEquipmentLayout.PlayerGrid);
            if (_merchantInventoryMode)
                foreach (GameUiMerchantItemView item in wares)
                    AddTradeItem(merchantItems, item, LocalItemRect(
                        RetailStartingEquipmentLayout.MerchantGrid, item.Item.InventoryLocation,
                        item.Item.InventoryFootprint, _merchantScrollRow));
            if (_playerInventoryMode)
                foreach (GameUiMerchantItemView item in player)
                    AddTradeItem(playerItems, item, LocalItemRect(
                        RetailStartingEquipmentLayout.PlayerGrid, item.Item.InventoryLocation,
                        item.Item.InventoryFootprint));
            IReadOnlyList<GameUiItemView> merchantEquipment = _merchantInventoryMode
                ? Array.Empty<GameUiItemView>()
                : _controller.ProjectMerchantEquipment();
            IReadOnlyList<GameUiItemView> playerEquipment = _playerInventoryMode
                ? Array.Empty<GameUiItemView>()
                : _controller.ProjectInventory().Where(value => value.IsEquipped).ToArray();
            SetEmptySlotVisibility(_merchantEmptySlots, merchantEquipment);
            SetEmptySlotVisibility(_playerEmptySlots, playerEquipment);
            if (!_merchantInventoryMode)
                foreach (GameUiItemView item in merchantEquipment)
                    AddPaperDollItem(item, RetailStartingEquipmentLayout.MerchantPaperDollSlots,
                        playerOwned: false);
            if (!_playerInventoryMode)
                foreach (GameUiItemView item in playerEquipment)
                    AddPaperDollItem(item, RetailStartingEquipmentLayout.PlayerPaperDollSlots,
                        playerOwned: true);
            if (_merchantScrollbar != null)
            {
                _merchantScrollbar.gameObject.SetActive(_merchantInventoryMode && _merchantMaximumScrollRow > 0);
                _merchantScrollbar.SetValueWithoutNotify(_merchantMaximumScrollRow == 0
                    ? 1f : 1f - _merchantScrollRow / _merchantMaximumScrollRow);
                _merchantScrollbar.size = _merchantMaximumScrollRow == 0 ? 1f
                    : Mathf.Clamp01(7f / (7f + _merchantMaximumScrollRow));
            }
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
            if (_playerAttackIcon != null) _playerAttackIcon.gameObject.SetActive(!_playerInventoryMode);
            if (_playerDefenseIcon != null) _playerDefenseIcon.gameObject.SetActive(!_playerInventoryMode);
            if (_playerAttack != null) _playerAttack.gameObject.SetActive(!_playerInventoryMode);
            if (_playerDefense != null) _playerDefense.gameObject.SetActive(!_playerInventoryMode);
            if (_playerWeight != null) _playerWeight.gameObject.SetActive(!_playerInventoryMode);
            if (_playerEncumbrance != null) _playerEncumbrance.gameObject.SetActive(!_playerInventoryMode);
            if (_playerSpeed != null) _playerSpeed.gameObject.SetActive(!_playerInventoryMode);
            if (_merchantAttack != null) _merchantAttack.gameObject.SetActive(!_merchantInventoryMode);
            if (_merchantDefense != null) _merchantDefense.gameObject.SetActive(!_merchantInventoryMode);
            if (_playerAttackHelp != null) _playerAttackHelp.gameObject.SetActive(!_playerInventoryMode);
            if (_playerDefenseHelp != null) _playerDefenseHelp.gameObject.SetActive(!_playerInventoryMode);
            if (_merchantAttackHelp != null) _merchantAttackHelp.gameObject.SetActive(!_merchantInventoryMode);
            if (_merchantDefenseHelp != null) _merchantDefenseHelp.gameObject.SetActive(!_merchantInventoryMode);
            if (_merchantDropTarget != null) _merchantDropTarget.gameObject.SetActive(_merchantInventoryMode);
            if (_playerDropTarget != null) _playerDropTarget.gameObject.SetActive(_playerInventoryMode);
        }

        private RectTransform AddClippedItemLayer(string name, Rect bounds)
        {
            RectTransform layer = AddRect(name, _dynamic, bounds);
            layer.gameObject.AddComponent<RectMask2D>();
            return layer;
        }

        private static Rect LocalItemRect(Rect grid, int location, InventoryFootprint footprint,
            float rowOffset = 0f)
        {
            Rect absolute = RetailStartingEquipmentLayout.ItemRect(grid, location, footprint, rowOffset);
            return new Rect(absolute.x - grid.x, absolute.y - grid.y, absolute.width, absolute.height);
        }

        private int MaximumScrollRow(IReadOnlyList<GameUiMerchantItemView> wares)
        {
            int rows = 0;
            foreach (GameUiMerchantItemView view in wares)
                rows = Math.Max(rows, view.Item.InventoryLocation / RetailStartingEquipmentLayout.GridColumns
                                      + Math.Max(1, view.Item.InventoryFootprint.Height));
            return Math.Max(0, rows - 7);
        }

        private void ScrollMerchant(float delta)
        {
            if (!_merchantInventoryMode || _merchantMaximumScrollRow == 0) return;
            _merchantScrollRow = Mathf.Clamp(_merchantScrollRow - Mathf.Sign(delta), 0f, _merchantMaximumScrollRow);
            _dirty = true;
            Synchronize();
        }

        private Scrollbar AddScrollbar(RectTransform parent, Rect rect)
        {
            RectTransform root = AddRect("Merchant Inventory Scrollbar", parent, rect);
            Image track = root.gameObject.AddComponent<Image>();
            track.color = Color.clear;
            Scrollbar scrollbar = root.gameObject.AddComponent<Scrollbar>();
            AddSourceImage("Scroll Up", root, RetailStartingEquipmentLayout.ScrollUpSourceId,
                new Rect(0f, 0f, rect.width, 17f));
            AddSourceImage("Scroll Down", root, RetailStartingEquipmentLayout.ScrollDownSourceId,
                new Rect(0f, rect.height - 17f, rect.width, 17f));
            RectTransform slide = AddRect("Slide", root, new Rect(0f, 17f, rect.width, rect.height - 34f));
            RectTransform handle = AddRect("Handle", slide, new Rect(3f, 0f, 11f, 40f));
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = Color.clear;
            AddSourceImage("Thumb Top", handle, RetailStartingEquipmentLayout.ScrollThumbTopSourceId,
                new Rect(0f, 0f, 11f, 5f));
            AddSourceImage("Thumb Middle", handle, RetailStartingEquipmentLayout.ScrollThumbMiddleSourceId,
                new Rect(0f, 5f, 11f, 28f));
            AddSourceImage("Thumb Bottom", handle, RetailStartingEquipmentLayout.ScrollThumbBottomSourceId,
                new Rect(0f, 33f, 11f, 7f));
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.numberOfSteps = 0;
            scrollbar.onValueChanged.AddListener(value =>
            {
                _merchantScrollRow = (1f - value) * _merchantMaximumScrollRow;
                _dirty = true;
                Synchronize();
            });
            return scrollbar;
        }

        private void AddPaperDollItem(GameUiItemView item,
            IReadOnlyDictionary<WornLocation, Rect> slots, bool playerOwned)
        {
            if (!item.WornLocation.HasValue || !slots.TryGetValue(item.WornLocation.Value, out Rect bounds)) return;
            RectTransform root = AddRect($"Equipped {item.Identity.Key}", _dynamic, bounds);
            Image hit = root.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            PreparedItemPresentation presentation = PrepareItemPresentation(item);
            SourceHoverHintTarget hint = root.gameObject.AddComponent<SourceHoverHintTarget>();
            hint.Bind(() => ShowItem(presentation), ClearMessage);
            if (playerOwned)
            {
                var view = new GameUiMerchantItemView
                {
                    Item = item,
                    Direction = EconomyTransactionDirection.SellToMerchant,
                    Failure = EconomyFailure.ItemRejected,
                };
                RetailStartingEquipmentDragTarget drag =
                    RetailInventoryDragPresenter.AddDragTarget<RetailStartingEquipmentDragTarget>(root.gameObject);
                drag.Bind(_dynamic,
                    eventData => DropItem(view, eventData),
                    clicked: null,
                    eventData => RefreshActionBoxes(view, eventData.position, eventData.pressEventCamera),
                    ResetActionBoxes);
            }
            if (item.InventoryArtId.HasValue
                && _resolver.Original.TryResolveInventoryItem(item.InventoryArtId.Value,
                    out RetailItemArtAsset asset))
            {
                Image image = root.gameObject.AddComponent<Image>();
                image.sprite = asset.Sprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }
        }

        private void AddPaperDollSilhouettes(SourceUiImage panel, Rect panelBounds,
            IReadOnlyDictionary<WornLocation, Rect> slots,
            IDictionary<WornLocation, SourceUiImage> targets, string prefix)
        {
            RectTransform parent = (RectTransform)panel.transform;
            foreach (RetailEquipmentSlotDefinition definition in RetailInventoryEquipmentLayout.EquipmentSlots)
            {
                if (!slots.TryGetValue(definition.Location, out Rect absolute)) continue;
                Rect local = new(absolute.x - panelBounds.x, absolute.y - panelBounds.y,
                    absolute.width, absolute.height);
                targets[definition.Location] = AddSourceImage(
                    $"{prefix} Empty {definition.Location}", parent, definition.EmptySourceId, local);
            }
        }

        private static void SetEmptySlotVisibility(
            IReadOnlyDictionary<WornLocation, SourceUiImage> silhouettes,
            IReadOnlyList<GameUiItemView> equipment)
        {
            foreach (KeyValuePair<WornLocation, SourceUiImage> pair in silhouettes)
                pair.Value.gameObject.SetActive(equipment.All(value => value.WornLocation != pair.Key));
        }

        private void AddTradeItem(RectTransform layer, GameUiMerchantItemView view, Rect bounds)
        {
            PreparedItemPresentation presentation = PrepareItemPresentation(view);
            RectTransform root = AddRect($"{view.Direction} {view.Item.Identity.Key}", layer, bounds);
            Image hit = root.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            root.gameObject.AddComponent<RetailStartingEquipmentInventorySideTarget>()
                .Bind(view.Direction == EconomyTransactionDirection.BuyFromMerchant);
            bool physicalMoney = view.Item.Type == Arcanum.Formats.Objects.ObjectType.Gold;
            if (!physicalMoney)
            {
                Button button = root.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                // Source barter still accepts the gesture when a transaction will fail so that the merchant can
                // present the authoritative reason (most notably insufficient Gold).
                button.interactable = true;
                button.onClick.AddListener(() => BeginOrExecute(view));
                RetailStartingEquipmentDragTarget drag =
                    RetailInventoryDragPresenter.AddDragTarget<RetailStartingEquipmentDragTarget>(root.gameObject);
                drag.Bind(_dynamic,
                    eventData => DropItem(view, eventData),
                    () => BeginOrExecute(view),
                    eventData => RefreshActionBoxes(view, eventData.position, eventData.pressEventCamera),
                    ResetActionBoxes);
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(drag.SubmitClick);
            }
            else if (view.Direction == EconomyTransactionDirection.SellToMerchant)
            {
                // The player's authoritative Gold stack remains a physical source-inventory object. It may be
                // rearranged within the player grid, but it is not an independently sellable second money balance.
                RetailStartingEquipmentDragTarget drag =
                    RetailInventoryDragPresenter.AddDragTarget<RetailStartingEquipmentDragTarget>(root.gameObject);
                drag.Bind(_dynamic,
                    eventData => DropPlayerGold(view, eventData),
                    clicked: null,
                    moved: null,
                    ResetActionBoxes);
            }
            SourceHoverHintTarget hint = root.gameObject.AddComponent<SourceHoverHintTarget>();
            hint.Bind(() => ShowItem(presentation), ClearMessage);

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
            // redraw_inven paints only the item ART in barter grids. Stack quantity belongs to examination and
            // the rotating quantity transaction window; it is never overlaid on the inventory sprite.
        }

        private PreparedItemPresentation PrepareItemPresentation(GameUiMerchantItemView view)
        {
            PreparedItemPresentation prepared = PrepareItemPresentation(view.Item);
            prepared.View = view;
            prepared.MerchantQuote = MerchantLine(view.Direction, view.UnitPrice, view.Item.Identity,
                view.ReportedMarkupPercent);
            return prepared;
        }

        private PreparedItemPresentation PrepareItemPresentation(GameUiItemView item)
        {
            GameUiItemInspectionView inspection = _controller.ProjectItemInspection(item.Identity);
            bool gold = item.Type == Arcanum.Formats.Objects.ObjectType.Gold;
            var prepared = new PreparedItemPresentation
            {
                Title = inspection?.Name ?? item.Name ?? string.Empty,
                TitleRight = inspection?.TitleRightLine ?? string.Empty,
                Properties = inspection?.PropertyLines == null
                    ? string.Empty : string.Join("  ", inspection.PropertyLines),
                Quantity = gold ? $"Quantity: {item.Quantity}" : inspection?.QuantityLine ?? string.Empty,
                Weight = inspection?.WeightLine ?? $"Weight: {item.Weight} stone",
                Condition = inspection?.ConditionLine ?? string.Empty,
                MessageIconSourceId = inspection?.MessageIconSourceId
                    ?? (gold ? RetailStartingEquipmentLayout.GoldInspectionIconSourceId
                        : RetailStartingEquipmentLayout.FeedbackIconSourceId),
                IsGold = gold,
            };
            PrepareItemPresentationLayouts(prepared);
            return prepared;
        }

        private void PrepareItemPresentationLayouts(PreparedItemPresentation prepared)
        {
            _merchantQuote.PrepareLayout(prepared.MerchantQuote, _merchantQuote.FontSourceId);
            _messageTitle.PrepareLayout(prepared.Title, _messageTitle.FontSourceId);
            _messageTitleRight.PrepareLayout(prepared.TitleRight, _messageTitleRight.FontSourceId);
            string[] lines = { prepared.Properties, prepared.Quantity, prepared.Weight, prepared.Condition };
            foreach (SourceUiBitmapText presenter in _messageLinePool)
                foreach (string line in lines)
                    presenter.PrepareLayout(line, SourceUiBitmapText.Flare12FontSourceId);
            _messageIcon.PrepareKey(new UiAssetKey(prepared.MessageIconSourceId));
        }

        private bool DropPlayerGold(GameUiMerchantItemView view, PointerEventData eventData)
        {
            if (eventData == null || view.Direction != EconomyTransactionDirection.SellToMerchant
                || !RectTransformUtility.RectangleContainsScreenPoint(
                    _playerDropTarget, eventData.position, eventData.pressEventCamera)
                || !TryInventoryLocation(eventData.position, eventData.pressEventCamera,
                    RetailStartingEquipmentLayout.PlayerGrid, 0f, out int destinationLocation)) return false;
            bool moved = _controller.MoveInventoryItem(_controller.Player, view.Item.Identity, destinationLocation);
            if (moved) _dirty = true;
            Synchronize();
            return moved;
        }

        private bool BeginOrExecute(GameUiMerchantItemView view, int destinationInventoryLocation = -1)
        {
            if (view.Item.Quantity > 1
                && view.Item.Type is Arcanum.Formats.Objects.ObjectType.Ammo
                    or Arcanum.Formats.Objects.ObjectType.Gold)
            {
                BeginQuantity(view, destinationInventoryLocation);
                return true;
            }
            return Execute(view, 1, destinationInventoryLocation);
        }

        private bool Execute(GameUiMerchantItemView view, int quantity, int destinationInventoryLocation = -1)
        {
            bool success = _controller.Trade(view.Direction, view.Item.Identity, quantity,
                destinationInventoryLocation);
            _dirty = true;
            Synchronize();
            if (!success)
            {
                SetText(_merchantQuote, _controller.Feedback);
                return false;
            }

            EconomyTransactionDirection reverse = view.Direction == EconomyTransactionDirection.BuyFromMerchant
                ? EconomyTransactionDirection.SellToMerchant
                : EconomyTransactionDirection.BuyFromMerchant;
            GameUiMerchantItemView reverseView = (reverse == EconomyTransactionDirection.SellToMerchant
                    ? _controller.ProjectPlayerSaleItems()
                    : _controller.ProjectMerchantWares())
                .FirstOrDefault(value => value.Item.Identity == view.Item.Identity);
            int reversePrice = reverseView?.UnitPrice ?? 0;
            SetText(_merchantQuote, MerchantLine(reverse, reversePrice, view.Item.Identity,
                reverseView?.ReportedMarkupPercent));
            return true;
        }

        private bool DropItem(GameUiMerchantItemView view, Vector2 screenPosition, Camera eventCamera)
        {
            if (IsInside(_gambleBox, screenPosition, eventCamera)) return DropOnGamble(view);
            if (IsInside(_useBox, screenPosition, eventCamera)) return DropOnUse(view);
            if (IsInside(_dropBox, screenPosition, eventCamera)) return DropOnWorld(view);
            bool overMerchant = RectTransformUtility.RectangleContainsScreenPoint(
                _merchantDropTarget, screenPosition, eventCamera);
            bool overPlayer = RectTransformUtility.RectangleContainsScreenPoint(
                _playerDropTarget, screenPosition, eventCamera);
            if (!overMerchant && !overPlayer) return false;

            bool currentlyMerchant = view.Direction == EconomyTransactionDirection.BuyFromMerchant;
            Rect destinationGrid = overMerchant
                ? RetailStartingEquipmentLayout.MerchantGrid
                : RetailStartingEquipmentLayout.PlayerGrid;
            float destinationScroll = overMerchant ? _merchantScrollRow : 0f;
            if (!TryInventoryLocation(screenPosition, eventCamera, destinationGrid, destinationScroll,
                    out int destinationLocation)) return false;
            // Source barter never permits rearranging the merchant's stock. The drag is presentation-only and
            // sub_575360 restores the original location when the source and target are both the merchant.
            if (currentlyMerchant && overMerchant) return false;
            if (!_session.Economy.TryResolveMerchant(_controller.MerchantTarget,
                    out ArcanumObjectId merchantOwner, out _)) return false;
            if (!currentlyMerchant && overPlayer)
            {
                bool moved = _controller.MoveInventoryItem(_controller.Player, view.Item.Identity,
                    destinationLocation);
                if (moved) _dirty = true;
                Synchronize();
                return moved;
            }

            ArcanumObjectId destinationOwner = currentlyMerchant ? _controller.Player : merchantOwner;
            if (!_session.CanReceiveInventoryItemAt(destinationOwner, view.Item.Identity, destinationLocation))
                return false;
            return BeginOrExecute(view, destinationLocation);
        }

        private bool DropItem(GameUiMerchantItemView view, PointerEventData eventData)
        {
            if (eventData == null) return false;
            return DropItem(view, eventData.position, eventData.pressEventCamera);
        }

        private static bool IsHitWithin(Transform hit, Component target)
            => hit != null && target != null && hit.IsChildOf(target.transform);

        private static bool IsInside(Component target, Vector2 position, Camera camera)
            => target != null && RectTransformUtility.RectangleContainsScreenPoint(
                (RectTransform)target.transform, position, camera);

        private void RefreshActionBoxes(GameUiMerchantItemView view, Vector2 position, Camera camera)
        {
            bool merchantItem = view.Direction == EconomyTransactionDirection.BuyFromMerchant;
            int gambling = _session.Progression.GetEffectiveSkillRank(
                _controller.MerchantTarget, CharacterSkill.Gambling);
            _gambleBox?.SetKey(new UiAssetKey(IsInside(_gambleBox, position, camera)
                ? merchantItem && gambling > 0 ? 344 : 346
                : RetailStartingEquipmentLayout.GambleBoxSourceId));
            _useBox?.SetKey(new UiAssetKey(IsInside(_useBox, position, camera)
                ? merchantItem || !CanUseInStartingShop(view) ? 347 : 343
                : RetailStartingEquipmentLayout.UseBoxSourceId));
            _dropBox?.SetKey(new UiAssetKey(IsInside(_dropBox, position, camera)
                ? merchantItem ? 348 : 225
                : RetailStartingEquipmentLayout.DropBoxSourceId));
        }

        private void ResetActionBoxes()
        {
            _gambleBox?.SetKey(new UiAssetKey(RetailStartingEquipmentLayout.GambleBoxSourceId));
            _useBox?.SetKey(new UiAssetKey(RetailStartingEquipmentLayout.UseBoxSourceId));
            _dropBox?.SetKey(new UiAssetKey(RetailStartingEquipmentLayout.DropBoxSourceId));
        }

        private bool DropOnGamble(GameUiMerchantItemView view)
        {
            if (view.Direction != EconomyTransactionDirection.BuyFromMerchant)
                return SetGamblingRefusal(view, 3300);
            int merchantGambling = _session.Progression.GetEffectiveSkillRank(
                _controller.MerchantTarget, CharacterSkill.Gambling);
            if (merchantGambling == 0) return SetGamblingRefusal(view, 5600);
            // The source continues through a Gambling skill invocation. That gameplay transaction remains outside
            // this bounded UI correction; preserve the item and expose the source capability boundary.
            return false;
        }

        private bool SetGamblingRefusal(GameUiMerchantItemView view, int first)
        {
            SetText(_merchantQuote, ReadNpcLine(first, view.Item.Identity));
            return false;
        }

        private string ReadNpcLine(int first, ArcanumObjectId item)
        {
            bool merchantFemale = _session.Characters.TryGet(_controller.MerchantTarget, out var merchant)
                                  && merchant.Gender == CharacterGender.Female;
            bool pcFemale = _session.Characters.TryGet(_controller.Player, out var pc)
                            && pc.Gender == CharacterGender.Female;
            string path = $"mes/gd_npc_{(merchantFemale ? "f" : "m")}2{(pcFemale ? "f" : "m")}.mes";
            int seed = item.GetHashCode() & int.MaxValue;
            for (int offset = 0; offset < 100; offset++)
            {
                int id = first + (seed + offset) % 100;
                if (_resolver.Original.TryReadMessage(path, id, out string value)
                    && !string.IsNullOrWhiteSpace(value)) return value.Trim();
            }
            return string.Empty;
        }

        private bool CanUseInStartingShop(GameUiMerchantItemView view)
        {
            if (view.Direction == EconomyTransactionDirection.BuyFromMerchant) return false;
            return _session.TryGetObjectState(view.Item.Identity, out PersistentObjectState state)
                   && state.UseScriptNum != 0;
        }

        private bool DropOnUse(GameUiMerchantItemView view)
        {
            // Source barter refuses merchant-owned items and routes eligible player items through item_use_on_obj.
            // Scripted item-use remains outside this bounded presentation layer, so preserve the object unchanged.
            if (view.Direction == EconomyTransactionDirection.BuyFromMerchant
                || !_session.TryGetObjectState(view.Item.Identity, out PersistentObjectState state)
                || state.UseScriptNum == 0) return false;
            return false;
        }

        private bool DropOnWorld(GameUiMerchantItemView view)
        {
            if (view.Direction == EconomyTransactionDirection.BuyFromMerchant) return false;
            bool dropped = _controller.DropInventoryItem(view.Item.Identity);
            if (dropped) _dirty = true;
            Synchronize();
            return dropped;
        }

        private void BeginQuantity(GameUiMerchantItemView view, int destinationInventoryLocation)
        {
            _pendingTransaction = view;
            _pendingDestinationLocation = destinationInventoryLocation;
            _maximumQuantity = Math.Max(1, view.Item.Quantity);
            _selectedQuantity = 0;
            _quantityItemIcon.sprite = null;
            if (view.Item.InventoryArtId.HasValue
                && _resolver.Original.TryResolveInventoryItem(view.Item.InventoryArtId.Value,
                    out RetailItemArtAsset asset))
            {
                _quantityItemIcon.sprite = asset.Sprite;
                // intgame draws the synthesized Gold/Ammo item ART at native dimensions at (269,93).
                _quantityItemRect.anchoredPosition = new Vector2(
                    RetailStartingEquipmentLayout.QuantityItemIcon.x,
                    -RetailStartingEquipmentLayout.QuantityItemIcon.y);
                _quantityItemRect.sizeDelta = asset.LogicalSize;
            }
            _quantityPanel.gameObject.SetActive(true);
            RefreshQuantity();
        }

        private void SetQuantity(int value)
        {
            _selectedQuantity = Mathf.Clamp(value, 0, _maximumQuantity);
            RefreshQuantity();
        }

        private void RefreshQuantity()
        {
            // roller_ui_draw(..., 6, 0) paints all six source wheels, including the leading zeroes.
            SetText(_quantityValue, _selectedQuantity.ToString("D6"), Color.black);
            ((RectTransform)_quantityValue.transform).anchoredPosition = new Vector2(
                RetailStartingEquipmentLayout.QuantityValue.x,
                -RetailStartingEquipmentLayout.QuantityValue.y);
        }

        private void ConfirmQuantity()
        {
            if (!QuantityPanelVisible || _pendingTransaction == null) return;
            GameUiMerchantItemView pending = _pendingTransaction;
            int quantity = _selectedQuantity;
            int destinationInventoryLocation = _pendingDestinationLocation;
            CloseQuantity();
            if (quantity == 0) return;
            Execute(pending, quantity, destinationInventoryLocation);
        }

        private void CancelQuantity() => CloseQuantity();

        private void CloseQuantity()
        {
            if (_quantityPanel != null) _quantityPanel.gameObject.SetActive(false);
            _pendingTransaction = null;
            _selectedQuantity = 0;
            _maximumQuantity = 0;
            _pendingDestinationLocation = -1;
        }

        private bool TryInventoryLocation(Vector2 screenPosition, Camera eventCamera, Rect grid, float rowOffset,
            out int location)
        {
            location = -1;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _window, screenPosition, eventCamera, out Vector2 local)) return false;
            float sourceX = local.x + _window.rect.width * _window.pivot.x;
            float sourceY = _window.rect.height * (1f - _window.pivot.y) - local.y;
            float x = sourceX - grid.x;
            float y = sourceY - grid.y;
            if (x < 0f || y < 0f || x >= grid.width || y >= grid.height) return false;
            int column = Mathf.FloorToInt(x / RetailStartingEquipmentLayout.CellSize);
            int row = Mathf.FloorToInt(y / RetailStartingEquipmentLayout.CellSize + rowOffset);
            location = row * RetailStartingEquipmentLayout.GridColumns + column;
            return true;
        }

        private void ShowItem(PreparedItemPresentation prepared)
        {
            SetText(_merchantQuote, prepared.MerchantQuote);
            SetText(_messageTitle, prepared.Title, SourceWhite);
            SetRightAlignedText(_messageTitleRight, prepared.TitleRight,
                RetailStartingEquipmentLayout.MessageTitle, SourceRed);
            if (prepared.IsGold) RebuildGoldDetails(prepared);
            else RebuildItemDetails(prepared);
            _messageIcon.gameObject.SetActive(true);
            _messageIcon.SetKey(new UiAssetKey(prepared.MessageIconSourceId));
            if (_messageItemIcon != null) _messageItemIcon.gameObject.SetActive(false);
        }

        private RectTransform AddCombatSummaryHover(string name, Rect rect,
            Func<ArcanumObjectId> owner, bool attack)
        {
            RectTransform target = AddRect(name, _window, rect);
            Image input = target.gameObject.AddComponent<Image>();
            input.color = Color.clear;
            input.raycastTarget = true;
            target.gameObject.AddComponent<SourceHoverHintTarget>().Bind(
                () => ShowCombatSummary(owner(), attack), ClearMessage);
            return target;
        }

        private void ShowCombatSummary(ArcanumObjectId owner, bool attack)
        {
            GameUiInventorySummaryView summary = _controller.ProjectInventorySummary(owner);
            if (summary == null) return;
            ClearMessageBody(_messageBody);
            _messageItemIcon.gameObject.SetActive(false);
            _messageIcon.gameObject.SetActive(true);
            _messageIcon.SetKey(new UiAssetKey(attack ? 675 : 674));
            if (attack)
            {
                SetText(_messageTitle,
                    $"{ReadIntgameMessage(57, "Total Attack")}: {summary.TotalAttack}", SourceWhite);
                AddCombatLine("Base To Hit", 0f, 0f,
                    $"{ReadIntgameMessage(69, "Base To Hit")}: {summary.BaseToHit}%");
                AddCombatLine("Fatigue", 0f, 18f,
                    $"{ReadIntgameMessage(59, "Fatigue")}: "
                    + DamageRange(summary.FatigueDamageMinimum, summary.FatigueDamageMaximum));
                AddCombatLine("Fire Damage", 0f, 36f,
                    $"{ReadIntgameMessage(61, "Fire Damage")}: "
                    + DamageRange(summary.FireDamageMinimum, summary.FireDamageMaximum));
                AddCombatLine("Damage", 145f, 0f,
                    $"{ReadIntgameMessage(58, "Damage")}: "
                    + DamageRange(summary.NormalDamageMinimum, summary.NormalDamageMaximum));
                AddCombatLine("Electrical Damage", 145f, 18f,
                    $"{ReadIntgameMessage(60, "Electrical Damage")}: "
                    + DamageRange(summary.ElectricalDamageMinimum, summary.ElectricalDamageMaximum));
                AddCombatLine("Poison", 145f, 36f,
                    $"{ReadIntgameMessage(62, "Poison")}: "
                    + DamageRange(summary.PoisonDamageMinimum, summary.PoisonDamageMaximum));
            }
            else
            {
                SetText(_messageTitle,
                    $"{ReadIntgameMessage(63, "Total Defense")}: {summary.TotalDefense}", SourceWhite);
                AddCombatLine("Total AC", 0f, 0f,
                    $"{ReadIntgameMessage(70, "Total AC")}: {summary.ArmorClass}");
                AddCombatLine("Magic Resistance", 0f, 18f,
                    $"{ReadIntgameMessage(65, "Magic Resistance")}: {ResistanceValue(summary.MagicResistance)}");
                AddCombatLine("Fire Resistance", 0f, 36f,
                    $"{ReadIntgameMessage(67, "Fire Resistance")}: {ResistanceValue(summary.FireResistance)}");
                AddCombatLine("Damage Resistance", 145f, 0f,
                    $"{ReadIntgameMessage(64, "Damage Resistance")}: {ResistanceValue(summary.DamageResistance)}");
                AddCombatLine("Electrical Resistance", 145f, 18f,
                    $"{ReadIntgameMessage(66, "Electrical Resistance")}: {ResistanceValue(summary.ElectricalResistance)}");
                AddCombatLine("Poison Resistance", 145f, 36f,
                    $"{ReadIntgameMessage(68, "Poison Resistance")}: {ResistanceValue(summary.PoisonResistance)}");
            }
        }

        private void AddCombatLine(string name, float x, float y, string value)
        {
            SourceUiBitmapText line = RentMessageLine(name,
                new Rect(x, y, 146f, 18f), SourceUiBitmapText.Flare12FontSourceId, SourceWhite);
            SetText(line, value, SourceWhite);
        }

        private string ReadIntgameMessage(int id, string fallback)
            => _resolver.Original.TryReadMessage("mes/intgame.mes", id, out string value)
               && !string.IsNullOrWhiteSpace(value) ? value.Trim() : fallback;

        private static string DamageRange(int minimum, int maximum)
            => maximum == 0 ? "0" : $"{minimum}-{maximum}";

        private static string ResistanceValue(int value)
            => value == 0 ? "0" : $"{value}%";

        private void RebuildItemDetails(PreparedItemPresentation prepared)
        {
            ClearMessageBody(_messageBody);
            SourceUiBitmapText propertyText = RentMessageLine("Item Properties",
                new Rect(0f, 0f, _messageBody.rect.width, 18f),
                SourceUiBitmapText.Flare12FontSourceId, SourceWhite);
            SetText(propertyText, prepared.Properties, SourceWhite);
            if (!string.IsNullOrEmpty(prepared.Quantity))
            {
                float quantityY = string.IsNullOrEmpty(prepared.Properties) ? 0f : 18f;
                SourceUiBitmapText quantity = RentMessageLine("Item Quantity",
                    new Rect(0f, quantityY, _messageBody.rect.width, 18f),
                    SourceUiBitmapText.Flare12FontSourceId, SourceWhite);
                SetText(quantity, prepared.Quantity, SourceWhite);
            }
            SourceUiBitmapText weight = RentMessageLine("Item Weight",
                new Rect(0f, 36f, _messageBody.rect.width, 18f),
                SourceUiBitmapText.Flare12FontSourceId, SourceRed);
            SetText(weight, prepared.Weight, SourceRed);
            if (!string.IsNullOrEmpty(prepared.Condition))
            {
                SourceUiBitmapText condition = RentMessageLine("Item Condition",
                    new Rect(0f, 36f, _messageBody.rect.width, 18f),
                    SourceUiBitmapText.Flare12FontSourceId, SourceRed);
                SetText(condition, prepared.Condition, SourceRed);
                RectTransform rect = (RectTransform)condition.transform;
                rect.anchoredPosition = new Vector2(Mathf.Max(0f, _messageBody.rect.width - rect.sizeDelta.x), -36f);
            }
        }

        private string MerchantLine(EconomyTransactionDirection direction, int price, ArcanumObjectId item,
            int? reportedMarkupPercent)
        {
            bool merchantFemale = _session.Characters.TryGet(_controller.MerchantTarget, out var merchant)
                                  && merchant.Gender == CharacterGender.Female;
            bool pcFemale = _session.Characters.TryGet(_controller.Player, out var pc)
                            && pc.Gender == CharacterGender.Female;
            string path = $"mes/gd_npc_{(merchantFemale ? "f" : "m")}2{(pcFemale ? "f" : "m")}.mes";
            int first = direction == EconomyTransactionDirection.BuyFromMerchant
                ? price == 0 ? 900 : 800
                : price == 0 ? 1200 : 1100;
            int seed = item.GetHashCode() & int.MaxValue;
            for (int offset = 0; offset < 100; offset++)
            {
                int id = first + (seed + offset) % 100;
                if (!_resolver.Original.TryReadMessage(path, id, out string value)
                    || string.IsNullOrWhiteSpace(value)) continue;
                string line = value.Trim().Replace("%d", price.ToString());
                return reportedMarkupPercent.HasValue
                    ? $"{line}({reportedMarkupPercent.Value}%)"
                    : line;
            }
            return string.Empty;
        }

        private void BindNavigationHint(GameObject target, bool next)
        {
            SourceHoverHintTarget hint = target.GetComponent<SourceHoverHintTarget>()
                                         ?? target.AddComponent<SourceHoverHintTarget>();
            hint.Bind(() =>
            {
                ClearMessage();
                SetText(_messageTitle, ReadMainMenuMessage(6999, "Action:"), SourceWhite);
                RebuildWrappedText(_messageBody,
                    ReadMainMenuMessage(next ? 7003 : 7002, next ? "Next Screen" : "Previous Screen"), 42, 2);
                _messageIcon.gameObject.SetActive(true);
                _messageIcon.SetKey(new UiAssetKey(RetailStartingEquipmentLayout.FeedbackIconSourceId));
            }, ClearMessage);
        }

        private string ReadMainMenuMessage(int id, string fallback)
            => _resolver.Original.TryReadMessage(RetailMainMenuSource.MessageTable, id, out string value)
               && !string.IsNullOrWhiteSpace(value) ? value.Trim() : fallback;

        private void ClearMessage()
        {
            if (_messageIcon != null) _messageIcon.gameObject.SetActive(false);
            if (_messageItemIcon != null) _messageItemIcon.gameObject.SetActive(false);
            SetText(_messageTitle, string.Empty);
            SetText(_messageTitleRight, string.Empty);
            if (_messageBody != null) ClearMessageBody(_messageBody);
        }

        private void RebuildGoldDetails(PreparedItemPresentation prepared)
        {
            ClearMessageBody(_messageBody);
            SourceUiBitmapText quantityText = RentMessageLine("Gold Quantity",
                new Rect(0f, 0f, _messageBody.rect.width, 18f),
                SourceUiBitmapText.Flare12FontSourceId, SourceWhite);
            SetText(quantityText, prepared.Quantity, SourceWhite);
            SourceUiBitmapText weightText = RentMessageLine("Gold Weight",
                new Rect(0f, 36f, _messageBody.rect.width, 18f),
                SourceUiBitmapText.Flare12FontSourceId, SourceRed);
            SetText(weightText, prepared.Weight, SourceRed);
        }

        private static void ClearMessageBody(RectTransform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
                parent.GetChild(index).gameObject.SetActive(false);
        }

        private void RebuildWrappedText(RectTransform parent, string value, int columns, int maxLines)
        {
            ClearMessageBody(parent);
            var lines = new List<string>();
            foreach (string paragraph in (value ?? string.Empty).Replace("\r", string.Empty).Split('\n'))
            {
                string current = string.Empty;
                foreach (string word in paragraph.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
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
                if (lines.Count >= maxLines) break;
            }
            for (int index = 0; index < lines.Count; index++)
                RentMessageLine($"Line {index + 1}", new Rect(0f, index * 15f, parent.rect.width, 15f),
                    229, SourceWhite).Bind(_resolver, lines[index], SourceWhite, 229);
        }

        private void PrepareMessageLinePool()
        {
            const int lineCount = 6;
            const int glyphCapacity = 48;
            for (int index = 0; index < lineCount; index++)
            {
                SourceUiBitmapText line = AddText($"Prepared Message Line {index + 1}", _messageBody,
                    new Rect(0f, 0f, _messageBody.rect.width, 18f),
                    SourceUiBitmapText.Flare12FontSourceId, SourceWhite);
                line.EnsureGlyphPresenterCapacity(glyphCapacity);
                line.gameObject.SetActive(false);
                _messageLinePool.Add(line);
            }
        }

        private SourceUiBitmapText RentMessageLine(string name, Rect rect, int font, Color color)
        {
            SourceUiBitmapText line = null;
            for (int index = 0; index < _messageLinePool.Count; index++)
                if (!_messageLinePool[index].gameObject.activeSelf)
                {
                    line = _messageLinePool[index];
                    break;
                }
            if (line == null)
            {
                line = AddText(name, _messageBody, rect, font, color);
                line.EnsureGlyphPresenterCapacity(48);
                _messageLinePool.Add(line);
            }
            RectTransform target = (RectTransform)line.transform;
            line.gameObject.name = name;
            target.anchoredPosition = new Vector2(rect.x, -rect.y);
            target.sizeDelta = new Vector2(rect.width, rect.height);
            line.gameObject.SetActive(true);
            line.Bind(_resolver, string.Empty, color, font);
            return line;
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
            RetailInventorySourceButtonState.Bind(source, _resolver, sourceId);
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

        private void SetRightAlignedText(SourceUiBitmapText text, string value, Rect sourceRect, Color tint)
        {
            SetText(text, value, tint);
            RectTransform rect = (RectTransform)text.transform;
            rect.anchoredPosition = new Vector2(sourceRect.x + sourceRect.width - rect.sizeDelta.x, -sourceRect.y);
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
                    hash = hash * 31 + value.ReportedMarkupPercent.GetValueOrDefault();
                    hash = hash * 31 + (value.ReportedMarkupPercent.HasValue ? 1 : 0);
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
            if (_session != null)
            {
                _session.ObjectPlacementChanged -= HandleInventoryChanged;
                _session.ObjectQuantityChanged -= HandleInventoryChanged;
                _session.ObjectStateRemoved -= HandleInventoryChanged;
            }
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
            _merchantPortrait = null;
            _merchantPaperDollToggle = null;
            _merchantInventoryToggle = null;
            _playerPaperDollToggle = null;
            _playerInventoryToggle = null;
            _gambleBox = null;
            _useBox = null;
            _dropBox = null;
            _quantityPanel = null;
            _quantityItemRect = null;
            _quantityItemIcon = null;
            _quantityValue = null;
            _messageTitleRight = null;
            _pendingTransaction = null;
            _selectedQuantity = 0;
            _maximumQuantity = 0;
            _pendingDestinationLocation = -1;
            _merchantInventoryMode = true;
            _playerInventoryMode = true;
            _nextArrangeMode = InventoryArrangeMode.Horizontal;
            _resolver = null;
            _controller = null;
            _session = null;
        }
    }
}
