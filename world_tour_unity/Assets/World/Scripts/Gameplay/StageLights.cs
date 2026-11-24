using System.Collections.Generic;
using UnityEngine;
using WorldTour.Game.Prefs;

namespace WorldTour.World.Gameplay
{
    /// <summary>
    /// The light burst on a right answer (phase 8.1 stage show): the stage's spotlight beams (the "Beam" children of the objects named Spot0, Spot1, ...)
    /// flare brighter and wider for about 0.7 s, then settle. A place with no spotlights has nothing to flare. Nothing moves under reduce motion.
    /// </summary>
    public sealed class StageLights : MonoBehaviour
    {
        const float Seconds = 0.7f;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        readonly List<Renderer> _beams = new List<Renderer>();
        readonly List<Vector3> _scales = new List<Vector3>();
        MaterialPropertyBlock _block;
        float _start = -10f;
        bool _lit;

        public int Count { get { return _beams.Count; } }

        /// <summary>The flare's strength right now, 0 to 1 (for tests).</summary>
        public float Level { get; private set; }

        /// <summary>Finds the beams in the loaded scene.</summary>
        public void Find()
        {
            _beams.Clear(); _scales.Clear();
            for (int i = 0; i < 12; i++)
            {
                GameObject spot = GameObject.Find("Spot" + i);
                if (spot == null) continue;
                foreach (Renderer r in spot.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.gameObject.name != "Beam") continue;
                    _beams.Add(r); _scales.Add(r.transform.localScale);
                }
            }
        }

        public void Flare()
        {
            if (GameSettings.ReducedMotion || _beams.Count == 0) return;
            _start = Time.time;
        }

        void Update()
        {
            float t = (Time.time - _start) / Seconds;
            float k = t >= 0f && t < 1f ? Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI * 0.5f) * (1f - t) * 2f : 0f;   // quick up, slower down
            k = Mathf.Clamp01(k);
            Level = k;
            if (k <= 0f && !_lit) return;
            _lit = k > 0f;
            if (_block == null) _block = new MaterialPropertyBlock();
            for (int i = 0; i < _beams.Count; i++)
            {
                Renderer r = _beams[i];
                if (r == null) continue;
                _block.SetColor(BaseColor, Color.white * (1f + 1.1f * k));
                r.SetPropertyBlock(_block);
                r.transform.localScale = new Vector3(_scales[i].x * (1f + 0.12f * k), _scales[i].y * (1f + 0.12f * k), _scales[i].z);
            }
        }
    }
}
