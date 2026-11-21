using System;
using System.Collections.Generic;
using System.Globalization;
using WorldTour.Core.Data;
using WorldTour.Core.Meta;
using WorldTour.Core.Shows;

namespace WorldTour.Game.Ticket
{
    /// <summary>
    /// Everything the ticket screen needs to draw one finished show (task 2.6). Build it from the show host's result
    /// (2.5 ShowScreen.Finished) and pass it to <see cref="TicketScreen.Open"/>.
    /// </summary>
    public sealed class TicketData
    {
        /// <summary>The engine's summary (required). Mode, Venue, N, First, HintOnly, Best, HintTotal, Fans, Pct, Encore, IsMath, Setlist, MissedItems, CanEncore and Sold are read.</summary>
        public ShowSummary Summary;
        /// <summary>The show's venue (ticket.js <c>venue</c>: its name, and its crowd size for the swell). Null = Venues.ById(Summary.Venue).</summary>
        public Venue Venue;
        /// <summary>The week's label for the eyebrow (ticket.js:27 <c>G.week.label</c>); not shown for a math set.</summary>
        public string WeekLabel;
        /// <summary>The mode's name for the eyebrow (ticket.js:27 <c>mode.name</c>, e.g. "remix"). Null = Summary.Mode.</summary>
        public string ModeName;
        /// <summary>Journal stickers this show earned (show.js:222 awardStickers, via ProgressRules.Attach onStickers). Null = none.</summary>
        public IReadOnlyList<Milestone> NewStickers;
        /// <summary>Venues unlocked during the show, in order (show.js G.unlocks, via ProgressRules.Attach onUnlock). Null = none. The ticket shows the last one (ticket.js:23).</summary>
        public IReadOnlyList<Venue> Unlocks;
        /// <summary>The local date "yyyy-mm-dd" (ticket.js:22 localISO()). Null = LocalClock.TodayIso() when the screen opens.</summary>
        public string Date;
    }

    /// <summary>Which button sits beside "back to the map" (ticket.js:75).</summary>
    public enum TicketPrimary { Encore, PlayAgain }

    /// <summary>One setlist line (ticket.js:69).</summary>
    public sealed class TicketSong
    {
        /// <summary>"01" (CSS counter decimal-leading-zero).</summary>
        public string Number;
        /// <summary>The word, or a math problem's label.</summary>
        public string Text;
        public SetlistTag Tag;
        /// <summary>The li class: "ok", "hint" or "miss".</summary>
        public string TagClass;
        /// <summary>"nailed it", "used a hint" or "rehearse again".</summary>
        public string TagText;
        /// <summary>A math problem's label (generator text with [[n/d]] fractions, drawn stacked), not a word from weeks.json.</summary>
        public bool IsMath;
    }

    /// <summary>
    /// The copy of world_tour/screens/ticket.js and copy.js finishHead / finishSub, word for word. Plain C#, no
    /// UnityEngine, checked against the real JS by Tools/ticket_vectors.mjs. Strings keep the web's case; the screen
    /// lowercases them (SetLower) as base.css does. Non-ASCII characters are built from their codes so this file stays ASCII.
    /// </summary>
    public static class TicketText
    {
        /// <summary>U+2736 six-pointed black star (the eyebrow separator). Neither font has it: the screen draws a glyph.</summary>
        public static readonly string Star = ((char)0x2736).ToString();
        /// <summary>U+2665 black heart ("admit one"). Neither font has it: the screen draws a glyph.</summary>
        public static readonly string Heart = ((char)0x2665).ToString();
        /// <summary>U+2019 right single quote, as the JS copy writes it.</summary>
        static readonly string Q = ((char)0x2019).ToString();

        /// <summary>copy.js NAME.</summary>
        public const string Name = "natalia";

        /// <summary>ticket.js:18 <c>vip = pct &gt;= .9</c> (also copy.js finishHead/finishSub's top line).</summary>
        public const double VipAt = .9;
        /// <summary>copy.js finishHead/finishSub's middle line.</summary>
        public const double GoodAt = .7;

        public static bool IsVip(double pct) { return pct >= VipAt; }

