using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WorldTour.Game.Map
{
    /// <summary>
    /// A thick polyline with round caps and joins, optionally dashed, drawn as a uGUI mesh (the tour route: art/map.js
    /// mapSVG's three strokes). Points are local positions in this RectTransform. <see cref="Reveal"/> draws only the
    /// first part of the line by length, so the route can grow toward a newly unlocked venue.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UiLine : MaskableGraphic
    {
        readonly List<Vector2> _pts = new List<Vector2>();
        float _width = 4f, _dash, _gap, _reveal = -1f;
        const int CapSegments = 8;

        public float Width { get { return _width; } set { _width = value; SetVerticesDirty(); } }

        /// <summary>Dash and gap lengths (stroke-dasharray); 0 dash = solid.</summary>
        public void SetDash(float dash, float gap) { _dash = dash; _gap = gap; SetVerticesDirty(); }

        /// <summary>Length to draw from the start; negative = all of it.</summary>
        public float Reveal { get { return _reveal; } set { _reveal = value; SetVerticesDirty(); } }

        public float TotalLength { get { return SvgPath.Length(_pts); } }

        public void SetPoints(IEnumerable<Vector2> pts)
        {
            _pts.Clear();
            if (pts != null) _pts.AddRange(pts);
            SetVerticesDirty();
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_pts.Count < 2 || _width <= 0f) return;
            IReadOnlyList<Vector2> line = _reveal >= 0f ? SvgPath.Cut(_pts, _reveal) : (IReadOnlyList<Vector2>)_pts;
            if (line.Count < 2) return;
            float r = _width * 0.5f;
            if (_dash <= 0f) { Stroke(vh, line, r); return; }

            foreach (List<Vector2> piece in SvgPath.Dashes(line, _dash, _gap)) Stroke(vh, piece, r);
        }

        /// <summary>One solid piece: a quad per segment plus round discs at every joint (round caps and joins).</summary>
        void Stroke(VertexHelper vh, IReadOnlyList<Vector2> p, float r)
        {
            Color32 c = color;
            for (int i = 1; i < p.Count; i++)
            {
                Vector2 a = p[i - 1], b = p[i], dir = b - a;
                if (dir.sqrMagnitude < 1e-8f) continue;
                Vector2 n = new Vector2(-dir.y, dir.x).normalized * r;
                int s = vh.currentVertCount;
                vh.AddVert(a + n, c, Vector4.zero); vh.AddVert(b + n, c, Vector4.zero);
                vh.AddVert(b - n, c, Vector4.zero); vh.AddVert(a - n, c, Vector4.zero);
                vh.AddTriangle(s, s + 1, s + 2); vh.AddTriangle(s, s + 2, s + 3);
            }
            for (int i = 0; i < p.Count; i++) Disc(vh, p[i], r, c);
        }

        static void Disc(VertexHelper vh, Vector2 at, float r, Color32 c)
        {
            int s = vh.currentVertCount;
            vh.AddVert(at, c, Vector4.zero);
            for (int k = 0; k <= CapSegments * 2; k++)
            {
                float a = k * Mathf.PI / CapSegments;
                vh.AddVert(at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, c, Vector4.zero);
                if (k > 0) vh.AddTriangle(s, s + k, s + k + 1);
            }
        }
    }

    /// <summary>The map's graph-paper lines (screens.css:45-50: 1 px accent lines at 22% every 28 px on the card colour).</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UiGrid : MaskableGraphic
    {
        float _step = 28f, _line = 1f;

        public void Set(float step, float line) { _step = Mathf.Max(4f, step); _line = line; SetVerticesDirty(); }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            Color32 c = color;
            for (float x = r.xMin; x <= r.xMax; x += _step) Quad(vh, new Rect(x, r.yMin, _line, r.height), c);
            for (float y = r.yMax; y >= r.yMin; y -= _step) Quad(vh, new Rect(r.xMin, y - _line, r.width, _line), c);
        }

        static void Quad(VertexHelper vh, Rect q, Color32 c)
        {
            int s = vh.currentVertCount;
            vh.AddVert(new Vector2(q.xMin, q.yMin), c, Vector4.zero); vh.AddVert(new Vector2(q.xMin, q.yMax), c, Vector4.zero);
            vh.AddVert(new Vector2(q.xMax, q.yMax), c, Vector4.zero); vh.AddVert(new Vector2(q.xMax, q.yMin), c, Vector4.zero);
            vh.AddTriangle(s, s + 1, s + 2); vh.AddTriangle(s, s + 2, s + 3);
        }
    }
}
