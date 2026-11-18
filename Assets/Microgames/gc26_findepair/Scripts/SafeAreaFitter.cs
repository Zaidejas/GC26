using UnityEngine;

namespace GC26.Findepair
{
    /// <summary>Keeps a full-screen rect inside Screen.safeArea (notches, rounded corners).</summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        Rect _last;
        Vector2Int _lastScreen;

        void OnEnable() => Apply();
        void Update() => Apply();

        void Apply()
        {
            var safe = Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (safe == _last && screen == _lastScreen) return;
            _last = safe;
            _lastScreen = screen;
            if (screen.x <= 0 || screen.y <= 0) return;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(safe.xMin / screen.x, safe.yMin / screen.y);
            rt.anchorMax = new Vector2(safe.xMax / screen.x, safe.yMax / screen.y);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
