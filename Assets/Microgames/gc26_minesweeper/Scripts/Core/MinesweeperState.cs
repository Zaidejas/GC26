namespace GC26.Minesweeper
{
    // The phases of one round.
    // Ready   = board shown, no tap yet (timer not running, red flags not placed)
    // Playing = the first tap has happened, timer is running
    // Won     = every safe square is opened
    // Lost    = a red flag was opened
    public enum MinesweeperState { Ready, Playing, Won, Lost }
}
