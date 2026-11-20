using System;

namespace WorldTour.Game.Mascot
{
    /// <summary>A CSS cubic-bezier timing function.</summary>
    public sealed class CubicBezier
    {
        readonly double _x1, _y1, _x2, _y2;
        public CubicBezier(double x1, double y1, double x2, double y2) { _x1 = x1; _y1 = y1; _x2 = x2; _y2 = y2; }

        static double B(double a, double b, double u) => 3 * a * u * (1 - u) * (1 - u) + 3 * b * u * u * (1 - u) + u * u * u;
        static double dB(double a, double b, double u) => 3 * a * (1 - u) * (1 - u) + 6 * (b - a) * u * (1 - u) + 3 * (1 - b) * u * u;

        /// <summary>Eased progress for linear progress t in [0, 1] (may leave [0, 1] for overshooting curves).</summary>
        public double Ease(double t)
        {
            if (t <= 0) return 0;
            if (t >= 1) return 1;
            double u = t;
            for (int i = 0; i < 8; i++)
            {
                double x = B(_x1, _x2, u) - t, d = dB(_x1, _x2, u);
                if (Math.Abs(x) < 1e-7) return B(_y1, _y2, u);
                if (Math.Abs(d) < 1e-6) break;
                u -= x / d;
            }
            double lo = 0, hi = 1; u = t;
            for (int i = 0; i < 40; i++)
            {
                double x = B(_x1, _x2, u);
                if (Math.Abs(x - t) < 1e-7) break;
                if (x < t) lo = u; else hi = u;
                u = (lo + hi) / 2;
            }
            return B(_y1, _y2, u);
        }

        /// <summary>tokens.css --spring, motion.js run() default.</summary>
        public static readonly CubicBezier Spring = new CubicBezier(.34, 1.56, .64, 1);
        public static readonly CubicBezier Ease_ = new CubicBezier(.25, .1, .25, 1);
        public static readonly CubicBezier EaseInOut = new CubicBezier(.42, 0, .58, 1);
        public static readonly CubicBezier EaseOut = new CubicBezier(0, 0, .58, 1);
    }

    /// <summary>Where the dog is drawn this frame, relative to rest. CSS directions: y down, degrees clockwise.</summary>
    public struct DogOffset
    {
        public double X, Y, Rotation, ScaleX, ScaleY, Alpha;
        public static DogOffset Rest => new DogOffset { ScaleX = 1, ScaleY = 1, Alpha = 1 };
    }

    /// <summary>
    /// The dog's timings, ported from the web where it has them: js/motion.js (hop, wobble, the reduced-motion
    /// fade) and css/screens.css (tail wag, blink, zzz). The idle bob is new (the web dog has none).
    /// Times are seconds; lengths are CSS px (= canvas units on a 1920x1080 reference) unless they say dog units
    /// (the 160-unit dog.js viewBox; multiply by the manifest's unitsToPx for sprite px).
    /// Under reduced motion every loop is at rest and a reaction is only a 120 ms fade from .6 (motion.js run()).
    /// </summary>
    public static class DogTiming
    {
        public const double HopSeconds = .38, WobbleSeconds = .34, ReducedSeconds = .12;
        public const double WagSeconds = 1.1, WagDegrees = 14;
        public const double BlinkSeconds = 4.2;
        public const double ZzzSeconds = 2.4, ZzzDelaySeconds = .4, ZzzDx = 3, ZzzDy = -5, ZzzAlpha = .55;
        /// <summary>New in Unity (the web dog has no bob): a 3 px rise and fall every 2.4 s.</summary>
        public const double BobSeconds = 2.4, BobPx = 3;
        /// <summary>show.js:111: the hint nudge fires after 15 s on one song with hints left.</summary>
        public const double NudgeSeconds = 15;

        /// <summary>Seconds a reaction runs (0 for none).</summary>
        public static double Duration(DogMotion m, bool reduced)
        {
            if (m == DogMotion.None) return 0;
            if (reduced) return ReducedSeconds;
            return m == DogMotion.Hop ? HopSeconds : WobbleSeconds;
        }

