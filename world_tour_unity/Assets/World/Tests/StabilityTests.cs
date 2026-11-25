using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WorldTour.World.Tests
{
    /// <summary>Phase 8.4 stability: the occluder fade is smooth and never jumps, and no two flat ground pieces share a height where they overlap (that shimmers as z-fighting).</summary>
    public class StabilityTests
    {
        const string Scenes = "Assets/World/Scenes";

        [Test]
        public void Fade_TakesAtLeastAQuarterSecond_AndNeverJumps()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var bank = ScriptableObject.CreateInstance<FadeBank>();
            try
            {
                var r = go.GetComponent<MeshRenderer>();
                var src = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                var twin = new Material(src);
                bank.sources = new[] { src };
                bank.twins = new[] { twin };
                r.sharedMaterial = src;
                var fade = new OccluderFade(r, bank);
                Assert.IsTrue(fade.HasTwin);
                Assert.GreaterOrEqual(OccluderFade.Seconds, 0.25f);

                fade.Set(false);
                float dt = 1f / 60f, last = fade.Alpha, t = 0f, biggest = 0f;
                while (fade.Busy && t < 3f) { fade.Step(dt); t += dt; biggest = Mathf.Max(biggest, Mathf.Abs(last - fade.Alpha)); Assert.LessOrEqual(fade.Alpha, last + 1e-5f); last = fade.Alpha; }
                Assert.GreaterOrEqual(t, 0.25f - dt, "the fade out lasts at least 0.25 s");
                Assert.LessOrEqual(biggest, dt / (0.25f - dt) , "no frame changes the alpha by more than a 0.25 s fade would");
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly, r.shadowCastingMode, "gone = shadows only");
                Assert.IsTrue(fade.Applied);

                // turn round half way: it continues from where it is, with no jump
                fade.Set(true);
                for (int i = 0; i < 6; i++) fade.Step(dt);
                float mid = fade.Alpha;
                fade.Set(false);
                fade.Step(dt);
                Assert.Less(Mathf.Abs(fade.Alpha - mid), 0.2f, "reversing does not jump");
                fade.Set(true);
                t = 0f; while (fade.Busy && t < 3f) { fade.Step(dt); t += dt; }
                Assert.AreEqual(1f, fade.Alpha);
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.On, r.shadowCastingMode);
                Assert.AreSame(src, r.sharedMaterial, "the original material is back");
                Assert.IsFalse(fade.Applied);
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(bank); }
        }

        static bool IsOpaque(Material m) { return m != null && (!m.HasProperty("_Surface") || m.GetFloat("_Surface") < 0.5f); }

        /// <summary>The up-facing triangles of a flat piece in world space: (corner, corner, corner) with their height.</summary>
        static List<Vector3[]> TopTriangles(MeshRenderer r)
        {
            var list = new List<Vector3[]>();
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return list;
            Mesh mesh = mf.sharedMesh;
            Vector3[] v = mesh.vertices; int[] t = mesh.triangles;
            Matrix4x4 m = r.transform.localToWorldMatrix;
            for (int i = 0; i + 2 < t.Length; i += 3)
            {
                Vector3 a = m.MultiplyPoint3x4(v[t[i]]), b = m.MultiplyPoint3x4(v[t[i + 1]]), c = m.MultiplyPoint3x4(v[t[i + 2]]);
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                if (Mathf.Abs(n.y) > 0.9f) list.Add(new[] { a, b, c });
            }
            return list;
        }

        static bool Inside(Vector3[] tri, float x, float z, out float y)
        {
            float d = (tri[1].z - tri[2].z) * (tri[0].x - tri[2].x) + (tri[2].x - tri[1].x) * (tri[0].z - tri[2].z);
            y = 0f;
            if (Mathf.Abs(d) < 1e-9f) return false;
            float l1 = ((tri[1].z - tri[2].z) * (x - tri[2].x) + (tri[2].x - tri[1].x) * (z - tri[2].z)) / d;
            float l2 = ((tri[2].z - tri[0].z) * (x - tri[2].x) + (tri[0].x - tri[2].x) * (z - tri[2].z)) / d;
            float l3 = 1f - l1 - l2;
            if (l1 < 0f || l2 < 0f || l3 < 0f) return false;
            y = l1 * tri[0].y + l2 * tri[1].y + l3 * tri[2].y;
            return true;
        }

        [Test]
        public void EveryPlace_NoTwoFlatGroundPiecesShareAHeight()
        {
            var report = new List<string>();
            foreach (string file in Directory.GetFiles(Scenes, "Place_*.unity"))
            {
                EditorSceneManager.OpenScene(file, OpenSceneMode.Single);
                string place = Path.GetFileNameWithoutExtension(file);
                var flat = new List<MeshRenderer>();
                var tris = new Dictionary<MeshRenderer, List<Vector3[]>>();
                foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (!r.enabled || r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;
                    Bounds b = r.bounds;
                    if (b.size.y > 0.1f || b.min.y > 0.5f || b.size.x < 0.2f || b.size.z < 0.2f) continue;
                    if (!IsOpaque(r.sharedMaterial)) continue;
                    var t = TopTriangles(r);
                    if (t.Count == 0) continue;
                    flat.Add(r); tris[r] = t;
                }
                for (int i = 0; i < flat.Count; i++)
                    for (int j = i + 1; j < flat.Count; j++)
                    {
                        Bounds a = flat[i].bounds, b = flat[j].bounds;
                        float x0 = Mathf.Max(a.min.x, b.min.x), x1 = Mathf.Min(a.max.x, b.max.x), z0 = Mathf.Max(a.min.z, b.min.z), z1 = Mathf.Min(a.max.z, b.max.z);
                        if (x1 - x0 <= 0.05f || z1 - z0 <= 0.05f) continue;
                        // sample the shared box: a point in an up-facing triangle of both, within 12 mm in height, is two surfaces drawn at one height
                        int bad = 0; float yy = 0f;
                        for (float x = x0 + 0.025f; x < x1; x += 0.05f)
                            for (float z = z0 + 0.025f; z < z1; z += 0.05f)
                            {
                                float ya = 0f, yb = 0f; bool ina = false, inb = false;
                                foreach (var t in tris[flat[i]]) if (Inside(t, x, z, out ya)) { ina = true; break; }
                                if (!ina) continue;
                                foreach (var t in tris[flat[j]]) if (Inside(t, x, z, out yb)) { inb = true; break; }
                                if (inb && Mathf.Abs(ya - yb) < 0.012f) { bad++; yy = ya; }
                            }
                        if (bad * 0.0025f > 0.2f) report.Add(place + ": " + flat[i].name + " and " + flat[j].name + " overlap by " + (bad * 0.0025f).ToString("0.00") + " m2 at height " + yy.ToString("0.000"));
                    }
            }
            Assert.IsEmpty(report, string.Join("\n", report));
        }

        [Test]
        public void FadeBankAsset_AndStarRiffPrefabs_HaveNoMissingScripts()
        {
            // a class that shares its file with another binds to nothing (the asset or component is saved as a missing script), so the fade and the height lift would be inert
            var bank = AssetDatabase.LoadAssetAtPath<FadeBank>("Assets/World/Gameplay/Resources/FadeBank.asset");
            Assert.IsNotNull(bank, "FadeBank.asset loads as a FadeBank");
            Assert.Greater(bank.sources.Length, 0, "the bank has materials");
            foreach (string path in new[] { "Assets/World/Prefabs/Star.prefab", "Assets/World/Prefabs/Riff.prefab" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, path);
                int missing = 0;
                foreach (var t in prefab.GetComponentsInChildren<Transform>(true)) missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                Assert.AreEqual(0, missing, path + " has missing scripts");
                Assert.IsNotNull(prefab.GetComponentInChildren<StandOnHeight>(true), path + " carries a StandOnHeight");
            }
        }
    }
}
