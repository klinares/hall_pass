// Phase 8.2 builder A: the shared kit every place scene is made from (moved out of PlaceCoffeeBuilder / PlaceCoffeeDressing, which now call it).
// Editor only. All of it is plain static helpers, so another place builder can call it as it stands:
//
//   PlaceKit.InitKits(tint)                 the KayKit palette materials (cityMat, restoMat, forestMat, blossomMat, furnMat)
//   PlaceKit.Group / Prim / Fbx / Block / MeshObject / Lit / DoubleSided / MB       scene-building helpers
//   PlaceKit.Look  + PlaceKit.Looks.*       a time of day: sun, sky, ambient, fog and the post-processing profile
//   PlaceKit.MakeProfile(look)              the post-processing VolumeProfile asset; FinishLook(look) adds the sun, sky and fog (call it last)
//   PlaceKit.PavingTexture(...)             the paving texture used for plazas and paths, any palette
//   PlaceKit.Stage(parent, blocks, spec)    stage block: deck, ramp + HeightZone, back wall, banner stars, truss, speakers, mic, spots + beams
//   PlaceKit.Populate(contract, ...)        the gameplay contract: PlayerSpawn, StageSpot, CrowdArea, ExitSpot (with the tour map signpost),
//                                           Crew_1..N with markers, camera + volume, Player, Riff, FollowCamera, WorldPlace
//   PlaceKit.StringLights(...), PixelText(...), Motes(...), Leaves(...)   dressing helpers
//
// Every material a caller makes through Lit() is rewritten in place when the name exists, so a place passes its own name prefix
// (coffee keeps none; records "rs_", rec "rc_", rooftop "rf_", club "cb_", kit pieces "pk_").
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using WorldTour.World;
using WorldTour.World.Gameplay;

public static partial class PlaceKit
{
    public const string City = "Assets/World/Art/KayKit_City/";
    public const string Resto = "Assets/World/Art/KayKit_Restaurant/";
    public const string Forest = "Assets/World/Art/KayKit_Forest/";
    public const string Furn = "Assets/World/Art/KayKit_Furniture/";
    public const string SceneDir = "Assets/World/Scenes/";
    public const string SettingsDir = "Assets/World/Settings/";
    public const float CityScale = 4f;

    public static Material cityMat, restoMat, forestMat, blossomMat, furnMat;
    public static readonly HashSet<string> Reported = new HashSet<string>();

    // ---------- small helpers ----------

    public static Color C(string hex) { return WorldArtBuilder.C(hex); }
    public static Vector3 V(float x, float y, float z) { return new Vector3(x, y, z); }
    public static Material Lit(string n, Color c, float s = 0.15f, Color? e = null) { return WorldArtBuilder.Lit(n, c, s, null, e); }

    public static Material DoubleSided(string name, Color c, float smooth, Color? emission = null)
    {
        var m = Lit(name, c, smooth, emission);
        m.SetFloat("_Cull", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    public static Material LoadOrNew(string name, string shader)
    {
        string path = WorldArtBuilder.MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
        return m;
    }

    public static GameObject Group(string name, Transform parent = null)
    {
        var g = new GameObject(name);
        if (parent != null) g.transform.SetParent(parent, false);
        return g;
    }

    public static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null, bool shadows = true)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        if (euler.HasValue) g.transform.localRotation = Quaternion.Euler(euler.Value);
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        if (!shadows) r.shadowCastingMode = ShadowCastingMode.Off;
        return g;
    }

