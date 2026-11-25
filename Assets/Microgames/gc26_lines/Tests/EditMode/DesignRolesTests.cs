using GC26.Lines.Core;
using GC26.Lines.UI;
using GC26.Lines.View;
using NUnit.Framework;
using UnityEngine;

namespace GC26.Lines.Tests
{
    /// <summary>
    /// The design review of Nov 2026 (GDD → Art → Colours and Fonts; Main menu; Scoring): colours by job, two fonts,
    /// the bigger house and gear, screen titles, and the game-over top-10 check.
    /// </summary>
    public class DesignRolesTests
    {
        Camera camera;
        HudView hud;
        LeaderboardView leaderboard;
        HowToPlayView howTo;
        static readonly Vector2 Screen = new Vector2(1080, 1920);
        static BoardLayout Layout => BoardLayout.Compute(Screen, new Rect(Vector2.zero, Screen));

        [SetUp]
        public void SetUp()
        {
            camera = new GameObject("Camera").AddComponent<Camera>();
            hud = HudView.Create(camera);
            hud.ApplyLayout(Layout);
        }

        [TearDown]
        public void TearDown()
        {
            Tween.KillAll();
            Texts.Set(Language.English);
            Object.DestroyImmediate(hud.gameObject);
            if (leaderboard != null) Object.DestroyImmediate(leaderboard.gameObject);
            if (howTo != null) Object.DestroyImmediate(howTo.gameObject);
            Object.DestroyImmediate(camera.gameObject);
        }

        static Leaderboard Full(int lowest)
        {
            var board = new Leaderboard();
            for (int i = 0; i < Leaderboard.MaxEntries; i++) board.Add(lowest + 10 * i, "P" + i, 1000L * i);
            return board;
        }

        [Test]
        public void Leaderboard_QualifiesOnlyAboveTheTenth()
        {
            var board = new Leaderboard();
            Assert.IsFalse(board.Qualifies(0), "0 points never");
            Assert.IsTrue(board.Qualifies(5), "Free places: any points");
            Assert.AreEqual(-1, board.TenthScore);
            board = Full(340);
            Assert.AreEqual(340, board.TenthScore);
            Assert.IsFalse(board.Qualifies(340), "Equal to the 10th loses: older scores win ties");
            Assert.IsFalse(board.Qualifies(100));
            Assert.IsTrue(board.Qualifies(341));
        }

        [Test]
        public void NotTopHint_BothLanguages()
        {
            Assert.AreEqual("Not enough for the top 10: 10th place has 340 points.", Texts.NotTopHint(340));
            Assert.AreEqual("Not enough for the top 10: 10th place has 1 point.", Texts.NotTopHint(1));
            Texts.Set(Language.Russian);
            Assert.AreEqual("Не хватило для топ-10: у 10-го места 340 очков.", Texts.NotTopHint(340));
            Assert.AreEqual("Не хватило для топ-10: у 10-го места 21 очко.", Texts.NotTopHint(21));
            Assert.AreEqual("Не хватило для топ-10: у 10-го места 42 очка.", Texts.NotTopHint(42));
            Assert.AreEqual("Ещё раз", Texts.Get("playAgain"));
        }

        [Test]
        public void GameOver_ScoreThatCantGetIn_NoNameField_ShowsTheGoal()
        {
            var dialog = hud.GameOver;
            dialog.Show(120, "Player1", tenthScore: 340);
            Assert.IsTrue(dialog.IsNotTop);
            Assert.IsFalse(dialog.IsZero);
            Assert.IsFalse(dialog.NameField.gameObject.activeSelf, "Nothing to save: no name asked");
            Assert.IsTrue(dialog.MenuButton.gameObject.activeSelf);
            Assert.IsTrue(dialog.PlayAgainButton.gameObject.activeSelf);
            Assert.AreEqual("Not enough for the top 10: 10th place has 340 points.", dialog.HintText);
            dialog.Show(400, "Player1");
            Assert.IsFalse(dialog.IsNotTop);
            Assert.IsTrue(dialog.NameField.gameObject.activeSelf, "A score that gets in asks for a name");
        }

        [Test]
        public void Buttons_ColouredByTheirJob()
        {
            Assert.IsTrue(hud.ResetButton.IsDanger, "Reset is ember red");
            Assert.AreEqual(Palette.EmberText, hud.ResetButton.LabelColor);
            Assert.IsFalse(hud.UndoButton.IsDanger);
            Assert.AreEqual(Palette.ControlText, hud.UndoButton.LabelColor, "Ordinary buttons: silver-lilac");
            Assert.AreEqual(Palette.ControlLine, hud.UndoButton.OutlineColor);
            Assert.AreEqual(Palette.Nav, hud.BackButton.IconColor, "The house is quiet blue-grey");
            Assert.AreEqual(Palette.Nav, hud.SettingsButton.IconColor, "...and the gear");
            Assert.AreEqual(UiFonts.Body, hud.UndoButton.Label.font, "Buttons in Nunito ExtraBold, not the title font");

            hud.Dialog.Show("Start a new game?", "", "Yes", () => { });
            Assert.IsTrue(hud.Dialog.ConfirmButton.IsDanger, "Start over: the Yes is ember red");
            hud.Dialog.Close();
            hud.Dialog.ShowSingle("Paused", "", "Resume", () => { });
            Assert.IsFalse(hud.Dialog.ConfirmButton.IsDanger, "Resume stays the gold main button");
            Assert.IsTrue(hud.Dialog.ConfirmButton.IsPrimary);
        }

