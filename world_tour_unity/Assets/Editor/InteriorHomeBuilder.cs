// Phase 10B.2: her home, a small interior scene (Assets/World/Scenes/Home.unity), its furniture prefabs (Assets/World/Prefabs/Home/furn_*.prefab) and the door
// on the coffee street. Built from the CC0 KayKit pieces already imported (Furniture couch, armchair, lamp, cacti and low table; Restaurant chair, round table,
// crate, jars and chalk board; City bench and street lamp; Forest bushes and trees) plus Unity primitives, the same way the boutique is. Re-runnable.
//
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -executeMethod InteriorHomeBuilder.CreateAll      (the pieces, the scene and the street door)
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -executeMethod InteriorHomeBuilder.CreateScene    (the pieces and the Home scene)
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -executeMethod InteriorHomeBuilder.AddCoffeeDoor  (the door in Place_Coffee)
//
// The scene holds four rooms at the same place (Space_0 her room, Space_1 the den, Space_2 the studio, Space_3 the garden); HomeGame switches one on at a time.
// Each room is 12 x 12 m (x and z from -6 to 6; the grid is 24 x 24 cells of 0.5 m, see HomeRules). Back wall at z = 6, side walls at x = +-6, nothing on the south side.
// A piece's prefab has its pivot at the middle of its footprint on the floor and faces south (-z) at rotation 0; a poster hangs toward +z (the wall).
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WorldTour.Core.Meta;
using WorldTour.World;
using WorldTour.World.Gameplay;
using static PlaceKit;

public static class InteriorHomeBuilder
{
    public const string ScenePath = "Assets/World/Scenes/Home.unity";
    public const string PieceDir = "Assets/World/Prefabs/Home";
    const string CoffeeScene = "Assets/World/Scenes/Place_Coffee.unity";

    public static void CreateAll() { Entry("InteriorHomeBuilder", () => { MakeScene(); PatchCoffee(); }); }
    public static void CreateScene() { Entry("InteriorHomeBuilder.CreateScene", MakeScene); }
    public static void AddCoffeeDoor() { Entry("InteriorHomeBuilder.AddCoffeeDoor", PatchCoffee); }

    static Material M(string name, string hex, float smooth = 0.2f, Color? emission = null) { return Lit("hm_" + name, C(hex), smooth, emission); }

    static PlaceKit.Look HomeLook()
    {
        return new PlaceKit.Look
        {
            ProfileName = "Home", SkyMaterial = "sky_home",
            SunColor = new Color(1f, 0.92f, 0.8f), SunIntensity = 1.3f, SunEuler = new Vector3(55f, -24f, 0f), SunShadowStrength = 0.55f,
            AmbSky = new Color(0.97f, 0.93f, 0.97f), AmbEquator = new Color(1f, 0.94f, 0.92f), AmbGround = new Color(0.74f, 0.66f, 0.7f), AmbIntensity = 1.2f,
            Fog = new Color(0.95f, 0.9f, 0.93f), FogDensity = 0f,
            CameraBackground = new Color(0.97f, 0.88f, 0.92f),
            BloomIntensity = 0.2f, VignetteIntensity = 0.14f,
        };
    }

    // ---------- the scene ----------

