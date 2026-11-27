using System.Collections.Generic;

namespace WorldTour.Core.Meta
{
    /// <summary>
    /// Phase 10B.1: one thing she can wear or carry. Ids are "slot:key" ("top:hoodie"). Colours and patterns are not items: every item comes in all
    /// <see cref="AvatarCatalogue.PaletteHex"/> colours (and, for tops and bottoms, every pattern), free, once she owns the item itself.
    /// </summary>
    public sealed class AvatarItem
    {
        public string Id;
        public string Slot;
        public string Name;
        /// <summary>In coins (the stores take coins). 0 = a free starter.</summary>
        public long Price;
        public bool StartsOwned;
    }

    /// <summary>
    /// The avatar catalogue: what the boutique sells and the wardrobe trunk lets her mix. Plain data, no engine types. The 3D parts that draw each id
    /// live in the World assembly (AvatarParts); an id with no entry there simply draws nothing extra.
    /// Price tiers: small 20 to 40, mid 60 to 120, big 200 to 350; a few starters (price 0) in every slot.
    /// </summary>
    public static class AvatarCatalogue
    {
        public const string Top = "top", Bottom = "bottom", Shoes = "shoes", Hair = "hair", Hat = "hat", Glasses = "glasses", Bag = "bag",
            Guitar = "guitar", Dog = "dog", Lights = "lights", Face = "face";

        /// <summary>The item slots, in the order the editor lists them. Hair colour and skin tone are indices in the look, not items.</summary>
        public static readonly IReadOnlyList<string> Slots = new[] { Top, Bottom, Shoes, Hair, Hat, Glasses, Bag, Guitar, Dog, Lights, Face };

        /// <summary>The slots that can be worn as nothing ("none" is a starter).</summary>
        public static readonly IReadOnlyList<string> OptionalSlots = new[] { Hat, Glasses, Bag, Dog };

        /// <summary>The slots whose item takes a pattern (stripes, dots and so on).</summary>
        public static readonly IReadOnlyList<string> PatternSlots = new[] { Top, Bottom };

        /// <summary>The slots whose item takes a colour from the palette (not stage lights or faces, which are drawn in their own colors).</summary>
        public static bool TakesColor(string slot) { return slot != Lights && slot != Face; }

        public static bool TakesPattern(string slot) { return slot == Top || slot == Bottom; }

        public static readonly IReadOnlyList<string> SlotLabels = new[] { "tops", "bottoms", "shoes", "hair", "hats", "glasses", "bags", "guitar", "riff", "lights", "faces" };

        /// <summary>The 24 colours. Index 1 is peach, index 2 is navy (the tour poster pair); 18 chestnut and 23 black are the old hair colours.</summary>
        public static readonly IReadOnlyList<string> PaletteHex = new[]
        {
            "#ffc4da", "#ffbb9a", "#2c4190", "#e8648c", "#e8364f", "#8c2a44", "#ff9a52", "#ffd34e",
            "#e8c066", "#b5e05a", "#4fb870", "#8fe3c4", "#2aa6a0", "#6cc8f0", "#3f7ae0", "#b3a2ff",
            "#8a55d8", "#c040b0", "#b5651d", "#6e4528", "#fafafa", "#fff1d6", "#9a9eaa", "#1d1a28",
        };

        public static readonly IReadOnlyList<string> PaletteNames = new[]
        {
            "pink", "peach", "navy", "rose", "red", "wine", "orange", "yellow",
            "honey", "lime", "green", "mint", "teal", "sky", "blue", "lilac",
            "purple", "magenta", "chestnut", "brown", "white", "cream", "grey", "black",
        };

        public const int Pink = 0, Peach = 1, Navy = 2, Honey = 8, Chestnut = 18, Brown = 19, Black = 23;

        /// <summary>Eight skin tones, light to deep.</summary>
        public static readonly IReadOnlyList<string> SkinHex = new[]
        {
            "#fdd2b4", "#f6c6a0", "#eab088", "#d99b6c", "#bd7c4e", "#94593a", "#6f4228", "#4a2d1e",
        };

        public static readonly IReadOnlyList<string> SkinNames = new[] { "light", "fair", "warm", "tan", "bronze", "brown", "deep", "dark" };

        /// <summary>The patterns of a top or a bottom. 0 is plain.</summary>
        public static readonly IReadOnlyList<string> PatternNames = new[] { "plain", "stripes", "dots", "stars", "checks" };

