using System;
using System.Collections.Generic;
using WorldTour.Core.Progress;

namespace WorldTour.Core.Meta
{
    /// <summary>Why a purchase didn't go through (JS <c>reason</c>).</summary>
    public enum BuyReason
    {
        /// <summary>It went through.</summary>
        None = 0,
        /// <summary>No such item ("unknown").</summary>
        Unknown,
        /// <summary>Already hers, or free ("owned").</summary>
        Owned,
        /// <summary>Not enough coins ("short"); <see cref="BuyResult.Need"/> says how many more.</summary>
        Short,
    }

    /// <summary>JS <c>buy</c>'s result: <c>{ ok: true }</c> or <c>{ ok: false, reason, need? }</c>.</summary>
    public struct BuyResult
    {
        public bool Ok;
        public BuyReason Reason;
        /// <summary>For <see cref="BuyReason.Short"/>: price minus balance. 0 otherwise.</summary>
        public long Need;

        public override string ToString()
        {
            return Ok ? "ok" : Reason == BuyReason.Short ? "short:" + Need : Reason.ToString().ToLowerInvariant();
        }
    }

    /// <summary>JS <c>look(s)</c>: what the game should look like right now.</summary>
    public struct Look
    {
        /// <summary>Theme name without the slot, for example "tour-pink" or "peach-navy".</summary>
        public string Theme;
        /// <summary>The guitar body color, a CSS value as the catalog writes it ("#8fe3c4", "var(--paper-deep)").</summary>
        public string Guitar;
        /// <summary>Riff's outfit without the slot ("shades"), or "" for none.</summary>
        public string Outfit;
    }

    /// <summary>One sticker on the journal page (JS <c>pageItems</c> entries).</summary>
    public sealed class PageItem
    {
        /// <summary>Milestone id or shop item id; also the key of its spot in the layout.</summary>
        public string Key;
        public string Art;
        public string Name;
        /// <summary>"earned &lt;day&gt;" or "from backstage".</summary>
        public string Note;
    }

    /// <summary>
    /// Backstage and tour journal rules, ported from world_tour/js/meta.js. Pure functions over the save: the
    /// Game layer calls them and then saves. Nothing here takes away anything she has earned: buying spends only
    /// the balance (never lifetime fans, never below zero), owned items and earned stickers are only ever added.
    /// </summary>
    public static class MetaRules
    {
        /* ---------- backstage ---------- */

        /// <summary>meta.js:7 <c>owns</c>: in her owned list, or a free catalog item.</summary>
        public static bool Owns(SaveBlob s, string id)
        {
            if (s.Inventory.Owned.Contains(id)) return true;
            ShopItem it = Shop.ById(id);
            return it != null && it.Price == 0;
        }

        /// <summary>
        /// meta.js:8 <c>equippedId</c>: "slot:name" for what is equipped in a slot. A slot with nothing saved reads
        /// "slot:undefined" (or "slot:null"), as the JS template string does, which matches no item.
        /// </summary>
        public static string EquippedId(SaveBlob s, string slot)
        {
            string v;
            if (!s.Inventory.Equipped.TryGetValue(slot, out v)) return slot + ":undefined";
            return slot + ":" + (v ?? "null");
        }

        /// <summary>
        /// meta.js:11 <c>buy</c>, changed in phase 10B.1: the price is paid in coins (<see cref="CoinRules.PriceOf"/>); fans are never
        /// deducted, neither the balance nor the lifetime. Refuses an unknown item, one she owns (or a free one), and one she can't
        /// afford, changing nothing.
        /// </summary>
        public static BuyResult Buy(SaveBlob s, string id)
        {
            ShopItem it = Shop.ById(id);
            if (it == null) return new BuyResult { Reason = BuyReason.Unknown };
            if (Owns(s, id)) return new BuyResult { Reason = BuyReason.Owned };
            long price = CoinRules.PriceOf(it);
            if (!CoinRules.TrySpend(s, price)) return new BuyResult { Reason = BuyReason.Short, Need = price - CoinRules.Coins(s) };
            s.Inventory.Owned.Add(id);
            return new BuyResult { Ok = true };
        }

        /// <summary>meta.js:21 <c>equip</c>: wear an owned theme, guitar or outfit. Stickers are never equipped.</summary>
        public static bool Equip(SaveBlob s, string id)
        {
            ShopItem it = Shop.ById(id);
            if (it == null || it.Slot == Shop.StickerSlot || !Owns(s, id)) return false;
            s.Inventory.Equipped[it.Slot] = id.Substring(it.Slot.Length + 1);
            return true;
        }

        /// <summary>meta.js:29 <c>look</c>: anything unknown or not owned falls back to the slot's free item.</summary>
        public static Look GetLook(SaveBlob s)
        {
            ShopItem theme = Pick(s, Shop.Theme), guitar = Pick(s, Shop.Guitar), dog = Pick(s, Shop.Dog);
            return new Look
            {
                Theme = theme.Id.Substring(6),
                Guitar = guitar.Color,
                Outfit = dog.Id == "dog:none" ? "" : dog.Id.Substring(4),
            };
        }

