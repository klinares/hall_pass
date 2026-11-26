using System;

namespace WorldTour.Game.Audio
{
    /// <summary>
    /// Phase 10A: decides when the guitar may play and which of its phrases. The guitar plays at most once every <see cref="Gap"/> seconds (silence in between, so it never loops on),
    /// and never the same phrase twice in a row. Plain C#, so the edit-mode tests can run thousands of requests.
    /// </summary>
    public sealed class GuitarPlanner
    {
        /// <summary>The least time, in seconds, between the start of one phrase and the start of the next.</summary>
        public const double Gap = 6.0;

        int _last = -1;
        double _lastAt = double.NegativeInfinity;

        /// <summary>The phrase to play now (0 to <see cref="Synth.PhraseCount"/> minus 1), or -1 when the last one was too recent.</summary>
        public int Request(double now, Random rng)
        {
            if (now - _lastAt < Gap) return -1;
            int pick = _last < 0 ? rng.Next(Synth.PhraseCount) : rng.Next(Synth.PhraseCount - 1);
            if (_last >= 0 && pick >= _last) pick++;          // skips the last phrase: every other one is equally likely
            _last = pick;
            _lastAt = now;
            return pick;
        }

        /// <summary>Forgets everything (a new session).</summary>
        public void Reset() { _last = -1; _lastAt = double.NegativeInfinity; }
    }
}
