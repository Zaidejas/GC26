using GC26.Lines.UI;
using GC26.Lines.View;
using NUnit.Framework;
using UnityEngine;

namespace GC26.Lines.Tests
{
    public class MenuTests
    {
        Camera camera;
        MenuView menu;
        HudView hud;

        [SetUp]
        public void SetUp()
        {
            Tween.KillAll();
            camera = new GameObject("Camera").AddComponent<Camera>();
            menu = MenuView.Create(camera);
            menu.ApplyLayout(BoardLayout.Compute(new Vector2(1080, 1920), new Rect(0, 0, 1080, 1920)));
        }

        [TearDown]
        public void TearDown()
        {
            Tween.KillAll();
            Object.DestroyImmediate(menu.gameObject);
            if (hud != null) Object.DestroyImmediate(hud.gameObject);
            Object.DestroyImmediate(camera.gameObject);
        }

        [Test]
        public void NoSavedGame_ContinueGreyed_WithoutScore()
        {
            menu.Show(null, PieceSet.Balls());
            Assert.IsTrue(menu.IsShowing);
            Assert.IsFalse(menu.ContinueButton.Interactable);
            Assert.AreEqual("Continue", menu.ContinueButton.Text);
            Assert.AreEqual(1f, menu.ContinueButton.GetComponent<CanvasGroup>().alpha, "Grey, not faded (prototype)");
        }

        [Test]
        public void SavedGame_ContinueShowsItsScore()
        {
            menu.Show(120, PieceSet.Balls());
            Assert.IsTrue(menu.ContinueButton.Interactable);
            StringAssert.StartsWith("Continue", menu.ContinueButton.Text);
            StringAssert.Contains("score 120", menu.ContinueButton.Text);
        }

        [Test]
        public void Buttons_RaiseTheirEvents()
        {
            menu.Show(5, PieceSet.Balls());
            int cont = 0, fresh = 0, howTo = 0, board = 0;
            menu.ContinueClicked += () => cont++;
            menu.NewGameClicked += () => fresh++;
            menu.HowToPlayClicked += () => howTo++;
            menu.LeaderboardClicked += () => board++;

            menu.ContinueButton.Click();
            menu.NewGameButton.Click();
            menu.HowToPlayButton.Click();
            menu.LeaderboardButton.Click();

            Assert.AreEqual((1, 1, 1, 1), (cont, fresh, howTo, board));
        }

        [Test]
        public void HowToPlayAndLeaderboard_CanBeGreyedOut()
        {
            menu.SetSecondaryScreensAvailable(false, false);
            Assert.IsFalse(menu.HowToPlayButton.Interactable);
            Assert.IsFalse(menu.LeaderboardButton.Interactable);
            int clicks = 0;
            menu.HowToPlayClicked += () => clicks++;
            menu.HowToPlayButton.Click();
            Assert.AreEqual(0, clicks, "A greyed-out button does nothing");
        }

        [Test]
        public void Pieces_Bob_AndStopWhenTheMenuHides()
        {
            menu.Show(null, PieceSet.Balls());
            var piece = menu.transform.Find("Piece 0");
            var rest = piece.localPosition;
            menu.AdvanceFloating(1f);
            Assert.Greater(piece.localPosition.y, rest.y, "Bobbing up");

            menu.Hide();
            Assert.IsFalse(menu.IsShowing);
            Assert.AreEqual(rest, piece.localPosition, "Back in place when hidden");
        }

        [Test]
        public void Hud_ScoreAndNext_LabelsShareOneLine_NumberLevelWithMushrooms()
        {
            hud = HudView.Create(camera);
            foreach (var size in new[] { new Vector2(1080, 1920), new Vector2(1080, 2400), new Vector2(750, 1334) })
            {
                hud.ApplyLayout(BoardLayout.Compute(size, new Rect(0, 0, size.x, size.y)));
                foreach (int score in new[] { 0, 7, 1234 })
                {
                    hud.SetScore(score);
                    Assert.AreEqual(hud.ScoreLabelY, hud.NextLabelY, 0.01f, "SCORE and NEXT on one line (" + size + ", score " + score + ")");
                    Assert.AreEqual(hud.NextMiddleY, hud.ScoreMiddleY, 0.5f, "The number on the same line as the mushrooms (" + size + ", score " + score + ")");
                }
            }
        }

        [Test]
        public void Hud_HasAPlainHouseButton()
        {
            hud = HudView.Create(camera);
            hud.ApplyLayout(BoardLayout.Compute(new Vector2(1080, 1920), new Rect(0, 0, 1080, 1920)));
            Assert.IsTrue(hud.BackButton.HasIcon);
            Assert.AreEqual("Home", hud.BackButton.IconSprite.name, "A house, not the back arrow");
            Assert.IsTrue(hud.BackButton.IsPlain, "Just the house: no ring or disc behind it");
            Assert.AreEqual("", hud.BackButton.Text, "Icon only, no word");
            int back = 0;
            hud.BackClicked += () => back++;
            hud.BackButton.Click();
            Assert.AreEqual(1, back);
        }

        [Test]
        public void Dialog_StaysUsableWhenTheHudIsHidden()
        {
            hud = HudView.Create(camera);
            hud.SetVisible(false);
            hud.Dialog.Show("Start a new game?", "", "Yes", () => { });
            Assert.IsTrue(hud.Dialog.gameObject.activeInHierarchy, "Dialogs live on their own layer, above the menu");
        }
    }
}
