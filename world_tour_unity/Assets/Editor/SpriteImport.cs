// Import settings and sprite atlases for the exported art in Assets/Art/Sprites/ (written by
// Tools/export_sprites.mjs, which also writes Assets/Art/Sprites/manifest.json).
//
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -buildTarget WebGL -executeMethod SpriteImport.ApplyAll
//       [-spriteSummary <file>]   default Temp/SpriteImport-summary.json
//   Menu: World Tour > Sprites > Apply import settings (same work, no exit)
//
// Every PNG under Assets/Art/Sprites/ is imported by SpriteImportPostprocessor as: Sprite (2D and UI), single,
// no mipmaps, bilinear, clamp, alpha is transparency, 200 pixels per unit (the PNGs are 2x their largest CSS
// size at 1920x1080, so Set Native Size gives that CSS size on a 1920x1080 reference canvas), full-rect mesh,
// pivot and, for the decor panels, the 9-slice border from the manifest. Compression: crunched DXT5 (BC3) for WebGL, crunched for the default platform.
// ApplyAll reimports them all, writes one Sprite Atlas V2 per group in Assets/Art/Atlases/, packs them, checks everything against the manifest and writes
// a summary. It always ends in EditorApplication.Exit(0 or 1); the summary file is the evidence (DECISIONS, D1).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

public static class SpriteImport
{
    public const string Root = "Assets/Art/Sprites";
    public const string AtlasFolder = "Assets/Art/Atlases";
    public const float PixelsPerUnit = 200f;
    /// <summary>A group folder with this suffix (decor_peach-navy, dog_peach-navy) holds the peach + navy twins of that group.</summary>
    public const string ThemeSuffix = "_peach-navy";
    public const int MaxSize = 2048;
    public const int Quality = 75;
    public static readonly string[] AtlasGroups = { "stickers", "map", "dog", "ui", "decor" };

    [Serializable] public class Entry { public string name; public string group; public string file; public int w; public int h; public float[] pivot; public int[] border; public bool tint; }
    [Serializable] public class Manifest { public int format; public string theme; public Entry[] sprites; }

    [Serializable] public class GroupInfo { public string name; public int sprites; public string atlas; public int packed; }
    [Serializable]
    public class Summary
    {
        public string result = "Failed";
        public string startedAt, endedAt;
        public string manifest;
        public int manifestSprites, pngFiles, imported, checkedOk;
        public List<GroupInfo> groups = new List<GroupInfo>();
        public List<string> errors = new List<string>();
        public List<string> warnings = new List<string>();
    }

    // ---- manifest (read from disk, so it works before the AssetDatabase has imported it) ----
    static Manifest cached;
    static DateTime cachedStamp;
    static Dictionary<string, Entry> byFile;

