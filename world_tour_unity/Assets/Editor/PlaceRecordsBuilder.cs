// Phase 8.2 builder A: Place_Records, "the record store basement, late morning". A brick street with the record shop front (a big spinning
// vinyl on the roof, record sleeves in the windows), and next to it the basement: a low brick cellar, open to the camera on its street side,
// with a stair coming down from the shop, beams and pipes overhead, a low stage with amp stacks and a drum kit, and a cat asleep on a bass case.
// The floor is flat (the walkers stay on y = 0), so the cellar reads as a basement by its brick, beams, stair and bulbs.
//
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -executeMethod PlaceRecordsBuilder.CreateAll
//
// Layout (x right, z away from the camera, walkers clamped to +-13.5): south sidewalk (spawn) -> road with a crossing -> north sidewalk with
// the record shop front along z = 4.4 (x -13.9 .. -1.5) and the cellar room east of it (x -1.5 .. 14, z 4.4 .. 14), stage at its north end.
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using WorldTour.World;
using static PlaceKit;

public static class PlaceRecordsBuilder
{
    public const string ScenePath = "Assets/World/Scenes/Place_Records.unity";
    const string P = "rs_";

    public static void CreateAll() { Entry("PlaceRecordsBuilder", Make); }

    static Material M(string n, string hex, float s = 0.15f, Color? e = null) { return Lit(P + n, C(hex), s, e); }

    public static void Make()
    {
        var scene = NewScene();
        InitKits(P, new Color(0.9f, 0.95f, 1.04f), false);
        var look = Looks.Records();
        var profile = MakeProfile(look);
        var world = Group("World").transform;
        var ground = Group("Ground", world).transform;
        var street = Group("Street", world).transform;
        var shop = Group("RecordShop", world).transform;
        var cellar = Group("Cellar", world).transform;
        var stage = Group("Stage", world).transform;
        var blds = Group("Buildings", world).transform;
        var props = Group("Props", world).transform;
        var blocks = Group("Blockers", world).transform;

        MakeGround(ground);
        MakeStreet(street, props, blocks);
        MakeShop(shop, props, blocks);
        MakeCellar(cellar, props, blocks);
        MakeStage(stage, props, blocks);
        MakeBuildings(blds);
        MakeDressing(world, props, blocks);

        var contract = new Contract
        {
            PlaceId = "records", RouteIndex = 1,
            Spawn = V(0, 0, -7.8f),
            StageSpot = V(8f, 0f, 10.4f),
            CrowdCenter = V(8f, 0f, 5.9f), CrowdSize = new Vector2(7.4f, 3.2f),
            Exit = V(-11.5f, 0f, -9.6f), ExitArrowYaw = 180f,
            Crew = new[] { V(-5.6f, 0f, 1.5f), V(3.4f, 0f, 0.6f), V(1.5f, 0f, 8.2f) },
            Dog = V(-1.8f, 0f, -9.8f),
        };
        Populate(contract, look, profile, blocks);
        FinishLook(look);
        Motes("DustMotes", V(6f, 2.6f, 9f), V(15f, 3.6f, 9f), new Color(1.5f, 1.25f, 0.8f, 0.55f), Color.white, 60, 9f, new Vector2(0.08f, 0.18f), new Vector2(5f, 9f), 0.5f, 0f, "rs_dust_particle");
        SaveScene(scene, ScenePath);
    }

    // ---------- ground, road ----------

    static void MakeGround(Transform t)
    {
        Prim(PrimitiveType.Plane, "Base", t, V(0, -0.03f, 0), V(24, 1, 24), M("base", "#8d97a3", 0.05f), null, false);
        var walk = PavingTexture("rs_walk", "#b6bfcb", "#abb5c3", "#9aa4b3", "#d9a07a", "#e8c3a6", 21, false);
        PavedPlane("SidewalkSouth", t, P + "walk_mat", walk, V(0, 0, -10.2f), new Vector2(28f, 6.6f), new Color(0.8f, 0.82f, 0.88f));
        PavedPlane("SidewalkNorth", t, P + "walk_mat", walk, V(0, 0, 1.2f), new Vector2(28f, 6.4f), new Color(0.8f, 0.82f, 0.88f));
        Prim(PrimitiveType.Plane, "Road", t, V(0, 0.01f, -4.4f), V(2.8f, 1, 0.48f), M("asphalt", "#4d5563", 0.06f), null, false);
        var cellarFloor = PavingTexture("rs_cellar_floor", "#6a6478", "#5d5870", "#443f55", "#d9a07a", "#e8c3a6", 31, false);
        PavedPlane("CellarFloor", t, P + "cellar_floor_mat", cellarFloor, V(6.2f, 0.015f, 9.2f), new Vector2(15.4f, 9.6f), new Color(0.95f, 0.92f, 1f));
    }

