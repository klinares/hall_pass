using System;
using System.Collections.Generic;
using System.Linq;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Core.Progress
{
    /// <summary>Helpers for JsonValue trees that the save needs (JsonValue itself belongs to Core/Math/Gen).</summary>
    public static class JsonTree
    {
        /// <summary>A deep copy (JS <c>JSON.parse(JSON.stringify(p))</c>), keeping key order. Null stays null.</summary>
        public static JsonValue Clone(JsonValue v)
        {
            if (v == null) return null;
            switch (v.Kind)
            {
                case JsonKind.Null: return JsonValue.Null;
                case JsonKind.Bool: return JsonValue.Bool(v.BoolValue);
                case JsonKind.Number: return JsonValue.Num(v.Number);
                case JsonKind.String: return JsonValue.Str(v.String);
                case JsonKind.Array: return JsonValue.Arr(v.Items.Select(Clone).ToList());
                default:
                    var o = JsonValue.Obj();
                    foreach (string k in v.Keys) o.Set(k, Clone(v[k]));
                    return o;
            }
        }

        /// <summary>
        /// JSON with keys in their stored order (JS JSON.stringify order), no whitespace, numbers as JS String(n),
        /// strings escaped as in <see cref="JsonValue.WriteString"/> (plain ASCII). The save is stored this way so
        /// dictionaries come back in the order they were filled, which the office relies on for ties.
        /// </summary>
        public static string Ordered(JsonValue v)
        {
            var sb = new System.Text.StringBuilder();
            WriteOrdered(v, sb);
            return sb.ToString();
        }

        private static void WriteOrdered(JsonValue v, System.Text.StringBuilder sb)
        {
            switch (v.Kind)
            {
                case JsonKind.Object:
                    sb.Append('{');
                    bool first = true;
                    foreach (string k in v.Keys)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        JsonValue.WriteString(sb, k);
                        sb.Append(':');
                        WriteOrdered(v[k], sb);
                    }
                    sb.Append('}');
                    break;
                case JsonKind.Array:
                    sb.Append('[');
                    for (int i = 0; i < v.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        WriteOrdered(v[i], sb);
                    }
                    sb.Append(']');
                    break;
                default:
                    v.WriteCanonical(sb);    // scalars are written the same either way
                    break;
            }
        }

        /// <summary>The string, or null when the value is missing or not a string.</summary>
        public static string Str(JsonValue v)
        {
            return v != null && v.Kind == JsonKind.String ? v.String : null;
        }

        /// <summary>True for a JSON number that is finite.</summary>
        public static bool IsNum(JsonValue v)
        {
            return v != null && v.Kind == JsonKind.Number && !double.IsNaN(v.Number) && !double.IsInfinity(v.Number);
        }

        /// <summary>A whole number in range, or the fallback. A fraction is floored.</summary>
        public static long Long(JsonValue v, long fallback)
        {
            if (!IsNum(v)) return fallback;
            double d = Math.Floor(v.Number);
            if (d > long.MaxValue / 2 || d < long.MinValue / 2) return fallback;
            return (long)d;
        }

        /// <summary>An int in range, or the fallback. A fraction is floored.</summary>
        public static int Int(JsonValue v, int fallback)
        {
            if (!IsNum(v)) return fallback;
            double d = Math.Floor(v.Number);
            if (d > int.MaxValue || d < int.MinValue) return fallback;
            return (int)d;
        }

        /// <summary>JS <c>typeof x === 'object' &amp;&amp; x</c> (objects and arrays).</summary>
        public static bool IsObjectLike(JsonValue v)
        {
            return v != null && (v.Kind == JsonKind.Object || v.Kind == JsonKind.Array);
        }
    }

    /// <summary>
    /// Reads a problem back from the JSON object a generator returns (<see cref="Problem.ToJson"/>), so a saved
    /// review problem comes back whole: prompt, label, answer, accept, choices (value or text), traps, steps,
    /// params, work and builder. <c>ToJson(Read(ToJson(p)))</c> is the same JSON as <c>ToJson(p)</c>.
    /// </summary>
    public static class ProblemJson
    {
        /// <summary>False (and a null problem) when the JSON doesn't have a problem's shape.</summary>
        public static bool TryRead(JsonValue j, out Problem problem)
        {
            problem = null;
            try
            {
                problem = Read(j);
                return true;
            }
            catch (FormatException) { return false; }
            catch (InvalidOperationException) { return false; }
            catch (OverflowException) { return false; }
        }

        /// <summary>Throws FormatException when the JSON doesn't have a problem's shape.</summary>
        public static Problem Read(JsonValue j)
        {
            if (j == null || j.Kind != JsonKind.Object) throw new FormatException("a problem is an object");
            var p = new Problem
            {
                Skill = JsonTree.Str(j["skill"]),
                Level = (int)Whole(j["level"], "level"),
                Kind = JsonTree.Str(j["kind"]),
                Seed = Whole(j["seed"], "seed"),
                Params = j["params"] == null ? null : JsonTree.Clone(j["params"]),
                Prompt = Need(j["prompt"], "prompt"),
                Label = JsonTree.Str(j["label"]),
            };
            JsonValue a = j["answer"];
            if (a == null || a.Kind != JsonKind.Object) throw new FormatException("answer");
            p.Answer = new Answer(TypeOf(Need(a["type"], "answer.type")), Value(a["value"]));
            JsonValue acc = j["accept"];
            p.Simplest = acc != null && acc["simplest"] != null && acc["simplest"].Kind == JsonKind.Bool && acc["simplest"].BoolValue;

            JsonValue cs = j["choices"];
            if (cs == null || cs.Kind == JsonKind.Null) p.Choices = null;
            else
            {
                if (cs.Kind != JsonKind.Array) throw new FormatException("choices");
                p.Choices = new List<Choice>();
                foreach (JsonValue c in cs.Items)
                {
                    bool correct = c["correct"] != null && c["correct"].Kind == JsonKind.Bool && c["correct"].BoolValue;
                    string why = JsonTree.Str(c["why"]);
                    JsonValue cv = c["value"];
                    p.Choices.Add(cv != null && cv.Kind == JsonKind.String ? new Choice(cv.String, correct, why) : new Choice(Value(cv), correct, why));
                }
            }
            p.Traps = new List<Trap>();
            JsonValue ts = j["traps"];
            if (ts != null && ts.Kind == JsonKind.Array)
                foreach (JsonValue t in ts.Items) p.Traps.Add(new Trap(Value(t["value"]), JsonTree.Str(t["why"])));
            JsonValue ss = j["steps"];
            if (ss == null || ss.Kind != JsonKind.Array) throw new FormatException("steps");
            p.Steps = ss.Items.Select(s => Need(s, "step")).ToList();
            p.Work = j["work"] == null ? null : JsonTree.Clone(j["work"]);
            JsonValue b = j["builder"];
            p.Builder = b == null ? (bool?)null : b.Kind == JsonKind.Bool ? b.BoolValue : throw new FormatException("builder");
            return p;
        }

        private static string Need(JsonValue v, string what)
        {
            string s = JsonTree.Str(v);
            if (s == null) throw new FormatException(what);
            return s;
        }

        private static long Whole(JsonValue v, string what)
        {
            if (!JsonTree.IsNum(v) || !MathValue.IsInteger(v.Number)) throw new FormatException(what);
            return checked((long)v.Number);
        }

        private static AnswerType TypeOf(string name)
        {
            switch (name)
            {
                case "money": return AnswerType.Money;
                case "int": return AnswerType.Int;
                case "decimal": return AnswerType.Decimal;
                case "fraction": return AnswerType.Fraction;
                case "point": return AnswerType.Point;
                default: throw new FormatException("answer type '" + name + "'");
            }
        }

        /// <summary>The inverse of <see cref="Problem.ValueJson"/>: a number, <c>{ n, d }</c>, <c>{ x, y }</c> or null.</summary>
        public static MathValue Value(JsonValue v)
        {
            if (v == null || v.Kind == JsonKind.Null) return MathValue.Null;
            if (v.Kind == JsonKind.Number) return MathValue.Num(v.Number);
            if (v.Kind == JsonKind.Object)
            {
                if (JsonTree.IsNum(v["n"]) && JsonTree.IsNum(v["d"])) return MathValue.Frac(v["n"].Number, v["d"].Number);
                if (JsonTree.IsNum(v["x"]) && JsonTree.IsNum(v["y"])) return MathValue.Pt(v["x"].Number, v["y"].Number);
            }
            throw new FormatException("not an answer value");
        }
    }
}
