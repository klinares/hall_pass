using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WorldTour.Game.Motion;

namespace WorldTour.Game.Ui
{
    /// <summary>Which greyed look (screens.css): a locked venue, or a sticker or item not yet earned.</summary>
    public enum GreyStyle
    {
        /// <summary>.venue.locked .vart: grayscale(1) contrast(.7) brightness(1.08), opacity .5.</summary>
        Locked,
        /// <summary>.toearn .stk: grayscale(1), opacity .45.</summary>
        Unearned,
    }

    /// <summary>
    /// Greys out uGUI art with the WorldTour/UI/Greyscale shader instead of grey sprite variants. The two shared
    /// materials live in Resources (Assets/Resources/WorldTour/UiGreyLocked.mat and UiGreyUnearned.mat, made by the
    /// UiBuilder Editor script), which also keeps the shader in the Web build despite shader stripping.
    /// Mask and RectMask2D keep working: uGUI copies the material for stencil use as it does for UI/Default.
    /// </summary>
    public static class Greyscale
    {
        public const string LockedPath = "WorldTour/UiGreyLocked";
        public const string UnearnedPath = "WorldTour/UiGreyUnearned";
        static readonly int GreyId = Shader.PropertyToID("_Grey");

        static Material s_Locked, s_Unearned;
        static readonly Dictionary<Graphic, Material> s_Instances = new Dictionary<Graphic, Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_Locked = null; s_Unearned = null; s_Instances.Clear(); }

        /// <summary>The shared material for a style (from the theme asset, else Resources). Null if neither exists yet.</summary>
        public static Material Shared(GreyStyle style)
        {
            ThemeAsset t = Theme.Asset;
            if (style == GreyStyle.Locked)
            {
                if (s_Locked == null) s_Locked = t != null && t.greyLocked != null ? t.greyLocked : Resources.Load<Material>(LockedPath);
                return s_Locked;
            }
            if (s_Unearned == null) s_Unearned = t != null && t.greyUnearned != null ? t.greyUnearned : Resources.Load<Material>(UnearnedPath);
            return s_Unearned;
        }

        /// <summary>Greys <paramref name="g"/> (on = true) or restores full colour (on = false).</summary>
        public static void Set(Graphic g, bool on, GreyStyle style = GreyStyle.Locked)
        {
            if (g == null) return;
            DropInstance(g);
            g.material = on ? Shared(style) : null;
        }

        /// <summary>
        /// Fades from grey to colour (a venue unlocking): the art gets its own material copy for the length of the tween,
        /// then goes back to the default material. Under reduce motion it switches at once.
        /// </summary>
        public static TweenHandle FadeToColour(Graphic g, float duration = 0.36f, GreyStyle style = GreyStyle.Locked)
        {
            if (g == null) return default;
            Material shared = Shared(style);
            if (shared == null) { g.material = null; return default; }
            DropInstance(g);
            var inst = new Material(shared) { name = shared.name + " (fade)" };
            s_Instances[g] = inst;
            g.material = inst;
            return Tweener.Float(1f, 0f, duration, Ease.Out,
                v => { if (inst != null) inst.SetFloat(GreyId, v); },
                () => { if (g != null) g.material = null; DropInstance(g); },
                owner: g);
        }

        static void DropInstance(Graphic g)
        {
            // ReferenceEquals: a destroyed Graphic still has to leave the table.
            if (ReferenceEquals(g, null) || !s_Instances.TryGetValue(g, out Material m)) return;
            s_Instances.Remove(g);
            if (m != null) Object.Destroy(m);
        }
    }
}
