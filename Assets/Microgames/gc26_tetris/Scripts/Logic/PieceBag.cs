using System;
using System.Collections.Generic;

namespace GC26.Tetris.Logic
{
    /// <summary>Deals every piece once per shuffled bag, so droughts stay short.</summary>
    public sealed class PieceBag
    {
        readonly Random random;
        readonly Queue<PieceType> queue = new Queue<PieceType>();

        public PieceBag(int seed)
        {
            random = new Random(seed);
        }

        public PieceType Take()
        {
            if (queue.Count == 0)
                Refill();
            return queue.Dequeue();
        }

        void Refill()
        {
            var pieces = new PieceType[PieceShapes.PieceCount];
            for (int i = 0; i < pieces.Length; i++)
                pieces[i] = (PieceType)i;

            for (int i = pieces.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                PieceType swap = pieces[i];
                pieces[i] = pieces[j];
                pieces[j] = swap;
            }

            foreach (PieceType piece in pieces)
                queue.Enqueue(piece);
        }
    }
}
