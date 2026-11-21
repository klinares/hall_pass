namespace WorldTour.Game.Save
{
    /// <summary>
    /// progress.js:107-117, the play-time timer's timing, with no Unity in it so it is tested with a fake clock: every 15 s
    /// one tick, credited only while the page is visible and there has been a pointer or key press in the last minute
    /// (Date.now() - lastInput &gt; 60000 skips the tick). As in the JS, the page's load counts as the first input. Times are
    /// in seconds on any steady clock. Ticks that fell due while the game wasn't running (a hidden tab stops the player's
    /// frames) are each checked against the last input, so a long absence adds at most the minute after the last press.
    /// </summary>
    public sealed class PlayClock
    {
        public const float Interval = 15f;
        public const float IdleLimit = 60f;

        float _next, _lastInput;
        bool _started;

        public bool Started { get { return _started; } }

        /// <summary>The page load: the first tick falls due 15 s later, and this counts as input.</summary>
        public void Start(float now)
        {
            _started = true;
            _next = now + Interval;
            _lastInput = now;
        }

        /// <summary>A pointer press or key press (progress.js listens for pointerdown and keydown).</summary>
        public void Input(float now)
        {
            if (now > _lastInput) _lastInput = now;
        }

        /// <summary>The ticks to credit up to <paramref name="now"/>. Each due tick counts when visible and within a minute of input.</summary>
        public int Advance(float now, bool visible)
        {
            if (!_started) return 0;
            int n = 0;
            while (now >= _next)
            {
                if (visible && _next - _lastInput <= IdleLimit) n++;
                _next += Interval;
            }
            return n;
        }
    }
}
