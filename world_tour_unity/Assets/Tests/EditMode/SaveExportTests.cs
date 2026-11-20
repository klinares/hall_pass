using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using NUnit.Framework;
using WorldTour.Core.Maths;
using WorldTour.Core.Progress;

namespace WorldTour.Core.Tests
{
    /// <summary>Export and import of the save as a code or a file, and the DEFLATE code underneath.</summary>
    public class SaveExportTests
    {
        private const string Today = "2026-11-20";

        [Test]
        public void Code_RoundTrips_TheWholeSave()
        {
            SaveBlob s = SaveFixtures.Full();
            string code = ExportCode.Export(s);
            StringAssert.StartsWith("WT1-", code);
            Console.WriteLine("full save export code: " + code.Length + " characters (json " + SaveJson.ToText(s).Length + ")");
            Assert.Less(code.Length, SaveJson.ToText(s).Length / 3, "the code is compressed");
            Assert.IsTrue(code.All(c => c < 128 && !char.IsWhiteSpace(c)), "one line of plain ASCII");
            ImportPreview p = ExportCode.Preview(code, SaveBlob.Defaults(Today), Today);
            Assert.IsTrue(p.Ok, p.Error);
            Assert.AreEqual(SaveJson.ToText(s), SaveJson.ToText(ExportCode.Apply(p)));
            Assert.AreEqual(s.Fans.Lifetime, p.From.Lifetime);
            Assert.AreEqual(8, p.From.Venues);
        }

        [Test]
        public void Code_SurvivesWrappingAndSpaces()
        {
            string code = ExportCode.Export(SaveFixtures.Full());
            var sb = new StringBuilder("  ");
            for (int i = 0; i < code.Length; i += 60) sb.Append(code, i, Math.Min(60, code.Length - i)).Append("\r\n ");
            ImportPreview p = ExportCode.Preview(sb.ToString(), null, Today);
            Assert.IsTrue(p.Ok, p.Error);
        }

        [Test]
        public void File_RoundTrips()
        {
            SaveBlob s = SaveFixtures.Full();
            ImportPreview p = ExportCode.Preview(ExportCode.ExportFile(s), null, Today);
            Assert.IsTrue(p.Ok, p.Error);
            Assert.AreEqual(SaveJson.ToText(s), SaveJson.ToText(p.Incoming));
            Assert.IsTrue(ExportCode.Preview((char)0xFEFF + ExportCode.ExportFile(s), null, Today).Ok, "a byte-order mark is fine");
        }

        [Test]
        public void BadCodes_AreRejectedWithAMessage()
        {
            string code = ExportCode.Export(SaveFixtures.Full());
            int dash = code.LastIndexOf('-');
            string flipped = code.Substring(0, 40) + (code[40] == 'A' ? 'B' : 'A') + code.Substring(41);
            string wrongCheck = code.Substring(0, dash + 1) + (code[dash + 1] == '0' ? '1' : '0') + code.Substring(dash + 2);
            var cases = new[]
            {
                new[] { "", "paste your code first." },
                new[] { "   ", "paste your code first." },
                new[] { "hello there", "that doesn't look like a world tour code. it starts with WT1-" },
                new[] { "WT2-abcd-12345678", "that code is from a newer version of the game." },
                new[] { code.Substring(0, code.Length - 3), "the end of the code is missing." },
                new[] { code.Substring(0, dash) + "-zzzzzzzz", "the end of the code is mistyped." },
                new[] { "WT1-ab$d-12345678", "part of the code is mistyped." },
                new[] { code.Substring(0, code.Length / 2) + code.Substring(dash), null },
                new[] { flipped, null },
                new[] { wrongCheck, "the code doesn't check out. copy it again, all of it." },
                new[] { "{\"hello\":1}", "that isn't a world tour save." },
                new[] { "{\"fans\":{},\"venues\":{},\"v\":2}", "that save is from a newer version of the game." },
                new[] { "{\"fans\":", "the save inside the code can't be read." },
            };
            var current = SaveFixtures.Full();
            string before = SaveJson.ToText(current);
            foreach (var c in cases)
            {
                ImportPreview p = ExportCode.Preview(c[0], current, Today);
                Assert.IsFalse(p.Ok, "accepted: " + c[0]);
                Assert.IsNotNull(p.Error);
                Assert.AreEqual(p.Error, p.Error.ToLowerInvariant().Replace("wt1-", "WT1-"), "lowercase voice");
                if (c[1] != null) Assert.AreEqual(c[1], p.Error, c[0]);
                Assert.Throws<InvalidOperationException>(() => ExportCode.Apply(p));
            }
            Assert.AreEqual(before, SaveJson.ToText(current), "a failed import changes nothing");
        }

