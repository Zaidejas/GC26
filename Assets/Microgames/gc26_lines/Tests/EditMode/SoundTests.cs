using System.Linq;
using GC26.Lines.Audio;
using GC26.Lines.Core;
using GC26.Lines.Game;
using GC26.Lines.View;
using NUnit.Framework;
using UnityEngine;

namespace GC26.Lines.Tests
{
    /// <summary>Sounds and music made from code (GDD → Sounds and music), and when the board plays them.</summary>
    public class SoundTests
    {
        static SoundBank bank;
        static SoundBank Bank => bank ??= SoundBank.Make(music: false);

        static float Peak(float[] s) => s.Max(Mathf.Abs);

        static int Crossings(float[] s, int count)
        {
            int n = 0;
            for (int i = 1; i < count; i++) if ((s[i - 1] < 0f) != (s[i] < 0f)) n++;
            return n;
        }

        [Test]
        public void EverySound_IsMade_AudibleAndClean()
        {
            foreach (LinesSound sound in System.Enum.GetValues(typeof(LinesSound)))
                Assert.IsNotNull(Bank.Effect(sound, sound == LinesSound.Clear ? 5 : 0), sound.ToString());
            for (int k = 0; k < SoundBank.StepNotes; k++) Assert.IsNotNull(Bank.Effect(LinesSound.Step, k));
            for (int n = SoundBank.ShortestLine; n <= SoundBank.LongestLine; n++) Assert.IsNotNull(Bank.Effect(LinesSound.Clear, n), "A line of " + n);

            foreach (var (sound, variant) in Bank.EffectKeys)
            {
                var samples = Bank.Effect(sound, variant);
                Assert.IsFalse(samples.Any(float.IsNaN), sound + " has no broken samples");
                Assert.Greater(Peak(samples), 0.02f, sound + " can be heard");
                Assert.LessOrEqual(Peak(samples), 1f, sound + " doesn't distort");
                Assert.Less(samples.Length, SoundBank.EffectRate * 6, sound + " ends (its silent tail is cut)");
            }
        }

        [Test]
        public void SparkleRun_AndHarp_ClimbHigher()
        {
            int window = SoundBank.EffectRate / 25; // the first 40 ms: only the first note
            Assert.Greater(Crossings(Bank.Effect(LinesSound.Step, 3), window), Crossings(Bank.Effect(LinesSound.Step, 0), window), "Later cells of a move sound higher");
            Assert.Greater(Crossings(Bank.Effect(LinesSound.Clear, 7), window), Crossings(Bank.Effect(LinesSound.Clear, 5), window), "A longer line starts higher");
        }

        [Test]
        public void Music_IsAQuietLoop_WithoutASeam()
        {
            var music = SoundBank.FireflyDance(7);
            Assert.AreEqual(Mathf.RoundToInt(SoundBank.MusicBeats * SoundBank.Beat * SoundBank.MusicRate), music.Length, "128 beats, about 46 s");
            Assert.IsFalse(music.Any(float.IsNaN));
            Assert.LessOrEqual(Peak(music), 1f);
            float rms = Mathf.Sqrt(music.Average(x => x * x));
            Assert.Greater(rms, 0.005f, "It can be heard");
            Assert.Less(Mathf.Abs(music[0] - music[music.Length - 1]), 0.05f, "The end runs into the start without a click");
        }

        [Test]
        public void LongestLine_AmongClearedCells()
        {
            var row = Enumerable.Range(0, 5).Select(c => new Cell(c, 4));
            var column = Enumerable.Range(0, 7).Select(r => new Cell(2, r)).Where(c => c.Row != 4);
            Assert.AreEqual(5, BoardPresenter.LongestLine(row.ToArray()));
            Assert.AreEqual(7, BoardPresenter.LongestLine(row.Concat(column).ToArray()), "A cross of 5 and 7: the 7 decides");
            var diagonal = Enumerable.Range(0, 6).Select(i => new Cell(i, 8 - i)).ToArray();
            Assert.AreEqual(6, BoardPresenter.LongestLine(diagonal));
        }

        // ---- The board's sounds ----

        BoardView view;
        BoardPresenter presenter;
        LinesAudio audio;

        void SetUpBoard(bool soundsOn = true)
        {
            Tween.KillAll();
            view = BoardView.Create();
            view.ApplyLayout(BoardLayout.Compute(new Vector2(1080, 1920), new Rect(0, 0, 1080, 1920)));
            // Not made yet (that happens in the background while playing), but every request is remembered.
            audio = LinesAudio.Create(null, musicOn: false, soundsOn: soundsOn);
            presenter = new BoardPresenter(view) { Sounds = audio };
            var board = TestBoards.FromRows(
                "R........",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........");
            presenter.ShowState(new GameState(board, new Preview(new[] { BallColor.Red }, new[] { new Cell(8, 8) }), 0, new Spawner(1)));
        }

        [TearDown]
        public void TearDown()
        {
            Tween.KillAll();
            if (view != null) Object.DestroyImmediate(view.gameObject);
            if (audio != null) Object.DestroyImmediate(audio.gameObject);
        }

        static void Run(float seconds) { for (float t = 0; t < seconds - 1e-4f; t += 0.01f) Tween.Tick(0.01f); }

        [Test]
        public void Board_PlaysItsSounds_AtTheRightMoments()
        {
            SetUpBoard();
            presenter.ShowSelected(new Cell(0, 0));
            presenter.ShowSelected(new Cell(0, 0)); // the same mushroom again: no second tick
            CollectionAssert.AreEqual(new[] { LinesSound.Select }, audio.Played);

            audio.Played.Clear();
            presenter.PlayMove(new[] { new Cell(0, 0), new Cell(1, 0), new Cell(2, 0), new Cell(3, 0) }, () => { });
            Run(0.5f);
            CollectionAssert.AreEqual(new[] { LinesSound.Step, LinesSound.Step, LinesSound.Step, LinesSound.Land }, audio.Played,
                "One sparkle per cell, then the dew drop");

            audio.Played.Clear();
            presenter.PlaySpawn(new[] { new Cell(5, 5) }, new[] { BallColor.Blue }, () => { });
            presenter.ShowBlocked(new Cell(3, 0), new Cell(8, 8));
            presenter.PlayClear(new[] { new Cell(5, 5) }, 0, () => { });
            CollectionAssert.AreEqual(new[] { LinesSound.Spawn, LinesSound.Blocked, LinesSound.Clear }, audio.Played);
        }

        [Test]
        public void SoundsOff_IsQuiet()
        {
            SetUpBoard(soundsOn: false);
            presenter.ShowSelected(new Cell(0, 0));
            presenter.ShowBlocked(new Cell(0, 0), new Cell(8, 8));
            Assert.IsEmpty(audio.Played);
            audio.Apply(musicOn: false, soundsOn: true);
            presenter.ShowSelected(null);
            presenter.ShowSelected(new Cell(0, 0));
            CollectionAssert.AreEqual(new[] { LinesSound.Select }, audio.Played, "Back on after the switch");
        }
    }
}
