using System;
using System.Collections.Generic;
using System.Linq;
using WorldTour.Core.Shows;

namespace WorldTour.Game.Modes
{
    /// <summary>One remix deal: the letters on the rack, which of them are decoys, and each tile's tilt.</summary>
    public sealed class RemixDeal
    {
        /// <summary>The rack, in order (remix.js <c>mix</c>).</summary>
        public readonly List<char> Mix;
        /// <summary>The decoy letters added (level 2: one, level 3: two).</summary>
        public readonly List<char> Decoys;
        /// <summary>Each tile's tilt in degrees (remix.js <c>tilt(4)</c>).</summary>
        public readonly List<float> Tilts;

        public RemixDeal(List<char> mix, List<char> decoys, List<float> tilts)
        {
            Mix = mix; Decoys = decoys; Tilts = tilts;
        }
    }

    /// <summary>
    /// The rules of remix, ported from world_tour/modes/spelling/remix.js. Plain C#: the random source stands in for
    /// Math.random and is used in the same order as the JS (decoy shuffle, rack shuffles, then one tilt per tile),
    /// so a seeded source deals the same rack as the web game.
    /// </summary>
    public static class RemixRules
    {
        /// <summary>remix.js:10 DECOYS.</summary>
        public const string DecoyLetters = "aeioulnrstcdmp";

        /// <summary>remix.js:111 <c>Math.max(2, Math.ceil(word.length / 3))</c>.</summary>
        public static int HintMax(string word)
        {
            return Math.Max(2, (word.Length + 2) / 3);
        }

        /// <summary>remix.js:19 level 2 adds one decoy letter, level 3 adds two.</summary>
        public static int DecoyCount(int level)
        {
            return Math.Max(0, level - 1);
        }

        /// <summary>
        /// remix.js:19-22 and :40: decoys = shuffle(DECOYS not in the word).slice(0, extra); then reshuffle the word plus
        /// decoys until the rack doesn't already spell the word; then tilt(4) per tile. One difference: a rack that can
        /// never differ from the word (one letter, or one letter repeated, with no decoys) is accepted instead of
        /// looping forever as the JS would.
        /// </summary>
        public static RemixDeal Deal(string word, int level, Func<double> random)
        {
            if (string.IsNullOrEmpty(word)) throw new ArgumentException("remix needs a word", nameof(word));
            if (random == null) throw new ArgumentNullException(nameof(random));
            int extra = DecoyCount(level);
            var pool = DecoyLetters.Where(c => word.IndexOf(c) < 0).ToList();
            var decoys = ShowPicker.Shuffle(pool, random).Take(extra).ToList();
            var letters = word.ToList();
            letters.AddRange(decoys);
            bool canDiffer = decoys.Count > 0 || word.Distinct().Count() > 1;
            List<char> mix;
            do { mix = ShowPicker.Shuffle(letters, random); }
            while (canDiffer && new string(mix.ToArray()) == word);
            var tilts = new List<float>();
            for (int i = 0; i < mix.Count; i++) tilts.Add(Tilt(4, random));
            return new RemixDeal(mix, decoys, tilts);
        }

        /// <summary>util.js:7 <c>tilt(max) = +((Math.random() * 2 - 1) * max).toFixed(1)</c>.</summary>
        public static float Tilt(double max, Func<double> random)
        {
            double v = (random() * 2 - 1) * max;
            return (float)(Math.Round(v * 10, MidpointRounding.AwayFromZero) / 10);
        }
    }

    /// <summary>What a tile pick did.</summary>
    public enum PickResult
    {
        /// <summary>Busy, already used, or the word is full (remix.js:67).</summary>
        Ignored,
        /// <summary>Placed in the next slot.</summary>
        Placed,
        /// <summary>Placed, and that filled the word: check it now (remix.js:75).</summary>
        Complete
    }

    /// <summary>What the hint button did (remix.js:112-122).</summary>
    public sealed class RemixHint
    {
        /// <summary>False: nothing new was shown, so the hint is free and not counted (the JS apply returns false).</summary>
        public bool Counted;
        /// <summary>Wrong letters were cleared first.</summary>
        public bool HadWrong;
        /// <summary>The tile now glowing, or -1.</summary>
        public int Glow = -1;
        /// <summary>The dog's line ("think" pose), or null when the hint did nothing at all (busy).</summary>
        public string Line;
    }

