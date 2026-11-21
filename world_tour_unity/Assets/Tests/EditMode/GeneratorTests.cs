using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Core.Tests
{
    /// <summary>
    /// Ported from world_tour/tests/math/generators.test.mjs (all seven generators): 3,000 fixed seeds per
    /// level, checked against an independent calculation, the generator contract, and the grade-level bounds in
    /// StreamingAssets/math_config.json. On top of the JS checks: no negative money or fraction anywhere the
    /// player sees it (prompt, label, steps, answer, traps, choices, and the numbers in params and work).
    /// </summary>
    public class GeneratorTests
    {
        private static GradeBounds _bounds;

        /// <summary>
        /// The grade bounds to test against: for each one, the stricter of the JS constants in
        /// world_tour/math/config.js (pinned here, so a looser deployed config can't loosen the test; the
        /// generators don't read BOUNDS) and StreamingAssets/math_config.json.
        /// </summary>
        private static GradeBounds Bounds()
        {
            if (_bounds != null) return _bounds;
            string path = Path.Combine(Application.dataPath, "StreamingAssets", "math_config.json");
            Assert.IsTrue(File.Exists(path), "missing " + path);
            var c = JsonUtility.FromJson<MathConfig>(File.ReadAllText(path));
            Assert.IsNotNull(c, "math_config.json did not parse");
            CollectionAssert.IsEmpty(c.Validate(), "math_config.json is not valid");
            GradeBounds js = MathConfig.WebDefaults().BOUNDS;
            Assert.AreEqual(10000, js.moneyCents, "JS BOUNDS.moneyCents");
            Assert.AreEqual(12, js.maxDenominator, "JS BOUNDS.maxDenominator");
            Assert.AreEqual(10, js.coordMax, "JS BOUNDS.coordMax");
            Assert.AreEqual(1000, js.maxVolume, "JS BOUNDS.maxVolume");
            Assert.AreEqual(150, js.storyDollars, "BOUNDS.storyDollars (Unity only, QUESTIONS 9.4)");
            Assert.AreEqual(150, OrderOps.MaxStoryDollars, "the generator keeps the same bound");
            _bounds = new GradeBounds
            {
                moneyCents = Math.Min(js.moneyCents, c.BOUNDS.moneyCents),
                maxDenominator = Math.Min(js.maxDenominator, c.BOUNDS.maxDenominator),
                coordMax = Math.Min(js.coordMax, c.BOUNDS.coordMax),
                maxVolume = Math.Min(js.maxVolume, c.BOUNDS.maxVolume),
                storyDollars = Math.Min(js.storyDollars, c.BOUNDS.storyDollars)
            };
            return _bounds;
        }

        private static IEnumerable<TestCaseData> Cases()
        {
            foreach (string skill in Generators.Skills)
                for (int level = 0; level <= 3; level++)
                    yield return new TestCaseData(skill, level).SetName("Volume " + skill + " level " + level);
        }

        // JS: each(mod, skill, fn)
        [Test, TestCaseSource(nameof(Cases))]
        public void VolumeRun(string skill, int level)
        {
            GradeBounds bounds = Bounds();
            Func<int, long, Problem> gen = (l, s) => Generators.Generate(skill, l, s);
            var kinds = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var sw = Stopwatch.StartNew();
            GeneratorContract.Repeatable(gen, level, 12345);
            int n = 0;
            foreach (long s in GeneratorContract.Seeds(level))
            {
                Problem p = gen(level, s);
                GeneratorContract.Contract(p, skill, level);
                kinds[p.Kind] = kinds.TryGetValue(p.Kind, out int k) ? k + 1 : 1;
                switch (skill)
                {
                    case "dec": CheckDec(p, level, bounds); break;
                    case "md": CheckMd(p, level); break;
                    case "frac": CheckFrac(p, level, bounds); break;
                    case "xy": CheckXy(p, level, bounds); break;
                    case "vol": CheckVol(p, level, bounds); break;
                    case "ops": CheckOps(p, level, bounds); break;
                    case "flu": CheckFlu(p, level); break;
                    default: Assert.Fail("no checks for " + skill); break;
                }
                NothingNegativeShown(p, bounds);
                n++;
            }
            Assert.AreEqual(GeneratorContract.Runs, n);
            Assert.IsTrue(kinds.Count >= 1);
            TestContext.Out.WriteLine(skill + " level " + level + ": " + n + " problems, kinds " +
                string.Join(", ", kinds.Select(kv => kv.Key + " " + kv.Value)) + ", " + sw.ElapsedMilliseconds + " ms");
        }

        // ---------- dec ----------

        private static void CheckDec(Problem p, int level, GradeBounds bounds)
        {
            string at = " [dec L" + level + " seed " + p.Seed + "]";
            IReadOnlyList<JsonValue> lines = p.Params["lines"].Items;
            long paid = p.Params["paid"].Long, total = p.Params["total"].Long;
            // independent total: quantities x prices, bulk = tenths x cents / 10
            long t = 0;
            foreach (JsonValue l in lines)
            {
                if (!l["unit"].IsNull)
                {
                    Assert.AreEqual(0, l["amount"].Long * l["each"].Long % 10, "bulk cost lands on whole cents" + at);
                    t += l["amount"].Long * l["each"].Long / 10;
                }
                else t += l["qty"].Long * l["each"].Long;
            }
            Assert.AreEqual(t, total, "total" + at);
            long want = p.Kind == "change" ? paid - total : total;
            Assert.AreEqual(AnswerType.Money, p.Answer.Type, "money answer" + at);
            Assert.AreEqual((double)want, p.Answer.Value.Number, "answer" + at);
            Assert.IsTrue(want > 0 && want < bounds.moneyCents, "money under the moneyCents bound: " + want + at);
            if (p.Kind == "change") Assert.AreEqual(new long[] { 1000, 1000, 2000, 5000 }[level], paid, "paid" + at);
            if (level == 0)
            {
                // warm-up: two items, every price a whole dollar or 50 cents, so the sum is too
                Assert.IsTrue(lines.All(l => l["each"].Long % 50 == 0 && l["qty"].Long == 1 && l["unit"].IsNull), "whole dollars and 50 cents" + at);
                Assert.IsTrue(total % 50 == 0, "the total is whole dollars or 50 cents" + at);
            }
            if (level <= 1) { Assert.AreEqual(2, lines.Count, at); Assert.IsTrue(total < 1000, at); }
            if (level == 2) { Assert.AreEqual(3, lines.Count, at); Assert.IsTrue(lines.Any(l => l["qty"].Long > 1), at); Assert.IsTrue(total < 2000, at); }
            if (level == 3)
            {
                Assert.IsFalse(lines[0]["unit"].IsNull, at);
                Assert.AreNotEqual(0, lines[0]["amount"].Long % 10, "a real decimal amount" + at);
                Assert.IsTrue(total < 5000, at);
            }
            // every price the player reads is under the bound too
            foreach (JsonValue l in lines) Assert.IsTrue(l["each"].Long < bounds.moneyCents && l["cost"].Long < bounds.moneyCents, "line price under the bound" + at);
            foreach (Match m in DollarText.Matches(GeneratorContract.Text(p)))
                Assert.IsTrue(long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * 100 + long.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) < bounds.moneyCents,
                    "money in the text under the moneyCents bound: " + m.Value + at);
        }

        private static readonly Regex DollarText = new Regex(@"\$([0-9]+)\.([0-9]{2})", RegexOptions.CultureInvariant);

        // ---------- md ----------

        private static readonly Regex PosterPer = new Regex(@"([0-9]+) posters|posters come ([0-9]+)", RegexOptions.CultureInvariant);
        private static int Digits(long n) => n.ToString(CultureInfo.InvariantCulture).Length;

        private static void CheckMd(Problem p, int level)
        {
            string at = " [md L" + level + " seed " + p.Seed + "]";
            long answer = (long)p.Answer.Value.Number;
            if (p.Kind == "mul")
            {
                long a = p.Params["a"].Long, b = p.Params["b"].Long;
                Assert.AreEqual(a * b, answer, "product" + at);
                if (level == 0)
                    Assert.IsTrue(a >= 2 && b >= 2 && (a <= 9 && b <= 9 || a >= 12 && a <= 50 && b <= 5), "a fact, or 2-digit x 1-digit with a factor to 5: " + a + " x " + b + at);
                else
                {
                    int[] want = new[] { new[] { 2, 1 }, new[] { 2, 2 }, new[] { 3, 2 } }[level - 1];
                    Assert.AreEqual(want[0], Digits(a), "digits of a" + at);
                    Assert.AreEqual(want[1], Digits(b), "digits of b" + at);
                }
                if (p.Prompt.Contains("poster"))
                {
                    Match m = PosterPer.Match(p.Prompt);
                    Assert.IsTrue(m.Success, at);
                    int per = int.Parse(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value, CultureInfo.InvariantCulture);
                    Assert.IsTrue(MultDiv.BoxSizes.Contains(per), "a real box size: " + per + at);
                }
            }
            else if (p.Kind == "div")
            {
                long N = p.Params["N"].Long, d = p.Params["d"].Long;
                // partial quotients: the chunks add up to the answer, use up N, and the hints keep a running total
                IReadOnlyList<JsonValue> chunks = p.Work["chunks"].Items;
                Assert.AreEqual(answer, chunks.Sum(c => c["c"].Long), "chunks add up" + at);
                Assert.AreEqual(0, chunks[chunks.Count - 1]["left"].Long, "chunks use up N" + at);
                Assert.IsTrue(chunks.Count == 1 || (chunks[0]["c"].Long % 10 == 0 && chunks[1]["c"].Long < 10), "tens first, then ones" + at);
                Assert.IsTrue(p.Steps[1].Contains("running total") && p.Steps[2].Contains("running total"), at);
                Assert.IsFalse(string.Join(" ", p.Steps).Contains("start big"), at);
                // quotient 10: hint 1 asks what x 10 does instead (QUESTIONS 8.2, an intended difference from the JS)
                if (answer > 10) Assert.IsTrue(p.Steps[0].StartsWith("how many tens of", StringComparison.Ordinal), at);
                Assert.AreEqual(0, N % d, "exact division" + at);
                Assert.AreEqual(N / d, answer, at);
                if (level == 0)
                    Assert.IsTrue(d >= 2 && answer >= 2 && (d <= 9 && answer <= 9 || d <= 5 && Digits(N) == 2 && answer >= 10), "a division fact, or a 2-digit number shared among up to 5: " + N + " / " + d + at);
                else
                {
                    int[] want = new[] { new[] { 3, 1 }, new[] { 3, 2 }, new[] { 4, 2 } }[level - 1];
                    Assert.AreEqual(want[0], Digits(N), "digits of N" + at);
                    Assert.AreEqual(want[1], Digits(d), "digits of d" + at);
                }
            }
            else
            {
                long N = p.Params["N"].Long, d = p.Params["d"].Long;
                Assert.AreEqual(3, level, at);
                Assert.AreNotEqual(0, N % d, "a remainder problem has a remainder" + at);
                Assert.AreEqual((long)Math.Floor((double)N / d), answer, at);
                Assert.AreEqual(2, Digits(d), at);
            }
            Assert.IsTrue(answer > 0, at);
        }

        // ---------- frac ----------

        private static readonly long[] AllowedDenoms = { 2, 3, 4, 6, 8, 12 };
        private static bool Close(double a, double b) => Math.Abs(a - b) < 1e-9 * Math.Max(1, Math.Abs(b));

        private static void CheckFrac(Problem p, int level, GradeBounds bounds)
        {
            string at = " [frac L" + level + " seed " + p.Seed + "]";
            JsonValue pr = p.Params;
            List<long> palette = pr["palette"].AsLongs(), pre = pr["pre"].AsLongs();
            long grid = pr["grid"].Long;
            var ds = palette.Concat(pre).Concat(new[] { grid }).ToList();
            Assert.IsTrue(ds.All(x => AllowedDenoms.Contains(x)), "denominators from the allowed set, at most 12" + at);
            Assert.IsTrue(ds.All(x => x <= bounds.maxDenominator), "denominators within maxDenominator" + at);
            Assert.IsTrue(palette.All(x => grid % x == 0) && pre.All(x => grid % x == 0), "every block sits on the grid" + at);
            Rational ans = Answers.ValueOf(p.Answer);
            Assert.IsTrue(ans.D <= bounds.maxDenominator, "answer denominator within maxDenominator" + at);
            double v = Rational.Num(ans);
            Assert.IsTrue(v > 0, "something to add or take away" + at);
            if (p.Builder == true)
            {
                double written = pre.Sum(x => 1.0 / x);
                Rational targetR = pr["target"].AsRational();
                double target = (double)targetR.N / targetR.D;
                Assert.IsTrue(Close(v, p.Kind == "takeout" ? written - target : target - written), "builder answer matches the measure" + at);
                // the worked hint's blocks really fill it
                List<int> fill = Fractions.Decompose(ans, palette.Select(x => (int)x));
                if (p.Kind == "takeout") Assert.IsTrue(Close(fill.Sum(x => 1.0 / x), v), at);
                else Assert.IsTrue(Fractions.Filled(pr, fill), "the decomposed blocks fill the measure exactly" + at);
                if (level == 1)
                {
                    Assert.IsTrue(palette.All(x => x == 2 || x == 4 || x == 8), at);
                    Assert.AreEqual(1.0, target, at);
                }
                if (level == 0)
                {
                    Assert.AreEqual(1, palette.Count, "one size of note" + at);
                    Assert.IsTrue(palette[0] == 3 || palette[0] == 4 || palette[0] == 6 || palette[0] == 8, "like denominators from 3, 4, 6, 8" + at);
                    Assert.IsTrue(pre.All(x => x == palette[0]), "every written note is that size" + at);
                    Assert.AreEqual(1.0, target, at);
                }
                if (p.Kind == "mixed") { Assert.IsTrue(target > 1 && target < 2, at); Assert.AreEqual(2, pr["bars"].Long, at); }
            }
            else
            {
                Assert.AreEqual(false, p.Builder, "typed problems say builder: false" + at);
                Rational a = pr["a"].AsRational(), b = pr["b"].AsRational();
                double A = Rational.Num(a), B = Rational.Num(b);
                Assert.IsTrue(Close(v, pr["op"].String == "+" ? A + B : A - B), "typed answer matches" + at);
                if (level == 0) { Assert.AreEqual(a.D, b.D, "like denominators" + at); Assert.AreEqual(p.Params["grid"].Long, a.D, "written over that denominator" + at); Assert.IsTrue(v < 1, "less than one whole" + at); }
                else Assert.AreNotEqual(a.D, b.D, "unlike denominators" + at);
                Assert.IsTrue(a.D <= bounds.maxDenominator && b.D <= bounds.maxDenominator, at);
                if (p.Kind == "diff") Assert.AreEqual(true, p.Simplest, at);
            }
        }

        // ---------- xy ----------

        private static void CheckXy(Problem p, int level, GradeBounds bounds)
        {
            string at = " [xy L" + level + " seed " + p.Seed + "]";
            Assert.AreEqual(AnswerType.Point, p.Answer.Type, at);
            double x = p.Answer.Value.X, y = p.Answer.Value.Y;
            long max = p.Params["max"].Long;
            Assert.AreEqual(level == 0 ? 4 : level == 1 ? 6 : 10, max, "axes" + at);
            Assert.IsTrue(max <= bounds.coordMax, "axes within coordMax" + at);
            Assert.IsTrue(x >= 0 && y >= 0 && x <= max && y <= max, "on the grid" + at);
            if (p.Kind == "move")
            {
                JsonValue start = p.Params["start"];
                long sx = start["x"].Long, sy = start["y"].Long, dx = p.Params["dx"].Long, dy = p.Params["dy"].Long;
                Assert.AreEqual((double)(sx + dx), x, at);
                Assert.AreEqual((double)(sy + dy), y, at);
                Assert.IsTrue(sx >= 0 && sy >= 0 && sx <= max && sy <= max, "start on the grid" + at);
                Assert.IsTrue(dx != 0 && dy != 0, at);
                // the prompt states the moves as distances with a direction, never as negative numbers
                StringAssert.Contains(Math.Abs(dx) + " " + (dx > 0 ? "right" : "left"), p.Prompt, at);
                StringAssert.Contains(Math.Abs(dy) + " " + (dy > 0 ? "up" : "down"), p.Prompt, at);
            }
            foreach (Trap t in p.Traps)
                Assert.IsTrue(t.Value.X <= max && t.Value.Y <= max && t.Value.X >= 0 && t.Value.Y >= 0, "trap on the grid" + at);
        }

        // ---------- vol ----------

        private static void CheckVol(Problem p, int level, GradeBounds bounds)
        {
            string at = " [vol L" + level + " seed " + p.Seed + "]";
            IReadOnlyList<JsonValue> bx = p.Params["boxes"].Items;
            long V = bx.Sum(b => b["l"].Long * b["w"].Long * b["h"].Long);
            long answer = (long)p.Answer.Value.Number;
            Assert.IsTrue(V <= 1000, "volume at most 1,000" + at);
            Assert.IsTrue(V <= bounds.maxVolume, "volume within maxVolume" + at);
            if (p.Kind == "build") { Assert.AreEqual(bx[0]["h"].Long, answer, at); Assert.AreEqual(V, p.Params["target"].Long, at); }
            else if (p.Kind == "missing") Assert.AreEqual(V, answer * bx[0]["l"].Long * bx[0]["w"].Long, at);
            else Assert.AreEqual(V, answer, at);
            if (level == 1) Assert.IsTrue(V <= 36, "level 1 stacks are small enough to count" + at);
            if (level == 0)
            {
                Assert.AreEqual("count", p.Kind, at);
                Assert.AreEqual(1, bx.Count, at);
                Assert.AreEqual(1, bx[0]["h"].Long, "a single visible layer" + at);
                Assert.IsTrue(bx[0]["l"].Long >= 2 && bx[0]["l"].Long <= 5 && bx[0]["w"].Long >= 2 && bx[0]["w"].Long <= 4, "2 to 5 across and 2 to 4 deep" + at);
            }
            if (p.Kind == "two") Assert.IsTrue(bx[1]["l"].Long <= bx[0]["l"].Long && bx[1]["w"].Long <= bx[0]["w"].Long, "the riser fits on top" + at);
            Assert.AreEqual(p.Kind == "build", p.Builder == true, "builder only for build" + at);
        }

        // ---------- ops ----------

        private static readonly Regex OpSigns = new Regex("[+" + Regex.Escape(Glyph.Minus) + Regex.Escape(Glyph.Times) + Regex.Escape(Glyph.Divide) + "]", RegexOptions.CultureInvariant);

        private static readonly Regex StoryDollarText = new Regex(@"\$([0-9]+)", RegexOptions.CultureInvariant);

        private static void CheckOps(Problem p, int level, GradeBounds bounds)
        {
            string at = " [ops L" + level + " seed " + p.Seed + "]";
            string expr = p.Params["expr"].String;
            double v = ExprEval.Eval(expr);
            Assert.AreEqual(v, p.Answer.Value.Number, "answer" + at);
            Assert.IsTrue(MathValue.IsInteger(v) && v > 0 && v <= 300, at);
            Assert.IsNotNull(OrderOps.Solve(expr), "every step stays whole and non-negative" + at);
            int nops = OpSigns.Matches(expr).Count;
            if (level == 0) { Assert.AreEqual(2, nops, at); Assert.IsFalse(expr.Contains("("), "no parentheses at level 0" + at); }
            if (level == 1) { Assert.AreEqual(2, nops, at); Assert.IsTrue(expr.Contains("("), at); }
            if (level == 2) Assert.AreEqual(3, nops, at);
            if (level == 1 && expr.Contains("(")) Assert.AreNotEqual(ExprEval.Eval(expr.Replace("(", "").Replace(")", "")), v, "the parentheses matter" + at);
            // money in tour budget is whole dollars in a story, bounded by storyDollars, not moneyCents (QUESTIONS 9.4)
            foreach (Match m in StoryDollarText.Matches(p.Prompt + " " + string.Join(" ", p.Steps)))
                Assert.IsTrue(long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) <= bounds.storyDollars, "story money within storyDollars: " + m.Value + at);
            if (level == 3)
            {
                Choice right = p.Choices.Single(c => c.Correct);
                Assert.IsTrue(right.IsText, at);
                Assert.AreEqual(expr, right.Text, at);
                foreach (Choice c in p.Choices.Where(c => !c.Correct))
                {
                    Assert.IsTrue(c.IsText, at);
                    Assert.AreNotEqual(v, ExprEval.Eval(c.Text), "a wrong expression gives a different total" + at);
                }
            }
            else Assert.IsNull(p.Choices, at);
        }

        // ---------- flu ----------

        private static readonly Regex FluL1Prompt = new Regex("\\A[0-9]+ [" + Regex.Escape(Glyph.Times) + Regex.Escape(Glyph.Divide) + "] [0-9]+ = \\?\\z", RegexOptions.CultureInvariant);
        private static readonly Regex FluSplit = new Regex(" [" + Regex.Escape(Glyph.Times) + Regex.Escape(Glyph.Divide) + "] ", RegexOptions.CultureInvariant);
        // superscripts 1 to 4, from their code points
        private static readonly string[] Sups = { ((char)0x00B9).ToString(), ((char)0x00B2).ToString(), ((char)0x00B3).ToString(), ((char)0x2074).ToString() };

        private static void CheckFlu(Problem p, int level)
        {
            string at = " [flu L" + level + " seed " + p.Seed + "]";
            string expr = p.Prompt.Replace(" = ?", "").Replace(",", "");
            for (int k = 1; k <= 4; k++)
                expr = expr.Replace("10" + Sups[k - 1], "(" + Math.Pow(10, k).ToString(CultureInfo.InvariantCulture) + ")");
            double v = ExprEval.Eval(expr);
            Assert.IsTrue(Close(Rational.Num(Answers.ValueOf(p.Answer)), v), p.Prompt + " -> " + v + at);
            Assert.AreEqual(4, p.Choices.Count, "four choices" + at);
            if (level <= 1) Assert.IsTrue(FluL1Prompt.IsMatch(p.Prompt), at);
            if (level < 3) Assert.IsTrue(MathValue.IsInteger(v), "whole numbers below level 3" + at);
            if (p.Kind == "mul" || p.Kind == "div")
            {
                string[] ab = FluSplit.Split(p.Label);
                double a = double.Parse(ab[0], CultureInfo.InvariantCulture), b = double.Parse(ab[1], CultureInfo.InvariantCulture);
                int top = level == 0 ? 5 : level == 1 ? 10 : 12;
                if (p.Kind == "mul") Assert.IsTrue(a <= top && b <= top, at);
                else Assert.IsTrue(b <= top && v <= top, at);
            }
            // the traps are exactly the wrong choices, in order (JS: choices.filter(c => !c.correct))
            Assert.AreEqual(string.Join("|", p.Choices.Where(c => !c.Correct).Select(c => c.Value + ":" + c.Why)),
                string.Join("|", p.Traps.Select(t => t.Value + ":" + t.Why)), at);
        }

        // ---------- nothing negative where the player sees it ----------

        private static readonly Regex NegDollars = new Regex(@"\$-|\$[0-9]+\.-", RegexOptions.CultureInvariant);
        private static readonly Regex HyphenDigit = new Regex(@"-[0-9]", RegexOptions.CultureInvariant);
        private static readonly Regex MinusDigit = new Regex(Regex.Escape(Glyph.Minus) + "[0-9]", RegexOptions.CultureInvariant);
        private static readonly Regex MarkupContent = new Regex(@"\[\[([^\]]*)\]\]", RegexOptions.CultureInvariant);
        private static readonly Regex FracText = new Regex(@"\A(?:[0-9]+|([0-9]+ )?[0-9]+/([0-9]+))\z", RegexOptions.CultureInvariant);

        // Typed and builder skills (dec, md, frac, xy, vol): trap values are not in this list. The web game only
        // matches them against what the child typed and then shows a canned line for trap.why
        // (modes/math/parts.js), so a trap like the added-denominators 13/15 is never displayed. They are still
        // checked below to be non-negative. Multiple-choice skills (flu, ops) do display their distractors
        // (soundcheck.js, budget.js), and flu builds its traps from its choices, so for those two the traps are
        // scanned as shown text too.
        private static IEnumerable<string> Shown(Problem p)
        {
            yield return p.Prompt;
            yield return p.Label;
            foreach (string s in p.Steps) yield return s;
            yield return Answers.Show(p.Answer);
            if (p.Choices != null) foreach (Choice c in p.Choices) yield return c.IsText ? c.Text : Answers.Show(new Answer(p.Answer.Type, c.Value));
            if (p.Skill == "flu" || p.Skill == "ops")
                foreach (Trap t in p.Traps) yield return Answers.Show(new Answer(p.Answer.Type, t.Value));
        }

        private static void NothingNegativeShown(Problem p, GradeBounds bounds)
        {
            string at = " [" + p.Skill + " L" + p.Level + " seed " + p.Seed + "]";
            foreach (string s in Shown(p))
            {
                Assert.IsFalse(NegDollars.IsMatch(s), "negative money shown: " + s + at);
                Assert.IsFalse(HyphenDigit.IsMatch(s), "negative number shown: " + s + at);
                Assert.IsFalse(MinusDigit.IsMatch(s), "negative number shown (minus sign): " + s + at);
                foreach (Match m in MarkupContent.Matches(s))
                {
                    Match f = FracText.Match(m.Groups[1].Value);
                    Assert.IsTrue(f.Success, "fraction markup is a plain non-negative number: " + m.Value + at);
                    if (f.Groups[2].Success)
                        Assert.IsTrue(long.Parse(f.Groups[2].Value, CultureInfo.InvariantCulture) <= bounds.maxDenominator, "shown denominator within maxDenominator: " + m.Value + at);
                }
            }
            AssertValueNotNegative(p.Answer.Value, "answer" + at);
            foreach (Trap t in p.Traps) AssertValueNotNegative(t.Value, "trap " + t.Why + at);
            if (p.Choices != null) foreach (Choice c in p.Choices) if (!c.IsText) AssertValueNotNegative(c.Value, "choice" + at);
            // xy params.dx and dy are signed moves. The player only reads them as "3 left" (checked in CheckXy),
            // and stage.js never displays them, so they are left out here.
            foreach (string key in p.Params.Keys)
                if (!(p.Skill == "xy" && (key == "dx" || key == "dy"))) AssertTreeNotNegative(p.Params[key], "params." + key + at);
            if (p.Work != null) AssertTreeNotNegative(p.Work, "work" + at);
        }

        private static void AssertValueNotNegative(MathValue v, string what)
        {
            switch (v.Kind)
            {
                case ValueKind.Number: Assert.IsTrue(v.Number >= 0, what + " is negative: " + v); break;
                case ValueKind.Fraction: Assert.IsTrue(v.N >= 0 && v.D > 0, what + " is negative: " + v); break;
                case ValueKind.Point: Assert.IsTrue(v.X >= 0 && v.Y >= 0, what + " is negative: " + v); break;
            }
        }

        private static void AssertTreeNotNegative(JsonValue v, string what)
        {
            switch (v.Kind)
            {
                case JsonKind.Number: Assert.IsTrue(v.Number >= 0, what + " holds a negative number: " + v); break;
                case JsonKind.Array: foreach (JsonValue x in v.Items) AssertTreeNotNegative(x, what); break;
                case JsonKind.Object: foreach (string key in v.Keys) AssertTreeNotNegative(v[key], what + "." + key); break;
                case JsonKind.String:
                    Assert.IsFalse(HyphenDigit.IsMatch(v.String) || MinusDigit.IsMatch(v.String) || NegDollars.IsMatch(v.String), what + " holds negative text: " + v);
                    break;
            }
        }

        // ---------- small units ----------

        [Test]
        public void GenerateRoutesBySkillAndRefusesUnknownSkills()
        {
            CollectionAssert.AreEqual(new[] { "dec", "md", "frac", "xy", "vol", "ops", "flu" }, Generators.Skills);
            Assert.AreEqual(Decimals.Generate(2, 77).ToCanonicalJson(), Generators.Generate("dec", 2, 77).ToCanonicalJson());
            Assert.AreEqual(MultDiv.Generate(3, 77).ToCanonicalJson(), Generators.Generate("md", 3, 77).ToCanonicalJson());
            Assert.AreEqual(Fractions.Generate(1, 77).ToCanonicalJson(), Generators.Generate("frac", 1, 77).ToCanonicalJson());
            Assert.AreEqual(Coords.Generate(3, 77).ToCanonicalJson(), Generators.Generate("xy", 3, 77).ToCanonicalJson());
            Assert.AreEqual(Volume.Generate(2, 77).ToCanonicalJson(), Generators.Generate("vol", 2, 77).ToCanonicalJson());
            Assert.AreEqual(OrderOps.Generate(3, 77).ToCanonicalJson(), Generators.Generate("ops", 3, 77).ToCanonicalJson());
            Assert.AreEqual(Fluency.Generate(3, 77).ToCanonicalJson(), Generators.Generate("flu", 3, 77).ToCanonicalJson());
            Assert.Throws<ArgumentException>(() => Generators.Generate("nope", 1, 1));
        }

        [Test]
        public void PartsSplitsByPlaceValue()
        {
            CollectionAssert.AreEqual(new long[] { 300, 40, 7 }, MultDiv.Parts(347));
            CollectionAssert.AreEqual(new long[] { 70 }, MultDiv.Parts(70));
            CollectionAssert.AreEqual(new long[] { 100, 4 }, MultDiv.Parts(104));
        }

        [Test]
        public void DecomposeAndFilled()
        {
            CollectionAssert.AreEqual(new[] { 2, 4, 8 }, Fractions.Decompose(Rational.R(7, 8), new[] { 8, 2, 4 }));
            CollectionAssert.AreEqual(new[] { 3, 12 }, Fractions.Decompose(Rational.R(5, 12), new[] { 3, 12 }));
            Assert.Throws<InvalidOperationException>(() => Fractions.Decompose(Rational.R(1, 3), new[] { 2, 4 }));
            JsonValue pr = JsonValue.Obj().Set("target", Rational.R(1)).Set("pre", JsonValue.Ints(new[] { 2, 4 }));
            Assert.IsTrue(Fractions.Filled(pr, new[] { 4 }));
            Assert.IsFalse(Fractions.Filled(pr, new[] { 8 }));
            Assert.IsTrue(Fractions.Filled(pr, new[] { 2 }, new[] { 1 }));
            Assert.AreEqual("halves", Fractions.Plural(2));
            Assert.AreEqual("twelfths", Fractions.Plural(12));
        }

        [Test]
        public void GuardSpotsAStepThatStatesTheAnswer()
        {
            var p = new Problem { Answer = Answer.Money(1250), Steps = new List<string> { "a = <b>$12.50</b>.", "b", "c" } };
            Assert.IsTrue(Guard.Leaks(p));
            p.Steps[0] = "a = $12.505";
            Assert.IsFalse(Guard.Leaks(p));
            p.Steps[1] = "so = $12.50";
            Assert.IsTrue(Guard.Leaks(p), "end of text counts");
            p.Steps = new List<string> { "x", "y", "= <b>$12.50</b>" };
            Assert.IsFalse(Guard.Leaks(p), "the worked step may state it");
            var big = new Problem { Answer = Answer.Int(71467), Steps = new List<string> { "68,530 + 2,937 = 71,467 now", "", "" } };
            Assert.IsTrue(Guard.Leaks(big), "whole numbers are matched with en-US commas");
            var f = new Problem { Answer = Answer.Fraction(Rational.R(1, 2)), Steps = new List<string> { "= [[1/2]]", "", "" } };
            Assert.IsFalse(Guard.Leaks(f), "fractions never count as leaks (as in the JS)");
        }
    }
}
