using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using WorldTour.Core.Progress;
using WorldTour.Core.Shows;
using WorldTour.Game.Map;
using WorldTour.Game.Modes;
using WorldTour.Game.Show;
using WorldTour.Game.Ticket;
using WorldTour.Game.Ui;
using WorldTour.Game.Util;

namespace WorldTour.Game.Tests
{
    /// <summary>
    /// Tasks 3.1-3.3: the six phase 3 spelling modes (missing letters, missing vowels, fan mail, studio, lyric, prefix) in
    /// the real scenes, from the venue card through one full show with at least one miss and one hint, to the ticket, then
    /// the encore (the missed words only, as <see cref="RemixShowTests"/> does) through to the encore ticket.
    /// One Enter must count as one try: TMP_InputField's own Enter handling and the EventSystem's Submit both submit, in
    /// the same frame or one frame apart, and the field passes on only one. Any error or exception log fails the test.
    /// </summary>
    public class SpellingShowTests
    {
        const string Week = "2026-10-10";

        string _savePath, _savedOriginal;
        bool _hadOriginal;
        readonly List<string> _logs = new List<string>();
        readonly List<string> _errors = new List<string>();
        ScreenStack _stack;
        TicketScreen _ticket;

        void OnLog(string message, string stack, LogType type)
        {
            _logs.Add(message);
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errors.Add(type + ": " + message + "\n" + stack);
        }

        void OnTicket(TicketScreen t) { _ticket = t; }

        [SetUp]
        public void SetUp()
        {
            _logs.Clear();                                                  // NUnit keeps one instance for the whole class
            _errors.Clear();
            _stack = null;
            _ticket = null;
            _savePath = Path.Combine(Application.persistentDataPath, SaveBlob.Key + ".json");
            _hadOriginal = File.Exists(_savePath);
            _savedOriginal = _hadOriginal ? File.ReadAllText(_savePath) : null;
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(_savePath, SaveJson.ToText(SaveBlob.Defaults(LocalClock.TodayIso())));
            Application.logMessageReceived += OnLog;
            TicketScreen.Opened += OnTicket;
        }

