using System;
using System.Collections.Generic;
using WorldTour.Core.Progress;
using WorldTour.Core.Shows;

namespace WorldTour.Core.Meta
{
    /// <summary>
    /// A playable mode as the journal and the office see it: the fields of a world_tour/modes/index.js
    /// <c>MODES</c> entry that meta.js reads (<c>{ id, name, kind, skill }</c>). The Game layer builds the list.
    /// </summary>
    public sealed class ModeInfo
    {
        public const string Spelling = "spelling";
        public const string Math = "math";

        public readonly string Id;
        public readonly string Name;
        /// <summary>"spelling" or "math".</summary>
        public readonly string Kind;
        /// <summary>Math modes: the generator skill ("dec", "frac", ...); null for spelling.</summary>
        public readonly string Skill;

        /// <summary>
        /// The level that counts as maxed for the max-level stickers: 3, or a math skill's MAX_LEVEL from
        /// math_config.json (the config cap, not the office's gentle cap; QUESTIONS 24).
        /// </summary>
        public readonly int MaxLevel;

        public ModeInfo(string id, string name, string kind, string skill = null, int maxLevel = 3)
        {
            Id = id; Name = name; Kind = kind; Skill = skill; MaxLevel = maxLevel;
        }

        /// <summary>The same mode with another maxed level.</summary>
        public ModeInfo WithMaxLevel(int maxLevel) { return new ModeInfo(Id, Name, Kind, Skill, maxLevel); }
    }

    /// <summary>
    /// One tour journal sticker (world_tour/data/milestones.js). <see cref="Test"/> reads saved progress and says
    /// whether it is earned. Earned stickers are kept forever, even if the thing that earned them changes later.
    /// </summary>
    public sealed class Milestone
    {
        public readonly string Id;
        /// <summary>A sticker art id, or "venue:&lt;id&gt;" for a venue drawing.</summary>
        public readonly string Art;
        public readonly string Name;
        /// <summary>How to earn it, shown on the empty slot.</summary>
        public readonly string How;
        /// <summary>JS <c>test(s, ctx)</c>, with <c>ctx.modes</c> passed directly.</summary>
        public readonly Func<SaveBlob, IReadOnlyList<ModeInfo>, bool> Test;

        public Milestone(string id, string art, string name, string how, Func<SaveBlob, IReadOnlyList<ModeInfo>, bool> test)
        {
            Id = id; Art = art; Name = name; How = how; Test = test;
        }
    }

    /// <summary>The tour journal stickers and their rules, ported from world_tour/data/milestones.js.</summary>
    public static class Milestones
    {
        /// <summary>
        /// milestones.js:8: mastered means at level 3 with 10 right answers there (one hot show can reach level 3,
        /// so that alone isn't enough).
        /// </summary>
        public const int MasterAt3 = 10;

        /// <summary>milestones.js:11 <c>skillKey</c>: the key a mode's level is saved under; math by skill, spelling as "spell:&lt;mode&gt;".</summary>
        public static string SkillKey(ModeInfo m)
        {
            return m.Kind == ModeInfo.Math ? m.Skill : "spell:" + m.Id;
        }

        /// <summary>milestones.js:5 <c>showsDone</c>: finished shows that are not encores.</summary>
        private static IEnumerable<ShowRecord> ShowsDone(SaveBlob s)
        {
            foreach (ShowRecord x in s.Shows) if (!x.Encore) yield return x;
        }

        /// <summary>milestones.js:6 <c>soldOut</c>: <c>x.sold || (x.n &gt;= 5 &amp;&amp; x.first / x.n &gt;= .9)</c>.</summary>
        private static bool SoldOut(ShowRecord x)
        {
            return x.Sold || (x.N >= 5 && (double)x.First / x.N >= .9);
        }

        private static int CountDone(SaveBlob s)
        {
            int n = 0;
            foreach (ShowRecord x in ShowsDone(s)) n++;
            return n;
        }

        private static bool AnyDone(SaveBlob s, Func<ShowRecord, bool> f)
        {
            foreach (ShowRecord x in ShowsDone(s)) if (f(x)) return true;
            return false;
        }

