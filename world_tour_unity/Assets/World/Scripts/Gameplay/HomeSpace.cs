using System;
using System.Collections.Generic;
using UnityEngine;
using WorldTour.World;

namespace WorldTour.World.Gameplay
{
    /// <summary>Phase 10B.2: one of her home's rooms in the Home scene (0 her room, 1 den, 2 studio, 3 garden). All four are built at the same place; only one is switched on at a time.</summary>
    public sealed class HomeSpace : MonoBehaviour
    {
        public int index;
    }
}
