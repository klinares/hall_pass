using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Core.Tests
{
    /// <summary>
    /// Pins each intended difference from the web game in the generators and typed answers (QUESTIONS 7, 8, 9; the
    /// list is IntendedDifferences.md). Seeds are the ones the questions quote, so the wording can be read against them.
    /// </summary>
    public class IntendedDifferenceTests
    {
        private static readonly string X = " " + Glyph.Times + " ";

        // ---------- 9.1 and 14 soundcheck: moving the decimal point ----------

        private static readonly string D = " " + Glyph.Divide + " ";

        /// <summary>The first level 3 problem (in seed order from 1) whose prompt is <paramref name="label"/> = ?.</summary>
        private static Problem FindL3(string label)
        {
            for (long s = 1; s < 200000; s++)
            {
                Problem p = Fluency.Generate(3, s);
                if (p.Prompt == label + " = ?") { TestContext.Out.WriteLine(label + ": seed " + s + ", hint 2 \"" + p.Steps[1] + "\""); return p; }
            }
            Assert.Fail("no seed gives " + label);
            return null;
        }

        [Test]
        public void Soundcheck_DecimalPointHint_NeverPrintsTheDigits()
        {
            Problem p = Fluency.Generate(3, 42596);                   // 8.82 x 10: the JS said "the digits stay 441"
            Assert.AreEqual("8.82" + X + "10 = ?", p.Prompt);
            Assert.AreEqual(Glyph.Times + " 10 moves every digit 1 place to the left.", p.Steps[1]);
            Problem q = Fluency.Generate(3, 501898);                  // 6.1 x 10: the JS said "the digits stay 61", the answer
            Assert.AreEqual("6.1" + X + "10 = ?", q.Prompt);
            StringAssert.DoesNotContain("61", q.Steps[1]);
            Problem r = Fluency.Generate(3, 486060);                  // 9.2 x 10: the JS said "the digits stay 46"
            StringAssert.DoesNotContain("46", r.Steps[1]);
        }

        [Test]
        public void Soundcheck_PlaceValueHint_TheQuestionsExamples()
        {
            // QUESTIONS 14: 140 / 10 = 14 drops a 0; 8.8 x 100 = 880 fills one in; 3.2 x 10 does neither.
            Problem a = Fluency.Generate(3, 57);
            Assert.AreEqual("140" + D + "10 = ?", a.Prompt);
            Assert.AreEqual(Glyph.Divide + " 10 moves every digit 1 place to the right. what happens to the 0 at the end?", a.Steps[1]);
            Assert.AreEqual(Glyph.Times + " 100 moves every digit 2 places to the left. what fills the empty place?", FindL3("8.8" + X + "100").Steps[1]);
            Assert.AreEqual(Glyph.Times + " 10 moves every digit 1 place to the left.", FindL3("3.2" + X + "10").Steps[1]);
            Assert.AreEqual(Glyph.Divide + " 100 moves every digit 2 places to the right.", FindL3("7" + D + "100").Steps[1]);
            Assert.AreEqual(Glyph.Divide + " 100 moves every digit 2 places to the right. what happens to the 0 at the end?", FindL3("350" + D + "100").Steps[1]);
            Assert.AreEqual(Glyph.Times + " 1,000 moves every digit 3 places to the left. what fills the empty place?", FindL3("4.25" + X + "1,000").Steps[1]);
        }

        [Test]
        public void Soundcheck_PlaceValueHint_WordingParts()
        {
            Assert.AreEqual(Glyph.Times + " 10 moves every digit 1 place to the left.", Fluency.PlaceValueHint(true, 10, 1, false));
            Assert.AreEqual(Glyph.Times + " 1,000 moves every digit 3 places to the left. what fills the empty place?", Fluency.PlaceValueHint(true, 1000, 3, true));
            Assert.AreEqual(Glyph.Divide + " 100 moves every digit 2 places to the right.", Fluency.PlaceValueHint(false, 100, 2, false));
            Assert.AreEqual(Glyph.Divide + " 10 moves every digit 1 place to the right. what happens to the 0 at the end?", Fluency.PlaceValueHint(false, 10, 1, true));
        }

        private static readonly Regex PlaceValueLine = new Regex(
            "^([" + Glyph.Times + Glyph.Divide + "]) ([0-9,]+) moves every digit ([0-9]+) (place|places) to the (left|right)\\.( what fills the empty place\\?| what happens to the 0 at the end\\?)?$",
            RegexOptions.CultureInvariant);
        private static readonly Regex Level2Line = new Regex(
            "^(multiplying|dividing) by ([0-9,]+) moves every digit ([0-9]+) (place|places) to the (left|right)\\.$", RegexOptions.CultureInvariant);
        private static readonly Regex NumberText = new Regex("[0-9][0-9,]*(\\.[0-9]+)?|\\.[0-9]+", RegexOptions.CultureInvariant);

        private static Rational ReadDecimal(string s)
        {
            s = s.Replace(",", "");
            int dot = s.IndexOf('.');
            if (dot < 0) return Rational.R(long.Parse(s, CultureInfo.InvariantCulture));
            long den = 1;
            for (int i = dot + 1; i < s.Length; i++) den *= 10;
            return Rational.R(long.Parse(s.Remove(dot, 1), CultureInfo.InvariantCulture), den);
        }

        /// <summary>The digits as written, without the point, commas or leading zeros: 0.07 is "7", 140 is "140", 8.8 is "88".</summary>
        private static string Digits(Rational v) => Rational.Dec(v).Replace(".", "").Replace(",", "").TrimStart('0');

        private static long Pow10(int k) { long p = 1; for (int i = 0; i < k; i++) p *= 10; return p; }

        /// <summary>
        /// QUESTIONS 14: for every decimal-point problem at every level, hint 2's claim holds for that problem, checked
        /// against the numbers rather than the generator's own rule: every digit moves N places (the answer is the
        /// operand times or divided by 10^N, and 10^N is the power in the prompt), left for x and right for /, and the
        /// question about a zero is there exactly when a zero is filled in (the answer's digits are the operand's plus
        /// zeros) or drops (the operand's digits are the answer's plus zeros). Hints 1 and 2 never name the answer
        /// (QUESTIONS 8, 9). Level 2's powers of ten keep their own wording; their "moves every digit" line is checked too.
        /// </summary>
        [Test]
        public void Soundcheck_PlaceValueHint_TrueForEveryDecimalPointProblem()
        {
            const int n = 20000;
            var seen = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
            void Count(string k) => seen[k] = seen.TryGetValue(k, out int c) ? c + 1 : 1;
            for (int level = 1; level <= 3; level++)
                foreach (long seed in GeneratorContract.Seeds(level, n))
                {
                    Problem p = Fluency.Generate(level, seed);
                    string where = "L" + level + " seed " + seed + " " + p.Label + ": ";
                    if (p.Kind == "pow")
                    {
                        Match m2 = Level2Line.Match(p.Steps[0]);
                        Assert.IsTrue(m2.Success, where + p.Steps[0]);
                        bool t2 = m2.Groups[1].Value == "multiplying";
                        int k2 = int.Parse(m2.Groups[3].Value, CultureInfo.InvariantCulture);
                        Assert.AreEqual(Pow10(k2), long.Parse(m2.Groups[2].Value.Replace(",", ""), CultureInfo.InvariantCulture), where + p.Steps[0]);
                        Assert.AreEqual(t2 ? "left" : "right", m2.Groups[5].Value, where + p.Steps[0]);
                        Count("L2 pow line");
                        continue;
                    }
                    if (p.Kind != "decpow" && p.Kind != "decdiv")
                    {
                        foreach (string h in p.Steps) StringAssert.DoesNotContain("moves every digit", h, where);
                        continue;
                    }
                    Assert.AreEqual(3, level, where + "decimal-point problems are level 3");
                    bool times = p.Kind == "decpow";
                    string[] ab = p.Label.Split(new[] { times ? X : D }, System.StringSplitOptions.None);
                    Assert.AreEqual(2, ab.Length, where);
                    Rational a = ReadDecimal(ab[0]);
                    long power = ReadDecimal(ab[1]).N;
                    Rational answer = Answers.ValueOf(p.Answer);

                    string h2 = p.Steps[1];
                    Match m = PlaceValueLine.Match(h2);
                    Assert.IsTrue(m.Success, where + "hint 2 \"" + h2 + "\"");
                    Assert.AreEqual(times ? Glyph.Times : Glyph.Divide, m.Groups[1].Value, where + h2);
                    Assert.AreEqual(power, ReadDecimal(m.Groups[2].Value).N, where + h2);
                    int places = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
                    Assert.AreEqual(places == 1 ? "place" : "places", m.Groups[4].Value, where + h2);
                    Assert.AreEqual(Pow10(places), power, where + "N matches the power: " + h2);
                    Assert.AreEqual(times ? "left" : "right", m.Groups[5].Value, where + h2);
                    Rational moved = times ? Rational.Mul(a, Rational.R(Pow10(places))) : Rational.Div(a, Rational.R(Pow10(places)));
                    Assert.IsTrue(Rational.Eq(moved, answer), where + "every digit moved " + places + " to the " + m.Groups[5].Value + " gives " + Rational.Dec(moved) + ", answer " + Rational.Dec(answer));

                    string da = Digits(a), dans = Digits(answer);
                    bool filled = dans.Length > da.Length, dropped = dans.Length < da.Length;
                    if (filled) Assert.AreEqual(da + new string('0', dans.Length - da.Length), dans, where + "filled zeros");
                    if (dropped) Assert.AreEqual(dans + new string('0', da.Length - dans.Length), da, where + "dropped zeros");
                    if (!filled && !dropped) Assert.AreEqual(da, dans, where + "same digits");
                    string want = filled ? " what fills the empty place?" : dropped ? " what happens to the 0 at the end?" : "";
                    Assert.AreEqual(want, m.Groups[6].Value, where + h2);
                    Assert.IsFalse(times && dropped, where + "x never drops a zero");
                    Assert.IsFalse(!times && filled, where + "/ never fills one in at the end");

                    // The power, the operand and the place count are what she can see or count off the prompt; one of
                    // them equals the answer only by coincidence (100 / 10 = 10, 10 / 10 = 1), as HintLadderTests allows.
                    for (int i = 0; i < 2; i++)
                        foreach (Match num in NumberText.Matches(p.Steps[i]))
                        {
                            Rational v = ReadDecimal(num.Value);
                            if (Rational.Eq(v, Rational.R(power)) || Rational.Eq(v, a) || Rational.Eq(v, Rational.R(places))) continue;
                            Assert.IsFalse(Rational.Eq(v, answer), where + "hint " + (i + 1) + " names the answer: \"" + p.Steps[i] + "\"");
                        }

                    Count((times ? "x " : "/ ") + power + (filled ? " filled" : dropped ? " dropped" : " plain"));
                }
            TestContext.Out.WriteLine(string.Join("\n", seen.Select(kv => kv.Value + " x " + kv.Key)));
            foreach (string k in new[] { "x 10 plain", "x 100 filled", "x 100 plain", "x 1000 filled", "/ 10 plain", "/ 10 dropped", "/ 100 plain", "/ 100 dropped", "/ 1000 dropped", "L2 pow line" })
                Assert.IsTrue(seen.ContainsKey(k), "the sweep reached " + k);
        }

        // ---------- 8 ticket sales ----------

        [Test]
        public void TicketSales_SmallQuotient_TheListStopsBelowTheAnswer()
        {
            Problem p = MultDiv.Generate(2, 120786);                  // 120 / 40: the JS listed 40 x 3 = 120
            Assert.AreEqual(3.0, p.Answer.Value.Number);
            Assert.AreEqual("how many 40s fit in 120? 40" + X + "2 = 80. is there room for another?", p.Steps[0]);
        }

        [Test]
        public void TicketSales_QuotientTen_NoProductPrinted()
        {
            Problem p = MultDiv.Generate(2, 334599);                  // 290 / 29: the JS printed 29 x 10 = 290
            Assert.AreEqual(10.0, p.Answer.Value.Number);
            Assert.AreEqual("what happens to a number when you multiply it by 10? try it with 29.", p.Steps[0]);
        }

        [Test]
        public void TicketSales_Rows_KeepsTheTwoCandidates()
        {
            Problem p = MultDiv.Generate(3, 3001);                    // QUESTIONS 8.3: a fair partial step, unchanged
            Assert.AreEqual("rows", p.Kind);
            StringAssert.Contains("which one fits under", p.Steps[1]);
        }

        // ---------- 9.2 pack the truck ----------

        [Test]
        public void PackTheTruck_OneLayer_Hint2HasNoTotal()
        {
            Problem p = Volume.Generate(1, 24758);                    // 4 x 3 x 1: the JS said "that's 12 crates"
            Assert.AreEqual(12.0, p.Answer.Value.Number);
            Assert.AreEqual("the top layer is 4 across and 3 deep. how many crates is that? there is 1 layer.", p.Steps[1]);
        }

        // ---------- 9.4 tour budget story money ----------

        [Test]
        public void TourBudget_StoriesStayWithinStoryDollars()
        {
            // web seed 41840 is "the band has $156..."; seed 5783871 in the volume set too
            foreach (long s in new long[] { 41840, 5783871 })
                Assert.IsTrue(OrderOps.StoryDollarsOk(OrderOps.Generate(3, s).Prompt), "seed " + s);
            for (long s = 1; s <= 20000; s++)
                Assert.IsTrue(OrderOps.StoryDollarsOk(OrderOps.Generate(3, s).Prompt), "seed " + s);
            Assert.IsTrue(OrderOps.StoryDollarsOk("gas costs $150 and food costs $12"));
            Assert.IsFalse(OrderOps.StoryDollarsOk("the band has $151."));
        }

        // ---------- the mask the JS-match tests use ----------

        [Test]
        public void EveryRestoredShapeReallyDiffers_AndTheCountsMatchTheWebScan()
        {
            // Counts of each changed shape in the 3,000-seed volume sets, from the web generators
            // (scratch scan of world_tour/math/gen with helpers.mjs seeds(level)).
            var want = new Dictionary<string, int>
            {
                { IntendedDifferences.MdSmallQuotient, 28 }, { IntendedDifferences.MdQuotientTen, 49 },
                { IntendedDifferences.VolOneLayer, 675 }, { IntendedDifferences.FluDecimalPoint, 529 + 245 },
                // QUESTIONS 14: hint 2 of every other decimal-point problem (x and / by 100 and 1,000) changed too.
                { IntendedDifferences.FluDecimalPointMore, GeneratorContract.Seeds(3).Select(sd => Fluency.Generate(3, sd)).Count(q => q.Kind == "decpow" || q.Kind == "decdiv") - (529 + 245) }
            };
            var got = want.Keys.ToDictionary(k => k, k => 0);
            foreach (var (skill, level) in new[] { ("md", 2), ("vol", 1), ("flu", 3), ("md", 1), ("md", 3), ("vol", 2), ("flu", 2) })
                foreach (long s in GeneratorContract.Seeds(level))
                {
                    Problem p = Generators.Generate(skill, level, s);
                    string before = string.Join("|", p.Steps);
                    string shape = IntendedDifferences.ToJsForm(p);
                    if (shape == null) continue;
                    Assert.AreNotEqual(before, string.Join("|", p.Steps), shape + " seed " + s + " restored nothing");
                    got[shape]++;
                }
            foreach (var kv in want) Assert.AreEqual(kv.Value, got[kv.Key], kv.Key);
        }

        // ---------- 7 typed answers ----------

        private static string Parse(string s)
        {
            ParsedNumber p = Answers.ParseNumber(s);
            return p == null ? "null" : p.V.N + "/" + p.V.D;
        }

        [Test]
        public void Commas_OnlyThousandsGroupingReads()
        {
            Assert.AreEqual("1000/1", Parse("1,000"));
            Assert.AreEqual("12500/1", Parse("12,500"));
            Assert.AreEqual("1000000/1", Parse("1,000,000"));
            Assert.AreEqual("25001/2", Parse("12,500.5"));
            Assert.AreEqual("1000/1", Parse("$1,000"));
            Assert.AreEqual("1000/1", Parse("1000"));
            foreach (string s in new[] { "3,50", "1,0,0,0", ",5", "12,34.5", "1,5", "1,000,", "1,0000", "0.123,456", "1 ,000", "$3,50" })
                Assert.AreEqual("null", Parse(s), s);
        }

        [Test]
        public void SpacesAroundTheSlash_Read()
        {
            Assert.AreEqual("3/4", Parse("3 / 4"));
            Assert.AreEqual("3/4", Parse("3 /4"));
            Assert.AreEqual("3/4", Parse("3/ 4"));
            Assert.AreEqual("7/4", Parse("1 3 / 4"));
            Assert.AreEqual("null", Parse("1 / 2 / 3"));
        }

        [Test]
        public void UnreadableInput_IsNeverAMiss_AndAsksForTheRightShape()
        {
            Answer money = Answer.Money(350);
            CheckResult r = Answers.Check("3,50", money);
            Assert.IsTrue(r.Bad && !r.Ok, "3,50 isn't read as 350 (a miss) or guessed as 3.50");
            Assert.IsTrue(Answers.IsRetype(r));
            Assert.AreEqual("that doesn" + Glyph.Apos + "t read as a number yet. try again.", Answers.Nudge(r, AnswerType.Money));

            Answer pt = Answer.Point(3, 5);
            foreach (string s in new[] { "(3 5)", "abc", "5" })
            {
                CheckResult c = Answers.Check(s, pt);
                Assert.IsTrue(c.Bad && !c.Empty && !c.Ok, s);
                Assert.IsTrue(Answers.IsRetype(c), s);
                Assert.AreEqual("that doesn" + Glyph.Apos + "t read as a number yet. type it like (4, 4): across, then up.", Answers.Nudge(c, AnswerType.Point), s);
            }
            CheckResult blank = Answers.Check("  ", pt);
            Assert.IsTrue(blank.Empty && !blank.Bad);
            Assert.AreEqual("type your answer on the keys first.", Answers.Nudge(blank, AnswerType.Point));
            Assert.IsTrue(Answers.Check("(3, 5)", pt).Ok);
            Assert.IsNull(Answers.Nudge(Answers.Check("(5, 3)", pt), AnswerType.Point), "a real miss gets the hint ladder, not a nudge");
            Assert.IsFalse(Answers.IsRetype(Answers.Check("(5, 3)", pt)));
        }

        [Test]
        public void ExampleShapeIsNeverAnAnswer()
        {
            // (4, 4) is on the diagonal; the stage designer never asks for a point there, so the example can't give one away.
            for (int level = 1; level <= 3; level++)
                foreach (long s in GeneratorContract.Seeds(level))
                {
                    Problem p = Coords.Generate(level, s);
                    Assert.AreNotEqual(p.Answer.Value.X, p.Answer.Value.Y, "seed " + s);
                }
        }

        [Test]
        public void MixedNumberWithATopHeavyPart_StillCounts()
        {
            // QUESTIONS 7.4: kept. 1 5/4 is 9/4; a simplest-form problem still asks her to simplify it.
            Assert.IsTrue(Answers.Check("1 5/4", Answer.Fraction(Rational.R(9, 4))).Ok);
            CheckResult s = Answers.Check("1 5/4", Answer.Fraction(Rational.R(9, 4)), simplest: true);
            Assert.IsTrue(s.Simplify && !s.Ok);
        }
    }
}
