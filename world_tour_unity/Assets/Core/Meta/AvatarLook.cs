using System.Collections.Generic;

namespace WorldTour.Core.Meta
{
    /// <summary>One slot of a look: the item id ("top:hoodie"), a palette colour index and a pattern index.</summary>
    public sealed class AvatarPick
    {
        public string Id;
        public int Color;
        public int Pattern;

        public AvatarPick() { }
        public AvatarPick(string id, int color = 0, int pattern = 0) { Id = id; Color = color; Pattern = pattern; }

        public AvatarPick Clone() { return new AvatarPick(Id, Color, Pattern); }

        public bool Same(AvatarPick o) { return o != null && Id == o.Id && Color == o.Color && Pattern == o.Pattern; }

        public override string ToString() { return Id + "/" + Color + "/" + Pattern; }
    }

    /// <summary>
    /// Phase 10B.1: everything she wears and carries. Per slot an item id with a colour index and a pattern index, plus her skin tone and hair colour
    /// (indices into <see cref="AvatarCatalogue.SkinHex"/> and <see cref="AvatarCatalogue.PaletteHex"/>). Plain data: no engine types. A look with a
    /// missing or unknown slot is fixed by <see cref="AvatarRules.Clean"/>.
    /// </summary>
    public sealed class AvatarLook
    {
        public AvatarPick Top, Bottom, Shoes, Hair, Hat, Glasses, Bag, Guitar, Dog, Lights, Face;
        public int Skin;
        public int HairColor = AvatarCatalogue.Chestnut;

        /// <summary>What she has worn since the start: a pink zip jacket, navy jeans, boots, the bob in chestnut, her usual guitar, no hat.</summary>
        public static AvatarLook Default()
        {
            return new AvatarLook
            {
                Top = new AvatarPick("top:zipjacket", AvatarCatalogue.Pink),
                Bottom = new AvatarPick("bottom:jeans", AvatarCatalogue.Navy),
                Shoes = new AvatarPick("shoes:boots", AvatarCatalogue.Brown),
                Hair = new AvatarPick("hair:bob", AvatarCatalogue.Chestnut),
                Hat = new AvatarPick("hat:none", 0),
                Glasses = new AvatarPick("glasses:none", 0),
                Bag = new AvatarPick("bag:none", 0),
                Guitar = new AvatarPick("guitar:acoustic", AvatarCatalogue.Honey),
                Dog = new AvatarPick("dog:none", 0),
                Lights = new AvatarPick("lights:tour", 0),
                Face = new AvatarPick("face:smile", 0),
                Skin = 0,
                HairColor = AvatarCatalogue.Chestnut,
            };
        }

        public AvatarPick Get(string slot)
        {
            switch (slot)
            {
                case AvatarCatalogue.Top: return Top;
                case AvatarCatalogue.Bottom: return Bottom;
                case AvatarCatalogue.Shoes: return Shoes;
                case AvatarCatalogue.Hair: return Hair;
                case AvatarCatalogue.Hat: return Hat;
                case AvatarCatalogue.Glasses: return Glasses;
                case AvatarCatalogue.Bag: return Bag;
                case AvatarCatalogue.Guitar: return Guitar;
                case AvatarCatalogue.Dog: return Dog;
                case AvatarCatalogue.Lights: return Lights;
                case AvatarCatalogue.Face: return Face;
            }
            return null;
        }

        public void Set(string slot, AvatarPick p)
        {
            switch (slot)
            {
                case AvatarCatalogue.Top: Top = p; break;
                case AvatarCatalogue.Bottom: Bottom = p; break;
                case AvatarCatalogue.Shoes: Shoes = p; break;
                case AvatarCatalogue.Hair: Hair = p; break;
                case AvatarCatalogue.Hat: Hat = p; break;
                case AvatarCatalogue.Glasses: Glasses = p; break;
                case AvatarCatalogue.Bag: Bag = p; break;
                case AvatarCatalogue.Guitar: Guitar = p; break;
                case AvatarCatalogue.Dog: Dog = p; break;
                case AvatarCatalogue.Lights: Lights = p; break;
                case AvatarCatalogue.Face: Face = p; break;
            }
        }

        public AvatarLook Clone()
        {
            var l = new AvatarLook { Skin = Skin, HairColor = HairColor };
            foreach (string s in AvatarCatalogue.Slots) { AvatarPick p = Get(s); l.Set(s, p == null ? null : p.Clone()); }
            return l;
        }

        public bool Same(AvatarLook o)
        {
            if (o == null || Skin != o.Skin || HairColor != o.HairColor) return false;
            foreach (string s in AvatarCatalogue.Slots)
            {
                AvatarPick a = Get(s), b = o.Get(s);
                if (a == null ? b != null : !a.Same(b)) return false;
            }
            return true;
        }

        /// <summary>One line for logs and tests: "top=zipjacket/0/0 bottom=... skin=0 hair=18".</summary>
        public override string ToString()
        {
            var parts = new List<string>();
            foreach (string s in AvatarCatalogue.Slots)
            {
                AvatarPick p = Get(s);
                parts.Add(s + "=" + (p == null ? "-" : AvatarCatalogue.Key(p.Id) + "/" + p.Color + "/" + p.Pattern));
            }
            parts.Add("skin=" + Skin); parts.Add("hair=" + HairColor);
            return string.Join(" ", parts);
        }
    }

    /// <summary>One of her five saved outfits: a word from <see cref="AvatarCatalogue.OutfitWords"/> and the look.</summary>
    public sealed class AvatarOutfit
    {
        public string Name;
        public AvatarLook Look;
    }
}
