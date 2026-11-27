// Phase 10B.1: the clothing boutique, a small interior scene (Assets/World/Scenes/Interior_Boutique.unity), and the door that leads to it
// on the coffee street. Built from the CC0 KayKit pieces already imported (Restaurant counter, Furniture couch, armchair, lamp and cactus) plus
// Unity primitives, the same way the places are. Re-runnable.
//
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -executeMethod InteriorBoutiqueBuilder.CreateAll      (both of the below)
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -executeMethod InteriorBoutiqueBuilder.CreateScene    (the boutique scene)
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -executeMethod InteriorBoutiqueBuilder.AddCoffeeDoor  (the door in Place_Coffee)
//
// Room layout (x right, z away from the camera, metres; she walks inside +-5.8): floor 13 x 13, the back wall at z = 6.5, side walls at x = +-6.5,
// no front wall (the camera looks in from the south), no roof. Back wall: hats, hair, mirror, glasses, bags. Left wall: tops, bottoms.
// Right wall: shoes, then the counter with the shopkeeper. A couch and a rug make the middle homely; the mat at the front is the way out.
// The gameplay finds everything by name (BoutiqueGame): PlayerSpawn, DoorOut, Shopkeeper, Mirror (child Stand), Rack_<slot> (child Stand).
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WorldTour.Core.Meta;
using WorldTour.World;
using WorldTour.World.Gameplay;
using static PlaceKit;

public static class InteriorBoutiqueBuilder
{
    public const string ScenePath = "Assets/World/Scenes/Interior_Boutique.unity";
    const string CoffeeScene = "Assets/World/Scenes/Place_Coffee.unity";

    /// <summary>The boutique scene and the door in Place_Coffee, in one run.</summary>
    public static void CreateAll() { Entry("InteriorBoutiqueBuilder", () => { MakeScene(); PatchCoffee(); }); }
    public static void CreateScene() { Entry("InteriorBoutiqueBuilder.CreateScene", MakeScene); }
    public static void AddCoffeeDoor() { Entry("InteriorBoutiqueBuilder.AddCoffeeDoor", PatchCoffee); }

    static Color Pal(int i) { return C(AvatarCatalogue.PaletteHex[((i % AvatarCatalogue.PaletteHex.Count) + AvatarCatalogue.PaletteHex.Count) % AvatarCatalogue.PaletteHex.Count]); }

    static PlaceKit.Look BoutiqueLook()
    {
        return new PlaceKit.Look
        {
            ProfileName = "Boutique", SkyMaterial = "sky_boutique",
            SunColor = new Color(1f, 0.9f, 0.78f), SunIntensity = 1.35f, SunEuler = new Vector3(52f, -28f, 0f), SunShadowStrength = 0.6f,
            AmbSky = new Color(0.96f, 0.9f, 0.95f), AmbEquator = new Color(1f, 0.92f, 0.9f), AmbGround = new Color(0.72f, 0.64f, 0.68f), AmbIntensity = 1.15f,
            Fog = new Color(0.95f, 0.88f, 0.92f), FogDensity = 0f,
            CameraBackground = new Color(0.95f, 0.85f, 0.9f),
            BloomIntensity = 0.25f, VignetteIntensity = 0.16f,
        };
    }

    // ---------- the scene ----------

    static void MakeScene()
    {
        var scene = NewScene();
        InitKits("", null, false);
        var look = BoutiqueLook();
        var profile = MakeProfile(look);
        var world = Group("World").transform;
        var room = Group("Room", world).transform;
        var racks = Group("Racks", world).transform;
        var props = Group("Props", world).transform;
        var blocks = Group("Blockers", world).transform;

        MakeRoom(room);
        MakeBackWall(racks, blocks);
        MakeLeftWall(racks, blocks);
        MakeRightWall(racks, props, blocks);
        MakeLounge(props, blocks);
        MakeDoorMat(room);
        Populate(look, profile, blocks, props);
        FinishLook(look);
        RenderSettings.fog = false;
        SaveScene(scene, ScenePath);
    }

    static GameObject Empty(string name, Transform parent, Vector3 pos)
    {
        var g = Group(name, parent);
        g.transform.position = pos;
        return g;
    }

    // ---------- room ----------

