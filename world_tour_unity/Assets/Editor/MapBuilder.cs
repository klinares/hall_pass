// Builds the tour map's assets by code (task 2.4), so no prefab or scene is edited by hand.
//
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -executeMethod MapBuilder.Build [-mapSummary <file>]
//
// Run after UiBuilder.BuildAll (it remakes Main.unity with the empty home) and after task 2.1's sprites are imported.
// Re-runnable; each run replaces what it made before:
//   Assets/Game/Map/MapArt.asset                  the sprite manifest plus every map, sticker, decor and ui sprite by name
//   Assets/Game/Ui/Prefabs/Screen_Venue.prefab    VenueCard
//   Assets/Game/Ui/Prefabs/Screen_Map.prefab      MapScreen (with the venue card prefab)
//   Assets/Scenes/Main.unity                      MainRoot.home = Screen_Map
// Writes a summary JSON (default Temp/MapBuilder-summary.json) and ends in EditorApplication.Exit(0 or 1) in batch mode.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WorldTour.Core.Data;
using WorldTour.Core.Maths.Gen;
using WorldTour.Game.Map;
using WorldTour.Game.Ui;

public static class MapBuilder
{
    const string ManifestPath = "Assets/Art/Sprites/manifest.json";
    const string SpriteRoot = "Assets/Art/Sprites/";
    const string ArtPath = "Assets/Game/Map/MapArt.asset";
    const string PrefabDir = "Assets/Game/Ui/Prefabs";
    const string ButtonPrefabPath = PrefabDir + "/StickerButton.prefab";
    const string VenuePrefabPath = PrefabDir + "/Screen_Venue.prefab";
    const string MapPrefabPath = PrefabDir + "/Screen_Map.prefab";
    const string MainScenePath = "Assets/Scenes/Main.unity";
    static readonly string[] Groups = { "map", "stickers", "decor", "ui" };

    [Serializable]
    public class Summary
    {
        public string method;
        public bool ok;
        public int sprites;
        public List<string> created = new List<string>();
        public List<string> wired = new List<string>();
        public List<string> warnings = new List<string>();
        public List<string> errors = new List<string>();
    }

    static Summary s_Summary;

    [MenuItem("World Tour/UI/Build tour map (2.4)")]
    static void BuildMenu() { Build(); }

    public static void Build()
    {
        s_Summary = new Summary { method = "Build" };
        try
        {
            MapArt art = MakeArt();
            StickerButton button = AssetDatabase.LoadAssetAtPath<StickerButton>(ButtonPrefabPath);
            if (button == null) throw new Exception(ButtonPrefabPath + " not found: run UiBuilder.BuildAll first");
            VenueCard card = MakeScreen<VenueCard>("Screen_Venue", VenuePrefabPath, so =>
            {
                Set(so, "art", art);
                Set(so, "buttonPrefab", button);
            });
            MapScreen map = MakeScreen<MapScreen>("Screen_Map", MapPrefabPath, so =>
            {
                Set(so, "art", art);
                Set(so, "buttonPrefab", button);
                Set(so, "venueCardPrefab", card);
            });
            WireMain(map);
            AssetDatabase.SaveAssets();
            s_Summary.ok = s_Summary.errors.Count == 0;
        }
        catch (Exception e)
        {
            s_Summary.errors.Add(e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
            s_Summary.ok = false;
        }
        finally
        {
            string path = Arg("-mapSummary") ?? Path.Combine("Temp", "MapBuilder-summary.json");
            try
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, JsonUtility.ToJson(s_Summary, true));
                Debug.Log("[MapBuilder] Build" + (s_Summary.ok ? " ok" : " FAILED") + ", summary: " + Path.GetFullPath(path));
            }
            catch (Exception e) { Debug.LogError("[MapBuilder] could not write the summary: " + e.Message); }
            if (Application.isBatchMode) EditorApplication.Exit(s_Summary.ok ? 0 : 1);
        }
    }

    static MapArt MakeArt()
    {
        var manifest = AssetDatabase.LoadAssetAtPath<TextAsset>(ManifestPath);
        if (manifest == null) throw new Exception(ManifestPath + " not found: task 2.1's sprites must be imported first");
        JsonValue root = StrictJson.Parse(manifest.text);
        MapLayout.FromManifest(root["map"]);   // throws now, not at runtime, if the map section is unreadable

        bool existed = File.Exists(ArtPath);
        MapArt art = AssetDatabase.LoadAssetAtPath<MapArt>(ArtPath);
        if (art == null)
        {
            art = ScriptableObject.CreateInstance<MapArt>();
            AssetDatabase.CreateAsset(art, ArtPath);
        }
        art.manifest = manifest;
        art.sprites = new List<MapArt.Entry>();
        foreach (JsonValue s in root["sprites"].Items)
        {
            string group = s["group"] != null ? s["group"].String : "";
            if (Array.IndexOf(Groups, group) < 0) continue;
            string file = SpriteRoot + s["file"].String;
            Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(file);
            if (sp == null) { s_Summary.warnings.Add("no sprite at " + file); continue; }
            art.sprites.Add(new MapArt.Entry { name = s["name"].String, sprite = sp });
        }
        foreach (Venue v in Venues.All)
            if (art.sprites.FindIndex(e => e.name == "venue_" + v.Art) < 0) s_Summary.errors.Add("missing venue sprite venue_" + v.Art);
        s_Summary.sprites = art.sprites.Count;
        EditorUtility.SetDirty(art);
        if (existed) s_Summary.warnings.Add("replaced " + ArtPath); else s_Summary.created.Add(ArtPath);
        return art;
    }

    static T MakeScreen<T>(string name, string path, Action<SerializedObject> wire) where T : GameScreen
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        go.AddComponent<CanvasGroup>();
        T screen = go.AddComponent<T>();
        var so = new SerializedObject(screen);
        wire(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        bool existed = File.Exists(path);
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, path, out bool success);
        UnityEngine.Object.DestroyImmediate(go);
        if (!success || saved == null) throw new Exception("could not save " + path);
        if (existed) s_Summary.warnings.Add("replaced " + path); else s_Summary.created.Add(path);
        return saved.GetComponent<T>();
    }

    static void WireMain(MapScreen map)
    {
        if (!File.Exists(MainScenePath)) throw new Exception(MainScenePath + " not found: run UiBuilder.BuildAll first");
        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        MainRoot root = null;
        foreach (GameObject g in scene.GetRootGameObjects()) { root = g.GetComponentInChildren<MainRoot>(true); if (root != null) break; }
        if (root == null) throw new Exception("Main.unity has no MainRoot");
        // OpenScene can unload the in-memory prefab object passed in, which then saves as a null reference: reload by path.
        GameObject mapAsset = AssetDatabase.LoadAssetAtPath<GameObject>(MapPrefabPath);
        map = mapAsset != null ? mapAsset.GetComponent<MapScreen>() : null;
        if (map == null) throw new Exception("could not reload " + MapPrefabPath);
        var so = new SerializedObject(root);
        Set(so, "home", map);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("could not save " + MainScenePath);
        s_Summary.wired.Add("MainRoot.home = " + MapPrefabPath);
    }

    static void Set(SerializedObject so, string field, UnityEngine.Object value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null) throw new Exception(so.targetObject.GetType().Name + " has no serialized field '" + field + "'");
        p.objectReferenceValue = value;
    }

    static string Arg(string name)
    {
        string[] a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return null;
    }
}
