using System.Collections;
using System.Linq;
using Lines.Core;
using Lines.Game;
using Lines.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lines.Tests
{
    /// <summary>Reset in the real Main scene: the question, Cancel, and Yes starting a fresh game.</summary>
    public class ResetPlayModeTests : PlayModeTestBase
    {
        [UnityTest]
        public IEnumerator Reset_AsksFirst_CancelKeepsTheGame_YesStartsANewOne()
        {
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            game.Menu.NewGameButton.Click();

            // Play one move so there is something to lose.
            var board = game.Controller.Game.Board;
            var all = Enumerable.Range(0, 81).Select(i => new Cell(i % 9, i / 9)).ToList();
            var ball = all.First(c => !board.IsEmpty(c));
            var target = all.First(c => board.IsEmpty(c) && Pathfinder.HasPath(board, ball, c));
            Vector2 Px(Cell c) => game.Layout.WorldToScreen(game.Layout.BoardCenterWorld + (Vector2)BoardView.CellToLocal(c));
            game.Taps.HandlePress(Px(ball));
            game.Taps.HandlePress(Px(target));
            float waited = 0;
            while (game.Controller.State != TurnState.Idle && waited < 2f) { waited += Time.deltaTime; yield return null; }
            var controllerBefore = game.Controller;
            var emptyBefore = board.EmptyCells();

            // Reset asks first; the board can't be tapped meanwhile.
            game.Hud.ResetButton.Click();
            Assert.IsTrue(game.Hud.Dialog.IsOpen);
            Assert.AreEqual(GameBootstrap.ResetTitle, game.Hud.Dialog.Title);
            Assert.IsFalse(game.Taps.HandlePress(Px(all.First(c => !board.IsEmpty(c)))), "Board blocked by the question");

            // Cancel: same game.
            game.Hud.Dialog.CancelButton.Click();
            Assert.IsFalse(game.Hud.Dialog.IsOpen);
            Assert.AreSame(controllerBefore, game.Controller);
            CollectionAssert.AreEqual(emptyBefore, board.EmptyCells());
            Assert.IsTrue(game.Hud.UndoButton.Interactable, "Undo still available");

            // Yes: a fresh game.
            game.Hud.ResetButton.Click();
            game.Hud.Dialog.ConfirmButton.Click();
            yield return null;
            Assert.IsFalse(game.Hud.Dialog.IsOpen);
            Assert.AreNotSame(controllerBefore, game.Controller);
            Assert.AreEqual(78, game.Controller.Game.Board.EmptyCount, "3 pieces on a new board");
            Assert.AreEqual(3, game.BoardView.BallCount);
            Assert.AreEqual("0", game.Hud.ScoreText);
            Assert.IsFalse(game.Hud.UndoButton.Interactable, "Nothing to undo in a new game");
            Assert.AreEqual(TurnState.Idle, game.Controller.State);
            Assert.IsTrue(game.Taps.HandlePress(Px(all.First(c => !game.Controller.Game.Board.IsEmpty(c)))), "Board works again");
        }
    }
}
