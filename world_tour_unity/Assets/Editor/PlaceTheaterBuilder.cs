// Phase 8.2 builder B: Place_Theater, "the old movie palace, dusk". A short plaza with a red carpet runs up to the palace front
// (a lit marquee with bulbs, gold and velvet red); the whole front and the roof are cut away, so the hall shows: a foyer, two wings of
// velvet seats with a crowd in them, balcony boxes up both side walls, and a grand stage with curtains behind the crowd.
//
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -executeMethod PlaceTheaterBuilder.CreateAll
//
// Layout (x right, z away from the camera; the walkers are clamped to +-13.5):
//   plaza z -13.5 .. -7 (spawn on the carpet at z -11.5), palace front at z -7 (marquee canopy reaches z -9.7),
//   foyer z -7 .. -1, seat wings x +-(8.6 .. 13.2) z 1 .. 8.4, crowd box x +-4.6 z 4.6 .. 8.8, stage deck z 10.2 .. 15.2 (ramp from z 9.4),
//   hall walls at x +-14.5 and z 15.6.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using WorldTour.World;
using WorldTour.World.Places;
using static PlaceBKit;

public static class PlaceTheaterBuilder
{
    public const string ScenePath = "Assets/World/Scenes/Place_Theater.unity";
    const string P = "th_";

    static Material red, redDeep, gold, cream, plum, glow, glowGold, wood, glass;
    static Material cityMat, forestMat, furnMat;

