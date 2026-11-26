using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Data;
using WorldTour.Core.Maths;
using WorldTour.Core.Shows;
using WorldTour.Game.Modes;

namespace WorldTour.Game.EditTests
{
    /// <summary>
    /// Missing letters, missing vowels and fan mail (world_tour/modes/spelling/slots.js, vowels.js, fanmail.js and the
    /// parts.js pieces they use) as plain classes. The JS vectors were printed by slots.js:110-111, fanmail.js:162/165 and
    /// parts.js:89-97, copied verbatim into b31_vectors.mjs (the 3.1 builder's scratchpad) with Math.random = mulberry32(seed).
    /// The letter-hint floor (QUESTIONS 11) is checked against every word in the live weeks.json.
    /// </summary>
    public class SpellingModesTests
    {
        private static readonly string Rsq = ((char)0x2019).ToString();

        // ---------- which letters hide ----------

        public static readonly object[][] JsSlots =
        {
            new object[] { "irrational", 1, 1, "3,7,8", 0.9948229456786066 },
            new object[] { "irrational", 2, 2, "0,1,4,9", 0.11403172370046377 },
            new object[] { "irrational", 3, 3, "1,2,5,6,9", 0.5526072245556861 },
            new object[] { "empire", 1, 4, "2,4", 0.5084968542214483 },
            new object[] { "temple", 3, 5, "0,1,2,5", 0.5921649402007461 },
            new object[] { "international", 2, 6, "1,3,8,9,12", 0.02235182886943221 },
            new object[] { "pyramid", 1, 7, "3,5", 0.4662326325196773 },
            new object[] { "ab", 1, 8, "1", 0.6251922610681504 },
            new object[] { "cat", 3, 9, "1,2", 0.1417661067098379 },
            new object[] { "irreplaceable", 3, 20261120, "0,2,3,5,7,8", 0.13346199970692396 },
            new object[] { "monument", 2, 11, "0,1,5,7", 0.5305055116768926 },
        };

        [Test, TestCaseSource(nameof(JsSlots))]
        public void SlotsHide_MatchesTheWebGame(string word, int level, int seed, string hide, double nextAfter)
        {
            var r = new Rng(seed);
            List<int> h = MissingLetters.SlotsHide(word, level, r.Next);
            Assert.AreEqual(hide, string.Join(",", h));
            Assert.AreEqual(nextAfter, r.Next(), 0.0, "used exactly as many random numbers as the JS");
        }

        [Test]
        public void SlotsHide_AboutAThird_MoreByLevel_NeverEveryLetter()
        {
            var r = new Rng(31);
            for (int len = 2; len <= 16; len++)
            {
                string w = new string(Enumerable.Range(0, len).Select(i => (char)('a' + i % 26)).ToArray());
                for (int level = 1; level <= 3; level++)
                {
                    int want = Math.Min(Math.Max(2, (int)Math.Round(len / 3.0, MidpointRounding.AwayFromZero)) + level - 1, len - 1);
                    for (int k = 0; k < 40; k++)
                    {
                        List<int> h = MissingLetters.SlotsHide(w, level, r.Next);
                        Assert.AreEqual(want, h.Count, w + " level " + level);
                        Assert.AreEqual(h.Count, h.Distinct().Count());
                        CollectionAssert.IsOrdered(h);
                        Assert.IsTrue(h.All(i => i >= 0 && i < len));
                    }
                }
            }
            Assert.AreEqual(2, MissingLetters.SlotsCount(4, 1));            // round(1.33)
            Assert.AreEqual(2, MissingLetters.SlotsCount(5, 1));            // round(1.67) = 2
            Assert.AreEqual(3, MissingLetters.SlotsCount(8, 1));            // round(2.67)
            Assert.AreEqual(5, MissingLetters.SlotsCount(13, 2));           // round(4.33) + 1
        }

        [Test]
        public void VowelsHide_EveryAeiou_NotY()
        {
            CollectionAssert.AreEqual(new[] { 0, 3, 5 }, MissingLetters.VowelsHide("empire"));
            Assert.AreEqual("_mp_r_", MissingLetters.Row("empire", MissingLetters.VowelsHide("empire"), '_'));   // vowels.js:1
            CollectionAssert.AreEqual(new[] { 3, 5 }, MissingLetters.VowelsHide("pyramid"));
            CollectionAssert.IsEmpty(MissingLetters.VowelsHide("rhythm"));
            Assert.AreEqual("rhythm", MissingLetters.Row("rhythm", MissingLetters.VowelsHide("rhythm"), '_'));
            Assert.IsFalse(MissingLetters.IsVowel('y'));
            foreach (char c in "aeiou") Assert.IsTrue(MissingLetters.IsVowel(c));
            Assert.AreEqual("e?pi?e", MissingLetters.Row("empire", new[] { 1, 4 }, '?'));
        }

