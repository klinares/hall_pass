using System;

namespace WorldTour.Core.Shows
{
    /// <summary>How a song was won (world_tour/js/show.js win(), lines 141-145).</summary>
    public enum WinKind
    {
        /// <summary>No wrong tries, no hints: the streak grows.</summary>
        Clean,
        /// <summary>Hints but no wrong tries: the streak is kept, not grown.</summary>
        HintOnly,
        /// <summary>Right after one or more wrong tries (the wrong try already reset the streak).</summary>
        AfterMiss
    }

    /// <summary>The show's numbers, ported from world_tour/js/show.js.</summary>
    public static class Scoring
    {
        /// <summary>show.js:18 <c>SHOW_LEN = 10</c>.</summary>
        public const int ShowLength = 10;
        /// <summary>show.js:19 <c>MAX_BONUS = 2</c>: at most this many misses are re-inserted per show (none in an encore).</summary>
        public const int MaxBonus = 2;
        /// <summary>show.js:185 <c>G.i + 4</c>: a re-inserted miss comes back with three songs in between.</summary>
        public const int RequeueOffset = 4;
        /// <summary>show.js:32 <c>.slice(0, 3)</c>: review problems at the start of a math show.</summary>
        public const int ReviewPerShow = 3;
        /// <summary>show.js:143 <c>1000 * Math.min(G.streak, 5)</c>.</summary>
        public const int CleanPerStreak = 1000;
        public const int StreakCap = 5;
        /// <summary>show.js:144.</summary>
        public const int HintOnlyGain = 500;
        /// <summary>show.js:145.</summary>
        public const int AfterMissGain = 300;
        /// <summary>ticket.js:18 <c>vip = pct &gt;= .9</c>, and show.js:221 sold out.</summary>
        public const double VipAt = 0.9;
        /// <summary>show.js:221 <c>n &gt;= 5</c> for a sold-out show.</summary>
        public const int SoldOutMinSongs = 5;

        /// <summary>show.js:141-145: the kind of win from this song's wrong tries and hints.</summary>
        public static WinKind Kind(int tries, int hints)
        {
            if (tries == 0 && hints == 0) return WinKind.Clean;
            return tries == 0 ? WinKind.HintOnly : WinKind.AfterMiss;
        }

        /// <summary>
        /// show.js:143-147: fans for one song. <paramref name="streakAfter"/> is the streak including this song
        /// (used only for a clean win). An encore halves it (JS Math.round), then <paramref name="extra"/>
        /// (soundcheck's beat-the-meter bonus) is added on top, never halved. Never below zero.
        /// </summary>
        public static int Gain(WinKind kind, int streakAfter, bool encore, int extra = 0)
        {
            if (extra < 0) throw new ArgumentOutOfRangeException(nameof(extra), "bonus fans can't be negative");
            int gain;
            switch (kind)
            {
                case WinKind.Clean: gain = CleanPerStreak * Math.Min(Math.Max(streakAfter, 1), StreakCap); break;
                case WinKind.HintOnly: gain = HintOnlyGain; break;
                default: gain = AfterMissGain; break;
            }
            if (encore) gain = (int)Math.Floor(gain / 2.0 + 0.5);
            return gain + extra;
        }

        /// <summary>show.js:219 <c>(G.firstTry + .5 * G.hintOnly) / Math.max(1, n)</c>.</summary>
        public static double Percent(int firstTry, int hintOnly, int n)
        {
            return (firstTry + .5 * hintOnly) / Math.Max(1, n);
        }

        /// <summary>show.js:221 <c>!G.encore &amp;&amp; n &gt;= 5 &amp;&amp; pct &gt;= .9</c>.</summary>
        public static bool SoldOut(bool encore, int n, double pct)
        {
            return !encore && n >= SoldOutMinSongs && pct >= VipAt;
        }
    }
}
