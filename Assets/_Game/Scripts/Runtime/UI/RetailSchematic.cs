using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Runtime.Crafting;
using Arcanum.Runtime.Technology;
using Arcanum.Runtime.World;
using OpenArcanum.UI;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Arcanum.Runtime.UI
{
    public enum RetailSchematicMode { Learned, Found }

    /// <summary>Native 800x600 coordinates transcribed from schematic_ui.c.</summary>
    public static class RetailSchematicLayout
    {
        public const int BackgroundSourceId = 365;
        public const int PcLensFrameSourceId = 231;
        public const int DescriptionFontSourceId = 229;
        public const int DisciplineFontSourceId = 370;
        public const int ComponentDisciplineFontSourceId = 300;
        public const int NameFontSourceId = 371;
        public static readonly Rect Window = new(0, 41, 800, 400);
        public static readonly Rect PcLens = new(50, 26, 89, 89);
        public static readonly Rect Name = new(261, 30, 420, 28);
        public static readonly Rect Description = new(219, 71, 466, 75);
        public static readonly Rect DisciplineIcon = new(222, 32, 100, 100);
        public static readonly Rect Illustration = new(240, 146, 320, 214);
        public static readonly Rect[] Components = { new(579, 181, 99, 67), new(579, 277, 99, 67) };
        public static readonly Rect[] ComponentIcons = { new(583, 182, 50, 50), new(583, 278, 50, 50) };
        public static readonly Rect[] ComponentComplexities = { new(658, 232, 50, 50), new(658, 328, 50, 50) };
        public static readonly Vector2 Previous = new(204, 57);
        public static readonly Vector2 Next = new(687, 57);
        public static readonly Vector2 Combine = new(76, 365);
        public static readonly Vector2 Learned = new(29, 201);
        public static readonly Vector2 Found = new(29, 289);
        public static readonly Vector2 Readiness = new(43, 338);
        public static readonly Vector2[] Tabs =
        {
            new(707, 58), new(707, 103), new(707, 148), new(707, 193),
            new(707, 238), new(707, 283), new(707, 328), new(707, 373),
        };
        public static readonly int[] TabSourceIds = { 355, 356, 361, 362, 363, 364, 368, 369 };

        public static Rect ScreenRect(int width, int height)
            => new((width - Window.width) * .5f, 41f + (height - 600f) * .5f, Window.width, Window.height);

        public static int ReadinessSourceId(GameUiSchematicView view)
        {
            if (view == null) return 359;
            if (!view.ComponentOne.HasExpertise || !view.ComponentTwo.HasExpertise) return 358;
            return view.ComponentOne.IsOwned && view.ComponentTwo.IsOwned ? 360 : 359;
        }

        public static IReadOnlyList<GameUiSchematicView> Filter(IReadOnlyList<GameUiSchematicView> source,
            RetailSchematicMode mode, TechnologyDiscipline discipline)
            => (source ?? Array.Empty<GameUiSchematicView>())
                .Where(value => value.Discipline == discipline
                                && value.Definition.Acquisition == (mode == RetailSchematicMode.Learned
                                    ? SchematicAcquisition.BuiltIn : SchematicAcquisition.Found))
                .OrderBy(value => value.Definition.Id.Value).ToArray();
    }

    /// <summary>Source presentation only; crafting state and transactions remain owned by CraftingStateService.</summary>
    [DisallowMultipleComponent]
    public sealed class RetailSchematicView : MonoBehaviour
    {
        private static readonly Color Ink = new Color32(0, 0, 0, 255);
        private static readonly Color White = new Color32(255, 255, 255, 255);
        private static readonly Color MissingExpertise = new Color32(255, 204, 102, 255);
        private readonly SourceUiButton[] _tabs = new SourceUiButton[8];
        private readonly List<GameObject> _dynamic = new();
        private GameUiController _controller;
        private WorldMapSessionCoordinator _session;
        private UiSkinResolver _resolver;
        private GameObject _canvasRoot;
        private RectTransform _window;
        private RectTransform _dynamicLayer;
        private SourceUiButton _previous;
        private SourceUiButton _next;
        private SourceUiButton _combine;
        private SourceUiButton _learned;
        private SourceUiButton _found;
        private RetailSchematicMode _mode;
        private TechnologyDiscipline _discipline;
        private readonly int[] _pages = new int[2];
        private readonly TechnologyDiscipline[] _disciplines =
            { TechnologyDiscipline.Herbology, TechnologyDiscipline.Herbology };
        private int _signature = int.MinValue;
        private bool _dirty = true;

        public bool IsAvailable => _resolver != null && _canvasRoot != null;
        public bool IsVisible => IsAvailable && _canvasRoot.activeSelf;
        public RetailSchematicMode Mode => _mode;
        public TechnologyDiscipline Discipline => _discipline;
        public int Page => _pages[(int)_mode];
        public RectTransform WindowTransform => _window;

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
            bool visible = _controller.HasPlayer && _controller.Screen == GameUiScreen.Crafting;
            if (_canvasRoot.activeSelf != visible) _canvasRoot.SetActive(visible);
            if (!visible) return;
            IReadOnlyList<GameUiSchematicView> all = _controller.ProjectSchematics();
            int signature = ProjectionSignature(all);
            if (_dirty || signature != _signature)
            {
                _dirty = false;
                _signature = signature;
                Rebuild(all);
            }
        }

        public bool ContainsScreenPoint(Vector2 screenBottomLeft)
        {
            if (!IsVisible) return false;
            return RetailSchematicLayout.ScreenRect(Screen.width, Screen.height)
                .Contains(new Vector2(screenBottomLeft.x, Screen.height - screenBottomLeft.y));
        }

        public void SelectMode(RetailSchematicMode mode)
        {
            if (_mode == mode) return;
            _disciplines[(int)_mode] = _discipline;
            _mode = mode;
            _discipline = _disciplines[(int)_mode];
            _dirty = true;
            Synchronize();
        }

        public void SelectDiscipline(TechnologyDiscipline discipline)
        {
            _discipline = discipline;
            _disciplines[(int)_mode] = discipline;
            _pages[(int)_mode] = 0;
            _dirty = true;
            Synchronize();
        }

        public void Turn(int delta)
        {
            IReadOnlyList<GameUiSchematicView> filtered = RetailSchematicLayout.Filter(
                _controller.ProjectSchematics(), _mode, _discipline);
            _pages[(int)_mode] = Mathf.Clamp(Page + Math.Sign(delta), 0, Math.Max(0, filtered.Count - 1));
            _dirty = true;
            Synchronize();
        }

        private void BuildPresentation()
        {
            _canvasRoot = new GameObject("Source-Faithful Schematics", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(SourceUiRuntime));
            _canvasRoot.transform.SetParent(transform, false);
            _canvasRoot.GetComponent<SourceUiRuntime>().BindResolver(_resolver);
            Canvas canvas = _canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            CanvasScaler scaler = _canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 1f;
            _window = AddRect("Retail Schematics", (RectTransform)_canvasRoot.transform, new Rect(0, 0, 800, 400));
            _window.anchorMin = _window.anchorMax = new Vector2(.5f, .5f);
            _window.pivot = new Vector2(.5f, .5f);
            _window.anchoredPosition = new Vector2(0f, 59f);
            AddSourceImage("Schematics Background", _window, RetailSchematicLayout.BackgroundSourceId,
                new Rect(0, 0, 800, 400));
            RectTransform blocker = AddRect("Schematics Input", _window, new Rect(0, 0, 800, 400));
            Image blockerImage = blocker.gameObject.AddComponent<Image>();
            blockerImage.color = Color.clear;
            blockerImage.raycastTarget = true;

            RectTransform lens = AddRect("Live PC Lens", _window, RetailSchematicLayout.PcLens);
            lens.gameObject.AddComponent<RetailPcLensView>().Bind(_session);
            SourceUiImage frame = AddSourceImage("PC Lens Frame", _window,
                RetailSchematicLayout.PcLensFrameSourceId, RetailSchematicLayout.PcLens);
            frame.Image.raycastTarget = true;
            Button close = frame.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.onClick.AddListener(() => _controller.Close());

            _previous = AddButton("Previous Schematic", 372, RetailSchematicLayout.Previous, () => Turn(-1));
            _next = AddButton("Next Schematic", 373, RetailSchematicLayout.Next, () => Turn(1));
            _combine = AddButton("Combine", 357, RetailSchematicLayout.Combine, CraftCurrent);
            _learned = AddButton("Learned Schematics", 6, RetailSchematicLayout.Learned,
                () => SelectMode(RetailSchematicMode.Learned));
            _found = AddButton("Found Schematics", 6, RetailSchematicLayout.Found,
                () => SelectMode(RetailSchematicMode.Found));
            BindMesHint(_combine.gameObject, 3, "Combine");
            BindMesHint(_learned.gameObject, 4, "Learned Schematics");
            BindMesHint(_found.gameObject, 5, "Found Schematics");

            for (int index = 0; index < _tabs.Length; index++)
            {
                TechnologyDiscipline discipline = (TechnologyDiscipline)index;
                _tabs[index] = AddButton(Friendly(discipline), RetailSchematicLayout.TabSourceIds[index],
                    RetailSchematicLayout.Tabs[index], () => SelectDiscipline(discipline));
                string owner = $"schematic-discipline-{index}";
                _tabs[index].gameObject.AddComponent<SourceHoverHintTarget>().Bind(
                    () => _controller.ShowContextualHelp(owner, Friendly(discipline)),
                    () => _controller.ClearContextualHelp(owner));
            }
            _dynamicLayer = AddRect("Schematic Page", _window, new Rect(0, 0, 800, 400));
        }

        private void Rebuild(IReadOnlyList<GameUiSchematicView> all)
        {
            ClearDynamic();
            IReadOnlyList<GameUiSchematicView> filtered = RetailSchematicLayout.Filter(all, _mode, _discipline);
            int mode = (int)_mode;
            _pages[mode] = Mathf.Clamp(_pages[mode], 0, Math.Max(0, filtered.Count - 1));
            _previous.gameObject.SetActive(Page > 0);
            _next.gameObject.SetActive(Page + 1 < filtered.Count);
            _learned.SetSelected(_mode == RetailSchematicMode.Learned);
            _found.SetSelected(_mode == RetailSchematicMode.Found);
            for (int index = 0; index < _tabs.Length; index++)
                _tabs[index].SetSelected(index == (int)_discipline);
            if (filtered.Count == 0)
            {
                _combine.gameObject.SetActive(false);
                AddDynamicText("Discipline", RetailSchematicLayout.Name, Friendly(_discipline),
                    RetailSchematicLayout.NameFontSourceId, Ink);
                return;
            }

            _combine.gameObject.SetActive(true);
            GameUiSchematicView view = filtered[Page];
            AddDynamicImage("Schematic Illustration", view.Definition.ArtNumber, RetailSchematicLayout.Illustration, true);
            AddDynamicText("Schematic Name", RetailSchematicLayout.Name, view.Definition.Name,
                RetailSchematicLayout.NameFontSourceId, Ink);
            AddDynamicParagraph("Schematic Description", RetailSchematicLayout.Description,
                view.Definition.Description ?? string.Empty, RetailSchematicLayout.DescriptionFontSourceId, Ink);
            AddDynamicText("Schematic Discipline", RetailSchematicLayout.DisciplineIcon,
                ((char)('0' + (int)view.Discipline)).ToString(), RetailSchematicLayout.DisciplineFontSourceId, Ink);
            DrawComponent(0, view.ComponentOne);
            DrawComponent(1, view.ComponentTwo);
            AddDynamicImage("Readiness", RetailSchematicLayout.ReadinessSourceId(view),
                new Rect(RetailSchematicLayout.Readiness, NativeSize(RetailSchematicLayout.ReadinessSourceId(view))), false);
        }

        private void DrawComponent(int index, GameUiSchematicComponentView component)
        {
            Rect slot = RetailSchematicLayout.Components[index];
            if (component.HasExpertise)
                AddDynamicImage(component.IsOwned ? "Owned Component" : "Missing Component",
                    component.IsOwned ? 831 : 830, slot, false);
            RectTransform root = AddRect(component.Name, _dynamicLayer, slot);
            _dynamic.Add(root.gameObject);
            Image image = root.gameObject.AddComponent<Image>();
            image.raycastTarget = true;
            image.preserveAspect = true;
            image.color = component.HasExpertise ? White : MissingExpertise;
            if (component.InventoryArtId.HasValue
                && _resolver.Original.TryResolveInventoryItem(component.InventoryArtId.Value, out RetailItemArtAsset asset))
                image.sprite = asset.Sprite;
            image.enabled = image.sprite != null;
            string owner = $"schematic-component-{index}";
            root.gameObject.AddComponent<SourceHoverHintTarget>().Bind(
                () => _controller.ShowContextualHelp(owner, component.Name),
                () => _controller.ClearContextualHelp(owner));
            if (component.Complexity <= 0) return;
            AddDynamicText($"Component {index + 1} Discipline", RetailSchematicLayout.ComponentIcons[index],
                ((char)('1' + (int)component.Discipline)).ToString(),
                RetailSchematicLayout.ComponentDisciplineFontSourceId, Ink);
            AddDynamicText($"Component {index + 1} Complexity", RetailSchematicLayout.ComponentComplexities[index],
                component.Complexity.ToString(), RetailSchematicLayout.DescriptionFontSourceId, Ink);
        }

        private void CraftCurrent()
        {
            IReadOnlyList<GameUiSchematicView> filtered = RetailSchematicLayout.Filter(
                _controller.ProjectSchematics(), _mode, _discipline);
            if (filtered.Count == 0) return;
            _controller.Craft(filtered[Mathf.Clamp(Page, 0, filtered.Count - 1)].Definition.Id);
            _dirty = true;
            Synchronize();
        }

        private SourceUiButton AddButton(string name, int sourceId, Vector2 topLeft, Action clicked)
        {
            Vector2 size = NativeSize(sourceId);
            RectTransform target = AddRect(name, _window, new Rect(topLeft, size));
            target.gameObject.AddComponent<Image>();
            target.gameObject.AddComponent<Button>();
            target.gameObject.AddComponent<SourceUiImage>();
            SourceUiButton source = target.gameObject.AddComponent<SourceUiButton>();
            source.Bind(_resolver, new UiAssetKey(sourceId, frame: 0), new UiAssetKey(sourceId, frame: 1),
                new UiAssetKey(sourceId, frame: 1), new UiAssetKey(sourceId, frame: 0),
                new UiAssetKey(sourceId, frame: 1));
            source.Button.onClick.AddListener(() => clicked());
            return source;
        }

        private void BindMesHint(GameObject target, int key, string fallback)
        {
            string value = _resolver.Original.TryReadMessage("mes/schematic_text.mes", key, out string source)
                ? source : fallback;
            string owner = $"schematic-help-{key}";
            target.AddComponent<SourceHoverHintTarget>().Bind(
                () => _controller.ShowContextualHelp(owner, value), () => _controller.ClearContextualHelp(owner));
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

        private void AddDynamicImage(string name, int sourceId, Rect rect, bool nativeSize)
        {
            Rect actual = nativeSize ? new Rect(rect.position, NativeSize(sourceId)) : rect;
            SourceUiImage image = AddSourceImage(name, _dynamicLayer, sourceId, actual);
            _dynamic.Add(image.gameObject);
        }

        private void AddDynamicText(string name, Rect rect, string value, int font, Color color)
        {
            RectTransform target = AddRect(name, _dynamicLayer, rect);
            SourceUiBitmapText text = target.gameObject.AddComponent<SourceUiBitmapText>();
            text.Bind(_resolver, value ?? string.Empty, color, font);
            _dynamic.Add(target.gameObject);
        }

        private void AddDynamicParagraph(string name, Rect rect, string value, int font, Color color)
        {
            const float lineHeight = 13f;
            var lines = new List<string>();
            foreach (string paragraph in (value ?? string.Empty).Replace("\r", string.Empty).Split('\n'))
            {
                string current = string.Empty;
                foreach (string word in paragraph.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string candidate = current.Length == 0 ? word : current + " " + word;
                    if (current.Length != 0 && Measure(candidate, font) > rect.width)
                    {
                        lines.Add(current);
                        current = word;
                    }
                    else current = candidate;
                }
                if (current.Length != 0) lines.Add(current);
            }
            int capacity = Mathf.Max(1, Mathf.FloorToInt(rect.height / lineHeight));
            for (int index = 0; index < Mathf.Min(capacity, lines.Count); index++)
                AddDynamicText(name + " " + index,
                    new Rect(rect.x, rect.y + index * lineHeight, rect.width, lineHeight), lines[index], font, color);
        }

        private float Measure(string value, int font)
        {
            float width = 0f;
            foreach (char character in value)
            {
                int frame = (byte)character - 31;
                if (frame >= 0 && _resolver.TryResolve(new UiAssetKey(font, frame: frame), out UiResolvedAsset asset))
                    width += asset.Hotspot.x > 0 ? asset.Hotspot.x : asset.LogicalSize.x;
            }
            return width;
        }

        private Vector2 NativeSize(int sourceId)
            => _resolver.TryResolve(new UiAssetKey(sourceId), out UiResolvedAsset asset)
                ? asset.LogicalSize : new Vector2(32, 32);

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

        private static int ProjectionSignature(IReadOnlyList<GameUiSchematicView> values)
        {
            unchecked
            {
                int hash = values.Count;
                foreach (GameUiSchematicView value in values)
                    hash = hash * 31 + HashCode.Combine(value.Definition.Id.Value, value.PreviewFailure,
                        value.ComponentOne.IsOwned, value.ComponentOne.HasExpertise,
                        value.ComponentTwo.IsOwned, value.ComponentTwo.HasExpertise);
                return hash;
            }
        }

        private static string Friendly(object value)
        {
            string text = value?.ToString() ?? string.Empty;
            return string.Concat(text.Select((character, index) => index > 0 && char.IsUpper(character)
                ? " " + character : character.ToString()));
        }

        private void ClearDynamic()
        {
            foreach (GameObject value in _dynamic)
                if (value != null)
                {
                    value.SetActive(false);
                    if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value);
                }
            _dynamic.Clear();
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
            _dynamicLayer = null;
            _resolver = null;
            _controller = null;
            _session = null;
            _signature = int.MinValue;
            _dirty = true;
        }
    }
}
