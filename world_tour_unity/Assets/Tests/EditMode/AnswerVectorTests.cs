using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using WorldTour.Core.Maths;

namespace WorldTour.Core.Tests
{
    /// <summary>
    /// Checks the C# Answers and Mistakes against vectors printed by the web game's own answer.js and
    /// mistakes.js (world_tour_unity/Tools/answer_vectors.mjs, which writes answer_vectors.json next to this file).
    /// Frozen in phase 9.3: the generator script and the JS source it read were retired; this file is the checked-in record.
    /// </summary>
    public class AnswerVectorTests
    {
        [Serializable] public class Vectors { public Target[] targets; public TextCase[] texts; public Shown[] shown; public PointCase[] points; public TrapSet[] traps; public ChoiceCase[] choices; }
        [Serializable] public class Val { public string k; public double a; public double b; }
        [Serializable] public class Target { public string type; public Val v; }
        [Serializable] public class TextCase { public string text; public string parse; public string[] checks; public bool[] matches; }
        [Serializable] public class Shown { public string type; public Val v; public string @out; }
        [Serializable] public class PointCase { public string text; public string @out; }
        [Serializable] public class TrapIn { public Val v; public string why; }
        [Serializable] public class TrapSet { public Target answer; public TrapIn[] traps; public string @out; }
        [Serializable] public class ChoiceCase { public long seed; public int n; public Target answer; public TrapIn[] traps; public Val[] fill; public string @out; }

        private static Vectors _vectors;

        private static Vectors Load()
        {
            if (_vectors != null) return _vectors;
            string path = Path.Combine(Application.dataPath, "Tests", "EditMode", "answer_vectors.json");
            if (!File.Exists(path)) throw new FileNotFoundException("missing answer vectors; the vector file is frozen and must stay checked in", path);
            var v = JsonUtility.FromJson<Vectors>(File.ReadAllText(path));
            if (v == null || v.targets == null || v.targets.Length == 0 || v.texts == null || v.texts.Length == 0)
                throw new InvalidDataException("answer_vectors.json is empty or malformed");
            _vectors = v;
            return v;
        }

        private static string Esc(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                if (c >= ' ' && c < 0x7f) sb.Append(c);
                else sb.Append("\\u").Append(((int)c).ToString("x4"));
            }
            return sb.Append('"').ToString();
        }

        private static IEnumerable<TestCaseData> Index(Func<Vectors, int> count, string label) =>
            Enumerable.Range(0, count(Load())).Select(i => new TestCaseData(i).SetName("{m} " + label + " " + (i + 1).ToString("000", CultureInfo.InvariantCulture)));

        private static IEnumerable<TestCaseData> Texts() =>
            Load().texts.Select((t, i) => new TestCaseData(i).SetName("{m} text " + (i + 1).ToString("000", CultureInfo.InvariantCulture)).SetDescription(Esc(t.text)));
        private static IEnumerable<TestCaseData> ShownCases() => Index(v => v.shown.Length, "show");
        private static IEnumerable<TestCaseData> TrapSets() => Index(v => v.traps.Length, "set");
        private static IEnumerable<TestCaseData> ChoiceCases() => Index(v => v.choices.Length, "set");

        private static MathValue Value(Val v)
        {
            switch (v.k)
            {
                case "num": return MathValue.Num(v.a);
                case "frac": return MathValue.Frac(v.a, v.b);
                case "pt": return MathValue.Pt(v.a, v.b);
                case "null": return MathValue.Null;
                default: throw new InvalidDataException("unknown value kind " + v.k);
            }
        }

        private static AnswerType Type(string t)
        {
            switch (t)
            {
                case "money": return AnswerType.Money;
                case "int": return AnswerType.Int;
                case "decimal": return AnswerType.Decimal;
                case "fraction": return AnswerType.Fraction;
                case "point": return AnswerType.Point;
                default: throw new InvalidDataException("unknown answer type " + t);
            }
        }

