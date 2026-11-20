using System.Collections.Generic;
using System.Linq;
using WorldTour.Core.Data;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// modes/index.js isAvailable and poolFor, with each mode's available(week) and pool(week) (lyric.js:69-70,
    /// prefix.js:155-156), and the venue card's note for modes left off a week (screens/venue.js:57).
    /// </summary>
    public static class ModeRules
    {
        private static readonly string Rsq = ((char)0x2019).ToString();

        /// <summary>index.js isAvailable: lyric writer and prefix mixer need 4 usable words; every other mode plays any week.</summary>
        public static bool IsAvailable(string modeId, Week week)
        {
            switch (modeId)
            {
                case "lyric": return WeekRules.LyricWriterAvailable(week);
                case "prefix": return WeekRules.PrefixMixerAvailable(week);
                default: return true;
            }
        }

        /// <summary>index.js poolFor: the words a show of this mode picks from.</summary>
        public static List<Word> Pool(string modeId, Week week)
        {
            switch (modeId)
            {
                case "lyric": return WeekRules.WithSentence(week);
                case "prefix": return WeekRules.WithPrefix(week);
                default: return week != null && week.words != null ? week.words.Where(v => v != null).ToList() : new List<Word>();
            }
        }

        /// <summary>
        /// venue.js:57: "not on this week's bill: lyric writer and prefix mixer. prefix mixer plays on weeks built around
        /// prefixes. lyric writer needs sentences for the words. " for the spelling modes left off (ids and names in
        /// setlist order), or null when every mode is on. The trailing space is the web's.
        /// </summary>
        public static string BillNote(IReadOnlyList<KeyValuePair<string, string>> hidden)
        {
            if (hidden == null || hidden.Count == 0) return null;
            string s = "not on this week" + Rsq + "s bill: " + string.Join(" and ", hidden.Select(m => m.Value)) + ". ";
            if (hidden.Any(m => m.Key == "prefix")) s += "prefix mixer plays on weeks built around prefixes. ";
            if (hidden.Any(m => m.Key == "lyric")) s += "lyric writer needs sentences for the words. ";
            return s;
        }
    }
}
