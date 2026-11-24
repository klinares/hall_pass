using UnityEngine;
using WorldTour.Game.Prefs;

namespace WorldTour.World.Places
{
    /// <summary>
    /// Slow decoration motion for the 8.2 places: a ferris wheel that turns, spotlights that sweep, a marquee that pulses.
    /// Everything holds still under reduce motion. No physics, no allocations per frame.
    /// </summary>
    public sealed class PlaceMotion : MonoBehaviour
    {
        public enum Kind { Spin, Sweep, Pulse }

        public Kind kind = Kind.Sweep;
        /// <summary>Local axis for Spin and Sweep.</summary>
        public Vector3 axis = Vector3.right;
        /// <summary>Spin: degrees a second. Sweep: how fast the swing cycles, in radians a second. Pulse: radians a second.</summary>
        public float speed = 0.6f;
        /// <summary>Sweep: swing in degrees either side of the rest pose. Pulse: scale swing in percent.</summary>
        public float amplitude = 20f;
        public float phase;
        /// <summary>A second axis the sweep also swings on, a quarter turn out of step, in degrees (0 = off).</summary>
        public float crossAmplitude;
        public Vector3 crossAxis = Vector3.up;

        Quaternion _rest;
        Vector3 _restScale;

        void Awake() { _rest = transform.localRotation; _restScale = transform.localScale; }

        void Update()
        {
            if (GameSettings.ReducedMotion) return;
            switch (kind)
            {
                case Kind.Spin:
                    transform.localRotation = _rest * Quaternion.AngleAxis(Time.time * speed + phase, axis);
                    break;
                case Kind.Sweep:
                    float a = Mathf.Sin(Time.time * speed + phase) * amplitude;
                    Quaternion q = _rest * Quaternion.AngleAxis(a, axis);
                    if (crossAmplitude != 0f) q *= Quaternion.AngleAxis(Mathf.Cos(Time.time * speed * 0.83f + phase) * crossAmplitude, crossAxis);
                    transform.localRotation = q;
                    break;
                case Kind.Pulse:
                    transform.localScale = _restScale * (1f + Mathf.Sin(Time.time * speed + phase) * amplitude * 0.01f);
                    break;
            }
        }
    }
}
