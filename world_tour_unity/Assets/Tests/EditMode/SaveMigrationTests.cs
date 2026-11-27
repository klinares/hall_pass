using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using WorldTour.Core.Data;
using WorldTour.Core.Maths.Gen;
using WorldTour.Core.Progress;

namespace WorldTour.Core.Tests
{
    /// <summary>
    /// Save version 2 (phase 5): a version 1 save loads with everything kept and an empty journal.seen; keys this
    /// version doesn't know are kept where they were stored and written back; a newer save keeps its version; the whole
    /// save stays under one key and well under 1 MB.
    /// </summary>
    public class SaveMigrationTests
    {
        const string Day = "2026-11-20";

        static string V1Text()
        {
            // the web game's own save after a replay of progress.js calls (Tools/save/save_vectors.mjs): a real version 1 save
            JsonValue v = StrictJson.Parse(File.ReadAllText(SaveFixtures.TestFile("save_vectors.json")));
            return v["replay"]["text"].String;
        }

        [Test]
        public void Version_IsThree_UnderTheSameKey()
        {
            Assert.AreEqual(3, SaveBlob.Version);
            Assert.AreEqual("worldtour.v1", SaveBlob.Key, "one key, unchanged, so the old save is found");
            StringAssert.StartsWith("{\"v\":3,", SaveJson.ToText(SaveBlob.Defaults(Day)));
        }

        [Test]
        public void V1Save_LoadsWithEverythingKept()
        {
            string v1 = V1Text();
            JsonValue raw = StrictJson.Parse(v1);
            Assert.AreEqual(1, raw["v"].Int);
            SaveBlob b = SaveJson.Load(v1, Day).Blob;
            Assert.AreEqual(3, b.V);
            Assert.AreEqual(raw["fans"]["lifetime"].Long, b.Fans.Lifetime);
            Assert.AreEqual(raw["fans"]["balance"].Long, b.Fans.Balance);
            CollectionAssert.AreEqual(raw["venues"]["unlocked"].Items.Select(x => x.String), b.Venues.Unlocked);
            Assert.AreEqual(raw["shows"].Count, b.Shows.Count);
            Assert.AreEqual(raw["review"]["math"].Count, b.Review.Math.Count);
            CollectionAssert.IsEmpty(b.Journal.Seen, "a version 1 save has seen nothing peel on yet");
            // everything the v1 save held is in the v2 text, untouched
            JsonValue back = StrictJson.Parse(SaveJson.ToText(b));
            foreach (string k in raw.Keys)
            {
                if (k == "v" || k == "journal") continue;
                Assert.AreEqual(raw[k].ToCanonicalJson(), back[k].ToCanonicalJson(), k);
            }
            Assert.AreEqual(raw["journal"]["stickers"].ToCanonicalJson(), back["journal"]["stickers"].ToCanonicalJson());
            Assert.AreEqual(raw["journal"]["layout"].ToCanonicalJson(), back["journal"]["layout"].ToCanonicalJson());
        }

        [Test]
        public void V1Save_WithStickersAndPurchases_KeepsThemAll()
        {
            string v1 = "{\"v\":1,\"created\":\"2026-11-01\",\"fans\":{\"balance\":100,\"lifetime\":90000}," +
                "\"inventory\":{\"owned\":[\"theme:tour-pink\",\"guitar:classic\",\"dog:none\",\"theme:peach-navy\",\"sticker:pass\"],\"equipped\":{\"theme\":\"peach-navy\",\"guitar\":\"classic\",\"dog\":\"none\"}}," +
                "\"journal\":{\"stickers\":{\"first-show\":\"2026-11-02\",\"venue-records\":\"2026-11-03\"},\"layout\":{\"first-show\":{\"x\":0.3,\"y\":0.4,\"r\":-2}}}}";
            SaveBlob b = SaveJson.Load(v1, Day).Blob;
            Assert.AreEqual("peach-navy", WorldTour.Core.Meta.MetaRules.GetLook(b).Theme, "the theme survives");
            CollectionAssert.Contains(b.Inventory.Owned, "sticker:pass");
            Assert.AreEqual(2, b.Journal.Stickers.Count);
            Assert.AreEqual(.3, b.Journal.Layout["first-show"].X);
            CollectionAssert.IsEmpty(b.Journal.Seen);
            SaveBlob again = SaveJson.Load(SaveJson.ToText(b), Day).Blob;
            Assert.AreEqual(SaveJson.ToText(b), SaveJson.ToText(again), "a v2 save round-trips byte for byte");
        }

