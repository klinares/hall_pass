// Phase 8.3: builds what the wardrobe needs from the Editor, re-runnable and additive (nothing existing is rewritten):
//   - one recoloured material set per outfit and per hair colour (256 px copies of the KayKit palette texture, as WorldArtBuilder does for the Star),
//   - Assets/World/Wardrobe/Resources/WardrobeAssets.asset (those materials, a plain Lit material the accessories clone, the built-in meshes),
//   - the attachment points: WearFront (Star's chest bone), WearHead and WearNeck (riff's head and neck bones), as local poses read at the prefabs' rest pose and stored in
//     the assets (WardrobeAnchors makes them at run time; the Star and Riff prefabs and the scenes are not changed: their StandOnHeight component cannot be re-saved from the Editor).
//
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -executeMethod WardrobeBuilder.Run
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using WorldTour.Core.Meta;
using WorldTour.World.Wardrobe;

public static class WardrobeBuilder
{
    const string Dir = "Assets/World/Wardrobe";
    const string AssetPath = Dir + "/Resources/WardrobeAssets.asset";

    [MenuItem("World Tour/Build wardrobe")]
    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Dir + "/Resources");
            Directory.CreateDirectory(WorldArtBuilder.MatDir);
            Directory.CreateDirectory(WorldArtBuilder.TexDir);
            MakeAssets();
            AddAnchors(AssetDatabase.LoadAssetAtPath<WardrobeAssets>(AssetPath));
            AssetDatabase.SaveAssets();
            Debug.Log("WardrobeBuilder: done");
        }
        catch (System.Exception e) { Debug.LogError("WardrobeBuilder failed: " + e); }
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static Color C(string hex) { return WorldArtBuilder.C(hex); }

    static string Hx(Color c) { return "#" + ColorUtility.ToHtmlStringRGB(c); }

    /// <summary>A swatch gradient: the colour lightened (or darkened) at the top and the bottom, as WorldArtBuilder.Cell does.</summary>
    static (int, string, string) G(int cell, string hex, float lighten, float darken) { return WorldArtBuilder.Cell(cell, hex, lighten, darken); }

    static void MakeAssets()
    {
        string tdir = WorldArtBuilder.Art + "/KayKit_Adventurers/";
        string src = tdir + "rogue_texture.png";
        var a = AssetDatabase.LoadAssetAtPath<WardrobeAssets>(AssetPath);
        if (a == null) { a = ScriptableObject.CreateInstance<WardrobeAssets>(); AssetDatabase.CreateAsset(a, AssetPath); }

        var outfits = new System.Collections.Generic.List<OutfitSet>();
        foreach (ShopItem it in Shop.World.Where(i => i.Slot == Shop.Outfit))
        {
            string key = it.Id.Substring(Shop.Outfit.Length + 1);
            if (key == "tour")
            {
                outfits.Add(new OutfitSet
                {
                    id = it.Id,
                    top = AssetDatabase.LoadAssetAtPath<Material>(WorldArtBuilder.MatDir + "/star_top.mat"),
                    legs = AssetDatabase.LoadAssetAtPath<Material>(WorldArtBuilder.MatDir + "/star_legs.mat"),
                });
                continue;
            }
            string jacket = it.Swatch[0], shade = it.Swatch[1], trousers = it.Swatch[2];
            var top = WorldArtBuilder.Recolor(src, "wd_top_" + key,
                (8, jacket, shade), G(9, trousers, 0.3f, 0.1f), G(5, trousers, 0.55f, 0.05f), G(6, trousers, 0f, 0.35f), G(15, trousers, 0f, 0.45f),
                (21, "#fdd2b4", "#f0a47f"));
            var legs = WorldArtBuilder.Recolor(src, "wd_legs_" + key, G(15, trousers, 0.05f, 0.3f), G(19, trousers, 0f, 0.45f));
            outfits.Add(new OutfitSet { id = it.Id, top = WorldArtBuilder.TexMat("wd_top_" + key, top), legs = WorldArtBuilder.TexMat("wd_legs_" + key, legs) });
        }
        a.outfits = outfits.ToArray();

        var hair = new System.Collections.Generic.List<HairSet>();
        foreach (ShopItem it in Shop.World.Where(i => i.Slot == Shop.Hair))
        {
            string key = it.Id.Substring(Shop.Hair.Length + 1);
            if (key == "chestnut") { hair.Add(new HairSet { id = it.Id, head = AssetDatabase.LoadAssetAtPath<Material>(WorldArtBuilder.MatDir + "/star_head.mat") }); continue; }
            var tex = WorldArtBuilder.Recolor(src, "wd_head_" + key, G(1, it.Color, 0.22f, 0.3f));
            hair.Add(new HairSet { id = it.Id, head = WorldArtBuilder.TexMat("wd_head_" + key, tex) });
        }
        a.hair = hair.ToArray();

        var baseLit = AssetDatabase.LoadAssetAtPath<Material>(Dir + "/wd_base.mat");
        if (baseLit == null) { baseLit = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(baseLit, Dir + "/wd_base.mat"); }
        a.baseLit = baseLit;
        a.cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        a.sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        a.cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        EditorUtility.SetDirty(a);
        EditorUtility.SetDirty(baseLit);
    }

    // ---------- attachment points ----------

    /// <summary>
    /// riff's nose points toward -z in the Riff prefab at rest (the head chain is Bone.001 to Bone.003, the tail Bone.004 to Bone.007), so his anchors are turned to face his nose:
    /// +z of an anchor is the nose, +y up. The head anchor sits at the head bone's joint plus this offset (in that frame, metres); the neck anchor halfway up the neck, its ring tilted to the neck.
    /// </summary>
    public static readonly Vector3 DogHeadOffset = new Vector3(0f, 0.0f, 0.07f);
    /// <summary>The Star's guitar anchor: this far above her feet (metres), on her centre line, aligned with her.</summary>
    public static readonly Vector3 StarFrontOffset = new Vector3(0f, 0.62f, 0f);

    /// <summary>Finds a bone's rest pose in an instance of the prefab (not saved) and records the anchor's local pose for <see cref="WardrobeAnchors"/>.</summary>
    static AnchorSpec Spec(GameObject root, string bone, string name, Vector3 worldPos, Quaternion worldRot)
    {
        Transform b = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == bone);
        if (b == null) throw new System.Exception("no bone " + bone);
        return new AnchorSpec { bone = bone, name = name, position = b.InverseTransformPoint(worldPos), rotation = Quaternion.Inverse(b.rotation) * worldRot };
    }

    static void AddAnchors(WardrobeAssets a)
    {
        var specs = new System.Collections.Generic.List<AnchorSpec>();
        var s = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WorldArtBuilder.StarPrefab));
        var r = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WorldArtBuilder.RiffPrefab));
        try
        {
            specs.Add(Spec(s, "chest", StarWardrobe.FrontAnchor, s.transform.position + StarFrontOffset, s.transform.rotation));
            Transform Bone(string n) { return r.GetComponentsInChildren<Transform>(true).First(t => t.name == n); }
            Quaternion nose = r.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
            Vector3 j2 = Bone("Bone.002").position, j3 = Bone("Bone.003").position;
            specs.Add(Spec(r, "Bone.003", DogWardrobe.HeadAnchor, j3 + nose * DogHeadOffset, nose));
            Vector3 neckAxis = (j3 - j2).normalized;
            specs.Add(Spec(r, "Bone.002", DogWardrobe.NeckAnchor, Vector3.Lerp(j2, j3, 0.5f), Quaternion.FromToRotation(nose * Vector3.up, neckAxis) * nose));
        }
        finally { Object.DestroyImmediate(s); Object.DestroyImmediate(r); }
        a.anchors = specs.ToArray();
        EditorUtility.SetDirty(a);
    }
}
