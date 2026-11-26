using System.Collections.Generic;
using System.Linq;
using WorldTour.Core.Data;
using WorldTour.Core.Maths.Gen;
using WorldTour.Core.Shows;

namespace WorldTour.Core.Progress
{
    /// <summary>What reading a stored save gave.</summary>
    public sealed class LoadResult
    {
        /// <summary>Always a usable save: the stored one migrated, or fresh defaults.</summary>
        public SaveBlob Blob;
        /// <summary>Null when nothing was stored.</summary>
        public string Text;
        /// <summary>True when stored text could not be read as JSON, so defaults were used (as the JS does).</summary>
        public bool Unreadable;
        /// <summary>The parser's message when <see cref="Unreadable"/>.</summary>
        public string Error;
    }

    /// <summary>
    /// The save as JSON, ported from world_tour/js/store.js (<c>defaults</c>, <c>migrate</c>, <c>load</c>). Reads
    /// and writes through JsonValue by hand, with no reflection. Dictionaries are filled in the stored key order
    /// and written back in it (never re-sorted), as JS objects keep insertion order.
    /// </summary>
    public static class SaveJson
    {
        // ---------- load ----------

        /// <summary>
        /// store.js:55-62 <c>load()</c> on text already read from storage: missing or unreadable text gives fresh
        /// defaults (the JS behaviour), anything else is migrated.
        /// </summary>
        public static LoadResult Load(string text, string today)
        {
            if (string.IsNullOrEmpty(text)) return new LoadResult { Blob = SaveBlob.Defaults(today) };
            JsonValue raw;
            StrictJsonException err;
            if (!StrictJson.TryParse(text, out raw, out err))
                return new LoadResult { Blob = SaveBlob.Defaults(today), Text = text, Unreadable = true, Error = err.Message };
            return new LoadResult { Blob = Migrate(raw, today), Text = text };
        }

        // Keys each part of the save knows. Anything else is kept as it was stored (save version 2) and written back
        // after the known keys, the order JS { ...defaults, ...raw } gives.
        /// <summary>The top-level keys this version knows (anything else is kept as stored).</summary>
        public static readonly string[] TopKeys = { "v", "created", "updated", "settings", "fans", "streak", "venues", "skills", "words", "review", "shows", "time", "inventory", "journal" };
        private static readonly string[] SettingsKeys = { "sound", "theme", "motion", "gentleSpelling", "gentleMath", "ambience", "effects" };
        private static readonly string[] FansKeys = { "balance", "lifetime" };
        private static readonly string[] StreakKeys = { "days", "best", "last" };
        private static readonly string[] VenuesKeys = { "unlocked", "seen" };
        private static readonly string[] ReviewKeys = { "words", "math" };
        private static readonly string[] InventoryKeys = { "owned", "equipped" };
        private static readonly string[] JournalKeys = { "stickers", "layout", "seen" };
        private static readonly string[] ShowKeys = { "d", "mode", "venue", "n", "first", "hinted", "fans", "secs", "best", "missed", "sold", "encore" };
        private static readonly string[] WordKeys = { "a", "c", "first", "h", "miss", "last" };
        private static readonly string[] SkillKeys = { "level", "run", "missRun", "a", "c", "h", "byLevel", "mistakes" };
        private static readonly string[] EntryKeys = { "skill", "level", "seed", "prob" };
        private static readonly string[] SpotKeys = { "x", "y", "r" };

        /// <summary>The keys of a stored object that <paramref name="known"/> doesn't list, deep-copied in stored order; null when none.</summary>
        internal static JsonValue Unknown(JsonValue src, string[] known)
        {
            if (src == null || src.Kind != JsonKind.Object) return null;
            JsonValue o = null;
            foreach (string k in src.Keys)
            {
                if (System.Array.IndexOf(known, k) >= 0) continue;
                if (o == null) o = JsonValue.Obj();
                o.Set(k, JsonTree.Clone(src[k]));
            }
            return o;
        }

        /// <summary>Appends kept unknown keys after the known ones (a known key is never overwritten).</summary>
        private static JsonValue WithExtra(JsonValue o, JsonValue extra)
        {
            if (extra == null) return o;
            foreach (string k in extra.Keys) if (!o.Has(k)) o.Set(k, extra[k]);
            return o;
        }

