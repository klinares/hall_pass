using System.Collections.Generic;
using System.Linq;
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

        // ---------- 9.1 soundcheck: moving the decimal point ----------

        [Test]
        public void Soundcheck_DecimalPointHint_NeverPrintsTheDigits()
        {
            Problem p = Fluency.Generate(3, 42596);                   // 8.82 x 10: the JS said "the digits stay 441"
            Assert.AreEqual("8.82" + X + "10 = ?", p.Prompt);
            Assert.AreEqual("the digits don" + Glyph.Apos + "t change; only the decimal point moves 1 place to the right.", p.Steps[1]);
            Problem q = Fluency.Generate(3, 501898);                  // 6.1 x 10: the JS said "the digits stay 61", the answer
            Assert.AreEqual("6.1" + X + "10 = ?", q.Prompt);
            StringAssert.DoesNotContain("61", q.Steps[1]);
            Problem r = Fluency.Generate(3, 486060);                  // 9.2 x 10: the JS said "the digits stay 46"
            StringAssert.DoesNotContain("46", r.Steps[1]);
        }

        [Test]
        public void Soundcheck_DecimalPointHint_SaysPlaceOrPlaces_AndTheDirection()
        {
            Assert.AreEqual("the digits don" + Glyph.Apos + "t change; only the decimal point moves 1 place to the left.", Fluency.DigitsStay(1, "left"));
            Assert.AreEqual("the digits don" + Glyph.Apos + "t change; only the decimal point moves 2 places to the right.", Fluency.DigitsStay(2, "right"));
            int div = 0;
            foreach (long s in GeneratorContract.Seeds(3))
            {
                Problem p = Fluency.Generate(3, s);
                if (p.Kind == "decdiv" && p.Label.EndsWith(" 10"))
                {
                    div++;
                    Assert.AreEqual(Fluency.DigitsStay(1, "left"), p.Steps[1], "seed " + s);
                }
            }
            Assert.Greater(div, 0);
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
                { IntendedDifferences.VolOneLayer, 675 }, { IntendedDifferences.FluDecimalPoint, 529 + 245 }
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
