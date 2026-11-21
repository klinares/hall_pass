using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Core.Tests
{
    /// <summary>
    /// The hint ladder rule for every generator (QUESTIONS 8 and 9; BRIEF "Learning rules"): hint 1 is a nudge and
    /// never contains the answer in any form; hint 2 is a partial step and never states the final answer outright;
    /// only hint 3 states the answer; and no hint ever states something false. Run over the 3,000-seed volume set of
    /// every skill and level, so it holds for shapes nobody listed. Legitimate coincidences are exempted by shape,
    /// each with its reason, never by seed.
    /// </summary>
    public class HintLadderTests
    {
        private static readonly string Times = Glyph.Times, Divide = Glyph.Divide, Minus = Glyph.Minus;
        private const string Ops = "+";

        // ---------- reading numbers out of hint text ----------

        /// <summary>A number the player reads: its value, and the text it was read from.</summary>
        internal struct Num
        {
            public double V;
            public string Text;
            public override string ToString() => Text;
        }

        private static readonly Regex Tags = new Regex("<[^>]*>", RegexOptions.CultureInvariant);
        private static readonly Regex Markup = new Regex(@"\[\[(?:([0-9]+) )?([0-9]+)(?:/([0-9]+))?\]\]", RegexOptions.CultureInvariant);
        private static readonly Regex Number = new Regex(@"(?<![0-9])(?:[0-9]{1,3}(?:,[0-9]{3})+|[0-9]+)(?:\.[0-9]+)?(?![0-9])", RegexOptions.CultureInvariant);
        private static readonly Regex Point = new Regex(@"\(([0-9]+), ([0-9]+)\)", RegexOptions.CultureInvariant);

        /// <summary>Tags dropped; fraction markup [[1 1/2]] becomes a number in parentheses the evaluator reads, (1+1/2).</summary>
        internal static string Plain(string s)
        {
            s = Tags.Replace(s, "");
            return Markup.Replace(s, m =>
            {
                string w = m.Groups[1].Value, n = m.Groups[2].Value, d = m.Groups[3].Value;
                string f = d.Length > 0 ? n + Divide + d : n;
                return "(" + (w.Length > 0 ? w + "+" + f : f) + ")";
            });
        }

        /// <summary>Every number in the text: fractions in markup as one value each, then plain numbers (en-US commas, decimals, $).</summary>
        internal static List<Num> Numbers(string s)
        {
            var r = new List<Num>();
            string t = Tags.Replace(s, "");
            foreach (Match m in Markup.Matches(t))
            {
                double w = m.Groups[1].Success && m.Groups[1].Value.Length > 0 ? double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
                double n = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                double d = m.Groups[3].Value.Length > 0 ? double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) : 1;
                r.Add(new Num { V = w + n / d, Text = m.Value });
            }
            t = Markup.Replace(t, " ");
            foreach (Match m in Number.Matches(t)) r.Add(new Num { V = double.Parse(m.Value.Replace(",", ""), CultureInfo.InvariantCulture), Text = m.Value });
            return r;
        }

        internal static bool Same(double a, double b) => Math.Abs(a - b) < 1e-9 * Math.Max(1, Math.Abs(b));

        /// <summary>The answer as one value (money in dollars, as the hints write it); NaN for a point.</summary>
        internal static double AnswerValue(Problem p)
        {
            switch (p.Answer.Type)
            {
                case AnswerType.Money: return p.Answer.Value.Number / 100;
                case AnswerType.Int: return p.Answer.Value.Number;
                case AnswerType.Point: return double.NaN;
                default: return p.Answer.Value.N / p.Answer.Value.D;
            }
        }

        /// <summary>Does the text state the answer: as a number (or a point) on its own, in any form the game writes?</summary>
        internal static bool StatesAnswer(Problem p, string text, out string what)
        {
            what = null;
            if (p.Answer.Type == AnswerType.Point)
            {
                foreach (Match m in Point.Matches(Tags.Replace(text, "")))
                    if (double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) == p.Answer.Value.X &&
                        double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) == p.Answer.Value.Y) { what = m.Value; return true; }
                return false;
            }
            double a = AnswerValue(p);
            foreach (Num n in Numbers(text))
                if (Same(n.V, a)) { what = n.Text; return true; }
            return false;
        }

        // ---------- equations ----------

        /// <summary>An "a op b = c" in a hint: both sides as the evaluator reads them.</summary>
        internal struct Equation
        {
            public string Left, Right;
            public double L, R;
            public bool Parsed, HasOp;
            public bool True => Parsed && Same(L, R);
            public override string ToString() => Left + " = " + Right;
        }

        private static bool IsExprChar(string s, int i)
        {
            char c = s[i];
            if (c >= '0' && c <= '9') return true;
            if (c == '(' || c == ')' || c == ' ' || c == '+' || c.ToString() == Times || c.ToString() == Divide || c.ToString() == Minus) return true;
            // a decimal point or a thousands comma only between digits
            if ((c == '.' || c == ',') && i > 0 && i < s.Length - 1 && char.IsDigit(s[i - 1]) && char.IsDigit(s[i + 1])) return true;
            return false;
        }

        private static string Trim(string e)
        {
            e = e.Trim();
            // drop unmatched parentheses at the ends, and leading or trailing operators
            for (bool changed = true; changed;)
            {
                changed = false;
                int open = e.Count(c => c == '('), close = e.Count(c => c == ')');
                if (close > open && e.EndsWith(")", StringComparison.Ordinal)) { e = e.Substring(0, e.Length - 1).Trim(); changed = true; }
                else if (open > close && e.StartsWith("(", StringComparison.Ordinal)) { e = e.Substring(1).Trim(); changed = true; }
                else if (e.Length > 0 && "+".IndexOf(e[0]) >= 0 || e.StartsWith(Times, StringComparison.Ordinal) || e.StartsWith(Divide, StringComparison.Ordinal) || e.StartsWith(Minus, StringComparison.Ordinal))
                { e = e.Substring(1).Trim(); changed = true; }
                else if (e.Length > 0 && (e.EndsWith("+", StringComparison.Ordinal) || e.EndsWith(Times, StringComparison.Ordinal) || e.EndsWith(Divide, StringComparison.Ordinal) || e.EndsWith(Minus, StringComparison.Ordinal)))
                { e = e.Substring(0, e.Length - 1).Trim(); changed = true; }
            }
            return e;
        }

        private static readonly Regex Remainder = new Regex(@"\A ?remainder ([0-9]+)", RegexOptions.CultureInvariant);

        /// <summary>Every "=" in the text with the arithmetic just before and after it (money and cents signs dropped).</summary>
        internal static List<Equation> Equations(string text)
        {
            string s = Plain(text).Replace("$", "").Replace(Glyph.Cent, "");
            var r = new List<Equation>();
            for (int i = s.IndexOf('='); i >= 0; i = s.IndexOf('=', i + 1))
            {
                int a = i;
                while (a > 0 && IsExprChar(s, a - 1)) a--;
                int b = i + 1;
                while (b < s.Length && IsExprChar(s, b)) b++;
                var e = new Equation { Left = Trim(s.Substring(a, i - a)), Right = Trim(s.Substring(i + 1, b - i - 1)) };
                e.HasOp = e.Left.IndexOfAny(new[] { '+', Times[0], Divide[0], Minus[0] }) >= 0;
                try
                {
                    e.L = ExprEval.Eval(e.Left.Replace(",", ""));
                    e.R = ExprEval.Eval(e.Right.Replace(",", ""));
                    e.Parsed = e.Left.Length > 0 && e.Right.Length > 0;
                    // "N / d = q remainder r": true when N = q x d + r with r < d; the right side then reads as N / d
                    Match rem = Remainder.Match(s.Substring(b));
                    string[] nd = e.Left.Split(Divide[0]);
                    if (e.Parsed && rem.Success && nd.Length == 2)
                    {
                        double n = ExprEval.Eval(nd[0].Replace(",", "")), d = ExprEval.Eval(nd[1].Replace(",", ""));
                        double left = double.Parse(rem.Groups[1].Value, CultureInfo.InvariantCulture);
                        e.Right += " remainder " + rem.Groups[1].Value;
                        e.R = Same(n, e.R * d + left) && left < d ? e.L : double.NaN;
                    }
                }
                catch (FormatException) { e.Parsed = false; }
                r.Add(e);
            }
            return r;
        }

        /// <summary>
        /// The number being divided, for problems that are a division in disguise (an equation that lands on it in hint 1
        /// is the whole calculation): ticket sales' N, pack the truck's volume or crate count, and soundcheck's p / a.
        /// </summary>
        internal static double? Dividend(Problem p)
        {
            if (p.Skill == MultDiv.Skill && (p.Kind == "div" || p.Kind == "rows")) return p.Params["N"].Number;
            if (p.Skill == Volume.Skill && p.Kind == "missing") { JsonValue b = p.Params["boxes"].Items[0]; return b["l"].Number * b["w"].Number * b["h"].Number; }
            if (p.Skill == Volume.Skill && p.Kind == "build") return p.Params["target"].Number;
            int at = p.Skill == Fluency.Skill ? p.Label.IndexOf(" " + Divide + " ", StringComparison.Ordinal) : -1;
            if (at > 0) return double.Parse(p.Label.Substring(0, at).Replace(",", ""), CultureInfo.InvariantCulture);
            return null;
        }

        // ---------- exemptions, by shape ----------

        /// <summary>
        /// For both hints. (1) The same number is already in the prompt or on the label, so repeating it hides nothing:
        /// a divisor equal to the quotient (100 / 10, 784 / 28), a price equal to the change, a factor equal to the
        /// answer of the other step (15 - 3 then 12 / 4 = 3), a fraction equal to the one subtracted. (2) Stage designer,
        /// place: the prompt says where to put the light.
        /// </summary>
        private static string Shared(Problem p, string found)
        {
            if (p.Skill == Coords.Skill && p.Kind == "place") return "xy place: the point is in the prompt";
            double v = Numbers(found).Count == 1 ? Numbers(found)[0].V : double.NaN;
            if (double.IsNaN(v)) return null;
            foreach (Num n in Numbers(p.Prompt).Concat(Numbers(p.Label))) if (Same(n.V, v)) return "the number is in the prompt";
            return null;
        }

        /// <summary>
        /// Hint-1 coincidences: a number equal to the answer that is not the answer.
        /// Each is a shape, with its reason. Returns the reason, or null.
        /// </summary>
        internal static string Hint1Exempt(Problem p, string found)
        {
            string shared = Shared(p, found);
            if (shared != null) return shared;
            // beat maker, take notes out: "how far past [[1]] does it go?" names the one whole the measure must end on.
            if (p.Skill == Fractions.Skill && p.Kind == "takeout" && found == "[[1]]") return "frac takeout: [[1]] is the target";
            // ticket sales, quotient 10: "multiply it by 10" names the strategy (QUESTIONS 8.2 wording), not the answer.
            if (p.Skill == MultDiv.Skill && p.Kind == "div" && found == "10" && p.Steps[0].StartsWith("what happens to a number when you multiply it by 10", StringComparison.Ordinal))
                return "md quotient 10: the advisor's wording names x 10";
            return null;
        }

        /// <summary>Hint-2 partial steps that legitimately contain the answer's value (QUESTIONS 8 and 9 list them as by design).</summary>
        internal static string Hint2Exempt(Problem p, string found)
        {
            string shared = Shared(p, found);
            if (shared != null) return shared;
            // soundcheck, x or / 100: the partial step goes by 10 once ("1,000 / 10 = 100. once more"); its 10 is the step,
            // and equals the answer only when the answer happens to be 10.
            if (p.Skill == Fluency.Skill && p.Kind == "pow" && found == "10" && p.Steps[1].Contains(" 10 = ")) return "flu x/100: the 10 is the first step";
            // merch table, decimal x decimal: the whole-number product "41 x 150 = 6150" is the partial step by design
            // (QUESTIONS 8); an operand or product can equal the change in dollars by coincidence.
            if (p.Skill == Decimals.Skill && p.Params["lines"].Items.Any(l => !l["unit"].IsNull))
            {
                JsonValue b = p.Params["lines"].Items.First(l => !l["unit"].IsNull);
                long amount = b["amount"].Long, each = b["each"].Long;
                string f = found.Replace(",", "");
                if (f == amount.ToString(CultureInfo.InvariantCulture) || f == each.ToString(CultureInfo.InvariantCulture) || f == (amount * each).ToString(CultureInfo.InvariantCulture))
                    return "dec decimal multiply: the whole-number partial product (by design)";
            }
            // ticket sales L3 rows: two candidates and a question (QUESTIONS 8.3 keeps it): she still decides which fits.
            if (p.Skill == MultDiv.Skill && p.Kind == "rows") return "md rows: two candidates, she picks (8.3)";
            // beat maker builders: "you need [[x]] more" (QUESTIONS 8, by design): she still places the blocks.
            if (p.Skill == Fractions.Skill && p.Builder == true) return "frac builder: you need x more (by design)";
            return null;
        }

        // ---------- the scan ----------

        private sealed class Tally
        {
            public readonly SortedDictionary<string, int> Count = new SortedDictionary<string, int>(StringComparer.Ordinal);
            public readonly SortedDictionary<string, string> Example = new SortedDictionary<string, string>(StringComparer.Ordinal);
            public void Add(string key, string example)
            {
                Count[key] = Count.TryGetValue(key, out int n) ? n + 1 : 1;
                if (!Example.ContainsKey(key)) Example[key] = example;
            }
            public string Report() => string.Join("\n", Count.Select(kv => kv.Value + " x " + kv.Key + ", e.g. " + Example[kv.Key]));
        }

        private static IEnumerable<TestCaseData> Cases()
        {
            foreach (string skill in Generators.Skills)
                for (int level = 0; level <= 3; level++)
                    yield return new TestCaseData(skill, level).SetName("Hints " + skill + " level " + level);
        }

        private static string Where(Problem p) => p.Skill + " L" + p.Level + " " + p.Kind + " seed " + p.Seed;

        /// <summary>Hint 1 never contains the answer as a number, nor an equation whose result is the answer or the dividend.</summary>
        [Test, TestCaseSource(nameof(Cases))]
        public void Hint1IsANudge(string skill, int level)
        {
            var t = new Tally();
            int exempt = 0;
            foreach (long seed in GeneratorContract.Seeds(level))
            {
                Problem p = Generators.Generate(skill, level, seed);
                string h = p.Steps[0];
                if (StatesAnswer(p, h, out string what))
                {
                    if (Hint1Exempt(p, what) != null) exempt++;
                    else t.Add(skill + " L" + level + " " + p.Kind + ": hint 1 contains the answer", Where(p) + ": \"" + h + "\" (" + what + ")");
                }
                double? dividend = Dividend(p);
                foreach (Equation e in Equations(h))
                    if (e.Parsed && e.HasOp && dividend.HasValue && Same(e.R, dividend.Value))
                        t.Add(skill + " L" + level + " " + p.Kind + ": hint 1 works out the dividend", Where(p) + ": \"" + h + "\"");
            }
            TestContext.Out.WriteLine(skill + " L" + level + ": hint 1 ok, " + exempt + " exempt by shape");
            Assert.IsEmpty(t.Count, t.Report());
        }

        /// <summary>Hint 2 is a partial step: it never states the final answer.</summary>
        [Test, TestCaseSource(nameof(Cases))]
        public void Hint2LeavesTheLastStep(string skill, int level)
        {
            var t = new Tally();
            int exempt = 0;
            foreach (long seed in GeneratorContract.Seeds(level))
            {
                Problem p = Generators.Generate(skill, level, seed);
                string h = p.Steps[1];
                if (!StatesAnswer(p, h, out string what)) continue;
                if (Hint2Exempt(p, what) != null) { exempt++; continue; }
                t.Add(skill + " L" + level + " " + p.Kind + ": hint 2 states the answer", Where(p) + ": \"" + h + "\" (" + what + ")");
            }
            TestContext.Out.WriteLine(skill + " L" + level + ": hint 2 ok, " + exempt + " exempt by shape");
            Assert.IsEmpty(t.Count, t.Report());
        }

        // ---------- no false numbers (QUESTIONS 9.1) ----------

        private static void AddTree(HashSet<double> into, JsonValue v)
        {
            if (v == null) return;
            switch (v.Kind)
            {
                case JsonKind.Number: into.Add(v.Number); break;
                case JsonKind.Array: foreach (JsonValue x in v.Items) AddTree(into, x); break;
                case JsonKind.Object:
                    if (v["n"] != null && v["d"] != null && v["n"].Kind == JsonKind.Number && v["d"].Kind == JsonKind.Number && v["d"].Number != 0) into.Add(v["n"].Number / v["d"].Number);
                    foreach (string k in v.Keys) AddTree(into, v[k]);
                    break;
                case JsonKind.String: foreach (Num n in Numbers(v.String)) into.Add(n.V); break;
            }
        }

        private static readonly string[] Sups = { ((char)0x00B9).ToString(), ((char)0x00B2).ToString(), ((char)0x00B3).ToString(), ((char)0x2074).ToString() };

        /// <summary>
        /// The numbers a hint may name without stating something false: what the player can see (prompt, label, the
        /// drawing's params and work, superscript exponents), the parts those numbers are made of (place-value parts,
        /// dollars and cents, decimal places, the zeros of a power of ten), the numbers in every equation the hints write
        /// that is true, and one step of + - x / between any of those (a correct intermediate value). The answer is added
        /// for hint 3 only, by the caller.
        /// </summary>
        internal static HashSet<double> Known(Problem p)
        {
            var seen = new HashSet<double> { 0, 1, 10, 100, 1000 };
            foreach (Num n in Numbers(p.Prompt)) seen.Add(n.V);
            foreach (Num n in Numbers(p.Label)) seen.Add(n.V);
            for (int k = 1; k <= 4; k++) if (p.Prompt.Contains("10" + Sups[k - 1])) { seen.Add(k); seen.Add(Math.Pow(10, k)); }
            AddTree(seen, p.Params);
            AddTree(seen, p.Work);
            foreach (string s in p.Steps)
                foreach (Equation e in Equations(s))
                    if (e.True) { foreach (Num n in Numbers(e.Left)) seen.Add(n.V); foreach (Num n in Numbers(e.Right)) seen.Add(n.V); }
            AddWorked(seen, p);
            var parts = new HashSet<double>(seen);
            foreach (double v in seen)
            {
                if (v < 0 || v > 1e7) continue;
                if (v == Math.Floor(v))
                {
                    foreach (long part in MultDiv.Parts((long)v)) parts.Add(part);
                    parts.Add(Math.Floor(v / 100)); parts.Add(v % 100);   // dollars and cents of a price in cents
                    if (v >= 10 && v % 10 == 0 && Math.Abs(Math.Log10(v) - Math.Round(Math.Log10(v))) < 1e-12) parts.Add(Math.Round(Math.Log10(v)));
                }
                else
                {
                    string s = v.ToString("0.########", CultureInfo.InvariantCulture);
                    int dot = s.IndexOf('.');
                    if (dot >= 0) { parts.Add(s.Length - dot - 1); parts.Add(double.Parse(s.Remove(dot, 1), CultureInfo.InvariantCulture)); }
                }
            }
            return parts;
        }

        /// <summary>
        /// Intermediate values the hints name in words rather than in an equation, each worked out here from the params,
        /// independently of the hint text: merch table's cents sum and its carry, ticket sales' tens counts, and beat
        /// maker's blocks, what is written, and (by design, QUESTIONS 8) what is still needed.
        /// </summary>
        private static void AddWorked(HashSet<double> seen, Problem p)
        {
            if (p.Skill == Decimals.Skill)
            {
                long c = p.Params["lines"].Items.Sum(l => l["cost"].Long % 100);
                seen.Add(c); seen.Add(Math.Floor(c / 100.0)); seen.Add(c % 100);
            }
            else if (p.Skill == MultDiv.Skill && p.Kind == "div")
            {
                foreach (JsonValue ch in p.Work["chunks"].Items) seen.Add(ch["c"].Number / 10);
                if (p.Work["lower"] != null) seen.Add(p.Work["lower"]["c"].Number / 10);
            }
            else if (p.Skill == Fractions.Skill)
            {
                List<long> pre = p.Params["pre"].AsLongs(), palette = p.Params["palette"].AsLongs();
                foreach (long d in pre.Concat(palette)) seen.Add(1.0 / d);
                double written = pre.Sum(d => 1.0 / d);
                seen.Add(written);
                if (p.Builder == true)
                {
                    Rational target = p.Params["target"].AsRational();
                    double t = (double)target.N / target.D;
                    seen.Add(Math.Abs(t - written));   // what is still needed, or what to take out
                }
            }
        }

        private static bool In(HashSet<double> set, double v)
        {
            if (set.Contains(v)) return true;
            foreach (double x in set) if (Same(x, v)) return true;
            return false;
        }

        /// <summary>
        /// No hint writes a false equation, and no hint names a number that is neither something she can see, nor a
        /// correct intermediate value, nor (in hint 3 only) the answer. The web game's "the digits stay 441" for 8.82 x 10
        /// is the case this exists for.
        /// </summary>
        [Test, TestCaseSource(nameof(Cases))]
        public void NoHintStatesAFalseNumber(string skill, int level)
        {
            var t = new Tally();
            int equations = 0, numbers = 0;
            foreach (long seed in GeneratorContract.Seeds(level))
            {
                Problem p = Generators.Generate(skill, level, seed);
                HashSet<double> known = Known(p);
                for (int i = 0; i < 3; i++)
                {
                    string h = p.Steps[i];
                    foreach (Equation e in Equations(h))
                    {
                        if (!e.Parsed) continue;
                        equations++;
                        if (!e.True) t.Add(skill + " L" + level + " " + p.Kind + ": hint " + (i + 1) + " writes a false equation", Where(p) + ": " + e + " in \"" + h + "\"");
                    }
                    foreach (Num n in Numbers(h))
                    {
                        numbers++;
                        if (i == 2 && p.Answer.Type != AnswerType.Point && Same(n.V, AnswerValue(p))) continue;
                        if (In(known, n.V)) continue;
                        t.Add(skill + " L" + level + " " + p.Kind + ": hint " + (i + 1) + " names a number from nowhere", Where(p) + ": " + n.Text + " in \"" + h + "\"");
                    }
                }
            }
            TestContext.Out.WriteLine(skill + " L" + level + ": " + equations + " equations true, " + numbers + " numbers accounted for");
            Assert.IsEmpty(t.Count, t.Report());
        }
    }
}
