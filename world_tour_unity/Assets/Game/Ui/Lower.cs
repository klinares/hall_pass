using TMPro;

namespace WorldTour.Game.Ui
{
    /// <summary>
    /// All display text is lowercase (base.css body text-transform:lowercase). Set text through these helpers so
    /// nothing capitalised reaches the screen. Typed input is the exception, as in the web game (input text-transform:none).
    /// </summary>
    public static class Lower
    {
        /// <summary>The lowercase form of <paramref name="s"/> (invariant culture, so a Turkish locale can't turn "I" into a dotless i). Null becomes "".</summary>
        public static string Of(string s)
        {
            return string.IsNullOrEmpty(s) ? "" : s.ToLowerInvariant();
        }
    }

    /// <summary>TextMeshPro helpers that lowercase what they set.</summary>
    public static class TextExt
    {
        /// <summary>Sets <paramref name="text"/> lowercased. Use this for every label.</summary>
        public static void SetLower(this TMP_Text label, string text)
        {
            if (label == null) return;
            label.text = Lower.Of(text);
        }
    }
}
