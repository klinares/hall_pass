// Builds Assets/Art/Mascot/DogArt.asset from Assets/Art/Sprites/manifest.json and the imported dog and bubble
// sprites, so the game never reads the manifest at runtime.
//
//   Menu: World Tour > Sprites > Build dog art
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -executeMethod MascotArtBuilder.BuildBatch
//       [-mascotSummary <file>]   default Temp/MascotArt-summary.json
// Run after SpriteImport.ApplyAll (the sprites must be imported). The batch method ends in
// EditorApplication.Exit(0 or 1); the summary file is the evidence (DECISIONS, D1).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using WorldTour.Game.Mascot;

public static class MascotArtBuilder
{
    const string Root = "Assets/Art/Sprites";
    const string AssetPath = "Assets/Art/Mascot/DogArt.asset";

    // filled by JsonUtility through reflection, so the compiler cannot see the assignments
#pragma warning disable 0649
    [Serializable] class Entry { public string name; public string group; public string file; public int w; public int h; public int[] offset; public int[] box; public string part; public string layer; }
    [Serializable] class StackEntry { public string pose; public string outfit; public string[] layers; }
    [Serializable] class Tail { public float[] rotateAboutPx; }
    [Serializable] class Lids { public float[] scaleAboutPx; }
    [Serializable] class Anim { public Tail tail; public Lids lids; }
    [Serializable] class DogBlock { public string[] poses; public string[] outfits; public StackEntry[] stackList; public Anim animation; }
    [Serializable] class DogGroup { public float[] framePx; public float unitsToPx; }
    [Serializable] class Groups { public DogGroup dog; }
    [Serializable] class Manifest { public int format; public Groups groups; public Entry[] sprites; public DogBlock dog; }

#pragma warning restore 0649

    [Serializable]
    public class Summary
    {
        public string result = "Failed";
        public int layers, stacks;
        public List<string> errors = new List<string>();
    }

    [MenuItem("World Tour/Sprites/Build dog art")]
    static void BuildFromMenu()
    {
        var s = Build();
        Debug.Log($"MascotArtBuilder: {s.result}, {s.layers} layers, {s.stacks} stacks");
        foreach (var e in s.errors) Debug.LogError("MascotArtBuilder: " + e);
    }

    public static void BuildBatch()
    {
        string path = Arg("-mascotSummary") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "MascotArt-summary.json"));
        Summary s;
        try { s = Build(); } catch (Exception e) { s = new Summary(); s.errors.Add(e.ToString()); }
        bool wrote = false;
        try { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, JsonUtility.ToJson(s, true)); wrote = true; }
        catch (Exception e) { Debug.LogError("MascotArtBuilder: summary not written: " + e.Message); }
        EditorApplication.Exit(wrote && s.result == "Succeeded" ? 0 : 1);
    }

    static string Arg(string name)
    {
        var a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return null;
    }

    public static Summary Build()
    {
        var sum = new Summary();
        string mf = Path.GetFullPath(Path.Combine(Application.dataPath, "..", Root, "manifest.json"));
        if (!File.Exists(mf)) { sum.errors.Add("missing " + Root + "/manifest.json"); return sum; }
        var m = JsonUtility.FromJson<Manifest>(File.ReadAllText(mf));
        if (m?.sprites == null || m.dog?.stackList == null || m.groups?.dog?.framePx == null || m.dog.animation?.tail == null || m.dog.animation.lids == null)
        { sum.errors.Add("manifest.json lacks dog data (re-run Tools/export_sprites.mjs)"); return sum; }

        var art = AssetDatabase.LoadAssetAtPath<DogArt>(AssetPath);
        if (art == null)
        {
            Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "..", Path.GetDirectoryName(AssetPath))));
            art = ScriptableObject.CreateInstance<DogArt>();
            AssetDatabase.CreateAsset(art, AssetPath);
        }
        Sprite Load(Entry e)
        {
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/" + e.file);
            if (sp == null) sum.errors.Add("sprite not imported: " + e.file);
            return sp;
        }

        art.framePx = new Vector2(m.groups.dog.framePx[0], m.groups.dog.framePx[1]);
        art.unitsToPx = m.groups.dog.unitsToPx;
        art.tailPivotPx = new Vector2(m.dog.animation.tail.rotateAboutPx[0], m.dog.animation.tail.rotateAboutPx[1]);
        art.lidPivotPx = new Vector2(m.dog.animation.lids.scaleAboutPx[0], m.dog.animation.lids.scaleAboutPx[1]);
        art.layers = m.sprites.Where(e => e.group == "dog").Select(e => new DogArt.Layer
        {
            name = e.name, sprite = Load(e), size = new Vector2(e.w, e.h),
            offset = e.offset != null && e.offset.Length == 2 ? new Vector2(e.offset[0], e.offset[1]) : Vector2.zero,
            part = e.part, layer = e.layer
        }).ToList();
        art.stacks = m.dog.stackList.Select(s => new DogArt.Stack { pose = s.pose, outfit = s.outfit, layers = s.layers }).ToList();
        var names = new HashSet<string>(art.layers.Select(l => l.name));
        foreach (var s in art.stacks) foreach (var n in s.layers) if (!names.Contains(n)) sum.errors.Add($"stack {s.pose}/{s.outfit} names unknown layer {n}");

        var bubble = m.sprites.FirstOrDefault(e => e.name == "bubble");
        var tail = m.sprites.FirstOrDefault(e => e.name == "bubble_tail");
        if (bubble == null || tail == null || bubble.box == null || tail.offset == null) sum.errors.Add("manifest lacks decor bubble or bubble_tail");
        else
        {
            art.bubble = Load(bubble);
            art.bubbleTail = Load(tail);
            art.bubbleBoxPx = new Vector4(bubble.box[0], bubble.box[1], bubble.box[2], bubble.box[3]);
            art.bubbleTailOffsetPx = new Vector2(tail.offset[0], tail.offset[1]);
        }

        // every pose with no outfit and every sitting pose with each outfit must have a stack
        foreach (var p in new[] { "idle", "cheer", "think", "oops", "sleepy" })
        {
            if (!art.stacks.Any(s => s.pose == p && s.outfit == "none")) sum.errors.Add("no stack for " + p);
            if (p != "sleepy") foreach (var o in DogPoses.Outfits) if (!art.stacks.Any(s => s.pose == p && s.outfit == o)) sum.errors.Add($"no stack for {p}/{o}");
        }

        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
        sum.layers = art.layers.Count; sum.stacks = art.stacks.Count;
        sum.result = sum.errors.Count == 0 ? "Succeeded" : "Failed";
        return sum;
    }
}
