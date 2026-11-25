using System;
using System.Collections.Generic;

namespace GC26._2048
{
    public enum MouseState
    {
        /// <summary>No cheese made yet this game.</summary>
        None,
        /// <summary>The first cheese is made; the hole opens after <see cref="CheeseRules.MouseDelayMoves"/> moves.</summary>
        Coming,
        /// <summary>The hole is on the field; the mouse runs out of it every <see cref="CheeseRules.MouseRunEvery"/> moves.</summary>
        Here
    }

    /// <summary>
    /// The mouse and its hole. <see cref="CheeseRules.MouseDelayMoves"/> moves after the first cheese of the game a hole
    /// opens on a random outer cell (the field shows it as a darker cell). Every <see cref="CheeseRules.MouseRunEvery"/>th
    /// move the mouse runs out of the hole, from that end of a row or column to the other end (the line is picked at
    /// random), eating every cheese on the way, and is gone again; then the hole moves to another outer cell.
    /// The mouse is seen only while it runs and never takes a cell. One mouse per game; it stays to the end.
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

            // the first cheese starts the countdown from the next move
            if (State == MouseState.None && result.Cheeses > 0)
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

            // the hole moves to another outer cell
            var outer = OuterCells(board.Size);
            outer.Remove((Row, Col));
            (Row, Col) = outer[rng.Next(outer.Count)];
            Countdown = CheeseRules.MouseRunEvery;
            result.HoleRow = Row;
            result.HoleCol = Col;
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
