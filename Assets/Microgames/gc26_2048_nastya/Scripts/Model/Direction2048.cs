namespace GC26._2048Nastya
{
    /// <summary>Direction of a move: every tile slides this way.</summary>
    public enum Direction2048
    {
        Up,
        Down,
        Left,
        Right
    }

    /// <summary>One tile on the board. Id is stable across moves, so the view can animate it.</summary>
    public readonly struct Tile2048
    {
        /// <summary>Unique within a board, greater than 0.</summary>
        public readonly int Id;

        /// <summary>2, 4, 8, ...</summary>
        public readonly int Value;

        public Tile2048(int id, int value)
        {
            Id = id;
            Value = value;
        }

        public override string ToString() => $"#{Id}:{Value}";
    }
}
