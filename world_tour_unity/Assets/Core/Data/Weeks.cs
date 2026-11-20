using System;
using System.Collections.Generic;

namespace WorldTour.Core.Data
{
    /// <summary>
    /// StreamingAssets/weeks.json: <c>{ "weeks": [ ... ] }</c>, newest week first, with the same fields as
    /// world_tour/data/weeks.js. The wrapper object leaves room for a later field and is the shape JsonUtility
    /// could read, but the game fills these classes from the strict tree (<see cref="DataFiles.LoadWeeks"/>), not by reflection.
    /// </summary>
    [Serializable]
    public class WeeksFile
    {
        public Week[] weeks;

        /// <summary>
        /// The meaning checks (the file already parsed and its keys and types checked). Rules come from the web game:
        /// world_tour/tools/content_review.mjs (blank count, give-aways, syllables), the modes that read each field
        /// (lyric.js, prefix.js, parts.js) and the week picker (screens/venue.js). Mirrored in Tools/validate_data.mjs.
        /// Each week stands alone: a week with an error is skipped and the rest still load (DECISIONS, 02:30).
        /// </summary>
        public List<Finding> Validate(string file = "weeks.json") => Check(file, null, out _);

        /// <summary>
        /// Validates week by week and splits them into the weeks the game can use (<paramref name="kept"/>, file order)
        /// and the ones it skips. <paramref name="rawSkipped"/>[i], when set, is the name of a week the key and type checks
        /// already refused; it is skipped without further checks. Ends with one plain line naming the skipped weeks, or an
        /// error when no week is left.
        /// </summary>
        internal List<Finding> Check(string file, string[] rawSkipped, out Week[] kept)
        {
            var f = new List<Finding>();
            var keptList = new List<Week>();
            kept = keptList.ToArray();
            if (weeks == null || weeks.Length == 0)
            {
                f.Add(new Finding(Severity.Error, file, 0, 0, "there are no weeks in the file"));
                return f;
            }
            var keptIds = new HashSet<string>(StringComparer.Ordinal);
            var skipped = new List<string>();
            for (int i = 0; i < weeks.Length; i++)
            {
                if (rawSkipped != null && rawSkipped[i] != null) { skipped.Add(rawSkipped[i]); continue; }
                Week wk = weeks[i];
                if (wk == null) { f.Add(new Finding(Severity.Error, file, 0, 0, "week #" + (i + 1) + " is empty")); skipped.Add(SkipName("week #" + (i + 1), 0)); continue; }
                if (CheckWeek(f, file, wk, i, keptIds)) { keptList.Add(wk); keptIds.Add(wk.id); }
                else skipped.Add(SkipName(WeekName(wk, i), wk.Line));
            }
            if (keptList.Count == 0) f.Add(new Finding(Severity.Error, file, 0, 0, "none of the weeks can be used, so the game has no words to show"));
            else if (skipped.Count > 0)
                f.Add(new Finding(Severity.Warning, file, 0, 0, "the game skips " + string.Join(", ", skipped) + " until " + (skipped.Count == 1 ? "it is" : "they are") + " fixed; the other weeks load"));
            kept = keptList.ToArray();
            return f;
        }

