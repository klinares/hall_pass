using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Shows;
using WorldTour.Game.Mascot;

namespace WorldTour.Mascot.Tests
{
    public class MascotCueTests
    {
        // a random source that counts how often it is used
        sealed class Counter { public int N; readonly double _v; public Counter(double v) { _v = v; } public double Next() { N++; return _v; } }

        static DogCueInput In(Counter c, string word = null) => new DogCueInput { Random = c.Next, Word = word, RevealLine = w => "reveal:" + w };
        static ShowEvent Ev(ShowEventKind k) => new ShowEvent { Kind = k };

        [Test]
        public void TableCoversTheShowEventsRiffReactsTo()
        {
            var want = new[] { ShowEventKind.ItemStarted, ShowEventKind.HintUsed, ShowEventKind.WrongTry, ShowEventKind.Answered, ShowEventKind.Revealed, ShowEventKind.ShowFinished };
            CollectionAssert.AreEquivalent(want, DogCues.Table.Keys.ToArray());
            foreach (ShowEventKind k in Enum.GetValues(typeof(ShowEventKind)))
                if (!want.Contains(k)) Assert.IsFalse(DogCues.For(Ev(k), In(new Counter(0))).HasPose, k.ToString());
            Assert.IsFalse(DogCues.For(null, In(new Counter(0))).HasPose);
        }

        [Test]
        public void ItemStartedSaysTheStartNextOrBonusLine()
        {
            var c = new Counter(0);
            var first = DogCues.For(new ShowEvent { Kind = ShowEventKind.ItemStarted, Index = 0, Slot = new ShowSlot(new WordItem("hello"), false) }, In(c));
            Assert.AreEqual(DogPose.Idle, first.Pose);
            Assert.AreEqual(DogLines.ShowStart[0], first.Line);
            var next = DogCues.For(new ShowEvent { Kind = ShowEventKind.ItemStarted, Index = 3, Slot = new ShowSlot(new WordItem("hello"), false) }, In(new Counter(.99)));
            Assert.AreEqual("next one.", next.Line);
            var c2 = new Counter(.5);
            var bonus = DogCues.For(new ShowEvent { Kind = ShowEventKind.ItemStarted, Index = 4, Slot = new ShowSlot(new WordItem("hello"), true) }, In(c2));
            Assert.AreEqual(DogLines.BonusSong, bonus.Line);
            Assert.AreEqual(0, c2.N, "the bonus line draws no random value");
            Assert.AreEqual(DogMotion.None, bonus.Motion);
        }

        [Test]
        public void WinsCheerWithAHopAndTheRightPool()
        {
            var c = new Counter(0);
            var clean3 = DogCues.For(new ShowEvent { Kind = ShowEventKind.Answered, Win = WinKind.Clean, Streak = 3 }, In(c));
            Assert.AreEqual(DogPose.Cheer, clean3.Pose);
            Assert.AreEqual(DogMotion.Hop, clean3.Motion);
            Assert.IsFalse(clean3.MotionAlways);
            Assert.AreEqual("3 in a row. the crowd is losing it.", clean3.Line);
            Assert.AreEqual(0, c.N);
            Assert.AreEqual("clean.", DogCues.For(new ShowEvent { Kind = ShowEventKind.Answered, Win = WinKind.Clean, Streak = 1 }, In(new Counter(0))).Line);
            CollectionAssert.Contains(DogLines.HintWin.ToArray(), DogCues.For(new ShowEvent { Kind = ShowEventKind.Answered, Win = WinKind.HintOnly }, In(new Counter(.7))).Line);
            CollectionAssert.Contains(DogLines.AfterMiss.ToArray(), DogCues.For(new ShowEvent { Kind = ShowEventKind.Answered, Win = WinKind.AfterMiss }, In(new Counter(.7))).Line);
        }

