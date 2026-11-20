using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Data;
using WorldTour.Core.Maths.Gen;
using WorldTour.Core.Progress;
using WorldTour.Core.Shows;

namespace WorldTour.Core.Tests
{
    /// <summary>
    /// Saves made by the real web game (world_tour_unity/Tools/save/save_vectors.mjs, which writes
    /// save_vectors.json next to this file): store.js migrate on good, partial, old and garbage input, and a
    /// replay of progress.js calls. The C# port must give the same saved JSON. One deliberate difference:
    /// keys the save doesn't know are dropped (the JS keeps them; see DECISIONS.md).
    /// </summary>
    public class SaveVectorTests
    {
        private static JsonValue _v;

        private static JsonValue Vectors()
        {
            if (_v == null) _v = StrictJson.Parse(File.ReadAllText(SaveFixtures.TestFile("save_vectors.json")));
            return _v;
        }

        private static readonly HashSet<string> Known = new HashSet<string>
        {
            "v", "created", "updated", "settings", "fans", "streak", "venues", "skills", "words", "review", "shows", "time", "inventory", "journal"
        };

        /// <summary>The JS output with top-level keys the save doesn't model taken out.</summary>
        private static string KnownOnly(string jsCanon)
        {
            JsonValue o = StrictJson.Parse(jsCanon);
            var k = JsonValue.Obj();
            foreach (string key in o.Keys) if (Known.Contains(key)) k.Set(key, o[key]);
            return k.ToCanonicalJson();
        }

        public static IEnumerable<string> MigrateCases()
        {
            return Vectors()["migrate"].Items.Select(x => x["name"].String);
        }

        [Test, TestCaseSource(nameof(MigrateCases))]
        public void Migrate_MatchesTheWebGame(string name)
        {
            JsonValue c = Vectors()["migrate"].Items.Single(x => x["name"].String == name);
            string today = Vectors()["today"].String;
            SaveBlob blob = SaveJson.Migrate(StrictJson.Parse(c["raw"].String), today);
            Assert.AreEqual(KnownOnly(c["out"].String), SaveJson.ToCanonical(blob), name);
        }

        [Test]
        public void Defaults_SameTextAsTheWebGame()
        {
            Assert.AreEqual(Vectors()["defaultsText"].String, SaveJson.ToText(SaveBlob.Defaults(Vectors()["today"].String)));
        }

        [Test]
        public void Migrate_AllCasesPresent()
        {
            Assert.GreaterOrEqual(Vectors()["migrate"].Count, 16);
        }

        [Test]
        public void Replay_ProgressCallsLeaveTheSameSave()
        {
            string today = Vectors()["today"].String;
            var s = SaveBlob.Defaults(today);
            foreach (JsonValue op in Vectors()["replay"]["ops"].Items)
            {
                var a = op.Items;
                switch (a[0].String)
                {
                    case "addFans": ProgressRules.AddFans(s, a[1].Long); break;
                    case "markSeen": ProgressRules.MarkSeen(s, a[1].String); break;
                    case "recordSkill": ProgressRules.RecordSkill(s, a[1].String, a[2].BoolValue, a[3].BoolValue, a[4].IsNull ? null : a[4].String); break;
                    case "noteMistake": ProgressRules.NoteMistake(s, a[1].String, a[2].String); break;
                    case "recordWord": ProgressRules.RecordWord(s, a[1].String, a[2].BoolValue, a[3].BoolValue, a[4].BoolValue, a[5].BoolValue, today); break;
                    case "recordMath":
                        var item = new MathItem(a[2].String, a[3].Int, a[4].Long, Generators.Generate(a[2].String, a[3].Int, a[4].Long));
                        ProgressRules.RecordMath(s, a[1].String, item, a[5].BoolValue, a[6].BoolValue, a[7].BoolValue);
                        break;
                    case "finishShow":
                        JsonValue m = a[1];
                        var sum = new ShowSummary
                        {
                            Mode = m["mode"].String, Venue = m["venue"].String, N = m["n"].Int, First = m["first"].Int, Hinted = m["hinted"].Int,
                            Fans = m["fans"].Int, Secs = m["secs"].Int, Best = m["best"].Int,
                            Sold = m["sold"] != null && m["sold"].BoolValue, Encore = m["encore"] != null && m["encore"].BoolValue
                        };
                        ProgressRules.FinishShow(s, sum, today);
                        break;
                    case "setSetting":
                        if (a[1].String == "motion") ProgressRules.SetMotion(s, a[2].String);
                        else if (a[1].String == "sound") ProgressRules.SetSound(s, a[2].BoolValue);
                        else Assert.Fail("setting " + a[1].String);
                        break;
                    default: Assert.Fail("unknown op " + a[0].String); break;
                }
            }
            Assert.AreEqual(Vectors()["replay"]["out"].String, SaveJson.ToCanonical(s));
            Assert.AreEqual(Vectors()["replay"]["text"].String, SaveJson.ToText(s), "same text as the web game stores, key order included");
            CollectionAssert.AreEqual(new[] { "coffee", "records", "rec" }, s.Venues.Unlocked);
        }

        [Test]
        public void JsMadeSave_ReloadsByteForByte()
        {
            string js = Vectors()["replay"]["text"].String;          // what the web game stores
            LoadResult r = SaveJson.Load(js, "2030-01-01");
            Assert.IsFalse(r.Unreadable);
            Assert.AreEqual(js, SaveJson.ToText(r.Blob), "byte for byte, key order included");
            Assert.AreEqual(Vectors()["replay"]["out"].String, SaveJson.ToCanonical(r.Blob));
            // ops, frac (a clean answer with keep doesn't clear it), xy, vol, flu, md; dec was cleared by a clean answer
            CollectionAssert.AreEqual(new[] { "ops", "frac", "xy", "vol", "flu", "md" }, r.Blob.Review.Math.Select(x => x.Skill).ToArray());
            Assert.IsTrue(r.Blob.Review.Math.All(x => Review.Usable(x.Prob)), "every one stored whole");
        }
    }
}
