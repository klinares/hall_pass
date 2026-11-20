using System;
using System.Collections.Generic;
using WorldTour.Core.Shows;

namespace WorldTour.Game.Mascot
{
    /// <summary>
    /// riff's own lines, ported word for word from world_tour/js/copy.js and js/show.js. No new copy.
    /// The miss ladder and the reveal line (show.js ladder, reveal) are mode lines: they live only in
    /// Game/Modes RemixLines and reach riff through ShowScreen.Said (the reveal through DogCueInput.RevealLine).
    /// The web text uses curly apostrophes; this source stays ASCII, so every ASCII apostrophe below is turned
    /// into U+2019 when the class loads.
    /// Lines carry the web's &lt;b&gt; tags, which TextMeshPro reads as bold. Random choices take a
    /// <c>Func&lt;double&gt;</c> in [0, 1) and use it exactly as the JS does (pick = arr[floor(r * length)]), so a
    /// seeded source repeats and a value is only drawn where the JS draws one.
    /// </summary>
    public static class DogLines
    {
        static readonly char Rsq = (char)0x2019;   // right single quote, the web's apostrophe

        static string Q(string s) => s.Replace('\'', Rsq);
        static string[] Q(params string[] a) { var r = new string[a.Length]; for (int i = 0; i < a.Length; i++) r[i] = Q(a[i]); return r; }

        /// <summary>copy.js:5 NAME.</summary>
        public const string Name = "natalia";

        /// <summary>copy.js:8 CHEERS: the toast after a win (show.js:162).</summary>
        public static readonly IReadOnlyList<string> Cheers = Q("iconic", "yes!!", "flawless", "encore!", "obsessed", "crowd screaming", "main character");

        /// <summary>copy.js:30 dogShowStart: the bubble when a show opens (show.js:82).</summary>
        public static readonly IReadOnlyList<string> ShowStart = Q("sound check done. let's go.", "deep breath. first song.", "crowd's in. first song.", "i'll be right here if you need a hint.");

        /// <summary>copy.js:31 dogWin, the pick when the streak is under 3.</summary>
        public static readonly IReadOnlyList<string> Win = Q("clean.", "nailed it.", "that one's going on the poster.", "yep. that's the one.", "easy work.");

        /// <summary>copy.js:32 dogHintWin.</summary>
        public static readonly IReadOnlyList<string> HintWin = Q("got there. streak's safe.", "hint + you = still a hit.", "that counts. streak's still going.");

        /// <summary>copy.js:33 dogAfterMiss.</summary>
        public static readonly IReadOnlyList<string> AfterMiss = Q("got it on the rebound.", "comeback kid.", "second take was the keeper.");

        /// <summary>show.js:198: the bubble when the next song starts.</summary>
        public static readonly IReadOnlyList<string> NextSong = Q("next song.", "keep it rolling.", "crowd's warmed up.", "next one.");

        /// <summary>show.js:198: the bubble when a bonus replay starts.</summary>
        public static readonly string BonusSong = Q("this one again. you've got it now.");

        /// <summary>copy.js:34 dogIdleHint: after 15 s with an unused hint (show.js:115).</summary>
        public static readonly string IdleHint = Q("stuck? tap the bulb. your streak is safe, you just win fewer fans.");

        static T Pick<T>(IReadOnlyList<T> a, Func<double> random) => a[(int)Math.Floor(random() * a.Count)];

        public static string PickShowStart(Func<double> random) => Pick(ShowStart, random);

        /// <summary>copy.js:31: a streak of 3 or more is named and draws no random value.</summary>
        public static string PickWin(int streak, Func<double> random) =>
            streak >= 3 ? streak + Q(" in a row. the crowd is losing it.") : Pick(Win, random);

        public static string PickHintWin(Func<double> random) => Pick(HintWin, random);
        public static string PickAfterMiss(Func<double> random) => Pick(AfterMiss, random);
        public static string PickNextSong(Func<double> random) => Pick(NextSong, random);

        /// <summary>show.js:163 the win line by kind (clean: dogWin(streak), hints only: dogHintWin, after a miss: dogAfterMiss).</summary>
        public static string ForWin(WinKind kind, int streak, Func<double> random)
        {
            switch (kind)
            {
                case WinKind.Clean: return PickWin(streak, random);
                case WinKind.HintOnly: return PickHintWin(random);
                default: return PickAfterMiss(random);
            }
        }

        /// <summary>show.js:162 the toast (not a dog line, for the feedback layer): "3 in a row!" on a clean streak of 3+, else a cheer.</summary>
        public static string Toast(bool clean, int streak, Func<double> random) =>
            clean && streak >= 3 ? streak + " in a row!" : Pick(Cheers, random);

        /// <summary>
        /// copy.js:19-28 dogHome: the map bubble. next is the next venue's short name (null when every venue is open),
        /// toGo the fans still needed, already formatted en-US (fmt), daysAway null when she has never played.
        /// No random value is drawn for the nap line; otherwise one pick from the two lines.
        /// </summary>
        public static string Home(string next, string toGo, int streak, int? daysAway, Func<double> random)
        {
            if (daysAway != null && daysAway >= 3) return "oh hey. i napped for " + daysAway + " days. ready when you are.";
            var lines = new List<string>(2);
            if (next != null) lines.Add("next stop: <b>" + next + "</b>. " + toGo + " fans to go.");
            else lines.Add("we made it to the arena. every show from here is a victory lap.");
            if (streak >= 2) lines.Add(streak + Q(" days in a row. don't break the chain."));
            else lines.Add("one show a day keeps the streak alive.");
            return Pick(lines, random);
        }
    }
}
