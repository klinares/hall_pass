using System;
using UnityEngine;

namespace WorldTour.Game.Meta
{
    /// <summary>
    /// Colour helpers for the screens built in phase 6.2A. The project blends in linear colour space, so a translucent
    /// fill comes out darker than the same CSS rgba() does in the browser (8% white over black shows about 31% grey).
    /// <see cref="Solid"/> pre-computes what the browser shows, in sRGB, as an opaque colour; <see cref="Alpha"/> gives the
    /// alpha that makes a linear blend land on the same luminance when the fill sits over a known background.
    /// </summary>
    public static class MetaColor
    {
        /// <summary>sRGB channel (0..1) to linear.</summary>
        public static float ToLinear(float c)
        {
            return c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
        }

        /// <summary>Relative luminance (WCAG) of an sRGB colour.</summary>
        public static double Luminance(Color c)
        {
            return 0.2126 * ToLinear(c.r) + 0.7152 * ToLinear(c.g) + 0.0722 * ToLinear(c.b);
        }

        /// <summary>WCAG contrast ratio (1..21).</summary>
        public static double Contrast(Color a, Color b)
        {
            double x = Luminance(a), y = Luminance(b);
            return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
        }

        /// <summary>What the browser draws for <paramref name="fg"/> at CSS alpha <paramref name="a"/> over <paramref name="bg"/>: blended in sRGB, opaque.</summary>
        public static Color Solid(Color fg, float a, Color bg)
        {
            return new Color(bg.r + (fg.r - bg.r) * a, bg.g + (fg.g - bg.g) * a, bg.b + (fg.b - bg.b) * a, 1f);
        }

        /// <summary>
        /// The alpha to give a fill (average sRGB level <paramref name="fg"/>) over a background (<paramref name="bg"/>) so a
        /// linear blend looks as the browser's sRGB blend at <paramref name="webAlpha"/> does.
        /// </summary>
        public static float Alpha(float webAlpha, float fg, float bg)
        {
            if (Mathf.Abs(fg - bg) < 1e-4f) return webAlpha;
            float want = bg + (fg - bg) * webAlpha;
            float lb = ToLinear(bg), lf = ToLinear(fg);
            return Mathf.Clamp01((ToLinear(want) - lb) / (lf - lb));
        }

        /// <summary>Alpha() for two colours, by their mean sRGB level.</summary>
        public static float Alpha(float webAlpha, Color fg, Color bg)
        {
            return Alpha(webAlpha, (fg.r + fg.g + fg.b) / 3f, (bg.r + bg.g + bg.b) / 3f);
        }

        /// <summary>
        /// <paramref name="fg"/> moved toward <paramref name="ink"/> in 5% steps until it reads on <paramref name="bg"/> at
        /// <paramref name="min"/>:1 (WCAG AA text is 4.5). A colour that already reads is returned as it is.
        /// </summary>
        public static Color Readable(Color fg, Color bg, Color ink, double min = 4.5)
        {
            Color c = fg;
            for (int i = 0; i <= 20 && Contrast(c, bg) < min; i++) c = Color.Lerp(fg, ink, i * 0.05f);
            c.a = fg.a;
            return c;
        }
    }
}
