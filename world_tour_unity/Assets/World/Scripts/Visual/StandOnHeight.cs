using UnityEngine;

namespace WorldTour.World
{
    /// <summary>Lifts this transform (a figure's model, child of the walking root) onto the <see cref="HeightZone"/> under its parent.</summary>
    public class StandOnHeight : MonoBehaviour
    {
        void LateUpdate()
        {
            Vector3 basePos = transform.parent != null ? transform.parent.position : Vector3.zero;
            Vector3 lp = transform.localPosition;
            float h = HeightZone.Sample(basePos);
            if (!Mathf.Approximately(lp.y, h)) { lp.y = h; transform.localPosition = lp; }
        }
    }
}
