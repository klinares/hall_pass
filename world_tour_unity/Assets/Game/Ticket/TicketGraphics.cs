using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WorldTour.Game.Ticket
{
    /// <summary>
    /// The per-show barcode (ticket.js barcode()), drawn as quads. The web draws a 100x30 SVG stretched into a 60x60 box
    /// and rotated 90 degrees clockwise (screens.css .barcode), so here each bar is a full-width horizontal stripe:
    /// SVG x runs from the top of this rect down, and a bar past x = 100 is clipped as the SVG clips it.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TicketBarcodeGraphic : MaskableGraphic
    {
        readonly List<BarcodeBar> _bars = new List<BarcodeBar>();

        public void SetBars(IReadOnlyList<BarcodeBar> bars)
        {
            _bars.Clear();
            if (bars != null) _bars.AddRange(bars);
            SetVerticesDirty();
        }

        public int BarCount { get { return _bars.Count; } }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float k = r.height / TicketBarcode.ViewWidth;
            foreach (BarcodeBar b in _bars)
            {
                float x0 = Mathf.Clamp(b.X, 0, TicketBarcode.ViewWidth), x1 = Mathf.Clamp(b.X + b.Width, 0, TicketBarcode.ViewWidth);
                if (x1 <= x0) continue;
                float top = r.yMax - x0 * k, bottom = r.yMax - x1 * k;
                Quad(vh, new Vector2(r.xMin, bottom), new Vector2(r.xMax, top));
            }
        }

        void Quad(VertexHelper vh, Vector2 min, Vector2 max)
        {
            int i = vh.currentVertCount;
            Color32 c = color;
            vh.AddVert(new Vector3(min.x, min.y), c, Vector2.zero);
            vh.AddVert(new Vector3(min.x, max.y), c, Vector2.zero);
            vh.AddVert(new Vector3(max.x, max.y), c, Vector2.zero);
            vh.AddVert(new Vector3(max.x, min.y), c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }
    }

    /// <summary>
    /// A glyph neither bundled font has (U+2736 six-pointed star, U+2665 heart), drawn as a filled shape in the text's
    /// colour. Used only when the ticket art has no sprite for it (2.1 exported no star or heart ui icon). The heart is
    /// world_tour/js/fx.js's confetti heart path; the star has six points, like the U+2736 glyph.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TicketGlyphGraphic : MaskableGraphic
    {
        public enum Shape { Star6, Heart }

        [SerializeField] Shape shape;

        public Shape GlyphShape { get { return shape; } set { shape = value; SetVerticesDirty(); } }

        /// <summary>The star's inner radius, as a share of the outer one.</summary>
        public const float StarInner = 0.42f;

        // fx.js heart: M0 .9 C-.9 .3 -1.2 -.3 -.8 -.75 C-.45 -1.05 -.1 -.9 0 -.55 C.1 -.9 .45 -1.05 .8 -.75 C1.2 -.3 .9 .3 0 .9z (y down)
        static readonly Vector2[] HeartCubics =
        {
            new Vector2(0f, .9f), new Vector2(-.9f, .3f), new Vector2(-1.2f, -.3f), new Vector2(-.8f, -.75f),
            new Vector2(-.45f, -1.05f), new Vector2(-.1f, -.9f), new Vector2(0f, -.55f),
            new Vector2(.1f, -.9f), new Vector2(.45f, -1.05f), new Vector2(.8f, -.75f),
            new Vector2(1.2f, -.3f), new Vector2(.9f, .3f), new Vector2(0f, .9f),
        };

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            var outline = new List<Vector2>(64);
            Vector2 centre;
            if (shape == Shape.Star6)
            {
                for (int i = 0; i < 12; i++)
                {
                    float a = (90f - i * 30f) * Mathf.Deg2Rad;
                    float rad = i % 2 == 0 ? 1f : StarInner;
                    outline.Add(new Vector2(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad));
                }
                centre = Vector2.zero;
            }
            else
            {
                for (int s = 0; s + 3 < HeartCubics.Length; s += 3)
                    for (int j = 0; j < 12; j++)
                    {
                        float t = j / 12f, u = 1f - t;
                        Vector2 p = u * u * u * HeartCubics[s] + 3 * u * u * t * HeartCubics[s + 1] + 3 * u * t * t * HeartCubics[s + 2] + t * t * t * HeartCubics[s + 3];
                        outline.Add(new Vector2(p.x, -p.y)); // y up
                    }
                centre = new Vector2(0f, -0.1f);
            }
            // the shapes span about [-1.2, 1.2] x [-1.05, 1]; fit them in the rect
            float scale = Mathf.Min(r.width, r.height) * 0.5f / (shape == Shape.Heart ? 1.2f : 1f);
            Vector2 mid = r.center;
            Color32 c = color;
            vh.AddVert(mid + centre * scale, c, Vector2.zero);
            foreach (Vector2 p in outline) vh.AddVert(mid + p * scale, c, Vector2.zero);
            int n = outline.Count;
            for (int i = 0; i < n; i++) vh.AddTriangle(0, 1 + i, 1 + (i + 1) % n);
        }
    }

    /// <summary>
    /// The wavy underline under a missed word (screens.css:184 <c>text-decoration: underline wavy var(--paper-deep) 2px;
    /// text-underline-offset: 4px</c>), as a sine stroke along the bottom of this rect.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TicketWavyLine : MaskableGraphic
    {
        /// <summary>Stroke width (CSS 2px).</summary>
        public float Thickness = 2f;
        /// <summary>One wave's length in canvas units.</summary>
        public float Wavelength = 8f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float amp = Mathf.Max(0f, (r.height - Thickness) * 0.5f);
            float midY = r.center.y;
            int steps = Mathf.Max(2, Mathf.CeilToInt(r.width / 2f));
            Color32 c = color;
            for (int i = 0; i <= steps; i++)
            {
                float x = r.xMin + r.width * i / steps;
                float ph = (x - r.xMin) / Wavelength * Mathf.PI * 2f;
                float y = midY + Mathf.Sin(ph) * amp;
                vh.AddVert(new Vector3(x, y - Thickness * 0.5f), c, Vector2.zero);
                vh.AddVert(new Vector3(x, y + Thickness * 0.5f), c, Vector2.zero);
                if (i > 0)
                {
                    int b = i * 2;
                    vh.AddTriangle(b - 2, b - 1, b + 1);
                    vh.AddTriangle(b + 1, b, b - 2);
                }
            }
        }
    }
}
