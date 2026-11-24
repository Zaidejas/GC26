using GC26.Lines.Core;
using GC26.Lines.UI;
using GC26.Lines.View;
using NUnit.Framework;
using UnityEngine;

namespace GC26.Lines.Tests
{
    /// <summary>L09 (GDD → Timed mode): the clock's rules, saving a timed game, and the pieces on screen.</summary>
    public class TimedModeTests
    {
        static Cell C(int column, int row) => new Cell(column, row);

        // Four red mushrooms in the top row and one more at (4, 2): moving it to (4, 0) clears a line of 5.
        static GameState NearlyALine(bool timed, float timeLeft = 100f)
        {
            var board = TestBoards.FromRows(
                "RRRR.....",
                ".........",
                "....R....",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                "........B");
            var preview = new Preview(new[] { BallColor.Green, BallColor.Yellow, BallColor.Purple }, new[] { C(6, 6), C(7, 7), C(6, 8) });
            return new GameState(board, preview, 0, new Spawner(5), timed: timed, timeLeft: timeLeft);
        }

        // ---- Rules ----

        [Test]
        public void ATimedGame_StartsWithThreeMinutes_AndHasNoUndo()
        {
            var game = GameState.NewGame(3, timed: true);
            Assert.IsTrue(game.Timed);
            Assert.AreEqual(180f, game.TimeLeft);
            var classic = GameState.NewGame(3);
            Assert.IsFalse(classic.Timed);
            Assert.AreEqual(0f, classic.TimeLeft);

            var timed = NearlyALine(timed: true);
            timed.TryMove(C(8, 8), C(8, 7)); // a plain move: new mushrooms come
            Assert.IsFalse(timed.CanUndo, "No Undo in Timed mode");
            var normal = NearlyALine(timed: false);
            normal.TryMove(C(8, 8), C(8, 7));
            Assert.IsTrue(normal.CanUndo, "Classic keeps Undo");
        }

        [Test]
        public void EveryClearedLine_AddsFiveSeconds()
        {
            var game = NearlyALine(timed: true, timeLeft: 42f);
            var result = game.TryMove(C(4, 2), C(4, 0));
            Assert.AreEqual(1, result.LinesFromMove.Count);
            Assert.AreEqual(5f, result.TimeAdded);
            Assert.AreEqual(47f, game.TimeLeft);
            var classic = NearlyALine(timed: false);
            Assert.AreEqual(0f, classic.TryMove(C(4, 2), C(4, 0)).TimeAdded, "Classic has no clock");
        }

        [Test]
        public void TheClock_RunsDown_AndAtZeroTheGameIsOver()
        {
            var game = NearlyALine(timed: true, timeLeft: 3f);
            int overs = 0;
            game.OnGameOver += _ => overs++;
            Assert.IsFalse(game.AdvanceClock(2f));
            Assert.AreEqual(1f, game.TimeLeft, 1e-4f);
            Assert.IsFalse(game.IsOver);
            Assert.IsTrue(game.AdvanceClock(5f), "This call ran the time out");
            Assert.AreEqual(0f, game.TimeLeft);
            Assert.IsTrue(game.IsOver);
            Assert.IsTrue(game.TimeUp);
            Assert.AreEqual(1, overs);
            Assert.IsFalse(game.AdvanceClock(1f), "Nothing more once it's over");
            Assert.IsFalse(NearlyALine(timed: false).AdvanceClock(10f), "Classic has no clock");
        }

        [Test]
        public void ATimedGame_IsSavedWithItsTimeLeft()
        {
            var game = NearlyALine(timed: true, timeLeft: 81.5f);
            var copy = JsonUtility.FromJson<GameSave>(JsonUtility.ToJson(GameSave.From(game))).ToGameState();
            Assert.IsTrue(copy.Timed);
            Assert.AreEqual(81.5f, copy.TimeLeft, 1e-3f);
            var old = JsonUtility.FromJson<GameSave>(JsonUtility.ToJson(GameSave.From(NearlyALine(timed: false)))).ToGameState();
            Assert.IsFalse(old.Timed, "Classic saves (and older ones, without these fields) stay Classic");
        }

        // ---- 0 points ----

