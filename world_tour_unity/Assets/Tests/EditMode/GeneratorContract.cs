using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Core.Tests
{
    /// <summary>
    /// Shared checks every generator must pass, ported from world_tour/tests/math/helpers.mjs
    /// (<c>RUNS</c>, <c>seeds</c>, <c>contract</c>, <c>repeatable</c>), with every assertion.
    /// </summary>
    public static class GeneratorContract
    {
        public const int Runs = 3000;

        /// <summary>JS: <c>seeds(level, n = RUNS)</c>: 1000 * level + i * 7919 + 1.</summary>
        public static IEnumerable<long> Seeds(int level, int n = Runs)
        {
            for (int i = 0; i < n; i++) yield return 1000L * level + i * 7919L + 1;
        }

        // JS \s
        private const string Ws = @"[\t\n\v\f\r \u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000\ufeff]";
        private static readonly Regex Broken = new Regex(@"undefined|NaN|null|\[object", RegexOptions.CultureInvariant);
        private static readonly Regex Markup = new Regex(@"\[\[[^\]]*\]\]", RegexOptions.CultureInvariant);
        private static readonly Regex BareFraction = new Regex(@"[0-9]+/[0-9]+", RegexOptions.CultureInvariant);

        /// <summary>JS <c>key(v)</c>: "x,y" for a point, n / d as a number for a fraction, else the number.</summary>
        public static string Key(MathValue v)
        {
            switch (v.Kind)
            {
                case ValueKind.Point: return JsNumber.Str(v.X) + "," + JsNumber.Str(v.Y);
                case ValueKind.Fraction: return JsNumber.Str(v.N / v.D);
                case ValueKind.Number: return JsNumber.Str(v.Number);
                default: return "null";
            }
        }

        /// <summary>JS <c>key(c.value)</c> for a choice: a text choice keys as its text (JS String(v)).</summary>
        public static string Key(Choice c) => c.IsText ? c.Text : Key(c.Value);

        /// <summary>The prompt, label and steps joined with spaces, as the JS contract reads them.</summary>
        public static string Text(Problem p) => string.Join(" ", new[] { p.Prompt, p.Label }.Concat(p.Steps));

        /// <summary>JS: <c>contract(p, skill, level)</c>.</summary>
        public static void Contract(Problem p, string skill, int level)
        {
            string at = " [" + skill + " L" + level + " seed " + p.Seed + "]";
            Assert.AreEqual(skill, p.Skill, "skill" + at);
            Assert.AreEqual(level, p.Level, "level" + at);
            Assert.IsNotNull(p.Kind, "kind is a string" + at);
            Assert.IsTrue(p.Params != null && p.Params.Kind == JsonKind.Object, "params" + at);
            Assert.IsNotNull(p.Answer, "answer" + at);
            Assert.IsTrue(Enum.IsDefined(typeof(AnswerType), p.Answer.Type), "answer type " + p.Answer.Type + at);
            Assert.IsNotNull(p.Prompt, "prompt is a string" + at);
            Assert.IsNotNull(p.Label, "label is a string" + at);
            Assert.IsTrue(p.Steps != null && p.Steps.Count == 3 && p.Steps.All(s => s != null && s.Length > 3), "three hint steps" + at);
            string text = Text(p);
            Assert.IsFalse(Broken.IsMatch(text), "no broken text: " + text + at);
            Assert.IsFalse(BareFraction.IsMatch(Markup.Replace(text, "")), "fractions only inside [[ ]] markup: " + text + at);
            Assert.DoesNotThrow(() => Answers.Show(p.Answer), "show(answer)" + at);
            if (p.Answer.Type != AnswerType.Point)
                Assert.IsTrue(Answers.ValueOf(p.Answer).N >= 0, "answer not negative" + at);
            // the bulb's two steps never state the answer: only the worked step (3) may
            if (p.Answer.Type == AnswerType.Int || p.Answer.Type == AnswerType.Money || p.Answer.Type == AnswerType.Decimal)
            {
                var leak = new Regex("= (<b>)?" + Regex.Escape(Answers.Show(p.Answer)) + "(</b>)?([.,:]|" + Ws + @"|\z)", RegexOptions.CultureInvariant);
                foreach (string s in p.Steps.Take(2)) Assert.IsFalse(leak.IsMatch(s), "hint gives the answer away: " + s + at);
            }
            // traps: tagged, different from the answer and from each other
            string ak = Key(p.Answer.Value);
            var seen = new HashSet<string>();
            foreach (Trap t in p.Traps ?? new List<Trap>())
            {
                Assert.IsNotNull(t.Why, "trap tagged" + at);
                Assert.AreNotEqual(ak, Key(t.Value), "trap differs from answer" + at);
                Assert.IsTrue(seen.Add(Key(t.Value)), "no duplicate traps" + at);
            }
            // choices: exactly one correct, no duplicates, every distractor tagged
            if (p.Choices != null)
            {
                Assert.IsTrue(p.Choices.Count >= 3, "at least 3 choices" + at);
                Assert.AreEqual(1, p.Choices.Count(c => c.Correct), "exactly one correct choice" + at);
                var ks = p.Choices.Select(c => Key(c)).ToList();
                Assert.AreEqual(ks.Count, new HashSet<string>(ks).Count, "no duplicate choices" + at);
                foreach (Choice c in p.Choices.Where(c => !c.Correct)) Assert.IsNotNull(c.Why, "distractor tagged" + at);
            }
        }

        /// <summary>JS: <c>repeatable(gen, level, seed)</c>: same seed, same problem.</summary>
        public static void Repeatable(Func<int, long, Problem> gen, int level, long seed)
        {
            Assert.AreEqual(gen(level, seed).ToCanonicalJson(), gen(level, seed).ToCanonicalJson(), "same seed, same problem");
        }
    }
}
