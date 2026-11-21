using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Maths;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;
using WorldTour.Core.Shows;
using WorldTour.Game.Modes;

namespace WorldTour.Game.EditTests
{
    /// <summary>Phase 7, QUESTIONS 24: a math skill is maxed for the max-level stickers at its config MAX_LEVEL, not always 3.</summary>
    public class StickerMaxLevelTests
    {
        private static SkillRecord At(int level, int tried, int right)
        {
            var k = new SkillRecord { Level = level };
            k.ByLevel[level] = new[] { tried, right };
            return k;
        }

        private static MathConfig Config(bool fracOn)
        {
            MathConfig c = MathConfig.WebDefaults();
            c.ENABLED_SKILLS.frac = fracOn;
            c.LEVELS = new SkillLevels();
            c.LEVELS.md = new LevelLimit { MAX_LEVEL = 1 };
            c.LEVELS.ops = new LevelLimit { MAX_LEVEL = 2 };
            return c;
        }

        private static List<string> Earned(SaveBlob s, MathConfig c) => MetaRules.Earn(s, ModeList.All(c), "d").Select(m => m.Id).ToList();

        [Test]
        public void ModeListCarriesTheConfigCap()
        {
            List<ModeInfo> all = ModeList.All(Config(true));
            Assert.AreEqual(1, all.First(m => m.Skill == "md").MaxLevel);
            Assert.AreEqual(2, all.First(m => m.Skill == "ops").MaxLevel);
            Assert.AreEqual(3, all.First(m => m.Skill == "dec").MaxLevel, "no entry means the default cap, 3");
            Assert.AreEqual(3, all.First(m => m.Kind == ModeInfo.Spelling).MaxLevel);
        }

        [Test]
        public void TenRightAtTheCappedLevelEarnsTheMathMaxStickers()
        {
            SaveBlob s = SaveBlob.Defaults("2026-11-18");
            s.Skills["md"] = At(1, 12, 10);
            List<string> ids = Earned(s, Config(true));
            Assert.IsTrue(ids.Contains("master-math"));
            Assert.IsTrue(ids.Contains("master-any"));
            Assert.IsFalse(ids.Contains("master-spell"));
        }

        [Test]
        public void NineRightOrTheWrongLevelDoesNot()
        {
            SaveBlob s = SaveBlob.Defaults("2026-11-18");
            s.Skills["md"] = At(1, 9, 9);
            s.Skills["ops"] = At(1, 30, 30);   // ops caps at 2, so level 1 is not maxed
            CollectionAssert.IsEmpty(Earned(s, Config(true)));
        }

        [Test]
        public void ALevelThreeSkillStillNeedsTenRightAtThree()
        {
            SaveBlob s = SaveBlob.Defaults("2026-11-18");
            s.Skills["dec"] = At(3, 10, 9);
            CollectionAssert.IsEmpty(Earned(s, Config(true)));
            s.Skills["dec"] = At(3, 10, 10);
            Assert.IsTrue(Earned(s, Config(true)).Contains("master-math"));
        }

        [Test]
        public void OnlyEnabledSkillsCount()
        {
            SaveBlob s = SaveBlob.Defaults("2026-11-18");
            s.Skills["frac"] = At(3, 12, 12);
            Assert.IsTrue(Earned(s, Config(true)).Contains("master-math"));
            CollectionAssert.IsEmpty(Earned(s, Config(false)), "frac is switched off, so its level counts for nothing");
        }

        [Test]
        public void AnEarnedStickerIsKeptWhenTheCapChanges()
        {
            SaveBlob s = SaveBlob.Defaults("2026-11-18");
            s.Skills["md"] = At(1, 12, 10);
            foreach (Milestone m in MetaRules.Earn(s, ModeList.All(Config(true)), "d")) Assert.IsTrue(MetaRules.HasSticker(s, m.Id), m.Id);
            MathConfig tighter = Config(true); tighter.LEVELS.md = new LevelLimit { MAX_LEVEL = 0 };
            Assert.IsEmpty(MetaRules.Earn(s, ModeList.All(tighter), "d"), "nothing new, and nothing removed");
            Assert.IsTrue(MetaRules.HasSticker(s, "master-math"));
        }
    }
}
