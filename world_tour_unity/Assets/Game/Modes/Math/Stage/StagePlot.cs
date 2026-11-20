using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WorldTour.Game.Map;
using WorldTour.Game.Show;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// The stage plot (stage.js plot(), math.css .plot): the first quadrant drawn on the ink desk, with the lights. A press
    /// snaps the light to the nearest point and dragging carries it from point to point; with the plot focused (Tab, or a
    /// click), the arrow keys move the light and Enter or Space sets it. Reports in plot units (x right, y down); the rules
    /// are <see cref="StageGame"/>'s.
    /// </summary>
    public sealed class StagePlot : Selectable, IPointerDownHandler, IDragHandler, IMoveHandler, ISubmitHandler
    {
        /// <summary>A press at a spot in plot units.</summary>
        public Action<double, double> Pressed;
        /// <summary>The pointer dragged to a spot in plot units.</summary>
        public Action<double, double> Dragged;
        /// <summary>An arrow key: dx, dy (up is +1).</summary>
        public Action<int, int> Nudged;
        /// <summary>Enter or Space.</summary>
        public Action Submitted;

        private int _max;
        private float _side;
        private bool _read;
        private ShapeBatch _batch;
        private UiLine _route;
        private readonly List<TMP_Text> _bottom = new List<TMP_Text>();
        private Lamp _start, _set, _ghost;
        private int _askedFrame = -1;

        public RectTransform Rect { get { return (RectTransform)transform; } }
        public RectTransform SetLamp { get { return _set.Rect; } }
        public RectTransform GhostLamp { get { return _ghost.Rect; } }

        /// <summary>One light: halo, ink ring, bulb (stage.js:24 lamp(); math.css .lamp).</summary>
        private sealed class Lamp
        {
            public RectTransform Rect;
            public Image Halo, Ring, Bulb;
            public UiLine Dash;
        }

        public static StagePlot Make(RectTransform parent, int max, float side, bool read)
        {
            RectTransform rt = SpellingUi.Box("Plot", parent, side, side);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(1, 1, 1, 0);                                   // the press target; the floor is drawn below
            var plot = rt.gameObject.AddComponent<StagePlot>();
            plot.targetGraphic = hit;
            plot.transition = Transition.None;
            plot._max = max; plot._side = side; plot._read = read;
            plot.Build();
            return plot;
        }

        /// <summary>Plot units to local pixels (the RectTransform's pivot is its centre).</summary>
        public Vector2 Px(double ux, double uy)
        {
            float s = _side / (float)StageGame.Side(_max);
            return new Vector2((float)ux * s - _side / 2f, _side / 2f - (float)uy * s);
        }

        private float S { get { return _side / (float)StageGame.Side(_max); } }

        private void Build()
        {
            float s = S;
            // stage.js:28 the floor (card at 6%) over the grid's own square, then the grid lines, then the axes
            RectTransform b = ShowUi.Rect("Grid", transform);
            ShowUi.Stretch(b);
            _batch = b.gameObject.AddComponent<ShapeBatch>();
            Color gl = MathUi.Mix(ColorRole.Card, .35f, ColorRole.Ink), axis = Theme.Color(ColorRole.Card);
            _batch.Poly(new[] { Px(StageGame.PlotX(_max, 0), StageGame.PlotY(_max, 0)), Px(StageGame.PlotX(_max, _max), StageGame.PlotY(_max, 0)),
                Px(StageGame.PlotX(_max, _max), StageGame.PlotY(_max, _max)), Px(StageGame.PlotX(_max, 0), StageGame.PlotY(_max, _max)) },
                MathUi.Mix(ColorRole.Card, .06f, ColorRole.Ink));                 // .plot .floor
            foreach (StagePlotLayout.Line l in StagePlotLayout.Lines(_max))
                if (!l.Axis) _batch.Stroke(Px(l.X1, l.Y1), Px(l.X2, l.Y2), Mathf.Max(1f, (float)StagePlotLayout.GridLineWidth * s), gl);
            foreach (StagePlotLayout.Line l in StagePlotLayout.Lines(_max))
                if (l.Axis) _batch.Stroke(Px(l.X1, l.Y1), Px(l.X2, l.Y2), (float)StagePlotLayout.AxisWidth * s, axis);

            foreach (StagePlotLayout.Label l in StagePlotLayout.Labels(_max, null))
            {
                float size = (float)(l.Letter ? StagePlotLayout.LetterFont : StagePlotLayout.AxisFont) * s;
                TMP_Text t = Text(l.Text, l.X, l.Y, size, l.Letter ? FontRole.Display : FontRole.BodyBold, l.Letter ? ColorRole.Accent : ColorRole.OnInk, l.Letter);
                if (!l.Letter && Math.Abs(l.Y - (StageGame.PlotY(_max, 0) + StagePlotLayout.BottomLabelDrop)) < 1e-9) _bottom.Add(t);
            }

            RectTransform r = ShowUi.Rect("Route", transform);
            ShowUi.Stretch(r);
            _route = r.gameObject.AddComponent<UiLine>();
            _route.Width = (float)StagePlotLayout.RouteWidth * s;
            _route.SetDash((float)StagePlotLayout.RouteDash * s, (float)StagePlotLayout.RouteGap * s);
            _route.color = Theme.Color(ColorRole.PaperDeep);

            _start = MakeLamp("Start", s, ColorRole.Card, true, ColorRole.Card);
            _set = MakeLamp("Set", s, ColorRole.Paper, false, ColorRole.Accent);
            _ghost = MakeLamp("Ghost", s, ColorRole.PaperDeep, false, ColorRole.PaperDeep);
        }

        private TMP_Text Text(string text, double x, double y, float size, FontRole font, ColorRole color, bool startAnchored)
        {
            RectTransform rt = ShowUi.Rect("Ax " + text, transform);
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.pivot = new Vector2(startAnchored ? 0f : .5f, .5f);
            rt.sizeDelta = new Vector2(size * 3f, size * 1.4f);
            // SVG text sits on its baseline; centre the box about a third of the size above it
            rt.anchoredPosition = Px(x, y) + new Vector2(0, size * .35f);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            Theme.ApplyFont(t, font);
            t.fontSize = size;
            t.color = Theme.Color(color);
            t.alignment = startAnchored ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Midline;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        private Lamp MakeLamp(string name, float s, ColorRole bulb, bool dashed, ColorRole halo)
        {
            var l = new Lamp();
            l.Rect = ShowUi.Rect("Lamp " + name, transform);
            l.Rect.anchorMin = l.Rect.anchorMax = new Vector2(.5f, .5f);
            float hd = (float)StagePlotLayout.HaloR * 2f * s, bd = (float)StagePlotLayout.BulbR * 2f * s, st = (float)StagePlotLayout.BulbStroke * s;
            l.Rect.sizeDelta = new Vector2(hd, hd);
            l.Halo = Disc(l.Rect, "Halo", hd, halo == ColorRole.PaperDeep ? MathUi.Mix(ColorRole.PaperDeep, .5f, ColorRole.Ink) : MathUi.Mix(ColorRole.Accent, .45f, ColorRole.Ink));
            l.Halo.enabled = !dashed;                                               // .lamp.start .halo { fill: none }
            l.Ring = Disc(l.Rect, "Ring", bd + st, Theme.Color(ColorRole.Ink));
            l.Bulb = Disc(l.Rect, "Bulb", bd - st, Theme.Color(bulb));
            if (dashed)
            {
                // .lamp.start .bulb: stroke-dasharray .08 .06 on the ink ring
                l.Ring.color = Theme.Color(bulb);
                RectTransform d = ShowUi.Rect("Dash", l.Rect);
                ShowUi.Stretch(d);
                l.Dash = d.gameObject.AddComponent<UiLine>();
                l.Dash.Width = st;
                l.Dash.SetDash(.08f * s, .06f * s);
                l.Dash.color = Theme.Color(ColorRole.Ink);
                var pts = new List<Vector2>();
                float rr = bd / 2f;
                for (int k = 0; k <= 32; k++) { float a = k * Mathf.PI * 2f / 32; pts.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr); }
                l.Dash.SetPoints(pts);
            }
            l.Rect.gameObject.SetActive(false);
            return l;
        }

        private static Image Disc(RectTransform parent, string name, float d, Color c)
        {
            RectTransform rt = ShowUi.Rect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.sizeDelta = new Vector2(d, d);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UiShapes.Circle;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>stage.js draw(): the lights, the route and the big bottom-line number.</summary>
        public void Paint(StageScene sc)
        {
            Place(_start, sc.Start);
            Place(_set, sc.Light);
            Place(_ghost, sc.Ghost);
            if (sc.Path == null) _route.SetPoints(null);
            else
            {
                var pts = new List<Vector2>();
                foreach (GridPt g in sc.Path.Points) pts.Add(Px(StageGame.PlotX(_max, g.X), StageGame.PlotY(_max, g.Y)));
                _route.SetPoints(pts);
            }
            float s = S;
            for (int i = 0; i < _bottom.Count; i++)
            {
                bool on = sc.Highlight.HasValue && sc.Highlight.Value == i;
                _bottom[i].fontSize = (float)(on ? StagePlotLayout.AxisFontOn : StagePlotLayout.AxisFont) * s;
                _bottom[i].color = Theme.Color(on ? ColorRole.PaperDeep : ColorRole.OnInk);
            }
        }

        private void Place(Lamp l, GridPt? g)
        {
            l.Rect.gameObject.SetActive(g.HasValue);
            if (g.HasValue) l.Rect.anchoredPosition = Px(StageGame.PlotX(_max, g.Value.X), StageGame.PlotY(_max, g.Value.Y));
        }

        /// <summary>The page-pixel centre of grid point (x, y), for the browser tests' clicks.</summary>
        public Vector2 ScreenPoint(int x, int y)
        {
            Vector3 w = Rect.TransformPoint(Px(StageGame.PlotX(_max, x), StageGame.PlotY(_max, y)));
            Canvas c = GetComponentInParent<Canvas>();
            Camera cam = c != null && c.renderMode != RenderMode.ScreenSpaceOverlay ? c.worldCamera : null;
            return RectTransformUtility.WorldToScreenPoint(cam, w);
        }

        private bool ToPlot(PointerEventData e, out double ux, out double uy)
        {
            ux = uy = 0;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, e.position, e.pressEventCamera, out local)) return false;
            Rect r = Rect.rect;
            StagePlotLayout.ToPlot(_max, r.width, r.height, local.x - r.xMin, local.y - r.yMin, out ux, out uy);
            return true;
        }

        public override void OnPointerDown(PointerEventData e)
        {
            base.OnPointerDown(e);                                               // selects it, so the arrows work after a click
            if (_read || e.button != PointerEventData.InputButton.Left || !IsInteractable()) return;
            double ux, uy;
            if (ToPlot(e, out ux, out uy) && Pressed != null) Pressed(ux, uy);
        }

        public void OnDrag(PointerEventData e)
        {
            if (_read || e.button != PointerEventData.InputButton.Left || !IsInteractable()) return;
            double ux, uy;
            if (ToPlot(e, out ux, out uy) && Dragged != null) Dragged(ux, uy);
        }

        /// <summary>The arrows move the light instead of the focus (Tab still leaves the plot).</summary>
        public override void OnMove(AxisEventData e)
        {
            if (_read || !IsInteractable()) { base.OnMove(e); return; }
            int dx = e.moveDir == MoveDirection.Left ? -1 : e.moveDir == MoveDirection.Right ? 1 : 0;
            int dy = e.moveDir == MoveDirection.Down ? -1 : e.moveDir == MoveDirection.Up ? 1 : 0;
            if (dx == 0 && dy == 0) return;
            e.Use();
            if (Nudged != null) Nudged(dx, dy);
        }

        public void OnSubmit(BaseEventData e) { if (IsInteractable()) Ask(); }

        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame && IsInteractable() && EventSystem.current != null
                && EventSystem.current.currentSelectedGameObject == gameObject) Ask();
        }

        private void Ask()
        {
            if (_askedFrame == Time.frameCount) return;
            _askedFrame = Time.frameCount;
            if (Submitted != null) Submitted();
        }
    }
}
