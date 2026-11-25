namespace GC26._2048
{
    /// <summary>Game rules from the GDD: 4×4 field, 8 item levels, 7 merges; 200 moves (the first GDD and the HTML prototype had 150).</summary>
    public static class CheeseRules
    {
        public const int Size = 4;
        /// <summary>Level 8 is the cheese, the last item: it stays on the field and never merges any further.</summary>
        public const int CheeseLevel = 8;
        public const int MoveLimit = 200;
        /// <summary>Chance that a new item is hay (level 2) instead of grass (level 1).</summary>
        public const double HayChance = 0.1;
        public const int WarnMovesLeft = 10;
        /// <summary>Moves after the first cheese of the game until the mouse comes.</summary>
        public const int MouseDelayMoves = 5;
        /// <summary>The mouse runs across its row or column every this many moves.</summary>
        public const int MouseRunEvery = 3;

        // Index = level of the two merged items.
        static readonly int[] MergePoints = { 0, 32, 64, 128, 256, 512, 1024, 2048 };

        public static int PointsForMerge(int level) =>
            level > 0 && level < MergePoints.Length ? MergePoints[level] : 0;
    }
}
