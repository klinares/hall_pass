namespace WorldTour.Game.Ui
{
    /// <summary>
    /// Ignores a second activation that arrives while the first one's press animation is still playing. The extra
    /// click is dropped, never queued, so a double-click can't answer twice (ShowEngine throws on a second Win or Miss).
    /// Time comes in from the caller, so this is plain C# and testable.
    /// </summary>
    public sealed class ClickGuard
    {
        /// <summary>Press (60 ms) plus spring back (240 ms): the length of one button animation.</summary>
        public const float DefaultWindow = 0.3f;

        public readonly float Window;
        /// <summary>Float slack, so a click exactly one window later always counts (1.3f - 1f is just under 0.3f).</summary>
        const float Slack = 1e-4f;
        float _last;
        bool _has;

        public ClickGuard(float window = DefaultWindow) { Window = window; }

        /// <summary>True if this activation counts; false if it came within <see cref="Window"/> seconds of the last one that counted.</summary>
        public bool TryAccept(float now)
        {
            if (_has && now >= _last && now - _last < Window - Slack) return false;
            _has = true;
            _last = now;
            return true;
        }

        /// <summary>True while a counted activation is still inside its window.</summary>
        public bool IsBusy(float now) { return _has && now >= _last && now - _last < Window - Slack; }

        public void Reset() { _has = false; }
    }

    /// <summary>The slight, stable tilt every sticker gets (paper.css: nothing grid-perfect). Plain C#.</summary>
    public static class StickerMath
    {
        /// <summary>FNV-1a of a name, for a seed that stays the same across runs and machines.</summary>
        public static int Seed(string name)
        {
            unchecked
            {
                uint h = 2166136261;
                if (name != null) foreach (char c in name) { h ^= c; h *= 16777619; }
                return (int)h;
            }
        }

        /// <summary>
        /// A tilt in degrees for a seed: magnitude between <see cref="Tokens.TiltMin"/> and <see cref="Tokens.TiltMax"/>
        /// (the .tilt-1..4 range), either direction. The same seed always gives the same tilt.
        /// </summary>
        public static float Tilt(int seed)
        {
            unchecked
            {
                // xorshift32 scramble so nearby seeds don't give nearby tilts
                uint x = (uint)seed;
                if (x == 0) x = 0x9E3779B9;
                x ^= x << 13; x ^= x >> 17; x ^= x << 5;
                float u = (x & 0xFFFF) / 65535f;
                bool left = (x & 0x10000) != 0;
                float mag = Tokens.TiltMin + (Tokens.TiltMax - Tokens.TiltMin) * u;
                return left ? -mag : mag;
            }
        }
    }
}
