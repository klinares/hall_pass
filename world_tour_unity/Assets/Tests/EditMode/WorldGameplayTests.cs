using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Data;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;
using WorldTour.Core.Progress;
using WorldTour.Core.Shows;
using WorldTour.Core.Tour;

namespace WorldTour.Core.Tests
{
    /// <summary>Phase 8.1: the rules under the 3D world's gameplay (gig assignment and length, crew lines, the crowd, the save key, travel).</summary>
    public class WorldGameplayTests
    {
        static readonly string[] Math5 = { "tickets", "truck", "budget", "soundcheck", "snack" };
        static readonly string[] Spell7 = { "slots", "vowels", "remix", "fanmail", "studio", "lyric", "prefix" };

        // ---------- gig assignment ----------

        [Test]
        public void Assign_IsTheSameEveryTime()
        {
            foreach (Venue v in Venues.All)
                CollectionAssert.AreEqual(GigPlan.Assign(v.Id, 3, Math5, Spell7), GigPlan.Assign(v.Id, 3, Math5, Spell7), v.Id);
        }

        [Test]
        public void Assign_GivesEachCrewMemberADifferentModeAndSpreadsMathAndSpelling()
        {
            foreach (Venue v in Venues.All)
            {
                string[] plan = GigPlan.Assign(v.Id, 3, Math5, Spell7);
                Assert.AreEqual(3, plan.Length);
                Assert.AreEqual(3, plan.Distinct().Count(), v.Id + ": " + string.Join(",", plan));
                Assert.IsTrue(Math5.Contains(plan[0]) && Spell7.Contains(plan[1]) && Math5.Contains(plan[2]), v.Id + ": math, spelling, math");
            }
        }

        [Test]
        public void Assign_CoffeeShopStartsWithTicketSales()
        {
            string[] plan = GigPlan.Assign("coffee", 3, Math5, Spell7);
            CollectionAssert.AreEqual(new[] { "tickets", "slots", "truck" }, plan);
        }

        [Test]
        public void Assign_NextPlaceLeadsWithAnotherMode()
        {
            Assert.AreNotEqual(GigPlan.Assign("coffee", 3, Math5, Spell7)[0], GigPlan.Assign("records", 3, Math5, Spell7)[0]);
        }

        [Test]
        public void Assign_UsesOnlyTheModesGiven_AndCopesWithShortLists()
        {
            string[] onlyMath = GigPlan.Assign("rooftop", 3, new[] { "tickets", "snack" }, new string[0]);
            Assert.IsTrue(onlyMath.All(m => m == "tickets" || m == "snack"));
            Assert.AreEqual(2, onlyMath.Distinct().Count(), "both used before one repeats");
            string[] onlySpelling = GigPlan.Assign("club", 2, new string[0], new[] { "remix" });
            CollectionAssert.AreEqual(new[] { "remix", "remix" }, onlySpelling);
            CollectionAssert.AreEqual(new[] { "", "" }, GigPlan.Assign("coffee", 2, new string[0], new string[0]));
            Assert.AreEqual(0, GigPlan.Assign("coffee", 0, Math5, Spell7).Length);
            Assert.AreEqual(3, GigPlan.Assign("nowhere", 3, null, Spell7).Length, "null list and unknown place do not throw");
        }

        // ---------- gig length ----------

        [Test]
        public void Length_IsThreeToFive_AndAllThreeShowUp()
        {
            var rng = new Rng(4242);
            var seen = new HashSet<int>();
            for (int i = 0; i < 400; i++)
            {
                int n = GigPlan.Length(rng.Next);
                Assert.That(n, Is.InRange(3, 5));
                seen.Add(n);
            }
            CollectionAssert.AreEquivalent(new[] { 3, 4, 5 }, seen);
            Assert.AreEqual(3, GigPlan.Length(() => 0.0));
            Assert.AreEqual(5, GigPlan.Length(() => 0.99999999));
            Assert.AreEqual(5, GigPlan.Length(() => 7.0), "a bad source stays in range");
        }

