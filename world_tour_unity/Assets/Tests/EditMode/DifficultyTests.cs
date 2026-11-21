using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Data;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;
using WorldTour.Core.Progress;
using WorldTour.Core.Shows;

namespace WorldTour.Core.Tests
{
    /// <summary>
    /// Phase 7.1, difficulty controls: the math_config.json LEVELS shape (caps, start levels, run lengths), the level
    /// policy (caps never rewrite a saved level, gentle mode), the warm-up level 0, and the gentle switches in the save.
    /// </summary>
    public class DifficultyTests
    {
        private const string Shipped = @"{
  ""ENABLED_SKILLS"": { ""dec"": false, ""md"": true, ""frac"": false, ""xy"": false, ""vol"": true, ""ops"": true, ""flu"": true },
  ""BOUNDS"": { ""moneyCents"": 10000, ""maxDenominator"": 12, ""coordMax"": 10, ""maxVolume"": 1000, ""storyDollars"": 150 },
  ""LEVEL_UP_AFTER"": 6, ""LEVEL_DOWN_AFTER"": 2,
  ""LEVELS"": { ""md"": { ""MAX_LEVEL"": 1, ""START_LEVEL"": 0 }, ""ops"": { ""MAX_LEVEL"": 2 }, ""flu"": { ""MAX_LEVEL"": 2 }, ""vol"": { } }
}";
        private const string Plain = @"{
  ""ENABLED_SKILLS"": {}, ""BOUNDS"": { ""moneyCents"": 10000, ""maxDenominator"": 12, ""coordMax"": 10, ""maxVolume"": 1000, ""storyDollars"": 150 }
}";

        private static MathConfig Load(string text)
        {
            MathConfigLoad l = DataFiles.LoadMathConfig(text);
            Assert.IsFalse(l.HasErrors, string.Join("; ", l.Findings.Select(f => f.Message)));
            return l.Config;
        }

        // ---------- the file ----------

        [Test]
        public void Config_ReadsCapsStartsAndRunLengths_AndLeavesEverythingElseAtTheDefaults()
        {
            MathConfig c = Load(Shipped);
            Assert.AreEqual(6, c.LEVEL_UP_AFTER);
            Assert.AreEqual(2, c.LEVEL_DOWN_AFTER);
            Assert.AreEqual(1, c.LimitsFor("md").MAX_LEVEL);
            Assert.AreEqual(2, c.LimitsFor("ops").MAX_LEVEL);
            Assert.AreEqual(2, c.LimitsFor("flu").MAX_LEVEL);
            Assert.AreEqual(3, c.LimitsFor("vol").MAX_LEVEL, "an empty block means no cap");
            Assert.AreEqual(0, c.LimitsFor("ops").START_LEVEL);
            Assert.AreEqual(3, c.LimitsFor("dec").MAX_LEVEL, "a skill left out has no cap");
            Assert.AreEqual(3, c.LimitsFor("nope").MAX_LEVEL, "an unknown id has no cap");
        }

        [Test]
        public void Config_WithNoLevelKeys_UsesSixAndTwoAndNoCaps()
        {
            MathConfig c = Load(Plain);
            Assert.AreEqual(6, c.LEVEL_UP_AFTER);
            Assert.AreEqual(2, c.LEVEL_DOWN_AFTER);
            foreach (string skill in Generators.Skills)
            {
                Assert.AreEqual(3, c.LimitsFor(skill).MAX_LEVEL, skill);
                Assert.AreEqual(0, c.LimitsFor(skill).START_LEVEL, skill);
            }
            Assert.AreEqual(6, MathConfig.WebDefaults().LEVEL_UP_AFTER);
        }

        [Test]
        public void Config_ProblemsAreReadable_AndAStartAboveTheCapOnlyWarns()
        {
            MathConfigLoad bad = DataFiles.LoadMathConfig(Plain.Replace("\"ENABLED_SKILLS\": {},", "\"ENABLED_SKILLS\": {}, \"LEVELS\": { \"md\": { \"MAX_LEVEL\": 4 } },"));
            Assert.IsTrue(bad.HasErrors);
            Assert.IsTrue(bad.Findings.Any(f => f.Message == "LEVELS.md: \"MAX_LEVEL\" should be a whole number from 0 to 3, but it is 4"), string.Join("\n", bad.Findings.Select(f => f.Message)));
            Assert.IsTrue(bad.UsedDefaults);
            MathConfigLoad many = DataFiles.LoadMathConfig(Plain.Replace("\"ENABLED_SKILLS\": {},", "\"ENABLED_SKILLS\": {}, \"LEVEL_UP_AFTER\": 30,"));
            Assert.IsTrue(many.Findings.Any(f => f.Message == "LEVEL_UP_AFTER must be 20 or less (it is 30)."));
            MathConfigLoad warn = DataFiles.LoadMathConfig(Plain.Replace("\"ENABLED_SKILLS\": {},", "\"ENABLED_SKILLS\": {}, \"LEVELS\": { \"md\": { \"MAX_LEVEL\": 1, \"START_LEVEL\": 2 } },"));
            Assert.IsFalse(warn.HasErrors, "a start above the cap still loads");
            Assert.AreEqual(1, warn.Findings.Count(f => f.Severity == Severity.Warning));
            Assert.AreEqual(1, LevelPolicy.ForMath(warn.Config, "md", false).Start, "and the game starts it at the cap");
        }

        // ---------- the policy ----------

        [Test]
        public void Policy_ForMath_ReadsTheConfig_AndGentleLowersTheCapAndAsksForEight()
        {
            MathConfig c = Load(Shipped);
            LevelPolicy md = LevelPolicy.ForMath(c, "md", false);
            Assert.AreEqual((0, 1, 0, 6, 2), (md.Floor, md.Cap, md.Start, md.UpAfter, md.DownAfter));
            Assert.AreEqual(3, LevelPolicy.ForMath(c, "vol", false).Cap);
            LevelPolicy gentle = LevelPolicy.ForMath(c, "vol", true);
            Assert.AreEqual((0, 2, 0, 8, 2), (gentle.Floor, gentle.Cap, gentle.Start, gentle.UpAfter, gentle.DownAfter));
            Assert.AreEqual(0, LevelPolicy.ForMath(c, "md", true).Cap, "one lower than md's cap of 1 is the warm-up");
            Assert.AreEqual(8, LevelPolicy.ForMath(null, "md", true).UpAfter);
            Assert.AreEqual(6, LevelPolicy.ForMath(null, "md", false).UpAfter, "no config: the defaults");
            c.LEVEL_UP_AFTER = 12;
            Assert.AreEqual(12, LevelPolicy.ForMath(c, "vol", true).UpAfter, "gentle never asks for less than the file does");
        }

        [Test]
        public void Policy_Spelling_KeepsTheWebRules_AndGentleCapsAtTwo()
        {
            LevelPolicy s = LevelPolicy.ForSpelling(false);
            Assert.AreEqual((1, 3, 1, 4, 2), (s.Floor, s.Cap, s.Start, s.UpAfter, s.DownAfter));
            LevelPolicy g = LevelPolicy.ForSpelling(true);
            Assert.AreEqual((1, 2, 1, 8, 2), (g.Floor, g.Cap, g.Start, g.UpAfter, g.DownAfter));
        }

        [Test]
        public void Policy_ForSkill_ReadsTheSavedSwitchPerSubject()
        {
            SaveBlob s = SaveBlob.Defaults("2026-11-21");
            s.Settings.GentleMath = true;
            MathConfig c = Load(Plain);
            Assert.AreEqual(8, ProgressRules.PolicyFor(s, "ops", c).UpAfter);
            Assert.AreEqual(4, ProgressRules.PolicyFor(s, "spell:remix", c).UpAfter, "spelling is not gentle until its own switch is on");
            s.Settings.GentleSpelling = true;
            Assert.AreEqual(8, ProgressRules.PolicyFor(s, "spell:remix", c).UpAfter);
        }

        // ---------- moving ----------

        private static int Right(SkillRecord k, LevelPolicy p, int n)
        {
            int m = 0;
            for (int i = 0; i < n; i++) m += LevelRules.Record(k, true, false, null, p);
            return m;
        }

        private static int Wrong(SkillRecord k, LevelPolicy p, int n)
        {
            int m = 0;
            for (int i = 0; i < n; i++) m += LevelRules.Record(k, false, false, null, p);
            return m;
        }

        [Test]
        public void Record_UpAfterSix_DownAfterTwo_StopsAtTheCapAndTheWarmUp()
        {
            LevelPolicy p = LevelPolicy.ForMath(Load(Shipped), "ops", false);   // cap 2
            var k = new SkillRecord { Level = 0 };
            Assert.AreEqual(0, Right(k, p, 5));
            Assert.AreEqual(0, k.Level, "five is not enough");
            Assert.AreEqual(1, Right(k, p, 1));
            Assert.AreEqual(1, k.Level);
            Assert.AreEqual(1, Right(k, p, 6));
            Assert.AreEqual(2, k.Level);
            Assert.AreEqual(0, Right(k, p, 20), "the cap holds");
            Assert.AreEqual(2, k.Level);
            Assert.AreEqual(-1, Wrong(k, p, 2));
            Assert.AreEqual(1, k.Level);
            Wrong(k, p, 2);
            Assert.AreEqual(0, k.Level);
            Assert.AreEqual(0, Wrong(k, p, 5), "level 0 is the floor");
            Assert.AreEqual(0, k.Level);
            Assert.IsTrue(k.ByLevel.ContainsKey(0) && k.ByLevel[0][0] > 0, "the warm-up is counted under level 0");
        }

        [Test]
        public void Record_ASavedLevelAboveTheCap_PlaysAtTheCap_AndIsNotRewrittenByRightAnswers()
        {
            LevelPolicy p = LevelPolicy.ForMath(Load(Shipped), "md", false);    // cap 1
            var k = new SkillRecord { Level = 3 };
            Assert.AreEqual(1, p.Effective(k.Level));
            Assert.AreEqual(0, Right(k, p, 30));
            Assert.AreEqual(3, k.Level, "nothing she earned is removed by the cap");
            Assert.AreEqual(30, k.ByLevel[1][0], "the answers count at the level she played");
            // two misses move her down from where she plays, not from where she was saved
            Assert.AreEqual(-1, Wrong(k, p, 2));
            Assert.AreEqual(0, k.Level);
        }

        [Test]
        public void Record_GentleAsksForEight_AndTheWebRulesAreUntouchedWithoutAPolicy()
        {
            LevelPolicy g = LevelPolicy.ForSpelling(true);
            var k = new SkillRecord();
            Assert.AreEqual(0, Right(k, g, 7));
            Assert.AreEqual(1, Right(k, g, 1));
            Assert.AreEqual(2, k.Level);
            Assert.AreEqual(0, Right(k, g, 30), "gentle spelling tops out at 2");
            var web = new SkillRecord();
            Assert.AreEqual(0, Right(web, null, 3));
            Assert.AreEqual(1, Right(web, null, 1), "no policy: 4 in a row, as in the web game");
            Right(web, null, 40);
            Assert.AreEqual(3, web.Level);
        }

        [Test]
        public void Untouched_AndStartLevel_AndPlayedLevelNeverCreatesARecord()
        {
            MathConfig c = Load(Shipped);
            LevelPolicy p = LevelPolicy.ForMath(c, "ops", false);
            SaveBlob s = SaveBlob.Defaults("2026-11-21");
            Assert.AreEqual(0, ProgressRules.PlayedLevel(s, "ops", p));
            Assert.IsFalse(s.Skills.ContainsKey("ops"), "looking at the map must not create a record");
            SkillRecord k = ProgressRules.SkillForShow(s, "ops", p);
            Assert.AreEqual(0, k.Level, "a new skill starts at the warm-up");
            s.Skills["flu"] = new SkillRecord();   // what the web game's on-demand record looks like: level 1, nothing played
            Assert.AreEqual(0, ProgressRules.SkillForShow(s, "flu", LevelPolicy.ForMath(c, "flu", false)).Level);
            s.Skills["vol"] = new SkillRecord { Level = 1, A = 12 };
            Assert.AreEqual(1, ProgressRules.SkillForShow(s, "vol", LevelPolicy.ForMath(c, "vol", false)).Level, "a skill she has played keeps its level");
            s.Skills["md"] = new SkillRecord { Level = 3, A = 40 };
            Assert.AreEqual(1, ProgressRules.PlayedLevel(s, "md", LevelPolicy.ForMath(c, "md", false)), "shown at the cap");
            var start1 = new LevelPolicy(0, 3, 1, 6, 2);
            Assert.AreEqual(1, ProgressRules.SkillForShow(SaveBlob.Defaults("2026-11-21"), "ops", start1).Level, "START_LEVEL 1");
        }

        // ---------- the engine ----------

        [Test]
        public void Engine_PlaysAtTheCap_FromTheSkillRecord()
        {
            LevelPolicy p = LevelPolicy.ForMath(Load(Shipped), "md", false);
            var rec = new SkillRecord { Level = 3 };
            var items = new List<ShowItem>();
            for (int i = 0; i < 4; i++) items.Add(new MathItem("md", null, 100 + i));
            var e = new ShowEngine(new ShowSetup { IsMath = true, ModeId = "tickets", Skill = "md", VenueId = "coffee", Items = items, SkillRecord = rec, Policy = p, ClockMs = () => 0 });
            var moves = new List<ShowEvent>();
            e.Raised += ev => { if (ev.Kind == ShowEventKind.LevelChanged) moves.Add(ev); };
            e.Start();
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(1, e.CurrentLevel, "a saved 3 plays at md's cap of 1");
                Assert.AreEqual(1, ((MathItem)e.Current.Item).Prob.Level);
                e.SetupHints(3);
                e.Win();
                e.Next();
            }
            Assert.AreEqual(0, moves.Count);
            Assert.AreEqual(3, rec.Level, "four right answers don't touch the saved level");
        }

        // ---------- the warm-up level ----------

        [Test]
        public void WarmUp_EverySkillHasALevelZero()
        {
            foreach (string skill in Generators.Skills)
            {
                Problem p = Generators.Generate(skill, 0, 12345);
                Assert.AreEqual(0, p.Level, skill);
                Assert.AreEqual(skill, p.Skill);
                GeneratorContract.Repeatable((l, s) => Generators.Generate(skill, l, s), 0, 777);
            }
        }

        // ---------- the save ----------

        [Test]
        public void Save_GentleSwitches_RoundTrip_AreWrittenOnlyWhenOn_AndUnknownKeysSurvive()
        {
            SaveBlob b = SaveBlob.Defaults("2026-11-21");
            string plain = SaveJson.ToText(b);
            StringAssert.DoesNotContain("gentle", plain, "off is not written, so a save without the switches matches the web game's");
            b.Settings.GentleMath = true;
            string text = SaveJson.ToText(b);
            StringAssert.Contains("\"gentleMath\":true", text);
            StringAssert.DoesNotContain("gentleSpelling", text);
            SaveBlob back = SaveJson.Load(text, "2026-11-21").Blob;
            Assert.IsTrue(back.Settings.GentleMath);
            Assert.IsFalse(back.Settings.GentleSpelling);
            back.Settings.GentleSpelling = true;
            SaveBlob again = SaveJson.Load(SaveJson.ToText(back), "2026-11-21").Blob;
            Assert.IsTrue(again.Settings.GentleMath && again.Settings.GentleSpelling);
            // a key from a newer game, next to the new ones, is kept
            string withUnknown = text.Replace("\"gentleMath\":true", "\"gentleMath\":true,\"futureThing\":7");
            SaveBlob kept = SaveJson.Load(withUnknown, "2026-11-21").Blob;
            Assert.IsTrue(kept.Settings.GentleMath);
            StringAssert.Contains("\"futureThing\":7", SaveJson.ToText(kept));
            // a level 0 row in byLevel survives a round trip
            kept.Skills["md"] = new SkillRecord { Level = 0 };
            kept.Skills["md"].ByLevel[0] = new[] { 5, 4 };
            SaveBlob lv = SaveJson.Load(SaveJson.ToText(kept), "2026-11-21").Blob;
            Assert.AreEqual(0, lv.Skills["md"].Level);
            CollectionAssert.AreEqual(new[] { 5, 4 }, lv.Skills["md"].ByLevel[0]);
        }
    }
}
