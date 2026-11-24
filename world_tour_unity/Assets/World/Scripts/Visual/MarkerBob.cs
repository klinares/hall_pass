using UnityEngine;

namespace WorldTour.World
{
    /// <summary>The floating interaction marker: always faces the camera, bobs, sways and pulses a little. Move <see cref="holder"/> children, not the root.</summary>
    public class MarkerBob : MonoBehaviour
    {
        public Transform holder;
        public float amplitude = 0.16f, speed = 2.6f, sway = 7f, pulse = 0.06f;
        Camera cam;
        float phase;
        Vector3 holderScale = Vector3.one;

        void OnEnable()
        {
            phase = transform.position.x * 1.7f + transform.position.z * 0.9f;
            if (holder != null) holderScale = holder.localScale;
        }

        void LateUpdate()
        {
            if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
            if (holder == null) return;
            float t = Time.time * speed + phase;
            holder.localPosition = new Vector3(0f, Mathf.Sin(t) * amplitude, 0f);
            holder.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.5f) * sway);
            holder.localScale = holderScale * (1f + Mathf.Sin(t * 2f) * pulse);
        }
    }
}
