using System.Collections.Generic;
using System.Globalization;
using System.Text;
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
    /// MODE: pack the truck (world_tour/modes/math/truck.js): volume of rectangular prisms, drawn isometric.
    /// Count, l x w x h, two blocks and missing side are typed on the shared keypad; the picture climbs with the hint
    /// ladder (lit top layer and unit lines at step 2, the hidden side at the worked step). The builder (level 2) is
    /// hands-on: a top-down floor plan of big spots; a press on a spot stacks a crate there, or a crate is dragged from
    /// the pile (or picked from it and placed) onto a spot; load exactly the target with every stack the same height. Its
    /// worked solution fills every spot to the answer's height. Rules: <see cref="TruckBuild"/>, <see cref="TruckPicture"/>,
    /// <see cref="TypedMathRound"/>.
    /// Keyboard: typed problems take the number keys and Enter. The builder's first spot has focus at the start; arrows
    /// move between spots, Enter or Space stacks a crate, Tab reaches the pile, undo, clear, load it, hint and next.
    /// Logs "WT_MATH mode=truck kind= level= seed= answer=", "WT_MATH_SUBMIT val= result= tries=" (typed),
    /// "WT_TRUCK_LOAD total= even= result= tries=" and "WT_TRUCK_SPOTS" (page pixels of each spot, for the browser tests).
    /// </summary>
    public sealed class TruckMode : MonoBehaviour, ISongMode, IBuilderHost, ITruckView
    {
        public const float TypedIsoW = 420f, TypedIsoH = 300f, BuildIsoW = 300f, BuildIsoH = 250f, SpotGap = 8f;

        private SongContext _ctx;
        private MathSongHost _song;
        private RectTransform _desk;
        private IsoView _iso;
        private ShowButton _next, _undo, _clear, _load;
        private TMP_Text _count;
        private DragChip _pile;
        private CrateSpot[,] _spots;
        private CrateSpot _dropSpot;

        public Problem Problem { get; private set; }
        public TruckBuild Build { get; private set; }
        public TruckPicture Picture { get; private set; }
        public TypedMathRound Round { get; private set; }
        public MathKeypad Pad { get; private set; }

        public void Render(SongContext ctx)
        {
            _ctx = ctx;
            Problem p = Problem = ctx.Problem;
            _song = new MathSongHost(ctx, "truck", () => _desk);
            Debug.Log("WT_MATH mode=truck kind=" + p.Kind + " level=" + p.Level + " seed=" + p.Seed + " answer=" + TypedMathRound.KeyedAnswer(p.Answer));
            if (p.Kind == "build") RenderBuild(ctx, p); else RenderTyped(ctx, p);
        }

        // ---------- typed: count, lwh, two, missing ----------

        private void RenderTyped(SongContext ctx, Problem p)
        {
            Picture = new TruckPicture(p);
            MathUi.Columns(ctx.Area, out RectTransform left, out RectTransform right);
            _desk = MathUi.Desk(left, TruckLines.DeskTag);
            RectTransform isoRow = SpellingUi.Row(_desk, "Trailer", 0, TypedIsoH);
            _iso = IsoView.Make(isoRow, TypedIsoW, TypedIsoH);
            if (p.Kind == "count")
            {
                RectTransform cap = SpellingUi.Box("Caption", _desk, -1, 22);
                ShowUi.Label(cap, "Text", TruckLines.Caption, Tokens.Fs1, FontRole.Body, ColorRole.OnInk);
            }
            _next = MathUi.Controls(ctx, left, out _);
            MathUi.PromptCard(right, p.Prompt, TruckLines.PromptLabel);
            Pad = MathKeypad.Make(right, p.Answer.Type, TruckLines.LoadIt, ShowNext);
            Round = new TypedMathRound(_song, p, Pad, s => ctx.Next(s), n => { if (Picture.Step(n)) _iso.Paint(Picture.Scene()); });
            Pad.Submitted += text =>
            {
                TypedMathResult r = Round.Submit(text);
                Debug.Log("WT_MATH_SUBMIT val=" + text + " result=" + r.ToString().ToLowerInvariant() + " tries=" + _ctx.Tries);
            };
            _iso.Paint(Picture.Scene());
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);   // Enter goes to the pad
            ctx.Later(() => Say(TruckLines.Intro(p.Kind), "idle"), .01f);
            ctx.Later(() => { if (Pad != null) Pad.LogPositions(); }, .45f);
        }

        // ---------- the builder ----------

        private void RenderBuild(SongContext ctx, Problem p)
        {
            Build = new TruckBuild(p, this, this);
            Build.Ladder = new BuilderLadder(_song, p, Build.Step);
            MathUi.Columns(ctx.Area, out RectTransform left, out RectTransform right);
            _desk = MathUi.Desk(left, TruckLines.BuildDeskTag(Build.Target));

            float planW = Build.L * CrateSpot.Size + (Build.L - 1) * SpotGap, planH = Build.W * CrateSpot.Size + (Build.W - 1) * SpotGap;
            RectTransform row = SpellingUi.Row(_desk, "Trailer", 20, BuildIsoH);
            _iso = IsoView.Make(row, BuildIsoW, BuildIsoH);
            RectTransform side = SpellingUi.Box("Floor", row, Mathf.Max(planW, 200f), BuildIsoH);
            var sv = side.gameObject.AddComponent<VerticalLayoutGroup>();
            sv.spacing = 12; sv.childAlignment = TextAnchor.MiddleCenter;
            sv.childControlWidth = true; sv.childControlHeight = true; sv.childForceExpandWidth = false; sv.childForceExpandHeight = false;

            // truck.js:135 the floor plan: rows of spots (y), each l across (x)
            RectTransform plan = SpellingUi.Box("Plan", side, planW, planH);
            var grid = plan.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(CrateSpot.Size, CrateSpot.Size);
            grid.spacing = new Vector2(SpotGap, SpotGap);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Build.L;
            grid.childAlignment = TextAnchor.MiddleCenter;
            _spots = new CrateSpot[Build.L, Build.W];
            for (int y = 0; y < Build.W; y++)
                for (int x = 0; x < Build.L; x++)
                {
                    CrateSpot s = CrateSpot.Make(plan, x, y);
                    s.Pressed = sp => Build.Add(sp.X, sp.Y);
                    _spots[x, y] = s;
                }

            // the crate pile: drag a crate onto a spot, or pick it and then press a spot (js/dnd.js's two ways in)
            RectTransform pileRow = SpellingUi.Row(side, "PileRow", 14, 58);
            RectTransform pileBox = SpellingUi.Box("Pile", pileRow, 96, 58);
            _pile = DragChip.Make(pileBox, 0, TruckLines.Crate, 3f);
            _pile.Tapped = c => { c.Selected = !c.Selected; Sfx.Play(SfxId.Tap); };
            _pile.IsOverTarget = OverSpot;
            _pile.Dropped = c => { if (_dropSpot != null) Build.Add(_dropSpot.X, _dropSpot.Y); };
            RectTransform cntBox = SpellingUi.Box("Count", pileRow, 120, 58);
            _count = ShowUi.Label(cntBox, "Text", "", Tokens.FontBody, FontRole.BodyBold, ColorRole.OnInk);
            _count.richText = false;

            RectTransform buttons = SpellingUi.Row(_desk, "Buttons", 12, Tokens.ButtonMinHeight);
            _undo = SpellingUi.Control(ShowUi.Button(ctx.ButtonPrefab, buttons, "Undo", TruckLines.Undo, true, () => Build.Undo()), 130);
            _clear = SpellingUi.Control(ShowUi.Button(ctx.ButtonPrefab, buttons, "Clear", TruckLines.Clear, true, () => Build.Clear()), 130);
            _load = SpellingUi.Control(ShowUi.Button(ctx.ButtonPrefab, buttons, "Load", TruckLines.LoadIt, false, Load), 160);

            _next = MathUi.Controls(ctx, left, out _);
            MathUi.PromptCard(right, p.Prompt, TruckLines.PromptLabel);
            Paint();
            ctx.Later(() => Build.Begin(), .01f);                                   // after the show's own song line
            ctx.Later(() => { if (EventSystem.current != null && _spots != null) EventSystem.current.SetSelectedGameObject(_spots[0, 0].gameObject); }, TypedRound.FocusDelay);
            ctx.Later(LogSpots, .45f);
        }

        private bool OverSpot(Vector2 screen, Camera cam)
        {
            _dropSpot = null;
            if (_spots == null) return false;
            foreach (CrateSpot s in _spots)
                if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)s.transform, screen, cam)) { _dropSpot = s; return true; }
            return false;
        }

        private void Load()
        {
            TruckCheck r = Build.Load();
            if (r == TruckCheck.Ignored) return;
            Debug.Log("WT_TRUCK_LOAD total=" + Build.Total + " even=" + (Build.Even ? "true" : "false") + " result=" + r.ToString().ToLowerInvariant() + " tries=" + _ctx.Tries);
        }

        private void LogSpots()
        {
            if (_spots == null) return;
            var sb = new StringBuilder("WT_TRUCK_SPOTS l=" + Build.L + " w=" + Build.W);
            foreach (CrateSpot s in _spots) sb.Append(' ').Append(s.X).Append(',').Append(s.Y).Append('=').Append(MathUi.Centre((RectTransform)s.transform));
            sb.Append(" pile=").Append(MathUi.Centre((RectTransform)_pile.transform));
            sb.Append(" undo=").Append(MathUi.Centre(_undo.Rect)).Append(" clear=").Append(MathUi.Centre(_clear.Rect)).Append(" load=").Append(MathUi.Centre(_load.Rect));
            Debug.Log(sb.ToString());
        }

        // ---------- ITruckView ----------

        public void Paint()
        {
            if (Build == null || _iso == null) return;
            _iso.Paint(Build.Scene());
            foreach (CrateSpot s in _spots) s.Show(Build.At(s.X, s.Y));
            _count.text = TruckLines.Loaded(Build.Total);
        }

        public void Tapped(int x, int y)
        {
            Sfx.Play(SfxId.Tap);
            if (x >= 0 && _spots != null) Feedback.Slap(_spots[x, y].Body);
        }

        public void Popped() { Feedback.Pop(_iso.Rect); }
        public void Wobbled() { Feedback.Wobble(_iso.Rect); }

        public void LockButtons()
        {
            if (_undo != null) _undo.Interactable = false;
            if (_clear != null) _clear.Interactable = false;
            if (_load != null) _load.Interactable = false;
            if (_pile != null)
            {
                _pile.Selected = false; _pile.Lock();
                var g = _pile.GetComponent<CanvasGroup>(); if (g != null) g.alpha = .4f;     // greyed with the other controls (P7)
            }
            if (_spots != null) foreach (CrateSpot s in _spots) s.interactable = false;
        }

        // ---------- IBuilderHost ----------

        public void Say(string line, string pose) { _song.Say(line, pose); }
        public void Mistake(string tag) { _ctx.Mistake(tag); }
        public void LockHints() { _ctx.LockHints(); }
        public void Next(float seconds) { _ctx.Next(seconds); }

        public void ShowNext() { MathUi.ShowNext(_ctx, _next); }                // parts.js showNext
    }
}
