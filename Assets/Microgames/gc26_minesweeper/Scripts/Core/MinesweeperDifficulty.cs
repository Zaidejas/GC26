namespace GC26.Minesweeper
{
    // Board size and number of red flags for one difficulty level.
    // Width × Height is the level's own shape, for a tall, portrait screen (Expert is the classic
    // 30x16 turned upright); ShapeFor can rearrange the same cells to fit a wider screen.
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

        // The same number of cells, arranged to fit the free space best (GDD 3.5).
        // Tries every columns × rows with exactly Width × Height cells whose long side / short side
        // is no more than the level's own (Expert: 16×30, 20×24, 24×20, 30×16; Beginner and
        // Intermediate stay square). Picks the one with the biggest square cells; on a tie the
        // level's own shape wins. Same cells, same red flags; only the outline changes.
        public (int cols, int rows) ShapeFor(float areaWidth, float areaHeight, float maxCell = 130f)
        {
            int cells = Width * Height;
            float maxRatio = (float)System.Math.Max(Width, Height) / System.Math.Min(Width, Height);
            (int cols, int rows) best = (Width, Height);
            float bestSize = CellSize(Width, Height, areaWidth, areaHeight, maxCell);
            for (int cols = 1; cols <= cells; cols++)
            {
                if (cells % cols != 0) continue;
                int rows = cells / cols;
                if ((float)System.Math.Max(cols, rows) / System.Math.Min(cols, rows) > maxRatio + 0.001f) continue;
                float size = CellSize(cols, rows, areaWidth, areaHeight, maxCell);
                if (size > bestSize) { best = (cols, rows); bestSize = size; }
            }
            return best;
        }

        // Whole canvas units, like the game's layout, so tiny differences don't count as "bigger".
        static float CellSize(int cols, int rows, float w, float h, float maxCell) =>
            (float)System.Math.Floor(System.Math.Min(System.Math.Min(w / cols, h / rows), maxCell));
    }
}
