// Phase 8.2 builder B: the helpers the theater, festival and arena builders share (copied from PlaceCoffeeBuilder / PlaceCoffeeDressing,
// which stay untouched). Every material this file or its callers make is named with a place prefix (th_, fe_, ar_, pb_), because
// WorldArtBuilder.Lit rewrites a material of the same name in place and Place_Coffee's must not change.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WorldTour.World;
using WorldTour.World.Gameplay;
using WorldTour.World.Places;

public static class PlaceBKit
{
    public const string SettingsDir = "Assets/World/Settings";
    public const string TexOut = "Assets/World/Textures/Art";
    public const string City = "Assets/World/Art/KayKit_City/";
    public const string Resto = "Assets/World/Art/KayKit_Restaurant/";
    public const string Forest = "Assets/World/Art/KayKit_Forest/";
    public const string Furn = "Assets/World/Art/KayKit_Furniture/";
    public const float CityScale = 4f;

    public static readonly HashSet<string> Reported = new HashSet<string>();

    public static Color C(string hex) { return WorldArtBuilder.C(hex); }
    public static Vector3 V(float x, float y, float z) { return new Vector3(x, y, z); }
    public static Material Lit(string n, Color c, float s = 0.15f, Color? e = null) { return WorldArtBuilder.Lit(n, c, s, null, e); }
    public static Material LitTex(string n, Color c, Texture2D t, float s = 0.12f) { return WorldArtBuilder.Lit(n, c, s, t, null); }

    public static Material DoubleSided(string n, Color c, float s = 0.2f, Color? e = null)
    {
        var m = Lit(n, c, s, e);
        m.SetFloat("_Cull", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>A KayKit pack material: the pack's palette texture on URP Lit.</summary>
    public static Material Kit(string name, string texturePath, float s = 0.12f)
    {
        return LitTex(name, Color.white, WorldArtBuilder.ArtTexture(texturePath), s);
    }

    // ---------- objects ----------

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
        UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        if (euler.HasValue) g.transform.localRotation = Quaternion.Euler(euler.Value);
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        if (!shadows) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
        return g;
    }

    public static GameObject Fbx(string path, Transform parent, Vector3 pos, float yaw, float scale, Material mat, string name = null)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (src == null) throw new Exception("missing " + path);
        var g = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
        g.name = name ?? src.name;
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(0, yaw, 0) * src.transform.localRotation;
        g.transform.localScale = src.transform.localScale * scale;
        foreach (var r in g.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
        }
        return g;
    }

    public static void Block(Transform parent, float x, float z, float radius)
    {
        var g = new GameObject("Block");
        g.transform.SetParent(parent, false);
        g.transform.position = V(x, 0, z);
        g.AddComponent<Obstacle>().radius = radius;
    }