    /// <summary>
    /// The state of one remix song: which tiles are in the slots, in order, and which tile glows. Every paint() in the
    /// JS clears the glow, so picking, undo, clear and the reset after a miss all clear it here.
    /// </summary>
    public sealed class RemixBoard
    {
        private readonly List<int> _picked = new List<int>();

        public readonly string Word;
        public readonly IReadOnlyList<char> Mix;

        /// <summary>True while the mode waits (after a miss, or the song is over): picks, undo, clear and hints do nothing.</summary>
        public bool Busy;
        /// <summary>The glowing tile's index, or -1.</summary>
        public int Glow { get; private set; } = -1;

        public RemixBoard(string word, IReadOnlyList<char> mix)
        {
            Word = word;
            Mix = mix;
        }

        /// <summary>Rack indexes in the slots, in order (remix.js <c>picked</c>).</summary>
        public IReadOnlyList<int> Picked { get { return _picked; } }

        public bool IsUsed(int tile) { return _picked.Contains(tile); }

        /// <summary>The letters in the slots so far.</summary>
        public string Built { get { return new string(_picked.Select(i => Mix[i]).ToArray()); } }

        public bool IsFull { get { return _picked.Count >= Word.Length; } }

        /// <summary>remix.js:80 <c>built === word</c>.</summary>
        public bool IsRight { get { return Built == Word; } }

        /// <summary>remix.js:66-76: a tile tap.</summary>
        public PickResult Pick(int tile)
        {
            if (Busy || tile < 0 || tile >= Mix.Count || _picked.Contains(tile) || IsFull) return PickResult.Ignored;
            _picked.Add(tile);
            Glow = -1;
            return IsFull ? PickResult.Complete : PickResult.Placed;
        }

        /// <summary>remix.js:108 undo. True when something changed.</summary>
        public bool Undo()
        {
            if (Busy || _picked.Count == 0) return false;
            _picked.RemoveAt(_picked.Count - 1);
            Glow = -1;
            return true;
        }

        /// <summary>remix.js:109 clear. True when something changed.</summary>
        public bool Clear()
        {
            if (Busy || _picked.Count == 0) return false;
            _picked.Clear();
            Glow = -1;
            return true;
        }

        /// <summary>
        /// Counts resets of the slots. A callback scheduled before a reset (a tile still landing, the wait after an
        /// earlier wrong try) carries the generation it was made in and does nothing once it has moved on.
        /// </summary>
        public int Generation { get; private set; }

        /// <summary>remix.js:99 the slots empty (after a wrong try, or for the reveal). Starts a new generation.</summary>
        public void ResetAfterMiss()
        {
            _picked.Clear();
            Glow = -1;
            Generation++;
        }

        /// <summary>
        /// remix.js:96 a wrong try that isn't the last: the board waits. Returns the token <see cref="FinishMissReset(int)"/>
        /// needs, so only this wait's own reset can end it.
        /// </summary>
        public int StartMiss()
        {
            Busy = true;
            return Generation;
        }

        /// <summary>
        /// The end of the wait after a wrong try (remix.js:99-100 picked.length = 0; busy = false): the slots empty and the
        /// board takes picks again, in one call, so the UI never repaints the tiles while still busy. A token from an
        /// earlier wait (the board has been reset since) is ignored and returns false.
        /// </summary>
        public bool FinishMissReset(int token)
        {
            if (token != Generation) return false;
            ResetAfterMiss();
            Busy = false;
            return true;
        }

        /// <summary>Ends the current wait, whatever its token.</summary>
        public void FinishMissReset()
        {
            FinishMissReset(Generation);
        }

        /// <summary>Whether rack tile <paramref name="tile"/> can be clicked or dragged now (not in a slot, board not busy).</summary>
        public bool TileInteractable(int tile) { return !Busy && !IsUsed(tile); }

