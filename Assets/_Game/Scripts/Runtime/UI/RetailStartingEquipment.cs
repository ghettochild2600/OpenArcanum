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
        public static readonly Rect PlayerAttackValue = new(422f, 358f, 23f, 35f);
        public static readonly Rect PlayerDefenseValue = new(606f, 358f, 23f, 35f);
        public static readonly Rect MerchantAttackValue = new(105f, 354f, 22f, 16f);
        public static readonly Rect MerchantDefenseValue = new(227f, 354f, 22f, 16f);
        public static readonly Rect PlayerAttackIcon = new(374f, 318f, 48f, 75f);
        public static readonly Rect PlayerDefenseIcon = new(599f, 319f, 48f, 74f);
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
        public static readonly int[] RequiredSourceIds =
        {
            BarterBackgroundSourceId, PlayerInventorySourceId, MerchantPaperDollSourceId,
            PlayerPaperDollSourceId, PaperDollToggleSourceId, InventoryToggleSourceId, ArrangeSourceId,
            DropBoxSourceId, UseBoxSourceId, GambleBoxSourceId, ScrollUpSourceId, ScrollDownSourceId,
            ScrollThumbTopSourceId, ScrollThumbMiddleSourceId, ScrollThumbBottomSourceId,
            FeedbackIconSourceId, 375, 321, 323,
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
        private Image _merchantPortrait;
        private SourceUiButton _merchantPaperDollToggle;
        private SourceUiButton _merchantInventoryToggle;
        private SourceUiButton _playerPaperDollToggle;
        private SourceUiButton _playerInventoryToggle;
        private SourceUiBitmapText _merchantName;
        private SourceUiBitmapText _merchantQuote;
        private SourceUiBitmapText _messageTitle;
        private SourceUiBitmapText _playerAttack;
        private SourceUiBitmapText _playerDefense;
        private SourceUiBitmapText _merchantAttack;
        private SourceUiBitmapText _merchantDefense;
        private SourceUiImage _playerAttackIcon;
        private SourceUiImage _playerDefenseIcon;
        private Scrollbar _merchantScrollbar;
        private RectTransform _messageBody;
        private SourceUiImage _messageIcon;
        private Image _messageItemIcon;
        private readonly SourceUiBitmapText[] _counters = new SourceUiBitmapText[5];
        private int _signature = int.MinValue;
        private bool _dirty = true;
        private bool _merchantInventoryMode = true;
        private bool _playerInventoryMode = true;
        private float _merchantScrollRow;
        private int _merchantMaximumScrollRow;
        private bool _centeredShoppingWorld;

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
            if (!visible)
            {
                _centeredShoppingWorld = false;
                return;
            }
            CenterShoppingWorldOnPlayer();

            IReadOnlyList<GameUiMerchantItemView> wares = _controller.ProjectMerchantWares();
            // inven_ui.c temporarily clears OIF_NO_DISPLAY for Gold while drawing barter inventories. The same
            // authoritative stack therefore drives both the physical inventory icon and the dedicated counter.
            IReadOnlyList<GameUiMerchantItemView> player = _controller.ProjectPlayerSaleItems();
            int signature = ProjectionSignature(wares, player);
            if (_dirty || signature != _signature)
            {
                _signature = signature;
                _dirty = false;
                RebuildItems(wares, player);
            }

            string name = _session.TryGetObjectState(_controller.MerchantTarget, out PersistentObjectState merchant)
                ? _session.ResolveDisplayName(merchant) : "Human Shopkeeper";
            SetMerchantPortrait(merchant);
            SetCenteredText(_merchantName, string.IsNullOrWhiteSpace(name) ? "Human Shopkeeper" : name.ToUpperInvariant(),
                RetailStartingEquipmentLayout.MerchantName);
            GameUiInventorySummaryView summary = _controller.ProjectInventorySummary();
            if (summary != null)
            {
                SetCenteredText(_counters[0], summary.Gold.ToString(), RetailStartingEquipmentLayout.CounterRects[0]);
                for (int index = 0; index < 4; index++)
                    SetCenteredText(_counters[index + 1], summary.Ammunition[index].ToString(),
                        RetailStartingEquipmentLayout.CounterRects[index + 1]);
                SetText(_playerAttack, summary.TotalAttack.ToString(), SourceWhite);
                SetText(_playerDefense, summary.TotalDefense.ToString(), SourceWhite);
            }
            GameUiInventorySummaryView merchantSummary = _controller.ProjectInventorySummary(_controller.MerchantTarget);
            if (merchantSummary != null)
            {
                SetText(_merchantAttack, merchantSummary.TotalAttack.ToString(), SourceWhite);
                SetText(_merchantDefense, merchantSummary.TotalDefense.ToString(), SourceWhite);
            }
        }

        public void HandleEscape() => _controller.ReturnToCharacterEditor();

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
                RetailStartingEquipmentLayout.PlayerAttackValue, 486, SourceWhite);
            _playerDefense = AddText("Player Defense", _window,
                RetailStartingEquipmentLayout.PlayerDefenseValue, 486, SourceWhite);
            _merchantAttack = AddText("Merchant Attack", _window,
                RetailStartingEquipmentLayout.MerchantAttackValue, 486, SourceWhite);
            _merchantDefense = AddText("Merchant Defense", _window,
                RetailStartingEquipmentLayout.MerchantDefenseValue, 486, SourceWhite);

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
            AddSourceImage("Gamble Drop Box", _window, RetailStartingEquipmentLayout.GambleBoxSourceId,
                RetailStartingEquipmentLayout.GambleBox);
            AddSourceImage("Use Drop Box", _window, RetailStartingEquipmentLayout.UseBoxSourceId,
                RetailStartingEquipmentLayout.UseBox);
            AddSourceImage("Drop Drop Box", _window, RetailStartingEquipmentLayout.DropBoxSourceId,
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
            _messageBody = AddRect("Shopkeeper Message Body", _bottom,
                RetailStartingEquipmentLayout.MessageBody);
            ClearMessage();
            _merchantDropTarget = AddRect("Merchant Inventory Drop Target", _window,
                RetailStartingEquipmentLayout.MerchantGrid);
            Image merchantScrollInput = _merchantDropTarget.gameObject.AddComponent<Image>();
            merchantScrollInput.color = Color.clear;
            merchantScrollInput.raycastTarget = true;
            _merchantDropTarget.gameObject.AddComponent<RetailStartingEquipmentScrollTarget>()
                .Bind(ScrollMerchant);
            _playerDropTarget = AddRect("Player Inventory Drop Target", _window,
                RetailStartingEquipmentLayout.PlayerGrid);
            _dynamic = AddRect("Shopkeeper Items", _window, RetailStartingEquipmentLayout.Window);
            _merchantScrollbar = AddScrollbar(_window, RetailStartingEquipmentLayout.MerchantScrollbar);

            var cursor = new GameObject("Shopkeeper Cursor",
                typeof(RectTransform), typeof(Image), typeof(SourceUiImage), typeof(RetailMainMenuCursorView));
            cursor.transform.SetParent(_canvasRoot.transform, false);
            cursor.GetComponent<RetailMainMenuCursorView>().Bind((RectTransform)_canvasRoot.transform, _resolver);
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
            if (!_merchantInventoryMode)
                foreach (GameUiItemView item in _controller.ProjectMerchantEquipment())
                    AddPaperDollItem(item, RetailStartingEquipmentLayout.MerchantPaperDollSlots);
            if (!_playerInventoryMode)
                foreach (GameUiItemView item in _controller.ProjectInventory().Where(value => value.IsEquipped))
                    AddPaperDollItem(item, RetailStartingEquipmentLayout.PlayerPaperDollSlots);
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
            if (_merchantAttack != null) _merchantAttack.gameObject.SetActive(!_merchantInventoryMode);
            if (_merchantDefense != null) _merchantDefense.gameObject.SetActive(!_merchantInventoryMode);
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
            IReadOnlyDictionary<WornLocation, Rect> slots)
        {
            if (!item.WornLocation.HasValue || !slots.TryGetValue(item.WornLocation.Value, out Rect bounds)) return;
            RectTransform root = AddRect($"Equipped {item.Identity.Key}", _dynamic, bounds);
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

        private void AddTradeItem(RectTransform layer, GameUiMerchantItemView view, Rect bounds)
        {
            RectTransform root = AddRect($"{view.Direction} {view.Item.Identity.Key}", layer, bounds);
            Image hit = root.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            bool physicalMoney = view.Item.Type == Arcanum.Formats.Objects.ObjectType.Gold;
            if (!physicalMoney)
            {
                Button button = root.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                // Source barter still accepts the gesture when a transaction will fail so that the merchant can
                // present the authoritative reason (most notably insufficient Gold).
                button.interactable = true;
                button.onClick.AddListener(() => Execute(view));
                RetailStartingEquipmentDragTarget drag =
                    root.gameObject.AddComponent<RetailStartingEquipmentDragTarget>();
                drag.Bind(view.Direction == EconomyTransactionDirection.BuyFromMerchant
                    ? _playerDropTarget : _merchantDropTarget, () => Execute(view));
            }
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
            SetText(_merchantQuote, MerchantLine(reverse, reversePrice, view.Item.Identity,
                reverseView?.ReportedMarkupPercent));
        }

        private void ShowItem(GameUiMerchantItemView view)
        {
            SetText(_merchantQuote, MerchantLine(view.Direction, view.UnitPrice, view.Item.Identity,
                view.ReportedMarkupPercent));
            GameUiItemInspectionView inspection = _controller.ProjectItemInspection(view.Item.Identity);
            SetText(_messageTitle, inspection?.Name ?? view.Item.Name ?? string.Empty, SourceWhite);
            string details = view.Item.Type == Arcanum.Formats.Objects.ObjectType.Gold
                ? $"Quantity: {view.Item.Quantity}\nWeight: {view.Item.Weight} stone"
                : inspection?.Summary ?? $"Weight: {view.Item.Weight} stone";
            RebuildWrappedText(_messageBody, details, 42, 4);
            RetailItemArtAsset asset = null;
            bool itemArt = inspection?.InventoryArtId is uint artId
                           && _resolver.Original.TryResolveInventoryItem(artId, out asset);
            _messageIcon.gameObject.SetActive(!itemArt);
            if (_messageItemIcon != null)
            {
                _messageItemIcon.gameObject.SetActive(itemArt);
                if (itemArt) _messageItemIcon.sprite = asset.Sprite;
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
            int first = direction == EconomyTransactionDirection.BuyFromMerchant ? 800 : 1100;
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
            if (_messageItemIcon != null) _messageItemIcon.gameObject.SetActive(false);
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
                AddText($"Line {index + 1}", parent, new Rect(0f, index * 15f, parent.rect.width, 15f),
                    229, SourceWhite).Bind(_resolver, lines[index], SourceWhite, 229);
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
            _merchantInventoryMode = true;
            _playerInventoryMode = true;
            _resolver = null;
        }
    }
}