    static void MakeStreet(Transform t, Transform props, Transform blocks)
    {
        var paint = M("road_paint", "#fff8ee", 0.1f);
        var yellow = M("road_yellow", "#ffcf4a", 0.1f);
        for (int i = 0; i < 9; i++)
        {
            float x = -14.4f + i * 3.6f;
            if (Mathf.Abs(x) < 3f) continue;
            Prim(PrimitiveType.Cube, "Dash", t, V(x, 0.02f, -4.65f), V(1.6f, 0.02f, 0.12f), yellow, null, false);
            Prim(PrimitiveType.Cube, "Dash", t, V(x, 0.02f, -4.15f), V(1.6f, 0.02f, 0.12f), yellow, null, false);
        }
        for (int i = 0; i < 5; i++)
            Prim(PrimitiveType.Cube, "Zebra", t, V(-1.8f + i * 0.9f, 0.02f, -4.4f), V(0.5f, 0.02f, 4.5f), paint, null, false);
        var kerb = M("kerb", "#dfe3ea", 0.1f);
        foreach (float z in new[] { -2.0f, -6.8f })
        {
            Prim(PrimitiveType.Cube, "KerbW", t, V(-9.1f, 0.07f, z), V(13.8f, 0.14f, 0.3f), kerb);
            Prim(PrimitiveType.Cube, "KerbE", t, V(9.1f, 0.07f, z), V(13.8f, 0.14f, 0.3f), kerb);
        }
        // parked cars
        Fbx(City + "car_stationwagon.fbx", t, V(9.0f, 0f, -5.3f), 90f, CityScale, cityMat, "Wagon");
        Block(blocks, 7.4f, -5.3f, 0.9f); Block(blocks, 8.8f, -5.3f, 0.9f); Block(blocks, 10.3f, -5.3f, 0.9f);
        Fbx(City + "car_hatchback.fbx", t, V(-8.4f, 0f, -3.5f), -90f, CityScale, cityMat, "Hatchback");
        Block(blocks, -9.8f, -3.5f, 0.85f); Block(blocks, -8.4f, -3.5f, 0.85f); Block(blocks, -7.0f, -3.5f, 0.85f);
    }

    // ---------- the record shop ----------

