using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Data;
using WorldTour.Core.Maths.Gen;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;

namespace WorldTour.Core.Tests
{
    /// <summary>Phase 10B.1: the avatar catalogue, the rules over the save, the save format (version 3) and the migration of old backstage items.</summary>
    public class AvatarTests
    {
        const string Day = "2026-11-26";

        static int Count(string slot) { return AvatarCatalogue.All.Count(i => i.Slot == slot); }

        // ---------- the catalogue ----------

        [Test]
        public void Catalogue_MeetsTheTargets()
        {
            Assert.GreaterOrEqual(Count("hair"), 12);
            Assert.GreaterOrEqual(Count("top"), 20);
            Assert.GreaterOrEqual(Count("bottom"), 12);
            Assert.GreaterOrEqual(Count("shoes"), 8);
            Assert.GreaterOrEqual(Count("hat") - 1 + Count("glasses") - 1 + Count("bag") - 1, 12, "hats and accessories, not counting none");
            Assert.GreaterOrEqual(Count("hat") - 1, 12);
            Assert.GreaterOrEqual(Count("guitar"), 4);
            Assert.GreaterOrEqual(Count("dog"), 8);
            Assert.AreEqual(24, AvatarCatalogue.PaletteHex.Count);
            Assert.AreEqual(24, AvatarCatalogue.PaletteNames.Count);
            Assert.AreEqual(AvatarCatalogue.PaletteHex.Count, AvatarCatalogue.PaletteHex.Distinct().Count());
            Assert.AreEqual("peach", AvatarCatalogue.PaletteNames[AvatarCatalogue.Peach]);
            Assert.AreEqual("navy", AvatarCatalogue.PaletteNames[AvatarCatalogue.Navy]);
            Assert.IsTrue(AvatarCatalogue.PaletteNames.Contains("peach") && AvatarCatalogue.PaletteNames.Contains("navy"));
            foreach (string h in AvatarCatalogue.PaletteHex.Concat(AvatarCatalogue.SkinHex)) Assert.That(h, Does.Match("^#[0-9a-f]{6}$"));
            Assert.GreaterOrEqual(AvatarCatalogue.PatternNames.Count, 4);
            Assert.AreEqual("plain", AvatarCatalogue.PatternNames[0]);
        }

        [Test]
        public void Catalogue_CombinationsRunIntoTheThousands()
        {
            long n = 1;
            foreach (string slot in new[] { "top", "bottom", "shoes", "hair", "hat", "glasses", "bag" }) n *= Count(slot);
            n *= AvatarCatalogue.PaletteHex.Count * AvatarCatalogue.SkinHex.Count;
            Assert.Greater(n, 1000000L);
            Assert.Greater(Count("top") * Count("bottom") * Count("hair") * Count("shoes"), 5000);
        }

        [Test]
        public void Catalogue_IdsAreUnique_PricesFollowTheTiers_EverySlotHasStarters()
        {
            Assert.AreEqual(AvatarCatalogue.All.Count, AvatarCatalogue.All.Select(i => i.Id).Distinct().Count());
            foreach (AvatarItem i in AvatarCatalogue.All)
            {
                Assert.IsTrue(i.Id.StartsWith(i.Slot + ":"), i.Id);
                Assert.IsTrue(AvatarCatalogue.Slots.Contains(i.Slot), i.Id);
                Assert.AreSame(i, AvatarCatalogue.ById(i.Id));
                Assert.AreEqual(i.Name.ToLowerInvariant(), i.Name, i.Id);
                Assert.IsTrue(i.Price == 0 || (i.Price >= 20 && i.Price <= 40) || (i.Price >= 60 && i.Price <= 120) || (i.Price >= 200 && i.Price <= 350), i.Id + " price " + i.Price);
                Assert.AreEqual(i.Price == 0, i.StartsOwned, i.Id);
            }
            foreach (string slot in AvatarCatalogue.Slots)
            {
                Assert.GreaterOrEqual(AvatarCatalogue.InSlot(slot).Count(i => i.StartsOwned), 1, slot + " has a starter");
                Assert.IsNotNull(AvatarCatalogue.Starter(slot));
            }
            foreach (string slot in new[] { "top", "bottom", "shoes", "hair" }) Assert.GreaterOrEqual(AvatarCatalogue.InSlot(slot).Count(i => i.StartsOwned), 3, slot);
            Assert.AreEqual(AvatarCatalogue.Slots.Count, AvatarCatalogue.SlotLabels.Count);
            foreach (string s in AvatarCatalogue.OptionalSlots) Assert.IsTrue(AvatarCatalogue.ById(s + ":none") != null && AvatarCatalogue.ById(s + ":none").StartsOwned, s);
            foreach (string w in AvatarCatalogue.OutfitWords) Assert.AreEqual(w.ToLowerInvariant(), w);
        }

