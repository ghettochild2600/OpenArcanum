using System;
using System.Collections.Generic;
using Arcanum.Runtime.Dialogue;
using Arcanum.Runtime.World;
using OpenArcanum.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Arcanum.Runtime.UI
{
    /// <summary>Source geometry for the ordinary <c>tc</c> conversation overlay.</summary>
    public static class RetailDialogueLayout
    {
        public const int ResponseFontSourceId = 229;
        public const int NpcBubbleFontSourceId = 229;
        public const int MaximumResponses = 5;
        public const float NpcBubbleMaximumWidth = 200f;
        public const float NpcBubbleMaximumHeight = 200f;
        public const float NpcBubbleLineHeight = 14f;
        public const float ResponseWidth = 400f;
        public const float ResponseHeight = 100f;
        public const float BottomHudHeight = 159f;
        public const float RowHeight = 20f;

        public static Rect ResponseScreenRect(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            return new Rect((width - ResponseWidth) * .5f,
                height - BottomHudHeight - ResponseHeight, ResponseWidth, ResponseHeight);
        }

        public static Rect ResponseRowRect(int index)
        {
            if (index < 0 || index >= MaximumResponses) throw new ArgumentOutOfRangeException(nameof(index));
            return new Rect(6f, index * RowHeight, ResponseWidth - 12f, RowHeight);
        }

        public static int VisibleResponseCount(int available)
            => Mathf.Clamp(available, 0, MaximumResponses);
    }

    /// <summary>
    /// Disposable UI-F.1 projection over <see cref="ProductionDialogueSession"/>. The session remains the sole
    /// owner of line selection, conditions, effects, training, voice, and completion.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RetailDialogueView : MonoBehaviour
    {
        private static readonly Color ResponseNormal = Color.white;
        private static readonly Color ResponseHover = new Color32(244, 214, 67, 255);
        private static readonly Color ResponseShadow = new Color32(20, 14, 10, 230);

        private readonly SourceUiBitmapText[] _responseShadows =
            new SourceUiBitmapText[RetailDialogueLayout.MaximumResponses];
        private readonly SourceUiBitmapText[] _responseTexts =
            new SourceUiBitmapText[RetailDialogueLayout.MaximumResponses];
        private readonly Button[] _responseButtons = new Button[RetailDialogueLayout.MaximumResponses];
        private readonly string[] _responseValues = new string[RetailDialogueLayout.MaximumResponses];

        private GameUiController _controller;
        private WorldMapSessionCoordinator _session;
        private UiSkinResolver _resolver;
        private GameObject _canvasRoot;
        private RectTransform _responsePanel;
        private RectTransform _bubble;
        private string _bubbleValue = string.Empty;

        public bool IsAvailable => _resolver != null && _canvasRoot != null;
        public bool IsVisible => IsAvailable && _canvasRoot.activeSelf;
        public int CurrentResponseCount { get; private set; }
        public bool OverflowDetected { get; private set; }
        public Rect ResponseRect => RetailDialogueLayout.ResponseScreenRect(Screen.width, Screen.height);
        public bool NpcBubbleVisible => _bubble != null && _bubble.gameObject.activeSelf;

        public static Color ResponseTint(bool active, bool pointerInside)
            => active && pointerInside ? ResponseHover : ResponseNormal;

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
            BuildPresentation();
            Synchronize();
        }

        public void Synchronize()
        {
            if (!IsAvailable || _controller == null) return;
            ProductionDialogueSession dialogue = _controller.Dialogue;
            bool visible = _controller.Screen == GameUiScreen.Dialogue
                           && dialogue?.Phase == DialogueSessionPhase.AwaitingPlayerChoice;
            if (_canvasRoot.activeSelf != visible) _canvasRoot.SetActive(visible);
            if (!visible) return;

            int available = dialogue.AvailableResponses.Count;
            CurrentResponseCount = RetailDialogueLayout.VisibleResponseCount(available);
            OverflowDetected = available > RetailDialogueLayout.MaximumResponses;
            for (int index = 0; index < _responseTexts.Length; index++)
            {
                bool active = !OverflowDetected && index < CurrentResponseCount;
                _responseButtons[index].gameObject.SetActive(active);
                _responseButtons[index].interactable = active;
                if (!active) continue;
                string value = $"{index + 1}. {dialogue.AvailableResponses[index].Text}";
                if (!string.Equals(_responseValues[index], value, StringComparison.Ordinal))
                {
                    _responseValues[index] = value;
                    _responseShadows[index].Bind(_resolver, value, ResponseShadow,
                        RetailDialogueLayout.ResponseFontSourceId);
                    _responseTexts[index].Bind(_resolver, value, ResponseNormal,
                        RetailDialogueLayout.ResponseFontSourceId);
                }
                RefreshResponseTint(index);
            }
            if (OverflowDetected)
            {
                const string overflow = "Dialogue response list exceeds the source five-row boundary.";
                _responseButtons[0].gameObject.SetActive(true);
                _responseButtons[0].interactable = false;
                if (!string.Equals(_responseValues[0], overflow, StringComparison.Ordinal))
                {
                    _responseValues[0] = overflow;
                    _responseShadows[0].Bind(_resolver, overflow, ResponseShadow,
                        RetailDialogueLayout.ResponseFontSourceId);
                    _responseTexts[0].Bind(_resolver, overflow, new Color32(230, 90, 70, 255),
                        RetailDialogueLayout.ResponseFontSourceId);
                }
            }
            string npcText = dialogue.NpcText ?? string.Empty;
            if (!string.Equals(_bubbleValue, npcText, StringComparison.Ordinal))
            {
                _bubbleValue = npcText;
                RebuildNpcBubble(npcText);
            }
            _bubble.gameObject.SetActive(!string.IsNullOrWhiteSpace(npcText));
            PositionNpcBubble(dialogue.NpcIdentity);
        }

        public bool ContainsScreenPoint(Vector2 screenBottomLeft)
        {
            if (!IsVisible) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(_responsePanel, screenBottomLeft)
                   || _bubble.gameObject.activeSelf
                   && RectTransformUtility.RectangleContainsScreenPoint(_bubble, screenBottomLeft);
        }

        private void BuildPresentation()
        {
            _canvasRoot = new GameObject("Source-Faithful Dialogue",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
                typeof(SourceUiRuntime));
            _canvasRoot.transform.SetParent(transform, worldPositionStays: false);
            _canvasRoot.GetComponent<SourceUiRuntime>().BindResolver(_resolver);
            Canvas canvas = _canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 650;
            CanvasScaler scaler = _canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 1f;

            RectTransform root = (RectTransform)_canvasRoot.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            _responsePanel = AddRect("Retail Dialogue Responses", root);
            _responsePanel.anchorMin = _responsePanel.anchorMax = new Vector2(.5f, 0f);
            _responsePanel.pivot = new Vector2(.5f, 0f);
            _responsePanel.anchoredPosition = new Vector2(0f, RetailDialogueLayout.BottomHudHeight);
            _responsePanel.sizeDelta = new Vector2(RetailDialogueLayout.ResponseWidth,
                RetailDialogueLayout.ResponseHeight);
            Image responseBackdrop = _responsePanel.gameObject.AddComponent<Image>();
            responseBackdrop.color = new Color32(0, 0, 0, 185);
            responseBackdrop.raycastTarget = false;

            for (int index = 0; index < RetailDialogueLayout.MaximumResponses; index++)
                AddResponseRow(index);

            _bubble = AddRect("Retail Dialogue NPC Bubble", root);
            _bubble.anchorMin = _bubble.anchorMax = Vector2.zero;
            _bubble.pivot = new Vector2(.5f, 0f);
            _bubble.sizeDelta = new Vector2(RetailDialogueLayout.NpcBubbleMaximumWidth,
                RetailDialogueLayout.NpcBubbleLineHeight);
        }

        private void RebuildNpcBubble(string value)
        {
            for (int index = _bubble.childCount - 1; index >= 0; index--)
            {
                GameObject child = _bubble.GetChild(index).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Object.Destroy(child); else Object.DestroyImmediate(child);
            }
            if (string.IsNullOrWhiteSpace(value)) return;

            List<string> lines = WrapNpcText(value, RetailDialogueLayout.NpcBubbleMaximumWidth);
            for (int index = 0; index < lines.Count; index++)
            {
                SourceUiBitmapText shadow = AddBubbleLine($"NPC Speech Shadow {index + 1}", lines[index],
                    ResponseShadow, index, 1f);
                SourceUiBitmapText text = AddBubbleLine($"NPC Speech {index + 1}", lines[index],
                    Color.white, index, 0f);
                CenterBubbleLine(shadow, shadow.LogicalSize.x, 1f);
                CenterBubbleLine(text, text.LogicalSize.x, 0f);
            }
            _bubble.sizeDelta = new Vector2(RetailDialogueLayout.NpcBubbleMaximumWidth,
                Mathf.Min(RetailDialogueLayout.NpcBubbleMaximumHeight,
                    lines.Count * RetailDialogueLayout.NpcBubbleLineHeight));
        }

        private SourceUiBitmapText AddBubbleLine(string name, string value, Color tint, int row, float offset)
        {
            RectTransform line = AddRect(name, _bubble);
            line.anchorMin = line.anchorMax = new Vector2(0f, 1f);
            line.pivot = new Vector2(0f, 1f);
            line.anchoredPosition = new Vector2(offset,
                -row * RetailDialogueLayout.NpcBubbleLineHeight - offset);
            SourceUiBitmapText text = line.gameObject.AddComponent<SourceUiBitmapText>();
            text.Bind(_resolver, value, tint, RetailDialogueLayout.NpcBubbleFontSourceId);
            return text;
        }

        private void CenterBubbleLine(SourceUiBitmapText text, float width, float offset)
        {
            RectTransform line = (RectTransform)text.transform;
            line.anchoredPosition = new Vector2((_bubble.sizeDelta.x - width) * .5f + offset,
                line.anchoredPosition.y);
        }

        private List<string> WrapNpcText(string value, float maximumWidth)
        {
            var lines = new List<string>();
            string current = string.Empty;
            foreach (string word in value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = current.Length == 0 ? word : current + " " + word;
                if (MeasureSourceText(candidate) <= maximumWidth) current = candidate;
                else
                {
                    if (current.Length > 0) lines.Add(current);
                    current = word;
                    if ((lines.Count + 1) * RetailDialogueLayout.NpcBubbleLineHeight
                        >= RetailDialogueLayout.NpcBubbleMaximumHeight) break;
                }
            }
            if (current.Length > 0
                && lines.Count * RetailDialogueLayout.NpcBubbleLineHeight
                < RetailDialogueLayout.NpcBubbleMaximumHeight) lines.Add(current);
            return lines;
        }

        private float MeasureSourceText(string value)
        {
            float width = 0f;
            foreach (char character in value)
            {
                int frame = (byte)character - 31;
                if (frame >= 0 && _resolver.TryResolve(
                        new UiAssetKey(RetailDialogueLayout.NpcBubbleFontSourceId, frame: frame),
                        out UiResolvedAsset asset))
                    width += asset.Hotspot.x > 0 ? asset.Hotspot.x : asset.LogicalSize.x;
            }
            return width;
        }

        private void AddResponseRow(int index)
        {
            Rect row = RetailDialogueLayout.ResponseRowRect(index);
            RectTransform rowRect = AddRect($"Dialogue Response {index + 1}", _responsePanel);
            PlaceTopLeft(rowRect, row);
            Image hit = rowRect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            Button button = rowRect.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            int captured = index;
            button.onClick.AddListener(() => _controller.SelectDialogueResponse(captured));
            _responseButtons[index] = button;

            RectTransform shadowRect = AddRect("Shadow", rowRect);
            PlaceTopLeft(shadowRect, new Rect(1f, 1f, row.width, row.height));
            _responseShadows[index] = shadowRect.gameObject.AddComponent<SourceUiBitmapText>();
            _responseShadows[index].Bind(_resolver, string.Empty, ResponseShadow,
                RetailDialogueLayout.ResponseFontSourceId);
            RectTransform textRect = AddRect("Text", rowRect);
            PlaceTopLeft(textRect, new Rect(0f, 0f, row.width, row.height));
            _responseTexts[index] = textRect.gameObject.AddComponent<SourceUiBitmapText>();
            _responseTexts[index].Bind(_resolver, string.Empty, ResponseNormal,
                RetailDialogueLayout.ResponseFontSourceId);

            EventTrigger trigger = rowRect.gameObject.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter,
                () => _responseTexts[captured].SetTint(ResponseTint(true, true)));
            AddTrigger(trigger, EventTriggerType.PointerExit,
                () => _responseTexts[captured].SetTint(ResponseTint(true, false)));
        }

        private void RefreshResponseTint(int index)
        {
            RectTransform row = (RectTransform)_responseButtons[index].transform;
            bool inside = RectTransformUtility.RectangleContainsScreenPoint(row, Input.mousePosition);
            _responseTexts[index].SetTint(ResponseTint(_responseButtons[index].interactable, inside));
        }

        private void PositionNpcBubble(Arcanum.Formats.Objects.ArcanumObjectId npc)
        {
            if (_bubble == null || !_bubble.gameObject.activeSelf) return;
            Vector2 position = new Vector2(Screen.width * .5f, Screen.height * .62f);
            if (_session != null && _session.TryGetLoadedObject(npc, out WorldObject runtime)
                                 && Camera.main != null)
            {
                Vector3 screen = Camera.main.WorldToScreenPoint(runtime.transform.position);
                if (screen.z >= 0f) position = new Vector2(screen.x, screen.y + 48f);
            }
            float half = _bubble.sizeDelta.x * .5f;
            position.x = Mathf.Clamp(position.x, half + 8f, Screen.width - half - 8f);
            position.y = Mathf.Clamp(position.y, RetailDialogueLayout.BottomHudHeight
                                                  + RetailDialogueLayout.ResponseHeight + 8f,
                Screen.height - _bubble.sizeDelta.y - 8f);
            _bubble.anchoredPosition = position;
        }

        private static RectTransform AddRect(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, worldPositionStays: false);
            return rect;
        }

        private static void PlaceTopLeft(RectTransform rect, Rect source)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(source.x, -source.y);
            rect.sizeDelta = source.size;
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers ??= new List<EventTrigger.Entry>();
            trigger.triggers.Add(entry);
        }

        private void OnDestroy() => Cleanup();

        private void Cleanup()
        {
            if (_canvasRoot != null)
            {
                _canvasRoot.SetActive(false);
                if (Application.isPlaying) Object.Destroy(_canvasRoot); else Object.DestroyImmediate(_canvasRoot);
            }
            _canvasRoot = null;
            _responsePanel = null;
            _bubble = null;
            _bubbleValue = string.Empty;
            _resolver = null;
            CurrentResponseCount = 0;
            OverflowDetected = false;
        }
    }
}
