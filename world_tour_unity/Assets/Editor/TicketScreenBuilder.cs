// Makes the ticket screen's assets by code (task 2.6), so nothing is edited by hand:
//   Assets/Art/Ticket/TicketArt.asset                 decor sprites (ticket_stub, stamp, paper_lined, tape_b, bubble,
//                                                     bubble_tail), every stickers/<id>.png and map/venue_<id>.png, and
//                                                     two TMP material presets on the display font
//   Assets/Art/Ticket/CaveatBrush SDF - Shout.mat     .shout: outline + offset underlay (saved as assets, so their
//   Assets/Art/Ticket/CaveatBrush SDF - Stroke.mat    shader variants ship in the Web build)
//   Assets/Game/Resources/Screens/Screen_Ticket.prefab  TicketScreen + CanvasGroup, full screen, with the StickerButton
//                                                     prefab and the art. TicketScreen.Open loads it from Resources.
// Run after UiBuilder.BuildAll (the button prefab) and after task 2.1's sprites and fonts are imported:
//   Unity -batchmode -projectPath ... -executeMethod TicketScreenBuilder.Build
// Logs "WT_TICKET_PREFAB ok ..." or "WT_TICKET_PREFAB failed ...", and exits through EditorApplication.Exit (D1) in batch mode.
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using WorldTour.Game.Ticket;
using WorldTour.Game.Ui;

public static class TicketScreenBuilder
{
    const string ArtDir = "Assets/Art/Ticket";
    const string ArtPath = ArtDir + "/TicketArt.asset";
    const string ShoutPath = ArtDir + "/CaveatBrush SDF - Shout.mat";
    const string StrokePath = ArtDir + "/CaveatBrush SDF - Stroke.mat";
    const string PrefabDir = "Assets/Game/Resources/Screens";
    const string PrefabPath = PrefabDir + "/Screen_Ticket.prefab";
    const string ButtonPrefabPath = "Assets/Game/Ui/Prefabs/StickerButton.prefab";
    const string Sprites = "Assets/Art/Sprites";
    const string DisplayFontPath = "Assets/Fonts/CaveatBrush/CaveatBrush-Regular SDF.asset";

    [MenuItem("World Tour/UI/Build ticket screen (2.6)")]
    static void BuildMenu() { Make(); }

    public static void Build()
    {
        int code = 0;
        try { Make(); }
        catch (Exception e) { Debug.LogError("WT_TICKET_PREFAB failed " + e.Message); code = 1; }
        if (Application.isBatchMode) EditorApplication.Exit(code);
    }

    static void Make()
    {
        Directory.CreateDirectory(ArtDir);
        Directory.CreateDirectory(PrefabDir);

        var art = AssetDatabase.LoadAssetAtPath<TicketArt>(ArtPath);
        if (art == null)
        {
            art = ScriptableObject.CreateInstance<TicketArt>();
            AssetDatabase.CreateAsset(art, ArtPath);
        }
        art.ticketStub = SpriteAt("decor/ticket_stub");
        art.stamp = SpriteAt("decor/stamp");
        art.paperLined = SpriteAt("decor/paper_lined");
        art.tape = SpriteAt("decor/tape_b");
        art.bubble = SpriteAt("decor/bubble");
        art.bubbleTail = SpriteAt("decor/bubble_tail");
        art.sprites.Clear();
        int stickers = 0, venues = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { Sprites + "/stickers", Sprites + "/map" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            bool sticker = path.Contains("/stickers/") && !name.EndsWith("_sm", StringComparison.Ordinal);
            bool venue = path.Contains("/map/") && name.StartsWith("venue_", StringComparison.Ordinal);
            if (!sticker && !venue) continue;
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null) continue;
            art.sprites.Add(new TicketArt.Entry { name = name, sprite = s });
            if (sticker) stickers++; else venues++;
        }
        art.sprites = art.sprites.OrderBy(e => e.name, StringComparer.Ordinal).ToList();

        var display = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DisplayFontPath);
        if (display != null && display.material != null)
        {
            // .shout: white face (the label colour), 2px ink outline, 5px ink offset shadow (at the h1's 86px size)
            art.shout = Preset(ShoutPath, display.material, m =>
            {
                m.EnableKeyword("OUTLINE_ON");
                m.EnableKeyword("UNDERLAY_ON");
                m.SetColor("_OutlineColor", Color.black);
                m.SetFloat("_OutlineWidth", 0.3f);
                m.SetColor("_UnderlayColor", Color.black);
                m.SetFloat("_UnderlayOffsetX", 0.5f);
                m.SetFloat("_UnderlayOffsetY", -0.5f);
                m.SetFloat("_UnderlayDilate", 0.3f);
                m.SetFloat("_UnderlaySoftness", 0f);
            });
            // -webkit-text-stroke .5-.6px ink (.tk-top, the missed tag)
            art.stroke = Preset(StrokePath, display.material, m =>
            {
                m.EnableKeyword("OUTLINE_ON");
                m.SetColor("_OutlineColor", Color.black);
                m.SetFloat("_OutlineWidth", 0.12f);
            });
        }
        else Debug.LogWarning("[ticket] " + DisplayFontPath + " is missing; the shout and stroke fall back to plain text");
        EditorUtility.SetDirty(art);

        var button = AssetDatabase.LoadAssetAtPath<StickerButton>(ButtonPrefabPath);
        if (button == null) Debug.LogWarning("[ticket] " + ButtonPrefabPath + " is missing (run UiBuilder.BuildAll first); the ticket has no buttons without it");

        var root = new GameObject("Screen_Ticket", typeof(RectTransform), typeof(CanvasGroup));
        root.layer = 5;
        var rt = (RectTransform)root.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var screen = root.AddComponent<TicketScreen>();
        var so = new SerializedObject(screen);
        so.FindProperty("buttonPrefab").objectReferenceValue = button;
        so.FindProperty("art").objectReferenceValue = art;
        so.ApplyModifiedPropertiesWithoutUndo();
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
        UnityEngine.Object.DestroyImmediate(root);
        if (!ok || saved == null) throw new Exception("could not save " + PrefabPath);
        AssetDatabase.SaveAssets();
        Debug.Log("WT_TICKET_PREFAB ok path=" + PrefabPath + " button=" + (button != null ? "sticker" : "none")
            + " stickers=" + stickers + " venues=" + venues
            + " decor=" + new[] { art.ticketStub, art.stamp, art.paperLined, art.tape, art.bubble, art.bubbleTail }.Count(s => s != null) + "/6"
            + " shout=" + (art.shout != null ? "yes" : "no"));
    }

    static Sprite SpriteAt(string rel)
    {
        string path = Sprites + "/" + rel + ".png";
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null) Debug.LogWarning("[ticket] no sprite at " + path);
        return s;
    }

    static Material Preset(string path, Material source, Action<Material> setup)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(source);
            AssetDatabase.CreateAsset(m, path);
        }
        else m.CopyPropertiesFromMaterial(source);
        m.shaderKeywords = new string[0];
        setup(m);
        EditorUtility.SetDirty(m);
        return m;
    }
}
