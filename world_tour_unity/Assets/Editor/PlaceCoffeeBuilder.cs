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
using static PlaceKit;

public static partial class PlaceCoffeeBuilder
{
    public const string ScenePath = "Assets/World/Scenes/Place_Coffee.unity";
        // the shared pieces (helpers, materials, look, stage, contract, exit, particles) are in PlaceKit.cs (using static)

    public static void CreateAll() { Entry("PlaceCoffeeBuilder", MakeScene); }

    // ---------- the scene ----------

    static void MakeScene()
    {
        var scene = NewScene();
        InitKits();
        var look = Looks.Coffee();

        var profile = MakeProfile(look);
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

        var contract = new Contract
        {
            PlaceId = "coffee", RouteIndex = 0,
            Spawn = V(0, 0, -7.6f),   // close enough that the first crew member is on screen at the start
            StageSpot = V(9f, 0f, 9.2f),
            CrowdCenter = V(9f, 0, 3.3f), CrowdSize = new Vector2(8.2f, 4.3f),   // front row ends 0.8 m short of the ramp
            Exit = V(11.5f, 0, -9.5f),
            Crew = new[] { V(-5.0f, 0, 1.6f), V(3.0f, 0, 3.4f), V(-9.2f, 0, 5.9f) },
            Dog = V(-1.8f, 0, -9.6f),
        };
        Populate(contract, look, profile, blocks);

        FinishLook(look);
        MakeParticles();
        SaveScene(scene, ScenePath);
    }

    // ---------- ground, road, plaza ----------

    static void MakeGround(Transform t)
    {
        Prim(PrimitiveType.Plane, "Lawn", t, V(0, -0.03f, 0), V(24, 1, 24), Lit("lawn_warm", C("#7cbf68"), 0.05f), null, false);
        var plaza = Lit("plaza_art", C("#ffffff"), 0.12f);
        plaza.SetTexture("_BaseMap", PavingTexture("plaza_sidewalk"));
        plaza.SetTextureScale("_BaseMap", new Vector2(7f, 3.875f));
        Prim(PrimitiveType.Plane, "Plaza", t, V(0, 0, 6.25f), V(2.8f, 1, 1.55f), plaza, null, false);
        var path = Lit("path_art", C("#ffffff"), 0.12f);
        path.SetTexture("_BaseMap", PavingTexture("plaza_sidewalk"));
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
        Stage(t, blocks, new StageSpec { Cx = 9f, Cz = 10f, SpotStart = 5.4f, SpotSpacing = 1.44f });
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
        // leaves and blossom petals drifting down, warm sun motes
        Leaves("Leaves", V(0, 7f, 2f), V(30f, 1f, 26f), "#ffb3cf", "#c8d86a");
        Motes("SunMotes", V(0, 3f, 3f), V(30f, 5f, 26f), new Color(1.5f, 1.2f, 0.7f, 0.6f), Color.white, 50, 7f, new Vector2(0.1f, 0.22f), new Vector2(5f, 9f), 0.6f, 0f, "mote_particle");
    }
}
