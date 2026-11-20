using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Shows;

namespace WorldTour.Core.Tests
{
    /// <summary>
    /// Scripted shows checked against the real web game. Each expected value was printed by running
    /// world_tour/js/show.js mountShow (with real progress.js, store.js and review.js; only sound, fx, dog,
    /// sticker, motion and the ticket screen stubbed out) on the same script, under Node. The harness is
    /// b14_shows.mjs in the 1.4 builder's scratchpad; it changes nothing in world_tour/.
    /// Encore order: Math.random was reseeded with mulberry32(5) right before the encore button, so the C#
    /// side shuffles with Rng(5).
    /// </summary>
    public class ShowEngineJsTests
    {
        public sealed class Pinned
        {
            public readonly string Name, Log, Show, Encore, Skill;
            public Pinned(string name, string log, string show, string encore, string skill)
            {
                Name = name; Log = log; Show = show; Encore = encore; Skill = skill;
            }
            public override string ToString() { return Name; }
        }

        private static readonly Dictionary<string, string[]> Scripts = new Dictionary<string, string[]>
        {
            { "A_one_miss", new[] { "w", "w", "mw", "w", "w", "w", "w", "w", "w", "w", "w" } },
            { "B_hints", new[] { "w", "w", "hw", "w", "hhw", "fw", "w", "w", "w", "w" } },
            { "C_three_misses", new[] { "mmmr", "mw", "w", "mw", "w", "w", "w", "w", "w", "w", "w", "w" } },
            { "D_late_misses", new[] { "w", "w", "w", "w", "w", "w", "w", "w", "mw", "mmmr", "w", "w" } },
            { "E_levels", new[] { "w", "w", "w", "w", "w", "w", "w", "w", "mmmr", "mmmr", "mmmr", "w" } },
            { "F_down_from_3", new[] { "mmmr", "mmmr", "mmmr", "mmmr", "w", "w", "w", "w", "w", "w", "w", "w" } },
            { "G_clean", new string[0] },
            { "H_bonus_miss_soundcheck", new[] { "mw", "x", "w", "w", "mmmr", "w", "w" } },
        };

        private static readonly Dictionary<string, string[]> EncoreScripts = new Dictionary<string, string[]>
        {
            { "A_one_miss", new[] { "w" } },
            { "C_three_misses", new[] { "w", "mw", "hw" } },
            { "D_late_misses", new[] { "w", "w" } },
            { "H_bonus_miss_soundcheck", new[] { "x" } },
        };

        private static readonly Dictionary<string, int> Sizes = new Dictionary<string, int> { { "H_bonus_miss_soundcheck", 6 } };
        private static readonly Dictionary<string, int> StartLevels = new Dictionary<string, int> { { "F_down_from_3", 3 } };

