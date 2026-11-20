using System;
using System.Collections.Generic;
using System.Globalization;

namespace WorldTour.Game.Modes
{
    /// <summary>A point in the isometric drawing's units (x right, y down, as truck.js's SVG).</summary>
    public struct IsoPt
    {
        public double X, Y;
        public IsoPt(double x, double y) { X = x; Y = y; }
    }

    /// <summary>A face's fill (math.css .iso).</summary>
    public enum IsoFill
    {
        /// <summary>.ft: card.</summary>
        Top,
        /// <summary>.fr: accent.</summary>
        Right,
        /// <summary>.fl: paper-deep.</summary>
        Left,
        /// <summary>.cube.lit .ft and .solid.b .ft: paper.</summary>
        TopLit,
        /// <summary>.solid.b .fr: accent mixed 60% with card.</summary>
        RightSoft,
        /// <summary>.truckfloor: card at 14%, dashed card outline.</summary>
        Floor,
    }

    public sealed class IsoPoly
    {
        public IsoPt[] Points;
        public IsoFill Fill;
    }

    /// <summary>A unit line on a solid (.ul: ink, .025 wide, 55% opacity).</summary>
    public struct IsoLine { public IsoPt A, B; }

    /// <summary>A side's length on a solid (.dim), at the text's start on the baseline.</summary>
    public struct IsoLabel
    {
        public IsoPt At;
        public string Text;
        /// <summary>"ld", "wd" or "hd": length, width, height.</summary>
        public string Side;
    }

    /// <summary>One drawing: faces back to front, then unit lines and labels, in a box (truck.js frame's viewBox).</summary>
    public sealed class IsoScene
    {
        public readonly List<IsoPoly> Polys = new List<IsoPoly>();
        public readonly List<IsoLine> Lines = new List<IsoLine>();
        public readonly List<IsoLabel> Labels = new List<IsoLabel>();
        public double X0, Y0, W, H;
        /// <summary>How many unit crates are drawn (crates() only).</summary>
        public int Cubes;
        /// <summary>How many of them have the lit top (hint 2's top layer).</summary>
        public int LitCubes;
    }

    /// <summary>Which unit lines a solid shows.</summary>
    public enum IsoLines { None, Top, All }

    public struct IsoBox
    {
        public int L, W, H;
        public IsoBox(int l, int w, int h) { L = l; W = w; H = h; }
    }

    /// <summary>
    /// world_tour/modes/math/truck.js's isometric drawing (P, cube, box, frame, cornersOf, crates), as plain geometry.
    /// x runs right-down, y runs left-down, z up. Line widths and the label font are math.css's, in the same units.
    /// </summary>
    public static class IsoGeometry
    {
        private static readonly double C = Math.Cos(Math.PI / 6);
        private const double Sn = .5;

        /// <summary>math.css .iso polygon stroke-width; .ul stroke-width; .dim font size and its outline.</summary>
        public const double EdgeWidth = .05, UnitLineWidth = .025, LabelFont = .62, LabelOutline = .14;
        /// <summary>math.css .iso max-height: 300px (and max-width 420px).</summary>
        public const float MaxHeightPx = 300f, MaxWidthPx = 420f;

        /// <summary>truck.js:10 P(x, y, z).</summary>
        public static IsoPt P(double x, double y, double z) { return new IsoPt((x - y) * C, (x + y) * Sn - z); }

        /// <summary>truck.js:13-18 one unit crate: top, right, left faces.</summary>
        public static void Cube(IsoScene s, int x, int y, int z, bool lit)
        {
            s.Polys.Add(new IsoPoly { Fill = lit ? IsoFill.TopLit : IsoFill.Top, Points = new[] { P(x, y, z + 1), P(x + 1, y, z + 1), P(x + 1, y + 1, z + 1), P(x, y + 1, z + 1) } });
            s.Polys.Add(new IsoPoly { Fill = IsoFill.Right, Points = new[] { P(x + 1, y, z), P(x + 1, y + 1, z), P(x + 1, y + 1, z + 1), P(x + 1, y, z + 1) } });
            s.Polys.Add(new IsoPoly { Fill = IsoFill.Left, Points = new[] { P(x, y + 1, z), P(x + 1, y + 1, z), P(x + 1, y + 1, z + 1), P(x, y + 1, z + 1) } });
            s.Cubes++;
            if (lit) s.LitCubes++;
        }