        /// <summary>
        /// store.js:26-40 <c>migrate(raw)</c>: fill in anything missing so older or partial saves keep working;
        /// coffee is always unlocked. Where the JS would keep a value of the wrong type (and then misbehave),
        /// this keeps the default instead; see DECISIONS.md. Save version 2: keys this version doesn't know are kept
        /// (top level, each group, and each show, word, skill, review entry and sticker spot), <c>journal.seen</c> is read
        /// (a version 1 save has none), and the version written is 2, or the stored one if it is newer.
        /// </summary>
        public static SaveBlob Migrate(JsonValue raw, string today)
        {
            var d = SaveBlob.Defaults(today);
            // store.js:28 if (!raw || typeof raw !== 'object') return d
            if (raw == null || raw.Kind != JsonKind.Object) return d;

            int storedV = JsonTree.Int(raw["v"], 0);
            if (storedV > SaveBlob.Version) d.V = storedV;
            d.Extra = Unknown(raw, TopKeys);

            // store.js:29 { ...d, ...raw, v: 1 }
            string created = JsonTree.Str(raw["created"]);
            if (created != null) d.Created = created;
            d.Updated = JsonTree.Long(raw["updated"], 0);

            // store.js:30-32 the nested groups merge key by key
            JsonValue s = Obj(raw["settings"]);
            if (s != null)
            {
                if (s["sound"] != null && s["sound"].Kind == JsonKind.Bool) d.Settings.Sound = s["sound"].BoolValue;
                d.Settings.Theme = JsonTree.Str(s["theme"]) ?? d.Settings.Theme;
                d.Settings.Motion = JsonTree.Str(s["motion"]) ?? d.Settings.Motion;
                if (s["gentleSpelling"] != null && s["gentleSpelling"].Kind == JsonKind.Bool) d.Settings.GentleSpelling = s["gentleSpelling"].BoolValue;
                if (s["gentleMath"] != null && s["gentleMath"].Kind == JsonKind.Bool) d.Settings.GentleMath = s["gentleMath"].BoolValue;
                if (s["ambience"] != null && s["ambience"].Kind == JsonKind.Number) d.Settings.Ambience = System.Math.Max(0, System.Math.Min(100, (int)JsonTree.Long(s["ambience"], 40)));
                if (s["effects"] != null && s["effects"].Kind == JsonKind.Number) d.Settings.Effects = System.Math.Max(0, System.Math.Min(100, (int)JsonTree.Long(s["effects"], 100)));
                d.Settings.Extra = Unknown(s, SettingsKeys);
            }
            JsonValue f = Obj(raw["fans"]);
            if (f != null)
            {
                d.Fans.Balance = JsonTree.Long(f["balance"], 0);
                d.Fans.Lifetime = JsonTree.Long(f["lifetime"], 0);
                d.Fans.Extra = Unknown(f, FansKeys);
            }
            JsonValue st = Obj(raw["streak"]);
            if (st != null)
            {
                d.Streak.Days = JsonTree.Int(st["days"], 0);
                d.Streak.Best = JsonTree.Int(st["best"], 0);
                d.Streak.Last = JsonTree.Str(st["last"]);
                d.Streak.Extra = Unknown(st, StreakKeys);
            }
            JsonValue ve = Obj(raw["venues"]);
            List<string> unlocked = null;
            if (ve != null)
            {
                unlocked = Strings(ve["unlocked"]);
                if (ve["seen"] != null) d.Venues.Seen = Strings(ve["seen"]) ?? new List<string> { Venues.First };
                d.Venues.Extra = Unknown(ve, VenuesKeys);
            }
            // store.js:38 coffee is always unlocked (deduplicated only when it had to be added, as in the JS)
            if (unlocked == null || !unlocked.Contains(Venues.First))
            {
                var fixedList = new List<string> { Venues.First };
                if (unlocked != null) fixedList.AddRange(unlocked);
                unlocked = fixedList.Distinct().ToList();
            }
            d.Venues.Unlocked = unlocked;

            JsonValue rv = Obj(raw["review"]);
            if (rv != null)
            {
                d.Review.Words = Strings(rv["words"]) ?? new List<string>();    // store.js:36
                d.Review.Math = ReadReview(rv["math"]);                           // store.js:37
                d.Review.Extra = Unknown(rv, ReviewKeys);
            }
            JsonValue inv = Obj(raw["inventory"]);
            if (inv != null)
            {
                if (inv["owned"] != null) d.Inventory.Owned = Strings(inv["owned"]) ?? new InventoryData().Owned;
                // store.js:33 equipped merges over the defaults
                JsonValue eq = Obj(inv["equipped"]);
                if (eq != null)
                    foreach (string k in eq.Keys)
                    {
                        string val = JsonTree.Str(eq[k]);
                        if (val != null) d.Inventory.Equipped[k] = val;
                    }
                d.Inventory.Extra = Unknown(inv, InventoryKeys);
            }
            JsonValue jo = Obj(raw["journal"]);
            if (jo != null)
            {
                JsonValue stk = Dict(jo["stickers"]);
                if (stk != null)
                    foreach (string k in stk.Keys)
                    {
                        string day = JsonTree.Str(stk[k]);
                        if (day != null) d.Journal.Stickers[k] = day;
                    }
                JsonValue lay = Dict(jo["layout"]);
                if (lay != null)
                    foreach (string k in lay.Keys)
                    {
                        JsonValue sp = Dict(lay[k]);
                        if (sp != null && JsonTree.IsNum(sp["x"]) && JsonTree.IsNum(sp["y"]))
                            d.Journal.Layout[k] = new Spot { X = sp["x"].Number, Y = sp["y"].Number, R = JsonTree.IsNum(sp["r"]) ? sp["r"].Number : 0, Extra = Unknown(sp, SpotKeys) };
                    }
                // version 2: the stickers she has seen peel on (a version 1 save has none); strings only, each once
                List<string> seen = Strings(jo["seen"]);
                if (seen != null) d.Journal.Seen = seen.Distinct().ToList();
                d.Journal.Extra = Unknown(jo, JournalKeys);
            }

            // store.js:34 skills, words, time must be plain objects
            JsonValue sk = Dict(raw["skills"]);
            if (sk != null)
                foreach (string k in sk.Keys)
                {
                    JsonValue r = Dict(sk[k]);
                    if (r != null) d.Skills[k] = ReadSkill(r);
                }
            JsonValue wd = Dict(raw["words"]);
            if (wd != null)
                foreach (string k in wd.Keys)
                {
                    JsonValue r = Dict(wd[k]);
                    if (r != null) d.Words[k] = ReadWord(r);
                }
            JsonValue tm = Dict(raw["time"]);
            if (tm != null)
                foreach (string k in tm.Keys)
                    if (JsonTree.IsNum(tm[k])) d.Time[k] = JsonTree.Int(tm[k], 0);

            // store.js:35 shows must be an array
            JsonValue sh = raw["shows"];
            if (sh != null && sh.Kind == JsonKind.Array)
                foreach (JsonValue x in sh.Items)
                {
                    JsonValue r = Dict(x);
                    if (r != null) d.Shows.Add(ReadShow(r));
                }
            return d;
        }

