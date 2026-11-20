using System;
using System.Globalization;

namespace WorldTour.Core.Progress
{
    /// <summary>
    /// Day stamps, ported from world_tour/js/util.js (<c>localISO</c>, <c>daysBetween</c>). Core has no clock:
    /// the Game layer passes today's stamp in (<c>Dates.LocalIso(DateTime.Now)</c>).
    /// </summary>
    public static class Dates
    {
        /// <summary>util.js:13 <c>localISO(d)</c>: the local calendar day as "yyyy-mm-dd". Pass a local DateTime.</summary>
        public static string LocalIso(DateTime local)
        {
            return local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// util.js:14 <c>Math.round((Date.parse(b) - Date.parse(a)) / 864e5)</c> for two "yyyy-mm-dd" stamps.
        /// Null where the JS gives NaN (a stamp that isn't a date). Only the date-only form the game writes is
        /// read; JS Date.parse would also take other forms.
        /// </summary>
        public static int? DaysBetween(string a, string b)
        {
            DateTime da, db;
            if (!TryParse(a, out da) || !TryParse(b, out db)) return null;
            return (int)Math.Round((db - da).TotalDays, MidpointRounding.AwayFromZero);
        }

        /// <summary>True for a real calendar day written "yyyy-mm-dd".</summary>
        public static bool IsDay(string s)
        {
            DateTime d;
            return TryParse(s, out d);
        }

        private static bool TryParse(string s, out DateTime d)
        {
            return DateTime.TryParseExact(s ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out d);
        }
    }
}