        /// <summary>truck.js:21-45 a solid box at an origin, with unit lines and side labels. soft: the riser (.solid.b).</summary>
        public static void Box(IsoScene s, IsoBox b, int ox, int oy, int oz, bool lines, bool labels, bool hideH, bool soft)
        {
            Box(s, b, ox, oy, oz, lines ? IsoLines.All : IsoLines.None, labels, hideH, soft);
        }

        /// <summary>truck.js box() with a choice of unit lines: none, the top face only (one layer), or all of them.</summary>
        public static void Box(IsoScene s, IsoBox b, int ox, int oy, int oz, IsoLines lines, bool labels, bool hideH, bool soft)
        {
            int l = b.L, w = b.W, h = b.H;
            Func<double, double, double, IsoPt> q = (x, y, z) => P(ox + x, oy + y, oz + z);
            s.Polys.Add(new IsoPoly { Fill = soft ? IsoFill.TopLit : IsoFill.Top, Points = new[] { q(0, 0, h), q(l, 0, h), q(l, w, h), q(0, w, h) } });
            s.Polys.Add(new IsoPoly { Fill = soft ? IsoFill.RightSoft : IsoFill.Right, Points = new[] { q(l, 0, 0), q(l, w, 0), q(l, w, h), q(l, 0, h) } });
            s.Polys.Add(new IsoPoly { Fill = IsoFill.Left, Points = new[] { q(0, w, 0), q(l, w, 0), q(l, w, h), q(0, w, h) } });
            if (lines == IsoLines.All)
            {
                for (int i = 1; i < l; i++) { s.Lines.Add(new IsoLine { A = q(i, 0, h), B = q(i, w, h) }); s.Lines.Add(new IsoLine { A = q(i, w, 0), B = q(i, w, h) }); }
                for (int j = 1; j < w; j++) { s.Lines.Add(new IsoLine { A = q(0, j, h), B = q(l, j, h) }); s.Lines.Add(new IsoLine { A = q(l, j, 0), B = q(l, j, h) }); }
                for (int k = 1; k < h; k++) { s.Lines.Add(new IsoLine { A = q(l, 0, k), B = q(l, w, k) }); s.Lines.Add(new IsoLine { A = q(0, w, k), B = q(l, w, k) }); }
            }
            else if (lines == IsoLines.Top)
            {
                // one layer: the length x width grid on the top face, nothing up the sides (answer 16)
                for (int i = 1; i < l; i++) s.Lines.Add(new IsoLine { A = q(i, 0, h), B = q(i, w, h) });
                for (int j = 1; j < w; j++) s.Lines.Add(new IsoLine { A = q(0, j, h), B = q(l, j, h) });
            }
            if (labels)
            {
                Label(s, q(0, w, 0), q(l, w, 0), Str(l), "ld", .35, .75);
                Label(s, q(l, 0, 0), q(l, w, 0), Str(w), "wd", .45, .7);
                Label(s, q(l, 0, 0), q(l, 0, h), hideH ? "?" : Str(h), "hd", .55, 0);
            }
        }

        private static void Label(IsoScene s, IsoPt a, IsoPt b, string text, string side, double dx, double dy)
        {
            // truck.js:38-42 the label's x/y are written with toFixed(2)
            double x = Fixed2((a.X + b.X) / 2 + dx), y = Fixed2((a.Y + b.Y) / 2 + dy);
            s.Labels.Add(new IsoLabel { At = new IsoPt(x, y), Text = text, Side = side });
        }

