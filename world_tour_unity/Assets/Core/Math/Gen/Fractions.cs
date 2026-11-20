using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WorldTour.Core.Maths.Gen
{
    /// <summary>
    /// BEAT MAKER: adding and subtracting fractions (5.NF.1-2). Ported from world_tour/math/gen/fractions.js;
    /// every RNG call happens in the same order, so problems match the JS.
    /// A measure is one whole. Note blocks are unit fractions: 1/2 1/4 1/8 1/3 1/6 1/12.
    /// L1: fill one whole with halves, quarters, eighths.
    /// L2: unlike denominators from {2,3,4,6,8,12}: fill the measure, or add two notes.
    /// L3: mixed-number targets (two measures), taking notes out, or subtracting.
    /// </summary>
    public static class Fractions
    {
        public const string Skill = "frac";

        /// <summary>JS NAMES: denominator to note name.</summary>
        public static readonly IReadOnlyDictionary<long, string> Names = new Dictionary<long, string>
        {
            { 1, "whole" }, { 2, "half" }, { 3, "third" }, { 4, "quarter" }, { 6, "sixth" }, { 8, "eighth" }, { 12, "twelfth" }
        };

        /// <summary>JS: <c>plural</c>. A denominator with no name comes out "undefineds", as in the JS.</summary>
        public static string Plural(long d) => d == 2 ? "halves" : (Names.TryGetValue(d, out string n) ? n : "undefined") + "s";

        /// <summary>Pairs of unlike denominators whose common grid stays at 12 or under.</summary>
        private static readonly int[][] Pairs =
        {
            new[] { 2, 3 }, new[] { 3, 4 }, new[] { 4, 6 }, new[] { 2, 6 }, new[] { 3, 6 }, new[] { 2, 12 },
            new[] { 3, 12 }, new[] { 4, 12 }, new[] { 6, 12 }, new[] { 2, 8 }, new[] { 4, 8 }, new[] { 2, 4 }
        };

        private static readonly string[] KindsL2 = { "fill", "fill", "sum" };
        private static readonly string[] KindsL3 = { "mixed", "takeout", "diff" };
        private static readonly int[] L1Blocks = { 2, 4, 8 };
        private static readonly int[] MixedDenoms = { 2, 3, 4, 6 };

        private static string F(Rational f) => "[[" + Rational.Str(f) + "]]";
        private static Rational Unit(long d) => Rational.R(1, d);
        private static long GridOf(IEnumerable<int> ds) => ds.Aggregate(1L, (a, b) => Rational.Lcm(a, b));
        private static Rational SumUnits(IEnumerable<int> ds) => Rational.Sum(ds.Select(d => Unit(d)));
        private static string S(long v) => v.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// JS: <c>decompose(f, palette)</c>: split a fraction into palette blocks, biggest first. Throws when it
        /// can't (the palette always includes 1/grid, so the generators never hit that).
        /// </summary>
        public static List<int> Decompose(Rational f, IEnumerable<int> palette)
        {
            var output = new List<int>();
            Rational left = f;
            var sorted = palette.ToList();
            sorted.Sort();
            foreach (int d in sorted)
            {
                while (Rational.Cmp(left, Unit(d)) >= 0)
                {
                    output.Add(d);
                    left = Rational.Sub(left, Unit(d));
                }
            }
            if (left.N != 0) throw new InvalidOperationException("could not decompose " + Rational.Str(f));
            return output;
        }

        /// <summary>JS: <c>filled(params, placed, removed)</c>: written notes, minus removed ones, plus placed ones, add to the target.</summary>
        public static bool Filled(JsonValue parameters, IEnumerable<int> placed = null, IEnumerable<int> removed = null)
        {
            var rem = new HashSet<int>(removed ?? Enumerable.Empty<int>());
            var notes = new List<int>();
            List<long> pre = parameters["pre"].AsLongs();
            for (int i = 0; i < pre.Count; i++) if (!rem.Contains(i)) notes.Add((int)pre[i]);
            if (placed != null) notes.AddRange(placed);
            return Rational.Eq(SumUnits(notes), parameters["target"].AsRational());
        }

        // JS: inGrid = (f, g) => `[[${f.n * (g / f.d)}/${g}]]`: "3/12" written over the grid, not reduced
        private static string InGrid(Rational f, long g) => "[[" + JsNumber.Str(f.N * ((double)g / f.D)) + "/" + S(g) + "]]";

        // JS: [...new Set(list)]
        private static List<int> Unique(IEnumerable<int> list)
        {
            var r = new List<int>();
            foreach (int x in list) if (!r.Contains(x)) r.Add(x);
            return r;
        }

        public static Problem Generate(int level, long seed) => Guard.Guarded(Build, level, seed);

        private static Problem Build(int level, long seed, Rng r)
        {
            string kind = level == 1 ? "fill" : level == 2 ? r.Pick(KindsL2) : r.Pick(KindsL3);
            Rational target = Rational.R(1);
            var pre = new List<int>();
            List<int> palette = null;
            Answer answer;
            bool simplest = false;
            List<string> steps;
            string prompt, label;
            var traps = new List<Trap>();
            JsonValue parameters;

            if (kind == "fill" || kind == "mixed")
            {
                int[] ds;
                int t = 0; // 0 = JS null
                if (level == 1) { ds = L1Blocks; palette = new List<int>(L1Blocks); }
                else
                {
                    for (;;)
                    {
                        ds = r.Pick(Pairs);
                        if (kind == "mixed")
                        {
                            t = r.Pick(MixedDenoms);
                            if (GridOf(ds.Concat(new[] { t })) > 12) continue;
                        }
                        break;
                    }
                }
                if (kind == "mixed")
                {
                    int m;
                    do m = r.Int(1, t - 1); while (Rational.R(m, t).D != t);
                    target = Rational.Add(Rational.R(1), Rational.R(m, t));
                }
                long grid = GridOf(t != 0 ? ds.Concat(new[] { t }) : (IEnumerable<int>)ds);
                if (level > 1) palette = Unique(ds.Concat(new[] { (int)grid }));
                // written notes: draw from the pair's blocks until there's no room for more
                Rational room = Rational.Sub(target, Unit(grid));
                int want = level == 1 ? r.Int(1, 2) : kind == "mixed" ? r.Int(3, 5) : r.Int(2, 3);
                for (int i = 0; i < 12 && pre.Count < want; i++)
                {
                    int d = level == 1 ? r.Pick(L1Blocks) : ds[pre.Count % 2 == 0 ? 0 : 1];
                    if (Rational.Cmp(Rational.Add(SumUnits(pre), Unit(d)), room) <= 0) pre.Add(d);
                }
                if (pre.Count == 0) pre.Add(ds.Max());
                Rational have = SumUnits(pre), need = Rational.Sub(target, have);
                answer = Answer.Fraction(need);
                parameters = JsonValue.Obj()
                    .Set("target", target).Set("grid", grid).Set("bars", (long)Math.Ceiling((double)target.N / target.D))
                    .Set("pre", JsonValue.Ints(pre)).Set("palette", JsonValue.Ints(palette)).Set("removable", false);
                prompt = kind == "mixed"
                    ? "this beat runs " + F(target) + " measures. finish it so it ends right on the line."
                    : "finish the measure: add notes until it" + Glyph.Apos + "s exactly one whole.";
                label = "fill " + F(target) + (kind == "mixed" ? " measures" : " measure");
                List<int> fill = Decompose(need, palette);
                steps = new List<string>
                {
                    (target.D == 1 ? "the measure holds one whole" : "the beat is " + F(target) + " measures long") +
                        ". how much is already written? count it in " + Plural(grid) + ".",
                    "written so far: " + InGrid(have, grid) + ". you need " + InGrid(need, grid) + " more.",
                    (fill.Count > 1 ? string.Join(" + ", fill.Select(d => F(Unit(d)))) + " = " + F(need) : "one " + F(need) + " note") +
                        (InGrid(need, grid) != F(need) && fill.Count == 1 ? " (that" + Glyph.Apos + "s " + InGrid(need, grid) + ")" : "") +
                        ". that fills it to " + (target.D == 1 ? "one whole" : F(target)) + "."
                };
            }
            else if (kind == "takeout")
            {
                int[] ds = r.Pick(Pairs);
                long grid = GridOf(ds);
                palette = Unique(ds.Concat(new[] { (int)grid }));
                // a measure that's exactly one whole, plus one or two extra notes jammed in
                var keep = new List<int>();
                while (Rational.Cmp(SumUnits(keep), Rational.R(1)) < 0)
                {
                    int d = r.Pick(ds);
                    keep.Add(Rational.Cmp(Rational.Add(SumUnits(keep), Unit(d)), Rational.R(1)) <= 0 ? d : (int)grid);
                }
                List<int> extra;
                if (r.Chance(.5)) extra = new List<int> { r.Pick(ds) };
                else
                {
                    int e1 = r.Pick(ds);
                    int e2 = r.Pick(palette);
                    extra = new List<int> { e1, e2 };
                }
                pre = r.Shuffle(keep.Concat(extra).ToList());
                Rational over = SumUnits(extra);
                answer = Answer.Fraction(over);
                parameters = JsonValue.Obj()
                    .Set("target", target).Set("grid", grid).Set("bars", 1)
                    .Set("pre", JsonValue.Ints(pre)).Set("palette", JsonValue.Ints(palette)).Set("removable", true);
                prompt = "the drummer jammed in too many notes. take notes out until the measure is exactly one whole.";
                label = "trim to 1 measure";
                Rational total = SumUnits(pre);
                Rational one = Rational.R(1);
                steps = new List<string>
                {
                    "add up what" + Glyph.Apos + "s written, in " + Plural(grid) + ". how far past " + F(one) + " does it go?",
                    "it adds to " + InGrid(total, grid) + ". one whole is " + InGrid(one, grid) + ", so take out " + InGrid(over, grid) + ".",
                    extra.Count > 1
                        ? "take out " + string.Join(" and ", extra.Select(d => F(Unit(d)))) + ". that" + Glyph.Apos + "s " + F(over) + ", which leaves exactly " + F(one) + "."
                        : "take out the " + F(over) + " note. that leaves exactly " + F(one) + "."
                };
            }
            else
            {
                // typed: a/b + c/d (level 2) or a/b - c/d (level 3)
                int a, b, x, y;
                for (;;)
                {
                    List<int> ab = r.Shuffle(r.Pick(Pairs));
                    a = ab[0];
                    b = ab[1];
                    x = r.Int(1, a - 1 != 0 ? a - 1 : 1);
                    y = r.Int(1, b - 1 != 0 ? b - 1 : 1);
                    if (a == 1 || b == 1) continue;
                    if (Rational.R(x, a).D != a || Rational.R(y, b).D != b) continue; // keep the written fractions in simplest form
                    if (kind == "diff" && Rational.Cmp(Rational.R(x, a), Rational.R(y, b)) <= 0) continue;
                    break;
                }
                long g = Rational.Lcm(a, b);
                Rational A = Rational.R(x, a), B = Rational.R(y, b);
                Rational res = kind == "sum" ? Rational.Add(A, B) : Rational.Sub(A, B);
                palette = Unique(new[] { a, b, (int)g });
                answer = Answer.Fraction(res);
                if (kind == "diff") simplest = true;
                string op = kind == "sum" ? "+" : Glyph.Minus;
                parameters = JsonValue.Obj()
                    .Set("target", JsonValue.Null).Set("grid", g)
                    .Set("bars", kind == "sum" ? Math.Max(1L, (long)Math.Ceiling((double)res.N / res.D)) : 1L)
                    .Set("a", A).Set("b", B).Set("op", op)
                    .Set("palette", JsonValue.Ints(palette)).Set("pre", JsonValue.Arr()).Set("removable", false);
                prompt = kind == "sum"
                    ? "the bass plays " + F(A) + " of a measure, then the drums play " + F(B) + ". how much is that together?"
                    : "a riff is " + F(A) + " of a measure long. cut " + F(B) + " off the end. how long is it now? simplest form.";
                label = F(A) + " " + op + " " + F(B);
                long A2 = x * (g / a), B2 = y * (g / b), raw = kind == "sum" ? A2 + B2 : A2 - B2;
                steps = new List<string>
                {
                    "the denominators don" + Glyph.Apos + "t match yet. what can " + S(a) + " and " + S(b) + " both turn into?",
                    "rewrite both in " + Plural(g) + ": " + InGrid(A, g) + " " + op + " " + InGrid(B, g) + ".",
                    InGrid(A, g) + " " + op + " " + InGrid(B, g) + " = [[" + S(raw) + "/" + S(g) + "]]" + (Rational.R(raw, g).D != g ? " = " + F(res) : "")
                };
                MathValue first = kind == "sum" ? MathValue.Frac(Rational.R(x + y, a + b))
                    : (a != b && x > y && a > b ? MathValue.Frac(Rational.R(x - y, a - b)) : MathValue.Null);
                MathValue second = kind == "sum" ? MathValue.Frac(Rational.R(x + y, g))
                    : (x > y ? MathValue.Frac(Rational.R(x - y, g)) : MathValue.Null);
                traps.Add(new Trap(first, kind == "sum" ? "added-denominators" : "subtracted-denominators"));
                traps.Add(new Trap(second, "tops-not-rewritten"));
            }

            return new Problem
            {
                Skill = Skill, Level = level, Kind = kind, Seed = seed,
                Params = parameters, Prompt = prompt, Label = label, Answer = answer,
                Simplest = simplest, Choices = null,
                Traps = Mistakes.CleanTraps(answer, traps), Steps = steps,
                // builder modes win when the measure adds to the target; typed modes use the answer
                Builder = kind != "sum" && kind != "diff"
            };
        }
    }
}
