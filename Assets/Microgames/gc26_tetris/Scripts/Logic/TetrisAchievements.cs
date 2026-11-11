using System;
using System.Collections.Generic;
using System.Globalization;

namespace GC26.Tetris.Logic
{
    /// <summary>What the game counts for achievements.</summary>
    public enum AchievementStat
    {
        LinesTotal,      // all lines ever cleared
        LinesAtOnce,     // most lines in one clear
        Combo,           // longest combo
        Score,           // best score in one game
        Level,           // highest level reached
        Bombs,           // bombs set off, all games
        Lightnings,      // lightnings set off, all games
        BotsTotal,       // bots beaten, all battles
        BotsInBattle,    // most bots beaten in one battle
        AttacksBlocked,  // bot attacks called off, all battles
    }

    public sealed class Achievement
    {
        public readonly string Key;
        public readonly AchievementStat Stat;
        public readonly int Target;
        public readonly int Reward;

        public Achievement(string key, AchievementStat stat, int target, int reward)
        {
            Key = key;
            Stat = stat;
            Target = target;
            Reward = reward;
        }

        /// <summary>Totals add up across games; the rest keep the best value reached.</summary>
        public bool IsTotal
        {
            get { return TetrisAchievements.IsTotal(Stat); }
        }
    }

    /// <summary>
    /// Goals that pay coins once. The game reports what happens; this class keeps the counters,
    /// tells which goals were just reached and saves itself as one short text line.
    /// </summary>
    public sealed class TetrisAchievements
    {
        /// <summary>Every achievement, in the order the list shows them. Keys are saved, so never rename one.</summary>
        public static readonly Achievement[] All =
        {
            new Achievement("first_line", AchievementStat.LinesTotal, 1, 5),
            new Achievement("four_lines", AchievementStat.LinesAtOnce, 4, 15),
            new Achievement("combo5", AchievementStat.Combo, 5, 20),
            new Achievement("combo10", AchievementStat.Combo, 10, 40),
            new Achievement("lines100", AchievementStat.LinesTotal, 100, 30),
            new Achievement("score10k", AchievementStat.Score, 10000, 25),
            new Achievement("score50k", AchievementStat.Score, 50000, 60),
            new Achievement("level10", AchievementStat.Level, 10, 40),
            new Achievement("bombs10", AchievementStat.Bombs, 10, 20),
            new Achievement("lightnings10", AchievementStat.Lightnings, 10, 20),
            new Achievement("first_bot", AchievementStat.BotsTotal, 1, 15),
            new Achievement("bots3", AchievementStat.BotsInBattle, 3, 40),
            new Achievement("blocks5", AchievementStat.AttacksBlocked, 5, 30),
        };

        static readonly int StatCount = Enum.GetValues(typeof(AchievementStat)).Length;

        readonly long[] stats = new long[StatCount];
        readonly HashSet<string> unlocked = new HashSet<string>();

        public static bool IsTotal(AchievementStat stat)
        {
            return stat == AchievementStat.LinesTotal || stat == AchievementStat.Bombs || stat == AchievementStat.Lightnings ||
                   stat == AchievementStat.BotsTotal || stat == AchievementStat.AttacksBlocked;
        }

        public long Value(AchievementStat stat)
        {
            return stats[(int)stat];
        }

        public bool IsUnlocked(Achievement achievement)
        {
            return unlocked.Contains(achievement.Key);
        }

        public int UnlockedCount
        {
            get { return unlocked.Count; }
        }

        /// <summary>Adds to a total, or offers a new best. Returns the achievements this just unlocked (often none).</summary>
        public List<Achievement> Report(AchievementStat stat, long amount)
        {
            if (amount < 0)
                amount = 0;
            int index = (int)stat;
            stats[index] = IsTotal(stat) ? stats[index] + amount : Math.Max(stats[index], amount);

            var reached = new List<Achievement>();
            foreach (Achievement achievement in All)
            {
                if (achievement.Stat == stat && stats[index] >= achievement.Target && unlocked.Add(achievement.Key))
                    reached.Add(achievement);
            }
            return reached;
        }

        /// <summary>"v=1,0,…;u=first_line,combo5": stat counters in enum order, then the unlocked keys.</summary>
        public string Serialize()
        {
            var values = new string[StatCount];
            for (int i = 0; i < StatCount; i++)
                values[i] = stats[i].ToString(CultureInfo.InvariantCulture);
            var keys = new List<string>();
            foreach (Achievement achievement in All)
            {
                if (unlocked.Contains(achievement.Key))
                    keys.Add(achievement.Key);
            }
            return "v=" + string.Join(",", values) + ";u=" + string.Join(",", keys);
        }

        /// <summary>Reads what Serialize wrote. Anything damaged is skipped, never thrown.</summary>
        public static TetrisAchievements Parse(string text)
        {
            var result = new TetrisAchievements();
            if (string.IsNullOrEmpty(text))
                return result;
            foreach (string part in text.Split(';'))
            {
                int equals = part.IndexOf('=');
                if (equals < 0)
                    continue;
                string name = part.Substring(0, equals);
                string[] values = part.Substring(equals + 1).Split(',');
                if (name == "v")
                {
                    for (int i = 0; i < StatCount && i < values.Length; i++)
                    {
                        if (long.TryParse(values[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) && value > 0)
                            result.stats[i] = value;
                    }
                }
                else if (name == "u")
                {
                    foreach (string key in values)
                    {
                        foreach (Achievement achievement in All)
                        {
                            if (achievement.Key == key)
                                result.unlocked.Add(key);
                        }
                    }
                }
            }
            return result;
        }
    }
}
