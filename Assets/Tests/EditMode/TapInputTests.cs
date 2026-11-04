using Lines.Core;
using Lines.Game;
using Lines.View;
using NUnit.Framework;
using UnityEngine;

namespace Lines.Tests
{
    public class TapInputTests
    {
        // ---- Screen position → board cell ----

        static BoardLayout Layout() => BoardLayout.Compute(new Vector2(1080, 2400), new Rect(0, 63, 1080, 2400 - 132 - 63));

        static Vector2 CellCentrePx(BoardLayout l, Cell cell) =>
            l.WorldToScreen(l.BoardCenterWorld + (Vector2)BoardView.CellToLocal(cell));

        [Test]
        public void EveryCellCentre_MapsToThatCell()
        {
            var l = Layout();
            for (int row = 0; row < 9; row++)
            for (int column = 0; column < 9; column++)
            {
                var cell = new Cell(column, row);
                Assert.IsTrue(l.TryGetCell(CellCentrePx(l, cell), out var hit));
                Assert.AreEqual(cell, hit);
            }
        }

        [Test]
        public void TapNearCellEdge_StillThatCell()
        {
            var l = Layout();
            var centre = CellCentrePx(l, new Cell(3, 5));
            float almostHalf = l.CellPitch * 0.45f;
            Assert.IsTrue(l.TryGetCell(centre + new Vector2(almostHalf, -almostHalf), out var hit));
            Assert.AreEqual(new Cell(3, 5), hit);
        }

        [Test]
        public void TapOutsideTheBoard_IsNotACell()
        {
            var l = Layout();
            Assert.IsFalse(l.TryGetCell(l.Hud.center, out _), "Score area");
            Assert.IsFalse(l.TryGetCell(l.Actions.center, out _), "Buttons area");
            Assert.IsFalse(l.TryGetCell(new Vector2(5, l.Board.center.y), out _), "Left margin");
        }

        // ---- Selected ball bounces ----

        GameObject root;
        [SetUp] public void SetUp() => Tween.KillAll();
        [TearDown] public void TearDown() { Tween.KillAll(); if (root != null) Object.DestroyImmediate(root); }

        BoardPresenter PresenterWith(Board board, out BoardView view)
        {
            view = BoardView.Create();
            root = view.gameObject;
            var presenter = new BoardPresenter(view);
            presenter.ShowState(new GameState(board, new Preview(new[] { BallColor.Red }, new Cell[0]), 0, new Spawner(1)));
            return presenter;
        }

        static Board TwoBalls() => TestBoards.FromRows(
            "R........",
            ".........",
            ".........",
            ".........",
            "....G....",
            ".........",
            ".........",
            ".........",
            ".........");

        [Test]
        public void SelectedBall_Bounces()
        {
            var presenter = PresenterWith(TwoBalls(), out var view);
            var ball = view.BallAt(new Cell(4, 4)).transform;
            var rest = ball.localPosition;

            presenter.ShowSelected(new Cell(4, 4));
            for (int i = 0; i < 36; i++) Tween.Tick(0.01f);

            Assert.AreEqual(rest.y + BoardPresenter.BounceHeight, ball.localPosition.y, 0.01f, "Top of the bounce");
        }

        [Test]
        public void SwitchingSelection_FirstBallSettlesBack()
        {
            var presenter = PresenterWith(TwoBalls(), out var view);
            var first = view.BallAt(new Cell(4, 4)).transform;
            var rest = first.localPosition;
            presenter.ShowSelected(new Cell(4, 4));
            for (int i = 0; i < 20; i++) Tween.Tick(0.01f);

            presenter.ShowSelected(new Cell(0, 0));

            Assert.AreEqual(rest, first.localPosition, "Back in its cell");
            Tween.Tick(0.01f);
            Assert.AreEqual(1, Tween.ActiveCount, "Only the new ball bounces");
        }

        [Test]
        public void Deselect_StopsBouncing()
        {
            var presenter = PresenterWith(TwoBalls(), out var view);
            presenter.ShowSelected(new Cell(0, 0));
            Tween.Tick(0.1f);
            presenter.ShowSelected(null);
            Tween.Tick(0.01f);
            Assert.AreEqual(0, Tween.ActiveCount);
            Assert.AreEqual(BoardView.CellToLocal(new Cell(0, 0)), view.BallAt(new Cell(0, 0)).transform.localPosition);
        }
    }
}