        // ---------- letter hints and the floor (QUESTIONS 11) ----------

        [Test]
        public void LetterHints_SyllablesFirst_ThenLeftmostBlank_ThenTheFloorHolds()
        {
            // civilization: 6 vowels, a third of 12 is 4, so 2 letters may be given (the web gives 5)
            var h = new LetterHints("civilization", "civ-i-li-za-tion", MissingLetters.VowelsHide("civilization"));
            Assert.AreEqual(6, h.WebMax);
            Assert.AreEqual(3, h.Max);
            LetterHint first = h.Apply(1);
            Assert.AreEqual(-1, first.Index);
            // civ-i-li-za-tion: chunks "civ - ___ - ___ - ___ - tion"; the blanks 1 and 3 may show, 5, 7, 9 and 10 are hers.
            // The chunks already name blank 1 (the i of "civ"), so the paid letter is blank 3 (polish 6.2B item 5).
            Assert.AreEqual(SpellingLines.SoundsLike("civ - ___ - ___ - ___ - t__n"), first.Line);
            LetterHint second = h.Apply(2);
            Assert.AreEqual(3, second.Index);
            Assert.AreEqual("here" + Rsq + "s a letter for you: <b>i</b>", second.Line);
            Assert.AreEqual(5, h.Remaining);
            Assert.AreEqual(-1, h.Apply(3).Index, "past the floor only the syllables come back");
            Assert.AreEqual(5, h.Remaining);
            Assert.AreEqual(4, h.Unknown, "the floor holds: 4 of 12 stay unknown");

            // temple: 2 vowels, nothing above the floor, so the only hint is the syllables
            var t = new LetterHints("temple", "tem-ple", MissingLetters.VowelsHide("temple"));
            Assert.AreEqual(2, t.WebMax);
            Assert.AreEqual(1, t.Max);
            Assert.AreEqual(-1, t.Apply(1).Index);
            Assert.AreEqual(-1, t.Apply(2).Index);

            // no vowels at all: one hint, the syllables (parts.js:74 Math.max(1, 0))
            Assert.AreEqual(1, new LetterHints("rhythm", "rhythm", new List<int>()).Max);
        }

        [Test]
        public void LetterHints_PaidLetterNeverRepeatsAFirstMissStartsWith()
        {
            // polish 6.2B item 5: imagination hides its vowels, the first one is the first letter. The first miss says
            // 'starts with "i"', so the paid letter must be the next blank, and the floor (4 of 11 unknown) still holds.
            const string w = "imagination";
            var h = new LetterHints(w, "i-mag-i-na-tion", MissingLetters.VowelsHide(w));
            Assert.AreEqual(0, h.Hide[0]);
            Assert.AreEqual(2, h.Givable);
            h.NoteLadder(1);
            Assert.IsTrue(h.IsTold(0));
            LetterHint paid = h.Apply(2);
            Assert.AreEqual(2, paid.Index, "skips the told first letter");
            StringAssert.Contains("<b>a</b>", paid.Line);
            StringAssert.DoesNotContain("<b>i</b>", paid.Line);
            Assert.AreEqual(-1, h.Apply(3).Index, "the floor holds: no more letters once 4 are unknown");
            Assert.GreaterOrEqual(h.Unknown, SpellingHints.MinHidden(w.Length));

            // the ladder's second line (chunks) tells its letters too
            var g = new LetterHints(w, "i-mag-i-na-tion", MissingLetters.VowelsHide(w));
            g.NoteLadder(2);
            var toldBefore = g.Hide.Where(g.IsTold).ToList();
            Assert.IsNotEmpty(toldBefore);
            Assert.IsFalse(toldBefore.Contains(g.Apply(2).Index));
        }

        private static List<Word> LiveWords()
        {
            string assets;
            try { assets = UnityEngine.Application.dataPath; }
            catch (Exception) { assets = Environment.GetEnvironmentVariable("WT_ASSETS"); }
            WeeksLoad load = DataFiles.LoadWeeks(File.ReadAllText(Path.Combine(assets, "StreamingAssets", "weeks.json")));
            Assert.IsNotNull(load.File, "weeks.json did not load");
            var words = load.File.weeks.SelectMany(w => w.words).Where(v => v != null && !string.IsNullOrEmpty(v.w)).ToList();
            Assert.IsNotEmpty(words);
            return words;
        }