        /// <summary>
        /// milestones.js:9 <c>maxLevel</c>: some mode is at its top level with byLevel[top][1] (right answers there)
        /// of at least <see cref="MasterAt3"/>. The top level is 3, or a math skill's config MAX_LEVEL
        /// (<see cref="ModeInfo.MaxLevel"/>). A missing record or row counts as 0.
        /// </summary>
        private static bool MaxLevel(SaveBlob s, IEnumerable<ModeInfo> modes)
        {
            foreach (ModeInfo m in modes)
            {
                string id = SkillKey(m);
                SkillRecord k;
                if (id == null || !s.Skills.TryGetValue(id, out k) || k == null || k.Level != m.MaxLevel) continue;
                int[] row;
                int right = k.ByLevel != null && k.ByLevel.TryGetValue(m.MaxLevel, out row) && row != null && row.Length > 1 ? row[1] : 0;
                if (right >= MasterAt3) return true;
            }
            return false;
        }

        private static IEnumerable<ModeInfo> Of(IReadOnlyList<ModeInfo> modes, string kind)
        {
            foreach (ModeInfo m in modes) if (kind == null || m.Kind == kind) yield return m;
        }

        private static long TotalTime(SaveBlob s)
        {
            long t = 0;
            foreach (int v in s.Time.Values) t += v;
            return t;
        }

        private static bool EveryMode(SaveBlob s, IReadOnlyList<ModeInfo> modes)
        {
            if (modes.Count == 0) return false;
            foreach (ModeInfo m in modes)
            {
                bool played = false;
                /* all shows, encores included, as the JS reads s.shows here (not showsDone) */
                foreach (ShowRecord x in s.Shows) if (x.Mode == m.Id) { played = true; break; }
                if (!played) return false;
            }
            return true;
        }

        private static Milestone Venue(string v, string name)
        {
            return new Milestone("venue-" + v, "venue:" + v, name,
                "unlock " + (name.StartsWith("the ", StringComparison.Ordinal) ? name : "the " + name),
                (s, c) => s.Venues.Unlocked.Contains(v));
        }

        /// <summary>milestones.js:13 <c>MILESTONES</c>, in journal order.</summary>
        public static readonly IReadOnlyList<Milestone> All = new[]
        {
            new Milestone("first-show", "ticket", "opening night", "finish your first show",
                (s, c) => CountDone(s) >= 1),
            new Milestone("sold-out", "star", "first sold-out show", "get 9 of 10 right on the first try",
                (s, c) => AnyDone(s, SoldOut)),
            new Milestone("clean-run", "bolt", "ten in a row", "play a whole show with no misses and no hints",
                // Unity (QUESTIONS 10 addendum, an intended difference): no misses and no hints, as the text says. The JS
                // checked first >= n only, which a show with a miss can reach through a clean bonus replay.
                (s, c) => AnyDone(s, x => x.N >= 10 && x.First >= x.N && x.Missed == 0 && x.Hinted == 0)),
            new Milestone("streak-3", "heart", "three-day streak", "play three days in a row",
                (s, c) => s.Streak.Best >= 3),
            new Milestone("streak-5", "lights", "five-day streak", "play five days in a row",
                (s, c) => s.Streak.Best >= 5),
            new Milestone("streak-10", "vinyl", "ten-day streak", "play ten days in a row",
                (s, c) => s.Streak.Best >= 10),
            new Milestone("master-any", "trophy", "mastered a skill", "get 10 right at level 3 in any mode",
                (s, c) => MaxLevel(s, Of(c, null))),
            new Milestone("master-spell", "mic", "spelling headliner", "get 10 right at level 3 in a spelling mode",
                (s, c) => MaxLevel(s, Of(c, ModeInfo.Spelling))),
            new Milestone("master-math", "amp", "math headliner", "get 10 right at level 3 in a math mode",
                (s, c) => MaxLevel(s, Of(c, ModeInfo.Math))),
            new Milestone("every-mode", "bus", "played every stage", "finish a show in every mode",
                EveryMode),
            new Milestone("ten-shows", "setlist", "ten shows", "finish ten shows",
                (s, c) => CountDone(s) >= 10),
            new Milestone("fifty-shows", "guitar", "fifty shows", "finish fifty shows",
                (s, c) => CountDone(s) >= 50),
            new Milestone("hour", "cassette", "an hour of practice", "play for an hour in total",
                (s, c) => TotalTime(s) >= 3600),
            /* one per venue after the first (milestones.js:41) */
            Venue("records", "record store"),
            Venue("rec", "rec center"),
            Venue("rooftop", "rooftop"),
            Venue("club", "the club"),
            Venue("theater", "theater"),
            Venue("festival", "festival"),
            Venue("arena", "the arena"),
        };

        /// <summary>milestones.js:48 <c>milestoneById</c>: the milestone, or null for an unknown id.</summary>
        public static Milestone ById(string id)
        {
            foreach (Milestone m in All) if (m.Id == id) return m;
            return null;
        }
    }
}
