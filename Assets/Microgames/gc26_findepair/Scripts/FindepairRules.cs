using System;
using System.Collections.Generic;

namespace GC26.Findepair
{
    /// <summary>Result of one finished round.</summary>
    public readonly struct FindepairResult
    {
        public readonly int Moves;
        public readonly int TimeMs;
        public readonly int Stars;

        public FindepairResult(int moves, int timeMs)
        {
            Moves = moves;
            TimeMs = timeMs;
            Stars = FindepairRules.Stars(moves);
        }
    }

    /// <summary>Pure game rules, kept apart from Unity objects so they can be unit-tested.</summary>
    public static class FindepairRules
    {
        /// <summary>Stars from the GDD: 3 up to 9 moves, 2 for 10-13, 1 for more (minimum is 6 with 6 pairs).</summary>
        public static int Stars(int moves) => moves <= 9 ? 3 : moves <= 13 ? 2 : 1;

        /// <summary>A record is fewer moves; on a tie, less time. bestMoves &lt; 0 means no record yet.</summary>
        public static bool IsRecord(int moves, int timeMs, int bestMoves, int bestTimeMs) =>
            bestMoves < 0 || moves < bestMoves || (moves == bestMoves && timeMs < bestTimeMs);

        /// <summary>Every pair twice, shuffled with Fisher-Yates.</summary>
        public static List<T> BuildDeck<T>(IReadOnlyList<T> pairs, Random rng)
        {
            var deck = new List<T>(pairs.Count * 2);
            foreach (var p in pairs) { deck.Add(p); deck.Add(p); }
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }
            return deck;
        }

        /// <summary>Grid shape for the board: 3 columns in portrait, 4 in landscape.</summary>
        public static (int cols, int rows) Grid(int cardCount, bool portrait)
        {
            int cols = portrait ? 3 : 4;
            int rows = (cardCount + cols - 1) / cols;
            return (cols, rows);
        }

        /// <summary>mm:ss</summary>
        public static string FormatTime(float seconds)
        {
            int s = Math.Max(0, (int)seconds);
            return $"{s / 60:00}:{s % 60:00}";
        }
    }
}
