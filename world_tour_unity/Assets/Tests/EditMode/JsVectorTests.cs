using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WorldTour.Core.Maths;

namespace WorldTour.Core.Tests
{
    /// <summary>
    /// Checks the C# Rng, Rational and Money against vectors printed by the web game's own JS
    /// (world_tour_unity/Tools/js_vectors.mjs, which writes js_vectors.json next to this file).
    /// Doubles are compared by their 64-bit pattern, so a match is bit for bit.
    /// Regenerate with: node world_tour_unity/Tools/js_vectors.mjs
    /// </summary>
    public class JsVectorTests
    {
        // ---------- JSON model (JsonUtility) ----------

        [Serializable] public class Vectors { public string[] pickArray; public int[] shuffleArray; public RngCase[] rng; public Call[] calls; }
        [Serializable] public class RngCase { public long seed; public NextBlock next; public int[] int1to10; public IntRange[] intRanges; public string[] pick; public IntList[] shuffles; public bool[] chance03; public MixedOp[] mixed; }
        [Serializable] public class NextBlock { public long[] u32; public string[] bits; }
        [Serializable] public class IntRange { public int lo; public int hi; public int[] values; }
        [Serializable] public class IntList { public int[] values; }
        [Serializable] public class MixedOp { public string op; public int lo; public int hi; public long value; public int[] values; }
        [Serializable] public class Call { public string fn; public long[] args; public string @out; }

        private static Vectors _vectors;

        private static Vectors Load()
        {
            if (_vectors != null) return _vectors;
            string path = Path.Combine(Application.dataPath, "Tests", "EditMode", "js_vectors.json");
            if (!File.Exists(path)) throw new FileNotFoundException("missing JS vectors; run node Tools/js_vectors.mjs", path);
            var v = JsonUtility.FromJson<Vectors>(File.ReadAllText(path));
            if (v == null || v.rng == null || v.rng.Length == 0 || v.calls == null || v.calls.Length == 0)
                throw new InvalidDataException("js_vectors.json is empty or malformed");
            _vectors = v;
            return v;
        }

        private static IEnumerable<TestCaseData> Seeds() =>
            Load().rng.Select((c, i) => new TestCaseData(i).SetName("{m} seed " + c.seed.ToString(CultureInfo.InvariantCulture)));

        private static IEnumerable<TestCaseData> Calls() =>
            Load().calls.Select((c, i) => new TestCaseData(i).SetName(
                "{m} " + (i + 1).ToString("000", CultureInfo.InvariantCulture) + " " + c.fn + " " +
                string.Join(" ", c.args.Select(a => a.ToString(CultureInfo.InvariantCulture)))));

        private static ulong Bits(double x) => unchecked((ulong)BitConverter.DoubleToInt64Bits(x));
        private static ulong Hex(string s) => Convert.ToUInt64(s, 16);

        // ---------- rng ----------

        [Test, TestCaseSource(nameof(Seeds))]
        public void RngNextMatchesJsBitForBit(int i)
        {
            RngCase c = Load().rng[i];
            var r = new Rng(c.seed);
            Assert.AreEqual(20, c.next.bits.Length);
            for (int k = 0; k < c.next.bits.Length; k++)
            {
                double x = r.Next();
                Assert.AreEqual(Hex(c.next.bits[k]), Bits(x), "next() #" + k + " bits");
                Assert.AreEqual(c.next.u32[k], (long)(x * 4294967296.0), "next() #" + k + " u32");
            }
        }

        [Test, TestCaseSource(nameof(Seeds))]
        public void RngIntMatchesJs(int i)
        {
            RngCase c = Load().rng[i];
            var r = new Rng(c.seed);
            CollectionAssert.AreEqual(c.int1to10, Enumerable.Range(0, c.int1to10.Length).Select(_ => r.Int(1, 10)).ToArray(), "int(1, 10)");
            foreach (IntRange range in c.intRanges)
            {
                var q = new Rng(c.seed);
                int[] got = Enumerable.Range(0, range.values.Length).Select(_ => q.Int(range.lo, range.hi)).ToArray();
                CollectionAssert.AreEqual(range.values, got, "int(" + range.lo + ", " + range.hi + ")");
            }
        }

        [Test, TestCaseSource(nameof(Seeds))]
        public void RngPickMatchesJs(int i)
        {
            Vectors v = Load();
            RngCase c = v.rng[i];
            var r = new Rng(c.seed);
            CollectionAssert.AreEqual(c.pick, Enumerable.Range(0, c.pick.Length).Select(_ => r.Pick(v.pickArray)).ToArray());
        }

        [Test, TestCaseSource(nameof(Seeds))]
        public void RngShuffleMatchesJs(int i)
        {
            Vectors v = Load();
            RngCase c = v.rng[i];
            var r = new Rng(c.seed);
            for (int k = 0; k < c.shuffles.Length; k++)
                CollectionAssert.AreEqual(c.shuffles[k].values, r.Shuffle(v.shuffleArray), "shuffle #" + k);
        }