        [Test]
        public void MissesHintsRevealsAndTheFinish()
        {
            var wrong = DogCues.For(Ev(ShowEventKind.WrongTry), In(new Counter(0)));
            Assert.AreEqual(DogPose.Oops, wrong.Pose); Assert.AreEqual(DogMotion.Wobble, wrong.Motion); Assert.IsTrue(wrong.MotionAlways); Assert.IsNull(wrong.Line);
            var hint = DogCues.For(Ev(ShowEventKind.HintUsed), In(new Counter(0)));
            Assert.AreEqual(DogPose.Think, hint.Pose); Assert.AreEqual(DogMotion.None, hint.Motion); Assert.IsNull(hint.Line);
            var reveal = DogCues.For(Ev(ShowEventKind.Revealed), In(new Counter(0), "ocean"));
            Assert.AreEqual(DogPose.Oops, reveal.Pose);
            Assert.AreEqual("reveal:ocean", reveal.Line, "the reveal text comes from the mode (RemixLines.Reveal)");
            Assert.IsNull(DogCues.For(Ev(ShowEventKind.Revealed), new DogCueInput { Random = () => 0, Word = "ocean" }).Line, "no reveal line without a mode's");
            Assert.IsNull(DogCues.For(Ev(ShowEventKind.Revealed), In(new Counter(0))).Line, "math modes say their own reveal");
            Func<double, DogCue> fin = pct => DogCues.For(new ShowEvent { Kind = ShowEventKind.ShowFinished, Summary = new ShowSummary { Pct = pct } }, In(new Counter(0)));
            Assert.AreEqual(DogPose.Cheer, fin(.95).Pose); Assert.AreEqual(DogMotion.Hop, fin(.95).Motion);
            Assert.AreEqual(DogPose.Cheer, fin(.7).Pose);
            Assert.AreEqual(DogPose.Idle, fin(.69).Pose); Assert.AreEqual(DogMotion.None, fin(.69).Motion);
            var nudge = DogCues.Nudge();
            Assert.AreEqual(DogPose.Think, nudge.Pose); Assert.AreEqual(DogLines.IdleHint, nudge.Line);
        }

        [Test]
        public void CoachHopsOnlyOnAChangeButWobblesEveryWrongTry()
        {
            var coach = new DogCoach();
            var win = DogCue.Of(DogPose.Cheer, DogMotion.Hop, "x");
            Assert.AreEqual(DogMotion.Hop, coach.Apply(win, out bool ch1)); Assert.IsTrue(ch1);
            Assert.AreEqual(DogMotion.None, coach.Apply(win, out bool ch2)); Assert.IsFalse(ch2);
            var wrong = DogCue.Of(DogPose.Oops, DogMotion.Wobble, null, always: true);
            Assert.AreEqual(DogMotion.Wobble, coach.Apply(wrong, out _));
            Assert.AreEqual(DogMotion.Wobble, coach.Apply(wrong, out bool ch3)); Assert.IsFalse(ch3);
            Assert.AreEqual("x", coach.Line, "a cue without a line keeps the bubble");
            Assert.AreEqual(DogMotion.Hop, coach.Say("yes", DogPose.Cheer, out _));
            Assert.AreEqual(DogMotion.None, coach.Say("still", null, out bool ch4)); Assert.IsFalse(ch4);
            Assert.AreEqual(DogPose.Cheer, coach.Pose); Assert.AreEqual("still", coach.Line);
        }

        [Test]
        public void AFullSpellingShowThroughTheRealEngine()
        {
            var items = new List<ShowItem> { new WordItem("ocean", "o-cean"), new WordItem("bright"), new WordItem("chorus", "cho-rus") };
            var engine = new ShowEngine(new ShowSetup { ModeId = "remix", VenueId = "coffee", Items = items, ClockMs = () => 0 });
            var coach = new DogCoach();
            var log = new List<string>();
            var rng = new WorldTour.Core.Maths.Rng(7);
            engine.Raised += e =>
            {
                var word = engine.Current?.Item is WordItem w ? w.W : null;
                var cue = DogCues.For(e, new DogCueInput { Random = rng.Next, Word = word });
                var m = coach.Apply(cue, out _);
                if (cue.HasPose) log.Add(e.Kind + ":" + DogPoses.Id(coach.Pose) + ":" + m);
            };
            engine.Start();                                   // song 1
            engine.Miss(); engine.Win();                      // wrong, then right
            engine.Next();                                    // song 2
            engine.SetupHints(3); engine.Hint(); engine.Win();
            engine.Next();                                    // song 3
            engine.Win();
            while (!engine.Finished) { engine.Next(); if (!engine.Finished) engine.Win(); }
            CollectionAssert.AreEqual(new[] {
                "ItemStarted:idle:None", "WrongTry:oops:Wobble", "Answered:cheer:Hop",
                "ItemStarted:idle:None", "HintUsed:think:None", "Answered:cheer:Hop",
                "ItemStarted:idle:None", "Answered:cheer:Hop" }, log.Take(8).ToArray());
            Assert.IsTrue(log.Last().StartsWith("ShowFinished:"), log.Last());
            Assert.AreEqual(DogPose.Cheer, coach.Pose, "a good show ends cheering");
        }
    }
}
