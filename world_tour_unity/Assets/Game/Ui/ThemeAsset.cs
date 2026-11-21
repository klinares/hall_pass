using TMPro;
using UnityEngine;

namespace WorldTour.Game.Ui
{
    /// <summary>
    /// The one theme asset (Assets/Resources/WorldTour/Theme.asset, made and wired by the UiBuilder Editor script).
    /// Holds references only: fonts, decoration sprites and the greyscale materials. Colours are code (<see cref="Palettes"/>),
    /// so the two themes can never drift from tokens.css.
    /// </summary>
    [CreateAssetMenu(menuName = "World Tour/Theme", fileName = "Theme")]
    public sealed class ThemeAsset : ScriptableObject
    {
        public const string ResourcePath = "WorldTour/Theme";

        [Header("fonts (TMP SDF, made in task 2.1)")]
        public TMP_FontAsset display;
        public TMP_FontAsset body;
        public TMP_FontAsset bodyBold;

        [Header("decor sprites (task 2.1, Assets/Art/Sprites/decor/)")]
        public Sprite paperPanel;
        public Sprite tape;
        public Sprite polaroidFrame;
        public Sprite halftone;
        public Sprite ticketStub;
        public Sprite roundedRect;

        [Header("greyscale (locked venues, unearned stickers)")]
        public Material greyLocked;
        public Material greyUnearned;

        [Header("mascot (task 2.3, Assets/Art/Mascot/DogArt.asset)")]
        public WorldTour.Game.Mascot.DogArt dogArt;

        [Header("ui icons (task 2.1, Assets/Art/Sprites/ui/, white for tinting)")]
        public Sprite iconSound;
        public Sprite iconMute;

        /// <summary>A decor sprite and its peach + navy twin (the same piece rendered from the web CSS with that theme on).</summary>
        [System.Serializable]
        public struct DecorTwin
        {
            public Sprite tourPink;
            public Sprite peachNavy;
        }

        [Header("peach + navy decor (phase 5, Assets/Art/Sprites/decor_peach-navy/, wired by MetaBuilder)")]
        [Tooltip("ThemeSkin swaps every Image showing one of these for its twin when the theme changes.")]
        public System.Collections.Generic.List<DecorTwin> peachDecor = new System.Collections.Generic.List<DecorTwin>();

        public TMP_FontAsset Font(FontRole r)
        {
            switch (r)
            {
                case FontRole.Display: return display;
                case FontRole.BodyBold: return bodyBold != null ? bodyBold : body;
                default: return body;
            }
        }

        public Sprite Sprite(SpriteRole r)
        {
            switch (r)
            {
                case SpriteRole.PaperPanel: return paperPanel;
                case SpriteRole.Tape: return tape;
                case SpriteRole.PolaroidFrame: return polaroidFrame;
                case SpriteRole.Halftone: return halftone;
                case SpriteRole.TicketStub: return ticketStub;
                default: return roundedRect;
            }
        }
    }
}
