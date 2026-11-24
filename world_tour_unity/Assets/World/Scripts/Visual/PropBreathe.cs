using UnityEngine;

namespace WorldTour.World
{
    /// <summary>A slow rise and fall in scale, like breathing.</summary>
    public sealed class PropBreathe : MonoBehaviour
    {
        public float amount = 0.03f, period = 3.2f;
        Vector3 _base;

        void OnEnable() { _base = transform.localScale; }

        void Update()
        {
            if (PropMotion.Reduced) { transform.localScale = _base; return; }
            float k = Mathf.Sin(Time.time * Mathf.PI * 2f / period + transform.position.x);
            transform.localScale = new Vector3(_base.x * (1f + amount * 0.5f * k), _base.y * (1f + amount * k), _base.z * (1f + amount * 0.5f * k));
        }
    }
}
