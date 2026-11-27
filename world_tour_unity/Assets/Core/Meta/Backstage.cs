using System.Globalization;
using WorldTour.Core.Progress;

namespace WorldTour.Core.Meta
{
    /// <summary>What the mirror's button does when pressed.</summary>
    public enum MirrorAction
    {
        /// <summary>Disabled: already wearing it, or not enough fans.</summary>
        None = 0,
        /// <summary>"wear it" / "switch to this theme".</summary>
        Equip,
        /// <summary>"buy for 12,000", then "tap again to buy".</summary>
        Buy,
        /// <summary>"see it in your journal" (an owned sticker).</summary>
        Journal,
    }

    /// <summary>One item button's state (backstage.js <c>tags()</c>).</summary>
    public struct ItemTag
    {
        /// <summary>"on", "wearing", "in journal", "yours", or the price ("12,000").</summary>
        public string Tag;
        public bool Own;
        public bool Wearing;
        /// <summary>Not hers and dearer than her balance (the thumb fades to 60%).</summary>
        public bool Short;
        /// <summary>The selected item (aria-pressed).</summary>
        public bool Pressed;
    }

    /// <summary>The mirror: what the selected item looks like on riff and what its button offers (backstage.js <c>paint()</c>).</summary>
    public struct Mirror
    {
        /// <summary>"yours" or "trying on".</summary>
        public string Eyebrow;
        public string Name;
        public string Blurb;
        /// <summary>The button's text.</summary>
        public string Action;
        public MirrorAction Does;
        /// <summary>riff's outfit on the mirror: the selected outfit, else what she wears ("" = none).</summary>
        public string Outfit;
        /// <summary>The guitar beside riff: the selected skin's id ("guitar:mint"), else the equipped one. Null for a sticker.</summary>
        public string Guitar;
        /// <summary>The sticker art shown instead of the guitar for a journal sticker; null otherwise.</summary>
        public string Sticker;
        /// <summary>The theme the page wears while this is selected: a selected theme is tried on, anything else shows her equipped theme.</summary>
        public string Theme;
    }

    /// <summary>What a press on the mirror's button did.</summary>
    public enum PressOutcome
    {
        /// <summary>Nothing (disabled button, or the journal link, which the screen follows).</summary>
        None = 0,
        /// <summary>First tap on "buy": armed, nothing spent ("tap again to buy").</summary>
        Armed,
        /// <summary>Bought (and, unless it is a sticker, put on).</summary>
        Bought,
        /// <summary>The purchase was refused (wobble, thud); nothing changed.</summary>
        Refused,
        /// <summary>Put on an item she owns.</summary>
        Equipped,
        /// <summary>"see it in your journal": the screen opens the journal.</summary>
        OpenJournal,
    }

    /// <summary>
    /// The backstage shop and locker's screen logic, ported from world_tour/screens/backstage.js with no UI: which item
    /// is selected, each item's tag, the mirror, and the two-tap buy (a stray tap never spends fans; the first tap arms
    /// the item, a second tap on it buys, and picking another item or <see cref="Disarm"/> after 3.5 s cancels).
    /// Buying spends only the balance through <see cref="MetaRules.Buy"/>: lifetime fans never go down and nothing owned
    /// is ever taken away. The caller saves after <see cref="PressOutcome.Bought"/> and <see cref="PressOutcome.Equipped"/>.
    /// </summary>
    public sealed class Backstage
    {
        /// <summary>backstage.js:117 the armed buy cancels after 3.5 s.</summary>
        public const float ArmSeconds = 3.5f;

        private readonly SaveBlob _s;

        /// <summary>The selected item's id. Starts on riff's equipped outfit (backstage.js:25 <c>sel = equippedId(s, 'dog')</c>).</summary>
        public string Selected { get; private set; }

        /// <summary>The item whose buy button has been tapped once, or null.</summary>
        public string Armed { get; private set; }

        public Backstage(SaveBlob s) : this(s, Shop.Dog) { }

        /// <summary>Starts on what is worn in <paramref name="slot"/> (the 3D world's panel starts on her outfit); an unknown or empty slot falls back to riff's.</summary>
        public Backstage(SaveBlob s, string slot)
        {
            _s = s;
            if (slot == null || Shop.FreeIn(slot) == null) slot = Shop.Dog;
            ShopItem start = MetaRules.Pick(s, slot);
            Selected = start != null ? start.Id : "dog:none";
        }

        public ShopItem Item { get { return Shop.ById(Selected); } }

        /// <summary>backstage.js:126 clicking an item: picking a different one cancels an armed buy.</summary>
        public void Select(string id)
        {
            if (Shop.ById(id) == null) return;
            if (Selected != id) Armed = null;
            Selected = id;
        }

        /// <summary>backstage.js:117 the armed buy timing out.</summary>
        public void Disarm() { Armed = null; }

        /// <summary>backstage.js:16 fmt(): whole fans with en-US thousands commas.</summary>
        public static string Fmt(long n) { return n.ToString("#,##0", CultureInfo.InvariantCulture); }

