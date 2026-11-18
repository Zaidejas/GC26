using System;
using System.Linq;

namespace GC26.Minesweeper
{
    // The lines shown under "You hit a red flag!". They come from
    // Resources/gc26_minesweeper/loss_lines.txt, one line per entry, so new lines can be
    // added by editing the file — no code changes needed.
    public static class MinesweeperLossLines
    {
        // Splits the file into lines; skips empty lines and comments starting with "#".
        public static string[] Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return new string[0];
            return text.Split('\n')
                       .Select(line => line.Trim())          // also removes the '\r' of Windows line endings
                       .Where(line => line.Length > 0 && !line.StartsWith("#"))
                       .ToArray();
        }

        // One random line, or "" if there are none.
        public static string PickRandom(string[] lines, Random random)
        {
            if (lines == null || lines.Length == 0) return "";
            return lines[random.Next(lines.Length)];
        }
    }
}