        [Test, TestCaseSource(nameof(Seeds))]
        public void RngChanceMatchesJs(int i)
        {
            RngCase c = Load().rng[i];
            var r = new Rng(c.seed);
            CollectionAssert.AreEqual(c.chance03, Enumerable.Range(0, c.chance03.Length).Select(_ => r.Chance(0.3)).ToArray());
        }

        [Test, TestCaseSource(nameof(Seeds))]
        public void RngInterleavedCallsMatchJs(int i)
        {
            Vectors v = Load();
            RngCase c = v.rng[i];
            var r = new Rng(c.seed);
            int[] five = v.shuffleArray.Take(5).ToArray();
            for (int k = 0; k < c.mixed.Length; k++)
            {
                MixedOp op = c.mixed[k];
                string at = "step " + k + " (" + op.op + ")";
                switch (op.op)
                {
                    case "int": Assert.AreEqual(op.value, r.Int(op.lo, op.hi), at); break;
                    case "pick": Assert.AreEqual(v.pickArray[op.value], r.Pick(v.pickArray), at); break;
                    case "shuffle": CollectionAssert.AreEqual(op.values, r.Shuffle(five), at); break;
                    case "chance": Assert.AreEqual(op.value == 1, r.Chance(0.5), at); break;
                    case "next": Assert.AreEqual(op.value, (long)(r.Next() * 4294967296.0), at); break;
                    default: Assert.Fail("unknown op " + op.op); break;
                }
            }
        }

        // ---------- rational and money ----------

        [Test, TestCaseSource(nameof(Calls))]
        public void RationalAndMoneyMatchJs(int i)
        {
            Call c = Load().calls[i];
            Assert.AreEqual(c.@out, Eval(c.fn, c.args), c.fn + " [" + string.Join(", ", c.args) + "]");
        }

        [Test]
        public void VectorFileCoversEveryFunction()
        {
            var fns = new HashSet<string>(Load().calls.Select(c => c.fn));
            foreach (string f in new[] { "R", "gcd", "lcm", "add", "sub", "mul", "div", "eq", "cmp", "sum", "isSimplest", "isInt", "num", "mixed", "str", "dec", "dollars", "noCarryAdd", "noBorrowSub" })
                Assert.IsTrue(fns.Contains(f), "no vectors for " + f);
            Assert.AreEqual(10, Load().rng.Length, "seed count");
        }

        /// <summary>Runs the C# port and writes the result the way the JS script's String(out) does.</summary>
        private static string Eval(string fn, long[] a)
        {
            try
            {
                switch (fn)
                {
                    case "R": return Frac(Rational.R(a[0], a[1]));
                    case "gcd": return Num(Rational.Gcd(a[0], a[1]));
                    case "lcm": return Num(Rational.Lcm(a[0], a[1]));
                    case "add": return Frac(Rational.Add(Rational.R(a[0], a[1]), Rational.R(a[2], a[3])));
                    case "sub": return Frac(Rational.Sub(Rational.R(a[0], a[1]), Rational.R(a[2], a[3])));
                    case "mul": return Frac(Rational.Mul(Rational.R(a[0], a[1]), Rational.R(a[2], a[3])));
                    case "div": return Frac(Rational.Div(Rational.R(a[0], a[1]), Rational.R(a[2], a[3])));
                    case "eq": return Bool(Rational.Eq(Rational.R(a[0], a[1]), Rational.R(a[2], a[3])));
                    case "cmp": return Num(Rational.Cmp(Rational.R(a[0], a[1]), Rational.R(a[2], a[3])));
                    case "sum":
                        var list = new List<Rational>();
                        for (int k = 0; k + 1 < a.Length; k += 2) list.Add(Rational.R(a[k], a[k + 1]));
                        return Frac(Rational.Sum(list));
                    case "isSimplest": return Bool(Rational.IsSimplest(a[0], a[1]));
                    case "isInt": return Bool(Rational.IsInt(a[0], a[1]));
                    case "num": return Bits(Rational.Num(Rational.R(a[0], a[1]))).ToString("x16");
                    case "mixed":
                        MixedNumber m = Rational.Mixed(Rational.R(a[0], a[1]));
                        return Num(m.W) + " " + Num(m.N) + "/" + Num(m.D);
                    case "str": return Rational.Str(Rational.R(a[0], a[1]));
                    case "dec": return Rational.Dec(Rational.R(a[0], a[1]));
                    case "dollars": return Money.Dollars(a[0]);
                    case "noCarryAdd": return Num(Money.NoCarryAdd(a));
                    case "noBorrowSub": return Num(Money.NoBorrowSub(a[0], a[1]));
                    default: return "unknown fn " + fn;
                }
            }
            catch (Exception)
            {
                return "throws";
            }
        }

        private static string Num(long v) => v.ToString(CultureInfo.InvariantCulture);
        private static string Frac(Rational f) => Num(f.N) + "/" + Num(f.D);
        private static string Bool(bool b) => b ? "true" : "false";
    }
}
