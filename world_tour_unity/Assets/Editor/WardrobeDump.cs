// Phase 8.3 scratch: measures riff's head and neck and the Star's torso (rest pose) so the accessories can be sized to them.
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class WardrobeDump
{
    public static void Run()
    {
        var riff = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WorldArtBuilder.RiffPrefab));
        Transform Bone(GameObject g, string n) { return g.GetComponentsInChildren<Transform>(true).First(t => t.name == n); }
        Quaternion nose = riff.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
        Vector3 j2 = Bone(riff, "Bone.002").position, j3 = Bone(riff, "Bone.003").position;
        Vector3 headC = j3 + nose * WardrobeBuilder.DogHeadOffset;
        var smr = riff.GetComponentInChildren<SkinnedMeshRenderer>();
        var baked = new Mesh();
        smr.BakeMesh(baked, true);
        Bounds head = new Bounds(), neck = new Bounds(); bool h0 = false, n0 = false;
        foreach (Vector3 v in baked.vertices)
        {
            Vector3 w = smr.transform.TransformPoint(v);
            Vector3 l = Quaternion.Inverse(nose) * (w - headC);   // x across, y up, z toward the nose
            if (l.z > -0.02f && l.y > -0.25f) { if (!h0) { head = new Bounds(l, Vector3.zero); h0 = true; } else head.Encapsulate(l); }
            Vector3 ln = Quaternion.Inverse(nose) * (w - Vector3.Lerp(j2, j3, 0.5f));
            if (Mathf.Abs(ln.y) < 0.06f && ln.z < 0.15f && ln.z > -0.3f) { if (!n0) { neck = new Bounds(ln, Vector3.zero); n0 = true; } else neck.Encapsulate(ln); }
        }
        Debug.Log("WT_DUMP dog head (anchor frame, +z nose): min=" + head.min.ToString("F3") + " max=" + head.max.ToString("F3") + "  neck slab: min=" + neck.min.ToString("F3") + " max=" + neck.max.ToString("F3")
            + "  j2=" + j2.ToString("F3") + " j3=" + j3.ToString("F3") + " smrScale=" + smr.transform.lossyScale);
        Object.DestroyImmediate(riff);

        var star = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WorldArtBuilder.StarPrefab));
        var sb = new Bounds(); bool s0 = false;
        foreach (var r in star.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!r.name.Contains("_Body")) continue;
            var m = new Mesh(); r.BakeMesh(m, true);
            foreach (Vector3 v in m.vertices) { Vector3 w = r.transform.TransformPoint(v); if (w.y > 0.35f && w.y < 1.0f) { if (!s0) { sb = new Bounds(w, Vector3.zero); s0 = true; } else sb.Encapsulate(w); } }
        }
        Debug.Log("WT_DUMP star torso y .35-1.0: min=" + sb.min.ToString("F3") + " max=" + sb.max.ToString("F3"));
        Object.DestroyImmediate(star);
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
