using UnityEngine;

namespace WorldTour.World
{
    /// <summary>Small ambient motion for scenery (a spinning record sign, a sleeping cat's breathing, a neon sign's glow). All of it stops under reduce motion.</summary>
    public static class PropMotion
    {
        /// <summary>Set by the gameplay from the reduce-motion setting when a place loads. Props hold still while true.</summary>
        public static bool Reduced;
    }
}
