// Phase 10B.1 fix: builds the Star's avatar art from the imported CC0 KayKit Rogue model (see ASSETS.md; nothing new is downloaded):
//   - clean body parts: the leftover adventurer gear (belt, buckle, pouch, earring) cut out, the painted-on eyes and brows cut out (the face is a decal now),
//     and the cloth triangles (trunk, sleeves, trousers) given UVs into their own patch of a doubled texture so patterns are painted INTO the cloth (see Fabric),
//   - the face decal mesh (a copy of the front of the head) and the face textures (smile, big smile, wink, freckles and more, drawn here with plain shapes),
//   - the decal material (transparent, lit) so its shader variant is part of the build.
// Re-runnable. It only changes WardrobeAssets.asset and the files under Assets/World/Wardrobe/{Meshes,Faces}.
//
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -executeMethod AvatarArtBuilder.Run
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using WorldTour.World.Wardrobe;

public static class AvatarArtBuilder
{
    const string Dir = "Assets/World/Wardrobe";
    const string AssetPath = Dir + "/Resources/WardrobeAssets.asset";
    const string RogueFbx = "Assets/World/Art/KayKit_Adventurers/Rogue.fbx";

    // the Rogue's palette cells (column + 8 * row): the gear to cut out, the cloth to give canvas UVs, the face
    static readonly int[] BodyGear = { 27, 29, 30 };      // buckle and metal, belt, pouch
    const int ClothCell = 16, TrousersCell = 23, EarringCell = 27, EyeCell = 26, HairCell = 25, SkinCell = 24;

    // the face decal covers this box of the head in the head mesh's own space (x both ways, y from the chin up) and is drawn on a texture with the same proportions
    public const float FaceHalfWidth = 0.48f, FaceY0 = 0.02f, FaceHeight = 0.6f;
    const int FaceW = 256, FaceH = 160;
    static readonly string[] FaceKeys = { "smile", "bigsmile", "wink", "freckles", "happy", "cheeky", "blush", "starry" };

    static readonly StringBuilder Log = new StringBuilder();

    [MenuItem("World Tour/Build avatar art")]
    public static void Run()
    {
        int exit = 0;
        try { Build(); }
        catch (Exception e) { Debug.LogError("AvatarArtBuilder failed: " + e); exit = 1; }
        Debug.Log("AvatarArtBuilder:\n" + Log);
        if (Application.isBatchMode) EditorApplication.Exit(exit);
    }

    /// <summary>Pushes a point of the buckle or pouch back onto the waist (an ellipse of the torso's size).</summary>
    static Vector3 Flatten(Vector3 p)
    {
        float e = (p.x / 0.35f) * (p.x / 0.35f) + (p.z / 0.295f) * (p.z / 0.295f);
        if (e > 1f) { float s = 1f / Mathf.Sqrt(e); p.x *= s; p.z *= s; }
        return p;
    }

    static int Cell(Vector2 uv) { return Mathf.Min(7, (int)(uv.x * 8f)) + 8 * Mathf.Min(3, (int)(uv.y * 4f)); }