        [Test]
        public void Import_SaysWhatWouldBeLost_BeforeReplacing()
        {
            SaveBlob big = SaveFixtures.Full();
            SaveBlob small = SaveBlob.Defaults(Today);
            ProgressRules.AddFans(small, 21000);
            ImportPreview p = ExportCode.Preview(ExportCode.Export(small), big, Today);
            Assert.IsTrue(p.Ok);
            Assert.IsTrue(p.LosesProgress);
            Assert.That(p.Losses, Has.Some.StartsWith("total fans would go from 1234567 to 21000"));
            Assert.That(p.Losses, Has.Some.EqualTo("some unlocked venues would lock again"));
            Assert.That(p.Losses, Has.Some.EqualTo("some journal stickers would go"));
            Assert.AreEqual(1234567, big.Fans.Lifetime, "preview changes nothing");

            ImportPreview up = ExportCode.Preview(ExportCode.Export(big), small, Today);
            Assert.IsTrue(up.Ok);
            Assert.IsFalse(up.LosesProgress, "moving to a save with more on it loses nothing");
            Assert.AreEqual(0, up.Losses.Count);
        }

        // ---------- the DEFLATE underneath ----------

        [Test]
        public void Deflate_ReadsWhatZlibWrites_AndZlibReadsWhatItWrites()
        {
            var rng = new Rng(77);
            foreach (byte[] data in new[]
            {
                new byte[0], new byte[] { 65 }, Encoding.ASCII.GetBytes(new string('a', 70000)),
                Encoding.UTF8.GetBytes(SaveJson.ToText(SaveFixtures.Full())),
                Enumerable.Range(0, 50000).Select(i => (byte)rng.Int(0, 255)).ToArray()
            })
            {
                byte[] ours = Deflate.Compress(data);
                CollectionAssert.AreEqual(data, Deflate.Inflate(ours), "ours -> ours");
                CollectionAssert.AreEqual(data, ZlibInflate(ours), "ours -> zlib");
                // Unity's Mono DeflateStream writes no bytes at all for empty input (not a valid stream; .NET writes 03 00),
                // so the zlib -> ours direction starts at one byte
                if (data.Length == 0) continue;
                foreach (CompressionLevel level in new[] { CompressionLevel.Optimal, CompressionLevel.Fastest, CompressionLevel.NoCompression })
                    CollectionAssert.AreEqual(data, Deflate.Inflate(ZlibDeflate(data, level)), "zlib " + level + " -> ours");
            }
        }

        [Test]
        public void Deflate_DamagedStreams_ThrowNotHang()
        {
            byte[] good = Deflate.Compress(Encoding.ASCII.GetBytes(SaveJson.ToText(SaveFixtures.Full())));
            var rng = new Rng(5);
            int rejected = 0;
            for (int t = 0; t < 300; t++)
            {
                byte[] bad = (byte[])good.Clone();
                if (t % 3 == 0) Array.Resize(ref bad, rng.Int(0, bad.Length - 1));
                else bad[rng.Int(0, bad.Length - 1)] ^= (byte)(1 << rng.Int(0, 7));
                try { Deflate.Inflate(bad); }
                catch (InvalidDataException) { rejected++; }
            }
            Assert.Greater(rejected, 100);
            Assert.Throws<InvalidDataException>(() => Deflate.Inflate(new byte[] { 0xff, 0xff, 0xff }));
            Assert.Throws<InvalidDataException>(() => Deflate.Inflate(Deflate.Compress(new byte[5000]), 100), "output is bounded");
        }

        [Test]
        public void Deflate_EmptyStreamFromZlib_ReadsAsEmpty()
        {
            CollectionAssert.AreEqual(new byte[0], Deflate.Inflate(new byte[] { 0x03, 0x00 }), "the empty fixed block zlib writes");
            CollectionAssert.AreEqual(new byte[0], Deflate.Inflate(new byte[] { 0x01, 0x00, 0x00, 0xff, 0xff }), "an empty stored block");
        }

        [Test]
        public void Crc32_KnownValue()
        {
            Assert.AreEqual(0xCBF43926u, Deflate.Crc32(Encoding.ASCII.GetBytes("123456789")));
        }

        private static byte[] ZlibInflate(byte[] raw)
        {
            using (var src = new MemoryStream(raw))
            using (var z = new DeflateStream(src, CompressionMode.Decompress))
            using (var dst = new MemoryStream())
            {
                z.CopyTo(dst);
                return dst.ToArray();
            }
        }

        private static byte[] ZlibDeflate(byte[] data, CompressionLevel level)
        {
            using (var dst = new MemoryStream())
            {
                using (var z = new DeflateStream(dst, level, true)) z.Write(data, 0, data.Length);
                return dst.ToArray();
            }
        }
    }
}
