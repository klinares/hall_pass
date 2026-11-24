// Phase 8.1: builds everything the place scenes share from the imported CC0 art (see ASSETS.md): recoloured palette textures,
// materials, the two Animator controllers, the Star / crew / Riff / marker prefabs. Re-runnable (assets are updated in place).
//
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -executeMethod WorldArtBuilder.BuildAll
//
// The KayKit characters sample one small palette texture (8 x 4 gradient swatches). A recolour here is a copy of that texture
// with some swatches repainted, applied per body part (the torso and arms, the legs and the head are separate renderers).
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using WorldTour.World;

public static class WorldArtBuilder
{
    public const string Art = "Assets/World/Art";
    public const string PrefabDir = "Assets/World/Prefabs";
    public const string MatDir = "Assets/World/Materials/Art";
    public const string TexDir = "Assets/World/Textures/Art";
    public const string AnimDir = "Assets/World/Animation";
    public const string MeshDir = "Assets/World/Prefabs/Meshes";
    public const string CharacterController = AnimDir + "/CharacterController.controller";
    public const string DogController = AnimDir + "/RiffController.controller";
    public const string StarPrefab = PrefabDir + "/Star.prefab";
    public const string RiffPrefab = PrefabDir + "/Riff.prefab";
    public const string MarkerPrefab = PrefabDir + "/InteractionMarker.prefab";
    public static readonly string[] CrewPrefabs = { PrefabDir + "/Crew_Mint.prefab", PrefabDir + "/Crew_Orange.prefab", PrefabDir + "/Crew_Yellow.prefab" };

    public const float CharacterScale = 0.85f;

    static readonly System.Collections.Generic.Dictionary<string, Material> MatCache = new System.Collections.Generic.Dictionary<string, Material>();

    public static void BuildAll()
    {
        try
        {
            Run();
            Debug.Log("WorldArtBuilder: done");
        }
        catch (System.Exception e)
        {
            Debug.LogError("WorldArtBuilder failed: " + e);
        }
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    public static void Run()
    {
        foreach (var d in new[] { PrefabDir, MatDir, TexDir, AnimDir, MeshDir })
            Directory.CreateDirectory(d);
        MatCache.Clear();
        WorldArtImport.ApplySettings();
        AssetDatabase.Refresh();
        MakeControllers();
        MakeStar();
        MakeCrew();
        MakeCrowd();
        MakeRiff();
        MakeMarker();
        AssetDatabase.SaveAssets();
    }

    // ---------- colour helpers ----------

    public static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

    /// <summary>The standard lit material (URP Lit), updated in place when it exists.</summary>
    public static Material Lit(string name, Color color, float smooth = 0.15f, Texture2D tex = null, Color? emission = null)
    {
        if (MatCache.TryGetValue(name, out var cached)) return cached;
        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        m.SetTexture("_BaseMap", tex);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", 0f);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        else
        {
            m.DisableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
        }
        EditorUtility.SetDirty(m);
        MatCache[name] = m;
        return m;
    }

    static Texture2D LoadRaw(string assetPath)
    {
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        t.LoadImage(File.ReadAllBytes(assetPath));
        return t;
    }

    /// <summary>A 256 x 256 copy of a 1024 palette texture with some of its 8 x 4 swatches repainted as top-to-bottom gradients.</summary>
    static Texture2D Recolor(string srcAsset, string outName, params (int cell, string top, string bottom)[] edits)
    {
        var src = LoadRaw(srcAsset);
        int sw = src.width, sh = src.height;
        var sp = src.GetPixels32();
        const int n = 256;
        var px = new Color32[n * n];
        int bx = sw / n, by = sh / n;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int r = 0, g = 0, b = 0, a = 0;
                for (int j = 0; j < by; j++)
                    for (int i = 0; i < bx; i++)
                    {
                        var c = sp[(y * by + j) * sw + (x * bx + i)];
                        r += c.r; g += c.g; b += c.b; a += c.a;
                    }
                int k = bx * by;
                px[y * n + x] = new Color32((byte)(r / k), (byte)(g / k), (byte)(b / k), (byte)(a / k));
            }
        Object.DestroyImmediate(src);
        int cw = n / 8, ch = n / 4;
        foreach (var e in edits)
        {
            int col = e.cell % 8, row = e.cell / 8;
            Color top = C(e.top), bot = C(e.bottom);
            for (int iy = 0; iy < ch; iy++)
            {
                float t = iy / (float)(ch - 1);
                Color c = Color.Lerp(top, bot, t);
                int y = n - 1 - (row * ch + iy);
                for (int ix = 0; ix < cw; ix++)
                {
                    Color cc = ix >= cw * 27 / 32 ? c * 0.9f : c;
                    cc.a = 1f;
                    px[y * n + col * cw + ix] = cc;
                }
            }
        }
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        string path = TexDir + "/" + outName + ".png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.filterMode = FilterMode.Point;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.maxTextureSize = 256;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    public static Texture2D ArtTexture(string path) { return AssetDatabase.LoadAssetAtPath<Texture2D>(path); }

    // ---------- animator controllers ----------

    static AnimationClip Clip(string assetPath, string clipName)
    {
        var c = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<AnimationClip>().FirstOrDefault(a => a.name == clipName);
        if (c == null) throw new System.Exception("clip " + clipName + " not found in " + assetPath);
        return c;
    }

    /// <summary>One of the six baked KayKit clips (Assets/World/Animation/Clips, made by WorldArtBake from the CC0 animation FBX files).</summary>
    public static AnimationClip KayClip(string name)
    {
        var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimDir + "/Clips/" + name + ".anim");
        if (c == null) throw new System.Exception("baked clip missing: " + name + " (run WorldArtBake.Run with the animation FBX files imported)");
        return c;
    }

