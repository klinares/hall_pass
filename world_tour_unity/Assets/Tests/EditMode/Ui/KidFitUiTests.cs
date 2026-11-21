using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;
using WorldTour.Core.Shows;
using WorldTour.Game.Modes;
using WorldTour.Game.Ticket;

namespace WorldTour.Game.EditTests
{
    /// <summary>Phase 7.4 kid-fit, round 1: ticket sales' step question, snack table prompts and picture, plain copy, first-try count.</summary>
    public class KidFitUiTests
    {
        private sealed class Fake : IMathSong, IMathPadView
        {
            public readonly List<string> Said = new List<string>();
            public int Tries, Wins; public bool Revealed;
            private Func<int, bool> _hint;
            public void SetupHints(int max, Func<int, bool> apply) { _hint = apply; }
            public int Miss() { return ++Tries; }
            public void Mistake(string tag) { }
            public void Win() { Wins++; }
            public void Reveal(string line) { Revealed = true; }
            public void Say(string line, string pose) { Said.Add(line); }
            public void LockHints() { }
            public bool Hint() { return _hint != null && _hint(0); }
            public void Nudge() { }
            public void LockRight() { }
            public void Wobble() { }
            public void ShowAnswer(string text) { }
            public void Clear() { }
            public void ShowNext() { }
        }

        private static long Ans(Problem p) { return (long)p.Answer.Value.Number; }

        [Test]
        public void TicketSales_StepQuestion_AndTheWholeAnswerStillFinishesIt()
        {
            int checkedDiv = 0;
            for (int level = 0; level <= 3; level++)
                for (long seed = 1; seed < 400; seed += 3)
                {
                    Problem p = MultDiv.Generate(level, seed);
                    if (p.Kind != "div")
                    {
                        Assert.IsNull(TicketsStep.Question(p));
                        Assert.IsNull(TicketsStep.StepAnswer(p));
                        continue;
                    }
                    checkedDiv++;
                    string q = TicketsStep.Question(p);
                    Assert.IsNotNull(q);
                    Assert.IsTrue(Regex.IsMatch(q, "^how many \\d+s fit in [\\d,]+\\?$"), q);
                    long step = TicketsStep.StepAnswer(p).Value;
                    Assert.That(step, Is.InRange(1, Ans(p) - 1), "the step is part of the answer");
                    Assert.IsTrue(TicketsStep.IsStepAnswer(p, step.ToString()));
                    Assert.IsFalse(TicketsStep.IsStepAnswer(p, Ans(p).ToString()), "the whole answer is not the step");
                    Assert.IsFalse(TicketsStep.IsStepAnswer(p, ""));
                    StringAssert.Contains("you already have", TicketsStep.Welcome(p));

                    // after a miss the second hint is the step (the dog says it), and the whole answer still wins
                    var f = new Fake();
                    var round = new TypedMathRound(f, p, f, s => { });
                    f.Hint(); f.Hint();
                    Assert.AreEqual(p.Steps[1], f.Said.Last(), "hint 2 is the step");
                    Assert.AreEqual(TypedMathResult.Wrong, round.Submit((Ans(p) + 1).ToString()));
                    Assert.AreEqual(TypedMathResult.Right, round.Submit(Ans(p).ToString()), "the whole answer is right after the step was asked, level " + level);
                    Assert.AreEqual(1, f.Wins);
                    Assert.IsFalse(f.Revealed);

                    var g = new Fake();
                    var round2 = new TypedMathRound(g, p, g, s => { });
                    round2.Submit((Ans(p) + 1).ToString()); round2.Submit((Ans(p) + 2).ToString());
                    Assert.AreEqual(2, g.Tries);
                    Assert.AreEqual(TypedMathResult.Right, round2.Submit(Ans(p).ToString()), "right on the third try too");
                }
            Assert.Greater(checkedDiv, 100);
        }

        // ---------- snack table ----------

        private static int Visible(string prompt)
        {
            return Regex.Replace(prompt, "\\[\\[([^\\]]*)\\]\\]", m => new string('x', m.Groups[1].Value.Length)).Length;
        }

