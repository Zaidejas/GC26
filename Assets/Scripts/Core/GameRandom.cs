using System;

namespace Lines.Core
{
    /// <summary>
    /// Small seeded random number generator (xorshift32).
    /// Gives the same numbers for the same seed on every platform (iOS, Android, editor),
    /// and its whole state is one number, so it can be saved and restored.
    /// </summary>
    public sealed class GameRandom
    {
        public uint State { get; private set; }

        public GameRandom(int seed)
        {
            State = Scramble(unchecked((uint)seed));
        }

        /// <summary>Continues from a saved <see cref="State"/>.</summary>
        public static GameRandom FromState(uint state)
        {
            var random = new GameRandom(0) { State = state == 0 ? FallbackState : state };
            return random;
        }

        /// <summary>A number from 0 to <paramref name="maxExclusive"/> − 1.</summary>
        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return (int)(NextUInt() % (uint)maxExclusive);
        }

        uint NextUInt()
        {
            unchecked
            {
                uint x = State;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                State = x;
                return x;
            }
        }

        const uint FallbackState = 0x6D2B79F5u;

        // Spreads nearby seeds (1, 2, 3 …) far apart; xorshift must never start at 0.
        static uint Scramble(uint s)
        {
            unchecked
            {
                s += 0x9E3779B9u;
                s = (s ^ (s >> 16)) * 0x85EBCA6Bu;
                s = (s ^ (s >> 13)) * 0xC2B2AE35u;
                s ^= s >> 16;
                return s == 0 ? FallbackState : s;
            }
        }
    }
}
