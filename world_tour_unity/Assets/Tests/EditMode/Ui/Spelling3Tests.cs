using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Data;
using WorldTour.Core.Maths;
using WorldTour.Core.Shows;
using WorldTour.Game.Modes;
using WorldTour.Game.Speech;

namespace WorldTour.Game.EditTests
{
    /// <summary>
    /// Studio session, lyric writer and prefix mixer (world_tour/modes/spelling/studio.js, lyric.js, prefix.js), read-aloud
    /// (js/speech.js, QUESTIONS 1) and the venue card's availability note (screens/venue.js:57). The JS vectors were printed
    /// by lyric.js:98 and prefix.js:159-162 plus parts.js crossOut, copied verbatim into b32_vectors.mjs (the 3.2 builder's
    /// scratchpad) with Math.random = mulberry32(seed), over the live weeks.json and one made-up four-word week.
    /// The hint floor (QUESTIONS 11, intended difference 14) is checked across every live word.
    /// </summary>
    public class Spelling3Tests
    {
        private static readonly string Rsq = ((char)0x2019).ToString();
        private static readonly string Ldq = ((char)0x201C).ToString();
        private static readonly string Rdq = ((char)0x201D).ToString();

        private static WeeksFile Live()
        {
            string assets;
            try { assets = UnityEngine.Application.dataPath; }
            catch (Exception) { assets = Environment.GetEnvironmentVariable("WT_ASSETS"); }
            WeeksLoad load = DataFiles.LoadWeeks(File.ReadAllText(Path.Combine(assets, "StreamingAssets", "weeks.json")));
            Assert.IsNotNull(load.File, "weeks.json did not load");
            return load.File;
        }

        private static Week LiveWeek(string id) { return Live().weeks.First(w => w.id == id); }

        private static IEnumerable<Word> LiveWords() { return Live().weeks.SelectMany(w => w.words).Where(v => v != null && !string.IsNullOrEmpty(v.w)); }

        private static Week Small()
        {
            Func<string, string, string, string, Word> W = (w, p, root, means) => new Word { w = w, h = w, prefix = new Prefix { p = p, root = root, means = means } };
            return new Week { id = "small", label = "small", words = new[] { W("unhappy", "un", "happy", "not"), W("unkind", "un", "kind", "not"), W("redo", "re", "do", "again"), W("retell", "re", "tell", "again") } };
        }

        // ---------- lyric writer ----------

        public static readonly object[][] JsLyric =
        {
            new object[] { "2026-10-26", "irrational", 1, "illogical,irrational,intersection,irreplaceable", "intersection,illogical", 0.15449313493445516 },
            new object[] { "2026-10-26", "intercept", 2, "intersection,irrational,intercept,interaction", "interaction,irrational", 0.14100669883191586 },
            new object[] { "2026-10-26", "illegal", 3, "intercept,illogical,irresponsible,illegal", "illogical,irresponsible", 0.8797733269166201 },
            new object[] { "2026-10-26", "international", 4, "international,illegal,illegible,irregular", "irregular,illegible", 0.7433461560867727 },
            new object[] { "2026-10-10", "population", 5, "civilization,temple,population,rainforest", "rainforest,temple", 0.013821420492604375 },
            new object[] { "2026-10-10", "tropical", 6, "rainforest,indigenous,tropical,peninsula", "indigenous,rainforest", 0.05154049233533442 },
            new object[] { "2026-10-10", "pyramid", 7, "rainforest,pyramid,peninsula,temple", "rainforest,temple", 0.5184025457128882 },
            new object[] { "2026-10-10", "empire", 8, "temple,peninsula,empire,population", "temple,population", 0.22035978082567453 },
        };

