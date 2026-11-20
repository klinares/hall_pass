using System;
using System.Linq;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Game.Modes
{
    /// <summary>What a math song needs from the show (the web's ctx), so the rules run without Unity.</summary>
    public interface IMathSong
    {
        /// <summary>ctx.setupHints(max, apply): apply returns false when there was nothing new (a free hint).</summary>
        void SetupHints(int max, Func<int, bool> apply);
        /// <summary>ctx.miss(): returns the wrong tries so far.</summary>
        int Miss();
        /// <summary>ctx.mistake(tag).</summary>
        void Mistake(string tag);
        /// <summary>ctx.win(extra).</summary>
        void Win();
        /// <summary>ctx.reveal(line): the song is lost; the line is the worked solution.</summary>
        void Reveal(string line);
        /// <summary>ctx.say(line, pose). Lines carry the generators' [[n/d]] and &lt;b&gt; markup.</summary>
        void Say(string line, string pose);
        /// <summary>ctx.lockHints().</summary>
        void LockHints();
    }

    /// <summary>
    /// parts.js:153-192 <c>ladder(ctx, prob, { onStep, revealExtra })</c>: the hint ladder the bulb and wrong answers share.
    /// The bulb gives steps 1 (nudge) and 2 (partial step); the worked solution (step 3) only comes after the third miss,
    /// together with the visual model (onStep(3)), so a hint never hands over the answer. A wrong answer that matches a
    /// known mistake gets that mistake's line first and is counted on the skill (ctx.mistake). Hints are counted by the
    /// engine, which lowers the song's reward and never touches the streak.
    /// </summary>
    public sealed class MathLadder
    {
        private readonly IMathSong _ctx;
        private readonly Problem _prob;
        private readonly Action<int> _onStep;
        private readonly string _revealExtra;

        public int Shown { get; private set; }
        public bool Over { get; private set; }

        public MathLadder(IMathSong ctx, Problem prob, Action<int> onStep = null, string revealExtra = "")
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _prob = prob ?? throw new ArgumentNullException(nameof(prob));
            _onStep = onStep;
            _revealExtra = revealExtra ?? "";
            _ctx.SetupHints(2, n =>
            {
                if (Over || Shown >= 2) return false;
                Shown++;
                _ctx.Say(_prob.Steps[Shown - 1], "think");
                Step(Shown);
                return true;
            });
        }

        private void Step(int n) { if (_onStep != null) _onStep(n); }

        /// <summary>
        /// A wrong answer: typed <paramref name="text"/> (matched against the traps) or a mistake tag the mode already knows
        /// (<paramref name="why"/>). True once it is the third miss and the worked solution has been shown.
        /// </summary>
        public bool Wrong(string text, string why = null)
        {
            int tries = _ctx.Miss();
            string trap = why;
            if (trap == null && text != null)
            {
                foreach (Trap t in _prob.Traps)
                    if (Answers.Matches(text, t.Value, _prob.Answer.Type)) { trap = t.Why; break; }
            }
            if (trap != null) _ctx.Mistake(trap);
            if (tries >= 3)
            {
                Over = true;
                Shown = 3;
                Step(3);
                _ctx.Reveal(MathLines.ComeBackAfter(_prob.Steps[2] + (_revealExtra.Length > 0 ? " " + _revealExtra : "")));
                return true;
            }
            Shown = Math.Min(2, Math.Max(Shown, tries));
            string lead;
            if (trap != null && MathLines.Trap.TryGetValue(trap, out string line)) lead = line + " ";
            else lead = tries == 1 ? MathLines.NotQuite : MathLines.KeepGoing;
            _ctx.Say(lead + _prob.Steps[Shown - 1], "oops");
            Step(Shown);
            return false;
        }

        /// <summary>L.right(extra): the song is won.</summary>
        public void Right()
        {
            Over = true;
            _ctx.Win();
        }

        /// <summary>L.end(): no more hints (the beat is playing).</summary>
        public void End()
        {
            Over = true;
            _ctx.LockHints();
        }
    }
}