    static void MakeScene()
    {
        var scene = NewScene();
        InitKits("", null, false);
        Directory.CreateDirectory(PieceDir);
        var pieces = BuildPieces();
        var look = HomeLook();
        var profile = MakeProfile(look);
        var world = Group("World").transform;
        var spaces = Group("Rooms", world).transform;
        for (int i = 0; i < HomeRules.RoomCount; i++) MakeSpace(spaces, i);
        Populate(look, profile);
        MakeStock(pieces);
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

    // ---------- one room ----------

    static readonly string[] WallHex = { "#fff3e2", "#d6f1e8", "#e4e1fb", "#c8a67a" };
    static readonly string[] FloorHex = { "#e2b283", "#d6a06c", "#bdb7c9", "#7cbf68" };

    static void MakeSpace(Transform parent, int room)
    {
        var space = Group("Space_" + room, parent);
        space.AddComponent<HomeSpace>().index = room;
        var t = space.transform;
        bool garden = room == 3;
        var wallMat = Lit("hm_wall" + room, C(WallHex[room]), 0.08f);
        var floorMat = Lit("hm_floor" + room, C(FloorHex[room]), garden ? 0.05f : 0.25f);
        var trim = M("trim", "#e98bb0");
        var skirt = M("skirt", "#f6a6c3", 0.15f);
        float wallH = garden ? 2.4f : 4.0f;

        var floor = Prim(PrimitiveType.Cube, "Floor", t, V(0, -0.06f, 0), V(12.8f, 0.12f, 12.8f), floorMat, null, false);
        var paintFloor = floor.AddComponent<HomePaint>();
        paintFloor.wall = false; paintFloor.own = C(FloorHex[room]); paintFloor.targets = new[] { floor.GetComponent<Renderer>() };

        var back = Prim(PrimitiveType.Cube, "BackWall", t, V(0, wallH / 2f, 6.2f), V(12.8f, wallH, 0.4f), wallMat);
        var west = Prim(PrimitiveType.Cube, "SideWallW", t, V(-6.2f, wallH / 2f, 0), V(0.4f, wallH, 12.8f), wallMat);
        var east = Prim(PrimitiveType.Cube, "SideWallE", t, V(6.2f, wallH / 2f, 0), V(0.4f, wallH, 12.8f), wallMat);
        var paintWall = back.AddComponent<HomePaint>();
        paintWall.wall = true; paintWall.own = C(WallHex[room]);
        paintWall.targets = new[] { back.GetComponent<Renderer>(), west.GetComponent<Renderer>(), east.GetComponent<Renderer>() };
        if (!garden)
        {
            Prim(PrimitiveType.Cube, "BackSkirt", t, V(0, 0.2f, 5.97f), V(12.0f, 0.4f, 0.06f), skirt, null, false);
            Prim(PrimitiveType.Cube, "BackTrim", t, V(0, wallH - 0.1f, 5.96f), V(12.0f, 0.12f, 0.08f), trim, null, false);
            foreach (int sgn in new[] { -1, 1 })
            {
                Prim(PrimitiveType.Cube, "SideSkirt" + (sgn < 0 ? "W" : "E"), t, V(5.97f * sgn, 0.2f, 0), V(0.06f, 0.4f, 12.0f), skirt, null, false);
                Prim(PrimitiveType.Cube, "SideTrim" + (sgn < 0 ? "W" : "E"), t, V(5.96f * sgn, wallH - 0.1f, 0), V(0.08f, 0.12f, 12.0f), trim, null, false);
            }
            Prim(PrimitiveType.Cube, "FrontKerb", t, V(0, 0.05f, -6.1f), V(12.8f, 0.1f, 0.2f), trim);
        }
        else
        {
            // a fence: pale posts along the top of the boards, and a few flower beds along the wall foot
            var post = M("fence_post", "#fff6ea");
            for (int i = -6; i <= 6; i += 2)
            {
                Prim(PrimitiveType.Cube, "Post", t, V(i, wallH / 2f, 5.92f), V(0.16f, wallH, 0.1f), post, null, false);
            }
            Prim(PrimitiveType.Cube, "FrontKerb", t, V(0, 0.05f, -6.1f), V(12.8f, 0.1f, 0.2f), M("kerb_stone", "#d8d0c4"));
        }

        MakeDoors(t, room);
        MakeBox(t);
        if (room == 0) MakeDoorMat(t);   // wrap-up: the ticket wall, sticker board and outfit rack were never filled, so they are not built
    }

    static readonly string[] DoorText = { "ROOM", "DEN", "STUDIO", "GARDEN" };
    static readonly string[] DoorHex = { "#f98bb0", "#8fe3c4", "#a8a9fc", "#8fd36a" };

    static void MakeDoors(Transform t, int room)
    {
        var frame = M("door_frame", "#fff6ea");
        var ink = M("door_ink", "#2b2350");
        var knob = M("door_knob", "#e8b94a", 0.5f);
        foreach (bool east in new[] { false, true })
        {
            int target = HomeRules.DoorTarget(room, east);
            if (target < 0) continue;
            float sgn = east ? 1f : -1f;
            var door = Group(east ? "Door_E" : "Door_W", t);
            door.transform.position = V(5.9f * sgn, 0, 0);
            var hd = door.AddComponent<HomeDoor>();
            hd.target = target; hd.east = east;
            var leaf = M("door_" + target, DoorHex[target], 0.2f);
            Prim(PrimitiveType.Cube, "Frame", door.transform, V(0.02f * sgn, 1.2f, 0), V(0.14f, 2.5f, 2.0f), frame);
            Prim(PrimitiveType.Cube, "Leaf", door.transform, V(-0.06f * sgn, 1.1f, 0), V(0.1f, 2.2f, 1.6f), leaf);
            Prim(PrimitiveType.Sphere, "Knob", door.transform, V(-0.16f * sgn, 1.1f, 0.5f * sgn), V(0.14f, 0.14f, 0.14f), knob, null, false);
            // a mat in front, and a sign over the door that reads from the room
            Prim(PrimitiveType.Cube, "Mat", door.transform, V(-0.8f * sgn, 0.025f, 0), V(1.0f, 0.04f, 1.6f), M("door_mat", "#a8a9fc", 0.1f, null), null, false);
            var sign = Group("Sign", door.transform);
            sign.transform.localPosition = V(-0.12f * sgn, 2.75f, 0);
            sign.transform.localRotation = Quaternion.Euler(0, east ? 90f : -90f, 0);
            Prim(PrimitiveType.Cube, "Board", sign.transform, V(0, 0, -0.04f), V(2.2f, 0.5f, 0.08f), leaf);
            PixelText(sign.transform, DoorText[target], V(0, 0, -0.12f), 0.055f, ink, "hm_text_" + DoorText[target].ToLowerInvariant(), 0.3f);
        }
    }

    static void MakeDoorMat(Transform t)
    {
        var mat = M("outmat", "#a8a9fc", 0.1f, new Color(0.35f, 0.35f, 0.9f));
        var white = M("outtext", "#ffffff", 0.1f, new Color(0.9f, 0.9f, 1f));
        var door = Empty("DoorOut", t, V(0, 0, -5.0f));
        Prim(PrimitiveType.Cube, "Mat", door.transform, V(0, 0.03f, 0), V(3.0f, 0.04f, 1.5f), mat, null, false);
        var text = PixelText(door.transform, "OUT", V(0, 0.07f, 0), 0.12f, white, "hm_text_out", 0.2f);
        text.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        Prim(PrimitiveType.Cube, "ArrowShaft", door.transform, V(0, 0.07f, -0.5f), V(0.1f, 0.02f, 0.3f), white, null, false);
    }

    /// <summary>The put-away box: a chest by the front wall, where the pieces she is not using wait.</summary>
    static void MakeBox(Transform t)
    {
        var box = Empty("PutAwayBox", t, V(4.25f, 0, -5.25f));
        var wood = M("chest", "#c98a52", 0.25f);
        var band = M("chest_band", "#e8b94a", 0.5f);
        var pink = M("chest_pink", "#f98bb0", 0.2f);
        Prim(PrimitiveType.Cube, "Body", box.transform, V(0, 0.35f, 0), V(1.3f, 0.7f, 0.9f), wood);
        Prim(PrimitiveType.Cylinder, "Lid", box.transform, V(0, 0.72f, 0), V(0.9f, 0.65f, 0.9f), wood, V(0, 0, 90f));
        Prim(PrimitiveType.Cube, "BandL", box.transform, V(-0.4f, 0.4f, -0.01f), V(0.1f, 0.78f, 0.94f), band);
        Prim(PrimitiveType.Cube, "BandR", box.transform, V(0.4f, 0.4f, -0.01f), V(0.1f, 0.78f, 0.94f), band);
        Prim(PrimitiveType.Cube, "Clasp", box.transform, V(0, 0.62f, -0.47f), V(0.18f, 0.2f, 0.06f), band);
        Prim(PrimitiveType.Sphere, "Star", box.transform, V(0.2f, 1.1f, 0), V(0.22f, 0.22f, 0.22f), pink, null, false);
    }

    // ---------- the player, riff and the camera ----------

    static void Populate(PlaceKit.Look look, UnityEngine.Rendering.VolumeProfile profile)
    {
        Empty("PlayerSpawn", null, V(0, 0, -3.0f));
        var info = Group("PlaceInfo").AddComponent<WorldPlace>();
        info.placeId = "coffee";
        info.interior = "home";

        var cam = MakeCamera(profile, look);
        cam.clearFlags = CameraClearFlags.SolidColor;
        var player = Group("Player").transform;
        player.position = V(0, 0, -3.0f);
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
        follow.offset = V(0, 6.2f, -9.8f);
        follow.tiltUp = 9f;
        follow.lead = 0.4f;
        follow.clearRadius = 0.9f;

        var st = new GameObject("SpikeStatus").AddComponent<SpikeStatus>();
        st.player = player; st.dog = dogGo.transform;
    }

    // ---------- the stock: prefabs, ghost materials, the plate ----------

    static Material Ghost(string name, Color c)
    {
        var m = LoadOrNew(name, "Universal Render Pipeline/Lit");
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", 5f);
        m.SetFloat("_DstBlend", 10f);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Smoothness", 0.1f);
        m.SetFloat("_Metallic", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_EMISSION");
        m.renderQueue = 3000;
        EditorUtility.SetDirty(m);
        return m;
    }

    static void MakeStock(List<HomePrefab> pieces)
    {
        var go = Group("HomeStock");
        var stock = go.AddComponent<HomeStock>();
        stock.pieces = pieces.ToArray();
        stock.ok = Ghost("home_ghost_ok", new Color(0.3f, 0.85f, 0.45f, 0.55f));
        stock.bad = Ghost("home_ghost_bad", new Color(0.95f, 0.25f, 0.3f, 0.55f));
        stock.pick = Ghost("home_ghost_pick", new Color(1f, 0.85f, 0.2f, 0.5f));
        var plate = Prim(PrimitiveType.Cube, "HomePlate", go.transform, V(0, 0.09f, 0), V(1, 0.03f, 1), stock.ok, null, false);
        plate.SetActive(false);
        stock.plate = plate;
    }

    // ---------- the pieces ----------

    static Bounds BoundsOf(GameObject g)
    {
        var b = new Bounds(g.transform.position, Vector3.zero);
        bool first = true;
        foreach (var r in g.GetComponentsInChildren<Renderer>(true)) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
        return b;
    }

    /// <summary>An FBX scaled to fit a w x d footprint and h height, standing on y = 0 with its middle at the origin of <paramref name="parent"/> (which is at the world origin).</summary>
    static GameObject Fit(string path, Transform parent, Material mat, float w, float d, float h, float yaw, string name = null)
    {
        var g = Fbx(path, parent, Vector3.zero, yaw, 1f, mat, name);
        Bounds b = BoundsOf(g);
        float s = Mathf.Min(w / Mathf.Max(0.001f, b.size.x), d / Mathf.Max(0.001f, b.size.z), h / Mathf.Max(0.001f, b.size.y));
        g.transform.localScale *= s;
        b = BoundsOf(g);
        g.transform.position += new Vector3(-b.center.x, -b.min.y, -b.center.z);
        return g;
    }

    static List<HomePrefab> BuildPieces()
    {
        var list = new List<HomePrefab>();
        foreach (HomePiece p in HomeCatalogue.All)
        {
            var root = Group(p.Id.Replace(':', '_'));
            try { BuildPiece(p, root.transform); }
            catch (Exception e) { Debug.LogError("home piece " + p.Id + ": " + e.Message); }
            string path = PieceDir + "/" + root.name + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            list.Add(new HomePrefab { id = p.Id, prefab = prefab });
        }
        return list;
    }

    static void BuildPiece(HomePiece p, Transform t)
    {
        float W = p.W * 0.5f, D = p.D * 0.5f;
        float fw = Mathf.Max(0.3f, W - 0.06f), fd = Mathf.Max(0.3f, D - 0.06f);
        var wood = M("wood", "#d9a273", 0.25f);
        var dark = M("darkwood", "#a8714a", 0.25f);
        var cream = M("cream", "#fff3e2", 0.1f);
        var white = M("white", "#fffaf3", 0.2f);
        var pink = M("pink", "#f6a6c3", 0.15f);
        var hot = M("hot", "#e8648c", 0.15f);
        var mint = M("mint", "#8fe3c4", 0.15f);
        var lilac = M("lilac", "#a8a9fc", 0.15f);
        var sky = M("sky", "#8ec9f0", 0.2f);
        var sun = M("sun", "#ffd34e", 0.2f);
        var steel = M("steel", "#c9ccdf", 0.6f);
        var green = M("green", "#5fb870", 0.1f);
        var ink = M("ink", "#2b2350", 0.2f);
        var gold = M("gold", "#e8b94a", 0.5f);
        var glow = M("glow", "#fff3c4", 0.2f, new Color(2.2f, 1.7f, 0.9f));
        switch (p.Id)
        {
            // ---- seating
            case "furn:sofa": Fit(Furn + "couch.fbx", t, furnMat, fw, fd, 1.1f, 180f, "Couch"); break;
            case "furn:armchair": Fit(Furn + "armchair.fbx", t, furnMat, fw, fd, 1.1f, 180f, "Armchair"); break;
            case "furn:chair": Fit(Resto + "chair_A.fbx", t, restoMat, fw, fd, 1.0f, 180f, "Chair"); break;
            case "furn:bench": Fit(City + "bench.fbx", t, cityMat, fw, fd, 1.0f, 180f, "Bench"); break;
            case "furn:beanbag":
                Prim(PrimitiveType.Sphere, "Bag", t, V(0, 0.32f, 0), V(W * 0.92f, 0.68f, D * 0.92f), hot);
                Prim(PrimitiveType.Sphere, "Top", t, V(0, 0.5f, 0.1f), V(W * 0.5f, 0.3f, D * 0.5f), pink);
                break;
            case "furn:stool":
                Prim(PrimitiveType.Cylinder, "Seat", t, V(0, 0.52f, 0), V(0.4f, 0.05f, 0.4f), sun);
                foreach (float a in new[] { 45f, 135f, 225f, 315f }) Prim(PrimitiveType.Cylinder, "Leg", t, V(0.16f * Mathf.Cos(a * Mathf.Deg2Rad), 0.25f, 0.16f * Mathf.Sin(a * Mathf.Deg2Rad)), V(0.04f, 0.26f, 0.04f), dark);
                break;
            // ---- tables
            case "furn:table_low": Fit(Furn + "table_low.fbx", t, furnMat, fw, fd, 0.7f, 0f, "LowTable"); break;
            case "furn:table_round": Fit(Resto + "table_round_A_small.fbx", t, restoMat, fw, fd, 1.0f, 0f, "RoundTable"); break;
            case "furn:desk":
                Prim(PrimitiveType.Cube, "Top", t, V(0, 0.76f, 0), V(W, 0.06f, D), wood);
                Prim(PrimitiveType.Cube, "SideL", t, V(-W / 2f + 0.05f, 0.38f, 0), V(0.06f, 0.76f, D - 0.05f), dark);
                Prim(PrimitiveType.Cube, "SideR", t, V(W / 2f - 0.05f, 0.38f, 0), V(0.06f, 0.76f, D - 0.05f), dark);
                Prim(PrimitiveType.Cube, "Drawer", t, V(W / 2f - 0.5f, 0.55f, -D / 2f + 0.02f), V(0.8f, 0.3f, 0.04f), pink, null, false);
                Prim(PrimitiveType.Cube, "Book", t, V(-0.4f, 0.82f, 0.1f), V(0.3f, 0.05f, 0.22f), mint, V(0, 15f, 0));
                break;
            case "furn:table_dining":
                Prim(PrimitiveType.Cube, "Top", t, V(0, 0.8f, 0), V(W, 0.07f, D), wood);
                foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f }) Prim(PrimitiveType.Cube, "Leg", t, V(sx * (W / 2f - 0.1f), 0.4f, sz * (D / 2f - 0.1f)), V(0.08f, 0.8f, 0.08f), dark);
                Prim(PrimitiveType.Cylinder, "Bowl", t, V(0, 0.88f, 0), V(0.35f, 0.07f, 0.35f), sun);
                break;
            case "furn:table_side":
                Prim(PrimitiveType.Cylinder, "Top", t, V(0, 0.52f, 0), V(0.42f, 0.03f, 0.42f), wood);
                Prim(PrimitiveType.Cylinder, "Leg", t, V(0, 0.26f, 0), V(0.06f, 0.26f, 0.06f), dark);
                Prim(PrimitiveType.Cylinder, "Foot", t, V(0, 0.02f, 0), V(0.3f, 0.02f, 0.3f), dark);
                break;
            // ---- beds (head at +z, the wall side)
            case "furn:bed_single": Bed(t, W, D, wood, white, pink, 1, false); break;
            case "furn:bed_day": Bed(t, W, D, dark, white, mint, 1, false); break;
            case "furn:bed_big": Bed(t, W, D, dark, white, lilac, 2, false); break;
            case "furn:bed_bunk": Bed(t, W, D, wood, white, sky, 1, true); break;
            // ---- lamps
            case "furn:lamp_floor": Fit(Furn + "lamp_standing.fbx", t, furnMat, 0.5f, 0.5f, 1.8f, 0f, "Lamp"); break;
            case "furn:lamp_table":
                Prim(PrimitiveType.Cylinder, "Base", t, V(0, 0.08f, 0), V(0.26f, 0.04f, 0.26f), dark);
                Prim(PrimitiveType.Cylinder, "Stem", t, V(0, 0.25f, 0), V(0.05f, 0.18f, 0.05f), steel);
                Prim(PrimitiveType.Cylinder, "Shade", t, V(0, 0.5f, 0), V(0.34f, 0.14f, 0.34f), glow);
                break;
            case "furn:lamp_globe":
                Prim(PrimitiveType.Cylinder, "Base", t, V(0, 0.03f, 0), V(0.3f, 0.03f, 0.3f), steel);
                Prim(PrimitiveType.Cylinder, "Pole", t, V(0, 0.6f, 0), V(0.05f, 0.6f, 0.05f), steel);
                Prim(PrimitiveType.Sphere, "Globe", t, V(0, 1.3f, 0), V(0.46f, 0.46f, 0.46f), glow);
                break;
            case "furn:lamp_star":
                Prim(PrimitiveType.Cylinder, "Base", t, V(0, 0.03f, 0), V(0.3f, 0.03f, 0.3f), steel);
                Prim(PrimitiveType.Cylinder, "Pole", t, V(0, 0.6f, 0), V(0.05f, 0.6f, 0.05f), steel);
                StarSign("Star", t, V(0, 1.35f, 0), 0.5f, M("starglow", "#ffe48a", 0.2f, new Color(2.4f, 1.9f, 0.6f)), 180f, 0.4f);
                break;
            // ---- plants
            case "furn:cactus": Fit(Furn + "cactus_medium_A.fbx", t, furnMat, 0.4f, 0.4f, 1.0f, 0f, "Cactus"); break;
            case "furn:cactus_small": Fit(Furn + "cactus_small_A.fbx", t, furnMat, 0.4f, 0.4f, 0.5f, 0f, "Cactus"); break;
            case "furn:cactus_tiny": Fit(Furn + "cactus_small_B.fbx", t, furnMat, 0.35f, 0.35f, 0.45f, 0f, "Cactus"); break;
            case "furn:bush": Fit(Forest + "Bush_2_A_Color1.fbx", t, forestMat, fw, fd, 1.0f, 0f, "Bush"); break;
            case "furn:tree": Fit(Forest + "Tree_4_A_Color1.fbx", t, forestMat, fw * 0.8f, fd * 0.8f, 3.0f, 0f, "Tree"); break;
            case "furn:fern":
                Pot(t, M("pot_terra", "#d98b5f", 0.2f), 0.18f, 0.3f);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * 60f;
                    Prim(PrimitiveType.Sphere, "Leaf", t, V(0.12f * Mathf.Cos(a * Mathf.Deg2Rad), 0.5f, 0.12f * Mathf.Sin(a * Mathf.Deg2Rad)), V(0.09f, 0.36f, 0.09f), green, V(Mathf.Sin(a * Mathf.Deg2Rad) * 35f, 0, -Mathf.Cos(a * Mathf.Deg2Rad) * 35f));
                }
                break;
            case "furn:flowers":
                Pot(t, M("pot_white", "#fffaf3", 0.2f), 0.17f, 0.28f);
                for (int i = 0; i < 4; i++)
                {
                    float x = (i - 1.5f) * 0.08f, h = 0.5f + (i % 2) * 0.1f;
                    Prim(PrimitiveType.Cylinder, "Stem", t, V(x, 0.28f + h / 2f - 0.12f, 0), V(0.015f, h / 2f - 0.06f, 0.015f), green, null, false);
                    Prim(PrimitiveType.Sphere, "Bloom", t, V(x, 0.28f + h - 0.05f, 0), V(0.14f, 0.14f, 0.14f), i % 2 == 0 ? hot : sun, null, false);
                }
                break;
            // ---- rugs
            case "furn:rug_round":
                Prim(PrimitiveType.Cylinder, "Rug", t, V(0, 0.015f, 0), V(W * 0.98f, 0.015f, D * 0.98f), M("rug_pink", "#fdb1cb", 0.05f), null, false);
                Prim(PrimitiveType.Cylinder, "Inner", t, V(0, 0.032f, 0), V(W * 0.7f, 0.015f, D * 0.7f), M("rug_cream", "#fff6ea", 0.05f), null, false);
                break;
            case "furn:rug_big":
                Prim(PrimitiveType.Cube, "Edge", t, V(0, 0.015f, 0), V(W * 0.99f, 0.03f, D * 0.99f), M("rug_navy", "#2c4190", 0.05f), null, false);
                Prim(PrimitiveType.Cube, "Rug", t, V(0, 0.032f, 0), V(W * 0.9f, 0.03f, D * 0.88f), M("rug_sky", "#8ec9f0", 0.05f), null, false);
                break;
            case "furn:rug_star":
                Prim(PrimitiveType.Cylinder, "Rug", t, V(0, 0.015f, 0), V(W * 0.98f, 0.015f, D * 0.98f), M("rug_lilac", "#a8a9fc", 0.05f), null, false);
                var star = StarSign("Star", t, V(0, 0.04f, 0), W * 0.62f, M("rug_sun", "#ffd34e", 0.05f), 0f, 0.05f);
                star.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                break;
            case "furn:rug_runner":
                Prim(PrimitiveType.Cube, "Rug", t, V(0, 0.015f, 0), V(W * 0.98f, 0.03f, D * 0.9f), M("rug_mint", "#8fe3c4", 0.05f), null, false);
                for (int i = 0; i < 4; i++) Prim(PrimitiveType.Cube, "Stripe", t, V(-W * 0.36f + i * W * 0.24f, 0.034f, 0), V(W * 0.07f, 0.03f, D * 0.9f), white, null, false);
                break;
            // ---- posters (hang toward +z, art toward -z)
            case "furn:poster_heart":
                PosterFrame(t, W, pink);
                Prim(PrimitiveType.Sphere, "HeartL", t, V(-0.14f, 1.8f, 0.14f), V(0.34f, 0.34f, 0.08f), hot, null, false);
                Prim(PrimitiveType.Sphere, "HeartR", t, V(0.14f, 1.8f, 0.14f), V(0.34f, 0.34f, 0.08f), hot, null, false);
                Prim(PrimitiveType.Cube, "HeartTip", t, V(0, 1.6f, 0.14f), V(0.3f, 0.3f, 0.08f), hot, V(0, 0, 45f), false);
                break;
            case "furn:poster_star":
                PosterFrame(t, W, ink);
                StarSign("Star", t, V(0, 1.7f, 0.14f), 0.75f, M("poster_sun", "#ffd34e", 0.2f), 180f, 0.1f);
                break;
            case "furn:poster_note":
                PosterFrame(t, W, mint);
                Prim(PrimitiveType.Sphere, "Head", t, V(-0.12f, 1.4f, 0.14f), V(0.28f, 0.2f, 0.08f), ink, null, false);
                Prim(PrimitiveType.Cube, "Stem", t, V(0.0f, 1.75f, 0.14f), V(0.05f, 0.7f, 0.06f), ink, null, false);
                Prim(PrimitiveType.Cube, "Flag", t, V(0.12f, 2.05f, 0.14f), V(0.28f, 0.1f, 0.06f), ink, V(0, 0, -20f), false);
                break;
            case "furn:poster_hills":
                PosterFrame(t, W, sky);
                Prim(PrimitiveType.Sphere, "Sun", t, V(0.35f, 2.05f, 0.14f), V(0.28f, 0.28f, 0.06f), sun, null, false);
                Prim(PrimitiveType.Sphere, "HillA", t, V(-0.3f, 1.2f, 0.14f), V(0.9f, 0.7f, 0.06f), green, null, false);
                Prim(PrimitiveType.Sphere, "HillB", t, V(0.3f, 1.2f, 0.12f), V(0.8f, 0.55f, 0.06f), M("hill2", "#3f9a5a", 0.1f), null, false);
                break;
            case "furn:poster_guitar":
                PosterFrame(t, W, lilac);
                Prim(PrimitiveType.Sphere, "Body", t, V(0, 1.45f, 0.14f), V(0.42f, 0.5f, 0.07f), M("g_wood", "#c98a52", 0.2f), null, false);
                Prim(PrimitiveType.Cube, "Neck", t, V(0, 1.95f, 0.14f), V(0.07f, 0.6f, 0.05f), dark, null, false);
                Prim(PrimitiveType.Cylinder, "Hole", t, V(0, 1.45f, 0.1f), V(0.14f, 0.01f, 0.14f), ink, V(90f, 0, 0), false);
                break;
            case "furn:poster_map":
                PosterFrame(t, W, steel);
                Prim(PrimitiveType.Cube, "Sea", t, V(0, 1.7f, 0.14f), V(1.25f, 0.8f, 0.04f), sky, null, false);
                Prim(PrimitiveType.Sphere, "LandA", t, V(-0.3f, 1.8f, 0.12f), V(0.4f, 0.3f, 0.04f), green, null, false);
                Prim(PrimitiveType.Sphere, "LandB", t, V(0.3f, 1.6f, 0.12f), V(0.45f, 0.35f, 0.04f), M("land2", "#e8c066", 0.1f), null, false);
                break;
            // ---- shelves
            case "furn:shelf_books":
            {
                Prim(PrimitiveType.Cube, "Back", t, V(0, 1.0f, D / 2f - 0.02f), V(W, 2.0f, 0.04f), dark);
                Prim(PrimitiveType.Cube, "SideL", t, V(-W / 2f + 0.02f, 1.0f, 0), V(0.04f, 2.0f, D), wood);
                Prim(PrimitiveType.Cube, "SideR", t, V(W / 2f - 0.02f, 1.0f, 0), V(0.04f, 2.0f, D), wood);
                string[] cols = { "#e8648c", "#8fe3c4", "#ffd34e", "#a8a9fc", "#6cc8f0", "#fdb1cb" };
                for (int s = 0; s < 5; s++)
                {
                    Prim(PrimitiveType.Cube, "Board", t, V(0, 0.04f + s * 0.48f, 0), V(W, 0.04f, D), wood);
                    if (s == 4) break;
                    for (int i = 0; i < 9; i++)
                        Prim(PrimitiveType.Cube, "Book", t, V(-W / 2f + 0.18f + i * (W - 0.3f) / 9f, 0.28f + s * 0.48f, 0.0f), V(0.08f, 0.32f + (i % 3) * 0.04f, 0.2f), M("book" + ((i + s) % 6), cols[(i + s) % 6], 0.2f), null, false);
                }
                break;
            }
            case "furn:shelf_cubby":
                Prim(PrimitiveType.Cube, "Back", t, V(0, 0.6f, D / 2f - 0.02f), V(W, 1.2f, 0.04f), dark);
                foreach (float x in new[] { -W / 2f + 0.02f, 0f, W / 2f - 0.02f }) Prim(PrimitiveType.Cube, "Divider", t, V(x, 0.6f, 0), V(0.04f, 1.2f, D), wood);
                foreach (float y in new[] { 0.03f, 0.6f, 1.18f }) Prim(PrimitiveType.Cube, "Board", t, V(0, y, 0), V(W, 0.04f, D), wood);
                Prim(PrimitiveType.Sphere, "Ball", t, V(-0.25f, 0.2f, 0), V(0.22f, 0.22f, 0.22f), hot);
                Prim(PrimitiveType.Cube, "Box", t, V(0.25f, 0.78f, 0), V(0.3f, 0.3f, 0.2f), mint);
                break;
            case "furn:shelf_guitar":
                foreach (float x in new[] { -0.4f, 0.4f }) Prim(PrimitiveType.Cube, "Leg", t, V(x, 0.6f, 0), V(0.05f, 1.2f, 0.05f), steel, V(0, 0, x > 0 ? -6f : 6f));
                Prim(PrimitiveType.Cube, "Bar", t, V(0, 0.45f, 0.1f), V(1.0f, 0.04f, 0.04f), steel);
                Prim(PrimitiveType.Sphere, "Body", t, V(0, 0.4f, 0), V(0.4f, 0.5f, 0.1f), M("g_wood", "#c98a52", 0.2f));
                Prim(PrimitiveType.Cube, "Neck", t, V(0, 0.9f, 0), V(0.07f, 0.6f, 0.05f), dark);
                Prim(PrimitiveType.Cube, "Head", t, V(0, 1.25f, 0), V(0.12f, 0.15f, 0.05f), dark);
                break;
            case "furn:shelf_crates":
                Fit(Resto + "crate.fbx", t, restoMat, 0.44f, 0.44f, 0.5f, 0f, "CrateA");
                var c2 = Fit(Resto + "crate.fbx", t, restoMat, 0.44f, 0.44f, 0.5f, 25f, "CrateB");
                c2.transform.position += V(0, 0.5f, 0);
                break;
            case "furn:shelf_jars":
                foreach (float x in new[] { -W / 2f + 0.03f, W / 2f - 0.03f }) Prim(PrimitiveType.Cube, "Post", t, V(x, 0.8f, 0), V(0.05f, 1.6f, D), wood);
                for (int s = 0; s < 3; s++)
                {
                    Prim(PrimitiveType.Cube, "Board", t, V(0, 0.4f + s * 0.55f, 0), V(W, 0.04f, D), wood);
                    for (int i = 0; i < 3; i++) Fit(i % 2 == 0 ? Resto + "jar_A_large.fbx" : Resto + "jar_B_medium.fbx", t, restoMat, 0.3f, 0.3f, 0.36f, 0f, "Jar").transform.position += V(-W / 2f + 0.3f + i * 0.45f, 0.42f + s * 0.55f, 0);
                }
                break;
            // ---- riff
            case "furn:riff_bed":
                Prim(PrimitiveType.Cylinder, "Rim", t, V(0, 0.1f, 0), V(W * 0.95f, 0.1f, D * 0.95f), M("rbed_rim", "#b5651d", 0.2f));
                Prim(PrimitiveType.Cylinder, "Cushion", t, V(0, 0.16f, 0), V(W * 0.78f, 0.08f, D * 0.78f), pink);
                Prim(PrimitiveType.Cube, "Bone", t, V(0, 0.26f, -0.2f), V(0.28f, 0.06f, 0.08f), white, null, false);
                break;
            case "furn:riff_cushion":
                Prim(PrimitiveType.Cube, "Cushion", t, V(0, 0.1f, 0), V(W * 0.9f, 0.2f, D * 0.9f), sun);
                Prim(PrimitiveType.Cube, "Trim", t, V(0, 0.2f, 0), V(W * 0.9f, 0.03f, D * 0.9f), hot);
                break;
            case "furn:riff_house":
                Prim(PrimitiveType.Cube, "Body", t, V(0, 0.45f, 0), V(W * 0.85f, 0.9f, D * 0.85f), M("house_wall", "#fff3e2", 0.1f));
                Prim(PrimitiveType.Cube, "RoofL", t, V(-W * 0.22f, 1.05f, 0), V(W * 0.55f, 0.08f, D * 0.95f), hot, V(0, 0, 28f));
                Prim(PrimitiveType.Cube, "RoofR", t, V(W * 0.22f, 1.05f, 0), V(W * 0.55f, 0.08f, D * 0.95f), hot, V(0, 0, -28f));
                Prim(PrimitiveType.Cylinder, "Door", t, V(0, 0.32f, -D * 0.43f), V(0.45f, 0.02f, 0.45f), ink, V(90f, 0, 0), false);
                break;
            case "furn:riff_bowls":
                Prim(PrimitiveType.Cube, "Mat", t, V(0, 0.01f, 0), V(0.46f, 0.02f, 0.3f), M("bowl_mat", "#6cc8f0", 0.1f), null, false);
                Prim(PrimitiveType.Cylinder, "BowlA", t, V(-0.11f, 0.06f, 0), V(0.18f, 0.04f, 0.18f), hot);
                Prim(PrimitiveType.Cylinder, "BowlB", t, V(0.11f, 0.06f, 0), V(0.18f, 0.04f, 0.18f), steel);
                break;
            case "furn:riff_toys":
                Prim(PrimitiveType.Cylinder, "Basket", t, V(0, 0.12f, 0), V(0.4f, 0.12f, 0.4f), M("basket", "#d9a273", 0.15f));
                Prim(PrimitiveType.Sphere, "BallA", t, V(-0.08f, 0.28f, 0.02f), V(0.16f, 0.16f, 0.16f), hot);
                Prim(PrimitiveType.Sphere, "BallB", t, V(0.1f, 0.26f, -0.04f), V(0.14f, 0.14f, 0.14f), sun);
                Prim(PrimitiveType.Cube, "Bone", t, V(0, 0.32f, 0.08f), V(0.22f, 0.05f, 0.07f), white, V(0, 20f, 0), false);
                break;
            // ---- decor
            case "furn:jar_big": Fit(Resto + "jar_A_large.fbx", t, restoMat, 0.4f, 0.4f, 0.9f, 0f, "Jar"); break;
            case "furn:jar_small": Fit(Resto + "jar_B_medium.fbx", t, restoMat, 0.35f, 0.35f, 0.55f, 0f, "Jar"); break;
            case "furn:menu": Fit(Resto + "menu.fbx", t, restoMat, 0.5f, 0.45f, 1.4f, 180f, "Board"); break;
            case "furn:globe":
                Prim(PrimitiveType.Cylinder, "Foot", t, V(0, 0.03f, 0), V(0.3f, 0.03f, 0.3f), dark);
                Prim(PrimitiveType.Cylinder, "Stem", t, V(0, 0.3f, 0), V(0.04f, 0.3f, 0.04f), dark);
                Prim(PrimitiveType.Sphere, "Globe", t, V(0, 0.72f, 0), V(0.46f, 0.46f, 0.46f), sky);
                Prim(PrimitiveType.Sphere, "Land", t, V(0.06f, 0.78f, -0.12f), V(0.22f, 0.18f, 0.2f), green, null, false);
                break;
            case "furn:trophy":
                Prim(PrimitiveType.Cube, "Base", t, V(0, 0.06f, 0), V(0.3f, 0.12f, 0.3f), dark);
                Prim(PrimitiveType.Cylinder, "Stem", t, V(0, 0.28f, 0), V(0.05f, 0.14f, 0.05f), gold);
                Prim(PrimitiveType.Cylinder, "Cup", t, V(0, 0.55f, 0), V(0.26f, 0.12f, 0.26f), gold);
                break;
            case "furn:speaker":
                Prim(PrimitiveType.Cube, "Box", t, V(0, 0.55f, 0), V(0.4f, 1.1f, 0.4f), ink);
                Prim(PrimitiveType.Cylinder, "Cone", t, V(0, 0.38f, -0.2f), V(0.28f, 0.02f, 0.28f), steel, V(90f, 0, 0), false);
                Prim(PrimitiveType.Cylinder, "Tweeter", t, V(0, 0.85f, -0.2f), V(0.14f, 0.02f, 0.14f), steel, V(90f, 0, 0), false);
                break;
            case "furn:keys":
                Prim(PrimitiveType.Cube, "Body", t, V(0, 0.8f, 0.1f), V(W * 0.95f, 0.1f, 0.34f), ink);
                for (int i = 0; i < 14; i++) Prim(PrimitiveType.Cube, "Key", t, V(-W * 0.45f + i * W * 0.9f / 13f, 0.87f, -0.02f), V(0.07f, 0.03f, 0.22f), white, null, false);
                foreach (float x in new[] { -W / 2f + 0.1f, W / 2f - 0.1f }) Prim(PrimitiveType.Cube, "Leg", t, V(x, 0.38f, 0.1f), V(0.05f, 0.76f, 0.05f), steel);
                break;
            case "furn:drums":
                Prim(PrimitiveType.Cylinder, "Kick", t, V(0, 0.35f, 0.3f), V(0.7f, 0.2f, 0.7f), hot, V(90f, 0, 0));
                Prim(PrimitiveType.Cylinder, "TomA", t, V(-0.25f, 0.85f, 0.1f), V(0.34f, 0.08f, 0.34f), white);
                Prim(PrimitiveType.Cylinder, "TomB", t, V(0.25f, 0.85f, 0.1f), V(0.34f, 0.08f, 0.34f), white);
                Prim(PrimitiveType.Cylinder, "Snare", t, V(-0.55f, 0.6f, -0.2f), V(0.36f, 0.07f, 0.36f), white);
                Prim(PrimitiveType.Cylinder, "Cymbal", t, V(0.6f, 1.15f, 0.2f), V(0.4f, 0.01f, 0.4f), gold, V(8f, 0, 0), false);
                Prim(PrimitiveType.Cylinder, "Stool", t, V(0, 0.34f, -0.55f), V(0.34f, 0.04f, 0.34f), ink);
                break;
            case "furn:tank":
                Prim(PrimitiveType.Cube, "Stand", t, V(0, 0.3f, 0), V(W * 0.95f, 0.6f, D * 0.9f), dark);
                Prim(PrimitiveType.Cube, "Water", t, V(0, 0.9f, 0), V(W * 0.9f, 0.55f, D * 0.8f), M("water", "#6cc8f0", 0.6f));
                Prim(PrimitiveType.Sphere, "FishA", t, V(-0.2f, 0.92f, -0.1f), V(0.14f, 0.09f, 0.05f), M("fish1", "#ff9a52", 0.3f), null, false);
                Prim(PrimitiveType.Sphere, "FishB", t, V(0.3f, 0.85f, -0.1f), V(0.12f, 0.08f, 0.05f), M("fish2", "#ffd34e", 0.3f), null, false);
                Prim(PrimitiveType.Cube, "Lid", t, V(0, 1.2f, 0), V(W * 0.95f, 0.04f, D * 0.88f), steel);
                break;
            default:
                Prim(PrimitiveType.Cube, "Block", t, V(0, 0.25f, 0), V(W * 0.9f, 0.5f, D * 0.9f), pink);
                Debug.LogWarning("home piece " + p.Id + " has no model yet");
                break;
        }
    }

    static void Pot(Transform t, Material mat, float r, float h)
    {
        Prim(PrimitiveType.Cylinder, "Pot", t, V(0, h / 2f, 0), V(r * 2f, h / 2f, r * 2f), mat);
    }

    /// <summary>A frame on the wall: its middle at 1.7 m, 1.5 m wide at most, toward +z.</summary>
    static void PosterFrame(Transform t, float w, Material mat)
    {
        Prim(PrimitiveType.Cube, "Frame", t, V(0, 1.7f, 0.2f), V(Mathf.Min(1.5f, w * 0.9f), 1.25f, 0.06f), M("frame_white", "#fffaf3", 0.2f));
        Prim(PrimitiveType.Cube, "Paper", t, V(0, 1.7f, 0.16f), V(Mathf.Min(1.5f, w * 0.9f) - 0.14f, 1.11f, 0.04f), mat, null, false);
    }

    static void Bed(Transform t, float W, float D, Material frame, Material sheet, Material blanket, int pillows, bool bunk)
    {
        float top = bunk ? 1.3f : 0.0f;
        Prim(PrimitiveType.Cube, "Frame", t, V(0, 0.22f, 0), V(W, 0.18f, D), frame);
        Prim(PrimitiveType.Cube, "Mattress", t, V(0, 0.4f, -0.04f), V(W - 0.1f, 0.2f, D - 0.12f), sheet);
        Prim(PrimitiveType.Cube, "Blanket", t, V(0, 0.52f, -D * 0.16f), V(W - 0.08f, 0.1f, D * 0.62f), blanket);
        Prim(PrimitiveType.Cube, "Headboard", t, V(0, 0.7f, D / 2f - 0.05f), V(W, 0.9f, 0.1f), frame);
        for (int i = 0; i < pillows; i++)
            Prim(PrimitiveType.Cube, "Pillow", t, V(pillows == 1 ? 0f : (i == 0 ? -W * 0.22f : W * 0.22f), 0.58f, D / 2f - 0.3f), V(pillows == 1 ? W * 0.6f : W * 0.38f, 0.14f, 0.34f), M("pillow", "#fff6ea", 0.1f));
        foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f }) Prim(PrimitiveType.Cube, "Leg", t, V(sx * (W / 2f - 0.05f), 0.08f, sz * (D / 2f - 0.05f)), V(0.08f, 0.16f, 0.08f), frame);
        if (bunk)
        {
            foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f }) Prim(PrimitiveType.Cube, "Post", t, V(sx * (W / 2f - 0.05f), 0.9f, sz * (D / 2f - 0.05f)), V(0.08f, 1.8f, 0.08f), frame);
            Prim(PrimitiveType.Cube, "TopFrame", t, V(0, top + 0.3f, 0), V(W, 0.14f, D), frame);
            Prim(PrimitiveType.Cube, "TopMattress", t, V(0, top + 0.43f, 0), V(W - 0.1f, 0.14f, D - 0.12f), sheet);
            Prim(PrimitiveType.Cube, "TopBlanket", t, V(0, top + 0.52f, -D * 0.15f), V(W - 0.08f, 0.08f, D * 0.6f), blanket);
            Prim(PrimitiveType.Cube, "Rail", t, V(0, top + 0.8f, -D / 2f + 0.04f), V(W, 0.06f, 0.05f), frame);
            Prim(PrimitiveType.Cube, "LadderL", t, V(W / 2f - 0.3f, 0.65f, -D / 2f - 0.04f), V(0.05f, 1.3f, 0.05f), frame);
            Prim(PrimitiveType.Cube, "LadderR", t, V(W / 2f - 0.6f, 0.65f, -D / 2f - 0.04f), V(0.05f, 1.3f, 0.05f), frame);
            for (int i = 0; i < 4; i++) Prim(PrimitiveType.Cube, "Rung", t, V(W / 2f - 0.45f, 0.25f + i * 0.3f, -D / 2f - 0.04f), V(0.3f, 0.04f, 0.05f), frame);
        }
    }

    // ---------- the door on the coffee street ----------

    /// <summary>
    /// Opens Place_Coffee and turns the left-hand window of the shop row (Front_1, x = -11) into a door with its own sign: "HOME" on a pink panel.
    /// "HomeDoor" is the empty the gameplay binds (its child Stand is where she is put back on the street). Safe to run again.
    /// </summary>
    static void PatchCoffee()
    {
        var scene = EditorSceneManager.OpenScene(CoffeeScene, OpenSceneMode.Single);
        InitKits("", null, true);
        foreach (string n in new[] { "HomeDoor", "HomeFront", "HomeDoorModel", "HomeSign" })
            foreach (var g in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (g != null && g.name == n) UnityEngine.Object.DestroyImmediate(g);
        var shop = GameObject.Find("CoffeeShop");
        if (shop == null) throw new Exception("no CoffeeShop group in Place_Coffee");
        Transform t = shop.transform;
        Transform window = t.Find("Front_1");
        if (window != null) UnityEngine.Object.DestroyImmediate(window.gameObject);
        Fbx(Resto + "wall_doorway.fbx", t, V(-11f, 0, 8.2f), 180f, 1f, restoMat, "HomeFront");
        Fbx(Resto + "door_A.fbx", t, V(-11f, 0, 8.2f), 180f, 1f, restoMat, "HomeDoorModel");
        var rose = Lit("hm_sign", C("#f98bb0"), 0.2f);
        var ink = Lit("hm_sign_ink", C("#2b2350"), 0.2f);
        var panel = Prim(PrimitiveType.Cube, "HomeSign", t, V(-11f, 5.85f, 7.95f), V(3.5f, 1.5f, 0.25f), rose);
        PixelText(panel.transform, "HOME", V(0, 0.1f, -0.62f), 0.07f, ink, "hm_text_home_door", 0.4f);
        Prim(PrimitiveType.Cube, "Hanger", panel.transform, V(0, -0.35f, -0.62f), V(0.4f, 0.05f, 0.1f), ink);
        Prim(PrimitiveType.Cube, "HangerHook", panel.transform, V(0, -0.22f, -0.62f), V(0.05f, 0.2f, 0.1f), ink);
        var door = new GameObject("HomeDoor");
        door.transform.position = V(-11f, 0, 7.3f);
        var stand = new GameObject("Stand");
        stand.transform.SetParent(door.transform, false);
        stand.transform.position = V(-11.8f, 0, 5.3f);
        SaveScene(scene, CoffeeScene);
    }
}