    static void MakeShop(Transform t, Transform props, Transform blocks)
    {
        var brickTex = PatternTexture("rs_brick", "brick", "#bd5a42", "#a24a37", "#d9c9b4", 5);
        var brick = PatternMat(P + "brick_mat", brickTex);
        var trim = M("trim", "#23323d", 0.2f);
        var cream = M("cream", "#fff1dc", 0.15f);
        var teal = M("teal", "#1f8f8a", 0.2f);
        var wood = M("wood", "#9c6a46", 0.2f);
        var dark = M("dark_inside", "#3a2f3a", 0.1f);
        const float z0 = 4.4f, thick = 0.4f, zc = z0 + thick / 2f, x0 = -13.9f, x1 = -1.5f, h = 6.2f;

        // the body: back and side walls, roof, floor of the shop
        TexBox(t, "BackWall", V((x0 + x1) / 2f, h / 2f, 14.2f), V(x1 - x0, h, 0.4f), brick);
        TexBox(t, "SideWallW", V(x0 - 0.2f, h / 2f, 9.3f), V(0.4f, h, 9.8f), brick);
        TexBox(t, "SideWallE", V(x1 - 0.2f, h / 2f, 9.3f), V(0.4f, h, 9.8f), brick);   // the cellar's west wall
        Prim(PrimitiveType.Cube, "Floor", t, V((x0 + x1) / 2f, 0.03f, 9.3f), V(x1 - x0, 0.06f, 9.8f), wood, null, false);
        Prim(PrimitiveType.Cube, "Roof", t, V((x0 + x1) / 2f, h + 0.1f, 9.4f), V(x1 - x0 + 0.5f, 0.3f, 10.2f), M("roof", "#59606c", 0.1f));

        // the front, z = 4.4: piers, sills, a band over the openings (two windows and a door)
        float[] piers = { -13.9f, -8.0f, -5.0f, -2.0f };
        float[] pierW = { 1.9f, 1.0f, 1.0f, 0.5f };
        for (int i = 0; i < piers.Length; i++)
            TexBox(t, "Pier" + i, V(piers[i] + pierW[i] / 2f, h / 2f, zc), V(pierW[i], h, thick), brick);
        TexBox(t, "Band", V((x0 + x1) / 2f, (3.3f + h) / 2f, zc), V(x1 - x0, h - 3.3f, thick), brick);
        TexBox(t, "SillW1", V(-10.0f, 0.45f, zc), V(4.0f, 0.9f, thick), brick);
        TexBox(t, "SillW2", V(-3.0f, 0.45f, zc), V(2.0f, 0.9f, thick), brick);
        Prim(PrimitiveType.Cube, "ParapetCap", t, V((x0 + x1) / 2f, h + 0.3f, z0 + 0.1f), V(x1 - x0 + 0.6f, 0.22f, 0.8f), cream);
        // window and door frames
        foreach (var (cx, w, y0, y1) in new[] { (-10.0f, 4.0f, 0.9f, 3.3f), (-3.0f, 2.0f, 0.9f, 3.3f), (-6.0f, 2.0f, 0.0f, 3.3f) })
        {
            float yc = (y0 + y1) / 2f, hh = y1 - y0;
            Prim(PrimitiveType.Cube, "FrameL", t, V(cx - w / 2f, yc, z0 - 0.04f), V(0.12f, hh, 0.14f), trim);
            Prim(PrimitiveType.Cube, "FrameR", t, V(cx + w / 2f, yc, z0 - 0.04f), V(0.12f, hh, 0.14f), trim);
            Prim(PrimitiveType.Cube, "FrameT", t, V(cx, y1, z0 - 0.04f), V(w + 0.12f, 0.12f, 0.14f), trim);
        }
        // window displays: a bin of record sleeves behind each window, and a shelf row above it
        var rng = new System.Random(7);
        string[] sleeve = { "#ff7a3c", "#1f8f8a", "#fdb1cb", "#ffd23a", "#a8a9fc", "#f2efe6", "#e8503a", "#3b6fd4" };
        foreach (var (cx, w) in new[] { (-10.0f, 3.6f), (-3.0f, 1.6f) })
        {
            Prim(PrimitiveType.Cube, "DisplayBin", t, V(cx, 1.05f, 5.1f), V(w, 0.3f, 0.7f), wood);
            int n = Mathf.RoundToInt(w / 0.14f);
            for (int i = 0; i < n; i++)
            {
                float x = cx - w / 2f + 0.1f + i * ((w - 0.2f) / (n - 1));
                var sm = M("sleeve" + (i % sleeve.Length), sleeve[Mathf.Abs(i + (int)cx) % sleeve.Length], 0.2f);
                Prim(PrimitiveType.Cube, "Sleeve", t, V(x, 1.5f, 5.05f + (float)rng.NextDouble() * 0.12f), V(0.1f, 0.62f, 0.62f), sm, V(-10 + (float)rng.NextDouble() * 20, 0, 0), false);
            }
            // shelf of tall sleeves leaning on the back wall, seen above the bin
            Prim(PrimitiveType.Cube, "DisplayShelf", t, V(cx, 2.45f, 5.6f), V(w, 0.08f, 0.5f), wood);
            for (int i = 0; i < n; i++)
            {
                float x = cx - w / 2f + 0.1f + i * ((w - 0.2f) / (n - 1));
                Prim(PrimitiveType.Cube, "Sleeve", t, V(x, 2.85f, 5.55f), V(0.1f, 0.7f, 0.5f), M("sleeve" + ((i + 3) % sleeve.Length), sleeve[(i + 3) % sleeve.Length], 0.2f), null, false);
            }
        }
        // a boombox on the counter inside the door
        Prim(PrimitiveType.Cube, "Boombox", t, V(-6.0f, 1.0f, 6.4f), V(1.0f, 0.55f, 0.4f), M("boombox", "#2d2fa0", 0.3f));
        foreach (float sx in new[] { -0.28f, 0.28f }) Prim(PrimitiveType.Cylinder, "BoomCone", t, V(-6.0f + sx, 1.0f, 6.18f), V(0.3f, 0.012f, 0.3f), M("boomcone", "#d8c9a0", 0.3f), V(90, 0, 0), false);

        // awning: teal and cream slats slanting out over the front
        var stripeA = M("awning_teal", "#1f8f8a", 0.25f); var stripeB = M("awning_cream", "#fff6ea", 0.25f);
        for (int i = 0; i < 16; i++)
        {
            float x = -13.5f + i * 0.8f;
            var m = i % 2 == 0 ? stripeA : stripeB;
            Prim(PrimitiveType.Cube, "Awning", t, V(x, 3.1f, z0 - 1.3f), V(0.8f, 0.07f, 2.45f), m, V(-20, 0, 0));
            Prim(PrimitiveType.Cube, "Valance", t, V(x, 2.62f, z0 - 2.2f), V(0.8f, 0.3f, 0.07f), m);
        }
        // the sign board over the awning and the giant vinyl hanging off the corner, turning slowly
        var board = Prim(PrimitiveType.Cube, "SignBoard", t, V(-7.2f, 4.75f, z0 - 0.12f), V(5.6f, 1.25f, 0.2f), M("board", "#1b2a36", 0.25f));
        PixelText(t, "RECORDS", V(-7.2f, 4.75f, z0 - 0.26f), 0.105f, M("board_text", "#fff1dc", 0.3f, new Color(1.1f, 1.0f, 0.85f)), "rs_text_records", 0.5f);
        var disc = Vinyl(t, "BigVinyl", V(-11.9f, 4.95f, z0 - 0.55f), 3.0f, "#ff7a3c", 0f, P);
        var spin = disc.AddComponent<PropSpin>(); spin.axis = Vector3.forward; spin.degreesPerSecond = -14f;
        Prim(PrimitiveType.Cylinder, "VinylArm", t, V(-11.9f, 4.95f, z0 - 0.2f), V(0.16f, 0.2f, 0.16f), trim, V(90, 0, 0));
        Prim(PrimitiveType.Cube, "VinylBracket", t, V(-11.9f, 4.95f, z0 - 0.04f), V(0.5f, 0.5f, 0.1f), trim);
        // the open sign hanging in the door
        var open = NeonMat(P + "neon_open", "#ff6b35", 2.4f);
        var openGo = PixelText(t, "OPEN", V(-6.0f, 3.0f, z0 - 0.02f), 0.075f, open, "rs_text_open", 0.5f);
        MakeGlow(openGo, new Color(1f, 0.42f, 0.2f) * 2.4f, 4f);
        Block(blocks, -13.0f, 5.0f, 0.7f);
        for (float x = -12.4f; x <= -1.8f; x += 1.7f) { if (x > -8.4f && x < -4.6f) continue; Block(blocks, x, 5.0f, 0.7f); }   // keep walkers off the wall line, the door stays open
        Block(blocks, -6.0f, 6.9f, 0.4f);
    }

