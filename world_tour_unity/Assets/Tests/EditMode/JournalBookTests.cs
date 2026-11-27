using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;

namespace WorldTour.Core.Tests
{
    /// <summary>The paged tour journal (JournalBook) and the backstage two-tap buy (Backstage), rules only.</summary>
    public class JournalBookTests
    {
        const string Day = "2026-11-20";

        static SaveBlob Fresh() { return SaveBlob.Defaults(Day); }

        [Test]
        public void EmptyBook_TwoPagesOfEmptySlots_InMilestoneOrder_WithTheWebHints()
        {
            List<JournalPage> pages = JournalBook.Pages(Fresh());
            Assert.AreEqual(2, pages.Count, "20 milestones, 10 to a page, no bought page yet");
            var slots = pages.SelectMany(p => p.Slots).ToList();
            CollectionAssert.AreEqual(Milestones.All.Select(m => m.Id), slots.Select(x => x.Key));
            Assert.IsTrue(slots.All(x => !x.Earned && !x.Fresh));
            for (int i = 0; i < slots.Count; i++)
            {
                Assert.AreEqual(Milestones.All[i].How, slots[i].Note, "an empty slot shows how to earn it");
                Assert.AreEqual(Milestones.All[i].Name, slots[i].Name);
                Assert.AreEqual(Milestones.All[i].Art, slots[i].Art);
                Assert.AreEqual(i / JournalBook.PerPage, slots[i].Page);
                Assert.AreEqual(i % JournalBook.PerPage, slots[i].Index);
            }
        }

        [Test]
        public void Homes_AreInsideThePage_AndNeverOverlap()
        {
            var homes = Enumerable.Range(0, JournalBook.PerPage).Select(JournalBook.Home).ToList();
            foreach (Spot h in homes)
            {
                Assert.That(h.X, Is.InRange(.05, .95));
                Assert.That(h.Y, Is.InRange(.05, .95));
                Assert.That(Math.Abs(h.R), Is.LessThanOrEqualTo(7));
            }
            for (int a = 0; a < homes.Count; a++)
                for (int b = a + 1; b < homes.Count; b++)
                    Assert.IsTrue(Math.Abs(homes[a].X - homes[b].X) > .12 || Math.Abs(homes[a].Y - homes[b].Y) > .3, "slots " + a + " and " + b + " overlap");
            Assert.AreEqual(JournalBook.Home(3).X, JournalBook.Home(13).X, "the same place on every page");
        }

        [Test]
        public void EarnedSticker_SitsInItsSlot_UntilPlaced_ThenWhereSheMovedIt()
        {
            SaveBlob s = Fresh();
            s.Journal.Stickers["streak-3"] = "2026-11-03";
            JournalSlot sl = JournalBook.Pages(s).SelectMany(p => p.Slots).Single(x => x.Key == "streak-3");
            Assert.IsTrue(sl.Earned);
            Assert.AreEqual("earned 2026-11-03", sl.Note, "meta.js pageItems note");
            Assert.AreEqual(sl.Home.X, sl.Spot.X);
            MetaRules.Place(s, "streak-3", .5, .4, sl.Home.R);
            sl = JournalBook.Pages(s).SelectMany(p => p.Slots).Single(x => x.Key == "streak-3");
            Assert.AreEqual(.5, sl.Spot.X);
            Assert.AreEqual(.4, sl.Spot.Y);
            Assert.AreEqual(sl.Home.R, sl.Spot.R, "keeps its tilt");
        }

        [Test]
        public void BoughtStickers_GetALastPage_InShopOrder()
        {
            SaveBlob s = Fresh();
            s.Inventory.Owned.Add("sticker:pass");
            s.Inventory.Owned.Add("sticker:pick");
            List<JournalPage> pages = JournalBook.Pages(s);
            Assert.AreEqual(3, pages.Count);
            Assert.IsTrue(pages[2].Backstage);
            CollectionAssert.AreEqual(new[] { "sticker:pick", "sticker:pass" }, pages[2].Slots.Select(x => x.Key));
            Assert.IsTrue(pages[2].Slots.All(x => x.Earned && x.Note == "from backstage"));
            Assert.AreEqual("pick", pages[2].Slots[0].Art);
        }

