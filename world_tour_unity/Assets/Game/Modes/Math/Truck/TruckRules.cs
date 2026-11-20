using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Game.Modes
{
    /// <summary>truck.js's copy, word for word.</summary>
    public static class TruckLines
    {
        private static readonly string Star = ((char)0x2736).ToString();
        private static readonly string Apos = ((char)0x2019).ToString();
        private static readonly string Times = ((char)0x00D7).ToString();

        /// <summary>truck.js:120 / 128 the desk tag.</summary>
        public const string DeskTag = "the trailer";
        /// <summary>truck.js:124 under a count problem's picture.</summary>
        public const string Caption = "every crate is 1 cubic unit";
        /// <summary>truck.js:126 / 147 the prompt card's label.</summary>
        public const string PromptLabel = "load out";
        /// <summary>truck.js:127 / 144 the check button.</summary>
        public const string LoadIt = "load it";
        public const string Undo = "undo", Clear = "clear";
        /// <summary>Unity addition: the crate pile's chip, which is dragged (or picked, then placed) onto a spot (DECISIONS, 4.2).</summary>
        public const string Crate = "crate";
        /// <summary>
        /// Unity (QUESTIONS 15, answer 7's rule; intended difference 15): "load it" on an empty floor only asks her to load
        /// something. No miss, no try, the ladder stays put. The web counts it as a miss.
        /// </summary>
        public const string EmptyFloor = "load a crate or two first.";
        /// <summary>truck.js:162 the builder's first line.</summary>
        public const string BuildIntro = "tap a spot on the floor to stack a crate there.";

        /// <summary>truck.js:134 the builder's desk tag: "the trailer * 24 crates".</summary>
        public static string BuildDeskTag(int target) { return "the trailer " + Star + " " + target.ToString(CultureInfo.InvariantCulture) + " crates"; }
        /// <summary>truck.js:153 the count under the floor plan.</summary>
        public static string Loaded(int n) { return n.ToString(CultureInfo.InvariantCulture) + " loaded"; }
        /// <summary>truck.js:157 a stack at the roof.</summary>
        public static string Roof(int maxH) { return "that stack" + Apos + "s at the roof. " + maxH.ToString(CultureInfo.InvariantCulture) + " is the max."; }
        /// <summary>truck.js:136 a spot's name for screen readers and the log: "spot 1, 2".</summary>
        public static string Spot(int x, int y) { return "spot " + (x + 1).ToString(CultureInfo.InvariantCulture) + ", " + (y + 1).ToString(CultureInfo.InvariantCulture); }

        /// <summary>truck.js:130 the typed problems' first line.</summary>
        public static string Intro(string kind)
        {
            return kind == "count" ? "count them up. some are hiding in the back." : "length " + Times + " width " + Times + " height.";
        }

        /// <summary>
        /// truck.js:179 the right number of crates in uneven stacks, after a miss: this line replaces the ladder's, with
        /// the step she's on (step 1 after the first miss, step 2 after the second; never the worked step).
        /// </summary>
        public static string Uneven(IList<string> steps, int shown)
        {
            return "right count, but the stacks aren" + Apos + "t all the same height. " + steps[Math.Min(1, shown - 1)];
        }
    }

    /// <summary>What a press on a floor spot did.</summary>
    public enum TruckAdd { Ignored, AtRoof, Added }

    /// <summary>What "load it" did.</summary>
    public enum TruckCheck { Ignored, Empty, Right, Uneven, Wrong, WrongShown }

    /// <summary>The builder's view: it repaints from <see cref="TruckBuild"/>'s state and plays the feedback.</summary>
    public interface ITruckView
    {
        /// <summary>truck.js paint(): the isometric view, each spot's count, the "n loaded" line.</summary>
        void Paint();
        /// <summary>snd.tap(), and slap the spot when a crate landed there (x, y), or (-1, -1) for undo and clear.</summary>
        void Tapped(int x, int y);
        void Popped();
        void Wobbled();
        /// <summary>Load, undo and clear off (answered or shown).</summary>
        void LockButtons();
    }

    /// <summary>
    /// The rules of the truck builder, ported from world_tour/modes/math/truck.js build(): a top-down floor plan of l x w
    /// spots; a press on a spot stacks a crate there (up to the roof); undo, clear; "load it" wins with exactly the
    /// target loaded and every stack the same height. Plain C#.
    /// </summary>
    public sealed class TruckBuild
    {
        /// <summary>truck.js:177 ctx.next(1400).</summary>
        public const float NextAfterWin = 1.4f;
        /// <summary>truck.js:174 the mistake noted for the right count in uneven stacks.</summary>
        public const string UnevenTag = "uneven-stacks";

        public readonly int L, W, Target, MaxH, Height;
        public readonly IList<string> Steps;
        private readonly int[][] _stacks;
        private readonly List<int[]> _log = new List<int[]>();
        private readonly IBuilderHost _host;
        private readonly ITruckView _view;

        public IMathLadder Ladder { get; set; }
        public bool Done { get; private set; }

        public TruckBuild(Problem p, IBuilderHost host, ITruckView view)
        {
            if (p == null || p.Skill != Volume.Skill || p.Kind != "build") throw new ArgumentException("not a truck builder problem");
            JsonValue pr = p.Params;
            L = (int)pr["floor"]["l"].Long; W = (int)pr["floor"]["w"].Long;
            Target = (int)pr["target"].Long; MaxH = (int)pr["maxH"].Long;
            Height = (int)p.Answer.Value.Number;
            Steps = p.Steps;
            _host = host; _view = view;
            _stacks = new int[W][];
            for (int y = 0; y < W; y++) _stacks[y] = new int[L];
        }

        /// <summary>truck.js:161-162, after the first paint.</summary>
        public void Begin()
        {
            _view.Paint();
            _host.Say(TruckLines.BuildIntro, "idle");
        }

        public int At(int x, int y) { return _stacks[y][x]; }
        public int Total { get { return _stacks.Sum(r => r.Sum()); } }
        public int Tallest { get { return _stacks.Max(r => r.Max()); } }
        /// <summary>Every stack the same height (an empty floor counts, as in the JS).</summary>
        public bool Even { get { int f = _stacks[0][0]; return _stacks.All(r => r.All(v => v == f)); } }
        public int Moves { get { return _log.Count; } }

        /// <summary>stacks[y][x], copied, for the drawing.</summary>
        public int[][] Stacks { get { return _stacks.Select(r => (int[])r.Clone()).ToArray(); } }

        /// <summary>truck.js:154-158 a press on spot (x, y).</summary>
        public TruckAdd Add(int x, int y)
        {
            if (Done || x < 0 || y < 0 || x >= L || y >= W) return TruckAdd.Ignored;
            if (_stacks[y][x] >= MaxH) { _host.Say(TruckLines.Roof(MaxH), "think"); return TruckAdd.AtRoof; }
            _stacks[y][x]++;
            _log.Add(new[] { x, y });
            _view.Tapped(x, y);
            _view.Paint();
            return TruckAdd.Added;
        }

        /// <summary>truck.js:159 undo the last crate.</summary>
        public bool Undo()
        {
            if (Done || _log.Count == 0) return false;
            int[] m = _log[_log.Count - 1];
            _log.RemoveAt(_log.Count - 1);
            _stacks[m[1]][m[0]]--;
            _view.Tapped(-1, -1);
            _view.Paint();
            return true;
        }

        /// <summary>truck.js:160 clear the floor (the tap sounds even when it's already empty, as in the JS).</summary>
        public bool Clear()
        {
            if (Done) return false;
            foreach (int[] r in _stacks) Array.Clear(r, 0, r.Length);
            _log.Clear();
            _view.Tapped(-1, -1);
            _view.Paint();
            return true;
        }

        /// <summary>truck.js:168-180 "load it" (an empty floor is not a try: <see cref="TruckLines.EmptyFloor"/>).</summary>
        public TruckCheck Load()
        {
            if (Done || (Ladder != null && Ladder.Over)) return TruckCheck.Ignored;
            int total = Total;
            if (total == 0) { _host.Say(TruckLines.EmptyFloor, "think"); return TruckCheck.Empty; }   // QUESTIONS 15: not a miss
            if (Even && total == Target)
            {
                Done = true;
                _host.LockHints();
                _view.Popped();
                _view.LockButtons();
                if (Ladder != null) Ladder.Right();
                _host.Next(NextAfterWin);
                return TruckCheck.Right;
            }
            _view.Wobbled();
            bool uneven = total == Target;
            if (uneven) _host.Mistake(UnevenTag);
            if (Ladder != null && Ladder.Wrong(MathValue.Null))
            {
                _view.LockButtons();
                _host.ShowNext();
                return TruckCheck.WrongShown;
            }
            if (uneven) { _host.Say(TruckLines.Uneven(Steps, Ladder != null ? Ladder.Shown : 1), "oops"); return TruckCheck.Uneven; }
            return TruckCheck.Wrong;
        }

        /// <summary>truck.js:164-166 the ladder's onStep: the worked solution fills every spot to the answer's height.</summary>
        public void Step(int n)
        {
            if (n < 3) return;
            foreach (int[] r in _stacks) for (int i = 0; i < r.Length; i++) r[i] = Height;
            Done = true;
            _view.Paint();
        }

        /// <summary>truck.js:151 the isometric view: the floor is drawn at least 4 high so the truck doesn't jump about.</summary>
        public IsoScene Scene() { return IsoGeometry.Crates(_stacks, L, W, false, Math.Max(4, Tallest)); }
    }

    /// <summary>
    /// The typed truck problems (count, l x w x h, two blocks, missing side; truck.js:100-131): the picture climbs with
    /// the hint ladder (stage = the highest step shown). The answer is typed into the shared numeric field.
    /// </summary>
    public sealed class TruckPicture
    {
        public readonly string Kind;
        public readonly List<IsoBox> Boxes = new List<IsoBox>();
        public readonly bool HideH;
        public int Stage { get; private set; }

        public TruckPicture(Problem p)
        {
            if (p == null || p.Skill != Volume.Skill || p.Kind == "build") throw new ArgumentException("not a typed truck problem");
            Kind = p.Kind;
            foreach (JsonValue b in p.Params["boxes"].Items) Boxes.Add(new IsoBox((int)b["l"].Long, (int)b["w"].Long, (int)b["h"].Long));
            JsonValue h = p.Params["hideH"];
            HideH = h != null && !h.IsNull && h.BoolValue;
        }

        /// <summary>truck.js:129 onStep: stage = max(stage, n). True when the picture changed.</summary>
        public bool Step(int n)
        {
            if (n <= Stage) return false;
            Stage = n;
            return true;
        }

        public IsoScene Scene() { return IsoGeometry.Typed(Kind, Boxes, HideH, Stage); }
    }
}