        [Test, TestCaseSource(nameof(JsLyric))]
        public void Lyric_OptionsAndCrossOuts_MatchTheWebGame(string week, string word, int seed, string opts, string crossed, double nextAfter)
        {
            var r = new Rng(seed);
            List<string> o = LyricRules.Options(word, LiveWeek(week), r.Next);
            Assert.AreEqual(opts, string.Join(",", o));
            var round = new LyricChoiceRound(word, o);
            var got = new List<string>();
            for (int k = 0; k < 2; k++) { int i = round.CrossOut(r.Next); got.Add(i < 0 ? "-" : o[i]); }
            Assert.AreEqual(crossed, string.Join(",", got));
            Assert.AreEqual(nextAfter, r.Next(), 0.0, "used exactly as many random numbers as the JS");
        }

        [Test]
        public void Lyric_SheetHalves_TypingFromLevel2_TwoPicks()
        {
            CollectionAssert.AreEqual(new[] { "we sang it ", " all night" }, LyricRules.Halves("we sang it ___ all night"));
            CollectionAssert.AreEqual(new[] { "a ", " b " }, LyricRules.Halves("a ___ b ___ c"), "the JS keeps only the first two parts");
            CollectionAssert.AreEqual(new[] { "no blank", "" }, LyricRules.Halves("no blank"));
            Assert.IsFalse(LyricRules.Typing(1));
            Assert.IsTrue(LyricRules.Typing(2));
            var round = new LyricChoiceRound("empire", new List<string> { "temple", "empire", "pyramid", "monument" });
            Assert.AreEqual(FanmailPick.Wrong, round.Pick(0));
            Assert.AreEqual(FanmailPick.Ignored, round.Pick(0), "a wrong pick is struck out and disabled");
            Assert.AreEqual(FanmailPick.Right, round.Pick(1));
            Assert.AreEqual(FanmailPick.Ignored, round.Pick(2));
            var h = new LyricChoiceRound("empire", new List<string> { "temple", "empire" });
            Assert.AreEqual("crossed one out.", h.Hint(1, () => 0, "x").Line);
            Assert.IsFalse(h.Hint(2, () => 0, "x").Counted, "nothing left to cross out: free");
            Assert.AreEqual("the word means: <b><noparse>a ruler" + Rsq + "s land</noparse></b>", h.Hint(3, () => 0, "A Ruler" + Rsq + "s Land").Line);
        }

        // ---------- prefix mixer ----------

        public static readonly object[][] JsPrefix =
        {
            new object[] { "2026-10-26", "irrational", 9, "inter,il,ir", "under|again|not|against", "il", "again", 0.28342978144064546 },
            new object[] { "2026-10-26", "illegal", 10, "ir,inter,il", "between|before|against|not", "inter", "between", 0.3927733809687197 },
            new object[] { "2026-10-26", "international", 11, "ir,inter,il", "again|between|too much|not", "ir", "not", 0.4199757508467883 },
            new object[] { "2026-10-26", "irresponsible", 12, "il,inter,ir", "not|under|before|again", "il", "again", 0.3264409159310162 },
            new object[] { "2026-10-26", "illegible", 13, "inter,ir,il", "not|before|against|again", "ir", "again", 0.2912685133051127 },
            new object[] { "small", "unhappy", 14, "un,pre,re", "under|too much|against|not", "pre", "under", 0.7593659609556198 },
            new object[] { "small", "unkind", 15, "pre,un,re", "before|under|not|too much", "pre", "before", 0.021488795056939125 },
            new object[] { "small", "redo", 16, "dis,re,un", "not|against|again|too much", "dis", "not", 0.27321626082994044 },
            new object[] { "small", "retell", 17, "dis,re,un", "against|before|again|too much", "un", "against", 0.23596820258535445 },
        };

