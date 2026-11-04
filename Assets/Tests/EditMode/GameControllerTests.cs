using System;
using System.Collections.Generic;
using System.Linq;
using Lines.Core;
using Lines.Game;
using NUnit.Framework;

namespace Lines.Tests
{
    public class GameControllerTests
    {
        /// <summary>A screen whose animations only finish when the test calls <see cref="FinishAnimation"/>.</summary>
        sealed class FakePresenter : IBoardPresenter
        {
            public readonly List<string> Log = new List<string>();
            readonly Queue<Action> running = new Queue<Action>();
            public bool Instant;
            public Cell? Selected;

            public int Running => running.Count;
            public void FinishAnimation() => running.Dequeue()();

            void Play(string what, Action done)
            {
                Log.Add(what);
                if (Instant) done(); else running.Enqueue(done);
            }

            public void ShowState(GameState game) => Log.Add("state");
            public void ShowSelected(Cell? cell) { Selected = cell; Log.Add(cell.HasValue ? "select " + cell : "deselect"); }
            public void ShowBlocked(Cell from, Cell to) => Log.Add("blocked");
            public void PlayMove(IReadOnlyList<Cell> path, Action done) => Play("move", done);
            public void PlayClear(IReadOnlyList<Cell> cells, int points, Action done) => Play($"clear {cells.Count} +{points}", done);
            public void PlaySpawn(IReadOnlyList<Cell> cells, Action done) => Play("spawn " + cells.Count, done);
            public void ShowGameOver(int score) => Log.Add("game over " + score);
        }

        static Cell C(int column, int row) => new Cell(column, row);

        static (GameController controller, FakePresenter screen) Start(Board board, Preview preview)
        {
            var screen = new FakePresenter();
            var controller = new GameController(new GameState(board, preview, 0, new Spawner(1)), screen);
            screen.Log.Clear();
            return (controller, screen);
        }

        static Preview PreviewAt(params Cell[] cells) =>
            new Preview(new[] { BallColor.Yellow, BallColor.Cyan, BallColor.Orange }, cells);

        static Board OneBall() => TestBoards.FromRows(
            "R........",
            ".........",
            ".........",
            ".........",
            ".........",
            ".........",
            ".........",
            ".........",
            "B........");

        // ---- Selecting ----

        [Test]
        public void StartsIdle_AndAcceptsInput()
        {
            var (controller, _) = Start(OneBall(), PreviewAt(C(8, 8), C(7, 8), C(6, 8)));
            Assert.AreEqual(TurnState.Idle, controller.State);
            Assert.IsTrue(controller.AcceptsInput);
        }

        [Test]
        public void TapBall_SelectsIt()
        {
            var (controller, screen) = Start(OneBall(), PreviewAt(C(8, 8), C(7, 8), C(6, 8)));

            controller.Tap(C(0, 0));

            Assert.AreEqual(TurnState.BallSelected, controller.State);
            Assert.AreEqual(C(0, 0), controller.Selected);
            Assert.AreEqual(C(0, 0), screen.Selected);
        }

        [Test]
        public void TapAnotherBall_SwitchesSelection()
        {
            var (controller, _) = Start(OneBall(), PreviewAt(C(8, 8), C(7, 8), C(6, 8)));
            controller.Tap(C(0, 0));
            controller.Tap(C(0, 8));
            Assert.AreEqual(C(0, 8), controller.Selected);
        }

        [Test]
        public void TapSelectedBallAgain_NothingHappens()
        {
            var (controller, screen) = Start(OneBall(), PreviewAt(C(8, 8), C(7, 8), C(6, 8)));
            controller.Tap(C(0, 0));
            screen.Log.Clear();

            controller.Tap(C(0, 0));

            Assert.AreEqual(C(0, 0), controller.Selected, "Stays selected");
            Assert.IsEmpty(screen.Log);
        }

        [Test]
        public void TapEmptyCell_WithoutSelection_NothingHappens()
        {
            var (controller, screen) = Start(OneBall(), PreviewAt(C(8, 8), C(7, 8), C(6, 8)));
            controller.Tap(C(4, 4));
            Assert.AreEqual(TurnState.Idle, controller.State);
            Assert.IsEmpty(screen.Log);
            Assert.AreEqual(79, controller.Game.Board.EmptyCount);
        }

        // ---- Blocked ----

        [Test]
        public void BlockedTarget_ShowsBlocked_BallStaysSelected_NoLock()
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
            var (controller, screen) = Start(board, PreviewAt(C(8, 8), C(7, 8), C(6, 8)));
            controller.Tap(C(4, 4));
            screen.Log.Clear();

            controller.Tap(C(0, 0));

            CollectionAssert.AreEqual(new[] { "blocked" }, screen.Log);
            Assert.AreEqual(TurnState.BallSelected, controller.State);
            Assert.AreEqual(C(4, 4), controller.Selected);
            Assert.IsTrue(controller.AcceptsInput);
        }

        // ---- The input lock (task 12's "done when") ----

