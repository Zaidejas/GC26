using GC26.Lines.UI;
using GC26.Lines.View;
using NUnit.Framework;
using UnityEngine;

namespace GC26.Lines.Tests
{
    /// <summary>L04: the crown next to the score and the fireworks (GDD → Scoring).</summary>
    public class BestScoreTests
    {
        Camera camera;
        HudView hud;
        Fireworks fireworks;

        [SetUp]
        public void SetUp()
        {
            Tween.KillAll();
            camera = new GameObject("Camera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 9.6f;
            camera.aspect = 1080f / 1920f;
        }

        [TearDown]
        public void TearDown()
        {
            Tween.KillAll();
            if (hud != null) Object.DestroyImmediate(hud.gameObject);
            if (fireworks != null) Object.DestroyImmediate(fireworks.gameObject);
            Object.DestroyImmediate(camera.gameObject);
        }

        [Test]
        public void Crown_SitsRightOfTheDigits_LevelWithTheirMiddle_HalfTheirHeight()
        {
            hud = HudView.Create(camera);
            var layout = BoardLayout.Compute(new Vector2(1080, 1920), new Rect(0, 0, 1080, 1920));
            hud.ApplyLayout(layout);
            foreach (int score in new[] { 52, 1140 })
            {
                hud.SetScore(score);
                hud.SetCrown(true);
                Assert.IsTrue(hud.CrownShowing);
                Assert.AreEqual(hud.ScoreMiddleY, hud.CrownCentre.y, 0.5f, score + ": level with the middle of the digits");
                Assert.AreEqual(HudView.CrownSize * layout.Unit, hud.CrownHeight, 0.01f);
            }
            hud.SetScore(52);
            float near = hud.CrownCentre.x;
            hud.SetScore(1140);
            Assert.Greater(hud.CrownCentre.x, near, "It moves right as the number grows");
            hud.SetCrown(false);
            Assert.IsFalse(hud.CrownShowing);
        }

        [Test]
        public void CrownPicture_IsMadeOnce()
        {
            var crown = SpriteFactory.Crown();
            Assert.AreEqual(128, crown.texture.width);
            Assert.AreSame(crown, SpriteFactory.Crown(), "Drawn once, then reused");
        }

        [Test]
        public void Fireworks_FiveBursts_ThenAllGone()
        {
            fireworks = Fireworks.Create(camera);
            Assert.IsFalse(fireworks.Playing);
            fireworks.Play();
            fireworks.Advance(0.1f);
            Assert.AreEqual(Fireworks.SparksPerBurst, fireworks.VisibleSparks, "The first burst at once");
            for (int i = 0; i < 10; i++) fireworks.Advance(0.1f);
            Assert.Greater(fireworks.VisibleSparks, 2 * Fireworks.SparksPerBurst, "Later bursts while earlier ones fade");
            for (int i = 0; i < 30; i++) fireworks.Advance(0.1f);
            Assert.IsFalse(fireworks.Playing, "Over within about 2.5 s");
            Assert.AreEqual(0, fireworks.VisibleSparks);
        }

        [Test]
        public void NewBest_PopsUp_ThenFades()
        {
            hud = HudView.Create(camera);
            hud.ApplyLayout(BoardLayout.Compute(new Vector2(1080, 1920), new Rect(0, 0, 1080, 1920)));
            Assert.IsFalse(hud.NewBestShowing);
            hud.ShowNewBest();
            for (int i = 0; i < 50; i++) Tween.Tick(0.01f);
            Assert.IsTrue(hud.NewBestShowing);
            Assert.AreEqual("New best!", hud.NewBestText);
            for (int i = 0; i < 200; i++) Tween.Tick(0.01f);
            Assert.IsFalse(hud.NewBestShowing, "Gone after 2 s");

            hud.ShowNewBest();
            for (int i = 0; i < 50; i++) Tween.Tick(0.01f);
            hud.StopNewBest(); // a new game starts mid-way
            Assert.IsFalse(hud.NewBestShowing);
            for (int i = 0; i < 100; i++) Tween.Tick(0.01f);
            Assert.IsFalse(hud.NewBestShowing, "It doesn't come back");
        }
    }
}
