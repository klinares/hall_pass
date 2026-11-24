// Phase 8.2 builder B: Place_Festival, "summer festival side stage, golden hour". A trodden field with striped tents, flags, two food
// trucks, fairy lights and a ferris wheel; the side stage stands on the west, a back crowd packs the mud in front of it, confetti drifts.
//
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -executeMethod PlaceFestivalBuilder.CreateAll
//
// Layout (x right, z away from the camera; walkers clamped to +-13.5):
//   spawn on the boardwalk (0, -10), welcome arch at z -4, lemonade stall west (-6, -6), food trucks east (x 5 and 10, z -2),
//   tents west and east, the side stage in the north-west (deck x -13.2 .. -4, z 8 .. 13.2, ramp from z 7.2), crowd box x -13 .. -4.2 z 2.3 .. 6.5,
//   a ferris wheel and a big top far to the north.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using WorldTour.World;
using WorldTour.World.Places;
using static PlaceBKit;

public static class PlaceFestivalBuilder
{
    public const string ScenePath = "Assets/World/Scenes/Place_Festival.unity";
    const string P = "fe_";

    static Material wood, cream, coral, teal, yellow, pink, lilac, orange, steel, dark, glowBulb, cloth;
    static Material forestMat, cityMat, restoMat;
    static readonly string[] FlagHex = { "#ff6f91", "#ffd23a", "#2fc4b2", "#a8a9fc", "#ff9a3c", "#ffffff" };

