using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using WorldTour.Core.Meta;

namespace WorldTour.Game.Map
{
    /// <summary>
    /// The tour route's SVG path data (art/map.js <c>curve</c>), read and flattened so uGUI can draw it. Only the two
    /// commands the web route uses: absolute M and C. Points stay in layout units (1000 x 610, y down).
    /// </summary>
    public static class SvgPath
    {
        static readonly Regex Token = new Regex(@"[MC]|-?(?:\d+\.?\d*|\.\d+)", RegexOptions.CultureInvariant);

        /// <summary>A path as cubic segments, each [start, control 1, control 2, end]. Throws FormatException on anything else.</summary>
        public static List<Vector2[]> Parse(string d)
        {
            var segs = new List<Vector2[]>();
            if (string.IsNullOrEmpty(d)) return segs;
            var t = new List<string>();
            foreach (Match m in Token.Matches(d)) t.Add(m.Value);
            int i = 0;
            Vector2 at = Vector2.zero;
            bool started = false;
            char cmd = ' ';
            while (i < t.Count)
            {
                if (t[i] == "M" || t[i] == "C") { cmd = t[i][0]; i++; }
                if (cmd == 'M') { at = new Vector2(Num(t, i), Num(t, i + 1)); i += 2; started = true; cmd = 'C'; continue; }
                if (cmd != 'C' || !started) throw new FormatException("route path must start with M");
                var c1 = new Vector2(Num(t, i), Num(t, i + 1));
                var c2 = new Vector2(Num(t, i + 2), Num(t, i + 3));
                var p = new Vector2(Num(t, i + 4), Num(t, i + 5));
                i += 6;
                segs.Add(new[] { at, c1, c2, p });
                at = p;
            }
            return segs;
        }

        static float Num(List<string> t, int i)
        {
            if (i >= t.Count || t[i] == "M" || t[i] == "C") throw new FormatException("route path ends early");
            return float.Parse(t[i], NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        /// <summary>Evaluates one cubic segment at t in 0..1.</summary>
        public static Vector2 Bezier(Vector2[] s, float t)
        {
            float u = 1f - t;
            return u * u * u * s[0] + 3f * u * u * t * s[1] + 3f * u * t * t * s[2] + t * t * t * s[3];
        }

        /// <summary>The path as a polyline, <paramref name="steps"/> pieces per segment (the first point once).</summary>
        public static List<Vector2> Flatten(List<Vector2[]> segs, int steps = 24)
        {
            var pts = new List<Vector2>();
            if (segs.Count == 0) return pts;
            pts.Add(segs[0][0]);
            foreach (Vector2[] s in segs)
                for (int k = 1; k <= steps; k++) pts.Add(Bezier(s, k / (float)steps));
            return pts;
        }

        /// <summary>Total length of a polyline.</summary>
        public static float Length(IReadOnlyList<Vector2> pts)
        {
            float l = 0f;
            for (int i = 1; i < pts.Count; i++) l += Vector2.Distance(pts[i - 1], pts[i]);
            return l;
        }

        /// <summary>The first <paramref name="length"/> units of a polyline (all of it when longer than the line).</summary>
        public static List<Vector2> Cut(IReadOnlyList<Vector2> pts, float length)
        {
            var o = new List<Vector2>();
            if (pts.Count == 0) return o;
            o.Add(pts[0]);
            float left = Mathf.Max(0f, length);
            for (int i = 1; i < pts.Count; i++)
            {
                float d = Vector2.Distance(pts[i - 1], pts[i]);
                if (d >= left) { if (d > 0f) o.Add(Vector2.Lerp(pts[i - 1], pts[i], left / d)); return o; }
                o.Add(pts[i]);
                left -= d;
            }
            return o;
        }

        /// <summary>
        /// stroke-dasharray: the "on" pieces of a polyline for dashes <paramref name="dash"/> long with
        /// <paramref name="gap"/> between them, starting with a dash at the first point.
        /// </summary>
        public static List<List<Vector2>> Dashes(IReadOnlyList<Vector2> line, float dash, float gap)
        {
            var pieces = new List<List<Vector2>>();
            if (line.Count < 2) return pieces;
            if (dash <= 0f || gap <= 0f) { pieces.Add(new List<Vector2>(line)); return pieces; }
            bool on = true;
            float left = dash;
            var cur = new List<Vector2> { line[0] };
            for (int i = 1; i < line.Count; i++)
            {
                Vector2 a = line[i - 1], b = line[i];
                float d = Vector2.Distance(a, b), t = 0f;
                while (d - t > left)
                {
                    t += left;
                    Vector2 p = d > 0f ? Vector2.Lerp(a, b, t / d) : b;
                    if (on) { cur.Add(p); pieces.Add(cur); cur = null; }
                    else cur = new List<Vector2> { p };
                    on = !on;
                    left = on ? dash : gap;
                }
                left -= d - t;
                if (on) cur.Add(b);
            }
            if (on && cur != null && cur.Count > 1) pieces.Add(cur);
            return pieces;
        }

        /// <summary>
        /// art/map.js:22 <c>curve(points)</c>: a smooth curve through the points (Catmull-Rom as cubic Bezier), as the
        /// same path text the web writes (control points with toFixed(1), the points as given). Used to check the
        /// manifest's curves against the layout.
        /// </summary>
        public static string Curve(IReadOnlyList<Vector2> p)
        {
            if (p.Count < 2) return "";
            var sb = new StringBuilder();
            sb.Append('M').Append(N(p[0].x)).Append(' ').Append(N(p[0].y));
            for (int i = 0; i < p.Count - 1; i++)
            {
                Vector2 p0 = i > 0 ? p[i - 1] : p[i], p1 = p[i], p2 = p[i + 1], p3 = i + 2 < p.Count ? p[i + 2] : p2;
                double c1x = p1.x + (p2.x - (double)p0.x) / 6, c1y = p1.y + (p2.y - (double)p0.y) / 6;
                double c2x = p2.x - (p3.x - (double)p1.x) / 6, c2y = p2.y - (p3.y - (double)p1.y) / 6;
                sb.Append(" C").Append(F1(c1x)).Append(' ').Append(F1(c1y)).Append(' ').Append(F1(c2x)).Append(' ').Append(F1(c2y))
                  .Append(' ').Append(N(p2.x)).Append(' ').Append(N(p2.y));
            }
            return sb.ToString();
        }

        /// <summary>JS <c>String(n)</c> for the whole-number layout points.</summary>
        static string N(float v) { return ((long)v).ToString(CultureInfo.InvariantCulture); }

        /// <summary>JS <c>v.toFixed(1)</c>.</summary>
        static string F1(double v) { return JsNum.ToFixedNumber(v, 1).ToString("0.0", CultureInfo.InvariantCulture); }
    }
}