        /// <summary>What is worn in a slot: the equipped item when it exists, is in that slot and is owned, else the slot's free item.</summary>
        public static ShopItem Pick(SaveBlob s, string slot)
        {
            ShopItem it = Shop.ById(EquippedId(s, slot));
            if (it != null && it.Slot == slot && Owns(s, it.Id)) return it;
            return Shop.FreeIn(slot);
        }

        /* ---------- tour journal ---------- */

        /// <summary>JS truthiness of <c>s.journal.stickers[id]</c>: a saved, non-empty day.</summary>
        public static bool HasSticker(SaveBlob s, string id)
        {
            string d;
            return s.Journal.Stickers.TryGetValue(id, out d) && !string.IsNullOrEmpty(d);
        }

        /// <summary>
        /// meta.js:37 <c>earn</c>: award every milestone whose rule now holds, dated <paramref name="today"/>, and
        /// return the newly earned ones in journal order. Earned ones are skipped, never re-tested or removed.
        /// A rule that throws counts as not earned, as the JS try/catch does.
        /// </summary>
        public static List<Milestone> Earn(SaveBlob s, IReadOnlyList<ModeInfo> modes, string today)
        {
            var fresh = new List<Milestone>();
            foreach (Milestone m in Milestones.All)
            {
                if (HasSticker(s, m.Id)) continue;
                bool ok;
                try { ok = m.Test(s, modes); }
                catch (Exception) { ok = false; }
                if (ok) { s.Journal.Stickers[m.Id] = today; fresh.Add(m); }
            }
            return fresh;
        }

        /// <summary>meta.js:49 <c>pageItems</c>: everything on the journal page, earned milestones then bought stickers.</summary>
        public static List<PageItem> PageItems(SaveBlob s)
        {
            var items = new List<PageItem>();
            foreach (Milestone m in Milestones.All)
                if (HasSticker(s, m.Id))
                    items.Add(new PageItem { Key = m.Id, Art = m.Art, Name = m.Name, Note = "earned " + s.Journal.Stickers[m.Id] });
            foreach (ShopItem i in Shop.All)
                if (i.Slot == Shop.StickerSlot && s.Inventory.Owned.Contains(i.Id))
                    items.Add(new PageItem { Key = i.Id, Art = i.Sticker, Name = i.Name, Note = "from backstage" });
            return items;
        }

        /// <summary>meta.js:56 <c>toEarn</c>: milestones not earned yet, in journal order.</summary>
        public static List<Milestone> ToEarn(SaveBlob s)
        {
            var left = new List<Milestone>();
            foreach (Milestone m in Milestones.All) if (!HasSticker(s, m.Id)) left.Add(m);
            return left;
        }

        /// <summary>meta.js:59 <c>defaultSpot</c> default column count.</summary>
        public const int DefaultCols = 5;

        /// <summary>
        /// meta.js:59 <c>defaultSpot</c>: where sticker <paramref name="i"/> sits before she moves it, a loose grid with
        /// a little jitter, as fractions of the page (x, y to 3 decimals) plus a whole-degree tilt.
        /// </summary>
        public static Spot DefaultSpot(int i, int cols = DefaultCols)
        {
            int r = i / cols, c = i % cols;
            double j = ((i * 37) % 11) / 11.0 - .5;
            return new Spot
            {
                X = JsNum.ToFixedNumber((c + .5) / cols + j * .06, 3),
                Y = JsNum.ToFixedNumber(Math.Min(.94, .09 + r * .165 + j * .03), 3),
                R = JsNum.Round(j * 14),
            };
        }

        /// <summary>meta.js:64 <c>spotOf</c>: her placed spot for a sticker, or its default.</summary>
        public static Spot SpotOf(SaveBlob s, string key, int i, int cols = DefaultCols)
        {
            Spot p;
            if (s.Journal.Layout.TryGetValue(key, out p) && p != null) return p;
            return DefaultSpot(i, cols);
        }

        /// <summary>
        /// meta.js:65 <c>place</c>: put a sticker at (x, y), clamped to 0.05..0.95 and rounded to 3 decimals. A sticker
        /// that was placed before keeps its tilt; <paramref name="r"/> is used only the first time.
        /// </summary>
        public static void Place(SaveBlob s, string key, double x, double y, double r = 0)
        {
            Spot prev;
            s.Journal.Layout.TryGetValue(key, out prev);
            s.Journal.Layout[key] = new Spot
            {
                X = JsNum.ToFixedNumber(Clamp(x), 3),
                Y = JsNum.ToFixedNumber(Clamp(y), 3),
                R = prev != null ? prev.R : r,
            };
        }

        /// <summary>meta.js:66 <c>Math.min(.95, Math.max(.05, v))</c>; NaN stays NaN, as in JS.</summary>
        private static double Clamp(double v)
        {
            if (double.IsNaN(v)) return v;
            return Math.Min(.95, Math.Max(.05, v));
        }
    }
}
