// A TextMeshPro sprite asset for the symbols the bundled fonts lack, so text can keep the real characters.
//
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -executeMethod GlyphSetup.ApplyAll
//       [-glyphSummary <file>]   default Temp/GlyphSetup-summary.json (Unity empties Temp/ on exit: pass a path outside)
//
// Reads the 'glyphs' entries of Assets/Art/Sprites/manifest.json (drawn and rendered by Tools/export_sprites.mjs:
// white shapes on a Courier Prime line, 128 px per em) and the PNG files themselves, packs them into
// Assets/Art/Glyphs/Glyphs Atlas.png, and writes Assets/Art/Glyphs/Glyphs.asset: one sprite per glyph, named by
// its codepoint (u2736, ...) with its unicode set, so TMP draws the sprite wherever the font has no such character.
// Then TMP Settings: default sprite asset = Glyphs, emoji (sprite) fallback on, and sprites tinted with the text
// colour. A check renders each character in a throwaway TextMeshPro and confirms TMP picked the sprite.
// Ends in EditorApplication.Exit(0 or 1) after writing the summary (DECISIONS, D1).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

public static class GlyphSetup
{
    const string Manifest = "Assets/Art/Sprites/manifest.json";
    const string Folder = "Assets/Art/Glyphs";
    const string AtlasPath = Folder + "/Glyphs Atlas.png";
    const string AssetPath = Folder + "/Glyphs.asset";

    // filled by JsonUtility through reflection, so the compiler cannot see the assignments
#pragma warning disable 0649
    [Serializable] class GlyphInfo { public int unicode; public float emPx; public float width; public float height; public float bearingX; public float bearingY; public float advance; }
    [Serializable] class Entry { public string name; public string group; public string file; public GlyphInfo glyph; }
    [Serializable] class Doc { public Entry[] sprites; }
#pragma warning restore 0649

    [Serializable]
    public class Summary
    {
        public string result = "Failed";
        public string startedAt, endedAt;
        public int glyphs;
        public string atlas;
        public int atlasWidth, atlasHeight;
        public List<string> substituted = new List<string>();
        public List<string> errors = new List<string>();
    }