        [Test]
        public void Fresh_UntilSeen_AndTheBookOpensOnTheFirstFreshPage()
        {
            SaveBlob s = Fresh();
            Assert.AreEqual(0, JournalBook.StartPage(JournalBook.Pages(s)));
            s.Journal.Stickers["venue-arena"] = Day;                 // the last milestone, page 2
            Assert.AreEqual(1, JournalBook.StartPage(JournalBook.Pages(s)));
            CollectionAssert.AreEqual(new[] { "venue-arena" }, JournalBook.MarkSeen(s, new[] { "venue-arena", "venue-arena", "first-show", "nope", null }));
            CollectionAssert.AreEqual(new[] { "venue-arena" }, s.Journal.Seen, "only stickers in her book, each once");
            Assert.IsFalse(JournalBook.Pages(s).SelectMany(p => p.Slots).Any(x => x.Fresh));
            Assert.AreEqual(0, JournalBook.StartPage(JournalBook.Pages(s)));
        }

        [Test]
        public void Eyebrow_CountsEarnedMilestonesOnly()
        {
            SaveBlob s = Fresh();
            s.Journal.Stickers["first-show"] = Day;
            s.Inventory.Owned.Add("sticker:tee");
            Assert.AreEqual("sticker book ✶ 1 of 20 earned", JournalBook.Eyebrow(s));
        }

        [Test]
        public void Backstage_TwoTapBuy_ThemeIsPutOn_TotalFansNeverMove()
        {
            SaveBlob s = Fresh();
            ProgressRules.AddFans(s, 61000); s.Fans.Coins = 122;   // 10B.1: backstage takes coins; fans only unlock places
            var b = new Backstage(s);
            Assert.AreEqual("dog:none", b.Selected);
            b.Select("theme:peach-navy");
            Mirror m = b.Mirror();
            Assert.AreEqual("buy for 120 coins", m.Action);
            Assert.AreEqual("peach-navy", m.Theme, "trying it on");
            Assert.AreEqual(PressOutcome.Armed, b.Press());
            Assert.AreEqual("tap again to buy", b.Mirror().Action);
            Assert.AreEqual(122, s.Fans.Coins, "one tap never spends");
            Assert.AreEqual(PressOutcome.Bought, b.Press());
            Assert.AreEqual(2, s.Fans.Coins);
            Assert.AreEqual(61000, s.Fans.Balance, "fans are never spent");
            Assert.AreEqual(61000, s.Fans.Lifetime);
            Assert.AreEqual("peach-navy", MetaRules.GetLook(s).Theme);
            Assert.AreEqual("theme is on", b.Mirror().Action);
            Assert.AreEqual("on", b.Tag(Shop.ById("theme:peach-navy")).Tag);
            b.Select("theme:tour-pink");
            Assert.AreEqual("switch to this theme", b.Mirror().Action);
            Assert.AreEqual(PressOutcome.Equipped, b.Press());
            Assert.AreEqual("tour-pink", MetaRules.GetLook(s).Theme);
            CollectionAssert.Contains(s.Inventory.Owned, "theme:peach-navy", "switching back never takes it away");
        }

        [Test]
        public void Backstage_PickingAnotherItem_OrTheTimer_CancelsTheArmedBuy()
        {
            SaveBlob s = Fresh();
            ProgressRules.AddFans(s, 20000); s.Fans.Coins = 40;   // 10B.1: coins pay
            var b = new Backstage(s);
            b.Select("sticker:pick");
            Assert.AreEqual(PressOutcome.Armed, b.Press());
            b.Select("sticker:note");
            Assert.IsNull(b.Armed);
            Assert.AreEqual(PressOutcome.Armed, b.Press());
            b.Disarm();
            Assert.AreEqual("buy for 10 coins", b.Mirror().Action);
            Assert.AreEqual(PressOutcome.Armed, b.Press());
            Assert.AreEqual(PressOutcome.Bought, b.Press());
            Assert.AreEqual("stuck it in!", Backstage.BoughtToast(Shop.ById("sticker:note")));
            Assert.AreEqual("see it in your journal", b.Mirror().Action);
            Assert.AreEqual(PressOutcome.OpenJournal, b.Press());
            Assert.IsTrue(JournalBook.InBook(s, "sticker:note"));
        }

