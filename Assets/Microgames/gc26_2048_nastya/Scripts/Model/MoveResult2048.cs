using System.Collections.Generic;

namespace GC26._2048Nastya
{
    /// <summary>A tile that changed position during a move (merge sources included).</summary>
    public readonly struct TileMove2048
    {
        public readonly int TileId;
        public readonly int FromX, FromY, ToX, ToY;

        public TileMove2048(int tileId, int fromX, int fromY, int toX, int toY)
        {
            TileId = tileId;
            FromX = fromX;
            FromY = fromY;
            ToX = toX;
            ToY = toY;
        }
    }

    /// <summary>Two tiles that merged into <see cref="NewTile"/> at cell (X, Y).</summary>
    public readonly struct TileMerge2048
    {
        public readonly int SourceIdA, SourceIdB;
        public readonly Tile2048 NewTile;
        public readonly int X, Y;

        public TileMerge2048(int sourceIdA, int sourceIdB, Tile2048 newTile, int x, int y)
        {
            SourceIdA = sourceIdA;
            SourceIdB = sourceIdB;
            NewTile = newTile;
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// What a move did, for the score and the animation. Reuse one instance and pass it to
    /// <see cref="Board2048.Move(Direction2048, MoveResult2048)"/>: the lists are cleared, not re-created.
    /// </summary>
    public sealed class MoveResult2048
    {
        /// <summary>False means the move was invalid: nothing moved and nothing merged.</summary>
        public bool Moved;

        /// <summary>Sum of the values of the tiles created by merges.</summary>
        public int ScoreGained;

        /// <summary>Largest tile value on the board after the move.</summary>
        public int MaxValue;

        public readonly List<TileMove2048> Moves = new List<TileMove2048>(16);
        public readonly List<TileMerge2048> Merges = new List<TileMerge2048>(8);

        public void Clear()
        {
            Moved = false;
            ScoreGained = 0;
            MaxValue = 0;
            Moves.Clear();
            Merges.Clear();
        }
    }
}
