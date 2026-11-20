using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WorldTour.Core.Progress;
using WorldTour.Core.Shows;

namespace WorldTour.Core.Meta
{
    /// <summary>One mode's row in the office (JS <c>office().skills</c> entries).</summary>
    public sealed class OfficeSkill
    {
        public string Id;
        public string Name;
        public string Kind;
        public int Level;
        /// <summary>Attempts.</summary>
        public int A;
        /// <summary>Percent right, or null with no attempts.</summary>
        public int? Acc;
        /// <summary>Percent hinted, or null with no attempts.</summary>
        public int? Hint;
        /// <summary>Finished shows in this mode, encores left out.</summary>
        public int Shows;
    }

    /// <summary>One most-missed word (JS <c>office().words</c> entries).</summary>
    public sealed class OfficeWord
    {
        public string W;
        public int Miss;
        public int A;
        /// <summary>Percent right on the first try, or null with no attempts.</summary>
        public int? First;
        /// <summary>The last day she spelled it, or null.</summary>
        public string Last;
    }

    /// <summary>One mistake pattern (JS <c>office().mistakes</c> entries).</summary>
    public sealed class OfficeMistake
    {
        public string Tag;
        public int N;
        /// <summary>The plain label from <see cref="Office.MistakeLabels"/>, or the tag itself.</summary>
        public string Label;
        /// <summary>The mode's name, or the skill key when no mode in the list uses it.</summary>
        public string Mode;
    }

    /// <summary>One calendar day of play (JS <c>office().daily</c> entries).</summary>
    public sealed class OfficeDay
    {
        /// <summary>"yyyy-mm-dd".</summary>
        public string D;
        public int Secs;
        public int Shows;
    }

    /// <summary>What the manager's office shows (JS <c>office()</c>'s result).</summary>
    public sealed class OfficeReport
    {
        /// <summary>Finished shows, encores left out.</summary>
        public int Shows;
        public long TotalSecs;
        public long Lifetime;
        public long Balance;
        /// <summary>Best day streak.</summary>
        public int Best;
        public int ReviewWords;
        public int ReviewMath;
        public List<OfficeSkill> Skills = new List<OfficeSkill>();
        /// <summary>At most 12, most missed first.</summary>
        public List<OfficeWord> Words = new List<OfficeWord>();
        /// <summary>At most 10, most frequent first.</summary>
        public List<OfficeMistake> Mistakes = new List<OfficeMistake>();
        /// <summary>The last days, oldest first, zeros included.</summary>
        public List<OfficeDay> Daily = new List<OfficeDay>();
        /// <summary>Keys in the saved sticker list (JS <c>Object.keys(s.journal.stickers).length</c>).</summary>
        public int Stickers;
        public int StickersTotal;
    }

    /// <summary>The manager's office (parent view), ported from world_tour/js/meta.js:72-155.</summary>
    public static class Office
    {
        public const int DefaultDays = 14;
        public const int MaxWords = 12;
        public const int MaxMistakes = 10;

        private static readonly string Apos = ((char)0x2019).ToString();
        private static readonly string Cubed = "10" + (char)0x00B3;
        private static readonly string Times = ((char)0x00D7).ToString();

        /// <summary>meta.js:72 <c>MISTAKES</c>: a plain label for every mistake tag the generators produce.</summary>
        public static readonly IReadOnlyDictionary<string, string> MistakeLabels = new Dictionary<string, string>
        {
            { "gave-total", "gave the total instead of the change" },
            { "no-borrow", "subtracted without regrouping (borrowing)" },
            { "no-carry", "added without carrying" },
            { "decimal-place", "decimal point in the wrong place" },
            { "added-instead", "added instead of multiplying" },
            { "partial-only", "multiplied by the ones digit only" },
            { "no-placeholder", "left out the zero when multiplying by tens" },
            { "place-value", "answer off by a factor of ten" },
            { "subtracted-instead", "subtracted once instead of dividing" },
            { "rounded-up", "counted a partly full row as full" },
            { "gave-remainder", "gave the remainder instead of the quotient" },
            { "added-denominators", "added the denominators" },
            { "subtracted-denominators", "subtracted the denominators" },
            { "tops-not-rewritten", "changed the denominator but not the numerator" },
            { "swapped", "swapped the x and y coordinates" },
            { "moves-swapped", "mixed up the across and up moves" },
            { "wrong-direction", "moved or shifted the wrong way" },
            { "neighbor-fact", "a neighboring times-table fact" },
            { "added", "added instead of multiplying" },
            { "subtracted", "subtracted instead of dividing" },
            { "zeros-off", "wrong number of zeros or decimal places" },
            { "multiplied-exponent", "read " + Cubed + " as 10 " + Times + " 3" },
            { "wrote-digits", "wrote " + Cubed + " as 103" },
            { "added-sides", "added the side lengths instead of multiplying" },
            { "one-layer", "counted one layer only" },
            { "visible-faces", "counted only the cubes that show" },
            { "surface-area", "found the outside surface, not the volume" },
            { "one-box", "counted one of the two boxes" },
            { "divided-once", "divided by one side instead of the floor area" },
            { "too-short", "didn" + Apos + "t fill the measure" },
            { "too-long", "overfilled the measure" },
            { "left-to-right", "worked left to right, ignoring order of operations" },
            { "ignored-parentheses", "ignored the parentheses" },
            { "missing-parentheses", "picked an expression missing its parentheses" },
            { "extra-parentheses", "picked an expression with parentheses that change the story" },
            { "added-all", "added every number in the story" },
            { "wrong-grouping", "grouped the wrong numbers together" },
            { "wrong-op", "used the wrong operation" },
        };

