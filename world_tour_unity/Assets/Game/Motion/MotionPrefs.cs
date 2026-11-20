using System;

namespace WorldTour.Game.Motion
{
    /// <summary>
    /// The one switch every motion reads (motion.js reduced()). Set by GameSettings from the save and the browser's
    /// prefers-reduced-motion. When on, movement is dropped and replaced by a 120 ms fade, as base.css does.
    /// </summary>
    public static class MotionPrefs
    {
        static bool s_Reduced;

        public static bool Reduced
        {
            get { return s_Reduced; }
            set
            {
                if (s_Reduced == value) return;
                s_Reduced = value;
                Changed?.Invoke();
            }
        }

        public static event Action Changed;

        /// <summary>Test and domain-reload helper.</summary>
        public static void Reset() { s_Reduced = false; Changed = null; }
    }
}
