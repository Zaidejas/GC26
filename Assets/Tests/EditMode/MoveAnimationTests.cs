using Lines.Core;
using Lines.Game;
using Lines.View;
using NUnit.Framework;
using UnityEngine;

namespace Lines.Tests
{
    public class MoveAnimationTests
    {
        BoardView view;
        BoardPresenter presenter;

        [SetUp]
        public void SetUp()
        {
            Tween.KillAll();
            view = BoardView.Create();
            presenter = new BoardPresenter(view);
            var board = TestBoards.FromRows(
                "R........",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
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
        }

        static void Run(float seconds) { for (float t = 0; t < seconds - 1e-4f; t += 0.01f) Tween.Tick(0.01f); }

        [Test]
        public void Ball_RollsCellByCell_40msPerCell_ThenDone()
        {
            // (0,0) → (1,0) → (2,0) → (2,1): 3 steps = 120 ms.
            var path = new[] { new Cell(0, 0), new Cell(1, 0), new Cell(2, 0), new Cell(2, 1) };
            var ball = view.BallAt(new Cell(0, 0)).transform;
            bool done = false;

            presenter.PlayMove(path, () => done = true);

            Run(0.04f);
            AssertAt(ball, new Cell(1, 0), "After 40 ms");
            Run(0.04f);
            AssertAt(ball, new Cell(2, 0), "After 80 ms");
            Assert.IsFalse(done, "Still moving");
            Run(0.05f);
            AssertAt(ball, new Cell(2, 1), "Arrived");
            Assert.IsTrue(done);
        }

        [Test]
        public void AfterTheMove_TheBallBelongsToItsNewCell()
        {
            var path = new[] { new Cell(0, 0), new Cell(0, 1) };
            var ball = view.BallAt(new Cell(0, 0));

            presenter.PlayMove(path, () => { });
            Run(0.1f);

            Assert.AreSame(ball, view.BallAt(new Cell(0, 1)));
            Assert.IsNull(view.BallAt(new Cell(0, 0)));
        }

        [Test]
        public void LongerPath_TakesLonger()
        {
            // 8 steps across the top row = 320 ms.
            var path = new Cell[9];
            for (int i = 0; i < 9; i++) path[i] = new Cell(i, 0);
            bool done = false;

            presenter.PlayMove(path, () => done = true);
            Run(0.3f);
            Assert.IsFalse(done);
            Run(0.03f);
            Assert.IsTrue(done);
        }

        static void AssertAt(Transform ball, Cell cell, string when)
        {
            var expected = BoardView.CellToLocal(cell);
            Assert.AreEqual(expected.x, ball.localPosition.x, 0.02f, when);
            Assert.AreEqual(expected.y, ball.localPosition.y, 0.02f, when);
        }
    }
}
