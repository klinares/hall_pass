using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Data;
using WorldTour.Core.Maths;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;
using WorldTour.Core.Shows;

namespace WorldTour.Core.Tests
{
    /// <summary>
    /// Core/Meta beyond the JS test file: the learning rule "nothing she has earned is ever taken away" checked over
    /// many random backstage and journal sessions, the data tables against the venue table and the house voice,
    /// the JS quirks the port keeps on purpose, and the two JS number helpers.
    /// </summary>
    public class MetaRuleTests
    {
        private static readonly IReadOnlyList<ModeInfo> Modes = MetaTests.Modes;

        /* ---------- learning rules ---------- */

        private sealed class Snapshot
        {
            public long Balance, Lifetime;
            public List<string> Owned;
            public Dictionary<string, string> Stickers;
            public HashSet<string> LayoutKeys;

            public static Snapshot Of(SaveBlob s)
            {
                return new Snapshot
                {
                    Balance = s.Fans.Balance, Lifetime = s.Fans.Lifetime, Owned = s.Inventory.Owned.ToList(),
                    Stickers = new Dictionary<string, string>(s.Journal.Stickers), LayoutKeys = new HashSet<string>(s.Journal.Layout.Keys),
                };
            }
        }

        /// <summary>
        /// 2,000 random sessions of buy, equip, earn and place from random saves: lifetime fans never move, the balance
        /// drops only by the price of a successful purchase and never below zero, owned items and earned stickers
        /// (with their dates) are never removed, and placed stickers stay placed.
        /// </summary>
        [Test]
        public void NothingEarnedIsEverTakenAway_OverRandomSessions()
        {
            string[] ids = Shop.All.Select(i => i.Id).Concat(new[] { "nope:x" }).ToArray();
            string[] keys = Milestones.All.Select(m => m.Id).Concat(new[] { "sticker:pick", "loose" }).ToArray();
            int buys = 0, shorts = 0, earned = 0;
            for (int seed = 1; seed <= 2000; seed++)
            {
                var r = new Rng(seed);
                SaveBlob s = MetaTests.Fresh();
                s.Fans.Balance = r.Int(0, 120) * 1000 + r.Int(0, 999);
                s.Fans.Lifetime = s.Fans.Balance + r.Int(0, 900) * 1000;
                s.Streak.Best = r.Int(0, 12);
                for (int j = r.Int(0, 12); j > 0; j--)
                    s.Shows.Add(new ShowRecord { D = "2026-11-18", Mode = r.Pick(Modes).Id, N = 10, First = r.Int(0, 10), Encore = r.Chance(.2) });
                foreach (Venue v in Venues.All) if (r.Chance(.3) && !s.Venues.Unlocked.Contains(v.Id)) s.Venues.Unlocked.Add(v.Id);
                for (int op = r.Int(5, 40); op > 0; op--)
                {
                    Snapshot before = Snapshot.Of(s);
                    int kind = r.Int(0, 3);
                    string what = "seed " + seed + " op " + kind;
                    long spent = 0;
                    if (kind == 0)
                    {
                        string id = r.Pick(ids);
                        BuyResult res = MetaRules.Buy(s, id);
                        if (res.Ok) { buys++; spent = Shop.ById(id).Price; Assert.AreEqual(before.Owned.Count + 1, s.Inventory.Owned.Count, what); }
                        else Assert.AreEqual(before.Owned.Count, s.Inventory.Owned.Count, what);
                        if (res.Reason == BuyReason.Short) { shorts++; Assert.Greater(res.Need, 0, what); }
                    }
                    else if (kind == 1) MetaRules.Equip(s, r.Pick(ids));
                    else if (kind == 2) earned += MetaRules.Earn(s, Modes, "2026-11-" + r.Int(10, 28)).Count;
                    else MetaRules.Place(s, r.Pick(keys), r.Int(-50, 150) / 100.0, r.Int(-50, 150) / 100.0, r.Int(-9, 9));

                    Assert.AreEqual(before.Lifetime, s.Fans.Lifetime, what + ": lifetime fans moved");
                    Assert.AreEqual(before.Balance - spent, s.Fans.Balance, what + ": balance changed by more than a price");
                    Assert.GreaterOrEqual(s.Fans.Balance, 0, what + ": balance went negative");
                    CollectionAssert.AreEqual(before.Owned, s.Inventory.Owned.Take(before.Owned.Count).ToList(), what + ": an owned item was lost");
                    foreach (KeyValuePair<string, string> e in before.Stickers)
                        Assert.AreEqual(e.Value, s.Journal.Stickers[e.Key], what + ": sticker " + e.Key + " was lost or re-dated");
                    Assert.IsTrue(before.LayoutKeys.IsSubsetOf(s.Journal.Layout.Keys), what + ": a placed sticker was lost");
                }
            }
            /* the sessions really exercised purchases, refusals and new stickers */
            Assert.Greater(buys, 1000); Assert.Greater(shorts, 1000); Assert.Greater(earned, 1000);
        }

