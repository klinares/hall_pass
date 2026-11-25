// Phase 8.2 builder A: Place_Rooftop, "the rooftop party, sunset". A wooden roof deck with a low parapet, a lounge of couches and rugs, a bar cart, string
// lights between poles, a water tower, an access hut (the way out), and a stage on the north side that opens onto the sunset: no back wall, only the skyline
// of dark towers with lit windows fading into an orange-pink haze, and a low sun disc.
//
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -executeMethod PlaceRooftopBuilder.CreateAll
//
// Layout (x right, z away from the camera, walkers clamped to +-13.5): lounge and spawn in the south, the bar and water tower on the west, the stage at the north-east
// (x 1 .. 10, z 8.8 .. 13.2), the access hut in the south-east corner with the exit ring in front of it.
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using WorldTour.World;
using static PlaceKit;

public static class PlaceRooftopBuilder
{
    public const string ScenePath = "Assets/World/Scenes/Place_Rooftop.unity";
    const string P = "rf_";

    public static void CreateAll() { Entry("PlaceRooftopBuilder", Make); }

    static Material M(string n, string hex, float s = 0.15f, Color? e = null) { return Lit(P + n, C(hex), s, e); }

    public static void Make()
    {
        var scene = NewScene();
        InitKits(P, new Color(1.0f, 0.9f, 0.86f), false);
        var look = Looks.Rooftop();
        var profile = MakeProfile(look);
        var world = Group("World").transform;
        var roof = Group("Roof", world).transform;
        var lounge = Group("Lounge", world).transform;
        var stage = Group("Stage", world).transform;
        var sky = Group("Skyline", world).transform;
        var props = Group("Props", world).transform;
        var blocks = Group("Blockers", world).transform;

        MakeRoof(roof, blocks);
        MakeLounge(lounge, blocks);
        MakeBarAndTower(props, blocks);
        MakeHut(props, blocks);
        MakeStage(stage, blocks);
        MakeSkyline(sky);
        MakeLights(world);

        var contract = new Contract
        {
            PlaceId = "rooftop", RouteIndex = 3,
            Spawn = V(0, 0, -8.2f),
            StageSpot = V(5.5f, 0f, 10.2f),
            CrowdCenter = V(5.5f, 0f, 5.7f), CrowdSize = new Vector2(8.2f, 3.2f),
            Exit = V(10.4f, 0f, -8.0f), ExitArrowYaw = 270f,
            Crew = new[] { V(-5.8f, 0f, -2.6f), V(-7.6f, 0f, 5.6f), V(-1.2f, 0f, 2.6f) },
            Dog = V(-1.8f, 0f, -9.8f),
        };
        Populate(contract, look, profile, blocks);
        FinishLook(look);
        Motes("Embers", V(0f, 4f, 2f), V(30f, 8f, 30f), new Color(1.8f, 1.1f, 0.55f, 0.7f), Color.white, 70, 8f, new Vector2(0.08f, 0.2f), new Vector2(6f, 10f), 0.9f, 0.35f, "rf_ember_particle");
        // the sun: a big warm disc low over the skyline in the north-west (unlit so the fog does not grey it)
        var sunMat = LoadOrNew("rf_sun_disc", "Universal Render Pipeline/Unlit");
        sunMat.SetColor("_BaseColor", new Color(1f, 0.62f, 0.3f));
        EditorUtility.SetDirty(sunMat);
        var sun = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sun.name = "SunDisc"; Object.DestroyImmediate(sun.GetComponent<Collider>());
        sun.transform.position = V(-36f, 13f, 62f); sun.transform.localScale = V(11f, 11f, 11f);
        var sr = sun.GetComponent<Renderer>(); sr.sharedMaterial = sunMat; sr.shadowCastingMode = ShadowCastingMode.Off; sr.receiveShadows = false;
        SaveScene(scene, ScenePath);
    }

    // ---------- the roof ----------

