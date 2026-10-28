using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.World;
using OpenArcanum.UI;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Arcanum.Runtime.UI
{
    /// <summary>Native 800x600 coordinates transcribed from mainmenu_ui.c.</summary>
    public static class RetailSaveLoadLayout
    {
        public const int BackgroundSourceId = 745;
        public const int PcLensFrameSourceId = 746;
        public const int InfoSourceId = 747;
        public const int ScreenshotFrameSourceId = 748;
        public const int ActionSourceId = 321;
        public const int CancelSourceId = 323;
        public const int DeleteSourceId = 32;
        public const int BodyFontSourceId = 229;
        public const int HeaderFontSourceId = 27;
        public static readonly Rect Window = new(0, 41, 800, 400);
        public static readonly Rect PcLens = new(84, 10, 89, 89);
        public static readonly Rect SlotList = new(42, 120, 145, 213);
        public static readonly Rect Screenshot = new(281, 55, 468, 300);
        public static readonly Rect Name = new(289, 192, 451, 18);
        public static readonly Rect Date = new(448, 126, 139, 18);
        public static readonly Rect Time = new(448, 138, 139, 18);
        public static readonly Rect Character = new(294, 173, 441, 18);
        public static readonly Rect Level = new(547, 126, 109, 18);
        public static readonly Rect Location = new(281, 212, 465, 18);
        public static readonly Rect[] StoryRows =
        {
            new(281, 232, 465, 18), new(281, 252, 465, 18),
            new(281, 272, 465, 18), new(281, 292, 465, 18),
        };
        public static readonly Vector2 Action = new(675, 55);
        public static readonly Vector2 Cancel = new(69, 55);
        public static readonly Vector2 Delete = new(115, 396);
        public static readonly Vector2 Info = new(499, 82);
        public const float SlotRowHeight = 20f;
        public const int VisibleSlotRows = 10;

        public static Rect ScreenRect(int width, int height)
            => new((width - Window.width) * .5f, 41f + (height - 600f) * .5f,
                Window.width, Window.height);

        public static IReadOnlyList<string> VisibleSlotNames(SaveLoadPanelController panel)
        {
            var values = new List<string>();
            if (panel.Mode == SaveLoadPanelMode.Save) values.Add(panel.DraftSaveName);
            values.AddRange(panel.Slots.Select(value => value.DisplayName));
            return values.Take(VisibleSlotRows).ToArray();
        }
    }

    /// <summary>Source presentation only; all persistence remains owned by SessionSaveSlotService.</summary>
    [DisallowMultipleComponent]
    public sealed class RetailSaveLoadView : MonoBehaviour
    {
        private static readonly Color White = new Color32(250, 250, 250, 255);
        private static readonly Color Yellow = new Color32(255, 210, 0, 255);
        private static readonly Color Red = new Color32(255, 50, 50, 255);
        private readonly List<GameObject> _dynamic = new();
        private GameUiController _controller;
        private WorldMapSessionCoordinator _session;
        private UiSkinResolver _resolver;
        private GameObject _canvasRoot;
        private RectTransform _window;
        private RectTransform _dynamicLayer;
        private SourceUiButton _action;
        private SourceUiButton _delete;
        private SourceUiButton _info;
        private bool _showInfo = true;
        private bool _wasVisible;
        private bool _editing;
        private string _cachedThumbnail;
        private int _signature = int.MinValue;
        private Texture2D _thumbnailTexture;
        private Sprite _thumbnailSprite;

        public bool IsAvailable => _resolver != null && _canvasRoot != null;
        public bool IsVisible => IsAvailable && _canvasRoot.activeSelf;
        public RectTransform WindowTransform => _window;
        public bool ShowsInformation => _showInfo;

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
            _session.SaveSlots.ThumbnailProvider = () => _cachedThumbnail;
            BuildPresentation();
            Synchronize();
        }

        public void Synchronize()
        {
            if (!IsAvailable || _controller == null) return;
            bool visible = _controller.Screen == GameUiScreen.SaveLoad && _controller.SaveLoad.IsOpen;
            if (visible && !_wasVisible && _controller.SaveLoad.Mode == SaveLoadPanelMode.Save)
                _cachedThumbnail = CaptureThumbnail();
            _wasVisible = visible;
            if (_canvasRoot.activeSelf != visible) _canvasRoot.SetActive(visible);
            if (!visible) return;
            HandleKeyboard();
            int signature = Signature();
            if (signature != _signature)
            {
                _signature = signature;
                Rebuild();
            }
        }

        public bool ContainsScreenPoint(Vector2 screenBottomLeft)
        {
            if (!IsVisible) return false;
            return RetailSaveLoadLayout.ScreenRect(Screen.width, Screen.height)
                .Contains(new Vector2(screenBottomLeft.x, Screen.height - screenBottomLeft.y));
        }

        private void BuildPresentation()
        {
            _canvasRoot = new GameObject("Source-Faithful Save Load", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(SourceUiRuntime));
            _canvasRoot.transform.SetParent(transform, false);
            _canvasRoot.GetComponent<SourceUiRuntime>().BindResolver(_resolver);
            Canvas canvas = _canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 510;
            CanvasScaler scaler = _canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 1f;

            _window = AddRect("Retail Save Load", (RectTransform)_canvasRoot.transform, new Rect(0, 0, 800, 400));
            _window.anchorMin = _window.anchorMax = new Vector2(.5f, .5f);
            _window.pivot = new Vector2(.5f, .5f);
            _window.anchoredPosition = new Vector2(0f, 59f);
            AddSourceImage("Save Load Background", _window, RetailSaveLoadLayout.BackgroundSourceId,
                new Rect(0, 0, 800, 400));
            RectTransform blocker = AddRect("Save Load Input", _window, new Rect(0, 0, 800, 400));
            Image blockerImage = blocker.gameObject.AddComponent<Image>();
            blockerImage.color = Color.clear;
            blockerImage.raycastTarget = true;

            RectTransform liveLens = AddRect("PC Lens Live Character", _window, RetailSaveLoadLayout.PcLens);
            liveLens.gameObject.AddComponent<RetailPcLensView>().Bind(_session);
            AddSourceImage("PC Lens Frame", _window, RetailSaveLoadLayout.PcLensFrameSourceId,
                RetailSaveLoadLayout.PcLens);

            _action = AddButton("Save Load Action", RetailSaveLoadLayout.ActionSourceId,
                RetailSaveLoadLayout.Action, Activate);
            AddButton("Cancel Save Load", RetailSaveLoadLayout.CancelSourceId,
                RetailSaveLoadLayout.Cancel, () => _controller.Close());
            _delete = AddButton("Delete Save", RetailSaveLoadLayout.DeleteSourceId,
                RetailSaveLoadLayout.Delete, () => _controller.SaveLoad.RequestDelete());
            _info = AddButton("Toggle Save Information", RetailSaveLoadLayout.InfoSourceId,
                RetailSaveLoadLayout.Info, () => { _showInfo = !_showInfo; Invalidate(); });
            BindHint(_action.gameObject, () => _controller.SaveLoad.Mode == SaveLoadPanelMode.Load ? 7020 : 7021,
                "Load Game", "Save Game");
            BindHint(_delete.gameObject, () => 7022, "Delete Game", "Delete Game");
            BindHint(_info.gameObject, () => 7025, "Game Information", "Game Information");
            _dynamicLayer = AddRect("Save Load Dynamic", _window, new Rect(0, 0, 800, 400));
        }

        private void Rebuild()
        {
            ClearDynamic();
            SaveLoadPanelController panel = _controller.SaveLoad;
            IReadOnlyList<string> names = RetailSaveLoadLayout.VisibleSlotNames(panel);
            for (int index = 0; index < names.Count; index++)
            {
                bool newSlot = panel.Mode == SaveLoadPanelMode.Save && index == 0;
                SaveLoadSlotView slot = newSlot ? null : panel.Slots[index - (panel.Mode == SaveLoadPanelMode.Save ? 1 : 0)];
                bool selected = newSlot ? string.IsNullOrEmpty(panel.SelectedSlotId)
                    : string.Equals(panel.SelectedSlotId, slot.SlotId, StringComparison.Ordinal);
                Color color = slot != null && !slot.IsValid ? Red : selected ? Yellow : White;
                float y = RetailSaveLoadLayout.SlotList.y + index * RetailSaveLoadLayout.SlotRowHeight;
                AddDynamicText("Save Slot " + index, new Rect(RetailSaveLoadLayout.SlotList.x, y,
                    RetailSaveLoadLayout.SlotList.width, RetailSaveLoadLayout.SlotRowHeight), names[index],
                    RetailSaveLoadLayout.BodyFontSourceId, color);
                RectTransform target = AddRect("Select Save Slot " + index, _dynamicLayer,
                    new Rect(RetailSaveLoadLayout.SlotList.x, y, RetailSaveLoadLayout.SlotList.width,
                        RetailSaveLoadLayout.SlotRowHeight));
                Image hit = target.gameObject.AddComponent<Image>();
                hit.color = Color.clear;
                Button button = target.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                int captured = index;
                button.onClick.AddListener(() => SelectRow(captured));
                _dynamic.Add(target.gameObject);
            }

            SaveLoadSlotView selectedSlot = panel.Slots.FirstOrDefault(value =>
                string.Equals(value.SlotId, panel.SelectedSlotId, StringComparison.Ordinal));
            bool hasSlot = selectedSlot != null;
            _delete.gameObject.SetActive(hasSlot);
            _delete.SetInteractable(hasSlot);
            _action.SetInteractable(panel.Mode == SaveLoadPanelMode.Save || selectedSlot?.IsValid == true);
            if (_showInfo && selectedSlot?.IsValid == true) DrawInformation(selectedSlot);
            if (panel.Confirmation != SaveLoadConfirmation.None) DrawConfirmation(panel);
            else if (!string.IsNullOrEmpty(panel.ErrorMessage))
                AddCenteredDynamicText("Save Error", new Rect(280, 326, 466, 18), panel.ErrorMessage,
                    RetailSaveLoadLayout.BodyFontSourceId, Red);
        }

        private void DrawInformation(SaveLoadSlotView slot)
        {
            DrawThumbnail(slot.ThumbnailPngBase64);
            AddSourceDynamicImage("Screenshot Frame", RetailSaveLoadLayout.ScreenshotFrameSourceId,
                RetailSaveLoadLayout.Screenshot);
            AddCenteredDynamicText("Save Name", RetailSaveLoadLayout.Name, slot.DisplayName,
                RetailSaveLoadLayout.HeaderFontSourceId, White);
            AddDynamicText("Save Character", RetailSaveLoadLayout.Character, slot.PcName,
                RetailSaveLoadLayout.BodyFontSourceId, White);
            AddDynamicText("Save Level", RetailSaveLoadLayout.Level, "Level " + slot.Level,
                RetailSaveLoadLayout.BodyFontSourceId, White);
            AddRightAlignedDynamicText("Save Date", RetailSaveLoadLayout.Date, slot.GameDate,
                RetailSaveLoadLayout.BodyFontSourceId, White);
            AddRightAlignedDynamicText("Save Time", RetailSaveLoadLayout.Time, slot.GameTime,
                RetailSaveLoadLayout.BodyFontSourceId, White);
            AddCenteredDynamicText("Save Location", RetailSaveLoadLayout.Location, slot.Location,
                RetailSaveLoadLayout.BodyFontSourceId, White);
        }

        private void DrawThumbnail(string base64)
        {
            DestroyThumbnail();
            if (string.IsNullOrWhiteSpace(base64)) return;
            try
            {
                _thumbnailTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!_thumbnailTexture.LoadImage(Convert.FromBase64String(base64))) return;
                _thumbnailSprite = Sprite.Create(_thumbnailTexture,
                    new Rect(0, 0, _thumbnailTexture.width, _thumbnailTexture.height), new Vector2(.5f, .5f), 1f);
                RectTransform target = AddRect("Save Thumbnail", _dynamicLayer, RetailSaveLoadLayout.Screenshot);
                Image image = target.gameObject.AddComponent<Image>();
                image.sprite = _thumbnailSprite;
                image.preserveAspect = false;
                image.raycastTarget = false;
                _dynamic.Add(target.gameObject);
            }
            catch { DestroyThumbnail(); }
        }

        private void DrawConfirmation(SaveLoadPanelController panel)
        {
            RectTransform modal = AddRect("Save Confirmation", _dynamicLayer, new Rect(238, 132, 325, 136));
            _dynamic.Add(modal.gameObject);
            AddSourceDynamicImage("Confirmation Background", 822, new Rect(238, 132, 325, 136));
            AddCenteredDynamicText("Confirmation Text", new Rect(258, 153, 285, 40), panel.ConfirmationMessage,
                RetailSaveLoadLayout.BodyFontSourceId, White);
            AddDynamicButton("Confirm", 823, new Vector2(298, 211), panel.Confirm);
            AddDynamicButton("Cancel Confirmation", 824, new Vector2(436, 211), panel.CancelConfirmation);
        }

        private void SelectRow(int index)
        {
            SaveLoadPanelController panel = _controller.SaveLoad;
            if (panel.Mode == SaveLoadPanelMode.Save && index == 0)
            {
                panel.SelectNewSlot();
                _editing = true;
            }
            else
            {
                int slotIndex = index - (panel.Mode == SaveLoadPanelMode.Save ? 1 : 0);
                if (slotIndex >= 0 && slotIndex < panel.Slots.Count)
                {
                    panel.SelectSlot(panel.Slots[slotIndex].SlotId);
                    _editing = panel.Mode == SaveLoadPanelMode.Save;
                }
            }
            Invalidate();
        }

        private void Activate()
        {
            SaveLoadPanelController panel = _controller.SaveLoad;
            if (panel.Mode == SaveLoadPanelMode.Load)
            {
                _controller.RequestLoad();
                return;
            }
            panel.RequestSave();
            if (panel.Confirmation == SaveLoadConfirmation.None && string.IsNullOrEmpty(panel.ErrorMessage))
                _controller.Close();
            Invalidate();
        }

        private void HandleKeyboard()
        {
            SaveLoadPanelController panel = _controller.SaveLoad;
            if (panel.Confirmation != SaveLoadConfirmation.None)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) panel.Confirm();
                else if (Input.GetKeyDown(KeyCode.Escape)) panel.CancelConfirmation();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape)) { _controller.Close(); return; }
            if (Input.GetKeyDown(KeyCode.Delete)) { panel.RequestDelete(); Invalidate(); return; }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) { Activate(); return; }
            if (Input.GetKeyDown(KeyCode.UpArrow)) MoveSelection(-1);
            if (Input.GetKeyDown(KeyCode.DownArrow)) MoveSelection(1);
            if (!_editing || panel.Mode != SaveLoadPanelMode.Save) return;
            string name = panel.DraftSaveName;
            if (Input.GetKeyDown(KeyCode.Backspace) && name.Length > 0) name = name.Substring(0, name.Length - 1);
            foreach (char character in Input.inputString)
                if (!char.IsControl(character) && character >= 32 && character <= 126 && name.Length < 32)
                    name += character;
            if (name != panel.DraftSaveName) { panel.SetDraftSaveName(name); Invalidate(); }
        }

        private void MoveSelection(int delta)
        {
            SaveLoadPanelController panel = _controller.SaveLoad;
            int offset = panel.Mode == SaveLoadPanelMode.Save ? 1 : 0;
            int current = string.IsNullOrEmpty(panel.SelectedSlotId) ? 0
                : panel.Slots.ToList().FindIndex(value => value.SlotId == panel.SelectedSlotId) + offset;
            int count = Mathf.Min(RetailSaveLoadLayout.VisibleSlotRows, panel.Slots.Count + offset);
            if (count <= 0) return;
            SelectRow(Mathf.Clamp(current + delta, 0, count - 1));
        }

        private string CaptureThumbnail()
        {
            if (!Application.isPlaying) return null;
            Camera camera = Camera.main;
            if (camera == null) return null;
            RenderTexture source = null;
            RenderTexture target = null;
            RenderTexture previousTarget = camera.targetTexture;
            Rect previousRect = camera.rect;
            RenderTexture previous = RenderTexture.active;
            try
            {
                // Source captures the centered 800x600 isometric window, not the surrounding
                // interface windows. Rendering the world camera directly preserves that rule and
                // prevents HUD/menu presentation from leaking into a saved-game preview.
                source = RenderTexture.GetTemporary(800, 600, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = source;
                camera.rect = new Rect(0f, 0f, 1f, 1f);
                camera.Render();
                target = RenderTexture.GetTemporary(468, 300, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                var scaled = new Texture2D(468, 300, TextureFormat.RGB24, false);
                scaled.ReadPixels(new Rect(0, 0, 468, 300), 0, 0);
                scaled.Apply();
                string result = Convert.ToBase64String(scaled.EncodeToPNG());
                Object.Destroy(scaled);
                return result;
            }
            catch { return null; }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.rect = previousRect;
                RenderTexture.active = previous;
                if (target != null) RenderTexture.ReleaseTemporary(target);
                if (source != null) RenderTexture.ReleaseTemporary(source);
            }
        }

        private int Signature()
        {
            unchecked
            {
                SaveLoadPanelController panel = _controller.SaveLoad;
                int hash = HashCode.Combine(panel.Mode, panel.SelectedSlotId, panel.DraftSaveName,
                    panel.Confirmation, panel.ErrorMessage, _showInfo);
                foreach (SaveLoadSlotView slot in panel.Slots)
                    hash = hash * 31 + HashCode.Combine(slot.SlotId, slot.DisplayName, slot.GameDate,
                        slot.GameTime, slot.Location, slot.IsValid, slot.ThumbnailPngBase64?.Length ?? 0);
                return hash;
            }
        }

        private SourceUiButton AddButton(string name, int sourceId, Vector2 topLeft, Action clicked)
        {
            Vector2 size = NativeSize(sourceId);
            RectTransform target = AddRect(name, _window, new Rect(topLeft, size));
            target.gameObject.AddComponent<Image>();
            target.gameObject.AddComponent<Button>();
            target.gameObject.AddComponent<SourceUiImage>();
            SourceUiButton button = target.gameObject.AddComponent<SourceUiButton>();
            UiAssetKey normal = Frame(sourceId, 0);
            UiAssetKey active = Frame(sourceId, 1);
            button.Bind(_resolver, normal, active, active, normal, active);
            button.Button.onClick.AddListener(() => clicked());
            return button;
        }

        private void AddDynamicButton(string name, int sourceId, Vector2 topLeft, Action clicked)
        {
            SourceUiButton button = AddButton(name, sourceId, topLeft, clicked);
            button.transform.SetParent(_dynamicLayer, false);
            _dynamic.Add(button.gameObject);
        }

        private UiAssetKey Frame(int sourceId, int frame)
            => _resolver.TryResolve(new UiAssetKey(sourceId, frame: frame), out _)
                ? new UiAssetKey(sourceId, frame: frame) : new UiAssetKey(sourceId, frame: 0);

        private void BindHint(GameObject target, Func<int> key, string loadFallback, string saveFallback)
        {
            string owner = "save-load-help-" + target.name;
            target.AddComponent<SourceHoverHintTarget>().Bind(() =>
            {
                string fallback = _controller.SaveLoad.Mode == SaveLoadPanelMode.Load ? loadFallback : saveFallback;
                string value = _resolver.Original.TryReadMessage("mes/mainmenu.mes", key(), out string source)
                    ? source : fallback;
                _controller.ShowContextualHelp(owner, value);
            }, () => _controller.ClearContextualHelp(owner));
        }

        private void AddSourceDynamicImage(string name, int sourceId, Rect rect)
        {
            SourceUiImage image = AddSourceImage(name, _dynamicLayer, sourceId, rect);
            _dynamic.Add(image.gameObject);
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

        private void AddDynamicText(string name, Rect rect, string value, int font, Color color)
        {
            RectTransform target = AddRect(name, _dynamicLayer, rect);
            SourceUiBitmapText text = target.gameObject.AddComponent<SourceUiBitmapText>();
            text.Bind(_resolver, value ?? string.Empty, color, font);
            _dynamic.Add(target.gameObject);
        }

        private void AddCenteredDynamicText(string name, Rect rect, string value, int font, Color color)
        {
            RectTransform target = AddRect(name, _dynamicLayer, rect);
            SourceUiBitmapText text = target.gameObject.AddComponent<SourceUiBitmapText>();
            text.Bind(_resolver, value ?? string.Empty, color, font);
            target.anchoredPosition += new Vector2(Mathf.Max(0f, (rect.width - text.LogicalSize.x) * .5f), 0f);
            _dynamic.Add(target.gameObject);
        }

        private void AddRightAlignedDynamicText(string name, Rect rect, string value, int font, Color color)
        {
            RectTransform target = AddRect(name, _dynamicLayer, rect);
            SourceUiBitmapText text = target.gameObject.AddComponent<SourceUiBitmapText>();
            text.Bind(_resolver, value ?? string.Empty, color, font);
            target.anchoredPosition += new Vector2(Mathf.Max(0f, rect.width - text.LogicalSize.x), 0f);
            _dynamic.Add(target.gameObject);
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

        private void Invalidate()
        {
            _signature = int.MinValue;
            if (this != null && IsVisible) Synchronize();
        }

        private void ClearDynamic()
        {
            DestroyThumbnail();
            foreach (GameObject value in _dynamic)
                if (value != null)
                {
                    value.SetActive(false);
                    if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value);
                }
            _dynamic.Clear();
        }

        private void DestroyThumbnail()
        {
            if (_thumbnailSprite != null)
            {
                if (Application.isPlaying) Object.Destroy(_thumbnailSprite); else Object.DestroyImmediate(_thumbnailSprite);
                _thumbnailSprite = null;
            }
            if (_thumbnailTexture != null)
            {
                if (Application.isPlaying) Object.Destroy(_thumbnailTexture); else Object.DestroyImmediate(_thumbnailTexture);
                _thumbnailTexture = null;
            }
        }

        private void OnDestroy() => Cleanup();

        private void Cleanup()
        {
            if (_resolver != null) _resolver.PresentationInvalidated -= Invalidate;
            if (_session?.SaveSlots != null && _session.SaveSlots.ThumbnailProvider != null)
                _session.SaveSlots.ThumbnailProvider = null;
            ClearDynamic();
            if (_canvasRoot != null)
            {
                _canvasRoot.SetActive(false);
                if (Application.isPlaying) Object.Destroy(_canvasRoot); else Object.DestroyImmediate(_canvasRoot);
            }
            _canvasRoot = null;
            _window = null;
            _dynamicLayer = null;
            _controller = null;
            _session = null;
            _resolver = null;
            _signature = int.MinValue;
            _wasVisible = false;
        }
    }
}
