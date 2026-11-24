using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WorldTour.Core.Meta;

namespace WorldTour.World.Wardrobe
{
    /// <summary>
    /// Phase 8.3: what the Star wears. Put on her root (the Star prefab instance): <see cref="Apply"/> swaps the jacket and trousers material for the outfit,
    /// the head material for the hair colour and the guitar's body colour; <see cref="ShowGuitar"/> hangs the guitar on her (stage shows). Nothing here is saved,
    /// the caller decides what to apply (the save's look, or an item being tried on).
    /// </summary>
    public sealed class StarWardrobe : MonoBehaviour
    {
        /// <summary>Anchor under the chest bone (made at run time by WardrobeAnchors): rest pose aligned with the Star, units in metres.</summary>
        public const string FrontAnchor = "WearFront";

        readonly List<Renderer> _top = new List<Renderer>(), _legs = new List<Renderer>();
        Renderer _head;
        Transform _front;
        GameObject _guitar;
        Material _guitarBody;
        bool _found, _guitarOn;
        WardrobeLook _look;
        bool _hasLook;

        public WardrobeLook Applied { get { return _look; } }
        public bool GuitarShown { get { return _guitar != null && _guitar.activeSelf; } }
        public GameObject Guitar { get { return _guitar; } }

        public static StarWardrobe On(GameObject star)
        {
            if (star == null) return null;
            var w = star.GetComponent<StarWardrobe>();
            if (w == null) w = star.AddComponent<StarWardrobe>();
            return w;
        }

        void Find()
        {
            if (_found) return;
            _found = true;
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.name.Contains("_Leg")) _legs.Add(r);
                else if (r.name.Contains("_Head")) _head = r;
                else _top.Add(r);
            }
        }

        public void Apply(WardrobeLook look)
        {
            Find();
            WardrobeAssets a = WardrobeAssets.Load();
            _look = look; _hasLook = true;
            if (a != null)
            {
                OutfitSet o = a.Outfit(look.Outfit);
                if (o != null)
                {
                    if (o.top != null) foreach (Renderer r in _top) r.sharedMaterial = o.top;
                    if (o.legs != null) foreach (Renderer r in _legs) r.sharedMaterial = o.legs;
                }
                HairSet h = a.Hair(look.Hair);
                if (h != null && h.head != null && _head != null) _head.sharedMaterial = h.head;
            }
            if (_guitarBody != null) _guitarBody.SetColor("_BaseColor", GuitarColor(look.Guitar));
        }

        /// <summary>Puts the guitar on her (or takes it off). Its body wears the guitar skin of the last <see cref="Apply"/>.</summary>
        public void ShowGuitar(bool on)
        {
            Find();
            _guitarOn = on;
            if (on && _guitar == null) BuildGuitar();
            if (_guitar != null) _guitar.SetActive(on);
        }

        /// <summary>The 3D colour of a guitar skin. The 2D shop's classic skin is the paper colour; it reads here as natural wood.</summary>
        public static Color GuitarColor(string guitarId)
        {
            ShopItem it = Shop.ById(guitarId);
            Color c;
            if (it != null && it.Color != null && it.Color.StartsWith("#") && ColorUtility.TryParseHtmlString(it.Color, out c)) return c;
            return new Color(0.93f, 0.74f, 0.47f);
        }

        void BuildGuitar()
        {
            WardrobeAssets a = WardrobeAssets.Load();
            _front = WardrobeAnchors.Ensure(gameObject, FrontAnchor);
            if (a == null || _front == null || a.cube == null || a.sphere == null) { Debug.LogWarning("[wardrobe] no guitar: assets or anchor missing"); return; }
            _guitar = new GameObject("Guitar");
            _guitar.transform.SetParent(_front, false);
            // her torso is about 0.6 deep and 0.5 wide (WardrobeDump), the front at z 0.3; the guitar sits on her right hip in front of it, the neck going up and across to her left
            _guitar.transform.localPosition = new Vector3(0.1f, -0.2f, 0.42f);
            _guitar.transform.localRotation = Quaternion.Euler(-12f, 0f, 48f);
            _guitar.transform.localScale = Vector3.one * 0.95f;
            _guitarBody = a.Make("guitar_body", GuitarColor(_hasLook ? _look.Guitar : "guitar:classic"), 0.45f);
            Material wood = a.Make("guitar_neck", new Color(0.36f, 0.2f, 0.1f), 0.3f);
            Material dark = a.Make("guitar_dark", new Color(0.09f, 0.08f, 0.12f), 0.3f);
            Material metal = a.Make("guitar_metal", new Color(0.85f, 0.85f, 0.9f), 0.7f);
            // two round bouts with a waist between them, a sound hole and pickguard on the front (+z), a bridge, a long neck and a headstock
            Part(a.sphere, "LowerBout", _guitarBody, new Vector3(0f, -0.02f, 0f), new Vector3(0.28f, 0.22f, 0.07f));
            Part(a.sphere, "UpperBout", _guitarBody, new Vector3(0f, 0.17f, 0f), new Vector3(0.19f, 0.15f, 0.065f));
            Part(a.sphere, "SoundHole", dark, new Vector3(0f, 0.05f, 0.04f), new Vector3(0.09f, 0.09f, 0.04f));
            Part(a.cube, "Pickguard", dark, new Vector3(0.075f, -0.05f, 0.034f), new Vector3(0.085f, 0.065f, 0.012f), Quaternion.Euler(0f, 0f, 20f));
            Part(a.cube, "Bridge", wood, new Vector3(0f, -0.09f, 0.036f), new Vector3(0.12f, 0.025f, 0.02f));
            Part(a.cube, "Neck", wood, new Vector3(0f, 0.52f, 0f), new Vector3(0.06f, 0.5f, 0.04f));
            Part(a.cube, "Fretboard", dark, new Vector3(0f, 0.52f, 0.02f), new Vector3(0.048f, 0.48f, 0.012f));
            Part(a.cube, "Headstock", wood, new Vector3(0f, 0.82f, -0.005f), new Vector3(0.095f, 0.17f, 0.035f));
            for (int i = 0; i < 3; i++)
            {
                Part(a.sphere, "PegL" + i, metal, new Vector3(-0.058f, 0.78f + i * 0.05f, -0.005f), new Vector3(0.035f, 0.035f, 0.035f));
                Part(a.sphere, "PegR" + i, metal, new Vector3(0.058f, 0.78f + i * 0.05f, -0.005f), new Vector3(0.035f, 0.035f, 0.035f));
            }
            _guitar.SetActive(_guitarOn);
        }

        void Part(Mesh mesh, string name, Material mat, Vector3 pos, Vector3 scale, Quaternion? rot = null)
        {
            var g = new GameObject(name);
            g.transform.SetParent(_guitar.transform, false);
            g.transform.localPosition = pos; g.transform.localScale = scale;
            if (rot.HasValue) g.transform.localRotation = rot.Value;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = g.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.On;
        }
    }
}