        [Test]
        public void Backstage_ShortOfFans_SaysHowManyMore_AndCannotBuy()
        {
            SaveBlob s = Fresh();
            ProgressRules.AddFans(s, 59000); s.Fans.Coins = 118;
            var b = new Backstage(s);
            b.Select("theme:peach-navy");
            Mirror m = b.Mirror();
            Assert.AreEqual("2 more coins needed", m.Action);
            Assert.AreEqual(MirrorAction.None, m.Does);
            Assert.AreEqual(PressOutcome.None, b.Press());
            Assert.AreEqual(118, s.Fans.Coins);
            Assert.IsTrue(b.Tag(Shop.ById("theme:peach-navy")).Short);
        }

        [Test]
        public void Copy_IsTheWebGames()
        {
            Assert.AreEqual("merch + wardrobe ✶ cosmetic only", Backstage.Eyebrow);
            // phase 10B.1: backstage takes coins now, so the web game's note about fans becomes one about coins
            Assert.AreEqual("coins buy things. your fans (12,345) are never spent.", Backstage.Note(12345));
        }

        /// <summary>
        /// "Nothing she has earned is ever taken away": 2,000 random sessions of buying, equipping, earning, placing and
        /// opening the journal, with a save and reload between some steps, never lower lifetime fans and never remove an
        /// owned item, an earned sticker, a placed spot or a seen mark.
        /// </summary>
        [Test]
        public void RandomSessions_NeverTakeAnythingAway()
        {
            var rnd = new Random(5150);
            var modes = new List<ModeInfo> { new ModeInfo("remix", "remix", ModeInfo.Spelling), new ModeInfo("merch", "merch table", ModeInfo.Math, "dec") };
            for (int run = 0; run < 2000; run++)
            {
                SaveBlob s = Fresh();
                var b = new Backstage(s);
                for (int k = 0; k < 30; k++)
                {
                    long life = s.Fans.Lifetime;
                    var owned = s.Inventory.Owned.ToList();
                    var stickers = s.Journal.Stickers.ToDictionary(x => x.Key, x => x.Value);
                    var seen = s.Journal.Seen.ToList();
                    var placed = s.Journal.Layout.Keys.ToList();
                    switch (rnd.Next(8))
                    {
                        case 0: ProgressRules.AddFans(s, rnd.Next(0, 40000)); break;
                        case 1: b.Select(Shop.All[rnd.Next(Shop.All.Count)].Id); break;
                        case 2: case 3: b.Press(); break;
                        case 4:
                            ProgressRules.FinishShow(s, new WorldTour.Core.Shows.ShowSummary { Mode = "remix", Venue = "coffee", N = 10, First = rnd.Next(11), Fans = 1000 }, Day);
                            ProgressRules.AwardStickers(s, modes, Day);
                            break;
                        case 5:
                            List<JournalPage> pages = JournalBook.Pages(s);
                            JournalBook.MarkSeen(s, pages[rnd.Next(pages.Count)].Slots.Where(x => x.Fresh).Select(x => x.Key));
                            break;
                        case 6:
                            var keys = JournalBook.Pages(s).SelectMany(p => p.Slots).Where(x => x.Earned).Select(x => x.Key).ToList();
                            if (keys.Count > 0) MetaRules.Place(s, keys[rnd.Next(keys.Count)], rnd.NextDouble(), rnd.NextDouble(), 3);
                            break;
                        case 7:
                            s = SaveJson.Load(SaveJson.ToText(s), Day).Blob;
                            b = new Backstage(s);
                            break;
                    }
                    Assert.GreaterOrEqual(s.Fans.Lifetime, life);
                    Assert.GreaterOrEqual(s.Fans.Balance, 0);
                    foreach (string o in owned) CollectionAssert.Contains(s.Inventory.Owned, o);
                    foreach (var kv in stickers) Assert.AreEqual(kv.Value, s.Journal.Stickers[kv.Key]);
                    foreach (string x in seen) CollectionAssert.Contains(s.Journal.Seen, x);
                    foreach (string x in placed) Assert.IsTrue(s.Journal.Layout.ContainsKey(x));
                }
            }
        }
    }
}
