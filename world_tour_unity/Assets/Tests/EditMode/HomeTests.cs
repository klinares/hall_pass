using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Maths.Gen;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;

namespace WorldTour.Core.Tests
{
    /// <summary>Phase 10B.2: the furniture catalogue, the placing rules on the grid, and the "home" key of the save.</summary>
    public class HomeTests
    {
        static SaveBlob Fresh() { return SaveBlob.Defaults("2026-11-27"); }

        // ---------- catalogue ----------

        [Test]
        public void Catalogue_HasThirtyPiecesInEveryCategoryAndTheTiers()
        {
            Assert.GreaterOrEqual(HomeCatalogue.All.Count, 30);
            foreach (string c in HomeCatalogue.Categories) Assert.Greater(HomeCatalogue.InCategory(c).Count, 0, c);
            Assert.AreEqual(HomeCatalogue.Categories.Count, HomeCatalogue.CategoryNames.Count);
            Assert.AreEqual(HomeCatalogue.All.Count, HomeCatalogue.All.Select(p => p.Id).Distinct().Count(), "ids are unique");
            foreach (HomePiece p in HomeCatalogue.All)
            {
                StringAssert.StartsWith("furn:", p.Id);
                Assert.AreSame(p, HomeCatalogue.ById(p.Id));
                Assert.IsTrue(p.Price == 0 || (p.Price >= 20 && p.Price <= 40) || (p.Price >= 60 && p.Price <= 120) || (p.Price >= 200 && p.Price <= 350), p.Id + " price " + p.Price);
                Assert.Greater(p.W, 0); Assert.Greater(p.D, 0);
                Assert.IsTrue(p.W <= 8 && p.D <= 8);
            }
            foreach (string id in HomeCatalogue.Starters) Assert.IsTrue(HomeCatalogue.ById(id).Starter, id);
            Assert.IsTrue(HomeCatalogue.All.Any(p => p.Price >= 200), "there are big things");
            Assert.IsNull(HomeCatalogue.ById("furn:nope"));
        }

        [Test]
        public void Words_AreShortAndLowerCase()
        {
            foreach (string line in HomeRules.AllLines().Concat(HomeCatalogue.All.Select(p => p.Name)).Concat(HomeCatalogue.CategoryNames))
            {
                Assert.LessOrEqual(line.Split(' ').Length, 12, line);
                Assert.AreEqual(line.ToLowerInvariant(), line, line);
            }
        }

        [Test]
        public void RoomPrices_AreBigTier()
        {
            Assert.AreEqual(0, HomeRules.RoomPrices[0]);
            for (int r = 1; r < HomeRules.RoomCount; r++) Assert.IsTrue(HomeRules.RoomPrices[r] >= 200 && HomeRules.RoomPrices[r] <= 350, "room " + r);
        }

        // ---------- owning ----------

        [Test]
        public void Grant_Owns_Buy()
        {
            SaveBlob s = Fresh();
            Assert.IsFalse(HomeRules.Owns(s, "furn:sofa"));
            Assert.IsFalse(HomeRules.Buy(s, "furn:sofa"), "no coins");
            s.Fans.Coins = 100;
            Assert.IsTrue(HomeRules.Buy(s, "furn:sofa"));
            Assert.AreEqual(10, s.Fans.Coins);
            Assert.IsTrue(HomeRules.Owns(s, "furn:sofa"));
            Assert.AreEqual(1, HomeRules.Boxed(s, "furn:sofa"));
            Assert.IsFalse(HomeRules.Grant(s, "furn:nothing"));
            Assert.IsFalse(HomeRules.Buy(s, "furn:bed_single"), "a starter is free, not sold");
            Assert.AreEqual(10, s.Fans.Coins);
        }

        // ---------- buying in the box (10B.2 wrap-up) ----------

