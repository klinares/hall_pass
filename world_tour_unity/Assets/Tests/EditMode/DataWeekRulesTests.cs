using System;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Data;

namespace WorldTour.Core.Tests
{
    [TestFixture]
    public class DataWeekRulesTests
    {
        static Week Wk(string id) => new Week { id = id, label = id, words = new[] { new Word { w = "a", s = "a", h = "a" } } };
        // deliberately not newest first, to prove the rules sort
        static readonly Week[] Weeks = { Wk("2026-10-10"), Wk("2026-11-09"), Wk("2026-10-26") };

        [TestCase("2026-10-26", "2026-10-26")]   // on the test date itself
        [TestCase("2026-11-01", "2026-10-26")]   // between weeks: the newest one already started
        [TestCase("2026-11-20", "2026-11-09")]
        [TestCase("2027-01-01", "2026-11-09")]   // after all: the newest
        [TestCase("2026-10-09", "2026-10-10")]   // before all: the oldest
        public void DefaultWeekIsNewestOnOrBeforeToday(string today, string expected)
        {
            Assert.AreEqual(expected, WeekRules.DefaultWeek(Weeks, today).id);
        }

        [Test]
        public void SortedNewestFirst()
        {
            CollectionAssert.AreEqual(new[] { "2026-11-09", "2026-10-26", "2026-10-10" }, WeekRules.SortedNewestFirst(Weeks).Select(w => w.id).ToList());
            Assert.IsNull(WeekRules.DefaultWeek(new Week[0], "2026-11-20"));
            Assert.IsNull(WeekRules.DefaultWeek(null, "2026-11-20"));
        }

        [Test]
        public void ChooseWeekUsesThePickOrFallsBack()
        {
            Assert.AreEqual("2026-10-10", WeekRules.ChooseWeek(Weeks, "2026-10-10", "2026-11-20").id);
            Assert.AreEqual("2026-11-09", WeekRules.ChooseWeek(Weeks, "1999-01-01", "2026-11-20").id);
            Assert.AreEqual("2026-11-09", WeekRules.ChooseWeek(Weeks, null, "2026-11-20").id);
        }

        [Test]
        public void ToIsoWritesLocalDate()
        {
            Assert.AreEqual("2026-03-07", WeekRules.ToIso(new DateTime(2026, 3, 7, 23, 59, 0, DateTimeKind.Local)));
        }

        static Word P(string w, string p, string root, string means) => new Word { w = w, s = w, h = "h", prefix = new Prefix { p = p, root = root, means = means } };
        static Word X(string w, string x) => new Word { w = w, s = w, h = "h", x = x };

        [Test]
        public void PrefixMixerNeedsFourFullPrefixes()
        {
            var three = new Week { id = "2026-11-23", label = "x", words = new[] { P("undo", "un", "do", "not"), P("redo", "re", "do", "again"), P("preview", "pre", "view", "before"), P("unfair", "un", "fair", "") } };
            Assert.AreEqual(3, WeekRules.WithPrefix(three).Count, "a prefix with an empty part does not count (prefix.js:10)");
            Assert.IsFalse(WeekRules.PrefixMixerAvailable(three));
            three.words[3].prefix.means = "not";
            Assert.IsTrue(WeekRules.PrefixMixerAvailable(three));
        }

        [Test]
        public void LyricWriterNeedsFourSentencesWithABlank()
        {
            var wk = new Week { id = "2026-11-23", label = "x", words = new[] { X("a", "___ one"), X("b", "two ___"), X("c", "th___ree"), X("d", "no blank"), new Word { w = "e", s = "e", h = "h" } } };
            Assert.AreEqual(3, WeekRules.WithSentence(wk).Count);
            Assert.IsFalse(WeekRules.LyricWriterAvailable(wk));
            wk.words[3].x = "now ___";
            Assert.IsTrue(WeekRules.LyricWriterAvailable(wk));
        }

        [Test]
        public void RealWordListModes()
        {
            WeeksFile f = DataFiles.LoadWeeks(DataTestPaths.Read(System.IO.Path.Combine(DataTestPaths.EditMode, "..", "..", "StreamingAssets", "weeks.json"))).File;
            Week oct26 = f.weeks.First(w => w.id == "2026-10-26"), oct10 = f.weeks.First(w => w.id == "2026-10-10");
            Assert.AreEqual(10, WeekRules.WithPrefix(oct26).Count);
            Assert.IsTrue(WeekRules.PrefixMixerAvailable(oct26));
            Assert.IsFalse(WeekRules.PrefixMixerAvailable(oct10), "CONTENT_REVIEW.md: oct 10 has 0 prefixes, so prefix mixer is hidden");
            Assert.IsTrue(WeekRules.LyricWriterAvailable(oct26));
            Assert.IsTrue(WeekRules.LyricWriterAvailable(oct10));
        }
    }
}
