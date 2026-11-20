namespace WorldTour.Core.Maths.Gen
{
    /// <summary>
    /// The non-ASCII characters the generators write, built from their code points so every source file
    /// stays plain ASCII.
    /// </summary>
    public static class Glyph
    {
        /// <summary>U+00D7 multiplication sign.</summary>
        public static readonly string Times = ((char)0x00D7).ToString();
        /// <summary>U+00F7 division sign.</summary>
        public static readonly string Divide = ((char)0x00F7).ToString();
        /// <summary>U+2212 minus sign.</summary>
        public static readonly string Minus = ((char)0x2212).ToString();
        /// <summary>U+00A2 cent sign.</summary>
        public static readonly string Cent = ((char)0x00A2).ToString();
        /// <summary>U+2019 right single quotation mark, the apostrophe in "what's".</summary>
        public static readonly string Apos = ((char)0x2019).ToString();
        /// <summary>U+2026 horizontal ellipsis.</summary>
        public static readonly string Ellipsis = ((char)0x2026).ToString();
    }
}
