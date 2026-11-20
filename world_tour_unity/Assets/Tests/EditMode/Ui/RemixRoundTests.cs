using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Maths;
using WorldTour.Game.Modes;

namespace WorldTour.Game.EditTests
{
    /// <summary>
    /// The remix round's timing (2.7 tester's freeze: after a wrong try and the reset, picks and keys did nothing).
    /// A fake show with a hand-turned clock runs the timers; landings can be held back, timers dropped, listeners made
    /// to throw. Whatever happens, the board must not stay busy once the wait is over.
    /// </summary>
    public class RemixRoundTests
    {
        private sealed class FakeHost : IRemixHost
        {
            public float Clock;
            public bool OpenFlag = true, ResolvedFlag;
            public int TriesCount;
            public bool ThrowOnMiss, ThrowOnSay, ThrowOnBlock, DropLaters;
            public readonly List<KeyValuePair<float, Action>> Timers = new List<KeyValuePair<float, Action>>();
            public readonly List<string> Logs = new List<string>();
            public readonly List<Exception> Errors = new List<Exception>();
            public int Wins, Reveals, Nexts;
            public bool HintsBlocked;

            public bool Open { get { return OpenFlag && !ResolvedFlag; } }
            public bool Resolved { get { return ResolvedFlag; } }
            public int Tries { get { return TriesCount; } }
            public float Now { get { return Clock; } }
            public int Miss() { TriesCount++; if (ThrowOnMiss) throw new InvalidOperationException("a listener failed"); return TriesCount; }
            public bool Win() { ResolvedFlag = true; Wins++; return true; }
            public bool Reveal() { ResolvedFlag = true; Reveals++; return true; }
            public void Say(string line, string pose) { if (ThrowOnSay) throw new InvalidOperationException("the dog failed"); }
            public void Later(Action action, float seconds) { if (!DropLaters) Timers.Add(new KeyValuePair<float, Action>(Clock + seconds, action)); }
            public void Next(float seconds) { Nexts++; }
            public void BlockHints(bool blocked) { if (ThrowOnBlock) throw new InvalidOperationException("hint button gone"); HintsBlocked = blocked; }
            public string Ladder(int tries) { return "ladder " + tries; }
            public void Log(string line) { Logs.Add(line); }
            public void Error(Exception e) { Errors.Add(e); }

            /// <summary>Moves the clock and runs every timer that is due, in order.</summary>
            public void Advance(float seconds)
            {
                float end = Clock + seconds;
                while (true)
                {
                    var due = Timers.Where(t => t.Key <= end).OrderBy(t => t.Key).FirstOrDefault();
                    if (due.Value == null) break;
                    Timers.Remove(due);
                    Clock = Math.Max(Clock, due.Key);
                    due.Value();
                }
                Clock = end;
            }
        }

        private sealed class FakeView : IRemixView
        {
            public bool HoldLandings;
            public readonly List<Action> Held = new List<Action>();
            public int Paints, Resets, Wobbles, Celebrations, Words, Locks;

            public void Leave(int tile, Action landed) { if (HoldLandings) Held.Add(landed); else landed(); }
            public void Paint(int landed) { Paints++; }
            public void Wobble() { Wobbles++; }
            public void LockTiles() { Locks++; }
            public void Celebrate() { Celebrations++; }
            public void ShowWord() { Words++; }
            public void ResetTiles() { Resets++; }
        }

        private static RemixRound Round(string word, string mix, out FakeHost host, out FakeView view)
        {
            host = new FakeHost();
            view = new FakeView();
            return new RemixRound(new RemixBoard(word, mix.ToList()), host, view);
        }

        /// <summary>Picks tiles to spell <paramref name="letters"/> (first unused tile with each letter).</summary>
        private static void Spell(RemixRound r, string letters)
        {
            foreach (char c in letters) r.Pick(r.Board.TileForKey(c));
        }

        // ---------- the board's token ----------

        [Test]
        public void Board_StaleResetToken_IsIgnored()
        {
            var b = new RemixBoard("cat", "tac".ToList());
            b.Pick(0); b.Pick(1); b.Pick(2);
            int first = b.StartMiss();
            Assert.IsTrue(b.FinishMissReset(first));
            Assert.IsFalse(b.Busy);
            b.Pick(0); b.Pick(1); b.Pick(2);
            int second = b.StartMiss();
            Assert.IsFalse(b.FinishMissReset(first), "an earlier wait's reset can't end this one");
            Assert.IsTrue(b.Busy);
            Assert.AreEqual(3, b.Picked.Count);
            Assert.IsTrue(b.FinishMissReset(second));
            Assert.IsFalse(b.Busy);
            Assert.AreEqual(0, b.Picked.Count);
        }

