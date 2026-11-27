using System.Collections.Generic;
using WorldTour.Core.Progress;

namespace WorldTour.Core.Meta
{
    /// <summary>Why a piece can or cannot stand where it is held.</summary>
    public enum PlaceCheck { Ok, Unknown, OutOfRoom, Reserved, Overlap, Locked, NotOwned }

    /// <summary>A rectangle of room cells.</summary>
    public struct HomeRect
    {
        public int X, Z, W, D;
        public HomeRect(int x, int z, int w, int d) { X = x; Z = z; W = w; D = d; }
        public bool Hits(HomeRect o) { return X < o.X + o.W && o.X < X + W && Z < o.Z + o.D && o.Z < Z + D; }
    }

    /// <summary>
    /// Phase 10B.2: her home. Pure functions over the save (the 3D scene only draws them). A room is a square of 24 x 24 cells of 0.5 m (12 m); cell (0,0) is the
    /// south-west corner, x grows east, z grows toward the back wall. A piece stands on the grid with its footprint (turned in 90 degree steps) inside the room,
    /// clear of the doors, the way out and the box, and never on another piece of the same layer. Floor pieces never overlap each other,
    /// rugs lie under them (and only never overlap other rugs), posters hang on the back wall (and never overlap other posters).
    /// Furniture is bought in the put-away box while decorating (<see cref="BoxList"/>, <see cref="Buy"/>): coins only, then it is in the box like any piece she owns.
    /// Rooms: 0 her room (free), 1 den, 2 studio, 3 garden (bought with coins, 200 to 350).
    /// </summary>
    public static class HomeRules
    {
        public const int Cells = 24;
        public const float CellSize = 0.5f;
        public const int RoomCount = 4;
        public static readonly IReadOnlyList<string> RoomNames = new[] { "my room", "den", "studio", "garden" };
        public static readonly IReadOnlyList<long> RoomPrices = new long[] { 0, 200, 250, 350 };

        /// <summary>The room a door leads to (west or east side), or -1 for none. Her room opens west to the studio and east to the den; the den east to the garden.</summary>
        public static int DoorTarget(int room, bool east)
        {
            switch (room)
            {
                case 0: return east ? 1 : 2;
                case 1: return east ? 3 : 0;
                case 2: return east ? 0 : -1;
                case 3: return east ? -1 : 1;
            }
            return -1;
        }

        /// <summary>Floor cells nobody may put a piece on: the doors, the put-away box and (her room) the way out.</summary>
        public static List<HomeRect> ReservedFloor(int room)
        {
            var l = new List<HomeRect>();
            l.Add(new HomeRect(19, 0, 3, 3));   // the put-away box
            if (room == 0) l.Add(new HomeRect(9, 0, 6, 4));   // the way out
            if (DoorTarget(room, false) >= 0) l.Add(new HomeRect(0, 10, 3, 5));
            if (DoorTarget(room, true) >= 0) l.Add(new HomeRect(21, 10, 3, 5));
            return l;
        }

        /// <summary>Back-wall cell ranges kept free: none since the wrap-up (the ticket wall and sticker board were dropped), kept so posters still ask.</summary>
        public static List<HomeRect> ReservedWall(int room)
        {
            return new List<HomeRect>();
        }

        public static bool RoomUnlocked(SaveBlob s, int room) { return room == 0 || s.Home.Rooms.Contains(room); }

        public static string RoomName(int room) { return room >= 0 && room < RoomCount ? RoomNames[room] : "room"; }

        /// <summary>Buys a room with coins. False (nothing changes) when it is not a room for sale, already hers, or she is short.</summary>
        public static bool UnlockRoom(SaveBlob s, int room)
        {
            if (room < 1 || room >= RoomCount || RoomUnlocked(s, room)) return false;
            if (!CoinRules.TrySpend(s, RoomPrices[room])) return false;
            s.Home.Rooms.Add(room);
            return true;
        }

        // ---------- owning ----------

        public static int Count(SaveBlob s, string id)
        {
            int n = 0;
            foreach (string o in s.Home.Owned) if (o == id) n++;
            return n;
        }

        public static int PlacedCount(SaveBlob s, string id)
        {
            int n = 0;
            foreach (HomeItem it in s.Home.Placed) if (it.Id == id) n++;
            return n;
        }

        /// <summary>How many of a piece are in the put-away box.</summary>
        public static int Boxed(SaveBlob s, string id) { return Count(s, id) - PlacedCount(s, id); }

        public static bool Owns(SaveBlob s, string id) { return HomeCatalogue.ById(id) != null && Count(s, id) > 0; }

        /// <summary>Gives her one more of a piece (it goes in the put-away box). No spending: the furniture store charges coins first. An unknown id is ignored (false).</summary>
        public static bool Grant(SaveBlob s, string id)
        {
            if (HomeCatalogue.ById(id) == null) return false;
            s.Home.Owned.Add(id);
            return true;
        }

