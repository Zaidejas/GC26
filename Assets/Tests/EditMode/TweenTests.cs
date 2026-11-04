using System.Collections.Generic;
using Lines.View;
using NUnit.Framework;
using UnityEngine;

namespace Lines.Tests
{
    public class TweenTests
    {
        readonly List<GameObject> created = new List<GameObject>();

        [SetUp] public void SetUp() => Tween.KillAll();

        [TearDown]
        public void TearDown()
        {
            Tween.KillAll();
            foreach (var go in created) if (go != null) Object.DestroyImmediate(go);
            created.Clear();
        }

        Transform NewObject()
        {
            var go = new GameObject("tween test");
            created.Add(go);
            return go.transform;
        }

        static void Run(float seconds, float step = 0.01f)
        {
            for (float t = 0; t < seconds - 1e-4f; t += step) Tween.Tick(step);
        }

        // ---- Easing ----

        [Test]
        public void Easing_StartsAtZeroEndsAtOne()
        {
            foreach (Ease ease in System.Enum.GetValues(typeof(Ease)))
            {
                Assert.AreEqual(0f, Easing.Evaluate(ease, 0f), 1e-4f, ease.ToString());
                Assert.AreEqual(1f, Easing.Evaluate(ease, 1f), 1e-4f, ease.ToString());
            }
        }

        [Test]
        public void Easing_Shapes()
        {
            Assert.AreEqual(0.5f, Easing.Evaluate(Ease.Linear, 0.5f), 1e-4f);
            Assert.Less(Easing.Evaluate(Ease.InQuad, 0.5f), 0.5f, "Starts slow");
            Assert.Greater(Easing.Evaluate(Ease.OutQuad, 0.5f), 0.5f, "Starts fast");
            Assert.Greater(Easing.Evaluate(Ease.OutBack, 0.8f), 1f, "Overshoots before settling");
        }

        // ---- Move / Scale ----

        [Test]
        public void Move_ReachesTargetAfterDuration_AndCallsOnCompleteOnce()
        {
            var t = NewObject();
            int done = 0;
            var handle = Tween.Move(t, new Vector3(2, 0, 0), 0.2f, Ease.Linear).OnComplete(() => done++);

            Run(0.1f);
            Assert.AreEqual(1f, t.localPosition.x, 0.02f, "Halfway");
            Assert.IsFalse(handle.IsDone);

            Run(0.15f);
            Assert.AreEqual(new Vector3(2, 0, 0), t.localPosition);
            Assert.IsTrue(handle.IsDone);
            Assert.AreEqual(1, done);
            Assert.AreEqual(0, Tween.ActiveCount);
        }

        [Test]
        public void Scale_ReachesTarget()
        {
            var t = NewObject();
            t.localScale = Vector3.zero;
            Tween.Scale(t, Vector3.one, 0.22f, Ease.OutBack);
            Run(0.3f);
            Assert.AreEqual(Vector3.one, t.localScale);
        }

        [Test]
        public void OnComplete_AfterFinishing_RunsImmediately()
        {
            var t = NewObject();
            var handle = Tween.Move(t, Vector3.one, 0.05f);
            Run(0.1f);
            bool called = false;
            handle.OnComplete(() => called = true);
            Assert.IsTrue(called);
        }

        // ---- Path (the ball rolling cell by cell) ----

        [Test]
        public void MovePath_VisitsEachPointAtItsTime_AndEndsOnTheLast()
        {
            var t = NewObject();
            var points = new[] { new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(2, 1, 0) };
            Tween.MovePath(t, points, 0.04f); // 40 ms per cell, as in the prototype

            Run(0.04f);
            Assert.AreEqual(points[0].x, t.localPosition.x, 0.01f);
            Assert.AreEqual(points[0].y, t.localPosition.y, 0.01f);
            Run(0.04f);
            Assert.AreEqual(points[1].y, t.localPosition.y, 0.01f);
            Run(0.06f);
            Assert.AreEqual(points[2], t.localPosition);
        }

        // ---- Shake / Bounce ----

        [Test]
        public void Shake_MovesAndReturnsToStart()
        {
            var t = NewObject();
            t.localPosition = new Vector3(3, 3, 0);
            Tween.Shake(t, 0.2f, 0.32f);

            float maxOffset = 0;
            for (int i = 0; i < 40; i++)
            {
                Tween.Tick(0.01f);
                maxOffset = Mathf.Max(maxOffset, Mathf.Abs(t.localPosition.x - 3));
            }
            Assert.Greater(maxOffset, 0.05f, "It did shake");
            Assert.AreEqual(new Vector3(3, 3, 0), t.localPosition, "And came back");
        }

        [Test]
        public void Bounce_LoopsUntilKilled_ThenRestoresPosition()
        {
            var t = NewObject();
            var handle = Tween.Bounce(t, 0.15f, 0.36f);

            Run(0.36f);
            Assert.AreEqual(0.15f, t.localPosition.y, 0.01f, "Top of the bounce");
            Run(0.36f);
            Assert.AreEqual(0f, t.localPosition.y, 0.01f, "Back down");
            Run(3f);
            Assert.IsFalse(handle.IsDone, "Still bouncing");

            Run(0.1f);
            handle.Kill();
            Assert.AreEqual(Vector3.zero, t.localPosition);
            Tween.Tick(0.01f);
            Assert.AreEqual(0, Tween.ActiveCount);
        }

        // ---- Delay / Kill / destroyed objects ----

        [Test]
        public void Delay_RunsActionLater()
        {
            bool called = false;
            Tween.Delay(0.25f, () => called = true);
            Run(0.2f);
            Assert.IsFalse(called);
            Run(0.1f);
            Assert.IsTrue(called);
        }

        [Test]
        public void KillTarget_StopsItsAnimations_OthersContinue()
        {
            var a = NewObject();
            var b = NewObject();
            Tween.Move(a, Vector3.one, 1f);
            Tween.Move(b, Vector3.one, 0.1f);

            Run(0.05f);
            Tween.Kill(a);
            var stoppedAt = a.localPosition;
            Run(0.2f);

            Assert.AreEqual(stoppedAt, a.localPosition);
            Assert.AreEqual(Vector3.one, b.localPosition);
        }

        [Test]
        public void KillWithComplete_JumpsToEnd()
        {
            var t = NewObject();
            bool done = false;
            Tween.Move(t, new Vector3(5, 0, 0), 1f).OnComplete(() => done = true);
            Run(0.1f);
            Tween.Kill(t, complete: true);
            Assert.AreEqual(new Vector3(5, 0, 0), t.localPosition);
            Assert.IsTrue(done);
        }

        [Test]
        public void DestroyedObject_IsDroppedWithoutErrors()
        {
            var t = NewObject();
            Tween.Move(t, Vector3.one, 1f);
            Object.DestroyImmediate(t.gameObject);

            Assert.DoesNotThrow(() => Tween.Tick(0.1f));
            Assert.AreEqual(0, Tween.ActiveCount);
        }

        [Test]
        public void StartingAnimationFromCallback_Works()
        {
            var t = NewObject();
            Tween.Move(t, new Vector3(1, 0, 0), 0.1f, Ease.Linear)
                .OnComplete(() => Tween.Move(t, new Vector3(1, 1, 0), 0.1f, Ease.Linear));
            Run(0.25f);
            Assert.AreEqual(new Vector3(1, 1, 0), t.localPosition);
        }
    }
}
