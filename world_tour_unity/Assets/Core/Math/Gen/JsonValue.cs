using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WorldTour.Core.Maths.Gen
{
    public enum JsonKind { Null = 0, Bool, Number, String, Array, Object }

    /// <summary>
    /// A small JSON tree for the loose parts of a problem (its <c>params</c> and <c>work</c>), which differ
    /// from skill to skill just as the JS object literals do. Objects keep insertion order; the canonical
    /// writer sorts keys. Plain C# so Core stays free of UnityEngine.
    /// </summary>
    public sealed class JsonValue
    {
        public readonly JsonKind Kind;
        private readonly double _num;
        private readonly bool _bool;
        private readonly string _str;
        private readonly List<JsonValue> _items;
        private readonly List<KeyValuePair<string, JsonValue>> _fields;

        private JsonValue(JsonKind kind, double num = 0, bool b = false, string str = null)
        {
            Kind = kind;
            _num = num;
            _bool = b;
            _str = str;
            if (kind == JsonKind.Array) _items = new List<JsonValue>();
            if (kind == JsonKind.Object) _fields = new List<KeyValuePair<string, JsonValue>>();
        }

        /// <summary>JSON null. Distinct from a C# null reference, which means "no such key" (JS undefined).</summary>
        public static readonly JsonValue Null = new JsonValue(JsonKind.Null);

        public static JsonValue Num(double v) => new JsonValue(JsonKind.Number, num: v);
        public static JsonValue Bool(bool v) => new JsonValue(JsonKind.Bool, b: v);
        public static JsonValue Str(string v) => v == null ? Null : new JsonValue(JsonKind.String, str: v);
        public static JsonValue Obj() => new JsonValue(JsonKind.Object);

        public static JsonValue Arr(IEnumerable<JsonValue> items)
        {
            var a = new JsonValue(JsonKind.Array);
            foreach (JsonValue v in items) a._items.Add(v ?? Null);
            return a;
        }

        public static JsonValue Arr(params JsonValue[] items) => Arr((IEnumerable<JsonValue>)items);

        public static JsonValue Ints(IEnumerable<int> items)
        {
            var a = new JsonValue(JsonKind.Array);
            foreach (int v in items) a._items.Add(Num(v));
            return a;
        }

        public static JsonValue Ints(IEnumerable<long> items)
        {
            var a = new JsonValue(JsonKind.Array);
            foreach (long v in items) a._items.Add(Num(v));
            return a;
        }

        /// <summary>A fraction as the JS writes it: <c>{ n, d }</c>.</summary>
        public static JsonValue Frac(Rational r) => Obj().Set("n", r.N).Set("d", r.D);

        /// <summary>Adds or replaces a key (keeping its first position, as a JS object does). Returns this, for chaining.</summary>
        public JsonValue Set(string key, JsonValue value)
        {
            if (Kind != JsonKind.Object) throw new InvalidOperationException("Set on a " + Kind);
            if (key == null) throw new ArgumentNullException(nameof(key));
            value = value ?? Null;
            for (int i = 0; i < _fields.Count; i++)
            {
                if (_fields[i].Key == key)
                {
                    _fields[i] = new KeyValuePair<string, JsonValue>(key, value);
                    return this;
                }
            }
            _fields.Add(new KeyValuePair<string, JsonValue>(key, value));
            return this;
        }

        public JsonValue Set(string key, long value) => Set(key, Num(value));
        public JsonValue Set(string key, string value) => Set(key, Str(value));
        public JsonValue Set(string key, bool value) => Set(key, Bool(value));
        public JsonValue Set(string key, Rational value) => Set(key, Frac(value));

        /// <summary>The value at a key, or a C# null when the key is missing (JS undefined). Null when this isn't an object.</summary>
        public JsonValue this[string key]
        {
            get
            {
                if (Kind != JsonKind.Object) return null;
                foreach (KeyValuePair<string, JsonValue> f in _fields) if (f.Key == key) return f.Value;
                return null;
            }
        }

        public JsonValue this[int index]
        {
            get
            {
                if (Kind != JsonKind.Array) throw new InvalidOperationException("index on a " + Kind);
                return _items[index];
            }
        }

        public bool Has(string key) => this[key] != null;

        /// <summary>Items in an array, or keys in an object.</summary>
        public int Count => Kind == JsonKind.Array ? _items.Count : Kind == JsonKind.Object ? _fields.Count : 0;

        public IReadOnlyList<JsonValue> Items => Kind == JsonKind.Array ? _items : (IReadOnlyList<JsonValue>)Array.Empty<JsonValue>();

        public IEnumerable<string> Keys
        {
            get
            {
                if (Kind != JsonKind.Object) yield break;
                foreach (KeyValuePair<string, JsonValue> f in _fields) yield return f.Key;
            }
        }

        public bool IsNull => Kind == JsonKind.Null;

        public double Number
        {
            get
            {
                if (Kind != JsonKind.Number) throw new InvalidOperationException("not a number: " + Kind);
                return _num;
            }
        }

        /// <summary>The number as a whole number; throws if it isn't one.</summary>
        public long Long
        {
            get
            {
                double v = Number;
                if (!MathValue.IsInteger(v)) throw new InvalidOperationException("not a whole number: " + JsNumber.Str(v));
                return checked((long)v);
            }
        }

        public int Int => checked((int)Long);

        public bool BoolValue
        {
            get
            {
                if (Kind != JsonKind.Bool) throw new InvalidOperationException("not a bool: " + Kind);
                return _bool;
            }
        }

        public string String
        {
            get
            {
                if (Kind != JsonKind.String) throw new InvalidOperationException("not a string: " + Kind);
                return _str;
            }
        }

        /// <summary>A <c>{ n, d }</c> object as a Rational.</summary>
        public Rational AsRational() => Rational.R(this["n"].Long, this["d"].Long);

        /// <summary>An array of whole numbers.</summary>
        public List<long> AsLongs()
        {
            var r = new List<long>();
            foreach (JsonValue v in Items) r.Add(v.Long);
            return r;
        }

        // ---------- canonical JSON ----------

        /// <summary>
        /// Canonical JSON, byte for byte what Tools/gen_vectors.mjs writes: keys sorted by UTF-16 code unit,
        /// no whitespace, numbers as JS String(n) (NaN and Infinity as null), strings with " and \ escaped and
        /// every character below 0x20 or above 0x7e as \u00xx (lowercase), so the result is plain ASCII.
        /// </summary>
        public string ToCanonicalJson()
        {
            var sb = new StringBuilder();
            WriteCanonical(sb);
            return sb.ToString();
        }

        public void WriteCanonical(StringBuilder sb)
        {
            switch (Kind)
            {
                case JsonKind.Null: sb.Append("null"); break;
                case JsonKind.Bool: sb.Append(_bool ? "true" : "false"); break;
                case JsonKind.Number: sb.Append(double.IsNaN(_num) || double.IsInfinity(_num) ? "null" : JsNumber.Str(_num)); break;
                case JsonKind.String: WriteString(sb, _str); break;
                case JsonKind.Array:
                    sb.Append('[');
                    for (int i = 0; i < _items.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        _items[i].WriteCanonical(sb);
                    }
                    sb.Append(']');
                    break;
                default:
                    var sorted = new List<KeyValuePair<string, JsonValue>>(_fields);
                    sorted.Sort((x, y) => string.CompareOrdinal(x.Key, y.Key));
                    sb.Append('{');
                    for (int i = 0; i < sorted.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        WriteString(sb, sorted[i].Key);
                        sb.Append(':');
                        sorted[i].Value.WriteCanonical(sb);
                    }
                    sb.Append('}');
                    break;
            }
        }

        public static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                if (c == '"') sb.Append("\\\"");
                else if (c == '\\') sb.Append("\\\\");
                else if (c < 0x20 || c > 0x7e) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else sb.Append(c);
            }
            sb.Append('"');
        }

        public override string ToString() => ToCanonicalJson();

        // ---------- where a parsed value came from (task 1.7; set only by Data.StrictJson.Parse) ----------
        // Additive: values built in code keep Line 0, Column 0 and RawNumber null, and the canonical writer ignores all of it.

        /// <summary>1-based line where this value starts in the parsed text; 0 when built in code.</summary>
        public int Line { get; internal set; }

        /// <summary>1-based column (UTF-16 characters) where this value starts; 0 when built in code.</summary>
        public int Column { get; internal set; }

        /// <summary>A parsed number's token exactly as typed ("12", "12.0", "1e3"); null when built in code or not a number.</summary>
        public string RawNumber { get; internal set; }

        /// <summary>
        /// True for a number typed with no fraction or exponent part (12, not 12.5, 12.0 or 1e3). For a number built in
        /// code there is no token, so it falls back to whether the value is whole.
        /// </summary>
        public bool IsWholeNumberToken =>
            Kind == JsonKind.Number && (RawNumber != null
                ? RawNumber.IndexOf('.') < 0 && RawNumber.IndexOf('e') < 0 && RawNumber.IndexOf('E') < 0
                : MathValue.IsInteger(_num));

        private Dictionary<string, int[]> _keyPos;

        /// <summary>Where a key's name starts in the parsed text. False when the key is missing or the object was built in code.</summary>
        public bool TryGetKeyPosition(string key, out int line, out int column)
        {
            line = column = 0;
            if (_keyPos == null || key == null || !_keyPos.TryGetValue(key, out int[] p)) return false;
            line = p[0];
            column = p[1];
            return true;
        }

        /// <summary>A fresh null value that can carry a position (the shared <see cref="Null"/> must not).</summary>
        internal static JsonValue NewNull() => new JsonValue(JsonKind.Null);

        /// <summary>Appends a parsed key with its position. The parser has already refused repeated keys.</summary>
        internal void AddParsed(string key, JsonValue value, int line, int column)
        {
            Set(key, value);
            if (_keyPos == null) _keyPos = new Dictionary<string, int[]>(StringComparer.Ordinal);
            _keyPos[key] = new[] { line, column };
        }
    }
}
