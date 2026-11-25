using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GC26._2048Nastya.Tests
{
    /// <summary>T45: save and load give back the same game; a broken file never breaks the game (TDD 8).</summary>
    public class SaveStore2048Tests
    {
        const string Prefix = "gc26_2048_nastya_test_";

        string _folder;
        SaveStore2048 _store;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "gc26_2048_nastya_tests_" + Guid.NewGuid().ToString("N"));
            _store = new SaveStore2048(_folder, Prefix);
            _store.DeleteAll();
        }

        [TearDown]
        public void TearDown()
        {
            _store.DeleteAll();
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        static GameState2048 SampleGame() =>
            new GameState2048(Board2048.FromValues(4, new[]
            {
                0, 2, 0, 0,
                4, 8, 0, 0,
                0, 0, 16, 0,
                2, 0, 0, 2048,
            }), score: 120, best: 500, moves: 37, wonShown: true);

        [Test]
        public void SaveThenLoad_GivesTheSameGame()
        {
            var game = SampleGame();
            _store.SaveGame(game);
            var loaded = _store.LoadGame(_store.LoadBest());

            Assert.IsNotNull(loaded);
            CollectionAssert.AreEqual(game.Board.ToValues(), loaded.Board.ToValues());
            Assert.AreEqual(120, loaded.Score);
            Assert.AreEqual(37, loaded.Moves);
            Assert.IsTrue(loaded.WonShown);
            Assert.AreEqual(500, loaded.Best);
            Assert.IsFalse(loaded.CanUndo, "undo history is not saved");
        }

        [Test]
        public void NoSave_LoadsNull()
        {
            Assert.IsNull(_store.LoadGame(0));
            Assert.IsFalse(_store.HasGame);
        }

        [Test]
        public void FinishedGame_IsNotSaved_AndRemovesTheOldSave()
        {
            _store.SaveGame(SampleGame());
            Assert.IsTrue(_store.HasGame);

            var over = new GameState2048(Board2048.FromValues(4, new[]
            {
                2, 4, 2, 4,
                4, 2, 4, 2,
                2, 4, 2, 4,
                4, 2, 4, 2,
            }), score: 300);
            Assert.IsTrue(over.IsOver);
            _store.SaveGame(over);

            Assert.IsFalse(_store.HasGame, "the next launch starts a new game");
            Assert.AreEqual(500, _store.LoadBest(), "the best score of the earlier save stays");
        }

        [TestCase("")]
        [TestCase("not json at all {{{")]
        [TestCase("{\"version\":99,\"size\":4,\"cells\":[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2],\"score\":0,\"moves\":0}")]
        [TestCase("{\"version\":1,\"size\":4,\"cells\":[0,2],\"score\":0,\"moves\":0}")]
        [TestCase("{\"version\":1,\"size\":9,\"cells\":[],\"score\":0,\"moves\":0}")]
        [TestCase("{\"version\":1,\"size\":4,\"cells\":[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,3],\"score\":0,\"moves\":0}")]
        [TestCase("{\"version\":1,\"size\":4,\"cells\":[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2],\"score\":-5,\"moves\":0}")]
        public void BrokenFile_IsIgnored_WithoutException(string content)
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(_store.GamePath, content);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("ignoring the saved game"));
            GameState2048 loaded = null;
            Assert.DoesNotThrow(() => loaded = _store.LoadGame(0));
            Assert.IsNull(loaded);
        }

        [Test]
        public void SafeWrite_LeavesNoTempFile_AndOverwrites()
        {
            _store.SaveGame(SampleGame());
            var second = SampleGame();
            second.Score = 999;
            _store.SaveGame(second);

            Assert.IsFalse(File.Exists(_store.GamePath + ".tmp"));
            Assert.AreEqual(999, _store.LoadGame(0).Score);
        }

        [Test]
        public void ClearGame_RemovesTheSave()
        {
            _store.SaveGame(SampleGame());
            _store.ClearGame();
            Assert.IsNull(_store.LoadGame(0));
        }

        [Test]
        public void Best_OnlyGrows()
        {
            _store.SaveBest(100);
            _store.SaveBest(40);
            Assert.AreEqual(100, _store.LoadBest());
            _store.SaveBest(250);
            Assert.AreEqual(250, _store.LoadBest());
        }

        [Test]
        public void Flags_ArePersisted_WithDefaults()
        {
            Assert.IsFalse(_store.HintShown);
            Assert.IsTrue(_store.SoundOn);
            _store.HintShown = true;
            _store.SoundOn = false;

            var again = new SaveStore2048(_folder, Prefix);
            Assert.IsTrue(again.HintShown);
            Assert.IsFalse(again.SoundOn);
        }

        [Test]
        public void TryParse_AcceptsTheTddExample()
        {
            const string json = "{\"version\":1,\"size\":4,\"cells\":[0,2,0,0,4,8,0,0,0,0,0,0,2,0,0,0]," +
                                "\"score\":120,\"moves\":37,\"wonShown\":false}";
            Assert.IsTrue(SaveStore2048.TryParse(json, out var data, out var problem), problem);
            Assert.AreEqual(4, data.size);
            Assert.AreEqual(120, data.score);
        }
    }
}
