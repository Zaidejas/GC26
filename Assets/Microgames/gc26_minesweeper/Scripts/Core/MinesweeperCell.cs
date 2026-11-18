namespace GC26.Minesweeper
{
    // Holds the data for ONE square on the board. Plain C#, no graphics.
    // "Mine" = a hidden red flag, "flagged" = the player's yellow flag (see the GDD, section 1.1).
    public class MinesweeperCell
    {
        public bool IsMine;         // true if this square hides a red flag
        public int AdjacentMines;   // how many of the 8 surrounding squares hide red flags (0–8)
        public bool IsRevealed;     // true once the player has opened this square
        public bool IsFlagged;      // true if the player has marked it with a yellow flag
    }
}
