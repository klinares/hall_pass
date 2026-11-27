using System;
using System.Collections.Generic;
using UnityEngine;
using WorldTour.World;

namespace WorldTour.World.Gameplay
{
    /// <summary>Marks renderers the wall or the floor color is painted on (a property block, so no material is ever copied).</summary>
    public sealed class HomePaint : MonoBehaviour
    {
        public bool wall;
        public Color own = Color.white;
        public Renderer[] targets = new Renderer[0];
    }
}