    // ---------- the cellar ----------

    static void MakeCellar(Transform t, Transform props, Transform blocks)
    {
        var brickTex = PatternTexture("rs_cellar_brick", "brick", "#8c4e43", "#744039", "#b3a395", 9);
        var brick = PatternMat(P + "cellar_brick_mat", brickTex, new Color(1f, 0.97f, 1f));
        var beam = M("beam", "#4a3a3a", 0.2f);
        var pipe = M("pipe", "#9aa3b2", 0.5f);
        var copper = M("copper", "#c8794a", 0.6f);
        var trim = M("trim", "#23323d", 0.2f);
        var wood = M("wood", "#9c6a46", 0.2f);
        const float xa = -1.5f, xb = 14.0f, z0 = 4.4f, z1 = 14.2f;

        // walls: east and back (the west one is the shop's)
        TexBox(t, "WallE", V(xb + 0.1f, 3.25f, (z0 + z1) / 2f), V(0.4f, 6.5f, z1 - z0), brick);
        TexBox(t, "WallN", V((xa + xb) / 2f, 3.25f, z1 - 0.1f), V(xb - xa + 0.4f, 6.5f, 0.4f), brick);
        // front: open (the cutaway the camera looks in through), a low brick kerb along it
        TexBox(t, "FrontKerb", V((xa + xb) / 2f, 0.15f, z0 + 0.1f), V(xb - xa, 0.3f, 0.3f), brick);
        // overhead: beams and pipes along the lanes (nothing across, so the stage stays in view), bulbs hanging
        foreach (float bx in new[] { 0.7f, 12.9f })
            Prim(PrimitiveType.Cube, "Beam", t, V(bx, 3.1f, 6.9f), V(0.3f, 0.3f, 5.0f), beam);
        Prim(PrimitiveType.Cylinder, "Pipe", t, V(1.7f, 2.85f, 6.9f), V(0.16f, 2.5f, 0.16f), pipe, V(90, 0, 0));
        Prim(PrimitiveType.Cylinder, "PipeCopper", t, V(12.0f, 2.8f, 6.9f), V(0.1f, 2.5f, 0.1f), copper, V(90, 0, 0));
        var bulbMat = M("bulb", "#fff0c8", 0.3f, new Color(3.3f, 2.3f, 1.0f));
        var wire = new MB(); var bulbs = new MB();
        StringLine(wire, bulbs, V(2.6f, 3.0f, 4.6f), V(2.6f, 3.0f, 9.0f), 0.5f, 0.7f, 0.1f);
        StringLine(wire, bulbs, V(11.6f, 3.0f, 4.6f), V(11.6f, 3.0f, 9.0f), 0.5f, 0.7f, 0.1f);
        StringLine(wire, bulbs, V(2.6f, 3.0f, 9.0f), V(11.6f, 3.0f, 9.0f), 0.8f, 0.8f, 0.09f);
        EmitStrings(t, wire, bulbs, "rs", C("#fff0c8"), new Color(3.3f, 2.3f, 1.0f));
        foreach (float px in new[] { 0.7f, 12.9f })
        {
            Prim(PrimitiveType.Cylinder, "Cord", t, V(px, 3.0f, 5.6f), V(0.03f, 0.3f, 0.03f), trim, null, false);
            Prim(PrimitiveType.Sphere, "Pendant", t, V(px, 2.62f, 5.6f), V(0.42f, 0.42f, 0.42f), bulbMat, null, false);
        }
        // a blade sign on a pole at the corner of the shop: BASEMENT, and an arrow toward the room
        var neon = NeonMat(P + "neon_live", "#ffb02e", 2.6f);
        Prim(PrimitiveType.Cylinder, "SignPole", t, V(-0.6f, 2.2f, 3.0f), V(0.12f, 2.2f, 0.12f), trim);
        Prim(PrimitiveType.Cube, "SignBoard", t, V(1.75f, 3.7f, 3.0f), V(4.7f, 1.0f, 0.16f), M("board", "#1b2a36", 0.25f));
        var txt = PixelText(t, "BASEMENT", V(1.55f, 3.7f, 2.89f), 0.075f, neon, "rs_text_basement", 0.5f);
        MakeGlow(txt, new Color(1f, 0.7f, 0.18f) * 2.6f, 5f);
        var arrowGo = PixelText(t, ">", V(3.75f, 3.7f, 2.89f), 0.1f, neon, "rs_text_arrow", 0.5f);
        MakeGlow(arrowGo, new Color(1f, 0.7f, 0.18f) * 2.6f, 0f);
        Block(blocks, -0.6f, 3.0f, 0.3f);

        // the stair coming down from the shop (a door high in the west wall, steps along the back wall; solid underneath)
        var step = M("step", "#6b5a58", 0.2f);
        var rail = M("rail", "#23323d", 0.4f);
        const int steps = 11;
        for (int i = 0; i < steps; i++)
        {
            float x = 3.2f - i * 0.4f, top = 0.3f * (i + 1);
            TexBox(t, "Step", V(x, top / 2f, 13.2f), V(0.4f, top, 1.3f), brick, 1f);
            Prim(PrimitiveType.Cube, "StepTop", t, V(x, top + 0.01f, 13.2f), V(0.4f, 0.04f, 1.3f), step, null, false);
        }
        for (int i = 0; i < steps; i += 2) Prim(PrimitiveType.Cylinder, "RailPost", t, V(3.2f - i * 0.4f, 0.3f * (i + 1) + 0.5f, 12.55f), V(0.05f, 0.5f, 0.05f), rail, null, false);
        var railBar = Prim(PrimitiveType.Cylinder, "Rail", t, V(1.3f, 2.1f, 12.55f), V(0.06f, 2.75f, 0.06f), rail, V(0, 0, 90f - Mathf.Atan2(0.3f, 0.4f) * Mathf.Rad2Deg));
        // a warm door at the top
        Prim(PrimitiveType.Cube, "DoorGlow", t, V(-1.08f, 3.6f, 13.2f), V(0.08f, 1.5f, 1.05f), M("door_glow", "#fff3c4", 0.2f, new Color(2.2f, 1.9f, 1.1f)), null, false);
        Prim(PrimitiveType.Cube, "DoorFrame", t, V(-1.06f, 3.6f, 12.6f), V(0.14f, 1.7f, 0.12f), trim, null, false);
        Prim(PrimitiveType.Cube, "DoorFrame", t, V(-1.06f, 3.6f, 13.8f), V(0.14f, 1.7f, 0.12f), trim, null, false);
        Prim(PrimitiveType.Cube, "DoorFrame", t, V(-1.06f, 4.45f, 13.2f), V(0.14f, 0.12f, 1.3f), trim, null, false);
        BlockLine(blocks, new Vector2(-1.0f, 13.2f), new Vector2(3.2f, 13.2f), 0.8f, 1.0f);

        // posters on the west wall, and old record sleeves on the back wall above the stage
        string[] pcol = { "#ff7a3c", "#1f8f8a", "#fdb1cb", "#ffd23a", "#a8a9fc" };
        var star = M("poster_star", "#fff8ee", 0.2f, new Color(0.35f, 0.33f, 0.3f));
        for (int i = 0; i < 5; i++)
        {
            float pz = 5.6f + i * 1.9f;
            if (pz > 11f) break;
            Prim(PrimitiveType.Cube, "Poster", t, V(-1.07f, 2.0f + (i % 2) * 0.45f, pz), V(0.04f, 1.1f, 0.85f), M("poster" + i, pcol[i % pcol.Length], 0.2f), null, false);
            StarSign("PosterStar", t, V(-1.04f, 2.05f + (i % 2) * 0.45f, pz), 0.4f, star, 90f, 0.2f);
        }
    }

