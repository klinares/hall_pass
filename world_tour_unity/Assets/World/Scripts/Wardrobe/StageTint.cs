using System.Collections.Generic;
using UnityEngine;
using WorldTour.Core.Meta;

namespace WorldTour.World.Wardrobe
{
    /// <summary>
    /// Phase 8.3: the stage light colour. Finds the spotlights of the open place (objects named Spot0.., Sweep0.. with a child "Beam" and a child "Lens", as PlaceKit makes them)
    /// and recolours them: the beam gets a cone of the same shape with the new colour in its vertex colours, the lens glows the new colour (a property block on its
    /// emission). "lights:tour" puts every place's own colours back. The right-answer flare (StageLights) is untouched: it brightens the beam material, which multiplies these colours.
    /// </summary>
    public sealed class StageTint : MonoBehaviour
    {
        sealed class Lamp
        {
            public MeshRenderer Beam, Lens;
            public Mesh BeamMesh;       // the place's own
            public float Length, Radius;
        }

        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly Dictionary<string, Mesh> Cones = new Dictionary<string, Mesh>();

        readonly List<Lamp> _lamps = new List<Lamp>();
        MaterialPropertyBlock _block;
        string _applied = "lights:tour";

        public int Count { get { return _lamps.Count; } }
        public string Applied { get { return _applied; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Cones.Clear(); }

        /// <summary>Finds the lamps in the loaded scene, or only under <paramref name="within"/> (call after a place loads, before <see cref="Apply"/>).</summary>
        public void Find(Transform within = null)
        {
            _lamps.Clear();
            _applied = "lights:tour";
            var byParent = new Dictionary<Transform, Lamp>();
            MeshRenderer[] all = within != null ? within.GetComponentsInChildren<MeshRenderer>(true) : Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (MeshRenderer r in all)
            {
                Transform p = r.transform.parent;
                if (p == null || !(p.name.StartsWith("Spot") || p.name.StartsWith("Sweep"))) continue;
                if (r.name != "Beam" && r.name != "Lens") continue;
                Lamp l;
                if (!byParent.TryGetValue(p, out l)) { l = new Lamp(); byParent[p] = l; _lamps.Add(l); }
                if (r.name == "Beam")
                {
                    l.Beam = r;
                    MeshFilter f = r.GetComponent<MeshFilter>();
                    l.BeamMesh = f != null ? f.sharedMesh : null;
                    if (l.BeamMesh != null) { l.Length = l.BeamMesh.bounds.max.z; l.Radius = Mathf.Max(l.BeamMesh.bounds.extents.x, l.BeamMesh.bounds.extents.y); }
                }
                else l.Lens = r;
            }
        }

        /// <summary>The colour of a lights item ("lights:pink"), or white for the free one.</summary>
        public static Color ColorOf(string lightsId)
        {
            ShopItem it = Shop.ById(lightsId);
            Color c;
            if (it != null && it.Slot == Shop.Lights && it.Price > 0 && it.Color != null && ColorUtility.TryParseHtmlString(it.Color, out c)) return c;
            return Color.white;
        }

        public void Apply(string lightsId)
        {
            _applied = lightsId ?? "lights:tour";
            ShopItem it = Shop.ById(_applied);
            bool tinted = it != null && it.Slot == Shop.Lights && it.Price > 0;
            if (_block == null) _block = new MaterialPropertyBlock();
            Color c = ColorOf(_applied);
            foreach (Lamp l in _lamps)
            {
                if (l.Beam != null && l.BeamMesh != null)
                {
                    MeshFilter f = l.Beam.GetComponent<MeshFilter>();
                    if (f != null) f.sharedMesh = tinted ? Cone(l, c, _applied) : l.BeamMesh;
                }
                if (l.Lens != null)
                {
                    if (!tinted) l.Lens.SetPropertyBlock(null);
                    else
                    {
                        l.Lens.GetPropertyBlock(_block);
                        _block.SetColor(EmissionColor, new Color(c.r * 3f, c.g * 3f, c.b * 3f, 1f));
                        _block.SetColor(BaseColor, Color.Lerp(Color.white, c, 0.35f));
                        l.Lens.SetPropertyBlock(_block);
                    }
                }
            }
        }

        static Mesh Cone(Lamp l, Color c, string id)
        {
            string key = id + "_" + l.Length.ToString("0.0") + "_" + l.Radius.ToString("0.00");
            Mesh m;
            if (Cones.TryGetValue(key, out m) && m != null) return m;
            m = MeshKit.BeamCone(l.Length, l.Radius, new Color(c.r * 5f, c.g * 5f, c.b * 5f, 1f), "beam_" + key);   // stronger than the places' own lens colours (they are 3 or so), so the new colour is plain on the purple stage wall
            Cones[key] = m;
            return m;
        }
    }
}