    static void MakeRoof(Transform t, Transform blocks)
    {
        var concrete = PavingTexture("rf_concrete", "#c9b3a2", "#bda696", "#a08a7c", "#d9b49a", "#e6c7b2", 51, false);
        PavedPlane("RoofSurface", t, P + "concrete_mat", concrete, V(0, -0.02f, 0), new Vector2(30f, 30f), null, 0.08f);
        // the wooden deck over the lounge and under the stage
        var planks = PatternTexture("rf_planks", "planks", "#a8764c", "#94653f", "#5e3c24", 12);
        var plankMat = PatternMat(P + "planks_mat", planks, null, 0.3f);
        TexBox(t, "Deck", V(-1.5f, -0.05f, -4f), V(20f, 0.1f, 16f), plankMat, 0.25f, null, false);
        TexBox(t, "StageDeck", V(5.5f, -0.05f, 9f), V(14f, 0.1f, 8f), plankMat, 0.25f, null, false);
        // the parapet around the roof, cream stucco with a darker cap, seen from inside
        var stucco = M("stucco", "#f0b890", 0.12f);
        var cap = M("cap", "#d98c6a", 0.2f);
        const float e = 14.4f, h = 1.1f;
        Prim(PrimitiveType.Cube, "ParapetW", t, V(-e, h / 2f, 0), V(0.5f, h, 2f * e + 0.5f), stucco);
        Prim(PrimitiveType.Cube, "ParapetE", t, V(e, h / 2f, 0), V(0.5f, h, 2f * e + 0.5f), stucco);
        Prim(PrimitiveType.Cube, "ParapetN", t, V(0, h / 2f, e), V(2f * e + 0.5f, h, 0.5f), stucco);
        Prim(PrimitiveType.Cube, "ParapetS", t, V(0, h / 2f, -e), V(2f * e + 0.5f, h, 0.5f), stucco);
        Prim(PrimitiveType.Cube, "CapW", t, V(-e, h + 0.06f, 0), V(0.8f, 0.12f, 2f * e + 0.9f), cap);
        Prim(PrimitiveType.Cube, "CapE", t, V(e, h + 0.06f, 0), V(0.8f, 0.12f, 2f * e + 0.9f), cap);
        Prim(PrimitiveType.Cube, "CapN", t, V(0, h + 0.06f, e), V(2f * e + 0.9f, 0.12f, 0.8f), cap);
        Prim(PrimitiveType.Cube, "CapS", t, V(0, h + 0.06f, -e), V(2f * e + 0.9f, 0.12f, 0.8f), cap);
        // lanterns on the parapet corners and a few along it
        var lantern = M("lantern", "#fff0c8", 0.3f, new Color(3.2f, 2.2f, 1.0f));
        for (int i = 0; i < 7; i++)
        {
            float z = -12f + i * 4f;
            Prim(PrimitiveType.Sphere, "Lantern", t, V(-e + 0.25f, h + 0.4f, z), V(0.34f, 0.4f, 0.34f), lantern, null, false);
            Prim(PrimitiveType.Sphere, "Lantern", t, V(e - 0.25f, h + 0.4f, z), V(0.34f, 0.4f, 0.34f), lantern, null, false);
        }
        // planters with bushes along the parapet (a few), and cacti
        var green = ForestTinted("rooftop_green", "#9ac96a", "#5b8c4a");
        var terracotta = M("terracotta", "#c9744a", 0.2f);
        var spots = new[] { (-12.6f, -11f), (-12.6f, 0.5f), (12.6f, -2.5f), (12.6f, 8f), (-4f, 13.0f), (-9f, 13.0f), (11.5f, 13.0f) };
        for (int i = 0; i < spots.Length; i++)
        {
            Prim(PrimitiveType.Cube, "Planter", t, V(spots[i].Item1, 0.32f, spots[i].Item2), V(1.6f, 0.64f, 0.8f), terracotta);
            Fbx(Forest + (i % 2 == 0 ? "Bush_2_A_Color1.fbx" : "Bush_4_A_Color1.fbx"), t, V(spots[i].Item1, 0.64f, spots[i].Item2), i * 53f, 1.4f, green, "PlanterBush");
            Block(blocks, spots[i].Item1, spots[i].Item2, 0.9f);
        }
        // the walls of the roof are a fence for the walkers (they are clamped to 13.5 anyway)
    }

    // ---------- the lounge ----------

