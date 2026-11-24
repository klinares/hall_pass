using System.Collections.Generic;
using UnityEngine;

namespace WorldTour.World
{
    /// <summary>
    /// A raised area on an otherwise flat ground (the stage deck). The walking code keeps every walker's root at y = 0, so a
    /// <see cref="StandOnHeight"/> on the figure lifts only the model: full <see cref="height"/> inside the box (centre = this
    /// transform, half sizes in x and z), and a straight ramp of <see cref="rampLength"/> metres down the south (-z) side.
    /// No Physics.
    /// </summary>
    public class HeightZone : MonoBehaviour
    {
        public static readonly List<HeightZone> All = new List<HeightZone>();
        public float height = 0.5f, rampLength = 0.9f;
        public Vector2 halfSize = new Vector2(4.5f, 3f);

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public static float Sample(Vector3 p)
        {
            float h = 0f;
            for (int i = 0; i < All.Count; i++) h = Mathf.Max(h, All[i].At(p));
            return h;
        }

        float At(Vector3 p)
        {
            Vector3 c = transform.position;
            if (Mathf.Abs(p.x - c.x) > halfSize.x || p.z > c.z + halfSize.y) return 0f;
            float front = c.z - halfSize.y;
            if (p.z >= front) return height;
            float t = (front - p.z) / Mathf.Max(0.01f, rampLength);
            return t >= 1f ? 0f : height * (1f - t);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position + Vector3.up * height * 0.5f, new Vector3(halfSize.x * 2f, height, halfSize.y * 2f));
        }
    }

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
