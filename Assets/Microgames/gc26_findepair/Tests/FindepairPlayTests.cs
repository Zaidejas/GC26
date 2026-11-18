#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace GC26.Findepair.Tests
{
    /// <summary>
    /// Loads the real scene in Play Mode and plays it. Set the environment variable FINDEPAIR_SHOTS to a
    /// folder to also save screenshots of the menu, board, burst and win screen there.
    /// </summary>
    public class FindepairPlayTests
    {
        const string ScenePath = "Assets/Microgames/gc26_findepair/Scenes/gc26_findepair_Main.unity";
        FindepairGame _game;
        FindepairSave _before;

        [UnityTearDown]
        public IEnumerator RestoreSave()
        {
            _before?.Save(); // tests must not change the player's real record or settings
            yield return null;
        }

        [UnitySetUp]
        public IEnumerator Load()
        {
            _before = new FindepairSave();
            _before.Load();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            _game = Object.FindAnyObjectByType<FindepairGame>();
            Assert.IsNotNull(_game, "FindepairGame in scene");
            yield return null; // Start() begins the game on its own (no shell)
            Assert.IsNotNull(_game.Board, "game started by itself without a shell");
        }

        FindepairBoard Board => _game.Board;

        static IEnumerator Seconds(float s)
        {
            for (float t = 0; t < s; t += Time.unscaledDeltaTime) yield return null;
        }

        IEnumerator Tap(Card c)
        {
            Board.OnCardClicked(c);
            yield return null;
        }

        (Card, Card) PairOf(Card c) => (c, Board.Cards.First(x => x != c && x.Data.id == c.Data.id && !x.IsMatched));

        [UnityTest]
        public IEnumerator Splash_GoesToMenu()
        {
            Assert.AreEqual(FindepairScreen.Splash, _game.UI.Current);
            yield return Seconds(1.8f);
            Assert.AreEqual(FindepairScreen.Menu, _game.UI.Current);
            yield return Shot("1_menu");
        }

        [UnityTest]
        public IEnumerator Match_VanishesAfterOneSecond_WithSparkles()
        {
            _game.StartGame();
            yield return null;
            Assert.AreEqual(12, Board.Cards.Count);
            var (a, b) = PairOf(Board.Cards[0]);
            yield return Tap(a);
            yield return Tap(b);
            Assert.AreEqual(1, Board.Moves);
            yield return Seconds(0.35f);
            Assert.IsTrue(a.IsMatched && b.IsMatched, "pair matched");
            Assert.IsFalse(Board.Locked, "other cards can be turned while the pair waits");
            yield return Seconds(0.6f);
            Assert.IsFalse(a.IsGone, "still visible before 1 s");
            yield return Seconds(0.5f);
            Assert.Greater(_game.UI.Sparkles.ActiveCount, 0, "sparkle burst running");
            yield return Shot("3_burst");
            yield return Seconds(0.3f);
            Assert.IsTrue(a.IsGone && b.IsGone, "pair vanished");
            Assert.AreEqual(12, Board.Cards.Count(c => c.gameObject.activeSelf), "grid slots stay (no reflow)");
            yield return Seconds(0.8f);
            Assert.AreEqual(0, _game.UI.Sparkles.ActiveCount, "sparkles cleaned up");
        }

        [UnityTest]
        public IEnumerator Miss_FlipsBack()
        {
            _game.StartGame();
            yield return null;
            var a = Board.Cards[0];
            var b = Board.Cards.First(x => x.Data.id != a.Data.id);
            yield return Tap(a);
            yield return Tap(b);
            var third = Board.Cards.First(x => x != a && x != b);
            yield return Tap(third);
            Assert.IsFalse(third.IsUp, "third card blocked during the check");
            yield return Seconds(1.5f);
            Assert.IsFalse(a.IsUp || b.IsUp, "both turned back");
            Assert.IsFalse(Board.Locked);
            Assert.AreEqual(1, Board.Moves);
        }

        [UnityTest]
        public IEnumerator Pause_DelaysVanish_AndStopsClock()
        {
            _game.StartGame();
            yield return null;
            var (a, b) = PairOf(Board.Cards[0]);
            yield return Tap(a);
            yield return Tap(b);
            yield return Seconds(0.4f);
            Board.SetPaused(true);
            Assert.IsTrue(_game.UI.PauseVisible, "pause overlay");
            float clock = Board.Elapsed;
            yield return Shot("4_pause");
            yield return Seconds(1.5f);
            Assert.IsFalse(a.IsGone, "no vanish while paused");
            Assert.AreEqual(clock, Board.Elapsed, 0.0001f, "clock stopped");
            Board.SetPaused(false);
            yield return Seconds(1.2f);
            Assert.IsTrue(a.IsGone, "vanished after resume");
        }

        [UnityTest]
        public IEnumerator FullRound_ShowsWin_AndSavesRecord()
        {
            _game.Save.ResetBest();
            _game.StartGame();
            yield return Seconds(0.2f);
            // open a few cards for the board screenshot
            var shown = Board.Cards.Take(5).ToList();
            foreach (var c in shown) c.StartCoroutine(c.Flip(true));
            yield return Seconds(0.4f);
            yield return Shot("2_board");
            foreach (var c in shown) c.StartCoroutine(c.Flip(false));
            yield return Seconds(0.4f);

            while (Board.Cards.Any(c => !c.IsMatched))
            {
                var (a, b) = PairOf(Board.Cards.First(c => !c.IsMatched));
                yield return Tap(a);
                yield return Tap(b);
                yield return Seconds(0.4f);
            }
            Assert.AreEqual(6, Board.Moves);
            Assert.IsFalse(Board.Running, "clock stops on the last match");
            Assert.IsFalse(_game.UI.WinVisible, "win waits for the last burst");
            yield return Seconds(2.2f);
            Assert.IsTrue(_game.UI.WinVisible, "win screen");
            Assert.AreEqual(6, _game.Save.BestMoves);
            Assert.AreEqual(3, _game.SessionScore);
            yield return Seconds(0.8f);
            yield return Shot("5_win");
        }

        [UnityTest]
        public IEnumerator Settings_And_Rules_Shots()
        {
            _game.UI.Show(FindepairScreen.Settings);
            yield return Seconds(0.2f);
            yield return Shot("6_settings");
            var rules = _game.GetComponentsInChildren<Transform>(true).First(t => t.name == "Rules").gameObject;
            rules.SetActive(true);
            yield return Seconds(0.2f);
            yield return Shot("7_rules");
        }

        // ---------- Screenshots ----------

        IEnumerator Shot(string name)
        {
            var dir = System.Environment.GetEnvironmentVariable("FINDEPAIR_SHOTS");
            if (string.IsNullOrEmpty(dir)) yield break;
            Directory.CreateDirectory(dir);
            var canvas = _game.GetComponentInChildren<Canvas>();
            var rt = new RenderTexture(1080, 2340, 24);
            var cam = new GameObject("ShotCamera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.targetTexture = rt;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1;
            yield return null;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Object.Destroy(cam.gameObject);
            Object.Destroy(tex);
            rt.Release();
        }
    }
}
#endif
