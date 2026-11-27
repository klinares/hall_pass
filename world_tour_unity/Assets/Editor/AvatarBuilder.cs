// Phase 10B.1: builds the wardrobe editor's screen prefab (Assets/Game/Resources/Screens/Screen_Avatar.prefab: map art, button prefab, shout material wired, as MetaBuilder does
// for the backstage) and runs the wardrobe builder (palette copy and Star anchors).
//
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -executeMethod AvatarBuilder.Run
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using WorldTour.Game.Map;
using WorldTour.Game.Meta;
using WorldTour.Game.Ui;

public static class AvatarBuilder
{
    const string ArtPath = "Assets/Game/Map/MapArt.asset";
    const string ButtonPrefabPath = "Assets/Game/Ui/Prefabs/StickerButton.prefab";
    const string ShoutPath = "Assets/Art/Ticket/CaveatBrush SDF - Shout.mat";
    const string ScreensDir = "Assets/Game/Resources/Screens";

    [MenuItem("World Tour/Build avatar editor")]
    public static void Run()
    {
        bool ok = true;
        try
        {
            var art = AssetDatabase.LoadAssetAtPath<MapArt>(ArtPath);
            var button = AssetDatabase.LoadAssetAtPath<StickerButton>(ButtonPrefabPath);
            var shout = AssetDatabase.LoadAssetAtPath<Material>(ShoutPath);
            if (art == null || button == null) throw new Exception("map art or button prefab missing");
            Directory.CreateDirectory(ScreensDir);
            string path = ScreensDir + "/Screen_Avatar.prefab";
            var go = new GameObject("Screen_Avatar", typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            go.AddComponent<CanvasGroup>();
            var screen = go.AddComponent<AvatarEditorScreen>();
            var so = new SerializedObject(screen);
            so.FindProperty("art").objectReferenceValue = art;
            so.FindProperty("buttonPrefab").objectReferenceValue = button;
            so.FindProperty("shout").objectReferenceValue = shout;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(go, path, out bool success);
            UnityEngine.Object.DestroyImmediate(go);
            if (!success) throw new Exception("could not save " + path);
            Debug.Log("AvatarBuilder: saved " + path);
        }
        catch (Exception e) { ok = false; Debug.LogError("AvatarBuilder failed: " + e); }
        AssetDatabase.SaveAssets();
        // the palette copy and the Star's anchors (this ends the Editor itself in batch mode)
        WardrobeBuilder.Run();
        if (!ok && Application.isBatchMode) EditorApplication.Exit(1);
    }
}
