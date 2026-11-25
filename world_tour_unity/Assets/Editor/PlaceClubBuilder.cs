// Phase 8.2 builder A: Place_Club, "the fuzz, downtown, evening". A neon-lit street at dusk: wet dark asphalt with glowing reflections, three shopfronts with neon
// signs on the west, a queue of fans behind velvet ropes, and the club on the north-east as a cutaway: dark checker floor, a real stage with a truss, coloured beams and
// speaker stacks, the neon FUZZ sign across the back wall, and a disco ball. Every sign says only generic words (PIZZA, OPEN, THE FUZZ).
//
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -executeMethod PlaceClubBuilder.CreateAll
//
// Layout (x right, z away from the camera, walkers clamped to +-13.5): south sidewalk (spawn) -> road with a crossing -> north sidewalk with the queue along the
// shops (x -14 .. 0.4, fronts at z = 3.6) and the club room east of them (x 0.8 .. 14.4, z 3.6 .. 14.2), stage at the north end.
using UnityEditor;
using UnityEngine;
using WorldTour.World;
using static PlaceKit;

public static class PlaceClubBuilder
{
    public const string ScenePath = "Assets/World/Scenes/Place_Club.unity";
    const string P = "cb_";

    public static void CreateAll() { Entry("PlaceClubBuilder", Make); }

    static Material M(string n, string hex, float s = 0.15f, Color? e = null) { return Lit(P + n, C(hex), s, e); }

    public static void Make()
    {
        var scene = NewScene();
        InitKits(P, new Color(0.55f, 0.5f, 0.85f), false);
        var look = Looks.Club();
        var profile = MakeProfile(look);
        var world = Group("World").transform;
        var ground = Group("Ground", world).transform;
        var shops = Group("Shops", world).transform;
        var club = Group("Club", world).transform;
        var stage = Group("Stage", world).transform;
        var queue = Group("Queue", world).transform;
        var sky = Group("Skyline", world).transform;
        var props = Group("Props", world).transform;
        var blocks = Group("Blockers", world).transform;

        MakeGround(ground);
        MakeShops(shops, blocks);
        MakeClub(club, blocks);
        MakeStage(stage, blocks);
        MakeQueue(queue, blocks);
        MakeStreetDressing(props, blocks);
        MakeSkyline(sky);

        var contract = new Contract
        {
            PlaceId = "club", RouteIndex = 4,
            Spawn = V(0, 0, -8.6f),
            StageSpot = V(7.8f, 0f, 10.2f),
            CrowdCenter = V(7.8f, 0f, 5.6f), CrowdSize = new Vector2(7.4f, 3.2f),
            Exit = V(11.4f, 0f, -10.2f), ExitArrowYaw = 0f,
            Crew = new[] { V(-5.6f, 0f, -0.6f), V(3.4f, 0f, -0.2f), V(2.2f, 0f, 6.8f) },
            Dog = V(-1.8f, 0f, -10.2f),
        };
        Populate(contract, look, profile, blocks);
        FinishLook(look);
        // haze in the club (so the beams read) and sparkle drifting up in the street
        Motes("Haze", V(7.6f, 2.2f, 8.6f), V(13f, 3.4f, 9f), new Color(0.9f, 0.6f, 1.2f, 0.16f), Color.white, 26, 3f, new Vector2(1.6f, 3.0f), new Vector2(7f, 11f), 0.5f, 0.1f, "cb_haze_particle");
        Motes("Sparkle", V(0f, 3f, -2f), V(30f, 6f, 24f), new Color(1.5f, 1.0f, 1.8f, 0.6f), Color.white, 50, 6f, new Vector2(0.06f, 0.14f), new Vector2(5f, 9f), 0.8f, 0.2f, "cb_sparkle_particle");
        SaveScene(scene, ScenePath);
    }

    // ---------- ground ----------

