using System;
using UnityEngine;
using WorldTour.Game.Show;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// The show side of a math song: the song context (show.js ctx) behind <see cref="IMathSong"/>. Lines are turned into
    /// TMP with stacked fractions here, so the rules keep the generators' markup. A wrong try also wobbles the desk
    /// (show.js:163 [data-wobble]); the show plays the thud.
    /// </summary>
    public sealed class MathSongHost : IMathSong
    {
        private readonly SongContext _c;
        private readonly Func<RectTransform> _wobble;
        private readonly string _mode;

        public MathSongHost(SongContext c, string mode, Func<RectTransform> wobble)
        {
            _c = c; _mode = mode; _wobble = wobble;
        }

        public void SetupHints(int max, Func<int, bool> apply)
        {
            _c.SetupHints(max, n =>
            {
                bool counted = apply(n);
                Debug.Log("WT_HINT mode=" + _mode + " n=" + n + " counted=" + (counted ? "true" : "false"));
                return counted;
            });
        }

        public int Miss()
        {
            int t = _c.Miss();
            RectTransform w = _wobble != null ? _wobble() : null;
            if (w != null) WorldTour.Game.Motion.Feedback.Wobble(w);
            return t;
        }

        public void Mistake(string tag) { _c.Mistake(tag); }
        public void Win() { _c.Win(); }

        public void Reveal(string line)
        {
            // show.js reveal(msg): the worked solution is the dog's line (the engine's own reveal cue has no line for math)
            if (_c.Reveal()) { _c.LockHints(); _c.Say(MathText.ToTmp(line, MathText.BubbleScale), "oops"); }   // show.js reveal: lockHints()
        }

        public void Say(string line, string pose) { _c.Say(MathText.ToTmp(line, MathText.BubbleScale), pose); }
        public void LockHints() { _c.LockHints(); }
    }
}
