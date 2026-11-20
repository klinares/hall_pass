using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// Flat shapes drawn as one uGUI mesh, in the order added: convex polygons (the truck's crate faces, the stage floor),
    /// straight strokes with square caps (grid lines, crate edges) and discs. The math builders' SVG drawings
    /// (stage.js plot, truck.js iso) on Unity. Points are local positions in this RectTransform. Colours are opaque mixes
    /// (the project blends UI in linear space, so CSS's see-through fills are mixed with the desk colour instead).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ShapeBatch : MaskableGraphic
    {
        private struct Shape
        {
            public Vector2[] Pts;
            public Color32 Color;
            public float Width;   // > 0: a stroke from Pts[0] to Pts[1]
        }

        private readonly List<Shape> _shapes = new List<Shape>();

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        public int Count { get { return _shapes.Count; } }

        public void Clear() { _shapes.Clear(); SetVerticesDirty(); }

        /// <summary>A filled convex polygon.</summary>
        public void Poly(IList<Vector2> pts, Color c)
        {
            if (pts == null || pts.Count < 3) return;
            var a = new Vector2[pts.Count];
            pts.CopyTo(a, 0);
            _shapes.Add(new Shape { Pts = a, Color = c });
            SetVerticesDirty();
        }

        /// <summary>The outline of a polygon, as strokes.</summary>
        public void Outline(IList<Vector2> pts, float width, Color c)
        {
            for (int i = 0; i < pts.Count; i++) Stroke(pts[i], pts[(i + 1) % pts.Count], width, c);
        }

        /// <summary>A straight stroke with square caps (half the width past each end), so joins close.</summary>
        public void Stroke(Vector2 a, Vector2 b, float width, Color c)
        {
            if (width <= 0f || (b - a).sqrMagnitude < 1e-8f) return;
            _shapes.Add(new Shape { Pts = new[] { a, b }, Color = c, Width = width });
            SetVerticesDirty();
        }

        /// <summary>A filled disc.</summary>
        public void Disc(Vector2 at, float r, Color c, int segments = 28)
        {
            var p = new Vector2[segments];
            for (int k = 0; k < segments; k++)
            {
                float t = k * Mathf.PI * 2f / segments;
                p[k] = at + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * r;
            }
            _shapes.Add(new Shape { Pts = p, Color = c });
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            foreach (Shape s in _shapes)
            {
                int start = vh.currentVertCount;
                if (s.Width > 0f)
                {
                    Vector2 a = s.Pts[0], b = s.Pts[1], d = (b - a).normalized, n = new Vector2(-d.y, d.x) * (s.Width * .5f);
                    a -= d * (s.Width * .5f); b += d * (s.Width * .5f);
                    vh.AddVert(a + n, s.Color, Vector4.zero); vh.AddVert(b + n, s.Color, Vector4.zero);
                    vh.AddVert(b - n, s.Color, Vector4.zero); vh.AddVert(a - n, s.Color, Vector4.zero);
                    vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
                    continue;
                }
                for (int i = 0; i < s.Pts.Length; i++) vh.AddVert(s.Pts[i], s.Color, Vector4.zero);
                for (int i = 1; i < s.Pts.Length - 1; i++) vh.AddTriangle(start, start + i, start + i + 1);
            }
        }
    }
}
