// BuildWeb.BuildWorld: builds the 3D scene(s) into world/ at the repo root. Same allowlist, staging and publish
// code as BuildWeb.Build (this is a partial of that class), with these differences:
//   - the output folder is only ever exactly <repo>/world (or a folder outside the repo, same rules as Build);
//   - the scene list is every Place_*.unity that exists, in tour order (Place_Coffee first), not the 2D build list;
//   - for the length of the build the quality levels and the default pipeline point at UniversalRP_3D (3D
//     renderer, HDR, shadows), then everything is put back, so tour/ builds and renders exactly as before;
//   - weeks.json and math_config.json are copied FROM tour/StreamingAssets/ into world/StreamingAssets/ (overwriting
//     the world copy): tour/ stays the one master.
//
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -buildTarget WebGL -executeMethod BuildWeb.BuildWorld
//       [-buildOut <dir>] [-buildStaging <dir>] [-buildSummary <file>] [-buildForceAdopt]   (as BuildWeb.Build)
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static partial class BuildWeb
{
    const string QualityFile = "ProjectSettings/QualitySettings.asset";
    const string GlobalFile = "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset";
    // phase 8.2: every place scene that exists, in tour order (coffee, records, rec, rooftop, club, theater, festival, arena); WorldSpike stays out of the build
    static string[] DefaultWorldScenes
    {
        get
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var v in WorldTour.Core.Data.Venues.All)
            {
                string path = "Assets/World/Scenes/" + WorldTour.World.Gameplay.WorldPlace.SceneName(v.Id) + ".unity";
                if (File.Exists(path)) list.Add(path);
            }
            if (list.Count == 0) list.Add(PlaceCoffeeBuilder.ScenePath);
            return list.ToArray();
        }
    }
    // 8.1 builder B: -worldScenes "a.unity;b.unity" overrides the list (the gameplay was tested on WorldSpike before Place_Coffee existed)
    // 8.3: the WorldAudioBank scene (the cue clips, kept out of Resources) is always last in the world build and is never in the 2D build list
    static string[] WorldScenes
    {
        get
        {
            string a = Arg("-worldScenes");
            var list = new System.Collections.Generic.List<string>(string.IsNullOrEmpty(a) ? DefaultWorldScenes : a.Split(';'));
            if (File.Exists(WorldAudioBankBuilder.ScenePath) && !list.Contains(WorldAudioBankBuilder.ScenePath)) list.Add(WorldAudioBankBuilder.ScenePath);
            return list.ToArray();
        }
    }

    public static void BuildWorld()
    {
        var summary = new Summary { startedAt = Now() };
        bool ok = false;
        string defaultSummary = null, summaryPath = null;
        var savedRefs = new System.Collections.Generic.List<UnityEngine.Object>();
        SerializedObject qualitySO = null;
        byte[] qualityBytes = null, globalBytes = null;
        var savedAA = new System.Collections.Generic.List<int>();
        int savedGlobalAA = 0;
        RenderPipelineAsset savedDefault = null;
        bool swapped = false;
        try
        {
            string project = Directory.GetCurrentDirectory();
            defaultSummary = Path.Combine(project, "Temp", "BuildWorld-summary.json");
            summaryPath = defaultSummary;
            string outDir = Path.GetFullPath(Arg("-buildOut") ?? Path.Combine(project, "..", "world"));
            string staging = Path.GetFullPath(Arg("-buildStaging") ?? Path.Combine(project, "Temp", "BuildWorld-staging"));
            summary.outputDir = outDir;
            summary.stagingDir = staging;
            summaryPath = ResolveSummaryPath(project, outDir, staging, defaultSummary, summary);

            if (!CheckPaths(project, outDir, staging, summary, "world")) throw new Exception("path check failed");
            CleanStaleWorldBuild(outDir, summary);

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new Exception("could not switch the active build target to Web");
            UnityEditor.WebGL.UserBuildSettings.codeOptimization = UnityEditor.WebGL.WasmCodeOptimization.DiskSizeLTO;
            EditorUserBuildSettings.development = false;
            summary.scenes.AddRange(WorldScenes);
            var web = UnityEditor.Build.NamedBuildTarget.WebGL;
            void S(string n, object v) => summary.settings.Add(new Setting { name = n, value = v?.ToString() ?? "null" });
            S("compressionFormat", PlayerSettings.WebGL.compressionFormat);
            S("decompressionFallback", PlayerSettings.WebGL.decompressionFallback);
            S("graphicsAPIs", string.Join(",", PlayerSettings.GetGraphicsAPIs(BuildTarget.WebGL)));
            S("managedStrippingLevel", PlayerSettings.GetManagedStrippingLevel(web));
            S("stripEngineCode", PlayerSettings.stripEngineCode);

            var pipe = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(WorldSpikeBuilder.PipelinePath);
            if (pipe == null) throw new Exception("missing " + WorldSpikeBuilder.PipelinePath + " (run WorldSpikeBuilder.CreateAll)");

            // Point every quality level and the default at the 3D pipeline; restored in finally. Read and written
            // through the serialized QualitySettings so the restore is exact (customRenderPipeline per level).
            savedDefault = GraphicsSettings.defaultRenderPipeline;
            qualitySO = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = qualitySO.FindProperty("m_QualitySettings");
            for (int i = 0; i < levels.arraySize; i++)
                savedRefs.Add(levels.GetArrayElementAtIndex(i).FindPropertyRelative("customRenderPipeline").objectReferenceValue);
            // URP copies the pipeline asset's MSAA setting into QualitySettings.antiAliasing when the pipeline changes, so the 0 the
            // quality levels have flips to 2 and Unity saves it at exit. Remember every level's value and put it back (8.1 housekeeping).
            savedGlobalAA = QualitySettings.antiAliasing;
            for (int i = 0; i < levels.arraySize; i++)
                savedAA.Add(levels.GetArrayElementAtIndex(i).FindPropertyRelative("antiAliasing").intValue);
            Debug.Log("BuildWorld: saved quality pipelines: " + string.Join(", ", savedRefs.Select(r => r == null ? "null" : r.name)));
            qualityBytes = File.ReadAllBytes(QualityFile);
            globalBytes = File.ReadAllBytes(GlobalFile);
            swapped = true;
            GraphicsSettings.defaultRenderPipeline = pipe;
            for (int i = 0; i < levels.arraySize; i++)
                levels.GetArrayElementAtIndex(i).FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipe;
            qualitySO.ApplyModifiedPropertiesWithoutUndo();

            string player = PrepareStaging(project, staging);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = WorldScenes,
                locationPathName = player,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None,
            });
            foreach (var step in report.steps)
                foreach (var m in step.messages)
                    if (m.type == LogType.Error || m.type == LogType.Exception || m.type == LogType.Assert)
                        summary.errors.Add(m.content);
            foreach (var f in Directory.GetFiles(player, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
            {
                long len = new FileInfo(f).Length;
                summary.files.Add(new FileEntry { path = Rel(player, f), size = len });
                summary.totalBytes += len;
            }
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                summary.errors.Add("build result: " + report.summary.result);
            else if (!File.Exists(Path.Combine(player, "index.html")) || !Directory.Exists(Path.Combine(player, "Build")))
                summary.errors.Add("staging has no index.html or Build/");
            else if (summary.errors.Count > 0)
                summary.errors.Add("errors were logged during the build; world/ was not touched");
            else
            {
                // tour/ is the master for the two data files: overwrite both the staged and the published copy.
                string tourSA = Path.GetFullPath(Path.Combine(project, "..", "tour", "StreamingAssets"));
                foreach (var name in LiveDataFiles)
                {
                    string src = Path.Combine(tourSA, name);
                    if (!File.Exists(src)) { summary.errors.Add("missing master data file " + src); continue; }
                    foreach (var dest in new[] { Path.Combine(player, "StreamingAssets", name), Path.Combine(outDir, "StreamingAssets", name) })
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                        File.Copy(src, dest, true);
                    }
                    summary.streamingAssetsSynced.Add("tour/StreamingAssets/" + name + " -> world/StreamingAssets/" + name);
                }
                if (summary.errors.Count == 0) Publish(player, outDir, summary);
            }
            ok = summary.errors.Count == 0;
        }
        catch (Exception e)
        {
            summary.errors.Add(e.ToString());
            Debug.LogError("BuildWorld failed: " + e);
            ok = false;
        }
        finally
        {
            if (swapped)
            {
                try
                {
                    qualitySO.Update();
                    var levels = qualitySO.FindProperty("m_QualitySettings");
                    for (int i = 0; i < levels.arraySize && i < savedRefs.Count; i++)
                        levels.GetArrayElementAtIndex(i).FindPropertyRelative("customRenderPipeline").objectReferenceValue = savedRefs[i];
                    for (int i = 0; i < levels.arraySize && i < savedAA.Count; i++)
                        levels.GetArrayElementAtIndex(i).FindPropertyRelative("antiAliasing").intValue = savedAA[i];
                    qualitySO.ApplyModifiedPropertiesWithoutUndo();
                    GraphicsSettings.defaultRenderPipeline = savedDefault;
                    QualitySettings.antiAliasing = savedGlobalAA;
                    AssetDatabase.SaveAssets();
                    // Belt and braces: if the file on disk still differs from what it was, put the original bytes back.
                    if (qualityBytes != null && !File.ReadAllBytes(QualityFile).AsSpan().SequenceEqual(qualityBytes))
                    {
                        File.WriteAllBytes(QualityFile, qualityBytes);
                        summary.warnings.Add("QualitySettings.asset on disk differed after the restore; the original bytes were written back");
                    }
                    // Unity renumbers a managed-reference id in the URP global settings on load; harmless, put the file back.
                    if (globalBytes != null && !File.ReadAllBytes(GlobalFile).AsSpan().SequenceEqual(globalBytes))
                        File.WriteAllBytes(GlobalFile, globalBytes);
                }
                catch (Exception e)
                {
                    summary.errors.Add("could not restore the render pipeline settings: " + e.Message);
                    ok = false;
                }
            }
        }

        if (summary.errors.Count > 0) ok = false;
        summary.result = ok ? "Succeeded" : "Failed";
        summary.errorCount = summary.errors.Count;
        summary.endedAt = Now();
        if (!WriteSummary(summaryPath, summary) &&
            !(defaultSummary != null && summaryPath != defaultSummary && WriteSummaryFallback(summaryPath, defaultSummary, summary)))
            ok = false;
        EditorApplication.Exit(ok ? 0 : 1);
    }

    static readonly System.Text.RegularExpressions.Regex HashedBuildFile = new System.Text.RegularExpressions.Regex(
        @"^[0-9a-f]{32}\.(loader\.js|framework\.js|data|wasm)(\.unityweb|\.br|\.gz)?$");

    /// <summary>
    /// A build that failed half way through publishing can leave hash-named Build/ files that no manifest lists. Before a new
    /// build, delete only files that are provably this builder's: the folder's build.json is written by BuildWeb, the file is
    /// in Build/, its name is the 32-hex-digit name a Unity Web build gives its loader, framework, data and wasm, it is in
    /// neither the current nor the previous set, and the live index.html does not mention it. Nothing else is ever touched.
    /// </summary>
    static void CleanStaleWorldBuild(string outDir, Summary summary)
    {
        string manifestPath = Path.Combine(outDir, "build.json"), buildDir = Path.Combine(outDir, "Build");
        if (!File.Exists(manifestPath) || !Directory.Exists(buildDir)) return;
        Manifest m;
        try { m = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath)); } catch { return; }
        if (m == null || m.writer != WriterId || m.current == null) return;
        var known = new System.Collections.Generic.HashSet<string>(BuildFiles(m.current));
        if (m.previous != null) known.UnionWith(BuildFiles(m.previous));
        string indexPath = Path.Combine(outDir, "index.html");
        string index = File.Exists(indexPath) ? File.ReadAllText(indexPath) : "";
        foreach (var f in Directory.GetFiles(buildDir))
        {
            string name = Path.GetFileName(f), rel = Rel(outDir, f);
            if (known.Contains(rel) || !HashedBuildFile.IsMatch(name) || index.Contains(name)) continue;
            File.Delete(f);
            summary.pruned.Add(rel);
            Debug.Log("BuildWorld: removed stale " + rel + " (listed in no build.json set, not used by index.html)");
        }
    }
}
