using UnityEngine;
using UnityEngine.Rendering;

namespace WorldTour.World.Wardrobe
{
    /// <summary>
    /// Phase 8.3: the wardrobe trunk every place has (made at run time from boxes, so no scene changes): a travelling trunk with a lid, brass corners and a pink star
    /// sticker, a clothes rail with four outfits behind it, a gold ring on the ground and a gold star that bobs over it (the marker). Its front faces -z, toward the camera.
    /// The trunk and the rail are blockers (<see cref="Obstacle"/>) so she walks round them.
    /// </summary>
    public static class WardrobeTrunk
    {
        public const string Name = "WardrobeTrunk";
        public const float Scale = 1.3f;

        static Color C(string hex) { Color c; ColorUtility.TryParseHtmlString(hex, out c); return c; }

        public static GameObject Build(Vector3 position, WardrobeAssets a)
        {
            if (a == null || a.cube == null) return null;
            var root = new GameObject(Name);
            root.transform.position = position;
            // the look is built at 1 m scale and shown 1.3 times bigger, so it reads from the follow camera
            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            model.transform.localScale = Vector3.one * Scale;

            Material navy = a.Make("tr_navy", C("#34409a"), 0.25f), lilac = a.Make("tr_lilac", C("#a8a9fc"), 0.25f), brass = a.Make("tr_brass", C("#ffc533"), 0.6f);
            Material pink = a.Make("tr_pink", C("#fdb1cb"), 0.2f), ink = a.Make("tr_ink", C("#1b1a30"), 0.2f), pole = a.Make("tr_pole", C("#d9dbe8"), 0.7f);
            Material[] garments =
            {
                a.Make("tr_g0", C("#ffc4da"), 0.15f), a.Make("tr_g1", C("#ffcfb5"), 0.15f), a.Make("tr_g2", C("#8fe3c4"), 0.15f), a.Make("tr_g3", C("#e8364f"), 0.15f),
            };
            Material glow = a.Make("tr_ring", C("#ffc533"), 0.2f, C("#ffb000") * 1.6f);

            // the trunk: body, lid, two straps, corners, latch, sticker
            Box(model.transform, a, "Body", navy, new Vector3(0f, 0.30f, 0f), new Vector3(1.15f, 0.6f, 0.68f));
            Box(model.transform, a, "Lid", lilac, new Vector3(0f, 0.68f, 0f), new Vector3(1.2f, 0.16f, 0.72f));
            Box(model.transform, a, "StrapL", ink, new Vector3(-0.35f, 0.4f, -0.345f), new Vector3(0.07f, 0.8f, 0.03f));
            Box(model.transform, a, "StrapR", ink, new Vector3(0.35f, 0.4f, -0.345f), new Vector3(0.07f, 0.8f, 0.03f));
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sy in new[] { 0.02f, 0.62f })
                    Box(model.transform, a, "Corner", brass, new Vector3(0.58f * sx, sy + 0.05f, -0.34f), new Vector3(0.09f, 0.09f, 0.06f));
            Box(model.transform, a, "Latch", brass, new Vector3(0f, 0.6f, -0.37f), new Vector3(0.14f, 0.12f, 0.04f));
            var star = new GameObject("Sticker");
            star.transform.SetParent(model.transform, false);
            star.transform.localPosition = new Vector3(0f, 0.3f, -0.35f);
            star.transform.localScale = new Vector3(0.28f, 0.012f, 0.28f);
            star.AddComponent<MeshFilter>().sharedMesh = a.cylinder;
            star.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Renderer(star, pink);

            // the clothes rail behind it
            Box(model.transform, a, "PoleL", pole, new Vector3(-0.85f, 0.8f, 0.55f), new Vector3(0.05f, 1.6f, 0.05f));
            Box(model.transform, a, "PoleR", pole, new Vector3(0.85f, 0.8f, 0.55f), new Vector3(0.05f, 1.6f, 0.05f));
            Box(model.transform, a, "Rail", pole, new Vector3(0f, 1.55f, 0.55f), new Vector3(1.8f, 0.05f, 0.05f));
            for (int i = 0; i < garments.Length; i++)
            {
                float x = -0.6f + i * 0.4f;
                Box(model.transform, a, "Hanger" + i, pole, new Vector3(x, 1.5f, 0.55f), new Vector3(0.03f, 0.1f, 0.03f));
                var j = Box(model.transform, a, "Jacket" + i, garments[i], new Vector3(x, 1.2f, 0.55f), new Vector3(0.3f, 0.5f, 0.07f));
                j.transform.localRotation = Quaternion.Euler(0f, (i % 2 == 0 ? 8f : -8f), 0f);
                var sl = Box(model.transform, a, "Sleeves" + i, garments[i], new Vector3(x, 1.28f, 0.55f), new Vector3(0.44f, 0.12f, 0.06f));
                sl.transform.localRotation = j.transform.localRotation;
            }

            // the ground ring, and the gold star over it
            var ring = new GameObject("Ring");
            ring.transform.SetParent(model.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.03f, 0.1f);
            ring.transform.localScale = new Vector3(1.0f, 1f, 0.85f);
            ring.AddComponent<MeshFilter>().sharedMesh = MeshKit.Torus(1.35f, 0.04f, 36, 6);
            Renderer(ring, glow, false);

            var marker = new GameObject("Marker");
            marker.transform.SetParent(root.transform, false);
            marker.transform.localPosition = new Vector3(0f, 3.1f, 0.4f);
            var holder = new GameObject("Holder");
            holder.transform.SetParent(marker.transform, false);
            holder.AddComponent<MeshFilter>().sharedMesh = MeshKit.Star(0.5f, 0.22f, 0.17f);
            Renderer(holder, glow, false);
            var bob = marker.AddComponent<MarkerBob>();
            bob.holder = holder.transform;

            // blockers: the trunk and the two ends of the rail
            AddObstacle(root.transform, new Vector3(0f, 0f, 0f), 0.75f * Scale);
            AddObstacle(root.transform, new Vector3(-0.85f * Scale, 0f, 0.55f * Scale), 0.3f * Scale);
            AddObstacle(root.transform, new Vector3(0.85f * Scale, 0f, 0.55f * Scale), 0.3f * Scale);
            return root;
        }

        static void AddObstacle(Transform parent, Vector3 local, float radius)
        {
            var g = new GameObject("Blocker");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = local;
            g.AddComponent<Obstacle>().radius = radius;
        }

        static GameObject Box(Transform parent, WardrobeAssets a, string name, Material m, Vector3 pos, Vector3 scale)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = scale;
            g.AddComponent<MeshFilter>().sharedMesh = a.cube;
            Renderer(g, m);
            return g;
        }

        static void Renderer(GameObject g, Material m, bool shadows = true)
        {
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = shadows;
        }
    }
}
