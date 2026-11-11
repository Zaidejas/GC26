using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GC26._2048
{
    /// <summary>One item on the field: tinted box, sprite, short name and level badge.</summary>
    public class TileView : MonoBehaviour
    {
        const float Gap = 0.03f;
        static readonly float Cell = (1f - 5f * Gap) / 4f;

        RectTransform rect;
        Image box;
        Image icon;
        TextMeshProUGUI label;
        TextMeshProUGUI badge;
        CanvasGroup group;

        public int Level { get; private set; }

        public static TileView Create(Transform parent, CheeseArt art)
        {
            var rt = CheeseUi.Rect("Tile", parent);
            var view = rt.gameObject.AddComponent<TileView>();
            view.rect = rt;
            view.group = rt.gameObject.AddComponent<CanvasGroup>();
            view.group.blocksRaycasts = false;
            view.box = rt.gameObject.AddComponent<Image>();
            view.box.sprite = art.Box;
            view.box.type = Image.Type.Sliced;
            view.box.raycastTarget = false;
            CheeseUi.AddShadow(rt.gameObject, 8f);

            view.icon = CheeseUi.Picture(rt, "Icon", null);
            view.icon.rectTransform.Place(0.12f, 0.06f, 0.88f, 0.74f);
            view.label = CheeseUi.Label(rt, "", 30f, CheeseArt.Ink, TextAlignmentOptions.Center, true);
            view.label.rectTransform.Place(0.02f, 0.72f, 0.98f, 0.96f);
            view.label.textWrappingMode = TextWrappingModes.NoWrap;
            view.label.enableAutoSizing = true;
            view.label.fontSizeMin = 18f;
            view.label.fontSizeMax = 30f;
            view.badge = CheeseUi.Label(rt, "", 26f, CheeseArt.InkDim, TextAlignmentOptions.TopLeft, true);
            view.badge.rectTransform.Place(0.1f, 0.05f, 0.5f, 0.3f);
            return view;
        }

        public void Show(int level, CheeseArt art)
        {
            Level = level;
            box.color = CheeseArt.TileColor(level);
            icon.sprite = art.Item(level);
            label.text = CheeseRules.ShortNames[level];
            badge.text = level.ToString();
            rect.localScale = Vector3.one;
            group.alpha = 1f;
            gameObject.SetActive(true);
        }

        public static Vector2 CellMin(int row, int col) =>
            new Vector2(Gap + col * (Cell + Gap), 1f - (Gap + row * (Cell + Gap)) - Cell);

        public static Vector2 CellSize => new Vector2(Cell, Cell);

        public Vector2 AnchorMin => rect.anchorMin;

        public void SetAnchor(Vector2 min)
        {
            rect.anchorMin = min;
            rect.anchorMax = min + CellSize;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        public void SetCell(int row, int col) => SetAnchor(CellMin(row, col));

        public IEnumerator Appear(float duration = 0.16f)
        {
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                rect.localScale = Vector3.one * EaseOut(t / duration);
                yield return null;
            }
            rect.localScale = Vector3.one;
        }

        public IEnumerator Pulse(float duration = 0.2f)
        {
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                rect.localScale = Vector3.one * (1f + 0.2f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            rect.localScale = Vector3.one;
        }

        /// <summary>The cheese flies up towards the hole and fades out.</summary>
        public IEnumerator FlyToHole(float duration = 0.45f)
        {
            var start = rect.anchoredPosition;
            float height = rect.rect.height;
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                float e = k * k;
                rect.anchoredPosition = start + new Vector2(0, height * 1.6f * e);
                rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.35f, e);
                rect.localRotation = Quaternion.Euler(0, 0, -20f * e);
                group.alpha = 1f - e;
                yield return null;
            }
            rect.anchoredPosition = start;
            rect.localRotation = Quaternion.identity;
        }

        static float EaseOut(float k) => 1f - (1f - k) * (1f - k);
    }
}
