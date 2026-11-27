using System.Collections.Generic;

namespace WorldTour.Core.Meta
{
    /// <summary>
    /// One backstage item (world_tour/data/shop.js). Everything is cosmetic. Prices are in fans to spend
    /// (balance); buying never touches lifetime fans, and nothing bought is ever taken away.
    /// </summary>
    public sealed class ShopItem
    {
        /// <summary>"slot:name", for example "guitar:mint".</summary>
        public readonly string Id;
        /// <summary>theme | guitar | dog | sticker, or (3D world only, phase 8.3) outfit | hair | lights.</summary>
        public readonly string Slot;
        public readonly string Name;
        /// <summary>Fans to spend; 0 = free and owned from the start.</summary>
        public readonly int Price;
        public readonly string Blurb;
        /// <summary>Themes only: four colors for the swatch (JS <c>swatch</c>); null for other slots.</summary>
        public readonly IReadOnlyList<string> Swatch;
        /// <summary>Guitars only: the body color, a CSS value as the JS writes it (JS <c>color</c>); null otherwise.</summary>
        public readonly string Color;
        /// <summary>Stickers only: the sticker art id (JS <c>sticker</c>); null otherwise.</summary>
        public readonly string Sticker;

        public ShopItem(string id, string slot, string name, int price, string blurb,
            IReadOnlyList<string> swatch = null, string color = null, string sticker = null)
        {
            Id = id; Slot = slot; Name = name; Price = price; Blurb = blurb; Swatch = swatch; Color = color; Sticker = sticker;
        }
    }

    /// <summary>A shelf in the backstage shop (shop.js <c>SLOTS</c>).</summary>
    public sealed class ShopSlot
    {
        public readonly string Id;
        public readonly string Name;
        public readonly string Note;

        public ShopSlot(string id, string name, string note) { Id = id; Name = name; Note = note; }
    }

    /// <summary>
    /// The backstage catalog, ported from world_tour/data/shop.js. A perfect 10-song show is about 40,000 fans;
    /// a typical one is 15,000 to 25,000. The first item in each wearable slot is free and owned from the start.
    /// </summary>
    public static class Shop
    {
        public const string Theme = "theme";
        public const string Guitar = "guitar";
        public const string Dog = "dog";
        public const string StickerSlot = "sticker";
        /// <summary>Phase 8.3, world only: the Star's outfit, her hair colour, the stage light colour. Not on the 2D backstage shelves.</summary>
        public const string Outfit = "outfit";
        public const string Hair = "hair";
        public const string Lights = "lights";

        /// <summary>U+2019, the right single quote the JS copy uses. Built from its code so the source stays ASCII.</summary>
        internal static readonly string Q = ((char)0x2019).ToString();

        private static string[] Sw(params string[] c) { return c; }

        /// <summary>shop.js:5 <c>SHOP</c>, in catalog order.</summary>
        public static readonly IReadOnlyList<ShopItem> All = new[]
        {
            /* color themes */
            new ShopItem("theme:tour-pink", Theme, "tour pink", 0, "the original. baby pink and ink.", swatch: Sw("#fdb1cb", "#000000", "#a8a9fc", "#ffffff")),
            new ShopItem("theme:peach-navy", Theme, "peach + navy", 60000, "peach paper, navy ink. very tour poster.", swatch: Sw("#ffcfb5", "#1c2747", "#9fb3ea", "#fffaf5")),

            /* guitar skins: the body color of every guitar sticker */
            new ShopItem("guitar:classic", Guitar, "classic", 0, "matches the page.", color: "var(--paper-deep)"),
            new ShopItem("guitar:lilac", Guitar, "lilac", 12000, "soft purple, loud amp.", color: "var(--accent)"),
            new ShopItem("guitar:sunburst", Guitar, "sunburst", 18000, "orange like a stage light.", color: "#ffb23f"),
            new ShopItem("guitar:mint", Guitar, "mint", 18000, "fresh out of the case.", color: "#8fe3c4"),
            new ShopItem("guitar:midnight", Guitar, "midnight", 25000, "deep blue, very serious.", color: "#2f2fc1"),
            new ShopItem("guitar:cherry", Guitar, "cherry", 30000, "the rock-star red.", color: "#e8364f"),

            /* outfits for riff */
            new ShopItem("dog:none", Dog, "just riff", 0, "band tee only. classic."),
            new ShopItem("dog:bandana", Dog, "bandana", 15000, "tied at the neck, road-trip style."),
            new ShopItem("dog:beanie", Dog, "beanie", 20000, "with a pompom. it" + Q + "s cold on the bus."),
            new ShopItem("dog:shades", Dog, "shades", 25000, "too cool to look directly at."),
            new ShopItem("dog:phones", Dog, "headphones", 30000, "tiny, for checking the mix."),
            new ShopItem("dog:collar", Dog, "spiked collar", 35000, "soft spikes. riff is a punk now."),

            /* stickers for the tour journal (milestone stickers are earned, not sold) */
            new ShopItem("sticker:pick", StickerSlot, "guitar pick", 5000, "always lost, always needed.", sticker: "pick"),
            new ShopItem("sticker:note", StickerSlot, "music note", 5000, "a little doodle.", sticker: "note"),
            new ShopItem("sticker:coffee", StickerSlot, "coffee cup", 6000, "from where it all started.", sticker: "coffee"),
            new ShopItem("sticker:phones", StickerSlot, "headphones", 8000, "for the bus ride home.", sticker: "phones"),
            new ShopItem("sticker:tee", StickerSlot, "merch tee", 8000, "sold out in every size.", sticker: "tee"),
            new ShopItem("sticker:crate", StickerSlot, "speaker crate", 10000, "fragile. this side up.", sticker: "crate"),
            new ShopItem("sticker:pass", StickerSlot, "backstage pass", 12000, "all access.", sticker: "pass"),
        };

