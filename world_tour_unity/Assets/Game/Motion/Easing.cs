using System;

namespace WorldTour.Game.Motion
{
    /// <summary>
    /// A CSS cubic-bezier(x1, y1, x2, y2) timing function, solved the way browsers do (Newton steps, then bisection).
    /// y may leave 0..1 (the spring curve overshoots); x is clamped to 0..1 as CSS requires. Plain C#.
    /// </summary>
    public readonly struct CubicBezier
    {
        readonly float _x1, _y1, _x2, _y2;
        readonly bool _linear;

        public CubicBezier(float x1, float y1, float x2, float y2)
        {
            _x1 = Clamp01(x1); _y1 = y1; _x2 = Clamp01(x2); _y2 = y2;
            _linear = _x1 == _y1 && _x2 == _y2;
        }

        /// <summary>Linear when default-constructed or when the control points sit on the diagonal.</summary>
        public bool IsLinear { get { return _linear || (_x1 == 0f && _y1 == 0f && _x2 == 0f && _y2 == 0f); } }

        /// <summary>Eased progress for linear progress <paramref name="t"/> (0..1; values outside are clamped).</summary>
        public float Evaluate(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            if (IsLinear) return t;
            return SampleY(SolveX(t));
        }

        // B(s) = 3(1-s)^2 s p1 + 3(1-s) s^2 p2 + s^3, with p0 = 0 and p3 = 1.
        static float Bez(float s, float p1, float p2)
        {
            float inv = 1f - s;
            return 3f * inv * inv * s * p1 + 3f * inv * s * s * p2 + s * s * s;
        }

        static float BezD(float s, float p1, float p2)
        {
            float inv = 1f - s;
            return 3f * inv * inv * p1 + 6f * inv * s * (p2 - p1) + 3f * s * s * (1f - p2);
        }

        float SampleY(float s) { return Bez(s, _y1, _y2); }

        float SolveX(float x)
        {
            float s = x;
            for (int i = 0; i < 8; i++)
            {
                float err = Bez(s, _x1, _x2) - x;
                if (Math.Abs(err) < 1e-6f) return s;
                float d = BezD(s, _x1, _x2);
                if (Math.Abs(d) < 1e-6f) break;
                s -= err / d;
            }
            float lo = 0f, hi = 1f;
            s = x;
            for (int i = 0; i < 40; i++)
            {
                float v = Bez(s, _x1, _x2);
                if (Math.Abs(v - x) < 1e-6f) return s;
                if (v < x) lo = s; else hi = s;
                s = 0.5f * (lo + hi);
            }
            return s;
        }

        static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }
    }

    /// <summary>The curves the web game uses (tokens.css and motion.js), by name.</summary>
    public static class Ease
    {
        /// <summary>--spring cubic-bezier(.34,1.56,.64,1): overshoots, the default for motion.js run().</summary>
        public static readonly CubicBezier Spring = new CubicBezier(0.34f, 1.56f, 0.64f, 1f);
        /// <summary>--ease cubic-bezier(.2,.8,.2,1): a quick settle (slideIn).</summary>
        public static readonly CubicBezier Out = new CubicBezier(0.2f, 0.8f, 0.2f, 1f);
        /// <summary>CSS "ease-out" (wobble).</summary>
        public static readonly CubicBezier CssEaseOut = new CubicBezier(0f, 0f, 0.58f, 1f);
        /// <summary>CSS "ease-in" (shrink).</summary>
        public static readonly CubicBezier CssEaseIn = new CubicBezier(0.42f, 0f, 1f, 1f);
        /// <summary>CSS "ease" (transition default).</summary>
        public static readonly CubicBezier CssEase = new CubicBezier(0.25f, 0.1f, 0.25f, 1f);
        /// <summary>Web Animations default.</summary>
        public static readonly CubicBezier Linear = new CubicBezier(0f, 0f, 1f, 1f);
    }
}
