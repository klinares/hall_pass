// Phase 8.1 setup. Makes the gameplay layer's small assets (Assets/World/Gameplay) and adds the placeholder gameplay empties
// and crew to the WorldSpike scene, so the gameplay can be played and tested before a real place scene exists.
//
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -executeMethod WorldGameplaySetup.Run
//
// The place scenes made by the art builder only need the empties PlayerSpawn, Crew_1 to Crew_3, StageSpot, CrowdArea and ExitSpot;
// nothing in this file is needed for them (the gameplay finds everything by name at run time).
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WorldTour.Game.Ui;
using WorldTour.World;
using WorldTour.World.Gameplay;

public static class WorldGameplaySetup
{
    const string Root = "Assets/World/Gameplay";
    const string AssetsPath = Root + "/Resources/WorldGameplayAssets.asset";
    const string SpikeScene = "Assets/World/Scenes/WorldSpike.unity";
    const string SoftDot = "Assets/World/Textures/soft_dot.png";
    const string ButtonPrefab = "Assets/Game/Ui/Prefabs/StickerButton.prefab";

    static readonly Color[] FigureColors =
    {
        new Color(0.98f, 0.55f, 0.69f), new Color(0.66f, 0.66f, 0.99f), new Color(0.98f, 0.85f, 0.5f),
        new Color(0.55f, 0.85f, 0.7f), new Color(0.95f, 0.65f, 0.45f), new Color(0.75f, 0.6f, 0.9f),
    };

    [MenuItem("World Tour/Set up world gameplay")]
    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Root + "/Resources");
            Directory.CreateDirectory(Root + "/Materials");
            MakeAssets();
            AddToSpikeScene();
            AssetDatabase.SaveAssets();
            Debug.Log("WorldGameplaySetup: done");
        }
        catch (System.Exception e)
        {
            Debug.LogError("WorldGameplaySetup failed: " + e);
        }
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static Material Make(string path, Shader shader, Color c, float smooth = 0.25f)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void MakeAssets()
    {
        var assets = AssetDatabase.LoadAssetAtPath<WorldGameplayAssets>(AssetsPath);
        if (assets == null) { assets = ScriptableObject.CreateInstance<WorldGameplayAssets>(); AssetDatabase.CreateAsset(assets, AssetsPath); }

        var btn = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefab);
        assets.buttonPrefab = btn != null ? btn.GetComponent<StickerButton>() : null;
        if (assets.buttonPrefab == null) Debug.LogWarning("WorldGameplaySetup: no StickerButton prefab at " + ButtonPrefab);

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        var figs = new Material[FigureColors.Length];
        for (int i = 0; i < figs.Length; i++) figs[i] = Make(Root + "/Materials/crowd_" + i + ".mat", lit, FigureColors[i]);
        assets.figureMaterials = figs;
        assets.capsuleMesh = Resources.GetBuiltinResource<Mesh>("Capsule.fbx");
        assets.sphereMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");

        Shader add = Shader.Find("Legacy Shaders/Particles/Additive");
        if (add == null) add = Shader.Find("Mobile/Particles/Additive");
        if (add != null)
        {
            var burst = Make(Root + "/Materials/cheer_burst.mat", add, new Color(1f, 1f, 1f, 1f));
            if (burst.HasProperty("_TintColor")) burst.SetColor("_TintColor", new Color(0.9f, 0.9f, 0.9f, 0.9f));
            var dot = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftDot);
            if (dot != null && burst.HasProperty("_MainTex")) burst.SetTexture("_MainTex", dot);
            assets.burstMaterial = burst;
        }
        else Debug.LogWarning("WorldGameplaySetup: no additive particle shader found; the cheer burst is skipped");
        EditorUtility.SetDirty(assets);
    }

    static GameObject Empty(Transform parent, string name, Vector3 pos)
    {
        var old = GameObject.Find(name);
        if (old != null) Object.DestroyImmediate(old);
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.position = pos;
        return g;
    }

    static void AddToSpikeScene()
    {
        var scene = EditorSceneManager.OpenScene(SpikeScene, OpenSceneMode.Single);
        var star = GameObject.Find("PopStar");
        Vector3 start = star != null ? star.transform.position : new Vector3(0, 0, -6);

        var root = GameObject.Find("Gameplay");
        if (root == null) root = new GameObject("Gameplay");
        var t = root.transform;

        Empty(t, "PlayerSpawn", start);
        Empty(t, "StageSpot", new Vector3(0, 0, 6.0f));
        Empty(t, "CrowdArea", new Vector3(0, 0, 2.2f));
        Empty(t, "ExitSpot", new Vector3(0, 0, -12.6f));

        var crewColors = new[] { new Color(0.98f, 0.85f, 0.5f), new Color(0.55f, 0.85f, 0.7f), new Color(0.75f, 0.6f, 0.9f) };
        var crewPos = new[] { new Vector3(-7.5f, 0, 1.5f), new Vector3(7.5f, 0, -3.5f), new Vector3(-3f, 0, -8f) };
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        for (int i = 0; i < 3; i++)
        {
            var crew = Empty(t, "Crew_" + (i + 1), crewPos[i]);
            var mat = Make(Root + "/Materials/crew_" + (i + 1) + ".mat", lit, crewColors[i]);
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body"; body.transform.SetParent(crew.transform, false);
            body.transform.localPosition = new Vector3(0, 0.85f, 0); body.transform.localScale = new Vector3(0.7f, 0.85f, 0.7f);
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head"; head.transform.SetParent(crew.transform, false);
            head.transform.localPosition = new Vector3(0, 1.85f, 0); head.transform.localScale = new Vector3(0.62f, 0.62f, 0.62f);
            foreach (var g in new[] { body, head })
            {
                Object.DestroyImmediate(g.GetComponent<Collider>());
                g.GetComponent<Renderer>().sharedMaterial = mat;
            }
            var ob = crew.AddComponent<Obstacle>();
            ob.radius = 0.4f;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
