using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GC26.Timetraveler.Tests
{
    /// <summary>
    /// Automated checks for Time Traveler Runner. Run them in Window > General > Test Runner > PlayMode.
    /// Any Debug.LogError or exception during a test makes it fail.
    /// The player's saved progress is backed up before each test and restored after it.
    /// </summary>
    public class TimetravelerTests
    {
        GameObject _cameraObject;
        Camera _camera;
        readonly List<GameObject> _spawned = new List<GameObject>();
        Dictionary<string, int> _savedPrefs;
        byte[] _savedFile, _savedSettings;
        static string SettingsPath => Path.Combine(GC26.Core.MicrogameSave.Folder(TimetravelerGame.Id), TimetravelerSettings.FileName);

        static string SavePath => Path.Combine(GC26.Core.MicrogameSave.Folder(TimetravelerGame.Id), TimetravelerSave.FileName);

        static IEnumerable<string> SaveKeys()
        {
            yield return "coins";
            yield return "runner_best";
            yield return "levels_done";
            foreach (var level in TimetravelerLevels.All) yield return "level_coins_" + level.Key;
        }

        static string Key(string k) => GC26.Core.MicrogameSave.Key(TimetravelerGame.Id, k);

        [SetUp]
        public void SetUp()
        {
            _savedPrefs = SaveKeys().ToDictionary(k => k, k => PlayerPrefs.HasKey(Key(k)) ? PlayerPrefs.GetInt(Key(k)) : int.MinValue);
            foreach (var k in SaveKeys()) PlayerPrefs.DeleteKey(Key(k));
            // The player's real save file: kept aside, tests start from no save at all.
            _savedFile = File.Exists(SavePath) ? File.ReadAllBytes(SavePath) : null;
            if (File.Exists(SavePath)) File.Delete(SavePath);
            _savedSettings = File.Exists(SettingsPath) ? File.ReadAllBytes(SettingsPath) : null;
            if (File.Exists(SettingsPath)) File.Delete(SettingsPath);
            _cameraObject = new GameObject("TestCamera");
            _camera = _cameraObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.transform.position = new Vector3(0f, 0f, -10f);
            Time.timeScale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) if (go != null) Object.Destroy(go);
            _spawned.Clear();
            if (_cameraObject != null) Object.Destroy(_cameraObject);
            Time.timeScale = 1f;
            foreach (var pair in _savedPrefs)
            {
                if (pair.Value == int.MinValue) PlayerPrefs.DeleteKey(Key(pair.Key));
                else PlayerPrefs.SetInt(Key(pair.Key), pair.Value);
            }
            PlayerPrefs.Save();
            if (_savedFile != null) File.WriteAllBytes(SavePath, _savedFile);
            else if (File.Exists(SavePath)) File.Delete(SavePath);
            if (File.Exists(SavePath + ".broken")) File.Delete(SavePath + ".broken");
            if (_savedSettings != null) File.WriteAllBytes(SettingsPath, _savedSettings);
            else if (File.Exists(SettingsPath)) File.Delete(SettingsPath);
        }

        RunnerSession NewSession(TimetravelerLevel level)
        {
            var go = new GameObject("TestRun");
            _spawned.Add(go);
            var session = go.AddComponent<RunnerSession>();
            session.Init(_camera, new TimetravelerSave(), level);
            return session;
        }

        // ---------- data ----------

        [Test]
        public void Levels_HaveUniqueKeysAndOrderedProgression()
        {
            var levels = TimetravelerLevels.All;
            Assert.That(levels.Select(l => l.Key).Distinct().Count(), Is.EqualTo(levels.Length), "duplicate level keys");
            Assert.That(levels.Select(l => l.Title).Distinct().Count(), Is.EqualTo(levels.Length), "duplicate level titles");
            for (var i = 0; i < levels.Length; i++)
            {
                Assert.That(levels[i].Order, Is.EqualTo(i), $"{levels[i].Title}: Order must match its position");
                Assert.That(levels[i].LengthMeters, Is.GreaterThan(50), $"{levels[i].Title} is too short");
                Assert.That(TimetravelerLevels.Eras[levels[i].Era].Levels, Does.Contain(levels[i]), $"{levels[i].Title} is listed in another timeline");
                if (i > 0 && levels[i].Era == levels[i - 1].Era)   // each timeline starts easier again, then grows
                {
                    Assert.That(levels[i].Tier, Is.GreaterThanOrEqualTo(levels[i - 1].Tier), $"{levels[i].Title} is easier than the level before");
                    Assert.That(levels[i].LengthMeters, Is.GreaterThanOrEqualTo(levels[i - 1].LengthMeters), $"{levels[i].Title} is shorter than the level before");
                }
            }
        }

        [Test]
        public void MapSpots_AreInsideTheMapAndApart()
        {
            var spots = TimetravelerLevels.All.Select(l => (l.Title, l.MapPosition))
                .Concat(new[] { ("endless", TimetravelerLevels.EndlessMapPosition) })
                .Concat(TimetravelerLevels.Places.Select(p => (p.Name, p.MapPosition))).ToList();
            var edge = TimetravelerLevels.MapHalfWidth - 1.2f;
            foreach (var (name, p) in spots)
            {
                Assert.That(Mathf.Abs(p.x), Is.LessThanOrEqualTo(edge), $"{name} is outside the walkable map width");
                Assert.That(p.y, Is.InRange(-4f, TimetravelerLevels.EraHeight * TimetravelerLevels.Eras.Length - 2.5f),
                    $"{name} is outside the map");
            }
            foreach (var level in TimetravelerLevels.All)
            {
                var bottom = TimetravelerLevels.Eras[level.Era].MapBottom;
                Assert.That(level.MapPosition.y, Is.InRange(bottom + 1f, bottom + TimetravelerLevels.EraHeight - 2.5f),
                    $"{level.Title} is not inside its timeline on the map (or behind its gate)");
            }
            for (var i = 0; i < spots.Count; i++)
            for (var j = i + 1; j < spots.Count; j++)
                Assert.That(Vector2.Distance(spots[i].Item2, spots[j].Item2), Is.GreaterThan(3.3f),
                    $"{spots[i].Item1} and {spots[j].Item1} are so close their panels overlap");
        }

        [Test]
        public void Save_CompletingALevelOpensTheNextOne()
        {
            var save = new TimetravelerSave();
            var l1 = TimetravelerLevels.Era1[0];
            var l2 = TimetravelerLevels.Era1[1];
            Assert.IsTrue(save.IsOpen(l1));
            Assert.IsFalse(save.IsOpen(l2));
            save.CompleteLevel(l1, 7);
            Assert.IsTrue(save.IsDone(l1));
            Assert.IsTrue(save.IsOpen(l2));
            Assert.AreEqual(7, new TimetravelerSave().LevelBestCoins(l1), "best coins not saved");
            save.CompleteLevel(l1, 3);
            Assert.AreEqual(7, new TimetravelerSave().LevelBestCoins(l1), "a worse result overwrote the best");
            save.AddCoins(5);
            Assert.AreEqual(5, new TimetravelerSave().Coins);
        }

        [Test]
        public void Save_FinishingATimelineOpensTheNext()
        {
            var save = new TimetravelerSave();
            var eras = TimetravelerLevels.Eras;
            Assert.IsTrue(eras[0].IsOpen(save));
            Assert.IsFalse(eras[1].IsOpen(save));
            foreach (var level in TimetravelerLevels.Era1) save.CompleteLevel(level, 0);
            Assert.IsTrue(eras[1].IsOpen(save), "finishing 1-5 must open the Age of Exploration");
            Assert.IsFalse(eras[2].IsOpen(save));
            foreach (var level in TimetravelerLevels.Era2) save.CompleteLevel(level, 0);
            Assert.IsTrue(eras[2].IsOpen(save), "finishing 2-5 must open the Renaissance");
        }

        [Test]
        public void Save_SurvivesARestartAndNewGameWipesIt()
        {
            var save = new TimetravelerSave();
            Assert.IsFalse(save.HasProgress);
            save.CompleteLevel(TimetravelerLevels.Era1[0], 9);
            save.AddCoins(12);
            save.ReportDistance(300);
            save.SetMapPosition(new Vector2(3f, 7f));
            save.Flush();

            var again = new TimetravelerSave();                 // as if the game was closed and opened again
            Assert.IsTrue(again.HasProgress);
            Assert.AreEqual(1, again.LevelsDone);
            Assert.AreEqual(12, again.Coins);
            Assert.AreEqual(300, again.BestMeters);
            Assert.AreEqual(9, again.LevelBestCoins(TimetravelerLevels.Era1[0]));
            Assert.AreEqual(new Vector2(3f, 7f), again.MapPosition);

            again.NewGame();
            var fresh = new TimetravelerSave();
            Assert.IsFalse(fresh.HasProgress, "new game did not wipe the save");
            Assert.AreEqual(0, fresh.LevelsDone);
            Assert.IsNull(fresh.MapPosition);
        }

        [Test]
        public void Save_ABrokenFileStartsFreshInsteadOfCrashing()
        {
            File.WriteAllText(SavePath, "{ this is not json");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Save file unreadable"));
            var save = new TimetravelerSave();
            Assert.IsFalse(save.HasProgress);
            Assert.IsTrue(File.Exists(SavePath + ".broken"), "the broken file was not kept for a look");
            save.AddCoins(1);
            Assert.AreEqual(1, new TimetravelerSave().Coins, "saving after a broken file does not work");
        }

        [Test]
        public void Save_OldPlayerPrefsProgressMovesIntoTheFile()
        {
            PlayerPrefs.SetInt(Key("coins"), 40);
            PlayerPrefs.SetInt(Key("levels_done"), 3);
            PlayerPrefs.SetInt(Key("level_coins_" + TimetravelerLevels.Era1[1].Key), 6);
            var save = new TimetravelerSave();
            Assert.AreEqual(40, save.Coins);
            Assert.AreEqual(3, save.LevelsDone);
            Assert.AreEqual(6, save.LevelBestCoins(TimetravelerLevels.Era1[1]));
            Assert.IsFalse(PlayerPrefs.HasKey(Key("coins")), "old keys were not removed");
            Assert.AreEqual(3, new TimetravelerSave().LevelsDone, "migrated progress was not written to the file");
        }

        [Test]
        public void Settings_AreSavedAndSurviveANewGame()
        {
            var settings = TimetravelerSettings.Load();
            settings.MusicVolume = 0.3f;
            settings.Shake = 0;
            settings.TutorialSlowMotion = false;
            settings.RhythmOffsetMs = 500;                 // clamped to 200
            new TimetravelerSave().NewGame();

            var again = TimetravelerSettings.Load();
            Assert.AreEqual(0.3f, again.MusicVolume, 0.001f);
            Assert.AreEqual(0, again.Shake);
            Assert.AreEqual(0f, again.ShakeFactor);
            Assert.IsFalse(again.TutorialSlowMotion);
            Assert.AreEqual(200, again.RhythmOffsetMs);
            again.ResetToDefaults();
            Assert.IsTrue(TimetravelerSettings.Load().TutorialSlowMotion, "reset did not restore the defaults");
        }

        // ---------- track structure ----------

        static IEnumerable<int> TrackCases() => Enumerable.Range(0, TimetravelerLevels.All.Length).Concat(new[] { -1, -2, -3 });

        /// <summary>Levels (0..14) and endless runs (-1..-3): builds the whole track and checks it is sane.</summary>
        [UnityTest]
        public IEnumerator Track_IsWellFormed([ValueSource(nameof(TrackCases))] int levelIndex)
        {
            var level = levelIndex >= 0 ? TimetravelerLevels.All[levelIndex] : null;
            var session = NewSession(level);
            session.Traveler.Died += _ => { };
            session.enabled = false;                   // build only, no running
            session.Traveler.RunSpeed = 0f;
            var length = level != null ? level.LengthMeters / RunnerSession.MetersPerUnit + 60f : 1500f;
            session.Track.Extend(length);
            yield return new WaitForFixedUpdate();
            Physics2D.SyncTransforms();

            if (level != null && level.Boss) Assert.IsFalse(float.IsInfinity(session.Track.ArenaX), "no boss arena was built");
            else if (level != null) Assert.IsFalse(float.IsInfinity(session.Track.FinishX), "no finish line was built");

            var pieces = session.Track.Pieces.Where(p => p != null).ToList();
            var hazards = pieces.Where(p => p.GetComponent<RunnerHazard>() != null && p.GetComponent<RunnerMover>() == null).ToList();
            var coins = pieces.Where(p => p.GetComponent<RunnerCoin>() != null).ToList();
            var problems = new List<string>();

            foreach (var coin in coins)
            {
                var cb = coin.GetComponent<Collider2D>().bounds;
                foreach (var h in hazards)
                    if (h.GetComponent<RunnerWorldMember>() == null && h.GetComponent<Collider2D>().bounds.Intersects(cb))
                        problems.Add($"coin at x={coin.position.x:0.0} is inside {h.name}");
            }

            // Ground hazards must stand on ground, not float over a gap.
            var filter = new ContactFilter2D { useTriggers = false };
            var hits = new RaycastHit2D[4];
            foreach (var h in hazards.Where(h => h.name == "TrackRock" || h.name == "TrackTallWall"))
            {
                var b = h.GetComponent<Collider2D>().bounds;
                var n = Physics2D.Raycast(new Vector2(b.center.x, b.min.y + 0.1f), Vector2.down, filter, hits, 1f);
                if (n == 0) problems.Add($"{h.name} at x={b.center.x:0.0} floats over a gap");
            }

            // Ground obstacles that need a jump must leave room to land and jump again.
            var jumpers = hazards.Where(h => h.name == "TrackRock" || h.name == "TrackTallWall")
                .Select(h => h.GetComponent<Collider2D>().bounds).OrderBy(b => b.min.x).ToList();
            for (var i = 1; i < jumpers.Count; i++)
            {
                var gap = jumpers[i].min.x - jumpers[i - 1].max.x;
                if (gap < 9f && Mathf.Abs(jumpers[i].min.y - jumpers[i - 1].min.y) < 0.5f)
                    problems.Add($"jump obstacles only {gap:0.0} apart at x={jumpers[i].min.x:0.0}");
            }

            Assert.IsEmpty(problems, string.Join("\n", problems));
            Debug.Log($"[test] {(level != null ? level.Title : "endless " + levelIndex)}: {hazards.Count} hazards, {coins.Count} coins, OK");
        }

        // ---------- flow ----------

        [UnityTest]
        public IEnumerator Flow_MenuMapLevelAndBack_LeavesNothingBehind()
        {
            var root = new GameObject("TestFlow");
            _spawned.Add(root);
            var flow = root.AddComponent<TimetravelerFlow>();
            var exitScore = -1;
            flow.Init(_camera, score => exitScore = score);
            yield return null;
            Assert.IsNotNull(flow.CurrentScreen.GetComponent<TimetravelerMenu>(), "menu not shown first");

            flow.ShowMap();
            yield return null;
            Assert.IsNotNull(flow.CurrentScreen.GetComponent<WorldMap>(), "map not shown");

            flow.StartRun(TimetravelerLevels.Era1[0]);
            yield return new WaitForSeconds(0.5f);
            var session = flow.CurrentScreen.GetComponent<RunnerSession>();
            Assert.IsNotNull(session, "level not started");
            Assert.AreEqual(1, _camera.GetComponents<RunnerCamera>().Length, "camera follow missing or doubled");

            session.Pause();
            Assert.AreEqual(0f, Time.timeScale, "pause did not stop time");
            session.Resume();
            Assert.AreEqual(1f, Time.timeScale, "resume did not restore time");

            session.LeaveRequested();
            yield return null;
            yield return null;
            Assert.IsNotNull(flow.CurrentScreen.GetComponent<WorldMap>(), "leaving a level must return to the map");
            Assert.AreEqual(0, _camera.GetComponents<RunnerCamera>().Length, "runner camera follow left on the camera");
            Assert.IsNull(Object.FindAnyObjectByType<RunnerSession>(), "the run was not cleaned up");
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator TimeSlow_IsRestoredWhenLeavingTheRun()
        {
            var session = NewSession(null);
            var slowNow = true;
            session.Input.Scripted = () =>
            {
                var f = new TravelerInput.ScriptedFrame { Slow = slowNow };
                slowNow = false;
                return f;
            };
            yield return null;
            yield return null;
            Assert.Less(Time.timeScale, 1f, "time slow did not start");
            Object.Destroy(session.gameObject);
            yield return null;
            Assert.AreEqual(1f, Time.timeScale, "time scale not restored after the run was closed");
        }

        // ---------- playthroughs ----------

        static IEnumerable<int> LevelCases() => Enumerable.Range(0, TimetravelerLevels.All.Length);

        /// <summary>The autopilot must be able to finish every level: proves each level can be completed.</summary>
        [UnityTest, Timeout(240000)]
        public IEnumerator Level_CanBeFinishedByTheBot([ValueSource(nameof(LevelCases))] int levelIndex)
        {
            var level = TimetravelerLevels.All[levelIndex];
            yield return Play(level, level.LengthMeters + 80);   // the finish line may sit a bit past the nominal length
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Endless_BotSurvives700Meters()
        {
            yield return Play(null, 700);
        }

        /// <summary>The first rock of a new save: time slows down, the jump ends it, time is back to normal,
        /// and the second run of the level does not slow down again.</summary>
        [UnityTest, Timeout(120000)]
        public IEnumerator Tutorial_SlowsTimeOnceAndRestoresIt()
        {
            var level = TimetravelerLevels.Era1[0];
            var session = NewSession(level);
            var bot = new TimetravelerBot(session);
            session.Input.Scripted = bot.Next;
            string death = null;
            session.Traveler.Died += reason => death = reason;
            Application.targetFrameRate = 60;

            var slowSeen = false;
            var timeout = Time.realtimeSinceStartup + 60f;
            while (session.DistanceMeters < 60 && death == null)
            {
                if (Time.realtimeSinceStartup > timeout) Assert.Fail("timed out");
                if (session.InTutorialSlowMotion)
                {
                    slowSeen = true;
                    Assert.Less(Time.timeScale, 0.5f, "tutorial slow motion is not slow");
                }
                else Assert.AreEqual(1f, Time.timeScale, 0.001f, "time scale not restored after the tutorial");
                yield return null;
            }
            Assert.IsNull(death, "the bot died during the tutorial: " + death);
            Assert.IsTrue(slowSeen, "no tutorial slow motion on the first obstacles of a new save");

            // Same save, new run: those obstacles are known now.
            Object.Destroy(session.gameObject);
            yield return null;
            Assert.AreEqual(1f, Time.timeScale, 0.001f, "time scale not restored when the run was closed");
            var again = NewSession(level);
            again.Input.Scripted = new TimetravelerBot(again).Next;
            timeout = Time.realtimeSinceStartup + 60f;
            while (again.DistanceMeters < 25)
            {
                if (Time.realtimeSinceStartup > timeout) Assert.Fail("timed out (second run)");
                Assert.IsFalse(again.InTutorialSlowMotion, "a tutorial already learned slowed time again");
                yield return null;
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Checkpoint_DeathAfterItRestartsHalfWay()
        {
            var level = TimetravelerLevels.Era1[0];
            var session = NewSession(level);
            var bot = new TimetravelerBot(session);
            var restart = false;
            session.Input.Scripted = () => restart ? new TravelerInput.ScriptedFrame { Restart = true } : bot.Next();
            Time.timeScale = 2f;
            var timeout = Time.realtimeSinceStartup + 90f;
            while (!session.CheckpointReached)
            {
                if (Time.realtimeSinceStartup > timeout || session.Traveler.IsDead) Assert.Fail("never reached the checkpoint");
                yield return null;
            }
            yield return new WaitForSeconds(1f);
            session.Traveler.Die("test death after the checkpoint");
            yield return new WaitForSecondsRealtime(0.6f);
            restart = true;
            yield return null;
            yield return null;
            restart = false;
            Time.timeScale = 1f;
            Assert.IsFalse(session.Traveler.IsDead, "did not restart");
            Assert.AreEqual(session.Track.CheckpointX + 1f, session.Traveler.transform.position.x, 2f,
                "restart did not put the traveler at the checkpoint");
        }

        static readonly string NL = System.Environment.NewLine;

        /// <summary>Track pieces around the traveler, for failure messages.</summary>
        static string DumpTrack(RunnerSession session)
        {
            var x = session.Traveler.transform.position.x;
            var parts = session.Track.Pieces.Where(p => p != null && p.GetComponent<RunnerCoin>() == null)
                .Select(p => (p, b: p.GetComponent<Collider2D>().bounds))
                .Where(t => t.b.max.x > x - 20f && t.b.min.x < x + 12f)
                .OrderBy(t => t.b.min.x)
                .Select(t => $"{t.p.name}[{t.b.min.x:0.0}..{t.b.max.x:0.0} y {t.b.min.y:0.0}..{t.b.max.y:0.0}]");
            return string.Join("; ", parts);
        }

        IEnumerator Play(TimetravelerLevel level, int targetMeters)
        {
            var session = NewSession(level);
            session.TutorialSlowMotion = false;   // faster; Tutorial_SlowsTimeOnceAndRestoresIt covers it
            var bot = new TimetravelerBot(session);
            session.Input.Scripted = bot.Next;
            string death = null;
            session.Traveler.Died += reason =>
                death = $"{reason} at {session.DistanceMeters} m (bot was doing: {bot.LastDecision})" + NL + "Track: " + DumpTrack(session) + NL + "Last frames:" + NL + bot.Trace;
            Application.targetFrameRate = 60;
            Time.timeScale = 2f;
            var startCoins = 0;

            var timeout = Time.realtimeSinceStartup + 200f;
            while (death == null && !session.IsFinished && session.DistanceMeters < targetMeters)
            {
                if (Time.realtimeSinceStartup > timeout) Assert.Fail($"timed out at {session.DistanceMeters} m (stuck?)");
                yield return null;
            }
            Time.timeScale = 1f;
            var name = level != null ? $"Level {level.Title}" : "Endless";
            Assert.IsNull(death, $"{name}: the bot died: {death}");
            if (level != null) Assert.IsTrue(session.IsFinished, $"{name}: finish line not reached");
            if (level != null && level.Boss)
            {
                Assert.AreEqual(RunnerBoss.State.Won, session.Boss.CurrentState, $"{name}: the boss was not beaten");
                Assert.AreEqual(RunnerBoss.MaxHearts, session.Boss.Hearts, $"{name}: notes played on the beat still lost hearts (hit window broken?)");
            }
            Assert.Greater(session.RunCoins, startCoins, $"{name}: no coins collected on the whole run");
            Debug.Log($"[test] {name}: done, {session.DistanceMeters} m, {session.RunCoins} coins");
        }
    }
}
