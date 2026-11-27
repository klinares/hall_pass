using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;

namespace WorldTour.Core.Tests
{
    /// <summary>Phase 10B.1: the boutique's rules. Buying takes coins and never fans, nothing owned is lost, every store line is short and lower case.</summary>
    public class BoutiqueRulesTests
    {
        private static SaveBlob Fresh() { return SaveBlob.Defaults("2026-11-26"); }

        [Test]
        public void Buy_TakesCoins_GrantsTheItem_PutsItOn_AndLeavesFansAlone()
        {
            var s = Fresh();
            s.Fans.Balance = 777; s.Fans.Lifetime = 9000; s.Fans.Coins = 100;
            AvatarItem hoodie = AvatarCatalogue.ById("top:hoodie");
            Assert.AreEqual(ItemState.CanBuy, BoutiqueRules.StateOf(s, hoodie));
            BuyResult r = BoutiqueRules.Buy(s, hoodie.Id, AvatarCatalogue.Navy, 1);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(100 - hoodie.Price, CoinRules.Coins(s));
            Assert.IsTrue(AvatarRules.Owns(s, hoodie.Id));
            Assert.AreEqual(ItemState.Wearing, BoutiqueRules.StateOf(s, hoodie));
            AvatarPick worn = AvatarRules.Look(s).Top;
            Assert.AreEqual("top:hoodie", worn.Id);
            Assert.AreEqual(AvatarCatalogue.Navy, worn.Color);
            Assert.AreEqual(1, worn.Pattern);
            Assert.AreEqual(777, s.Fans.Balance);
            Assert.AreEqual(9000, s.Fans.Lifetime);
        }

        [Test]
        public void Buy_WithTooFewCoins_ChangesNothing_AndSaysHowManyMore()
        {
            var s = Fresh();
            s.Fans.Coins = 10; s.Fans.Lifetime = 5000000; s.Fans.Balance = 5000000;   // lots of fans, few coins
            AvatarItem it = AvatarCatalogue.ById("top:bomber");
            Assert.AreEqual(ItemState.TooMuch, BoutiqueRules.StateOf(s, it));
            BuyResult r = BoutiqueRules.Buy(s, it.Id, 0, 0);
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(BuyReason.Short, r.Reason);
            Assert.AreEqual(it.Price - 10, r.Need);
            Assert.AreEqual(10, CoinRules.Coins(s));
            Assert.IsFalse(AvatarRules.Owns(s, it.Id));
        }

        [Test]
        public void Buy_UnknownOrOwned_ChangesNothing()
        {
            var s = Fresh();
            s.Fans.Coins = 500;
            Assert.AreEqual(BuyReason.Unknown, BoutiqueRules.Buy(s, "top:nothing", 0, 0).Reason);
            Assert.AreEqual(BuyReason.Owned, BoutiqueRules.Buy(s, "top:tee", 0, 0).Reason, "a starter is already hers");
            Assert.AreEqual(500, CoinRules.Coins(s));
            BoutiqueRules.Buy(s, "top:tank", 0, 0);
            long after = CoinRules.Coins(s);
            Assert.AreEqual(BuyReason.Owned, BoutiqueRules.Buy(s, "top:tank", 0, 0).Reason, "never charged twice");
            Assert.AreEqual(after, CoinRules.Coins(s));
        }

        [Test]
        public void WhatSheBought_StaysHers_WhateverSheWearsNext()
        {
            var s = Fresh();
            s.Fans.Coins = 500;
            BoutiqueRules.Buy(s, "top:hoodie", 3, 0);
            BoutiqueRules.Buy(s, "shoes:sandals", 4, 0);
            Assert.IsTrue(BoutiqueRules.WearOwned(s, "top:tee", 0, 0));
            Assert.IsTrue(AvatarRules.Owns(s, "top:hoodie"));
            Assert.AreEqual(ItemState.Yours, BoutiqueRules.StateOf(s, AvatarCatalogue.ById("top:hoodie")));
            Assert.IsTrue(AvatarRules.Owns(s, "shoes:sandals"));
            Assert.IsFalse(BoutiqueRules.WearOwned(s, "top:puffer", 0, 0), "not hers, so it can't be put on for free");
        }

        [Test]
        public void TryLook_ChangesOnlyThatSlot_AndNeverTheSave()
        {
            var s = Fresh();
            AvatarLook before = AvatarRules.Look(s);
            AvatarLook tried = BoutiqueRules.TryLook(s, AvatarCatalogue.ById("hat:crown"), AvatarCatalogue.Peach, 0);
            Assert.AreEqual("hat:crown", tried.Hat.Id);
            Assert.AreEqual(before.Top.Id, tried.Top.Id);
            Assert.AreEqual(before.Shoes.Id, tried.Shoes.Id);
            Assert.AreEqual(before.Hair.Id, tried.Hair.Id);
            Assert.IsTrue(before.Same(AvatarRules.Look(s)), "trying on does not change what she wears");
            Assert.IsFalse(AvatarRules.Owns(s, "hat:crown"));
        }

        [Test]
        public void EveryRack_HasItems_AndRacksAreTheClothingSlots()
        {
            foreach (string slot in BoutiqueRules.RackSlots)
            {
                Assert.Greater(BoutiqueRules.Rack(slot).Count, 4, slot);
                StringAssert.DoesNotContain("  ", BoutiqueRules.RackName(slot));
            }
            Assert.AreEqual("tops", BoutiqueRules.RackName("top"));
            Assert.AreEqual("hats", BoutiqueRules.RackName("hat"));
            CollectionAssert.DoesNotContain(BoutiqueRules.RackSlots, AvatarCatalogue.Dog);
            CollectionAssert.DoesNotContain(BoutiqueRules.RackSlots, AvatarCatalogue.Guitar);
        }

        [Test]
        public void StoreWords_AreShort_LowerCase_AndPlain()
        {
            var s = Fresh();
            foreach (string line in BoutiqueRules.AllLines(s))
            {
                int words = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries).Length;
                Assert.LessOrEqual(words, 12, line);
                Assert.AreEqual(line.ToLowerInvariant(), line, "lower case: " + line);
            }
            foreach (string label in new[] { BoutiqueRules.DoorGoIn, BoutiqueRules.DoorStay, BoutiqueRules.LeaveGo, BoutiqueRules.LeaveStay, BoutiqueRules.Look, BoutiqueRules.OtherRack,
                BoutiqueRules.Done, BoutiqueRules.Before, BoutiqueRules.Next, BoutiqueRules.Colour, BoutiqueRules.Back, BoutiqueRules.WearIt, BoutiqueRules.OnNow })
                Assert.AreEqual(label.ToLowerInvariant(), label);
        }

        [Test]
        public void ItemNames_AreLowerCase()
        {
            foreach (string slot in BoutiqueRules.RackSlots)
                foreach (AvatarItem it in BoutiqueRules.Rack(slot))
                    Assert.AreEqual(it.Name.ToLowerInvariant(), it.Name, it.Id);
        }

        [Test]
        public void Price_SaysCoinsInPlainWords()
        {
            Assert.AreEqual("1 coin", BoutiqueRules.Price(1));
            Assert.AreEqual("30 coins", BoutiqueRules.Price(30));
            Assert.AreEqual("buy 30 coins", BoutiqueRules.BuyLabel(30));
            Assert.AreEqual("need 5 more", BoutiqueRules.NeedLabel(5));
        }
    }
}