        /// <summary>Presses the hint button as often as the song allows and checks what is left for her.</summary>
        private static void CheckFloor(string w, string s, List<int> hide, string at)
        {
            var h = new LetterHints(w, s, hide);
            Assert.LessOrEqual(h.Max, h.WebMax, at + ": never more hints than the web game");
            int floor = SpellingHints.MinHidden(w.Length);
            var given = new List<int>();
            // hint 1 (the syllables) names its chunk letters; a paid letter never repeats one (polish 6.2B item 5)
            HashSet<int> told = MaskedChunks.Render(w, s, h.Masked).Shown;
            for (int n = 1; n <= h.Max; n++)
            {
                LetterHint x = h.Apply(n);
                if (n == 1) Assert.AreEqual(-1, x.Index, at + ": hint 1 is the syllables");
                else if (x.Index < 0) Assert.AreEqual(-1, x.Index);                      // nothing new may be said: the syllables again
                else
                {
                    Assert.IsTrue(hide.Contains(x.Index), at + ": only a blank is filled");
                    Assert.IsFalse(told.Contains(x.Index) || given.Contains(x.Index), at + ": hint " + n + " repeats a letter already told");
                    Assert.AreEqual(hide.First(k => !given.Contains(k) && !told.Contains(k)), x.Index, at + ": the leftmost untold blank");
                    given.Add(x.Index);
                }
            }
            int left = hide.Count(k => !given.Contains(k) && !told.Contains(k));
            Assert.GreaterOrEqual(left, Math.Min(hide.Count, floor), at + ": too few blanks left (" + left + ")");
            // the last hidden letters are hers: every letter a paid hint gave is one the floor allows
            Assert.IsTrue(given.All(k => h.Allowed.Contains(k)), at);
            // every pre-reveal hint together: the visible row, the given letters, the sounds-like text, the ladder's line 2
            var known = new HashSet<int>(Enumerable.Range(0, w.Length).Where(i => !hide.Contains(i)));
            known.UnionWith(given);
            ChunksText sounds = MaskedChunks.Render(w, s, h.Masked);
            known.UnionWith(sounds.Shown);
            CheckShownMatchesText(w, sounds, at);
            StringAssert.Contains(sounds.Text, h.SoundsLikeText);
            StringAssert.Contains("<b>" + sounds.Text + "</b>", MaskedChunks.Ladder(w, s, 2, () => .9, h.Masked));
            // the ladder's first line names the first letter only when that is allowed
            string ladder1 = MaskedChunks.Ladder(w, s, 1, () => .9, h.Masked);
            StringAssert.StartsWith("not quite. it" + Rsq + "s <b>" + w.Length + " letters</b>", ladder1, at);
            if (ladder1.Contains("starts with")) known.Add(0);
            Assert.AreEqual(!h.Masked.Contains(0), ladder1.Contains("starts with"), at + ": " + ladder1);
            int unknown = hide.Count(i => !known.Contains(i));
            Assert.GreaterOrEqual(unknown, Math.Min(hide.Count, floor), at + ": after every hint only " + unknown + " letters are unknown (" + sounds.Text + ")");
        }

        /// <summary>The text's letters, read in order, are exactly the word's letters at the positions it claims to show.</summary>
        private static void CheckShownMatchesText(string w, ChunksText c, string at)
        {
            string letters = new string(c.Text.Where(ch => ch >= 'a' && ch <= 'z').ToArray());
            string want = new string(c.Shown.OrderBy(i => i).Select(i => w[i]).ToArray());
            Assert.AreEqual(want, letters, at + ": \"" + c.Text + "\"");
        }

        [Test]
        public void MaskedChunks_WithNoMask_IsSpellingHintsChunks_ForEveryLiveWordAndEverySplit()
        {
            foreach (Word v in LiveWords())
            {
                ChunksText c = MaskedChunks.Render(v.w, v.s, null);
                Assert.AreEqual(SpellingHints.Chunks(v.w, v.s), c.Text, v.w);
                CheckShownMatchesText(v.w, c, v.w);
                Assert.AreEqual(RemixLines.Ladder(v.w, v.s, 2, () => .2), MaskedChunks.Ladder(v.w, v.s, 2, () => .2, null));
                Assert.AreEqual(RemixLines.Ladder(v.w, v.s, 2, () => .7), MaskedChunks.Ladder(v.w, v.s, 2, () => .7, new HashSet<int>()));
                Assert.AreEqual(RemixLines.Ladder(v.w, v.s, 1, () => .2), MaskedChunks.Ladder(v.w, v.s, 1, () => .2, new HashSet<int> { 1 }));
            }
            for (int n = 1; n <= 14; n++)
            {
                string w = new string(Enumerable.Range(0, n).Select(i => (char)('a' + i % 26)).ToArray());
                Assert.AreEqual(SpellingHints.Chunks(w, null), MaskedChunks.Render(w, null, null).Text, w);
                foreach (string s in Splits(w))
                {
                    ChunksText c = MaskedChunks.Render(w, s, null);
                    Assert.AreEqual(SpellingHints.Chunks(w, s), c.Text, w + " " + s);
                    CheckShownMatchesText(w, c, s);
                }
            }
        }