    // ---------- the stage ----------

    static void MakeStage(Transform t, Transform props, Transform blocks)
    {
        Stage(t, blocks, new StageSpec
        {
            Prefix = P, Cx = 8f, Cz = 11.2f, Width = 8f, Depth = 4.4f, Height = 0.35f, RampLength = 0.6f,
            Deck = "#4a3030", Trim = "#ff7a3c", TrimGlow = new Color(0.7f, 0.25f, 0.08f), Wall = "#2b2438", Dark = "#1b1a28", Truss = "#8c8fa6", Ring = "#ff7a3c",
            BannerStars = false, Speakers = false, WallHeight = 3.8f, TrussHeight = 3.55f, SpotHeight = 3.3f, BeamLength = 3.8f, BeamRadius = 0.85f,
            Lens = new[] { new Color(3f, 1.4f, 0.4f), new Color(0.6f, 2.4f, 2.2f), new Color(3f, 0.8f, 1.2f), new Color(3f, 2.4f, 0.7f), new Color(0.6f, 2.4f, 2.2f) },
            SpotStart = 4.6f, SpotSpacing = 1.7f,
        });
        // backdrop: three vinyls on the stage wall
        Vinyl(t, "WallVinylA", V(5.6f, 2.35f, 13.33f), 1.5f, "#1f8f8a", 0f, P);
        Vinyl(t, "WallVinylB", V(8.0f, 2.6f, 13.33f), 2.1f, "#ff7a3c", 0f, P);
        Vinyl(t, "WallVinylC", V(10.5f, 2.3f, 13.33f), 1.4f, "#fdb1cb", 0f, P);
        // gear: amp stacks at both ends, the drum kit at the back, a mic (the kit's), and a bass amp
        AmpStack(t, V(4.7f, 0.35f, 12.2f), 20f, P, 1.1f);
        AmpStack(t, V(11.3f, 0.35f, 12.2f), -20f, P, 1.1f);
        DrumKit(t, V(8.0f, 0.35f, 11.9f), 0f, P);
        Block(blocks, 4.7f, 12.2f, 0.8f); Block(blocks, 11.3f, 12.2f, 0.8f);
        // a low wedge monitor and a cable on the deck
        Prim(PrimitiveType.Cube, "Monitor", t, V(7.0f, 0.55f, 9.7f), V(0.8f, 0.35f, 0.45f), M("monitor", "#26262e", 0.3f), V(-25, 0, 0));
        Prim(PrimitiveType.Cube, "Cable", t, V(9.4f, 0.37f, 10.2f), V(1.6f, 0.02f, 0.05f), M("cable", "#111116", 0.2f), V(0, 25, 0), false);
    }

