using System;

namespace Lines.Core
{
    /// <summary>
    /// A position on the board. Column 0 is the left edge, row 0 is the top edge.
    /// </summary>
    public readonly struct Cell : IEquatable<Cell>
    {
        public readonly int Column;
        public readonly int Row;

        public Cell(int column, int row)
        {
            Column = column;
            Row = row;
        }

        public bool Equals(Cell other) => Column == other.Column && Row == other.Row;
        public override bool Equals(object obj) => obj is Cell other && Equals(other);
        public override int GetHashCode() => Column * 397 ^ Row;
        public static bool operator ==(Cell a, Cell b) => a.Equals(b);
        public static bool operator !=(Cell a, Cell b) => !a.Equals(b);
        public override string ToString() => $"({Column},{Row})";
    }
}
