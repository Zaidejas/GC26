using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace GC26._2048
{
    /// <summary>
    /// "The game is alive" hint: after <see cref="IdleTimer.DefaultInterval"/> seconds without input the mouse's face
    /// peeks in from a random edge of the frame, stays <see cref="ShowSeconds"/> s and hides again; repeats every
    /// interval while the player stays idle. Any key, click or touch resets the timer and sends the mouse back.
    /// Lives on a full-frame rect above everything else and never blocks clicks.
    /// </summary>
    public class MousePeek : MonoBehaviour
    {
        public const float ShowSeconds = 2f;
        const float RiseSeconds = 0.35f, HideSeconds = 0.3f, QuickHideSeconds = 0.15f;
        const float FaceWidth = 300f;
        /// <summary>How far the cut-off bottom of the face stays behind the edge when fully out.</summary>
        const float Tuck = 18f;

        enum Edge { Bottom, Left, Right, Top }

        readonly IdleTimer timer = new IdleTimer();
        RectTransform face;
        float faceHeight;
        Coroutine running;
        bool hiding;
        Edge lastEdge = Edge.Top;
        float baseAngle; // 0 / ±90 / 180: the face's "up" points inward from the edge
        float offset; // distance of the face's bottom from the edge, inward; negative = hidden

        public void Init(Sprite sprite)
        {
            var image = CheeseUi.Picture(transform, "MouseFace", sprite);
            image.raycastTarget = false;
            face = image.rectTransform;
            float aspect = sprite != null ? sprite.rect.height / sprite.rect.width : 0.67f;
            faceHeight = FaceWidth * aspect;
            face.pivot = new Vector2(0.5f, 0f);
            face.sizeDelta = new Vector2(FaceWidth, faceHeight);
            face.gameObject.SetActive(false);
        }

        void OnDisable()
        {
            running = null;
            hiding = false;
            if (face != null) face.gameObject.SetActive(false);
            timer.Reset();
        }

        void Update()
        {
            if (face == null) return;
            if (PlayerActed())
            {
                timer.Reset();
                if (running != null && !hiding) Restart(Hide(QuickHideSeconds));
                return;
            }
            if (timer.Tick(Time.unscaledDeltaTime) && running == null) Restart(Peek());
        }

        static bool PlayerActed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame) return true;
            return false;
#else
            return Input.anyKeyDown || Input.touchCount > 0;
#endif
        }

        void Restart(IEnumerator routine)
        {
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(routine);
        }

        IEnumerator Peek()
        {
            hiding = false;
            Place();
            face.gameObject.SetActive(true);
            float hidden = -faceHeight - Tuck, shown = -Tuck;

            for (float t = 0; t < RiseSeconds; t += Time.unscaledDeltaTime)
            {
                SetOffset(Mathf.LerpUnclamped(hidden, shown, EaseOutBack(t / RiseSeconds)), 0f);
                yield return null;
            }
            for (float t = 0; t < ShowSeconds; t += Time.unscaledDeltaTime)
            {
                // a little head tilt and bob, so it reads as alive, not as a sticker
                SetOffset(shown + 6f * Mathf.Sin(t * Mathf.PI * 2f), 5f * Mathf.Sin(t * Mathf.PI * 1.5f));
                yield return null;
            }
            yield return Hide(HideSeconds);
        }

        IEnumerator Hide(float seconds)
        {
            hiding = true;
            float from = offset, hidden = -faceHeight - Tuck;
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = t / seconds;
                SetOffset(Mathf.Lerp(from, hidden, k * k), 0f);
                yield return null;
            }
            face.gameObject.SetActive(false);
            hiding = false;
            running = null;
        }

        /// <summary>Picks an edge (not the same as last time) and a spot along it; the face looks inward.</summary>
        void Place()
        {
            Edge edge;
            do edge = (Edge)Random.Range(0, 4); while (edge == lastEdge);
            lastEdge = edge;
            float along = Random.Range(0.2f, 0.8f);
            Vector2 anchor;
            float angle;
            switch (edge)
            {
                case Edge.Left: anchor = new Vector2(0f, along); angle = -90f; break;
                case Edge.Right: anchor = new Vector2(1f, along); angle = 90f; break;
                case Edge.Top: anchor = new Vector2(along, 1f); angle = 180f; break;
                default: anchor = new Vector2(along, 0f); angle = 0f; break;
            }
            face.anchorMin = face.anchorMax = anchor;
            baseAngle = angle;
        }

        /// <summary>Moves the face along its own "up" (= inward from the edge) and tilts it by <paramref name="tilt"/>°.</summary>
        void SetOffset(float value, float tilt)
        {
            offset = value;
            face.localEulerAngles = new Vector3(0f, 0f, baseAngle + tilt);
            float rad = baseAngle * Mathf.Deg2Rad;
            var inward = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));
            face.anchoredPosition = inward * value;
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            x = Mathf.Clamp01(x) - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }
    }
}
