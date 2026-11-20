using System.Collections.Generic;
using System.Globalization;

namespace WorldTour.Game.Ticket
{
    /// <summary>One bar of the ticket barcode: an ink rect at <see cref="X"/>, <see cref="Width"/> wide, in the 100x30 viewBox.</summary>
    public struct BarcodeBar
    {
        public int X;
        public int Width;

        public BarcodeBar(int x, int width) { X = x; Width = width; }

        public override string ToString() { return X.ToString(CultureInfo.InvariantCulture) + ":" + Width.ToString(CultureInfo.InvariantCulture); }
    }

    /// <summary>
    /// The per-show barcode, ported exactly from world_tour/screens/ticket.js:10-15 <c>barcode(seed)</c>. Plain C#, no
    /// UnityEngine, so edit-mode tests check it against the real JS (Tools/ticket_vectors.mjs).
    /// <para>
    /// The JS multiplies in doubles: <c>x * 1103515245</c> reaches about 2^62, past the 53-bit mantissa, so the low bits
    /// are rounded before <c>&gt;&gt;&gt; 0</c> takes the result mod 2^32. This port repeats that double arithmetic on
    /// purpose (an exact uint multiply would give different bars). One visible effect, kept as in the web game: bit 8
    /// is often 0 after the rounding, so many tickets get only a few bars, and some none at all.
    /// </para>
    /// </summary>
    public static class TicketBarcode
    {
        /// <summary>ticket.js:13 <c>while (pos &lt; 100)</c>: the viewBox width.</summary>
        public const int ViewWidth = 100;
        /// <summary>ticket.js:13 <c>height="30"</c>: the viewBox height.</summary>
        public const int ViewHeight = 30;

        /// <summary>ticket.js:45 <c>barcode(date + mode.id + G.fans)</c>: the seed string for one show.</summary>
        public static string Seed(string localIsoDate, string modeId, int fans)
        {
            return (localIsoDate ?? "") + (modeId ?? "") + fans.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The bars <c>barcode(seed)</c> draws, in order. Bars are not clipped: the last one may run past x = 100, as the SVG rect does (the SVG clips it).</summary>
        public static List<BarcodeBar> Bars(string seed)
        {
            var bars = new List<BarcodeBar>();
            uint x = Hash(seed ?? "");
            int pos = 0;
            while (pos < ViewWidth)
            {
                x = Lcg(x);
                int w = 1 + (int)((x >> 16) % 4);
                if (((x >> 8) & 1) != 0) bars.Add(new BarcodeBar(pos, w));
                pos += w + 1;
            }
            return bars;
        }

        /// <summary>
        /// ticket.js:11 <c>for (const c of seed) x = (x * 31 + c.charCodeAt(0)) &gt;&gt;&gt; 0</c>. The loop walks code points,
        /// and charCodeAt(0) of an astral code point is its high surrogate, so a surrogate pair counts once, by its high half.
        /// x * 31 + c stays below 2^53, so uint wrap-around gives the same value as the JS double.
        /// </summary>
        public static uint Hash(string seed)
        {
            uint x = 0;
            for (int i = 0; i < seed.Length; i++)
            {
                char c = seed[i];
                unchecked { x = x * 31u + c; }
                if (char.IsHighSurrogate(c) && i + 1 < seed.Length && char.IsLowSurrogate(seed[i + 1])) i++;
            }
            return x;
        }

        /// <summary>ticket.js:13 <c>x = (x * 1103515245 + 12345) &gt;&gt;&gt; 0</c>, in IEEE doubles as the JS evaluates it.</summary>
        public static uint Lcg(uint x)
        {
            double p = (double)x * 1103515245.0;
            p = p + 12345.0;
            // p is a whole number below 2^63, so the conversion is exact; ToUint32 keeps the low 32 bits.
            return (uint)((ulong)p & 0xFFFFFFFFUL);
        }

        /// <summary>The bars as "x:w,x:w" (the vector file's format).</summary>
        public static string Format(IReadOnlyList<BarcodeBar> bars)
        {
            var parts = new string[bars.Count];
            for (int i = 0; i < bars.Count; i++) parts[i] = bars[i].ToString();
            return string.Join(",", parts);
        }
    }
}
