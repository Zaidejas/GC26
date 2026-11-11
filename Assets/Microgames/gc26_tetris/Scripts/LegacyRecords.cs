using UnityEngine;

namespace GC26.Tetris
{
    /// <summary>
    /// One-time move of saves from the standalone BlockDrop builds ("BlockDrop.X") to the GC26 keys ("gc26_tetris_X"),
    /// so records, coins and the sound setting survive the rename. Does nothing once the old keys are gone.
    /// </summary>
    static class LegacyRecords
    {
        const string OldPrefix = "BlockDrop.";
        const string OldTestPrefix = "BlockDrop.Test.";
        static readonly string[] IntNames = { "BestScore", "Coins", "Muted" };
        static readonly string[] StringNames = { "MarathonTable", "BattleTable" };

        public static void Move()
        {
            bool changed = false;
            foreach (string name in IntNames)
            {
                string oldKey = OldPrefix + name;
                if (!PlayerPrefs.HasKey(oldKey))
                    continue;
                string newKey = TetrisGame.Id + "_" + name;
                if (!PlayerPrefs.HasKey(newKey))
                    PlayerPrefs.SetInt(newKey, PlayerPrefs.GetInt(oldKey));
                PlayerPrefs.DeleteKey(oldKey);
                changed = true;
            }
            foreach (string name in StringNames)
            {
                string oldKey = OldPrefix + name;
                if (!PlayerPrefs.HasKey(oldKey))
                    continue;
                string newKey = TetrisGame.Id + "_" + name;
                if (!PlayerPrefs.HasKey(newKey))
                    PlayerPrefs.SetString(newKey, PlayerPrefs.GetString(oldKey));
                PlayerPrefs.DeleteKey(oldKey);
                changed = true;
            }
            // Old test-run keys are not worth keeping.
            foreach (string name in new[] { "BestScore", "Coins", "MarathonTable", "BattleTable" })
            {
                if (PlayerPrefs.HasKey(OldTestPrefix + name))
                {
                    PlayerPrefs.DeleteKey(OldTestPrefix + name);
                    changed = true;
                }
            }
            if (changed)
            {
                PlayerPrefs.Save();
                Debug.Log("[BlockDrop] Moved old BlockDrop saves to gc26_tetris_ keys");
            }
        }
    }
}