        // ---------- the freeze ----------

        [Test]
        public void LastTile_ThenTheMiss_ThenTheReset_PicksWorkAgain()
        {
            RemixRound r = Round("cat", "tac", out FakeHost h, out FakeView v);
            Spell(r, "tac");                                              // wrong, and the last pick checks at once
            Assert.IsTrue(r.Board.Busy);
            Assert.IsTrue(r.InMissWait);
            Assert.AreEqual(1, v.Wobbles);
            Assert.IsTrue(h.HintsBlocked);
            Assert.AreEqual(PickResult.Ignored, r.Pick(0), "busy during the wait");
            h.Advance(.74f);
            Assert.IsTrue(r.Board.Busy);
            h.Advance(.02f);
            Assert.IsFalse(r.Board.Busy, "free after 750 ms");
            Assert.IsFalse(r.InMissWait);
            Assert.AreEqual(0, r.Board.Picked.Count);
            Assert.IsFalse(h.HintsBlocked);
            Assert.AreEqual(1, v.Resets);
            Spell(r, "cat");
            Assert.AreEqual(1, h.Wins);
            Assert.IsTrue(h.Logs.Any(l => l == "WT_PICK idx=" + r.Board.Mix.ToList().IndexOf('t') + " landed=false busy=true open=true resolved=false"));
        }

        [Test]
        public void LandingsHeldPastTheReset_NeverTouchBusy_AndAreDropped()
        {
            RemixRound r = Round("because", "aubeesc", out FakeHost h, out FakeView v);
            v.HoldLandings = true;
            Spell(r, "becuase");                                          // wrong order
            Assert.IsTrue(r.Board.Busy);
            h.Advance(.8f);                                               // the reset runs before any tile has landed
            Assert.IsFalse(r.Board.Busy);
            int paints = v.Paints;
            foreach (Action land in v.Held) land();                       // the old landings arrive late
            Assert.IsFalse(r.Board.Busy, "a landing never sets Busy");
            Assert.AreEqual(paints, v.Paints, "landings from before the reset are dropped");
            v.Held.Clear();
            Spell(r, "because");
            Assert.AreEqual(1, h.Wins);
        }

        [Test]
        public void LandingDuringTheWait_StillPaintsTheLastLetter()
        {
            RemixRound r = Round("cat", "tac", out FakeHost h, out FakeView v);
            v.HoldLandings = true;
            Spell(r, "tac");
            int paints = v.Paints;
            v.Held.Last()();                                              // the last tile lands during the wait
            Assert.AreEqual(paints + 1, v.Paints, "the wrong word shows in full while she reads the ladder");
            Assert.IsTrue(r.Board.Busy);
            h.Advance(1f);
            Assert.IsFalse(r.Board.Busy);
        }

        [Test]
        public void TheDogThrowing_CantSkipTheReset()
        {
            RemixRound r = Round("cat", "tac", out FakeHost h, out FakeView v);
            h.ThrowOnSay = true;
            h.ThrowOnBlock = true;
            Spell(r, "tac");
            Assert.AreEqual(2, h.Errors.Count, "logged, not thrown into the input handler");
            h.Advance(.8f);
            Assert.IsFalse(r.Board.Busy);
            Spell(r, "cat");
            Assert.AreEqual(1, h.Wins);
        }

        [Test]
        public void AListenerThrowingInMiss_CantSkipTheReset()
        {
            RemixRound r = Round("cat", "tac", out FakeHost h, out FakeView v);
            h.ThrowOnMiss = true;
            Spell(r, "tac");
            Assert.AreEqual(1, h.Errors.Count);
            Assert.IsTrue(r.Board.Busy);
            h.Advance(.8f);
            Assert.IsFalse(r.Board.Busy, "the tries count still came from the engine and the wait ended");
        }

        [Test]
        public void ADroppedTimer_IsRecoveredHalfASecondLate()
        {
            RemixRound r = Round("cat", "tac", out FakeHost h, out FakeView v);
            h.DropLaters = true;
            Spell(r, "tac");
            h.Advance(1.2f);
            r.Tick();
            Assert.IsTrue(r.Board.Busy, "not before 750 + 500 ms");
            h.Advance(.06f);
            r.Tick();
            Assert.IsFalse(r.Board.Busy);
            Assert.IsTrue(h.Logs.Any(l => l.StartsWith("WT_PICK_RECOVER")));
            Assert.AreEqual(0, r.Board.Picked.Count);
            h.DropLaters = false;
            Spell(r, "cat");
            Assert.AreEqual(1, h.Wins);
        }