        /// <summary>meta.js:113 <c>pct</c>: whole percent, or null when there is nothing to divide by.</summary>
        private static int? Pct(long a, long b)
        {
            if (b == 0) return null;
            return (int)JsNum.Round(100.0 * a / b);
        }

        /// <summary>
        /// meta.js:115 <c>office(s, modes, today, days)</c>. <paramref name="today"/> is "yyyy-mm-dd" (the JS takes
        /// anything Date.parse reads; the game only ever passes this form). An unreadable day throws, as the JS
        /// toISOString does, unless <paramref name="days"/> is 0 or less.
        /// Text ties sort by ordinal comparison where the JS uses localeCompare (see DECISIONS.md).
        /// </summary>
        public static OfficeReport Build(SaveBlob s, IReadOnlyList<ModeInfo> modes, string today, int days = DefaultDays)
        {
            var real = s.Shows.Where(x => !x.Encore).ToList();
            long totalSecs = 0;
            foreach (int v in s.Time.Values) totalSecs += v;

            var o = new OfficeReport
            {
                Shows = real.Count, TotalSecs = totalSecs, Lifetime = s.Fans.Lifetime, Balance = s.Fans.Balance,
                Best = s.Streak.Best, ReviewWords = s.Review.Words.Count, ReviewMath = s.Review.Math.Count,
                Stickers = s.Journal.Stickers.Count, StickersTotal = Milestones.All.Count,
            };

            foreach (ModeInfo m in modes)
            {
                SkillRecord k;
                string key = Milestones.SkillKey(m);
                if (key == null || !s.Skills.TryGetValue(key, out k)) k = null;
                o.Skills.Add(new OfficeSkill
                {
                    Id = m.Id, Name = m.Name, Kind = m.Kind,
                    Level = k != null ? k.Level : 1, A = k != null ? k.A : 0,
                    Acc = Pct(k != null ? k.C : 0, k != null ? k.A : 0),
                    Hint = Pct(k != null ? k.H : 0, k != null ? k.A : 0),
                    Shows = real.Count(x => x.Mode == m.Id),
                });
            }

            o.Words = s.Words
                .Select(e => new OfficeWord { W = e.Key, Miss = e.Value.Miss, A = e.Value.A, First = Pct(e.Value.First, e.Value.A), Last = e.Value.Last })
                .Where(x => x.Miss > 0)
                .OrderByDescending(x => x.Miss)
                .ThenBy(x => x.First ?? 0)
                .ThenBy(x => x.W, StringComparer.Ordinal)
                .Take(MaxWords)
                .ToList();

            /* Object.fromEntries: a later mode with the same key wins */
            var byKey = new Dictionary<string, ModeInfo>();
            foreach (ModeInfo m in modes) { string key = Milestones.SkillKey(m); if (key != null) byKey[key] = m; }
            var mistakes = new List<OfficeMistake>();
            foreach (KeyValuePair<string, SkillRecord> e in s.Skills)
            {
                if (e.Value == null || e.Value.Mistakes == null) continue;
                foreach (KeyValuePair<string, int> t in e.Value.Mistakes)
                {
                    if (t.Value <= 0 || t.Key == "near") continue;
                    string label, mode = null;
                    if (!MistakeLabels.TryGetValue(t.Key, out label) || string.IsNullOrEmpty(label)) label = t.Key;
                    ModeInfo bm;
                    if (byKey.TryGetValue(e.Key, out bm)) mode = bm.Name;
                    mistakes.Add(new OfficeMistake { Tag = t.Key, N = t.Value, Label = label, Mode = string.IsNullOrEmpty(mode) ? e.Key : mode });
                }
            }
            o.Mistakes = mistakes.OrderByDescending(x => x.N).ThenBy(x => x.Label, StringComparer.Ordinal).Take(MaxMistakes).ToList();

            if (days > 0)
            {
                DateTime t0;
                if (!DateTime.TryParseExact(today ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out t0))
                    throw new ArgumentException("office: today must be a day written yyyy-mm-dd, got \"" + today + "\"", "today");
                for (int i = days - 1; i >= 0; i--)
                {
                    string d = t0.AddDays(-i).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    int secs;
                    s.Time.TryGetValue(d, out secs);
                    o.Daily.Add(new OfficeDay { D = d, Secs = secs, Shows = real.Count(x => x.D == d) });
                }
            }
            return o;
        }

        /// <summary>meta.js:155 <c>mins</c>: "0 min", "&lt;1 min", or whole minutes rounded the JS way.</summary>
        public static string Mins(long secs)
        {
            if (secs < 60) return secs != 0 ? "<1 min" : "0 min";
            return ((long)JsNum.Round(secs / 60.0)).ToString(CultureInfo.InvariantCulture) + " min";
        }
    }
}
