using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WorldTour.Core.Maths.Gen
{
    /// <summary>
    /// SOUNDCHECK: quick facts and powers of ten (5.NBT.1-2). Ported from world_tour/math/gen/fluency.js; every
    /// RNG call happens in the same order, so problems match the JS. Multiple choice, with a timer that only
    /// changes bonus fans.
    /// L1: x and / facts to 10.
    /// L2: facts to 12, x and / by 10 and 100.
    /// L3: x and / by 10, 100, 1000 with decimals, and powers of ten (10^3, 4.5 x 10^2).
    /// </summary>
    public static class Fluency
    {
        public const string Skill = "flu";

        private static readonly string[] KindsL1 = { "mul", "div" };
        private static readonly string[] KindsL2 = { "mul", "div", "pow", "pow" };
        private static readonly string[] KindsL3 = { "decpow", "decpow", "decdiv", "exp" };

        /// <summary>JS SUP: superscript 1 to 4 (U+00B9, U+00B2, U+00B3, U+2074), built from code points to keep the source ASCII.</summary>
        public static string Sup(int k)
        {
            switch (k)
            {
                case 1: return ((char)0x00B9).ToString();
                case 2: return ((char)0x00B2).ToString();
                case 3: return ((char)0x00B3).ToString();
                case 4: return ((char)0x2074).ToString();
                default: return "undefined"; // JS SUP[k] for a missing key; never reached
            }
        }

        // JS: f = n => n.toLocaleString('en-US'), for whole numbers
        private static string F(long n) => n.ToString("#,##0", CultureInfo.InvariantCulture);
        private static string S(long n) => n.ToString(CultureInfo.InvariantCulture);
        private static long Pow10(int k) { long p = 1; for (int i = 0; i < k; i++) p *= 10; return p; }
        private static Rational R(long n, long d = 1) => Rational.R(n, d);
        private static MathValue Fv(Rational r) => MathValue.Frac(r);
        private static MathValue N(long v) => MathValue.Num(v);
        private static string Dec(Rational r) => Rational.Dec(r);

        /// <summary>
        /// Hint 2 for moving the decimal point (QUESTIONS 9.1 and 14, an intended difference from the JS): place-value
        /// wording that is true for every problem. x 10, 100, 1000 moves every digit left; / moves it right. When a
        /// zero has to be filled in (8.8 x 100) or drops off the end (140 / 10), it asks about the zero instead of
        /// saying it, so the last step stays hers. Never prints the digits or the answer.
        /// </summary>
        public static string PlaceValueHint(bool times, long power, int places, bool zeroChanges) =>
            (times ? Glyph.Times : Glyph.Divide) + " " + F(power) + " moves every digit " + S(places) + " place" + (places == 1 ? "" : "s")
            + " to the " + (times ? "left" : "right") + "."
            + (zeroChanges ? (times ? " what fills the empty place?" : " what happens to the 0 at the end?") : "");

        public static Problem Generate(int level, long seed) => Guard.Guarded(Build, level, seed);

        private static Problem Build(int level, long seed, Rng r)
        {
            string kind = level == 1 ? r.Pick(KindsL1) : level == 2 ? r.Pick(KindsL2) : r.Pick(KindsL3);
            Answer answer;
            string prompt, label;
            List<Trap> traps;
            List<MathValue> fillers;
            List<string> steps;
            string x = " " + Glyph.Times + " ", dv = " " + Glyph.Divide + " ";

            if (kind == "mul" || kind == "div")
            {
                int top = level == 1 ? 10 : 12;
                int a = r.Int(2, top), b = r.Int(2, top), p = a * b;
                if (kind == "mul")
                {
                    answer = Answer.Int(p);
                    prompt = S(a) + x + S(b);
                    label = prompt;
                    traps = new List<Trap> { new Trap(N(a * (b + 1)), "neighbor-fact"), new Trap(N(a * (b - 1)), "neighbor-fact"), new Trap(N(a + b), "added") };
                    fillers = new List<MathValue> { N((a + 1) * b), N((a - 1) * b), N(p + 10), N(p - 2) };
                    steps = new List<string>
                    {
                        "think of " + S(a) + " groups of " + S(b) + ".",
                        S(a) + x + S(b - 1) + " = " + S(a * (b - 1)) + ". one more " + S(a) + Glyph.Ellipsis,
                        S(a) + x + S(b) + " = <b>" + S(p) + "</b>"
                    };
                }
                else
                {
                    answer = Answer.Int(b);
                    prompt = S(p) + dv + S(a);
                    label = prompt;
                    traps = new List<Trap> { new Trap(N(b + 1), "neighbor-fact"), new Trap(N(b - 1), "neighbor-fact"), new Trap(N(p - a), "subtracted") };
                    fillers = new List<MathValue> { N(b + 2), N(b - 2), N(a) };
                    steps = new List<string>
                    {
                        "what times " + S(a) + " makes " + S(p) + "?",
                        S(a) + x + S(b - 1) + " = " + S(a * (b - 1)) + ". a little more" + Glyph.Ellipsis,
                        S(a) + x + S(b) + " = " + S(p) + ", so <b>" + S(b) + "</b>"
                    };
                }
            }
            else if (kind == "pow")
            {
                int k = r.Int(1, 2);
                long p = Pow10(k);
                bool times = r.Chance(.5);
                string places = S(k) + " place" + (k > 1 ? "s" : "");
                if (times)
                {
                    long n = r.Int(2, 999);
                    answer = Answer.Int(n * p);
                    prompt = F(n) + x + F(p);
                    traps = new List<Trap> { new Trap(N(n * p * 10), "zeros-off"), new Trap(N(k == 2 ? n * 10 : n * 100), "zeros-off"), new Trap(N(n + p), "added") };
                    fillers = new List<MathValue> { N(n * p * 100), N(n) };
                    steps = new List<string>
                    {
                        "multiplying by " + F(p) + " moves every digit " + places + " to the left.",
                        k == 2 ? F(n) + x + "10 = " + F(n * 10) + ". once more" + Glyph.Ellipsis : "every digit shifts one place left, and a 0 fills the ones place.",
                        F(n) + x + F(p) + " = <b>" + F(n * p) + "</b>"
                    };
                }
                else
                {
                    long q = r.Int(2, 999), n = q * p;
                    answer = Answer.Int(q);
                    prompt = F(n) + dv + F(p);
                    traps = new List<Trap>
                    {
                        new Trap(N(n * p), "wrong-direction"),
                        new Trap(k == 2 ? N(q * 10) : (q % 10 == 0 ? N(q / 10) : MathValue.Null), "zeros-off"),
                        new Trap(N(n - p), "subtracted")
                    };
                    fillers = new List<MathValue> { N(q * 100), N(q + 10) };
                    steps = new List<string>
                    {
                        "dividing by " + F(p) + " moves every digit " + places + " to the right.",
                        k == 2 ? F(n) + dv + "10 = " + F(n / 10) + ". once more" + Glyph.Ellipsis : "every digit shifts one place right, so the 0 in the ones place drops off.",
                        F(n) + dv + F(p) + " = <b>" + F(q) + "</b>"
                    };
                }
                label = prompt;
            }
            else if (kind == "decpow" || kind == "decdiv")
            {
                int k = r.Int(1, 3);
                long p = Pow10(k);
                string places = S(k) + " place" + (k > 1 ? "s" : "");
                if (kind == "decpow")
                {
                    int pl = r.Int(1, 2);
                    int m;
                    do m = r.Int(11, pl == 1 ? 99 : 999); while (m % 10 == 0);
                    Rational xv = R(m, Pow10(pl));
                    Rational ans = Rational.Mul(xv, R(p));
                    answer = Answer.Decimal(ans);
                    prompt = Dec(xv) + x + F(p);
                    traps = new List<Trap>
                    {
                        new Trap(Fv(Rational.Mul(xv, R(p * 10))), "zeros-off"),
                        new Trap(k > 1 ? Fv(Rational.Mul(xv, R(p / 10))) : MathValue.Null, "zeros-off"),
                        new Trap(Fv(Rational.Div(xv, R(p))), "wrong-direction")
                    };
                    fillers = new List<MathValue> { Fv(Rational.Mul(xv, R(p * 100))), Fv(Rational.Mul(xv, R(10))) };
                    steps = new List<string>
                    {
                        Glyph.Times + " " + F(p) + " moves the decimal point " + places + " to the right.",
                        // Unity (QUESTIONS 9.1, 14): the JS printed "the digits stay " + the reduced numerator for x 10
                        // (wrong for 8.82, the answer for 6.1) and "x 10 = ... keep going" for 100 and 1000. A zero is
                        // filled in exactly when the power has more zeros than xv has decimal places (8.8 x 100 = 880).
                        PlaceValueHint(true, p, k, k > pl),
                        Dec(xv) + x + F(p) + " = <b>" + Dec(ans) + "</b>"
                    };
                }
                else
                {
                    int n = r.Int(2, 999);
                    Rational xv = R(n);
                    Rational ans = Rational.Div(xv, R(p));
                    answer = Answer.Decimal(ans);
                    prompt = F(n) + dv + F(p);
                    traps = new List<Trap>
                    {
                        new Trap(Fv(Rational.Div(xv, R(p * 10))), "zeros-off"),
                        new Trap(k > 1 ? Fv(Rational.Div(xv, R(p / 10))) : Fv(Rational.Mul(xv, R(10))), k > 1 ? "zeros-off" : "wrong-direction"),
                        new Trap(Fv(Rational.Mul(xv, R(p))), "wrong-direction")
                    };
                    fillers = new List<MathValue> { Fv(Rational.Div(xv, R(p * 100))), Fv(R(n)) };
                    steps = new List<string>
                    {
                        Glyph.Divide + " " + F(p) + " moves the decimal point " + places + " to the left.",
                        // Unity (QUESTIONS 9.1, 14): the JS printed n's digits for / 10 ("the digits stay 7") and
                        // "/ 10 = ... keep going" for 100 and 1000. A 0 drops off the end exactly when n ends in 0.
                        PlaceValueHint(false, p, k, n % 10 == 0),
                        F(n) + dv + F(p) + " = <b>" + Dec(ans) + "</b>"
                    };
                }
                label = prompt;
            }
            else
            {
                int k = r.Int(2, 4);
                bool withX = r.Chance(.6);
                if (!withX)
                {
                    answer = Answer.Decimal(R(Pow10(k)));
                    prompt = "10" + Sup(k);
                    traps = new List<Trap>
                    {
                        new Trap(Fv(R(10 * k)), "multiplied-exponent"),
                        new Trap(Fv(R(Pow10(k + 1))), "zeros-off"),
                        new Trap(Fv(R(long.Parse("10" + S(k), CultureInfo.InvariantCulture))), "wrote-digits")
                    };
                    fillers = new List<MathValue> { Fv(R(Pow10(k - 1))) };
                    steps = new List<string>
                    {
                        "10" + Sup(k) + " means " + S(k) + " tens multiplied together.",
                        string.Join(x, Enumerable.Repeat("10", k)),
                        "10" + Sup(k) + " = <b>" + F(Pow10(k)) + "</b>: a 1 with " + S(k) + " zeros."
                    };
                }
                else
                {
                    int m;
                    do m = r.Int(11, 99); while (m % 10 == 0);
                    Rational xv = R(m, 10);
                    int kk = Math.Min(k, 3);
                    Rational ans = Rational.Mul(xv, R(Pow10(kk)));
                    answer = Answer.Decimal(ans);
                    prompt = Dec(xv) + x + "10" + Sup(kk);
                    traps = new List<Trap>
                    {
                        new Trap(Fv(Rational.Mul(xv, R(10 * kk))), "multiplied-exponent"),
                        new Trap(Fv(Rational.Mul(xv, R(Pow10(kk + 1)))), "zeros-off"),
                        new Trap(Fv(Rational.Mul(xv, R(Pow10(kk - 1)))), "zeros-off")
                    };
                    fillers = new List<MathValue> { Fv(Rational.Div(xv, R(10))) };
                    steps = new List<string>
                    {
                        "10" + Sup(kk) + " = " + F(Pow10(kk)) + ". so this is " + Dec(xv) + x + F(Pow10(kk)) + ".",
                        Glyph.Times + " " + F(Pow10(kk)) + " moves the decimal point " + S(kk) + " places to the right.",
                        Dec(xv) + x + F(Pow10(kk)) + " = <b>" + Dec(ans) + "</b>"
                    };
                }
                label = prompt;
            }

            List<Choice> choices = Mistakes.ChoiceSet(r, answer, traps, fillers);
            return new Problem
            {
                Skill = Skill, Level = level, Kind = kind, Seed = seed,
                Params = JsonValue.Obj().Set("seconds", level == 1 ? 8 : level == 2 ? 9 : 12),
                Prompt = prompt + " = ?", Label = label, Answer = answer, Simplest = false,
                Choices = choices,
                // JS: choices.filter(c => !c.correct).map(c => ({ value: c.value, why: c.why })), not through cleanTraps
                Traps = choices.Where(c => !c.Correct).Select(c => new Trap(c.Value, c.Why)).ToList(),
                Steps = steps
            };
        }
    }
}
