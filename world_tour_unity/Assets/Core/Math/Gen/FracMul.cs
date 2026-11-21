using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WorldTour.Core.Maths.Gen
{
    /// <summary>
    /// SNACK TABLE (skill "fm"): multiplying and dividing fractions (5.NF.4-7), set at the green room's snack table, with
    /// no JS twin. A tray is one whole. Levels start at 0.
    /// L0 unitof: a unit fraction of a whole number that divides evenly (1/4 of 12).
    /// L1 part: a part of a part, unit x unit (1/2 of 1/4).
    /// L2 unitnon, nonnon (denominators up to 6), shaded (which expression matches the shaded region).
    /// L3 mixed (a mixed number x a fraction), missing (2/3 x ? = 20/21), compare (<, = or > without multiplying).
    /// Division (a switch, <see cref="DivisionEnabled"/>, mixed into L2 and L3): share (a unit fraction shared by a whole
    /// number, 1/4 / 2) and howmany (how many unit fractions in a whole number, 2 / 1/4).
    /// Any equivalent form is accepted (never simplest form), mixed or improper too. Every hint step is written so hints
    /// 1 and 2 (and <see cref="FracMulPicture"/>) never state the product or let it be counted out.
    /// </summary>
    public static class FracMul
    {
        public const string Skill = "fm";

        /// <summary>Config seam: false leaves share and howmany out (math_config.json fmDivision). The Game layer sets it at start.</summary>
        public static bool DivisionEnabled = true;

        /// <summary>Largest denominator of any factor the player is shown (the missing factor's may reach 7).</summary>
        public const int MaxDenominator = 6, MaxMissingDenominator = 7;

        public const int MinLevel = 0, MaxLevel = 3;

        public static readonly string Times = Glyph.Times, Divide = Glyph.Divide;

        static readonly string[] Items = { "pretzels", "cookies", "granola bars", "fruit cups", "sandwiches", "juice boxes", "popcorn bags", "veggie cups" };
        static readonly string[] Trays = { "pretzels", "cookies", "granola bars", "fruit", "sandwiches", "trail mix", "popcorn", "veggies" };
        /// <summary>The mixed-number problem's own ingredients: "[[1 1/2]] cups of oats".</summary>
        static readonly string[] Ingredients = { "oats", "raisins", "peanuts", "pretzel bits", "dried cranberries", "banana chips", "cereal", "sunflower seeds" };
        static readonly string[] Who = { "the drummer", "the bassist", "the lighting crew", "the opener", "the tour manager", "the dog" };
        static readonly string[] Names = { "", "", "halves", "thirds", "fourths", "fifths", "sixths", "sevenths" };

        // proper fractions in simplest form with a numerator of 2 or more, denominators 3..6
        static readonly long[][] NonUnit = { new long[] { 2, 3 }, new long[] { 3, 4 }, new long[] { 2, 5 }, new long[] { 3, 5 }, new long[] { 4, 5 }, new long[] { 5, 6 } };

        static string S(long v) { return v.ToString(CultureInfo.InvariantCulture); }
        static string F(Rational f) { return "[[" + Rational.Str(f) + "]]"; }
        /// <summary>A fraction written as given, not reduced and not turned into a mixed number: [[6/12]], [[10/7]].</summary>
        public static string Raw(long n, long d) { return "[[" + S(n) + "/" + S(d) + "]]"; }
        static string Apos { get { return Glyph.Apos; } }
        static string Part(int d) { return Names[d]; }
        /// <summary>"a tray of cookies", from the food's plural.</summary>
        static string TrayOf(string food) { return "tray of " + food; }
        /// <summary>"3 more fourths", "1 more half".</summary>
        static string More(long n, int d) { return S(n) + " more " + (n == 1 ? Singular(d) : Part(d)); }
        /// <summary>"between" for two, "among" for more.</summary>
        static string Among(int n) { return n == 2 ? "between" : "among"; }
        static string Singular(int d) { return d == 2 ? "half" : Names[d].TrimEnd('s'); }

        public static Problem Generate(int level, long seed) { return Generate(level, seed, DivisionEnabled); }

        public static Problem Generate(int level, long seed, bool division)
        {
            return Guard.Guarded((lv, s, r) => Build(lv, s, r, division), level, seed);
        }

        static Problem Build(int level, long seed, Rng r, bool division)
        {
            int lv = Math.Max(MinLevel, Math.Min(MaxLevel, level));
            string[] pool;
            if (lv == 0) pool = new[] { "unitof" };
            else if (lv == 1) pool = new[] { "part" };
            else if (lv == 2) pool = division ? new[] { "unitnon", "nonnon", "shaded", "share", "howmany" } : new[] { "unitnon", "nonnon", "shaded" };
            else pool = division ? new[] { "mixed", "missing", "compare", "share", "howmany" } : new[] { "mixed", "missing", "compare" };
            string kind = r.Pick(pool);
            Problem p;
            switch (kind)
            {
                case "unitof": p = UnitOf(r); break;
                case "part": p = PartOfPart(r); break;
                case "unitnon": p = Times2(r, true); break;
                case "nonnon": p = Times2(r, false); break;
                case "shaded": p = Shaded(r); break;
                case "share": p = Share(r, lv); break;
                case "howmany": p = HowMany(r, lv); break;
                case "mixed": p = Mixed(r); break;
                case "missing": p = Missing(r); break;
                default: p = Compare(r); break;
            }
            p.Skill = Skill; p.Level = level; p.Seed = seed; p.Kind = kind;
            p.Simplest = false;
            return p;
        }

        static JsonValue FracJ(long n, long d) { return JsonValue.Obj().Set("n", n).Set("d", d); }

        static Problem Make(string prompt, string label, Answer answer, JsonValue prms, List<string> steps, List<Trap> traps, List<Choice> choices = null)
        {
            return new Problem
            {
                Prompt = prompt, Label = label, Answer = answer, Params = prms, Steps = steps,
                Traps = Mistakes.CleanTraps(answer, traps), Choices = choices
            };
        }

        static MathValue V(Rational x) { return MathValue.Frac(x); }

        // ---------- L0: a unit fraction of a whole number ----------

        static Problem UnitOf(Rng r)
        {
            int d = r.Int(2, 6);
            int k = r.Int(2, Math.Min(9, 48 / d));
            int n = d * k;
            string who = r.Pick(Who), item = r.Pick(Items);
            string unit = F(Rational.R(1, d));
            var traps = new List<Trap>
            {
                new Trap(MathValue.Num((long)n * d), "multiplied-instead"),
                new Trap(MathValue.Num(n - d), "took-some-away"),
                new Trap(MathValue.Num(d), "gave-the-bottom"),
            };
            return Make(
                S(n) + " " + item + " sit on the snack table. " + who + " takes " + unit + " of them. how many is that?",
                unit + " of " + S(n), Answer.Int(k),
                JsonValue.Obj().Set("whole", n).Set("parts", d).Set("factors", JsonValue.Arr(FracJ(1, d))),
                new List<string>
                {
                    unit + " of " + S(n) + " means: split " + S(n) + " into " + S(d) + " equal groups, then take one group.",
                    "taking " + unit + " of a number is dividing it by " + S(d) + ". what times " + S(d) + " makes " + S(n) + "?",
                    S(n) + " " + Divide + " " + S(d) + " = " + S(k) + ", because " + S(k) + " " + Times + " " + S(d) + " = " + S(n) + ". so " + unit + " of " + S(n) + " is " + S(k) + "."
                },
                traps);
        }

        // ---------- L1: a part of a part ----------

        static Problem PartOfPart(Rng r)
        {
            int a = r.Int(2, 6), b = r.Int(2, 6);
            string who = r.Pick(Who), who2 = r.Pick(Who.Where(w => w != who).ToArray()), tray = r.Pick(Trays);
            Rational ans = Rational.R(1, (long)a * b);
            string fa = F(Rational.R(1, a)), fb = F(Rational.R(1, b));
            var traps = new List<Trap>
            {
                new Trap(V(Rational.R(1, a + b)), "added-the-bottoms"),
                new Trap(V(Rational.R(1, a)), "kept-one-part"),
                new Trap(V(Rational.R(1, b)), "kept-one-part"),
            };
            return Make(
                who + " sets aside " + fa + " of the " + TrayOf(tray) + " for the encore. " + who2 + " gets " + fb + " of that part. what fraction of the whole tray is " + who2 + Apos + "s share?",
                fb + " of " + fa, Answer.Fraction(ans),
                AreaParams(a, 1, b, 1, new[] { Tuple.Create(1L, (long)a), Tuple.Create(1L, (long)b) }),
                new List<string>
                {
                    "this is a part of a part. the tray is cut into " + Part(a) + ", then one of those parts is cut into " + S(b) + " equal shares.",
                    "the picture cuts one part into " + S(b) + " shares. every other part would be cut the same way. how many of these little shares would fill the whole tray?",
                    fb + " of " + fa + " is " + fa + " " + Times + " " + fb + ". tops: 1 " + Times + " 1 = 1. bottoms: " + S(a) + " " + Times + " " + S(b) + " = " + S((long)a * b) + ". so " + F(ans) + " of the tray."
                }, traps);
        }

        static JsonValue AreaParams(int cols, int colsOn, int rows, int rowsOn, IEnumerable<Tuple<long, long>> factors)
        {
            return JsonValue.Obj().Set("cols", cols).Set("colsOn", colsOn).Set("rows", rows).Set("rowsOn", rowsOn)
                .Set("factors", JsonValue.Arr(factors.Select(f => FracJ(f.Item1, f.Item2))));
        }

        // ---------- L2: unit x non-unit, non-unit x non-unit ----------

        static Problem Times2(Rng r, bool unit)
        {
            // rows = the fraction taken ("a/b of ..."), cols = the part it is taken from ("... c/d")
            long a, b, c, d;
            if (unit)
            {
                long[] nu = r.Pick(NonUnit);
                long m = r.Int(2, 6);
                if (r.Chance(.5)) { a = 1; b = m; c = nu[0]; d = nu[1]; } else { a = nu[0]; b = nu[1]; c = 1; d = m; }
            }
            else
            {
                long[] x = r.Pick(NonUnit), y = r.Pick(NonUnit);
                a = x[0]; b = x[1]; c = y[0]; d = y[1];
            }
            string who = r.Pick(Who), tray = r.Pick(Trays);
            Rational ans = Rational.R(a * c, b * d);
            string fa = Raw(a, b), fc = Raw(c, d);
            var traps = new List<Trap>
            {
                new Trap(V(Rational.R(a + c, b + d)), "added-across"),
                new Trap(V(Rational.R(a * c, b)), "multiplied-only-the-tops"),
                new Trap(V(Rational.R(a * c, d)), "multiplied-only-the-tops"),
                new Trap(V(Rational.R(a, b * d)), "forgot-a-top"),
                new Trap(V(Rational.R(c, b * d)), "forgot-a-top"),
            };
            string rawAns = Raw(a * c, b * d);
            return Make(
                "there is " + fc + " of a " + TrayOf(tray) + " left. " + who + " takes " + fa + " of that. how much of a whole tray is that?",
                fa + " of " + fc, Answer.Fraction(ans),
                AreaParams((int)d, (int)c, (int)b, (int)a, new[] { Tuple.Create(a, b), Tuple.Create(c, d) }),
                new List<string>
                {
                    "a part of a part again: take " + fa + " of " + fc + ". the tray is cut into " + Part((int)d) + " first, and " + S(c) + (c == 1 ? " of them is left." : " of them are left."),
                    "the picture cuts the part that is left into " + S(b) + " rows. you take " + S(a) + " of those rows. how many little pieces is that, and how many of them would fill a whole tray?",
                    fa + " " + Times + " " + fc + ": tops " + S(a) + " " + Times + " " + S(c) + " = " + S(a * c) + ", bottoms " + S(b) + " " + Times + " " + S(d) + " = " + S(b * d) + ". " + rawAns + (Rational.R(a * c, b * d).D != b * d ? " = " + F(ans) : "") + "."
                }, traps);
        }

        // ---------- L2: which expression matches the shaded region ----------

        static Problem Shaded(Rng r)
        {
            for (;;)
            {
                int b = r.Int(2, 6), d = r.Int(2, 6);
                int a = r.Int(1, b - 1), c = r.Int(1, d - 1);
                Rational ans = Rational.R((long)a * c, (long)b * d);
                string correct = r.Chance(.5) ? Raw(a, b) + " " + Times + " " + Raw(c, d) : Raw(c, d) + " " + Times + " " + Raw(a, b);
                var cand = new List<Tuple<string, Rational, string>>
                {
                    Tuple.Create(Raw(a, b) + " + " + Raw(c, d), Rational.Add(Rational.R(a, b), Rational.R(c, d)), "added-them-up"),
                    Tuple.Create(Raw(b - a, b) + " " + Times + " " + Raw(c, d), Rational.R((long)(b - a) * c, (long)b * d), "counted-the-unshaded"),
                    Tuple.Create(Raw(a, b) + " " + Times + " " + Raw(d - c, d), Rational.R((long)a * (d - c), (long)b * d), "counted-the-unshaded"),
                    Tuple.Create(Raw(1, b) + " " + Times + " " + Raw(1, d), Rational.R(1, (long)b * d), "one-cell-only"),
                };
                var ok = new List<Tuple<string, Rational, string>>();
                foreach (var t in r.Shuffle(cand))
                {
                    if (Rational.Eq(t.Item2, ans)) continue;
                    if (ok.Any(o => Rational.Eq(o.Item2, t.Item2) || o.Item1 == t.Item1)) continue;
                    ok.Add(t);
                }
                if (ok.Count < 3) continue;
                var choices = new List<Choice> { new Choice(correct, true, null) };
                foreach (var t in ok.Take(3)) choices.Add(new Choice(t.Item1, false, t.Item3));
                choices = r.Shuffle(choices);
                JsonValue prm = AreaParams(d, c, b, a, new[] { Tuple.Create((long)a, (long)b), Tuple.Create((long)c, (long)d) });
                prm.Set("overlap", true);
                string rawAns = Raw((long)a * c, (long)b * d);
                var p = Make(
                    "the tour manager cut a tray into rows and columns and shaded part of it. which expression matches the shaded part?",
                    "match the picture", Answer.Fraction(ans), prm,
                    new List<string>
                    {
                        "read the picture two ways: how much of the tray's height is shaded, and how much of its width?",
                        "count the rows and the columns the whole tray is cut into. then count how many of each the shaded part covers.",
                        "the tray has " + S(b) + " rows and " + S(d) + " columns. the shading covers " + S(a) + " of the " + S(b) + " rows and " + S(c) + " of the " + S(d) + " columns. so it is " + Raw(a, b) + " " + Times + " " + Raw(c, d) + ", which is " + rawAns + (ans.D != (long)b * d ? " = " + F(ans) : "") + " of the tray."
                    },
                    new List<Trap>(), choices);
                return p;
            }
        }

        // ---------- division ----------

        static Problem Share(Rng r, int level)
        {
            int cap = level <= 2 ? 4 : 6;
            int d = r.Int(2, cap), n = r.Int(2, cap);
            string tray = r.Pick(Trays);
            Rational ans = Rational.R(1, (long)d * n);
            string u = F(Rational.R(1, d));
            var traps = new List<Trap>
            {
                new Trap(V(Rational.R(n, d)), "multiplied-the-share"),
                new Trap(V(Rational.R(d, n)), "divided-wrong-way"),
                new Trap(V(Rational.R(1, d + n)), "added-the-bottoms"),
            };
            return Make(
                u + " of a " + TrayOf(tray) + " is left. the crew shares it evenly " + Among(n) + " " + S(n) + " people. what fraction of the whole tray does each person get?",
                u + " " + Divide + " " + S(n), Answer.Fraction(ans),
                JsonValue.Obj().Set("parts", d).Set("split", n).Set("factors", JsonValue.Arr(FracJ(1, d))),
                new List<string>
                {
                    "sharing " + u + " " + Among(n) + " " + S(n) + " people makes a part of a part: each share is " + u + " cut into " + S(n) + " equal pieces.",
                    "the picture cuts one " + Singular(d) + " into " + S(n) + " shares. how many of those little shares would fill the whole tray?",
                    u + " " + Divide + " " + S(n) + " = " + u + " " + Times + " " + F(Rational.R(1, n)) + " = " + F(ans) + ". check: " + F(ans) + " " + Times + " " + S(n) + " = " + u + "."
                }, traps);
        }

        static Problem HowMany(Rng r, int level)
        {
            int w = r.Int(2, level <= 2 ? 3 : 4), d = r.Int(2, level <= 2 ? 4 : 6);
            string tray = r.Pick(Trays);
            long ans = (long)w * d;
            string u = F(Rational.R(1, d));
            var traps = new List<Trap>
            {
                new Trap(V(Rational.R(w, d)), "divided-by-the-bottom"),
                new Trap(V(Rational.R(w + d)), "added-them-up"),
                new Trap(V(Rational.R(d)), "one-tray-only"),
            };
            return Make(
                S(w) + " trays of " + tray + " get cut into servings. each serving is " + u + " of a tray. how many servings is that?",
                S(w) + " " + Divide + " " + u, Answer.Fraction(Rational.R(ans)),
                JsonValue.Obj().Set("wholes", w).Set("parts", d).Set("factors", JsonValue.Arr(FracJ(1, d))),
                new List<string>
                {
                    "how many " + u + "s fit in " + S(w) + "? start with just one whole tray.",
                    "the picture cuts the first tray into " + Part(d) + ". the other " + S(w - 1) + (w - 1 == 1 ? " tray gets" : " trays get") + " cut the same way. how many servings in all?",
                    S(w) + " " + Divide + " " + u + " = " + S(w) + " " + Times + " " + S(d) + " = " + S(ans) + ". check: " + S(ans) + " " + Times + " " + u + " = " + S(w) + "."
                }, traps);
        }

        // ---------- L3 ----------

        static Problem Mixed(Rng r)
        {
            long w, n, d, c, e;
            for (;;)
            {
                w = r.Int(1, 3); d = r.Int(2, 4); n = r.Int(1, (int)d - 1);
                e = r.Int(2, 6); c = r.Int(1, (int)e - 1);
                if (Rational.Gcd(n, d) != 1 || Rational.Gcd(c, e) != 1) continue;
                break;
            }
            string stuff = r.Pick(Ingredients);
            long x = w * d + n;
            Rational ans = Rational.R(x * c, d * e);
            string cup = ans.N <= ans.D ? "cup" : "cups";
            string mixed = "[[" + S(w) + " " + S(n) + "/" + S(d) + "]]", frac = F(Rational.R(c, e)), imp = Raw(x, d);
            var traps = new List<Trap>
            {
                new Trap(V(Rational.R(n * c, d * e)), "forgot-the-whole"),
                new Trap(V(Rational.Add(Rational.R(w * c, e), Rational.R(n, d))), "multiplied-only-the-whole"),
                new Trap(V(Rational.R(x * c, d)), "multiplied-only-the-tops"),
            };
            return Make(
                "the snack mix recipe uses " + mixed + " cups of " + stuff + ". the band makes " + frac + " of the recipe. how many cups of " + stuff + " is that?",
                mixed + " " + Times + " " + frac, Answer.Fraction(ans),
                JsonValue.Obj().Set("whole", w).Set("factors", JsonValue.Arr(FracJ(n, d), FracJ(c, e))),
                new List<string>
                {
                    "a mixed number times a fraction: first write the mixed number as one fraction, with all its pieces counted.",
                    mixed + " is " + S(w) + " whole" + (w > 1 ? "s" : "") + " and " + More(n, (int)d) + ". that's " + imp + ". now it's a fraction times a fraction: multiply the tops, then the bottoms.",
                    mixed + " = " + imp + ". " + imp + " " + Times + " " + frac + " = " + Raw(x * c, d * e) + (ans.D != d * e ? " = " + F(ans) : "") + ". so " + F(ans) + " " + cup + "."
                }, traps);
        }

        static Problem Missing(Rng r)
        {
            long a, b, p, q;
            for (;;)
            {
                b = r.Int(2, 6); a = r.Int(1, (int)b - 1);
                q = r.Int(2, MaxMissingDenominator); p = r.Int(1, (int)(2 * q) - 1);
                if (p == q) continue;
                if (Rational.Gcd(a, b) != 1 || Rational.Gcd(p, q) != 1) continue;
                if (Rational.Gcd(a * p, b * q) != 1) continue;     // the product is shown in simplest form, so tops and bottoms divide exactly
                break;
            }
            Rational x = Rational.R(p, q), known = Rational.R(a, b), prod = Rational.R(a * p, b * q);
            string fk = F(known), fp = Raw(a * p, b * q);
            var traps = new List<Trap>
            {
                new Trap(V(Rational.Mul(known, prod)), "multiplied-the-known"),
                new Trap(V(Rational.Div(known, prod)), "divided-wrong-way"),
                new Trap(V(Rational.Sub(prod, known)), "subtracted-the-known"),
            };
            return Make(
                "the recipe card got smudged. " + fk + " " + Times + " ? = " + fp + ". what fraction goes in the box?",
                fk + " " + Times + " ? = " + fp, Answer.Fraction(x),
                JsonValue.Obj().Set("factors", JsonValue.Arr(FracJ(a, b), FracJ(p, q))).Set("product", FracJ(a * p, b * q)),
                new List<string>
                {
                    "work backwards. the missing factor is whatever you multiply " + fk + " by to land on " + fp + ".",
                    "look at the tops first: " + S(a) + " " + Times + " ? = " + S(a * p) + ". then the bottoms: " + S(b) + " " + Times + " ? = " + S(b * q) + ".",
                    "tops: " + S(a) + " " + Times + " " + S(p) + " = " + S(a * p) + ". bottoms: " + S(b) + " " + Times + " " + S(q) + " = " + S(b * q) + ". so the box holds " + F(x) + ". check: " + fk + " " + Times + " " + F(x) + " = " + fp + "."
                }, traps);
        }

        /// <summary>The three choices of a comparison, in this order.</summary>
        public static readonly string[] CompareWords = { "less than  <", "equal to  =", "greater than  >" };

        static Problem Compare(Rng r)
        {
            int truth; { double t = r.Next(); truth = t < .4 ? 0 : t < .8 ? 2 : 1; }
            long fa, fb, w = 0;            // the fixed factor: a proper fraction, or a mixed number
            string fixedText; Rational fixedV;
            if (r.Chance(.35))
            {
                for (;;) { w = r.Int(1, 2); fb = r.Int(2, 4); fa = r.Int(1, (int)fb - 1); if (Rational.Gcd(fa, fb) == 1) break; }
                fixedText = "[[" + S(w) + " " + S(fa) + "/" + S(fb) + "]]"; fixedV = Rational.R(w * fb + fa, fb);
            }
            else
            {
                for (;;) { fb = r.Int(2, 6); fa = r.Int(1, (int)fb - 1); if (Rational.Gcd(fa, fb) == 1) break; }
                fixedText = Raw(fa, fb); fixedV = Rational.R(fa, fb);
            }
            long gn, gd; string otherText;
            if (truth == 0)
            {
                for (;;) { gd = r.Int(2, 6); gn = r.Int(1, (int)gd - 1); if (Rational.Gcd(gn, gd) == 1) break; }
                otherText = Raw(gn, gd);
            }
            else if (truth == 2)
            {
                if (r.Chance(.5))
                {
                    long ww;
                    for (;;) { ww = r.Int(1, 2); gd = r.Int(2, 4); gn = r.Int(1, (int)gd - 1); if (Rational.Gcd(gn, gd) == 1) break; }
                    otherText = "[[" + S(ww) + " " + S(gn) + "/" + S(gd) + "]]"; gn = ww * gd + gn;
                }
                else
                {
                    for (;;) { gd = r.Int(2, 6); gn = r.Int(gd == 2 ? 3 : (int)gd + 1, (int)gd * 2 - 1); if (gn > gd && Rational.Gcd(gn, gd) == 1) break; }
                    otherText = Raw(gn, gd);
                }
            }
            else
            {
                gd = r.Int(2, 6); gn = gd; otherText = Raw(gn, gd);
            }
            Rational other = Rational.R(gn, gd), ans = Rational.Mul(fixedV, other);
            string[] why = new string[3];
            if (truth == 0) { why[1] = "ignored-the-size"; why[2] = "multiplying-makes-bigger"; }
            else if (truth == 2) { why[0] = "multiplying-makes-smaller"; why[1] = "ignored-the-size"; }
            else { why[0] = "multiplying-makes-smaller"; why[2] = "multiplying-makes-bigger"; }
            var choices = new List<Choice>();
            for (int i = 0; i < 3; i++) choices.Add(new Choice(CompareWords[i], i == truth, i == truth ? null : why[i]));
            string[] verdict = { "less than", "equal to", "greater than" };
            string otherSize = truth == 0 ? "less than 1" : truth == 2 ? "more than 1" : "exactly 1";
            var p = Make(
                "no multiplying allowed! how does " + fixedText + " " + Times + " " + otherText + " compare with " + fixedText + "?",
                "compare, no multiplying", Answer.Fraction(ans),
                JsonValue.Obj().Set("factors", JsonValue.Arr(FracJ(fixedV.N, fixedV.D), FracJ(gn, gd))).Set("truth", truth),
                new List<string>
                {
                    "look at the other factor, " + otherText + ". is it more than 1, less than 1, or exactly 1?",
                    "taking 1 of something keeps all of it. so when you take " + otherText + " of " + fixedText + ", do you keep more than all of it, less than all of it, or exactly all of it?",
                    otherText + " is " + otherSize + ", so " + fixedText + " " + Times + " " + otherText + " is " + verdict[truth] + " " + fixedText + ". (check: it comes to " + F(ans) + ".)"
                }, new List<Trap>(), choices);
            return p;
        }
    }
}
