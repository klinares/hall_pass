using System;
using System.Collections.Generic;
using System.Linq;
using WorldTour.Core.Shows;

namespace WorldTour.Game.Modes
{
    /// <summary>One fan mail deal: the choices in button order and the fan's handle.</summary>
    public sealed class FanmailDeal
    {
        public readonly List<string> Options;
        public readonly string Handle;

        public FanmailDeal(List<string> options, string handle)
        {
            Options = options; Handle = handle;
        }
    }

    /// <summary>What a choice press did.</summary>
    public enum FanmailPick
    {
        /// <summary>Already picked, crossed out, or not a choice.</summary>
        Ignored,
        Right,
        Wrong
    }

    /// <summary>What one press of the hint button did.</summary>
    public sealed class FanmailHint
    {
        /// <summary>False: nothing left to cross out, so the hint is free (fanmail.js:185 returns false).</summary>
        public bool Counted;
        /// <summary>The choice crossed out, or -1.</summary>
        public int Crossed = -1;
        /// <summary>The dog's line ("think" pose), or null when nothing happened.</summary>
        public string Line;
    }

    /// <summary>
    /// The rules of fan mail, ported from world_tour/modes/spelling/fanmail.js and parts.js (optionsFrom, crossOut). Plain C#:
    /// the random source stands in for Math.random and is used in the same order as the JS (the pool shuffle, the options
    /// shuffle, the handle, then one value per crossed-out choice).
    /// </summary>
    public static class FanmailRules
    {
        /// <summary>parts.js:97 optionsFrom's n.</summary>
        public const int Choices = 4;
        /// <summary>fanmail.js:174.</summary>
        public const float NextAfterWin = 1.3f;
        /// <summary>fanmail.js:183 setupHints(3).</summary>
        public const int HintMax = 3;

        /// <summary>
        /// parts.js:97 <c>shuffle([correct, ...shuffle(pool.filter(x => x !== correct)).slice(0, n - 1)])</c>, then
        /// fanmail.js:165 <c>pick(HANDLES)</c>. The pool is the whole week's words (fanmail.js:162 ctx.week.words).
        /// </summary>
        public static FanmailDeal Deal(string word, IEnumerable<string> weekWords, Func<double> random)
        {
            if (string.IsNullOrEmpty(word)) throw new ArgumentException("fan mail needs a word", nameof(word));
            if (random == null) throw new ArgumentNullException(nameof(random));
            var others = ShowPicker.Shuffle((weekWords ?? Enumerable.Empty<string>()).Where(x => x != word), random).Take(Choices - 1);
            var opts = new List<string> { word };
            opts.AddRange(others);
            List<string> options = ShowPicker.Shuffle(opts, random);
            string handle = Pick(SpellingLines.Handles, random);
            return new FanmailDeal(options, handle);
        }

        /// <summary>util.js:5 <c>pick(a) = a[Math.floor(Math.random() * a.length)]</c>.</summary>
        public static T Pick<T>(IReadOnlyList<T> a, Func<double> random)
        {
            return a[(int)Math.Floor(random() * a.Count)];
        }

        /// <summary>fanmail.js:166 <c>v.h.toLowerCase()</c>.</summary>
        public static string ClueLower(string h)
        {
            return (h ?? "").ToLowerInvariant();
        }
    }

    /// <summary>
    /// One fan mail song: one pick, like the original (fanmail.js:171-182), and the hint ladder (fanmail.js:183-188):
    /// cross out a wrong choice (twice), then the first letter and the length.
    /// </summary>
    public sealed class FanmailRound
    {
        private readonly bool[] _gone;

        public readonly string Word;
        public readonly IReadOnlyList<string> Options;

        public FanmailRound(string word, IReadOnlyList<string> options)
        {
            Word = word ?? throw new ArgumentNullException(nameof(word));
            Options = options ?? throw new ArgumentNullException(nameof(options));
            _gone = new bool[options.Count];
            RightIndex = -1;
            for (int i = 0; i < options.Count; i++) if (options[i] == word) { RightIndex = i; break; }
        }

        /// <summary>The button with the word on it.</summary>
        public int RightIndex { get; private set; }
        /// <summary>The choice she picked, or -1.</summary>
        public int Picked { get; private set; } = -1;
        public bool Done { get { return Picked >= 0; } }

        /// <summary>parts.js:93 a crossed-out choice (.gone, disabled).</summary>
        public bool IsGone(int i) { return i >= 0 && i < _gone.Length && _gone[i]; }

        /// <summary>fanmail.js:171-182: the first pick decides the song; later presses and crossed-out choices do nothing.</summary>
        public FanmailPick Pick(int i)
        {
            if (Done || i < 0 || i >= Options.Count || _gone[i]) return FanmailPick.Ignored;
            Picked = i;
            return Options[i] == Word ? FanmailPick.Right : FanmailPick.Wrong;
        }

        /// <summary>
        /// parts.js:90-95 crossOut: one wrong, unpicked, live choice at random (in button order), or -1 when none is
        /// left. After the pick every button is disabled, so nothing is live.
        /// </summary>
        public int CrossOut(Func<double> random)
        {
            if (Done) return -1;
            var live = new List<int>();
            for (int i = 0; i < Options.Count; i++) if (Options[i] != Word && !_gone[i]) live.Add(i);
            if (live.Count == 0) return -1;
            int k = FanmailRules.Pick(live, random);
            _gone[k] = true;
            return k;
        }

        /// <summary>fanmail.js:183-188, hint number <paramref name="level"/> (1-based).</summary>
        public FanmailHint Hint(int level, Func<double> random)
        {
            var h = new FanmailHint();
            if (level <= 2)
            {
                h.Crossed = CrossOut(random);
                if (h.Crossed < 0) return h;
                h.Counted = true;
                h.Line = level == 1 ? SpellingLines.CrossedOne : SpellingLines.OneMoreGone;
                return h;
            }
            h.Counted = true;
            h.Line = SpellingLines.StartsWith(Word);
            return h;
        }
    }
}