    static void MakeRoom(Transform t)
    {
        var wood = Lit("bq_wood", C("#e2b283"), 0.25f);
        var cream = Lit("bq_cream", C("#fff3e2"), 0.08f);
        var pink = Lit("bq_pink", C("#f6a6c3"), 0.15f);
        var rug = Lit("bq_rug", C("#fdb1cb"), 0.05f);
        var rugEdge = Lit("bq_rug_edge", C("#fff6ea"), 0.05f);
        var trim = Lit("bq_trim", C("#e98bb0"), 0.2f);
        Prim(PrimitiveType.Cube, "Floor", t, V(0, -0.06f, 0.1f), V(13.6f, 0.12f, 13.4f), wood, null, false);
        Prim(PrimitiveType.Cube, "RugEdge", t, V(0, 0.012f, 0.4f), V(7.6f, 0.02f, 5.0f), rugEdge, null, false);
        Prim(PrimitiveType.Cube, "Rug", t, V(0, 0.026f, 0.4f), V(7.0f, 0.02f, 4.4f), rug, null, false);
        // walls: back and sides, with a pink skirting and a top trim; nothing on the south side
        Prim(PrimitiveType.Cube, "BackWall", t, V(0, 2.1f, 6.7f), V(14f, 4.2f, 0.4f), cream);
        Prim(PrimitiveType.Cube, "BackSkirt", t, V(0, 0.55f, 6.47f), V(13.1f, 1.1f, 0.06f), pink, null, false);
        Prim(PrimitiveType.Cube, "BackTrim", t, V(0, 4.12f, 6.46f), V(13.1f, 0.14f, 0.08f), trim, null, false);
        foreach (int sgn in new[] { -1, 1 })
        {
            string n = sgn < 0 ? "W" : "E";
            Prim(PrimitiveType.Cube, "SideWall" + n, t, V(6.7f * sgn, 2.1f, 0.1f), V(0.4f, 4.2f, 13.6f), cream);
            Prim(PrimitiveType.Cube, "SideSkirt" + n, t, V(6.47f * sgn, 0.55f, 0.1f), V(0.06f, 1.1f, 13.0f), pink, null, false);
            Prim(PrimitiveType.Cube, "SideTrim" + n, t, V(6.46f * sgn, 4.12f, 0.1f), V(0.08f, 0.14f, 13.0f), trim, null, false);
        }
        // the front edge: a low pink kerb along the open side, so the room has an edge
        Prim(PrimitiveType.Cube, "FrontKerb", t, V(0, 0.07f, -6.55f), V(13.6f, 0.14f, 0.3f), trim);
    }

    static void MakeDoorMat(Transform t)
    {
        var mat = Lit("bq_doormat", C("#a8a9fc"), 0.1f, new Color(0.35f, 0.35f, 0.9f));
        var white = Lit("bq_mat_text", C("#ffffff"), 0.1f, new Color(0.9f, 0.9f, 1f));
        var door = Empty("DoorOut", null, V(0, 0, -5.0f));
        Prim(PrimitiveType.Cube, "Mat", door.transform, V(0, 0.03f, 0), V(3.0f, 0.04f, 1.5f), mat, null, false);
        var text = PixelText(door.transform, "OUT", V(0, 0.07f, 0), 0.12f, white, "bq_text_out", 0.2f);
        text.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        // a small arrow toward the street (south)
        var arrow = Group("Arrow", door.transform).transform;
        Prim(PrimitiveType.Cube, "ArrowShaft", arrow, V(0, 0.07f, -0.5f), V(0.1f, 0.02f, 0.3f), white, null, false);
    }

    // ---------- the back wall: hats, hair, mirror, glasses, bags ----------

