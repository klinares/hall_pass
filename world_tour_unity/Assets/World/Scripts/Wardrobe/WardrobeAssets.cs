using System;
using UnityEngine;

namespace WorldTour.World.Wardrobe
{
    [Serializable] public sealed class OutfitSet { public string id; public Material top, legs; }
    [Serializable] public sealed class HairSet { public string id; public Material head; }

    /// <summary>An attachment point: a child of a bone with this local pose (found at its rest pose in the Star and Riff prefabs; scale is compensated at run time so children are laid out in metres).</summary>
    [Serializable] public sealed class AnchorSpec { public string bone, name; public Vector3 position; public Quaternion rotation = Quaternion.identity; }

    /// <summary>
    /// Phase 8.3: the few assets the wardrobe needs by reference, in Resources so a place scene needs no wiring: one recoloured material set per outfit
    /// (jacket and arms, trousers) and per hair colour (the head), a plain URP Lit material the accessories and the guitar clone (so the shader is in the build),
    /// and the built-in meshes the accessories are made of (the physics module is stripped from the web build, so CreatePrimitive cannot be used).
    /// Made by the WardrobeBuilder Editor script (menu World Tour).
    /// </summary>
    public sealed class WardrobeAssets : ScriptableObject
    {
        public const string ResourcePath = "WardrobeAssets";

        public OutfitSet[] outfits = new OutfitSet[0];
        public HairSet[] hair = new HairSet[0];
        public AnchorSpec[] anchors = new AnchorSpec[0];
        public Material baseLit;
        public Mesh cube, sphere, cylinder;

        static WardrobeAssets s_Loaded;
        public static WardrobeAssets Load()
        {
            if (s_Loaded == null) s_Loaded = Resources.Load<WardrobeAssets>(ResourcePath);
            return s_Loaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_Loaded = null; }

        public AnchorSpec Anchor(string name)
        {
            foreach (AnchorSpec a in anchors) if (a != null && a.name == name) return a;
            return null;
        }

        public OutfitSet Outfit(string id)
        {
            foreach (OutfitSet o in outfits) if (o != null && o.id == id) return o;
            return outfits.Length > 0 ? outfits[0] : null;
        }

        public HairSet Hair(string id)
        {
            foreach (HairSet h in hair) if (h != null && h.id == id) return h;
            return hair.Length > 0 ? hair[0] : null;
        }

        /// <summary>A lit material of one colour, cloned from <see cref="baseLit"/> (null when the asset has none).</summary>
        public Material Make(string name, Color color, float smooth = 0.3f, Color? emission = null)
        {
            if (baseLit == null) return null;
            var m = new Material(baseLit) { name = name };
            m.SetColor("_BaseColor", color);
            m.SetTexture("_BaseMap", null);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0f);
            if (emission.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value); }
            else { m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black); }
            return m;
        }
    }
}
