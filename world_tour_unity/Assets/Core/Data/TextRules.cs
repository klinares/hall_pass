using System.Globalization;
using System.Text;

namespace WorldTour.Core.Data
{
    /// <summary>
    /// Text checks for the data files, written with explicit character sets (no culture lowercasing, no string.Trim) so
    /// the C# and Node validators agree by construction, whatever runtime the player uses. Mirrored in Tools/validate_data.mjs.
    /// </summary>
    public static class TextRules
    {
        /// <summary>
        /// The characters JS String.prototype.trim() strips: tab, LF, VT, FF, CR, space, U+00A0, U+1680, U+2000-U+200A,
        /// U+2028, U+2029, U+202F, U+205F, U+3000 and U+FEFF.
        /// </summary>
        public static bool IsSpace(char c) =>
            (c >= 0x09 && c <= 0x0D) || c == 0x20 || c == 0xA0 || c == 0x1680 || (c >= 0x2000 && c <= 0x200A)
            || c == 0x2028 || c == 0x2029 || c == 0x202F || c == 0x205F || c == 0x3000 || c == 0xFEFF;

        /// <summary>Null, empty, or only <see cref="IsSpace"/> characters.</summary>
        public static bool Blank(string s)
        {
            if (s == null) return true;
            foreach (char c in s) if (!IsSpace(c)) return false;
            return true;
        }

        /// <summary>
        /// A spelling word must be lowercase ASCII letters a-z only: the typed answer is trimmed and lowercased, then compared
        /// exactly (parts.js:43), so anything else can never be typed right. None of the 22 checked words needs ' or -.
        /// Returns the index of the first character that isn't allowed, or -1.
        /// </summary>
        public static int FirstNonLetter(string w)
        {
            for (int i = 0; i < w.Length; i++) if (w[i] < 'a' || w[i] > 'z') return i;
            return -1;
        }

        /// <summary>How a character is named in a message: "I" for printable ASCII, "a space", or U+0130.</summary>
        public static string NameChar(char c)
        {
            if (c == ' ') return "a space";
            if (c > 0x20 && c < 0x7F) return "\"" + c + "\"";
            return "U+" + ((int)c).ToString("X4", CultureInfo.InvariantCulture);
        }

        /// <summary>A-Z to a-z, nothing else: for the give-away checks.</summary>
        public static string AsciiLower(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s) sb.Append(c >= 'A' && c <= 'Z' ? (char)(c + 32) : c);
            return sb.ToString();
        }

        /// <summary>
        /// Characters a reader can't see (controls, non-breaking and zero-width spaces, soft hyphen, BOM, line and paragraph
        /// separators). Messages show them as U+XXXX instead of an invisible gap.
        /// </summary>
        public static bool IsInvisible(char c) =>
            c < 0x20 || (c >= 0x7F && c <= 0x9F) || c == 0xA0 || c == 0xAD || c == 0x1680 || c == 0x180E
            || (c >= 0x2000 && c <= 0x200F) || (c >= 0x2028 && c <= 0x202F) || (c >= 0x205F && c <= 0x2064)
            || c == 0x3000 || c == 0xFEFF;

        /// <summary>The text with every <see cref="IsInvisible"/> character written as U+XXXX.</summary>
        public static string Visible(string s)
        {
            if (s == null) return null;
            StringBuilder sb = null;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (!IsInvisible(c)) { sb?.Append(c); continue; }
                if (sb == null) sb = new StringBuilder(s, 0, i, s.Length + 8);
                sb.Append("U+").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
            }
            return sb == null ? s : sb.ToString();
        }
    }
}
