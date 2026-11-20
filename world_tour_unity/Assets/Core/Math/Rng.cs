using System;
using System.Collections.Generic;

namespace WorldTour.Core.Maths
{
    /// <summary>
    /// Seeded PRNG (mulberry32), ported from world_tour/math/rng.js, so a saved seed regenerates
    /// the same problem and tests repeat. For the same seed it returns the same sequence as the JS,
    /// bit for bit: every method consumes Next() exactly as the JS does.
    /// </summary>
    public sealed class Rng
    {
        private uint _a;

        /// <summary>JS: <c>let a = seed &gt;&gt;&gt; 0</c>. A negative or larger seed wraps the way ToUint32 does.</summary>
        public Rng(long seed)
        {
            _a = unchecked((uint)seed);
        }

        /// <summary>A double in [0, 1). Same bits as the JS <c>next()</c>.</summary>
        public double Next()
        {
            unchecked
            {
                // a = (a + 0x6D2B79F5) | 0  (int and uint share the same 32 bits)
                _a += 0x6D2B79F5u;
                uint a = _a;
                // t = Math.imul(a ^ (a >>> 15), 1 | a)
                uint t = (a ^ (a >> 15)) * (1u | a);
                // t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t   (the add wraps to 32 bits through ToInt32)
                t = (t + (t ^ (t >> 7)) * (61u | t)) ^ t;
                // ((t ^ (t >>> 14)) >>> 0) / 4294967296
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }

        /// <summary>JS: <c>lo + Math.floor(next() * (hi - lo + 1))</c>. Inclusive of both ends when lo &lt;= hi.</summary>
        public int Int(int lo, int hi)
        {
            double span = (double)hi - lo + 1;
            return (int)(lo + Math.Floor(Next() * span));
        }

        /// <summary>
        /// JS: <c>arr[Math.floor(next() * arr.length)]</c>. Consumes one Next() even for an empty list;
        /// the JS then returns undefined, here it throws.
        /// </summary>
        public T Pick<T>(IReadOnlyList<T> arr)
        {
            if (arr == null) throw new ArgumentNullException(nameof(arr));
            int i = (int)Math.Floor(Next() * arr.Count);
            if (arr.Count == 0) throw new InvalidOperationException("pick from an empty list");
            return arr[i];
        }

        /// <summary>A shuffled copy (Fisher-Yates from the end); the input is left alone. Consumes Count - 1 values.</summary>
        public List<T> Shuffle<T>(IReadOnlyList<T> arr)
        {
            if (arr == null) throw new ArgumentNullException(nameof(arr));
            var b = new List<T>(arr);
            for (int i = b.Count - 1; i > 0; i--)
            {
                int j = (int)Math.Floor(Next() * (i + 1));
                T tmp = b[i];
                b[i] = b[j];
                b[j] = tmp;
            }
            return b;
        }

        /// <summary>JS: <c>next() &lt; p</c>.</summary>
        public bool Chance(double p)
        {
            return Next() < p;
        }

        /// <summary>
        /// JS: <c>newSeed = () =&gt; 1 + Math.floor(Math.random() * 2147483646)</c>, with the random source
        /// passed in so Core stays deterministic. <paramref name="unitRandom"/> must return a double in [0, 1).
        /// </summary>
        public static int NewSeed(Func<double> unitRandom)
        {
            if (unitRandom == null) throw new ArgumentNullException(nameof(unitRandom));
            return 1 + (int)Math.Floor(unitRandom() * 2147483646.0);
        }
    }
}
