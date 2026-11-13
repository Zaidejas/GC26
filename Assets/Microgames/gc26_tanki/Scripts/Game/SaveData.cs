using System.Collections.Generic;
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

        // The garage: the tank in use and the ones bought (by PlayerTank number).
        public int Tank;
        public List<int> Owned = new List<int> { (int)PlayerTank.Guard };

        // Stars per map (0 to 3), the open assault and the awards already paid.
        public int[] Stars = new int[Levels.All.Length];
        public int AssaultUnlocked;
        public int[] AssaultStars = new int[Levels.Assault.Length];
        public List<string> Awards = new List<string>();
        public bool TutorialDone;

        // Counters for the awards.
        public int Kills;
        public int Bosses;
        public int Crates;
        public int MinesShot;
        public int Abilities;
        public int Turrets;
        public int Flawless;

        public PlayerTank ChosenTank => (PlayerTank)Tank;

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
            data.AssaultUnlocked = Mathf.Clamp(data.AssaultUnlocked, 0, Levels.Assault.Length - 1);
            data.Stars = Resize(data.Stars, Levels.All.Length);
            data.AssaultStars = Resize(data.AssaultStars, Levels.Assault.Length);
            if (data.Owned == null) data.Owned = new List<int>();
            if (!data.Owned.Contains((int)PlayerTank.Guard)) data.Owned.Insert(0, (int)PlayerTank.Guard);
            if (!data.Owned.Contains(data.Tank)) data.Tank = (int)PlayerTank.Guard;
            if (data.Awards == null) data.Awards = new List<string>();
            return data;
        }

        static int[] Resize(int[] values, int length)
        {
            var result = new int[length];
            if (values != null)
                for (int i = 0; i < Mathf.Min(length, values.Length); i++) result[i] = Mathf.Clamp(values[i], 0, 3);
            return result;
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

        public bool Owns(PlayerTank tank) => Owned.Contains((int)tank);

        /// <summary>Buys a tank from the garage and takes it into use.</summary>
        public bool BuyTank(PlayerTank tank)
        {
            int price = TankDef.For(tank).Price;
            if (Owns(tank) || Parts < price) return false;
            Parts -= price;
            Owned.Add((int)tank);
            Tank = (int)tank;
            Save();
            return true;
        }

        public void ChooseTank(PlayerTank tank)
        {
            if (!Owns(tank)) return;
            Tank = (int)tank;
            Save();
        }

        public int CampaignStars => Sum(Stars);
        public int TotalStars => Sum(Stars) + Sum(AssaultStars);
        public int AssaultWins
        {
            get
            {
                int count = 0;
                foreach (int stars in AssaultStars) if (stars > 0) count++;
                return count;
            }
        }

        static int Sum(int[] values)
        {
            int total = 0;
            foreach (int value in values) total += value;
            return total;
        }
    }
}
