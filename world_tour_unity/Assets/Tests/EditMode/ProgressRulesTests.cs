using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Data;
using WorldTour.Core.Maths;
using WorldTour.Core.Progress;
using WorldTour.Core.Shows;

namespace WorldTour.Core.Tests
{
    /// <summary>
    /// world_tour/js/progress.js rules on the save. (No Node test covers progress.js directly: core.test.mjs is
    /// rational, money and answers, and meta.test.mjs is task 1.6; these are written against the JS source.)
    /// </summary>
    public class ProgressRulesTests
    {
        private const string Today = "2026-11-20";

        private static SaveBlob Fresh() { return SaveBlob.Defaults(Today); }

        // ---------- fans and venues ----------

        [Test]
        public void Venues_MatchTheWebGameTable()
        {
            CollectionAssert.AreEqual(new[] { "coffee", "records", "rec", "rooftop", "club", "theater", "festival", "arena" }, Venues.All.Select(v => v.Id).ToArray());
            CollectionAssert.AreEqual(new long[] { 0, 20000, 60000, 125000, 225000, 375000, 600000, 900000 }, Venues.All.Select(v => v.Fans).ToArray());
            Assert.AreEqual("coffee", Venues.ById("nope").Id);
            StringAssert.Contains("someone" + (char)0x2019 + "s aunt", Venues.ById("rooftop").Blurb, "the curly quote from the JS copy");
        }

        [Test]
        public void AddFans_UnlocksAtTheThresholds_InOrder()
        {
            var s = Fresh();
            Assert.AreEqual(0, ProgressRules.AddFans(s, 19999).Count);
            CollectionAssert.AreEqual(new[] { "records" }, ProgressRules.AddFans(s, 1).Select(v => v.Id).ToArray(), "exactly 20,000");
            CollectionAssert.AreEqual(new[] { "rec", "rooftop", "club" }, ProgressRules.AddFans(s, 205000).Select(v => v.Id).ToArray(), "several at once");
            Assert.AreEqual(0, ProgressRules.AddFans(s, 0).Count);
            ProgressRules.AddFans(s, 10000000);
            CollectionAssert.AreEqual(Venues.All.Select(v => v.Id).ToArray(), s.Venues.Unlocked);
            Assert.AreEqual(s.Fans.Lifetime, s.Fans.Balance);
        }

        [Test]
        public void AddFans_NeverTakesAway()
        {
            var s = Fresh();
            ProgressRules.AddFans(s, 70000);
            Assert.Throws<ArgumentOutOfRangeException>(() => ProgressRules.AddFans(s, -1));
            Assert.AreEqual(70000, s.Fans.Lifetime);
            Assert.AreEqual(70000, s.Fans.Balance);
            s.Fans.Balance = 100;                                 // she spent some backstage (task 1.6)
            ProgressRules.AddFans(s, 500);
            Assert.AreEqual(600, s.Fans.Balance, "balance and lifetime are separate");
            Assert.AreEqual(70500, s.Fans.Lifetime);
            CollectionAssert.AreEqual(new[] { "coffee", "records", "rec" }, s.Venues.Unlocked, "spending never locks a venue");
        }

        [Test]
        public void NewestNextUnseen_AndMarkSeen()
        {
            var s = Fresh();
            Assert.AreEqual("coffee", ProgressRules.NewestVenue(s).Id);
            Assert.AreEqual("records", ProgressRules.NextVenue(s).Id);
            Assert.AreEqual(0, ProgressRules.UnseenUnlocks(s).Count);
            ProgressRules.AddFans(s, 60000);
            Assert.AreEqual("rec", ProgressRules.NewestVenue(s).Id);
            Assert.AreEqual("rooftop", ProgressRules.NextVenue(s).Id);
            CollectionAssert.AreEqual(new[] { "records", "rec" }, ProgressRules.UnseenUnlocks(s));
            Assert.IsTrue(ProgressRules.MarkSeen(s, "records"));
            Assert.IsFalse(ProgressRules.MarkSeen(s, "records"));
            CollectionAssert.AreEqual(new[] { "rec" }, ProgressRules.UnseenUnlocks(s));
            Assert.IsTrue(ProgressRules.Unlocked(s, "rec"));
            Assert.IsFalse(ProgressRules.Unlocked(s, "arena"));
            ProgressRules.AddFans(s, 900000);
            Assert.IsNull(ProgressRules.NextVenue(s));
            Assert.AreEqual("arena", ProgressRules.NewestVenue(s).Id);
        }

        // ---------- day streak ----------

