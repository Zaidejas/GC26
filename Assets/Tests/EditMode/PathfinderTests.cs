using System;
using System.Collections.Generic;
using Lines.Core;
using NUnit.Framework;

namespace Lines.Tests
{
    public class PathfinderTests
    {
        [Test]
        public void OpenBoard_StraightLine_IsShortest()
        {
            var board = new Board();
            board.Place(new Cell(0, 0), BallColor.Red);

            var path = Pathfinder.FindPath(board, new Cell(0, 0), new Cell(8, 0));

            Assert.IsNotNull(path);
            Assert.AreEqual(9, path.Count);
            Assert.AreEqual(new Cell(0, 0), path[0]);
            Assert.AreEqual(new Cell(8, 0), path[8]);
            AssertValidPath(board, path);
        }

        [Test]
        public void OpenBoard_CornerToCorner_HasManhattanLength()
        {
            var board = new Board();
            board.Place(new Cell(0, 0), BallColor.Red);

            var path = Pathfinder.FindPath(board, new Cell(0, 0), new Cell(8, 8));

            // 8 steps right + 8 steps down = 16 steps = 17 cells.
            Assert.AreEqual(17, path.Count);
            AssertValidPath(board, path);
        }

        [Test]
        public void NeighbourCell_PathOfTwoCells()
        {
            var board = new Board();
            board.Place(new Cell(4, 4), BallColor.Red);

            var path = Pathfinder.FindPath(board, new Cell(4, 4), new Cell(4, 5));

            CollectionAssert.AreEqual(new[] { new Cell(4, 4), new Cell(4, 5) }, path);
        }

        [Test]
        public void Wall_PathGoesAroundTheGap()
        {
            // A wall in column 4 with one gap at the bottom.
            var board = TestBoards.FromRows(
                "R...B....",
                "....B....",
                "....B....",
                "....B....",
                "....B....",
                "....B....",
                "....B....",
                "....B....",
                ".........");

            var path = Pathfinder.FindPath(board, new Cell(0, 0), new Cell(8, 0));

            Assert.IsNotNull(path);
            // 4 right + 8 down to reach the gap, then 4 right + 8 up = 24 steps = 25 cells.
            Assert.AreEqual(25, path.Count);
            CollectionAssert.Contains(path, new Cell(4, 8));
            AssertValidPath(board, path);
        }

        [Test]
        public void ShortestOfTwoRoutes_IsChosen()
        {
            // Two gaps in the wall: row 1 (near) and row 8 (far).
            var board = TestBoards.FromRows(
                "R...B....",
                ".........",
                "....B....",
                "....B....",
                "....B....",
                "....B....",
                "....B....",
                "....B....",
                ".........");

            var path = Pathfinder.FindPath(board, new Cell(0, 0), new Cell(8, 0));

            // 8 right + 1 down + 1 up = 10 steps = 11 cells, through the near gap.
            Assert.AreEqual(11, path.Count);
            CollectionAssert.Contains(path, new Cell(4, 1));
            AssertValidPath(board, path);
        }

        [Test]
        public void EnclosedTarget_HasNoPath()
        {
            var board = TestBoards.FromRows(
                "R........",
                ".........",
                ".........",
                "....G....",
                "...G.G...",
                "....G....",
                ".........",
                ".........",
                ".........");

            Assert.IsNull(Pathfinder.FindPath(board, new Cell(0, 0), new Cell(4, 4)));
            Assert.IsFalse(Pathfinder.HasPath(board, new Cell(0, 0), new Cell(4, 4)));
        }

        [Test]
        public void EnclosedBall_CantMoveAnywhere()
        {
            var board = TestBoards.FromRows(
                ".........",
                ".........",
                ".........",
                "....G....",
                "...GRG...",
                "....G....",
                ".........",
                ".........",
                ".........");
            var ball = new Cell(4, 4);

            foreach (var target in board.EmptyCells())
                Assert.IsNull(Pathfinder.FindPath(board, ball, target), $"Unexpected path to {target}");
        }

        [Test]
        public void NoDiagonalMoves()
        {
            // The ball in the corner is blocked to the right and below;
            // the diagonal cell (1,1) is empty but can't be reached.
            var board = TestBoards.FromRows(
                "RB.......",
                "B........",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........");

            Assert.IsNull(Pathfinder.FindPath(board, new Cell(0, 0), new Cell(1, 1)));
        }

        [Test]
        public void OccupiedTarget_HasNoPath()
        {
            var board = new Board();
            board.Place(new Cell(0, 0), BallColor.Red);
            board.Place(new Cell(5, 5), BallColor.Blue);

            Assert.IsNull(Pathfinder.FindPath(board, new Cell(0, 0), new Cell(5, 5)));
        }

        [Test]
        public void SameCell_HasNoPath()
        {
            var board = new Board();
            board.Place(new Cell(3, 3), BallColor.Red);

            Assert.IsNull(Pathfinder.FindPath(board, new Cell(3, 3), new Cell(3, 3)));
        }

        [Test]
        public void StartWithoutBall_Throws()
        {
            var board = new Board();
            Assert.Throws<ArgumentException>(() => Pathfinder.FindPath(board, new Cell(0, 0), new Cell(1, 0)));
        }

        [Test]
        public void TargetOutsideBoard_Throws()
        {
            var board = new Board();
            board.Place(new Cell(0, 0), BallColor.Red);
            Assert.Throws<ArgumentOutOfRangeException>(() => Pathfinder.FindPath(board, new Cell(0, 0), new Cell(9, 0)));
        }

        [Test]
        public void CrowdedBoard_PathsAreAlwaysValid()
        {
            // Fill half the board with a fixed seed, then check every path found.
            var random = new Random(42);
            var board = new Board();
            foreach (var cell in board.EmptyCells())
                if (random.NextDouble() < 0.5)
                    board.Place(cell, BallColors.All[random.Next(BallColors.All.Length)]);

            var balls = new List<Cell>();
            for (int row = 0; row < board.Size; row++)
            for (int column = 0; column < board.Size; column++)
                if (!board.IsEmpty(new Cell(column, row)))
                    balls.Add(new Cell(column, row));

            int found = 0;
            foreach (var ball in balls)
            foreach (var target in board.EmptyCells())
            {
                var path = Pathfinder.FindPath(board, ball, target);
                if (path == null) continue;
                found++;
                Assert.AreEqual(ball, path[0]);
                Assert.AreEqual(target, path[path.Count - 1]);
                AssertValidPath(board, path);
            }
            Assert.Greater(found, 0);
        }

        /// <summary>Each step moves one cell up/down/left/right, and every cell after the start is empty.</summary>
        static void AssertValidPath(Board board, List<Cell> path)
        {
            for (int i = 1; i < path.Count; i++)
            {
                int distance = Math.Abs(path[i].Column - path[i - 1].Column) + Math.Abs(path[i].Row - path[i - 1].Row);
                Assert.AreEqual(1, distance, $"Step {i - 1}→{i} is not to a neighbouring cell");
                Assert.IsTrue(board.IsEmpty(path[i]), $"Path goes through a ball at {path[i]}");
            }
            CollectionAssert.AllItemsAreUnique(path);
        }
    }
}