        private sealed class PHost : IPrefixHost
        {
            public int Tries, Wins, Reveals, Locks, Nexts;
            public readonly List<string> Said = new List<string>();
            public readonly List<Action> Laters = new List<Action>();
            public int Miss() { return ++Tries; }
            public bool Win() { Wins++; return true; }
            public bool Reveal() { Reveals++; return true; }
            public void LockHints() { Locks++; }
            public void Say(string line, string pose) { Said.Add(pose + ":" + line); }
            public void Next(float s) { Nexts++; }
            public void Later(Action a, float s) { Laters.Add(a); }
            public void Log(string line) { }
        }

        private sealed class PView : IPrefixView
        {
            public readonly List<string> Calls = new List<string>();
            public void Select(int c) { Calls.Add("select " + c); }
            public void DropWrong(string p) { Calls.Add("wrong " + p); }
            public void DropClear() { Calls.Add("clear"); }
            public void ChipGone(int c) { Calls.Add("gone " + c); }
            public void FinishWord() { Calls.Add("finish"); }
            public void ShowStep2() { Calls.Add("step2"); }
            public void MeaningGood(int i) { Calls.Add("good " + i); }
            public void MeaningGone(int i) { Calls.Add("mgone " + i); }
            public void LockMeanings() { Calls.Add("lockm"); }
            public void ShowNext() { Calls.Add("next"); }
            public void Tap() { }
        }

        [Test, TestCaseSource(nameof(JsPrefix))]
        public void Prefix_DealAndCrossOuts_MatchTheWebGame(string week, string word, int seed, string board, string meanings, string chip, string meaning, double nextAfter)
        {
            Week wk = week == "small" ? Small() : LiveWeek(week);
            Word v = wk.words.First(x => x.w == word);
            var r = new Rng(seed);
            PrefixDeal d = PrefixRules.Deal(v, wk, r.Next);
            Assert.AreEqual(board, string.Join(",", d.Board));
            Assert.AreEqual(meanings, string.Join("|", d.Meanings));
            var host = new PHost(); var view = new PView();
            var round = new PrefixRound(v, d, host, view);
            Assert.IsTrue(round.Hint(r.Next), "hint 1: try each out loud");
            Assert.IsTrue(round.Hint(r.Next), "hint 2: cross one out");
            string gone = view.Calls.Where(c => c.StartsWith("gone ")).Select(c => d.Board[int.Parse(c.Substring(5))]).Single();
            Assert.AreEqual(chip, gone);
            round.Drop(d.Board.IndexOf(v.prefix.p));
            Assert.AreEqual(2, round.Step);
            Assert.IsTrue(round.Hint(r.Next), "step 2 hint 1: the meaning");
            Assert.IsTrue(round.Hint(r.Next), "step 2 hint 2: cross a meaning out");
            string mg = view.Calls.Where(c => c.StartsWith("mgone ")).Select(c => d.Meanings[int.Parse(c.Substring(6))]).Single();
            Assert.AreEqual(meaning, mg);
            Assert.AreEqual(nextAfter, r.Next(), 0.0, "used exactly as many random numbers as the JS");
        }

        [Test]
        public void Prefix_ThreeWrongTriesAcrossBothSteps_ShowTheAnswer()
        {
            Week wk = Small();
            Word v = wk.words[0];                                               // unhappy: un + happy, not
            var d = new PrefixDeal(new List<string> { "re", "pre", "un" }, new List<string> { "again", "not", "before", "under" });
            var host = new PHost(); var view = new PView();
            var round = new PrefixRound(v, d, host, view);
            round.Select(0);
            round.PlaceSelected();                                              // re: wrong (1)
            Assert.AreEqual(1, host.Tries);
            Assert.AreEqual("oops:say it out loud: " + Ldq + "<noparse>rehappy</noparse>." + Rdq + " is that a word you know?", host.Said.Last());
            round.Drop(0);
            Assert.AreEqual(1, host.Tries, "a struck-out chip can't be dropped again");
            round.Drop(1);                                                      // pre: wrong (2), the rule
            StringAssert.Contains("<b><noparse>un</noparse>-</b> means " + Ldq + "<noparse>not</noparse>." + Rdq, host.Said.Last());
            round.Drop(2);                                                      // un: built
            Assert.AreEqual(2, round.Step);
            Assert.AreEqual("cheer:<b><noparse>unhappy</noparse></b>. nice build. now the meaning.", host.Said.Last());
            foreach (Action a in host.Laters) a();
            CollectionAssert.DoesNotContain(view.Calls.Skip(view.Calls.IndexOf("finish")), "clear", "the reset after a wrong drop leaves the built word alone");
            round.PickMeaning(0);                                               // again: wrong (3) -> the answer
            Assert.AreEqual(3, host.Tries);
            Assert.AreEqual(1, host.Reveals);
            Assert.IsTrue(round.Over);
            CollectionAssert.Contains(view.Calls, "good 1");
            CollectionAssert.Contains(view.Calls, "next");
            Assert.IsFalse(round.Hint(() => 0), "no hints once it's over");
            round.PickMeaning(1);
            Assert.AreEqual(0, host.Wins);
        }

