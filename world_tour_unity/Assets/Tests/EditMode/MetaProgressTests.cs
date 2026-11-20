using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Maths;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;
using WorldTour.Core.Shows;

namespace WorldTour.Core.Tests
{
    /// <summary>
    /// The meta rules reached through ProgressRules, as world_tour/js/progress.js:122-129 reaches meta.js, and the
    /// sticker award at the end of a show (show.js:221-222: finishShow, then awardStickers for the ticket screen).
    /// </summary>
    public class MetaProgressTests
    {
        private const string Today = "2026-11-20";

        [Test]
        public void BuyEquipPlace_GoThroughTheMetaRules()
        {
            SaveBlob s = SaveBlob.Defaults(Today); s.Fans.Balance = 20000; s.Fans.Lifetime = 20000;
            Assert.IsTrue(ProgressRules.BuyItem(s, "guitar:mint").Ok);
            Assert.AreEqual(2000, s.Fans.Balance); Assert.AreEqual(20000, s.Fans.Lifetime);
            Assert.AreEqual(BuyReason.Owned, ProgressRules.BuyItem(s, "guitar:mint").Reason);
            Assert.IsTrue(ProgressRules.EquipItem(s, "guitar:mint"));
            Assert.AreEqual("#8fe3c4", MetaRules.GetLook(s).Guitar);
            ProgressRules.PlaceSticker(s, "sticker:pick", 2, .25, 5);
            MetaTests.AssertSpot(.95, .25, 5, s.Journal.Layout["sticker:pick"]);
        }

        [Test]
        public void AwardStickers_NullModes_IsTheJsEmptyDefault()
        {
            SaveBlob s = SaveBlob.Defaults(Today); s.Streak.Best = 3;
            CollectionAssert.AreEqual(new[] { "streak-3" }, ProgressRules.AwardStickers(s, null, Today).Select(m => m.Id).ToList());
            Assert.AreEqual(Today, s.Journal.Stickers["streak-3"]);
            CollectionAssert.IsEmpty(ProgressRules.AwardStickers(s, null, Today));
        }

        [Test]
        public void Attach_AFinishedShowAwardsStickers_ForTheTicket()
        {
            SaveBlob s = SaveBlob.Defaults(Today);
            var items = Enumerable.Range(0, 10).Select(i => (ShowItem)new WordItem("w" + i)).ToList();
            var engine = new ShowEngine(new ShowSetup
            {
                ModeId = "remix", VenueId = "coffee", Items = items, SkillRecord = ProgressRules.Skill(s, "spell:remix"), ClockMs = () => 0
            });
            var got = new List<List<string>>();
            ProgressRules.Attach(engine, s, () => Today, null, MetaTests.Modes, fresh => got.Add(fresh.Select(m => m.Id).ToList()));
            new ShowTestDriver().Play(engine, new[] { "w", "w", "mw", "w", "w", "w", "w", "w", "w", "w", "w" });   // JS scenario A
            Assert.AreEqual(1, got.Count, "awarded once, when the show finished");
            /* scenario A: first 10 of 10 (the miss was replayed), 33,300 lifetime fans unlock the record store.
               Intended difference (QUESTIONS 10 addendum): the JS also awards "clean run" here; Unity doesn't, because
               the show had a miss. */
            CollectionAssert.AreEqual(new[] { "first-show", "sold-out", "venue-records" }, got[0]);
            Assert.AreEqual(1, s.Shows[0].Missed, "the miss is on the saved record");
            Assert.AreEqual(Today, s.Journal.Stickers["first-show"]);
            Assert.AreEqual(1, s.Shows.Count, "the show was saved before the award");

            var enc = engine.CreateEncore(new Rng(5).Next);
            var again = new List<List<string>>();
            ProgressRules.Attach(enc, s, () => Today, null, MetaTests.Modes, fresh => again.Add(fresh.Select(m => m.Id).ToList()));
            new ShowTestDriver().Play(enc, new[] { "w" });
            Assert.AreEqual(1, again.Count);
            CollectionAssert.IsEmpty(again[0], "nothing new, and nothing earned is awarded twice");
            Assert.AreEqual(3, s.Journal.Stickers.Count);
        }
    }
}
