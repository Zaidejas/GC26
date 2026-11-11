using System.Linq;
using NUnit.Framework;

namespace GC26._2048.Tests
{
    public class CheeseBoardTests
    {
        static Board Row(params int[] row)
        {
            var board = new Board();
            var cells = new int[16];
            row.CopyTo(cells, 0);
            board.Load(cells);
            return board;
        }

        static int[] FirstRow(Board b) => Enumerable.Range(0, 4).Select(c => b.LevelAt(0, c)).ToArray();

        [Test]
        public void FourEqualMergeIntoTwo()
        {
            var b = Row(1, 1, 1, 1);
            var r = b.Move(Direction.Left);
            Assert.AreEqual(new[] { 2, 2, 0, 0 }, FirstRow(b));
            Assert.AreEqual(64, r.Points);
        }

        [Test]
        public void MergedItemDoesNotMergeAgainInSameMove()
        {
            var b = Row(1, 1, 2, 0);
            var r = b.Move(Direction.Left);
            Assert.AreEqual(new[] { 2, 2, 0, 0 }, FirstRow(b));
            Assert.AreEqual(32, r.Points);
        }

        [Test]
        public void MergeStartsFromTheMoveSide()
        {
            var b = Row(1, 1, 1, 0);
            b.Move(Direction.Right);
            Assert.AreEqual(new[] { 0, 0, 1, 2 }, FirstRow(b));
        }

        [Test]
        public void GapsCloseBeforeMerging()
        {
            var b = Row(3, 0, 3, 0);
            var r = b.Move(Direction.Left);
            Assert.AreEqual(new[] { 4, 0, 0, 0 }, FirstRow(b));
            Assert.AreEqual(128, r.Points);
        }

        [Test]
        public void TwoCottageCheeseBowlsMakeCheeseThatLeavesTheField()
        {
            var b = Row(7, 7, 1, 0);
            var r = b.Move(Direction.Left);
            Assert.AreEqual(new[] { 1, 0, 0, 0 }, FirstRow(b));
            Assert.AreEqual(2048, r.Points);
            Assert.AreEqual(1, r.Cheeses);
            Assert.IsTrue(r.Events.Any(e => e.Kind == TileEventKind.ToHole));
        }

        [Test]
        public void ItemsBeforeTheCheeseCannotMergeAcrossIt()
        {
            var b = Row(1, 7, 7, 1);
            b.Move(Direction.Left);
            Assert.AreEqual(new[] { 1, 1, 0, 0 }, FirstRow(b));
        }

        [TestCase(1, 32)]
        [TestCase(2, 64)]
        [TestCase(3, 128)]
        [TestCase(4, 256)]
        [TestCase(5, 512)]
        [TestCase(6, 1024)]
        [TestCase(7, 2048)]
        public void PointsForEachOfTheSevenMerges(int level, int points)
        {
            var r = Row(level, level, 0, 0).Move(Direction.Left);
            Assert.AreEqual(points, r.Points);
        }

        [Test]
        public void MoveThatChangesNothingIsNotCounted()
        {
            var b = Row(1, 2, 0, 0);
            var r = b.Move(Direction.Left);
            Assert.IsFalse(r.Changed);
            Assert.AreEqual(0, r.Events.Count);
        }

        [Test]
        public void FullBoardWithoutEqualNeighboursHasNoMoves()
        {
            var b = new Board();
            b.Load(new[] { 1, 2, 1, 2, 2, 1, 2, 1, 1, 2, 1, 2, 2, 1, 2, 1 });
            Assert.IsFalse(b.HasMoves());
        }

        [Test]
        public void FullBoardWithEqualNeighboursHasMoves()
        {
            var b = new Board();
            b.Load(new[] { 1, 1, 1, 2, 2, 1, 2, 1, 1, 2, 1, 2, 2, 1, 2, 1 });
            Assert.IsTrue(b.HasMoves());
        }

        [Test]
        public void SessionSpendsAMoveAndSpawnsOnlyOnChange()
        {
            var s = new CheeseSession(1);
            s.Restore(new[] { 1, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 0, 150, 0, false, 2);
            s.Move(Direction.Left);
            Assert.AreEqual(150, s.MovesLeft);
            s.Move(Direction.Right);
            Assert.AreEqual(149, s.MovesLeft);
            Assert.AreEqual(3, s.Board.ToArray().Count(l => l > 0));
        }

        [Test]
        public void SessionCountsCheeseAndFlagsTheFirstOne()
        {
            var s = new CheeseSession(1);
            s.Restore(new[] { 7, 7, 0, 0, 7, 7, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 0, 150, 0, false, 7);
            var r = s.Move(Direction.Left);
            Assert.AreEqual(2, s.Cheeses);
            Assert.AreEqual(4096, s.Score);
            Assert.IsTrue(r.FirstCheese);
            Assert.AreEqual(CheeseRules.CheeseLevel, s.BestLevel);
        }

        [Test]
        public void SessionEndsWhenMovesRunOut()
        {
            var s = new CheeseSession(1);
            s.Restore(new[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 0, 1, 0, false, 1);
            s.Move(Direction.Right);
            Assert.AreEqual(EndReason.OutOfMoves, s.Ended);
        }

        [Test]
        public void NewGameStartsWithTwoGrassAnd150Moves()
        {
            var s = new CheeseSession(7);
            s.StartNew();
            var cells = s.Board.ToArray();
            Assert.AreEqual(2, cells.Count(l => l == 1));
            Assert.AreEqual(14, cells.Count(l => l == 0));
            Assert.AreEqual(150, s.MovesLeft);
        }
    }
}
