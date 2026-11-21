using System;
using System.Collections.Generic;
using System.Linq;
using WorldTour.Core.Data;
using WorldTour.Core.Shows;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// Copy for studio session, lyric writer and prefix mixer, word for word from world_tour/modes/spelling (studio.js,
    /// lyric.js, prefix.js). ASCII source: curly quotes are built from their codes. Data (clues, roots, meanings) is passed
    /// in already escaped for TMP (<see cref="Esc"/>), as the web passes it through esc().
    /// </summary>
    public static class Spelling3Lines
    {
        private static readonly string Rsq = ((char)0x2019).ToString();
        private static readonly string Ldq = ((char)0x201C).ToString();
        private static readonly string Rdq = ((char)0x201D).ToString();

        /// <summary>util.js esc() for TMP: no rich text inside.</summary>
        public static string Esc(string s) { return "<noparse>" + (s ?? "") + "</noparse>"; }

        // ---------- studio.js ----------
        /// <summary>studio.js:19.</summary>
        public const string StudioDeskTag = "recording";
        /// <summary>studio.js:21.</summary>
        public const string TapToHear = "tap the record to hear it again";
        /// <summary>studio.js:22.</summary>
        public static readonly string StudioPlaceholder = "spell it like it" + Rsq + "s test day";
        /// <summary>studio.js:23.</summary>
        public const string Submit = "submit";
        /// <summary>studio.js:34.</summary>
        public static string StudioClue(string escLowerClue) { return "clue: <b>" + escLowerClue + "</b>"; }
        /// <summary>studio.js:36.</summary>
        public static string Ends(string w) { return w.Length + " letters: " + Pat.Ends(w); }
        /// <summary>studio.js:37.</summary>
        public static string Consonants(string w) { return "just the consonants: " + Pat.Consonants(w); }

        // ---------- lyric.js ----------
        /// <summary>lyric.js:58.</summary>
        public const string VerseLabel = "verse 2, line 1";
        /// <summary>lyric.js:79.</summary>
        public const string LyricPlaceholder = "type the missing word";
        /// <summary>lyric.js:89, :125.</summary>
        public static string Means(string escLowerClue) { return "the word means: <b>" + escLowerClue + "</b>"; }
        /// <summary>lyric.js:118.</summary>
        public static string NotThatOne(string escLowerClue)
        {
            return "not that one. read the whole line again: what would <i>" + escLowerClue + "</i> sound like here?";
        }
        /// <summary>lyric.js:124.</summary>
        public const string LyricCrossedOne = "crossed one out.";
        /// <summary>lyric.js:90 trimmed (QUESTIONS 11): when the first letter is past the floor, only the length.</summary>
        public static string HasLetters(string w) { return "it has <b>" + w.Length + "</b> letters."; }
        /// <summary>lyric.js:124.</summary>
        public const string DownToTwo = "down to two. read it out loud with each.";

        // ---------- prefix.js ----------
        /// <summary>prefix.js:166.</summary>
        public const string MixerTag = "the mixer";
        /// <summary>prefix.js:169.</summary>
        public const string DropBlank = "?";
        /// <summary>prefix.js:175.</summary>
        public const string DragOrTap = "drag a prefix onto the blank, or tap one and then tap the blank";
        /// <summary>prefix.js:173 a chip's label.</summary>
        public static string Chip(string p) { return p + "-"; }
        /// <summary>prefix.js:178.</summary>
        public static string Question(string escP, string escRoot) { return "so what does <b>" + escP + "-</b> do to <b>" + escRoot + "</b>?"; }
        /// <summary>prefix.js:179.</summary>
        public static string ItMeans(string escMeans) { return "it means " + Ldq + escMeans + Rdq; }
        /// <summary>prefix.js:187.</summary>
        public static readonly string BuildIt = "build this week" + Rsq + "s word: which prefix goes on the front?";
        /// <summary>prefix.js:197.</summary>
        public static string NiceBuild(string escW) { return "<b>" + escW + "</b>. nice build. now the meaning."; }
        /// <summary>prefix.js:219.</summary>
        public static string RevealBuild(string escP, string escRoot, string escW, string escMeans)
        {
            return "it" + Rsq + "s <b>" + escP + "-" + escRoot + "</b>: " + escW + ". <b>" + escP + "-</b> means " + Ldq + escMeans + "." + Rdq;
        }
        /// <summary>prefix.js:220 (the JS escapes p + root together).</summary>
        public static string SayItOutLoud(string escPRoot) { return "say it out loud: " + Ldq + escPRoot + "." + Rdq + " is that a word you know?"; }
        /// <summary>prefix.js:146 the "not" family rule.</summary>
        public static string NotRule(string escRoot, string escRoot0)
        {
            return "the " + Ldq + "not" + Rdq + " prefix changes to match the root" + Rsq + "s first sound: <b>il-</b> before l, <b>ir-</b> before r, <b>im-</b> before m or p, <b>in-</b> for most others. <b>"
                + escRoot + "</b> starts with " + Ldq + escRoot0 + Rdq + ".";
        }
        /// <summary>prefix.js:147.</summary>
        public static string MeansRule(string escP, string escMeans, string escRoot)
        {
            return "<b>" + escP + "-</b> means " + Ldq + escMeans + "." + Rdq + " does " + Ldq + escMeans + " " + escRoot + Rdq + " fit this word?";
        }
        /// <summary>prefix.js:232.</summary>
        public static string WinLine(string escP, string escRoot, string escMeans, string escW)
        {
            return escP + "- + " + escRoot + " = " + escMeans + " " + escRoot + ". that" + Rsq + "s <b>" + escW + "</b>.";
        }
        /// <summary>prefix.js:241.</summary>
        public static string RevealMeaning(string escP, string escMeans, string escW, string escLowerClue)
        {
            return "<b>" + escP + "-</b> means " + Ldq + escMeans + "." + Rdq + " " + escW + ": " + escLowerClue + ".";
        }
        /// <summary>prefix.js:242.</summary>
        public static string NotQuite(string escW, string escLowerClue, string escRoot)
        {
            return "not quite. here" + Rsq + "s what " + escW + " means: <i>" + escLowerClue + "</i>. which word fits in front of " + Ldq + escRoot + Rdq + "?";
        }
        /// <summary>prefix.js:251.</summary>
        public const string TryEach = "try each one out loud in front of the root. only one makes a real word.";
        /// <summary>prefix.js:252.</summary>
        public const string PrefixCrossedOne = "crossed one out.";
        /// <summary>prefix.js:258.</summary>
        public static string HereIsWhat(string escW, string escLowerClue) { return "here" + Rsq + "s what " + escW + " means: <i>" + escLowerClue + "</i>."; }
        /// <summary>prefix.js:259.</summary>
        public const string OneMeaningCrossed = "one meaning crossed out.";
    }

    /// <summary>parts.js:34 pat(w, show): each letter or "_", joined by spaces.</summary>
    public static class Pat
    {
        public static string Of(string w, Func<char, int, bool> show)
        {
            var parts = new string[w.Length];
            for (int i = 0; i < w.Length; i++) parts[i] = show(w[i], i) ? w[i].ToString() : "_";
            return string.Join(" ", parts);
        }

        /// <summary>studio.js:36, lyric.js:92: first and last letter.</summary>
        public static string Ends(string w) { return Of(w, (c, i) => i == 0 || i == w.Length - 1); }

        /// <summary>studio.js:37: every letter but a, e, i, o, u.</summary>
        public static string Consonants(string w) { return Of(w, (c, i) => !MissingLetters.IsVowel(c)); }
    }

    /// <summary>What a hint step said, and whether it reads the word slowly too (studio's syllable hint).</summary>
    public sealed class StepHint
    {
        public string Step;
        public string Line;
        /// <summary>studio.js:35: say(w, .55) after the syllables, when there is a voice.</summary>
        public bool SpeakSlow;
    }

    /// <summary>
    /// QUESTIONS 11 across a typed song's whole hint sequence (the lead's 3.2 ruling; intended difference 14): every hint
    /// and ladder line shows only what keeps at least <see cref="SpellingHints.MinHidden"/> letters unknown, counting what
    /// the earlier ones showed. Each step keeps the web's wording and order; letters past the floor are masked as "_",
    /// leftmost kept, and a line that would only name a masked first letter is trimmed.
    /// </summary>
    public sealed class HintFloor
    {
        private readonly HashSet<int> _shown = new HashSet<int>();
        public readonly string Word, Syllables;

        public HintFloor(string word, string syllables) { Word = word ?? ""; Syllables = syllables; }

        public int Floor { get { return SpellingHints.MinHidden(Word.Length); } }
        public IReadOnlyCollection<int> Shown { get { return _shown; } }
        public int Unknown { get { return Word.Length - _shown.Count; } }

        /// <summary>Of <paramref name="candidates"/>, the positions this step may show: those already known, then new ones from the left while the floor allows.</summary>
        public HashSet<int> Allow(IEnumerable<int> candidates)
        {
            var ok = new HashSet<int>();
            foreach (int p in candidates.Distinct().OrderBy(p => p))
            {
                if (_shown.Contains(p)) ok.Add(p);
                else if (Unknown - 1 >= Floor) { _shown.Add(p); ok.Add(p); }
            }
            return ok;
        }

        /// <summary>The "sounds like" chunks (SpellingHints.Chunks) with the letters past the floor masked.</summary>
        public string Chunks()
        {
            HashSet<int> full = MaskedChunks.Render(Word, Syllables, null).Shown;
            HashSet<int> ok = Allow(full);
            full.ExceptWith(ok);
            return MaskedChunks.Render(Word, Syllables, full).Text;
        }

        /// <summary>parts.js pat(w, show) with the letters past the floor shown as "_" too.</summary>
        public string Pat(Func<char, int, bool> show)
        {
            HashSet<int> ok = Allow(Enumerable.Range(0, Word.Length).Where(i => show(Word[i], i)));
            return global::WorldTour.Game.Modes.Pat.Of(Word, (c, i) => ok.Contains(i));
        }

        /// <summary>
        /// Polish 6.2D item 8: a pattern that gives only letters she has not been told yet (positions in <see cref="Shown"/> before
        /// this call stay hidden), or null when the floor lets no new one through. Same floor as <see cref="Pat"/>.
        /// </summary>
        public string NewPat(Func<char, int, bool> show)
        {
            var before = new HashSet<int>(_shown);
            HashSet<int> ok = Allow(Enumerable.Range(0, Word.Length).Where(i => show(Word[i], i)));
            ok.ExceptWith(before);
            return ok.Count == 0 ? null : global::WorldTour.Game.Modes.Pat.Of(Word, (c, i) => ok.Contains(i));
        }

        /// <summary>May the first letter be named ("starts with x")?</summary>
        public bool First() { return Word.Length > 0 && Allow(new[] { 0 }).Contains(0); }

        /// <summary>show.js ladder(): line 1 drops "and starts with x" when the first letter is past the floor; line 2's chunks are masked.</summary>
        public string Ladder(int tries, Func<double> random)
        {
            if (tries == 1) return MaskedChunks.Ladder(Word, Syllables, 1, random, First() ? null : new HashSet<int> { 0 });
            HashSet<int> full = MaskedChunks.Render(Word, Syllables, null).Shown;
            HashSet<int> ok = Allow(full);
            full.ExceptWith(ok);
            return MaskedChunks.Ladder(Word, Syllables, tries, random, full);
        }
    }

    /// <summary>
    /// studio.js:31-39: the hints are the clue, the syllables (read slowly too), first + last letters, then consonants
    /// only. Without a voice the clue is on screen from the start and its hint is skipped.
    /// The syllables are <see cref="SpellingHints.Chunks"/> (QUESTIONS 11: never the whole word).
    /// </summary>
    public sealed class StudioHints
    {
        public readonly string Word, Syllables, Clue;
        public readonly bool CanSpeak;
        public readonly IReadOnlyList<string> Steps;
        /// <summary>What every hint and ladder line of this song has shown (QUESTIONS 11).</summary>
        public readonly HintFloor Floor;

        public StudioHints(string word, string syllables, string clue, bool canSpeak, HintFloor floor = null)
        {
            Word = word; Syllables = syllables; Clue = clue ?? ""; CanSpeak = canSpeak;
            Floor = floor ?? new HintFloor(word, syllables);
            Steps = canSpeak ? new[] { "clue", "chunks", "ends", "cons" } : new[] { "chunks", "ends", "cons" };
        }

        public int Max { get { return Steps.Count; } }

        public StepHint Apply(int level)
        {
            string s = Steps[Math.Max(0, Math.Min(Steps.Count - 1, level - 1))];
            var h = new StepHint { Step = s };
            switch (s)
            {
                case "clue": h.Line = Spelling3Lines.StudioClue(Spelling3Lines.Esc(Clue.ToLowerInvariant())); break;
                case "chunks": h.Line = SpellingLines.SoundsLike(Floor.Chunks()); h.SpeakSlow = CanSpeak; break;
                case "ends": h.Line = Word.Length + " letters: " + Floor.Pat((c, i) => i == 0 || i == Word.Length - 1); break;
                default: h.Line = "just the consonants: " + Floor.Pat((c, i) => !MissingLetters.IsVowel(c)); break;
            }
            return h;
        }
    }

    /// <summary>lyric.js:86-94 the typed lyric's hints: the meaning, the first letter and length, the syllables, the ends.</summary>
    public sealed class LyricHints
    {
        public static readonly string[] Steps = { "def", "start", "chunks", "ends" };
        public readonly string Word, Syllables, Clue;
        public readonly HintFloor Floor;

        public LyricHints(string word, string syllables, string clue, HintFloor floor = null)
        {
            Word = word; Syllables = syllables; Clue = clue ?? "";
            Floor = floor ?? new HintFloor(word, syllables);
        }

        public int Max { get { return Steps.Length; } }

        public StepHint Apply(int level)
        {
            string s = Steps[Math.Max(0, Math.Min(Steps.Length - 1, level - 1))];
            var h = new StepHint { Step = s };
            switch (s)
            {
                case "def": h.Line = Spelling3Lines.Means(Spelling3Lines.Esc(Clue.ToLowerInvariant())); break;
                // a first letter past the floor is left out: "it has N letters." (the web line trimmed)
                case "start": h.Line = Floor.First() ? SpellingLines.StartsWith(Word) : Spelling3Lines.HasLetters(Word); break;
                case "chunks": h.Line = SpellingLines.SoundsLike(Floor.Chunks()); break;
                default:
                    // the first and last letters, but never one a hint or the ladder already told her ("starts with i" then "i _ _ l"):
                    // with nothing new the floor allows, it falls back to the letter count
                    string pat = Floor.NewPat((c, i) => i == 0 || i == Word.Length - 1);
                    h.Line = pat ?? Spelling3Lines.HasLetters(Word);
                    break;
            }
            return h;
        }
    }

    /// <summary>lyric.js: the sentence words, the deal, and the sheet's two halves.</summary>
    public static class LyricRules
    {
        /// <summary>lyric.js:73 typing from level 2.</summary>
        public static bool Typing(int level) { return level >= 2; }
        /// <summary>lyric.js:107.</summary>
        public const float NextAfterWin = 1.5f;
        /// <summary>lyric.js:112 the second wrong pick shows the word.</summary>
        public const int PicksBeforeReveal = 2;
        /// <summary>lyric.js:121.</summary>
        public const int ChoiceHints = 3;

        /// <summary>lyric.js:53 <c>v.x.split('___')</c>: the text before the first blank and the text after it (up to a second blank, as the JS keeps only two parts).</summary>
        public static string[] Halves(string x)
        {
            string[] parts = (x ?? "").Split(new[] { "___" }, StringSplitOptions.None);
            return new[] { parts[0], parts.Length > 1 ? parts[1] : "" };
        }

        /// <summary>parts.js:97 optionsFrom(correct, pool, n = 4): the word and up to three others, shuffled (two shuffles, in the JS order).</summary>
        public static List<string> OptionsFrom(string correct, IEnumerable<string> pool, Func<double> random, int n = 4)
        {
            var others = ShowPicker.Shuffle((pool ?? Enumerable.Empty<string>()).Where(x => x != correct), random).Take(n - 1);
            return ShowPicker.Shuffle(new[] { correct }.Concat(others), random);
        }

        /// <summary>lyric.js:98 the choices: optionsFrom(w, the week's sentence words).</summary>
        public static List<string> Options(string word, Week week, Func<double> random)
        {
            var pool = WeekRules.WithSentence(week).Select(v => v.w);
            return OptionsFrom(word, pool, random);
        }
    }

    /// <summary>
    /// Lyric writer's level-1 song (lyric.js:98-126): pick the word for the blank. A wrong pick is struck out and the dog
    /// nudges; the second wrong pick shows the word. Hints: cross out a wrong choice (twice), then the meaning.
    /// </summary>
    public sealed class LyricChoiceRound
    {
        private readonly bool[] _gone;
        public readonly string Word;
        public readonly IReadOnlyList<string> Options;
        public int RightIndex { get; private set; }
        public bool Done { get; private set; }
        public int Wrong { get; private set; }

        public LyricChoiceRound(string word, IReadOnlyList<string> options)
        {
            Word = word; Options = options; _gone = new bool[options.Count];
            RightIndex = options.ToList().IndexOf(word);
        }

        public bool IsGone(int i) { return i >= 0 && i < _gone.Length && _gone[i]; }

        /// <summary>A press: Right, Wrong (struck out), or Ignored (done, gone, or not a choice).</summary>
        public FanmailPick Pick(int i)
        {
            if (Done || i < 0 || i >= Options.Count || _gone[i]) return FanmailPick.Ignored;
            if (Options[i] == Word) { Done = true; return FanmailPick.Right; }
            _gone[i] = true;
            Wrong++;
            return FanmailPick.Wrong;
        }

        /// <summary>The engine's try count decides the reveal (lyric.js:112 tries >= 2); call after a wrong pick.</summary>
        public void EndAfterReveal() { Done = true; }

        /// <summary>parts.js crossOut: a live wrong choice at random (button order), or -1.</summary>
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

        /// <summary>lyric.js:121-126.</summary>
        public FanmailHint Hint(int level, Func<double> random, string clue)
        {
            var h = new FanmailHint();
            if (level <= 2)
            {
                h.Crossed = CrossOut(random);
                if (h.Crossed < 0) return h;
                h.Counted = true;
                h.Line = level == 1 ? Spelling3Lines.LyricCrossedOne : Spelling3Lines.DownToTwo;
                return h;
            }
            h.Counted = true;
            h.Line = Spelling3Lines.Means(Spelling3Lines.Esc((clue ?? "").ToLowerInvariant()));
            return h;
        }
    }

    /// <summary>One prefix mixer deal: the prefix chips and the meaning choices, in button order.</summary>
    public sealed class PrefixDeal
    {
        public readonly List<string> Board;
        public readonly List<string> Meanings;
        public PrefixDeal(List<string> board, List<string> meanings) { Board = board; Meanings = meanings; }
    }

    /// <summary>prefix.js:138-162: the board of prefixes and the meaning choices. Random numbers in the JS order.</summary>
    public static class PrefixRules
    {
        /// <summary>prefix.js:141.</summary>
        public static readonly string[] ExtraPrefixes = { "un", "re", "dis", "pre" };
        /// <summary>prefix.js:142.</summary>
        public static readonly string[] ExtraMeanings = { "again", "before", "against", "too much", "under" };
        /// <summary>prefix.js:143.</summary>
        public static readonly string[] NotFamily = { "in", "im", "il", "ir" };
        /// <summary>prefix.js:173 chip tilts by position (CSS degrees).</summary>
        public static readonly float[] ChipTilts = { -4f, 3f, -2f, 5f };
        /// <summary>prefix.js:245.</summary>
        public const int HintMax = 5;
        /// <summary>prefix.js:233.</summary>
        public const float NextAfterWin = 2f;
        /// <summary>prefix.js:218.</summary>
        public const float DropReset = .7f;

        public static PrefixDeal Deal(Word v, Week week, Func<double> random)
        {
            if (v == null || v.prefix == null) throw new ArgumentException("prefix mixer needs a word with a prefix", nameof(v));
            if (random == null) throw new ArgumentNullException(nameof(random));
            List<Word> withP = WeekRules.WithPrefix(week);
            var own = withP.Select(x => x.prefix.p).Distinct().ToList();      // [...new Set()] keeps first-seen order
            List<string> board;
            if (own.Count >= 3) board = ShowPicker.Shuffle(own, random);
            else
            {
                var extra = ShowPicker.Shuffle(ExtraPrefixes.Where(p => !own.Contains(p)), random).Take(3 - own.Count);
                board = ShowPicker.Shuffle(own.Concat(extra), random);
            }
            var meanings = withP.Select(x => x.prefix.means).Distinct().ToList();
            var others = meanings.Concat(ExtraMeanings).Distinct().Where(m => m != v.prefix.means);   // filter keeps first of each
            var picked = ShowPicker.Shuffle(others, random).Take(3);
            var mOpts = ShowPicker.Shuffle(new[] { v.prefix.means }.Concat(picked), random);
            return new PrefixDeal(board, mOpts);
        }

        /// <summary>prefix.js:145-148 rule(pf), with data escaped for TMP.</summary>
        public static string Rule(Prefix pf)
        {
            if (NotFamily.Contains(pf.p)) return Spelling3Lines.NotRule(Spelling3Lines.Esc(pf.root), Spelling3Lines.Esc(pf.root.Substring(0, 1)));
            return Spelling3Lines.MeansRule(Spelling3Lines.Esc(pf.p), Spelling3Lines.Esc(pf.means), Spelling3Lines.Esc(pf.root));
        }
    }

    /// <summary>What a prefix song asks of the show (the web ctx).</summary>
    public interface IPrefixHost
    {
        int Miss();
        bool Win();
        bool Reveal();
        void LockHints();
        void Say(string line, string pose);
        void Next(float seconds);
        void Later(Action action, float seconds);
        void Log(string line);
    }

    /// <summary>How a prefix song is drawn.</summary>
    public interface IPrefixView
    {
        /// <summary>The selected chip (tap to select, tap the blank to place), or -1.</summary>
        void Select(int chip);
        /// <summary>The blank shows a wrong prefix (.drop.bad, wobble), or "?" again.</summary>
        void DropWrong(string p);
        void DropClear();
        /// <summary>A chip struck out and disabled (.pchip.gone).</summary>
        void ChipGone(int chip);
        /// <summary>prefix.js:189-193 finishWord: the prefix in the blank, both halves ok, every chip off.</summary>
        void FinishWord();
        /// <summary>prefix.js:194-199 the meaning question appears.</summary>
        void ShowStep2();
        void MeaningGood(int i);
        void MeaningGone(int i);
        void LockMeanings();
        /// <summary>"next song" appears.</summary>
        void ShowNext();
        void Tap();
    }

    /// <summary>
    /// prefix.js:183-260 as a plain state machine: step 1 builds the word (drop the right prefix on the root), step 2 picks
    /// what the prefix means. Wrong tries count across both steps; the third ends the song with the answer. Five hints:
    /// three for step 1 (try each out loud, cross one out, the rule) and two for step 2 (the meaning, cross one out).
    /// </summary>
    public sealed class PrefixRound
    {
        private readonly IPrefixHost _host;
        private readonly IPrefixView _view;
        private readonly bool[] _chipGone, _meanGone;

        public readonly Word Word;
        public readonly PrefixDeal Deal;
        public int Step { get; private set; } = 1;
        public int H1 { get; private set; }
        public int H2 { get; private set; }
        public bool Over { get; private set; }
        public int Selected { get; private set; } = -1;
        public bool Built { get; private set; }

        public PrefixRound(Word word, PrefixDeal deal, IPrefixHost host, IPrefixView view)
        {
            Word = word; Deal = deal; _host = host; _view = view;
            _chipGone = new bool[deal.Board.Count];
            _meanGone = new bool[deal.Meanings.Count];
        }

        private Prefix Pf { get { return Word.prefix; } }
        private string E(string s) { return Spelling3Lines.Esc(s); }
        private string ClueLower { get { return E(WorldTour.Core.Data.TextRules.Curly(Word.h ?? "").ToLowerInvariant()); } }

        public bool ChipGone(int i) { return i >= 0 && i < _chipGone.Length && _chipGone[i]; }
        public bool MeaningGone(int i) { return i >= 0 && i < _meanGone.Length && _meanGone[i]; }

        /// <summary>dnd.js select: a tap (or Enter) on a chip selects it, a second tap deselects.</summary>
        public void Select(int chip)
        {
            if (Over || Step != 1 || chip < 0 || chip >= _chipGone.Length || _chipGone[chip]) return;
            Selected = Selected == chip ? -1 : chip;
            _view.Select(Selected);
        }

        /// <summary>dnd.js place: a tap on the blank drops the selected chip.</summary>
        public void PlaceSelected()
        {
            if (Selected < 0) return;
            int c = Selected;
            Selected = -1;
            _view.Select(-1);
            Drop(c);
        }

        /// <summary>prefix.js:210-222 onDrop.</summary>
        public void Drop(int chip)
        {
            if (Over || Step != 1 || chip < 0 || chip >= _chipGone.Length || _chipGone[chip]) return;
            string p = Deal.Board[chip];
            _host.Log("WT_PREFIX_DROP p=" + p + " right=" + (p == Pf.p ? "true" : "false"));
            _view.Tap();
            if (p == Pf.p) { FinishWord(); ToStep2(); return; }
            _view.DropWrong(p);
            _chipGone[chip] = true;
            _view.ChipGone(chip);
            int tries = _host.Miss();
            _host.Later(() => { if (!Built) _view.DropClear(); }, PrefixRules.DropReset);
            if (tries >= 3) { FinishWord(); EndWithReveal(Spelling3Lines.RevealBuild(E(Pf.p), E(Pf.root), E(Word.w), E(Pf.means))); }
            else if (tries == 1) _host.Say(Spelling3Lines.SayItOutLoud(E(p + Pf.root)), "oops");
            else _host.Say(PrefixRules.Rule(Pf), "oops");
        }

        private void FinishWord()
        {
            Built = true;
            Selected = -1;
            _view.FinishWord();
        }

        private void ToStep2()
        {
            Step = 2;
            _view.ShowStep2();
            _host.Say(Spelling3Lines.NiceBuild(E(Word.w)), "cheer");
        }

        private void EndWithReveal(string msg)
        {
            Over = true;
            _host.LockHints();
            _host.Reveal();
            _host.Say(msg, "oops");
            _view.ShowNext();
        }

        /// <summary>prefix.js:226-243 a meaning pick.</summary>
        public void PickMeaning(int i)
        {
            if (Over || Step != 2 || i < 0 || i >= _meanGone.Length || _meanGone[i]) return;
            string m = Deal.Meanings[i];
            _host.Log("WT_PREFIX_MEANING m=" + m + " right=" + (m == Pf.means ? "true" : "false"));
            if (m == Pf.means)
            {
                Over = true;
                _host.LockHints();
                _view.LockMeanings();
                _view.MeaningGood(i);
                _host.Win();
                _host.Say(Spelling3Lines.WinLine(E(Pf.p), E(Pf.root), E(Pf.means), E(Word.w)), "cheer");
                _host.Next(PrefixRules.NextAfterWin);
                return;
            }
            _meanGone[i] = true;
            _view.MeaningGone(i);
            int tries = _host.Miss();
            if (tries >= 3)
            {
                _view.LockMeanings();
                _view.MeaningGood(Deal.Meanings.IndexOf(Pf.means));
                EndWithReveal(Spelling3Lines.RevealMeaning(E(Pf.p), E(Pf.means), E(Word.w), ClueLower));
            }
            else _host.Say(Spelling3Lines.NotQuite(E(Word.w), ClueLower, E(Pf.root)), "oops");
        }

        /// <summary>prefix.js:245-260. False = nothing new (free). The JS bumps h1 before a cross-out that finds nothing, and so does this.</summary>
        public bool Hint(Func<double> random)
        {
            if (Over) return false;
            if (Step == 1)
            {
                if (H1 >= 3) return false;
                H1++;
                Selected = -1;
                _view.Select(-1);                                          // dnd.clear()
                if (H1 == 1) _host.Say(Spelling3Lines.TryEach, "think");
                else if (H1 == 2)
                {
                    int k = CrossOut(_chipGone, Deal.Board, Pf.p, random);
                    if (k < 0) return false;
                    _view.ChipGone(k);
                    _host.Say(Spelling3Lines.PrefixCrossedOne, "think");
                }
                else _host.Say(PrefixRules.Rule(Pf), "think");
                return true;
            }
            if (H2 >= 2) return false;
            H2++;
            if (H2 == 1) _host.Say(Spelling3Lines.HereIsWhat(E(Word.w), ClueLower), "think");
            else
            {
                int k = CrossOut(_meanGone, Deal.Meanings, Pf.means, random);
                if (k < 0) return false;
                _view.MeaningGone(k);
                _host.Say(Spelling3Lines.OneMeaningCrossed, "think");
            }
            return true;
        }

        private static int CrossOut(bool[] gone, IReadOnlyList<string> values, string right, Func<double> random)
        {
            var live = new List<int>();
            for (int i = 0; i < values.Count; i++) if (values[i] != right && !gone[i]) live.Add(i);
            if (live.Count == 0) return -1;
            int k = FanmailRules.Pick(live, random);
            gone[k] = true;
            return k;
        }
    }
}
