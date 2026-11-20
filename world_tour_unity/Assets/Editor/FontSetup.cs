// TextMeshPro font assets for the two bundled fonts, and TMP defaults.
//
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -executeMethod FontSetup.ApplyAll
//       [-fontSummary <file>]   default Temp/FontSetup-summary.json
//       [-fontRebuild]          recreate the font assets even if they exist (their GUIDs change; the Boot scene and
//                               TMP Settings are relinked, anything else that points at them must be relinked by hand)
//
// Makes static SDF assets (no source font needed at runtime, so the .ttf files stay out of the build):
//   Assets/Fonts/CaveatBrush/CaveatBrush-Regular SDF.asset   (display)
//   Assets/Fonts/CourierPrime/CourierPrime-Regular SDF.asset (body, numbers; TMP default)
//   Assets/Fonts/CourierPrime/CourierPrime-Bold SDF.asset    (Courier Prime's bold, used by <b> through the weight table)
// with ASCII 32-126 plus the non-ASCII characters the game shows (Glyphs below). Characters a font lacks are listed
// in the summary. Then: TMP Settings default font = Courier Prime Regular, no fallback fonts, no default sprite asset
// (EmojiOne); the Boot scene's text moves to Courier Prime; LiberationSans and EmojiOne are deleted when nothing
// else uses them, and moved out of Resources (so they do not ship) when something still does. Ends in EditorApplication.Exit(0 or 1) after writing the summary (DECISIONS, D1).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class FontSetup
{
    public const int SamplingPointSize = 64;
    public const int Padding = 8;

    /// <summary>The non-ASCII characters in the web game's text and the Core generators (as character codes, so this file stays ASCII).</summary>
    public static readonly int[] Glyphs =
    {
        0x00D7, 0x00F7, 0x2212,                 // times, divide, minus
        0x2018, 0x2019, 0x201C, 0x201D,         // curly quotes and apostrophe
        0x2026, 0x2014, 0x2013, 0x2022, 0x00B7, // ellipsis, dashes, bullet, middle dot
        0x00A2, 0x00E9,                         // cent, e acute
        0x00B9, 0x00B2, 0x00B3, 0x2074,         // superscript 1 2 3 4
        0x2044, 0x00BD, 0x00BC, 0x215B, 0x2153, 0x2159, // fraction slash and vulgar fractions
        0x2192, 0x2190, 0x2191, 0x2193, 0x25B6, // arrows
        0x2736, 0x2605, 0x2606, 0x2665, 0x25A2  // six-point star, stars, heart, the fraction-key box
    };

    sealed class Spec { public string Name, Ttf, Asset; }
    static readonly Spec Caveat = new Spec { Name = "CaveatBrush-Regular SDF", Ttf = "Assets/Fonts/CaveatBrush/CaveatBrush-Regular.ttf", Asset = "Assets/Fonts/CaveatBrush/CaveatBrush-Regular SDF.asset" };
    static readonly Spec Courier = new Spec { Name = "CourierPrime-Regular SDF", Ttf = "Assets/Fonts/CourierPrime/CourierPrime-Regular.ttf", Asset = "Assets/Fonts/CourierPrime/CourierPrime-Regular SDF.asset" };
    static readonly Spec CourierBold = new Spec { Name = "CourierPrime-Bold SDF", Ttf = "Assets/Fonts/CourierPrime/CourierPrime-Bold.ttf", Asset = "Assets/Fonts/CourierPrime/CourierPrime-Bold SDF.asset" };

    static readonly string[] Remove =
    {
        "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset",
        "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset",
        "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Drop Shadow.mat",
        "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat",
        "Assets/TextMesh Pro/Fonts/LiberationSans.ttf",          // its OFL.txt stays while the .ttf does
        "Assets/TextMesh Pro/Resources/Sprite Assets/EmojiOne.asset",
        "Assets/TextMesh Pro/Sprites/EmojiOne.png",
        "Assets/TextMesh Pro/Sprites/EmojiOne.json",
        "Assets/TextMesh Pro/Sprites/EmojiOne Attribution.txt",
    };

    [Serializable] public class FontInfo { public string asset; public int atlas; public int glyphs; public string mode; public List<string> missing = new List<string>(); }
    [Serializable]
    public class Summary
    {
        public string result = "Failed";
        public string startedAt, endedAt;
        public List<FontInfo> fonts = new List<FontInfo>();
        public string defaultFont;
        public int bootTextsRelinked;
        public List<string> removed = new List<string>();
        public List<string> kept = new List<string>();
        public List<string> errors = new List<string>();
        public List<string> warnings = new List<string>();
    }

    static string Arg(string name)
    {
        var a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return null;
    }
    static bool Flag(string name) => Environment.GetCommandLineArgs().Contains(name);
    static string Now() => DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");

    public static void ApplyAll()
    {
        string path = Arg("-fontSummary") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "FontSetup-summary.json"));
        var s = new Summary { startedAt = Now() };
        try { Run(s, Flag("-fontRebuild")); } catch (Exception e) { s.errors.Add(e.ToString()); }
        s.endedAt = Now();
        s.result = s.errors.Count == 0 ? "Succeeded" : "Failed";
        bool wrote = false;
        try { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))); File.WriteAllText(path, JsonUtility.ToJson(s, true)); wrote = true; }
        catch (Exception e) { Debug.LogError("FontSetup: summary not written: " + e.Message); }
        Debug.Log($"FontSetup summary: {s.result}, {s.errors.Count} errors -> {path}");
        EditorApplication.Exit(wrote && s.result == "Succeeded" ? 0 : 1);
    }

    static string CharSet()
    {
        var chars = new List<char>();
        for (int c = 32; c <= 126; c++) chars.Add((char)c);
        foreach (int g in Glyphs) chars.Add((char)g);
        return new string(chars.ToArray());
    }

    static TMP_FontAsset Make(Spec spec, bool rebuild, Summary sum)
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(spec.Asset);
        var info = new FontInfo { asset = spec.Asset };
        sum.fonts.Add(info);
        if (existing != null && !rebuild)
        {
            sum.warnings.Add(spec.Asset + " exists; kept (use -fontRebuild to recreate)");
            Describe(existing, info);
            return existing;
        }
        if (existing != null) AssetDatabase.DeleteAsset(spec.Asset);
        var font = AssetDatabase.LoadAssetAtPath<Font>(spec.Ttf);
        if (font == null) { sum.errors.Add("missing font " + spec.Ttf); return null; }

        // which wanted characters the font has at all
        FontEngine.InitializeFontEngine();
        if (FontEngine.LoadFontFace(font, SamplingPointSize) != FontEngineError.Success) { sum.errors.Add("cannot load " + spec.Ttf); return null; }
        string all = CharSet();
        var has = all.Where(c => FontEngine.TryGetGlyphIndex(c, out uint gi) && gi != 0).ToArray();
        info.missing = all.Where(c => !has.Contains(c)).Select(c => "U+" + ((int)c).ToString("X4")).ToList();

        foreach (int size in new[] { 512, 1024, 2048 })
        {
            var fa = TMP_FontAsset.CreateFontAsset(font, SamplingPointSize, Padding, GlyphRenderMode.SDFAA, size, size, AtlasPopulationMode.Dynamic, false);
            fa.TryAddCharacters(new string(has), out string notAdded);
            if (!string.IsNullOrEmpty(notAdded) && size < 2048) { UnityEngine.Object.DestroyImmediate(fa, true); continue; }
            if (!string.IsNullOrEmpty(notAdded)) sum.errors.Add(spec.Asset + ": did not fit a 2048 atlas: " + notAdded.Length + " characters");
            fa.name = spec.Name;
            fa.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(fa, spec.Asset);
            fa.atlasTexture.name = spec.Name + " Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTexture, fa);
            fa.material.name = spec.Name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            Describe(fa, info);
            return fa;
        }
        return null;
    }

    static void Describe(TMP_FontAsset fa, FontInfo info)
    {
        info.atlas = fa.atlasWidth;
        info.glyphs = fa.characterTable.Count;
        info.mode = fa.atlasPopulationMode.ToString();
    }

    static void Run(Summary sum, bool rebuild)
    {
        var caveat = Make(Caveat, rebuild, sum);
        var courier = Make(Courier, rebuild, sum);
        var bold = Make(CourierBold, rebuild, sum);
        if (caveat == null || courier == null || bold == null) { sum.errors.Add("font assets missing"); return; }

        // <b> on Courier Prime uses the real bold face
        courier.fontWeightTable[7].regularTypeface = bold;     // the getter returns the asset's own array
        EditorUtility.SetDirty(courier);

        // TMP Settings: Courier Prime by default, no fallback fonts, no EmojiOne
        var so = new SerializedObject(TMP_Settings.instance);
        so.FindProperty("m_defaultFontAsset").objectReferenceValue = courier;
        so.FindProperty("m_fallbackFontAssets").arraySize = 0;
        so.FindProperty("m_defaultSpriteAsset").objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(TMP_Settings.instance);
        sum.defaultFont = AssetDatabase.GetAssetPath(TMP_Settings.defaultFontAsset);

        // Boot scene text: off LiberationSans
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Boot.unity", OpenSceneMode.Single);
        foreach (var t in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.font == courier) continue;
            t.font = courier;
            t.fontSharedMaterial = courier.material;
            EditorUtility.SetDirty(t);
            sum.bootTextsRelinked++;
        }
        if (sum.bootTextsRelinked > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
        AssetDatabase.SaveAssets();

        // LiberationSans and EmojiOne: delete what nothing else uses; what something still uses (TMP's own shader
        // graphs point at LiberationSans SDF) moves out of Resources, since everything in a Resources folder ships
        var targets = new HashSet<string>(Remove.Where(p => AssetDatabase.LoadMainAssetAtPath(p) != null));
        var users = new Dictionary<string, List<string>>();
        foreach (var p in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && !targets.Contains(p)))
            foreach (var d in AssetDatabase.GetDependencies(p, false))
                if (targets.Contains(d)) { if (!users.ContainsKey(d)) users[d] = new List<string>(); users[d].Add(p); }
        var keep = new HashSet<string>(users.Keys);
        for (bool grew = true; grew;)                       // and whatever a kept target needs
        {
            grew = false;
            foreach (var k in keep.ToList())
                foreach (var d in AssetDatabase.GetDependencies(k, false))
                    if (targets.Contains(d) && keep.Add(d)) grew = true;
        }
        foreach (var p in targets.OrderBy(x => x, StringComparer.Ordinal))
        {
            if (keep.Contains(p))
            {
                string why = users.ContainsKey(p) ? "used by " + string.Join(", ", users[p]) : "needed by a kept asset";
                if (p.Contains("/Resources/"))
                {
                    string dest = "Assets/TextMesh Pro/Fonts/" + Path.GetFileName(p);
                    string err = AssetDatabase.MoveAsset(p, dest);
                    if (string.IsNullOrEmpty(err)) sum.kept.Add(p + " -> " + dest + " (out of Resources; " + why + ")");
                    else sum.errors.Add("could not move " + p + ": " + err);
                }
                else sum.kept.Add(p + " (" + why + ")");
            }
            else if (AssetDatabase.DeleteAsset(p)) sum.removed.Add(p);
            else sum.errors.Add("could not delete " + p);
        }
        AssetDatabase.SaveAssets();
        foreach (var p in AssetDatabase.GetAllAssetPaths())
            if (p.Contains("/Resources/") && (p.Contains("LiberationSans") || p.Contains("EmojiOne"))) sum.errors.Add("still in Resources: " + p);

        if (TMP_Settings.defaultFontAsset != courier) sum.errors.Add("TMP default font is not Courier Prime");
        foreach (var f in new[] { caveat, courier, bold })
            if (f.atlasPopulationMode != AtlasPopulationMode.Static) sum.errors.Add(f.name + " is not static");
    }
}
