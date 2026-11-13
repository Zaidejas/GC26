using GC26.Lines.UI;
using GC26.Lines.View;
using NUnit.Framework;
using UnityEngine;

namespace GC26.Lines.Tests
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

        // Lets a card change (slide out, slide in) finish.
        static void Settle() { for (int i = 0; i < 50; i++) Tween.Tick(0.01f); }

        [TearDown]
        public void TearDown()
        {
            Tween.KillAll();
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
            Settle();
            Assert.AreEqual(1, view.Step);
            Assert.AreEqual("Make lines of five", view.Title);
            Assert.IsTrue(view.PreviousButton.Interactable);
            view.NextButton.Click();
            Settle();
            Assert.AreEqual("Watch what's coming", view.Title);
            Assert.AreEqual("Got it", view.NextButton.Text);
            view.PreviousButton.Click();
            Settle();
            Assert.AreEqual(1, view.Step);
            Assert.AreEqual("Next", view.NextButton.Text);
        }

        [Test]
        public void GotIt_AndTheHouse_Close()
        {
            int closed = 0;
            view.Closed += () => closed++;
            view.GoTo(2);
            view.NextButton.Click();
            Assert.AreEqual(1, closed);
            Assert.AreEqual("Home", view.BackButton.IconSprite.name, "The same plain house as on the game screen");
            Assert.IsTrue(view.BackButton.IsPlain);
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

        // ---- Swiping (and the slide between cards) ----

        float Width => 1080f;

        [Test]
        public void Next_SlidesTheCardOut_ThenTheNewOneIn()
        {
            view.NextButton.Click();
            Assert.IsTrue(view.Sliding);
            Tween.Tick(HowToPlayView.SlideOutSeconds * 0.6f);
            Assert.Less(view.CardOffset, 0f, "The old card leaves to the left");
            Assert.Less(view.CardAlpha, 1f);
            Assert.AreEqual(0, view.Step, "...and is still the old card");
            Tween.Tick(HowToPlayView.SlideOutSeconds * 0.5f + 0.02f);
            Assert.AreEqual(1, view.Step);
            Assert.Greater(view.CardOffset, 0f, "The new card comes in from the right");
            Settle();
            Assert.AreEqual(0f, view.CardOffset, 1e-3f);
            Assert.AreEqual(1f, view.CardAlpha, 1e-3f);
            Assert.IsFalse(view.Sliding);
        }

        [Test]
        public void SwipeLeft_IsNext_SwipeRight_IsPrevious()
        {
            view.Swipe(-Width * 0.3f, 0.4f);
            Settle();
            Assert.AreEqual(1, view.Step);
            view.Swipe(Width * 0.3f, 0.4f);
            Settle();
            Assert.AreEqual(0, view.Step);
        }

        [Test]
        public void QuickFlick_IsEnough_ButAShortSlowSwipe_SpringsBack()
        {
            view.Swipe(-Width * 0.1f, 0.4f);
            Assert.AreEqual(-Width * 0.1f, view.CardOffset, 1f, "The card followed the finger");
            Settle();
            Assert.AreEqual(0, view.Step, "Too short and slow");
            Assert.AreEqual(0f, view.CardOffset, 1e-3f, "...so it springs back");
            view.Swipe(-Width * 0.1f, 0.05f);
            Settle();
            Assert.AreEqual(1, view.Step, "A quick flick changes the card");
        }

        [Test]
        public void FirstAndLastCard_OnlyStretch_AndASwipeNeverCloses()
        {
            int closed = 0;
            view.Closed += () => closed++;
            view.Swipe(Width * 0.5f, 0.1f);
            Settle();
            Assert.AreEqual(0, view.Step, "Nothing before the first card");
            view.GoTo(2);
            view.Swipe(-Width * 0.5f, 0.1f);
            Assert.AreEqual(-Width * 0.5f * HowToPlayView.EdgeStretch, view.CardOffset, 1f, "It only stretches a little");
            Settle();
            Assert.AreEqual(2, view.Step);
            Assert.AreEqual(0, closed, "Only Got it closes");
            Assert.AreEqual(0f, view.CardOffset, 1e-3f);
        }

        [Test]
        public void ButtonsAndSwipes_WaitForTheSlide()
        {
            view.NextButton.Click();
            view.NextButton.Click(); // pressed again while sliding: ignored
            Settle();
            Assert.AreEqual(1, view.Step);
        }
    }
}
