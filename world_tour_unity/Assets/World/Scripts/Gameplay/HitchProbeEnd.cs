using UnityEngine;

namespace WorldTour.World.Gameplay
{
    /// <summary>The other half of <see cref="HitchProbe"/>: runs last in the frame and closes its measurement.</summary>
    [DefaultExecutionOrder(32000)]
    public sealed class HitchProbeEnd : MonoBehaviour
    {
        public HitchProbe Owner;
        void LateUpdate() { if (Owner != null) Owner.Finish(); }
    }
}
