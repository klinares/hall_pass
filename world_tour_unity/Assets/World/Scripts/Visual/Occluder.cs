using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WorldTour.World
{
    /// <summary>
    /// A tall prop (lamp post, tree) that can stand between the follow camera and the player. While it does, its meshes are not drawn
    /// (they still cast their shadow, so the street does not change), and they come back when the way is clear.
    /// <see cref="FollowCamera"/> asks every registered occluder each frame. No Physics: it is a flat distance test against the camera-to-player line.
    /// </summary>
    public class Occluder : MonoBehaviour
    {
        public static readonly List<Occluder> All = new List<Occluder>();

        [Tooltip("Horizontal radius of the thing, metres. 0 = taken from the renderers when it starts.")]
        public float radius;
        [Tooltip("Height of the top, metres. 0 = taken from the renderers when it starts.")]
        public float height;

        Renderer[] _renderers;
        Vector2 _xz;
        bool _hidden;

        public bool Hidden { get { return _hidden; } }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Start()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            var b = new Bounds(transform.position, Vector3.zero);
            bool first = true;
            foreach (var r in _renderers) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            _xz = new Vector2(transform.position.x, transform.position.z);
            if (radius <= 0f) radius = Mathf.Min(1.6f, Mathf.Max(0.35f, Mathf.Max(b.extents.x, b.extents.z) * 0.8f));
            if (height <= 0f) height = b.max.y;
        }

        /// <summary>True when this prop blocks the line from the camera to the head (hysteresis: it needs a clearer gap to come back).</summary>
        public void Test(Vector3 cam, Vector3 head)
        {
            if (_renderers == null) return;
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
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].shadowCastingMode = hide ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        }

        /// <summary>Brings everything back (the stage camera takes over, a scene reset).</summary>
        public void Show() { if (_hidden && _renderers != null) Set(false); }
    }
}
