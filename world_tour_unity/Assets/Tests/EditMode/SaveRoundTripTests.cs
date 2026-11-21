using System;
using System.Linq;
using System.Text;
using NUnit.Framework;
using WorldTour.Core.Data;
using WorldTour.Core.Maths.Gen;
using WorldTour.Core.Progress;

namespace WorldTour.Core.Tests
{
    /// <summary>The save blob: defaults, load, round trip, size, and garbage in storage.</summary>
    public class SaveRoundTripTests
    {
        [Test]
        public void Defaults_MatchStoreJs()
        {
            var d = SaveBlob.Defaults("2026-11-20");
            Assert.AreEqual("worldtour.v1", SaveBlob.Key);
            Assert.AreEqual(
                "{\"created\":\"2026-11-20\",\"fans\":{\"balance\":0,\"lifetime\":0},\"inventory\":{\"equipped\":{\"dog\":\"none\",\"guitar\":\"classic\",\"theme\":\"tour-pink\"}," +
                "\"owned\":[\"theme:tour-pink\",\"guitar:classic\",\"dog:none\"]},\"journal\":{\"layout\":{},\"seen\":[],\"stickers\":{}},\"review\":{\"math\":[],\"words\":[]}," +
                "\"settings\":{\"motion\":\"auto\",\"sound\":true,\"theme\":\"tour-pink\"},\"shows\":[],\"skills\":{},\"streak\":{\"best\":0,\"days\":0,\"last\":null}," +
                "\"time\":{},\"updated\":0,\"v\":2,\"venues\":{\"seen\":[\"coffee\"],\"unlocked\":[\"coffee\"]},\"words\":{}}",
                SaveJson.ToCanonical(d));
        }

        [Test]
        public void DictionaryOrder_SurvivesSaveAndLoad()
        {
            // the office breaks ties in insertion order (JS object key order), so nothing may be re-sorted
            var s = SaveBlob.Defaults("2026-11-20");
            foreach (string w in new[] { "zebra", "apple", "mango", "kiwi" }) ProgressRules.RecordWord(s, w, false, false, true, false, "2026-11-20");
            foreach (string t in new[] { "no-carry", "gave-total", "decimal-place" }) ProgressRules.NoteMistake(s, "dec", t);
            ProgressRules.Skill(s, "spell:remix");
            ProgressRules.Skill(s, "dec");
            s.Journal.Stickers["streak-3"] = "2026-11-02";
            s.Journal.Stickers["first-show"] = "2026-11-01";
            s.Journal.Layout["streak-3"] = new Spot { X = .5, Y = .5, R = 2 };
            s.Journal.Layout["first-show"] = new Spot { X = .25, Y = .5, R = -2 };
            for (int d = 130; d >= 1; d--) ProgressRules.AddPlayTime(s, Dates.LocalIso(new DateTime(2026, 1, 1).AddDays(d)), d);   // newest first, then trimmed
            var timeOrder = s.Time.Keys.ToList();
            SaveBlob back = SaveJson.Load(SaveJson.ToText(s), "2026-11-21").Blob;
            CollectionAssert.AreEqual(new[] { "zebra", "apple", "mango", "kiwi" }, back.Words.Keys);
            CollectionAssert.AreEqual(new[] { "no-carry", "gave-total", "decimal-place" }, back.Skills["dec"].Mistakes.Keys);
            CollectionAssert.AreEqual(new[] { "dec", "spell:remix" }, back.Skills.Keys, "dec was created first, by NoteMistake");
            CollectionAssert.AreEqual(new[] { "streak-3", "first-show" }, back.Journal.Stickers.Keys);
            CollectionAssert.AreEqual(new[] { "streak-3", "first-show" }, back.Journal.Layout.Keys);
            Assert.AreEqual(120, back.Time.Count);
            CollectionAssert.AreEqual(timeOrder, back.Time.Keys, "trimming keeps the rest in insertion order");
            Assert.AreEqual(Dates.LocalIso(new DateTime(2026, 1, 1).AddDays(130)), back.Time.Keys.First(), "insertion order, not date order");
            Assert.AreEqual(SaveJson.ToText(s), SaveJson.ToText(back));
        }

        [Test]
        public void FullSave_RoundTripsExactly()
        {
            SaveBlob s = SaveFixtures.Full();
            string text = SaveJson.ToText(s);
            LoadResult r = SaveJson.Load(text, "2030-01-01");
            Assert.IsFalse(r.Unreadable);
            Assert.AreEqual(text, SaveJson.ToText(r.Blob));
            Assert.AreEqual("2025-11-20", r.Blob.Created, "created is kept, not reset to today");
            Assert.AreEqual(s.Fans.Lifetime, r.Blob.Fans.Lifetime);
            Assert.AreEqual(Review.Max, r.Blob.Review.Math.Count);
            Assert.IsTrue(r.Blob.Review.Math.All(e => Review.Item(e).Prob != null));
        }

        [Test]
        public void FullSave_StaysWellUnderOneMegabyte()
        {
            string text = SaveJson.ToText(SaveFixtures.Full());
            int bytes = Encoding.UTF8.GetByteCount(text);
            Console.WriteLine("full save: " + bytes + " bytes of JSON");
            Assert.AreEqual(text.Length, bytes, "plain ASCII");
            Assert.Less(bytes, 200 * 1024, "well under 1 MB (localStorage holds about 5 MB per site)");
        }

