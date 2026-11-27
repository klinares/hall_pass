using System.Collections.Generic;

namespace WorldTour.Core.Meta
{
    /// <summary>Where a piece sits: on the floor, flat on the floor under the others (rugs), or on the back wall.</summary>
    public enum HomeLayer { Floor, Rug, Wall }

    /// <summary>
    /// Phase 10B.2: one piece of furniture or decoration. Ids are "furn:key". W and D are the footprint in 0.5 m cells at rotation 0 (W along x, D along z);
    /// H is the height in metres (used to pick a piece with the mouse). Price 0 is a free starter.
    /// </summary>
    public sealed class HomePiece
    {
        public string Id, Name, Category;
        public long Price;
        public int W, D;
        public float H;
        public HomeLayer Layer;
        /// <summary>Tall pieces stop her walking through them; rugs, posters and low things do not.</summary>
        public bool Blocks;
        public bool Starter { get { return Price == 0; } }
    }

    /// <summary>
    /// The furniture catalogue, plain data. Price tiers (the money rule): small 20 to 40, mid 60 to 120, big 200 to 350.
    /// The 3D model of each id is built by InteriorHomeBuilder (KayKit Furniture, Restaurant, City and Forest pieces plus primitives, all CC0).
    /// </summary>
    public static class HomeCatalogue
    {
        public const string Seating = "seating", Tables = "tables", Beds = "beds", Lamps = "lamps", Plants = "plants", Rugs = "rugs",
            Posters = "posters", Shelves = "shelves", Riff = "riff", Decor = "decor";

        public static readonly IReadOnlyList<string> Categories = new[] { Seating, Tables, Beds, Lamps, Plants, Rugs, Posters, Shelves, Riff, Decor };
        public static readonly IReadOnlyList<string> CategoryNames = new[] { "seating", "tables", "beds", "lamps", "plants", "rugs", "posters", "shelves", "riff's bed", "decor" };

        public static string CategoryName(string cat)
        {
            for (int i = 0; i < Categories.Count; i++) if (Categories[i] == cat) return CategoryNames[i];
            return cat;
        }

        static HomePiece P(string key, string name, string cat, long price, int w, int d, float h, HomeLayer layer = HomeLayer.Floor, bool blocks = true)
        {
            return new HomePiece { Id = "furn:" + key, Name = name, Category = cat, Price = price, W = w, D = d, H = h, Layer = layer, Blocks = blocks && layer == HomeLayer.Floor };
        }

        static HomePiece Poster(string key, string name, long price) { return P(key, name, Posters, price, 3, 1, 2f, HomeLayer.Wall, false); }

