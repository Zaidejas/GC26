using System.Linq;
using Lines.Core;
using Lines.Game;
using Lines.UI;
using Lines.View;
using NUnit.Framework;
using UnityEngine;

namespace Lines.Tests
{
    public class UndoTests
    {
        static Cell C(int column, int row) => new Cell(column, row);

        static GameState Game(Board board, params Cell[] previewCells) =>
            new GameState(board, new Preview(new[] { BallColor.Yellow, BallColor.Cyan, BallColor.Orange }, previewCells), 0, new Spawner(1));

        static Board OneBall() => TestBoards.FromRows(
            "R........",
            ".........",
            ".........",
            ".........",
            ".........",
            ".........",
            ".........",
            ".........",
            ".........");

        // ---- Core rules ----

        [Test]
        public void NewGame_NothingToUndo()
        {
            var game = GameState.NewGame(3);
            Assert.IsFalse(game.CanUndo);
            Assert.IsFalse(game.Undo());
        }

        [Test]
        public void Undo_RestoresBoardScoreAndPreview_Exactly()
        {
            var game = GameState.NewGame(4);
            var boardBefore = game.Board.Clone();
            var previewBefore = game.Preview;
            var ball = Enumerable.Range(0, 81).Select(i => C(i % 9, i / 9)).First(c => !game.Board.IsEmpty(c));
            var target = game.Board.EmptyCells().First(c => Pathfinder.HasPath(game.Board, ball, c));

            game.TryMove(ball, target);
            Assert.IsTrue(game.CanUndo);
            bool undone = false;
            game.OnUndone += () => undone = true;

            Assert.IsTrue(game.Undo());

            Assert.IsTrue(undone);
            for (int i = 0; i < 81; i++)
                Assert.AreEqual(boardBefore.Get(C(i % 9, i / 9)), game.Board.Get(C(i % 9, i / 9)));
            Assert.AreSame(previewBefore, game.Preview);
            Assert.AreEqual(0, game.Score);
        }

        [Test]
        public void OnlyOneStep_SecondUndoDoesNothing()
        {
            var game = Game(OneBall(), C(8, 8), C(7, 8), C(6, 8));
            game.TryMove(C(0, 0), C(0, 1));
            game.TryMove(C(0, 1), C(0, 2));

            Assert.IsTrue(game.Undo());
            Assert.AreEqual(BallColor.Red, game.Board.Get(C(0, 1)), "Back one move, not two");
            Assert.IsFalse(game.CanUndo, "Disabled right after an undo");
            Assert.IsFalse(game.Undo());
        }

        [Test]
        public void UndoAfterClearingALine_BringsBallsAndPointsBack()
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
            var game = Game(board, C(8, 0), C(7, 0), C(6, 0));
            game.TryMove(C(4, 8), C(4, 4));
            Assert.AreEqual(10, game.Score);

            game.Undo();

            Assert.AreEqual(0, game.Score, "Undo removes the points of the move it takes back");
            Assert.AreEqual(76, game.Board.EmptyCount);
            Assert.AreEqual(BallColor.Red, game.Board.Get(C(4, 8)));
        }

