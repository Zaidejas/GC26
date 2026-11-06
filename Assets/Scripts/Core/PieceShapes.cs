namespace BlockDrop.Core
{
    public enum PieceType { I, O, T, S, Z, J, L }

    public readonly struct Cell
    {
        public readonly int X;
        public readonly int Y;

        public Cell(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>Piece geometry. Coordinates are local to a square box, with Y pointing up.</summary>
    public static class PieceShapes
    {
        public const int PieceCount = 7;
        public const int RotationCount = 4;

        static readonly Cell[][][] Rotations = Build();

        public static Cell[] Get(PieceType type, int rotation)
        {
            return Rotations[(int)type][Mod(rotation, RotationCount)];
        }

        public static int BoxSize(PieceType type)
        {
            switch (type)
            {
                case PieceType.I: return 4;
                case PieceType.O: return 2;
                default: return 3;
            }
        }

        public static int Mod(int value, int modulus)
        {
            return ((value % modulus) + modulus) % modulus;
        }

        static Cell[][][] Build()
        {
            var result = new Cell[PieceCount][][];
            for (int type = 0; type < PieceCount; type++)
            {
                var pieceType = (PieceType)type;
                int size = BoxSize(pieceType);
                var rotations = new Cell[RotationCount][];
                rotations[0] = BaseCells(pieceType);
                for (int r = 1; r < RotationCount; r++)
                {
                    rotations[r] = new Cell[rotations[r - 1].Length];
                    for (int i = 0; i < rotations[r].Length; i++)
                    {
                        Cell c = rotations[r - 1][i];
                        // Clockwise rotation inside the box.
                        rotations[r][i] = new Cell(c.Y, size - 1 - c.X);
                    }
                }
                result[type] = rotations;
            }
            return result;
        }

        static Cell[] BaseCells(PieceType type)
        {
            switch (type)
            {
                case PieceType.I: return new[] { new Cell(0, 2), new Cell(1, 2), new Cell(2, 2), new Cell(3, 2) };
                case PieceType.O: return new[] { new Cell(0, 0), new Cell(1, 0), new Cell(0, 1), new Cell(1, 1) };
                case PieceType.T: return new[] { new Cell(0, 1), new Cell(1, 1), new Cell(2, 1), new Cell(1, 2) };
                case PieceType.S: return new[] { new Cell(0, 1), new Cell(1, 1), new Cell(1, 2), new Cell(2, 2) };
                case PieceType.Z: return new[] { new Cell(0, 2), new Cell(1, 2), new Cell(1, 1), new Cell(2, 1) };
                case PieceType.J: return new[] { new Cell(0, 2), new Cell(0, 1), new Cell(1, 1), new Cell(2, 1) };
                default: return new[] { new Cell(2, 2), new Cell(0, 1), new Cell(1, 1), new Cell(2, 1) };
            }
        }
    }
}
