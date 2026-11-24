using UnityEngine;

namespace WorldTour.World
{
    /// <summary>
    /// Idle life for a crew member: within <see cref="range"/> metres of the player she turns her body toward the player and the
    /// head follows (humanoid look-at IK, so the Animator layer needs its IK pass on); farther away she eases back to the way she
    /// was facing. No Physics. Put it on the object that holds the Animator.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class LookAtPlayer : MonoBehaviour
    {
        public Transform target;
        public float range = 4f;
        public float turnSpeed = 5f;
        public float headHeight = 1.5f;
        Animator anim;
        Quaternion home;
        float weight, nextFind;
        bool near;

        void Awake()
        {
            anim = GetComponent<Animator>();
            home = transform.rotation;
        }

        void Update()
        {
            if (target == null && Time.unscaledTime >= nextFind)
            {
                nextFind = Time.unscaledTime + 1f;
                var pc = FindFirstObjectByType<PlayerController>();
                if (pc != null) target = pc.transform;
            }
            Quaternion want = home;
            near = false;
            if (target != null)
            {
                Vector3 to = target.position - transform.position; to.y = 0;
                if (to.sqrMagnitude < range * range && to.sqrMagnitude > 0.04f)
                {
                    near = true;
                    want = Quaternion.LookRotation(to.normalized, Vector3.up);
                }
            }
            transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
            weight = Mathf.MoveTowards(weight, near ? 1f : 0f, Time.deltaTime * 3f);
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (target == null || weight < 0.01f) { anim.SetLookAtWeight(0f); return; }
            anim.SetLookAtWeight(weight, 0.25f, 0.85f, 1f, 0.5f);
            anim.SetLookAtPosition(target.position + Vector3.up * headHeight);
        }
    }
}
