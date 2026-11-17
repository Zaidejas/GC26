namespace GC26.Lines.Core
{
    /// <summary>The 7 ball colors from the GDD. <see cref="None"/> marks an empty cell.</summary>
    public enum BallColor
    {
        None = 0,
        Red,
        Orange,
        Yellow,
        Green,
        Cyan,
        Blue,
        Purple,
        /// <summary>L14: the rainbow mushroom, a joker that counts as every colour (never one of the 7 plain ones).</summary>
        Rainbow,
    }

    public static class BallColors
    {
        /// <summary>All playable colors, without <see cref="BallColor.None"/>.</summary>
        public static readonly BallColor[] All =
        {
            BallColor.Red, BallColor.Orange, BallColor.Yellow, BallColor.Green,
            BallColor.Cyan, BallColor.Blue, BallColor.Purple,
        };

        /// <summary>One of the 7 plain colours (not empty, not the rainbow).</summary>
        public static bool IsPlain(BallColor color) => color >= BallColor.Red && color <= BallColor.Purple;
    }
}
