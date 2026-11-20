using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Shows;

namespace WorldTour.Core.Tests
{
    /// <summary>
    /// show.js pickWords / pickProblems / chunks and util.js shuffle. The expected picks were printed by the
    /// JS functions themselves (bodies copied verbatim into b14_picks.mjs in the 1.4 builder's scratchpad)
    /// with Math.random replaced by mulberry32(seed).next, the same sequence as Rng(seed).Next.
    /// </summary>
    public class ShowPickerTests
    {
        private static List<WordItem> Pool(int n)
        {
            return Enumerable.Range(0, n).Select(i => new WordItem("w" + i)).ToList();
        }

        private static readonly Dictionary<string, int> Stats = new Dictionary<string, int>
        {
            { "w0", 2 }, { "w1", 0 }, { "w2", 5 }, { "w5", 1 }, { "w7", 1 }, { "w9", 3 }, { "w12", 1 }
        };

        private static int Attempts(string w)
        {
            int a;
            return Stats.TryGetValue(w, out a) ? a : 0;
        }

        [TestCase(1, "w7 w12 w10 w6 w8 w11 w13 w3 w1 w4", 0.042729709995910525)]
        [TestCase(42, "w1 w10 w6 w12 w3 w7 w8 w4 w11 w13", 0.5923239905387163)]
        [TestCase(20261120, "w4 w3 w7 w10 w13 w11 w8 w1 w5 w6", 0.034555165097117424)]
        public void PickWords_MatchesTheJs(int seed, string expected, double nextAfter)
        {
            var random = ShowTestDriver.Random(seed);
            var got = ShowPicker.PickWords(Pool(14), new HashSet<string> { "w3", "w11" }, Attempts, random);
            Assert.AreEqual(expected, string.Join(" ", got.Select(x => x.W)));
            Assert.AreEqual(nextAfter, random(), 0.0, "used exactly as many random numbers as the JS");
        }

        [Test]
        public void PickWords_ReviewWordsFirst_ThenLeastPracticed()
        {
            var got = ShowPicker.PickWords(Pool(14), new HashSet<string> { "w3", "w11" }, Attempts, ShowTestDriver.Random(3)).Select(x => x.W).ToList();
            Assert.AreEqual(10, got.Count);
            CollectionAssert.IsSubsetOf(new[] { "w3", "w11" }, got, "missed last time always makes the show");
            CollectionAssert.DoesNotContain(got, "w2", "the most practiced word sits out");
            CollectionAssert.DoesNotContain(got, "w9");
            CollectionAssert.DoesNotContain(got, "w0");
        }

        [Test]
        public void PickWords_SmallPool_AllOfItShuffled()
        {
            var got = ShowPicker.PickWords(Pool(4), new HashSet<string>(), null, ShowTestDriver.Random(7));
            Assert.AreEqual("w0 w2 w1 w3", string.Join(" ", got.Select(x => x.W)), "JS: rng(7), 4 words, no stats");
        }

        [Test]
        public void PickProblems_ReviewFirstAtMostThree_ThenFreshSeeds()
        {
            var review = new[] { new MathItem("dec", 2, 5), new MathItem("dec", 1, 6) };
            var random = ShowTestDriver.Random(99);
            var got = ShowPicker.PickProblems("dec", review, random);
            CollectionAssert.AreEqual(new long[] { 5, 6, 559346073, 1728343727, 1161512776, 1482286469, 2445358, 1755660411, 24375757, 226423167 },
                got.Cast<MathItem>().Select(x => x.Seed).ToArray());
            Assert.AreEqual(0.3965785033069551, random(), 0.0);
            Assert.IsTrue(got.Skip(2).Cast<MathItem>().All(x => x.Level == null && x.Prob == null && x.Skill == "dec"));
            Assert.AreSame(review[0], got[0]);

            var five = Enumerable.Range(1, 5).Select(i => new MathItem("frac", 1, i)).ToArray();
            var capped = ShowPicker.PickProblems("frac", five, ShowTestDriver.Random(1));
            Assert.AreEqual(10, capped.Count);
            CollectionAssert.AreEqual(new long[] { 1, 2, 3 }, capped.Take(4).Cast<MathItem>().Take(3).Select(x => x.Seed).ToArray());
            Assert.IsNull(((MathItem)capped[3]).Level, "only three review problems per show");
        }

        [Test]
        public void Shuffle_MatchesUtilShuffle()
        {
            CollectionAssert.AreEqual(new[] { "c", "e", "b", "a", "d" }, ShowPicker.Shuffle(new[] { "a", "b", "c", "d", "e" }, ShowTestDriver.Random(3)));
        }

        [Test]
        public void Chunks_FirstAndLastSyllableOnly()
        {
            // the web form where it leaves at least a third hidden (unchanged from show.js)
            Assert.AreEqual("ex - ___ - ___ - ment", ShowPicker.Chunks("excitement", "ex-cit-e-ment"));
            Assert.AreEqual("ir - ___ - ___ - al", ShowPicker.Chunks("irrational", "ir-ra-tion-al"));
            Assert.AreEqual("st" + ShowPicker.Ellipsis + "hs", ShowPicker.Chunks("strengths", null));
            // intended differences (QUESTIONS 11); the JS gave the text in the comment
            Assert.AreEqual("sum - ___", ShowPicker.Chunks("summer", "sum-mer"));                // sum - mer
            Assert.AreEqual("em - ____", ShowPicker.Chunks("empire", "em-pire"));                 // em - pire
            Assert.AreEqual("beau - __ - ___", ShowPicker.Chunks("beautiful", "beau-ti-ful"));    // beau - ___ - ful (2 of 9 hidden)
            Assert.AreEqual("il - __ - ___", ShowPicker.Chunks("illegal", "il-le-gal"));          // il - ___ - gal
            Assert.AreEqual("c__", ShowPicker.Chunks("cat", null));                               // ca...at
            Assert.AreEqual("c__", ShowPicker.Chunks("cat", ""));
            Assert.AreEqual("j___", ShowPicker.Chunks("jump", "jump"));                           // ju...mp
            Assert.AreEqual("g_", ShowPicker.Chunks("go", null));                                 // go...go
            Assert.AreEqual(1, ShowPicker.Ellipsis.Length);
            Assert.AreEqual(0x2026, ShowPicker.Ellipsis[0]);
        }
    }
}
