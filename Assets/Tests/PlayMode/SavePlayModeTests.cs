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
    /// <summary>Saving in the real Main scene: play, "restart the app" (reload the scene), continue.</summary>
    public class SavePlayModeTests : PlayModeTestBase
    {
        static Cell C(int i) => new Cell(i % 9, i / 9);

        static IEnumerator WaitUntilSettled(GameBootstrap game)
        {
            float waited = 0;
            while (!game.Controller.AcceptsInput && game.Controller.State != TurnState.GameOver && waited < 3f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
        }

        static IEnumerator Launch()
        {
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayThenRestart_TheSameGameContinues()
        {
            yield return Launch();
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            Assert.IsFalse(game.Menu.ContinueButton.Interactable, "Nothing to continue on a first launch");
            game.Menu.NewGameButton.Click();
            Assert.IsTrue(game.Saves.HasSave, "A new game is saved straight away");

            // Play one turn.
            var board = game.Controller.Game.Board;
            var ball = Enumerable.Range(0, 81).Select(C).First(c => !board.IsEmpty(c));
            var target = board.EmptyCells().First(c => Pathfinder.HasPath(board, ball, c));
            game.Controller.Tap(ball);
            game.Controller.Tap(target);
            yield return WaitUntilSettled(game);
            var cellsBefore = Enumerable.Range(0, 81).Select(i => board.Get(C(i))).ToArray();
            var scoreBefore = game.Controller.Game.Score;
            var previewBefore = game.Controller.Game.Preview.Cells.ToArray();
            var creaturesBefore = game.BoardView.Pieces.CreatureIndices;

            // "Restart the app": it opens on the menu, and Continue brings the game back.
            yield return Launch();
            game = Object.FindAnyObjectByType<GameBootstrap>();
            Assert.IsTrue(game.Menu.IsShowing);
            Assert.IsTrue(game.Menu.ContinueButton.Interactable);
            StringAssert.Contains("score " + scoreBefore, game.Menu.ContinueButton.Text);
            game.Menu.ContinueButton.Click();

            var restored = game.Controller.Game;
            CollectionAssert.AreEqual(cellsBefore, Enumerable.Range(0, 81).Select(i => restored.Board.Get(C(i))).ToArray(), "Same board");
            Assert.AreEqual(scoreBefore, restored.Score);
            CollectionAssert.AreEqual(previewBefore, restored.Preview.Cells.ToArray(), "Same preview");
            CollectionAssert.AreEqual(creaturesBefore, game.BoardView.Pieces.CreatureIndices, "Same creatures");
            Assert.AreEqual(81 - restored.Board.EmptyCount, game.BoardView.BallCount, "The screen shows it");
            Assert.IsTrue(game.Hud.UndoButton.Interactable, "Undo works after a restart");

            // Undo after the restart takes back the move from before it.
            game.Hud.UndoButton.Click();
            Assert.AreEqual(BallColor.None, restored.Board.Get(target));
        }

        [UnityTest]
        public IEnumerator Reset_SavesTheNewGame()
        {
            yield return Launch();
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            game.Menu.NewGameButton.Click();
            game.AskToReset();
            game.Hud.Dialog.ConfirmButton.Click();
            var cellsAfterReset = Enumerable.Range(0, 81).Select(i => game.Controller.Game.Board.Get(C(i))).ToArray();

            yield return Launch();
            game = Object.FindAnyObjectByType<GameBootstrap>();
            game.Menu.ContinueButton.Click();

            CollectionAssert.AreEqual(cellsAfterReset, Enumerable.Range(0, 81).Select(i => game.Controller.Game.Board.Get(C(i))).ToArray());
            Assert.IsFalse(game.Hud.UndoButton.Interactable);
        }

        [UnityTest]
        public IEnumerator DamagedSaveFile_StartsANewGameInstead()
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(SaveFolder, Lines.Services.SaveService.FileName), "{ broken");
            yield return Launch();
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            Assert.IsFalse(game.Menu.ContinueButton.Interactable, "A broken save can't be continued");
            game.Menu.NewGameButton.Click();
            Assert.AreEqual(78, game.Controller.Game.Board.EmptyCount, "A fresh game");
            Assert.AreEqual(TurnState.Idle, game.Controller.State);
        }
    }
}
