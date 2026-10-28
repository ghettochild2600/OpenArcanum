using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.World;
using OpenArcanum.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Arcanum.Runtime.UI
{
    public readonly struct RetailEquipmentSlotDefinition
    {
        public WornLocation Location { get; }
        public Rect Rect { get; }
        public int EmptySourceId { get; }

        public RetailEquipmentSlotDefinition(WornLocation location, Rect rect, int emptySourceId)
        {
            Location = location;
            Rect = rect;
            EmptySourceId = emptySourceId;
        }
    }

    /// <summary>Source coordinates from inven_ui.c for the ordinary Inventory mode.</summary>
    public static class RetailInventoryEquipmentLayout
    {
        public const int PaperDollSourceId = 223;
        public const int InventorySourceId = 221;
        public const int PcLensFrameSourceId = 233;
        public const int ArrangeSourceId = 340;
        public const int TotalAttackSourceId = 676;
        public const int TotalDefenseSourceId = 677;
        public const int GridColumns = 10;
        public const int GridRows = 12;
        public const float CellSize = 32f;

        public static readonly Rect Window = new(0f, 41f, 800f, 400f);
        public static readonly Rect PaperDoll = new(0f, 0f, 800f, 400f);
        public static readonly Rect Inventory = new(358f, 0f, 442f, 400f);
        public static readonly Rect InventoryGrid = new(368f, 8f, 320f, 384f);
        public static readonly Rect PcLens = new(11f, 9f, 89f, 89f);
        public static readonly Rect WeightText = new(145f, 29f, 185f, 15f);
        public static readonly Rect EncumbranceText = new(145f, 44f, 185f, 15f);
        public static readonly Rect SpeedText = new(145f, 59f, 185f, 15f);
        public static readonly Rect AttackIcon = new(29f, 318f, 54f, 71f);
        public static readonly Rect DefenseIcon = new(254f, 319f, 83f, 71f);
        public static readonly Rect AttackValue = new(80f, 358f, 23f, 35f);
        public static readonly Rect DefenseValue = new(261f, 358f, 23f, 35f);
        public static readonly Rect ArrangeButton = new(763f, 197f, 32f, 32f);
        public static readonly Rect[] CounterRects =
        {
            new(729f, 70f, 58f, 20f), new(729f, 93f, 58f, 20f), new(729f, 116f, 58f, 20f),
            new(729f, 139f, 58f, 20f), new(729f, 162f, 58f, 20f),
        };

        public static readonly RetailEquipmentSlotDefinition[] EquipmentSlots =
        {
            new(WornLocation.Helmet, new Rect(151f, 107f, 64f, 64f), 241),
            new(WornLocation.Ring1, new Rect(247f, 107f, 32f, 32f), 246),
            new(WornLocation.Ring2, new Rect(279f, 107f, 32f, 32f), 247),
            new(WornLocation.Medallion, new Rect(247f, 139f, 64f, 32f), 245),
            new(WornLocation.Weapon, new Rect(23f, 170f, 96f, 128f), 249),
            new(WornLocation.Shield, new Rect(247f, 171f, 96f, 128f), 248),
            new(WornLocation.Armor, new Rect(119f, 171f, 128f, 160f), 244),
            new(WornLocation.Gauntlet, new Rect(55f, 107f, 64f, 64f), 243),
            new(WornLocation.Boots, new Rect(150f, 331f, 64f, 64f), 242),
        };

        public static Rect ScreenRect(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            return new Rect((width - Window.width) * .5f, 41f + (height - 600f) * .5f,
                Window.width, Window.height);
        }

        public static Rect InventoryItemRect(int location, InventoryFootprint footprint)
        {
            int bounded = Mathf.Clamp(location, 0, GridColumns * GridRows - 1);
            return new Rect(InventoryGrid.x + CellSize * (bounded % GridColumns),
                InventoryGrid.y + CellSize * (bounded / GridColumns),
                CellSize * Mathf.Max(1, footprint.Width), CellSize * Mathf.Max(1, footprint.Height));
        }
    }

    /// <summary>
    /// Retail inventory/equipment presentation. It projects retained state and submits commands to
    /// <see cref="GameUiController"/>; no ownership or equipment state is stored in the view hierarchy.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RetailInventoryEquipmentView : MonoBehaviour
    {
        private const string AttackHelpKey = "inventory-total-attack";
        private const string DefenseHelpKey = "inventory-total-defense";
        private static readonly Color SourceText = Color.white;
        private static readonly Color Selected = new Color32(222, 168, 35, 90);

        private GameUiController _controller;
        private WorldMapSessionCoordinator _session;
        private UiSkinResolver _resolver;
        private GameObject _canvasRoot;
        private RectTransform _window;
        private RectTransform _dynamicLayer;
        private SourceUiImage _paperDoll;
        private SourceUiImage _inventory;
        private RetailPcLensView _pcLens;
        private SourceUiBitmapText _weight;
        private SourceUiBitmapText _encumbrance;
        private SourceUiBitmapText _speed;
        private SourceUiBitmapText _attack;
        private SourceUiBitmapText _defense;
        private readonly SourceUiBitmapText[] _counters = new SourceUiBitmapText[5];
        private readonly Dictionary<WornLocation, Button> _equipmentButtons = new();
        private readonly List<Image> _selectionHighlights = new();
        private IReadOnlyList<GameUiItemView> _projectedItems = Array.Empty<GameUiItemView>();
        private int _projectionSignature = int.MinValue;
        private InventoryArrangeMode _nextArrangeMode = InventoryArrangeMode.Horizontal;
        private bool _dirty = true;

        public bool IsAvailable => _canvasRoot != null && _resolver != null;
        public bool IsVisible => IsAvailable && _canvasRoot.activeSelf;
        public RectTransform WindowTransform => _window;
        public UiResolvedAsset PaperDollAsset => _paperDoll?.CurrentAsset;
        public UiResolvedAsset InventoryAsset => _inventory?.CurrentAsset;
        public IReadOnlyList<GameUiItemView> ProjectedItems => _projectedItems;
        public int DynamicItemCount { get; private set; }

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
            bool visible = _controller.HasPlayer && _controller.Screen == GameUiScreen.Inventory;
            if (_canvasRoot.activeSelf != visible) _canvasRoot.SetActive(visible);
            if (!visible) return;

            IReadOnlyList<GameUiItemView> items = _controller.ProjectInventory();
            int signature = ProjectionSignature(items);
            if (_dirty || signature != _projectionSignature)
            {
                _projectedItems = items;
                _projectionSignature = signature;
                _dirty = false;
                RebuildItems();
            }
            RefreshSelection();
            RefreshSummary();
        }

        public bool ContainsScreenPoint(Vector2 screenBottomLeft)
        {
            if (!IsVisible) return false;
            Vector2 topLeft = new(screenBottomLeft.x, Screen.height - screenBottomLeft.y);
            return RetailInventoryEquipmentLayout.ScreenRect(Screen.width, Screen.height).Contains(topLeft);
        }

        private void BuildPresentation()
        {
            _canvasRoot = new GameObject("Source-Faithful Inventory Equipment",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
                typeof(SourceUiRuntime));
            _canvasRoot.transform.SetParent(transform, false);
            _canvasRoot.GetComponent<SourceUiRuntime>().BindResolver(_resolver);
            Canvas canvas = _canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 450;
            CanvasScaler scaler = _canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 1f;

            _window = AddRect("Retail Inventory Window", (RectTransform)_canvasRoot.transform,
                new Rect(0f, 0f, 800f, 400f));
            _window.anchorMin = _window.anchorMax = new Vector2(.5f, .5f);
            _window.pivot = new Vector2(.5f, .5f);
            _window.anchoredPosition = new Vector2(0f, 59f);
            _paperDoll = AddSourceImage("Paper Doll Panel", _window,
                RetailInventoryEquipmentLayout.PaperDollSourceId, RetailInventoryEquipmentLayout.PaperDoll);
            _inventory = AddSourceImage("Inventory Grid Panel", _window,
                RetailInventoryEquipmentLayout.InventorySourceId, RetailInventoryEquipmentLayout.Inventory);

            RectTransform lens = AddRect("Live PC Lens", _window, RetailInventoryEquipmentLayout.PcLens);
            _pcLens = lens.gameObject.AddComponent<RetailPcLensView>();
            _pcLens.Bind(_session);
            AddSourceImage("PC Lens Frame", _window, RetailInventoryEquipmentLayout.PcLensFrameSourceId,
                RetailInventoryEquipmentLayout.PcLens);

            RectTransform blocker = AddRect("Inventory Input", _window, new Rect(0f, 0f, 800f, 400f));
            Image blockerImage = blocker.gameObject.AddComponent<Image>();
            blockerImage.color = Color.clear;
            blockerImage.raycastTarget = true;

            AddSourceButton("Arrange Items", _window, RetailInventoryEquipmentLayout.ArrangeSourceId,
                RetailInventoryEquipmentLayout.ArrangeButton, () =>
                {
                    _controller.ArrangeInventory(_nextArrangeMode);
                    _nextArrangeMode = _nextArrangeMode == InventoryArrangeMode.Horizontal
                        ? InventoryArrangeMode.Vertical
                        : InventoryArrangeMode.Horizontal;
                    _dirty = true;
                    Synchronize();
                });

            SourceUiImage attackIcon = AddSourceImage("Total Attack", _window,
                RetailInventoryEquipmentLayout.TotalAttackSourceId, RetailInventoryEquipmentLayout.AttackIcon);
            SourceUiImage defenseIcon = AddSourceImage("Total Defense", _window,
                RetailInventoryEquipmentLayout.TotalDefenseSourceId, RetailInventoryEquipmentLayout.DefenseIcon);
            BindCombatSummaryHover(attackIcon.gameObject, true);
            BindCombatSummaryHover(defenseIcon.gameObject, false);

            foreach (RetailEquipmentSlotDefinition slot in RetailInventoryEquipmentLayout.EquipmentSlots)
            {
                AddSourceImage($"Empty {slot.Location}", _window, slot.EmptySourceId, slot.Rect);
                Button button = AddTransparentButton($"Equipment {slot.Location}", _window, slot.Rect,
                    () => OnEquipmentSlot(slot.Location));
                _equipmentButtons.Add(slot.Location, button);
            }

            _weight = AddText("Weight", _window, RetailInventoryEquipmentLayout.WeightText,
                SourceUiBitmapText.Flare12FontSourceId);
            _encumbrance = AddText("Encumbrance", _window, RetailInventoryEquipmentLayout.EncumbranceText,
                SourceUiBitmapText.Flare12FontSourceId);
            _speed = AddText("Speed", _window, RetailInventoryEquipmentLayout.SpeedText,
                SourceUiBitmapText.Flare12FontSourceId);
            _attack = AddText("Attack Value", _window, RetailInventoryEquipmentLayout.AttackValue,
                SourceUiBitmapText.InventoryStatFontSourceId);
            _defense = AddText("Defense Value", _window, RetailInventoryEquipmentLayout.DefenseValue,
                SourceUiBitmapText.InventoryStatFontSourceId);
            BindCombatValueHover("Attack Value Help", RetailInventoryEquipmentLayout.AttackValue, true);
            BindCombatValueHover("Defense Value Help", RetailInventoryEquipmentLayout.DefenseValue, false);
            for (int index = 0; index < _counters.Length; index++)
                _counters[index] = AddText($"Counter {index}", _window,
                    RetailInventoryEquipmentLayout.CounterRects[index]);
            _dynamicLayer = AddRect("Inventory Items", _window, new Rect(0f, 0f, 800f, 400f));
        }

        private void RebuildItems()
        {
            ClearDynamicLayer();
            DynamicItemCount = 0;
            foreach (GameUiItemView item in _projectedItems)
            {
                if (item.IsEquipped && item.WornLocation.HasValue)
                {
                    RetailEquipmentSlotDefinition slot = RetailInventoryEquipmentLayout.EquipmentSlots
                        .First(value => value.Location == item.WornLocation.Value);
                    AddItemButton(item, slot.Rect, equipped: true);
                }
                else
                    AddItemButton(item, RetailInventoryEquipmentLayout.InventoryItemRect(
                        item.InventoryLocation, item.InventoryFootprint), equipped: false);
                DynamicItemCount++;
            }
        }

        private void AddItemButton(GameUiItemView item, Rect bounds, bool equipped)
        {
            RectTransform root = AddRect($"Item {item.Identity.Key}", _dynamicLayer, bounds);
            Image selection = root.gameObject.AddComponent<Image>();
            selection.color = Color.clear;
            selection.raycastTarget = true;
            _selectionHighlights.Add(selection);
            Button button = root.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => OnItem(item, equipped));
            SourceHoverHintTarget hint = root.gameObject.AddComponent<SourceHoverHintTarget>();
            hint.Bind(() => _controller.InspectItem(item.Identity), null);
            RetailInventoryItemDragTarget drag =
                RetailInventoryDragPresenter.AddDragTarget<RetailInventoryItemDragTarget>(root.gameObject);
            drag.Bind(_window, item.Identity, equipped, OnInventoryDrop);

            if (item.InventoryArtId.HasValue
                && _resolver.Original.TryResolveInventoryItem(item.InventoryArtId.Value,
                    out RetailItemArtAsset asset))
            {
                Vector2 size = asset.LogicalSize;
                float scale = Mathf.Min(1f, Mathf.Min(bounds.width / Mathf.Max(1f, size.x),
                    bounds.height / Mathf.Max(1f, size.y)));
                size *= scale;
                Rect iconRect = new((bounds.width - size.x) * .5f, (bounds.height - size.y) * .5f,
                    size.x, size.y);
                RectTransform icon = AddRect("Source Item Art", root, iconRect);
                Image image = icon.gameObject.AddComponent<Image>();
                image.sprite = asset.Sprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }
            else
            {
                SourceUiBitmapText fallback = AddText("Item Label", root,
                    new Rect(4f, 4f, Mathf.Max(0f, bounds.width - 8f), 14f));
                SetText(fallback, string.IsNullOrEmpty(item.Name) ? "?" : item.Name[..1]);
            }

            if (item.Quantity > 1)
            {
                SourceUiBitmapText quantity = AddText("Quantity", root,
                    new Rect(Mathf.Max(0f, bounds.width - 18f), Mathf.Max(0f, bounds.height - 14f), 18f, 12f));
                SetText(quantity, item.Quantity.ToString());
            }
        }

        private void OnItem(GameUiItemView item, bool equipped)
        {
            if (_controller.SelectedItem == item.Identity)
            {
                if (equipped && item.WornLocation.HasValue) _controller.Unequip(item.WornLocation.Value);
                else _controller.Equip(item.Identity);
                _dirty = true;
            }
            else
                _controller.SelectItem(item.Identity);
            Synchronize();
        }

        private void OnEquipmentSlot(WornLocation location)
        {
            GameUiItemView equipped = _projectedItems.FirstOrDefault(value => value.WornLocation == location);
            if (equipped != null)
            {
                if (_controller.SelectedItem == equipped.Identity) _controller.Unequip(location);
                else _controller.SelectItem(equipped.Identity);
            }
            else if (!_controller.SelectedItem.IsNull)
                _controller.Equip(_controller.SelectedItem);
            _dirty = true;
            Synchronize();
        }

        private bool OnInventoryDrop(ArcanumObjectId item, bool equipped, Vector2 screenPosition,
            Vector2 itemTopLeftScreenPosition, Camera eventCamera)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _window, screenPosition, eventCamera, out Vector2 local)) return false;
            float sourceX = local.x + _window.rect.width * _window.pivot.x;
            float sourceY = _window.rect.height * (1f - _window.pivot.y) - local.y;
            Vector2 sourcePoint = new(sourceX, sourceY);
            foreach (RetailEquipmentSlotDefinition slot in RetailInventoryEquipmentLayout.EquipmentSlots)
                if (slot.Rect.Contains(sourcePoint))
                {
                    bool equippedAtSlot = _controller.Equip(item, slot.Location);
                    if (equippedAtSlot) _dirty = true;
                    Synchronize();
                    return equippedAtSlot;
                }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _window, itemTopLeftScreenPosition, eventCamera, out Vector2 itemTopLeftLocal)) return false;
            float itemSourceX = itemTopLeftLocal.x + _window.rect.width * _window.pivot.x;
            float itemSourceY = _window.rect.height * (1f - _window.pivot.y) - itemTopLeftLocal.y;
            float x = itemSourceX - RetailInventoryEquipmentLayout.InventoryGrid.x;
            float y = itemSourceY - RetailInventoryEquipmentLayout.InventoryGrid.y;
            if (x < 0f || y < 0f || x >= RetailInventoryEquipmentLayout.InventoryGrid.width
                || y >= RetailInventoryEquipmentLayout.InventoryGrid.height) return false;
            int column = Mathf.FloorToInt(x / RetailInventoryEquipmentLayout.CellSize);
            int row = Mathf.FloorToInt(y / RetailInventoryEquipmentLayout.CellSize);
            bool moved = _controller.MoveInventoryItem(item,
                row * RetailInventoryEquipmentLayout.GridColumns + column);
            if (moved)
                _dirty = true;
            Synchronize();
            return moved;
        }

        private void RefreshSelection()
        {
            for (int index = 0; index < _selectionHighlights.Count && index < _projectedItems.Count; index++)
                _selectionHighlights[index].color = _projectedItems[index].Identity == _controller.SelectedItem
                    ? Selected : Color.clear;
        }

        private void RefreshSummary()
        {
            GameUiInventorySummaryView summary = _controller.ProjectInventorySummary();
            if (summary == null) return;
            SetText(_weight, $"{SourceMessage(9, "Weight")}: {summary.Weight} {SourceMessage(6, "stones")}");
            SetText(_encumbrance, $"{SourceMessage(10, "Encumbrance")}: "
                                  + $"{EncumbranceName(summary.EncumbranceLevel)} ({summary.EncumbranceThreshold})");
            SetText(_speed, $"{SourceMessage(11, "Speed")}: {summary.Speed}");
            SetRightAlignedText(_attack, summary.TotalAttack.ToString(), RetailInventoryEquipmentLayout.AttackValue);
            SetText(_defense, summary.TotalDefense.ToString());
            SetText(_counters[0], summary.Gold.ToString());
            for (int index = 0; index < 4; index++)
                SetText(_counters[index + 1], summary.Ammunition[index].ToString());
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

        private void BindCombatSummaryHover(GameObject target, bool attack)
        {
            Image image = target.GetComponent<Image>();
            image.raycastTarget = true;
            target.AddComponent<SourceHoverHintTarget>().Bind(
                () => ShowCombatSummary(attack),
                () => _controller.ClearContextualHelp(attack ? AttackHelpKey : DefenseHelpKey));
        }

        private void BindCombatValueHover(string name, Rect rect, bool attack)
        {
            RectTransform target = AddRect(name, _window, rect);
            Image image = target.gameObject.AddComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
            target.gameObject.AddComponent<SourceHoverHintTarget>().Bind(
                () => ShowCombatSummary(attack),
                () => _controller.ClearContextualHelp(attack ? AttackHelpKey : DefenseHelpKey));
        }

        private void ShowCombatSummary(bool attack)
        {
            GameUiInventorySummaryView summary = _controller.ProjectInventorySummary();
            if (summary == null) return;
            if (attack)
            {
                string body = $"Base To Hit: {summary.BaseToHit}%  Damage: "
                              + $"{DamageRange(summary.NormalDamageMinimum, summary.NormalDamageMaximum)}\n"
                              + $"Fatigue: {DamageRange(summary.FatigueDamageMinimum, summary.FatigueDamageMaximum)}  "
                              + $"Electrical Damage: {DamageRange(summary.ElectricalDamageMinimum, summary.ElectricalDamageMaximum)}\n"
                              + $"Fire Damage: {DamageRange(summary.FireDamageMinimum, summary.FireDamageMaximum)}  "
                              + $"Poison: {DamageRange(summary.PoisonDamageMinimum, summary.PoisonDamageMaximum)}";
                _controller.ShowContextualHelp(AttackHelpKey, body,
                    $"Total Attack: {summary.TotalAttack}", 675);
            }
            else
            {
                string body = $"Total AC: {summary.ArmorClass}  Damage Resistance: {ResistanceValue(summary.DamageResistance)}\n"
                              + $"Magic Resistance: {ResistanceValue(summary.MagicResistance)}  "
                              + $"Electrical Resistance: {ResistanceValue(summary.ElectricalResistance)}\n"
                              + $"Fire Resistance: {ResistanceValue(summary.FireResistance)}  "
                              + $"Poison Resistance: {ResistanceValue(summary.PoisonResistance)}";
                _controller.ShowContextualHelp(DefenseHelpKey, body,
                    $"Total Defense: {summary.TotalDefense}", 674);
            }
        }

        private static string DamageRange(int minimum, int maximum)
            => maximum == 0 ? "0" : $"{minimum}-{maximum}";

        private static string ResistanceValue(int value)
            => value == 0 ? "0" : $"{value}%";

        private void Invalidate()
        {
            _dirty = true;
            if (this != null && IsVisible) Synchronize();
        }

        private static int ProjectionSignature(IReadOnlyList<GameUiItemView> items)
        {
            unchecked
            {
                int hash = 17;
                foreach (GameUiItemView item in items)
                {
                    hash = hash * 31 + item.Identity.GetHashCode();
                    hash = hash * 31 + item.Quantity;
                    hash = hash * 31 + item.InventoryLocation;
                    hash = hash * 31 + (item.WornLocation.HasValue ? (int)item.WornLocation.Value : 0);
                    hash = hash * 31 + item.InventoryFootprint.GetHashCode();
                    hash = hash * 31 + item.InventoryArtId.GetHashCode();
                }
                return hash;
            }
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

        private Button AddTransparentButton(string name, RectTransform parent, Rect rect, Action clicked)
        {
            RectTransform target = AddRect(name, parent, rect);
            Image image = target.gameObject.AddComponent<Image>();
            image.color = Color.clear;
            Button button = target.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => clicked());
            return button;
        }

        private SourceUiBitmapText AddText(string name, RectTransform parent, Rect rect,
            int fontSourceId = RetailGameplayHudLayout.TextFontSourceId)
        {
            RectTransform target = AddRect(name, parent, rect);
            SourceUiBitmapText text = target.gameObject.AddComponent<SourceUiBitmapText>();
            text.Bind(_resolver, string.Empty, SourceText, fontSourceId);
            return text;
        }

        private void SetText(SourceUiBitmapText text, string value)
        {
            if (text.Text == value) return;
            text.Bind(_resolver, value, SourceText, text.FontSourceId);
        }

        private void SetRightAlignedText(SourceUiBitmapText text, string value, Rect sourceRect)
        {
            SetText(text, value);
            RectTransform rect = (RectTransform)text.transform;
            rect.anchoredPosition = new Vector2(sourceRect.x + sourceRect.width - rect.sizeDelta.x, -sourceRect.y);
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

        private void ClearDynamicLayer()
        {
            _selectionHighlights.Clear();
            for (int index = _dynamicLayer.childCount - 1; index >= 0; index--)
            {
                GameObject child = _dynamicLayer.GetChild(index).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Object.Destroy(child); else Object.DestroyImmediate(child);
            }
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
            _dynamicLayer = null;
            _paperDoll = null;
            _inventory = null;
            _pcLens = null;
            _weight = null;
            _encumbrance = null;
            _speed = null;
            _attack = null;
            _defense = null;
            Array.Clear(_counters, 0, _counters.Length);
            _equipmentButtons.Clear();
            _selectionHighlights.Clear();
            _projectedItems = Array.Empty<GameUiItemView>();
            _resolver = null;
            _controller = null;
            _session = null;
            DynamicItemCount = 0;
            _projectionSignature = int.MinValue;
            _nextArrangeMode = InventoryArrangeMode.Horizontal;
            _dirty = true;
        }
    }

    /// <summary>Live projection of the currently rendered PC sprite into the source 89x89 lens.</summary>
    [DisallowMultipleComponent]
    public sealed class RetailPcLensView : MonoBehaviour
    {
        private WorldMapSessionCoordinator _session;
        private Image _image;
        private RectTransform _imageRect;

        public Sprite CurrentSprite => _image != null ? _image.sprite : null;

        public void Bind(WorldMapSessionCoordinator session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            RectTransform root = (RectTransform)transform;
            Image maskImage = gameObject.AddComponent<Image>();
            maskImage.color = Color.black;
            maskImage.raycastTarget = false;
            gameObject.AddComponent<RectMask2D>();
            var child = new GameObject("Live PC Sprite", typeof(RectTransform), typeof(Image));
            _imageRect = child.GetComponent<RectTransform>();
            _imageRect.SetParent(root, false);
            _imageRect.anchorMin = _imageRect.anchorMax = new Vector2(.5f, .5f);
            _imageRect.pivot = new Vector2(.5f, .5f);
            _imageRect.anchoredPosition = new Vector2(0f, -8f);
            _imageRect.sizeDelta = new Vector2(80f, 104f);
            _image = child.GetComponent<Image>();
            _image.preserveAspect = true;
            _image.raycastTarget = false;
            Refresh();
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (_image == null || _session?.PlayerState == null
                || !_session.TryGetLoadedObject(_session.PlayerState.Identity, out WorldObject player)
                || player.View == null)
            {
                if (_image != null) _image.enabled = false;
                return;
            }
            _image.enabled = true;
            _image.sprite = player.View.sprite;
            _image.color = player.View.color;
            _imageRect.localScale = new Vector3(player.View.flipX ? -1f : 1f, 1f, 1f);
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class RetailInventoryItemDragTarget : MonoBehaviour, IBeginDragHandler, IDragHandler,
        IEndDragHandler, ICancelHandler
    {
        private RectTransform _window;
        private RectTransform _itemTransform;
        private ArcanumObjectId _item;
        private bool _equipped;
        private Func<ArcanumObjectId, bool, Vector2, Vector2, Camera, bool> _dropped;
        private CanvasGroup _canvasGroup;
        private Vector2 _startPosition;
        private Vector2 _pointerOffset;
        private bool _dragging;

        public void Bind(RectTransform window, ArcanumObjectId item, bool equipped,
            Func<ArcanumObjectId, bool, Vector2, Vector2, Camera, bool> dropped)
        {
            _window = window;
            _itemTransform = (RectTransform)transform;
            _item = item;
            _equipped = equipped;
            _dropped = dropped;
            _canvasGroup = RetailInventoryDragPresenter.EnsureCanvasGroup(gameObject);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_itemTransform == null || eventData == null) return;
            _canvasGroup = RetailInventoryDragPresenter.EnsureCanvasGroup(gameObject);
            if (_canvasGroup == null)
            {
                _dragging = false;
                return;
            }
            _startPosition = _itemTransform.anchoredPosition;
            _dragging = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_itemTransform.parent, eventData.position, eventData.pressEventCamera,
                out Vector2 pointer);
            if (!_dragging) return;
            _pointerOffset = _startPosition - pointer;
            _canvasGroup.alpha = .72f;
            _canvasGroup.blocksRaycasts = false;
            _itemTransform.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || eventData == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)_itemTransform.parent, eventData.position, eventData.pressEventCamera,
                    out Vector2 pointer))
                _itemTransform.anchoredPosition = pointer + _pointerOffset;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            Vector2 itemTopLeft = eventData == null
                ? Vector2.zero
                : RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, _itemTransform.position);
            RestorePresentation();
            if (eventData != null)
                _dropped?.Invoke(_item, _equipped, eventData.position, itemTopLeft,
                    eventData.pressEventCamera);
        }

        public void OnCancel(BaseEventData eventData) => RestorePresentation();

        private void RestorePresentation()
        {
            if (!_dragging) return;
            _dragging = false;
            if (_itemTransform != null) _itemTransform.anchoredPosition = _startPosition;
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = true;
            }
        }
    }
}