        [Test]
        public void Dates_LocalIsoAndDaysBetween()
        {
            Assert.AreEqual("2026-03-07", Dates.LocalIso(new DateTime(2026, 3, 7, 23, 59, 0, DateTimeKind.Local)));
            Assert.AreEqual(1, Dates.DaysBetween("2026-11-19", "2026-11-20"));
            Assert.AreEqual(-3, Dates.DaysBetween("2026-11-20", "2026-11-17"));
            Assert.AreEqual(1, Dates.DaysBetween("2024-02-28", "2024-02-29"), "leap day");
            Assert.AreEqual(1, Dates.DaysBetween("2026-03-08", "2026-03-09"), "no daylight-saving drift (UTC days)");
            Assert.AreEqual(365, Dates.DaysBetween("2025-11-20", "2026-11-20"));
            Assert.IsNull(Dates.DaysBetween("garbage", "2026-11-20"));
            Assert.IsNull(Dates.DaysBetween("2026-02-30", "2026-11-20"));
        }

        [Test]
        public void DayStreak_TodayOrYesterdayCounts_ThenItLapses()
        {
            var s = Fresh();
            Assert.AreEqual(0, ProgressRules.DayStreak(s, Today));
            Assert.IsNull(ProgressRules.DaysAway(s, Today));
            ProgressRules.TickStreak(s, "2026-11-18");
            ProgressRules.TickStreak(s, "2026-11-19");
            ProgressRules.TickStreak(s, "2026-11-19");             // twice in a day counts once
            Assert.AreEqual(2, s.Streak.Days);
            Assert.AreEqual(2, ProgressRules.DayStreak(s, "2026-11-19"));
            Assert.AreEqual(2, ProgressRules.DayStreak(s, "2026-11-20"), "yesterday still counts");
            Assert.AreEqual(0, ProgressRules.DayStreak(s, "2026-11-21"), "two days away: lapsed");
            Assert.AreEqual(2, ProgressRules.DaysAway(s, "2026-11-21"));
            ProgressRules.TickStreak(s, "2026-11-25");
            Assert.AreEqual(1, s.Streak.Days, "a gap starts over");
            Assert.AreEqual(2, s.Streak.Best, "the best is kept");
            s.Streak.Last = "not a date";
            Assert.AreEqual(0, ProgressRules.DayStreak(s, Today));
            Assert.IsNull(ProgressRules.DaysAway(s, Today));
        }

        // ---------- words ----------

        [Test]
        public void RecordWord_CountsAndReviewQueue()
        {
            var s = Fresh();
            ProgressRules.RecordWord(s, "because", clean: false, hinted: true, missed: true, keep: false, today: "2026-11-19");
            WordStat e = ProgressRules.WordStats(s, "because");
            Assert.AreEqual(new[] { 1, 0, 0, 1, 1 }, new[] { e.A, e.C, e.First, e.H, e.Miss });
            Assert.AreEqual("2026-11-19", e.Last);
            CollectionAssert.AreEqual(new[] { "because" }, ProgressRules.ReviewWords(s));
            ProgressRules.RecordWord(s, "because", true, false, false, keep: true, today: Today);
            CollectionAssert.AreEqual(new[] { "because" }, ProgressRules.ReviewWords(s), "missed earlier this show: stays");
            ProgressRules.RecordWord(s, "because", false, true, false, false, Today);
            CollectionAssert.AreEqual(new[] { "because" }, ProgressRules.ReviewWords(s), "a hinted answer isn't clean: stays");
            ProgressRules.RecordWord(s, "because", true, false, false, false, Today);
            Assert.AreEqual(0, ProgressRules.ReviewWords(s).Count, "a clean answer next time clears it");
            Assert.AreEqual(4, ProgressRules.WordAttempts(s, "because"));
            Assert.AreEqual(0, ProgressRules.WordAttempts(s, "never"));
            Assert.IsNull(ProgressRules.WordStats(s, "never"));
            ProgressRules.RecordWord(s, "a", false, false, true, false, Today);
            ProgressRules.RecordWord(s, "a", false, false, true, false, Today);
            CollectionAssert.AreEqual(new[] { "a" }, ProgressRules.ReviewWords(s), "no duplicates");
        }

        // ---------- shows and time ----------

        [Test]
        public void FinishShow_RecordsTheSummary_TicksTheStreak_Keeps200()
        {
            var s = Fresh();
            var sum = new ShowSummary { Mode = "remix", Venue = "coffee", N = 10, First = 9, Hinted = 1, Fans = 30000, Secs = 300, Best = 6, Sold = true };
            ShowRecord r = ProgressRules.FinishShow(s, sum, Today);
            Assert.AreEqual(Today, r.D);
            Assert.AreEqual(1, s.Streak.Days);
            Assert.AreEqual(Today, s.Streak.Last);
            for (int i = 0; i < 250; i++) ProgressRules.FinishShow(s, new ShowSummary { Mode = "m" + i, N = 1 }, Today);
            Assert.AreEqual(200, s.Shows.Count);
            Assert.AreEqual("m50", s.Shows[0].Mode, "the oldest go first");
        }

