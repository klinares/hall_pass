using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WorldTour.Game.Ui
{
    /// <summary>Text roles: display = Caveat Brush (brush script), body and numbers = Courier Prime (typewriter).</summary>
    public enum FontRole { Display, Body, BodyBold }

    /// <summary>Art roles the UI asks for by name. The PNGs come from task 2.1 (Assets/Art/Sprites/decor/), never drawn in code.</summary>
    public enum SpriteRole
    {
        /// <summary>Torn paper scrap, 9-sliced (paper.css .torn).</summary>
        PaperPanel,
        /// <summary>Washi tape strip (.tape).</summary>
        Tape,
        /// <summary>Polaroid frame (.polaroid), 9-sliced.</summary>
        PolaroidFrame,
        /// <summary>Halftone dot patch (.halftone / body::before).</summary>
        Halftone,
        /// <summary>Ticket stub with perforation (.perf).</summary>
        TicketStub,
        /// <summary>Rounded rectangle for button faces, rings and shadows, 9-sliced. Falls back to uGUI's built-in UISprite.</summary>
        RoundedRect,
    }

    /// <summary>
    /// The live theme: which palette is on, and the asset with fonts and sprites. Components that colour themselves
    /// (<see cref="ThemeColor"/>, <see cref="StickerButton"/>) listen to <see cref="Changed"/>, as look.js re-applies tokens.
    /// </summary>
    public static class Theme
    {
        static ThemeAsset s_Asset;
        static bool s_Loaded;

        public static Palette Palette { get; private set; } = Palettes.TourPink;

        /// <summary>Raised after <see cref="Use"/> switches palettes.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_Asset = null; s_Loaded = false; Palette = Palettes.TourPink; Changed = null; }

        /// <summary>The theme asset from Resources, or null if the UiBuilder script hasn't made it (callers fall back to TMP defaults).</summary>
        public static ThemeAsset Asset
        {
            get
            {
                if (!s_Loaded) { s_Loaded = true; s_Asset = Resources.Load<ThemeAsset>(ThemeAsset.ResourcePath); }
                return s_Asset;
            }
        }

        /// <summary>Switches to the palette for a theme id ("tour-pink", "peach-navy"); unknown ids get tour-pink.</summary>
        public static void Use(string id)
        {
            Palette p = Palettes.Get(id);
            if (p == Palette) return;
            Palette = p;
            Changed?.Invoke();
        }

        public static Color Color(ColorRole r) { return ToColor(Palette[r]); }

        public static Color ToColor(Rgba32 c) { return new Color32(c.R, c.G, c.B, c.A); }

        public static TMP_FontAsset Font(FontRole r) { return Asset != null ? Asset.Font(r) : null; }

        /// <summary>The decor sprite for a role, in the live theme (ThemeSkin's twin when the theme has one).</summary>
        public static Sprite Sprite(SpriteRole r) { return Asset != null ? ThemeSkin.For(Asset.Sprite(r)) : null; }

        /// <summary>Applies a font role to a label (leaves the TMP default when 2.1's fonts aren't in yet).</summary>
        public static void ApplyFont(TMP_Text label, FontRole r)
        {
            if (label == null) return;
            TMP_FontAsset f = Font(r);
            if (f != null) label.font = f;
        }
    }

}