        [Test]
        public void BuyFromBox_LandsInTheBox_AndTakesThePriceInCoins()
        {
            SaveBlob s = Fresh();
            s.Fans.Coins = 100;
            long fans = s.Fans.Lifetime;
            HomePiece sofa = HomeCatalogue.ById("furn:sofa");
            Assert.IsTrue(HomeRules.ForSale(s, sofa.Id));
            Assert.Contains(sofa, HomeRules.BoxList(s, HomeCatalogue.Seating), "the box offers it");
            Assert.AreEqual(BoutiqueRules.Price(90), HomeRules.BoxTag(s, sofa), "it shows its price");
            Assert.AreEqual(0, HomeRules.Short(s, sofa.Id));
            Assert.IsTrue(HomeRules.Buy(s, sofa.Id));
            Assert.AreEqual(100 - sofa.Price, s.Fans.Coins, "coins drop by the price");
            Assert.AreEqual(fans, s.Fans.Lifetime, "fans are never spent");
            Assert.AreEqual(1, HomeRules.Boxed(s, sofa.Id), "it lands in the box");
            Assert.IsFalse(HomeRules.ForSale(s, sofa.Id), "while one is in the box it is not offered again");
            Assert.AreEqual(HomeRules.InBox, HomeRules.BoxTag(s, sofa));
            Assert.AreEqual(PlaceCheck.Ok, HomeRules.Place(s, sofa.Id, 8, 10, 0, 0), "and she can place it");
            Assert.IsTrue(HomeRules.ForSale(s, sofa.Id), "with it standing she may buy another");
        }

        [Test]
        public void BuyFromBox_ShortOfCoins_ChangesNothing()
        {
            SaveBlob s = Fresh();
            s.Fans.Coins = 50;
            string before = SaveJson.ToCanonical(s);
            Assert.AreEqual(40, HomeRules.Short(s, "furn:sofa"));
            Assert.AreEqual(BoutiqueRules.NeedLabel(40), BoutiqueRules.NeedLabel(HomeRules.Short(s, "furn:sofa")));
            Assert.IsFalse(HomeRules.Buy(s, "furn:sofa"));
            Assert.AreEqual(before, SaveJson.ToCanonical(s), "the save is untouched");
            Assert.AreEqual(0, HomeRules.Boxed(s, "furn:sofa"));
            Assert.AreEqual(50, s.Fans.Coins);
        }

        [Test]
        public void BuyFromBox_StartersAreNeverSold()
        {
            SaveBlob s = Fresh();
            s.Fans.Coins = 1000;
            foreach (string id in HomeCatalogue.Starters)
            {
                Assert.IsFalse(HomeRules.ForSale(s, id), id);
                Assert.IsFalse(HomeRules.Buy(s, id), id);
            }
            Assert.AreEqual(1000, s.Fans.Coins);
            foreach (string c in HomeCatalogue.Categories)
                foreach (HomePiece p in HomeRules.BoxList(s, c)) Assert.IsFalse(p.Starter, p.Id + " is free, not for sale");
            Assert.IsFalse(HomeRules.ForSale(s, "furn:nope"));
            Assert.IsFalse(HomeRules.Buy(s, "furn:nope"));
        }

        [Test]
        public void BoxList_HerPiecesFirst_ThenWhatIsForSale()
        {
            SaveBlob s = Fresh();
            HomeRules.EnsureStarters(s);
            HomeRules.Grant(s, "furn:lamp_star");
            var lamps = HomeRules.BoxList(s, HomeCatalogue.Lamps).Select(p => p.Id).ToList();
            CollectionAssert.AreEqual(new[] { "furn:lamp_star", "furn:lamp_table", "furn:lamp_globe" }, lamps, "the floor lamp stands in the room and is free, so it is not listed");
            HomeRules.PutAway(s, s.Home.Placed.FindIndex(it => it.Id == "furn:lamp_floor"));
            lamps = HomeRules.BoxList(s, HomeCatalogue.Lamps).Select(p => p.Id).ToList();
            CollectionAssert.AreEqual(new[] { "furn:lamp_floor", "furn:lamp_star", "furn:lamp_table", "furn:lamp_globe" }, lamps);
        }

        [Test]
        public void UnlockRoom_TakesCoinsOnce()
        {
            SaveBlob s = Fresh();
            s.Fans.Coins = 199;
            Assert.IsFalse(HomeRules.UnlockRoom(s, 1));
            Assert.AreEqual(199, s.Fans.Coins);
            s.Fans.Coins = 450;
            Assert.IsTrue(HomeRules.UnlockRoom(s, 1));
            Assert.AreEqual(250, s.Fans.Coins);
            Assert.IsFalse(HomeRules.UnlockRoom(s, 1), "already hers");
            Assert.IsFalse(HomeRules.UnlockRoom(s, 0));
            Assert.IsFalse(HomeRules.UnlockRoom(s, 9));
            Assert.IsTrue(HomeRules.RoomUnlocked(s, 0)); Assert.IsTrue(HomeRules.RoomUnlocked(s, 1)); Assert.IsFalse(HomeRules.RoomUnlocked(s, 3));
            Assert.AreEqual(0, s.Fans.Lifetime, "fans are never touched");
        }

