using System.Collections;
using Lines.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Lines.Tests
{
    /// <summary>Animations running in Unity's real frame loop (Play mode), not driven by the test.</summary>
    public class TweenPlayModeTests
    {
        [UnityTest]
        public IEnumerator SpriteMovesAndScales_FrameByFrame()
        {
            var go = new GameObject("tweened sprite");
            go.AddComponent<SpriteRenderer>();
            var t = go.transform;
            t.localScale = Vector3.zero;

            var move = Tween.Move(t, new Vector3(2, 1, 0), 0.25f);
            var scale = Tween.Scale(t, Vector3.one, 0.25f, Ease.OutBack);

            yield return null;
            yield return null;
            Assert.IsFalse(move.IsDone, "Still running after two frames");

            yield return move.Wait();
            yield return scale.Wait();

            Assert.AreEqual(new Vector3(2, 1, 0), t.localPosition);
            Assert.AreEqual(Vector3.one, t.localScale);
            Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator Delay_FiresAfterRealTime()
        {
            bool called = false;
            float start = Time.time;
            yield return Tween.Delay(0.2f, () => called = true).Wait();
            Assert.IsTrue(called);
            Assert.GreaterOrEqual(Time.time - start, 0.19f);
        }
    }
}