        /// <summary>The words an outfit can be named from (no typing).</summary>
        public static readonly IReadOnlyList<string> OutfitWords = new[]
        {
            "sunny", "cozy", "stage", "sparkly", "comfy", "cool", "fancy", "bold", "bright", "dreamy",
            "rocker", "sporty", "zippy", "starry", "mellow", "jazzy", "fresh", "lucky", "cute", "party",
        };

        public const int OutfitSlots = 5;

        static AvatarItem I(string slot, string key, string name, long price)
        {
            return new AvatarItem { Id = slot + ":" + key, Slot = slot, Name = name, Price = price, StartsOwned = price == 0 };
        }

        /// <summary>Every item, slot by slot.</summary>
        public static readonly IReadOnlyList<AvatarItem> All = new[]
        {
            // tops (20)
            I(Top, "tee", "tee", 0), I(Top, "sweater", "sweater", 0), I(Top, "zipjacket", "zip jacket", 0),
            I(Top, "tank", "tank", 25), I(Top, "polo", "polo", 25), I(Top, "hoodie", "hoodie", 30), I(Top, "turtleneck", "turtleneck", 35), I(Top, "cardigan", "cardigan", 40), I(Top, "vest", "vest", 30),
            I(Top, "denimjacket", "work jacket", 85), I(Top, "bomber", "bomber", 100), I(Top, "raincoat", "raincoat", 90), I(Top, "overalls", "overalls", 80), I(Top, "poncho", "poncho", 110), I(Top, "sailor", "sailor top", 75), I(Top, "puffer", "puffer", 120),
            I(Top, "rockerjacket", "rocker jacket", 220), I(Top, "varsity", "team jacket", 200), I(Top, "stagecoat", "stage coat", 300), I(Top, "sequin", "sequin top", 340),
            // bottoms (12)
            I(Bottom, "jeans", "jeans", 0), I(Bottom, "shorts", "shorts", 0), I(Bottom, "skirt", "skirt", 0),
            I(Bottom, "leggings", "leggings", 25), I(Bottom, "capris", "capris", 30), I(Bottom, "joggers", "joggers", 35),
            I(Bottom, "cargo", "cargo pants", 70), I(Bottom, "pleated", "pleated skirt", 80), I(Bottom, "flares", "flares", 90), I(Bottom, "tutu", "tutu", 120),
            I(Bottom, "stagepants", "stage pants", 230), I(Bottom, "sparkleskirt", "sparkle skirt", 260),
            // shoes (8)
            I(Shoes, "boots", "boots", 0), I(Shoes, "sneakers", "sneakers", 0), I(Shoes, "flats", "flats", 0),
            I(Shoes, "sandals", "sandals", 25), I(Shoes, "hightops", "high tops", 40), I(Shoes, "rainboots", "rain boots", 70),
            I(Shoes, "platforms", "platforms", 110), I(Shoes, "glitterboots", "glitter boots", 240),
            // hair styles (14); the colour is separate
            I(Hair, "bob", "bob", 0), I(Hair, "ponytail", "ponytail", 0), I(Hair, "pigtails", "pigtails", 0),
            I(Hair, "bun", "bun", 25), I(Hair, "braid", "braid", 30), I(Hair, "fringe", "fringe", 30), I(Hair, "spacebuns", "space buns", 40), I(Hair, "long", "long", 40),
            I(Hair, "wavy", "wavy", 60), I(Hair, "pompoms", "pom poms", 70), I(Hair, "curls", "curls", 80), I(Hair, "afro", "big puff", 90), I(Hair, "mohawk", "mohawk", 100), I(Hair, "spikes", "spikes", 110),
            // hats (13 and none)
            I(Hat, "none", "no hat", 0), I(Hat, "beanie", "beanie", 0), I(Hat, "cap", "cap", 0),
            I(Hat, "headband", "headband", 20), I(Hat, "bucket", "bucket hat", 30), I(Hat, "bow", "big bow", 35), I(Hat, "partyhat", "party hat", 40), I(Hat, "flowers", "flower crown", 40),
            I(Hat, "beret", "beret", 60), I(Hat, "catears", "cat ears", 60), I(Hat, "sunhat", "sun hat", 70), I(Hat, "headphones", "headphones", 90),
            I(Hat, "tophat", "top hat", 120), I(Hat, "crown", "crown", 280),
            // glasses (6 and none)
            I(Glasses, "none", "no glasses", 0), I(Glasses, "round", "round", 0),
            I(Glasses, "square", "square", 25), I(Glasses, "shades", "shades", 30), I(Glasses, "cateye", "cat eye", 60), I(Glasses, "star", "star shades", 60), I(Glasses, "goggles", "goggles", 90), I(Glasses, "visor", "visor", 120),
            // bags (7 and none)
            I(Bag, "none", "no bag", 0), I(Bag, "backpack", "backpack", 0),
            I(Bag, "tote", "tote", 30), I(Bag, "beltbag", "belt bag", 40), I(Bag, "satchel", "satchel", 60), I(Bag, "heartbag", "heart bag", 70), I(Bag, "bigpack", "trail pack", 120), I(Bag, "starbag", "star bag", 200),
            // the guitar she plays (5)
            I(Guitar, "acoustic", "acoustic", 0), I(Guitar, "electric", "electric", 0), I(Guitar, "ukulele", "ukulele", 40), I(Guitar, "bass", "bass", 90), I(Guitar, "starguitar", "star guitar", 250),
            // riff: collars, bandanas and more (bandana, beanie, shades, phones and collar are the old backstage ids, so what she bought there stays hers)
            I(Dog, "none", "just riff", 0), I(Dog, "bowtie", "bow tie", 0),
            I(Dog, "bandana", "bandana", 25), I(Dog, "beanie", "beanie", 30), I(Dog, "shades", "shades", 30), I(Dog, "scarf", "scarf", 40), I(Dog, "partyhat", "party hat", 35),
            I(Dog, "flowers", "flower collar", 40), I(Dog, "phones", "headphones", 60), I(Dog, "collar", "spiked collar", 60), I(Dog, "crown", "crown", 220),
            // faces (8): four free friendly starters, four to buy; drawn as a decal on her head
            I(Face, "smile", "smile", 0), I(Face, "bigsmile", "big smile", 0), I(Face, "wink", "wink", 0), I(Face, "freckles", "freckles", 0),
            I(Face, "happy", "happy eyes", 25), I(Face, "cheeky", "cheeky", 30), I(Face, "blush", "rosy cheeks", 40), I(Face, "starry", "star eyes", 60),
            // stage lights (the old ids, so old purchases carry over)
            I(Lights, "tour", "tour mix", 0), I(Lights, "pink", "hot pink", 40), I(Lights, "cyan", "ice blue", 60), I(Lights, "gold", "gold", 90), I(Lights, "violet", "violet", 110),
        };

