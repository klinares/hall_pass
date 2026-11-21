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
    /// Tasks 4.2 and 4.3: stage designer, pack the truck, ticket sales, tour budget and soundcheck in the real scenes, from
    /// the venue card's math sets through one full show each with a press that isn't an attempt (where the mode has one),
    /// a miss and a hint, to the ticket, then the encore (the missed problems only) through to the encore ticket, as
    /// TourMathShowTests does for 4.1. The builders are played through their real controls: the stage by a click on a grid
    /// point and by the arrow keys, the truck by pressing floor spots and dragging a crate from the pile. Pack the truck
    /// starts at level 2 (so the builder comes up) and tour budget at level 3 (the story choices). Any error or exception
    /// log fails the test. Named to run after RemixShowTests, which must be the session's first Boot.
    /// </summary>
    public class TourMathBuilderShowTests
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

        string _seedSkill;
        int _seedLevel;

        void Seed(string skill, int level)
        {
            _seedSkill = skill; _seedLevel = level;
            SaveBlob b = SaveBlob.Defaults(LocalClock.TodayIso());
            if (skill != null) b.Skills[skill] = new SkillRecord { Level = level };
            File.WriteAllText(_savePath, SaveJson.ToText(b));
        }

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
            Seed(null, 1);
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

        // ------------------------------------------------------------------ stage designer

        [UnityTest, Timeout(240000)]
        public IEnumerator Stage_SetBeforeMovingIsNotATry_ClickMiss_Hint_ArrowKeys_Reveal_ToTheTicketAndEncore()
        {
            ShowScreen show = null;
            yield return OpenShow("stage", s => show = s);
            ShowEngine engine = show.Engine;
            Assert.AreEqual("xy", engine.Skill);
            int play = 0;
            bool revealed = false;
            while (!engine.Finished)
            {
                int i = engine.Index;
                StageMode mode = null;
                yield return Until(() => engine.Finished || (engine.Index == i && (mode = ModeAt<StageMode>(show, i)) != null && mode.Game != null && !engine.Resolved), 10f, "song " + (i + 1) + " never became playable");
                if (engine.Finished) break;
                yield return new WaitForSecondsRealtime(.1f);
                StageGame g = mode.Game;
                if (play == 0)
                {
                    // "set" before anything is placed: not an attempt (QUESTIONS 15, answer 15)
                    if (g.Setup.Read) yield return Tap(FindDeep(mode.transform, "Set").gameObject);
                    else ExecuteEvents.Execute(mode.Plot.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                    yield return null;
                    Assert.AreEqual(0, engine.Tries, "set before moving the light is not a miss");
                    Assert.AreEqual(0, g.Ladder.Shown, "the ladder did not move");
                    Assert.IsTrue(_logs.Any(l => l.StartsWith("WT_STAGE_SET got=none result=needinput")));
                    // a wrong point (a click on the grid, or the pickers): one try
                    GridPt a = g.Setup.Answer, wrong = new GridPt(a.X == g.Setup.Max ? a.X - 1 : a.X + 1, a.Y);
                    yield return Place(mode, wrong, click: true);
                    yield return Tap(FindDeep(mode.transform, "Set").gameObject);
                    Assert.AreEqual(1, engine.Tries);
                    Assert.IsFalse(engine.Resolved);
                    yield return new WaitForSecondsRealtime(.2f);
                    yield return Tap(FindDeep(mode.transform, "Hint").gameObject);
                    Assert.AreEqual(1, engine.Hints, "the hint did not count");
                    Assert.AreEqual(2, g.Ladder.Shown);
                    // answer 16 (intended difference 29): a hint-2 route that would end on the answer is not drawn, and an
                    // answer on the bottom line gets no big number. Both depend on the dealt problem (y = 0 about 1 in 7).
                    GridPt from2 = g.Setup.Start ?? new GridPt(0, 0);
                    GridPt end2 = g.Setup.Read ? new GridPt(a.X, 0) : new GridPt(a.X, from2.Y);
                    if (end2.Equals(a))
                    {
                        Assert.IsNull(g.Scene().Path, "no hint-2 route onto the answer (answer 16)");
                        Assert.IsNull(g.Scene().Highlight, "no big number onto the answer (answer 16)");
                    }
                    else
                    {
                        Assert.IsNotNull(g.Scene().Path, "the partial step draws the route");
                        Assert.AreEqual(a.Y == 0 ? (int?)null : a.X, g.Scene().Highlight);
                    }
                    yield return Place(mode, a, click: false);                 // the keyboard: arrows (or picker arrows)
                    if (g.Setup.Read) yield return Tap(FindDeep(mode.transform, "Set").gameObject);
                    else ExecuteEvents.Execute(mode.Plot.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                    yield return null;
                    Assert.IsTrue(engine.Resolved, "the right point did not win");
                    Assert.AreEqual(1, engine.Tries);
                }
                else if (!revealed && play >= 1 && !engine.IsBonus)
                {
                    revealed = true;
                    GridPt a = g.Setup.Answer, wrong = new GridPt(a.X == g.Setup.Max ? a.X - 1 : a.X + 1, a.Y);
                    yield return Place(mode, wrong, click: true);
                    for (int k = 0; k < 3; k++) { yield return new WaitForSecondsRealtime(.35f); yield return Tap(FindDeep(mode.transform, "Set").gameObject); }
                    Assert.AreEqual(3, engine.Tries);
                    Assert.IsTrue(engine.Resolved, "the third miss shows the answer");
                    Assert.IsTrue(g.Done);
                    Assert.AreEqual(a, g.Scene().Path.End, "the worked route ends on the answer");
                    Transform next = FindDeep(mode.transform, "Next", activeOnly: true);
                    Assert.IsNotNull(next, "no next song button after the worked solution");
                    Press(next.gameObject);
                }
                else yield return StageRight(show, i);
                yield return Until(() => engine.Index != i || engine.Finished, 8f, "song " + (i + 1) + " never moved on");
                play++;
            }
            Assert.IsTrue(revealed);
            yield return EncoreToTicket(StageRight);
            Assert.IsEmpty(_errors, string.Join("\n", _errors));
        }

        /// <summary>Puts her light on a point: a click on the grid, or the arrow keys (move events on the focused stage); read mode uses the pickers.</summary>
        IEnumerator Place(StageMode mode, GridPt to, bool click)
        {
            StageGame g = mode.Game;
            if (g.Setup.Read)
            {
                foreach (bool across in new[] { true, false })
                {
                    Transform row = FindDeep(mode.transform, "Picker " + (across ? StageLines.Across : StageLines.Up));
                    int target = across ? to.X : to.Y;
                    for (int guard = 0; guard < 30 && (across ? g.PickX : g.PickY) != target; guard++)
                    {
                        int? cur = across ? g.PickX : g.PickY;
                        bool up = cur == null ? target > 0 : cur < target;
                        if (click) yield return Tap(FindDeep(row, up ? "Plus" : "Minus").gameObject);
                        else
                        {
                            StagePicker pk = row.GetComponentInChildren<StagePicker>();
                            EventSystem.current.SetSelectedGameObject(pk.gameObject);
                            ExecuteEvents.Execute(pk.gameObject, new AxisEventData(EventSystem.current) { moveDir = up ? MoveDirection.Up : MoveDirection.Down }, ExecuteEvents.moveHandler);
                        }
                        yield return null;
                    }
                    Assert.AreEqual(target, across ? g.PickX : g.PickY, "the picker did not reach " + target);
                }
                yield break;
            }
            if (click)
            {
                Vector2 at = mode.Plot.ScreenPoint(to.X, to.Y);
                var e = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = at, pressPosition = at };
                ExecuteEvents.Execute(mode.Plot.gameObject, e, ExecuteEvents.pointerDownHandler);
                yield return null;
                Assert.AreEqual(to, g.Ghost, "a click on the grid point did not put the light there");
                yield break;
            }
            EventSystem.current.SetSelectedGameObject(mode.Plot.gameObject);
            for (int guard = 0; guard < 40 && !(g.Ghost.HasValue && g.Ghost.Value.Equals(to)); guard++)
            {
                GridPt from = g.Ghost ?? g.Setup.Start ?? new GridPt(0, 0);
                MoveDirection d = from.X < to.X ? MoveDirection.Right : from.X > to.X ? MoveDirection.Left : from.Y < to.Y ? MoveDirection.Up : from.Y > to.Y ? MoveDirection.Down
                    : MoveDirection.Right;                                   // already there but not yet out: step off and back
                if (!g.Ghost.HasValue && from.Equals(to)) d = to.X < g.Setup.Max ? MoveDirection.Right : MoveDirection.Left;
                ExecuteEvents.Execute(mode.Plot.gameObject, new AxisEventData(EventSystem.current) { moveDir = d }, ExecuteEvents.moveHandler);
                yield return null;
            }
            Assert.AreEqual(to, g.Ghost, "the arrow keys did not bring the light to " + to);
        }

        IEnumerator StageRight(ShowScreen show, int i)
        {
            ShowEngine engine = show.Engine;
            StageMode mode = null;
            yield return Until(() => engine.Index == i && (mode = ModeAt<StageMode>(show, i)) != null && mode.Game != null && !engine.Resolved, 10f, "song " + (i + 1) + " never became playable");
            yield return Place(mode, mode.Game.Setup.Answer, click: true);
            yield return Tap(FindDeep(mode.transform, "Set").gameObject);
            yield return Until(() => engine.Resolved, 3f, "song " + (i + 1) + " did not resolve");
        }

        // ------------------------------------------------------------------ pack the truck

        [UnityTest, Timeout(240000)]
        public IEnumerator Truck_EmptyFloorIsNotATry_UnevenMiss_Hint_SpotsAndDrag_TypedToo_ToTheTicketAndEncore()
        {
            Seed("vol", 2);                                                    // level 2: the builder and l x w x h
            ShowScreen show = null;
            yield return OpenShow("truck", s => show = s);
            ShowEngine engine = show.Engine;
            Assert.AreEqual("vol", engine.Skill);
            bool built = false, typed = false;
            while (!engine.Finished)
            {
                int i = engine.Index;
                TruckMode mode = null;
                yield return Until(() => engine.Finished || (engine.Index == i && (mode = ModeAt<TruckMode>(show, i)) != null && (mode.Build != null || mode.Round != null) && !engine.Resolved), 10f, "song " + (i + 1) + " never became playable");
                if (engine.Finished) break;
                yield return new WaitForSecondsRealtime(.1f);
                if (!built && mode.Build != null && !engine.IsBonus)
                {
                    built = true;
                    TruckBuild b = mode.Build;
                    yield return Tap(FindDeep(mode.transform, "Load").gameObject);
                    Assert.AreEqual(0, engine.Tries, "an empty floor is not a miss");
                    Assert.AreEqual(0, b.Ladder.Shown);
                    Assert.IsTrue(_logs.Any(l => l.StartsWith("WT_TRUCK_LOAD total=0 even=true result=empty")));
                    // the right count piled on one spot (or as near as the roof allows): uneven, a miss
                    for (int k = 0; k < Math.Min(b.Target, b.MaxH); k++) { Press(Spot(mode, 0, 0)); yield return null; }
                    Assert.AreEqual(Math.Min(b.Target, b.MaxH), b.Total);
                    yield return new WaitForSecondsRealtime(.35f);
                    yield return Tap(FindDeep(mode.transform, "Load").gameObject);
                    Assert.AreEqual(1, engine.Tries);
                    Assert.IsFalse(engine.Resolved);
                    yield return Tap(FindDeep(mode.transform, "Clear").gameObject);
                    Assert.AreEqual(0, b.Total);
                    yield return new WaitForSecondsRealtime(.35f);
                    yield return Tap(FindDeep(mode.transform, "Hint").gameObject);
                    Assert.AreEqual(1, engine.Hints, "the hint did not count");
                    Assert.AreEqual(2, b.Ladder.Shown);
                    // a crate dragged from the pile onto a spot, then undo
                    yield return DragCrate(mode, 0, 0);
                    Assert.AreEqual(1, b.Total, "a crate dropped on a spot did not land");
                    yield return Tap(FindDeep(mode.transform, "Undo").gameObject);
                    Assert.AreEqual(0, b.Total);
                    // Enter on a focused spot stacks too
                    GameObject s00 = Spot(mode, 0, 0);
                    EventSystem.current.SetSelectedGameObject(s00);
                    ExecuteEvents.Execute(s00, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                    yield return null;
                    Assert.AreEqual(1, b.Total, "Enter on a spot did not stack a crate");
                    yield return Tap(FindDeep(mode.transform, "Undo").gameObject);
                    yield return FillTruck(mode);
                    yield return Tap(FindDeep(mode.transform, "Load").gameObject);
                    Assert.IsTrue(engine.Resolved, "a full, even load did not win");
                }
                else if (!typed && mode.Round != null && !engine.IsBonus)
                {
                    typed = true;
                    int before = engine.Tries;
                    mode.Pad.Press("go");
                    Assert.AreEqual(before, engine.Tries, "an empty answer is not a miss");
                    yield return TypedRight(mode.Pad, mode.Problem);
                    Assert.IsTrue(engine.Resolved);
                }
                else yield return TruckRight(show, i);
                // until the builder has come up, hold the skill at level 2 (four right in a row would move it to level 3,
                // which has no builder; the show's seed is the clock, so the kinds are random). Fix round 2: this flaked.
                if (!built) { SkillRecord k = Boot.Store.Blob.Skills["vol"]; k.Level = 2; k.Run = 0; }
                yield return Until(() => engine.Index != i || engine.Finished, 8f, "song " + (i + 1) + " never moved on");
            }
            Assert.IsTrue(built, "no builder problem came up at level 2");
            yield return EncoreToTicket(TruckRight);
            Assert.IsEmpty(_errors, string.Join("\n", _errors));
        }

        GameObject Spot(TruckMode mode, int x, int y) { return FindDeep(mode.transform, "Spot " + (x + 1) + "," + (y + 1)).gameObject; }

        IEnumerator FillTruck(TruckMode mode)
        {
            TruckBuild b = mode.Build;
            for (int z = 0; z < b.Height; z++)
                for (int y = 0; y < b.W; y++)
                    for (int x = 0; x < b.L; x++) { Press(Spot(mode, x, y)); yield return null; }
            Assert.AreEqual(b.Target, b.Total);
            Assert.IsTrue(b.Even);
        }

        IEnumerator DragCrate(TruckMode mode, int x, int y)
        {
            DragChip chip = FindDeep(mode.transform, "Pile").GetComponentInChildren<DragChip>();
            RectTransform spot = (RectTransform)Spot(mode, x, y).transform;
            Vector2 target = RectTransformUtility.WorldToScreenPoint(null, spot.TransformPoint(spot.rect.center));
            var e = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = target };
            chip.OnBeginDrag(e); yield return null;
            chip.OnDrag(e); yield return null;
            chip.OnEndDrag(e); yield return null;
        }

        IEnumerator TruckRight(ShowScreen show, int i)
        {
            ShowEngine engine = show.Engine;
            TruckMode mode = null;
            yield return Until(() => engine.Index == i && (mode = ModeAt<TruckMode>(show, i)) != null && (mode.Build != null || mode.Round != null) && !engine.Resolved, 10f, "song " + (i + 1) + " never became playable");
            if (mode.Build != null) { yield return FillTruck(mode); yield return Tap(FindDeep(mode.transform, "Load").gameObject); }
            else yield return TypedRight(mode.Pad, mode.Problem);
            yield return Until(() => engine.Resolved, 3f, "song " + (i + 1) + " did not resolve");
        }

        // ------------------------------------------------------------------ ticket sales

        [UnityTest, Timeout(240000)]
        public IEnumerator Tickets_EmptyIsNotAMiss_Miss_HintDrawsThePaper_ToTheTicketAndEncore()
        {
            ShowScreen show = null;
            yield return OpenShow("tickets", s => show = s);
            ShowEngine engine = show.Engine;
            Assert.AreEqual("md", engine.Skill);
            bool missed = false;
            while (!engine.Finished)
            {
                int i = engine.Index;
                TicketsMode mode = null;
                yield return Until(() => engine.Finished || (engine.Index == i && (mode = ModeAt<TicketsMode>(show, i)) != null && mode.Round != null && !engine.Resolved), 10f, "song " + (i + 1) + " never became playable");
                if (engine.Finished) break;
                if (!missed && !engine.IsBonus)
                {
                    missed = true;
                    Assert.IsNotNull(FindDeep(mode.transform, "Letterboard"), "no letterboard");
                    mode.Pad.Press("go");
                    Assert.AreEqual(0, engine.Tries, "an empty answer is not a miss");
                    Type(mode.Pad, ((long)mode.Problem.Answer.Value.Number + 1).ToString());
                    mode.Pad.Press("go");
                    Assert.AreEqual(1, engine.Tries);
                    Assert.IsFalse(engine.Resolved);
                    yield return new WaitForSecondsRealtime(.2f);
                    yield return Tap(FindDeep(mode.transform, "Hint").gameObject);
                    Assert.AreEqual(1, engine.Hints);
                    Assert.AreEqual(2, mode.Round.Ladder.Shown);
                    Assert.IsNotNull(mode.Work, "the partial step draws the worked paper");
                    Assert.IsNotNull(FindDeep(mode.transform, "Paper", activeOnly: true));
                    yield return TypedRight(mode.Pad, mode.Problem);
                    Assert.IsTrue(engine.Resolved);
                }
                else yield return TicketsRight(show, i);
                yield return Until(() => engine.Index != i || engine.Finished, 8f, "song " + (i + 1) + " never moved on");
            }
            Assert.IsTrue(missed);
            yield return EncoreToTicket(TicketsRight);
            Assert.IsEmpty(_errors, string.Join("\n", _errors));
        }

        IEnumerator TicketsRight(ShowScreen show, int i)
        {
            ShowEngine engine = show.Engine;
            TicketsMode mode = null;
            yield return Until(() => engine.Index == i && (mode = ModeAt<TicketsMode>(show, i)) != null && mode.Round != null && !engine.Resolved, 10f, "song " + (i + 1) + " never became playable");
            yield return TypedRight(mode.Pad, mode.Problem);
            yield return Until(() => engine.Resolved, 3f, "song " + (i + 1) + " did not resolve");
        }

        // ------------------------------------------------------------------ tour budget

        [UnityTest, Timeout(240000)]
        public IEnumerator Budget_WrongExpressionIsAMiss_HintPicksIt_EmptyIsNot_ToTheTicketAndEncore()
        {
            Seed("ops", 3);                                                    // level 3: the story choices
            ShowScreen show = null;
            yield return OpenShow("budget", s => show = s);
            ShowEngine engine = show.Engine;
            Assert.AreEqual("ops", engine.Skill);
            bool missed = false;
            while (!engine.Finished)
            {
                int i = engine.Index;
                BudgetMode mode = null;
                yield return Until(() => engine.Finished || (engine.Index == i && (mode = ModeAt<BudgetMode>(show, i)) != null && mode.Round != null && !engine.Resolved), 10f, "song " + (i + 1) + " never became playable");
                if (engine.Finished) break;
                if (!missed && !engine.IsBonus && mode.Game.Story)
                {
                    missed = true;
                    Assert.IsFalse(mode.Pad.gameObject.activeInHierarchy, "the keypad waits for the expression");
                    int wrong = Enumerable.Range(0, mode.Problem.Choices.Count).First(k => !mode.Problem.Choices[k].Correct);
                    yield return Tap(FindDeep(mode.transform, "Choice " + wrong).gameObject);
                    Assert.AreEqual(1, engine.Tries);
                    Assert.IsFalse(mode.Game.Picked);
                    yield return new WaitForSecondsRealtime(.2f);
                    yield return Tap(FindDeep(mode.transform, "Hint").gameObject);
                    Assert.AreEqual(1, engine.Hints);
                    // after the miss (step 1), the bulb gives step 2, which names the expression and picks it
                    Assert.AreEqual(2, mode.Round.Ladder.Shown);
                    Assert.IsTrue(mode.Game.Picked, "hint 2 names the expression and picks it");
                    Assert.IsTrue(mode.Pad.gameObject.activeInHierarchy, "the keypad shows once it's picked");
                    Assert.AreEqual(2, BudgetRound.Sheet(mode.Problem, 2).Count);
                    mode.Pad.Press("go");
                    Assert.AreEqual(1, engine.Tries, "an empty answer is not a miss");
                    yield return TypedRight(mode.Pad, mode.Problem);
                    Assert.IsTrue(engine.Resolved);
                }
                else yield return BudgetRight(show, i);
                yield return Until(() => engine.Index != i || engine.Finished, 8f, "song " + (i + 1) + " never moved on");
            }
            Assert.IsTrue(missed);
            yield return EncoreToTicket(BudgetRight);
            Assert.IsEmpty(_errors, string.Join("\n", _errors));
        }

        IEnumerator BudgetRight(ShowScreen show, int i)
        {
            ShowEngine engine = show.Engine;
            BudgetMode mode = null;
            yield return Until(() => engine.Index == i && (mode = ModeAt<BudgetMode>(show, i)) != null && mode.Round != null && !engine.Resolved, 10f, "song " + (i + 1) + " never became playable");
            if (mode.Game.Story && !mode.Game.Picked)
            {
                yield return Tap(FindDeep(mode.transform, "Choice " + mode.Game.RightIndex).gameObject);
                Assert.IsTrue(mode.Game.Picked);
            }
            yield return TypedRight(mode.Pad, mode.Problem);
            yield return Until(() => engine.Resolved, 3f, "song " + (i + 1) + " did not resolve");
        }

        // ------------------------------------------------------------------ soundcheck

        [UnityTest, Timeout(240000)]
        public IEnumerator Soundcheck_WrongPickMiss_HintCrossesOne_BonusInTime_ToTheTicketAndEncore()
        {
            ShowScreen show = null;
            yield return OpenShow("soundcheck", s => show = s);
            ShowEngine engine = show.Engine;
            Assert.AreEqual("flu", engine.Skill);
            bool missed = false, bonus = false;
            while (!engine.Finished)
            {
                int i = engine.Index;
                SoundcheckMode mode = null;
                yield return Until(() => engine.Finished || (engine.Index == i && (mode = ModeAt<SoundcheckMode>(show, i)) != null && mode.Round != null && !engine.Resolved), 10f, "song " + (i + 1) + " never became playable");
                if (engine.Finished) break;
                SoundcheckRound r = mode.Round;
                if (!missed && !engine.IsBonus)
                {
                    missed = true;
                    Assert.IsTrue(r.Meter.Running, "the meter runs");
                    int wrong = Enumerable.Range(0, mode.Problem.Choices.Count).First(k => !mode.Problem.Choices[k].Correct);
                    yield return Tap(FindDeep(mode.transform, "Choice " + wrong).gameObject);
                    Assert.AreEqual(1, engine.Tries);
                    Assert.IsTrue(r.Meter.Running, "a miss doesn't stop the meter");
                    yield return new WaitForSecondsRealtime(.2f);
                    yield return Tap(FindDeep(mode.transform, "Hint").gameObject);
                    Assert.AreEqual(1, engine.Hints);
                    // after the miss (step 1), the bulb gives step 2: one more wrong choice crossed out
                    Assert.AreEqual(2, r.Ladder.Shown);
                    Assert.AreEqual(2, Enumerable.Range(0, mode.Problem.Choices.Count).Count(r.IsOff), "the miss and the hint each took one choice away");
                    Assert.IsFalse(r.IsOff(r.RightIndex), "hint 2 never crosses out the right one");
                    yield return Tap(FindDeep(mode.transform, "Choice " + r.RightIndex).gameObject);
                    Assert.IsTrue(engine.Resolved);
                    Assert.IsTrue(_logs.Any(l => l.StartsWith("WT_SOUNDCHECK_PICK i=" + r.RightIndex + " result=right bonus=300")), "right in time earns the bonus");
                }
                else
                {
                    if (!bonus && !engine.IsBonus)
                    {
                        bonus = true;
                        long before = engine.Fans;
                        yield return SoundcheckRight(show, i);
                        Assert.Greater(engine.Fans - before, 300, "the bonus fans are on top of the song's");
                    }
                    else yield return SoundcheckRight(show, i);
                }
                yield return Until(() => engine.Index != i || engine.Finished, 8f, "song " + (i + 1) + " never moved on");
            }
            Assert.IsTrue(missed && bonus);
            yield return EncoreToTicket(SoundcheckRight);
            Assert.IsEmpty(_errors, string.Join("\n", _errors));
        }

        IEnumerator SoundcheckRight(ShowScreen show, int i)
        {
            ShowEngine engine = show.Engine;
            SoundcheckMode mode = null;
            yield return Until(() => engine.Index == i && (mode = ModeAt<SoundcheckMode>(show, i)) != null && mode.Round != null && !engine.Resolved, 10f, "song " + (i + 1) + " never became playable");
            yield return Tap(FindDeep(mode.transform, "Choice " + mode.Round.RightIndex).gameObject);
            yield return Until(() => engine.Resolved, 3f, "song " + (i + 1) + " did not resolve");
        }

        // ------------------------------------------------------------------ shared

        IEnumerator TypedRight(MathKeypad pad, Problem p)
        {
            Type(pad, TypedMathRound.KeyedAnswer(p.Answer));
            pad.Press("go");
            yield return null;
        }

        static void Type(MathKeypad pad, string text)
        {
            foreach (char c in text) pad.Press(c.ToString());
        }

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
            yield return Until(() => _ticket != null && _stack.Top == _ticket, 8f, "the encore ticket never opened");
            Assert.AreNotSame(first, _ticket);
            Assert.IsTrue(_ticket.Data.Summary.Encore, "the second ticket is the encore's");
            Assert.AreEqual(TicketPrimary.PlayAgain, _ticket.Model.Primary, "the encore ticket offers play again");
            Assert.AreEqual(showsBefore + 1, Boot.Store.Blob.Shows.Count, "the encore is not in the history");
            Assert.AreEqual(lifetimeBefore + e2.Fans, Boot.Store.Blob.Fans.Lifetime, "the encore's fans were not added");
        }

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
                // Boot read the save once; a later fixture's seeded level goes in through the store itself
                SceneManager.LoadScene("Main");
                yield return null;
                yield return null;
            }
            ReloadSeed();
            ScreenStack stack = null;
            yield return Until(() => (stack = UnityEngine.Object.FindFirstObjectByType<ScreenStack>()) != null && stack.Top is MapScreen, 30f, "the map never showed");
            _stack = stack;
            Press(FindDeep(stack.Top.transform, "Venue_coffee").gameObject);
            yield return Until(() => stack.Top is VenueCard, 5f, "the venue card never opened");
            yield return null; yield return null;
            Transform row = FindDeep(stack.Top.transform, "Mode_" + modeId, activeOnly: true);
            Assert.IsNotNull(row, "no " + modeId + " row on the venue card");
            Press(row.gameObject);
            yield return Until(() => stack.Top is ShowScreen, 5f, "the show never opened");
            got((ShowScreen)stack.Top);
        }

        /// <summary>The seeded skill levels, copied into the live save (Boot reads the file only once per session).</summary>
        void ReloadSeed()
        {
            if (Boot.Store == null) return;
            Boot.Store.Blob.Skills.Clear();                                    // every fixture starts from the seeded levels
            if (_seedSkill != null) Boot.Store.Blob.Skills[_seedSkill] = new SkillRecord { Level = _seedLevel };
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

        /// <summary>A sticker button (hint, set, load, undo, clear, a choice, the pickers' keys): waits out its click guard first.</summary>
        static IEnumerator Tap(GameObject go)
        {
            yield return new WaitForSecondsRealtime(ClickGuard.DefaultWindow + .05f);
            Press(go);
            yield return null;
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