        [Test]
        public void Prefix_NotFamilyRule_AndTheWin()
        {
            var pf = new Prefix { p = "ir", root = "regular", means = "not" };
            Assert.AreEqual("the " + Ldq + "not" + Rdq + " prefix changes to match the root" + Rsq + "s first sound: <b>il-</b> before l, <b>ir-</b> before r, <b>im-</b> before m or p, <b>in-</b> for most others. <b><noparse>regular</noparse></b> starts with " + Ldq + "<noparse>r</noparse>" + Rdq + ".", PrefixRules.Rule(pf));
            var v = new Word { w = "irregular", h = "Not regular", prefix = pf };
            var host = new PHost(); var view = new PView();
            var round = new PrefixRound(v, new PrefixDeal(new List<string> { "ir", "il", "in" }, new List<string> { "not", "again" }), host, view);
            round.Drop(0);
            round.PickMeaning(0);
            Assert.AreEqual(1, host.Wins);
            Assert.AreEqual(1, host.Nexts);
            Assert.AreEqual("cheer:<noparse>ir</noparse>- + <noparse>regular</noparse> = <noparse>not</noparse> <noparse>regular</noparse>. that" + Rsq + "s <b><noparse>irregular</noparse></b>.", host.Said.Last());
        }

        // ---------- availability (index.js isAvailable, venue.js:57) ----------

        [Test]
        public void Availability_LyricAndPrefixNeedFourWords_AndTheBillNote()
        {
            Week oct10 = LiveWeek("2026-10-10"), oct26 = LiveWeek("2026-10-26");
            Assert.IsTrue(ModeRules.IsAvailable("lyric", oct10));
            Assert.IsFalse(ModeRules.IsAvailable("prefix", oct10), "the geography week has no prefixes");
            Assert.IsTrue(ModeRules.IsAvailable("prefix", oct26));
            Assert.IsTrue(ModeRules.IsAvailable("slots", new Week { id = "x", words = new Word[0] }));
            Assert.IsTrue(ModeRules.Pool("prefix", oct26).All(w => w.prefix != null && w.prefix.p + w.prefix.root == w.w));
            Assert.IsTrue(ModeRules.Pool("lyric", oct10).All(w => w.x.Contains("___")));
            Assert.AreEqual(oct10.words.Length, ModeRules.Pool("slots", oct10).Count);
            Assert.IsNull(ModeRules.BillNote(new List<KeyValuePair<string, string>>()));
            Assert.AreEqual("not on this week" + Rsq + "s bill: prefix mixer. prefix mixer plays on weeks built around prefixes. ",
                ModeRules.BillNote(new[] { new KeyValuePair<string, string>("prefix", "prefix mixer") }));
            Assert.AreEqual("not on this week" + Rsq + "s bill: lyric writer and prefix mixer. prefix mixer plays on weeks built around prefixes. lyric writer needs sentences for the words. ",
                ModeRules.BillNote(new[] { new KeyValuePair<string, string>("lyric", "lyric writer"), new KeyValuePair<string, string>("prefix", "prefix mixer") }));
        }