        /// <summary>
        /// One week's checks. True when it has no error. A repeated id is checked against the weeks already kept, so the
        /// copy higher in the file wins, as the web game's <c>WEEKS.find(w =&gt; w.id === id)</c> (show.js:13) picks it.
        /// </summary>
        private static bool CheckWeek(List<Finding> f, string file, Week wk, int i, HashSet<string> keptIds)
        {
            int before = ErrorCount(f);
            string at = WeekName(wk, i);
            int line = wk.Line;
            if (Blank(wk.id)) f.Add(new Finding(Severity.Error, file, line, 0, at + ": the id is missing; it should be the test date, like 2026-11-23"));
            else if (!WeekRules.IsDateShape(wk.id)) f.Add(new Finding(Severity.Error, file, line, 0, at + ": the id \"" + wk.id + "\" should be the test date written as YYYY-MM-DD, like 2026-11-23"));
            else if (!WeekRules.IsRealDate(wk.id)) f.Add(new Finding(Severity.Error, file, line, 0, at + ": the id \"" + wk.id + "\" is not a real date"));
            else if (keptIds.Contains(wk.id)) f.Add(new Finding(Severity.Error, file, line, 0, at + ": a week higher in the file already uses the id " + wk.id + "; each week needs its own date"));
            if (Blank(wk.label)) f.Add(new Finding(Severity.Error, file, line, 0, at + ": the label is missing; it is the name shown in the week menu"));
            if (wk.words == null || wk.words.Length == 0)
            {
                f.Add(new Finding(Severity.Error, file, line, 0, at + ": there are no words in this week"));
                return false;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int j = 0; j < wk.words.Length; j++)
            {
                Word v = wk.words[j];
                if (v == null) { f.Add(new Finding(Severity.Error, file, line, 0, at + ", word #" + (j + 1) + " is empty")); continue; }
                CheckWord(f, file, at + ", " + WordName(v, j), v, seen);
            }
            int withX = WeekRules.WithSentence(wk).Count, withP = WeekRules.WithPrefix(wk).Count;
            if (withX > 0 && withX < WeekRules.MinWordsForMode)
                f.Add(new Finding(Severity.Warning, file, line, 0, at + ": only " + Count(withX) + " a sentence (x), so lyric writer is hidden that week (it needs " + WeekRules.MinWordsForMode + ")"));
            if (withP > 0 && withP < WeekRules.MinWordsForMode)
                f.Add(new Finding(Severity.Warning, file, line, 0, at + ": only " + Count(withP) + " a prefix, so prefix mixer is hidden that week (it needs " + WeekRules.MinWordsForMode + ")"));
            return ErrorCount(f) == before;
        }

        /// <summary>A skipped week's name with its line, so two weeks with one id can be told apart: "week 2026-11-23 (line 14)".</summary>
        internal static string SkipName(string name, int line) => line > 0 ? name + " (line " + line + ")" : name;

        internal static int ErrorCount(List<Finding> f)
        {
            int n = 0;
            foreach (Finding x in f) if (x.IsError) n++;
            return n;
        }