        /// <summary>JS <c>x &amp;&amp; typeof x === 'object'</c>, but an array counts as having no keys.</summary>
        private static JsonValue Obj(JsonValue v)
        {
            if (v == null) return null;
            if (v.Kind == JsonKind.Object) return v;
            if (v.Kind == JsonKind.Array) return JsonValue.Obj();
            return null;
        }

        /// <summary>A plain object (not an array), or null.</summary>
        private static JsonValue Dict(JsonValue v)
        {
            return v != null && v.Kind == JsonKind.Object ? v : null;
        }

        /// <summary>The strings in an array, or null when it isn't an array. Non-strings are dropped.</summary>
        private static List<string> Strings(JsonValue v)
        {
            if (v == null || v.Kind != JsonKind.Array) return null;
            return v.Items.Where(x => x.Kind == JsonKind.String).Select(x => x.String).ToList();
        }

        private static List<ReviewEntry> ReadReview(JsonValue v)
        {
            var list = new List<ReviewEntry>();
            if (v == null || v.Kind != JsonKind.Array) return list;
            foreach (JsonValue e in v.Items)
            {
                if (e.Kind != JsonKind.Object) continue;
                string skill = JsonTree.Str(e["skill"]);
                if (skill == null || !JsonTree.IsNum(e["level"]) || !JsonTree.IsNum(e["seed"])) continue;
                JsonValue prob = e["prob"];
                list.Add(new ReviewEntry
                {
                    Skill = skill,
                    Level = JsonTree.Int(e["level"], 1),
                    Seed = JsonTree.Long(e["seed"], 0),
                    Prob = prob == null ? null : JsonTree.Clone(prob),
                    Extra = Unknown(e, EntryKeys)
                });
            }
            return list;
        }