        /// <summary>Every way to cut a word into 1 to 5 non-empty syllables.</summary>
        private static IEnumerable<string> Splits(string w)
        {
            int n = w.Length;
            for (int mask = 0; mask < (1 << Math.Max(0, n - 1)); mask++)
            {
                int cuts = 0; for (int b = mask; b != 0; b >>= 1) cuts += b & 1;
                if (cuts > 4) continue;
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < n; i++) { sb.Append(w[i]); if (i < n - 1 && (mask >> i & 1) == 1) sb.Append('-'); }
                yield return sb.ToString();
            }
        }

        [Test]
        public void SoundsLike_MasksTheBlanksThatAreHers()
        {
            // temple, blanks 1 and 2 at level 1 ("t??ple"): the web hint "tem - ple" would finish the word
            var t = new LetterHints("temple", "tem-ple", new List<int> { 1, 2 });
            Assert.AreEqual("t__ - ___", t.SoundsLikeText);
            // empire, vowels 0, 3, 5: one may show (0), the other two are hers
            var e = new LetterHints("empire", "em-pire", MissingLetters.VowelsHide("empire"));
            Assert.AreEqual("em - ____", e.SoundsLikeText);
            CollectionAssert.AreEquivalent(new[] { 3, 5 }, e.Masked);
            // a masked first letter: the ladder keeps the length and drops the letter
            var m = new LetterHints("empire", "em-pire", new List<int> { 0, 4 });
            Assert.AreEqual("not quite. it" + Rsq + "s <b>6 letters</b>.", MaskedChunks.Ladder("empire", "em-pire", 1, () => 0, m.Masked));
            Assert.AreEqual(RemixLines.Ladder("empire", "em-pire", 1, () => 0), MaskedChunks.Ladder("empire", "em-pire", 1, () => 0, e.Masked));
            // nothing hidden in the chunks' letters: unchanged
            Assert.AreEqual("em - ____", new LetterHints("empire", "em-pire", new List<int> { 4, 5 }).SoundsLikeText);
        }

        [Test]
        public void LetterHints_LeaveTheLastBlanksToHer_ForEveryLiveWord_Slots()
        {
            var r = new Rng(2026);
            foreach (Word v in LiveWords())
                for (int level = 1; level <= 3; level++)
                    for (int k = 0; k < 300; k++)
                        CheckFloor(v.w, v.s, MissingLetters.SlotsHide(v.w, level, r.Next), "slots " + v.w + " level " + level);
        }

        [Test]
        public void LetterHints_LeaveTheLastBlanksToHer_ForEveryLiveWord_Vowels()
        {
            foreach (Word v in LiveWords()) CheckFloor(v.w, v.s, MissingLetters.VowelsHide(v.w), "vowels " + v.w);
        }

        // ---------- remix: the glows and the ladder share the floor (task Q tester D1) ----------

        /// <summary>Lights and takes glowing tiles until the hint stops; returns the positions the glows showed.</summary>
        private static void GlowAll(RemixBoard b, HashSet<int> seen)
        {
            for (int guard = 0; guard < 40 && !b.IsFull; guard++)
            {
                RemixHint h = b.Hint();
                if (h.Glow < 0) return;
                int pos = b.Picked.Count;
                for (int k = 0; k <= pos; k++) seen.Add(k);
                b.Pick(h.Glow);
            }
        }

        private static void Ladder(RemixBoard b, int tries, string w, string s, HashSet<int> seen, string at)
        {
            string line = b.LadderLine(tries, () => .9);
            if (tries == 1)
            {
                StringAssert.StartsWith("not quite. it" + Rsq + "s <b>" + w.Length + " letters</b>", line, at);
                if (line.Contains("starts with")) { Assert.IsTrue(b.Shown.Contains(0), at); seen.Add(0); }
            }
            else
            {
                // the line's letters are exactly the chunk letters the board now counts as shown
                ChunksText full = MaskedChunks.Render(w, s, null);
                int open = line.IndexOf("<b>", StringComparison.Ordinal) + 3, close = line.LastIndexOf("</b>", StringComparison.Ordinal);
                string text = line.Substring(open, close - open);
                string letters = new string(text.Where(c => c >= 'a' && c <= 'z').ToArray());
                string want = new string(full.Shown.Where(k => b.Shown.Contains(k)).OrderBy(k => k).Select(k => w[k]).ToArray());
                Assert.AreEqual(want, letters, at + ": " + text);
                foreach (int k in full.Shown) if (b.Shown.Contains(k)) seen.Add(k);
            }
            b.ResetAfterMiss();                                               // the slots empty after the wrong try
        }

