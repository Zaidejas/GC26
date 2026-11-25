using UnityEngine;

namespace GC26._2048Nastya
{
    /// <summary>
    /// Easing curves for the coroutine animations (TDD 6.3). There is no tween package (rule 5),
    /// so the views run short coroutines on <see cref="Time.unscaledDeltaTime"/> and use these curves.
    /// </summary>
    public static class Tween2048
    {
        /// <summary>Ease-out cubic: 1 - (1 - t)^3.</summary>
        public static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            return 1f - u * u * u;
        }

        /// <summary>0 → 1 → 0 hump, for "go there and come back" (merge pop, bump).</summary>
        public static float Hump(float t) => Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);

        /// <summary>Progress of a tween that started <paramref name="elapsed"/> seconds ago, 0..1.</summary>
        public static float Progress(float elapsed, float duration) =>
            duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
    }
}
