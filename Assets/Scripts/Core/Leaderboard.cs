using System;
using System.Collections.Generic;
using System.Globalization;

namespace BlockDrop.Core
{
    /// <summary>The best few results of one mode, best first. Saved as plain text by the caller.</summary>
    public sealed class Leaderboard
    {
        public const int Size = 5;

        readonly bool lowerIsBetter;
        readonly List<double> entries = new List<double>();

        /// <param name="lowerIsBetter">True for times, false for scores.</param>
        public Leaderboard(bool lowerIsBetter)
        {
            this.lowerIsBetter = lowerIsBetter;
        }

        public IReadOnlyList<double> Entries { get { return entries; } }

        /// <summary>Records a result. Returns its place from 1, or 0 if it did not make the table.</summary>
        public int Add(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0.0)
                return 0;

            // A tie goes below the older result: whoever got there first keeps the place.
            int index = 0;
            while (index < entries.Count && !IsBetter(value, entries[index]))
                index++;
            if (index >= Size)
                return 0;

            entries.Insert(index, value);
            if (entries.Count > Size)
                entries.RemoveAt(Size);
            return index + 1;
        }

        bool IsBetter(double candidate, double existing)
        {
            return lowerIsBetter ? candidate < existing : candidate > existing;
        }

        public string Serialize()
        {
            var parts = new string[entries.Count];
            for (int i = 0; i < parts.Length; i++)
                parts[i] = entries[i].ToString("R", CultureInfo.InvariantCulture);
            return string.Join(";", parts);
        }

        /// <summary>Reads saved text. Anything unreadable is skipped, so bad data can't break the game.</summary>
        public static Leaderboard Parse(string text, bool lowerIsBetter)
        {
            var board = new Leaderboard(lowerIsBetter);
            if (string.IsNullOrEmpty(text))
                return board;
            foreach (string part in text.Split(';'))
            {
                if (double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                    board.Add(value);
            }
            return board;
        }
    }
}
