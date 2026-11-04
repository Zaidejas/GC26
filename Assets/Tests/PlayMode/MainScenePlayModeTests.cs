using System.Collections;
using Lines.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lines.Tests
{
    /// <summary>Starts the real Main scene, as pressing Play does, and checks what appears.</summary>
    public class MainScenePlayModeTests
    {
        [UnityTest]
        public IEnumerator MainScene_ShowsBoardWithThreeBalls()
        {
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;

            var board = Object.FindAnyObjectByType<BoardView>();
            Assert.IsNotNull(board, "The board was created from code");
            Assert.AreEqual(3, board.BallCount, "A new game starts with 3 balls");
            Assert.IsTrue(Camera.main.orthographic);
        }
    }
}