        [Test]
        public void ZeroPoints_NeverGoOnTheLeaderboard()
        {
            var board = new Leaderboard();
            Assert.AreEqual(-1, board.Add(0, "Anna", 1));
            Assert.AreEqual(0, board.Count);
            board.entries.Add(new ScoreEntry { score = 0, name = "Old", time = 2 }); // saved before the rule
            board.Add(10, "Bob", 3);
            board.Tidy();
            Assert.AreEqual(1, board.Count, "Old 0-point entries are dropped");
        }

        // ---- On screen ----

        [Test]
        public void ClockText_MinutesAndSeconds_RoundedUp()
        {
            Assert.AreEqual("3:00", MenuView.ClockText(180f));
            Assert.AreEqual("1:05", MenuView.ClockText(64.2f));
            Assert.AreEqual("0:09", MenuView.ClockText(8.01f));
            Assert.AreEqual("0:00", MenuView.ClockText(0f));
            Assert.AreEqual("0:00", MenuView.ClockText(-3f));
        }

        Camera camera;
        HudView hud;
        GameOverDialog dialog;

        [TearDown]
        public void TearDown()
        {
            Tween.KillAll();
            if (hud != null) Object.DestroyImmediate(hud.gameObject);
            if (camera != null) Object.DestroyImmediate(camera.gameObject);
        }

        HudView MakeHud(out BoardLayout layout)
        {
            camera = new GameObject("Camera").AddComponent<Camera>();
            hud = HudView.Create(camera);
            layout = BoardLayout.Compute(new Vector2(1080, 2400), new Rect(0, 63, 1080, 2400 - 132 - 63));
            hud.ApplyLayout(layout);
            return hud;
        }

        [Test]
        public void Clock_AboveScoreAndNext_PauseTakesUndosPlace()
        {
            var h = MakeHud(out var layout);
            Assert.IsFalse(h.ClockShowing, "Classic: no clock");
            Assert.IsTrue(h.UndoShowing);
            h.SetTimed(true);
            Assert.IsTrue(h.ClockShowing);
            Assert.IsFalse(h.UndoShowing, "Timed: no Undo");
            Assert.IsTrue(h.PauseShowing, "L18: Pause in Undo's place");
            Assert.AreEqual(((RectTransform)h.UndoButton.transform).anchoredPosition, ((RectTransform)h.PauseButton.transform).anchoredPosition);
            Assert.Greater(h.ResetCentre.x, layout.Actions.center.x, "Reset stays on the right; the puffball is in the middle");
            Assert.AreEqual(layout.Actions.center.x, h.Puffball.Rect.anchoredPosition.x, 0.5f);
            Assert.Greater(h.ClockCentre.y, layout.Hud.y + h.ScoreLabelY, "Above the SCORE / NEXT labels");
            Assert.LessOrEqual(h.ClockCentre.y, layout.BackButton.center.y + 0.5f, "Never above the house and gear's line");
            Assert.AreEqual(layout.Board.center.x, h.ClockCentre.x, 0.5f, "The digits are centred on the screen");
            Assert.Less(h.RingCentre.x, h.ClockCentre.x - 5f * layout.Unit, "The spore ring hangs to the left of the digits");
            Assert.AreEqual(h.ClockCentre.y, h.RingCentre.y, 0.5f, "...level with them");
            Assert.LessOrEqual(h.ClockCentre.y, layout.SafeArea.yMax, "Inside the safe area (clear of the camera cut-out)");
            h.SetTimed(false);
            Assert.IsTrue(h.UndoShowing, "Back to Classic: Undo returns");
            Assert.IsFalse(h.PauseShowing);
        }

        [Test]
        public void Clock_DigitsAllTheSameWidth_SoTheTimeDoesNotShift()
        {
            var h = MakeHud(out _);
            h.SetTimed(true);
            h.SetClock(71f, running: true); // 1:11, the narrowest digits
            float narrow = h.ClockTextWidth;
            Assert.AreEqual("1:11", h.ClockReading);
            h.SetClock(178f, running: true); // 2:58
            Assert.AreEqual(narrow, h.ClockTextWidth, 0.5f);
            h.SetClock(40f, running: true); // 0:40
            Assert.AreEqual(narrow, h.ClockTextWidth, 0.5f);
        }

