using GC26.Core;
using UnityEngine;

namespace GC26.Findepair
{
    /// <summary>Record and settings in PlayerPrefs, keys prefixed with the game id (gc26_findepair_...).</summary>
    public class FindepairSave
    {
        public int BestMoves = -1;
        public int BestTimeMs = -1;
        public bool Sound = true;
        public bool Music = true;
        public bool Vibro = true;

        public bool HasRecord => BestMoves >= 0;

        static string Key(string name) => MicrogameSave.Key(FindepairGame.Id, name);

        public void Load()
        {
            BestMoves = PlayerPrefs.GetInt(Key("bestMoves"), -1);
            BestTimeMs = PlayerPrefs.GetInt(Key("bestTimeMs"), -1);
            Sound = PlayerPrefs.GetInt(Key("sound"), 1) == 1;
            Music = PlayerPrefs.GetInt(Key("music"), 1) == 1;
            Vibro = PlayerPrefs.GetInt(Key("vibro"), 1) == 1;
        }

        public void Save()
        {
            PlayerPrefs.SetInt(Key("bestMoves"), BestMoves);
            PlayerPrefs.SetInt(Key("bestTimeMs"), BestTimeMs);
            PlayerPrefs.SetInt(Key("sound"), Sound ? 1 : 0);
            PlayerPrefs.SetInt(Key("music"), Music ? 1 : 0);
            PlayerPrefs.SetInt(Key("vibro"), Vibro ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>Stores the result if it beats the record. Returns true for a new record.</summary>
        public bool TrySetBest(FindepairResult r)
        {
            if (!FindepairRules.IsRecord(r.Moves, r.TimeMs, BestMoves, BestTimeMs)) return false;
            BestMoves = r.Moves;
            BestTimeMs = r.TimeMs;
            Save();
            return true;
        }

        public void ResetBest()
        {
            BestMoves = BestTimeMs = -1;
            PlayerPrefs.DeleteKey(Key("bestMoves"));
            PlayerPrefs.DeleteKey(Key("bestTimeMs"));
            PlayerPrefs.Save();
        }

        public string BestText => HasRecord
            ? $"Record: {BestMoves} moves, {FindepairRules.FormatTime(BestTimeMs / 1000f)}"
            : "";
    }
}
