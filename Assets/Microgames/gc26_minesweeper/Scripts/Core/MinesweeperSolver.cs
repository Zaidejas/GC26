using System.Collections.Generic;
using UnityEngine;

namespace GC26.Minesweeper
{
    // Plays a board using logic only, the way a careful player would, to find out whether it can be
    // solved WITHOUT guessing. It only "knows" what a player sees: opened numbers and its own deductions.
    // Rules, from simple to strong:
    //   1. A number whose red flags are all found -> its other hidden neighbours are safe.
    //      A number with exactly as many hidden neighbours as missing red flags -> they are all red flags.
    //   2. Subsets: if number A's hidden cells are all inside number B's, the cells only B touches
    //      hold (B - A) red flags -> safe if that is 0, all red flags if it equals their count.
    //   3. Total count: no red flags left -> everything hidden is safe, and the other way round.
    //   4. Trying every layout of a group of hidden cells along the edge that fits all the numbers:
    //      a cell that is safe in every layout is safe, one that is a red flag in every layout is one.
    // If none of these finds anything while safe cells are still hidden, the player would have to guess.
    // The solver never guesses, so "solved" means truly solvable by logic.
    public static class MinesweeperSolver
    {
        public const int MaxEnumerationCells = 24;       // bigger groups are skipped (too many layouts)
        const int MaxEnumerationSteps = 200000;          // safety limit per group

        public sealed class Result
        {
            public bool Solved;          // every safe cell opened without guessing
            public bool SolverError;     // a deduction was wrong (must never happen; checked by the tests)
            public int OpenedCells;
            public int SafeCells;
        }

        public static bool IsSolvableWithoutGuessing(MinesweeperBoard board, int firstX, int firstY) =>
            Solve(board, firstX, firstY).Solved;

        public static Result Solve(MinesweeperBoard board, int firstX, int firstY)
        {
            return new Run(board).Play(firstX, firstY);
        }

        // One solving run. Cells are numbered i = y * width + x.
        sealed class Run
        {
            readonly MinesweeperBoard board;
            readonly int w, h;
            readonly bool[] open, flagged;      // what the solver has opened / deduced as a red flag
            readonly int[][] neighbours;
            int openedCount, flaggedCount;
            readonly Result result = new Result();

            public Run(MinesweeperBoard b)
            {
                board = b; w = b.Width; h = b.Height;
                open = new bool[w * h];
                flagged = new bool[w * h];
                neighbours = new int[w * h][];
                var list = new List<int>(8);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        list.Clear();
                        foreach (var n in b.Neighbours(x, y)) list.Add(n.y * w + n.x);
                        neighbours[y * w + x] = list.ToArray();
                    }
                result.SafeCells = w * h - b.MineCount;
            }

            MinesweeperCell CellAt(int i) => board.GetCell(i % w, i / w);

            public Result Play(int firstX, int firstY)
            {
                OpenCell(firstY * w + firstX);
                while (!result.SolverError && openedCount < result.SafeCells)
                {
                    if (StepBasicAndSubsets()) continue;
                    if (StepTotalCount()) continue;
                    if (StepEnumerate()) continue;
                    break;                                   // stuck: a guess would be needed
                }
                result.OpenedCells = openedCount;
                result.Solved = !result.SolverError && openedCount == result.SafeCells;
                return result;
            }

            // Opens a cell the solver believes is safe; a 0 opens its neighbours too (like the game).
            void OpenCell(int start)
            {
                if (open[start] || flagged[start]) return;
                if (CellAt(start).IsMine) { result.SolverError = true; return; }
                var queue = new Queue<int>();
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue();
                    if (open[i] || flagged[i]) continue;
                    open[i] = true;
                    openedCount++;
                    if (CellAt(i).AdjacentMines == 0)
                        foreach (int n in neighbours[i])
                            if (!open[n]) queue.Enqueue(n);
                }
            }

            void Flag(int i)
            {
                if (flagged[i]) return;
                if (!CellAt(i).IsMine) { result.SolverError = true; return; }
                flagged[i] = true;
                flaggedCount++;
            }

            bool Hidden(int i) => !open[i] && !flagged[i];

            // A number's hidden neighbours and how many red flags are still missing among them.
            struct Constraint
            {
                public int[] Cells;   // sorted
                public int Need;
            }

            List<Constraint> BuildConstraints()
            {
                var list = new List<Constraint>();
                var seen = new HashSet<string>();
                var cells = new List<int>(8);
                for (int i = 0; i < w * h; i++)
                {
                    if (!open[i]) continue;
                    int number = CellAt(i).AdjacentMines;
                    if (number == 0) continue;
                    cells.Clear();
                    int found = 0;
                    foreach (int n in neighbours[i])
                    {
                        if (flagged[n]) found++;
                        else if (!open[n]) cells.Add(n);
                    }
                    if (cells.Count == 0) continue;
                    cells.Sort();
                    string key = string.Join(",", cells) + ":" + (number - found);
                    if (!seen.Add(key)) continue;            // the same constraint twice adds nothing
                    list.Add(new Constraint { Cells = cells.ToArray(), Need = number - found });
                }
                return list;
            }

