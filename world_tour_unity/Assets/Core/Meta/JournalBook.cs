using System;
using System.Collections.Generic;
using WorldTour.Core.Progress;

namespace WorldTour.Core.Meta
{
    /// <summary>One place in the tour journal: an earned or bought sticker, or the empty slot of one still to earn.</summary>
    public sealed class JournalSlot
    {
        /// <summary>Milestone id, or the shop id ("sticker:pick") of a bought sticker. Also its key in the layout.</summary>
        public string Key;
        /// <summary>A sticker art id, or "venue:&lt;id&gt;" (milestones.js <c>art</c>).</summary>
        public string Art;
        public string Name;
        /// <summary>
        /// Earned: "earned &lt;day&gt;" or "from backstage" (meta.js pageItems <c>note</c>). Empty slot: how to earn it
        /// (milestones.js <c>how</c>, the web journal's "still to earn" hint).
        /// </summary>
        public string Note;
        /// <summary>True when it is in her book (earned milestone or bought sticker); false for an empty slot.</summary>
        public bool Earned;
        /// <summary>Earned but not yet seen peeling onto the page (not in <c>journal.seen</c>).</summary>
        public bool Fresh;
        /// <summary>Page number, from 0.</summary>
        public int Page;
        /// <summary>Its place on the page, from 0 (reading order of the slot grid).</summary>
        public int Index;
        /// <summary>The slot's own spot on the page (fractions of the page, plus a tilt in degrees).</summary>
        public Spot Home;
        /// <summary>Where the sticker sits: where she placed it, or its slot. Same as <see cref="Home"/> for an empty slot.</summary>
        public Spot Spot;
    }

    /// <summary>One page of the journal.</summary>
    public sealed class JournalPage
    {
        /// <summary>From 0.</summary>
        public int Number;
        /// <summary>True for the page of bought stickers (shown only when she has bought one).</summary>
        public bool Backstage;
        public readonly List<JournalSlot> Slots = new List<JournalSlot>();
    }

    /// <summary>
    /// The tour journal as a paged sticker book (Unity; the web journal is one long page plus a "still to earn" list).
    /// Every milestone has its own slot, in milestones.js order, <see cref="PerPage"/> to a page: an earned sticker sits
    /// in its slot until she moves it (then where she put it, saved by <see cref="MetaRules.Place"/> as fractions of its
    /// page), and one still to earn shows its empty slot with the web's hint (<c>how</c>). Bought stickers get a last page,
    /// in shop order, once she owns one. Nothing here removes a sticker: pages are read from the save, and
    /// <see cref="MarkSeen"/> only ever adds to <c>journal.seen</c>.
    /// </summary>
    public static class JournalBook
    {
        /// <summary>Slots per page: five across, two down.</summary>
        public const int PerPage = 10;
        public const int Cols = 5;
        public const string BoughtNote = "from backstage";

        /// <summary>The milestone pages (20 milestones, 10 to a page).</summary>
        public static int MilestonePages { get { return (Milestones.All.Count + PerPage - 1) / PerPage; } }

        /// <summary>
        /// A slot's home on its page: the web's defaultSpot jitter (meta.js:59) on a five-by-two grid, so the rows sit at
        /// about a quarter and three quarters of the page height. x and y are fractions of the page to 3 decimals, r whole degrees.
        /// </summary>
        public static Spot Home(int index)
        {
            int i = ((index % PerPage) + PerPage) % PerPage;
            int r = i / Cols, c = i % Cols;
            double j = ((i * 37) % 11) / 11.0 - .5;
            return new Spot
            {
                X = JsNum.ToFixedNumber((c + .5) / Cols + j * .06, 3),
                Y = JsNum.ToFixedNumber((r == 0 ? .28 : .72) + j * .03, 3),
                R = JsNum.Round(j * 14),
            };
        }

