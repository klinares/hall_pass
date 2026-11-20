using System;
using System.Numerics;

namespace WorldTour.Core.Meta
{
    /// <summary>The two JS number operations meta.js leans on, with the JS results bit for bit.</summary>
    public static class JsNum
    {
        /// <summary>
        /// JS <c>Math.round(x)</c>: nearest integer, a tie goes up (2.5 to 3, -2.5 to -2). Computed from the floor
        /// so a value just under a half (0.49999999999999994) isn't pushed over by the addition <c>x + 0.5</c>.
        /// NaN and infinities come back unchanged.
        /// </summary>
        public static double Round(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return x;
            double f = Math.Floor(x);
            return x - f >= 0.5 ? f + 1 : f;
        }

        /// <summary>
        /// JS <c>+x.toFixed(digits)</c>: the exact binary value of x rounded to <paramref name="digits"/> decimals
        /// (a tie goes away from zero, as toFixed works on |x|), read back as a double. q / 10^digits is one
        /// correctly rounded IEEE division, which is exactly what reading the decimal string back gives,
        /// as long as |x| * 10^digits stays under 2^53 (meta.js only rounds page fractions and tilts).
        /// </summary>
        public static double ToFixedNumber(double x, int digits)
        {
            if (double.IsNaN(x) || double.IsInfinity(x) || Math.Abs(x) >= 1e21) return x;
            bool neg = x < 0;
            long bits = BitConverter.DoubleToInt64Bits(Math.Abs(x));
            int exp = (int)((bits >> 52) & 0x7FF);
            long mant = bits & 0xFFFFFFFFFFFFFL;
            if (exp == 0) exp = 1; else mant |= 1L << 52;
            exp -= 1075; /* |x| = mant * 2^exp */
            BigInteger scale = BigInteger.Pow(10, digits);
            BigInteger q;
            if (exp >= 0)
            {
                q = new BigInteger(mant) * BigInteger.Pow(2, exp) * scale;
            }
            else
            {
                BigInteger num = new BigInteger(mant) * scale;
                BigInteger den = BigInteger.One << -exp;
                BigInteger rem;
                q = BigInteger.DivRem(num, den, out rem);
                if (rem * 2 >= den) q += 1;
            }
            double r = (double)q / Math.Pow(10, digits);
            /* toFixed(-0.0001) is "-0.000", and +"-0.000" is -0 */
            return neg ? -r : r;
        }
    }
}
