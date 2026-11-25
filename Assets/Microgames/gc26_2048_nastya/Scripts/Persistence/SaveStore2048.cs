using System;
using System.IO;
using GC26.Core;
using UnityEngine;

namespace GC26._2048Nastya
{
    /// <summary>Contents of game.json (TDD 8.2). Tile Ids are not saved; they are re-created on load.</summary>
    [Serializable]
    public sealed class SaveData2048
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public int size;
        public int[] cells;   // row by row from the top-left, 0 = empty
        public int score;
        public int moves;
        public bool wonShown;
    }

    /// <summary>
    /// Saves (TDD 8): the game in progress in game.json inside <see cref="MicrogameSave.Folder"/>,
    /// the best score and the small flags in PlayerPrefs with the game prefix.
    /// Never throws: a missing or broken save just means "start a new game" (with a warning in the log).
    /// </summary>
    public sealed class SaveStore2048
    {
        public const string FileName = "game.json";

        readonly string _folder;
        readonly string _bestKey, _hintKey, _soundKey;

        /// <param name="folder">Where game.json lives.</param>
        /// <param name="keyPrefix">PlayerPrefs prefix, for example "gc26_2048_nastya_" (tests use their own).</param>
        public SaveStore2048(string folder, string keyPrefix)
        {
            _folder = folder;
            _bestKey = keyPrefix + "best";
            _hintKey = keyPrefix + "hint";
            _soundKey = keyPrefix + "sound";
        }

        /// <summary>The real save location of this game.</summary>
        public static SaveStore2048 ForGame() =>
            new SaveStore2048(MicrogameSave.Folder(Game2048Nastya.Id), MicrogameSave.Key(Game2048Nastya.Id, ""));

        public string GamePath => Path.Combine(_folder, FileName);
        string TempPath => GamePath + ".tmp";

        // ---------- game in progress ----------

        /// <summary>Writes the game in progress. A finished game is not resumable, so it removes the save instead.</summary>
        public void SaveGame(GameState2048 state)
        {
            if (state == null) return;
            SaveBest(state.Best);
            if (state.IsOver)
            {
                ClearGame();
                return;
            }

            var data = new SaveData2048
            {
                size = state.Board.Size,
                cells = state.Board.ToValues(),
                score = state.Score,
                moves = state.Moves,
                wonShown = state.WonShown,
            };
            try
            {
                Directory.CreateDirectory(_folder);
                // Safe write: a crash in the middle leaves the old game.json untouched.
                File.WriteAllText(TempPath, JsonUtility.ToJson(data));
                if (File.Exists(GamePath)) File.Replace(TempPath, GamePath, null);
                else File.Move(TempPath, GamePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[{Game2048Nastya.Id}] could not save the game: {e.Message}");
            }
        }

        /// <summary>The saved game, or null when there is none or it is broken (then a new game should start).</summary>
        public GameState2048 LoadGame(int best)
        {
            string json;
            try
            {
                if (!File.Exists(GamePath)) return null;
                json = File.ReadAllText(GamePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[{Game2048Nastya.Id}] could not read the save: {e.Message}");
                return null;
            }

            if (!TryParse(json, out var data, out string problem))
            {
                Debug.LogWarning($"[{Game2048Nastya.Id}] ignoring the saved game: {problem}");
                return null;
            }

            var state = new GameState2048(Board2048.FromValues(data.size, data.cells),
                data.score, best, data.moves, data.wonShown);
            return state.IsOver ? null : state; // a finished game is never resumed
        }

        public void ClearGame()
        {
            try
            {
                if (File.Exists(GamePath)) File.Delete(GamePath);
                if (File.Exists(TempPath)) File.Delete(TempPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[{Game2048Nastya.Id}] could not delete the save: {e.Message}");
            }
        }

        public bool HasGame => File.Exists(GamePath);

        /// <summary>Validation on load (TDD 8.2): version, size, cell count, powers of two, no negative numbers.</summary>
        public static bool TryParse(string json, out SaveData2048 data, out string problem)
        {
            data = null;
            problem = null;
            if (string.IsNullOrWhiteSpace(json)) { problem = "empty file"; return false; }
            try
            {
                data = JsonUtility.FromJson<SaveData2048>(json);
            }
            catch (Exception e)
            {
                problem = "not valid JSON: " + e.Message;
                return false;
            }

            if (data == null) problem = "not valid JSON";
            else if (data.version != SaveData2048.CurrentVersion) problem = $"version {data.version}";
            else if (data.size < Board2048.MinSize || data.size > Board2048.MaxSize) problem = $"size {data.size}";
            else if (data.cells == null || data.cells.Length != data.size * data.size)
                problem = $"{data.cells?.Length ?? 0} cells for size {data.size}";
            else if (data.score < 0 || data.moves < 0) problem = "negative score or moves";
            else
            {
                foreach (int v in data.cells)
                {
                    if (v != 0 && !Board2048.IsPowerOfTwo(v)) { problem = $"bad tile value {v}"; break; }
                }
            }

            if (problem == null) return true;
            data = null;
            return false;
        }

        // ---------- small values in PlayerPrefs ----------

        public int LoadBest() => Math.Max(0, PlayerPrefs.GetInt(_bestKey, 0));

        /// <summary>Keeps the larger of the stored and the given best score.</summary>
        public void SaveBest(int best)
        {
            if (best > PlayerPrefs.GetInt(_bestKey, 0)) PlayerPrefs.SetInt(_bestKey, best);
        }

        /// <summary>The first-launch hint was already shown.</summary>
        public bool HintShown
        {
            get => PlayerPrefs.GetInt(_hintKey, 0) != 0;
            set { PlayerPrefs.SetInt(_hintKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Sound setting for the pause menu (M5). On by default.</summary>
        public bool SoundOn
        {
            get => PlayerPrefs.GetInt(_soundKey, 1) != 0;
            set { PlayerPrefs.SetInt(_soundKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Writes PlayerPrefs to disk now (on exit and when the app goes to the background).</summary>
        public void Flush() => PlayerPrefs.Save();

        /// <summary>Removes everything this store wrote (tests and a future "reset" button).</summary>
        public void DeleteAll()
        {
            ClearGame();
            PlayerPrefs.DeleteKey(_bestKey);
            PlayerPrefs.DeleteKey(_hintKey);
            PlayerPrefs.DeleteKey(_soundKey);
        }
    }
}