        // ---------- read-aloud (QUESTIONS 1) ----------

        [Test]
        public void Speech_Rates_Decision_AndTheLogLine()
        {
            Assert.AreEqual(.8f, SpeechRules.Rate(false));
            Assert.AreEqual(.55f, SpeechRules.Rate(true));
            Assert.AreEqual(SpeechChoice.Clue, SpeechRules.Decide(false, true, 3, 0));
            Assert.AreEqual(SpeechChoice.Clue, SpeechRules.Decide(true, false, 3, 0), "before the start click: the clue");
            Assert.AreEqual(SpeechChoice.Speak, SpeechRules.Decide(true, true, 3, 0));
            Assert.AreEqual(SpeechChoice.Wait, SpeechRules.Decide(true, true, 0, 1.9f), "voices load late: wait");
            Assert.AreEqual(SpeechChoice.Clue, SpeechRules.Decide(true, true, 0, SpeechRules.VoicesTimeout), "no voice in time: the clue");
            Assert.AreEqual("WT_SPEAK word=empire rate=0.80", SpeechRules.Log("empire", .8f));
            Assert.AreEqual("WT_SPEAK word=empire rate=0.55", SpeechRules.Log("empire", .55f));
        }

        [Test]
        public void Speech_InTheEditor_LogsTheWord_AndForceNoVoiceGivesTheClue()
        {
            try { UnityEngine.Debug.isDebugBuild.ToString(); }
            catch (Exception) { Assert.Ignore("needs Unity (the scratch harness has no engine)"); }
            Assert.AreEqual(SpeechChoice.Speak, ReadAloud.Decide(0), "the Editor's logging voice");
            var logs = new List<string>();
            UnityEngine.Application.LogCallback grab = (m, st, t) => logs.Add(m);
            UnityEngine.Application.logMessageReceived += grab;
            try { ReadAloud.Say("temple"); ReadAloud.Say("temple", true); }
            finally { UnityEngine.Application.logMessageReceived -= grab; }
            CollectionAssert.AreEqual(new[] { "WT_SPEAK word=temple rate=0.80", "WT_SPEAK word=temple rate=0.55" }, logs.Where(l => l.StartsWith("WT_SPEAK")).ToList());
            ReadAloud.ForceNoVoice = true;
            try { Assert.AreEqual(SpeechChoice.Clue, ReadAloud.Decide(0)); }
            finally { ReadAloud.ForceNoVoice = false; }
        }

        // ---------- studio hints and the floor (QUESTIONS 11, intended difference 14) ----------

        [Test]
        public void Studio_Steps_WithAndWithoutAVoice_AndPyramidsConsonants()
        {
            var can = new StudioHints("pyramid", "pyr-a-mid", "A Monument", true);
            CollectionAssert.AreEqual(new[] { "clue", "chunks", "ends", "cons" }, can.Steps);
            Assert.AreEqual("clue: <b><noparse>a monument</noparse></b>", can.Apply(1).Line);
            StepHint chunks = can.Apply(2);
            Assert.IsTrue(chunks.SpeakSlow);
            Assert.AreEqual("sounds like: <b>pyr - _ - ___</b>", chunks.Line);
            Assert.AreEqual("7 letters: p _ _ _ _ _ d", can.Apply(3).Line);
            // the web shows p y r _ m _ d, which with the rest leaves only a and i (2 of 7, under the floor of 3)
            Assert.AreEqual("just the consonants: p y r _ _ _ d", can.Apply(4).Line);
            var no = new StudioHints("pyramid", "pyr-a-mid", "x", false);
            CollectionAssert.AreEqual(new[] { "chunks", "ends", "cons" }, no.Steps);
            Assert.IsFalse(no.Apply(1).SpeakSlow, "no voice: nothing read slowly");
            // a long word keeps the web lines while the floor allows
            var irr = new StudioHints("irreplaceable", "ir-re-place-a-ble", "x", true);
            Assert.AreEqual("sounds like: <b>ir - ___ - ___ - ___ - ble</b>", irr.Apply(2).Line);
        }

