using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace WorldTour.Core.Maths
{
    /// <summary>JS <c>answer.type</c>.</summary>
    public enum AnswerType { Money, Int, Decimal, Fraction, Point }

    /// <summary>
    /// JS <c>{ type, value }</c>. Money and Int hold a number (cents, or the whole number);
    /// Decimal and Fraction hold <c>{ n, d }</c>; Point holds <c>{ x, y }</c>.
    /// </summary>
    public sealed class Answer
    {
        public readonly AnswerType Type;
        public readonly MathValue Value;

        public Answer(AnswerType type, MathValue value)
        {
            Type = type;
            Value = value;
        }

        public static Answer Money(long cents) => new Answer(AnswerType.Money, MathValue.Num(cents));
        public static Answer Int(long value) => new Answer(AnswerType.Int, MathValue.Num(value));
        public static Answer Decimal(Rational value) => new Answer(AnswerType.Decimal, MathValue.Frac(value));
        public static Answer Fraction(Rational value) => new Answer(AnswerType.Fraction, MathValue.Frac(value));
        public static Answer Point(double x, double y) => new Answer(AnswerType.Point, MathValue.Pt(x, y));

        /// <summary>JS type names: "money", "int", "decimal", "fraction", "point".</summary>
        public static string TypeName(AnswerType t)
        {
            switch (t)
            {
                case AnswerType.Money: return "money";
                case AnswerType.Int: return "int";
                case AnswerType.Decimal: return "decimal";
                case AnswerType.Fraction: return "fraction";
                default: return "point";
            }
        }
    }

    /// <summary>
    /// JS <c>check</c> result <c>{ ok, simplify, empty, bad }</c>. A key the JS leaves out reads as false here;
    /// the web game only ever tests these for truth.
    /// </summary>
    public readonly struct CheckResult : IEquatable<CheckResult>
    {
        public readonly bool Ok;
        public readonly bool Simplify;
        public readonly bool Empty;
        public readonly bool Bad;

        public CheckResult(bool ok, bool simplify, bool empty = false, bool bad = false)
        {
            Ok = ok;
            Simplify = simplify;
            Empty = empty;
            Bad = bad;
        }

        public bool Equals(CheckResult o) => Ok == o.Ok && Simplify == o.Simplify && Empty == o.Empty && Bad == o.Bad;
        public override bool Equals(object obj) => obj is CheckResult c && Equals(c);
        public override int GetHashCode() => (Ok ? 1 : 0) | (Simplify ? 2 : 0) | (Empty ? 4 : 0) | (Bad ? 8 : 0);
        public override string ToString() => "ok=" + Ok + " simplify=" + Simplify + " empty=" + Empty + " bad=" + Bad;
    }

    /// <summary>JS <c>parseNumber</c> result <c>{ v, simplest, form }</c>. Form is "int", "decimal", "fraction" or "mixed".</summary>
    public sealed class ParsedNumber
    {
        public readonly Rational V;
        /// <summary>Worked out from the numerator and denominator as typed, before reducing.</summary>
        public readonly bool Simplest;
        public readonly string Form;

        public ParsedNumber(Rational v, bool simplest, string form)
        {
            V = v;
            Simplest = simplest;
            Form = form;
        }
    }

    /// <summary>
    /// Parse and compare typed answers, ported from world_tour/math/answer.js.
    /// 0.5 = .5 = .50; $3.50 = 3.5 for money; 1 1/2 = 3/2 = 1.5 for fractions;
    /// 2/4 = 1/2 unless simplest form is asked for, in which case 2/4 is "right amount, now simplify".
    /// Regexes use JS's meaning of \s (its whitespace set) and \d (ASCII 0-9 only).
    /// </summary>
    public static class Answers
    {
        // JS \s and String.prototype.trim: WhiteSpace plus LineTerminator.
        private const string Ws = @"[\t\n\v\f\r \u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000\ufeff]";
        private const RegexOptions Opt = RegexOptions.CultureInvariant;

        private static readonly Regex DollarPrefix = new Regex(@"\A\$" + Ws + "*", Opt);
        private static readonly Regex WsRun = new Regex(Ws + "+", Opt);
        private static readonly Regex MixedRe = new Regex(@"\A([0-9]+) ([0-9]+)/([0-9]+)\z", Opt);
        private static readonly Regex FractionRe = new Regex(@"\A([0-9]+)/([0-9]+)\z", Opt);
        private static readonly Regex DecimalRe = new Regex(@"\A([0-9]*)\.([0-9]+)\z", Opt);
        private static readonly Regex TrailingPointRe = new Regex(@"\A([0-9]+)\.\z", Opt);
        private static readonly Regex IntRe = new Regex(@"\A[0-9]+\z", Opt);
        // QUESTIONS 7: 1 to 3 digits, then groups of exactly 3, not inside a longer run of digits, commas or decimals.
        private static readonly Regex Grouped = new Regex(@"(?<![0-9.,])[0-9]{1,3}(?:,[0-9]{3})+(?![0-9,])", Opt);
        private static readonly Regex SlashSpaces = new Regex(@" ?/ ?", Opt);
        private static readonly Regex PointRe = new Regex(@"\A" + Ws + @"*\(?" + Ws + "*([0-9]+)" + Ws + "*," + Ws + "*([0-9]+)" + Ws + @"*\)?" + Ws + @"*\z", Opt);

        /// <summary>JS <c>String.prototype.trim</c>.</summary>
        public static string JsTrim(string s)
        {
            int start = 0, end = s.Length;
            while (start < end && IsJsSpace(s[start])) start++;
            while (end > start && IsJsSpace(s[end - 1])) end--;
            return s.Substring(start, end - start);
        }

        private static bool IsJsSpace(char c)
        {
            switch (c)
            {
                case '\t': case '\n': case '\v': case '\f': case '\r': case ' ':
                case '\u00a0': case '\u1680': case '\u2028': case '\u2029': case '\u202f':
                case '\u205f': case '\u3000': case '\ufeff':
                    return true;
                default:
                    return c >= '\u2000' && c <= '\u200a';
            }
        }

        private static bool TryDigits(string digits, out long value) =>
            long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out value);

        /// <summary>
        /// JS <c>parseNumber</c>: text to <c>{ v, simplest, form }</c>, or null if it doesn't read as a number.
        /// Takes off one leading "$", squeezes whitespace runs to one space.
        /// Accepts "2", "1/2", "1 1/2", "0.5", ".5", "5." only: no signs.
        /// Intended differences from the JS (QUESTIONS 7): a comma counts only as thousands grouping ("1,000",
        /// "12,500.5"); any other comma ("3,50", "1,0,0,0", ",5") makes the text unreadable (null), where the JS
        /// removed every comma. Spaces around "/" are accepted ("3 / 4", "1 3 / 4"), where the JS gave null.
        /// Unlike the JS, a number too big for a 64-bit integer gives null (the JS loses precision past 2^53).
        /// </summary>
        public static ParsedNumber ParseNumber(string text)
        {
            if (text == null) return null;
            string s = JsTrim(text);
            s = DollarPrefix.Replace(s, "", 1);
            if (s.IndexOf(',') >= 0)
            {
                s = Grouped.Replace(s, g => g.Value.Replace(",", ""));
                if (s.IndexOf(',') >= 0) return null;   // a comma that isn't thousands grouping: unreadable, never a guess
            }
            s = WsRun.Replace(s, " ");
            s = SlashSpaces.Replace(s, "/");
            try
            {
                Match m;
                if ((m = MixedRe.Match(s)).Success)
                {
                    if (!TryDigits(m.Groups[1].Value, out long w) || !TryDigits(m.Groups[2].Value, out long n) || !TryDigits(m.Groups[3].Value, out long d)) return null;
                    if (d == 0) return null;
                    return new ParsedNumber(Rational.R(checked(w * d + n), d), n < d && Rational.Gcd(n, d) == 1, "mixed");
                }
                if ((m = FractionRe.Match(s)).Success)
                {
                    if (!TryDigits(m.Groups[1].Value, out long n) || !TryDigits(m.Groups[2].Value, out long d)) return null;
                    if (d == 0) return null;
                    return new ParsedNumber(Rational.R(n, d), Rational.IsSimplest(n, d), "fraction");
                }
                if ((m = DecimalRe.Match(s)).Success || (m = TrailingPointRe.Match(s)).Success)
                {
                    string whole = m.Groups[1].Value.Length > 0 ? m.Groups[1].Value : "0";
                    string frac = m.Groups.Count > 2 ? m.Groups[2].Value : "";
                    if (frac.Length > 18 || !TryDigits(whole + frac, out long n)) return null;
                    long d = 1;
                    for (int i = 0; i < frac.Length; i++) d *= 10;
                    return new ParsedNumber(Rational.R(n, d), true, "decimal");
                }
                if (IntRe.IsMatch(s))
                {
                    if (!TryDigits(s, out long n)) return null;
                    return new ParsedNumber(Rational.R(n), true, "int");
                }
                return null;
            }
            catch (OverflowException)
            {
                return null;
            }
        }

        /// <summary>JS <c>valueOf</c>: the answer's value as a fraction. Throws for a point.</summary>
        public static Rational ValueOf(Answer answer)
        {
            switch (answer.Type)
            {
                case AnswerType.Money: return MathValue.ToRational(answer.Value.Number, 100);
                case AnswerType.Int: return MathValue.ToRational(answer.Value.Number, 1);
                case AnswerType.Decimal:
                case AnswerType.Fraction: return MathValue.ToRational(answer.Value.N, answer.Value.D);
                default: throw new InvalidOperationException("no numeric value for " + Answer.TypeName(answer.Type));
            }
        }

        private static bool SameValue(Rational a, Rational b)
        {
            try { return Rational.Eq(a, b); }
            catch (OverflowException) { return false; }
        }

        /// <summary>JS <c>check(text, answer, accept)</c> with typed text. <paramref name="simplest"/> is <c>accept.simplest</c>.</summary>
        public static CheckResult Check(string text, Answer answer, bool simplest = false)
        {
            if (answer.Type == AnswerType.Point)
            {
                MathValue p = ParsePoint(text);
                bool has = p.Kind != ValueKind.Null;
                // QUESTIONS 7.3 (an intended difference): typed text that isn't a point is "bad" (doesn't read yet),
                // not "empty"; the JS said "type your answer on the keys first" although she typed something.
                bool blank = JsTrim(text ?? "") == "";
                return new CheckResult(has && p.X == answer.Value.X && p.Y == answer.Value.Y, false, !has && blank, !has && !blank);
            }
            if (JsTrim(text ?? "") == "") return new CheckResult(false, false, empty: true);
            ParsedNumber parsed = ParseNumber(text);
            if (parsed == null) return new CheckResult(false, false, bad: true);
            bool same = SameValue(parsed.V, ValueOf(answer));
            if (same && simplest && !parsed.Simplest) return new CheckResult(false, true);
            return new CheckResult(same, false);
        }

        /// <summary>
        /// JS <c>check(object, answer, accept)</c>: a picked value rather than typed text (the stage designer's
        /// clicked point). MathValue.Null behaves like JS null.
        /// </summary>
        public static CheckResult Check(MathValue picked, Answer answer, bool simplest = false)
        {
            if (answer.Type == AnswerType.Point)
            {
                // typeof number is not 'object', so the JS reads a number as text, which never parses as a point.
                MathValue p = picked.Kind == ValueKind.Number ? ParsePoint(JsNumber.Str(picked.Number)) : picked;
                bool has = p.Kind != ValueKind.Null;
                return new CheckResult(has && p.X == answer.Value.X && p.Y == answer.Value.Y, false, !has);
            }
            // String(null ?? '') is empty; String(object) is "[object Object]", which doesn't parse.
            if (picked.Kind == ValueKind.Null) return new CheckResult(false, false, empty: true);
            if (picked.IsObject) return new CheckResult(false, false, bad: true);
            return Check(JsNumber.Str(picked.Number), answer, simplest);
        }

        /// <summary>
        /// The nudge for typed input that isn't an answer yet, or null when the check is a right answer or a real
        /// miss. Empty, unreadable and "now simplify" input is never a miss: it costs no fans, no try and no streak,
        /// and only asks her to retype. Unreadable point text shows the expected shape (QUESTIONS 7.3); the example
        /// (4, 4) is on the diagonal, which the stage designer never asks for, so it can't give an answer away.
        /// </summary>
        public static string Nudge(CheckResult r, AnswerType type)
        {
            if (r.Bad) return type == AnswerType.Point
                ? "that doesn" + Apos + "t read as a number yet. type it like (4, 4): across, then up."
                : "that doesn" + Apos + "t read as a number yet. try again.";
            if (r.Empty) return "type your answer on the keys first.";
            if (r.Simplify) return "right amount! now simplify it: divide the top and bottom by the same number.";
            return null;
        }

        /// <summary>True when the check only asks her to retype (empty, unreadable, or simplify): never a miss.</summary>
        public static bool IsRetype(CheckResult r) => r.Empty || r.Bad || r.Simplify;

        private static readonly string Apos = ((char)0x2019).ToString();

        /// <summary>JS <c>parsePoint</c>: "(3, 5)", "3,5", "(3,5" all read as (3, 5). Null kind when it doesn't match.</summary>
        public static MathValue ParsePoint(string text)
        {
            Match m = PointRe.Match(text ?? "");
            if (!m.Success) return MathValue.Null;
            return MathValue.Pt(double.Parse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture),
                                double.Parse(m.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture));
        }

        /// <summary>JS <c>matches(text, value, type)</c>: does a typed answer equal some other value of this answer type (used to spot a known mistake).</summary>
        public static bool Matches(string text, MathValue value, AnswerType type)
        {
            if (type == AnswerType.Point)
            {
                MathValue p = ParsePoint(text);
                return p.Kind != ValueKind.Null && p.X == value.X && p.Y == value.Y;
            }
            ParsedNumber parsed = ParseNumber(text);
            return parsed != null && SameValue(parsed.V, ValueOf(new Answer(type, value)));
        }

        /// <summary>JS <c>matches(object, value, type)</c> with a picked value.</summary>
        public static bool Matches(MathValue picked, MathValue value, AnswerType type)
        {
            if (type == AnswerType.Point)
                return picked.Kind != ValueKind.Null && picked.X == value.X && picked.Y == value.Y;
            if (picked.Kind != ValueKind.Number) return false;
            return Matches(JsNumber.Str(picked.Number), value, type);
        }

        /// <summary>
        /// JS <c>show</c>: how the game writes an answer. Money "$12.05"; whole numbers with en-US thousands
        /// separators ("1,000", never the machine's culture); decimals "0.65"; fractions as "[[1 1/2]]" markup,
        /// which the UI stacks; points "(3, 5)".
        /// </summary>
        public static string Show(Answer answer)
        {
            MathValue v = answer.Value;
            switch (answer.Type)
            {
                case AnswerType.Money: return DollarsJs(v.Number);
                case AnswerType.Int: return EnUs(v.Number);
                case AnswerType.Decimal: return Rational.Dec(MathValue.ToRational(v.N, v.D));
                case AnswerType.Fraction: return "[[" + Rational.Str(MathValue.ToRational(v.N, v.D)) + "]]";
                default: return "(" + JsNumber.Str(v.X) + ", " + JsNumber.Str(v.Y) + ")";
            }
        }

        /// <summary>JS <c>isSimplestForm(f) = isSimplest(f)</c>, on the numerator and denominator as given.</summary>
        public static bool IsSimplestForm(long n, long d) => Rational.IsSimplest(n, d);

        // Number.prototype.toLocaleString('en-US'): grouping by 3 with ",", at most 3 decimals.
        // The invariant culture has exactly these separators, and it exists even where en-US data doesn't.
        private static string EnUs(double x) => x.ToString("#,##0.###", CultureInfo.InvariantCulture);

        private static string DollarsJs(double c)
        {
            if (MathValue.IsInteger(c) && Math.Abs(c) < 9007199254740992.0) return Money.Dollars((long)c);
            return "$" + JsNumber.Str(Math.Floor(c / 100)) + "." + JsNumber.Str(c % 100).PadLeft(2, '0');
        }
    }
}
