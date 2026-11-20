using System.Text;

namespace WorldTour.Game.Ui
{
    /// <summary>
    /// Courier Prime draws "_" wider than its advance (1284 of 1228 units, overhanging each side), so a run like "____"
    /// prints as one solid line and the hint stops saying how many letters are hidden. <see cref="Spread"/> puts TMP
    /// character spacing on every run of two or more underscores, leaving single ones (the "p _ _ d" patterns, already
    /// spaced) and anything inside &lt;noparse&gt; (data) alone.
    /// </summary>
    public static class Blanks
    {
        /// <summary>The extra space after each underscore in a run, in em.</summary>
        public const string Gap = "0.15em";

        public static string Spread(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf("__", System.StringComparison.Ordinal) < 0) return text ?? "";
            var sb = new StringBuilder(text.Length + 32);
            const string Open = "<noparse>", Close = "</noparse>";
            int i = 0;
            while (i < text.Length)
            {
                if (string.CompareOrdinal(text, i, Open, 0, Open.Length) == 0)
                {
                    int end = text.IndexOf(Close, i + Open.Length, System.StringComparison.Ordinal);
                    end = end < 0 ? text.Length : end + Close.Length;
                    sb.Append(text, i, end - i);
                    i = end;
                    continue;
                }
                if (text[i] == '_')
                {
                    int j = i;
                    while (j < text.Length && text[j] == '_') j++;
                    if (j - i >= 2) sb.Append("<cspace=").Append(Gap).Append('>').Append('_', j - i).Append("</cspace>");
                    else sb.Append('_');
                    i = j;
                    continue;
                }
                sb.Append(text[i]);
                i++;
            }
            return sb.ToString();
        }
    }
}