        [Test]
        public void ShortShow_PicksExactlyThatManyItems()
        {
            var rng = new Rng(9);
            // math: the three review items still count inside a three-song gig
            var review = Enumerable.Range(0, 3).Select(i => new MathItem("md", null, 100 + i)).ToList();
            foreach (int n in new[] { 3, 4, 5, 10 })
            {
                Assert.AreEqual(n, ShowPicker.PickProblems("md", review, rng.Next, n).Count, "math n=" + n);
                Assert.AreEqual(n, ShowPicker.PickProblems("md", new List<MathItem>(), rng.Next, n).Count);
            }
            Assert.AreEqual(2, ShowPicker.PickProblems("md", review, rng.Next, 2).Count, "a review list never makes a show longer than asked");
            var pool = Enumerable.Range(0, 12).Select(i => new WordItem("word" + i)).ToList();
            foreach (int n in new[] { 3, 4, 5 })
                Assert.AreEqual(n, ShowPicker.PickWords(pool, new string[0], w => 0, rng.Next, n).Count, "words n=" + n);
            Assert.AreEqual(10, ShowPicker.PickProblems("md", review, rng.Next).Count, "the full show is still ten");
        }

        // ---------- a gig plays by the show rules ----------

        static ShowEngine GigEngine(SaveBlob s, int n, bool gig)
        {
            var items = Enumerable.Range(0, n).Select(i => (ShowItem)new WordItem("g" + i)).ToList();
            var e = new ShowEngine(new ShowSetup
            {
                IsMath = false, ModeId = "remix", VenueId = "coffee", Items = items, Gig = gig,
                SkillRecord = ProgressRules.Skill(s, "spell:remix"), ClockMs = () => 0
            });
            ProgressRules.Attach(e, s, () => "2026-11-23");
            return e;
        }

        [Test]
        public void Gig_EarnsFansAndLevelsLikeAShow_ButIsNotASoldOutNightOrAHistoryEntry()
        {
            var s = SaveBlob.Defaults("2026-11-23");
            var e = GigEngine(s, 5, gig: true);
            new ShowTestDriver().Play(e, "w w w w w".Split(' '));
            Assert.IsTrue(e.Summary.Gig);
            Assert.IsFalse(e.Summary.Sold, "five clean songs in a gig is not a sold out show");
            Assert.AreEqual(1000 + 2000 + 3000 + 4000 + 5000, e.Fans, "same fans per question as the show");
            Assert.AreEqual(15000, s.Fans.Lifetime);
            Assert.AreEqual(0, s.Shows.Count, "the show history is for shows");
            Assert.AreEqual(1, s.Streak.Days, "the day streak still ticks");
            Assert.AreEqual(5, s.Skills["spell:remix"].C, "the skill record is updated as in a show");
            Assert.AreEqual(5, s.Words.Count, "each word is recorded");
        }

        [Test]
        public void Gig_MissStillReturnsThreeLaterAndSavesTheReview()
        {
            var s = SaveBlob.Defaults("2026-11-23");
            var e = GigEngine(s, 4, gig: true);
            var d = new ShowTestDriver();
            d.Play(e, "mw w w w w w".Split(' '));
            Assert.IsTrue(e.Finished);
            Assert.AreEqual(4, e.Summary.N, "main songs");
            Assert.AreEqual(5, e.Queue.Count, "the miss came back as a bonus song");
            Assert.IsTrue(s.Review.Words.Contains("g0"), "the missed word is in the review queue");
        }

        [Test]
        public void ARealShow_StillGoesInTheHistoryAndCanSellOut()
        {
            var s = SaveBlob.Defaults("2026-11-23");
            var e = GigEngine(s, 10, gig: false);
            new ShowTestDriver().Play(e, Enumerable.Repeat("w", 10).ToArray());
            Assert.IsFalse(e.Summary.Gig);
            Assert.IsTrue(e.Summary.Sold);
            Assert.AreEqual(1, s.Shows.Count);
        }

        // ---------- crew lines ----------

        [Test]
        public void CrewLines_AreShortLowercaseAndAskNothingOfHerTime()
        {
            foreach (string line in CrewLines.All())
            {
                int words = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
                Assert.LessOrEqual(words, CrewLines.MaxWords, line);
                Assert.AreEqual(line.ToLowerInvariant(), line, "lowercase: " + line);
                Assert.IsFalse(line.Contains("’") || line.Contains("'"), "no apostrophes: " + line);
                Assert.IsFalse(line.ToLowerInvariant().Contains("hurry") || line.Contains("timer") || line.Contains("fail"), line);
            }
        }