    /// <summary>A row of blockers along a line, so walkers keep off a wall or a table.</summary>
    public static void BlockLine(Transform parent, Vector3 a, Vector3 b, float radius)
    {
        float len = Vector3.Distance(a, b);
        int n = Mathf.Max(1, Mathf.CeilToInt(len / (radius * 1.7f)));
        for (int i = 0; i <= n; i++) { var p = Vector3.Lerp(a, b, i / (float)n); Block(parent, p.x, p.z, radius); }
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

    public static void StaticBatch(Transform root)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.GetComponent<Obstacle>() != null || t.GetComponent<PlaceMotion>() != null) continue;
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
        }
    }

    // ---------- generated meshes ----------

    /// <summary>A growing mesh. Gems share their six vertices (soft, for people and bulbs); quads and boxes do not (flat, for cloth and walls).</summary>
    public sealed class MB
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int> t = new List<int>();

        public int Count { get { return v.Count; } }

        void Tri(int a, int b, int c) { t.Add(a); t.Add(b); t.Add(c); }

        public void Gem(Vector3 c, Vector3 ax, Vector3 ay, Vector3 az)
        {
            if (Vector3.Dot(Vector3.Cross(ax, ay), az) < 0f) ay = -ay;
            int i = v.Count;
            v.Add(c + ax); v.Add(c - ax); v.Add(c + ay); v.Add(c - ay); v.Add(c + az); v.Add(c - az);
            for (int k = 0; k < 6; k++) uv.Add(Vector2.zero);
            // 0 +x, 1 -x, 2 +y, 3 -y, 4 +z, 5 -z
            Tri(i + 2, i + 4, i + 0); Tri(i + 2, i + 1, i + 4); Tri(i + 2, i + 5, i + 1); Tri(i + 2, i + 0, i + 5);
            Tri(i + 3, i + 0, i + 4); Tri(i + 3, i + 4, i + 1); Tri(i + 3, i + 1, i + 5); Tri(i + 3, i + 5, i + 0);
        }

        public void Gem(Vector3 c, float rx, float ry, float rz) { Gem(c, Vector3.right * rx, Vector3.up * ry, Vector3.forward * rz); }

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

        /// <summary>A flat triangle with its own vertices (bunting flag, tent panel).</summary>
        public void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(0.5f, 1));
            Tri(i, i + 1, i + 2);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
            Tri(i, i + 1, i + 2); Tri(i, i + 2, i + 3);
        }

        /// <summary>A box with hard edges, axis aligned, rotated by yaw about its own centre.</summary>
        public void Box(Vector3 c, Vector3 size, float yaw = 0f)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            Vector3 hx = q * V(size.x / 2, 0, 0), hy = V(0, size.y / 2, 0), hz = q * V(0, 0, size.z / 2);
            Quad(c - hx - hz + hy, c + hx - hz + hy, c + hx + hz + hy, c - hx + hz + hy);   // top
            Quad(c - hx - hz - hy, c - hx + hz - hy, c + hx + hz - hy, c + hx - hz - hy);   // bottom
            Quad(c - hx - hz - hy, c + hx - hz - hy, c + hx - hz + hy, c - hx - hz + hy);   // -z
            Quad(c + hx + hz - hy, c - hx + hz - hy, c - hx + hz + hy, c + hx + hz + hy);   // +z
            Quad(c - hx + hz - hy, c - hx - hz - hy, c - hx - hz + hy, c - hx + hz + hy);   // -x
            Quad(c + hx - hz - hy, c + hx + hz - hy, c + hx + hz + hy, c + hx - hz + hy);   // +x
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            string path = WorldArtBuilder.MeshDir + "/" + name + ".asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null) { EditorUtility.CopySerialized(m, old); UnityEngine.Object.DestroyImmediate(m); return old; }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }

    // ---------- textures ----------

    /// <summary>A generated RGBA texture saved as a PNG under Assets/World/Textures/Art.</summary>
    public static Texture2D MakeTexture(string file, int w, int h, Func<int, int, Color> pixel, TextureWrapMode wrap, FilterMode filter, bool mips)
    {
        Directory.CreateDirectory(TexOut);
        string path = TexOut + "/" + file + ".png";
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = pixel(x, y);
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.SetPixels32(px);
        File.WriteAllBytes(path, t.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = wrap; imp.mipmapEnabled = mips; imp.filterMode = filter; imp.maxTextureSize = 512; imp.anisoLevel = mips ? 8 : 1;
        imp.alphaIsTransparency = false;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ---------- the look: profile, sky, light, camera ----------

    public sealed class Grade
    {
        public float BloomThreshold = 1f, BloomIntensity = 0.4f, BloomScatter = 0.6f;
        public Color BloomTint = new Color(1f, 0.92f, 0.85f);
        public float Exposure, Contrast = 16f, Saturation = 16f, Temperature = 6f, Tint = 3f, Vignette = 0.2f;
        public Color Filter = new Color(1f, 0.985f, 0.96f);
        public Vector4 Shadows = new Vector4(0.86f, 0.9f, 1.12f, 0f), Highlights = new Vector4(1.08f, 1f, 0.9f, 0f);
    }

    public static VolumeProfile MakeProfile(string file, Grade g)
    {
        string path = SettingsDir + "/" + file + ".asset";
        AssetDatabase.DeleteAsset(path);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);
        var bloom = profile.Add<Bloom>();
        bloom.threshold.Override(g.BloomThreshold); bloom.intensity.Override(g.BloomIntensity); bloom.scatter.Override(g.BloomScatter);
        bloom.tint.Override(g.BloomTint); bloom.highQualityFiltering.Override(false);
        profile.Add<Tonemapping>().mode.Override(TonemappingMode.Neutral);
        var grade = profile.Add<ColorAdjustments>();
        grade.postExposure.Override(g.Exposure); grade.contrast.Override(g.Contrast); grade.saturation.Override(g.Saturation); grade.colorFilter.Override(g.Filter);
        var wb = profile.Add<WhiteBalance>();
        wb.temperature.Override(g.Temperature); wb.tint.Override(g.Tint);
        var smh = profile.Add<ShadowsMidtonesHighlights>();
        smh.shadows.Override(g.Shadows); smh.midtones.Override(new Vector4(1f, 1f, 1f, 0f)); smh.highlights.Override(g.Highlights);
        smh.shadowsStart.Override(0f); smh.shadowsEnd.Override(0.4f); smh.highlightsStart.Override(0.55f); smh.highlightsEnd.Override(1f);
        var vig = profile.Add<Vignette>();
        vig.intensity.Override(g.Vignette); vig.smoothness.Override(0.5f);
        foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
        EditorUtility.SetDirty(profile);
        return profile;
    }

    public sealed class Air
    {
        public string SkyName = "pb_sky";
        public Color Top, Mid, Horizon, Bottom;
        public Vector3 SunEuler = new Vector3(36f, 52f, 0f);
        public Color AmbientSky, AmbientEquator, AmbientGround, Fog, Shade = new Color(0.42f, 0.48f, 0.68f);
        public float AmbientIntensity = 0.9f, FogDensity = 0.0075f;
    }

    public static void MakeAtmosphere(Air a)
    {
        string skyPath = WorldArtBuilder.MatDir + "/" + a.SkyName + ".mat";
        var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        var skyShader = Shader.Find("WorldTour/SkyGradient");
        if (sky == null) { sky = new Material(skyShader); AssetDatabase.CreateAsset(sky, skyPath); }
        sky.shader = skyShader;
        sky.SetColor("_Top", a.Top); sky.SetColor("_Mid", a.Mid); sky.SetColor("_Horizon", a.Horizon); sky.SetColor("_Bottom", a.Bottom);
        sky.SetVector("_SunDir", -(Quaternion.Euler(a.SunEuler) * Vector3.forward));
        EditorUtility.SetDirty(sky);
        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = a.AmbientSky; RenderSettings.ambientEquatorColor = a.AmbientEquator; RenderSettings.ambientGroundColor = a.AmbientGround;
        RenderSettings.ambientIntensity = a.AmbientIntensity;
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = a.FogDensity; RenderSettings.fogColor = a.Fog;
        RenderSettings.subtractiveShadowColor = a.Shade;
    }

    public static Light MakeSun(Color color, float intensity, Vector3 euler, float shadowStrength = 0.9f)
    {
        var g = new GameObject("Sun");
        var l = g.AddComponent<Light>();
        l.type = LightType.Directional; l.color = color; l.intensity = intensity;
        l.shadows = LightShadows.Soft; l.shadowStrength = shadowStrength; l.shadowBias = 0.05f; l.shadowNormalBias = 0.4f;
        g.transform.rotation = Quaternion.Euler(euler);
        RenderSettings.sun = l;
        return l;
    }

    public static Camera MakeCamera(VolumeProfile profile, Color bg)
    {
        var g = new GameObject("Main Camera");
        g.tag = "MainCamera";
        var c = g.AddComponent<Camera>();
        c.clearFlags = CameraClearFlags.Skybox; c.backgroundColor = bg;
        c.fieldOfView = 44f; c.nearClipPlane = 0.5f; c.farClipPlane = 150f;
        g.AddComponent<AudioListener>();
        var data = g.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true; data.renderShadows = true; data.antialiasing = AntialiasingMode.None;
        var vol = new GameObject("Post Volume").AddComponent<Volume>();
        vol.isGlobal = true; vol.sharedProfile = profile;
        return c;
    }

    // ---------- the gameplay contract ----------

    public sealed class Spec
    {
        public string Id, ScenePath;
        public Vector3 Spawn = V(0, 0, -7.6f), Stage, Crowd, Exit = V(11.5f, 0, -9.5f), Riff = V(-1.8f, 0, -9.6f);
        public Vector2 CrowdSize;
        public Vector3[] Crew;
        public float[] CrewYaw;
        public Color Bg = new Color(0.8f, 0.87f, 0.97f);
        public Color ExitRing = new Color(0.9f, 0.9f, 2.2f), ExitRingBase, ExitSign, ExitHole;
        public float ExitYaw;
    }

    public static void MakeExit(Transform exit, string prefix, Spec s)
    {
        var ring = Lit(prefix + "exit_ring", s.ExitRingBase, 0.1f, s.ExitRing);
        Prim(PrimitiveType.Cylinder, "Ring", exit, V(0, 0.03f, 0), V(2.4f, 0.015f, 2.4f), ring, null, false);
        Prim(PrimitiveType.Cylinder, "RingHole", exit, V(0, 0.04f, 0), V(1.8f, 0.015f, 1.8f), Lit(prefix + "exit_hole", s.ExitHole, 0.1f), null, false);
        var arrow = Lit(prefix + "exit_arrow", C("#ffffff"), 0.1f, new Color(1.2f, 1.2f, 1.4f));
        var a = Group("Arrow", exit).transform;
        a.localRotation = Quaternion.Euler(0, s.ExitYaw, 0);
        Prim(PrimitiveType.Cube, "ArrowShaft", a, V(-0.15f, 0.06f, 0), V(0.9f, 0.015f, 0.26f), arrow, null, false);
        Prim(PrimitiveType.Cube, "ArrowHeadA", a, V(0.38f, 0.06f, 0.13f), V(0.6f, 0.015f, 0.2f), arrow, V(0, 35, 0), false);
        Prim(PrimitiveType.Cube, "ArrowHeadB", a, V(0.38f, 0.06f, -0.13f), V(0.6f, 0.015f, 0.2f), arrow, V(0, -35, 0), false);
        var pole = Lit(prefix + "sign_pole", C("#3a3a68"), 0.4f);
        Prim(PrimitiveType.Cylinder, "Pole", exit, V(1.7f, 1.4f, 0.3f), V(0.12f, 1.4f, 0.12f), pole);
        var board = Prim(PrimitiveType.Cube, "Board", exit, V(1.7f, 2.65f, 0.3f), V(1.5f, 0.8f, 0.1f), Lit(prefix + "sign_board", s.ExitSign, 0.2f));
        Prim(PrimitiveType.Cube, "BoardArrow", board.transform, V(0f, 0f, -0.7f), V(0.5f, 0.12f, 0.4f), Lit(prefix + "sign_arrow", C("#ffffff"), 0.2f, new Color(0.5f, 0.5f, 0.5f)));
    }

    /// <summary>The empties the gameplay binds by name, the crew, the player, Riff, the camera rig and the venue marker. Returns the camera.</summary>
    public static Camera Contract(Spec s, Transform blocks, VolumeProfile profile, string prefix)
    {
        Group("Place").AddComponent<WorldPlace>().placeId = s.Id;
        var spawn = Group("PlayerSpawn").transform; spawn.position = s.Spawn;
        var stageSpot = Group("StageSpot").transform; stageSpot.position = s.Stage;
        var crowd = Group("CrowdArea").transform; crowd.position = s.Crowd; crowd.localScale = V(s.CrowdSize.x, 1f, s.CrowdSize.y);
        var exit = Group("ExitSpot").transform; exit.position = s.Exit;
        MakeExit(exit, prefix, s);

        var markerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorldArtBuilder.MarkerPrefab);
        for (int i = 0; i < s.Crew.Length; i++)
        {
            var root = Group("Crew_" + (i + 1)).transform;
            root.position = s.Crew[i];
            var crew = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WorldArtBuilder.CrewPrefabs[i % WorldArtBuilder.CrewPrefabs.Length]), root);
            crew.transform.localPosition = Vector3.zero;
            float yaw = s.CrewYaw != null && i < s.CrewYaw.Length ? s.CrewYaw[i] : 180f - 14f + i * 14f;
            crew.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var mk = (GameObject)PrefabUtility.InstantiatePrefab(markerPrefab, root);
            mk.transform.localPosition = V(0, 3.65f, 0);
            Block(blocks, s.Crew[i].x, s.Crew[i].z, 0.45f);
        }

        var cam = MakeCamera(profile, s.Bg);
        var player = Group("Player").transform;
        player.position = s.Spawn;
        var star = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WorldArtBuilder.StarPrefab), player);
        star.transform.localPosition = Vector3.zero;
        var pc = player.gameObject.AddComponent<PlayerController>();
        pc.cam = cam;
        pc.animator = star.GetComponent<Animator>();
        var click = Prim(PrimitiveType.Cylinder, "ClickMarker", null, V(0, 0.03f, 0), V(0.7f, 0.02f, 0.7f),
            Lit(prefix + "click_marker", C("#ff8cc0"), 0.1f, new Color(1.6f, 0.5f, 0.9f)), null, false);
        click.SetActive(false);
        pc.marker = click.transform;

        var dogGo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WorldArtBuilder.RiffPrefab));
        dogGo.name = "Riff";
        dogGo.transform.position = s.Riff;
        dogGo.GetComponent<DogFollower>().target = player;

        var follow = cam.gameObject.AddComponent<FollowCamera>();
        follow.target = player;
        follow.offset = V(0, 6.6f, -11.4f);
        follow.tiltUp = 9f;
        follow.lead = 1.2f;
        follow.clearRadius = 0.9f;

        var st = new GameObject("SpikeStatus").AddComponent<SpikeStatus>();
        st.player = player; st.dog = dogGo.transform;
        return cam;
    }

    // ---------- stage pieces ----------

    /// <summary>A raised deck with a ramp down its south side and the HeightZone that lifts the walkers. Returns the deck centre top y.</summary>
    public static void Deck(Transform t, Transform blocks, float cx, float cz, float halfW, float halfD, float h, Material deck, Material lip)
    {
        Prim(PrimitiveType.Cube, "Deck", t, V(cx, h / 2, cz), V(halfW * 2, h, halfD * 2), deck);
        float front = cz - halfD;
        Prim(PrimitiveType.Cube, "DeckLip", t, V(cx, h - 0.04f, front - 0.02f), V(halfW * 2 + 0.04f, 0.12f, 0.1f), lip);
        const float rl = 0.8f;
        float len = Mathf.Sqrt(rl * rl + h * h), ang = Mathf.Atan2(h, rl);
        Prim(PrimitiveType.Cube, "Ramp", t, V(cx, h / 2 - 0.05f * Mathf.Cos(ang), front - rl / 2 + 0.05f * Mathf.Sin(ang)), V(halfW * 2, 0.1f, len), deck, V(-ang * Mathf.Rad2Deg, 0, 0));
        var zone = new GameObject("HeightZone").AddComponent<HeightZone>();
        zone.transform.SetParent(t, false);
        zone.transform.position = V(cx, 0, cz);
        zone.height = h; zone.rampLength = rl; zone.halfSize = new Vector2(halfW, halfD);
        // keep walkers off the deck sides and back
        for (float z = front + 0.8f; z <= cz + halfD; z += 1.5f) { Block(blocks, cx - halfW - 1.1f, z, 0.5f); Block(blocks, cx + halfW + 1.1f, z, 0.5f); }
    }

    public static void Rod(Transform t, Material m, Vector3 a, Vector3 b, float r)
    {
        var mid = (a + b) / 2f; var d = b - a;
        var g = Prim(PrimitiveType.Cylinder, "Rod", t, mid, V(r * 2f, d.magnitude / 2f, r * 2f), m);
        g.transform.up = d.normalized;
    }

    public static void TrussBeam(Transform t, Material m, Vector3 a, Vector3 b, Vector3 up, float width)
    {
        Vector3 top = up * width / 2f, bot = -up * width / 2f;
        Rod(t, m, a + top, b + top, 0.05f); Rod(t, m, a + bot, b + bot, 0.05f);
        int n = Mathf.Max(2, Mathf.RoundToInt((b - a).magnitude / width));
        for (int i = 0; i < n; i++)
        {
            var p0 = Vector3.Lerp(a, b, i / (float)n); var p1 = Vector3.Lerp(a, b, (i + 1) / (float)n);
            if (i % 2 == 0) Rod(t, m, p0 + bot, p1 + top, 0.03f); else Rod(t, m, p0 + top, p1 + bot, 0.03f);
        }
    }

    public static void Speaker(Transform t, Transform blocks, Vector3 foot, Material body, Material cone, Material ring, float scale = 1f)
    {
        Prim(PrimitiveType.Cube, "Speaker", t, foot + V(0, 0.95f * scale, 0), V(1.1f * scale, 1.9f * scale, 0.9f * scale), body);
        foreach (var wy in new[] { 0.55f, 1.35f })
        {
            Prim(PrimitiveType.Cylinder, "Woofer", t, foot + V(0, wy * scale, -0.47f * scale), V(0.7f * scale, 0.02f, 0.7f * scale), cone, V(90, 0, 0));
            Prim(PrimitiveType.Cylinder, "WooferRing", t, foot + V(0, wy * scale, -0.475f * scale), V(0.8f * scale, 0.015f, 0.8f * scale), ring, V(90, 0, 0));
        }
        Block(blocks, foot.x, foot.z, 0.75f * scale);
    }

    public static Material BeamMaterial()
    {
        string path = WorldArtBuilder.MatDir + "/pb_beam.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m, path); }
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 2f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_ZWrite", 0f); m.SetFloat("_Cull", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(m);
        return m;
    }

    public static Mesh ConeMesh(float length, float radius, Color tint, float apexAlpha = 0.24f)
    {
        const int seg = 24;
        var v = new List<Vector3>(); var c = new List<Color>(); var tri = new List<int>();
        v.Add(Vector3.zero); c.Add(new Color(tint.r * 0.22f, tint.g * 0.22f, tint.b * 0.22f, apexAlpha));
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
        string p = WorldArtBuilder.MeshDir + "/PBBeam_" + ColorUtility.ToHtmlStringRGB(new Color(tint.r / 4f, tint.g / 4f, tint.b / 4f)) + "_" + Mathf.RoundToInt(length) + "_" + Mathf.RoundToInt(radius * 10) + "_" + Mathf.RoundToInt(apexAlpha * 100) + ".asset";
        m.name = Path.GetFileNameWithoutExtension(p);
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(p);
        if (old != null) { EditorUtility.CopySerialized(m, old); UnityEngine.Object.DestroyImmediate(m); return old; }
        AssetDatabase.CreateAsset(m, p);
        return m;
    }

    /// <summary>A hanging stage light named SpotN (the gameplay flares the "Beam" child of Spot0 to Spot11 on a right answer).</summary>
    public static Transform SpotLight(Transform parent, string name, Vector3 pos, Quaternion rot, Color lens, Material housing, Material lensMat, float beamLen, float beamRadius, float apexAlpha = 0.24f)
    {
        var spot = Group(name, parent).transform;
        spot.position = pos; spot.rotation = rot;
        Prim(PrimitiveType.Cylinder, "Housing", spot, V(0, 0, 0.25f), V(0.42f, 0.28f, 0.42f), housing, V(90, 0, 0));
        Prim(PrimitiveType.Cylinder, "Lens", spot, V(0, 0, 0.55f), V(0.34f, 0.02f, 0.34f), lensMat, V(90, 0, 0), false);
        var beam = new GameObject("Beam");
        beam.transform.SetParent(spot, false);
        beam.transform.localPosition = V(0, 0, 0.57f);
        beam.AddComponent<MeshFilter>().sharedMesh = ConeMesh(beamLen, beamRadius, lens, apexAlpha);
        var br = beam.AddComponent<MeshRenderer>();
        br.sharedMaterial = BeamMaterial();
        br.shadowCastingMode = ShadowCastingMode.Off; br.receiveShadows = false;
        return spot;
    }

    // ---------- bulbs, wire, bunting ----------

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

    /// <summary>A line of flags on a sagging cord, alternating between the MBs in <paramref name="flags"/>.</summary>
    public static void Bunting(MB wire, MB[] flags, Vector3 a, Vector3 b, float sag, float flagW = 0.42f, float flagH = 0.5f)
    {
        int n = Mathf.Max(3, Mathf.RoundToInt((b - a).magnitude / (flagW * 1.25f)));
        Vector3 prev = a;
        for (int i = 1; i <= n * 2; i++)
        {
            float s = i / (float)(n * 2);
            Vector3 p = Vector3.Lerp(a, b, s) + Vector3.down * (sag * 4f * s * (1f - s));
            wire.Spindle(prev, p, 0.012f);
            prev = p;
        }
        Vector3 along = (b - a).normalized;
        for (int i = 0; i < n; i++)
        {
            float s = (i + 0.5f) / n;
            Vector3 p = Vector3.Lerp(a, b, s) + Vector3.down * (sag * 4f * s * (1f - s));
            var m = flags[i % flags.Length];
            Vector3 l = p - along * flagW / 2f, r = p + along * flagW / 2f;
            m.Tri(l, r, p + Vector3.down * flagH);
        }
    }

    // ---------- particles ----------

    public static Material ParticleMaterial(string name, Texture2D tex, bool additive, Color tint)
    {
        string path = WorldArtBuilder.MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m, path); }
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", additive ? 2f : 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f); m.SetFloat("_Cull", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", tint);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>A box-shaped drifting emitter (motes, petals, glitter, confetti). Colours come from a two-colour random range or a gradient.</summary>
    public static ParticleSystem Drift(string name, Vector3 pos, Vector3 box, Material mat, int max, float rate, Vector2 life, Vector2 size,
        Color c0, Color c1, Vector3 vel0, Vector3 vel1, float gravity = 0f, float noise = 0.5f, float spin = 0f, bool fadeInOut = true)
    {
        var g = new GameObject(name);
        g.transform.position = pos;
        var ps = g.AddComponent<ParticleSystem>();
        var r = g.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        var main = ps.main;
        main.loop = true; main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(c0, c1);
        main.gravityModifier = gravity;
        main.maxParticles = max;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = rate;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = box;
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(vel0.x, vel1.x); vel.y = new ParticleSystem.MinMaxCurve(vel0.y, vel1.y); vel.z = new ParticleSystem.MinMaxCurve(vel0.z, vel1.z);
        if (noise > 0f) { var n = ps.noise; n.enabled = true; n.strength = noise; n.frequency = 0.3f; n.scrollSpeed = 0.2f; }
        if (spin != 0f) { var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-spin, spin); }
        if (fadeInOut)
        {
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
        }
        return ps;
    }

    // ---------- the standing crowd in the stands: one mesh per colour ----------

    /// <summary>People as two soft gems each (body, head), merged per clothes colour: a few hundred figures cost a handful of draws.</summary>
    public static void StaticCrowd(Transform parent, string prefix, List<Vector3> spots, List<float> yaws, string[] clothesHex, string[] skinHex, int seed, float scale = 1f, float sit = 0f)
    {
        var rng = new System.Random(seed);
        var body = new MB[clothesHex.Length]; var head = new MB[skinHex.Length];
        for (int i = 0; i < body.Length; i++) body[i] = new MB();
        for (int i = 0; i < head.Length; i++) head[i] = new MB();
        for (int i = 0; i < spots.Count; i++)
        {
            Vector3 p = spots[i];
            float s = scale * (0.9f + (float)rng.NextDouble() * 0.25f);
            float yaw = yaws != null && yaws.Count > i ? yaws[i] : 0f;
            var q = Quaternion.Euler(0, yaw, 0);
            int ci = rng.Next(body.Length), hi = rng.Next(head.Length);
            float h = 0.42f * s * (1f - sit);
            body[ci].Gem(p + V(0, 0.45f * s * (1f - sit * 0.5f), 0), q * V(0.22f * s, 0, 0), V(0, h, 0), q * V(0, 0, 0.17f * s));
            head[hi].Gem(p + V(0, 1.05f * s * (1f - sit * 0.4f), 0), 0.15f * s, 0.17f * s, 0.15f * s);
        }
        for (int i = 0; i < body.Length; i++)
            if (body[i].Count > 0) MeshObject(prefix + "Clothes" + i, parent, body[i].ToMesh(prefix + "crowd_body" + i), Lit(prefix + "crowd_c" + i, C(clothesHex[i]), 0.1f), false);
        for (int i = 0; i < head.Length; i++)
            if (head[i].Count > 0) MeshObject(prefix + "Heads" + i, parent, head[i].ToMesh(prefix + "crowd_head" + i), Lit(prefix + "crowd_s" + i, C(skinHex[i]), 0.2f), false);
    }

    public static void Log(string what) { Debug.Log("PlaceB: " + what); }
}
