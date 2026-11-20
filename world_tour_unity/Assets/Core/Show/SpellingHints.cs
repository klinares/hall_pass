using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldTour.Core.Shows
{
    /// <summary>
    /// The spelling twin of the math hint ladder (QUESTIONS 11, an intended difference from the web game): before the
    /// final reveal, no hint in any spelling mode shows the whole word. Every pre-reveal hint leaves at least a third of
    /// the letters hidden, and never fewer than two; the last hidden letters are always hers to find. Missing letters,
    /// missing vowels and remix use <see cref="CanReveal"/> for their letter-by-letter hints; the "sound it out" hint
    /// uses <see cref="Chunks"/>.
    /// </summary>
    public static class SpellingHints
    {
        /// <summary>Letters a pre-reveal hint must leave hidden: a third of the word, rounded up, and at least two.</summary>
        public static int MinHidden(int length) => Math.Max(2, (length + 2) / 3);

        /// <summary>Letters a pre-reveal hint may show, counting from the start: the rest stay hidden.</summary>
        public static int MaxShown(int length) => Math.Max(0, length - MinHidden(length));

        /// <summary>May a letter-by-letter hint show the letter at <paramref name="index"/> (0-based)? Only the first <see cref="MaxShown"/>.</summary>
        public static bool CanReveal(int index, int length) => index >= 0 && index < MaxShown(length);

        /// <summary>
        /// Words this short can't keep two letters hidden once the first letter is shown (the ladder's first step), so
        /// the hints have almost nothing to hide. The data validator warns about them.
        /// </summary>
        public const int ShortWord = 3;

        /// <summary>The first letter and one underscore per other letter: "j___" for jump.</summary>
        public static string FirstAndLength(string w)
        {
            if (string.IsNullOrEmpty(w)) return "";
            return w.Substring(0, 1) + new string('_', w.Length - 1);
        }

        /// <summary>
        /// The "sound it out" hint (show.js chunks), held to the floor. In order, the first of these that leaves at least
        /// <see cref="MinHidden"/> letters hidden:
        /// three or more syllables: the web form, first and last syllable around "___" ("ir - ___ - ___ - al");
        /// two or more syllables: the first syllable, then one underscore per hidden letter, syllable by syllable
        /// ("em - ____", "il - __ - ___");
        /// no syllable split: the web form, first two and last two letters ("st...ng");
        /// otherwise, and always for a word of <see cref="ShortWord"/> letters or fewer: the first letter and the length ("j___").
        /// </summary>
        public static string Chunks(string w, string s)
        {
            w = w ?? "";
            if (w.Length == 0) return "";
            if (w.Length <= ShortWord) return FirstAndLength(w);
            string[] parts = (string.IsNullOrEmpty(s) ? w : s).Split('-');
            int min = MinHidden(w.Length);
            if (parts.Length >= 3 && w.Length - parts[0].Length - parts[parts.Length - 1].Length >= min)
            {
                var bits = new List<string> { parts[0] };
                for (int i = 1; i < parts.Length - 1; i++) bits.Add("___");
                bits.Add(parts[parts.Length - 1]);
                return string.Join(" - ", bits);
            }
            if (parts.Length >= 2 && w.Length - parts[0].Length >= min)
                return parts[0] + " - " + string.Join(" - ", parts.Skip(1).Select(p => new string('_', Math.Max(1, p.Length))));
            if (parts.Length < 2 && w.Length > 4 && w.Length - 4 >= min)
                return w.Substring(0, 2) + ShowPicker.Ellipsis + w.Substring(w.Length - 2);
            return FirstAndLength(w);
        }

        /// <summary>How many of the word's letters a hint text leaves out: the word's length minus the letters it shows.</summary>
        public static int HiddenIn(string word, string hint)
        {
            int shown = 0;
            foreach (char c in hint ?? "") if (c >= 'a' && c <= 'z') shown++;
            return Math.Max(0, (word ?? "").Length - shown);
        }
    }
}
