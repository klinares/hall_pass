using System;
using System.Collections.Generic;
using System.Globalization;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Core.Tests
{
    /// <summary>
    /// The places where the Unity generators deliberately differ from the web game (QUESTIONS 8 and 9; the full list
    /// with reasons is IntendedDifferences.md next to this file). The JS-match tests stay exact: for a problem in one
    /// of these shapes, <see cref="ToJsForm"/> puts the web game's wording back into exactly the changed step, and the
    /// result must then hash to the JS vector like every other problem. Nothing else is masked.
    /// </summary>
    internal static class IntendedDifferences
    {
        /// <summary>Shape names, for counting what was restored.</summary>
        public const string FluDecimalPoint = "flu L3 decimal point one place: hint 2 (9.1, 14)";
        public const string FluDecimalPointMore = "flu L3 decimal point 2 or 3 places: hint 2 (14)";
        public const string MdSmallQuotient = "md L2 quotient 2 or 3: hint 1 (8.1)";
        public const string MdQuotientTen = "md L2 quotient 10: hint 1 (8.2)";
        public const string VolOneLayer = "vol L1 one-layer stack: hint 2 (9.2)";

        /// <summary>
        /// Tour budget (ops L3) seeds whose web story shows more than $150 (QUESTIONS 9.4): the Unity story picks again,
        /// so the whole problem differs. The JS problem's hash stands in for it in the digest; Tools/gen_vectors.mjs
        /// --one ops 3 5783871 prints it ("the band has $156. they buy 8 sets of strings at $12 a set."). Found by
        /// scanning every seed the vectors use (3,000 volume-set seeds plus the hashed ones) in the web generator.
        /// </summary>
        public static readonly IReadOnlyDictionary<long, string> OpsStoryJsHashes = new Dictionary<long, string>
        {
            { 5783871, "34bef9a88bd6eaedd00ddb9aca17e8be22cf6a1b5a88f541f0da4c4af5d896f4" }
        };

        private static string S(long n) => n.ToString(CultureInfo.InvariantCulture);
        private static string F(long n) => n.ToString("#,##0", CultureInfo.InvariantCulture);
        private static readonly string Times = " " + Glyph.Times + " ";

        /// <summary>
        /// Puts the web wording back if <paramref name="p"/> is in a changed shape (the problem is changed in place) and
        /// returns the shape's name, or returns null and leaves it alone. Shapes are recognised from the problem itself,
        /// which is the same in both games outside the changed step.
        /// </summary>
        public static string ToJsForm(Problem p)
        {
            if (p.Skill == Fluency.Skill && (p.Kind == "decpow" || p.Kind == "decdiv"))
            {
                bool mul = p.Kind == "decpow";
                string[] ab = p.Label.Split(new[] { mul ? Times : " " + Glyph.Divide + " " }, StringSplitOptions.None);
                if (ab.Length != 2) return null;
                if (ab[1] != "10")
                {
                    // 100 and 1,000: the web hint 2 worked the first x 10 or / 10 and said "keep going" (QUESTIONS 14).
                    Rational v = DecimalText(ab[0]);
                    Rational ten = mul ? Rational.Mul(v, Rational.R(10)) : Rational.Div(v, Rational.R(10));
                    p.Steps[1] = ab[0] + (mul ? Times : " " + Glyph.Divide + " ") + "10 = " + Rational.Dec(ten) + ". keep going" + Glyph.Ellipsis;
                    return FluDecimalPointMore;
                }
                if (mul)
                {
                    Rational xv = DecimalText(ab[0]);
                    p.Steps[1] = "the digits stay " + S(xv.N) + "; only the decimal point moves, one place right.";
                }
                else p.Steps[1] = "the digits stay " + ab[0] + "; the decimal point moves one place left.";
                return FluDecimalPoint;
            }
            if (p.Skill == MultDiv.Skill && p.Kind == "div")
            {
                long q = (long)p.Answer.Value.Number, d = p.Params["d"].Long, n = p.Params["N"].Long;
                string ds = S(d);
                if (q == 10)
                {
                    p.Steps[0] = "how many tens of " + ds + " fit in " + F(n) + "? one ten of " + ds + " is " + ds + Times + "10 = " + F(d * 10) + ".";
                    return MdQuotientTen;
                }
                if (q <= 3)
                {
                    p.Steps[0] = "how many " + ds + "s fit in " + F(n) + "? try a few: " + ds + Times + "2 = " + F(d * 2) + ", " + ds + Times + "3 = " + F(d * 3) + Glyph.Ellipsis;
                    return MdSmallQuotient;
                }
                return null;
            }
            if (p.Skill == Volume.Skill && p.Kind == "count")
            {
                JsonValue b = p.Params["boxes"].Items[0];
                long l = b["l"].Long, w = b["w"].Long, h = b["h"].Long;
                if (h != 1) return null;
                p.Steps[1] = "the top layer is " + S(l) + " by " + S(w) + ": that" + Glyph.Apos + "s " + S(l * w) + " crates. there is 1 layer.";
                return VolOneLayer;
            }
            return null;
        }

        /// <summary>"8.82" as a reduced fraction (441/50), the way the JS held it.</summary>
        private static Rational DecimalText(string s)
        {
            int dot = s.IndexOf('.');
            if (dot < 0) return Rational.R(long.Parse(s, CultureInfo.InvariantCulture));
            string digits = s.Remove(dot, 1);
            long den = 1;
            for (int i = dot + 1; i < s.Length; i++) den *= 10;
            return Rational.R(long.Parse(digits, CultureInfo.InvariantCulture), den);
        }

        /// <summary>The canonical JSON the web game gives for this (skill, level, seed), as far as the C# can rebuild it.</summary>
        public static string JsCanon(string skill, int level, long seed, out string shape)
        {
            Problem p = Generators.Generate(skill, level, seed);
            shape = ToJsForm(p);
            return p.ToCanonicalJson();
        }
    }
}