        [Test]
        public void DefaultLook_IsCompleteAndOwnedByANewSave()
        {
            SaveBlob s = SaveBlob.Defaults(Day);
            AvatarLook l = AvatarRules.Look(s);
            foreach (string slot in AvatarCatalogue.Slots) { Assert.IsNotNull(l.Get(slot), slot); Assert.IsTrue(AvatarRules.Owns(s, l.Get(slot).Id), slot); }
            Assert.IsTrue(l.Same(AvatarLook.Default()));
            Assert.AreEqual(AvatarCatalogue.Peach, AvatarCatalogue.NearestColor("#ffbb9a"));
            Assert.AreEqual(AvatarCatalogue.Navy, AvatarCatalogue.NearestColor("#2c4190"));
        }

        // ---------- rules ----------

        [Test]
        public void Owns_Grant_AddOwnershipOnly()
        {
            SaveBlob s = SaveBlob.Defaults(Day);
            Assert.IsTrue(AvatarRules.Owns(s, "top:tee"));
            Assert.IsFalse(AvatarRules.Owns(s, "top:hoodie"));
            Assert.IsFalse(AvatarRules.Owns(s, "top:nope"));
            long balance = s.Fans.Balance, coins = s.Fans.Coins;
            AvatarRules.Grant(s, "top:hoodie");
            AvatarRules.Grant(s, "top:hoodie");
            AvatarRules.Grant(s, "top:nope");
            AvatarRules.Grant(s, "top:tee");
            Assert.IsTrue(AvatarRules.Owns(s, "top:hoodie"));
            CollectionAssert.AreEqual(new[] { "top:hoodie" }, s.Avatar.Owned);
            Assert.AreEqual(balance, s.Fans.Balance); Assert.AreEqual(coins, s.Fans.Coins);
        }

        [Test]
        public void Wear_KeepsWhatSheOwns_AndFallsBackForTheRest()
        {
            SaveBlob s = SaveBlob.Defaults(Day);
            AvatarLook l = AvatarLook.Default();
            l.Top = new AvatarPick("top:hoodie", AvatarCatalogue.Peach, 2);   // not owned
            l.Hat = new AvatarPick("hat:cap", 99, 3);                        // owned, colour out of range, no pattern on hats
            l.Bottom = new AvatarPick("bottom:shorts", 5, 1);
            l.Shoes = new AvatarPick("hat:cap", 0, 0);                       // wrong slot
            l.Glasses = null;
            l.Skin = 99; l.HairColor = -4;
            AvatarRules.Wear(s, l);
            AvatarLook w = AvatarRules.Look(s);
            Assert.AreEqual("top:zipjacket", w.Top.Id);
            Assert.AreEqual("hat:cap", w.Hat.Id); Assert.AreEqual(23, w.Hat.Color); Assert.AreEqual(0, w.Hat.Pattern);
            Assert.AreEqual("bottom:shorts", w.Bottom.Id); Assert.AreEqual(1, w.Bottom.Pattern);
            Assert.AreEqual("shoes:boots", w.Shoes.Id);
            Assert.AreEqual("glasses:none", w.Glasses.Id);
            Assert.AreEqual(7, w.Skin); Assert.AreEqual(0, w.HairColor);
            AvatarRules.Grant(s, "top:hoodie");
            l.Top = new AvatarPick("top:hoodie", AvatarCatalogue.Peach, 2);
            AvatarRules.Wear(s, l);
            Assert.AreEqual("top:hoodie", AvatarRules.Look(s).Top.Id);
            Assert.AreEqual(2, AvatarRules.Look(s).Top.Pattern);
        }

        [Test]
        public void Outfits_SaveByWord_FiveSlots_NoFreeTyping()
        {
            SaveBlob s = SaveBlob.Defaults(Day);
            AvatarLook l = AvatarLook.Default(); l.Top.Color = AvatarCatalogue.Peach;
            Assert.IsTrue(AvatarRules.SaveOutfit(s, 0, "sunny", l));
            Assert.IsTrue(AvatarRules.SaveOutfit(s, 4, "party", l));
            Assert.IsFalse(AvatarRules.SaveOutfit(s, 5, "sunny", l));
            Assert.IsFalse(AvatarRules.SaveOutfit(s, -1, "sunny", l));
            Assert.IsFalse(AvatarRules.SaveOutfit(s, 1, "my own name", l), "only words on the list");
            Assert.IsNull(AvatarRules.Outfit(s, 1));
            Assert.AreEqual("sunny", AvatarRules.Outfit(s, 0).Name);
            Assert.AreEqual(AvatarCatalogue.Peach, AvatarRules.OutfitLook(s, 0).Top.Color);
            AvatarRules.ClearOutfit(s, 0);
            Assert.IsNull(AvatarRules.Outfit(s, 0));
            Assert.AreEqual("sunny", AvatarRules.NextWord(null, 1));
            Assert.AreEqual("party", AvatarRules.NextWord("sunny", -1));
            Assert.AreEqual("sunny", AvatarRules.NextWord("party", 1));
        }

