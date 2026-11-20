using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;
using WorldTour.Core.Progress;
using WorldTour.Core.Shows;

namespace WorldTour.Core.Tests
{
    /// <summary>Ported from world_tour/tests/math/review.test.mjs (js/review.js), plus the save round trip.</summary>
    public class ProgressReviewTests
    {
        private static MathItem Item(string skill, int level, long seed)
        {
            return new MathItem(skill, level, seed, Generators.Generate(skill, level, seed));
        }

        /// <summary>The entry as it comes back from storage: through the save's JSON text and back.</summary>
        private static List<ReviewEntry> ThroughStorage(List<ReviewEntry> list)
        {
            var s = SaveBlob.Defaults("2026-11-20");
            s.Review.Math = list;
            return SaveJson.Load(SaveJson.ToText(s), "2026-11-20").Blob.Review.Math;
        }

        [Test]
        public void AMissedProblemIsStoredWhole_PromptAnswerChoicesHints()
        {
            var v = Item("ops", 3, 77);
            var r = Review.Record(new List<ReviewEntry>(), "ops", v, clean: false, missed: true).Single();
            Assert.AreEqual(v.Prob.Prompt, r.Prob["prompt"].String);
            Assert.AreEqual(v.Prob.ToJson()["answer"].ToCanonicalJson(), r.Prob["answer"].ToCanonicalJson());
            Assert.AreEqual(v.Prob.ToJson()["choices"].ToCanonicalJson(), r.Prob["choices"].ToCanonicalJson());
            Assert.AreEqual(v.Prob.ToJson()["steps"].ToCanonicalJson(), r.Prob["steps"].ToCanonicalJson());
            Assert.IsTrue(v.Prob.Choices.Any(c => c.IsText), "ops level 3 has text choices");
            v.Prob.Prompt = "changed later";                       // a copy, not a reference
            v.Prob.Params.Set("changed", true);
            Assert.AreNotEqual("changed later", r.Prob["prompt"].String);
            Assert.IsNull(r.Prob["params"]["changed"]);
        }

        [Test]
        public void ItComesBackExactlyAsSaved_EvenIfTheGeneratorNowMakesSomethingElse()
        {
            var v = Item("dec", 2, 12345);
            var saved = ThroughStorage(Review.Record(new List<ReviewEntry>(), "dec", v, clean: false, missed: true));
            saved[0].Prob.Set("prompt", "a fan buys a thing the old generator wrote");
            MathItem back = Review.Item(saved[0]);
            Assert.AreEqual("a fan buys a thing the old generator wrote", back.Prob.Prompt);
            Assert.AreEqual("dec", back.Skill);
            Assert.AreEqual(2, back.Level);
            Assert.AreEqual(12345, back.Seed);
        }

        [Test]
        public void EveryGeneratorsProblemsSurviveTheSave()
        {
            // JS: 7 skills x 3 levels x 60 seeds, through JSON.stringify/parse and reviewItem, deepEqual.
            int n = 0;
            foreach (string sk in Generators.Skills)
                for (int lv = 1; lv <= 3; lv++)
                    for (int seed = 1; seed <= 60; seed++)
                    {
                        var v = Item(sk, lv, seed);
                        var r = ThroughStorage(Review.Record(new List<ReviewEntry>(), sk, v, clean: false, missed: true))[0];
                        MathItem back = Review.Item(r);
                        Assert.IsNotNull(back.Prob, sk + " L" + lv + " seed " + seed);
                        Assert.AreEqual(v.Prob.ToCanonicalJson(), back.Prob.ToCanonicalJson(), sk + " L" + lv + " seed " + seed);
                        Assert.AreEqual(v.Kid, back.Kid);
                        n++;
                    }
            Assert.AreEqual(7 * 3 * 60, n);
        }

