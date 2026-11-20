using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WorldTour.Core.Maths.Gen;
using WorldTour.Game.Audio;
using WorldTour.Game.Motion;
using WorldTour.Game.Show;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// MODE: stage designer (world_tour/modes/math/stage.js), a hands-on builder on the first quadrant. Place and move:
    /// click the stage (the light snaps to the nearest point) or drag the light, fine-tune with the arrow keys or the
    /// arrow buttons, then set it. Read: set the two numbers with the pickers, then lock it in. Laid out like the other
    /// math modes: the desk with the stage and the controls on the left, the prompt card and the stage-hand panel (arrows
    /// or pickers, the pair, the set button) on the right. The hint ladder draws the route: across to the answer's x at
    /// step 2, the whole route and the light set at the worked step. Rules: <see cref="StageGame"/>.
    /// Keyboard: the stage (or the across picker) has focus at the start; arrows move the light (or change the number),
    /// Enter or Space sets it, Tab reaches the arrow buttons, set, hint and next.
    /// Logs "WT_MATH mode=stage kind= level= seed= answer=", "WT_STAGE_SET got= result= tries=", "WT_HINT mode=stage n="
    /// and "WT_STAGE_PLOT max= p00= pmm=" (the page pixels of (0, 0) and (max, max), for the browser tests' clicks).
    /// </summary>
    public sealed class StageMode : MonoBehaviour, ISongMode, IBuilderHost, IStageView
    {
        /// <summary>math.css .plotwrap max-width min(440px, 62vh), at 768 high.</summary>
        public const float PlotSide = 440f;
        public const float NudgeWidth = 60f, SetWidth = 250f;

        private SongContext _ctx;
        private MathSongHost _song;
        private RectTransform _desk;
        private StagePlot _plot;
        private StagePicker _px, _py;
        private TMP_Text _pair;
        private ShowButton _set, _next;
        private readonly ShowButton[] _nudges = new ShowButton[4];

        public StageGame Game { get; private set; }
        public Problem Problem { get; private set; }
        public StagePlot Plot { get { return _plot; } }

        public void Render(SongContext ctx)
        {
            _ctx = ctx;
            Problem p = Problem = ctx.Problem;
            StageSetup setup = StageSetup.From(p);
            _song = new MathSongHost(ctx, "stage", () => _desk);
            Game = new StageGame(setup, this, this);
            Game.Ladder = new BuilderLadder(_song, p, Game.Step);
            Build(ctx.Area, p, setup);
            Paint();
            Debug.Log("WT_MATH mode=stage kind=" + p.Kind + " level=" + p.Level + " seed=" + p.Seed + " answer=" + setup.Answer.X + "," + setup.Answer.Y
                + " max=" + setup.Max + " start=" + (setup.Start.HasValue ? setup.Start.Value.X + "," + setup.Start.Value.Y : "none"));
            ctx.Later(() => Game.Begin(), .01f);                                    // after the show's own song line
            ctx.Later(Focus, TypedRound.FocusDelay);
            ctx.Later(LogPlot, .45f);
        }

        private void Focus()
        {
            if (EventSystem.current == null) return;
            GameObject go = Game.Setup.Read ? (_px != null ? _px.gameObject : null) : (_plot != null ? _plot.gameObject : null);
            if (go != null) EventSystem.current.SetSelectedGameObject(go);
        }

        private void LogPlot()
        {
            if (_plot == null) return;
            int m = Game.Setup.Max;
            Vector2 a = _plot.ScreenPoint(0, 0), b = _plot.ScreenPoint(m, m);
            Debug.Log("WT_STAGE_PLOT max=" + m + " p00=" + Px(a) + " pmm=" + Px(b));
            // the stage-hand panel's controls, page pixels from the top left, for the mouse playthroughs
            var sb = new System.Text.StringBuilder("WT_STAGE_POS set=" + MathUi.Centre(_set.Rect));
            string[] names = { "left", "up", "down", "right" };
            for (int i = 0; i < 4; i++) if (_nudges[i] != null) sb.Append(' ').Append(names[i]).Append('=').Append(MathUi.Centre(_nudges[i].Rect));
            if (_px != null)
                foreach (var pk in new[] { new { n = "x", p = _px }, new { n = "y", p = _py } })
                {
                    Transform row = pk.p.transform.parent;
                    sb.Append(' ').Append(pk.n).Append("minus=").Append(MathUi.Centre((RectTransform)row.Find("Minus")))
                      .Append(' ').Append(pk.n).Append("plus=").Append(MathUi.Centre((RectTransform)row.Find("Plus")));
                }
            Debug.Log(sb.ToString());
        }

        private static string Px(Vector2 v)
        {
            return Mathf.RoundToInt(v.x).ToString(CultureInfo.InvariantCulture) + ":" + Mathf.RoundToInt(UnityEngine.Screen.height - v.y).ToString(CultureInfo.InvariantCulture);
        }

        // ---------- drawing ----------

        private void Build(RectTransform area, Problem p, StageSetup setup)
        {
            MathUi.Columns(area, out RectTransform left, out RectTransform right);
            _desk = MathUi.Desk(left, StageLines.DeskTag(setup.Gear));
            RectTransform holder = SpellingUi.Row(_desk, "Stage", 0, PlotSide);
            _plot = StagePlot.Make(holder, setup.Max, PlotSide, setup.Read);
            _plot.interactable = !setup.Read;
            _plot.Pressed = (ux, uy) => { if (Game.TapAt(ux, uy)) Feedback.Slap(_plot.GhostLamp); };
            _plot.Dragged = (ux, uy) => Game.DragAt(ux, uy);
            _plot.Nudged = (dx, dy) => Game.Nudge(dx, dy);
            _plot.Submitted = Set;
            _next = MathUi.Controls(_ctx, left, out _);

            MathUi.PromptCard(right, p.Prompt, StageLines.PromptLabel(setup.Read));
            RectTransform panel = MathUi.Desk(right, "");
            Transform tag = panel.Find("Tag");
            if (tag != null) tag.gameObject.SetActive(false);
            if (setup.Read)
            {
                _px = StagePicker.Make(panel, StageLines.Across, _ctx.ButtonPrefab);
                _py = StagePicker.Make(panel, StageLines.Up, _ctx.ButtonPrefab);
                _px.Stepped = d => Game.Pick(true, d);
                _py.Stepped = d => Game.Pick(false, d);
                _px.Submitted = Set; _py.Submitted = Set;
            }
            else
            {
                // stage.js:79-84 the arrows, in the web's order: left, up, down, right
                RectTransform row = SpellingUi.Row(panel, "Nudges", 8, 56);
                int[][] dirs = { new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 }, new[] { 1, 0 } };
                string[] glyph = { ((char)0x2190).ToString(), ((char)0x2191).ToString(), ((char)0x2193).ToString(), ((char)0x2192).ToString() };
                string[] names = { "Left", "Up", "Down", "Right" };
                for (int i = 0; i < 4; i++)
                {
                    int dx = dirs[i][0], dy = dirs[i][1];
                    _nudges[i] = SpellingUi.Control(ShowUi.Button(_ctx.ButtonPrefab, row, names[i], glyph[i], true, () => Game.Nudge(dx, dy)), NudgeWidth);
                }
            }
            RectTransform pairBox = SpellingUi.Box("Pair", panel, -1, 34);
            _pair = ShowUi.Label(pairBox, "Text", "", 20.8f, FontRole.BodyBold, ColorRole.OnInk);
            _pair.richText = false;
            RectTransform setRow = SpellingUi.Row(panel, "SetRow", 0, Tokens.ButtonMinHeight);
            _set = SpellingUi.Control(ShowUi.Button(_ctx.ButtonPrefab, setRow, "Set", StageLines.SetLabel(setup.Read, setup.Gear), false, Set), SetWidth);
        }

        private void Set()
        {
            StageSet r = Game.Set();
            if (r == StageSet.Ignored) return;
            GridPt? got = Game.Setup.Read ? (Game.PickX.HasValue && Game.PickY.HasValue ? new GridPt(Game.PickX.Value, Game.PickY.Value) : (GridPt?)null) : Game.Ghost;
            Debug.Log("WT_STAGE_SET got=" + (r == StageSet.Right ? Game.Setup.Answer.X + "," + Game.Setup.Answer.Y : got.HasValue ? got.Value.X + "," + got.Value.Y : "none")
                + " result=" + r.ToString().ToLowerInvariant() + " tries=" + _ctx.Tries);
        }

        // ---------- IStageView ----------

        public void Paint()
        {
            if (_plot == null) return;
            _plot.Paint(Game.Scene());
            _pair.text = Game.PairText();                                       // one short line: nothing to wrap
            bool on = Game.SetEnabled;
            if (_set != null) _set.Interactable = on;
            if (_px != null) { _px.Show(Game.PickX); _py.Show(Game.PickY); }
            if (Game.Done)
            {
                if (_px != null) { _px.Lock(); _py.Lock(); }
                foreach (ShowButton b in _nudges) if (b != null) b.Interactable = false;
                _plot.interactable = false;
            }
        }

        public void Tapped() { Sfx.Play(SfxId.Tap); }
        public void Popped() { Feedback.Pop(_plot.SetLamp.gameObject.activeSelf ? _plot.SetLamp : _plot.GhostLamp); }
        public void Wobbled() { Feedback.Wobble(_plot.Rect); }

        // ---------- IBuilderHost ----------

        public void Say(string line, string pose) { _song.Say(line, pose); }
        public void Mistake(string tag) { _ctx.Mistake(tag); }
        public void LockHints() { _ctx.LockHints(); }
        public void Next(float seconds) { _ctx.Next(seconds); }

        public void ShowNext() { MathUi.ShowNext(_ctx, _next); }                // parts.js showNext
    }
}
