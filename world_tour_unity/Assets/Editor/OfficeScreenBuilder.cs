// Makes the manager's office screen prefab by code (task 5.2), so nothing is edited by hand:
//   Assets/Game/Resources/Screens/Screen_Office.prefab   OfficeScreen + CanvasGroup, full screen. OfficeScreen builds its
//                                                        page in code when pushed (the save decides what it shows), so
//                                                        the prefab carries no art references. OfficeScreen.Open loads it
//                                                        from Resources.
//   Unity -batchmode -projectPath ... -executeMethod OfficeScreenBuilder.Build
// Logs "WT_OFFICE_PREFAB ok ..." or "WT_OFFICE_PREFAB failed ...", and exits through EditorApplication.Exit (D1) in batch mode.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using WorldTour.Game.Office;

public static class OfficeScreenBuilder
{
    const string PrefabDir = "Assets/Game/Resources/Screens";
    const string PrefabPath = PrefabDir + "/Screen_Office.prefab";

    [MenuItem("World Tour/UI/Build manager's office screen (5.2)")]
    static void BuildMenu() { Make(); }

    public static void Build()
    {
        int code = 0;
        try { Make(); }
        catch (Exception e) { Debug.LogError("WT_OFFICE_PREFAB failed " + e.Message); code = 1; }
        if (Application.isBatchMode) EditorApplication.Exit(code);
    }

    static void Make()
    {
        Directory.CreateDirectory(PrefabDir);
        var root = new GameObject("Screen_Office", typeof(RectTransform), typeof(CanvasGroup));
        root.layer = 5;
        var rt = (RectTransform)root.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        root.AddComponent<OfficeScreen>();
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
        UnityEngine.Object.DestroyImmediate(root);
        if (!ok || saved == null) throw new Exception("could not save " + PrefabPath);
        AssetDatabase.SaveAssets();
        Debug.Log("WT_OFFICE_PREFAB ok path=" + PrefabPath);
    }
}