    static void MakeGround(Transform t)
    {
        Prim(PrimitiveType.Plane, "Base", t, V(0, -0.03f, 0), V(24, 1, 24), M("base", "#241d38", 0.05f), null, false);
        var walk = PavingTexture("cb_walk", "#5d5776", "#534d6c", "#3d3852", "#7a5a8c", "#8e6ca0", 61, false);
        PavedPlane("SidewalkSouth", t, P + "walk_mat", walk, V(0, 0, -10.4f), new Vector2(28f, 6.2f), null, 0.3f);
        PavedPlane("SidewalkNorth", t, P + "walk_mat", walk, V(0, 0, 0.4f), new Vector2(28f, 6.4f), null, 0.3f);
        // the road: wet dark asphalt (high smoothness so the lights glint)
        Prim(PrimitiveType.Plane, "Road", t, V(0, 0.01f, -5.0f), V(2.8f, 1, 0.44f), M("asphalt", "#221c30", 0.82f), null, false);
        var paint = M("road_paint", "#fff0c8", 0.3f, new Color(0.5f, 0.45f, 0.35f));
        for (int i = 0; i < 9; i++)
        {
            float x = -14.4f + i * 3.6f;
            if (Mathf.Abs(x) < 3f) continue;
            Prim(PrimitiveType.Cube, "Dash", t, V(x, 0.02f, -5.0f), V(1.6f, 0.02f, 0.16f), paint, null, false);
        }
        for (int i = 0; i < 5; i++)
            Prim(PrimitiveType.Cube, "Zebra", t, V(-1.8f + i * 0.9f, 0.02f, -5.0f), V(0.5f, 0.02f, 4.2f), paint, null, false);
        var kerb = M("kerb", "#8a7fa8", 0.3f);
        foreach (float z in new[] { -2.8f, -7.2f })
        {
            Prim(PrimitiveType.Cube, "KerbW", t, V(-9.1f, 0.07f, z), V(13.8f, 0.14f, 0.3f), kerb);
            Prim(PrimitiveType.Cube, "KerbE", t, V(9.1f, 0.07f, z), V(13.8f, 0.14f, 0.3f), kerb);
        }
        // neon reflections on the wet road and the sidewalk: soft additive pools under the signs
        GlowDecal(t, V(7.5f, 0.03f, -3.6f), new Vector2(14f, 5f), "cb_glow_pink", new Color(1.6f, 0.25f, 0.9f, 0.5f));
        GlowDecal(t, V(-8.5f, 0.03f, -3.8f), new Vector2(10f, 4f), "cb_glow_cyan", new Color(0.2f, 1.1f, 1.6f, 0.4f));
        GlowDecal(t, V(-2.0f, 0.03f, -4.6f), new Vector2(7f, 3f), "cb_glow_amber", new Color(1.6f, 0.7f, 0.15f, 0.35f));
        GlowDecal(t, V(2.5f, 0.03f, 1.8f), new Vector2(9f, 3.4f), "cb_glow_pink", new Color(1.6f, 0.25f, 0.9f, 0.5f));
        GlowDecal(t, V(-9.0f, 0.03f, 1.8f), new Vector2(9f, 3f), "cb_glow_red", new Color(1.8f, 0.3f, 0.2f, 0.4f));
        // the club floor: a dark checker
        var checker = PatternTexture("cb_checker", "checker", "#1c1830", "#2c2448", "#3a2f5c", 14);
        TexBox(t, "ClubFloor", V(7.6f, 0.02f, 8.9f), V(13.6f, 0.04f, 10.6f), PatternMat(P + "checker_mat", checker, null, 0.6f), 1f, null, false);
        // a red carpet from the pavement into the club
        Prim(PrimitiveType.Cube, "Carpet", t, V(2.2f, 0.03f, 1.8f), V(1.8f, 0.02f, 5.4f), M("carpet", "#b8223a", 0.15f), null, false);
    }

    // ---------- the shops on the west ----------

