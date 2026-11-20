using System.Collections.Generic;
using TMPro;
using UnityEngine;
using WorldTour.Game.Map;
using WorldTour.Game.Show;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// The trailer picture (truck.js iso SVG, math.css .iso): an <see cref="IsoScene"/> scaled to fit a box, faces back to
    /// front with ink edges, the dashed truck floor, unit lines and the side labels.
    /// </summary>
    public sealed class IsoView : MonoBehaviour
    {
        private RectTransform _rt;
        private ShapeBatch _floor, _main;
        private UiLine _floorDash;
        private RectTransform _labels;
        private readonly List<TMP_Text> _texts = new List<TMP_Text>();
        private float _w, _h;

        public RectTransform Rect { get { return _rt; } }

        public static IsoView Make(RectTransform parent, float w, float h)
        {
            RectTransform rt = SpellingUi.Box("Iso", parent, w, h);
            var v = rt.gameObject.AddComponent<IsoView>();
            v._rt = rt; v._w = w; v._h = h;
            v._floor = Layer(rt, "Floor").gameObject.AddComponent<ShapeBatch>();
            RectTransform d = Layer(rt, "FloorDash");
            v._floorDash = d.gameObject.AddComponent<UiLine>();
            v._floorDash.color = Theme.Color(ColorRole.Card);
            v._main = Layer(rt, "Crates").gameObject.AddComponent<ShapeBatch>();
            v._labels = Layer(rt, "Labels");
            return v;
        }

        private static RectTransform Layer(RectTransform parent, string name)
        {
            RectTransform rt = ShowUi.Rect(name, parent);
            ShowUi.Stretch(rt);
            return rt;
        }

        private static Color FillColor(IsoFill f)
        {
            switch (f)
            {
                case IsoFill.Top: return Theme.Color(ColorRole.Card);
                case IsoFill.Right: return Theme.Color(ColorRole.Accent);
                case IsoFill.Left: return Theme.Color(ColorRole.PaperDeep);
                case IsoFill.TopLit: return Theme.Color(ColorRole.Paper);
                case IsoFill.RightSoft: return MathUi.Mix(ColorRole.Accent, .6f, ColorRole.Card);
                default: return MathUi.Mix(ColorRole.Card, .14f, ColorRole.Ink);
            }
        }

        public void Paint(IsoScene s)
        {
            float scale = Mathf.Min(_w / (float)s.W, _h / (float)s.H);
            float ox = (_w - (float)s.W * scale) / 2f, oy = (_h - (float)s.H * scale) / 2f;
            System.Func<IsoPt, Vector2> px = p => new Vector2(ox + (float)(p.X - s.X0) * scale - _w / 2f, _h / 2f - oy - (float)(p.Y - s.Y0) * scale);
            Color ink = Theme.Color(ColorRole.Ink);
            float edge = Mathf.Max(1f, (float)IsoGeometry.EdgeWidth * scale);

            _floor.Clear(); _main.Clear(); _floorDash.SetPoints(null);
            var pts = new List<Vector2>(4);
            foreach (IsoPoly poly in s.Polys)
            {
                pts.Clear();
                foreach (IsoPt p in poly.Points) pts.Add(px(p));
                if (poly.Fill == IsoFill.Floor)
                {
                    // .truckfloor: card at 14%, a dashed card outline (.15 / .1)
                    _floor.Poly(pts, FillColor(IsoFill.Floor));
                    var ring = new List<Vector2>(pts) { pts[0] };
                    _floorDash.Width = edge;
                    _floorDash.SetDash(.15f * scale, .1f * scale);
                    _floorDash.SetPoints(ring);
                    continue;
                }
                _main.Poly(pts, FillColor(poly.Fill));
                _main.Outline(pts, edge, ink);
            }
            Color ul = ink; ul.a = .55f;
            foreach (IsoLine l in s.Lines) _main.Stroke(px(l.A), px(l.B), Mathf.Max(1f, (float)IsoGeometry.UnitLineWidth * scale), ul);

            float font = Mathf.Max(15f, (float)IsoGeometry.LabelFont * scale);          // readable at 1366 on a big picture (P9)
            for (int i = 0; i < s.Labels.Count; i++)
            {
                TMP_Text t = i < _texts.Count ? _texts[i] : NewLabel();
                IsoLabel l = s.Labels[i];
                t.gameObject.SetActive(true);
                t.fontSize = font;
                t.text = l.Text;
                var rt = t.rectTransform;
                rt.sizeDelta = new Vector2(font * 4f, font * 1.4f);
                // SVG text: x is the start, y the baseline
                rt.anchoredPosition = px(l.At) + new Vector2(0, font * .35f);
            }
            for (int i = s.Labels.Count; i < _texts.Count; i++) _texts[i].gameObject.SetActive(false);
        }

        private TMP_Text NewLabel()
        {
            RectTransform rt = ShowUi.Rect("Dim", _labels);
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.pivot = new Vector2(0f, .5f);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            Theme.ApplyFont(t, FontRole.BodyBold);
            t.color = Theme.Color(ColorRole.OnInk);
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            t.richText = false;
            // .dim: paint-order stroke, ink, .14 wide at a .62 font
            t.outlineWidth = .22f;
            t.outlineColor = Theme.Color(ColorRole.Ink);
            _texts.Add(t);
            return t;
        }
    }
}
