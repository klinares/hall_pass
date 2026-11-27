using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WorldTour.Core.Maths.Gen;
using WorldTour.Core.Meta;

namespace WorldTour.Core.Progress
{
    /// <summary>
    /// Phase 10B.1: the "avatar" key of the save, compact. <c>{"owned":["top:hoodie"],"look":{"top":"top:zipjacket|0|0","hat":"hat:cap|7|0","skin":2,"hc":18},"outfits":[{"n":"sunny","look":{...}},null,...]}</c>.
    /// A slot is "id|colour|pattern". Unknown keys inside are kept.
    /// </summary>
    public static class AvatarJson
    {
        static readonly string[] Keys = { "owned", "look", "outfits" };
        static readonly string[] LookKeys = { "top", "bottom", "shoes", "hair", "hat", "glasses", "bag", "guitar", "dog", "lights", "skin", "hc" };

        public static AvatarData Read(JsonValue v)
        {
            var d = new AvatarData();
            if (v == null || v.Kind != JsonKind.Object) return d;
            JsonValue owned = v["owned"];
            if (owned != null && owned.Kind == JsonKind.Array)
                foreach (JsonValue x in owned.Items)
                    if (x.Kind == JsonKind.String && AvatarCatalogue.ById(x.String) != null && !d.Owned.Contains(x.String)) d.Owned.Add(x.String);
            d.Look = ReadLook(v["look"]);
            JsonValue outs = v["outfits"];
            if (outs != null && outs.Kind == JsonKind.Array)
                for (int i = 0; i < AvatarCatalogue.OutfitSlots && i < outs.Count; i++)
                {
                    JsonValue o = outs[i];
                    if (o == null || o.Kind != JsonKind.Object) continue;
                    string n = JsonTree.Str(o["n"]);
                    AvatarLook l = ReadLook(o["look"]);
                    if (n != null && l != null && AvatarCatalogue.OutfitWords.Contains(n)) d.Outfits[i] = new AvatarOutfit { Name = n, Look = l };
                }
            d.Extra = SaveJson.Unknown(v, Keys);
            return d;
        }

        /// <summary>The look in a stored object, or null when it is not an object. Slots that are missing or unreadable stay at the starter.</summary>
        static AvatarLook ReadLook(JsonValue v)
        {
            if (v == null || v.Kind != JsonKind.Object) return null;
            AvatarLook l = AvatarLook.Default();
            foreach (string slot in AvatarCatalogue.Slots)
            {
                string s = JsonTree.Str(v[slot]);
                if (s == null) continue;
                string[] part = s.Split('|');
                if (AvatarCatalogue.ById(part[0]) == null) continue;
                l.Set(slot, new AvatarPick(part[0], part.Length > 1 ? ParseInt(part[1]) : 0, part.Length > 2 ? ParseInt(part[2]) : 0));
            }
            l.Skin = JsonTree.Int(v["skin"], 0);
            l.HairColor = JsonTree.Int(v["hc"], AvatarCatalogue.Chestnut);
            return l;
        }

        static int ParseInt(string s)
        {
            int n;
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : 0;
        }

        /// <summary>The stored object, or null when there is nothing to store (an old save's text then does not change).</summary>
        public static JsonValue Write(AvatarData d)
        {
            if (d == null || d.IsEmpty) return null;
            var o = JsonValue.Obj();
            var owned = new List<JsonValue>();
            foreach (string id in d.Owned) owned.Add(JsonValue.Str(id));
            o.Set("owned", JsonValue.Arr(owned));
            if (d.Look != null) o.Set("look", WriteLook(d.Look));
            bool any = false;
            foreach (AvatarOutfit x in d.Outfits) if (x != null) any = true;
            if (any)
            {
                var list = new List<JsonValue>();
                foreach (AvatarOutfit x in d.Outfits)
                    list.Add(x == null ? JsonValue.Null : JsonValue.Obj().Set("n", x.Name).Set("look", WriteLook(x.Look)));
                o.Set("outfits", JsonValue.Arr(list));
            }
            if (d.Extra != null) foreach (string k in d.Extra.Keys) if (!o.Has(k)) o.Set(k, d.Extra[k]);
            return o;
        }

        static JsonValue WriteLook(AvatarLook l)
        {
            var o = JsonValue.Obj();
            foreach (string slot in AvatarCatalogue.Slots)
            {
                AvatarPick p = l.Get(slot);
                if (p == null) continue;
                o.Set(slot, p.Id + "|" + p.Color.ToString(CultureInfo.InvariantCulture) + "|" + p.Pattern.ToString(CultureInfo.InvariantCulture));
            }
            o.Set("skin", l.Skin).Set("hc", l.HairColor);
            return o;
        }
    }
}
