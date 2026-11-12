using System.Collections.Generic;
using System.Linq;
using GC26.Lines.Core;
using GC26.Lines.Game;
using GC26.Lines.UI;
using GC26.Lines.View;
using NUnit.Framework;
using UnityEngine;

namespace GC26.Lines.Tests
{
    /// <summary>
    /// GDD → Edge cases, played through the real screen (board view, HUD, animations), checking after
    /// every turn that what's drawn matches the rules exactly.
    /// </summary>
    public class EdgeCaseTests
    {
        BoardView view;
        HudView hud;
        Camera camera;
        LinesGameController controller;

        static Cell C(int column, int row) => new Cell(column, row);

        void Start(GameState game)
        {
            Tween.KillAll();
            camera = new GameObject("Camera").AddComponent<Camera>();
            view = BoardView.Create();
            hud = HudView.Create(camera);
            hud.ApplyLayout(BoardLayout.Compute(new Vector2(1080, 1920), new Rect(0, 0, 1080, 1920)));
            controller = new LinesGameController(game, new BoardPresenter(view, hud));
        }

        [TearDown]
        public void TearDown()
        {
            Tween.KillAll();
            if (view != null) Object.DestroyImmediate(view.gameObject);
            if (hud != null) Object.DestroyImmediate(hud.gameObject);
            if (camera != null) Object.DestroyImmediate(camera.gameObject);
        }

        void Play(Cell from, Cell to)
        {
            controller.Tap(from);
            controller.Tap(to);
            for (int i = 0; i < 300 && !controller.AcceptsInput && controller.State != TurnState.GameOver; i++)
                Tween.Tick(0.02f);
            for (int i = 0; i < 30; i++) Tween.Tick(0.02f); // let particles and "+N" finish
        }

        /// <summary>Every cell on screen shows exactly what the rules say, in place and at full size.</summary>
        void AssertScreenMatchesRules(string when)
        {
            var game = controller.Game;
            for (int i = 0; i < 81; i++)
            {
                var cell = C(i % 9, i / 9);
                var color = game.Board.Get(cell);
                var ball = view.BallAt(cell);
                if (color == BallColor.None)
                {
                    Assert.IsNull(ball, $"{when}: {cell} should be empty on screen");
                    continue;
                }
                Assert.IsNotNull(ball, $"{when}: {cell} should show a piece");
                Assert.AreEqual(color, ball.Color, $"{when}: {cell} color");
                Assert.AreEqual(BoardView.CellToLocal(cell), ball.transform.localPosition, $"{when}: {cell} position");
                Assert.AreEqual(ball.FullScale, ball.transform.localScale, $"{when}: {cell} size");
                Assert.AreEqual(1f, ball.Renderer.color.a, $"{when}: {cell} fully visible");
            }
            Assert.AreEqual(81 - game.Board.EmptyCount, view.BallCount, $"{when}: no extra pieces on screen");
            Assert.AreEqual(game.Score.ToString(), hud.ScoreText, $"{when}: score");
            if (!game.IsOver)
                CollectionAssert.AreEquivalent(game.Preview.Cells.Where(game.Board.IsEmpty), view.DotCells.ToList(), $"{when}: preview dots");
            Assert.AreEqual(game.CanUndo, hud.UndoButton.Interactable, $"{when}: Undo button");
            Assert.AreEqual(0, view.GetComponentsInChildren<SpriteRenderer>().Count(r => r.name == "Particle"), $"{when}: particles cleaned up");
        }

        /// <summary>A full board where no two neighbours share a color, so it has no lines.</summary>
        static Board FullBoardWithoutLines(params Cell[] free)
        {
            var board = new Board();
            foreach (var cell in board.EmptyCells())
                if (!free.Contains(cell))
                    board.Place(cell, BallColors.All[(cell.Column + 2 * cell.Row) % 7]);
            return board;
        }

        static Preview PreviewOf(BallColor[] colors, params Cell[] cells) => new Preview(colors, cells);

        // ---- Crossing lines ----

        [Test]
        public void CrossingLines_BothClearOnScreen_40Points()
        {
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
            Start(new GameState(board, PreviewOf(new[] { BallColor.Blue, BallColor.Green, BallColor.Cyan }, C(8, 0), C(7, 0), C(6, 0)), 0, new Spawner(1)));

            Play(C(8, 8), C(4, 4));

            Assert.AreEqual(40, controller.Game.Score);
            Assert.AreEqual(0, view.BallCount, "All 9 pieces gone from the screen");
            AssertScreenMatchesRules("after the crossing");
        }

        // ---- Moving onto a preview dot ----

        [Test]
        public void MoveOntoAPreviewDot_DotGoes_ThatPieceLandsElsewhere()
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
            var preview = PreviewOf(new[] { BallColor.Blue, BallColor.Green, BallColor.Cyan }, C(1, 8), C(8, 0), C(8, 1));
            Start(new GameState(board, preview, 0, new Spawner(3)));
            Assert.Contains(C(1, 8), view.DotCells.ToList());

            Play(C(0, 8), C(1, 8));

            Assert.AreEqual(BallColor.Red, controller.Game.Board.Get(C(1, 8)), "The player's piece took the dot's cell");
            Assert.AreEqual(4, view.BallCount, "1 moved + 3 new, one of them somewhere else");
            Assert.AreEqual(1, Enumerable.Range(0, 81).Count(i => controller.Game.Board.Get(C(i % 9, i / 9)) == BallColor.Blue),
                "The blue piece still arrived");
            AssertScreenMatchesRules("after moving onto a dot");
        }