    // ---------- buildings ----------

    static void MakeBuildings(Transform t)
    {
        string[] north = { "building_G", "building_C", "building_D", "building_H", "building_B", "building_E" };
        for (int i = 0; i < north.Length; i++)
            Fbx(City + north[i] + ".fbx", t, V(-20f + i * 8f, 0, 20.4f), 0f, CityScale, cityMat, "North_" + (i + 1));
        string[] west = { "building_E", "building_A", "building_F", "building_C" };
        string[] east = { "building_D", "building_E", "building_G", "building_A" };
        for (int i = 0; i < 4; i++)
        {
            Fbx(City + west[i] + ".fbx", t, V(-18.5f, 0, -10f + i * 8f), -90f, CityScale, cityMat, "West_" + (i + 1));
            Fbx(City + east[i] + ".fbx", t, V(18.5f, 0, -10f + i * 8f), 90f, CityScale, cityMat, "East_" + (i + 1));
        }
        Fbx(City + "building_C.fbx", t, V(-18.5f, 0, 14f), -90f, CityScale, cityMat, "West_5");
        Fbx(City + "building_H.fbx", t, V(18.5f, 0, 14f), 90f, CityScale, cityMat, "East_5");
        Fbx(City + "building_B.fbx", t, V(-17f, 0, -22f), 180f, CityScale, cityMat, "South_1");
        Fbx(City + "building_F.fbx", t, V(17f, 0, -22f), 180f, CityScale, cityMat, "South_2");
        Fbx(City + "building_A.fbx", t, V(0f, 0, -34f), 180f, CityScale, cityMat, "South_3");
    }

    // ---------- street dressing ----------

