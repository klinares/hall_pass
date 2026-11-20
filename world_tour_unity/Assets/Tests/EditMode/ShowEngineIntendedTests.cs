using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;
using WorldTour.Core.Shows;

namespace WorldTour.Core.Tests
{
    /// <summary>
    /// The show rules where Unity deliberately differs from show.js (QUESTIONS 10 and its addendum; listed in
    /// IntendedDifferences.md): a miss on the final song waits for the encore, and "clean run" needs no misses and
    /// no hints. Everything else about the queue and scoring stays as the web game has it (ShowEngineTests, ShowEngineJsTests).
    /// </summary>
    public class ShowEngineIntendedTests
    {
        private static ShowEngine Started(int n, List<ShowEvent> log)
        {
            var e = ShowTestDriver.Spelling(ShowTestDriver.Words(n));
            e.Raised += log.Add;
            e.Start();
            return e;
        }

        private static void Song(ShowEngine e, string actions)
        {
            e.SetupHints(3);
            foreach (char ch in actions) ShowTestDriver.Act(e, ch);
            e.Next();
        }

        private static string Q(ShowEngine e) => string.Join(" ", e.Queue.Select(x => x.Item.Kid + (x.Bonus ? "*" : "")));

        [Test]
        public void MissOnTheFinalSong_DoesNotReplay_GoesToTheEncoreOnly()
        {
            // JS: at = min(i + 4, length) = 10, so w9 played again straight after its own worked solution.
            var log = new List<ShowEvent>();
            var e = Started(10, log);
            for (int i = 0; i < 9; i++) Song(e, "w");
            e.SetupHints(3);
            e.Miss(); e.Miss(); e.Miss(); e.Reveal();            // w9, the final song: three misses, then the reveal
            Assert.AreEqual(0, log.Count(x => x.Kind == ShowEventKind.Requeued), "no replay");
            ShowEvent skip = log.Single(x => x.Kind == ShowEventKind.RequeueSkipped);
            Assert.AreEqual("final-song", skip.Tag);
            Assert.AreEqual(2, e.BonusLeft, "the bonus slot is not used up");
            Assert.AreEqual("w0 w1 w2 w3 w4 w5 w6 w7 w8 w9", Q(e));
            e.Next();
            Assert.IsTrue(e.Finished, "the show ends after the final song");
            CollectionAssert.AreEqual(new[] { "w9" }, e.Summary.MissedItems.Select(x => x.Kid).ToArray(), "it goes to the encore and the review queue");
            Assert.IsTrue(e.Summary.CanEncore);
            Assert.AreEqual(1, e.Summary.Missed);
            CollectionAssert.AreEqual(new[] { "w9" }, e.CreateEncore(ShowTestDriver.Random(3)).Queue.Select(x => x.Item.Kid).ToArray());
        }

        [Test]
        public void MissOnTheFinalSong_WonOnASecondTry_AlsoWaitsForTheEncore()
        {
            var log = new List<ShowEvent>();
            var e = Started(10, log);
            for (int i = 0; i < 9; i++) Song(e, "w");
            Song(e, "mw");
            Assert.IsTrue(e.Finished);
            Assert.AreEqual("final-song", log.Single(x => x.Kind == ShowEventKind.RequeueSkipped).Tag);
            CollectionAssert.AreEqual(new[] { "w9" }, e.Summary.MissedItems.Select(x => x.Kid).ToArray());
        }

        [Test]
        public void MissWithFewerThanThreeLeft_ReturnsAsTheLastSong()
        {
            var log = new List<ShowEvent>();
            var e = Started(10, log);
            for (int i = 0; i < 8; i++) Song(e, "w");
            Song(e, "mw");                                       // w8, one song left
            Assert.AreEqual("w0 w1 w2 w3 w4 w5 w6 w7 w8 w9 w8*", Q(e), "after the one song left, as the last song");
            Song(e, "w");                                        // w9 is no longer the final slot: a miss on it would replay
            Song(e, "w");                                        // w8*
            Assert.IsTrue(e.Finished);
        }

