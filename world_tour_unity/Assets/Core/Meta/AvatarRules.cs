using System.Collections.Generic;
using System.Linq;
using WorldTour.Core.Progress;

namespace WorldTour.Core.Meta
{
    /// <summary>
    /// Phase 10B.1: the avatar rules over the save. Pure functions, like <see cref="MetaRules"/>. Ownership only ever grows; wearing something she does not own
    /// is refused by falling back to the slot's starter; an old save's backstage items (outfits, hair colours, guitar skins, riff's outfits, stage lights) are
    /// mapped onto the new catalogue with nothing written, so the look she had is the look she gets.
    /// </summary>
    public static class AvatarRules
    {
        // ---------- ownership ----------

        /// <summary>True for a free starter, an item in her avatar list, an item the old backstage already gave her (riff's outfits and stage lights keep their ids), or one an old purchase maps to.</summary>
        public static bool Owns(SaveBlob s, string id)
        {
            AvatarItem it = AvatarCatalogue.ById(id);
            if (it == null) return false;
            if (it.StartsOwned) return true;
            if (s.Avatar.Owned.Contains(id)) return true;
            if (s.Inventory.Owned.Contains(id)) return true;
            return LegacyGrants(s).Contains(id);
        }

        /// <summary>Adds an item to what she owns. No spending (the store charges coins first). An unknown id is ignored.</summary>
        public static void Grant(SaveBlob s, string id)
        {
            if (AvatarCatalogue.ById(id) == null || Owns(s, id)) return;
            s.Avatar.Owned.Add(id);
        }

        /// <summary>The avatar items an old save's backstage purchases map to (outfits to a top and sometimes bottoms, paid hair colours to a hair style, paid guitar skins to a guitar). Derived from the old inventory every time; never stored.</summary>
        public static HashSet<string> LegacyGrants(SaveBlob s)
        {
            var g = new HashSet<string>();
            foreach (string o in s.Inventory.Owned)
            {
                switch (o)
                {
                    case "outfit:peach-navy": g.Add("top:hoodie"); break;
                    case "outfit:mint": g.Add("top:cardigan"); g.Add("bottom:joggers"); break;
                    case "outfit:sunshine": g.Add("top:raincoat"); g.Add("bottom:cargo"); break;
                    case "outfit:cherry": g.Add("top:rockerjacket"); g.Add("bottom:flares"); break;
                    case "outfit:starlight": g.Add("top:stagecoat"); g.Add("bottom:sparkleskirt"); break;
                    case "hair:black": g.Add("hair:braid"); break;
                    case "hair:blonde": g.Add("hair:long"); break;
                    case "hair:lilac": g.Add("hair:wavy"); break;
                    case "hair:cherry": g.Add("hair:spikes"); break;
                    case "guitar:lilac": case "guitar:sunburst": case "guitar:mint": g.Add("guitar:ukulele"); break;
                    case "guitar:midnight": case "guitar:cherry": g.Add("guitar:bass"); break;
                }
            }
            return g;
        }

        // ---------- the look ----------

        /// <summary>What she wears: the saved avatar look, or (a save that has never used the new editor) the one worked out from the old backstage items. Always complete and owned.</summary>
        public static AvatarLook Look(SaveBlob s)
        {
            AvatarLook l = s.Avatar.Look != null ? s.Avatar.Look.Clone() : FromOldSave(s);
            return Clean(s, l);
        }

        /// <summary>Wears a look. What she does not own, and anything unknown, falls back to the slot's starter. Also keeps the old equipped slots (riff's outfit, stage lights) in step, so the 2D backstage shows the same.</summary>
        public static void Wear(SaveBlob s, AvatarLook l)
        {
            AvatarLook c = Clean(s, l ?? AvatarLook.Default());
            s.Avatar.Look = c;
            foreach (string slot in new[] { AvatarCatalogue.Dog, AvatarCatalogue.Lights })
            {
                string id = c.Get(slot).Id;
                if (Shop.ById(id) != null && MetaRules.Owns(s, id)) s.Inventory.Equipped[slot] = AvatarCatalogue.Key(id);
            }
        }