        /// <summary>
        /// remix.js:112-122 the hint: if busy, nothing. If a tile already glows, a free reminder. Otherwise keep the
        /// right letters at the start, clear the rest, and glow the first unused tile with the next letter.
        /// </summary>
        /// <summary>The word's syllables (for the ladder's "sound it out" line), or null.</summary>
        public string Syllables;

        /// <summary>
        /// Letters a hint or a ladder line has shown so far this song (QUESTIONS 11; the task Q tester's D1): the glowing
        /// tile counts as showing the letters up to it, the first ladder line the first letter, the second its chunks. Kept
        /// across the resets after a miss, since she has already seen them.
        /// </summary>
        private readonly HashSet<int> _shown = new HashSet<int>();

        public IReadOnlyCollection<int> Shown { get { return _shown; } }

        private int Unknown(IEnumerable<int> more)
        {
            var known = new HashSet<int>(_shown);
            foreach (int k in more) known.Add(k);
            return Word.Length - known.Count;
        }

        /// <summary>
        /// May the hint light the letter at <paramref name="pos"/>? Only within <see cref="SpellingHints.MaxShown"/>, and
        /// only while at least <see cref="SpellingHints.MinHidden"/> letters stay unknown counting what the ladder lines have
        /// already shown ("in - ___ - cept" plus the glows used to spell intercept).
        /// </summary>
        public bool CanGlow(int pos)
        {
            if (!SpellingHints.CanReveal(pos, Word.Length)) return false;
            return Unknown(Enumerable.Range(0, pos + 1)) >= SpellingHints.MinHidden(Word.Length);
        }

        /// <summary>
        /// show.js ladder() held to the same floor, counting the glows already seen: the first line drops "and starts with x"
        /// if naming the first letter would leave fewer than the floor unknown; the second masks the chunk letters that
        /// would, keeping the leftmost ones it may show. With nothing shown yet it is the RemixLines.Ladder text.
        /// </summary>
        public string LadderLine(int tries, Func<double> random)
        {
            int floor = SpellingHints.MinHidden(Word.Length);
            var mask = new HashSet<int>();
            if (tries == 1)
            {
                if (!_shown.Contains(0) && Unknown(new[] { 0 }) < floor) mask.Add(0);
                else _shown.Add(0);
            }
            else
            {
                var chunk = MaskedChunks.Render(Word, Syllables, null).Shown.Where(k => !_shown.Contains(k)).OrderBy(k => k).ToList();
                int allowed = Math.Max(0, Unknown(new int[0]) - floor);
                for (int i = 0; i < chunk.Count; i++)
                {
                    if (i < allowed) _shown.Add(chunk[i]);
                    else mask.Add(chunk[i]);
                }
            }
            return MaskedChunks.Ladder(Word, Syllables, tries, random, mask);
        }

        public RemixHint Hint()
        {
            var r = new RemixHint();
            if (Busy) return r;
            if (Glow >= 0)
            {
                r.Line = RemixLines.HintStillGlowing;
                r.Glow = Glow;
                return r;
            }
            int ok = 0;
            while (ok < _picked.Count && Mix[_picked[ok]] == Word[ok]) ok++;
            r.HadWrong = ok < _picked.Count;
            _picked.RemoveRange(ok, _picked.Count - ok);
            Glow = -1;
            // QUESTIONS 11 (an intended difference from remix.js): the last hidden letters are always hers to find, so a
            // hint never lights a letter past SpellingHints.MaxShown. Clearing wrong letters still counts as a hint.
            if (!CanGlow(ok))
            {
                r.Glow = -1;
                r.Counted = r.HadWrong;
                r.Line = r.HadWrong ? RemixLines.HintClearedYoursToFind : RemixLines.HintYoursToFind;
                return r;
            }
            if (ok < Word.Length)
                for (int i = 0; i < Mix.Count; i++)
                    if (!_picked.Contains(i) && Mix[i] == Word[ok]) { Glow = i; break; }
            if (Glow >= 0) for (int k = 0; k <= ok; k++) _shown.Add(k);
            r.Glow = Glow;
            r.Counted = true;
            r.Line = r.HadWrong ? RemixLines.HintCleared : RemixLines.HintGlow;
            return r;
        }

