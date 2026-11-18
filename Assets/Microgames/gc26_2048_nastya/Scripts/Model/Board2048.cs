using System;
using System.Collections.Generic;

namespace GC26._2048Nastya
{
    /// <summary>
    /// The board and the move rules (GDD 2.3, TDD 4.3). Pure C#: no UnityEngine types.
    /// Coordinates are (x, y): x to the right, y down from the top row.
    /// </summary>
    public sealed class Board2048
    {
        public const int DefaultSize = 4;
        public const int MinSize = 3;
        public const int MaxSize = 6;

        public int Size { get; }

        readonly Tile2048?[,] _cells;
        int _nextId = 1;

        // Scratch buffers for one line, so a move allocates nothing.
        readonly Tile2048[] _lineTiles;
        readonly int[] _lineFrom;

        public Board2048(int size = DefaultSize)
        {
            if (size < MinSize || size > MaxSize)
                throw new ArgumentOutOfRangeException(nameof(size), size, $"Board size must be {MinSize}..{MaxSize}");
            Size = size;
            _cells = new Tile2048?[size, size];
            _lineTiles = new Tile2048[size];
            _lineFrom = new int[size];
        }

        public Tile2048? Get(int x, int y) => _cells[x, y];

        public bool IsEmpty(int x, int y) => !_cells[x, y].HasValue;

        /// <summary>Puts a new tile into an empty cell. Used by the spawner, loading and tests.</summary>
        public Tile2048 Place(int x, int y, int value)
        {
            if (!IsPowerOfTwo(value))
                throw new ArgumentException($"Tile value must be a power of two >= 2, got {value}", nameof(value));
            if (_cells[x, y].HasValue)
                throw new InvalidOperationException($"Cell ({x}, {y}) is not empty");
            var tile = new Tile2048(_nextId++, value);
            _cells[x, y] = tile;
            return tile;
        }

        public int EmptyCount()
        {
            int n = 0;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    if (!_cells[x, y].HasValue) n++;
            return n;
        }

        /// <summary>Empty cells row by row from the top-left.</summary>
        public IEnumerable<(int x, int y)> EmptyCells()
        {
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    if (!_cells[x, y].HasValue) yield return (x, y);
        }

        public int MaxValue()
        {
            int max = 0;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    if (_cells[x, y] is Tile2048 t && t.Value > max) max = t.Value;
            return max;
        }

        /// <summary>True if some move is possible: an empty cell or two equal neighbors.</summary>
        public bool HasMoves()
        {
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    if (!(_cells[x, y] is Tile2048 t)) return true;
                    if (x + 1 < Size && _cells[x + 1, y] is Tile2048 r && r.Value == t.Value) return true;
                    if (y + 1 < Size && _cells[x, y + 1] is Tile2048 d && d.Value == t.Value) return true;
                }
            }
            return false;
        }

        public MoveResult2048 Move(Direction2048 dir)
        {
            var result = new MoveResult2048();
            Move(dir, result);
            return result;
        }

        /// <summary>Slides every tile toward <paramref name="dir"/>. Fills <paramref name="result"/> (cleared first).</summary>
        public void Move(Direction2048 dir, MoveResult2048 result)
        {
            result.Clear();
            for (int line = 0; line < Size; line++)
                MoveLine(dir, line, result);
            result.Moved = result.Moves.Count > 0 || result.Merges.Count > 0;
        }

        // k = 0 is the cell at the wall the tiles move toward.
        void MoveLine(Direction2048 dir, int line, MoveResult2048 result)
        {
            int count = 0;
            for (int k = 0; k < Size; k++)
            {
                Cell(dir, line, k, out int x, out int y);
                if (_cells[x, y] is Tile2048 t)
                {
                    _lineTiles[count] = t;
                    _lineFrom[count] = k;
                    count++;
                    _cells[x, y] = null;
                }
            }

            int outK = 0;
            int i = 0;
            while (i < count)
            {
                Cell(dir, line, outK, out int toX, out int toY);
                var a = _lineTiles[i];
                if (i + 1 < count && _lineTiles[i + 1].Value == a.Value)
                {
                    var b = _lineTiles[i + 1];
                    var merged = new Tile2048(_nextId++, a.Value * 2);
                    _cells[toX, toY] = merged;
                    RecordMove(dir, line, a.Id, _lineFrom[i], outK, toX, toY, result);
                    RecordMove(dir, line, b.Id, _lineFrom[i + 1], outK, toX, toY, result);
                    result.Merges.Add(new TileMerge2048(a.Id, b.Id, merged, toX, toY));
                    result.ScoreGained += merged.Value;
                    if (merged.Value > result.MaxValue) result.MaxValue = merged.Value;
                    i += 2; // the merged tile is never compared again: one merge per tile per move
                }
                else
                {
                    _cells[toX, toY] = a;
                    RecordMove(dir, line, a.Id, _lineFrom[i], outK, toX, toY, result);
                    if (a.Value > result.MaxValue) result.MaxValue = a.Value;
                    i += 1;
                }
                outK++;
            }
        }

        void RecordMove(Direction2048 dir, int line, int id, int fromK, int toK, int toX, int toY, MoveResult2048 result)
        {
            if (fromK == toK) return;
            Cell(dir, line, fromK, out int fromX, out int fromY);
            result.Moves.Add(new TileMove2048(id, fromX, fromY, toX, toY));
        }

        void Cell(Direction2048 dir, int line, int k, out int x, out int y)
        {
            switch (dir)
            {
                case Direction2048.Left: x = k; y = line; break;
                case Direction2048.Right: x = Size - 1 - k; y = line; break;
                case Direction2048.Up: x = line; y = k; break;
                default: x = line; y = Size - 1 - k; break; // Down
            }
        }

        /// <summary>A full copy with the same tile Ids (for undo).</summary>
        public Board2048 Clone()
        {
            var copy = new Board2048(Size);
            Array.Copy(_cells, copy._cells, _cells.Length);
            copy._nextId = _nextId;
            return copy;
        }

        /// <summary>Values row by row from the top-left, 0 = empty (the save format, TDD 8.2).</summary>
        public int[] ToValues()
        {
            var values = new int[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    values[y * Size + x] = _cells[x, y]?.Value ?? 0;
            return values;
        }

        /// <summary>Builds a board from <see cref="ToValues"/> output. Tile Ids are created anew.</summary>
        public static Board2048 FromValues(int size, IReadOnlyList<int> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (values.Count != size * size)
                throw new ArgumentException($"Expected {size * size} values, got {values.Count}", nameof(values));
            var board = new Board2048(size);
            for (int i = 0; i < values.Count; i++)
                if (values[i] != 0)
                    board.Place(i % size, i / size, values[i]);
            return board;
        }

        public static bool IsPowerOfTwo(int value) => value >= 2 && (value & (value - 1)) == 0;
    }
}