    public static void CreateAll()
    {
        try
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(WorldArtBuilder.StarPrefab) == null) WorldArtBuilder.Run();
            Directory.CreateDirectory("Assets/World/Scenes");
            MakeScene();
            AssetDatabase.SaveAssets();
            Debug.Log("PlaceFestivalBuilder: done " + ScenePath);
        }
        catch (System.Exception e) { Debug.LogError("PlaceFestivalBuilder failed: " + e); }
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    // ---------- textures ----------

    static Texture2D GrassTexture()
    {
        Color a = C("#a9c45c"), b = C("#97b84e"), c = C("#bdd06a");
        return MakeTexture("fe_grass", 128, 128, (x, y) =>
        {
            int h = (x * 73856093 ^ y * 19349663) & 1023;
            float n = Mathf.Sin(x * 0.21f) * Mathf.Sin(y * 0.17f) + Mathf.Sin(x * 0.07f + y * 0.11f);
            Color col = n > 0.9f ? c : (n < -0.7f ? b : a);
            return col * (0.95f + h / 1023f * 0.1f);
        }, TextureWrapMode.Repeat, FilterMode.Bilinear, true);
    }

    static Texture2D BoardTexture()
    {
        Color w = C("#c9965f");
        return MakeTexture("fe_boards", 128, 128, (x, y) =>
        {
            int col = x / 16;
            int yy = (y + col * 29) % 64;
            if (x % 16 < 1 || yy == 0) return w * 0.5f;
            float v = 0.9f + ((col * 37 + (y + col * 29) / 64 * 11) % 7) / 7f * 0.2f;
            return w * v;
        }, TextureWrapMode.Repeat, FilterMode.Bilinear, true);
    }

    static Texture2D MudTexture()
    {
        Color a = C("#7a5a3c"), b = C("#6a4c33"), c = C("#8c6a46");
        return MakeTexture("fe_mud", 128, 128, (x, y) =>
        {
            int h = (x * 73856093 ^ y * 19349663) & 1023;
            float n = Mathf.Sin(x * 0.3f + Mathf.Sin(y * 0.2f) * 2f) + Mathf.Sin(y * 0.13f + x * 0.05f);
            Color col = n > 0.8f ? c : (n < -0.6f ? b : a);
            return col * (0.94f + h / 1023f * 0.12f);
        }, TextureWrapMode.Repeat, FilterMode.Bilinear, true);
    }

    // ---------- the scene ----------

    static void MakeScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Reported.Clear();

        wood = LitTex(P + "wood", Color.white, BoardTexture(), 0.25f);
        cream = Lit(P + "cream", C("#fff3dc"), 0.15f);
        coral = Lit(P + "coral", C("#ff6f61"), 0.2f);
        teal = Lit(P + "teal", C("#2fc4b2"), 0.2f);
        yellow = Lit(P + "yellow", C("#ffd23a"), 0.2f);
        pink = Lit(P + "pink", C("#ff8fb8"), 0.2f);
        lilac = Lit(P + "lilac", C("#a8a9fc"), 0.2f);
        orange = Lit(P + "orange", C("#ff9a3c"), 0.2f);
        steel = Lit(P + "steel", C("#c9ccdf"), 0.6f);
        dark = Lit(P + "dark", C("#2c2a45"), 0.4f);
        glowBulb = Lit(P + "bulb", C("#fff0c8"), 0.3f, new Color(3.4f, 2.4f, 1.1f));
        cloth = DoubleSided(P + "cloth", C("#fff3dc"), 0.15f);
        forestMat = LitTex(P + "forest_kit", new Color(1.15f, 1.08f, 0.85f), WorldArtBuilder.ArtTexture(Forest + "forest_texture.png"), 0.08f);
        cityMat = LitTex(P + "city_kit", new Color(1.1f, 0.95f, 0.85f), WorldArtBuilder.ArtTexture(City + "citybits_texture.png"), 0.1f);
        restoMat = LitTex(P + "resto_kit", new Color(1.1f, 1f, 0.9f), WorldArtBuilder.ArtTexture(Resto + "restaurantbits_texture.png"), 0.1f);

        var profile = MakeProfile("FestivalVolumeProfile", new Grade
        {
            BloomThreshold = 0.95f, BloomIntensity = 0.5f, BloomScatter = 0.65f, BloomTint = new Color(1f, 0.9f, 0.75f),
            Exposure = 0.05f, Contrast = 18f, Saturation = 30f, Temperature = 14f, Tint = 2f, Vignette = 0.22f,
            Filter = new Color(1f, 0.97f, 0.92f),
            Shadows = new Vector4(0.84f, 0.88f, 1.12f, 0f), Highlights = new Vector4(1.12f, 1.0f, 0.82f, 0f),
        });
        var world = Group("World").transform;
        var ground = Group("Ground", world).transform;
        var gate = Group("Gate", world).transform;
        var field = Group("Field", world).transform;
        var stage = Group("Stage", world).transform;
        var far = Group("Far", world).transform;
        var props = Group("Props", world).transform;
        var blocks = Group("Blockers", world).transform;

        MakeGround(ground);
        MakeGate(gate, blocks);
        MakeField(field, blocks);
        MakeStage(stage, blocks);
        MakeFar(far);
        MakeFairyLights(world);
        MakeBackCrowd(world, blocks);

        var spec = new Spec
        {
            Id = "festival", ScenePath = ScenePath,
            Spawn = V(0, 0, -10f), Stage = V(-8.6f, 0, 9.6f), Crowd = V(-8.6f, 0, 4.4f), CrowdSize = new Vector2(8.8f, 4.2f), Exit = V(11.5f, 0, -10.5f), Riff = V(-1.8f, 0, -11.2f),
            Crew = new[] { V(-4.6f, 0, -6.2f), V(7.4f, 0, -5.4f), V(1.8f, 0, 6.6f) },
            Bg = new Color(0.75f, 0.85f, 1f),
            ExitRingBase = C("#ffd23a"), ExitRing = new Color(2.2f, 1.7f, 0.5f), ExitHole = C("#ff6f61"), ExitSign = C("#2fc4b2"), ExitYaw = 0f,
        };
        Contract(spec, blocks, profile, P);
        MakeAtmosphere(new Air
        {
            SkyName = "fe_sky", Top = C("#3f78d6"), Mid = C("#8dbcf0"), Horizon = C("#ffd48f"), Bottom = C("#f0b27a"),
            SunEuler = new Vector3(19f, 64f, 0f),
            AmbientSky = C("#8fb6ff"), AmbientEquator = C("#ffd9a8"), AmbientGround = C("#6b5c3c"), Fog = C("#f7d3a0"),
            AmbientIntensity = 0.95f, FogDensity = 0.0075f, Shade = C("#6a74b8"),
        });
        // golden hour: a low warm sun from the west, long shadows toward the east
        MakeSun(new Color(1f, 0.74f, 0.42f), 2.3f, new Vector3(19f, 64f, 0f), 0.9f);
        MakeParticles();
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    // ---------- ground ----------

    static void MakeGround(Transform t)
    {
        var grass = LitTex(P + "grass_mat", Color.white, GrassTexture(), 0.05f);
        grass.SetTextureScale("_BaseMap", new Vector2(36f, 36f));
        Prim(PrimitiveType.Plane, "Field", t, V(0, -0.03f, 10f), V(24, 1, 24), grass, null, false);
        // the boardwalk from the gate up the middle
        var boards = LitTex(P + "boardwalk", Color.white, BoardTexture(), 0.25f);
        boards.SetTextureScale("_BaseMap", new Vector2(2f, 7f));
        Prim(PrimitiveType.Cube, "Boardwalk", t, V(0, 0.03f, -5.5f), V(3.0f, 0.06f, 16f), boards, null, false);
        foreach (float x in new[] { -1.55f, 1.55f }) Prim(PrimitiveType.Cube, "BoardEdge", t, V(x, 0.05f, -5.5f), V(0.1f, 0.1f, 16f), Lit(P + "edge", C("#8a5f3a"), 0.2f), null, false);
        // churned mud under the crowd, and a trodden patch by the trucks
        var mud = LitTex(P + "mud_mat", Color.white, MudTexture(), 0.05f);
        mud.SetTextureScale("_BaseMap", new Vector2(3f, 2f));
        Prim(PrimitiveType.Cylinder, "Mud", t, V(-8.6f, 0.006f, 4.6f), V(13.5f, 0.01f, 9.5f), mud, null, false);
        Prim(PrimitiveType.Cylinder, "MudB", t, V(-5.0f, 0.005f, 2.2f), V(5f, 0.01f, 3.2f), mud, null, false);
        var dirt = LitTex(P + "dirt_mat", Color.white, MudTexture(), 0.05f);
        dirt.SetTextureScale("_BaseMap", new Vector2(4f, 2f));
        Prim(PrimitiveType.Cylinder, "Trodden", t, V(8f, 0.004f, -4.3f), V(14f, 0.01f, 5f), dirt, null, false);
        // a light pool of glitter-gold where the sun comes in low
        var dot = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/World/Textures/soft_dot.png");
        var glow = ParticleMaterial("fe_sunpool", dot, true, new Color(1.4f, 0.95f, 0.5f, 0.35f));
        Prim(PrimitiveType.Quad, "SunPool", t, V(-8.6f, 0.07f, 5f), V(14f, 10f, 1f), glow, V(90, 0, 0), false);
    }

    // ---------- the welcome arch ----------

    static void MakeGate(Transform t, Transform blocks)
    {
        const float z = -4f;
        var stripeA = coral; var stripeB = cream;
        foreach (float s in new[] { -1f, 1f })
        {
            float x = s * 3.4f;
            for (int i = 0; i < 6; i++) Prim(PrimitiveType.Cylinder, "PostStripe", t, V(x, 0.45f + i * 0.9f, z), V(0.46f, 0.45f, 0.46f), i % 2 == 0 ? stripeA : stripeB);
            Prim(PrimitiveType.Sphere, "PostBall", t, V(x, 5.75f, z), V(0.7f, 0.7f, 0.7f), yellow);
            Block(blocks, x, z, 0.5f);
        }
        // two sunburst boards on the posts, off to the sides so nothing hangs over the path (the camera would hide only part of a sign across it)
        var rays = Lit(P + "sun_ray", C("#ffe9a0"), 0.3f, new Color(1.8f, 1.4f, 0.6f));
        var sunMat = Lit(P + "sun", C("#ffd23a"), 0.3f, new Color(2.4f, 1.6f, 0.4f));
        var bulbsMb = new MB();
        foreach (float s in new[] { -1f, 1f })
        {
            float bx = s * 4.9f;
            Prim(PrimitiveType.Cube, "SignFrame", t, V(bx, 4.3f, z + 0.05f), V(2.8f, 2.4f, 0.1f), coral);
            Prim(PrimitiveType.Cube, "SignBoard", t, V(bx, 4.3f, z), V(2.5f, 2.1f, 0.2f), teal);
            Prim(PrimitiveType.Cylinder, "Sun", t, V(bx, 4.3f, z - 0.15f), V(0.95f, 0.03f, 0.95f), sunMat, V(90, 0, 0), false);
            for (int i = 0; i < 11; i++)
            {
                float a = -100f + i * 20f;
                var r = Prim(PrimitiveType.Cube, "Ray", t, V(bx, 4.3f, z - 0.14f), V(0.1f, 0.4f, 0.03f), rays, V(0, 0, a), false);
                r.transform.localPosition = V(bx, 4.3f, z - 0.14f) + Quaternion.Euler(0, 0, a) * V(0, 0.8f, 0);
            }
            for (float x = -1.2f; x <= 1.21f; x += 0.4f) { bulbsMb.Gem(V(bx + x, 5.5f, z - 0.2f), 0.07f, 0.1f, 0.07f); bulbsMb.Gem(V(bx + x, 3.1f, z - 0.2f), 0.07f, 0.1f, 0.07f); }
            Prim(PrimitiveType.Cylinder, "SignPole", t, V(bx, 1.4f, z + 0.1f), V(0.1f, 1.4f, 0.1f), wood);
            Block(blocks, bx, z, 0.45f);
        }
        MeshObject("SignBulbs", t, bulbsMb.ToMesh("fe_sign_bulbs"), glowBulb, false);

        // the lemonade stall by the arch and a flag line
        MakeStall(t, blocks, V(-6.6f, 0, -6.4f), yellow, cream, true);
        MakeStall(t, blocks, V(4.2f, 0, -7.2f), pink, cream, false);
    }

    static void MakeStall(Transform t, Transform blocks, Vector3 p, Material a, Material b, bool lemon)
    {
        var g = Group(lemon ? "LemonadeStall" : "SweetStall", t).transform; g.position = p;
        Prim(PrimitiveType.Cube, "Counter", g, V(0, 0.55f, 0), V(2.4f, 1.1f, 0.9f), wood);
        Prim(PrimitiveType.Cube, "CounterTop", g, V(0, 1.14f, -0.02f), V(2.6f, 0.08f, 1.05f), cream);
        foreach (float sx in new[] { -1.15f, 1.15f }) { Prim(PrimitiveType.Cylinder, "Post", g, V(sx, 1.3f, -0.45f), V(0.08f, 1.3f, 0.08f), steel); Prim(PrimitiveType.Cylinder, "PostB", g, V(sx, 1.3f, 0.45f), V(0.08f, 1.3f, 0.08f), steel); }
        for (int i = 0; i < 7; i++)
        {
            var m = i % 2 == 0 ? a : b;
            Prim(PrimitiveType.Cube, "Awning", g, V(-1.2f + i * 0.4f, 2.7f, -0.2f), V(0.4f, 0.06f, 1.6f), m, V(-16f, 0, 0));
            Prim(PrimitiveType.Cube, "Valance", g, V(-1.2f + i * 0.4f, 2.42f, -0.98f), V(0.4f, 0.24f, 0.05f), m);
        }
        if (lemon)
        {
            var lm = new MB(); lm.Gem(Vector3.zero, 0.34f, 0.26f, 0.26f);
            var l = MeshObject("BigLemon", g, lm.ToMesh("fe_lemon"), Lit(P + "lemon", C("#ffe14a"), 0.4f, new Color(0.3f, 0.25f, 0.05f)), true);
            l.transform.localPosition = V(0, 3.0f, -0.3f);
            // jugs on the counter
            for (int i = 0; i < 3; i++)
            {
                Prim(PrimitiveType.Cylinder, "Jug", g, V(-0.8f + i * 0.8f, 1.42f, 0f), V(0.3f, 0.26f, 0.3f), Lit(P + "jug", C("#ffe9a0"), 0.6f, new Color(0.25f, 0.2f, 0.05f)));
            }
        }
        else
        {
            for (int i = 0; i < 4; i++) Prim(PrimitiveType.Sphere, "Candy", g, V(-0.9f + i * 0.6f, 1.34f, 0f), V(0.28f, 0.28f, 0.28f), i % 2 == 0 ? pink : lilac);
        }
        BlockLine(blocks, p + V(-1.1f, 0, 0), p + V(1.1f, 0, 0), 0.7f);
    }

    // ---------- the field: tents, trucks, picnic, trees ----------

    static void MakeField(Transform t, Transform blocks)
    {
        MakeTent(t, blocks, V(-10.8f, 0, -6.2f), 3.6f, 3.0f, coral, cream, 8f, 0f);
        MakeTent(t, blocks, V(-8.0f, 0, -10.8f), 3.4f, 3.0f, teal, cream, 4f, 0f);
        MakeTent(t, blocks, V(8.2f, 0, 6.8f), 3.8f, 3.2f, yellow, cream, 5.5f, 0f);
        MakeTent(t, blocks, V(12.0f, 0, 2.6f), 3.2f, 2.8f, pink, cream, 5f, 0f);
        MakeTruck(t, blocks, V(4.8f, 0, -1.4f), coral, yellow, true);
        MakeTruck(t, blocks, V(10.2f, 0, -1.4f), teal, pink, false);
        // trampled path and benches: haybales around the crowd, picnic blankets
        var hay = Lit(P + "hay", C("#e2b85a"), 0.1f);
        var hayEnd = Lit(P + "hay_end", C("#f0cf7a"), 0.1f);
        float[] hx = { -3.4f, -3.2f, -13.2f, 2.8f };
        float[] hz = { 3.0f, 5.4f, 7.0f, 8.0f };
        for (int i = 0; i < hx.Length; i++)
        {
            Prim(PrimitiveType.Cylinder, "HayBale", t, V(hx[i], 0.45f, hz[i]), V(0.9f, 0.55f, 0.9f), hay, V(0, 0, 90f));
            Prim(PrimitiveType.Cylinder, "HayEnd", t, V(hx[i] + 0.0f, 0.45f, hz[i]), V(0.7f, 0.58f, 0.7f), hayEnd, V(0, 0, 90f), false);
            Block(blocks, hx[i], hz[i], 0.6f);
        }
        MakeBlanket(t, V(3.4f, 0, 2.6f), coral, cream, 20f);
        MakeBlanket(t, V(5.8f, 0, 9.6f), teal, yellow, -10f);
        MakeBlanket(t, V(-2.4f, 0, 11f), pink, cream, 35f);
        // a bus stop beside the way out, a parked shuttle
        Fbx(City + "car_taxi.fbx", t, V(11.2f, 0, -13.0f), 90f, CityScale, cityMat, "Shuttle");
        Block(blocks, 10f, -13f, 0.9f); Block(blocks, 11.2f, -13f, 0.9f); Block(blocks, 12.4f, -13f, 0.9f);
        // trees along the sides, golden
        float[] tz = { -12.5f, -6.5f, 0.5f, 7.5f, 12.5f };
        string[] kinds = { "Tree_3_A_Color1", "Tree_1_A_Color1", "Tree_2_A_Color1", "Tree_3_A_Color1", "Tree_1_A_Color1" };
        for (int i = 0; i < tz.Length; i++)
        {
            Fbx(Forest + kinds[i] + ".fbx", t, V(13.4f + (i % 2) * 1.4f, 0, tz[i] + (i == 2 ? 3.5f : 0)), i * 67f, 1.35f, forestMat, "EdgeTree");
            if (i > 1) Fbx(Forest + kinds[(i + 2) % 5] + ".fbx", t, V(-15.2f, 0, tz[i] - 3f), i * 41f, 1.35f, forestMat, "EdgeTree");
        }
        for (int i = 0; i < 6; i++)
        {
            float x = -3f + i * 2.2f;
            Fbx(Forest + (i % 2 == 0 ? "Bush_2_A_Color1.fbx" : "Bush_4_A_Color1.fbx"), t, V(x, 0, 12.4f + (i % 3) * 0.4f), i * 53f, 1.5f, forestMat, "Bush");
        }
        var rng = new System.Random(5);
        for (int i = 0; i < 40; i++)
        {
            float x = (float)(rng.NextDouble() * 2 - 1) * 13f, z = -13f + (float)rng.NextDouble() * 26f;
            if (Mathf.Abs(x) < 2.0f && z < 3f) continue;
            if (x < -3.5f && z > 1.5f && z < 8f) continue;
            Fbx(Forest + (i % 2 == 0 ? "Grass_1_A_Color1.fbx" : "Grass_2_A_Color1.fbx"), t, V(x, 0, z), i * 47f, 1.4f, forestMat, "Tuft");
        }
        // trash and a water point: a drum with a tap
        Fbx(City + "trash_A.fbx", t, V(2.6f, 0, -9.4f), 20f, CityScale, cityMat, "Bin"); Block(blocks, 2.6f, -9.4f, 0.4f);
        Fbx(City + "streetlight.fbx", t, V(-2.6f, 0, 1.2f), 0f, CityScale, cityMat, "Lamp"); Block(blocks, -2.6f, 1.2f, 0.3f);
    }

    static void MakeBlanket(Transform t, Vector3 p, Material a, Material b, float yaw)
    {
        var g = Group("Blanket", t).transform; g.position = p; g.rotation = Quaternion.Euler(0, yaw, 0);
        Prim(PrimitiveType.Cube, "Cloth", g, V(0, 0.02f, 0), V(2.0f, 0.03f, 1.6f), a, null, false);
        for (int i = 0; i < 4; i++) Prim(PrimitiveType.Cube, "Stripe", g, V(-0.75f + i * 0.5f, 0.04f, 0), V(0.14f, 0.015f, 1.6f), b, null, false);
        Prim(PrimitiveType.Cylinder, "Basket", g, V(0.4f, 0.2f, 0.2f), V(0.5f, 0.17f, 0.5f), wood);
    }

    static void MakeTent(Transform t, Transform blocks, Vector3 p, float w, float d, Material a, Material b, float roofH, float yaw)
    {
        var g = Group("Tent", t).transform; g.position = p; g.rotation = Quaternion.Euler(0, yaw, 0);
        float hw = w / 2f, hd = d / 2f, wallH = 2.3f;
        var ma = new MB(); var mb = new MB();
        int stripes = Mathf.RoundToInt(w / 0.5f);
        // back and side walls, front open
        for (int i = 0; i < stripes; i++)
        {
            float x0 = -hw + i * (w / stripes), x1 = x0 + w / stripes;
            (i % 2 == 0 ? ma : mb).Quad(V(x0, 0, hd), V(x1, 0, hd), V(x1, wallH, hd), V(x0, wallH, hd));
        }
        int dst = Mathf.RoundToInt(d / 0.5f);
        for (int i = 0; i < dst; i++)
        {
            float z0 = -hd + i * (d / dst), z1 = z0 + d / dst;
            var m = i % 2 == 0 ? ma : mb;
            m.Quad(V(-hw, 0, z0), V(-hw, 0, z1), V(-hw, wallH, z1), V(-hw, wallH, z0));
            m.Quad(V(hw, 0, z1), V(hw, 0, z0), V(hw, wallH, z0), V(hw, wallH, z1));
        }
        // pyramid roof in stripes (a peak above the centre), scalloped valance on the front
        Vector3 peak = V(0, roofH, 0);
        Vector3[] c = { V(-hw - 0.2f, wallH, -hd - 0.2f), V(hw + 0.2f, wallH, -hd - 0.2f), V(hw + 0.2f, wallH, hd + 0.2f), V(-hw - 0.2f, wallH, hd + 0.2f) };
        int seg = 6;
        for (int e = 0; e < 4; e++)
        {
            Vector3 s0 = c[e], s1 = c[(e + 1) % 4];
            for (int i = 0; i < seg; i++)
            {
                Vector3 q0 = Vector3.Lerp(s0, s1, i / (float)seg), q1 = Vector3.Lerp(s0, s1, (i + 1) / (float)seg);
                (((e * seg + i) % 2) == 0 ? ma : mb).Tri(q0, q1, peak);
            }
        }
        for (int i = 0; i < stripes; i++)
        {
            float x0 = -hw - 0.2f + i * ((w + 0.4f) / stripes), x1 = x0 + (w + 0.4f) / stripes;
            (i % 2 == 0 ? mb : ma).Tri(V(x0, wallH, -hd - 0.2f), V(x1, wallH, -hd - 0.2f), V((x0 + x1) / 2, wallH - 0.35f, -hd - 0.2f));
        }
        string id = Mathf.RoundToInt(p.x * 10) + "_" + Mathf.RoundToInt(p.z * 10);
        MeshObject("TentA", g, ma.ToMesh("fe_tent_a_" + id), DoubleSided(P + "tent_" + a.name, a.color, 0.15f), true);
        MeshObject("TentB", g, mb.ToMesh("fe_tent_b_" + id), DoubleSided(P + "tent_" + b.name, b.color, 0.15f), true);
        Prim(PrimitiveType.Cylinder, "Mast", g, V(0, roofH + 0.4f, 0), V(0.06f, 0.4f, 0.06f), steel);
        Prim(PrimitiveType.Sphere, "Finial", g, V(0, roofH + 0.85f, 0), V(0.2f, 0.2f, 0.2f), yellow);
        foreach (float sx in new[] { -hw - 0.2f, hw + 0.2f }) Prim(PrimitiveType.Cylinder, "Pole", g, V(sx, wallH / 2, -hd - 0.2f), V(0.08f, wallH / 2, 0.08f), wood);
        // inside: a trestle table with a few goods
        Prim(PrimitiveType.Cube, "Table", g, V(0, 0.55f, hd - 0.7f), V(w - 0.8f, 0.1f, 0.7f), wood);
        Prim(PrimitiveType.Cube, "TableCloth", g, V(0, 0.4f, hd - 0.7f), V(w - 0.7f, 0.5f, 0.72f), a);
        for (int i = 0; i < 4; i++) Prim(PrimitiveType.Sphere, "Goods", g, V(-w / 2 + 0.9f + i * ((w - 1.8f) / 3f), 0.78f, hd - 0.7f), V(0.3f, 0.3f, 0.3f), i % 2 == 0 ? pink : yellow);
        BlockLine(blocks, p + V(-hw, 0, hd), p + V(hw, 0, hd), 0.6f);
        BlockLine(blocks, p + V(-hw, 0, -hd), p + V(-hw, 0, hd), 0.6f);
        BlockLine(blocks, p + V(hw, 0, -hd), p + V(hw, 0, hd), 0.6f);
    }

    static void MakeTruck(Transform t, Transform blocks, Vector3 p, Material body, Material accent, bool burger)
    {
        var g = Group(burger ? "BurgerTruck" : "IceCreamTruck", t).transform; g.position = p;
        // box body, a lower cab at the east end, wheels
        Prim(PrimitiveType.Cube, "Body", g, V(-0.5f, 1.6f, 0), V(3.6f, 2.2f, 2.2f), body);
        Prim(PrimitiveType.Cube, "BodyStripe", g, V(-0.5f, 0.95f, -1.12f), V(3.62f, 0.3f, 0.04f), accent, null, false);
        Prim(PrimitiveType.Cube, "Cab", g, V(1.9f, 1.15f, 0), V(1.3f, 1.5f, 2.1f), body);
        Prim(PrimitiveType.Cube, "CabGlass", g, V(2.56f, 1.55f, 0), V(0.05f, 0.7f, 1.7f), Lit(P + "truck_glass", C("#9fd0ff"), 0.7f));
        Prim(PrimitiveType.Cube, "Bumper", g, V(2.6f, 0.45f, 0), V(0.2f, 0.25f, 2.1f), steel);
        var tyre = Lit(P + "tyre", C("#2a2838"), 0.2f);
        foreach (float wx in new[] { -1.3f, 1.8f })
            foreach (float wz in new[] { -1.1f, 1.1f })
            {
                Prim(PrimitiveType.Cylinder, "Wheel", g, V(wx, 0.42f, wz), V(0.85f, 0.14f, 0.85f), tyre, V(90, 0, 0));
                Prim(PrimitiveType.Cylinder, "Hub", g, V(wx, 0.42f, wz * 1.06f), V(0.4f, 0.14f, 0.4f), steel, V(90, 0, 0));
            }
        // serving window on the camera side (-z): a dark opening, a counter, a striped awning
        Prim(PrimitiveType.Cube, "Window", g, V(-0.5f, 1.85f, -1.12f), V(2.3f, 0.95f, 0.05f), Lit(P + "truck_window", C("#ffd9a0"), 0.3f, new Color(1.2f, 0.8f, 0.4f)));
        Prim(PrimitiveType.Cube, "Counter", g, V(-0.5f, 1.35f, -1.35f), V(2.5f, 0.07f, 0.55f), cream);
        for (int i = 0; i < 6; i++)
        {
            var m = i % 2 == 0 ? accent : cream;
            Prim(PrimitiveType.Cube, "Awning", g, V(-1.55f + i * 0.42f, 2.62f, -1.55f), V(0.42f, 0.05f, 1.0f), m, V(-22f, 0, 0));
        }
        // the roof sign: a burger or an ice cream cone, made of simple shapes
        if (burger)
        {
            Prim(PrimitiveType.Sphere, "BunTop", g, V(-0.5f, 3.35f, 0), V(1.3f, 0.7f, 1.3f), Lit(P + "bun", C("#e8a14a"), 0.3f));
            Prim(PrimitiveType.Cylinder, "Lettuce", g, V(-0.5f, 3.0f, 0), V(1.35f, 0.05f, 1.35f), Lit(P + "lettuce", C("#69c85a"), 0.2f));
            Prim(PrimitiveType.Cylinder, "Patty", g, V(-0.5f, 2.88f, 0), V(1.25f, 0.1f, 1.25f), Lit(P + "patty", C("#6e4630"), 0.2f));
            Prim(PrimitiveType.Sphere, "BunBottom", g, V(-0.5f, 2.74f, 0), V(1.25f, 0.4f, 1.25f), Lit(P + "bun", C("#e8a14a"), 0.3f));
        }
        else
        {
            var cone = new MB(); cone.Gem(Vector3.zero, 0.5f, 0.85f, 0.5f);
            var cm = MeshObject("Cone", g, cone.ToMesh("fe_truck_cone"), Lit(P + "waffle", C("#e0a96a"), 0.1f), true);
            cm.transform.localPosition = V(-0.5f, 3.35f, 0); cm.transform.localRotation = Quaternion.Euler(0, 0, 180f);
            Prim(PrimitiveType.Sphere, "ScoopA", g, V(-0.5f, 4.05f, 0), V(1.05f, 0.95f, 1.05f), pink);
            Prim(PrimitiveType.Sphere, "ScoopB", g, V(-0.5f, 4.7f, 0), V(0.8f, 0.75f, 0.8f), lilac);
            Prim(PrimitiveType.Sphere, "Cherry", g, V(-0.5f, 5.15f, 0), V(0.25f, 0.25f, 0.25f), coral);
        }
        // a chalkboard-free menu: colour bars on a board by the window
        Prim(PrimitiveType.Cube, "MenuBoard", g, V(1.0f, 1.9f, -1.14f), V(0.7f, 0.9f, 0.05f), dark);
        for (int i = 0; i < 3; i++) Prim(PrimitiveType.Cube, "MenuBar", g, V(1.0f, 2.15f - i * 0.25f, -1.18f), V(0.5f - i * 0.08f, 0.07f, 0.03f), i == 0 ? yellow : cream, null, false);
        BlockLine(blocks, p + V(-2.2f, 0, 0), p + V(2.4f, 0, 0), 1.0f);
    }

    // ---------- the side stage ----------

    static void MakeStage(Transform t, Transform blocks)
    {
        const float cx = -8.6f, cz = 10.6f, h = 0.55f;
        var deckMat = LitTex(P + "deck", new Color(1.15f, 1.05f, 0.95f), BoardTexture(), 0.3f);
        deckMat.SetTextureScale("_BaseMap", new Vector2(3f, 2f));
        Deck(t, blocks, cx, cz, 4.6f, 2.6f, h, deckMat, coral);
        // back wall: teal fabric with a big sun and a ring of rays
        Prim(PrimitiveType.Cube, "BackWall", t, V(cx, 3.6f, 13.5f), V(9.4f, 7.0f, 0.3f), Lit(P + "back", C("#1f6f7a"), 0.1f), null, false);
        var sunMat = Lit(P + "stage_sun", C("#ffd23a"), 0.3f, new Color(2.2f, 1.5f, 0.35f));
        Prim(PrimitiveType.Cylinder, "StageSun", t, V(cx, 3.7f, 13.3f), V(2.9f, 0.04f, 2.9f), sunMat, V(90, 0, 0), false);
        Prim(PrimitiveType.Cylinder, "StageSunRing", t, V(cx, 3.7f, 13.32f), V(3.6f, 0.03f, 3.6f), Lit(P + "stage_sun_ring", C("#ff8fb8"), 0.3f, new Color(1.6f, 0.5f, 0.8f)), V(90, 0, 0), false);
        var rayMat = Lit(P + "stage_ray", C("#ffe9a0"), 0.3f, new Color(1.8f, 1.3f, 0.5f));
        for (int i = 0; i < 16; i++)
        {
            float a = i * 22.5f;
            var r = Prim(PrimitiveType.Cube, "StageRay", t, V(cx, 3.7f, 13.28f), V(0.22f, 1.1f, 0.04f), rayMat, V(0, 0, a), false);
            r.transform.localPosition = V(cx, 3.7f, 13.28f) + Quaternion.Euler(0, 0, a) * V(0, 2.55f, 0);
        }
        // truss: front header, two towers, a back header, a cloth roof of coloured panels
        float fz = 7.9f, bz = 13.1f, top = 6.4f;
        TrussBeam(t, steel, V(cx - 4.8f, top, fz), V(cx + 4.8f, top, fz), Vector3.up, 0.6f);
        TrussBeam(t, steel, V(cx - 4.8f, h, fz), V(cx - 4.8f, top, fz), Vector3.forward, 0.5f);
        TrussBeam(t, steel, V(cx + 4.8f, h, fz), V(cx + 4.8f, top, fz), Vector3.forward, 0.5f);
        TrussBeam(t, steel, V(cx - 4.8f, top, fz), V(cx - 4.8f, top, bz), Vector3.up, 0.5f);
        TrussBeam(t, steel, V(cx + 4.8f, top, fz), V(cx + 4.8f, top, bz), Vector3.up, 0.5f);
        TrussBeam(t, steel, V(cx - 4.8f, top, bz), V(cx + 4.8f, top, bz), Vector3.up, 0.5f);
        var roof = new MB(); var roofB = new MB();
        for (int i = 0; i < 10; i++)
        {
            float x0 = cx - 4.9f + i * 0.98f, x1 = x0 + 0.98f;
            (i % 2 == 0 ? roof : roofB).Quad(V(x0, top + 0.35f, fz - 0.4f), V(x1, top + 0.35f, fz - 0.4f), V(x1, top + 0.1f, bz + 0.2f), V(x0, top + 0.1f, bz + 0.2f));
        }
        MeshObject("RoofA", t, roof.ToMesh("fe_stage_roof_a"), DoubleSided(P + "roof_a", C("#ff6f61"), 0.2f), true);
        MeshObject("RoofB", t, roofB.ToMesh("fe_stage_roof_b"), DoubleSided(P + "roof_b", C("#fff3dc"), 0.2f), true);
        // side flags: tall pennant poles at the tower tops
        var pennants = new MB[] { new MB(), new MB(), new MB() };
        foreach (float sx in new[] { -1f, 1f })
        {
            Prim(PrimitiveType.Cylinder, "FlagPole", t, V(cx + sx * 4.8f, 4.2f, fz - 0.2f), V(0.07f, 4.2f, 0.07f), steel);
            Vector3 b0 = V(cx + sx * 4.8f, 8.0f, fz - 0.2f);
            for (int i = 0; i < 3; i++) pennants[i].Quad(b0 + V(0, -0.1f - i * 0.5f, 0), b0 + V(sx * 1.6f, -0.2f - i * 0.5f + 0.05f, -0.2f), b0 + V(sx * 1.6f, -0.45f - i * 0.5f, -0.2f), b0 + V(0, -0.5f - i * 0.5f, 0));
        }
        Material[] pm = { coral, yellow, teal };
        for (int i = 0; i < 3; i++) MeshObject("Pennants" + i, t, pennants[i].ToMesh("fe_pennants" + i), DoubleSided(P + "pennant" + i, pm[i].color, 0.2f), false);
        // speaker stacks either side of the deck, a mic stand and a sun on the deck
        var woofer = Lit(P + "woofer", C("#2e2f55"), 0.4f);
        var ring = Lit(P + "woofer_ring", C("#ffd23a"), 0.4f);
        foreach (float sx in new[] { cx - 3.7f, cx + 3.7f }) Speaker(t, blocks, V(sx, h, fz + 0.9f), dark, woofer, ring);
        Prim(PrimitiveType.Cylinder, "MicBase", t, V(cx + 0.7f, h + 0.02f, 9.0f), V(0.4f, 0.02f, 0.4f), dark);
        Prim(PrimitiveType.Cylinder, "MicPole", t, V(cx + 0.7f, h + 0.75f, 9.0f), V(0.035f, 0.75f, 0.035f), steel);
        Prim(PrimitiveType.Sphere, "MicHead", t, V(cx + 0.7f, h + 1.55f, 8.95f), V(0.16f, 0.16f, 0.16f), cream);
        var deckSun = Prim(PrimitiveType.Cylinder, "DeckSun", t, V(cx, h + 0.012f, 9.6f), V(2.0f, 0.012f, 2.0f), Lit(P + "deck_sun", C("#ffd23a"), 0.3f, new Color(1.0f, 0.65f, 0.15f)), null, false);
        // spots on the header
        var housing = Lit(P + "spot_housing", C("#26274f"), 0.5f);
        Color[] lens = { new Color(3.2f, 2.2f, 0.7f), new Color(3f, 0.7f, 1.6f), new Color(0.8f, 3f, 2.4f), new Color(3.2f, 2.2f, 0.7f), new Color(3f, 0.7f, 1.6f), new Color(0.8f, 3f, 2.4f) };
        for (int i = 0; i < lens.Length; i++)
        {
            Vector3 pos = V(cx - 3.8f + i * 1.52f, top - 0.45f, fz + 0.05f);
            Vector3 aim = V(cx + (i - 2.5f) * 0.45f, h, 9.6f);
            SpotLight(t, "Spot" + i, pos, Quaternion.LookRotation(aim - pos), lens[i], housing, Lit(P + "spot_lens" + i, Color.white, 0.2f, lens[i]), 7.4f, 1.1f, 0.2f);
        }
        for (float x = cx - 4.8f; x <= cx + 4.9f; x += 1.4f) Block(blocks, x, 13.9f, 0.6f);
    }

    // ---------- the far field: a big top, the wheel, hills ----------

    static void MakeFar(Transform t)
    {
        // hills melting into the haze
        var hill = Lit(P + "hill", C("#94b062"), 0.05f);
        var hill2 = Lit(P + "hill2", C("#a9bf72"), 0.05f);
        Prim(PrimitiveType.Sphere, "HillA", t, V(-24f, -4f, 52f), V(70f, 18f, 30f), hill, null, false);
        Prim(PrimitiveType.Sphere, "HillB", t, V(26f, -5f, 58f), V(80f, 20f, 34f), hill2, null, false);
        Prim(PrimitiveType.Sphere, "HillC", t, V(0f, -6f, 70f), V(90f, 18f, 30f), hill, null, false);
        // the big top, east and north, with a flag
        var ma = new MB(); var mb = new MB();
        Vector3 c = V(10.5f, 0, 21f);
        float r = 6.0f, wallH = 3.6f, peakH = 10.5f;
        int n = 20;
        for (int i = 0; i < n; i++)
        {
            float a0 = i / (float)n * Mathf.PI * 2f, a1 = (i + 1) / (float)n * Mathf.PI * 2f;
            Vector3 p0 = c + V(Mathf.Cos(a0) * r, 0, Mathf.Sin(a0) * r), p1 = c + V(Mathf.Cos(a1) * r, 0, Mathf.Sin(a1) * r);
            var m = i % 2 == 0 ? ma : mb;
            m.Quad(p0, p1, p1 + V(0, wallH, 0), p0 + V(0, wallH, 0));
            m.Tri(p0 + V(0, wallH, 0), p1 + V(0, wallH, 0), c + V(0, peakH, 0));
        }
        MeshObject("BigTopA", t, ma.ToMesh("fe_bigtop_a"), DoubleSided(P + "bigtop_a", C("#ff6f61"), 0.15f), true);
        MeshObject("BigTopB", t, mb.ToMesh("fe_bigtop_b"), DoubleSided(P + "bigtop_b", C("#fff3dc"), 0.15f), true);
        Prim(PrimitiveType.Cylinder, "BigTopMast", t, c + V(0, peakH + 1.1f, 0), V(0.12f, 1.1f, 0.12f), steel);
        var fl = new MB(); fl.Quad(c + V(0, peakH + 2.0f, 0), c + V(1.5f, peakH + 1.8f, 0.2f), c + V(1.5f, peakH + 1.3f, 0.2f), c + V(0, peakH + 1.5f, 0));
        MeshObject("BigTopFlag", t, fl.ToMesh("fe_bigtop_flag"), DoubleSided(P + "bigtop_flag", C("#ffd23a"), 0.2f), false);
        // the ferris wheel behind the stage
        MakeWheel(t, V(-1.5f, 0, 27f), 8.4f);
        // far trees
        for (int i = 0; i < 12; i++)
            Fbx(Forest + (i % 3 == 0 ? "Tree_1_A_Color1.fbx" : "Tree_3_A_Color1.fbx"), t, V(-30f + i * 5.6f, 0, 36f + (i % 3) * 3f), i * 71f, 2.2f, forestMat, "FarTree");
        for (int i = 0; i < 7; i++)
        {
            Fbx(Forest + "Tree_2_A_Color1.fbx", t, V(-24f, 0, -14f + i * 7f), i * 39f, 1.6f, forestMat, "SideTreeW");
            Fbx(Forest + "Tree_1_A_Color1.fbx", t, V(24f, 0, -14f + i * 7f), i * 53f, 1.6f, forestMat, "SideTreeE");
        }
    }

    static void MakeWheel(Transform t, Vector3 baseP, float radius)
    {
        float cy = radius + 1.8f;
        var frame = Lit(P + "wheel_frame", C("#f4f0ea"), 0.4f);
        var wheel = Group("FerrisWheel", t).transform;
        wheel.position = baseP + V(0, cy, 0);
        var spin = wheel.gameObject.AddComponent<PlaceMotion>(); spin.kind = PlaceMotion.Kind.Spin; spin.axis = Vector3.forward; spin.speed = 4f;
        int n = 24;
        for (int i = 0; i < n; i++)
        {
            float a0 = i / (float)n * Mathf.PI * 2f, a1 = (i + 1) / (float)n * Mathf.PI * 2f;
            Rod(wheel, frame, V(Mathf.Cos(a0) * radius, Mathf.Sin(a0) * radius, 0.5f), V(Mathf.Cos(a1) * radius, Mathf.Sin(a1) * radius, 0.5f), 0.12f);
            Rod(wheel, frame, V(Mathf.Cos(a0) * radius, Mathf.Sin(a0) * radius, -0.5f), V(Mathf.Cos(a1) * radius, Mathf.Sin(a1) * radius, -0.5f), 0.12f);
        }
        for (int i = 0; i < 12; i++)
        {
            float a = i / 12f * Mathf.PI * 2f;
            Rod(wheel, frame, V(0, 0, 0.5f), V(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0.5f), 0.05f);
            Rod(wheel, frame, V(0, 0, -0.5f), V(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, -0.5f), 0.05f);
            Rod(wheel, frame, V(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0.5f), V(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, -0.5f), 0.05f);
        }
        Prim(PrimitiveType.Cylinder, "Hub", wheel, V(0, 0, 0), V(1.2f, 0.7f, 1.2f), frame, V(90, 0, 0));
        // cabins hang from the rim and stay upright as it turns (they counter-rotate)
        Material[] cab = { coral, yellow, teal, pink, lilac, orange };
        for (int i = 0; i < 12; i++)
        {
            float a = i / 12f * Mathf.PI * 2f;
            var pivot = Group("CabinPivot", wheel).transform;
            pivot.localPosition = V(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0);
            var back = pivot.gameObject.AddComponent<PlaceMotion>(); back.kind = PlaceMotion.Kind.Spin; back.axis = Vector3.forward; back.speed = -4f; back.phase = 0f;
            Prim(PrimitiveType.Cube, "Cabin", pivot, V(0, -0.75f, 0), V(1.3f, 1.0f, 1.0f), cab[i % cab.Length]);
            Prim(PrimitiveType.Cube, "CabinRoof", pivot, V(0, -0.2f, 0), V(1.5f, 0.12f, 1.2f), cream);
            Prim(PrimitiveType.Cube, "CabinGlass", pivot, V(0, -0.7f, -0.51f), V(1.0f, 0.5f, 0.03f), Lit(P + "cabin_glass", C("#ffe9b0"), 0.5f, new Color(0.9f, 0.7f, 0.35f)), null, false);
        }
        var bulbs = new MB();
        for (int i = 0; i < 48; i++)
        {
            float a = i / 48f * Mathf.PI * 2f;
            bulbs.Gem(V(Mathf.Cos(a) * (radius + 0.25f), Mathf.Sin(a) * (radius + 0.25f), 0), 0.12f, 0.12f, 0.12f);
        }
        MeshObject("WheelBulbs", wheel, bulbs.ToMesh("fe_wheel_bulbs"), Lit(P + "wheel_bulb", C("#fff0c8"), 0.3f, new Color(3.6f, 2.6f, 1.2f)), false);
        // the A-frame legs and a little base
        foreach (float z in new[] { -0.9f, 0.9f })
        {
            Rod(t, frame, baseP + V(-4.2f, 0.3f, z), baseP + V(0, cy, z), 0.16f);
            Rod(t, frame, baseP + V(4.2f, 0.3f, z), baseP + V(0, cy, z), 0.16f);
        }
        Prim(PrimitiveType.Cube, "Booth", t, baseP + V(-6.5f, 0.9f, -1f), V(2f, 1.8f, 2f), coral);
    }

    // ---------- lights, flags, back crowd, particles ----------

    static void MakeFairyLights(Transform world)
    {
        var dress = Group("Dressing", world).transform;
        var wire = new MB(); var bulbs = new MB();
        var poleMat = Lit(P + "pole", C("#8a5f3a"), 0.15f);
        float[] pz = { -8.5f, -2.0f, 4.5f };
        foreach (float z in pz)
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Cylinder, "FairyPole", dress, V(s * 3.0f, 2.2f, z), V(0.11f, 2.2f, 0.11f), poleMat);
        for (int i = 0; i < pz.Length - 1; i++)
            foreach (float s in new[] { -1f, 1f })
                StringLine(wire, bulbs, V(s * 3.0f, 4.3f, pz[i]), V(s * 3.0f, 4.3f, pz[i + 1]), 0.5f, 0.6f);
        foreach (float z in pz) StringLine(wire, bulbs, V(-3.0f, 4.3f, z), V(3.0f, 4.3f, z), 0.7f, 0.55f);
        // tents and trucks to the poles
        StringLine(wire, bulbs, V(-3.0f, 4.3f, -2.0f), V(-10.8f, 3.3f, -6.2f), 1.2f, 0.7f, 0.07f);
        StringLine(wire, bulbs, V(3.0f, 4.3f, -2.0f), V(6.5f, 4.4f, -1.4f), 0.7f, 0.7f, 0.07f);
        StringLine(wire, bulbs, V(6.5f, 4.4f, -1.4f), V(11.8f, 4.4f, -1.4f), 0.8f, 0.7f, 0.07f);
        // the stage garland
        StringLine(wire, bulbs, V(-13.2f, 6.05f, 7.7f), V(-8.6f, 6.05f, 7.7f), 0.5f, 0.5f);
        StringLine(wire, bulbs, V(-8.6f, 6.05f, 7.7f), V(-4.0f, 6.05f, 7.7f), 0.5f, 0.5f);
        MeshObject("FairyWire", dress, wire.ToMesh("fe_fairy_wire"), DoubleSided(P + "wire", C("#4a3b4d"), 0.2f), false);
        MeshObject("FairyBulbs", dress, bulbs.ToMesh("fe_fairy_bulbs"), DoubleSided(P + "fairy", C("#fff0c8"), 0.3f, new Color(3.1f, 2.1f, 0.95f)), false);

        // bunting between the poles and across the crowd's side, six colours
        var cord = new MB();
        var flags = new MB[FlagHex.Length];
        for (int i = 0; i < flags.Length; i++) flags[i] = new MB();
        Bunting(cord, flags, V(-3.0f, 3.8f, -8.5f), V(3.0f, 3.8f, -8.5f), 0.5f);
        Bunting(cord, flags, V(-3.0f, 3.8f, 4.5f), V(3.0f, 3.8f, 4.5f), 0.5f);
        Bunting(cord, flags, V(-6.6f, 3.4f, -6.4f), V(-3.4f, 5.0f, -4f), 0.5f);
        Bunting(cord, flags, V(3.4f, 5.0f, -4f), V(4.2f, 3.2f, -7.2f), 0.4f);
        Bunting(cord, flags, V(-13.2f, 6.2f, 8f), V(-4.0f, 6.2f, 8f), 0.7f);
        Bunting(cord, flags, V(-4.0f, 6.2f, 8f), V(2.0f, 3.0f, 3.0f), 0.8f);
        Bunting(cord, flags, V(5f, 3.8f, 6.8f), V(12.0f, 3.8f, 2.6f), 0.7f);
        MeshObject("BuntingCord", dress, cord.ToMesh("fe_bunting_cord"), DoubleSided(P + "bunting_wire", C("#4a3b4d"), 0.2f), false);
        for (int i = 0; i < flags.Length; i++)
            MeshObject("Bunting" + i, dress, flags[i].ToMesh("fe_bunting_" + i), DoubleSided(P + "flag" + i, C(FlagHex[i]), 0.2f), false);
    }

    static void MakeBackCrowd(Transform world, Transform blocks)
    {
        // a back crowd of the same baked figures the live crowd uses, packed behind it in front of the mud; blocked off so nobody walks through it
        var assets = WorldTour.World.Gameplay.WorldGameplayAssets.Load();
        var rng = new System.Random(17);
        var root = Group("BackCrowd", world).transform;
        int n = 0;
        for (int row = 0; row < 2; row++)
            for (int i = 0; i < 11; i++)
            {
                float x = -12.8f + i * 0.8f + (row % 2) * 0.4f + (float)rng.NextDouble() * 0.15f;
                if (x > -4.6f) continue;
                var src = assets.crowdFigures[(n * 5 + rng.Next(assets.crowdFigures.Length)) % assets.crowdFigures.Length];
                n++;
                var g = (GameObject)PrefabUtility.InstantiatePrefab(src.gameObject, root);
                g.name = "BackFigure" + n;
                g.transform.position = V(x, 0f, 0.1f + row * 0.7f + (float)rng.NextDouble() * 0.15f);
                g.transform.rotation = Quaternion.Euler(0, (float)(rng.NextDouble() - 0.5) * 24f, 0);
                g.transform.localScale = Vector3.one * (0.85f * (0.92f + (float)rng.NextDouble() * 0.2f));
            }
        BlockLine(blocks, V(-12.9f, 0, 0.8f), V(-4.4f, 0, 0.8f), 0.8f);
    }

    static void MakeParticles()
    {
        var dot = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/World/Textures/soft_dot.png");
        // glitter and confetti: three drifting emitters over the crowd and the stage, plus golden motes everywhere
        var conf = ParticleMaterial("fe_confetti", null, false, Color.white);
        Drift("ConfettiPink", V(-8.6f, 7f, 7f), V(10f, 0.5f, 8f), conf, 70, 7f, new Vector2(5f, 8f), new Vector2(0.1f, 0.18f), C("#ff6f91"), C("#ffd23a"),
            new Vector3(-0.15f, -0.9f, -0.1f), new Vector3(0.15f, -0.5f, 0.1f), 0f, 0.5f, 2.5f);
        Drift("ConfettiTeal", V(-8.6f, 7f, 5f), V(10f, 0.5f, 8f), conf, 70, 7f, new Vector2(5f, 8f), new Vector2(0.1f, 0.18f), C("#2fc4b2"), C("#a8a9fc"),
            new Vector3(-0.15f, -0.9f, -0.1f), new Vector3(0.15f, -0.5f, 0.1f), 0f, 0.5f, 2.5f);
        Drift("ConfettiOrange", V(2f, 8f, 3f), V(26f, 0.5f, 22f), conf, 50, 4f, new Vector2(7f, 11f), new Vector2(0.09f, 0.15f), C("#ff9a3c"), C("#ffffff"),
            new Vector3(-0.1f, -0.7f, -0.1f), new Vector3(0.1f, -0.4f, 0.1f), 0f, 0.6f, 2f);
        var motes = ParticleMaterial("fe_motes", dot, true, new Color(1.7f, 1.3f, 0.7f, 0.6f));
        Drift("GoldMotes", V(0, 3f, 3f), V(30f, 5f, 28f), motes, 70, 8f, new Vector2(5f, 9f), new Vector2(0.1f, 0.22f), Color.white, Color.white,
            new Vector3(0.05f, -0.02f, -0.05f), new Vector3(0.2f, 0.1f, 0.05f), 0f, 0.6f);
    }
}
