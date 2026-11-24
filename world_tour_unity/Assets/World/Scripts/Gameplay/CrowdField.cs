using System.Collections.Generic;
using UnityEngine;
using WorldTour.Core.Tour;
using WorldTour.Game.Prefs;

namespace WorldTour.World.Gameplay
{
    /// <summary>
    /// The crowd (phase 8.1): simple placeholder figures spread over the CrowdArea, more of them as her fans grow
    /// (<see cref="CrowdRules.Count"/>), that hop when she gets a song right, with a light burst. Under reduce motion they stay still
    /// and there is no burst. The figures are capsule-and-ball stand-ins; real art can replace <see cref="MakeFigure"/> without touching the rest.
    /// </summary>
    public sealed class CrowdField : MonoBehaviour
    {
        sealed class Figure
        {
            public Transform Root;
            public CrowdFigure Pose;
            public Vector3 Home;
            public float Phase, HopStart = -10f, Size = 1f, AppearStart = -10f;
        }

        const float HopSeconds = 0.55f;
        const float CharacterScale = 0.85f;   // the baked meshes are at model scale 1

        readonly List<Figure> _figures = new List<Figure>();
        Transform _center;
        Vector2 _extent = new Vector2(3.5f, 1.75f);
        Vector3 _facing = Vector3.back;
        WorldGameplayAssets _assets;
        ParticleSystem _burst;
        System.Random _rng;
        int _wanted;

        public int Count { get { return _figures.Count; } }

        public void Setup(Transform center, Vector3 facePoint, WorldGameplayAssets assets, int seed)
        {
            _center = center;
            _assets = assets;
            _rng = new System.Random(seed);
            // the area's size comes from its scale when it was set, else a default 7 x 3.5 m
            Vector3 s = center.lossyScale;
            if (Mathf.Abs(s.x - 1f) > 0.01f || Mathf.Abs(s.z - 1f) > 0.01f) _extent = new Vector2(Mathf.Max(1f, Mathf.Abs(s.x)) / 2f, Mathf.Max(1f, Mathf.Abs(s.z)) / 2f);
            Vector3 d = facePoint - center.position; d.y = 0;
            _facing = d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward;
            BuildBurst();
        }

        void OnDestroy()
        {
            foreach (Figure f in _figures) if (f.Root != null) Destroy(f.Root.gameObject);
            if (_burst != null) Destroy(_burst.gameObject);
        }

        /// <summary>Grows the crowd to match lifetime fans (never shrinks it).</summary>
        public void SetFans(long lifetimeFans)
        {
            _wanted = Mathf.Max(_wanted, CrowdRules.Count(lifetimeFans));
            while (_figures.Count < _wanted) Add();
        }

        void Add()
        {
            int i = _figures.Count;
            // a loose grid with jitter, rows toward the stage first, so a small crowd gathers near the front
            int perRow = Mathf.Max(3, Mathf.RoundToInt(_extent.x * 2f / 1.1f));
            int row = i / perRow, col = i % perRow;
            float u = perRow > 1 ? (col + 0.5f * (row % 2)) / perRow : 0.5f;
            float x = Mathf.Lerp(-_extent.x, _extent.x, Mathf.Clamp01(u)) + Jitter(0.25f);
            float rowsMax = Mathf.Max(1, Mathf.CeilToInt(36f / perRow));
            float z = Mathf.Lerp(_extent.y, -_extent.y, Mathf.Clamp01(row / rowsMax)) + Jitter(0.25f);
            Vector3 right = Vector3.Cross(Vector3.up, _facing);
            Vector3 pos = _center.position + right * x + _facing * z;
            pos.y = 0f;
            CrowdFigure pose;
            Transform root = MakeFigure(i, out pose);
            root.SetParent(transform, true);
            root.position = pos;
            // facing the stage, a little off so the rows are not a parade
            root.rotation = Quaternion.LookRotation(_facing, Vector3.up) * Quaternion.Euler(0f, Jitter(18f), 0f);
            var f = new Figure { Root = root, Pose = pose, Home = pos, Phase = (float)_rng.NextDouble() * 6.28f, Size = (pose != null ? CharacterScale : 1f) * (0.92f + (float)_rng.NextDouble() * 0.2f), AppearStart = Time.time };
            root.localScale = Vector3.one * f.Size * (Time.time > 1f && !GameSettings.ReducedMotion ? 0.01f : 1f);
            _figures.Add(f);
        }

