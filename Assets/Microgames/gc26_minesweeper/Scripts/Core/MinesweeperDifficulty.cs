namespace GC26.Minesweeper
{
    // Board size and number of red flags for one difficulty level.
    // Boards are arranged for a tall, portrait screen (Expert is the classic 30x16 turned upright).
    public sealed class MinesweeperDifficulty
    {
        public readonly string Name;
        public readonly int Width;    // columns
        public readonly int Height;   // rows
        public readonly int Mines;    // red flags

        public MinesweeperDifficulty(string name, int width, int height, int mines)
        {
            Name = name; Width = width; Height = height; Mines = mines;
        }

        public static readonly MinesweeperDifficulty Beginner = new MinesweeperDifficulty("Beginner", 9, 9, 10);
        public static readonly MinesweeperDifficulty Intermediate = new MinesweeperDifficulty("Intermediate", 16, 16, 40);
        public static readonly MinesweeperDifficulty Expert = new MinesweeperDifficulty("Expert", 16, 30, 99);

        public static readonly MinesweeperDifficulty[] All = { Beginner, Intermediate, Expert };
    }
}
