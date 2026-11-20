using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using WorldTour.Core.Maths;
using static WorldTour.Core.Maths.Answers;
using static WorldTour.Core.Maths.Rational;

namespace WorldTour.Core.Tests
{
    /// <summary>
    /// Ported from world_tour/tests/math/core.test.mjs: the four tests that cover answer.js,
    /// assertion for assertion (the other three were ported with rational, money and rng in 1.1).
    /// </summary>
    public class AnswerCoreTests
    {
        [Test, Description("core.test.mjs: equivalent typed answers")]
        public void EquivalentTypedAnswers()
        {
            var half = Answer.Decimal(R(1, 2));
            foreach (var t in new[] { "0.5", ".5", ".50", "0.50", "1/2", "2/4" }) Assert.IsTrue(Check(t, half).Ok, t);
            var money = Answer.Money(350);
            foreach (var t in new[] { "$3.50", "3.5", "3.50", "$3.5", " 3.50 " }) Assert.IsTrue(Check(t, money).Ok, t);
            Assert.IsFalse(Check("3.05", money).Ok);
            Assert.IsFalse(Check("35", money).Ok);
            var frac = Answer.Fraction(R(3, 2));
            foreach (var t in new[] { "1 1/2", "3/2", "1.5", "6/4" }) Assert.IsTrue(Check(t, frac).Ok, t);
            Assert.IsTrue(Check("1,000", Answer.Int(1000)).Ok);
            Assert.IsTrue(Check("12.0", Answer.Int(12)).Ok);
        }

        [Test, Description("core.test.mjs: simplest form: right amount, now simplify")]
        public void SimplestFormRightAmountNowSimplify()
        {
            var a = Answer.Fraction(R(1, 2));
            Assert.AreEqual(new CheckResult(false, true), Check("2/4", a, simplest: true));
            Assert.IsTrue(Check("1/2", a, simplest: true).Ok);
            Assert.IsTrue(Check("2/4", a, simplest: false).Ok);
            var b = Answer.Fraction(R(5, 4));
            Assert.IsTrue(Check("1 1/4", b, simplest: true).Ok);
            Assert.IsTrue(Check("5/4", b, simplest: true).Ok);
            Assert.AreEqual(new CheckResult(false, true), Check("1 2/8", b, simplest: true));
        }

        [Test, Description("core.test.mjs: bad input is not a miss-shaped crash")]
        public void BadInputIsNotAMissShapedCrash()
        {
            var a = Answer.Int(5);
            Assert.AreEqual(new CheckResult(false, false, empty: true), Check("", a));
            Assert.IsTrue(Check("5/0", a).Bad);
            Assert.IsTrue(Check("abc", a).Bad);
            Assert.IsNull(ParseNumber("1/"));
        }

        [Test, Description("core.test.mjs: points and matches")]
        public void PointsAndMatches()
        {
            var p = Answer.Point(3, 5);
            Assert.IsTrue(Check("(3, 5)", p).Ok);
            Assert.IsTrue(Check(MathValue.Pt(3, 5), p).Ok);
            Assert.IsFalse(Check(MathValue.Pt(5, 3), p).Ok);
            Assert.IsTrue(Matches("2/4", MathValue.Frac(R(1, 2)), AnswerType.Fraction));
            Assert.IsTrue(Matches("$1.25", MathValue.Num(125), AnswerType.Money));
            Assert.AreEqual("[[1 1/2]]", Show(Answer.Fraction(R(3, 2))));
            Assert.AreEqual("$12.05", Show(Answer.Money(1205)));
        }
    }

    /// <summary>C#-side checks for answer.js, mistakes.js and config.js that the JS tests don't cover.</summary>
    public class AnswerExtraTests
    {
        [Test]
        public void SimplestFormLooksAtTheNumbersAsTyped()
        {
            Assert.IsFalse(ParseNumber("6/8").Simplest);
            Assert.AreEqual(R(3, 4), ParseNumber("6/8").V);
            Assert.IsFalse(ParseNumber("2 6/8").Simplest);
            Assert.IsFalse(ParseNumber("1 5/4").Simplest);
            Assert.IsTrue(ParseNumber("3/4").Simplest);
            Assert.IsFalse(IsSimplestForm(2, 4));
            Assert.IsTrue(IsSimplestForm(1, 2));
        }

        [Test]
        public void ShowIgnoresTheMachineCulture()
        {
            CultureInfo saved = Thread.CurrentThread.CurrentCulture;
            try
            {
                foreach (string name in new[] { "de-DE", "fr-FR", "hi-IN", "ar-SA" })
                {
                    Thread.CurrentThread.CurrentCulture = new CultureInfo(name);
                    Assert.AreEqual("1,234,567", Show(Answer.Int(1234567)), name);
                    Assert.AreEqual("0.65", Show(Answer.Decimal(R(13, 20))), name);
                    Assert.AreEqual("$12.05", Show(Answer.Money(1205)), name);
                    Assert.IsTrue(Check("1.5", Answer.Fraction(R(3, 2))).Ok, name);
                }
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = saved;
            }
        }

