namespace WorldTour.Game.Ui
{
    /// <summary>
    /// Sizes and timings from world_tour/css/tokens.css and components.css, in canvas units. The canvas reference is
    /// 1366x768, where one unit is one CSS pixel of the web game in a 1366-wide window, so CSS px carry over 1:1
    /// (rem = 16 px; clamp() values are taken at 1366 px wide).
    /// </summary>
    public static class Tokens
    {
        // ---- shape ----
        /// <summary>--bw: outline width.</summary>
        public const float BorderWidth = 3f;
        /// <summary>The white sticker pad around a sticker's art or face (web wt-stk filter, about 3 px at UI size).</summary>
        public const float StickerPad = 4f;
        /// <summary>--shadow: 6px 6px 0 ink (scraps).</summary>
        public const float Shadow = 6f;
        /// <summary>--shadow-md: 4px 4px 0 (buttons, polaroids).</summary>
        public const float ShadowMd = 4f;
        /// <summary>--shadow-sm: 3px 3px 0 (chips, tiles, bubbles).</summary>
        public const float ShadowSm = 3f;
        /// <summary>The pressed shadow (.btn:active box-shadow 1px 1px).</summary>
        public const float ShadowPressed = 1f;
        /// <summary>--gutter: clamp(16px, 4vw, 32px), 32 at 1366 wide.</summary>
        public const float Gutter = 32f;
        public const float GutterMin = 16f;
        /// <summary>--tap: smallest hit target.</summary>
        public const float Tap = 48f;
        /// <summary>.btn min-height and horizontal padding.</summary>
        public const float ButtonMinHeight = 56f;
        public const float ButtonPadX = 24f;
        /// <summary>.btn.sm min-height (= Tap) and padding.</summary>
        public const float ButtonSmPadX = 16f;
        /// <summary>.btn border-radius 14px 18px 14px 20px; the sprite uses one radius.</summary>
        public const float ButtonRadius = 16f;
        /// <summary>.row gap.</summary>
        public const float RowGap = 12f;
        /// <summary>:focus-visible outline: 3px accent-2, offset 3px.</summary>
        public const float FocusWidth = 3f;
        public const float FocusOffset = 3f;

        // ---- type (px) ----
        /// <summary>body font-size.</summary>
        public const float FontBody = 17f;
        public const float Fs0 = 12.8f;   // .8rem
        public const float Fs1 = 16f;     // 1rem
        public const float Fs2 = 20f;     // 1.25rem
        public const float Fs3 = 33.6f;   // clamp(1.6rem,5vw,2.1rem) at 1366
        public const float Fs4 = 57.6f;   // clamp(2.4rem,9vw,3.6rem) at 1366
        public const float Fs5 = 86.4f;   // clamp(3rem,13vw,5.4rem) at 1366
        /// <summary>.btn font-size 1.75rem (display face).</summary>
        public const float FontButton = 28f;
        /// <summary>.btn.sm font-size 1.35rem.</summary>
        public const float FontButtonSm = 21.6f;

        // ---- tilt (paper.css .tilt-1..4) ----
        public const float TiltMin = 1.2f;
        public const float TiltMax = 2.4f;

        // ---- motion (seconds; all under 0.4) ----
        /// <summary>--t-fast.</summary>
        public const float TFast = 0.12f;
        /// <summary>--t-med.</summary>
        public const float TMed = 0.24f;
        /// <summary>--t-slow.</summary>
        public const float TSlow = 0.36f;
        /// <summary>.btn:active transition-duration.</summary>
        public const float TPress = 0.06f;
        /// <summary>Hard ceiling from the brief: every animation is shorter than this.</summary>
        public const float MaxAnim = 0.4f;
    }
}
