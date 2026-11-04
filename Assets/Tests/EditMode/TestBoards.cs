using System;
using Lines.Core;

namespace Lines.Tests
{
    /// <summary>
    /// Builds boards from text pictures, one string per row (top row first):
    /// '.' is an empty cell; R O Y G C B P are Red, Orange, Yellow, Green, Cyan, Blue, Purple.
    /// </summary>
    public static class TestBoards
    {
        public static Board FromRows(params string[] rows)
        {
            var board = new Board(rows.Length);
            for (int row = 0; row < rows.Length; row++)
            {
                if (rows[row].Length != rows.Length)
                    throw new ArgumentException($"Row {row} has {rows[row].Length} cells, expected {rows.Length}.");
                for (int column = 0; column < rows.Length; column++)
                {
                    var color = ToColor(rows[row][column]);
                    if (color != BallColor.None)
                        board.Place(new Cell(column, row), color);
                }
            }
            return board;
        }

        static BallColor ToColor(char c)
        {
            switch (c)
            {
                case '.': return BallColor.None;
                case 'R': return BallColor.Red;
                case 'O': return BallColor.Orange;
                case 'Y': return BallColor.Yellow;
                case 'G': return BallColor.Green;
                case 'C': return BallColor.Cyan;
                case 'B': return BallColor.Blue;
                case 'P': return BallColor.Purple;
                default: throw new ArgumentException($"Unknown board symbol '{c}'.");
            }
        }
    }
}