        // ---------- the grid ----------

        [Test]
        public void Place_Move_Turn_PutAway()
        {
            SaveBlob s = Fresh();
            HomeRules.Grant(s, "furn:sofa");
            Assert.AreEqual(PlaceCheck.Ok, HomeRules.Place(s, "furn:sofa", 5, 5, 0, 0));
            Assert.AreEqual(0, HomeRules.Boxed(s, "furn:sofa"));
            Assert.AreEqual(PlaceCheck.NotOwned, HomeRules.Place(s, "furn:sofa", 12, 12, 0, 0), "only one");
            Assert.AreEqual(PlaceCheck.Ok, HomeRules.Move(s, 0, 6, 5, 1, 0));
            Assert.AreEqual(1, s.Home.Placed[0].Rot);
            Assert.IsTrue(HomeRules.PutAway(s, 0));
            Assert.AreEqual(0, s.Home.Placed.Count);
            Assert.AreEqual(1, HomeRules.Boxed(s, "furn:sofa"), "still hers, back in the box");
            Assert.IsFalse(HomeRules.PutAway(s, 0));
            Assert.AreEqual(PlaceCheck.Ok, HomeRules.Place(s, "furn:sofa", 5, 5, 0, 0), "and she can take it out again");
        }

        [Test]
        public void Bounds_FootprintTurnsWithRotation()
        {
            SaveBlob s = Fresh();
            HomeRules.Grant(s, "furn:sofa");   // 4 x 2
            Assert.AreEqual(PlaceCheck.OutOfRoom, HomeRules.Check(s, "furn:sofa", 21, 5, 0, 0, -1));
            Assert.AreEqual(PlaceCheck.Ok, HomeRules.Check(s, "furn:sofa", 20, 5, 0, 0, -1));
            Assert.AreEqual(PlaceCheck.OutOfRoom, HomeRules.Check(s, "furn:sofa", 5, 21, 1, 0, -1), "turned it is 2 x 4");
            Assert.AreEqual(PlaceCheck.Ok, HomeRules.Check(s, "furn:sofa", 5, 20, 1, 0, -1));
            Assert.AreEqual(PlaceCheck.OutOfRoom, HomeRules.Check(s, "furn:sofa", -1, 5, 0, 0, -1));
            Assert.AreEqual(PlaceCheck.OutOfRoom, HomeRules.Check(s, "furn:sofa", 5, -1, 0, 0, -1));
            int w, d;
            HomeRules.Footprint(HomeCatalogue.ById("furn:sofa"), 3, out w, out d);
            Assert.AreEqual(2, w); Assert.AreEqual(4, d);
        }

        [Test]
        public void NoOverlap_ButTouchingIsFine_AndMovingOntoItselfIsFine()
        {
            SaveBlob s = Fresh();
            HomeRules.Grant(s, "furn:sofa"); HomeRules.Grant(s, "furn:chair"); HomeRules.Grant(s, "furn:chair");
            Assert.AreEqual(PlaceCheck.Ok, HomeRules.Place(s, "furn:sofa", 5, 5, 0, 0));   // x 5..8, z 5..6
            Assert.AreEqual(PlaceCheck.Overlap, HomeRules.Place(s, "furn:chair", 8, 6, 0, 0));
            Assert.AreEqual(PlaceCheck.Ok, HomeRules.Place(s, "furn:chair", 9, 5, 0, 0), "next to it");
            Assert.AreEqual(PlaceCheck.Ok, HomeRules.Place(s, "furn:chair", 5, 7, 0, 0), "in front of it");
            Assert.AreEqual(PlaceCheck.Overlap, HomeRules.Move(s, 1, 5, 6, 0, 0));
            Assert.AreEqual(PlaceCheck.Overlap, HomeRules.Move(s, 0, 5, 5, 1, 0), "turned it is 2 x 4 and runs into the chair in front");
            Assert.AreEqual(PlaceCheck.Ok, HomeRules.Move(s, 0, 5, 8, 1, 0), "clear of the chairs");
            Assert.AreEqual(PlaceCheck.Ok, HomeRules.Move(s, 0, 5, 5, 0, 0));
            Assert.AreEqual(PlaceCheck.Ok, HomeRules.Check(s, "furn:sofa", 5, 5, 0, 0, 0), "ignores itself");
        }