        /// <summary>Buys a piece: coins first, then it is in the box. Nothing changes when she is short.</summary>
        public static bool Buy(SaveBlob s, string id)
        {
            HomePiece p = HomeCatalogue.ById(id);
            if (p == null || p.Price <= 0 || !CoinRules.TrySpend(s, p.Price)) return false;
            return Grant(s, id);
        }

        /// <summary>Whether the box offers a piece for sale: a real piece that is not a free starter and that she has none of in the box.</summary>
        public static bool ForSale(SaveBlob s, string id)
        {
            HomePiece p = HomeCatalogue.ById(id);
            return p != null && !p.Starter && Boxed(s, id) <= 0;
        }

        /// <summary>How many more coins she needs for a piece (0 when she has enough).</summary>
        public static long Short(SaveBlob s, string id)
        {
            HomePiece p = HomeCatalogue.ById(id);
            return p == null ? 0 : System.Math.Max(0, p.Price - CoinRules.Coins(s));
        }

        /// <summary>What the box shows for a group: the pieces in it first, then the ones she can buy (catalogue order both times).</summary>
        public static List<HomePiece> BoxList(SaveBlob s, string category)
        {
            var l = new List<HomePiece>();
            List<HomePiece> all = HomeCatalogue.InCategory(category);
            foreach (HomePiece p in all) if (Boxed(s, p.Id) > 0) l.Add(p);
            foreach (HomePiece p in all) if (ForSale(s, p.Id)) l.Add(p);
            return l;
        }

        /// <summary>The small line under a piece in the box: "in the box" for hers, else its price ("90 coins").</summary>
        public static string BoxTag(SaveBlob s, HomePiece p) { return Boxed(s, p.Id) > 0 ? InBox : BoutiqueRules.Price(p.Price); }

        // ---------- the grid ----------

        public static void Footprint(HomePiece p, int rot, out int w, out int d)
        {
            if (p.Layer == HomeLayer.Wall) { w = p.W; d = 1; return; }
            bool turn = (rot & 1) == 1;
            w = turn ? p.D : p.W;
            d = turn ? p.W : p.D;
        }

        /// <summary>The world position (metres) of a cell's corner: room cells run from -6 to 6 in x and z.</summary>
        public static float WorldX(float cellX) { return cellX * CellSize - Cells * CellSize / 2f; }
        public static float WorldZ(float cellZ) { return cellZ * CellSize - Cells * CellSize / 2f; }
        /// <summary>The cell a world coordinate falls in (floor division).</summary>
        public static int CellOf(float world) { return (int)System.Math.Floor((world + Cells * CellSize / 2f) / CellSize); }

        /// <summary>Whether a piece could stand here. <paramref name="ignore"/> is the index of a placed piece that is being moved (it does not count), or -1.</summary>
        public static PlaceCheck Check(SaveBlob s, string id, int x, int z, int rot, int room, int ignore)
        {
            HomePiece p = HomeCatalogue.ById(id);
            if (p == null) return PlaceCheck.Unknown;
            if (!RoomUnlocked(s, room)) return PlaceCheck.Locked;
            int w, d;
            Footprint(p, rot, out w, out d);
            if (p.Layer == HomeLayer.Wall) z = Cells - 1;
            if (x < 0 || z < 0 || x + w > Cells || z + d > Cells) return PlaceCheck.OutOfRoom;
            var me = new HomeRect(x, z, w, d);
            if (p.Layer == HomeLayer.Wall) { foreach (HomeRect r in ReservedWall(room)) if (me.Hits(r)) return PlaceCheck.Reserved; }
            else if (p.Layer == HomeLayer.Floor) { foreach (HomeRect r in ReservedFloor(room)) if (me.Hits(r)) return PlaceCheck.Reserved; }
            for (int i = 0; i < s.Home.Placed.Count; i++)
            {
                if (i == ignore) continue;
                HomeItem o = s.Home.Placed[i];
                if (o.Room != room) continue;
                HomePiece op = HomeCatalogue.ById(o.Id);
                if (op == null || op.Layer != p.Layer) continue;
                int ow, od;
                Footprint(op, o.Rot, out ow, out od);
                int oz = op.Layer == HomeLayer.Wall ? Cells - 1 : o.Z;
                if (me.Hits(new HomeRect(o.X, oz, ow, od))) return PlaceCheck.Overlap;
            }
            return PlaceCheck.Ok;
        }

        /// <summary>Stands a piece from the box in a room. Not hers or none left in the box: <see cref="PlaceCheck.NotOwned"/>.</summary>
        public static PlaceCheck Place(SaveBlob s, string id, int x, int z, int rot, int room)
        {
            if (HomeCatalogue.ById(id) == null) return PlaceCheck.Unknown;
            if (Boxed(s, id) <= 0) return PlaceCheck.NotOwned;
            PlaceCheck c = Check(s, id, x, z, rot, room, -1);
            if (c != PlaceCheck.Ok) return c;
            HomePiece p = HomeCatalogue.ById(id);
            s.Home.Placed.Add(new HomeItem { Id = id, X = x, Z = p.Layer == HomeLayer.Wall ? Cells - 1 : z, Rot = p.Layer == HomeLayer.Wall ? 0 : rot & 3, Room = room });
            return PlaceCheck.Ok;
        }