        // ---------- the save ----------

        [Test]
        public void NewSaveText_HasNoAvatarKey_AndAnEmptyAvatarIsNotWritten()
        {
            string t = SaveJson.ToText(SaveBlob.Defaults(Day));
            StringAssert.DoesNotContain("avatar", t);
            StringAssert.StartsWith("{\"v\":3,", t);
        }

        [Test]
        public void Avatar_RoundTrips_Compact_AndKeepsUnknownKeys()
        {
            SaveBlob s = SaveBlob.Defaults(Day);
            AvatarRules.Grant(s, "hat:crown"); AvatarRules.Grant(s, "top:hoodie");
            AvatarLook l = AvatarLook.Default();
            l.Top = new AvatarPick("top:hoodie", AvatarCatalogue.Peach, 1); l.Hat = new AvatarPick("hat:crown", 7, 0); l.Skin = 3; l.HairColor = 15;
            AvatarRules.Wear(s, l);
            AvatarRules.SaveOutfit(s, 2, "cool", l);
            s.Avatar.Extra = JsonValue.Obj().Set("later", true);
            string text = SaveJson.ToText(s);
            StringAssert.Contains("\"avatar\":{\"owned\":[\"hat:crown\",\"top:hoodie\"],\"look\":{\"top\":\"top:hoodie|1|1\"", text);
            Assert.Less(text.Length - SaveJson.ToText(SaveBlob.Defaults(Day)).Length, 1500, "compact: a full look and an outfit are about a kilobyte");
            SaveBlob b = SaveJson.Load(text, Day).Blob;
            Assert.AreEqual(text, SaveJson.ToText(b));
            AvatarLook r = AvatarRules.Look(b);
            Assert.IsTrue(r.Same(AvatarRules.Look(s)));
            Assert.AreEqual("hat:crown", r.Hat.Id); Assert.AreEqual(3, r.Skin); Assert.AreEqual(15, r.HairColor);
            Assert.AreEqual("cool", b.Avatar.Outfits[2].Name);
            Assert.IsNull(b.Avatar.Outfits[0]);
            Assert.IsTrue(b.Avatar.Outfits[2].Look.Same(AvatarRules.OutfitLook(s, 2)));
            Assert.IsTrue(StrictJson.Parse(text)["avatar"]["later"].BoolValue);
        }

        [Test]
        public void Avatar_ReadsDamagedData_WithoutBreaking()
        {
            string text = "{\"v\":3,\"fans\":{\"balance\":1,\"lifetime\":2},\"avatar\":{\"owned\":[\"top:nope\",7,\"top:vest\"],\"look\":{\"top\":\"top:vest|x|\",\"hat\":5,\"skin\":\"a\"},\"outfits\":[{\"n\":\"not a word\",\"look\":{}},3,{\"n\":\"bold\"}]}}";
            SaveBlob b = SaveJson.Load(text, Day).Blob;
            CollectionAssert.AreEqual(new[] { "top:vest" }, b.Avatar.Owned);
            AvatarLook l = AvatarRules.Look(b);
            Assert.AreEqual("top:vest", l.Top.Id); Assert.AreEqual(0, l.Top.Color);
            Assert.AreEqual("hat:none", l.Hat.Id);
            foreach (AvatarOutfit o in b.Avatar.Outfits) Assert.IsNull(o);
            Assert.AreEqual(1, b.Fans.Balance);
        }

        // ---------- old saves ----------

        const string OldV2 = "{\"v\":2,\"created\":\"2026-11-01\",\"fans\":{\"balance\":40000,\"lifetime\":90000}," +
            "\"inventory\":{\"owned\":[\"theme:tour-pink\",\"guitar:classic\",\"dog:none\",\"outfit:tour\",\"outfit:peach-navy\",\"outfit:cherry\",\"hair:chestnut\",\"hair:lilac\",\"dog:beanie\",\"dog:collar\",\"guitar:midnight\",\"lights:gold\"]," +
            "\"equipped\":{\"theme\":\"tour-pink\",\"guitar\":\"midnight\",\"dog\":\"beanie\",\"outfit\":\"peach-navy\",\"hair\":\"lilac\",\"lights\":\"gold\"}}}";

