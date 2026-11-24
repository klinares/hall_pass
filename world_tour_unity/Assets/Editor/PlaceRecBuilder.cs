// Phase 8.2 builder A: Place_Rec, "the rec center, battle of the bands, afternoon". A school yard with a half court and a sign wall in front, and the gym
// as a cutaway: low front wall with a wide doorway, a wooden court floor, padded blue walls with high windows and a scoreboard, folding chairs, bleachers,
// a judges' table with score cards, and a navy stage with a red BATTLE OF THE BANDS banner and a drum kit.
//
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -executeMethod PlaceRecBuilder.CreateAll
//
// Layout (x right, z away from the camera, walkers clamped to +-13.5): the yard (spawn, path, court on the west, lawn and the exit on the east) -> the gym's
// front wall at z = 2.6 with a doorway x -3.5 .. 3.5 -> the gym floor z 2.6 .. 14, stage at the north (x -4.5 .. 4.5, z 9 .. 13.4), chairs west, judges east.
using UnityEditor;
using UnityEngine;
using WorldTour.World;
using static PlaceKit;

public static class PlaceRecBuilder
{
    public const string ScenePath = "Assets/World/Scenes/Place_Rec.unity";
    const string P = "rc_";
    const float GymZ = 2.6f;

    public static void CreateAll() { Entry("PlaceRecBuilder", Make); }

    static Material M(string n, string hex, float s = 0.15f, Color? e = null) { return Lit(P + n, C(hex), s, e); }

    public static void Make()
    {
        var scene = NewScene();
        InitKits(P, new Color(1.02f, 1.0f, 0.96f), false);
        var look = Looks.Rec();
        var profile = MakeProfile(look);
        var world = Group("World").transform;
        var ground = Group("Ground", world).transform;
        var yard = Group("Yard", world).transform;
        var gym = Group("Gym", world).transform;
        var stage = Group("Stage", world).transform;
        var blds = Group("Buildings", world).transform;
        var props = Group("Props", world).transform;
        var blocks = Group("Blockers", world).transform;

        MakeGround(ground);
        MakeYard(yard, props, blocks);
        MakeGym(gym, props, blocks);
        MakeStage(stage, blocks);
        MakeSeating(props, blocks);
        MakeBuildings(blds);

        var contract = new Contract
        {
            PlaceId = "rec", RouteIndex = 2,
            Spawn = V(0, 0, -8.2f),
            StageSpot = V(0f, 0f, 10.4f),
            CrowdCenter = V(0f, 0f, 5.8f), CrowdSize = new Vector2(8.2f, 3.2f),
            Exit = V(11.2f, 0f, -9.6f), ExitArrowYaw = 0f,
            Crew = new[] { V(-5.4f, 0f, -3.4f), V(5.2f, 0f, -0.4f), V(6.0f, 0f, 3.9f) },
            Dog = V(-1.8f, 0f, -10.2f),
        };
        Populate(contract, look, profile, blocks);
        FinishLook(look);
        Motes("DustMotes", V(0f, 3.6f, 8f), V(26f, 4f, 11f), new Color(1.5f, 1.3f, 0.8f, 0.5f), Color.white, 60, 8f, new Vector2(0.08f, 0.18f), new Vector2(5f, 9f), 0.5f, 0f, "rc_dust_particle");
        Leaves("Leaves", V(0, 8f, -7f), V(28f, 1f, 12f), "#ffd27a", "#9ccf5a", 24, 2f);
        SaveScene(scene, ScenePath);
    }

    // ---------- ground ----------

