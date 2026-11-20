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
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;
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
    /// Task 4.1: the merch table and the beat maker in the real scenes, from the venue card's math sets through one full
    /// show with a miss, a hint (and, in the beat maker, a third miss that shows the worked solution) to the ticket, then the
    /// encore (the missed problems only, the same problems again) through to the encore ticket, as SpellingShowTests do.
    /// Unreadable input must never count as a miss. The beat maker adds notes by click, by Enter (keyboard) and by drag.
    /// Any error or exception log fails the test. Named to run after RemixShowTests, which must be the session's first Boot
    /// (it checks a seeded save that Boot reads once per session; NUnit runs fixtures in name order).
    /// </summary>
    public class TourMathShowTests
    {
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
            _logs.Clear();
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

        // ------------------------------------------------------------------ merch table

        [UnityTest, Timeout(240000)]
        public IEnumerator Merch_NudgeIsNotAMiss_MissThenHint_ToTheTicketAndEncore()
        {
            ShowScreen show = null;
            yield return OpenShow("merch", s => show = s);
            ShowEngine engine = show.Engine;
            Assert.IsTrue(engine.IsMath);
            Assert.AreEqual("dec", engine.Skill);
            bool missed = false;
            while (!engine.Finished)
            {
                int i = engine.Index;
                MerchMode mode = null;
                yield return Until(() => engine.Finished || (engine.Index == i && (mode = ModeAt<MerchMode>(show, i)) != null && mode.Round != null && !engine.Resolved), 10f, "song " + (i + 1) + " never became playable");
                if (engine.Finished) break;
                Problem p = mode.Problem;
                string right = TypedMathRound.KeyedAnswer(p.Answer);
                if (!missed && !engine.IsBonus)
                {
                    missed = true;
                    Assert.IsNotNull(FindDeep(mode.transform, "Prompt"), "no prompt card");
                    Assert.IsNotNull(FindDeep(mode.transform, "Item 0"), "no merch item");
                    // nothing typed: a nudge, never a miss (QUESTIONS 7)
                    mode.Pad.Press("go");
                    Assert.AreEqual(0, engine.Tries, "an empty answer is not a miss");
                    Assert.IsTrue(_logs.Any(l => l.StartsWith("WT_MATH_SUBMIT val= result=nudge")));
                    // a wrong amount: one try, the ladder's first step, the song stays open
                    Type(mode.Pad, Money.Dollars((long)p.Answer.Value.Number + 100).Substring(1));
                    mode.Pad.Press("go");
                    Assert.AreEqual(1, engine.Tries);
                    Assert.IsFalse(engine.Resolved);
                    Assert.AreEqual("", mode.Pad.Text, "a wrong answer clears the readout");
                    Assert.AreEqual(1, mode.Round.Ladder.Shown);
                    // the hint: the partial step (and the receipt, when the problem has one), costs fans, keeps it open
                    yield return new WaitForSecondsRealtime(.2f);
                    Press(FindDeep(mode.transform, "Hint").gameObject);
                    yield return null;
                    Assert.AreEqual(1, engine.Hints, "the hint did not count");
                    Assert.AreEqual(2, mode.Round.Ladder.Shown);
                    if (p.Work != null && !p.Work.IsNull) Assert.IsNotNull(FindDeep(mode.transform, "Receipt", activeOnly: true), "the partial step draws the receipt");
                    Type(mode.Pad, right);
                    mode.Pad.Press("go");
                    Assert.IsTrue(engine.Resolved, "the right amount did not win");
                    Assert.AreEqual(1, engine.Tries);
                    Assert.IsTrue(_logs.Any(l => l.StartsWith("WT_MATH_SUBMIT val=" + right + " result=right")));
                }
                else
                {
                    Type(mode.Pad, right);
                    mode.Pad.Press("go");
                    Assert.IsTrue(engine.Resolved, "song " + (i + 1) + " did not resolve");
                }
                yield return Until(() => engine.Index != i || engine.Finished, 6f, "song " + (i + 1) + " never moved on");
            }
            Assert.IsTrue(missed);
            yield return EncoreToTicket(MerchRight);
            Assert.IsEmpty(_errors, string.Join("\n", _errors));
        }

        IEnumerator MerchRight(ShowScreen show, int i)
        {
            ShowEngine engine = show.Engine;
            MerchMode mode = null;
            yield return Until(() => engine.Index == i && (mode = ModeAt<MerchMode>(show, i)) != null && mode.Round != null && !engine.Resolved, 10f, "song " + (i + 1) + " never became playable");
            Type(mode.Pad, TypedMathRound.KeyedAnswer(mode.Problem.Answer));
            mode.Pad.Press("go");
            yield return Until(() => engine.Resolved, 3f, "song " + (i + 1) + " did not resolve");
        }

        static void Type(MathKeypad pad, string text)
        {
            foreach (char c in text) pad.Press(c.ToString());
        }

        // ------------------------------------------------------------------ beat maker

        [UnityTest, Timeout(240000)]
        public IEnumerator Beat_ShortThenHintThenFill_ThreeMissesShowTheWorkedBeat_ClickKeyDrag_ToTheTicketAndEncore()
        {
            ShowScreen show = null;
            yield return OpenShow("beat", s => show = s);
            ShowEngine engine = show.Engine;
            Assert.AreEqual("frac", engine.Skill);
            int play = 0;
            bool revealed = false;
            while (!engine.Finished)
            {
                int i = engine.Index;
                BeatMode mode = null;
                yield return Until(() => engine.Finished || (engine.Index == i && (mode = ModeAt<BeatMode>(show, i)) != null && mode.Ladder != null && !engine.Resolved), 10f, "song " + (i + 1) + " never became playable");
                if (engine.Finished) break;
                yield return new WaitForSecondsRealtime(.1f);
                if (play == 0 && mode.Board != null && !mode.Board.Removable)
                {
                    // play an empty measure: not an attempt (QUESTIONS 15): no try, riff asks for a note
                    Press(mode.PlayButton.Go);
                    yield return null;
                    Assert.AreEqual(0, engine.Tries, "an empty measure is not a miss");
                    Assert.AreEqual(0, mode.Ladder.Shown, "the ladder did not move");
                    Assert.IsTrue(_logs.Contains("WT_BEAT_PLAY result=empty"));
                    // two halves: too long, a miss, the song stays open
                    yield return new WaitForSecondsRealtime(.35f);
                    BeatChip half = mode.Chips.First(c => c.D == 2);
                    Press(half.gameObject); yield return null; Press(half.gameObject); yield return null;
                    Press(mode.PlayButton.Go);
                    yield return null;
                    Assert.AreEqual(1, engine.Tries);
                    Assert.IsFalse(engine.Resolved);
                    Assert.IsTrue(_logs.Contains("WT_BEAT_PLAY result=too-long"));
                    Press(mode.UndoButton.Go); yield return new WaitForSecondsRealtime(.35f);
                    Press(mode.UndoButton.Go); yield return null;
                    Assert.AreEqual(0, mode.Board.Placed.Count);
                    yield return new WaitForSecondsRealtime(.35f);
                    Press(FindDeep(mode.transform, "Hint").gameObject);
                    yield return null;
                    Assert.AreEqual(1, engine.Hints, "the hint did not count");
                    Assert.AreEqual(2, mode.Board.Stage, "the partial step shows the grid");
                    Assert.IsNotNull(FindDeep(mode.transform, "GridNo", activeOnly: true), "the grid numbers are drawn");
                    // the three ways in: Enter on a focused block (the keyboard) and Enter on the focused note takes it back;
                    // a drag onto the measure, then undo; then a click per note to fill it
                    BeatChip first = mode.Chips[0];
                    EventSystem.current.SetSelectedGameObject(first.gameObject);
                    ExecuteEvents.Execute(first.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                    yield return null;
                    Assert.AreEqual(1, mode.Board.Placed.Count, "Enter on a block did not add it");
                    BeatNoteButton placed = mode.Notes.Last();
                    EventSystem.current.SetSelectedGameObject(placed.gameObject);
                    ExecuteEvents.Execute(placed.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                    yield return null;
                    Assert.AreEqual(0, mode.Board.Placed.Count, "Enter on a placed note did not take it back");
                    Assert.IsNotNull(EventSystem.current.currentSelectedGameObject, "the keyboard focus was dropped");
                    yield return Drag(mode.Chips[mode.Chips.Count - 1], mode);
                    yield return null;
                    Assert.AreEqual(1, mode.Board.Placed.Count, "a drop on the measure did not add the note");
                    Assert.IsTrue(_logs.Contains("WT_BEAT_ADD d=" + mode.Chips[mode.Chips.Count - 1].D + " result=added"));
                    Press(mode.UndoButton.Go);
                    yield return null;
                    Assert.AreEqual(0, mode.Board.Placed.Count, "undo did not take it back");
                    List<int> fill = mode.Board.FillSolution();
                    foreach (int d in fill) { Press(mode.Chips.First(c => c.D == d).gameObject); yield return null; }
                    Assert.AreEqual(fill.Count, mode.Board.Placed.Count);
                    // a click on a placed note takes it back too
                    BeatNoteButton last = mode.Notes.Last();
                    int ld = mode.Board.Blocks()[last.Index].D;
                    Press(last.gameObject);
                    yield return null;
                    Assert.AreEqual(fill.Count - 1, mode.Board.Placed.Count, "the note was not taken back");
                    Press(mode.Chips.First(c => c.D == ld).gameObject);
                    yield return null;
                    Assert.IsTrue(mode.Board.Filled());
                    EventSystem.current.SetSelectedGameObject(null);
                    Press(mode.PlayButton.Go);
                    yield return null;
                    Assert.IsTrue(engine.Resolved, "a full measure did not win");
                    Assert.IsTrue(_logs.Contains("WT_BEAT_PLAY result=filled"));
                }
                else if (!revealed && play >= 1 && !engine.IsBonus && mode.Board != null)
                {
                    // three wrong plays: the third shows the worked beat and the next-song button
                    revealed = true;
                    if (!mode.Board.Removable)
                    {
                        // a whole measure of halves on top of the written notes: always too long
                        BeatChip half = mode.Chips.First(c => c.D == mode.Chips.Min(x => x.D));
                        Press(half.gameObject); yield return null; Press(half.gameObject); yield return null;
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        yield return new WaitForSecondsRealtime(.35f);
                        Press(mode.PlayButton.Go);
                        yield return null;
                    }
                    Assert.AreEqual(3, engine.Tries);
                    Assert.IsTrue(engine.Resolved, "the third miss shows the answer");
                    Assert.IsTrue(mode.Board.Done && mode.Board.Filled(), "the worked solution fills the measure");
                    Assert.AreEqual(3, mode.Ladder.Shown);
                    Transform next = FindDeep(mode.transform, "Next", activeOnly: true);
                    Assert.IsNotNull(next, "no next song button after the worked solution");
                    Press(next.gameObject);
                }
                else yield return BeatRight(show, i);
                yield return Until(() => engine.Index != i || engine.Finished, 8f, "song " + (i + 1) + " never moved on");
                play++;
            }
            Assert.IsTrue(revealed, "no song had the three-miss reveal");
            yield return EncoreToTicket(BeatRight);
            Assert.IsEmpty(_errors, string.Join("\n", _errors));
        }

        IEnumerator BeatRight(ShowScreen show, int i)
        {
            ShowEngine engine = show.Engine;
            BeatMode mode = null;
            yield return Until(() => engine.Index == i && (mode = ModeAt<BeatMode>(show, i)) != null && mode.Ladder != null && !engine.Resolved, 10f, "song " + (i + 1) + " never became playable");
            if (mode.Board == null)
            {
                Type(mode.Pad, TypedMathRound.KeyedAnswer(mode.Problem.Answer));          // typed add / subtract (level 2-3)
                mode.Pad.Press("go");
            }
            else if (mode.Board.Removable)
            {
                foreach (int k in BeatBoard.CheapestCut(mode.Board.Pre, mode.Problem.Answer.Value).OrderByDescending(x => x))
                {
                    BeatNoteButton b = mode.Notes.First(n => { BeatBlock bl = mode.Board.Blocks()[n.Index]; return bl.Pre && bl.Ref == k; });
                    Press(b.gameObject);
                    yield return null;
                }
                Press(mode.PlayButton.Go);
            }
            else
            {
                foreach (int d in mode.Board.FillSolution()) { Press(mode.Chips.First(c => c.D == d).gameObject); yield return null; }
                Press(mode.PlayButton.Go);
            }
            yield return Until(() => engine.Resolved, 3f, "song " + (i + 1) + " did not resolve");
        }

        IEnumerator Drag(BeatChip chip, BeatMode mode)
        {
            RectTransform track = (RectTransform)FindDeep(mode.transform, "Track");
            Vector2 target = RectTransformUtility.WorldToScreenPoint(null, track.TransformPoint(track.rect.center));
            var e = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = target };
            chip.OnBeginDrag(e);
            yield return null;
            chip.OnDrag(e);
            yield return null;
            chip.OnEndDrag(e);
        }

        // ------------------------------------------------------------------ the ticket and the encore

        IEnumerator EncoreToTicket(Func<ShowScreen, int, IEnumerator> playRight)
        {
            yield return Until(() => _ticket != null && _stack.Top == _ticket, 10f, "the ticket never opened");
            TicketScreen first = _ticket;
            ShowSummary s = first.Data.Summary;
            Assert.IsFalse(s.Encore, "the first ticket is the main show's");
            Assert.IsTrue(s.IsMath);
            Assert.GreaterOrEqual(s.HintTotal, 1, "the show had no hint");
            Assert.GreaterOrEqual(s.MissedItems.Count, 1, "the show had no miss");
            Assert.IsTrue(s.CanEncore, "a missed problem should offer the encore");
            Assert.AreEqual(TicketPrimary.Encore, first.Model.Primary);
            Assert.IsTrue(first.Model.Setlist.All(x => x.IsMath), "math setlist lines are drawn as math");
            List<string> missed = s.MissedItems.Select(x => x.Kid).ToList();
            int showsBefore = Boot.Store.Blob.Shows.Count;
            long lifetimeBefore = Boot.Store.Blob.Fans.Lifetime;

            _ticket = null;
            Press(first.PrimaryButton.gameObject);
            Press(first.PrimaryButton.gameObject);                          // a double click must not open two encores
            ShowScreen encore = null;
            yield return Until(() => (encore = _stack.Top as ShowScreen) != null, 5f, "the encore never opened");
            Assert.IsTrue(encore.Request.Encore);
            Assert.AreEqual(missed.Count, encore.Engine.SongCount, "the encore replays only the missed problems");
            Assert.AreEqual(3, _stack.Count, "the encore should replace the ticket (map, venue card, encore)");
            ShowEngine e2 = encore.Engine;
            var played = new List<string>();
            while (!e2.Finished)
            {
                int i = e2.Index;
                yield return Until(() => e2.Finished || (e2.Index == i && e2.Current != null && !e2.Resolved), 10f, "encore song " + (i + 1) + " never started");
                if (e2.Finished) break;
                played.Add(e2.Current.Item.Kid);
                yield return playRight(encore, i);
                Assert.IsTrue(e2.Resolved, "encore song " + (i + 1) + " did not resolve");
                Assert.AreEqual(0, e2.Tries, "encore song " + (i + 1) + " should be right first time");
                yield return Until(() => e2.Finished || e2.Index != i, 8f, "the encore did not move on from song " + (i + 1));
            }
            CollectionAssert.AreEquivalent(missed, played, "the encore did not play the missed problems");
            Assert.AreEqual(missed.Count, e2.FirstTry, "every encore song right first time");
            Assert.Greater(e2.Fans, 0, "a clean encore earns fans");

            yield return Until(() => _ticket != null && _stack.Top == _ticket, 8f, "the encore ticket never opened");
            Assert.AreNotSame(first, _ticket);
            Assert.IsTrue(_ticket.Data.Summary.Encore, "the second ticket is the encore's");
            Assert.AreEqual(TicketPrimary.PlayAgain, _ticket.Model.Primary, "the encore ticket offers play again");
            Assert.AreEqual(showsBefore + 1, Boot.Store.Blob.Shows.Count, "the encore is not in the history");
            Assert.AreEqual(lifetimeBefore + e2.Fans, Boot.Store.Blob.Fans.Lifetime, "the encore's fans were not added");
        }

        // ------------------------------------------------------------------ helpers

        IEnumerator OpenShow(string modeId, Action<ShowScreen> got)
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
                SceneManager.LoadScene("Main");
                yield return null;
                yield return null;
            }
            ScreenStack stack = null;
            yield return Until(() => (stack = UnityEngine.Object.FindFirstObjectByType<ScreenStack>()) != null && stack.Top is MapScreen, 30f, "the map never showed");
            _stack = stack;
            Press(FindDeep(stack.Top.transform, "Venue_coffee").gameObject);
            yield return Until(() => stack.Top is VenueCard, 5f, "the venue card never opened");
            yield return null; yield return null;
            Assert.IsNotNull(FindDeep(stack.Top.transform, "MathHeading", activeOnly: true), "no math sets on the venue card");
            Transform row = FindDeep(stack.Top.transform, "Mode_" + modeId, activeOnly: true);
            Assert.IsNotNull(row, "no " + modeId + " row on the venue card");
            Assert.IsNotNull(FindDeep(row, "Level", activeOnly: true), "the math row shows its level");
            Press(row.gameObject);
            yield return Until(() => stack.Top is ShowScreen, 5f, "the show never opened");
            got((ShowScreen)stack.Top);
        }

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