    static void MakeLounge(Transform t, Transform blocks)
    {
        // rugs, couches round low tables, standing lamps
        void Lounge(Vector3 c, float yaw, string rugHex)
        {
            var g = Group("LoungeSet", t).transform; g.position = c; g.rotation = Quaternion.Euler(0, yaw, 0);
            TexBox(g, "Rug", V(0, 0.015f, 0), V(4.6f, 0.02f, 3.4f), M("rug_" + rugHex.TrimStart('#'), rugHex, 0.05f), 1f, null, false);
            Prim(PrimitiveType.Cube, "RugBorder", g, V(0, 0.026f, 0), V(4.0f, 0.01f, 2.8f), M("rug_border", "#fff1dc", 0.05f), null, false);
            Fbx(Furn + "couch.fbx", g, V(0, 0.02f, 1.3f), 180f, 1.35f, furnMat, "Couch");
            Fbx(Furn + "armchair.fbx", g, V(-1.9f, 0.02f, -0.2f), 90f, 1.2f, furnMat, "Armchair");
            Fbx(Furn + "armchair.fbx", g, V(1.9f, 0.02f, -0.2f), -90f, 1.2f, furnMat, "Armchair");
            Fbx(Furn + "table_low.fbx", g, V(0, 0.02f, 0f), 0f, 1.3f, furnMat, "LowTable");
            Prim(PrimitiveType.Cylinder, "Candle", g, V(0.2f, 0.62f, 0.05f), V(0.14f, 0.1f, 0.14f), M("candle", "#fff0c8", 0.3f, new Color(2.8f, 1.9f, 0.8f)), null, false);
            Prim(PrimitiveType.Cylinder, "Cup", g, V(-0.3f, 0.62f, -0.1f), V(0.14f, 0.08f, 0.14f), M("cup", "#ff7aa8", 0.4f), null, false);
        }
        Lounge(V(-4.6f, 0f, -9.4f), 0f, "#e8503a");
        Lounge(V(5.2f, 0f, -3.4f), 0f, "#1f8f8a");
        Lounge(V(-5.8f, 0f, 1.2f), 90f, "#a0527a");
        foreach (var p in new[] { (-4.6f, -9.4f), (5.2f, -3.4f), (-5.8f, 1.2f) }) { Block(blocks, p.Item1, p.Item2 + 0.9f, 1.3f); Block(blocks, p.Item1 - 1.5f, p.Item2, 0.7f); Block(blocks, p.Item1 + 1.5f, p.Item2, 0.7f); }
        // the central rug where she starts, a runner toward the stage
        TexBox(t, "Runner", V(0f, 0.015f, -4.2f), V(2.4f, 0.02f, 10f), M("runner", "#ffd27a", 0.05f), 1f, null, false);
        Prim(PrimitiveType.Cube, "RunnerStripe", t, V(0f, 0.026f, -4.2f), V(0.3f, 0.01f, 10f), M("runner_stripe", "#e8503a", 0.05f), null, false);
        // standing lamps
        foreach (var p in new[] { (-8.2f, -8.0f), (8.2f, -9.4f), (-8.4f, 3.6f) })
        {
            Fbx(Furn + "lamp_standing.fbx", t, V(p.Item1, 0.1f, p.Item2), 0f, 1.4f, furnMat, "Lamp");
            Block(blocks, p.Item1, p.Item2, 0.35f);
        }
        // cacti
        Fbx(Furn + "cactus_medium_A.fbx", t, V(1.9f, 0.02f, -10.4f), 20f, 1.2f, furnMat, "Cactus"); Block(blocks, 1.9f, -10.4f, 0.4f);
        Fbx(Furn + "cactus_medium_A.fbx", t, V(-9.6f, 0.02f, -4.6f), 140f, 1.2f, furnMat, "Cactus"); Block(blocks, -9.6f, -4.6f, 0.4f);
    }

    // ---------- the bar cart, the water tower, the aunt ----------