        [Test]
        public void NumbersPastLongRangeReadAsBadInput()
        {
            // Documented difference: the JS keeps going in imprecise doubles (a miss); here it doesn't parse.
            Assert.IsNull(ParseNumber("99999999999999999999"));
            Assert.IsTrue(Check("99999999999999999999", Answer.Int(5)).Bad);
            Assert.IsNull(ParseNumber("0.1234567890123456789"));
            Assert.IsNotNull(ParseNumber("0.123456789012345678"));
        }

        [Test]
        public void PickedValuesBehaveLikeJsObjects()
        {
            Assert.AreEqual(new CheckResult(false, false, empty: true), Check(MathValue.Null, Answer.Point(3, 5)));
            Assert.AreEqual(new CheckResult(false, false, empty: true), Check(MathValue.Num(3), Answer.Point(3, 5)));
            Assert.AreEqual(new CheckResult(false, false, empty: true), Check(MathValue.Null, Answer.Int(5)));
            Assert.AreEqual(new CheckResult(false, false, bad: true), Check(MathValue.Pt(5, 0), Answer.Int(5)));
            Assert.IsTrue(Check(MathValue.Num(5), Answer.Int(5)).Ok);
            Assert.IsTrue(Matches(MathValue.Pt(5, 3), MathValue.Pt(5, 3), AnswerType.Point));
            Assert.IsFalse(Matches(MathValue.Null, MathValue.Pt(5, 3), AnswerType.Point));
        }

        [Test]
        public void MathConfigFileMatchesTheWebGameAndValidates()
        {
            string path = Path.Combine(Application.dataPath, "StreamingAssets", "math_config.json");
            Assert.IsTrue(File.Exists(path), "missing " + path + "; run node Tools/config_to_json.mjs");
            var config = JsonUtility.FromJson<MathConfig>(File.ReadAllText(path));
            Assert.IsNotNull(config);
            CollectionAssert.IsEmpty(config.Validate());
            var web = MathConfig.WebDefaults();
            Assert.AreEqual(JsonUtility.ToJson(web), JsonUtility.ToJson(config));
            foreach (string skill in new[] { "dec", "md", "frac", "xy", "vol", "ops", "flu" }) Assert.IsTrue(config.IsEnabled(skill), skill);
            Assert.AreEqual(10000, config.BOUNDS.moneyCents);
            Assert.AreEqual(12, config.BOUNDS.maxDenominator);
            Assert.AreEqual(10, config.BOUNDS.coordMax);
            Assert.AreEqual(1000, config.BOUNDS.maxVolume);
        }

        [Test]
        public void MathConfigMissingKeysAndBadValuesAreReported()
        {
            var partial = JsonUtility.FromJson<MathConfig>("{\"ENABLED_SKILLS\":{\"frac\":false},\"BOUNDS\":{\"moneyCents\":10000,\"maxDenominator\":0,\"coordMax\":10}}");
            Assert.IsFalse(partial.IsEnabled("frac"));
            Assert.IsTrue(partial.IsEnabled("dec"), "a missing switch stays shown, as in the JS");
            Assert.IsTrue(partial.IsEnabled("not-a-skill"));
            var errors = partial.Validate();
            Assert.AreEqual(2, errors.Count, string.Join("\n", errors));
            Assert.IsTrue(errors.Any(e => e.Contains("BOUNDS.maxDenominator must be a whole number above 0 (it is 0)")), string.Join("\n", errors));
            Assert.IsTrue(errors.Any(e => e.Contains("BOUNDS.maxVolume is missing")), string.Join("\n", errors));

            var empty = JsonUtility.FromJson<MathConfig>("{}");
            Assert.AreEqual(4, empty.Validate().Count, string.Join("\n", empty.Validate()));
            Assert.IsTrue(empty.IsEnabled("flu"));

            var nulls = new MathConfig { ENABLED_SKILLS = null, BOUNDS = null };
            Assert.AreEqual(2, nulls.Validate().Count);
        }

        [Test]
        public void ChoiceSetWithNoRoomAndCleanTrapsKeepOrder()
        {
            var answer = Answer.Int(9);
            var traps = new[] { new Trap(MathValue.Num(8), "a"), new Trap(MathValue.Num(10), "b") };
            var clean = Mistakes.CleanTraps(answer, traps);
            CollectionAssert.AreEqual(new[] { "8:a", "10:b" }, clean.Select(t => t.ToString()).ToArray());
            var one = Mistakes.ChoiceSet(new Rng(1), answer, traps, new MathValue[0], 1);
            Assert.AreEqual(1, one.Count);
            Assert.IsTrue(one[0].Correct);
        }
    }
}
