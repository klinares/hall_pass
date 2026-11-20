using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace WorldTour.Core.Maths.Gen
{
    /// <summary>
    /// TOUR BUDGET: order of operations (5.OA.1-2). Ported from world_tour/math/gen/orderops.js; every RNG call
    /// happens in the same order, so problems match the JS.
    /// L1: 2 operations with parentheses. L2: 3 operations. L3: pick the expression for a 2-step story, then
    /// work it out (multiple choice with text choices). Every step along the way stays a whole number, never
    /// negative, every division exact.
    /// </summary>
    public static class OrderOps
    {
        public const string Skill = "ops";

        /// <summary>U+2192 rightwards arrow, between the rewrites in the worked hint.</summary>
        private static readonly string Arrow = ((char)0x2192).ToString();

        /// <summary>One token: a number, or one of + - x / ( ) (minus, times and divide as the JS signs).</summary>
        public readonly struct Token
        {
            public readonly double Num;
            /// <summary>The sign, or null for a number.</summary>
            public readonly string Op;

            private Token(double num, string op) { Num = num; Op = op; }

            public static Token Number(double n) => new Token(n, null);
            public static Token Sign(string op) => new Token(0, op);
            public bool IsNum => Op == null;
            public bool Is(string op) => Op == op;
            public override string ToString() => IsNum ? JsNumber.Str(Num) : Op;
        }

        /// <summary>JS <c>solve</c> result: the value and every rewrite from the start.</summary>
        public sealed class Solution
        {
            public readonly Token Value;
            public readonly List<string> States;
            public readonly List<string> Did;

            public Solution(Token value, List<string> states, List<string> did) { Value = value; States = states; Did = did; }
        }

        /// <summary>JS <c>step</c> result.</summary>
        public sealed class StepResult
        {
            public readonly List<Token> Toks;
            /// <summary>"a op b = v", or null when the step only dropped a pair of parentheses.</summary>
            public readonly string Did;

            public StepResult(List<Token> toks, string did) { Toks = toks; Did = did; }
        }

        private static double Apply(string op, double a, double b)
        {
            if (op == "+") return a + b;
            if (op == "-" || op == Glyph.Minus) return a - b;
            if (op == Glyph.Times) return a * b;
            if (op == Glyph.Divide) return a / b;
            return double.NaN; // JS: OPS[op] is undefined and the call throws; never reached here
        }

        private static readonly Regex TokenRe = new Regex("[0-9]+|[-+" + Regex.Escape(Glyph.Minus) + Regex.Escape(Glyph.Times) + Regex.Escape(Glyph.Divide) + "()]", RegexOptions.CultureInvariant);

        /// <summary>JS: <c>tokenize</c>. An ASCII "-" becomes the minus sign.</summary>
        public static List<Token> Tokenize(string s)
        {
            var r = new List<Token>();
            foreach (Match m in TokenRe.Matches(s))
            {
                string t = m.Value;
                if (t[0] >= '0' && t[0] <= '9') r.Add(Token.Number(double.Parse(t, NumberStyles.None, CultureInfo.InvariantCulture)));
                else r.Add(Token.Sign(t == "-" ? Glyph.Minus : t));
            }
            return r;
        }

        /// <summary>JS: <c>untokenize</c>: joined with spaces, no space inside parentheses.</summary>
        public static string Untokenize(IEnumerable<Token> toks) =>
            string.Join(" ", toks.Select(t => t.ToString())).Replace("( ", "(").Replace(" )", ")");

        // JS Array.prototype.slice(start, end) with negative indexes counting from the back
        private static List<Token> Slice(List<Token> a, int start, int? end = null)
        {
            int n = a.Count;
            int s = start < 0 ? Math.Max(n + start, 0) : Math.Min(start, n);
            int e = end == null ? n : end.Value < 0 ? Math.Max(n + end.Value, 0) : Math.Min(end.Value, n);
            var r = new List<Token>();
            for (int i = s; i < e; i++) r.Add(a[i]);
            return r;
        }

        private static List<Token> Concat(params IEnumerable<Token>[] parts)
        {
            var r = new List<Token>();
            foreach (IEnumerable<Token> p in parts) r.AddRange(p);
            return r;
        }

        /// <summary>
        /// JS: <c>step</c>: innermost parentheses first, then x and / left to right, then + and - left to right.
        /// Returns null if the step would leave a number that isn't whole or is negative.
        /// </summary>
        public static StepResult Step(List<Token> toks)
        {
            int lo = 0, hi = toks.Count;
            int close = toks.FindIndex(t => t.Is(")"));
            if (close >= 0)
            {
                hi = close;
                lo = toks.FindLastIndex(close, t => t.Is("(")) + 1;
            }
            List<Token> seg = Slice(toks, lo, hi);
            int i = seg.FindIndex(t => t.Is(Glyph.Times) || t.Is(Glyph.Divide));
            if (i < 0) i = seg.FindIndex(t => t.Is("+") || t.Is(Glyph.Minus));
            if (i < 0) return new StepResult(Concat(Slice(toks, 0, lo - 1), new[] { seg[0] }, Slice(toks, hi + 1)), null); // (12) -> 12
            Token a = seg[i - 1], b = seg[i + 1];
            string op = seg[i].Op;
            double v = Apply(op, a.Num, b.Num);
            if (!MathValue.IsInteger(v) || v < 0) return null;
            var vt = Token.Number(v);
            List<Token> nseg = Concat(Slice(seg, 0, i - 1), new[] { vt }, Slice(seg, i + 2));
            List<Token> output = nseg.Count == 1 && close >= 0
                ? Concat(Slice(toks, 0, lo - 1), new[] { vt }, Slice(toks, hi + 1))
                : Concat(Slice(toks, 0, lo), nseg, Slice(toks, hi));
            return new StepResult(output, a + " " + op + " " + b + " = " + JsNumber.Str(v));
        }

        /// <summary>JS: <c>solve</c>: every rewrite from the start to a single number, or null if some step breaks the rules.</summary>
        public static Solution Solve(string expr)
        {
            List<Token> toks = Tokenize(expr);
            var states = new List<string> { Untokenize(toks) };
            var did = new List<string>();
            for (int guard = 0; toks.Count > 1 && guard < 20; guard++)
            {
                StepResult s = Step(toks);
                if (s == null) return null;
                toks = s.Toks;
                if (s.Did != null)
                {
                    did.Add(s.Did);
                    states.Add(Untokenize(toks));
                }
            }
            return new Solution(toks[0], states, did);
        }

        /// <summary>JS: <c>leftToRight</c>: the classic mistake of working straight through, ignoring precedence and parentheses.</summary>
        public static double LeftToRight(string expr)
        {
            List<Token> t = Tokenize(expr).Where(x => !x.Is("(") && !x.Is(")")).ToList();
            double v = t[0].Num;
            for (int i = 1; i < t.Count; i += 2) v = Apply(t[i].Op, v, t[i + 1].Num);
            return v;
        }

        /// <summary>JS: <c>noParens</c>: the value with the parentheses ignored, or null if that breaks the rules.</summary>
        public static double? NoParens(string expr)
        {
            Solution s = Solve(expr.Replace("(", "").Replace(")", ""));
            return s == null ? (double?)null : s.Value.Num;
        }

        private static string M => Glyph.Minus;
        private static string X => Glyph.Times;
        private static string D => Glyph.Divide;

        private static readonly string[] L1 =
        {
            "(a + b) " + X + " c", "a " + X + " (b + c)", "(a " + M + " b) " + X + " c", "a " + X + " (b " + M + " c)",
            "(a + b) " + D + " c", "a " + M + " (b + c)", "(a " + M + " b) " + D + " c"
        };

        private static readonly string[] L2 =
        {
            "a + b " + X + " (c " + M + " d)", "(a + b) " + X + " c " + M + " d", "a " + X + " b " + M + " c " + X + " d",
            "(a " + M + " b) " + X + " (c + d)", "a " + D + " b + c " + X + " d", "a " + X + " (b + c) " + M + " d", "a + (b + c) " + D + " d"
        };

        private struct Filled { public string Expr; public Solution S; }

        private static Filled? Fill(Rng r, string tpl, int[][] ranges)
        {
            for (int tries = 0; tries < 400; tries++)
            {
                int a = r.Int(ranges[0][0], ranges[0][1]), b = r.Int(ranges[1][0], ranges[1][1]);
                int c = r.Int(ranges[2][0], ranges[2][1]), d = r.Int(ranges[3][0], ranges[3][1]);
                var sb = new StringBuilder();
                foreach (char ch in tpl)
                {
                    if (ch == 'a') sb.Append(I(a));
                    else if (ch == 'b') sb.Append(I(b));
                    else if (ch == 'c') sb.Append(I(c));
                    else if (ch == 'd') sb.Append(I(d));
                    else sb.Append(ch);
                }
                string expr = sb.ToString();
                Solution s = Solve(expr);
                if (s == null || s.Value.Num > 300 || s.Value.Num == 0) continue;
                if (tpl.Contains("(") && NoParens(expr) == s.Value.Num) continue; // the parentheses have to matter
                return new Filled { Expr = expr, S = s };
            }
            return null;
        }

        private sealed class Story
        {
            public string Text, Right;
            public string[][] Wrong; // [expression, why]
        }

        private static string I(int n) => n.ToString(CultureInfo.InvariantCulture);

        private static readonly Func<Rng, Story>[] Stories =
        {
            r =>
            {
                int n = r.Int(3, 9), a = r.Int(2, 9), b = r.Int(4, 15);
                return new Story
                {
                    Text = I(n) + " fans each buy a $" + I(a) + " pin and a $" + I(b) + " poster. how much do they spend in all?",
                    Right = I(n) + " " + X + " (" + I(a) + " + " + I(b) + ")",
                    Wrong = new[]
                    {
                        new[] { I(n) + " " + X + " " + I(a) + " + " + I(b), "missing-parentheses" },
                        new[] { I(n) + " + " + I(a) + " + " + I(b), "added-all" },
                        new[] { "(" + I(n) + " + " + I(a) + ") " + X + " " + I(b), "wrong-grouping" }
                    }
                };
            },
            r =>
            {
                int n = r.Int(3, 8), a = r.Int(4, 12), m = n * a + r.Int(5, 60);
                return new Story
                {
                    Text = "the band has $" + I(m) + ". they buy " + I(n) + " sets of strings at $" + I(a) + " a set. how much is left?",
                    Right = I(m) + " " + M + " " + I(n) + " " + X + " " + I(a),
                    Wrong = new[]
                    {
                        new[] { "(" + I(m) + " " + M + " " + I(n) + ") " + X + " " + I(a), "wrong-grouping" },
                        new[] { I(m) + " " + M + " " + I(n) + " " + M + " " + I(a), "added-all" },
                        new[] { I(m) + " " + M + " " + I(n) + " + " + I(a), "wrong-op" }
                    }
                };
            },
            r =>
            {
                int n = r.Int(3, 9), a = r.Int(6, 24), b = r.Int(2, 11);
                return new Story
                {
                    Text = "the crew unpacks " + I(n) + " boxes with " + I(a) + " shirts each, plus " + I(b) + " loose shirts. how many shirts?",
                    Right = I(n) + " " + X + " " + I(a) + " + " + I(b),
                    Wrong = new[]
                    {
                        new[] { I(n) + " " + X + " (" + I(a) + " + " + I(b) + ")", "extra-parentheses" },
                        new[] { I(n) + " + " + I(a) + " + " + I(b), "added-all" },
                        new[] { "(" + I(n) + " + " + I(b) + ") " + X + " " + I(a), "wrong-grouping" }
                    }
                };
            },
            r =>
            {
                int k = r.Int(2, 6), tot = k * r.Int(6, 30);
                int a = r.Int((int)Math.Ceiling(tot / 4.0), (int)Math.Floor(tot * 3 / 4.0)), b = tot - a;
                return new Story
                {
                    Text = "gas costs $" + I(a) + " and food costs $" + I(b) + ". the " + I(k) + " band members split it evenly. how much does each pay?",
                    Right = "(" + I(a) + " + " + I(b) + ") " + D + " " + I(k),
                    Wrong = new[]
                    {
                        new[] { I(a) + " + " + I(b) + " " + D + " " + I(k), "missing-parentheses" },
                        new[] { I(a) + " " + D + " " + I(k) + " + " + I(b), "wrong-grouping" },
                        new[] { "(" + I(a) + " + " + I(b) + ") " + X + " " + I(k), "wrong-op" }
                    }
                };
            },
            r =>
            {
                int n = r.Int(5, 9), a = r.Int(8, 20), b = r.Int(2, n - 1);
                return new Story
                {
                    Text = "the venue has " + I(n) + " rows of " + I(a) + " seats, but " + I(b) + " seats are broken. how many seats can fans use?",
                    Right = I(n) + " " + X + " " + I(a) + " " + M + " " + I(b),
                    Wrong = new[]
                    {
                        new[] { I(n) + " " + X + " (" + I(a) + " " + M + " " + I(b) + ")", "extra-parentheses" },
                        new[] { I(n) + " + " + I(a) + " " + M + " " + I(b), "wrong-op" },
                        new[] { "(" + I(n) + " " + M + " " + I(b) + ") " + X + " " + I(a), "wrong-grouping" }
                    }
                };
            },
            r =>
            {
                int k = r.Int(2, 6), q = r.Int(3, 12), m = k * q, b = r.Int(1, Math.Min(9, q - 1));
                return new Story
                {
                    Text = I(k) + " fans share " + I(m) + " stickers equally. each fan already had " + I(b) + ". how many stickers does each fan have now?",
                    Right = I(m) + " " + D + " " + I(k) + " + " + I(b),
                    Wrong = new[]
                    {
                        new[] { I(m) + " " + D + " (" + I(k) + " + " + I(b) + ")", "extra-parentheses" },
                        new[] { "(" + I(m) + " + " + I(b) + ") " + D + " " + I(k), "wrong-grouping" },
                        new[] { I(m) + " " + D + " " + I(k) + " " + M + " " + I(b), "wrong-op" }
                    }
                };
            }
        };

        // JS: s.states.join(' -> ').replace(/-> ([^->]+)$/, '-> <b>$1</b>'): the last state in bold
        private static string Worked(List<string> states)
        {
            string joined = string.Join(" " + Arrow + " ", states);
            var re = new Regex(Regex.Escape(Arrow) + " ([^" + Regex.Escape(Arrow) + "]+)\\z", RegexOptions.CultureInvariant);
            return re.Replace(joined, Arrow + " <b>$1</b>", 1);
        }

        private static List<Trap> Traps(Answer answer, string expr)
        {
            double? np = NoParens(expr);
            return Mistakes.CleanTraps(answer, new[]
            {
                new Trap(MathValue.Num(LeftToRight(expr)), "left-to-right"),
                new Trap(np.HasValue ? MathValue.Num(np.Value) : MathValue.Null, "ignored-parentheses")
            });
        }

        public static Problem Generate(int level, long seed) => Guard.Guarded(Build, level, seed);

        private static Problem Build(int level, long seed, Rng r)
        {
            if (level == 3) return BuildStory(r, seed);
            string tpl = r.Pick(level == 1 ? L1 : L2);
            int[][] ranges = level == 1
                ? new[] { new[] { 2, 15 }, new[] { 2, 12 }, new[] { 2, 9 }, new[] { 2, 9 } }
                : new[] { new[] { 2, 20 }, new[] { 2, 12 }, new[] { 2, 12 }, new[] { 2, 9 } };
            Filled? got = Fill(r, tpl, ranges);
            if (got == null)
                got = Fill(r, level == 1 ? "(a + b) " + X + " c" : "(a + b) " + X + " c " + M + " d",
                    new[] { new[] { 2, 9 }, new[] { 2, 9 }, new[] { 2, 5 }, new[] { 1, 5 } });
            if (got == null) throw new InvalidOperationException("no expression found"); // the JS would throw here too
            string expr = got.Value.Expr;
            Solution s = got.Value.S;
            Answer answer = Answer.Int(checked((long)s.Value.Num));
            return new Problem
            {
                Skill = Skill, Level = level, Kind = "eval", Seed = seed,
                Params = JsonValue.Obj().Set("expr", expr).Set("states", JsonValue.Arr(s.States.Select(JsonValue.Str))),
                Prompt = "the budget sheet says <b class=\"expr\">" + expr + "</b>. what does it come to?",
                Label = expr, Answer = answer, Simplest = false, Choices = null,
                Traps = Traps(answer, expr),
                Steps = new List<string>
                {
                    "parentheses first, then " + X + " and " + D + ", then + and " + M + ".",
                    "first: <b>" + s.Did[0] + "</b>. that leaves " + s.States[1] + ".",
                    Worked(s.States)
                }
            };
        }

        /// <summary>
        /// The most a tour budget story may show, in whole dollars (BOUNDS.storyDollars in math_config.json; QUESTIONS 9.4).
        /// Kept apart from moneyCents, which means decimal money work.
        /// </summary>
        public const int MaxStoryDollars = 150;

        private static readonly Regex StoryDollars = new Regex(@"\$([0-9]+)", RegexOptions.CultureInvariant);

        /// <summary>Every "$n" in the story text is at most <see cref="MaxStoryDollars"/>.</summary>
        public static bool StoryDollarsOk(string text)
        {
            foreach (Match m in StoryDollars.Matches(text ?? ""))
                if (m.Groups[1].Value.Length > 6 || int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) > MaxStoryDollars) return false;
            return true;
        }

        private static Problem BuildStory(Rng r, long seed)
        {
            Story st = null;
            Solution s = null;
            for (int i = 0; i < 50; i++)
            {
                st = r.Pick(Stories)(r);
                s = Solve(st.Right);
                Solution sol = s;
                // Unity (QUESTIONS 9.4, an intended difference from the JS): a story never shows more than
                // MaxStoryDollars. Only the strings story can pass it ($156 at most); about 1 in 3,200 seeds pick again.
                if (sol != null && StoryDollarsOk(st.Text) && st.Wrong.All(w => { Solution ws = Solve(w[0]); return ws == null || ws.Value.Num != sol.Value.Num; })) break;
            }
            if (s == null) throw new InvalidOperationException("no story found"); // the JS would throw here too
            Answer answer = Answer.Int(checked((long)s.Value.Num));
            var options = new List<Choice> { new Choice(st.Right, true, null) };
            foreach (string[] w in st.Wrong) options.Add(new Choice(w[0], false, w[1]));
            return new Problem
            {
                Skill = Skill, Level = 3, Kind = "story", Seed = seed,
                Params = JsonValue.Obj().Set("expr", st.Right).Set("states", JsonValue.Arr(s.States.Select(JsonValue.Str))),
                Prompt = st.Text, Label = st.Right, Answer = answer, Simplest = false,
                Choices = r.Shuffle(options),
                Traps = Traps(answer, st.Right),
                Steps = new List<string>
                {
                    "first pick the expression that matches the story, then work it out: parentheses, then " + X + " and " + D + ", then + and " + M + ".",
                    "it" + Glyph.Apos + "s <b>" + st.Right + "</b>. first: " + s.Did[0] + ".",
                    Worked(s.States)
                }
            };
        }
    }
}