        /// <summary>
        /// Web Animations keyframes: offsets and values, sampled at eased progress p. The easing in motion.js is
        /// the effect easing (whole animation), so p can overshoot past 1 with --spring; like the browser, the
        /// first and last segments are then extended.
        /// </summary>
        public static double Keyframes(double[] offsets, double[] values, double p)
        {
            int n = offsets.Length;
            int i = 0;
            if (p >= offsets[n - 1]) i = n - 2;
            else if (p > offsets[0]) while (i < n - 2 && p > offsets[i + 1]) i++;
            double span = offsets[i + 1] - offsets[i];
            double f = span <= 0 ? 0 : (p - offsets[i]) / span;
            return values[i] + (values[i + 1] - values[i]) * f;
        }

        static readonly double[] HopAt = { 0, .4, 1 };
        static readonly double[] WobAt = { 0, .25, .5, .75, 1 };

        /// <summary>The reaction t seconds after it started. Rest once it is over.</summary>
        public static DogOffset Reaction(DogMotion m, double t, bool reduced)
        {
            var o = DogOffset.Rest;
            double d = Duration(m, reduced);
            if (d <= 0 || t < 0 || t >= d) return o;
            if (reduced) { o.Alpha = .6 + .4 * (t / d); return o; }      // linear: run() gives no easing here
            if (m == DogMotion.Hop)
            {
                double p = CubicBezier.Spring.Ease(t / d);
                o.Y = Keyframes(HopAt, new double[] { 0, -16, 0 }, p);
                o.ScaleX = Keyframes(HopAt, new double[] { 1, 1.04, 1 }, p);
                o.ScaleY = Keyframes(HopAt, new double[] { 1, .96, 1 }, p);
            }
            else
            {
                double p = CubicBezier.EaseOut.Ease(t / d);
                o.X = Keyframes(WobAt, new double[] { 0, -7, 6, -3, 0 }, p);
                o.Rotation = Keyframes(WobAt, new double[] { 0, -2, 1.5, -.5, 0 }, p);
            }
            return o;
        }

        static double Phase(double time, double period) { double p = (time % period) / period; return p < 0 ? p + 1 : p; }

        /// <summary>screens.css wag: rotate 0 -> 14deg (at 50%) -> 0 over 1.1 s, ease-in-out per keyframe, about (103, 128) units.</summary>
        public static double TailDegrees(double time, bool reduced)
        {
            if (reduced) return 0;
            double ph = Phase(time, WagSeconds);
            return ph < .5 ? WagDegrees * CubicBezier.EaseInOut.Ease(ph / .5) : WagDegrees * (1 - CubicBezier.EaseInOut.Ease((ph - .5) / .5));
        }

        /// <summary>screens.css blink: lids scaleY 0 until 92%, 1 at 95%, 0 at 100% of 4.2 s ('ease' per keyframe). 0 means hidden.</summary>
        public static double LidScale(double time, bool reduced)
        {
            if (reduced) return 0;
            double ph = Phase(time, BlinkSeconds);
            if (ph < .92) return 0;
            if (ph < .95) return CubicBezier.Ease_.Ease((ph - .92) / .03);
            return 1 - CubicBezier.Ease_.Ease((ph - .95) / .05);
        }

        /// <summary>
        /// screens.css zz for z number index (0, 1, 2): at 50% of 2.4 s it is moved (3, -5) dog units with opacity
        /// .55, ease-in-out per keyframe; the second and third start .4 s and .8 s later (at rest until then).
        /// </summary>
        public static DogOffset Zzz(double time, int index, bool reduced)
        {
            var o = DogOffset.Rest;
            double delay = ZzzDelaySeconds * index;
            if (reduced || time < delay) return o;
            double ph = Phase(time - delay, ZzzSeconds);
            double f = ph < .5 ? CubicBezier.EaseInOut.Ease(ph / .5) : 1 - CubicBezier.EaseInOut.Ease((ph - .5) / .5);
            o.X = ZzzDx * f; o.Y = ZzzDy * f; o.Alpha = 1 + (ZzzAlpha - 1) * f;
            return o;
        }

        /// <summary>The idle bob (new): 0 at rest, up to -3 px (up) halfway through each 2.4 s, smooth.</summary>
        public static double BobY(double time, bool reduced)
        {
            if (reduced) return 0;
            double ph = Phase(time, BobSeconds);
            return -BobPx * (1 - Math.Cos(2 * Math.PI * ph)) / 2;
        }
    }
}
