using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Runtime.Save;
using OpenArcanum.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Arcanum.Runtime.UI
{
    public enum RetailMainMenuState
    {
        TopLevel,
        SinglePlayer,
        NewGameChoice,
        Notice,
        QuitConfirmation,
    }

    public enum RetailMainMenuCommand
    {
        SinglePlayer,
        Multiplayer,
        Options,
        Credits,
        ExitGame,
        NewGame,
        LoadGame,
        LastSave,
        ViewIntro,
        PickCharacter,
        NewCharacter,
        Back,
        ConfirmQuit,
        CancelQuit,
    }

    public readonly struct RetailMainMenuEntry
    {
        public RetailMainMenuCommand Command { get; }
        public string Label { get; }
        public int SourceMessageId { get; }

        public RetailMainMenuEntry(RetailMainMenuCommand command, string label, int sourceMessageId = -1)
        {
            Command = command;
            Label = label ?? string.Empty;
            SourceMessageId = sourceMessageId;
        }
    }

    /// <summary>Source labels and IDs for UI-B. The mounted retail text table stays authoritative where present.</summary>
    public sealed class RetailMainMenuSource
    {
        public const string MessageTable = "mes/mainmenu.mes";
        private readonly Dictionary<int, string> _messages;

        private RetailMainMenuSource(Dictionary<int, string> messages) => _messages = messages;

        public static RetailMainMenuSource Read(UiSkinResolver resolver)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            var messages = new Dictionary<int, string>();
            foreach (int id in new[] { 50, 51, 52, 53, 54, 420, 421, 422, 460, 461, 462, 463, 5100 })
                if (resolver.Original.TryReadMessage(MessageTable, id, out string value)) messages[id] = value;
            return new RetailMainMenuSource(messages);
        }

        public string Get(int id, string fallback)
            => _messages.TryGetValue(id, out string value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

        public bool IsSourceBacked(int id) => _messages.ContainsKey(id);
    }

    /// <summary>
    /// Presentation-only retail menu state. It submits existing controller commands and never owns session state.
    /// Unsupported retail branches remain visible and fail closed as bounded notices.
    /// </summary>
    public sealed class RetailMainMenuModel
    {
        private readonly GameUiController _controller;
        private readonly Action _quit;
        private readonly RetailMainMenuSource _source;
        private readonly List<RetailMainMenuEntry> _entries = new List<RetailMainMenuEntry>();

        public event Action Changed;
        public RetailMainMenuState State { get; private set; }
        public IReadOnlyList<RetailMainMenuEntry> Entries => _entries;
        public string NoticeTitle { get; private set; } = string.Empty;
        public string NoticeMessage { get; private set; } = string.Empty;
        public int BackgroundSourceId => State == RetailMainMenuState.SinglePlayer ? 331 : 329;

        public RetailMainMenuModel(GameUiController controller, RetailMainMenuSource source, Action quit)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _quit = quit ?? (() => { });
            ShowTopLevel();
        }

        public void Reset()
        {
            ShowTopLevel();
            Changed?.Invoke();
        }

        public void Submit(RetailMainMenuCommand command)
        {
            switch (command)
            {
                case RetailMainMenuCommand.SinglePlayer:
                    SetState(RetailMainMenuState.SinglePlayer);
                    break;
                case RetailMainMenuCommand.Multiplayer:
                    ShowNotice("MULTIPLAYER", "MULTIPLAYER IS NOT IMPLEMENTED IN THIS RUNTIME");
                    break;
                case RetailMainMenuCommand.Options:
                    _controller.Open(GameUiScreen.Options);
                    break;
                case RetailMainMenuCommand.Credits:
                    ShowNotice("CREDITS", "RETAIL CREDITS PRESENTATION IS NOT YET AVAILABLE");
                    break;
                case RetailMainMenuCommand.ExitGame:
                    SetState(RetailMainMenuState.QuitConfirmation);
                    break;
                case RetailMainMenuCommand.NewGame:
                    SetState(RetailMainMenuState.NewGameChoice);
                    break;
                case RetailMainMenuCommand.LoadGame:
                case RetailMainMenuCommand.LastSave:
                    _controller.OpenSaveLoad(SaveLoadPanelMode.Load);
                    break;
                case RetailMainMenuCommand.ViewIntro:
                    ShowNotice("VIEW INTRO", "RETAIL INTRO MOVIES ARE NOT YET MAPPED");
                    break;
                case RetailMainMenuCommand.PickCharacter:
                    ShowNotice("PICK CHARACTER", "PREGENERATED CHARACTER SELECTION IS NOT YET AVAILABLE");
                    break;
                case RetailMainMenuCommand.NewCharacter:
                    _controller.BeginNewGame();
                    break;
                case RetailMainMenuCommand.Back:
                    ShowTopLevel();
                    break;
                case RetailMainMenuCommand.ConfirmQuit:
                    _quit();
                    break;
                case RetailMainMenuCommand.CancelQuit:
                    ShowTopLevel();
                    break;
            }
            Changed?.Invoke();
        }

        public void Escape()
        {
            if (State == RetailMainMenuState.TopLevel) return;
            ShowTopLevel();
            Changed?.Invoke();
        }

        private void ShowTopLevel()
        {
            State = RetailMainMenuState.TopLevel;
            NoticeTitle = NoticeMessage = string.Empty;
            _entries.Clear();
            _entries.Add(new RetailMainMenuEntry(RetailMainMenuCommand.SinglePlayer,
                _source.Get(460, "Single Player"), 460));
            // The mounted single-player distribution omits this row; the supplied original retail reference proves it.
            _entries.Add(new RetailMainMenuEntry(RetailMainMenuCommand.Multiplayer, "Multiplayer"));
            _entries.Add(new RetailMainMenuEntry(RetailMainMenuCommand.Options,
                _source.Get(461, "Options"), 461));
            _entries.Add(new RetailMainMenuEntry(RetailMainMenuCommand.Credits,
                _source.Get(462, "Credits"), 462));
            _entries.Add(new RetailMainMenuEntry(RetailMainMenuCommand.ExitGame,
                _source.Get(463, "Exit Game"), 463));
        }

        private void SetState(RetailMainMenuState state)
        {
            State = state;
            NoticeTitle = NoticeMessage = string.Empty;
            _entries.Clear();
            if (state == RetailMainMenuState.SinglePlayer)
            {
                _entries.Add(new RetailMainMenuEntry(RetailMainMenuCommand.NewGame,
                    _source.Get(50, "New Game"), 50));
                _entries.Add(new RetailMainMenuEntry(RetailMainMenuCommand.LoadGame,
                    _source.Get(51, "Load Game"), 51));
                _entries.Add(new RetailMainMenuEntry(RetailMainMenuCommand.LastSave,
                    _source.Get(52, "Last Save"), 52));
                _entries.Add(new RetailMainMenuEntry(RetailMainMenuCommand.ViewIntro,
                    _source.Get(53, "View Intro"), 53));
                _entries.Add(new RetailMainMenuEntry(RetailMainMenuCommand.Back,
                    _source.Get(54, "Cancel"), 54));
            }
            else if (state == RetailMainMenuState.NewGameChoice)
            {
                _entries.Add(new RetailMainMenuEntry(RetailMainMenuCommand.PickCharacter,
                    _source.Get(420, "Pick Character"), 420));
                _entries.Add(new RetailMainMenuEntry(RetailMainMenuCommand.NewCharacter,
                    _source.Get(421, "New Character"), 421));
                _entries.Add(new RetailMainMenuEntry(RetailMainMenuCommand.Back,
                    _source.Get(422, "Cancel"), 422));
            }
            else if (state == RetailMainMenuState.QuitConfirmation)
            {
                NoticeTitle = _source.Get(5100, "Are you sure you want to quit?").ToUpperInvariant();
                _entries.Add(new RetailMainMenuEntry(RetailMainMenuCommand.ConfirmQuit, "Yes"));
                _entries.Add(new RetailMainMenuEntry(RetailMainMenuCommand.CancelQuit, "No"));
            }
        }

        private void ShowNotice(string title, string message)
        {
            State = RetailMainMenuState.Notice;
            NoticeTitle = title;
            NoticeMessage = message;
            _entries.Clear();
            _entries.Add(new RetailMainMenuEntry(RetailMainMenuCommand.Back, "Back"));
        }
    }

    public enum RetailMainMenuButtonState { Normal, Hover, Pressed, Selected }

    /// <summary>
    /// Source cursor rendered in the same logical Canvas as the retail composition. Runtime-decoded ART textures
    /// cannot satisfy every platform's native-cursor importer requirements, so this preserves the retail pixels and
    /// hotspot without producing platform cursor warnings or changing pointer hit testing.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(Image), typeof(SourceUiImage))]
    public sealed class RetailMainMenuCursorView : MonoBehaviour
    {
        private RectTransform _canvas;
        private RectTransform _rect;
        private SourceUiImage _source;

        public UiResolvedAsset CurrentAsset => _source != null ? _source.CurrentAsset : null;
        public Vector2 Hotspot { get; private set; }

        public void Bind(RectTransform canvas, UiSkinResolver resolver)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            _rect = (RectTransform)transform;
            _source = GetComponent<SourceUiImage>();
            _source.Bind(resolver ?? throw new ArgumentNullException(nameof(resolver)), new UiAssetKey(0));
            UiResolvedAsset asset = _source.CurrentAsset;
            if (asset == null) return;

            Hotspot = asset.Hotspot;
            _rect.anchorMin = _rect.anchorMax = new Vector2(.5f, .5f);
            _rect.sizeDelta = asset.LogicalSize;
            _rect.pivot = new Vector2(
                asset.LogicalSize.x > 0 ? asset.Hotspot.x / asset.LogicalSize.x : 0f,
                asset.LogicalSize.y > 0 ? 1f - asset.Hotspot.y / asset.LogicalSize.y : 1f);
            GetComponent<Image>().raycastTarget = false;
        }

        private void OnEnable()
        {
            if (Application.isPlaying) Cursor.visible = false;
        }

        private void OnDisable()
        {
            if (Application.isPlaying) Cursor.visible = true;
        }

        private void OnDestroy()
        {
            if (Application.isPlaying) Cursor.visible = true;
        }

        private void LateUpdate()
        {
            if (_canvas == null || _rect == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvas, Input.mousePosition, null, out Vector2 local))
                _rect.anchoredPosition = local;
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(Image), typeof(Button))]
    public sealed class RetailMainMenuButtonView : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        private static readonly Color PassBlack = new Color32(0, 0, 0, 255);
        private static readonly Color PassBrown = new Color32(97, 61, 42, 255);
        private static readonly Color NormalRed = new Color32(100, 0, 0, 255);
        private static readonly Color HighlightRed = new Color32(240, 15, 15, 255);

        private SourceUiBitmapText _foreground;
        private bool _inside;
        private bool _pressed;
        private bool _selected;

        public RetailMainMenuEntry Entry { get; private set; }
        public Rect SourceRect { get; private set; }
        public RetailMainMenuButtonState State { get; private set; }
        public Button Button { get; private set; }

        public void Bind(UiSkinResolver resolver, RetailMainMenuEntry entry, float centerX, float sourceY,
            Action<RetailMainMenuCommand> command)
        {
            Entry = entry;
            Button = GetComponent<Button>();
            Button.transition = Selectable.Transition.None;
            Button.targetGraphic = GetComponent<Image>();
            Image hit = GetComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            Button.onClick.RemoveAllListeners();
            Button.onClick.AddListener(() => command(entry.Command));

            SourceUiBitmapText black = CreatePass("Black Pass", resolver, entry.Label, PassBlack, new Vector2(-1f, 1f));
            SourceUiBitmapText brown = CreatePass("Brown Pass", resolver, entry.Label, PassBrown, new Vector2(1f, -1f));
            _foreground = CreatePass("Red Pass", resolver, entry.Label, NormalRed, Vector2.zero);
            Vector2 size = new Vector2(
                Mathf.Max(black.LogicalSize.x, brown.LogicalSize.x, _foreground.LogicalSize.x),
                Mathf.Max(black.LogicalSize.y, brown.LogicalSize.y, _foreground.LogicalSize.y));

            RectTransform rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = new Vector2(centerX, -sourceY);
            rect.sizeDelta = size;
            SourceRect = new Rect(centerX - size.x * .5f, sourceY, size.x, size.y);
            SetState(RetailMainMenuButtonState.Normal);
        }

        public void Invoke() => Button?.onClick.Invoke();

        public void OnPointerEnter(PointerEventData eventData) { _inside = true; RefreshState(); }
        public void OnPointerExit(PointerEventData eventData) { _inside = _pressed = false; RefreshState(); }
        public void OnPointerDown(PointerEventData eventData) { _pressed = true; RefreshState(); }
        public void OnPointerUp(PointerEventData eventData) { _pressed = false; RefreshState(); }
        public void OnSelect(BaseEventData eventData) { _selected = true; RefreshState(); }
        public void OnDeselect(BaseEventData eventData) { _selected = false; RefreshState(); }

        private SourceUiBitmapText CreatePass(string name, UiSkinResolver resolver, string text, Color tint,
            Vector2 offset)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(SourceUiBitmapText));
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(transform, worldPositionStays: false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = offset;
            SourceUiBitmapText bitmap = child.GetComponent<SourceUiBitmapText>();
            bitmap.Bind(resolver, text.ToUpperInvariant(), tint);
            return bitmap;
        }

        private void RefreshState()
        {
            if (_pressed) SetState(RetailMainMenuButtonState.Pressed);
            else if (_selected) SetState(RetailMainMenuButtonState.Selected);
            else if (_inside) SetState(RetailMainMenuButtonState.Hover);
            else SetState(RetailMainMenuButtonState.Normal);
        }

        private void SetState(RetailMainMenuButtonState state)
        {
            State = state;
            _foreground?.SetTint(state == RetailMainMenuButtonState.Normal ? NormalRed : HighlightRed);
        }
    }

    /// <summary>One source-backed Main Menu view over the existing production controller.</summary>
    [DisallowMultipleComponent]
    public sealed class RetailMainMenuView : MonoBehaviour
    {
        private const float ButtonCenterX = 410f;
        private static readonly float[] ButtonRows = { 143f, 193f, 243f, 293f, 343f };

        private GameUiController _controller;
        private UiSkinResolver _resolver;
        private bool _ownsResolver;
        private GameObject _canvasRoot;
        private SourceUiPresentationRoot _presentation;
        private SourceUiImage _background;
        private RectTransform _menuLayer;
        private readonly List<RetailMainMenuButtonView> _buttons = new List<RetailMainMenuButtonView>();
        private bool _lastVisible;

        public RetailMainMenuModel Model { get; private set; }
        public IReadOnlyList<RetailMainMenuButtonView> Buttons => _buttons;
        public bool IsAvailable => _resolver != null && _canvasRoot != null;
        public bool IsVisible => IsAvailable && _canvasRoot.activeSelf;
        public UiResolvedAsset BackgroundAsset => _background != null ? _background.CurrentAsset : null;

        public void Bind(GameUiController controller, Action quit, UiSkinResolver resolver = null)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            // An explicit resolver is a validation seam for generated test-only Enhanced fixtures. Normal
            // production rebinding still remains idempotent.
            if (_controller == controller && IsAvailable && resolver == null) return;
            Cleanup();
            _controller = controller;
            try
            {
                _resolver = resolver ?? UiSkinResolver.CreateProduction(ReportProductionDiagnostic);
                _ownsResolver = resolver == null;
                Model = new RetailMainMenuModel(controller, RetailMainMenuSource.Read(_resolver), quit);
                Model.Changed += Rebuild;
                EnsurePresentation();
                Rebuild();
                Synchronize();
            }
            catch (Exception exception)
            {
                Debug.LogError("OpenArcanum UI-B Main Menu could not initialize: " + exception.Message);
                Cleanup();
            }
        }

        public void Synchronize()
        {
            if (!IsAvailable || _controller == null) return;
            bool visible = _controller.Screen == GameUiScreen.MainMenu;
            if (visible != _lastVisible)
            {
                _canvasRoot.SetActive(visible);
                _lastVisible = visible;
                if (visible) Model.Reset();
            }
        }

        public void HandleEscape()
        {
            if (IsVisible) Model.Escape();
        }

        private void Update()
        {
            Synchronize();
            if (!IsVisible) return;
            if (Input.GetKeyUp(KeyCode.Escape)) { Model.Escape(); return; }
            if (_buttons.Count == 0) return;
            if (Input.GetKeyDown(KeyCode.DownArrow)) SelectRelative(1);
            else if (Input.GetKeyDown(KeyCode.UpArrow)) SelectRelative(-1);
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                GameObject selected = EventSystem.current?.currentSelectedGameObject;
                RetailMainMenuButtonView view = selected?.GetComponent<RetailMainMenuButtonView>();
                if (view != null && _buttons.Contains(view)) view.Invoke();
            }
        }

        private void OnDestroy() => Cleanup();

        private void EnsurePresentation()
        {
            _canvasRoot = new GameObject("Source-Faithful Main Menu",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
                typeof(SourceUiPresentationRoot), typeof(SourceUiRuntime));
            _canvasRoot.transform.SetParent(transform, worldPositionStays: false);
            _presentation = _canvasRoot.GetComponent<SourceUiPresentationRoot>();
            _canvasRoot.GetComponent<SourceUiRuntime>().BindResolver(_resolver);
            _canvasRoot.GetComponent<Canvas>().sortingOrder = 1000;

            RectTransform modal = _presentation.GetLayer(SourceUiLayer.Modal);
            var background = new GameObject("Retail Main Menu Background",
                typeof(RectTransform), typeof(Image), typeof(SourceUiImage));
            RectTransform backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.SetParent(modal, worldPositionStays: false);
            Stretch(backgroundRect);
            _background = background.GetComponent<SourceUiImage>();

            var menu = new GameObject("Retail Main Menu Commands", typeof(RectTransform));
            _menuLayer = menu.GetComponent<RectTransform>();
            _menuLayer.SetParent(modal, worldPositionStays: false);
            Stretch(_menuLayer);

            var cursor = new GameObject("Retail Main Menu Cursor",
                typeof(RectTransform), typeof(Image), typeof(SourceUiImage), typeof(RetailMainMenuCursorView));
            cursor.transform.SetParent(_canvasRoot.transform, worldPositionStays: false);
            cursor.GetComponent<RetailMainMenuCursorView>().Bind((RectTransform)_canvasRoot.transform, _resolver);

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("OpenArcanum UI EventSystem",
                    typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(_canvasRoot.transform, worldPositionStays: false);
            }
        }

        private void Rebuild()
        {
            if (_menuLayer == null || Model == null) return;
            ClearMenuLayer();
            _background.Bind(_resolver, new UiAssetKey(Model.BackgroundSourceId));

            if (!string.IsNullOrEmpty(Model.NoticeTitle))
                AddCenteredText("Notice Title", Model.NoticeTitle, 200f, 327, new Color32(240, 15, 15, 255));
            if (!string.IsNullOrEmpty(Model.NoticeMessage))
                AddCenteredText("Notice Message", Model.NoticeMessage, 245f, 229,
                    new Color32(231, 217, 174, 255));

            int rowOffset = Model.State == RetailMainMenuState.Notice ? 4
                : Model.State == RetailMainMenuState.QuitConfirmation ? 3 : 0;
            for (int index = 0; index < Model.Entries.Count; index++)
            {
                int row = Mathf.Min(index + rowOffset, ButtonRows.Length - 1);
                RetailMainMenuEntry entry = Model.Entries[index];
                var go = new GameObject(entry.Command.ToString(), typeof(RectTransform), typeof(Image), typeof(Button),
                    typeof(RetailMainMenuButtonView));
                go.transform.SetParent(_menuLayer, worldPositionStays: false);
                RetailMainMenuButtonView view = go.GetComponent<RetailMainMenuButtonView>();
                view.Bind(_resolver, entry, ButtonCenterX, ButtonRows[row], Model.Submit);
                _buttons.Add(view);
            }
            ConfigureNavigation();
        }

        private void AddCenteredText(string name, string text, float sourceY, int fontSourceId, Color tint)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(SourceUiBitmapText));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(_menuLayer, worldPositionStays: false);
            SourceUiBitmapText bitmap = go.GetComponent<SourceUiBitmapText>();
            bitmap.Bind(_resolver, text, tint, fontSourceId);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = new Vector2(ButtonCenterX, -sourceY);
        }

        private void ConfigureNavigation()
        {
            for (int index = 0; index < _buttons.Count; index++)
            {
                Navigation navigation = new Navigation { mode = Navigation.Mode.Explicit };
                navigation.selectOnUp = _buttons[(index - 1 + _buttons.Count) % _buttons.Count].Button;
                navigation.selectOnDown = _buttons[(index + 1) % _buttons.Count].Button;
                _buttons[index].Button.navigation = navigation;
            }
        }

        private void SelectRelative(int direction)
        {
            int current = _buttons.FindIndex(value => value.gameObject == EventSystem.current?.currentSelectedGameObject);
            int next = current < 0 ? 0 : (current + direction + _buttons.Count) % _buttons.Count;
            EventSystem.current?.SetSelectedGameObject(_buttons[next].gameObject);
        }

        private void ClearMenuLayer()
        {
            _buttons.Clear();
            for (int index = _menuLayer.childCount - 1; index >= 0; index--)
            {
                GameObject child = _menuLayer.GetChild(index).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Object.Destroy(child); else Object.DestroyImmediate(child);
            }
        }

        private void Cleanup()
        {
            if (Model != null) Model.Changed -= Rebuild;
            Model = null;
            _buttons.Clear();
            if (_canvasRoot != null)
            {
                _canvasRoot.SetActive(false);
                if (Application.isPlaying) Object.Destroy(_canvasRoot); else Object.DestroyImmediate(_canvasRoot);
            }
            _canvasRoot = null;
            _presentation = null;
            _background = null;
            _menuLayer = null;
            if (_ownsResolver) _resolver?.Dispose();
            _resolver = null;
            _ownsResolver = false;
            _lastVisible = false;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void ReportProductionDiagnostic(string message)
        {
            // Incremental Enhanced coverage is expected: an absent replacement is a normal Original fallback,
            // while corrupt or dimensionally invalid files remain actionable warnings.
            if (message?.IndexOf(" is missing; using Original retail frame", StringComparison.Ordinal) >= 0) return;
            Debug.LogWarning(message);
        }
    }
}
