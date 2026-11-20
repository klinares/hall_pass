using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;
using WorldTour.Core.Shows;

namespace WorldTour.Core.Tests
{
    /// <summary>The show rules from world_tour/js/show.js, driven through the engine.</summary>
    public class ShowEngineTests
    {
        private static ShowEngine Started(int n, SkillRecord rec = null, List<ShowEvent> log = null)
        {
            var e = ShowTestDriver.Spelling(ShowTestDriver.Words(n), rec);
            if (log != null) e.Raised += log.Add;
            e.Start();
            return e;
        }

        private static void Song(ShowEngine e, string actions)
        {
            e.SetupHints(3);
            foreach (char ch in actions) ShowTestDriver.Act(e, ch);
            e.Next();
        }

        // ---------- the queue ----------

        [Test]
        public void Show_IsTenSongsByDefault()
        {
            Assert.AreEqual(10, Scoring.ShowLength);
            var e = Started(10);
            Assert.AreEqual(10, e.SongCount);
            Assert.AreEqual(1, e.SongNumber);
            Assert.AreEqual("w0", e.Current.Item.Kid);
        }

        [Test]
        public void Miss_ComesBackExactlyThreeSongsLater_AndAgainInTheEncore()
        {
            var log = new List<ShowEvent>();
            var e = Started(10, null, log);
            Song(e, "w");
            Song(e, "w");
            Song(e, "mw");                                        // w2 missed at index 2
            var re = log.Single(x => x.Kind == ShowEventKind.Requeued);
            Assert.AreEqual(6, re.Index, "re-inserted at i + 4");
            Assert.AreEqual(new[] { "w3", "w4", "w5" }, e.Queue.Skip(3).Take(3).Select(x => x.Item.Kid).ToArray(), "three songs in between");
            Assert.IsTrue(e.Queue[6].Bonus);
            Assert.AreSame(e.Queue[2].Item, e.Queue[6].Item, "the same item comes back");
            Song(e, "w"); Song(e, "w"); Song(e, "w");
            Assert.AreEqual(6, e.Index);
            Assert.AreEqual("w2", e.Current.Item.Kid);
            Assert.IsTrue(e.IsBonus);
            Assert.AreEqual(6, e.SongNumber, "a bonus song doesn't advance the song count");
            while (!e.Finished) Song(e, "w");

            Assert.AreEqual(10, e.Summary.N, "bonus replays aren't songs");
            Assert.AreEqual(new[] { "w2" }, e.Summary.MissedItems.Select(x => x.Kid).ToArray());
            Assert.IsTrue(e.Summary.CanEncore);
            var encore = e.CreateEncore(ShowTestDriver.Random(1));
            Assert.IsTrue(encore.IsEncore);
            Assert.AreEqual(new[] { "w2" }, encore.Queue.Select(x => x.Item.Kid).ToArray());
        }

        [Test]
        public void Miss_InTheLastThreeSongs_GoesAsLateAsTheQueueAllows()
        {
            // JS: at = Math.min(G.i + 4, G.q.length). Index 7 of 10 -> 10 (end), two songs in between.
            var log = new List<ShowEvent>();
            var e = Started(10, null, log);
            for (int i = 0; i < 7; i++) Song(e, "w");
            Song(e, "mw");                                       // index 7
            Assert.AreEqual(10, log.Last(x => x.Kind == ShowEventKind.Requeued).Index);
            Song(e, "w");                                        // index 8
            Song(e, "mmmr");                                     // index 9, the last main song
            Assert.AreEqual(11, log.Last(x => x.Kind == ShowEventKind.Requeued).Index, "the last song's miss plays straight after");
            Assert.AreEqual("w0 w1 w2 w3 w4 w5 w6 w7 w8 w9 w7* w9*",
                string.Join(" ", e.Queue.Select(x => x.Item.Kid + (x.Bonus ? "*" : ""))));
            Song(e, "w");
            Song(e, "w");
            Assert.IsTrue(e.Finished);
            CollectionAssert.AreEqual(new[] { "w7", "w9" }, e.Summary.MissedItems.Select(x => x.Kid).ToArray());
        }