    static void MakeDressing(Transform world, Transform props, Transform blocks)
    {
        var d = Group("Dressing", world).transform;
        var trim = M("trim", "#23323d", 0.2f);
        var wood = M("wood", "#9c6a46", 0.2f);
        // street lamps along the north sidewalk and benches on the south one, with the string lights of the cellar kept inside
        foreach (float x in new[] { -12.3f, -4.2f, 4.3f, 12.3f })
        {
            Fbx(City + "streetlight.fbx", d, V(x, 0, -1.55f), 0f, CityScale, cityMat, "Streetlight").AddComponent<Occluder>();
            Block(blocks, x, -1.55f, 0.3f);
        }
        Fbx(City + "firehydrant.fbx", d, V(-1.9f, 0, -1.6f), 0f, CityScale, cityMat, "Hydrant"); Block(blocks, -1.9f, -1.6f, 0.35f);
        Fbx(City + "dumpster.fbx", d, V(13.0f, 0, 1.6f), -90f, CityScale * 0.8f, cityMat, "Dumpster"); Block(blocks, 13.0f, 1.6f, 1.0f);
        Fbx(City + "trash_B.fbx", d, V(0.9f, 0, -1.6f), 20f, CityScale, cityMat, "Bin"); Block(blocks, 0.9f, -1.6f, 0.35f);
        var benches = new[] { (-4.2f, -9.6f, 0f), (4.2f, -9.6f, 0f), (10.5f, -12.2f, 0f) };
        foreach (var b in benches)
        {
            Fbx(City + "bench.fbx", d, V(b.Item1, 0, b.Item2), b.Item3 + 90f, CityScale, cityMat, "Bench");
            Block(blocks, b.Item1, b.Item2, 0.8f);
        }
        // sale crates on the sidewalk in front of the shop: a table and two crates of sleeves
        var sale = Group("SaleTable", d).transform; sale.position = V(-9.4f, 0, 2.3f);
        Prim(PrimitiveType.Cube, "TableTop", sale, V(0, 0.85f, 0), V(2.6f, 0.08f, 0.9f), wood);
        foreach (float lx in new[] { -1.15f, 1.15f }) Prim(PrimitiveType.Cube, "Leg", sale, V(lx, 0.42f, 0), V(0.08f, 0.85f, 0.7f), trim);
        string[] sleeve = { "#ff7a3c", "#1f8f8a", "#fdb1cb", "#ffd23a", "#a8a9fc", "#f2efe6" };
        for (int c = 0; c < 2; c++)
        {
            Prim(PrimitiveType.Cube, "Crate", sale, V(-0.6f + c * 1.2f, 1.02f, 0f), V(1.0f, 0.28f, 0.6f), wood);
            for (int i = 0; i < 9; i++)
                Prim(PrimitiveType.Cube, "Sleeve", sale, V(-1.0f + c * 1.2f + i * 0.1f, 1.38f, 0.02f), V(0.09f, 0.5f, 0.5f), M("sleeve" + ((i + c * 2) % sleeve.Length), sleeve[(i + c * 2) % sleeve.Length], 0.2f), V((i % 3 - 1) * 5f, 0, 0), false);
        }
        Block(blocks, -9.4f, 2.3f, 1.1f); Block(blocks, -10.3f, 2.3f, 0.8f); Block(blocks, -8.5f, 2.3f, 0.8f);
        // planters with trees on the south sidewalk, bushes
        var forest = ForestTinted("records_green", "#6fc27a", "#2f8a5c");
        string[] kinds = { "Tree_3_A_Color1", "Tree_2_A_Color1", "Tree_1_A_Color1", "Tree_4_A_Color1" };
        float[] tx = { -11.8f, 11.8f, -13.0f, 13.0f };
        float[] tz = { -9.2f, -9.6f, -12.8f, -12.4f };
        for (int i = 0; i < 4; i++)
        {
            Prim(PrimitiveType.Cube, "Planter", d, V(tx[i], 0.2f, tz[i]), V(1.6f, 0.4f, 1.6f), M("planter", "#d9d4cc", 0.15f));
            Fbx(Forest + kinds[i] + ".fbx", d, V(tx[i], 0.35f, tz[i]), i * 70f, 1.0f, forest, "Tree");
            Block(blocks, tx[i], tz[i], 0.95f);
        }
        // a cat-sized bus stop shelter would crowd the walk: instead a bike rack, built of tubes
        for (int i = 0; i < 5; i++) Prim(PrimitiveType.Cylinder, "BikeRack", d, V(6.6f + i * 0.45f, 0.4f, -12.4f), V(0.05f, 0.4f, 0.05f), trim);
        Prim(PrimitiveType.Cylinder, "BikeRackBar", d, V(6.6f + 0.9f, 0.7f, -12.4f), V(0.05f, 0.95f, 0.05f), trim, V(0, 0, 90));
        // rooftop clutter above the shop so the roofline is not bare
        Prim(PrimitiveType.Cube, "AirCon", d, V(-3.2f, 6.75f, 9.0f), V(1.6f, 0.9f, 1.2f), M("aircon", "#c9ccd6", 0.4f));
        Prim(PrimitiveType.Cylinder, "Fan", d, V(-3.2f, 7.23f, 9.0f), V(0.9f, 0.03f, 0.9f), trim);
        Prim(PrimitiveType.Cube, "Vent", d, V(-11.5f, 6.75f, 11.0f), V(0.9f, 0.9f, 0.9f), M("vent", "#8c93a3", 0.3f));
        // the cat asleep on a bass case, in the cellar's west lane
        BassCaseAndCat(d, V(2.5f, 0f, 9.3f), blocks);
    }

