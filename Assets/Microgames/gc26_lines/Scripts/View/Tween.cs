using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GC26.Lines.View
{
    /// <summary>
    /// One running animation. Keep it to stop the animation early or to wait for it.
    /// </summary>
    public sealed class TweenHandle
    {
        internal Object Target;
        internal bool HasTarget;
        internal float Duration;
        internal float Elapsed;
        internal Ease Ease;
        internal Action<float> Apply;
        internal bool Loop;        // ping-pong forever (until killed)
        internal Action OnKilled;  // restores the target when a looping animation stops
        Action onComplete;

        public bool IsDone { get; private set; }

        /// <summary>Runs <paramref name="action"/> when the animation finishes (right away if it already has).</summary>
        public TweenHandle OnComplete(Action action)
        {
            if (IsDone) action?.Invoke();
            else onComplete += action;
            return this;
        }

        /// <summary>Stops the animation. With <paramref name="complete"/> it jumps to the end first.</summary>
        public void Kill(bool complete = false)
        {
            if (IsDone) return;
            if (complete && !Loop && TargetAlive) Apply?.Invoke(1f);
            OnKilled?.Invoke();
            IsDone = true;
            if (complete) onComplete?.Invoke();
        }

        /// <summary>For coroutines: <c>yield return handle.Wait();</c></summary>
        public IEnumerator Wait()
        {
            while (!IsDone) yield return null;
        }

        internal bool TargetAlive => !HasTarget || Target != null;

        /// <summary>Advances by <paramref name="deltaTime"/>; returns true when finished.</summary>
        internal bool Step(float deltaTime)
        {
            if (IsDone) return true;
            if (!TargetAlive) { IsDone = true; return true; } // the object was destroyed: just drop it

            Elapsed += deltaTime;
            if (Loop)
            {
                float cycle = Duration <= 0f ? 0f : Mathf.PingPong(Elapsed / Duration, 1f);
                Apply(Easing.Evaluate(Ease, cycle));
                return false;
            }

            float progress = Duration <= 0f ? 1f : Mathf.Clamp01(Elapsed / Duration);
            Apply(Easing.Evaluate(Ease, progress));
            if (progress < 1f) return false;

            IsDone = true;
            onComplete?.Invoke();
            return true;
        }
    }

    /// <summary>
    /// Our own small animation helper (instead of DOTween). Animations run every frame while the
    /// game plays; tests can drive them by calling <see cref="Tick"/> directly.
    /// Positions are local positions (relative to the parent).
    /// </summary>
    public static class Tween
    {
        static readonly List<TweenHandle> Active = new List<TweenHandle>();
        static TweenRunner runner;

        public static int ActiveCount => Active.Count;

        /// <summary>The basic animation: calls <paramref name="apply"/> with eased progress 0..1 every frame.</summary>
        public static TweenHandle To(Object target, float duration, Action<float> apply, Ease ease = Ease.Linear)
        {
            if (apply == null) throw new ArgumentNullException(nameof(apply));
            var handle = new TweenHandle
            {
                Target = target, HasTarget = target != null, Duration = Mathf.Max(0f, duration), Ease = ease, Apply = apply,
            };
            Active.Add(handle);
            EnsureRunner();
            return handle;
        }

        public static TweenHandle Move(Transform t, Vector3 to, float duration, Ease ease = Ease.OutQuad)
        {
            var from = t.localPosition;
            return To(t, duration, p => t.localPosition = Vector3.LerpUnclamped(from, to, p), ease);
        }

        /// <summary>Moves through <paramref name="points"/> one after another, at an even speed (e.g. 40 ms per cell).</summary>
        public static TweenHandle MovePath(Transform t, IReadOnlyList<Vector3> points, float secondsPerStep)
        {
            if (points == null || points.Count == 0) throw new ArgumentException("The path is empty.", nameof(points));
            var start = t.localPosition;
            int steps = points.Count;
            return To(t, steps * secondsPerStep, p =>
            {
                float position = p * steps;
                int i = Mathf.Min((int)position, steps - 1);
                var from = i == 0 ? start : points[i - 1];
                t.localPosition = Vector3.Lerp(from, points[i], position - i);
            });
        }

        public static TweenHandle Scale(Transform t, Vector3 to, float duration, Ease ease = Ease.OutQuad)
        {
            var from = t.localScale;
            return To(t, duration, p => t.localScale = Vector3.LerpUnclamped(from, to, p), ease);
        }

        /// <summary>Shakes left and right, fading out, and ends where it started (the blocked-move shake).</summary>
        public static TweenHandle Shake(Transform t, float strength, float duration, int shakes = 4)
        {
            var start = t.localPosition;
            return To(t, duration, p =>
            {
                float offset = strength * Mathf.Sin(p * Mathf.PI * 2f * shakes) * (1f - p);
                t.localPosition = start + Vector3.right * offset;
            });
        }

        /// <summary>
        /// Bounces up and down until killed (the selected ball). Killing it puts the object back where it was.
        /// </summary>
        public static TweenHandle Bounce(Transform t, float height, float halfPeriod)
        {
            var start = t.localPosition;
            var handle = To(t, halfPeriod, p => t.localPosition = start + Vector3.up * (height * p), Ease.InOutSine);
            handle.Loop = true;
            handle.OnKilled = () => { if (t != null) t.localPosition = start; };
            return handle;
        }

        /// <summary>Runs <paramref name="action"/> after <paramref name="seconds"/>.</summary>
        public static TweenHandle Delay(float seconds, Action action) =>
            To(null, seconds, _ => { }).OnComplete(action);

        /// <summary>Stops every animation on <paramref name="target"/>.</summary>
        public static void Kill(Object target, bool complete = false)
        {
            foreach (var handle in Active.ToArray())
                if (handle.HasTarget && handle.Target == target)
                    handle.Kill(complete);
            Active.RemoveAll(h => h.IsDone);
        }

        public static void KillAll()
        {
            foreach (var handle in Active.ToArray()) handle.Kill();
            Active.Clear();
        }

        /// <summary>Advances all animations. Called every frame by the runner, or directly by tests.</summary>
        public static void Tick(float deltaTime)
        {
            // Copy, so callbacks can start or stop animations safely.
            foreach (var handle in Active.ToArray())
                handle.Step(deltaTime);
            Active.RemoveAll(h => h.IsDone);
        }

        static void EnsureRunner()
        {
            if (runner != null || !Application.isPlaying) return;
            // Lives in the game's own scene (team rule: nothing kept alive between scenes). When the shared
            // menu unloads the game, the runner goes with it and is made again the next time it is needed.
            var go = new GameObject("TweenRunner") { hideFlags = HideFlags.HideInHierarchy };
            runner = go.AddComponent<TweenRunner>();
        }
    }

    /// <summary>Hidden object that advances <see cref="Tween"/> once per frame while the game runs.</summary>
    [DefaultExecutionOrder(-100)]
    sealed class TweenRunner : MonoBehaviour
    {
        void Update() => Tween.Tick(Time.deltaTime);
    }
}
