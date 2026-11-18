using System.Collections.Generic;   // List<T>, Queue<T>
using UnityEngine;                  // Vector2Int, Mathf

namespace GC26.Minesweeper
{
    // All the rules of the game. Knows nothing about sprites, touches or the screen,
    // so it is easy to test. Same as Board.cs in the technical design document.
    public class MinesweeperBoard
    {
        public int Width { get; }
        public int Height { get; }
        public int MineCount { get; }

        public int FlagCount { get; private set; }      // yellow flags currently placed
        public int RevealedCount { get; private set; }  // safe squares opened so far
        public bool MinesPlaced { get; private set; }   // false until the first tap

        private readonly MinesweeperCell[,] cells;
        private readonly System.Random rng;

        // "seed" is optional: the same seed always gives the same layout (used by the tests).
        public MinesweeperBoard(int width, int height, int mines, int? seed = null)
        {
            Width = width; Height = height;
            // At least 1 red flag, and keep 9 squares free for the safe 3x3 area around the first tap.
            MineCount = Mathf.Clamp(mines, 1, width * height - 9);
            rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();

            cells = new MinesweeperCell[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    cells[x, y] = new MinesweeperCell();
        }

        public MinesweeperCell GetCell(int x, int y) => cells[x, y];

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        // The up to 8 neighbours of (x, y) (fewer at edges and corners).
        public IEnumerable<Vector2Int> Neighbours(int x, int y)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (InBounds(nx, ny)) yield return new Vector2Int(nx, ny);
                }
        }

        // Called on the FIRST tap, so the first tap and its neighbours are never red flags.
        public void PlaceMines(int safeX, int safeY)
        {
            int placed = 0;
            while (placed < MineCount)
            {
                int x = rng.Next(Width), y = rng.Next(Height);
                bool inSafeArea = Mathf.Abs(x - safeX) <= 1 && Mathf.Abs(y - safeY) <= 1;
                if (cells[x, y].IsMine || inSafeArea) continue;
                cells[x, y].IsMine = true;
                placed++;
            }
            CalculateNumbers();
            MinesPlaced = true;
        }

        private void CalculateNumbers()
        {
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {
                    int count = 0;
                    foreach (var n in Neighbours(x, y))
                        if (cells[n.x, n.y].IsMine) count++;
                    cells[x, y].AdjacentMines = count;
                }
        }

        // Opens (x, y). A 0 also opens its neighbours, and so on (flood fill with a queue,
        // so big empty areas can't overflow the stack). Returns every square that was opened.
        public List<Vector2Int> Reveal(int x, int y)
        {
            var changed = new List<Vector2Int>();
            MinesweeperCell start = cells[x, y];
            if (start.IsRevealed || start.IsFlagged) return changed;

            var queue = new Queue<Vector2Int>();
            queue.Enqueue(new Vector2Int(x, y));

            while (queue.Count > 0)
            {
                Vector2Int p = queue.Dequeue();
                MinesweeperCell c = cells[p.x, p.y];
                if (c.IsRevealed || c.IsFlagged) continue;

                c.IsRevealed = true;
                changed.Add(p);
                if (c.IsMine) break;            // a red flag: the round is lost

                RevealedCount++;
                if (c.AdjacentMines == 0)
                    foreach (var n in Neighbours(p.x, p.y))
                        if (!cells[n.x, n.y].IsRevealed) queue.Enqueue(n);
            }
            return changed;
        }

        // Places or removes a yellow flag. Returns false if the square is already open.
        public bool ToggleFlag(int x, int y)
        {
            MinesweeperCell c = cells[x, y];
            if (c.IsRevealed) return false;
            c.IsFlagged = !c.IsFlagged;
            FlagCount += c.IsFlagged ? 1 : -1;
            return true;
        }

        // Tapping an open number whose yellow flags match it opens all its other neighbours.
        public List<Vector2Int> Chord(int x, int y)
        {
            var changed = new List<Vector2Int>();
            MinesweeperCell c = cells[x, y];
            if (!c.IsRevealed || c.AdjacentMines == 0) return changed;

            int flags = 0;
            foreach (var n in Neighbours(x, y))
                if (cells[n.x, n.y].IsFlagged) flags++;
            if (flags != c.AdjacentMines) return changed;

            foreach (var n in Neighbours(x, y))
                changed.AddRange(Reveal(n.x, n.y));
            return changed;
        }

        // Won when every square without a red flag is open.
        public bool IsWon => RevealedCount == Width * Height - MineCount;
    }
}
