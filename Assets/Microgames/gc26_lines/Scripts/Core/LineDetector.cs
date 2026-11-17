using System;
using System.Collections.Generic;
using System.Linq;

namespace GC26.Lines.Core
{
    /// <summary>
    /// A run of 5 or more balls of one color in a row, column or diagonal (rainbows count as that color;
    /// <see cref="Color"/> is <see cref="BallColor.Rainbow"/> only for a line made of rainbows alone).
    /// </summary>
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
    /// in all 4 directions: row, column and both diagonals. L14: a rainbow fits any line, so a rainbow in the
    /// middle can join two different colors in one direction (each side may make its own line).
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
            var own = board.Get(cell);
            if (own == BallColor.None) return lines;

            var seen = new HashSet<string>();
            var colors = new List<BallColor>(2);
            foreach (var (dc, dr) in Directions)
            {
                colors.Clear();
                if (own != BallColor.Rainbow) colors.Add(own);
                else
                {
                    // A rainbow takes the color of the first plain ball on each side (past any other rainbows).
                    foreach (var sign in new[] { 1, -1 })
                    {
                        var next = new Cell(cell.Column + dc * sign, cell.Row + dr * sign);
                        while (board.IsInside(next) && board.Get(next) == BallColor.Rainbow)
                            next = new Cell(next.Column + dc * sign, next.Row + dr * sign);
                        if (board.IsInside(next) && board.Get(next) != BallColor.None && !colors.Contains(board.Get(next)))
                            colors.Add(board.Get(next));
                    }
                    if (colors.Count == 0) colors.Add(BallColor.Rainbow); // rainbows alone
                }
                foreach (var color in colors)
                {
                    var run = new List<Cell> { cell };
                    foreach (var sign in new[] { 1, -1 })
                    {
                        var next = new Cell(cell.Column + dc * sign, cell.Row + dr * sign);
                        while (board.IsInside(next) && Fits(board.Get(next), color))
                        {
                            run.Add(next);
                            next = new Cell(next.Column + dc * sign, next.Row + dr * sign);
                        }
                    }
                    if (run.Count < GameConfig.MinLineLength) continue;
                    var line = new Line(color, run);
                    if (seen.Add(line.Key)) lines.Add(line);
                }
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

        // A ball fits a line of <paramref name="color"/>: the same color, or a rainbow.
        static bool Fits(BallColor ball, BallColor color) => ball == color || (ball == BallColor.Rainbow && color != BallColor.None);

        /// <summary>The cells to clear: every cell of every line, a shared cell listed once.</summary>
        public static List<Cell> CellsToClear(IEnumerable<Line> lines) =>
            lines.SelectMany(l => l.Cells).Distinct().ToList();
    }
}