        [Test]
        public void SeveralMisses_OnlyTwoAreReinserted_AllGoToTheEncore()
        {
            var log = new List<ShowEvent>();
            var e = Started(10, null, log);
            Song(e, "mmmr");
            Song(e, "mw");
            Song(e, "mw");                                       // the third miss: no bonus left
            Assert.AreEqual(2, log.Count(x => x.Kind == ShowEventKind.Requeued));
            var skip = log.Single(x => x.Kind == ShowEventKind.RequeueSkipped);
            Assert.AreEqual("no-bonus-left", skip.Tag);
            Assert.AreEqual("w2", skip.Slot.Item.Kid);
            Assert.AreEqual(0, e.BonusLeft);
            while (!e.Finished) Song(e, "w");
            Assert.AreEqual(12, e.Queue.Count);
            var encoreItems = e.EncoreItems(ShowTestDriver.Random(9)).Select(x => x.Kid).OrderBy(x => x).ToArray();
            CollectionAssert.AreEqual(new[] { "w0", "w1", "w2" }, encoreItems);
        }

        [Test]
        public void MissOnABonusReplay_IsNotReinsertedAgain()
        {
            var log = new List<ShowEvent>();
            var e = Started(5, null, log);
            Song(e, "mw");                                       // bonus at 4
            Song(e, "w"); Song(e, "w"); Song(e, "w");
            Assert.IsTrue(e.IsBonus);
            Song(e, "mmmr");
            Assert.AreEqual("bonus", log.Last(x => x.Kind == ShowEventKind.RequeueSkipped).Tag);
            Assert.AreEqual(1, e.BonusLeft);
            Song(e, "w");
            Assert.IsTrue(e.Finished);
            Assert.AreEqual(SetlistTag.Miss, e.Summary.Setlist[0].Tag);
        }

        [Test]
        public void Encore_HalfFans_NoReinserts_NoEncoreOfTheEncore()
        {
            var e = Started(3);
            Song(e, "mw"); Song(e, "mw"); Song(e, "w");
            while (!e.Finished) Song(e, "w");
            var log = new List<ShowEvent>();
            var enc = e.CreateEncore(ShowTestDriver.Random(2));
            enc.Raised += log.Add;
            enc.Start();
            Assert.AreEqual(0, enc.BonusLeft);
            Assert.AreEqual(500, enc.PotentialGain(), "clean in an encore: 1000 / 2");
            Song(enc, "mw");
            Assert.AreEqual(150, enc.Fans, "after a miss: 300 / 2");
            Assert.AreEqual("no-bonus-left", log.Single(x => x.Kind == ShowEventKind.RequeueSkipped).Tag);
            Song(enc, "hw");
            Assert.AreEqual(150 + 250, enc.Fans, "hint only: 500 / 2");
            Assert.IsTrue(enc.Finished);
            Assert.IsTrue(enc.Summary.Encore);
            Assert.IsFalse(enc.Summary.Sold, "an encore never sells out");
            Assert.IsFalse(enc.Summary.CanEncore);
            Assert.Throws<InvalidOperationException>(() => enc.CreateEncore(ShowTestDriver.Random(3)));
        }

        [Test]
        public void Encore_WithNoMisses_IsNotOffered()
        {
            var e = Started(10);
            while (!e.Finished) Song(e, "hw");
            Assert.IsFalse(e.Summary.CanEncore);
            Assert.AreEqual(0, e.Summary.MissedItems.Count);
            Assert.AreEqual(0, e.EncoreItems(ShowTestDriver.Random(4)).Count);
            Assert.Throws<InvalidOperationException>(() => e.CreateEncore(ShowTestDriver.Random(4)));
        }