        float Jitter(float amount) { return ((float)_rng.NextDouble() * 2f - 1f) * amount; }

        Transform MakeFigure(int i, out CrowdFigure pose)
        {
            pose = null;
            if (_assets != null && _assets.crowdFigures != null && _assets.crowdFigures.Length > 0)
            {
                // a shuffled walk through the recolours, so neighbours differ
                int n = _assets.crowdFigures.Length;
                var src = _assets.crowdFigures[(i * 5 + _rng.Next(n)) % n];
                if (src != null)
                {
                    pose = Instantiate(src);
                    pose.name = "CrowdFigure" + (i + 1);
                    return pose.transform;
                }
            }
            var root = new GameObject("CrowdFigure" + (i + 1)).transform;
            Material mat = null;
            if (_assets != null && _assets.figureMaterials != null && _assets.figureMaterials.Length > 0)
                mat = _assets.figureMaterials[i % _assets.figureMaterials.Length];
            Prim(_assets != null ? _assets.capsuleMesh : null, "Body", root, new Vector3(0, 0.62f, 0), new Vector3(0.5f, 0.6f, 0.5f), mat);
            Prim(_assets != null ? _assets.sphereMesh : null, "Head", root, new Vector3(0, 1.38f, 0), new Vector3(0.44f, 0.44f, 0.44f), mat);
            return root;
        }

        static void Prim(Mesh mesh, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            if (mesh == null) return;   // no mesh asset: no figure (never a CreatePrimitive: the web build has no physics module for its collider)
            var g = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            g.GetComponent<MeshFilter>().sharedMesh = mesh;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            var r = g.GetComponent<MeshRenderer>();
            if (mat != null) r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void BuildBurst()
        {
            if (_assets == null || _assets.burstMaterial == null) return;
            var go = new GameObject("CheerBurst");
            go.transform.SetParent(transform, false);
            _burst = go.AddComponent<ParticleSystem>();
            var main = _burst.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.4f);
            main.gravityModifier = 0.6f;
            main.maxParticles = 120;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1.0f, 0.55f, 0.8f, 1f), new Color(0.7f, 0.75f, 1f, 1f));
            var emission = _burst.emission;
            emission.enabled = false;
            var shape = _burst.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 32f;
            shape.radius = 0.6f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var col = _burst.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = _assets.burstMaterial;
            rend.renderMode = ParticleSystemRenderMode.Billboard;
        }

        /// <summary>A right answer: the crowd hops and a burst goes up over them. Nothing moves under reduce motion.</summary>
        public void Cheer(Vector3 burstAt)
        {
            if (GameSettings.ReducedMotion) return;
            foreach (Figure f in _figures) f.HopStart = Time.time + Random.value * 0.12f;
            if (_burst != null)
            {
                _burst.transform.position = burstAt + Vector3.up * 1.2f;
                _burst.Emit(48);
            }
        }

        void Update()
        {
            bool still = GameSettings.ReducedMotion;
            for (int i = 0; i < _figures.Count; i++)
            {
                Figure f = _figures[i];
                if (f.Root == null) continue;
                float appear = Mathf.Clamp01((Time.time - f.AppearStart) / 0.3f);
                f.Root.localScale = Vector3.one * f.Size * (still ? 1f : Mathf.Max(0.01f, EaseOutBack(appear)));
                float hop = 0f;
                bool cheering = false;
                if (!still)
                {
                    float t = (Time.time - f.HopStart) / HopSeconds;
                    if (t >= 0f && t < 1f) hop = Mathf.Sin(t * Mathf.PI) * 0.45f;
                    cheering = t >= 0f && t < 1.7f;   // arms stay up a little after the hop
                    hop += 0.025f * Mathf.Sin(Time.time * 2.4f + f.Phase);   // a little sway at rest
                }
                if (f.Pose != null) f.Pose.Cheer(cheering);
                f.Root.position = f.Home + Vector3.up * hop;
            }
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.4f, c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }
    }
}
