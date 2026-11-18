using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GC26._2048
{
    /// <summary>One item on the field: tinted box with the item icon only (names live in the rules window).</summary>
    public class TileView : MonoBehaviour
    {
        const float Gap = 0.03f;
        static readonly float Cell = (1f - 5f * Gap) / 4f;

        RectTransform rect;
        Image box;
        Image icon;
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
            view.icon.rectTransform.Place(0.08f, 0.08f, 0.92f, 0.92f);
            return view;
        }

        public void Show(int level, CheeseArt art)
        {
            Level = level;
            box.color = CheeseArt.TileColor(level);
            icon.sprite = art.Item(level);
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
