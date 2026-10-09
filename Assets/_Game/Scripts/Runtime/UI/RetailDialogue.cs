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
        public const int MaximumResponses = 5;
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
        private Text _bubbleText;

        public bool IsAvailable => _resolver != null && _canvasRoot != null;
        public bool IsVisible => IsAvailable && _canvasRoot.activeSelf;
        public int CurrentResponseCount { get; private set; }
        public bool OverflowDetected { get; private set; }
        public Rect ResponseRect => RetailDialogueLayout.ResponseScreenRect(Screen.width, Screen.height);
        public bool NpcBubbleVisible => _bubble != null && _bubble.gameObject.activeSelf;

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
            else
            {
                for (int index = 0; index < CurrentResponseCount; index++)
                    _responseButtons[index].interactable = true;
            }

            _bubbleText.text = dialogue.NpcText ?? string.Empty;
            _bubble.gameObject.SetActive(!string.IsNullOrWhiteSpace(_bubbleText.text));
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
            _bubble.sizeDelta = new Vector2(320f, 72f);
            Image bubbleBackdrop = _bubble.gameObject.AddComponent<Image>();
            bubbleBackdrop.color = new Color32(5, 5, 5, 190);
            bubbleBackdrop.raycastTarget = false;
            var textObject = new GameObject("NPC Speech", typeof(RectTransform), typeof(Text), typeof(Shadow));
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(_bubble, worldPositionStays: false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 6f);
            textRect.offsetMax = new Vector2(-8f, -6f);
            _bubbleText = textObject.GetComponent<Text>();
            _bubbleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _bubbleText.fontSize = 14;
            _bubbleText.color = Color.white;
            _bubbleText.alignment = TextAnchor.MiddleCenter;
            _bubbleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _bubbleText.verticalOverflow = VerticalWrapMode.Truncate;
            _bubbleText.raycastTarget = false;
            Shadow shadow = textObject.GetComponent<Shadow>();
            shadow.effectColor = ResponseShadow;
            shadow.effectDistance = new Vector2(1f, -1f);
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
                () => _responseTexts[captured].SetTint(ResponseHover));
            AddTrigger(trigger, EventTriggerType.PointerExit,
                () => _responseTexts[captured].SetTint(ResponseNormal));
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
            _bubbleText = null;
            _resolver = null;
            CurrentResponseCount = 0;
            OverflowDetected = false;
        }
    }
}
