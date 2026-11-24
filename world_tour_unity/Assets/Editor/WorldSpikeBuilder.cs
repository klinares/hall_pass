// Builds the phase 8.0 spike: the 3D URP renderer and pipeline asset, a volume profile, procedural materials and
// textures, and the WorldSpike scene. Everything is made from Unity primitives and code; nothing is downloaded.
//
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -executeMethod WorldSpikeBuilder.CreateAll
//
// The pipeline asset (UniversalRP_3D) is separate from UniversalRP.asset on purpose: tour/ keeps rendering through
// the 2D renderer and its settings (HDR off, no shadows) untouched. BuildWeb.BuildWorld points the quality levels at
// the 3D asset for the length of a world build only.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using WorldTour.World;

public static class WorldSpikeBuilder
{
    public const string Root = "Assets/World";
    public const string ScenePath = Root + "/Scenes/WorldSpike.unity";
    public const string PipelinePath = Root + "/Settings/UniversalRP_3D.asset";
    const string RendererPath = Root + "/Settings/Renderer3D.asset";
    const string ProfilePath = Root + "/Settings/WorldVolumeProfile.asset";

    static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();
    static readonly Color Sky = new Color(0.74f, 0.85f, 0.98f);

    [MenuItem("World Tour/Build world spike")]
    public static void CreateAll()
    {
        try
        {
            foreach (var d in new[] { "Scenes", "Materials", "Textures", "Settings" })
                Directory.CreateDirectory(Path.Combine(Root, d));
            Mats.Clear();
            MakePipeline();
            var profile = MakeProfile();
            MakeScene(profile);
            AssetDatabase.SaveAssets();
            Debug.Log("WorldSpikeBuilder: done " + ScenePath);
        }
        catch (System.Exception e)
        {
            Debug.LogError("WorldSpikeBuilder failed: " + e);
        }
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    // ---- renderer and pipeline ----

    static void MakePipeline()
    {
        AssetDatabase.DeleteAsset(PipelinePath);
        AssetDatabase.DeleteAsset(RendererPath);
        var rd = ScriptableObject.CreateInstance<UniversalRendererData>();
        rd.renderingMode = RenderingMode.Forward;
        AssetDatabase.CreateAsset(rd, RendererPath);
        var pipe = UniversalRenderPipelineAsset.Create(rd);
        AssetDatabase.CreateAsset(pipe, PipelinePath);

        var so = new SerializedObject(pipe);
        void I(string n, int v) { var p = so.FindProperty(n); if (p == null) Debug.LogWarning("no property " + n); else p.intValue = v; }
        void B(string n, bool v) { var p = so.FindProperty(n); if (p == null) Debug.LogWarning("no property " + n); else p.boolValue = v; }
        void F(string n, float v) { var p = so.FindProperty(n); if (p == null) Debug.LogWarning("no property " + n); else p.floatValue = v; }
        B("m_SupportsHDR", true);
        I("m_HDRColorBufferPrecision", 0);   // 32 bit (R11G11B10): cheaper than 64
        I("m_MSAA", 2);                      // 2x: edges stay clean at modest cost
        B("m_MainLightShadowsSupported", true);
        I("m_MainLightShadowmapResolution", 2048);
        B("m_AnyShadowsSupported", true);
        B("m_SoftShadowsSupported", true);
        I("m_SoftShadowQuality", 2);
        F("m_ShadowDistance", 45f);
        I("m_ShadowCascadeCount", 2);
        I("m_AdditionalLightsRenderingMode", 0); // none: one directional light only
        B("m_UseAdaptivePerformance", false);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pipe);
    }

    static VolumeProfile MakeProfile()
    {
        AssetDatabase.DeleteAsset(ProfilePath);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, ProfilePath);

        var bloom = profile.Add<Bloom>();
        bloom.threshold.Override(1.0f);
        bloom.intensity.Override(0.55f);
        bloom.scatter.Override(0.6f);
        bloom.highQualityFiltering.Override(false);

        var tone = profile.Add<Tonemapping>();
        tone.mode.Override(TonemappingMode.ACES);

