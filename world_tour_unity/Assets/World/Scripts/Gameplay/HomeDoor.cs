using System;
using System.Collections.Generic;
using UnityEngine;
using WorldTour.World;

namespace WorldTour.World.Gameplay
{
    /// <summary>A door between two rooms. Its child "Stand" (optional) is where she is put when she comes through it.</summary>
    public sealed class HomeDoor : MonoBehaviour
    {
        public int target;
        public bool east;
    }
}