        // ---- Spawned pieces complete a line ----

        [Test]
        public void SpawnedPieceCompletesALine_ClearsOnScreen()
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
            var preview = PreviewOf(new[] { BallColor.Green, BallColor.Blue, BallColor.Purple }, C(4, 0), C(8, 8), C(7, 8));
            Start(new GameState(board, preview, 0, new Spawner(6)));

            Play(C(0, 8), C(1, 8));

            Assert.AreEqual(10, controller.Game.Score, "No multiplier for spawned lines");
            Assert.IsNull(view.BallAt(C(0, 0)), "The green line is gone");
            AssertScreenMatchesRules("after a spawned line");
        }

        // ---- Fewer than 3 free cells: the board fills, game over is final ----

        [Test]
        public void LastSpawnFillsTheBoard_GameOver_IsFinal()
        {
            var board = FullBoardWithoutLines(C(6, 8), C(7, 8), C(8, 8));
            var preview = PreviewOf(new[] { BallColor.Red, BallColor.Blue, BallColor.Purple }, C(5, 8), C(7, 8), C(8, 8));
            Start(new GameState(board, preview, 120, new Spawner(7)));

            Play(C(5, 8), C(6, 8));

            Assert.AreEqual(TurnState.GameOver, controller.State);
            Assert.AreEqual(81, view.BallCount, "The screen shows a full board");
            Assert.IsFalse(hud.UndoButton.Interactable, "No Undo after game over");
            Assert.IsFalse(controller.Undo());
            Assert.IsFalse(controller.Tap(C(0, 0)), "Taps are ignored");
            AssertScreenMatchesRules("game over");
        }

        [Test]
        public void FewerThanThreeFree_OnlyThoseFill_ThenGameOver()
        {
            // 3 free cells; the move frees one and fills one, so the spawn has only 3 cells for 3 pieces…
            // here the preview has only 2 cells, as on a nearly full board.
            var board = FullBoardWithoutLines(C(7, 8), C(8, 8));
            var preview = PreviewOf(new[] { BallColor.Red, BallColor.Blue, BallColor.Purple }, C(8, 8));
            Start(new GameState(board, preview, 0, new Spawner(8)));

            Play(C(6, 8), C(7, 8));

            Assert.AreEqual(TurnState.GameOver, controller.State, "2 free cells, 3 pieces due: the board fills");
            Assert.AreEqual(81, view.BallCount);
            AssertScreenMatchesRules("nearly full board");
        }

        [Test]
        public void LastSpawnFillsTheBoard_ButMakesALine_NotGameOver()
        {
            // Row 8 starts with 4 reds; (5,8) is red too. The red preview piece lands on (4,8): a line of 6.
            var board = FullBoardWithoutLines(C(4, 8), C(7, 0), C(8, 0));
            for (int c = 0; c < 4; c++) { board.Remove(C(c, 8)); board.Place(C(c, 8), BallColor.Red); }
            var preview = PreviewOf(new[] { BallColor.Red, BallColor.Purple, BallColor.Orange }, C(4, 8), C(6, 0), C(8, 0));
            Start(new GameState(board, preview, 0, new Spawner(9)));

            Play(C(6, 0), C(7, 0)); // frees (6,0); the 3 pieces then fill the last 3 cells

            Assert.AreNotEqual(TurnState.GameOver, controller.State, "The line freed cells, so the game goes on");
            Assert.IsFalse(controller.Game.IsOver);
            Assert.AreEqual(14, controller.Game.Score, "A line of 6 from a spawn: 14 points");
            Assert.AreEqual(6, controller.Game.Board.EmptyCount);
            Assert.Greater(controller.Game.Preview.Cells.Count, 0, "A new preview was chosen");
            AssertScreenMatchesRules("spawn filled the board but cleared a line");
        }

        // ---- Whole games through the real screen ----

        [Test]
        public void WholeGames_WithAnimations_ScreenAlwaysMatchesRules()
        {
            for (int seed = 11; seed <= 14; seed++)
            {
                TearDown();
                Start(GameState.NewGame(seed));
                var random = new GameRandom(seed);
                int turns = 0;
                while (controller.State != TurnState.GameOver)
                {
                    Assert.Less(turns++, 500, "The game should end");
                    var (from, to) = RandomLegalMove(controller.Game.Board, random);
                    Play(from, to);

                    // Now and then, undo and replay to mix Undo into the game.
                    if (turns % 7 == 0 && controller.Game.CanUndo)
                    {
                        controller.Undo();
                        AssertScreenMatchesRules($"seed {seed}, after undo at turn {turns}");
                        continue;
                    }
                    AssertScreenMatchesRules($"seed {seed}, turn {turns}");
                }
            }
        }

        static (Cell from, Cell to) RandomLegalMove(Board board, GameRandom random)
        {
            var balls = Enumerable.Range(0, 81).Select(i => C(i % 9, i / 9)).Where(c => !board.IsEmpty(c)).ToList();
            var empty = board.EmptyCells();
            for (int attempt = 0; attempt < 300; attempt++)
            {
                var from = balls[random.Next(balls.Count)];
                var to = empty[random.Next(empty.Count)];
                if (Pathfinder.HasPath(board, from, to)) return (from, to);
            }
            foreach (var from in balls)
            foreach (var to in empty)
                if (Pathfinder.HasPath(board, from, to)) return (from, to);
            throw new AssertionException("No legal move");
        }
    }
}