    static void MakeGround(Transform t)
    {
        Prim(PrimitiveType.Plane, "Lawn", t, V(0, -0.03f, 0), V(24, 1, 24), M("lawn", "#86b36a", 0.05f), null, false);
        var yardTex = PavingTexture("rc_yard", "#d9d2c1", "#cfc7b4", "#aaa28f", "#c9b79a", "#d8c8ae", 41, false);
        // the yard in front of the gym: paving over the whole width, then the lawn patch on the east
        PavedPlane("YardPaving", t, P + "yard_mat", yardTex, V(-4f, 0.005f, -4.4f), new Vector2(20f, 14.6f));
        PavedPlane("YardPath", t, P + "yard_mat", yardTex, V(7.5f, 0.005f, 1.4f), new Vector2(7f, 2.6f));
        // the half court, painted on the west of the yard
        var court = Prim(PrimitiveType.Cube, "Court", t, V(-7.8f, 0.02f, -6.6f), V(8.2f, 0.03f, 9.4f), M("court", "#3f64a8", 0.2f), null, false);
        var line = M("court_line", "#fffaf0", 0.2f);
        void L(float x, float z, float w, float d) { Prim(PrimitiveType.Cube, "CourtLine", t, V(x, 0.04f, z), V(w, 0.01f, d), line, null, false); }
        L(-7.8f, -11.2f, 7.8f, 0.1f); L(-7.8f, -2.0f, 7.8f, 0.1f); L(-11.8f, -6.6f, 0.1f, 9.3f); L(-3.8f, -6.6f, 0.1f, 9.3f);
        Prim(PrimitiveType.Cube, "Key", t, V(-7.8f, 0.035f, -3.7f), V(2.8f, 0.01f, 3.4f), M("key", "#e8803a", 0.2f), null, false);
        L(-9.2f, -3.7f, 0.1f, 3.4f); L(-6.4f, -3.7f, 0.1f, 3.4f); L(-7.8f, -5.4f, 2.9f, 0.1f);
        // the arc over the key: a ring of short bars
        for (int i = 0; i < 18; i++)
        {
            float a = Mathf.Lerp(-80f, 80f, i / 17f) * Mathf.Deg2Rad;
            var bar = Prim(PrimitiveType.Cube, "CourtArc", t, V(-7.8f + Mathf.Sin(a) * 3.5f, 0.04f, -5.4f - Mathf.Cos(a) * 3.5f), V(0.42f, 0.01f, 0.1f), line, V(0, a * Mathf.Rad2Deg + 90f, 0), false);
        }
        // the gym floor: wooden planks
        var planks = PatternTexture("rc_planks", "planks", "#d9a05a", "#c88d46", "#9d6a34", 8);
        var plankMat = PatternMat(P + "planks_mat", planks, null, 0.35f);
        TexBox(t, "GymFloor", V(0, 0.03f, (GymZ + 14f) / 2f), V(28f, 0.06f, 14f - GymZ), plankMat, 0.25f, null, false);
        // court lines on the floor: the side lines, the baseline in front, and the centre circle
        void G(float x, float z, float w, float d) { Prim(PrimitiveType.Cube, "GymLine", t, V(x, 0.065f, z), V(w, 0.01f, d), M("gym_line", "#f6efe0", 0.3f), null, false); }
        G(0f, 3.3f, 24f, 0.12f); G(-12f, 8.6f, 0.12f, 10.6f); G(12f, 8.6f, 0.12f, 10.6f); G(0f, 8.6f, 0.12f, 10.6f);
        for (int i = 0; i < 28; i++)
        {
            float a = i / 28f * Mathf.PI * 2f;
            Prim(PrimitiveType.Cube, "GymCircle", t, V(0 + Mathf.Sin(a) * 2.6f, 0.065f, 6.8f + Mathf.Cos(a) * 2.6f), V(0.62f, 0.01f, 0.12f), M("gym_line", "#f6efe0", 0.3f), V(0, a * Mathf.Rad2Deg + 90f, 0), false);
        }
        // a red carpet strip from the path through the doorway
        Prim(PrimitiveType.Cube, "Carpet", t, V(0, 0.075f, 1.3f), V(3.2f, 0.02f, 5.6f), M("carpet", "#c4412f", 0.1f), null, false);
    }

    // ---------- the yard ----------