        [Test]
        public void CrewLines_EveryModeHasItsOwnPitch()
        {
            var all = Math5.Concat(Spell7).Concat(new[] { "merch", "beat", "stage" }).ToList();
            foreach (string id in all) Assert.AreNotEqual(CrewLines.Generic, CrewLines.For(id), id);
            Assert.AreEqual(all.Count, all.Select(CrewLines.For).Distinct().Count(), "no two modes share a line");
            Assert.AreEqual(CrewLines.Generic, CrewLines.For("nope"));
            Assert.AreEqual(CrewLines.Generic, CrewLines.For(null));
        }

        // ---------- the crowd ----------

        [Test]
        public void Crowd_GrowsWithFansAndStopsAtItsCap()
        {
            Assert.AreEqual(CrowdRules.Min, CrowdRules.Count(0));
            Assert.AreEqual(CrowdRules.Min, CrowdRules.Count(-5));
            int last = 0;
            for (long f = 0; f <= 1000000; f += 2500)
            {
                int c = CrowdRules.Count(f);
                Assert.GreaterOrEqual(c, last, "never fewer as fans grow");
                Assert.That(c, Is.InRange(CrowdRules.Min, CrowdRules.Max));
                last = c;
            }
            Assert.AreEqual(CrowdRules.Max, CrowdRules.Count(900000));
            Assert.Greater(CrowdRules.Count(20000), CrowdRules.Count(0));
        }

        // ---------- the save key ----------

        const string Today = "2026-11-23";

        static SaveBlob PlayedSave()
        {
            var s = SaveBlob.Defaults(Today);
            ProgressRules.AddFans(s, 24500);   // unlocks the record store
            ProgressRules.RecordWord(s, "enough", true, false, false, false, Today);
            ProgressRules.Skill(s, "md").Level = 2;
            return s;
        }

        [Test]
        public void WorldKey_RoundTripsThroughTheSaveText()
        {
            var s = PlayedSave();
            var w = new WorldState { Place = "records", HasPosition = true, X = -3.25f, Z = 7.5f };
            w.MetCrew.Add("coffee/Crew_1");
            w.MetCrew.Add("records/Crew_2");
            WorldSave.Write(s, w);
            string text = SaveJson.ToText(s);
            StringAssert.Contains("\"world\":", text);
            SaveBlob back = SaveJson.Load(text, Today).Blob;
            WorldState r = WorldSave.Read(back);
            Assert.AreEqual("records", r.Place);
            Assert.IsTrue(r.HasPosition);
            Assert.AreEqual(-3.25f, r.X, 0.001f);
            Assert.AreEqual(7.5f, r.Z, 0.001f);
            CollectionAssert.AreEqual(new[] { "coffee/Crew_1", "records/Crew_2" }, r.MetCrew);
            Assert.IsTrue(r.Met("coffee", "Crew_1"));
            Assert.IsFalse(r.Met("coffee", "Crew_2"));
        }

        [Test]
        public void SaveWrittenByTheWorldGame_LoadsInTheOldGameWithEverythingKept()
        {
            // "the 2D game" is this same loader and writer: it keeps keys it does not know, and reads every key it does
            var s = PlayedSave();
            WorldSave.Write(s, new WorldState { Place = "coffee", HasPosition = true, X = 1, Z = 2, MetCrew = { "coffee/Crew_3" } });
            string fromWorld = SaveJson.ToText(s);

            LoadResult old = SaveJson.Load(fromWorld, Today);
            Assert.IsFalse(old.Unreadable);
            Assert.AreEqual(24500, old.Blob.Fans.Lifetime, "fans carry over");
            Assert.IsTrue(old.Blob.Venues.Unlocked.Contains("records"), "venues carry over");
            Assert.AreEqual(2, old.Blob.Skills["md"].Level, "levels carry over");
            Assert.IsTrue(old.Blob.Words.ContainsKey("enough"), "words carry over");

            // the old game plays and saves again; the world's key is still there afterwards
            ProgressRules.AddFans(old.Blob, 1000);
            SaveBlob again = SaveJson.Load(SaveJson.ToText(old.Blob), Today).Blob;
            Assert.AreEqual(25500, again.Fans.Lifetime);
            WorldState kept = WorldSave.Read(again);
            Assert.AreEqual("coffee", kept.Place);
            Assert.AreEqual(1f, kept.X, 0.001f);
            CollectionAssert.AreEqual(new[] { "coffee/Crew_3" }, kept.MetCrew);
        }

