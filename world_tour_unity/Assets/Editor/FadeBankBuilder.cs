using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WorldTour.World;

/// <summary>
/// Phase 8.4 stability: makes the transparent twins the follow camera fades props with (see FadeBank and OccluderFade in Occluder.cs) and sets the ground textures to
/// trilinear filtering. Run from the menu "World Tour" (or -executeMethod FadeBankBuilder.Build); safe to run again. The twins are real assets and the bank sits in
/// Resources, so the web build keeps the shader variants of the twins (a material made at run time would have none).
/// </summary>
public static class FadeBankBuilder
{
    const string Scenes = "Assets/World/Scenes";
    const string TwinDir = "Assets/World/Materials/Fade";
    const string BankPath = "Assets/World/Gameplay/Resources/FadeBank.asset";
    const string LitName = "Universal Render Pipeline/Lit";

    [MenuItem("World Tour/Build fade bank and ground textures")]
    public static void Build()
    {
        FixTextures();
        var sources = new List<Material>();
        var seen = new HashSet<Material>();
        void Add(Material m)
        {
            if (m == null || m.shader == null || m.shader.name != LitName || !AssetDatabase.Contains(m)) return;
            if (seen.Add(m)) sources.Add(m);
        }
        foreach (string file in Directory.GetFiles(Scenes, "Place_*.unity"))
        {
            EditorSceneManager.OpenScene(file, OpenSceneMode.Single);
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Bounds b = r.bounds;
                if (!FollowCamera.IsCandidate(b)) continue;
                foreach (var m in r.sharedMaterials) Add(m);
            }
        }
        // what gets spawned at run time: crew, crowd figures, the markers
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/World/Prefabs" }))
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (go == null) continue;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true)) foreach (var m in r.sharedMaterials) Add(m);
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) foreach (var m in r.sharedMaterials) Add(m);
        }
        var gameplay = AssetDatabase.LoadAssetAtPath<WorldTour.World.Gameplay.WorldGameplayAssets>("Assets/World/Gameplay/Resources/WorldGameplayAssets.asset");
        if (gameplay != null && gameplay.figureMaterials != null) foreach (var m in gameplay.figureMaterials) Add(m);

        Directory.CreateDirectory(TwinDir);
        var twins = new List<Material>();
        foreach (Material src in sources)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(src));
            string path = TwinDir + "/" + Sanitize(src.name) + "_" + guid.Substring(0, 6) + "_fade.mat";
            var twin = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (twin == null) { twin = new Material(src); AssetDatabase.CreateAsset(twin, path); }
            else twin.CopyPropertiesFromMaterial(src);
            twin.name = Path.GetFileNameWithoutExtension(path);
            MakeTransparent(twin);
            EditorUtility.SetDirty(twin);
            twins.Add(twin);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(BankPath));
        var bank = AssetDatabase.LoadAssetAtPath<FadeBank>(BankPath);
        if (bank == null) { bank = ScriptableObject.CreateInstance<FadeBank>(); AssetDatabase.CreateAsset(bank, BankPath); }
        bank.sources = sources.ToArray();
        bank.twins = twins.ToArray();
        EditorUtility.SetDirty(bank);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("WT_FADEBANK twins=" + twins.Count);
    }

    static string Sanitize(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Replace(' ', '_');
    }

    /// <summary>The same settings the URP Lit inspector writes for Surface Type: Transparent, Blend: Alpha.</summary>
    static void MakeTransparent(Material m)
    {
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.DisableKeyword("_ALPHAMODULATE_ON");
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetShaderPassEnabled("DepthOnly", false);
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    /// <summary>Ground and wall textures that have mip maps: trilinear (a bilinear lookup snaps from one mip level to the next as the camera moves, which shimmers on bricks and tiles) and anisotropic.</summary>
    public static void FixTextures()
    {
        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/World/Textures" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null || !imp.mipmapEnabled) continue;
            int aniso = imp.wrapMode == TextureWrapMode.Repeat ? 8 : 4;
            if (imp.filterMode == FilterMode.Trilinear && imp.anisoLevel >= aniso) continue;
            imp.filterMode = FilterMode.Trilinear;
            imp.anisoLevel = Mathf.Max(imp.anisoLevel, aniso);
            imp.SaveAndReimport();
            changed++;
        }
        Debug.Log("WT_TEXTURES trilinear+aniso set on " + changed);
    }
}
