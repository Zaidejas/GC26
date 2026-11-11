using System;
using System.IO;
using GC26.Core;
using UnityEngine;

namespace GC26._2048
{
    [Serializable]
    public class CheeseSaveData
    {
        public int[] cells;
        public int score;
        public int moves;
        public int cheeses;
        public bool cheeseShown;
        public int bestLevel;
    }

    /// <summary>
    /// Record, best item and sound in PlayerPrefs (keys gc26_2048_*), the current game as JSON
    /// in MicrogameSave.Folder(gc26_2048) — as RULES.md asks.
    /// </summary>
    public class CheeseStorage
    {
        readonly string gameId;

        public CheeseStorage(string gameId)
        {
            this.gameId = gameId;
        }

        string GamePath => Path.Combine(MicrogameSave.Folder(gameId), "game.json");

        public int Best
        {
            get => PlayerPrefs.GetInt(MicrogameSave.Key(gameId, "best"), 0);
            set { PlayerPrefs.SetInt(MicrogameSave.Key(gameId, "best"), value); PlayerPrefs.Save(); }
        }

        public int BestLevel
        {
            get => PlayerPrefs.GetInt(MicrogameSave.Key(gameId, "best_level"), 0);
            set { PlayerPrefs.SetInt(MicrogameSave.Key(gameId, "best_level"), value); PlayerPrefs.Save(); }
        }

        public bool Sound
        {
            get => PlayerPrefs.GetInt(MicrogameSave.Key(gameId, "sound"), 1) == 1;
            set { PlayerPrefs.SetInt(MicrogameSave.Key(gameId, "sound"), value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public CheeseSaveData LoadGame()
        {
            try
            {
                if (!File.Exists(GamePath)) return null;
                var data = JsonUtility.FromJson<CheeseSaveData>(File.ReadAllText(GamePath));
                bool valid = data != null && data.cells != null &&
                             data.cells.Length == CheeseRules.Size * CheeseRules.Size && data.moves > 0;
                return valid ? data : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[{gameId}] could not read the saved game: {e.Message}");
                return null;
            }
        }

        public bool HasGame => LoadGame() != null;

        public void SaveGame(CheeseSession s)
        {
            if (s.IsOver)
            {
                DeleteGame();
                return;
            }
            var data = new CheeseSaveData
            {
                cells = s.Board.ToArray(),
                score = s.Score,
                moves = s.MovesLeft,
                cheeses = s.Cheeses,
                cheeseShown = s.CheeseShown,
                bestLevel = s.BestLevel
            };
            try
            {
                File.WriteAllText(GamePath, JsonUtility.ToJson(data));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[{gameId}] could not save the game: {e.Message}");
            }
        }

        public void DeleteGame()
        {
            try
            {
                if (File.Exists(GamePath)) File.Delete(GamePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[{gameId}] could not delete the saved game: {e.Message}");
            }
        }
    }
}
