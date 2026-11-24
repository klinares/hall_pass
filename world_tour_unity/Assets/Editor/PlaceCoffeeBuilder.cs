// Phase 8.1: the Place_Coffee scene, "the coffee shop street, morning". Built from the CC0 KayKit pieces (ASSETS.md) plus
// Unity primitives. Re-runnable: it rebuilds the prefabs (WorldArtBuilder.Run) and then the scene from scratch.
//
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -executeMethod PlaceCoffeeBuilder.CreateAll
//   Unity.exe -batchmode -projectPath world_tour_unity -executeMethod PlaceCoffeeBuilder.Preview -previewOut <dir>   (needs a GPU)
//
// World layout (x right, z away from the camera, metres; the walkers are clamped to +-13.5):
//   south park strip (spawn) -> road with a crosswalk -> tiled plaza with the coffee shop front along z = 8 (x -13 .. 3)
//   and the outdoor stage at the east end (deck x 4.5 .. 13.5, z 7 .. 13), tall KayKit buildings behind and on both sides.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using WorldTour.World;

public static partial class PlaceCoffeeBuilder
{
    public const string ScenePath = "Assets/World/Scenes/Place_Coffee.unity";
    const string ProfilePath = "Assets/World/Settings/CoffeeVolumeProfile.asset";
    const string City = "Assets/World/Art/KayKit_City/";
    const string Resto = "Assets/World/Art/KayKit_Restaurant/";
    const string Forest = "Assets/World/Art/KayKit_Forest/";

    const float CityScale = 4f;

    static Material cityMat, restoMat, forestMat, blossomMat;
    static readonly HashSet<string> Reported = new HashSet<string>();

    static Color C(string hex) { return WorldArtBuilder.C(hex); }
    static Vector3 V(float x, float y, float z) { return new Vector3(x, y, z); }
    static Material Lit(string n, Color c, float s = 0.15f, Color? e = null) { return WorldArtBuilder.Lit(n, c, s, null, e); }