        [Test]
        public void EmptyShow_FinishesAtOnce()
        {
            var e = Started(0);
            Assert.IsTrue(e.Finished);
            Assert.AreEqual(0, e.Summary.N);
            Assert.AreEqual(0.0, e.Summary.Pct);
            Assert.IsFalse(e.Summary.CanEncore);
        }

        [Test]
        public void EncoreOrder_IsAShuffleOfTheMissedSongs()
        {
            var e = Started(10);
            Song(e, "mmmr"); Song(e, "mw"); Song(e, "w"); Song(e, "mw");
            while (!e.Finished) Song(e, "w");
            // JS harness, scenario C: shuffle(['w0','w1','w3']) with mulberry32(5) gives this order
            CollectionAssert.AreEqual(new[] { "w0", "w1", "w3" }, e.EncoreItems(ShowTestDriver.Random(5)).Select(x => x.Kid).ToArray());
            var other = e.EncoreItems(ShowTestDriver.Random(77)).Select(x => x.Kid).OrderBy(x => x).ToArray();
            CollectionAssert.AreEqual(new[] { "w0", "w1", "w3" }, other);
        }

        // ---------- scoring and streak ----------

        [Test]
        public void Clean_FansGrowWithTheStreak_CappedAtFive()
        {
            var e = Started(7);
            var gains = new List<int>();
            for (int i = 0; i < 7; i++) { int before = e.Fans; Song(e, "w"); gains.Add(e.Fans - before); }
            CollectionAssert.AreEqual(new[] { 1000, 2000, 3000, 4000, 5000, 5000, 5000 }, gains);
            Assert.AreEqual(7, e.Summary.Best);
        }

        [Test]
        public void Hint_CostsThisSongsRewardNotWonFans_AndKeepsTheStreak()
        {
            var log = new List<ShowEvent>();
            var e = Started(10, null, log);
            Song(e, "w"); Song(e, "w");                          // streak 2, 3000 fans
            Assert.AreEqual(3000, e.Fans);
            e.SetupHints(3);
            Assert.AreEqual(3000, e.PotentialGain(), "a clean third song is worth 3000");
            Assert.IsTrue(e.Hint());
            var used = log.Last(x => x.Kind == ShowEventKind.HintUsed);
            Assert.AreEqual(2500, used.Amount, "the hint costs 2500 of this song's reward");
            Assert.AreEqual(3000, e.Fans, "fans already won are untouched");
            Assert.AreEqual(2, e.Streak, "a hint never breaks the streak");
            Assert.IsTrue(e.Hint());
            Assert.AreEqual(0, log.Last(x => x.Kind == ShowEventKind.HintUsed).Amount, "a second hint costs nothing more");
            e.Win();
            Assert.AreEqual(3500, e.Fans);
            Assert.AreEqual(2, e.Streak, "a hinted win keeps the streak but doesn't grow it");
            Assert.AreEqual(WinKind.HintOnly, log.Last(x => x.Kind == ShowEventKind.Answered).Win);
            e.Next();
            Song(e, "w");
            Assert.AreEqual(3500 + 3000, e.Fans, "the next clean song continues the streak at 3");
            Assert.AreEqual(3, e.Streak);
            Assert.AreEqual(2, e.HintTotal);
            Assert.AreEqual(1, e.HintedKids.Count);
        }

        [Test]
        public void Hint_FreeWhenNothingNew_LimitedByMax_OffAfterTheAnswer()
        {
            var e = Started(3);
            Assert.IsFalse(e.Hint(), "no hints before the mode sets them up");
            e.SetupHints(2);
            Assert.IsFalse(e.Hint(level => false), "nothing new to show: free and not counted");
            Assert.AreEqual(0, e.Hints);
            int asked = 0;
            Assert.IsTrue(e.Hint(level => { asked = level; return true; }));
            Assert.AreEqual(1, asked);
            Assert.IsTrue(e.Hint(level => { asked = level; return true; }));
            Assert.AreEqual(2, asked);
            Assert.IsFalse(e.CanHint);
            Assert.IsFalse(e.Hint(), "no hints left");
            Assert.AreEqual(2, e.HintTotal);
            e.Next();
            e.SetupHints(2);
            e.Win();
            Assert.IsFalse(e.Hint(), "the hint button is off once the song is answered");
            e.Next();
            e.SetupHints(2);
            e.LockHints();
            Assert.IsFalse(e.Hint(), "a mode can lock the hints");
        }

