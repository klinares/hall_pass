using UnityEngine;

namespace WorldTour.World
{
    /// <summary>The dog trots after the target and stops a little behind her; the tail always wags.</summary>
    public class DogFollower : MonoBehaviour
    {
        public Transform target;
        public Transform rig, tail, legFL, legFR, legBL, legBR;
        public float followDistance = 1.7f, speed = 5.6f;
        [Tooltip("Optional. Found on the figure when empty. Gets the float parameter \"Speed\" (phase 8.1 contract).")]
        public Animator animator;
        float phase, trot;

        void Awake() { if (animator == null) animator = GetComponentInChildren<Animator>(); }

        void Update()
        {
            Vector3 p = transform.position;
            Vector3 to = target.position - p; to.y = 0;
            float d = to.magnitude;
            float moved = 0f;
            if (d > followDistance)
            {
                float step = Mathf.Min(speed * Mathf.Clamp01((d - followDistance) * 1.2f + 0.25f) * Time.deltaTime, d - followDistance);
                Vector3 np = Obstacle.PushOut(p + (to / d) * step, 0.3f);
                np.y = 0;
                moved = (np - p).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
                transform.position = np;
            }
            if (d > 0.2f)
            {
                Quaternion want = Quaternion.LookRotation(to / d, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-9f * Time.deltaTime));
            }
            AnimSafe.Float(animator, "Speed", moved);
            trot = Mathf.MoveTowards(trot, Mathf.Clamp01(moved / 2f), Time.deltaTime * 6f);
            phase += Time.deltaTime * (9f + moved) * trot;
            float s = Mathf.Sin(phase) * 35f * trot;
            legFL.localRotation = Quaternion.Euler(s, 0, 0);
            legBR.localRotation = Quaternion.Euler(s, 0, 0);
            legFR.localRotation = Quaternion.Euler(-s, 0, 0);
            legBL.localRotation = Quaternion.Euler(-s, 0, 0);
            rig.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(phase)) * 0.06f * trot + 0.01f * Mathf.Sin(Time.time * 2.6f), 0);
            tail.localRotation = Quaternion.Euler(-35f, Mathf.Sin(Time.time * 14f) * 28f, 0);
        }
    }
}