        [Test]
        public void Rugs_LieUnderOtherPieces_ButNotUnderOtherRugs()
        {
            SaveBlob s = Fresh();
            foreach (string id in new[] { "furn:rug_big", "furn:rug_star", "furn:sofa" }) HomeRules.Grant(s, id);
            Assert.AreEqual(PlaceCheck.Ok, HomeRules.Place(s, "furn:rug_big", 8, 8, 0, 0));
            Assert.AreEqual(PlaceCheck.Ok, HomeRules.Place(s, "furn:sofa", 9, 9, 0, 0), "a sofa stands on a rug");
            Assert.AreEqual(PlaceCheck.Overlap, HomeRules.Place(s, "furn:rug_star", 10, 8, 0, 0));
        }

        [Test]
        public void Posters_HangOnTheBackWall()
        {
            SaveBlob s = Fresh();
            foreach (string id in new[] { "furn:poster_heart", "furn:poster_star", "furn:sofa" }) HomeRules.Grant(s, id);
            Assert.AreEqual(PlaceCheck.Ok, HomeRules.Place(s, "furn:poster_heart", 8, 3, 2, 0), "z and turn are ignored: it goes on the wall");
            Assert.AreEqual(HomeRules.Cells - 1, s.Home.Placed[0].Z);
            Assert.AreEqual(0, s.Home.Placed[0].Rot);
            Assert.AreEqual(PlaceCheck.Overlap, HomeRules.Place(s, "furn:poster_star", 10, 0, 0, 0));
            Assert.AreEqual(PlaceCheck.Ok, HomeRules.Check(s, "furn:poster_star", 2, 0, 0, 0, -1), "the old ticket wall spot is free since the wrap-up");
            Assert.AreEqual(PlaceCheck.Ok, HomeRules.Check(s, "furn:poster_star", 18, 0, 0, 0, -1), "the old sticker board spot is free since the wrap-up");
            Assert.AreEqual(PlaceCheck.Ok, HomeRules.Place(s, "furn:poster_star", 11, 0, 0, 0), "beside the first");
            Assert.AreEqual(PlaceCheck.Ok, HomeRules.Check(s, "furn:sofa", 8, 21, 0, 0, -1), "a sofa can stand under a poster");
        }

        [Test]
        public void ReservedSpots_DoorsWayOutAndBoxStayFree()
        {
            SaveBlob s = Fresh();
            foreach (string id in new[] { "furn:chair", "furn:chair", "furn:chair", "furn:chair" }) HomeRules.Grant(s, id);
            Assert.AreEqual(PlaceCheck.Reserved, HomeRules.Check(s, "furn:chair", 11, 1, 0, 0, -1), "the way out");
            Assert.AreEqual(PlaceCheck.Reserved, HomeRules.Check(s, "furn:chair", 1, 12, 0, 0, -1), "the west door");
            Assert.AreEqual(PlaceCheck.Reserved, HomeRules.Check(s, "furn:chair", 22, 12, 0, 0, -1), "the east door");
            Assert.AreEqual(PlaceCheck.Ok, HomeRules.Check(s, "furn:chair", 0, 18, 0, 0, -1), "the old outfit rack spot is free since the wrap-up");
            Assert.AreEqual(PlaceCheck.Reserved, HomeRules.Check(s, "furn:chair", 20, 1, 0, 0, -1), "the box");
            Assert.AreEqual(PlaceCheck.Ok, HomeRules.Check(s, "furn:chair", 11, 10, 0, 0, -1));
        }

        [Test]
        public void Rooms_AreSeparate_AndLockedUntilBought()
        {
            SaveBlob s = Fresh();
            HomeRules.Grant(s, "furn:sofa"); HomeRules.Grant(s, "furn:sofa");
            Assert.AreEqual(PlaceCheck.Locked, HomeRules.Place(s, "furn:sofa", 5, 5, 0, 1));
            s.Fans.Coins = 300;
            Assert.IsTrue(HomeRules.UnlockRoom(s, 1));
            Assert.AreEqual(PlaceCheck.Ok, HomeRules.Place(s, "furn:sofa", 5, 5, 0, 0));
            Assert.AreEqual(PlaceCheck.Ok, HomeRules.Place(s, "furn:sofa", 5, 5, 0, 1), "same cells, other room");
            CollectionAssert.AreEqual(new[] { 0 }, HomeRules.InRoom(s, 0));
            CollectionAssert.AreEqual(new[] { 1 }, HomeRules.InRoom(s, 1));
            Assert.AreEqual(1, HomeRules.DoorTarget(0, true)); Assert.AreEqual(2, HomeRules.DoorTarget(0, false)); Assert.AreEqual(3, HomeRules.DoorTarget(1, true));
            Assert.AreEqual(-1, HomeRules.DoorTarget(3, true));
        }