        [Test]
        public void TapsDuringAnimations_AreIgnored()
        {
            var (controller, screen) = Start(OneBall(), PreviewAt(C(8, 8), C(7, 8), C(6, 8)));
            controller.Tap(C(0, 0));
            controller.Tap(C(0, 4));
            // The rules apply the whole turn at once (move + spawn); only the screen catches up step by step.
            var boardAfterTurn = controller.Game.Board.Clone();

            // Moving: every tap is ignored and nothing changes.
            Assert.AreEqual(TurnState.Moving, controller.State);
            Assert.IsFalse(controller.AcceptsInput);
            Assert.IsFalse(controller.Tap(C(0, 8)), "Tap on a ball ignored");
            Assert.IsFalse(controller.Tap(C(5, 5)), "Tap on an empty cell ignored");
            Assert.IsNull(controller.Selected);

            // Spawning: still locked.
            screen.FinishAnimation();
            Assert.AreEqual(TurnState.Spawning, controller.State);
            Assert.IsFalse(controller.Tap(C(0, 8)));

            // Done: input works again.
            screen.FinishAnimation();
            Assert.AreEqual(TurnState.Idle, controller.State);
            Assert.IsTrue(controller.Tap(C(0, 8)));
            Assert.AreEqual(TurnState.BallSelected, controller.State);

            // The ignored taps didn't move anything on the board.
            Assert.AreEqual(BallColor.Red, controller.Game.Board.Get(C(0, 4)));
            Assert.AreEqual(BallColor.Blue, controller.Game.Board.Get(C(0, 8)));
            CollectionAssert.AreEqual(boardAfterTurn.EmptyCells(), controller.Game.Board.EmptyCells(), "Ignored taps changed nothing");
        }

        // ---- Turn order ----

        [Test]
        public void MoveWithoutLine_MoveThenSpawnThenRedraw()
        {
            var (controller, screen) = Start(OneBall(), PreviewAt(C(8, 8), C(7, 8), C(6, 8)));
            screen.Instant = true;
            controller.Tap(C(0, 0));
            screen.Log.Clear();

            controller.Tap(C(0, 1));

            CollectionAssert.AreEqual(new[] { "deselect", "move", "spawn 3", "state" }, screen.Log);
            Assert.AreEqual(TurnState.Idle, controller.State);
        }

        [Test]
        public void MoveCompletesLine_ClearNoSpawn()
        {
            var board = TestBoards.FromRows(
                ".........",
                ".........",
                ".........",
                ".........",
                "RRRR.....",
                ".........",
                ".........",
                ".........",
                "....R....");
            var (controller, screen) = Start(board, PreviewAt(C(8, 0), C(7, 0), C(6, 0)));
            controller.Tap(C(4, 8));
            screen.Log.Clear();

            controller.Tap(C(4, 4));
            Assert.AreEqual(TurnState.Moving, controller.State);
            screen.FinishAnimation();
            Assert.AreEqual(TurnState.Resolving, controller.State);
            screen.FinishAnimation();

            CollectionAssert.AreEqual(new[] { "deselect", "move", "clear 5 +10", "state" }, screen.Log);
            Assert.AreEqual(TurnState.Idle, controller.State);
            Assert.AreEqual(10, controller.Game.Score);
        }

        [Test]
        public void SpawnCompletesLine_SpawnThenClear()
        {
            var board = TestBoards.FromRows(
                "GGGG.....",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                "R........");
            var preview = new Preview(new[] { BallColor.Green, BallColor.Blue, BallColor.Purple }, new[] { C(4, 0), C(8, 8), C(7, 8) });
            var (controller, screen) = Start(board, preview);
            screen.Instant = true;
            controller.Tap(C(0, 8));
            screen.Log.Clear();

            controller.Tap(C(1, 8));

            CollectionAssert.AreEqual(new[] { "deselect", "move", "spawn 3", "clear 5 +10", "state" }, screen.Log);
        }

        // ---- Game over ----

        [Test]
        public void LastSpawnFillsBoard_GameOver_InputStaysLocked()
        {
            var board = new Board();
            foreach (var cell in board.EmptyCells())
                board.Place(cell, BallColors.All[(cell.Column + 2 * cell.Row) % 7]); // no lines anywhere
            board.Remove(C(6, 8)); board.Remove(C(7, 8)); board.Remove(C(8, 8));
            var preview = new Preview(new[] { BallColor.Red, BallColor.Blue, BallColor.Purple }, new[] { C(5, 8), C(7, 8), C(8, 8) });
            var (controller, screen) = Start(board, preview);
            screen.Instant = true;

            controller.Tap(C(5, 8));
            controller.Tap(C(6, 8));

            Assert.AreEqual(TurnState.GameOver, controller.State);
            Assert.AreEqual("game over 0", screen.Log.Last());
            Assert.IsFalse(controller.AcceptsInput);
            Assert.IsFalse(controller.Tap(C(0, 0)));
        }

        // ---- Whole games through the controller ----

        [Test]
        public void WholeGames_ThroughTaps_EndInGameOver()
        {
            for (int seed = 1; seed <= 10; seed++)
            {
                var screen = new FakePresenter { Instant = true };
                var controller = new GameController(GameState.NewGame(seed), screen);
                var random = new GameRandom(seed + 1000);
                int turns = 0;
                while (controller.State != TurnState.GameOver)
                {
                    Assert.Less(turns++, 2000);
                    var board = controller.Game.Board;
                    var balls = Enumerable.Range(0, 81).Select(i => C(i % 9, i / 9)).Where(c => !board.IsEmpty(c)).ToList();
                    var from = balls[random.Next(balls.Count)];
                    var empty = board.EmptyCells();
                    var to = empty[random.Next(empty.Count)];

                    controller.Tap(from);
                    controller.Tap(to); // may be blocked; then the next loop picks again
                    Assert.IsTrue(controller.State == TurnState.Idle || controller.State == TurnState.BallSelected
                                  || controller.State == TurnState.GameOver, $"Unexpected {controller.State}");
                }
                Assert.IsTrue(controller.Game.IsOver);
            }
        }
    }
}