    static void MakeYard(Transform t, Transform props, Transform blocks)
    {
        var trim = M("trim", "#243b6b", 0.3f);
        var white = M("white", "#fffaf0", 0.2f);
        var navy = M("navy", "#2a4f9a", 0.2f);
        var red = M("red", "#e8503a", 0.2f);
        var cream = M("cream", "#efe6d2", 0.15f);
        // the front wall of the gym, low, with a wide doorway; a navy stripe along it
        foreach (float sx in new[] { -1f, 1f })
        {
            float cx = sx * 11.2f;
            Prim(PrimitiveType.Cube, "FrontWall", t, V(cx, 0.6f, GymZ), V(5.6f, 1.2f, 0.5f), cream);
            Prim(PrimitiveType.Cube, "FrontStripe", t, V(cx, 0.8f, GymZ - 0.27f), V(5.6f, 0.22f, 0.06f), navy, null, false);
            Prim(PrimitiveType.Cube, "FrontCap", t, V(cx, 1.25f, GymZ), V(5.8f, 0.14f, 0.7f), white);
            Prim(PrimitiveType.Cube, "DoorPost", t, V(sx * 8.2f, 1.5f, GymZ), V(0.5f, 3.0f, 0.5f), navy);
            Prim(PrimitiveType.Sphere, "PostBall", t, V(sx * 8.2f, 3.1f, GymZ), V(0.5f, 0.5f, 0.5f), red);
            for (float x = 9.0f; x <= 13.4f; x += 1.6f) Block(blocks, sx * x, GymZ, 0.7f);
        }
        // the sign wall: REC CENTER in white on navy, low, so nothing stands between the stage camera and the stage
        Prim(PrimitiveType.Cube, "SignWall", t, V(8.6f, 0.65f, -0.9f), V(8.6f, 1.3f, 0.5f), navy);
        Prim(PrimitiveType.Cube, "SignWallCap", t, V(8.6f, 1.35f, -0.9f), V(8.8f, 0.14f, 0.7f), white);
        PixelText(t, "REC CENTER", V(8.6f, 0.68f, -1.2f), 0.095f, M("sign_text", "#fffaf0", 0.3f, new Color(0.55f, 0.55f, 0.5f)), "rc_text_rec", 0.5f);
        Prim(PrimitiveType.Cube, "SignStripe", t, V(8.6f, 1.2f, -1.17f), V(8.6f, 0.07f, 0.04f), red, null, false);
        BlockLine(blocks, new Vector2(4.4f, -0.9f), new Vector2(12.8f, -0.9f), 0.6f, 1.2f);
        // flags on poles beside the doorway
        string[] flag = { "#e8503a", "#ffd23a", "#3f64a8" };
        for (int i = 0; i < 3; i++)
        {
            float x = -6.4f + i * 0.9f;
            var pole = Prim(PrimitiveType.Cylinder, "FlagPole", t, V(x, 2.2f, -0.2f), V(0.07f, 2.2f, 0.07f), M("pole", "#c9ccd6", 0.5f));
            pole.AddComponent<Occluder>();
            Prim(PrimitiveType.Cube, "Flag", t, V(x + 0.45f, 3.85f - i * 0.12f, -0.2f), V(0.8f, 0.5f, 0.03f), M("flag" + i, flag[i], 0.2f), V(0, 0, (i - 1) * 4f), false);
            Block(blocks, x, -0.2f, 0.25f);
        }
        // the basketball hoop at the end of the court: pole, backboard, ring, a ball on the ground
        var steel = M("steel", "#9aa0b8", 0.6f);
        Prim(PrimitiveType.Cylinder, "HoopPole", t, V(-7.8f, 1.7f, -1.3f), V(0.14f, 1.7f, 0.14f), steel);
        Prim(PrimitiveType.Cube, "HoopArm", t, V(-7.8f, 3.2f, -1.7f), V(0.1f, 0.1f, 0.9f), steel);
        Prim(PrimitiveType.Cube, "Backboard", t, V(-7.8f, 3.55f, -2.2f), V(1.8f, 1.15f, 0.06f), white);
        Prim(PrimitiveType.Cube, "BackboardBox", t, V(-7.8f, 3.4f, -2.17f), V(0.6f, 0.45f, 0.02f), red, null, false);
        HoopRing(t, V(-7.8f, 3.1f, -2.65f), 0.42f, M("hoop", "#ff6a2a", 0.3f));
        Prim(PrimitiveType.Sphere, "Ball", t, V(-6.2f, 0.19f, -4.6f), V(0.38f, 0.38f, 0.38f), M("ball", "#e8803a", 0.3f));
        Block(blocks, -7.8f, -1.3f, 0.4f);
        // benches by the path, bike rack, lamps, trees, bushes
        foreach (var b in new[] { (-2.8f, -4.0f, 90f), (2.8f, -4.0f, 90f), (-3.4f, -11.8f, 90f) })
        {
            Fbx(City + "bench.fbx", t, V(b.Item1, 0, b.Item2), b.Item3, CityScale, cityMat, "Bench");
            Block(blocks, b.Item1, b.Item2, 0.8f);
        }
        for (int i = 0; i < 6; i++) Prim(PrimitiveType.Cylinder, "BikeRack", t, V(6.6f + i * 0.5f, 0.4f, -5.2f), V(0.05f, 0.4f, 0.05f), steel);
        Prim(PrimitiveType.Cylinder, "BikeRackBar", t, V(8.1f, 0.7f, -5.2f), V(0.05f, 1.35f, 0.05f), steel, V(0, 0, 90));
        foreach (float x in new[] { -2.9f, 2.9f })
        {
            Fbx(City + "streetlight.fbx", t, V(x, 0, -1.2f), 0f, CityScale, cityMat, "Streetlight").AddComponent<Occluder>();
            Block(blocks, x, -1.2f, 0.3f);
        }
        var green = ForestTinted("rec_green", "#8fcf5a", "#4b9a3a");
        string[] kinds = { "Tree_1_A_Color1", "Tree_3_A_Color1", "Tree_2_A_Color1", "Tree_4_A_Color1", "Tree_3_A_Color1" };
        var spots = new[] { (12.4f, -3.2f), (11.8f, -13f), (-13.0f, -13.2f), (13.0f, 0.2f), (3.8f, -12.8f) };
        for (int i = 0; i < spots.Length; i++)
        {
            Fbx(Forest + kinds[i] + ".fbx", t, V(spots[i].Item1, 0, spots[i].Item2), i * 60f, 1.15f, green, "Tree").AddComponent<Occluder>();
            Block(blocks, spots[i].Item1, spots[i].Item2, 0.6f);
        }
        var bush = new[] { (5.2f, -6.8f), (9.4f, -12.8f), (-13f, -5.0f), (13f, -7f) };
        for (int i = 0; i < bush.Length; i++)
        {
            Fbx(Forest + (i % 2 == 0 ? "Bush_2_A_Color1.fbx" : "Bush_4_A_Color1.fbx"), t, V(bush[i].Item1, 0, bush[i].Item2), i * 77f, 1.6f, green, "Bush");
            Block(blocks, bush[i].Item1, bush[i].Item2, 0.55f);
        }
        // the lawn on the east and its edging
        Prim(PrimitiveType.Cube, "LawnEdge", t, V(9.4f, 0.05f, -2.3f), V(8f, 0.1f, 0.18f), cream, null, false);
    }

