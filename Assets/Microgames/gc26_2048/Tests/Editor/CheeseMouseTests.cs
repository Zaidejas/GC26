using System;
using System.Linq;
using NUnit.Framework;

namespace GC26._2048.Tests
{
    public class CheeseMouseTests
    {
        static Board Field(params int[] cells)
        {
            var board = new Board();
            var all = new int[16];
            cells.CopyTo(all, 0);
            board.Load(all);
            return board;
        }

        static MoveResult Cheese() => new MoveResult { Cheeses = 1 };

        [Test]
        public void ComesFiveMovesAfterTheFirstCheese()
        {
            var board = Field();
            var rng = new Random(3);
            var mouse = new CheeseMouse();
            mouse.AfterMove(board, rng, Cheese());
            Assert.AreEqual(MouseState.Coming, mouse.State);
            for (int i = 1; i < CheeseRules.MouseDelayMoves; i++)
            {
                var r = new MoveResult();
                mouse.AfterMove(board, rng, r);
                Assert.IsFalse(r.MouseArrived, $"move {i}");
            }
            var arrival = new MoveResult();
            mouse.AfterMove(board, rng, arrival);
            Assert.IsTrue(arrival.MouseArrived);
            Assert.AreEqual(MouseState.Here, mouse.State);
            Assert.IsTrue(CheeseMouse.IsOuter(mouse.Row, mouse.Col, 4));
        }

        [Test]
        public void NoCheeseNoMouse()
        {
            var mouse = new CheeseMouse();
            for (int i = 0; i < 20; i++) mouse.AfterMove(Field(), new Random(i), new MoveResult());
            Assert.AreEqual(MouseState.None, mouse.State);
        }

        [Test]
        public void AlwaysArrivesOnAnOuterCell()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var mouse = new CheeseMouse();
                mouse.Restore(MouseState.Coming, 1, 0, 0);
                mouse.AfterMove(Field(), new Random(seed), new MoveResult());
                Assert.IsTrue(CheeseMouse.IsOuter(mouse.Row, mouse.Col, 4), $"seed {seed}: {mouse.Row},{mouse.Col}");
            }
        }

        [Test]
        public void RunsEveryThirdMoveToTheOtherEndOfItsLine()
        {
            var mouse = new CheeseMouse();
            mouse.Restore(MouseState.Here, CheeseRules.MouseRunEvery, 0, 1); // top edge, not a corner
            var board = Field();
            var rng = new Random(1);
            for (int i = 1; i < CheeseRules.MouseRunEvery; i++)
            {
                var r = new MoveResult();
                mouse.AfterMove(board, rng, r);
                Assert.IsFalse(r.MouseRan, $"move {i}");
            }
            var run = new MoveResult();
            mouse.AfterMove(board, rng, run);
            Assert.IsTrue(run.MouseRan);
            Assert.AreEqual((0, 1), (run.MouseFromRow, run.MouseFromCol));
            Assert.AreEqual((3, 1), (mouse.Row, mouse.Col), "only its column goes edge to edge");
            Assert.AreEqual(CheeseRules.MouseRunEvery, mouse.Countdown);
        }

        [Test]
        public void FromACornerItPicksRowOrColumnAtRandom()
        {
            bool row = false, column = false;
            for (int seed = 0; seed < 40; seed++)
            {
                var mouse = new CheeseMouse();
                mouse.Restore(MouseState.Here, 1, 0, 0);
                mouse.AfterMove(Field(), new Random(seed), new MoveResult());
                if ((mouse.Row, mouse.Col) == (0, 3)) row = true;
                else if ((mouse.Row, mouse.Col) == (3, 0)) column = true;
                else Assert.Fail($"ran to {mouse.Row},{mouse.Col}");
            }
            Assert.IsTrue(row && column, "both directions happen");
        }

        [Test]
        public void EatsEveryCheeseOnItsWayAndNothingElse()
        {
            // column 1: cheese, grass, cheese, cow; a cheese elsewhere stays
            var board = Field(0, 8, 0, 8,
                              0, 1, 0, 0,
                              0, 8, 0, 0,
                              0, 3, 0, 0);
            int top = board.IdAt(0, 1), middle = board.IdAt(2, 1);
            var mouse = new CheeseMouse();
            mouse.Restore(MouseState.Here, 1, 0, 1);
            var r = new MoveResult();
            mouse.AfterMove(board, new Random(0), r);
            CollectionAssert.AreEqual(new[] { top, middle }, r.Eaten.Select(e => e.Id).ToArray());
            Assert.AreEqual(0, board.LevelAt(0, 1));
            Assert.AreEqual(1, board.LevelAt(1, 1));
            Assert.AreEqual(0, board.LevelAt(2, 1));
            Assert.AreEqual(3, board.LevelAt(3, 1));
            Assert.AreEqual(8, board.LevelAt(0, 3), "not on its way");
        }

        [Test]
        public void SecondCheeseDoesNotBringASecondMouse()
        {
            var mouse = new CheeseMouse();
            mouse.Restore(MouseState.Here, 3, 3, 3);
            mouse.AfterMove(Field(), new Random(0), Cheese());
            Assert.AreEqual(MouseState.Here, mouse.State);
            Assert.AreEqual(2, mouse.Countdown);
        }

        [Test]
        public void BrokenSaveMeansNoMouseYet()
        {
            var mouse = new CheeseMouse();
            mouse.Restore(MouseState.Here, 2, 1, 1); // inner cell
            Assert.AreEqual(MouseState.None, mouse.State);
            mouse.Restore(MouseState.Coming, 0, 0, 0);
            Assert.AreEqual(MouseState.None, mouse.State);
        }

        [Test]
        public void MouseWaitsWhenAMoveChangesNothing()
        {
            var s = new CheeseSession(5);
            s.Restore(new[] { 8, 8, 8, 8, 1, 2, 1, 2, 2, 1, 2, 1, 1, 2, 1, 3 }, 0, 100, 4, true, 8);
            s.Mouse.Restore(MouseState.Here, 1, 0, 0);
            var r = s.Move(Direction.Left);
            Assert.IsFalse(r.Changed);
            Assert.IsFalse(r.MouseRan);
            Assert.AreEqual(1, s.Mouse.Countdown);
        }

        [Test]
        public void RunInsideAMoveEatsCheeseBeforeTheEndCheck()
        {
            var s = new CheeseSession(5);
            s.Restore(new[] { 8, 8, 8, 8, 1, 2, 1, 2, 2, 3, 2, 1, 1, 1, 2, 3 }, 0, 100, 4, true, 8);
            s.Mouse.Restore(MouseState.Here, 1, 0, 0);
            var r = s.Move(Direction.Left);
            Assert.IsTrue(r.Changed);
            Assert.IsTrue(r.MouseRan);
            Assert.IsTrue(r.Eaten.Any(e => e.Row == 0 && e.Col == 0));
            Assert.AreEqual(0, s.Board.LevelAt(0, 0));
            Assert.AreEqual(EndReason.None, s.Ended);
        }
    }
}
