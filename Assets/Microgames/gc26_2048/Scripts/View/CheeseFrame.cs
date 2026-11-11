using UnityEngine;

namespace GC26._2048
{
    /// <summary>
    /// Keeps the game in a fixed 9:16 frame (1080×1920 units) inside the safe area:
    /// bars above and below on taller phones, centred frame on tablets and desktops.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CheeseFrame : MonoBehaviour
    {
        public const float Width = 1080f;
        public const float Height = 1920f;

        RectTransform safe;
        Rect lastSafe;
        Vector2 lastSize;

        public void Init(RectTransform safeArea)
        {
            safe = safeArea;
            var rt = (RectTransform)transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(Width, Height);
            rt.anchoredPosition = Vector2.zero;
            Refresh();
        }

        void LateUpdate() => Refresh();

        void Refresh()
        {
            if (safe == null) return;
            var area = Screen.safeArea;
            if (area != lastSafe && Screen.width > 0 && Screen.height > 0)
            {
                lastSafe = area;
                safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
                safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
                safe.offsetMin = safe.offsetMax = Vector2.zero;
            }

            var size = safe.rect.size;
            if (size == lastSize || size.x <= 0 || size.y <= 0) return;
            lastSize = size;
            float scale = Mathf.Min(size.x / Width, size.y / Height);
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
