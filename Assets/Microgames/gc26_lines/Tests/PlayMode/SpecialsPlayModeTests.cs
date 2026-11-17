using System.Collections;
using GC26.Lines.Core;
using GC26.Lines.Game;
using GC26.Lines.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GC26.Lines.Tests
{
    /// <summary>
    /// Tasks L14–L19 in the real scene: the puffball (tutorial, aiming, burst, bubble, banner, rescue), the rainbow and
    /// ice announcements, a tap on a frozen mushroom, Pause and Settings hiding the board, How to play's back arrow and
    /// Continue's stopwatch.
    /// </summary>
    public class SpecialsPlayModeTests : PlayModeTestBase
    {
        static Cell C(int column, int row) => new Cell(column, row);

        static IEnumerator Launch()
        {
            yield return LoadGameScene();
            yield return null;
        }

        // A saved game: four red mushrooms in the top row and a fifth below (move (4,1) → (4,0) for a line of 5).
        static GameState NearLine(int score, bool timed = false, int puffballs = 0, Preview next = null, params string[] shown)
        {
            var board = new Board();
            foreach (var c in new[] { C(0, 0), C(1, 0), C(2, 0), C(3, 0), C(4, 1) }) board.Place(c, BallColor.Red);
            board.Place(C(2, 5), BallColor.Blue);
            board.Place(C(3, 5), BallColor.Blue, isFrozen: true);
            board.Place(C(8, 8), BallColor.Green);
            next = next ?? new Preview(new[] { BallColor.Green, BallColor.Yellow, BallColor.Purple }, new[] { C(6, 6), C(7, 7), C(6, 8) });
            var game = new GameState(board, next, score, new Spawner(5), timed: timed, timeLeft: 120f, puffballs: puffballs);
            foreach (var key in shown) game.TutorialsShown.Add(key);
            return game;
        }

        static IEnumerator ContinueWith(GameState saved)
        {
            new SaveService(timed: saved.Timed).Save(GameSave.From(saved));
            yield return Launch();
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            if (saved.Timed) game.Menu.Modes.TimedButton.onClick.Invoke();
            game.Menu.ContinueButton.Click();
            yield return new WaitForSeconds(1.3f); // the entrance, then the tutorials check
        }

        static IEnumerator WaitForIdle(GameBootstrap game)
        {
            float waited = 0f;
            while (game.Controller.State != TurnState.Idle && waited < 3f) { waited += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(0.4f);
        }

        static Vector2 Px(GameBootstrap game, Cell c) => game.Layout.WorldToScreen(game.Layout.BoardCenterWorld + (Vector2)GC26.Lines.View.BoardView.CellToLocal(c));

        [UnityTest]
        public IEnumerator TheFirstPuffball_TutorialThenAimMoveAndBurst()
        {
            yield return ContinueWith(NearLine(195, shown: new[] { "ice" }));
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            Assert.IsFalse(game.Coach.IsShowing, "nothing yet");
            game.Controller.Tap(C(4, 1));
            game.Controller.Tap(C(4, 0));
            yield return WaitForIdle(game);
            Assert.AreEqual(1, game.Controller.Game.Puffballs, "200 points: a puffball");
            Assert.AreEqual("super", game.TutorialShowing, "the first one: the tutorial");
            Assert.AreEqual("You earned a puffball!", game.Coach.Title);
            Assert.IsTrue(game.Coach.SkipShowing);
            Assert.IsFalse(game.Hud.Banner.IsShowing, "the tutorial instead of the banner");

            game.Coach.TapAt(new Vector2(5f, 5f)); // a tap off the lit spot does nothing
            Assert.AreEqual(0, game.TutorialStep);
            game.Coach.TapAt(game.Coach.Hole.center);
            Assert.AreEqual(1, game.TutorialStep);
            Assert.IsTrue(game.Controller.Aiming, "aiming on the busiest spot");
            Assert.AreEqual("Where to burst?", game.Coach.Title);
            Assert.IsTrue(game.BoardView.Aim.HasValue);

            game.Coach.TapAt(Px(game, C(2, 6)));
            Assert.AreEqual(C(2, 6), game.Controller.AimCell, "a tap on another cell moves it");
            Assert.AreEqual(1, game.Controller.Game.Puffballs, "...without bursting");
            game.Coach.TapAt(Px(game, C(2, 6)));
            Assert.IsFalse(game.Coach.IsShowing);
            Assert.AreEqual(0, game.Controller.Game.Puffballs, "a tap on the lit spot bursts");
            yield return WaitForIdle(game);
            Assert.IsTrue(game.Controller.Game.Board.IsEmpty(C(2, 5)) && game.Controller.Game.Board.IsEmpty(C(3, 5)), "the blue ones, the frozen one too");
            Assert.IsNull(game.BoardView.BallAt(C(3, 5)));
            Assert.Contains(GC26.Lines.Audio.LinesSound.Boom, game.Sounds.Played);
        }

        [UnityTest]
        public IEnumerator NoPuffballYet_TheBubbleSaysHowFar_AndTheSecondOneGetsTheBanner()
        {
            yield return ContinueWith(NearLine(190, shown: new[] { "super", "ice" }));
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            game.Hud.Puffball.Click();
            Assert.IsFalse(game.Controller.Aiming);
            Assert.IsTrue(game.Hud.Bubble.IsShowing);
            Assert.AreEqual("Puffball in 10 points", game.Hud.Bubble.Title);
            Assert.IsTrue(game.Hud.Bubble.BarShowing);

            game.Controller.Tap(C(4, 1));
            game.Controller.Tap(C(4, 0));
            yield return WaitForIdle(game);
            Assert.AreEqual(1, game.Controller.Game.Puffballs);
            Assert.IsNull(game.TutorialShowing, "already shown in this game");
            Assert.IsTrue(game.Hud.Banner.IsShowing, "A puffball for you!");
            Assert.AreEqual(1, game.Hud.Puffball.Count);

            game.Hud.Puffball.Click();
            Assert.IsTrue(game.Controller.Aiming);
            Assert.AreEqual("Where to burst?", game.Hud.Bubble.Title);
            game.Hud.Puffball.Click();
            Assert.IsFalse(game.Controller.Aiming, "the button again cancels");
            Assert.AreEqual(1, game.Controller.Game.Puffballs);
        }

        [UnityTest]
        public IEnumerator TheFirstRainbowAndIce_AreAnnounced_GotItCloses_OncePerGame()
        {
            var next = new Preview(new[] { BallColor.Rainbow, BallColor.Yellow, BallColor.Purple }, new[] { C(6, 6), C(7, 7), C(6, 8) });
            yield return ContinueWith(NearLine(10, next: next));
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            Assert.AreEqual("rainbow", game.TutorialShowing, "the rainbow in NEXT");
            Assert.AreEqual("A rainbow mushroom: it fits any line", game.Coach.Title);
            Assert.IsTrue(game.Coach.GotItShowing);
            Assert.IsTrue(game.InputBlocked, "the board waits");
            game.Coach.TapAt(new Vector2(5f, 5f));
            Assert.IsTrue(game.Coach.IsShowing, "only Got it closes it");
            game.Coach.GotItButton.Click();
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual("ice", game.TutorialShowing, "then the frozen one on the board");
            game.Coach.GotItButton.Click();
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(game.Coach.IsShowing);
            CollectionAssert.IsSubsetOf(new[] { "rainbow", "ice" }, game.Controller.Game.TutorialsShown);
            game.CheckTutorials();
            Assert.IsFalse(game.Coach.IsShowing, "once per game");
        }

        [UnityTest]
        public IEnumerator TappingAFrozenMushroom_SaysWhy()
        {
            yield return ContinueWith(NearLine(10, shown: new[] { "ice" }));
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            game.Controller.Tap(C(3, 5));
            Assert.AreEqual(TurnState.Idle, game.Controller.State, "not selected");
            Assert.IsTrue(game.Hud.Alert.IsShowing);
            Assert.AreEqual("Make a line through it to break the ice", game.Hud.Alert.SecondLine);
            Assert.IsTrue(game.BoardView.BallAt(C(3, 5)).Frozen);
            Assert.IsTrue(game.BoardView.BallAt(C(3, 5)).Ice.enabled);
        }

        [UnityTest]
        public IEnumerator StuckWithAPuffball_TheRescue_EndGameEndsIt()
        {
            var board = new Board();
            for (int i = 0; i < 81; i++) board.Place(C(i % 9, i / 9), BallColors.All[(i * 3 + i / 9) % 7]);
            var stuck = new GameState(board, new Preview(new[] { BallColor.Red, BallColor.Red, BallColor.Red }, new Cell[0]), 40, new Spawner(5), puffballs: 1);
            stuck.TutorialsShown.Add("super");
            yield return ContinueWith(stuck);
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            Assert.AreEqual("rescue", game.TutorialShowing);
            Assert.AreEqual("The board is full!", game.Coach.Title);
            Assert.AreEqual("End game", game.Coach.SkipButton.Text);
            game.Coach.TapAt(game.Coach.Hole.center);
            Assert.IsTrue(game.Controller.Aiming, "tap the puffball: aim");
            game.Hud.Puffball.Click(); // cancel: the offer comes back
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual("rescue", game.TutorialShowing);
            game.Coach.SkipButton.Click();
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(game.Controller.Game.IsOver);
            Assert.IsTrue(game.Hud.GameOver.IsOpen);
            Assert.AreEqual("Final score", game.Hud.GameOver.NoteText, "a full board");
            Assert.IsFalse(game.Hud.GameOver.LabelShowing, "L19: no label above the name");
        }

        [UnityTest]
        public IEnumerator Pause_StopsTheClock_AndHidesTheBoard_SettingsToo()
        {
            yield return ContinueWith(NearLine(10, timed: true, shown: new[] { "ice" }));
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            Assert.IsTrue(game.Hud.PauseShowing);
            Assert.IsFalse(game.Hud.UndoShowing);
            game.Hud.PauseButton.Click();
            Assert.IsTrue(game.Paused);
            Assert.IsTrue(game.Hud.Dialog.IsSingle, "Paused, with Resume only");
            Assert.IsTrue(game.BoardView.PiecesHidden, "no free thinking time");
            float left = game.Controller.Game.TimeLeft;
            game.TickClock(5f);
            Assert.AreEqual(left, game.Controller.Game.TimeLeft, 1e-4f, "the clock stops");
            game.Hud.Dialog.ConfirmButton.Click();
            Assert.IsFalse(game.Paused);
            Assert.IsFalse(game.BoardView.PiecesHidden);

            yield return new WaitForSeconds(0.4f);
            game.Hud.SettingsButton.Click();
            Assert.IsTrue(game.BoardView.PiecesHidden, "Timed: Settings hides the board too");
            game.Hud.Settings.Close();
            yield return new WaitForSeconds(0.4f);
            Assert.IsFalse(game.BoardView.PiecesHidden);

            game.Hud.SettingsButton.Click();
            game.Hud.Settings.HowToPlayButton.Click();
            Assert.IsTrue(game.HowToPlay.BackIsArrow, "L19: from the game, a back arrow");
            game.HowToPlay.BackButton.Click();
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(game.GameScreenShowing);
            game.Hud.BackButton.Click();
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(game.Menu.ContinueClockShowing, "L18: Continue shows the stopwatch before the time");
            game.Menu.HowToPlayButton.Click();
            Assert.IsFalse(game.HowToPlay.BackIsArrow, "from the menu, the house");
        }
    }
}