        [Test]
        public void Load_NothingStored_GivesDefaults()
        {
            LoadResult r = SaveJson.Load(null, "2026-11-20");
            Assert.IsFalse(r.Unreadable);
            Assert.AreEqual(SaveJson.ToText(SaveBlob.Defaults("2026-11-20")), SaveJson.ToText(r.Blob));
            Assert.IsFalse(SaveJson.Load("", "2026-11-20").Unreadable);
        }

        [Test]
        public void Load_UnreadableText_GivesDefaults_AndSaysSo()
        {
            // JS: JSON.parse throws, raw = null, migrate(null) = defaults. The Game layer must keep the text (see DECISIONS).
            foreach (string bad in new[] { "{", "not json", "{\"fans\":1,}", "{'a':1}" })
            {
                LoadResult r = SaveJson.Load(bad, "2026-11-20");
                Assert.IsTrue(r.Unreadable, bad);
                Assert.IsNotNull(r.Error);
                Assert.AreEqual(bad, r.Text, "the unreadable text is handed back so it can be kept");
                Assert.AreEqual(0, r.Blob.Fans.Lifetime);
            }
        }

        [Test]
        public void Migrate_WrongTypesKeepDefaults_NeverCrash()
        {
            string raw = "{\"fans\":{\"balance\":\"500\",\"lifetime\":12.7},\"venues\":{\"unlocked\":\"records\",\"seen\":5},\"settings\":{\"sound\":\"no\",\"theme\":3}," +
                         "\"skills\":{\"dec\":{\"level\":2},\"bad\":7},\"words\":{\"a\":\"x\"},\"time\":{\"2026-11-01\":\"300\"},\"shows\":[1,{\"mode\":\"remix\",\"n\":10,\"sold\":1}]," +
                         "\"review\":{\"math\":[{\"skill\":\"dec\"},{\"skill\":\"dec\",\"level\":1,\"seed\":4}],\"words\":[\"ok\",3]},\"streak\":{\"days\":\"2\",\"last\":7}}";
            SaveBlob s = SaveJson.Migrate(StrictJson.Parse(raw), "2026-11-20");
            Assert.AreEqual(0, s.Fans.Balance, "JS would keep the string and then concatenate");
            Assert.AreEqual(12, s.Fans.Lifetime, "a fraction is floored");
            CollectionAssert.AreEqual(new[] { "coffee" }, s.Venues.Unlocked, "JS would spread the string into letters");
            CollectionAssert.AreEqual(new[] { "coffee" }, s.Venues.Seen);
            Assert.IsTrue(s.Settings.Sound);
            Assert.AreEqual("tour-pink", s.Settings.Theme);
            Assert.AreEqual(2, s.Skills["dec"].Level);
            Assert.AreEqual(3, s.Skills["dec"].ByLevel.Count, "a partial skill record is filled in");
            Assert.IsFalse(s.Skills.ContainsKey("bad"));
            Assert.AreEqual(0, s.Words.Count);
            Assert.AreEqual(0, s.Time.Count);
            Assert.AreEqual(1, s.Shows.Count);
            Assert.IsTrue(s.Shows[0].Sold, "truthy, as the milestones read it");
            Assert.AreEqual(1, s.Review.Math.Count, "an entry without level and seed is dropped");
            CollectionAssert.AreEqual(new[] { "ok" }, s.Review.Words);
            Assert.AreEqual(0, s.Streak.Days);
            Assert.IsNull(s.Streak.Last);
        }

        [Test]
        public void Migrate_NothingEarnedIsLost_OnAFullSave()
        {
            SaveBlob s = SaveFixtures.Full();
            SaveBlob m = SaveJson.Migrate(SaveJson.Write(s), "2030-01-01");
            Assert.AreEqual(s.Fans.Lifetime, m.Fans.Lifetime);
            Assert.AreEqual(s.Fans.Balance, m.Fans.Balance);
            CollectionAssert.AreEqual(s.Venues.Unlocked, m.Venues.Unlocked);
            CollectionAssert.AreEquivalent(s.Journal.Stickers, m.Journal.Stickers);
            CollectionAssert.AreEqual(s.Inventory.Owned, m.Inventory.Owned);
            Assert.AreEqual(s.Streak.Best, m.Streak.Best);
            Assert.AreEqual(s.Skills.Count, m.Skills.Count);
            foreach (var kv in s.Skills) Assert.AreEqual(kv.Value.Level, m.Skills[kv.Key].Level);
        }

        [Test]
        public void JsonTree_CloneIsDeep()
        {
            var o = JsonValue.Obj().Set("a", JsonValue.Arr(JsonValue.Num(1), JsonValue.Obj().Set("b", "c")));
            var c = JsonTree.Clone(o);
            Assert.AreEqual(o.ToCanonicalJson(), c.ToCanonicalJson());
            o["a"][1].Set("b", "changed");
            Assert.AreEqual("c", c["a"][1]["b"].String);
            Assert.IsNull(JsonTree.Clone(null));
        }
    }
}
