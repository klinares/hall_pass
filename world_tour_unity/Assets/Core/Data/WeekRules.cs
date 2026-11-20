using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WorldTour.Core.Data
{
    /// <summary>
    /// Which week opens and which word-based modes a week offers, ported from the web game.
    /// "Today" is always a parameter (YYYY-MM-DD, local time) so the rules test without a clock.
    /// </summary>
    public static class WeekRules
    {
        /// <summary>Lyric writer and prefix mixer each need at least this many usable words (lyric.js:28, prefix.js:27).</summary>
        public const int MinWordsForMode = 4;

        /// <summary>
        /// Newest first by id (screens/venue.js:10, <c>b.id.localeCompare(a.id)</c>). Ids are YYYY-MM-DD, so ordinal
        /// order is date order. Stable, like the JS sort, for equal ids.
        /// </summary>
        public static List<Week> SortedNewestFirst(IEnumerable<Week> weeks) =>
            weeks == null ? new List<Week>() : weeks.OrderByDescending(w => w.id ?? "", StringComparer.Ordinal).ToList();

        /// <summary>
        /// The week that opens by itself: the newest one on or before today, or the oldest week when every week is
        /// still in the future (screens/venue.js:11). Null when there are no weeks.
        /// </summary>
        public static Week DefaultWeek(IEnumerable<Week> weeks, string todayIso)
        {
            List<Week> s = SortedNewestFirst(weeks);
            if (s.Count == 0) return null;
            foreach (Week w in s) if (string.CompareOrdinal(w.id ?? "", todayIso ?? "") <= 0) return w;
            return s[s.Count - 1];
        }

        /// <summary>The week the player picked (by id), or the default week if that id isn't there (show.js:13, venue.js:19).</summary>
        public static Week ChooseWeek(IEnumerable<Week> weeks, string chosenId, string todayIso)
        {
            if (weeks != null && !string.IsNullOrEmpty(chosenId))
                foreach (Week w in weeks) if (w != null && w.id == chosenId) return w;
            return DefaultWeek(weeks, todayIso);
        }

        /// <summary>Today's date as the web game's <c>localISO()</c> writes it (js/util.js:13): YYYY-MM-DD from a local time.</summary>
        public static string ToIso(DateTime local) => local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        /// <summary>Words lyric writer can use: x is set and holds ___ (lyric.js:9).</summary>
        public static List<Word> WithSentence(Week week) =>
            week?.words == null ? new List<Word>() : week.words.Where(v => v != null && v.x != null && v.x.Contains("___")).ToList();

        /// <summary>Words prefix mixer can use: p, root and means all non-empty (prefix.js:10, JS truthiness).</summary>
        public static List<Word> WithPrefix(Week week) =>
            week?.words == null ? new List<Word>() : week.words.Where(v => v?.prefix != null
                && !string.IsNullOrEmpty(v.prefix.p) && !string.IsNullOrEmpty(v.prefix.root) && !string.IsNullOrEmpty(v.prefix.means)).ToList();

        /// <summary>Lyric writer is on the bill for this week (lyric.js:28).</summary>
        public static bool LyricWriterAvailable(Week week) => WithSentence(week).Count >= MinWordsForMode;

        /// <summary>Prefix mixer is on the bill for this week (prefix.js:27).</summary>
        public static bool PrefixMixerAvailable(Week week) => WithPrefix(week).Count >= MinWordsForMode;

        /// <summary>Exactly four digits, a hyphen, two digits, a hyphen, two digits (ASCII digits only).</summary>
        public static bool IsDateShape(string id)
        {
            if (id == null || id.Length != 10 || id[4] != '-' || id[7] != '-') return false;
            for (int k = 0; k < 10; k++) if (k != 4 && k != 7 && (id[k] < '0' || id[k] > '9')) return false;
            return true;
        }

        /// <summary>A date-shaped id that names a real calendar day (month 1-12, day within the month, leap years counted).</summary>
        public static bool IsRealDate(string id)
        {
            if (!IsDateShape(id)) return false;
            int y = int.Parse(id.Substring(0, 4), CultureInfo.InvariantCulture);
            int m = int.Parse(id.Substring(5, 2), CultureInfo.InvariantCulture);
            int d = int.Parse(id.Substring(8, 2), CultureInfo.InvariantCulture);
            if (y < 1 || m < 1 || m > 12 || d < 1) return false;
            int[] days = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
            bool leap = (y % 4 == 0 && y % 100 != 0) || y % 400 == 0;
            int max = m == 2 && leap ? 29 : days[m - 1];
            return d <= max;
        }
    }
}
