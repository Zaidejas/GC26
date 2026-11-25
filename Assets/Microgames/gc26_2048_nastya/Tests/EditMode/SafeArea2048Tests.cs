using NUnit.Framework;
using UnityEngine;

namespace GC26._2048Nastya.Tests
{
    /// <summary>T58: safe area → normalized anchors.</summary>
    public class SafeArea2048Tests
    {
        [Test]
        public void FullScreen_GivesFullAnchors()
        {
            var (min, max) = SafeArea2048.Anchors(new Rect(0, 0, 1080, 1920), 1080, 1920);
            Assert.AreEqual(Vector2.zero, min);
            Assert.AreEqual(Vector2.one, max);
        }

        [Test]
        public void PortraitNotch_CutsTheTop()
        {
            // 1080×2400 phone with a 100 px notch at the top and a 60 px gesture bar at the bottom.
            var (min, max) = SafeArea2048.Anchors(new Rect(0, 60, 1080, 2240), 1080, 2400);
            Assert.AreEqual(0f, min.x, 1e-6f);
            Assert.AreEqual(60f / 2400f, min.y, 1e-6f);
            Assert.AreEqual(1f, max.x, 1e-6f);
            Assert.AreEqual(2300f / 2400f, max.y, 1e-6f);
        }

        [Test]
        public void LandscapeNotch_CutsTheSide()
        {
            var (min, max) = SafeArea2048.Anchors(new Rect(100, 0, 2300, 1080), 2400, 1080);
            Assert.AreEqual(100f / 2400f, min.x, 1e-6f);
            Assert.AreEqual(1f, max.y, 1e-6f);
        }

        [Test]
        public void UnknownScreen_FallsBackToFullScreen()
        {
            var (min, max) = SafeArea2048.Anchors(new Rect(0, 0, 0, 0), 0, 0);
            Assert.AreEqual(Vector2.zero, min);
            Assert.AreEqual(Vector2.one, max);
        }
    }
}
