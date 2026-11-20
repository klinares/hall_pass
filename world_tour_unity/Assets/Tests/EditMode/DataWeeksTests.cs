using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Data;

namespace WorldTour.Core.Tests
{
    /// <summary>Validate() rules on models built in code, and on the sample files (which go through all three steps).</summary>
    [TestFixture]
    public class DataWeeksTests
    {
        static Word W(string w, string s = null, string h = "a clue", string x = null, Prefix p = null) => new Word { w = w, s = s ?? w, h = h, x = x, prefix = p };
        static WeeksFile File(params Week[] weeks) => new WeeksFile { weeks = weeks };
        static Week Wk(string id, params Word[] words) => new Week { id = id, label = "label " + id, words = words };
        static List<string> Errors(WeeksFile f) => f.Validate().Where(x => x.IsError).Select(x => x.Message).ToList();
        static List<string> Warnings(WeeksFile f) => f.Validate().Where(x => !x.IsError).Select(x => x.Message).ToList();

        static void Has(List<string> list, string text)
        {
            Assert.IsTrue(list.Any(m => m.Contains(text)), "expected a finding containing <" + text + ">, got: " + string.Join(" || ", list));
        }

        [Test]
        public void GoodWeekHasNoFindings()
        {
            Assert.IsEmpty(File(Wk("2026-11-23", W("echo", "ech-o"), W("tempo", "tem-po"))).Validate());
        }

        [Test] public void NoWeeks() { Has(Errors(File()), "there are no weeks in the file"); Has(Errors(new WeeksFile()), "there are no weeks"); }
        [Test] public void MissingW() => Has(Errors(File(Wk("2026-11-23", new Word { s = "a", h = "b" }))), "week 2026-11-23, word #1: the word (w) is missing");
        [Test] public void MissingS() => Has(Errors(File(Wk("2026-11-23", new Word { w = "echo", h = "b" }))), "word \"echo\": the syllables (s) are missing");
        [Test] public void EmptyH() => Has(Errors(File(Wk("2026-11-23", new Word { w = "echo", s = "ech-o", h = " " }))), "word \"echo\": the clue (h) is missing");
        [Test] public void EmptyId() => Has(Errors(File(Wk("", W("echo")))), "week #1: the id is missing");
        [Test] public void BadDateShape() => Has(Errors(File(Wk("2026-1-23", W("echo")))), "week 2026-1-23: the id \"2026-1-23\" should be the test date written as YYYY-MM-DD");
        [Test] public void NotARealDate() => Has(Errors(File(Wk("2026-13-01", W("echo")))), "is not a real date");
        [Test] public void LeapDayIsReal() => Assert.IsEmpty(Errors(File(Wk("2028-02-29", W("echo")))));
        [Test] public void NonLeapFeb29() => Has(Errors(File(Wk("2027-02-29", W("echo")))), "is not a real date");
        [Test] public void DuplicateWeekIds() => Has(Errors(File(Wk("2026-11-23", W("a")), Wk("2026-11-23", W("b")))), "a week higher in the file already uses the id 2026-11-23");
        [Test] public void MissingLabel() => Has(Errors(File(new Week { id = "2026-11-23", words = new[] { W("echo") } })), "the label is missing");
        [Test] public void NoWords() => Has(Errors(File(new Week { id = "2026-11-23", label = "x", words = new Word[0] })), "there are no words in this week");
        [Test] public void CapitalWord() => Has(Errors(File(Wk("2026-11-23", W("Echo")))), "the word must be lowercase letters a to z only, or a typed answer can never match it (it has \"E\")");
        [Test] public void DuplicateWord() => Has(Errors(File(Wk("2026-11-23", W("echo"), W("echo")))), "this word is already in this week");
        [Test] public void SentenceWithoutBlank() => Has(Errors(File(Wk("2026-11-23", W("echo", x: "no blank")))), "the sentence (x) has no ___");
        [Test] public void SentenceWithTwoBlanks() => Has(Errors(File(Wk("2026-11-23", W("echo", x: "___ and ___")))), "has 2 blanks");
        [Test] public void PrefixFieldMissing() => Has(Errors(File(Wk("2026-11-23", W("unhappy", p: new Prefix { p = "un", root = "happy" })))), "the prefix is missing \"means\"");
        [Test] public void PrefixDoesNotBuildWord() => Has(Errors(File(Wk("2026-11-23", W("unhappy", p: new Prefix { p = "un", root = "hapy", means = "not" })))), "makes \"unhapy\", not \"unhappy\"");

