using System;
using System.Collections.Generic;
using UnityEngine;
using WorldTour.World;

namespace WorldTour.World.Gameplay
{
    /// <summary>A piece standing (or being held) in the room: remembers its own materials so the ghost tint can be put on and taken off without copying anything.</summary>
    public sealed class HomeView : MonoBehaviour
    {
        public string id;
        Renderer[] _rs;
        Material[][] _orig, _ok, _bad, _pick;
        Transform[] _blockers;
        int _mode = -1;

        public void Capture(HomeStock stock)
        {
            _rs = GetComponentsInChildren<Renderer>(true);
            int n = _rs.Length;
            _orig = new Material[n][]; _ok = new Material[n][]; _bad = new Material[n][]; _pick = new Material[n][];
            for (int i = 0; i < n; i++)
            {
                Material[] m = _rs[i].sharedMaterials;
                _orig[i] = m;
                _ok[i] = Fill(m.Length, stock.ok); _bad[i] = Fill(m.Length, stock.bad); _pick[i] = Fill(m.Length, stock.pick);
            }
            _mode = 0;
        }

        static Material[] Fill(int n, Material m)
        {
            var a = new Material[n];
            for (int i = 0; i < n; i++) a[i] = m;
            return a;
        }

        /// <summary>0 as built, 1 ghost green, 2 ghost red, 3 yellow (selected).</summary>
        public void Show(int mode)
        {
            if (mode == _mode || _rs == null) return;
            _mode = mode;
            for (int i = 0; i < _rs.Length; i++)
            {
                if (_rs[i] == null) continue;
                switch (mode)
                {
                    case 1: _rs[i].sharedMaterials = _ok[i]; break;
                    case 2: _rs[i].sharedMaterials = _bad[i]; break;
                    case 3: _rs[i].sharedMaterials = _pick[i]; break;
                    default: _rs[i].sharedMaterials = _orig[i]; break;
                }
                _rs[i].shadowCastingMode = mode == 0 ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// <summary>Round blockers over the footprint (at rotation 0, local to the piece, so they turn and move with it) so she walks round it.</summary>
        public void MakeBlockers(int w, int d)
        {
            int nx = Mathf.Max(1, Mathf.CeilToInt(w / 2f)), nz = Mathf.Max(1, Mathf.CeilToInt(d / 2f));
            float sx = w * 0.5f / nx, sz = d * 0.5f / nz, r = Mathf.Max(0.35f, 0.5f * Mathf.Min(sx, sz));
            _blockers = new Transform[nx * nz];
            int k = 0;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    var g = new GameObject("Block");
                    g.transform.SetParent(transform, false);
                    g.transform.localPosition = new Vector3(-w * 0.25f + (i + 0.5f) * sx, 0f, -d * 0.25f + (j + 0.5f) * sz);
                    g.AddComponent<Obstacle>().radius = r;
                    _blockers[k++] = g.transform;
                }
        }

        public void SetBlockers(bool on)
        {
            if (_blockers == null) return;
            for (int i = 0; i < _blockers.Length; i++) if (_blockers[i] != null && _blockers[i].gameObject.activeSelf != on) _blockers[i].gameObject.SetActive(on);
        }
    }
}