    static void MakeShops(Transform t, Transform blocks)
    {
        var upper = WindowTowerMaterial("cb_tower_shop", "#3a3060", "#4a4078", "#ffb36b", 0.3f, 17, 2.4f);
        var dark = M("shop_dark", "#2a2348", 0.3f);
        // three storefronts, 4.6 m wide, fronts at z = 3.6: a lit window, a door, a neon awning edge and a sign
        var shopDefs = new[]
        {
            (cx: -11.8f, word: "PIZZA", neon: "#ff6a3a", glow: new Color(1f, 0.42f, 0.23f), window: "#ffd29a"),
            (cx: -7.0f, word: "OPEN", neon: "#38e8ff", glow: new Color(0.22f, 0.91f, 1f), window: "#a8f0ff"),
            (cx: -2.2f, word: "ARCADE", neon: "#ff4adf", glow: new Color(1f, 0.29f, 0.87f), window: "#ffb0f0"),
        };
        float z0 = 3.6f;
        foreach (var s in shopDefs)
        {
            var g = Group("Shop_" + s.word, t).transform;
            var neon = NeonMat(P + "neon_" + s.word.ToLower(), s.neon, 2.6f);
            var winMat = M("window_" + s.word.ToLower(), s.window, 0.2f, C(s.window) * 1.5f);
            float w = 4.6f;
            // body: ground floor box, upper floors as a window tower
            TexBox(g, "Ground", V(s.cx, 1.8f, z0 + 3.0f), V(w, 3.6f, 6f), dark);
            Tower(g, "Upper", V(s.cx, 3.6f, z0 + 3.0f), V(w, 8f, 6f), upper);
            // front details
            Prim(PrimitiveType.Cube, "Window", g, V(s.cx - 0.9f, 1.55f, z0 - 0.02f), V(2.2f, 1.7f, 0.08f), winMat, null, false);
            Prim(PrimitiveType.Cube, "Door", g, V(s.cx + 1.55f, 1.15f, z0 - 0.02f), V(1.1f, 2.3f, 0.1f), M("door", "#1a1530", 0.3f));
            Prim(PrimitiveType.Cube, "DoorGlass", g, V(s.cx + 1.55f, 1.4f, z0 - 0.08f), V(0.8f, 1.2f, 0.04f), winMat, null, false);
            // awning with a neon edge
            Prim(PrimitiveType.Cube, "Awning", g, V(s.cx, 3.0f, z0 - 0.7f), V(w, 0.1f, 1.5f), M("awning_" + s.word.ToLower(), s.neon, 0.3f, C(s.neon) * 0.25f), V(-14, 0, 0));
            NeonBar(g, V(s.cx - w / 2f, 2.72f, z0 - 1.42f), V(s.cx + w / 2f, 2.72f, z0 - 1.42f), 0.08f, neon);
            // the sign over it: a dark board with the word in neon
            Prim(PrimitiveType.Cube, "SignBoard", g, V(s.cx, 4.1f, z0 - 0.1f), V(3.6f, 1.0f, 0.16f), M("board", "#150f28", 0.3f));
            var txt = PixelText(g, s.word, V(s.cx, 4.1f, z0 - 0.22f), s.word.Length > 5 ? 0.085f : 0.1f, neon, "cb_text_" + s.word.ToLower(), 0.5f);
            MakeGlow(txt, s.glow * 2.6f, s.word == "OPEN" ? 3.5f : 0f, 0.8f, 1.15f);
            GlowDecal(g, V(s.cx, 4.1f, z0 - 0.4f), new Vector2(5.4f, 2.4f), "cb_halo_" + s.word.ToLower(), new Color(s.glow.r, s.glow.g, s.glow.b, 0.5f) * 1.2f, false);
            BlockLine(blocks, new Vector2(s.cx - w / 2f, z0 + 0.5f), new Vector2(s.cx + w / 2f, z0 + 0.5f), 0.7f, 1.4f);
        }
        // the dividing side wall between the shops and the club (so the cutaway has a west edge)
        TexBox(t, "ClubWestWall", V(0.6f, 3.0f, 8.9f), V(0.4f, 6.0f, 10.6f), dark);
    }

    // ---------- the club ----------

