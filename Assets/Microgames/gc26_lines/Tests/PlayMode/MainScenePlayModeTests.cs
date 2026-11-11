using System.Collections;
using GC26.Lines.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GC26.Lines.Tests
{
    /// <summary>Starts the real Main scene, as pressing Play does, and checks what appears.</summary>
    public class MainScenePlayModeTests : PlayModeTestBase
    {
        [UnityTest]
        public IEnumerator MainScene_ShowsBoardWithThreeBalls()
        {
            yield return LoadGameScene();
            yield return null;

            var game = Object.FindAnyObjectByType<GC26.Lines.Game.GameBootstrap>();
            Assert.IsTrue(game.Menu.IsShowing, "The app opens on the main menu");
            Assert.IsNull(Object.FindAnyObjectByType<BoardView>(), "The board is hidden behind the menu");
            game.Menu.NewGameButton.Click();

            var board = Object.FindAnyObjectByType<BoardView>();
            Assert.IsNotNull(board, "The board was created from code");
            Assert.AreEqual(3, board.BallCount, "A new game starts with 3 balls");
            Assert.IsTrue(Camera.main.orthographic);

            var hud = Object.FindAnyObjectByType<GC26.Lines.UI.HudView>();
            Assert.IsNotNull(hud, "The score/next strip was created from code");
            Assert.AreEqual("0", hud.ScoreText);
            Assert.AreEqual(3, System.Linq.Enumerable.Count(hud.NextPieces, i => i.enabled && i.sprite != null));
            Assert.AreEqual(3, System.Linq.Enumerable.Count(board.DotCells), "3 preview dots on the board");
        }
    }
}
