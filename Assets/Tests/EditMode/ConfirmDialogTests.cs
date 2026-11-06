using Lines.UI;
using Lines.View;
using NUnit.Framework;
using UnityEngine;

namespace Lines.Tests
{
    public class ConfirmDialogTests
    {
        Camera camera;
        HudView hud;
        ConfirmDialog Dialog => hud.Dialog;

        [SetUp]
        public void SetUp()
        {
            camera = new GameObject("Camera").AddComponent<Camera>();
            hud = HudView.Create(camera);
            hud.ApplyLayout(BoardLayout.Compute(new Vector2(1080, 1920), new Rect(0, 0, 1080, 1920)));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(hud.gameObject);
            Object.DestroyImmediate(camera.gameObject);
        }

        [Test]
        public void StartsClosed()
        {
            Assert.IsFalse(Dialog.IsOpen);
        }

        [Test]
        public void Show_DisplaysTheQuestion()
        {
            Dialog.Show("Start a new game?", "Your current game will be lost.", "Yes", () => { });
            Assert.IsTrue(Dialog.IsOpen);
            Assert.AreEqual("Start a new game?", Dialog.Title);
            Assert.AreEqual("Your current game will be lost.", Dialog.Message);
            Assert.AreEqual("Cancel", Dialog.CancelButton.Text);
            Assert.AreEqual("Yes", Dialog.ConfirmButton.Text);
        }

        [Test]
        public void Yes_RunsTheActionOnce_AndCloses()
        {
            int ran = 0;
            Dialog.Show("Start a new game?", "", "Yes", () => ran++);
            Dialog.ConfirmButton.Click();
            Assert.AreEqual(1, ran);
            Assert.IsFalse(Dialog.IsOpen);
        }

        [Test]
        public void Cancel_ClosesWithoutRunningTheAction()
        {
            int ran = 0;
            Dialog.Show("Start a new game?", "", "Yes", () => ran++);
            Dialog.CancelButton.Click();
            Assert.AreEqual(0, ran);
            Assert.IsFalse(Dialog.IsOpen);
        }

        [Test]
        public void ShowingAgain_UsesTheNewAction()
        {
            int first = 0, second = 0;
            Dialog.Show("A?", "", "Yes", () => first++);
            Dialog.CancelButton.Click();
            Dialog.Show("B?", "", "Yes", () => second++);
            Dialog.ConfirmButton.Click();
            Assert.AreEqual(0, first);
            Assert.AreEqual(1, second);
        }
    }
}
