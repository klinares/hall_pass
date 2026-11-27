using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;

namespace WorldTour.Core.Tests
{
    /// <summary>Phase 8.3: the items she can see in the 3D world (outfits, hair, stage lights), next to the 2D catalog that stays as it was.</summary>
    public class WardrobeShopTests
    {
        const string Day = "2026-11-24";

        [Test]
        public void WorldCatalog_HasTheNewSlots_FreeDefaults_AndPricesOnTheExistingScale()
        {
            Assert.AreEqual(21, Shop.All.Count, "the 2D catalog is untouched");
            foreach (string slot in new[] { "outfit", "hair", "lights" })
            {
                Assert.AreEqual(1, Shop.World.Count(i => i.Slot == slot && i.Price == 0), slot + " has exactly one free item");
                Assert.IsNotNull(Shop.FreeIn(slot));
            }
            Assert.That(Shop.World.Count(i => i.Slot == "outfit"), Is.InRange(5, 7), "4 to 6 outfits to buy, plus the free one");
            Assert.That(Shop.World.Count(i => i.Slot == "hair"), Is.InRange(5, 6));
            Assert.That(Shop.World.Count(i => i.Slot == "lights"), Is.InRange(5, 6));
            Assert.AreEqual(Shop.Every.Count(), Shop.Every.Select(i => i.Id).Distinct().Count());
            foreach (ShopItem i in Shop.World)
            {
                Assert.IsTrue(i.Id.StartsWith(i.Slot + ":"), i.Id);
                Assert.That(i.Price == 0 || (i.Price >= 5000 && i.Price <= 35000), i.Id + " price " + i.Price);
                Assert.AreEqual(i.Name.ToLowerInvariant(), i.Name); Assert.AreEqual(i.Blurb.ToLowerInvariant(), i.Blurb);
                if (i.Slot == "outfit") Assert.AreEqual(3, i.Swatch.Count, i.Id);
                else Assert.That(i.Color, Does.Match("^#[0-9a-f]{6}$"), i.Id);
                Assert.AreSame(i, Shop.ById(i.Id));
            }
            Assert.AreEqual(5000, Shop.ById("outfit:peach-navy").Price, "peach and navy is cheap and early");
            Assert.LessOrEqual(Shop.ById("outfit:peach-navy").Price, 6000);
        }

        [Test]
        public void WorldShelves_AreNotOnThe2DBackstage_AndEveryShelfHasItems()
        {
            CollectionAssert.AreEqual(new[] { "theme", "guitar", "dog", "sticker" }, Shop.Slots.Select(s => s.Id).ToList());
            foreach (ShopSlot sl in Shop.WorldSlots) Assert.IsTrue(Shop.Every.Any(i => i.Slot == sl.Id), sl.Id);
            foreach (ShopSlot sl in Shop.WorldSlots) { Assert.AreEqual(sl.Name.ToLowerInvariant(), sl.Name); Assert.AreEqual(sl.Note.ToLowerInvariant(), sl.Note); }
            Assert.IsFalse(Shop.All.Any(i => i.Slot == "outfit" || i.Slot == "hair" || i.Slot == "lights"));
        }

        [Test]
        public void Defaults_AreOwnedAndWorn_ASaveWithNoWorldKeysLooksTheSame()
        {
            SaveBlob s = SaveBlob.Defaults(Day);
            WardrobeLook l = WardrobeLook.Of(s);
            Assert.AreEqual("outfit:tour", l.Outfit); Assert.AreEqual("hair:chestnut", l.Hair); Assert.AreEqual("lights:tour", l.Lights);
            Assert.AreEqual("guitar:classic", l.Guitar); Assert.AreEqual("dog:none", l.Dog);
            Assert.IsTrue(MetaRules.Owns(s, "outfit:tour"));
            Assert.IsFalse(MetaRules.Owns(s, "outfit:cherry"));
        }

        [Test]
        public void BuyEquip_SpendsOnlyTheBalance_AndTheLookFollows()
        {
            SaveBlob s = SaveBlob.Defaults(Day); s.Fans.Balance = 20000; s.Fans.Lifetime = 20000; s.Fans.Coins = 40;   // 10B.1: coins pay (the old price over 500); fans are never spent
            Assert.IsTrue(MetaRules.Buy(s, "outfit:peach-navy").Ok);
            Assert.AreEqual(30, s.Fans.Coins); Assert.AreEqual(20000, s.Fans.Balance); Assert.AreEqual(20000, s.Fans.Lifetime);
            Assert.IsFalse(MetaRules.Equip(s, "outfit:cherry"), "not owned");
            Assert.IsTrue(MetaRules.Equip(s, "outfit:peach-navy"));
            Assert.IsTrue(MetaRules.Buy(s, "hair:blonde").Ok); Assert.IsTrue(MetaRules.Equip(s, "hair:blonde"));
            Assert.IsTrue(MetaRules.Buy(s, "lights:pink").Ok); Assert.IsTrue(MetaRules.Equip(s, "lights:pink"));
            WardrobeLook l = WardrobeLook.Of(s);
            Assert.AreEqual("outfit:peach-navy", l.Outfit); Assert.AreEqual("hair:blonde", l.Hair); Assert.AreEqual("lights:pink", l.Lights);
            Assert.AreEqual(BuyReason.Short, MetaRules.Buy(s, "outfit:starlight").Reason);
            Assert.AreEqual(70 - s.Fans.Coins, MetaRules.Buy(s, "outfit:starlight").Need);
            Assert.AreEqual(BuyReason.Owned, MetaRules.Buy(s, "outfit:peach-navy").Reason);
        }

