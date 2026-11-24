// Phase 8.1: import settings for the CC0 art under Assets/World/Art (see world_tour_unity/ASSETS.md).
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -executeMethod WorldArtImport.Apply
// Characters and the KayKit animation rigs are Humanoid (so the clips retarget); the dog is Generic with its own two clips;
// everything else is a static mesh. FBX materials are not imported: the builders assign their own (WorldArtBuilder).
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class WorldArtImport
{
    public const string Art = "Assets/World/Art";

    // Only the clips the controllers use are kept (a General.fbx clip also made the import log non-finite curve assertions).
    static readonly string[] UsedClips = { "Idle_A", "Idle_B", "Interact", "Walking_A", "Running_A", "Cheering", "DogArmature|Idle", "DogArmature|Walking" };
    static bool AllClips { get { return System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-artAll") >= 0; } }
    static string[] ForceLoops
    {
        get
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == "-artLoops") return a[i + 1] == "none" ? new string[0] : a[i + 1].Split(',');
            return null;
        }
    }
    static readonly string[] LoopClips = { "Idle_A", "Idle_B", "Walking_A", "Running_A", "DogArmature|Idle", "DogArmature|Walking" };

    public static void Apply()
    {
        ApplySettings();
        AssetDatabase.SaveAssets();
        EditorApplication.Exit(0);
    }

    /// <summary>Deletes imported art files the Place_Coffee scene and the prefabs do not use (the brief: import only what is used).</summary>
    public static void PruneUnused()
    {
        var roots = new System.Collections.Generic.List<string> { "Assets/World/Scenes/Place_Coffee.unity" };
        foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/World/Prefabs" })) roots.Add(AssetDatabase.GUIDToAssetPath(g));
        var used = new System.Collections.Generic.HashSet<string>(AssetDatabase.GetDependencies(roots.ToArray(), true));
        int n = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Model t:Texture2D", new[] { Art }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (!used.Contains(p)) { Debug.Log("WT_PRUNE " + p); AssetDatabase.DeleteAsset(p); n++; }
        }
        Debug.Log("WT_PRUNE count=" + n);
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    public static void ApplySettings()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Art }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            bool changed = false;
            bool character = path.Contains("KayKit_Adventurers/");
            bool anim = path.Contains("KayKit_Animations/");
            bool dog = path.Contains("Quaternius_Dog/");
            if (character || anim)
            {
                if (imp.animationType != ModelImporterAnimationType.Human) { imp.animationType = ModelImporterAnimationType.Human; changed = true; }
                if (imp.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel) { imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; changed = true; }
            }
            else if (dog)
            {
                if (imp.animationType != ModelImporterAnimationType.Generic) { imp.animationType = ModelImporterAnimationType.Generic; changed = true; }
            }
            else if (imp.animationType != ModelImporterAnimationType.None) { imp.animationType = ModelImporterAnimationType.None; changed = true; }
            bool wantAnim = anim || dog;
            if (imp.importAnimation != wantAnim) { imp.importAnimation = wantAnim; changed = true; }
            if (wantAnim && imp.resampleCurves) { imp.resampleCurves = false; changed = true; }
            if (wantAnim)
            {
                var cur = imp.clipAnimations;
                if (ForceLoops != null || cur == null || cur.Length == 0 || cur[0].lockRootRotation || System.Array.Exists(cur, c => System.Array.IndexOf(UsedClips, c.name) < 0))
                {
                    var clips = System.Array.FindAll(imp.defaultClipAnimations, c => AllClips || System.Array.IndexOf(UsedClips, c.name) >= 0);
                    foreach (var c in clips)
                    {
                        c.loopTime = System.Array.IndexOf(ForceLoops ?? LoopClips, c.name) >= 0;
                        c.lockRootRotation = false; c.lockRootHeightY = false; c.lockRootPositionXZ = false;   // Animator root motion is off; the NaN assertions came with the lock flags
                    }
                    imp.clipAnimations = clips;
                    changed = true;
                }
            }
            if (imp.materialImportMode != ModelImporterMaterialImportMode.None) { imp.materialImportMode = ModelImporterMaterialImportMode.None; changed = true; }
            if (imp.isReadable) { imp.isReadable = false; changed = true; }
            if (imp.importCameras) { imp.importCameras = false; changed = true; }
            if (imp.importLights) { imp.importLights = false; changed = true; }
            if (changed) imp.SaveAndReimport();
        }
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Art }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var t = (TextureImporter)AssetImporter.GetAtPath(path);
            int size = path.Contains("KayKit_Adventurers/") ? 256 : 512;
            bool changed = false;
            if (t.filterMode != FilterMode.Point) { t.filterMode = FilterMode.Point; changed = true; }
            if (t.maxTextureSize != size) { t.maxTextureSize = size; changed = true; }
            if (t.mipmapEnabled) { t.mipmapEnabled = false; changed = true; }
            if (changed) t.SaveAndReimport();
        }
    }
}

public static class WorldArtBake
{
    /// <summary>
    /// Copies the six KayKit clips we use into plain .anim assets and deletes the three animation FBX files. Importing
    /// Rig_Medium_General.fbx as Humanoid logs 56 "IsFinite(curve...)" assertions every time it is imported, and the build counts
    /// them as errors; the baked clips are the same humanoid motion without the FBX.
    /// </summary>
    public static void Run()
    {
        System.IO.Directory.CreateDirectory("Assets/World/Animation/Clips");
        string[] names = { "Idle_A", "Idle_B", "Interact", "Walking_A", "Running_A", "Cheering" };
        string[] loops = { "Idle_A", "Idle_B", "Walking_A", "Running_A" };
        foreach (var n in names)
        {
            AnimationClip src = null;
            foreach (var f in new[] { "General", "MovementBasic", "Simulation" })
            {
                src = AssetDatabase.LoadAllAssetsAtPath(WorldArtImport.Art + "/KayKit_Animations/Rig_Medium_" + f + ".fbx").OfType<AnimationClip>().FirstOrDefault(c => c.name == n);
                if (src != null) break;
            }
            if (src == null) { Debug.LogError("clip not found " + n); continue; }
            var copy = Object.Instantiate(src);
            copy.name = n;
            var set = AnimationUtility.GetAnimationClipSettings(copy);
            set.loopTime = System.Array.IndexOf(loops, n) >= 0;
            AnimationUtility.SetAnimationClipSettings(copy, set);
            string path = "Assets/World/Animation/Clips/" + n + ".anim";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(copy, path);
            Debug.Log("WT_BAKE " + n + " humanMotion=" + copy.humanMotion + " length=" + copy.length + " loop=" + set.loopTime);
        }
        AssetDatabase.SaveAssets();
        foreach (var f in new[] { "General", "MovementBasic", "Simulation" })
            AssetDatabase.DeleteAsset(WorldArtImport.Art + "/KayKit_Animations/Rig_Medium_" + f + ".fbx");
        AssetDatabase.SaveAssets();
        EditorApplication.Exit(0);
    }
}
