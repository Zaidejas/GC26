using System.Collections;
using GC26.Lines.Core;
using GC26.Lines.Game;
using GC26.Lines.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GC26.Lines.Tests
{
    /// <summary>GDD → Debug panel: 5 quick taps on the version open it, and each tool sets up its test case.</summary>
    public class DebugPanelPlayModeTests : PlayModeTestBase
    {
        static IEnumerator Launch()
        {
            yield return LoadGameScene();
            yield return null;
        }

        static void Open(GameBootstrap game)
        {
            for (int i = 0; i < MenuView.DebugTaps; i++) game.Menu.TapVersion();
        }

        static IEnumerator Use(GameBootstrap game, DebugTool tool)
        {
            if (!game.Hud.DebugTools.IsOpen) Open(game);
            yield return null;
            game.Hud.DebugTools.Choose(tool);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FiveQuickTapsOnTheVersion_OpenThePanel()
        {
            yield return Launch();
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            for (int i = 0; i < MenuView.DebugTaps - 1; i++) game.Menu.TapVersion();
            Assert.IsFalse(game.Hud.DebugTools.IsOpen, "4 taps: nothing");
            game.Menu.TapVersion();
            Assert.IsTrue(game.Hud.DebugTools.IsOpen, "The 5th opens the Debug panel");
            Assert.AreEqual(DebugPanel.Hint, game.Hud.DebugTools.NoteText);
            game.Hud.DebugTools.Close();
        }

        [UnityTest]
        public IEnumerator GameTools_SetUpTheirCase()
        {
            yield return Launch();
            var game = Object.FindAnyObjectByType<GameBootstrap>();

            yield return Use(game, DebugTool.StartAt190);
            Assert.IsTrue(game.GameScreenShowing, "Game tools open the game");
            Assert.IsFalse(game.Hud.DebugTools.IsOpen, "...and close the panel");
            Assert.AreEqual(190, game.Controller.Game.Score);
            Assert.AreEqual(200, game.Controller.Game.NextPuffballAt);
            Assert.AreEqual(BallColor.Red, game.Controller.Game.Board.Get(new Cell(5, 6)), "The 5th red one, below the line");

            SkipTutorials(game);
            game.Hud.BackButton.Click();
            yield return Use(game, DebugTool.RainbowNext);
            Assert.AreEqual(BallColor.Rainbow, game.Controller.Game.Preview.Colors[0]);
            Assert.AreEqual(190, game.Controller.Game.Score, "The same game goes on");
            Assert.IsFalse(game.Controller.Game.TutorialsShown.Contains("rainbow"), "Its tutorial shows again");

            SkipTutorials(game);
            game.Hud.BackButton.Click();
            yield return Use(game, DebugTool.FrozenNext);
            Assert.IsTrue(game.Controller.Game.Preview.Frozen[0]);
            Assert.AreNotEqual(BallColor.Rainbow, game.Controller.Game.Preview.Colors[0], "A rainbow never freezes");

            SkipTutorials(game);
            game.Hud.BackButton.Click();
            yield return Use(game, DebugTool.AddPuffball);
            Assert.AreEqual(1, game.Controller.Game.Puffballs);

            SkipTutorials(game);
            game.Hud.BackButton.Click();
            yield return Use(game, DebugTool.ClockTo35);
            Assert.IsTrue(game.Timed, "Switches to Timed");
            Assert.AreEqual(35f, game.Controller.Game.TimeLeft, 0.5f);
        }

        [UnityTest]
        public IEnumerator GameOverNow_TheThreeCases()
        {
            yield return Launch();
            var game = Object.FindAnyObjectByType<GameBootstrap>();

            yield return Use(game, DebugTool.GameOverTop);
            Assert.IsTrue(game.Hud.GameOver.IsOpen);
            Assert.IsTrue(game.Hud.GameOver.NameField.gameObject.activeSelf, "Top 10: a name is asked");
            Assert.AreEqual(50, game.Hud.GameOver.Score, "An empty board: 50 points");
            game.Hud.GameOver.Close();
            game.ShowMenu();

            yield return Use(game, DebugTool.GameOverShort);
            Assert.IsTrue(game.Hud.GameOver.IsNotTop, "Not top 10: no name, the goal instead");
            Assert.AreEqual(Leaderboard.MaxEntries, game.Scores.Count, "The board was filled first");
            Assert.AreEqual(120, game.Hud.GameOver.Score, "10 below the 10th place (130)");
            game.Hud.GameOver.MenuButton.Click();

            yield return Use(game, DebugTool.GameOverTop);
            Assert.AreEqual(140, game.Hud.GameOver.Score, "A full board: 10 above the 10th place");
            game.Hud.GameOver.Close();
            game.ShowMenu();

            yield return Use(game, DebugTool.GameOverZero);
            Assert.IsTrue(game.Hud.GameOver.IsZero);
            game.Hud.GameOver.MenuButton.Click();
        }

        [UnityTest]
        public IEnumerator SavedDataTools_StayOnTheMenu_AndSayWhatTheyDid()
        {
            yield return Launch();
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            game.Menu.NewGameButton.Click();
            SkipTutorials(game);
            yield return null;
            game.Hud.BackButton.Click();
            Assert.IsTrue(game.Menu.ContinueButton.gameObject.activeSelf, "A game to continue");

            yield return Use(game, DebugTool.ForgetGames);
            Assert.IsTrue(game.Hud.DebugTools.IsOpen, "The panel stays open");
            Assert.AreEqual("Saved games forgotten", game.Hud.DebugTools.NoteText);
            Assert.IsFalse(game.Menu.ContinueButton.gameObject.activeSelf, "Like a first launch");
            Assert.IsTrue(game.Menu.NewGameButton.IsPrimary);
            Assert.IsFalse(game.Saves.HasSave);

            yield return Use(game, DebugTool.FillLeaderboards);
            Assert.AreEqual(Leaderboard.MaxEntries, game.Scores.Count);
            Assert.AreEqual(Leaderboard.MaxEntries, game.TimedScores.Count, "Both modes");
            Assert.AreEqual(130, game.Scores.TenthScore);
            yield return Use(game, DebugTool.ClearLeaderboards);
            Assert.AreEqual(0, game.Scores.Count);
            Assert.AreEqual(0, game.TimedScores.Count);
            Assert.AreEqual("Leaderboards cleared", game.Hud.DebugTools.NoteText);
            game.Hud.DebugTools.Close();
        }
    }
}