        /// <summary>truck.js:53 cornersOf(l, w, h, o).</summary>
        public static IEnumerable<IsoPt> Corners(int l, int w, int h, int ox = 0, int oy = 0, int oz = 0)
        {
            yield return P(ox, oy, oz); yield return P(ox + l, oy, oz); yield return P(ox + l, oy + w, oz); yield return P(ox, oy + w, oz);
            yield return P(ox, oy, oz + h); yield return P(ox + l, oy, oz + h); yield return P(ox + l, oy + w, oz + h); yield return P(ox, oy + w, oz + h);
        }

        /// <summary>truck.js:47-51 frame(): the box round the corners, with room for the labels (viewBox numbers toFixed(2)).</summary>
        public static void Frame(IsoScene s, IEnumerable<IsoPt> corners)
        {
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (IsoPt c in corners) { minX = Math.Min(minX, c.X); minY = Math.Min(minY, c.Y); maxX = Math.Max(maxX, c.X); maxY = Math.Max(maxY, c.Y); }
            double x0 = minX - 1.2, y0 = minY - .8, W = maxX - x0 + 1.4, H = maxY - y0 + 1.2;
            s.X0 = Fixed2(x0); s.Y0 = Fixed2(y0); s.W = Fixed2(W); s.H = Fixed2(H);
        }

        /// <summary>
        /// truck.js:56-64 crates(): stacks[y][x] unit crates, drawn back to front so the near ones cover the far ones, on
        /// the dashed truck floor. topLit lights each stack's top crate (hint 2 on a count problem).
        /// </summary>
        public static IsoScene Crates(int[][] stacks, int l, int w, bool topLit, int maxH)
        {
            var s = new IsoScene();
            s.Polys.Add(new IsoPoly { Fill = IsoFill.Floor, Points = new[] { P(0, 0, 0), P(l, 0, 0), P(l, w, 0), P(0, w, 0) } });
            for (int d = 0; d <= l + w - 2; d++)
                for (int x = 0; x < l; x++)
                {
                    int y = d - x;
                    if (y < 0 || y >= w) continue;
                    int hgt = stacks[y][x];
                    for (int z = 0; z < hgt; z++) Cube(s, x, y, z, topLit && z == hgt - 1);
                }
            Frame(s, Corners(l, w, Math.Max(maxH, 1)));
            return s;
        }

        /// <summary>truck.js:108-118 the picture for a typed problem at ladder stage n (0 before any hint).</summary>
        public static IsoScene Typed(string kind, IList<IsoBox> boxes, bool hideH, int stage)
        {
            if (kind == "count")
            {
                IsoBox b = boxes[0];
                var stacks = new int[b.W][];
                for (int y = 0; y < b.W; y++) { stacks[y] = new int[b.L]; for (int x = 0; x < b.L; x++) stacks[y][x] = b.H; }
                return Crates(stacks, b.L, b.W, stage >= 2, b.H);
            }
            var s = new IsoScene();
            if (kind == "two")
            {
                IsoBox A = boxes[0], B = boxes[1];
                Box(s, A, 0, 0, 0, stage >= 2, true, false, false);
                Box(s, B, 0, 0, A.H, stage >= 2, true, false, true);
                var c = new List<IsoPt>(Corners(A.L, A.W, A.H));
                c.AddRange(Corners(B.L, B.W, B.H, 0, 0, A.H));
                Frame(s, c);
                return s;
            }
            IsoBox one = boxes[0];
            // the missing height (hideH): hint 2 shows one layer (the top face's grid) and no lines up the sides, so the
            // layers can't be counted; every line comes with the worked step (answer 16; intended difference 31)
            IsoLines ln = stage < 2 ? IsoLines.None : hideH && stage < 3 ? IsoLines.Top : IsoLines.All;
            Box(s, one, 0, 0, 0, ln, true, hideH && stage < 3, false);
            Frame(s, Corners(one.L, one.W, one.H));
            return s;
        }

        private static string Str(int n) { return n.ToString(CultureInfo.InvariantCulture); }

        /// <summary>JS (+v).toFixed(2) read back as a number.</summary>
        public static double Fixed2(double v)
        {
            return double.Parse(v.ToString("F2", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }
    }
}