        private static void CheckWord(List<Finding> f, string file, string at, Word v, HashSet<string> seen)
        {
            int line = v.Line;
            bool hasW = !Blank(v.w);
            if (!hasW) f.Add(new Finding(Severity.Error, file, line, 0, at + ": the word (w) is missing"));
            else
            {
                // parts.js:43 and lyric.js:43 compare the typed answer, trimmed and lowercased, to w exactly.
                int bad = TextRules.FirstNonLetter(v.w);
                if (bad >= 0)
                    f.Add(new Finding(Severity.Error, file, line, 0, at + ": the word must be lowercase letters a to z only, or a typed answer can never match it (it has " + TextRules.NameChar(v.w[bad]) + ")"));
                if (!seen.Add(v.w)) f.Add(new Finding(Severity.Error, file, line, 0, at + ": this word is already in this week"));
            }
            if (Blank(v.s)) f.Add(new Finding(Severity.Error, file, line, 0, at + ": the syllables (s) are missing; write them like ex-am-ple"));
            if (Blank(v.h)) f.Add(new Finding(Severity.Error, file, line, 0, at + ": the clue (h) is missing"));
            if (v.x != null)
            {
                // content_review.mjs:53; lyric.js:9 only uses x when it holds ___, and splits it at the blank.
                int blanks = CountBlanks(v.x);
                if (blanks == 0) f.Add(new Finding(Severity.Error, file, line, 0, at + ": the sentence (x) has no ___ to show where the word goes"));
                else if (blanks > 1) f.Add(new Finding(Severity.Error, file, line, 0, at + ": the sentence (x) has " + blanks + " blanks (___); it needs exactly one"));
            }
            if (v.prefix != null)
            {
                // prefix.js:10 skips a prefix with any part missing; prefix.js:96 and :109 show p + root as the word.
                bool p = !Blank(v.prefix.p), root = !Blank(v.prefix.root), means = !Blank(v.prefix.means);
                if (!p) f.Add(new Finding(Severity.Error, file, line, 0, at + ": the prefix is missing \"p\" (the prefix itself, like ir)"));
                if (!root) f.Add(new Finding(Severity.Error, file, line, 0, at + ": the prefix is missing \"root\" (the word it is added to)"));
                if (!means) f.Add(new Finding(Severity.Error, file, line, 0, at + ": the prefix is missing \"means\" (what the prefix adds, like not)"));
                if (p && root && hasW && v.prefix.p + v.prefix.root != v.w)
                    f.Add(new Finding(Severity.Error, file, line, 0, at + ": the prefix \"" + v.prefix.p + "\" plus the root \"" + v.prefix.root + "\" makes \"" + v.prefix.p + v.prefix.root + "\", not \"" + v.w + "\""));
            }
            if (hasW)
            {
                // content_review.mjs:56, 54, 55: flagged for review, the game still plays.
                if (!Blank(v.s) && v.s.Replace("-", "") != v.w)
                    f.Add(new Finding(Severity.Warning, file, line, 0, at + ": the syllables \"" + v.s + "\" don't spell the word"));
                string lw = TextRules.AsciiLower(v.w);
                if (v.x != null && TextRules.AsciiLower(v.x).Contains(lw))
                    f.Add(new Finding(Severity.Warning, file, line, 0, at + ": the sentence (x) gives the word away"));
                if (!Blank(v.h) && TextRules.AsciiLower(v.h).Contains(lw))
                    f.Add(new Finding(Severity.Warning, file, line, 0, at + ": the clue (h) gives the word away"));
            }
        }

        private static string Count(int n) => n == 1 ? "1 word has" : n + " words have";

        private static int CountBlanks(string x)
        {
            // Same as the JS (x.match(/___/g) || []).length: non-overlapping, left to right.
            int n = 0, k = 0;
            while ((k = x.IndexOf("___", k, StringComparison.Ordinal)) >= 0) { n++; k += 3; }
            return n;
        }

        internal static bool Blank(string s) => TextRules.Blank(s);

        /// <summary>"week 2026-10-26", or "week #2" when the id is missing.</summary>
        public static string WeekName(Week wk, int index) => wk != null && !Blank(wk.id) ? "week " + wk.id : "week #" + (index + 1);

        /// <summary>"word \"illegal\"", or "word #3" when w is missing.</summary>
        public static string WordName(Word v, int index) => v != null && !Blank(v.w) ? "word \"" + v.w + "\"" : "word #" + (index + 1);
    }

    [Serializable]
    public class Week
    {
        /// <summary>The test date, YYYY-MM-DD. The newest week on or before today opens by itself.</summary>
        public string id;
        /// <summary>The name in the week menu, like "week of oct 26, 2026".</summary>
        public string label;
        public Word[] words;
        /// <summary>Where the week's { starts in the file, for messages. 0 when built in code.</summary>
        [NonSerialized] public int Line;
    }

    [Serializable]
    public class Word
    {
        /// <summary>The word, lowercase.</summary>
        public string w;
        /// <summary>Syllable chunks joined by hyphens, like "ir-ra-tion-al" (the hint after a second miss).</summary>
        public string s;
        /// <summary>Definition or clue (fan mail and the hints).</summary>
        public string h;
        /// <summary>Optional sentence with ___ where the word goes (lyric writer). Null when absent.</summary>
        public string x;
        /// <summary>Optional prefix breakdown (prefix mixer). Null when absent.</summary>
        public Prefix prefix;
        [NonSerialized] public int Line;
    }

    [Serializable]
    public class Prefix
    {
        public string p;      // the prefix, like "ir"
        public string root;   // the word it is added to, like "rational"
        public string means;  // what the prefix adds, like "not"
    }
}
