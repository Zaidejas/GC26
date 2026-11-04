using System;
using System.Collections.Generic;

namespace Lines.Core
{
    /// <summary>
    /// The 9×9 grid of cells, each empty or holding one ball.
    /// Only stores balls; the rules (paths, lines, spawning) live in other classes.
    /// </summary>
    public class Board
    {
        public int Size { get; }
        readonly BallColor[,] cells;

        public Board(int size = GameConfig.BoardSize)
        {
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            Size = size;
            cells = new BallColor[size, size];
        }

        public int CellCount => Size * Size;

        public bool IsInside(Cell cell) =>
            cell.Column >= 0 && cell.Column < Size && cell.Row >= 0 && cell.Row < Size;

        public BallColor Get(Cell cell)
        {
            CheckInside(cell);
            return cells[cell.Column, cell.Row];
        }

        public bool IsEmpty(Cell cell) => Get(cell) == BallColor.None;

        /// <summary>Puts a ball into an empty cell.</summary>
        public void Place(Cell cell, BallColor color)
        {
            if (color == BallColor.None) throw new ArgumentException("Can't place an empty ball.", nameof(color));
            if (!IsEmpty(cell)) throw new InvalidOperationException($"Cell {cell} is already occupied.");
            cells[cell.Column, cell.Row] = color;
        }

        /// <summary>Takes the ball out of a cell and returns its color.</summary>
        public BallColor Remove(Cell cell)
        {
            var color = Get(cell);
            if (color == BallColor.None) throw new InvalidOperationException($"Cell {cell} is empty.");
            cells[cell.Column, cell.Row] = BallColor.None;
            return color;
        }

        /// <summary>
        /// Moves a ball to an empty cell. Doesn't check for a path; the Pathfinder does that.
        /// </summary>
        public void Move(Cell from, Cell to)
        {
            if (from == to) throw new ArgumentException("A ball can't move onto itself.");
            if (!IsEmpty(to)) throw new InvalidOperationException($"Cell {to} is already occupied.");
            Place(to, Remove(from));
        }

        /// <summary>All empty cells, row by row from the top-left corner.</summary>
        public List<Cell> EmptyCells()
        {
            var result = new List<Cell>();
            for (int row = 0; row < Size; row++)
            for (int column = 0; column < Size; column++)
                if (cells[column, row] == BallColor.None)
                    result.Add(new Cell(column, row));
            return result;
        }

        public int EmptyCount
        {
            get
            {
                int count = 0;
                foreach (var color in cells)
                    if (color == BallColor.None) count++;
                return count;
            }
        }

        public bool IsFull => EmptyCount == 0;

        public void Clear() => Array.Clear(cells, 0, cells.Length);

        /// <summary>An independent copy, used for Undo snapshots and saving.</summary>
        public Board Clone()
        {
            var copy = new Board(Size);
            Array.Copy(cells, copy.cells, cells.Length);
            return copy;
        }

        void CheckInside(Cell cell)
        {
            if (!IsInside(cell)) throw new ArgumentOutOfRangeException(nameof(cell), $"Cell {cell} is outside the board.");
        }
    }
}