    static void MakeControllers()
    {
        // The shared character controller (Star and the crew): Speed (0 idle, ~2 walk, ~5 run), Talk and Cheer triggers, Listen bool.
        AssetDatabase.DeleteAsset(CharacterController);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(CharacterController);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Talk", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Cheer", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Listen", AnimatorControllerParameterType.Bool);
        var sm = ctrl.layers[0].stateMachine;

        ctrl.CreateBlendTreeInController("Move", out BlendTree bt);
        bt.blendParameter = "Speed";
        bt.useAutomaticThresholds = false;
        bt.AddChild(KayClip("Idle_A"), 0f);
        bt.AddChild(KayClip("Walking_A"), 2f);
        bt.AddChild(KayClip("Running_A"), 5f);
        var move = sm.states.First(s => s.state.name == "Move").state;
        sm.defaultState = move;

        var listen = sm.AddState("Listen");
        listen.motion = KayClip("Idle_B");
        var talk = sm.AddState("Talk");
        talk.motion = KayClip("Interact");
        var cheer = sm.AddState("Cheer");
        cheer.motion = KayClip("Cheering");

        var t = move.AddTransition(listen);
        t.hasExitTime = false; t.duration = 0.2f;
        t.AddCondition(AnimatorConditionMode.If, 0, "Listen");
        t.AddCondition(AnimatorConditionMode.Less, 0.15f, "Speed");
        t = listen.AddTransition(move);
        t.hasExitTime = false; t.duration = 0.2f;
        t.AddCondition(AnimatorConditionMode.IfNot, 0, "Listen");
        t = listen.AddTransition(move);
        t.hasExitTime = false; t.duration = 0.15f;
        t.AddCondition(AnimatorConditionMode.Greater, 0.3f, "Speed");

        foreach (var (state, trigger) in new[] { (talk, "Talk"), (cheer, "Cheer") })
        {
            var any = sm.AddAnyStateTransition(state);
            any.hasExitTime = false; any.duration = 0.12f; any.canTransitionToSelf = false;
            any.AddCondition(AnimatorConditionMode.If, 0, trigger);
            var back = state.AddTransition(move);
            back.hasExitTime = true; back.exitTime = 0.92f; back.duration = 0.2f;
        }
        var layers = ctrl.layers;
        layers[0].iKPass = true;
        ctrl.layers = layers;
        EditorUtility.SetDirty(ctrl);

        // Riff: the single float Speed (metres a second); the walk plays faster as he runs.
        string dogFbx = Art + "/Quaternius_Dog/Dog.fbx";
        AssetDatabase.DeleteAsset(DogController);
        var dc = AnimatorController.CreateAnimatorControllerAtPath(DogController);
        dc.AddParameter("Speed", AnimatorControllerParameterType.Float);
        dc.CreateBlendTreeInController("Move", out BlendTree dbt);
        dbt.blendParameter = "Speed";
        dbt.useAutomaticThresholds = false;
        dbt.AddChild(Clip(dogFbx, "DogArmature|Idle"), 0f);
        dbt.AddChild(Clip(dogFbx, "DogArmature|Walking"), 1.2f);
        dbt.AddChild(Clip(dogFbx, "DogArmature|Walking"), 5.6f);
        var kids = dbt.children;
        kids[1].timeScale = 1.0f;
        kids[2].timeScale = 2.4f;
        dbt.children = kids;
        EditorUtility.SetDirty(dc);
    }

