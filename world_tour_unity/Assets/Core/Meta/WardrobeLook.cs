namespace WorldTour.Core.Meta
{
    /// <summary>
    /// Phase 8.3: everything the 3D world draws from the backstage items, as full item ids ("outfit:peach-navy"): what she wears, her hair, the guitar she plays,
    /// riff's outfit and the stage light colour. <see cref="Of"/> reads the save (anything unknown or not owned falls back to the slot's free item, as
    /// <see cref="MetaRules.GetLook"/> does); with a try-on id the item replaces its own slot only, so leaving the shop without buying shows the save's look again.
    /// </summary>
    public struct WardrobeLook
    {
        public string Outfit, Hair, Guitar, Dog, Lights;

        public static WardrobeLook Of(Progress.SaveBlob s, string tryOn = null)
        {
            var l = new WardrobeLook
            {
                Outfit = MetaRules.Pick(s, Shop.Outfit).Id,
                Hair = MetaRules.Pick(s, Shop.Hair).Id,
                Guitar = MetaRules.Pick(s, Shop.Guitar).Id,
                Dog = MetaRules.Pick(s, Shop.Dog).Id,
                Lights = MetaRules.Pick(s, Shop.Lights).Id,
            };
            ShopItem t = tryOn == null ? null : Shop.ById(tryOn);
            if (t != null) l.Set(t.Id);
            return l;
        }

        /// <summary>Puts an item on in its own slot (themes and stickers are not part of the 3D look and are ignored).</summary>
        public void Set(string id)
        {
            ShopItem t = Shop.ById(id);
            if (t == null) return;
            switch (t.Slot)
            {
                case Shop.Outfit: Outfit = id; break;
                case Shop.Hair: Hair = id; break;
                case Shop.Guitar: Guitar = id; break;
                case Shop.Dog: Dog = id; break;
                case Shop.Lights: Lights = id; break;
            }
        }

        /// <summary>Short name without the slot ("peach-navy").</summary>
        public static string Name(string id) { int i = id == null ? -1 : id.IndexOf(':'); return i < 0 ? id : id.Substring(i + 1); }

        public override string ToString() { return "outfit=" + Name(Outfit) + " hair=" + Name(Hair) + " guitar=" + Name(Guitar) + " dog=" + Name(Dog) + " lights=" + Name(Lights); }
    }
}