        [Test]
        public void TextChoices_ComeBackAsText()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                var v = Item("ops", 3, seed);
                var back = Review.Item(ThroughStorage(Review.Record(new List<ReviewEntry>(), "ops", v, false, true))[0]).Prob;
                for (int i = 0; i < v.Prob.Choices.Count; i++)
                {
                    Assert.AreEqual(v.Prob.Choices[i].IsText, back.Choices[i].IsText);
                    Assert.AreEqual(v.Prob.Choices[i].Text, back.Choices[i].Text);
                    Assert.AreEqual(v.Prob.Choices[i].Correct, back.Choices[i].Correct);
                    Assert.AreEqual(v.Prob.Choices[i].Why, back.Choices[i].Why);
                }
            }
        }

        [Test]
        public void OldSeedOnlyEntriesStillWork_AndGetTheFullProblemOnTheNextMiss()
        {
            var old = new List<ReviewEntry> { new ReviewEntry { Skill = "dec", Level = 2, Seed = 12345 } };
            Assert.IsNull(Review.Item(old[0]).Prob, "the show regenerates it from the seed");
            var list = Review.Record(old, "dec", Item("dec", 2, 12345), clean: false, missed: true);
            Assert.AreEqual(1, list.Count);
            Assert.IsTrue(Review.Usable(list[0].Prob));
            Assert.IsNull(old[0].Prob, "the input list is left alone");
            var broken = new ReviewEntry { Skill = "dec", Level = 2, Seed = 9, Prob = JsonValue.Obj().Set("prompt", "x") };   // hand-edited or damaged
            Assert.IsNull(Review.Item(broken).Prob);
        }

        [Test]
        public void ACleanAnswerClearsItUnlessMissedEarlierThisShow_TheQueueIsCapped()
        {
            var v = Item("frac", 1, 5);
            var list = Review.Record(new List<ReviewEntry>(), "frac", v, clean: false, missed: true);
            Assert.AreEqual(1, Review.Record(list, "frac", v, clean: true, missed: false, keep: true).Count);
            Assert.AreEqual(0, Review.Record(list, "frac", v, clean: true, missed: false).Count);
            Assert.AreEqual(1, Review.Record(list, "frac", v, clean: false, missed: true).Count, "no duplicates");
            list = new List<ReviewEntry>();
            for (int s = 1; s <= Review.Max + 5; s++) list = Review.Record(list, "md", Item("md", 1, s), clean: false, missed: true);
            Assert.AreEqual(Review.Max, list.Count);
            Assert.AreEqual(40, Review.Max);
            Assert.AreEqual(6, list[0].Seed, "oldest dropped first");
        }

        [Test]
        public void UsableProblemThatCantBeRead_IsRegeneratedNotCrashed()
        {
            // usable by the JS test (prompt, answer object, steps array) but the answer type is unknown
            var odd = JsonValue.Obj().Set("prompt", "p").Set("answer", JsonValue.Obj().Set("type", "banana")).Set("steps", JsonValue.Arr());
            Assert.IsTrue(Review.Usable(odd));
            Assert.IsNull(Review.Item(new ReviewEntry { Skill = "dec", Level = 1, Seed = 3, Prob = odd }).Prob);
        }

        [Test]
        public void ReviewItems_FirstThreeForTheSkill_FeedTheShow()
        {
            var s = SaveBlob.Defaults("2026-11-20");
            foreach (long seed in new long[] { 1, 2, 3, 4 }) ProgressRules.RecordMath(s, "dec", Item("dec", 1, seed), false, true, false);
            ProgressRules.RecordMath(s, "frac", Item("frac", 1, 9), false, true, false);
            var items = ProgressRules.ReviewItems(s, "dec");
            CollectionAssert.AreEqual(new long[] { 1, 2, 3 }, items.Select(x => x.Seed).ToArray());
            var picked = ShowPicker.PickProblems("dec", items, new Rng(1).Next);
            Assert.AreSame(items[0], picked[0]);
            Assert.AreEqual(Generators.Generate("dec", 1, 1).ToCanonicalJson(), ((MathItem)picked[0]).Prob.ToCanonicalJson());
        }
    }
}
