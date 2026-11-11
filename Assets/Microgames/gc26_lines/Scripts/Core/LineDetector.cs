using System;
using System.Collections.Generic;
using System.Linq;

namespace GC26.Lines.Core
{
    /// <summary>A run of 5 or more balls of one color in a row, column or diagonal.</summary>
    public sealed class Line
    {
        public BallColor Color { get; }
        /// <summary>The cells of the line, sorted row by row from the top-left.</summary>
        public IReadOnlyList<Cell> Cells { get; }
        public int Length => Cells.Count;

        public Line(BallColor color, IEnumerable<Cell> cells)
        {
            Color = color;
            Cells = cells.OrderBy(c => c.Row).ThenBy(c => c.Column).ToList();
        }

        internal string Key => string.Join(";", Cells);
        public override string ToString() => $"{Color} x{Length}: {Key}";
    }

    /// <summary>
    /// Finds lines of <see cref="GameConfig.MinLineLength"/>+ balls of one color through given cells,
    /// in all 4 directions: row, column and both diagonals.
    /// Same rule as linesThrough() in docs/prototype/lines-prototype.html.
    /// </summary>
    public static class LineDetector
    {
        // Row, column, diagonal down-right, diagonal up-right.
        static readonly (int column, int row)[] Directions = { (1, 0), (0, 1), (1, 1), (1, -1) };

        /// <summary>All lines that pass through <paramref name="cell"/>. Empty if the cell is empty.</summary>
        public static List<Line> FindLinesThrough(Board board, Cell cell)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            var lines = new List<Line>();
            var color = board.Get(cell);
            if (color == BallColor.None) return lines;

            foreach (var (dc, dr) in Directions)
            {
                var run = new List<Cell> { cell };
                foreach (var sign in new[] { 1, -1 })
                {
                    var next = new Cell(cell.Column + dc * sign, cell.Row + dr * sign);
                    while (board.IsInside(next) && board.Get(next) == color)
                    {
                        run.Add(next);
                        next = new Cell(next.Column + dc * sign, next.Row + dr * sign);
                    }
                }
                if (run.Count >= GameConfig.MinLineLength)
                    lines.Add(new Line(color, run));
            }
            return lines;
        }

        /// <summary>
        /// All lines through any of <paramref name="cells"/> (e.g. the 3 spawned balls),
        /// each line listed once even if several of the cells are on it.
        /// </summary>
        public static List<Line> FindLinesThrough(Board board, IEnumerable<Cell> cells)
        {
            var seen = new HashSet<string>();
            var lines = new List<Line>();
            foreach (var cell in cells)
            foreach (var line in FindLinesThrough(board, cell))
                if (seen.Add(line.Key))
                    lines.Add(line);
            return lines;
        }

        /// <summary>The cells to clear: every cell of every line, a shared cell listed once.</summary>
        public static List<Cell> CellsToClear(IEnumerable<Line> lines) =>
            lines.SelectMany(l => l.Cells).Distinct().ToList();
    }
}