        [Test]
        public void Buy_AtExactlyTheBalance_LeavesZero_AndOneFanShortIsRefused()
        {
            SaveBlob s = MetaTests.Fresh(); s.Fans.Balance = 12000; s.Fans.Lifetime = 12000;
            Assert.IsTrue(MetaRules.Buy(s, "sticker:pass").Ok);
            Assert.AreEqual(0, s.Fans.Balance); Assert.AreEqual(12000, s.Fans.Lifetime);
            s.Fans.Balance = 4999;
            BuyResult r = MetaRules.Buy(s, "sticker:pick");
            Assert.AreEqual(BuyReason.Short, r.Reason); Assert.AreEqual(1, r.Need); Assert.AreEqual(4999, s.Fans.Balance);
        }

        [Test]
        public void Refusals_ChangeNothing()
        {
            SaveBlob s = MetaTests.Fresh(); s.Fans.Balance = 3000;
            var owned = s.Inventory.Owned.ToList();
            foreach (string id in new[] { "nope:x", "dog:none", "guitar:cherry", "" })
            {
                Assert.IsFalse(MetaRules.Buy(s, id).Ok, id);
                Assert.AreEqual(3000, s.Fans.Balance); CollectionAssert.AreEqual(owned, s.Inventory.Owned);
            }
            var eq = new Dictionary<string, string>(s.Inventory.Equipped);
            foreach (string id in new[] { "nope:x", "guitar:cherry", "sticker:pick", "theme:" }) Assert.IsFalse(MetaRules.Equip(s, id), id);
            CollectionAssert.AreEquivalent(eq, s.Inventory.Equipped);
        }

        /* ---------- data ---------- */

        [Test]
        public void VenueMilestones_MatchTheVenueTable()
        {
            var venueStickers = Milestones.All.Where(m => m.Id.StartsWith("venue-", StringComparison.Ordinal)).ToList();
            var later = Venues.All.Skip(1).ToList();
            Assert.AreEqual(later.Count, venueStickers.Count, "one sticker per venue after the first");
            for (int i = 0; i < later.Count; i++)
            {
                Assert.AreEqual("venue-" + later[i].Id, venueStickers[i].Id);
                Assert.AreEqual("venue:" + later[i].Art, venueStickers[i].Art);
                Assert.AreEqual(later[i].Short, venueStickers[i].Name, "sticker name is the venue's short name");
            }
            Assert.AreEqual("unlock the record store", Milestones.ById("venue-records").How);
            Assert.AreEqual("unlock the club", Milestones.ById("venue-club").How);
            Assert.AreEqual("unlock the arena", Milestones.ById("venue-arena").How);
        }

        [Test]
        public void Milestones_UniqueIdsAndArt_TwentyInAll()
        {
            Assert.AreEqual(20, Milestones.All.Count);
            Assert.AreEqual(Milestones.All.Count, Milestones.All.Select(m => m.Id).Distinct().Count());
            Assert.AreEqual(Milestones.All.Count, Milestones.All.Select(m => m.Art).Distinct().Count());
            Assert.AreSame(Milestones.All[3], Milestones.ById("streak-3"));
            Assert.IsNull(Milestones.ById("nope"));
            Assert.AreEqual(10, Milestones.MasterAt3);
        }