        [Test]
        public void TryOn_ChangesOnlyItsSlot_AndLeavesTheSaveAlone()
        {
            SaveBlob s = SaveBlob.Defaults(Day); s.Fans.Balance = 9000;
            string before = SaveJson.ToText(s);
            WardrobeLook l = WardrobeLook.Of(s, "outfit:starlight");
            Assert.AreEqual("outfit:starlight", l.Outfit); Assert.AreEqual("hair:chestnut", l.Hair);
            l = WardrobeLook.Of(s, "dog:shades");
            Assert.AreEqual("dog:shades", l.Dog); Assert.AreEqual("outfit:tour", l.Outfit);
            Assert.AreEqual("outfit:tour", WardrobeLook.Of(s, "theme:peach-navy").Outfit, "a theme is not part of the 3D look");
            Assert.AreEqual("outfit:tour", WardrobeLook.Of(s, "nope:x").Outfit);
            Assert.AreEqual(before, SaveJson.ToText(s), "leaving without buying reverts: nothing was written");
        }

        [Test]
        public void ShopModel_WorksOnWorldItems_TwoTapBuy_StartsOnTheWornOutfit()
        {
            SaveBlob s = SaveBlob.Defaults(Day); s.Fans.Balance = 7000; s.Fans.Lifetime = 7000; s.Fans.Coins = 14;
            var b = new Backstage(s, Shop.Outfit);
            Assert.AreEqual("outfit:tour", b.Selected);
            b.Select("outfit:peach-navy");
            Mirror m = b.Mirror();
            Assert.AreEqual("trying on", m.Eyebrow); Assert.AreEqual("buy for 10 coins", m.Action); Assert.AreEqual(MirrorAction.Buy, m.Does);
            Assert.AreEqual(PressOutcome.Armed, b.Press());
            Assert.AreEqual(14, s.Fans.Coins, "a stray tap never spends");
            Assert.AreEqual("tap again to buy", b.Mirror().Action);
            Assert.AreEqual(PressOutcome.Bought, b.Press());
            Assert.AreEqual(4, s.Fans.Coins); Assert.AreEqual(7000, s.Fans.Balance); Assert.AreEqual(7000, s.Fans.Lifetime);
            Assert.AreEqual("outfit:peach-navy", WardrobeLook.Of(s).Outfit, "bought items are put on at once");
            Assert.AreEqual("wearing", b.Tag(Shop.ById("outfit:peach-navy")).Tag);
            b.Select("outfit:cherry");
            Assert.AreEqual("46 more coins needed", b.Mirror().Action);
            Assert.AreEqual(PressOutcome.None, b.Press());
            b.Select("outfit:tour");
            Assert.AreEqual(PressOutcome.Equipped, b.Press());
            Assert.AreEqual("outfit:tour", WardrobeLook.Of(s).Outfit);
            Assert.IsTrue(MetaRules.Owns(s, "outfit:peach-navy"), "nothing bought is taken away");
            Assert.AreEqual("dog:none", new Backstage(s).Selected, "the 2D screen still starts on riff");
            Assert.AreEqual("dog:none", new Backstage(s, "nonsense").Selected);
        }

        [Test]
        public void Save_RoundTrips_AndTheOldGameKeepsWhatItDoesNotKnow()
        {
            SaveBlob s = SaveBlob.Defaults(Day); s.Fans.Balance = 90000; s.Fans.Lifetime = 90000; s.Fans.Coins = 180;
            foreach (string id in new[] { "outfit:peach-navy", "hair:lilac", "lights:gold", "guitar:mint", "dog:beanie" }) { Assert.IsTrue(MetaRules.Buy(s, id).Ok, id); Assert.IsTrue(MetaRules.Equip(s, id), id); }
            string text = SaveJson.ToText(s);
            SaveBlob back = SaveJson.Load(text, Day).Blob;
            Assert.AreEqual(WardrobeLook.Of(s).ToString(), WardrobeLook.Of(back).ToString());
            CollectionAssert.AreEquivalent(s.Inventory.Owned, back.Inventory.Owned);
            Assert.AreEqual("peach-navy", back.Inventory.Equipped["outfit"]);
            // the 2D game reads the same save: its look (theme, guitar, riff) is right and a second write keeps every world key
            Look look = MetaRules.GetLook(back);
            Assert.AreEqual("#8fe3c4", look.Guitar); Assert.AreEqual("beanie", look.Outfit); Assert.AreEqual("tour-pink", look.Theme);
            Assert.AreEqual(text, SaveJson.ToText(back), "writing it back changes nothing");
            // the 2D backstage walks Shop.All and Shop.Slots only: every one of its tags still works on this save
            var b2d = new Backstage(back);
            foreach (ShopItem it in Shop.All) Assert.DoesNotThrow(() => b2d.Tag(it));
        }

        [Test]
        public void ASaveFromTheOldGame_LoadsInTheWorld_WithDefaults_AndAnUnknownWornIdFallsBack()
        {
            SaveBlob old = SaveJson.Load("{\"v\":3,\"inventory\":{\"owned\":[\"theme:tour-pink\",\"guitar:classic\",\"dog:none\",\"guitar:cherry\"],\"equipped\":{\"theme\":\"tour-pink\",\"guitar\":\"cherry\",\"dog\":\"none\"}}}", Day).Blob;
            WardrobeLook l = WardrobeLook.Of(old);
            Assert.AreEqual("guitar:cherry", l.Guitar); Assert.AreEqual("outfit:tour", l.Outfit);
            old.Inventory.Equipped["outfit"] = "cherry";           // worn but never bought (edited save)
            old.Inventory.Equipped["hair"] = "rainbow";            // unknown
            l = WardrobeLook.Of(old);
            Assert.AreEqual("outfit:tour", l.Outfit); Assert.AreEqual("hair:chestnut", l.Hair);
        }
    }
}
