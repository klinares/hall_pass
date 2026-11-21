using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WorldTour.Game.Ui
{
    /// <summary>
    /// The art half of a theme switch. Colours already come from the palette tokens (Theme.Color, ThemeColor,
    /// StickerButton); the scrapbook decor sprites (torn paper, tape, bubbles, ticket stub, the map paper) were rendered
    /// from the web CSS in tour pink, so each has a twin rendered with peach + navy on (ThemeAsset.peachDecor). This sweeps
    /// every Image under the scene's canvases and swaps a decor sprite for the current theme's twin: when the theme
    /// changes, for a few frames after the top screen changes (screens build their UI as they open), and every half
    /// second while a theme other than tour pink is on (a screen that rebuilds itself, the map after a resize). Screens
    /// need no change of their own. MainRoot adds one to the Main scene.
    /// </summary>
    public sealed class ThemeSkin : MonoBehaviour
    {
        /// <summary>Logged on every theme switch, for the browser tests: "WT_THEME theme=peach-navy swapped=12".</summary>
        public const string ThemeLog = "WT_THEME";
        const int SweepFrames = 3;
        const float PeriodSeconds = 0.5f;

        static ThemeSkin s_Instance;

        readonly Dictionary<Sprite, Sprite> _toPeach = new Dictionary<Sprite, Sprite>();
        readonly Dictionary<Sprite, Sprite> _toPink = new Dictionary<Sprite, Sprite>();
        readonly List<Image> _images = new List<Image>(512);
        ScreenStack _stack;
        int _frames;
        float _next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_Instance = null; }

        /// <summary>Number of Images swapped by the last sweep (for tests).</summary>
        public int LastSwapped { get; private set; }

        /// <summary>Adds the sweeper to <paramref name="host"/> and follows <paramref name="stack"/>'s screen changes.</summary>
        public static ThemeSkin Attach(GameObject host, ScreenStack stack)
        {
            ThemeSkin t = host.GetComponent<ThemeSkin>();
            if (t == null) t = host.AddComponent<ThemeSkin>();
            t.Follow(stack);
            return t;
        }

        /// <summary>Asks for a sweep over the next few frames (call after building UI outside a screen change).</summary>
        public static void Dirty() { if (s_Instance != null) s_Instance._frames = SweepFrames; }

        /// <summary>The twin for the live theme, or the sprite itself when it has none.</summary>
        public static Sprite For(Sprite s)
        {
            if (s == null || s_Instance == null) return s;
            Sprite t;
            return s_Instance.Map().TryGetValue(s, out t) && t != null ? t : s;
        }

        void Follow(ScreenStack stack)
        {
            if (_stack != null) _stack.TopChanged -= OnTop;
            _stack = stack;
            if (_stack != null) _stack.TopChanged += OnTop;
            _frames = SweepFrames;
        }

        void Awake()
        {
            s_Instance = this;
            Load();
        }

        void OnEnable() { Theme.Changed += OnTheme; }

        void OnDisable() { Theme.Changed -= OnTheme; }

        void OnDestroy()
        {
            if (_stack != null) _stack.TopChanged -= OnTop;
            if (s_Instance == this) s_Instance = null;
        }

        void Load()
        {
            _toPeach.Clear(); _toPink.Clear();
            ThemeAsset a = Theme.Asset;
            if (a == null || a.peachDecor == null) return;
            foreach (ThemeAsset.DecorTwin t in a.peachDecor)
            {
                if (t.tourPink == null || t.peachNavy == null) continue;
                _toPeach[t.tourPink] = t.peachNavy;
                _toPink[t.peachNavy] = t.tourPink;
            }
        }

        Dictionary<Sprite, Sprite> Map() { return Theme.Palette == Palettes.PeachNavy ? _toPeach : _toPink; }

        void OnTop(GameScreen top) { _frames = SweepFrames; }

        void OnTheme()
        {
            Sweep();
            _frames = SweepFrames;
            Debug.Log(ThemeLog + " theme=" + Theme.Palette.Id + " swapped=" + LastSwapped);
        }

        void LateUpdate()
        {
            bool periodic = Theme.Palette != Palettes.TourPink && Time.unscaledTime >= _next;
            if (_frames <= 0 && !periodic) return;
            if (_frames > 0) _frames--;
            Sweep();
        }

        /// <summary>Swaps decor sprites under every root canvas for the live theme's twins.</summary>
        public void Sweep()
        {
            _next = Time.unscaledTime + PeriodSeconds;
            LastSwapped = 0;
            Dictionary<Sprite, Sprite> map = Map();
            if (map.Count == 0) return;
            foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!c.isRootCanvas) continue;
                c.GetComponentsInChildren(true, _images);
                foreach (Image img in _images)
                {
                    Sprite s = img.sprite, t;
                    if (s != null && map.TryGetValue(s, out t) && t != null) { img.sprite = t; LastSwapped++; }
                }
                _images.Clear();
            }
        }
    }
}