    static void MakeBarAndTower(Transform t, Transform blocks)
    {
        var wood = M("wood", "#9c6a46", 0.3f);
        var teal = M("teal", "#1f8f8a", 0.25f);
        var steel = M("steel", "#c9ccdf", 0.6f);
        // the juice stand: a counter with a coloured front, soda cans, a row of cups, a cooler, two stools
        var bar = Group("JuiceStand", t).transform; bar.position = V(-9.4f, 0f, -2.0f); bar.rotation = Quaternion.Euler(0, 90f, 0);
        Prim(PrimitiveType.Cube, "Counter", bar, V(0, 0.55f, 0), V(3.6f, 1.1f, 0.8f), teal);
        Prim(PrimitiveType.Cube, "CounterTop", bar, V(0, 1.14f, 0), V(3.8f, 0.08f, 1.0f), wood);
        string[] bottle = { "#ff7aa8", "#ffd27a", "#8fd0ff", "#c9a0ff", "#9aeaa0" };
        for (int i = 0; i < 8; i++)
        {
            Prim(PrimitiveType.Cylinder, "SodaCan", bar, V(-1.5f + i * 0.42f, 1.3f, 0.1f), V(0.17f, 0.1f, 0.17f), M("can" + (i % 5), bottle[i % 5], 0.7f));
            if (i % 2 == 0) Prim(PrimitiveType.Cylinder, "Cup", bar, V(-1.3f + i * 0.4f, 1.24f, -0.25f), V(0.16f, 0.08f, 0.16f), M("cup", "#ff7aa8", 0.4f), null, false);
        }
        Prim(PrimitiveType.Cube, "Cooler", bar, V(1.2f, 0.3f, 0.9f), V(0.9f, 0.6f, 0.55f), M("cooler", "#e8503a", 0.3f));
        Prim(PrimitiveType.Cube, "CoolerLid", bar, V(1.2f, 0.63f, 0.9f), V(0.95f, 0.06f, 0.6f), M("white", "#fffaf0", 0.3f));
        foreach (float sx in new[] { -0.8f, 0.4f })
        {
            Prim(PrimitiveType.Cylinder, "StoolLeg", bar, V(sx, 0.36f, -0.9f), V(0.06f, 0.36f, 0.06f), steel);
            Prim(PrimitiveType.Cylinder, "Stool", bar, V(sx, 0.74f, -0.9f), V(0.42f, 0.05f, 0.42f), M("stool", "#ffd27a", 0.3f));
        }
        // a string of mini pendants over the juice stand
        Prim(PrimitiveType.Cube, "BarCanopy", bar, V(0, 2.9f, 0), V(3.8f, 0.08f, 1.3f), M("canopy", "#fff1dc", 0.2f));
        foreach (float px in new[] { -1.8f, 1.8f }) foreach (float pz in new[] { -0.6f, 0.6f }) Prim(PrimitiveType.Cylinder, "CanopyPost", bar, V(px, 1.45f, pz), V(0.07f, 1.45f, 0.07f), wood);
        BlockLine(blocks, new Vector2(-9.4f, -3.8f), new Vector2(-9.4f, -0.2f), 0.9f, 1.1f);

        // the water tower, the landmark: the CC0 city kit's tower, enlarged, with a warm side light and a glowing sign ring
        var tower = Fbx(City + "watertower.fbx", t, V(-10.6f, 0f, 9.4f), 20f, 2.6f, cityMat, "WaterTower");
        tower.AddComponent<Occluder>();
        foreach (var p in new[] { (-11.8f, 8.2f), (-9.4f, 8.2f), (-11.8f, 10.6f), (-9.4f, 10.6f) }) Block(blocks, p.Item1, p.Item2, 0.5f);
        Block(blocks, -10.6f, 9.4f, 1.2f);

        // the access hut's neighbour: a few crates and a hose reel
        Fbx(Resto + "crate.fbx", t, V(11.8f, 0f, -4.4f), 20f, 1.1f, restoMat, "Crate"); Block(blocks, 11.8f, -4.4f, 0.6f);
        Fbx(Resto + "crate.fbx", t, V(12.3f, 0.0f, -3.4f), -30f, 0.9f, restoMat, "Crate");

        // an aunt filming everything: a guest on the lounge side with a camera held up
        var aunt = StaticFigure(t, "Crowd_crowd_mage_pink", V(2.4f, 0f, -1.0f), 25f, 0.9f);
        Prim(PrimitiveType.Cube, "Camcorder", aunt.transform, V(0.0f, 1.55f, 0.35f), V(0.22f, 0.16f, 0.3f), M("cam", "#26262e", 0.3f));
        Prim(PrimitiveType.Sphere, "CamLens", aunt.transform, V(0.0f, 1.55f, 0.52f), V(0.1f, 0.1f, 0.06f), M("cam_lens", "#8fd0ff", 0.8f));
        Prim(PrimitiveType.Sphere, "CamLight", aunt.transform, V(0.1f, 1.66f, 0.4f), V(0.05f, 0.05f, 0.05f), M("rec_led", "#ff4a4a", 0.3f, new Color(3f, 0.3f, 0.3f)), null, false);
        Block(blocks, 2.4f, -1.0f, 0.45f);
    }

