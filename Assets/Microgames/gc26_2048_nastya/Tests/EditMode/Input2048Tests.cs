using NUnit.Framework;
using UnityEngine;

namespace GC26._2048Nastya.Tests
{
    /// <summary>Swipe direction (TDD 7.2) and the easing curves (TDD 6.3).</summary>
    public class Input2048Tests
    {
        const float MinLength = 54f; // 5% of 1080

        [TestCase(100f, 10f, Direction2048.Right)]
        [TestCase(-100f, 30f, Direction2048.Left)]
        [TestCase(20f, 100f, Direction2048.Up)]   // screen y points up
        [TestCase(-20f, -100f, Direction2048.Down)]
        [TestCase(70f, -60f, Direction2048.Right)] // dominant axis wins
        public void Swipe_UsesDominantAxis(float x, float y, Direction2048 expected)
        {
            Assert.IsTrue(Input2048.TrySwipeDirection(new Vector2(x, y), MinLength, out var dir));
            Assert.AreEqual(expected, dir);
        }

        [TestCase(0f, 0f)]
        [TestCase(40f, 30f)]  // length 50 < 54
        [TestCase(0f, -53f)]
        public void Swipe_ShorterThanThreshold_IsIgnored(float x, float y)
        {
            Assert.IsFalse(Input2048.TrySwipeDirection(new Vector2(x, y), MinLength, out _));
        }

        [Test]
        public void Swipe_AtThreshold_Counts()
        {
            Assert.IsTrue(Input2048.TrySwipeDirection(new Vector2(0f, -MinLength), MinLength, out var dir));
            Assert.AreEqual(Direction2048.Down, dir);
        }

        [Test]
        public void EaseOut_StartsAtZero_EndsAtOne_IsMonotonic()
        {
            Assert.AreEqual(0f, Tween2048.EaseOut(0f), 1e-6f);
            Assert.AreEqual(1f, Tween2048.EaseOut(1f), 1e-6f);
            float prev = 0f;
            for (int i = 1; i <= 20; i++)
            {
                float v = Tween2048.EaseOut(i / 20f);
                Assert.GreaterOrEqual(v, prev);
                prev = v;
            }
            Assert.Greater(Tween2048.EaseOut(0.5f), 0.5f, "ease-out is ahead of linear");
        }

        [Test]
        public void Hump_ReturnsToZero()
        {
            Assert.AreEqual(0f, Tween2048.Hump(0f), 1e-6f);
            Assert.AreEqual(1f, Tween2048.Hump(0.5f), 1e-6f);
            Assert.AreEqual(0f, Tween2048.Hump(1f), 1e-5f);
        }

        [Test]
        public void Theme_WholeMoveFitsInQuarterSecond()
        {
            // The theme asset the scene uses (artists may tweak it), GDD 5.3: a move takes at most 0.25 s.
            var theme = UnityEditor.AssetDatabase.LoadAssetAtPath<Theme2048>(
                "Assets/Microgames/gc26_2048_nastya/Art/gc26_2048_nastya_Theme.asset");
            Assert.IsNotNull(theme, "theme asset");
            Assert.LessOrEqual(theme.slide + Mathf.Max(theme.pop, theme.spawn), 0.25f + 1e-6f);
        }

        [TestCase(2, 1.05f)]
        [TestCase(2048, 1.55f)]
        [TestCase(4096, 1.6f)]    // capped
        [TestCase(131072, 1.6f)]
        public void MergePitch_RisesWithValue_Capped(int value, float expected)
        {
            Assert.AreEqual(expected, Sfx2048.MergePitch(value), 1e-4f);
        }
    }
}
