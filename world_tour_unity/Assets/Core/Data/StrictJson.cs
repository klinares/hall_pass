using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Core.Data
{
    /// <summary>
    /// Malformed JSON: one readable message and where it was found. <see cref="Exception.Message"/> is
    /// "line 3, column 5: ..." (or just the reason for an empty file); <see cref="Reason"/> is the bare reason.
    /// </summary>
    public sealed class StrictJsonException : FormatException
    {
        /// <summary>1-based; 0 when the problem has no place (an empty file).</summary>
        public int Line { get; }
        public int Column { get; }
        public string Reason { get; }

        public StrictJsonException(int line, int column, string reason)
            : base(line > 0 ? "line " + line + ", column " + column + ": " + reason : reason)
        {
            Line = line;
            Column = column;
            Reason = reason;
        }
    }

    /// <summary>
    /// A small strict JSON reader (RFC 8259: no comments, no trailing commas, double quotes only) with no dependencies,
    /// so Core can check a hand-edited data file before trusting it. It returns the shared <see cref="JsonValue"/> tree
    /// (object key order kept; every value carries its Line and Column, numbers their RawNumber token, and objects their
    /// key positions) and stops at the first syntax problem with a plain-words message.
    /// A number's value is double.Parse of its token, which is what JS JSON.parse gives.
    /// A leading byte-order mark is skipped. A repeated key in one object is a syntax problem.
    /// Mirrored line for line by the reader in Tools/validate_data.mjs; the parity test keeps the messages identical.
    /// </summary>
    public static class StrictJson
    {
        /// <summary>
        /// The deepest nesting of { } and [ ] accepted. The parser recurses once per level, and a stack overflow can't be
        /// caught, so deeper text (a crafted import code) is refused with a StrictJsonException instead. Real files nest under 10.
        /// </summary>
        public const int MaxDepth = 64;

        private static readonly Regex NumberShape = new Regex("^-?(0|[1-9][0-9]*)(\\.[0-9]+)?([eE][+-]?[0-9]+)?$", RegexOptions.CultureInvariant);

        /// <summary>Parses the whole text. Throws <see cref="StrictJsonException"/> when it is malformed or empty.</summary>
        public static JsonValue Parse(string text) => new Reader(text ?? "").Document();

        /// <summary>Like <see cref="Parse"/>, but returns false with the problem instead of throwing.</summary>
        public static bool TryParse(string text, out JsonValue value, out StrictJsonException error)
        {
            try
            {
                value = Parse(text);
                error = null;
                return true;
            }
            catch (StrictJsonException e)
            {
                value = null;
                error = e;
                return false;
            }
        }

        private struct Opener { public char C; public int Line, Column; }

        private sealed class Reader
        {
            private readonly string s;
            private int i, line = 1, lineStart;
            private readonly List<Opener> open = new List<Opener>();

            public Reader(string text)
            {
                s = text;
                if (s.Length > 0 && s[0] == (char)0xFEFF) { i = 1; lineStart = 1; }
            }

            private int Col(int at) => at - lineStart + 1;
            private StrictJsonException At(int at, string msg) => new StrictJsonException(line, Col(at), msg);

            private static JsonValue Placed(JsonValue v, int line, int column)
            {
                v.Line = line;
                v.Column = column;
                return v;
            }

            private void SkipWs()
            {
                while (i < s.Length)
                {
                    char c = s[i];
                    if (c == '\n') { i++; line++; lineStart = i; }
                    else if (c == ' ' || c == '\t' || c == '\r') i++;
                    else break;
                }
            }

            private StrictJsonException Eof()
            {
                if (open.Count == 0) return new StrictJsonException(0, 0, "the file is empty");
                Opener o = open[open.Count - 1];
                return new StrictJsonException(o.Line, o.Column, "this " + o.C + " is never closed");
            }

            public JsonValue Document()
            {
                SkipWs();
                if (i >= s.Length) throw Eof();
                JsonValue root = Value();
                SkipWs();
                if (i < s.Length) throw At(i, "there is extra text after the end of the data");
                return root;
            }

            private JsonValue Value()
            {
                SkipWs();
                if (i >= s.Length) throw Eof();
                char c = s[i];
                if (c == '{') return Obj();
                if (c == '[') return Arr();
                if (c == '"') { int l = line, cl = Col(i); return Placed(JsonValue.Str(Str()), l, cl); }
                if (c == '-' || (c >= '0' && c <= '9')) return Num();
                if (IsWordChar(c))
                {
                    int at = i;
                    string word = Word();
                    if (word == "true" || word == "false") return Placed(JsonValue.Bool(word == "true"), line, Col(at));
                    if (word == "null") return Placed(JsonValue.NewNull(), line, Col(at));
                    throw At(at, "\"" + word + "\" is not a value: text needs double quotes, and true, false and null are lowercase");
                }
                if (c == '\'') throw At(i, "use double quotes (\") around text, not single quotes");
                if (c == '/') throw At(i, "JSON files can't have comments");
                throw At(i, "expected a value here, but found \"" + c + "\"");
            }

            private static bool IsWordChar(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';

            private string Word()
            {
                int start = i;
                while (i < s.Length && IsWordChar(s[i])) i++;
                return s.Substring(start, i - start);
            }

            private JsonValue Obj()
            {
                JsonValue node = Placed(JsonValue.Obj(), line, Col(i));
                if (open.Count >= MaxDepth) throw At(i, "the data is too deeply nested (more than " + MaxDepth + " levels of { } and [ ])");
                open.Add(new Opener { C = '{', Line = line, Column = Col(i) });
                i++;
                SkipWs();
                if (i >= s.Length) throw Eof();
                if (s[i] == '}') { i++; open.RemoveAt(open.Count - 1); return node; }
                int commaLine = 0, commaCol = 0;
                while (true)
                {
                    SkipWs();
                    if (i >= s.Length) throw Eof();
                    char c = s[i];
                    if (c != '"')
                    {
                        if (c == '}' && commaLine > 0) throw new StrictJsonException(commaLine, commaCol, "there is an extra comma here, before }");
                        if (c == '\'') throw At(i, "use double quotes (\") around text, not single quotes");
                        if (IsWordChar(c)) { int at = i; throw At(at, "field names need double quotes, like \"" + Word() + "\""); }
                        if (c == '/') throw At(i, "JSON files can't have comments");
                        throw At(i, "expected a field name in double quotes, but found \"" + c + "\"");
                    }
                    int keyLine = line, keyCol = Col(i);
                    string key = Str();
                    if (node.Has(key)) throw new StrictJsonException(keyLine, keyCol, "\"" + key + "\" appears twice in the same { }");
                    SkipWs();
                    if (i >= s.Length) throw Eof();
                    if (s[i] != ':') throw At(i, "expected : after \"" + key + "\"");
                    i++;
                    JsonValue v = Value();
                    node.AddParsed(key, v, keyLine, keyCol);
                    SkipWs();
                    if (i >= s.Length) throw Eof();
                    c = s[i];
                    if (c == ',') { commaLine = line; commaCol = Col(i); i++; continue; }
                    if (c == '}') { i++; open.RemoveAt(open.Count - 1); return node; }
                    if (c == '"') throw At(i, "a comma is missing before this field");
                    if (c == ']') throw At(i, "found ] where } was expected; the { on line " + node.Line + " is not closed");
                    if (IsWordChar(c)) throw At(i, "expected , or } here, but found \"" + c + "\"; check the quotes just before it");
                    throw At(i, "expected , or } here, but found \"" + c + "\"");
                }
            }

            private JsonValue Arr()
            {
                int l = line, cl = Col(i);
                var items = new List<JsonValue>();
                if (open.Count >= MaxDepth) throw At(i, "the data is too deeply nested (more than " + MaxDepth + " levels of { } and [ ])");
                open.Add(new Opener { C = '[', Line = l, Column = cl });
                i++;
                SkipWs();
                if (i >= s.Length) throw Eof();
                if (s[i] == ']') { i++; open.RemoveAt(open.Count - 1); return Placed(JsonValue.Arr(items), l, cl); }
                int commaLine = 0, commaCol = 0;
                while (true)
                {
                    SkipWs();
                    if (i >= s.Length) throw Eof();
                    if (s[i] == ']' && commaLine > 0) throw new StrictJsonException(commaLine, commaCol, "there is an extra comma here, before ]");
                    items.Add(Value());
                    SkipWs();
                    if (i >= s.Length) throw Eof();
                    char c = s[i];
                    if (c == ',') { commaLine = line; commaCol = Col(i); i++; continue; }
                    if (c == ']') { i++; open.RemoveAt(open.Count - 1); return Placed(JsonValue.Arr(items), l, cl); }
                    if (c == '{' || c == '[' || c == '"' || c == '-' || IsWordChar(c)) throw At(i, "a comma is missing before this item");
                    if (c == '}') throw At(i, "found } where ] was expected; the [ on line " + l + " is not closed");
                    throw At(i, "expected , or ] here, but found \"" + c + "\"");
                }
            }

            private string Str()
            {
                int quote = i;
                i++;
                var sb = new StringBuilder();
                while (true)
                {
                    if (i >= s.Length || s[i] == '\n' || s[i] == '\r') throw At(quote, "this text has no closing quote");
                    char c = s[i];
                    if (c == '"') { i++; return sb.ToString(); }
                    if (c < ' ') throw At(i, "a tab or other control character can't go inside quotes");
                    if (c != '\\') { sb.Append(c); i++; continue; }
                    if (i + 1 >= s.Length) throw At(quote, "this text has no closing quote");
                    char e = s[i + 1];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            int code = 0;
                            for (int k = 0; k < 4; k++)
                            {
                                int d = i + 2 + k < s.Length ? HexDigit(s[i + 2 + k]) : -1;
                                if (d < 0) throw At(i, "a backslash-u code inside quotes needs 4 hex digits");
                                code = code * 16 + d;
                            }
                            sb.Append((char)code);
                            i += 4;
                            break;
                        default:
                            throw At(i, "\"\\" + e + "\" is not allowed inside quotes; to write a backslash, type two of them");
                    }
                    i += 2;
                }
            }

            private static int HexDigit(char c)
            {
                if (c >= '0' && c <= '9') return c - '0';
                if (c >= 'a' && c <= 'f') return c - 'a' + 10;
                if (c >= 'A' && c <= 'F') return c - 'A' + 10;
                return -1;
            }

            private JsonValue Num()
            {
                int start = i;
                while (i < s.Length && ((s[i] >= '0' && s[i] <= '9') || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E')) i++;
                string raw = s.Substring(start, i - start);
                if (!NumberShape.IsMatch(raw)) throw At(start, "\"" + raw + "\" is not a valid number");
                double v;
                try { v = double.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture); }
                catch (OverflowException) { v = raw[0] == '-' ? double.NegativeInfinity : double.PositiveInfinity; }   // JS gives Infinity too
                JsonValue n = Placed(JsonValue.Num(v), line, Col(start));
                n.RawNumber = raw;
                return n;
            }
        }
    }
}
