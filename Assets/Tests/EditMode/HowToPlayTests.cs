using Lines.UI;
using Lines.View;
using NUnit.Framework;
using UnityEngine;

namespace Lines.Tests
{
    public class HowToPlayTests
    {
        Camera camera;
        HowToPlayView view;

        [SetUp]
        public void SetUp()
        {
            camera = new GameObject("Camera").AddComponent<Camera>();
            view = HowToPlayView.Create(camera);
            view.ApplyLayout(BoardLayout.Compute(new Vector2(1080, 1920), new Rect(0, 0, 1080, 1920)));
            view.Show(PieceSet.Balls());
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(view.gameObject);
            Object.DestroyImmediate(camera.gameObject);
        }

        [Test]
        public void OpensOnTheFirstCard_BackGreyed()
        {
            Assert.IsTrue(view.IsShowing);
            Assert.AreEqual(0, view.Step);
            Assert.AreEqual("Move a piece", view.Title);
            Assert.IsFalse(view.PreviousButton.Interactable);
            Assert.AreEqual("Next", view.NextButton.Text);
            Assert.IsTrue(view.BackButton.HasIcon);
        }

        [Test]
        public void NextAndBack_StepThroughTheCards()
        {
            view.NextButton.Click();
            Assert.AreEqual(1, view.Step);
            Assert.AreEqual("Make lines of five", view.Title);
            Assert.IsTrue(view.PreviousButton.Interactable);
            view.NextButton.Click();
            Assert.AreEqual("Watch what's coming", view.Title);
            Assert.AreEqual("Got it", view.NextButton.Text);
            view.PreviousButton.Click();
            Assert.AreEqual(1, view.Step);
            Assert.AreEqual("Next", view.NextButton.Text);
        }

        [Test]
        public void GotIt_AndBackArrow_Close()
        {
            int closed = 0;
            view.Closed += () => closed++;
            view.GoTo(2);
            view.NextButton.Click();
            Assert.AreEqual(1, closed);
            view.BackButton.Click();
            Assert.AreEqual(2, closed);
        }

        [Test]
        public void MiniBoards_ShowTheirPictures()
        {
            Assert.AreEqual("piece", view.CellKind(0));
            Assert.AreEqual("step", view.CellKind(1));
            Assert.AreEqual("target", view.CellKind(5));
            view.GoTo(1);
            Assert.AreEqual("empty", view.CellKind(0));
            for (int i = 1; i < 6; i++) Assert.AreEqual("piece", view.CellKind(i), "Five in a row");
            view.GoTo(2);
            Assert.AreEqual("dot", view.CellKind(0));
            Assert.AreEqual("piece", view.CellKind(2));
        }

        [Test]
        public void PictureAndTitle_StayInPlace_BetweenCards()
        {
            var mini = view.MiniBoard.anchoredPosition;
            var title = view.TitleRect.anchoredPosition;
            for (int step = 1; step < HowToPlayView.Cards.Length; step++)
            {
                view.GoTo(step);
                Assert.AreEqual(mini, view.MiniBoard.anchoredPosition, "Picture doesn't jump");
                Assert.AreEqual(title, view.TitleRect.anchoredPosition, "Title doesn't jump");
            }
        }

        [Test]
        public void ReopeningStartsOnCardOne()
        {
            view.GoTo(2);
            view.Hide();
            view.Show(PieceSet.Balls());
            Assert.AreEqual(0, view.Step);
        }
    }
}