        [Test]
        public void UnknownKeys_AreKept_WhereTheyWereStored_AndWrittenBack()
        {
            string text = "{\"v\":2,\"future\":{\"a\":[1,2]},\"created\":\"2026-11-01\"," +
                "\"settings\":{\"sound\":true,\"volume\":0.5}," +
                "\"fans\":{\"balance\":1,\"lifetime\":2,\"peak\":9}," +
                "\"streak\":{\"days\":1,\"best\":1,\"last\":null,\"freeze\":true}," +
                "\"venues\":{\"unlocked\":[\"coffee\"],\"seen\":[\"coffee\"],\"pins\":[\"rec\"]}," +
                "\"skills\":{\"dec\":{\"level\":2,\"tag\":\"x\"}}," +
                "\"words\":{\"because\":{\"a\":1,\"c\":1,\"first\":1,\"h\":0,\"miss\":0,\"last\":\"2026-11-02\",\"star\":true}}," +
                "\"review\":{\"words\":[],\"math\":[{\"skill\":\"dec\",\"level\":1,\"seed\":5,\"why\":\"q\"}],\"extra\":1}," +
                "\"shows\":[{\"d\":\"2026-11-02\",\"mode\":\"remix\",\"venue\":\"coffee\",\"n\":10,\"first\":9,\"hinted\":0,\"fans\":100,\"secs\":60,\"best\":9,\"crowd\":0.4}]," +
                "\"inventory\":{\"owned\":[],\"equipped\":{\"hat\":\"cap\"},\"gifts\":[]}," +
                "\"journal\":{\"stickers\":{},\"layout\":{\"first-show\":{\"x\":0.5,\"y\":0.5,\"r\":1,\"z\":3}},\"seen\":[\"first-show\"],\"cover\":\"red\"}," +
                "\"zzz\":null}";
            SaveBlob b = SaveJson.Load(text, Day).Blob;
            JsonValue o = StrictJson.Parse(SaveJson.ToText(b));
            Assert.AreEqual("{\"a\":[1,2]}", o["future"].ToCanonicalJson());
            Assert.IsTrue(o["zzz"].IsNull);
            Assert.AreEqual(.5, o["settings"]["volume"].Number);
            Assert.AreEqual(9, o["fans"]["peak"].Int);
            Assert.IsTrue(o["streak"]["freeze"].BoolValue);
            Assert.AreEqual("rec", o["venues"]["pins"][0].String);
            Assert.AreEqual("x", o["skills"]["dec"]["tag"].String);
            Assert.IsTrue(o["words"]["because"]["star"].BoolValue);
            Assert.AreEqual("q", o["review"]["math"][0]["why"].String);
            Assert.AreEqual(1, o["review"]["extra"].Int);
            Assert.AreEqual(.4, o["shows"][0]["crowd"].Number);
            Assert.AreEqual("cap", o["inventory"]["equipped"]["hat"].String);
            Assert.AreEqual(0, o["inventory"]["gifts"].Count);
            Assert.AreEqual(3, o["journal"]["layout"]["first-show"]["z"].Int);
            Assert.AreEqual("red", o["journal"]["cover"].String);
            // known keys first, in the save's order, then the unknown ones in stored order (JS { ...defaults, ...raw })
            CollectionAssert.AreEqual(new[] { "v", "created", "updated", "settings", "fans", "streak", "venues", "skills", "words", "review", "shows", "time", "inventory", "journal", "future", "zzz" }, o.Keys.ToArray());
            CollectionAssert.AreEqual(new[] { "stickers", "layout", "seen", "cover" }, o["journal"].Keys.ToArray());
            // and they survive another load and save unchanged
            Assert.AreEqual(SaveJson.ToText(b), SaveJson.ToText(SaveJson.Load(SaveJson.ToText(b), Day).Blob));
        }

        [Test]
        public void NewerSave_KeepsItsVersion()
        {
            SaveBlob b = SaveJson.Load("{\"v\":4,\"fans\":{\"balance\":5,\"lifetime\":5},\"later\":true}", Day).Blob;
            Assert.AreEqual(4, b.V, "never stamped back down to 3");
            StringAssert.StartsWith("{\"v\":4,", SaveJson.ToText(b));
            StringAssert.Contains("\"later\":true", SaveJson.ToText(b));
        }

        [Test]
        public void Seen_KeepsStringsOnce_AndIgnoresAWrongType()
        {
            SaveBlob b = SaveJson.Load("{\"v\":2,\"journal\":{\"seen\":[\"a\",3,\"a\",\"b\",null]}}", Day).Blob;
            CollectionAssert.AreEqual(new[] { "a", "b" }, b.Journal.Seen);
            b = SaveJson.Load("{\"v\":2,\"journal\":{\"seen\":\"a\"}}", Day).Blob;
            CollectionAssert.IsEmpty(b.Journal.Seen);
        }

        [Test]
        public void FullSave_WithEveryStickerPlacedAndSeen_StaysWellUnderOneMegabyte()
        {
            SaveBlob s = SaveFixtures.Full();
            foreach (var m in WorldTour.Core.Meta.Milestones.All)
            {
                s.Journal.Stickers[m.Id] = Day;
                WorldTour.Core.Meta.MetaRules.Place(s, m.Id, .5, .5, 3);
            }
            foreach (var it in WorldTour.Core.Meta.Shop.All) if (!s.Inventory.Owned.Contains(it.Id)) s.Inventory.Owned.Add(it.Id);
            WorldTour.Core.Meta.JournalBook.MarkSeen(s, WorldTour.Core.Meta.JournalBook.Pages(s).SelectMany(p => p.Slots).Select(x => x.Key));
            string text = SaveJson.ToText(s);
            int bytes = Encoding.UTF8.GetByteCount(text);
            System.Console.WriteLine("full v2 save: " + bytes + " bytes");
            Assert.AreEqual(27, s.Journal.Seen.Count);
            Assert.Less(bytes, 200 * 1024, "well under 1 MB");
        }
    }
}