        [Test]
        public void SnackTable_EveryPromptIsShort()
        {
            int worst = 0; string worstText = "";
            foreach (bool division in new[] { true, false })
                for (int level = 0; level <= 3; level++)
                    for (long seed = 1; seed < 1500; seed += 1)
                    {
                        Problem p = FracMul.Generate(level, seed, division);
                        int v = Visible(p.Prompt);
                        if (v > worst) { worst = v; worstText = p.Prompt; }
                    }
            TestContext.Out.WriteLine("KIDFIT_SNACK_WORST " + worst + " " + worstText);
            Assert.LessOrEqual(worst, 102, worstText);   // about three lines of 34 at the card's smallest font
        }

        [Test]
        public void SnackTable_PartOfAPart_NamesTheNumbers_AndOpensOnTheTray()
        {
            int seen = 0;
            for (long seed = 1; seed <= 20; seed++)
            {
                Problem p = FracMul.Generate(1, seed, true);
                Assert.AreEqual("part", p.Kind);
                seen++;
                var f = p.Params["factors"].Items;
                long a = f[0]["d"].Long, b = f[1]["d"].Long;
                Assert.AreEqual("you want [[1/" + b + "]] of a [[1/" + a + "]] piece. picture the tray.", FracMulLines.Intro(p));
                StringAssert.Contains("[[1/" + a + "]] of the", p.Prompt);
                FmPicture pic = FracMulPicture.For(p, 0);
                Assert.AreEqual("area", pic.Kind);
                Assert.AreEqual(a, pic.Cols, "the whole tray is cut into the first part's pieces");
                Assert.AreEqual(1, pic.ColsOn, "the first part is shaded");
                Assert.IsFalse(pic.Overlap, "never the product");
                Assert.AreEqual(1, pic.Rows, "the second cut is the player's to picture");
                Assert.AreEqual(0, pic.RowsOn);
            }
            Assert.AreEqual(20, seen);
            Assert.AreEqual("a part of a part. picture the tray.", FracMulLines.Intro("part"), "the kind-only line is unchanged for the other callers");
        }

        // ---------- plain words ----------

        [Test]
        public void PlainWords_InTheCopy()
        {
            Assert.AreEqual("what is the answer?", BudgetLines.WhatComes);
            Assert.AreEqual("quick as you can. the bar only changes bonus fans. you can't lose fans.", SoundcheckLines.Intro);
            StringAssert.Contains("stuck? tap hint.", WorldTour.Game.Mascot.DogLines.IdleHint);
            StringAssert.DoesNotContain("bulb", WorldTour.Game.Mascot.DogLines.IdleHint);
            Assert.AreEqual("every star practices. try it again.", TicketText.Sub(0));
            Assert.AreEqual("pick any set to play a show. math is further down.", WorldTour.Game.Map.MapText.FirstVisit);
        }

        // ---------- first try ----------

        private static ShowEngine Engine(List<ShowItem> items)
        {
            return new ShowEngine(new ShowSetup { IsMath = false, ModeId = "vowels", VenueId = "coffee", Items = items, SkillRecord = new SkillRecord(), ClockMs = () => 0 });
        }

        [Test]
        public void FirstTry_NeverCountsAMissReplayedClean()
        {
            var items = Enumerable.Range(0, 10).Select(i => (ShowItem)new WordItem("w" + i)).ToList();
            var e = Engine(items);
            e.Start();
            int song = 0;
            while (!e.Finished)
            {
                e.SetupHints(3);
                if (song == 2) e.Miss();       // one deliberate miss; the song comes back clean as a bonus replay
                e.Win();
                e.Next();
                song++;
            }
            Assert.AreEqual(10, e.Summary.N);
            Assert.AreEqual(10, e.Summary.First, "the web rule (saved count, VIP percent) is unchanged");
            Assert.AreEqual(9, e.Summary.FirstMain);
            var m = TicketModel.Build(new TicketData { Summary = e.Summary, Date = "2026-11-21" });
            Assert.AreEqual("9/10", m.Rows.First(r => r.Key == "first try").Value);

            var e2 = Engine(items);
            e2.Start();
            while (!e2.Finished) { e2.SetupHints(3); e2.Win(); e2.Next(); }
            Assert.AreEqual(10, e2.Summary.FirstMain);
        }
    }
}
