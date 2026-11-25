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

        static BoardView2048 View() => Object.FindAnyObjectByType<BoardView2048>();

        /// <summary>Same number of tiles, and every model tile has a view at its cell with its value and scale 1.</summary>
        static void AssertViewMatchesModel(Game2048Nastya game, string when)
        {
            var view = View();
            var tiles = Object.FindObjectsByType<TileView2048>(FindObjectsInactive.Exclude);
            Assert.AreEqual(ModelTiles(game.State), tiles.Length, when);
            var board = game.State.Board;
            for (int y = 0; y < board.Size; y++)
            {
                for (int x = 0; x < board.Size; x++)
                {
                    if (!(board.Get(x, y) is Tile2048 tile)) continue;
                    var pos = view.CellPosition(x, y);
                    Assert.IsTrue(tiles.Any(t => t.Value == tile.Value
                                                 && (t.Rect.anchoredPosition - pos).sqrMagnitude < 0.01f
                                                 && t.Rect.localScale == Vector3.one),
                        $"{when}: tile {tile.Value} at ({x},{y})");
                }
            }
        }

        /// <summary>Replaces the board with a known one (row-major values, 0 = empty).</summary>
        static void SetBoard(Game2048Nastya game, params int[] values)
        {
            game.FinishAnimation();
            game.State.Board = Board2048.FromValues(4, values);
            game.State.IsOver = !game.State.Board.HasMoves();
            View().Render(game.State.Board);
        }

        static IEnumerator WaitIdle(float timeout)
        {
            float start = Time.realtimeSinceStartup;
            while (View().IsAnimating && Time.realtimeSinceStartup - start < timeout)
                yield return null;
        }

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
                game.FinishAnimation();
                AssertViewMatchesModel(game, $"after move {i}");
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
            game.FinishAnimation();
            int best = game.State.Best;
            game.StartNewGame();
            Assert.AreEqual(0, game.State.Score);
            Assert.AreEqual(best, game.State.Best);
            Assert.AreEqual(2, ActiveTileViews());
            yield break;
        }

        [UnityTest]
        public IEnumerator ValidMove_Animates_AndFinishesWithinQuarterSecond()
        {
            var game = Object.FindAnyObjectByType<Game2048Nastya>();
            SetBoard(game,
                0, 0, 2, 2,
                0, 0, 0, 0,
                0, 0, 0, 4,
                0, 0, 0, 0);
            Assert.IsTrue(game.Move(Direction2048.Left));
            Assert.IsTrue(View().IsAnimating, "animation started");

            float start = Time.realtimeSinceStartup;
            yield return WaitIdle(1f);
            float took = Time.realtimeSinceStartup - start;

            Assert.IsFalse(View().IsAnimating);
            // 0.25 s by the GDD plus a little slack for a busy editor frame.
            Assert.LessOrEqual(took, 0.35f, $"move took {took:0.000} s");
            AssertViewMatchesModel(game, "after the animation");
        }

        [UnityTest]
        public IEnumerator InputDuringAnimation_BuffersOneMove()
        {
            var game = Object.FindAnyObjectByType<Game2048Nastya>();
            SetBoard(game,
                0, 0, 0, 2,
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0);
            int moves = game.State.Moves;

            game.OnMoveInput(Direction2048.Left);  // plays at once
            Assert.AreEqual(moves + 1, game.State.Moves);
            Assert.IsTrue(View().IsAnimating);

            game.OnMoveInput(Direction2048.Right); // waits in the buffer
            Assert.AreEqual(moves + 1, game.State.Moves, "buffered, not played yet");

            game.OnMoveInput(Direction2048.Up);    // finishes the animation, plays Right, Up waits
            Assert.AreEqual(moves + 2, game.State.Moves, "the buffered move ran");
            Assert.IsTrue(View().IsAnimating);

            yield return WaitIdle(2f);
            Assert.IsFalse(View().IsAnimating, "the queue drains");
            AssertViewMatchesModel(game, "after the queue drained");
        }

        [UnityTest]
        public IEnumerator KeyMashing_NeverDesyncsTheView()
        {
            var game = Object.FindAnyObjectByType<Game2048Nastya>();
            for (int i = 0; i < 300 && !game.State.IsOver; i++)
            {
                game.OnMoveInput((Direction2048)((i * 7 + i / 3) % 4));
                if (i % 3 == 0) yield return null; // several inputs per frame, some frames apart
            }
            yield return WaitIdle(2f);
            Assert.IsFalse(View().IsAnimating);
            Assert.Greater(game.State.Moves, 10);
            AssertViewMatchesModel(game, "after mashing");
        }

        [UnityTest]
        public IEnumerator InvalidMove_BumpsBoardAndReturns()
        {
            var game = Object.FindAnyObjectByType<Game2048Nastya>();
            SetBoard(game,
                2, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0);
            var area = (RectTransform)View().transform.parent;
            var basePos = area.anchoredPosition;
            int moves = game.State.Moves;

            Assert.IsFalse(game.Move(Direction2048.Left));
            Assert.AreEqual(moves, game.State.Moves, "nothing changes");
            Assert.AreEqual(1, ActiveTileViews(), "nothing spawns");
            Assert.IsTrue(View().IsBumping);

            yield return new WaitForSecondsRealtime(0.2f);
            Assert.IsFalse(View().IsBumping);
            Assert.AreEqual(basePos, area.anchoredPosition, "the board comes back");
        }
    }
}