        [Test]
        public void Starters_AreGivenOnceAndStandInTheRoom()
        {
            SaveBlob s = Fresh();
            Assert.IsTrue(HomeRules.EnsureStarters(s));
            foreach (string id in HomeCatalogue.Starters) Assert.IsTrue(HomeRules.Owns(s, id), id);
            Assert.AreEqual(4, s.Home.Placed.Count, "all four stand in the room");
            for (int i = 0; i < s.Home.Placed.Count; i++)
            {
                HomeItem it = s.Home.Placed[i];
                Assert.AreEqual(PlaceCheck.Ok, HomeRules.Check(s, it.Id, it.X, it.Z, it.Rot, it.Room, i), it.Id);
            }
            HomeRules.PutAway(s, 0);
            Assert.IsFalse(HomeRules.EnsureStarters(s), "not again");
            Assert.AreEqual(3, s.Home.Placed.Count);
            Assert.AreEqual(0, s.Fans.Coins, "free");
        }

        [Test]
        public void Colors_AreFromThePalette()
        {
            SaveBlob s = Fresh();
            Assert.IsTrue(HomeRules.SetWall(s, 11));
            Assert.IsTrue(HomeRules.SetFloor(s, 21));
            Assert.IsFalse(HomeRules.SetWall(s, 24));
            Assert.IsFalse(HomeRules.SetFloor(s, -2));
            Assert.AreEqual(11, s.Home.Wall); Assert.AreEqual(21, s.Home.Floor);
        }

        // ---------- the save ----------

        [Test]
        public void OldSave_HasAnEmptyHome_AndWritesNoHomeKey()
        {
            SaveBlob s = Fresh();
            Assert.IsTrue(s.Home.IsEmpty);
            Assert.IsNull(SaveJson.Write(s)["home"]);
            SaveBlob back = SaveJson.Load("{\"v\":2,\"fans\":{\"balance\":5,\"lifetime\":900}}", "2026-11-27").Blob;
            Assert.IsTrue(back.Home.IsEmpty);
            Assert.AreEqual(0, back.Home.Placed.Count);
            Assert.IsFalse(back.Home.Started);
            Assert.IsFalse(SaveJson.ToText(back).Contains("\"home\""), "the text of an old save does not grow");
            Assert.AreEqual(900, back.Fans.Lifetime);
        }

        [Test]
        public void RoundTrip_KeepsEverything()
        {
            SaveBlob s = Fresh();
            s.Fans.Coins = 900;
            foreach (string id in new[] { "furn:sofa", "furn:chair", "furn:chair", "furn:chair", "furn:rug_big", "furn:poster_map" }) HomeRules.Grant(s, id);
            HomeRules.UnlockRoom(s, 1); HomeRules.UnlockRoom(s, 3);
            HomeRules.EnsureStarters(s);
            HomeRules.Place(s, "furn:sofa", 12, 12, 1, 0);
            HomeRules.Place(s, "furn:chair", 5, 5, 0, 1);
            HomeRules.Place(s, "furn:poster_map", 9, 0, 0, 0);
            HomeRules.SetWall(s, 3); HomeRules.SetFloor(s, 20);
            string text = SaveJson.ToText(s);
            SaveBlob back = SaveJson.Load(text, "2030-01-01").Blob;
            Assert.AreEqual(text, SaveJson.ToText(back));
            Assert.AreEqual(s.Home.Placed.Count, back.Home.Placed.Count);
            for (int i = 0; i < s.Home.Placed.Count; i++)
            {
                Assert.AreEqual(s.Home.Placed[i].Id, back.Home.Placed[i].Id);
                Assert.AreEqual(s.Home.Placed[i].X, back.Home.Placed[i].X); Assert.AreEqual(s.Home.Placed[i].Z, back.Home.Placed[i].Z);
                Assert.AreEqual(s.Home.Placed[i].Rot, back.Home.Placed[i].Rot); Assert.AreEqual(s.Home.Placed[i].Room, back.Home.Placed[i].Room);
            }
            CollectionAssert.AreEquivalent(s.Home.Owned, back.Home.Owned);
            Assert.AreEqual(3, HomeRules.Count(back, "furn:chair"));
            CollectionAssert.AreEqual(new[] { 1, 3 }, back.Home.Rooms);
            Assert.AreEqual(3, back.Home.Wall); Assert.AreEqual(20, back.Home.Floor);
            Assert.IsTrue(back.Home.Started);
            Assert.AreEqual(s.Fans.Coins, back.Fans.Coins);
        }

