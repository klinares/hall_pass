using System.Collections.Generic;
using UnityEngine;

namespace WorldTour.World
{
    /// <summary>A round blocker on the ground (lamp post, bench, stage). No Physics: walkers push out of these circles (and out of <see cref="KeepOutZone"/> boxes).</summary>
    public class Obstacle : MonoBehaviour
    {
        public static readonly List<Obstacle> All = new List<Obstacle>();
        public float radius = 0.5f;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        /// <summary>Moves a ground position out of every obstacle and keep-out box it overlaps, keeping margin metres clear.</summary>
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
            for (int i = 0; i < KeepOutZone.All.Count; i++) p = KeepOutZone.All[i].PushOut(p, margin);
            return p;
        }

        /// <summary>True when a ground point is inside an obstacle circle or a keep-out box, each grown by margin metres (what <see cref="PushOut"/> would move it out of).</summary>
        public static bool IsBlocked(Vector3 p, float margin)
        {
            for (int i = 0; i < All.Count; i++)
            {
                Vector3 c = All[i].transform.position;
                float dx = p.x - c.x, dz = p.z - c.z, min = All[i].radius + margin;
                if (dx * dx + dz * dz < min * min) return true;
            }
            for (int i = 0; i < KeepOutZone.All.Count; i++)
            {
                Rect r = KeepOutZone.All[i].Area;
                if (p.x > r.xMin - margin && p.x < r.xMax + margin && p.z > r.yMin - margin && p.z < r.yMax + margin) return true;
            }
            return false;
        }

        /// <summary>
        /// Where to walk next on the way to <paramref name="goal"/>: the goal itself, or the corner of a keep-out box that goes round it
        /// when the straight line would cross one (the shorter way round, kept 0.6 m clear so the push-out never fights it).
        /// </summary>
        public static Vector3 SteerAround(Vector3 from, Vector3 goal)
        {
            for (int i = 0; i < KeepOutZone.All.Count; i++)
            {
                Vector3 g = KeepOutZone.All[i].SteerAround(from, goal, 0.6f);
                if (g != goal) return g;
            }
            return goal;
        }
    }

    /// <summary>
    /// A box on the ground (centre and size from this transform's position and scale in x and z, like the CrowdArea empty) that walkers cannot enter,
    /// so the crowd is walked round and the way to the stage stays clear of figures. Registers itself; no Physics.
    /// </summary>
    public class KeepOutZone : MonoBehaviour
    {
        public static readonly List<KeepOutZone> All = new List<KeepOutZone>();

        [Tooltip("Extra half size in x and z on top of the transform's scale, metres.")]
        public float grow = 0f;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public Rect Area
        {
            get
            {
                Vector3 c = transform.position, s = transform.lossyScale;
                float hx = Mathf.Abs(s.x) / 2f + grow, hz = Mathf.Abs(s.z) / 2f + grow;
                return new Rect(c.x - hx, c.z - hz, hx * 2f, hz * 2f);
            }
        }

        public Vector3 PushOut(Vector3 p, float margin) { return PushOutOf(Area, p, margin); }

        public Vector3 SteerAround(Vector3 from, Vector3 goal, float margin) { return SteerAroundRect(Area, from, goal, margin); }

        /// <summary>Moves a ground point out of a rectangle (grown by margin) through the nearest edge.</summary>
        public static Vector3 PushOutOf(Rect r, Vector3 p, float margin)
        {
            float x0 = r.xMin - margin, x1 = r.xMax + margin, z0 = r.yMin - margin, z1 = r.yMax + margin;
            if (p.x <= x0 || p.x >= x1 || p.z <= z0 || p.z >= z1) return p;
            float l = p.x - x0, rr = x1 - p.x, b = p.z - z0, t = z1 - p.z;
            float m = Mathf.Min(Mathf.Min(l, rr), Mathf.Min(b, t));
            if (m == l) p.x = x0; else if (m == rr) p.x = x1; else if (m == b) p.z = z0; else p.z = z1;
            return p;
        }

        /// <summary>True when the segment a to b passes through the rectangle (flat, x and z).</summary>
        public static bool SegmentHits(Rect r, Vector3 a, Vector3 b)
        {
            float t0 = 0f, t1 = 1f;
            float dx = b.x - a.x, dz = b.z - a.z;
            return Clip(-dx, a.x - r.xMin, ref t0, ref t1) && Clip(dx, r.xMax - a.x, ref t0, ref t1)
                && Clip(-dz, a.z - r.yMin, ref t0, ref t1) && Clip(dz, r.yMax - a.z, ref t0, ref t1);
        }

        static bool Clip(float p, float q, ref float t0, ref float t1)
        {
            if (Mathf.Abs(p) < 1e-6f) return q >= 0f;
            float t = q / p;
            if (p < 0f) { if (t > t1) return false; if (t > t0) t0 = t; }
            else { if (t < t0) return false; if (t < t1) t1 = t; }
            return true;
        }

        /// <summary>
        /// The goal, or the next corner (margin outside the rectangle) on the shortest way round when the straight line crosses it. The way is the shortest path
        /// over the four corners (a corner is joined to what it can see without crossing the box), so it never flips back and forth near a corner.
        /// </summary>
        public static Vector3 SteerAroundRect(Rect r, Vector3 from, Vector3 goal, float margin)
        {
            Rect big = new Rect(r.xMin - margin, r.yMin - margin, r.width + 2f * margin, r.height + 2f * margin);
            if (!SegmentHits(r, from, goal)) return goal;
            // nodes: 0 = here, 1 to 4 = the corners, 5 = the goal
            Vector3[] n =
            {
                from,
                new Vector3(big.xMin, 0, big.yMin), new Vector3(big.xMax, 0, big.yMin),
                new Vector3(big.xMax, 0, big.yMax), new Vector3(big.xMin, 0, big.yMax),
                goal,
            };
            var dist = new float[6]; var prev = new int[6];
            for (int i = 1; i < 6; i++) { dist[i] = float.MaxValue; prev[i] = -1; }
            for (int pass = 0; pass < 6; pass++)
                for (int a = 0; a < 6; a++)
                {
                    if (dist[a] == float.MaxValue) continue;
                    for (int b = 0; b < 6; b++)
                    {
                        if (a == b || SegmentHits(r, n[a], n[b])) continue;
                        float d = dist[a] + Vector3.Distance(n[a], n[b]);
                        if (d < dist[b] - 1e-4f) { dist[b] = d; prev[b] = a; }
                    }
                }
            if (prev[5] < 0) return goal;
            var path = new System.Collections.Generic.List<int>();
            for (int at = 5; at > 0; at = prev[at]) path.Add(at);
            path.Reverse();
            foreach (int i in path) if (i == 5 || Vector3.Distance(from, n[i]) >= 0.25f) return n[i];   // a corner she is already standing at is passed
            return goal;
        }
    }
}
