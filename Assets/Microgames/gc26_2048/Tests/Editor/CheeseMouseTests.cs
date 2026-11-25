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

        /// <summary>A move that made one cheese (tile <paramref name="id"/> at row 3, col 3).</summary>
        static MoveResult Cheese(int id = 999)
        {
            var r = new MoveResult { Cheeses = 1 };
            r.Events.Add(TileEvent.Merged(id, id + 1, 3, 3, CheeseRules.CheeseLevel));
            return r;
        }

        [Test]
        public void HoleOpensFiveMovesAfterTheFirstCheese()
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
                Assert.IsFalse(r.HoleOpened, $"move {i}");
            }
            var arrival = new MoveResult();
            mouse.AfterMove(board, rng, arrival);
            Assert.IsTrue(arrival.HoleOpened);
            Assert.AreEqual((mouse.Row, mouse.Col), (arrival.HoleRow, arrival.HoleCol));
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
        public void HoleIsAlwaysOnAnOuterCell()
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
        public void RunsOutOfTheHoleEveryThirdMoveToTheOtherEndOfItsLine()
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
            Assert.AreEqual((3, 1), (run.MouseToRow, run.MouseToCol), "only its column goes edge to edge");
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
                var r = new MoveResult();
                mouse.AfterMove(Field(), new Random(seed), r);
                if ((r.MouseToRow, r.MouseToCol) == (0, 3)) row = true;
                else if ((r.MouseToRow, r.MouseToCol) == (3, 0)) column = true;
                else Assert.Fail($"ran to {r.MouseToRow},{r.MouseToCol}");
            }
            Assert.IsTrue(row && column, "both directions happen");
        }

        [Test]
        public void AfterARunTheHoleMovesToAnotherOuterCell()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var mouse = new CheeseMouse();
                mouse.Restore(MouseState.Here, 1, 0, 2);
                var r = new MoveResult();
                mouse.AfterMove(Field(), new Random(seed), r);
                Assert.IsTrue(r.MouseRan);
                Assert.AreNotEqual((0, 2), (mouse.Row, mouse.Col), $"seed {seed}");
                Assert.IsTrue(CheeseMouse.IsOuter(mouse.Row, mouse.Col, 4), $"seed {seed}");
                Assert.AreEqual((mouse.Row, mouse.Col), (r.HoleRow, r.HoleCol));
                Assert.AreEqual(MouseState.Here, mouse.State);
            }
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
        public void GettingCheeseClosesTheHoleUntilTheNextCheese()
        {
            var board = Field(0, 8, 0, 0);
            var rng = new Random(0);
            var mouse = new CheeseMouse();
            mouse.Restore(MouseState.Here, 1, 0, 0); // corner: the cheese at (0,1) is on the row run only
            var run = new MoveResult();
            for (int seed = 0; !run.MouseRan || run.Eaten.Count == 0; seed++)
            {
                board = Field(0, 8, 0, 0);
                mouse.Restore(MouseState.Here, 1, 0, 0);
                run = new MoveResult();
                mouse.AfterMove(board, new Random(seed), run);
                Assert.Less(seed, 50, "the row run never happened");
            }
            Assert.IsTrue(run.HoleClosed);
            Assert.AreEqual(MouseState.None, mouse.State);

            // moves without cheese: no hole
            for (int i = 0; i < 10; i++)
            {
                var r = new MoveResult();
                mouse.AfterMove(board, rng, r);
                Assert.IsFalse(r.HoleOpened || r.MouseRan, $"move {i}");
            }
            // the next cheese brings it back after the usual delay
            mouse.AfterMove(board, rng, Cheese());
            Assert.AreEqual(MouseState.Coming, mouse.State);
            for (int i = 1; i < CheeseRules.MouseDelayMoves; i++) mouse.AfterMove(board, rng, new MoveResult());
            var back = new MoveResult();
            mouse.AfterMove(board, rng, back);
            Assert.IsTrue(back.HoleOpened);
        }

        [Test]
        public void RunWithoutCheeseKeepsTheHoleOpen()
        {
            var mouse = new CheeseMouse();
            mouse.Restore(MouseState.Here, 1, 0, 1);
            var r = new MoveResult();
            mouse.AfterMove(Field(1, 2, 3, 4), new Random(0), r);
            Assert.IsTrue(r.MouseRan);
            Assert.IsFalse(r.HoleClosed);
            Assert.AreEqual(MouseState.Here, mouse.State);
        }

        [Test]
        public void CheeseEatenOnTheMoveItWasMadeDoesNotReopenTheHole()
        {
            // the move made cheese at (0,1) and the mouse ate it on the same move
            var board = Field(0, 8, 0, 0);
            int id = board.IdAt(0, 1);
            var mouse = new CheeseMouse();
            mouse.Restore(MouseState.Here, 1, 3, 1); // bottom edge: runs up column 1
            var r = new MoveResult { Cheeses = 1 };
            r.Events.Add(TileEvent.Merged(id, id + 100, 0, 1, CheeseRules.CheeseLevel));
            mouse.AfterMove(board, new Random(0), r);
            Assert.IsTrue(r.HoleClosed);
            Assert.AreEqual(MouseState.None, mouse.State);
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
