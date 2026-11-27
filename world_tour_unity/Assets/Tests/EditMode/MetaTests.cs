using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;
using WorldTour.Core.Shows;

namespace WorldTour.Core.Tests
{
    /// <summary>
    /// Port of world_tour/tests/math/meta.test.mjs, one test per JS test, every assertion kept and in order.
    /// Backstage, journal and office logic (Core/Meta).
    /// </summary>
    public class MetaTests
    {
        /// <summary>Same shape as store.defaults() (the JS test's <c>fresh()</c>).</summary>
        internal static SaveBlob Fresh() { return SaveBlob.Defaults("2026-11-18"); }

        internal static readonly IReadOnlyList<ModeInfo> Modes = new[]
        {
            new ModeInfo("remix", "remix", ModeInfo.Spelling), new ModeInfo("slots", "missing letters", ModeInfo.Spelling),
            new ModeInfo("merch", "merch table", ModeInfo.Math, "dec"), new ModeInfo("beat", "beat maker", ModeInfo.Math, "frac"),
        };

        private static SkillRecord Skill(int level, int atThreeTried, int atThreeRight)
        {
            var k = new SkillRecord { Level = level };
            k.ByLevel[3] = new[] { atThreeTried, atThreeRight };
            return k;
        }

        private static List<string> Ids(IEnumerable<Milestone> ms) { return ms.Select(m => m.Id).ToList(); }

        [Test]
        public void Catalog_UniqueIds_AFreeItemInEveryWearableSlot_SanePrices()
        {
            Assert.AreEqual(Shop.All.Count, Shop.All.Select(i => i.Id).Distinct().Count());
            foreach (ShopSlot sl in Shop.Slots) Assert.IsTrue(Shop.All.Any(i => i.Slot == sl.Id), sl.Id);
            foreach (string sl in new[] { "theme", "guitar", "dog" }) Assert.AreEqual(1, Shop.All.Count(i => i.Slot == sl && i.Price == 0), sl);
            foreach (ShopItem i in Shop.All)
            {
                Assert.IsTrue(i.Id.StartsWith(i.Slot + ":", System.StringComparison.Ordinal));
                Assert.IsTrue(i.Price >= 0);
            }
            foreach (ShopItem i in Shop.All.Where(i => i.Slot == "sticker"))
                Assert.IsFalse(Milestones.All.Any(m => m.Art == i.Sticker), i.Id + " duplicates a milestone sticker");
        }

        [Test]
        public void Buy_SpendsCoinsOnly_NeverFans_AndNeverTwice()
        {
            SaveBlob s = Fresh(); s.Fans.Balance = 20000; s.Fans.Lifetime = 50000; s.Fans.Coins = 40;
            Assert.AreEqual(new BuyResult { Ok = true }, MetaRules.Buy(s, "guitar:sunburst"));
            Assert.AreEqual(4, s.Fans.Coins); Assert.AreEqual(20000, s.Fans.Balance); Assert.AreEqual(50000, s.Fans.Lifetime);
            Assert.AreEqual(BuyReason.Owned, MetaRules.Buy(s, "guitar:sunburst").Reason);
            BuyResult r = MetaRules.Buy(s, "dog:beanie");
            Assert.AreEqual(BuyReason.Short, r.Reason); Assert.AreEqual(36, r.Need); Assert.AreEqual(4, s.Fans.Coins);
            Assert.AreEqual(BuyReason.Unknown, MetaRules.Buy(s, "nope:x").Reason);
            Assert.AreEqual(BuyReason.Owned, MetaRules.Buy(s, "theme:tour-pink").Reason);
        }

        [Test]
        public void Equip_OnlyWhatSheOwns_StickersAreNeverEquipped_LookFallsBackSafely()
        {
            SaveBlob s = Fresh(); s.Fans.Balance = 1000000; s.Fans.Coins = 2000;
            Assert.IsFalse(MetaRules.Equip(s, "dog:shades"));
            MetaRules.Buy(s, "dog:shades"); MetaRules.Buy(s, "theme:peach-navy"); MetaRules.Buy(s, "guitar:mint"); MetaRules.Buy(s, "sticker:pick");
            Assert.IsTrue(MetaRules.Equip(s, "dog:shades") && MetaRules.Equip(s, "theme:peach-navy") && MetaRules.Equip(s, "guitar:mint"));
            Assert.IsFalse(MetaRules.Equip(s, "sticker:pick"));
            Assert.AreEqual(new Look { Theme = "peach-navy", Guitar = "#8fe3c4", Outfit = "shades" }, MetaRules.GetLook(s));
            s.Inventory.Equipped = new Dictionary<string, string> { { "theme", "gone" }, { "guitar", "gone" }, { "dog", "gone" } };
            Assert.AreEqual(new Look { Theme = "tour-pink", Guitar = "var(--paper-deep)", Outfit = "" }, MetaRules.GetLook(s));
            s.Inventory.Equipped["dog"] = "collar";            /* equipped but not owned (hand-edited save) */
            Assert.AreEqual("", MetaRules.GetLook(s).Outfit);
        }

