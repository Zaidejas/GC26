using System.Linq;
using GC26.Lines.Core;
using GC26.Lines.Game;
using GC26.Lines.View;
using NUnit.Framework;
using UnityEngine;

namespace GC26.Lines.Tests
{
    /// <summary>
    /// The livelier board (teacher's feedback): mushrooms sway, a moving one leaves magic dust and pulses when it
    /// lands, fireflies drift behind the board, and the board makes an entrance when the game screen opens.
    /// </summary>
    public class LivelyBoardTests
    {
        BoardView view;
        BoardPresenter presenter;

        [SetUp]
        public void SetUp()
        {
            Tween.KillAll();
            view = BoardView.Create();
            view.ApplyLayout(BoardLayout.Compute(new Vector2(1080, 1920), new Rect(0, 0, 1080, 1920)));
            presenter = new BoardPresenter(view);
            var board = TestBoards.FromRows(
                "R...G....",
                ".........",
                ".........",
                ".........",
                "....B....",
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
            Object.DestroyImmediate(view.gameObject);
        }

        static void Run(float seconds) { for (float t = 0; t < seconds - 1e-4f; t += 0.01f) Tween.Tick(0.01f); }

        // ---- Sway ----

        [Test]
        public void Mushrooms_Sway_EachAtItsOwnRhythm_WithoutMoving()
        {
            var a = view.BallAt(new Cell(0, 0));
            var b = view.BallAt(new Cell(4, 0));
            var position = a.transform.localPosition;
            var scale = a.transform.localScale;
            var seen = new System.Collections.Generic.List<float>();
            for (int i = 0; i < 60; i++) { view.AdvanceSway(0.1f); seen.Add(a.SwayAngle); }

            Assert.LessOrEqual(seen.Max(), BoardView.SwayDegrees + 1e-3f);
            Assert.GreaterOrEqual(seen.Min(), -BoardView.SwayDegrees - 1e-3f);
            Assert.Greater(seen.Max() - seen.Min(), 4f, "It rocks almost the full 3° each way within 6 s");
            Assert.AreNotEqual(a.SwayAngle, b.SwayAngle, "Different rhythms");
            Assert.AreEqual(position, a.transform.localPosition, "Swaying doesn't move the ball");
            Assert.AreEqual(scale, a.transform.localScale, "...or resize it");
        }

        [Test]
        public void SelectedMushroom_DoesntSway()
        {
            presenter.ShowSelected(new Cell(0, 0));
            view.AdvanceSway(1.3f);
            Assert.AreEqual(0f, view.BallAt(new Cell(0, 0)).SwayAngle, 1e-3f);
            Assert.AreNotEqual(0f, view.BallAt(new Cell(4, 0)).SwayAngle, "The others still do");
            presenter.ShowSelected(null);
            view.AdvanceSway(0.7f);
            Assert.Greater(Mathf.Abs(view.BallAt(new Cell(0, 0)).SwayAngle), 1e-3f, "Sways again once let go");
        }

        // ---- Magic dust and the landing pulse ----

        [Test]
        public void MovingMushroom_LeavesMagicDust_ThatFades()
        {
            var path = new[] { new Cell(0, 0), new Cell(0, 1), new Cell(0, 2), new Cell(0, 3) };
            presenter.PlayMove(path, () => { });
            Run(0.09f);
            int dust = view.GetComponentsInChildren<SpriteRenderer>(true).Count(r => r.name == "Dust");
            Assert.GreaterOrEqual(dust, 2 * BoardPresenter.DustPerCell, "Sparkles on the cells it has left");
            Run(1.2f);
            Assert.AreEqual(0, view.GetComponentsInChildren<SpriteRenderer>().Count(r => r.name == "Dust"), "All gone (hidden, kept for the next move)");
        }

        [Test]
        public void TheNextMove_ReusesTheSparkles_InsteadOfMakingNewOnes()
        {
            int Sparkles() => view.GetComponentsInChildren<SpriteRenderer>(true).Count(r => r.name == "Dust");
            presenter.PlayMove(new[] { new Cell(0, 0), new Cell(0, 1), new Cell(0, 2) }, () => { });
            Run(1.2f);
            int afterFirst = Sparkles();
            Assert.Greater(afterFirst, 0, "Kept, hidden, for the next move");
            presenter.PlayMove(new[] { new Cell(0, 2), new Cell(0, 1), new Cell(0, 0) }, () => { });
            Run(1.2f);
            Assert.AreEqual(afterFirst, Sparkles(), "The same sparkles again, no new ones");
        }

        [Test]
        public void ArrivingMushroom_ContractsExpandsAndSettles()
        {
            var path = new[] { new Cell(0, 0), new Cell(0, 1) };
            var ball = view.BallAt(new Cell(0, 0));
            var full = ball.FullScale.x;
            presenter.PlayMove(path, () => { });
            Run(0.04f); // arrived
            float smallest = float.MaxValue, largest = 0f;
            for (int i = 0; i < 40; i++)
            {
                Tween.Tick(0.01f);
                smallest = Mathf.Min(smallest, ball.transform.localScale.x);
                largest = Mathf.Max(largest, ball.transform.localScale.x);
            }
            Assert.AreEqual(full * BoardPresenter.LandSmallest, smallest, full * 0.03f, "Contracts to about 82 %");
            Assert.AreEqual(full * BoardPresenter.LandLargest, largest, full * 0.03f, "Expands to about 112 %");
            Assert.AreEqual(full, ball.transform.localScale.x, 1e-4f, "Settles at its normal size");
            Assert.IsTrue(ball.Swaying, "Sways again after landing");
        }

        [Test]
        public void TurnEndingDuringThePulse_PulsesOnlyTheMovedMushroom()
        {
            var path = new[] { new Cell(0, 0), new Cell(0, 1) };
            var moved = view.BallAt(new Cell(0, 0));
            presenter.PlayMove(path, () => { });
            Run(0.06f); // arrived, pulsing
            // The turn ends (after the spawn) while the pulse still plays: the whole board is redrawn.
            var after = TestBoards.FromRows(
                ".........",
                "R...G....",
                ".........",
                ".........",
                "....B....",
                ".........",
                ".........",
                ".........",
                "........R");
            presenter.ShowState(new GameState(after, new Preview(new[] { BallColor.Red }, new[] { new Cell(7, 7) }), 0, new Spawner(1)));
            Assert.AreSame(moved, view.BallAt(new Cell(0, 1)), "The moved mushroom keeps its own picture");

            var others = new[] { new Cell(4, 1), new Cell(4, 4), new Cell(8, 8) }.Select(view.BallAt).ToArray();
            for (int i = 0; i < 40; i++)
            {
                Tween.Tick(0.01f);
                foreach (var other in others)
                    Assert.AreEqual(other.FullScale.x, other.transform.localScale.x, 1e-4f, "No other mushroom pulses");
            }
            Assert.AreEqual(moved.FullScale.x, moved.transform.localScale.x, 1e-4f, "The pulse finished on the moved one");
        }

        // ---- Fireflies ----

        [Test]
        public void Fireflies_DriftAndPulse_OnTheScreen()
        {
            var flies = view.Fireflies;
            Assert.AreEqual(Fireflies.Count, flies.FlyCount);
            var before = Enumerable.Range(0, flies.FlyCount).Select(flies.Position).ToArray();
            var brightBefore = Enumerable.Range(0, flies.FlyCount).Select(flies.Brightness).ToArray();
            flies.Advance(1.7f);
            int moved = Enumerable.Range(0, flies.FlyCount).Count(i => flies.Position(i) != before[i]);
            int pulsed = Enumerable.Range(0, flies.FlyCount).Count(i => Mathf.Abs(flies.Brightness(i) - brightBefore[i]) > 0.01f);
            Assert.Greater(moved, Fireflies.Count / 2, "They drift");
            Assert.Greater(pulsed, Fireflies.Count / 2, "They pulse");
            for (int i = 0; i < flies.FlyCount; i++)
            {
                Assert.GreaterOrEqual(flies.Brightness(i), 0.15f - 1e-3f);
                Assert.LessOrEqual(flies.Brightness(i), 0.9f + 1e-3f);
            }
        }

        // ---- Entrance ----

        [Test]
        public void Entrance_BoardFadesIn_ThenMushroomsPopUpInOrder()
        {
            var first = view.BallAt(new Cell(0, 0));
            var last = view.BallAt(new Cell(4, 4));
            var full = first.FullScale;
            view.PlayEntrance();

            Tween.Tick(0.01f);
            Assert.Less(view.transform.localScale.x, 1f, "The board grows into place");
            Assert.AreEqual(Vector3.zero, first.transform.localScale, "Mushrooms wait for the board");
            Run(0.3f);
            Assert.Greater(first.transform.localScale.x, 0f, "The first one has started to pop up");
            Assert.AreEqual(0f, last.transform.localScale.x, 1e-4f, "The last one hasn't yet");
            Run(1.2f);
            Assert.AreEqual(1f, view.transform.localScale.x, 1e-4f);
            Assert.AreEqual(full, first.transform.localScale, "Everything in place");
            Assert.AreEqual(full, last.transform.localScale);
        }
    }
}
