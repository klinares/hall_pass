using System.Collections.Generic;
using UnityEngine;

namespace WorldTour.World
{
    /// <summary>
    /// Which transparent twin to draw for each opaque material (made in the Editor by the FadeBankBuilder, kept in Resources so the web build keeps the
    /// shader variants of the twins). A fading prop swaps to its twins for the length of the fade, so the fade needs no shader that is not already in the build.
    /// </summary>
    public sealed class FadeBank : ScriptableObject
    {
        public const string ResourcePath = "FadeBank";
        public Material[] sources = new Material[0];
        public Material[] twins = new Material[0];

        Dictionary<Material, Material> _map;
        static FadeBank s_Loaded;
        static bool s_Tried;

        public static FadeBank Load()
        {
            if (!s_Tried) { s_Tried = true; s_Loaded = Resources.Load<FadeBank>(ResourcePath); }
            return s_Loaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_Loaded = null; s_Tried = false; }

        /// <summary>The transparent twin of a material, or null when it has none.</summary>
        public Material TwinOf(Material m)
        {
            if (m == null) return null;
            if (_map == null)
            {
                _map = new Dictionary<Material, Material>();
                for (int i = 0; i < sources.Length && i < twins.Length; i++) if (sources[i] != null && twins[i] != null) _map[sources[i]] = twins[i];
            }
            Material t; return _map.TryGetValue(m, out t) ? t : null;
        }
    }
}
