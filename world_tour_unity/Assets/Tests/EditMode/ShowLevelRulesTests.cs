using System.Collections.Generic;
using NUnit.Framework;
using WorldTour.Core.Shows;

namespace WorldTour.Core.Tests
{
    /// <summary>
    /// world_tour/js/progress.js recordSkill: level 1..3, up after 4 right in a row, down after 2 misses in a
    /// row. (The Node suites have no test of these; these are new, written against the JS source.)
    /// </summary>
    public class ShowLevelRulesTests
    {
        private static int Right(SkillRecord k, int n)
        {
            int moved = 0;
            for (int i = 0; i < n; i++) moved += LevelRules.Record(k, true);
            return moved;
        }

        private static int Wrong(SkillRecord k, int n)
        {
            int moved = 0;
            for (int i = 0; i < n; i++) moved += LevelRules.Record(k, false);
            return moved;
        }

        [Test]
        public void NewRecord_MatchesTheJsDefaults()
        {
            var k = new SkillRecord();
            Assert.AreEqual(1, k.Level);
            Assert.AreEqual(0, k.Run + k.MissRun + k.A + k.C + k.H);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, k.ByLevel.Keys);
            Assert.AreEqual(0, k.Mistakes.Count);
        }

        [Test]
        public void FourRightInARow_LevelsUp_AndResetsTheRun()
        {
            var k = new SkillRecord();
            Assert.AreEqual(0, Right(k, 3));
            Assert.AreEqual(1, k.Level);
            Assert.AreEqual(1, LevelRules.Record(k, true));
            Assert.AreEqual(2, k.Level);
            Assert.AreEqual(0, k.Run);
            Assert.AreEqual(0, Right(k, 3));
            Assert.AreEqual(1, Right(k, 1));
            Assert.AreEqual(3, k.Level);
        }

        [Test]
        public void ThreeRightThenAMiss_DoesNotLevelUp()
        {
            var k = new SkillRecord();
            Right(k, 3);
            Wrong(k, 1);
            Right(k, 3);
            Assert.AreEqual(1, k.Level);
            Right(k, 1);
            Assert.AreEqual(2, k.Level);
        }

        [Test]
        public void TwoMissesInARow_LevelsDown_AndResetsTheMissRun()
        {
            var k = new SkillRecord { Level = 3 };
            Assert.AreEqual(0, Wrong(k, 1));
            Assert.AreEqual(-1, LevelRules.Record(k, false));
            Assert.AreEqual(2, k.Level);
            Assert.AreEqual(0, k.MissRun);
            Assert.AreEqual(0, Wrong(k, 1));
            Assert.AreEqual(-1, Wrong(k, 1));
            Assert.AreEqual(1, k.Level);
        }

        [Test]
        public void MissRightMiss_DoesNotLevelDown()
        {
            var k = new SkillRecord { Level = 2 };
            Wrong(k, 1); Right(k, 1); Wrong(k, 1);
            Assert.AreEqual(2, k.Level);
        }

        [Test]
        public void Boundaries_NeverBelowOneNeverAboveThree()
        {
            var k = new SkillRecord();
            Assert.AreEqual(0, Wrong(k, 10));
            Assert.AreEqual(1, k.Level);
            Assert.AreEqual(10, k.MissRun, "the miss run keeps counting at the floor (JS)");
            k = new SkillRecord { Level = 3 };
            Assert.AreEqual(0, Right(k, 12));
            Assert.AreEqual(3, k.Level);
            Assert.AreEqual(12, k.Run, "the run keeps counting at the top (JS)");
            Assert.AreEqual(-1, Wrong(k, 2), "but a run of misses still moves it down");
            Assert.AreEqual(2, k.Level);
        }

        [Test]
        public void AtTheFloor_ABuiltUpMissRunDoesNotCarryOver()
        {
            // JS quirk worth pinning: at level 1 missRun grows past 2; one right answer resets it.
            var k = new SkillRecord();
            Wrong(k, 5);
            Right(k, 1);
            Assert.AreEqual(0, k.MissRun);
        }

        [Test]
        public void Counts_AttemptsCorrectHintedByLevelAndMistakes()
        {
            var k = new SkillRecord();
            LevelRules.Record(k, true, hinted: true);
            LevelRules.Record(k, false, mistake: "no-borrow");
            LevelRules.Record(k, true);
            LevelRules.Record(k, true);
            LevelRules.Record(k, true);
            LevelRules.Record(k, true);                          // 4 in a row: level 2 after this one is counted at 1
            LevelRules.Record(k, false, hinted: true, mistake: "no-borrow");
            LevelRules.NoteMistake(k, "gave-total");
            LevelRules.NoteMistake(k, null);
            Assert.AreEqual(7, k.A);
            Assert.AreEqual(5, k.C);
            Assert.AreEqual(2, k.H);
            CollectionAssert.AreEqual(new[] { 6, 5 }, k.ByLevel[1]);
            CollectionAssert.AreEqual(new[] { 1, 0 }, k.ByLevel[2]);
            CollectionAssert.AreEqual(new Dictionary<string, int> { { "no-borrow", 2 }, { "gave-total", 1 } }, k.Mistakes);
        }

        [Test]
        public void DamagedRecord_MissingByLevelRow_IsRepaired()
        {
            var k = new SkillRecord { Level = 2, ByLevel = new Dictionary<int, int[]>() };
            LevelRules.Record(k, true);
            CollectionAssert.AreEqual(new[] { 1, 1 }, k.ByLevel[2]);
        }
    }
}