        [Test]
        public void Clock_TurnsYellowAt30_AndRedAt10()
        {
            var h = MakeHud(out _);
            h.SetTimed(true);
            h.SetClock(45f, running: true);
            Assert.AreEqual("0:45", h.ClockReading);
            Assert.AreEqual(Palette.Clock, h.ClockColor, "Cool white, never gold (gold means points)");
            h.SetClock(25f, running: true);
            Assert.AreEqual(HudView.ClockWarn, h.ClockColor);
            h.SetClock(9f, running: true);
            Assert.AreEqual(HudView.ClockLow, h.ClockColor);
            h.ShowTimeAdded(5f);
            Assert.IsTrue(h.RingFlashing, "The spores flash mint");
            for (int i = 0; i < 20; i++) Tween.Tick(0.01f);
            Assert.IsTrue(h.TimeAddedShowing, "+5s floats up");
            for (int i = 0; i < 100; i++) Tween.Tick(0.01f);
            Assert.IsFalse(h.TimeAddedShowing);
        }

        [Test]
        public void Clock_SporesGoDarkOneEvery15Seconds()
        {
            Assert.AreEqual(12, HudView.SporesLit(180f), "All lit at 3:00");
            Assert.AreEqual(12, HudView.SporesLit(240f), "...and above (time added)");
            Assert.AreEqual(12, HudView.SporesLit(166f));
            Assert.AreEqual(11, HudView.SporesLit(165f));
            Assert.AreEqual(1, HudView.SporesLit(1f));
            Assert.AreEqual(0, HudView.SporesLit(0f));
            var h = MakeHud(out _);
            h.SetTimed(true);
            h.SetClock(100f, running: true);
            Assert.AreEqual(7, h.SporesLitNow);
        }

        [Test]
        public void GameOverWithZeroPoints_NoNameField_MenuOrPlayAgain()
        {
            var h = MakeHud(out _);
            dialog = h.GameOver;
            int menu = 0, again = 0;
            dialog.MenuChosen += () => menu++;
            dialog.PlayAgainChosen += () => again++;
            dialog.Show(0, "Player1");
            Assert.IsTrue(dialog.IsZero);
            Assert.IsFalse(dialog.NameField.gameObject.activeSelf, "No name to ask for");
            Assert.IsFalse(dialog.SaveButton.gameObject.activeSelf);
            Assert.AreEqual("Clear a line to get on the leaderboard.", dialog.HintText);
            dialog.MenuButton.Click();
            dialog.PlayAgainButton.Click();
            Assert.AreEqual((1, 1), (menu, again));
            Assert.IsTrue(dialog.MenuButton.HasLeadingIcon, "Menu has the house icon");
            Assert.IsTrue(dialog.PlayAgainButton.HasLeadingIcon, "Play again has Reset's circular arrow");
            dialog.Show(40, "Player1");
            Assert.IsFalse(dialog.IsZero);
            Assert.IsTrue(dialog.NameField.gameObject.activeSelf, "Points: the name field is back");
        }

        [Test]
        public void GameOver_WalledInByIce_SaysNoMovesLeft_AndHasNoLabel()
        {
            var h = MakeHud(out _);
            h.GameOver.Show(40, "Player1", noMovesLeft: true);
            Assert.AreEqual("No moves left. Final score", h.GameOver.NoteText);
            Assert.IsFalse(h.GameOver.LabelShowing, "L19: no label above the name field");
            h.GameOver.Show(40, "Player1");
            Assert.AreEqual("Final score", h.GameOver.NoteText);
        }

        [Test]
        public void ModeBadges_TellWhichWasChosen()
        {
            camera = new GameObject("Camera").AddComponent<Camera>();
            hud = HudView.Create(camera); // only as a parent with a canvas
            var badges = ModeBadges.Create((RectTransform)hud.transform, sideLabels: false);
            bool? chosen = null;
            badges.Chosen += timed => chosen = timed;
            badges.SetChosen(false);
            badges.ClassicButton.onClick.Invoke();
            Assert.IsNull(chosen, "The chosen one again: nothing changes");
            badges.TimedButton.onClick.Invoke();
            Assert.AreEqual(true, chosen);
        }
    }
}
