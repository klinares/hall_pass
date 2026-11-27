using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WorldTour.Core.Meta;

namespace WorldTour.World.Wardrobe
{
    /// <summary>Phase 10B.1: colour helpers for the avatar (palette and skin from Core, plus shades).</summary>
    public static class AvatarColors
    {
        static readonly Dictionary<string, Color> Cache = new Dictionary<string, Color>();

        public static Color Hex(string hex)
        {
            Color c;
            if (Cache.TryGetValue(hex, out c)) return c;
            if (!ColorUtility.TryParseHtmlString(hex, out c)) c = Color.magenta;
            Cache[hex] = c;
            return c;
        }

        public static Color Palette(int i) { return Hex(AvatarCatalogue.PaletteHex[Mathf.Clamp(i, 0, AvatarCatalogue.PaletteHex.Count - 1)]); }
        public static Color Skin(int i) { return Hex(AvatarCatalogue.SkinHex[Mathf.Clamp(i, 0, AvatarCatalogue.SkinHex.Count - 1)]); }
        public static Color Shade(Color c, float t) { Color o = Color.Lerp(c, Color.black, t); o.a = 1f; return o; }
        public static Color Tint(Color c, float t) { Color o = Color.Lerp(c, Color.white, t); o.a = 1f; return o; }
        public static float Luma(Color c) { return 0.299f * c.r + 0.587f * c.g + 0.114f * c.b; }
        /// <summary>A very dark color made lighter until it is at least <paramref name="min"/> bright, so black shades or headphones keep a visible rim instead of a black hole.</summary>
        public static Color Lift(Color c, float min = 0.3f)
        {
            float l = Luma(c);
            return l >= min ? c : Color.Lerp(c, Color.white, (min - l) / (1f - l));
        }
        /// <summary>A colour that shows on <paramref name="c"/>: darker on a light one, lighter on a dark one (pattern marks, trims).</summary>
        public static Color Contrast(Color c) { return Luma(c) > 0.55f ? Shade(c, 0.55f) : Tint(c, 0.7f); }
    }

    /// <summary>
    /// Phase 10B.1: what an avatar part builder draws with. <see cref="Frame"/> gives the container of a body frame ("head", "chest", "hips", "armL", "armR", "legL", "legR", "footL", "footR"):
    /// a child of that bone whose local space is the Star's own model space (units of the model, the rest pose), so a part is placed by where it sits on her in the dump
    /// (head top y 2.19, front of the torso z 0.33). Everything made is listed in <see cref="Made"/> so a slot can be cleared and rebuilt.
    /// </summary>
    public sealed class AvatarCtx
    {
        public WardrobeAssets A;
        public Func<string, Transform> Frame;
        public readonly List<GameObject> Made = new List<GameObject>();
        public Color C, Ds, Ls, Pc, Skin, Hair;
        public int Pattern;
        /// <summary>The hat she wears (its key, "none" for no hat): hair that would poke through it hides its top parts.</summary>
        public string HatKey = "none";

        static readonly Dictionary<int, Material> Mats = new Dictionary<int, Material>();
        static readonly Dictionary<long, Material> Fabrics = new Dictionary<long, Material>();
        static readonly Dictionary<string, Material> Faces = new Dictionary<string, Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Mats.Clear(); Fabrics.Clear(); Faces.Clear(); }

        public void Colors(Color main)
        {
            C = main; Ds = AvatarColors.Shade(main, 0.28f); Ls = AvatarColors.Tint(main, 0.4f); Pc = AvatarColors.Contrast(main);
        }

        /// <summary>A lit material of one colour (shared and cached by colour, smoothness and glow).</summary>
        public Material M(Color c, float smooth = 0.3f, float glow = 0f)
        {
            if (A == null || A.baseLit == null) return null;
            Color32 k = c;
            int key = (k.r << 24 | k.g << 16 | k.b << 8) ^ (Mathf.RoundToInt(smooth * 20f) * 7919) ^ (Mathf.RoundToInt(glow * 10f) * 104729);
            Material m;
            if (Mats.TryGetValue(key, out m) && m != null) return m;
            m = A.Make("av_" + ColorUtility.ToHtmlStringRGB(c), c, smooth, glow > 0f ? (Color?)(c * glow) : null);
            Mats[key] = m;
            return m;
        }

        public Material Metal(Color c) { return M(c, 0.75f); }

        /// <summary>The cloth of a top or a bottom: plain when the pick has no pattern, otherwise the colour with the pattern woven in (a small tile; the part that wears it gets matching UVs).</summary>
        public Material F(Color c, float smooth = 0.25f)
        {
            if (Pattern <= 0 || A == null || A.baseLit == null) return M(c, smooth);
            Color32 k = c;
            long key = ((long)(k.r << 16 | k.g << 8 | k.b) << 8) | (uint)Pattern;
            Material m;
            if (Fabrics.TryGetValue(key, out m) && m != null) return m;
            m = A.Make("avf_" + ColorUtility.ToHtmlStringRGB(c) + "_" + Pattern, Color.white, smooth);
            m.SetTexture("_BaseMap", Fabric.MakeTile(Pattern, c, AvatarColors.Contrast(c)));
            Fabrics[key] = m;
            return m;
        }

        /// <summary>The face decal material of a face key (the face texture in the decal material the builder made), or null.</summary>
        public Material FaceMat(string key)
        {
            if (A == null || A.faceMat == null) return null;
            Material m;
            if (Faces.TryGetValue(key, out m) && m != null) return m;
            Texture2D tex = A.FaceTexture(key);
            if (tex == null) return null;
            m = new Material(A.faceMat) { name = "avface_" + key };
            m.SetTexture("_BaseMap", tex);
            Faces[key] = m;
            return m;
        }

