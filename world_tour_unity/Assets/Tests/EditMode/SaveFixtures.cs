using System.Collections.Generic;
using System.Globalization;
using WorldTour.Core.Data;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;
using WorldTour.Core.Progress;
using WorldTour.Core.Shows;

namespace WorldTour.Core.Tests
{
    /// <summary>Saves for the save and progress tests.</summary>
    internal static class SaveFixtures
    {
        public static readonly string[] Skills = { "dec", "md", "frac", "xy", "vol", "ops", "flu" };
        public static readonly string[] SpellModes = { "slots", "vowels", "remix", "fanmail", "studio", "lyric", "prefix" };

        /// <summary>
        /// A heavy, realistic save after a long year of play: every venue, every skill, 300 words, the review
        /// queue full (40 whole problems, ops level 3 text choices included), 200 shows (the cap), 120 days of
        /// time (the cap), a big wardrobe and a full journal.
        /// </summary>
        public static SaveBlob Full()
        {
            var s = SaveBlob.Defaults("2025-11-20");
            s.Updated = 1795158977703;
            ProgressRules.AddFans(s, 1234567);
            s.Fans.Balance = 345678;
            foreach (Venue v in Venues.All) ProgressRules.MarkSeen(s, v.Id);
            s.Streak = new StreakData { Days = 12, Best = 31, Last = "2026-11-19" };
            var rng = new Rng(4242);
            foreach (string id in Skills) Fill(ProgressRules.Skill(s, id), rng, true);
            foreach (string m in SpellModes) Fill(ProgressRules.Skill(s, "spell:" + m), rng, false);
            for (int i = 0; i < 300; i++)
                s.Words["wordnumber" + i] = new WordStat { A = rng.Int(1, 60), C = rng.Int(0, 50), First = rng.Int(0, 40), H = rng.Int(0, 9), Miss = rng.Int(0, 9), Last = "2026-10-" + (10 + i % 20).ToString(CultureInfo.InvariantCulture) };
            for (int i = 0; i < 30; i++) s.Review.Words.Add("wordnumber" + i);
            for (int i = 0; i < Review.Max; i++)
            {
                string sk = Skills[i % Skills.Length];
                int lv = sk == "ops" ? 3 : 1 + i % 3;
                var item = new MathItem(sk, lv, 1000 + i, Generators.Generate(sk, lv, 1000 + i));
                ProgressRules.RecordMath(s, sk, item, false, true, false);
            }
            for (int i = 0; i < 260; i++)
            {
                var sum = new ShowSummary { Mode = i % 2 == 0 ? "remix" : "merch", Venue = Venues.All[i % 8].Id, N = 10, First = rng.Int(4, 10), Hinted = rng.Int(0, 4), Fans = rng.Int(3000, 40000), Secs = rng.Int(120, 900), Best = rng.Int(1, 10), Sold = i % 3 == 0, Encore = i % 5 == 0 };
                ProgressRules.FinishShow(s, sum, "2026-" + (1 + i % 12).ToString("00", CultureInfo.InvariantCulture) + "-" + (1 + i % 28).ToString("00", CultureInfo.InvariantCulture));
            }
            for (int d = 0; d < 150; d++) ProgressRules.AddPlayTime(s, "2026-" + (1 + d / 28).ToString("00", CultureInfo.InvariantCulture) + "-" + (1 + d % 28).ToString("00", CultureInfo.InvariantCulture), 1800);
            for (int i = 0; i < 40; i++) s.Inventory.Owned.Add("dog:outfitnumber" + i);
            s.Inventory.Equipped["dog"] = "outfitnumber3";
            for (int i = 0; i < 25; i++)
            {
                s.Journal.Stickers["milestone-" + i] = "2026-0" + (1 + i % 9).ToString(CultureInfo.InvariantCulture) + "-15";
                s.Journal.Layout["milestone-" + i] = new Spot { X = .123 + i * .03, Y = .456, R = -7 + i % 14 };
            }
            return s;
        }

        /// <summary>A file next to the tests: Assets/Tests/EditMode in Unity, B15_TESTDATA in the scratch harness.</summary>
        public static string TestFile(string name)
        {
#if UNITY_EDITOR
            string dir = System.IO.Path.Combine(UnityEngine.Application.dataPath, "Tests", "EditMode");
#else
            string dir = System.Environment.GetEnvironmentVariable("B15_TESTDATA") ?? ".";
#endif
            return System.IO.Path.Combine(dir, name);
        }

        private static void Fill(SkillRecord k, Rng rng, bool math)
        {
            for (int i = 0; i < 200; i++) LevelRules.Record(k, rng.Chance(.8), rng.Chance(.2), math && rng.Chance(.1) ? "no-borrow" : null);
        }
    }
}
