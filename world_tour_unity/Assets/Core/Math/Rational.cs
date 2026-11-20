using System;
using System.Collections.Generic;
using System.Globalization;

namespace WorldTour.Core.Maths
{
    /// <summary>
    /// An exact fraction, ported from world_tour/math/rational.js. Always reduced, with a positive
    /// denominator (the JS <c>R(n, d)</c> result), so structural equality is value equality.
    /// The JS works in doubles, exact up to 2^53; here every product is checked and throws
    /// OverflowException instead of silently losing precision.
    /// </summary>
    public readonly struct Rational : IEquatable<Rational>
    {
        public readonly long N;
        // Stored as d - 1 so default(Rational) is 0/1, never 0/0.
        private readonly long _dMinus1;

        public long D => _dMinus1 + 1;

        private Rational(long n, long d)
        {
            N = n;
            _dMinus1 = d - 1;
        }

        public static readonly Rational Zero = new Rational(0, 1);

        // ---------- integer helpers ----------

        /// <summary>JS: <c>gcd</c>. Works on absolute values; gcd(0, 0) is 1.</summary>
        public static long Gcd(long a, long b)
        {
            a = Math.Abs(a);
            b = Math.Abs(b);
            while (b != 0)
            {
                long t = a % b;
                a = b;
                b = t;
            }
            return a == 0 ? 1 : a;
        }

        /// <summary>JS: <c>lcm = a / gcd(a, b) * b</c>. Keeps the JS sign: lcm(-4, 6) is -12, lcm(0, 0) is 0.</summary>
        public static long Lcm(long a, long b)
        {
            return checked(a / Gcd(a, b) * b);
        }

        // ---------- construction ----------

        /// <summary>JS: <c>R(n, d = 1)</c>, reduced, sign on the numerator. Throws on a zero denominator.</summary>
        public static Rational R(long n, long d = 1)
        {
            if (d == 0) throw new ArgumentException("bad fraction " + n.ToString(CultureInfo.InvariantCulture) + "/0");
            if (d < 0)
            {
                n = checked(-n);
                d = checked(-d);
            }
            long g = Gcd(n, d);
            return new Rational(n / g, d / g);
        }

        // ---------- arithmetic ----------

        public static Rational Add(Rational a, Rational b) => R(checked(a.N * b.D + b.N * a.D), checked(a.D * b.D));
        public static Rational Sub(Rational a, Rational b) => R(checked(a.N * b.D - b.N * a.D), checked(a.D * b.D));
        public static Rational Mul(Rational a, Rational b) => R(checked(a.N * b.N), checked(a.D * b.D));
        public static Rational Div(Rational a, Rational b) => R(checked(a.N * b.D), checked(a.D * b.N));

        /// <summary>JS: <c>eq</c>, by cross-multiplying.</summary>
        public static bool Eq(Rational a, Rational b) => checked(a.N * b.D) == checked(b.N * a.D);

        /// <summary>JS: <c>cmp</c>. Returns the cross-product difference (negative, zero or positive), as the JS does.</summary>
        public static long Cmp(Rational a, Rational b) => checked(a.N * b.D - b.N * a.D);

        /// <summary>JS: <c>sum = list.reduce(add, R(0))</c>.</summary>
        public static Rational Sum(IEnumerable<Rational> list)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            Rational acc = R(0);
            foreach (Rational f in list) acc = Add(acc, f);
            return acc;
        }

        /// <summary>JS: <c>isSimplest(f) = gcd(f.n, f.d) === 1</c>. A Rational is always reduced, so this is true.</summary>
        public static bool IsSimplest(Rational f) => Gcd(f.N, f.D) == 1;

        /// <summary>JS <c>isSimplest</c> on a raw, possibly unreduced numerator and denominator.</summary>
        public static bool IsSimplest(long n, long d) => Gcd(n, d) == 1;

        /// <summary>JS: <c>isInt(f) = R(f.n, f.d).d === 1</c>.</summary>
        public static bool IsInt(Rational f) => f.D == 1;

        /// <summary>JS <c>isInt</c> on a raw numerator and denominator (throws on a zero denominator, as R does).</summary>
        public static bool IsInt(long n, long d) => R(n, d).D == 1;

        /// <summary>JS: <c>num(f) = f.n / f.d</c>, as a double.</summary>
        public static double Num(Rational f) => (double)f.N / f.D;

        // ---------- formatting ----------

        /// <summary>
        /// JS: <c>mixed</c>. Whole part is <c>Math.floor(n / d)</c> (floor of the double quotient), so a
        /// negative value keeps a positive remainder: -1/2 is { w: -1, n: 1, d: 2 }.
        /// </summary>
        public static MixedNumber Mixed(Rational f)
        {
            long w = (long)Math.Floor((double)f.N / f.D);
            return new MixedNumber(w, checked(f.N - w * f.D), f.D);
        }

        /// <summary>JS: <c>str</c>: "3/4", "1 1/2", "2". Negatives follow <c>mixed</c>: -1/2 is "-1 1/2".</summary>
        public static string Str(Rational f)
        {
            MixedNumber m = Mixed(f);
            if (m.N == 0) return Js(m.W);
            return m.W != 0 ? Js(m.W) + " " + Js(m.N) + "/" + Js(m.D) : Js(m.N) + "/" + Js(m.D);
        }

        /// <summary>
        /// JS: <c>dec</c>: a terminating decimal written out, 13/20 is "0.65". At most 9 decimal places;
        /// otherwise throws InvalidOperationException("not a terminating decimal").
        /// </summary>
        public static string Dec(Rational f)
        {
            long n = f.N, d = f.D;
            int k = 0;
            long p = 1;
            while (checked(p * n) % d != 0)
            {
                k++;
                p = checked(p * 10);
                if (k > 9) throw new InvalidOperationException("not a terminating decimal");
            }
            long scaled = checked(p * n) / d;
            string s = Js(Math.Abs(scaled)).PadLeft(k + 1, '0');
            string output = k != 0 ? s.Substring(0, s.Length - k) + "." + s.Substring(s.Length - k) : s;
            return (scaled < 0 ? "-" : "") + output;
        }

        private static string Js(long v) => v.ToString(CultureInfo.InvariantCulture);

        // ---------- equality ----------

        public bool Equals(Rational other) => N == other.N && D == other.D;
        public override bool Equals(object obj) => obj is Rational r && Equals(r);
        public override int GetHashCode() => unchecked(N.GetHashCode() * 397 ^ D.GetHashCode());
        public static bool operator ==(Rational a, Rational b) => a.Equals(b);
        public static bool operator !=(Rational a, Rational b) => !a.Equals(b);

        /// <summary>Debug form "n/d" (not the JS <c>str</c>; use <see cref="Str"/> for display).</summary>
        public override string ToString() => Js(N) + "/" + Js(D);
    }

    /// <summary>JS <c>mixed</c> result: { w, n, d }.</summary>
    public readonly struct MixedNumber : IEquatable<MixedNumber>
    {
        public readonly long W;
        public readonly long N;
        public readonly long D;

        public MixedNumber(long w, long n, long d)
        {
            W = w;
            N = n;
            D = d;
        }

        public bool Equals(MixedNumber other) => W == other.W && N == other.N && D == other.D;
        public override bool Equals(object obj) => obj is MixedNumber m && Equals(m);
        public override int GetHashCode() => unchecked((W.GetHashCode() * 397 ^ N.GetHashCode()) * 397 ^ D.GetHashCode());
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} {1}/{2}", W, N, D);
    }
}