        public GameObject Add(string frame, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, Vector3 euler = default(Vector3), bool shadow = false)
        {
            Transform f = Frame(frame);
            if (f == null || mesh == null || mat == null) return null;
            if (mat.name.StartsWith("avf_")) { mesh = BakeUv(mesh, pos, scale, euler, frame, int.Parse(mat.name.Substring(mat.name.LastIndexOf('_') + 1))); pos = Vector3.zero; scale = Vector3.one; euler = Vector3.zero; }
            var g = new GameObject(name);
            g.transform.SetParent(f, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(euler);
            g.transform.localScale = scale;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = g.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = false;
            Made.Add(g);
            return g;
        }

        /// <summary>A copy of a part's mesh already in model space with UVs that lay the cloth flat (see <see cref="Fabric"/>), so a pattern lies in the part's surface. Named "avf_" so its owner destroys it.</summary>
        static Mesh BakeUv(Mesh src, Vector3 pos, Vector3 scale, Vector3 euler, string frame, int pattern)
        {
            Fabric.Mode mode = frame.StartsWith("arm") ? Fabric.Mode.Arm : (frame.StartsWith("leg") ? Fabric.Mode.Leg : Fabric.Mode.Body);
            float cx = frame == "legL" ? -0.17f : (frame == "legR" ? 0.17f : 0f);
            Vector3[] sv = src.vertices, sn = src.normals; int[] st = src.triangles;
            Quaternion q = Quaternion.Euler(euler);
            Vector3 inv = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
            Vector2 per = Fabric.Period(pattern);
            int n = st.Length;
            var v = new Vector3[n]; var nn = new Vector3[n]; var uv = new Vector2[n]; var tri = new int[n];
            var u = new float[3]; var w = new float[3];
            for (int k = 0; k < n; k += 3)
            {
                for (int j = 0; j < 3; j++)
                {
                    int i = st[k + j];
                    v[k + j] = pos + q * Vector3.Scale(sv[i], scale);
                    nn[k + j] = sn.Length > i ? (q * Vector3.Scale(sn[i], inv)).normalized : Vector3.up;
                    Fabric.Project(mode, v[k + j], cx, out u[j], out w[j]);
                    tri[k + j] = k + j;
                }
                Fabric.Unwrap(mode, ref u[0], ref u[1], ref u[2]);
                for (int j = 0; j < 3; j++) uv[k + j] = new Vector2(u[j] / per.x, w[j] / per.y);
            }
            var m = new Mesh { name = "avf_part" };
            m.SetVertices(v); m.SetNormals(nn); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            return m;
        }
        public GameObject Cube(string frame, string name, Material m, Vector3 pos, Vector3 size, Vector3 euler = default(Vector3)) { return Add(frame, name, A.cube, m, pos, size, euler); }
        // the built-in sphere and cylinder are 2 across (radius 1), the cube is 1: sizes given to Ball and Cyl are the diameters, so they are halved here
        public GameObject Ball(string frame, string name, Material m, Vector3 pos, Vector3 size, Vector3 euler = default(Vector3)) { return Add(frame, name, A.sphere, m, pos, size * 0.5f, euler); }
        public GameObject Ball(string frame, string name, Material m, Vector3 pos, float d) { return Add(frame, name, A.sphere, m, pos, Vector3.one * (d * 0.5f)); }
        /// <summary>A drum: <paramref name="d"/> across, <paramref name="h"/> tall, axis up (the built-in cylinder is 2 tall, so its Y scale is half).</summary>
        public GameObject Cyl(string frame, string name, Material m, Vector3 pos, float d, float h, Vector3 euler = default(Vector3)) { return Add(frame, name, A.cylinder, m, pos, new Vector3(d * 0.5f, h * 0.5f, d * 0.5f), euler); }
        public GameObject Cone(string frame, string name, Material m, Vector3 pos, float r, float h, Vector3 euler = default(Vector3)) { return Add(frame, name, MeshKit.Cone(0.5f, 1f, 10), m, pos, new Vector3(r * 2f, h, r * 2f), euler); }
        /// <summary>A band round the body (axis up unless turned): outer radii <paramref name="rx"/> and <paramref name="rz"/>, <paramref name="h"/> tall.</summary>
        public GameObject Ring(string frame, string name, Material m, Vector3 pos, float rx, float rz, float h, Vector3 euler = default(Vector3), float wall = 0.04f)
        {
            return Add(frame, name, MeshKit.Tube(0.5f, 1f, wall, 28), m, pos, new Vector3(rx / (0.5f + wall), h, rz / (0.5f + wall)), euler);
        }
        /// <summary>Half a ball: the dome of a hood, a cap or a beanie.</summary>
        public GameObject Dome(string frame, string name, Material m, Vector3 pos, Vector3 size, Vector3 euler = default(Vector3), float squash = 1f) { return Add(frame, name, MeshKit.Dome(0.5f, squash, 18, 7), m, pos, size, euler); }
        public GameObject Star(string frame, string name, Material m, Vector3 pos, float size, Vector3 euler = default(Vector3)) { return Add(frame, name, MeshKit.Star(0.5f, 0.22f, 0.12f), m, pos, new Vector3(size, size, size * 0.5f), euler); }
        /// <summary>A hanging skirt or bell (open both ends): radii at the bottom and the top, height, y of its bottom.</summary>
        public GameObject Skirt(string frame, string name, Material m, float yBottom, float rBottom, float rTop, float h, float zScale = 1f, bool faceted = false, Vector3? at = null, int segs = 20)
        {
            Vector3 p = at ?? Vector3.zero; p.y = yBottom;
            return Add(frame, name, MeshKit.Frustum(1f, rTop / rBottom, h, segs, faceted), m, p, new Vector3(rBottom, 1f, rBottom * zScale));
        }
    }
}
