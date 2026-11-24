using UnityEngine;

namespace WorldTour.World
{
    /// <summary>
    /// Slides the picture sideways inside the camera (a lens shift, done on the projection matrix) so what the camera looks at lands in the middle of
    /// the part of the screen a docked panel leaves open, instead of under the panel (phase 8.1 stage show). 0 = off: the projection is the camera's own.
    /// <see cref="Target"/> is the shift as a fraction of the screen width: 0.25 puts the aim point at the centre of the left half.
    /// It eases in and out (or jumps under reduce motion), and it re-applies every frame after anything else moved the lens.
    /// </summary>
    [DefaultExecutionOrder(500)]
    [RequireComponent(typeof(Camera))]
    public sealed class PanelLensShift : MonoBehaviour
    {
        /// <summary>Where the shift should end up. The panel's width over the screen width puts the aim point in the middle of the open part.</summary>
        public float Target;
        /// <summary>True: jump instead of easing (reduce motion).</summary>
        public bool Instant;
        public float Current { get; private set; }

        Camera _cam;
        bool _applied;

        void Awake() { _cam = GetComponent<Camera>(); }

        void LateUpdate()
        {
            if (_cam == null) return;
            if (Instant) Current = Target;
            else Current = Mathf.MoveTowards(Current, Target, Time.unscaledDeltaTime * 2.6f * Mathf.Max(0.2f, Mathf.Abs(Target - Current) + 0.15f));
            if (Mathf.Abs(Current) < 0.0005f && Mathf.Abs(Target) < 0.0005f)
            {
                if (_applied) { _cam.ResetProjectionMatrix(); _applied = false; }
                return;
            }
            _cam.ResetProjectionMatrix();
            Matrix4x4 m = _cam.projectionMatrix;
            m.m02 = Current * 2f;   // clip space is 2 wide: a fraction of the screen width is twice that
            _cam.projectionMatrix = m;
            _applied = true;
        }

        void OnDisable() { if (_cam != null && _applied) { _cam.ResetProjectionMatrix(); _applied = false; } }
    }
}
