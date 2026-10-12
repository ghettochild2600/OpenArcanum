using System;
using System.Collections.Generic;
using Arcanum.Formats.Quest;
using Arcanum.Runtime.Campaign;
using Arcanum.Runtime.World;
using OpenArcanum.UI;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Arcanum.Runtime.UI
{
    public enum RetailLogbookTab
    {
        RumorsAndNotes,
        Quests,
        Reputations,
        BlessingsAndCurses,
        KillsAndInjuries,
        Background,
        Keys,
    }

    public readonly struct RetailLogbookTabLayout
    {
        public RetailLogbookTab Tab { get; }
        public string Label { get; }
        public int SourceId { get; }
        public Vector2 Position { get; }

        public RetailLogbookTabLayout(RetailLogbookTab tab, string label, int sourceId, float x, float y)
        {
            Tab = tab;
            Label = label;
            SourceId = sourceId;
            Position = new Vector2(x, y);
        }
    }

    /// <summary>Source geometry from <c>logbook_ui.c</c>.</summary>
    public static class RetailLogbookLayout
    {
        public const int BodyFontSourceId = 229;
        public const int HeaderFontSourceId = 27;
        public const float LineHeight = 15f;
        public static readonly Rect Window = new(0f, 41f, 800f, 400f);
        public static readonly Rect LeftContent = new(222f, 72f, 215f, 270f);
        public static readonly Rect RightContent = new(468f, 72f, 215f, 270f);
        public static readonly Rect LeftHeader = new(236f, 43f, 187f, 19f);
        public static readonly Rect RightHeader = new(482f, 43f, 187f, 19f);
        public static readonly Rect LeftFooter = new(222f, 346f, 215f, 15f);
        public static readonly Rect RightFooter = new(468f, 346f, 215f, 15f);
        public static readonly RetailLogbookTabLayout[] Tabs =
        {
            new(RetailLogbookTab.RumorsAndNotes, "Rumors & Notes", 265, 696f, 83f),
            new(RetailLogbookTab.Quests, "Quests", 267, 696f, 129f),
            new(RetailLogbookTab.Reputations, "Reputations", 271, 696f, 175f),
            new(RetailLogbookTab.BlessingsAndCurses, "Blessings & Curses", 266, 695f, 221f),
            new(RetailLogbookTab.KillsAndInjuries, "Kills & Injuries", 270, 696f, 267f),
            new(RetailLogbookTab.Background, "Background", 268, 696f, 313f),
            new(RetailLogbookTab.Keys, "Keys", 269, 696f, 359f),
        };

        public static Rect ScreenRect(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            return new Rect((width - Window.width) * .5f, 41f + (height - 600f) * .5f,
                Window.width, Window.height);
        }

        public static int PageLineCapacity => Mathf.FloorToInt(LeftContent.height / LineHeight);

        public static UiAssetKey ButtonFrame(int sourceId, int frame)
            => new(sourceId, frame: frame);
    }

    public sealed class RetailLogbookQuestBlock
    {
        public QuestJournalEntry Entry { get; }
        public IReadOnlyList<string> Lines { get; }

        public RetailLogbookQuestBlock(QuestJournalEntry entry, IReadOnlyList<string> lines)
        {
            Entry = entry;
            Lines = lines ?? throw new ArgumentNullException(nameof(lines));
        }
    }

    /// <summary>Pure two-page packing used by the runtime view and EditMode checks.</summary>
    public static class RetailLogbookPaginator
    {
        public static IReadOnlyList<string> Wrap(string value, float width, Func<char, float> measure)
        {
            if (measure == null) throw new ArgumentNullException(nameof(measure));
            var lines = new List<string>();
            foreach (string paragraph in (value ?? string.Empty).Replace("\r", string.Empty).Split('\n'))
            {
                string[] words = paragraph.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0)
                {
                    lines.Add(string.Empty);
                    continue;
                }
                string line = string.Empty;
                foreach (string word in words)
                {
                    string candidate = line.Length == 0 ? word : line + " " + word;
                    if (line.Length != 0 && Measure(candidate, measure) > width)
                    {
                        lines.Add(line);
                        line = word;
                    }
                    else line = candidate;
                }
                lines.Add(line);
            }
            return lines;
        }

        public static int LastVisibleEntry(IReadOnlyList<RetailLogbookQuestBlock> blocks, int start,
            int linesPerPage = 18)
        {
            if (blocks == null) throw new ArgumentNullException(nameof(blocks));
            if (blocks.Count == 0) return -1;
            if (start < 0 || start >= blocks.Count) throw new ArgumentOutOfRangeException(nameof(start));
            if (linesPerPage <= 0) throw new ArgumentOutOfRangeException(nameof(linesPerPage));
            int page = 0;
            int remaining = linesPerPage;
            int last = start - 1;
            for (int index = start; index < blocks.Count; index++)
            {
                int required = blocks[index].Lines.Count + (index < blocks.Count - 1 ? 1 : 0);
                if (required > remaining)
                {
                    if (page == 1) break;
                    page = 1;
                    remaining = linesPerPage;
                }
                // The source permits a first-on-page entry to truncate to the page boundary.
                remaining -= Mathf.Min(required, remaining);
                last = index;
            }
            return last;
        }

        public static IReadOnlyList<IReadOnlyList<string>> Paginate(string value, float width,
            int linesPerPage, Func<char, float> measure)
        {
            if (linesPerPage <= 0) throw new ArgumentOutOfRangeException(nameof(linesPerPage));
            IReadOnlyList<string> lines = Wrap(value, width, measure);
            var pages = new List<IReadOnlyList<string>>();
            for (int start = 0; start < lines.Count; start += linesPerPage)
            {
                int count = Math.Min(linesPerPage, lines.Count - start);
                var page = new List<string>(count);
                for (int index = 0; index < count; index++) page.Add(lines[start + index]);
                pages.Add(page);
            }
            return pages;
        }

        private static float Measure(string value, Func<char, float> measure)
        {
            float width = 0f;
            foreach (char character in value) width += measure(character);
            return width;
        }
    }

    /// <summary>
    /// Source-backed Logbook shell. Quest facts remain owned by CampaignStateService/JournalProjectionService.
    /// Tabs without complete authority are shown but fail closed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RetailLogbookView : MonoBehaviour
    {
        private static readonly Color Ink = new Color32(20, 16, 12, 255);
        private static readonly Color Active = new Color32(35, 72, 165, 255);
        private static readonly Color Completed = new Color32(30, 112, 45, 255);
        private static readonly Color Failed = new Color32(165, 35, 30, 255);

        private readonly Dictionary<RetailLogbookTab, SourceUiButton> _tabs = new();
        private readonly List<GameObject> _pageText = new();
        private readonly List<int> _spreadStarts = new() { 0 };
        private GameUiController _controller;
        private WorldMapSessionCoordinator _session;
        private UiSkinResolver _resolver;
        private GameObject _canvasRoot;
        private RectTransform _window;
        private RectTransform _pageLayer;
        private SourceUiButton _left;
        private SourceUiButton _right;
        private SourceUiBitmapText _notice;
        private RetailLogbookTab _tab = RetailLogbookTab.Quests;
        private int _spreadIndex;
        private int _signature = int.MinValue;
        private int _backgroundSignature = int.MinValue;
        private int _lastVisibleEntry = -1;
        private int _projectedEntryCount;
        private bool _dirty = true;

        public bool IsAvailable => _resolver != null && _canvasRoot != null;
        public bool IsVisible => IsAvailable && _canvasRoot.activeSelf;
        public RetailLogbookTab SelectedTab => _tab;
        public int CurrentLeftPage => _spreadIndex * 2 + 1;
        public int SpreadIndex => _spreadIndex;
        public int LastVisibleEntry => _lastVisibleEntry;
        public int ProjectedEntryCount => _projectedEntryCount;
        public int RenderedLineCount => _pageText.Count;
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
            bool visible = _controller.HasPlayer && _controller.Screen == GameUiScreen.Journal;
            if (_canvasRoot.activeSelf != visible) _canvasRoot.SetActive(visible);
            if (!visible) return;
            IReadOnlyList<QuestJournalEntry> entries = _controller.ProjectJournal();
            GameUiBackgroundLogbookView background = _controller.ProjectBackgroundLogbook();
            int backgroundSignature = BackgroundSignature(background);
            if (_tab == RetailLogbookTab.Background && _backgroundSignature != int.MinValue
                && backgroundSignature != _backgroundSignature)
                ResetPagination();
            _backgroundSignature = backgroundSignature;
            int signature = ProjectionSignature(entries, background);
            if (_dirty || signature != _signature)
            {
                _dirty = false;
                _signature = signature;
                RebuildPages(entries, background);
            }
        }

        public bool ContainsScreenPoint(Vector2 screenBottomLeft)
        {
            if (!IsVisible) return false;
            Vector2 topLeft = new(screenBottomLeft.x, Screen.height - screenBottomLeft.y);
            return RetailLogbookLayout.ScreenRect(Screen.width, Screen.height).Contains(topLeft);
        }

        public void SelectTab(RetailLogbookTab tab)
        {
            _tab = tab;
            ResetPagination();
            _dirty = true;
            Synchronize();
        }

        public void TurnLeft()
        {
            if (_spreadIndex <= 0) return;
            _spreadIndex--;
            _dirty = true;
            Synchronize();
        }

        public void TurnRight()
        {
            if (_lastVisibleEntry < 0 || _lastVisibleEntry >= _projectedEntryCount - 1) return;
            int next = _lastVisibleEntry + 1;
            _spreadIndex++;
            if (_spreadStarts.Count <= _spreadIndex) _spreadStarts.Add(next);
            else _spreadStarts[_spreadIndex] = next;
            _dirty = true;
            Synchronize();
        }

        private void BuildPresentation()
        {
            _canvasRoot = new GameObject("Source-Faithful Logbook", typeof(RectTransform), typeof(Canvas),
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

            _window = AddRect("Retail Logbook", (RectTransform)_canvasRoot.transform, new Rect(0, 0, 800, 400));
            _window.anchorMin = _window.anchorMax = new Vector2(.5f, .5f);
            _window.pivot = new Vector2(.5f, .5f);
            _window.anchoredPosition = new Vector2(0f, 59f);
            AddSourceImage("Logbook Sidebar", _window, 264, new Rect(0, 0, 165, 400));
            AddSourceImage("Book Pages", _window, 260, new Rect(165, 0, 635, 400));
            AddSourceImage("Quest Border", _window, 261, new Rect(172, 23, 561, 355));
            RectTransform blocker = AddRect("Logbook Input", _window, new Rect(0, 0, 800, 400));
            Image blockerImage = blocker.gameObject.AddComponent<Image>();
            blockerImage.color = Color.clear;
            blockerImage.raycastTarget = true;

            SourceUiImage lens = AddSourceImage("PC Lens", _window, 198, new Rect(25, 24, 89, 89));
            Button close = lens.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.onClick.AddListener(() => _controller.Close());
            lens.Image.raycastTarget = true;

            foreach (RetailLogbookTabLayout layout in RetailLogbookLayout.Tabs)
            {
                SourceUiButton button = AddButton(layout.Label, _window, layout.SourceId, layout.Position,
                    () => SelectTab(layout.Tab));
                button.SetSelected(layout.Tab == RetailLogbookTab.Quests);
                _tabs.Add(layout.Tab, button);
            }
            _left = AddButton("Previous Page", _window, 272, new Vector2(213, 77), TurnLeft);
            _right = AddButton("Next Page", _window, 273, new Vector2(675, 77), TurnRight);
            _notice = AddText("Unavailable Tab Notice", _window, new Rect(18, 338, 142, 32),
                RetailLogbookLayout.BodyFontSourceId, Failed);
            _notice.Bind(_resolver, string.Empty, Failed, RetailLogbookLayout.BodyFontSourceId);
            _pageLayer = AddRect("Logbook Page Text", _window, new Rect(0, 0, 800, 400));
        }

        private void RebuildPages(IReadOnlyList<QuestJournalEntry> entries,
            GameUiBackgroundLogbookView background)
        {
            ClearPageText();
            _notice.Bind(_resolver, string.Empty, Failed, RetailLogbookLayout.BodyFontSourceId);
            foreach ((RetailLogbookTab tab, SourceUiButton button) in _tabs)
                button.SetSelected(tab == _tab);

            string tabLabel = Array.Find(RetailLogbookLayout.Tabs, value => value.Tab == _tab).Label;
            AddCenteredText("Left Header", RetailLogbookLayout.LeftHeader, $"-{tabLabel}-",
                RetailLogbookLayout.HeaderFontSourceId, Ink);
            AddCenteredText("Right Header", RetailLogbookLayout.RightHeader, $"-{tabLabel}-",
                RetailLogbookLayout.HeaderFontSourceId, Ink);
            AddCenteredText("Left Page", RetailLogbookLayout.LeftFooter, $"-{CurrentLeftPage}-",
                RetailLogbookLayout.BodyFontSourceId, Ink);
            AddCenteredText("Right Page", RetailLogbookLayout.RightFooter, $"-{CurrentLeftPage + 1}-",
                RetailLogbookLayout.BodyFontSourceId, Ink);

            if (_tab == RetailLogbookTab.Background)
            {
                RebuildBackgroundPages(background);
                return;
            }

            if (_tab != RetailLogbookTab.Quests)
            {
                _lastVisibleEntry = -1;
                _projectedEntryCount = 0;
                AddPageLine(0, 0, $"No {tabLabel.ToLowerInvariant()} entries are available.", Ink, false);
                _left.SetInteractable(false);
                _right.SetInteractable(false);
                return;
            }

            var blocks = new List<RetailLogbookQuestBlock>();
            foreach (QuestJournalEntry entry in entries)
            {
                var lines = new List<string>();
                lines.AddRange(RetailLogbookPaginator.Wrap(
                    $"Day {entry.Timestamp.Days + 1}  {entry.StateLabel}",
                    RetailLogbookLayout.LeftContent.width, GlyphAdvance));
                lines.AddRange(RetailLogbookPaginator.Wrap(entry.Description,
                    RetailLogbookLayout.LeftContent.width, GlyphAdvance));
                blocks.Add(new RetailLogbookQuestBlock(entry, lines));
            }
            _projectedEntryCount = blocks.Count;

            int start = blocks.Count == 0 ? 0 : Mathf.Clamp(_spreadStarts[Mathf.Min(_spreadIndex,
                _spreadStarts.Count - 1)], 0, blocks.Count - 1);
            _lastVisibleEntry = blocks.Count == 0 ? -1 : RetailLogbookPaginator.LastVisibleEntry(blocks, start,
                RetailLogbookLayout.PageLineCapacity);
            if (blocks.Count == 0)
            {
                AddPageLine(0, 0, "No known quests.", Ink, false);
            }
            else
            {
                int page = 0;
                int line = 0;
                for (int index = start; index <= _lastVisibleEntry; index++)
                {
                    RetailLogbookQuestBlock block = blocks[index];
                    int required = block.Lines.Count + (index < blocks.Count - 1 ? 1 : 0);
                    if (required > RetailLogbookLayout.PageLineCapacity - line)
                    {
                        page = 1;
                        line = 0;
                    }
                    int available = RetailLogbookLayout.PageLineCapacity - line;
                    int count = Mathf.Min(block.Lines.Count, available);
                    Color color = EntryColor(block.Entry.State);
                    bool strike = block.Entry.State is QuestState.Completed or QuestState.OtherCompleted
                        or QuestState.Botched;
                    for (int offset = 0; offset < count; offset++)
                        AddPageLine(page, line++, block.Lines[offset], color, strike);
                    if (line < RetailLogbookLayout.PageLineCapacity) line++;
                }
            }
            _left.SetInteractable(_spreadIndex > 0);
            _right.SetInteractable(_lastVisibleEntry >= 0 && _lastVisibleEntry < entries.Count - 1);
        }

        private void RebuildBackgroundPages(GameUiBackgroundLogbookView background)
        {
            if (background == null)
            {
                _lastVisibleEntry = -1;
                _projectedEntryCount = 0;
                AddPageLine(0, 0, "No background entries are available.", Ink, false);
                _left.SetInteractable(false);
                _right.SetInteractable(false);
                return;
            }

            IReadOnlyList<IReadOnlyList<string>> pages = RetailLogbookPaginator.Paginate(background.Body,
                RetailLogbookLayout.LeftContent.width, RetailLogbookLayout.PageLineCapacity, GlyphAdvance);
            _projectedEntryCount = pages.Count;
            int start = pages.Count == 0 ? 0 : Mathf.Clamp(_spreadStarts[Mathf.Min(_spreadIndex,
                _spreadStarts.Count - 1)], 0, pages.Count - 1);
            _lastVisibleEntry = pages.Count == 0 ? -1 : Math.Min(start + 1, pages.Count - 1);
            for (int index = start; index <= _lastVisibleEntry; index++)
            {
                IReadOnlyList<string> page = pages[index];
                for (int line = 0; line < page.Count; line++)
                    AddPageLine(index - start, line, page[line], Ink, false);
            }
            _left.SetInteractable(_spreadIndex > 0);
            _right.SetInteractable(_lastVisibleEntry >= 0 && _lastVisibleEntry < pages.Count - 1);
        }

        private void AddPageLine(int page, int line, string value, Color color, bool strike)
        {
            Rect pageRect = page == 0 ? RetailLogbookLayout.LeftContent : RetailLogbookLayout.RightContent;
            Rect rect = new(pageRect.x, pageRect.y + line * RetailLogbookLayout.LineHeight,
                pageRect.width, RetailLogbookLayout.LineHeight);
            SourceUiBitmapText text = AddText("Quest Text", _pageLayer, rect,
                RetailLogbookLayout.BodyFontSourceId, color);
            text.Bind(_resolver, value ?? string.Empty, color, RetailLogbookLayout.BodyFontSourceId);
            _pageText.Add(text.gameObject);
            if (!strike || string.IsNullOrEmpty(value)) return;
            RectTransform rule = AddRect("Quest Strikethrough", _pageLayer,
                new Rect(rect.x, rect.y + 7f, Mathf.Min(text.LogicalSize.x, rect.width), 1f));
            Image image = rule.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            _pageText.Add(rule.gameObject);
        }

        private void AddCenteredText(string name, Rect rect, string value, int font, Color color)
        {
            SourceUiBitmapText text = AddText(name, _pageLayer, rect, font, color);
            text.Bind(_resolver, value, color, font);
            RectTransform transform = (RectTransform)text.transform;
            transform.anchoredPosition += new Vector2(Mathf.Max(0, (rect.width - text.LogicalSize.x) * .5f), 0);
            _pageText.Add(text.gameObject);
        }

        private float GlyphAdvance(char character)
        {
            int frame = (byte)character - 31;
            if (frame < 0 || !_resolver.TryResolve(new UiAssetKey(RetailLogbookLayout.BodyFontSourceId, frame: frame),
                    out UiResolvedAsset asset)) return 0f;
            return asset.Hotspot.x > 0 ? asset.Hotspot.x : asset.LogicalSize.x;
        }

        private static Color EntryColor(QuestState state) => state switch
        {
            QuestState.Accepted or QuestState.Achieved => Active,
            QuestState.Completed => Completed,
            QuestState.OtherCompleted or QuestState.Botched => Failed,
            _ => Ink,
        };

        private static int ProjectionSignature(IReadOnlyList<QuestJournalEntry> entries,
            GameUiBackgroundLogbookView background)
        {
            unchecked
            {
                int hash = entries.Count;
                foreach (QuestJournalEntry entry in entries)
                    hash = hash * 31 + HashCode.Combine(entry.QuestId, entry.State, entry.Timestamp, entry.Description);
                hash = hash * 31 + BackgroundSignature(background);
                return hash;
            }
        }

        private static int BackgroundSignature(GameUiBackgroundLogbookView background)
            => background == null ? 0 : HashCode.Combine(background.BackgroundId, background.TextId, background.Body);

        private void ResetPagination()
        {
            _spreadIndex = 0;
            _spreadStarts.Clear();
            _spreadStarts.Add(0);
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

        private SourceUiButton AddButton(string name, RectTransform parent, int sourceId, Vector2 topLeft,
            Action clicked)
        {
            Vector2 size = _resolver.TryResolve(new UiAssetKey(sourceId), out UiResolvedAsset asset)
                ? asset.LogicalSize : new Vector2(32, 32);
            RectTransform target = AddRect(name, parent, new Rect(topLeft.x, topLeft.y, size.x, size.y));
            target.gameObject.AddComponent<Image>();
            target.gameObject.AddComponent<Button>();
            target.gameObject.AddComponent<SourceUiImage>();
            SourceUiButton source = target.gameObject.AddComponent<SourceUiButton>();
            source.Bind(_resolver, RetailLogbookLayout.ButtonFrame(sourceId, 0),
                RetailLogbookLayout.ButtonFrame(sourceId, 1),
                RetailLogbookLayout.ButtonFrame(sourceId, Mathf.Min(2, sourceId is 272 or 273 ? 2 : 1)),
                RetailLogbookLayout.ButtonFrame(sourceId, 0), RetailLogbookLayout.ButtonFrame(sourceId, 1));
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

        private void Invalidate()
        {
            _dirty = true;
            if (this != null && IsVisible) Synchronize();
        }

        private void ClearPageText()
        {
            foreach (GameObject item in _pageText)
            {
                if (item == null) continue;
                item.SetActive(false);
                if (Application.isPlaying) Object.Destroy(item); else Object.DestroyImmediate(item);
            }
            _pageText.Clear();
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
            _tabs.Clear();
            _pageText.Clear();
            _canvasRoot = null;
            _window = null;
            _pageLayer = null;
            _left = null;
            _right = null;
            _notice = null;
            _resolver = null;
            _dirty = true;
            _signature = int.MinValue;
            _backgroundSignature = int.MinValue;
            _projectedEntryCount = 0;
        }
    }
}