        /// <summary>Every page, in order: the milestone pages, then the bought-sticker page if she owns one.</summary>
        public static List<JournalPage> Pages(SaveBlob s)
        {
            var seen = new HashSet<string>(s.Journal.Seen);
            var pages = new List<JournalPage>();
            for (int i = 0; i < Milestones.All.Count; i++)
            {
                Milestone m = Milestones.All[i];
                JournalPage page = PageAt(pages, i / PerPage, false);
                bool earned = MetaRules.HasSticker(s, m.Id);
                var slot = new JournalSlot
                {
                    Key = m.Id, Art = m.Art, Name = m.Name,
                    Note = earned ? "earned " + s.Journal.Stickers[m.Id] : m.How,
                    Earned = earned, Fresh = earned && !seen.Contains(m.Id),
                    Page = page.Number, Index = i % PerPage, Home = Home(i),
                };
                slot.Spot = earned ? Placed(s, m.Id, slot.Home) : slot.Home;
                page.Slots.Add(slot);
            }
            int n = 0;
            foreach (ShopItem it in Shop.All)
            {
                if (it.Slot != Shop.StickerSlot || !s.Inventory.Owned.Contains(it.Id)) continue;
                JournalPage page = PageAt(pages, MilestonePages + n / PerPage, true);
                var slot = new JournalSlot
                {
                    Key = it.Id, Art = it.Sticker, Name = it.Name, Note = BoughtNote,
                    Earned = true, Fresh = !seen.Contains(it.Id),
                    Page = page.Number, Index = n % PerPage, Home = Home(n),
                };
                slot.Spot = Placed(s, it.Id, slot.Home);
                page.Slots.Add(slot);
                n++;
            }
            return pages;
        }

        private static JournalPage PageAt(List<JournalPage> pages, int number, bool backstage)
        {
            while (pages.Count <= number) pages.Add(new JournalPage { Number = pages.Count, Backstage = backstage });
            return pages[number];
        }

        private static Spot Placed(SaveBlob s, string key, Spot home)
        {
            Spot p;
            return s.Journal.Layout.TryGetValue(key, out p) && p != null ? p : home;
        }

        /// <summary>The page the book opens on: the first page with a sticker she hasn't seen yet, else the first page.</summary>
        public static int StartPage(List<JournalPage> pages)
        {
            foreach (JournalPage p in pages)
                foreach (JournalSlot sl in p.Slots)
                    if (sl.Fresh) return p.Number;
            return 0;
        }

        /// <summary>
        /// Adds these keys to <c>journal.seen</c>, in order, skipping any already there and any that isn't in her book
        /// (an unearned milestone or an unbought sticker). Returns the keys added. Never removes one.
        /// </summary>
        public static List<string> MarkSeen(SaveBlob s, IEnumerable<string> keys)
        {
            var added = new List<string>();
            if (keys == null) return added;
            foreach (string k in keys)
            {
                if (k == null || s.Journal.Seen.Contains(k) || !InBook(s, k)) continue;
                s.Journal.Seen.Add(k);
                added.Add(k);
            }
            return added;
        }

        /// <summary>True for an earned milestone or a bought journal sticker.</summary>
        public static bool InBook(SaveBlob s, string key)
        {
            if (key == null) return false;
            if (Milestones.ById(key) != null) return MetaRules.HasSticker(s, key);
            ShopItem it = Shop.ById(key);
            return it != null && it.Slot == Shop.StickerSlot && s.Inventory.Owned.Contains(key);
        }

        /// <summary>journal.js:17 <c>earnedN</c>: the earned milestones (bought stickers don't count).</summary>
        public static int EarnedCount(SaveBlob s)
        {
            int n = 0;
            foreach (Milestone m in Milestones.All) if (MetaRules.HasSticker(s, m.Id)) n++;
            return n;
        }

        /// <summary>journal.js:24 the eyebrow: "sticker book ✶ 3 of 20 earned".</summary>
        public static string Eyebrow(SaveBlob s)
        {
            int earned = EarnedCount(s);
            return "sticker book " + Star + " " + earned + " of " + Milestones.All.Count + " earned";
        }

        /// <summary>U+2736, the six-pointed star the web copy uses. Built from its code so the source stays ASCII.</summary>
        public static readonly string Star = ((char)0x2736).ToString();
    }
}
