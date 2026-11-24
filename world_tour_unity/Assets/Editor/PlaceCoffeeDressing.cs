// Phase 8.1 look pass: the "dressing" of Place_Coffee (string lights, flower beds and boxes, potted plants, a chalkboard sign,
// two park kiosks, parked cars, a traffic light, a tree line). Part of PlaceCoffeeBuilder; every piece is the CC0 KayKit
// models (ASSETS.md) or primitives and small generated meshes. Kept clear of the crew, the walking lines and the stage.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using WorldTour.World;
using static PlaceKit;

public static partial class PlaceCoffeeBuilder
{
    // ---------- string lights ----------

    static void MakeStringLights(Transform t)
    {
        var wire = new MB(); var bulbs = new MB();
        // along the street, pole to pole (the lamp heads are at 3.7 m; the strings hang from just under them)
        float[] poles = { -12.3f, -4.2f, 4.3f, 12.3f };
        for (int i = 0; i < poles.Length - 1; i++)
            StringLine(wire, bulbs, V(poles[i], 3.5f, -0.85f), V(poles[i + 1], 3.5f, -0.85f), 0.85f, 0.62f);
        // a second, higher run in front of the shop, from one end of the awning to the other, scalloped
        for (float x = -12.6f; x < 3.0f; x += 3.2f)
            StringLine(wire, bulbs, V(x, 2.42f, 5.62f), V(Mathf.Min(x + 3.2f, 3.2f), 2.42f, 5.62f), 0.3f, 0.5f, 0.07f);
        // a garland along the stage's front truss
        StringLine(wire, bulbs, V(4.8f, 6.15f, 7.4f), V(9f, 6.15f, 7.4f), 0.5f, 0.5f);
        StringLine(wire, bulbs, V(9f, 6.15f, 7.4f), V(13.2f, 6.15f, 7.4f), 0.5f, 0.5f);
        // a long swag from the west lamp across to the shop's corner post
        StringLine(wire, bulbs, V(-12.3f, 3.5f, -0.85f), V(-13.0f, 4.6f, 7.7f), 0.9f, 0.8f, 0.07f);
        StringLine(wire, bulbs, V(12.3f, 3.5f, -0.85f), V(13.3f, 6.2f, 7.4f), 1.0f, 0.8f, 0.07f);

        var wireMat = DoubleSided("string_wire", C("#4a3b4d"), 0.2f);
        var bulbMat = DoubleSided("string_bulb", C("#fff0c8"), 0.3f, new Color(3.1f, 2.1f, 0.95f));
        MeshObject("StringWire", t, wire.ToMesh("strings_wire"), wireMat, false);
        MeshObject("StringBulbs", t, bulbs.ToMesh("strings_bulbs"), bulbMat, false);
    }

    // ---------- flowers ----------

    static readonly string[] FlowerHex = { "#ff7fae", "#ffd84a", "#fff6ee", "#b9a4ff" };
    static MB[] flowers;

    static void ScatterFlowers(Vector3 center, float radiusX, float radiusZ, float domeH, int count, int seed)
    {
        var rng = new System.Random(seed);
        for (int i = 0; i < count; i++)
        {
            double u = rng.NextDouble(), w = rng.NextDouble() * Mathf.PI * 2;
            float r = Mathf.Sqrt((float)u);
            float dx = Mathf.Cos((float)w) * r, dz = Mathf.Sin((float)w) * r;
            float y = domeH * Mathf.Sqrt(Mathf.Max(0f, 1f - r * r)) + 0.03f;
            float sz = 0.04f + (float)rng.NextDouble() * 0.035f;
            Vector3 c = center + V(dx * radiusX, y, dz * radiusZ);
            flowers[rng.Next(FlowerHex.Length)].Gem(c, sz, sz * 1.2f, sz);
        }
    }