        private bool Wearing(ShopItem it)
        {
            return it.Slot != Shop.StickerSlot && MetaRules.EquippedId(_s, it.Slot) == it.Id;
        }

        /// <summary>backstage.js:60-68 <c>tags()</c> for one item.</summary>
        public ItemTag Tag(ShopItem it)
        {
            bool own = MetaRules.Owns(_s, it.Id), wearing = Wearing(it);
            string tag = wearing ? (it.Slot == Shop.Theme ? "on" : "wearing")
                : own ? (it.Slot == Shop.StickerSlot ? "in journal" : "yours")
                : Fmt(CoinRules.PriceOf(it));
            return new ItemTag { Tag = tag, Own = own, Wearing = wearing, Short = !own && CoinRules.PriceOf(it) > CoinRules.Coins(_s), Pressed = it.Id == Selected };
        }

        /// <summary>backstage.js:73-97 <c>paint()</c>: the mirror for the selected item.</summary>
        public Mirror Mirror()
        {
            ShopItem it = Item;
            bool own = MetaRules.Owns(_s, it.Id), wearing = Wearing(it);
            Look look = MetaRules.GetLook(_s);
            var m = new Mirror
            {
                Eyebrow = own ? "yours" : "trying on",
                Name = it.Name,
                Blurb = it.Blurb,
                Outfit = it.Slot == Shop.Dog ? (it.Id == "dog:none" ? "" : it.Id.Substring(4)) : look.Outfit,
                Guitar = it.Slot == Shop.StickerSlot ? null : it.Slot == Shop.Guitar ? it.Id : EquippedGuitarId(),
                Sticker = it.Slot == Shop.StickerSlot ? it.Sticker : null,
                Theme = it.Slot == Shop.Theme ? it.Id.Substring(6) : look.Theme,
            };
            if (wearing) { m.Action = it.Slot == Shop.Theme ? "theme is on" : "wearing it"; m.Does = MirrorAction.None; }
            else if (own && it.Slot == Shop.StickerSlot) { m.Action = "see it in your journal"; m.Does = MirrorAction.Journal; }
            else if (own) { m.Action = it.Slot == Shop.Theme ? "switch to this theme" : "wear it"; m.Does = MirrorAction.Equip; }
            else if (CoinRules.PriceOf(it) > CoinRules.Coins(_s)) { m.Action = Fmt(CoinRules.PriceOf(it) - CoinRules.Coins(_s)) + " more coins needed"; m.Does = MirrorAction.None; }
            else { m.Action = Armed == it.Id ? "tap again to buy" : "buy for " + Fmt(CoinRules.PriceOf(it)) + " coins"; m.Does = MirrorAction.Buy; }
            return m;
        }

        /// <summary>The equipped guitar skin's id, falling back to the free one as look() does.</summary>
        public string EquippedGuitarId()
        {
            string id = MetaRules.EquippedId(_s, Shop.Guitar);
            ShopItem g = Shop.ById(id);
            return g != null && MetaRules.Owns(_s, id) ? id : "guitar:classic";
        }

        /// <summary>
        /// The mirror's button (backstage.js:99-121 <c>doEquip</c>, <c>doBuy</c>). Buy needs two presses on the same item;
        /// a bought sticker goes to the journal, anything else is put on at once.
        /// </summary>
        public PressOutcome Press()
        {
            ShopItem it = Item;
            Mirror m = Mirror();
            switch (m.Does)
            {
                case MirrorAction.Equip:
                    return ProgressRules.EquipItem(_s, it.Id) ? PressOutcome.Equipped : PressOutcome.None;
                case MirrorAction.Journal:
                    return PressOutcome.OpenJournal;
                case MirrorAction.Buy:
                    if (Armed != it.Id) { Armed = it.Id; return PressOutcome.Armed; }
                    Armed = null;
                    BuyResult r = ProgressRules.BuyItem(_s, it.Id);
                    if (!r.Ok) return PressOutcome.Refused;
                    if (it.Slot != Shop.StickerSlot) ProgressRules.EquipItem(_s, it.Id);
                    return PressOutcome.Bought;
                default:
                    return PressOutcome.None;
            }
        }

        /// <summary>backstage.js:112 the toast after a purchase.</summary>
        public static string BoughtToast(ShopItem it) { return it.Slot == Shop.StickerSlot ? "stuck it in!" : "yours!"; }

        // ---------- the copy around the shop (backstage.js:28-35) ----------

        public const string BackLink = "the map";
        public const string Title = "backstage";
        public const string FansToSpend = "coins to spend";

        /// <summary>backstage.js:31 the eyebrow: "merch + wardrobe ✶ cosmetic only".</summary>
        public static readonly string Eyebrow = "merch + wardrobe " + JournalBook.Star + " cosmetic only";

        /// <summary>backstage.js:35: "spending never lowers your total fans (12,345), so the map only goes forward."</summary>
        public static string Note(long lifetime)
        {
            return "coins buy things. your fans (" + Fmt(lifetime) + ") are never spent.";
        }
    }
}
