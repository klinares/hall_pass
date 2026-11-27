using System.Collections.Generic;
using WorldTour.Core.Progress;

namespace WorldTour.Core.Meta
{
    /// <summary>What a rack item is to her right now.</summary>
    public enum ItemState
    {
        /// <summary>Not hers; she can pay for it.</summary>
        CanBuy,
        /// <summary>Not hers; she needs more coins.</summary>
        TooMuch,
        /// <summary>Hers, not on.</summary>
        Yours,
        /// <summary>Hers and on right now (the same item).</summary>
        Wearing,
    }

    /// <summary>
    /// Phase 10B.1: the clothing boutique's rules. Pure functions over the save, so the 3D store (BoutiqueGame in the World assembly) only draws them.
    /// Buying is <see cref="CoinRules.TrySpend"/> then <see cref="AvatarRules.Grant"/> and puts the thing on at once; fans are never touched and
    /// nothing she owns is ever taken away. All the words a store says are here, in lower case, 12 words or fewer, so a test can check them.
    /// </summary>
    public static class BoutiqueRules
    {
        /// <summary>The racks, in the order the store walks them: tops, bottoms, shoes, hair, hats, glasses, bags, faces (faces have no rack in the room: the mirror lists them).</summary>
        public static readonly IReadOnlyList<string> RackSlots = new[]
        {
            AvatarCatalogue.Top, AvatarCatalogue.Bottom, AvatarCatalogue.Shoes, AvatarCatalogue.Hair,
            AvatarCatalogue.Hat, AvatarCatalogue.Glasses, AvatarCatalogue.Bag, AvatarCatalogue.Face,
        };

        /// <summary>The rack's name on its sign ("tops", "bottoms", "shoes", "hair", "hats", "glasses", "bags").</summary>
        public static string RackName(string slot)
        {
            int i = -1;
            for (int k = 0; k < AvatarCatalogue.Slots.Count; k++) if (AvatarCatalogue.Slots[k] == slot) i = k;
            return i >= 0 ? AvatarCatalogue.SlotLabels[i] : slot;
        }

        /// <summary>The items on a rack, in catalogue order (the "none" items of hats, glasses and bags stay: she can take a hat off).</summary>
        public static List<AvatarItem> Rack(string slot) { return AvatarCatalogue.InSlot(slot); }

        /// <summary>How many of a rack's items she owns, and how many there are, as "3 of 14".</summary>
        public static string Owned(SaveBlob s, string slot)
        {
            int own = 0, all = 0;
            foreach (AvatarItem it in Rack(slot)) { all++; if (AvatarRules.Owns(s, it.Id)) own++; }
            return own + " of " + all;
        }

        /// <summary>The look she would see in the mirror: what she wears now with this item (in this colour and pattern) in its slot.</summary>
        public static AvatarLook TryLook(SaveBlob s, AvatarItem item, int color, int pattern)
        {
            AvatarLook l = AvatarRules.Look(s);
            l.Set(item.Slot, new AvatarPick(item.Id, AvatarCatalogue.TakesColor(item.Slot) ? color : 0, AvatarCatalogue.TakesPattern(item.Slot) ? pattern : 0));
            return l;
        }

        /// <summary>Is it hers, on, or for sale (and can she pay).</summary>
        public static ItemState StateOf(SaveBlob s, AvatarItem item)
        {
            if (AvatarRules.Owns(s, item.Id))
            {
                AvatarPick worn = AvatarRules.Look(s).Get(item.Slot);
                return worn != null && worn.Id == item.Id ? ItemState.Wearing : ItemState.Yours;
            }
            return CoinRules.Coins(s) >= item.Price ? ItemState.CanBuy : ItemState.TooMuch;
        }

        /// <summary>
        /// Buys an item: coins first, then it is hers and she puts it on in the colour and pattern she tried. Unknown, already hers, or not enough
        /// coins change nothing (<see cref="BuyResult.Need"/> says how many more). Never touches fans.
        /// </summary>
        public static BuyResult Buy(SaveBlob s, string itemId, int color, int pattern)
        {
            AvatarItem it = AvatarCatalogue.ById(itemId);
            if (it == null) return new BuyResult { Reason = BuyReason.Unknown };
            if (AvatarRules.Owns(s, itemId)) return new BuyResult { Reason = BuyReason.Owned };
            long short_ = it.Price - CoinRules.Coins(s);
            if (!CoinRules.TrySpend(s, it.Price)) return new BuyResult { Reason = BuyReason.Short, Need = short_ };
            AvatarRules.Grant(s, itemId);
            AvatarRules.Wear(s, TryLook(s, it, color, pattern));
            return new BuyResult { Ok = true };
        }

        /// <summary>Puts on something she owns, in the colour and pattern she tried. False when it is not hers.</summary>
        public static bool WearOwned(SaveBlob s, string itemId, int color, int pattern)
        {
            AvatarItem it = AvatarCatalogue.ById(itemId);
            if (it == null || !AvatarRules.Owns(s, itemId)) return false;
            AvatarRules.Wear(s, TryLook(s, it, color, pattern));
            return true;
        }

        /// <summary>The price as a store sign says it: "30 coins" (and "1 coin").</summary>
        public static string Price(long coins) { return coins == 1 ? "1 coin" : coins + " coins"; }

        // ---------- the words (all lower case, 12 words or fewer) ----------

        public const string DoorOutside = "boutique";
        public const string DoorGoIn = "go in";
        public const string DoorStay = "not now";
        public const string DoorLine = "clothes, hair and hats. come in!";
        public const string Welcome = "hi! try things on at the mirror.";
        public const string WelcomeLooking = "look around. nothing is ever taken away.";
        public const string Bye = "come back any time!";
        public const string LeaveLine = "back out to the street?";
        public const string LeaveGo = "go out";
        public const string LeaveStay = "stay in";
        public const string Look = "look";
        public const string OtherRack = "other rack";
        public const string Done = "all done";
        public const string Before = "previous";
        public const string Next = "next";
        public const string Colour = "color";
        public const string Back = "back";
        public const string WearIt = "wear it";
        public const string OnNow = "wearing it";
        public const string Bought = "yours! it is on.";
        public const string Mirror = "mirror";
        public const string Shopkeeper = "shopkeeper";

        public static string BuyLabel(long price) { return "buy " + Price(price); }
        public static string NeedLabel(long more) { return "need " + (more == 1 ? "1 more" : more + " more"); }
        public static string Wallet(long coins) { return "you have " + Price(coins); }
        public static string RackLine(string slot) { return "pick a look from the " + RackName(slot) + " rack."; }

        /// <summary>Every sentence the store says, for the word-count test.</summary>
        public static IEnumerable<string> AllLines(SaveBlob s)
        {
            yield return DoorLine; yield return Welcome; yield return WelcomeLooking; yield return Bye; yield return LeaveLine; yield return Bought;
            yield return Wallet(0); yield return Wallet(1); yield return Wallet(12345);
            yield return BuyLabel(1); yield return BuyLabel(350); yield return NeedLabel(1); yield return NeedLabel(199);
            foreach (string slot in RackSlots) { yield return RackLine(slot); yield return Owned(s, slot); }
        }
    }
}
