using System.Collections;
using System.Linq;
using GC26.Lines.Core;
using GC26.Lines.Game;
using GC26.Lines.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GC26.Lines.Tests
{
    /// <summary>Game over, the name pop-up and the leaderboard in the real Main scene.</summary>
    public class GameOverPlayModeTests : PlayModeTestBase
    {
        static Cell C(int column, int row) => new Cell(column, row);

        static IEnumerator Launch()
        {
            yield return LoadGameScene();
            yield return null;
        }

        // A saved game one move from the end: two free cells, and the next spawn fills the board
        // (the same position as EdgeCaseTests.FewerThanThreeFree_OnlyThoseFill_ThenGameOver).
        void SaveNearlyOverGame(int score)
        {
            var board = new Board();
            foreach (var cell in board.EmptyCells())
                if (cell != C(7, 8) && cell != C(8, 8))
                    board.Place(cell, BallColors.All[(cell.Column + 2 * cell.Row) % 7]);
            var preview = new Preview(new[] { BallColor.Red, BallColor.Blue, BallColor.Purple }, new[] { C(8, 8) });
            new SaveService().Save(GameSave.From(new GameState(board, preview, score, new Spawner(8))));
        }

        static IEnumerator PlayTheLastMove(GameBootstrap game)
        {
            game.Menu.ContinueButton.Click();
            game.Controller.Tap(C(6, 8));
            game.Controller.Tap(C(7, 8));
            float waited = 0;
            while (game.Controller.State != TurnState.GameOver && waited < 3f) { waited += Time.deltaTime; yield return null; }
            Assert.AreEqual(TurnState.GameOver, game.Controller.State);
        }

        [UnityTest]
        public IEnumerator GameOver_Name_Save_OpensTheLeaderboard()
        {
            SaveNearlyOverGame(120);
            yield return Launch();
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            Assert.IsTrue(game.Menu.LeaderboardButton.Interactable);
            yield return PlayTheLastMove(game);

            Assert.IsTrue(game.Hud.GameOver.IsOpen, "The pop-up opens when the board is full");
            Assert.AreEqual("120", game.Hud.GameOver.FinalText);
            Assert.AreEqual("Player1", game.Hud.GameOver.NameField.text);
            Assert.IsTrue(game.InputBlocked);
            Assert.IsTrue(game.Saves.Load().hasPendingScore, "The score waits in the save");
            Assert.AreEqual(0, game.Scores.Count, "Not on the leaderboard before Save");

            game.Hud.GameOver.SetName("Anna");
            game.Hud.GameOver.SaveButton.Click();

            Assert.IsFalse(game.Hud.GameOver.IsOpen);
            Assert.IsTrue(game.LeaderboardScreen.IsShowing);
            Assert.IsFalse(game.GameScreenShowing);
            Assert.AreEqual("New best score, Anna!", game.LeaderboardScreen.Message);
            Assert.AreEqual(0, game.LeaderboardScreen.HighlightedRow);
            Assert.IsTrue(game.LeaderboardScreen.PlayAgainShowing);
            Assert.IsFalse(game.Saves.Load().hasPendingScore);

            // Back to the menu: Continue is off (the game is over); the leaderboard keeps the score.
            game.LeaderboardScreen.BackButton.Click();
            Assert.IsTrue(game.Menu.IsShowing);
            Assert.IsFalse(game.Menu.ContinueButton.Interactable);

            // "Restart the app": the score survives, the name is remembered.
            yield return Launch();
            game = Object.FindAnyObjectByType<GameBootstrap>();
            Assert.AreEqual(1, game.Scores.Count, "Not added twice");
            Assert.AreEqual("Anna", game.Scores.entries[0].name);
            Assert.AreEqual("Anna", game.Scores.DefaultName);
            game.Menu.LeaderboardButton.Click();
            Assert.AreEqual(1, game.LeaderboardScreen.RowCount);
            Assert.AreEqual("", game.LeaderboardScreen.Message, "No result line from the menu");
            Assert.IsFalse(game.LeaderboardScreen.PlayAgainShowing);
        }

        [UnityTest]
        public IEnumerator ClosingOnThePopUp_AddsTheScoreAtTheNextLaunch()
        {
            SaveNearlyOverGame(75);
            yield return Launch();
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            yield return PlayTheLastMove(game);
            Assert.IsTrue(game.Hud.GameOver.IsOpen);

            yield return Launch(); // the app closes with the pop-up open
            game = Object.FindAnyObjectByType<GameBootstrap>();

            Assert.AreEqual(1, game.Scores.Count);
            Assert.AreEqual(75, game.Scores.entries[0].score);
            Assert.AreEqual("Player1", game.Scores.entries[0].name, "Under the pre-filled name");
            Assert.AreEqual("Player2", game.Scores.DefaultName, "The placeholder count moved on");
            Assert.IsFalse(game.Saves.Load().hasPendingScore);
            Assert.IsFalse(game.Menu.ContinueButton.Interactable);

            yield return Launch();
            game = Object.FindAnyObjectByType<GameBootstrap>();
            Assert.AreEqual(1, game.Scores.Count, "Added once only");
        }

        [UnityTest]
        public IEnumerator PlayAgain_StartsANewGame_AndResetNeverAddsAScore()
        {
            SaveNearlyOverGame(50);
            yield return Launch();
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            yield return PlayTheLastMove(game);
            game.Hud.GameOver.SaveButton.Click();
            game.LeaderboardScreen.PlayAgainButton.Click();

            Assert.IsTrue(game.GameScreenShowing);
            Assert.AreEqual(78, game.Controller.Game.Board.EmptyCount, "A fresh game");
            Assert.AreEqual(1, game.Scores.Count);

            game.AskToReset();
            game.Hud.Dialog.ConfirmButton.Click();
            Assert.AreEqual(1, game.Scores.Count, "Reset doesn't add the abandoned game");
            Assert.AreEqual(1, new LeaderboardService().Load().Count);
            yield return null;
        }
    }
}
