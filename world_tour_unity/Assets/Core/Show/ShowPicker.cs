using System;
using System.Collections.Generic;
using System.Linq;
using WorldTour.Core.Maths;

namespace WorldTour.Core.Shows
{
    /// <summary>
    /// Picking a show's items and the syllable hint, ported from world_tour/js/show.js (<c>pickProblems</c>,
    /// <c>pickWords</c>, <c>chunks</c>) and the <c>shuffle</c> in js/util.js. The JS uses Math.random; here the
    /// random source is passed in (a function returning [0, 1)), so a seeded source picks the same items
    /// as the JS would with that same sequence in place of Math.random.
    /// </summary>
    public static class ShowPicker
    {
        /// <summary>js/util.js <c>shuffle</c>: a shuffled copy, Fisher-Yates from the end. Consumes Count - 1 values.</summary>
        public static List<T> Shuffle<T>(IEnumerable<T> items, Func<double> random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            var b = new List<T>(items);
            for (int i = b.Count - 1; i > 0; i--)
            {
                int j = (int)Math.Floor(random() * (i + 1));
                T t = b[i];
                b[i] = b[j];
                b[j] = t;
            }
            return b;
        }

        /// <summary>
        /// show.js:31-35 <c>pickProblems</c>: the first three review problems for this skill (already turned into
        /// items by the review queue: the stored problem, or the seed to regenerate), then fresh items with a
        /// new seed each and no level yet. <paramref name="review"/> must already be filtered to the skill.
        /// </summary>
        public static List<ShowItem> PickProblems(string skill, IReadOnlyList<MathItem> review, Func<double> random, int n = Scoring.ShowLength)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            var outList = new List<ShowItem>();
            if (review != null)
                foreach (MathItem r in review.Take(Scoring.ReviewPerShow)) outList.Add(r);
            int fresh = n - outList.Count;
            for (int i = 0; i < fresh; i++) outList.Add(new MathItem(skill, null, Rng.NewSeed(random)));
            return outList;
        }

        /// <summary>
        /// show.js:39-48 <c>pickWords</c>: up to n words, missed last time first, then least practiced, then
        /// random, in a shuffled order. <paramref name="pool"/> is the mode's pool for the week (JS
        /// <c>mode.pool(week)</c> or <c>week.words</c>); <paramref name="attempts"/> is the word's saved attempt
        /// count (JS <c>st ? st.a : 0</c>). The sort is stable, as JS Array.prototype.sort is.
        /// </summary>
        public static List<WordItem> PickWords(IReadOnlyList<WordItem> pool, ICollection<string> reviewWords,
            Func<string, int> attempts, Func<double> random, int n = Scoring.ShowLength)
        {
            if (pool == null) throw new ArgumentNullException(nameof(pool));
            if (random == null) throw new ArgumentNullException(nameof(random));
            var shuffled = Shuffle(pool, random);
            var scored = shuffled.Select(v => new
            {
                v,
                k = (reviewWords != null && reviewWords.Contains(v.W) ? 0 : 1) * 1000 + (attempts != null ? attempts(v.W) : 0)
            }).ToList();
            var sorted = scored.OrderBy(x => x.k).ToList();   // OrderBy is stable
            return Shuffle(sorted.Take(Math.Min(n, sorted.Count)).Select(x => x.v), random);
        }

        /// <summary>
        /// show.js:23-26 <c>chunks</c>, held to the spelling hint floor (QUESTIONS 11, an intended difference): the JS
        /// spelled out two-syllable words ("em - pire") and short words ("ju...mp"). See <see cref="SpellingHints.Chunks"/>.
        /// </summary>
        public static string Chunks(string w, string s) => SpellingHints.Chunks(w, s);

        /// <summary>The JS uses the one-character ellipsis (U+2026). Built from its code so the source stays ASCII.</summary>
        public static readonly string Ellipsis = ((char)0x2026).ToString();
    }
}
