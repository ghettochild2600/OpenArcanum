using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Runtime.Audio;
using Arcanum.Runtime.World;
using OpenArcanum.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Arcanum.Runtime.UI
{
    public enum RetailOptionsTab { Game, Video, Sound }
    public enum RetailOptionKind { Choice, Numeric }
    public enum RetailOptionId
    {
        Difficulty, ViolenceFilter, DefaultCombatMode, AutoAttack, AutoSwitchWeapons,
        AlwaysRun, FollowerSkills, Brightness, TextDuration, Floats, FloatSpeed,
        CombatTaunts, EffectsVolume, VoiceVolume, MusicVolume,
    }

    public readonly struct RetailOptionDefinition
    {
        public RetailOptionId Id { get; }
        public RetailOptionsTab Tab { get; }
        public RetailOptionKind Kind { get; }
        public string Key { get; }
        public int DefaultValue { get; }
        public int Maximum { get; }
        public int TitleMessageKey { get; }
        public string ValueMessagePath { get; }
        public Vector2 Position { get; }

        public RetailOptionDefinition(RetailOptionId id, RetailOptionsTab tab, RetailOptionKind kind,
            string key, int defaultValue, int maximum, int titleMessageKey, string valueMessagePath,
            Vector2 position)
        {
            Id = id;
            Tab = tab;
            Kind = kind;
            Key = key;
            DefaultValue = defaultValue;
            Maximum = maximum;
            TitleMessageKey = titleMessageKey;
            ValueMessagePath = valueMessagePath;
            Position = position;
        }
    }

    /// <summary>Exact source Options pages and cyclic-control geometry from options_ui.c.</summary>
    public static class RetailOptionsLayout
    {
        public const int BackgroundSourceId = 556;
        public const int PcLensFrameSourceId = 670;
        public const int ChoiceSourceId = 671;
        public const int SliderSourceId = 688;
        public const int LeftArrowSourceId = 667;
        public const int RightArrowSourceId = 668;
        public const int FillSourceId = 623;
        public const int TitleFontSourceId = 27;
        public const int ValueFontSourceId = 229;
        public const int TabFontSourceId = 327;
        public static readonly Rect Window = new(0, 0, 800, 600);
        public static readonly Rect PcLens = new(84, 67, 89, 89);
        public static readonly Vector2[] TabTextPositions =
        {
            new(130, 221), new(130, 261), new(130, 301), new(130, 341),
        };
        public static readonly Vector2[] ControlPositions =
        {
            new(261, 122), new(261, 197), new(261, 272), new(261, 347),
            new(531, 122), new(531, 197), new(531, 272), new(531, 347),
        };
        public static readonly int[] TabMessageKeys = { 4000, 4001, 4002, 4003 };

        private static readonly RetailOptionDefinition[] Definitions =
        {
            // Source control zero is Module. It is deliberately absent in play and OpenArcanum has one module.
            Choice(RetailOptionId.Difficulty, RetailOptionsTab.Game, "difficulty", 1, 2, 101,
                "mes/OptionsDifficulty.mes", 1),
            Choice(RetailOptionId.ViolenceFilter, RetailOptionsTab.Game, "violence filter", 0, 1, 102,
                "mes/OptionsOffOn.mes", 2),
            Choice(RetailOptionId.DefaultCombatMode, RetailOptionsTab.Game, "default combat mode", 0, 2, 103,
                "mes/OptionsCombatMode.mes", 3),
            Choice(RetailOptionId.AutoAttack, RetailOptionsTab.Game, "auto attack", 1, 1, 104,
                "mes/OptionsOffOn.mes", 4),
            Choice(RetailOptionId.AutoSwitchWeapons, RetailOptionsTab.Game, "auto switch", 0, 1, 105,
                "mes/OptionsOffOn.mes", 5),
            Choice(RetailOptionId.AlwaysRun, RetailOptionsTab.Game, "always run", 0, 1, 106,
                "mes/OptionsOffOn.mes", 6),
            Choice(RetailOptionId.FollowerSkills, RetailOptionsTab.Game, "follower skills", 1, 1, 107,
                "mes/OptionsOffOn.mes", 7),
            Numeric(RetailOptionId.Brightness, RetailOptionsTab.Video, "brightness", 0, 9, 200, 0),
            Numeric(RetailOptionId.TextDuration, RetailOptionsTab.Video, "text duration", 6, 10, 201, 1),
            Choice(RetailOptionId.Floats, RetailOptionsTab.Video, "text floaters", 1, 2, 202,
                "mes/OptionsFloats.mes", 2),
            Numeric(RetailOptionId.FloatSpeed, RetailOptionsTab.Video, "float speed", 2, 10, 203, 3),
            Choice(RetailOptionId.CombatTaunts, RetailOptionsTab.Video, "combat taunts", 1, 1, 204,
                "mes/OptionsOffOn.mes", 4),
            Numeric(RetailOptionId.EffectsVolume, RetailOptionsTab.Sound, "effects volume", 5, 10, 300, 0),
            Numeric(RetailOptionId.VoiceVolume, RetailOptionsTab.Sound, "voice volume", 5, 10, 301, 1),
            Numeric(RetailOptionId.MusicVolume, RetailOptionsTab.Sound, "music volume", 5, 10, 302, 2),
        };

        public static IReadOnlyList<RetailOptionDefinition> All => Definitions;
        public static IReadOnlyList<RetailOptionDefinition> ForTab(RetailOptionsTab tab)
            => Definitions.Where(value => value.Tab == tab).ToArray();
        public static RetailOptionDefinition Definition(RetailOptionId id)
            => Definitions.Single(value => value.Id == id);
        public static Rect ScreenRect(int width, int height)
            => new((width - 800f) * .5f, (height - 600f) * .5f, 800f, 600f);

        private static RetailOptionDefinition Choice(RetailOptionId id, RetailOptionsTab tab, string key,
            int defaultValue, int maximum, int title, string values, int slot)
            => new(id, tab, RetailOptionKind.Choice, key, defaultValue, maximum, title, values,
                ControlPositions[slot]);
        private static RetailOptionDefinition Numeric(RetailOptionId id, RetailOptionsTab tab, string key,
            int defaultValue, int maximum, int title, int slot)
            => new(id, tab, RetailOptionKind.Numeric, key, defaultValue, maximum, title, null,
                ControlPositions[slot]);
    }

    public interface IRetailOptionsStore
    {
        int Read(string key, int fallback);
        void Write(string key, int value);
        void Save();
    }

    public sealed class PlayerPrefsRetailOptionsStore : IRetailOptionsStore
    {
        private const string Prefix = "OpenArcanum.SourceOption.";
        public int Read(string key, int fallback) => PlayerPrefs.GetInt(Prefix + key, fallback);
        public void Write(string key, int value) => PlayerPrefs.SetInt(Prefix + key, value);
        public void Save() => PlayerPrefs.Save();
    }

    /// <summary>
    /// Persistent source option values. The source mutates a setting as each arrow is pressed; it has no
    /// Apply/Cancel/Defaults or keyboard-remapping page, so this model intentionally exposes none.
    /// </summary>
    public sealed class RetailOptionsModel
    {
        private readonly IRetailOptionsStore _store;
        private readonly Dictionary<RetailOptionId, int> _values = new();
        private readonly Action<RetailOptionId, int> _apply;

        public event Action Changed;
        public RetailOptionsTab Tab { get; private set; }
        public bool HasApply => false;
        public bool HasCancel => false;
        public bool HasDefaults => false;
        public bool HasKeyRemapping => false;

        public RetailOptionsModel(IRetailOptionsStore store, Action<RetailOptionId, int> apply = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _apply = apply;
            foreach (RetailOptionDefinition definition in RetailOptionsLayout.All)
            {
                int value = Mathf.Clamp(_store.Read(definition.Key, definition.DefaultValue), 0,
                    definition.Maximum);
                _values.Add(definition.Id, value);
                _apply?.Invoke(definition.Id, value);
            }
        }

        public int Value(RetailOptionId id) => _values[id];
        public void SelectTab(RetailOptionsTab tab)
        {
            if (Tab == tab) return;
            Tab = tab;
            Changed?.Invoke();
        }

        public void Cycle(RetailOptionId id, int direction)
        {
            RetailOptionDefinition definition = RetailOptionsLayout.Definition(id);
            int value = _values[id];
            if (definition.Kind == RetailOptionKind.Numeric)
                value = Mathf.Clamp(value + Math.Sign(direction), 0, definition.Maximum);
            else
            {
                value += Math.Sign(direction);
                if (value < 0) value = definition.Maximum;
                else if (value > definition.Maximum) value = 0;
            }
            Set(id, value);
        }

        public void Set(RetailOptionId id, int value)
        {
            RetailOptionDefinition definition = RetailOptionsLayout.Definition(id);
            value = Mathf.Clamp(value, 0, definition.Maximum);
            if (_values[id] == value) return;
            _values[id] = value;
            _store.Write(definition.Key, value);
            _store.Save();
            _apply?.Invoke(id, value);
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Source Main Menu morph-text button used by the Options page. mainmenu_ui.c draws font 327 in
    /// three TIG passes and changes only the foreground red while the measured text hitbox is hovered.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(Image), typeof(Button))]
    public sealed class RetailOptionsTabView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerClickHandler
    {
        private static readonly Color PassBlack = new Color32(0, 0, 0, 255);
        private static readonly Color PassBrown = new Color32(97, 61, 42, 255);
        private static readonly Color NormalRed = new Color32(100, 0, 0, 255);
        private static readonly Color HighlightRed = new Color32(240, 15, 15, 255);

        private SourceUiBitmapText _foreground;
        private Action _clicked;

        public Rect SourceRect { get; private set; }
        public bool PointerInside { get; private set; }
        public Button Button { get; private set; }

        public void Bind(UiSkinResolver resolver, string text, Vector2 sourcePosition, Action clicked)
        {
            Button = GetComponent<Button>();
            Button.transition = Selectable.Transition.None;
            Button.onClick.RemoveAllListeners();
            _clicked = clicked;
            Image hit = GetComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;

            SourceUiBitmapText black = CreatePass("Black Pass", resolver, text, PassBlack,
                new Vector2(-1f, 1f));
            SourceUiBitmapText brown = CreatePass("Brown Pass", resolver, text, PassBrown,
                new Vector2(1f, -1f));
            _foreground = CreatePass("Red Pass", resolver, text, NormalRed, Vector2.zero);
            Vector2 size = new(
                Mathf.Max(black.LogicalSize.x, brown.LogicalSize.x, _foreground.LogicalSize.x),
                Mathf.Max(black.LogicalSize.y, brown.LogicalSize.y, _foreground.LogicalSize.y));

            RectTransform rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(sourcePosition.x - size.x * .5f, -sourcePosition.y);
            rect.sizeDelta = size;
            SourceRect = new Rect(sourcePosition.x - size.x * .5f, sourcePosition.y, size.x, size.y);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            PointerInside = true;
            _foreground?.SetTint(HighlightRed);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            PointerInside = false;
            _foreground?.SetTint(NormalRed);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) _clicked?.Invoke();
        }

        private SourceUiBitmapText CreatePass(string name, UiSkinResolver resolver, string text, Color tint,
            Vector2 offset)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(SourceUiBitmapText));
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = offset;
            SourceUiBitmapText bitmap = child.GetComponent<SourceUiBitmapText>();
            bitmap.Bind(resolver, text, tint, RetailOptionsLayout.TabFontSourceId);
            return bitmap;
        }
    }

    [DisallowMultipleComponent]
    public sealed class RetailOptionsView : MonoBehaviour
    {
        private static readonly Color White = new Color32(255, 255, 255, 255);
        private static readonly Color Red = new Color32(240, 15, 15, 255);
        private GameUiController _controller;
        private WorldMapSessionCoordinator _session;
        private UiSkinResolver _resolver;
        private ProductionKeyboardController _keyboard;
        private ProductionAudioPresenter _audio;
        private GameObject _canvasRoot;
        private RectTransform _window;
        private RectTransform _controls;

        public RetailOptionsModel Model { get; private set; }
        public bool IsAvailable => _resolver != null && _canvasRoot != null;
        public bool IsVisible => IsAvailable && _canvasRoot.activeSelf;
        public RectTransform WindowTransform => _window;

        public void Bind(GameUiController controller, WorldMapSessionCoordinator session, UiSkinResolver resolver,
            ProductionKeyboardController keyboard, ProductionAudioPresenter audio,
            IRetailOptionsStore store = null)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            Cleanup();
            _controller = controller;
            _session = session;
            _resolver = resolver;
            _keyboard = keyboard ?? throw new ArgumentNullException(nameof(keyboard));
            _audio = audio;
            Model = new RetailOptionsModel(store ?? new PlayerPrefsRetailOptionsStore(), Apply);
            Model.Changed += RebuildControls;
            BuildPresentation();
            Synchronize();
        }

        public void Synchronize()
        {
            if (!IsAvailable || _controller == null) return;
            bool visible = _controller.Screen == GameUiScreen.Options;
            if (_canvasRoot.activeSelf != visible) _canvasRoot.SetActive(visible);
            if (!visible) return;
            ApplyAudio();
        }

        public bool ContainsScreenPoint(Vector2 screenBottomLeft)
        {
            if (!IsVisible) return false;
            return RetailOptionsLayout.ScreenRect(Screen.width, Screen.height)
                .Contains(new Vector2(screenBottomLeft.x, Screen.height - screenBottomLeft.y));
        }

        private void BuildPresentation()
        {
            _canvasRoot = new GameObject("Source-Faithful Options", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(SourceUiRuntime));
            _canvasRoot.transform.SetParent(transform, false);
            _canvasRoot.GetComponent<SourceUiRuntime>().BindResolver(_resolver);
            Canvas canvas = _canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 800;
            CanvasScaler scaler = _canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 1f;

            _window = AddRect("Retail Options", (RectTransform)_canvasRoot.transform, RetailOptionsLayout.Window);
            _window.anchorMin = _window.anchorMax = new Vector2(.5f, .5f);
            _window.pivot = new Vector2(.5f, .5f);
            _window.anchoredPosition = Vector2.zero;
            AddSourceImage("Options Background", _window, RetailOptionsLayout.BackgroundSourceId,
                RetailOptionsLayout.Window);
            RectTransform blocker = AddRect("Options Input", _window, RetailOptionsLayout.Window);
            Image blockerImage = blocker.gameObject.AddComponent<Image>();
            blockerImage.color = Color.clear;
            blockerImage.raycastTarget = true;

            RectTransform liveLens = AddRect("PC Lens Live Character", _window, RetailOptionsLayout.PcLens);
            liveLens.gameObject.AddComponent<RetailPcLensView>().Bind(_session);
            AddSourceImage("PC Lens Frame", _window, RetailOptionsLayout.PcLensFrameSourceId,
                RetailOptionsLayout.PcLens);

            for (int index = 0; index < RetailOptionsLayout.TabMessageKeys.Length; index++)
            {
                int captured = index;
                string label = SourceText("mes/mainmenu.mes", RetailOptionsLayout.TabMessageKeys[index],
                    index == 0 ? "Game" : index == 1 ? "Video" : index == 2 ? "Sound" : "Done");
                AddTab(index == 3 ? "Done" : ((RetailOptionsTab)index).ToString(), label,
                    RetailOptionsLayout.TabTextPositions[index],
                    () => { if (captured == 3) _controller.Close(); else Model.SelectTab((RetailOptionsTab)captured); });
            }
            _controls = AddRect("Options Controls", _window, RetailOptionsLayout.Window);
            RebuildControls();
        }

        private void RebuildControls()
        {
            if (_controls == null || Model == null) return;
            for (int index = _controls.childCount - 1; index >= 0; index--)
            {
                GameObject child = _controls.GetChild(index).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Object.Destroy(child); else Object.DestroyImmediate(child);
            }
            foreach (RetailOptionDefinition definition in RetailOptionsLayout.ForTab(Model.Tab))
                AddControl(definition);
        }

        private void AddControl(RetailOptionDefinition definition)
        {
            int art = definition.Kind == RetailOptionKind.Numeric
                ? RetailOptionsLayout.SliderSourceId : RetailOptionsLayout.ChoiceSourceId;
            Vector2 size = _resolver.TryResolve(new UiAssetKey(art), out UiResolvedAsset resolved)
                ? resolved.LogicalSize : new Vector2(213, 56);
            Rect rect = new(definition.Position, size);
            AddSourceImage(definition.Id + " Base", _controls, art, rect);
            string title = SourceText("mes/options_ui.mes", definition.TitleMessageKey,
                SplitName(definition.Id.ToString()));
            AddCenteredText(definition.Id + " Title", _controls,
                new Rect(rect.x, rect.y + 2, rect.width, 18), title,
                RetailOptionsLayout.TitleFontSourceId, White);
            if (definition.Kind == RetailOptionKind.Choice)
            {
                string value = SourceText(definition.ValueMessagePath, Model.Value(definition.Id),
                    Model.Value(definition.Id).ToString());
                AddCenteredText(definition.Id + " Value", _controls,
                    new Rect(rect.x + 47, rect.y + 30, 131, 16), value,
                    RetailOptionsLayout.ValueFontSourceId, Red);
            }
            else
            {
                float fill = definition.Maximum == 0 ? 0 : (float)Model.Value(definition.Id) / definition.Maximum;
                SourceUiImage liquid = AddSourceImage(definition.Id + " Fill", _controls,
                    RetailOptionsLayout.FillSourceId, new Rect(rect.x + 47, rect.y + 24, 131, 13));
                liquid.Image.type = Image.Type.Filled;
                liquid.Image.fillMethod = Image.FillMethod.Horizontal;
                liquid.Image.fillOrigin = (int)Image.OriginHorizontal.Left;
                liquid.Image.fillAmount = fill;
            }
            AddArrow(definition.Id + " Left", RetailOptionsLayout.LeftArrowSourceId,
                new Vector2(rect.x + 6, rect.y + 21), () => Model.Cycle(definition.Id, -1));
            AddArrow(definition.Id + " Right", RetailOptionsLayout.RightArrowSourceId,
                new Vector2(rect.x + 180, rect.y + 21), () => Model.Cycle(definition.Id, 1));
        }

        private void AddArrow(string name, int sourceId, Vector2 topLeft, Action clicked)
        {
            Vector2 size = _resolver.TryResolve(new UiAssetKey(sourceId), out UiResolvedAsset asset)
                ? asset.LogicalSize : new Vector2(27, 27);
            RectTransform target = AddRect(name, _controls, new Rect(topLeft, size));
            target.gameObject.AddComponent<Image>();
            target.gameObject.AddComponent<Button>();
            target.gameObject.AddComponent<SourceUiImage>();
            SourceUiButton button = target.gameObject.AddComponent<SourceUiButton>();
            button.Bind(_resolver, new UiAssetKey(sourceId, frame: 0), new UiAssetKey(sourceId, frame: 1),
                new UiAssetKey(sourceId, frame: 2), new UiAssetKey(sourceId, frame: 0));
            button.Button.onClick.AddListener(() => clicked());
        }

        private void AddTransparentButton(string name, Rect rect, Action clicked)
        {
            RectTransform target = AddRect(name, _window, rect);
            Image image = target.gameObject.AddComponent<Image>();
            image.color = Color.clear;
            Button button = target.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => clicked());
        }

        private void AddTab(string name, string label, Vector2 sourcePosition, Action clicked)
        {
            var value = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button),
                typeof(RetailOptionsTabView));
            RectTransform target = value.GetComponent<RectTransform>();
            target.SetParent(_window, false);
            value.GetComponent<RetailOptionsTabView>().Bind(_resolver, label, sourcePosition, clicked);
        }

        private void Apply(RetailOptionId id, int value)
        {
            if (id == RetailOptionId.AlwaysRun) _keyboard?.SetAlwaysRun(value != 0);
            if (id is RetailOptionId.EffectsVolume or RetailOptionId.VoiceVolume or RetailOptionId.MusicVolume)
                ApplyAudio();
        }

        private void ApplyAudio()
        {
            if (_audio == null || Model == null) return;
            _audio.ApplySourceOptionVolumes(Model.Value(RetailOptionId.EffectsVolume),
                Model.Value(RetailOptionId.VoiceVolume), Model.Value(RetailOptionId.MusicVolume));
        }

        private string SourceText(string path, int key, string fallback)
            => _resolver.Original.TryReadMessage(path, key, out string source) && !string.IsNullOrWhiteSpace(source)
                ? source : fallback;

        private static string SplitName(string value)
        {
            for (int index = value.Length - 1; index > 0; index--)
                if (char.IsUpper(value[index])) value = value.Insert(index, " ");
            return value;
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

        private SourceUiBitmapText AddCenteredText(string name, RectTransform parent, Rect bounds, string value,
            int font, Color color)
        {
            RectTransform target = AddRect(name, parent, bounds);
            SourceUiBitmapText text = target.gameObject.AddComponent<SourceUiBitmapText>();
            text.Bind(_resolver, value, color, font);
            target.anchoredPosition = new Vector2(bounds.x + Mathf.Max(0, (bounds.width - text.LogicalSize.x) * .5f),
                -bounds.y);
            return text;
        }

        private static RectTransform AddRect(string name, RectTransform parent, Rect rect)
        {
            var value = new GameObject(name, typeof(RectTransform));
            RectTransform target = value.GetComponent<RectTransform>();
            target.SetParent(parent, false);
            target.anchorMin = target.anchorMax = new Vector2(0, 1);
            target.pivot = new Vector2(0, 1);
            target.anchoredPosition = new Vector2(rect.x, -rect.y);
            target.sizeDelta = rect.size;
            return target;
        }

        private void OnDestroy() => Cleanup();
        private void Cleanup()
        {
            if (Model != null) Model.Changed -= RebuildControls;
            Model = null;
            if (_canvasRoot != null)
            {
                _canvasRoot.SetActive(false);
                if (Application.isPlaying) Object.Destroy(_canvasRoot); else Object.DestroyImmediate(_canvasRoot);
            }
            _canvasRoot = null;
            _window = null;
            _controls = null;
        }
    }
}
