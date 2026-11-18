using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GC26.Towerdefence
{
    /// <summary>One round button in the radial menu.</summary>
    public class RadialItem
    {
        public string Label;
        /// <summary>Second line (price, refund, mode); read every frame so it stays current.</summary>
        public Func<string> Sub;
        public Color Color;
        /// <summary>Button is greyed out while this returns false (e.g. not enough gold).</summary>
        public Func<bool> CanUse = () => true;
        public Action Use;
        /// <summary>Range shown while the mouse is over the button; 0 = keep the current preview.</summary>
        public float PreviewRange;
    }

    /// <summary>
    /// The radial menu around a slot: on an empty slot one button per tower (name + cost),
    /// on a built tower Upgrade / Sell / Target. BuildManager decides the items; this only draws them.
    /// Hovering a button with a mouse previews its range (touch screens have no hover).
    /// Placeholder look; the real one comes with the art (W5-W6).
    /// </summary>
    public class BuildMenuView : MonoBehaviour
    {
        [SerializeField] PlaceholderArt art;

        const float Radius = 190f;
        const float ButtonSize = 170f;

        RectTransform _canvasRect;
        CanvasScaler _scaler;
        RectTransform _ring;
        readonly List<(Button button, TMP_Text sub, RadialItem item)> _buttons = new();
        Action<float> _preview;

        public bool IsOpen => _ring != null && _ring.gameObject.activeSelf;

        void Awake()
        {
            var canvasGo = new GameObject("BuildMenuCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90; // under the HUD and the pause menu
            _scaler = canvasGo.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();
            _canvasRect = (RectTransform)canvasGo.transform;

            _ring = new GameObject("Ring", typeof(RectTransform)).GetComponent<RectTransform>();
            _ring.SetParent(_canvasRect, false);
            _ring.anchorMin = _ring.anchorMax = new Vector2(0.5f, 0.5f);
            _ring.sizeDelta = Vector2.zero;
            _ring.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!IsOpen) return;
            foreach (var (button, sub, item) in _buttons)
            {
                button.interactable = item.CanUse();
                sub.text = item.Sub != null ? item.Sub() : "";
            }
        }

        /// <param name="screenPoint">Center of the ring (the slot on screen).</param>
        /// <param name="preview">Called with a range to preview on hover, or 0 when the pointer leaves.</param>
        public void Open(Vector2 screenPoint, IReadOnlyList<RadialItem> items, Action<float> preview)
        {
            _preview = preview;
            // Same scaling rule as the HUD: the short side of the screen is 1080 reference pixels.
            bool portrait = Screen.height > Screen.width;
            _scaler.referenceResolution = portrait ? new Vector2(1080, 1920) : new Vector2(1920, 1080);
            _scaler.matchWidthOrHeight = portrait ? 0f : 1f;
            Canvas.ForceUpdateCanvases();

            RebuildButtons(items);

            // Keep the whole ring on screen.
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPoint, null, out var local);
            Vector2 half = _canvasRect.rect.size / 2f;
            float edge = Radius + ButtonSize / 2f + 12f;
            local.x = Mathf.Clamp(local.x, -half.x + edge, half.x - edge);
            local.y = Mathf.Clamp(local.y, -half.y + edge, half.y - edge);
            _ring.anchoredPosition = local;
            _ring.gameObject.SetActive(true);
            Update();
        }

        public void Close()
        {
            if (_ring != null) _ring.gameObject.SetActive(false);
        }

        void RebuildButtons(IReadOnlyList<RadialItem> items)
        {
            foreach (var (button, _, _) in _buttons) Destroy(button.gameObject);
            _buttons.Clear();

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                float angle = Mathf.PI / 2f - i * 2f * Mathf.PI / items.Count; // first one on top, then clockwise
                var rect = new GameObject(item.Label, typeof(RectTransform)).GetComponent<RectTransform>();
                rect.SetParent(_ring, false);
                rect.sizeDelta = new Vector2(ButtonSize, ButtonSize);
                rect.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Radius;

                var image = rect.gameObject.AddComponent<Image>();
                image.sprite = art.Circle;
                image.color = Color.white;
                var button = rect.gameObject.AddComponent<Button>();
                var colors = button.colors;
                colors.normalColor = item.Color;
                colors.highlightedColor = Color.Lerp(item.Color, Color.white, 0.2f);
                colors.pressedColor = Color.Lerp(item.Color, Color.black, 0.3f);
                colors.selectedColor = item.Color;
                colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.85f);
                button.colors = colors;
                button.onClick.AddListener(() =>
                {
                    if (item.CanUse()) item.Use?.Invoke();
                });

                if (item.PreviewRange > 0f)
                {
                    var hover = rect.gameObject.AddComponent<EventTrigger>();
                    AddTrigger(hover, EventTriggerType.PointerEnter, () => _preview?.Invoke(item.PreviewRange));
                    AddTrigger(hover, EventTriggerType.PointerExit, () => _preview?.Invoke(0f));
                }

                var title = Label(rect, item.Label, 38f, Color.white);
                title.rectTransform.anchoredPosition = new Vector2(0f, 18f);
                var sub = Label(rect, "", 34f, new Color(1f, 0.84f, 0.31f));
                sub.rectTransform.anchoredPosition = new Vector2(0f, -26f);

                _buttons.Add((button, sub, item));
            }
        }

        static void AddTrigger(EventTrigger trigger, EventTriggerType type, Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        static TMP_Text Label(Transform parent, string text, float size, Color color)
        {
            var label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(parent, false);
            label.rectTransform.sizeDelta = new Vector2(ButtonSize, 50f);
            label.text = text;
            label.fontSize = size;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }
    }
}
