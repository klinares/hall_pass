using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Data;
using WorldTour.Core.Shows;

namespace WorldTour.Core.Tests
{
    /// <summary>
    /// QUESTIONS 11: before the final reveal no spelling hint shows the whole word; each leaves at least a third of the
    /// letters hidden and never fewer than two. Checked against every word in the live weeks.json (so it holds for weeks
    /// added later) and against made-up words of every length.
    /// </summary>
    public class SpellingHintsTests
    {
        private static List<Word> LiveWords()
        {
            WeeksLoad load = DataFiles.LoadWeeks(DataTestPaths.Read(Path.Combine(DataTestPaths.StreamingAssets, "weeks.json")));
            Assert.IsNotNull(load.File, "weeks.json did not load");
            var words = load.File.weeks.SelectMany(w => w.words).Where(v => v != null && !string.IsNullOrEmpty(v.w)).ToList();
            Assert.IsNotEmpty(words);
            return words;
        }

        private static void CheckChunks(string w, string s)
        {
            string c = SpellingHints.Chunks(w, s);
            string at = w + " (" + s + ") -> \"" + c + "\"";
            Assert.IsFalse(c.Replace(" - ", "").Contains(w), "shows the whole word: " + at);
            if (w.Length > SpellingHints.ShortWord)
                Assert.GreaterOrEqual(SpellingHints.HiddenIn(w, c), SpellingHints.MinHidden(w.Length), "too few letters hidden: " + at);
            else
                Assert.AreEqual(SpellingHints.FirstAndLength(w), c, "a short word shows only its first letter and length: " + at);
        }

        [Test]
        public void SoundItOut_HidesEnough_ForEveryLiveWord()
        {
            foreach (Word v in LiveWords()) CheckChunks(v.w, v.s);
        }

        [Test]
        public void LetterByLetterHints_LeaveTheLastLettersHidden_ForEveryLiveWord()
        {
            foreach (Word v in LiveWords())
            {
                int n = v.w.Length, shown = Enumerable.Range(0, n).Count(i => SpellingHints.CanReveal(i, n));
                Assert.GreaterOrEqual(n - shown, SpellingHints.MinHidden(n), v.w);
                Assert.IsFalse(SpellingHints.CanReveal(n - 1, n), v.w + ": the last letter is hers to find");
                Assert.IsFalse(SpellingHints.CanReveal(n - 2, n), v.w + ": so is the one before it");
                Assert.IsTrue(SpellingHints.CanReveal(0, n), v.w + ": the first letter can be shown");
            }
        }

        [Test]
        public void TheFloor_ForEveryLength()
        {
            Assert.AreEqual(2, SpellingHints.MinHidden(3));
            Assert.AreEqual(2, SpellingHints.MinHidden(6));
            Assert.AreEqual(3, SpellingHints.MinHidden(7));
            Assert.AreEqual(5, SpellingHints.MinHidden(13));
            for (int n = 1; n <= 30; n++)
            {
                Assert.GreaterOrEqual(n - SpellingHints.MaxShown(n), System.Math.Min(n, SpellingHints.MinHidden(n)), "length " + n);
                Assert.GreaterOrEqual(SpellingHints.MinHidden(n) * 3, n, "a third, rounded up: " + n);
            }
        }

        [Test]
        public void SoundItOut_HidesEnough_ForMadeUpWordsOfEveryShape()
        {
            // every split of words from 2 to 14 letters into 1 to 5 syllables of at least one letter
            for (int n = 2; n <= 14; n++)
            {
                string w = new string(Enumerable.Range(0, n).Select(i => (char)('a' + i % 26)).ToArray());
                CheckChunks(w, null);
                foreach (List<int> split in Splits(n, 5))
                {
                    var parts = new List<string>();
                    int at = 0;
                    foreach (int len in split) { parts.Add(w.Substring(at, len)); at += len; }
                    CheckChunks(w, string.Join("-", parts));
                }
            }
        }

        private static IEnumerable<List<int>> Splits(int n, int maxParts)
        {
            if (n == 0) { yield return new List<int>(); yield break; }
            if (maxParts == 0) yield break;
            for (int first = 1; first <= n; first++)
                foreach (List<int> rest in Splits(n - first, maxParts - 1))
                {
                    var l = new List<int> { first };
                    l.AddRange(rest);
                    yield return l;
                }
        }

        [Test]
        public void TheValidator_WarnsAboutWordsTooShortToHide()
        {
            WeeksLoad load = DataFiles.LoadWeeks(DataTestPaths.Sample("weeks_short_word.json"));
            var warn = load.Findings.Where(f => !f.IsError).Select(f => f.Message).ToList();
            Assert.IsTrue(warn.Any(m => m.Contains("word \"fun\": it is only 3 letters long")), string.Join(" || ", warn));
            Assert.IsTrue(warn.Any(m => m.Contains("word \"ox\": it is only 2 letters long")), string.Join(" || ", warn));
            Assert.IsFalse(warn.Any(m => m.Contains("\"funny\"")), "five letters are fine");
            Assert.IsFalse(load.Findings.Any(f => f.IsError), "a warning only: the week still loads");
            Assert.IsFalse(DataFiles.LoadWeeks(DataTestPaths.Read(Path.Combine(DataTestPaths.StreamingAssets, "weeks.json"))).Findings.Any(f => f.Message.Contains("almost nothing to hide")),
                "the live words are all long enough");
        }
    }
}
