// Phase 8.2 builder A: more of the shared kit (second half of PlaceKit): textured boxes with world-sized UVs, generated surface textures
// (brick, planks, checker), lit-window towers for skylines, neon, a queue of fans, and a few props every music place can use
// (amp stack, drum kit, vinyl disc, folding chair, rope post, lamp bulbs). No downloaded art: primitives and small generated meshes only.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using WorldTour.World;

public static partial class PlaceKit
{
    // ---------- boxes with UVs in metres ----------

    /// <summary>A box whose UVs run in metres on every face (times <paramref name="uvPerMetre"/>), so a tiling texture keeps its size on any wall. The mesh is an asset (one per size, shared).</summary>
    public static GameObject TexBox(Transform parent, string name, Vector3 center, Vector3 size, Material mat, float uvPerMetre = 1f, Vector3? euler = null, bool shadows = true)
    {
        return BoxObject(parent, name, center, size, mat, uvPerMetre, euler, shadows, false);
    }

    static GameObject BoxObject(Transform parent, string name, Vector3 center, Vector3 size, Material mat, float uvPerMetre, Vector3? euler, bool shadows, bool tower)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = center;
        if (euler.HasValue) g.transform.localRotation = Quaternion.Euler(euler.Value);
        g.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size, uvPerMetre, tower);
        var r = g.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        if (!shadows) r.shadowCastingMode = ShadowCastingMode.Off;
        return g;
    }

    static readonly Dictionary<string, Mesh> BoxMeshes = new Dictionary<string, Mesh>();

    static Mesh BoxMesh(Vector3 size, float uvPerMetre, bool tower)
    {
        string key = (tower ? "tw_" : "tb_") + size.x.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "x" + size.y.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
            + "x" + size.z.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "_" + uvPerMetre.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        Mesh cached;
        if (BoxMeshes.TryGetValue(key, out cached) && cached != null) return cached;
        string dir = WorldArtBuilder.MeshDir + "/Boxes";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        string path = dir + "/" + key + ".asset";
        var mesh = new Mesh { name = key };
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var n = new List<Vector3>(); var t = new List<int>();
        Vector3 h = size / 2f;
        void Face(Vector3 origin, Vector3 u, Vector3 w, Vector3 normal, float su, float sw, bool side, float across0)
        {
            int i = v.Count;
            v.Add(origin); v.Add(origin + u); v.Add(origin + u + w); v.Add(origin + w);
            if (tower)
            {
                // 1 uv unit = 4 m across and 8 m up; the top face is a single point of the texture
                if (side) { uv.Add(new Vector2(across0 / 4f, 0)); uv.Add(new Vector2((across0 + su) / 4f, 0)); uv.Add(new Vector2((across0 + su) / 4f, sw / 8f)); uv.Add(new Vector2(across0 / 4f, sw / 8f)); }
                else for (int k = 0; k < 4; k++) uv.Add(Vector2.zero);
            }
            else { uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(su * uvPerMetre, 0)); uv.Add(new Vector2(su * uvPerMetre, sw * uvPerMetre)); uv.Add(new Vector2(0, sw * uvPerMetre)); }
            for (int k = 0; k < 4; k++) n.Add(normal);
            // wind so the face points along its normal (Unity front faces are clockwise)
            if (Vector3.Dot(Vector3.Cross(u, w), normal) < 0f) { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
            else { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
        }
        Face(new Vector3(-h.x, -h.y, -h.z), new Vector3(size.x, 0, 0), new Vector3(0, size.y, 0), Vector3.back, size.x, size.y, true, 0f);
        Face(new Vector3(-h.x, -h.y, h.z), new Vector3(size.x, 0, 0), new Vector3(0, size.y, 0), Vector3.forward, size.x, size.y, true, 0f);
        Face(new Vector3(-h.x, -h.y, -h.z), new Vector3(0, 0, size.z), new Vector3(0, size.y, 0), Vector3.left, size.z, size.y, true, 0f);
        Face(new Vector3(h.x, -h.y, -h.z), new Vector3(0, 0, size.z), new Vector3(0, size.y, 0), Vector3.right, size.z, size.y, true, 0f);
        Face(new Vector3(-h.x, h.y, -h.z), new Vector3(size.x, 0, 0), new Vector3(0, 0, size.z), Vector3.up, size.x, size.z, false, 0f);
        Face(new Vector3(-h.x, -h.y, -h.z), new Vector3(size.x, 0, 0), new Vector3(0, 0, size.z), Vector3.down, size.x, size.z, false, 0f);
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetNormals(n); mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old != null) { EditorUtility.CopySerialized(mesh, old); Object.DestroyImmediate(mesh); mesh = old; }
        else AssetDatabase.CreateAsset(mesh, path);
        BoxMeshes[key] = mesh;
        return mesh;
    }

    // ---------- generated surface textures ----------

    /// <summary>
    /// A 256 px tiling texture covering 1 m x 1 m (use it with <see cref="TexBox"/> and uvPerMetre 1). kind: "brick" (chunky bricks, 4 courses a metre),
    /// "planks" (covers 4 m: eight 0.5 m boards, use uvPerMetre 0.25), "checker" (0.5 m tiles), "tiles" (0.25 m tiles with grout).
    /// </summary>
    public static Texture2D PatternTexture(string assetName, string kind, string a, string b, string joint, int seed = 3)
    {
        string path = "Assets/World/Textures/" + assetName + ".png";
        const int n = 256;
        var px = new Color32[n * n];
        Color ca = C(a), cb = C(b), cj = C(joint);
        var rng = new System.Random(seed);
        float Rand() { return (float)rng.NextDouble(); }
        if (kind == "brick")
        {
            const int rows = 4, ch = n / rows, bw = n / 2;
            for (int r = 0; r < rows; r++)
            {
                int off = (r % 2) * (bw / 2);
                for (int k = -1; k < 3; k++)
                {
                    Color baseC = Color.Lerp(ca, cb, Rand()) * (0.93f + Rand() * 0.14f); baseC.a = 1f;
                    for (int y = 0; y < ch; y++)
                        for (int x = 0; x < bw; x++)
                        {
                            int X = (k * bw + x + off + n * 4) % n, Y = r * ch + y;
                            Color c = baseC;
                            if (y < 5 || x < 5) c = cj;                                   // mortar on the bottom and left of each brick
                            else if (y > ch - 11) c = Color.Lerp(c, Color.white, 0.12f);  // lit top edge
                            else c *= 1f + (Rand() - 0.5f) * 0.05f;
                            px[Y * n + X] = c;
                        }
                }
            }
        }
        else if (kind == "planks")
        {
            // 4 m x 4 m: eight 0.5 m boards (use uvPerMetre 0.25), each with one or two seams
            const int planks = 8, ph = n / planks;
            for (int pl = 0; pl < planks; pl++)
            {
                int seam1 = (int)(Rand() * n), seam2 = Rand() < 0.5f ? (seam1 + n / 2) % n : -1;
                float shade = 0.94f + Rand() * 0.12f;
                for (int y = 0; y < ph; y++)
                    for (int x = 0; x < n; x++)
                    {
                        int seg = (x >= seam1 ? 1 : 0) + (seam2 >= 0 && x >= seam2 ? 1 : 0);
                        Color c = Color.Lerp(ca, cb, (seg % 2 == 0 ? 0.15f : 0.75f) + (Rand() - 0.5f) * 0.05f) * shade; c.a = 1f;
                        if (y < 2 || x == seam1 || x == (seam1 + 1) % n || (seam2 >= 0 && (x == seam2 || x == (seam2 + 1) % n))) c = cj;
                        else if (y > ph - 4) c = Color.Lerp(c, Color.white, 0.06f);
                        if (Rand() < 0.03f) c *= 0.96f;
                        px[(pl * ph + y) * n + x] = c;
                    }
            }
        }
        else   // checker / tiles
        {
            int cells = kind == "checker" ? 2 : 4, cs = n / cells;
            for (int ty = 0; ty < cells; ty++)
                for (int tx = 0; tx < cells; tx++)
                {
                    Color baseC = (tx + ty) % 2 == 0 ? ca : cb;
                    if (kind == "tiles") baseC = Color.Lerp(ca, cb, Rand());
                    for (int y = 0; y < cs; y++)
                        for (int x = 0; x < cs; x++)
                        {
                            Color c = baseC;
                            if (x < 2 || y < 2) c = kind == "tiles" ? cj : Color.Lerp(c, cj, 0.4f);
                            else if (x >= cs - 2 || y >= cs - 2) c = Color.Lerp(c, Color.white, 0.07f);
                            else c *= 1f + (Rand() - 0.5f) * 0.03f;
                            c.a = 1f;
                            px[(ty * cs + y) * n + tx * cs + x] = c;
                        }
                }
        }
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = TextureWrapMode.Repeat; imp.mipmapEnabled = true; imp.filterMode = FilterMode.Trilinear; imp.anisoLevel = 4; imp.maxTextureSize = 256;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>A lit material with a pattern texture (1 m tile): pair with <see cref="TexBox"/>.</summary>
    public static Material PatternMat(string matName, Texture2D tex, Color? tint = null, float smooth = 0.12f)
    {
        var m = Lit(matName, tint ?? Color.white, smooth);
        m.SetTexture("_BaseMap", tex);
        return m;
    }

    /// <summary>
    /// A tower texture: dark walls with a grid of windows, some lit (emission map), covering 4 m x 8 m. Returns a material (use with TexBox, uvPerMetre 0.25 in x
    /// and a box of any size: the windows keep their size when the box is a multiple of 4 m wide).
    /// </summary>
    public static Material WindowTowerMaterial(string name, string wall, string windowOff, string windowOn, float litShare, int seed, float emissionBoost = 2.2f)
    {
        string a = "Assets/World/Textures/" + name + "_a.png", e = "Assets/World/Textures/" + name + "_e.png";
        const int w = 128, h = 256;
        var pa = new Color32[w * h]; var pe = new Color32[w * h];
        Color cw = C(wall), off = C(windowOff), on = C(windowOn);
        var rng = new System.Random(seed);
        const int cols = 6, rows = 8;
        int cw2 = w / cols, rh = h / rows;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float shade = 0.9f + 0.1f * (y / (float)h);
                pa[y * w + x] = cw * shade; pe[y * w + x] = Color.black;
            }
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                bool lit = rng.NextDouble() < litShare;
                Color wc = lit ? Color.Lerp(on, Color.white, 0.2f) : off * (0.8f + (float)rng.NextDouble() * 0.4f);
                for (int y = rh / 5; y < rh - rh / 5; y++)
                    for (int x = cw2 / 5; x < cw2 - cw2 / 5; x++)
                    {
                        int X = c * cw2 + x, Y = r * rh + y;
                        pa[Y * w + X] = wc;
                        if (lit) pe[Y * w + X] = on;
                    }
            }
        void Save(string p, Color32[] px)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.SetPixels32(px);
            File.WriteAllBytes(p, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(p);
            var imp = (TextureImporter)AssetImporter.GetAtPath(p);
            imp.wrapMode = TextureWrapMode.Repeat; imp.mipmapEnabled = true; imp.filterMode = FilterMode.Bilinear; imp.maxTextureSize = 256;
            imp.SaveAndReimport();
        }
        Save(a, pa); Save(e, pe);
        var m = Lit(name, Color.white, 0.1f);
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(a));
        m.EnableKeyword("_EMISSION");
        m.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(e));
        m.SetColor("_EmissionColor", Color.white * emissionBoost);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>A tower of the window material: width and depth in multiples of 4 m work best. UVs run 0.25 per metre across and 0.125 up.</summary>
    public static GameObject Tower(Transform parent, string name, Vector3 baseCenter, Vector3 size, Material mat)
    {
        size.y = Mathf.Max(2f, Mathf.Round(size.y / 2f) * 2f);   // heights in 2 m steps, so towers share mesh assets
        return BoxObject(parent, name, baseCenter + V(0, size.y / 2f, 0), size, mat, 1f, null, false, true);
    }

    // ---------- neon ----------

    public static Material NeonMat(string name, string hex, float strength = 2.4f)
    {
        Color c = C(hex);
        return Lit(name, c, 0.3f, new Color(c.r * strength, c.g * strength, c.b * strength));
    }

    /// <summary>A neon tube between two points (a thin box), glowing; pass a name so the material is shared.</summary>
    public static GameObject NeonBar(Transform parent, Vector3 a, Vector3 b, float thick, Material mat)
    {
        Vector3 mid = (a + b) / 2f, d = b - a;
        var g = Prim(PrimitiveType.Cube, "Neon", parent, mid, V(thick, thick, d.magnitude + thick), mat, null, false);
        Vector3 upv = Mathf.Abs(Vector3.Dot(d.normalized, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
        g.transform.localRotation = Quaternion.LookRotation(d.normalized, upv);
        return g;
    }

    /// <summary>Makes a group pulse and flicker like a neon sign (PropGlow with the given emission).</summary>
    public static void MakeGlow(GameObject g, Color emission, float flickerEvery = 0f, float low = 0.85f, float high = 1.12f)
    {
        var glow = g.AddComponent<PropGlow>();
        glow.emission = emission; glow.low = low; glow.high = high; glow.flickerEvery = flickerEvery;
    }

    // ---------- people and props ----------

    /// <summary>A baked crowd figure (static pose) placed in a scene, e.g. a queue outside the club.</summary>
    public static GameObject StaticFigure(Transform parent, string prefabName, Vector3 pos, float yaw, float scale = 0.85f, bool cheer = false)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(WorldArtBuilder.PrefabDir + "/Crowd/" + prefabName + ".prefab");
        if (src == null) throw new System.Exception("missing crowd prefab " + prefabName);
        var g = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
        g.name = prefabName.Replace("Crowd_", "Fan_");
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        g.transform.localScale = Vector3.one * scale;
        var f = g.GetComponent<CrowdFigure>();
        if (f != null && cheer) f.Cheer(true);
        return g;
    }

    public static readonly string[] FanPrefabs =
    {
        "Crowd_crowd_knight_coral", "Crowd_crowd_mage_pink", "Crowd_crowd_rogue_mint", "Crowd_crowd_knight_sky", "Crowd_crowd_mage_sun",
        "Crowd_crowd_rogue_lilac", "Crowd_crowd_knight_lilac", "Crowd_crowd_mage_teal", "Crowd_crowd_rogue_peach", "Crowd_crowd_rogue_coral",
    };

    /// <summary>A vinyl record standing on its edge facing -z: black disc with grooves, a coloured label and a centre hole. Diameter in metres.</summary>
    public static GameObject Vinyl(Transform parent, string name, Vector3 pos, float diameter, string labelHex, float yaw = 0f, string prefix = "pk_")
    {
        var root = Group(name, parent).transform;
        root.localPosition = pos; root.localRotation = Quaternion.Euler(0, yaw, 0);
        var black = Lit(prefix + "vinyl_black", C("#1a1a22"), 0.65f);
        var groove = Lit(prefix + "vinyl_groove", C("#2f2f3c"), 0.55f);
        var label = Lit(prefix + "vinyl_label_" + labelHex.TrimStart('#'), C(labelHex), 0.3f, C(labelHex) * 0.35f);
        float d = diameter;
        Prim(PrimitiveType.Cylinder, "Disc", root, V(0, 0, 0), V(d, 0.02f, d), black, V(90, 0, 0));
        foreach (float k in new[] { 0.86f, 0.74f, 0.62f })
            Prim(PrimitiveType.Cylinder, "Groove", root, V(0, 0, -0.026f), V(d * k, 0.002f, d * k), (k > 0.8f || k < 0.7f) ? groove : black, V(90, 0, 0), false);
        Prim(PrimitiveType.Cylinder, "Label", root, V(0, 0, -0.03f), V(d * 0.34f, 0.002f, d * 0.34f), label, V(90, 0, 0), false);
        Prim(PrimitiveType.Cylinder, "Hole", root, V(0, 0, -0.034f), V(d * 0.05f, 0.002f, d * 0.05f), Lit(prefix + "vinyl_hole", C("#fff8ee"), 0.2f), V(90, 0, 0), false);
        // a bright glint across the grooves
        Prim(PrimitiveType.Cube, "Glint", root, V(d * 0.2f, d * 0.18f, -0.038f), V(d * 0.3f, d * 0.02f, 0.004f), Lit(prefix + "vinyl_glint", C("#ffffff"), 0.2f, new Color(0.5f, 0.5f, 0.55f)), V(0, 0, -38), false);
        return root.gameObject;
    }

    /// <summary>A stack of two guitar amps with a speaker grille, a knob strip and a lit pilot light. Faces -z at yaw 0.</summary>
    public static GameObject AmpStack(Transform parent, Vector3 pos, float yaw, string prefix, float scale = 1f, string trimHex = "#d8c9a0")
    {
        var root = Group("AmpStack", parent).transform;
        root.localPosition = pos; root.localRotation = Quaternion.Euler(0, yaw, 0); root.localScale = Vector3.one * scale;
        var tolex = Lit(prefix + "amp_tolex", C("#26262e"), 0.3f);
        var grille = Lit(prefix + "amp_grille", C(trimHex), 0.2f);
        var cone = Lit(prefix + "amp_cone", C("#3a3a46"), 0.3f);
        var knob = Lit(prefix + "amp_knob", C("#f2efe6"), 0.4f);
        var led = Lit(prefix + "amp_led", C("#ff4a4a"), 0.3f, new Color(3f, 0.4f, 0.3f));
        // lower cabinet with four speakers, upper head with knobs
        Prim(PrimitiveType.Cube, "Cabinet", root, V(0, 0.65f, 0), V(1.3f, 1.3f, 0.7f), tolex);
        Prim(PrimitiveType.Cube, "Grille", root, V(0, 0.65f, -0.36f), V(1.12f, 1.12f, 0.03f), grille, null, false);
        foreach (float cx in new[] { -0.28f, 0.28f })
            foreach (float cy in new[] { 0.37f, 0.93f })
                Prim(PrimitiveType.Cylinder, "Cone", root, V(cx, cy, -0.385f), V(0.46f, 0.012f, 0.46f), cone, V(90, 0, 0), false);
        Prim(PrimitiveType.Cube, "Head", root, V(0, 1.6f, 0.02f), V(1.3f, 0.5f, 0.6f), tolex);
        Prim(PrimitiveType.Cube, "HeadPanel", root, V(0, 1.6f, -0.285f), V(1.18f, 0.32f, 0.03f), grille, null, false);
        for (int i = 0; i < 5; i++) Prim(PrimitiveType.Cylinder, "Knob", root, V(-0.42f + i * 0.21f, 1.6f, -0.31f), V(0.1f, 0.03f, 0.1f), knob, V(90, 0, 0), false);
        Prim(PrimitiveType.Sphere, "Pilot", root, V(0.5f, 1.6f, -0.31f), V(0.07f, 0.07f, 0.04f), led, null, false);
        return root.gameObject;
    }

    /// <summary>A drum kit from cylinders: kick, snare, two toms, hi-hat and two cymbals. Faces -z.</summary>
    public static GameObject DrumKit(Transform parent, Vector3 pos, float yaw, string prefix, string shellHex = "#f98bb0")
    {
        var root = Group("DrumKit", parent).transform;
        root.localPosition = pos; root.localRotation = Quaternion.Euler(0, yaw, 0);
        var shell = Lit(prefix + "drum_shell", C(shellHex), 0.45f);
        var head = Lit(prefix + "drum_head", C("#fff8ee"), 0.5f);
        var metal = Lit(prefix + "drum_metal", C("#d8b84a"), 0.7f);
        var steel = Lit(prefix + "drum_steel", C("#9aa0b8"), 0.6f);
        Prim(PrimitiveType.Cylinder, "Kick", root, V(0, 0.55f, 0), V(0.95f, 0.3f, 0.95f), shell, V(90, 0, 0));
        Prim(PrimitiveType.Cylinder, "KickHead", root, V(0, 0.55f, -0.31f), V(0.85f, 0.01f, 0.85f), head, V(90, 0, 0), false);
        foreach (float tx in new[] { -0.32f, 0.32f })
        {
            Prim(PrimitiveType.Cylinder, "Tom", root, V(tx, 1.2f, 0.05f), V(0.45f, 0.14f, 0.45f), shell, V(-12, 0, 0));
            Prim(PrimitiveType.Cylinder, "TomHead", root, V(tx, 1.33f, 0.02f), V(0.4f, 0.008f, 0.4f), head, V(-12, 0, 0), false);
        }
        Prim(PrimitiveType.Cylinder, "Snare", root, V(-0.95f, 0.78f, -0.45f), V(0.5f, 0.12f, 0.5f), shell);
        Prim(PrimitiveType.Cylinder, "SnareHead", root, V(-0.95f, 0.9f, -0.45f), V(0.44f, 0.008f, 0.44f), head, null, false);
        Prim(PrimitiveType.Cylinder, "SnareLeg", root, V(-0.95f, 0.4f, -0.45f), V(0.04f, 0.4f, 0.04f), steel);
        Prim(PrimitiveType.Cylinder, "HiHat", root, V(-1.5f, 1.15f, -0.1f), V(0.42f, 0.015f, 0.42f), metal);
        Prim(PrimitiveType.Cylinder, "HiHatStand", root, V(-1.5f, 0.58f, -0.1f), V(0.035f, 0.58f, 0.035f), steel);
        Prim(PrimitiveType.Cylinder, "Crash", root, V(-0.75f, 1.85f, 0.5f), V(0.62f, 0.012f, 0.62f), metal, V(8, 0, 6));
        Prim(PrimitiveType.Cylinder, "CrashStand", root, V(-0.75f, 0.95f, 0.5f), V(0.035f, 0.95f, 0.035f), steel);
        Prim(PrimitiveType.Cylinder, "Ride", root, V(0.95f, 1.6f, 0.35f), V(0.7f, 0.012f, 0.7f), metal, V(-6, 0, -8));
        Prim(PrimitiveType.Cylinder, "RideStand", root, V(0.95f, 0.8f, 0.35f), V(0.035f, 0.8f, 0.035f), steel);
        Prim(PrimitiveType.Cylinder, "Stool", root, V(0, 0.45f, 1.05f), V(0.45f, 0.06f, 0.45f), Lit(prefix + "drum_stool", C("#2d2fa0"), 0.3f));
        Prim(PrimitiveType.Cylinder, "StoolLeg", root, V(0, 0.22f, 1.05f), V(0.06f, 0.22f, 0.06f), steel);
        return root.gameObject;
    }

    /// <summary>A folding chair (metal frame, rounded seat) from primitives. Faces -z at yaw 0 (the seat's front).</summary>
    public static GameObject FoldingChair(Transform parent, Vector3 pos, float yaw, Material seat, Material frame)
    {
        var root = Group("FoldingChair", parent).transform;
        root.localPosition = pos; root.localRotation = Quaternion.Euler(0, yaw, 0);
        Prim(PrimitiveType.Cube, "Seat", root, V(0, 0.46f, 0), V(0.46f, 0.06f, 0.44f), seat);
        Prim(PrimitiveType.Cube, "Back", root, V(0, 0.82f, 0.21f), V(0.44f, 0.34f, 0.05f), seat);
        foreach (float lx in new[] { -0.2f, 0.2f })
        {
            Prim(PrimitiveType.Cylinder, "LegF", root, V(lx, 0.22f, -0.2f), V(0.035f, 0.23f, 0.035f), frame, V(0, 0, 0), false);
            Prim(PrimitiveType.Cylinder, "LegB", root, V(lx, 0.34f, 0.2f), V(0.035f, 0.5f, 0.035f), frame, null, false);
        }
        return root.gameObject;
    }

    /// <summary>A velvet-rope post: a brass pole with a ball on a round foot. The rope itself is a separate bar.</summary>
    public static void RopePost(Transform parent, Vector3 pos, Material brass)
    {
        Prim(PrimitiveType.Cylinder, "PostFoot", parent, pos + V(0, 0.03f, 0), V(0.34f, 0.03f, 0.34f), brass);
        Prim(PrimitiveType.Cylinder, "Post", parent, pos + V(0, 0.52f, 0), V(0.06f, 0.5f, 0.06f), brass);
        Prim(PrimitiveType.Sphere, "PostBall", parent, pos + V(0, 1.06f, 0), V(0.14f, 0.14f, 0.14f), brass);
    }

    /// <summary>A string of round bulbs hanging as a single mesh, between two points (a thin variant of the string lights).</summary>
    public static void BulbLine(MB wire, MB bulbs, Vector3 a, Vector3 b, float sag, float spacing) { StringLine(wire, bulbs, a, b, sag, spacing, 0.1f); }

    /// <summary>A soft additive glow lying on the ground (a neon sign's reflection on wet asphalt) or standing facing -z (a halo behind a sign). Not lit, no shadows.</summary>
    public static GameObject GlowDecal(Transform parent, Vector3 pos, Vector2 size, string matName, Color tint, bool flat = true)
    {
        var dot = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/World/Textures/soft_dot.png");
        var m = ParticleMaterial(matName, true, dot, tint);
        var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        g.name = "Glow";
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = flat ? Quaternion.Euler(90, 0, 0) : Quaternion.identity;
        g.transform.localScale = V(size.x, size.y, 1f);
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        return g;
    }
}