    static void MakeBackWall(Transform racks, Transform blocks)
    {
        var wood = Lit("bq_shelf", C("#d9a273"), 0.25f);
        var white = Lit("bq_white", C("#fffaf3"), 0.2f);
        var gold = Lit("bq_gold", C("#e8b94a"), 0.5f);
        var glass = Lit("bq_mirror", C("#b9d8f0"), 0.9f, new Color(0.04f, 0.07f, 0.1f));
        var sign = Lit("bq_sign", C("#8fe3c4"), 0.2f);
        var ink = Lit("bq_ink", C("#2b2350"), 0.2f);

        // the sign over the mirror
        var panel = Prim(PrimitiveType.Cube, "SignPanel", racks, V(0.2f, 3.55f, 6.2f), V(4.2f, 0.9f, 0.18f), sign);
        PixelText(panel.transform, "BOUTIQUE", V(0, 0, -0.62f), 0.1f, ink, "bq_text_boutique", 0.4f);

        // the mirror: a gold frame round a pale glass, with a stand mark on the rug in front of it
        var mirror = Empty("Mirror", racks, V(0.2f, 0, 6.3f));
        Prim(PrimitiveType.Cube, "Frame", mirror.transform, V(0, 1.7f, 0.05f), V(2.5f, 3.2f, 0.12f), gold);
        Prim(PrimitiveType.Cube, "Glass", mirror.transform, V(0, 1.7f, -0.03f), V(2.2f, 2.9f, 0.05f), glass, null, false);
        Prim(PrimitiveType.Cube, "Shine", mirror.transform, V(-0.55f, 1.9f, -0.065f), V(0.18f, 1.8f, 0.01f), white, V(0, 0, 14f), false);
        var stand = Empty("Stand", mirror.transform, V(0.2f, 0, 3.4f));
        Prim(PrimitiveType.Cylinder, "TryOnSpot", stand.transform, V(0, 0.04f, 0), V(1.5f, 0.012f, 1.5f), Lit("bq_spot", C("#fff0a8"), 0.1f, new Color(0.9f, 0.8f, 0.3f)), null, false);
        BlockLine(blocks, new Vector2(-1.1f, 6.0f), new Vector2(1.5f, 6.0f), 0.3f);

        // hats: two shelves on pale boards, a hat on each stand
        var hats = Empty("Rack_hat", racks, V(-4.9f, 0, 6.2f));
        Empty("Stand", hats.transform, V(-4.9f, 0, 4.4f));
        for (int s = 0; s < 2; s++) Prim(PrimitiveType.Cube, "Board", hats.transform, V(0, 1.1f + s * 1.1f, -0.1f), V(2.8f, 0.08f, 0.6f), wood);
        for (int i = 0; i < 5; i++)
            for (int s = 0; s < 2; s++)
            {
                float x = -1.1f + i * 0.55f;
                var h = Group("Hat", hats.transform).transform; h.localPosition = V(x, 1.14f + s * 1.1f, -0.1f);
                var m = Lit("bq_hat" + ((i + s * 5) % 10), Pal(i * 3 + s * 7 + 2), 0.2f);
                Prim(PrimitiveType.Cylinder, "Crown", h, V(0, 0.14f, 0), V(0.28f, 0.14f, 0.28f), m);
                if ((i + s) % 2 == 0) Prim(PrimitiveType.Cylinder, "Brim", h, V(0, 0.03f, 0), V(0.5f, 0.015f, 0.5f), m);
            }
        BlockLine(blocks, new Vector2(-6.0f, 6.0f), new Vector2(-3.8f, 6.0f), 0.3f);

        // hair: round stands with a head and a hairdo in a few colours
        var hair = Empty("Rack_hair", racks, V(-2.55f, 0, 6.2f));
        Empty("Stand", hair.transform, V(-2.55f, 0, 4.4f));
        Prim(PrimitiveType.Cube, "Board", hair.transform, V(0, 1.15f, -0.1f), V(2.4f, 0.08f, 0.7f), wood);
        var skin = Lit("bq_head", C("#f0c4a4"), 0.2f);
        for (int i = 0; i < 4; i++)
        {
            float x = -0.85f + i * 0.57f;
            var hm = Lit("bq_hairdo" + i, Pal(new[] { 18, 8, 0, 5 }[i]), 0.25f);
            Prim(PrimitiveType.Cylinder, "Neck", hair.transform, V(x, 1.28f, -0.1f), V(0.1f, 0.12f, 0.1f), skin);
            Prim(PrimitiveType.Sphere, "Head", hair.transform, V(x, 1.62f, -0.1f), V(0.38f, 0.44f, 0.38f), skin);
            Prim(PrimitiveType.Sphere, "Hair", hair.transform, V(x, 1.7f, -0.08f), V(0.44f, 0.46f, 0.44f), hm);
            if (i % 2 == 0) Prim(PrimitiveType.Sphere, "Tail", hair.transform, V(x + 0.2f, 1.45f, 0.02f), V(0.16f, 0.34f, 0.16f), hm);
            else Prim(PrimitiveType.Sphere, "Bun", hair.transform, V(x, 1.98f, -0.1f), V(0.2f, 0.2f, 0.2f), hm);
        }
        BlockLine(blocks, new Vector2(-3.8f, 6.0f), new Vector2(-1.3f, 6.0f), 0.3f);

        // glasses: a small shelf with pairs standing on it
        var glasses = Empty("Rack_glasses", racks, V(3.0f, 0, 6.2f));
        Empty("Stand", glasses.transform, V(3.0f, 0, 4.4f));
        Prim(PrimitiveType.Cube, "Board", glasses.transform, V(0, 1.25f, -0.1f), V(2.2f, 0.08f, 0.5f), wood);
        Prim(PrimitiveType.Cube, "BoardLow", glasses.transform, V(0, 0.8f, -0.1f), V(2.2f, 0.08f, 0.5f), wood);
        for (int i = 0; i < 4; i++)
            for (int s = 0; s < 2; s++)
            {
                float x = -0.75f + i * 0.5f;
                var gm = Lit("bq_glass" + ((i + s * 4) % 8), Pal(i * 5 + s * 9 + 1), 0.4f);
                var g = Group("Pair", glasses.transform).transform; g.localPosition = V(x, (s == 0 ? 1.29f : 0.84f), -0.1f);
                Prim(PrimitiveType.Cylinder, "LensL", g, V(-0.1f, 0.1f, 0), V(0.16f, 0.012f, 0.16f), gm, V(90, 0, 0));
                Prim(PrimitiveType.Cylinder, "LensR", g, V(0.1f, 0.1f, 0), V(0.16f, 0.012f, 0.16f), gm, V(90, 0, 0));
                Prim(PrimitiveType.Cube, "Bridge", g, V(0, 0.1f, 0), V(0.06f, 0.02f, 0.02f), gm);
            }
        BlockLine(blocks, new Vector2(1.8f, 6.0f), new Vector2(4.2f, 6.0f), 0.3f);

        // bags: hooks on a rail with bags hanging, a few on the floor
        var bags = Empty("Rack_bag", racks, V(5.15f, 0, 6.2f));
        Empty("Stand", bags.transform, V(5.15f, 0, 4.4f));
        Prim(PrimitiveType.Cube, "Rail", bags.transform, V(0, 2.3f, -0.1f), V(1.9f, 0.06f, 0.06f), wood);
        for (int i = 0; i < 4; i++)
        {
            float x = -0.7f + i * 0.47f;
            var bm = Lit("bq_bag" + i, Pal(new[] { 3, 12, 20, 9 }[i]), 0.2f);
            Prim(PrimitiveType.Cube, "Bag", bags.transform, V(x, 1.65f, -0.12f), V(0.34f, 0.4f, 0.14f), bm);
            Prim(PrimitiveType.Cube, "Strap", bags.transform, V(x, 2.05f, -0.12f), V(0.04f, 0.5f, 0.03f), bm);
        }
        Fbx(Resto + "crate.fbx", bags.transform, V(0.3f, 0, -0.4f), 12f, 0.4f, restoMat, "Crate");
        BlockLine(blocks, new Vector2(4.2f, 6.0f), new Vector2(6.0f, 6.0f), 0.3f);
    }