    static void BassCaseAndCat(Transform parent, Vector3 pos, Transform blocks)
    {
        var root = Group("BassCase", parent).transform;
        root.position = pos; root.rotation = Quaternion.Euler(0, 12f, 0);
        var caseMat = M("case", "#2a2a33", 0.35f);
        var steel = M("case_steel", "#c9ccdf", 0.7f);
        Prim(PrimitiveType.Cube, "Case", root, V(0, 0.2f, 0), V(1.7f, 0.4f, 0.62f), caseMat);
        Prim(PrimitiveType.Cube, "CaseLid", root, V(0, 0.43f, 0), V(1.62f, 0.06f, 0.55f), M("case_lid", "#3a3a46", 0.35f));
        foreach (float lx in new[] { -0.55f, 0.55f })
        {
            Prim(PrimitiveType.Cube, "Latch", root, V(lx, 0.32f, -0.32f), V(0.18f, 0.1f, 0.03f), steel, null, false);
        }
        Prim(PrimitiveType.Cube, "Handle", root, V(0, 0.28f, -0.32f), V(0.34f, 0.05f, 0.03f), steel, null, false);
        foreach (float cx in new[] { -0.8f, 0.8f }) Prim(PrimitiveType.Cube, "Corner", root, V(cx, 0.2f, 0), V(0.08f, 0.42f, 0.64f), steel, null, false);
        // the cat: a ginger loaf with a white belly and tail, eyes shut
        var cat = Group("Cat", root).transform;
        cat.localPosition = V(0.1f, 0.47f, 0);
        var fur = M("cat_fur", "#f2a048", 0.2f);
        var furD = M("cat_stripe", "#c9742a", 0.2f);
        var white = M("cat_white", "#fff4e6", 0.2f);
        var pink = M("cat_pink", "#ff9fb4", 0.2f);
        var body = Prim(PrimitiveType.Sphere, "CatBody", cat, V(0, 0.2f, 0), V(0.85f, 0.42f, 0.5f), fur);
        body.AddComponent<PropBreathe>();
        Prim(PrimitiveType.Sphere, "CatBelly", cat, V(-0.05f, 0.1f, -0.16f), V(0.55f, 0.2f, 0.3f), white, null, false);
        Prim(PrimitiveType.Sphere, "CatHead", cat, V(0.42f, 0.26f, -0.08f), V(0.38f, 0.32f, 0.34f), fur);
        foreach (float ez in new[] { -0.2f, 0.04f })
        {
            var ear = new MB(); ear.Gem(Vector3.zero, 0.07f, 0.1f, 0.05f);
            var eo = MeshObject("CatEar", cat, ear.ToMesh("rs_cat_ear"), fur, true);
            eo.transform.localPosition = V(0.5f, 0.47f, ez); eo.transform.localRotation = Quaternion.Euler(0, 0, -15f);
        }
        foreach (float ez in new[] { -0.19f, 0.03f })
            Prim(PrimitiveType.Cube, "CatEye", cat, V(0.6f, 0.3f, ez - 0.03f), V(0.015f, 0.025f, 0.09f), M("cat_eye", "#3a2a2a", 0.1f), V(0, 0, 0), false);
        Prim(PrimitiveType.Sphere, "CatNose", cat, V(0.61f, 0.23f, -0.08f), V(0.05f, 0.04f, 0.05f), pink, null, false);
        // tail curling round the front, with two stripes
        for (int i = 0; i < 6; i++)
        {
            float a = i / 5f * 2.2f;
            Prim(PrimitiveType.Sphere, "CatTail", cat, V(-0.35f - Mathf.Sin(a) * 0.28f, 0.1f, -0.2f - (1f - Mathf.Cos(a)) * 0.3f), V(0.14f, 0.14f, 0.14f), i % 2 == 0 ? fur : furD, null, false);
        }
        Prim(PrimitiveType.Sphere, "CatPaw", cat, V(0.45f, 0.06f, -0.3f), V(0.14f, 0.1f, 0.14f), white, null, false);
        // a little "z z" over its head
        var zMat = NeonMat(P + "zzz", "#fff4e6", 1.2f);
        PixelText(cat, "Z", V(0.55f, 0.85f, -0.25f), 0.05f, zMat, "rs_text_z1", 0.4f);
        PixelText(cat, "Z", V(0.75f, 1.12f, -0.25f), 0.07f, zMat, "rs_text_z2", 0.4f);
        Block(blocks, pos.x, pos.z, 0.95f);
    }
}
