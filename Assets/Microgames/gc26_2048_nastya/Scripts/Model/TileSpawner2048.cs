using System;

namespace GC26._2048Nastya
{
    /// <summary>A tile the spawner placed, with its cell (for the spawn animation).</summary>
    public readonly struct TileSpawn2048
    {
        public readonly Tile2048 Tile;
        public readonly int X, Y;

        public TileSpawn2048(Tile2048 tile, int x, int y)
        {
            Tile = tile;
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// Places new tiles (GDD 2.2): a uniformly random empty cell, value 4 with <see cref="FourChance"/>, otherwise 2.
    /// Uses System.Random, so it shares no global random state and can be seeded in tests.
    /// </summary>
    public sealed class TileSpawner2048
    {
        public const float DefaultFourChance = 0.1f;

        readonly Random _rng;

        public float FourChance { get; }

        public TileSpawner2048(Random rng, float fourChance = DefaultFourChance)
        {
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            if (fourChance < 0f || fourChance > 1f)
                throw new ArgumentOutOfRangeException(nameof(fourChance), fourChance, "Must be 0..1");
            FourChance = fourChance;
        }

        /// <summary>Places one tile. Returns null if the board is full.</summary>
        public TileSpawn2048? Spawn(Board2048 board)
        {
            int empty = board.EmptyCount();
            if (empty == 0) return null;

            int pick = _rng.Next(empty);
            int value = _rng.NextDouble() < FourChance ? 4 : 2;

            for (int y = 0; y < board.Size; y++)
            {
                for (int x = 0; x < board.Size; x++)
                {
                    if (!board.IsEmpty(x, y)) continue;
                    if (pick-- == 0)
                        return new TileSpawn2048(board.Place(x, y, value), x, y);
                }
            }
            return null; // unreachable: empty > 0
        }
    }
}
