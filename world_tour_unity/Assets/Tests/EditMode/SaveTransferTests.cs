using System;
using System.Collections.Generic;
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
    /// Task 5.2: the office's import gate (SaveTransfer), the backup taken before an import or a reset (SaveBackup), and the
    /// two-step confirms (ResetConfirm, ImportConfirm). A bad, truncated, wrong-version or too-large input never touches the
    /// current save; a good one is handed over only after the yes. transfer_vectors.txt holds saves made by the web game's
    /// own store.js and progress.js, as it stores them, with WT1 codes made by node's zlib (Tools/save/transfer_vectors.mjs).
    /// </summary>
    public class SaveTransferTests
    {
        private const string Today = "2026-11-20";

        private sealed class Slots : ISaveSlots
        {
            public readonly Dictionary<string, string> D = new Dictionary<string, string>();
            public Func<string, bool> FailWrite = k => false;
            public Func<string, string, string> Mangle = (k, v) => v;
            public int Writes;
            public string Read(string key) { string v; return D.TryGetValue(key, out v) ? v : null; }
            public bool Write(string key, string text)
            {
                Writes++;
                if (FailWrite(key)) return false;
                D[key] = Mangle(key, text);
                return true;
            }
        }

        private static SaveBlob Played(long fans = 1234)
        {
            SaveBlob s = SaveBlob.Defaults(Today);
            ProgressRules.AddFans(s, fans);
            s.Shows.Add(new ShowRecord { D = Today, Mode = "remix", N = 10, First = 8 });
            return s;
        }

        private static string Vectors(string name) { return Path.Combine(DataTestPaths.EditMode, name); }

        // ---------------------------------------------------------------- the gate

        [Test]
        public void BadInputs_AreRefused_AndTheCurrentSaveIsUntouched()
        {
            SaveBlob current = Played();
            string before = SaveJson.ToText(current);
            string code = ExportCode.Export(Played(99999));
            int dash = code.LastIndexOf('-');
            var bad = new Dictionary<string, string>
            {
                { "null", null },
                { "empty", "" },
                { "spaces", "  \r\n\t " },
                { "random words", "hello there" },
                { "truncated code", code.Substring(0, code.Length / 2) },
                { "code missing its end", code.Substring(0, dash) },
                { "one character changed", code.Substring(0, 30) + (code[30] == 'A' ? 'B' : 'A') + code.Substring(31) },
                { "wrong check", code.Substring(0, dash + 1) + "00000000" },
                { "older code version", "WT0-" + code.Substring(4) },
                { "newer code version", "WT2-" + code.Substring(4) },
                { "much newer", "WT13-" + code.Substring(4) },
                { "WT10", "WT10" + code.Substring(4) },
                { "not base64", "WT1-!!!!-12345678" },
                { "truncated json", SaveJson.ToText(Played()).Substring(0, 200) },
                { "json array", "[1,2,3]" },
                { "json not a save", "{\"hello\":1}" },
                { "newer save version", SaveJson.ToText(Played()).Replace("\"v\":" + SaveBlob.Version, "\"v\":" + (SaveBlob.Version + 1)) },
                { "deep nesting", "{\"fans\":{},\"venues\":" + new string('[', 5000) + new string(']', 5000) + "}" },
                { "huge paste", new string('A', SaveTransfer.MaxChars * 2 + 1) },
                { "huge json", "{\"fans\":{},\"venues\":{},\"x\":\"" + new string('a', SaveTransfer.MaxChars) + "\"}" },
            };
            foreach (var kv in bad)
            {
                ImportPreview p = SaveTransfer.Check(kv.Value, current, Today);
                Assert.IsFalse(p.Ok, kv.Key + " should be refused");
                Assert.IsNull(p.Incoming, kv.Key);
                Assert.IsFalse(string.IsNullOrEmpty(p.Error), kv.Key + " needs a message");
                string words = p.Error.Replace(ExportCode.Prefix, ""); // the code itself is shown as it is written
                Assert.AreEqual(words.ToLowerInvariant(), words, kv.Key + ": lowercase");
                Assert.AreEqual(before, SaveJson.ToText(current), kv.Key + " changed the current save");
            }
            Assert.AreEqual(SaveTransfer.NewerCode, SaveTransfer.Check("WT2-" + code.Substring(4), current, Today).Error);
            Assert.AreEqual(SaveTransfer.NotACode, SaveTransfer.Check("WT0-" + code.Substring(4), current, Today).Error);
            Assert.AreEqual(SaveTransfer.NotACode, SaveTransfer.Check("WT10" + code.Substring(4), current, Today).Error);
            Assert.AreEqual(SaveTransfer.TooBig, SaveTransfer.Check(bad["huge paste"], current, Today).Error);
            Assert.AreEqual(SaveTransfer.Empty, SaveTransfer.Check("   ", current, Today).Error);
            StringAssert.Contains("newer version", SaveTransfer.Check(bad["newer save version"], current, Today).Error);
        }

        [Test]
        public void AHugeSaveInsideASmallCode_IsRefused()
        {
            // a short code whose save is over the limit: one word with a 1.1 million letter name. The code is packed with
            // the runtime's DeflateStream (any raw DEFLATE reads the same), which is fast in the Editor's Mono where the
            // game's own packer takes minutes on a save this size.
            SaveBlob s = Played();
            s.Words[new string('a', SaveTransfer.MaxChars + 100000)] = new WordStat { A = 1, Miss = 1, Last = Today };
            string json = SaveJson.ToText(s);
            Assert.Greater(json.Length, SaveTransfer.MaxChars);
            byte[] raw = Encoding.UTF8.GetBytes(json);
            byte[] packed;
            using (var ms = new MemoryStream())
            {
                using (var z = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionLevel.Optimal, true)) z.Write(raw, 0, raw.Length);
                packed = ms.ToArray();
            }
            string code = ExportCode.Prefix + Convert.ToBase64String(packed).TrimEnd('=').Replace('+', '-').Replace('/', '_')
                + "-" + Deflate.Crc32(raw).ToString("x8");
            Assert.Less(code.Length, 20000, "a short code");
            Assert.AreEqual(SaveTransfer.TooBig, SaveTransfer.Check(code, Played(), Today).Error);
        }

        [Test]
        public void GoodInputs_AreAccepted_AndStillChangeNothing()
        {
            SaveBlob current = Played(5);
            string before = SaveJson.ToText(current);
            SaveBlob there = Played(70000);
            string code = ExportCode.Export(there);
            var wrapped = new StringBuilder();
            for (int i = 0; i < code.Length; i += 50) wrapped.Append(code, i, Math.Min(50, code.Length - i)).Append(i % 100 == 0 ? "​\r\n" : " ­");
            var good = new[] { code, "  " + code + "\n", wrapped.ToString(), "wt1-" + code.Substring(4), ExportCode.ExportFile(there), "﻿" + ExportCode.ExportFile(there) + "\r\n" };
            foreach (string g in good)
            {
                ImportPreview p = SaveTransfer.Check(g, current, Today);
                Assert.IsTrue(p.Ok, p.Error);
                Assert.AreEqual(SaveJson.ToText(there), SaveJson.ToText(p.Incoming));
                Assert.AreEqual(before, SaveJson.ToText(current));
                Assert.IsFalse(p.LosesProgress, "more fans, nothing lost");
            }
        }

        [Test]
        public void Losses_AreListed()
        {
            ImportPreview p = SaveTransfer.Check(ExportCode.Export(SaveBlob.Defaults(Today)), Played(), Today);
            Assert.IsTrue(p.Ok, p.Error);
            Assert.IsTrue(p.LosesProgress);
            Assert.IsTrue(p.Losses.Any(l => l.Contains("total fans")));
            Assert.IsTrue(p.Losses.Any(l => l.Contains("show history")));
        }

        [Test]
        public void WebGameSaves_Import_AsFileAndAsCode()
        {
            string[] lines = File.ReadAllLines(Vectors("transfer_vectors.txt")).Where(l => l.Length > 0 && char.IsDigit(l[0])).ToArray();
            Assert.That(lines.Length, Is.GreaterThanOrEqualTo(50));
            foreach (string line in lines)
            {
                string[] f = line.Split('	');
                string seed = f[0], today = f[1], json = f[2], code = f[3];
                // importing the web game's stored text is exactly what loading it from storage would give
                string loaded = SaveJson.ToText(SaveJson.Load(json, today).Blob);
                ImportPreview byFile = SaveTransfer.Check(json, null, today);
                Assert.IsTrue(byFile.Ok, "seed " + seed + " file: " + byFile.Error);
                Assert.AreEqual(loaded, SaveJson.ToText(byFile.Incoming), "seed " + seed + ": file import = load");
                // the code made by node's own zlib reads the same
                ImportPreview byCode = SaveTransfer.Check(code, null, today);
                Assert.IsTrue(byCode.Ok, "seed " + seed + " node code: " + byCode.Error);
                Assert.AreEqual(loaded, SaveJson.ToText(byCode.Incoming), "seed " + seed + ": node code import = load");
                // nothing the web game recorded is lost on the way in
                JsonValue raw; StrictJsonException err;
                Assert.IsTrue(StrictJson.TryParse(json, out raw, out err));
                SaveBlob s = byFile.Incoming;
                Assert.AreEqual((long)raw["fans"]["lifetime"].Number, s.Fans.Lifetime, "seed " + seed);
                Assert.AreEqual((long)raw["fans"]["balance"].Number, s.Fans.Balance, "seed " + seed);
                Assert.AreEqual(raw["shows"].Count, s.Shows.Count, "seed " + seed);
                Assert.AreEqual(raw["review"]["words"].Count, s.Review.Words.Count, "seed " + seed);
                Assert.AreEqual(raw["review"]["math"].Count, s.Review.Math.Count, "seed " + seed);
                CollectionAssert.AreEquivalent(raw["words"].Keys.ToList(), s.Words.Keys.ToList(), "seed " + seed);
                CollectionAssert.AreEquivalent(raw["skills"].Keys.ToList(), s.Skills.Keys.ToList(), "seed " + seed);
                CollectionAssert.AreEquivalent(raw["venues"]["unlocked"].Items.Select(x => x.String).ToList(), s.Venues.Unlocked, "seed " + seed);
                foreach (string w in raw["words"].Keys) Assert.AreEqual((int)raw["words"][w]["miss"].Number, s.Words[w].Miss, "seed " + seed + " " + w);
                // while the save format is still the web game's (version 1), the text comes back byte for byte
                int version = SaveBlob.Version;   // not the const itself, so the check compiles at every version without a warning
                if (version == 1) Assert.AreEqual(json, SaveJson.ToText(s), "seed " + seed + ": byte for byte");
                // and Unity's own code for that save reads back the same
                ImportPreview again = SaveTransfer.Check(ExportCode.Export(s), null, today);
                Assert.IsTrue(again.Ok, again.Error);
                Assert.AreEqual(SaveJson.ToText(s), SaveJson.ToText(again.Incoming));
            }
        }

        [Test]
        public void HasProgress_IgnoresSettings()
        {
            SaveBlob s = SaveBlob.Defaults(Today);
            Assert.IsFalse(SaveTransfer.HasProgress(s));
            Assert.IsFalse(SaveTransfer.HasProgress(null));
            s.Settings.Sound = false; s.Settings.Motion = "reduced";
            Assert.IsFalse(SaveTransfer.HasProgress(s));
            s.Time[Today] = 15;
            Assert.IsTrue(SaveTransfer.HasProgress(s));
            Assert.IsTrue(SaveTransfer.HasProgress(Played()));
        }

        // ---------------------------------------------------------------- the backup

        [Test]
        public void Backup_KeepsTheLastTwo_NewestFirst()
        {
            var slots = new Slots();
            SaveBlob a = Played(100), b = Played(200), c = Played(300);
            Assert.AreEqual(BackupOutcome.Kept, SaveBackup.Keep(slots, a));
            Assert.AreEqual(SaveJson.ToText(a), slots.Read(SaveBackup.Key));
            Assert.IsNull(slots.Read(SaveBackup.Key2));
            Assert.AreEqual(BackupOutcome.AlreadyKept, SaveBackup.Keep(slots, a));
            Assert.AreEqual(BackupOutcome.Kept, SaveBackup.Keep(slots, b));
            Assert.AreEqual(SaveJson.ToText(b), slots.Read(SaveBackup.Key));
            Assert.AreEqual(SaveJson.ToText(a), slots.Read(SaveBackup.Key2));
            Assert.AreEqual(BackupOutcome.Kept, SaveBackup.Keep(slots, c));
            Assert.AreEqual(SaveJson.ToText(c), slots.Read(SaveBackup.Key));
            Assert.AreEqual(SaveJson.ToText(b), slots.Read(SaveBackup.Key2));
            Assert.IsFalse(slots.D.ContainsKey(SaveBlob.Key), "the backup never writes the live key");
            Assert.AreNotEqual(SaveStoreKeys.Rescue, SaveBackup.Key);
        }

        [Test]
        public void Backup_OfAFreshSave_LeavesTheSlotsAlone()
        {
            var slots = new Slots();
            Assert.AreEqual(BackupOutcome.Kept, SaveBackup.Keep(slots, Played()));
            string kept = slots.Read(SaveBackup.Key);
            // reset twice: the second reset backs up a fresh save, which must not push the real copy out
            Assert.AreEqual(BackupOutcome.NothingToKeep, SaveBackup.Keep(slots, SaveBlob.Defaults(Today)));
            Assert.AreEqual(kept, slots.Read(SaveBackup.Key));
            Assert.IsNull(slots.Read(SaveBackup.Key2));
        }

        [Test]
        public void Backup_Fails_WhenAWriteFailsOrDoesNotReadBack()
        {
            var slots = new Slots { FailWrite = k => true };
            Assert.AreEqual(BackupOutcome.Failed, SaveBackup.Keep(slots, Played()));

            slots = new Slots();
            SaveBackup.Keep(slots, Played(1));
            string first = slots.Read(SaveBackup.Key);
            slots.FailWrite = k => k == SaveBackup.Key2;
            Assert.AreEqual(BackupOutcome.Failed, SaveBackup.Keep(slots, Played(2)));
            Assert.AreEqual(first, slots.Read(SaveBackup.Key), "the newest copy isn't overwritten when the older one couldn't move");

            slots = new Slots { Mangle = (k, v) => v.Substring(0, v.Length - 1) };
            Assert.AreEqual(BackupOutcome.Failed, SaveBackup.Keep(slots, Played()));
        }

        // ---------------------------------------------------------------- the confirms

        [Test]
        public void Reset_NeedsTwoDeliberateSteps()
        {
            var r = new ResetConfirm();
            Assert.IsFalse(r.Asking);
            Assert.IsFalse(r.TryErase(), "the erase button does nothing before the question");
            r.Ask();
            Assert.IsTrue(r.Asking);
            r.Cancel();
            Assert.IsFalse(r.TryErase(), "cancel closes the question");
            r.Ask();
            Assert.IsTrue(r.TryErase());
            Assert.IsFalse(r.TryErase(), "one yes erases once");
            Assert.IsFalse(r.Asking);
        }

        [Test]
        public void Import_NeedsAGoodCheckThenAYes()
        {
            var c = new ImportConfirm();
            Assert.IsNull(c.TakeConfirmed());
            SaveBlob current = Played(5);
            c.Check("nope", current, Today);
            Assert.IsNull(c.Pending);
            Assert.IsNull(c.TakeConfirmed(), "a bad check can't be confirmed");
            string code = ExportCode.Export(Played(800));
            c.Check(code, current, Today);
            Assert.IsNotNull(c.Pending);
            c.Cancel();
            Assert.IsNull(c.TakeConfirmed(), "cancel drops it");
            c.Check(code, current, Today);
            c.Check("WT1-broken-00000000", current, Today);
            Assert.IsNull(c.TakeConfirmed(), "a newer bad check replaces the good one");
            c.Check(code, current, Today);
            SaveBlob got = c.TakeConfirmed();
            Assert.IsNotNull(got);
            Assert.AreEqual(800 + 0, got.Fans.Lifetime);
            Assert.IsNull(c.TakeConfirmed(), "one yes, one import");
            Assert.AreEqual(5, current.Fans.Lifetime, "the current save object is never changed by the import rules");
        }

        /// <summary>SaveStore's rescue keys live in the Game layer; the names are repeated here so they can't collide.</summary>
        private static class SaveStoreKeys
        {
            public const string Rescue = SaveBlob.Key + ".unreadable";
        }
    }
}