        [Test]
        public void BlockedMove_DoesNotChangeUndo()
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
            var game = Game(board, C(8, 8), C(7, 8), C(6, 8));
            game.TryMove(C(0, 0), C(4, 4)); // blocked
            Assert.IsFalse(game.CanUndo, "A blocked tap is not a move");
        }

        [Test]
        public void GameOver_NoUndo()
        {
            var board = new Board();
            foreach (var cell in board.EmptyCells())
                board.Place(cell, BallColors.All[(cell.Column + 2 * cell.Row) % 7]);
            board.Remove(C(6, 8)); board.Remove(C(7, 8)); board.Remove(C(8, 8));
            var game = new GameState(board, new Preview(new[] { BallColor.Red, BallColor.Blue, BallColor.Purple },
                new[] { C(5, 8), C(7, 8), C(8, 8) }), 0, new Spawner(1));

            game.TryMove(C(5, 8), C(6, 8));

            Assert.IsTrue(game.IsOver);
            Assert.IsFalse(game.CanUndo, "Game over is final");
            Assert.IsFalse(game.Undo());
        }

        [Test]
        public void Board_CopyFrom()
        {
            var a = OneBall();
            var b = new Board();
            b.Place(C(5, 5), BallColor.Blue);
            b.CopyFrom(a);
            Assert.AreEqual(BallColor.Red, b.Get(C(0, 0)));
            Assert.IsTrue(b.IsEmpty(C(5, 5)));
        }

        // ---- Controller and button ----

        BoardView view;
        HudView hud;
        Camera camera;

        GameController Controller(GameState game)
        {
            Tween.KillAll();
            camera = new GameObject("Camera").AddComponent<Camera>();
            view = BoardView.Create();
            hud = HudView.Create(camera);
            hud.ApplyLayout(BoardLayout.Compute(new Vector2(1080, 1920), new Rect(0, 0, 1080, 1920)));
            var controller = new GameController(game, new BoardPresenter(view, hud));
            hud.UndoClicked += () => controller.Undo();
            return controller;
        }

        [TearDown]
        public void TearDown()
        {
            Tween.KillAll();
            if (view != null) Object.DestroyImmediate(view.gameObject);
            if (hud != null) Object.DestroyImmediate(hud.gameObject);
            if (camera != null) Object.DestroyImmediate(camera.gameObject);
        }

        static void FinishTurn(GameController controller)
        {
            for (int i = 0; i < 200 && !controller.AcceptsInput; i++) Tween.Tick(0.02f);
        }

        [Test]
        public void UndoButton_GreyAtStart_UsableAfterAMove_GreyAfterUndo()
        {
            var controller = Controller(Game(OneBall(), C(8, 8), C(7, 8), C(6, 8)));
            Assert.IsFalse(hud.UndoButton.Interactable, "Nothing to undo at the start");

            controller.Tap(C(0, 0));
            controller.Tap(C(0, 3));
            FinishTurn(controller);
            Assert.IsTrue(hud.UndoButton.Interactable);

            hud.UndoButton.Click();

            Assert.AreEqual(BallColor.Red, controller.Game.Board.Get(C(0, 0)), "The ball is back");
            Assert.AreEqual(1, view.BallCount, "The spawned balls are gone from the screen too");
            Assert.AreEqual(3, view.DotCells.Count(), "The old preview dots are back");
            Assert.IsFalse(hud.UndoButton.Interactable, "Grey right after an undo");
            Assert.AreEqual(TurnState.Idle, controller.State);
        }

        [Test]
        public void UndoDuringAnAnimation_IsIgnored()
        {
            var controller = Controller(Game(OneBall(), C(8, 8), C(7, 8), C(6, 8)));
            controller.Tap(C(0, 0));
            controller.Tap(C(0, 1));
            FinishTurn(controller);
            controller.Tap(C(0, 1));
            controller.Tap(C(0, 5)); // second move, still rolling

            Assert.AreEqual(TurnState.Moving, controller.State);
            Assert.IsFalse(controller.Undo(), "Not while the ball rolls");
            FinishTurn(controller);
            Assert.IsTrue(controller.Undo(), "Fine once the turn is over");
            Assert.AreEqual(BallColor.Red, controller.Game.Board.Get(C(0, 1)));
        }

        [Test]
        public void Undo_ClearsTheSelection()
        {
            var controller = Controller(Game(OneBall(), C(8, 8), C(7, 8), C(6, 8)));
            controller.Tap(C(0, 0));
            controller.Tap(C(0, 1));
            FinishTurn(controller);
            controller.Tap(C(8, 8)); // select a spawned ball

            controller.Undo();

            Assert.IsNull(controller.Selected);
            Assert.AreEqual(TurnState.Idle, controller.State);
        }
    }
}
