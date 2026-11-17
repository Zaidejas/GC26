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
        // Special mushrooms (GDD → Special mushrooms, tasks L14–L16). Balanced with a robot player (250 games each).
        public const int RainbowOdds = 40;        // L14: 1 new mushroom in 40 is a rainbow (never in a new game's first NEXT)
        public const int IceFrom = 100;           // L15: frozen mushrooms from 100 points...
        public const int IceOdds = 20;            // ...1 new mushroom in 20...
        public const int IceMax = 4;              // ...and never more than 4 frozen on the board at once
        public const int PuffballEvery = 200;     // L16: one puffball every 200 points...
        public const int PuffballMax = 2;         // ...keep up to 2 (a full pocket pauses the count)
    }
}
