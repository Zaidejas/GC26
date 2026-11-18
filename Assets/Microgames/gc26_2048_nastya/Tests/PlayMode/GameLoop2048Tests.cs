using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GC26._2048Nastya.Tests
{
    /// <summary>Opens the entry scene standalone and plays moves through Game2048Nastya.</summary>
    public class GameLoop2048Tests
    {
        const string ScenePath = "Assets/Microgames/gc26_2048_nastya/Scenes/gc26_2048_nastya_Main.unity";

        Scene _scene;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            _scene = SceneManager.GetSceneByPath(ScenePath);
            yield return null; // Start() → Begin()
            yield return null;
#else
            Assert.Ignore("Editor only");
            yield break;
#endif
        }

        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            if (_scene.IsValid() && _scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(_scene);
        }

        static int ActiveTileViews() =>
            Object.FindObjectsByType<TileView2048>(FindObjectsInactive.Exclude).Length;

        static int ModelTiles(GameState2048 s) => s.Board.ToValues().Count(v => v != 0);

        [UnityTest]
        public IEnumerator Standalone_StartsGameWithTwoTiles()
        {
            var game = Object.FindAnyObjectByType<Game2048Nastya>();
            Assert.IsNotNull(game, "Game2048Nastya in the scene");
            Assert.IsNotNull(game.State, "Begin() ran in standalone mode");
            Assert.AreEqual(2, ModelTiles(game.State));
            Assert.AreEqual(2, ActiveTileViews());
            Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude).Length);
            yield break;
        }

        [UnityTest]
        public IEnumerator Moves_KeepViewInSyncWithModel()
        {
            var game = Object.FindAnyObjectByType<Game2048Nastya>();
            var dirs = new[] { Direction2048.Left, Direction2048.Down, Direction2048.Right, Direction2048.Up };
            int valid = 0;
            for (int i = 0; i < 200 && !game.State.IsOver; i++)
            {
                if (game.Move(dirs[i % 4])) valid++;
                Assert.AreEqual(ModelTiles(game.State), ActiveTileViews(), $"after move {i}");
                if (i % 20 == 0) yield return null;
            }
            Assert.Greater(valid, 10);
            Assert.AreEqual(valid, game.State.Moves);
        }

        [UnityTest]
        public IEnumerator NewGame_ResetsScoreKeepsBest()
        {
            var game = Object.FindAnyObjectByType<Game2048Nastya>();
            for (int i = 0; i < 60 && game.State.Score == 0; i++)
                game.Move((Direction2048)(i % 4));
            int best = game.State.Best;
            game.StartNewGame();
            Assert.AreEqual(0, game.State.Score);
            Assert.AreEqual(best, game.State.Best);
            Assert.AreEqual(2, ActiveTileViews());
            yield break;
        }
    }
}