        private static Answer ToAnswer(Target t) => new Answer(Type(t.type), Value(t.v));
        private static string Bits(CheckResult r) => (r.Ok ? "1" : "0") + (r.Simplify ? "1" : "0") + (r.Empty ? "1" : "0") + (r.Bad ? "1" : "0");
        private static string Attempt(Func<string> f) { try { return f(); } catch (Exception) { return "throws"; } }

        // ---------- intended differences from the JS (QUESTIONS 7; see IntendedDifferences.md) ----------
        // The JS results stay the reference. Only these documented changes are applied to them, by rule, before comparing:
        //  7.1 a comma that isn't thousands grouping makes number text unreadable: parse null, check "bad", no match;
        //  7.2 spaces around "/" are accepted: the text gives the JS results of the same text without those spaces;
        //  7.3 typed text that isn't a point gets "bad" instead of "empty" against a point answer.

        private static readonly System.Text.RegularExpressions.Regex ProperGrouping =
            new System.Text.RegularExpressions.Regex(@"(?<![0-9.,])[0-9]{1,3}(?:,[0-9]{3})+(?![0-9,])");
        private static readonly System.Text.RegularExpressions.Regex SpacedSlash =
            new System.Text.RegularExpressions.Regex(@"[ \t]+/[ \t]*|[ \t]*/[ \t]+");

        /// <summary>7.1: after the same trim and "$" the parser does, some comma is left that isn't thousands grouping.</summary>
        internal static bool HasStrayComma(string text)
        {
            string s = Answers.JsTrim(text);
            if (s.StartsWith("$", StringComparison.Ordinal)) s = Answers.JsTrim(s.Substring(1));
            return ProperGrouping.Replace(s, "").IndexOf(',') >= 0;
        }

        /// <summary>The JS results this text should give in Unity, and which rule changed them (null: unchanged).</summary>
        private static TextCase Intended(Vectors v, TextCase c, out string rule)
        {
            rule = null;
            var r = new TextCase { text = c.text, parse = c.parse, checks = (string[])c.checks.Clone(), matches = (bool[])c.matches.Clone() };
            TextCase src = null;
            if (c.parse != "null" && HasStrayComma(c.text)) { rule = "7.1"; r.parse = "null"; }   // the JS read it by dropping commas
            else if (SpacedSlash.IsMatch(c.text))
            {
                string tight = SpacedSlash.Replace(c.text, "/");
                src = v.texts.FirstOrDefault(t => t.text == tight);
                Assert.IsNotNull(src, "7.2: the vectors need the text without spaces too: " + Esc(tight));
                rule = "7.2"; r.parse = src.parse;
            }
            for (int k = 0; k < v.targets.Length; k++)
            {
                if (v.targets[k].type == "point")
                {
                    for (int j = 0; j < 2; j++)
                        if (r.checks[2 * k + j] == "0010" && Answers.JsTrim(c.text) != "") { r.checks[2 * k + j] = "0001"; rule = rule ?? "7.3"; }
                    continue;
                }
                if (rule == "7.1") { r.checks[2 * k] = r.checks[2 * k + 1] = "0001"; r.matches[k] = false; }
                else if (rule == "7.2") { r.checks[2 * k] = src.checks[2 * k]; r.checks[2 * k + 1] = src.checks[2 * k + 1]; r.matches[k] = src.matches[k]; }
            }
            return r;
        }

        [Test]
        public void IntendedDifferencesCoverExactlyTheDocumentedTexts()
        {
            Vectors v = Load();
            var by = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (TextCase c in v.texts)
            {
                Intended(v, c, out string rule);
                if (rule == null) continue;
                if (!by.ContainsKey(rule)) by[rule] = new List<string>();
                by[rule].Add(c.text);
            }
            CollectionAssert.AreEquivalent(new[] { "1,0,0,0", "3,50", "1,5", "3,5" }, by["7.1"]);
            CollectionAssert.AreEquivalent(new[] { "3 /4", "3/ 4", "3 / 4" }, by["7.2"]);
            Assert.IsTrue(by["7.3"].Contains("abc") && by["7.3"].Contains("1 1/2") && !by["7.3"].Contains("(3, 5)") && !by["7.3"].Contains(""),
                "7.3 is every non-blank text that isn't a point");
            TestContext.Out.WriteLine(string.Join("; ", by.Select(kv => kv.Key + ": " + kv.Value.Count + " texts")));
        }