        [Test]
        public void Remix_GlowsAndLadderTogether_LeaveTheFloor_ForEveryLiveWord()
        {
            string[] orders = { "G12", "12G", "1G2", "G1G2G", "1G2G", "G2" };
            foreach (Word v in LiveWords())
                foreach (string order in orders)
                {
                    var b = new RemixBoard(v.w, v.w.Reverse().ToList()) { Syllables = v.s };
                    var seen = new HashSet<int>();
                    string at = v.w + " order " + order;
                    foreach (char step in order)
                    {
                        if (step == 'G') GlowAll(b, seen);
                        else Ladder(b, step - '0', v.w, v.s, seen, at);
                        int unknown = v.w.Length - seen.Count;
                        Assert.GreaterOrEqual(unknown, SpellingHints.MinHidden(v.w.Length), at + " after " + step + ": only " + unknown + " unknown");
                    }
                }
        }

        [Test]
        public void Remix_Intercept_TheLadderStopsTheGlow()
        {
            // the tester's case: "in - ___ - cept" shows 6 of 9, so after it no tile may glow
            var b = new RemixBoard("intercept", "intercept".Reverse().ToList()) { Syllables = "in-ter-cept" };
            Assert.AreEqual(RemixLines.Ladder("intercept", "in-ter-cept", 2, () => .9), b.LadderLine(2, () => .9), "nothing shown yet: the web line");
            b.ResetAfterMiss();
            var after = new HashSet<int>(b.Shown);
            GlowAll(b, after);                                                // may relight i, n (already shown), never t
            Assert.AreEqual(2, b.Picked.Count, "the glow stops before the first letter she hasn't been shown");
            Assert.AreEqual(6, after.Count);
            RemixHint h = b.Hint();
            Assert.AreEqual(-1, h.Glow);
            Assert.AreEqual(RemixLines.HintYoursToFind, h.Line);
            // and the other way round: glows first, then the line masks what would be too much
            var c = new RemixBoard("intercept", "intercept".Reverse().ToList()) { Syllables = "in-ter-cept" };
            var seen = new HashSet<int>();
            GlowAll(c, seen);
            Assert.AreEqual(6, seen.Count, "intercept may show 6 of 9");
            StringAssert.Contains("<b>in - ___ - c___</b>", c.LadderLine(2, () => .9));
        }

        [Test]
        public void Blanks_RunsOfUnderscoresAreSpread_SinglesAndDataLeftAlone()
        {
            Assert.AreEqual("pop - _ - <cspace=0.15em>__</cspace> - <cspace=0.15em>____</cspace>", WorldTour.Game.Ui.Blanks.Spread("pop - _ - __ - ____"));
            Assert.AreEqual("p _ _ _ d", WorldTour.Game.Ui.Blanks.Spread("p _ _ _ d"));
            Assert.AreEqual("a <noparse>x___y</noparse> <cspace=0.15em>___</cspace>", WorldTour.Game.Ui.Blanks.Spread("a <noparse>x___y</noparse> ___"));
            Assert.AreEqual("", WorldTour.Game.Ui.Blanks.Spread(null));
            Assert.AreEqual("sounds like: <b>em - <cspace=0.15em>____</cspace></b>", WorldTour.Game.Ui.Blanks.Spread(SpellingLines.SoundsLike("em - ____")));
        }

        // ---------- the typed answer ----------

        [Test]
        public void TypedAnswer_TrimsLikeJs_AndLowercases()
        {
            Assert.AreEqual("empire", TypedAnswer.Normalize("  Empire \t"));
            Assert.AreEqual("empire", TypedAnswer.Normalize(((char)0xFEFF).ToString() + "EMPIRE" + (char)0x00A0));
            Assert.AreEqual("em pire", TypedAnswer.Normalize(" em pire "));
            Assert.AreEqual("", TypedAnswer.Normalize("   "));
            Assert.AreEqual("", TypedAnswer.Normalize(null));
            Assert.AreEqual('e', TypedAnswer.Filter('E'));
            Assert.AreEqual('\0', TypedAnswer.Filter('\t'));
            Assert.AreEqual('\0', TypedAnswer.Filter('\n'));
            Assert.AreEqual('-', TypedAnswer.Filter('-'));
        }