        [Test]
        public void WrongTry_ResetsTheStreak_WinAfterItIsWorth300()
        {
            var e = Started(4);
            Song(e, "w"); Song(e, "w");
            e.SetupHints(3);
            Assert.AreEqual(1, e.Miss());
            Assert.AreEqual(0, e.Streak);
            Assert.AreEqual(2, e.Miss());
            Assert.AreEqual(300, e.PotentialGain());
            e.Win();
            Assert.AreEqual(3300, e.Fans);
            Assert.AreEqual(SlotState.Miss, e.Current.State);
            Assert.AreEqual(2, e.Best, "the best streak is kept");
        }

        [Test]
        public void SoundcheckBonus_IsAddedOnTop_NeverHalvedOrNegative()
        {
            var e = Started(2);
            e.SetupHints(2);
            e.Win(250);
            Assert.AreEqual(1250, e.Fans);
            e.Next();
            Assert.Throws<ArgumentOutOfRangeException>(() => e.Win(-1));
            Assert.AreEqual(1250, e.Fans);
            Assert.Throws<ArgumentOutOfRangeException>(() => Scoring.Gain(WinKind.Clean, 1, false, -5));
            Assert.AreEqual(500 + 250, Scoring.Gain(WinKind.Clean, 1, true, 250), "encore halves the base, not the bonus");
        }

        [Test]
        public void NothingEarnedIsEverTakenAway_RandomShows()
        {
            // 400 random shows of random actions: fans never go down, every award is positive,
            // a hint or wrong try never lowers fans, the level stays in 1..3, and the summary adds up.
            var pick = new Rng(20261120);
            string[] songs = { "w", "w", "w", "hw", "hhw", "mw", "mmw", "mmmr", "hmw", "hmmmr", "fw", "x", "hhhw" };
            for (int run = 0; run < 400; run++)
            {
                var rec = new SkillRecord { Level = pick.Int(1, 3) };
                var log = new List<ShowEvent>();
                var e = ShowTestDriver.Spelling(ShowTestDriver.Words(pick.Int(1, 12)), rec);
                int last = 0;
                e.Raised += ev =>
                {
                    log.Add(ev);
                    Assert.GreaterOrEqual(e.Fans, last, "fans never go down");
                    last = e.Fans;
                    if (ev.Kind == ShowEventKind.FansAwarded) Assert.Greater(ev.Amount, 0);
                    if (ev.Kind == ShowEventKind.HintUsed) Assert.GreaterOrEqual(ev.Amount, 0);
                    Assert.That(rec.Level, Is.InRange(LevelRules.MinLevel, LevelRules.MaxLevel));
                };
                e.Start();
                while (!e.Finished)
                {
                    int streakBefore = e.Streak;
                    e.SetupHints(pick.Int(0, 3));
                    string a = pick.Pick(songs);
                    foreach (char ch in a)
                    {
                        if (ch == 'h') { e.Hint(); Assert.AreEqual(streakBefore, e.Streak, "a hint never breaks a streak"); }
                        else ShowTestDriver.Act(e, ch);
                    }
                    e.Next();
                }
                AssertSummaryAddsUp(e, log);
                if (e.Summary.CanEncore)
                {
                    var enc = e.CreateEncore(pick.Next);
                    var elog = new List<ShowEvent>();
                    enc.Raised += elog.Add;
                    enc.Start();
                    while (!enc.Finished) { enc.SetupHints(2); foreach (char ch in pick.Pick(songs)) ShowTestDriver.Act(enc, ch); enc.Next(); }
                    AssertSummaryAddsUp(enc, elog);
                    Assert.AreEqual(enc.Summary.N, enc.Queue.Count, "no re-inserts in an encore");
                }
            }
        }

