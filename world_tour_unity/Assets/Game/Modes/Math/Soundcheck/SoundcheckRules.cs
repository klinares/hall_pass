using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Game.Modes
{
    /// <summary>soundcheck.js's copy, word for word.</summary>
    public static class SoundcheckLines
    {
        private static readonly string Star = ((char)0x2736).ToString();

        /// <summary>soundcheck.js:11 the bonus for beating the meter.</summary>
        public const int Bonus = 300;
        /// <summary>soundcheck.js:26.</summary>
        public static readonly string DeskTag = "soundcheck " + Star + " one, two";
        /// <summary>soundcheck.js:28 after the seconds: " sec for +300 bonus fans".</summary>
        public static readonly string SecFor = " sec for +" + Bonus + " bonus fans";
        /// <summary>soundcheck.js:43 the meter ran out (it costs nothing).</summary>
        public const string Out = "no bonus this time. no rush.";
        /// <summary>soundcheck.js:66.</summary>
        public static readonly string Beat = "beat the meter. <b>+" + Bonus + "</b> bonus fans.";
        /// <summary>soundcheck.js:75 the dog's first line.</summary>
        public const string Intro = "quick as you can. the bar only changes bonus fans. you can't lose fans.";
        /// <summary>soundcheck.js:31 the choice tilts (--r, degrees, CSS clockwise).</summary>
        public static readonly float[] Tilts = { -1f, 1.2f, -.6f, .8f };

        /// <summary>soundcheck.js:22 a choice's label: show() of its value, with [[n/d]] markup.</summary>
        public static string Label(Problem p, Choice c) { return Answers.Show(new Answer(p.Answer.Type, c.Value)); }
    }

    /// <summary>
    /// soundcheck.js:36-45 the meter, the only timer in the game. It only decides bonus fans: right before it runs out is
    /// +300; running out costs nothing (no miss, no fans, the streak untouched) and says so. The view calls
    /// <see cref="Tick"/> with the seconds since the song was drawn; the web ticks every 100 ms.
    /// </summary>
    public sealed class SoundcheckMeter
    {
        public readonly double Seconds;
        public double Left { get; private set; }
        /// <summary>False once the meter has run out.</summary>
        public bool Beatable { get; private set; } = true;
        public bool Running { get; private set; } = true;

        public SoundcheckMeter(double seconds) { Seconds = seconds; Left = seconds; }

        /// <summary>The meter's fill, 1 to 0.</summary>
        public double Fill { get { return Seconds > 0 ? Left / Seconds : 0; } }
        /// <summary>The number beside it: whole seconds left, rounded up.</summary>
        public int Shown { get { return (int)Math.Ceiling(Left); } }

        /// <summary>One tick. True when it ran out on this tick.</summary>
        public bool Tick(double elapsed)
        {
            if (!Running) return false;
            Left = Math.Max(0, Seconds - elapsed);
            if (Left <= 0) { Beatable = false; Running = false; return true; }
            return false;
        }

        public void Stop() { Running = false; }

        /// <summary>soundcheck.js:63 the bonus a right answer now earns.</summary>
        public int BonusNow { get { return Beatable && Left > 0 ? SoundcheckLines.Bonus : 0; } }
    }

    /// <summary>What a press on a choice did.</summary>
    public enum SoundcheckPick { Ignored, Right, Wrong, WrongShown }

    /// <summary>The choices' side of the song.</summary>
    public interface ISoundcheckView
    {
        void FreezeChoices();
        void Good(int i, bool pop);
        /// <summary>.gone (and .bad when she picked it): struck through, off, a wobble.</summary>
        void Gone(int i);
        /// <summary>snd.tick(): the bonus chime.</summary>
        void Ticked();
        void ShowNext();
    }

    /// <summary>
    /// The show's ctx with the win carrying soundcheck's bonus fans (parts.js L.right(extra)): the shared ladder's Right()
    /// wins through <see cref="IMathSong.Win"/>, which has no extra, so the round sets <see cref="Extra"/> first.
    /// </summary>
    public sealed class BonusSong : IMathSong
    {
        private readonly IMathSong _inner;
        private readonly Action<int> _win;
        public int Extra;

        public BonusSong(IMathSong inner, Action<int> winWithExtra) { _inner = inner; _win = winWithExtra; }

        public void SetupHints(int max, Func<int, bool> apply) { _inner.SetupHints(max, apply); }
        public int Miss() { return _inner.Miss(); }
        public void Mistake(string tag) { _inner.Mistake(tag); }
        public void Win() { _win(Extra); }
        public void Reveal(string line) { _inner.Reveal(line); }
        public void Say(string line, string pose) { _inner.Say(line, pose); }
        public void LockHints() { _inner.LockHints(); }
    }

    /// <summary>
    /// The rules of soundcheck, ported from world_tour/modes/math/soundcheck.js: four choices; a wrong pick is crossed out
    /// and climbs the shared ladder; hint 2 crosses out one more wrong choice when two or more are left (picked at random,
    /// Math.random in the JS); the worked step marks the right one. A right pick before the meter runs out adds +300.
    /// </summary>
    public sealed class SoundcheckRound
    {
        /// <summary>soundcheck.js:67 ctx.next(1100).</summary>
        public const float NextAfterRight = 1.1f;

        private readonly Problem _p;
        private readonly BonusSong _song;
        private readonly ISoundcheckView _view;
        private readonly Func<double> _random;
        private readonly Action<float> _next;
        private readonly bool[] _off;

        public readonly SoundcheckMeter Meter;
        public readonly MathLadder Ladder;

        public SoundcheckRound(Problem p, BonusSong song, ISoundcheckView view, Func<double> random, Action<float> next)
        {
            _p = p; _song = song; _view = view; _random = random; _next = next;
            _off = new bool[p.Choices.Count];
            Meter = new SoundcheckMeter(p.Params["seconds"].Number);
            Ladder = new MathLadder(song, p, Step);
        }

        public int RightIndex { get { return _p.Choices.FindIndex(c => c.Correct); } }
        public bool IsOff(int i) { return _off[i]; }

        /// <summary>soundcheck.js:48-55 the ladder's onStep.</summary>
        private void Step(int n)
        {
            if (n == 2)
            {
                List<int> live = Enumerable.Range(0, _off.Length).Where(i => !_p.Choices[i].Correct && !_off[i]).ToList();
                if (live.Count > 1)
                {
                    int b = live[(int)Math.Floor(_random() * live.Count)];      // util.js pick()
                    _off[b] = true;
                    _view.Gone(b);
                }
            }
            if (n >= 3)
            {
                for (int i = 0; i < _off.Length; i++) _off[i] = true;
                _view.FreezeChoices();
                _view.Good(RightIndex, false);
            }
        }

        /// <summary>soundcheck.js:57-71 a press on choice i.</summary>
        public SoundcheckPick Pick(int i)
        {
            if (Ladder.Over || i < 0 || i >= _off.Length || _off[i]) return SoundcheckPick.Ignored;
            Choice c = _p.Choices[i];
            if (c.Correct)
            {
                Meter.Stop();
                for (int k = 0; k < _off.Length; k++) _off[k] = true;
                _view.FreezeChoices();
                _view.Good(i, true);
                int bonus = Meter.BonusNow;
                _song.Extra = bonus;
                Ladder.Right();
                if (bonus > 0) { _view.Ticked(); _song.Say(SoundcheckLines.Beat, "cheer"); }
                _next(NextAfterRight);
                return SoundcheckPick.Right;
            }
            _off[i] = true;
            _view.Gone(i);
            if (Ladder.Wrong(null, c.Why)) { Meter.Stop(); _view.ShowNext(); return SoundcheckPick.WrongShown; }
            return SoundcheckPick.Wrong;
        }
    }
}
