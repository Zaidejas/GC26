namespace GC26._2048
{
    /// <summary>Game rules from the GDD: 4×4 field, 8 item levels, 7 merges, 150 moves.</summary>
    public static class CheeseRules
    {
        public const int Size = 4;
        /// <summary>Level 8 is the cheese: it never stays on the field, the mouse takes it to the hole.</summary>
        public const int CheeseLevel = 8;
        public const int MoveLimit = 150;
        /// <summary>Chance that a new item is hay (level 2) instead of grass (level 1).</summary>
        public const double HayChance = 0.1;
        public const int WarnMovesLeft = 10;

        // Index = level of the two merged items.
        static readonly int[] MergePoints = { 0, 32, 64, 128, 256, 512, 1024, 2048 };

        public static readonly string[] Names =
        {
            "", "Зелёная трава", "Стог сена", "Корова", "Бидон молока",
            "Бутылка молока", "Миска сметаны", "Чаша творога", "Сыр"
        };

        public static readonly string[] ShortNames =
        {
            "", "Трава", "Сено", "Корова", "Бидон", "Бутылка", "Сметана", "Творог", "Сыр"
        };

        public static int PointsForMerge(int level) =>
            level > 0 && level < MergePoints.Length ? MergePoints[level] : 0;
    }
}
