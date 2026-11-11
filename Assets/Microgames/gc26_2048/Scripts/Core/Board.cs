using System;
using System.Collections.Generic;

namespace GC26._2048
{
    /// <summary>
    /// The field: item levels 0–7 per cell (0 = empty) and a stable id per item for the animations.
    /// Plain C#, no Unity types, covered by edit-mode tests.
    /// </summary>
    public class Board
    {
        readonly int[] levels;
        readonly int[] ids;
        int nextId = 1;

        public int Size { get; }

        public Board(int size = CheeseRules.Size)
        {
            Size = size;
            levels = new int[size * size];
            ids = new int[size * size];
        }

        public int LevelAt(int row, int col) => levels[row * Size + col];
        public int IdAt(int row, int col) => ids[row * Size + col];

        public void Clear()
        {
            Array.Clear(levels, 0, levels.Length);
            Array.Clear(ids, 0, ids.Length);
        }

        /// <summary>Puts an item of <paramref name="level"/> into a cell and returns its new id (0 for an empty cell).</summary>
        public int Place(int row, int col, int level)
        {
            int i = row * Size + col;
            levels[i] = level;
            ids[i] = level > 0 ? nextId++ : 0;
            return ids[i];
        }

        public int[] ToArray() => (int[])levels.Clone();

        public void Load(int[] cells)
        {
            Clear();
            for (int i = 0; i < levels.Length && i < cells.Length; i++)
                if (cells[i] > 0 && cells[i] < CheeseRules.CheeseLevel)
                    Place(i / Size, i % Size, cells[i]);
        }

        public bool HasMoves()
        {
            for (int r = 0; r < Size; r++)
            for (int c = 0; c < Size; c++)
            {
                int level = LevelAt(r, c);
                if (level == 0) return true;
                if (c < Size - 1 && LevelAt(r, c + 1) == level) return true;
                if (r < Size - 1 && LevelAt(r + 1, c) == level) return true;
            }
            return false;
        }

        /// <summary>
        /// Slides every item to the side of <paramref name="dir"/>. Two equal neighbours merge once per move,
        /// starting from that side; two level-7 items make cheese, which leaves the field at once.
        /// Does not spawn a new item and leaves the board untouched when nothing moved.
        /// </summary>
        public MoveResult Move(Direction dir)
        {
            var result = new MoveResult();
            var newLevels = new int[levels.Length];
            var newIds = new int[ids.Length];
            var line = new (int r, int c)[Size];

            for (int l = 0; l < Size; l++)
            {
                for (int k = 0; k < Size; k++)
                    line[k] = Cell(dir, l, k);

                int p = 0, lastId = 0, lastLevel = 0;
                bool canMerge = false;
                for (int k = 0; k < Size; k++)
                {
                    var (r, c) = line[k];
                    int i = r * Size + c, level = levels[i], id = ids[i];
                    if (level == 0) continue;

                    if (canMerge && lastLevel == level)
                    {
                        var (tr, tc) = line[p - 1];
                        int t = tr * Size + tc;
                        result.Points += CheeseRules.PointsForMerge(level);
                        result.Changed = true;
                        result.Events.Add(TileEvent.Moved(id, tr, tc));
                        if (level + 1 == CheeseRules.CheeseLevel)
                        {
                            newLevels[t] = 0;
                            newIds[t] = 0;
                            p--;
                            result.Cheeses++;
                            result.Events.Add(TileEvent.ToHole(lastId, id, tr, tc));
                        }
                        else
                        {
                            newLevels[t] = level + 1;
                            result.Events.Add(TileEvent.Merged(lastId, id, tr, tc, level + 1));
                        }
                        result.TopLevel = Math.Max(result.TopLevel, level + 1);
                        canMerge = false;
                    }
                    else
                    {
                        var (tr, tc) = line[p];
                        int t = tr * Size + tc;
                        if (t != i)
                        {
                            result.Changed = true;
                            result.Events.Add(TileEvent.Moved(id, tr, tc));
                        }
                        newLevels[t] = level;
                        newIds[t] = id;
                        p++;
                        lastId = id;
                        lastLevel = level;
                        canMerge = true;
                    }
                }
            }

            if (result.Changed)
            {
                Array.Copy(newLevels, levels, levels.Length);
                Array.Copy(newIds, ids, ids.Length);
            }
            return result;
        }

        /// <summary>Puts grass (90%) or hay (10%) into a random empty cell; null when the field is full.</summary>
        public TileEvent? SpawnRandom(Random rng, int levelOverride = 0)
        {
            var empty = new List<int>();
            for (int i = 0; i < levels.Length; i++)
                if (levels[i] == 0) empty.Add(i);
            if (empty.Count == 0) return null;

            int cell = empty[rng.Next(empty.Count)];
            int level = levelOverride > 0 ? levelOverride : rng.NextDouble() < CheeseRules.HayChance ? 2 : 1;
            int row = cell / Size, col = cell % Size;
            int id = Place(row, col, level);
            return TileEvent.Spawned(id, row, col, level);
        }

        (int r, int c) Cell(Direction dir, int line, int k)
        {
            switch (dir)
            {
                case Direction.Left: return (line, k);
                case Direction.Right: return (line, Size - 1 - k);
                case Direction.Up: return (k, line);
                default: return (Size - 1 - k, line);
            }
        }
    }
}
