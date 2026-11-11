using GC26.Lines.Core;
using GC26.Lines.View;
using NUnit.Framework;
using UnityEngine;

namespace GC26.Lines.Tests
{
    public class BoardLayoutTests
    {
        const float Eps = 0.5f; // half a pixel

        static BoardLayout Layout(float w, float h, float topInset = 0, float bottomInset = 0) =>
            BoardLayout.Compute(new Vector2(w, h), new Rect(0, bottomInset, w, h - topInset - bottomInset));

        static void AssertInside(Rect inner, Rect outer, string what)
        {
            Assert.GreaterOrEqual(inner.xMin, outer.xMin - Eps, what + " left");
            Assert.LessOrEqual(inner.xMax, outer.xMax + Eps, what + " right");
            Assert.GreaterOrEqual(inner.yMin, outer.yMin - Eps, what + " bottom");
            Assert.LessOrEqual(inner.yMax, outer.yMax + Eps, what + " top");
        }

        static void AssertStackedWithoutOverlap(BoardLayout l)
        {
            Assert.Greater(l.BackButton.yMin, l.Hud.yMax, "Back button above the score");
            Assert.Greater(l.Hud.yMin, l.Board.yMax, "Score above the board");
            Assert.Greater(l.Board.yMin, l.Actions.yMax, "Board above the buttons");
            AssertInside(l.BackButton, l.SafeArea, "Back button");
            AssertInside(l.Hud, l.SafeArea, "Score");
            AssertInside(l.Board, l.SafeArea, "Board");
            AssertInside(l.Actions, l.SafeArea, "Buttons");
        }

        [Test]
        public void Ratio16x9_BoardFullWidthMinusMargins_EverythingFits()
        {
            var l = Layout(1080, 1920);

            Assert.AreEqual(0.92f * 1080, l.Board.width, Eps, "Board = full width minus 4% margins");
            Assert.AreEqual(l.Board.width, l.Board.height, Eps, "Square board");
            Assert.AreEqual(1080 / 2f, l.Board.center.x, Eps, "Centred horizontally");
            AssertStackedWithoutOverlap(l);
        }

        [Test]
        public void Ratio20x9_SameWidth_ExtraHeightSplitEvenly()
        {
            var l = Layout(1080, 2400);

            Assert.AreEqual(0.92f * 1080, l.Board.width, Eps, "Same board width as on 16:9");
            AssertStackedWithoutOverlap(l);
            // Space above the score (minus the top padding) equals space below the buttons (minus the bottom padding).
            float above = l.SafeArea.yMax - l.Hud.yMax - BoardLayout.TopPadding * l.Unit;
            float below = l.Actions.yMin - l.SafeArea.yMin - BoardLayout.BottomPadding * l.Unit;
            Assert.AreEqual(above, below, Eps);
            Assert.Greater(above, 100f, "There really is extra space on a tall screen");
        }

        [Test]
        public void Ratio20x9_WithNotchAndHomeBar_StaysInSafeArea()
        {
            var l = Layout(1080, 2400, topInset: 132, bottomInset: 63);
            AssertStackedWithoutOverlap(l);
            Assert.LessOrEqual(l.BackButton.yMax, 2400 - 132 + Eps, "Below the notch");
        }

        [Test]
        public void RealPhones_FitInSafeArea()
        {
            AssertStackedWithoutOverlap(Layout(750, 1334));                    // iPhone SE (3rd gen), 16:9
            AssertStackedWithoutOverlap(Layout(1206, 2622, 177, 102));          // iPhone 18 Pro, ~19.5:9
        }

        [Test]
        public void WideScreen_Tablet_ShrinksToFitHeight()
        {
            var l = Layout(1536, 2048); // 4:3
            AssertStackedWithoutOverlap(l);
            Assert.Less(l.Board.width, 0.92f * 1536, "Narrower than full width, so it fits the height");
            Assert.AreEqual(1536 / 2f, l.Board.center.x, Eps, "Still centred");
        }

        [Test]
        public void TextAndButtons_SameSizeOn16x9And20x9()
        {
            // GDD: only the empty space changes between the two shapes.
            var a = Layout(1080, 1920);
            var b = Layout(1080, 2400);
            Assert.AreEqual(a.Unit, b.Unit, 1e-4f);
            Assert.AreEqual(a.Actions.height, b.Actions.height, 1e-3f);
            Assert.AreEqual(a.Hud.height, b.Hud.height, 1e-3f);
        }

        [Test]
        public void Camera_OneWorldUnitIsOneCell_CellsLineUpWithTheBoard()
        {
            var l = Layout(1080, 2400, 132, 63);

            // Top-left cell centre: board edge + inset + half a cell.
            var topLeftWorld = l.BoardCenterWorld + (Vector2)BoardView.CellToLocal(new Cell(0, 0));
            var topLeftPx = l.WorldToScreen(topLeftWorld);
            Assert.AreEqual(l.Board.xMin + BoardLayout.BoardInset * l.Unit + l.CellPitch / 2f, topLeftPx.x, Eps);
            Assert.AreEqual(l.Board.yMax - BoardLayout.BoardInset * l.Unit - l.CellPitch / 2f, topLeftPx.y, Eps);

            // The camera shows exactly the screen height.
            Assert.AreEqual(2400f, 2f * l.OrthographicSize * l.CellPitch, Eps);
            Assert.AreEqual(1f - BoardLayout.CellGap * l.Unit / l.CellPitch, l.CellSizeWorld, 1e-5f);
        }

        [Test]
        public void ScreenToWorld_AndBack()
        {
            var l = Layout(1080, 1920);
            var p = new Vector2(123, 456);
            var back = l.WorldToScreen(l.ScreenToWorld(p));
            Assert.AreEqual(p.x, back.x, 1e-3f);
            Assert.AreEqual(p.y, back.y, 1e-3f);
        }
    }
}
