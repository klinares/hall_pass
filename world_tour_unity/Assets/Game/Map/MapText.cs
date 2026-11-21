using System;
using System.Globalization;
using WorldTour.Core.Data;
using WorldTour.Core.Meta;

namespace WorldTour.Game.Map
{
    /// <summary>
    /// The map and venue card copy, word for word from world_tour/screens/map.js, screens/venue.js and js/copy.js,
    /// with the number formats of js/util.js. Non-ASCII characters are built from their codes so sources stay ASCII.
    /// Bold uses TMP rich text where the web uses &lt;b&gt;.
    /// </summary>
    public static class MapText
    {
        /// <summary>U+2019 right single quote.</summary>
        public static readonly string Apos = ((char)0x2019).ToString();
        /// <summary>U+2736 six-pointed black star, the web's separator.</summary>
        public static readonly string Star = ((char)0x2736).ToString();
        /// <summary>U+2192 rightwards arrow.</summary>
        public static readonly string Arrow = ((char)0x2192).ToString();
        /// <summary>U+2191 upwards arrow (screens.css:77 .here-tag::before).</summary>
        public static readonly string UpArrow = ((char)0x2191).ToString();

        /// <summary>copy.js:5 NAME.</summary>
        public const string Name = "natalia";

        /// <summary>util.js:16 <c>fmt</c>: rounded, en-US thousands separators (no culture data needed).</summary>
        public static string Fmt(double n)
        {
            return ((long)JsNum.Round(n)).ToString("#,##0", CultureInfo.InvariantCulture);
        }

        /// <summary>util.js:17 <c>short</c>: 1.2m, 20k, 950.</summary>
        public static string Short(long n)
        {
            if (n >= 1000000) return JsNum.ToFixedNumber(n / 1e6, n % 1000000 != 0 ? 1 : 0).ToString(n % 1000000 != 0 ? "0.0" : "0", CultureInfo.InvariantCulture) + "m";
            if (n >= 1000) return ((long)JsNum.Round(n / 1e3)).ToString(CultureInfo.InvariantCulture) + "k";
            return n.ToString(CultureInfo.InvariantCulture);
        }

        // ---------- map.js ----------

        /// <summary>map.js:30.</summary>
        public static string Eyebrow { get { return "spelling + math " + Star + " on the road"; } }

        /// <summary>map.js:31 (the &lt;br&gt; as a line break).</summary>
        public static string Title { get { return Name + Apos + "s\nworld tour"; } }

        /// <summary>map.js:32.</summary>
        public static string Tonight(Venue newest) { return "tonight: " + newest.Short + " " + Arrow; }

        /// <summary>map.js:37.</summary>
        public const string TotalFans = "total fans";

        /// <summary>map.js:41.</summary>
        public static string DayStreak(int streak, int best)
        {
            return "day streak" + (best > streak ? " " + Star + " best " + best.ToString(CultureInfo.InvariantCulture) : "");
        }

        /// <summary>map.js:51.</summary>
        public const string RouteHeading = "the route";

        /// <summary>map.js:52: the line over the map.</summary>
        public static string RouteLine(Venue next)
        {
            return next != null ? Short(next.Fans) + " fans unlocks " + next.Short : "every venue unlocked. legend.";
        }

        /// <summary>map.js:60 backstage card: "12,000 fans to spend".</summary>
        public static string FansToSpend(long balance) { return Fmt(balance) + " fans to spend"; }

        /// <summary>map.js:61 journal card: "3 of 20 stickers".</summary>
        public static string StickersOf(int have, int total) { return have + " of " + total + " stickers"; }

        /// <summary>map.js:84 the venue number, "01".."08".</summary>
        public static string VenueNumber(int index) { return (index + 1).ToString("00", CultureInfo.InvariantCulture); }

        /// <summary>map.js:87 the sticky note on a locked venue: "20k" over "fans".</summary>
        public static string StickyFans(Venue v) { return Short(v.Fans); }

        public const string StickySmall = "fans";

        /// <summary>map.js:88 and screens.css:77.</summary>
        public static string HereTag { get { return UpArrow + " you" + Apos + "re here"; } }

        /// <summary>map.js:83 the venue's accessible name.</summary>
        public static string VenueAria(Venue v, bool open)
        {
            return v.Name + (open ? "" : ", locked, needs " + Fmt(v.Fans) + " fans");
        }

        // ---------- copy.js dogHome ----------

        /// <summary>
        /// copy.js:19 <c>dogHome</c>: after 3 or more days away the nap line; otherwise one of two lines, picked by
        /// <paramref name="pickSecond"/> (the web's Math.random pick). <paramref name="toGo"/> is already formatted.
        /// </summary>
        /// <summary>7.4 (kid-fit): the dog's line on the map until her first show is done, so she knows where to start.</summary>
        public const string FirstVisit = "pick any set to play a show. math is further down.";

        public static string DogHome(string next, string toGo, int streak, int? away, bool pickSecond)
        {
            if (away != null && away.Value >= 3) return "oh hey. i napped for " + away.Value + " days. ready when you are.";
            string first = next != null ? "next stop: <b>" + next + "</b>. " + toGo + " fans to go."
                                        : "we made it to the arena. every show from here is a victory lap.";
            string second = streak >= 2 ? streak + " days in a row. don" + Apos + "t break the chain."
                                        : "one show a day keeps the streak alive.";
            return pickSecond ? second : first;
        }

        // ---------- venue.js ----------

        /// <summary>venue.js:26 the back link.</summary>
        public const string BackToMap = "the map";

        /// <summary>venue.js:32: "stop 2 of 8 * tonight".</summary>
        public static string StopOf(int index, int count, bool open)
        {
            return "stop " + (index + 1) + " of " + count + " " + Star + " " + (open ? "tonight" : "locked");
        }

        /// <summary>venue.js:35: "unlocks at <b>20,000</b> fans. 5,000 to go." (after the lock icon).</summary>
        public static string Need(Venue v, long lifetime)
        {
            return "unlocks at <b>" + Fmt(v.Fans) + "</b> fans. " + Fmt(v.Fans - lifetime) + " to go.";
        }

        /// <summary>venue.js:42.</summary>
        public const string SpellingSets = "spelling sets";

        /// <summary>venue.js:44.</summary>
        public const string TourDate = "tour date: pick a set";

        /// <summary>venue.js:58 the math section's heading and its pencil note.</summary>
        public const string MathSets = "math sets";
        public const string LevelsMove = "levels move with you";

        /// <summary>venue.js:64 .mlvl: "<stars> level n" (filled stars for the level, empty for the rest of 3).</summary>
        public static string LevelStars(int level)
        {
            int l = System.Math.Max(0, System.Math.Min(3, level));
            return new string((char)0x2605, l) + new string((char)0x2606, 3 - l);
        }

        /// <summary>venue.js:68: the button on a locked venue's card.</summary>
        public static string PlayInstead(Venue newest) { return "play at " + newest.Short + " instead"; }
    }
}