        [Test]
        public void Catalog_PricesAndLookups_MatchShopJs()
        {
            Assert.AreEqual(21, Shop.All.Count);
            Assert.AreEqual(60000, Shop.ById("theme:peach-navy").Price);
            Assert.AreEqual(35000, Shop.ById("dog:collar").Price);
            Assert.AreEqual(12000, Shop.ById("sticker:pass").Price);
            Assert.AreEqual(4, Shop.ById("theme:tour-pink").Swatch.Count);
            Assert.AreEqual("#2f2fc1", Shop.ById("guitar:midnight").Color);
            Assert.AreEqual("crate", Shop.ById("sticker:crate").Sticker);
            Assert.IsNull(Shop.ById("nope:x"));
            CollectionAssert.AreEqual(new[] { "theme", "guitar", "dog", "sticker" }, Shop.Slots.Select(sl => sl.Id).ToList());
            Assert.AreEqual("riff" + (char)0x2019 + "s outfits", Shop.Slots[2].Name);
            Assert.AreEqual("with a pompom. it" + (char)0x2019 + "s cold on the bus.", Shop.ById("dog:beanie").Blurb);
        }

        /// <summary>The house voice is all lowercase; every player-facing string in the tables keeps it.</summary>
        [Test]
        public void EveryShownString_IsLowercase()
        {
            var text = new List<string>();
            foreach (ShopItem i in Shop.All) { text.Add(i.Name); text.Add(i.Blurb); }
            foreach (ShopSlot sl in Shop.Slots) { text.Add(sl.Name); text.Add(sl.Note); }
            foreach (Milestone m in Milestones.All) { text.Add(m.Name); text.Add(m.How); }
            text.AddRange(Office.MistakeLabels.Values);
            foreach (string t in text) Assert.AreEqual(t.ToLowerInvariant(), t, t);
        }

        [Test]
        public void MistakeLabels_NonAsciiCopyIsExact()
        {
            Assert.AreEqual(38, Office.MistakeLabels.Count);
            Assert.AreEqual("read 10" + (char)0x00B3 + " as 10 " + (char)0x00D7 + " 3", Office.MistakeLabels["multiplied-exponent"]);
            Assert.AreEqual("wrote 10" + (char)0x00B3 + " as 103", Office.MistakeLabels["wrote-digits"]);
            Assert.AreEqual("didn" + (char)0x2019 + "t fill the measure", Office.MistakeLabels["too-short"]);
        }

        /* ---------- JS quirks kept on purpose ---------- */

        [Test]
        public void Quirk_EveryMode_CountsEncores()
        {
            SaveBlob s = MetaTests.Fresh();
            foreach (ModeInfo m in Modes) s.Shows.Add(new ShowRecord { Mode = m.Id, N = 3, First = 3, Encore = true });
            List<string> got = MetaRules.Earn(s, Modes, "d").Select(m => m.Id).ToList();
            CollectionAssert.AreEqual(new[] { "every-mode" }, got, "milestones.js reads s.shows, not showsDone, for every-mode");
        }

        [Test]
        public void Quirk_AnEmptyStickerDate_CountsAsNotEarned_AndIsRedated()
        {
            SaveBlob s = MetaTests.Fresh();
            s.Journal.Stickers["streak-3"] = "";
            s.Streak.Best = 3;
            Assert.IsFalse(MetaRules.PageItems(s).Any(i => i.Key == "streak-3"));
            Assert.IsTrue(MetaRules.ToEarn(s).Any(m => m.Id == "streak-3"));
            CollectionAssert.AreEqual(new[] { "streak-3" }, MetaRules.Earn(s, Modes, "2026-11-20").Select(m => m.Id).ToList());
            Assert.AreEqual("2026-11-20", s.Journal.Stickers["streak-3"]);
        }

        [Test]
        public void Quirk_OfficeStickerCount_CountsEveryKey()
        {
            SaveBlob s = MetaTests.Fresh();
            s.Journal.Stickers["first-show"] = "d"; s.Journal.Stickers["old-thing"] = "d"; s.Journal.Stickers["streak-3"] = "";
            OfficeReport o = Office.Build(s, Modes, "2026-11-18");
            Assert.AreEqual(3, o.Stickers); Assert.AreEqual(20, o.StickersTotal);
        }

        [Test]
        public void FreeItems_AreOwnedEvenWhenMissingFromTheList_AndCanBeEquipped()
        {
            SaveBlob s = MetaTests.Fresh();
            s.Inventory.Owned.Clear();
            Assert.IsTrue(MetaRules.Owns(s, "guitar:classic"));
            Assert.AreEqual(BuyReason.Owned, MetaRules.Buy(s, "guitar:classic").Reason);
            Assert.IsTrue(MetaRules.Equip(s, "dog:none"));
            s.Inventory.Equipped.Remove("theme");             /* a slot with nothing saved */
            Assert.AreEqual("theme:undefined", MetaRules.EquippedId(s, "theme"));
            Assert.AreEqual("tour-pink", MetaRules.GetLook(s).Theme);
        }