        [Test]
        public void MissOnTheLastMainSong_WithABonusStillToCome_ReturnsAfterIt()
        {
            // w9 is not the final slot when w7* follows it, so its miss returns as the last song, as in the JS.
            var log = new List<ShowEvent>();
            var e = Started(10, log);
            for (int i = 0; i < 7; i++) Song(e, "w");
            Song(e, "mw");                                       // w7 -> end
            Song(e, "w");
            Song(e, "mw");                                       // w9, then w7*, then w9*
            Assert.AreEqual("w0 w1 w2 w3 w4 w5 w6 w7 w8 w9 w7* w9*", Q(e));
            Song(e, "w");
            Song(e, "mw");                                       // w9*: a bonus replay never comes back again
            Assert.AreEqual("bonus", log.Last(x => x.Kind == ShowEventKind.RequeueSkipped).Tag);
            Assert.IsTrue(e.Finished);
        }

        [Test]
        public void TheCapOfTwoReinsertsStays()
        {
            var log = new List<ShowEvent>();
            var e = Started(10, log);
            Song(e, "mw"); Song(e, "mw"); Song(e, "mw");
            Assert.AreEqual(2, log.Count(x => x.Kind == ShowEventKind.Requeued));
            Assert.AreEqual("no-bonus-left", log.Single(x => x.Kind == ShowEventKind.RequeueSkipped).Tag);
            while (!e.Finished) Song(e, "w");
            Assert.AreEqual(3, e.Summary.Missed);
            Assert.AreEqual(3, e.Summary.MissedItems.Count);
        }

        // ---------- clean run (QUESTIONS 10 addendum) ----------

        private const string Today = "2026-11-20";

        private static SaveBlob Finish(string script)
        {
            SaveBlob s = SaveBlob.Defaults(Today);
            var e = ShowTestDriver.Spelling(ShowTestDriver.Words(10));
            new ShowTestDriver().Play(e, script.Split(' '));
            ProgressRules.FinishShow(s, e.Summary, Today);
            return s;
        }

        private static bool CleanRun(SaveBlob s) => Milestones.ById("clean-run").Test(s, MetaTests.Modes);

        [Test]
        public void CleanRun_NeedsNoMissesAndNoHints()
        {
            Assert.IsTrue(CleanRun(Finish("w w w w w w w w w w")), "ten clean songs");
            // JS scenario A: one miss, replayed clean, first = 10 of 10. The JS awards it; the sticker says "no misses".
            SaveBlob missed = Finish("w w mw w w w w w w w w");
            Assert.AreEqual(10, missed.Shows[0].First);
            Assert.AreEqual(1, missed.Shows[0].Missed);
            Assert.IsFalse(CleanRun(missed), "a miss, even one replayed clean");
            Assert.IsFalse(CleanRun(Finish("w hw w w w w w w w w")), "a hint");
            Assert.IsTrue(CleanRun(Finish("w fw w w w w w w w w")), "a free hint (nothing new shown) is not counted");
        }

        [Test]
        public void ScoringAndVip_KeepTheWebRule_WhenAMissIsReplayedClean()
        {
            var e = ShowTestDriver.Spelling(ShowTestDriver.Words(10));
            new ShowTestDriver().Play(e, "w w mw w w w w w w w w".Split(' '));
            Assert.AreEqual(10, e.Summary.First, "a clean bonus replay still counts as a first try");
            Assert.IsTrue(e.Summary.Vip, "and can still earn the VIP ticket");
            Assert.AreEqual(1, e.Summary.Missed);
        }

        [Test]
        public void MissedCount_IsSavedOnlyWhenAboveZero_AndReadBack()
        {
            SaveBlob s = Finish("w w mw w w w w w w w w");
            string json = SaveJson.ToText(s);
            StringAssert.Contains("\"missed\":1", json);
            Assert.AreEqual(1, SaveJson.Load(json, Today).Blob.Shows[0].Missed);
            SaveBlob clean = Finish("w w w w w w w w w w");
            StringAssert.DoesNotContain("\"missed\"", SaveJson.ToText(clean), "a show with no misses is saved as the JS saves it");
        }
    }
}