        [Test]
        public void SyllablesThatDontSpellTheWordAreAWarning()
        {
            WeeksFile f = File(Wk("2026-11-23", W("monument", "mon-u-mint")));
            Assert.IsEmpty(Errors(f));
            Has(Warnings(f), "the syllables \"mon-u-mint\" don't spell the word");
        }

        [Test]
        public void GiveAwaysAreWarnings()
        {
            WeeksFile f = File(Wk("2026-11-23", W("temple", h: "a Temple is sacred", x: "temple ___")));
            Assert.IsEmpty(Errors(f));
            Has(Warnings(f), "the sentence (x) gives the word away");
            Has(Warnings(f), "the clue (h) gives the word away");
        }

        [Test]
        public void TooFewForAModeIsAWarningOnlyWhenSomeAreThere()
        {
            Prefix P(string p, string root) => new Prefix { p = p, root = root, means = "not" };
            WeeksFile three = File(Wk("2026-11-23", W("unable", p: P("un", "able")), W("undo", p: P("un", "do")), W("unfair", p: P("un", "fair"))));
            Has(Warnings(three), "only 3 words have a prefix, so prefix mixer is hidden that week (it needs 4)");
            WeeksFile one = File(Wk("2026-11-23", W("echo", x: "an ___ came back"), W("tempo")));
            Has(Warnings(one), "only 1 word has a sentence (x), so lyric writer is hidden that week");
            Assert.IsEmpty(File(Wk("2026-11-23", W("echo"))).Validate(), "none at all is a week without that mode, not a mistake");
        }

        [Test]
        public void SampleFilesReportLines()
        {
            WeeksLoad r = DataFiles.LoadWeeks(DataTestPaths.Sample("weeks_words.json"));
            Assert.IsTrue(r.HasErrors);
            Assert.IsNull(r.File, "a file with errors is not handed to the game");
            Finding cap = r.Findings.First(x => x.Message.Contains("Capital"));
            Assert.AreEqual(16, cap.Line);
            Assert.AreEqual("weeks.json, line 16: week 2026-11-23, word \"Capital\": the word must be lowercase letters a to z only, or a typed answer can never match it (it has \"C\")", cap.ToString());
        }

        [Test]
        public void GoodSampleLoads()
        {
            WeeksLoad r = DataFiles.LoadWeeks(DataTestPaths.Sample("weeks_good.json"));
            Assert.IsEmpty(r.Findings);
            Assert.AreEqual(2, r.File.weeks.Length);
            Assert.AreEqual(5, r.File.weeks[0].words.Length);
            Assert.IsNull(r.File.weeks[0].words[4].x, "an absent x stays null");
            Assert.IsNull(r.File.weeks[0].words[4].prefix, "an absent prefix stays null");
            Assert.AreEqual("happy", r.File.weeks[0].words[0].prefix.root);
            Assert.IsEmpty(DataFiles.LoadWeeks(DataTestPaths.Sample("weeks_good_bom.json")).Findings, "a byte-order mark and CRLF line ends are fine");
        }

        [Test]
        public void WarningsStillLoad()
        {
            WeeksLoad r = DataFiles.LoadWeeks(DataTestPaths.Sample("weeks_warnings.json"));
            Assert.IsFalse(r.HasErrors);
            Assert.IsNotNull(r.File);
            Assert.IsNotEmpty(r.Findings);
        }