        [Test]
        public void Summary_TotalsAddUp_ForTheScriptedShows()
        {
            foreach (var script in new[]
            {
                new[] { "w", "hw", "mw", "mmmr", "fw", "w", "hhw", "w", "x", "w", "w", "w" },
                new[] { "mmmr", "mmmr", "mmmr", "w", "w", "w", "w", "w", "w", "w", "w", "w" },
                new string[0]
            })
            {
                var d = new ShowTestDriver();
                var e = ShowTestDriver.Spelling(ShowTestDriver.Words(10));
                d.Play(e, script);
                AssertSummaryAddsUp(e, d.Events);
            }
        }

        private static void AssertSummaryAddsUp(ShowEngine e, List<ShowEvent> log)
        {
            var s = e.Summary;
            Assert.IsNotNull(s);
            var answered = log.Where(x => x.Kind == ShowEventKind.Answered).ToList();
            Assert.AreEqual(log.Where(x => x.Kind == ShowEventKind.FansAwarded).Sum(x => x.Amount), s.Fans, "fans = sum of awards");
            Assert.AreEqual(answered.Sum(x => x.Amount), s.Fans);
            Assert.AreEqual(e.Queue.Count(x => !x.Bonus), s.N);
            Assert.AreEqual(s.N, s.Setlist.Count);
            Assert.AreEqual(answered.Count(x => x.Win == WinKind.Clean), s.First);
            Assert.AreEqual(answered.Count(x => x.Win == WinKind.HintOnly), s.HintOnly);
            Assert.AreEqual(log.Count(x => x.Kind == ShowEventKind.HintUsed), s.HintTotal);
            Assert.AreEqual(e.HintedKids.Count, s.Hinted);
            Assert.AreEqual((s.First + .5 * s.HintOnly) / Math.Max(1, s.N), s.Pct, 0.0);
            Assert.AreEqual(s.Pct >= .9, s.Vip);
            Assert.AreEqual(!s.Encore && s.N >= 5 && s.Pct >= .9, s.Sold);
            Assert.AreEqual(e.Queue.Count, answered.Count + log.Count(x => x.Kind == ShowEventKind.Revealed), "every song ended once");
            Assert.AreEqual(s.Setlist.Count(x => x.Tag == SetlistTag.Miss), s.MissedItems.Count);
            Assert.AreEqual(s.MissedItems.Count > 0 && !s.Encore, s.CanEncore);
            int best = 0, run = 0;
            foreach (var ev in log)
            {
                if (ev.Kind == ShowEventKind.WrongTry) run = 0;
                if (ev.Kind == ShowEventKind.Answered && ev.Win == WinKind.Clean) { run++; best = Math.Max(best, run); }
            }
            Assert.AreEqual(best, s.Best);
            Assert.AreEqual(1, log.Count(x => x.Kind == ShowEventKind.ShowFinished));
            Assert.That(s.Pct, Is.InRange(0.0, 1.0));
        }

        [Test]
        public void Summary_SetlistTags_MissThenHintThenOk()
        {
            var e = Started(4);
            Song(e, "hmw");      // hinted and missed: miss wins
            Song(e, "hw");
            Song(e, "w");
            Song(e, "fw");       // a free hint is not a hint
            while (!e.Finished) Song(e, "w");
            CollectionAssert.AreEqual(new[] { SetlistTag.Miss, SetlistTag.Hint, SetlistTag.Ok, SetlistTag.Ok },
                e.Summary.Setlist.Select(x => x.Tag).ToArray());
            Assert.AreEqual("remix", e.Summary.Mode);
            Assert.AreEqual("coffee", e.Summary.Venue);
            Assert.IsFalse(e.Summary.IsMath);
        }