        /// <summary>Plays a typed song's hints and ladder lines in <paramref name="order"/> and checks the floor after each.</summary>
        private static void CheckFloor(string w, string s, string order, Func<HintFloor, int, string> hint, int maxHints, string at)
        {
            var floor = new HintFloor(w, s);
            int n = 0;
            foreach (char step in order)
            {
                string line;
                if (step == 'H') { if (++n > maxHints) continue; line = hint(floor, n); }
                else line = floor.Ladder(step - '0', () => .9);
                Assert.GreaterOrEqual(floor.Unknown, SpellingHints.MinHidden(w.Length), at + " after " + step + ": " + line);
                if (step == '1' && line.Contains("starts with")) CollectionAssert.Contains(floor.Shown.ToList(), 0, at);
            }
        }

        [Test]
        public void Floor_StudioAndLyric_WholeHintSequence_ForEveryLiveWord()
        {
            string[] orders = { "HHHH12", "12HHHH", "1H2HHH", "H1H2HH", "HH12HH" };
            foreach (Word v in LiveWords())
                foreach (string order in orders)
                {
                    CheckFloor(v.w, v.s, order, (f, k) => new StudioHints(v.w, v.s, v.h, true, f).Apply(k).Line, 4, "studio " + v.w + " " + order);
                    CheckFloor(v.w, v.s, order, (f, k) => new StudioHints(v.w, v.s, v.h, false, f).Apply(k).Line, 3, "studio, no voice, " + v.w + " " + order);
                    CheckFloor(v.w, v.s, order, (f, k) => new LyricHints(v.w, v.s, v.h, f).Apply(k).Line, 4, "lyric " + v.w + " " + order);
                }
        }

        [Test]
        public void Floor_PatternsShowOnlyCountedLetters_ForEveryLiveWord()
        {
            foreach (Word v in LiveWords())
            {
                var f = new HintFloor(v.w, v.s);
                var h = new StudioHints(v.w, v.s, v.h, true, f);
                for (int k = 1; k <= 4; k++)
                {
                    string line = h.Apply(k).Line;
                    int colon = line.IndexOf(": ", StringComparison.Ordinal);
                    if (h.Steps[k - 1] != "ends" && h.Steps[k - 1] != "cons") continue;
                    string[] cells = line.Substring(colon + 2).Split(' ');
                    Assert.AreEqual(v.w.Length, cells.Length, v.w + ": " + line);
                    for (int i = 0; i < cells.Length; i++)
                        if (cells[i] != "_") { Assert.AreEqual(v.w[i].ToString(), cells[i], v.w); CollectionAssert.Contains(f.Shown.ToList(), i, v.w + ": " + line); }
                }
            }
        }

        [Test]
        public void Curly_TurnsStraightApostrophesIntoTheCopysCurlyOne()
        {
            Assert.AreEqual("it" + (char)0x2019 + "s can" + (char)0x2019 + "t", TextRules.Curly("it's can't"));
            Assert.IsNull(TextRules.Curly(null));
            Assert.AreEqual("no marks", TextRules.Curly("no marks"));
        }