        private static SkillRecord ReadSkill(JsonValue r)
        {
            var k = new SkillRecord
            {
                Level = JsonTree.Int(r["level"], LevelRules.MinLevel),
                Run = JsonTree.Int(r["run"], 0),
                MissRun = JsonTree.Int(r["missRun"], 0),
                A = JsonTree.Int(r["a"], 0),
                C = JsonTree.Int(r["c"], 0),
                H = JsonTree.Int(r["h"], 0),
            };
            JsonValue bl = Dict(r["byLevel"]);
            if (bl != null)
                foreach (string key in bl.Keys)
                {
                    int lv;
                    JsonValue row = bl[key];
                    if (!int.TryParse(key, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out lv)) continue;
                    if (row == null || row.Kind != JsonKind.Array || row.Count < 2) continue;
                    k.ByLevel[lv] = new[] { JsonTree.Int(row[0], 0), JsonTree.Int(row[1], 0) };
                }
            JsonValue ms = Dict(r["mistakes"]);
            if (ms != null)
                foreach (string key in ms.Keys)
                    if (JsonTree.IsNum(ms[key])) k.Mistakes[key] = JsonTree.Int(ms[key], 0);
            k.Extra = Unknown(r, SkillKeys);
            return k;
        }

        private static WordStat ReadWord(JsonValue r)
        {
            return new WordStat
            {
                A = JsonTree.Int(r["a"], 0),
                C = JsonTree.Int(r["c"], 0),
                First = JsonTree.Int(r["first"], 0),
                H = JsonTree.Int(r["h"], 0),
                Miss = JsonTree.Int(r["miss"], 0),
                Last = JsonTree.Str(r["last"]),
                Extra = Unknown(r, WordKeys)
            };
        }

        private static ShowRecord ReadShow(JsonValue r)
        {
            return new ShowRecord
            {
                D = JsonTree.Str(r["d"]),
                Mode = JsonTree.Str(r["mode"]),
                Venue = JsonTree.Str(r["venue"]),
                N = JsonTree.Int(r["n"], 0),
                First = JsonTree.Int(r["first"], 0),
                Hinted = JsonTree.Int(r["hinted"], 0),
                Fans = JsonTree.Int(r["fans"], 0),
                Secs = JsonTree.Int(r["secs"], 0),
                Best = JsonTree.Int(r["best"], 0),
                Missed = System.Math.Max(0, JsonTree.Int(r["missed"], 0)),
                Sold = Truthy(r["sold"]),
                Encore = Truthy(r["encore"]),
                Extra = Unknown(r, ShowKeys)
            };
        }

        /// <summary>JS truthiness for the flags milestones test (<c>x.sold</c>, <c>x.encore</c>).</summary>
        private static bool Truthy(JsonValue v)
        {
            if (v == null) return false;
            switch (v.Kind)
            {
                case JsonKind.Bool: return v.BoolValue;
                case JsonKind.Number: return v.Number != 0 && !double.IsNaN(v.Number);
                case JsonKind.String: return v.String.Length > 0;
                case JsonKind.Null: return false;
                default: return true;
            }
        }

        // ---------- write ----------