        var grade = profile.Add<ColorAdjustments>();
        grade.postExposure.Override(0.15f);
        grade.contrast.Override(8f);
        grade.saturation.Override(14f);
        grade.colorFilter.Override(new Color(1f, 0.97f, 0.94f));

        var vig = profile.Add<Vignette>();
        vig.intensity.Override(0.22f);
        vig.smoothness.Override(0.5f);

        foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
        EditorUtility.SetDirty(profile);
        return profile;
    }

    // ---- textures and materials ----

    static Texture2D PlazaTexture()
    {
        const string path = Root + "/Textures/plaza_tiles.png";
        const int n = 256, tiles = 4;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var a = new Color(0.97f, 0.86f, 0.89f);
        var b = new Color(0.91f, 0.82f, 0.9f);
        var grout = new Color(0.78f, 0.7f, 0.76f);
        int size = n / tiles;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int tx = x / size, ty = y / size;
                Color c = ((tx + ty) & 1) == 0 ? a : b;
                int lx = x % size, ly = y % size;
                if (lx < 2 || ly < 2) c = grout;
                float noise = (Mathf.PerlinNoise(x * 0.09f, y * 0.09f) - 0.5f) * 0.05f;
                c.r += noise; c.g += noise; c.b += noise;
                t.SetPixel(x, y, c);
            }
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.mipmapEnabled = true;
        imp.anisoLevel = 8;
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.textureCompression = TextureImporterCompression.Compressed;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Texture2D SoftDot()
    {
        const string path = Root + "/Textures/soft_dot.png";
        const int n = 64;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                float a = Mathf.Clamp01(1f - d); a *= a;
                t.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material Lit(string name, Color c, float smooth = 0.25f, Color? emission = null)
    {
        if (Mats.TryGetValue(name, out var m)) return m;
        string path = Root + "/Materials/" + name + ".mat";
        AssetDatabase.DeleteAsset(path);
        m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", 0f);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        AssetDatabase.CreateAsset(m, path);
        Mats[name] = m;
        return m;
    }

    static Material FireflyMaterial(Texture2D dot)
    {
        string path = Root + "/Materials/firefly.mat";
        AssetDatabase.DeleteAsset(path);
        var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 2f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.SetTexture("_BaseMap", dot);
        m.SetColor("_BaseColor", new Color(2.4f, 2.0f, 0.7f, 1f));
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // ---- scene helpers ----

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        if (euler.HasValue) g.transform.localRotation = Quaternion.Euler(euler.Value);
        g.GetComponent<Renderer>().sharedMaterial = mat;
        return g;
    }

    static Transform Pivot(string name, Transform parent, Vector3 pos)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        return g.transform;
    }

    static void Block(Transform t, float radius) { t.gameObject.AddComponent<Obstacle>().radius = radius; }

    static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

    // ---- the scene ----

    static void MakeScene(VolumeProfile profile)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var dot = SoftDot();
        var world = new GameObject("World").transform;

        // Ground: a 30x30 tiled plaza on a big lawn.
        var plazaMat = Lit("plaza", new Color(0.88f, 0.84f, 0.9f), 0.15f);
        plazaMat.SetTexture("_BaseMap", PlazaTexture());
        plazaMat.SetTextureScale("_BaseMap", new Vector2(7.5f, 7.5f));
        EditorUtility.SetDirty(plazaMat);
        Prim(PrimitiveType.Plane, "Plaza", world, V(0, 0, 0), V(3, 1, 3), plazaMat);
        Prim(PrimitiveType.Plane, "Lawn", world, V(0, -0.03f, 0), V(20, 1, 20), Lit("lawn", new Color(0.55f, 0.82f, 0.55f), 0.05f));
        // Low kerb around the plaza.
        var kerb = Lit("kerb", new Color(0.93f, 0.9f, 0.93f), 0.1f);
        Prim(PrimitiveType.Cube, "KerbN", world, V(0, 0.08f, 15.15f), V(30.6f, 0.2f, 0.3f), kerb);
        Prim(PrimitiveType.Cube, "KerbS", world, V(0, 0.08f, -15.15f), V(30.6f, 0.2f, 0.3f), kerb);
        Prim(PrimitiveType.Cube, "KerbE", world, V(15.15f, 0.08f, 0), V(0.3f, 0.2f, 30f), kerb);
        Prim(PrimitiveType.Cube, "KerbW", world, V(-15.15f, 0.08f, 0), V(0.3f, 0.2f, 30f), kerb);

        // Props.
        var props = new GameObject("Props").transform;
        props.SetParent(world, false);
        foreach (var p in new[] { V(-9, 0, -6), V(9, 0, -6), V(-9, 0, 4), V(9, 0, 4) }) MakeLamp(props, p);
        MakeBench(props, V(-6, 0, -1), 90f);
        MakeBench(props, V(6, 0, -1), -90f);
        MakeBench(props, V(0, 0, -10), 0f);
        foreach (var p in new[] { V(-12.5f, 0, -11), V(12.5f, 0, -11), V(-12.5f, 0, 0), V(12.5f, 0, 0), V(-12.5f, 0, 8), V(12.5f, 0, 8) }) MakeTree(props, p);
        MakeStage(props);
        MakeBackdrop(world);

        // Characters.
        var cam = MakeCamera(profile);
        var star = MakeStar(cam);
        var dog = MakeDog(star.transform);

        var marker = Prim(PrimitiveType.Cylinder, "ClickMarker", world, V(0, 0.03f, 0), V(0.7f, 0.02f, 0.7f),
            Lit("marker", new Color(1f, 0.55f, 0.75f), 0.1f, new Color(1.6f, 0.5f, 0.9f)));
        star.GetComponent<PlayerController>().marker = marker.transform;
        marker.SetActive(false);

        var follow = cam.gameObject.AddComponent<FollowCamera>();
        follow.target = star.transform;

        var st = new GameObject("SpikeStatus").AddComponent<SpikeStatus>();
        st.player = star.transform; st.dog = dog.transform;

        MakeLight();
        MakeFireflies(dot);
        MakeAtmosphere();

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    static void MakeAtmosphere()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.74f, 0.95f);
        RenderSettings.ambientEquatorColor = new Color(0.86f, 0.76f, 0.82f);
        RenderSettings.ambientGroundColor = new Color(0.5f, 0.52f, 0.5f);
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.012f;
        RenderSettings.fogColor = Sky;
        RenderSettings.skybox = null;
        RenderSettings.subtractiveShadowColor = new Color(0.42f, 0.48f, 0.65f);
    }

    static void MakeLight()
    {
        var g = new GameObject("Sun");
        var l = g.AddComponent<Light>();
        l.type = LightType.Directional;
        l.color = new Color(1f, 0.93f, 0.82f);
        l.intensity = 1.25f;
        l.shadows = LightShadows.Soft;
        l.shadowStrength = 0.85f;
        g.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        RenderSettings.sun = l;
    }

    static Camera MakeCamera(VolumeProfile profile)
    {
        var g = new GameObject("Main Camera");
        g.tag = "MainCamera";
        var c = g.AddComponent<Camera>();
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = Sky;
        c.fieldOfView = 36f;
        c.nearClipPlane = 0.5f;
        c.farClipPlane = 120f;
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

    static GameObject MakeStar(Camera cam)
    {
        var jacket = Lit("star_jacket", new Color(0.98f, 0.55f, 0.69f), 0.3f);
        var skirt = Lit("star_skirt", new Color(0.18f, 0.18f, 0.76f), 0.2f);
        var skin = Lit("star_skin", new Color(0.93f, 0.74f, 0.62f), 0.3f);
        var hair = Lit("star_hair", new Color(0.2f, 0.1f, 0.3f), 0.4f);
        var tie = Lit("star_tie", new Color(0.66f, 0.66f, 0.99f), 0.3f);
        var dark = Lit("star_dark", new Color(0.1f, 0.09f, 0.14f), 0.3f);
        var eye = Lit("star_eye", new Color(0.05f, 0.04f, 0.08f), 0.6f);
        var cheek = Lit("star_cheek", new Color(0.98f, 0.6f, 0.65f), 0.2f);

        var root = new GameObject("PopStar");
        var rig = Pivot("Rig", root.transform, Vector3.zero);

        Prim(PrimitiveType.Cylinder, "Skirt", rig, V(0, 0.84f, 0), V(0.66f, 0.12f, 0.66f), skirt);
        Prim(PrimitiveType.Capsule, "Torso", rig, V(0, 1.2f, 0), V(0.5f, 0.38f, 0.4f), jacket);
        Prim(PrimitiveType.Sphere, "Head", rig, V(0, 1.85f, 0), V(0.7f, 0.68f, 0.68f), skin);
        // Hair: a back cap, bangs, a ponytail with a lilac tie.
        Prim(PrimitiveType.Sphere, "HairBack", rig, V(0, 1.9f, -0.07f), V(0.76f, 0.76f, 0.7f), hair);
        Prim(PrimitiveType.Sphere, "Bangs", rig, V(0, 2.07f, 0.12f), V(0.72f, 0.3f, 0.5f), hair);
        var pony = Pivot("Ponytail", rig, V(0, 2.0f, -0.34f));
        Prim(PrimitiveType.Sphere, "Tie", pony, V(0, 0, 0), V(0.14f, 0.14f, 0.14f), tie);
        Prim(PrimitiveType.Capsule, "Tail", pony, V(0, -0.2f, -0.02f), V(0.2f, 0.26f, 0.2f), hair);
        // Face.
        Prim(PrimitiveType.Sphere, "EyeL", rig, V(-0.13f, 1.86f, 0.31f), V(0.075f, 0.1f, 0.05f), eye);
        Prim(PrimitiveType.Sphere, "EyeR", rig, V(0.13f, 1.86f, 0.31f), V(0.075f, 0.1f, 0.05f), eye);
        Prim(PrimitiveType.Sphere, "CheekL", rig, V(-0.2f, 1.76f, 0.28f), V(0.1f, 0.06f, 0.04f), cheek);
        Prim(PrimitiveType.Sphere, "CheekR", rig, V(0.2f, 1.76f, 0.28f), V(0.1f, 0.06f, 0.04f), cheek);

        Transform Leg(string n, float x)
        {
            var p = Pivot(n, rig, V(x, 0.8f, 0));
            Prim(PrimitiveType.Capsule, "Leg", p, V(0, -0.38f, 0), V(0.17f, 0.36f, 0.17f), dark);
            Prim(PrimitiveType.Cube, "Boot", p, V(0, -0.72f, 0.04f), V(0.2f, 0.14f, 0.3f), jacket);
            return p;
        }
        Transform Arm(string n, float x)
        {
            var p = Pivot(n, rig, V(x, 1.5f, 0));
            Prim(PrimitiveType.Capsule, "Sleeve", p, V(0, -0.27f, 0), V(0.14f, 0.28f, 0.14f), jacket);
            Prim(PrimitiveType.Sphere, "Hand", p, V(0, -0.58f, 0), V(0.15f, 0.15f, 0.15f), skin);
            return p;
        }
        var legL = Leg("LegL", -0.14f); var legR = Leg("LegR", 0.14f);
        var armL = Arm("ArmL", -0.36f); var armR = Arm("ArmR", 0.36f);

        var pc = root.AddComponent<PlayerController>();
        pc.cam = cam;
        var anim = root.AddComponent<CharacterAnim>();
        anim.controller = pc;
        anim.rig = rig; anim.legL = legL; anim.legR = legR; anim.armL = armL; anim.armR = armR; anim.ponytail = pony;
        root.transform.position = V(0, 0, -4);
        return root;
    }

    static GameObject MakeDog(Transform target)
    {
        var fur = Lit("dog_fur", new Color(0.93f, 0.8f, 0.58f), 0.2f);
        var ear = Lit("dog_ear", new Color(0.62f, 0.4f, 0.26f), 0.2f);
        var muzzle = Lit("dog_muzzle", new Color(0.99f, 0.94f, 0.84f), 0.2f);
        var black = Lit("dog_black", new Color(0.06f, 0.05f, 0.07f), 0.5f);
        var collar = Lit("dog_collar", new Color(0.18f, 0.18f, 0.76f), 0.3f);

        var root = new GameObject("Dog");
        var rig = Pivot("Rig", root.transform, Vector3.zero);
        Prim(PrimitiveType.Capsule, "Body", rig, V(0, 0.46f, 0), V(0.34f, 0.42f, 0.34f), fur, V(90, 0, 0));
        Prim(PrimitiveType.Sphere, "Head", rig, V(0, 0.68f, 0.5f), V(0.36f, 0.34f, 0.36f), fur);
        Prim(PrimitiveType.Sphere, "Muzzle", rig, V(0, 0.6f, 0.68f), V(0.18f, 0.15f, 0.2f), muzzle);
        Prim(PrimitiveType.Sphere, "Nose", rig, V(0, 0.64f, 0.79f), V(0.075f, 0.065f, 0.065f), black);
        Prim(PrimitiveType.Sphere, "EyeL", rig, V(-0.1f, 0.74f, 0.66f), V(0.06f, 0.07f, 0.04f), black);
        Prim(PrimitiveType.Sphere, "EyeR", rig, V(0.1f, 0.74f, 0.66f), V(0.06f, 0.07f, 0.04f), black);
        Prim(PrimitiveType.Sphere, "EarL", rig, V(-0.17f, 0.78f, 0.45f), V(0.1f, 0.24f, 0.14f), ear, V(0, 0, 18));
        Prim(PrimitiveType.Sphere, "EarR", rig, V(0.17f, 0.78f, 0.45f), V(0.1f, 0.24f, 0.14f), ear, V(0, 0, -18));
        Prim(PrimitiveType.Cylinder, "Collar", rig, V(0, 0.58f, 0.4f), V(0.3f, 0.025f, 0.3f), collar, V(70, 0, 0));

        Transform Leg(string n, float x, float z)
        {
            var p = Pivot(n, rig, V(x, 0.36f, z));
            Prim(PrimitiveType.Capsule, "Leg", p, V(0, -0.17f, 0), V(0.1f, 0.18f, 0.1f), fur);
            Prim(PrimitiveType.Sphere, "Paw", p, V(0, -0.34f, 0.02f), V(0.13f, 0.08f, 0.17f), muzzle);
            return p;
        }
        var fl = Leg("LegFL", -0.14f, 0.3f); var fr = Leg("LegFR", 0.14f, 0.3f);
        var bl = Leg("LegBL", -0.14f, -0.3f); var br = Leg("LegBR", 0.14f, -0.3f);
        var tail = Pivot("Tail", rig, V(0, 0.56f, -0.42f));
        Prim(PrimitiveType.Capsule, "TailMesh", tail, V(0, 0.16f, 0), V(0.07f, 0.17f, 0.07f), fur);

        var f = root.AddComponent<DogFollower>();
        f.target = target;
        f.rig = rig; f.tail = tail; f.legFL = fl; f.legFR = fr; f.legBL = bl; f.legBR = br;
        root.transform.position = V(-1.6f, 0, -6);
        return root;
    }

    static void MakeLamp(Transform parent, Vector3 pos)
    {
        var metal = Lit("lamp_metal", new Color(0.18f, 0.18f, 0.3f), 0.5f);
        var bulb = Lit("lamp_bulb", new Color(1f, 0.92f, 0.7f), 0.2f, new Color(2.6f, 2.0f, 1.0f));
        var g = Pivot("Lamp", parent, pos);
        Prim(PrimitiveType.Cylinder, "Base", g, V(0, 0.15f, 0), V(0.45f, 0.15f, 0.45f), metal);
        Prim(PrimitiveType.Cylinder, "Pole", g, V(0, 1.9f, 0), V(0.12f, 1.75f, 0.12f), metal);
        Prim(PrimitiveType.Cylinder, "Cap", g, V(0, 3.95f, 0), V(0.5f, 0.05f, 0.5f), metal);
        Prim(PrimitiveType.Sphere, "Bulb", g, V(0, 3.75f, 0), V(0.42f, 0.42f, 0.42f), bulb);
        Block(g, 0.28f);
    }

    static void MakeBench(Transform parent, Vector3 pos, float yaw)
    {
        var wood = Lit("bench_wood", new Color(0.78f, 0.5f, 0.38f), 0.2f);
        var metal = Lit("bench_metal", new Color(0.18f, 0.18f, 0.3f), 0.5f);
        var g = Pivot("Bench", parent, pos);
        g.localRotation = Quaternion.Euler(0, yaw, 0);
        Prim(PrimitiveType.Cube, "Seat", g, V(0, 0.5f, 0), V(1.9f, 0.12f, 0.55f), wood);
        Prim(PrimitiveType.Cube, "Back", g, V(0, 0.95f, -0.25f), V(1.9f, 0.5f, 0.1f), wood, V(-8, 0, 0));
        Prim(PrimitiveType.Cube, "LegL", g, V(-0.8f, 0.25f, 0), V(0.1f, 0.5f, 0.45f), metal);
        Prim(PrimitiveType.Cube, "LegR", g, V(0.8f, 0.25f, 0), V(0.1f, 0.5f, 0.45f), metal);
        foreach (float x in new[] { -0.6f, 0f, 0.6f })
        {
            var o = Pivot("Block", g, V(x, 0, 0));
            Block(o, 0.45f);
        }
    }

    static void MakeTree(Transform parent, Vector3 pos)
    {
        var trunk = Lit("tree_trunk", new Color(0.55f, 0.37f, 0.3f), 0.1f);
        var leaf1 = Lit("tree_leaf1", new Color(0.4f, 0.75f, 0.5f), 0.1f);
        var leaf2 = Lit("tree_leaf2", new Color(0.5f, 0.83f, 0.55f), 0.1f);
        var g = Pivot("Tree", parent, pos);
        Prim(PrimitiveType.Cylinder, "Trunk", g, V(0, 1.1f, 0), V(0.38f, 1.1f, 0.38f), trunk);
        Prim(PrimitiveType.Sphere, "Crown", g, V(0, 3.0f, 0), V(2.8f, 2.5f, 2.8f), leaf1);
        Prim(PrimitiveType.Sphere, "CrownTop", g, V(0.2f, 4.0f, 0.1f), V(1.9f, 1.7f, 1.9f), leaf2);
        Block(g, 0.5f);
    }

    static void MakeStage(Transform parent)
    {
        var deck = Lit("stage_deck", new Color(0.18f, 0.18f, 0.76f), 0.35f);
        var trim = Lit("stage_trim", new Color(0.98f, 0.55f, 0.69f), 0.3f);
        var wall = Lit("stage_wall", new Color(0.22f, 0.2f, 0.4f), 0.2f);
        var truss = Lit("stage_truss", new Color(0.12f, 0.12f, 0.2f), 0.6f);
        var g = Pivot("Stage", parent, V(0, 0, 11));
        Prim(PrimitiveType.Cube, "Deck", g, V(0, 0.5f, 0), V(11f, 1f, 5f), deck);
        Prim(PrimitiveType.Cube, "Trim", g, V(0, 1.02f, -2.4f), V(11.1f, 0.06f, 0.2f), trim);
        Prim(PrimitiveType.Cube, "BackWall", g, V(0, 3.5f, 2.3f), V(11f, 5f, 0.4f), wall);
        Prim(PrimitiveType.Cube, "Arch", g, V(0, 6.4f, 0.4f), V(11f, 0.35f, 0.5f), truss);
        Prim(PrimitiveType.Cube, "TowerL", g, V(-5.3f, 3.2f, -1.6f), V(0.35f, 6.4f, 0.35f), truss);
        Prim(PrimitiveType.Cube, "TowerR", g, V(5.3f, 3.2f, -1.6f), V(0.35f, 6.4f, 0.35f), truss);
        Prim(PrimitiveType.Cube, "Beam", g, V(0, 6.3f, -1.6f), V(10.9f, 0.3f, 0.3f), truss);
        var cols = new[] { new Color(3f, 0.6f, 1.4f), new Color(1.0f, 1.2f, 3.2f), new Color(3f, 2.4f, 0.8f), new Color(0.8f, 3f, 1.6f), new Color(3f, 0.6f, 1.4f), new Color(1.0f, 1.2f, 3.2f) };
        for (int i = 0; i < cols.Length; i++)
        {
            var m = Lit("stage_light" + i, Color.white, 0.2f, cols[i]);
            float x = -4.5f + i * 1.8f;
            Prim(PrimitiveType.Sphere, "Light" + i, g, V(x, 6.0f, -1.6f), V(0.5f, 0.5f, 0.5f), m);
        }
        for (int i = 0; i < 5; i++) { var o = Pivot("Block", g, V(-4.5f + i * 2.25f, 0, 0)); Block(o, 2.7f); }
    }

    static void MakeBackdrop(Transform parent)
    {
        var g = new GameObject("Backdrop").transform;
        g.SetParent(parent, false);
        var cols = new[] { new Color(0.99f, 0.8f, 0.86f), new Color(0.76f, 0.77f, 0.99f), new Color(0.99f, 0.9f, 0.78f), new Color(0.8f, 0.93f, 0.9f), new Color(0.97f, 0.7f, 0.8f) };
        var win = Lit("window", new Color(1f, 0.9f, 0.7f), 0.3f, new Color(1.5f, 1.15f, 0.6f));
        int k = 0;
        void Building(float x, float z, float w, float d, float h, bool faceX, float sign)
        {
            var m = Lit("bld" + (k % cols.Length), cols[k % cols.Length], 0.1f);
            k++;
            var b = Prim(PrimitiveType.Cube, "Building", g, V(x, h / 2, z), V(w, h, d), m);
            // Rows of lit windows on the face toward the plaza.
            int rows = Mathf.Max(2, (int)(h / 2.6f));
            int colsN = Mathf.Max(2, (int)((faceX ? d : w) / 2.4f));
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < colsN; c++)
                {
                    float u = ((c + 0.5f) / colsN - 0.5f) * ((faceX ? d : w) - 1f);
                    float y = 1.8f + r * 2.4f;
                    if (y > h - 1f) continue;
                    if (((r * 7 + c * 3 + k) % 4) == 0) continue;
                    Vector3 p = faceX ? V(x + sign * (w / 2 + 0.02f), y, z + u) : V(x + u, y, z + sign * (d / 2 + 0.02f));
                    Vector3 s = faceX ? V(0.05f, 1.0f, 0.9f) : V(0.9f, 1.0f, 0.05f);
                    Prim(PrimitiveType.Cube, "Win", g, p, s, win);
                }
        }
        for (int i = -2; i <= 2; i++) Building(i * 10f, 23f, 8.5f, 6f, 8f + ((i * i * 5) % 9), false, -1f);
        for (int i = -1; i <= 2; i++) Building(-22f, -6f + i * 9f, 6f, 8f, 7f + ((i * 3 + 9) % 6), true, 1f);
        for (int i = -1; i <= 2; i++) Building(22f, -6f + i * 9f, 6f, 8f, 8f + ((i * 5 + 11) % 7), true, -1f);
    }

    static void MakeFireflies(Texture2D dot)
    {
        var g = new GameObject("Fireflies");
        g.transform.position = V(0, 2.2f, 0);
        var ps = g.AddComponent<ParticleSystem>();
        var r = g.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = FireflyMaterial(dot);
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
        main.startSpeed = 0.15f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.3f);
        main.startColor = Color.white;
        main.maxParticles = 90;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = ps.emission;
        em.rateOverTime = 14f;
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = V(28f, 3.5f, 28f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.9f;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.3f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0.2f, 0.6f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;
    }
}