        [Test]
        public void LyricHint4_NeverRepeatsALetterAlreadyTold_AndKeepsTheFloor()
        {
            foreach (Word v in LiveWords())
            {
                var f = new HintFloor(v.w, v.s);
                var h = new LyricHints(v.w, v.s, v.h, f);
                h.Apply(1); h.Apply(2); h.Apply(3);
                var told = new HashSet<int>(f.Shown);
                string line = h.Apply(4).Line;
                if (line.StartsWith("it has", StringComparison.Ordinal)) StringAssert.Contains("letters", line, v.w);
                else
                {
                    string[] cells = line.Split(' ');
                    Assert.AreEqual(v.w.Length, cells.Length, v.w + ": " + line);
                    Assert.IsTrue(cells.Any(c => c != "_"), v.w + ": a pattern with no letter gives nothing: " + line);
                    for (int i = 0; i < cells.Length; i++)
                        if (cells[i] != "_") Assert.IsFalse(told.Contains(i), v.w + ": hint 4 repeats the letter at " + i + ": " + line);
                }
                Assert.GreaterOrEqual(f.Unknown, SpellingHints.MinHidden(v.w.Length), v.w);
            }
            // the tester's case: hint 2 told the first letter, so hint 4 gives the last letter alone
            var g = new HintFloor("illogical", "il-log-i-cal");
            var lh = new LyricHints("illogical", "il-log-i-cal", "x", g);
            lh.Apply(1); lh.Apply(2);
            string four = lh.Apply(4).Line;
            Assert.AreEqual("_ _ _ _ _ _ _ _ l", four, "the first letter was told by hint 2");
        }

        // ---------- copy, word for word from the web game ----------

        private static string WebFile(string rel)
        {
            var roots = new List<string>();
            string env = Environment.GetEnvironmentVariable("WT_WEB");
            if (!string.IsNullOrEmpty(env)) roots.Add(env);
            roots.Add(Path.Combine(Directory.GetCurrentDirectory(), "..", "world_tour"));
            roots.Add(Path.Combine(Directory.GetCurrentDirectory(), "world_tour"));
            foreach (string root in roots)
            {
                string p = Path.GetFullPath(Path.Combine(root, rel));
                if (File.Exists(p)) return File.ReadAllText(p);
            }
            return null;
        }