        /// <summary>ticket.js:27 the eyebrow: <c>${G.math ? 'math set' : G.week.label} &#x2736; ${mode.name}${G.encore ? ' encore' : ''}</c>.</summary>
        public static string Eyebrow(bool isMath, string weekLabel, string modeName, bool encore)
        {
            return EyebrowLeft(isMath, weekLabel) + " " + Star + " " + EyebrowRight(modeName, encore);
        }

        /// <summary>The part of the eyebrow before the star.</summary>
        public static string EyebrowLeft(bool isMath, string weekLabel) { return isMath ? "math set" : weekLabel ?? ""; }

        /// <summary>The part of the eyebrow after the star.</summary>
        public static string EyebrowRight(string modeName, bool encore) { return (modeName ?? "") + (encore ? " encore" : ""); }

        /// <summary>copy.js:35 finishHead (ticket.js:28, the shout).</summary>
        public static string Head(double pct)
        {
            return pct >= VipAt ? "sold out" : pct >= GoodAt ? "what a show" : "rehearsal night";
        }

        /// <summary>copy.js:36 finishSub (ticket.js:29, the lede).</summary>
        public static string Sub(double pct)
        {
            return pct >= VipAt ? Name + ", you" + Q + "re so ready for that test."
                : pct >= GoodAt ? "a couple more run-throughs and it" + Q + "s perfect."
                : "every star practices. try it again.";
        }

        /// <summary>ticket.js:34, without its heart (drawn as a glyph after it).</summary>
        public const string AdmitOne = "admit one";

        /// <summary>ticket.js:34 as the web prints it.</summary>
        public static string Top { get { return AdmitOne + " " + Heart; } }

        /// <summary>ticket.js:35.</summary>
        public static string LiveAt(string venueName) { return "live at " + (venueName ?? ""); }

        /// <summary>ticket.js:36 the word after the number.</summary>
        public const string FansWord = "fans";

