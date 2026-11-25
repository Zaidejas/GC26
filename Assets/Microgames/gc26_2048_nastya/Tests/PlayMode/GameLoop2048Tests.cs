using System;
using System.Collections;
using System.IO;
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
        string _saveFolder;
        SaveStore2048 _store;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
#if UNITY_EDITOR
            // A save of our own: the player's real game and best score are never touched.
            _saveFolder = Path.Combine(Path.GetTempPath(), "gc26_2048_nastya_playtests_" + Guid.NewGuid().ToString("N"));
            _store = new SaveStore2048(_saveFolder, "gc26_2048_nastya_playtest_");
            _store.DeleteAll();
            Game2048Nastya.TestSaveStore = _store;
            Game2048Nastya.TestSkipSplash = true; // tests play at once; the splash has its own test
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
            Game2048Nastya.TestSaveStore = null;
            Game2048Nastya.TestSkipSplash = false;
            _store?.DeleteAll();
            if (_saveFolder != null && Directory.Exists(_saveFolder)) Directory.Delete(_saveFolder, true);
        }

        static Overlays2048 Overlays() => UnityEngine.Object.FindAnyObjectByType<Overlays2048>();

        static int ActiveTileViews() =>
            UnityEngine.Object.FindObjectsByType<TileView2048>(FindObjectsInactive.Exclude).Length;

        static int ModelTiles(GameState2048 s) => s.Board.ToValues().Count(v => v != 0);

        static BoardView2048 View() => UnityEngine.Object.FindAnyObjectByType<BoardView2048>();

        /// <summary>Same number of tiles, and every model tile has a view at its cell with its value and scale 1.</summary>
        static void AssertViewMatchesModel(Game2048Nastya game, string when)
        {
            var view = View();
            var tiles = UnityEngine.Object.FindObjectsByType<TileView2048>(FindObjectsInactive.Exclude);
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
            var game = UnityEngine.Object.FindAnyObjectByType<Game2048Nastya>();
            Assert.IsNotNull(game, "Game2048Nastya in the scene");
            Assert.IsNotNull(game.State, "Begin() ran in standalone mode");
            Assert.AreEqual(2, ModelTiles(game.State));
            Assert.AreEqual(2, ActiveTileViews());
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude).Length);
            yield break;
        }

        [UnityTest]
        public IEnumerator Moves_KeepViewInSyncWithModel()
        {
            var game = UnityEngine.Object.FindAnyObjectByType<Game2048Nastya>();
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
            var game = UnityEngine.Object.FindAnyObjectByType<Game2048Nastya>();
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
            var game = UnityEngine.Object.FindAnyObjectByType<Game2048Nastya>();
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
            var game = UnityEngine.Object.FindAnyObjectByType<Game2048Nastya>();
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
            var game = UnityEngine.Object.FindAnyObjectByType<Game2048Nastya>();
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
            var game = UnityEngine.Object.FindAnyObjectByType<Game2048Nastya>();
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

        // ---------- M4: overlays, saves, exit ----------

        [UnityTest]
        public IEnumerator Win_ShowsOverlayOncePerGame()
        {
            var game = UnityEngine.Object.FindAnyObjectByType<Game2048Nastya>();
            SetBoard(game,
                1024, 1024, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0);
            Assert.IsTrue(game.Move(Direction2048.Left));
            yield return WaitIdle(1f);
            Assert.AreEqual(Overlays2048.Kind.Win, Overlays().Open);
            Assert.IsFalse(game.Move(Direction2048.Right), "the overlay blocks moves");

            game.KeepGoing();
            Assert.IsFalse(Overlays().IsOpen);

            SetBoard(game,
                1024, 1024, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0);
            Assert.IsTrue(game.Move(Direction2048.Left));
            yield return WaitIdle(1f);
            Assert.IsFalse(Overlays().IsOpen, "never twice in one game");
        }

        [UnityTest]
        public IEnumerator NoMoves_ShowsGameOver_AndRemovesTheSave()
        {
            var game = UnityEngine.Object.FindAnyObjectByType<Game2048Nastya>();
            // Left fills the last cell; with a 2 or a 4 there, no move is possible.
            SetBoard(game,
                0, 8, 16, 8,
                32, 64, 32, 64,
                128, 256, 128, 256,
                512, 1024, 512, 1024);
            Assert.IsTrue(game.Move(Direction2048.Left));
            Assert.IsTrue(game.State.IsOver);
            yield return WaitIdle(1f);

            Assert.AreEqual(Overlays2048.Kind.GameOver, Overlays().Open);
            Assert.IsFalse(_store.HasGame, "a finished game is not resumed");
        }

        [UnityTest]
        public IEnumerator Pause_BlocksMoves_ResumeUnblocks_TimeScaleUntouched()
        {
            var game = UnityEngine.Object.FindAnyObjectByType<Game2048Nastya>();
            SetBoard(game,
                0, 0, 0, 2,
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0);
            game.Pause();
            Assert.AreEqual(Overlays2048.Kind.Pause, Overlays().Open);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(game.Move(Direction2048.Left));
            Assert.AreEqual(0, game.State.Moves);

            game.Pause(); // Esc again closes it
            Assert.IsFalse(Overlays().IsOpen);
            Assert.IsTrue(game.Move(Direction2048.Left));
            yield break;
        }

        [UnityTest]
        public IEnumerator ToMenu_SavesAndRelaunchRestoresBoardAndScore()
        {
            var game = UnityEngine.Object.FindAnyObjectByType<Game2048Nastya>();
            for (int i = 0; i < 40 && game.State.Score == 0; i++)
            {
                game.Move((Direction2048)(i % 4));
                game.FinishAnimation();
            }
            var values = game.State.Board.ToValues();
            int score = game.State.Score, moves = game.State.Moves;

            // Standalone, "To menu" saves and then launches the game again from the save.
            game.ExitToMenu();
            yield return null;

            CollectionAssert.AreEqual(values, game.State.Board.ToValues());
            Assert.AreEqual(score, game.State.Score);
            Assert.AreEqual(moves, game.State.Moves);
            AssertViewMatchesModel(game, "after the relaunch");
        }

        [UnityTest]
        public IEnumerator Hint_FirstLaunchOnly_GoesAwayAfterFirstMove()
        {
            var game = UnityEngine.Object.FindAnyObjectByType<Game2048Nastya>();
            var hud = UnityEngine.Object.FindAnyObjectByType<Hud2048>();
            Assert.IsTrue(hud.IsHintVisible, "fresh save: first launch");
            Assert.IsTrue(_store.HintShown);

            SetBoard(game,
                0, 0, 0, 2,
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0);
            Assert.IsTrue(game.Move(Direction2048.Left));
            Assert.IsFalse(hud.IsHintVisible);

            game.ExitToMenu(); // the next launch
            yield return null;
            Assert.IsFalse(hud.IsHintVisible);
        }

        // ---------- M5: undo button, glow, frame rate, safe area ----------

        [UnityTest]
        public IEnumerator UndoButton_FollowsUndoAvailability()
        {
            var game = UnityEngine.Object.FindAnyObjectByType<Game2048Nastya>();
            var hud = UnityEngine.Object.FindAnyObjectByType<Hud2048>();
            Assert.IsFalse(hud.IsUndoAvailable, "nothing to undo in a new game");

            SetBoard(game,
                0, 0, 0, 2,
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0);
            var before = game.State.Board.ToValues();
            Assert.IsTrue(game.Move(Direction2048.Left));
            Assert.IsTrue(hud.IsUndoAvailable);

            game.Undo();
            CollectionAssert.AreEqual(before, game.State.Board.ToValues());
            Assert.IsFalse(hud.IsUndoAvailable, "one step only");
            AssertViewMatchesModel(game, "after undo");

            game.StartNewGame();
            Assert.IsFalse(hud.IsUndoAvailable, "a new game cannot be undone");
            yield break;
        }

        [UnityTest]
        public IEnumerator HighTiles_Glow_And2048Pulses()
        {
            var game = UnityEngine.Object.FindAnyObjectByType<Game2048Nastya>();
            SetBoard(game,
                2, 128, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 2048);
            yield return null;
            var tiles = UnityEngine.Object.FindObjectsByType<TileView2048>(FindObjectsInactive.Exclude);
            var two = tiles.Single(t => t.Value == 2);
            var big = tiles.Single(t => t.Value == 128);
            var win = tiles.Single(t => t.Value == 2048);

            Assert.AreEqual(0f, two.Glow, "low tiles have no glow");
            Assert.Greater(big.Glow, 0f);
            Assert.IsFalse(big.IsPulsing);
            Assert.IsTrue(win.IsPulsing);
            Assert.IsFalse(two.IsPulsing);
        }

        [UnityTest]
        public IEnumerator FrameRate_Is60WhilePlaying_RestoredAfterUnload()
        {
            Assert.AreEqual(Game2048Nastya.TargetFrameRate, Application.targetFrameRate);
            yield return SceneManager.UnloadSceneAsync(_scene);
            Assert.AreNotEqual(Game2048Nastya.TargetFrameRate, Application.targetFrameRate,
                "the previous value (usually -1, the platform default) comes back");
        }

        [UnityTest]
        public IEnumerator SafeArea_WrapsHudAndBoard()
        {
            var safe = UnityEngine.Object.FindAnyObjectByType<SafeArea2048>();
            Assert.IsNotNull(safe);
            Assert.IsTrue(View().transform.IsChildOf(safe.transform), "the board is inside the safe area");
            Assert.IsTrue(UnityEngine.Object.FindAnyObjectByType<Hud2048>().transform.IsChildOf(safe.transform));
            yield break;
        }

        // ---------- M5: sound ----------

        [UnityTest]
        public IEnumerator Sounds_MatchWhatHappened()
        {
            var game = UnityEngine.Object.FindAnyObjectByType<Game2048Nastya>();
            var sfx = UnityEngine.Object.FindAnyObjectByType<Sfx2048>();
            Assert.IsNotNull(sfx, "Sfx2048 in the scene");

            SetBoard(game,
                2, 2, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0);
            Assert.IsTrue(game.Move(Direction2048.Left));
            StringAssert.Contains("Merge", sfx.LastPlayed.name);

            SetBoard(game,
                2, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0);
            Assert.IsFalse(game.Move(Direction2048.Left));
            StringAssert.Contains("Invalid", sfx.LastPlayed.name);

            Assert.IsTrue(game.Move(Direction2048.Right));
            StringAssert.Contains("Slide", sfx.LastPlayed.name);
            yield break;
        }

        [UnityTest]
        public IEnumerator SoundToggle_MutesAndIsSaved()
        {
            var game = UnityEngine.Object.FindAnyObjectByType<Game2048Nastya>();
            var sfx = UnityEngine.Object.FindAnyObjectByType<Sfx2048>();
            Assert.IsFalse(sfx.Muted, "sound is on by default");
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude).Length,
                "exactly one listener standalone");

            game.ToggleSound();
            Assert.IsTrue(sfx.Muted);
            Assert.IsFalse(_store.SoundOn, "saved");

            game.ExitToMenu(); // launching again keeps the setting
            yield return null;
            Assert.IsTrue(sfx.Muted);

            game.ToggleSound();
            Assert.IsFalse(sfx.Muted);
            Assert.IsTrue(_store.SoundOn);
        }

        // ---------- splash and title ----------

        [UnityTest]
        public IEnumerator Splash_BlocksMoves_AnyInputSkipsIt()
        {
            var game = UnityEngine.Object.FindAnyObjectByType<Game2048Nastya>();
            var splash = UnityEngine.Object.FindAnyObjectByType<Splash2048>(FindObjectsInactive.Include);
            Assert.IsNotNull(splash);
            Assert.IsFalse(splash.IsVisible, "tests start without it");

            Game2048Nastya.TestSkipSplash = false;
            game.ExitToMenu(); // standalone: launching again, now with the splash
            yield return null;
            Assert.IsTrue(splash.IsBlocking);
            int moves = game.State.Moves;
            Assert.IsFalse(game.CanPlay);

            game.OnMoveInput(Direction2048.Left); // only skips
            Assert.IsFalse(splash.IsVisible);
            Assert.AreEqual(moves, game.State.Moves, "the skipping key is not a move");
            Assert.IsTrue(game.CanPlay);
        }

        [UnityTest]
        public IEnumerator Splash_GoesAwayByItself()
        {
            var game = UnityEngine.Object.FindAnyObjectByType<Game2048Nastya>();
            var splash = UnityEngine.Object.FindAnyObjectByType<Splash2048>(FindObjectsInactive.Include);
            Game2048Nastya.TestSkipSplash = false;
            game.ExitToMenu();
            yield return null;
            Assert.IsTrue(splash.IsVisible);

            yield return new WaitForSecondsRealtime(1.2f + 0.4f + 0.3f);
            Assert.IsFalse(splash.IsVisible);
            Assert.IsTrue(game.CanPlay);
        }

        [UnityTest]
        public IEnumerator HudTitle_2048UnderColours_RightAligned_SameFontAndSize()
        {
            yield return null; // let the layout settle
            var hud = UnityEngine.Object.FindAnyObjectByType<Hud2048>();
            var texts = hud.GetComponentsInChildren<TMPro.TMP_Text>(true);
            var name = texts.Single(t => t.text == "Colours");
            var number = texts.Single(t => t.text == "2048" && t.transform.parent == name.transform.parent);

            Assert.AreEqual(name.font, number.font);
            Assert.AreEqual(name.fontSize, number.fontSize);
            Assert.AreEqual(name.fontStyle, number.fontStyle);
            Assert.AreEqual(TMPro.TextAlignmentOptions.Right, number.alignment);

            var a = name.rectTransform;
            var b = number.rectTransform;
            Assert.Less(b.position.y, a.position.y, "2048 is below Colours");
            Assert.AreEqual(a.rect.width, b.rect.width, 0.5f, "same width, so the right edges line up");
        }
    }
}