    static void FlowerBed(Transform t, Vector3 center, float length, bool alongX, int seed)
    {
        var edge = Lit("bed_edge", C("#a9754f"), 0.1f);
        var soil = Lit("bed_soil", C("#6d4b3a"), 0.05f);
        Vector3 size = alongX ? V(length, 0.3f, 0.9f) : V(0.9f, 0.3f, length);
        Prim(PrimitiveType.Cube, "BedEdge", t, center + V(0, 0.15f, 0), size, edge);
        Prim(PrimitiveType.Cube, "BedSoil", t, center + V(0, 0.2f, 0), size - (alongX ? V(0.14f, -0.1f, 0.14f) : V(0.14f, -0.1f, 0.14f)), soil, null, false);
        int bushes = Mathf.Max(1, Mathf.RoundToInt(length / 1.2f));
        for (int i = 0; i < bushes; i++)
        {
            float s = bushes == 1 ? 0f : (i / (float)(bushes - 1) - 0.5f) * (length - 1.0f);
            Vector3 p = center + (alongX ? V(s, 0.3f, 0) : V(0, 0.3f, s));
            Fbx(Forest + (i % 2 == 0 ? "Bush_2_A_Color1.fbx" : "Bush_4_A_Color1.fbx"), t, p, seed * 37f + i * 71f, 1.15f, (i % 2 == 0) ? forestMat : blossomMat, "BedBush");
            ScatterFlowers(p + V(0, 0.25f, 0), 0.45f, 0.42f, 0.5f, 34, seed * 13 + i);
        }
    }

    static void FlowerBox(Transform t, float x, float z, float width, int seed)
    {
        var wood = Lit("flowerbox_wood", C("#b98a5e"), 0.1f);
        Prim(PrimitiveType.Cube, "FlowerBox", t, V(x, 0.62f, z), V(width, 0.42f, 0.46f), wood);
        Prim(PrimitiveType.Cube, "FlowerBoxStripe", t, V(x, 0.62f, z - 0.235f), V(width, 0.1f, 0.02f), Lit("flowerbox_pink", C("#f98bb0"), 0.2f), null, false);
        var leaf = Lit("flower_leaf", C("#5fae55"), 0.1f);
        var mass = Prim(PrimitiveType.Cube, "BoxGreen", t, V(x, 0.86f, z), V(width - 0.1f, 0.14f, 0.36f), leaf, null, false);
        ScatterFlowers(V(x, 0.9f, z), width * 0.5f - 0.06f, 0.14f, 0.12f, Mathf.RoundToInt(width * 26f), seed);
    }

    // ---------- the chalkboard sign ----------

