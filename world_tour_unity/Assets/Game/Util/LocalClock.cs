using System;
using WorldTour.Core.Data;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace WorldTour.Game.Util
{
    /// <summary>
    /// Today's date on the player's own clock, as "YYYY-MM-DD" (the web game's localISO, js/util.js:13). In the web player
    /// it asks the browser (Plugins/WebGL/clock.jslib), because DateTime.Now there may be UTC; in the Editor it is
    /// DateTime.Now. Core never reads a clock: pass this in (WeekRules.DefaultWeek, the day streak).
    /// </summary>
    public static class LocalClock
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern string WT_LocalDateIso();

        public static string TodayIso()
        {
            try
            {
                string iso = WT_LocalDateIso();
                if (WeekRules.IsRealDate(iso)) return iso;
            }
            catch (Exception)
            {
                // fall through to the runtime clock
            }
            return WeekRules.ToIso(DateTime.Now);
        }
#else
        public static string TodayIso() => WeekRules.ToIso(DateTime.Now);
#endif
    }
}