    public static void CreateAll()
    {
        try
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(WorldArtBuilder.StarPrefab) == null) WorldArtBuilder.Run();
            Directory.CreateDirectory("Assets/World/Scenes");
            MakeScene();
            AssetDatabase.SaveAssets();
            Debug.Log("PlaceTheaterBuilder: done " + ScenePath);
        }
        catch (System.Exception e) { Debug.LogError("PlaceTheaterBuilder failed: " + e); }
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    // ---------- textures ----------

    static Texture2D CobbleTexture()
    {
        var rng = new System.Random(3);
        Color grout = C("#2b2433"), a = C("#6a5a78"), b = C("#8a6f86"), c = C("#58506c");
        return MakeTexture("th_cobble", 256, 256, (x, y) =>
        {
            int row = y / 16, off = (row % 2) * 16;
            int xx = (x + off) % 32, yy = y % 16;
            if (xx < 2 || yy < 2) return grout;
            int stone = ((x + off) / 32 * 7 + row * 13) % 5;
            Color col = stone == 0 ? b : stone == 1 ? c : a;
            float v = 0.92f + ((x * 73856093 ^ y * 19349663) & 255) / 255f * 0.12f;
            float hi = (xx < 4 || yy < 4) ? 1.12f : 1f;
            return col * v * hi;
        }, TextureWrapMode.Repeat, FilterMode.Bilinear, true);
    }

    static Texture2D CarpetTexture()
    {
        Color baseC = C("#8d1a3a"), gl = C("#e2b24a"), dk = C("#6a1030");
        return MakeTexture("th_carpet", 256, 256, (x, y) =>
        {
            float n = 0.94f + ((x * 73856093 ^ y * 19349663) & 255) / 255f * 0.1f;
            int d1 = (x + y) % 64, d2 = (x - y + 256) % 64;
            if (d1 < 3 || d2 < 3) return gl * n;
            int cx = x % 64, cy = y % 64;
            if (Mathf.Abs(cx - 32) + Mathf.Abs(cy - 32) < 9) return Color.Lerp(baseC, gl, 0.75f);
            if (d1 < 6 || d2 < 6) return dk * n;
            return baseC * n;
        }, TextureWrapMode.Repeat, FilterMode.Bilinear, true);
    }

    static Texture2D PlankTexture()
    {
        var rng = new System.Random(8);
        var shade = new float[16]; for (int i = 0; i < 16; i++) shade[i] = 0.85f + (float)rng.NextDouble() * 0.3f;
        Color w = C("#b9824f");
        return MakeTexture("th_planks", 128, 128, (x, y) =>
        {
            int row = y / 8;
            int xx = (x + row * 37) % 64;
            if (y % 8 == 0 || xx == 0) return w * 0.55f;
            float grain = 0.96f + Mathf.Sin(x * 0.4f + row) * 0.04f;
            return w * shade[row % 16] * grain;
        }, TextureWrapMode.Repeat, FilterMode.Bilinear, true);
    }

    // ---------- the scene ----------

    static void MakeScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Reported.Clear();

        red = Lit(P + "red", C("#a01d40"), 0.2f);
        redDeep = Lit(P + "red_deep", C("#5d0f2c"), 0.15f);
        gold = Lit(P + "gold", C("#e8b447"), 0.55f, new Color(0.5f, 0.32f, 0.07f));
        cream = Lit(P + "cream", C("#f4e6c8"), 0.15f);
        plum = Lit(P + "plum", C("#3a1d4a"), 0.1f);
        glow = Lit(P + "bulb", C("#fff0c8"), 0.3f, new Color(3.4f, 2.4f, 1.1f));
        glowGold = Lit(P + "glow_gold", C("#ffd36a"), 0.3f, new Color(2.6f, 1.7f, 0.5f));
        wood = LitTex(P + "wood", Color.white, PlankTexture(), 0.3f);
        glass = Lit(P + "window", C("#ffcf8a"), 0.4f, new Color(1.5f, 0.9f, 0.4f));
        cityMat = LitTex(P + "city_kit", new Color(0.58f, 0.5f, 0.82f), WorldArtBuilder.ArtTexture(City + "citybits_texture.png"), 0.1f);   // dusk-tinted: violet, so the orange and green blocks sit back
        forestMat = Kit(P + "forest_kit", Forest + "forest_texture.png", 0.08f);
        furnMat = Kit(P + "furn_kit", Furn + "furniturebits_texture.png");

        var profile = MakeProfile("TheaterVolumeProfile", new Grade
        {
            BloomThreshold = 0.9f, BloomIntensity = 0.7f, BloomScatter = 0.7f, BloomTint = new Color(1f, 0.85f, 0.78f),
            Exposure = -0.1f, Contrast = 20f, Saturation = 22f, Temperature = 10f, Tint = 6f, Vignette = 0.3f,
            Filter = new Color(1f, 0.96f, 0.98f),
            Shadows = new Vector4(0.8f, 0.8f, 1.15f, 0f), Highlights = new Vector4(1.12f, 0.98f, 0.84f, 0f),
        });
        var world = Group("World").transform;
        var ground = Group("Ground", world).transform;
        var plaza = Group("Plaza", world).transform;
        var front = Group("PalaceFront", world).transform;
        var hall = Group("Hall", world).transform;
        var stage = Group("Stage", world).transform;
        var blds = Group("Skyline", world).transform;
        var props = Group("Props", world).transform;
        var blocks = Group("Blockers", world).transform;

        MakeGround(ground);
        MakePlaza(plaza, props, blocks);
        MakeFront(front, blocks);
        MakeHall(hall, blocks);
        MakeStage(stage, blocks);
        MakeSkyline(blds);
        MakeLights(world);

        var spec = new Spec
        {
            Id = "theater", ScenePath = ScenePath,
            Spawn = V(0, 0, -11.5f), Stage = V(0, 0, 11.8f), Crowd = V(0, 0, 6.7f), CrowdSize = new Vector2(9.2f, 4.2f), Exit = V(11.4f, 0, -11.2f), Riff = V(-1.8f, 0, -12.6f),
            Crew = new[] { V(-4.2f, 0, -8.6f), V(7.0f, 0, -3.2f), V(-6.6f, 0, 2.0f) },
            Bg = new Color(0.35f, 0.25f, 0.5f),
            ExitRingBase = C("#e8b447"), ExitRing = new Color(2.2f, 1.5f, 0.5f), ExitHole = C("#8d1a3a"), ExitSign = C("#a01d40"), ExitYaw = 0f,
        };
        Contract(spec, blocks, profile, P);
        MakeAtmosphereAndSun();
        MakeParticles();
        StaticBatch(ground);
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    static void MakeAtmosphereAndSun()
    {
        MakeAtmosphere(new Air
        {
            SkyName = "th_sky", Top = C("#1f1b5c"), Mid = C("#5b3a8e"), Horizon = C("#ff9a68"), Bottom = C("#9d5675"),
            SunEuler = new Vector3(22f, 12f, 0f),
            AmbientSky = C("#6a64c0"), AmbientEquator = C("#d68a96"), AmbientGround = C("#3e2d4e"), Fog = C("#b0698a"),
            AmbientIntensity = 0.82f, FogDensity = 0.0095f, Shade = C("#5a4f86"),
        });
        // low sun in the south, behind the camera: it pours in through the open front and gilds the stage
        MakeSun(new Color(1f, 0.55f, 0.32f), 1.75f, new Vector3(22f, 12f, 0f), 0.75f);
    }

    // ---------- ground ----------

    static void MakeGround(Transform t)
    {
        var dark = Lit(P + "ground_dark", C("#2f2640"), 0.05f);
        Prim(PrimitiveType.Plane, "Base", t, V(0, -0.04f, 0), V(24, 1, 24), dark, null, false);
        var cob = LitTex(P + "cobble_mat", Color.white, CobbleTexture(), 0.12f);
        cob.SetTextureScale("_BaseMap", new Vector2(7f, 2.5f));
        Prim(PrimitiveType.Plane, "Cobbles", t, V(0, 0, -11f), V(3.2f, 1, 0.9f), cob, null, false);
        var carpet = LitTex(P + "carpet_mat", Color.white, CarpetTexture(), 0.1f);
        carpet.SetTextureScale("_BaseMap", new Vector2(9.5f, 7.6f));
        Prim(PrimitiveType.Plane, "HallCarpet", t, V(0, 0.025f, 4.3f), V(2.9f, 1, 2.26f), carpet, null, false);
        // the red runner out front
        var runner = Lit(P + "runner", C("#b8183f"), 0.2f);
        Prim(PrimitiveType.Cube, "Runner", t, V(0, 0.025f, -10.4f), V(3.4f, 0.03f, 7.4f), runner, null, false);
        var edge = Lit(P + "runner_edge", C("#e8b447"), 0.4f, new Color(0.35f, 0.22f, 0.05f));
        foreach (float x in new[] { -1.72f, 1.72f }) Prim(PrimitiveType.Cube, "RunnerEdge", t, V(x, 0.03f, -10.4f), V(0.07f, 0.03f, 7.4f), edge, null, false);
    }

    // ---------- the plaza ----------

    static void MakePlaza(Transform t, Transform props, Transform blocks)
    {
        var lampMat = Lit(P + "lamp_head", C("#fff3c4"), 0.3f, new Color(3.2f, 2.3f, 1.0f));
        var pole = Lit(P + "lamp_pole", C("#2a2438"), 0.5f);
        // lamps either side of the carpet, string lights between them and the palace
        float[] lx = { -6.4f, 6.4f, -12.2f, 12.2f };
        float[] lz = { -9.6f, -9.6f, -12.4f, -12.4f };
        for (int i = 0; i < lx.Length; i++)
        {
            Prim(PrimitiveType.Cylinder, "LampPole", t, V(lx[i], 2.0f, lz[i]), V(0.12f, 2.0f, 0.12f), pole);
            Prim(PrimitiveType.Cylinder, "LampBase", t, V(lx[i], 0.25f, lz[i]), V(0.3f, 0.25f, 0.3f), pole);
            Prim(PrimitiveType.Cube, "LampArm", t, V(lx[i] * 0.985f, 4.05f, lz[i]), V(0.5f, 0.07f, 0.07f), pole);
            Prim(PrimitiveType.Sphere, "LampGlobe", t, V(lx[i], 4.15f, lz[i]), V(0.5f, 0.5f, 0.5f), lampMat, null, false);
            Prim(PrimitiveType.Cylinder, "LampCap", t, V(lx[i], 4.45f, lz[i]), V(0.4f, 0.04f, 0.4f), pole);
            Block(blocks, lx[i], lz[i], 0.35f);
        }
        var wire = new MB(); var bulbs = new MB();
        StringLine(wire, bulbs, V(-6.4f, 4.1f, -9.6f), V(-2.2f, 4.5f, -7.5f), 0.7f, 0.6f, 0.07f);
        StringLine(wire, bulbs, V(6.4f, 4.1f, -9.6f), V(2.2f, 4.5f, -7.5f), 0.7f, 0.6f, 0.07f);
        StringLine(wire, bulbs, V(-6.4f, 4.1f, -9.6f), V(6.4f, 4.1f, -9.6f), 1.0f, 0.6f, 0.07f);
        StringLine(wire, bulbs, V(-12.2f, 4.1f, -12.4f), V(-6.4f, 4.1f, -9.6f), 0.8f, 0.6f, 0.07f);
        StringLine(wire, bulbs, V(12.2f, 4.1f, -12.4f), V(6.4f, 4.1f, -9.6f), 0.8f, 0.6f, 0.07f);
        MeshObject("StringWire", t, wire.ToMesh("th_strings_wire"), DoubleSided(P + "wire", C("#2a2230"), 0.2f), false);
        MeshObject("StringBulbs", t, bulbs.ToMesh("th_strings_bulbs"), DoubleSided(P + "bulb2", C("#fff0c8"), 0.3f, new Color(3.1f, 2.1f, 0.95f)), false);

        // warm pools of light on the ground under every lamp and spilling out from the marquee
        var dotTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/World/Textures/soft_dot.png");
        var pool = ParticleMaterial("th_pool", dotTex, true, new Color(1.5f, 0.9f, 0.45f, 0.5f));
        for (int i = 0; i < lx.Length; i++)
            Prim(PrimitiveType.Quad, "LightPool", t, V(lx[i], 0.05f, lz[i]), V(6.5f, 6.5f, 1f), pool, V(90, 0, 0), false);
        var spill = ParticleMaterial("th_spill", dotTex, true, new Color(1.6f, 0.95f, 0.5f, 0.55f));
        Prim(PrimitiveType.Quad, "MarqueeSpill", t, V(0, 0.06f, -10.2f), V(15f, 8f, 1f), spill, V(90, 0, 0), false);

        // velvet-rope stanchions along the carpet
        var rope = new MB(); var posts = new MB();
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 prev = Vector3.zero; bool have = false;
            for (int i = 0; i < 5; i++)
            {
                if (i == 1) { have = false; continue; }   // round 8.4: no post here, so there is a 3 m way through the ropes beside the spawn toward the exit
                float z = -13.2f + i * 1.45f;
                float x = side * 2.2f;
                Prim(PrimitiveType.Cylinder, "Post", t, V(x, 0.45f, z), V(0.09f, 0.45f, 0.09f), gold);
                Prim(PrimitiveType.Sphere, "PostTop", t, V(x, 0.95f, z), V(0.2f, 0.2f, 0.2f), gold);
                Prim(PrimitiveType.Cylinder, "PostBase", t, V(x, 0.03f, z), V(0.32f, 0.03f, 0.32f), gold);
                Block(blocks, x, z, 0.18f);
                var p = V(x, 0.8f, z);
                if (have) StringLine(rope, rope, prev, p, 0.16f, 99f, 0.01f);
                prev = p; have = true;
            }
        }
        MeshObject("Ropes", t, rope.ToMesh("th_ropes"), DoubleSided(P + "rope", C("#7d1230"), 0.3f), true);

        // the ticket booth (west), a popcorn cart (east)
        MakeBooth(t, blocks, V(-8.6f, 0, -10.2f));
        MakePopcornCart(t, blocks, V(8.4f, 0, -8.4f));

        // blank "coming soon" poster stands: no words, just a star and colour
        MakePoster(t, blocks, V(-3.9f, 0, -8.0f), C("#f98bb0"), -12f);
        MakePoster(t, blocks, V(3.9f, 0, -8.0f), C("#a8a9fc"), 12f);

        // urns with clipped topiary by the doors, a few trees in tubs at the edges
        foreach (float x in new[] { -6.2f, 6.2f })
        {
            Prim(PrimitiveType.Cylinder, "Urn", t, V(x, 0.45f, -7.9f), V(0.7f, 0.45f, 0.7f), cream);
            Prim(PrimitiveType.Cylinder, "UrnRim", t, V(x, 0.92f, -7.9f), V(0.85f, 0.05f, 0.85f), gold);
            Prim(PrimitiveType.Sphere, "Topiary", t, V(x, 1.65f, -7.9f), V(1.0f, 1.0f, 1.0f), Lit(P + "topiary", C("#2f7a4a"), 0.1f));
            Block(blocks, x, -7.9f, 0.55f);
        }
        float[] tx = { -13.0f, 13.0f, -13.0f, 13.0f };
        float[] tz = { -8.6f, -8.2f, -13.0f, -13.2f };
        for (int i = 0; i < 4; i++)
        {
            Fbx(Forest + (i % 2 == 0 ? "Tree_3_A_Color1.fbx" : "Tree_1_A_Color1.fbx"), t, V(tx[i], 0, tz[i]), i * 61f, 1.1f, forestMat, "TubTree");
            Block(blocks, tx[i], tz[i], 0.5f);
        }
        // a parked sedan near the way out
        Fbx(City + "car_sedan.fbx", t, V(8.6f, 0, -12.6f), 90f, CityScale, cityMat, "Sedan");
        Block(blocks, 7.4f, -12.6f, 0.9f); Block(blocks, 8.6f, -12.6f, 0.9f); Block(blocks, 9.8f, -12.6f, 0.9f);
    }

    static void MakeBooth(Transform t, Transform blocks, Vector3 p)
    {
        var g = Group("TicketBooth", t).transform; g.position = p;
        Prim(PrimitiveType.Cube, "Body", g, V(0, 1.1f, 0), V(2.4f, 2.2f, 1.6f), cream);
        Prim(PrimitiveType.Cube, "Trim", g, V(0, 0.35f, -0.82f), V(2.5f, 0.7f, 0.06f), red);
        Prim(PrimitiveType.Cube, "Window", g, V(0, 1.45f, -0.82f), V(1.3f, 0.8f, 0.05f), glass);
        Prim(PrimitiveType.Cube, "Sill", g, V(0, 1.0f, -0.95f), V(1.6f, 0.08f, 0.3f), gold);
        Prim(PrimitiveType.Cube, "Roof", g, V(0, 2.3f, 0), V(2.8f, 0.18f, 2.0f), red);
        Prim(PrimitiveType.Cube, "RoofGold", g, V(0, 2.42f, -1.0f), V(2.8f, 0.07f, 0.07f), gold);
        for (int i = 0; i < 6; i++) Prim(PrimitiveType.Sphere, "Bulb", g, V(-1.1f + i * 0.44f, 2.12f, -1.02f), V(0.12f, 0.12f, 0.12f), glow, null, false);
        BlockLine(blocks, p + V(-1.1f, 0, 0), p + V(1.1f, 0, 0), 0.8f);
    }

    static void MakePopcornCart(Transform t, Transform blocks, Vector3 p)
    {
        var g = Group("PopcornCart", t).transform; g.position = p;
        var stripeA = Lit(P + "cart_red", C("#d8283f"), 0.25f);
        Prim(PrimitiveType.Cube, "Base", g, V(0, 0.55f, 0), V(1.6f, 0.9f, 1.0f), cream);
        Prim(PrimitiveType.Cube, "BaseStripe", g, V(0, 0.2f, -0.52f), V(1.62f, 0.3f, 0.04f), stripeA);
        Prim(PrimitiveType.Cube, "Glass", g, V(0, 1.4f, 0), V(1.5f, 0.9f, 0.9f), Lit(P + "cart_glass", C("#fff4d8"), 0.5f, new Color(1.1f, 0.85f, 0.45f)));
        for (int i = 0; i < 6; i++)
            Prim(PrimitiveType.Cube, "Awning", g, V(-0.75f + i * 0.3f, 2.25f, -0.1f), V(0.3f, 0.06f, 1.5f), i % 2 == 0 ? stripeA : cream, V(-14f, 0, 0));
        Prim(PrimitiveType.Cylinder, "PostL", g, V(-0.78f, 1.05f, -0.45f), V(0.06f, 1.05f, 0.06f), gold);
        Prim(PrimitiveType.Cylinder, "PostR", g, V(0.78f, 1.05f, -0.45f), V(0.06f, 1.05f, 0.06f), gold);
        foreach (float x in new[] { -0.6f, 0.6f }) Prim(PrimitiveType.Cylinder, "Wheel", g, V(x, 0.28f, 0.0f), V(0.55f, 0.04f, 0.55f), Lit(P + "wheel", C("#2a2438"), 0.3f), V(0, 0, 90f));
        // popcorn: a heap of warm gems
        var pop = new MB();
        var rng = new System.Random(4);
        for (int i = 0; i < 40; i++)
            pop.Gem(V((float)(rng.NextDouble() - 0.5) * 1.2f, 1.9f + (float)rng.NextDouble() * 0.05f, (float)(rng.NextDouble() - 0.5) * 0.7f), 0.09f, 0.08f, 0.09f);
        var pm = MeshObject("Popcorn", g, pop.ToMesh("th_popcorn"), Lit(P + "popcorn", C("#fff1b8"), 0.2f, new Color(0.35f, 0.3f, 0.12f)), false);
        pm.transform.localPosition = V(0, -0.5f, 0);
        BlockLine(blocks, p + V(-0.7f, 0, 0), p + V(0.7f, 0, 0), 0.7f);
    }

    static void MakePoster(Transform t, Transform blocks, Vector3 p, Color c, float yaw)
    {
        var g = Group("PosterStand", t).transform; g.position = p; g.rotation = Quaternion.Euler(0, yaw, 0);
        Prim(PrimitiveType.Cube, "Frame", g, V(0, 1.55f, 0), V(1.5f, 2.2f, 0.12f), gold);
        var panel = Prim(PrimitiveType.Cube, "Poster", g, V(0, 1.55f, -0.07f), V(1.3f, 2.0f, 0.04f), Lit(P + "poster" + ColorUtility.ToHtmlStringRGB(c), c, 0.2f, c * 0.35f));
        var star = new GameObject("PosterStar");
        star.transform.SetParent(g, false);
        star.transform.localPosition = V(0, 1.7f, -0.1f);
        star.transform.localRotation = Quaternion.Euler(0, 180f, 0);
        star.transform.localScale = V(0.9f, 0.9f, 0.05f);
        star.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(WorldArtBuilder.MeshDir + "/MarkerStar.asset");
        star.AddComponent<MeshRenderer>().sharedMaterial = glowGold;
        Prim(PrimitiveType.Cube, "Caption", g, V(0, 0.95f, -0.1f), V(0.9f, 0.1f, 0.03f), cream, null, false);
        Prim(PrimitiveType.Cube, "Caption2", g, V(-0.1f, 0.78f, -0.1f), V(0.6f, 0.07f, 0.03f), cream, null, false);
        Prim(PrimitiveType.Cylinder, "LegL", g, V(-0.55f, 0.25f, 0.1f), V(0.06f, 0.5f, 0.06f), gold);
        Prim(PrimitiveType.Cylinder, "LegR", g, V(0.55f, 0.25f, 0.1f), V(0.06f, 0.5f, 0.06f), gold);
        Block(blocks, p.x, p.z, 0.7f);
    }

    // ---------- the palace front (cut away in the middle, marquee over the doors) ----------

    static void MakeFront(Transform t, Transform blocks)
    {
        const float z = -7f;
        // two piers, red velvet wall with gold stripes, lit windows and blank posters
        foreach (float s in new[] { -1f, 1f })
        {
            float cx = s * 10.0f;
            var pier = Prim(PrimitiveType.Cube, "Pier", t, V(cx, 4.7f, z), V(9.0f, 9.4f, 1.2f), red, null, false);
            Prim(PrimitiveType.Cube, "PierBase", t, V(cx, 0.45f, z - 0.05f), V(9.1f, 0.9f, 1.3f), plum, null, false);
            Prim(PrimitiveType.Cube, "Cornice", t, V(cx, 9.3f, z - 0.3f), V(9.4f, 0.5f, 1.8f), gold, null, false);
            Prim(PrimitiveType.Cube, "CorniceUnder", t, V(cx, 8.95f, z - 0.2f), V(9.2f, 0.25f, 1.5f), redDeep, null, false);
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Cube, "Stripe", t, V(cx + (i - 1.5f) * 2.3f, 4.7f, z - 0.62f), V(0.16f, 7.6f, 0.06f), gold, null, false);
            foreach (float dx in new[] { -2.4f, 2.4f })
            {
                float wx = cx + dx;
                Prim(PrimitiveType.Cube, "WindowFrame", t, V(wx, 4.0f, z - 0.64f), V(1.5f, 3.2f, 0.07f), gold, null, false);
                Prim(PrimitiveType.Cube, "Window", t, V(wx, 3.9f, z - 0.7f), V(1.2f, 2.9f, 0.05f), glass, null, false);
                Prim(PrimitiveType.Cube, "WindowBar", t, V(wx, 3.9f, z - 0.74f), V(0.07f, 2.9f, 0.04f), gold, null, false);
                Prim(PrimitiveType.Cube, "WindowBarH", t, V(wx, 4.5f, z - 0.74f), V(1.2f, 0.07f, 0.04f), gold, null, false);
                var arch = Prim(PrimitiveType.Cylinder, "WindowArch", t, V(wx, 5.55f, z - 0.64f), V(1.5f, 0.035f, 1.5f), gold, V(90, 0, 0), false);
                Prim(PrimitiveType.Cylinder, "WindowArchGlass", t, V(wx, 5.55f, z - 0.7f), V(1.2f, 0.03f, 1.2f), glass, V(90, 0, 0), false);
                Prim(PrimitiveType.Cube, "WindowCover", t, V(wx, 5.2f, z - 0.66f), V(1.5f, 0.7f, 0.05f), red, null, false);
            }
            // blank poster boards up high, lit from above
            foreach (float dx in new[] { -2.4f, 2.4f })
            {
                float wx = cx + dx;
                Prim(PrimitiveType.Cube, "PosterFrame", t, V(wx, 7.3f, z - 0.64f), V(2.0f, 1.8f, 0.08f), gold, null, false);
                var col = (s * dx > 0) ? C("#f98bb0") : C("#a8a9fc");
                Prim(PrimitiveType.Cube, "PosterBoard", t, V(wx, 7.3f, z - 0.7f), V(1.75f, 1.55f, 0.04f), Lit(P + "board" + ColorUtility.ToHtmlStringRGB(col), col, 0.2f, col * 0.35f), null, false);
                var star = new GameObject("PosterStar");
                star.transform.SetParent(t, false);
                star.transform.localPosition = V(wx, 7.3f, z - 0.76f);
                star.transform.localRotation = Quaternion.Euler(0, 180f, 0);
                star.transform.localScale = V(1.1f, 1.1f, 0.05f);
                star.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(WorldArtBuilder.MeshDir + "/MarkerStar.asset");
                var sr = star.AddComponent<MeshRenderer>(); sr.sharedMaterial = glowGold; sr.shadowCastingMode = ShadowCastingMode.Off;
            }
            BlockLine(blocks, V(s * 5.7f, 0, z), V(s * 13.9f, 0, z), 0.55f);
        }
        // the header over the doors and the marquee
        Prim(PrimitiveType.Cube, "Header", t, V(0, 8.1f, z), V(11.0f, 2.6f, 1.2f), red, null, false);
        Prim(PrimitiveType.Cube, "HeaderBand", t, V(0, 7.0f, z - 0.62f), V(11.0f, 0.18f, 0.08f), gold, null, false);
        Prim(PrimitiveType.Cube, "Cornice", t, V(0, 9.3f, z - 0.3f), V(11.4f, 0.5f, 1.8f), gold, null, false);
        // a gold crest above the header
        var starMesh = AssetDatabase.LoadAssetAtPath<Mesh>(WorldArtBuilder.MeshDir + "/MarkerStar.asset");
        var crest = new GameObject("Crest");
        crest.transform.SetParent(t, false);
        crest.transform.localPosition = V(0, 11.3f, z - 0.2f);
        crest.transform.localRotation = Quaternion.Euler(0, 180f, 0);
        crest.transform.localScale = V(3.4f, 3.4f, 0.4f);
        crest.AddComponent<MeshFilter>().sharedMesh = starMesh;
        var cr = crest.AddComponent<MeshRenderer>(); cr.sharedMaterial = glowGold; cr.shadowCastingMode = ShadowCastingMode.Off;
        crest.AddComponent<PlaceMotion>().kind = PlaceMotion.Kind.Pulse;
        var cm = crest.GetComponent<PlaceMotion>(); cm.speed = 2.2f; cm.amplitude = 5f;
        Prim(PrimitiveType.Cube, "CrestPlinth", t, V(0, 9.9f, z - 0.2f), V(2.2f, 0.8f, 0.8f), gold, null, false);

        // marquee: a lit board with a big star and no words ("tonight: you!" is for the 8.4 pass), a bulb frame, and a canopy with bulbs
        Prim(PrimitiveType.Cube, "MarqueeFrame", t, V(0, 5.9f, z - 0.78f), V(9.8f, 2.2f, 0.28f), gold, null, false);
        Prim(PrimitiveType.Cube, "MarqueeBoard", t, V(0, 5.9f, z - 0.95f), V(9.2f, 1.75f, 0.06f), Lit(P + "marquee_board", C("#fff2d4"), 0.3f, new Color(1.6f, 1.25f, 0.8f)), null, false);
        var ms = new GameObject("MarqueeStar");
        ms.transform.SetParent(t, false);
        ms.transform.localPosition = V(0, 5.9f, z - 1.03f);
        ms.transform.localRotation = Quaternion.Euler(0, 180f, 0);
        ms.transform.localScale = V(1.7f, 1.7f, 0.06f);
        ms.AddComponent<MeshFilter>().sharedMesh = starMesh;
        var msr = ms.AddComponent<MeshRenderer>(); msr.sharedMaterial = Lit(P + "marquee_star", C("#f98bb0"), 0.3f, new Color(2.0f, 0.6f, 1.0f)); msr.shadowCastingMode = ShadowCastingMode.Off;
        // the show title, in the dark red of the walls, either side of the star: "moonlight revue", tonight, one night only (a made-up show)
        var titleMat = Lit(P + "marquee_title", C("#7d1230"), 0.3f);
        PlaceKit.PixelText(t, "MOONLIGHT", V(-2.75f, 6.2f, z - 1.0f), 0.062f, titleMat, "th_title_a", 0.5f);
        PlaceKit.PixelText(t, "REVUE", V(2.75f, 6.2f, z - 1.0f), 0.098f, titleMat, "th_title_b", 0.5f);
        PlaceKit.PixelText(t, "TONIGHT", V(-2.75f, 5.55f, z - 1.0f), 0.046f, titleMat, "th_title_c", 0.5f);
        PlaceKit.PixelText(t, "ONE NIGHT ONLY", V(2.75f, 5.45f, z - 1.0f), 0.042f, titleMat, "th_title_d", 0.5f);
        // canopy
        Prim(PrimitiveType.Cube, "Canopy", t, V(0, 4.55f, z - 1.55f), V(10.4f, 0.28f, 3.1f), red, null, false);
        Prim(PrimitiveType.Cube, "CanopyTrim", t, V(0, 4.38f, z - 3.1f), V(10.5f, 0.3f, 0.12f), gold, null, false);
        Prim(PrimitiveType.Cube, "CanopyTop", t, V(0, 4.72f, z - 1.55f), V(10.6f, 0.08f, 3.2f), gold, null, false);
        // bulbs: the board's frame and the canopy lip (two meshes, merged)
        var bulbs = new MB();
        for (float x = -4.75f; x <= 4.76f; x += 0.5f) { bulbs.Gem(V(x, 6.95f, z - 0.96f), 0.11f, 0.15f, 0.11f); bulbs.Gem(V(x, 4.85f, z - 0.96f), 0.11f, 0.15f, 0.11f); }
        for (float y = 5.2f; y <= 6.7f; y += 0.4f) { bulbs.Gem(V(-4.75f, y, z - 0.96f), 0.11f, 0.15f, 0.11f); bulbs.Gem(V(4.75f, y, z - 0.96f), 0.11f, 0.15f, 0.11f); }
        for (float x = -5.1f; x <= 5.11f; x += 0.55f) bulbs.Gem(V(x, 4.25f, z - 3.18f), 0.12f, 0.17f, 0.12f);
        for (float zz = -0.4f; zz > -3.1f; zz -= 0.55f) { bulbs.Gem(V(-5.2f, 4.25f, z + zz), 0.12f, 0.17f, 0.12f); bulbs.Gem(V(5.2f, 4.25f, z + zz), 0.12f, 0.17f, 0.12f); }
        MeshObject("MarqueeBulbs", t, bulbs.ToMesh("th_marquee_bulbs"), Lit(P + "marquee_bulb", C("#fff0c8"), 0.3f, new Color(4f, 3.0f, 1.4f)), false);
        // columns holding the canopy up at the corners of the walk
        foreach (float s in new[] { -1f, 1f })
        {
            Prim(PrimitiveType.Cylinder, "Column", t, V(s * 6.0f, 2.25f, z - 2.9f), V(0.5f, 2.25f, 0.5f), cream, null, true);
            Prim(PrimitiveType.Cylinder, "ColumnBase", t, V(s * 6.0f, 0.15f, z - 2.9f), V(0.8f, 0.15f, 0.8f), gold);
            Prim(PrimitiveType.Cylinder, "ColumnCap", t, V(s * 6.0f, 4.25f, z - 2.9f), V(0.8f, 0.1f, 0.8f), gold);
            Block(blocks, s * 6.0f, z - 2.9f, 0.55f);
        }
        // the front shadow off: the palace is a cut-away, the low sun reaches the whole hall
        foreach (var r in t.GetComponentsInChildren<Renderer>(true)) if (r.name.StartsWith("Pier") || r.name == "Header" || r.name == "Cornice" || r.name == "CorniceUnder" || r.name == "Crest" || r.name == "CrestPlinth") r.shadowCastingMode = ShadowCastingMode.Off;
    }

    // ---------- the hall ----------

    static void MakeHall(Transform t, Transform blocks)
    {
        // walls: velvet panels with gold pilasters
        Prim(PrimitiveType.Cube, "WallW", t, V(-14.7f, 5.5f, 4.3f), V(0.4f, 11f, 22.6f), redDeep, null, false);
        Prim(PrimitiveType.Cube, "WallE", t, V(14.7f, 5.5f, 4.3f), V(0.4f, 11f, 22.6f), redDeep, null, false);
        Prim(PrimitiveType.Cube, "WainscotW", t, V(-14.4f, 1.1f, 4.3f), V(0.25f, 2.2f, 22.4f), plum, null, false);
        Prim(PrimitiveType.Cube, "WainscotE", t, V(14.4f, 1.1f, 4.3f), V(0.25f, 2.2f, 22.4f), plum, null, false);
        Prim(PrimitiveType.Cube, "RailW", t, V(-14.25f, 2.25f, 4.3f), V(0.2f, 0.12f, 22.4f), gold, null, false);
        Prim(PrimitiveType.Cube, "RailE", t, V(14.25f, 2.25f, 4.3f), V(0.2f, 0.12f, 22.4f), gold, null, false);
        var sconceMat = Lit(P + "sconce", C("#fff0c8"), 0.3f, new Color(3.2f, 2.2f, 0.9f));
        for (int i = 0; i < 8; i++)
        {
            float z = -5.5f + i * 3.0f;
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "Pilaster", t, V(s * 14.3f, 5.6f, z), V(0.35f, 10.8f, 0.8f), gold, null, false);
                Prim(PrimitiveType.Cube, "PilasterCap", t, V(s * 14.2f, 10.6f, z), V(0.5f, 0.3f, 1.0f), cream, null, false);
                // a wall panel between pilasters, darker, with a lit sconce
                Prim(PrimitiveType.Cube, "Panel", t, V(s * 14.4f, 6.2f, z + 1.5f), V(0.1f, 6.8f, 1.9f), red, null, false);
                Prim(PrimitiveType.Sphere, "Sconce", t, V(s * 14.0f, 3.4f, z + 1.5f), V(0.32f, 0.5f, 0.32f), sconceMat, null, false);
            }
        }
        // back wall and fly tower behind the stage
        Prim(PrimitiveType.Cube, "BackWall", t, V(0, 7.5f, 15.9f), V(30f, 15f, 0.5f), plum, null, false);

        // seat wings: velvet seats in rows, some with a person in them
        var seats = new MB(); var backs = new MB();
        var spots = new List<Vector3>(); var yaws = new List<float>();
        var rng = new System.Random(21);
        foreach (float s in new[] { -1f, 1f })
        {
            for (int row = 0; row < 7; row++)
            {
                float z = 1.0f + row * 1.1f;
                for (int c = 0; c < 6; c++)
                {
                    float x = s * (8.9f + c * 0.78f);
                    seats.Box(V(x, 0.28f, z), V(0.68f, 0.42f, 0.62f));
                    backs.Box(V(x, 0.78f, z - 0.3f), V(0.68f, 0.9f, 0.14f));
                    if (rng.NextDouble() < 0.72) { spots.Add(V(x, 0.2f, z + 0.02f)); yaws.Add(0f); }
                }
            }
            for (int blk = 0; blk < 4; blk++) BlockLine(blocks, V(s * 8.9f, 0, 1.4f + blk * 2.2f), V(s * 13.3f, 0, 1.4f + blk * 2.2f), 0.6f);
        }
        MeshObject("Seats", t, seats.ToMesh("th_seats"), Lit(P + "seat_red", C("#b3243f"), 0.35f), true);
        MeshObject("SeatBacks", t, backs.ToMesh("th_seat_backs"), Lit(P + "seat_back", C("#8d1a3a"), 0.35f), true);
        StaticCrowd(t, "TH_Seated_", spots, yaws, new[] { "#f98bb0", "#a8a9fc", "#ffd23a", "#7fd0b8", "#f4e6c8", "#ff8a5b", "#4a62d8" }, new[] { "#f2c6a0", "#d89c74", "#a8704c", "#6e4630" }, 5, 0.85f, 0.45f);

        // balcony boxes up both side walls, two tiers, with a few standing people
        var bSpots = new List<Vector3>(); var bYaws = new List<float>();
        for (int tier = 0; tier < 2; tier++)
        {
            float y = tier == 0 ? 4.4f : 7.5f;
            foreach (float s in new[] { -1f, 1f })
                for (int b = 0; b < 5; b++)
                {
                    float z = -3.5f + b * 3.9f;
                    Prim(PrimitiveType.Cube, "BalconyFloor", t, V(s * 13.45f, y, z), V(2.3f, 0.32f, 3.5f), redDeep, null, true);
                    Prim(PrimitiveType.Cube, "BalconyFront", t, V(s * 12.35f, y + 0.55f, z), V(0.22f, 1.0f, 3.5f), red);
                    Prim(PrimitiveType.Cube, "BalconyCap", t, V(s * 12.3f, y + 1.1f, z), V(0.32f, 0.14f, 3.6f), gold);
                    Prim(PrimitiveType.Cube, "BalconyUnder", t, V(s * 12.4f, y - 0.25f, z), V(0.3f, 0.22f, 3.6f), gold, null, false);
                    for (int g = 0; g < 3; g++)
                    {
                        bSpots.Add(V(s * 13.1f, y + 0.18f, z - 1.0f + g * 1.0f));
                        bYaws.Add(s > 0 ? -90f : 90f);
                    }
                }
        }
        StaticCrowd(t, "TH_Balcony_", bSpots, bYaws, new[] { "#f98bb0", "#a8a9fc", "#ffd23a", "#7fd0b8", "#f4e6c8" }, new[] { "#f2c6a0", "#d89c74", "#a8704c", "#6e4630" }, 9, 0.85f, 0f);

        // foyer columns, a chandelier or two
        foreach (float s in new[] { -1f, 1f })
            foreach (float z in new[] { -5.2f, -2.2f })
            {
                Prim(PrimitiveType.Cylinder, "Column", t, V(s * 6.8f, 2.6f, z), V(0.55f, 2.6f, 0.55f), cream);
                Prim(PrimitiveType.Cylinder, "ColumnBase", t, V(s * 6.8f, 0.18f, z), V(0.9f, 0.18f, 0.9f), gold);
                Prim(PrimitiveType.Cylinder, "ColumnCap", t, V(s * 6.8f, 5.15f, z), V(0.9f, 0.12f, 0.9f), gold);
                Block(blocks, s * 6.8f, z, 0.55f);
            }
        Chandelier(t, V(0, 8.4f, -2.6f));
        Chandelier(t, V(0, 8.8f, 3.0f));
    }

    static void Chandelier(Transform t, Vector3 p)
    {
        var g = Group("Chandelier", t).transform; g.position = p;
        Prim(PrimitiveType.Cylinder, "Chain", g, V(0, 1.6f, 0), V(0.04f, 1.6f, 0.04f), gold, null, false);
        Prim(PrimitiveType.Cylinder, "Ring", g, V(0, 0, 0), V(1.9f, 0.04f, 1.9f), gold, null, false);
        Prim(PrimitiveType.Cylinder, "RingInner", g, V(0, -0.35f, 0), V(1.1f, 0.03f, 1.1f), gold, null, false);
        var bulbs = new MB();
        for (int i = 0; i < 12; i++)
        {
            float a = i / 12f * Mathf.PI * 2f;
            bulbs.Gem(V(Mathf.Cos(a) * 0.93f, 0.2f, Mathf.Sin(a) * 0.93f), 0.07f, 0.16f, 0.07f);
        }
        for (int i = 0; i < 6; i++)
        {
            float a = i / 6f * Mathf.PI * 2f + 0.3f;
            bulbs.Gem(V(Mathf.Cos(a) * 0.55f, -0.1f, Mathf.Sin(a) * 0.55f), 0.06f, 0.14f, 0.06f);
        }
        var crystals = new MB();
        for (int i = 0; i < 16; i++)
        {
            float a = i / 16f * Mathf.PI * 2f;
            crystals.Gem(V(Mathf.Cos(a) * 0.95f, -0.3f - (i % 2) * 0.12f, Mathf.Sin(a) * 0.95f), 0.05f, 0.17f, 0.05f);
        }
        crystals.Gem(V(0, -0.7f, 0), 0.12f, 0.4f, 0.12f);
        var bm = MeshObject("Bulbs", g, bulbs.ToMesh("th_chandelier_bulbs_" + Mathf.RoundToInt(p.z * 10)), glow, false); bm.transform.localPosition = Vector3.zero;
        MeshObject("Crystals", g, crystals.ToMesh("th_chandelier_crystals_" + Mathf.RoundToInt(p.z * 10)), Lit(P + "crystal", C("#fff8ee"), 0.8f, new Color(0.9f, 0.85f, 0.75f)), false);
    }

    // ---------- the grand stage ----------

    static void MakeStage(Transform t, Transform blocks)
    {
        const float cx = 0f, cz = 12.7f, h = 0.6f;
        Deck(t, blocks, cx, cz, 6.5f, 2.5f, h, wood, gold);
        // gold footlights along the lip
        var foot = new MB();
        for (float x = -6.2f; x <= 6.21f; x += 0.45f) foot.Gem(V(x, h + 0.05f, 10.35f), 0.09f, 0.11f, 0.09f);
        MeshObject("Footlights", t, foot.ToMesh("th_footlights"), Lit(P + "footlight", C("#ffb46a"), 0.3f, new Color(3.4f, 1.8f, 0.7f)), false);

        // proscenium: two gold-and-red columns and a header, with a scalloped pelmet
        foreach (float s in new[] { -1f, 1f })
        {
            Prim(PrimitiveType.Cube, "ProsColumn", t, V(s * 7.0f, 4.3f, 10.3f), V(1.2f, 8.2f, 0.9f), red);
            Prim(PrimitiveType.Cube, "ProsGoldA", t, V(s * 6.38f, 4.3f, 9.8f), V(0.12f, 8.0f, 0.14f), gold, null, false);
            Prim(PrimitiveType.Cube, "ProsGoldB", t, V(s * 7.0f, 4.3f, 9.82f), V(0.12f, 8.0f, 0.14f), gold, null, false);
            Prim(PrimitiveType.Cube, "ProsBase", t, V(s * 7.0f, 0.5f, 10.3f), V(1.5f, 1.0f, 1.2f), gold);
        }
        Prim(PrimitiveType.Cube, "ProsHeader", t, V(0, 8.2f, 10.3f), V(15.0f, 1.5f, 0.9f), red);
        Prim(PrimitiveType.Cube, "ProsHeaderGold", t, V(0, 7.4f, 9.78f), V(14.4f, 0.2f, 0.1f), gold, null, false);
        var pel = new MB();
        for (int i = 0; i < 14; i++)
        {
            float x0 = -6.5f + i, x1 = x0 + 1f;
            pel.Tri(V(x0, 7.35f, 9.75f), V(x1, 7.35f, 9.75f), V(x0 + 0.5f, 6.75f, 9.75f));
        }
        MeshObject("Pelmet", t, pel.ToMesh("th_pelmet"), DoubleSided(P + "pelmet", C("#c42a4c"), 0.3f), false);
        var pelGold = new MB();
        for (int i = 0; i < 14; i++) pelGold.Gem(V(-6.0f + i, 6.7f, 9.72f), 0.07f, 0.1f, 0.07f);
        MeshObject("PelmetTassels", t, pelGold.ToMesh("th_pelmet_gold"), gold, false);

        // curtains: pleated, tied back with gold at 2.6 m
        var cur = new MB();
        foreach (float s in new[] { -1f, 1f }) Curtain(cur, s, 10.0f);
        MeshObject("Curtains", t, cur.ToMesh("th_curtains"), DoubleSided(P + "curtain", C("#a81f43"), 0.3f), true);
        foreach (float s in new[] { -1f, 1f })
        {
            Prim(PrimitiveType.Sphere, "TieBack", t, V(s * 5.3f, 2.6f, 9.8f), V(0.38f, 0.38f, 0.38f), gold);
            var tas = new MB(); tas.Gem(V(0, 0, 0), 0.08f, 0.35f, 0.08f);
            var tm = MeshObject("Tassel", t, tas.ToMesh("th_tassel"), gold, false);
            tm.transform.position = V(s * 5.3f, 2.15f, 9.7f);
        }

        // backdrop: a plum cyclorama with a sunburst and a big gold star, lit warm
        Prim(PrimitiveType.Cube, "Cyclorama", t, V(0, 5.0f, 15.5f), V(13.2f, 10f, 0.3f), Lit(P + "cyc", C("#4a2468"), 0.1f), null, false);
        var rayMat = Lit(P + "ray", C("#ffcf8a"), 0.2f, new Color(1.5f, 0.95f, 0.45f));
        for (int i = 0; i < 14; i++)
        {
            float a = -80f + i * (160f / 13f);
            var ray = Prim(PrimitiveType.Cube, "Ray", t, V(0, 3.0f, 15.3f), V(0.28f, 9f, 0.05f), rayMat, V(0, 0, a), false);
            ray.transform.localPosition = V(0, 3.0f, 15.3f) + Quaternion.Euler(0, 0, a) * V(0, 4.8f, 0);
        }
        var starMesh = AssetDatabase.LoadAssetAtPath<Mesh>(WorldArtBuilder.MeshDir + "/MarkerStar.asset");
        void Star(Vector3 pos, float s, Material m)
        {
            var g = new GameObject("BackStar");
            g.transform.SetParent(t, false);
            g.transform.localPosition = pos; g.transform.localRotation = Quaternion.Euler(0, 180f, 0); g.transform.localScale = V(s, s, s * 0.35f);
            g.AddComponent<MeshFilter>().sharedMesh = starMesh;
            var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off;
        }
        Star(V(0, 4.6f, 15.2f), 4.8f, Lit(P + "star_edge", C("#f98bb0"), 0.3f, new Color(1.6f, 0.5f, 0.9f)));
        Star(V(0, 4.6f, 15.0f), 3.8f, glowGold);

        // a pink star on the deck, a mic stand, two wedge monitors
        var deckStar = new GameObject("DeckStar");
        deckStar.transform.SetParent(t, false);
        deckStar.transform.localPosition = V(0, h + 0.012f, 11.6f);
        deckStar.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        deckStar.transform.localScale = V(1.9f, 1.9f, 0.02f);
        deckStar.AddComponent<MeshFilter>().sharedMesh = starMesh;
        deckStar.AddComponent<MeshRenderer>().sharedMaterial = Lit(P + "deck_star", C("#f98bb0"), 0.3f, new Color(0.7f, 0.2f, 0.4f));
        Prim(PrimitiveType.Cylinder, "MicBase", t, V(0.8f, h + 0.02f, 11.1f), V(0.4f, 0.02f, 0.4f), plum);
        Prim(PrimitiveType.Cylinder, "MicPole", t, V(0.8f, h + 0.75f, 11.1f), V(0.035f, 0.75f, 0.035f), gold);
        Prim(PrimitiveType.Sphere, "MicHead", t, V(0.8f, h + 1.55f, 11.05f), V(0.16f, 0.16f, 0.16f), gold);
        var wedgeMat = Lit(P + "wedge", C("#2a2438"), 0.4f);
        foreach (float sx in new[] { -2.6f, 2.6f })
            Prim(PrimitiveType.Cube, "Wedge", t, V(sx, h + 0.22f, 10.6f), V(0.9f, 0.4f, 0.6f), wedgeMat, V(-25f, 0, 0));

        // hanging spots behind the pelmet, lilac/pink/gold, aimed at the deck star
        var housing = Lit(P + "spot_housing", C("#2a2438"), 0.5f);
        Color[] lens = { new Color(3f, 0.7f, 1.6f), new Color(3.2f, 2.5f, 0.8f), new Color(1.2f, 1.4f, 3.4f), new Color(3.2f, 2.5f, 0.8f), new Color(3f, 0.7f, 1.6f), new Color(1.2f, 1.4f, 3.4f) };
        for (int i = 0; i < lens.Length; i++)
        {
            float x = -5f + i * 2f;
            Vector3 pos = V(x, 7.0f, 11.0f);
            Vector3 aim = V(i * 0.6f - 1.5f, h, 11.5f);
            SpotLight(t, "Spot" + i, pos, Quaternion.LookRotation(aim - pos), lens[i], housing,
                Lit(P + "spot_lens" + i, Color.white, 0.2f, lens[i]), 8.5f, 1.15f);
        }
        // back wall blockers
        for (float x = -6f; x <= 6.1f; x += 1.4f) Block(blocks, x, 14.9f, 0.6f);
    }

    static void Curtain(MB mb, float sign, float zBase)
    {
        const int cols = 16, rows = 12;
        float W(float y)
        {
            if (y < 2.6f) return Mathf.Lerp(2.3f, 1.2f, y / 2.6f);
            return Mathf.Lerp(1.2f, 3.4f, Mathf.Pow((y - 2.6f) / 4.6f, 1.4f));
        }
        Vector3 P0(float f, float y)
        {
            float x = sign * (6.5f - f * W(y));
            float z = zBase + Mathf.Sin(f * cols * 0.5f * Mathf.PI * 2f * 0.5f) * 0.16f - f * 0.15f;
            return V(x, y, z);
        }
        for (int r = 0; r < rows; r++)
        {
            float y0 = 0.6f + r * (6.7f / rows), y1 = 0.6f + (r + 1) * (6.7f / rows);
            for (int c = 0; c < cols; c++)
            {
                float f0 = c / (float)cols, f1 = (c + 1) / (float)cols;
                mb.Quad(P0(f0, y0), P0(f1, y0), P0(f1, y1), P0(f0, y1));
            }
        }
    }

    // ---------- the city beyond ----------

    static void MakeSkyline(Transform t)
    {
        string[] north = { "building_G", "building_C", "building_D", "building_H", "building_B", "building_E" };
        for (int i = 0; i < north.Length; i++)
            Fbx(City + north[i] + ".fbx", t, V(-20f + i * 8f, 0, 26f), 0f, CityScale * 1.5f, cityMat, "North_" + (i + 1));
        string[] side = { "building_E", "building_A", "building_F", "building_C", "building_D" };
        for (int i = 0; i < side.Length; i++)
        {
            Fbx(City + side[i] + ".fbx", t, V(-24f, 0, -14f + i * 10f), -90f, CityScale * 1.4f, cityMat, "West_" + (i + 1));
            Fbx(City + side[(i + 2) % 5] + ".fbx", t, V(24f, 0, -14f + i * 10f), 90f, CityScale * 1.4f, cityMat, "East_" + (i + 1));
        }
        Fbx(City + "building_A.fbx", t, V(-12f, 0, -22f), 180f, CityScale * 1.2f, cityMat, "South_1");
        Fbx(City + "building_G.fbx", t, V(12f, 0, -22f), 180f, CityScale * 1.2f, cityMat, "South_2");
        // the buildings beside the palace front, low, so the plaza's edges read as a street
        Fbx(City + "building_B.fbx", t, V(-19f, 0, -10f), -90f, CityScale, cityMat, "Side_W");
        Fbx(City + "building_D.fbx", t, V(19f, 0, -10f), 90f, CityScale, cityMat, "Side_E");
    }

    // ---------- light shows and particles ----------

    static void MakeLights(Transform world) { }

    static void MakeParticles()
    {
        var dot = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/World/Textures/soft_dot.png");
        // warm dust in the shafts of light, over the whole hall
        var dust = ParticleMaterial("th_dust", dot, true, new Color(1.6f, 1.2f, 0.8f, 0.55f));
        Drift("HallDust", V(0, 4.5f, 3f), V(26f, 8f, 28f), dust, 80, 8f, new Vector2(6f, 10f), new Vector2(0.08f, 0.2f), Color.white, Color.white,
            new Vector3(-0.05f, -0.02f, -0.05f), new Vector3(0.05f, 0.08f, 0.05f), 0f, 0.5f);
        // glitter falling softly in the plaza and over the crowd
        var glit = ParticleMaterial("th_glitter", dot, true, new Color(2f, 1.6f, 1.2f, 0.9f));
        Drift("Glitter", V(0, 7f, -3f), V(24f, 1f, 22f), glit, 90, 9f, new Vector2(7f, 11f), new Vector2(0.06f, 0.13f), C("#ffd36a"), C("#ff9fcf"),
            new Vector3(-0.1f, -0.7f, -0.1f), new Vector3(0.1f, -0.3f, 0.1f), 0f, 0.4f);
    }
}

