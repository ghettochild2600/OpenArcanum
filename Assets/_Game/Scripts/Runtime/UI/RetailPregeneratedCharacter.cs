using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Creation;
using Arcanum.Runtime.World;
using OpenArcanum.UI;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Arcanum.Runtime.UI
{
    /// <summary>
    /// Source ID 766 pregenerated-character selector. Selection is presentation state; Start delegates the exact
    /// prototype-backed transaction to CharacterCreationStateService.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RetailPregeneratedCharacterView : MonoBehaviour
    {
        public const int BackgroundSourceId = 766;
        private static readonly Color SourceWhite = new Color32(231, 217, 174, 255);
        private static readonly Color SourceYellow = new Color32(245, 201, 65, 255);
        private static readonly Rect PortraitDraw = new(95f, 28f, 128f, 128f);

        private GameUiController _controller;
        private WorldMapSessionCoordinator _session;
        private UiSkinResolver _resolver;
        private GameObject _canvasRoot;
        private RectTransform _topRegion;
        private RectTransform _window;
        private RectTransform _bottomRegion;
        private SourceUiImage _topBackground;
        private SourceUiImage _background;
        private SourceUiImage _bottomBackground;
        private Image _portrait;
        private SourceUiBitmapText _name;
        private SourceUiBitmapText _gender;
        private SourceUiBitmapText _race;
        private SourceUiBitmapText _backgroundName;
        private SourceUiBitmapText _descriptionTitle;
        private RectTransform _description;
        private SourceUiBitmapText _feedback;
        private readonly List<SourceUiBitmapText> _stats = new();
        private readonly Dictionary<SourceUiBitmapText, Rect> _textBounds = new();
        private int _signature = int.MinValue;

        public bool IsAvailable => _canvasRoot != null && _resolver != null;
        public bool IsVisible => IsAvailable && _canvasRoot.activeSelf;
        public int SelectedSourceIndex => _controller?.PregeneratedCharacterIndex ?? 0;
        public RectTransform TopRegionRoot => _topRegion;
        public RectTransform CenterRegionRoot => _window;
        public RectTransform BottomRegionRoot => _bottomRegion;
        public UiResolvedAsset TopBackgroundAsset => _topBackground?.CurrentAsset;
        public UiResolvedAsset BackgroundAsset => _background?.CurrentAsset;
        public UiResolvedAsset BottomBackgroundAsset => _bottomBackground?.CurrentAsset;
        public Image PortraitImage => _portrait;

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
            Build();
            Synchronize();
        }

        public void Synchronize()
        {
            if (!IsAvailable || _controller == null) return;
            bool visible = _controller.Screen == GameUiScreen.PregeneratedCharacter;
            if (_canvasRoot.activeSelf != visible) _canvasRoot.SetActive(visible);
            if (!visible) return;
            int signature = _controller.PregeneratedCharacterIndex * 31 + (_controller.Feedback?.GetHashCode() ?? 0);
            if (signature == _signature) return;
            _signature = signature;
            Refresh();
        }

        public void HandleEscape()
        {
            if (IsVisible) _controller.Open(GameUiScreen.MainMenu);
        }

        private void Build()
        {
            _canvasRoot = new GameObject("Source Pregenerated Character",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
                typeof(SourceUiRuntime));
            _canvasRoot.transform.SetParent(transform, false);
            RetailPartialMainMenuComposition.ConfigureNativeCanvas(_canvasRoot, _resolver, 995);
            RectTransform canvas = (RectTransform)_canvasRoot.transform;
            RetailPartialMainMenuRegion top = RetailPartialMainMenuComposition.CreateTop(canvas, _resolver,
                "Pregenerated Top Region");
            RetailPartialMainMenuRegion center = RetailPartialMainMenuComposition.CreateCenter(canvas, _resolver,
                BackgroundSourceId, "Pregenerated Center Region");
            RetailPartialMainMenuRegion bottom = RetailPartialMainMenuComposition.CreateBottom(canvas, _resolver,
                "Pregenerated Bottom Region");
            _topRegion = top.Root;
            _topBackground = top.Background;
            _window = center.Root;
            _background = center.Background;
            _bottomRegion = bottom.Root;
            _bottomBackground = bottom.Background;

            // mainmenu_ui draws the portrait before it creates the four source buttons.  Keeping
            // that order is significant: the 128x128 portrait intentionally intersects the
            // button rectangles, but the buttons (and their complete hit targets) remain on top.
            _portrait = AddImage("Portrait 128x128", PortraitDraw);
            AddButton("Start", _bottomRegion, 321,
                RetailPartialMainMenuLayout.BottomLocal(new Vector2(675f, 55f)),
                () => _controller.FinalizePregeneratedCharacter());
            AddButton("Back", _bottomRegion, 323,
                RetailPartialMainMenuLayout.BottomLocal(new Vector2(69f, 55f)),
                () => _controller.Open(GameUiScreen.MainMenu));
            AddButton("Previous Portrait", _window, 283,
                RetailPartialMainMenuLayout.CenterLocal(new Vector2(43f, 118f)), () => Cycle(-1));
            AddButton("Next Portrait", _window, 284,
                RetailPartialMainMenuLayout.CenterLocal(new Vector2(244f, 118f)), () => Cycle(1));
            _name = AddText("Name", new Rect(46f, 183f, 228f, 18f), 27, SourceYellow);
            _gender = AddText("Gender", RetailCharacterCreationLayout.Gender, 27, SourceWhite);
            _race = AddText("Race", RetailCharacterCreationLayout.Race, 27, SourceWhite);
            _backgroundName = AddText("Background", RetailCharacterCreationLayout.Background, 27, SourceWhite);
            for (int index = 0; index < 8; index++)
            {
                float x = index < 4 ? 346f : 406f;
                float y = 46f + 20f * (index % 4);
                _stats.Add(AddText($"Stat {index}", new Rect(x, y, 50f, 15f), 26, SourceWhite));
            }
            _descriptionTitle = AddText("Description Title", RetailCharacterCreationLayout.DescriptionTitle,
                27, SourceYellow);
            _description = AddRect("Description", _window, RetailCharacterCreationLayout.DescriptionBody);
            _feedback = AddText("Feedback", new Rect(350f, 371f, 395f, 18f), 26,
                new Color32(245, 130, 95, 255));

            var cursor = new GameObject("Pregenerated Cursor",
                typeof(RectTransform), typeof(Image), typeof(SourceUiImage), typeof(RetailMainMenuCursorView));
            cursor.transform.SetParent(_canvasRoot.transform, false);
            cursor.GetComponent<RetailMainMenuCursorView>().Bind((RectTransform)_canvasRoot.transform, _resolver);
        }

        private void Refresh()
        {
            PregeneratedCharacterDefinition selected = _session.CharacterCreation.Catalog.PregeneratedCharacters
                .First(value => value.SourceIndex == _controller.PregeneratedCharacterIndex);
            ObjectProtoInfo prototype = _session.CharacterCreation.ResolvePregeneratedPrototype(selected);
            SetCentered(_name, selected.Name, SourceYellow);
            SetCentered(_gender, selected.Gender.ToString(), SourceWhite);
            SetCentered(_race, Friendly(selected.Race), SourceWhite);
            BackgroundDefinition background = _session.CharacterCreation.Catalog.Backgrounds
                .GetValueOrDefault(selected.BackgroundId);
            SetCentered(_backgroundName, background?.Name ?? $"Background {selected.BackgroundId}", SourceWhite);
            SetText(_descriptionTitle, background?.Name ?? "Background", SourceYellow);
            RebuildDescription(background?.Description ?? string.Empty);
            SetText(_feedback, _controller.Feedback,
                new Color32(245, 130, 95, 255));

            _portrait.sprite = null;
            if (_session.CharacterCreation.Catalog.Portraits.TryGetValue(selected.PortraitId,
                    out PortraitDefinition portrait)
                && _resolver.Original.TryResolvePortrait(portrait.SourceName, out RetailPortraitAsset asset))
                _portrait.sprite = asset.Sprite;
            string[] abbreviations = { "ST", "DX", "CN", "BE", "IN", "PE", "WP", "CH" };
            for (int index = 0; index < _stats.Count; index++)
                SetText(_stats[index], $"{abbreviations[index]}:{prototype?.StatBase?[index] ?? 0}", SourceWhite);
        }

        private void Cycle(int direction)
        {
            _controller.CyclePregeneratedCharacter(direction);
            _signature = int.MinValue;
            Synchronize();
        }

        private void AddButton(string name, RectTransform parent, int sourceId, Vector2 topLeft, Action action)
        {
            Vector2 size = _resolver.TryResolve(new UiAssetKey(sourceId), out UiResolvedAsset asset)
                ? asset.LogicalSize : new Vector2(32f, 32f);
            RectTransform target = AddRect(name, parent, new Rect(topLeft.x, topLeft.y, size.x, size.y));
            target.gameObject.AddComponent<Image>();
            target.gameObject.AddComponent<Button>();
            target.gameObject.AddComponent<SourceUiImage>();
            SourceUiButton button = target.gameObject.AddComponent<SourceUiButton>();
            button.Bind(_resolver, new UiAssetKey(sourceId, frame: 0), new UiAssetKey(sourceId, frame: 1),
                new UiAssetKey(sourceId, frame: 2), new UiAssetKey(sourceId, frame: 0));
            button.Button.onClick.AddListener(() => action());
        }

        private Image AddImage(string name, Rect rect)
        {
            RectTransform target = AddRect(name, _window, rect);
            Image image = target.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = false;
            return image;
        }

        private SourceUiImage AddSourceImage(string name, RectTransform parent, int sourceId, Rect rect)
        {
            RectTransform target = AddRect(name, parent, rect);
            target.gameObject.AddComponent<Image>().raycastTarget = false;
            SourceUiImage source = target.gameObject.AddComponent<SourceUiImage>();
            source.Bind(_resolver, new UiAssetKey(sourceId));
            return source;
        }

        private SourceUiBitmapText AddText(string name, Rect rect, int font, Color tint)
        {
            RectTransform target = AddRect(name, _window, rect);
            SourceUiBitmapText text = target.gameObject.AddComponent<SourceUiBitmapText>();
            _textBounds[text] = rect;
            text.Bind(_resolver, string.Empty, tint, font);
            return text;
        }

        private void SetCentered(SourceUiBitmapText text, string value, Color tint)
        {
            Rect bounds = _textBounds[text];
            text.Bind(_resolver, value ?? string.Empty, tint, text.FontSourceId);
            ((RectTransform)text.transform).anchoredPosition = new Vector2(
                bounds.x + Mathf.Max(0f, (bounds.width - text.LogicalSize.x) * .5f), -bounds.y);
        }

        private void SetText(SourceUiBitmapText text, string value, Color tint)
        {
            if (text.Text == (value ?? string.Empty)) return;
            text.Bind(_resolver, value ?? string.Empty, tint, text.FontSourceId);
        }

        private void RebuildDescription(string value)
        {
            for (int index = _description.childCount - 1; index >= 0; index--)
                Object.DestroyImmediate(_description.GetChild(index).gameObject);
            string[] words = (value ?? string.Empty).Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            string line = string.Empty;
            int row = 0;
            foreach (string word in words)
            {
                string next = line.Length == 0 ? word : line + " " + word;
                if (next.Length <= 52) { line = next; continue; }
                AddDescriptionLine(line, row++);
                line = word;
                if (row >= 11) break;
            }
            if (line.Length > 0 && row < 11) AddDescriptionLine(line, row);
        }

        private void AddDescriptionLine(string value, int row)
        {
            RectTransform target = AddRect($"Line {row + 1}", _description,
                new Rect(0f, row * 15f, RetailCharacterCreationLayout.DescriptionBody.width, 15f));
            target.gameObject.AddComponent<SourceUiBitmapText>().Bind(_resolver, value, SourceWhite, 26);
        }

        private static RectTransform AddRect(string name, RectTransform parent, Rect rect)
        {
            var go = new GameObject(name, typeof(RectTransform));
            RectTransform target = go.GetComponent<RectTransform>();
            target.SetParent(parent, false);
            target.anchorMin = target.anchorMax = new Vector2(0f, 1f);
            target.pivot = new Vector2(0f, 1f);
            target.anchoredPosition = new Vector2(rect.x, -rect.y);
            target.sizeDelta = rect.size;
            return target;
        }

        private static string Friendly(CharacterRace race)
            => race switch
            {
                CharacterRace.HalfElf => "Half-Elf",
                CharacterRace.HalfOrc => "Half-Orc",
                CharacterRace.HalfOgre => "Half-Ogre",
                _ => race.ToString(),
            };

        private void OnDestroy() => Cleanup();

        private void Cleanup()
        {
            if (_canvasRoot != null)
            {
                _canvasRoot.SetActive(false);
                if (Application.isPlaying) Object.Destroy(_canvasRoot); else Object.DestroyImmediate(_canvasRoot);
            }
            _canvasRoot = null;
            _topRegion = null;
            _window = null;
            _bottomRegion = null;
            _topBackground = null;
            _background = null;
            _bottomBackground = null;
            _portrait = null;
            _stats.Clear();
            _textBounds.Clear();
            _signature = int.MinValue;
        }
    }
}
