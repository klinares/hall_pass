using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WorldTour.World.Wardrobe
{
    /// <summary>
    /// Phase 8.3: riff's outfit as small meshes hung on two anchors on riff's bones (made at run time by WardrobeAnchors): <c>WearHead</c> on the head bone and <c>WearNeck</c> on
    /// the neck bone, both aligned with riff (nose toward +z), units in metres. Items are built the first time they are shown and then switched on and off.
    /// </summary>
    public sealed class DogWardrobe : MonoBehaviour
    {
        public const string HeadAnchor = "WearHead", NeckAnchor = "WearNeck";

        readonly Dictionary<string, GameObject> _items = new Dictionary<string, GameObject>();
        Transform _head, _neck;
        bool _found;
        string _shown = "dog:none";

        public string Shown { get { return _shown; } }

        /// <summary>The way riff's nose points in the world (the head anchor's +z). In the Riff prefab as it is his nose is at the root's -z.</summary>
        public Vector3 NoseDirection { get { Find(); return _head != null ? _head.forward : -transform.forward; } }

        /// <summary>Degrees about Y to add to a rotation that points the root's +z where the nose should go (180 while his nose is at the root's -z, 0 if the model is ever turned round).</summary>
        public float NoseYaw { get { Find(); return _head != null && Vector3.Dot(_head.forward, transform.forward) < 0f ? 180f : 0f; } }

        public static DogWardrobe On(GameObject dog)
        {
            if (dog == null) return null;
            var w = dog.GetComponent<DogWardrobe>();
            if (w == null) w = dog.AddComponent<DogWardrobe>();
            return w;
        }

        void Find()
        {
            if (_found) return;
            _found = true;
            _head = WardrobeAnchors.Ensure(gameObject, HeadAnchor);
            _neck = WardrobeAnchors.Ensure(gameObject, NeckAnchor);
        }

        /// <summary>Shows one outfit ("dog:beanie"); "dog:none" or anything unknown shows none.</summary>
        public void Apply(string dogItemId)
        {
            Find();
            _shown = dogItemId ?? "dog:none";
            foreach (var kv in _items) if (kv.Value != null) kv.Value.SetActive(false);
            if (_shown == "dog:none") return;
            GameObject g;
            if (!_items.TryGetValue(_shown, out g) || g == null) { g = Build(_shown); _items[_shown] = g; }
            if (g != null) g.SetActive(true);
        }

        GameObject Build(string id)
        {
            WardrobeAssets a = WardrobeAssets.Load();
            if (a == null || a.cube == null || a.sphere == null || a.cylinder == null) return null;
            Transform at = id == "dog:bandana" || id == "dog:collar" ? _neck : _head;
            if (at == null) { Debug.LogWarning("[wardrobe] riff has no " + (id == "dog:bandana" || id == "dog:collar" ? NeckAnchor : HeadAnchor) + " anchor"); return null; }
            var root = new GameObject("Wear_" + id.Substring(4));
            root.transform.SetParent(at, false);
            switch (id)
            {
                case "dog:beanie": Beanie(root.transform, a); break;
                case "dog:shades": Shades(root.transform, a); break;
                case "dog:phones": Phones(root.transform, a); break;
                case "dog:bandana": Bandana(root.transform, a); break;
                case "dog:collar": Collar(root.transform, a); break;
            }
            return root;
        }

        static Color C(string hex) { Color c; ColorUtility.TryParseHtmlString(hex, out c); return c; }

        static GameObject Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, Vector3? euler = null)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = scale;
            if (euler.HasValue) g.transform.localRotation = Quaternion.Euler(euler.Value);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = g.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.On;
            return g;
        }

        // riff head in the head anchor frame (x across, y up, z toward the nose, metres, measured by WardrobeDump): the skull is about 0.17 wide, top at y 0.12, bottom at -0.08,
        // from the back of the skull (z -0.01) to the nose (0.195); the ears hang out to +-0.16.
        void Beanie(Transform t, WardrobeAssets a)
        {
            Material wool = a.Make("beanie_wool", C("#2f2fc1"), 0.2f), cuff = a.Make("beanie_cuff", C("#f98bb0"), 0.2f), pom = a.Make("beanie_pom", C("#fdb1cb"), 0.15f);
            Part(t, "Cap", MeshKit.Dome(0.5f, 0.9f, 18, 7), wool, new Vector3(0f, 0.062f, 0.045f), new Vector3(0.215f, 0.2f, 0.235f));
            Part(t, "Cuff", MeshKit.Tube(0.1f, 0.04f, 0.018f, 22), cuff, new Vector3(0f, 0.065f, 0.045f), new Vector3(1.05f, 1f, 1.15f));
            Part(t, "Pompom", a.sphere, pom, new Vector3(0f, 0.176f, 0.045f), Vector3.one * 0.075f);
        }

        void Shades(Transform t, WardrobeAssets a)
        {
            Material black = a.Make("shades_lens", C("#14121c"), 0.8f), frame = a.Make("shades_frame", C("#fdb1cb"), 0.3f);
            foreach (float s in new[] { -1f, 1f })
            {
                Part(t, "Lens", a.sphere, black, new Vector3(0.05f * s, 0.06f, 0.125f), new Vector3(0.062f, 0.048f, 0.03f));
                Part(t, "Arm", a.cube, frame, new Vector3(0.088f * s, 0.062f, 0.06f), new Vector3(0.012f, 0.014f, 0.135f));
            }
            Part(t, "Bridge", a.cube, frame, new Vector3(0f, 0.066f, 0.128f), new Vector3(0.03f, 0.012f, 0.012f));
        }

        void Phones(Transform t, WardrobeAssets a)
        {
            Material band = a.Make("phones_band", C("#f98bb0"), 0.4f), cup = a.Make("phones_cup", C("#26274f"), 0.4f), pad = a.Make("phones_pad", C("#fdb1cb"), 0.2f);
            // the band arcs over the head in the XY plane: a torus turned so its axis is z, 190 degrees, from the right ear over the top to the left
            Part(t, "Band", MeshKit.Torus(0.5f, 0.05f, 28, 8, 190f), band, new Vector3(0f, 0.02f, 0.03f), new Vector3(0.25f, 0.25f, 0.25f), new Vector3(-90f, 0f, 0f));
            foreach (float s in new[] { -1f, 1f })
            {
                Part(t, "Cup", a.cylinder, cup, new Vector3(0.118f * s, 0.02f, 0.03f), new Vector3(0.085f, 0.018f, 0.085f), new Vector3(0f, 0f, 90f));
                Part(t, "Pad", a.cylinder, pad, new Vector3(0.1f * s, 0.02f, 0.03f), new Vector3(0.065f, 0.012f, 0.065f), new Vector3(0f, 0f, 90f));
            }
        }

        // the neck anchor sits half way up the neck with +y along the neck and +z toward the nose; the neck is about 0.22 across
        void Bandana(Transform t, WardrobeAssets a)
        {
            Material red = a.Make("bandana_red", C("#e8364f"), 0.2f), dot = a.Make("bandana_dot", C("#fff3f5"), 0.2f);
            Part(t, "Wrap", MeshKit.Tube(0.108f, 0.075f, 0.016f, 24), red, Vector3.zero, new Vector3(1f, 1f, 1.05f));
            // the triangle hangs at the front of the neck: a diamond turned on its point, flat, with three white dots
            Part(t, "Flap", a.cube, red, new Vector3(0f, -0.075f, 0.12f), new Vector3(0.15f, 0.15f, 0.018f), new Vector3(0f, 0f, 45f));
            Part(t, "Dot", a.sphere, dot, new Vector3(0f, -0.095f, 0.13f), new Vector3(0.026f, 0.026f, 0.01f));
            Part(t, "DotL", a.sphere, dot, new Vector3(-0.034f, -0.062f, 0.13f), new Vector3(0.02f, 0.02f, 0.01f));
            Part(t, "DotR", a.sphere, dot, new Vector3(0.034f, -0.062f, 0.13f), new Vector3(0.02f, 0.02f, 0.01f));
        }

        void Collar(Transform t, WardrobeAssets a)
        {
            Material band = a.Make("collar_band", C("#1b1a24"), 0.3f), spike = a.Make("collar_spike", C("#e9e9f2"), 0.8f);
            Part(t, "Band", MeshKit.Torus(0.5f, 0.1f, 22, 8), band, Vector3.zero, new Vector3(0.235f, 0.12f, 0.235f));
            const int n = 10;
            for (int i = 0; i < n; i++)
            {
                float ang = i / (float)n * 360f;
                Quaternion q = Quaternion.Euler(0f, ang, 0f);
                Vector3 outDir = q * Vector3.right;
                // each spike points straight out from the band
                var s = Part(t, "Spike" + i, MeshKit.Cone(0.5f, 1f, 6), spike, outDir * 0.118f, new Vector3(0.035f, 0.05f, 0.035f), q.eulerAngles + new Vector3(0f, 0f, -90f));
                s.transform.localRotation = q * Quaternion.Euler(0f, 0f, -90f);
            }
        }
    }
}
