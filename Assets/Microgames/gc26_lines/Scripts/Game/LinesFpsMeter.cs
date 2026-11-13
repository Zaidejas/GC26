using GC26.Lines.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GC26.Lines.Game
{
    /// <summary>
    /// Task 35: the frame rate, measured on the phone. Only test builds have it (LINES_FPS=1 when building):
    /// a small line at the top of the screen ("60 fps · slowest 18 ms") and, every 5 s, a line in the log with
    /// the average, the slowest frame and how many frames were late (longer than 1/50 s). Normal builds don't
    /// include it at all.
    /// </summary>
    public sealed class LinesFpsMeter : MonoBehaviour
    {
        public const float ScreenEvery = 0.5f, LogEvery = 5f, LateFrame = 1f / 50f;

        TextMeshProUGUI label;
        int screenFrames, logFrames, lateFrames, lettersBefore, cleanUpsBefore;
        float screenTime, logTime, screenSlowest, logSlowest;

        public static LinesFpsMeter Create(Camera camera)
        {
            var go = new GameObject("FPS meter");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var meter = go.AddComponent<LinesFpsMeter>();
            var rt = (RectTransform)new GameObject("Text", typeof(RectTransform)).transform;
            rt.SetParent(go.transform, false);
            var safe = Screen.safeArea;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(safe.center.x - Screen.width / 2f, safe.yMax);
            rt.sizeDelta = new Vector2(Screen.width, Screen.height * 0.03f);
            meter.label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            UiFonts.UseBody(meter.label);
            meter.label.fontSize = Screen.height * 0.016f;
            meter.label.alignment = TextAlignmentOptions.Top;
            meter.label.color = new Color(0.55f, 1f, 0.6f, 0.9f);
            meter.label.raycastTarget = false;
            return meter;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            screenFrames++; logFrames++;
            screenTime += dt; logTime += dt;
            screenSlowest = Mathf.Max(screenSlowest, dt);
            logSlowest = Mathf.Max(logSlowest, dt);
            if (dt > LateFrame) lateFrames++;

            if (screenTime >= ScreenEvery)
            {
                label.SetText("{0:0} fps · slowest {1:0} ms", screenFrames / screenTime, screenSlowest * 1000f);
                screenFrames = 0; screenTime = 0f; screenSlowest = 0f;
            }
            if (logTime >= LogEvery)
            {
                int letters = UiFonts.LetterCount, cleanUps = System.GC.CollectionCount(0);
                Debug.Log($"Lines fps: average {logFrames / logTime:0.0}, slowest frame {logSlowest * 1000f:0} ms, " +
                          $"late frames {lateFrames} of {logFrames}, new letters {letters - lettersBefore}, " +
                          $"garbage clean-ups {cleanUps - cleanUpsBefore}");
                lettersBefore = letters; cleanUpsBefore = cleanUps;
                logFrames = 0; logTime = 0f; logSlowest = 0f; lateFrames = 0;
            }
        }
    }
}
