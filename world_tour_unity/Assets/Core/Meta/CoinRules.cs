using System;
using WorldTour.Core.Progress;

namespace WorldTour.Core.Meta
{
    /// <summary>
    /// Phase 10B.1: coins, the second number beside fans. Fans unlock places and are never spent or lost; coins are what the
    /// stores take. Coins come only from answering in gigs and shows (a first-try answer earns most, a hinted one still earns
    /// some, a miss earns nothing and costs nothing) and are only ever taken by a purchase she chose.
    /// Numbers (DECISIONS.md, 10B.1): first try 4, hinted 2; a normal 15 minute session is about 50 answers, so about 160 coins:
    /// small items 20 to 40, big ones 200 to 350 (two or three sessions).
    /// </summary>
    public static class CoinRules
    {
        /// <summary>Coins for a right answer with no wrong try and no hint.</summary>
        public const int FirstTryCoins = 4;
        /// <summary>Coins for a right answer that used a hint but had no wrong try.</summary>
        public const int HintedCoins = 2;
        /// <summary>The old-save grant: one coin for every this many lifetime fans (a 10-song show is about 20,000 fans, so 40 coins).</summary>
        public const int FansPerGrantCoin = 500;
        /// <summary>The old-save grant never gives more than this, so the first visit buys a few things and not the whole store.</summary>
        public const int GrantCap = 500;

        /// <summary>What she has to spend.</summary>
        public static long Coins(SaveBlob s) { return s.Fans.Coins; }

        /// <summary>Takes <paramref name="n"/> coins when she has them and returns true; otherwise changes nothing. A price of 0 or less is not a spend.</summary>
        public static bool TrySpend(SaveBlob s, long n)
        {
            if (n < 0 || s.Fans.Coins < n) return false;
            s.Fans.Coins -= n;
            return true;
        }

        /// <summary>
        /// The coins one answer earns, added to the save and returned. <paramref name="firstTry"/>: no wrong try and no hint (3).
        /// Otherwise a hinted answer that had no wrong try earns 2. A miss (a wrong try before the right one, or the answer shown)
        /// earns 0: call with hinted false and firstTry false, or simply don't call.
        /// </summary>
        public static long Earn(SaveBlob s, bool firstTry, bool hinted)
        {
            long n = Amount(firstTry, hinted);
            s.Fans.Coins += n;
            return n;
        }

        /// <summary>What <see cref="Earn"/> would give, without changing anything.</summary>
        public static long Amount(bool firstTry, bool hinted)
        {
            if (firstTry && !hinted) return FirstTryCoins;
            return hinted ? HintedCoins : 0;
        }

        /// <summary>
        /// Phase 10B.1, the one-time grant: a save with fans from before coins gets lifetime fans / 500 coins (at most 500) the first
        /// time this runs, and the save remembers it, so it never repeats. Returns the coins given (0 when it was done before).
        /// A brand new save is marked done with 0 coins.
        /// </summary>
        public static long GrantFromFans(SaveBlob s)
        {
            if (s.Fans.CoinsGranted) return 0;
            long n = Math.Min(GrantCap, Math.Max(0, s.Fans.Lifetime) / FansPerGrantCoin);
            s.Fans.Coins += n;
            s.Fans.CoinsGranted = true;
            return n;
        }

        /// <summary>The coin price of an old backstage item: its old price in fans over 500, rounded up (5,000 fans is 10 coins; 60,000 is 120).</summary>
        public static int PriceOf(ShopItem it)
        {
            if (it == null || it.Price <= 0) return 0;
            return (it.Price + FansPerGrantCoin - 1) / FansPerGrantCoin;
        }
    }
}