        [Test]
        public void Place_FirstTimeUsesTheGivenTilt_SpotOfPrefersHerSpot()
        {
            SaveBlob s = MetaTests.Fresh();
            Spot d = MetaRules.SpotOf(s, "sticker:pick", 0);
            MetaTests.AssertSpot(.07, .075, -7, d);     /* JS defaultSpot(0): x .1 - .03, y .09 - .015 */
            MetaRules.Place(s, "sticker:pick", .12345, .9999, 4);
            MetaTests.AssertSpot(.123, .95, 4, s.Journal.Layout["sticker:pick"]);
            Assert.AreSame(s.Journal.Layout["sticker:pick"], MetaRules.SpotOf(s, "sticker:pick", 0));
        }

        [Test]
        public void DefaultSpots_AreExactThreeDecimalDoubles_AndOnThePage()
        {
            foreach (int cols in new[] { 3, 4, 5, 6 })
                for (int i = 0; i < 200; i++)
                {
                    Spot p = MetaRules.DefaultSpot(i, cols);
                    Assert.AreEqual(Math.Round(p.X * 1000) / 1000.0, p.X, 0.0);
                    Assert.AreEqual(Math.Round(p.Y * 1000) / 1000.0, p.Y, 0.0);
                    Assert.IsTrue(p.X > 0 && p.X < 1 && p.Y > 0 && p.Y <= .94, cols + "/" + i);
                    Assert.AreEqual(Math.Round(p.R), p.R, 0.0);
                }
        }

        [Test]
        public void Office_TodayMustBeADay_UnlessNoDaysAreAsked()
        {
            SaveBlob s = MetaTests.Fresh();
            Assert.Throws<ArgumentException>(() => Office.Build(s, Modes, "soon"));
            Assert.AreEqual(0, Office.Build(s, Modes, "soon", 0).Daily.Count);
            Assert.AreEqual("2026-02-28", Office.Build(s, Modes, "2026-03-01", 2).Daily[0].D);
        }

        [Test]
        public void Office_MistakeFromASkillNoModeUses_ShowsTheSkillKey()
        {
            SaveBlob s = MetaTests.Fresh();
            var k = new SkillRecord(); k.Mistakes["swapped"] = 2; s.Skills["xy"] = k;
            OfficeReport o = Office.Build(s, Modes, "2026-11-18");
            Assert.AreEqual("xy", o.Mistakes.Single().Mode);
        }

        [Test]
        public void Mins_EdgeCases()
        {
            Assert.AreEqual("0 min", Office.Mins(0)); Assert.AreEqual("<1 min", Office.Mins(59));
            Assert.AreEqual("1 min", Office.Mins(60)); Assert.AreEqual("2 min", Office.Mins(90)); Assert.AreEqual("60 min", Office.Mins(3600));
        }

        /* ---------- JS number helpers ---------- */

        [Test]
        public void JsRound_MatchesMathRound()
        {
            Assert.AreEqual(0.0, JsNum.Round(0.49999999999999994));
            Assert.AreEqual(-2.0, JsNum.Round(-2.5));
            Assert.AreEqual(3.0, JsNum.Round(2.5));
            Assert.AreEqual(0.0, JsNum.Round(-0.3));
            Assert.AreEqual(2.0, JsNum.Round(1.5));
        }

        /// <summary>Expected values printed by Node: <c>+v.toFixed(3)</c>.</summary>
        [TestCase(1.0005, 1.0)]
        [TestCase(1.0045, 1.004)]
        [TestCase(0.0005, 0.001)]
        [TestCase(0.1235, 0.123)]
        [TestCase(0.9995, 1.0)]
        [TestCase(2.675, 2.675)]
        [TestCase(0.0015, 0.002)]
        [TestCase(123.4565, 123.457)]
        public void JsToFixed_MatchesNode(double v, double want)
        {
            Assert.AreEqual(want, JsNum.ToFixedNumber(v, 3), 0.0);
        }

        [Test]
        public void JsToFixed_NegativeTinyIsNegativeZero()
        {
            double z = JsNum.ToFixedNumber(-0.0001, 3);
            Assert.AreEqual(0.0, z, 0.0);
            Assert.IsTrue(double.IsNegative(z) || 1 / z < 0);
        }
    }
}