        /// <summary>util.js:16 <c>fmt</c>: Math.round(n).toLocaleString('en-US'), with no culture data needed.</summary>
        public static string Fmt(long n)
        {
            string digits = Math.Abs(n).ToString(CultureInfo.InvariantCulture);
            var sb = new System.Text.StringBuilder(digits.Length + digits.Length / 3 + 1);
            if (n < 0) sb.Append('-');
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0) sb.Append(',');
                sb.Append(digits[i]);
            }
            return sb.ToString();
        }

        /// <summary>ticket.js:38-40, the three rows: (label, value).</summary>
        public static KeyValuePair<string, string>[] Rows(int firstTry, int n, int best, int hintTotal)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            return new[]
            {
                new KeyValuePair<string, string>("first try", firstTry.ToString(c) + "/" + n.ToString(c)),
                new KeyValuePair<string, string>("best streak", best.ToString(c)),
                new KeyValuePair<string, string>("hints", hintTotal.ToString(c)),
            };
        }

        /// <summary>ticket.js:44.</summary>
        public static string ClassOf(bool vip) { return vip ? "vip" : "ga"; }

        /// <summary>ticket.js:49 the stamp, shown only when vip: <c>sold&lt;br&gt;out</c>.</summary>
        public const string Stamp = "sold\nout";

        /// <summary>ticket.js:55.</summary>
        public const string StickersTitle = "new for your tour journal";

        /// <summary>ticket.js:54-55: at most this many stickers are drawn and named.</summary>
        public const int StickersShown = 3;

        /// <summary>ticket.js:55: the first three names joined by ", ", then " + N more".</summary>
        public static string StickerNames(IReadOnlyList<string> names)
        {
            if (names == null || names.Count == 0) return "";
            int k = Math.Min(StickersShown, names.Count);
            var first = new string[k];
            for (int i = 0; i < k; i++) first[i] = names[i] ?? "";
            string s = string.Join(", ", first);
            if (names.Count > StickersShown) s += " + " + (names.Count - StickersShown).ToString(CultureInfo.InvariantCulture) + " more";
            return s;
        }

        /// <summary>ticket.js:61.</summary>
        public const string UnlockTitle = "new venue unlocked";

        /// <summary>ticket.js:67.</summary>
        public static string SetlistTitle { get { return "tonight" + Q + "s setlist"; } }

        /// <summary>ticket.js:21: missed beats hinted beats ok (the engine's Finish does the same).</summary>
        public static SetlistTag TagOf(bool missed, bool hinted)
        {
            return missed ? SetlistTag.Miss : hinted ? SetlistTag.Hint : SetlistTag.Ok;
        }

        /// <summary>ticket.js:21 the li class.</summary>
        public static string TagClass(SetlistTag t)
        {
            switch (t)
            {
                case SetlistTag.Miss: return "miss";
                case SetlistTag.Hint: return "hint";
                default: return "ok";
            }
        }

        /// <summary>ticket.js:21 the tag's words.</summary>
        public static string TagText(SetlistTag t)
        {
            switch (t)
            {
                case SetlistTag.Miss: return "rehearse again";
                case SetlistTag.Hint: return "used a hint";
                default: return "nailed it";
            }
        }

        /// <summary>screens.css:181 <c>counter(s, decimal-leading-zero)</c>: at least two digits.</summary>
        public static string SongNumber(int oneBased)
        {
            string d = Math.Abs(oneBased).ToString(CultureInfo.InvariantCulture);
            if (d.Length < 2) d = "0" + d;
            return (oneBased < 0 ? "-" : "") + d;
        }

        /// <summary>ticket.js:69 <c>x.v.w ? x.v.w : rich(labelOf(x.v))</c>. Math labels keep their [[n/d]] markup; the screen stacks them (TicketSong.IsMath).</summary>
        public static string SongText(ShowItem item)
        {
            var w = item as WordItem;
            if (w != null && !string.IsNullOrEmpty(w.W)) return w.W;
            return item != null ? item.Label ?? "" : "";
        }

        /// <summary>ticket.js:75 the encore button.</summary>
        public static string EncoreButton(int missed) { return "encore: " + missed.ToString(CultureInfo.InvariantCulture) + " to rehearse"; }

        /// <summary>ticket.js:75.</summary>
        public const string PlayAgain = "play again";

        /// <summary>ticket.js:76.</summary>
        public const string BackToMap = "back to the map";

        /// <summary>ticket.js:81 riff's bubble.</summary>
        public static string Coach(bool canEncore, int missed, bool vip)
        {
            if (canEncore)
                return "the encore replays the " + (missed == 1 ? "one you missed" : missed.ToString(CultureInfo.InvariantCulture) + " you missed") + ". half fans, zero pressure.";
            return vip ? "that" + Q + "s a vip night. framing this ticket." : "good show. the bus leaves when you" + Q + "re ready.";
        }

        /// <summary>JS Math.round(pct * 100), for the log line (the ticket itself never prints a percent).</summary>
        public static int PctRound(double pct)
        {
            return (int)Math.Floor(pct * 100 + .5);
        }
    }

    /// <summary>
    /// The finished ticket as data: every string and choice the screen draws, built from a <see cref="TicketData"/>.
    /// Plain C#, so edit-mode tests compare it with what the real renderTicket() printed.
    /// </summary>
    public sealed class TicketModel
    {
        public double Pct;
        public bool Vip;
        /// <summary>Summary.Sold (show.js:221). Logged only: the ticket's stamp follows <see cref="Vip"/>, as ticket.js:49 does.</summary>
        public bool Sold;
        public bool IsEncore;
        public string EyebrowLeft, EyebrowRight, Head, Sub, LiveAt, FansNumber, Class, Date, BarcodeSeed;
        public KeyValuePair<string, string>[] Rows;
        public List<BarcodeBar> Bars;
        /// <summary>ticket.js:49: the "sold out" stamp is on the ticket (pct >= .9).</summary>
        public bool ShowStamp;
        /// <summary>Every new sticker (the log counts them all); the first three are drawn.</summary>
        public List<Milestone> Stickers = new List<Milestone>();
        public string StickerNames;
        /// <summary>The last venue unlocked during the show, or null (ticket.js:23).</summary>
        public Venue Unlock;
        public int UnlockCount;
        public List<TicketSong> Setlist = new List<TicketSong>();
        public TicketPrimary Primary;
        public string PrimaryLabel;
        public int MissedCount;
        public string Coach;
        public Venue Venue;

        public string Eyebrow { get { return EyebrowLeft + " " + TicketText.Star + " " + EyebrowRight; } }

        /// <summary>The browser tests' line: WT_TICKET_SHOWN pct=&lt;n&gt; vip=&lt;b&gt; sold=&lt;b&gt; stickers=&lt;n&gt; unlocks=&lt;n&gt; encore=&lt;b&gt;.</summary>
        public string LogLine
        {
            get
            {
                CultureInfo c = CultureInfo.InvariantCulture;
                return "WT_TICKET_SHOWN pct=" + TicketText.PctRound(Pct).ToString(c)
                    + " vip=" + B(Vip) + " sold=" + B(Sold)
                    + " stickers=" + Stickers.Count.ToString(c) + " unlocks=" + UnlockCount.ToString(c)
                    + " encore=" + B(Primary == TicketPrimary.Encore);
            }
        }

        static string B(bool b) { return b ? "true" : "false"; }

        /// <summary>Builds the model. <paramref name="date"/> overrides data.Date (the screen passes the local clock's day when data.Date is null).</summary>
        public static TicketModel Build(TicketData data, string date = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            ShowSummary s = data.Summary ?? throw new ArgumentException("the ticket needs the show's summary", nameof(data));
            var m = new TicketModel();
            m.Pct = s.Pct;
            m.Vip = TicketText.IsVip(s.Pct);
            m.Sold = s.Sold;
            m.IsEncore = s.Encore;
            m.Venue = data.Venue ?? Venues.ById(s.Venue);
            m.EyebrowLeft = TicketText.EyebrowLeft(s.IsMath, data.WeekLabel);
            m.EyebrowRight = TicketText.EyebrowRight(data.ModeName ?? s.Mode, s.Encore);
            m.Head = TicketText.Head(s.Pct);
            m.Sub = TicketText.Sub(s.Pct);
            m.LiveAt = TicketText.LiveAt(m.Venue != null ? m.Venue.Name : s.Venue);
            m.FansNumber = TicketText.Fmt(s.Fans);
            m.Rows = TicketText.Rows(s.FirstMain >= 0 ? s.FirstMain : s.First, s.N, s.Best, s.HintTotal);
            m.Class = TicketText.ClassOf(m.Vip);
            m.Date = date ?? data.Date ?? "";
            m.BarcodeSeed = TicketBarcode.Seed(m.Date, s.Mode, s.Fans);
            m.Bars = TicketBarcode.Bars(m.BarcodeSeed);
            m.ShowStamp = m.Vip;

            if (data.NewStickers != null) foreach (Milestone x in data.NewStickers) if (x != null) m.Stickers.Add(x);
            var names = new List<string>();
            foreach (Milestone x in m.Stickers) names.Add(x.Name);
            m.StickerNames = TicketText.StickerNames(names);

            m.UnlockCount = data.Unlocks != null ? data.Unlocks.Count : 0;
            m.Unlock = m.UnlockCount > 0 ? data.Unlocks[m.UnlockCount - 1] : null;

            int i = 0;
            if (s.Setlist != null)
                foreach (SetlistEntry e in s.Setlist)
                {
                    i++;
                    m.Setlist.Add(new TicketSong
                    {
                        Number = TicketText.SongNumber(i), Text = TicketText.SongText(e.Item), Tag = e.Tag,
                        TagClass = TicketText.TagClass(e.Tag), TagText = TicketText.TagText(e.Tag), IsMath = e.Item is MathItem
                    });
                }

            m.MissedCount = s.MissedItems != null ? s.MissedItems.Count : 0;
            m.Primary = s.CanEncore ? TicketPrimary.Encore : TicketPrimary.PlayAgain;
            m.PrimaryLabel = s.CanEncore ? TicketText.EncoreButton(m.MissedCount) : TicketText.PlayAgain;
            m.Coach = TicketText.Coach(s.CanEncore, m.MissedCount, m.Vip);
            return m;
        }
    }
}
