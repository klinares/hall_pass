using System;
using UnityEngine;

namespace WorldTour.World.Gameplay
{
    /// <summary>One piece's prefab, by id (see <see cref="HomeStock"/>).</summary>
    [Serializable]
    public struct HomePrefab
    {
        public string id;
        public GameObject prefab;
    }
}