        [Test]
        public void RawKeyAndTypeProblemsStopBeforeMeaningChecks()
        {
            List<string> keys = DataFiles.LoadWeeks(DataTestPaths.Sample("weeks_unknown_keys.json")).Findings.Select(x => x.Message).ToList();
            Has(keys, "word #1: \"W\" is not a field the game knows (did you mean \"w\"? names are case-sensitive)");
            Has(keys, "\"words2\" is not a field the game knows");
            Assert.IsFalse(keys.Any(m => m.Contains("the word (w) is missing")), "one mistake, one message");
            List<string> types = DataFiles.LoadWeeks(DataTestPaths.Sample("weeks_types.json")).Findings.Select(x => x.Message).ToList();
            Has(types, "week #1, word #1: \"w\" should be text in double quotes, but it is 12");
            Has(types, "\"prefix\" should be a { } block, but it is the text \"un\"");
        }

        // ---------- skipping broken weeks (DECISIONS 02:30) ----------

        [Test]
        public void OneBadWeekIsSkippedAndTheGoodOnesLoad()
        {
            WeeksLoad r = DataFiles.LoadWeeks(DataTestPaths.Sample("weeks_one_bad_week.json"));
            Assert.IsFalse(r.Refused);
            Assert.IsTrue(r.HasErrors, "the broken week is still an error to fix");
            CollectionAssert.AreEqual(new[] { "2026-11-23", "2026-11-09" }, r.File.weeks.Select(w => w.id).ToList());
            CollectionAssert.AreEqual(new[] { "week 2026-11-16 (line 58)" }, r.Skipped);
            Has(r.Findings.Select(f => f.Message).ToList(), "week 2026-11-16, word \"echo\": the syllables (s) are missing");
            Assert.AreEqual("weeks.json: the game skips week 2026-11-16 (line 58) until it is fixed; the other weeks load", r.Findings.Last().ToString());
            Assert.IsFalse(r.Findings.Last().IsError);
            Assert.AreEqual("2026-11-09", WeekRules.DefaultWeek(r.File.weeks, "2026-11-20").id, "the skipped week never opens");
        }

        [Test]
        public void AWeekWithAKeyOrTypeProblemIsSkippedToo()
        {
            WeeksLoad r = DataFiles.LoadWeeks(DataTestPaths.Sample("weeks_one_bad_week_raw.json"));
            Assert.AreEqual(2, r.File.weeks.Length);
            CollectionAssert.AreEqual(new[] { "week 2026-11-16 (line 58)" }, r.Skipped);
        }

        [Test]
        public void AllWeeksBadIsRefused()
        {
            WeeksLoad r = DataFiles.LoadWeeks(DataTestPaths.Sample("weeks_all_bad.json"));
            Assert.IsTrue(r.Refused);
            Assert.IsNull(r.File);
            Assert.IsEmpty(r.Skipped);
            Assert.AreEqual("none of the weeks can be used, so the game has no words to show", r.Findings.Last().Message);
            Assert.IsTrue(r.Findings.Last().IsError);
        }

        [Test]
        public void SyntaxErrorIsRefused()
        {
            WeeksLoad r = DataFiles.LoadWeeks(DataTestPaths.Sample("weeks_syntax_missing_comma_list.json"));
            Assert.IsTrue(r.Refused);
            Assert.AreEqual(1, r.Findings.Count);
            Assert.IsTrue(DataFiles.LoadWeeks(DataTestPaths.Sample("weeks_top_array.json")).Refused);
            Assert.IsTrue(DataFiles.LoadWeeks(DataTestPaths.Sample("weeks_no_weeks.json")).Refused);
            Assert.IsTrue(DataFiles.LoadWeeks(DataTestPaths.Sample("weeks_missing_weeks.json")).Refused);
        }

        [Test]
        public void DuplicateIdKeepsTheCopyHigherInTheFile()
        {
            // The owner adds new weeks at the top, and the web game's WEEKS.find(w => w.id === id) (show.js:13) opens the first.
            WeeksLoad r = DataFiles.LoadWeeks(DataTestPaths.Sample("weeks_dup_keeps_first.json"));
            CollectionAssert.AreEqual(new[] { "new copy (top)", "week of nov 9, 2026" }, r.File.weeks.Select(w => w.label).ToList());
            CollectionAssert.AreEqual(new[] { "week 2026-11-23 (line 14)" }, r.Skipped);
        }

