// Builds the phase 5 meta screens' assets by code (task 5.1), so no prefab or asset is edited by hand.
//
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -buildTarget WebGL -executeMethod MetaBuilder.Build [-metaSummary <file>]
//
// Run after UiBuilder.BuildAll and MapBuilder.Build. Re-runnable; each run replaces what it made before:
//   Assets/Art/Sprites/decor_peach-navy/*.png        reimported (SpriteImport's postprocessor gives them the tour-pink entry's settings)
//   Assets/Resources/WorldTour/Theme.asset           peachDecor = each decor sprite and its peach + navy twin, matched by file name
//   Assets/Game/Resources/Screens/Screen_Backstage.prefab   BackstageScreen (map art, button prefab, shout material)
//   Assets/Game/Resources/Screens/Screen_Journal.prefab     JournalScreen (the same)
// Writes a summary JSON (default Temp/MetaBuilder-summary.json) and ends in EditorApplication.Exit(0 or 1) in batch mode.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using WorldTour.Game.Map;
using WorldTour.Game.Meta;
using WorldTour.Game.Ui;

public static class MetaBuilder
{
    const string DecorPink = "Assets/Art/Sprites/decor";
    const string DecorPeach = "Assets/Art/Sprites/decor_peach-navy";
    const string DogPink = "Assets/Art/Sprites/dog";
    const string DogPeach = "Assets/Art/Sprites/dog_peach-navy";
    const string ThemePath = "Assets/Resources/WorldTour/Theme.asset";
    const string ArtPath = "Assets/Game/Map/MapArt.asset";
    const string ButtonPrefabPath = "Assets/Game/Ui/Prefabs/StickerButton.prefab";
    const string ShoutPath = "Assets/Art/Ticket/CaveatBrush SDF - Shout.mat";
    const string ScreensDir = "Assets/Game/Resources/Screens";

    [Serializable]
    public class Summary
    {
        public string method;
        public bool ok;
        public int twins;
        public int dogTwins;
        public List<string> created = new List<string>();
        public List<string> wired = new List<string>();
        public List<string> warnings = new List<string>();
        public List<string> errors = new List<string>();
    }

    static Summary s_Summary;

    [MenuItem("World Tour/UI/Build backstage and journal (5.1)")]
    static void BuildMenu() { Build(); }

    public static void Build()
    {
        s_Summary = new Summary { method = "Build" };
        try
        {
            WireTwins();
            var art = AssetDatabase.LoadAssetAtPath<MapArt>(ArtPath);
            if (art == null) throw new Exception(ArtPath + " not found: run MapBuilder.Build first");
            var button = AssetDatabase.LoadAssetAtPath<StickerButton>(ButtonPrefabPath);
            if (button == null) throw new Exception(ButtonPrefabPath + " not found: run UiBuilder.BuildAll first");
            var shout = AssetDatabase.LoadAssetAtPath<Material>(ShoutPath);
            if (shout == null) s_Summary.warnings.Add(ShoutPath + " not found: titles fall back to plain ink");
            Directory.CreateDirectory(ScreensDir);
            MakeScreen<BackstageScreen>("Screen_Backstage", art, button, shout);
            MakeScreen<JournalScreen>("Screen_Journal", art, button, shout);
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
            string path = Arg("-metaSummary") ?? Path.Combine("Temp", "MetaBuilder-summary.json");
            try
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, JsonUtility.ToJson(s_Summary, true));
                Debug.Log("[MetaBuilder] Build" + (s_Summary.ok ? " ok" : " FAILED") + ", summary: " + Path.GetFullPath(path));
            }
            catch (Exception e) { Debug.LogError("[MetaBuilder] could not write the summary: " + e.Message); }
            if (Application.isBatchMode) EditorApplication.Exit(s_Summary.ok ? 0 : 1);
        }
    }

    /// <summary>Reimports the peach + navy decor and lists each twin beside its tour-pink sprite in the theme asset.</summary>
    static void WireTwins()
    {
        var theme = AssetDatabase.LoadAssetAtPath<ThemeAsset>(ThemePath);
        if (theme == null) throw new Exception(ThemePath + " not found: run UiBuilder.BuildAll first");
        theme.peachDecor = new List<ThemeAsset.DecorTwin>();
        if (!AssetDatabase.IsValidFolder(DecorPeach)) { s_Summary.warnings.Add(DecorPeach + " is missing: the peach theme keeps the tour-pink decor"); EditorUtility.SetDirty(theme); return; }
        s_Dog = false;
        WireFolder(theme, DecorPeach, DecorPink);
        // riff's twins (6.2A): stickers and venues are drawn in fixed colours (no theme variables), so their peach render is
        // pixel-identical and has no twin; only the dog layers that take theme colours do.
        s_Dog = true;
        WireFolder(theme, DogPeach, DogPink);
        EditorUtility.SetDirty(theme);
        s_Summary.wired.Add(ThemePath + ": peachDecor x" + s_Summary.twins + " (dog x" + s_Summary.dogTwins + ")");
    }

    static bool s_Dog;

    static void WireFolder(ThemeAsset theme, string DecorPeach, string DecorPink)
    {
        if (!AssetDatabase.IsValidFolder(DecorPeach)) { s_Summary.warnings.Add(DecorPeach + " is missing"); return; }
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { DecorPeach }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
            string name = Path.GetFileName(p);
            var peach = AssetDatabase.LoadAssetAtPath<Sprite>(p);
            var pink = AssetDatabase.LoadAssetAtPath<Sprite>(DecorPink + "/" + name);
            if (peach == null || pink == null) { s_Summary.errors.Add("no sprite pair for " + name); continue; }
            if (peach.rect.size != pink.rect.size || peach.border != pink.border || peach.pivot != pink.pivot)
                s_Summary.errors.Add(name + ": the twin's size, border or pivot differs from tour pink");
            theme.peachDecor.Add(new ThemeAsset.DecorTwin { tourPink = pink, peachNavy = peach });
            s_Summary.twins++;
            if (s_Dog) s_Summary.dogTwins++;
        }
    }

    static void MakeScreen<T>(string name, MapArt art, StickerButton button, Material shout) where T : GameScreen
    {
        string path = ScreensDir + "/" + name + ".prefab";
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        go.AddComponent<CanvasGroup>();
        T screen = go.AddComponent<T>();
        var so = new SerializedObject(screen);
        Set(so, "art", art);
        Set(so, "buttonPrefab", button);
        Set(so, "shout", shout);
        so.ApplyModifiedPropertiesWithoutUndo();
        bool existed = File.Exists(path);
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, path, out bool success);
        UnityEngine.Object.DestroyImmediate(go);
        if (!success || saved == null) throw new Exception("could not save " + path);
        if (existed) s_Summary.warnings.Add("replaced " + path); else s_Summary.created.Add(path);
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
