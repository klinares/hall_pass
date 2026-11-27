using NUnit.Framework;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;

namespace WorldTour.Core.Tests
{
    /// <summary>Phase 10B.1: coins. Earned only by answering, never lost on a miss, taken only by a purchase, fans never spent, an old save's one-time grant.</summary>
    public class CoinRulesTests
    {
        private static SaveBlob Fresh() { return SaveBlob.Defaults("2026-11-26"); }

        [Test]
        public void Earn_FirstTryMost_HintedSome_MissNothing()
        {
            var s = Fresh();
            Assert.AreEqual(4, CoinRules.Earn(s, true, false));
            Assert.AreEqual(2, CoinRules.Earn(s, false, true));
            Assert.AreEqual(0, CoinRules.Earn(s, false, false));
            Assert.AreEqual(6, CoinRules.Coins(s));
            Assert.Greater(CoinRules.FirstTryCoins, CoinRules.HintedCoins);
            Assert.Greater(CoinRules.HintedCoins, 0);
        }

        [Test]
        public void Earn_NeverTouchesFans()
        {
            var s = Fresh();
            s.Fans.Balance = 500; s.Fans.Lifetime = 900;
            CoinRules.Earn(s, true, false);
            Assert.AreEqual(500, s.Fans.Balance);
            Assert.AreEqual(900, s.Fans.Lifetime);
        }

        [Test]
        public void TrySpend_TakesOnlyWhenAffordable_AndNeverGoesBelowZero()
        {
            var s = Fresh();
            s.Fans.Coins = 30;
            Assert.IsFalse(CoinRules.TrySpend(s, 31));
            Assert.AreEqual(30, CoinRules.Coins(s));
            Assert.IsTrue(CoinRules.TrySpend(s, 30));
            Assert.AreEqual(0, CoinRules.Coins(s));
            Assert.IsFalse(CoinRules.TrySpend(s, 1));
            Assert.IsFalse(CoinRules.TrySpend(s, -5), "a negative price is not a way to get coins");
            Assert.AreEqual(0, CoinRules.Coins(s));
            Assert.IsTrue(CoinRules.TrySpend(s, 0));
        }

        [Test]
        public void Buy_SpendsCoins_NotFans()
        {
            var s = Fresh();
            s.Fans.Balance = 1000000; s.Fans.Lifetime = 1000000;
            ShopItem it = Shop.ById("guitar:lilac");
            BuyResult none = MetaRules.Buy(s, it.Id);
            Assert.IsFalse(none.Ok);
            Assert.AreEqual(BuyReason.Short, none.Reason, "fans can't pay for it");
            Assert.AreEqual(CoinRules.PriceOf(it), none.Need);
            s.Fans.Coins = 100;
            BuyResult ok = MetaRules.Buy(s, it.Id);
            Assert.IsTrue(ok.Ok);
            Assert.AreEqual(100 - CoinRules.PriceOf(it), CoinRules.Coins(s));
            Assert.AreEqual(1000000, s.Fans.Balance);
            Assert.AreEqual(1000000, s.Fans.Lifetime);
            Assert.IsTrue(MetaRules.Owns(s, it.Id));
        }

        [Test]
        public void OldShopPrices_AreSmallCoinPrices()
        {
            foreach (ShopItem it in Shop.All)
            {
                int p = CoinRules.PriceOf(it);
                if (it.Price == 0) Assert.AreEqual(0, p, it.Id);
                else { Assert.GreaterOrEqual(p, 10, it.Id); Assert.LessOrEqual(p, 130, it.Id); }
            }
        }

        [Test]
        public void Grant_IsInProportionToFans_AndHappensOnce()
        {
            var s = Fresh();
            s.Fans.Lifetime = 100000;
            Assert.AreEqual(200, CoinRules.GrantFromFans(s));
            Assert.AreEqual(200, CoinRules.Coins(s));
            Assert.IsTrue(s.Fans.CoinsGranted);
            Assert.AreEqual(0, CoinRules.GrantFromFans(s));
            s.Fans.Lifetime = 400000;   // more fans later do not trigger it again
            Assert.AreEqual(0, CoinRules.GrantFromFans(s));
            Assert.AreEqual(200, CoinRules.Coins(s));
        }

        [Test]
        public void Grant_IsCapped_AndNewSavesGetNothing_ButAreMarked()
        {
            var big = Fresh();
            big.Fans.Lifetime = 50000000;
            Assert.AreEqual(CoinRules.GrantCap, CoinRules.GrantFromFans(big));
            var fresh = Fresh();
            Assert.AreEqual(0, CoinRules.GrantFromFans(fresh));
            Assert.IsTrue(fresh.Fans.CoinsGranted);
            fresh.Fans.Lifetime = 90000;   // fans earned after coins existed never grant coins
            Assert.AreEqual(0, CoinRules.GrantFromFans(fresh));
        }

        [Test]
        public void Coins_AndTheGrantFlag_SurviveSaveAndReload_AndAnOldSaveTextIsUnchanged()
        {
            var s = Fresh();
            StringAssert.DoesNotContain("coins", SaveJson.ToText(s), "no coins, no new keys");
            s.Fans.Lifetime = 60000;
            CoinRules.GrantFromFans(s);
            CoinRules.Earn(s, true, false);
            SaveBlob back = SaveJson.Load(SaveJson.ToText(s), "2026-11-27").Blob;
            Assert.AreEqual(CoinRules.Coins(s), CoinRules.Coins(back));
            Assert.IsTrue(back.Fans.CoinsGranted);
            Assert.AreEqual(0, CoinRules.GrantFromFans(back), "a reload never grants again");
            Assert.AreEqual(SaveJson.ToCanonical(s), SaveJson.ToCanonical(back));
            // an old save (no coins keys) loads with no coins and not yet granted
            SaveBlob old = SaveJson.Load("{\"v\":2,\"fans\":{\"balance\":10,\"lifetime\":45000}}", "2026-11-27").Blob;
            Assert.AreEqual(0, CoinRules.Coins(old));
            Assert.IsFalse(old.Fans.CoinsGranted);
            Assert.AreEqual(90, CoinRules.GrantFromFans(old));
        }

        [Test]
        public void ASession_BuysASmallItem_AndABigOneTakesTwoOrThree()
        {
            // a normal 15 minute session: about 50 answers, 7 in 10 first try, 2 in 10 hinted, 1 in 10 a miss
            long perSession = 35 * CoinRules.Amount(true, false) + 10 * CoinRules.Amount(false, true);
            Assert.GreaterOrEqual(perSession, 100, "one session buys a small item (20 to 40 coins)");
            Assert.LessOrEqual(perSession, 200);
            foreach (int big in new[] { 200, 350 })
                Assert.That(System.Math.Ceiling(big / (double)perSession), Is.InRange(2, 3), "a big item of " + big + " takes two or three sessions");
        }
    }
}
