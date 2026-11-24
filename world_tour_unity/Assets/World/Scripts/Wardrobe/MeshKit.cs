using System.Collections.Generic;
using UnityEngine;

namespace WorldTour.World.Wardrobe
{
    /// <summary>Small procedural meshes for the wardrobe (a ring, a cone, a dome): built once at run time, a few hundred vertices each.</summary>
    public static class MeshKit
    {
        static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Cache.Clear(); }

        static Mesh Get(string key, System.Func<Mesh> make)
        {
            Mesh m;
            if (Cache.TryGetValue(key, out m) && m != null) return m;
            m = make();
            m.name = key;
            Cache[key] = m;
            return m;
        }

        /// <summary>A ring lying in the XZ plane (axis Y): major radius R, tube radius r; <paramref name="arcDeg"/> under 360 makes an open arc starting on +X.</summary>
        public static Mesh Torus(float R, float r, int segs = 20, int sides = 8, float arcDeg = 360f)
        {
            return Get("wt_torus_" + R + "_" + r + "_" + segs + "_" + sides + "_" + arcDeg, () =>
            {
                var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
                for (int i = 0; i <= segs; i++)
                {
                    float a = i / (float)segs * Mathf.Deg2Rad * arcDeg;
                    Vector3 c = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    for (int j = 0; j <= sides; j++)
                    {
                        float b = j / (float)sides * Mathf.PI * 2f;
                        Vector3 nn = c * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                        v.Add(c * R + nn * r); n.Add(nn);
                    }
                }
                for (int i = 0; i < segs; i++)
                    for (int j = 0; j < sides; j++)
                    {
                        int a = i * (sides + 1) + j, b = (i + 1) * (sides + 1) + j;
                        t.Add(a); t.Add(a + 1); t.Add(b); t.Add(b); t.Add(a + 1); t.Add(b + 1);
                    }
                return Build(v, n, t);
            });
        }

        /// <summary>A short open tube (a band): inner radius <paramref name="radius"/>, wall <paramref name="wall"/> thick, <paramref name="height"/> tall, axis Y, centred on the origin.</summary>
        public static Mesh Tube(float radius, float height, float wall, int segs = 24)
        {
            return Get("wt_tube_" + radius + "_" + height + "_" + wall + "_" + segs, () =>
            {
                var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
                float ro = radius + wall, h = height * 0.5f;
                for (int i = 0; i <= segs; i++)
                {
                    float a = i / (float)segs * Mathf.PI * 2f;
                    Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    v.Add(d * ro + Vector3.up * h); n.Add(d);
                    v.Add(d * ro - Vector3.up * h); n.Add(d);
                    v.Add(d * radius + Vector3.up * h); n.Add(-d);
                    v.Add(d * radius - Vector3.up * h); n.Add(-d);
                    v.Add(d * ro + Vector3.up * h); n.Add(Vector3.up);
                    v.Add(d * radius + Vector3.up * h); n.Add(Vector3.up);
                    v.Add(d * ro - Vector3.up * h); n.Add(Vector3.down);
                    v.Add(d * radius - Vector3.up * h); n.Add(Vector3.down);
                }
                for (int i = 0; i < segs; i++)
                {
                    int a = i * 8, b = (i + 1) * 8;
                    t.Add(a); t.Add(b); t.Add(a + 1); t.Add(a + 1); t.Add(b); t.Add(b + 1);
                    t.Add(a + 2); t.Add(a + 3); t.Add(b + 2); t.Add(a + 3); t.Add(b + 3); t.Add(b + 2);
                    t.Add(a + 4); t.Add(b + 4); t.Add(a + 5); t.Add(a + 5); t.Add(b + 4); t.Add(b + 5);
                    t.Add(a + 6); t.Add(a + 7); t.Add(b + 6); t.Add(a + 7); t.Add(b + 7); t.Add(b + 6);
                }
                return Build(v, n, t);
            });
        }

