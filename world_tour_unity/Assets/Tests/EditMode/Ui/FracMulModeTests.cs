using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;
using WorldTour.Game.Modes;

namespace WorldTour.Game.EditTests
{
    /// <summary>The snack table's rules (Game side): the picking round, the ladder's pictures, and a line for every mistake it tags.</summary>
    public class FracMulModeTests
    {
        private sealed class Song : IMathSong
        {
            public readonly List<string> Log = new List<string>();
            int _tries;
            public void SetupHints(int max, Func<int, bool> apply) { }
            public int Miss() { return ++_tries; }
            public void Mistake(string tag) { Log.Add("mistake:" + tag); }
            public void Win() { Log.Add("win"); }
            public void Reveal(string line) { Log.Add("reveal"); }
            public void Say(string line, string pose) { Log.Add("say:" + MathText.Plain(line)); }
            public void LockHints() { }
        }

        private sealed class View : IFmChoiceView
        {
            public readonly List<string> Log = new List<string>();
            public void Gone(int i) { Log.Add("gone:" + i); }
            public void FreezeChoices() { Log.Add("freeze"); }
            public void Good(int i, bool pop) { Log.Add("good:" + i + ":" + pop); }
            public void ShowNext() { Log.Add("next"); }
        }

        static Problem Picking(int level, string kind)
        {
            for (long seed = 1; seed < 500; seed++)
            {
                Problem p = FracMul.Generate(level, seed, true);
                if (p.Kind == kind) return p;
            }
            throw new InvalidOperationException("no " + kind);
        }

        [Test]
        public void PickRound_RightWins_WrongIsATryWithItsMistake_ThirdShowsTheRightOne()
        {
            Problem p = Picking(2, "shaded");
            var song = new Song(); var view = new View();
            float next = -1;
            var round = new FmChoiceRound(p, song, view, s => next = s, null);
            int wrongA = Enumerable.Range(0, p.Choices.Count).Where(k => !p.Choices[k].Correct).ElementAt(0);
            Assert.AreEqual(FmPick.Wrong, round.Pick(wrongA));
            CollectionAssert.Contains(view.Log, "gone:" + wrongA);
            CollectionAssert.Contains(song.Log, "mistake:" + p.Choices[wrongA].Why);
            Assert.IsFalse(round.Ladder.Over);
            Assert.AreEqual(FmPick.Wrong, round.Pick(Enumerable.Range(0, 4).Where(k => !p.Choices[k].Correct && k != wrongA).First()));
            int third = Enumerable.Range(0, 4).Where(k => !p.Choices[k].Correct).Last();
            Assert.AreEqual(FmPick.WrongShown, round.Pick(third));
            CollectionAssert.Contains(view.Log, "good:" + round.RightIndex + ":False");
            CollectionAssert.Contains(view.Log, "next");
            Assert.IsTrue(round.Ladder.Over);
            Assert.AreEqual(FmPick.Ignored, round.Pick(round.RightIndex), "the song is decided");

            var song2 = new Song(); var view2 = new View();
            var r2 = new FmChoiceRound(p, song2, view2, s => next = s, null);
            Assert.AreEqual(FmPick.Right, r2.Pick(r2.RightIndex));
            Assert.AreEqual(TypedMathRound.NextAfterRight, next);
            CollectionAssert.Contains(song2.Log, "win");
            Assert.AreEqual(FmPick.Ignored, r2.Pick(0));
        }

        [Test]
        public void LadderStages_FollowTheMisses_AndWorkedPictureComesLast()
        {
            Problem p = Picking(2, "nonnon");
            var song = new Song();
            var stages = new List<int>();
            var round = new FmChoiceRound(Picking(2, "shaded"), song, new View(), s => { }, n => stages.Add(n));
            var ladder = new MathLadder(song, p, n => stages.Add(100 + n));
            ladder.Wrong("1/99");
            ladder.Wrong("1/99");
            Assert.AreEqual(new[] { 101, 102 }, stages.Where(s => s >= 100).ToArray(), "misses 1 and 2 show hints 1 and 2");
            Assert.IsTrue(ladder.Wrong("1/99"));
            Assert.AreEqual(103, stages.Last(), "the third miss draws the worked picture");
            Assert.IsNotNull(round);
        }

        [Test]
        public void EveryTaggedMistakeHasALine()
        {
            var tags = new HashSet<string>();
            foreach (int level in new[] { 0, 1, 2, 3 })
                for (long seed = 1; seed <= 2000; seed++)
                {
                    Problem p = FracMul.Generate(level, seed, true);
                    foreach (Trap t in p.Traps) tags.Add(t.Why);
                    if (p.Choices != null) foreach (Choice c in p.Choices) if (c.Why != null) tags.Add(c.Why);
                }
            Assert.Greater(tags.Count, 15);
            foreach (string t in tags) Assert.IsTrue(FracMulLines.Trap.ContainsKey(t), "no line for " + t);
            foreach (string t in tags) Assert.AreEqual(FracMulLines.Trap[t], FracMulLines.Trap[t].ToLowerInvariant());
        }

        [Test]
        public void ChoiceLabels_StackFractions_AndKeepTheSignsOutOfTags()
        {
            Problem c = Picking(3, "compare");
            string less = FracMulLines.ChoiceLabel(c, c.Choices[0]);
            StringAssert.Contains("<noparse><</noparse>", less);
            Problem s = Picking(2, "shaded");
            StringAssert.Contains("<mspace", FracMulLines.ChoiceLabel(s, s.Choices[0]));
            Assert.IsTrue(FracMulLines.Picks(s) && FracMulLines.Picks(c));
            Assert.IsFalse(FracMulLines.Picks(Picking(1, "part")));
            Assert.IsTrue(FracMulLines.HasPicture(s));
            Assert.IsFalse(FracMulLines.HasPicture(c));
        }
    }
}
