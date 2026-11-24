using UnityEngine;

namespace WorldTour.World
{
    /// <summary>Fixed friendly angle: the rotation never changes, the position glides after the target.</summary>
    public class FollowCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0, 11f, -12.5f);
        public float smoothTime = 0.28f;
        Vector3 vel;

        void Start()
        {
            transform.rotation = Quaternion.LookRotation(-offset, Vector3.up);
            if (target != null) transform.position = target.position + offset;
        }

        void LateUpdate()
        {
            if (target == null) return;
            transform.position = Vector3.SmoothDamp(transform.position, target.position + offset, ref vel, smoothTime);
        }
    }
}