        [Test]
        public void Milestones_EarnedOnce_Kept_AndDated()
        {
            SaveBlob s = Fresh();
            CollectionAssert.IsEmpty(MetaRules.Earn(s, Modes, "2026-11-18"));
            s.Shows.Add(new ShowRecord { D = "2026-11-18", Mode = "remix", N = 10, First = 9, Hinted = 1, Fans = 30000 });
            List<string> got = Ids(MetaRules.Earn(s, Modes, "2026-11-18"));
            got.Sort(System.StringComparer.Ordinal);
            CollectionAssert.AreEqual(new[] { "first-show", "sold-out" }, got);
            Assert.AreEqual("2026-11-18", s.Journal.Stickers["first-show"]);
            CollectionAssert.IsEmpty(MetaRules.Earn(s, Modes, "2026-11-19"));
            s.Shows.Clear();                                   /* history trimmed: stickers stay */
            Assert.AreEqual(0, MetaRules.Earn(s, Modes, "2026-11-19").Count);
            Assert.IsTrue(MetaRules.HasSticker(s, "sold-out"));
        }

        [Test]
        public void Milestones_Encores_Streaks_Levels_EveryMode_Time_Venues()
        {
            SaveBlob s = Fresh();
            s.Shows.Add(new ShowRecord { Mode = "remix", N = 3, First = 3, Encore = true });
            CollectionAssert.IsEmpty(MetaRules.Earn(s, Modes, "d"), "an encore is not a show");
            s.Streak.Best = 5; s.Skills["spell:remix"] = Skill(3, 12, 10); s.Skills["dec"] = Skill(3, 9, 9);
            s.Time = new Dictionary<string, int> { { "a", 1800 }, { "b", 1800 } }; s.Venues.Unlocked.Add("records"); s.Venues.Unlocked.Add("rec");
            foreach (ModeInfo m in Modes) s.Shows.Add(new ShowRecord { Mode = m.Id, N = 10, First = 10 });
            List<string> ids = Ids(MetaRules.Earn(s, Modes, "d"));
            foreach (string id in new[] { "first-show", "sold-out", "clean-run", "streak-3", "streak-5", "master-any", "master-spell", "every-mode", "hour", "venue-records", "venue-rec" })
                Assert.IsTrue(ids.Contains(id), id);
            foreach (string id in new[] { "streak-10", "master-math", "ten-shows", "venue-rooftop" }) Assert.IsFalse(ids.Contains(id), id);
            s.Skills["dec"].ByLevel[3] = new[] { 11, 10 };
            CollectionAssert.AreEqual(new[] { "master-math" }, Ids(MetaRules.Earn(s, Modes, "d")));
        }

        [Test]
        public void JournalPage_EarnedThenBought_SpotsStayOnThePage_PlacingClampsAndKeepsTheTilt()
        {
            SaveBlob s = Fresh(); s.Fans.Balance = 100000; s.Fans.Coins = 200;
            s.Journal.Stickers = new Dictionary<string, string> { { "first-show", "d1" }, { "streak-3", "d2" } }; MetaRules.Buy(s, "sticker:note");
            CollectionAssert.AreEqual(new[] { "first-show", "streak-3", "sticker:note" }, MetaRules.PageItems(s).Select(i => i.Key).ToList());
            Assert.AreEqual(Milestones.All.Count - 2, MetaRules.ToEarn(s).Count);
            for (int i = 0; i < Milestones.All.Count + Shop.All.Count; i++)
            {
                Spot p = MetaRules.DefaultSpot(i);
                Assert.IsTrue(p.X > 0 && p.X < 1 && p.Y > 0 && p.Y < 1, "spot " + i);
            }
            MetaRules.Place(s, "first-show", 1.4, -2, 7);
            AssertSpot(.95, .05, 7, s.Journal.Layout["first-show"]);
            MetaRules.Place(s, "first-show", .5, .5, -3);
            AssertSpot(.5, .5, 7, s.Journal.Layout["first-show"]);
        }

