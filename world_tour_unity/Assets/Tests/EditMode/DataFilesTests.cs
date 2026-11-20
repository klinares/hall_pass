using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Data;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Core.Tests
{
    /// <summary>The converter output against weeks.js, the live StreamingAssets files, math_config.json checks, and C#/Node parity.</summary>
    [TestFixture]
    public class DataFilesTests
    {
        // ---------- converter (vector written by Tools/weeks_to_json.mjs --vectors) ----------

        [Test]
        public void ConverterOutputHoldsEveryWordAndFieldOfWeeksJs()
        {
            JsonValue vec = StrictJson.Parse(DataTestPaths.Read(Path.Combine(DataTestPaths.EditMode, "data_weeks_vectors.json")));
            Assert.AreEqual(22, vec["words"].Long);
            WeeksLoad load = DataFiles.LoadWeeks(vec["output"].String);
            Assert.IsEmpty(load.Findings, "the converted file has no errors or warnings");
            JsonValue source = vec["source"];
            Assert.AreEqual(source.Count, load.File.weeks.Length);
            int words = 0, fields = 0;
            for (int i = 0; i < source.Count; i++)
            {
                JsonValue sw = source[i];
                Week mw = load.File.weeks[i];
                CollectionAssert.AreEqual(new[] { "id", "label", "words" }, sw.Keys.ToList(), "weeks.js has only the fields the game knows");
                Assert.AreEqual(sw["id"].String, mw.id);
                Assert.AreEqual(sw["label"].String, mw.label);
                Assert.AreEqual(sw["words"].Count, mw.words.Length);
                for (int j = 0; j < sw["words"].Count; j++, words++)
                {
                    JsonValue sv = sw["words"][j];
                    Word mv = mw.words[j];
                    foreach (string k in sv.Keys)
                    {
                        fields++;
                        switch (k)
                        {
                            case "w": Assert.AreEqual(sv["w"].String, mv.w); break;
                            case "s": Assert.AreEqual(sv["s"].String, mv.s); break;
                            case "h": Assert.AreEqual(sv["h"].String, mv.h); break;
                            case "x": Assert.AreEqual(sv["x"].String, mv.x); break;
                            case "prefix":
                                Assert.AreEqual(sv["prefix"]["p"].String, mv.prefix.p);
                                Assert.AreEqual(sv["prefix"]["root"].String, mv.prefix.root);
                                Assert.AreEqual(sv["prefix"]["means"].String, mv.prefix.means);
                                Assert.AreEqual(3, sv["prefix"].Count);
                                break;
                            default: Assert.Fail("weeks.js word " + mv.w + " has a field the converter would drop: " + k); break;
                        }
                    }
                    Assert.AreEqual(sv.Has("x"), mv.x != null, "x present exactly when weeks.js has it: " + mv.w);
                    Assert.AreEqual(sv.Has("prefix"), mv.prefix != null, "prefix present exactly when weeks.js has it: " + mv.w);
                }
            }
            Assert.AreEqual(22, words);
            Assert.AreEqual(22 * 3 + 22 + 10, fields, "22 x (w, s, h) + 22 sentences + 10 prefixes");
        }

        [Test]
        public void LiveWeeksFileLoadsWithoutErrors()
        {
            WeeksLoad r = DataFiles.LoadWeeks(DataTestPaths.Read(Path.Combine(DataTestPaths.StreamingAssets, "weeks.json")));
            Assert.IsEmpty(r.Findings.Where(f => f.IsError).Select(f => f.ToString()));
            Assert.IsNotNull(r.File);
        }

        [Test]
        public void LiveMathConfigLoadsWithoutErrors()
        {
            MathConfigLoad r = DataFiles.LoadMathConfig(DataTestPaths.Read(Path.Combine(DataTestPaths.StreamingAssets, "math_config.json")));
            Assert.IsEmpty(r.Findings.Select(f => f.ToString()));
            Assert.IsFalse(r.UsedDefaults);
        }

        // ---------- math_config.json raw checks (the 1.2 tester's JsonUtility cases) ----------

        [TestCase("config_fracc.json", "ENABLED_SKILLS: \"fracc\" is not a field the game knows (fields here: dec, md, frac, xy, vol, ops, flu)")]
        [TestCase("config_Frac.json", "ENABLED_SKILLS: \"Frac\" is not a field the game knows (did you mean \"frac\"? names are case-sensitive)")]
        [TestCase("config_12_5.json", "BOUNDS: \"maxDenominator\" should be a whole number, but it is 12.5")]
        [TestCase("config_no.json", "ENABLED_SKILLS: \"frac\" should be true or false (without quotes), but it is the text \"no\"")]
        [TestCase("config_big.json", "BOUNDS: \"moneyCents\" is 99999999999, which is too big (the largest allowed is 2147483647)")]
        [TestCase("config_malformed.json", "a comma is missing before this field")]
        [TestCase("config_trailing_comma.json", "there is an extra comma here, before }")]
        [TestCase("config_unclosed.json", "this { is never closed")]
        [TestCase("config_empty.json", "the file is empty")]
        [TestCase("config_zero.json", "BOUNDS: \"coordMax\" should be a whole number above 0, but it is 0")]
        [TestCase("config_coordmax_minus_one.json", "BOUNDS: \"coordMax\" should be a whole number above 0, but it is -1")]   // D3: a typed -1 is not "missing"
        [TestCase("config_missing_bound.json", "BOUNDS.maxVolume is missing.")]
        public void BadConfigIsReportedAndFallsBackToWebDefaults(string sample, string message)
        {
            MathConfigLoad r = DataFiles.LoadMathConfig(DataTestPaths.Sample(sample));
            Assert.IsTrue(r.HasErrors);
            Assert.IsTrue(r.Findings.Any(f => f.Message == message), "got: " + string.Join(" || ", r.Findings.Select(f => f.ToString())));
            Assert.IsTrue(r.UsedDefaults);
            Assert.AreEqual(MathConfig.WebDefaults().BOUNDS.maxDenominator, r.Config.BOUNDS.maxDenominator);
            Assert.IsTrue(r.Config.IsEnabled("frac"));
        }

        [Test]
        public void ConfigMessagesNameLineAndColumn()
        {
            Finding f = DataFiles.LoadMathConfig(DataTestPaths.Sample("config_malformed.json")).Findings.Single();
            Assert.AreEqual("math_config.json, line 5, column 5: a comma is missing before this field", f.ToString());
            Finding big = DataFiles.LoadMathConfig(DataTestPaths.Sample("config_big.json")).Findings.Single();
            Assert.AreEqual(12, big.Line);
        }

        [Test]
        public void GoodConfigMapsWithoutReflection()
        {
            MathConfigLoad off = DataFiles.LoadMathConfig(DataTestPaths.Sample("config_frac_off.json"));
            Assert.IsEmpty(off.Findings);
            Assert.IsFalse(off.UsedDefaults);
            Assert.IsFalse(off.Config.IsEnabled("frac"));
            Assert.IsTrue(off.Config.IsEnabled("dec"));
            Assert.AreEqual(10000, off.Config.BOUNDS.moneyCents);
            MathConfigLoad some = DataFiles.LoadMathConfig(DataTestPaths.Sample("config_some_missing.json"));
            Assert.IsEmpty(some.Findings);
            Assert.IsFalse(some.Config.IsEnabled("frac"));
            Assert.IsTrue(some.Config.IsEnabled("vol"), "a missing switch stays on, as in the JS");
            Assert.AreEqual(2147483647, DataFiles.LoadMathConfig(DataTestPaths.Sample("config_int_max.json")).Config.BOUNDS.maxVolume);
        }

        // ---------- C#/Node parity (expected_findings.json written by Tools/validate_data.mjs --parity) ----------

        [Test]
        public void CSharpRulesGiveTheSameFindingsAsTheNodeValidator()
        {
            JsonValue expected = StrictJson.Parse(DataTestPaths.Sample("expected_findings.json"))["samples"];
            var files = Directory.GetFiles(DataTestPaths.Samples, "*.json").Select(Path.GetFileName)
                .Where(n => n != "expected_findings.json").OrderBy(n => n, System.StringComparer.Ordinal).ToList();
            CollectionAssert.AreEqual(files, expected.Keys.ToList(), "every sample file has Node findings; rerun validate_data.mjs --parity");
            Assert.IsTrue(files.Count >= 60, "sample count " + files.Count);
            foreach (string name in files)
            {
                string text = DataTestPaths.Sample(name);
                List<Finding> got = name.StartsWith("weeks") ? DataFiles.LoadWeeks(text).Findings : DataFiles.LoadMathConfig(text).Findings;
                CollectionAssert.AreEqual(expected[name].Items.Select(v => v.String).ToList(), got.Select(f => f.ParityKey).ToList(), name);
            }
        }
    }
}