        public static readonly Pinned[] Cases =
        {
            new Pinned("A_one_miss",
                "w0,0,w,1000,1,1|w1,0,w,3000,2,1|w2,0,mw,3300,0,1|w3,0,w,4300,1,2|w4,0,w,6300,2,2|w5,0,w,9300,3,2|w2,1,w,13300,4,2|w6,0,w,18300,5,3|w7,0,w,23300,6,3|w8,0,w,28300,7,3|w9,0,w,33300,8,3|E|w2,0,w,500,1,3",
                "w0:done w1:done w2:miss w3:done w4:done w5:done w2*:done w6:done w7:done w8:done w9:done;33300;10;0;8;0;;w2;1;10;1;0;0",
                "w2:done;500;1;0;1;0;;;1;1;0;1;0",
                "3;4;0;12;12;0;4/4 4/4 4/4"),
            new Pinned("B_hints",
                "w0,0,w,1000,1,1|w1,0,w,3000,2,1|w2,0,hw,3500,2,1|w3,0,w,6500,3,2|w4,0,hhw,7000,3,2|w5,0,fw,11000,4,2|w6,0,w,16000,5,2|w7,0,w,21000,6,3|w8,0,w,26000,7,3|w9,0,w,31000,8,3",
                "w0:done w1:done w2:done w3:done w4:done w5:done w6:done w7:done w8:done w9:done;31000;8;2;8;3;w2 w4;;0.9;10;1;0;2",
                "",
                "3;2;0;10;10;2;4/4 4/4 2/2"),
            new Pinned("C_three_misses",
                "w0,0,mmmr,0,0,1|w1,0,mw,300,0,1|w2,0,w,1300,1,1|w3,0,mw,1600,0,1|w0,1,w,2600,1,2|w1,1,w,4600,2,2|w4,0,w,7600,3,2|w5,0,w,11600,4,2|w6,0,w,16600,5,3|w7,0,w,21600,6,3|w8,0,w,26600,7,3|w9,0,w,31600,8,3|E|w0,0,w,500,1,3|w1,0,mw,650,0,3|w3,0,hw,900,0,3",
                "w0:miss w1:miss w2:done w3:miss w0*:done w1*:done w4:done w5:done w6:done w7:done w8:done w9:done;31600;9;0;8;0;;w0 w1 w3;0.9;10;1;0;0",
                "w0:done w1:miss w3:done;900;1;1;1;1;w3;w1;0.5;3;0;1;1",
                "3;6;0;15;14;1;5/4 4/4 6/6"),
            new Pinned("D_late_misses",
                "w0,0,w,1000,1,1|w1,0,w,3000,2,1|w2,0,w,6000,3,1|w3,0,w,10000,4,2|w4,0,w,15000,5,2|w5,0,w,20000,6,2|w6,0,w,25000,7,2|w7,0,w,30000,8,3|w8,0,mw,30300,0,3|w9,0,mmmr,30300,0,3|w8,1,w,31300,1,3|w9,1,w,33300,2,3|E|w8,0,w,500,1,3|w9,0,w,1500,2,3",
                "w0:done w1:done w2:done w3:done w4:done w5:done w6:done w7:done w8:miss w9:miss w8*:done w9*:done;33300;10;0;8;0;;w8 w9;1;10;1;0;0",
                "w8:done w9:done;1500;2;0;2;0;;;1;2;0;1;0",
                "3;4;0;14;13;0;4/4 4/4 6/5"),
            new Pinned("E_levels",
                "w0,0,w,1000,1,1|w1,0,w,3000,2,1|w2,0,w,6000,3,1|w3,0,w,10000,4,2|w4,0,w,15000,5,2|w5,0,w,20000,6,2|w6,0,w,25000,7,2|w7,0,w,30000,8,3|w8,0,mmmr,30000,0,3|w9,0,mmmr,30000,0,2|w8,1,mmmr,30000,0,2|w9,1,w,31000,1,2",
                "w0:done w1:done w2:done w3:done w4:done w5:done w6:done w7:done w8:miss w9:miss w8*:miss w9*:done;31000;9;0;8;0;;w8 w9;0.9;10;1;0;0",
                "",
                "2;1;0;12;9;0;4/4 6/5 2/0"),
            new Pinned("F_down_from_3",
                "w0,0,mmmr,0,0,3|w1,0,mmmr,0,0,2|w2,0,mmmr,0,0,2|w3,0,mmmr,0,0,1|w0,1,w,1000,1,1|w1,1,w,3000,2,1|w4,0,w,6000,3,1|w5,0,w,10000,4,2|w6,0,w,15000,5,2|w7,0,w,20000,6,2|w8,0,w,25000,7,2|w9,0,w,30000,8,3",
                "w0:miss w1:miss w2:miss w3:miss w0*:done w1*:done w4:done w5:done w6:done w7:done w8:done w9:done;30000;8;0;8;0;;w0 w1 w2 w3;0.8;10;0;0;0",
                "",
                "3;0;0;12;8;0;4/4 6/4 2/0"),
            new Pinned("G_clean",
                "w0,0,w,1000,1,1|w1,0,w,3000,2,1|w2,0,w,6000,3,1|w3,0,w,10000,4,2|w4,0,w,15000,5,2|w5,0,w,20000,6,2|w6,0,w,25000,7,2|w7,0,w,30000,8,3|w8,0,w,35000,9,3|w9,0,w,40000,10,3",
                "w0:done w1:done w2:done w3:done w4:done w5:done w6:done w7:done w8:done w9:done;40000;10;0;10;0;;;1;10;1;0;0",
                "",
                "3;2;0;10;10;0;4/4 4/4 2/2"),
            new Pinned("H_bonus_miss_soundcheck",
                "w0,0,mw,300,0,1|w1,0,x,1550,1,1|w2,0,w,3550,2,1|w3,0,w,6550,3,2|w0,1,mmmr,6550,0,2|w4,0,w,7550,1,2|w5,0,w,9550,2,2|E|w0,0,x,750,1,2",
                "w0:miss w1:done w2:done w3:done w0*:miss w4:done w5:done;9550;5;0;3;0;;w0;0.8333333333333334;6;0;0;0",
                "w0:done;750;1;0;1;0;;;1;1;0;1;0",
                "2;3;0;8;7;0;4/4 4/3 0/0"),
        };

        [Test, TestCaseSource(nameof(Cases))]
        public void ScriptedShow_MatchesTheWebGame(Pinned c)
        {
            int size;
            if (!Sizes.TryGetValue(c.Name, out size)) size = 10;
            var rec = new SkillRecord();
            int lv;
            if (StartLevels.TryGetValue(c.Name, out lv)) rec.Level = lv;

            var d = new ShowTestDriver();
            var show = ShowTestDriver.Spelling(ShowTestDriver.Words(size), rec);
            d.Play(show, Scripts[c.Name]);
            ShowTestDriver.AssertSummary(c.Show, show, c.Name);

            string[] enc;
            if (EncoreScripts.TryGetValue(c.Name, out enc))
            {
                Assert.IsTrue(show.Summary.CanEncore, c.Name + " offers an encore");
                var encore = show.CreateEncore(ShowTestDriver.Random(5));
                d.Log.Add("E");
                d.Play(encore, enc);
                ShowTestDriver.AssertSummary(c.Encore, encore, c.Name + " encore");
            }
            else
            {
                Assert.AreEqual("", c.Encore);
            }

            Assert.AreEqual(c.Log, string.Join("|", d.Log), c.Name + " song by song");
            Assert.AreEqual(c.Skill, ShowTestDriver.DescribeSkill(rec), c.Name + " skill record");
        }

        [Test]
        public void ScriptedShow_AllCasesAreScripted()
        {
            CollectionAssert.AreEquivalent(Cases.Select(x => x.Name), Scripts.Keys);
        }
    }
}
