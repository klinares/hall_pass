using System.Collections.Generic;
using UnityEngine;

namespace WorldTour.World
{
    /// <summary>A round blocker on the ground (lamp post, bench, stage). No Physics: walkers push out of these circles.</summary>
    public class Obstacle : MonoBehaviour
    {
        public static readonly List<Obstacle> All = new List<Obstacle>();
        public float radius = 0.5f;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        /// <summary>Moves a ground position out of every obstacle it overlaps, keeping margin metres clear.</summary>
        public static Vector3 PushOut(Vector3 p, float margin)
        {
            for (int i = 0; i < All.Count; i++)
            {
                Vector3 c = All[i].transform.position;
                Vector2 d = new Vector2(p.x - c.x, p.z - c.z);
                float min = All[i].radius + margin;
                float m = d.magnitude;
                if (m < min)
                {
                    if (m < 0.0001f) d = Vector2.right; else d /= m;
                    p.x = c.x + d.x * min;
                    p.z = c.z + d.y * min;
                }
            }
            return p;
        }
    }
}
