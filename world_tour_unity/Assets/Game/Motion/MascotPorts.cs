using System;
using WorldTour.Game.Mascot;

namespace WorldTour.Game.Motion
{
    /// <summary>
    /// The 2.3 mascot's ports (Game/Mascot/MotionPorts.cs) over the 2.2 design system. Place this file together with
    /// MotionPorts.cs; it does not compile without it.
    /// </summary>
    public sealed class TweenerPort : ITweener
    {
        public static readonly TweenerPort Instance = new TweenerPort();

        /// <summary>
        /// Linear progress 0..1 on unscaled time, step(1) last, then done. Reduce motion is NOT applied here: the mascot
        /// reads <see cref="MotionSettingsPort"/> and picks its own reduced behaviour.
        /// </summary>
        public ITweenHandle Run(float seconds, Action<float> step, Action done = null)
        {
            return new Handle(Tweener.Float(0f, 1f, seconds, Ease.Linear, step, done, owner: null, honourReduced: false));
        }

        sealed class Handle : ITweenHandle
        {
            readonly TweenHandle _h;
            public Handle(TweenHandle h) { _h = h; }
            /// <summary>Stops it without step(1) or done.</summary>
            public void Cancel() { _h.Kill(); }
        }
    }

    /// <summary>Reduce motion as the settings resolved it (save setting, or the browser's preference when "auto").</summary>
    public sealed class MotionSettingsPort : IMotionSettings
    {
        public static readonly MotionSettingsPort Instance = new MotionSettingsPort();
        public bool ReduceMotion { get { return MotionPrefs.Reduced; } }
    }
}
