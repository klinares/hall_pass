using UnityEngine;
using UnityEngine.UI;

namespace WorldTour.Game.Ui
{
    /// <summary>
    /// One full screen in Main (map, venue card, show, ticket, backstage, journal, office). Subclass it per screen and
    /// make it a prefab; <see cref="ScreenStack"/> instantiates, shows and removes it. Named GameScreen so it never
    /// hides UnityEngine.Screen.
    /// </summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public class GameScreen : MonoBehaviour
    {
        [Tooltip("Gets keyboard focus whenever this screen comes to the top, so Enter works at once.")]
        [SerializeField] Selectable firstSelected;

        /// <summary>The stack this screen is on (set before <see cref="OnPushed"/>).</summary>
        public ScreenStack Stack { get; internal set; }

        public RectTransform Rect { get { return (RectTransform)transform; } }

        /// <summary>Tab visits the HUD's controls (sound toggle) after every control of this screen, not by position.</summary>
        public virtual bool HudControlsLast { get { return false; } }

        public CanvasGroup Group { get { return GetComponent<CanvasGroup>(); } }

        public Selectable FirstSelected { get { return firstSelected; } set { firstSelected = value; } }

        /// <summary>Once, right after it is created and placed on the stack.</summary>
        protected internal virtual void OnPushed() { }

        /// <summary>Each time it becomes the top screen (after a push, or when the screen above it is popped). It takes input from this moment.</summary>
        protected internal virtual void OnShown() { }

        /// <summary>Each time it stops being the top screen (covered or removed). Input is already off.</summary>
        protected internal virtual void OnHidden() { }

        /// <summary>Once, just before it is destroyed (popped or replaced).</summary>
        protected internal virtual void OnRemoved() { }
    }
}
