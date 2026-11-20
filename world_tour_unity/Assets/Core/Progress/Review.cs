using System;
using System.Collections.Generic;
using System.Linq;
using WorldTour.Core.Maths.Gen;
using WorldTour.Core.Shows;

namespace WorldTour.Core.Progress
{
    /// <summary>
    /// The math review queue, ported from world_tour/js/review.js. A missed problem is saved whole (prompt,
    /// answer, choices, hints and whatever its mode draws from), so it comes back exactly as she saw it, even
    /// after the generators change. Entries from older saves have only a seed; those are regenerated.
    /// Pure functions on the list.
    /// </summary>
    public static class Review
    {
        /// <summary>review.js:8 REVIEW_MAX.</summary>
        public const int Max = 40;

        /// <summary>
        /// review.js:14 <c>usable(p)</c>: a stored problem still has the parts every mode needs: a string prompt,
        /// an answer object and a steps array.
        /// </summary>
        public static bool Usable(JsonValue p)
        {
            return p != null && p.Kind == JsonKind.Object
                && p["prompt"] != null && p["prompt"].Kind == JsonKind.String
                && p["answer"] != null && (p["answer"].Kind == JsonKind.Object || p["answer"].Kind == JsonKind.Array)
                && p["steps"] != null && p["steps"].Kind == JsonKind.Array;
        }

        /// <summary>review.js:11 <c>same(skill, v)</c>.</summary>
        private static bool Same(ReviewEntry r, string skill, int level, long seed)
        {
            return r.Skill == skill && r.Level == level && r.Seed == seed;
        }

        /// <summary>
        /// review.js:20-31 <c>recordReview(list, skill, v, { clean, missed, keep })</c>: returns the new list (the input
        /// is left alone). A miss adds the problem (a deep copy) if it isn't there, or fills in the problem of an
        /// old seed-only entry; a clean answer removes it unless it was missed earlier this show (keep). Never
        /// longer than <see cref="Max"/>; the oldest go first.
        /// </summary>
        public static List<ReviewEntry> Record(IReadOnlyList<ReviewEntry> list, string skill, MathItem v, bool clean, bool missed, bool keep = false)
        {
            if (v == null) throw new ArgumentNullException(nameof(v));
            int level = v.Level ?? 0;
            var outList = new List<ReviewEntry>(list ?? new ReviewEntry[0]);
            ReviewEntry hit = outList.FirstOrDefault(r => Same(r, skill, level, v.Seed));
            JsonValue prob = v.Prob != null ? v.Prob.ToJson() : null;
            if (missed)
            {
                if (hit == null)
                    outList.Add(new ReviewEntry { Skill = skill, Level = level, Seed = v.Seed, Prob = Usable(prob) ? JsonTree.Clone(prob) : null });
                else if (!Usable(hit.Prob) && Usable(prob))
                {
                    int at = outList.IndexOf(hit);
                    outList[at] = new ReviewEntry { Skill = hit.Skill, Level = hit.Level, Seed = hit.Seed, Prob = JsonTree.Clone(prob) };
                }
            }
            if (clean && !keep) outList = outList.Where(r => !Same(r, skill, level, v.Seed)).ToList();
            if (outList.Count > Max) outList = outList.Skip(outList.Count - Max).ToList();
            return outList;
        }

        /// <summary>
        /// review.js:34 <c>reviewItem(r)</c>: a show item for a saved entry: the stored problem (a fresh copy) if it is
        /// usable, else just the seed so the show regenerates it. A stored problem that is usable by the JS test
        /// but can't be read back as a problem is treated as unusable too (see <see cref="ProblemJson.TryRead"/>).
        /// </summary>
        public static MathItem Item(ReviewEntry r)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            Problem p = null;
            if (Usable(r.Prob)) ProblemJson.TryRead(r.Prob, out p);
            return new MathItem(r.Skill, r.Level, r.Seed, p);
        }
    }
}
