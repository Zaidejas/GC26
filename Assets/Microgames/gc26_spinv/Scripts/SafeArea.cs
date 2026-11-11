using UnityEngine;

namespace GC26.Spinv
{
    // Держит элементы интерфейса в безопасной зоне экрана, чтобы они не уходили под вырез
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        Rect lastArea;
        Vector2Int lastScreen;

        void LateUpdate()
        {
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.safeArea == lastArea && screen == lastScreen) return;

            lastArea = Screen.safeArea;
            lastScreen = screen;

            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(lastArea.xMin / screen.x, lastArea.yMin / screen.y);
            rect.anchorMax = new Vector2(lastArea.xMax / screen.x, lastArea.yMax / screen.y);
        }
    }
}
