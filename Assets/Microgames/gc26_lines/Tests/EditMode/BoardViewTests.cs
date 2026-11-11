using GC26.Lines.Core;
using GC26.Lines.View;
using NUnit.Framework;
using UnityEngine;

namespace GC26.Lines.Tests
{
    public class BoardViewTests
    {
        BoardView view;

        [SetUp] public void SetUp() => view = BoardView.Create();
        [TearDown] public void TearDown() => Object.DestroyImmediate(view.gameObject);

        [Test]
        public void ShowBoard_OneBallPerOccupiedCell_InTheRightPlace()
        {
            var board = TestBoards.FromRows(
                "R........",
                ".........",
                ".........",
                ".........",
                "....G....",
                ".........",
                ".........",
                ".........",
                "........B");

            view.ShowBoard(board);

            Assert.AreEqual(3, view.BallCount);
            Assert.AreEqual(BallColor.Green, view.BallAt(new Cell(4, 4)).Color);
            Assert.AreEqual(Vector3.zero, view.BallAt(new Cell(4, 4)).transform.localPosition, "Centre cell at the board centre");
            Assert.AreEqual(new Vector3(-4, 4, 0), view.BallAt(new Cell(0, 0)).transform.localPosition, "Top-left");
            Assert.AreEqual(new Vector3(4, -4, 0), view.BallAt(new Cell(8, 8)).transform.localPosition, "Bottom-right");
            Assert.IsNull(view.BallAt(new Cell(1, 1)));
        }

        [Test]
        public void ShowBoardAgain_ReusesBallObjects()
        {
            var full = new Board();
            foreach (var cell in full.EmptyCells()) full.Place(cell, BallColor.Red);
            view.ShowBoard(full);
            int created = view.GetComponentsInChildren<BallView>(true).Length;

            var few = new Board();
            few.Place(new Cell(0, 0), BallColor.Blue);
            view.ShowBoard(few);
            Assert.AreEqual(1, view.BallCount);
            Assert.AreEqual(80, view.PooledCount, "The other balls wait in the pool");

            view.ShowBoard(full);
            Assert.AreEqual(created, view.GetComponentsInChildren<BallView>(true).Length, "No new objects were made");
        }
    }
}
