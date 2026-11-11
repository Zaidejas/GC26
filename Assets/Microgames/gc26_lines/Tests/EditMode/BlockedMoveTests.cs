using GC26.Lines.Core;
using GC26.Lines.Game;
using GC26.Lines.UI;
using GC26.Lines.View;
using NUnit.Framework;
using UnityEngine;

namespace GC26.Lines.Tests
{
    public class BlockedMoveTests
    {
        BoardView view;
        HudView hud;
        Camera camera;
        BoardPresenter presenter;
        static readonly Cell Enclosed = new Cell(4, 4);
        static readonly Cell Other = new Cell(0, 0);

        [SetUp]
        public void SetUp()
        {
            Tween.KillAll();
            camera = new GameObject("Camera").AddComponent<Camera>();
            view = BoardView.Create();
            hud = HudView.Create(camera);
            hud.ApplyLayout(BoardLayout.Compute(new Vector2(1080, 1920), new Rect(0, 0, 1080, 1920)));
            presenter = new BoardPresenter(view, hud);
            var board = TestBoards.FromRows(
                "B........",
                ".........",
                ".........",
                "....G....",
                "...GRG...",
                "....G....",
                ".........",
                ".........",
                ".........");
            presenter.ShowState(new GameState(board, new Preview(new[] { BallColor.Red }, new Cell[0]), 0, new Spawner(1)));
        }

        [TearDown]
        public void TearDown()
        {
            Tween.KillAll();
            Object.DestroyImmediate(view.gameObject);
            Object.DestroyImmediate(hud.gameObject);
            Object.DestroyImmediate(camera.gameObject);
        }

        static void Run(float seconds) { for (float t = 0; t < seconds - 1e-4f; t += 0.01f) Tween.Tick(0.01f); }

        Vector3 Pos(Cell cell) => view.BallAt(cell).transform.localPosition;

        [Test]
        public void Blocked_ShowsNoPath_ThenItFadesAway()
        {
            presenter.ShowSelected(Enclosed);
            presenter.ShowBlocked(Enclosed, new Cell(8, 8));

            Assert.AreEqual("No path", hud.Alert.Text);
            Run(0.16f);
            Assert.AreEqual(1f, hud.Alert.Alpha, 0.01f, "Fully shown after 0.15 s");
            Run(0.9f);
            Assert.IsTrue(hud.Alert.IsShowing, "Still there at about 1 s");
            Run(0.4f);
            Assert.AreEqual(0f, hud.Alert.Alpha, 0.01f, "Gone after 1.1 s + fade");
            Assert.IsFalse(hud.Alert.IsShowing);
        }

        [Test]
        public void Blocked_BallShakes_ReturnsToItsCell_AndKeepsBouncing()
        {
            var rest = BoardView.CellToLocal(Enclosed);
            presenter.ShowSelected(Enclosed);
            Run(0.2f);

            presenter.ShowBlocked(Enclosed, new Cell(8, 8));
            float maxSideways = 0;
            for (int i = 0; i < 32; i++)
            {
                Tween.Tick(0.01f);
                maxSideways = Mathf.Max(maxSideways, Mathf.Abs(Pos(Enclosed).x - rest.x));
                Assert.AreEqual(rest.y, Pos(Enclosed).y, 1e-4f, "No bouncing while it shakes");
            }
            Assert.Greater(maxSideways, 0.05f, "It shook left and right");
            Tween.Tick(0.01f);
            Assert.AreEqual(rest.x, Pos(Enclosed).x, 1e-4f, "Back in its cell");

            Run(0.36f);
            Assert.Greater(Pos(Enclosed).y, rest.y + 0.1f, "Still selected: bouncing again");
        }

        [Test]
        public void SelectingAnotherBallDuringShake_OldBallSettles()
        {
            presenter.ShowSelected(Enclosed);
            presenter.ShowBlocked(Enclosed, new Cell(8, 8));
            Run(0.1f);

            presenter.ShowSelected(Other);
            Run(0.5f);

            Assert.AreEqual(BoardView.CellToLocal(Enclosed), Pos(Enclosed), "Old ball sits still in its cell");
            Assert.AreNotEqual(BoardView.CellToLocal(Other), Pos(Other), "New ball bounces");
        }

        [Test]
        public void BlockedTwiceInARow_StillEndsInPlace()
        {
            presenter.ShowSelected(Enclosed);
            presenter.ShowBlocked(Enclosed, new Cell(8, 8));
            Run(0.1f);
            presenter.ShowBlocked(Enclosed, new Cell(8, 7));
            Run(0.33f);
            Assert.AreEqual(BoardView.CellToLocal(Enclosed).x, Pos(Enclosed).x, 1e-4f);
            Assert.IsTrue(hud.Alert.IsShowing, "The message restarts its timer");
        }
    }
}
