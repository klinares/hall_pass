using System.Collections.Generic;

namespace WorldTour.Core.Shows
{
    /// <summary>
    /// One skill's saved record, the shape world_tour/js/progress.js keeps in <c>skills[id]</c>:
    /// <c>{ level: 1, run: 0, missRun: 0, a: 0, c: 0, h: 0, byLevel: { 1: [0, 0], 2: [0, 0], 3: [0, 0] }, mistakes: {} }</c>.
    /// Task 1.5 stores and saves it; the rules that change it are in <see cref="LevelRules"/>.
    /// </summary>
    public sealed class SkillRecord
    {
        public int Level = LevelRules.MinLevel;
        /// <summary>Correct answers in a row (JS <c>run</c>).</summary>
        public int Run;
        /// <summary>Misses in a row (JS <c>missRun</c>).</summary>
        public int MissRun;
        /// <summary>Attempts (JS <c>a</c>).</summary>
        public int A;
        /// <summary>Correct (JS <c>c</c>).</summary>
        public int C;
        /// <summary>Hinted (JS <c>h</c>).</summary>
        public int H;
        /// <summary>Per level: [attempts, correct] (JS <c>byLevel</c>).</summary>
        public Dictionary<int, int[]> ByLevel = new Dictionary<int, int[]>
        {
            { 1, new[] { 0, 0 } }, { 2, new[] { 0, 0 } }, { 3, new[] { 0, 0 } }
        };
        /// <summary>Mistake tag to count (JS <c>mistakes</c>).</summary>
        public Dictionary<string, int> Mistakes = new Dictionary<string, int>();
    }

    /// <summary>
    /// Level up and down, ported from world_tour/js/progress.js (<c>recordSkill</c>, <c>noteMistake</c>):
    /// level 1..3, up after 4 right in a row, down after 2 misses in a row.
    /// </summary>
    public static class LevelRules
    {
        public const int MinLevel = 1;
        public const int MaxLevel = 3;
        /// <summary>progress.js:56 <c>k.run &gt;= 4</c>.</summary>
        public const int UpAfter = 4;
        /// <summary>progress.js:59 <c>k.missRun &gt;= 2</c>.</summary>
        public const int DownAfter = 2;

        /// <summary>
        /// JS <c>recordSkill(id, { correct, hinted, mistake })</c> on the record itself. Returns how the level
        /// moved: 1 up, -1 down, 0 not at all. Moving resets the run that caused it.
        /// </summary>
        public static int Record(SkillRecord k, bool correct, bool hinted = false, string mistake = null)
        {
            int[] lv = Row(k, k.Level);
            k.A++;
            lv[0]++;
            if (hinted) k.H++;
            if (!string.IsNullOrEmpty(mistake)) Count(k, mistake);
            int moved = 0;
            if (correct)
            {
                k.C++;
                lv[1]++;
                k.MissRun = 0;
                k.Run++;
                if (k.Run >= UpAfter && k.Level < MaxLevel) { k.Level++; k.Run = 0; moved = 1; }
            }
            else
            {
                k.Run = 0;
                k.MissRun++;
                if (k.MissRun >= DownAfter && k.Level > MinLevel) { k.Level--; k.MissRun = 0; moved = -1; }
            }
            return moved;
        }

        /// <summary>JS <c>noteMistake(id, tag)</c>: which wrong answer she gave. An empty tag is ignored.</summary>
        public static void NoteMistake(SkillRecord k, string tag)
        {
            if (string.IsNullOrEmpty(tag)) return;
            Count(k, tag);
        }

        private static void Count(SkillRecord k, string tag)
        {
            int n;
            k.Mistakes.TryGetValue(tag, out n);
            k.Mistakes[tag] = n + 1;
        }

        /// <summary>The byLevel row for a level; a record missing one (a hand-edited save) gets it added instead of failing.</summary>
        private static int[] Row(SkillRecord k, int level)
        {
            if (k.ByLevel == null) k.ByLevel = new Dictionary<int, int[]>();
            int[] row;
            if (!k.ByLevel.TryGetValue(level, out row) || row == null || row.Length < 2)
            {
                row = new[] { 0, 0 };
                k.ByLevel[level] = row;
            }
            return row;
        }
    }
}