        [Test]
        public void Shape_IsCompact_OwnWithCountsAndPutArrays()
        {
            SaveBlob s = Fresh();
            HomeRules.Grant(s, "furn:chair"); HomeRules.Grant(s, "furn:chair");
            HomeRules.Place(s, "furn:chair", 5, 6, 3, 0);
            string text = SaveJson.ToText(s);
            StringAssert.Contains("\"home\":{\"box\":[\"furn:chair\"],\"put\":[[\"furn:chair\",5,6,3,0]]}", text);
        }

        [Test]
        public void Size_FortyPlacedItemsUnderTwoKilobytes()
        {
            SaveBlob s = Fresh();
            s.Fans.Coins = 100000;
            for (int r = 1; r < HomeRules.RoomCount; r++) HomeRules.UnlockRoom(s, r);
            // every catalogue piece once (the worst case for the owned list), then the 40 that fit, spread over the rooms
            foreach (HomePiece p in HomeCatalogue.All) HomeRules.Grant(s, p.Id);
            int placed = 0, room = 0;
            foreach (HomePiece p in HomeCatalogue.All)
            {
                if (placed >= 40) break;
                bool ok = false;
                for (int tries = 0; tries < HomeRules.RoomCount && !ok; tries++)
                {
                    int rr = (room + tries) % HomeRules.RoomCount;
                    for (int z = 0; z < HomeRules.Cells && !ok; z += 1)
                        for (int x = 0; x < HomeRules.Cells && !ok; x += 1)
                            if (HomeRules.Place(s, p.Id, x, z, 0, rr) == PlaceCheck.Ok) { ok = true; room = rr; }
                }
                if (ok) placed++;
                room = (room + 1) % HomeRules.RoomCount;
            }
            Assert.AreEqual(40, s.Home.Placed.Count, "forty fit");
            string home = SaveJson.Write(s)["home"].ToCanonicalJson();
            Assert.Less(home.Length, 2048, "home is " + home.Length + " bytes");
            SaveBlob back = SaveJson.Load(SaveJson.ToText(s), "2030-01-01").Blob;
            Assert.AreEqual(40, back.Home.Placed.Count);
        }

        [Test]
        public void Migration_UnknownKeysAreKept_AndBadItemsDropped()
        {
            string old = "{\"v\":3,\"home\":{\"box\":[\"furn:sofa\",\"furn:ghost\",7],\"put\":[[\"furn:sofa\",1,2,9,0],[\"furn:ghost\",1,1,0,0],[\"furn:sofa\"],\"x\"],\"wall\":99,\"floor\":4,\"rooms\":[2,2,0,17],\"st\":1,\"future\":{\"a\":1}},\"extra\":5}";
            SaveBlob b = SaveJson.Load(old, "2026-11-27").Blob;
            Assert.AreEqual(2, b.Home.Owned.Count, "one in the box and the one placed");
            Assert.AreEqual(1, b.Home.Placed.Count);
            Assert.AreEqual(1, b.Home.Placed[0].Rot, "9 turns wrap to 1");
            Assert.AreEqual(-1, b.Home.Wall, "99 is not in the palette");
            Assert.AreEqual(4, b.Home.Floor);
            CollectionAssert.AreEqual(new[] { 2 }, b.Home.Rooms);
            string text = SaveJson.ToText(b);
            StringAssert.Contains("\"future\":{\"a\":1}", text);
            StringAssert.Contains("\"extra\":5", text);
        }

        [Test]
        public void HomeAlone_IsStillWritten_WhenOnlyAColorIsSet()
        {
            SaveBlob s = Fresh();
            HomeRules.SetWall(s, 5);
            Assert.IsFalse(s.Home.IsEmpty);
            StringAssert.Contains("\"home\":{\"wall\":5}", SaveJson.ToText(s));
        }
    }
}