    /// <summary>A KayKit FBX placed in the scene (kept as a prefab instance) with one of the shared palette materials.</summary>
    public static GameObject Fbx(string path, Transform parent, Vector3 pos, float yaw, float scale, Material mat, string name = null)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (src == null) throw new System.Exception("missing " + path);
        var g = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
        g.name = name ?? src.name;
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(0, yaw, 0) * src.transform.localRotation;   // keep the FBX root rotation too
        g.transform.localScale = src.transform.localScale * scale;   // keep the FBX root scale (some kits carry 100)
        foreach (var r in g.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
        }
        if (Reported.Add(path))
        {
            var b = new Bounds(g.transform.position, Vector3.zero);
            bool first = true;
            foreach (var r in g.GetComponentsInChildren<Renderer>(true)) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            Debug.Log("WT_PIECE " + Path.GetFileNameWithoutExtension(path) + " scale=" + scale + " size=" + b.size.ToString("F2") + " ctr=" + (b.center - g.transform.position).ToString("F2"));
        }
        return g;
    }

    /// <summary>A round blocker on the ground: walkers push out of it.</summary>
    public static void Block(Transform parent, float x, float z, float radius)
    {
        var g = new GameObject("Block");
        g.transform.SetParent(parent, false);
        g.transform.position = V(x, 0, z);
        g.AddComponent<Obstacle>().radius = radius;
    }

    /// <summary>A line of blockers along a wall or a row of tables, one every <paramref name="step"/> metres.</summary>
    public static void BlockLine(Transform parent, Vector2 a, Vector2 b, float radius, float step = 1.2f)
    {
        float len = Vector2.Distance(a, b);
        int n = Mathf.Max(1, Mathf.CeilToInt(len / step));
        for (int i = 0; i <= n; i++) { var p = Vector2.Lerp(a, b, i / (float)n); Block(parent, p.x, p.y, radius); }
    }

    public static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material mat, bool shadows)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = g.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        if (!shadows) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
        return g;
    }

    /// <summary>A star (the marker star mesh) as a flat sign, facing the camera side (-z) by default.</summary>
    public static GameObject StarSign(string name, Transform parent, Vector3 pos, float size, Material mat, float yaw = 180f, float thickness = 0.45f)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        g.transform.localScale = V(size, size, size * thickness);
        g.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(WorldArtBuilder.MeshDir + "/MarkerStar.asset");
        g.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return g;
    }

    // ---------- the KayKit palette materials ----------

    /// <summary>
    /// Builds the shared pack materials. <paramref name="namePrefix"/> keeps a place's tinted copy apart from the others (a place that passes
    /// "" gets exactly the coffee street's materials). <paramref name="cityTint"/> multiplies the city pack's palette (a sunset or night wash).
    /// </summary>
    public static void InitKits(string namePrefix = "", Color? cityTint = null, bool blossom = true)
    {
        Reported.Clear();
        cityMat = Lit(namePrefix + "city_kit", cityTint ?? Color.white, 0.12f);
        cityMat.SetTexture("_BaseMap", WorldArtBuilder.ArtTexture(City + "citybits_texture.png"));
        restoMat = Lit(namePrefix + "resto_kit", Color.white, 0.12f);
        restoMat.SetTexture("_BaseMap", WorldArtBuilder.ArtTexture(Resto + "restaurantbits_texture.png"));
        forestMat = Lit(namePrefix + "forest_kit", Color.white, 0.08f);
        forestMat.SetTexture("_BaseMap", WorldArtBuilder.ArtTexture(Forest + "forest_texture.png"));
        furnMat = Lit(namePrefix + "furniture_kit", Color.white, 0.12f, null);
        furnMat.SetTexture("_BaseMap", WorldArtBuilder.ArtTexture(Furn + "furniturebits_texture.png"));
        if (blossom)
        {
            blossomMat = Lit(namePrefix + "forest_blossom", Color.white, 0.08f);
            blossomMat.SetTexture("_BaseMap", BlossomTexture());
        }
    }

    /// <summary>The forest palette with its green swatch repainted cherry-blossom pink.</summary>
    public static Texture2D BlossomTexture()
    {
        string src = Forest + "forest_texture.png";
        string dst = WorldArtBuilder.TexDir + "/forest_blossom.png";
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        t.LoadImage(File.ReadAllBytes(src));
        var px = t.GetPixels32();
        int w = t.width, h = t.height;
        Color top = C("#ffd0e0"), bot = C("#ee7aa8");
        for (int y = h - 256; y < h; y++)
            for (int x = 0; x < 128; x++)
            {
                float k = (h - 1 - y) / 255f;
                Color c = Color.Lerp(top, bot, k);
                px[y * w + x] = c;
            }
        t.SetPixels32(px);
        File.WriteAllBytes(dst, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(dst);
        var imp = (TextureImporter)AssetImporter.GetAtPath(dst);
        imp.filterMode = FilterMode.Point; imp.mipmapEnabled = false; imp.maxTextureSize = 512; imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
    }

    /// <summary>A copy of the forest palette with its green swatch repainted (a dry autumn, a night green). Written once per name.</summary>
    public static Material ForestTinted(string name, string topHex, string bottomHex)
    {
        string src = Forest + "forest_texture.png";
        string dst = WorldArtBuilder.TexDir + "/forest_" + name + ".png";
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        t.LoadImage(File.ReadAllBytes(src));
        var px = t.GetPixels32();
        int w = t.width, h = t.height;
        Color top = C(topHex), bot = C(bottomHex);
        for (int y = h - 256; y < h; y++)
            for (int x = 0; x < 128; x++)
            {
                float k = (h - 1 - y) / 255f;
                px[y * w + x] = Color.Lerp(top, bot, k);
            }
        t.SetPixels32(px);
        File.WriteAllBytes(dst, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(dst);
        var imp = (TextureImporter)AssetImporter.GetAtPath(dst);
        imp.filterMode = FilterMode.Point; imp.mipmapEnabled = false; imp.maxTextureSize = 512; imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
        var m = Lit("forest_" + name, Color.white, 0.08f);
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(dst));
        return m;
    }

    // ---------- a mesh builder for small faceted shapes ----------

    /// <summary>A growing mesh of small faceted shapes: gems (double pyramids) with any three axes, and boxes. Used for bulbs, wire, blossoms, pixel text.</summary>
    public sealed class MB
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<int> t = new List<int>();

        void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }

        void TriOut(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) Tri(a, c, b); else Tri(a, b, c);
        }

        public void Gem(Vector3 c, Vector3 ax, Vector3 ay, Vector3 az)
        {
            if (Vector3.Dot(Vector3.Cross(ax, ay), az) < 0f) ay = -ay;
            Vector3 X = c + ax, nX = c - ax, Y = c + ay, nY = c - ay, Z = c + az, nZ = c - az;
            Tri(Y, Z, X); Tri(Y, nX, Z); Tri(Y, nZ, nX); Tri(Y, X, nZ);
            Tri(nY, X, Z); Tri(nY, Z, nX); Tri(nY, nX, nZ); Tri(nY, nZ, X);
        }

        public void Gem(Vector3 c, float rx, float ry, float rz) { Gem(c, Vector3.right * rx, Vector3.up * ry, Vector3.forward * rz); }

        /// <summary>A box from its centre and half sizes, turned <paramref name="yaw"/> degrees about the vertical through its centre.</summary>
        public void Box(Vector3 c, Vector3 h, float yaw = 0f)
        {
            Quaternion q = Quaternion.Euler(0, yaw, 0);
            Vector3 P(float sx, float sy, float sz) { return c + q * new Vector3(sx * h.x, sy * h.y, sz * h.z); }
            Vector3 a = P(-1, -1, -1), b = P(1, -1, -1), d = P(1, 1, -1), e = P(-1, 1, -1), f = P(-1, -1, 1), g = P(1, -1, 1), i2 = P(1, 1, 1), j = P(-1, 1, 1);
            Quad(a, b, d, e, q * Vector3.back); Quad(g, f, j, i2, q * Vector3.forward);
            Quad(f, a, e, j, q * Vector3.left); Quad(b, g, i2, d, q * Vector3.right);
            Quad(e, d, i2, j, Vector3.up); Quad(f, g, b, a, Vector3.down);
        }

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward) { TriOut(a, b, c, outward); TriOut(a, c, d, outward); }

        /// <summary>A thin spindle from a to b: joined end to end these read as a beaded wire.</summary>
        public void Spindle(Vector3 a, Vector3 b, float thick)
        {
            Vector3 d = b - a;
            Vector3 dir = d.normalized;
            Vector3 side = Vector3.Cross(dir, Vector3.up);
            if (side.sqrMagnitude < 0.0001f) side = Vector3.right;
            side.Normalize();
            Vector3 up = Vector3.Cross(side, dir).normalized;
            Gem((a + b) / 2f, dir * (d.magnitude / 2f), side * thick, up * thick);
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            string path = WorldArtBuilder.MeshDir + "/" + name + ".asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null) { EditorUtility.CopySerialized(m, old); Object.DestroyImmediate(m); return old; }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }

    // ---------- pixel text (signs: no font asset, no readable real names) ----------

    static readonly Dictionary<char, string[]> Glyphs = MakeGlyphs();

    static Dictionary<char, string[]> MakeGlyphs()
    {
        var d = new Dictionary<char, string[]>();
        void G(char c, params string[] rows) { d[c] = rows; }
        G('A', ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#");
        G('B', "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####.");
        G('C', ".####", "#....", "#....", "#....", "#....", "#....", ".####");
        G('D', "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####.");
        G('E', "#####", "#....", "#....", "####.", "#....", "#....", "#####");
        G('F', "#####", "#....", "#....", "####.", "#....", "#....", "#....");
        G('G', ".####", "#....", "#....", "#.###", "#...#", "#...#", ".###.");
        G('H', "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#");
        G('I', "###", ".#.", ".#.", ".#.", ".#.", ".#.", "###");
        G('J', "..###", "...#.", "...#.", "...#.", "...#.", "#..#.", ".##..");
        G('K', "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#");
        G('L', "#....", "#....", "#....", "#....", "#....", "#....", "#####");
        G('M', "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#");
        G('N', "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#");
        G('O', ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###.");
        G('P', "####.", "#...#", "#...#", "####.", "#....", "#....", "#....");
        G('Q', ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#");
        G('R', "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#");
        G('S', ".####", "#....", "#....", ".###.", "....#", "....#", "####.");
        G('T', "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#..");
        G('U', "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###.");
        G('V', "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#..");
        G('W', "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#");
        G('X', "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#");
        G('Y', "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#..");
        G('Z', "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####");
        G('0', ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###.");
        G('1', "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###.");
        G('2', ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####");
        G('3', "####.", "....#", "....#", ".###.", "....#", "....#", "####.");
        G('4', "#...#", "#...#", "#...#", "#####", "....#", "....#", "....#");
        G('5', "#####", "#....", "####.", "....#", "....#", "#...#", ".###.");
        G('6', ".###.", "#....", "#....", "####.", "#...#", "#...#", ".###.");
        G('7', "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#...");
        G('8', ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###.");
        G('9', ".###.", "#...#", "#...#", ".####", "....#", "....#", ".###.");
        G('-', ".....", ".....", ".....", "#####", ".....", ".....", ".....");
        G(':', ".", "#", ".", ".", "#", ".", ".");
        G('!', "#", "#", "#", "#", "#", ".", "#");
        G('>', "#....", ".#...", "..#..", "...#.", "..#..", ".#...", "#....");
        G(' ', "..", "..", "..", "..", "..", "..", "..");
        return d;
    }

    /// <summary>
    /// Block letters (5 x 7 pixels) as one mesh, centred on <paramref name="pos"/> (local to the parent), reading left to right as seen from the south
    /// (the camera side). <paramref name="px"/> is the pixel size in metres. Returns the object.
    /// </summary>
    public static GameObject PixelText(Transform parent, string text, Vector3 pos, float px, Material mat, string meshName, float depth = 0.6f, float spacing = 1f)
    {
        var mb = new MB();
        text = text.ToUpperInvariant();
        float total = 0f;
        foreach (char ch in text) { string[] g; if (!Glyphs.TryGetValue(ch, out g)) g = Glyphs[' ']; total += g[0].Length + spacing; }
        total -= spacing;
        float x0 = -total / 2f;
        foreach (char ch in text)
        {
            string[] g; if (!Glyphs.TryGetValue(ch, out g)) g = Glyphs[' '];
            for (int row = 0; row < 7; row++)
                for (int col = 0; col < g[row].Length; col++)
                    if (g[row][col] == '#')
                        mb.Box(V((x0 + col + 0.5f) * px, (3.5f - row - 0.5f) * px, 0), V(px * 0.5f, px * 0.5f, px * depth * 0.5f));
            x0 += g[0].Length + spacing;
        }
        var go = MeshObject("Text_" + text, parent, mb.ToMesh(meshName), mat, false);
        go.transform.localPosition = pos;
        return go;
    }

    // ---------- string lights ----------

    public static void StringLine(MB wire, MB bulbs, Vector3 a, Vector3 b, float sag, float spacing, float bulbSize = 0.085f)
    {
        int seg = Mathf.Max(6, Mathf.CeilToInt((b - a).magnitude / 0.5f));
        Vector3 prev = a;
        for (int i = 1; i <= seg; i++)
        {
            float s = i / (float)seg;
            Vector3 p = Vector3.Lerp(a, b, s) + Vector3.down * (sag * 4f * s * (1f - s));
            wire.Spindle(prev, p, 0.014f);
            prev = p;
        }
        int n = Mathf.Max(3, Mathf.RoundToInt((b - a).magnitude / spacing));
        for (int i = 1; i < n; i++)
        {
            float s = i / (float)n;
            Vector3 p = Vector3.Lerp(a, b, s) + Vector3.down * (sag * 4f * s * (1f - s) + 0.09f);
            bulbs.Gem(p, bulbSize * 0.8f, bulbSize * 1.15f, bulbSize * 0.8f);
        }
    }

    /// <summary>Emits the two meshes (wire, bulbs) of a set of string lines. <paramref name="id"/> names the mesh assets and the materials.</summary>
    public static void EmitStrings(Transform t, MB wire, MB bulbs, string id, Color bulbColor, Color bulbGlow)
    {
        var wireMat = DoubleSided(id + "_string_wire", C("#4a3b4d"), 0.2f);
        var bulbMat = DoubleSided(id + "_string_bulb", bulbColor, 0.3f, bulbGlow);
        MeshObject("StringWire", t, wire.ToMesh(id + "_strings_wire"), wireMat, false);
        MeshObject("StringBulbs", t, bulbs.ToMesh(id + "_strings_bulbs"), bulbMat, false);
    }

    // ---------- paving ----------

    /// <summary>Paving with joints and a scatter of accent slabs: 8 x 8 slabs per tile. The defaults are the coffee plaza's cream and pink.</summary>
    public static Texture2D PavingTexture(string assetName, string cream = "#f3e6cf", string warm = "#e9d6b9", string grout = "#b9a998", string pink = "#f4a9c4", string pinkLight = "#f9cddc", int seed = 11, bool accents = true)
    {
        string path = "Assets/World/Textures/" + assetName + ".png";
        const int n = 512, cell = n / 8;
        var px = new Color32[n * n];
        Color cr = C(cream), wm = C(warm), gr = C(grout), pk = C(pink), pl = C(pinkLight);
        var rng = new System.Random(seed);
        for (int ty = 0; ty < 8; ty++)
            for (int tx = 0; tx < 8; tx++)
            {
                bool accent = accents && ((tx == 1 && ty == 1) || (tx == 5 && ty == 5) || (tx == 3 && ty == 6) || (tx == 6 && ty == 2));
                float v = 0.965f + (float)rng.NextDouble() * 0.07f;
                Color baseC = accent ? Color.Lerp(pk, pl, (float)rng.NextDouble() * 0.5f) : Color.Lerp(cr, wm, (float)rng.NextDouble() * 0.6f);
                baseC *= v; baseC.a = 1f;
                for (int y = 0; y < cell; y++)
                    for (int x = 0; x < cell; x++)
                    {
                        Color c = baseC;
                        float edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(cell - 1 - x, cell - 1 - y));
                        if (x >= cell - 3 || y >= cell - 3) c = gr;                                      // joint on the far sides
                        else if (x < 2 || y < 2) c = Color.Lerp(c, Color.white, 0.35f);                   // lit near edge
                        else c *= 1f + ((float)rng.NextDouble() - 0.5f) * 0.035f;                        // a little grain
                        if (accent && edge > 12 && edge < 15) c = Color.Lerp(c, pl, 0.55f);               // a lighter inner outline
                        px[(ty * cell + y) * n + tx * cell + x] = c;
                    }
            }
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.SetPixels32(px);
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = TextureWrapMode.Repeat; imp.mipmapEnabled = true; imp.filterMode = FilterMode.Trilinear; imp.anisoLevel = 8; imp.maxTextureSize = 512;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>A tiled plane of ground in a paving texture: <paramref name="size"/> metres, one texture tile per 4 m.</summary>
    public static GameObject PavedPlane(string name, Transform t, string matName, Texture2D tex, Vector3 center, Vector2 size, Color? tint = null, float smooth = 0.12f)
    {
        var m = Lit(matName, tint ?? Color.white, smooth);
        m.SetTexture("_BaseMap", tex);
        m.SetTextureScale("_BaseMap", new Vector2(size.x / 4f, size.y / 4f));
        return Prim(PrimitiveType.Plane, name, t, center, V(size.x / 10f, 1, size.y / 10f), m, null, false);
    }

    // ---------- the time of day: sun, sky, fog, post ----------

    /// <summary>One time of day. Values are the coffee street's morning unless a factory below says otherwise.</summary>
    public sealed class Look
    {
        public string ProfileName = "Coffee";                 // Assets/World/Settings/<ProfileName>VolumeProfile.asset
        public string SkyMaterial = "sky";                    // Assets/World/Materials/Art/<SkyMaterial>.mat
        public Color SunColor = new Color(1f, 0.83f, 0.62f);
        public float SunIntensity = 2.1f, SunShadowStrength = 0.9f;
        public Vector3 SunEuler = new Vector3(36f, 52f, 0f);
        public Color SkyTop = new Color(0.33f, 0.52f, 0.95f), SkyMid = new Color(0.6f, 0.76f, 1.0f), SkyHorizon = new Color(1.0f, 0.89f, 0.84f), SkyBottom = new Color(0.93f, 0.84f, 0.82f);
        public Color? SkyGlow;
        public Color AmbSky = new Color(0.55f, 0.68f, 0.98f), AmbEquator = new Color(0.95f, 0.8f, 0.76f), AmbGround = new Color(0.46f, 0.42f, 0.48f);
        public float AmbIntensity = 0.9f;
        public Color Fog = new Color(0.93f, 0.89f, 0.9f);
        public float FogDensity = 0.0075f;
        public Color ShadowTint = new Color(0.42f, 0.48f, 0.68f);
        public Color CameraBackground = new Color(0.8f, 0.87f, 0.97f);
        // post
        public float BloomThreshold = 1.0f, BloomIntensity = 0.38f, BloomScatter = 0.6f;
        public Color BloomTint = new Color(1f, 0.92f, 0.85f);
        public float Exposure = 0f, Contrast = 16f, Saturation = 16f;
        public Color ColorFilter = new Color(1f, 0.985f, 0.96f);
        public float WbTemperature = 6f, WbTint = 3f;
        public Vector4 Shadows = new Vector4(0.86f, 0.9f, 1.12f, 0f), Midtones = new Vector4(1f, 1f, 1f, 0f), Highlights = new Vector4(1.08f, 1.0f, 0.9f, 0f);
        public float VignetteIntensity = 0.2f, VignetteSmoothness = 0.5f;
    }

    /// <summary>Creates the volume profile asset for a look (replacing the old asset).</summary>
    public static VolumeProfile MakeProfile(Look l)
    {
        string path = SettingsDir + l.ProfileName + "VolumeProfile.asset";
        AssetDatabase.DeleteAsset(path);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);
        var bloom = profile.Add<Bloom>();
        bloom.threshold.Override(l.BloomThreshold);
        bloom.intensity.Override(l.BloomIntensity);
        bloom.scatter.Override(l.BloomScatter);
        bloom.tint.Override(l.BloomTint);
        bloom.highQualityFiltering.Override(false);
        var tone = profile.Add<Tonemapping>();
        tone.mode.Override(TonemappingMode.Neutral);   // keeps colour rich where ACES greys it
        var grade = profile.Add<ColorAdjustments>();
        grade.postExposure.Override(l.Exposure);
        grade.contrast.Override(l.Contrast);
        grade.saturation.Override(l.Saturation);
        grade.colorFilter.Override(l.ColorFilter);
        var wb = profile.Add<WhiteBalance>();
        wb.temperature.Override(l.WbTemperature);
        wb.tint.Override(l.WbTint);
        // cool the shadows, warm the lights: the shadow side of every wall reads blue-lilac, the sunlit side peach
        var smh = profile.Add<ShadowsMidtonesHighlights>();
        smh.shadows.Override(l.Shadows);
        smh.midtones.Override(l.Midtones);
        smh.highlights.Override(l.Highlights);
        smh.shadowsStart.Override(0f); smh.shadowsEnd.Override(0.4f);
        smh.highlightsStart.Override(0.55f); smh.highlightsEnd.Override(1f);
        var vig = profile.Add<Vignette>();
        vig.intensity.Override(l.VignetteIntensity);
        vig.smoothness.Override(l.VignetteSmoothness);
        foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
        EditorUtility.SetDirty(profile);
        return profile;
    }

    public static void MakeSun(Look l)
    {
        var g = new GameObject("Sun");
        var li = g.AddComponent<Light>();
        li.type = LightType.Directional;
        li.color = l.SunColor;
        li.intensity = l.SunIntensity;
        li.shadows = LightShadows.Soft;
        li.shadowStrength = l.SunShadowStrength;
        li.shadowBias = 0.05f;
        li.shadowNormalBias = 0.4f;
        g.transform.rotation = Quaternion.Euler(l.SunEuler);
        RenderSettings.sun = li;
    }

    public static void MakeAtmosphere(Look l)
    {
        // a soft gradient sky (WorldTour/SkyGradient); the fog is the horizon colour so far things melt into it
        string skyPath = WorldArtBuilder.MatDir + "/" + l.SkyMaterial + ".mat";
        var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        var skyShader = Shader.Find("WorldTour/SkyGradient");
        if (sky == null) { sky = new Material(skyShader); AssetDatabase.CreateAsset(sky, skyPath); }
        sky.shader = skyShader;
        sky.SetColor("_Top", l.SkyTop);
        sky.SetColor("_Mid", l.SkyMid);
        sky.SetColor("_Horizon", l.SkyHorizon);
        sky.SetColor("_Bottom", l.SkyBottom);
        if (l.SkyGlow.HasValue) sky.SetColor("_SunColor", l.SkyGlow.Value);
        sky.SetVector("_SunDir", -(Quaternion.Euler(l.SunEuler) * Vector3.forward));
        EditorUtility.SetDirty(sky);
        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = l.AmbSky;
        RenderSettings.ambientEquatorColor = l.AmbEquator;
        RenderSettings.ambientGroundColor = l.AmbGround;
        RenderSettings.ambientIntensity = l.AmbIntensity;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = l.FogDensity;
        RenderSettings.fogColor = l.Fog;
        RenderSettings.subtractiveShadowColor = l.ShadowTint;
    }

    public static Camera MakeCamera(VolumeProfile profile, Look l)
    {
        var g = new GameObject("Main Camera");
        g.tag = "MainCamera";
        var c = g.AddComponent<Camera>();
        c.clearFlags = CameraClearFlags.Skybox;
        c.backgroundColor = l.CameraBackground;
        c.fieldOfView = 44f;
        c.nearClipPlane = 0.5f;
        c.farClipPlane = 150f;
        g.AddComponent<AudioListener>();
        var data = g.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        data.renderShadows = true;
        data.antialiasing = AntialiasingMode.None;
        var vol = new GameObject("Post Volume").AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = profile;
        return c;
    }

    /// <summary>The times of day the places use. Coffee is the approved reference.</summary>
    public static class Looks
    {
        public static Look Coffee() { return new Look(); }

        /// <summary>Records: late morning, bright and a little cool, thin haze, a high white-yellow sun.</summary>
        public static Look Records()
        {
            return new Look
            {
                ProfileName = "Records", SkyMaterial = "sky_records",
                SunColor = new Color(1f, 0.93f, 0.8f), SunIntensity = 2.2f, SunEuler = new Vector3(48f, 40f, 0f),
                SkyTop = new Color(0.24f, 0.46f, 0.88f), SkyMid = new Color(0.52f, 0.75f, 0.96f), SkyHorizon = new Color(0.88f, 0.94f, 0.97f), SkyBottom = new Color(0.82f, 0.86f, 0.88f),
                SkyGlow = new Color(1f, 0.93f, 0.75f),
                AmbSky = new Color(0.5f, 0.68f, 0.98f), AmbEquator = new Color(0.82f, 0.84f, 0.86f), AmbGround = new Color(0.42f, 0.4f, 0.42f), AmbIntensity = 0.95f,
                Fog = new Color(0.82f, 0.88f, 0.92f), FogDensity = 0.0085f, ShadowTint = new Color(0.38f, 0.46f, 0.7f),
                CameraBackground = new Color(0.7f, 0.84f, 0.95f),
                BloomIntensity = 0.3f, BloomTint = new Color(1f, 0.96f, 0.9f), Contrast = 14f, Saturation = 8f,
                ColorFilter = new Color(0.99f, 1f, 1f), WbTemperature = -3f, WbTint = 0f,
                Shadows = new Vector4(0.84f, 0.92f, 1.15f, 0f), Highlights = new Vector4(1.04f, 1.02f, 0.94f, 0f),
            };
        }

        /// <summary>Rec center: afternoon, clear and a little gold, a lower sun from the west.</summary>
        public static Look Rec()
        {
            return new Look
            {
                ProfileName = "Rec", SkyMaterial = "sky_rec",
                SunColor = new Color(1f, 0.86f, 0.6f), SunIntensity = 2.2f, SunEuler = new Vector3(40f, 64f, 0f),
                SkyTop = new Color(0.3f, 0.5f, 0.92f), SkyMid = new Color(0.58f, 0.76f, 0.98f), SkyHorizon = new Color(1f, 0.92f, 0.8f), SkyBottom = new Color(0.9f, 0.84f, 0.76f),
                SkyGlow = new Color(1f, 0.82f, 0.5f),
                AmbSky = new Color(0.56f, 0.7f, 0.98f), AmbEquator = new Color(0.96f, 0.86f, 0.74f), AmbGround = new Color(0.5f, 0.45f, 0.4f), AmbIntensity = 0.92f,
                Fog = new Color(0.94f, 0.9f, 0.84f), FogDensity = 0.007f,
                CameraBackground = new Color(0.78f, 0.86f, 0.96f),
                Contrast = 15f, Saturation = 14f, WbTemperature = 8f, WbTint = 2f,
            };
        }

        /// <summary>Rooftop: sunset, a low orange sun, a warm orange-pink sky.</summary>
        public static Look Rooftop()
        {
            return new Look
            {
                ProfileName = "Rooftop", SkyMaterial = "sky_rooftop",
                SunColor = new Color(1f, 0.6f, 0.34f), SunIntensity = 2.3f, SunEuler = new Vector3(15f, 62f, 0f), SunShadowStrength = 0.85f,
                SkyTop = new Color(0.34f, 0.3f, 0.7f), SkyMid = new Color(0.86f, 0.46f, 0.6f), SkyHorizon = new Color(1f, 0.66f, 0.38f), SkyBottom = new Color(0.62f, 0.34f, 0.42f),
                SkyGlow = new Color(1f, 0.55f, 0.3f),
                AmbSky = new Color(0.62f, 0.5f, 0.84f), AmbEquator = new Color(1f, 0.66f, 0.55f), AmbGround = new Color(0.42f, 0.3f, 0.38f), AmbIntensity = 0.95f,
                Fog = new Color(1f, 0.66f, 0.5f), FogDensity = 0.011f, ShadowTint = new Color(0.46f, 0.36f, 0.6f),
                CameraBackground = new Color(0.9f, 0.55f, 0.5f),
                BloomThreshold = 0.9f, BloomIntensity = 0.55f, BloomScatter = 0.7f, BloomTint = new Color(1f, 0.78f, 0.62f),
                Contrast = 18f, Saturation = 18f, ColorFilter = new Color(1f, 0.96f, 0.93f), WbTemperature = 10f, WbTint = 6f,
                Shadows = new Vector4(0.8f, 0.82f, 1.18f, 0f), Highlights = new Vector4(1.12f, 0.98f, 0.82f, 0f),
                VignetteIntensity = 0.26f,
            };
        }

        /// <summary>Club: evening, deep blue-violet sky, a dim cool moon-like key light, neon doing the work.</summary>
        public static Look Club()
        {
            return new Look
            {
                ProfileName = "Club", SkyMaterial = "sky_club",
                SunColor = new Color(0.62f, 0.62f, 1f), SunIntensity = 1.15f, SunEuler = new Vector3(44f, 40f, 0f), SunShadowStrength = 0.75f,
                SkyTop = new Color(0.07f, 0.07f, 0.25f), SkyMid = new Color(0.22f, 0.14f, 0.45f), SkyHorizon = new Color(0.78f, 0.3f, 0.55f), SkyBottom = new Color(0.2f, 0.1f, 0.3f),
                SkyGlow = new Color(0.8f, 0.4f, 0.9f),
                AmbSky = new Color(0.32f, 0.3f, 0.7f), AmbEquator = new Color(0.55f, 0.36f, 0.7f), AmbGround = new Color(0.22f, 0.15f, 0.3f), AmbIntensity = 1.0f,
                Fog = new Color(0.2f, 0.12f, 0.34f), FogDensity = 0.012f, ShadowTint = new Color(0.25f, 0.2f, 0.5f),
                CameraBackground = new Color(0.12f, 0.08f, 0.26f),
                BloomThreshold = 0.8f, BloomIntensity = 0.85f, BloomScatter = 0.72f, BloomTint = new Color(1f, 0.7f, 0.95f),
                Contrast = 18f, Saturation = 22f, ColorFilter = new Color(1f, 0.95f, 1f), WbTemperature = -6f, WbTint = 10f,
                Shadows = new Vector4(0.8f, 0.78f, 1.25f, 0f), Midtones = new Vector4(1f, 0.97f, 1.05f, 0f), Highlights = new Vector4(1.1f, 0.95f, 1.1f, 0f),
                VignetteIntensity = 0.3f,
            };
        }
    }

    /// <summary>The sun, the sky and the fog for a look (made last, after the camera, as the coffee builder always did).</summary>
    public static void FinishLook(Look l) { MakeSun(l); MakeAtmosphere(l); }

    // ---------- the stage ----------

    /// <summary>One stage block. The defaults are the coffee street's outdoor stage (deck 9 x 6 at x 9, z 10, facing the camera).</summary>
    public sealed class StageSpec
    {
        public string Prefix = "";                                  // material name prefix ("" keeps coffee's names)
        public float Cx = 9f, Cz = 10f, Width = 9f, Depth = 6f, Height = 0.5f;
        public string Deck = "#2d2fa0", Trim = "#f98bb0", Wall = "#4a3d9a", Dark = "#1d1e4a", Truss = "#7d82a8", Gold = "#ffd23a", StarEdge = "#fdb1cb", Ring = "#a8a9fc";
        public Color TrimGlow = new Color(0.55f, 0.12f, 0.3f);
        public bool BackWall = true, BannerStars = true, TrussRig = true, Speakers = true;
        public float WallHeight = 6.6f, TrussHeight = 6.4f;
        public float RampLength = 0.8f;
        public Color[] Lens = { new Color(3f, 0.7f, 1.6f), new Color(1.2f, 1.4f, 3.4f), new Color(3.2f, 2.5f, 0.8f), new Color(0.8f, 3f, 1.8f), new Color(3f, 0.7f, 1.6f), new Color(1.2f, 1.4f, 3.4f) };
        public float SpotHeight = 6.1f;
        public float BeamLength = 6.5f, BeamRadius = 1.25f;
        public float SpotStart = float.NaN, SpotSpacing = float.NaN;   // NaN = spread over the deck
        public float SpeakerOffset = 3.5f;
    }

    public static void Stage(Transform t, Transform blocks, StageSpec s)
    {
        string p = s.Prefix;
        var deck = Lit(p + "stage_deck", C(s.Deck), 0.35f);
        var pink = Lit(p + "stage_pink", C(s.Trim), 0.3f, s.TrimGlow);
        var wall = Lit(p + "stage_wall", C(s.Wall), 0.2f);
        var dark = Lit(p + "stage_dark", C(s.Dark), 0.4f);
        var truss = Lit(p + "stage_truss", C(s.Truss), 0.55f);
        float cx = s.Cx, cz = s.Cz, h = s.Height, halfW = s.Width / 2f, halfD = s.Depth / 2f;
        float front = cz - halfD, backZ = cz + halfD;

        Prim(PrimitiveType.Cube, "Deck", t, V(cx, h / 2, cz), V(s.Width, h, s.Depth), deck);
        Prim(PrimitiveType.Cube, "DeckLip", t, V(cx, h - 0.04f, front - 0.02f), V(s.Width + 0.04f, 0.12f, 0.1f), pink);
        // ramp up the front, the lift the walkers get from HeightZone below
        float rl = s.RampLength, len = Mathf.Sqrt(rl * rl + h * h), ang = Mathf.Atan2(h, rl);
        Vector3 up = V(0, Mathf.Cos(ang), -Mathf.Sin(ang));
        Vector3 mid = V(cx, h / 2f, front - rl / 2f) - up * 0.05f;
        Prim(PrimitiveType.Cube, "Ramp", t, mid, V(s.Width, 0.1f, len), deck, V(-ang * Mathf.Rad2Deg, 0, 0));
        var zone = new GameObject("HeightZone").AddComponent<HeightZone>();
        zone.transform.SetParent(t, false);
        zone.transform.position = V(cx, 0, cz);
        zone.height = h; zone.rampLength = rl; zone.halfSize = new Vector2(halfW, halfD);

        var starMesh = AssetDatabase.LoadAssetAtPath<Mesh>(WorldArtBuilder.MeshDir + "/MarkerStar.asset");
        if (s.BackWall)
        {
            Prim(PrimitiveType.Cube, "BackWall", t, V(cx, 3.8f * s.WallHeight / 6.6f, backZ + 0.2f), V(s.Width + 0.4f, s.WallHeight, 0.4f), wall);
            Prim(PrimitiveType.Cube, "BackWallTrim", t, V(cx, s.WallHeight + 0.55f, backZ), V(s.Width + 0.6f, 0.4f, 0.8f), pink);
        }
        var gold = Lit(p + "stage_star", C(s.Gold), 0.3f, new Color(1.5f, 1.0f, 0.25f));
        if (s.BannerStars && s.BackWall)
        {
            float k = s.WallHeight / 6.6f;
            StarSign("BannerStar", t, V(cx, 4.3f * k, backZ - 0.1f), 3.4f * k, Lit(p + "stage_star_edge", C(s.StarEdge), 0.3f, new Color(0.7f, 0.2f, 0.4f)));
            StarSign("BannerStar", t, V(cx, 4.3f * k, backZ - 0.3f), 2.7f * k, gold);
            StarSign("BannerStar", t, V(cx - 3.2f, 3.3f * k, backZ - 0.1f), 1.1f * k, gold);
            StarSign("BannerStar", t, V(cx + 3.2f, 3.3f * k, backZ - 0.1f), 1.1f * k, gold);
        }
        // a star on the deck marks where she performs
        var deckStar = new GameObject("DeckStar");
        deckStar.transform.SetParent(t, false);
        deckStar.transform.localPosition = V(cx, h + 0.012f, cz - 0.8f);
        deckStar.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        deckStar.transform.localScale = V(1.9f, 1.9f, 0.02f);
        deckStar.AddComponent<MeshFilter>().sharedMesh = starMesh;
        deckStar.AddComponent<MeshRenderer>().sharedMaterial = pink;

        float trussY = s.TrussHeight, frontZ = front + 0.5f;   // the beam stands 0.5 m inside the deck's front edge
        if (s.TrussRig)
        {
            void Rod(Vector3 a, Vector3 b, float r)
            {
                var mp = (a + b) / 2f; var d = b - a;
                var g = Prim(PrimitiveType.Cylinder, "Rod", t, mp, V(r * 2f, d.magnitude / 2f, r * 2f), truss);
                g.transform.up = d.normalized;
            }
            void TrussBeam(Vector3 a, Vector3 b, Vector3 upv, float width)
            {
                Vector3 top = upv * width / 2f, bot = -upv * width / 2f;
                Rod(a + top, b + top, 0.05f); Rod(a + bot, b + bot, 0.05f);
                int n = Mathf.Max(2, Mathf.RoundToInt((b - a).magnitude / width));
                for (int i = 0; i < n; i++)
                {
                    var p0 = Vector3.Lerp(a, b, i / (float)n); var p1 = Vector3.Lerp(a, b, (i + 1) / (float)n);
                    if (i % 2 == 0) Rod(p0 + bot, p1 + top, 0.03f); else Rod(p0 + top, p1 + bot, 0.03f);
                }
            }
            float x0 = cx - halfW + 0.2f, x1 = cx + halfW - 0.2f;
            TrussBeam(V(x0, trussY, frontZ), V(x1, trussY, frontZ), Vector3.up, 0.6f);
            TrussBeam(V(x0, h, frontZ), V(x0, trussY, frontZ), Vector3.forward, 0.5f);
            TrussBeam(V(x1, h, frontZ), V(x1, trussY, frontZ), Vector3.forward, 0.5f);
            TrussBeam(V(x0, trussY, frontZ), V(x0, trussY, backZ - 0.2f), Vector3.up, 0.5f);
            TrussBeam(V(x1, trussY, frontZ), V(x1, trussY, backZ - 0.2f), Vector3.up, 0.5f);
        }

        if (s.Speakers)
        {
            var woofer = Lit(p + "woofer", C("#2e2f55"), 0.4f);
            var ring = Lit(p + "woofer_ring", C(s.Ring), 0.4f);
            foreach (float sx in new[] { cx - s.SpeakerOffset, cx + s.SpeakerOffset })
            {
                Prim(PrimitiveType.Cube, "Speaker", t, V(sx, h + 0.95f, front + 1.3f), V(1.1f, 1.9f, 0.9f), dark);
                foreach (var wy in new[] { 0.55f, 1.35f })
                    Prim(PrimitiveType.Cylinder, "Woofer", t, V(sx, h + wy, front + 0.83f), V(0.7f, 0.02f, 0.7f), woofer, V(90, 0, 0));
                Prim(PrimitiveType.Cylinder, "WooferRing", t, V(sx, h + 0.55f, front + 0.835f), V(0.8f, 0.015f, 0.8f), ring, V(90, 0, 0));
                Prim(PrimitiveType.Cylinder, "WooferRing", t, V(sx, h + 1.35f, front + 0.835f), V(0.8f, 0.015f, 0.8f), ring, V(90, 0, 0));
                Block(blocks, sx, front + 1.3f, 0.75f);
            }
        }
        // mic stand in front of the spot
        Prim(PrimitiveType.Cylinder, "MicBase", t, V(cx + 0.7f, h + 0.02f, cz - 1.1f), V(0.4f, 0.02f, 0.4f), dark);
        Prim(PrimitiveType.Cylinder, "MicPole", t, V(cx + 0.7f, h + 0.75f, cz - 1.1f), V(0.035f, 0.75f, 0.035f), truss);
        Prim(PrimitiveType.Sphere, "MicHead", t, V(cx + 0.7f, h + 1.55f, cz - 1.15f), V(0.16f, 0.16f, 0.16f), Lit(p + "mic_head", C("#fff8ee"), 0.5f));

        // spotlights hanging from the beam, with soft beams down onto the deck. They are named Spot0, Spot1 ... (StageLights flares the "Beam" children)
        var coneMat = BeamMaterial();
        var housing = Lit(p + "spot_housing", C("#26274f"), 0.5f);
        int n6 = s.Lens.Length;
        float spacing = float.IsNaN(s.SpotSpacing) ? (s.Width - 0.6f) / Mathf.Max(1, n6 - 1) : s.SpotSpacing;
        float spot0 = float.IsNaN(s.SpotStart) ? cx - halfW + 0.4f : s.SpotStart;
        for (int i = 0; i < n6; i++)
        {
            float x = spot0 + i * spacing;
            var spot = Group("Spot" + i, t).transform;
            spot.position = V(x, s.SpotHeight, frontZ + 0.05f);
            spot.rotation = Quaternion.Euler(52f + (i % 2) * 6f, (i - (n6 - 1) / 2f) * 6f, 0);
            Prim(PrimitiveType.Cylinder, "Housing", spot, V(0, 0, 0.25f), V(0.42f, 0.28f, 0.42f), housing, V(90, 0, 0));
            Prim(PrimitiveType.Cylinder, "Lens", spot, V(0, 0, 0.55f), V(0.34f, 0.02f, 0.34f), Lit(p + "spot_lens" + i, Color.white, 0.2f, s.Lens[i]), V(90, 0, 0), false);
            var beam = new GameObject("Beam");
            beam.transform.SetParent(spot, false);
            beam.transform.localPosition = V(0, 0, 0.57f);
            beam.AddComponent<MeshFilter>().sharedMesh = ConeMesh(s.BeamLength, s.BeamRadius, s.Lens[i]);
            var br = beam.AddComponent<MeshRenderer>();
            br.sharedMaterial = coneMat;
            br.shadowCastingMode = ShadowCastingMode.Off;
            br.receiveShadows = false;
        }

        // keep walkers off the deck sides and the back wall
        for (float z = front + 0.6f; z <= backZ - 0.4f; z += 1.5f)
        {
            Block(blocks, cx - halfW - 1.1f, z, 0.5f);
        }
        for (float x = cx - halfW + 0.5f; x <= cx + halfW + 0.6f; x += 1.4f) Block(blocks, x, backZ + 0.95f, 0.6f);
    }

    public static Material BeamMaterial()
    {
        string path = WorldArtBuilder.MatDir + "/beam.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m, path); }
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 2f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.SetColor("_BaseColor", new Color(1f, 1f, 1f, 1f));
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>An open cone, apex at the origin, opening along +z; vertex colour fades from the apex out.</summary>
    public static Mesh ConeMesh(float length, float radius, Color tint)
    {
        const int seg = 24;
        var v = new List<Vector3>(); var c = new List<Color>(); var tri = new List<int>();
        v.Add(Vector3.zero); c.Add(new Color(tint.r * 0.22f, tint.g * 0.22f, tint.b * 0.22f, 0.24f));
        for (int i = 0; i < seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            v.Add(V(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, length));
            c.Add(new Color(tint.r * 0.1f, tint.g * 0.1f, tint.b * 0.1f, 0f));
        }
        for (int i = 0; i < seg; i++) { tri.Add(0); tri.Add(1 + i); tri.Add(1 + (i + 1) % seg); }
        var m = new Mesh { name = "BeamCone" };
        m.SetVertices(v); m.SetColors(c); m.SetTriangles(tri, 0);
        m.RecalculateBounds();
        string path = WorldArtBuilder.MeshDir + "/BeamCone_" + ColorUtility.ToHtmlStringRGB(new Color(tint.r / 4f, tint.g / 4f, tint.b / 4f)) + "_" + length.ToString("0.0") + "_" + radius.ToString("0.00") + ".asset";
        if (length == 6.5f && radius == 1.25f)   // the coffee stage's original names
            path = WorldArtBuilder.MeshDir + "/BeamCone_" + ColorUtility.ToHtmlStringRGB(new Color(tint.r / 4f, tint.g / 4f, tint.b / 4f)) + ".asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old != null) { EditorUtility.CopySerialized(m, old); Object.DestroyImmediate(m); return old; }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // ---------- the gameplay contract ----------

    /// <summary>Where everything the gameplay binds by name goes, for one place.</summary>
    public sealed class Contract
    {
        public string PlaceId = "coffee";
        public Vector3 Spawn = new Vector3(0, 0, -7.6f);
        public Vector3 StageSpot = new Vector3(9f, 0f, 9.2f);
        public Vector3 CrowdCenter = new Vector3(9f, 0f, 3.3f);
        public Vector2 CrowdSize = new Vector2(8.2f, 4.3f);
        public Vector3 Exit = new Vector3(11.5f, 0f, -9.5f);
        public float ExitArrowYaw = 0f;             // 0 points east (+x)
        public int RouteIndex = 0;                  // this place's position on the tour map signpost (0 .. 7)
        public Vector3[] Crew = new Vector3[0];
        public float[] CrewYaw;                     // null = face the camera, fanned a little
        public Vector3 Dog = new Vector3(-1.8f, 0f, -9.6f);
        public Vector3 CameraOffset = new Vector3(0, 6.6f, -11.4f);
    }

    /// <summary>
    /// The contract empties (PlayerSpawn, StageSpot, CrowdArea, ExitSpot with its tour map signpost, Crew_1..N with markers), the camera and post volume,
    /// the Player with the Star, a click marker, Riff, the follow camera and the spike status. Returns the main camera.
    /// </summary>
    public static Camera Populate(Contract c, Look look, VolumeProfile profile, Transform blocks)
    {
        // the contract empties (the gameplay finds these by name)
        var spawn = Group("PlayerSpawn").transform; spawn.position = c.Spawn;
        var stageSpot = Group("StageSpot").transform; stageSpot.position = c.StageSpot;   // ground level: the gameplay turns clicks into ground points and flat distances
        var crowd = Group("CrowdArea").transform; crowd.position = c.CrowdCenter; crowd.localScale = V(c.CrowdSize.x, 1f, c.CrowdSize.y);
        var exit = Group("ExitSpot").transform; exit.position = c.Exit;
        MakeExit(exit, c.RouteIndex, c.ExitArrowYaw);
        var info = Group("PlaceInfo").AddComponent<WorldPlace>();
        info.placeId = c.PlaceId;

        var markerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorldArtBuilder.MarkerPrefab);
        for (int i = 0; i < c.Crew.Length; i++)
        {
            var root = Group("Crew_" + (i + 1)).transform;
            root.position = c.Crew[i];
            var crew = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WorldArtBuilder.CrewPrefabs[i % WorldArtBuilder.CrewPrefabs.Length]), root);
            crew.transform.localPosition = Vector3.zero;
            float yaw = c.CrewYaw != null && i < c.CrewYaw.Length ? c.CrewYaw[i] : 180f - 14f + i * 14f;
            crew.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var mk = (GameObject)PrefabUtility.InstantiatePrefab(markerPrefab, root);
            mk.transform.localPosition = V(0, 3.65f, 0);   // above the name tag, which hangs under the bubble
            Block(blocks, c.Crew[i].x, c.Crew[i].z, 0.45f);
        }

        var cam = MakeCamera(profile, look);
        var player = Group("Player").transform;
        player.position = spawn.position;
        var star = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WorldArtBuilder.StarPrefab), player);
        star.transform.localPosition = Vector3.zero;
        var pc = player.gameObject.AddComponent<PlayerController>();
        pc.cam = cam;
        pc.animator = star.GetComponent<Animator>();
        var click = Prim(PrimitiveType.Cylinder, "ClickMarker", null, V(0, 0.03f, 0), V(0.7f, 0.02f, 0.7f),
            Lit("click_marker", C("#ff8cc0"), 0.1f, new Color(1.6f, 0.5f, 0.9f)), null, false);
        click.SetActive(false);
        pc.marker = click.transform;

        var dogGo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WorldArtBuilder.RiffPrefab));
        dogGo.name = "Riff";
        dogGo.transform.position = c.Dog;
        dogGo.GetComponent<DogFollower>().target = player;

        var follow = cam.gameObject.AddComponent<FollowCamera>();
        follow.target = player;
        follow.offset = c.CameraOffset;
        follow.tiltUp = 9f;
        follow.lead = 1.2f;
        follow.clearRadius = 0.9f;

        var st = new GameObject("SpikeStatus").AddComponent<SpikeStatus>();
        st.player = player; st.dog = dogGo.transform;
        return cam;
    }

    /// <summary>
    /// The exit: a lilac ring on the ground with a white arrow, and the tour map signpost 1.7 m to its east (the gameplay's "next stop" pin hangs over
    /// it). The board shows the eight stops as a dotted road with this stop marked.
    /// </summary>
    public static void MakeExit(Transform exit, int routeIndex, float arrowYaw)
    {
        var ring = Lit("exit_ring", C("#a8a9fc"), 0.1f, new Color(0.9f, 0.9f, 2.2f));
        Prim(PrimitiveType.Cylinder, "Ring", exit, V(0, 0.03f, 0), V(2.4f, 0.015f, 2.4f), ring, null, false);
        Prim(PrimitiveType.Cylinder, "RingHole", exit, V(0, 0.04f, 0), V(1.8f, 0.015f, 1.8f), Lit("exit_hole", C("#fdb1cb"), 0.1f), null, false);
        // an arrow on the ground
        var arrowRoot = Group("Arrow", exit).transform;
        arrowRoot.localRotation = Quaternion.Euler(0, -arrowYaw, 0);
        var arrow = Lit("exit_arrow", C("#ffffff"), 0.1f, new Color(1.2f, 1.2f, 1.4f));
        Prim(PrimitiveType.Cube, "ArrowShaft", arrowRoot, V(-0.15f, 0.06f, 0), V(0.9f, 0.015f, 0.26f), arrow, null, false);
        Prim(PrimitiveType.Cube, "ArrowHeadA", arrowRoot, V(0.38f, 0.06f, 0.13f), V(0.6f, 0.015f, 0.2f), arrow, V(0, 35, 0), false);
        Prim(PrimitiveType.Cube, "ArrowHeadB", arrowRoot, V(0.38f, 0.06f, -0.13f), V(0.6f, 0.015f, 0.2f), arrow, V(0, -35, 0), false);
        // the tour map signpost
        var sign = Group("TourMapSign", exit).transform;
        sign.localPosition = V(1.7f, 0f, 0.3f);
        var pole = Lit("sign_pole", C("#3a3a68"), 0.4f);
        Prim(PrimitiveType.Cylinder, "Pole", sign, V(0f, 1.4f, 0f), V(0.12f, 1.4f, 0.12f), pole);
        Prim(PrimitiveType.Cube, "Board", sign, V(0f, 2.65f, 0f), V(1.9f, 0.95f, 0.1f), Lit("sign_board", C("#f98bb0"), 0.2f));
        var white = Lit("sign_arrow", C("#ffffff"), 0.2f, new Color(0.5f, 0.5f, 0.5f));
        var past = Lit("sign_dot_past", C("#a8a9fc"), 0.2f, new Color(0.25f, 0.25f, 0.55f));
        var next = Lit("sign_dot_next", C("#2f2fc1"), 0.2f);
        var here = Lit("sign_dot_here", C("#ffd23a"), 0.2f, new Color(1.2f, 0.9f, 0.2f));
        // the road: a white line along the lower part of the board with eight dots on it, this stop big and gold
        Prim(PrimitiveType.Cube, "Road", sign, V(0f, 2.45f, -0.06f), V(1.6f, 0.05f, 0.02f), white, null, false);
        for (int i = 0; i < 8; i++)
        {
            float d = i == routeIndex ? 0.28f : 0.13f;
            Material m = i == routeIndex ? here : i < routeIndex ? past : next;
            Prim(PrimitiveType.Cylinder, "Stop" + i, sign, V((-3.5f + i) * 0.22f, 2.45f, -0.07f), V(d, 0.012f, d), m, V(90, 0, 0), false);
        }
        PixelText(sign, "TOUR", V(0f, 2.86f, -0.065f), 0.045f, white, "pk_text_tour", 0.5f);
    }

    // ---------- particles ----------

    public static Material ParticleMaterial(string name, bool additive, Texture2D tex, Color tint)
    {
        var m = LoadOrNew(name, "Universal Render Pipeline/Particles/Unlit");
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", additive ? 2f : 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f); if (!additive) m.SetFloat("_Cull", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", tint);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>Soft glowing motes drifting in a box (dust in a sunbeam, embers, sparkle). Additive, no shadows.</summary>
    public static ParticleSystem Motes(string name, Vector3 center, Vector3 box, Color tint, Color startColor, int max, float rate, Vector2 size, Vector2 life, float noise = 0.6f, float rise = 0f, string matName = null)
    {
        var dot = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/World/Textures/soft_dot.png");
        var mm = ParticleMaterial(matName ?? name + "_mat", true, dot, tint);
        var g = new GameObject(name);
        g.transform.position = center;
        var ps = g.AddComponent<ParticleSystem>();
        var r = g.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mm; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        var m = ps.main;
        m.loop = true; m.prewarm = true;
        m.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
        m.startSpeed = 0.05f;
        m.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        m.startColor = startColor;
        m.maxParticles = max;
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        var e = ps.emission; e.rateOverTime = rate;
        var s = ps.shape; s.shapeType = ParticleSystemShapeType.Box; s.scale = box;
        var n = ps.noise; n.enabled = true; n.strength = noise; n.frequency = 0.3f; n.scrollSpeed = 0.25f;
        if (rise != 0f)
        {
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f); vel.y = new ParticleSystem.MinMaxCurve(rise * 0.6f, rise); vel.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
        }
        var co = ps.colorOverLifetime; co.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0.3f, 0.65f), new GradientAlphaKey(0f, 1f) });
        co.color = grad;
        return ps;
    }

    public static Texture2D LeafTexture()
    {
        string path = WorldArtBuilder.TexDir + "/leaf.png";
        const int n = 64;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // a pointed oval along the diagonal, with a lighter midrib
                float u = (x - n / 2f) / (n / 2f), v = (y - n / 2f) / (n / 2f);
                float a = (u + v) * 0.7071f, b = (u - v) * 0.7071f;
                float len = 0.95f, wid = 0.48f * Mathf.Sqrt(Mathf.Max(0f, 1f - (a / len) * (a / len)));
                float inside = Mathf.Abs(b) < wid ? 1f : 0f;
                float alpha = inside * Mathf.Clamp01((wid - Mathf.Abs(b)) * 14f + 0.2f);
                float rib = Mathf.Abs(b) < 0.04f ? 1f : 0f;
                t.SetPixel(x, y, new Color(1f - rib * 0.0f, 1f - rib * 0.25f, 1f - rib * 0.25f, alpha));
            }
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.alphaIsTransparency = true; imp.mipmapEnabled = false; imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>Leaves or petals drifting down (two colours), over the whole place.</summary>
    public static ParticleSystem Leaves(string name, Vector3 center, Vector3 box, string colorA, string colorB, int max = 70, float rate = 6f)
    {
        var lm = ParticleMaterial("leaf_particle", false, LeafTexture(), Color.white);
        var g = new GameObject(name);
        g.transform.position = center;
        var ps = g.AddComponent<ParticleSystem>();
        var r = g.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = lm;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        var main = ps.main;
        main.loop = true; main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 12f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.36f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(C(colorA), C(colorB));
        main.gravityModifier = 0.05f;
        main.maxParticles = max;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = rate;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = box;
        var vel = ps.velocityOverLifetime; vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.5f); vel.y = new ParticleSystem.MinMaxCurve(-0.5f, -0.2f); vel.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.8f; noise.frequency = 0.25f; noise.scrollSpeed = 0.2f;
        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
        return ps;
    }

    // ---------- scenes ----------

    public static Scene NewScene()
    {
        return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    /// <summary>
    /// Saves the scene and refuses a scene that carries an embedded MonoScript (yaml class 115): that happens when a MonoBehaviour's class is not the only class
    /// in a file of the same name, and the web build then reads the level as corrupted ("The file 'levelN' is corrupted").
    /// </summary>
    public static void SaveScene(Scene scene, string path)
    {
        EditorSceneManager.SaveScene(scene, path);
        string text = File.ReadAllText(path);
        if (text.Contains("--- !u!115 ")) throw new System.Exception(path + " has an embedded MonoScript: put each MonoBehaviour in a file named after its class");
    }

    /// <summary>Runs one builder step; logs and exits (batch mode) with the result as the place builders' entry points do.</summary>
    public static void Entry(string label, System.Action build)
    {
        int code = 0;
        try
        {
            // the shared prefabs (Star, crew, Riff, markers, crowd) are built once; re-running WorldArtBuilder.Run rewrites them with fresh ids
            if (!File.Exists(WorldArtBuilder.StarPrefab) || !File.Exists(WorldArtBuilder.RiffPrefab) || !File.Exists(WorldArtBuilder.MarkerPrefab)) WorldArtBuilder.Run();
            else { WorldArtImport.ApplySettings(); AssetDatabase.Refresh(); }
            Directory.CreateDirectory("Assets/World/Scenes");
            build();
            AssetDatabase.SaveAssets();
            Debug.Log(label + ": done");
        }
        catch (System.Exception e)
        {
            Debug.LogError(label + " failed: " + e);
            code = 1;
        }
        if (Application.isBatchMode) EditorApplication.Exit(code);
    }
}
