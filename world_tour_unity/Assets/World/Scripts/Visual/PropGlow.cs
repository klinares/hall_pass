using UnityEngine;

namespace WorldTour.World
{
    /// <summary>
    /// Pulses the emission of its renderers between two strengths, with an occasional short dip (a neon sign). Uses a property block, so the
    /// shared material is not touched.
    /// </summary>
    public sealed class PropGlow : MonoBehaviour
    {
        public Color emission = Color.white;
        public float low = 0.8f, high = 1.15f, speed = 1.4f;
        [Tooltip("Seconds between the short dips; 0 = never.")]
        public float flickerEvery = 0f;
        Renderer[] _renderers;
        MaterialPropertyBlock _block;
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        float _dipAt, _dipUntil;

        void OnEnable()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _block = new MaterialPropertyBlock();
            _dipAt = Time.time + flickerEvery * (0.5f + Mathf.Abs(Mathf.Sin(transform.position.x * 3.1f)));
        }

        void Update()
        {
            float k = PropMotion.Reduced ? (low + high) * 0.5f : Mathf.Lerp(low, high, 0.5f + 0.5f * Mathf.Sin(Time.time * speed + transform.position.x));
            if (!PropMotion.Reduced && flickerEvery > 0f)
            {
                if (Time.time >= _dipAt) { _dipUntil = Time.time + 0.12f; _dipAt = Time.time + flickerEvery * (0.6f + Random.value * 0.8f); }
                if (Time.time < _dipUntil) k *= 0.35f;
            }
            _block.SetColor(EmissionColor, emission * k);
            for (int i = 0; i < _renderers.Length; i++) if (_renderers[i] != null) _renderers[i].SetPropertyBlock(_block);
        }
    }
}
