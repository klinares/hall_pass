using System;
using System.Collections.Generic;
using System.Globalization;

namespace WorldTour.Core.Maths
{
    /// <summary>
    /// Money held as integer cents, ported from world_tour/math/money.js, so 0.1 + 0.2 never turns
    /// into 0.30000000000000004. The arithmetic below repeats the JS double operations step for step,
    /// so odd inputs (negatives) give the same text and numbers as the web game.
    /// </summary>
    public static class Money
    {
        /// <summary>
        /// JS: <c>dollars = c =&gt; `$${Math.floor(c / 100)}.${String(c % 100).padStart(2, '0')}`</c>.
        /// 350 is "$3.50", 5 is "$0.05". A negative amount comes out the JS way: -5 is "$-1.-5".
        /// </summary>
        public static string Dollars(long cents)
        {
            long whole = (long)Math.Floor(cents / 100.0);
            long rest = cents % 100; // truncated remainder, same sign as cents, like JS %
            return "$" + whole.ToString(CultureInfo.InvariantCulture) + "." +
                   rest.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0');
        }

        /// <summary>
        /// JS: <c>noCarryAdd(nums)</c>: the digit-by-digit sum a kid gets by never carrying.
        /// 175 + 250 gives 325. Used to spot that mistake in a typed answer. An empty list gives 0.
        /// </summary>
        public static long NoCarryAdd(IReadOnlyList<long> nums)
        {
            if (nums == null) throw new ArgumentNullException(nameof(nums));
            // Math.max(...nums.map(n => String(n).length)); -Infinity for an empty list, so no loop.
            int len = 0;
            foreach (long n in nums) len = Math.Max(len, n.ToString(CultureInfo.InvariantCulture).Length);
            double output = 0, place = 1;
            for (int i = 0; i < len; i++)
            {
                double s = 0;
                foreach (long n in nums) s = s + Math.Floor(n / place) % 10;
                double d = s % 10;
                output += d * place;
                place *= 10;
            }
            return (long)output;
        }

        /// <summary>Convenience overload: <c>NoCarryAdd(175, 250)</c>.</summary>
        public static long NoCarryAdd(params long[] nums) => NoCarryAdd((IReadOnlyList<long>)nums);

        /// <summary>
        /// JS: <c>noBorrowSub(a, b)</c>: digit by digit, smaller digit from larger, never borrowing.
        /// 1000 - 375 gives 1375.
        /// </summary>
        public static long NoBorrowSub(long a, long b)
        {
            double output = 0, place = 1;
            double da = a, db = b;
            while (place <= Math.Max(da, db))
            {
                double x = Math.Floor(da / place) % 10, y = Math.Floor(db / place) % 10;
                output += Math.Abs(x - y) * place;
                place *= 10;
            }
            return (long)output;
        }
    }
}
