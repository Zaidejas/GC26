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
    /// <summary>The main menu and the back arrow in the real Main scene.</summary>
    public class MenuPlayModeTests : PlayModeTestBase
    {
        static Cell C(int i) => new Cell(i % 9, i / 9);

        static IEnumerator Launch()
        {
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
        }

        static IEnumerator PlayOneTurn(GameBootstrap game)
        {
            var board = game.Controller.Game.Board;
            var ball = Enumerable.Range(0, 81).Select(C).First(c => !board.IsEmpty(c));
            var target = board.EmptyCells().First(c => Pathfinder.HasPath(board, ball, c));
            game.Controller.Tap(ball);
            game.Controller.Tap(target);
            float waited = 0;
            while (!game.Controller.AcceptsInput && waited < 3f) { waited += Time.deltaTime; yield return null; }
        }

        [UnityTest]
        public IEnumerator FirstLaunch_OpensOnTheMenu()
        {
            yield return Launch();
            var game = Object.FindAnyObjectByType<GameBootstrap>();

            Assert.IsTrue(game.Menu.IsShowing);
            Assert.IsFalse(game.GameScreenShowing);
            Assert.IsFalse(game.Menu.ContinueButton.Interactable, "No game to continue yet");
            Assert.IsTrue(game.Menu.NewGameButton.Interactable);
            Assert.IsTrue(game.Menu.HowToPlayButton.Interactable);
            Assert.IsFalse(game.Menu.LeaderboardButton.Interactable, "Comes in task 24");
            Assert.IsTrue(game.InputBlocked, "The board can't be tapped behind the menu");
        }

        [UnityTest]
        public IEnumerator NewGame_Back_Continue_ReturnsToTheSameGame()
        {
            yield return Launch();
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            game.Menu.NewGameButton.Click();
            Assert.IsTrue(game.GameScreenShowing);
            Assert.IsFalse(game.Menu.IsShowing);
            yield return PlayOneTurn(game);
            var controller = game.Controller;
            var cells = Enumerable.Range(0, 81).Select(i => controller.Game.Board.Get(C(i))).ToArray();

            game.Hud.BackButton.Click();

            Assert.IsTrue(game.Menu.IsShowing, "Back to the menu");
            Assert.IsFalse(game.GameScreenShowing);
            Assert.IsTrue(game.Menu.ContinueButton.Interactable);
            StringAssert.Contains("score " + controller.Game.Score, game.Menu.ContinueButton.Text);

            game.Menu.ContinueButton.Click();

            Assert.IsTrue(game.GameScreenShowing);
            Assert.AreSame(controller, game.Controller, "The same game, not a new one");
            CollectionAssert.AreEqual(cells, Enumerable.Range(0, 81).Select(i => game.Controller.Game.Board.Get(C(i))).ToArray());
            Assert.IsTrue(game.Hud.UndoButton.Interactable, "Undo still there");
        }

        [UnityTest]
        public IEnumerator Back_DropsTheSelection_AndSaves()
        {
            yield return Launch();
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            game.Menu.NewGameButton.Click();
            yield return PlayOneTurn(game);
            var ball = Enumerable.Range(0, 81).Select(C).First(c => !game.Controller.Game.Board.IsEmpty(c));
            game.Controller.Tap(ball);
            Assert.AreEqual(TurnState.BallSelected, game.Controller.State);

            game.Hud.BackButton.Click();

            Assert.AreEqual(TurnState.Idle, game.Controller.State, "No piece left selected");
            var saved = game.Saves.Load().ToGameState();
            Assert.AreEqual(game.Controller.Game.Score, saved.Score);
            Assert.AreEqual(game.Controller.Game.Board.EmptyCount, saved.Board.EmptyCount);
        }

        [UnityTest]
        public IEnumerator Back_IsIgnoredWhileAPieceMoves()
        {
            yield return Launch();
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            game.Menu.NewGameButton.Click();
            var board = game.Controller.Game.Board;
            var ball = Enumerable.Range(0, 81).Select(C).First(c => !board.IsEmpty(c));
            var target = board.EmptyCells().OrderByDescending(c => System.Math.Abs(c.Row - ball.Row) + System.Math.Abs(c.Column - ball.Column))
                .First(c => Pathfinder.HasPath(board, ball, c));
            game.Controller.Tap(ball);
            game.Controller.Tap(target);
            Assert.AreEqual(TurnState.Moving, game.Controller.State);

            game.Hud.BackButton.Click();

            Assert.IsTrue(game.GameScreenShowing, "Stays on the game until the turn is done");
            yield return null;
        }

        [UnityTest]
        public IEnumerator HowToPlay_OpensFromTheMenu_AndReturns()
        {
            yield return Launch();
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            game.Menu.HowToPlayButton.Click();
            Assert.IsTrue(game.HowToPlay.IsShowing);
            Assert.IsFalse(game.Menu.IsShowing);
            Assert.IsTrue(game.InputBlocked);
            Assert.AreEqual(0, game.HowToPlay.Step);

            game.HowToPlay.NextButton.Click();
            game.HowToPlay.NextButton.Click();
            Assert.AreEqual("Got it", game.HowToPlay.NextButton.Text);
            game.HowToPlay.NextButton.Click();
            Assert.IsTrue(game.Menu.IsShowing, "Got it returns to the menu");
            Assert.IsFalse(game.HowToPlay.IsShowing);

            game.Menu.HowToPlayButton.Click();
            Assert.AreEqual(0, game.HowToPlay.Step, "Starts on the first card again");
            game.HowToPlay.BackButton.Click();
            Assert.IsTrue(game.Menu.IsShowing, "The back arrow returns to the menu");
            yield return null;
        }

        [UnityTest]
        public IEnumerator NewGame_WithAnUnfinishedGame_AsksFirst()
        {
            yield return Launch();
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            game.Menu.NewGameButton.Click();
            yield return PlayOneTurn(game);
            game.Hud.BackButton.Click();
            var controller = game.Controller;

            game.Menu.NewGameButton.Click();
            Assert.IsTrue(game.Hud.Dialog.IsOpen, "Asks before throwing the game away");
            Assert.AreEqual(GameBootstrap.ResetTitle, game.Hud.Dialog.Title);

            game.Hud.Dialog.CancelButton.Click();
            Assert.IsTrue(game.Menu.IsShowing, "Cancel stays on the menu");
            Assert.AreSame(controller, game.Controller);

            game.Menu.NewGameButton.Click();
            game.Hud.Dialog.ConfirmButton.Click();
            Assert.IsTrue(game.GameScreenShowing);
            Assert.AreNotSame(controller, game.Controller, "A new game");
            Assert.AreEqual(78, game.Controller.Game.Board.EmptyCount);
        }
    }
}
