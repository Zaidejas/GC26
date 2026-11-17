using System;
using System.Collections.Generic;

namespace GC26.Lines.Core
{
    /// <summary>
    /// The 9×9 grid of cells, each empty or holding one ball; a ball can be frozen (L15: it can't move).
    /// Only stores balls; the rules (paths, lines, spawning) live in other classes.
    /// </summary>
    public class Board
    {
        public int Size { get; }
        readonly BallColor[,] cells;
        readonly bool[,] frozen;

        public Board(int size = GameConfig.BoardSize)
        {
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            Size = size;
            cells = new BallColor[size, size];
            frozen = new bool[size, size];
        }

        /// <summary>L15: the ball in <paramref name="cell"/> is frozen (it can't be selected or moved).</summary>
        public bool IsFrozen(Cell cell)
        {
            CheckInside(cell);
            return frozen[cell.Column, cell.Row];
        }

        /// <summary>How many frozen balls are on the board.</summary>
        public int FrozenCount
        {
            get
            {
                int count = 0;
                foreach (bool f in frozen) if (f) count++;
                return count;
            }
        }

        /// <summary>
        /// No ball can move: no ball that isn't frozen has an empty cell next to it (up, down, left or right).
        /// True on a full board, and when the only free cells are walled in by frozen balls (GDD → Game over).
        /// </summary>
        public bool NoMovesLeft()
        {
            for (int row = 0; row < Size; row++)
            for (int column = 0; column < Size; column++)
            {
                if (cells[column, row] == BallColor.None || frozen[column, row]) continue;
                if ((column > 0 && cells[column - 1, row] == BallColor.None) || (column < Size - 1 && cells[column + 1, row] == BallColor.None) ||
                    (row > 0 && cells[column, row - 1] == BallColor.None) || (row < Size - 1 && cells[column, row + 1] == BallColor.None))
                    return false;
            }
            return true;
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

        /// <summary>Puts a ball into an empty cell; <paramref name="isFrozen"/>: it arrives frozen (never a rainbow).</summary>
        public void Place(Cell cell, BallColor color, bool isFrozen = false)
        {
            if (color == BallColor.None) throw new ArgumentException("Can't place an empty ball.", nameof(color));
            if (!IsEmpty(cell)) throw new InvalidOperationException($"Cell {cell} is already occupied.");
            cells[cell.Column, cell.Row] = color;
            frozen[cell.Column, cell.Row] = isFrozen && color != BallColor.Rainbow;
        }

        /// <summary>Takes the ball out of a cell and returns its color.</summary>
        public BallColor Remove(Cell cell)
        {
            var color = Get(cell);
            if (color == BallColor.None) throw new InvalidOperationException($"Cell {cell} is empty.");
            cells[cell.Column, cell.Row] = BallColor.None;
            frozen[cell.Column, cell.Row] = false; // a cleared or burst ball takes its ice with it
            return color;
        }

        /// <summary>
        /// Moves a ball to an empty cell. Doesn't check for a path; the Pathfinder does that.
        /// </summary>
        public void Move(Cell from, Cell to)
        {
            if (from == to) throw new ArgumentException("A ball can't move onto itself.");
            if (!IsEmpty(to)) throw new InvalidOperationException($"Cell {to} is already occupied.");
            if (IsFrozen(from)) throw new InvalidOperationException($"The ball at {from} is frozen.");
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

        public void Clear()
        {
            Array.Clear(cells, 0, cells.Length);
            Array.Clear(frozen, 0, frozen.Length);
        }

        /// <summary>Makes this board hold exactly the same balls as <paramref name="other"/> (used by Undo).</summary>
        public void CopyFrom(Board other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            if (other.Size != Size) throw new ArgumentException("Boards have different sizes.", nameof(other));
            Array.Copy(other.cells, cells, cells.Length);
            Array.Copy(other.frozen, frozen, frozen.Length);
        }

        /// <summary>An independent copy, used for Undo snapshots and saving.</summary>
        public Board Clone()
        {
            var copy = new Board(Size);
            Array.Copy(cells, copy.cells, cells.Length);
            Array.Copy(frozen, copy.frozen, frozen.Length);
            return copy;
        }

        void CheckInside(Cell cell)
        {
            if (!IsInside(cell)) throw new ArgumentOutOfRangeException(nameof(cell), $"Cell {cell} is outside the board.");
        }
    }
}
