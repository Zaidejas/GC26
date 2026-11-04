using System;
using System.Collections.Generic;
using System.Linq;

namespace Lines.Core
{
    /// <summary>
    /// Points for cleared lines (GDD → Scoring). Same rules as clearLines() in
    /// docs/prototype/lines-prototype.html:
    /// - each line scores by its length: 5→10, 6→14, 7→20, 8→28, 9→36;
    /// - lines made by the player's move: the total is multiplied by the number of lines;
    /// - lines made by spawned balls: no multiplier;
    /// - crossing lines: the shared ball counts in the length of both lines.
    /// </summary>
    public static class ScoreCalculator
    {
        /// <summary>Points for one line of the given length (5 to 9 balls).</summary>
        public static int PointsForLine(int length)
        {
            switch (length)
            {
                case 5: return 10;
                case 6: return 14;
                case 7: return 20;
                case 8: return 28;
                case 9: return 36; // a 9×9 board can't hold a longer line
                default:
                    throw new ArgumentOutOfRangeException(nameof(length), $"A line has 5 to 9 balls, not {length}.");
            }
        }

        /// <summary>Points for lines completed by the player's move: sum × number of lines.</summary>
        public static int ForMove(IReadOnlyCollection<Line> lines) => ForMove(Lengths(lines));

        /// <summary>Points for lines completed by spawned balls: just the sum.</summary>
        public static int ForSpawn(IReadOnlyCollection<Line> lines) => ForSpawn(Lengths(lines));

        public static int ForMove(IReadOnlyCollection<int> lineLengths) => Sum(lineLengths) * lineLengths.Count;

        public static int ForSpawn(IReadOnlyCollection<int> lineLengths) => Sum(lineLengths);

        static int Sum(IEnumerable<int> lineLengths) => lineLengths.Sum(PointsForLine);

        static List<int> Lengths(IReadOnlyCollection<Line> lines)
        {
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            return lines.Select(l => l.Length).ToList();
        }
    }
}
