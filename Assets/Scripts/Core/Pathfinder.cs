using System;
using System.Collections.Generic;

namespace Lines.Core
{
    /// <summary>
    /// Finds the shortest path for a ball with breadth-first search (BFS).
    /// A ball moves up, down, left or right through empty cells only:
    /// no diagonals and no jumping over balls.
    /// </summary>
    public static class Pathfinder
    {
        // Fixed order keeps results repeatable: up, down, left, right.
        static readonly int[] StepColumn = { 0, 0, -1, 1 };
        static readonly int[] StepRow = { -1, 1, 0, 0 };

        /// <summary>
        /// Returns the cells the ball passes through, from <paramref name="from"/> to
        /// <paramref name="to"/> (both included), or null if the ball can't get there.
        /// </summary>
        public static List<Cell> FindPath(Board board, Cell from, Cell to)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (board.IsEmpty(from)) throw new ArgumentException($"There is no ball at {from}.", nameof(from));
            if (!board.IsInside(to)) throw new ArgumentOutOfRangeException(nameof(to), $"Cell {to} is outside the board.");
            if (from == to || !board.IsEmpty(to)) return null;

            var cameFrom = new Dictionary<Cell, Cell>();
            var queue = new Queue<Cell>();
            queue.Enqueue(from);
            cameFrom[from] = from;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == to) return BuildPath(cameFrom, from, to);

                for (int i = 0; i < StepColumn.Length; i++)
                {
                    var next = new Cell(current.Column + StepColumn[i], current.Row + StepRow[i]);
                    if (!board.IsInside(next) || cameFrom.ContainsKey(next) || !board.IsEmpty(next))
                        continue;
                    cameFrom[next] = current;
                    queue.Enqueue(next);
                }
            }

            return null;
        }

        public static bool HasPath(Board board, Cell from, Cell to) => FindPath(board, from, to) != null;

        static List<Cell> BuildPath(Dictionary<Cell, Cell> cameFrom, Cell from, Cell to)
        {
            var path = new List<Cell>();
            for (var cell = to; cell != from; cell = cameFrom[cell])
                path.Add(cell);
            path.Add(from);
            path.Reverse();
            return path;
        }
    }
}