        [Test]
        public void ALateResetAfterTheRecovery_DoesNothing()
        {
            RemixRound r = Round("cat", "tac", out FakeHost h, out FakeView v);
            Spell(r, "tac");
            var reset = h.Timers.Single();
            h.Timers.Clear();                                             // hold the reset back
            h.Advance(1.3f);
            r.Tick();                                                     // recovered
            Spell(r, "act");                                              // a second wrong try starts a new wait
            Assert.IsTrue(r.Board.Busy);
            reset.Value();                                                // the first wait's reset finally lands
            Assert.IsTrue(r.Board.Busy, "it can't end the second wait early");
            Assert.AreEqual(3, r.Board.Picked.Count);
            h.Advance(.8f);
            Assert.IsFalse(r.Board.Busy);
        }

        [Test]
        public void TheThirdMiss_Reveals_AndStaysBusy()
        {
            RemixRound r = Round("cat", "tac", out FakeHost h, out FakeView v);
            for (int i = 0; i < 2; i++) { Spell(r, "tac"); h.Advance(.8f); }
            Spell(r, "tac");
            Assert.AreEqual(1, h.Reveals);
            Assert.AreEqual(1, h.Nexts);
            h.Advance(5f);
            r.Tick();
            Assert.AreEqual(1, v.Words);
            Assert.IsTrue(r.Board.Busy, "the song is over: no recovery reopens it");
            Assert.IsFalse(r.InMissWait);
            Assert.AreEqual(PickResult.Ignored, r.Pick(0));
        }

        [Test]
        public void ARightAnswer_Locks_DoubleClicksAreIgnored()
        {
            RemixRound r = Round("cat", "tac", out FakeHost h, out FakeView v);
            Spell(r, "cat");
            Assert.AreEqual(1, h.Wins);
            Assert.AreEqual(PickResult.Ignored, r.Pick(0));
            Assert.AreEqual(PickResult.Ignored, r.Pick(1));
            Assert.AreEqual(1, h.Wins, "never a second Win");
            h.Advance(1f);
            Assert.AreEqual(1, v.Celebrations);
            Assert.IsTrue(h.Logs.Last().Contains("open=false"));
        }

        [Test]
        public void UndoAndClear_DuringTheWait_DoNothing()
        {
            RemixRound r = Round("cat", "tac", out FakeHost h, out FakeView v);
            Spell(r, "tac");
            Assert.IsFalse(r.Undo());
            Assert.IsFalse(r.Clear());
            h.Advance(.8f);
            r.Pick(2);
            Assert.IsTrue(r.Undo());
        }

        [Test]
        public void Hammer_RandomPlay_NeverStuck()
        {
            // 300 songs of random presses, undos, clears, held landings, late or dropped timers and throwing listeners.
            // After every step: once past every deadline, the board is busy only if the song is answered.
            var rng = new Rng(20261120);
            int songs = 0, wrongWaits = 0;
            for (int song = 0; song < 300; song++)
            {
                RemixRound r = Round("because", "aubeescl", out FakeHost h, out FakeView v);
                h.ThrowOnSay = rng.Chance(.2);
                h.ThrowOnMiss = rng.Chance(.1);
                h.DropLaters = rng.Chance(.15);
                v.HoldLandings = rng.Chance(.5);
                for (int step = 0; step < 120 && !h.ResolvedFlag; step++)
                {
                    double a = rng.Next();
                    if (a < .6) r.Pick(rng.Int(0, 7));
                    else if (a < .65) r.Undo();
                    else if (a < .68) r.Clear();
                    else if (a < .8 && v.Held.Count > 0) { int i = rng.Int(0, v.Held.Count - 1); Action land = v.Held[i]; v.Held.RemoveAt(i); land(); }
                    else h.Advance((float)rng.Next() * .5f);
                    if (r.InMissWait) wrongWaits++;
                    r.Tick();
                    Assert.IsFalse(r.Board.IsFull && !r.Board.Busy && !h.ResolvedFlag, "a full board always gets checked");
                }
                h.Advance(2f);
                r.Tick();
                foreach (Action land in v.Held) land();
                Assert.IsTrue(!r.Board.Busy || h.ResolvedFlag, "song " + song + ": stuck busy");
                Assert.IsFalse(r.InMissWait);
                if (!h.ResolvedFlag)
                {
                    int free = Enumerable.Range(0, r.Board.Mix.Count).First(i => !r.Board.IsUsed(i));
                    Assert.AreNotEqual(PickResult.Ignored, r.Pick(free), "picks work");
                }
                songs++;
            }
            Assert.AreEqual(300, songs);
            Assert.Greater(wrongWaits, 100, "the hammer did reach the wait after wrong tries");
        }
    }
}
