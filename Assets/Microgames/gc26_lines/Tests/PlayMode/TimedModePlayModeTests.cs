using System.Collections;
using GC26.Lines.Audio;
using GC26.Lines.Core;
using GC26.Lines.Game;
using GC26.Lines.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GC26.Lines.Tests
{
    /// <summary>L09 (GDD → Timed mode) in the real scene: choosing the mode, the clock, Time's up and the two lists.</summary>
    public class TimedModePlayModeTests : PlayModeTestBase
    {
        static Cell C(int column, int row) => new Cell(column, row);

        static IEnumerator Launch()
        {
            yield return LoadGameScene();
            yield return null;
        }

        // A saved timed game: four red mushrooms in the top row, and the red one at (4, 2) can finish the line.
        static void SaveTimedGame(int score, float timeLeft)
        {
            var board = new Board();
            foreach (var c in new[] { C(0, 0), C(1, 0), C(2, 0), C(3, 0), C(4, 2) }) board.Place(c, BallColor.Red);
            board.Place(C(8, 8), BallColor.Blue);
            var preview = new Preview(new[] { BallColor.Green, BallColor.Yellow, BallColor.Purple }, new[] { C(6, 6), C(7, 7), C(6, 8) });
            new SaveService(timed: true).Save(GameSave.From(new GameState(board, preview, score, new Spawner(5), timed: true, timeLeft: timeLeft)));
        }

        static IEnumerator WaitForIdle(GameBootstrap game)
        {
            float waited = 0f;
            while (game.Controller.State != TurnState.Idle && waited < 3f) { waited += Time.deltaTime; yield return null; }
        }

        [UnityTest]
        public IEnumerator ChoosingTimed_ANewTimedGame_WithTheClock_EachModeKeepsItsGame()
        {
            yield return Launch();
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            Assert.IsFalse(game.Timed, "Classic at first");
            game.Menu.NewGameButton.Click(); // a Classic game in progress
            SkipTutorials(game);
            yield return null;
            game.Hud.BackButton.Click();

            game.Menu.Modes.TimedButton.onClick.Invoke();
            Assert.IsTrue(game.Timed);
            Assert.IsTrue(game.Settings.mode == "timed", "Remembered");
            Assert.IsFalse(game.Menu.ContinueButton.Interactable, "No timed game yet");
            game.Menu.NewGameButton.Click();
            SkipTutorials(game);
            yield return null;
            Assert.IsTrue(game.Controller.Game.Timed);
            Assert.IsTrue(game.Hud.ClockShowing);
            Assert.IsFalse(game.Hud.UndoShowing);
            Assert.AreEqual("3:00", game.Hud.ClockReading);

            game.TickClock(10f);
            Assert.AreEqual(170f, game.Controller.Game.TimeLeft, 0.3f, "The clock runs while the player thinks");
            Assert.AreEqual("2:50", game.Hud.ClockReading);

            game.Hud.SettingsButton.Click();
            game.TickClock(10f);
            Assert.AreEqual(170f, game.Controller.Game.TimeLeft, 0.3f, "...not behind Settings");
            game.Hud.Settings.Close();
            yield return new WaitForSeconds(0.3f);

            float left = game.Controller.Game.TimeLeft;
            game.Hud.BackButton.Click();
            game.TickClock(10f);
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(left, game.Controller.Game.TimeLeft, 1e-4f, "...nor on the menu");
            StringAssert.Contains(GC26.Lines.UI.MenuView.ClockText(left), game.Menu.ContinueButton.Text, "Continue shows the time left");

            game.Menu.Modes.ClassicButton.onClick.Invoke();
            Assert.IsFalse(game.Controller.Game.Timed, "The Classic game is back behind Continue");
            game.Menu.ContinueButton.Click();
            SkipTutorials(game);
            yield return null;
            Assert.IsFalse(game.Hud.ClockShowing);
            Assert.IsTrue(game.Hud.UndoShowing);
            game.Hud.BackButton.Click();
            game.Menu.Modes.TimedButton.onClick.Invoke();
            Assert.AreEqual(left, game.Controller.Game.TimeLeft, 1e-3f, "...and the timed one waits with its time");
        }

        [UnityTest]
        public IEnumerator TheClockStops_WhileMushroomsMove_AndAClearedLineAddsTime()
        {
            SaveTimedGame(score: 0, timeLeft: 100f);
            yield return Launch();
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            game.Menu.Modes.TimedButton.onClick.Invoke();
            game.Menu.ContinueButton.Click();
            SkipTutorials(game);
            yield return null;
            game.Controller.Tap(C(4, 2));
            game.Controller.Tap(C(4, 0));
            Assert.IsFalse(game.Controller.AcceptsInput, "Moving");
            game.TickClock(5f);
            Assert.AreEqual(100f, game.Controller.Game.TimeLeft - 5f, 0.3f, "No time lost during the move (only the +5 s)");
            yield return WaitForIdle(game);
            Assert.AreEqual(105f, game.Controller.Game.TimeLeft, 0.3f, "+5 s for the line");
            yield return new WaitForSeconds(0.15f);
            Assert.IsTrue(game.Hud.TimeAddedShowing, "+5s floats up");
        }

        [UnityTest]
        public IEnumerator Last30Seconds_Tick_ThenTimesUp_AndTheScoreGoesToTheTimedList()
        {
            SaveTimedGame(score: 30, timeLeft: 26f);
            yield return Launch();
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            game.Menu.Modes.TimedButton.onClick.Invoke();
            game.Menu.ContinueButton.Click();
            SkipTutorials(game);
            yield return null;
            game.Sounds.Played.Clear();
            game.TickClock(1f);
            Assert.Contains(LinesSound.Tick, game.Sounds.Played, "Ticking in the last 30 seconds");
            Assert.AreEqual(GC26.Lines.UI.HudView.ClockWarn, game.Hud.ClockColor);

            game.TickClock(30f);
            Assert.IsTrue(game.TimeUpShowing, "Time's up!");
            Assert.IsTrue(game.Controller.Game.IsOver);
            Assert.IsFalse(game.Hud.GameOver.IsOpen, "The pop-up waits a moment");
            game.Hud.BackButton.Click();
            Assert.IsTrue(game.GameScreenShowing, "Leaving waits too");
            yield return new WaitForSeconds(1.6f);
            Assert.IsTrue(game.Hud.GameOver.IsOpen);
            Assert.IsFalse(game.Hud.GameOver.IsZero);

            game.Hud.GameOver.SetName("Speedy");
            game.Hud.GameOver.SaveButton.Click();
            Assert.IsTrue(game.LeaderboardScreen.IsShowing);
            Assert.IsTrue(game.LeaderboardScreen.ShowingTimed, "Opens on the Timed tab");
            Assert.AreEqual(1, game.LeaderboardScreen.RowCount);
            Assert.AreEqual(1, new LeaderboardService(timed: true).Load().Count);
            Assert.AreEqual(0, new LeaderboardService().Load().Count, "Not on the Classic list");
            Assert.AreEqual("Speedy", new LeaderboardService().Load().DefaultName, "The name is remembered for both");

            game.LeaderboardScreen.Tabs.ClassicButton.onClick.Invoke();
            Assert.IsFalse(game.LeaderboardScreen.ShowingTimed);
            Assert.AreEqual(0, game.LeaderboardScreen.RowCount, "The Classic tab: its own list");
            Assert.IsTrue(game.LeaderboardScreen.PlayAgainShowing, "Play again stays");
        }

        [UnityTest]
        public IEnumerator ZeroPoints_TheGameOverPopUp_OffersMenuOrPlayAgain()
        {
            SaveTimedGame(score: 0, timeLeft: 2f);
            yield return Launch();
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            game.Menu.Modes.TimedButton.onClick.Invoke();
            game.Menu.ContinueButton.Click();
            SkipTutorials(game);
            yield return null;
            game.TickClock(3f);
            yield return new WaitForSeconds(1.6f);
            Assert.IsTrue(game.Hud.GameOver.IsOpen);
            Assert.IsTrue(game.Hud.GameOver.IsZero);
            Assert.IsFalse(game.ScorePending);
            game.Hud.GameOver.PlayAgainButton.Click();
            Assert.IsFalse(game.Hud.GameOver.IsOpen);
            Assert.IsTrue(game.Controller.Game.Timed, "A new timed game");
            Assert.AreEqual(180f, game.Controller.Game.TimeLeft);
            Assert.AreEqual(0, new LeaderboardService(timed: true).Load().Count, "0 points weren't saved");
        }
    }
}
