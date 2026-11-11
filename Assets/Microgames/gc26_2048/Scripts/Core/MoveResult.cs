using System.Collections.Generic;

namespace GC26._2048
{
    public enum TileEventKind
    {
        /// <summary>Item <c>Id</c> slides to (Row, Col).</summary>
        Moved,
        /// <summary>Item <c>OtherId</c> merged into <c>Id</c>, which becomes <c>Level</c>.</summary>
        Merged,
        /// <summary>Items <c>Id</c> and <c>OtherId</c> became cheese at (Row, Col); the mouse takes it to the hole.</summary>
        ToHole,
        /// <summary>New item <c>Id</c> of <c>Level</c> appears at (Row, Col).</summary>
        Spawned
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
        public static TileEvent ToHole(int id, int otherId, int row, int col) => new TileEvent(TileEventKind.ToHole, id, otherId, row, col, CheeseRules.CheeseLevel);
        public static TileEvent Spawned(int id, int row, int col, int level) => new TileEvent(TileEventKind.Spawned, id, 0, row, col, level);
    }

    /// <summary>What one move did; the view animates the events in order.</summary>
    public class MoveResult
    {
        public bool Changed;
        public int Points;
        public int Cheeses;
        /// <summary>Highest level created by a merge this move (8 = cheese), 0 if none.</summary>
        public int TopLevel;
        public bool FirstCheese;
        public TileEvent? Spawned;
        public readonly List<TileEvent> Events = new List<TileEvent>();
    }
}
