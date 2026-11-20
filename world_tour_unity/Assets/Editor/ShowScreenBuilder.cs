// Makes the show screen prefab by code (task 2.5), so nothing is edited by hand:
//   Assets/Game/Resources/Screens/Screen_Show.prefab  (ShowScreen + CanvasGroup, full-screen; the buttons use 2.2's
//   StickerButton prefab). ShowScreen.Open loads it from Resources, so the map and the ticket can open a show without
//   a scene reference. Run after UiBuilder.BuildAll:
//   Unity -batchmode -projectPath ... -executeMethod ShowScreenBuilder.Build
// Logs "WT_SHOW_PREFAB ok path=..." or "WT_SHOW_PREFAB failed ...", and exits through EditorApplication.Exit (D1) in batch mode.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using WorldTour.Game.Show;
using WorldTour.Game.Ui;

public static class ShowScreenBuilder
{
    const string Dir = "Assets/Game/Resources/Screens";
    const string PrefabPath = Dir + "/Screen_Show.prefab";
    const string ButtonPrefabPath = "Assets/Game/Ui/Prefabs/StickerButton.prefab";

    [MenuItem("World Tour/UI/Build show screen (2.5)")]
    static void BuildMenu() { Make(); }

    public static void Build()
    {
        int code = 0;
        try { Make(); }
        catch (Exception e) { Debug.LogError("WT_SHOW_PREFAB failed " + e.Message); code = 1; }
        if (Application.isBatchMode) EditorApplication.Exit(code);
    }

    static void Make()
    {
        var button = AssetDatabase.LoadAssetAtPath<StickerButton>(ButtonPrefabPath);
        if (button == null) Debug.LogWarning("[show] " + ButtonPrefabPath + " is missing (run UiBuilder.BuildAll first); the show falls back to plain buttons");
        Directory.CreateDirectory(Dir);
        var root = new GameObject("Screen_Show", typeof(RectTransform), typeof(CanvasGroup));
        root.layer = 5;
        var rt = (RectTransform)root.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var screen = root.AddComponent<ShowScreen>();
        var so = new SerializedObject(screen);
        so.FindProperty("buttonPrefab").objectReferenceValue = button;
        var bolt = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/stickers/bolt_sm.png");
        if (bolt == null) Debug.LogWarning("[show] stickers/bolt_sm.png is missing; the streak chip has no icon");
        so.FindProperty("streakIcon").objectReferenceValue = bolt;
        var vinyl = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/stickers/vinyl.png");
        if (vinyl == null) Debug.LogWarning("[show] stickers/vinyl.png is missing; studio session's record is a plain disc");
        so.FindProperty("recordSticker").objectReferenceValue = vinyl;
        so.ApplyModifiedPropertiesWithoutUndo();
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
        UnityEngine.Object.DestroyImmediate(root);
        if (!ok || saved == null) throw new Exception("could not save " + PrefabPath);
        AssetDatabase.SaveAssets();
        Debug.Log("WT_SHOW_PREFAB ok path=" + PrefabPath + " button=" + (button != null ? "sticker" : "plain") + " streakIcon=" + (bolt != null ? "bolt_sm" : "none") + " record=" + (vinyl != null ? "vinyl" : "none"));
    }
}