    // ---------- the access hut ----------

    static void MakeHut(Transform t, Transform blocks)
    {
        var wall = M("hut", "#f3cba6", 0.12f);
        var trim = M("hut_trim", "#8c4a3a", 0.25f);
        var door = M("hut_door", "#5a3a52", 0.2f);
        var glow = M("hut_glow", "#fff3c4", 0.2f, new Color(2.6f, 2.1f, 1.2f));
        var hut = Group("AccessHut", t).transform; hut.position = V(10.6f, 0f, -11.8f);
        Prim(PrimitiveType.Cube, "HutBody", hut, V(0, 1.7f, 0), V(4.4f, 3.4f, 2.8f), wall);
        Prim(PrimitiveType.Cube, "HutRoof", hut, V(0, 3.55f, 0), V(4.8f, 0.3f, 3.2f), trim);
        Prim(PrimitiveType.Cube, "Door", hut, V(0, 1.15f, -1.42f), V(1.5f, 2.3f, 0.1f), door);
        Prim(PrimitiveType.Cube, "DoorFrame", hut, V(0, 1.15f, -1.4f), V(1.76f, 2.5f, 0.08f), trim);
        Prim(PrimitiveType.Sphere, "DoorLamp", hut, V(0, 2.8f, -1.55f), V(0.34f, 0.34f, 0.34f), glow, null, false);
        Prim(PrimitiveType.Cube, "HutWindow", hut, V(1.4f, 2.0f, -1.42f), V(0.8f, 0.7f, 0.06f), glow, null, false);
        // an antenna on the roof with a blinking light
        Prim(PrimitiveType.Cylinder, "Antenna", hut, V(1.5f, 5.0f, 0.4f), V(0.07f, 1.5f, 0.07f), trim);
        var blink = Prim(PrimitiveType.Sphere, "AntennaLight", hut, V(1.5f, 6.55f, 0.4f), V(0.24f, 0.24f, 0.24f), M("beacon", "#ff4a4a", 0.3f, new Color(3.5f, 0.3f, 0.3f)), null, false);
        MakeGlow(blink, new Color(3.5f, 0.3f, 0.3f), 0f, 0.3f, 1.4f);
        Block(blocks, 10.6f, -11.8f, 1.4f); Block(blocks, 8.8f, -11.8f, 0.8f); Block(blocks, 12.4f, -11.8f, 0.8f);
    }

    // ---------- the stage ----------

