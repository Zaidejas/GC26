using System.Collections.Generic;

namespace GC26._2048
{
    public enum TileEventKind
    {
        /// <summary>Item <c>Id</c> slides to (Row, Col).</summary>
        Moved,
        /// <summary>Item <c>OtherId</c> merged into <c>Id</c>, which becomes <c>Level</c>.</summary>
        Merged,
        /// <summary>New item <c>Id</c> of <c>Level</c> appears at (Row, Col).</summary>
        Spawned,
        /// <summary>The mouse ate cheese <c>Id</c> at (Row, Col).</summary>
        Eaten
    }

    public readonly struct TileEvent
    {
        public readonly TileEventKind Kind;
        public readonly int Id;
        public readonly int OtherId;
        public readonly int Row;
        public readonly int Col;
        public readonly int Level;

        TileEvent(TileEventKind kind, int id, int otherId, int row, int col, int level)
        {
            Kind = kind;
            Id = id;
            OtherId = otherId;
            Row = row;
            Col = col;
            Level = level;
        }

        public static TileEvent Moved(int id, int row, int col) => new TileEvent(TileEventKind.Moved, id, 0, row, col, 0);
        public static TileEvent Merged(int id, int otherId, int row, int col, int level) => new TileEvent(TileEventKind.Merged, id, otherId, row, col, level);
        public static TileEvent Spawned(int id, int row, int col, int level) => new TileEvent(TileEventKind.Spawned, id, 0, row, col, level);
        public static TileEvent Eaten(int id, int row, int col) => new TileEvent(TileEventKind.Eaten, id, 0, row, col, CheeseRules.CheeseLevel);
    }

    /// <summary>What one move did; the view animates the events in order.</summary>
    public class MoveResult
    {
        public bool Changed;
        public int Points;
        /// <summary>Cheeses made by this move (they stay on the field).</summary>
        public int Cheeses;
        /// <summary>Highest level created by a merge this move (8 = cheese), 0 if none.</summary>
        public int TopLevel;
        public bool FirstCheese;
        public TileEvent? Spawned;
        /// <summary>Side the items slid to.</summary>
        public Direction Direction;
        /// <summary>
        /// Items that knocked into each other without merging: one slid up against another and stopped, so both
        /// get the "jelly" squash. Items made by a merge are not listed (they glow instead).
        /// </summary>
        public readonly List<int> Bumped = new List<int>();
        public readonly List<TileEvent> Events = new List<TileEvent>();

        /// <summary>The mouse came this move and sits at (MouseRow, MouseCol).</summary>
        public bool MouseArrived;
        /// <summary>The mouse ran from (MouseFromRow, MouseFromCol) to (MouseRow, MouseCol), eating <see cref="Eaten"/>.</summary>
        public bool MouseRan;
        public int MouseFromRow, MouseFromCol, MouseRow, MouseCol;
        /// <summary>Cheeses the mouse ate on its run, in the order it reached them.</summary>
        public readonly List<TileEvent> Eaten = new List<TileEvent>();
    }
}
