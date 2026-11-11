using GC26.Core;
using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>
    /// Saved progress: coin bank, best endless distance, finished levels.
    /// PlayerPrefs keys always go through MicrogameSave.Key, so they start with "gc26_timetraveler_" (rules).
    /// One instance per game run (no static state).
    /// </summary>
    public class TimetravelerSave
    {
        static string K(string key) => MicrogameSave.Key(TimetravelerGame.Id, key);

        public int Coins { get; private set; }
        public int BestMeters { get; private set; }
        /// <summary>How many levels of the story are finished, in order (level N is open when N &lt;= this).</summary>
        public int LevelsDone { get; private set; }

        public TimetravelerSave()
        {
            Coins = PlayerPrefs.GetInt(K("coins"), 0);
            BestMeters = PlayerPrefs.GetInt(K("runner_best"), 0);
            LevelsDone = PlayerPrefs.GetInt(K("levels_done"), 0);
            TimetravelerLog.Info($"Save loaded: {Coins} coins, best {BestMeters} m, {LevelsDone} levels done.");
        }

        public void AddCoins(int amount)
        {
            if (amount <= 0) return;
            Coins += amount;
            PlayerPrefs.SetInt(K("coins"), Coins);
            PlayerPrefs.Save();
        }

        /// <returns>True if this is a new record.</returns>
        public bool ReportDistance(int meters)
        {
            if (meters <= BestMeters) return false;
            BestMeters = meters;
            PlayerPrefs.SetInt(K("runner_best"), BestMeters);
            PlayerPrefs.Save();
            return true;
        }

        public int LevelBestCoins(TimetravelerLevel level) => PlayerPrefs.GetInt(K("level_coins_" + level.Key), 0);

        public bool IsDone(TimetravelerLevel level) => level.Order < LevelsDone;
        public bool IsOpen(TimetravelerLevel level) => level.Order <= LevelsDone;

        public void CompleteLevel(TimetravelerLevel level, int coins)
        {
            if (level.Order + 1 > LevelsDone)
            {
                LevelsDone = level.Order + 1;
                PlayerPrefs.SetInt(K("levels_done"), LevelsDone);
            }
            if (coins > LevelBestCoins(level)) PlayerPrefs.SetInt(K("level_coins_" + level.Key), coins);
            PlayerPrefs.Save();
        }

        /// <summary>Endless mode opens after the first timeline is finished (GDD); always open in the editor for testing.</summary>
        public bool EndlessOpen => Application.isEditor || LevelsDone >= TimetravelerLevels.Era1.Length;
    }
}
