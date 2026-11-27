using System;
using System.Collections.Generic;
using UnityEngine;
using WorldTour.World;

namespace WorldTour.World.Gameplay
{
    /// <summary>
    /// The things the Home scene hands to HomeGame: the prefab of every piece (by id, built by InteriorHomeBuilder), the see-through green, red and yellow
    /// materials for the ghost and the footprint plate, and the plate itself. Only references: scene unload frees them.
    /// </summary>
    public sealed class HomeStock : MonoBehaviour
    {
        public HomePrefab[] pieces = new HomePrefab[0];
        public Material ok, bad, pick;
        public GameObject plate;

        Dictionary<string, GameObject> _map;

        public GameObject PrefabOf(string id)
        {
            if (_map == null)
            {
                _map = new Dictionary<string, GameObject>();
                foreach (HomePrefab p in pieces) if (p.prefab != null) _map[p.id] = p.prefab;
            }
            GameObject g;
            return id != null && _map.TryGetValue(id, out g) ? g : null;
        }
    }
}