        private sealed class FakeHost : ITypedHost
        {
            public bool OpenFlag = true, Resolved, ThrowOnMiss, ThrowOnSay;
            public int TriesCount, Wins, Reveals;
            public readonly List<float> Nexts = new List<float>();
            public readonly List<string> Said = new List<string>();
            public readonly List<string> Logs = new List<string>();
            public readonly List<Exception> Errors = new List<Exception>();
            public bool Open { get { return OpenFlag && !Resolved; } }
            public int Tries { get { return TriesCount; } }
            public int Miss() { TriesCount++; if (ThrowOnMiss) throw new InvalidOperationException("a listener failed"); return TriesCount; }
            public bool Win() { Resolved = true; Wins++; return true; }
            public bool Reveal() { Resolved = true; Reveals++; return true; }
            public void Say(string line, string pose) { if (ThrowOnSay) throw new InvalidOperationException("the dog failed"); Said.Add(pose + ":" + line); }
            public void Next(float seconds) { Nexts.Add(seconds); }
            public string Ladder(int tries) { return "ladder " + tries; }
            public void Log(string line) { Logs.Add(line); }
            public void Error(Exception e) { Errors.Add(e); }
        }

        private sealed class FakeView : ITypedView
        {
            public readonly List<string> Calls = new List<string>();
            public void Focus() { Calls.Add("focus"); }
            public void SelectAll() { Calls.Add("select"); }
            public void Lock() { Calls.Add("lock"); }
            public void MarkRight() { Calls.Add("right"); }
            public void Wobble() { Calls.Add("wobble"); }
            public void ShowAnswer(string word) { Calls.Add("show " + word); }
        }

        [Test]
        public void Typed_Empty_OnlyRefocuses()
        {
            var host = new FakeHost(); var view = new FakeView();
            var round = new TypedRound("empire", host, view);
            Assert.AreEqual(TypedResult.Empty, round.Submit("   "));
            Assert.AreEqual(0, host.TriesCount, "an empty box is never a miss");
            CollectionAssert.AreEqual(new[] { "focus" }, view.Calls);
            Assert.IsFalse(round.Done);
            StringAssert.StartsWith("WT_TYPED val= result=empty", host.Logs.Last());
        }

        [Test]
        public void Typed_Right_AnyCaseAndSpaces_WinsOnce()
        {
            var host = new FakeHost(); var view = new FakeView();
            var round = new TypedRound("empire", host, view);
            Assert.AreEqual(TypedResult.Right, round.Submit("  EMPIRE "));
            Assert.AreEqual(1, host.Wins);
            CollectionAssert.AreEqual(new[] { TypedRound.NextAfterWin }, host.Nexts);
            CollectionAssert.AreEqual(new[] { "lock", "right" }, view.Calls);
            Assert.IsTrue(round.Done);
            Assert.AreEqual(TypedResult.Ignored, round.Submit("empire"), "a second Enter does nothing");
            Assert.AreEqual(1, host.Wins);
            Assert.AreEqual("WT_TYPED val=empire result=right tries=0", host.Logs[0]);
        }

        [Test]
        public void Typed_ThreeMisses_LadderTwice_ThenTheWordIsShown()
        {
            var host = new FakeHost(); var view = new FakeView();
            var round = new TypedRound("empire", host, view);
            Assert.AreEqual(TypedResult.Wrong, round.Submit("empyre"));
            Assert.AreEqual(TypedResult.Wrong, round.Submit("empier"));
            CollectionAssert.AreEqual(new[] { "oops:ladder 1", "oops:ladder 2" }, host.Said);
            CollectionAssert.AreEqual(new[] { "wobble", "select", "wobble", "select" }, view.Calls);
            Assert.IsFalse(round.Done);
            Assert.AreEqual(TypedResult.Revealed, round.Submit("emprie"));
            Assert.AreEqual(1, host.Reveals);
            Assert.AreEqual(0, host.Wins);
            CollectionAssert.AreEqual(new[] { TypedRound.NextAfterReveal }, host.Nexts);
            CollectionAssert.AreEqual(new[] { "wobble", "select", "wobble", "select", "wobble", "lock", "show empire" }, view.Calls);
            Assert.AreEqual(2, host.Said.Count, "the reveal line is the show's, not the mode's");
            Assert.AreEqual(TypedResult.Ignored, round.Submit("empire"));
        }

