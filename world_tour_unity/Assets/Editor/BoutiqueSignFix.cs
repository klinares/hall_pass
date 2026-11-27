using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 10B perf round: the boutique's "BOUTIQUE" signs were 5 x 7 pixel-letter meshes (bq_text_boutique, bq_text_boutique_door): on the street the B read as an R and the Q as a G,
/// inside the bars clipped into the walls. They are now TextMeshPro 3D text in the bold Courier Prime SDF already in the project (crisp at every size, nothing to clip).
/// <see cref="SignText"/> is what InteriorBoutiqueBuilder calls; <see cref="Apply"/> swaps the old meshes for it in the two scenes without rebuilding them:
/// Unity -batchmode -executeMethod BoutiqueSignFix.Apply. Safe to run again.
/// </summary>
public static class BoutiqueSignFix
{
    const string FontPath = "Assets/Fonts/CourierPrime/CourierPrime-Bold SDF.asset";
    const string Coffee = "Assets/World/Scenes/Place_Coffee.unity";
    const string Interior = "Assets/World/Scenes/Interior_Boutique.unity";

    /// <summary>Makes a sign text named "Text_BOUTIQUE" under <paramref name="parent"/> (unscaled), centred on <paramref name="localPos"/>, facing -z (the camera side), fitted to <paramref name="size"/> metres.</summary>
    public static GameObject SignText(Transform parent, string text, Vector3 localPos, Vector2 size, Color ink)
    {
        var go = new GameObject("Text_" + text);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        var t = go.AddComponent<TextMeshPro>();
        t.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        t.text = text;
        t.color = ink;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.rectTransform.sizeDelta = size;
        t.enableAutoSizing = true;
        t.fontSizeMin = 1f; t.fontSizeMax = 40f;
        t.fontStyle = FontStyles.Bold;
        t.characterSpacing = 4f;
        t.raycastTarget = false;
        var mr = go.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go;
    }

    public static void Apply()
    {
        int code = 0;
        try
        {
            Swap(Coffee, "BoutiqueSign", new Vector3(0f, 0.1f, 0f), new Vector2(3.2f, 0.95f));
            Swap(Interior, "SignPanel", new Vector3(0f, 0f, 0f), new Vector2(3.9f, 0.7f));
            foreach (string n in new[] { "bq_text_boutique", "bq_text_boutique_door" })
                AssetDatabase.DeleteAsset("Assets/World/Prefabs/Meshes/" + n + ".asset");
            AssetDatabase.SaveAssets();
            Debug.Log("BoutiqueSignFix: done");
        }
        catch (System.Exception e) { Debug.LogError("BoutiqueSignFix: " + e); code = 1; }
        EditorApplication.Exit(code);
    }

    static void Swap(string scenePath, string panelName, Vector3 offset, Vector2 size)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        GameObject panel = null;
        foreach (var g in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (g.name == panelName) panel = g;
        if (panel == null) throw new System.Exception("no " + panelName + " in " + scenePath);
        // the old pixel mesh is a child of the panel; an earlier run's text is a sibling named the same
        for (int i = panel.transform.childCount - 1; i >= 0; i--) if (panel.transform.GetChild(i).name == "Text_BOUTIQUE") Object.DestroyImmediate(panel.transform.GetChild(i).gameObject);
        foreach (var t in panel.transform.parent.GetComponentsInChildren<Transform>(true)) if (t != null && t.name == "Text_BOUTIQUE" && t.parent == panel.transform.parent) { Object.DestroyImmediate(t.gameObject); break; }
        Transform p = panel.transform;
        Vector3 pos = p.localPosition + new Vector3(offset.x, offset.y, -p.localScale.z * 0.5f - 0.03f);
        SignText(p.parent, "BOUTIQUE", pos, size, new Color32(0x2b, 0x23, 0x50, 255));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
