using System;
using System.Collections.Generic;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Magic;
using Arcanum.Runtime.World;
using OpenArcanum.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Arcanum.Runtime.UI
{
    /// <summary>
    /// Source-backed UI-C geometry. Source coordinates remain the retail 800x600 coordinate space, while the
    /// production windows follow <c>iso_interface_create</c>: native 800-pixel art centered horizontally, with ID 185
    /// anchored to the top and ID 184 anchored to the bottom.
    /// </summary>
    public static class RetailGameplayHudLayout
    {
        public const int TopSourceId = 185;
        public const int BottomSourceId = 184;
        public const int MessageWindowSourceId = 354;
        public const int SkillsWindowSourceId = 274;
        public const int HealthLiquidSourceId = 18;
        public const int FatigueLiquidSourceId = 19;
        public const int EmptyVialSourceId = 20;
        public const int TextFontSourceId = 27;
        public const int CounterFontSourceId = 171;
        public const int ContextualHelpIconSourceId = 444;
        public const int ContextualHelpFontSourceId = 229;
        public const int ContextualHelpTitleFontSourceId = 27;
        public const float ContextualHelpLineHeight = 18f;
        public const int FollowerSlotCount = 6;
        public const int FollowerFrameSourceId = 503;
        public const int FollowerFatigueSourceId = 504;
        public const int FollowerHealthSourceId = 505;
        public const int FollowerPoisonSourceId = 616;
        public const int FollowerLowHealthThreshold = 20;
        public const int FollowerPoisonThreshold = 20;

        public static readonly Rect TopInterface = new Rect(0f, 0f, 800f, 41f);
        public static readonly Rect WorldViewport = new Rect(0f, 41f, 800f, 400f);
        public static readonly Rect BottomInterface = new Rect(0f, 441f, 800f, 159f);
        public static readonly Rect HealthBar = new Rect(14f, 472f, 28f, 88f);
        public static readonly Rect FatigueBar = new Rect(754f, 473f, 28f, 88f);
        public static readonly Rect HealthCounter = new Rect(15f, 578f, 29f, 12f);
        public static readonly Rect FatigueCounter = new Rect(755f, 578f, 27f, 12f);
        public static readonly Rect FateCounter = new Rect(190f, 17f, 24f, 12f);
        public static readonly Rect ContextCounter = new Rect(104f, 512f, 50f, 20f);
        public static readonly Rect ClockFrame = new Rect(648f, 5f, 128f, 30f);
        public static readonly Rect MessageWindow = new Rect(196f, 492f, 410f, 107f);
        public static readonly Rect MessageText = new Rect(211f, 503f, 383f, 82f);
        public static readonly Rect WorldTargetIcon = new Rect(217f, 510f, 64f, 64f);
        public static readonly Rect WorldTargetName = new Rect(290f, 504f, 291f, 19f);
        public static readonly Rect WorldTargetStatus = new Rect(290f, 528f, 291f, 18f);
        public static readonly Rect ContextualHelpText = new Rect(290f, 504f, 291f, 78f);
        public static readonly Rect ContextualHelpTitle = new Rect(290f, 504f, 291f, 19f);
        public static readonly Rect ContextualHelpBody = new Rect(290f, 528f, 291f, 54f);
        public static readonly Rect FollowerPortrait = new Rect(4f, 4f, 32f, 32f);
        public static readonly Rect FollowerHealth = new Rect(3f, 39f, 34f, 3f);
        public static readonly Rect FollowerFatigue = new Rect(3f, 44f, 34f, 3f);
        public static readonly Rect AmmoButton = new Rect(61f, 509f, 37f, 24f);
        public static readonly Rect[] RecentActions =
            { new Rect(69f, 548f, 32f, 32f), new Rect(114f, 548f, 32f, 32f) };
        public static readonly int[] ExperienceSegmentSourceIds =
            { 773, 774, 775, 776, 777, 778, 779, 780, 781, 782 };
        public static readonly float[] ExperienceSegmentX =
            { 211f, 249f, 287f, 327f, 365f, 403f, 439f, 478f, 516f, 555f };

        public static readonly int[] QuickSlotSourceIds =
            { 173, 174, 175, 176, 177, 178, 179, 180, 181, 172 };
        public static readonly float[] QuickSlotX =
            { 198f, 237f, 276f, 315f, 354f, 418f, 456f, 495f, 534f, 573f };
        public static readonly int[] MaintainedSpellOpenSourceIds = { 188, 189, 190, 191, 192 };
        public static readonly int[] MaintainedSpellPluggedSourceIds = { 628, 629, 630, 631, 632 };

        public static Rect FollowerSlot(int index)
        {
            if (index < 0 || index >= FollowerSlotCount) throw new ArgumentOutOfRangeException(nameof(index));
            return new Rect(11f, 50f + 62f * index, 40f, 49f);
        }

        public static int FollowerVisiblePixels(int percent)
            => 34 * Mathf.Clamp(percent, 0, 100) / 100;

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
            // The source creates both interface windows over the ISO window. The world therefore remains full-screen
            // and naturally expands around/between the native-sized HUD strips at high resolutions.
            return new Rect(0f, 0f, 1f, 1f);
        }

        public static float GameplayCameraOrthographicSize(int height, float pixelsPerUnit)
        {
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (pixelsPerUnit <= 0f) throw new ArgumentOutOfRangeException(nameof(pixelsPerUnit));
            // The source draws world art one-for-one into the ISO window. Match that pixel density while allowing
            // the CE high-resolution content rect to expose additional pixels without rescaling the world.
            return height / (2f * pixelsPerUnit);
        }

        public static Rect TopScreenRect(int width, int height)
        {
            ValidateResolution(width, height);
            return new Rect((width - TopInterface.width) * .5f, 0f,
                TopInterface.width, TopInterface.height);
        }

        public static Rect BottomScreenRect(int width, int height)
        {
            ValidateResolution(width, height);
            return new Rect((width - BottomInterface.width) * .5f, height - BottomInterface.height,
                BottomInterface.width, BottomInterface.height);
        }

        public static Rect TopWindowLocal(Rect sourceRect)
            => new Rect(sourceRect.x - TopInterface.x, sourceRect.y - TopInterface.y,
                sourceRect.width, sourceRect.height);

        public static Rect BottomWindowLocal(Rect sourceRect)
            => new Rect(sourceRect.x - BottomInterface.x, sourceRect.y - BottomInterface.y,
                sourceRect.width, sourceRect.height);

        public static bool ContainsInterfacePoint(Vector2 screenBottomLeft, int width, int height)
        {
            ValidateResolution(width, height);
            Vector2 topLeft = new Vector2(screenBottomLeft.x, height - screenBottomLeft.y);
            return TopScreenRect(width, height).Contains(topLeft)
                   || BottomScreenRect(width, height).Contains(topLeft);
        }

        private static void ValidateResolution(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
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

    public enum RetailClockPeriod { Morning, Midday, Evening, Night }

    [DisallowMultipleComponent]
    public sealed class RetailGameplayHudView : MonoBehaviour
    {
        private const string ClockHelpKey = "mes/intgame.mes:clock";
        private static readonly Color SourceText = new Color32(231, 217, 174, 255);
        private static readonly Color SelectedText = new Color32(240, 190, 45, 255);
        private static readonly Color ContextualHelpTextColor = Color.white;

        private GameUiController _controller;
        private WorldMapSessionCoordinator _session;
        private Action<GameUiScreen> _openScreen;
        private UiSkinResolver _resolver;
        private bool _ownsResolver;
        private GameObject _canvasRoot;
        private RectTransform _topWindow;
        private RectTransform _bottomWindow;
        private RectTransform _cursorLayer;
        private RectTransform _followerLayer;
        private SourceUiImage _topBackground;
        private SourceUiImage _bottomBackground;
        private Image _healthLiquid;
        private Image _fatigueLiquid;
        private SourceUiBitmapText _healthText;
        private SourceUiBitmapText _fatigueText;
        private SourceUiBitmapText _fateText;
        private SourceUiBitmapText _contextText;
        private SourceUiBitmapText _messageLineOne;
        private SourceUiBitmapText _messageLineTwo;
        private SourceUiImage _worldTargetIcon;
        private Image _worldTargetItemIcon;
        private SourceUiBitmapText _worldTargetName;
        private SourceUiBitmapText _worldTargetStatus;
        private readonly SourceUiBitmapText[] _contextualHelpLines = new SourceUiBitmapText[4];
        private SourceUiBitmapText _contextualHelpTitle;
        private GameUiWorldTargetView _worldTarget;
        private SourceUiImage _ammoImage;
        private readonly SourceUiBitmapText[] _slotLabels = new SourceUiBitmapText[10];
        private readonly RetailQuickSlotPresentation[] _slots = new RetailQuickSlotPresentation[10];
        private readonly SourceUiImage[] _maintainedSpellSlots = new SourceUiImage[5];
        private readonly Button[] _maintainedSpellButtons = new Button[5];
        private readonly SourceUiImage[] _recentActionImages = new SourceUiImage[2];
        private readonly Button[] _recentActionButtons = new Button[2];
        private readonly string[] _recentActionHelpOwners = new string[2];
        private readonly SourceUiImage[] _experienceSegments = new SourceUiImage[10];
        private SourceUiImage _experiencePartial;
        private RectTransform _clockClip;
        private readonly SourceUiImage[] _clockBands = new SourceUiImage[3];
        private SourceUiImage _clockPointer;
        private SourceUiButton _characterButton;
        private SourceUiButton _logbookButton;
        private SourceUiButton _mapButton;
        private SourceUiButton _inventoryButton;
        private SourceUiButton _fateButton;
        private SourceUiButton _sleepButton;
        private RectTransform _fatePanel;
        private RectTransform _sleepPanel;
        private RectTransform _skillsPanel;
        private readonly SourceUiButton[] _skillActionButtons = new SourceUiButton[4];
        private readonly SourceUiBitmapText[] _sleepLabels = new SourceUiBitmapText[8];
        private readonly SourceUiButton[] _sleepButtons = new SourceUiButton[8];
        private readonly Button[] _sleepRows = new Button[8];
        private SourceUiButton _combatButton;
        private readonly RectTransform[] _followerSlots = new RectTransform[RetailGameplayHudLayout.FollowerSlotCount];
        private readonly Image[] _followerPortraits = new Image[RetailGameplayHudLayout.FollowerSlotCount];
        private readonly SourceUiImage[] _followerFallbackIcons = new SourceUiImage[RetailGameplayHudLayout.FollowerSlotCount];
        private readonly SourceUiImage[] _followerHealthBars = new SourceUiImage[RetailGameplayHudLayout.FollowerSlotCount];
        private readonly SourceUiImage[] _followerFatigueBars = new SourceUiImage[RetailGameplayHudLayout.FollowerSlotCount];
        private readonly Image[] _followerDangerTints = new Image[RetailGameplayHudLayout.FollowerSlotCount];
        private Camera _managedCamera;
        private Rect _originalCameraRect;
        private float _originalCameraOrthographicSize;
        private bool _managingViewport;

        public bool IsAvailable => _resolver != null && _canvasRoot != null;
        public bool IsVisible => IsAvailable && _canvasRoot.activeSelf;
        public UiSkinResolver Resolver => _resolver;
        public UiResolvedAsset TopAsset => _topBackground != null ? _topBackground.CurrentAsset : null;
        public UiResolvedAsset BottomAsset => _bottomBackground != null ? _bottomBackground.CurrentAsset : null;
        public IReadOnlyList<RetailQuickSlotPresentation> QuickSlots => _slots;
        public float HealthFill { get; private set; }
        public float FatigueFill { get; private set; }
        public GameUiHudView LastProjection { get; private set; }
        public RetailClockPeriod ClockPeriod { get; private set; }
        public GameUiWorldTargetView WorldTarget => _worldTarget;
        public Canvas CursorCanvas => _cursorLayer != null ? _cursorLayer.GetComponent<Canvas>() : null;
        public bool IsContextualHelpVisible => _contextualHelpLines[0] != null
                                               && _contextualHelpLines[0].gameObject.activeSelf;
        public string ContextualHelpText => _controller?.ContextualHelp ?? string.Empty;
        public string ContextualHelpTitle => _controller?.ContextualHelpTitle ?? string.Empty;

        public void SetWorldTarget(GameUiWorldTargetView target) => _worldTarget = target;

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
            bool visible = show && _controller.HasPlayer
                                && _controller.Screen is GameUiScreen.None or GameUiScreen.Inventory
                                    or GameUiScreen.Character or GameUiScreen.Magic
                                    or GameUiScreen.Technology or GameUiScreen.Crafting
                                    or GameUiScreen.Dialogue or GameUiScreen.Journal;
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
            SetCounter(_healthText, Mathf.Max(0, LastProjection.HitPoints));
            SetCounter(_fatigueText, Mathf.Max(0, LastProjection.Fatigue));
            RefreshMaintainedSpellSlots(LastProjection.MaintainedSpellSlotCapacity, LastProjection.ActiveEffects);

            bool contextualHelpVisible = !string.IsNullOrWhiteSpace(_controller.ContextualHelp)
                                         || !string.IsNullOrWhiteSpace(_controller.ContextualHelpTitle);
            bool targetVisible = !contextualHelpVisible && _worldTarget != null
                                 && _controller.Screen == GameUiScreen.None;
            bool itemTarget = targetVisible && _worldTarget.Item != null;
            _worldTargetIcon.gameObject.SetActive(contextualHelpVisible || targetVisible && !itemTarget);
            _worldTargetItemIcon.gameObject.SetActive(itemTarget);
            _worldTargetName.gameObject.SetActive(targetVisible);
            _worldTargetStatus.gameObject.SetActive(targetVisible);
            _messageLineOne.gameObject.SetActive(!contextualHelpVisible && !targetVisible);
            _messageLineTwo.gameObject.SetActive(!contextualHelpVisible && !targetVisible);
            if (!contextualHelpVisible) HideContextualHelp();
            if (contextualHelpVisible)
            {
                _worldTargetIcon.SetKey(new UiAssetKey(_controller.ContextualHelpIconSourceId));
                RefreshContextualHelp();
            }
            else if (targetVisible)
            {
                if (itemTarget && _worldTarget.Item.InventoryArtId is uint inventoryArt
                    && _resolver.Original.TryResolveInventoryItem(inventoryArt, out RetailItemArtAsset asset))
                    _worldTargetItemIcon.sprite = asset.Sprite;
                else if (!itemTarget)
                    _worldTargetIcon.SetKey(new UiAssetKey(_worldTarget.IconSourceId));
                SetText(_worldTargetName, Clip(_worldTarget.Name, 38),
                    _worldTarget.Hostile ? new Color32(244, 43, 25, 255) : SourceText);
                string status = itemTarget ? _worldTarget.Item.Summary
                    : _worldTarget.Dead ? SourceMessage("mes/intgame.mes", 16, string.Empty) : string.Empty;
                SetText(_worldTargetStatus, Clip(status, 46),
                    new Color32(244, 43, 25, 255));
            }
            else
            {
                SetText(_messageLineOne, Clip(_controller.Feedback, 46), SourceText);
                string combat = LastProjection.CombatActive
                    ? $"{LastProjection.CombatMode}  AP {LastProjection.ActionPoints}/{LastProjection.MaximumActionPoints}  {LastProjection.Readiness}"
                    : string.Empty;
                SetText(_messageLineTwo, Clip(combat, 46), SourceText);
            }
            SetCounter(_fateText, Mathf.Max(0, LastProjection.FatePoints), 2);
            SetCounter(_contextText, Mathf.Max(0, LastProjection.ContextQuantity), 6);
            if (_ammoImage != null) _ammoImage.SetKey(new UiAssetKey(LastProjection.ContextIconSourceId));
            RefreshPrimaryButtons();
            RefreshClock();
            RefreshExperienceGauge();
            RefreshRecentActions();
            RefreshFollowerSlots();
            if (_fatePanel != null) _fatePanel.gameObject.SetActive(_controller.HudPanel == GameUiHudPanel.Fate);
            if (_sleepPanel != null)
            {
                bool sleepOpen = _controller.HudPanel == GameUiHudPanel.Sleep;
                _sleepPanel.gameObject.SetActive(sleepOpen);
                if (sleepOpen) RefreshSleepPanel();
            }
            if (_skillsPanel != null)
                _skillsPanel.gameObject.SetActive(_controller.HudPanel == GameUiHudPanel.Skills);
            _combatButton?.SetSelected(LastProjection.CombatActive);
            RefreshQuickSlots();
        }

        public bool ContainsScreenPoint(Vector2 screenBottomLeft)
        {
            if (!IsVisible) return false;
            if (RetailGameplayHudLayout.ContainsInterfacePoint(screenBottomLeft, Screen.width, Screen.height))
                return true;
            Vector2 topLeft = new Vector2(screenBottomLeft.x, Screen.height - screenBottomLeft.y);
            int visible = Mathf.Min(RetailGameplayHudLayout.FollowerSlotCount,
                _controller?.ProjectParty().Count ?? 0);
            for (int index = 0; index < visible; index++)
                if (RetailGameplayHudLayout.FollowerSlot(index).Contains(topLeft)) return true;
            return false;
        }

        private void BuildPresentation()
        {
            _canvasRoot = new GameObject("Source-Faithful Gameplay HUD",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
                typeof(SourceUiRuntime));
            _canvasRoot.transform.SetParent(transform, worldPositionStays: false);
            _canvasRoot.GetComponent<SourceUiRuntime>().BindResolver(_resolver);
            Canvas canvas = _canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            CanvasScaler scaler = _canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 1f;

            RectTransform canvasRect = (RectTransform)_canvasRoot.transform;
            _topWindow = AddWindow("Retail Top HUD Window", canvasRect, top: true);
            _bottomWindow = AddWindow("Retail Bottom HUD Window", canvasRect, top: false);
            _followerLayer = AddFullScreenLayer("Retail Follower HUD Layer", canvasRect);
            _cursorLayer = AddFullScreenLayer("Retail Gameplay Cursor Layer", canvasRect);
            Canvas cursorCanvas = _cursorLayer.gameObject.AddComponent<Canvas>();
            cursorCanvas.overrideSorting = true;
            cursorCanvas.sortingOrder = canvas.sortingOrder + 1;

            _topBackground = AddImage("Retail Top HUD Frame", _topWindow,
                new UiAssetKey(RetailGameplayHudLayout.TopSourceId), new Rect(0f, 0f, 800f, 41f), false);
            _bottomBackground = AddImage("Retail Bottom HUD Frame", _bottomWindow,
                new UiAssetKey(RetailGameplayHudLayout.BottomSourceId), new Rect(0f, 0f, 800f, 159f), false);
            AddInputBlocker("Top Interface Input", _topWindow, new Rect(0f, 0f, 800f, 41f));
            AddInputBlocker("Bottom Interface Input", _bottomWindow, new Rect(0f, 0f, 800f, 159f));

            for (int index = 0; index < _maintainedSpellSlots.Length; index++)
            {
                _maintainedSpellSlots[index] = AddImage($"Maintained Spell Slot {index + 1}", _topWindow,
                    new UiAssetKey(RetailGameplayHudLayout.MaintainedSpellPluggedSourceIds[index]),
                    new Rect(280f + 50f * index, 2f, 35f, 35f), false);
                Button button = _maintainedSpellSlots[index].gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                _maintainedSpellSlots[index].Image.raycastTarget = true;
                int captured = index;
                button.onClick.AddListener(() => CancelMaintainedSpell(captured));
                _maintainedSpellButtons[index] = button;
            }

            _fateText = AddCounter("Fate Counter", _topWindow,
                RetailGameplayHudLayout.TopWindowLocal(RetailGameplayHudLayout.FateCounter));

            _clockClip = AddRect("Clock Clip", _topWindow,
                RetailGameplayHudLayout.TopWindowLocal(RetailGameplayHudLayout.ClockFrame));
            _clockClip.gameObject.AddComponent<RectMask2D>();
            Image clockHit = _clockClip.gameObject.AddComponent<Image>();
            clockHit.color = Color.clear;
            clockHit.raycastTarget = true;
            _clockClip.gameObject.AddComponent<SourceHoverHintTarget>().Bind(
                ShowClockContextualHelp, () => _controller.ClearContextualHelp(ClockHelpKey));
            for (int index = 0; index < _clockBands.Length; index++)
                _clockBands[index] = AddImage($"Clock Band {index + 1}", _clockClip,
                    new UiAssetKey(index % 2 == 0 ? 207 : 208), new Rect(0f, 0f, 1f, 30f), false);
            Vector2 pointerSize = ResolveLogicalSize(new UiAssetKey(216));
            _clockPointer = AddImage("Clock Pointer", _topWindow, new UiAssetKey(216),
                new Rect(708f, 6f, pointerSize.x, pointerSize.y), false);

            AddVial("Health Empty Vial", _bottomWindow,
                RetailGameplayHudLayout.BottomWindowLocal(new Rect(11f, 471f, 34f, 90f)));
            AddVial("Fatigue Empty Vial", _bottomWindow,
                RetailGameplayHudLayout.BottomWindowLocal(new Rect(751f, 472f, 34f, 90f)));
            _healthLiquid = AddLiquid("Health Liquid", _bottomWindow,
                RetailGameplayHudLayout.BottomWindowLocal(RetailGameplayHudLayout.HealthBar),
                RetailGameplayHudLayout.HealthLiquidSourceId);
            _fatigueLiquid = AddLiquid("Fatigue Liquid", _bottomWindow,
                RetailGameplayHudLayout.BottomWindowLocal(RetailGameplayHudLayout.FatigueBar),
                RetailGameplayHudLayout.FatigueLiquidSourceId);
            _healthText = AddCounter("Health Counter", _bottomWindow,
                RetailGameplayHudLayout.BottomWindowLocal(RetailGameplayHudLayout.HealthCounter));
            _fatigueText = AddCounter("Fatigue Counter", _bottomWindow,
                RetailGameplayHudLayout.BottomWindowLocal(RetailGameplayHudLayout.FatigueCounter));

            for (int index = 0; index < _experienceSegments.Length; index++)
            {
                Vector2 size = ResolveLogicalSize(new UiAssetKey(RetailGameplayHudLayout.ExperienceSegmentSourceIds[index]));
                _experienceSegments[index] = AddImage($"Experience Segment {index + 1}", _bottomWindow,
                    new UiAssetKey(RetailGameplayHudLayout.ExperienceSegmentSourceIds[index]),
                    RetailGameplayHudLayout.BottomWindowLocal(new Rect(
                        RetailGameplayHudLayout.ExperienceSegmentX[index], 478f, size.x, size.y)), false);
            }
            Vector2 partialSize = ResolveLogicalSize(new UiAssetKey(772));
            _experiencePartial = AddImage("Experience Partial", _bottomWindow, new UiAssetKey(772),
                RetailGameplayHudLayout.BottomWindowLocal(new Rect(216f, 488f, partialSize.x, partialSize.y)), false);
            _experiencePartial.Image.type = Image.Type.Filled;
            _experiencePartial.Image.fillMethod = Image.FillMethod.Horizontal;
            _experiencePartial.Image.fillOrigin = (int)Image.OriginHorizontal.Left;

            AddImage("Message Lens", _bottomWindow, new UiAssetKey(RetailGameplayHudLayout.MessageWindowSourceId),
                RetailGameplayHudLayout.BottomWindowLocal(RetailGameplayHudLayout.MessageWindow), false);
            RectTransform textClip = AddRect("Message Text Clip", _bottomWindow,
                RetailGameplayHudLayout.BottomWindowLocal(RetailGameplayHudLayout.MessageText));
            textClip.gameObject.AddComponent<RectMask2D>();
            _messageLineOne = AddText("Message", textClip, new Rect(4f, 5f, 375f, 14f), string.Empty,
                RetailGameplayHudLayout.TextFontSourceId);
            _messageLineTwo = AddText("Combat State", textClip, new Rect(4f, 25f, 375f, 14f), string.Empty,
                RetailGameplayHudLayout.TextFontSourceId);
            _worldTargetIcon = AddImage("World Target Icon", _bottomWindow, new UiAssetKey(375),
                RetailGameplayHudLayout.BottomWindowLocal(RetailGameplayHudLayout.WorldTargetIcon), false);
            RectTransform targetItemIcon = AddRect("World Item Icon", _bottomWindow,
                RetailGameplayHudLayout.BottomWindowLocal(RetailGameplayHudLayout.WorldTargetIcon));
            _worldTargetItemIcon = targetItemIcon.gameObject.AddComponent<Image>();
            _worldTargetItemIcon.preserveAspect = true;
            _worldTargetItemIcon.raycastTarget = false;
            _worldTargetItemIcon.gameObject.SetActive(false);
            _worldTargetName = AddText("World Target Name", _bottomWindow,
                RetailGameplayHudLayout.BottomWindowLocal(RetailGameplayHudLayout.WorldTargetName), string.Empty, 27);
            _worldTargetStatus = AddText("World Target Status", _bottomWindow,
                RetailGameplayHudLayout.BottomWindowLocal(RetailGameplayHudLayout.WorldTargetStatus), string.Empty, 229);
            _contextualHelpTitle = AddText("Contextual Help Title", _bottomWindow,
                RetailGameplayHudLayout.BottomWindowLocal(RetailGameplayHudLayout.ContextualHelpTitle), string.Empty,
                RetailGameplayHudLayout.ContextualHelpTitleFontSourceId);
            for (int index = 0; index < _contextualHelpLines.Length; index++)
                _contextualHelpLines[index] = AddText(index == 0 ? "Contextual Help" : $"Contextual Help {index + 1}",
                    _bottomWindow, RetailGameplayHudLayout.BottomWindowLocal(new Rect(
                        RetailGameplayHudLayout.ContextualHelpText.x,
                        RetailGameplayHudLayout.ContextualHelpText.y
                        + RetailGameplayHudLayout.ContextualHelpLineHeight * index,
                        RetailGameplayHudLayout.ContextualHelpText.width,
                        RetailGameplayHudLayout.ContextualHelpLineHeight)), string.Empty,
                    RetailGameplayHudLayout.ContextualHelpFontSourceId);

            _ammoImage = AddImage("Ammo", _bottomWindow, new UiAssetKey(251),
                RetailGameplayHudLayout.BottomWindowLocal(RetailGameplayHudLayout.AmmoButton), false);
            _contextText = AddCounter("Context Counter", _bottomWindow,
                RetailGameplayHudLayout.BottomWindowLocal(RetailGameplayHudLayout.ContextCounter));

            _characterButton = AddScreenButton("Character", _topWindow, 169, 4f, 2f, GameUiScreen.Character);
            _logbookButton = AddScreenButton("Logbook", _topWindow, 187, 41f, 2f, GameUiScreen.Journal);
            _mapButton = AddScreenButton("Map", _topWindow, 193, 78f, 2f, GameUiScreen.Map);
            _inventoryButton = AddScreenButton("Inventory", _topWindow, 186, 115f, 2f, GameUiScreen.Inventory);
            _fateButton = AddButton("Fate", _topWindow, 137, 157f, 9f, () => _controller.ToggleFatePanel());
            _sleepButton = AddButton("Sleep", _topWindow, 137, 605f, 9f,
                () => _controller.ToggleSleepPanel(), hasPressedFrame: false);
            SourceUiButton skills = AddButton("Skills", _bottomWindow, 472, 693f, 456f - 441f,
                () => _controller.ToggleSkillsPanel());
            SourceUiButton spells = AddScreenButton("Spells", _bottomWindow, 473, 649f, 494f - 441f,
                GameUiScreen.Magic);
            _combatButton = AddButton("Combat", _bottomWindow, 470, 86f, 457f - 441f,
                () => _controller.ToggleCombatMode());
            SourceUiButton schematics = AddScreenButton("Schematics", _bottomWindow, 471, 693f, 539f - 441f,
                GameUiScreen.Crafting);

            BindFeedbackHelp(_characterButton, 1005);
            BindFeedbackHelp(_logbookButton, 1004);
            BindFeedbackHelp(_mapButton, 1007);
            BindFeedbackHelp(_inventoryButton, 1006);
            BindFeedbackHelp(_fateButton, 1008);
            BindFeedbackHelp(_sleepButton, 1009);
            BindFeedbackHelp(_combatButton, 1002);
            BindFeedbackHelp(skills, 1001);
            BindFeedbackHelp(spells, 1000);
            BindFeedbackHelp(schematics, 1003);

            for (int index = 0; index < _recentActionImages.Length; index++) AddRecentAction(index);

            for (int index = 0; index < 10; index++) AddQuickSlot(_bottomWindow, index);

            BuildFatePanel();
            BuildSleepPanel();
            BuildSkillsPanel();
            BuildFollowerSlots();

            var cursor = new GameObject("Retail Gameplay Cursor",
                typeof(RectTransform), typeof(Image), typeof(SourceUiImage), typeof(RetailMainMenuCursorView));
            cursor.transform.SetParent(_cursorLayer, worldPositionStays: false);
            cursor.GetComponent<RetailMainMenuCursorView>().Bind(canvasRect, _resolver);

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

        private SourceUiButton AddScreenButton(string name, RectTransform parent, int sourceId, float x, float y,
            GameUiScreen screen)
            => AddButton(name, parent, sourceId, x, y,
                () => { if (_controller.Screen == screen) _controller.Close(); else _openScreen(screen); });

        private SourceUiButton AddButton(string name, RectTransform parent, int sourceId, float x, float y,
            Action clicked, bool hasPressedFrame = true)
        {
            Vector2 size = ResolveLogicalSize(new UiAssetKey(sourceId));
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(SourceUiImage),
                typeof(SourceUiButton));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, worldPositionStays: false);
            Place(rect, new Rect(x, y, size.x, size.y));
            SourceUiButton source = go.GetComponent<SourceUiButton>();
            source.Bind(_resolver, new UiAssetKey(sourceId, frame: 0), new UiAssetKey(sourceId, frame: 1),
                new UiAssetKey(sourceId, frame: hasPressedFrame ? 2 : 1), new UiAssetKey(sourceId, frame: 0),
                new UiAssetKey(sourceId, frame: 1));
            source.Button.onClick.AddListener(() => clicked());
            return source;
        }

        private void AddRecentAction(int index)
        {
            Rect sourceRect = RetailGameplayHudLayout.BottomWindowLocal(RetailGameplayHudLayout.RecentActions[index]);
            SourceUiImage image = AddImage($"Recent Action {index + 1}", _bottomWindow,
                new UiAssetKey(index == 0 ? 280 : 279), sourceRect, true);
            Button button = image.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            int captured = index;
            button.onClick.AddListener(() => _controller.ActivateRecentAction(captured));
            image.gameObject.AddComponent<SourceHoverHintTarget>().Bind(
                () => ShowRecentActionContextualHelp(captured),
                () => ClearRecentActionContextualHelp(captured));
            _recentActionImages[index] = image;
            _recentActionButtons[index] = button;
        }

        private void BuildFatePanel()
        {
            _fatePanel = AddSourcePanel("Fate Panel", _topWindow, 292, 0f, 41f);
            string[] fallback =
            {
                "Full Heal", "Force Good Reaction", "Critical Hit", "Critical Miss",
                "Save Against Magick", "Spell At Maximum", "Critical Success: Gambling",
                "Critical Success: Heal", "Critical Success: Pick Pocket", "Critical Success: Repair",
                "Critical Success: Pick Locks", "Critical Success: Disarm Traps",
            };
            float width = _fatePanel.sizeDelta.x;
            for (int index = 0; index < fallback.Length; index++)
            {
                string label = SourceMessage("mes/fate_ui.mes", index, fallback[index]);
                AddText($"Fate Label {index}", _fatePanel, new Rect(11f, 3f + 18f * index,
                    Mathf.Max(1f, width - 11f), 18f), label, 229);
                int captured = index;
                AddButton($"Fate Choice {index}", _fatePanel, 293, 223f, 4f + 18f * index,
                    () => _controller.ActivateFate((FateChoice)captured), hasPressedFrame: false);
                AddChoiceHitArea($"Fate Row {index}", _fatePanel,
                    new Rect(7f, 2f + 18f * index, Mathf.Max(1f, width - 10f), 18f),
                    () => _controller.ActivateFate((FateChoice)captured));
            }
            _fatePanel.gameObject.SetActive(false);
        }

        private void BuildSleepPanel()
        {
            _sleepPanel = AddSourcePanel("Sleep Panel", _topWindow, 565, 573f, 41f);
            string[] fallback =
            {
                "Sleep For One Hour", "Sleep For Two Hours", "Sleep For Four Hours",
                "Sleep For Eight Hours", "Sleep For One Day", "Sleep Until Morning",
                "Sleep Until Evening", "Sleep Until Healed",
            };
            float width = _sleepPanel.sizeDelta.x;
            for (int index = 0; index < fallback.Length; index++)
            {
                string label = SourceMessage("mes/sleepui.mes", index, fallback[index]);
                _sleepLabels[index] = AddText($"Sleep Label {index}", _sleepPanel, new Rect(36f, 2f + 18f * index,
                    Mathf.Max(1f, width - 36f), 18f), label, 229);
                int captured = index;
                _sleepButtons[index] = AddButton($"Sleep Choice {index}", _sleepPanel, 293, 8f,
                    3f + 18f * index, () => _controller.Sleep((SleepOption)captured), hasPressedFrame: false);
                _sleepRows[index] = AddChoiceHitArea($"Sleep Row {index}", _sleepPanel,
                    new Rect(5f, 2f + 18f * index, Mathf.Max(1f, width - 8f), 18f),
                    () => _controller.Sleep((SleepOption)captured));
            }
            _sleepPanel.gameObject.SetActive(false);
        }

        private void BuildSkillsPanel()
        {
            _skillsPanel = AddSourcePanel("Skills Rotating Window", _bottomWindow,
                RetailGameplayHudLayout.SkillsWindowSourceId, 196f, 492f - 441f);
            int[] sourceIds = { 279, 280, 282, 278 };
            float[] sourceX = { 288f, 351f, 414f, 477f };
            for (int index = 0; index < sourceIds.Length; index++)
            {
                int captured = index;
                _skillActionButtons[index] = AddButton($"Source Skill Action {index}", _skillsPanel,
                    sourceIds[index], sourceX[index] - 196f, 514f - 492f,
                    () => _controller.ActivateSourceSkillAction(captured), hasPressedFrame: false);
            }
            _skillsPanel.gameObject.SetActive(false);
        }

        private void RefreshSleepPanel()
        {
            bool wait = _controller.ActiveSleepMode == SleepLocationMode.TownWait;
            string[] sleepFallback =
            {
                "Sleep For One Hour", "Sleep For Two Hours", "Sleep For Four Hours",
                "Sleep For Eight Hours", "Sleep For One Day", "Sleep Until Morning",
                "Sleep Until Evening", "Sleep Until Healed",
            };
            string[] waitFallback =
            {
                "Wait For One Hour", "Wait For Two Hours", "Wait For Four Hours",
                "Wait For Eight Hours", "Wait For One Day", "Wait Until Morning",
                "Wait Until Evening",
            };
            for (int index = 0; index < _sleepLabels.Length; index++)
            {
                bool active = !wait || index < waitFallback.Length;
                _sleepLabels[index].gameObject.SetActive(active);
                _sleepButtons[index].gameObject.SetActive(active);
                _sleepRows[index].gameObject.SetActive(active);
                if (!active) continue;
                string fallback = wait ? waitFallback[index] : sleepFallback[index];
                int message = wait ? index + 12 : index;
                SetText(_sleepLabels[index], SourceMessage("mes/sleepui.mes", message, fallback), SourceText);
            }
        }

        private RectTransform AddSourcePanel(string name, RectTransform parent, int sourceId, float x, float y)
        {
            Vector2 size = ResolveLogicalSize(new UiAssetKey(sourceId));
            RectTransform panel = AddRect(name, parent, new Rect(x, y, size.x, size.y));
            AddImage(name + " Background", panel, new UiAssetKey(sourceId),
                new Rect(0f, 0f, size.x, size.y), false);
            return panel;
        }

        private string SourceMessage(string path, int key, string fallback)
            => _resolver.Original.TryReadMessage(path, key, out string value)
                && !string.IsNullOrWhiteSpace(value) ? value : fallback;

        private void BindFeedbackHelp(SourceUiButton button, int messageKey)
        {
            string owner = $"mes/intgame.mes:{messageKey}";
            button.gameObject.AddComponent<SourceHoverHintTarget>().Bind(
                () => _controller.ShowContextualHelp(owner,
                    SourceMessage("mes/intgame.mes", messageKey, string.Empty)),
                () => _controller.ClearContextualHelp(owner));
        }

        private void ShowRecentActionContextualHelp(int index)
        {
            if (LastProjection?.RecentActions == null || index < 0
                || index >= LastProjection.RecentActions.Count)
                return;

            RecentActionBinding binding = LastProjection.RecentActions[index];
            int titleKey;
            int bodyKey;
            if (binding.Kind != RecentActionKind.Skill)
                return;
            switch (binding.SourceId)
            {
                case 0: // hotkey_ui_init source slot: Prowling.
                    titleKey = 6;
                    bodyKey = 26;
                    break;
                case 1: // hotkey_ui_init source slot: Pick Pocket.
                    titleKey = 5;
                    bodyKey = 25;
                    break;
                default:
                    return;
            }

            string owner = $"mes/skill.mes:{titleKey}+{bodyKey}";
            _recentActionHelpOwners[index] = owner;
            _controller.ShowContextualHelp(owner,
                SourceMessage("mes/skill.mes", bodyKey, string.Empty),
                SourceMessage("mes/skill.mes", titleKey, string.Empty));
        }

        private void ClearRecentActionContextualHelp(int index)
        {
            if (index < 0 || index >= _recentActionHelpOwners.Length) return;
            string owner = _recentActionHelpOwners[index];
            _recentActionHelpOwners[index] = null;
            if (!string.IsNullOrEmpty(owner)) _controller.ClearContextualHelp(owner);
        }

        private void ShowClockContextualHelp()
        {
            var timestamp = SourceCalendar.Timestamp(_session.SourceTime.CampaignMilliseconds);
            string timeLabel = SourceMessage("mes/intgame.mes", 22, string.Empty);
            string dateLabel = SourceMessage("mes/intgame.mes", 23, string.Empty);
            _controller.ShowContextualHelp(ClockHelpKey,
                $"{timeLabel}: {SourceCalendar.FormatHudTime(timestamp)}   "
                + $"{dateLabel}: {SourceCalendar.FormatHudDate(timestamp)}");
        }

        private void RefreshContextualHelp()
        {
            bool titled = !string.IsNullOrWhiteSpace(_controller.ContextualHelpTitle);
            _contextualHelpTitle.gameObject.SetActive(titled);
            SetText(_contextualHelpTitle, titled ? _controller.ContextualHelpTitle : string.Empty,
                ContextualHelpTextColor);
            Rect sourceRect = titled
                ? RetailGameplayHudLayout.ContextualHelpBody
                : RetailGameplayHudLayout.ContextualHelpText;
            int maximumLines = titled ? 3 : 4;
            IReadOnlyList<string> lines = WrapSourceText(_controller.ContextualHelp,
                RetailGameplayHudLayout.ContextualHelpFontSourceId, sourceRect.width, maximumLines);
            for (int index = 0; index < _contextualHelpLines.Length; index++)
            {
                SourceUiBitmapText line = _contextualHelpLines[index];
                bool active = index < lines.Count;
                line.gameObject.SetActive(active);
                if (!active) continue;
                Place((RectTransform)line.transform, RetailGameplayHudLayout.BottomWindowLocal(new Rect(
                    sourceRect.x, sourceRect.y + RetailGameplayHudLayout.ContextualHelpLineHeight * index,
                    sourceRect.width, RetailGameplayHudLayout.ContextualHelpLineHeight)));
                SetText(line, lines[index], ContextualHelpTextColor);
            }
        }

        private void HideContextualHelp()
        {
            _contextualHelpTitle.gameObject.SetActive(false);
            foreach (SourceUiBitmapText line in _contextualHelpLines)
                line.gameObject.SetActive(false);
        }

        private IReadOnlyList<string> WrapSourceText(string value, int fontSourceId, float maximumWidth,
            int maximumLines)
        {
            var lines = new List<string>();
            value ??= string.Empty;
            int start = 0;
            while (start < value.Length && lines.Count < maximumLines)
            {
                while (start < value.Length && value[start] == ' ') start++;
                if (start >= value.Length) break;
                int cursor = start;
                int lastSpace = -1;
                float width = 0f;
                while (cursor < value.Length)
                {
                    char character = value[cursor];
                    if (character is '\r' or '\n') break;
                    float advance = SourceGlyphAdvance(fontSourceId, character);
                    if (cursor > start && width + advance > maximumWidth) break;
                    width += advance;
                    if (character == ' ') lastSpace = cursor;
                    cursor++;
                }
                int end = cursor;
                if (cursor < value.Length && value[cursor] is not ('\r' or '\n') && lastSpace >= start)
                    end = lastSpace;
                string line = value.Substring(start, Math.Max(1, end - start)).TrimEnd();
                lines.Add(line);
                start = Math.Max(start + 1, end);
                while (start < value.Length && value[start] is ' ' or '\r' or '\n') start++;
            }
            if (lines.Count == 0) lines.Add(string.Empty);
            return lines;
        }

        private float SourceGlyphAdvance(int fontSourceId, char character)
        {
            int frame = (byte)character - 31;
            if (frame >= 0 && _resolver.TryResolve(new UiAssetKey(fontSourceId, frame: frame),
                    out UiResolvedAsset asset))
                return asset.Hotspot.x > 0 ? asset.Hotspot.x : asset.LogicalSize.x;
            return 0f;
        }

        private static Button AddChoiceHitArea(string name, RectTransform parent, Rect rect, Action clicked)
        {
            RectTransform target = AddRect(name, parent, rect);
            Image image = target.gameObject.AddComponent<Image>();
            image.color = Color.clear;
            Button button = target.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => clicked());
            return button;
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
            Place(rect, new Rect(RetailGameplayHudLayout.QuickSlotX[index], 445f - 441f, size.x, size.y));
            go.GetComponent<SourceUiImage>().Bind(_resolver, new UiAssetKey(sourceId));
            Button button = go.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            int captured = index;
            button.onClick.AddListener(() =>
            {
                if (_controller.Screen == GameUiScreen.Inventory && !_controller.SelectedItem.IsNull)
                    _controller.AssignQuickSlotItem(captured, _controller.SelectedItem);
                else
                    _controller.ActivateQuickSlot(captured);
            });
            _slotLabels[index] = AddText("Binding", rect, new Rect(5f, 5f, size.x - 6f, size.y - 6f),
                string.Empty, RetailGameplayHudLayout.TextFontSourceId);
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

        private void RefreshMaintainedSpellSlots(int capacity, IReadOnlyList<ActiveSpellEffect> effects)
        {
            capacity = Mathf.Clamp(capacity, 0, _maintainedSpellSlots.Length);
            for (int index = 0; index < _maintainedSpellSlots.Length; index++)
            {
                SpellDefinition spell = null;
                bool active = effects != null && index < effects.Count
                              && PhaseOneSpellCatalog.TryGet(effects[index].SpellId, out spell);
                bool open = index < capacity;
                SourceUiImage slot = _maintainedSpellSlots[index];
                slot.SetKey(new UiAssetKey(active ? spell.IconSourceId : open
                    ? RetailGameplayHudLayout.MaintainedSpellOpenSourceIds[index]
                    : RetailGameplayHudLayout.MaintainedSpellPluggedSourceIds[index]));
                _maintainedSpellButtons[index].interactable = active;
                Place((RectTransform)slot.transform, active || open
                    ? new Rect(281f + 50f * index, 3f, 32f, 32f)
                    : new Rect(280f + 50f * index, 2f, 35f, 35f));
            }
        }

        private void CancelMaintainedSpell(int index)
        {
            if (LastProjection?.ActiveEffects == null || index < 0 || index >= LastProjection.ActiveEffects.Count)
                return;
            _controller.CancelEffect(LastProjection.ActiveEffects[index].Id);
        }

        private void RefreshPrimaryButtons()
        {
            HudPrimaryNotification notifications = LastProjection.PrimaryNotifications;
            BindButtonPresentation(_characterButton,
                (notifications & HudPrimaryNotification.Character) != 0 ? 561 : 169);
            BindButtonPresentation(_logbookButton,
                (notifications & HudPrimaryNotification.Logbook) != 0 ? 560 : 187);
            bool mapHighlighted = LastProjection.UsesWorldMapButton
                ? (notifications & HudPrimaryNotification.WorldMap) != 0
                : (notifications & HudPrimaryNotification.TownMap) != 0;
            BindButtonPresentation(_mapButton, LastProjection.UsesWorldMapButton
                ? mapHighlighted ? 195 : 194
                : mapHighlighted ? 558 : 193);
            BindButtonPresentation(_inventoryButton,
                (notifications & HudPrimaryNotification.Inventory) != 0 ? 559 : 186);
        }

        private void BindButtonPresentation(SourceUiButton button, int sourceId)
        {
            if (button == null) return;
            button.Bind(_resolver, new UiAssetKey(sourceId, frame: 0),
                new UiAssetKey(sourceId, frame: 1), new UiAssetKey(sourceId, frame: 2),
                new UiAssetKey(sourceId, frame: 0), new UiAssetKey(sourceId, frame: 1));
        }

        private void RefreshClock()
        {
            long sourceSeconds = SourceTimeService.InitialHour * 3600L
                                 + LastProjection.SourceTimeMilliseconds / 1000L;
            int hour = (int)((sourceSeconds / 3600L) % 24L);
            ClockPeriod = hour switch
            {
                >= 6 and < 12 => RetailClockPeriod.Morning,
                >= 12 and < 17 => RetailClockPeriod.Midday,
                >= 17 and < 21 => RetailClockPeriod.Evening,
                _ => RetailClockPeriod.Night,
            };
            int timeWidth = Mathf.Max(1, Mathf.RoundToInt(ResolveLogicalSize(new UiAssetKey(207)).x));
            int moonWidth = Mathf.Max(1, Mathf.RoundToInt(ResolveLogicalSize(new UiAssetKey(208)).x));
            int cycleWidth = timeWidth + moonWidth;
            int offset = (int)((cycleWidth + (sourceSeconds + 73800L) % 86400L * cycleWidth / 86400L
                                - RetailGameplayHudLayout.ClockFrame.width / 2f) % cycleWidth);
            int moonDay = (int)(((sourceSeconds + 43200L) / 84600L) % 28L);
            int[] thresholds = { 0, 4, 9, 13, 14, 18, 23, 27 };
            int moon = 0;
            while (moon < thresholds.Length - 1 && moonDay > thresholds[moon]) moon++;
            float x = -offset;
            for (int index = 0; index < _clockBands.Length; index++)
            {
                bool time = index % 2 == 0;
                int sourceId = time ? 207 : 208 + moon;
                float width = time ? timeWidth : moonWidth;
                _clockBands[index].SetKey(new UiAssetKey(sourceId));
                Place((RectTransform)_clockBands[index].transform, new Rect(x, 0f, width, 30f));
                x += width;
            }
        }

        private void RefreshExperienceGauge()
        {
            int value = Mathf.Clamp(LastProjection.ExperienceGaugeValue, 0, 1099);
            int full = Mathf.Min(_experienceSegments.Length, value / 100);
            for (int index = 0; index < _experienceSegments.Length; index++)
                _experienceSegments[index].gameObject.SetActive(index < full);
            int partial = value % 100;
            _experiencePartial.gameObject.SetActive(partial > 0);
            _experiencePartial.Image.type = Image.Type.Filled;
            _experiencePartial.Image.fillMethod = Image.FillMethod.Horizontal;
            _experiencePartial.Image.fillOrigin = (int)Image.OriginHorizontal.Left;
            _experiencePartial.Image.fillAmount = partial / 100f;
        }

        private void RefreshRecentActions()
        {
            if (LastProjection.RecentActions == null) return;
            for (int index = 0; index < _recentActionImages.Length; index++)
            {
                RecentActionBinding binding = LastProjection.RecentActions[index];
                SourceUiImage image = _recentActionImages[index];
                RetailItemArtAsset resolved = null;
                bool itemArt = binding.Kind == RecentActionKind.Item
                               && _session.GameplayHud.TryResolveRecentItem(index, out ArcanumObjectId identity)
                               && _session.TryGetObjectState(identity, out PersistentObjectState state)
                               && state.InventoryArtId.HasValue
                               && _resolver.Original.TryResolveInventoryItem(state.InventoryArtId.Value, out resolved);
                if (itemArt)
                {
                    image.enabled = false;
                    image.Image.sprite = resolved.Sprite;
                    image.Image.color = Color.white;
                    image.Image.preserveAspect = true;
                }
                else
                {
                    image.enabled = true;
                    image.SetKey(new UiAssetKey(binding.IconSourceId));
                    image.Image.preserveAspect = true;
                }
            }
        }

        private void BuildFollowerSlots()
        {
            for (int index = 0; index < RetailGameplayHudLayout.FollowerSlotCount; index++)
            {
                RectTransform slot = AddRect($"Follower Slot {index + 1}", _followerLayer,
                    RetailGameplayHudLayout.FollowerSlot(index));
                _followerSlots[index] = slot;
                AddImage("Frame", slot, new UiAssetKey(RetailGameplayHudLayout.FollowerFrameSourceId),
                    new Rect(0f, 0f, 40f, 49f), false);

                SourceUiImage fallback = AddImage("Fallback Portrait", slot, new UiAssetKey(375),
                    RetailGameplayHudLayout.FollowerPortrait, false);
                fallback.Image.preserveAspect = true;
                _followerFallbackIcons[index] = fallback;

                RectTransform portraitRect = AddRect("Portrait", slot, RetailGameplayHudLayout.FollowerPortrait);
                Image portrait = portraitRect.gameObject.AddComponent<Image>();
                portrait.preserveAspect = true;
                portrait.raycastTarget = false;
                _followerPortraits[index] = portrait;

                _followerHealthBars[index] = AddFollowerBar("Health", slot,
                    RetailGameplayHudLayout.FollowerHealthSourceId, RetailGameplayHudLayout.FollowerHealth);
                _followerFatigueBars[index] = AddFollowerBar("Fatigue", slot,
                    RetailGameplayHudLayout.FollowerFatigueSourceId, RetailGameplayHudLayout.FollowerFatigue);

                RectTransform tintRect = AddRect("Low Health Tint", slot, RetailGameplayHudLayout.FollowerPortrait);
                Image tint = tintRect.gameObject.AddComponent<Image>();
                tint.color = new Color(1f, 0f, 0f, .35f);
                tint.raycastTarget = false;
                _followerDangerTints[index] = tint;
                slot.gameObject.SetActive(false);
            }
        }

        private SourceUiImage AddFollowerBar(string name, RectTransform slot, int sourceId, Rect rect)
        {
            SourceUiImage bar = AddImage(name, slot, new UiAssetKey(sourceId), rect, false);
            bar.Image.type = Image.Type.Filled;
            bar.Image.fillMethod = Image.FillMethod.Horizontal;
            bar.Image.fillOrigin = (int)Image.OriginHorizontal.Left;
            return bar;
        }

        private void RefreshFollowerSlots()
        {
            IReadOnlyList<GameUiPartyMemberView> members = _controller.ProjectParty();
            for (int index = 0; index < _followerSlots.Length; index++)
            {
                bool active = index < members.Count;
                _followerSlots[index].gameObject.SetActive(active);
                if (!active) continue;
                GameUiPartyMemberView member = members[index];
                SourceUiImage health = _followerHealthBars[index];
                health.SetKey(new UiAssetKey(member.Poisoned
                    ? RetailGameplayHudLayout.FollowerPoisonSourceId
                    : RetailGameplayHudLayout.FollowerHealthSourceId));
                health.Image.type = Image.Type.Filled;
                health.Image.fillMethod = Image.FillMethod.Horizontal;
                health.Image.fillOrigin = (int)Image.OriginHorizontal.Left;
                health.Image.fillAmount = RetailGameplayHudLayout.FollowerVisiblePixels(member.HealthPercent) / 34f;
                _followerFatigueBars[index].Image.fillAmount =
                    RetailGameplayHudLayout.FollowerVisiblePixels(member.FatiguePercent) / 34f;
                _followerDangerTints[index].gameObject.SetActive(member.LowHealth);

                Image portrait = _followerPortraits[index];
                RetailPortraitAsset portraitAsset = null;
                bool resolved = member.PortraitId > 0
                                && _resolver.Original.TryResolvePortrait(member.PortraitId,
                                    out portraitAsset);
                portrait.gameObject.SetActive(resolved);
                portrait.sprite = resolved ? portraitAsset.Sprite : null;
                _followerFallbackIcons[index].gameObject.SetActive(!resolved);
                if (!resolved)
                    _followerFallbackIcons[index].SetKey(new UiAssetKey(member.FallbackIconSourceId));
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

        private SourceUiBitmapText AddCounter(string name, RectTransform parent, Rect sourceRect)
        {
            RectTransform frame = AddRect(name, parent, sourceRect);
            Image backing = frame.gameObject.AddComponent<Image>();
            backing.color = Color.black;
            backing.raycastTarget = false;
            RectTransform textRect = AddRect(name + " Text", frame, new Rect(0f, 0f, 0f, 0f));
            var bitmap = textRect.gameObject.AddComponent<SourceUiBitmapText>();
            bitmap.Bind(_resolver, string.Empty, SourceText, RetailGameplayHudLayout.CounterFontSourceId);
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

        private static RectTransform AddWindow(string name, RectTransform parent, bool top)
        {
            var go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, worldPositionStays: false);
            rect.anchorMin = rect.anchorMax = top ? new Vector2(.5f, 1f) : new Vector2(.5f, 0f);
            rect.pivot = top ? new Vector2(.5f, 1f) : new Vector2(.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = top ? RetailGameplayHudLayout.TopInterface.size : RetailGameplayHudLayout.BottomInterface.size;
            return rect;
        }

        private static RectTransform AddFullScreenLayer(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, worldPositionStays: false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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
                    _originalCameraOrthographicSize = camera.orthographicSize;
                    _managingViewport = true;
                }
                camera.rect = RetailGameplayHudLayout.GameplayCameraViewport(Screen.width, Screen.height);
                if (camera.orthographic)
                {
                    WorldObjectSectorLoader loader = _session.GetComponent<WorldObjectSectorLoader>();
                    float pixelsPerUnit = loader != null ? loader.PixelsPerUnit : 100f;
                    camera.orthographicSize = RetailGameplayHudLayout.GameplayCameraOrthographicSize(
                        Screen.height, pixelsPerUnit);
                }
            }
            else RestoreWorldViewport();
        }

        private void RestoreWorldViewport()
        {
            if (_managingViewport && _managedCamera != null)
            {
                _managedCamera.rect = _originalCameraRect;
                if (_managedCamera.orthographic) _managedCamera.orthographicSize = _originalCameraOrthographicSize;
            }
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

        private void SetCounter(SourceUiBitmapText text, int value, int digits = 3)
        {
            digits = Mathf.Max(1, digits);
            int maximum = 1;
            for (int index = 0; index < digits; index++) maximum *= 10;
            string format = "D" + digits;
            SetText(text, Mathf.Clamp(value, 0, maximum - 1).ToString(format), SourceText);
            RectTransform rect = (RectTransform)text.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            // Source centers the measured Cloister 18 bitmap string inside the exact counter box.
            rect.sizeDelta = text.LogicalSize;
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
            _topWindow = null;
            _bottomWindow = null;
            _followerLayer = null;
            _cursorLayer = null;
            _topBackground = null;
            _bottomBackground = null;
            _healthLiquid = null;
            _fatigueLiquid = null;
            _healthText = null;
            _fatigueText = null;
            _fateText = null;
            _contextText = null;
            _messageLineOne = null;
            _messageLineTwo = null;
            _contextualHelpTitle = null;
            Array.Clear(_contextualHelpLines, 0, _contextualHelpLines.Length);
            _ammoImage = null;
            _clockClip = null;
            _clockPointer = null;
            _characterButton = null;
            _logbookButton = null;
            _mapButton = null;
            _inventoryButton = null;
            _fateButton = null;
            _sleepButton = null;
            _fatePanel = null;
            _sleepPanel = null;
            _skillsPanel = null;
            Array.Clear(_skillActionButtons, 0, _skillActionButtons.Length);
            Array.Clear(_recentActionHelpOwners, 0, _recentActionHelpOwners.Length);
            _combatButton = null;
            Array.Clear(_followerSlots, 0, _followerSlots.Length);
            Array.Clear(_followerPortraits, 0, _followerPortraits.Length);
            Array.Clear(_followerFallbackIcons, 0, _followerFallbackIcons.Length);
            Array.Clear(_followerHealthBars, 0, _followerHealthBars.Length);
            Array.Clear(_followerFatigueBars, 0, _followerFatigueBars.Length);
            Array.Clear(_followerDangerTints, 0, _followerDangerTints.Length);
            Array.Clear(_maintainedSpellSlots, 0, _maintainedSpellSlots.Length);
            Array.Clear(_maintainedSpellButtons, 0, _maintainedSpellButtons.Length);
            Array.Clear(_recentActionImages, 0, _recentActionImages.Length);
            Array.Clear(_recentActionButtons, 0, _recentActionButtons.Length);
            Array.Clear(_experienceSegments, 0, _experienceSegments.Length);
            Array.Clear(_clockBands, 0, _clockBands.Length);
            _experiencePartial = null;
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
