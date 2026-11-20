using System;
using System.Collections.Generic;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Core.Data
{
    /// <summary>
    /// The result of reading weeks.json. <see cref="File"/> holds only the weeks the game can use, in file order; it is
    /// null (the file is refused) when the text can't be parsed, has no weeks list, or no week is valid. A week with an
    /// error is skipped: its findings name it, and <see cref="Skipped"/> lists it for the grown-up message.
    /// </summary>
    public sealed class WeeksLoad
    {
        public WeeksFile File;
        public List<Finding> Findings = new List<Finding>();
        /// <summary>Names of the skipped weeks ("week 2026-11-23", or "week #3" without an id), in file order. Empty when refused.</summary>
        public List<string> Skipped = new List<string>();
        public bool HasErrors => Findings.Exists(f => f.IsError);
        public bool Refused => File == null;
    }

    /// <summary>The result of reading math_config.json. <see cref="Config"/> is never null: on any error it is the web defaults.</summary>
    public sealed class MathConfigLoad
    {
        public MathConfig Config;
        public bool UsedDefaults;
        public List<Finding> Findings = new List<Finding>();
        public bool HasErrors => Findings.Exists(f => f.IsError);
    }

    /// <summary>
    /// Reads the two hand-edited data files in three steps: strict parse (syntax, with line and column), raw key and
    /// type checks on the tree (unknown keys, wrong types, numbers out of range), then the model's own Validate()
    /// (meaning). For weeks.json both checks go week by week: a week with a problem is skipped (the meaning checks run only
    /// on weeks the key and type checks passed, so one mistake gives one message) and the rest still load.
    /// The models are filled straight from the tree: no JsonUtility, no reflection.
    /// Tools/validate_data.mjs does the same steps with the same messages; the parity test keeps them identical.
    /// </summary>
    public static class DataFiles
    {
        public const string WeeksFileName = "weeks.json";
        public const string MathConfigFileName = "math_config.json";

        private static readonly string[] RootKeys = { "weeks" };
        private static readonly string[] WeekKeys = { "id", "label", "words" };
        private static readonly string[] WordKeys = { "w", "s", "h", "x", "prefix" };
        private static readonly string[] PrefixKeys = { "p", "root", "means" };
        private static readonly string[] ConfigKeys = { "ENABLED_SKILLS", "BOUNDS" };
        private static readonly string[] SkillKeys = { "dec", "md", "frac", "xy", "vol", "ops", "flu" };
        private static readonly string[] BoundKeys = { "moneyCents", "maxDenominator", "coordMax", "maxVolume", "storyDollars" };

        // ---------------- weeks.json ----------------

        public static WeeksLoad LoadWeeks(string text, string file = WeeksFileName)
        {
            var load = new WeeksLoad();
            JsonValue root = ParseOrReport(text, file, load.Findings);
            if (root == null) return load;
            var f = load.Findings;
            if (root.Kind != JsonKind.Object)
            {
                f.Add(Err(file, root.Line, "the file should be a { } block holding \"weeks\": [ ... ]"));
                return load;
            }
            Unknown(f, file, root, "the top level", RootKeys);
            JsonValue weeks = root["weeks"];
            if (weeks == null) { f.Add(Err(file, root.Line, "the top level: \"weeks\" is missing")); return load; }
            if (!Expect(f, file, "the top level", "weeks", weeks, JsonKind.Array)) return load;

            // Key and type checks week by week: a week with any problem here is skipped without the meaning checks.
            var rawSkipped = new string[weeks.Count];
            for (int i = 0; i < weeks.Count; i++)
            {
                int before = WeeksFile.ErrorCount(f);
                string at = RawWeek(f, file, weeks[i], i);
                if (WeeksFile.ErrorCount(f) > before) rawSkipped[i] = WeeksFile.SkipName(at, weeks[i].Line);
            }

            WeeksFile model = MapWeeks(weeks, rawSkipped);
            f.AddRange(model.Check(file, rawSkipped, out Week[] kept));
            if (kept.Length > 0)
            {
                load.File = new WeeksFile { weeks = kept };
                for (int i = 0; i < model.weeks.Length; i++)
                {
                    if (rawSkipped[i] != null) load.Skipped.Add(rawSkipped[i]);
                    else if (Array.IndexOf(kept, model.weeks[i]) < 0) load.Skipped.Add(WeeksFile.SkipName(WeeksFile.WeekName(model.weeks[i], i), model.weeks[i].Line));
                }
            }
            return load;
        }

        /// <summary>Key and type checks for one week. Returns the week's name for messages.</summary>
        private static string RawWeek(List<Finding> f, string file, JsonValue wk, int i)
        {
            string at = "week #" + (i + 1);
            if (wk.Kind != JsonKind.Object) { f.Add(Err(file, wk.Line, at + " should be a { } block, but it is " + Describe(wk))); return at; }
            JsonValue id = wk["id"];
            if (id != null && id.Kind == JsonKind.String && !WeeksFile.Blank(id.String)) at = "week " + id.String;
            Unknown(f, file, wk, at, WeekKeys);
            ExpectOptional(f, file, at, "id", wk, JsonKind.String);
            ExpectOptional(f, file, at, "label", wk, JsonKind.String);
            JsonValue words = wk["words"];
            if (words == null || !Expect(f, file, at, "words", words, JsonKind.Array)) return at;
            for (int j = 0; j < words.Count; j++)
            {
                JsonValue v = words[j];
                string wat = at + ", word #" + (j + 1);
                if (v.Kind != JsonKind.Object) { f.Add(Err(file, v.Line, wat + " should be a { } block, but it is " + Describe(v))); continue; }
                JsonValue w = v["w"];
                if (w != null && w.Kind == JsonKind.String && !WeeksFile.Blank(w.String)) wat = at + ", word \"" + w.String + "\"";
                Unknown(f, file, v, wat, WordKeys);
                ExpectOptional(f, file, wat, "w", v, JsonKind.String);
                ExpectOptional(f, file, wat, "s", v, JsonKind.String);
                ExpectOptional(f, file, wat, "h", v, JsonKind.String);
                ExpectOptional(f, file, wat, "x", v, JsonKind.String);
                JsonValue p = v["prefix"];
                if (p != null && Expect(f, file, wat, "prefix", p, JsonKind.Object))
                {
                    string pat = wat + ", prefix";
                    Unknown(f, file, p, pat, PrefixKeys);
                    ExpectOptional(f, file, pat, "p", p, JsonKind.String);
                    ExpectOptional(f, file, pat, "root", p, JsonKind.String);
                    ExpectOptional(f, file, pat, "means", p, JsonKind.String);
                }
            }
            return at;
        }

        /// <summary>Fills the model from the weeks that passed the key and type checks (others stay null). Missing keys stay null.</summary>
        private static WeeksFile MapWeeks(JsonValue weeks, string[] rawSkipped)
        {
            var list = new Week[weeks.Count];
            for (int i = 0; i < list.Length; i++)
            {
                if (rawSkipped[i] != null) continue;
                JsonValue wk = weeks[i];
                JsonValue words = wk["words"];
                var week = new Week { id = Text(wk, "id"), label = Text(wk, "label"), Line = wk.Line };
                if (words != null)
                {
                    week.words = new Word[words.Count];
                    for (int j = 0; j < week.words.Length; j++)
                    {
                        JsonValue v = words[j];
                        JsonValue p = v["prefix"];
                        week.words[j] = new Word
                        {
                            w = Text(v, "w"), s = Text(v, "s"), h = Text(v, "h"), x = Text(v, "x"), Line = v.Line,
                            prefix = p == null ? null : new Prefix { p = Text(p, "p"), root = Text(p, "root"), means = Text(p, "means") }
                        };
                    }
                }
                list[i] = week;
            }
            return new WeeksFile { weeks = list };
        }

        // ---------------- math_config.json ----------------

        public static MathConfigLoad LoadMathConfig(string text, string file = MathConfigFileName)
        {
            var load = new MathConfigLoad();
            var f = load.Findings;
            JsonValue root = ParseOrReport(text, file, f);
            if (root != null)
            {
                if (root.Kind != JsonKind.Object) f.Add(Err(file, root.Line, "the file should be a { } block holding \"ENABLED_SKILLS\" and \"BOUNDS\""));
                else
                {
                    Unknown(f, file, root, "the top level", ConfigKeys);
                    JsonValue sk = root["ENABLED_SKILLS"];
                    if (sk != null && Expect(f, file, "the top level", "ENABLED_SKILLS", sk, JsonKind.Object))
                    {
                        Unknown(f, file, sk, "ENABLED_SKILLS", SkillKeys);
                        foreach (string k in SkillKeys) ExpectOptional(f, file, "ENABLED_SKILLS", k, sk, JsonKind.Bool);
                    }
                    JsonValue b = root["BOUNDS"];
                    if (b != null && Expect(f, file, "the top level", "BOUNDS", b, JsonKind.Object))
                    {
                        Unknown(f, file, b, "BOUNDS", BoundKeys);
                        foreach (string k in BoundKeys) ExpectInt(f, file, "BOUNDS", k, b);
                    }
                    if (!load.HasErrors)
                    {
                        MathConfig cfg = MapConfig(root);
                        // MathConfig.Validate() (task 1.2) puts the file name in front; the Finding carries it instead.
                        foreach (string e in cfg.Validate())
                        {
                            string msg = e.StartsWith(file + ": ", StringComparison.Ordinal) ? e.Substring(file.Length + 2) : e;
                            f.Add(Err(file, 0, msg));
                        }
                        if (!load.HasErrors) load.Config = cfg;
                    }
                }
            }
            if (load.Config == null) { load.Config = MathConfig.WebDefaults(); load.UsedDefaults = true; }
            return load;
        }

        /// <summary>
        /// Missing keys keep the class defaults, as JsonUtility would: a missing skill switch stays on (the JS shows a
        /// skill unless it is false) and a missing bound stays -1, which MathConfig.Validate() reports.
        /// </summary>
        private static MathConfig MapConfig(JsonValue root)
        {
            var cfg = new MathConfig();
            JsonValue sk = root["ENABLED_SKILLS"];
            if (sk != null)
            {
                SkillSwitches s = cfg.ENABLED_SKILLS;
                s.dec = Bool(sk, "dec", s.dec); s.md = Bool(sk, "md", s.md); s.frac = Bool(sk, "frac", s.frac); s.xy = Bool(sk, "xy", s.xy);
                s.vol = Bool(sk, "vol", s.vol); s.ops = Bool(sk, "ops", s.ops); s.flu = Bool(sk, "flu", s.flu);
            }
            JsonValue b = root["BOUNDS"];
            if (b != null)
            {
                GradeBounds g = cfg.BOUNDS;
                g.moneyCents = Int(b, "moneyCents", g.moneyCents); g.maxDenominator = Int(b, "maxDenominator", g.maxDenominator);
                g.coordMax = Int(b, "coordMax", g.coordMax); g.maxVolume = Int(b, "maxVolume", g.maxVolume);
                g.storyDollars = Int(b, "storyDollars", g.storyDollars);
            }
            return cfg;
        }

        // ---------------- shared checks ----------------

        private static JsonValue ParseOrReport(string text, string file, List<Finding> f)
        {
            if (StrictJson.TryParse(text, out JsonValue root, out StrictJsonException err)) return root;
            f.Add(new Finding(Severity.Error, file, err.Line, err.Column, err.Reason));
            return null;
        }

        private static Finding Err(string file, int line, string msg) => new Finding(Severity.Error, file, line, 0, msg);

        private static void Unknown(List<Finding> f, string file, JsonValue obj, string at, string[] known)
        {
            foreach (string key in obj.Keys)
            {
                if (Array.IndexOf(known, key) >= 0) continue;
                string near = null;
                foreach (string k in known) if (TextRules.AsciiLower(k) == TextRules.AsciiLower(key)) near = k;
                obj.TryGetKeyPosition(key, out int keyLine, out int _);
                f.Add(Err(file, keyLine, at + ": \"" + key + "\" is not a field the game knows" +
                    (near != null ? " (did you mean \"" + near + "\"? names are case-sensitive)" : " (fields here: " + string.Join(", ", known) + ")")));
            }
        }

        private static void ExpectOptional(List<Finding> f, string file, string at, string key, JsonValue obj, JsonKind kind)
        {
            JsonValue v = obj[key];
            if (v != null) Expect(f, file, at, key, v, kind);
        }

        private static bool Expect(List<Finding> f, string file, string at, string key, JsonValue v, JsonKind kind)
        {
            if (v.Kind == kind) return true;
            f.Add(Err(file, v.Line, at + ": \"" + key + "\" should be " + Expected(kind) + ", but it is " + Describe(v)));
            return false;
        }

        private static void ExpectInt(List<Finding> f, string file, string at, string key, JsonValue obj)
        {
            JsonValue v = obj[key];
            if (v == null) return;
            if (v.Kind != JsonKind.Number || !v.IsWholeNumberToken)
            {
                f.Add(Err(file, v.Line, at + ": \"" + key + "\" should be a whole number, but it is " + Describe(v)));
                return;
            }
            string raw = Raw(v);
            string digits = raw.TrimStart('-');
            bool neg = raw.StartsWith("-", StringComparison.Ordinal);
            bool inRange = digits.Length <= 10 && (neg ? -long.Parse(digits, System.Globalization.CultureInfo.InvariantCulture) >= int.MinValue : long.Parse(digits, System.Globalization.CultureInfo.InvariantCulture) <= int.MaxValue);
            if (!inRange)
                f.Add(Err(file, v.Line, at + ": \"" + key + "\" is " + raw + ", which is too " + (neg ? "small (the smallest allowed is " + int.MinValue : "big (the largest allowed is " + int.MaxValue) + ")"));
            // Every bound must be above 0. Checked here, on what was typed, because MathConfig keeps -1 as its "not set"
            // marker and would call a typed -1 missing.
            else if (neg || long.Parse(digits, System.Globalization.CultureInfo.InvariantCulture) == 0)
                f.Add(Err(file, v.Line, at + ": \"" + key + "\" should be a whole number above 0, but it is " + raw));
        }

        private static string Expected(JsonKind kind)
        {
            switch (kind)
            {
                case JsonKind.String: return "text in double quotes";
                case JsonKind.Bool: return "true or false (without quotes)";
                case JsonKind.Number: return "a number";
                case JsonKind.Array: return "a list in [ ]";
                case JsonKind.Object: return "a { } block";
                default: return "null";
            }
        }

        /// <summary>What was typed, in words: the text "no", 12.5, true, null, a list [ ], a { } block.</summary>
        public static string Describe(JsonValue v)
        {
            switch (v.Kind)
            {
                case JsonKind.String: return "the text \"" + (v.String.Length > 40 ? v.String.Substring(0, 40) + "..." : v.String) + "\"";
                case JsonKind.Number: return Raw(v);
                case JsonKind.Bool: return v.BoolValue ? "true" : "false";
                case JsonKind.Null: return "null";
                case JsonKind.Array: return "a list [ ]";
                default: return "a { } block";
            }
        }

        /// <summary>A number as typed; for a value built in code, as JS String(n) writes it.</summary>
        private static string Raw(JsonValue v) => v.RawNumber ?? JsNumber.Str(v.Number);
        private static string Text(JsonValue obj, string key) => obj[key]?.String;
        private static bool Bool(JsonValue obj, string key, bool fallback) { JsonValue v = obj[key]; return v == null ? fallback : v.BoolValue; }
        private static int Int(JsonValue obj, string key, int fallback) { JsonValue v = obj[key]; return v == null ? fallback : int.Parse(Raw(v), System.Globalization.CultureInfo.InvariantCulture); }
    }
}
