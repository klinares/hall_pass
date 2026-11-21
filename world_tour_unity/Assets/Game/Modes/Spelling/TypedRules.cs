using System;
using System.Collections.Generic;
using System.Linq;
using WorldTour.Core.Maths;
using WorldTour.Core.Shows;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// Copy for the typed spelling modes and fan mail, word for word from world_tour/modes/spelling (parts.js, slots.js,
    /// vowels.js, fanmail.js). The source stays ASCII: curly quotes, the ellipsis and the arrow are built from their codes.
    /// The miss ladder and the default reveal line are show.js's, in <see cref="RemixLines"/>.
    /// </summary>
    public static class SpellingLines
    {
        private static readonly string Rsq = ((char)0x2019).ToString();
        private static readonly string Ldq = ((char)0x201C).ToString();
        private static readonly string Rdq = ((char)0x201D).ToString();

        /// <summary>parts.js:31 typedField's placeholder.</summary>
        public const string TypePlaceholder = "type the whole word";
        /// <summary>slots.js:115, vowels.js:140 the submit button.</summary>
        public const string CheckIt = "check it";
        /// <summary>slots.js:114 desk tag.</summary>
        public const string SlotsDeskTag = "soundcheck";
        /// <summary>vowels.js:139 desk tag.</summary>
        public const string VowelsDeskTag = "no vowels allowed";
        /// <summary>slots.js:114 slotRow's blank.</summary>
        public const char SlotsBlank = '?';
        /// <summary>vowels.js:139 slotRow's blank.</summary>
        public const char VowelsBlank = '_';

        /// <summary>parts.js:76 letterHints' first hint.</summary>
        public static string SoundsLike(string chunks) { return "sounds like: <b>" + chunks + "</b>"; }

        /// <summary>parts.js:80 a letter filled in by a hint.</summary>
        public static string LetterForYou(char c) { return "here" + Rsq + "s a letter for you: <b>" + c + "</b>"; }

        /// <summary>fanmail.js:153 HANDLES.</summary>
        public static readonly string[] Handles =
            { "@riffs_biggest_fan", "@front_row_forever", "@glitterbus_kid", "@encore_or_riot", "@setlist_collector", "@gymfloor_glitter" };

        /// <summary>fanmail.js:165.</summary>
        public const string SentYouAMessage = "sent you a message";
        /// <summary>fanmail.js:166 the message, before the clue.</summary>
        public static readonly string MessageBefore = "ok quick question" + (char)0x2026 + " which of your words means ";
        /// <summary>fanmail.js:166 the message, after the clue.</summary>
        public const string MessageAfter = "?";
        /// <summary>fanmail.js:165 the avatar (a heart; the team-drawn glyph sprite).</summary>
        public static readonly string Avatar = ((char)0x2665).ToString();
        /// <summary>fanmail.js:169 the button shown after a wrong pick.</summary>
        public static readonly string NextSong = "next song " + (char)0x2192;
        /// <summary>fanmail.js:186 hint 1.</summary>
        public const string CrossedOne = "crossed one out for you.";
        /// <summary>fanmail.js:186 hint 2.</summary>
        public static readonly string OneMoreGone = "one more gone. you" + Rsq + "ve got this.";

        /// <summary>fanmail.js:187 hint 3.</summary>
        public static string StartsWith(string w)
        {
            return "it starts with <b>" + Ldq + w[0] + Rdq + "</b> and has <b>" + w.Length + "</b> letters.";
        }

        /// <summary>fanmail.js:180 after a wrong pick.</summary>
        public static string ItWas(string w)
        {
            return "it was <b>" + w + "</b>. it" + Rsq + "ll come back around.";
        }

        /// <summary>parts.js:87 choiceButtons' tilts, in CSS degrees (clockwise), by position.</summary>
        public static readonly float[] ChoiceTilts = { -1f, 1.2f, -.6f, .8f };
    }

    /// <summary>
    /// Which letters missing letters and missing vowels hide (slots.js:110-111, vowels.js:136), and the row they show
    /// (parts.js:26-29 slotRow, slots.js:122-124 fillAll). Plain C#; the random source stands in for Math.random and is
    /// used in the same order, so a seeded source hides the same letters as the web game.
    /// </summary>
    public static class MissingLetters
    {
        /// <summary>vowels.js:136 'aeiou' (y is not a vowel here).</summary>
        public const string Vowels = "aeiou";

        public static bool IsVowel(char c) { return Vowels.IndexOf(c) >= 0; }

        /// <summary>
        /// slots.js:110 <c>Math.max(2, Math.round(w.length / 3)) + (ctx.level - 1)</c>: how many letters to hide before
        /// the cap of one letter shown. w.length / 3 never ends in exactly .5, so rounding has no tie to break.
        /// </summary>
        public static int SlotsCount(int length, int level)
        {
            return Math.Max(2, (int)Math.Floor(length / 3.0 + .5)) + (level - 1);
        }

        /// <summary>
        /// slots.js:111: shuffle the indexes, keep the first min(n, length - 1), sorted. At least one letter always shows.
        /// Uses length - 1 random numbers (util.js shuffle).
        /// </summary>
        public static List<int> SlotsHide(string w, int level, Func<double> random)
        {
            if (string.IsNullOrEmpty(w)) throw new ArgumentException("missing letters needs a word", nameof(w));
            if (random == null) throw new ArgumentNullException(nameof(random));
            int n = SlotsCount(w.Length, level);
            var hide = ShowPicker.Shuffle(Enumerable.Range(0, w.Length), random).Take(Math.Min(n, w.Length - 1)).ToList();
            hide.Sort();
            return hide;
        }

        /// <summary>vowels.js:136: every vowel's index, in order. A word with no a, e, i, o or u hides nothing.</summary>
        public static List<int> VowelsHide(string w)
        {
            var hide = new List<int>();
            for (int i = 0; i < (w ?? "").Length; i++) if (IsVowel(w[i])) hide.Add(i);
            return hide;
        }

        /// <summary>parts.js:28 slotRow: each letter, or the blank where it is hidden.</summary>
        public static string Row(string w, IEnumerable<int> hide, char blank)
        {
            var set = new HashSet<int>(hide ?? Enumerable.Empty<int>());
            var c = new char[w.Length];
            for (int i = 0; i < w.Length; i++) c[i] = set.Contains(i) ? blank : w[i];
            return new string(c);
        }
    }

    /// <summary>What one press of the hint button showed in missing letters or missing vowels.</summary>
    public sealed class LetterHint
    {
        /// <summary>The blank filled in (its index in the word), or -1 for the "sounds like" hint.</summary>
        public int Index = -1;
        /// <summary>The dog's line ("think" pose).</summary>
        public string Line;
    }

    /// <summary>
    /// parts.js:72-83 letterHints: hint 1 is the syllables ("sounds like"), then each press fills the first blank not yet
    /// given. Every press counts (the JS apply never returns false).
    /// One intended difference (QUESTIONS 11, the hint floor): a letter hint never brings the blanks below
    /// <see cref="SpellingHints.MinHidden"/> (a third of the word, at least two), so the last hidden letters are always
    /// hers to find. The JS keeps only the last blank back (<c>Math.max(1, hide.length)</c> hints); here the hint count is
    /// 1 + however many letters the floor lets it give, so the button's count stays honest and no new copy is needed.
    /// The "sounds like" text is <see cref="SpellingHints.Chunks"/> with the blanks that are hers masked
    /// (<see cref="Masked"/>), so the chunks plus the visible row never show more than the floor allows.
    /// </summary>
    public sealed class LetterHints
    {
        private readonly HashSet<int> _given = new HashSet<int>();
        private readonly HashSet<int> _told = new HashSet<int>();

        public readonly string Word;
        public readonly string Syllables;
        /// <summary>The hidden indexes, in order.</summary>
        public readonly IReadOnlyList<int> Hide;

        public LetterHints(string word, string syllables, IReadOnlyList<int> hide)
        {
            Word = word ?? throw new ArgumentNullException(nameof(word));
            Syllables = syllables;
            Hide = hide ?? throw new ArgumentNullException(nameof(hide));
        }

        /// <summary>The web count, parts.js:74 <c>Math.max(1, hide.length)</c> (for reference and tests).</summary>
        public int WebMax { get { return Math.Max(1, Hide.Count); } }

        /// <summary>Letters a hint may still give: blanks above the floor.</summary>
        public int Givable { get { return Math.Max(0, Hide.Count - SpellingHints.MinHidden(Word.Length)); } }

        /// <summary>Hints this song has: the syllables, then one per letter the floor allows.</summary>
        public int Max { get { return 1 + Givable; } }

        /// <summary>Blanks still hidden.</summary>
        public int Remaining { get { return Hide.Count - _given.Count; } }

        public bool IsGiven(int index) { return _given.Contains(index); }

        /// <summary>Has a hint or ladder line already named the letter at this index (polish 6.2B item 5)?</summary>
        public bool IsTold(int index) { return _told.Contains(index) || _given.Contains(index); }

        /// <summary>Blanks she has not been told, by a letter hint or a line that named the letter.</summary>
        public int Unknown { get { int n = 0; foreach (int k in Hide) if (!IsTold(k)) n++; return n; } }

        /// <summary>The "sounds like" chunks (hint 1 or the ladder's second line) name their letters; remember them.</summary>
        public void NoteChunks()
        {
            _told.UnionWith(MaskedChunks.Render(Word, Syllables, Masked).Shown);
        }

        /// <summary>
        /// A ladder line for this many wrong tries was shown: try 1 names the first letter ("starts with i") unless it is
        /// one of her masked blanks; try 2 shows the chunks. A later letter hint must pick another hidden letter.
        /// </summary>
        public void NoteLadder(int tries)
        {
            if (tries == 1) { if (Word.Length > 0 && !Masked.Contains(0)) _told.Add(0); }
            else NoteChunks();
        }

        /// <summary>
        /// The blanks a hint may ever show: the leftmost <see cref="Givable"/> (the same ones the letter hints fill, in
        /// the same order). The rest, the rightmost blanks, are hers: no hint before the reveal shows them.
        /// </summary>
        public ISet<int> Allowed
        {
            get
            {
                var a = new HashSet<int>();
                foreach (int k in Hide.Take(Givable)) a.Add(k);
                return a;
            }
        }

        /// <summary>The blanks every pre-reveal hint keeps hidden.</summary>
        public ISet<int> Masked
        {
            get
            {
                var m = new HashSet<int>(Hide);
                m.ExceptWith(Allowed);
                return m;
            }
        }

        /// <summary>
        /// The "sounds like" chunks with the masked blanks shown as "_" (QUESTIONS 11, decided by the lead in 3.1: the
        /// chunks plus the visible row must never show more than the floor allows). Also used for the ladder's second line.
        /// </summary>
        public string SoundsLikeText { get { return MaskedChunks.Render(Word, Syllables, Masked).Text; } }

        /// <summary>parts.js:74-82, hint number <paramref name="level"/> (1-based).</summary>
        public LetterHint Apply(int level)
        {
            int i = -1;
            if (level > 1 && Unknown - 1 >= SpellingHints.MinHidden(Word.Length))
                foreach (int k in Hide) if (!_given.Contains(k) && !_told.Contains(k)) { i = k; break; }
            if (i < 0) { NoteChunks(); return new LetterHint { Line = SpellingLines.SoundsLike(SoundsLikeText) }; }
            _given.Add(i);
            return new LetterHint { Index = i, Line = SpellingLines.LetterForYou(Word[i]) };
        }
    }

    /// <summary>A chunks text and the word positions whose letters it shows.</summary>
    public sealed class ChunksText
    {
        public string Text;
        public readonly HashSet<int> Shown = new HashSet<int>();
    }

    /// <summary>
    /// <see cref="SpellingHints.Chunks"/> drawn position by position, so some letters can be masked with "_". With no
    /// mask the text is exactly SpellingHints.Chunks (a test checks every live word and every syllable split, so the two
    /// can't drift). The branches mirror SpellingHints.Chunks in the same order.
    /// </summary>
    public static class MaskedChunks
    {
        public static ChunksText Render(string w, string s, ICollection<int> masked)
        {
            w = w ?? "";
            var r = new ChunksText();
            int n = w.Length;
            if (n == 0) { r.Text = ""; return r; }
            masked = masked ?? new HashSet<int>();
            var sb = new System.Text.StringBuilder();
            Action<int> letter = p =>
            {
                if (masked.Contains(p)) sb.Append('_');
                else { sb.Append(w[p]); r.Shown.Add(p); }
            };
            string[] parts = (string.IsNullOrEmpty(s) ? w : s).Split('-');
            int min = SpellingHints.MinHidden(n);
            int first = parts[0].Length, last = parts[parts.Length - 1].Length;
            if (n <= SpellingHints.ShortWord) FirstAndLength(sb, n, letter);
            else if (parts.Length >= 3 && n - first - last >= min)
            {
                for (int p = 0; p < first; p++) letter(p);
                for (int i = 1; i < parts.Length - 1; i++) sb.Append(" - ___");
                sb.Append(" - ");
                for (int p = n - last; p < n; p++) letter(p);
            }
            else if (parts.Length >= 2 && n - first >= min)
            {
                for (int p = 0; p < first; p++) letter(p);
                for (int i = 1; i < parts.Length; i++) sb.Append(" - ").Append('_', Math.Max(1, parts[i].Length));
            }
            else if (parts.Length < 2 && n > 4 && n - 4 >= min)
            {
                letter(0); letter(1);
                sb.Append(ShowPicker.Ellipsis);
                letter(n - 2); letter(n - 1);
            }
            else FirstAndLength(sb, n, letter);
            r.Text = sb.ToString();
            return r;
        }

        private static void FirstAndLength(System.Text.StringBuilder sb, int n, Action<int> letter)
        {
            letter(0);
            sb.Append('_', n - 1);
        }

        /// <summary>
        /// show.js ladder() held to the floor (lead's 3.1 ruling under QUESTIONS 11). Try 1 is <see cref="RemixLines.Ladder"/>,
        /// trimmed to "not quite. it's N letters." when the first letter is one of her masked blanks (naming it would leave
        /// fewer than the floor unknown). Try 2 masks the chunks and draws one random value (her name or not) exactly as
        /// RemixLines.Ladder does.
        /// </summary>
        public static string Ladder(string w, string s, int tries, Func<double> random, ICollection<int> masked)
        {
            if (tries == 1)
            {
                string line = RemixLines.Ladder(w, s, tries, random);
                if (masked == null || !masked.Contains(0)) return line;
                int cut = line.IndexOf(" and starts with ", StringComparison.Ordinal);
                return cut < 0 ? line : line.Substring(0, cut) + ".";
            }
            bool name = tries == 2 && random() < .5;
            return "keep going" + (name ? ", " + RemixLines.Name : "") + ". sound it out: <b>" + Render(w, s, masked).Text + "</b>";
        }
    }

    /// <summary>parts.js:43 what the typed answer is compared as: JS trim, then lowercase.</summary>
    public static class TypedAnswer
    {
        public static string Normalize(string raw)
        {
            return Answers.JsTrim(raw ?? "").ToLowerInvariant();
        }

        /// <summary>
        /// What a key typed into the field becomes: lowercase (screens.css .ans text-transform:lowercase), and nothing for
        /// tabs, line breaks and other control characters (Tab moves focus, Enter submits).
        /// </summary>
        public static char Filter(char c)
        {
            if (char.IsControl(c)) return '\0';
            return char.ToLowerInvariant(c);
        }
    }

    /// <summary>What a typed submission did.</summary>
    public enum TypedResult
    {
        /// <summary>The song is already answered, or not on screen.</summary>
        Ignored,
        /// <summary>Nothing typed: the field takes focus, nothing else (parts.js:44).</summary>
        Empty,
        Right,
        /// <summary>A wrong try with tries left: the ladder line.</summary>
        Wrong,
        /// <summary>The third wrong try: the word is shown.</summary>
        Revealed
    }

    /// <summary>What a typed round needs from the show (the web ctx).</summary>
    public interface ITypedHost
    {
        /// <summary>The song is on screen and not answered yet.</summary>
        bool Open { get; }
        int Tries { get; }
        int Miss();
        bool Win();
        bool Reveal();
        void Say(string line, string pose);
        void Next(float seconds);
        /// <summary>show.js ladder() for this many wrong tries.</summary>
        string Ladder(int tries);
        void Log(string line);
        void Error(Exception e);
    }

    /// <summary>What a typed round asks of its drawing.</summary>
    public interface ITypedView
    {
        /// <summary>The field takes keyboard focus.</summary>
        void Focus();
        /// <summary>parts.js:60 inp.select(): focus with the text selected, so typing replaces it.</summary>
        void SelectAll();
        /// <summary>parts.js:46/54 the field and the check button off.</summary>
        void Lock();
        /// <summary>parts.js:47 .ans.right and pop.</summary>
        void MarkRight();
        /// <summary>parts.js:52 the field and the row wobble (show.js:163 wobbles [data-wobble], parts.js:52 the field).</summary>
        void Wobble();
        /// <summary>parts.js:55-56 the word in the field (.ans.shown), and onReveal: every blank filled (slots.js fillAll).</summary>
        void ShowAnswer(string word);
    }

    /// <summary>
    /// parts.js:38-68 wireTyped as a plain state machine: Enter or "check it" submits; empty does nothing; right wins and
    /// moves on after 1.4 s; a wrong try says the ladder and selects the text; the third wrong try shows the word and moves
    /// on after 2.8 s. Hints never touch it (they cost fans, never the streak: the engine's rule).
    /// Logs "WT_TYPED val=... result=... tries=N" for the browser tests.
    /// </summary>
    public sealed class TypedRound
    {
        /// <summary>parts.js:48, :57.</summary>
        public const float NextAfterWin = 1.4f, NextAfterReveal = 2.8f;
        /// <summary>parts.js:66 focus 50 ms after the song is drawn.</summary>
        public const float FocusDelay = .05f;

        public readonly string Word;
        private readonly ITypedHost _host;
        private readonly ITypedView _view;

        public TypedRound(string word, ITypedHost host, ITypedView view)
        {
            Word = word ?? throw new ArgumentNullException(nameof(word));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _view = view ?? throw new ArgumentNullException(nameof(view));
        }

        /// <summary>parts.js:40 done: answered or shown.</summary>
        public bool Done { get; private set; }

        /// <summary>parts.js:41-62 check().</summary>
        public TypedResult Submit(string raw)
        {
            if (Done || !_host.Open) return Log("", TypedResult.Ignored);
            string val = TypedAnswer.Normalize(raw);
            if (val.Length == 0)
            {
                Safe(_view.Focus);
                return Log(val, TypedResult.Empty);
            }
            if (val == Word)
            {
                Done = true;
                Safe(_view.Lock);
                Safe(_view.MarkRight);
                Safe(() => _host.Win());
                _host.Next(NextAfterWin);
                return Log(val, TypedResult.Right);
            }
            int tries;
            try { tries = _host.Miss(); }
            catch (Exception e) { _host.Error(e); tries = _host.Tries; }
            Safe(_view.Wobble);
            if (tries >= 3)
            {
                Done = true;
                Safe(_view.Lock);
                Safe(() => _view.ShowAnswer(Word));
                Safe(() => _host.Reveal());
                _host.Next(NextAfterReveal);
                return Log(val, TypedResult.Revealed);
            }
            Safe(() => _host.Say(_host.Ladder(tries), "oops"));
            Safe(_view.SelectAll);
            return Log(val, TypedResult.Wrong);
        }

        private TypedResult Log(string val, TypedResult r)
        {
            string name = r.ToString().ToLowerInvariant();
            _host.Log("WT_TYPED val=" + val.Replace(' ', '_') + " result=" + name + " tries=" + _host.Tries);
            return r;
        }

        private void Safe(Action a)
        {
            try { a(); }
            catch (Exception e) { _host.Error(e); }
        }
    }
}