    static void MakeClub(Transform t, Transform blocks)
    {
        var brickTex = PatternTexture("cb_brick", "brick", "#4a2e5e", "#3c2650", "#6a4a80", 19);
        var brick = PatternMat(P + "brick_mat", brickTex, null, 0.2f);
        var trim = M("trim", "#150f28", 0.3f);
        const float z0 = 3.6f, z1 = 14.2f, xa = 0.8f, xb = 14.5f;
        // walls: the back wall and the east wall, dark purple brick
        TexBox(t, "BackWall", V((xa + xb) / 2f, 4.0f, z1 + 0.1f), V(xb - xa, 8f, 0.4f), brick);
        TexBox(t, "EastWall", V(xb + 0.1f, 4.0f, (z0 + z1) / 2f), V(0.4f, 8f, z1 - z0), brick);
        // a low front kerb with a gap at the door, and posts with neon rings at the corners (nothing tall in front of the stage)
        TexBox(t, "FrontKerb", V(8.6f, 0.15f, z0 + 0.1f), V(11.8f, 0.3f, 0.3f), brick);
        // THE FUZZ across the back wall, in neon pink, with a blue halo and little neon bolts
        var pink = NeonMat(P + "neon_fuzz", "#ff4adf", 3.0f);
        var blue = NeonMat(P + "neon_the", "#38e8ff", 2.6f);
        Prim(PrimitiveType.Cube, "SignPlate", t, V(7.6f, 5.7f, z1 - 0.1f), V(11.2f, 3.2f, 0.12f), M("sign_plate", "#120d22", 0.3f));
        var fuzz = PixelText(t, "FUZZ", V(7.6f, 5.55f, z1 - 0.24f), 0.34f, pink, "cb_text_fuzz", 0.5f);
        MakeGlow(fuzz, new Color(1f, 0.29f, 0.87f) * 3.0f, 6f, 0.85f, 1.1f);
        var the = PixelText(t, "THE", V(1.9f, 4.35f, z1 - 0.24f), 0.16f, blue, "cb_text_the", 0.5f);
        MakeGlow(the, new Color(0.22f, 0.91f, 1f) * 2.6f);
        GlowDecal(t, V(7.6f, 5.6f, z1 - 0.5f), new Vector2(14f, 5f), "cb_halo_fuzz", new Color(1.5f, 0.4f, 1.4f, 0.55f), false);
        // wall lights: a row of small neon bars along the back wall base and the east wall
        var strip = NeonMat(P + "neon_strip", "#ff4adf", 2.0f);
        NeonBar(t, V(1.2f, 0.9f, z1 - 0.12f), V(14.1f, 0.9f, z1 - 0.12f), 0.07f, strip);
        NeonBar(t, V(14.1f, 0.9f, z0 + 0.5f), V(14.1f, 0.9f, z1 - 0.2f), 0.07f, strip);
        // a disco ball hanging on the west side, turning
        var ballRoot = Group("DiscoBall", t).transform; ballRoot.position = V(3.6f, 4.3f, 7.2f);
        Prim(PrimitiveType.Cylinder, "BallRod", ballRoot, V(0, 0.9f, 0), V(0.03f, 0.9f, 0.03f), trim, null, false);
        var ball = Prim(PrimitiveType.Sphere, "Ball", ballRoot, V(0, 0, 0), V(0.95f, 0.95f, 0.95f), M("mirror", "#d8dcff", 0.95f, new Color(0.5f, 0.5f, 0.8f)));
        var spin = ballRoot.gameObject.AddComponent<PropSpin>(); spin.axis = Vector3.up; spin.degreesPerSecond = 30f;
        // facet glints: little bright squares on the ball
        var glint = M("glint", "#ffffff", 0.9f, new Color(2.2f, 2.2f, 2.6f));
        var rng = new System.Random(3);
        for (int i = 0; i < 26; i++)
        {
            Vector3 d = new Vector3((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1).normalized;
            var g = Prim(PrimitiveType.Cube, "Facet", ballRoot, d * 0.48f, V(0.1f, 0.1f, 0.04f), glint, null, false);
            g.transform.rotation = Quaternion.LookRotation(d);
        }
        // coloured wall washers (soft glow patches on the back wall)
        GlowDecal(t, V(3.0f, 2.4f, z1 - 0.4f), new Vector2(5f, 4f), "cb_wash_cyan", new Color(0.3f, 1.2f, 1.6f, 0.4f), false);
        GlowDecal(t, V(12.2f, 2.4f, z1 - 0.4f), new Vector2(5f, 4f), "cb_wash_pink", new Color(1.6f, 0.3f, 1.2f, 0.4f), false);
        // speaker stacks standing in the corners (the stage has its own pair)
        var cab = M("cab", "#14102a", 0.35f); var cone = M("cone", "#3a3560", 0.3f); var ring = M("cab_ring", "#38e8ff", 0.4f, new Color(0.3f, 1.1f, 1.6f));
        foreach (var p in new[] { (13.7f, 12.9f), (1.9f, 12.6f) })
        {
            Prim(PrimitiveType.Cube, "Cab", t, V(p.Item1, 1.1f, p.Item2), V(1.2f, 2.2f, 0.9f), cab);
            foreach (float cy in new[] { 0.65f, 1.55f })
            {
                Prim(PrimitiveType.Cylinder, "Cone", t, V(p.Item1, cy, p.Item2 - 0.455f), V(0.7f, 0.014f, 0.7f), cone, V(90, 0, 0), false);
                Prim(PrimitiveType.Cylinder, "ConeRing", t, V(p.Item1, cy, p.Item2 - 0.46f), V(0.8f, 0.012f, 0.8f), ring, V(90, 0, 0), false);
            }
            Block(blocks, p.Item1, p.Item2, 0.8f);
        }
    }

    // ---------- the stage ----------

    static void MakeStage(Transform t, Transform blocks)
    {
        Stage(t, blocks, new StageSpec
        {
            Prefix = P, Cx = 7.8f, Cz = 11.0f, Width = 9f, Depth = 4.4f, Height = 0.5f, RampLength = 0.8f,
            Deck = "#1a1530", Trim = "#ff4adf", TrimGlow = new Color(2.6f, 0.5f, 2.2f), Wall = "#1a1530", Dark = "#0e0a1e", Truss = "#8c88b0", Ring = "#38e8ff",
            BackWall = false, BannerStars = false, TrussHeight = 5.8f, SpotHeight = 5.5f, BeamLength = 6.2f, BeamRadius = 1.2f, SpeakerOffset = 3.9f,
            Lens = new[] { new Color(3.4f, 0.5f, 2.6f), new Color(0.5f, 2.6f, 3.4f), new Color(3.4f, 2.2f, 0.4f), new Color(0.6f, 3.2f, 1.2f), new Color(3.4f, 0.5f, 2.6f), new Color(0.5f, 2.6f, 3.4f) },
            SpotStart = 3.5f, SpotSpacing = 1.56f,
        });
        // LED strip along the front of the deck, a drum kit with lit heads, amps
        var led = NeonMat(P + "neon_deck", "#38e8ff", 2.4f);
        NeonBar(t, V(3.4f, 0.28f, 8.76f), V(12.2f, 0.28f, 8.76f), 0.05f, led);
        DrumKit(t, V(7.8f, 0.5f, 12.0f), 0f, P, "#ff4adf");
        AmpStack(t, V(4.2f, 0.5f, 12.4f), 16f, P, 1.1f, "#9a90c8");
        AmpStack(t, V(11.4f, 0.5f, 12.4f), -16f, P, 1.1f, "#9a90c8");
        Block(blocks, 4.2f, 12.4f, 0.8f); Block(blocks, 11.4f, 12.4f, 0.8f);
        // light bars on the truss: little emissive strips in two colours
        var barA = NeonMat(P + "bar_a", "#ff4adf", 2.2f); var barB = NeonMat(P + "bar_b", "#38e8ff", 2.2f);
        for (int i = 0; i < 6; i++) Prim(PrimitiveType.Cube, "LightBar", t, V(3.9f + i * 1.5f, 5.45f, 9.55f), V(1.0f, 0.12f, 0.12f), i % 2 == 0 ? barA : barB, null, false);
    }

    // ---------- the queue ----------

    static void MakeQueue(Transform t, Transform blocks)
    {
        // a line of fans along the shop fronts, waiting for the door, behind velvet ropes
        var brass = M("brass", "#e8c25a", 0.7f, new Color(0.4f, 0.3f, 0.1f));
        var rope = M("rope", "#c42a48", 0.25f);
        float zq = 2.3f;
        for (int i = 0; i < 10; i++)
        {
            float x = 0.5f - i * 1.05f;
            StaticFigure(t, FanPrefabs[i % FanPrefabs.Length], V(x, 0f, zq + (i % 3 - 1) * 0.12f), 90f + (i % 4 - 1.5f) * 10f, 0.85f, i % 4 == 1);
            Block(blocks, x, zq, 0.42f);
        }
        float zr = 1.1f;
        for (int i = 0; i <= 5; i++) RopePost(t, V(1.0f - i * 2.2f, 0, zr), brass);
        for (int i = 0; i < 5; i++)
        {
            Vector3 a = V(1.0f - i * 2.2f, 0.95f, zr), b = V(1.0f - (i + 1) * 2.2f, 0.95f, zr);
            var r = Prim(PrimitiveType.Cylinder, "Rope", t, (a + b) / 2f + V(0, -0.1f, 0), V(0.05f, 1.1f, 0.05f), rope, V(0, 0, 90), false);
            r.transform.localRotation = Quaternion.Euler(0, 0, 90f);
        }
        BlockLine(blocks, new Vector2(1.0f, zr), new Vector2(-10.0f, zr), 0.3f, 1.1f);
        // the bouncer at the door
        StaticFigure(t, "Crowd_crowd_knight_lilac", V(2.9f, 0f, 3.0f), 180f, 1.0f);
        Block(blocks, 2.9f, 3.0f, 0.5f);
    }

    // ---------- the street ----------

    static void MakeStreetDressing(Transform props, Transform blocks)
    {
        var d = Group("Street", props).transform;
        var trim = M("trim", "#150f28", 0.3f);
        var neonPink = NeonMat(P + "neon_pole", "#ff4adf", 2.4f);
        // street lamps with coloured heads
        foreach (var x in new[] { -12.3f, -4.2f, 4.3f, 12.3f })
        {
            Fbx(City + "streetlight.fbx", d, V(x, 0, -2.0f), 0f, CityScale, cityMat, "Streetlight").AddComponent<Occluder>();
            Block(blocks, x, -2.0f, 0.3f);
            Prim(PrimitiveType.Sphere, "LampGlow", d, V(x + 0.82f, 3.7f, -2.0f), V(0.5f, 0.28f, 0.5f), M("lamp", "#ffd9a8", 0.3f, new Color(3.6f, 2.6f, 1.6f)), null, false);
        }
        // a blade neon sign standing at the west corner of the club: LIVE, vertical bolts
        var live = NeonMat(P + "neon_live", "#ffe040", 2.8f);
        var pole = Group("LiveSign", d).transform; pole.position = V(0.3f, 0, 2.2f);   // east of the ARCADE sign, so the board no longer hides its last letters
        Prim(PrimitiveType.Cylinder, "Pole", pole, V(0, 2.0f, 0), V(0.1f, 2.0f, 0.1f), trim);
        var board = Prim(PrimitiveType.Cube, "Board", pole, V(0, 4.2f, 0), V(1.7f, 1.0f, 0.14f), M("board", "#150f28", 0.3f));
        var lt = PixelText(pole, "LIVE", V(0, 4.2f, -0.12f), 0.07f, live, "cb_text_live", 0.5f);
        MakeGlow(lt, new Color(1f, 0.88f, 0.25f) * 2.8f, 4.5f);
        Block(blocks, 0.3f, 2.2f, 0.3f);
        // parked cars in the road, a taxi, bins, a dumpster, a hydrant
        Fbx(City + "car_taxi.fbx", d, V(9.4f, 0, -6.2f), 90f, CityScale, cityMat, "Taxi");
        Block(blocks, 7.9f, -6.2f, 0.9f); Block(blocks, 9.4f, -6.2f, 0.9f); Block(blocks, 10.9f, -6.2f, 0.9f);
        Fbx(City + "car_sedan.fbx", d, V(-9.0f, 0, -3.8f), -90f, CityScale, cityMat, "Sedan");
        Block(blocks, -10.4f, -3.8f, 0.9f); Block(blocks, -9.0f, -3.8f, 0.9f); Block(blocks, -7.6f, -3.8f, 0.9f);
        Fbx(City + "dumpster.fbx", d, V(13.0f, 0, -1.2f), -90f, CityScale * 0.8f, cityMat, "Dumpster"); Block(blocks, 13.0f, -1.2f, 1.0f);
        Fbx(City + "trash_B.fbx", d, V(-12.8f, 0, -1.4f), 20f, CityScale, cityMat, "Bin"); Block(blocks, -12.8f, -1.4f, 0.35f);
        Fbx(City + "firehydrant.fbx", d, V(5.8f, 0, -2.0f), 0f, CityScale, cityMat, "Hydrant"); Block(blocks, 5.8f, -2.0f, 0.35f);
        // bollards with pink caps along the south kerb, benches
        for (int i = 0; i < 9; i++)
        {
            float x = -12f + i * 3f;
            if (Mathf.Abs(x) < 2.5f) continue;
            Prim(PrimitiveType.Cylinder, "Bollard", d, V(x, 0.35f, -7.8f), V(0.2f, 0.35f, 0.2f), trim);
            Prim(PrimitiveType.Sphere, "BollardCap", d, V(x, 0.74f, -7.8f), V(0.24f, 0.2f, 0.24f), neonPink, null, false);
            Block(blocks, x, -7.8f, 0.22f);
        }
        foreach (var b in new[] { (-5.4f, -11.6f), (5.4f, -11.6f) })
        {
            Fbx(City + "bench.fbx", d, V(b.Item1, 0, b.Item2), 90f, CityScale, cityMat, "Bench");
            Block(blocks, b.Item1, b.Item2, 0.8f);
        }
        // a few wet-looking planters with glowing bushes in the south
        var green = ForestTinted("club_green", "#6a5cc0", "#2e2a78");
        foreach (var p in new[] { (-12.6f, -12.4f), (12.6f, -12.8f), (-12.6f, -8.8f) })
        {
            Prim(PrimitiveType.Cube, "Planter", d, V(p.Item1, 0.25f, p.Item2), V(1.6f, 0.5f, 1.6f), M("planter", "#3a3358", 0.3f));
            Fbx(Forest + "Bush_2_A_Color1.fbx", d, V(p.Item1, 0.5f, p.Item2), p.Item1 * 17f, 1.5f, green, "Bush");
            Block(blocks, p.Item1, p.Item2, 0.9f);
        }
        // string lights across the street from lamp to lamp
        var wire = new MB(); var bulbs = new MB();
        for (int i = 0; i < 3; i++)
        {
            float[] xs = { -12.3f, -4.2f, 4.3f, 12.3f };
            StringLine(wire, bulbs, V(xs[i], 3.5f, -2.0f), V(xs[i + 1], 3.5f, -2.0f), 0.9f, 0.7f, 0.09f);
        }
        EmitStrings(d, wire, bulbs, "cb", C("#ffd9f8"), new Color(3.0f, 1.4f, 2.8f));
    }

    // ---------- the skyline ----------

    static void MakeSkyline(Transform t)
    {
        var near = WindowTowerMaterial("cb_tower_near", "#2a2250", "#382e66", "#ffb36b", 0.3f, 23, 2.2f);
        var far = WindowTowerMaterial("cb_tower_far", "#3a2c64", "#463a78", "#ff9ae0", 0.26f, 29, 1.8f);
        var rng = new System.Random(8);
        // behind the club and the shops, and along both sides: towers rising from below the ground
        for (int i = 0; i < 8; i++)
            Tower(t, "TowerN", V(-28f + i * 8.5f, 0f, 21f + (float)rng.NextDouble() * 5f), V(8f, 12f + (float)rng.NextDouble() * 12f, 8f), near);
        for (int i = 0; i < 4; i++)
        {
            Tower(t, "TowerW", V(-19.5f, 0f, -12f + i * 9f), V(8f, 9f + (float)rng.NextDouble() * 10f, 8f), near);
            Tower(t, "TowerE", V(19.5f, 0f, -12f + i * 9f), V(8f, 9f + (float)rng.NextDouble() * 10f, 8f), near);
        }
        for (int i = 0; i < 12; i++)
            Tower(t, "TowerFar", V(-70f + i * 12.5f, 0f, 52f + (float)rng.NextDouble() * 24f), V(12f, 20f + (float)rng.NextDouble() * 36f, 12f), far);
        for (int i = 0; i < 4; i++) Tower(t, "TowerS", V(-30f + i * 20f, 0f, -34f), V(12f, 10f + (float)rng.NextDouble() * 10f, 12f), near);
    }
}
