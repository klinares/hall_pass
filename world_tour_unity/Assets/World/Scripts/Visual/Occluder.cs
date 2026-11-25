using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WorldTour.World
{
    /// <summary>
    /// Which transparent twin to draw for each opaque material (made in the Editor by the FadeBankBuilder, kept in Resources so the web build keeps the
    /// shader variants of the twins). A fading prop swaps to its twins for the length of the fade, so the fade needs no shader that is not already in the build.
    /// </summary>
    public sealed class FadeBank : ScriptableObject
    {
        public const string ResourcePath = "FadeBank";
        public Material[] sources = new Material[0];
        public Material[] twins = new Material[0];

        Dictionary<Material, Material> _map;
        static FadeBank s_Loaded;
        static bool s_Tried;

        public static FadeBank Load()
        {
            if (!s_Tried) { s_Tried = true; s_Loaded = Resources.Load<FadeBank>(ResourcePath); }
            return s_Loaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_Loaded = null; s_Tried = false; }

        /// <summary>The transparent twin of a material, or null when it has none.</summary>
        public Material TwinOf(Material m)
        {
            if (m == null) return null;
            if (_map == null)
            {
                _map = new Dictionary<Material, Material>();
                for (int i = 0; i < sources.Length && i < twins.Length; i++) if (sources[i] != null && twins[i] != null) _map[sources[i]] = twins[i];
            }
            Material t; return _map.TryGetValue(m, out t) ? t : null;
        }
    }

    /// <summary>
    /// One prop that can stand between the follow camera and the player, and its fade. It goes from drawn to gone over <see cref="Seconds"/> (and back the same way) by drawing its
    /// transparent twin materials with a falling alpha; fully gone it is drawn shadows-only (the street's shadows do not change). A prop with no twin for one of its materials
    /// just switches (the few things the bank does not cover). The state belongs to the renderer, not to a scan, so nothing resets when the camera rescans.
    /// </summary>
    public sealed class OccluderFade
    {
        public const float Seconds = 0.3f;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static MaterialPropertyBlock s_Block;

        public readonly Renderer R;
        readonly ShadowCastingMode _shadow;
        readonly bool _enabled;
        readonly Material[] _orig, _twin;     // _twin is null when some material has no twin
        float _alpha = 1f;                    // 1 drawn, 0 gone
        bool _ghosted, _off;

        public float Target { get; private set; } = 1f;
        public bool Busy { get { return Mathf.Abs(_alpha - Target) > 0.0005f; } }
        public float Alpha { get { return _alpha; } }
        /// <summary>True from the first frame of the fade out until it is back to fully drawn (the prop is not what it normally is).</summary>
        public bool Applied { get { return _ghosted || _off; } }
        public bool HasTwin { get { return _twin != null; } }

        public OccluderFade(Renderer r, FadeBank bank)
        {
            R = r;
            _shadow = r.shadowCastingMode;
            _enabled = r.enabled;
            _orig = r.sharedMaterials;
            if (bank != null && _orig.Length > 0)
            {
                var twin = new Material[_orig.Length];
                bool all = true;
                for (int i = 0; i < _orig.Length; i++) { twin[i] = bank.TwinOf(_orig[i]); if (twin[i] == null) all = false; }
                if (all) _twin = twin;
            }
        }

        /// <summary>Starts the fade toward drawn (true) or gone (false).</summary>
        public void Set(bool visible) { Target = visible ? 1f : 0f; }

        /// <summary>Jumps to the end of the fade now (a scene reset, a preview).</summary>
        public void Snap(bool visible)
        {
            Target = visible ? 1f : 0f;
            _alpha = Target;
            Apply();
        }

        public void Step(float dt)
        {
            if (R == null || !Busy) return;
            _alpha = _twin == null ? Target : Mathf.MoveTowards(_alpha, Target, dt / Seconds);
            Apply();
        }

        void Apply()
        {
            if (R == null) return;
            if (_alpha >= 0.999f)
            {
                _alpha = 1f;
                Restore();
            }
            else if (_alpha <= 0.001f)
            {
                _alpha = 0f;
                if (_twin != null && !_ghosted) { R.sharedMaterials = _twin; _ghosted = true; }
                if (!_off)
                {
                    _off = true;
                    if (_shadow == ShadowCastingMode.Off) R.enabled = false;
                    else R.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                }
            }
            else
            {
                if (_off) { _off = false; R.enabled = _enabled; R.shadowCastingMode = _shadow; }
                if (_twin != null)
                {
                    if (!_ghosted) { R.sharedMaterials = _twin; _ghosted = true; }
                    float a = _alpha * _alpha * (3f - 2f * _alpha);
                    if (s_Block == null) s_Block = new MaterialPropertyBlock();
                    for (int i = 0; i < _twin.Length; i++)
                    {
                        s_Block.Clear();
                        Color c = _twin[i].HasProperty(BaseColor) ? _twin[i].GetColor(BaseColor) : Color.white;
                        c.a = a;
                        s_Block.SetColor(BaseColor, c);
                        R.SetPropertyBlock(s_Block, i);
                    }
                }
            }
        }

        void Restore()
        {
            if (_ghosted)
            {
                R.sharedMaterials = _orig;
                for (int i = 0; i < _orig.Length; i++) R.SetPropertyBlock(null, i);
                _ghosted = false;
            }
            if (_off) { _off = false; R.enabled = _enabled; R.shadowCastingMode = _shadow; }
        }

        /// <summary>Puts the renderer back exactly as it was (it stops being tracked).</summary>
        public void Release()
        {
            if (R == null) return;
            _alpha = 1f; Target = 1f;
            Restore();
        }
    }

    /// <summary>
    /// A tall prop (lamp post, tree) that can stand between the follow camera and the player. While it does it fades out (shadows stay) and it fades back when the way is clear.
    /// <see cref="FollowCamera"/> does the testing for every renderer in the scene (hysteresis and a hold time, so nothing flickers on the edge); this component marks the few
    /// hand-placed props and gives <see cref="FollowCamera.ShowAllOccluders"/> a list to put back.
    /// </summary>
    public class Occluder : MonoBehaviour
    {
        public static readonly List<Occluder> All = new List<Occluder>();

        [Tooltip("Horizontal radius of the thing, metres. 0 = taken from the renderers when it starts.")]
        public float radius;
        [Tooltip("Height of the top, metres. 0 = taken from the renderers when it starts.")]
        public float height;

        OccluderFade[] _fades;
        Vector2 _xz;
        bool _hidden;

        public bool Hidden { get { return _hidden; } }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Start()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            var b = new Bounds(transform.position, Vector3.zero);
            bool first = true;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (first) { b = renderers[i].bounds; first = false; } else b.Encapsulate(renderers[i].bounds);
            }
            _xz = new Vector2(transform.position.x, transform.position.z);
            if (radius <= 0f) radius = Mathf.Min(1.6f, Mathf.Max(0.35f, Mathf.Max(b.extents.x, b.extents.z) * 0.8f));
            if (height <= 0f) height = b.max.y;
        }

        void Update()
        {
            if (_fades == null) return;
            for (int i = 0; i < _fades.Length; i++) _fades[i].Step(Time.unscaledDeltaTime);
        }

        /// <summary>Whether this prop blocks the line from the camera to the head (hysteresis: it needs a clearer gap to come back).</summary>
        public void Test(Vector3 cam, Vector3 head)
        {
            if (_fades == null)
            {
                // made on first use: the follow camera tracks every renderer itself, so a prop nobody tests through here carries no fade state of its own
                var renderers = GetComponentsInChildren<Renderer>(true);
                var bank = FadeBank.Load();
                _fades = new OccluderFade[renderers.Length];
                for (int i = 0; i < renderers.Length; i++) _fades[i] = new OccluderFade(renderers[i], bank);
            }
            Vector2 a = new Vector2(cam.x, cam.z), d = new Vector2(head.x, head.z) - a;
            float len2 = d.sqrMagnitude;
            float t = len2 > 0.0001f ? Mathf.Clamp01(Vector2.Dot(_xz - a, d) / len2) : 0f;
            float dist = (a + d * t - _xz).magnitude;
            float lineY = Mathf.Lerp(cam.y, head.y, t);
            bool between = t > 0.02f && t < 0.985f;
            float r = radius * (_hidden ? 1.35f : 1f);
            bool block = between && dist < r && height > lineY - 0.3f;
            if (block != _hidden) Set(block);
        }

        void Set(bool hide)
        {
            _hidden = hide;
            for (int i = 0; i < _fades.Length; i++) _fades[i].Set(!hide);
        }

        /// <summary>Brings everything back (the stage camera takes over, a scene reset).</summary>
        public void Show() { if (_hidden && _fades != null) Set(false); }
    }
}