    static void MakeStage(Transform t, Transform blocks)
    {
        Stage(t, blocks, new StageSpec
        {
            Prefix = P, Cx = 5.5f, Cz = 11.0f, Width = 9f, Depth = 4.4f, Height = 0.4f, RampLength = 0.7f,
            Deck = "#4a2d52", Trim = "#ff7aa8", TrimGlow = new Color(0.9f, 0.25f, 0.45f), Wall = "#4a2d52", Dark = "#2a1a3c", Truss = "#d8b0c0", Ring = "#ffd27a",
            BackWall = false, BannerStars = false, TrussHeight = 4.6f, SpotHeight = 4.3f, BeamLength = 5.4f, BeamRadius = 1.1f, SpeakerOffset = 3.9f,
            Lens = new[] { new Color(3f, 1.0f, 0.5f), new Color(3f, 0.6f, 1.6f), new Color(2.8f, 2.2f, 0.7f), new Color(3f, 0.6f, 1.6f), new Color(3f, 1.0f, 0.5f), new Color(1.6f, 1.2f, 3.2f) },
            SpotStart = 1.3f, SpotSpacing = 1.56f,
        });
        // a string-light canopy over the stage and a few paper stars hanging from the beam
        var wire = new MB(); var bulbs = new MB();
        StringLine(wire, bulbs, V(1.2f, 4.5f, 9.2f), V(5.5f, 4.9f, 9.2f), 0.5f, 0.55f, 0.1f);
        StringLine(wire, bulbs, V(5.5f, 4.9f, 9.2f), V(9.8f, 4.5f, 9.2f), 0.5f, 0.55f, 0.1f);
        StringLine(wire, bulbs, V(1.2f, 4.5f, 9.2f), V(1.2f, 4.5f, 13.0f), 0.4f, 0.55f, 0.1f);
        StringLine(wire, bulbs, V(9.8f, 4.5f, 9.2f), V(9.8f, 4.5f, 13.0f), 0.4f, 0.55f, 0.1f);
        StringLine(wire, bulbs, V(1.2f, 4.5f, 13.0f), V(9.8f, 4.5f, 13.0f), 0.8f, 0.55f, 0.1f);
        EmitStrings(t, wire, bulbs, "rf_stage", C("#fff0c8"), new Color(3.2f, 2.2f, 1.0f));
        var gold = Lit("rf_star_gold", C("#ffd27a"), 0.3f, new Color(1.6f, 1.1f, 0.4f));
        StarSign("PaperStar", t, V(3.1f, 3.9f, 9.4f), 0.7f, gold, 180f);
        StarSign("PaperStar", t, V(5.5f, 4.0f, 9.4f), 1.0f, Lit("rf_star_pink", C("#ff7aa8"), 0.3f, new Color(1.2f, 0.3f, 0.5f)), 180f);
        StarSign("PaperStar", t, V(7.9f, 3.9f, 9.4f), 0.7f, gold, 180f);
        // a DJ-style table and a keyboard at the back, a drum kit
        DrumKit(t, V(5.5f, 0.4f, 12.0f), 0f, P, "#ff7aa8");
        AmpStack(t, V(2.2f, 0.4f, 12.3f), 18f, P, 1.0f, "#e8d4a0");
        AmpStack(t, V(8.8f, 0.4f, 12.3f), -18f, P, 1.0f, "#e8d4a0");
        Block(blocks, 2.2f, 12.3f, 0.8f); Block(blocks, 8.8f, 12.3f, 0.8f);
        // the parapet behind the stage needs a block line so nobody walks onto it
        for (float x = 0.5f; x <= 11f; x += 1.4f) Block(blocks, x, 13.9f, 0.6f);
    }

    // ---------- the skyline ----------

