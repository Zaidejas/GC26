using Lines.UI;
using Lines.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Lines.Tests
{
    public class TransitionsAndFontsTests
    {
        Camera camera;
        HudView hud;

        [SetUp]
        public void SetUp()
        {
            Tween.KillAll();
            camera = new GameObject("Camera").AddComponent<Camera>();
            hud = HudView.Create(camera);
            hud.ApplyLayout(BoardLayout.Compute(new Vector2(1080, 1920), new Rect(0, 0, 1080, 1920)));
        }

        [TearDown]
        public void TearDown()
        {
            Tween.KillAll();
            Object.DestroyImmediate(hud.gameObject);
            Object.DestroyImmediate(camera.gameObject);
        }

        static void Run(float seconds) { for (float t = 0; t < seconds; t += 0.02f) Tween.Tick(0.02f); }

        [Test]
        public void PopUp_FadesAndGrowsIn()
        {
            var dialog = hud.Dialog;
            dialog.Show("Start a new game?", "", "Yes", null);
            var group = dialog.GetComponent<CanvasGroup>();
            var card = dialog.transform.Find("Card");
            Assert.IsTrue(dialog.IsOpen);
            Assert.AreEqual(0f, group.alpha, 0.01f, "Starts see-through");
            Assert.Less(card.localScale.x, 1f, "Starts a little smaller");

            Run(PopupMotion.OpenSeconds + 0.05f);
            Assert.AreEqual(1f, group.alpha, 0.01f);
            Assert.AreEqual(1f, card.localScale.x, 0.001f);
        }

        [Test]
        public void PopUp_ClosesAtOnce_ThenFadesOut()
        {
            var dialog = hud.Dialog;
            dialog.Show("Start a new game?", "", "Yes", null);
            Run(0.3f);
            dialog.Close();
            var group = dialog.GetComponent<CanvasGroup>();
            Assert.IsFalse(dialog.IsOpen, "Closed straight away");
            Assert.IsFalse(group.blocksRaycasts, "Taps go through while it fades");
            Assert.IsTrue(dialog.gameObject.activeSelf, "Still fading");

            Run(PopupMotion.CloseSeconds + 0.05f);
            Assert.IsFalse(dialog.gameObject.activeSelf);
        }

        [Test]
        public void PopUp_ReopenedWhileFading_StaysOpen()
        {
            var dialog = hud.Dialog;
            dialog.Show("A", "", "Yes", null);
            dialog.Close();
            dialog.Show("B", "", "Yes", null);
            Run(0.5f);
            Assert.IsTrue(dialog.IsOpen);
            Assert.IsTrue(dialog.gameObject.activeSelf);
            Assert.AreEqual(1f, dialog.GetComponent<CanvasGroup>().alpha, 0.01f);
        }

        [Test]
        public void GameOverPopUp_Animates()
        {
            hud.GameOver.Show(10, "Player1");
            Assert.AreEqual(0f, hud.GameOver.GetComponent<CanvasGroup>().alpha, 0.01f);
            Run(0.3f);
            Assert.AreEqual(1f, hud.GameOver.GetComponent<CanvasGroup>().alpha, 0.01f);
        }

        [Test]
        public void Button_ShrinksWhilePressed_AndSpringsBack()
        {
            var button = hud.ResetButton;
            var data = new PointerEventData(null);
            button.OnPointerDown(data);
            Run(0.1f);
            Assert.AreEqual(PillButton.PressedScale, button.transform.localScale.x, 0.01f);
            button.OnPointerUp(data);
            Run(0.2f);
            Assert.AreEqual(1f, button.transform.localScale.x, 0.001f);
        }

        [Test]
        public void ADisabledButton_DoesNotMove()
        {
            hud.UndoButton.Interactable = false;
            hud.UndoButton.OnPointerDown(new PointerEventData(null));
            Run(0.1f);
            Assert.AreEqual(1f, hud.UndoButton.transform.localScale.x);
        }

        [Test]
        public void Fonts_AreThePrototypes()
        {
            Assert.IsNotNull(UiFonts.Body, "Nunito found in Resources/Fonts");
            Assert.IsNotNull(UiFonts.Display, "Fredoka found in Resources/Fonts");
            Assert.AreEqual(UiFonts.BodyFile, UiFonts.Body.name);
            Assert.AreEqual(UiFonts.BoldFile, UiFonts.Body.fontWeightTable[7].regularTypeface.name, "Bold text uses Nunito ExtraBold");

            var label = hud.ResetButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            Assert.AreSame(UiFonts.Body, label.font, "Buttons: Nunito");
            var score = hud.transform.Find("HUD/Score") ?? FindDeep(hud.transform, "Score");
            Assert.AreSame(UiFonts.Display, score.GetComponent<TMPro.TextMeshProUGUI>().font, "The score: Fredoka");
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }
    }
}
