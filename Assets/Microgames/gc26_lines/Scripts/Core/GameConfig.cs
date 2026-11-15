namespace GC26.Lines.Core
{
    /// <summary>Fixed game rules from the GDD.</summary>
    public static class GameConfig
    {
        public const int BoardSize = 9;
        public const int ColorCount = 7;
        public const int StartBalls = 3;
        public const int BallsPerTurn = 3;
        public const int MinLineLength = 5;

        // Timed mode (GDD → Timed mode, task L09), in seconds.
        public const float TimedStart = 180f;     // 3:00 on the clock at the start
        public const float TimeBonus = 5f;        // added for every cleared line
        public const float TimeWarn = 30f;        // the clock turns yellow and ticks
        public const float TimeLow = 10f;         // ...red, with a sharper tick
    }
}
