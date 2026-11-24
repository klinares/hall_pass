using UnityEngine;

namespace WorldTour.World
{
    /// <summary>
    /// Friendly third-person view (phase 8.1 look pass): a fixed rotation, about 30 degrees down, tilted a little back up so she sits in
    /// the lower half of the frame and the fronts, awnings and sky above her read. The position glides after her and leans a short way
    /// into the direction she walks. Lamp posts and trees that stand between the lens and her are not drawn (<see cref="Occluder"/>).
    /// </summary>
    public class FollowCamera : MonoBehaviour
    {
        public Transform target;
        [Tooltip("From her feet to the lens. Height over distance gives the pitch (6.6 / 11.4 = 30 degrees).")]
        public Vector3 offset = new Vector3(0, 6.6f, -11.4f);
        [Tooltip("Extra tilt back up, degrees, so the view reaches the sky and she sits below the middle.")]
        public float tiltUp = 7f;
        public float smoothTime = 0.32f;
        [Tooltip("How far the camera leans ahead of her while she walks, metres.")]
        public float lead = 2.2f;
        [Tooltip("Phase 8.1: while true another script (the stage view) drives the camera and this one leaves it alone.")]
        public bool frozen;
        Vector3 vel, leadVel, leadNow;
        PlayerController _pc;

        /// <summary>Where this camera sits when it follows the target now (no lead: used when handing the camera back).</summary>
        public Vector3 FollowPosition { get { return target != null ? target.position + offset : transform.position; } }
        /// <summary>The fixed follow rotation.</summary>
        public Quaternion FollowRotation { get { return Quaternion.LookRotation(-offset, Vector3.up) * Quaternion.Euler(-tiltUp, 0f, 0f); } }

        void Start()
        {
            transform.rotation = FollowRotation;
            if (target != null) { transform.position = target.position + offset; _pc = target.GetComponent<PlayerController>(); }
        }

        void LateUpdate()
        {
            if (target == null || frozen) return;
            if (_pc == null) _pc = target.GetComponent<PlayerController>();
            Vector3 want = Vector3.zero;
            if (_pc != null && _pc.planarSpeed > 0.3f)
            {
                Vector3 f = target.forward; f.y = 0f;
                want = f.normalized * (lead * Mathf.Clamp01(_pc.planarSpeed / 4f));
            }
            leadNow = Vector3.SmoothDamp(leadNow, want, ref leadVel, 0.6f);
            transform.position = Vector3.SmoothDamp(transform.position, target.position + leadNow + offset, ref vel, smoothTime);
            transform.rotation = FollowRotation;

            Vector3 head = target.position + Vector3.up * 1.3f;
            for (int i = 0; i < Occluder.All.Count; i++) Occluder.All[i].Test(transform.position, head);
        }

        /// <summary>Forget the glide speed (after the camera was moved by hand) and bring every hidden prop back.</summary>
        public void ResetVelocity()
        {
            vel = Vector3.zero; leadVel = Vector3.zero; leadNow = Vector3.zero;
        }

        /// <summary>The stage camera took over: put every prop back so the stage view shows the whole street.</summary>
        public void ShowAllOccluders()
        {
            for (int i = 0; i < Occluder.All.Count; i++) Occluder.All[i].Show();
        }
    }
}