            // Rules 1 and 2.
            bool StepBasicAndSubsets()
            {
                List<Constraint> cs = BuildConstraints();
                var safe = new HashSet<int>();
                var mines = new HashSet<int>();

                foreach (var c in cs)
                {
                    if (c.Need == 0) safe.UnionWith(c.Cells);
                    else if (c.Need == c.Cells.Length) mines.UnionWith(c.Cells);
                }

                if (safe.Count == 0 && mines.Count == 0)
                {
                    for (int a = 0; a < cs.Count; a++)
                        for (int b = 0; b < cs.Count; b++)
                        {
                            if (a == b || cs[a].Cells.Length >= cs[b].Cells.Length) continue;
                            if (!IsSubset(cs[a].Cells, cs[b].Cells)) continue;
                            int rest = cs[b].Need - cs[a].Need;
                            int restCount = cs[b].Cells.Length - cs[a].Cells.Length;
                            if (rest != 0 && rest != restCount) continue;
                            foreach (int cell in cs[b].Cells)
                                if (System.Array.BinarySearch(cs[a].Cells, cell) < 0)
                                    (rest == 0 ? safe : mines).Add(cell);
                        }
                }
                return Apply(safe, mines);
            }

            static bool IsSubset(int[] small, int[] big)
            {
                foreach (int x in small)
                    if (System.Array.BinarySearch(big, x) < 0) return false;
                return true;
            }

            // Rule 3.
            bool StepTotalCount()
            {
                int left = board.MineCount - flaggedCount;
                var hidden = new List<int>();
                for (int i = 0; i < w * h; i++) if (Hidden(i)) hidden.Add(i);
                if (hidden.Count == 0) return false;
                if (left == 0) return Apply(new HashSet<int>(hidden), new HashSet<int>());
                if (left == hidden.Count) return Apply(new HashSet<int>(), new HashSet<int>(hidden));
                return false;
            }

            // Rule 4: try every layout of each group of edge cells that share numbers.
            bool StepEnumerate()
            {
                List<Constraint> cs = BuildConstraints();
                if (cs.Count == 0) return false;
                int minesLeft = board.MineCount - flaggedCount;

                // Group the constraints that share cells (union-find over cells).
                var parent = new Dictionary<int, int>();
                int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
                foreach (var c in cs)
                    foreach (int cell in c.Cells)
                        if (!parent.ContainsKey(cell)) parent[cell] = cell;
                foreach (var c in cs)
                    for (int k = 1; k < c.Cells.Length; k++)
                    {
                        int ra = Find(c.Cells[0]), rb = Find(c.Cells[k]);
                        if (ra != rb) parent[ra] = rb;
                    }

                var groups = new Dictionary<int, List<int>>();
                foreach (int cell in new List<int>(parent.Keys))    // copy: Find() updates the dictionary
                {
                    int r = Find(cell);
                    if (!groups.TryGetValue(r, out var g)) groups[r] = g = new List<int>();
                    g.Add(cell);
                }

                var safe = new HashSet<int>();
                var mines = new HashSet<int>();
                foreach (var kv in groups)
                {
                    List<int> cells = kv.Value;
                    if (cells.Count > MaxEnumerationCells) continue;
                    int root = kv.Key;
                    var groupConstraints = cs.FindAll(c => Find(c.Cells[0]) == root);
                    EnumerateGroup(cells, groupConstraints, minesLeft, safe, mines);
                }
                return Apply(safe, mines);
            }

            void EnumerateGroup(List<int> cells, List<Constraint> constraints, int minesLeft, HashSet<int> safe, HashSet<int> mines)
            {
                int n = cells.Count;
                var local = new Dictionary<int, int>();
                for (int k = 0; k < n; k++) local[cells[k]] = k;

                int m = constraints.Count;
                var need = new int[m];
                var assignedMines = new int[m];
                var unassigned = new int[m];
                var touching = new List<int>[n];
                for (int k = 0; k < n; k++) touching[k] = new List<int>();
                for (int c = 0; c < m; c++)
                {
                    need[c] = constraints[c].Need;
                    unassigned[c] = constraints[c].Cells.Length;
                    foreach (int cell in constraints[c].Cells) touching[local[cell]].Add(c);
                }

                var value = new int[n];
                var mineCount = new long[n];
                long solutions = 0;
                int steps = 0;
                bool aborted = false;

                void Assign(int k, int v)
                {
                    value[k] = v;
                    foreach (int c in touching[k]) { unassigned[c]--; assignedMines[c] += v; }
                }
                void Unassign(int k, int v)
                {
                    foreach (int c in touching[k]) { unassigned[c]++; assignedMines[c] -= v; }
                }
                bool Fits(int k)
                {
                    foreach (int c in touching[k])
                        if (assignedMines[c] > need[c] || assignedMines[c] + unassigned[c] < need[c]) return false;
                    return true;
                }
                void Search(int k, int minesUsed)
                {
                    if (aborted) return;
                    if (++steps > MaxEnumerationSteps) { aborted = true; return; }
                    if (k == n)
                    {
                        solutions++;
                        for (int j = 0; j < n; j++) mineCount[j] += value[j];
                        return;
                    }
                    for (int v = 0; v <= 1; v++)
                    {
                        if (minesUsed + v > minesLeft) break;
                        Assign(k, v);
                        if (Fits(k)) Search(k + 1, minesUsed + v);
                        Unassign(k, v);
                    }
                }

                Search(0, 0);
                if (aborted || solutions == 0) return;
                for (int k = 0; k < n; k++)
                {
                    if (mineCount[k] == 0) safe.Add(cells[k]);
                    else if (mineCount[k] == solutions) mines.Add(cells[k]);
                }
            }

            bool Apply(HashSet<int> safe, HashSet<int> mines)
            {
                bool progress = false;
                foreach (int i in mines) if (Hidden(i)) { Flag(i); progress = true; }
                foreach (int i in safe) if (Hidden(i)) { OpenCell(i); progress = true; }
                return progress;
            }
        }
    }
}
