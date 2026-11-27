using System.Collections.Generic;
using WorldTour.Core.Maths.Gen;
using WorldTour.Core.Meta;

namespace WorldTour.Core.Progress
{
    /// <summary>
    /// Phase 10B.2: the "home" key, compact. <c>{"box":["furn:chair*3"],"put":[["furn:sofa",4,10,1,0]],"wall":3,"floor":9,"rooms":[1,2],"st":1}</c>.
    /// "put" items are [id, x, z, rot, room]. "box" lists the pieces she owns that are not standing anywhere (the put-away box), each id once, with "*n" after it when
    /// there are n. What she owns is the box plus the placed pieces, so nothing is written twice. Unknown keys inside are kept.
    /// </summary>
    public static class HomeJson
    {
        static readonly string[] Keys = { "box", "put", "wall", "floor", "rooms", "st" };

        public static HomeData Read(JsonValue v)
        {
            var d = new HomeData();
            if (v == null || v.Kind != JsonKind.Object) return d;
            JsonValue own = v["box"];
            if (own != null && own.Kind == JsonKind.Array)
                foreach (JsonValue x in own.Items)
                {
                    if (x.Kind != JsonKind.String) continue;
                    string s = x.String;
                    int n = 1, star = s.IndexOf('*');
                    if (star > 0)
                    {
                        if (!int.TryParse(s.Substring(star + 1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out n)) n = 1;
                        s = s.Substring(0, star);
                    }
                    if (HomeCatalogue.ById(s) == null) continue;
                    for (int i = 0; i < n && i < 99; i++) d.Owned.Add(s);
                }
            JsonValue put = v["put"];
            if (put != null && put.Kind == JsonKind.Array)
                foreach (JsonValue it in put.Items)
                {
                    if (it.Kind != JsonKind.Array || it.Count < 4) continue;
                    string id = JsonTree.Str(it[0]);
                    if (id == null || HomeCatalogue.ById(id) == null) continue;
                    d.Placed.Add(new HomeItem { Id = id, X = JsonTree.Int(it[1], 0), Z = JsonTree.Int(it[2], 0), Rot = ((JsonTree.Int(it[3], 0) % 4) + 4) % 4, Room = it.Count > 4 ? JsonTree.Int(it[4], 0) : 0 });
                }
            foreach (HomeItem it in d.Placed) d.Owned.Add(it.Id);
            d.Wall = Palette(JsonTree.Int(v["wall"], -1));
            d.Floor = Palette(JsonTree.Int(v["floor"], -1));
            JsonValue rooms = v["rooms"];
            if (rooms != null && rooms.Kind == JsonKind.Array)
                foreach (JsonValue r in rooms.Items)
                {
                    int n = JsonTree.Int(r, 0);
                    if (n >= 1 && n < HomeRules.RoomCount && !d.Rooms.Contains(n)) d.Rooms.Add(n);
                }
            d.Started = JsonTree.Int(v["st"], 0) != 0;
            d.Extra = SaveJson.Unknown(v, Keys);
            return d;
        }

        static int Palette(int n) { return n >= 0 && n < AvatarCatalogue.PaletteHex.Count ? n : -1; }

        /// <summary>The stored object, or null when there is nothing to store (an old save's text then does not change).</summary>
        public static JsonValue Write(HomeData d)
        {
            if (d == null || d.IsEmpty) return null;
            var o = JsonValue.Obj();
            var boxed = new List<JsonValue>();
            foreach (HomePiece p in HomeCatalogue.All)
            {
                int n = 0;
                foreach (string x in d.Owned) if (x == p.Id) n++;
                foreach (HomeItem it in d.Placed) if (it.Id == p.Id) n--;
                if (n > 0) boxed.Add(JsonValue.Str(n > 1 ? p.Id + "*" + n.ToString(System.Globalization.CultureInfo.InvariantCulture) : p.Id));
            }
            if (boxed.Count > 0) o.Set("box", JsonValue.Arr(boxed));
            if (d.Placed.Count > 0)
            {
                var list = new List<JsonValue>();
                foreach (HomeItem it in d.Placed)
                    list.Add(JsonValue.Arr(JsonValue.Str(it.Id), JsonValue.Num(it.X), JsonValue.Num(it.Z), JsonValue.Num(it.Rot), JsonValue.Num(it.Room)));
                o.Set("put", JsonValue.Arr(list));
            }
            if (d.Wall >= 0) o.Set("wall", d.Wall);
            if (d.Floor >= 0) o.Set("floor", d.Floor);
            if (d.Rooms.Count > 0)
            {
                var list = new List<JsonValue>();
                foreach (int r in d.Rooms) list.Add(JsonValue.Num(r));
                o.Set("rooms", JsonValue.Arr(list));
            }
            if (d.Started) o.Set("st", 1);
            if (d.Extra != null) foreach (string k in d.Extra.Keys) if (!o.Has(k)) o.Set(k, d.Extra[k]);
            return o;
        }
    }
}
