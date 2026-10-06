using System;
using System.Collections.Generic;
using Arcanum.Runtime.World;
using OpenArcanum.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Arcanum.Runtime.UI
{
    /// <summary>Source-backed UI-C geometry. Every rectangle is expressed in retail top-left 800x600 pixels.</summary>
    public static class RetailGameplayHudLayout
    {
        public const int BackgroundSourceId = 3;
        public const int MessageWindowSourceId = 354;
        public const int HealthLiquidSourceId = 18;
        public const int FatigueLiquidSourceId = 19;
        public const int EmptyVialSourceId = 20;
        public const int CounterFontSourceId = 27;

        public static readonly Rect TopInterface = new Rect(0f, 0f, 800f, 41f);
        public static readonly Rect WorldViewport = new Rect(0f, 41f, 800f, 400f);
        public static readonly Rect BottomInterface = new Rect(0f, 441f, 800f, 159f);
        public static readonly Rect HealthBar = new Rect(14f, 472f, 28f, 88f);
        public static readonly Rect FatigueBar = new Rect(754f, 473f, 28f, 88f);
        public static readonly Rect HealthCounter = new Rect(15f, 578f, 29f, 12f);
        public static readonly Rect FatigueCounter = new Rect(755f, 578f, 27f, 12f);
        public static readonly Rect MessageWindow = new Rect(196f, 492f, 410f, 107f);
        public static readonly Rect MessageText = new Rect(211f, 503f, 383f, 82f);
        public static readonly Rect AmmoButton = new Rect(61f, 509f, 37f, 24f);

        public static readonly int[] QuickSlotSourceIds =
            { 173, 174, 175, 176, 177, 178, 179, 180, 181, 172 };
        public static readonly float[] QuickSlotX =
            { 198f, 237f, 276f, 315f, 354f, 418f, 456f, 495f, 534f, 573f };

        /// <summary>
        /// The retail blitter keeps an eight-pixel liquid overlap under the vial neck. Zero remains genuinely empty;
        /// every other value uses the same bounded <c>filled + 8</c> crop as intgame_draw_bar_rect.
        /// </summary>
        public static int LiquidVisiblePixels(int current, int maximum)
        {
            if (maximum <= 0 || current <= 0) return 0;
            int filled = Mathf.Clamp(Mathf.FloorToInt(88f * current / maximum), 0, 88);
            return Mathf.Min(88, filled + 8);
        }

        public static float LiquidFill01(int current, int maximum)
            => LiquidVisiblePixels(current, maximum) / 88f;

        public static Rect GameplayCameraViewport(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            // Source ID 3 contains real alpha cut-outs inside both interface bands. The world camera therefore
            // remains full-screen behind the composition; opaque source pixels conceal it while alpha apertures
            // reveal it. Input is still bounded by the audited 41/400/159 interaction partition.
            return new Rect(0f, 0f, 1f, 1f);
        }

        public static bool ContainsInterfacePoint(Vector2 screenBottomLeft, int width, int height)
        {
            UiLogicalMapping mapping = UiLogicalMapping.ForResolution(width, height);
            Vector2 topLeft = new Vector2(screenBottomLeft.x, height - screenBottomLeft.y);
            if (!mapping.ReferenceScreenRect.Contains(topLeft)) return false;
            Vector2 logical = mapping.ScreenToLogical(topLeft);
            return TopInterface.Contains(logical) || BottomInterface.Contains(logical);
        }
    }

    public sealed class RetailQuickSlotPresentation
    {
        public int Index { get; internal set; }
        public QuickSlotKind Kind { get; internal set; }
        public int SourceId { get; internal set; }
        public int Quantity { get; internal set; }
        public bool Active { get; internal set; }
    }

    [DisallowMultipleComponent]
    public sealed class RetailGameplayHudView : MonoBehaviour
    {
        private static readonly Color SourceText = new Color32(231, 217, 174, 255);
        private static readonly Color SelectedText = new Color32(240, 190, 45, 255);

        private GameUiController _controller;
        private WorldMapSessionCoordinator _session;
        private Action<GameUiScreen> _openScreen;
        private UiSkinResolver _resolver;
        private bool _ownsResolver;
        private GameObject _canvasRoot;
        private SourceUiPresentationRoot _presentation;
        private SourceUiImage _background;
        private Image _healthLiquid;
        private Image _fatigueLiquid;
        private SourceUiBitmapText _healthText;
        private SourceUiBitmapText _fatigueText;
        private SourceUiBitmapText _messageLineOne;
        private SourceUiBitmapText _messageLineTwo;
        private SourceUiBitmapText _ammoText;
        private SourceUiImage _ammoImage;
        private readonly SourceUiBitmapText[] _slotLabels = new SourceUiBitmapText[10];
        private readonly RetailQuickSlotPresentation[] _slots = new RetailQuickSlotPresentation[10];
        private SourceUiButton _combatButton;
        private Camera _managedCamera;
        private Rect _originalCameraRect;
        private bool _managingViewport;

        public bool IsAvailable => _resolver != null && _canvasRoot != null;
        public bool IsVisible => IsAvailable && _canvasRoot.activeSelf;
        public UiResolvedAsset BackgroundAsset => _background != null ? _background.CurrentAsset : null;
        public IReadOnlyList<RetailQuickSlotPresentation> QuickSlots => _slots;
        public float HealthFill { get; private set; }
        public float FatigueFill { get; private set; }
        public GameUiHudView LastProjection { get; private set; }

        public void Bind(GameUiController controller, WorldMapSessionCoordinator session,
            Action<GameUiScreen> openScreen, UiSkinResolver resolver = null)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            if (session == null) throw new ArgumentNullException(nameof(session));
            bool alreadyUsingRequestedResolver = resolver == null
                ? _ownsResolver
                : ReferenceEquals(_resolver, resolver);
            if (_controller == controller && IsAvailable && alreadyUsingRequestedResolver) return;
            Cleanup();
            _controller = controller;
            _session = session;
            _openScreen = openScreen ?? throw new ArgumentNullException(nameof(openScreen));
            try
            {
                _resolver = resolver ?? UiSkinResolver.CreateProduction(ReportProductionDiagnostic);
                _ownsResolver = resolver == null;
                BuildPresentation();
                Synchronize(show: true);
            }
            catch (Exception exception)
            {
                Debug.LogError("OpenArcanum UI-C gameplay HUD could not initialize: " + exception.Message);
                Cleanup();
            }
        }

        public void Synchronize(bool show)
        {
            if (!IsAvailable || _controller == null) return;
            bool visible = show && _controller.HasPlayer && _controller.Screen == GameUiScreen.None;
            if (_canvasRoot.activeSelf != visible) _canvasRoot.SetActive(visible);
            ManageWorldViewport(visible);
            if (!visible) return;

            LastProjection = _controller.ProjectHud();
            if (LastProjection == null) return;
            HealthFill = RetailGameplayHudLayout.LiquidFill01(
                LastProjection.HitPoints, LastProjection.MaximumHitPoints);
            FatigueFill = RetailGameplayHudLayout.LiquidFill01(
                LastProjection.Fatigue, LastProjection.MaximumFatigue);
            _healthLiquid.fillAmount = HealthFill;
            _fatigueLiquid.fillAmount = FatigueFill;
            SetText(_healthText, Mathf.Max(0, LastProjection.HitPoints).ToString(), SourceText);
            SetText(_fatigueText, Mathf.Max(0, LastProjection.Fatigue).ToString(), SourceText);

            string feedback = string.IsNullOrWhiteSpace(_controller.Feedback)
                ? LastProjection.Weapon : _controller.Feedback;
            SetText(_messageLineOne, Clip(feedback, 46), SourceText);
            string combat = LastProjection.CombatActive
                ? $"{LastProjection.CombatMode}  AP {LastProjection.ActionPoints}/{LastProjection.MaximumActionPoints}  {LastProjection.Readiness}"
                : $"{LastProjection.Readiness}  {LastProjection.Weapon}";
            SetText(_messageLineTwo, Clip(combat, 46), SourceText);
            SetText(_ammoText, LastProjection.Ammunition > 0 ? LastProjection.Ammunition.ToString() : string.Empty,
                SourceText);
            if (_ammoImage != null) _ammoImage.gameObject.SetActive(LastProjection.Ammunition > 0);
            _combatButton?.SetSelected(LastProjection.CombatActive);
            RefreshQuickSlots();
        }

        public bool ContainsScreenPoint(Vector2 screenBottomLeft)
            => IsVisible && RetailGameplayHudLayout.ContainsInterfacePoint(
                screenBottomLeft, Screen.width, Screen.height);

        private void BuildPresentation()
        {
            _canvasRoot = new GameObject("Source-Faithful Gameplay HUD",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
                typeof(SourceUiPresentationRoot), typeof(SourceUiRuntime));
            _canvasRoot.transform.SetParent(transform, worldPositionStays: false);
            _presentation = _canvasRoot.GetComponent<SourceUiPresentationRoot>();
            _canvasRoot.GetComponent<SourceUiRuntime>().BindResolver(_resolver);
            _canvasRoot.GetComponent<Canvas>().sortingOrder = 500;
            RectTransform hud = _presentation.GetLayer(SourceUiLayer.Hud);

            _background = AddImage("Retail HUD Frame", hud, new UiAssetKey(RetailGameplayHudLayout.BackgroundSourceId),
                new Rect(0f, 0f, 800f, 600f), false);
            AddInputBlocker("Top Interface Input", hud, RetailGameplayHudLayout.TopInterface);
            AddInputBlocker("Bottom Interface Input", hud, RetailGameplayHudLayout.BottomInterface);

            AddVial("Health Empty Vial", hud, new Rect(11f, 471f, 34f, 90f));
            AddVial("Fatigue Empty Vial", hud, new Rect(751f, 472f, 34f, 90f));
            _healthLiquid = AddLiquid("Health Liquid", hud, RetailGameplayHudLayout.HealthBar,
                RetailGameplayHudLayout.HealthLiquidSourceId);
            _fatigueLiquid = AddLiquid("Fatigue Liquid", hud, RetailGameplayHudLayout.FatigueBar,
                RetailGameplayHudLayout.FatigueLiquidSourceId);
            _healthText = AddText("Health Counter", hud, RetailGameplayHudLayout.HealthCounter, string.Empty,
                RetailGameplayHudLayout.CounterFontSourceId);
            _fatigueText = AddText("Fatigue Counter", hud, RetailGameplayHudLayout.FatigueCounter, string.Empty,
                RetailGameplayHudLayout.CounterFontSourceId);

            AddImage("Message Lens", hud, new UiAssetKey(RetailGameplayHudLayout.MessageWindowSourceId),
                RetailGameplayHudLayout.MessageWindow, false);
            RectTransform textClip = AddRect("Message Text Clip", hud, RetailGameplayHudLayout.MessageText);
            textClip.gameObject.AddComponent<RectMask2D>();
            _messageLineOne = AddText("Message", textClip, new Rect(4f, 5f, 375f, 14f), string.Empty,
                RetailGameplayHudLayout.CounterFontSourceId);
            _messageLineTwo = AddText("Combat State", textClip, new Rect(4f, 25f, 375f, 14f), string.Empty,
                RetailGameplayHudLayout.CounterFontSourceId);

            _ammoImage = AddImage("Ammo", hud, new UiAssetKey(251), RetailGameplayHudLayout.AmmoButton, false);
            _ammoText = AddText("Ammo Counter", hud, new Rect(99f, 512f, 38f, 14f), string.Empty,
                RetailGameplayHudLayout.CounterFontSourceId);

            AddScreenButton("Character", hud, 169, 4f, 2f, GameUiScreen.Character);
            AddScreenButton("Logbook", hud, 187, 41f, 2f, GameUiScreen.Journal);
            AddScreenButton("Map", hud, 193, 78f, 2f, GameUiScreen.Map);
            AddScreenButton("Inventory", hud, 186, 115f, 2f, GameUiScreen.Inventory);
            AddScreenButton("Skills", hud, 472, 693f, 456f, GameUiScreen.Skills);
            AddScreenButton("Spells", hud, 473, 649f, 494f, GameUiScreen.Magic);
            _combatButton = AddButton("Combat", hud, 470, 86f, 457f, () => _controller.ToggleCombatMode());
            AddScreenButton("Schematics", hud, 471, 693f, 539f, GameUiScreen.Crafting);

            for (int index = 0; index < 10; index++) AddQuickSlot(hud, index);

            var cursor = new GameObject("Retail Gameplay Cursor",
                typeof(RectTransform), typeof(Image), typeof(SourceUiImage), typeof(RetailMainMenuCursorView));
            cursor.transform.SetParent(_presentation.GetLayer(SourceUiLayer.Cursor), worldPositionStays: false);
            cursor.GetComponent<RetailMainMenuCursorView>().Bind(_presentation.ReferenceSurface, _resolver);

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("OpenArcanum UI EventSystem",
                    typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(transform, worldPositionStays: false);
            }
        }

        private void AddVial(string name, RectTransform parent, Rect rect)
            => AddImage(name, parent, new UiAssetKey(RetailGameplayHudLayout.EmptyVialSourceId), rect, false);

        private Image AddLiquid(string name, RectTransform parent, Rect rect, int sourceId)
        {
            SourceUiImage source = AddImage(name, parent, new UiAssetKey(sourceId), rect, false);
            Image image = source.Image;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Vertical;
            image.fillOrigin = (int)Image.OriginVertical.Bottom;
            image.fillAmount = 1f;
            return image;
        }

        private void AddScreenButton(string name, RectTransform parent, int sourceId, float x, float y,
            GameUiScreen screen)
            => AddButton(name, parent, sourceId, x, y, () => _openScreen(screen));

        private SourceUiButton AddButton(string name, RectTransform parent, int sourceId, float x, float y,
            Action clicked)
        {
            Vector2 size = ResolveLogicalSize(new UiAssetKey(sourceId));
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(SourceUiImage),
                typeof(SourceUiButton));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, worldPositionStays: false);
            Place(rect, new Rect(x, y, size.x, size.y));
            SourceUiButton source = go.GetComponent<SourceUiButton>();
            source.Bind(_resolver, new UiAssetKey(sourceId, frame: 0), new UiAssetKey(sourceId, frame: 1),
                new UiAssetKey(sourceId, frame: 2), new UiAssetKey(sourceId, frame: 0),
                new UiAssetKey(sourceId, frame: 1));
            source.Button.onClick.AddListener(() => clicked());
            return source;
        }

        private void AddQuickSlot(RectTransform parent, int index)
        {
            int sourceId = RetailGameplayHudLayout.QuickSlotSourceIds[index];
            Vector2 size = ResolveLogicalSize(new UiAssetKey(sourceId));
            if (size.x <= 0f || size.y <= 0f) size = new Vector2(32f, 32f);
            var go = new GameObject($"Quick Slot {(index == 9 ? 0 : index + 1)}",
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(SourceUiImage));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, worldPositionStays: false);
            Place(rect, new Rect(RetailGameplayHudLayout.QuickSlotX[index], 445f, size.x, size.y));
            go.GetComponent<SourceUiImage>().Bind(_resolver, new UiAssetKey(sourceId));
            Button button = go.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            int captured = index;
            button.onClick.AddListener(() => _controller.ActivateQuickSlot(captured));
            _slotLabels[index] = AddText("Binding", rect, new Rect(5f, 5f, size.x - 6f, size.y - 6f),
                string.Empty, RetailGameplayHudLayout.CounterFontSourceId);
            _slots[index] = new RetailQuickSlotPresentation { Index = index };
        }

        private void RefreshQuickSlots()
        {
            for (int index = 0; index < _slots.Length; index++)
            {
                QuickSlotBinding binding = _session.Shortcuts.Get(index);
                int quantity = 0;
                if (binding.Kind == QuickSlotKind.Item && _session.Shortcuts.TryResolveItem(index, out var item)
                    && _session.TryGetObjectState(item, out PersistentObjectState state))
                    quantity = state.StackQuantity ?? 1;
                RetailQuickSlotPresentation slot = _slots[index];
                slot.Kind = binding.Kind;
                slot.SourceId = binding.SourceId;
                slot.Quantity = quantity;
                slot.Active = _session.Shortcuts.ActiveSlot == index;
                string label = binding.Kind switch
                {
                    QuickSlotKind.Item => quantity > 1 ? quantity.ToString() : "I",
                    QuickSlotKind.Spell => "S",
                    _ => string.Empty,
                };
                SetText(_slotLabels[index], label, slot.Active ? SelectedText : SourceText);
            }
        }

        private SourceUiImage AddImage(string name, RectTransform parent, UiAssetKey key, Rect sourceRect,
            bool raycast)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(SourceUiImage));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, worldPositionStays: false);
            Place(rect, sourceRect);
            SourceUiImage source = go.GetComponent<SourceUiImage>();
            source.Bind(_resolver, key);
            source.Image.raycastTarget = raycast;
            return source;
        }

        private static void AddInputBlocker(string name, RectTransform parent, Rect sourceRect)
        {
            RectTransform rect = AddRect(name, parent, sourceRect);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
        }

        private SourceUiBitmapText AddText(string name, RectTransform parent, Rect sourceRect, string value,
            int fontSourceId)
        {
            RectTransform rect = AddRect(name, parent, sourceRect);
            var bitmap = rect.gameObject.AddComponent<SourceUiBitmapText>();
            bitmap.Bind(_resolver, value, SourceText, fontSourceId);
            return bitmap;
        }

        private static RectTransform AddRect(string name, RectTransform parent, Rect sourceRect)
        {
            var go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, worldPositionStays: false);
            Place(rect, sourceRect);
            return rect;
        }

        private static void Place(RectTransform rect, Rect sourceRect)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(sourceRect.x, -sourceRect.y);
            rect.sizeDelta = sourceRect.size;
        }

        private Vector2 ResolveLogicalSize(UiAssetKey key)
            => _resolver.TryResolve(key, out UiResolvedAsset asset) ? asset.LogicalSize : new Vector2(32f, 32f);

        private void ManageWorldViewport(bool visible)
        {
            Camera camera = Camera.main;
            if (visible && camera != null)
            {
                if (!_managingViewport || _managedCamera != camera)
                {
                    RestoreWorldViewport();
                    _managedCamera = camera;
                    _originalCameraRect = camera.rect;
                    _managingViewport = true;
                }
                camera.rect = RetailGameplayHudLayout.GameplayCameraViewport(Screen.width, Screen.height);
            }
            else RestoreWorldViewport();
        }

        private void RestoreWorldViewport()
        {
            if (_managingViewport && _managedCamera != null) _managedCamera.rect = _originalCameraRect;
            _managedCamera = null;
            _managingViewport = false;
        }

        private static void SetText(SourceUiBitmapText text, string value, Color tint)
        {
            if (text == null) return;
            if (!string.Equals(text.Text, value, StringComparison.Ordinal))
                text.Bind(GetResolver(text), value, tint, text.FontSourceId);
            else text.SetTint(tint);
        }

        private static UiSkinResolver GetResolver(SourceUiBitmapText text)
        {
            SourceUiRuntime runtime = text.GetComponentInParent<SourceUiRuntime>();
            return runtime != null ? runtime.Resolver : throw new InvalidOperationException("Source UI runtime missing.");
        }

        private static string Clip(string value, int maximum)
            => string.IsNullOrEmpty(value) ? string.Empty : value.Length <= maximum ? value : value[..maximum];

        private void OnDisable() => RestoreWorldViewport();
        private void OnDestroy() => Cleanup();

        private void Cleanup()
        {
            RestoreWorldViewport();
            if (_canvasRoot != null)
            {
                _canvasRoot.SetActive(false);
                if (Application.isPlaying) Object.Destroy(_canvasRoot); else Object.DestroyImmediate(_canvasRoot);
            }
            if (_ownsResolver) _resolver?.Dispose();
            _resolver = null;
            _ownsResolver = false;
            _canvasRoot = null;
            _presentation = null;
            _background = null;
            _healthLiquid = null;
            _fatigueLiquid = null;
            _healthText = null;
            _fatigueText = null;
            _messageLineOne = null;
            _messageLineTwo = null;
            _ammoText = null;
            _ammoImage = null;
            _combatButton = null;
            Array.Clear(_slotLabels, 0, _slotLabels.Length);
            Array.Clear(_slots, 0, _slots.Length);
            LastProjection = null;
        }

        private static void ReportProductionDiagnostic(string message)
        {
            if (message?.IndexOf(" is missing; using Original retail frame", StringComparison.Ordinal) >= 0) return;
            Debug.LogWarning(message);
        }
    }
}
