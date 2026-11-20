using System;
using System.Collections.Generic;
using System.Globalization;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Game.Modes
{
    /// <summary>A whole-number point on the stage grid.</summary>
    public readonly struct GridPt : IEquatable<GridPt>
    {
        public readonly int X, Y;
        public GridPt(int x, int y) { X = x; Y = y; }
        public bool Equals(GridPt o) { return X == o.X && Y == o.Y; }
        public override bool Equals(object obj) { return obj is GridPt g && Equals(g); }
        public override int GetHashCode() { return X * 397 ^ Y; }
        public override string ToString() { return "(" + X + ", " + Y + ")"; }
        public MathValue Value { get { return MathValue.Pt(X, Y); } }
    }

    public enum StageKind { Place, Read, Move }

    /// <summary>What the stage designer problem asks for, read from the generated problem (coords.js params).</summary>
    public sealed class StageSetup
    {
        public int Max;
        public string Gear;
        public StageKind Kind;
        /// <summary>The gear's starting point (move problems), else null.</summary>
        public GridPt? Start;
        /// <summary>The point to read (read problems), else null.</summary>
        public GridPt? Point;
        public GridPt Answer;

        public bool Read { get { return Kind == StageKind.Read; } }

        public static StageSetup From(Problem p)
        {
            if (p == null || p.Skill != Coords.Skill) throw new ArgumentException("not a stage designer problem");
            JsonValue pr = p.Params;
            var s = new StageSetup
            {
                Max = (int)pr["max"].Long,
                Gear = pr["gear"].String,
                Kind = p.Kind == "read" ? StageKind.Read : p.Kind == "move" ? StageKind.Move : StageKind.Place,
                Answer = new GridPt((int)p.Answer.Value.X, (int)p.Answer.Value.Y),
            };
            JsonValue st = pr["start"];
            if (st != null && !st.IsNull) s.Start = new GridPt((int)st["x"].Long, (int)st["y"].Long);
            JsonValue pt = pr["point"];
            if (pt != null && !pt.IsNull) s.Point = new GridPt((int)pt["x"].Long, (int)pt["y"].Long);
            return s;
        }
    }

    /// <summary>stage.js's copy, word for word.</summary>
    public static class StageLines
    {
        private static readonly string Star = ((char)0x2736).ToString();

        /// <summary>stage.js:73 the desk tag: "stage plot * spotlight".</summary>
        public static string DeskTag(string gear) { return "stage plot " + Star + " " + gear; }
        /// <summary>stage.js:87 the prompt card's label.</summary>
        public static string PromptLabel(bool read) { return read ? "where is it" : "stage plot"; }
        /// <summary>stage.js:77 / 86 the set button.</summary>
        public static string SetLabel(bool read, string gear) { return read ? "lock it in" : "set the " + gear; }
        /// <summary>stage.js:44 the pickers' labels.</summary>
        public const string Across = "across", Up = "up";
        /// <summary>stage.js:85 / 95 before the light is on the stage.</summary>
        public const string TapTheStage = "tap the stage";
        /// <summary>stage.js:130.</summary>
        public const string NeedBoth = "set both numbers first.";
        public const string NeedSpot = "tap a spot on the stage first.";
        /// <summary>
        /// Unity (QUESTIONS 15, answer 15; intended difference 18): "set" on a move problem before the light has moved. The
        /// gear is already on the stage, so the web's "tap a spot on the stage first." didn't fit. Not a try either way.
        /// </summary>
        public static string NeedMove(string gear) { return "move the " + gear + " first."; }

        /// <summary>stage.js:142 the dog's first line.</summary>
        public static string Intro(StageSetup s)
        {
            return s.Read ? "across first, then up." : s.Start.HasValue ? "move it, then set it." : "tap the spot. the arrows fine-tune it.";
        }

        /// <summary>stage.js:95 the pair under the stage: "( 3, ? )".</summary>
        public static string Pair(int? x, int? y)
        {
            return "( " + (x.HasValue ? x.Value.ToString(CultureInfo.InvariantCulture) : "?") + ", "
                 + (y.HasValue ? y.Value.ToString(CultureInfo.InvariantCulture) : "?") + " )";
        }
    }

    /// <summary>stage.js plot()'s route: from a point, across to x, then up or down to y.</summary>
    public sealed class StagePath
    {
        public readonly GridPt From;
        public readonly int ToX, ToY;
        public StagePath(GridPt from, int toX, int toY) { From = from; ToX = toX; ToY = toY; }

        /// <summary>The polyline: from, the corner after going across, the end.</summary>
        public GridPt[] Points { get { return new[] { From, new GridPt(ToX, From.Y), new GridPt(ToX, ToY) }; } }
        public GridPt End { get { return new GridPt(ToX, ToY); } }
    }

    /// <summary>What the stage shows at a moment (stage.js draw()).</summary>
    public sealed class StageScene
    {
        /// <summary>The set light (.lamp.set): the gear to read, or the answer once it's placed or shown.</summary>
        public GridPt? Light;
        /// <summary>Her light (.lamp.ghost): where she's put it, or her read pair once it's locked in right or shown.</summary>
        public GridPt? Ghost;
        /// <summary>The dashed starting light (.lamp.start), move problems only.</summary>
        public GridPt? Start;
        public StagePath Path;
        /// <summary>The bottom-line number drawn big (.ax.on), or null.</summary>
        public int? Highlight;
    }

    /// <summary>What a press of "set" / "lock it in" did.</summary>
    public enum StageSet { Ignored, NeedInput, Right, Wrong, WrongShown }

    /// <summary>The stage's view: it repaints from <see cref="StageGame"/>'s state and plays the feedback.</summary>
    public interface IStageView
    {
        /// <summary>Redraw the grid, lights, route, pair and pickers, and the set button's state.</summary>
        void Paint();
        /// <summary>snd.tap(): the light moved or a number changed.</summary>
        void Tapped();
        /// <summary>Right: pop the light.</summary>
        void Popped();
        /// <summary>Wrong: wobble the stage (never red).</summary>
        void Wobbled();
    }

    /// <summary>
    /// The rules of stage designer, ported from world_tour/modes/math/stage.js render(): place or move (tap the stage,
    /// the light snaps to the nearest point, nudge it with the arrows, then set it) and read (set the two numbers with
    /// the pickers). Plain C#; the view draws <see cref="Scene"/>.
    /// </summary>
    public sealed class StageGame
    {
        /// <summary>stage.js:11 room for the axis numbers, in grid units.</summary>
        public const double Pad = 1.1;
        /// <summary>stage.js:136 ctx.next(1400).</summary>
        public const float NextAfterWin = 1.4f;

        public readonly StageSetup Setup;
        private readonly IBuilderHost _host;
        private readonly IStageView _view;
        private IMathLadder _ladder;

        public GridPt? Ghost { get; private set; }
        public int? PickX { get; private set; }
        public int? PickY { get; private set; }
        public bool Done { get; private set; }
        public StagePath Path { get; private set; }
        public int? Highlight { get; private set; }
        /// <summary>stage.js #set disabled: place and move start disabled until the light is on the stage, and every kind turns it off once answered or shown.</summary>
        public bool SetEnabled { get; private set; }

        public StageGame(StageSetup setup, IBuilderHost host, IStageView view)
        {
            Setup = setup; _host = host; _view = view;
            SetEnabled = setup.Read;
        }

        /// <summary>The ladder, made with <see cref="Step"/> as its onStep (they need each other).</summary>
        public IMathLadder Ladder { get { return _ladder; } set { _ladder = value; } }

        /// <summary>stage.js:142, after the first draw.</summary>
        public void Begin()
        {
            _view.Paint();
            _host.Say(StageLines.Intro(Setup), "idle");
        }

        // ---------- the plot's units (stage.js plot: viewBox 0 0 S S) ----------

        /// <summary>stage.js:14 the plot's side in grid units.</summary>
        public static double Side(int max) { return max + Pad + .5; }
        /// <summary>stage.js:15 X(x): a grid x to plot units (right).</summary>
        public static double PlotX(int max, double x) { return x + Pad; }
        /// <summary>stage.js:15 Y(y): a grid y to plot units (down).</summary>
        public static double PlotY(int max, double y) { return max - y + .5; }

        /// <summary>JS Math.round: halves round up (towards +infinity).</summary>
        public static int JsRound(double v) { return (int)Math.Floor(v + .5); }

        /// <summary>stage.js:105-106 the nearest grid point to a spot in plot units, or false off the grid.</summary>
        public static bool Snap(int max, double ux, double uy, out GridPt p)
        {
            int x = JsRound(ux - Pad), y = JsRound(max + .5 - uy);
            p = new GridPt(x, y);
            return !(x < 0 || y < 0 || x > max || y > max);
        }

        // ---------- place and move ----------

        /// <summary>stage.js:100-109 a press on the stage at a spot in plot units. False when ignored.</summary>
        public bool TapAt(double ux, double uy)
        {
            if (Done || Setup.Read) return false;
            GridPt p;
            if (!Snap(Setup.Max, ux, uy, out p)) return false;
            Ghost = p;
            _view.Tapped();
            SetEnabled = true;
            _view.Paint();
            return true;
        }

        /// <summary>
        /// A drag over the stage: the light follows to each new grid point. Unity addition (the web has taps only), so
        /// the light can also be dragged; it moves only when the nearest point changes, so the tap sound isn't spammed.
        /// </summary>
        public bool DragAt(double ux, double uy)
        {
            if (Done || Setup.Read) return false;
            GridPt p;
            if (!Snap(Setup.Max, ux, uy, out p)) return false;
            if (Ghost.HasValue && Ghost.Value.Equals(p)) return false;
            return TapAt(ux, uy);
        }

        /// <summary>stage.js:110-115 an arrow: from her light, or the start (or (0, 0)) if it isn't out yet; kept on the grid.</summary>
        public bool Nudge(int dx, int dy)
        {
            if (Done || Setup.Read) return false;
            GridPt g = Ghost ?? Setup.Start ?? new GridPt(0, 0);
            Ghost = new GridPt(Math.Max(0, Math.Min(Setup.Max, g.X + dx)), Math.Max(0, Math.Min(Setup.Max, g.Y + dy)));
            _view.Tapped();
            SetEnabled = true;
            _view.Paint();
            return true;
        }

        // ---------- read ----------

        /// <summary>stage.js:117-123 a picker's minus / plus: from nothing it starts at 0, and stays on the grid.</summary>
        public bool Pick(bool across, int d)
        {
            if (Done || !Setup.Read) return false;
            int v = Math.Max(0, Math.Min(Setup.Max, ((across ? PickX : PickY) ?? 0) + d));
            if (across) PickX = v; else PickY = v;
            _view.Tapped();
            _view.Paint();
            return true;
        }

        // ---------- set ----------

        /// <summary>stage.js:127-141 "set the gear" / "lock it in".</summary>
        public StageSet Set()
        {
            if (Done || (_ladder != null && _ladder.Over)) return StageSet.Ignored;
            GridPt? got = Setup.Read ? (PickX.HasValue && PickY.HasValue ? new GridPt(PickX.Value, PickY.Value) : (GridPt?)null) : Ghost;
            if (!got.HasValue)
            {
                // QUESTIONS 15: nothing set yet is not an attempt (no miss, no try, the ladder stays put), as on the web
                _host.Say(Setup.Read ? StageLines.NeedBoth : Setup.Start.HasValue ? StageLines.NeedMove(Setup.Gear) : StageLines.NeedSpot, "think");
                return StageSet.NeedInput;
            }
            if (got.Value.Equals(Setup.Answer))
            {
                Done = true;
                _host.LockHints();
                if (!Setup.Read) Ghost = null;
                SetEnabled = false;
                _view.Paint();
                _view.Popped();
                if (_ladder != null) _ladder.Right();
                _host.Next(NextAfterWin);
                return StageSet.Right;
            }
            _view.Wobbled();
            if (_ladder != null && _ladder.Wrong(got.Value.Value))
            {
                SetEnabled = false;
                _view.Paint();
                _host.ShowNext();
                return StageSet.WrongShown;
            }
            return StageSet.Wrong;
        }

        /// <summary>
        /// stage.js:125 the ladder's onStep. 2: the route across to the answer's x (read: from the gear straight down to
        /// the bottom line) with that number big. 3, the worked solution: the whole route to the answer, the light set
        /// there (read: her pair set to it), and the stage done.
        /// </summary>
        public void Step(int n)
        {
            GridPt a = Setup.Answer, from = Setup.Start ?? new GridPt(0, 0);
            if (n == 2)
            {
                Highlight = a.X; Path = Setup.Read ? new StagePath(a, a.X, 0) : new StagePath(from, a.X, from.Y);
                // answer 16: a hint-2 route that would end on the answer (a point on the bottom line) marks it. Then no route
                // and no big number: the text alone (answer 9's low items). Intended difference 29.
                if (Path.End.Equals(a)) { Path = null; Highlight = null; }
                // an answer on the bottom line: the big bottom-line number would sit on the answer point itself
                if (a.Y == 0) Highlight = null;
            }
            if (n >= 3)
            {
                Highlight = a.X; Path = new StagePath(from, a.X, a.Y); Done = true;
                if (Setup.Read) { PickX = a.X; PickY = a.Y; } else Ghost = null;
            }
            _view.Paint();
        }

        /// <summary>stage.js draw().</summary>
        public StageScene Scene()
        {
            bool read = Setup.Read;
            return new StageScene
            {
                Light = read ? Setup.Point : Done ? Setup.Answer : (GridPt?)null,
                Ghost = read ? (PickX.HasValue && PickY.HasValue && Done ? new GridPt(PickX.Value, PickY.Value) : (GridPt?)null) : Ghost,
                Start = Setup.Start,
                Path = Path,
                Highlight = Highlight,
            };
        }

        /// <summary>stage.js:95 the pair under the stage.</summary>
        public string PairText()
        {
            if (Setup.Read) return StageLines.Pair(PickX, PickY);
            return Ghost.HasValue ? StageLines.Pair(Ghost.Value.X, Ghost.Value.Y) : StageLines.TapTheStage;
        }
    }

    /// <summary>
    /// stage.js plot(): the grid lines and axis numbers in plot units (x right, y down), so the view can draw them at any
    /// size. Line widths and font sizes are math.css's, also in plot units.
    /// </summary>
    public static class StagePlotLayout
    {
        /// <summary>math.css .plot .gl stroke-width.</summary>
        public const double GridLineWidth = .04;
        /// <summary>math.css .plot .axis stroke-width.</summary>
        public const double AxisWidth = .09;
        /// <summary>math.css .ax font-size, and .ax.on.</summary>
        public const double AxisFont = .42, AxisFontOn = .58;
        /// <summary>math.css .axl font-size (the x and y letters).</summary>
        public const double LetterFont = .5;
        /// <summary>stage.js:24 the lamp: halo r .42, bulb r .24; math.css .bulb stroke .07.</summary>
        public const double HaloR = .42, BulbR = .24, BulbStroke = .07;
        /// <summary>math.css .route stroke .12, dashes .25 / .15.</summary>
        public const double RouteWidth = .12, RouteDash = .25, RouteGap = .15;
        /// <summary>The bottom-line numbers' baseline under the axis: stage.js has .62; .9 keeps a light on the bottom line (halo .42) off its number (P5).</summary>
        public const double BottomLabelDrop = .9;

        public struct Line { public double X1, Y1, X2, Y2; public bool Axis; }
        /// <summary>A text anchored at its middle (text-anchor:middle for numbers; start for the x/y letters) on the baseline.</summary>
        public struct Label { public double X, Y; public string Text; public bool On; public bool Letter; }

        public static List<Line> Lines(int max)
        {
            var l = new List<Line>();
            for (int i = 0; i <= max; i++)
            {
                double x = StageGame.PlotX(max, i), y = StageGame.PlotY(max, i);
                l.Add(new Line { X1 = x, Y1 = StageGame.PlotY(max, 0), X2 = x, Y2 = StageGame.PlotY(max, max), Axis = i == 0 });
                l.Add(new Line { X1 = StageGame.PlotX(max, 0), Y1 = y, X2 = StageGame.PlotX(max, max), Y2 = y, Axis = i == 0 });
            }
            return l;
        }

        public static List<Label> Labels(int max, int? highlight)
        {
            var l = new List<Label>();
            for (int i = 0; i <= max; i++)
            {
                string t = i.ToString(CultureInfo.InvariantCulture);
                l.Add(new Label { X = StageGame.PlotX(max, i), Y = StageGame.PlotY(max, 0) + BottomLabelDrop, Text = t, On = highlight.HasValue && highlight.Value == i });
                if (i != 0) l.Add(new Label { X = StageGame.PlotX(max, 0) - .45, Y = StageGame.PlotY(max, i) + .17, Text = t });
            }
            l.Add(new Label { X = StageGame.PlotX(max, max) + .25, Y = StageGame.PlotY(max, 0) + .2, Text = "x", Letter = true });
            l.Add(new Label { X = StageGame.PlotX(max, 0) - .2, Y = StageGame.PlotY(max, max) - .2, Text = "y", Letter = true });
            return l;
        }

        /// <summary>A spot in a box of <paramref name="w"/> x <paramref name="h"/> (any units, y up, origin bottom-left) to plot units, for a plot drawn centred and as large as fits.</summary>
        public static void ToPlot(int max, double w, double h, double bx, double by, out double ux, out double uy)
        {
            double s = StageGame.Side(max), scale = Math.Min(w, h) / s;
            double ox = (w - s * scale) / 2, oy = (h - s * scale) / 2;
            ux = (bx - ox) / scale;
            uy = s - (by - oy) / scale;
        }
    }
}
