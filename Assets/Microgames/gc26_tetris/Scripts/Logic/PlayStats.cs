using System;
using System.Collections.Generic;
using System.Globalization;

namespace GC26.Tetris.Logic
{
    /// <summary>What the statistics screen counts, over all games.</summary>
    public enum PlayStat
    {
        MarathonGames,
        Battles,
        Seconds,         // time spent actually playing, pauses and menus not counted
        Pieces,
        Lines,
        Fours,           // clears of four lines at once
        BestCombo,
        BestLevel,
        Bombs,
        Lightnings,
        Bots,
        AttacksBlocked,
        GarbageRows,     // garbage rows the bots managed to push in
        Coins,           // every coin earned: battles and achievement rewards
    }

    /// <summary>
    /// Lifetime counters for the statistics screen. Most add up; the "best" ones keep the highest value.
    /// Saved as one line of "key=value" pairs, so new counters can be added without breaking old saves.
    /// </summary>
    public sealed class PlayStats
    {
        /// <summary>Saved names, in enum order. Never rename one, or that counter starts again from zero.</summary>
        static readonly string[] Keys =
        {
            "marathons", "battles", "seconds", "pieces", "lines", "fours", "combo", "level",
            "bombs", "lightnings", "bots", "blocked", "garbage", "coins",
        };

        public static readonly int Count = Enum.GetValues(typeof(PlayStat)).Length;

        readonly long[] values = new long[Count];

        public static bool IsBest(PlayStat stat)
        {
            return stat == PlayStat.BestCombo || stat == PlayStat.BestLevel;
        }

        public static string KeyOf(PlayStat stat)
        {
            return Keys[(int)stat];
        }

        public long Get(PlayStat stat)
        {
            return values[(int)stat];
        }

        /// <summary>Adds to a counter, or offers a new best for the "best" ones. Negative amounts are ignored.</summary>
        public void Add(PlayStat stat, long amount)
        {
            if (amount <= 0)
                return;
            int index = (int)stat;
            values[index] = IsBest(stat) ? Math.Max(values[index], amount) : values[index] + amount;
        }

        /// <summary>Makes sure a counter is at least this high, whatever its kind. Used to carry over older numbers.</summary>
        public void Raise(PlayStat stat, long atLeast)
        {
            int index = (int)stat;
            values[index] = Math.Max(values[index], atLeast);
        }

        public string Serialize()
        {
            var parts = new List<string>();
            for (int i = 0; i < Count; i++)
                parts.Add(Keys[i] + "=" + values[i].ToString(CultureInfo.InvariantCulture));
            return string.Join(";", parts);
        }

        /// <summary>Reads what Serialize wrote. Unknown names and damaged numbers are skipped, never thrown.</summary>
        public static PlayStats Parse(string text)
        {
            var result = new PlayStats();
            if (string.IsNullOrEmpty(text))
                return result;
            foreach (string part in text.Split(';'))
            {
                int equals = part.IndexOf('=');
                if (equals <= 0)
                    continue;
                int index = Array.IndexOf(Keys, part.Substring(0, equals).Trim());
                if (index < 0)
                    continue;
                if (long.TryParse(part.Substring(equals + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) && value > 0)
                    result.values[index] = value;
            }
            return result;
        }
    }
}
