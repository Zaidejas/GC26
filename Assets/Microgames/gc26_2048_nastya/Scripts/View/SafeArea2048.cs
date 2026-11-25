using UnityEngine;

namespace GC26._2048Nastya
{
    /// <summary>
    /// T58: keeps its RectTransform inside <see cref="Screen.safeArea"/>, so the HUD and the board never sit under
    /// a notch, a camera hole or the system bars. Put it on a full-screen child of the canvas and place the
    /// content under it; full-screen backgrounds and overlay blockers stay outside. Follows rotation and resizes.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeArea2048 : MonoBehaviour
    {
        Rect _applied;
        Vector2Int _screen;

        void OnEnable() => Apply(force: true);

        void Update() => Apply(force: false);

        void Apply(bool force)
        {
            var area = Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (!force && area == _applied && screen == _screen) return;
            _applied = area;
            _screen = screen;

            var (min, max) = Anchors(area, screen.x, screen.y);
            var rect = (RectTransform)transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Normalized anchors of the safe area; the whole screen when the size is unknown.</summary>
        public static (Vector2 min, Vector2 max) Anchors(Rect safeArea, int screenWidth, int screenHeight)
        {
            if (screenWidth <= 0 || screenHeight <= 0 || safeArea.width <= 0f || safeArea.height <= 0f)
                return (Vector2.zero, Vector2.one);
            var size = new Vector2(screenWidth, screenHeight);
            var min = new Vector2(Mathf.Clamp01(safeArea.xMin / size.x), Mathf.Clamp01(safeArea.yMin / size.y));
            var max = new Vector2(Mathf.Clamp01(safeArea.xMax / size.x), Mathf.Clamp01(safeArea.yMax / size.y));
            return (min, max);
        }
    }
}
