using System;
using System.Collections.Generic;
using System.IO;
using GC26.Core;
using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>
    /// Saved progress in one JSON file: MicrogameSave.Folder(Id)/save.json (= persistentDataPath/gc26_timetraveler/,
    /// as the rules ask). Coin bank, best endless distance, finished levels, best coins per level, where the
    /// traveler stood on the map, play time. Saved automatically on every change (level done, coins banked, record)
    /// and when leaving the map. <see cref="NewGame"/> wipes it.
    /// Older builds kept progress in PlayerPrefs (gc26_timetraveler_* keys): it is moved into the file once.
    /// One instance per game run (no static state).
    /// </summary>
    public class TimetravelerSave
    {
        public const string FileName = "save.json";
        const int Version = 1;

        [Serializable]
        class LevelCoins
        {
            public string key;
            public int coins;
        }

        [Serializable]
        class Data
        {
            public int version = Version;
            public int coins;
            public int bestMeters;
            public int levelsDone;
            public List<LevelCoins> levelCoins = new List<LevelCoins>();
            public bool hasMapPosition;
            public float mapX, mapY;
            public float playSeconds;
            public List<string> tutorialsSeen = new List<string>();
            public string savedAt = "";
        }

        readonly string _path;
        Data _data = new Data();

        public int Coins => _data.coins;
        public int BestMeters => _data.bestMeters;
        /// <summary>How many levels of the story are finished, in order (level N is open when N &lt;= this).</summary>
        public int LevelsDone => _data.levelsDone;
        /// <summary>Anything worth continuing (the menu shows "Continue" and asks before a new game).</summary>
        public bool HasProgress => _data.levelsDone > 0 || _data.coins > 0 || _data.bestMeters > 0;
        /// <summary>Where the traveler last stood on the world map, if saved.</summary>
        public Vector2? MapPosition => _data.hasMapPosition ? new Vector2(_data.mapX, _data.mapY) : (Vector2?)null;
        public float PlaySeconds => _data.playSeconds;
        /// <summary>Local time of the last save, as text ("" if never saved).</summary>
        public string SavedAt => _data.savedAt;

        public TimetravelerSave() : this(Path.Combine(MicrogameSave.Folder(TimetravelerGame.Id), FileName)) { }

        /// <summary>Tests: a save at another path.</summary>
        internal TimetravelerSave(string path)
        {
            _path = path;
            Load();
        }

        // ---------- changes (each one is written to disk) ----------

        public void AddCoins(int amount)
        {
            if (amount <= 0) return;
            _data.coins += amount;
            Write();
        }

        /// <returns>True if this is a new record.</returns>
        public bool ReportDistance(int meters)
        {
            if (meters <= _data.bestMeters) return false;
            _data.bestMeters = meters;
            Write();
            return true;
        }

        public int LevelBestCoins(TimetravelerLevel level)
        {
            foreach (var l in _data.levelCoins) if (l.key == level.Key) return l.coins;
            return 0;
        }

        public bool IsDone(TimetravelerLevel level) => level.Order < LevelsDone;
        public bool IsOpen(TimetravelerLevel level) => level.Order <= LevelsDone;

        public void CompleteLevel(TimetravelerLevel level, int coins)
        {
            if (level.Order + 1 > _data.levelsDone) _data.levelsDone = level.Order + 1;
            if (coins > LevelBestCoins(level))
            {
                _data.levelCoins.RemoveAll(l => l.key == level.Key);
                _data.levelCoins.Add(new LevelCoins { key = level.Key, coins = coins });
            }
            Write();
        }

        /// <summary>Remembers the map position (written with the next save, see <see cref="Flush"/>).</summary>
        public void SetMapPosition(Vector2 position)
        {
            _data.hasMapPosition = true;
            _data.mapX = position.x;
            _data.mapY = position.y;
            _dirty = true;
        }

        /// <summary>The slow-motion tutorial for this obstacle type was already shown (once per save).</summary>
        public bool TutorialSeen(string key) => _data.tutorialsSeen.Contains(key);

        public void MarkTutorialSeen(string key)
        {
            if (string.IsNullOrEmpty(key) || TutorialSeen(key)) return;
            _data.tutorialsSeen.Add(key);
            Write();
        }

        /// <summary>Dev hotkey: every story level open.</summary>
        internal void DevUnlockAll()
        {
            _data.levelsDone = TimetravelerLevels.All.Length;
            Write();
        }

        /// <summary>Dev hotkey: show every slow-motion tutorial again.</summary>
        internal void DevResetTutorials()
        {
            _data.tutorialsSeen.Clear();
            Write();
        }

        /// <summary>Counts play time (written with the next save).</summary>
        public void AddPlayTime(float seconds)
        {
            if (seconds <= 0f) return;
            _data.playSeconds += seconds;
            _dirty = true;
        }

        /// <summary>Writes pending small changes (map position, play time) if there are any.</summary>
        public void Flush()
        {
            if (_dirty) Write();
        }

        /// <summary>Starts over: everything back to zero, and the file says so straight away.</summary>
        public void NewGame()
        {
            _data = new Data();
            Write();
            TimetravelerLog.Info("New game: progress reset.");
        }

        /// <summary>Endless mode opens after the first timeline is finished (GDD); always open in the editor for testing.</summary>
        public bool EndlessOpen => Application.isEditor || LevelsDone >= TimetravelerLevels.Era1.Length;

        // ---------- file ----------

        bool _dirty;

        void Load()
        {
            if (File.Exists(_path))
            {
                try
                {
                    var loaded = JsonUtility.FromJson<Data>(File.ReadAllText(_path));
                    if (loaded == null) throw new InvalidDataException("empty save file");
                    if (loaded.levelCoins == null) loaded.levelCoins = new List<LevelCoins>();
                    if (loaded.tutorialsSeen == null) loaded.tutorialsSeen = new List<string>();
                    _data = loaded;
                    TimetravelerLog.Info($"Save loaded: {Coins} coins, best {BestMeters} m, {LevelsDone} levels done (saved {SavedAt}).");
                    return;
                }
                catch (Exception e)
                {
                    // Never crash on a broken file: keep it aside for a look, start clean.
                    TimetravelerLog.Warn($"Save file unreadable ({e.Message}); kept as {FileName}.broken, starting fresh.");
                    try { File.Copy(_path, _path + ".broken", true); } catch (IOException) { }
                    _data = new Data();
                    Write();
                    return;
                }
            }
            if (MigrateFromPlayerPrefs()) Write();
        }

        /// <summary>Progress from older builds (PlayerPrefs) moves into the file once; the old keys are removed.</summary>
        bool MigrateFromPlayerPrefs()
        {
            string K(string key) => MicrogameSave.Key(TimetravelerGame.Id, key);
            if (!PlayerPrefs.HasKey(K("coins")) && !PlayerPrefs.HasKey(K("levels_done")) && !PlayerPrefs.HasKey(K("runner_best")))
                return false;
            _data.coins = PlayerPrefs.GetInt(K("coins"), 0);
            _data.bestMeters = PlayerPrefs.GetInt(K("runner_best"), 0);
            _data.levelsDone = PlayerPrefs.GetInt(K("levels_done"), 0);
            PlayerPrefs.DeleteKey(K("coins"));
            PlayerPrefs.DeleteKey(K("runner_best"));
            PlayerPrefs.DeleteKey(K("levels_done"));
            foreach (var level in TimetravelerLevels.All)
            {
                var key = K("level_coins_" + level.Key);
                if (!PlayerPrefs.HasKey(key)) continue;
                _data.levelCoins.Add(new LevelCoins { key = level.Key, coins = PlayerPrefs.GetInt(key) });
                PlayerPrefs.DeleteKey(key);
            }
            PlayerPrefs.Save();
            TimetravelerLog.Info($"Old progress moved into {FileName}: {Coins} coins, {LevelsDone} levels done.");
            return true;
        }

        void Write()
        {
            _data.version = Version;
            _data.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            try
            {
                // Write a temporary file first, then swap it in: a crash mid-write never leaves half a save.
                var tmp = _path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(_data, true));
                if (File.Exists(_path)) File.Delete(_path);
                File.Move(tmp, _path);
                _dirty = false;
            }
            catch (IOException e)
            {
                TimetravelerLog.Warn($"Could not save progress: {e.Message}");
            }
            catch (UnauthorizedAccessException e)
            {
                TimetravelerLog.Warn($"Could not save progress: {e.Message}");
            }
        }
    }
}
