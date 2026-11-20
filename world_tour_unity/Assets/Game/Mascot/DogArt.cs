using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldTour.Game.Mascot
{
    /// <summary>
    /// riff's layer sprites and the numbers from Assets/Art/Sprites/manifest.json that the rig needs at runtime.
    /// Written by the Editor menu World Tour > Sprites > Build dog art (MascotArtBuilder); not edited by hand.
    /// All positions are sprite px in the dog frame (380 px for the 160-unit dog.js box), y down from the top-left.
    /// </summary>
    [CreateAssetMenu(menuName = "World Tour/Dog Art", fileName = "DogArt")]
    public sealed class DogArt : ScriptableObject
    {
        [Serializable]
        public sealed class Layer
        {
            public string name;
            public Sprite sprite;
            /// <summary>Top-left of the sprite in the frame, px, y down.</summary>
            public Vector2 offset;
            public Vector2 size;
            /// <summary>tail, body, arms, head, lids, fx, outfit, paws, z1..z3.</summary>
            public string part;
            /// <summary>art, ink or pad.</summary>
            public string layer;
        }

        [Serializable]
        public sealed class Stack
        {
            public string pose;
            /// <summary>"none" or an outfit id.</summary>
            public string outfit;
            /// <summary>Bottom to top: every _ink, every _pad, then the art in dog.js order.</summary>
            public string[] layers;
        }

        public Vector2 framePx = new Vector2(380, 380);
        public float unitsToPx = 2.375f;
        public Vector2 tailPivotPx;
        public Vector2 lidPivotPx;
        public List<Layer> layers = new List<Layer>();
        public List<Stack> stacks = new List<Stack>();

        [Header("Speech bubble (decor)")]
        public Sprite bubble;
        public Sprite bubbleTail;
        /// <summary>Where the web box sits in the bubble sprite, px [x, y, w, h]; the rest is its 3px shadow.</summary>
        public Vector4 bubbleBoxPx;
        /// <summary>Tail sprite top-left from the bubble's bottom centre, px, y down.</summary>
        public Vector2 bubbleTailOffsetPx;

        Dictionary<string, Layer> _byName;

        public Layer Find(string name)
        {
            if (_byName == null || _byName.Count != layers.Count)
            {
                _byName = new Dictionary<string, Layer>(StringComparer.Ordinal);
                foreach (var l in layers) if (l != null && !string.IsNullOrEmpty(l.name)) _byName[l.name] = l;
            }
            return _byName.TryGetValue(name, out var hit) ? hit : null;
        }

        public string[] StackFor(DogPose pose, string outfit)
        {
            string p = DogPoses.Id(pose), o = DogPoses.StackOutfit(pose, outfit);
            foreach (var s in stacks) if (s.pose == p && s.outfit == o) return s.layers;
            foreach (var s in stacks) if (s.pose == p && s.outfit == "none") return s.layers;
            return Array.Empty<string>();
        }

        void OnValidate() { _byName = null; }
    }
}