        /// <summary>The save as the JSON object the JS keeps (same keys and shapes).</summary>
        public static JsonValue Write(SaveBlob b)
        {
            var o = JsonValue.Obj()
                .Set("v", b.V)
                .Set("created", b.Created)
                .Set("updated", b.Updated)
                .Set("settings", WithExtra(WriteSettings(b.Settings), b.Settings.Extra))
                .Set("fans", WithExtra(JsonValue.Obj().Set("balance", b.Fans.Balance).Set("lifetime", b.Fans.Lifetime), b.Fans.Extra))
                .Set("streak", WithExtra(JsonValue.Obj().Set("days", b.Streak.Days).Set("best", b.Streak.Best).Set("last", b.Streak.Last), b.Streak.Extra))
                .Set("venues", WithExtra(JsonValue.Obj().Set("unlocked", StrArr(b.Venues.Unlocked)).Set("seen", StrArr(b.Venues.Seen)), b.Venues.Extra));
            var skills = JsonValue.Obj();
            foreach (var kv in b.Skills) skills.Set(kv.Key, WriteSkill(kv.Value));
            o.Set("skills", skills);
            var words = JsonValue.Obj();
            foreach (var kv in b.Words)
            {
                WordStat w = kv.Value;
                words.Set(kv.Key, WithExtra(JsonValue.Obj().Set("a", w.A).Set("c", w.C).Set("first", w.First).Set("h", w.H).Set("miss", w.Miss).Set("last", w.Last), w.Extra));
            }
            o.Set("words", words);
            var math = new List<JsonValue>();
            foreach (ReviewEntry r in b.Review.Math)
            {
                var e = JsonValue.Obj().Set("skill", r.Skill).Set("level", r.Level).Set("seed", r.Seed);
                if (r.Prob != null) e.Set("prob", r.Prob);
                math.Add(WithExtra(e, r.Extra));
            }
            o.Set("review", WithExtra(JsonValue.Obj().Set("words", StrArr(b.Review.Words)).Set("math", JsonValue.Arr(math)), b.Review.Extra));
            var shows = new List<JsonValue>();
            foreach (ShowRecord x in b.Shows)
            {
                var e = JsonValue.Obj().Set("d", x.D).Set("mode", x.Mode).Set("venue", x.Venue).Set("n", x.N).Set("first", x.First)
                    .Set("hinted", x.Hinted).Set("fans", x.Fans).Set("secs", x.Secs).Set("best", x.Best);
                if (x.Missed > 0) e.Set("missed", x.Missed);
                if (x.Sold) e.Set("sold", true);
                if (x.Encore) e.Set("encore", true);
                shows.Add(WithExtra(e, x.Extra));
            }
            o.Set("shows", JsonValue.Arr(shows));
            var time = JsonValue.Obj();
            foreach (var kv in b.Time) time.Set(kv.Key, kv.Value);
            o.Set("time", time);
            var eq = JsonValue.Obj();
            foreach (var kv in b.Inventory.Equipped) eq.Set(kv.Key, kv.Value);
            o.Set("inventory", WithExtra(JsonValue.Obj().Set("owned", StrArr(b.Inventory.Owned)).Set("equipped", eq), b.Inventory.Extra));
            var stk = JsonValue.Obj();
            foreach (var kv in b.Journal.Stickers) stk.Set(kv.Key, kv.Value);
            var lay = JsonValue.Obj();
            foreach (var kv in b.Journal.Layout)
                lay.Set(kv.Key, WithExtra(JsonValue.Obj().Set("x", JsonValue.Num(kv.Value.X)).Set("y", JsonValue.Num(kv.Value.Y)).Set("r", JsonValue.Num(kv.Value.R)), kv.Value.Extra));
            o.Set("journal", WithExtra(JsonValue.Obj().Set("stickers", stk).Set("layout", lay).Set("seen", StrArr(b.Journal.Seen)), b.Journal.Extra));
            return WithExtra(o, b.Extra);
        }

        /// <summary>
        /// The text to store under <see cref="SaveBlob.Key"/>: JSON in the JS key order (the order of store.js
        /// defaults, and dictionaries in the order they were filled), plain ASCII. Loading keeps that order.
        /// </summary>
        public static string ToText(SaveBlob b)
        {
            return JsonTree.Ordered(Write(b));
        }

        /// <summary>Canonical JSON (sorted keys), for comparing saves.</summary>
        public static string ToCanonical(SaveBlob b)
        {
            return Write(b).ToCanonicalJson();
        }

        /// <summary>The known settings; the two gentle switches are written only when on, so saves without them match the web game's.</summary>
        private static JsonValue WriteSettings(SettingsData st)
        {
            JsonValue o = JsonValue.Obj().Set("sound", st.Sound).Set("theme", st.Theme).Set("motion", st.Motion);
            if (st.GentleSpelling) o.Set("gentleSpelling", true);
            if (st.GentleMath) o.Set("gentleMath", true);
            if (st.Ambience.HasValue) o.Set("ambience", (long)st.Ambience.Value);
            if (st.Effects.HasValue) o.Set("effects", (long)st.Effects.Value);
            return o;
        }

        private static JsonValue WriteSkill(SkillRecord k)
        {
            var bl = JsonValue.Obj();
            foreach (var kv in k.ByLevel.OrderBy(x => x.Key))
                bl.Set(kv.Key.ToString(System.Globalization.CultureInfo.InvariantCulture), JsonValue.Ints(kv.Value.Select(x => (long)x)));
            var ms = JsonValue.Obj();
            foreach (var kv in k.Mistakes) ms.Set(kv.Key, kv.Value);
            return WithExtra(JsonValue.Obj().Set("level", k.Level).Set("run", k.Run).Set("missRun", k.MissRun)
                .Set("a", k.A).Set("c", k.C).Set("h", k.H).Set("byLevel", bl).Set("mistakes", ms), k.Extra);
        }

        private static JsonValue StrArr(IEnumerable<string> items)
        {
            return JsonValue.Arr(items.Select(JsonValue.Str).ToList());
        }
    }
}
