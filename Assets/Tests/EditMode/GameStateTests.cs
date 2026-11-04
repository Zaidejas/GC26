using System;
using System.Collections.Generic;
using System.Linq;
using Lines.Core;
using NUnit.Framework;

namespace Lines.Tests
{
    public class GameStateTests
    {
        // ---- New game ----

        [Test]
        public void NewGame_ThreeBallsPreviewNoScore()
        {
            var game = GameState.NewGame(1);

            Assert.AreEqual(78, game.Board.EmptyCount);
            Assert.AreEqual(3, game.Preview.Colors.Count);
            Assert.AreEqual(3, game.Preview.Cells.Count);
            Assert.AreEqual(0, game.Score);
            Assert.IsFalse(game.IsOver);
        }

        [Test]
        public void NewGame_SameSeed_SameGame()
        {
            var a = GameState.NewGame(42);
            var b = GameState.NewGame(42);
            CollectionAssert.AreEqual(a.Board.EmptyCells(), b.Board.EmptyCells());
            CollectionAssert.AreEqual(a.Preview.Cells, b.Preview.Cells);
            CollectionAssert.AreEqual(a.Preview.Colors, b.Preview.Colors);
        }

        // ---- Blocked move ----

        [Test]
        public void BlockedMove_NothingChanges()
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
            var preview = Preview(Cells((8, 8), (7, 8), (6, 8)));
            var game = new GameState(board, preview, 5, new Spawner(1));
            var blocked = new List<(Cell, Cell)>();
            game.OnMoveBlocked += (f, t) => blocked.Add((f, t));

            var result = game.TryMove(new Cell(4, 4), new Cell(0, 0));

            Assert.IsFalse(result.Moved);
            Assert.AreEqual(1, blocked.Count);
            Assert.AreEqual(BallColor.Red, game.Board.Get(new Cell(4, 4)));
            Assert.AreEqual(76, game.Board.EmptyCount, "No spawn");
            Assert.AreSame(preview, game.Preview);
            Assert.AreEqual(5, game.Score);
        }

        // ---- Move without a line: spawn ----

        [Test]
        public void MoveWithoutLine_SpawnsPreviewBalls_AndChoosesNewPreview()
        {
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
            var preview = Preview(Cells((8, 8), (7, 8), (6, 8)), BallColor.Green, BallColor.Blue, BallColor.Purple);
            var game = new GameState(board, preview, 0, new Spawner(2));

            var result = game.TryMove(new Cell(0, 0), new Cell(0, 1));

            Assert.IsTrue(result.Moved);
            CollectionAssert.AreEqual(new[] { new Cell(0, 0), new Cell(0, 1) }, result.Path);
            CollectionAssert.AreEqual(preview.Cells, result.Spawned);
            Assert.AreEqual(BallColor.Green, game.Board.Get(new Cell(8, 8)));
            Assert.AreEqual(BallColor.Blue, game.Board.Get(new Cell(7, 8)));
            Assert.AreEqual(BallColor.Purple, game.Board.Get(new Cell(6, 8)));
            Assert.AreEqual(77, game.Board.EmptyCount);
            Assert.AreNotSame(preview, game.Preview, "A new preview after a spawn");
            Assert.IsTrue(game.Preview.Cells.All(c => game.Board.IsEmpty(c)));
            Assert.AreEqual(0, game.Score);
        }

        [Test]
        public void MoveOntoPreviewCell_ThatBallAppearsElsewhere()
        {
            var board = TestBoards.FromRows(
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                "R........");
            var preview = Preview(Cells((1, 8), (8, 0), (8, 1)));
            var game = new GameState(board, preview, 0, new Spawner(3));

            var result = game.TryMove(new Cell(0, 8), new Cell(1, 8));

            Assert.AreEqual(3, result.Spawned.Count);
            CollectionAssert.DoesNotContain(result.Spawned, new Cell(1, 8));
            Assert.AreEqual(BallColor.Red, game.Board.Get(new Cell(1, 8)), "The player's ball stays");
            Assert.AreEqual(77, game.Board.EmptyCount);
        }

        // ---- Move completes a line ----

        [Test]
        public void MoveCompletesLine_ClearsAndScores_NoSpawn_PreviewKept()
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
            var preview = Preview(Cells((8, 0), (7, 0), (6, 0)));
            var game = new GameState(board, preview, 0, new Spawner(4));
            var cleared = new List<(int points, bool byMove)>();
            game.OnLinesCleared += (lines, cells, points, byMove) => cleared.Add((points, byMove));

