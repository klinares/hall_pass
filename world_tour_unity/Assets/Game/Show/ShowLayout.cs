using UnityEngine;

namespace WorldTour.Game.Show
{
    /// <summary>
    /// Phase 8.1 (stage show): how the show screen and the math modes lay themselves out. The 2D game never sets
    /// <see cref="Compact"/>, so every screen there is built exactly as before. The 3D world sets it while a gig or a show is on, so
    /// the screen fits a panel docked to the right of the view (the lit stage or the street stays visible beside it): a slimmer top bar,
    /// no crowd strip or confetti (the 3D crowd and lights carry the feedback), one column of controls and no big dog (his lines
    /// show as a bubble beside the 3D dog).
    /// </summary>
    public static class ShowLayout
    {
        /// <summary>True while the world hosts the show. Read when a screen or a song is built, so set it before opening one.</summary>
        public static bool Compact { get; set; }

        /// <summary>The size the compact panel lays its screens out in (set by the host when it docks the panel; zero = unknown, no fit guard).</summary>
        public static Vector2 PanelSize { get; set; }

        /// <summary>The compact panel's rendered width in canvas units (about 48% of 1366), and the gap around it.</summary>
        public const float PanelWidth = 660f, PanelMargin = 12f;
        /// <summary>The panel's content is laid out larger and drawn at this scale (the 18.4 px prompt then lands at 16 px).</summary>
        public const float PanelScale = 0.87f;

        /// <summary>The compact panel's width in canvas units for a canvas this wide (never more than half of it).</summary>
        public static float WidthFor(float canvasWidth) { return Mathf.Min(PanelWidth, Mathf.Floor(canvasWidth * 0.5f) - PanelMargin); }

        /// <summary>The width the screens are laid out in: the drawn width divided by the scale.</summary>
        public static float LayoutWidth(float canvasWidth) { return WidthFor(canvasWidth) / PanelScale; }

        /// <summary>The height the screens are laid out in for a canvas this tall.</summary>
        public static float LayoutHeight(float canvasHeight) { return (canvasHeight - 2f * PanelMargin) / PanelScale; }

        /// <summary>Gutter of the compact screens (the show bar and the play area).</summary>
        public const float Gutter = 16f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Compact = false; PanelSize = Vector2.zero; }
    }
}