        /// <summary>Moves (and turns) a placed piece. Anything but Ok changes nothing.</summary>
        public static PlaceCheck Move(SaveBlob s, int index, int x, int z, int rot, int room)
        {
            if (index < 0 || index >= s.Home.Placed.Count) return PlaceCheck.Unknown;
            HomeItem it = s.Home.Placed[index];
            PlaceCheck c = Check(s, it.Id, x, z, rot, room, index);
            if (c != PlaceCheck.Ok) return c;
            HomePiece p = HomeCatalogue.ById(it.Id);
            it.X = x; it.Z = p.Layer == HomeLayer.Wall ? Cells - 1 : z; it.Rot = p.Layer == HomeLayer.Wall ? 0 : rot & 3; it.Room = room;
            return PlaceCheck.Ok;
        }

        /// <summary>Puts a placed piece back in the box (she still owns it).</summary>
        public static bool PutAway(SaveBlob s, int index)
        {
            if (index < 0 || index >= s.Home.Placed.Count) return false;
            s.Home.Placed.RemoveAt(index);
            return true;
        }

        /// <summary>The indices of the pieces standing in a room, in the order she put them down.</summary>
        public static List<int> InRoom(SaveBlob s, int room)
        {
            var l = new List<int>();
            for (int i = 0; i < s.Home.Placed.Count; i++) if (s.Home.Placed[i].Room == room) l.Add(i);
            return l;
        }

        // ---------- colors ----------

        public static bool SetWall(SaveBlob s, int color) { if (color < -1 || color >= AvatarCatalogue.PaletteHex.Count) return false; s.Home.Wall = color; return true; }
        public static bool SetFloor(SaveBlob s, int color) { if (color < -1 || color >= AvatarCatalogue.PaletteHex.Count) return false; s.Home.Floor = color; return true; }

        // ---------- the starters ----------

        /// <summary>
        /// The first time she is home: the free bed, rug, lamp and riff's bed are hers and stand in her room, so it is not empty. Does nothing after that
        /// (so a piece she put away stays in the box). Returns true when it did something.
        /// </summary>
        public static bool EnsureStarters(SaveBlob s)
        {
            if (s.Home.Started) return false;
            s.Home.Started = true;
            foreach (string id in HomeCatalogue.Starters) if (Count(s, id) == 0) Grant(s, id);
            Place(s, "furn:rug_round", 9, 8, 0, 0);
            Place(s, "furn:bed_single", 2, 16, 0, 0);
            Place(s, "furn:lamp_floor", 6, 21, 0, 0);
            Place(s, "furn:riff_bed", 17, 17, 0, 0);
            return true;
        }

        // ---------- the words (lower case, 12 words or fewer) ----------

        public const string DoorOutside = "my home";
        public const string DoorLine = "your place. decorate it!";
        public const string DoorGoIn = "go in";
        public const string DoorStay = "not now";
        public const string LeaveLine = "back out to the street?";
        public const string LeaveGo = "go out";
        public const string LeaveStay = "stay in";
        public const string Decorate = "decorate";
        public const string BoxName = "put-away box";
        public const string BoxEmpty = "the box is empty.";
        public const string KeysLine = "arrows move, r turns, enter drops, delete puts away.";
        public const string KeysLine2 = "tab picks the next piece. esc is done.";
        public const string KeysBox = "left and right pick, up and down switch group. enter chooses.";
        public const string InBox = "in the box";
        public const string Bought = "it is yours! now place it.";
        public const string KeysColor = "left and right: wall. up and down: floor.";
        public const string NoRoom = "no room there.";
        public const string Reserved = "that spot is kept free.";
        public const string Dropped = "placed!";
        public const string PutBack = "in the box.";
        public const string NothingHeld = "pick a piece first.";

        public static string RoomLine(int room) { return "a new room! " + BoutiqueRules.Price(RoomPrices[room]) + "."; }
        public static string UnlockLabel(int room) { return "buy " + BoutiqueRules.Price(RoomPrices[room]); }
        public static string RoomPin(int room, bool open) { return open ? "to " + RoomName(room) : RoomName(room) + " - " + BoutiqueRules.Price(RoomPrices[room]); }

        public static string Why(PlaceCheck c)
        {
            switch (c)
            {
                case PlaceCheck.Reserved: return Reserved;
                case PlaceCheck.Ok: return Dropped;
                default: return NoRoom;
            }
        }

        public static IEnumerable<string> AllLines()
        {
            yield return DoorLine; yield return LeaveLine; yield return BoxEmpty; yield return KeysLine; yield return KeysLine2; yield return KeysBox; yield return KeysColor;
            yield return NoRoom; yield return Reserved; yield return Dropped; yield return PutBack; yield return NothingHeld; yield return InBox; yield return Bought;
            for (int r = 1; r < RoomCount; r++) { yield return RoomLine(r); yield return RoomPin(r, false); yield return UnlockLabel(r); }
        }
    }
}