            var result = game.TryMove(new Cell(4, 8), new Cell(4, 4));

            Assert.AreEqual(1, result.LinesFromMove.Count);
            Assert.AreEqual(10, result.MovePoints);
            Assert.AreEqual(10, game.Score);
            Assert.IsEmpty(result.Spawned);
            Assert.AreEqual(81, game.Board.EmptyCount, "Line removed, nothing spawned");
            Assert.AreSame(preview, game.Preview, "Preview stays for the next turn");
            CollectionAssert.AreEqual(new[] { (10, true) }, cleared);
        }

        [Test]
        public void MoveCompletesTwoCrossingLines_GddExample()
        {
            // Row 4 and the main diagonal both wait for (4,4); the shared ball counts in both lines.
            var board = TestBoards.FromRows(
                "R........",
                ".R.......",
                "..R......",
                "...R.....",
                "RRRR.....",
                ".........",
                ".........",
                ".........",
                "........R");
            var game = new GameState(board, Preview(Cells((8, 0), (7, 0), (6, 0))), 0, new Spawner(5));

            var result = game.TryMove(new Cell(8, 8), new Cell(4, 4));

            Assert.AreEqual(2, result.LinesFromMove.Count);
            Assert.AreEqual(40, game.Score, "(10 + 10) × 2");
            Assert.AreEqual(81, game.Board.EmptyCount, "All 9 balls removed");
        }

        // ---- Spawned balls complete a line ----

        [Test]
        public void SpawnCompletesLine_ScoresWithoutMultiplier()
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
            var preview = Preview(Cells((4, 0), (8, 8), (7, 8)), BallColor.Green, BallColor.Blue, BallColor.Purple);
            var game = new GameState(board, preview, 0, new Spawner(6));
            var cleared = new List<bool>();
            game.OnLinesCleared += (lines, cells, points, byMove) => cleared.Add(byMove);

            var result = game.TryMove(new Cell(0, 8), new Cell(1, 8));

            Assert.AreEqual(3, result.Spawned.Count);
            Assert.AreEqual(1, result.LinesFromSpawn.Count);
            Assert.AreEqual(10, result.SpawnPoints);
            Assert.AreEqual(10, game.Score);
            Assert.IsTrue(game.Board.IsEmpty(new Cell(0, 0)), "The green line is gone");
            CollectionAssert.AreEqual(new[] { false }, cleared);
            Assert.IsFalse(game.IsOver);
        }

        // ---- Game over ----

        [Test]
        public void SpawnFillsBoard_GameOver()
        {
            var board = FullBoardWithoutLines(new Cell(6, 8), new Cell(7, 8), new Cell(8, 8));
            var preview = Preview(Cells((5, 8), (7, 8), (8, 8)), BallColor.Red, BallColor.Blue, BallColor.Purple);
            var game = new GameState(board, preview, 120, new Spawner(7));
            int? finalScore = null;
            game.OnGameOver += s => finalScore = s;

            // Moving (5,8) → (6,8) frees (5,8); the 3 spawned balls then fill the board.
            var result = game.TryMove(new Cell(5, 8), new Cell(6, 8));

            Assert.IsTrue(result.GameOver);
            Assert.IsTrue(game.IsOver);
            Assert.IsTrue(game.Board.IsFull);
            Assert.AreEqual(120, finalScore);
            Assert.Throws<InvalidOperationException>(() => game.TryMove(new Cell(0, 0), new Cell(1, 0)));
        }

        // ---- Events and invalid calls ----

        [Test]
        public void Events_FireInTurnOrder()
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
            var preview = Preview(Cells((4, 0), (8, 8), (7, 8)), BallColor.Green, BallColor.Blue, BallColor.Purple);
            var game = new GameState(board, preview, 0, new Spawner(8));
            var log = new List<string>();
            game.OnBallMoved += (f, t, p) => log.Add("moved");
            game.OnBallsSpawned += c => log.Add("spawned");
            game.OnLinesCleared += (l, c, p, m) => log.Add("cleared");
            game.OnGameOver += s => log.Add("over");

            game.TryMove(new Cell(0, 8), new Cell(1, 8));

            CollectionAssert.AreEqual(new[] { "moved", "spawned", "cleared" }, log);
        }

        [Test]
        public void InvalidMoves_Throw()
        {
            var board = TestBoards.FromRows(
                "RB.......",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........");
            var game = new GameState(board, Preview(Cells((8, 8), (7, 8), (6, 8))), 0, new Spawner(9));

            Assert.Throws<ArgumentException>(() => game.TryMove(new Cell(5, 5), new Cell(6, 6)), "No ball to move");
            Assert.Throws<ArgumentException>(() => game.TryMove(new Cell(0, 0), new Cell(1, 0)), "Target occupied");
        }

        // ---- Whole games, no graphics (the task's "done when") ----

        [Test]
        public void FullGames_PlayToGameOver_RulesHoldEveryTurn()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var (turns, score) = PlayWholeGame(seed);
                Assert.Greater(turns, 0, $"seed {seed}");
                Assert.GreaterOrEqual(score, 0);
            }
        }

        [Test]
        public void FullGame_SameSeedSameChoices_SameResult()
        {
            Assert.AreEqual(PlayWholeGame(77), PlayWholeGame(77));
        }

        /// <summary>
        /// Plays random legal moves until the board is full, checking the rules after every turn.
        /// Returns the number of turns and the final score.
        /// </summary>
        static (int turns, int score) PlayWholeGame(int seed)
        {
            var game = GameState.NewGame(seed);
            var chooser = new GameRandom(seed * 31 + 7);
            int turns = 0;
            while (!game.IsOver)
            {
                Assert.Less(turns, 2000, "The game should end");
                var (from, to) = PickLegalMove(game.Board, chooser);
                int scoreBefore = game.Score;
                int ballsBefore = 81 - game.Board.EmptyCount;

                var result = game.TryMove(from, to);
                turns++;

                Assert.IsTrue(result.Moved);
                Assert.GreaterOrEqual(game.Score, scoreBefore, "Score never goes down");
                Assert.AreEqual(scoreBefore + result.MovePoints + result.SpawnPoints, game.Score);
                int removed = LineDetector.CellsToClear(result.LinesFromMove.Concat(result.LinesFromSpawn)).Count;
                Assert.AreEqual(ballsBefore + result.Spawned.Count - removed, 81 - game.Board.EmptyCount);
                if (result.LinesFromMove.Count > 0) Assert.IsEmpty(result.Spawned, "A cleared line skips the spawn");

                if (!game.IsOver)
                {
                    CollectionAssert.AllItemsAreUnique(game.Preview.Cells);
                    Assert.IsTrue(game.Preview.Cells.All(c => game.Board.IsEmpty(c)), "Preview cells are free");
                }
                else
                {
                    Assert.IsTrue(game.Board.IsFull);
                }
            }
            return (turns, game.Score);
        }

        static (Cell from, Cell to) PickLegalMove(Board board, GameRandom random)
        {
            var balls = AllCells().Where(c => !board.IsEmpty(c)).ToList();
            var empty = board.EmptyCells();
            Shuffle(balls, random);
            Shuffle(empty, random);
            foreach (var ball in balls)
            foreach (var target in empty)
                if (Pathfinder.HasPath(board, ball, target))
                    return (ball, target);
            throw new AssertionException("No legal move although the board isn't full");
        }

        // ---- Helpers ----

        static IEnumerable<Cell> AllCells() =>
            Enumerable.Range(0, 81).Select(i => new Cell(i % 9, i / 9));

        static void Shuffle<T>(List<T> list, GameRandom random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        static Cell[] Cells(params (int column, int row)[] cells) =>
            cells.Select(c => new Cell(c.column, c.row)).ToArray();

        static Preview Preview(Cell[] cells, params BallColor[] colors) =>
            new Preview(colors.Length > 0 ? colors : new[] { BallColor.Yellow, BallColor.Cyan, BallColor.Orange }, cells);

        /// <summary>
        /// A full board where no two neighbours share a color (color = column + 2 × row), so it has no lines.
        /// </summary>
        static Board FullBoardWithoutLines(params Cell[] free)
        {
            var board = new Board();
            foreach (var cell in AllCells())
                if (!free.Contains(cell))
                    board.Place(cell, BallColors.All[(cell.Column + 2 * cell.Row) % 7]);
            return board;
        }
    }
}