        [Test]
        public void EveryLineIsInTheWebSource()
        {
            string studio = WebFile("modes/spelling/studio.js"), lyric = WebFile("modes/spelling/lyric.js"), prefix = WebFile("modes/spelling/prefix.js"),
                venue = WebFile("screens/venue.js"), speech = WebFile("js/speech.js");
            if (studio == null || lyric == null || prefix == null || venue == null || speech == null) Assert.Ignore("world_tour not found next to the project");
            const string E = "${esc(v.h.toLowerCase())}";
            StringAssert.Contains("desk('" + Spelling3Lines.StudioDeskTag + "'", studio);
            StringAssert.Contains("<p class=\"tap\">" + Spelling3Lines.TapToHear + "</p>", studio);
            StringAssert.Contains("typedField('" + Spelling3Lines.StudioPlaceholder + "')", studio);
            StringAssert.Contains(">" + Spelling3Lines.Submit + "</button>", studio);
            StringAssert.Contains("`" + Spelling3Lines.StudioClue(E) + "`", studio);
            StringAssert.Contains("`${w.length} letters: <span class=\"pat\">", studio);
            StringAssert.Contains("`just the consonants: <span class=\"pat\">", studio);
            StringAssert.Contains("ctx.later(() => s.click(), " + (int)Math.Round(SpeechRules.FirstPlayDelay * 1000) + ")", studio);
            StringAssert.Contains("say(w, " + SpeechRules.SlowRate.ToString(System.Globalization.CultureInfo.InvariantCulture).TrimStart('0') + ")", studio);
            StringAssert.Contains("say(text, rate = " + SpeechRules.NormalRate.ToString(System.Globalization.CultureInfo.InvariantCulture).TrimStart('0') + ")", speech);
            StringAssert.Contains("<span class=\"cluelbl\">" + Spelling3Lines.VerseLabel + "</span>", lyric);
            StringAssert.Contains("typedField('" + Spelling3Lines.LyricPlaceholder + "')", lyric);
            StringAssert.Contains("`" + Spelling3Lines.Means(E) + "`", lyric);
            StringAssert.Contains("`" + Spelling3Lines.NotThatOne(E) + "`", lyric);
            StringAssert.Contains("'" + Spelling3Lines.LyricCrossedOne + "'", lyric);
            StringAssert.Contains("'" + Spelling3Lines.DownToTwo + "'", lyric);
            StringAssert.Contains("ctx.next(" + (int)Math.Round(LyricRules.NextAfterWin * 1000) + ")", lyric);
            StringAssert.Contains("<span class=\"desk-tag\">" + Spelling3Lines.MixerTag + "</span>", prefix);
            StringAssert.Contains("<p class=\"tap\">" + Spelling3Lines.DragOrTap + "</p>", prefix);
            StringAssert.Contains("'" + Spelling3Lines.BuildIt + "'", prefix);
            StringAssert.Contains("'" + Spelling3Lines.TryEach + "'", prefix);
            StringAssert.Contains("'" + Spelling3Lines.OneMeaningCrossed + "'", prefix);
            StringAssert.Contains("`" + Spelling3Lines.NiceBuild("${esc(w)}") + "`", prefix);
            StringAssert.Contains("`" + Spelling3Lines.SayItOutLoud("${esc(p + pf.root)}") + "`", prefix);
            StringAssert.Contains(Spelling3Lines.RevealBuild("${esc(pf.p)}", "${esc(pf.root)}", "${esc(w)}", "${esc(pf.means)}"), prefix);
            StringAssert.Contains(Spelling3Lines.NotRule("${esc(pf.root)}", "${esc(pf.root[0])}"), prefix);
            StringAssert.Contains(Spelling3Lines.MeansRule("${esc(pf.p)}", "${esc(pf.means)}", "${esc(pf.root)}"), prefix);
            StringAssert.Contains(Spelling3Lines.WinLine("${esc(pf.p)}", "${esc(pf.root)}", "${esc(pf.means)}", "${esc(w)}"), prefix);
            StringAssert.Contains(Spelling3Lines.RevealMeaning("${esc(pf.p)}", "${esc(pf.means)}", "${esc(w)}", E), prefix);
            StringAssert.Contains(Spelling3Lines.NotQuite("${esc(w)}", E, "${esc(pf.root)}"), prefix);
            StringAssert.Contains(Spelling3Lines.HereIsWhat("${esc(w)}", E), prefix);
            StringAssert.Contains(Spelling3Lines.Question("${esc(pf.p)}", "${esc(pf.root)}"), prefix);
            StringAssert.Contains(Spelling3Lines.ItMeans("${esc(m)}"), prefix);
            StringAssert.Contains("[" + string.Join(", ", PrefixRules.ExtraPrefixes.Select(x => "'" + x + "'")) + "]", prefix);
            StringAssert.Contains("[" + string.Join(", ", PrefixRules.ExtraMeanings.Select(x => "'" + x + "'")) + "]", prefix);
            StringAssert.Contains("[" + string.Join(", ", PrefixRules.NotFamily.Select(x => "'" + x + "'")) + "]", prefix);
            StringAssert.Contains("[-4, 3, -2, 5]", prefix);
            StringAssert.Contains("ctx.setupHints(" + PrefixRules.HintMax + ",", prefix);
            StringAssert.Contains("ctx.next(" + (int)Math.Round(PrefixRules.NextAfterWin * 1000) + ")", prefix);
            StringAssert.Contains("}, " + (int)Math.Round(PrefixRules.DropReset * 1000) + ");", prefix);
            StringAssert.Contains("not on this week" + Rsq + "s bill: ${hidden.map(m => esc(m.name)).join(' and ')}. ", venue);
            StringAssert.Contains("'prefix mixer plays on weeks built around prefixes. '", venue);
            StringAssert.Contains("'lyric writer needs sentences for the words. '", venue);
        }
    }
}
