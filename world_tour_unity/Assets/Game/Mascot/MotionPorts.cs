using System;

namespace WorldTour.Game.Mascot
{
    // Minimal interfaces the mascot is written against until the 2.2 design system's Tween helper and
    // reduce-motion setting are known. The lead wires them to that code (an adapter of a few lines each).

    /// <summary>A running tween that can be stopped early (the next reaction replaces the last one).</summary>
    public interface ITweenHandle
    {
        void Cancel();
    }

    /// <summary>
    /// Runs a callback every frame for a duration, on unscaled time, without blocking input.
    /// step gets the linear progress 0..1 (the mascot applies its own keyframes and easing); step(1) is always
    /// called last unless cancelled, then done (if given).
    /// </summary>
    public interface ITweener
    {
        ITweenHandle Run(float seconds, Action<float> step, Action done = null);
    }

    /// <summary>The settings toggle (and the OS preference, if the design system reads it).</summary>
    public interface IMotionSettings
    {
        bool ReduceMotion { get; }
    }
}
