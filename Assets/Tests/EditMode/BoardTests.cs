using System;
using Lines.Core;
using NUnit.Framework;

namespace Lines.Tests
{
    public class BoardTests
    {
        static readonly Cell A = new Cell(0, 0);
        static readonly Cell B = new Cell(4, 4);
        static readonly Cell C = new Cell(8, 8);

        [Test]
        public void NewBoard_Is9x9AndEmpty()
        {
            var board = new Board();
            Assert.AreEqual(9, board.Size);
            Assert.AreEqual(81, board.CellCount);
            Assert.AreEqual(81, board.EmptyCount);
            Assert.IsFalse(board.IsFull);
            Assert.AreEqual(BallColor.None, board.Get(B));
        }

        [Test]
        public void BallColors_HasSevenColorsWithoutNone()
        {
            Assert.AreEqual(GameConfig.ColorCount, BallColors.All.Length);
            CollectionAssert.DoesNotContain(BallColors.All, BallColor.None);
            CollectionAssert.AllItemsAreUnique(BallColors.All);
        }

        [Test]
        public void Place_PutsBallInCell()
        {
            var board = new Board();
            board.Place(B, BallColor.Green);
            Assert.AreEqual(BallColor.Green, board.Get(B));
            Assert.IsFalse(board.IsEmpty(B));
            Assert.AreEqual(80, board.EmptyCount);
        }

        [Test]
        public void Place_OnOccupiedCell_Throws()
        {
            var board = new Board();
            board.Place(B, BallColor.Green);
            Assert.Throws<InvalidOperationException>(() => board.Place(B, BallColor.Red));
            Assert.AreEqual(BallColor.Green, board.Get(B));
        }

        [Test]
        public void Place_EmptyColor_Throws()
        {
            var board = new Board();
            Assert.Throws<ArgumentException>(() => board.Place(B, BallColor.None));
        }

        [Test]
        public void Remove_EmptiesCellAndReturnsColor()
        {
            var board = new Board();
            board.Place(B, BallColor.Blue);
            Assert.AreEqual(BallColor.Blue, board.Remove(B));
            Assert.IsTrue(board.IsEmpty(B));
            Assert.AreEqual(81, board.EmptyCount);
        }

        [Test]
        public void Remove_FromEmptyCell_Throws()
        {
            var board = new Board();
            Assert.Throws<InvalidOperationException>(() => board.Remove(B));
        }

        [Test]
        public void Move_TransfersBall()
        {
            var board = new Board();
            board.Place(A, BallColor.Red);
            board.Move(A, C);
            Assert.IsTrue(board.IsEmpty(A));
            Assert.AreEqual(BallColor.Red, board.Get(C));
            Assert.AreEqual(80, board.EmptyCount);
        }

        [Test]
        public void Move_OntoOccupiedCell_ThrowsAndChangesNothing()
        {
            var board = new Board();
            board.Place(A, BallColor.Red);
            board.Place(C, BallColor.Blue);
            Assert.Throws<InvalidOperationException>(() => board.Move(A, C));
            Assert.AreEqual(BallColor.Red, board.Get(A));
            Assert.AreEqual(BallColor.Blue, board.Get(C));
        }

        [Test]
        public void Move_FromEmptyCell_Throws()
        {
            var board = new Board();
            Assert.Throws<InvalidOperationException>(() => board.Move(A, C));
        }

        [Test]
        public void Move_OntoItself_Throws()
        {
            var board = new Board();
            board.Place(A, BallColor.Red);
            Assert.Throws<ArgumentException>(() => board.Move(A, A));
        }

        [Test]
        public void OutsideCells_AreRejected()
        {
            var board = new Board();
            Assert.IsFalse(board.IsInside(new Cell(-1, 0)));
            Assert.IsFalse(board.IsInside(new Cell(0, 9)));
            Assert.IsTrue(board.IsInside(C));
            Assert.Throws<ArgumentOutOfRangeException>(() => board.Get(new Cell(9, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => board.Place(new Cell(0, -1), BallColor.Red));
        }

        [Test]
        public void EmptyCells_ListsFreeCellsRowByRow()
        {
            var board = new Board();
            board.Place(new Cell(1, 0), BallColor.Red);
            var empty = board.EmptyCells();
            Assert.AreEqual(80, empty.Count);
            Assert.AreEqual(new Cell(0, 0), empty[0]);
            Assert.AreEqual(new Cell(2, 0), empty[1]);
            CollectionAssert.DoesNotContain(empty, new Cell(1, 0));
            Assert.AreEqual(new Cell(8, 8), empty[empty.Count - 1]);
        }

        [Test]
        public void FilledBoard_IsFull()
        {
            var board = new Board();
            foreach (var cell in board.EmptyCells())
                board.Place(cell, BallColor.Yellow);
            Assert.IsTrue(board.IsFull);
            Assert.AreEqual(0, board.EmptyCount);
            Assert.IsEmpty(board.EmptyCells());
        }

        [Test]
        public void Clear_EmptiesTheBoard()
        {
            var board = new Board();
            board.Place(A, BallColor.Red);
            board.Place(C, BallColor.Cyan);
            board.Clear();
            Assert.AreEqual(81, board.EmptyCount);
        }

        [Test]
        public void Clone_IsIndependentCopy()
        {
            var board = new Board();
            board.Place(A, BallColor.Purple);
            var copy = board.Clone();

            Assert.AreEqual(BallColor.Purple, copy.Get(A));
            copy.Remove(A);
            copy.Place(C, BallColor.Orange);

            Assert.AreEqual(BallColor.Purple, board.Get(A));
            Assert.IsTrue(board.IsEmpty(C));
        }

        [Test]
        public void Cell_Equality()
        {
            Assert.AreEqual(new Cell(3, 5), new Cell(3, 5));
            Assert.AreNotEqual(new Cell(3, 5), new Cell(5, 3));
            Assert.IsTrue(new Cell(2, 2) == new Cell(2, 2));
            Assert.IsTrue(new Cell(2, 2) != new Cell(2, 3));
        }
    }
}
