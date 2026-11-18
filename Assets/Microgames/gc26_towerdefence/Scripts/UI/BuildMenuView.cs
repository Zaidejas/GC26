using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GC26.Towerdefence
{
    /// <summary>
    /// The radial build menu: one round button per tower (name + cost) around the tapped slot.
    /// Buttons the player cannot afford are greyed out. Hovering a button with a mouse previews its range.
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
        readonly List<(Button button, TowerData tower)> _buttons = new();
        BuildManager _owner;

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
            if (!IsOpen || _owner == null) return;
            foreach (var (button, tower) in _buttons)
                button.interactable = _owner.CanAfford(tower);
        }

        public void Open(Vector2 screenPoint, BuildManager owner)
        {
            _owner = owner;
            // Same scaling rule as the HUD: the short side of the screen is 1080 reference pixels.
            bool portrait = Screen.height > Screen.width;
            _scaler.referenceResolution = portrait ? new Vector2(1080, 1920) : new Vector2(1920, 1080);
            _scaler.matchWidthOrHeight = portrait ? 0f : 1f;
            Canvas.ForceUpdateCanvases();

            if (_buttons.Count != owner.Towers.Count) RebuildButtons(owner.Towers);

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

        void RebuildButtons(IReadOnlyList<TowerData> towers)
        {
            foreach (var (button, _) in _buttons) Destroy(button.gameObject);
            _buttons.Clear();

            for (int i = 0; i < towers.Count; i++)
            {
                var tower = towers[i];
                if (tower == null) continue;
                float angle = Mathf.PI / 2f - i * 2f * Mathf.PI / towers.Count; // first one on top, then clockwise
                var rect = new GameObject(tower.displayName, typeof(RectTransform)).GetComponent<RectTransform>();
                rect.SetParent(_ring, false);
                rect.sizeDelta = new Vector2(ButtonSize, ButtonSize);
                rect.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Radius;

                var image = rect.gameObject.AddComponent<Image>();
                image.sprite = art.Circle;
                image.color = Color.white;
                var button = rect.gameObject.AddComponent<Button>();
                var colors = button.colors;
                colors.normalColor = tower.color;
                colors.highlightedColor = Color.Lerp(tower.color, Color.white, 0.2f);
                colors.pressedColor = Color.Lerp(tower.color, Color.black, 0.3f);
                colors.selectedColor = tower.color;
                colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.85f);
                button.colors = colors;
                button.onClick.AddListener(() => _owner.BuildSelected(tower));

                // Mouse hover previews the range (touch screens have no hover).
                var hover = rect.gameObject.AddComponent<EventTrigger>();
                AddTrigger(hover, EventTriggerType.PointerEnter, () => _owner.PreviewRange(tower));
                AddTrigger(hover, EventTriggerType.PointerExit, () => _owner.PreviewRange(null));

                var title = Label(rect, tower.displayName, 38f, Color.white);
                title.rectTransform.anchoredPosition = new Vector2(0f, 18f);
                var cost = Label(rect, $"{tower.cost}g", 34f, new Color(1f, 0.84f, 0.31f));
                cost.rectTransform.anchoredPosition = new Vector2(0f, -26f);

                _buttons.Add((button, tower));
            }
        }

        static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action action)
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
