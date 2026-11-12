using System.Linq;
using GC26.Lines.Core;
using GC26.Lines.Game;
using GC26.Lines.UI;
using GC26.Lines.View;
using NUnit.Framework;
using UnityEngine;

namespace GC26.Lines.Tests
{
    public class SpawnClearAnimationTests
    {
        BoardView view;
        HudView hud;
        Camera camera;
        BoardPresenter presenter;

        [SetUp]
        public void SetUp()
        {
            Tween.KillAll();
            camera = new GameObject("Camera").AddComponent<Camera>();
            view = BoardView.Create();
            hud = HudView.Create(camera);
            presenter = new BoardPresenter(view, hud);
        }

        [TearDown]
        public void TearDown()
        {
            Tween.KillAll();
            Object.DestroyImmediate(view.gameObject);
            Object.DestroyImmediate(hud.gameObject);
            Object.DestroyImmediate(camera.gameObject);
        }

        void Show(Board board, Preview preview) =>
            presenter.ShowState(new GameState(board, preview, 0, new Spawner(1)));

        static void Run(float seconds) { for (float t = 0; t < seconds - 1e-4f; t += 0.01f) Tween.Tick(0.01f); }

        int ParticleCount => view.GetComponentsInChildren<SpriteRenderer>().Count(r => r.name == "Particle");

        // ---- Grow in ----

        [Test]
        public void SpawnedPieces_GrowIn_ThenTheTurnContinues()
        {
            var cells = new[] { new Cell(1, 1), new Cell(7, 7) };
            Show(new Board(), new Preview(new[] { BallColor.Red, BallColor.Blue, BallColor.Green }, cells));
            Assert.AreEqual(2, view.DotCells.Count(), "Dots before the spawn");
            bool done = false;

            presenter.PlaySpawn(cells, new[] { BallColor.Red, BallColor.Blue }, () => done = true);

            var ball = view.BallAt(new Cell(1, 1));
            Assert.AreEqual(BallColor.Red, ball.Color);
            Assert.AreEqual(BallColor.Blue, view.BallAt(new Cell(7, 7)).Color);
            Assert.AreEqual(Vector3.zero, ball.transform.localScale, "Starts at nothing");
            Assert.IsEmpty(view.DotCells, "The dots are covered by the new pieces");

            Run(0.11f);
            Assert.Greater(ball.transform.localScale.x, 0f);
            Assert.Less(ball.transform.localScale.x, ball.FullScale.x, "Still growing");
            Run(0.12f);
            Assert.AreEqual(ball.FullScale, ball.transform.localScale, "Full size at 0.22 s");
            Assert.IsFalse(done);
            Run(0.03f);
            Assert.IsTrue(done, "Turn continues at 0.24 s");
        }

        // ---- Pop and burst ----

        static Board RedRow() => TestBoards.FromRows(
            ".........",
            ".........",
            ".........",
            ".........",
            "RRRRR....",
            ".........",
            ".........",
            ".........",
            ".........");

        static Cell[] RowCells() => Enumerable.Range(0, 5).Select(c => new Cell(c, 4)).ToArray();

        [Test]
        public void ClearedPieces_SwellThenVanish_WithParticles_ThenScore()
        {
            Show(RedRow(), new Preview(new[] { BallColor.Blue }, new Cell[0]));
            var ball = view.BallAt(new Cell(2, 4));
            var full = ball.FullScale;
            bool done = false;

            presenter.PlayClear(RowCells(), 10, () => done = true);

            Assert.AreEqual(5 * BoardPresenter.ParticlesPerPiece, ParticleCount, "7 particles per piece");
            Run(0.1f);
            Assert.Greater(ball.transform.localScale.x, full.x, "Swells first");
            Assert.AreEqual("0", hud.ScoreText, "Score waits for the pop");

            Run(0.17f);
            Assert.IsTrue(done, "Turn continues at 0.26 s");
            Assert.IsNull(view.BallAt(new Cell(2, 4)), "Pieces removed");
            Assert.AreEqual(0, view.BallCount);
            Assert.AreEqual("10", hud.ScoreText, "Points added");
            Assert.Greater(ParticleCount, 0, "Particles still flying");

            Run(0.25f);
            Assert.AreEqual(0, ParticleCount, "Particles gone by 0.45 s");
        }

        [Test]
        public void Particles_FlyOutward_AndFade()
        {
            Show(RedRow(), new Preview(new[] { BallColor.Blue }, new Cell[0]));
            presenter.PlayClear(new[] { new Cell(4, 4) }, 0, () => { });
            var centre = BoardView.CellToLocal(new Cell(4, 4));
            var particles = view.GetComponentsInChildren<SpriteRenderer>().Where(r => r.name == "Particle").ToList();

            Run(0.3f);

            foreach (var p in particles)
            {
                Assert.Greater(Vector3.Distance(p.transform.localPosition, centre), 0.3f, "Moved away from the cell");
                Assert.Less(p.color.a, 0.5f, "Fading");
            }
        }

        [Test]
        public void ReusedBall_IsFullyVisibleAgain()
        {
            Show(RedRow(), new Preview(new[] { BallColor.Blue }, new Cell[0]));
            presenter.PlayClear(RowCells(), 10, () => { });
            Run(0.5f);

            // The popped balls went back to the pool; new ones taken from it must not stay faded or tiny.
            presenter.PlaySpawn(new[] { new Cell(8, 8) }, new[] { BallColor.Green }, () => { });
            Run(0.3f);
            var ball = view.BallAt(new Cell(8, 8));
            Assert.AreEqual(ball.FullScale, ball.transform.localScale);
            Assert.AreEqual(1f, ball.Renderer.color.a);
        }
    }
}
