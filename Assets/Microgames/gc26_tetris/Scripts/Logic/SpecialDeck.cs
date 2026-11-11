using System;
using System.Collections.Generic;

namespace GC26.Tetris.Logic
{
    public enum SpecialKind { None, Bomb, Lightning }

    /// <summary>What a special piece did when it landed.</summary>
    public readonly struct SpecialResult
    {
        public readonly SpecialKind Kind;
        public readonly int X;
        public readonly int Y;
        public readonly int Points;
        public readonly IReadOnlyList<RemovedCell> Removed;

        public SpecialResult(SpecialKind kind, int x, int y, int points, IReadOnlyList<RemovedCell> removed)
        {
            Kind = kind;
            X = x;
            Y = y;
            Points = points;
            Removed = removed;
        }
    }

    public readonly struct RemovedCell
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Value;

        public RemovedCell(int x, int y, int value)
        {
            X = x;
            Y = y;
            Value = value;
        }
    }

    /// <summary>
    /// Decides which pieces are special: one every 8 to 12 pieces, never in the opening,
    /// and every kind once before any kind repeats.
    /// </summary>
    public sealed class SpecialDeck
    {
        public const int MinGap = 8;
        public const int MaxGap = 12;
        public const int FirstAfter = 5;

        static readonly SpecialKind[] Kinds = { SpecialKind.Bomb, SpecialKind.Lightning };

        readonly Random random;
        readonly List<SpecialKind> bag = new List<SpecialKind>();
        int piecesUntilSpecial;

        public SpecialDeck(int seed)
        {
            random = new Random(unchecked(seed * 397 + 11));
            piecesUntilSpecial = FirstAfter + random.Next(MaxGap - MinGap + 1);
        }

        /// <summary>Call once for each piece dealt, in order.</summary>
        public SpecialKind Next()
        {
            if (piecesUntilSpecial > 0)
            {
                piecesUntilSpecial--;
                return SpecialKind.None;
            }

            piecesUntilSpecial = MinGap - 1 + random.Next(MaxGap - MinGap + 1);
            if (bag.Count == 0)
                bag.AddRange(Kinds);
            int index = random.Next(bag.Count);
            SpecialKind kind = bag[index];
            bag.RemoveAt(index);
            return kind;
        }
    }
}