        /// <summary>
        /// Keyboard typing (the brief: typed answers work from the keyboard alone; the web game has no keys here). A letter
        /// picks the glowing tile if it is that letter, else the first unused tile on the rack with that letter; -1 when none.
        /// </summary>
        public int TileForKey(char c)
        {
            c = char.ToLowerInvariant(c);
            if (Glow >= 0 && !_picked.Contains(Glow) && Mix[Glow] == c) return Glow;
            for (int i = 0; i < Mix.Count; i++)
                if (!_picked.Contains(i) && Mix[i] == c) return i;
            return -1;
        }
    }

    /// <summary>
    /// remix's lines and the spelling ladder, word for word from the web game. The source stays ASCII: the curly
    /// apostrophe and quotes are built from their character codes.
    /// </summary>
    public static class RemixLines
    {
        private static readonly string Rsq = ((char)0x2019).ToString();
        private static readonly string Ldq = ((char)0x201C).ToString();
        private static readonly string Rdq = ((char)0x201D).ToString();

        /// <summary>copy.js:5 NAME.</summary>
        public const string Name = "natalia";

        /// <summary>remix.js:15 sub.</summary>
        public const string Sub = "tap the letter stickers in order";
        /// <summary>remix.js:28 the clue card's label.</summary>
        public const string ClueLabel = "clue";
        /// <summary>remix.js:35 desk tag.</summary>
        public const string DeskTag = "the mix";
        public const string Undo = "undo";
        public const string Clear = "clear";

        /// <summary>remix.js:114.</summary>
        public static readonly string HintStillGlowing = "that glowing tile is still your next letter. tap it.";
        /// <summary>remix.js:121, wrong letters cleared.</summary>
        public static readonly string HintCleared = "i cleared the wrong letters. tap the glowing tile next.";
        /// <summary>remix.js:121.</summary>
        public static readonly string HintGlow = "tap the glowing tile. that" + Rsq + "s your next letter.";
        /// <summary>Unity only (QUESTIONS 11): the hint won't light any of the last letters.</summary>
        public static readonly string HintYoursToFind = "the last letters are yours to find. sound it out.";
        /// <summary>Unity only (QUESTIONS 11): wrong letters cleared, and the rest is hers.</summary>
        public static readonly string HintClearedYoursToFind = "i cleared the wrong letters. the last ones are yours to find.";

        /// <summary>remix.js:42 the decoy note, or null with no decoys.</summary>
        public static string DecoyNote(int decoys)
        {
            if (decoys <= 0) return null;
            return (decoys == 1 ? "one letter doesn" + Rsq + "t belong" : "two letters don" + Rsq + "t belong")
                + ". leave " + (decoys == 1 ? "it" : "them") + " on the rack.";
        }

        /// <summary>show.js:111 the hint button: "hint (n)" while hints are left, else "no hints left".</summary>
        public static string HintButton(int left)
        {
            return left > 0 ? "hint <size=70%>(" + left + ")</size>" : "no hints left";
        }

        /// <summary>
        /// show.js:166-171 <c>ladder()</c>, said after a wrong try that isn't the last: try 1 gives the length and first
        /// letter, try 2 the first and last syllable (show.js chunks). A random value is drawn only on try 2, to decide
        /// whether to add her name, as in the JS.
        /// </summary>
        public static string Ladder(string w, string s, int tries, Func<double> random)
        {
            if (tries == 1)
                return "not quite. it" + Rsq + "s <b>" + w.Length + " letters</b> and starts with <b>" + Ldq + w[0] + Rdq + "</b>.";
            bool name = tries == 2 && random() < .5;
            return "keep going" + (name ? ", " + Name : "") + ". sound it out: <b>" + ShowPicker.Chunks(w, s) + "</b>";
        }

        /// <summary>show.js:178 the reveal line when the mode passes none (remix doesn't).</summary>
        public static string Reveal(string w)
        {
            return "it" + Rsq + "s <b>" + w + "</b>. look closely, it" + Rsq + "ll come back around.";
        }
    }
}