    static void Build()
    {
        var a = AssetDatabase.LoadAssetAtPath<WardrobeAssets>(AssetPath);
        if (a == null || a.palette == null) throw new Exception("run WardrobeBuilder first (no WardrobeAssets / palette)");
        Directory.CreateDirectory(Dir + "/Meshes");
        Directory.CreateDirectory(Dir + "/Faces");
        int w = a.palette.width, h = a.palette.height;
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(RogueFbx);
        var inst = (GameObject)UnityEngine.Object.Instantiate(fbx);
        try
        {
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); inst.transform.localScale = Vector3.one;
            var list = new List<NamedMesh>();
            Mesh headClean = null; SkinnedMeshRenderer head = null;
            foreach (var r in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!(r.name.Contains("_Body") || r.name.Contains("_Arm") || r.name.Contains("_Head") || r.name.Contains("_Leg"))) continue;
                Mesh m = CleanPart(r, inst.transform, w, h);
                string path = Dir + "/Meshes/star_" + r.name + ".asset";
                list.Add(new NamedMesh { name = r.name, mesh = SaveMesh(m, path) });
                if (r.name.Contains("_Head")) { head = r; headClean = m; }
            }
            a.bodyMeshes = list.ToArray();
            if (head == null) throw new Exception("no head");
            a.faceMesh = SaveMesh(FaceDecal(head, inst.transform), Dir + "/Meshes/star_face_decal.asset");
        }
        finally { UnityEngine.Object.DestroyImmediate(inst); }

        Log.AppendLine("  builtin bounds: sphere " + a.sphere.bounds.size + " cube " + a.cube.bounds.size + " cylinder " + a.cylinder.bounds.size);
        a.faceMat = MakeDecalMaterial(a);
        var faces = new List<FaceTex>();
        foreach (string key in FaceKeys)
        {
            string p = Dir + "/Faces/face_" + key + ".png";
            WriteFace(key, p);
            faces.Add(new FaceTex { id = key, tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p) });
        }
        a.faces = faces.ToArray();
        CrewFaces(a);
        EditorUtility.SetDirty(a);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static Mesh SaveMesh(Mesh m, string path)
    {
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old != null) { EditorUtility.CopySerialized(m, old); UnityEngine.Object.DestroyImmediate(m); EditorUtility.SetDirty(old); return old; }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // ============================================================== body parts

    static Mesh CleanPart(SkinnedMeshRenderer r, Transform root, int w, int h)
    {
        Mesh src = r.sharedMesh;
        if (src.subMeshCount != 1) throw new Exception(r.name + " has " + src.subMeshCount + " submeshes");
        Vector3[] sv = src.vertices, sn = src.normals; Vector2[] suv = src.uv; BoneWeight[] sw = src.boneWeights; int[] st = src.triangles;
        var v = new List<Vector3>(sv); var n = new List<Vector3>(sn); var w2 = new List<BoneWeight>(sw);
        var uv = new List<Vector2>(sv.Length);
        foreach (Vector2 u in suv) uv.Add(u * 0.5f);              // the palette moves to the lower left quarter of the doubled texture
        var tris = new List<int>();
        var drop = new HashSet<int>();
        bool head = r.name.Contains("_Head"), body = r.name.Contains("_Body"), arm = r.name.Contains("_Arm"), leg = r.name.Contains("_Leg");
        if (head) drop = HeadFaceTriangles(r, src, st, suv, sv);
        int cut = 0, cloth = 0;
        for (int k = 0; k < st.Length; k += 3)
        {
            int tri = k / 3;
            int c = Cell(suv[st[k]]);
            if (drop.Contains(tri)) { cut++; continue; }
            if (head && c == EarringCell) { cut++; continue; }
            if (body && c == 30 && (sv[st[k]].y + sv[st[k + 1]].y + sv[st[k + 2]].y) / 3f > 0.7f && Mathf.Abs(sv[st[k]].x + sv[st[k + 1]].x + sv[st[k + 2]].x) / 3f > 0.14f) { cut++; continue; }   // the strap across the chest (the tab in the middle is the window of the jacket and stays, as cloth)
            Fabric.Mode mode = Fabric.Mode.Body; float cx = 0f; bool isCloth = false;
            bool gear = body && BodyGear.Contains(c);   // the belt band is the waist of the jacket: it becomes cloth, and the buckle and pouch are flattened into it
            if (body && (c == ClothCell || gear)) { isCloth = true; mode = Fabric.Mode.Body; }
            else if (arm && c == ClothCell) { isCloth = true; mode = Fabric.Mode.Arm; }
            else if (leg && c == TrousersCell) { isCloth = true; mode = Fabric.Mode.Leg; }
            if (!isCloth) { tris.Add(st[k]); tris.Add(st[k + 1]); tris.Add(st[k + 2]); continue; }
            cloth++;
            var m3 = new Vector3[3]; var u = new float[3]; var vv = new float[3];
            Vector3 centre = Vector3.zero;
            var p3 = new Vector3[3];
            for (int j = 0; j < 3; j++) { p3[j] = gear && sv[st[k + j]].y < 0.68f ? Flatten(sv[st[k + j]]) : sv[st[k + j]]; m3[j] = root.InverseTransformPoint(r.transform.TransformPoint(p3[j])); centre += m3[j] / 3f; }
            if (leg) cx = centre.x < 0f ? -0.17f : 0.17f;
            for (int j = 0; j < 3; j++) Fabric.Project(mode, m3[j], cx, out u[j], out vv[j]);
            Fabric.Unwrap(mode, ref u[0], ref u[1], ref u[2]);
            for (int j = 0; j < 3; j++)
            {
                int i = st[k + j];
                tris.Add(v.Count);
                v.Add(p3[j]); n.Add(gear && sv[i].y < 0.68f ? Vector3.Lerp(sn[i], (new Vector3(p3[j].x / 0.35f, 0f, p3[j].z / 0.295f)).normalized, 0.8f).normalized : sn[i]); w2.Add(sw[i]); uv.Add(Fabric.CanvasUv(mode, u[j], vv[j], w, h));
            }
        }
        var m = new Mesh { name = "star_" + r.name };
        m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.boneWeights = w2.ToArray(); m.bindposes = src.bindposes;
        m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        Log.AppendLine("  " + r.name + ": cut " + cut + " triangles, " + cloth + " cloth triangles on the canvas, " + tris.Count / 3 + " left");
        return m;
    }

    /// <summary>The head triangles that are the painted-on face: the eye blocks and the little islands of the hair swatch high on the front (the brows).</summary>
    static HashSet<int> HeadFaceTriangles(SkinnedMeshRenderer r, Mesh src, int[] st, Vector2[] uv, Vector3[] sv)
    {
        var drop = new HashSet<int>();
        // union the hair-swatch triangles that touch
        var parent = new int[sv.Length];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;
        int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        var weld = new Dictionary<string, int>();
        string Key(Vector3 p) { return Mathf.RoundToInt(p.x * 1000f) + "," + Mathf.RoundToInt(p.y * 1000f) + "," + Mathf.RoundToInt(p.z * 1000f); }
        for (int k = 0; k < st.Length; k += 3)
        {
            if (Cell(uv[st[k]]) != HairCell) continue;
            for (int j = 0; j < 3; j++)
            {
                string key = Key(sv[st[k + j]]); int other;
                if (weld.TryGetValue(key, out other)) parent[Find(st[k + j])] = Find(other); else weld[key] = st[k + j];
            }
            parent[Find(st[k + 1])] = Find(st[k]); parent[Find(st[k + 2])] = Find(st[k]);
        }
        var bounds = new Dictionary<int, Bounds>();
        for (int k = 0; k < st.Length; k += 3)
        {
            if (Cell(uv[st[k]]) != HairCell) continue;
            int root = Find(st[k]);
            for (int j = 0; j < 3; j++) { if (!bounds.ContainsKey(root)) bounds[root] = new Bounds(sv[st[k + j]], Vector3.zero); else { var b = bounds[root]; b.Encapsulate(sv[st[k + j]]); bounds[root] = b; } }
        }
        var brows = new HashSet<int>();
        foreach (var kv in bounds)
        {
            Bounds b = kv.Value;
            bool brow = b.size.x <= 0.34f && b.size.y <= 0.22f && b.center.z > 0.25f && b.center.y > 0.4f && b.center.y < 0.66f && Mathf.Abs(b.center.x) > 0.06f;
            if (b.center.z > 0.2f) Log.AppendLine("    hair island centre " + b.center.ToString("F2") + " size " + b.size.ToString("F2") + (brow ? "  -> brow" : ""));
            if (brow) brows.Add(kv.Key);
        }
        for (int k = 0; k < st.Length; k += 3)
        {
            int c = Cell(uv[st[k]]);
            if (c == EyeCell) drop.Add(k / 3);
            else if (c == HairCell && brows.Contains(Find(st[k]))) drop.Add(k / 3);
        }
        return drop;
    }

    // ============================================================== the crew's faces

    /// <summary>
    /// The crew are other KayKit bodies (Knight, Mage) whose faces are painted-on geometry. Their eyes and brows are cut out and they wear the same transparent smile decal as the Star.
    /// </summary>
    static void CrewFaces(WardrobeAssets a)
    {
        // the crew's painted-on eyes and brows are cut out of their heads and each gets the same transparent smile decal as the Star (a second skinned mesh on the same bones)
        string matPath = Dir + "/face_crew_smile.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { mat = new Material(a.faceMat); AssetDatabase.CreateAsset(mat, matPath); }
        mat.CopyPropertiesFromMaterial(a.faceMat);
        mat.SetTexture("_BaseMap", a.FaceTexture("smile"));
        mat.renderQueue = 3000;
        EditorUtility.SetDirty(mat);
        var clean = new Dictionary<string, Mesh>(); var decal = new Dictionary<string, Mesh>();
        string[] crewModels = { "Knight", "Mage", "Knight" };
        for (int i = 0; i < WorldArtBuilder.CrewPrefabs.Length; i++)
        {
            string model = crewModels[i];
            if (!clean.ContainsKey(model))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/World/Art/KayKit_Adventurers/" + model + ".fbx");
                SkinnedMeshRenderer fh = go.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name.Contains("_Head"));
                clean[model] = SaveMesh(CrewHead(fh), Dir + "/Meshes/crew_head_" + model + ".asset");
                decal[model] = SaveMesh(SkinnedDecal(fh), Dir + "/Meshes/crew_face_decal_" + model + ".asset");
            }
            string pp = WorldArtBuilder.CrewPrefabs[i];
            GameObject root = PrefabUtility.LoadPrefabContents(pp);
            try
            {
                SkinnedMeshRenderer head = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name.Contains("_Head"));
                head.sharedMesh = clean[model];
                foreach (var old in root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name == "FaceDecal").ToArray()) UnityEngine.Object.DestroyImmediate(old.gameObject);
                var g = new GameObject("FaceDecal");
                g.transform.SetParent(head.transform.parent, false);
                g.transform.localPosition = head.transform.localPosition; g.transform.localRotation = head.transform.localRotation; g.transform.localScale = head.transform.localScale;
                var d = g.AddComponent<SkinnedMeshRenderer>();
                d.sharedMesh = decal[model]; d.bones = head.bones; d.rootBone = head.rootBone; d.localBounds = head.localBounds;
                d.sharedMaterial = mat;
                d.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; d.receiveShadows = false;
                PrefabUtility.SaveAsPrefabAsset(root, pp);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        var bake = typeof(WorldArtBuilder).GetMethod("MakeCrowd", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (bake != null) bake.Invoke(null, null);
        Log.AppendLine("  crew heads cleaned and given the smile decal; crowd baked again (crowd keeps the geometry faces)");
    }

    /// <summary>The original head without its eye blocks and brow islands (same UVs, same bones).</summary>
    static Mesh CrewHead(SkinnedMeshRenderer r)
    {
        Mesh src = r.sharedMesh;
        int[] st = src.triangles; Vector2[] suv = src.uv; Vector3[] sv = src.vertices;
        HashSet<int> drop = HeadFaceTriangles(r, src, st, suv, sv);
        var tris = new List<int>();
        for (int k = 0; k < st.Length; k += 3) if (!drop.Contains(k / 3)) { tris.Add(st[k]); tris.Add(st[k + 1]); tris.Add(st[k + 2]); }
        var m = new Mesh { name = "crew_head" };
        m.SetVertices(sv); m.SetNormals(src.normals); m.SetUVs(0, suv); m.boneWeights = src.boneWeights; m.bindposes = src.bindposes;
        m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        Log.AppendLine("  crew head " + r.name + ": cut " + drop.Count + " face triangles");
        return m;
    }

    /// <summary>A skinned copy of the front of the head (skin triangles), pushed out a little, UVs a front projection, weights copied: the same decal as the Star's, but on the head's own bones.</summary>
    static Mesh SkinnedDecal(SkinnedMeshRenderer r)
    {
        Mesh src = r.sharedMesh;
        Vector3[] sv = src.vertices, sn = src.normals; Vector2[] suv = src.uv; BoneWeight[] sw = src.boneWeights; int[] st = src.triangles;
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var w = new List<BoneWeight>(); var tris = new List<int>();
        for (int k = 0; k < st.Length; k += 3)
        {
            if (Cell(suv[st[k]]) != SkinCell) continue;
            Vector3 cen = (sv[st[k]] + sv[st[k + 1]] + sv[st[k + 2]]) / 3f;
            Vector3 vn = (sn[st[k]] + sn[st[k + 1]] + sn[st[k + 2]]).normalized;
            if (cen.z < 0.18f || vn.z < 0.25f) continue;
            if (Mathf.Abs(cen.x) > FaceHalfWidth + 0.05f || cen.y < FaceY0 - 0.04f || cen.y > FaceY0 + FaceHeight + 0.04f) continue;
            for (int j = 0; j < 3; j++)
            {
                int i = st[k + j];
                tris.Add(v.Count);
                v.Add(sv[i] + sn[i].normalized * 0.006f); n.Add(sn[i]); w.Add(sw[i]);
                uv.Add(new Vector2((FaceHalfWidth - sv[i].x) / (2f * FaceHalfWidth), (sv[i].y - FaceY0) / FaceHeight));
            }
        }
        var m = new Mesh { name = "crew_face_decal" };
        m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.boneWeights = w.ToArray(); m.bindposes = src.bindposes;
        m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        return m;
    }

    // ============================================================== the face decal mesh

    static Mesh FaceDecal(SkinnedMeshRenderer head, Transform root)
    {
        Mesh src = head.sharedMesh;
        Vector3[] sv = src.vertices, sn = src.normals; Vector2[] suv = src.uv; int[] st = src.triangles;
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tris = new List<int>();
        Matrix4x4 toModel = root.worldToLocalMatrix * head.transform.localToWorldMatrix;
        Log.AppendLine("  head mesh to model: eye height " + toModel.MultiplyPoint3x4(new Vector3(0f, 0.39f, 0.4f)).ToString("F3") + " (mesh 0, 0.39, 0.4)");
        int used = 0;
        for (int k = 0; k < st.Length; k += 3)
        {
            if (Cell(suv[st[k]]) != SkinCell) continue;   // the skin of the face (the eye blocks and brows are cut out of the head, the decal draws them)
            Vector3 cen = (sv[st[k]] + sv[st[k + 1]] + sv[st[k + 2]]) / 3f;
            Vector3 vn = (sn[st[k]] + sn[st[k + 1]] + sn[st[k + 2]]).normalized;
            if (cen.z < 0.18f || vn.z < 0.25f) continue;
            if (Mathf.Abs(cen.x) > FaceHalfWidth + 0.05f || cen.y < FaceY0 - 0.04f || cen.y > FaceY0 + FaceHeight + 0.04f) continue;
            used++;
            for (int j = 0; j < 3; j++)
            {
                int i = st[k + j];
                Vector3 p = toModel.MultiplyPoint3x4(sv[i]);
                Vector3 pn = toModel.MultiplyVector(sn[i]).normalized;
                p += pn * 0.006f;
                tris.Add(v.Count);
                v.Add(p); n.Add(pn);
                uv.Add(new Vector2((FaceHalfWidth - sv[i].x) / (2f * FaceHalfWidth), (sv[i].y - FaceY0) / FaceHeight));
            }
        }
        Log.AppendLine("  face decal from " + used + " head triangles");
        var m = new Mesh { name = "star_face_decal" };
        m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        return m;
    }

    static Material MakeDecalMaterial(WardrobeAssets a)
    {
        string path = Dir + "/face_decal.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_AlphaClip", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_ReceiveShadows", 0f);
        m.SetFloat("_Smoothness", 0.15f);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_SpecularHighlights", 0f);
        m.SetFloat("_EnvironmentReflections", 0f);
        m.SetColor("_BaseColor", Color.white);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.EnableKeyword("_RECEIVE_SHADOWS_OFF");
        m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        m.DisableKeyword("_ALPHATEST_ON");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = 3000;
        m.SetShaderPassEnabled("ShadowCaster", false);
        m.SetShaderPassEnabled("DepthOnly", false);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ============================================================== the faces

    sealed class Face
    {
        public readonly Color[] Px = new Color[FaceW * FaceH];

        public void Over(Func<float, float, float> sdf, Color c)
        {
            float ppu = FaceW / (2f * FaceHalfWidth);
            for (int y = 0; y < FaceH; y++)
                for (int x = 0; x < FaceW; x++)
                {
                    float fx = (x + 0.5f) / ppu - FaceHalfWidth, fy = (y + 0.5f) / ppu;
                    float a = Mathf.Clamp01(0.5f - sdf(fx, fy) * ppu) * c.a;
                    if (a <= 0f) continue;
                    Color o = Px[y * FaceW + x];
                    float outA = a + o.a * (1f - a);
                    Px[y * FaceW + x] = new Color((c.r * a + o.r * o.a * (1f - a)) / outA, (c.g * a + o.g * o.a * (1f - a)) / outA, (c.b * a + o.b * o.a * (1f - a)) / outA, outA);
                }
        }
    }

    // signed distances in face units (negative inside)
    static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
    {
        float dx = (x - cx) / rx, dy = (y - cy) / ry;
        return (Mathf.Sqrt(dx * dx + dy * dy) - 1f) * Mathf.Min(rx, ry);
    }

    static float Seg(float x, float y, Vector2 a, Vector2 b)
    {
        Vector2 p = new Vector2(x, y), ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
        return (p - (a + ab * t)).magnitude;
    }

    /// <summary>A stroke of width <paramref name="r"/> * 2 along a curve given as a function of t (0 to 1), as short segments.</summary>
    static Func<float, float, float> Stroke(Func<float, Vector2> f, float r, int steps = 24)
    {
        var pts = new Vector2[steps + 1];
        for (int i = 0; i <= steps; i++) pts[i] = f(i / (float)steps);
        return (x, y) => { float d = float.MaxValue; for (int i = 0; i < steps; i++) d = Mathf.Min(d, Seg(x, y, pts[i], pts[i + 1])); return d - r; };
    }

    static float Star5(float x, float y, float cx, float cy, float outer, float inner)
    {
        float dx = x - cx, dy = y - cy, d = Mathf.Sqrt(dx * dx + dy * dy);
        float ang = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg; if (ang < 0f) ang += 360f;
        float t = Mathf.Abs((ang % 72f) - 36f) / 36f;
        return d - Mathf.Lerp(inner, outer, t);
    }

    static readonly Color Ink = new Color(0.17f, 0.11f, 0.2f), BrowC = new Color(0.3f, 0.2f, 0.16f), LipC = new Color(0.62f, 0.2f, 0.27f), MouthC = new Color(0.36f, 0.09f, 0.16f),
        TongueC = new Color(1f, 0.5f, 0.6f), CheekC = new Color(1f, 0.5f, 0.58f, 0.32f), FreckleC = new Color(0.62f, 0.34f, 0.2f, 0.8f), ShineC = Color.white, Gold = new Color(1f, 0.82f, 0.2f);

    const float EyeX = 0.165f, EyeY = 0.36f, BrowY = 0.5f, MouthY = 0.17f;

    static void Brows(Face f, bool wink = false)
    {
        // soft level arches that rise a touch at the outer ends: a friendly look, never a frown
        foreach (int s in new[] { -1, 1 })
        {
            float cx = EyeX * s;
            float lift = (wink && s == 1) ? 0.03f : 0f;
            f.Over(Stroke(t => new Vector2(cx + (t - 0.5f) * 0.15f, BrowY + lift + 0.016f * Mathf.Sin(t * Mathf.PI) + (s * (t - 0.5f) * 0.01f)), 0.0075f, 14), BrowC);
        }
    }

    static void EyeOpen(Face f, float cx)
    {
        f.Over((x, y) => Ellipse(x, y, cx, EyeY, 0.052f, 0.066f), Ink);
        f.Over((x, y) => Ellipse(x, y, cx + 0.016f, EyeY + 0.026f, 0.017f, 0.02f), ShineC);
        f.Over((x, y) => Ellipse(x, y, cx - 0.014f, EyeY - 0.024f, 0.009f, 0.009f), new Color(1f, 1f, 1f, 0.7f));
    }

    static void EyeArch(Face f, float cx, float thick = 0.0105f)
    {
        f.Over(Stroke(t => new Vector2(cx + (t - 0.5f) * 0.13f, EyeY - 0.02f + 0.05f * Mathf.Sin(t * Mathf.PI)), thick, 18), Ink);
    }

    static void Smile(Face f, float halfWidth = 0.09f, float depth = 0.04f)
    {
        f.Over(Stroke(t => { float x = (t * 2f - 1f) * halfWidth; return new Vector2(x, MouthY + depth * (1f - (x / halfWidth) * (x / halfWidth)) * -1f + depth); }, 0.0085f, 20), LipC);
    }

    static void BigSmile(Face f)
    {
        float hw = 0.115f, dep = 0.075f, top = MouthY + 0.045f;
        // an open mouth: a flat top and a round bottom
        f.Over((x, y) => Mathf.Max(Ellipse(x, y, 0f, top, hw, dep), y - top), MouthC);
        f.Over((x, y) => Mathf.Max(Ellipse(x, y, 0f, top, hw * 0.98f, dep * 0.98f), Mathf.Max(y - top, top - 0.026f - y)), new Color(1f, 1f, 1f, 0.95f));   // the teeth along the top
        f.Over((x, y) => Mathf.Max(Ellipse(x, y, 0f, top - dep * 0.78f, 0.062f, 0.036f), Mathf.Max(y - (top - 0.03f), -1f)), TongueC);
        f.Over(Stroke(t => { float x = (t * 2f - 1f) * (hw + 0.004f); return new Vector2(x, top); }, 0.005f, 6), LipC);
    }

    static void Cheeks(Face f, float strength = 1f)
    {
        foreach (int s in new[] { -1, 1 })
        {
            Color c = CheekC; c.a *= strength;
            f.Over((x, y) => Ellipse(x, y, 0.28f * s, 0.24f, 0.07f, 0.045f), c);
        }
    }

    static Texture2D Draw(string key)
    {
        var f = new Face();
        switch (key)
        {
            case "smile":
                Brows(f); EyeOpen(f, -EyeX); EyeOpen(f, EyeX); Cheeks(f, 0.7f); Smile(f);
                break;
            case "bigsmile":
                Brows(f); EyeOpen(f, -EyeX); EyeOpen(f, EyeX); Cheeks(f, 0.9f); BigSmile(f);
                break;
            case "wink":
                Brows(f, true); EyeOpen(f, -EyeX); EyeArch(f, EyeX); Cheeks(f, 0.7f); Smile(f, 0.1f, 0.045f);
                // a little lash flick on the winking eye
                f.Over(Stroke(t => new Vector2(EyeX + 0.07f + t * 0.025f, EyeY + 0.0f + t * 0.03f), 0.006f, 4), Ink);
                break;
            case "freckles":
                Brows(f); EyeOpen(f, -EyeX); EyeOpen(f, EyeX); Cheeks(f, 0.5f); Smile(f);
                foreach (int s in new[] { -1, 1 })
                {
                    Vector2[] dots = { new Vector2(0.19f, 0.27f), new Vector2(0.25f, 0.29f), new Vector2(0.15f, 0.235f), new Vector2(0.23f, 0.235f), new Vector2(0.29f, 0.255f), new Vector2(0.2f, 0.205f) };
                    foreach (Vector2 d in dots) { Vector2 dd = new Vector2(d.x * s, d.y); f.Over((x, y) => Ellipse(x, y, dd.x, dd.y, 0.0125f, 0.0125f), FreckleC); }
                }
                break;
            case "happy":
                Brows(f); EyeArch(f, -EyeX, 0.012f); EyeArch(f, EyeX, 0.012f); Cheeks(f, 1f); BigSmile(f);
                break;
            case "cheeky":
                Brows(f, true); EyeOpen(f, -EyeX); EyeArch(f, EyeX); Cheeks(f, 0.8f);
                f.Over((x, y) => Mathf.Max(Ellipse(x, y, 0.05f, MouthY - 0.03f, 0.034f, 0.048f), 0f), TongueC);
                f.Over(Stroke(t => { float x = (t * 2f - 1f) * 0.1f; return new Vector2(x, MouthY + 0.03f - 0.034f * (1f - (x / 0.1f) * (x / 0.1f)) + x * 0.04f); }, 0.0085f, 20), LipC);
                f.Over(Stroke(t => new Vector2(0.05f, MouthY - 0.012f - t * 0.06f), 0.0035f, 4), new Color(0.85f, 0.3f, 0.42f));
                break;
            case "blush":
                Brows(f); EyeOpen(f, -EyeX); EyeOpen(f, EyeX); Cheeks(f, 2.2f); Smile(f, 0.08f, 0.035f);
                break;
            case "starry":
                Brows(f);
                foreach (int s in new[] { -1, 1 })
                {
                    float cx = EyeX * s;
                    f.Over((x, y) => Star5(x, y, cx, EyeY, 0.085f, 0.04f), Ink);
                    f.Over((x, y) => Star5(x, y, cx, EyeY, 0.066f, 0.03f), Gold);
                }
                Cheeks(f, 0.8f); BigSmile(f);
                break;
        }
        // let the transparent pixels carry the colour of the nearest drawn ones, so the texture's bilinear edge has no dark fringe
        var px = f.Px;
        for (int pass = 0; pass < 3; pass++)
        {
            var next = (Color[])px.Clone();
            for (int y = 0; y < FaceH; y++)
                for (int x = 0; x < FaceW; x++)
                {
                    if (px[y * FaceW + x].a > 0f) continue;
                    foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= FaceW || ny >= FaceH) continue;
                        Color q = px[ny * FaceW + nx];
                        if (q.a > 0f) { next[y * FaceW + x] = new Color(q.r, q.g, q.b, 0f); break; }
                    }
                }
            px = next;
        }
        var t2 = new Texture2D(FaceW, FaceH, TextureFormat.RGBA32, false);
        var c32 = new Color32[px.Length];
        for (int i = 0; i < px.Length; i++) c32[i] = new Color(px[i].r, px[i].g, px[i].b, f.Px[i].a);
        t2.SetPixels32(c32);
        t2.Apply();
        return t2;
    }

    static void WriteFace(string key, string path)
    {
        Texture2D t = Draw(key);
        File.WriteAllBytes(path, t.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Default;
        imp.alphaSource = TextureImporterAlphaSource.FromInput;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.filterMode = FilterMode.Bilinear;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.sRGBTexture = true;
        imp.SaveAndReimport();
    }
}