        [Test, TestCaseSource(nameof(Texts))]
        public void TypedTextMatchesJs(int i)
        {
            Vectors v = Load();
            TextCase c = Intended(v, v.texts[i], out string rule);
            string at = Esc(c.text) + (rule != null ? " (intended difference " + rule + ")" : "");
            ParsedNumber p = Answers.ParseNumber(c.text);
            string parse = p == null ? "null" : p.V.N.ToString(CultureInfo.InvariantCulture) + "/" + p.V.D.ToString(CultureInfo.InvariantCulture) + " " + (p.Simplest ? "true" : "false") + " " + p.Form;
            Assert.AreEqual(c.parse, parse, "parseNumber(" + at + ")");
            Assert.AreEqual(v.targets.Length * 2, c.checks.Length);
            Assert.AreEqual(v.targets.Length, c.matches.Length);
            for (int k = 0; k < v.targets.Length; k++)
            {
                Answer a = ToAnswer(v.targets[k]);
                string target = v.targets[k].type + " " + Value(v.targets[k].v);
                Assert.AreEqual(c.checks[2 * k], Bits(Answers.Check(c.text, a)), "check(" + at + ", " + target + ") [ok simplify empty bad]");
                Assert.AreEqual(c.checks[2 * k + 1], Bits(Answers.Check(c.text, a, simplest: true)), "check(" + at + ", " + target + ", simplest) [ok simplify empty bad]");
                Assert.AreEqual(c.matches[k], Answers.Matches(c.text, a.Value, a.Type), "matches(" + at + ", " + target + ")");
            }
        }

        [Test, TestCaseSource(nameof(ShownCases))]
        public void ShowMatchesJs(int i)
        {
            Shown c = Load().shown[i];
            var a = new Answer(Type(c.type), Value(c.v));
            Assert.AreEqual(c.@out, Attempt(() => Answers.Show(a)), "show(" + c.type + " " + a.Value + ")");
        }

        [Test]
        public void ParsePointMatchesJs()
        {
            foreach (PointCase c in Load().points)
                Assert.AreEqual(c.@out, Answers.ParsePoint(c.text).ToString(), "parsePoint(" + Esc(c.text) + ")");
        }

        [Test, TestCaseSource(nameof(TrapSets))]
        public void CleanTrapsMatchesJs(int i)
        {
            TrapSet c = Load().traps[i];
            Answer a = ToAnswer(c.answer);
            var traps = c.traps.Select(t => new Trap(Value(t.v), t.why)).ToList();
            Assert.AreEqual(c.@out, Attempt(() => string.Join("|", Mistakes.CleanTraps(a, traps).Select(t => t.ToString()))));
        }

        [Test, TestCaseSource(nameof(ChoiceCases))]
        public void ChoiceSetMatchesJs(int i)
        {
            ChoiceCase c = Load().choices[i];
            Answer a = ToAnswer(c.answer);
            var traps = c.traps.Select(t => new Trap(Value(t.v), t.why)).ToList();
            var fill = c.fill.Select(Value).ToList();
            Assert.AreEqual(c.@out, Attempt(() => string.Join("|", Mistakes.ChoiceSet(new Rng(c.seed), a, traps, fill, c.n).Select(x => x.ToString()))), "seed " + c.seed);
        }

        [Test]
        public void VectorFileDecodesEscapesAndCoversEnough()
        {
            Vectors v = Load();
            var texts = new HashSet<string>(v.texts.Select(t => t.text));
            foreach (string s in new[] { "\u00a03/4", "\ufeff5", "\u00855", "\uff15", "1\t1/2", "5\n", "", "3 /4", "$1.5", "1,000" })
                Assert.IsTrue(texts.Contains(s), "vector text missing or mis-decoded: " + Esc(s));
            Assert.GreaterOrEqual(v.texts.Length, 90);
            Assert.AreEqual(17, v.targets.Length);
            Assert.GreaterOrEqual(v.choices.Length, 24);
        }
    }
}
