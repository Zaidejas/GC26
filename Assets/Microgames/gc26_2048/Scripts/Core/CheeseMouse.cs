using System;
using System.Collections.Generic;

namespace GC26._2048
{
    public enum MouseState
    {
        /// <summary>No hole: no cheese made yet, or the mouse got cheese and waits for the next one.</summary>
        None,
        /// <summary>A cheese is made; the hole opens after <see cref="CheeseRules.MouseDelayMoves"/> moves.</summary>
        Coming,
        /// <summary>The hole is on the field; the mouse runs out of it every <see cref="CheeseRules.MouseRunEvery"/> moves.</summary>
        Here
    }

    /// <summary>
    /// The mouse and its hole. <see cref="CheeseRules.MouseDelayMoves"/> moves after a cheese is made a hole opens on a
    /// random outer cell (the field shows it as a darker, framed cell). Every <see cref="CheeseRules.MouseRunEvery"/>th
    /// move the mouse runs out of the hole, from that end of a row or column to the other end (the line is picked at
    /// random), eating every cheese on the way, and is gone again. If it got cheese, the hole closes and opens again
    /// only after the next cheese is made; if not, the hole moves to another outer cell.
    /// The mouse is seen only while it runs and never takes a cell.
    /// Plain C#, covered by edit-mode tests.
    /// </summary>
    public sealed class CheeseMouse
    {
        public MouseState State { get; private set; }
        /// <summary>Moves left until the hole opens (Coming) or until the next run (Here).</summary>
        public int Countdown { get; private set; }
        /// <summary>The hole: an outer cell (valid when <see cref="State"/> is Here).</summary>
        public int Row { get; private set; }
        public int Col { get; private set; }

        public void Reset()
        {
            State = MouseState.None;
            Countdown = 0;
            Row = Col = 0;
        }

        /// <summary>Puts back a saved mouse; anything that does not make sense becomes "no mouse yet".</summary>
        public void Restore(MouseState state, int countdown, int row, int col, int size = CheeseRules.Size)
        {
            Reset();
            if (state == MouseState.Coming && countdown > 0)
            {
                State = state;
                Countdown = Math.Min(countdown, CheeseRules.MouseDelayMoves);
            }
            else if (state == MouseState.Here && IsOuter(row, col, size))
            {
                State = state;
                Countdown = countdown > 0 ? Math.Min(countdown, CheeseRules.MouseRunEvery) : CheeseRules.MouseRunEvery;
                Row = row;
                Col = col;
            }
        }

        /// <summary>
        /// Called after every counted move (the board has moved and the new item has appeared).
        /// Opens the hole or makes the mouse run, and notes it all in <paramref name="result"/>.
        /// </summary>
        public void AfterMove(Board board, Random rng, MoveResult result)
        {
            if (State == MouseState.Coming)
            {
                if (--Countdown <= 0) OpenHole(board.Size, rng, result);
            }
            else if (State == MouseState.Here)
            {
                if (--Countdown <= 0) Run(board, rng, result);
            }

            // a new cheese (that the mouse did not just eat) starts the countdown from the next move
            if (State == MouseState.None && MadeCheeseLeft(result))
            {
                State = MouseState.Coming;
                Countdown = CheeseRules.MouseDelayMoves;
            }
        }

        void OpenHole(int size, Random rng, MoveResult result)
        {
            var outer = OuterCells(size);
            var (r, c) = outer[rng.Next(outer.Count)];
            State = MouseState.Here;
            Countdown = CheeseRules.MouseRunEvery;
            Row = r;
            Col = c;
            result.HoleOpened = true;
            result.HoleRow = r;
            result.HoleCol = c;
        }

        void Run(Board board, Random rng, MoveResult result)
        {
            int last = board.Size - 1;
            var ends = new List<(int r, int c)>();
            if (Col == 0 || Col == last) ends.Add((Row, last - Col)); // along its row
            if (Row == 0 || Row == last) ends.Add((last - Row, Col)); // along its column
            var (toR, toC) = ends[rng.Next(ends.Count)];

            result.MouseRan = true;
            result.MouseFromRow = Row;
            result.MouseFromCol = Col;
            result.MouseToRow = toR;
            result.MouseToCol = toC;
            int dr = Math.Sign(toR - Row), dc = Math.Sign(toC - Col);
            for (int r = Row, c = Col; ; r += dr, c += dc)
            {
                if (board.LevelAt(r, c) == CheeseRules.CheeseLevel)
                {
                    int id = board.IdAt(r, c);
                    board.Place(r, c, 0);
                    result.Eaten.Add(TileEvent.Eaten(id, r, c));
                }
                if (r == toR && c == toC) break;
            }

            if (result.Eaten.Count > 0)
            {
                // fed: the hole closes until the next cheese
                result.HoleClosed = true;
                State = MouseState.None;
                Countdown = 0;
                return;
            }

            // no cheese on the way: the hole moves to another outer cell
            var outer = OuterCells(board.Size);
            outer.Remove((Row, Col));
            (Row, Col) = outer[rng.Next(outer.Count)];
            Countdown = CheeseRules.MouseRunEvery;
            result.HoleRow = Row;
            result.HoleCol = Col;
        }

        /// <summary>A cheese made this move is still on the field (the mouse may have eaten it on the same move).</summary>
        static bool MadeCheeseLeft(MoveResult result)
        {
            if (result.Cheeses <= 0) return false;
            foreach (var e in result.Events)
            {
                if (e.Kind != TileEventKind.Merged || e.Level != CheeseRules.CheeseLevel) continue;
                bool eaten = false;
                foreach (var g in result.Eaten)
                    if (g.Id == e.Id) eaten = true;
                if (!eaten) return true;
            }
            return false;
        }

        public static bool IsOuter(int row, int col, int size) =>
            row >= 0 && col >= 0 && row < size && col < size &&
            (row == 0 || col == 0 || row == size - 1 || col == size - 1);

        static List<(int r, int c)> OuterCells(int size)
        {
            var cells = new List<(int, int)>();
            for (int r = 0; r < size; r++)
            for (int c = 0; c < size; c++)
                if (IsOuter(r, c, size)) cells.Add((r, c));
            return cells;
        }
    }
}