        static Dictionary<string, AvatarItem> s_ById;

        /// <summary>The item with this id, or null.</summary>
        public static AvatarItem ById(string id)
        {
            if (id == null) return null;
            if (s_ById == null)
            {
                var d = new Dictionary<string, AvatarItem>();
                foreach (AvatarItem i in All) d[i.Id] = i;
                s_ById = d;
            }
            AvatarItem it;
            return s_ById.TryGetValue(id, out it) ? it : null;
        }

        /// <summary>The items of one slot, in catalogue order.</summary>
        public static List<AvatarItem> InSlot(string slot)
        {
            var l = new List<AvatarItem>();
            foreach (AvatarItem i in All) if (i.Slot == slot) l.Add(i);
            return l;
        }

        /// <summary>The first free item of a slot (what a slot falls back to).</summary>
        public static AvatarItem Starter(string slot)
        {
            foreach (AvatarItem i in All) if (i.Slot == slot && i.StartsOwned) return i;
            return null;
        }

        /// <summary>"zipjacket" from "top:zipjacket".</summary>
        public static string Key(string id)
        {
            int c = id == null ? -1 : id.IndexOf(':');
            return c < 0 ? id : id.Substring(c + 1);
        }

        /// <summary>The palette colour nearest an old swatch (a "#rrggbb"), by plain RGB distance. Used to carry old outfits, hair and guitar skins over.</summary>
        public static int NearestColor(string hex)
        {
            int r, g, b;
            if (!Parse(hex, out r, out g, out b)) return Pink;
            int best = 0; long bd = long.MaxValue;
            for (int i = 0; i < PaletteHex.Count; i++)
            {
                int pr, pg, pb;
                Parse(PaletteHex[i], out pr, out pg, out pb);
                long d = (long)(pr - r) * (pr - r) + (long)(pg - g) * (pg - g) + (long)(pb - b) * (pb - b);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        static bool Parse(string hex, out int r, out int g, out int b)
        {
            r = g = b = 0;
            if (hex == null || hex.Length != 7 || hex[0] != '#') return false;
            try
            {
                r = System.Convert.ToInt32(hex.Substring(1, 2), 16);
                g = System.Convert.ToInt32(hex.Substring(3, 2), 16);
                b = System.Convert.ToInt32(hex.Substring(5, 2), 16);
                return true;
            }
            catch (System.FormatException) { return false; }
        }
    }
}