        [Test]
        public void SaveWrittenByTheOldGame_LoadsInTheWorldGameWithNoWorldKey()
        {
            var s = PlayedSave();
            string text = SaveJson.ToText(s);
            StringAssert.DoesNotContain("\"world\"", text);
            WorldState w = WorldSave.Read(SaveJson.Load(text, Today).Blob);
            Assert.IsNull(w.Place);
            Assert.IsFalse(w.HasPosition);
            Assert.AreEqual(0, w.MetCrew.Count);
        }

        [Test]
        public void WorldKey_OnlyAddsOneTopLevelKey_AndNothingElseMoves()
        {
            var s = PlayedSave();
            string before = SaveJson.ToCanonical(s);
            WorldSave.Write(s, new WorldState { Place = "coffee", HasPosition = true, X = 0.5f, Z = -1f });
            string after = SaveJson.ToCanonical(s);
            Assert.AreNotEqual(before, after);
            var b = StrictJson.Parse(before); var a = StrictJson.Parse(after);
            var added = a.Keys.Where(k => !b.Keys.Contains(k)).ToList();
            CollectionAssert.AreEqual(new[] { "world" }, added);
            foreach (string k in b.Keys) Assert.AreEqual(b[k].ToCanonicalJson(), a[k].ToCanonicalJson(), "unchanged: " + k);
        }

        [Test]
        public void WorldKey_ToleratesJunk()
        {
            var s = SaveBlob.Defaults(Today);
            s.Extra = JsonValue.Obj().Set("world", JsonValue.Arr(JsonValue.Num(1)));
            Assert.IsNull(WorldSave.Read(s).Place, "not an object");
            s.Extra = JsonValue.Obj().Set("world", JsonValue.Obj().Set("place", JsonValue.Num(4)).Set("position", JsonValue.Str("x")).Set("metCrew", JsonValue.Arr(JsonValue.Num(1), JsonValue.Str("a/b"), JsonValue.Str("a/b"))));
            WorldState w = WorldSave.Read(s);
            Assert.IsNull(w.Place);
            Assert.IsFalse(w.HasPosition);
            CollectionAssert.AreEqual(new[] { "a/b" }, w.MetCrew);
            Assert.DoesNotThrow(() => WorldSave.Write(s, w));
            Assert.DoesNotThrow(() => WorldSave.Write(SaveBlob.Defaults(Today), new WorldState()));
        }

        // ---------- travel ----------

        [Test]
        public void Journey_FollowsTheVenueRouteAndItsThresholds()
        {
            Assert.AreEqual("records", Journey.Next("coffee").Id);
            Assert.AreEqual("arena", Journey.Next("festival").Id);
            Assert.IsNull(Journey.Next("arena"));
            Assert.AreEqual("next stop: record store", Journey.NextStopLine(Journey.Next("coffee")));
            Venue rec = Journey.Next("coffee");
            Assert.AreEqual("19,000 more fans to open it", Journey.DetailLine(rec, 1000, false, false));
            Assert.AreEqual("coming soon", Journey.DetailLine(rec, 20000, true, false));
            Assert.AreEqual("", Journey.DetailLine(rec, 20000, true, true));
            StringAssert.Contains("last stop", Journey.NextStopLine(null));
            // the unlock rule is the one the 2D game uses
            var s = SaveBlob.Defaults(Today);
            Assert.IsFalse(ProgressRules.Unlocked(s, "records"));
            ProgressRules.AddFans(s, 20000);
            Assert.IsTrue(ProgressRules.Unlocked(s, "records"));
        }
    }
}
