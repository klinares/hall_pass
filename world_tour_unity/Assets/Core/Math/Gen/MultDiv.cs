using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WorldTour.Core.Maths.Gen
{
    /// <summary>
    /// TICKET SALES: multi-digit multiplication and division (5.NBT.5-6). Ported from
    /// world_tour/math/gen/multdiv.js; every RNG call happens in the same order, so problems match the JS.
    /// L0 (Unity only, warm-up): 4th-grade review. Single-digit facts, mostly single-digit facts, plus 2-digit x 1-digit with no regrouping or a multiple of ten,
    /// and 2-digit / 1-digit with no regrouping and no remainder (7.4).
    /// L1: 2-digit x 1-digit, 3-digit / 1-digit.
    /// L2: 2-digit x 2-digit, 3-digit / 2-digit.
    /// L3: 3-digit x 2-digit, 4-digit / 2-digit, and "how many full rows" with a remainder.
    /// </summary>
    public static class MultDiv
    {
        public const string Skill = "md";

        /// <summary>Posters ship in the box sizes a real order would have.</summary>
        public static readonly IReadOnlyList<int> BoxSizes = new[] { 12, 24, 25, 48, 50, 100, 144 };

        private static readonly string[] KindsL3 = { "mul", "div", "rows" };
        private static readonly string[] KindsL12 = { "mul", "div" };
        private static readonly string[] Tales = { "rows", "boxes", "other" };
        private static readonly string[] TalesNoBoxes = { "rows", "other" };

        // JS: f = n => n.toLocaleString('en-US'), for whole numbers
        private static string F(long n) => n.ToString("#,##0", CultureInfo.InvariantCulture);
        private static string S(long n) => n.ToString(CultureInfo.InvariantCulture);

        /// <summary>JS: <c>parts</c>: a number split into place-value parts, 347 is [300, 40, 7], zeros dropped.</summary>
        public static List<long> Parts(long n)
        {
            string s = S(n);
            var r = new List<long>();
            for (int i = 0; i < s.Length; i++)
            {
                long v = (s[i] - '0') * (long)Math.Pow(10, s.Length - 1 - i);
                if (v != 0) r.Add(v);
            }
            return r;
        }

        public static Problem Generate(int level, long seed) => Guard.Guarded(Build, level, seed);

        private static Problem Build(int level, long seed, Rng r)
        {
            string kind = r.Pick(level == 3 ? KindsL3 : KindsL12);
            long a = 0, b = 0;
            Answer answer;
            string prompt, label;
            List<string> steps;
            List<Trap> traps;
            JsonValue parameters;
            JsonValue work = JsonValue.Null;
            string times = " " + Glyph.Times + " ", minus = " " + Glyph.Minus + " ";

            if (kind == "mul")
            {
                // level 0 only: half the problems are single-digit facts (no box sizes: they start at 12)
                // (7.4: seven in ten at level 0, and the rest never regroup: see NoRegroup)
                bool fact = level == 0 && r.Chance(.7);
                string tale = r.Pick(fact || level == 0 ? TalesNoBoxes : Tales);
                List<int> small = BoxSizes.Where(n => n < 100).ToList();
                if (fact) { a = r.Int(2, 9); b = r.Int(3, 9); }
                else if (level == 0)
                {
                    // 7.4: a multiple of ten (40 x 6), or tens and ones that each multiply without carrying (23 x 3)
                    if (r.Chance(.4)) { a = 10 * r.Int(2, 9); b = r.Int(2, 9); }
                    else { b = r.Int(2, 4); a = 10 * r.Int(1, 9 / (int)b) + r.Int(1, 9 / (int)b); }
                }
                else if (tale == "boxes")
                {
                    // posters ship in real box sizes. level 3 needs 3-digit x 2-digit, so either a 144 box
                    // (x100 is too easy to count) or a 2-digit box size and a 3-digit number of boxes
                    if (level == 1) { a = r.Pick(small); b = r.Int(3, 9); }
                    else if (level == 2) { a = r.Pick(small); b = r.Int(12, 99); }
                    else
                    {
                        var sizes = new List<int>(small) { 144 };
                        int size = r.Pick(sizes);
                        if (size > 99) { a = size; b = r.Int(12, 99); }
                        else { a = r.Int(102, 999); b = size; }
                    }
                }
                else if (level == 1) { a = r.Int(12, 99); b = r.Int(3, 9); }
                else if (level == 2) { a = r.Int(12, 99); b = r.Int(12, 99); }
                else { a = r.Int(102, 999); b = r.Int(12, 99); }
                long p = a * b;
                answer = Answer.Int(p);
                long per = level == 3 && tale == "boxes" && a != 144 ? b : a;
                long count = per == a ? b : a;
                prompt = tale == "rows" ? S(a) + " fans fit in each row and there are " + S(b) + " rows. how many seats is that?"
                    : tale == "boxes" ? (count >= 100
                        ? "posters come " + S(per) + " to a box. the tour ships " + F(count) + " boxes over the whole tour. how many posters?"
                        : "each box holds " + S(per) + " posters. the crew unloads " + S(count) + " boxes. how many posters?")
                    : a >= 100 ? "a show sells " + S(a) + " tickets. the tour plays " + S(b) + " shows. how many tickets in all?"
                    : "each fan-club bus brings " + S(a) + " fans. " + S(b) + " buses pull in. how many fans is that?";
                label = F(a) + times + S(b);
                // break the smaller factor into tens and ones (or the 1-digit factor's partner, at level 1)
                // a fact (level 0) is split as one group short plus one more, the way its hints go
                List<long> split = fact ? new List<long> { b - 1, 1 } : level <= 1 ? Parts(a) : Parts(b);
                long other = fact ? a : level <= 1 ? b : a;
                List<long> pp = split.Select(s => s * other).ToList();
                work = JsonValue.Obj().Set("kind", "area").Set("rowParts", JsonValue.Ints(new[] { other })).Set("colParts", JsonValue.Ints(split));
                if (fact)
                {
                    steps = new List<string>
                    {
                        "think of " + S(b) + " groups of " + S(a) + ". start from a fact you know that is close.",
                        S(a) + times + S(b - 1) + " = " + S(a * (b - 1)) + ". one more " + S(a) + Glyph.Ellipsis,
                        S(a * (b - 1)) + " + " + S(a) + " = <b>" + S(p) + "</b>"
                    };
                }
                else if (split.Count > 1)
                {
                    steps = new List<string>
                    {
                        "break " + S(level <= 1 ? a : b) + " into " + string.Join(" + ", split.Select(F)) + ", and multiply each part by " + F(other) + ".",
                        string.Join(", ", split.Select((s, i) => F(other) + times + F(s) + " = " + F(pp[i]))) + ". now add those up.",
                        string.Join(" + ", pp.Select(F)) + " = <b>" + F(p) + "</b>"
                    };
                }
                else
                {
                    // a round number (70): one part only, so the partial step is "times 7, then ten times bigger"
                    long s = split[0], digit = s / 10;
                    steps = new List<string>
                    {
                        F(s) + " is " + S(digit) + " tens. multiply " + F(other) + " by " + S(digit) + " first, then by 10.",
                        F(other) + times + S(digit) + " = " + F(other * digit) + ". now make that ten times bigger.",
                        F(other * digit) + times + "10 = <b>" + F(p) + "</b>"
                    };
                }
                traps = fact ? new List<Trap> { new Trap(MathValue.Num(a * (b - 1)), "one-group-short") }
                    : new List<Trap> { new Trap(MathValue.Num(Money.NoCarryAdd(pp)), "no-carry") };
                if (level > 1) traps.Add(new Trap(MathValue.Num(a * (b % 10)), "partial-only"));
                if (level > 1) traps.Add(new Trap(MathValue.Num(a * (b / 10 + b % 10)), "no-placeholder"));
                traps.Add(new Trap(MathValue.Num(a + b), "added-instead"));
                parameters = JsonValue.Obj().Set("a", a).Set("b", b).Set("op", Glyph.Times);
            }
            else if (kind == "div")
            {
                int d, q;
                if (level == 0)
                {
                    // 7.4: facts to 9 x 9 (seven in ten), or a 2-digit number shared among 2 to 4 that splits tens and
                    // ones with no regrouping and no remainder (84 / 4, 69 / 3, 60 / 3)
                    if (r.Chance(.7)) { d = r.Int(2, 9); q = r.Int(2, 9); }
                    else { d = r.Int(2, 4); q = 10 * r.Int(1, 9 / d) + r.Int(0, 9 / d); }
                }
                else if (level == 1) { d = r.Int(2, 9); q = r.Int(CeilDiv(100, d), 999 / d); }
                else if (level == 2) { d = r.Int(11, 49); q = r.Int(Math.Max(2, CeilDiv(100, d)), 999 / d); }
                else { d = r.Int(11, 99); q = r.Int(CeilDiv(1000, d), Math.Min(999, 9999 / d)); }
                long N = (long)d * q;
                answer = Answer.Int(q);
                var stories = new[]
                {
                    F(N) + " tickets are split evenly into " + S(d) + " sections. how many tickets per section?",
                    "the crew packs " + F(N) + " wristbands into " + S(d) + " equal bags. how many in each bag?",
                    F(N) + " fans line up in " + S(d) + " equal lines at the gates. how many fans per line?"
                };
                prompt = r.Pick(stories);
                label = F(N) + " " + Glyph.Divide + " " + S(d);
                // partial quotients: first as many tens of the divisor as fit (189 is 18 tens = 180),
                // then the ones with what's left (9), keeping a running total
                long tens = q / 10 * 10, ones = q % 10;
                var chunks = new List<JsonValue>();
                int chunkCount = 0;
                long left = N;
                foreach (long c in new[] { tens, ones })
                {
                    if (c == 0) continue;
                    chunks.Add(JsonValue.Obj().Set("c", c).Set("take", c * d).Set("left", left - c * d));
                    left -= c * d;
                    chunkCount++;
                }
                // one chunk (q = 4, or q = 40): the first chunk IS the answer, so the partial step
                // stops short of it and asks whether one more fits
                long? lower = chunkCount > 1 ? (long?)null : q >= 20 ? q - 10 : q - 1;
                work = JsonValue.Obj().Set("kind", "chunks").Set("d", d).Set("N", N).Set("chunks", JsonValue.Arr(chunks));
                if (lower != null)
                {
                    long take = lower.Value * d;
                    work.Set("lower", JsonValue.Obj().Set("c", lower.Value).Set("take", take).Set("left", N - take));
                }
                long first = lower ?? tens, rest = q - first, after = N - first * d;
                Func<long, string> tensWord = n => F(n / 10) + " ten" + (n == 10 ? "" : "s");
                string ds = S(d);
                steps = new List<string>
                {
                    // Unity (QUESTIONS 8, intended differences from the JS): hint 1 never reaches the answer.
                    // q = 10: the JS printed "d x 10 = N", the dividend itself. q = 2 or 3: the JS "try a few" list
                    // ran up to d x q = N. The list now stops below the answer and hands the next try to her.
                    q == 10
                        ? "what happens to a number when you multiply it by 10? try it with " + ds + "."
                    : q >= 10
                        ? "how many tens of " + ds + " fit in " + F(N) + "? one ten of " + ds + " is " + ds + times + "10 = " + F(d * 10L) + "."
                    : q <= 3
                        ? "how many " + ds + "s fit in " + F(N) + "? " + ds + times + S(q - 1) + " = " + F(d * (q - 1L)) + ". is there room for another?"
                    // level 0 (Unity only): a short list of multiples can land on the quotient itself (2 x 3 = 6 for 12 / 2),
                    // so the nudge names no products
                    : level == 0
                        ? "how many " + ds + "s fit in " + F(N) + "? use the " + ds + "s facts you know to get close."
                        : "how many " + ds + "s fit in " + F(N) + "? try a few: " + ds + times + "2 = " + F(d * 2L) + ", " + ds + times + "3 = " + F(d * 3L) + Glyph.Ellipsis,
                    level == 0
                        ? ds + times + F(first) + " = " + F(first * d) + ". that leaves " + F(after) + ". how many " + ds + "s fit in " + F(after) + "?"
                    : q >= 10
                        ? (lower != null ? (first % 10 != 0 ? "start with " : "start with " + tensWord(first) + " of " + ds + ": ")
                                         : tensWord(first) + " of " + ds + " " + (first == 10 ? "fits" : "fit") + ": ") +
                          ds + times + F(first) + " = " + F(first * d) + ". " + F(N) + minus + F(first * d) + " = " + F(after) +
                          ". running total: " + F(first) + ". now how many " + ds + "s fit in " + F(after) + "?"
                        : ds + times + F(first) + " = " + F(first * d) + ", which leaves " + F(after) + ". running total: " + F(first) +
                          ". how many more " + ds + "s fit in that?",
                    ds + times + F(rest) + " = " + F(rest * d) + ", and " + F(after) + minus + F(rest * d) + " = 0. running total: " +
                        F(first) + " + " + F(rest) + " = <b>" + F(q) + "</b>."
                };
                traps = new List<Trap>
                {
                    new Trap(MathValue.Num(q * 10L), "place-value"),
                    new Trap(q % 10 == 0 ? MathValue.Num(q / 10) : MathValue.Null, "place-value"),
                    new Trap(MathValue.Num(N - d), "subtracted-instead")
                };
                parameters = JsonValue.Obj().Set("N", N).Set("d", d).Set("op", Glyph.Divide);
            }
            else
            {
                int d = r.Int(12, 48);
                int q = r.Int(CeilDiv(100, d) + 1, 999 / d);
                int rem = r.Int(1, d - 1);
                long N = (long)d * q + rem;
                answer = Answer.Int(q);
                string ds = S(d);
                prompt = F(N) + " fans are coming and each row has " + ds + " seats. how many rows will be completely full?";
                label = "full rows of " + ds;
                work = JsonValue.Obj().Set("kind", "remainder").Set("d", d).Set("N", N).Set("q", q).Set("rem", rem);
                steps = new List<string>
                {
                    "divide " + F(N) + " by " + ds + ". the leftover fans won" + Glyph.Apos + "t fill a whole row.",
                    ds + times + F(q) + " = " + F((long)d * q) + " and " + ds + times + F(q + 1) + " = " + F((long)d * (q + 1)) + ". which one fits under " + F(N) + "?",
                    F(N) + " " + Glyph.Divide + " " + ds + " = " + F(q) + " remainder " + S(rem) + ". the last " + S(rem) +
                        " fans sit in a row that isn" + Glyph.Apos + "t full, so <b>" + F(q) + "</b> full rows."
                };
                traps = new List<Trap>
                {
                    new Trap(MathValue.Num(q + 1), "rounded-up"),
                    new Trap(MathValue.Num(rem), "gave-remainder"),
                    new Trap(MathValue.Num(N - d), "subtracted-instead")
                };
                parameters = JsonValue.Obj().Set("N", N).Set("d", d).Set("op", Glyph.Divide);
            }

            return new Problem
            {
                Skill = Skill, Level = level, Kind = kind, Seed = seed,
                Params = parameters, Prompt = prompt, Label = label, Answer = answer,
                Simplest = false, Choices = null,
                Traps = Mistakes.CleanTraps(answer, traps), Steps = steps, Work = work
            };
        }

        // JS Math.ceil(a / b) for positive whole numbers
        private static int CeilDiv(int a, int b) => (a + b - 1) / b;
    }
}
