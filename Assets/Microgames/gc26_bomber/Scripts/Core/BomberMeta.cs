using System;
using System.Collections.Generic;
using GC26.Core;
using UnityEngine;

namespace GC26.Bomber
{
    /// <summary>
    /// Progress kept between runs (prototype "meta"): Data, unlocks, upgrades, rescued survivors, achievements, stats.
    /// Saved as JSON in PlayerPrefs under MicrogameSave.Key(gc26_bomber, "meta").
    /// </summary>
    [Serializable]
    public sealed class BomberMeta
    {
        [Serializable] public sealed class Level { public string Id; public int L; }

        public int Data, Best;
        public bool TutorialDone;
        public string Char = "spark", StartWeapon = "blaster";
        public List<string> Ach = new List<string>(), Rescued = new List<string>(), Weapons = new List<string>(), Chars = new List<string>();
        public List<Level> Ups = new List<Level>(), CharUps = new List<Level>();
        public int Runs, Wins, Kills, Overheats, NoDmg, BestSector = 1, Bosses, DataEarned, Fusions, BestCombo;

        static string SaveKey => MicrogameSave.Key(BomberGame.Id, "meta");

        public static BomberMeta Load()
        {
            try
            {
                var raw = PlayerPrefs.GetString(SaveKey, "");
                if (!string.IsNullOrEmpty(raw)) { var m = JsonUtility.FromJson<BomberMeta>(raw); if (m != null) return m; }
            }
            catch (Exception e) { Debug.LogWarning($"[{BomberGame.Id}] save is damaged, starting fresh: {e.Message}"); }
            return new BomberMeta();
        }

        public void Save() { PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(this)); PlayerPrefs.Save(); }

        // ---- levels ----
        static int Get(List<Level> l, string id) { foreach (var x in l) if (x.Id == id) return x.L; return 0; }
        static void Inc(List<Level> l, string id) { foreach (var x in l) if (x.Id == id) { x.L++; return; } l.Add(new Level { Id = id, L = 1 }); }
        public int Up(string id) => Get(Ups, id);
        public void AddUp(string id) => Inc(Ups, id);
        public int CharUp(string ch, string k) => Get(CharUps, ch + "." + k);
        public void AddCharUp(string ch, string k) => Inc(CharUps, ch + "." + k);
        public int CharUpSum(string ch) { int s = 0; foreach (var u in BomberMetaData.CharUps) s += CharUp(ch, u.Id); return s; }

        // ---- unlocks ----
        public bool HasAch(string id) => Ach.Contains(id);
        public bool IsRescued(string id) => Rescued.Contains(id);

        public bool CharUnlocked(string id)
        {
            var c = BomberMetaData.Char(id);
            if (c.Id != id) return false;
            if (c.Rescue) return IsRescued(id);
            if (c.Ach != null) return HasAch(c.Ach);
            return c.Cost == 0 || Chars.Contains(id);
        }

        public bool WeaponUnlocked(string id)
        {
            if (!BomberMetaData.WeaponUnlock.TryGetValue(id, out var u)) return Array.IndexOf(BomberItems.BaseWeapons, id) >= 0;
            return u.ach != null ? HasAch(u.ach) : Weapons.Contains(id);
        }

        public bool CompUnlocked(string id) => id != "striker" || HasAch("rooms12");
    }
}