    // ---------- the left wall: tops on a rail, bottoms folded ----------

    static void MakeLeftWall(Transform racks, Transform blocks)
    {
        var steel = Lit("bq_steel", C("#c9ccdf"), 0.6f);
        var wood = Lit("bq_shelf", C("#d9a273"), 0.25f);

        // tops: a rail along z on two posts, garments hanging with their faces to the camera
        var tops = Empty("Rack_top", racks, V(-5.7f, 0, 3.2f));
        Empty("Stand", tops.transform, V(-3.9f, 0, 3.2f));
        foreach (float z in new[] { -1.5f, 1.5f })
        {
            Prim(PrimitiveType.Cylinder, "Post", tops.transform, V(0, 0.95f, z), V(0.07f, 0.95f, 0.07f), steel);
            Prim(PrimitiveType.Cube, "Foot", tops.transform, V(0, 0.03f, z), V(0.7f, 0.05f, 0.1f), steel);
        }
        Prim(PrimitiveType.Cube, "Rail", tops.transform, V(0, 1.9f, 0), V(0.06f, 0.06f, 3.2f), steel);
        for (int i = 0; i < 9; i++)
        {
            float z = -1.4f + i * 0.35f;
            var m = Lit("bq_top" + i, Pal(i * 2 + 1), 0.15f);
            var g = Group("Top", tops.transform).transform; g.localPosition = V(0, 1.55f, z);
            Prim(PrimitiveType.Cube, "Body", g, V(0, 0, 0), V(0.42f, 0.62f, 0.12f), m);
            Prim(PrimitiveType.Cube, "SleeveL", g, V(0, 0.2f, 0.17f), V(0.3f, 0.2f, 0.1f), m, V(0, 0, 0));
            Prim(PrimitiveType.Cube, "Hanger", g, V(0, 0.38f, 0), V(0.05f, 0.1f, 0.03f), steel);
        }
        BlockLine(blocks, new Vector2(-5.9f, 1.8f), new Vector2(-5.9f, 4.6f), 0.35f);

        // bottoms: shelves of folded trousers and skirts
        var bottoms = Empty("Rack_bottom", racks, V(-5.9f, 0, -0.2f));
        Empty("Stand", bottoms.transform, V(-3.9f, 0, -0.2f));
        for (int s = 0; s < 3; s++) Prim(PrimitiveType.Cube, "Board", bottoms.transform, V(0.1f, 0.5f + s * 0.75f, 0), V(0.6f, 0.07f, 3.0f), wood);
        Prim(PrimitiveType.Cube, "Back", bottoms.transform, V(-0.2f, 1.2f, 0), V(0.06f, 2.0f, 3.0f), wood);
        for (int s = 0; s < 3; s++)
            for (int i = 0; i < 4; i++)
            {
                float z = -1.1f + i * 0.72f;
                var m = Lit("bq_bot" + ((s * 4 + i) % 12), Pal(s * 6 + i * 4 + 2), 0.15f);
                for (int k = 0; k < 3; k++)
                    Prim(PrimitiveType.Cube, "Fold", bottoms.transform, V(0.1f, 0.58f + s * 0.75f + k * 0.07f, z), V(0.46f, 0.07f, 0.55f), m);
            }
        BlockLine(blocks, new Vector2(-6.0f, -1.8f), new Vector2(-6.0f, 1.4f), 0.35f);
    }