        [Test]
        public void Summary_Secs_FromTheClock()
        {
            long now = 1000;
            var e = new ShowEngine(new ShowSetup { ModeId = "remix", Items = ShowTestDriver.Words(1), ClockMs = () => now });
            e.Start();
            e.SetupHints(1);
            e.Win();
            now = 1000 + 61500;
            e.Next();
            Assert.AreEqual(62, e.Summary.Secs, "JS Math.round(61.5) = 62");
        }

        // ---------- level during a show ----------

        [Test]
        public void FourRightInARow_LevelsUp_TwoMissesInARow_LevelsDown()
        {
            var rec = new SkillRecord();
            var log = new List<ShowEvent>();
            var e = Started(10, rec, log);
            Song(e, "w"); Song(e, "hw"); Song(e, "mw");          // a win after a wrong try still counts as right (JS)
            Assert.AreEqual(1, rec.Level);
            Song(e, "w");
            Assert.AreEqual(2, rec.Level, "4 right in a row");
            var up = log.Single(x => x.Kind == ShowEventKind.LevelChanged);
            Assert.AreEqual(1, up.Moved);
            Assert.AreEqual(2, up.Level);
            Song(e, "mmmr");
            Assert.AreEqual(2, rec.Level);
            Song(e, "mmmr");
            Assert.AreEqual(1, rec.Level, "2 misses in a row");
            Assert.AreEqual(-1, log.Last(x => x.Kind == ShowEventKind.LevelChanged).Moved);
            Song(e, "mmmr"); Song(e, "mmmr");
            Assert.AreEqual(1, rec.Level, "never below level 1");
        }

        [Test]
        public void WrongTries_AloneDoNotLevelDown()
        {
            // JS: only win() and reveal() call recordSkill; miss() doesn't.
            var rec = new SkillRecord { Level = 2 };
            var e = Started(3, rec);
            e.SetupHints(1);
            e.Miss(); e.Miss();
            Assert.AreEqual(2, rec.Level);
            Assert.AreEqual(0, rec.A);
        }

        [Test]
        public void ItemRecorded_KeepIsSetWhenMissedEarlierThisShow()
        {
            // Port of review.test.mjs "a clean answer clears it unless it was missed earlier this show":
            // the show is what sets keep, so the clean bonus replay must not clear the review entry.
            var log = new List<ShowEvent>();
            var e = Started(5, null, log);
            Song(e, "mw");
            var first = log.Last(x => x.Kind == ShowEventKind.ItemRecorded);
            Assert.IsFalse(first.Clean); Assert.IsTrue(first.Missed); Assert.IsTrue(first.Keep);
            Song(e, "w");
            var fresh = log.Last(x => x.Kind == ShowEventKind.ItemRecorded);
            Assert.IsTrue(fresh.Clean); Assert.IsFalse(fresh.Keep);
            Song(e, "w"); Song(e, "w");
            Assert.IsTrue(e.IsBonus);
            Song(e, "w");
            var replay = log.Last(x => x.Kind == ShowEventKind.ItemRecorded);
            Assert.IsTrue(replay.Clean, "the replay was clean");
            Assert.IsTrue(replay.Keep, "but it stays in review");
            Song(e, "hw");
            var hinted = log.Last(x => x.Kind == ShowEventKind.ItemRecorded);
            Assert.IsTrue(hinted.Hinted); Assert.IsFalse(hinted.Clean); Assert.IsFalse(hinted.Missed);
        }

        [Test]
        public void Mistake_IsCountedOnTheSkill()
        {
            var rec = new SkillRecord();
            var log = new List<ShowEvent>();
            var e = Started(2, rec, log);
            e.SetupHints(2);
            e.NoteMistake("no-borrow");
            e.NoteMistake("no-borrow");
            e.NoteMistake("");
            Assert.AreEqual(2, rec.Mistakes["no-borrow"]);
            Assert.AreEqual(2, log.Count(x => x.Kind == ShowEventKind.MistakeNoted));
        }

        // ---------- math shows ----------

