using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GC26._2048
{
    /// <summary>
    /// One item on the field: tinted box with the item icon only (names live in the rules window).
    /// Effects: appear, pulse, glow after a merge, jelly squash after a bump, flight to the hole.
    /// Effects run on the tile itself, so a tile going back to the pool stops them; <see cref="Show"/> resets the look.
    /// </summary>
    public class TileView : MonoBehaviour
    {
        const float Gap = 0.03f;
        static readonly float Cell = (1f - 5f * Gap) / 4f;

        RectTransform rect;
        Image box;
        Image icon;
        CanvasGroup group;
        Image glow;
        Coroutine shapeEffect, glowEffect;

        static readonly Color GlowColor = new Color(1f, 0.93f, 0.55f, 0f);

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

            // under the icon, bigger than the tile: lights the tile up and spills a little halo around it
            view.glow = CheeseUi.Picture(rt, "Glow", art.Glow);
            view.glow.preserveAspect = false;
            view.glow.rectTransform.Place(-0.3f, -0.3f, 1.3f, 1.3f);
            view.glow.color = GlowColor;
            view.glow.gameObject.SetActive(false);

            view.icon = CheeseUi.Picture(rt, "Icon", null);
            view.icon.rectTransform.Place(0.08f, 0.08f, 0.92f, 0.92f);
            return view;
        }

        public void Show(int level, CheeseArt art)
        {
            Level = level;
            box.color = CheeseArt.TileColor(level);
            icon.sprite = art.Item(level);
            StopEffects();
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

        void StopEffects()
        {
            if (shapeEffect != null) StopCoroutine(shapeEffect);
            if (glowEffect != null) StopCoroutine(glowEffect);
            shapeEffect = glowEffect = null;
            rect.localScale = Vector3.one;
            glow.gameObject.SetActive(false);
        }

        void OnDisable()
        {
            shapeEffect = glowEffect = null; // coroutines stop with the object
        }

        /// <summary>Starts a scale effect, replacing the one running (a new move can come mid-animation).</summary>
        void PlayShape(IEnumerator effect)
        {
            if (!isActiveAndEnabled) return;
            if (shapeEffect != null) StopCoroutine(shapeEffect);
            rect.localScale = Vector3.one;
            shapeEffect = StartCoroutine(effect);
        }

        /// <summary>Merge result: a short pulse and a soft warm glow that flares up and fades.</summary>
        public void PlayMerge()
        {
            PlayShape(Pulse());
            if (!isActiveAndEnabled) return;
            if (glowEffect != null) StopCoroutine(glowEffect);
            glowEffect = StartCoroutine(Glow());
        }

        /// <summary>Bumped into another item without merging: jelly squash along the move axis.</summary>
        public void PlayBump(bool horizontal) => PlayShape(Jelly(horizontal));

        IEnumerator Glow(float duration = 0.55f)
        {
            glow.gameObject.SetActive(true);
            var grt = glow.rectTransform;
            grt.SetAsFirstSibling(); // below the icon
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                // quick flare (first 20 %), then a slow fade
                float a = k < 0.2f ? k / 0.2f : 1f - (k - 0.2f) / 0.8f;
                glow.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, 0.85f * a * a);
                grt.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.15f, EaseOut(k));
                yield return null;
            }
            glow.gameObject.SetActive(false);
            glowEffect = null;
        }

        /// <summary>
        /// Squashed along the move axis at the moment of impact (and a little wider across it), then it wobbles back
        /// like jelly: a damped spring, about 0.4 s.
        /// </summary>
        IEnumerator Jelly(bool horizontal, float duration = 0.42f)
        {
            const float squash = 0.16f, bulge = 0.08f, damping = 7f, wobbles = 2.6f;
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                float f = Mathf.Exp(-damping * t) * Mathf.Cos(2f * Mathf.PI * wobbles * t);
                float along = 1f - squash * f, across = 1f + bulge * f;
                rect.localScale = horizontal ? new Vector3(along, across, 1f) : new Vector3(across, along, 1f);
                yield return null;
            }
            rect.localScale = Vector3.one;
            shapeEffect = null;
        }

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
