using System;

namespace WorldTour.Game.Ui
{
    /// <summary>An sRGB colour as four bytes. Plain C# so the palette is testable in edit mode.</summary>
    public struct Rgba32 : IEquatable<Rgba32>
    {
        public byte R, G, B, A;

        public Rgba32(byte r, byte g, byte b, byte a = 255) { R = r; G = g; B = b; A = a; }

        /// <summary>"#rrggbb" or "#rrggbbaa" (the leading # is optional). Throws FormatException otherwise.</summary>
        public static Rgba32 Parse(string hex)
        {
            if (hex == null) throw new FormatException("colour is null");
            string h = hex.StartsWith("#", StringComparison.Ordinal) ? hex.Substring(1) : hex;
            if (h.Length != 6 && h.Length != 8) throw new FormatException("colour must be #rrggbb or #rrggbbaa: " + hex);
            byte a = h.Length == 8 ? Byte(h, 6, hex) : (byte)255;
            return new Rgba32(Byte(h, 0, hex), Byte(h, 2, hex), Byte(h, 4, hex), a);
        }

        static byte Byte(string h, int i, string whole)
        {
            int hi = Nibble(h[i]), lo = Nibble(h[i + 1]);
            if (hi < 0 || lo < 0) throw new FormatException("not a hex colour: " + whole);
            return (byte)(hi * 16 + lo);
        }

        static int Nibble(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        public string ToHex() { return "#" + R.ToString("x2") + G.ToString("x2") + B.ToString("x2") + (A == 255 ? "" : A.ToString("x2")); }
        public bool Equals(Rgba32 o) { return R == o.R && G == o.G && B == o.B && A == o.A; }
        public override bool Equals(object o) { return o is Rgba32 && Equals((Rgba32)o); }
        public override int GetHashCode() { return (R << 24) | (G << 16) | (B << 8) | A; }
        public override string ToString() { return ToHex(); }
    }

    /// <summary>The colour roles of world_tour/css/tokens.css. Every colour the UI uses comes from one of these.</summary>
    public enum ColorRole
    {
        /// <summary>--paper: the page.</summary>
        Paper,
        /// <summary>--paper-deep: halftone, accents.</summary>
        PaperDeep,
        /// <summary>--ink: outlines, text, shadows.</summary>
        Ink,
        /// <summary>--card: paper scraps, stickers, sticker borders.</summary>
        Card,
        /// <summary>--on-ink: text sitting on ink.</summary>
        OnInk,
        /// <summary>--accent: lilac.</summary>
        Accent,
        /// <summary>--accent-2: ink blue.</summary>
        Accent2,
        /// <summary>--muted: pencil grey for locked things.</summary>
        Muted,
    }

    /// <summary>One theme: a value per <see cref="ColorRole"/>. Ported from tokens.css.</summary>
    public sealed class Palette
    {
        public readonly string Id;
        readonly Rgba32[] _c;

        public Palette(string id, string paper, string paperDeep, string ink, string card, string onInk, string accent, string accent2, string muted)
        {
            Id = id;
            _c = new[] { Rgba32.Parse(paper), Rgba32.Parse(paperDeep), Rgba32.Parse(ink), Rgba32.Parse(card), Rgba32.Parse(onInk), Rgba32.Parse(accent), Rgba32.Parse(accent2), Rgba32.Parse(muted) };
        }

        public Rgba32 this[ColorRole r] { get { return _c[(int)r]; } }
    }

    /// <summary>The two themes. Ids match the shop's "theme:&lt;id&gt;" items and MetaRules.GetLook().Theme.</summary>
    public static class Palettes
    {
        public const string TourPinkId = "tour-pink";
        public const string PeachNavyId = "peach-navy";

        /// <summary>tokens.css :root (default).</summary>
        public static readonly Palette TourPink = new Palette(TourPinkId,
            paper: "#fdb1cb", paperDeep: "#f98bb0", ink: "#000000", card: "#ffffff", onInk: "#ffffff",
            accent: "#a8a9fc", accent2: "#2f2fc1", muted: "#6b5560");

        /// <summary>tokens.css :root[data-theme="peach-navy"] (unlockable backstage, phase 5).</summary>
        public static readonly Palette PeachNavy = new Palette(PeachNavyId,
            paper: "#ffcfb5", paperDeep: "#ffa47e", ink: "#1c2747", card: "#fffaf5", onInk: "#fffaf5",
            accent: "#9fb3ea", accent2: "#1c2747", muted: "#6d6a78");

        /// <summary>The palette for a theme id; unknown or null ids get the default, as look.js does.</summary>
        public static Palette Get(string id)
        {
            return id == PeachNavyId ? PeachNavy : TourPink;
        }
    }
}
