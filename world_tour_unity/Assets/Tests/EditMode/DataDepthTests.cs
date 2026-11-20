using System;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using WorldTour.Core.Data;
using WorldTour.Core.Maths.Gen;
using WorldTour.Core.Progress;

namespace WorldTour.Core.Tests
{
    /// <summary>
    /// The nesting limit (StrictJson.MaxDepth = 64). Deeper text used to overflow the stack, which no try/catch can
    /// catch, through a stored save or a crafted import code (1.5/1.6 tester, D1). It must be refused instead.
    /// </summary>
    [TestFixture]
    public class DataDepthTests
    {
        static string Arrays(int depth) => new string('[', depth) + new string(']', depth);

        [Test]
        public void Depth64IsRead()
        {
            JsonValue v = StrictJson.Parse(Arrays(StrictJson.MaxDepth));
            int depth = 0;
            while (v.Kind == JsonKind.Array && v.Count > 0) { v = v[0]; depth++; }
            Assert.AreEqual(StrictJson.MaxDepth - 1, depth, "63 arrays inside the outer one");
            Assert.IsNotNull(StrictJson.Parse("{\"a\":" + Arrays(StrictJson.MaxDepth - 1) + "}"), "64 levels counting an object");
        }

        [Test]
        public void Depth65IsRefusedWithLineAndColumn()
        {
            var e = Assert.Throws<StrictJsonException>(() => StrictJson.Parse(Arrays(65)));
            Assert.AreEqual("the data is too deeply nested (more than 64 levels of { } and [ ])", e.Reason);
            Assert.AreEqual(1, e.Line);
            Assert.AreEqual(65, e.Column);
            var o = Assert.Throws<StrictJsonException>(() => StrictJson.Parse("{\n \"a\": " + Arrays(64) + "}"));
            Assert.AreEqual(2, o.Line);
            Assert.AreEqual(70, o.Column, "the 65th opener: column 7 plus 63");
        }

        [Test]
        public void Depth20000IsRefusedWithoutCrashing()
        {
            var e = Assert.Throws<StrictJsonException>(() => StrictJson.Parse(Arrays(20000)));
            Assert.AreEqual(65, e.Column);
            Assert.IsFalse(StrictJson.TryParse("{\"a\":" + Arrays(20000) + "}", out _, out StrictJsonException err));
            StringAssert.Contains("too deeply nested", err.Reason);
        }

        [Test]
        public void DeepStoredSaveIsUnreadableNotACrash()
        {
            LoadResult r = SaveJson.Load(new string('[', 2000) + new string(']', 2000), "2026-11-20");
            Assert.IsTrue(r.Unreadable);
            Assert.IsNotNull(r.Blob, "defaults to play on");
            StringAssert.Contains("too deeply nested", r.Error);
            Assert.IsTrue(SaveJson.Load(Arrays(20000), "2026-11-20").Unreadable);
        }

        [Test]
        public void DeepImportIsRefusedNotACrash()
        {
            SaveBlob current = SaveBlob.Defaults("2026-11-20");
            // a crafted WT1 code: base64url(raw DEFLATE of 20,000 nested arrays) plus its CRC-32, as ExportCode builds one
            byte[] json = Encoding.UTF8.GetBytes(Arrays(20000));
            string code = ExportCode.Prefix + Convert.ToBase64String(Deflate.Compress(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_')
                + "-" + Deflate.Crc32(json).ToString("x8", CultureInfo.InvariantCulture);
            ImportPreview p = ExportCode.Preview(code, current, "2026-11-20");
            Assert.IsFalse(p.Ok);
            Assert.AreEqual("the save inside the code can't be read.", p.Error);
            // and the file path (text starting with {)
            ImportPreview f = ExportCode.Preview("{\"fans\":" + Arrays(20000) + "}", current, "2026-11-20");
            Assert.IsFalse(f.Ok);
            Assert.AreEqual("the save inside the code can't be read.", f.Error);
        }
    }
}