    static void ChalkboardSign(Transform t, Vector3 pos, float yaw)
    {
        var g = Group("ChalkboardSign", t).transform;
        g.position = pos; g.rotation = Quaternion.Euler(0, yaw, 0);
        var wood = Lit("chalk_frame", C("#c6935f"), 0.15f);
        var board = Lit("chalk_board", C("#2f3d3c"), 0.05f);
        var chalk = Lit("chalk_white", C("#f6f1e6"), 0.05f, new Color(0.25f, 0.25f, 0.24f));
        var chalkPink = Lit("chalk_pink", C("#ffb3cf"), 0.05f, new Color(0.3f, 0.2f, 0.25f));
        foreach (float side in new[] { -1f, 1f })
        {
            var a = Group("Face", g).transform;
            a.localPosition = V(0, 0.0f, side * 0.0f); a.localRotation = Quaternion.Euler(-side * 11f, 0, 0);
            // a leaning board: hinge at the top (y = 1.2) so the two faces make an A
            a.localPosition = V(0, 0.6f, side * 0.29f);
            Prim(PrimitiveType.Cube, "Frame", a, V(0, 0, 0), V(0.86f, 1.2f, 0.07f), wood);
            Prim(PrimitiveType.Cube, "Board", a, V(0, 0.02f, -side * 0.0f), V(0.72f, 1.0f, 0.075f), board);
            if (side < 0f)
            {
                // doodles on the front: a cup with steam, a heart-star, three lines of "writing"
                float zf = -0.045f;
                Prim(PrimitiveType.Cylinder, "Cup", a, V(-0.12f, 0.12f, zf), V(0.17f, 0.09f, 0.02f), chalk, V(90, 0, 0), false);
                Prim(PrimitiveType.Cube, "CupHandle", a, V(0.0f, 0.12f, zf), V(0.05f, 0.1f, 0.02f), chalk, null, false);
                Prim(PrimitiveType.Cube, "Saucer", a, V(-0.12f, 0.02f, zf), V(0.3f, 0.025f, 0.02f), chalk, null, false);
                for (int i = 0; i < 3; i++) Prim(PrimitiveType.Cube, "Steam", a, V(-0.18f + i * 0.06f, 0.3f + (i % 2) * 0.03f, zf), V(0.025f, 0.12f, 0.02f), chalk, V(0, 0, (i - 1) * 14f), false);
                var star = new GameObject("DoodleStar");
                star.transform.SetParent(a, false);
                star.transform.localPosition = V(0.2f, 0.3f, zf);
                star.transform.localRotation = Quaternion.Euler(0, 180f, 0);
                star.transform.localScale = V(0.22f, 0.22f, 0.02f);
                star.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(WorldArtBuilder.MeshDir + "/MarkerStar.asset");
                var sr = star.AddComponent<MeshRenderer>(); sr.sharedMaterial = chalkPink; sr.shadowCastingMode = ShadowCastingMode.Off;
                float[] widths = { 0.5f, 0.38f, 0.44f };
                for (int i = 0; i < 3; i++) Prim(PrimitiveType.Cube, "Line", a, V(-0.04f - (0.5f - widths[i]) * 0.3f, -0.18f - i * 0.12f, zf), V(widths[i], 0.035f, 0.02f), chalk, null, false);
                Prim(PrimitiveType.Cube, "Underline", a, V(-0.1f, 0.38f, zf), V(0.01f, 0.01f, 0.02f), chalk, null, false);
            }
        }
        Block(t, pos.x, pos.z, 0.5f);
    }

    // ---------- park kiosks ----------