        [Test]
        public void Typed_AMissAfterAWrongTry_ThenRight_Wins()
        {
            var host = new FakeHost(); var view = new FakeView();
            var round = new TypedRound("temple", host, view);
            round.Submit("tempel");
            Assert.AreEqual(TypedResult.Right, round.Submit("Temple"));
            Assert.AreEqual(1, host.Wins);
        }

        [Test]
        public void Typed_ThrowingListeners_NeverStopTheRound()
        {
            var host = new FakeHost { ThrowOnMiss = true, ThrowOnSay = true }; var view = new FakeView();
            var round = new TypedRound("empire", host, view);
            Assert.AreEqual(TypedResult.Wrong, round.Submit("x"));
            Assert.AreEqual(2, host.Errors.Count);
            CollectionAssert.Contains(view.Calls, "select");
            Assert.AreEqual(TypedResult.Right, round.Submit("empire"));
        }

        [Test]
        public void Typed_NotOpen_Ignored()
        {
            var host = new FakeHost { OpenFlag = false }; var view = new FakeView();
            Assert.AreEqual(TypedResult.Ignored, new TypedRound("empire", host, view).Submit("empire"));
            Assert.AreEqual(0, host.Wins);
            CollectionAssert.IsEmpty(view.Calls);
        }

        /// <summary>The real engine behind the round: SongContext's guards, without Unity.</summary>
        private sealed class EngineHost : ITypedHost
        {
            private readonly ShowEngine _e;
            public EngineHost(ShowEngine e) { _e = e; }
            public bool Open { get { return !_e.Finished && !_e.Resolved; } }
            public int Tries { get { return _e.Tries; } }
            public int Miss() { return _e.Miss(); }
            public bool Win() { _e.Win(); return true; }
            public bool Reveal() { _e.Reveal(); return true; }
            public void Say(string line, string pose) { }
            public void Next(float seconds) { }
            public string Ladder(int tries) { return RemixLines.Ladder(_e.Current.Item.Kid, null, tries, () => .9); }
            public void Log(string line) { }
            public void Error(Exception e) { throw e; }
        }

        [Test]
        public void Typed_OnTheEngine_HintsCostFans_NeverTheStreak()
        {
            var items = new List<ShowItem> { new WordItem("empire", "em-pire"), new WordItem("temple", "tem-ple"), new WordItem("pyramid", "pyr-a-mid") };
            var e = new ShowEngine(new ShowSetup { IsMath = false, ModeId = "slots", VenueId = "coffee", Items = items, SkillRecord = new SkillRecord(), ClockMs = () => 0 });
            e.Start();
            var view = new FakeView();

            // song 1: clean
            var h1 = new LetterHints("empire", "em-pire", new List<int> { 1, 4 });
            e.SetupHints(h1.Max);
            Assert.AreEqual(TypedResult.Right, new TypedRound("empire", new EngineHost(e), view).Submit("empire"));
            Assert.AreEqual(1, e.Streak);
            Assert.AreEqual(1000, e.Fans);
            e.Next();

            // song 2: one hint, then right: fewer fans, streak kept (not grown)
            var h2 = new LetterHints("temple", "tem-ple", MissingLetters.VowelsHide("temple"));
            e.SetupHints(h2.Max);
            Assert.IsTrue(e.Hint(n => { h2.Apply(n); return true; }));
            Assert.AreEqual(TypedResult.Right, new TypedRound("temple", new EngineHost(e), view).Submit("temple"));
            Assert.AreEqual(1, e.Streak, "a hint never breaks the streak");
            Assert.AreEqual(1500, e.Fans, "a hinted song earns 500");
            e.Next();

            // song 3: a wrong try breaks it, as in the web game
            e.SetupHints(1);
            var r3 = new TypedRound("pyramid", new EngineHost(e), view);
            Assert.AreEqual(TypedResult.Wrong, r3.Submit("piramid"));
            Assert.AreEqual(0, e.Streak);
            Assert.AreEqual(TypedResult.Right, r3.Submit("pyramid"));
        }

        // ---------- fan mail ----------

        private static readonly string[] WeekA = { "population", "tropical", "pyramid", "empire", "peninsula", "civilization", "rainforest", "indigenous", "temple", "monument" };
        private static readonly string[] WeekB = { "irrational", "intercept", "illegal" };
        private static readonly string[] WeekC = { "solo", "duet" };