    static string ProjectPath(string assetPath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));

    public static Manifest LoadManifest()
    {
        string p = ProjectPath(Root + "/manifest.json");
        if (!File.Exists(p)) { cached = null; byFile = null; return null; }
        var stamp = File.GetLastWriteTimeUtc(p);
        if (cached != null && stamp == cachedStamp) return cached;
        var m = JsonUtility.FromJson<Manifest>(File.ReadAllText(p));
        if (m == null || m.sprites == null) throw new InvalidDataException("manifest.json has no sprites list");
        byFile = new Dictionary<string, Entry>(StringComparer.Ordinal);
        foreach (var e in m.sprites) if (e != null && !string.IsNullOrEmpty(e.file)) byFile[e.file] = e;
        cached = m; cachedStamp = stamp;
        return m;
    }

    public static bool IsOurs(string assetPath) =>
        assetPath.StartsWith(Root + "/", StringComparison.Ordinal) && assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

    public static Entry Find(string assetPath)
    {
        try { LoadManifest(); } catch (Exception e) { Debug.LogWarning("SpriteImport: manifest unreadable: " + e.Message); return null; }
        if (byFile == null) return null;
        string rel = assetPath.Substring(Root.Length + 1);
        // A theme's twin of a decor piece (decor_peach-navy/bubble.png, phase 5) has the same size, pivot and border as the
        // tour-pink piece (the exporter's peach-navy manifest matches entry for entry), so it imports with that entry.
        // The same goes for riff's layers (dog_peach-navy/, 6.2A): only the pieces whose pixels differ have a twin.
        int slash = rel.IndexOf('/');
        if (slash > 0)
        {
            string group = rel.Substring(0, slash);
            if (group.EndsWith(ThemeSuffix, StringComparison.Ordinal)) rel = group.Substring(0, group.Length - ThemeSuffix.Length) + rel.Substring(slash);
        }
        byFile.TryGetValue(rel, out var hit);
        return hit;
    }

    static Vector2 PivotOf(Entry e) =>
        e != null && e.pivot != null && e.pivot.Length == 2 ? new Vector2(e.pivot[0], e.pivot[1]) : new Vector2(0.5f, 0.5f);

    // manifest border is [left, bottom, right, top] px, the same order as Sprite.border (Vector4 x, y, z, w)
    static Vector4 BorderOf(Entry e) =>
        e != null && e.border != null && e.border.Length == 4 ? new Vector4(e.border[0], e.border[1], e.border[2], e.border[3]) : Vector4.zero;

    static TextureImporterPlatformSettings WebGLSettings() => new TextureImporterPlatformSettings
    {
        name = "WebGL", overridden = true, maxTextureSize = MaxSize,
        format = TextureImporterFormat.DXT5Crunched, textureCompression = TextureImporterCompression.Compressed,
        crunchedCompression = true, compressionQuality = Quality
    };

    /// <summary>Called from the postprocessor before each import of a PNG under Assets/Art/Sprites/.</summary>
    public static void Configure(TextureImporter ti, Entry e)
    {
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Bilinear;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.alphaSource = TextureImporterAlphaSource.FromInput;
        ti.alphaIsTransparency = true;
        ti.sRGBTexture = true;
        ti.isReadable = false;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.spritePixelsPerUnit = PixelsPerUnit;
        ti.maxTextureSize = MaxSize;
        ti.textureCompression = TextureImporterCompression.Compressed;
        ti.crunchedCompression = true;
        ti.compressionQuality = Quality;

        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteMode = (int)SpriteImportMode.Single;
        s.spriteAlignment = (int)SpriteAlignment.Custom;
        s.spritePivot = PivotOf(e);
        s.spriteMeshType = SpriteMeshType.FullRect;
        s.spriteExtrude = 1;
        s.spriteGenerateFallbackPhysicsShape = false;
        ti.SetTextureSettings(s);
        ti.spriteBorder = BorderOf(e);

        ti.SetPlatformTextureSettings(WebGLSettings());
    }

    // ---- menu and batch entry points ----
    [MenuItem("World Tour/Sprites/Apply import settings")]
    static void ApplyFromMenu()
    {
        var summary = Run();
        Debug.Log($"SpriteImport: {summary.result}, {summary.checkedOk}/{summary.manifestSprites} sprites ok, {summary.errors.Count} errors, {summary.warnings.Count} warnings");
        foreach (var e in summary.errors) Debug.LogError("SpriteImport: " + e);
        foreach (var w in summary.warnings) Debug.LogWarning("SpriteImport: " + w);
    }

    public static void ApplyAll()
    {
        string summaryPath = Arg("-spriteSummary") ?? ProjectPath("Temp/SpriteImport-summary.json");
        Summary summary;
        try { summary = Run(); }
        catch (Exception e) { summary = new Summary { startedAt = Now() }; summary.errors.Add(e.ToString()); }
        summary.endedAt = Now();
        if (summary.errors.Count > 0) summary.result = "Failed";
        bool wrote = false;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(summaryPath)));
            File.WriteAllText(summaryPath, JsonUtility.ToJson(summary, true));
            wrote = true;
            Debug.Log($"SpriteImport summary: {summary.result}, {summary.checkedOk}/{summary.manifestSprites} sprites, {summary.errors.Count} errors -> {summaryPath}");
        }
        catch (Exception e) { Debug.LogError("SpriteImport: could not write the summary: " + e.Message); }
        EditorApplication.Exit(wrote && summary.result == "Succeeded" ? 0 : 1);
    }

    static string Arg(string name)
    {
        var a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return null;
    }

    static string Now() => DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");

    public static Summary Run()
    {
        var sum = new Summary { startedAt = Now(), manifest = Root + "/manifest.json" };
        cached = null;
        var m = LoadManifest();
        if (m == null) { sum.errors.Add("missing " + sum.manifest + " (run Tools/export_sprites.mjs)"); return sum; }
        sum.manifestSprites = m.sprites.Length;

        // 1. reimport every PNG with the settings above (the postprocessor applies them)
        var pngs = AssetDatabase.FindAssets("t:Texture2D", new[] { Root })
            .Select(AssetDatabase.GUIDToAssetPath).Where(IsOurs).OrderBy(p => p, StringComparer.Ordinal).ToList();
        sum.pngFiles = pngs.Count;
        AssetDatabase.StartAssetEditing();
        try { foreach (var p in pngs) { AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate); sum.imported++; } }
        finally { AssetDatabase.StopAssetEditing(); }
        var listed = new HashSet<string>(m.sprites.Select(e => Root + "/" + e.file), StringComparer.Ordinal);
        foreach (var p in pngs) if (!listed.Contains(p) && p.IndexOf(ThemeSuffix + "/", StringComparison.Ordinal) < 0) sum.warnings.Add(p + " is not in the manifest (left from an older export?)");

        // 2. check each manifest sprite against what Unity imported
        foreach (var e in m.sprites)
        {
            string p = Root + "/" + e.file;
            var ti = AssetImporter.GetAtPath(p) as TextureImporter;
            if (ti == null) { sum.errors.Add(p + ": missing"); continue; }
            var s = new TextureImporterSettings(); ti.ReadTextureSettings(s);
            var want = PivotOf(e);
            var web = ti.GetPlatformTextureSettings("WebGL");
            var bad = new List<string>();
            if (ti.textureType != TextureImporterType.Sprite) bad.Add("not a Sprite");
            if (ti.mipmapEnabled) bad.Add("mipmaps on");
            if (ti.filterMode != FilterMode.Bilinear) bad.Add("filter " + ti.filterMode);
            if (Math.Abs(ti.spritePixelsPerUnit - PixelsPerUnit) > 1e-3f) bad.Add("ppu " + ti.spritePixelsPerUnit);
            if ((s.spritePivot - want).sqrMagnitude > 1e-8f) bad.Add($"pivot {s.spritePivot} not {want}");
            if (ti.spriteBorder != BorderOf(e)) bad.Add($"border {ti.spriteBorder} not {BorderOf(e)}");
            if (!web.overridden || web.format != TextureImporterFormat.DXT5Crunched) bad.Add("WebGL format " + web.format);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(p);
            if (sprite == null) bad.Add("no Sprite object");
            else if ((int)sprite.rect.width != e.w || (int)sprite.rect.height != e.h) bad.Add($"size {sprite.rect.width}x{sprite.rect.height} not {e.w}x{e.h}");
            if (bad.Count > 0) sum.errors.Add(p + ": " + string.Join(", ", bad)); else sum.checkedOk++;
        }

        // 3. one Sprite Atlas V2 per group, packed for the active target
        Directory.CreateDirectory(ProjectPath(AtlasFolder));
        AssetDatabase.Refresh();
        var atlases = new List<SpriteAtlas>();
        foreach (var g in m.sprites.Select(e => e.group).Distinct().OrderBy(x => x, StringComparer.Ordinal))
        {
            var info = new GroupInfo { name = g, sprites = m.sprites.Count(e => e.group == g) };
            sum.groups.Add(info);
            if (!AtlasGroups.Contains(g)) continue;
            string folder = Root + "/" + g;
            var folderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder);
            if (folderAsset == null) { sum.errors.Add("no folder " + folder); continue; }
            string path = AtlasFolder + "/" + g + ".spriteatlasv2";
            var asset = new SpriteAtlasAsset();
            asset.Add(new UnityEngine.Object[] { folderAsset });
            SpriteAtlasAsset.Save(asset, path);          // rewrites the file; the .meta (and GUID) stay
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = AssetImporter.GetAtPath(path) as SpriteAtlasImporter;
            if (imp == null) { sum.errors.Add(path + ": no SpriteAtlasImporter (is Sprite Atlas V2 enabled?)"); continue; }
            imp.includeInBuild = true;
            imp.packingSettings = new SpriteAtlasPackingSettings
            {
                blockOffset = 1, padding = 4, enableRotation = false,
                enableTightPacking = false        // UI Images draw the full rect, so tight packing would bleed neighbours
            };
            imp.textureSettings = new SpriteAtlasTextureSettings
            {
                readable = false, generateMipMaps = false, sRGB = true, filterMode = FilterMode.Bilinear, anisoLevel = 0
            };
            var def = imp.GetPlatformSettings("DefaultTexturePlatform");
            def.maxTextureSize = MaxSize; def.textureCompression = TextureImporterCompression.Compressed;
            def.crunchedCompression = true; def.compressionQuality = Quality;
            imp.SetPlatformSettings(def);
            imp.SetPlatformSettings(WebGLSettings());
            imp.SaveAndReimport();
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
            if (atlas == null) { sum.errors.Add(path + ": not loadable as a SpriteAtlas"); continue; }
            info.atlas = path;
            atlases.Add(atlas);
        }
        if (atlases.Count > 0) SpriteAtlasUtility.PackAtlases(atlases.ToArray(), EditorUserBuildSettings.activeBuildTarget, false);
        foreach (var info in sum.groups)
        {
            if (info.atlas == null) { if (AtlasGroups.Contains(info.name)) continue; sum.warnings.Add($"group {info.name}: no atlas by design (single textures)"); continue; }
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(info.atlas);
            info.packed = atlas != null ? atlas.spriteCount : 0;
            if (info.packed != info.sprites) sum.errors.Add($"{info.atlas}: packed {info.packed} sprites, manifest has {info.sprites}");
        }
        AssetDatabase.SaveAssets();
        sum.result = sum.errors.Count == 0 ? "Succeeded" : "Failed";
        return sum;
    }
}

/// <summary>Applies SpriteImport settings to every PNG imported under Assets/Art/Sprites/.</summary>
public class SpriteImportPostprocessor : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!SpriteImport.IsOurs(assetPath)) return;
        var e = SpriteImport.Find(assetPath);
        if (e == null) Debug.LogWarning("SpriteImport: " + assetPath + " is not in manifest.json; centre pivot used");
        SpriteImport.Configure((TextureImporter)assetImporter, e);
    }
}