        /// <summary>
        /// Phase 8.3: the items you can see in the 3D world. They are not in <see cref="All"/> (the 2D backstage and the JS parity tests walk that list
        /// and stay as they were); <see cref="ById"/>, <see cref="Every"/> and the free defaults know them. A save that owns one loads in the 2D game
        /// untouched (an owned id and an equipped slot it has no shelf for are kept as they are).
        /// Outfits carry three swatches (jacket, jacket shade, trousers); hair and stage lights carry their colour in <c>Color</c>.
        /// </summary>
        public static readonly IReadOnlyList<ShopItem> World = new[]
        {
            /* outfits for the star: a jacket and trousers colour set */
            new ShopItem("outfit:tour", Outfit, "tour pink", 0, "the one from the poster.", swatch: Sw("#ffc4da", "#f58bb2", "#34409a")),
            new ShopItem("outfit:peach-navy", Outfit, "peach + navy", 5000, "peach jacket, navy trousers. very tour poster.", swatch: Sw("#ffbb9a", "#f2926c", "#2c4190")),
            new ShopItem("outfit:mint", Outfit, "mint + denim", 12000, "fresh and a bit sporty.", swatch: Sw("#8fe3c4", "#52b896", "#3a5da8")),
            new ShopItem("outfit:sunshine", Outfit, "sunshine", 18000, "yellow, so no one misses you.", swatch: Sw("#ffd34e", "#e0a91e", "#4a5068")),
            new ShopItem("outfit:cherry", Outfit, "cherry + black", 25000, "the rock-star one.", swatch: Sw("#e8364f", "#a81a30", "#1b1b26")),
            new ShopItem("outfit:starlight", Outfit, "starlight", 35000, "lilac and gold, for the big nights.", swatch: Sw("#b3a2ff", "#7d6be0", "#2b2470")),

            /* hair colours */
            new ShopItem("hair:chestnut", Hair, "chestnut", 0, "the usual.", color: "#b5651d"),
            new ShopItem("hair:black", Hair, "midnight black", 5000, "sleek, with a blue shine.", color: "#1d1a28"),
            new ShopItem("hair:blonde", Hair, "honey blonde", 8000, "catches every light.", color: "#e8c066"),
            new ShopItem("hair:cherry", Hair, "cherry red", 12000, "loud on purpose.", color: "#c8283c"),
            new ShopItem("hair:lilac", Hair, "lilac", 15000, "soft purple, hard rock.", color: "#a58cf0"),

            /* stage light colours (every spotlight in every place) */
            new ShopItem("lights:tour", Lights, "tour mix", 0, "every place keeps its own lights.", color: "#ffffff"),
            new ShopItem("lights:pink", Lights, "hot pink", 6000, "the whole stage glows pink.", color: "#ff5fa8"),
            new ShopItem("lights:cyan", Lights, "ice blue", 10000, "cool and sharp.", color: "#4fd0ff"),
            new ShopItem("lights:gold", Lights, "gold", 15000, "warm, like a sold-out show.", color: "#ffc533"),
            new ShopItem("lights:violet", Lights, "violet", 20000, "deep purple haze.", color: "#9a5cff"),
        };

        /// <summary>The shelves of the 3D world's backstage panel, in order (phase 8.3): what she can see on her, on riff and on the stage.</summary>
        public static readonly IReadOnlyList<ShopSlot> WorldSlots = new[]
        {
            new ShopSlot(Outfit, "outfits", "what you wear on tour"),
            new ShopSlot(Hair, "hair colors", "shows on you everywhere"),
            new ShopSlot(Guitar, "guitar skins", "the one you play on stage"),
            new ShopSlot(Dog, "riff" + Q + "s outfits", "riff wears it everywhere"),
            new ShopSlot(Lights, "stage lights", "every stage, every place"),
        };

        /// <summary>The slots a save may hold an equipped item in, in the order the world reads them (the 2D three plus the world three).</summary>
        public static readonly IReadOnlyList<string> WorldWearable = new[] { Outfit, Hair, Guitar, Dog, Lights };

        /// <summary>Every item, 2D catalog first, then the world's (what <see cref="ById"/> searches).</summary>
        public static IEnumerable<ShopItem> Every
        {
            get { foreach (ShopItem i in All) yield return i; foreach (ShopItem i in World) yield return i; }
        }

        /// <summary>The free item of a slot (what a slot with nothing owned falls back to), or null for a slot with none.</summary>
        public static ShopItem FreeIn(string slot)
        {
            foreach (ShopItem i in Every) if (i.Slot == slot && i.Price == 0) return i;
            return null;
        }

        /// <summary>shop.js:37 <c>SLOTS</c>, in shelf order.</summary>
        public static readonly IReadOnlyList<ShopSlot> Slots = new[]
        {
            new ShopSlot(Theme, "color themes", "changes the whole game"),
            new ShopSlot(Guitar, "guitar skins", "every guitar sticker"),
            new ShopSlot(Dog, "riff" + Q + "s outfits", "riff wears it everywhere"),
            new ShopSlot(StickerSlot, "journal stickers", "yours to stick in the tour journal"),
        };

        /// <summary>The slots that can be equipped, in the order <c>look</c> reads them.</summary>
        public static readonly IReadOnlyList<string> Wearable = new[] { Theme, Guitar, Dog };

        /// <summary>shop.js:44 <c>itemById</c>: the item, or null for an unknown id.</summary>
        public static ShopItem ById(string id)
        {
            foreach (ShopItem i in All) if (i.Id == id) return i;
            foreach (ShopItem i in World) if (i.Id == id) return i;
            return null;
        }
    }
}