        public static readonly IReadOnlyList<HomePiece> All = new[]
        {
            P("sofa", "sofa", Seating, 90, 4, 2, 1.0f),
            P("armchair", "armchair", Seating, 60, 2, 2, 1.0f),
            P("chair", "chair", Seating, 25, 1, 1, 1.0f),
            P("bench", "bench", Seating, 35, 3, 1, 0.9f),
            P("beanbag", "beanbag", Seating, 30, 2, 2, 0.6f),
            P("stool", "stool", Seating, 20, 1, 1, 0.6f),

            P("table_low", "low table", Tables, 30, 3, 2, 0.5f),
            P("table_round", "round table", Tables, 40, 2, 2, 0.9f),
            P("desk", "desk", Tables, 80, 4, 2, 1.0f),
            P("table_dining", "dining table", Tables, 100, 4, 3, 0.9f),
            P("table_side", "side table", Tables, 20, 1, 1, 0.6f),

            P("bed_single", "cozy bed", Beds, 0, 4, 6, 0.8f),
            P("bed_day", "day bed", Beds, 110, 4, 4, 0.7f),
            P("bed_big", "big bed", Beds, 300, 6, 6, 0.9f),
            P("bed_bunk", "bunk bed", Beds, 260, 4, 6, 1.8f),

            P("lamp_floor", "floor lamp", Lamps, 0, 1, 1, 1.8f),
            P("lamp_table", "table lamp", Lamps, 20, 1, 1, 0.6f),
            P("lamp_globe", "globe lamp", Lamps, 60, 1, 1, 1.4f),
            P("lamp_star", "star lamp", Lamps, 40, 1, 1, 1.5f),

            P("cactus", "tall cactus", Plants, 25, 1, 1, 1.0f),
            P("cactus_small", "small cactus", Plants, 20, 1, 1, 0.5f),
            P("cactus_tiny", "tiny cactus", Plants, 20, 1, 1, 0.5f),
            P("bush", "bush", Plants, 60, 2, 2, 1.0f),
            P("tree", "little tree", Plants, 100, 3, 3, 3.0f),
            P("fern", "fern pot", Plants, 25, 1, 1, 0.9f),
            P("flowers", "flower pot", Plants, 30, 1, 1, 0.8f),

            P("rug_round", "round rug", Rugs, 0, 6, 6, 0.05f, HomeLayer.Rug),
            P("rug_big", "big rug", Rugs, 70, 8, 6, 0.05f, HomeLayer.Rug),
            P("rug_star", "star rug", Rugs, 80, 6, 6, 0.05f, HomeLayer.Rug),
            P("rug_runner", "runner", Rugs, 40, 8, 2, 0.05f, HomeLayer.Rug),

            Poster("poster_heart", "heart poster", 20),
            Poster("poster_star", "star poster", 20),
            Poster("poster_note", "music poster", 25),
            Poster("poster_hills", "hills poster", 25),
            Poster("poster_guitar", "guitar poster", 30),
            Poster("poster_map", "world map", 35),

            P("shelf_books", "bookcase", Shelves, 70, 4, 1, 2.0f),
            P("shelf_cubby", "cubby shelf", Shelves, 60, 2, 1, 1.2f),
            P("shelf_guitar", "guitar stand", Shelves, 80, 2, 1, 1.3f),
            P("shelf_crates", "crate stack", Shelves, 25, 1, 1, 1.0f),
            P("shelf_jars", "jar shelf", Shelves, 65, 3, 1, 1.6f),

            P("riff_bed", "riff's bed", Riff, 0, 2, 2, 0.4f),
            P("riff_cushion", "square cushion", Riff, 30, 2, 2, 0.3f),
            P("riff_house", "dog house", Riff, 110, 3, 3, 1.4f),
            P("riff_bowls", "riff's bowls", Riff, 20, 1, 1, 0.3f),
            P("riff_toys", "toy basket", Riff, 25, 1, 1, 0.5f),

            P("jar_big", "big jar", Decor, 20, 1, 1, 1.0f),
            P("jar_small", "small jar", Decor, 20, 1, 1, 0.6f),
            P("menu", "chalk board", Decor, 30, 1, 1, 1.4f),
            P("globe", "globe", Decor, 60, 1, 1, 1.0f),
            P("trophy", "trophy", Decor, 40, 1, 1, 0.8f),
            P("speaker", "speaker", Decor, 60, 1, 1, 1.2f),
            P("keys", "keyboard", Decor, 120, 3, 1, 1.0f),
            P("drums", "drum set", Decor, 200, 3, 3, 1.2f),
            P("tank", "fish tank", Decor, 220, 3, 1, 1.2f),
        };

        static Dictionary<string, HomePiece> _byId;

        public static HomePiece ById(string id)
        {
            if (id == null) return null;
            if (_byId == null)
            {
                var d = new Dictionary<string, HomePiece>();
                foreach (HomePiece p in All) d[p.Id] = p;
                _byId = d;
            }
            HomePiece r;
            return _byId.TryGetValue(id, out r) ? r : null;
        }

        public static List<HomePiece> InCategory(string cat)
        {
            var l = new List<HomePiece>();
            foreach (HomePiece p in All) if (p.Category == cat) l.Add(p);
            return l;
        }

        /// <summary>The free pieces she starts with: a bed, a rug, a lamp and riff's bed.</summary>
        public static readonly IReadOnlyList<string> Starters = new[] { "furn:bed_single", "furn:rug_round", "furn:lamp_floor", "furn:riff_bed" };
    }
}
