using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Core.Tests
{
    /// <summary>
    /// The snack table (skill fm): seeded checks over a few thousand seeds per level, with division on and off. One correct
    /// answer, unique choices, denominators within bounds, hints 1 and 2 (and their pictures) never state or count out the
    /// product, any equivalent answer accepted, the traps real and tagged.
    /// </summary>
    public class FracMulTests
    {
        const int Runs = 3000;
        static readonly Regex Frac = new Regex(@"\[\[(?:(\d+) )?(\d+)(?:/(\d+))?\]\]");
        static readonly Regex Equation = new Regex(@"=\s*(?:\[\[(?:(\d+) )?(\d+)(?:/(\d+))?\]\]|(\d+))");

        static IEnumerable<long> Seeds(int level) { for (int i = 0; i < Runs; i++) yield return 1000L * (level + 1) + i * 7919L + 1; }

        static IEnumerable<TestCaseData> Cases()
        {
            for (int level = 0; level <= 3; level++)
                foreach (bool div in new[] { true, false })
                    yield return new TestCaseData(level, div).SetName("Snack table level " + level + (div ? " with division" : " without division"));
        }

        static readonly Dictionary<int, string[]> Allowed = new Dictionary<int, string[]>
        {
            { 0, new[] { "unitof" } },
            { 1, new[] { "part" } },
            { 2, new[] { "unitnon", "nonnon", "shaded", "share", "howmany" } },
            { 3, new[] { "mixed", "missing", "compare", "share", "howmany" } },
        };

        static string Where(Problem p) { return "fm L" + p.Level + " " + p.Kind + " seed " + p.Seed; }

        static Rational Value(Problem p) { return Answers.ValueOf(p.Answer); }

        static Rational Parse(Match m)
        {
            if (m.Groups[4].Success && m.Groups[4].Value.Length > 0) return Rational.R(long.Parse(m.Groups[4].Value));
            long w = m.Groups[1].Success && m.Groups[1].Length > 0 ? long.Parse(m.Groups[1].Value) : 0;
            long n = long.Parse(m.Groups[2].Value);
            if (!m.Groups[3].Success) return Rational.R(n);
            long d = long.Parse(m.Groups[3].Value);
            return Rational.R(w * d + n, d);
        }

        /// <summary>Any "= value" in the text equal to the answer.</summary>
        static bool StatesAnswer(Problem p, string text)
        {
            Rational a = Value(p);
            foreach (Match m in Equation.Matches(text)) if (Rational.Eq(Parse(m), a)) return true;
            return false;
        }

        [Test, TestCaseSource(nameof(Cases))]
        public void ContractAndKinds(int level, bool division)
        {
            var seen = new HashSet<string>();
            foreach (long seed in Seeds(level))
            {
                Problem p = FracMul.Generate(level, seed, division);
                string at = Where(p);
                GeneratorContract.Contract(p, "fm", level);
                Assert.AreEqual(3, p.Steps.Count, at);
                Assert.IsFalse(p.Simplest, at + ": never ask for simplest form");
                CollectionAssert.Contains(Allowed[level], p.Kind, at);
                if (!division) Assert.IsFalse(p.Kind == "share" || p.Kind == "howmany", at + ": division is off");
                seen.Add(p.Kind);
                // the same seed gives the same problem
                Assert.AreEqual(p.ToCanonicalJson(), FracMul.Generate(level, seed, division).ToCanonicalJson(), at);
                // choices only on the two choice kinds: exactly one correct, unique text
                if (p.Kind == "shaded" || p.Kind == "compare")
                {
                    Assert.IsNotNull(p.Choices, at);
                    Assert.AreEqual(p.Kind == "compare" ? 3 : 4, p.Choices.Count, at);
                    Assert.AreEqual(1, p.Choices.Count(c => c.Correct), at + ": one correct choice");
                    Assert.AreEqual(p.Choices.Count, p.Choices.Select(c => c.Text).Distinct().Count(), at + ": unique choices");
                    foreach (Choice c in p.Choices) Assert.IsTrue(c.IsText && (c.Correct == (c.Why == null)), at);
                }
                else Assert.IsNull(p.Choices, at);
            }
            CollectionAssert.AreEquivalent(Allowed[level].Where(k => division || (k != "share" && k != "howmany")), seen, "every kind of level " + level + " shows up");
        }

        [Test, TestCaseSource(nameof(Cases))]
        public void AnswerIsWhatTheNumbersSay_DenominatorsInBounds(int level, bool division)
        {
            foreach (long seed in Seeds(level))
            {
                Problem p = FracMul.Generate(level, seed, division);
                string at = Where(p);
                JsonValue f = p.Params["factors"];
                Assert.IsNotNull(f, at);
                long max = p.Kind == "missing" ? FracMul.MaxMissingDenominator : FracMul.MaxDenominator;
                foreach (JsonValue x in f.Items) { Assert.LessOrEqual(x["d"].Long, max, at + ": denominator"); Assert.GreaterOrEqual(x["d"].Long, 2, at); }
                if (p.Kind == "unitof") Assert.AreEqual(0, p.Params["whole"].Long % p.Params["parts"].Long, at);
                Rational expect;
                switch (p.Kind)
                {
                    case "unitof": expect = Rational.R(p.Params["whole"].Long, p.Params["parts"].Long); break;
                    case "part": expect = Rational.Mul(f[0].AsRational(), f[1].AsRational()); break;
                    case "unitnon": case "nonnon": expect = Rational.Mul(f[0].AsRational(), f[1].AsRational()); break;
                    case "shaded": expect = Rational.R((long)p.Params["rowsOn"].Int * p.Params["colsOn"].Int, (long)p.Params["rows"].Int * p.Params["cols"].Int); break;
                    case "share": expect = Rational.Div(f[0].AsRational(), Rational.R(p.Params["split"].Long)); break;
                    case "howmany": expect = Rational.Div(Rational.R(p.Params["wholes"].Long), f[0].AsRational()); break;
                    case "mixed": expect = Rational.Mul(Rational.Add(Rational.R(p.Params["whole"].Long), f[0].AsRational()), f[1].AsRational()); break;
                    case "missing": expect = Rational.Div(p.Params["product"].AsRational(), f[0].AsRational()); break;
                    default: expect = Rational.Mul(f[0].AsRational(), f[1].AsRational()); break;
                }
                Assert.IsTrue(Rational.Eq(expect, Value(p)), at + ": answer " + Value(p) + " vs " + expect);
                if (p.Kind == "unitof") Assert.AreEqual(AnswerType.Int, p.Answer.Type);
                else Assert.AreEqual(AnswerType.Fraction, p.Answer.Type, at);
                if (p.Kind == "howmany") Assert.AreEqual(1, Value(p).D, at + ": a whole number of servings");
                if (p.Kind == "missing")
                {
                    JsonValue pr = p.Params["product"];
                    Assert.AreEqual(1, Rational.Gcd(pr["n"].Long, pr["d"].Long), at + ": product shown in simplest form");
                }
                if (p.Kind == "compare")
                {
                    Rational o = f[1].AsRational(), fx = f[0].AsRational();
                    int truth = p.Params["truth"].Int;
                    Assert.AreEqual(truth - 1, Math.Sign(Rational.Cmp(Value(p), fx)), at + ": compare");
                    Assert.AreEqual(truth - 1, Math.Sign(Rational.Cmp(o, Rational.R(1))), at);
                    Assert.IsTrue(p.Choices[truth].Correct, at);
                }
                if (p.Kind == "shaded")
                {
                    var c = p.Choices.Single(x => x.Correct);
                    Assert.IsTrue(c.Text.Contains(Glyph.Times), at);
                    foreach (Choice w in p.Choices.Where(x => !x.Correct)) Assert.IsNotNull(w.Why, at);
                    // no wrong expression has the same value as the picture
                    foreach (Choice w in p.Choices.Where(x => !x.Correct)) Assert.AreNotEqual(Value(Expr(w.Text)), Value(p), at + ": " + w.Text);
                    Assert.LessOrEqual(p.Params["rows"].Int, 6, at);
                    Assert.LessOrEqual(p.Params["cols"].Int, 6, at);
                }
            }
        }

        // value of "[[a/b]] x [[c/d]]" or "[[a/b]] + [[c/d]]"
        static Problem Expr(string text)
        {
            var ms = Frac.Matches(text).Cast<Match>().Select(Parse).ToList();
            Rational v = text.Contains("+") ? Rational.Add(ms[0], ms[1]) : Rational.Mul(ms[0], ms[1]);
            return new Problem { Answer = Answer.Fraction(v) };
        }

        [Test, TestCaseSource(nameof(Cases))]
        public void HintsOneAndTwoNeverStateTheAnswer_HintThreeDoes(int level, bool division)
        {
            foreach (long seed in Seeds(level))
            {
                Problem p = FracMul.Generate(level, seed, division);
                string at = Where(p);
                for (int i = 0; i < 2; i++)
                {
                    Assert.IsFalse(StatesAnswer(p, p.Steps[i]), at + ": hint " + (i + 1) + " states the answer: " + p.Steps[i]);
                    Assert.IsFalse(Regex.IsMatch(p.Steps[i], @"(is|makes|gives|comes to) \[\[" + Regex.Escape(Rational.Str(Value(p))) + @"\]\]"), at + ": " + p.Steps[i]);
                }
                string worked = p.Steps[2];
                string shown = Rational.Str(Value(p));
                Assert.IsTrue(worked.Contains(shown), at + ": the worked step shows the answer: " + worked);
                if (p.Kind != "compare" && p.Kind != "shaded" && p.Kind != "unitof")
                    Assert.IsTrue(worked.Contains(Glyph.Times) || worked.Contains(Glyph.Divide), at);
                if (p.Kind == "share" || p.Kind == "howmany") Assert.IsTrue(worked.Contains("check:"), at + ": the multiplication partner");
                foreach (string s in p.Steps) Assert.IsFalse(Regex.IsMatch(s, "undefined|NaN|null|\\[object"), at);
                Assert.AreEqual(p.Steps.Select(s => s.ToLowerInvariant()), p.Steps, at + ": lowercase voice");
                // an unreduced [[n/d]] is only ever a product the problem asked for or the worked step names
                Assert.AreEqual(p.Prompt, p.Prompt.ToLowerInvariant(), at);
            }
        }

        [Test, TestCaseSource(nameof(Cases))]
        public void PicturesNeverShadeOrCountTheProductBeforeTheWorkedStep(int level, bool division)
        {
            foreach (long seed in Seeds(level))
            {
                Problem p = FracMul.Generate(level, seed, division);
                string at = Where(p);
                string answer = "[[" + Rational.Str(Value(p)) + "]]";
                for (int stage = 0; stage <= 2; stage++)
                {
                    FmPicture pic = FracMulPicture.For(p, stage);
                    if (p.Kind != "shaded")
                    {
                        Assert.IsFalse(pic.Overlap, at + " stage " + stage + ": the product is not shaded");
                        Assert.IsFalse(pic.CutAll, at + " stage " + stage + ": the whole is not cut into every piece");
                        Assert.IsFalse(pic.SplitAll, at);
                        Assert.IsFalse(pic.Shade, at);
                        Assert.IsFalse(pic.Numbered, at + ": no numbering before the worked step");
                    }
                    Assert.IsFalse(pic.Caption.Contains(answer) || pic.Caption.Contains("="), at + ": caption");
                    Assert.IsFalse(pic.RowsLabel.Contains(answer) || pic.ColsLabel.Contains(answer), at);
                    if (p.Kind == "howmany") Assert.LessOrEqual(pic.WholesCut, 1, at + ": only the first tray is cut");
                    if (p.Kind == "unitof") Assert.AreEqual("", pic.Caption, at);
                    if (p.Kind == "share") Assert.IsFalse(pic.SplitAll, at);
                    if (p.Kind == "unitnon" || p.Kind == "nonnon" || p.Kind == "part")
                        Assert.IsTrue(!pic.CutAll && (pic.Rows == 1 || pic.CutShadedOnly), at);
                }
                FmPicture w = FracMulPicture.For(p, 3);
                if (p.Kind == "unitof" || p.Kind == "share") Assert.IsTrue(w.Shade, at);
                if (p.Kind == "part" || p.Kind == "unitnon" || p.Kind == "nonnon") Assert.IsTrue(w.Overlap && w.CutAll, at);
                if (p.Kind == "howmany") Assert.IsTrue(w.Numbered && w.WholesCut == w.Wholes, at);
                if (p.Kind == "mixed" || p.Kind == "missing" || p.Kind == "compare") Assert.AreEqual("none", w.Kind, at);
            }
        }

        [Test, TestCaseSource(nameof(Cases))]
        public void TrapsAreRealMistakes(int level, bool division)
        {
            var tags = new HashSet<string>();
            var known = new[]
            {
                "multiplied-instead", "took-some-away", "gave-the-bottom", "added-the-bottoms", "multiplied-the-share", "multiplied-the-known", "subtracted-the-known", "kept-one-part", "added-across",
                "multiplied-only-the-tops", "forgot-a-top", "divided-wrong-way", "divided-by-the-bottom", "added-them-up", "one-tray-only",
                "forgot-the-whole", "multiplied-only-the-whole", "counted-the-unshaded", "one-cell-only", "ignored-the-size",
                "multiplying-makes-bigger", "multiplying-makes-smaller"
            };
            foreach (long seed in Seeds(level))
            {
                Problem p = FracMul.Generate(level, seed, division);
                string at = Where(p);
                foreach (Trap t in p.Traps)
                {
                    CollectionAssert.Contains(known, t.Why, at);
                    tags.Add(t.Why);
                    Assert.IsFalse(Answers.Matches(Answers.Show(new Answer(p.Answer.Type, t.Value)).Replace("[[", "").Replace("]]", ""), p.Answer.Value, p.Answer.Type), at + ": a trap that is the answer");
                }
                if (p.Choices != null) foreach (Choice c in p.Choices) if (c.Why != null) CollectionAssert.Contains(known, c.Why, at);
                if (p.Choices == null) Assert.GreaterOrEqual(p.Traps.Count, 1, at + ": typed problems carry a trap");
            }
            TestContext.Out.WriteLine("level " + level + " tags: " + string.Join(", ", tags));
        }

        [Test]
        public void AnyEquivalentFormIsRight_NeverSimplestForm()
        {
            // 1/2 of 1/4: 1/8 and 7/56 are both right
            var p = new Problem { Answer = Answer.Fraction(Rational.R(1, 8)), Simplest = false };
            foreach (string t in new[] { "1/8", "7/56", "2/16", "3 / 24" }) Assert.IsTrue(Answers.Check(t, p.Answer, p.Simplest).Ok, t);
            var imp = Answer.Fraction(Rational.R(10, 7));
            foreach (string t in new[] { "10/7", "1 3/7", "20/14", "30/21" }) Assert.IsTrue(Answers.Check(t, imp).Ok, t);
            Assert.IsFalse(Answers.Check("7/10", imp).Ok);
            var whole = Answer.Fraction(Rational.R(8));
            foreach (string t in new[] { "8", "16/2", "8/1" }) Assert.IsTrue(Answers.Check(t, whole).Ok, t);
            // across every generated problem, the answer typed unreduced, doubled and (when it is more than one) mixed is accepted
            foreach (int level in new[] { 0, 1, 2, 3 })
                foreach (long seed in Seeds(level).Take(500))
                {
                    Problem g = FracMul.Generate(level, seed, true);
                    if (g.Choices != null) continue;
                    Rational v = Value(g);
                    Assert.IsTrue(Answers.Check(Rational.Str(v), g.Answer, false).Ok, Where(g) + " as " + Rational.Str(v));
                    Assert.IsTrue(Answers.Check((v.N * 2) + "/" + (v.D * 2), g.Answer, false).Ok, Where(g) + " doubled");
                    Assert.IsTrue(Answers.Check((v.N * 5) + "/" + (v.D * 5), g.Answer, false).Ok, Where(g) + " times five");
                    Assert.IsFalse(Answers.Check((v.N + 1) + "/" + v.D, g.Answer, false).Ok, Where(g) + " off by one");
                }
        }

        [Test]
        public void TrapsAreWrongAnswersAndAreCaughtWhenTyped()
        {
            var tried = 0;
            foreach (int level in new[] { 0, 1, 2, 3 })
                foreach (long seed in Seeds(level).Take(300))
                {
                    Problem p = FracMul.Generate(level, seed, true);
                    foreach (Trap t in p.Traps)
                    {
                        string typed = p.Answer.Type == AnswerType.Int ? JsNumber.Str(t.Value.Number) : Rational.Str(MathValue.ToRational(t.Value.N, t.Value.D));
                        Assert.IsFalse(Answers.Check(typed, p.Answer).Ok, Where(p) + " trap " + typed);
                        Assert.IsTrue(Answers.Matches(typed, t.Value, p.Answer.Type), Where(p) + " matches " + typed);
                        tried++;
                    }
                }
            Assert.Greater(tried, 1000);
        }

        [Test]
        public void TheSeamDefaultsToDivisionOn_AndGeneratorsRegistryKnowsFm()
        {
            Assert.IsTrue(FracMul.DivisionEnabled);
            bool saved = FracMul.DivisionEnabled;
            try
            {
                FracMul.DivisionEnabled = false;
                for (int i = 0; i < 400; i++)
                {
                    string k = Generators.Generate("fm", 2, i + 1).Kind;
                    Assert.IsTrue(k != "share" && k != "howmany", k);
                }
                FracMul.DivisionEnabled = true;
                Assert.IsTrue(Enumerable.Range(1, 400).Any(i => Generators.Generate("fm", 3, i).Kind == "share"));
            }
            finally { FracMul.DivisionEnabled = saved; }
            CollectionAssert.Contains(Generators.UnitySkills, "fm");
            CollectionAssert.DoesNotContain(Generators.Skills, "fm");
            Assert.IsTrue(new MathConfig().IsEnabled("fm"), "an unknown skill id counts as shown");
        }

        [Test]
        public void LevelZeroIsUnitFractionOfAWholeNumberThatDividesEvenly()
        {
            foreach (long seed in Seeds(0))
            {
                Problem p = FracMul.Generate(0, seed);
                long n = p.Params["whole"].Long, d = p.Params["parts"].Long;
                Assert.AreEqual(0, n % d);
                Assert.AreEqual(n / d, (long)p.Answer.Value.Number);
                Assert.IsTrue(d >= 2 && d <= 6 && n <= 48);
            }
        }

        [Test, TestCaseSource(nameof(Cases))]
        public void HintTwoPictureMarksNoProductCells(int level, bool division)
        {
            foreach (long seed in Seeds(level))
            {
                Problem p = FracMul.Generate(level, seed, division);
                FmPicture pic = FracMulPicture.For(p, 2);
                string at = Where(p);
                if (p.Kind == "shaded") continue;      // the shaded region is that problem's question, drawn from the start
                Assert.IsFalse(pic.Overlap, at + ": no dark product");
                Assert.IsFalse(pic.Shade, at + ": no shaded answer piece");
                Assert.IsFalse(pic.CutAll, at);
                // the unit-of tape's first piece IS the answer, so hint 2 must not light it
                if (p.Kind == "unitof") Assert.IsFalse(pic.MarkFirst, at + ": the piece that is the answer is not marked");
            }
        }

        static readonly Regex Slip = new Regex(
            @"(^|[^/\d.\]])1 (more )?(halves|thirds|fourths|fifths|sixths|sevenths|trays|servings|rows|columns|people|cups|pieces|shares)"
            + @"|1 of them are|1 \w+ are|between [3-9] people|among 2 people|[2-9] (half|third|fourth|fifth|sixth|seventh)"
            + @"|trail mix recipe|cups of (sandwiches|cookies|pretzels|granola bars|fruit|trail mix|popcorn|veggies)|(cookies|pretzels|sandwiches|veggies|fruit|popcorn|granola bars|trail mix) tray");

        [Test, TestCaseSource(nameof(Cases))]
        public void WordingHasNoPluralSlipsOrTrayOrderSlips(int level, bool division)
        {
            for (int i = 0; i < 6000; i++)
            {
                Problem p = FracMul.Generate(level, 17L + i * 104729L, division);
                string text = p.Prompt + " | " + string.Join(" | ", p.Steps);
                Match m = Slip.Match(text);
                Assert.IsFalse(m.Success, Where(p) + ": '" + m.Value + "' in: " + text);
                Assert.IsFalse(text.Contains("it'll come back") || text.Contains("it’ll come back"), Where(p));
            }
        }
    }
}
