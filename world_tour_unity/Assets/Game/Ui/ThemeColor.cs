using UnityEngine;
using UnityEngine.UI;

namespace WorldTour.Game.Ui
{
    /// <summary>Paints a Graphic (Image, TMP text) with a palette role and repaints when the theme changes.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Graphic))]
    public sealed class ThemeColor : MonoBehaviour
    {
        public ColorRole role = ColorRole.Paper;
        [Range(0f, 1f)] public float alpha = 1f;

        Graphic _g;

        void OnEnable()
        {
            _g = GetComponent<Graphic>();
            Theme.Changed += Paint;
            Paint();
        }

        void OnDisable() { Theme.Changed -= Paint; }

        void OnValidate() { if (_g != null) Paint(); }

        /// <summary>
        /// Adds a ThemeColor painted with <paramref name="role"/> at once. Use this when building UI at runtime:
        /// AddComponent paints in OnEnable with the default role, before a role set afterwards could take effect.
        /// </summary>
        public static ThemeColor Add(GameObject go, ColorRole role, float alpha = 1f)
        {
            var tc = go.AddComponent<ThemeColor>();
            tc.role = role;
            tc.alpha = alpha;
            tc.Paint();
            return tc;
        }

        public void Paint()
        {
            if (_g == null) return;
            Color c = Theme.Color(role);
            c.a *= alpha;
            _g.color = c;
        }
    }
}