    static void MakeSkyline(Transform t)
    {
        var far = WindowTowerMaterial("rf_tower_far", "#5a3a68", "#6a4a7a", "#ffc873", 0.28f, 5, 1.8f);
        var near = WindowTowerMaterial("rf_tower_near", "#3a2548", "#4a3258", "#ffb050", 0.32f, 9, 2.2f);
        var rng = new System.Random(4);
        // a ring of towers beyond the parapet: north, then the two sides, tops from just above the horizon up to a tall few
        void Ring(Material m, int count, float x0, float x1, float z, float minTop, float maxTop, float zJitter)
        {
            for (int i = 0; i < count; i++)
            {
                float x = Mathf.Lerp(x0, x1, count == 1 ? 0.5f : i / (float)(count - 1)) + ((float)rng.NextDouble() - 0.5f) * 3f;
                float w = 6f + (float)rng.NextDouble() * 8f, d = 6f + (float)rng.NextDouble() * 6f;
                float top = Mathf.Lerp(minTop, maxTop, (float)rng.NextDouble());
                float zz = z + ((float)rng.NextDouble() - 0.5f) * zJitter;
                Tower(t, "Tower", V(x, -40f, zz), V(Mathf.Round(w / 4f) * 4f, top + 40f, Mathf.Round(d / 4f) * 4f), m);
            }
        }
        Ring(near, 9, -70f, 70f, 46f, 2f, 16f, 8f);
        Ring(far, 11, -110f, 110f, 90f, 8f, 44f, 20f);
        // the sides: neighbours a little lower than this roof (their tops are below it), and further ones
        for (int i = 0; i < 6; i++)
        {
            float z = -40f + i * 17f;
            Tower(t, "TowerW", V(-27f - (float)rng.NextDouble() * 4f, -40f, z), V(12f, 40f + (float)(rng.NextDouble() * 6f - 5f), 12f), near);
            Tower(t, "TowerE", V(27f + (float)rng.NextDouble() * 4f, -40f, z), V(12f, 40f + (float)(rng.NextDouble() * 6f - 5f), 12f), near);
        }
        for (int i = 0; i < 6; i++)
        {
            float z = -60f + i * 28f;
            Tower(t, "TowerWFar", V(-60f, -40f, z), V(12f, 40f + 14f + (float)rng.NextDouble() * 28f, 16f), far);
            Tower(t, "TowerEFar", V(60f, -40f, z), V(12f, 40f + 14f + (float)rng.NextDouble() * 28f, 16f), far);
        }
        // and behind the camera, so the fog has something to melt into when the view swings
        for (int i = 0; i < 6; i++) Tower(t, "TowerS", V(-60f + i * 24f, -40f, -60f), V(12f, 40f + 8f + (float)rng.NextDouble() * 24f, 12f), far);
        // rooftop clutter on the near neighbours (water tanks), so their tops are not bare slabs
        var tank = M("tank", "#6a4a52", 0.2f);
        foreach (var p in new[] { (-27f, -6f), (27f, 11f), (-28f, 28f), (27f, -23f) })
        {
            Prim(PrimitiveType.Cylinder, "RoofTank", t, V(p.Item1, 1.4f, p.Item2), V(3f, 1.4f, 3f), tank);
            Prim(PrimitiveType.Cylinder, "RoofTankLid", t, V(p.Item1, 2.9f, p.Item2), V(3.2f, 0.12f, 3.2f), M("tank_lid", "#8a5a62", 0.2f));
        }
    }

    // ---------- string lights ----------

    static void MakeLights(Transform world)
    {
        var t = Group("Lights", world).transform;
        var wire = new MB(); var bulbs = new MB();
        // two spans across the roof between pole pairs, and a long swag toward the bar
        StringLine(wire, bulbs, V(-12.4f, 3.9f, -3.6f), V(12.4f, 3.9f, -3.6f), 1.5f, 0.75f, 0.1f);
        StringLine(wire, bulbs, V(-12.4f, 3.9f, 2.6f), V(-3f, 3.9f, 2.6f), 1.0f, 0.75f, 0.1f);
        StringLine(wire, bulbs, V(-12.4f, 3.9f, -3.6f), V(-12.4f, 3.9f, 2.6f), 0.6f, 0.75f, 0.1f);
        StringLine(wire, bulbs, V(12.4f, 3.9f, -3.6f), V(12.4f, 3.9f, -11.6f), 1.0f, 0.75f, 0.1f);
        StringLine(wire, bulbs, V(-12.4f, 3.9f, -3.6f), V(-12.4f, 3.9f, -11.6f), 1.0f, 0.75f, 0.1f);
        StringLine(wire, bulbs, V(-12.4f, 3.9f, -11.6f), V(12.4f, 3.9f, -11.6f), 1.4f, 0.75f, 0.1f);
        EmitStrings(t, wire, bulbs, "rf", C("#fff0c8"), new Color(3.4f, 2.3f, 1.0f));
        var pole = M("pole", "#6e4428", 0.2f);
        foreach (var p in new[] { (-12.4f, -3.6f), (12.4f, -3.6f), (-12.4f, 2.6f), (-12.4f, -11.6f), (12.4f, -11.6f) })
            Prim(PrimitiveType.Cylinder, "LightPole", t, V(p.Item1, 1.95f, p.Item2), V(0.14f, 1.95f, 0.14f), pole).AddComponent<Occluder>();
        // round 8.4: the paper lanterns that hung on the long span are gone: from the follow camera they floated as white balls over the crew's heads
    }
}