    // ---------- the right wall: shoes, then the counter and the shopkeeper ----------

    static void MakeRightWall(Transform racks, Transform props, Transform blocks)
    {
        var wood = Lit("bq_shelf", C("#d9a273"), 0.25f);
        var shoes = Empty("Rack_shoes", racks, V(5.9f, 0, 3.2f));
        Empty("Stand", shoes.transform, V(3.9f, 0, 3.2f));
        for (int s = 0; s < 3; s++) Prim(PrimitiveType.Cube, "Board", shoes.transform, V(-0.1f, 0.45f + s * 0.8f, 0), V(0.6f, 0.07f, 3.0f), wood);
        Prim(PrimitiveType.Cube, "Back", shoes.transform, V(0.2f, 1.2f, 0), V(0.06f, 2.0f, 3.0f), wood);
        for (int s = 0; s < 3; s++)
            for (int i = 0; i < 4; i++)
            {
                float z = -1.1f + i * 0.72f;
                var m = Lit("bq_shoe" + ((s * 4 + i) % 12), Pal(s * 5 + i * 6 + 4), 0.3f);
                var sole = Lit("bq_sole", C("#fff6ea"), 0.2f);
                var g = Group("Shoe", shoes.transform).transform; g.localPosition = V(-0.1f, 0.52f + s * 0.8f, z);
                Prim(PrimitiveType.Cube, "Sole", g, V(-0.05f, 0.02f, 0), V(0.4f, 0.06f, 0.2f), sole);
                Prim(PrimitiveType.Cube, "Upper", g, V(-0.05f, 0.13f, 0), V(0.3f, 0.16f, 0.18f), m);
                Prim(PrimitiveType.Cube, "Toe", g, V(-0.2f, 0.08f, 0), V(0.14f, 0.1f, 0.18f), m);
            }
        BlockLine(blocks, new Vector2(6.0f, 1.8f), new Vector2(6.0f, 4.6f), 0.35f);

        // the counter along the wall, a till and the shopkeeper behind it
        var steel = Lit("bq_steel", C("#c9ccdf"), 0.6f);
        for (int i = 0; i < 2; i++) Fbx(Resto + "kitchencounter_straight_A.fbx", props, V(4.9f, 0, -1.5f - i * 2f), 90f, 1f, restoMat, "Counter");
        Prim(PrimitiveType.Cube, "Till", props, V(4.9f, 1.15f, -2.0f), V(0.5f, 0.28f, 0.5f), steel);
        Prim(PrimitiveType.Cube, "TillScreen", props, V(4.9f, 1.45f, -2.0f), V(0.4f, 0.26f, 0.05f), Lit("bq_screen", C("#a8a9fc"), 0.3f, new Color(0.3f, 0.3f, 0.9f)), V(-20, 90, 0));
        BlockLine(blocks, new Vector2(4.9f, -0.7f), new Vector2(4.9f, -4.0f), 0.55f);

        var keeper = Group("Shopkeeper", props).transform;
        keeper.position = V(6.0f, 0, -2.6f);
        var crew = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WorldArtBuilder.CrewPrefabs[1]), keeper);
        crew.transform.localPosition = Vector3.zero;
        crew.transform.localRotation = Quaternion.Euler(0, 270f, 0);   // facing west, into the shop
        var mk = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WorldArtBuilder.MarkerPrefab), keeper);
        mk.transform.localPosition = V(0, 3.65f, 0);
        Block(blocks, 6.0f, -2.6f, 0.45f);
    }

    // ---------- the lounge corner and plants ----------

    static void MakeLounge(Transform props, Transform blocks)
    {
        Fbx(Furn + "couch.fbx", props, V(-4.6f, 0.02f, -3.4f), 180f, 1.0f, furnMat, "Couch");
        Fbx(Furn + "armchair.fbx", props, V(-2.6f, 0.02f, -3.2f), 160f, 0.75f, furnMat, "Armchair");
        Fbx(Furn + "table_low.fbx", props, V(-3.6f, 0.02f, -2.0f), 0f, 0.7f, furnMat, "LowTable");
        BlockLine(blocks, new Vector2(-5.6f, -3.4f), new Vector2(-3.6f, -3.4f), 0.5f);
        Block(blocks, -2.6f, -3.2f, 0.55f); Block(blocks, -3.6f, -2.0f, 0.5f);
        Fbx(Furn + "lamp_standing.fbx", props, V(-5.9f, 0.02f, 5.6f), 0f, 0.8f, furnMat, "Lamp");
        Fbx(Furn + "lamp_standing.fbx", props, V(5.9f, 0.02f, 5.6f), 0f, 0.8f, furnMat, "Lamp");
        Fbx(Furn + "cactus_medium_A.fbx", props, V(-2.0f, 0.02f, -5.0f), 20f, 1.0f, furnMat, "Cactus");
        Fbx(Furn + "cactus_medium_A.fbx", props, V(4.4f, 0.02f, -5.1f), 120f, 1.0f, furnMat, "Cactus");
        Fbx(Furn + "cactus_small_A.fbx", props, V(-3.6f, 0.38f, -2.0f), 0f, 1.0f, furnMat, "TableCactus");
        Block(blocks, -2.0f, -5.0f, 0.4f); Block(blocks, 4.4f, -5.1f, 0.4f);
    }

    // ---------- the player, riff and the camera ----------

    static void Populate(PlaceKit.Look look, VolumeProfile profile, Transform blocks, Transform props)
    {
        var spawn = Empty("PlayerSpawn", null, V(0, 0, -3.0f));
        var info = Group("PlaceInfo").AddComponent<WorldPlace>();
        info.placeId = "coffee";
        info.interior = "boutique";

        var cam = MakeCamera(profile, look);
        cam.clearFlags = CameraClearFlags.SolidColor;
        var player = Group("Player").transform;
        player.position = spawn.transform.position;
        var star = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WorldArtBuilder.StarPrefab), player);
        star.transform.localPosition = Vector3.zero;
        var pc = player.gameObject.AddComponent<PlayerController>();
        pc.cam = cam;
        pc.animator = star.GetComponent<Animator>();
        pc.bounds = 5.8f;
        var click = Prim(PrimitiveType.Cylinder, "ClickMarker", null, V(0, 0.03f, 0), V(0.7f, 0.02f, 0.7f),
            Lit("click_marker", C("#ff8cc0"), 0.1f, new Color(1.6f, 0.5f, 0.9f)), null, false);
        click.SetActive(false);
        pc.marker = click.transform;

        var dogGo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WorldArtBuilder.RiffPrefab));
        dogGo.name = "Riff";
        dogGo.transform.position = V(-1.4f, 0, -4.0f);
        dogGo.GetComponent<DogFollower>().target = player;

        var follow = cam.gameObject.AddComponent<FollowCamera>();
        follow.target = player;
        follow.offset = V(0, 6.0f, -9.6f);
        follow.tiltUp = 9f;
        follow.lead = 1.0f;
        follow.clearRadius = 0.9f;

        var st = new GameObject("SpikeStatus").AddComponent<SpikeStatus>();
        st.player = player; st.dog = dogGo.transform;
    }

    // ---------- the door on the coffee street ----------

    /// <summary>
    /// Opens Place_Coffee and turns the right-hand window of the shop row (Front_4, x = 1) into a door with its own sign: "BOUTIQUE" on a mint
    /// panel. "BoutiqueDoor" is the empty the gameplay binds (its child Stand is where she is put back on the street). Safe to run again: the
    /// pieces it adds are removed first.
    /// </summary>
    static void PatchCoffee()
    {
        var scene = EditorSceneManager.OpenScene(CoffeeScene, OpenSceneMode.Single);
        InitKits("", null, true);
        foreach (string n in new[] { "BoutiqueDoor", "BoutiqueFront", "BoutiqueDoorModel", "BoutiqueSign" })
            foreach (var g in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (g != null && g.name == n) Object.DestroyImmediate(g);
        var shop = GameObject.Find("CoffeeShop");
        if (shop == null) throw new System.Exception("no CoffeeShop group in Place_Coffee");
        Transform t = shop.transform;
        // the window module at x = 1 becomes a doorway with a door
        Transform window = t.Find("Front_4");
        if (window != null) Object.DestroyImmediate(window.gameObject);
        Fbx(Resto + "wall_doorway.fbx", t, V(1f, 0, 8.2f), 180f, 1f, restoMat, "BoutiqueFront");
        Fbx(Resto + "door_A.fbx", t, V(1f, 0, 8.2f), 180f, 1f, restoMat, "BoutiqueDoorModel");
        // the sign above it, as the coffee sign sits over its door
        var mint = Lit("bq_sign", C("#8fe3c4"), 0.2f);
        var ink = Lit("bq_ink", C("#2b2350"), 0.2f);
        var panel = Prim(PrimitiveType.Cube, "BoutiqueSign", t, V(1f, 5.85f, 7.95f), V(3.5f, 1.5f, 0.25f), mint);
        PixelText(panel.transform, "BOUTIQUE", V(0, 0.1f, -0.62f), 0.058f, ink, "bq_text_boutique_door", 0.4f);
        Prim(PrimitiveType.Cube, "Hanger", panel.transform, V(0, -0.35f, -0.62f), V(0.4f, 0.05f, 0.1f), ink);
        Prim(PrimitiveType.Cube, "HangerHook", panel.transform, V(0, -0.22f, -0.62f), V(0.05f, 0.2f, 0.1f), ink);
        // the empty the gameplay finds: on the threshold, with the spot she stands on when she comes back out
        var door = new GameObject("BoutiqueDoor");
        door.transform.position = V(1f, 0, 7.3f);
        var stand = new GameObject("Stand");
        stand.transform.SetParent(door.transform, false);
        stand.transform.position = V(1.5f, 0, 5.5f);
        SaveScene(scene, CoffeeScene);
    }
}