        /// <summary>A cone, base on the XZ plane (centre at the origin), apex at +Y.</summary>
        public static Mesh Cone(float radius, float height, int segs = 10)
        {
            return Get("wt_cone_" + radius + "_" + height + "_" + segs, () =>
            {
                var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
                float slope = radius / height;
                for (int i = 0; i < segs; i++)
                {
                    float a0 = i / (float)segs * Mathf.PI * 2f, a1 = (i + 1) / (float)segs * Mathf.PI * 2f, am = (a0 + a1) * 0.5f;
                    int k = v.Count;
                    v.Add(new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius));
                    v.Add(new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius));
                    v.Add(new Vector3(0f, height, 0f));
                    Vector3 nn = new Vector3(Mathf.Cos(am), slope, Mathf.Sin(am)).normalized;
                    n.Add(nn); n.Add(nn); n.Add(nn);
                    t.Add(k); t.Add(k + 2); t.Add(k + 1);
                }
                int c = v.Count;
                v.Add(Vector3.zero); n.Add(Vector3.down);
                for (int i = 0; i < segs; i++)
                {
                    float a = i / (float)segs * Mathf.PI * 2f;
                    v.Add(new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius)); n.Add(Vector3.down);
                }
                for (int i = 0; i < segs; i++) { t.Add(c); t.Add(c + 1 + i); t.Add(c + 1 + (i + 1) % segs); }
                return Build(v, n, t);
            });
        }

        /// <summary>The upper half of a sphere (radius, flattened vertically by <paramref name="squash"/>), open at the bottom, centre at the origin.</summary>
        public static Mesh Dome(float radius, float squash = 1f, int segs = 16, int rings = 6)
        {
            return Get("wt_dome_" + radius + "_" + squash + "_" + segs + "_" + rings, () =>
            {
                var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
                for (int r = 0; r <= rings; r++)
                {
                    float el = r / (float)rings * Mathf.PI * 0.5f;
                    for (int s = 0; s <= segs; s++)
                    {
                        float az = s / (float)segs * Mathf.PI * 2f;
                        Vector3 d = new Vector3(Mathf.Cos(el) * Mathf.Cos(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Sin(az));
                        v.Add(new Vector3(d.x * radius, d.y * radius * squash, d.z * radius)); n.Add(d);
                    }
                }
                for (int r = 0; r < rings; r++)
                    for (int s = 0; s < segs; s++)
                    {
                        int a = r * (segs + 1) + s, b = (r + 1) * (segs + 1) + s;
                        t.Add(a); t.Add(b); t.Add(a + 1); t.Add(a + 1); t.Add(b); t.Add(b + 1);
                    }
                return Build(v, n, t);
            });
        }

        /// <summary>A cone for a stage light's beam: apex at the origin opening along +z over <paramref name="length"/> to <paramref name="radius"/>, vertex colour fading out
        /// from the apex (the same shape and fade as the place builders' <c>ConeMesh</c>, so a recoloured beam looks like the original).</summary>
        public static Mesh BeamCone(float length, float radius, Color tint, string key)
        {
            const int seg = 24;
            var v = new List<Vector3>(); var c = new List<Color>(); var tri = new List<int>();
            v.Add(Vector3.zero); c.Add(new Color(tint.r * 0.22f, tint.g * 0.22f, tint.b * 0.22f, 0.24f));
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                v.Add(new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, length));
                c.Add(new Color(tint.r * 0.1f, tint.g * 0.1f, tint.b * 0.1f, 0f));
            }
            for (int i = 0; i < seg; i++) { tri.Add(0); tri.Add(1 + i); tri.Add(1 + (i + 1) % seg); }
            var m = new Mesh { name = key };
            m.SetVertices(v); m.SetColors(c); m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>A puffy five-point star facing +z (flat shaded): points at <paramref name="outer"/>, notches at <paramref name="inner"/>, centre puffed out by <paramref name="puff"/>.</summary>
        public static Mesh Star(float outer, float inner, float puff)
        {
            return Get("wt_star_" + outer + "_" + inner + "_" + puff, () =>
            {
                var ring = new Vector3[10];
                for (int i = 0; i < 10; i++)
                {
                    float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f, r = i % 2 == 0 ? outer : inner;
                    ring[i] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
                }
                var v = new List<Vector3>(); var t = new List<int>();
                Vector3 front = new Vector3(0f, 0f, puff), back = new Vector3(0f, 0f, -puff);
                for (int i = 0; i < 10; i++)
                {
                    Vector3 p0 = ring[i], p1 = ring[(i + 1) % 10];
                    int k = v.Count;
                    v.Add(front); v.Add(p0); v.Add(p1); t.Add(k); t.Add(k + 1); t.Add(k + 2);
                    k = v.Count;
                    v.Add(back); v.Add(p1); v.Add(p0); t.Add(k); t.Add(k + 1); t.Add(k + 2);
                }
                var m = new Mesh();
                m.SetVertices(v); m.SetTriangles(t, 0);
                m.RecalculateNormals(); m.RecalculateBounds();
                return m;
            });
        }

        static Mesh Build(List<Vector3> v, List<Vector3> n, List<int> t)
        {
            var m = new Mesh();
            m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