        [Test]
        public void OldV2Save_ShowsItsCurrentLook_AndKeepsEveryItem()
        {
            SaveBlob b = SaveJson.Load(OldV2, Day).Blob;
            Assert.AreEqual(3, b.V);
            AvatarLook l = AvatarRules.Look(b);
            Assert.AreEqual("top:zipjacket", l.Top.Id);
            Assert.AreEqual(AvatarCatalogue.Peach, l.Top.Color, "peach jacket");
            Assert.AreEqual(AvatarCatalogue.Navy, l.Bottom.Color, "navy trousers");
            Assert.AreEqual(AvatarCatalogue.PaletteNames.ToList().IndexOf("lilac"), l.HairColor);
            Assert.AreEqual("dog:beanie", l.Dog.Id);
            Assert.AreEqual("lights:gold", l.Lights.Id);
            Assert.AreEqual("guitar:acoustic", l.Guitar.Id);
            Assert.AreEqual(AvatarCatalogue.PaletteNames.ToList().IndexOf("navy"), l.Guitar.Color, "the midnight skin is the navy guitar");
            // everything bought is still owned in the new catalogue
            foreach (string id in new[] { "dog:beanie", "dog:collar", "lights:gold", "top:hoodie", "top:rockerjacket", "bottom:flares", "guitar:bass", "hair:wavy" })
                Assert.IsTrue(AvatarRules.Owns(b, id), id);
            Assert.IsFalse(AvatarRules.Owns(b, "top:stagecoat"), "an outfit she never bought maps to nothing");
            // nothing is written by loading, so the old data is still exactly there
            Assert.AreEqual(0, b.Avatar.Owned.Count);
            StringAssert.DoesNotContain("avatar", SaveJson.ToText(b));
            CollectionAssert.Contains(b.Inventory.Owned, "outfit:cherry");
            Assert.AreEqual("peach-navy", b.Inventory.Equipped["outfit"]);
            Assert.AreEqual(40000, b.Fans.Balance);
        }

        [Test]
        public void EveryOldWorldItem_MapsToTheNewCatalogue()
        {
            foreach (ShopItem it in Shop.World)
            {
                SaveBlob b = SaveBlob.Defaults(Day);
                b.Inventory.Owned.Add(it.Id);
                b.Inventory.Equipped[it.Slot] = it.Id.Substring(it.Slot.Length + 1);
                AvatarLook l = AvatarRules.Look(b);
                switch (it.Slot)
                {
                    case "outfit": Assert.AreEqual(AvatarCatalogue.NearestColor(it.Swatch[0]), l.Top.Color, it.Id); Assert.AreEqual(AvatarCatalogue.NearestColor(it.Swatch[2]), l.Bottom.Color, it.Id); break;
                    case "hair": Assert.AreEqual(AvatarCatalogue.NearestColor(it.Color), l.HairColor, it.Id); break;
                    case "lights": Assert.AreEqual(it.Id, l.Lights.Id); Assert.IsTrue(AvatarRules.Owns(b, it.Id)); break;
                }
            }
            foreach (ShopItem it in Shop.All.Where(i => i.Slot == "dog"))
            {
                SaveBlob b = SaveBlob.Defaults(Day);
                b.Inventory.Owned.Add(it.Id); b.Inventory.Equipped["dog"] = it.Id.Substring(4);
                Assert.AreEqual(it.Id, AvatarRules.Look(b).Dog.Id, it.Id);
            }
            foreach (ShopItem it in Shop.All.Where(i => i.Slot == "guitar"))
            {
                SaveBlob b = SaveBlob.Defaults(Day);
                b.Inventory.Owned.Add(it.Id); b.Inventory.Equipped["guitar"] = it.Id.Substring(7);
                Assert.AreEqual("guitar:acoustic", AvatarRules.Look(b).Guitar.Id);
            }
        }

        [Test]
        public void OnceSheWearsSomethingNew_TheSavedLookWins_AndRiffAndLightsStayInStepWithTheOldSlots()
        {
            SaveBlob b = SaveJson.Load(OldV2, Day).Blob;
            AvatarLook l = AvatarRules.Look(b);
            l.Dog = new AvatarPick("dog:collar"); l.Lights = new AvatarPick("lights:tour"); l.Top.Color = AvatarCatalogue.Peach + 3;
            AvatarRules.Wear(b, l);
            Assert.AreEqual("collar", b.Inventory.Equipped["dog"]);
            Assert.AreEqual("tour", b.Inventory.Equipped["lights"]);
            Assert.AreEqual(AvatarCatalogue.Peach + 3, AvatarRules.Look(b).Top.Color);
            // a new dog item has no old slot: the old slot is left alone
            l.Dog = new AvatarPick("dog:bowtie");
            AvatarRules.Wear(b, l);
            Assert.AreEqual("collar", b.Inventory.Equipped["dog"]);
            Assert.AreEqual("dog:bowtie", AvatarRules.Look(b).Dog.Id);
        }
    }
}