        public static readonly object[][] JsFanmail =
        {
            new object[] { "empire", "A", 1, "indigenous,empire,pyramid,temple", "@glitterbus_kid", "indigenous,pyramid", 0.24752389639616013 },
            new object[] { "temple", "A", 2, "population,civilization,temple,monument", "@front_row_forever", "civilization,monument", 0.5813204094301909 },
            new object[] { "monument", "A", 3, "civilization,monument,peninsula,pyramid", "@riffs_biggest_fan", "pyramid,civilization", 0.7937320580240339 },
            new object[] { "irrational", "B", 4, "intercept,illegal,irrational", "@riffs_biggest_fan", "intercept,illegal", 0.0773914884775877 },
            new object[] { "illegal", "B", 5, "irrational,illegal,intercept", "@encore_or_riot", "irrational,intercept", 0.7201022456865758 },
            new object[] { "solo", "C", 6, "solo,duet", "@riffs_biggest_fan", "duet,-", 0.20216270093806088 },
            new object[] { "population", "A", 20261120, "rainforest,tropical,population,peninsula", "@gymfloor_glitter", "rainforest,tropical", 0.9886806784197688 },
        };

        [Test, TestCaseSource(nameof(JsFanmail))]
        public void Fanmail_DealAndCrossOuts_MatchTheWebGame(string word, string week, int seed, string opts, string handle, string crossed, double nextAfter)
        {
            string[] pool = week == "A" ? WeekA : week == "B" ? WeekB : WeekC;
            var r = new Rng(seed);
            FanmailDeal d = FanmailRules.Deal(word, pool, r.Next);
            Assert.AreEqual(opts, string.Join(",", d.Options));
            Assert.AreEqual(handle, d.Handle);
            var round = new FanmailRound(word, d.Options);
            var got = new List<string>();
            for (int k = 0; k < 2; k++) { int i = round.CrossOut(r.Next); got.Add(i < 0 ? "-" : d.Options[i]); }
            Assert.AreEqual(crossed, string.Join(",", got));
            Assert.AreEqual(nextAfter, r.Next(), 0.0, "used exactly as many random numbers as the JS");
        }

        [Test]
        public void Fanmail_OnePickPerSong()
        {
            var round = new FanmailRound("empire", new List<string> { "indigenous", "empire", "pyramid", "temple" });
            Assert.AreEqual(1, round.RightIndex);
            Assert.AreEqual(FanmailPick.Wrong, round.Pick(2));
            Assert.AreEqual(FanmailPick.Ignored, round.Pick(1), "one pick, like the original");
            Assert.AreEqual(2, round.Picked);
            Assert.AreEqual(-1, round.CrossOut(() => 0), "nothing is live after the pick");

            var right = new FanmailRound("empire", new List<string> { "indigenous", "empire" });
            Assert.AreEqual(FanmailPick.Right, right.Pick(1));
            Assert.AreEqual(FanmailPick.Ignored, right.Pick(0));
            Assert.AreEqual(FanmailPick.Ignored, new FanmailRound("a", new List<string> { "a" }).Pick(5));
        }

        [Test]
        public void Fanmail_HintLadder_CrossTwice_ThenFirstLetter()
        {
            var round = new FanmailRound("empire", new List<string> { "indigenous", "empire", "pyramid", "temple" });
            FanmailHint h1 = round.Hint(1, () => 0);
            Assert.IsTrue(h1.Counted);
            Assert.AreEqual(0, h1.Crossed);
            Assert.AreEqual("crossed one out for you.", h1.Line);
            Assert.AreEqual(FanmailPick.Ignored, round.Pick(0), "a crossed-out choice can't be picked");
            FanmailHint h2 = round.Hint(2, () => .99);
            Assert.AreEqual(3, h2.Crossed);
            Assert.AreEqual("one more gone. you" + Rsq + "ve got this.", h2.Line);
            FanmailHint h3 = round.Hint(3, () => 0);
            Assert.AreEqual(-1, h3.Crossed);
            Assert.IsTrue(h3.Counted);
            Assert.AreEqual("it starts with <b>" + (char)0x201C + "e" + (char)0x201D + "</b> and has <b>6</b> letters.", h3.Line);
            Assert.IsFalse(round.IsGone(2));
            Assert.AreEqual(FanmailPick.Right, round.Pick(1));

            // a two-word week: the second cross-out has nothing left, so it is free
            var small = new FanmailRound("solo", new List<string> { "solo", "duet" });
            Assert.IsTrue(small.Hint(1, () => 0).Counted);
            FanmailHint none = small.Hint(2, () => 0);
            Assert.IsFalse(none.Counted);
            Assert.IsNull(none.Line);
        }

        [Test]
        public void Fanmail_NoWeek_StillDealsTheWord()
        {
            FanmailDeal d = FanmailRules.Deal("empire", null, new Rng(1).Next);
            CollectionAssert.AreEqual(new[] { "empire" }, d.Options);
            Assert.AreEqual("a tricky clue", FanmailRules.ClueLower("A Tricky Clue"));
        }
    }
}