    static void Kiosk(Transform t, Transform blocks, Vector3 pos, string kind, string a, string b)
    {
        var g = Group("Kiosk_" + kind, t).transform;
        g.position = pos;
        var wood = Lit("kiosk_wood_" + kind, C(a), 0.12f);
        var cream = Lit("kiosk_cream", C("#fff4e2"), 0.15f);
        var post = Lit("kiosk_post", C("#8f6a4a"), 0.1f);
        var stripeA = Lit("kiosk_stripe_" + kind + "A", C(b), 0.25f);
        var stripeB = Lit("kiosk_stripeB", C("#fff6ea"), 0.25f);
        Prim(PrimitiveType.Cube, "Counter", g, V(0, 0.5f, 0), V(2.4f, 1.0f, 0.95f), wood);
        Prim(PrimitiveType.Cube, "CounterTop", g, V(0, 1.04f, -0.02f), V(2.55f, 0.09f, 1.1f), cream);
        Prim(PrimitiveType.Cube, "BackPanel", g, V(0, 1.55f, 0.5f), V(2.4f, 1.1f, 0.07f), wood);
        foreach (float sx in new[] { -1.15f, 1.15f })
            Prim(PrimitiveType.Cube, "Post", g, V(sx, 1.35f, -0.48f), V(0.09f, 2.7f, 0.09f), post);
        foreach (float sx in new[] { -1.15f, 1.15f })
            Prim(PrimitiveType.Cube, "PostBack", g, V(sx, 1.35f, 0.48f), V(0.09f, 2.7f, 0.09f), post);
        for (int i = 0; i < 8; i++)
        {
            float x = -1.4f + i * 0.4f + 0.2f;
            var m = i % 2 == 0 ? stripeA : stripeB;
            Prim(PrimitiveType.Cube, "Awning", g, V(x, 2.82f, -0.18f), V(0.4f, 0.06f, 1.75f), m, V(-18f, 0, 0));
            Prim(PrimitiveType.Cube, "Valance", g, V(x, 2.46f, -1.02f), V(0.4f, 0.26f, 0.05f), m);
        }
        var goods = Group("Goods", g).transform;
        if (kind == "flowers")
        {
            for (int i = 0; i < 3; i++)
            {
                Fbx(Resto + "crate.fbx", goods, V(-0.8f + i * 0.8f, 1.08f, -0.05f), i * 17f, 0.34f, restoMat, "Crate");
                ScatterFlowers(g.position + V(-0.8f + i * 0.8f, 1.34f, -0.05f), 0.3f, 0.3f, 0.12f, 30, 100 + i);
            }
            // buckets of tall flowers on the ground beside it
            var bucket = Lit("bucket", C("#9fb4d0"), 0.3f);
            for (int i = 0; i < 2; i++)
            {
                Prim(PrimitiveType.Cylinder, "Bucket", g, V(1.65f + i * 0.2f, 0.25f, -0.2f - i * 0.35f), V(0.4f, 0.25f, 0.4f), bucket);
                ScatterFlowers(g.position + V(1.65f + i * 0.2f, 0.52f, -0.2f - i * 0.35f), 0.2f, 0.2f, 0.2f, 22, 130 + i);
            }
        }
        else
        {
            var scoopCols = new[] { C("#ff9fc4"), C("#aef0d4"), C("#fff3c8") };
            var cone = Lit("waffle", C("#e0a96a"), 0.1f);
            for (int i = 0; i < 3; i++)
            {
                var gm = new MB(); gm.Gem(Vector3.zero, 0.14f, 0.3f, 0.14f);
                var cm = MeshObject("Cone", goods, gm.ToMesh("kiosk_cone"), cone, true);
                cm.transform.localPosition = V(-0.7f + i * 0.7f, 1.4f, -0.05f); cm.transform.localRotation = Quaternion.Euler(0, 0, 180f);
                Prim(PrimitiveType.Sphere, "Scoop", goods, V(-0.7f + i * 0.7f, 1.72f, -0.05f), V(0.3f, 0.28f, 0.3f), Lit("scoop" + i, scoopCols[i], 0.4f));
            }
            // a big cone sign on the roof
            var big = new MB(); big.Gem(Vector3.zero, 0.3f, 0.65f, 0.3f);
            var bm = MeshObject("SignCone", g, big.ToMesh("kiosk_signcone"), cone, true);
            bm.transform.localPosition = V(0, 3.35f, -0.4f); bm.transform.localRotation = Quaternion.Euler(0, 0, 180f);
            Prim(PrimitiveType.Sphere, "SignScoop", g, V(0, 4.05f, -0.4f), V(0.7f, 0.65f, 0.7f), Lit("scoop0", scoopCols[0], 0.4f));
        }
        for (float x = -1.2f; x <= 1.3f; x += 1.2f) Block(blocks, pos.x + x, pos.z, 0.7f);
    }

    // ---------- all of it ----------

