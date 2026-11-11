using UnityEngine;

namespace GC26.Lines.View
{
    /// <summary>How an animation speeds up and slows down.</summary>
    public enum Ease
    {
        Linear,
        InQuad,
        OutQuad,
        InOutQuad,
        InOutSine,
        /// <summary>Overshoots a little, then settles: used for balls growing in.</summary>
        OutBack,
    }

    public static class Easing
    {
        /// <summary>Maps progress 0..1 to eased progress (0 at the start, 1 at the end).</summary>
        public static float Evaluate(Ease ease, float t)
        {
            t = Mathf.Clamp01(t);
            switch (ease)
            {
                case Ease.InQuad: return t * t;
                case Ease.OutQuad: return 1f - (1f - t) * (1f - t);
                case Ease.InOutQuad: return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                case Ease.InOutSine: return -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;
                case Ease.OutBack:
                    const float c1 = 1.70158f, c3 = c1 + 1f;
                    return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
                default: return t;
            }
        }
    }
}
