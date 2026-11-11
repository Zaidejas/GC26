using System;
using System.Linq;
using GC26.Lines.Core;
using NUnit.Framework;

namespace GC26.Lines.Tests
{
    public class LineDetectorTests
    {
        [Test]
        public void Row_OfFive_IsFound()
        {
            var board = TestBoards.FromRows(
                ".........",
                ".........",
                ".........",
                ".........",
                "..RRRRR..",
                ".........",
                ".........",
                ".........",
                ".........");

            var lines = LineDetector.FindLinesThrough(board, new Cell(4, 4));

            Assert.AreEqual(1, lines.Count);
            Assert.AreEqual(BallColor.Red, lines[0].Color);
            Assert.AreEqual(5, lines[0].Length);
            CollectionAssert.AreEqual(Enumerable.Range(2, 5).Select(c => new Cell(c, 4)), lines[0].Cells);
        }

        [Test]
        public void Column_OfFive_IsFound()
        {
            var board = TestBoards.FromRows(
                "....G....",
                "....G....",
                "....G....",
                "....G....",
                "....G....",
                ".........",
                ".........",
                ".........",
                ".........");

            var lines = LineDetector.FindLinesThrough(board, new Cell(4, 0));

            Assert.AreEqual(1, lines.Count);
            Assert.AreEqual(5, lines[0].Length);
        }

        [Test]
        public void DiagonalDownRight_IsFound()
        {
            var board = TestBoards.FromRows(
                "B........",
                ".B.......",
                "..B......",
                "...B.....",
                "....B....",
                ".........",
                ".........",
                ".........",
                ".........");

            var lines = LineDetector.FindLinesThrough(board, new Cell(2, 2));

            Assert.AreEqual(1, lines.Count);
            Assert.AreEqual(5, lines[0].Length);
        }

        [Test]
        public void DiagonalUpRight_IsFound()
        {
            var board = TestBoards.FromRows(
                ".........",
                ".........",
                ".........",
                ".........",
                "........P",
                ".......P.",
                "......P..",
                ".....P...",
                "....P....");

            var lines = LineDetector.FindLinesThrough(board, new Cell(8, 4));

            Assert.AreEqual(1, lines.Count);
            Assert.AreEqual(5, lines[0].Length);
        }

        [Test]
        public void FourInARow_IsNotALine()
        {
            var board = TestBoards.FromRows(
                ".........",
                ".........",
                ".........",
                ".........",
                "..YYYY...",
                ".........",
                ".........",
                ".........",
                ".........");

            Assert.IsEmpty(LineDetector.FindLinesThrough(board, new Cell(3, 4)));
        }

        [Test]
        public void DifferentColors_DontJoin()
        {
            var board = TestBoards.FromRows(
                ".........",
                ".........",
                ".........",
                ".........",
                "RRRBRRR..",
                ".........",
                ".........",
                ".........",
                ".........");

            Assert.IsEmpty(LineDetector.FindLinesThrough(board, new Cell(1, 4)));
            Assert.IsEmpty(LineDetector.FindLinesThrough(board, new Cell(3, 4)));
        }

        [Test]
        public void LongerLines_KeepTheirFullLength()
        {
            var board = TestBoards.FromRows(
                "CCCCCCC..",
                ".........",
                ".........",
                ".........",
                "OOOOOOOOO",
                ".........",
                ".........",
                ".........",
                ".........");

            Assert.AreEqual(7, LineDetector.FindLinesThrough(board, new Cell(0, 0))[0].Length);
            Assert.AreEqual(9, LineDetector.FindLinesThrough(board, new Cell(8, 4))[0].Length);
        }

        [Test]
        public void CellInTheMiddle_FindsWholeLine()
        {
            // The moved ball lands in the middle and joins two runs: 3 + it + 2 = 6.
            var board = TestBoards.FromRows(
                ".........",
                ".........",
                ".........",
                ".........",
                ".GGGGGG..",
                ".........",
                ".........",
                ".........",
                ".........");

            var lines = LineDetector.FindLinesThrough(board, new Cell(4, 4));

            Assert.AreEqual(1, lines.Count);
            Assert.AreEqual(6, lines[0].Length);
        }

        [Test]
        public void CrossingLines_BothFound_SharedCellClearedOnce()
        {
            // A "+" of two 5-lines sharing the centre cell (4,4).
            var board = TestBoards.FromRows(
                ".........",
                ".........",
                "....R....",
                "....R....",
                "..RRRRR..",
                "....R....",
                "....R....",
                ".........",
                ".........");

            var lines = LineDetector.FindLinesThrough(board, new Cell(4, 4));

            Assert.AreEqual(2, lines.Count);
            Assert.IsTrue(lines.All(l => l.Length == 5), "Each line keeps the shared cell in its length (GDD scoring)");
            var cells = LineDetector.CellsToClear(lines);
            Assert.AreEqual(9, cells.Count);
            CollectionAssert.AllItemsAreUnique(cells);
        }

        [Test]
        public void AllFourDirections_ThroughOneCell()
        {
            // A star: row, column and both diagonals through (4,4).
            var board = TestBoards.FromRows(
                ".........",
                ".........",
                "..B.B.B..",
                "...BBB...",
                "..BBBBB..",
                "...BBB...",
                "..B.B.B..",
                ".........",
                ".........");

            var lines = LineDetector.FindLinesThrough(board, new Cell(4, 4));

            Assert.AreEqual(4, lines.Count);
            Assert.AreEqual(17, LineDetector.CellsToClear(lines).Count);
        }

        [Test]
        public void EmptyCell_HasNoLines()
        {
            var board = TestBoards.FromRows(
                ".........",
                ".........",
                ".........",
                ".........",
                "RRRR.RRRR",
                ".........",
                ".........",
                ".........",
                ".........");

            Assert.IsEmpty(LineDetector.FindLinesThrough(board, new Cell(4, 4)));
        }

        [Test]
        public void SeveralCellsOnTheSameLine_LineListedOnce()
        {
            // Like 3 spawned balls that all finish the same row.
            var board = TestBoards.FromRows(
                ".........",
                ".........",
                ".........",
                ".........",
                "YYYYY....",
                ".........",
                ".........",
                ".........",
                ".........");

            var lines = LineDetector.FindLinesThrough(board, new[] { new Cell(0, 4), new Cell(2, 4), new Cell(4, 4) });

            Assert.AreEqual(1, lines.Count);
        }

        [Test]
        public void SeveralCells_SeparateLines_AllFound()
        {
            var board = TestBoards.FromRows(
                "GGGGG....",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                "....PPPPP");

            var lines = LineDetector.FindLinesThrough(board, new[] { new Cell(0, 0), new Cell(8, 8), new Cell(4, 4) });

            Assert.AreEqual(2, lines.Count);
            CollectionAssert.AreEquivalent(new[] { BallColor.Green, BallColor.Purple }, lines.Select(l => l.Color));
        }

        [Test]
        public void NullBoard_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => LineDetector.FindLinesThrough(null, new Cell(0, 0)));
        }
    }
}
