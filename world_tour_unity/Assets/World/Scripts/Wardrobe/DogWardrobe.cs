using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WorldTour.Core.Meta;

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

        /// <summary>Shows one outfit ("dog:beanie") in its own colours; "dog:none" or anything unknown shows none.</summary>
        public void Apply(string dogItemId) { Show(dogItemId ?? "dog:none", null); }

        /// <summary>Phase 10B.1: shows one item from the avatar look, in the picked palette colour.</summary>
        public void Apply(AvatarPick pick)
        {
            if (pick == null) { Show("dog:none", null); return; }
            Show(pick.Id ?? "dog:none", AvatarColors.Palette(pick.Color));
        }

        void Show(string id, Color? main)
        {
            Find();
            _shown = id;
            foreach (var kv in _items) if (kv.Value != null) kv.Value.SetActive(false);
            if (_shown == "dog:none") return;
            string key = main.HasValue ? _shown + "/" + ColorUtility.ToHtmlStringRGB(main.Value) : _shown;
            GameObject g;
            if (!_items.TryGetValue(key, out g) || g == null) { g = Build(_shown, main); _items[key] = g; }
            if (g != null) g.SetActive(true);
        }

        static bool AtNeck(string id) { return id == "dog:bandana" || id == "dog:collar" || id == "dog:bowtie" || id == "dog:scarf" || id == "dog:flowers"; }

        GameObject Build(string id, Color? main)
        {
            WardrobeAssets a = WardrobeAssets.Load();
            if (a == null || a.cube == null || a.sphere == null || a.cylinder == null) return null;
            Transform at = AtNeck(id) ? _neck : _head;
            if (at == null) { Debug.LogWarning("[wardrobe] riff has no " + (AtNeck(id) ? NeckAnchor : HeadAnchor) + " anchor"); return null; }
            var root = new GameObject("Wear_" + id.Substring(4));
            root.transform.SetParent(at, false);
            switch (id)
            {
                case "dog:beanie": Beanie(root.transform, a, main); break;
                case "dog:shades": Shades(root.transform, a, main); break;
                case "dog:phones": Phones(root.transform, a, main); break;
                case "dog:bandana": Bandana(root.transform, a, main); break;
                case "dog:collar": Collar(root.transform, a, main); break;
                case "dog:bowtie": BowTie(root.transform, a, main ?? C("#e8364f")); break;
                case "dog:scarf": Scarf(root.transform, a, main ?? C("#ffbb9a")); break;
                case "dog:partyhat": PartyHat(root.transform, a, main ?? C("#8a55d8")); break;
                case "dog:flowers": Flowers(root.transform, a, main ?? C("#ffc4da")); break;
                case "dog:crown": Crown(root.transform, a, main ?? C("#ffd34e")); break;
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
        void Beanie(Transform t, WardrobeAssets a, Color? main)
        {
            Material wool = a.Make("beanie_wool", main ?? C("#2f2fc1"), 0.2f), cuff = a.Make("beanie_cuff", C("#f98bb0"), 0.2f), pom = a.Make("beanie_pom", C("#fdb1cb"), 0.15f);
            Part(t, "Cap", MeshKit.Dome(0.5f, 0.9f, 18, 7), wool, new Vector3(0f, 0.062f, 0.045f), new Vector3(0.215f, 0.2f, 0.235f));
            Part(t, "Cuff", MeshKit.Tube(0.1f, 0.04f, 0.018f, 22), cuff, new Vector3(0f, 0.065f, 0.045f), new Vector3(1.05f, 1f, 1.15f));
            Part(t, "Pompom", a.sphere, pom, new Vector3(0f, 0.176f, 0.045f), Vector3.one * 0.075f);
        }

        void Shades(Transform t, WardrobeAssets a, Color? main)
        {
            Material black = a.Make("shades_lens", C("#14121c"), 0.8f), frame = a.Make("shades_frame", main ?? C("#fdb1cb"), 0.3f);
            foreach (float s in new[] { -1f, 1f })
            {
                Part(t, "Lens", a.sphere, black, new Vector3(0.05f * s, 0.06f, 0.125f), new Vector3(0.062f, 0.048f, 0.03f));
                Part(t, "Arm", a.cube, frame, new Vector3(0.088f * s, 0.062f, 0.06f), new Vector3(0.012f, 0.014f, 0.135f));
            }
            Part(t, "Bridge", a.cube, frame, new Vector3(0f, 0.066f, 0.128f), new Vector3(0.03f, 0.012f, 0.012f));
        }

        void Phones(Transform t, WardrobeAssets a, Color? main)
        {
            Material band = a.Make("phones_band", main ?? C("#f98bb0"), 0.4f), cup = a.Make("phones_cup", C("#26274f"), 0.4f), pad = a.Make("phones_pad", C("#fdb1cb"), 0.2f);
            // the band arcs over the head in the XY plane: a torus turned so its axis is z, 190 degrees, from the right ear over the top to the left
            Part(t, "Band", MeshKit.Torus(0.5f, 0.05f, 28, 8, 190f), band, new Vector3(0f, 0.02f, 0.03f), new Vector3(0.25f, 0.25f, 0.25f), new Vector3(-90f, 0f, 0f));
            foreach (float s in new[] { -1f, 1f })
            {
                Part(t, "Cup", a.cylinder, cup, new Vector3(0.118f * s, 0.02f, 0.03f), new Vector3(0.085f, 0.018f, 0.085f), new Vector3(0f, 0f, 90f));
                Part(t, "Pad", a.cylinder, pad, new Vector3(0.1f * s, 0.02f, 0.03f), new Vector3(0.065f, 0.012f, 0.065f), new Vector3(0f, 0f, 90f));
            }
        }

        // the neck anchor sits half way up the neck with +y along the neck and +z toward the nose; the neck is about 0.22 across
        void Bandana(Transform t, WardrobeAssets a, Color? main)
        {
            Material red = a.Make("bandana_red", main ?? C("#e8364f"), 0.2f), dot = a.Make("bandana_dot", C("#fff3f5"), 0.2f);
            Part(t, "Wrap", MeshKit.Tube(0.108f, 0.075f, 0.016f, 24), red, Vector3.zero, new Vector3(1f, 1f, 1.05f));
            // the triangle hangs at the front of the neck: a diamond turned on its point, flat, with three white dots
            Part(t, "Flap", a.cube, red, new Vector3(0f, -0.075f, 0.12f), new Vector3(0.15f, 0.15f, 0.018f), new Vector3(0f, 0f, 45f));
            Part(t, "Dot", a.sphere, dot, new Vector3(0f, -0.095f, 0.13f), new Vector3(0.026f, 0.026f, 0.01f));
            Part(t, "DotL", a.sphere, dot, new Vector3(-0.034f, -0.062f, 0.13f), new Vector3(0.02f, 0.02f, 0.01f));
            Part(t, "DotR", a.sphere, dot, new Vector3(0.034f, -0.062f, 0.13f), new Vector3(0.02f, 0.02f, 0.01f));
        }

        void Collar(Transform t, WardrobeAssets a, Color? main)
        {
            Material band = a.Make("collar_band", main ?? C("#1b1a24"), 0.3f), spike = a.Make("collar_spike", C("#e9e9f2"), 0.8f);
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

        // ---------- phase 10B.1: more of riff's things, each in the picked colour ----------

        void BowTie(Transform t, WardrobeAssets a, Color col)
        {
            Material m = a.Make("bow_m", col, 0.25f), k = a.Make("bow_k", AvatarColors.Shade(col, 0.3f), 0.25f);
            foreach (float s in new[] { -1f, 1f }) Part(t, "Wing", a.sphere, m, new Vector3(0.05f * s, -0.07f, 0.125f), new Vector3(0.075f, 0.055f, 0.03f), new Vector3(0f, 0f, 20f * s));
            Part(t, "Knot", a.sphere, k, new Vector3(0f, -0.07f, 0.13f), Vector3.one * 0.035f);
        }

        void Scarf(Transform t, WardrobeAssets a, Color col)
        {
            Material m = a.Make("scarf_m", col, 0.2f), s2 = a.Make("scarf_s", AvatarColors.Contrast(col), 0.2f);
            Part(t, "Wrap", MeshKit.Tube(0.108f, 0.09f, 0.02f, 24), m, Vector3.zero, new Vector3(1f, 1f, 1.05f));
            Part(t, "Stripe", MeshKit.Tube(0.108f, 0.02f, 0.021f, 24), s2, new Vector3(0f, 0.02f, 0f), new Vector3(1f, 1f, 1.05f));
            Part(t, "Tail", a.cube, m, new Vector3(0.05f, -0.1f, 0.12f), new Vector3(0.055f, 0.14f, 0.02f), new Vector3(0f, 0f, 8f));
        }

        void PartyHat(Transform t, WardrobeAssets a, Color col)
        {
            Material m = a.Make("party_m", col, 0.2f), p = a.Make("party_p", AvatarColors.Tint(col, 0.5f), 0.2f);
            Part(t, "Cone", MeshKit.Cone(0.5f, 1f, 12), m, new Vector3(0f, 0.1f, 0.045f), new Vector3(0.12f, 0.15f, 0.12f));
            Part(t, "Pom", a.sphere, p, new Vector3(0f, 0.255f, 0.045f), Vector3.one * 0.04f);
        }

        void Flowers(Transform t, WardrobeAssets a, Color col)
        {
            Material m = a.Make("fl_m", col, 0.2f), y = a.Make("fl_y", C("#ffd34e"), 0.2f);
            for (int i = 0; i < 7; i++)
            {
                float ang = i / 7f * Mathf.PI * 2f;
                Vector3 p = new Vector3(Mathf.Sin(ang) * 0.12f, 0f, Mathf.Cos(ang) * 0.12f);
                Part(t, "Petals", a.sphere, m, p, Vector3.one * 0.05f);
                Part(t, "Heart", a.sphere, y, p * 1.06f, Vector3.one * 0.022f);
            }
        }

        void Crown(Transform t, WardrobeAssets a, Color col)
        {
            Material m = a.Make("crown_m", col, 0.7f);
            Part(t, "Band", MeshKit.Tube(0.1f, 0.04f, 0.012f, 20), m, new Vector3(0f, 0.115f, 0.045f), new Vector3(1f, 1f, 1.1f));
            for (int i = 0; i < 6; i++)
            {
                float ang = i / 6f * Mathf.PI * 2f;
                Part(t, "Point", MeshKit.Cone(0.5f, 1f, 6), m, new Vector3(Mathf.Sin(ang) * 0.1f, 0.135f, 0.045f + Mathf.Cos(ang) * 0.11f), new Vector3(0.035f, 0.06f, 0.035f));
            }
        }
    }
}
