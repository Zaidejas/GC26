namespace Lines.Core
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
    }

    public static class BallColors
    {
        /// <summary>All playable colors, without <see cref="BallColor.None"/>.</summary>
        public static readonly BallColor[] All =
        {
            BallColor.Red, BallColor.Orange, BallColor.Yellow, BallColor.Green,
            BallColor.Cyan, BallColor.Blue, BallColor.Purple,
        };
    }
}
