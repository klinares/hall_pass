using System;
using NUnit.Framework;
using WorldTour.Core.Maths;
using static WorldTour.Core.Maths.Rational;

namespace WorldTour.Core.Tests
{
    /// <summary>
    /// Ported from world_tour/tests/math/core.test.mjs: the three tests that cover rational.js,
    /// money.js and rng.js, assertion for assertion. The other four tests in that file cover
    /// answer.js and are ported with it (task 1.2).
    /// </summary>
    public class CoreMathTests
    {
        [Test, Description("core.test.mjs: rational arithmetic is exact")]
        public void RationalArithmeticIsExact()
        {
            Assert.AreEqual(R(1, 2), Add(R(1, 3), R(1, 6)));
            Assert.AreEqual(R(7, 12), Sub(R(3, 4), R(1, 6)));
            Assert.AreEqual(R(3), Mul(R(5, 2), R(6, 5)));
            Assert.AreEqual(R(2), Div(R(3, 4), R(3, 8)));
            Assert.IsTrue(Eq(R(2, 4), R(1, 2)));
            Assert.AreEqual("1 1/2", Str(R(3, 2)));
            Assert.AreEqual("2", Str(R(4, 2)));
            Assert.AreEqual("1/4", Str(R(3, 12)));
            Assert.AreEqual(new MixedNumber(1, 3, 4), Mixed(R(7, 4)));
            Assert.AreEqual("0.65", Dec(R(13, 20)));
            Assert.AreEqual("0.052", Dec(R(52, 1000)));
            Assert.AreEqual("450", Dec(R(450)));
            Assert.AreEqual(12, Lcm(4, 6));
            Assert.AreEqual(6, Gcd(12, 18));
            Assert.Throws<ArgumentException>(() => R(1, 0));
        }

        [Test, Description("core.test.mjs: money stays in whole cents")]
        public void MoneyStaysInWholeCents()
        {
            Assert.AreEqual("$3.50", Money.Dollars(350));
            Assert.AreEqual("$0.05", Money.Dollars(5));
            Assert.AreEqual("$0.30", Money.Dollars(10 + 20));
            Assert.AreEqual(325, Money.NoCarryAdd(175, 250));   // 1.75 + 2.50 without carrying: 3.25
            Assert.AreEqual(290, Money.NoCarryAdd(199, 101));
            Assert.AreEqual(1375, Money.NoBorrowSub(1000, 375)); // digit by digit, smaller from larger
        }

        [Test, Description("core.test.mjs: seeded rng repeats")]
        public void SeededRngRepeats()
        {
            var a = new Rng(42);
            var b = new Rng(42);
            for (int i = 0; i < 100; i++) Assert.AreEqual(a.Next(), b.Next());
            var r = new Rng(7);
            for (int i = 0; i < 1000; i++)
            {
                int n = r.Int(3, 9);
                Assert.IsTrue(n >= 3 && n <= 9, "int(3, 9) gave " + n);
            }
        }
    }

    /// <summary>C#-side behaviour the JS tests don't cover: the parts that differ by necessity.</summary>
    public class CoreMathExtraTests
    {
        [Test]
        public void DefaultRationalIsZeroOverOne()
        {
            Rational z = default;
            Assert.AreEqual(0, z.N);
            Assert.AreEqual(1, z.D);
            Assert.AreEqual(R(0), z);
            Assert.AreEqual("0", Str(z));
        }

        [Test]
        public void RationalOverflowThrowsInsteadOfLosingPrecision()
        {
            Rational big = R(long.MaxValue / 2 + 1, 1);
            Assert.Throws<OverflowException>(() => Add(big, big));
            Assert.Throws<OverflowException>(() => Mul(R(3_000_000_000L, 7), R(5_000_000_000L, 11)));
            Assert.Throws<OverflowException>(() => Gcd(long.MinValue, 1));
        }

        [Test]
        public void NewSeedUsesTheInjectedSourceLikeJs()
        {
            Assert.AreEqual(1, Rng.NewSeed(() => 0.0));
            Assert.AreEqual(2147483646, Rng.NewSeed(() => 0.9999999999999999));
            Assert.AreEqual(1 + 1073741823, Rng.NewSeed(() => 0.5));
        }

        [Test]
        public void PickFromEmptyListThrowsAfterConsumingOneValue()
        {
            var r = new Rng(42);
            var reference = new Rng(42);
            Assert.Throws<InvalidOperationException>(() => r.Pick(new int[0]));
            reference.Next();
            Assert.AreEqual(reference.Next(), r.Next());
        }

        [Test]
        public void ShuffleLeavesTheInputAlone()
        {
            var input = new[] { 1, 2, 3, 4, 5 };
            var shuffled = new Rng(9).Shuffle(input);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, input);
            CollectionAssert.AreEquivalent(input, shuffled);
        }
    }
}
