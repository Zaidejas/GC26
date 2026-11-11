using System.Collections.Generic;

namespace GC26.Bomber
{
    /// <summary>Seeded xorshift32, identical to the HTML prototype (same seed = same rooms and waves).</summary>
    public sealed class BomberRng
    {
        uint _s;

        public BomberRng(uint seed) { _s = seed == 0 ? 0x9e3779b9u : seed; }

        /// <summary>Uniform value in [0, 1).</summary>
        public float Next()
        {
            _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5;
            return (float)(_s / 4294967296.0);
        }

        public int Range(int n) { var v = (int)(Next() * n); return v >= n ? n - 1 : v; }
        public T Pick<T>(IList<T> list) => list[Range(list.Count)];

        public void Shuffle<T>(IList<T> a)
        {
            for (int i = a.Count - 1; i > 0; i--) { int j = (int)(Next() * (i + 1)); if (j > i) j = i; (a[i], a[j]) = (a[j], a[i]); }
        }

        public static uint Hash(uint a, uint b)
        {
            unchecked
            {
                uint h = (a * 2654435761u) ^ (b * 40503u);
                h = (h ^ (h >> 16)) * 2246822507u;
                return h ^ (h >> 13);
            }
        }
    }
}
