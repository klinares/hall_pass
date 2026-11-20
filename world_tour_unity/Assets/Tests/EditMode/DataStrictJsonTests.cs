using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Data;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Core.Tests
{
    [TestFixture]
    public class DataStrictJsonTests
    {
        [Test]
        public void ParsesTreeWithOrderPositionsAndRawNumbers()
        {
            string text = "{\n  \"b\": 12,\n  \"a\": [true, null, \"x\", 12.5, 1e3],\n  \"c\": { \"n\": -0.25 }\n}\n";
            JsonValue v = StrictJson.Parse(text);
            CollectionAssert.AreEqual(new[] { "b", "a", "c" }, v.Keys.ToList());
            Assert.AreEqual(1, v.Line);
            Assert.AreEqual(1, v.Column);
            Assert.AreEqual(2, v["b"].Line);
            Assert.AreEqual(8, v["b"].Column);
            Assert.AreEqual("12", v["b"].RawNumber);
            Assert.IsTrue(v["b"].IsWholeNumberToken);
            Assert.AreEqual(12.0, v["b"].Number);
            JsonValue a = v["a"];
            Assert.AreEqual(5, a.Count);
            Assert.IsTrue(a[0].BoolValue);
            Assert.IsTrue(a[1].IsNull);
            Assert.AreEqual(3, a[1].Line);
            Assert.AreEqual("x", a[2].String);
            Assert.IsFalse(a[3].IsWholeNumberToken);
            Assert.AreEqual(12.5, a[3].Number);
            Assert.IsFalse(a[4].IsWholeNumberToken, "1e3 is typed as an exponent");
            Assert.AreEqual(1000.0, a[4].Number);
            Assert.AreEqual(-0.25, v["c"]["n"].Number);
            Assert.IsTrue(v.TryGetKeyPosition("a", out int kl, out int kc));
            Assert.AreEqual(3, kl);
            Assert.AreEqual(3, kc);
            Assert.IsFalse(v.TryGetKeyPosition("zz", out _, out _));
        }

        [Test]
        public void CanonicalWriterIgnoresPositions()
        {
            JsonValue v = StrictJson.Parse("{ \"b\": 1, \"a\": [true, null, \"x\", 0.1] }");
            Assert.AreEqual("{\"a\":[true,null,\"x\",0.1],\"b\":1}", v.ToCanonicalJson());
        }

        [Test]
        public void ValuesBuiltInCodeHaveNoPosition()
        {
            JsonValue v = JsonValue.Obj().Set("n", 12L);
            Assert.AreEqual(0, v.Line);
            Assert.IsNull(v["n"].RawNumber);
            Assert.IsTrue(v["n"].IsWholeNumberToken);
            Assert.IsFalse(JsonValue.Num(2.5).IsWholeNumberToken);
            Assert.IsFalse(v.TryGetKeyPosition("n", out _, out _));
            Assert.AreEqual(0, JsonValue.Null.Line, "the shared Null never carries a position");
            StrictJson.Parse("[null]");
            Assert.AreEqual(0, JsonValue.Null.Line);
        }

        [Test]
        public void SkipsByteOrderMarkAndReadsEscapes()
        {
            string bs = "\\";
            string text = (char)0xFEFF + "{\"s\": \"caf" + bs + "u00e9 " + bs + "\"q" + bs + "\" " + bs + bs + " " + bs + "/\"}";
            JsonValue v = StrictJson.Parse(text);
            Assert.AreEqual("caf" + (char)0xE9 + " \"q\" \\ /", v["s"].String);
            Assert.AreEqual(1, v.Column, "the mark is not counted as a column");
        }

        [TestCase("", 0, 0, "the file is empty")]
        [TestCase("  \r\n ", 0, 0, "the file is empty")]
        [TestCase("{\n  \"a\": 1,\n}", 2, 9, "there is an extra comma here, before }")]
        [TestCase("[1, 2, ]", 1, 6, "there is an extra comma here, before ]")]
        [TestCase("{\"a\": 1 \"b\": 2}", 1, 9, "a comma is missing before this field")]
        [TestCase("[1\n 2]", 2, 2, "a comma is missing before this item")]
        [TestCase("{\"a\": [1, 2}", 1, 12, "found } where ] was expected; the [ on line 1 is not closed")]
        [TestCase("[{\"a\": 1]", 1, 9, "found ] where } was expected; the { on line 1 is not closed")]
        [TestCase("{\n\"a\": {\"b\": 1}", 1, 1, "this { is never closed")]
        [TestCase("{\"a\": \"x}", 1, 7, "this text has no closing quote")]
        [TestCase("{'a': 1}", 1, 2, "use double quotes (\") around text, not single quotes")]
        [TestCase("{a: 1}", 1, 2, "field names need double quotes, like \"a\"")]
        [TestCase("{\"a\": 1, \"a\": 2}", 1, 10, "\"a\" appears twice in the same { }")]
        [TestCase("{\"a\": no}", 1, 7, "\"no\" is not a value: text needs double quotes, and true, false and null are lowercase")]
        [TestCase("{\"a\": 1} x", 1, 10, "there is extra text after the end of the data")]
        [TestCase("{\"a\" 1}", 1, 6, "expected : after \"a\"")]
        [TestCase("[1,,2]", 1, 4, "expected a value here, but found \",\"")]
        [TestCase("[01]", 1, 2, "\"01\" is not a valid number")]
        [TestCase("[1.]", 1, 2, "\"1.\" is not a valid number")]
        [TestCase("// hi\n{}", 1, 1, "JSON files can't have comments")]
        [TestCase("[\"a\tb\"]", 1, 4, "a tab or other control character can't go inside quotes")]
        public void BadInputGivesOneMessageWithLineAndColumn(string text, int line, int column, string reason)
        {
            var e = Assert.Throws<StrictJsonException>(() => StrictJson.Parse(text));
            Assert.AreEqual(reason, e.Reason);
            Assert.AreEqual(line, e.Line, "line");
            Assert.AreEqual(column, e.Column, "column");
            Assert.AreEqual(line > 0 ? "line " + line + ", column " + column + ": " + reason : reason, e.Message);
            Assert.IsFalse(StrictJson.TryParse(text, out JsonValue v, out StrictJsonException err));
            Assert.IsNull(v);
            Assert.AreEqual(reason, err.Reason);
        }

        [Test]
        public void ExceptionIsAFormatException()
        {
            Assert.IsTrue(Assert.Throws<StrictJsonException>(() => StrictJson.Parse("{")) is System.FormatException);
            Assert.Throws<StrictJsonException>(() => StrictJson.Parse(null));
        }
    }
}
