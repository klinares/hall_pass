using UnityEngine;

namespace WorldTour.World
{
    /// <summary>
    /// A crowd figure baked from a character model: one static mesh per pose (arms down, arms up), three materials (top, head, legs).
    /// No Animator and no skinning, so thirty-six of them cost a few draw calls. <see cref="Cheer"/> swaps the pose.
    /// Made by <c>CrowdArtBuilder</c> in the Editor.
    /// </summary>
    public class CrowdFigure : MonoBehaviour
    {
        public MeshFilter filter;
        public Mesh idle, cheer;

        public void Cheer(bool on)
        {
            if (filter == null) return;
            Mesh m = on && cheer != null ? cheer : idle;
            if (filter.sharedMesh != m) filter.sharedMesh = m;
        }
    }
}