    static void MakeDressing(Transform world, Transform props, Transform blocks)
    {
        var dress = Group("Dressing", world).transform;

        flowers = new MB[FlowerHex.Length];
        for (int i = 0; i < flowers.Length; i++) flowers[i] = new MB();

        // flower boxes under the two big windows, planters of cacti by the corners
        FlowerBox(dress, -11f, 7.7f, 2.4f, 1);
        FlowerBox(dress, 1f, 7.7f, 2.4f, 2);
        Fbx(Furn + "cactus_medium_A.fbx", dress, V(-1.9f, 0, 7.15f), 20f, 0.9f, furnMat, "Cactus");
        Fbx(Furn + "cactus_medium_A.fbx", dress, V(3.55f, 0, 6.9f), 130f, 0.9f, furnMat, "Cactus");
        Fbx(Furn + "cactus_small_A.fbx", dress, V(-12.0f, 0.9f, 3.8f), 0f, 1.2f, furnMat, "TableCactus");
        Fbx(Furn + "cactus_small_B.fbx", dress, V(-0.4f, 0.9f, 4.6f), 0f, 1.2f, furnMat, "TableCactus");
        Block(blocks, -1.9f, 7.15f, 0.4f); Block(blocks, 3.55f, 6.9f, 0.4f);

        ChalkboardSign(dress, V(1.9f, 0, 6.3f), 196f);

        // flower beds beside the park path, round the benches, and in front of the stage speakers
        FlowerBed(dress, V(-2.25f, 0, -11.2f), 4.4f, false, 3);
        FlowerBed(dress, V(2.25f, 0, -11.2f), 4.4f, false, 4);
        FlowerBed(dress, V(-7.3f, 0, -8.4f), 2.6f, true, 5);
        FlowerBed(dress, V(7.3f, 0, -8.4f), 2.6f, true, 6);

        // two kiosks in the park
        Kiosk(dress, blocks, V(-10.2f, 0, -11.2f), "flowers", "#f6a6c2", "#f98bb0");
        Kiosk(dress, blocks, V(10.2f, 0, -11.6f), "icecream", "#a9dccb", "#7fd0b8");

        // parked cars and the traffic light at the crossing
        var sedan = Fbx(City + "car_sedan.fbx", dress, V(-9.2f, 0, -4.9f), 90f, CityScale, cityMat, "Sedan");
        Block(blocks, -10.5f, -4.9f, 0.9f); Block(blocks, -9.2f, -4.9f, 0.9f); Block(blocks, -7.9f, -4.9f, 0.9f);
        Fbx(City + "car_hatchback.fbx", dress, V(-11.8f, 0, -2.9f), -90f, CityScale, cityMat, "Hatchback");
        Block(blocks, -12.9f, -2.9f, 0.85f); Block(blocks, -11.8f, -2.9f, 0.85f); Block(blocks, -10.7f, -2.9f, 0.85f);

        // a tree line outside the walking area, so the sides have depth
        float[] tz = { -13.5f, -9.5f, -4.0f, 1.5f, 6.5f };
        string[] kinds = { "Tree_3_A_Color1", "Tree_1_A_Color1", "Tree_3_A_Color1", "Tree_2_A_Color1", "Tree_3_A_Color1" };
        for (int i = 0; i < tz.Length; i++)
        {
            Fbx(Forest + kinds[i] + ".fbx", dress, V(-15.6f, 0, tz[i] + (i % 2) * 0.6f), i * 67f, 1.3f, i % 2 == 0 ? forestMat : blossomMat, "EdgeTree");
            Fbx(Forest + kinds[(i + 2) % 5] + ".fbx", dress, V(15.8f, 0, tz[i] - (i % 2) * 0.8f), i * 41f + 9f, 1.3f, i % 2 == 1 ? forestMat : blossomMat, "EdgeTree");
        }
        // grass tufts in the park
        var rng = new System.Random(5);
        for (int i = 0; i < 26; i++)
        {
            float x = (float)(rng.NextDouble() * 2 - 1) * 12.5f, z = -13.5f + (float)rng.NextDouble() * 6f;
            if (Mathf.Abs(x) < 2.3f) continue;
            Fbx(Forest + (i % 2 == 0 ? "Grass_1_A_Color1.fbx" : "Grass_2_A_Color1.fbx"), dress, V(x, 0, z), i * 47f, 1.3f, forestMat, "Tuft");
        }

        MakeStringLights(dress);

        // the blossoms: one mesh per colour, so all of them together cost four draws
        for (int i = 0; i < flowers.Length; i++)
            MeshObject("Blossoms_" + i, dress, flowers[i].ToMesh("blossoms_" + i), DoubleSided("blossom_" + i, C(FlowerHex[i]), 0.35f), false);
    }
}