        [Test]
        public void AddPlayTime_PerDay_Keeps120Days()
        {
            var s = Fresh();
            ProgressRules.AddPlayTime(s, Today);
            ProgressRules.AddPlayTime(s, Today);
            Assert.AreEqual(30, s.Time[Today]);
            for (int d = 0; d < 130; d++) ProgressRules.AddPlayTime(s, Dates.LocalIso(new DateTime(2026, 1, 1).AddDays(d)), 60);
            Assert.AreEqual(120, s.Time.Count);
            Assert.IsTrue(s.Time.ContainsKey(Today), "the newest days stay");
            Assert.IsFalse(s.Time.ContainsKey("2026-01-01"));
            Assert.Throws<ArgumentOutOfRangeException>(() => ProgressRules.AddPlayTime(s, Today, -15));
        }

        // ---------- a show wired to the save ----------

        [Test]
        public void Attach_AShowLandsInTheSave()
        {
            var s = Fresh();
            var unlocked = new List<string>();
            var items = Enumerable.Range(0, 10).Select(i => (ShowItem)new WordItem("w" + i)).ToList();
            var engine = new ShowEngine(new ShowSetup
            {
                ModeId = "remix", VenueId = "coffee", Items = items, SkillRecord = ProgressRules.Skill(s, "spell:remix"), ClockMs = () => 0
            });
            ProgressRules.Attach(engine, s, () => Today, v => unlocked.Add(v.Id));
            var d = new ShowTestDriver();
            d.Play(engine, new[] { "w", "w", "mw", "w", "w", "w", "w", "w", "w", "w", "w" });   // JS scenario A
            Assert.AreEqual(33300, s.Fans.Lifetime);
            CollectionAssert.AreEqual(new[] { "records" }, unlocked);
            Assert.AreEqual(3, ProgressRules.Level(s, "spell:remix"), "level changes land in the save");
            CollectionAssert.AreEqual(new[] { "w2" }, ProgressRules.ReviewWords(s), "the clean replay kept it (keep)");
            Assert.AreEqual(1, s.Shows.Count);
            Assert.IsTrue(s.Shows[0].Sold);
            Assert.AreEqual(2, ProgressRules.WordStats(s, "w2").A);

            var enc = engine.CreateEncore(new Rng(5).Next);
            ProgressRules.Attach(enc, s, () => Today);
            new ShowTestDriver().Play(enc, new[] { "w" });
            Assert.AreEqual(0, ProgressRules.ReviewWords(s).Count, "a clean encore clears it");
            Assert.AreEqual(2, s.Shows.Count);
            Assert.IsTrue(s.Shows[1].Encore);
            Assert.AreEqual(33800, s.Fans.Lifetime);
        }

        [Test]
        public void Attach_MathShow_MissedProblemSavedWholeForNextSession()
        {
            var s = Fresh();
            var items = ShowPicker.PickProblems("frac", ProgressRules.ReviewItems(s, "frac"), new Rng(3).Next, 3);
            var engine = new ShowEngine(new ShowSetup { IsMath = true, ModeId = "beat", Skill = "frac", Items = items, SkillRecord = ProgressRules.Skill(s, "frac"), ClockMs = () => 0 });
            ProgressRules.Attach(engine, s, () => Today);
            new ShowTestDriver().Play(engine, new[] { "w", "mmmr", "w", "w" });
            Assert.AreEqual(1, s.Review.Math.Count);
            var stored = s.Review.Math[0];
            var played = (MathItem)items[1];
            Assert.AreEqual(played.Prob.ToCanonicalJson(), Review.Item(stored).Prob.ToCanonicalJson());

            // next session: it comes back first, as stored, after a reload
            var reloaded = SaveJson.Load(SaveJson.ToText(s), "2026-11-21").Blob;
            var next = ShowPicker.PickProblems("frac", ProgressRules.ReviewItems(reloaded, "frac"), new Rng(4).Next);
            Assert.AreEqual(played.Kid, next[0].Kid);
            Assert.AreEqual(played.Prob.ToCanonicalJson(), ((MathItem)next[0]).Prob.ToCanonicalJson());
        }

        [Test]
        public void Attach_RequiresTheSavesSkillRecord()
        {
            var s = Fresh();
            var engine = new ShowEngine(new ShowSetup { ModeId = "remix", Items = new List<ShowItem>(), SkillRecord = new SkillRecord() });
            Assert.Throws<ArgumentException>(() => ProgressRules.Attach(engine, s, () => Today));
        }
    }
}