    static string Arg(string name)
    {
        var a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return null;
    }
    static string Now() => DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");
    static string Full(string assetPath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));

    public static void ApplyAll()
    {
        string path = Arg("-glyphSummary") ?? Full("Temp/GlyphSetup-summary.json");
        var s = new Summary { startedAt = Now() };
        try { Run(s); } catch (Exception e) { s.errors.Add(e.ToString()); }
        s.endedAt = Now();
        s.result = s.errors.Count == 0 ? "Succeeded" : "Failed";
        bool wrote = false;
        try { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))); File.WriteAllText(path, JsonUtility.ToJson(s, true)); wrote = true; }
        catch (Exception e) { Debug.LogError("GlyphSetup: summary not written: " + e.Message); }
        Debug.Log($"GlyphSetup summary: {s.result}, {s.glyphs} glyphs, {s.errors.Count} errors -> {path}");
        EditorApplication.Exit(wrote && s.result == "Succeeded" ? 0 : 1);
    }

    static void Run(Summary sum)
    {
        if (!File.Exists(Full(Manifest))) { sum.errors.Add("missing " + Manifest); return; }
        var doc = JsonUtility.FromJson<Doc>(File.ReadAllText(Full(Manifest)));
        var glyphs = (doc?.sprites ?? new Entry[0]).Where(e => e.group == "glyphs" && e.glyph != null && e.glyph.unicode > 0).OrderBy(e => e.glyph.unicode).ToList();
        if (glyphs.Count == 0) { sum.errors.Add("no glyphs in the manifest (re-run Tools/export_sprites.mjs)"); return; }
        float em = glyphs[0].glyph.emPx;

        // 1. pack the PNGs (read from disk, so their sprite import settings do not matter)
        var textures = new List<Texture2D>();
        foreach (var g in glyphs)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!t.LoadImage(File.ReadAllBytes(Full("Assets/Art/Sprites/" + g.file)))) { sum.errors.Add("cannot read " + g.file); return; }
            textures.Add(t);
        }
        var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Rect[] uv = atlas.PackTextures(textures.ToArray(), 4, 1024, false);
        Directory.CreateDirectory(Full(Folder));
        File.WriteAllBytes(Full(AtlasPath), atlas.EncodeToPNG());
        int aw = atlas.width, ah = atlas.height;
        AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceUpdate);
        var ti = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
        ti.textureType = TextureImporterType.Default;
        ti.alphaIsTransparency = true; ti.alphaSource = TextureImporterAlphaSource.FromInput;
        ti.mipmapEnabled = false; ti.filterMode = FilterMode.Bilinear; ti.wrapMode = TextureWrapMode.Clamp;
        ti.npotScale = TextureImporterNPOTScale.None; ti.isReadable = false; ti.sRGBTexture = true;
        ti.textureCompression = TextureImporterCompression.Uncompressed;      // a few KB of crisp white shapes
        ti.SaveAndReimport();
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        sum.atlas = AtlasPath; sum.atlasWidth = aw; sum.atlasHeight = ah;

        // 2. the sprite asset (recreated each run; TMP Settings is relinked below)
        if (AssetDatabase.LoadMainAssetAtPath(AssetPath) != null) AssetDatabase.DeleteAsset(AssetPath);
        var sa = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
        sa.name = "Glyphs";
        sa.spriteSheet = tex;
        for (int i = 0; i < glyphs.Count; i++)
        {
            var g = glyphs[i].glyph;
            var r = uv[i];
            var glyph = new TMP_SpriteGlyph
            {
                index = (uint)i,
                metrics = new GlyphMetrics(g.width, g.height, g.bearingX, g.bearingY, g.advance),
                glyphRect = new GlyphRect(Mathf.RoundToInt(r.x * aw), Mathf.RoundToInt(r.y * ah), Mathf.RoundToInt(r.width * aw), Mathf.RoundToInt(r.height * ah)),
                scale = 1f,
                atlasIndex = 0
            };
            sa.spriteGlyphTable.Add(glyph);
            var ch = new TMP_SpriteCharacter((uint)g.unicode, glyph) { name = glyphs[i].name, scale = 1f };
            sa.spriteCharacterTable.Add(ch);
        }
        var mat = new Material(Shader.Find("TextMeshPro/Sprite")) { name = "Glyphs Material" };
        mat.SetTexture(ShaderUtilities.ID_MainTex, tex);
        sa.material = mat;
        AssetDatabase.CreateAsset(sa, AssetPath);
        AssetDatabase.AddObjectToAsset(mat, sa);

        // face info: one em = emPx sprite pixels, on Courier Prime's line (ascent .78125 em, descent .3418 em)
        var so = new SerializedObject(sa);
        // Mark the asset as already in the current format. With an empty version, UpdateLookupTables runs TMP's legacy
        // upgrade, which rebuilds both tables from the (empty) old spriteInfoList and so wipes every glyph added above.
        var ver = so.FindProperty("m_Version");
        if (ver == null) throw new Exception("TMP_SpriteAsset has no serialized m_Version; check the TMP version");
        ver.stringValue = "1.1.0";
        var fi = so.FindProperty("m_FaceInfo");
        fi.FindPropertyRelative("m_PointSize").floatValue = em;
        fi.FindPropertyRelative("m_Scale").floatValue = 1f;
        fi.FindPropertyRelative("m_AscentLine").floatValue = .78125f * em;
        fi.FindPropertyRelative("m_DescentLine").floatValue = -.341797f * em;
        fi.FindPropertyRelative("m_LineHeight").floatValue = (.78125f + .341797f) * em;
        fi.FindPropertyRelative("m_Baseline").floatValue = 0f;
        so.ApplyModifiedPropertiesWithoutUndo();
        sa.UpdateLookupTables();
        EditorUtility.SetDirty(sa);
        AssetDatabase.SaveAssets();
        sum.glyphs = sa.spriteCharacterTable.Count;

        // 3. TMP Settings
        var st = new SerializedObject(TMP_Settings.instance);
        st.FindProperty("m_defaultSpriteAsset").objectReferenceValue = sa;
        st.FindProperty("m_enableEmojiSupport").boolValue = true;
        st.FindProperty("m_enableTintAllSprites").boolValue = true;
        st.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(TMP_Settings.instance);
        AssetDatabase.SaveAssets();

        // 4. check: TMP draws each character from the sprite asset
        var go = new GameObject("GlyphCheck");
        try
        {
            var text = go.AddComponent<TextMeshPro>();
            foreach (var g in glyphs)
            {
                text.text = "a" + char.ConvertFromUtf32(g.glyph.unicode) + "a";
                text.ForceMeshUpdate(true, true);
                var info = text.textInfo.characterInfo;
                bool ok = text.textInfo.characterCount == 3 && info[1].elementType == TMP_TextElementType.Sprite;
                if (ok) sum.substituted.Add(g.name);
                else sum.errors.Add(g.name + ": TMP did not substitute the sprite (characterCount " + text.textInfo.characterCount + ")");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
}
