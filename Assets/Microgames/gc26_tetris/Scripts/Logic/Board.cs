using System.Collections.Generic;

namespace GC26.Tetris.Logic
{
    /// <summary>The well. Row 0 is the bottom; rows at or above VisibleHeight are hidden.</summary>
    public sealed class Board
    {
        public const int Width = 10;
        public const int VisibleHeight = 20;
        public const int Height = 24;
        public const int Empty = -1;

        readonly int[,] cells = new int[Width, Height];

        public Board()
        {
            Clear();
        }

        public void Clear()
        {
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    cells[x, y] = Empty;
        }

        /// <summary>Piece type index stored in the cell, or Empty.</summary>
        public int Get(int x, int y)
        {
            return cells[x, y];
        }

        public void Set(int x, int y, int value)
        {
            cells[x, y] = value;
        }

        public bool IsFree(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height && cells[x, y] == Empty;
        }

        public bool Fits(PieceType type, int rotation, int x, int y)
        {
            foreach (Cell c in PieceShapes.Get(type, rotation))
            {
                if (!IsFree(x + c.X, y + c.Y))
                    return false;
            }
            return true;
        }

        public void Place(PieceType type, int rotation, int x, int y)
        {
            foreach (Cell c in PieceShapes.Get(type, rotation))
                cells[x + c.X, y + c.Y] = (int)type;
        }

        /// <summary>Removes full rows and returns their indices as they were before the removal.</summary>
        public List<int> ClearFullRows()
        {
            var cleared = new List<int>();
            int writeRow = 0;
            for (int y = 0; y < Height; y++)
            {
                if (IsRowFull(y))
                {
                    cleared.Add(y);
                    continue;
                }
                if (writeRow != y)
                {
                    for (int x = 0; x < Width; x++)
                        cells[x, writeRow] = cells[x, y];
                }
                writeRow++;
            }
            for (int y = writeRow; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                    cells[x, y] = Empty;
            }
            return cleared;
        }

        bool IsRowFull(int y)
        {
            for (int x = 0; x < Width; x++)
            {
                if (cells[x, y] == Empty)
                    return false;
            }
            return true;
        }
    }
}
