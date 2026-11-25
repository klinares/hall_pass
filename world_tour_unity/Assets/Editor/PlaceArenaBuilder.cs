// Phase 8.2 builder B: Place_Arena, "the arena, night". The walkable floor of a huge cut-away bowl: stepped stands rising on both sides with a
// packed crowd (about a thousand figures merged into a handful of meshes) and glowing tier edges, a big stage with a screen wall, a sound desk
// in the middle, sweeping spotlights, a hanging video cube, sparks, stars overhead.
//
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -executeMethod PlaceArenaBuilder.CreateAll
//
// Layout (x right, z away from the camera; walkers clamped to +-13.5):
//   spawn at the tunnel mouth (0, -10.5), runway lights up the middle, sound desk at (0, -2), crowd box x +-6.4 z 2.6 .. 7.0,
//   stage deck x +-8 z 8.8 .. 13.6 (ramp from z 8), stands at x +-15 .. 30 rising 1.1 m a tier, screens behind the stage.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using WorldTour.World;
using WorldTour.World.Places;
using static PlaceBKit;

public static class PlaceArenaBuilder
{
    public const string ScenePath = "Assets/World/Scenes/Place_Arena.unity";
    const string P = "ar_";

    static Material dark, navy, steel, black, pinkGlow, cyanGlow, whiteGlow, goldGlow, lilacGlow;

