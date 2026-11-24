using UnityEngine;

namespace WorldTour.World
{
    /// <summary>Turns slowly around an axis (local space), degrees per second.</summary>
    public sealed class PropSpin : MonoBehaviour
    {
        public Vector3 axis = Vector3.up;
        public float degreesPerSecond = 20f;

        void Update()
        {
            if (PropMotion.Reduced) return;
            transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);
        }
    }
}
