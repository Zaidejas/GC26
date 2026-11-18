using System.Collections;
using System.Linq;
using GC26.Lines.Audio;
using GC26.Lines.Core;
using GC26.Lines.Game;
using GC26.Lines.Haptics;
using GC26.Lines.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GC26.Lines.Tests
{
    /// <summary>L04 (GDD → Scoring): passing the best score during play — fireworks, "New best!" and the crown.</summary>
    public class BestScorePlayModeTests : PlayModeTestBase
    {
        static Cell C(int column, int row) => new Cell(column, row);

        // The best finished game scored <paramref name="best"/>; the saved game has <paramref name="score"/> points and
        // four red mushrooms in the top row, so moving the red one at (4, 2) up to (4, 0) clears a line of 5 (10 points).
        void Prepare(int best, int score)
        {
            var scores = new Leaderboard();
            scores.Record(best, "Anna", 1000);
            new LeaderboardService().Save(scores);
            var board = new Board();
            foreach (var c in new[] { C(0, 0), C(1, 0), C(2, 0), C(3, 0), C(4, 2) }) board.Place(c, BallColor.Red);
            board.Place(C(8, 8), BallColor.Blue);
            var preview = new Preview(new[] { BallColor.Green, BallColor.Yellow, BallColor.Purple }, new[] { C(6, 6), C(7, 7), C(6, 8) });
            new SaveService().Save(GameSave.From(new GameState(board, preview, score, new Spawner(5))));
        }

        static IEnumerator Launch()
        {
            yield return LoadGameScene();
            yield return null;
        }

        static IEnumerator WaitForIdle(GameBootstrap game)
        {
            float waited = 0f;
            while (game.Controller.State != TurnState.Idle && waited < 3f) { waited += Time.deltaTime; yield return null; }
        }

        [UnityTest]
        public IEnumerator PassingTheBest_Fireworks_NewBest_Crown_Once()
        {
            Prepare(best: 12, score: 5);
            yield return Launch();
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            game.Menu.ContinueButton.Click();
            SkipTutorials(game);
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(12, game.BestAtStart);
            Assert.IsFalse(game.Hud.CrownShowing, "5 points: below the best");

            game.Controller.Tap(C(4, 2));
            game.Controller.Tap(C(4, 0));
            yield return WaitForIdle(game);
            Assert.AreEqual(15, game.Controller.Game.Score);
            Assert.IsTrue(game.Hud.CrownShowing, "Above the best: the crown");
            Assert.IsTrue(game.Fireworks.Playing, "Fireworks");
            yield return new WaitForSeconds(0.4f); // "New best!" fades in
            Assert.IsTrue(game.Hud.NewBestShowing);
            Assert.AreEqual("New best!", game.Hud.NewBestText);
            Assert.Contains(LinesSound.Record, game.Sounds.Played);
            CollectionAssert.AreEqual(new[] { LinesFeel.Clear, LinesFeel.Success }, game.Haptics.Played);
            Assert.AreEqual(game.Hud.ScoreMiddleY, game.Hud.CrownCentre.y, 1f, "Level with the middle of the digits");

            yield return new WaitForSeconds(2.2f);
            Assert.IsFalse(game.Fireworks.Playing, "The fireworks end");
            Assert.AreEqual(0, game.Fireworks.VisibleSparks);
            Assert.IsFalse(game.Hud.NewBestShowing, "New best! fades");
            Assert.IsTrue(game.Hud.CrownShowing, "The crown stays");

            // Undo below the best: the crown goes quietly; passing it again doesn't celebrate twice.
            game.Hud.UndoButton.Click();
            Assert.IsFalse(game.Hud.CrownShowing);
            game.Controller.Tap(C(4, 2));
            game.Controller.Tap(C(4, 0));
            yield return WaitForIdle(game);
            Assert.IsTrue(game.Hud.CrownShowing, "The crown is back");
            Assert.IsFalse(game.Fireworks.Playing, "No second show");
            Assert.AreEqual(1, game.Haptics.Played.Count(f => f == LinesFeel.Success));

            // Leaving the game stops any fireworks.
            game.Fireworks.Play();
            game.Hud.BackButton.Click();
            Assert.IsFalse(game.Fireworks.Playing);
        }

        [UnityTest]
        public IEnumerator ContinuingAGameAboveTheBest_ShowsTheCrown_WithoutFireworks()
        {
            Prepare(best: 12, score: 20);
            yield return Launch();
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            game.Menu.ContinueButton.Click();
            SkipTutorials(game);
            yield return null;
            Assert.IsTrue(game.Hud.CrownShowing);
            Assert.IsFalse(game.Fireworks.Playing);
            Assert.IsFalse(game.Hud.NewBestShowing);
        }

        [UnityTest]
        public IEnumerator NoFinishedGamesYet_NoBest_NothingHappens()
        {
            yield return Launch();
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            game.Menu.NewGameButton.Click();
            SkipTutorials(game);
            yield return null;
            Assert.AreEqual(0, game.BestAtStart);
            Assert.IsFalse(game.Hud.CrownShowing);
        }
    }
}
