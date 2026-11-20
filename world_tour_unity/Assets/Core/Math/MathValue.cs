using System;
using System.Globalization;

namespace WorldTour.Core.Maths
{
    public enum ValueKind { Null = 0, Number, Fraction, Point }

    /// <summary>
    /// One JS answer value as the web game passes it around: a number (cents or a whole number),
    /// a fraction <c>{ n, d }</c> (possibly unreduced, as generators build traps), a point
    /// <c>{ x, y }</c>, or null. Parts are doubles, as in JS. A part the value doesn't have
    /// reads as NaN, which behaves like JS <c>undefined</c> in comparisons and arithmetic
    /// (every comparison false, every product NaN).
    /// </summary>
    public readonly struct MathValue : IEquatable<MathValue>
    {
        public readonly ValueKind Kind;
        private readonly double _a;
        private readonly double _b;

        private MathValue(ValueKind kind, double a, double b)
        {
            Kind = kind;
            _a = a;
            _b = b;
        }

        public static readonly MathValue Null = default;

        public static MathValue Num(double value) => new MathValue(ValueKind.Number, value, 0);
        public static MathValue Frac(double n, double d) => new MathValue(ValueKind.Fraction, n, d);
        public static MathValue Frac(Rational r) => new MathValue(ValueKind.Fraction, r.N, r.D);
        public static MathValue Pt(double x, double y) => new MathValue(ValueKind.Point, x, y);

        public double Number => Kind == ValueKind.Number ? _a : double.NaN;
        public double N => Kind == ValueKind.Fraction ? _a : double.NaN;
        public double D => Kind == ValueKind.Fraction ? _b : double.NaN;
        public double X => Kind == ValueKind.Point ? _a : double.NaN;
        public double Y => Kind == ValueKind.Point ? _b : double.NaN;

        /// <summary>JS <c>typeof v === 'object'</c> for a non-null value.</summary>
        public bool IsObject => Kind == ValueKind.Fraction || Kind == ValueKind.Point;

        /// <summary>JS <c>Number.isInteger</c>.</summary>
        public static bool IsInteger(double x) => !double.IsNaN(x) && !double.IsInfinity(x) && Math.Floor(x) == x;

        /// <summary>JS <c>R(n, d)</c> on this value's parts; throws like R on anything but whole numbers and d != 0.</summary>
        public static Rational ToRational(double n, double d)
        {
            if (!IsInteger(n) || !IsInteger(d) || d == 0)
                throw new ArgumentException("bad fraction " + JsNumber.Str(n) + "/" + JsNumber.Str(d));
            return Rational.R(checked((long)n), checked((long)d));
        }

        public bool Equals(MathValue o) => Kind == o.Kind && _a.Equals(o._a) && _b.Equals(o._b);
        public override bool Equals(object obj) => obj is MathValue m && Equals(m);
        public override int GetHashCode() => unchecked(((int)Kind * 397 ^ _a.GetHashCode()) * 397 ^ _b.GetHashCode());

        /// <summary>Canonical text: "350", "3/4", "(3,5)", "null". Used by tests and logs, never shown to the player.</summary>
        public override string ToString()
        {
            switch (Kind)
            {
                case ValueKind.Number: return JsNumber.Str(_a);
                case ValueKind.Fraction: return JsNumber.Str(_a) + "/" + JsNumber.Str(_b);
                case ValueKind.Point: return "(" + JsNumber.Str(_a) + "," + JsNumber.Str(_b) + ")";
                default: return "null";
            }
        }
    }

    /// <summary>JS <c>String(number)</c> for the numbers this game uses.</summary>
    public static class JsNumber
    {
        public static string Str(double x)
        {
            if (double.IsNaN(x)) return "NaN";
            if (double.IsPositiveInfinity(x)) return "Infinity";
            if (double.IsNegativeInfinity(x)) return "-Infinity";
            if (Math.Floor(x) == x && Math.Abs(x) < 1e18) return ((long)x).ToString(CultureInfo.InvariantCulture); // -0 prints "0", as in JS
            return x.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