    public static void CreateAll()
    {
        try
        {
            WorldArtBuilder.Run();
            Directory.CreateDirectory("Assets/World/Scenes");
            MakeScene();
            AssetDatabase.SaveAssets();
            Debug.Log("PlaceCoffeeBuilder: done " + ScenePath);
        }
        catch (System.Exception e)
        {
            Debug.LogError("PlaceCoffeeBuilder failed: " + e);
        }
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    // ---------- helpers ----------

    static GameObject Group(string name, Transform parent = null)
    {
        var g = new GameObject(name);
        if (parent != null) g.transform.SetParent(parent, false);
        return g;
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null, bool shadows = true)
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
    static GameObject Fbx(string path, Transform parent, Vector3 pos, float yaw, float scale, Material mat, string name = null)
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

    static void Block(Transform parent, float x, float z, float radius)
    {
        var g = new GameObject("Block");
        g.transform.SetParent(parent, false);
        g.transform.position = V(x, 0, z);
        g.AddComponent<Obstacle>().radius = radius;
    }

    // ---------- the scene ----------

    static void MakeScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Reported.Clear();

        cityMat = Lit("city_kit", Color.white, 0.12f);
        cityMat.SetTexture("_BaseMap", WorldArtBuilder.ArtTexture(City + "citybits_texture.png"));
        restoMat = Lit("resto_kit", Color.white, 0.12f);
        restoMat.SetTexture("_BaseMap", WorldArtBuilder.ArtTexture(Resto + "restaurantbits_texture.png"));
        forestMat = Lit("forest_kit", Color.white, 0.08f);
        forestMat.SetTexture("_BaseMap", WorldArtBuilder.ArtTexture(Forest + "forest_texture.png"));
        blossomMat = Lit("forest_blossom", Color.white, 0.08f);
        blossomMat.SetTexture("_BaseMap", BlossomTexture());

        var profile = MakeProfile();
        var world = Group("World").transform;
        var ground = Group("Ground", world).transform;
        var street = Group("Street", world).transform;
        var shop = Group("CoffeeShop", world).transform;
        var stage = Group("Stage", world).transform;
        var blds = Group("Buildings", world).transform;
        var nature = Group("Nature", world).transform;
        var props = Group("Props", world).transform;
        var blocks = Group("Blockers", world).transform;

        MakeGround(ground);
        MakeStreet(street, props, blocks);
        MakeShop(shop, props, blocks);
        MakeStage(stage, blocks);
        MakeBuildings(blds);
        MakeNature(nature, blocks);
        MakeProps(props, blocks);
        MakeDressing(world, props, blocks);

        // The contract empties (the gameplay finds these by name).
        var spawn = Group("PlayerSpawn").transform; spawn.position = V(0, 0, -7.6f);   // close enough that the first crew member is on screen at the start
        var stageSpot = Group("StageSpot").transform; stageSpot.position = V(9f, 0f, 9.2f);   // ground level: the gameplay turns clicks into ground points and flat distances
        var crowd = Group("CrowdArea").transform; crowd.position = V(9f, 0, 3.3f); crowd.localScale = V(8.2f, 1f, 4.3f);   // front row ends 0.8 m short of the ramp
        var exit = Group("ExitSpot").transform; exit.position = V(11.5f, 0, -9.5f);
        MakeExit(exit);

        var markerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorldArtBuilder.MarkerPrefab);
        var crewPos = new[] { V(-5.0f, 0, 1.6f), V(3.0f, 0, 3.4f), V(-9.2f, 0, 5.9f) };
        for (int i = 0; i < 3; i++)
        {
            var root = Group("Crew_" + (i + 1)).transform;
            root.position = crewPos[i];
            var crew = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WorldArtBuilder.CrewPrefabs[i]), root);
            crew.transform.localPosition = Vector3.zero;
            crew.transform.localRotation = Quaternion.Euler(0, 180f - 14f + i * 14f, 0);
            var mk = (GameObject)PrefabUtility.InstantiatePrefab(markerPrefab, root);
            mk.transform.localPosition = V(0, 3.65f, 0);   // above the name tag, which hangs under the bubble (fix round 1)
            Block(blocks, crewPos[i].x, crewPos[i].z, 0.45f);
        }

        var cam = MakeCamera(profile);
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
        dogGo.transform.position = V(-1.8f, 0, -9.6f);
        dogGo.GetComponent<DogFollower>().target = player;

        var follow = cam.gameObject.AddComponent<FollowCamera>();
        follow.target = player;
        follow.offset = V(0, 6.6f, -11.4f);
        follow.tiltUp = 9f;
        follow.lead = 1.2f;
        follow.clearRadius = 0.9f;

        var st = new GameObject("SpikeStatus").AddComponent<SpikeStatus>();
        st.player = player; st.dog = dogGo.transform;

        MakeLight();
        MakeAtmosphere();
        MakeParticles();
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    // ---------- look: lights, fog, volume, camera ----------

    static VolumeProfile MakeProfile()
    {
        AssetDatabase.DeleteAsset(ProfilePath);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, ProfilePath);
        var bloom = profile.Add<Bloom>();
        bloom.threshold.Override(1.0f);
        bloom.intensity.Override(0.38f);
        bloom.scatter.Override(0.6f);
        bloom.tint.Override(new Color(1f, 0.92f, 0.85f));
        bloom.highQualityFiltering.Override(false);
        var tone = profile.Add<Tonemapping>();
        tone.mode.Override(TonemappingMode.Neutral);   // keeps colour rich where ACES greys it
        var grade = profile.Add<ColorAdjustments>();
        grade.postExposure.Override(0.0f);
        grade.contrast.Override(16f);
        grade.saturation.Override(16f);
        grade.colorFilter.Override(new Color(1f, 0.985f, 0.96f));
        var wb = profile.Add<WhiteBalance>();
        wb.temperature.Override(6f);
        wb.tint.Override(3f);
        // cool the shadows, warm the lights: the shadow side of every wall reads blue-lilac, the sunlit side peach
        var smh = profile.Add<ShadowsMidtonesHighlights>();
        smh.shadows.Override(new Vector4(0.86f, 0.9f, 1.12f, 0f));
        smh.midtones.Override(new Vector4(1f, 1f, 1f, 0f));
        smh.highlights.Override(new Vector4(1.08f, 1.0f, 0.9f, 0f));
        smh.shadowsStart.Override(0f); smh.shadowsEnd.Override(0.4f);
        smh.highlightsStart.Override(0.55f); smh.highlightsEnd.Override(1f);
        var vig = profile.Add<Vignette>();
        vig.intensity.Override(0.2f);
        vig.smoothness.Override(0.5f);
        foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
        EditorUtility.SetDirty(profile);
        return profile;
    }

    static void MakeAtmosphere()
    {
        // a soft gradient sky (WorldTour/SkyGradient): blue overhead, pale peach at the horizon; the fog is the horizon colour so far things melt into it
        string skyPath = WorldArtBuilder.MatDir + "/sky.mat";
        var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        var skyShader = Shader.Find("WorldTour/SkyGradient");
        if (sky == null) { sky = new Material(skyShader); AssetDatabase.CreateAsset(sky, skyPath); }
        sky.shader = skyShader;
        sky.SetColor("_Top", new Color(0.33f, 0.52f, 0.95f));
        sky.SetColor("_Mid", new Color(0.6f, 0.76f, 1.0f));
        sky.SetColor("_Horizon", new Color(1.0f, 0.89f, 0.84f));
        sky.SetColor("_Bottom", new Color(0.93f, 0.84f, 0.82f));
        sky.SetVector("_SunDir", -(Quaternion.Euler(36f, 52f, 0f) * Vector3.forward));
        EditorUtility.SetDirty(sky);
        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.55f, 0.68f, 0.98f);
        RenderSettings.ambientEquatorColor = new Color(0.95f, 0.8f, 0.76f);
        RenderSettings.ambientGroundColor = new Color(0.46f, 0.42f, 0.48f);
        RenderSettings.ambientIntensity = 0.9f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.0075f;
        RenderSettings.fogColor = new Color(0.93f, 0.89f, 0.9f);
        RenderSettings.subtractiveShadowColor = new Color(0.42f, 0.48f, 0.68f);
    }

    static void MakeLight()
    {
        var g = new GameObject("Sun");
        var l = g.AddComponent<Light>();
        l.type = LightType.Directional;
        l.color = new Color(1f, 0.83f, 0.62f);   // warm morning sun
        l.intensity = 2.1f;
        l.shadows = LightShadows.Soft;
        l.shadowStrength = 0.9f;
        l.shadowBias = 0.05f;
        l.shadowNormalBias = 0.4f;
        // from the west and a little behind her: fronts facing the camera catch light, shadows fall to the right and away
        g.transform.rotation = Quaternion.Euler(36f, 52f, 0f);
        RenderSettings.sun = l;
    }

    static Camera MakeCamera(VolumeProfile profile)
    {
        var g = new GameObject("Main Camera");
        g.tag = "MainCamera";
        var c = g.AddComponent<Camera>();
        c.clearFlags = CameraClearFlags.Skybox;
        c.backgroundColor = new Color(0.8f, 0.87f, 0.97f);
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

    // ---------- textures made here ----------

    /// <summary>The forest palette with its green swatch repainted cherry-blossom pink.</summary>
    static Texture2D BlossomTexture()
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

    /// <summary>Cream paving with warm-grey joints and a scatter of pink slabs: 8 x 8 half-metre slabs per 4 m of ground.</summary>
    static Texture2D PlazaTexture()
    {
        string path = "Assets/World/Textures/plaza_sidewalk.png";
        const int n = 512, cell = n / 8;
        var px = new Color32[n * n];
        Color cream = C("#f3e6cf"), warm = C("#e9d6b9"), grout = C("#b9a998"), pink = C("#f4a9c4"), pinkLight = C("#f9cddc");
        var rng = new System.Random(11);
        for (int ty = 0; ty < 8; ty++)
            for (int tx = 0; tx < 8; tx++)
            {
                bool accent = (tx == 1 && ty == 1) || (tx == 5 && ty == 5) || (tx == 3 && ty == 6) || (tx == 6 && ty == 2);
                float v = 0.965f + (float)rng.NextDouble() * 0.07f;
                Color baseC = accent ? Color.Lerp(pink, pinkLight, (float)rng.NextDouble() * 0.5f) : Color.Lerp(cream, warm, (float)rng.NextDouble() * 0.6f);
                baseC *= v; baseC.a = 1f;
                for (int y = 0; y < cell; y++)
                    for (int x = 0; x < cell; x++)
                    {
                        Color c = baseC;
                        float edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(cell - 1 - x, cell - 1 - y));
                        if (x >= cell - 3 || y >= cell - 3) c = grout;                                  // joint on the far sides
                        else if (x < 2 || y < 2) c = Color.Lerp(c, Color.white, 0.35f);                   // lit near edge
                        else c *= 1f + ((float)rng.NextDouble() - 0.5f) * 0.035f;                        // a little grain
                        if (accent && edge > 12 && edge < 15) c = Color.Lerp(c, pinkLight, 0.55f);        // a lighter inner outline
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

    static Texture2D LeafTexture()
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

    // ---------- ground, road, plaza ----------

    static void MakeGround(Transform t)
    {
        Prim(PrimitiveType.Plane, "Lawn", t, V(0, -0.03f, 0), V(24, 1, 24), Lit("lawn_warm", C("#7cbf68"), 0.05f), null, false);
        var plaza = Lit("plaza_art", C("#ffffff"), 0.12f);
        plaza.SetTexture("_BaseMap", PlazaTexture());
        plaza.SetTextureScale("_BaseMap", new Vector2(7f, 3.875f));
        Prim(PrimitiveType.Plane, "Plaza", t, V(0, 0, 6.25f), V(2.8f, 1, 1.55f), plaza, null, false);
        var path = Lit("path_art", C("#ffffff"), 0.12f);
        path.SetTexture("_BaseMap", PlazaTexture());
        path.SetTextureScale("_BaseMap", new Vector2(3.2f, 7.4f));
        Prim(PrimitiveType.Plane, "ParkPath", t, V(0, 0.005f, -10.6f), V(0.32f, 1, 0.74f), path, null, false);
        Prim(PrimitiveType.Plane, "Road", t, V(0, 0.01f, -4f), V(3.2f, 1, 0.5f), Lit("asphalt", C("#5d596e"), 0.05f), null, false);
    }

    static void MakeStreet(Transform t, Transform props, Transform blocks)
    {
        var paint = Lit("road_paint", C("#fff8ee"), 0.1f);
        for (int i = 0; i < 9; i++)
        {
            float x = -14.4f + i * 3.6f;
            if (Mathf.Abs(x) < 3f) continue;
            Prim(PrimitiveType.Cube, "Dash", t, V(x, 0.02f, -4f), V(1.6f, 0.02f, 0.16f), paint, null, false);
        }
        for (int i = 0; i < 5; i++)
            Prim(PrimitiveType.Cube, "Zebra", t, V(-1.8f + i * 0.9f, 0.02f, -4f), V(0.5f, 0.02f, 4.3f), paint, null, false);
        var kerb = Lit("kerb_art", C("#f6e9ee"), 0.1f);
        foreach (float z in new[] { -1.4f, -6.6f })
        {
            Prim(PrimitiveType.Cube, "KerbW", t, V(-9.1f, 0.07f, z), V(13.8f, 0.14f, 0.3f), kerb);
            Prim(PrimitiveType.Cube, "KerbE", t, V(9.1f, 0.07f, z), V(13.8f, 0.14f, 0.3f), kerb);
        }
        // Parked taxi at the east end (the way out of town).
        var taxi = Fbx(City + "car_taxi.fbx", t, V(10f, 0.0f, -4.9f), 90f, CityScale, cityMat);
        Block(blocks, 8.7f, -4.9f, 0.9f); Block(blocks, 10f, -4.9f, 0.9f); Block(blocks, 11.3f, -4.9f, 0.9f);
        taxi.name = "Taxi";
    }

    static void MakeExit(Transform exit)
    {
        var ring = Lit("exit_ring", C("#a8a9fc"), 0.1f, new Color(0.9f, 0.9f, 2.2f));
        Prim(PrimitiveType.Cylinder, "Ring", exit, V(0, 0.03f, 0), V(2.4f, 0.015f, 2.4f), ring, null, false);
        Prim(PrimitiveType.Cylinder, "RingHole", exit, V(0, 0.04f, 0), V(1.8f, 0.015f, 1.8f), Lit("exit_hole", C("#fdb1cb"), 0.1f), null, false);
        // an arrow on the ground pointing east
        var arrow = Lit("exit_arrow", C("#ffffff"), 0.1f, new Color(1.2f, 1.2f, 1.4f));
        Prim(PrimitiveType.Cube, "ArrowShaft", exit, V(-0.15f, 0.06f, 0), V(0.9f, 0.015f, 0.26f), arrow, null, false);
        Prim(PrimitiveType.Cube, "ArrowHeadA", exit, V(0.38f, 0.06f, 0.13f), V(0.6f, 0.015f, 0.2f), arrow, V(0, 35, 0), false);
        Prim(PrimitiveType.Cube, "ArrowHeadB", exit, V(0.38f, 0.06f, -0.13f), V(0.6f, 0.015f, 0.2f), arrow, V(0, -35, 0), false);
        // a signpost
        var pole = Lit("sign_pole", C("#3a3a68"), 0.4f);
        Prim(PrimitiveType.Cylinder, "Pole", exit, V(1.7f, 1.4f, 0.3f), V(0.12f, 1.4f, 0.12f), pole);
        var board = Prim(PrimitiveType.Cube, "Board", exit, V(1.7f, 2.65f, 0.3f), V(1.5f, 0.8f, 0.1f), Lit("sign_board", C("#f98bb0"), 0.2f));
        Prim(PrimitiveType.Cube, "BoardArrow", board.transform, V(0f, 0f, -0.7f), V(0.5f, 0.12f, 0.4f), Lit("sign_arrow", C("#ffffff"), 0.2f, new Color(0.5f, 0.5f, 0.5f)));
    }

    // ---------- the coffee shop ----------

    static void MakeShop(Transform t, Transform props, Transform blocks)
    {
        var cream = Lit("shop_cream", C("#fff1dc"), 0.1f);
        var pink = Lit("shop_pink", C("#f98bb0"), 0.2f);
        var white = Lit("shop_white", C("#fffaf3"), 0.2f);
        var lilac = Lit("shop_lilac", C("#a8a9fc"), 0.2f);
        var wood = Lit("shop_wood", C("#d9a273"), 0.2f);
        // body: floor, back and side walls, roof
        Prim(PrimitiveType.Cube, "Floor", t, V(-5f, 0.03f, 11.5f), V(16f, 0.06f, 7f), wood, null, false);
        Prim(PrimitiveType.Cube, "BackWall", t, V(-5f, 2.2f, 15.2f), V(16.8f, 4.4f, 0.4f), cream);
        Prim(PrimitiveType.Cube, "SideWallW", t, V(-13.2f, 2.2f, 11.7f), V(0.4f, 4.4f, 7.2f), cream);
        Prim(PrimitiveType.Cube, "SideWallE", t, V(3.2f, 2.2f, 11.7f), V(0.4f, 4.4f, 7.2f), cream);
        Prim(PrimitiveType.Cube, "Roof", t, V(-5f, 4.45f, 11.6f), V(17f, 0.4f, 7.6f), white);
        Prim(PrimitiveType.Cube, "Parapet", t, V(-5f, 4.95f, 8.0f), V(17f, 0.7f, 0.4f), white);
        Prim(PrimitiveType.Cube, "ParapetStripe", t, V(-5f, 4.72f, 7.78f), V(17f, 0.16f, 0.1f), pink);

        // front: four 4 m modules at z = 8
        string[] pieces = { "wall_window_open", "wall_orderwindow_decorated", "wall_doorway", "wall_window_open" };
        float[] xs = { -11f, -7f, -3f, 1f };
        for (int i = 0; i < 4; i++) Fbx(Resto + pieces[i] + ".fbx", t, V(xs[i], 0, 8.2f), 180f, 1f, restoMat, "Front_" + (i + 1));
        Fbx(Resto + "door_A.fbx", t, V(-3f, 0, 8.2f), 180f, 1f, restoMat, "Door");
                for (float x = -13f; x <= 2.8f; x += 1.7f) Block(blocks, x, 8.7f, 0.7f);  // walkers stay off the wall line

        // interior: counter, shelf with jars, espresso machine, pendant lamps
        for (int i = 0; i < 3; i++) Fbx(Resto + "kitchencounter_straight_A.fbx", t, V(-8f + i * 2f, 0, 10.3f), 180f, 1f, restoMat, "Counter");
        for (int i = 0; i < 4; i++) Fbx(Resto + (i % 2 == 0 ? "jar_A_large.fbx" : "jar_B_medium.fbx"), t, V(-10.3f + i * 0.9f, 3.0f, 14.9f), 0f, 1f, restoMat, "Jar");
        Prim(PrimitiveType.Cube, "ShelfBoard", t, V(-8.8f, 2.6f, 14.85f), V(4.4f, 0.1f, 0.5f), wood);
        var steel = Lit("steel", C("#c9ccdf"), 0.6f);
        Prim(PrimitiveType.Cube, "Espresso", t, V(-6.6f, 1.35f, 10.4f), V(1.1f, 0.7f, 0.6f), steel);
        Prim(PrimitiveType.Cylinder, "EspressoTop", t, V(-6.6f, 1.8f, 10.4f), V(0.5f, 0.1f, 0.5f), pink);
        var lampMat = Lit("pendant", C("#fff3c4"), 0.2f, new Color(2.6f, 2.0f, 1.0f));
        for (int i = 0; i < 3; i++)
        {
            Prim(PrimitiveType.Cylinder, "Cord", t, V(-9f + i * 3.5f, 3.6f, 10.6f), V(0.03f, 0.55f, 0.03f), steel, null, false);
            Prim(PrimitiveType.Sphere, "Pendant", t, V(-9f + i * 3.5f, 2.95f, 10.6f), V(0.55f, 0.55f, 0.55f), lampMat, null, false);
        }

        // awning: striped slats slanting out over the front
        var stripeA = Lit("awning_pink", C("#f98bb0"), 0.25f);
        var stripeB = Lit("awning_cream", C("#fff6ea"), 0.25f);
        const float slant = 20f;
        for (int i = 0; i < 20; i++)
        {
            float x = -12.6f + i * 0.8f;
            var m = i % 2 == 0 ? stripeA : stripeB;
            Prim(PrimitiveType.Cube, "Awning", t, V(x, 3.05f, 6.87f), V(0.8f, 0.07f, 2.45f), m, V(-slant, 0, 0));
            Prim(PrimitiveType.Cube, "Valance", t, V(x, 2.55f, 5.74f), V(0.8f, 0.3f, 0.07f), m);
        }

        // sign above the door: a lilac panel with a coffee cup
        var panel = Prim(PrimitiveType.Cube, "SignPanel", t, V(-3f, 5.85f, 7.95f), V(3.8f, 1.5f, 0.25f), lilac);
        var cup = Lit("sign_cup", C("#f3ebdd"), 0.3f);
        Prim(PrimitiveType.Cylinder, "Cup", panel.transform, V(-0.1f, -0.05f, -0.8f), V(0.2f, 0.25f, 0.9f), cup, V(90, 0, 0));
        Prim(PrimitiveType.Cylinder, "Saucer", panel.transform, V(-0.1f, -0.33f, -0.8f), V(0.34f, 0.02f, 1.2f), cup, V(90, 0, 0));
        Prim(PrimitiveType.Cube, "CupHandle", panel.transform, V(0.17f, -0.03f, -0.8f), V(0.08f, 0.2f, 0.5f), cup);
        var steam = Lit("sign_steam", C("#ffffff"), 0.3f, new Color(0.8f, 0.8f, 0.9f));
        for (int i = 0; i < 3; i++) Prim(PrimitiveType.Sphere, "Steam", panel.transform, V(-0.18f + i * 0.08f, 0.2f + i * 0.1f, -0.8f), V(0.05f, 0.05f, 0.3f), steam);

        // outdoor seating
        float[] tx = { -12.0f, -0.4f };   // kept clear of the straight walk from the spawn to the first crew member
        float[] tz = { 3.8f, 4.6f };
        for (int i = 0; i < 2; i++)
        {
            var g = Group("CafeTable", props).transform;
            g.position = V(tx[i], 0, tz[i]);
            Fbx(Resto + "table_round_A_small.fbx", g, Vector3.zero, 0f, 0.9f, restoMat, "Table");
            Fbx(Resto + "chair_A.fbx", g, V(-1.1f, 0, 0.15f), 90f, 0.9f, restoMat, "Chair");
            Fbx(Resto + "chair_A.fbx", g, V(1.1f, 0, -0.15f), -90f, 0.9f, restoMat, "Chair");
            Prim(PrimitiveType.Cylinder, "Mug", g, V(0.18f, 1.1f, 0.1f), V(0.22f, 0.12f, 0.22f), Lit("mug_white", C("#ffffff"), 0.35f));
            Prim(PrimitiveType.Cylinder, "Mug", g, V(-0.25f, 1.1f, -0.12f), V(0.22f, 0.12f, 0.22f), Lit("mug_pink", C("#fdb1cb"), 0.35f));
            Block(blocks, tx[i], tz[i], 0.75f);
            Block(blocks, tx[i] - 1.1f, tz[i] + 0.15f, 0.35f);
            Block(blocks, tx[i] + 1.1f, tz[i] - 0.15f, 0.35f);
        }
        // menu board by the door, planters flanking it
        Fbx(Resto + "menu.fbx", props, V(-5.2f, 0, 6.6f), 200f, 1.8f, restoMat, "MenuBoard");
        Block(blocks, -5.2f, 6.6f, 0.45f);
        var planter = Lit("planter", C("#fff6ea"), 0.15f);
        foreach (float x in new[] { -0.9f, -5.1f })
        {
            Prim(PrimitiveType.Cube, "Planter", props, V(x, 0.35f, 7.3f), V(1.6f, 0.7f, 0.7f), planter);
            Fbx(Forest + "Bush_2_A_Color1.fbx", props, V(x, 0.7f, 7.3f), x * 31f, 1.5f, blossomMat, "PlanterBush");
        }
    }

    // ---------- the stage ----------

    static void MakeStage(Transform t, Transform blocks)
    {
        var deck = Lit("stage_deck", C("#2d2fa0"), 0.35f);
        var pink = Lit("stage_pink", C("#f98bb0"), 0.3f, new Color(0.55f, 0.12f, 0.3f));
        var wall = Lit("stage_wall", C("#4a3d9a"), 0.2f);
        var dark = Lit("stage_dark", C("#1d1e4a"), 0.4f);
        var truss = Lit("stage_truss", C("#7d82a8"), 0.55f);
        const float cx = 9f, cz = 10f, h = 0.5f;

        Prim(PrimitiveType.Cube, "Deck", t, V(cx, h / 2, cz), V(9f, h, 6f), deck);
        Prim(PrimitiveType.Cube, "DeckLip", t, V(cx, h - 0.04f, cz - 3.02f), V(9.04f, 0.12f, 0.1f), pink);
        // ramp up the front, the lift the walkers get from HeightZone below
        const float len = 0.943f;
        Prim(PrimitiveType.Cube, "Ramp", t, V(cx, 0.2076f, 6.6265f), V(9f, 0.1f, len), deck, V(-32f, 0, 0));
        var zone = new GameObject("HeightZone").AddComponent<HeightZone>();
        zone.transform.SetParent(t, false);
        zone.transform.position = V(cx, 0, cz);
        zone.height = h; zone.rampLength = 0.8f; zone.halfSize = new Vector2(4.5f, 3f);

        Prim(PrimitiveType.Cube, "BackWall", t, V(cx, 3.8f, 13.2f), V(9.4f, 6.6f, 0.4f), wall);
        Prim(PrimitiveType.Cube, "BackWallTrim", t, V(cx, 7.15f, 13.0f), V(9.6f, 0.4f, 0.8f), pink);
        // banner stars on the back wall
        var gold = Lit("stage_star", C("#ffd23a"), 0.3f, new Color(1.5f, 1.0f, 0.25f));
        var starMesh = AssetDatabase.LoadAssetAtPath<Mesh>(WorldArtBuilder.MeshDir + "/MarkerStar.asset");
        void Star(Vector3 p, float s, Material m, float zscale = 0.45f)
        {
            var g = new GameObject("BannerStar");
            g.transform.SetParent(t, false);
            g.transform.localPosition = p;
            g.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            g.transform.localScale = V(s, s, s * zscale);
            g.AddComponent<MeshFilter>().sharedMesh = starMesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = m;
        }
        Star(V(cx, 4.3f, 12.9f), 3.4f, Lit("stage_star_edge", C("#fdb1cb"), 0.3f, new Color(0.7f, 0.2f, 0.4f)));
        Star(V(cx, 4.3f, 12.7f), 2.7f, gold);
        Star(V(cx - 3.2f, 3.3f, 12.9f), 1.1f, gold);
        Star(V(cx + 3.2f, 3.3f, 12.9f), 1.1f, gold);
        // a pink star on the deck marks where she performs
        var deckStar = new GameObject("DeckStar");
        deckStar.transform.SetParent(t, false);
        deckStar.transform.localPosition = V(cx, h + 0.012f, 9.2f);
        deckStar.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        deckStar.transform.localScale = V(1.9f, 1.9f, 0.02f);
        deckStar.AddComponent<MeshFilter>().sharedMesh = starMesh;
        deckStar.AddComponent<MeshRenderer>().sharedMaterial = pink;

        // truss: two towers and a front beam, with zig-zag braces
        void Rod(Vector3 a, Vector3 b, float r)
        {
            var mid = (a + b) / 2f; var d = b - a;
            var g = Prim(PrimitiveType.Cylinder, "Rod", t, mid, V(r * 2f, d.magnitude / 2f, r * 2f), truss);
            g.transform.up = d.normalized;
        }
        void TrussBeam(Vector3 a, Vector3 b, Vector3 up, float width)
        {
            Vector3 top = up * width / 2f, bot = -up * width / 2f;
            Rod(a + top, b + top, 0.05f); Rod(a + bot, b + bot, 0.05f);
            int n = Mathf.Max(2, Mathf.RoundToInt((b - a).magnitude / width));
            for (int i = 0; i < n; i++)
            {
                var p0 = Vector3.Lerp(a, b, i / (float)n); var p1 = Vector3.Lerp(a, b, (i + 1) / (float)n);
                if (i % 2 == 0) Rod(p0 + bot, p1 + top, 0.03f); else Rod(p0 + top, p1 + bot, 0.03f);
            }
        }
        float frontZ = 7.5f;
        TrussBeam(V(4.7f, 6.4f, frontZ), V(13.3f, 6.4f, frontZ), Vector3.up, 0.6f);
        TrussBeam(V(4.7f, h, frontZ), V(4.7f, 6.4f, frontZ), Vector3.forward, 0.5f);
        TrussBeam(V(13.3f, h, frontZ), V(13.3f, 6.4f, frontZ), Vector3.forward, 0.5f);
        TrussBeam(V(4.7f, 6.4f, frontZ), V(4.7f, 6.4f, 12.8f), Vector3.up, 0.5f);
        TrussBeam(V(13.3f, 6.4f, frontZ), V(13.3f, 6.4f, 12.8f), Vector3.up, 0.5f);

        // speaker stacks on the deck
        var woofer = Lit("woofer", C("#2e2f55"), 0.4f);
        foreach (float sx in new[] { 5.5f, 12.5f })
        {
            Prim(PrimitiveType.Cube, "Speaker", t, V(sx, h + 0.95f, 8.3f), V(1.1f, 1.9f, 0.9f), dark);
            foreach (var wy in new[] { 0.55f, 1.35f })
                Prim(PrimitiveType.Cylinder, "Woofer", t, V(sx, h + wy, 7.83f), V(0.7f, 0.02f, 0.7f), woofer, V(90, 0, 0));
            Prim(PrimitiveType.Cylinder, "WooferRing", t, V(sx, h + 0.55f, 7.835f), V(0.8f, 0.015f, 0.8f), Lit("woofer_ring", C("#a8a9fc"), 0.4f), V(90, 0, 0));
            Prim(PrimitiveType.Cylinder, "WooferRing", t, V(sx, h + 1.35f, 7.835f), V(0.8f, 0.015f, 0.8f), Lit("woofer_ring", C("#a8a9fc"), 0.4f), V(90, 0, 0));
            Block(blocks, sx, 8.3f, 0.75f);
        }
        // mic stand in front of the spot
        Prim(PrimitiveType.Cylinder, "MicBase", t, V(cx + 0.7f, h + 0.02f, 8.9f), V(0.4f, 0.02f, 0.4f), dark);
        Prim(PrimitiveType.Cylinder, "MicPole", t, V(cx + 0.7f, h + 0.75f, 8.9f), V(0.035f, 0.75f, 0.035f), truss);
        Prim(PrimitiveType.Sphere, "MicHead", t, V(cx + 0.7f, h + 1.55f, 8.85f), V(0.16f, 0.16f, 0.16f), Lit("mic_head", C("#fff8ee"), 0.5f));

        // spotlights hanging from the beam, with soft beams down onto the deck
        var coneMat = BeamMaterial();
        Color[] lens = { new Color(3f, 0.7f, 1.6f), new Color(1.2f, 1.4f, 3.4f), new Color(3.2f, 2.5f, 0.8f), new Color(0.8f, 3f, 1.8f), new Color(3f, 0.7f, 1.6f), new Color(1.2f, 1.4f, 3.4f) };
        var housing = Lit("spot_housing", C("#26274f"), 0.5f);
        for (int i = 0; i < lens.Length; i++)
        {
            float x = 5.4f + i * 1.44f;
            var spot = Group("Spot" + i, t).transform;
            spot.position = V(x, 6.1f, 7.55f);
            spot.rotation = Quaternion.Euler(52f + (i % 2) * 6f, (i - 2.5f) * 6f, 0);
            Prim(PrimitiveType.Cylinder, "Housing", spot, V(0, 0, 0.25f), V(0.42f, 0.28f, 0.42f), housing, V(90, 0, 0));
            Prim(PrimitiveType.Cylinder, "Lens", spot, V(0, 0, 0.55f), V(0.34f, 0.02f, 0.34f), Lit("spot_lens" + i, Color.white, 0.2f, lens[i]), V(90, 0, 0), false);
            var beam = new GameObject("Beam");
            beam.transform.SetParent(spot, false);
            beam.transform.localPosition = V(0, 0, 0.57f);
            beam.AddComponent<MeshFilter>().sharedMesh = ConeMesh(6.5f, 1.25f, lens[i]);
            var br = beam.AddComponent<MeshRenderer>();
            br.sharedMaterial = coneMat;
            br.shadowCastingMode = ShadowCastingMode.Off;
            br.receiveShadows = false;
        }

        // keep walkers off the deck sides and the back wall
        for (float z = 7.6f; z <= 12.6f; z += 1.5f) Block(blocks, 3.4f, z, 0.5f);
        for (float x = 5f; x <= 13.6f; x += 1.4f) Block(blocks, x, 13.95f, 0.6f);
    }

    static Material BeamMaterial()
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
    static Mesh ConeMesh(float length, float radius, Color tint)
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
        string p = WorldArtBuilder.MeshDir + "/BeamCone_" + ColorUtility.ToHtmlStringRGB(new Color(tint.r / 4f, tint.g / 4f, tint.b / 4f)) + ".asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(p);
        if (old != null) { EditorUtility.CopySerialized(m, old); Object.DestroyImmediate(m); return old; }
        AssetDatabase.CreateAsset(m, p);
        return m;
    }

    // ---------- buildings ----------

    static void MakeBuildings(Transform t)
    {
        string[] names = { "building_A", "building_B", "building_C", "building_D", "building_E", "building_F", "building_G", "building_H" };
        // north row, behind the shop and the stage (fronts toward -z)
        string[] north = { "building_G", "building_C", "building_D", "building_H", "building_B" };
        for (int i = 0; i < north.Length; i++)
            Fbx(City + north[i] + ".fbx", t, V(-16f + i * 8f, 0, 19.6f), 0f, CityScale, cityMat, "North_" + (i + 1));
        // west and east columns (fronts toward the street)
        string[] west = { "building_E", "building_A", "building_F", "building_C" };
        string[] east = { "building_D", "building_E", "building_G", "building_A" };
        for (int i = 0; i < 4; i++)
        {
            Fbx(City + west[i] + ".fbx", t, V(-18.5f, 0, -10f + i * 8f), -90f, CityScale, cityMat, "West_" + (i + 1));
            Fbx(City + east[i] + ".fbx", t, V(18.5f, 0, -10f + i * 8f), 90f, CityScale, cityMat, "East_" + (i + 1));
        }
        // far south: low buildings seen past the park
        Fbx(City + "building_B.fbx", t, V(-17f, 0, -22f), 180f, CityScale, cityMat, "South_1");
        Fbx(City + "building_F.fbx", t, V(17f, 0, -22f), 180f, CityScale, cityMat, "South_2");
        Fbx(City + "building_A.fbx", t, V(0f, 0, -34f), 180f, CityScale, cityMat, "South_3");
    }

    // ---------- trees, bushes ----------

    static void MakeNature(Transform t, Transform blocks)
    {
        var trees = new[]
        {
            (-13.2f, -7.8f, "Tree_4_A_Color1", false), (-13.2f, 1.2f, "Tree_1_A_Color1", true), (13.4f, -7.0f, "Tree_2_A_Color1", true),
        };
        foreach (var tr in trees)
        {
            var tree = Fbx(Forest + tr.Item3 + ".fbx", t, V(tr.Item1, 0, tr.Item2), tr.Item1 * 40f, 1.1f, tr.Item4 ? blossomMat : forestMat, "Tree");
            tree.AddComponent<Occluder>();
            Block(blocks, tr.Item1, tr.Item2, 0.55f);
        }
        var bushes = new[] { (-4.5f, -12f), (4.5f, -12f), (-9.2f, -9.4f), (9.2f, -9.4f), (-2.4f, -7.9f), (2.4f, -7.9f), (13f, -2.4f), (-13f, -2.4f) };
        int k = 0;
        foreach (var b in bushes)
        {
            string f = (k++ % 3) switch { 0 => "Bush_1_A_Color1", 1 => "Bush_2_A_Color1", _ => "Bush_4_A_Color1" };
            Fbx(Forest + f + ".fbx", t, V(b.Item1, 0, b.Item2), k * 53f, 1.6f, (k % 2 == 0) ? blossomMat : forestMat, "Bush");
            Block(blocks, b.Item1, b.Item2, 0.5f);
        }
        Fbx(Forest + "Rock_1_A_Color1.fbx", t, V(-7.6f, 0, -10.6f), 20f, 1.4f, forestMat, "Rock");
    }

    // ---------- street furniture ----------

    static void MakeProps(Transform t, Transform blocks)
    {
        foreach (var x in new[] { -12.3f, -4.2f, 4.3f, 12.3f })
        {
            Fbx(City + "streetlight.fbx", t, V(x, 0, -0.85f), 0f, CityScale, cityMat, "Streetlight").AddComponent<Occluder>();
            Block(blocks, x, -0.85f, 0.3f);
        }
        Fbx(City + "firehydrant.fbx", t, V(-2.4f, 0, -0.8f), 0f, CityScale, cityMat, "Hydrant");
        Block(blocks, -2.4f, -0.8f, 0.35f);
        Fbx(City + "trash_A.fbx", t, V(5.9f, 0, -0.8f), 20f, CityScale, cityMat, "Bin");
        Block(blocks, 5.9f, -0.8f, 0.35f);
        // benches facing the path in the park, and one on the plaza
        var benches = new[] { (-3.6f, -9.8f, 90f), (3.6f, -9.8f, -90f), (-10.5f, 1.2f, 180f) };
        foreach (var b in benches)
        {
            Fbx(City + "bench.fbx", t, V(b.Item1, 0, b.Item2), b.Item3, CityScale, cityMat, "Bench");
            Block(blocks, b.Item1, b.Item2, 0.8f);
        }
    }

    // ---------- particles ----------

    static void MakeParticles()
    {
        // leaves and blossom petals drifting down
        var leaf = LeafTexture();
        var lm = LoadOrNew("leaf_particle", "Universal Render Pipeline/Particles/Unlit");
        lm.SetFloat("_Surface", 1f); lm.SetFloat("_Blend", 0f);
        lm.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        lm.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        lm.SetFloat("_ZWrite", 0f); lm.SetFloat("_Cull", 0f);
        lm.SetOverrideTag("RenderType", "Transparent");
        lm.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        lm.renderQueue = (int)RenderQueue.Transparent;
        lm.SetTexture("_BaseMap", leaf);
        lm.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(lm);

        var g = new GameObject("Leaves");
        g.transform.position = V(0, 7f, 2f);
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
        main.startColor = new ParticleSystem.MinMaxGradient(C("#ffb3cf"), C("#c8d86a"));
        main.gravityModifier = 0.05f;
        main.maxParticles = 70;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = 6f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = V(30f, 1f, 26f);
        var vel = ps.velocityOverLifetime; vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.5f); vel.y = new ParticleSystem.MinMaxCurve(-0.5f, -0.2f); vel.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.8f; noise.frequency = 0.25f; noise.scrollSpeed = 0.2f;
        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);

        // warm sun motes
        var dot = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/World/Textures/soft_dot.png");
        var mm = LoadOrNew("mote_particle", "Universal Render Pipeline/Particles/Unlit");
        mm.SetFloat("_Surface", 1f); mm.SetFloat("_Blend", 2f);
        mm.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mm.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mm.SetFloat("_ZWrite", 0f);
        mm.SetOverrideTag("RenderType", "Transparent");
        mm.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mm.renderQueue = (int)RenderQueue.Transparent;
        mm.SetTexture("_BaseMap", dot);
        mm.SetColor("_BaseColor", new Color(1.5f, 1.2f, 0.7f, 0.6f));
        EditorUtility.SetDirty(mm);
        var g2 = new GameObject("SunMotes");
        g2.transform.position = V(0, 3f, 3f);
        var p2 = g2.AddComponent<ParticleSystem>();
        var r2 = g2.GetComponent<ParticleSystemRenderer>();
        r2.sharedMaterial = mm; r2.shadowCastingMode = ShadowCastingMode.Off; r2.receiveShadows = false;
        var m2 = p2.main;
        m2.loop = true; m2.prewarm = true;
        m2.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
        m2.startSpeed = 0.05f;
        m2.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
        m2.startColor = Color.white;
        m2.maxParticles = 50;
        m2.simulationSpace = ParticleSystemSimulationSpace.World;
        var e2 = p2.emission; e2.rateOverTime = 7f;
        var s2 = p2.shape; s2.shapeType = ParticleSystemShapeType.Box; s2.scale = V(30f, 5f, 26f);
        var n2 = p2.noise; n2.enabled = true; n2.strength = 0.6f; n2.frequency = 0.3f; n2.scrollSpeed = 0.25f;
        var c2 = p2.colorOverLifetime; c2.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0.3f, 0.65f), new GradientAlphaKey(0f, 1f) });
        c2.color = grad;
    }

    static Material LoadOrNew(string name, string shader)
    {
        string path = WorldArtBuilder.MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
        return m;
    }
}