        internal static void AssertSpot(double x, double y, double r, Spot p)
        {
            Assert.AreEqual(x, p.X, 0.0, "x"); Assert.AreEqual(y, p.Y, 0.0, "y"); Assert.AreEqual(r, p.R, 0.0, "r");
        }

        [Test]
        public void Office_Accuracy_MostMissedWords_Mistakes_And14DaysOfTime()
        {
            SaveBlob s = Fresh();
            s.Skills["spell:remix"] = new SkillRecord { Level = 2, A = 10, C = 8, H = 3 };
            var dec = new SkillRecord { Level = 1, A = 4, C = 1, H = 0 };
            dec.Mistakes = new Dictionary<string, int> { { "no-borrow", 3 }, { "gave-total", 1 }, { "made-up", 2 }, { "near", 9 } };
            s.Skills["dec"] = dec;
            s.Words = new Dictionary<string, WordStat>
            {
                { "illegible", new WordStat { A = 4, C = 2, First = 1, H = 1, Miss = 2, Last = "2026-11-17" } },
                { "irregular", new WordStat { A = 3, C = 3, First = 3, H = 0, Miss = 0 } },
                { "intercept", new WordStat { A = 2, C = 1, First = 0, H = 0, Miss = 2, Last = "2026-11-16" } },
            };
            s.Time = new Dictionary<string, int> { { "2026-11-18", 600 }, { "2026-11-05", 300 }, { "2026-10-01", 900 } };
            s.Shows = new List<ShowRecord>
            {
                new ShowRecord { D = "2026-11-18", Mode = "remix", N = 10, First = 8 },
                new ShowRecord { D = "2026-11-18", Mode = "remix", N = 2, First = 2, Encore = true },
            };
            OfficeReport o = Office.Build(s, Modes, "2026-11-18");
            Assert.AreEqual(1, o.Shows); Assert.AreEqual(1800, o.TotalSecs);
            OfficeSkill remix = o.Skills.First(k => k.Id == "remix");
            CollectionAssert.AreEqual(new int?[] { 2, 10, 80, 30, 1 }, new int?[] { remix.Level, remix.A, remix.Acc, remix.Hint, remix.Shows });
            Assert.IsNull(o.Skills.First(k => k.Id == "beat").Acc);
            CollectionAssert.AreEqual(new[] { "intercept", "illegible" }, o.Words.Select(w => w.W).ToList());   /* tie on misses: lower first-try rate first */
            CollectionAssert.AreEqual(new[] { "no-borrow|3|merch table", "made-up|2|merch table", "gave-total|1|merch table" },
                o.Mistakes.Select(m => m.Tag + "|" + m.N + "|" + m.Mode).ToList());
            Assert.AreEqual("subtracted without regrouping (borrowing)", o.Mistakes[0].Label);
            Assert.AreEqual(14, o.Daily.Count);
            Assert.AreEqual("2026-11-05", o.Daily[0].D); Assert.AreEqual("2026-11-18", o.Daily[13].D);
            CollectionAssert.AreEqual(new[] { 300, 600, 1 }, new[] { o.Daily[0].Secs, o.Daily[13].Secs, o.Daily[13].Shows });
        }

        [Test]
        public void EveryMistakeTagTheGeneratorsProduce_HasAPlainLabel()
        {
            var tags = new HashSet<string>();
            foreach (string sk in new[] { "dec", "md", "frac", "xy", "vol", "ops", "flu" })
                for (int lv = 1; lv <= 3; lv++)
                    for (int seed = 1; seed <= 600; seed++)
                    {
                        Problem p = Generators.Generate(sk, lv, seed);
                        foreach (Trap t in p.Traps ?? new List<Trap>()) if (!string.IsNullOrEmpty(t.Why)) tags.Add(t.Why);
                        foreach (Choice c in p.Choices ?? new List<Choice>()) if (!string.IsNullOrEmpty(c.Why)) tags.Add(c.Why);
                    }
            /* the beat maker adds these two from the builder, not from traps */
            tags.Add("too-short"); tags.Add("too-long");
            Assert.IsTrue(tags.Count > 25, "found " + tags.Count);
            tags.Remove("near");      /* filler choices; the office leaves them out */
            foreach (string tg in tags) Assert.IsTrue(Office.MistakeLabels.ContainsKey(tg), "no label for " + tg);
        }
    }
}
