using System;
using System.Text.RegularExpressions;

namespace WorldTour.Core.Maths.Gen
{
    /// <summary>
    /// Ported from world_tour/math/gen/guard.js. The bulb gives hint steps 1 and 2; only step 3 (after the
    /// third miss) may state the answer. <see cref="Leaks"/> spots a step that writes "= answer".
    /// <see cref="Guarded"/> re-rolls with the same seeded RNG until no step leaks, so a saved seed still
    /// regenerates the same problem.
    /// </summary>
    public static class Guard
    {
        // JS \s: WhiteSpace plus LineTerminator.
        private const string Ws = @"[\t\n\v\f\r \u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000\ufeff]";

        /// <summary>JS: <c>leaks(p)</c>. Only int, money and decimal answers can leak.</summary>
        public static bool Leaks(Problem p)
        {
            if (p.Answer.Type != AnswerType.Int && p.Answer.Type != AnswerType.Money && p.Answer.Type != AnswerType.Decimal) return false;
            Regex re = LeakPattern(Answers.Show(p.Answer));
            for (int i = 0; i < 2 && i < p.Steps.Count; i++)
                if (re.IsMatch(p.Steps[i])) return true;
            return false;
        }

        /// <summary>JS: <c>new RegExp(`= (&lt;b&gt;)?${ans}(&lt;/b&gt;)?([.,:]|\s|$)`)</c> with $ and . escaped; $ is end of text.</summary>
        public static Regex LeakPattern(string shown) =>
            new Regex("= (<b>)?" + Regex.Escape(shown) + "(</b>)?([.,:]|" + Ws + @"|\z)", RegexOptions.CultureInvariant);

        /// <summary>
        /// JS: <c>guarded(build)(level, seed)</c>. One RNG for every attempt; up to 40 builds, and the last one
        /// is returned even if it leaks.
        /// </summary>
        public static Problem Guarded(Func<int, long, Rng, Problem> build, int level, long seed)
        {
            if (build == null) throw new ArgumentNullException(nameof(build));
            var r = new Rng(seed);
            Problem p = null;
            for (int i = 0; i < 40; i++)
            {
                p = build(level, seed, r);
                if (!Leaks(p)) return p;
            }
            return p;
        }
    }
}
