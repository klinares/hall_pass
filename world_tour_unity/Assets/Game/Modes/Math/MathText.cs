using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WorldTour.Game.Modes
{
    /// <summary>One piece of a math line: plain text (generator markup such as &lt;b&gt;), or a stacked fraction.</summary>
    public struct MathSeg
    {
        /// <summary>The text, when <see cref="IsFrac"/> is false.</summary>
        public string Text;
        public bool IsFrac;
        /// <summary>The whole-number part of a mixed number, or null.</summary>
        public string Whole;
        public string Num, Den;
    }

    /// <summary>
    /// world_tour/modes/math/parts.js <c>rich</c> and <c>fracHTML</c> on TextMeshPro. The generators write fractions as
    /// [[3/4]], [[1 1/2]] and [[2]]; <see cref="ToTmp"/> turns every one into a real stacked fraction (numerator over a
    /// bar over denominator, BRIEF "drawn as real fractions, large and unambiguous") inline in TMP rich text, so prompts
    /// and the dog's lines keep flowing and wrapping. It is built for the body face (Courier Prime, every glyph 0.6 em
    /// wide): the digits are monospaced with &lt;mspace&gt;, the bar is a run of underscores (Courier's "_" overhangs its
    /// advance, so a run joins into one line, a little wider than the digits), and the three rows are moved back over each
    /// other with negative &lt;space&gt; and lifted with &lt;voffset&gt;. The bar is inside &lt;noparse&gt; so
    /// Ui.Blanks.Spread never spaces it out. Stand-alone fractions (the keypad readout, note blocks) are drawn as UI
    /// pieces instead (MathUi.Frac). No UnityEngine here, so it is tested in edit mode.
    /// </summary>
    public static class MathText
    {
        /// <summary>parts.js:11 the rich() pattern.</summary>
        public static readonly Regex Rich = new Regex(@"\[\[(?:(\d+) )?(\d+)\/(\d+)\]\]|\[\[(\d+)\]\]", RegexOptions.CultureInvariant);

        /// <summary>The fraction's digits against the text around it: as big as the text, as on the web (components.css .frac).</summary>
        public const float DefaultScale = 1f;
        /// <summary>The prompt card (math.css:11 .mathq .frac is 1.05em; a little larger here, so it reads at a glance).</summary>
        public const float PromptScale = 1.15f;
        /// <summary>The dog's lines (math.css:14 .bubble .frac is .95em; larger here for the same reason).</summary>
        public const float BubbleScale = 1.1f;
        /// <summary>math.css:13 .setlist .frac .8em (the ticket); 1.1 here, in bold, so the digits read at 1366 (fix round 1, P4).</summary>
        public const float SetlistScale = 1.1f;

        // Courier Prime (64 pt SDF face info, units of one em): digits rise 0.592 from the baseline; the underscore sits
        // from -0.123 to -0.189 (Regular). The bar's centre goes on the math axis, 0.3 em above the baseline (the minus
        // sign's height), with a 0.1 em gap to the digits above and below.
        const double DigitTop = .592, BarTop = -.123, BarBottom = -.189, Axis = .30, Gap = .10, Advance = .6;

        /// <summary>Splits a line into text and fractions (rich()). [[2]] is plain text "2".</summary>
        public static List<MathSeg> Parse(string s)
        {
            var list = new List<MathSeg>();
            s = s ?? "";
            int at = 0;
            foreach (Match m in Rich.Matches(s))
            {
                if (m.Index > at) list.Add(new MathSeg { Text = s.Substring(at, m.Index - at) });
                if (m.Groups[4].Success) list.Add(new MathSeg { Text = m.Groups[4].Value });
                else list.Add(new MathSeg { IsFrac = true, Whole = m.Groups[1].Success ? m.Groups[1].Value : null, Num = m.Groups[2].Value, Den = m.Groups[3].Value });
                at = m.Index + m.Length;
            }
            if (at < s.Length) list.Add(new MathSeg { Text = s.Substring(at) });
            // join neighbouring text pieces ([[2]] next to text)
            var joined = new List<MathSeg>();
            foreach (MathSeg g in list)
            {
                if (!g.IsFrac && joined.Count > 0 && !joined[joined.Count - 1].IsFrac)
                {
                    MathSeg last = joined[joined.Count - 1];
                    last.Text += g.Text;
                    joined[joined.Count - 1] = last;
                }
                else joined.Add(g);
            }
            return joined;
        }

        /// <summary>The line with the markup written out flat: "[[1 1/2]]" becomes "1 1/2" (logs, tests).</summary>
        public static string Plain(string s)
        {
            return Rich.Replace(s ?? "", m => m.Groups[4].Success ? m.Groups[4].Value
                : (m.Groups[1].Success ? m.Groups[1].Value + " " : "") + m.Groups[2].Value + "/" + m.Groups[3].Value);
        }

        /// <summary>rich(): every [[...]] as an inline stacked fraction in TMP rich text; everything else unchanged.</summary>
        private static readonly Regex PairRe = new Regex(@"\((\d+), (\d+)\)", RegexOptions.CultureInvariant);

        /// <summary>
        /// A coordinate pair "(3, 5)" keeps together on one line: TMP's &lt;nobr&gt; (P6). A no-break space didn't hold: the
        /// bundled font has no U+00A0, so TMP drew it as an ordinary, breaking space.
        /// </summary>
        public static string Pairs(string s)
        {
            return s == null ? null : PairRe.Replace(s, "<nobr>($1, $2)</nobr>");
        }

        public static string ToTmp(string s, float scale = DefaultScale)
        {
            s = Pairs(s);
            return Rich.Replace(s ?? "", m => m.Groups[4].Success ? m.Groups[4].Value
                : Stacked(m.Groups[1].Success ? m.Groups[1].Value : null, m.Groups[2].Value, m.Groups[3].Value, scale));
        }

        /// <summary>
        /// One stacked fraction in TMP rich text (fracHTML): the whole number bold (.mixed .whole), a .08 em gap, then the
        /// numerator, bar and denominator in one column, each row centred in it.
        /// </summary>
        public static string Stacked(string whole, string num, string den, float scale = DefaultScale)
        {
            num = num ?? ""; den = den ?? "";
            int k = System.Math.Max(1, System.Math.Max(num.Length, den.Length));
            double f = scale;
            // in em of the fraction's own size
            double vb = Axis / f + (-(BarTop + BarBottom) / 2);
            double vn = vb + BarTop + Gap;
            double vd = vb + BarBottom - Gap - DigitTop;
            double w = k * Advance;
            var sb = new StringBuilder(240);
            sb.Append("<space=0.12em>");                                          // .frac margin 0 .12em
            if (!string.IsNullOrEmpty(whole)) sb.Append("<b>").Append(whole).Append("</b><space=0.08em>");
            sb.Append("<size=").Append(F(f * 100)).Append("%><mspace=0.6em>");
            Row(sb, num, k, vn);
            sb.Append("<space=").Append(F(-w)).Append("em>");
            sb.Append("<voffset=").Append(F(vb)).Append("em><noparse>").Append('_', k).Append("</noparse></voffset>");
            sb.Append("<space=").Append(F(-w)).Append("em>");
            Row(sb, den, k, vd);
            sb.Append("</mspace></size><space=0.12em>");
            return sb.ToString();
        }

        private static void Row(StringBuilder sb, string digits, int k, double v)
        {
            double pad = (k - digits.Length) * Advance / 2;
            if (pad > 0) sb.Append("<space=").Append(F(pad)).Append("em>");
            sb.Append("<voffset=").Append(F(v)).Append("em>").Append(digits).Append("</voffset>");
            if (pad > 0) sb.Append("<space=").Append(F(pad)).Append("em>");
        }

        private static string F(double v)
        {
            return v.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
