// Builds the Web player into tour/ at the repo root, the same way on any machine.
//
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -buildTarget WebGL -executeMethod BuildWeb.Build
//       [-buildOut <dir>]        default ../tour (relative to the project)
//       [-buildStaging <dir>]    default Temp/BuildWeb-staging
//       [-buildSummary <file>]   default Temp/BuildWeb-summary.json
//       [-buildTag <text>]       test only: bakes a throwaway Resources text into the build so the data hash changes
//       [-buildForceAdopt]       publish into an existing output folder that has no build.json written by BuildWeb
//
// Allowlist: BuildWeb only writes into folders it can prove it owns. Anything else is refused with a Failed
// summary before a single file is created, written or deleted.
//   Output   inside the repo, only exactly <repo>/tour. Anywhere allowed, the folder must be missing, empty, or
//            hold a build.json with "writer": "WorldTour.BuildWeb". -buildForceAdopt is the only override, for an
//            existing folder without that file. A legacy tour/build.json with no writer is accepted once.
//   Staging  deleted and rebuilt on every run, so: strictly inside <project>/Temp, or outside the repo and missing,
//            empty, or holding BuildWeb's .buildweb-staging marker. Never the output, project or repo, nor inside
//            the output. The player is built into <staging>/player.
//   Summary  outside the repo or inside <project>/Temp, never inside the output or staging folder. Otherwise the
//            summary goes to the default path and says why.
// Publish order: Build/ files, index.html, new StreamingAssets files, build.json, then prune.
//
// Live data: the deployed tour/StreamingAssets/weeks.json and math_config.json are the single source of truth
// (ADVISOR 2026-11-20 00:12). Before building, each of those two files is copied back into Assets/StreamingAssets/
// when the deployed copy exists and differs (or the project copy is missing), so the build packages the live copy.
// No other file is synced. Deployed StreamingAssets files are never overwritten or deleted; a missing one is
// created from the project copy, and any other file that differs is kept and reported as a warning.
//
// The run always ends in EditorApplication.Exit(0 or 1). The Editor can crash at shutdown on some machines
// (DECISIONS, D1), so the summary file is the evidence of a good build, not the exit code.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static partial class BuildWeb
{
    const string TagFolder = "Assets/_BuildTag";

    [Serializable] public class FileEntry { public string path; public long size; }
    [Serializable] public class Setting { public string name; public string value; }

    [Serializable]
    public class BuildSet
    {
        public string builtAt;
        public List<FileEntry> files = new List<FileEntry>();
    }

    /// <summary>tour/build.json: which hashed Build/ files belong to the current and the previous build.</summary>
    [Serializable]
    public class Manifest
    {
        // No initializer on purpose: JsonUtility keeps initializer values for keys missing from the JSON,
        // which would make a build.json without this field look like ours.
        public string writer;
        public string note = "written by BuildWeb.cs; Build/ keeps the current and the previous build only";
        public BuildSet current;
        public BuildSet previous;
    }

    [Serializable]
    public class Summary
    {
        public string result = "Failed";
        public string startedAt;
        public string endedAt;
        public string outputDir;
        public string stagingDir;
        public List<string> scenes = new List<string>();
        public List<Setting> settings = new List<Setting>();
        public int errorCount;
        public List<string> errors = new List<string>();
        public long totalBytes;
        public List<FileEntry> files = new List<FileEntry>();
        public List<string> published = new List<string>();
        public List<string> pruned = new List<string>();
        public List<string> streamingAssetsCopied = new List<string>();
        public List<string> streamingAssetsKept = new List<string>();
        public List<string> streamingAssetsSynced = new List<string>();
        public List<string> dataProof = new List<string>();
        public List<string> warnings = new List<string>();
    }

    static string Arg(string name)
    {
        var a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return null;
    }

    static string Now() => DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");

    public static void Build()
    {
        var summary = new Summary { startedAt = Now() };
        bool ok = false;
        string defaultSummary = null;
        string summaryPath = null;
        try
        {
            string project = Directory.GetCurrentDirectory();
            defaultSummary = Path.Combine(project, "Temp", "BuildWeb-summary.json");
            summaryPath = defaultSummary;
            string outDir = Path.GetFullPath(Arg("-buildOut") ?? Path.Combine(project, "..", "tour"));
            string staging = Path.GetFullPath(Arg("-buildStaging") ?? Path.Combine(project, "Temp", "BuildWeb-staging"));
            summary.outputDir = outDir;
            summary.stagingDir = staging;
            summaryPath = ResolveSummaryPath(project, outDir, staging, defaultSummary, summary);
            ok = Run(project, outDir, staging, summary);
        }
        catch (Exception e)
        {
            summary.errors.Add(e.ToString());
            Debug.LogError("BuildWeb failed: " + e);
            ok = false;
        }
        finally
        {
            try { RemoveTag(); }
            catch (Exception e)
            {
                string msg = "could not remove the test-only " + TagFolder + " folder: " + e.Message;
                summary.warnings.Add(msg);
                Debug.LogWarning("BuildWeb: " + msg);
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

    static string ResolveSummaryPath(string project, string outDir, string staging, string defaultSummary, Summary summary)
    {
        string arg = Arg("-buildSummary");
        if (arg == null) return defaultSummary;
        try
        {
            string full = Path.GetFullPath(arg);
            if (string.IsNullOrEmpty(Path.GetFileName(full))) throw new ArgumentException("it names a folder, not a file");
            // Allowed outside the repo or inside <project>/Temp; never where it would be published or deleted.
            string repo = Path.GetFullPath(Path.Combine(project, ".."));
            if (Inside(full, outDir) || Same(full, outDir)) throw new ArgumentException("it is inside the output folder, which gets published");
            if (Inside(full, staging) || Same(full, staging)) throw new ArgumentException("it is inside the staging folder, which gets deleted");
            if (Inside(full, repo) && !Inside(full, Path.Combine(project, "Temp")))
                throw new ArgumentException("it is inside the repo but not inside " + Path.Combine(project, "Temp"));
            return full;
        }
        catch (Exception e)
        {
            summary.warnings.Add($"-buildSummary \"{arg}\" is unusable ({e.Message}); summary written to {defaultSummary}");
            return defaultSummary;
        }
    }

    static bool WriteSummary(string path, Summary summary)
    {
        if (path == null) return false;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(summary, true));
            Debug.Log($"BuildWeb summary: {summary.result}, {summary.totalBytes} bytes, {summary.errorCount} errors -> {path}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"BuildWeb could not write the summary to {path}: {e.Message}");
            return false;
        }
    }

    static bool WriteSummaryFallback(string badPath, string defaultSummary, Summary summary)
    {
        summary.warnings.Add($"could not write the summary to {badPath}; summary written to {defaultSummary}");
        return WriteSummary(defaultSummary, summary);
    }

    static bool Run(string project, string outDir, string staging, Summary summary)
    {
        // 0. Path guards, before anything is created, built or deleted.
        if (!CheckPaths(project, outDir, staging, summary)) return false;

        // 1. Settings that live in Library (not committed): set them on every run.
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL &&
            !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            throw new Exception("could not switch the active build target to Web");
        UnityEditor.WebGL.UserBuildSettings.codeOptimization = UnityEditor.WebGL.WasmCodeOptimization.DiskSizeLTO;
        EditorUserBuildSettings.development = false;

        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0) throw new Exception("no enabled scenes in the build list");
        summary.scenes.AddRange(scenes);

        // Settings read from ProjectSettings (committed): logged, never overridden.
        var web = NamedBuildTarget.WebGL;
        void S(string n, object v) => summary.settings.Add(new Setting { name = n, value = v?.ToString() ?? "null" });
        S("activeBuildTarget", EditorUserBuildSettings.activeBuildTarget);
        S("codeOptimization", UnityEditor.WebGL.UserBuildSettings.codeOptimization);
        S("development", EditorUserBuildSettings.development);
        S("compressionFormat", PlayerSettings.WebGL.compressionFormat);
        S("decompressionFallback", PlayerSettings.WebGL.decompressionFallback);
        S("nameFilesAsHashes", PlayerSettings.WebGL.nameFilesAsHashes);
        S("dataCaching", PlayerSettings.WebGL.dataCaching);
        S("exceptionSupport", PlayerSettings.WebGL.exceptionSupport);
        S("template", PlayerSettings.WebGL.template);
        S("managedStrippingLevel", PlayerSettings.GetManagedStrippingLevel(web));
        S("il2cppCodeGeneration", PlayerSettings.GetIl2CppCodeGeneration(web));
        S("stripEngineCode", PlayerSettings.stripEngineCode);
        S("graphicsAPIs", string.Join(",", PlayerSettings.GetGraphicsAPIs(BuildTarget.WebGL)));
        S("defaultWebScreen", $"{PlayerSettings.defaultWebScreenWidth}x{PlayerSettings.defaultWebScreenHeight}");
        S("productName", PlayerSettings.productName);
        S("splashScreen", PlayerSettings.SplashScreen.show);
        Debug.Log("BuildWeb settings: " + string.Join(", ", summary.settings.Select(s => s.name + "=" + s.value)));

        SyncLiveData(project, outDir, summary);

        string tag = Arg("-buildTag");
        if (!string.IsNullOrEmpty(tag)) WriteTag(tag, summary);

        // 2. Build into a clean staging folder, never straight into tour/.
        // The player goes into <staging>/player; the marker file at <staging> proves BuildWeb made the folder.
        string player = PrepareStaging(project, staging);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
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

        if (report.summary.result != BuildResult.Succeeded)
        {
            summary.errors.Add("build result: " + report.summary.result);
            return false;
        }
        if (!File.Exists(Path.Combine(player, "index.html")) || !Directory.Exists(Path.Combine(player, "Build")))
        {
            summary.errors.Add("staging has no index.html or Build/");
            return false;
        }
        // Any error logged during the build means no publish, even if BuildPlayer reported success.
        if (summary.errors.Count > 0)
        {
            summary.errors.Add("errors were logged during the build; tour/ was not touched");
            return false;
        }

        // 3. Publish.
        Publish(player, outDir, summary);
        return summary.errors.Count == 0;
    }

    const string WriterId = "WorldTour.BuildWeb";
    const string StagingMarker = ".buildweb-staging";

    /// <summary>
    /// Allowlist: BuildWeb only writes into folders it can prove it owns. Runs before anything is created, written
    /// or deleted, and every refusal leaves the disk untouched.
    /// Output: inside the repo only exactly &lt;repo&gt;/tour; anywhere allowed, the folder must be missing, empty, or
    /// hold a build.json with writer "WorldTour.BuildWeb" (-buildForceAdopt overrides the last part only).
    /// A legacy tour/build.json with a current set but no writer field is accepted once and rewritten.
    /// Staging (deleted and recreated on every run): strictly inside &lt;project&gt;/Temp, or outside the repo and
    /// missing, empty, or holding the .buildweb-staging marker. It must not overlap the output, project or repo.
    /// </summary>
    static bool CheckPaths(string project, string outDir, string staging, Summary summary, string allowedName = "tour")
    {
        string repo = Path.GetFullPath(Path.Combine(project, ".."));
        string temp = Path.Combine(project, "Temp");
        string tour = Path.Combine(repo, allowedName);
        var problems = new List<string>();

        // ---- output folder ----
        foreach (var (name, p) in new[] { ("project folder", project), ("repo root", repo) })
            if (Same(outDir, p) || Inside(p, outDir))
                problems.Add($"-buildOut {outDir} is or contains the {name} {p}");
        bool outInRepo = Same(outDir, repo) || Inside(outDir, repo);
        if (outInRepo && !Same(outDir, tour))
            problems.Add($"-buildOut {outDir} is inside the repo; the only output allowed there is {tour}");
        else if (File.Exists(outDir))
            problems.Add($"-buildOut {outDir} is a file, not a folder");
        else if (Directory.Exists(outDir) && Directory.EnumerateFileSystemEntries(outDir).Any())
        {
            string manifest = Path.Combine(outDir, "build.json");
            Manifest m = null;
            if (File.Exists(manifest))
                try { m = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifest)); } catch { m = null; }
            bool hasCurrent = m != null && m.current != null && m.current.files != null && m.current.files.Count > 0;
            bool force = Environment.GetCommandLineArgs().Contains("-buildForceAdopt");
            if (m != null && m.writer == WriterId && hasCurrent) { /* ours */ }
            else if (Same(outDir, tour) && m != null && string.IsNullOrEmpty(m.writer) && hasCurrent)
                summary.warnings.Add($"{manifest} had no writer field; accepted as ours this one time and rewritten with writer \"{WriterId}\"");
            else if (force)
                summary.warnings.Add($"-buildForceAdopt: adopting {outDir}, which has no build.json written by BuildWeb");
            else
                problems.Add($"-buildOut {outDir} already holds files and no build.json written by BuildWeb, so it isn't ours; pass -buildForceAdopt to adopt it");
        }

        // ---- staging folder ----
        foreach (var (name, p) in new[] { ("output folder", outDir), ("project folder", project), ("repo root", repo) })
            if (Same(staging, p) || Inside(p, staging))
                problems.Add($"-buildStaging {staging} is or contains the {name} {p}");
        if (Inside(staging, outDir))
            problems.Add($"-buildStaging {staging} is inside the output folder {outDir}");
        bool stagingInRepo = Same(staging, repo) || Inside(staging, repo);
        if (stagingInRepo && !Inside(staging, temp))
            problems.Add($"-buildStaging {staging} is inside the repo but not inside {temp}");
        else if (!stagingInRepo && !StagingIsOurs(staging))
            problems.Add($"-buildStaging {staging} already holds files and no {StagingMarker} marker, so BuildWeb didn't make it and won't delete it");

        foreach (var p in problems.Distinct())
        {
            summary.errors.Add("refused: " + p);
            Debug.LogError("BuildWeb refused: " + p);
        }
        return problems.Count == 0;
    }

    /// <summary>Missing, an empty folder, or a folder holding BuildWeb's marker file.</summary>
    static bool StagingIsOurs(string staging)
    {
        if (File.Exists(staging)) return false;
        if (!Directory.Exists(staging)) return true;
        return File.Exists(Path.Combine(staging, StagingMarker)) || !Directory.EnumerateFileSystemEntries(staging).Any();
    }

    /// <summary>Cleans the staging folder (only one BuildWeb can prove it owns), writes the marker, returns the player folder.</summary>
    static string PrepareStaging(string project, string staging)
    {
        bool inTemp = Inside(staging, Path.Combine(project, "Temp"));
        if (!inTemp && !StagingIsOurs(staging))
            throw new Exception($"refusing to delete {staging}: no {StagingMarker} marker");
        if (Directory.Exists(staging)) Directory.Delete(staging, true);
        Directory.CreateDirectory(staging);
        File.WriteAllText(Path.Combine(staging, StagingMarker), "Made by BuildWeb.cs. This folder is deleted and rebuilt on every web build.\n");
        string player = Path.Combine(staging, "player");
        Directory.CreateDirectory(player);
        return player;
    }

    /// <summary>The deployed copies of these files are the source of truth; the project copies mirror them.</summary>
    static readonly string[] LiveDataFiles = { "weeks.json", "math_config.json" };

    static void SyncLiveData(string project, string outDir, Summary summary)
    {
        bool changed = false;
        foreach (var name in LiveDataFiles)
        {
            string deployed = Path.Combine(outDir, "StreamingAssets", name);
            string local = Path.Combine(project, "Assets", "StreamingAssets", name);
            if (!File.Exists(deployed)) continue;
            if (File.Exists(local) && SameBytes(deployed, local)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(local));
            File.Copy(deployed, local, true);
            changed = true;
            summary.streamingAssetsSynced.Add("StreamingAssets/" + name);
            Debug.Log($"BuildWeb: synced tour/StreamingAssets/{name} -> Assets/StreamingAssets/{name}");
        }
        if (changed) AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    static string Norm(string p) => Path.GetFullPath(p).TrimEnd('\\', '/');
    static bool Same(string a, string b) => string.Equals(Norm(a), Norm(b), StringComparison.OrdinalIgnoreCase);
    /// <summary>True when <paramref name="child"/> is strictly inside <paramref name="parent"/>.</summary>
    static bool Inside(string child, string parent)
    {
        string c = Norm(child), p = Norm(parent) + Path.DirectorySeparatorChar;
        return c.StartsWith(p, StringComparison.OrdinalIgnoreCase);
    }

    static void Publish(string staging, string outDir, Summary summary)
    {
        Directory.CreateDirectory(outDir);
        string outBuild = Path.Combine(outDir, "Build");
        Directory.CreateDirectory(outBuild);
        string manifestPath = Path.Combine(outDir, "build.json");

        Manifest old = null;
        if (File.Exists(manifestPath))
        {
            try { old = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath)); }
            catch (Exception e) { summary.warnings.Add("build.json unreadable, treating Build/ as unknown: " + e.Message); }
        }

        var current = new BuildSet { builtAt = Now() };
        foreach (var f in Directory.GetFiles(staging, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
        {
            string rel = Rel(staging, f);
            if (rel.StartsWith("StreamingAssets/", StringComparison.Ordinal)) continue;
            current.files.Add(new FileEntry { path = rel, size = new FileInfo(f).Length });
        }

        // The previous set is what was current before this build. If this build produced exactly the same
        // Build/ files, the old previous set stays previous. With no readable build.json, whatever is in
        // Build/ now counts as the previous build.
        BuildSet previous;
        var newBuildFiles = new HashSet<string>(BuildFiles(current));
        if (old != null && old.current != null)
        {
            bool same = new HashSet<string>(BuildFiles(old.current)).SetEquals(newBuildFiles);
            previous = same ? old.previous : old.current;
        }
        else
        {
            previous = new BuildSet { builtAt = "unknown" };
            foreach (var f in Directory.GetFiles(outBuild))
                previous.files.Add(new FileEntry { path = Rel(outDir, f), size = new FileInfo(f).Length });
        }
        if (previous != null)
        {
            // Only Build/ files are kept from an older build; top-level files are always replaced.
            previous.files = previous.files.Where(e => IsBuildFile(e.path) && File.Exists(Path.Combine(outDir, e.path))).ToList();
            if (previous.files.Count == 0) previous = null;
        }

        // Build/ first, so the new index.html never points at a file that isn't there yet.
        foreach (var e in current.files.Where(e => IsBuildFile(e.path)))
            CopyIfChanged(Path.Combine(staging, e.path), Path.Combine(outDir, e.path), e.path, summary);
        foreach (var e in current.files.Where(e => !IsBuildFile(e.path)))
            CopyIfChanged(Path.Combine(staging, e.path), Path.Combine(outDir, e.path), e.path, summary);

        // StreamingAssets: copy only files tour/ doesn't have yet; never overwrite or delete.
        string stagingSA = Path.Combine(staging, "StreamingAssets");
        if (Directory.Exists(stagingSA))
        {
            foreach (var f in Directory.GetFiles(stagingSA, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
            {
                string rel = Rel(staging, f);
                string dest = Path.Combine(outDir, rel);
                if (!File.Exists(dest))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    File.Copy(f, dest);
                    summary.streamingAssetsCopied.Add(rel);
                }
                else if (!SameBytes(f, dest))
                {
                    string msg = $"kept tour/{rel} (differs from project copy)";
                    summary.streamingAssetsKept.Add(rel);
                    summary.warnings.Add(msg);
                    Debug.LogWarning("BuildWeb: " + msg);
                }
            }
        }

        // Record the new sets before pruning: if anything below fails, build.json already describes
        // what index.html uses, so the next run can't prune the live build's files.
        File.WriteAllText(manifestPath, JsonUtility.ToJson(new Manifest { writer = WriterId, current = current, previous = previous }, true));
        summary.published.Add("build.json");

        // Prune Build/: keep only the current and the previous set.
        var keep = new HashSet<string>(newBuildFiles);
        if (previous != null) keep.UnionWith(BuildFiles(previous));
        foreach (var f in Directory.GetFiles(outBuild, "*", SearchOption.AllDirectories))
        {
            string rel = Rel(outDir, f);
            if (keep.Contains(rel)) continue;
            File.Delete(f);
            summary.pruned.Add(rel);
            Debug.Log("BuildWeb: pruned " + rel);
        }
    }

    static IEnumerable<string> BuildFiles(BuildSet set) =>
        set == null ? Enumerable.Empty<string>() : set.files.Select(e => e.path).Where(IsBuildFile);

    static bool IsBuildFile(string rel) => rel.StartsWith("Build/", StringComparison.Ordinal);

    static void CopyIfChanged(string src, string dest, string rel, Summary summary)
    {
        if (File.Exists(dest) && SameBytes(src, dest)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(dest));
        File.Copy(src, dest, true);
        summary.published.Add(rel);
    }

    static bool SameBytes(string a, string b)
    {
        var fa = new FileInfo(a); var fb = new FileInfo(b);
        if (fa.Length != fb.Length) return false;
        return File.ReadAllBytes(a).AsSpan().SequenceEqual(File.ReadAllBytes(b));
    }

    static string Rel(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

    // ---- test-only build tag ----

    static void WriteTag(string tag, Summary summary)
    {
        Directory.CreateDirectory(TagFolder + "/Resources");
        File.WriteAllText(TagFolder + "/Resources/buildtag.txt", tag);
        AssetDatabase.ImportAsset(TagFolder, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceSynchronousImport);
        summary.warnings.Add("test-only -buildTag \"" + tag + "\" baked into this build");
    }

    static void RemoveTag()
    {
        if (!AssetDatabase.IsValidFolder(TagFolder) && !Directory.Exists(TagFolder)) return;
        if (!AssetDatabase.DeleteAsset(TagFolder))
        {
            Directory.Delete(TagFolder, true);
            if (File.Exists(TagFolder + ".meta")) File.Delete(TagFolder + ".meta");
        }
    }
}