        [Test]
        public void DuplicateIdKeepsTheSecondCopyWhenTheFirstIsBroken()
        {
            WeeksLoad r = DataFiles.LoadWeeks(DataTestPaths.Sample("weeks_dup_first_broken.json"));
            CollectionAssert.AreEqual(new[] { "good second copy" }, r.File.weeks.Select(w => w.label).ToList());
            CollectionAssert.AreEqual(new[] { "week 2026-11-23 (line 3)" }, r.Skipped);
        }

        [Test]
        public void UnknownTopLevelKeyIsAnErrorButTheWeeksStillLoad()
        {
            WeeksLoad r = DataFiles.LoadWeeks(DataTestPaths.Sample("weeks_unknown_top_key.json"));
            Assert.IsTrue(r.HasErrors);
            Assert.IsFalse(r.Refused);
            Assert.AreEqual(1, r.File.weeks.Length);
            Assert.IsEmpty(r.Skipped);
        }

        // ---------- fix round 1: w is a-z only, checked without culture lowercasing or Trim ----------

        [TestCase("weeks_w_dotted_capital_i.json", "U+0130")]   // D1: Mono's ToLowerInvariant leaves it as is
        [TestCase("weeks_w_bom_at_end.json", "U+FEFF")]         // D2: JS trim strips it, .NET Trim doesn't
        [TestCase("weeks_w_zero_width_space.json", "U+200B")]
        [TestCase("weeks_w_nbsp.json", "U+00A0")]
        [TestCase("weeks_w_inner_space.json", "a space")]
        [TestCase("weeks_w_digit.json", "\"2\"")]
        public void WordOutsideAToZSkipsItsWeek(string sample, string named)
        {
            WeeksLoad r = DataFiles.LoadWeeks(DataTestPaths.Sample(sample));
            Has(r.Findings.Where(f => f.IsError).Select(f => f.Message).ToList(), "the word must be lowercase letters a to z only, or a typed answer can never match it (it has " + named + ")");
            CollectionAssert.AreEqual(new[] { "week 2026-11-23 (line 3)" }, r.Skipped);
            CollectionAssert.AreEqual(new[] { "2026-11-09" }, r.File.weeks.Select(w => w.id).ToList());
        }

        [Test]
        public void InvisibleCharactersAreNamedInMessages()
        {
            WeeksLoad r = DataFiles.LoadWeeks(DataTestPaths.Sample("weeks_w_zero_width_space.json"));
            Assert.IsTrue(r.Findings[0].Message.StartsWith("week 2026-11-23, word \"interU+200Bcept\":"), r.Findings[0].Message);
            Assert.AreEqual("a" + "U+00A0" + "b", TextRules.Visible("a" + (char)0xA0 + "b"));
            Assert.AreEqual("plain", TextRules.Visible("plain"));
        }

        [Test]
        public void BlankUsesTheJsTrimSet()
        {
            Assert.IsTrue(TextRules.Blank("" + (char)0xA0 + (char)0xFEFF + " 	"));
            Assert.IsFalse(TextRules.Blank("" + (char)0x200B), "U+200B is not trimmed by JS either");
            Assert.IsTrue(DataFiles.LoadWeeks(DataTestPaths.Sample("weeks_label_only_nbsp.json")).Findings.Any(f => f.Message.Contains("the label is missing")));
        }

        [Test]
        public void MalformedWeeksGivesOneMessage()
        {
            WeeksLoad r = DataFiles.LoadWeeks(DataTestPaths.Sample("weeks_syntax_trailing_comma.json"));
            Assert.AreEqual(1, r.Findings.Count);
            Assert.AreEqual("weeks.json, line 3, column 82: there is an extra comma here, before }", r.Findings[0].ToString());
            Assert.AreEqual("the file is empty", DataFiles.LoadWeeks("").Findings.Single().Message);
            Assert.AreEqual("the file is empty", DataFiles.LoadWeeks(null).Findings.Single().Message);
        }
    }
}
