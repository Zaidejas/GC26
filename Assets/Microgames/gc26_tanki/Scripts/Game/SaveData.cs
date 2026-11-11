using GC26.Core;
using GC26.Tanki.Rules;
using UnityEngine;

namespace GC26.Tanki.Game
{
    /// <summary>Everything kept between launches, stored as one JSON string in PlayerPrefs.</summary>
    [System.Serializable]
    public sealed class SaveData
    {
        static readonly string Key = MicrogameSave.Key(TankiGame.Id, "save");

        // How far the campaign is open: the index of the highest level the player may start.
        public int UnlockedLevel;
        public int Parts;
        public int[] Upgrades = new int[4];
        public int BestScore;
        public int BestWave;
        public int Difficulty = (int)Rules.Difficulty.Normal;
        public bool SoundOn = true;
        public bool VibrationOn = true;

        public static SaveData Load()
        {
            string json = PlayerPrefs.GetString(Key, "");
            SaveData data = null;
            if (json.Length > 0)
            {
                try { data = JsonUtility.FromJson<SaveData>(json); }
                catch (System.ArgumentException) { data = null; }
            }
            if (data == null) data = new SaveData();
            if (data.Upgrades == null || data.Upgrades.Length != 4) data.Upgrades = new int[4];
            data.UnlockedLevel = Mathf.Clamp(data.UnlockedLevel, 0, Levels.All.Length - 1);
            return data;
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public PlayerStats Stats()
        {
            var stats = new PlayerStats();
            for (int i = 0; i < 4; i++) stats.Levels[i] = Mathf.Clamp(Upgrades[i], 0, Tuning.WorkshopMaxLevel);
            return stats;
        }

        /// <summary>The price of the next level of a stat, or -1 when it is at the top.</summary>
        public int NextCost(Stat stat)
        {
            int level = Upgrades[(int)stat];
            return level >= Tuning.WorkshopMaxLevel ? -1 : Tuning.WorkshopCost[level];
        }

        public bool Buy(Stat stat)
        {
            int cost = NextCost(stat);
            if (cost < 0 || Parts < cost) return false;
            Parts -= cost;
            Upgrades[(int)stat]++;
            Save();
            return true;
        }
    }
}
