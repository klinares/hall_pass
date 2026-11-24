using UnityEngine;

namespace WorldTour.World
{
    /// <summary>Idle breathing, and a walk cycle (bob, leg and arm swing) driven by the controller's speed.</summary>
    public class CharacterAnim : MonoBehaviour
    {
        public PlayerController controller;
        public Transform rig, armL, armR, legL, legR, ponytail;
        float phase, walk;

        void Update()
        {
            float sp = controller != null ? controller.planarSpeed : 0f;
            walk = Mathf.MoveTowards(walk, Mathf.Clamp01(sp / 3f), Time.deltaTime * 6f);
            phase += Time.deltaTime * (6f + sp * 1.6f) * walk;

            float breathe = Mathf.Sin(Time.time * 2.1f);
            float bob = Mathf.Abs(Mathf.Sin(phase)) * 0.09f * walk;
            rig.localPosition = new Vector3(0, bob, 0);
            rig.localScale = new Vector3(1f - 0.012f * breathe, 1f + 0.025f * breathe, 1f - 0.012f * breathe);

            float swing = Mathf.Sin(phase) * 38f * walk;
            legL.localRotation = Quaternion.Euler(swing, 0, 0);
            legR.localRotation = Quaternion.Euler(-swing, 0, 0);
            armL.localRotation = Quaternion.Euler(-swing * 0.9f, 0, 8f + 3f * breathe * (1 - walk));
            armR.localRotation = Quaternion.Euler(swing * 0.9f, 0, -8f - 3f * breathe * (1 - walk));
            if (ponytail != null) ponytail.localRotation = Quaternion.Euler(20f + 10f * Mathf.Sin(Time.time * 2.1f - 0.6f) + 14f * walk, 0, 0);
        }
    }
}