    public static void CreateAll()
    {
        try
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(WorldArtBuilder.StarPrefab) == null) WorldArtBuilder.Run();
            Directory.CreateDirectory("Assets/World/Scenes");
            MakeScene();
            AssetDatabase.SaveAssets();
            Debug.Log("PlaceArenaBuilder: done " + ScenePath);
        }
        catch (System.Exception e) { Debug.LogError("PlaceArenaBuilder failed: " + e); }
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static Texture2D FloorTexture()
    {
        Color a = C("#1b1f4a"), b = C("#23285a"), line = C("#4b53b8"), dot = C("#7a86ff");
        return MakeTexture("ar_floor", 256, 256, (x, y) =>
        {
            int cx = x % 64, cy = y % 64;
            int tile = ((x / 64) + (y / 64)) % 2;
            Color c = tile == 0 ? a : b;
            if (cx < 2 || cy < 2) return line;
            if (Mathf.Abs(cx - 32) + Mathf.Abs(cy - 32) < 4) return dot;
            float v = 0.96f + ((x * 73856093 ^ y * 19349663) & 255) / 255f * 0.07f;
            // a soft sheen toward the middle of each tile
            float d = Mathf.Max(Mathf.Abs(cx - 32), Mathf.Abs(cy - 32)) / 32f;
            return c * v * (1.12f - d * 0.2f);
        }, TextureWrapMode.Repeat, FilterMode.Bilinear, true);
    }

    // ---------- the scene ----------

    public static void MakeScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Reported.Clear();

        dark = Lit(P + "dark", C("#171a3c"), 0.4f);
        navy = Lit(P + "navy", C("#1f2a66"), 0.25f);
        steel = Lit(P + "steel", C("#8f95c4"), 0.6f);
        black = Lit(P + "black", C("#0e0f24"), 0.5f);
        pinkGlow = Lit(P + "pink_glow", C("#ff5fa8"), 0.3f, new Color(3.2f, 0.6f, 1.6f));
        cyanGlow = Lit(P + "cyan_glow", C("#4ae0ff"), 0.3f, new Color(0.5f, 2.6f, 3.4f));
        whiteGlow = Lit(P + "white_glow", C("#ffffff"), 0.3f, new Color(3f, 3f, 3.4f));
        goldGlow = Lit(P + "gold_glow", C("#ffd36a"), 0.3f, new Color(3.2f, 2.1f, 0.5f));
        lilacGlow = Lit(P + "lilac_glow", C("#a8a9fc"), 0.3f, new Color(1.6f, 1.6f, 3.6f));

        var profile = MakeProfile("ArenaVolumeProfile", new Grade
        {
            BloomThreshold = 0.8f, BloomIntensity = 1.0f, BloomScatter = 0.8f, BloomTint = new Color(0.9f, 0.85f, 1f),
            Exposure = 0.1f, Contrast = 20f, Saturation = 22f, Temperature = -6f, Tint = 8f, Vignette = 0.38f,
            Filter = new Color(0.96f, 0.97f, 1.04f),
            Shadows = new Vector4(0.8f, 0.82f, 1.2f, 0f), Highlights = new Vector4(1.0f, 0.95f, 1.1f, 0f),
        });
        var world = Group("World").transform;
        var ground = Group("Floor", world).transform;
        var stands = Group("Stands", world).transform;
        var stage = Group("Stage", world).transform;
        var rig = Group("Rig", world).transform;
        var props = Group("Props", world).transform;
        var blocks = Group("Blockers", world).transform;

        MakeFloor(ground);
        MakeStands(stands);
        MakeStage(stage, blocks);
        MakeRig(rig);
        MakeProps(props, blocks);
        MakeSky(world);

        var spec = new Spec
        {
            Id = "arena", ScenePath = ScenePath,
            Spawn = V(0, 0, -10.5f), Stage = V(0, 0, 10.5f), Crowd = V(0, 0, 4.8f), CrowdSize = new Vector2(12.8f, 4.4f), Exit = V(11.5f, 0, -10.5f), Riff = V(-1.8f, 0, -11.6f),
            Crew = new[] { V(-5.0f, 0, -5.8f), V(8.8f, 0, 1.4f), V(-9.6f, 0, 5.4f) },
            Bg = new Color(0.05f, 0.05f, 0.18f),
            ExitRingBase = C("#4ae0ff"), ExitRing = new Color(0.6f, 2.4f, 3.2f), ExitHole = C("#1f2a66"), ExitSign = C("#ff5fa8"), ExitYaw = 0f,
        };
        Contract(spec, blocks, profile, P);
        MakeAtmosphere(new Air
        {
            SkyName = "ar_sky", Top = C("#06071f"), Mid = C("#121540"), Horizon = C("#4a2a78"), Bottom = C("#150f32"),
            SunEuler = new Vector3(40f, -25f, 0f),
            AmbientSky = C("#4a52c0"), AmbientEquator = C("#6a4aa8"), AmbientGround = C("#2a2050"), Fog = C("#1a1444"),
            AmbientIntensity = 1.2f, FogDensity = 0.0045f, Shade = C("#30306a"),
        });
        // a cool moon from the front left: it gives the figures a rim, shadows fall back toward the stage
        MakeSun(new Color(0.55f, 0.62f, 1f), 1.15f, new Vector3(40f, -25f, 0f), 0.7f);
        MakeParticles();
        StaticBatch(ground);
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    // ---------- floor ----------

    static void MakeFloor(Transform t)
    {
        var floor = LitTex(P + "floor_mat", Color.white, FloorTexture(), 0.55f);
        floor.SetTextureScale("_BaseMap", new Vector2(10f, 10f));
        Prim(PrimitiveType.Plane, "Floor", t, V(0, 0, 5f), V(5f, 1, 5f), floor, null, false);
        // a darker apron outside the playing floor
        Prim(PrimitiveType.Plane, "Apron", t, V(0, -0.02f, 5f), V(24f, 1, 24f), dark, null, false);
        // runway lights up the middle from the tunnel to the sound desk, chevrons between
        var line = new MB();
        for (float z = -13f; z < 1.6f; z += 0.9f) { line.Box(V(-2.1f, 0.04f, z), V(0.14f, 0.04f, 0.5f)); line.Box(V(2.1f, 0.04f, z), V(0.14f, 0.04f, 0.5f)); }
        MeshObject("RunwayLights", t, line.ToMesh("ar_runway"), cyanGlow, false);
        var chev = new MB();
        for (float z = -12f; z < 0f; z += 2.4f)
        {
            chev.Box(V(-0.35f, 0.035f, z), V(0.9f, 0.03f, 0.18f), 35f);
            chev.Box(V(0.35f, 0.035f, z), V(0.9f, 0.03f, 0.18f), -35f);
        }
        MeshObject("Chevrons", t, chev.ToMesh("ar_chevrons"), pinkGlow, false);
        // the big star ring under the stage front: a bright circle and a star, both flat
        var starMesh = AssetDatabase.LoadAssetAtPath<Mesh>(WorldArtBuilder.MeshDir + "/MarkerStar.asset");
        var ring = Prim(PrimitiveType.Cylinder, "StageRing", t, V(0, 0.03f, 4.8f), V(10.5f, 0.01f, 10.5f), Lit(P + "ring_dark", C("#14163a"), 0.5f), null, false);
        Prim(PrimitiveType.Cylinder, "StageRingEdge", t, V(0, 0.012f, 4.8f), V(11f, 0.01f, 11f), lilacGlow, null, false);
        var star = new GameObject("FloorStar");
        star.transform.SetParent(t, false);
        star.transform.localPosition = V(0, 0.06f, 4.8f);
        star.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        star.transform.localScale = V(7f, 7f, 0.02f);
        star.AddComponent<MeshFilter>().sharedMesh = starMesh;
        var sr = star.AddComponent<MeshRenderer>(); sr.sharedMaterial = Lit(P + "floor_star", C("#2d2f78"), 0.5f, new Color(0.35f, 0.3f, 1.0f)); sr.shadowCastingMode = ShadowCastingMode.Off;
        // light spill from the stage: soft pink and cyan pools on the floor
        var dot = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/World/Textures/soft_dot.png");
        Prim(PrimitiveType.Quad, "SpillPink", t, V(-3f, 0.07f, 6f), V(16f, 9f, 1f), ParticleMaterial("ar_spill_pink", dot, true, new Color(1.6f, 0.45f, 1.1f, 0.34f)), V(90, 0, 0), false);
        Prim(PrimitiveType.Quad, "SpillCyan", t, V(4f, 0.075f, 5f), V(14f, 8f, 1f), ParticleMaterial("ar_spill_cyan", dot, true, new Color(0.4f, 1.3f, 1.8f, 0.3f)), V(90, 0, 0), false);
        Prim(PrimitiveType.Quad, "SpillGold", t, V(0f, 0.08f, 9.5f), V(18f, 6f, 1f), ParticleMaterial("ar_spill_gold", dot, true, new Color(1.6f, 1.1f, 0.5f, 0.4f)), V(90, 0, 0), false);
    }

    // ---------- the stands: stepped tiers with neon edges and a packed crowd ----------

    static void MakeStands(Transform t)
    {
        const int tiers = 7;
        const float x0 = 15f, depth = 2.2f, rise = 1.1f, z0 = -14f, z1 = 24f;
        var seats = new MB(); var edges = new MB[] { new MB(), new MB() };
        var spots = new List<Vector3>(); var yaws = new List<float>();
        var rng = new System.Random(77);
        var phones = new MB[] { new MB(), new MB() };
        foreach (float s in new[] { -1f, 1f })
        {
            for (int i = 0; i < tiers; i++)
            {
                float h = rise * (i + 1);
                float cx = s * (x0 + i * depth + depth / 2f);
                float cz = (z0 + z1) / 2f;
                seats.Box(V(cx, h / 2f, cz), V(depth, h, z1 - z0));
                // the glowing lip on the stage-side edge of every tier
                edges[i % 2].Box(V(s * (x0 + i * depth) - s * 0.03f, h - 0.04f, cz), V(0.08f, 0.1f, z1 - z0 - 0.4f));
                // two rows of people standing on the tier, packed along z
                for (int row = 0; row < 2; row++)
                {
                    float x = s * (x0 + i * depth + 0.7f + row * 0.85f);
                    for (float z = z0 + 0.6f; z < z1 - 0.5f; z += 0.72f)
                    {
                        if (rng.NextDouble() < 0.06) continue;
                        var p = V(x + (float)(rng.NextDouble() - 0.5) * 0.15f, h, z + (float)(rng.NextDouble() - 0.5) * 0.18f);
                        spots.Add(p); yaws.Add(s > 0 ? -90f : 90f);
                        // some hold a phone light up
                        if (rng.NextDouble() < 0.18) phones[rng.Next(2)].Gem(p + V(-s * 0.12f, 1.55f + (float)rng.NextDouble() * 0.2f, 0), 0.07f, 0.1f, 0.07f);
                    }
                }
            }
            // the back wall of the bowl
            seats.Box(V(s * (x0 + tiers * depth + 0.5f), 7f, 5f), V(1f, 14f, z1 - z0 + 4f));
        }
        MeshObject("Tiers", t, seats.ToMesh("ar_tiers"), navy, true);
        MeshObject("TierEdgesA", t, edges[0].ToMesh("ar_edges_a"), pinkGlow, false);
        MeshObject("TierEdgesB", t, edges[1].ToMesh("ar_edges_b"), cyanGlow, false);
        MeshObject("PhonesA", t, phones[0].ToMesh("ar_phones_a"), whiteGlow, false);
        MeshObject("PhonesB", t, phones[1].ToMesh("ar_phones_b"), goldGlow, false);
        StaticCrowd(t, "AR_Stand_", spots, yaws, new[] { "#ff5fa8", "#4ae0ff", "#ffffff", "#ffd23a", "#a8a9fc", "#ff9a3c", "#7fe3b0", "#e0e0ff" },
            new[] { "#f2c6a0", "#d89c74", "#a8704c", "#6e4630", "#f6d7c0" }, 31, 0.95f, 0f);
        // the north end: three upper tiers behind the screens with distant lights
        var far = new MB(); var lights = new MB();
        for (int i = 0; i < 5; i++)
        {
            float h = 2.2f + i * 1.3f;
            far.Box(V(0, h / 2f, 22f + i * 2.2f), V(64f, h, 2.2f));
        }
        for (int k = 0; k < 140; k++)
            lights.Gem(V((float)(rng.NextDouble() - 0.5) * 58f, 3.5f + (float)rng.NextDouble() * 6f, 22f + (float)rng.NextDouble() * 9f), 0.09f, 0.12f, 0.09f);
        MeshObject("NorthTiers", t, far.ToMesh("ar_north_tiers"), navy, false);
        MeshObject("NorthLights", t, lights.ToMesh("ar_north_lights"), whiteGlow, false);
    }

    // ---------- the stage and its screens ----------

    static void MakeStage(Transform t, Transform blocks)
    {
        const float cx = 0f, cz = 11.2f, h = 0.7f;
        var deckMat = Lit(P + "deck", C("#101230"), 0.65f);
        Deck(t, blocks, cx, cz, 8f, 2.4f, h, deckMat, pinkGlow);
        // a glowing apron line round the deck and a lit inset star
        var edge = new MB();
        edge.Box(V(0, h + 0.02f, 8.85f), V(15.6f, 0.03f, 0.14f));
        edge.Box(V(-7.8f, h + 0.02f, cz), V(0.14f, 0.03f, 4.6f));
        edge.Box(V(7.8f, h + 0.02f, cz), V(0.14f, 0.03f, 4.6f));
        MeshObject("DeckEdge", t, edge.ToMesh("ar_deck_edge"), cyanGlow, false);
        var starMesh = AssetDatabase.LoadAssetAtPath<Mesh>(WorldArtBuilder.MeshDir + "/MarkerStar.asset");
        var deckStar = new GameObject("DeckStar");
        deckStar.transform.SetParent(t, false);
        deckStar.transform.localPosition = V(0, h + 0.012f, 10.0f);
        deckStar.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        deckStar.transform.localScale = V(2.0f, 2.0f, 0.02f);
        deckStar.AddComponent<MeshFilter>().sharedMesh = starMesh;
        deckStar.AddComponent<MeshRenderer>().sharedMaterial = Lit(P + "deck_star", C("#ff5fa8"), 0.3f, new Color(1.3f, 0.3f, 0.7f));

        // the screen wall: a centre screen in a frame, two angled side screens, simple lit shapes (rings, a star, bars) and no words
        float sz = 14.2f;
        Prim(PrimitiveType.Cube, "ScreenBack", t, V(0, 5.0f, sz + 0.25f), V(20f, 10.5f, 0.3f), black, null, false);
        Prim(PrimitiveType.Cube, "ScreenFrame", t, V(0, 5.6f, sz - 0.05f), V(12.6f, 7.0f, 0.3f), steel, null, false);
        var screen = Lit(P + "screen", C("#1b1650"), 0.3f, new Color(0.25f, 0.2f, 0.9f));
        Prim(PrimitiveType.Cube, "Screen", t, V(0, 5.6f, sz - 0.24f), V(12.0f, 6.4f, 0.1f), screen, null, false);
        for (int i = 0; i < 4; i++)
        {
            float r = 5.0f - i * 1.1f;
            Prim(PrimitiveType.Cylinder, "ScreenRing" + i, t, V(0, 5.6f, sz - 0.31f - i * 0.005f), V(r, 0.012f, r), i % 2 == 0 ? pinkGlow : lilacGlow, V(90, 0, 0), false);
            Prim(PrimitiveType.Cylinder, "ScreenRingCut" + i, t, V(0, 5.6f, sz - 0.315f - i * 0.005f), V(r - 0.28f, 0.012f, r - 0.28f), screen, V(90, 0, 0), false);
        }
        var sstar = new GameObject("ScreenStar");
        sstar.transform.SetParent(t, false);
        sstar.transform.localPosition = V(0, 5.6f, sz - 0.4f);
        sstar.transform.localRotation = Quaternion.Euler(0, 180f, 0);
        sstar.transform.localScale = V(3.4f, 3.4f, 0.1f);
        sstar.AddComponent<MeshFilter>().sharedMesh = starMesh;
        var ssr = sstar.AddComponent<MeshRenderer>(); ssr.sharedMaterial = goldGlow; ssr.shadowCastingMode = ShadowCastingMode.Off;
        var pulse = sstar.AddComponent<PlaceMotion>(); pulse.kind = PlaceMotion.Kind.Pulse; pulse.speed = 2.4f; pulse.amplitude = 6f;
        // equaliser bars along the screen's foot
        var bars = new MB();
        for (int i = 0; i < 20; i++) bars.Box(V(-5.5f + i * 0.58f, 2.5f + (0.3f + 0.25f * Mathf.Abs(Mathf.Sin(i * 1.7f)) * 2.2f) / 2f, sz - 0.34f), V(0.4f, 0.3f + 0.25f * Mathf.Abs(Mathf.Sin(i * 1.7f)) * 2.2f, 0.06f));
        MeshObject("ScreenBars", t, bars.ToMesh("ar_screen_bars"), cyanGlow, false);
        foreach (float s in new[] { -1f, 1f })
        {
            var side = Group("SideScreen", t).transform;
            side.position = V(s * 10.2f, 5.0f, sz - 0.9f);
            side.rotation = Quaternion.Euler(0, s * -22f, 0);
            Prim(PrimitiveType.Cube, "Frame", side, V(0, 0, 0), V(4.2f, 7.4f, 0.3f), steel, null, false);
            Prim(PrimitiveType.Cube, "Panel", side, V(0, 0, -0.17f), V(3.8f, 7.0f, 0.1f), Lit(P + "side_screen", C("#18144a"), 0.3f, new Color(0.2f, 0.15f, 0.8f)), null, false);
            for (int i = 0; i < 5; i++)
                Prim(PrimitiveType.Cube, "Stripe", side, V(0, -2.8f + i * 1.4f, -0.23f), V(3.0f - (i % 2) * 0.8f, 0.3f, 0.05f), i % 2 == 0 ? pinkGlow : cyanGlow, null, false);
        }
        // front truss with lights, and two towers that carry it
        var truss = steel;
        float fz = 8.2f, top = 7.6f;
        TrussBeam(t, truss, V(-9.2f, top, fz), V(9.2f, top, fz), Vector3.up, 0.7f);
        TrussBeam(t, truss, V(-9.2f, h, fz), V(-9.2f, top, fz), Vector3.forward, 0.6f);
        TrussBeam(t, truss, V(9.2f, h, fz), V(9.2f, top, fz), Vector3.forward, 0.6f);
        TrussBeam(t, truss, V(-9.2f, top, fz), V(-9.2f, top, 13.8f), Vector3.up, 0.5f);
        TrussBeam(t, truss, V(9.2f, top, fz), V(9.2f, top, 13.8f), Vector3.up, 0.5f);
        // two hanging line arrays and subs on the deck
        var woofer = Lit(P + "woofer", C("#1a1b3c"), 0.4f);
        foreach (float sx in new[] { -10.4f, 10.4f })
        {
            for (int k = 0; k < 6; k++)
            {
                Prim(PrimitiveType.Cube, "Array", t, V(sx, 6.8f - k * 0.62f, 9.0f), V(1.0f, 0.55f, 0.8f), black);
                Prim(PrimitiveType.Cube, "ArrayLip", t, V(sx, 6.8f - k * 0.62f, 8.58f), V(0.9f, 0.08f, 0.04f), cyanGlow, null, false);
            }
            Prim(PrimitiveType.Cube, "ArrayRig", t, V(sx, 8.1f, 9.0f), V(1.2f, 0.2f, 1.0f), steel);
            Speaker(t, blocks, V(sx * 0.82f, h, 9.0f), black, woofer, lilacGlow, 1.1f);
        }
        // mic
        Prim(PrimitiveType.Cylinder, "MicBase", t, V(0.9f, h + 0.02f, 9.8f), V(0.4f, 0.02f, 0.4f), black);
        Prim(PrimitiveType.Cylinder, "MicPole", t, V(0.9f, h + 0.75f, 9.8f), V(0.035f, 0.75f, 0.035f), steel);
        Prim(PrimitiveType.Sphere, "MicHead", t, V(0.9f, h + 1.55f, 9.75f), V(0.16f, 0.16f, 0.16f), whiteGlow);

        // the six flare spots along the truss (the gameplay's Spot0..5), beams down onto the deck
        Color[] lens = { new Color(3f, 0.6f, 1.6f), new Color(0.6f, 2.4f, 3.4f), new Color(3.2f, 2.5f, 0.8f), new Color(0.6f, 2.4f, 3.4f), new Color(3f, 0.6f, 1.6f), new Color(3.2f, 2.5f, 0.8f) };
        var housing = Lit(P + "spot_housing", C("#26274f"), 0.5f);
        for (int i = 0; i < lens.Length; i++)
        {
            Vector3 pos = V(-6.0f + i * 2.4f, top - 0.5f, fz + 0.05f);
            Vector3 aim = V((i - 2.5f) * 0.8f, h, 10.0f);
            SpotLight(t, "Spot" + i, pos, Quaternion.LookRotation(aim - pos), lens[i], housing, Lit(P + "spot_lens" + i, Color.white, 0.2f, lens[i]), 9f, 1.4f, 0.16f);
        }
        for (float x = -8f; x <= 8.1f; x += 1.4f) Block(blocks, x, 14.7f, 0.6f);
    }

    // ---------- the moving lights and the hanging video cube ----------

    static void MakeRig(Transform t)
    {
        // a big ring-and-bar truss over the floor with moving heads that sweep across the crowd (names Sweep0.., not Spot, so the flare leaves them alone)
        var housing = Lit(P + "sweep_housing", C("#26274f"), 0.5f);
        Color[] cols = { new Color(3f, 0.6f, 1.6f), new Color(0.6f, 2.4f, 3.4f), new Color(3.2f, 2.5f, 0.8f), new Color(1.6f, 1.6f, 3.6f) };
        TrussBeam(t, steel, V(-11.5f, 10.6f, 12.7f), V(11.5f, 10.6f, 12.7f), Vector3.up, 0.7f);
        for (int i = 0; i < 8; i++)
        {
            float x = -9.8f + i * 2.8f;
            Vector3 pos = V(x, 10.1f, 12.5f);
            Vector3 aim = V(x * 0.9f, 0.5f, 3.5f);
            var s = SpotLight(t, "Sweep" + i, pos, Quaternion.LookRotation(aim - pos), cols[i % cols.Length], housing, Lit(P + "sweep_lens" + (i % 4), Color.white, 0.2f, cols[i % cols.Length]), 14f, 1.1f, 0.07f);
            var m = s.gameObject.AddComponent<PlaceMotion>();
            m.kind = PlaceMotion.Kind.Sweep; m.axis = Vector3.up; m.amplitude = 22f; m.speed = 0.55f + i * 0.07f; m.phase = i * 0.9f;
            m.crossAxis = Vector3.right; m.crossAmplitude = 8f;
        }
        // the hanging video cube: four glowing faces with simple shapes
        var cube = Group("VideoCube", t).transform;
        cube.position = V(0, 9.6f, 6.0f);
        Prim(PrimitiveType.Cube, "Body", cube, V(0, 0, 0), V(4.6f, 2.6f, 4.6f), black);
        var cm = cube.gameObject.AddComponent<PlaceMotion>(); cm.kind = PlaceMotion.Kind.Spin; cm.axis = Vector3.up; cm.speed = 8f;
        for (int f = 0; f < 4; f++)
        {
            var face = Group("Face" + f, cube).transform;
            face.localRotation = Quaternion.Euler(0, f * 90f, 0);
            Prim(PrimitiveType.Cube, "Panel", face, V(0, 0, -2.32f), V(4.2f, 2.2f, 0.06f), Lit(P + "cube_face", C("#1b1650"), 0.3f, new Color(0.3f, 0.25f, 1.0f)), null, false);
            Prim(PrimitiveType.Cylinder, "Ring", face, V(0, 0, -2.37f), V(1.7f, 0.01f, 1.7f), f % 2 == 0 ? pinkGlow : cyanGlow, V(90, 0, 0), false);
            Prim(PrimitiveType.Cylinder, "RingCut", face, V(0, 0, -2.375f), V(1.35f, 0.01f, 1.35f), Lit(P + "cube_face", C("#1b1650"), 0.3f, new Color(0.3f, 0.25f, 1.0f)), V(90, 0, 0), false);
            Prim(PrimitiveType.Cube, "Bar", face, V(0, -0.9f, -2.38f), V(3.4f, 0.14f, 0.03f), goldGlow, null, false);
        }
        foreach (var o in new[] { V(-1.8f, 0, -1.8f), V(1.8f, 0, -1.8f), V(-1.8f, 0, 1.8f), V(1.8f, 0, 1.8f) })
            Rod(t, steel, cube.position + o + V(0, 1.3f, 0), V(o.x * 1.6f, 24f, 6f + o.z * 1.6f), 0.025f);
    }

    // ---------- the sound desk and the props ----------

    static void MakeProps(Transform t, Transform blocks)
    {
        // the front-of-house desk in the middle: a console, three glowing screens, a lit canopy
        var g = Group("SoundDesk", t).transform; g.position = V(0, 0, -2.0f);
        Prim(PrimitiveType.Cube, "Desk", g, V(0, 0.5f, 0), V(3.4f, 1.0f, 1.4f), black);
        Prim(PrimitiveType.Cube, "Console", g, V(0, 1.05f, -0.1f), V(3.2f, 0.1f, 1.1f), steel, V(-12f, 0, 0));
        for (int i = 0; i < 12; i++)
            Prim(PrimitiveType.Cube, "Fader", g, V(-1.4f + i * 0.255f, 1.12f, -0.1f), V(0.07f, 0.04f, 0.5f), i % 3 == 0 ? pinkGlow : cyanGlow, V(-12f, 0, 0), false);
        for (int i = 0; i < 3; i++)
            Prim(PrimitiveType.Cube, "Monitor", g, V(-1.0f + i * 1.0f, 1.65f, 0.45f), V(0.8f, 0.5f, 0.05f), i == 1 ? goldGlow : lilacGlow, V(-10f, 0, 0), false);
        foreach (float sx in new[] { -1.7f, 1.7f }) Prim(PrimitiveType.Cylinder, "Post", g, V(sx, 1.6f, -0.7f), V(0.06f, 1.6f, 0.06f), steel);
        Prim(PrimitiveType.Cube, "Canopy", g, V(0, 3.25f, 0), V(3.8f, 0.12f, 1.9f), black);
        Prim(PrimitiveType.Cube, "CanopyLip", g, V(0, 3.2f, -0.96f), V(3.8f, 0.1f, 0.05f), pinkGlow, null, false);
        BlockLine(blocks, V(-1.5f, 0, -2.0f), V(1.5f, 0, -2.0f), 0.9f);

        // flight cases and amp racks along the sides, a catering table, a merch stand with a glowing star
        var caseMat = Lit(P + "case", C("#20224a"), 0.4f);
        for (int i = 0; i < 4; i++)
        {
            Prim(PrimitiveType.Cube, "FlightCase", t, V(-12.4f, 0.4f + (i % 2) * 0.8f, -9f + (i / 2) * 1.2f), V(1.0f, 0.8f, 1.0f), caseMat);
            Prim(PrimitiveType.Cube, "CaseEdge", t, V(-11.88f, 0.4f + (i % 2) * 0.8f, -9f + (i / 2) * 1.2f), V(0.02f, 0.76f, 0.9f), i % 2 == 0 ? pinkGlow : cyanGlow, null, false);
        }
        Block(blocks, -12.4f, -9f, 0.9f); Block(blocks, -12.4f, -7.8f, 0.9f);
        // merch stand
        var m = Group("MerchStand", t).transform; m.position = V(7.0f, 0, -6.2f);
        Prim(PrimitiveType.Cube, "Counter", m, V(0, 0.55f, 0), V(2.6f, 1.1f, 0.9f), caseMat);
        Prim(PrimitiveType.Cube, "CounterTop", m, V(0, 1.14f, 0), V(2.7f, 0.07f, 1.0f), steel);
        foreach (float sx in new[] { -1.25f, 1.25f }) Prim(PrimitiveType.Cylinder, "Post", m, V(sx, 1.5f, 0.4f), V(0.06f, 1.5f, 0.06f), steel);
        Prim(PrimitiveType.Cube, "SignBoard", m, V(0, 3.2f, 0.4f), V(2.8f, 0.9f, 0.1f), black);
        var starMesh = AssetDatabase.LoadAssetAtPath<Mesh>(WorldArtBuilder.MeshDir + "/MarkerStar.asset");
        var ms = new GameObject("MerchStar");
        ms.transform.SetParent(m, false);
        ms.transform.localPosition = V(0, 3.2f, 0.3f);
        ms.transform.localRotation = Quaternion.Euler(0, 180f, 0);
        ms.transform.localScale = V(0.8f, 0.8f, 0.08f);
        ms.AddComponent<MeshFilter>().sharedMesh = starMesh;
        var msr = ms.AddComponent<MeshRenderer>(); msr.sharedMaterial = goldGlow; msr.shadowCastingMode = ShadowCastingMode.Off;
        for (int i = 0; i < 5; i++) Prim(PrimitiveType.Cube, "Tee", m, V(-1.0f + i * 0.5f, 1.35f, 0.05f), V(0.36f, 0.4f, 0.06f), i % 2 == 0 ? Lit(P + "tee_a", C("#ff5fa8"), 0.2f) : Lit(P + "tee_b", C("#4ae0ff"), 0.2f));
        BlockLine(blocks, V(5.9f, 0, -6.2f), V(8.1f, 0, -6.2f), 0.7f);
        // catering table on the east side, with a few round glowing lamps
        var c = Group("Catering", t).transform; c.position = V(10.6f, 0, 6.2f);
        Prim(PrimitiveType.Cube, "Table", c, V(0, 0.55f, 0), V(1.0f, 1.1f, 3.0f), caseMat);
        Prim(PrimitiveType.Cube, "Cloth", c, V(0, 1.12f, 0), V(1.1f, 0.05f, 3.1f), Lit(P + "cloth", C("#f4eefc"), 0.2f));
        for (int i = 0; i < 4; i++) Prim(PrimitiveType.Sphere, "Lamp", c, V(0, 1.3f, -1.1f + i * 0.75f), V(0.26f, 0.26f, 0.26f), i % 2 == 0 ? pinkGlow : cyanGlow, null, false);
        BlockLine(blocks, V(10.6f, 0, 4.8f), V(10.6f, 0, 7.6f), 0.8f);
        // uplights on the floor round the crowd
        var up = new MB();
        for (int i = 0; i < 8; i++) { up.Gem(V(-7.2f, 0.12f, 2.6f + i * 0.9f), 0.14f, 0.1f, 0.14f); up.Gem(V(7.2f, 0.12f, 2.6f + i * 0.9f), 0.14f, 0.1f, 0.14f); }
        MeshObject("Uplights", t, up.ToMesh("ar_uplights"), lilacGlow, false);
        MakeFloorDressing(t, blocks);
        // way out: a lit doorway frame behind the exit ring
        Prim(PrimitiveType.Cube, "ExitLintel", t, V(11.5f, 3.4f, -12.9f), V(3.2f, 0.25f, 0.4f), cyanGlow, null, false);
        foreach (float sx in new[] { 9.9f, 13.1f }) { Prim(PrimitiveType.Cube, "ExitPost", t, V(sx, 1.7f, -12.9f), V(0.25f, 3.4f, 0.4f), cyanGlow, null, false); Block(blocks, sx, -12.9f, 0.4f); }
    }

    // ---------- the near floor: cables, spike marks, a camera dolly (phase 8.3 look: the foreground was an empty grid) ----------

    static void MakeFloorDressing(Transform t, Transform blocks)
    {
        var rubber = Lit(P + "cable", C("#0e0f24"), 0.35f);
        var tape = Lit(P + "tape", C("#d9c24a"), 0.3f);
        // two cable runs wandering from the flight cases toward the sound desk and the tunnel, with yellow cable ramps across them
        var cab = new MB(); var ramp = new MB();
        Vector3[][] runs =
        {
            new[] { V(-11.4f, 0.05f, -8.6f), V(-9.6f, 0.05f, -7.6f), V(-7.6f, 0.05f, -7.9f), V(-5.6f, 0.05f, -9.4f), V(-4.7f, 0.05f, -11.4f), V(-4.9f, 0.05f, -13.0f) },
            new[] { V(-11.4f, 0.05f, -8.2f), V(-9.5f, 0.05f, -7.2f), V(-7.5f, 0.05f, -7.5f), V(-5.2f, 0.05f, -9.0f), V(-4.2f, 0.05f, -11.0f), V(-4.3f, 0.05f, -13.0f) },
            new[] { V(11.6f, 0.05f, -7.0f), V(9.4f, 0.05f, -8.6f), V(7.4f, 0.05f, -10.2f), V(5.6f, 0.05f, -10.4f), V(4.4f, 0.05f, -12.0f), V(4.6f, 0.05f, -13.0f) },
        };
        foreach (var run in runs)
            for (int i = 0; i + 1 < run.Length; i++)
            {
                Vector3 a = run[i], b = run[i + 1], d = b - a;
                float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                cab.Box((a + b) * 0.5f, V(0.05f, 0.05f, d.magnitude * 0.5f + 0.04f), yaw);
            }
        MeshObject("Cables", t, cab.ToMesh("ar_cables"), rubber, true);
        foreach (var at in new[] { V(-5.6f, 0.06f, -9.4f), V(-8.0f, 0.06f, -7.8f), V(5.7f, 0.06f, -10.4f) })
            ramp.Box(at, V(0.55f, 0.06f, 0.28f), at.x < 0f ? 35f : -20f);
        MeshObject("CableRamps", t, ramp.ToMesh("ar_cable_ramps"), tape, false);
        // spike marks: taped crosses where the band stands (flat, cyan and pink)
        var cross = new MB(); var crossB = new MB();
        foreach (var at in new[] { V(-7.2f, 0.04f, -11f), V(-11.6f, 0.04f, -4.0f), V(4.6f, 0.04f, -7.6f), V(9.4f, 0.04f, -12.2f), V(-6.2f, 0.04f, -4.4f) })
        {
            var m = at.x < -8f || at.z > -6f ? crossB : cross;
            m.Box(at, V(0.4f, 0.015f, 0.05f), 45f); m.Box(at, V(0.4f, 0.015f, 0.05f), -45f);
        }
        MeshObject("SpikeMarks", t, cross.ToMesh("ar_spike_a"), cyanGlow, false);
        MeshObject("SpikeMarksPink", t, crossB.ToMesh("ar_spike_b"), pinkGlow, false);
        // painted rings on the floor
        foreach (var at in new[] { V(9.8f, 0.03f, -2.4f), V(-9.8f, 0.03f, -4.2f) })
        {
            Prim(PrimitiveType.Cylinder, "FloorRingEdge", t, at, V(3.0f, 0.005f, 3.0f), lilacGlow, null, false);
            Prim(PrimitiveType.Cylinder, "FloorRing", t, at + V(0, 0.02f, 0), V(2.84f, 0.005f, 2.84f), Lit(P + "ring_dark", C("#14163a"), 0.5f), null, false);
        }
        // a camera dolly on two rails: a low cart with a camera on a post, a rail light
        var g = Group("CameraDolly", t).transform; g.position = V(-8.3f, 0, -10.0f); g.rotation = Quaternion.Euler(0, 10f, 0);
        foreach (float rx in new[] { -0.45f, 0.45f })
        {
            Prim(PrimitiveType.Cube, "Rail", g, V(rx, 0.05f, 0), V(0.06f, 0.07f, 5.2f), steel, null, false);
            for (int i = 0; i < 9; i++) Prim(PrimitiveType.Cube, "Sleeper", g, V(0, 0.025f, -2.4f + i * 0.6f), V(1.1f, 0.05f, 0.1f), black, null, false);
        }
        Prim(PrimitiveType.Cube, "Cart", g, V(0, 0.28f, 0.4f), V(1.0f, 0.18f, 1.1f), steel);
        foreach (float wx in new[] { -0.45f, 0.45f }) foreach (float wz in new[] { -0.05f, 0.85f })
            Prim(PrimitiveType.Cylinder, "Wheel", g, V(wx, 0.14f, wz), V(0.22f, 0.04f, 0.22f), black, V(0, 0, 90f));
        Prim(PrimitiveType.Cylinder, "Post", g, V(0, 0.95f, 0.4f), V(0.07f, 0.65f, 0.07f), steel);
        Prim(PrimitiveType.Cube, "CameraBody", g, V(0, 1.68f, 0.4f), V(0.38f, 0.32f, 0.62f), black);
        Prim(PrimitiveType.Cylinder, "Lens", g, V(0, 1.68f, 0.86f), V(0.22f, 0.12f, 0.22f), dark, V(90f, 0, 0));
        Prim(PrimitiveType.Sphere, "RecordLight", g, V(0.1f, 1.88f, 0.28f), V(0.06f, 0.06f, 0.06f), pinkGlow, null, false);
        Block(blocks, -8.1f, -9.6f, 0.75f);
    }


    // ---------- stars, sparks, haze ----------

    static void MakeSky(Transform world)
    {
        var rng = new System.Random(5);
        var stars = new MB();
        for (int i = 0; i < 220; i++)
        {
            float az = (float)rng.NextDouble() * Mathf.PI * 2f, el = 0.12f + (float)rng.NextDouble() * 1.1f;
            float r = 95f + (float)rng.NextDouble() * 10f;
            Vector3 p = V(Mathf.Cos(az) * Mathf.Cos(el) * r, Mathf.Sin(el) * r, Mathf.Sin(az) * Mathf.Cos(el) * r);
            float s = 0.25f + (float)rng.NextDouble() * 0.35f;
            stars.Gem(p, s, s, s);
        }
        var go = MeshObject("Stars", world, stars.ToMesh("ar_stars"), Lit(P + "star_dot", C("#ffffff"), 0.2f, new Color(3f, 3f, 3.6f)), false);
        go.transform.position = V(0, 0, 5);
    }

    static void MakeParticles()
    {
        var dot = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/World/Textures/soft_dot.png");
        // haze drifting in the light, and sparks from two fountains at the deck's ends
        var haze = ParticleMaterial("ar_haze", dot, true, new Color(0.9f, 0.8f, 1.6f, 0.35f));
        Drift("Haze", V(0, 4f, 4f), V(24f, 7f, 24f), haze, 60, 6f, new Vector2(6f, 10f), new Vector2(0.4f, 1.0f), Color.white, Color.white,
            new Vector3(-0.1f, -0.02f, -0.1f), new Vector3(0.1f, 0.08f, 0.1f), 0f, 0.4f);
        var spark = ParticleMaterial("ar_spark", dot, true, new Color(3f, 2.3f, 1.0f, 1f));
        foreach (float sx in new[] { -7.4f, 7.4f })
        {
            var ps = Drift("SparkFountain", V(sx, 0.8f, 9.4f), V(0.3f, 0.1f, 0.3f), spark, 90, 40f, new Vector2(0.9f, 1.6f), new Vector2(0.06f, 0.14f), C("#ffd36a"), C("#ffffff"),
                new Vector3(-0.6f, 4.2f, -0.4f), new Vector3(0.6f, 6.2f, 0.4f), 1.1f, 0f, 0f, false);
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
        }
        var conf = ParticleMaterial("ar_confetti", null, false, Color.white);
        Drift("Confetti", V(0, 11f, 6f), V(16f, 0.5f, 10f), conf, 70, 8f, new Vector2(5f, 8f), new Vector2(0.1f, 0.17f), C("#ff5fa8"), C("#4ae0ff"),
            new Vector3(-0.2f, -1.0f, -0.1f), new Vector3(0.2f, -0.6f, 0.1f), 0f, 0.5f, 3f);
    }
}
