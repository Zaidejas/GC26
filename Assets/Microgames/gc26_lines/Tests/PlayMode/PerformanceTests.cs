using System.Collections;
using System.Linq;
using GC26.Lines.Core;
using GC26.Lines.Game;
using GC26.Lines.View;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;

namespace GC26.Lines.Tests
{
    /// <summary>
    /// Task 35: what the game does every frame (animations, sway, fireflies, floating menu mushrooms, music fade)
    /// must make no garbage: garbage makes the phone pause now and then to clean up, which shows as a hitch.
    /// The numbers are also written to the log.
    /// </summary>
    public class PerformanceTests : PlayModeTestBase
    {
        ProfilerRecorder garbage;

        [SetUp]
        public void StartRecording()
        {
            garbage = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        }

        [TearDown]
        public void StopRecording()
        {
            garbage.Dispose();
        }

        // Garbage made by one call of <paramref name="work"/> (bytes): the work runs many times inside one frame, and
        // that frame's garbage is compared with ordinary frames (the editor makes some garbage of its own).
        IEnumerator GarbagePerCall(string name, System.Action work, System.Collections.Generic.Dictionary<string, long> results, int calls = 2000)
        {
            work(); work(); // warm up (first calls may set things up once)
            yield return null; yield return null;
            long quiet = long.MaxValue;
            for (int i = 0; i < 5; i++) { yield return null; quiet = System.Math.Min(quiet, garbage.LastValue); }
            for (int i = 0; i < calls; i++) work();
            yield return null; // the frame with the work is now finished
            results[name] = System.Math.Max(0, garbage.LastValue - quiet) / calls;
        }

        [UnityTest]
        public IEnumerator OurEveryFrameWork_MakesNoGarbage()
        {
            yield return LoadGameScene();
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            game.Menu.NewGameButton.Click();
            yield return new WaitForSeconds(1.5f);
            game.Controller.Tap(FirstBall(game)); // a selected mushroom bounces (a running animation)
            yield return null;
            Assert.Greater(Tween.ActiveCount, 0, "An animation is running while measuring");

            var results = new System.Collections.Generic.Dictionary<string, long>();
            yield return GarbagePerCall("animations (Tween.Tick)", () => Tween.Tick(0f), results);
            yield return GarbagePerCall("sway", () => game.BoardView.AdvanceSway(0.016f), results);
            yield return GarbagePerCall("fireflies", () => game.BoardView.Fireflies.Advance(0.016f), results);
            yield return GarbagePerCall("menu floating", () => game.Menu.AdvanceFloating(0.016f), results);
            yield return GarbagePerCall("music fade", () => game.Sounds.FadeMusic(0.016f), results);
            Debug.Log("Lines perf (" + Tween.ActiveCount + " animations running), garbage per frame: " + string.Join(", ", results.Select(r => r.Key + " " + r.Value + " B")));
            foreach (var r in results) Assert.AreEqual(0, r.Value, r.Key + " makes no garbage each frame");
        }

        static Cell FirstBall(GameBootstrap game)
        {
            var board = game.Controller.Game.Board;
            for (int i = 0; i < board.CellCount; i++)
            {
                var c = new Cell(i % board.Size, i / board.Size);
                if (!board.IsEmpty(c)) return c;
            }
            return default;
        }
    }
}