        [Test]
        public void HouseAndGear_BiggerButTheirMiddlesStayPut()
        {
            var l = Layout;
            float u = l.Unit;
            Assert.AreEqual(12f * u, l.BackButton.width, 0.01f, "About 47 pt to tap");
            Assert.AreEqual(9f * u, l.BackButton.center.x - (l.SafeArea.x + (l.SafeArea.width - 100f * u) / 2f), 0.01f);
            Assert.AreEqual(8.5f * u, l.SafeArea.yMax - l.BackButton.center.y, 0.01f);
            Assert.AreEqual(l.Board.center.x - l.BackButton.center.x, l.SettingsButton.center.x - l.Board.center.x, 0.01f, "The gear mirrors the house");
            Assert.AreEqual(PillButton.HouseIconSize * u, hud.BackButton.IconPixels, 0.01f);
            Assert.AreEqual(PillButton.GearIconSize * u, hud.SettingsButton.IconPixels, 0.01f);
        }

        [Test]
        public void ScreenTitles_OnTheHousesLine_TabsUnderIt()
        {
            var l = Layout;
            leaderboard = LeaderboardView.Create(camera);
            leaderboard.ApplyLayout(l);
            leaderboard.Show(new Leaderboard());
            Assert.AreEqual("Leaderboard", leaderboard.ScreenTitle.text);
            var title = leaderboard.ScreenTitle.rectTransform.anchoredPosition;
            Assert.AreEqual(l.BackButton.center.y, title.y, 0.5f);
            Assert.AreEqual(l.SafeArea.center.x, title.x, 0.5f, "Centred on the screen");
            Assert.Less(leaderboard.TabsCentre.y, l.BackButton.y, "Classic / Timed under the title");

            howTo = HowToPlayView.Create(camera);
            howTo.ApplyLayout(l);
            howTo.Show(PieceSet.Balls());
            Assert.AreEqual("How to play", howTo.ScreenTitle.text);
            Assert.AreEqual(l.BackButton.center.y, howTo.ScreenTitle.rectTransform.anchoredPosition.y, 0.5f);
            Assert.AreEqual(leaderboard.ScreenTitle.fontSize, howTo.ScreenTitle.fontSize, 0.01f, "Both titles the same size");
            Assert.AreSame(leaderboard.ScreenTitle.font, howTo.ScreenTitle.font, "...and font");
            Assert.AreEqual(leaderboard.ScreenTitle.fontStyle, howTo.ScreenTitle.fontStyle);
            Assert.AreEqual(6f * l.Unit, l.BackButton.y - (leaderboard.TabsCentre.y + 4.5f * l.Unit), 0.5f, "6 units between the title's row and the tabs");
            Texts.Set(Language.Russian);
            howTo.RefreshTexts();
            Assert.AreEqual("Как играть", howTo.ScreenTitle.text);
        }

        [Test]
        public void Points_GoldLetters_ClockCoolWhite()
        {
            var score = hud.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
            var number = System.Array.Find(score, t => t.name == "Score");
            Assert.IsTrue(number.enableVertexGradient, "The score's letters are gold");
            Assert.AreEqual(Palette.ScoreTop, number.colorGradient.topLeft);
            var label = System.Array.Find(score, t => t.name == "Score label");
            Assert.AreEqual(UiFonts.Body, label.font, "SCORE / NEXT in Nunito ExtraBold");
            hud.SetTimed(true);
            hud.SetClock(100f, running: true);
            Assert.AreEqual(Palette.Clock, hud.ClockColor);
        }

        [Test]
        public void Clock_PulsesFromItsMiddle_SoTheDigitsDontJumpSideways()
        {
            hud.SetTimed(true);
            hud.SetClock(25f, running: true);
            var clock = System.Array.Find(hud.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true), t => t.name == "Clock");
            Assert.AreEqual(new Vector2(0.5f, 0.5f), clock.rectTransform.pivot, "Grows from its middle");
            Assert.AreEqual(TMPro.HorizontalAlignmentOptions.Center, clock.horizontalAlignment, "...and the digits sit in the middle of the box");
            Assert.AreEqual(hud.ClockCentre, clock.rectTransform.anchoredPosition);
            hud.PulseClock();
            Tween.Tick(0.05f);
            Assert.AreEqual(hud.ClockCentre, clock.rectTransform.anchoredPosition, "The pulse only changes the size");
            Assert.GreaterOrEqual(HudView.BreatheBig / HudView.BreatheSmall, 2f, "The last spore's breathing is easy to see");
        }

        [Test]
        public void AimingHint_StandsAboveTheBoard_NotOverIt()
        {
            var l = Layout;
            hud.Bubble.Show("Where to burst?", "Tap another cell to move it, tap the lit spot to burst.", overBoard: true);
            Assert.IsTrue(hud.Bubble.AboveBoard);
            for (int i = 0; i < 30; i++) Tween.Tick(0.01f);
            Assert.GreaterOrEqual(hud.Bubble.BottomY, l.Board.yMax, "Its bottom is above the board's top: no cell is hidden");
            hud.Bubble.Show("Puffball in 40 points", "You get one every 200 points");
            Assert.IsFalse(hud.Bubble.AboveBoard, "Other hints stay above the puffball button");
        }

        [Test]
        public void Clock_DigitsStayOnOneLine_WhateverTheTime()
        {
            hud.SetTimed(true);
            var clock = System.Array.Find(hud.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true), t => t.name == "Clock");
            float? baseline = null;
            foreach (float seconds in new[] { 180f, 179f, 134f, 71f, 45f, 9f, 0f })
            {
                hud.SetClock(seconds, running: true);
                clock.ForceMeshUpdate();
                float y = clock.textInfo.characterInfo[0].baseLine;
                if (baseline == null) baseline = y;
                Assert.AreEqual(baseline.Value, y, 0.01f, "The time stays on the same line at " + hud.ClockReading);
            }
        }
    }
}
