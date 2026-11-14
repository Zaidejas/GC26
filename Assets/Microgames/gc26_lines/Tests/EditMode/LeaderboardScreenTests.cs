using System.Linq;
using GC26.Lines.Core;
using GC26.Lines.UI;
using GC26.Lines.View;
using NUnit.Framework;
using UnityEngine;

namespace GC26.Lines.Tests
{
    public class LeaderboardScreenTests
    {
        Camera camera;
        LeaderboardView view;
        HudView hud;

        [SetUp]
        public void SetUp()
        {
            camera = new GameObject("Camera").AddComponent<Camera>();
            view = LeaderboardView.Create(camera);
            view.ApplyLayout(BoardLayout.Compute(new Vector2(1080, 1920), new Rect(0, 0, 1080, 1920)));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(view.gameObject);
            if (hud != null) Object.DestroyImmediate(hud.gameObject);
            Object.DestroyImmediate(camera.gameObject);
        }

        static Leaderboard Board(int count)
        {
            var board = new Leaderboard();
            for (int i = 1; i <= count; i++) board.Add(i * 10, "P" + i, 1000L * i);
            return board;
        }

        [Test]
        public void Empty_ShowsTheExplanation()
        {
            view.Show(new Leaderboard());
            Assert.IsTrue(view.ShowsEmptyText);
            Assert.AreEqual(0, view.RowCount);
            Assert.AreEqual("", view.Message);
            Assert.IsFalse(view.PlayAgainShowing);
        }

        [Test]
        public void FromTheMenu_ListOnly_NoResultNoPlayAgain()
        {
            view.Show(Board(3));
            Assert.IsFalse(view.ShowsEmptyText);
            Assert.AreEqual(3, view.RowCount);
            Assert.AreEqual(-1, view.HighlightedRow);
            Assert.IsFalse(view.PlayAgainShowing);
            Assert.AreEqual("30", view.transform.Find("List/Rows/Row 1/Points").GetComponent<TMPro.TextMeshProUGUI>().text, "Best first");
            Assert.IsFalse(view.transform.Find("List/Rows/Row 4").gameObject.activeSelf, "Unused rows hidden");
        }

        [Test]
        public void AfterAGame_ResultLine_Highlight_PlayAgain()
        {
            var board = Board(3);
            board.Add(25, "Anna", 5000);
            view.Show(board, 5000, "You're #2, Anna!", showPlayAgain: true);
            Assert.AreEqual("You're #2, Anna!", view.Message);
            Assert.AreEqual(1, view.HighlightedRow);
            Assert.IsTrue(view.PlayAgainShowing);
            int again = 0, back = 0;
            view.PlayAgainClicked += () => again++;
            view.Closed += () => back++;
            view.PlayAgainButton.Click();
            view.BackButton.Click();
            Assert.AreEqual((1, 1), (again, back));
            Assert.AreEqual("Home", view.BackButton.IconSprite.name, "A plain house, like the game screen");
            Assert.IsTrue(view.BackButton.IsPlain);
        }

        [Test]
        public void ALowHighlight_IsScrolledIntoView()
        {
            var board = Board(9);
            board.Add(1, "Last", 99000);
            view.Show(board, 99000, "You're #10, Last!", showPlayAgain: true);
            Assert.AreEqual(9, view.HighlightedRow);
            Assert.Less(view.ScrollPosition, 0.01f, "Scrolled to the bottom on a 16:9 screen");
        }

        [Test]
        public void AllTenPlaces_FitOnA16x9Screen_WithSlightlyShorterRows()
        {
            view.Show(Board(10));
            Assert.IsTrue(view.AllRowsFit, "No scrolling needed from the menu");
            Assert.Less(view.RowHeightNow, 15f, "Rows a little shorter");
            Assert.GreaterOrEqual(view.RowHeightNow, LeaderboardView.ShortestRow);
        }

        [Test]
        public void TallScreens_KeepTheFullRowHeight()
        {
            view.ApplyLayout(BoardLayout.Compute(new Vector2(1080, 2400), new Rect(0, 0, 1080, 2400)));
            view.Show(Board(10));
            Assert.IsTrue(view.AllRowsFit);
            Assert.AreEqual(15f, view.RowHeightNow, 1e-4f, "20:9: rows as before");
        }

        [Test]
        public void Names_AreDrawn()
        {
            var board = new Leaderboard();
            board.Add(110, "nat", 1000);
            board.Add(54, "A long name here", 2000);
            view.Show(board, 2000, "You're #2, nat!", showPlayAgain: true);
            for (int i = 1; i <= 2; i++)
            {
                var name = view.transform.Find($"List/Rows/Row {i}/Name").GetComponent<TMPro.TextMeshProUGUI>();
                name.ForceMeshUpdate();
                Assert.Greater(name.textInfo.characterCount, 0, $"Row {i}");
                Assert.AreEqual(name.text.Length, name.textInfo.characterInfo.Take(name.textInfo.characterCount).Count(c => c.isVisible || c.character == ' '),
                    $"Row {i}: every letter shows");
            }
        }

        [Test]
        public void Dates_LookLikeThePrototype()
        {
            long time = new System.DateTimeOffset(2026, 11, 6, 12, 0, 0, System.TimeSpan.Zero).ToUnixTimeMilliseconds();
            Assert.AreEqual("6 Nov", LeaderboardView.DateText(time));
        }

        [Test]
        public void GameOver_PrefillsTheName_AndSaveHandsItOn()
        {
            hud = HudView.Create(camera);
            hud.ApplyLayout(BoardLayout.Compute(new Vector2(1080, 1920), new Rect(0, 0, 1080, 1920)));
            var dialog = hud.GameOver;
            string saved = null;
            dialog.Saved += n => saved = n;

            dialog.Show(240, "Player3");
            Assert.IsTrue(dialog.IsOpen);
            Assert.AreEqual("240", dialog.FinalText);
            Assert.AreEqual("Player3", dialog.NameField.text);
            Assert.AreEqual(Leaderboard.MaxNameLength, dialog.NameField.characterLimit);

            dialog.SetName("Anna");
            dialog.SaveButton.Click();
            Assert.AreEqual("Anna", saved);
            dialog.Close();
            Assert.IsFalse(dialog.IsOpen);
        }
    }
}
