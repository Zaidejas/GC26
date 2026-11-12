using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GC26.Spinv
{
    // Плашка «ACHIEVEMENT UNLOCKED»: выезжает сверху, название печатается, через пару секунд уезжает.
    // Несколько достижений показываются по очереди. Реальное время: работает и на экране победы
    public class AchievementToast : MonoBehaviour
    {
        public RectTransform panel;
        public TMP_Text titleText;
        public float shownY = -340f;
        public float hiddenY = 200f;
        public float slideTime = 0.25f;
        public float holdTime = 2.2f;
        public float charTime = 0.04f;

        readonly Queue<string> queue = new Queue<string>();
        bool playing;

        void Awake()
        {
            panel.gameObject.SetActive(false);
        }

        public void Show(string title)
        {
            queue.Enqueue(title);
            if (!playing) StartCoroutine(Play());
        }

        IEnumerator Play()
        {
            playing = true;
            while (queue.Count > 0)
            {
                string title = queue.Dequeue();
                titleText.text = "";
                panel.gameObject.SetActive(true);
                SpinvUiSound.Achievement();
                SpinvHaptics.Light();

                yield return Slide(hiddenY, shownY);
                for (int k = 1; k <= title.Length; k++)
                {
                    titleText.text = title.Substring(0, k) + (k < title.Length ? "_" : "");
                    SpinvUiSound.TypeTick(title[k - 1]);
                    yield return new WaitForSecondsRealtime(charTime);
                }
                yield return new WaitForSecondsRealtime(holdTime);
                yield return Slide(shownY, hiddenY);
                panel.gameObject.SetActive(false);
            }
            playing = false;
        }

        IEnumerator Slide(float from, float to)
        {
            for (float t = 0f; t < slideTime; t += Time.unscaledDeltaTime)
            {
                SetY(Mathf.Lerp(from, to, t / slideTime));
                yield return null;
            }
            SetY(to);
        }

        void SetY(float y)
        {
            panel.anchoredPosition = new Vector2(panel.anchoredPosition.x, y);
        }
    }
}
