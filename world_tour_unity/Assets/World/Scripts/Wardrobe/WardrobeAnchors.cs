using System.Linq;
using UnityEngine;

namespace WorldTour.World.Wardrobe
{
    /// <summary>
    /// Phase 8.3: the attachment points on the Star (WearFront, on the chest bone) and riff (WearHead and WearNeck, on his head and neck bones). They are made at run time from
    /// the poses WardrobeBuilder found at the prefabs' rest pose (<see cref="WardrobeAssets.anchors"/>), so the Star, crew and Riff prefabs and the eight scenes are not touched.
    /// An anchor is aligned with the figure and its scale is cancelled, so what hangs on it is laid out in metres.
    /// </summary>
    public static class WardrobeAnchors
    {
        public static Transform Ensure(GameObject root, string name)
        {
            Transform have = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
            if (have != null) return have;
            WardrobeAssets a = WardrobeAssets.Load();
            AnchorSpec spec = a != null ? a.Anchor(name) : null;
            if (spec == null) return null;
            Transform bone = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == spec.bone);
            if (bone == null) return null;
            var g = new GameObject(name);
            g.transform.SetParent(bone, false);
            g.transform.localPosition = spec.position;
            g.transform.localRotation = spec.rotation;
            Vector3 l = bone.lossyScale;
            g.transform.localScale = new Vector3(1f / Mathf.Max(1e-5f, l.x), 1f / Mathf.Max(1e-5f, l.y), 1f / Mathf.Max(1e-5f, l.z));
            return g.transform;
        }
    }
}
