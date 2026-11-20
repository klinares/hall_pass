using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Core.Tests
{
    /// <summary>
    /// The orderops helpers (tokenize, untokenize, step, solve, leftToRight, noParens) and the text choice.
    /// Expected values were printed by the real world_tour/math/gen/orderops.js. In the expressions below,
    /// x, : and ~ stand for the times, divide and minus signs.
    /// </summary>
    public class OrderOpsTests
    {
        private static string E(string s) => s.Replace("x", Glyph.Times).Replace(":", Glyph.Divide).Replace("~", Glyph.Minus);

        private static IEnumerable<TestCaseData> Cases()
        {
            // expr, states (| separated, "null" when solve fails), did, leftToRight, noParens (null = none)
            yield return new TestCaseData("(3 + 4) x 5", "(3 + 4) x 5|7 x 5|35", "3 + 4 = 7|7 x 5 = 35", 35.0, (double?)23);
            yield return new TestCaseData("20 ~ (3 + 4)", "20 ~ (3 + 4)|20 ~ 7|13", "3 + 4 = 7|20 ~ 7 = 13", 21.0, (double?)21);
            yield return new TestCaseData("2 + 3 x (8 ~ 6)", "2 + 3 x (8 ~ 6)|2 + 3 x 2|2 + 6|8", "8 ~ 6 = 2|3 x 2 = 6|2 + 6 = 8", 34.0, (double?)20);
            yield return new TestCaseData("12 : 4 + 2 x 3", "12 : 4 + 2 x 3|3 + 2 x 3|3 + 6|9", "12 : 4 = 3|2 x 3 = 6|3 + 6 = 9", 15.0, (double?)9);
            yield return new TestCaseData("(9 ~ 4) x (2 + 3)", "(9 ~ 4) x (2 + 3)|5 x (2 + 3)|5 x 5|25", "9 ~ 4 = 5|2 + 3 = 5|5 x 5 = 25", 13.0, (double?)4);
            yield return new TestCaseData("7 ~ 9 + 5", "null", "", 3.0, null);
            yield return new TestCaseData("10 : 4 x 2", "null", "", 5.0, null);
        }

        [Test, TestCaseSource(nameof(Cases))]
        public void SolveMatchesJs(string expr, string states, string did, double ltr, double? np)
        {
            string x = E(expr);
            Assert.AreEqual(x, OrderOps.Untokenize(OrderOps.Tokenize(x)));
            OrderOps.Solution s = OrderOps.Solve(x);
            if (states == "null") Assert.IsNull(s);
            else
            {
                Assert.IsNotNull(s);
                Assert.AreEqual(E(states), string.Join("|", s.States));
                Assert.AreEqual(E(did), string.Join("|", s.Did));
                Assert.AreEqual(double.Parse(states.Split('|').Last()), s.Value.Num);
            }
            Assert.AreEqual(ltr, OrderOps.LeftToRight(x));
            Assert.AreEqual(np, OrderOps.NoParens(x));
        }

        [Test]
        public void TokenizeTurnsAHyphenIntoTheMinusSign()
        {
            Assert.AreEqual("1 " + Glyph.Minus + " 2", OrderOps.Untokenize(OrderOps.Tokenize("1-2")));
            Assert.IsNull(OrderOps.Solve("1-2"));
            Assert.AreEqual(-1.0, OrderOps.LeftToRight("1-2"));
        }

        [Test]
        public void StepDropsLoneParentheses()
        {
            OrderOps.StepResult r = OrderOps.Step(OrderOps.Tokenize(E("(12) x 2")));
            Assert.IsNull(r.Did);
            Assert.AreEqual(E("12 x 2"), OrderOps.Untokenize(r.Toks));
        }

        [Test]
        public void TextChoicesSerialiseAsStrings()
        {
            var c = new Choice("3 + 4", true, null);
            Assert.IsTrue(c.IsText);
            Assert.AreEqual(ValueKind.Null, c.Value.Kind);
            Assert.IsFalse(new Choice(MathValue.Num(7), false, "near").IsText);
            var p = new Problem
            {
                Skill = "ops", Level = 3, Kind = "story", Params = JsonValue.Obj(), Prompt = "", Label = "", Answer = Answer.Int(7),
                Choices = new List<Choice> { c, new Choice(MathValue.Num(5), false, "near") }
            };
            StringAssert.Contains("\"choices\":[{\"correct\":true,\"value\":\"3 + 4\",\"why\":null},{\"correct\":false,\"value\":5,\"why\":\"near\"}]", p.ToCanonicalJson());
        }
    }
}
