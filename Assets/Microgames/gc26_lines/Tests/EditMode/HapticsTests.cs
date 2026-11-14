using System.Linq;
using GC26.Lines.Core;
using GC26.Lines.Game;
using GC26.Lines.Haptics;
using GC26.Lines.View;
using NUnit.Framework;
using UnityEngine;

namespace GC26.Lines.Tests
{
    /// <summary>Task L02: which moments on the board vibrate, and how (GDD → Controls and feedback → Vibration).</summary>
    public class HapticsTests
    {
        BoardView view;
        BoardPresenter presenter;
        LinesHaptics haptics;

        void SetUpBoard(bool vibrationOn = true)
        {
            Tween.KillAll();
            view = BoardView.Create();
            view.ApplyLayout(BoardLayout.Compute(new Vector2(1080, 1920), new Rect(0, 0, 1080, 1920)));
            haptics = new LinesHaptics(vibrationOn);
            presenter = new BoardPresenter(view) { Haptics = haptics };
            var board = TestBoards.FromRows(
                "RRRRRRR..",
                "R........",
                "R........",
                "R........",
                "R........",
                ".........",
                ".........",
                ".........",
                ".........");
            presenter.ShowState(new GameState(board, new Preview(new[] { BallColor.Red }, new[] { new Cell(8, 8) }), 0, new Spawner(1)));
        }

        [TearDown]
        public void TearDown()
        {
            Tween.KillAll();
            if (view != null) Object.DestroyImmediate(view.gameObject);
        }

        static void Run(float seconds) { for (float t = 0; t < seconds - 1e-4f; t += 0.01f) Tween.Tick(0.01f); }
        static Cell[] Row(int length) => Enumerable.Range(0, length).Select(c => new Cell(c, 0)).ToArray();

        [Test]
        public void LineOfFive_FirmTap_LineOfSeven_Stronger()
        {
            SetUpBoard();
            presenter.PlayClear(Row(5), 10, () => { });
            presenter.PlayClear(Row(7), 20, () => { });
            CollectionAssert.AreEqual(new[] { LinesFeel.Clear, LinesFeel.ClearBig }, haptics.Played);
        }

        [Test]
        public void TwoLinesAtOnce_TwoTaps()
        {
            SetUpBoard();
            var cross = Row(5).Concat(Enumerable.Range(1, 4).Select(r => new Cell(0, r))).ToArray(); // shares (0,0)
            Assert.AreEqual(2, BoardPresenter.LineCount(cross));
            presenter.PlayClear(cross, 40, () => { });
            CollectionAssert.AreEqual(new[] { LinesFeel.Clear }, haptics.Played, "The first tap at once");
            Run(LinesHaptics.RepeatGap + 0.02f);
            CollectionAssert.AreEqual(new[] { LinesFeel.Clear, LinesFeel.Clear }, haptics.Played, "The second a moment later");
        }

        [Test]
        public void NoPath_ErrorFeel_AndNothingElseOnTheBoardVibrates()
        {
            SetUpBoard();
            presenter.ShowSelected(new Cell(0, 0));
            presenter.ShowBlocked(new Cell(0, 0), new Cell(8, 8));
            presenter.PlayMove(new[] { new Cell(6, 0), new Cell(7, 0) }, () => { });
            presenter.PlaySpawn(new[] { new Cell(5, 5) }, new[] { BallColor.Blue }, () => { });
            Run(1f);
            CollectionAssert.AreEqual(new[] { LinesFeel.Error }, haptics.Played, "Selecting, moving, landing and new mushrooms don't vibrate");
        }

        [Test]
        public void VibrationOff_NothingVibrates()
        {
            SetUpBoard(vibrationOn: false);
            presenter.ShowBlocked(new Cell(0, 0), new Cell(8, 8));
            presenter.PlayClear(Row(7), 20, () => { });
            haptics.Play(LinesFeel.GameOver);
            Assert.IsEmpty(haptics.Played);
        }

        [Test]
        public void SwitchedOffBetweenTwoTaps_TheSecondDoesntCome()
        {
            SetUpBoard();
            haptics.Play(LinesFeel.Clear, times: 2);
            haptics.Enabled = false;
            Run(0.3f);
            Assert.AreEqual(1, haptics.Played.Count);
        }
    }
}