        [Test]
        public void Math_FreshProblemsTakeTheLevelWhenPlayed_BonusReusesTheProblem()
        {
            var calls = new List<string>();
            var rec = new SkillRecord();
            var review = new MathItem("dec", 2, 777, new Problem { Skill = "dec", Level = 2, Seed = 777, Label = "stored" });
            var items = ShowPicker.PickProblems("dec", new[] { review }, ShowTestDriver.Random(99));
            var e = new ShowEngine(new ShowSetup
            {
                IsMath = true, ModeId = "merch", Skill = "dec", Items = items, SkillRecord = rec, ClockMs = () => 0,
                Generate = (lv, seed) => { calls.Add(lv + ":" + seed); return new Problem { Skill = "dec", Level = lv, Seed = seed, Label = "gen" + seed }; }
            });
            e.Start();
            Assert.AreEqual("stored", ((MathItem)e.Current.Item).Prob.Label, "a review problem is played as stored");
            Assert.AreEqual(2, e.CurrentLevel);
            Assert.AreEqual(0, calls.Count);
            Song(e, "mw");                                       // review item missed: bonus at 4
            Assert.AreEqual(1, ((MathItem)e.Current.Item).Level, "fresh items take her level (1) now");
            Song(e, "w"); Song(e, "w"); Song(e, "w");            // 4 right in a row (the miss-then-win counted): level 2
            Assert.AreEqual(2, rec.Level);
            Assert.IsTrue(e.IsBonus);
            Assert.AreEqual("stored", ((MathItem)e.Current.Item).Prob.Label, "the bonus replay is the same problem");
            Song(e, "w");
            Assert.AreEqual(2, ((MathItem)e.Current.Item).Level, "later fresh items take the new level");
            while (!e.Finished) Song(e, "w");
            Assert.AreEqual(9, calls.Count, "one generate per fresh item, none for the review item or the replay");
            StringAssert.StartsWith("1:", calls[0]);
            Assert.AreEqual("dec:2:777", e.Summary.MissedItems.Single().Kid);
            Assert.IsTrue(e.Summary.IsMath);
        }

        [Test]
        public void Math_DefaultGenerator_IsTheRealOne()
        {
            var items = new List<ShowItem> { new MathItem("dec", null, 12345) };
            var e = new ShowEngine(new ShowSetup { IsMath = true, ModeId = "merch", Skill = "dec", Items = items, SkillRecord = new SkillRecord { Level = 2 } });
            e.Start();
            var m = (MathItem)e.Current.Item;
            Assert.AreEqual(2, m.Level);
            Assert.AreEqual(Generators.Generate("dec", 2, 12345).ToCanonicalJson(), m.Prob.ToCanonicalJson());
            Assert.AreEqual("dec:2:12345", m.Kid);
        }

        [Test]
        public void Spelling_SkillIsSpellPlusModeId()
        {
            var e = ShowTestDriver.Spelling(ShowTestDriver.Words(1));
            Assert.AreEqual("spell:remix", e.Skill);
            Assert.Throws<ArgumentException>(() => new ShowEngine(new ShowSetup { IsMath = true, ModeId = "merch", Items = new List<ShowItem>() }));
        }

        // ---------- misuse ----------

        [Test]
        public void Guards_NoDoubleAnswers_NothingAfterTheEnd()
        {
            var e = ShowTestDriver.Spelling(ShowTestDriver.Words(1));
            Assert.Throws<InvalidOperationException>(() => e.Win(), "not started");
            e.Start();
            Assert.Throws<InvalidOperationException>(() => e.Start());
            e.SetupHints(1);
            e.Win();
            Assert.Throws<InvalidOperationException>(() => e.Win(), "already answered");
            Assert.Throws<InvalidOperationException>(() => e.Miss());
            Assert.Throws<InvalidOperationException>(() => e.Reveal());
            Assert.AreEqual(1000, e.Fans);
            e.Next();
            Assert.IsTrue(e.Finished);
            Assert.Throws<InvalidOperationException>(() => e.Next());
            Assert.Throws<InvalidOperationException>(() => e.Hint());
        }
    }
}