    static void HoopRing(Transform parent, Vector3 center, float radius, Material mat)
    {
        for (int i = 0; i < 14; i++)
        {
            float a = i / 14f * Mathf.PI * 2f;
            Prim(PrimitiveType.Cube, "HoopRing", parent, center + V(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius), V(0.14f, 0.05f, 0.05f), mat, V(0, -a * Mathf.Rad2Deg + 90f, 0), false);
        }
    }

    // ---------- the gym ----------

    static void MakeGym(Transform t, Transform props, Transform blocks)
    {
        var padded = M("padded", "#3f64a8", 0.25f);
        var padSeam = M("padded_seam", "#2f4f8c", 0.2f);
        var cream = M("wall_cream", "#efe6d2", 0.12f);
        var trim = M("trim", "#243b6b", 0.3f);
        var red = M("red", "#e8503a", 0.2f);
        var window = M("window", "#fff3c4", 0.2f, new Color(1.4f, 1.25f, 0.8f));
        const float zA = GymZ, zB = 14.0f;
        // side walls and the back wall: padded blue to 2.6 m, cream above, a red stripe between, windows high up
        foreach (float sx in new[] { -1f, 1f })
        {
            float x = sx * 14.1f;
            Prim(PrimitiveType.Cube, "SideWall", t, V(x, 4.0f, (zA + zB) / 2f), V(0.4f, 8f, zB - zA), cream);
            Prim(PrimitiveType.Cube, "SidePad", t, V(x - sx * 0.12f, 1.3f, (zA + zB) / 2f), V(0.2f, 2.6f, zB - zA - 0.2f), padded);
            Prim(PrimitiveType.Cube, "SideStripe", t, V(x - sx * 0.12f, 2.72f, (zA + zB) / 2f), V(0.2f, 0.16f, zB - zA), red, null, false);
            for (int i = 0; i < 5; i++)
                Prim(PrimitiveType.Cube, "SideWindow", t, V(x - sx * 0.12f, 5.6f, zA + 1.6f + i * 2.4f), V(0.1f, 1.3f, 1.7f), window, null, false);
            for (float z = zA + 1.2f; z < zB - 0.5f; z += 1.6f) Prim(PrimitiveType.Cube, "PadSeam", t, V(x - sx * 0.23f, 1.3f, z), V(0.02f, 2.55f, 0.06f), padSeam, null, false);
        }
        Prim(PrimitiveType.Cube, "BackWall", t, V(0, 4.8f, zB + 0.2f), V(28.8f, 9.6f, 0.4f), cream);
        Prim(PrimitiveType.Cube, "BackPad", t, V(0, 1.3f, zB - 0.02f), V(27.6f, 2.6f, 0.2f), padded);
        Prim(PrimitiveType.Cube, "BackStripe", t, V(0, 2.72f, zB - 0.02f), V(27.6f, 0.16f, 0.2f), red, null, false);
        // the scoreboard high on the back wall
        var board = M("scoreboard", "#14161f", 0.25f);
        var amber = NeonMat(P + "score_amber", "#ffb02e", 2.2f);
        var redLed = NeonMat(P + "score_red", "#ff5a4a", 2.0f);
        Prim(PrimitiveType.Cube, "Scoreboard", t, V(0, 7.5f, zB - 0.35f), V(7.4f, 2.7f, 0.5f), board);
        Prim(PrimitiveType.Cube, "ScoreboardTrim", t, V(0, 9.0f, zB - 0.4f), V(7.6f, 0.2f, 0.6f), red);
        var s1 = PixelText(t, "HOME", V(-2.1f, 8.4f, zB - 0.64f), 0.085f, redLed, "rc_text_home", 0.5f);
        var s2 = PixelText(t, "GUEST", V(2.1f, 8.4f, zB - 0.64f), 0.085f, redLed, "rc_text_guest", 0.5f);
        var d1 = PixelText(t, "12", V(-2.1f, 7.35f, zB - 0.64f), 0.17f, amber, "rc_text_12", 0.5f);
        var d2 = PixelText(t, "07", V(2.1f, 7.35f, zB - 0.64f), 0.17f, amber, "rc_text_07", 0.5f);
        var d3 = PixelText(t, "3", V(0f, 7.35f, zB - 0.64f), 0.17f, amber, "rc_text_3", 0.5f);
        MakeGlow(s1, new Color(1f, 0.35f, 0.29f) * 2f); MakeGlow(s2, new Color(1f, 0.35f, 0.29f) * 2f);
        MakeGlow(d1, new Color(1f, 0.69f, 0.18f) * 2.2f); MakeGlow(d2, new Color(1f, 0.69f, 0.18f) * 2.2f); MakeGlow(d3, new Color(1f, 0.69f, 0.18f) * 2.2f);
        // two hoops on the side walls (the camera sees their backs and rims)
        foreach (float sx in new[] { -1f, 1f })
        {
            Prim(PrimitiveType.Cube, "WallBackboard", t, V(sx * 13.8f, 4.1f, 8.6f), V(0.08f, 1.15f, 1.8f), M("white", "#fffaf0", 0.2f));
            Prim(PrimitiveType.Cube, "WallBackboardBox", t, V(sx * 13.76f, 3.95f, 8.6f), V(0.02f, 0.45f, 0.6f), red, null, false);
            var ring = Group("WallHoop", t).transform; ring.position = V(sx * 13.25f, 3.65f, 8.6f);
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f;
                Prim(PrimitiveType.Cube, "HoopRing", ring, V(Mathf.Cos(a) * 0.42f, 0, Mathf.Sin(a) * 0.42f), V(0.14f, 0.05f, 0.05f), M("hoop", "#ff6a2a", 0.3f), V(0, -a * Mathf.Rad2Deg + 90f, 0), false);
            }
        }
        // bunting along both side walls and the back wall (flat diamonds hanging in a curve), one mesh per colour
        string[] flagHex = { "#e8503a", "#ffd23a", "#3f64a8", "#fffaf0" };
        var flags = new MB[4]; for (int i = 0; i < 4; i++) flags[i] = new MB();
        var wireMb = new MB(); var bulbMb = new MB();
        void Bunting(Vector3 a, Vector3 b)
        {
            int n = Mathf.RoundToInt((b - a).magnitude / 0.55f);
            for (int i = 1; i < n; i++)
            {
                float s = i / (float)n;
                Vector3 p = Vector3.Lerp(a, b, s) + Vector3.down * (0.45f * 4f * s * (1f - s) + 0.2f);
                Vector3 dir = (b - a).normalized;
                flags[i % 4].Gem(p, dir * 0.17f, Vector3.up * 0.22f, Vector3.Cross(dir, Vector3.up) * 0.015f);
            }
            StringLine(wireMb, bulbMb, a, b, 0.45f, 100f, 0.01f);
        }
        Bunting(V(-13.7f, 5.0f, 3.2f), V(-13.7f, 5.0f, 13.6f));
        Bunting(V(13.7f, 5.0f, 3.2f), V(13.7f, 5.0f, 13.6f));
        Bunting(V(-13.6f, 6.2f, 13.7f), V(13.6f, 6.2f, 13.7f));
        for (int i = 0; i < 4; i++) MeshObject("Bunting" + i, t, flags[i].ToMesh("rc_bunting_" + i), DoubleSided(P + "flag_" + i, C(flagHex[i]), 0.25f), false);
        MeshObject("BuntingWire", t, wireMb.ToMesh("rc_bunting_wire"), DoubleSided(P + "bunting_wire", C("#4a3b4d"), 0.2f), false);
    }

    // ---------- the stage ----------

    static void MakeStage(Transform t, Transform blocks)
    {
        Stage(t, blocks, new StageSpec
        {
            Prefix = P, Cx = 0f, Cz = 11.2f, Width = 9f, Depth = 4.4f, Height = 0.45f, RampLength = 0.7f,
            Deck = "#a8703c", Trim = "#e8503a", TrimGlow = new Color(0.5f, 0.12f, 0.08f), Wall = "#2a4f9a", Dark = "#1a2a55", Truss = "#9aa0b8", Ring = "#ffd23a",
            BannerStars = false, WallHeight = 4.6f, TrussHeight = 5.0f, SpotHeight = 4.7f, BeamLength = 5.4f, BeamRadius = 1.1f, SpeakerOffset = 3.9f,
            Lens = new[] { new Color(3f, 0.8f, 0.6f), new Color(2.8f, 2.4f, 0.9f), new Color(1.0f, 1.4f, 3.2f), new Color(3f, 2.8f, 2.4f), new Color(3f, 0.8f, 0.6f), new Color(1.0f, 1.4f, 3.2f) },
            SpotStart = -3.9f, SpotSpacing = 1.56f,
        });
        // the banner on the stage wall: BATTLE OF / THE BANDS on a red board
        Prim(PrimitiveType.Cube, "BannerBoard", t, V(0f, 3.0f, 13.34f), V(7.2f, 1.9f, 0.1f), M("banner", "#d2402e", 0.25f));
        Prim(PrimitiveType.Cube, "BannerTrim", t, V(0f, 3.0f, 13.4f), V(7.4f, 2.1f, 0.06f), M("white", "#fffaf0", 0.2f), null, false);
        var bt = M("banner_text", "#fffaf0", 0.3f, new Color(0.7f, 0.7f, 0.65f));
        var by = NeonMat(P + "banner_yellow", "#ffd23a", 1.2f);
        PixelText(t, "BATTLE OF", V(0f, 3.4f, 13.26f), 0.095f, bt, "rc_text_battle", 0.5f);
        PixelText(t, "THE BANDS", V(0f, 2.55f, 13.26f), 0.095f, by, "rc_text_bands", 0.5f);
        // gear: a drum kit at the back, a guitar amp each side of it
        DrumKit(t, V(0f, 0.45f, 12.0f), 0f, P, "#e8503a");
        AmpStack(t, V(-3.4f, 0.45f, 12.4f), 16f, P, 1.05f, "#e8d4a0");
        AmpStack(t, V(3.4f, 0.45f, 12.4f), -16f, P, 1.05f, "#e8d4a0");
        Block(blocks, -3.4f, 12.4f, 0.8f); Block(blocks, 3.4f, 12.4f, 0.8f);
    }

    // ---------- seating, judges ----------

    static void MakeSeating(Transform props, Transform blocks)
    {
        var d = Group("Seating", props).transform;
        // folding chairs on the west, in a fan facing the stage: one mesh for the seats, one for the frames
        var seat = new MB(); var frame = new MB();
        for (int row = 0; row < 5; row++)
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = V(-12.2f + i * 0.78f + row * 0.15f, 0f, 4.4f + row * 1.15f);
                Vector3 toStage = V(0f, 0f, 10.4f) - p; toStage.y = 0;
                float yaw = Mathf.Atan2(toStage.x, toStage.z) * Mathf.Rad2Deg;   // the seat's front (-z at yaw 0) turned to face the stage: back toward -toStage
                float ry = yaw;
                Quaternion q = Quaternion.Euler(0, ry, 0);
                // seat faces +z at ry = 0 in this build (front toward the stage); the back stands behind it
                seat.Box(p + q * V(0, 0.46f, 0), V(0.23f, 0.03f, 0.22f), ry);
                seat.Box(p + q * V(0, 0.8f, -0.2f), V(0.22f, 0.17f, 0.025f), ry);
                foreach (float lx in new[] { -0.19f, 0.19f })
                {
                    frame.Box(p + q * V(lx, 0.22f, 0.19f), V(0.018f, 0.22f, 0.018f), ry);
                    frame.Box(p + q * V(lx, 0.32f, -0.19f), V(0.018f, 0.33f, 0.018f), ry);
                }
            }
        MeshObject("ChairSeats", d, seat.ToMesh("rc_chair_seats"), M("chair_seat", "#3f64a8", 0.3f), true);
        MeshObject("ChairFrames", d, frame.ToMesh("rc_chair_frames"), M("chair_frame", "#c9ccd6", 0.6f), true);
        // the chairs are blockers on a grid
        for (int row = 0; row < 5; row++) for (int i = 0; i < 6; i += 1) Block(blocks, -12.2f + i * 0.78f + row * 0.15f, 4.4f + row * 1.15f, 0.34f);
        // bleachers on the east wall: three tiers
        string[] tier = { "#3f64a8", "#e8503a", "#ffd23a" };
        for (int i = 0; i < 3; i++)
        {
            Prim(PrimitiveType.Cube, "Bleacher", d, V(13.15f - i * 0.0f, 0.3f + i * 0.55f, 9.0f), V(1.7f - i * 0.5f, 0.6f + i * 0.55f, 9.0f), M("bleacher" + i, tier[i], 0.25f));
            Prim(PrimitiveType.Cube, "BleacherTop", d, V(13.15f + 0.0f, 0.62f + i * 0.55f + 0.01f, 9.0f), V(1.7f - i * 0.5f + 0.02f, 0.04f, 9.02f), M("bleacher_top", "#d9a05a", 0.3f), null, false);
        }
        BlockLine(blocks, new Vector2(12.7f, 4.2f), new Vector2(12.7f, 13.4f), 0.55f, 1.2f);
        // the judges' table on the east lane: a long table with a cloth, three score cards, three judges behind it facing the stage
        var table = Group("JudgesTable", d).transform; table.position = V(9.2f, 0f, 5.9f);
        Prim(PrimitiveType.Cube, "Cloth", table, V(0, 0.45f, 0), V(4.8f, 0.9f, 1.0f), M("cloth", "#fffaf0", 0.15f));
        Prim(PrimitiveType.Cube, "ClothHem", table, V(0, 0.12f, 0.0f), V(4.86f, 0.24f, 1.06f), M("cloth_hem", "#e8503a", 0.15f));
        Prim(PrimitiveType.Cube, "TableTop", table, V(0, 0.93f, 0), V(4.9f, 0.06f, 1.1f), M("wood", "#a8703c", 0.3f));
        string[] scores = { "8", "9", "7" };
        for (int i = 0; i < 3; i++)
        {
            float x = -1.5f + i * 1.5f;
            Prim(PrimitiveType.Cube, "CardBoard", table, V(x, 1.28f, -0.3f), V(0.7f, 0.62f, 0.05f), M("white", "#fffaf0", 0.2f));
            PixelText(table, scores[i], V(x, 1.28f, -0.345f), 0.07f, M("card_digit", "#d2402e", 0.3f), "rc_text_score" + scores[i], 0.5f);
            Prim(PrimitiveType.Cube, "CardFoot", table, V(x, 1.0f, -0.3f), V(0.3f, 0.06f, 0.2f), M("white", "#fffaf0", 0.2f), null, false);
            Prim(PrimitiveType.Cylinder, "Mug", table, V(x + 0.45f, 1.06f, 0.1f), V(0.18f, 0.1f, 0.18f), M("mug", "#e8503a", 0.4f), null, false);
        }
        for (int i = 0; i < 3; i++)
        {
            float x = 9.2f - 1.5f + i * 1.5f;
            StaticFigure(d, FanPrefabs[(i * 3 + 1) % FanPrefabs.Length], V(x, 0f, 5.0f), 0f, 0.9f);
        }
        Block(blocks, 7.2f, 5.9f, 0.9f); Block(blocks, 8.7f, 5.9f, 0.9f); Block(blocks, 10.2f, 5.9f, 0.9f); Block(blocks, 11.4f, 5.9f, 0.9f);
        // a water cooler and a trophy on a stand at the front of the gym
        Prim(PrimitiveType.Cylinder, "CoolerBase", d, V(-3.0f, 0.5f, 3.4f), V(0.4f, 0.5f, 0.4f), M("cooler", "#c9d6e8", 0.4f));
        Prim(PrimitiveType.Cylinder, "CoolerJug", d, V(-3.0f, 1.3f, 3.4f), V(0.38f, 0.3f, 0.38f), M("water", "#8fd0ff", 0.7f));
        Block(blocks, -3.0f, 3.4f, 0.45f);
        Prim(PrimitiveType.Cube, "TrophyStand", d, V(3.1f, 0.5f, 3.5f), V(0.6f, 1.0f, 0.6f), M("wood", "#a8703c", 0.3f));
        var gold = M("gold", "#ffd23a", 0.6f, new Color(0.6f, 0.45f, 0.1f));
        Prim(PrimitiveType.Cylinder, "TrophyCup", d, V(3.1f, 1.3f, 3.5f), V(0.36f, 0.18f, 0.36f), gold);
        Prim(PrimitiveType.Cylinder, "TrophyStem", d, V(3.1f, 1.1f, 3.5f), V(0.1f, 0.12f, 0.1f), gold);
        Block(blocks, 3.1f, 3.5f, 0.5f);
    }

    // ---------- buildings ----------

    static void MakeBuildings(Transform t)
    {
        string[] north = { "building_C", "building_G", "building_D", "building_H", "building_C", "building_G" };
        for (int i = 0; i < north.Length; i++)
            Fbx(City + north[i] + ".fbx", t, V(-20f + i * 8f, 0, 21f), 0f, CityScale, cityMat, "North_" + (i + 1));
        string[] west = { "building_A", "building_E", "building_B", "building_F" };
        string[] east = { "building_F", "building_A", "building_E", "building_B" };
        for (int i = 0; i < 4; i++)
        {
            Fbx(City + west[i] + ".fbx", t, V(-18.5f, 0, -10f + i * 8f), -90f, CityScale, cityMat, "West_" + (i + 1));
            Fbx(City + east[i] + ".fbx", t, V(18.5f, 0, -10f + i * 8f), 90f, CityScale, cityMat, "East_" + (i + 1));
        }
        Fbx(City + "building_E.fbx", t, V(-18.5f, 0, 14f), -90f, CityScale, cityMat, "West_5");
        Fbx(City + "building_D.fbx", t, V(18.5f, 0, 14f), 90f, CityScale, cityMat, "East_5");
        Fbx(City + "building_B.fbx", t, V(-17f, 0, -22f), 180f, CityScale, cityMat, "South_1");
        Fbx(City + "building_A.fbx", t, V(17f, 0, -22f), 180f, CityScale, cityMat, "South_2");
        Fbx(City + "building_E.fbx", t, V(0f, 0, -34f), 180f, CityScale, cityMat, "South_3");
    }
}