    // ---------- characters ----------

    static readonly string[] BodyParts = { "_Arm", "_Body", "_Head", "_Leg" };

    static GameObject CleanModel(string fbx, string rootName)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        var go = Object.Instantiate(src);
        go.name = rootName;
        // Keep only the body parts: no hats, hoods, capes, weapons, shields or books.
        foreach (var r in go.GetComponentsInChildren<Renderer>(true).ToArray())
        {
            if (!BodyParts.Any(p => r.name.Contains(p))) Object.DestroyImmediate(r.gameObject);
        }
        go.transform.localScale = Vector3.one * CharacterScale;
        var anim = go.GetComponent<Animator>();
        anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CharacterController);
        anim.applyRootMotion = false;
        go.AddComponent<StandOnHeight>();
        return go;
    }

    static void Dress(GameObject go, Material top, Material legs, Material head)
    {
        foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (r.name.Contains("_Leg")) r.sharedMaterial = legs;
            else if (r.name.Contains("_Head")) r.sharedMaterial = head;
            else r.sharedMaterial = top;
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = true;
        }
    }

    static Material TexMat(string name, Texture2D tex) { return Lit(name, Color.white, 0.1f, tex); }

    static void SavePrefab(GameObject go, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }

    static void MakeStar()
    {
        string tdir = Art + "/KayKit_Adventurers/";
        var topTex = Recolor(tdir + "rogue_texture.png", "star_top",
            (8, "#ffc4da", "#f58bb2"), (9, "#c7c8ff", "#8f90ec"), (5, "#b9baff", "#8586e0"), (6, "#3d3d9e", "#26266e"),
            (15, "#3b3b92", "#20205c"), (21, "#fdd2b4", "#f0a47f"));
        var legTex = Recolor(tdir + "rogue_texture.png", "star_legs",
            (15, "#34409a", "#1b2166"), (19, "#2f3880", "#181d58"));
        var headTex = ArtTexture(tdir + "rogue_texture.png");
        var go = CleanModel(tdir + "Rogue.fbx", "Star");
        Dress(go, TexMat("star_top", topTex), TexMat("star_legs", legTex), TexMat("star_head", headTex));
        SavePrefab(go, StarPrefab);
    }

    // ---------- looks: casual outfits from the three KayKit bodies ----------
    // The KayKit palette is one small texture; a "look" is a 256 px copy of it per body part with some swatches repainted.
    // Knight body = hoodie + jeans (belt swatches become the hoodie's own darker shade), Mage = a rain jacket, Rogue = a zip jacket.
    // Hats, hoods, capes, weapons and shields are not in the prefabs; the leftover belts are repainted into the clothes.

    sealed class LookSpec
    {
        public string Name, Model, Top, Legs, Hair, Skin, Accent;
        public LookSpec(string name, string model, string top, string legs, string hair, string skin = null, string accent = null)
        { Name = name; Model = model; Top = top; Legs = legs; Hair = hair; Skin = skin; Accent = accent; }
    }

    static string Hx(Color c) { return "#" + ColorUtility.ToHtmlStringRGB(c); }
    static (int, string, string) Cell(int cell, string hex, float lighten = 0.32f, float darken = 0.2f)
    {
        var c = C(hex);
        return (cell, Hx(Color.Lerp(c, Color.white, lighten)), Hx(Color.Lerp(c, Color.black, darken)));
    }

    static (Material top, Material legs, Material head) Look(LookSpec s)
    {
        string tdir = Art + "/KayKit_Adventurers/";
        string src = tdir + s.Model.ToLowerInvariant() + "_texture.png";
        var top = new System.Collections.Generic.List<(int, string, string)>();
        var legs = new System.Collections.Generic.List<(int, string, string)>();
        var head = new System.Collections.Generic.List<(int, string, string)>();
        string acc = s.Accent ?? "#ff8a6e";
        switch (s.Model)
        {
            case "Knight":
                top.AddRange(new[] { Cell(3, s.Top), Cell(4, s.Top, 0.1f, 0.38f), Cell(7, s.Top, 0.55f, 0.08f), Cell(10, s.Top, 0.22f, 0.25f),
                    Cell(8, acc, 0.35f, 0.15f), Cell(5, s.Top, 0.0f, 0.45f), Cell(6, s.Top, 0.0f, 0.45f), Cell(13, s.Top, 0.0f, 0.45f) });
                legs.AddRange(new[] { Cell(3, s.Legs), Cell(6, s.Legs, 0.1f, 0.3f), Cell(7, s.Legs, 0.3f, 0.1f), Cell(5, s.Legs, 0f, 0.5f), Cell(13, s.Legs, 0f, 0.5f) });
                head.Add(Cell(1, s.Hair, 0.25f, 0.3f));
                break;
            case "Mage":
                top.AddRange(new[] { Cell(8, s.Top), Cell(3, s.Top, 0.55f, 0.05f), Cell(4, s.Top, 0.18f, 0.3f), Cell(5, s.Top, 0.05f, 0.3f), Cell(18, s.Top, 0.0f, 0.5f) });
                legs.AddRange(new[] { Cell(19, s.Legs, 0.2f, 0.25f) });
                head.AddRange(new[] { Cell(1, s.Hair, 0.2f, 0.4f), Cell(2, s.Hair, 0.1f, 0.5f) });
                break;
            default:   // Rogue
                top.AddRange(new[] { Cell(8, s.Top), Cell(9, s.Top, 0.3f, 0.2f), Cell(5, s.Top, 0.55f, 0.08f), Cell(6, s.Top, 0.0f, 0.4f), Cell(15, s.Top, 0.0f, 0.55f) });
                legs.AddRange(new[] { Cell(15, s.Legs, 0.05f, 0.35f), Cell(19, s.Legs, 0f, 0.5f) });
                head.Add(Cell(1, s.Hair, 0.25f, 0.3f));
                break;
        }
        if (s.Skin != null)
        {
            top.Add(Cell(0, s.Skin, 0.25f, 0.2f)); head.Add(Cell(0, s.Skin, 0.25f, 0.2f));
            if (s.Model == "Rogue") top.Add(Cell(21, s.Skin, 0.25f, 0.2f));
        }
        else if (s.Model == "Rogue") top.Add(Cell(21, "#fdd2b4", 0.25f, 0.2f));
        var t = Recolor(src, s.Name + "_top", top.ToArray());
        var l = Recolor(src, s.Name + "_legs", legs.ToArray());
        var h = Recolor(src, s.Name + "_head", head.ToArray());
        return (TexMat(s.Name + "_top", t), TexMat(s.Name + "_legs", l), TexMat(s.Name + "_head", h));
    }

    static readonly LookSpec[] CrewLooks =
    {
        new LookSpec("crew_mint", "Knight", "#7fd8c0", "#4f77c4", "#e6c27a", null, "#ff9a80"),
        new LookSpec("crew_orange", "Mage", "#ff9a52", "#cdb98a", "#1f1f2b"),
        new LookSpec("crew_yellow", "Knight", "#ffd34e", "#4a5068", "#7a4a2e", "#e9b58c", "#ff8a6e"),
    };

    static readonly LookSpec[] CrowdLooks =
    {
        new LookSpec("crowd_knight_coral", "Knight", "#ff9a86", "#4f77c4", "#6b4128", null, "#ffe08a"),
        new LookSpec("crowd_knight_lilac", "Knight", "#b3a2ff", "#c9b78a", "#2a2230", "#c98e66", "#ffe08a"),
        new LookSpec("crowd_knight_sky", "Knight", "#7cc8ff", "#33406e", "#c8602e", "#f2c19c", "#fff0a0"),
        new LookSpec("crowd_mage_pink", "Mage", "#ff8fbd", "#59607a", "#1a1a22", "#c98e66"),
        new LookSpec("crowd_mage_teal", "Mage", "#5ccfc0", "#3a3d52", "#6b4128", "#f2c19c"),
        new LookSpec("crowd_mage_sun", "Mage", "#ffd25a", "#4f77c4", "#1a1a22", "#8d5a3c"),
        new LookSpec("crowd_rogue_lilac", "Rogue", "#b3a2ff", "#33406e", "#e6c27a", "#f2c19c"),
        new LookSpec("crowd_rogue_peach", "Rogue", "#ffb08a", "#6a7a4a", "#1a1a22", "#c98e66"),
        new LookSpec("crowd_rogue_mint", "Rogue", "#86e0b6", "#4f77c4", "#c8602e", "#fdd2b4"),
        new LookSpec("crowd_rogue_coral", "Rogue", "#ff8a8a", "#c9b78a", "#4b2f20", "#8d5a3c"),
    };

    static void MakeCrew()
    {
        string tdir = Art + "/KayKit_Adventurers/";
        for (int i = 0; i < 3; i++)
        {
            var spec = CrewLooks[i];
            var go = CleanModel(tdir + spec.Model + ".fbx", new[] { "Crew_Mint", "Crew_Orange", "Crew_Yellow" }[i]);
            go.AddComponent<LookAtPlayer>();
            var m = Look(spec);
            Dress(go, m.top, m.legs, m.head);
            // The crew stand on the ground; the lift is only for the walkers.
            Object.DestroyImmediate(go.GetComponent<StandOnHeight>());
            SavePrefab(go, CrewPrefabs[i]);
        }
    }

    // ---------- the crowd: baked figures ----------

    public const string CrowdDir = PrefabDir + "/Crowd";

    static void MakeCrowd()
    {
        Directory.CreateDirectory(CrowdDir);
        string tdir = Art + "/KayKit_Adventurers/";
        var looks = CrewLooks.Concat(CrowdLooks).ToArray();
        // one baked mesh pair per body, shared by every recolour of it
        var meshes = new System.Collections.Generic.Dictionary<string, (Mesh idle, Mesh cheer)>();
        foreach (var model in new[] { "Knight", "Mage", "Rogue" })
        {
            var go = CleanModel(tdir + model + ".fbx", "bake_" + model);
            go.transform.localScale = Vector3.one;   // baked at model scale; CrowdField applies the character scale
            go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity;
            var idle = BakeMesh(go, KayClip("Idle_A"), 0.4f, "Crowd_" + model + "_idle");
            var cheer = BakeMesh(go, KayClip("Cheering"), 0.32f, "Crowd_" + model + "_cheer");
            meshes[model] = (idle, cheer);
            Object.DestroyImmediate(go);
        }
        var figs = new System.Collections.Generic.List<CrowdFigure>();
        foreach (var spec in looks)
        {
            var m = Look(spec);
            var g = new GameObject("Crowd_" + spec.Name);
            var f = g.AddComponent<MeshFilter>();
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterials = new[] { m.top, m.head, m.legs };
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = true;
            var cf = g.AddComponent<CrowdFigure>();
            cf.filter = f; cf.idle = meshes[spec.Model].idle; cf.cheer = meshes[spec.Model].cheer;
            f.sharedMesh = cf.idle;
            string path = CrowdDir + "/" + g.name + ".prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(g, path);
            Object.DestroyImmediate(g);
            figs.Add(saved.GetComponent<CrowdFigure>());
        }
        // hand the list to the gameplay assets (Resources) so the scene needs no wiring
        const string assetsPath = "Assets/World/Gameplay/Resources/WorldGameplayAssets.asset";
        var assets = AssetDatabase.LoadAssetAtPath<WorldTour.World.Gameplay.WorldGameplayAssets>(assetsPath);
        if (assets != null) { assets.crowdFigures = figs.ToArray(); EditorUtility.SetDirty(assets); }
        else Debug.LogWarning("WorldArtBuilder: no " + assetsPath + " (run WorldGameplaySetup first); the crowd stays capsules");
    }

    /// <summary>Poses the model with a clip and bakes the skinned parts into one static mesh with three submeshes: top (arms and body), head, legs.</summary>
    static Mesh BakeMesh(GameObject root, AnimationClip clip, float time, string name)
    {
        clip.SampleAnimation(root, time);
        var groups = new[] { new System.Collections.Generic.List<CombineInstance>(), new System.Collections.Generic.List<CombineInstance>(), new System.Collections.Generic.List<CombineInstance>() };
        var temp = new System.Collections.Generic.List<Mesh>();
        Matrix4x4 toRoot = root.transform.worldToLocalMatrix;
        foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var baked = new Mesh();
            smr.BakeMesh(baked, true);   // local space of the renderer, its scale applied
            temp.Add(baked);
            // back to the root's space: rotation and position of the renderer, its scale is already in the vertices
            var m = toRoot * Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            int g = smr.name.Contains("_Head") ? 1 : smr.name.Contains("_Leg") ? 2 : 0;
            groups[g].Add(new CombineInstance { mesh = baked, subMeshIndex = 0, transform = m });
        }
        var parts = new CombineInstance[3];
        for (int g = 0; g < 3; g++)
        {
            var one = new Mesh();
            one.CombineMeshes(groups[g].ToArray(), true, true);
            temp.Add(one);
            parts[g] = new CombineInstance { mesh = one, subMeshIndex = 0, transform = Matrix4x4.identity };
        }
        var final = new Mesh { name = name };
        final.CombineMeshes(parts, false, false);
        final.RecalculateBounds();
        foreach (var t in temp) Object.DestroyImmediate(t);
        string path = MeshDir + "/" + name + ".asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old != null) { EditorUtility.CopySerialized(final, old); Object.DestroyImmediate(final); Debug.Log("WT_BAKE " + name + " " + old.bounds.size.ToString("F2") + " verts=" + old.vertexCount); return old; }
        AssetDatabase.CreateAsset(final, path);
        Debug.Log("WT_BAKE " + name + " " + final.bounds.size.ToString("F2") + " verts=" + final.vertexCount);
        return final;
    }

    // ---------- Riff ----------

    static GameObject Dummy(string name, Transform parent)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        return g;
    }

    static Transform BoneByName(Transform root, string name)
    {
        return root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
    }

    static GameObject AttachPrim(PrimitiveType type, string name, Transform bone, Vector3 worldPos, Vector3 worldScale, Material mat, Vector3? worldEuler = null)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(bone, true);
        g.transform.position = worldPos;
        if (worldEuler.HasValue) g.transform.rotation = Quaternion.Euler(worldEuler.Value);
        var ls = bone.lossyScale;
        g.transform.localScale = new Vector3(worldScale.x / ls.x, worldScale.y / ls.y, worldScale.z / ls.z);
        g.GetComponent<Renderer>().sharedMaterial = mat;
        return g;
    }

    // Dog model space (before the 0.2 scale): nose toward -x, tail toward +x, back at y ~ 3. The model already faces +z in the prefab.
    const float DogScale = 0.2f;

    static void MakeRiff()
    {
        var root = new GameObject("Riff");
        var lift = Dummy("Lift", root.transform);
        lift.AddComponent<StandOnHeight>();
        var rig = Dummy("Rig", lift.transform);
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/Quaternius_Dog/Dog.fbx");
        var model = Object.Instantiate(src);
        model.name = "Model";
        model.transform.SetParent(rig.transform, false);
        model.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);   // nose toward +z
        model.transform.localScale = Vector3.one * DogScale;
        var animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.AddComponent<Animator>();
        animator.avatar = AssetDatabase.LoadAllAssetsAtPath(Art + "/Quaternius_Dog/Dog.fbx").OfType<Avatar>().FirstOrDefault();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(DogController);
        animator.applyRootMotion = false;

        foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = true;
        }

        // Dark ears, a dark nose and a blue collar are painted on the mesh: a copy of it gets UVs into a 4-swatch palette
        // (fur, ear, nose, collar) chosen from each vertex's rest position (dog model units: the nose is toward -x, the tail toward +x, up +y).
        var smr0 = model.GetComponentInChildren<SkinnedMeshRenderer>();
        var baked = new Mesh();
        smr0.BakeMesh(baked, true);
        var bv = baked.vertices;
        var srcMesh = smr0.sharedMesh;
        var sv = srcMesh.vertices; var sn = srcMesh.normals; var sw = srcMesh.boneWeights; var st = srcMesh.triangles;
        var ov = new System.Collections.Generic.List<Vector3>(); var on = new System.Collections.Generic.List<Vector3>();
        var ow = new System.Collections.Generic.List<BoneWeight>(); var ou = new System.Collections.Generic.List<Vector2>();
        var ot = new System.Collections.Generic.List<int>();
        int nEar = 0, nNose = 0, nCollar = 0;
        // one colour per triangle (its centroid decides), so the palette never smears across a face
        for (int t = 0; t < st.Length; t += 3)
        {
            Vector3 c = Vector3.zero;
            for (int k = 0; k < 3; k++) c += model.transform.InverseTransformPoint(smr0.transform.TransformPoint(bv[st[t + k]]));
            c /= 3f;
            int cell = 0;
            if (c.x < -2.3f && c.x > -3.05f && Mathf.Abs(c.z + 0.33f) > 0.5f) { cell = 1; nEar++; }
            else if (c.x < -3.6f && c.y > 3.45f) { cell = 2; nNose++; }
            else if (c.y > 2.4f && c.x > -2.45f && c.x < -2.2f) { cell = 3; nCollar++; }
            for (int k = 0; k < 3; k++)
            {
                int vi = st[t + k];
                ot.Add(ov.Count);
                ov.Add(sv[vi]); on.Add(sn[vi]); ow.Add(sw[vi]); ou.Add(new Vector2((cell + 0.5f) / 4f, 0.5f));
            }
        }
        Debug.Log("WT_DOGPAINT ear tris=" + nEar + " nose tris=" + nNose + " collar tris=" + nCollar + " of " + st.Length / 3);
        var painted = new Mesh { name = "RiffMesh" };
        painted.SetVertices(ov); painted.SetNormals(on); painted.SetUVs(0, ou);
        painted.boneWeights = ow.ToArray(); painted.bindposes = srcMesh.bindposes;
        painted.SetTriangles(ot, 0);
        painted.RecalculateBounds();
        string meshPath = MeshDir + "/RiffMesh.asset";
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(painted, meshPath);
        smr0.sharedMesh = painted;
        var pal = new Texture2D(4, 1, TextureFormat.RGBA32, false);
        pal.SetPixels(new[] { C("#fffaf0"), C("#5a3a2c"), C("#1d1620"), C("#2f2fc1") });
        string palPath = TexDir + "/riff_palette.png";
        File.WriteAllBytes(palPath, pal.EncodeToPNG());
        Object.DestroyImmediate(pal);
        AssetDatabase.ImportAsset(palPath);
        var pimp = (TextureImporter)AssetImporter.GetAtPath(palPath);
        pimp.filterMode = FilterMode.Point; pimp.mipmapEnabled = false; pimp.wrapMode = TextureWrapMode.Clamp; pimp.maxTextureSize = 32;
        pimp.SaveAndReimport();
        var furMat = Lit("dog_fur", Color.white, 0.1f, AssetDatabase.LoadAssetAtPath<Texture2D>(palPath));
        smr0.sharedMaterial = furMat;

        // Dummy joints the spike's DogFollower writes its procedural trot into; the real animation is the Animator's.
        var tail = Dummy("Tail", root.transform);
        var fl = Dummy("LegFL", root.transform);
        var fr = Dummy("LegFR", root.transform);
        var bl = Dummy("LegBL", root.transform);
        var br = Dummy("LegBR", root.transform);
        var f = root.AddComponent<DogFollower>();
        f.rig = rig.transform; f.tail = tail.transform; f.legFL = fl.transform; f.legFR = fr.transform; f.legBL = bl.transform; f.legBR = br.transform;
        f.animator = animator;
        PrefabUtility.SaveAsPrefabAsset(root, RiffPrefab);
        Object.DestroyImmediate(root);
    }

    // ---------- the interaction marker ----------

    /// <summary>A puffy five-point star facing +z, flat shaded.</summary>
    public static Mesh StarMesh(float outer, float inner, float puff)
    {
        const int pts = 5;
        var ring = new Vector3[pts * 2];
        for (int i = 0; i < pts * 2; i++)
        {
            float a = Mathf.PI / 2f + i * Mathf.PI / pts;
            float r = (i & 1) == 0 ? outer : inner;
            ring[i] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
        }
        var verts = new System.Collections.Generic.List<Vector3>();
        var tris = new System.Collections.Generic.List<int>();
        void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            int k = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
        }
        var front = new Vector3(0, 0, puff);
        var back = new Vector3(0, 0, -puff);
        for (int i = 0; i < pts * 2; i++)
        {
            var a = ring[i]; var b = ring[(i + 1) % (pts * 2)];
            Tri(front, a, b);      // clockwise seen from +z means the ring runs counter-clockwise: swap to face +z
            Tri(back, b, a);
        }
        var m = new Mesh { name = "MarkerStar" };
        // Triangles above are wound (front, a, b) with the ring counter-clockwise seen from +z, which faces +z.
        m.SetVertices(verts);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    static void MakeMarker()
    {
        var mesh = StarMesh(0.62f, 0.3f, 0.2f);
        string meshPath = MeshDir + "/MarkerStar.asset";
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);

        var gold = Lit("marker_star", C("#ffd23a"), 0.35f, null, new Color(1.0f, 0.62f, 0.1f));
        var ink = Lit("marker_outline", C("#2f2fc1"), 0.2f, null, new Color(0.05f, 0.05f, 0.5f));
        var halo = HaloMaterial();

        var root = new GameObject("InteractionMarker");
        var holder = new GameObject("Holder").transform;
        holder.SetParent(root.transform, false);

        var star = new GameObject("Star");
        star.transform.SetParent(holder, false);
        star.AddComponent<MeshFilter>().sharedMesh = mesh;
        var sr = star.AddComponent<MeshRenderer>();
        sr.sharedMaterial = gold;
        sr.shadowCastingMode = ShadowCastingMode.Off;
        sr.receiveShadows = false;

        var outline = new GameObject("Outline");
        outline.transform.SetParent(holder, false);
        outline.transform.localScale = new Vector3(1.28f, 1.28f, 0.6f);
        outline.transform.localPosition = new Vector3(0, 0, -0.1f);
        outline.AddComponent<MeshFilter>().sharedMesh = mesh;
        var orr = outline.AddComponent<MeshRenderer>();
        orr.sharedMaterial = ink;
        orr.shadowCastingMode = ShadowCastingMode.Off;
        orr.receiveShadows = false;

        var glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
        glow.name = "Halo";
        Object.DestroyImmediate(glow.GetComponent<Collider>());
        glow.transform.SetParent(holder, false);
        glow.transform.localPosition = new Vector3(0, 0, -0.35f);
        glow.transform.localRotation = Quaternion.Euler(0, 180f, 0);
        glow.transform.localScale = Vector3.one * 3.2f;
        var gr = glow.GetComponent<MeshRenderer>();
        gr.sharedMaterial = halo;
        gr.shadowCastingMode = ShadowCastingMode.Off;
        gr.receiveShadows = false;

        var bob = root.AddComponent<MarkerBob>();
        bob.holder = holder;
        PrefabUtility.SaveAsPrefabAsset(root, MarkerPrefab);
        Object.DestroyImmediate(root);
    }

    public static Material HaloMaterial()
    {
        string path = MatDir + "/marker_halo.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(m, path);
        }
        var dot = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/World/Textures/soft_dot.png");
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 2f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent + 5;
        m.SetTexture("_BaseMap", dot);
        m.SetColor("_BaseColor", new Color(1.3f, 0.75f, 0.35f, 0.55f));
        EditorUtility.SetDirty(m);
        return m;
    }
}