        /// <summary>A complete, owned look: every slot filled with an item she owns (else the starter), indices in range, patterns only where a slot takes one.</summary>
        public static AvatarLook Clean(SaveBlob s, AvatarLook l)
        {
            AvatarLook d = AvatarLook.Default();
            var o = new AvatarLook
            {
                Skin = Clamp(l.Skin, AvatarCatalogue.SkinHex.Count),
                HairColor = Clamp(l.HairColor, AvatarCatalogue.PaletteHex.Count),
            };
            foreach (string slot in AvatarCatalogue.Slots)
            {
                AvatarPick p = l.Get(slot);
                AvatarItem it = p == null ? null : AvatarCatalogue.ById(p.Id);
                if (it == null || it.Slot != slot || !Owns(s, it.Id)) { o.Set(slot, d.Get(slot).Clone()); continue; }
                o.Set(slot, new AvatarPick(it.Id,
                    AvatarCatalogue.TakesColor(slot) ? Clamp(p.Color, AvatarCatalogue.PaletteHex.Count) : 0,
                    AvatarCatalogue.TakesPattern(slot) ? Clamp(p.Pattern, AvatarCatalogue.PatternNames.Count) : 0));
            }
            // hair colour is her own pick; the hair pick's colour follows it
            o.Hair.Color = o.HairColor;
            return o;
        }

        static int Clamp(int v, int count) { return v < 0 ? 0 : (v >= count ? count - 1 : v); }

        /// <summary>The look an old save has: outfit colours on the tour jacket and trousers, hair colour, guitar skin colour, riff's outfit and the stage lights she had on.</summary>
        public static AvatarLook FromOldSave(SaveBlob s)
        {
            AvatarLook l = AvatarLook.Default();
            ShopItem outfit = MetaRules.Pick(s, Shop.Outfit);
            if (outfit != null && outfit.Swatch != null && outfit.Swatch.Count >= 3)
            {
                l.Top.Color = AvatarCatalogue.NearestColor(outfit.Swatch[0]);
                l.Bottom.Color = AvatarCatalogue.NearestColor(outfit.Swatch[2]);
            }
            ShopItem hair = MetaRules.Pick(s, Shop.Hair);
            if (hair != null && hair.Color != null) l.HairColor = AvatarCatalogue.NearestColor(hair.Color);
            l.Hair.Color = l.HairColor;
            ShopItem guitar = MetaRules.Pick(s, Shop.Guitar);
            if (guitar != null && guitar.Color != null && guitar.Color.StartsWith("#")) l.Guitar.Color = AvatarCatalogue.NearestColor(guitar.Color);
            ShopItem dog = MetaRules.Pick(s, Shop.Dog);
            if (dog != null && AvatarCatalogue.ById(dog.Id) != null) l.Dog = new AvatarPick(dog.Id, 0);
            ShopItem lights = MetaRules.Pick(s, Shop.Lights);
            if (lights != null && AvatarCatalogue.ById(lights.Id) != null) l.Lights = new AvatarPick(lights.Id, 0);
            return l;
        }

        // ---------- outfits ----------

        /// <summary>The outfit in a slot (0 to 4), or null when empty.</summary>
        public static AvatarOutfit Outfit(SaveBlob s, int slot)
        {
            return slot < 0 || slot >= AvatarCatalogue.OutfitSlots ? null : s.Avatar.Outfits[slot];
        }

        /// <summary>Saves a look under a word from the fixed list. False (nothing changed) for a bad slot or a word not on the list.</summary>
        public static bool SaveOutfit(SaveBlob s, int slot, string word, AvatarLook l)
        {
            if (slot < 0 || slot >= AvatarCatalogue.OutfitSlots || word == null || !AvatarCatalogue.OutfitWords.Contains(word) || l == null) return false;
            s.Avatar.Outfits[slot] = new AvatarOutfit { Name = word, Look = Clean(s, l) };
            return true;
        }

        public static void ClearOutfit(SaveBlob s, int slot)
        {
            if (slot >= 0 && slot < AvatarCatalogue.OutfitSlots) s.Avatar.Outfits[slot] = null;
        }

        /// <summary>The outfit's look made safe to wear now (items she owns); null for an empty slot.</summary>
        public static AvatarLook OutfitLook(SaveBlob s, int slot)
        {
            AvatarOutfit o = Outfit(s, slot);
            return o == null ? null : Clean(s, o.Look);
        }

        /// <summary>The next word on the list after this one (wraps), for the naming buttons. A word not on the list starts at the first.</summary>
        public static string NextWord(string word, int step)
        {
            int n = AvatarCatalogue.OutfitWords.Count;
            int i = word == null ? -1 : AvatarCatalogue.OutfitWords.ToList().IndexOf(word);
            return AvatarCatalogue.OutfitWords[((i + step) % n + n) % n];
        }
    }
}