        [TearDown]
        public void TearDown()
        {
            TicketScreen.Opened -= OnTicket;
            Application.logMessageReceived -= OnLog;
            if (_hadOriginal) File.WriteAllText(_savePath, _savedOriginal);
            else if (File.Exists(_savePath)) File.Delete(_savePath);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator MissingLetters_OneEnterIsOneTry_ToTheTicketAndEncore() { return TypedShow("slots"); }

        [UnityTest, Timeout(240000)]
        public IEnumerator MissingVowels_ToTheTicketAndEncore() { return TypedShow("vowels"); }

        [UnityTest, Timeout(240000)]
        public IEnumerator FanMail_WrongPickNextSongAndHint_ToTheTicketAndEncore()
        {
            ShowScreen show = null;
            yield return OpenShow("fanmail", s => show = s);
            ShowEngine engine = show.Engine;
            bool missed = false, hinted = false;
            while (!engine.Finished)
            {
                int i = engine.Index;
                FanmailMode mode = null;
                yield return Until(() => engine.Finished || (engine.Index == i && (mode = ModeAt<FanmailMode>(show, i)) != null && mode.Round != null && !engine.Resolved), 10f, "song " + (i + 1) + " never became playable");
                if (engine.Finished) break;
                FanmailRound round = mode.Round;
                if (!missed && round.Options.Count > 1)
                {
                    missed = true;
                    int wrong = Enumerable.Range(0, round.Options.Count).First(k => k != round.RightIndex);
                    Press(Choice(mode, wrong));
                    yield return null;
                    Assert.AreEqual(1, engine.Tries);
                    Assert.IsTrue(engine.Resolved, "one pick per song");
                    Press(Choice(mode, round.RightIndex));
                    yield return null;
                    Assert.AreEqual(1, engine.Tries, "a second pick does nothing");
                    Transform next = FindDeep(mode.transform, "Next", activeOnly: true);
                    Assert.IsNotNull(next, "no next song button after a wrong pick");
                    Press(next.gameObject);
                }
                else if (missed && !hinted && !engine.IsBonus && round.Options.Count > 2)
                {
                    hinted = true;
                    Press(FindDeep(mode.transform, "Hint").gameObject);
                    yield return null;
                    Assert.AreEqual(1, engine.Hints);
                    Assert.AreEqual(1, Enumerable.Range(0, round.Options.Count).Count(round.IsGone), "the hint crossed out one choice");
                    Press(Choice(mode, round.RightIndex));
                }
                else Press(Choice(mode, round.RightIndex));
                yield return Until(() => engine.Index != i || engine.Finished, 6f, "song " + (i + 1) + " never moved on");
            }
            Assert.IsTrue(missed, "no song had two choices to miss on");
            Assert.IsTrue(hinted, "no song had three choices to hint on");
            yield return EncoreToTicket(FanmailRight);
            Assert.IsEmpty(_errors, string.Join("\n", _errors));
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Studio_SpeaksTheWord_SlowSyllables_MissThenWin_ToTheTicketAndEncore()
        {
            ShowScreen show = null;
            yield return OpenShow("studio", s => show = s);
            ShowEngine engine = show.Engine;
            int play = 0;
            while (!engine.Finished)
            {
                int i = engine.Index;
                StudioMode mode = null;
                yield return Until(() => engine.Finished || (engine.Index == i && (mode = ModeAt<StudioMode>(show, i)) != null && mode.Round != null && mode.Field != null && !engine.Resolved), 10f, "song " + (i + 1) + " never became playable");
                if (engine.Finished) break;
                string word = mode.Round.Word;
                if (play == 0)
                {
                    Assert.IsTrue(mode.CanSpeak, "the Editor's logging voice speaks");
                    Assert.IsTrue(_logs.Any(l => l.StartsWith("WT_STUDIO voice=speak")));
                    yield return Until(() => _logs.Contains("WT_SPEAK word=" + word + " rate=0.80"), 3f, "the word was not spoken at rate 0.8");
                    Assert.IsNull(FindDeep(mode.transform, "Clue"), "with a voice the clue is not on screen");
                    Press(FindDeep(mode.transform, "Hint").gameObject);         // the clue
                    yield return new WaitForSecondsRealtime(.35f);
                    Press(FindDeep(mode.transform, "Hint").gameObject);         // the syllables, read slowly
                    yield return null;
                    Assert.AreEqual(2, engine.Hints);
                    Assert.IsTrue(_logs.Contains("WT_SPEAK word=" + word + " rate=0.55"), "the syllable hint did not read the word slowly");
                    // a wrong spelling: one try, the song stays open
                    yield return new WaitForSecondsRealtime(.25f);
                    mode.Field.Text = "zz";
                    EnterBothPaths(mode.Field);
                    yield return Until(() => engine.Tries == 1, 3f, "the wrong spelling did not count a try");
                    yield return null; yield return null;
                    Assert.AreEqual(1, engine.Tries, "one Enter (both submit paths) must be one try");
                    Assert.IsFalse(engine.Resolved, "one wrong spelling: try again");
                    Assert.AreEqual(1, _logs.Count(l => l.StartsWith("WT_TYPED val=zz result=wrong")));
                    yield return new WaitForSecondsRealtime(.3f);
                    mode.Field.Text = word;
                    EnterBothPaths(mode.Field);
                    yield return Until(() => engine.Resolved, 3f, "the right word did not win");
                    Assert.AreEqual(1, engine.Tries);
                    Assert.IsTrue(engine.MissedKids.Contains(engine.Current.Item.Kid), "the miss was not recorded");
                }
                else
                {
                    yield return new WaitForSecondsRealtime(.25f);
                    mode.Field.Text = word;
                    EnterBothPaths(mode.Field);
                    yield return Until(() => engine.Resolved, 3f, "song " + (i + 1) + " did not resolve");
                }
                yield return Until(() => engine.Index != i || engine.Finished, 6f, "song " + (i + 1) + " never moved on");
                play++;
            }
            yield return EncoreToTicket(StudioRight);
            Assert.IsEmpty(_errors, string.Join("\n", _errors));
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Studio_NoVoice_ShowsTheClue()
        {
            WorldTour.Game.Speech.ReadAloud.ForceNoVoice = true;
            try
            {
                ShowScreen show = null;
                yield return OpenShow("studio", s => show = s);
                StudioMode mode = null;
                yield return Until(() => (mode = ModeAt<StudioMode>(show, 0)) != null && mode.Round != null, 10f, "studio never drew");
                Assert.IsFalse(mode.CanSpeak);
                Assert.IsNotNull(FindDeep(mode.transform, "Clue"), "no voice: the clue is on screen (studio.js:18)");
                Assert.IsNull(FindDeep(mode.transform, "Record"), "no voice: no record button");
                Assert.AreEqual(3, show.Engine.MaxHints, "no voice: the clue hint is skipped");
                Assert.IsTrue(_logs.Any(l => l.StartsWith("WT_STUDIO voice=clue")));
            }
            finally { WorldTour.Game.Speech.ReadAloud.ForceNoVoice = false; }
            Assert.IsEmpty(_errors, string.Join("\n", _errors));
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Lyric_TwoWrongPicksShowTheWord_HintThenLevelUp_PrefixIsOffTheBill_ToTheTicketAndEncore()
        {
            ShowScreen show = null;
            yield return OpenShow("lyric", s => show = s, venueCheck: card =>
            {
                Assert.IsNull(FindDeep(card, "Mode_prefix", activeOnly: true), "the geography week has no prefix words: prefix mixer is off the bill");
                Transform note = FindDeep(card, "BillNote", activeOnly: true);
                Assert.IsNotNull(note, "no bill note");
                StringAssert.Contains("prefix mixer plays on weeks built around prefixes.", note.GetComponent<TMPro.TMP_Text>().text);
            });
            ShowEngine engine = show.Engine;
            bool missed = false, hinted = false;
            int typed = 0;
            while (!engine.Finished)
            {
                int i = engine.Index;
                LyricMode mode = null;
                yield return Until(() => engine.Finished || (engine.Index == i && (mode = ModeAt<LyricMode>(show, i)) != null && (mode.Choice != null || (mode.Round != null && mode.Field != null && mode.Field.Input != null)) && !engine.Resolved), 10f, "song " + (i + 1) + " never became playable");
                if (engine.Finished) break;
                if (mode.Choice == null)
                {
                    // four right in a row moved the level up: from level 2 the lyric is typed (lyric.js:73)
                    Assert.GreaterOrEqual(engine.CurrentLevel, 2);
                    typed++;
                    yield return new WaitForSecondsRealtime(.25f);
                    mode.Field.Text = mode.Round.Word;
                    EnterBothPaths(mode.Field);
                    yield return Until(() => engine.Resolved, 3f, "the typed lyric did not resolve");
                    yield return Until(() => engine.Index != i || engine.Finished, 6f, "song " + (i + 1) + " never moved on");
                    continue;
                }
                LyricChoiceRound round = mode.Choice;
                if (!missed && round.Options.Count > 2)
                {
                    missed = true;
                    var wrong = Enumerable.Range(0, round.Options.Count).Where(k => k != round.RightIndex).Take(2).ToList();
                    Press(FindDeep(mode.transform, "Choice " + wrong[0]).gameObject);
                    yield return null;
                    Assert.AreEqual(1, engine.Tries);
                    Assert.IsFalse(engine.Resolved, "one wrong pick: try again");
                    Press(FindDeep(mode.transform, "Choice " + wrong[1]).gameObject);
                    yield return null;
                    Assert.AreEqual(2, engine.Tries);
                    Assert.IsTrue(engine.Resolved, "the second wrong pick shows the word");
                    Press(FindDeep(mode.transform, "Next", activeOnly: true).gameObject);
                }
                else if (missed && !hinted && !engine.IsBonus && round.Options.Count > 2)
                {
                    // a hint crosses out one wrong word; a hinted win still counts toward the level-up
                    hinted = true;
                    Press(FindDeep(mode.transform, "Hint").gameObject);
                    yield return null;
                    Assert.AreEqual(1, engine.Hints);
                    Assert.AreEqual(0, engine.Tries, "a hint is not a try");
                    string line = _logs.LastOrDefault(l => l.StartsWith("WT_HINT mode=lyric n=1 "));
                    Assert.IsNotNull(line, "no lyric hint log");
                    Assert.IsFalse(line.EndsWith("crossed=-1"), "the first lyric hint crossed out nothing: " + line);
                    Press(FindDeep(mode.transform, "Choice " + round.RightIndex).gameObject);
                    yield return Until(() => engine.Resolved, 3f, "the hinted pick did not win");
                    Assert.AreEqual(1, engine.Hints);
                }
                else Press(FindDeep(mode.transform, "Choice " + round.RightIndex).gameObject);
                yield return Until(() => engine.Index != i || engine.Finished, 6f, "song " + (i + 1) + " never moved on");
            }
            Assert.IsTrue(missed, "no lyric song had three choices to miss on");
            Assert.IsTrue(hinted, "no lyric song had three choices to hint on");
            Assert.Greater(typed, 0, "the level never moved up to typed lyrics");
            yield return EncoreToTicket(LyricRight);
            Assert.IsEmpty(_errors, string.Join("\n", _errors));
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Prefix_WrongChipThenBuildThenMeaning_Hint_ToTheTicketAndEncore()
        {
            ShowScreen show = null;
            yield return OpenShow("prefix", s => show = s, week: "2026-10-26");
            ShowEngine engine = show.Engine;
            int play = 0;
            while (!engine.Finished)
            {
                int i = engine.Index;
                PrefixMode mode = null;
                yield return Until(() => engine.Finished || (engine.Index == i && (mode = ModeAt<PrefixMode>(show, i)) != null && mode.Round != null && !engine.Resolved), 10f, "song " + (i + 1) + " never became playable");
                if (engine.Finished) break;
                PrefixRound round = mode.Round;
                int right = round.Deal.Board.IndexOf(round.Word.prefix.p);
                if (play == 0)
                {
                    int wrong = Enumerable.Range(0, round.Deal.Board.Count).First(k => k != right);
                    Press(mode.Chips[wrong].gameObject);                         // tap to select ...
                    yield return null;
                    Assert.AreEqual(wrong, round.Selected);
                    Press(mode.Drop.gameObject);                                 // ... tap the blank to place
                    yield return null;
                    Assert.AreEqual(1, engine.Tries);
                    Assert.IsTrue(_logs.Any(l => l.StartsWith("WT_PREFIX_DROP") && l.Contains("right=false")));
                }
                else if (play == 1)
                {
                    // a hint at the build step: "try each one", counted, not a try
                    Press(FindDeep(mode.transform, "Hint").gameObject);
                    yield return null;
                    Assert.AreEqual(1, engine.Hints);
                    Assert.AreEqual(1, round.H1);
                    Assert.AreEqual(0, engine.Tries, "a hint is not a try");
                    Assert.IsTrue(_logs.Any(l => l.StartsWith("WT_HINT mode=prefix n=1 counted=true")), "no counted prefix hint");
                }
                Press(mode.Chips[right].gameObject);
                yield return null;
                Press(mode.Drop.gameObject);
                yield return null;
                Assert.AreEqual(2, round.Step, "the right prefix builds the word");
                if (play == 0)
                {
                    // the meaning question appears below the desk (the 3.2 playthrough once showed it over the header)
                    yield return new WaitForSecondsRealtime(.45f);
                    Transform step2 = FindDeep(mode.transform, "Step2", activeOnly: true), desk = FindDeep(mode.transform, "Desk");
                    Assert.IsNotNull(step2, "no meaning question");
                    Assert.Less(step2.position.y, desk.position.y, "the meaning question is not below the desk");
                }
                int m = round.Deal.Meanings.IndexOf(round.Word.prefix.means);
                Press(FindDeep(mode.transform, "Choice " + m, activeOnly: true).gameObject);
                yield return Until(() => engine.Resolved, 3f, "the right meaning did not win");
                if (play == 1) Assert.AreEqual(1, engine.Hints, "the hinted song kept its one hint");
                yield return Until(() => engine.Index != i || engine.Finished, 6f, "song " + (i + 1) + " never moved on");
                play++;
            }
            yield return EncoreToTicket(PrefixRight);
            Assert.IsEmpty(_errors, string.Join("\n", _errors));
        }

        IEnumerator TypedShow(string modeId)
        {
            ShowScreen show = null;
            yield return OpenShow(modeId, s => show = s);
            ShowEngine engine = show.Engine;
            int play = 0;
            while (!engine.Finished)
            {
                int i = engine.Index;
                MissingLettersMode mode = null;
                yield return Until(() => engine.Finished || (engine.Index == i && (mode = ModeAt<MissingLettersMode>(show, i)) != null && mode.Field != null && mode.Field.Input != null && !engine.Resolved), 10f, "song " + (i + 1) + " never became playable");
                if (engine.Finished) break;
                string word = mode.Round.Word;
                TypedField field = mode.Field;
                if (play == 0)
                {
                    // one Enter, both paths in the same frame
                    field.Text = "zz";
                    EnterBothPaths(field);
                    yield return null; yield return null;
                    Assert.AreEqual(1, engine.Tries, "one Enter (both submit paths, same frame) must be one try");
                    Assert.AreEqual(1, _logs.Count(l => l.StartsWith("WT_TYPED val=zz result=wrong")));
                    yield return new WaitForSecondsRealtime(.3f);
                    // one Enter, the two paths a frame apart
                    field.Text = "zzz";
                    field.Input.onSubmit.Invoke(field.Text);
                    yield return null;
                    ExecuteEvents.Execute(field.Input.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                    yield return null; yield return null;
                    Assert.AreEqual(2, engine.Tries, "one Enter (paths a frame apart) must be one try");
                    yield return new WaitForSecondsRealtime(.3f);
                    // an empty Enter is never a try
                    field.Text = "   ";
                    field.Input.onSubmit.Invoke(field.Text);
                    yield return null; yield return null;
                    Assert.AreEqual(2, engine.Tries, "an empty box is not a try");
                    // a hint, then the word through the check button
                    Press(FindDeep(mode.transform, "Hint").gameObject);
                    yield return null;
                    Assert.AreEqual(1, engine.Hints);
                    Assert.IsTrue(_logs.Any(l => l.StartsWith("WT_HINT mode=" + modeId + " n=1")));
                    field.Text = word.ToUpperInvariant() + " ";
                    Press(FindDeep(mode.transform, "Check").gameObject);
                    yield return Until(() => engine.Resolved, 3f, "the right word did not win");
                    Assert.AreEqual(2, engine.Tries);
                }
                else
                {
                    yield return new WaitForSecondsRealtime(.25f);
                    field.Text = word;
                    EnterBothPaths(field);
                    yield return Until(() => engine.Resolved, 3f, "song " + (i + 1) + " did not resolve");
                    Assert.AreEqual(0, engine.Tries, "a clean Enter counted a try");
                }
                yield return Until(() => engine.Index != i || engine.Finished, 6f, "song " + (i + 1) + " never moved on");
                play++;
            }
            yield return EncoreToTicket(TypedRight);
            Assert.IsEmpty(_errors, string.Join("\n", _errors));
        }

        // ------------------------------------------------------------------ the ticket and the encore

        /// <summary>
        /// The main show's ticket (a miss and a hint on it, the encore offered), the encore pressed (twice: one opens),
        /// every encore song answered right first time by <paramref name="playRight"/>, then the encore ticket.
        /// The encore plays exactly the words missed in the show.
        /// </summary>
        IEnumerator EncoreToTicket(Func<ShowScreen, int, IEnumerator> playRight)
        {
            yield return Until(() => _ticket != null && _stack.Top == _ticket, 10f, "the ticket never opened");
            TicketScreen first = _ticket;
            ShowSummary s = first.Data.Summary;
            Assert.IsFalse(s.Encore, "the first ticket is the main show's");
            Assert.GreaterOrEqual(s.HintTotal, 1, "the show had no hint");
            Assert.GreaterOrEqual(s.MissedItems.Count, 1, "the show had no miss");
            Assert.IsTrue(s.CanEncore, "a missed word should offer the encore");
            Assert.AreEqual(TicketPrimary.Encore, first.Model.Primary);
            Assert.AreEqual(s.MissedItems.Count, first.Model.MissedCount);
            Assert.AreEqual(s.MissedItems.Count, s.Setlist.Count(e => e.Tag == SetlistTag.Miss));
            List<string> missedWords = s.MissedItems.Select(x => ((WordItem)x).W).ToList();
            int showsBefore = Boot.Store.Blob.Shows.Count;
            long lifetimeBefore = Boot.Store.Blob.Fans.Lifetime;

            _ticket = null;
            Press(first.PrimaryButton.gameObject);
            Press(first.PrimaryButton.gameObject);                          // a double click must not open two encores
            ShowScreen encore = null;
            yield return Until(() => (encore = _stack.Top as ShowScreen) != null, 5f, "the encore never opened");
            Assert.IsTrue(encore.Request.Encore);
            Assert.AreEqual(missedWords.Count, encore.Engine.SongCount, "the encore replays only the missed words");
            Assert.AreEqual(3, _stack.Count, "the encore should replace the ticket (map, venue card, encore)");
            ShowEngine e2 = encore.Engine;
            var played = new List<string>();
            while (!e2.Finished)
            {
                int i = e2.Index;
                yield return Until(() => e2.Finished || (e2.Index == i && e2.Current != null && !e2.Resolved), 10f, "encore song " + (i + 1) + " never started");
                if (e2.Finished) break;
                played.Add(((WordItem)e2.Current.Item).W);
                yield return playRight(encore, i);
                Assert.IsTrue(e2.Resolved, "encore song " + (i + 1) + " did not resolve");
                Assert.AreEqual(0, e2.Tries, "encore song " + (i + 1) + " should be right first time");
                yield return Until(() => e2.Finished || e2.Index != i, 6f, "the encore did not move on from song " + (i + 1));
            }
            CollectionAssert.AreEquivalent(missedWords, played, "the encore did not play the missed words");
            Assert.AreEqual(missedWords.Count, e2.FirstTry, "every encore song right first time");
            Assert.Greater(e2.Fans, 0, "a clean encore earns fans");

            yield return Until(() => _ticket != null && _stack.Top == _ticket, 6f, "the encore ticket never opened");
            Assert.AreNotSame(first, _ticket);
            Assert.IsTrue(_ticket.Data.Summary.Encore, "the second ticket is the encore's");
            Assert.AreEqual(TicketPrimary.PlayAgain, _ticket.Model.Primary, "the encore ticket offers play again");
            Assert.AreEqual(showsBefore + 1, Boot.Store.Blob.Shows.Count, "the encore is not in the history");
            Assert.AreEqual(lifetimeBefore + e2.Fans, Boot.Store.Blob.Fans.Lifetime, "the encore's fans were not added");
        }

        IEnumerator TypedRight(ShowScreen show, int i)
        {
            ShowEngine engine = show.Engine;
            MissingLettersMode mode = null;
            yield return Until(() => engine.Index == i && (mode = ModeAt<MissingLettersMode>(show, i)) != null && mode.Field != null && mode.Field.Input != null && !engine.Resolved, 10f, "song " + (i + 1) + " never became playable");
            yield return new WaitForSecondsRealtime(.25f);
            mode.Field.Text = mode.Round.Word;
            EnterBothPaths(mode.Field);
            yield return Until(() => engine.Resolved, 3f, "song " + (i + 1) + " did not resolve");
        }

        IEnumerator StudioRight(ShowScreen show, int i)
        {
            ShowEngine engine = show.Engine;
            StudioMode mode = null;
            yield return Until(() => engine.Index == i && (mode = ModeAt<StudioMode>(show, i)) != null && mode.Round != null && mode.Field != null && !engine.Resolved, 10f, "song " + (i + 1) + " never became playable");
            yield return new WaitForSecondsRealtime(.25f);
            mode.Field.Text = mode.Round.Word;
            EnterBothPaths(mode.Field);
            yield return Until(() => engine.Resolved, 3f, "song " + (i + 1) + " did not resolve");
        }

        IEnumerator FanmailRight(ShowScreen show, int i)
        {
            ShowEngine engine = show.Engine;
            FanmailMode mode = null;
            yield return Until(() => engine.Index == i && (mode = ModeAt<FanmailMode>(show, i)) != null && mode.Round != null && !engine.Resolved, 10f, "song " + (i + 1) + " never became playable");
            Press(Choice(mode, mode.Round.RightIndex));
            yield return Until(() => engine.Resolved, 3f, "song " + (i + 1) + " did not resolve");
        }

        IEnumerator LyricRight(ShowScreen show, int i)
        {
            ShowEngine engine = show.Engine;
            LyricMode mode = null;
            yield return Until(() => engine.Index == i && (mode = ModeAt<LyricMode>(show, i)) != null && (mode.Choice != null || (mode.Round != null && mode.Field != null && mode.Field.Input != null)) && !engine.Resolved, 10f, "song " + (i + 1) + " never became playable");
            if (mode.Choice != null) Press(FindDeep(mode.transform, "Choice " + mode.Choice.RightIndex).gameObject);
            else
            {
                yield return new WaitForSecondsRealtime(.25f);
                mode.Field.Text = mode.Round.Word;
                EnterBothPaths(mode.Field);
            }
            yield return Until(() => engine.Resolved, 3f, "song " + (i + 1) + " did not resolve");
        }

        IEnumerator PrefixRight(ShowScreen show, int i)
        {
            ShowEngine engine = show.Engine;
            PrefixMode mode = null;
            yield return Until(() => engine.Index == i && (mode = ModeAt<PrefixMode>(show, i)) != null && mode.Round != null && !engine.Resolved, 10f, "song " + (i + 1) + " never became playable");
            PrefixRound round = mode.Round;
            Press(mode.Chips[round.Deal.Board.IndexOf(round.Word.prefix.p)].gameObject);
            yield return null;
            Press(mode.Drop.gameObject);
            yield return null;
            Assert.AreEqual(2, round.Step, "the right prefix builds the word");
            Press(FindDeep(mode.transform, "Choice " + round.Deal.Meanings.IndexOf(round.Word.prefix.means), activeOnly: true).gameObject);
            yield return Until(() => engine.Resolved, 3f, "song " + (i + 1) + " did not resolve");
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>What one Enter does to a focused TMP_InputField: its own key handling submits, and so does the EventSystem's Submit.</summary>
        static void EnterBothPaths(TypedField field)
        {
            field.Input.onSubmit.Invoke(field.Text);
            ExecuteEvents.Execute(field.Input.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        }

        IEnumerator OpenShow(string modeId, Action<ShowScreen> got, string week = Week, Action<Transform> venueCheck = null)
        {
            if (!Boot.Ready)
            {
                SceneManager.LoadScene("Boot");
                yield return Until(() => Boot.Ready, 30f, "Boot never became ready");
                StartCard card = UnityEngine.Object.FindFirstObjectByType<StartCard>();
                Assert.IsNotNull(card, "no start card");
                yield return null;
                Press(card.GetComponentsInChildren<StickerButton>(true).First().gameObject);
            }
            else
            {
                // Boot runs once per session (its statics), as in the game: an earlier test already booted, so go to
                // Main the way the start card does
                SceneManager.LoadScene("Main");
                yield return null;
                yield return null;
            }
            ScreenStack stack = null;
            yield return Until(() => (stack = UnityEngine.Object.FindFirstObjectByType<ScreenStack>()) != null && stack.Top is MapScreen, 30f, "the map never showed");
            _stack = stack;
            Press(FindDeep(stack.Top.transform, "Venue_coffee").gameObject);
            yield return Until(() => stack.Top is VenueCard, 5f, "the venue card never opened");
            yield return null;
            Press(FindDeep(stack.Top.transform, "Week_" + week).gameObject);
            yield return null; yield return null;
            if (venueCheck != null) venueCheck(stack.Top.transform);
            Transform row = FindDeep(stack.Top.transform, "Mode_" + modeId, activeOnly: true);
            Assert.IsNotNull(row, "no " + modeId + " row on the venue card");
            Press(row.gameObject);
            yield return Until(() => stack.Top is ShowScreen, 5f, "the show never opened");
            got((ShowScreen)stack.Top);
        }

        static GameObject Choice(FanmailMode mode, int i) { return FindDeep(mode.transform, "Choice " + i).gameObject; }

        static T ModeAt<T>(ShowScreen show, int index) where T : Component
        {
            Transform area = FindDeep(show.transform, "Song " + (index + 1), activeOnly: true);
            return area != null ? area.GetComponent<T>() : null;
        }

        static IEnumerator Until(Func<bool> done, float seconds, string failure)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!done())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail(failure);
                yield return null;
            }
        }

        static void Press(GameObject go)
        {
            var e = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(go, e, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(go, e, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(go, e, ExecuteEvents.pointerClickHandler);
        }

        static Transform FindDeep(Transform root, string name, bool activeOnly = false)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(!activeOnly))
                if (t.name == name) return t;
            return null;
        }
    }
}
