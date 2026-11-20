using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;
using WorldTour.Game.Audio;
using WorldTour.Game.Map;
using WorldTour.Game.Motion;
using WorldTour.Game.Show;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// MODE: beat maker (world_tour/modes/math/beat.js). Fill the measure with note blocks (1/2 1/4 1/8 1/3 1/6 1/12) on the
    /// common-denominator grid: click a block to add it, or drag it onto the measure; click a placed note to take it back.
    /// "play it" plays the beat when the measure is exactly full; too much spills past the bar line. Take-out problems
    /// start too full: click written notes to take them out. Levels 2-3 also have typed add and subtract problems, with
    /// the two notes drawn on the grid and the answer on the keypad. Everything works from the keyboard: Tab to a block
    /// or a note and press Enter or Space; Tab to undo and play it.
    /// Rules: <see cref="BeatBoard"/>, <see cref="MathLadder"/>, <see cref="TypedMathRound"/>.
    /// Logs "WT_MATH mode=beat kind= level= seed= answer= builder=", "WT_BEAT_ADD d= result=", "WT_BEAT_TAP i= ok=",
    /// "WT_BEAT_PLAY result=", "WT_BEAT_POS palette=d:x:y,... track=x:y undo=x:y play=x:y", "WT_BEAT_NOTES pos=i:x:y,..."
    /// and, for typed problems, "WT_MATH_SUBMIT" and "WT_KEYPAD".
    /// </summary>
    public sealed class BeatMode : MonoBehaviour, ISongMode
    {
        /// <summary>beat.js:15 SHADE: each denominator's colour.</summary>
        public static Color Shade(int d)
        {
            switch (d)
            {
                case 2: return Theme.Color(ColorRole.Accent);
                case 3: return Theme.Color(ColorRole.PaperDeep);
                case 4: return Theme.Color(ColorRole.Card);
                case 6: return MathUi.Mix(ColorRole.PaperDeep, .55f, ColorRole.Card);
                case 8: return MathUi.Mix(ColorRole.Accent, .5f, ColorRole.Card);
                default: return Theme.Color(ColorRole.Paper);
            }
        }

        public const float RowHeight = 58f;

        private SongContext _ctx;
        private Problem _p;
        private RectTransform _desk, _track, _cut;
        private ShowButton _undo, _play, _next;
        private readonly List<BeatChip> _chips = new List<BeatChip>();
        private readonly List<RectTransform> _rows = new List<RectTransform>();
        private readonly List<BeatNoteButton> _notes = new List<BeatNoteButton>();
        private RectTransform _lastBarline;
        private bool _armed;

        public BeatBoard Board { get; private set; }
        public MathLadder Ladder { get; private set; }
        public TypedMathRound Typed { get; private set; }
        public MathKeypad Pad { get; private set; }
        public IReadOnlyList<BeatChip> Chips { get { return _chips; } }
        public IReadOnlyList<BeatNoteButton> Notes { get { return _notes; } }
        public ShowButton PlayButton { get { return _play; } }
        public ShowButton UndoButton { get { return _undo; } }
        public Problem Problem { get { return _p; } }

        public void Render(SongContext ctx)
        {
            _ctx = ctx;
            _p = ctx.Problem;
            if (_p.Builder != true) { RenderTyped(); return; }
            Board = new BeatBoard(_p);
            BuildBuilder();
            Ladder = new MathLadder(new MathSongHost(ctx, "beat", () => _desk), _p, OnStep);
            Paint();
            PaintCut();
            string sol = Board.Removable ? "cut=" + string.Join(",", BeatBoard.CheapestCut(Board.Pre, _p.Answer.Value)) : "fill=" + string.Join(",", Board.FillSolution());
            Debug.Log("WT_MATH mode=beat kind=" + _p.Kind + " level=" + _p.Level + " seed=" + _p.Seed + " answer=" + TypedMathRound.KeyedAnswer(_p.Answer)
                + " builder=true pre=" + string.Join(",", Board.Pre) + " palette=" + string.Join(",", Board.Palette) + " " + sol);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            ctx.Later(() => ctx.Say(Board.Removable ? MathLines.TooMany : MathLines.TapOrDrag, "idle"), .01f);
            ctx.Later(LogNotes, .45f);
        }

        // ---------- the builder ----------

        private void BuildBuilder()
        {
            MathUi.Columns(_ctx.Area, out RectTransform left, out RectTransform right);
            string tag = MathLines.BeatTag + (Board.Bars > 1 ? "[[" + Rational.Str(Board.Target) + "]]" + MathLines.Measures : MathLines.OneMeasure);
            _desk = MathUi.Desk(left, tag);

            _track = ShowUi.Rect("Track", _desk);
            MathUi.TopPivot(_track);
            var tl = _track.gameObject.AddComponent<VerticalLayoutGroup>();
            tl.spacing = 12;
            tl.childControlWidth = true; tl.childControlHeight = true; tl.childForceExpandWidth = true; tl.childForceExpandHeight = false;

            if (Board.Removable)
            {
                _cut = SpellingUi.Row(_desk, "Cut", 8, 44);
            }
            else
            {
                RectTransform palette = SpellingUi.Row(_desk, "Palette", 10, 70);
                foreach (int d in Board.Palette) _chips.Add(Chip(palette, d));
            }

            RectTransform btns = SpellingUi.Row(_desk, "BeatButtons", 12, Tokens.Tap);
            _undo = SpellingUi.Control(ShowUi.Button(_ctx.ButtonPrefab, btns, "Undo", Board.Removable ? MathLines.PutBack : MathLines.Undo, true, Undo), 150);
            _play = SpellingUi.Control(ShowUi.Button(_ctx.ButtonPrefab, btns, "Play", MathLines.PlayIt, false, PlayIt), 170);

            _next = MathUi.Controls(_ctx, left, out _);
            MathUi.PromptCard(right, _p.Prompt, Board.Removable ? MathLines.TrimIt : MathLines.FillIt);
        }

        /// <summary>math.css .nblock: the note's length as a bar, its fraction, its name.</summary>
        private BeatChip Chip(RectTransform parent, int d)
        {
            RectTransform rt = SpellingUi.Box("Block " + d, parent, 78, 68);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(1, 1, 1, 0);
            var chip = rt.gameObject.AddComponent<BeatChip>();
            chip.D = d;
            chip.targetGraphic = hit;
            chip.transition = Selectable.Transition.None;
            chip.Group = rt.gameObject.AddComponent<CanvasGroup>();
            chip.Body = ShowUi.Rect("Body", rt);
            ShowUi.Stretch(chip.Body);
            // border in card colour, shade face, a 3px paper-deep shadow
            Image sh = UiKit.Box(chip.Body, "Shadow", ColorRole.PaperDeep);
            UiKit.Stretch(sh.rectTransform); sh.rectTransform.anchoredPosition = new Vector2(3, -3);
            Image ring = UiKit.Box(chip.Body, "Ring", ColorRole.Card);
            UiKit.Stretch(ring.rectTransform);
            Image face = UiKit.Box(chip.Body, "Face", ColorRole.Card);
            UiKit.Stretch(face.rectTransform, Tokens.BorderWidth);
            face.color = Shade(d);

            // .bar: width min(1, 2/d) * 46 + 8, 5px, ink at .7
            RectTransform bar = ShowUi.Rect("Bar", face.rectTransform);
            bar.anchorMin = bar.anchorMax = new Vector2(.5f, 1); bar.pivot = new Vector2(.5f, 1);
            bar.sizeDelta = new Vector2(Mathf.Min(1f, 2f / d) * 46f + 8f, 5f);
            bar.anchoredPosition = new Vector2(0, -7);
            var bi = bar.gameObject.AddComponent<Image>();
            Color bc = Theme.Color(ColorRole.Ink); bc.a = .7f; bi.color = bc; bi.raycastTarget = false;
            // .nblock: 1.15rem bold, the fraction at that size
            RectTransform f = MathUi.Frac(face.rectTransform, null, "1", d.ToString(System.Globalization.CultureInfo.InvariantCulture), 17f, ColorRole.Ink, true);
            f.anchorMin = f.anchorMax = new Vector2(.5f, .5f); f.pivot = new Vector2(.5f, .5f); f.anchoredPosition = new Vector2(0, -1);
            RectTransform small = ShowUi.Rect("Name", face.rectTransform);
            small.anchorMin = new Vector2(0, 0); small.anchorMax = new Vector2(1, 0); small.pivot = new Vector2(.5f, 0);
            small.sizeDelta = new Vector2(0, 14); small.anchoredPosition = new Vector2(0, 2);
            ShowUi.Label(small, "Text", Fractions.Names[d], 10.4f, FontRole.Body, ColorRole.Ink);

            chip.Tapped = c => Add(c.D);
            chip.Dropped = c => Add(c.D);
            chip.IsOverTarget = OverTrack;
            chip.Lifted = on => { _armed = on; PaintArmed(); };
            return chip;
        }

        /// <summary>dnd.js zoneAt: the measure, with 12 px of slack round it.</summary>
        private bool OverTrack(Vector2 screen, Camera cam)
        {
            if (_track == null) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_track, screen, cam, out Vector2 local)) return false;
            Rect r = _track.rect;
            r.xMin -= 12; r.xMax += 12; r.yMin -= 12; r.yMax += 12;
            return r.Contains(local);
        }

        private void Add(int d)
        {
            BeatAdd r = Board.Add(d);
            Debug.Log("WT_BEAT_ADD d=" + d + " result=" + r.ToString().ToLowerInvariant());
            if (r == BeatAdd.NoRoom) { _ctx.Say(MathLines.NoRoom, "think"); return; }
            if (r != BeatAdd.Added) return;
            Sfx.Play(SfxId.Tap);
            Paint();
            BeatNoteButton last = _notes.LastOrDefault(n => n != null && n.name.StartsWith("Note new", System.StringComparison.Ordinal));
            if (last != null) Feedback.Slap((RectTransform)last.transform.GetChild(0));
        }

        private void TapNote(BeatNoteButton b)
        {
            int index = b.Index;
            bool keyboard = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == b.gameObject;
            bool ok = Board.Tap(index);
            Debug.Log("WT_BEAT_TAP i=" + index + " ok=" + (ok ? "true" : "false"));
            if (!ok) return;
            Sfx.Play(SfxId.Tap);
            Paint();
            PaintCut();
            if (keyboard && EventSystem.current != null)
            {
                // the focused note is gone: keep the keyboard on the measure (the note now in its place), else the next control
                BeatNoteButton same = _notes.FirstOrDefault(n => n.Index == index) ?? _notes.LastOrDefault();
                GameObject next = same != null ? same.gameObject : _chips.Count > 0 ? _chips[0].gameObject : _undo != null ? _undo.Go : null;
                EventSystem.current.SetSelectedGameObject(next);
            }
        }

        private void Undo()
        {
            if (!Board.Undo()) return;
            Paint();
            PaintCut();
        }

        private void OnStep(int n)
        {
            Board.Step(n);
            Paint();
            PaintCut();
            if (n == 1 && _lastBarline != null) Feedback.Pop(_lastBarline);
            if (n >= 3) Lock();
        }

        private void PlayIt()
        {
            if (Board.Done || Ladder.Over) return;
            BeatPlay r = Board.Play();
            Debug.Log("WT_BEAT_PLAY result=" + (r == BeatPlay.Filled ? "filled" : r == BeatPlay.TooLong ? "too-long" : r == BeatPlay.TooShort ? "too-short" : r == BeatPlay.Empty ? "empty" : "ignored"));
            if (r == BeatPlay.Empty) { _ctx.Say(MathLines.AddANoteFirst, "think"); return; }   // QUESTIONS 15: not an attempt
            if (r == BeatPlay.Filled)
            {
                Paint();
                _ctx.LockHints();
                var notes = Board.Notes().Select(x => new BeatNote(x.Key, x.Value)).ToArray();
                double secs = Sfx.Beat(notes);
                double total = notes.Sum(x => x.Len);
                Playhead(total);
                Lock();
                Ladder.Right();
                _ctx.Next(Mathf.Max(1.6f, (float)((secs > 0 ? secs : 1.6 * Board.Bars) + .5)));
                return;
            }
            if (r == BeatPlay.Ignored) return;
            Feedback.Wobble(_track);
            if (Ladder.Wrong(null, r == BeatPlay.TooLong ? "too-long" : "too-short"))
            {
                Lock();
                ShowNext();
            }
        }

        /// <summary>beat.js:159-161: no more blocks, undo or play.</summary>
        private void Lock()
        {
            if (_play != null) _play.Interactable = false;
            if (_undo != null) _undo.Interactable = false;
            foreach (BeatChip c in _chips) c.interactable = false;
            foreach (BeatChip c in _chips) { var g = c.GetComponent<CanvasGroup>(); if (g != null) g.alpha = .5f; }
        }

        private void ShowNext() { MathUi.ShowNext(_ctx, _next); }

        // ---------- the measure ----------

        /// <summary>beat.js:94-108 paint(): the rows, the notes laid end to end, the bar line and the spill.</summary>
        private void Paint()
        {
            List<BeatBlock> bl = Board.Blocks();
            DrawTrack(bl, Board.Rows(), Board.Target, Board.Grid, Board.Stage >= 2, Board.Over());
        }

        private void DrawTrack(List<BeatBlock> blocks, int rows, Rational? target, int grid, bool showGrid, bool over)
        {
            for (int i = _track.childCount - 1; i >= 0; i--) { GameObject c = _track.GetChild(i).gameObject; c.SetActive(false); Destroy(c); }
            _rows.Clear();
            _notes.Clear();
            _lastBarline = null;
            List<BeatPiece> pieces = BeatBoard.Layout(blocks.Select(b => b.Len).ToList(), rows);
            for (int r = 0; r < rows; r++)
            {
                RectTransform row = SpellingUi.Box("Row " + r, _track, -1, RowHeight);
                _rows.Add(row);
                // .mrow: a card-coloured border, a faint fill and a grid line every 1/grid
                Image ring = UiKit.Box(row, "Ring", ColorRole.Card);
                UiKit.Stretch(ring.rectTransform);
                Image fill = UiKit.Box(row, "Fill", ColorRole.Ink);
                UiKit.Stretch(fill.rectTransform, Tokens.BorderWidth);
                fill.color = MathUi.Mix(ColorRole.Card, .07f, ColorRole.Ink);
                RectTransform inner = fill.rectTransform;
                inner.gameObject.AddComponent<RectMask2D>();
                Color gl = MathUi.Mix(ColorRole.Card, .34f, ColorRole.Ink);
                for (int k = 1; k < grid; k++) VLine(inner, "Grid", (float)k / grid, 1.5f, gl, 0f);

                double? end = BeatBoard.BarEnd(target, r);
                if (end.HasValue && end.Value < 1)
                {
                    // .spill: hatched past the bar line
                    RectTransform sp = ShowUi.Rect("Spill", inner);
                    sp.anchorMin = new Vector2((float)end.Value, 0); sp.anchorMax = Vector2.one; sp.offsetMin = sp.offsetMax = Vector2.zero;
                    var si = sp.gameObject.AddComponent<Image>();
                    si.sprite = MathUi.Stripes; si.type = Image.Type.Tiled; si.raycastTarget = false; si.pixelsPerUnitMultiplier = .7f;
                    si.color = MathUi.Mix(ColorRole.PaperDeep, .45f, ColorRole.Ink);
                }
                foreach (BeatPiece pc in pieces.Where(x => x.Row == r)) Note(inner, blocks[pc.Block], pc);
                if (showGrid) GridNumbers(inner, grid);
                if (end.HasValue && end.Value > 0 && end.Value <= 1)
                {
                    // .barline: 5px of paper with a 2px ink outline, standing 3px proud of the row
                    RectTransform blr = ShowUi.Rect("Barline", row);
                    blr.anchorMin = new Vector2((float)end.Value, 0); blr.anchorMax = new Vector2((float)end.Value, 1);
                    float x = (float)end.Value;
                    blr.pivot = new Vector2(.5f, .5f);
                    blr.sizeDelta = new Vector2(9f, 0f);                                // the row's own height: 3px past the inner fill each way
                    blr.anchoredPosition = new Vector2(Tokens.BorderWidth * (1 - 2 * x), 0);
                    var o = blr.gameObject.AddComponent<Image>(); o.color = Theme.Color(ColorRole.Ink); o.raycastTarget = false;
                    RectTransform pin = ShowUi.Rect("Paper", blr);
                    ShowUi.Stretch(pin, 2f);
                    var pi = pin.gameObject.AddComponent<Image>(); pi.color = Theme.Color(ColorRole.Paper); pi.raycastTarget = false;
                    _lastBarline = blr;
                }
            }
            // .track.over .mrow:last-child: a paper-deep ring round the row that spills
            if (over && _rows.Count > 0) Glow(_rows[_rows.Count - 1], Theme.Color(ColorRole.PaperDeep), "OverRing");
            PaintArmed();
            if (Board != null && !Board.Done) _ctx.Later(LogNotes, .05f);   // after layout: "WT_BEAT_NOTES" and "WT_BEAT_POS"
        }

        private void PaintArmed()
        {
            foreach (RectTransform row in _rows)
            {
                if (row == null) continue;
                Transform g = row.Find("Armed");
                if (_armed && g == null) { Color c = Theme.Color(ColorRole.Accent); c.a = .6f; Glow(row, c, "Armed"); }
                else if (!_armed && g != null) Destroy(g.gameObject);
            }
        }

        private static void Glow(RectTransform row, Color c, string name)
        {
            RectTransform g = ShowUi.Rect(name, row);
            ShowUi.Stretch(g, -4f);
            g.SetAsFirstSibling();
            Image gi = UiKit.Box(g, "Box", ColorRole.Accent);
            UiKit.Stretch(gi.rectTransform);
            gi.color = c;
        }

        private static void VLine(RectTransform parent, string name, float x, float width, Color c, float inset)
        {
            RectTransform l = ShowUi.Rect(name, parent);
            l.anchorMin = new Vector2(x, 0); l.anchorMax = new Vector2(x, 1);
            l.pivot = new Vector2(0, .5f);
            l.sizeDelta = new Vector2(width, -2 * inset);
            l.anchoredPosition = Vector2.zero;
            var i = l.gameObject.AddComponent<Image>(); i.color = c; i.raycastTarget = false;
        }

        /// <summary>beat.js:44-48 a note piece: its shade, an ink border, the label on its first piece if it's wide enough.</summary>
        private void Note(RectTransform row, BeatBlock b, BeatPiece pc)
        {
            bool tap = b.Tap;
            string kind = b.Gone ? "gone" : b.Pre ? "pre" : "new";
            RectTransform rt = ShowUi.Rect("Note " + kind + " " + pc.Block, row);
            rt.anchorMin = new Vector2((float)pc.X, 0); rt.anchorMax = new Vector2((float)(pc.X + pc.W), 1);
            rt.offsetMin = new Vector2(1, 6 - Tokens.BorderWidth); rt.offsetMax = new Vector2(-1, -(6 - Tokens.BorderWidth));
            RectTransform body = ShowUi.Rect("Body", rt);
            ShowUi.Stretch(body);
            Image ring = UiKit.Box(body, "Ring", ColorRole.Ink);
            UiKit.Stretch(ring.rectTransform);
            if (b.Gone) { Color rc = ring.color; rc.a = .45f; ring.color = rc; }
            Image face = UiKit.Box(body, "Face", ColorRole.Card);
            UiKit.Stretch(face.rectTransform, 2.5f);
            face.color = b.Gone ? MathUi.Mix(ColorRole.Card, .45f, ColorRole.Ink) : Shade(b.D);
            if (b.Pre && !b.Gone)
            {
                // .note.pre: faint ink hatching, so written notes read apart from hers
                RectTransform h = ShowUi.Rect("Hatch", face.rectTransform);
                ShowUi.Stretch(h);
                var hi = h.gameObject.AddComponent<Image>();
                hi.sprite = MathUi.Stripes; hi.type = Image.Type.Tiled; hi.raycastTarget = false; hi.pixelsPerUnitMultiplier = .8f;
                Color hc = Theme.Color(ColorRole.Ink); hc.a = .12f; hi.color = hc;
            }
            if (pc.First && pc.W >= 1.0 / 13)
            {
                MathSeg s = MathText.Parse(b.Label).FirstOrDefault(x => x.IsFrac);
                if (s.IsFrac)
                {
                    // P4: 16 read about 9.6 px a digit. Raised 6.5 px so it clears the cell numbers along the note's bottom
                    // (fix round 2: at 20 px, centred, the denominator touched them)
                    RectTransform f = MathUi.Frac(face.rectTransform, s.Whole, s.Num, s.Den, 19f, ColorRole.Ink, true);
                    f.anchorMin = f.anchorMax = f.pivot = new Vector2(.5f, .5f);
                    f.anchoredPosition = new Vector2(0f, 6.5f);
                }
            }
            if (tap && !b.Pre)
            {
                // button.note.new::after: a small x in the corner
                RectTransform x = ShowUi.Rect("X", face.rectTransform);
                x.anchorMin = x.anchorMax = x.pivot = new Vector2(1, 1);
                x.sizeDelta = new Vector2(12, 12); x.anchoredPosition = new Vector2(-2, -1);
                TMP_Text xt = ShowUi.Label(x, "Text", "x", 11.2f, FontRole.Body, ColorRole.Ink);
                Color xc = xt.color; xc.a = .6f; xt.color = xc;
            }
            if (tap && pc.First)
            {
                var hit = rt.gameObject.AddComponent<Image>();
                hit.color = new Color(1, 1, 1, 0);
                var btn = rt.gameObject.AddComponent<BeatNoteButton>();
                btn.Index = pc.Block;
                btn.targetGraphic = hit;
                btn.transition = Selectable.Transition.None;
                btn.Tapped = TapNote;
                _notes.Add(btn);
            }
            else if (tap)
            {
                // the second piece of a cut note: a click there takes the same note back
                var hit = rt.gameObject.AddComponent<Image>();
                hit.color = new Color(1, 1, 1, 0);
                var btn = rt.gameObject.AddComponent<BeatNoteButton>();
                btn.Index = pc.Block;
                btn.targetGraphic = hit;
                btn.transition = Selectable.Transition.None;
                var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;
                btn.Tapped = TapNote;
            }
        }

        /// <summary>beat.js:51 .gridnums: the grid counted 1..g along the bottom (from the partial step).</summary>
        /// <summary>
        /// A typed problem's picture before the worked step: one row per note, each from the left edge, with only its own
        /// cells numbered (BeatTypedPicture).
        /// </summary>
        private void DrawSeparate(List<BeatTypedRow> rows, int grid)
        {
            for (int i = _track.childCount - 1; i >= 0; i--) { GameObject c = _track.GetChild(i).gameObject; c.SetActive(false); Destroy(c); }
            _rows.Clear();
            _notes.Clear();
            _lastBarline = null;
            for (int r = 0; r < rows.Count; r++)
            {
                RectTransform row = SpellingUi.Box("Row " + r, _track, -1, RowHeight);
                _rows.Add(row);
                Image ring = UiKit.Box(row, "Ring", ColorRole.Card);
                UiKit.Stretch(ring.rectTransform);
                Image fill = UiKit.Box(row, "Fill", ColorRole.Ink);
                UiKit.Stretch(fill.rectTransform, Tokens.BorderWidth);
                fill.color = MathUi.Mix(ColorRole.Card, .07f, ColorRole.Ink);
                RectTransform inner = fill.rectTransform;
                inner.gameObject.AddComponent<RectMask2D>();
                Color gl = MathUi.Mix(ColorRole.Card, .34f, ColorRole.Ink);
                for (int k = 1; k < grid; k++) VLine(inner, "Grid", (float)k / grid, 1.5f, gl, 0f);
                for (int i = 0; i < rows[r].Blocks.Count; i++)
                {
                    BeatTypedBlock x = rows[r].Blocks[i];
                    var b = new BeatBlock { Len = x.Len, D = x.Shade, Pre = true, Gone = x.Gone, Label = x.Label };
                    Note(inner, b, new BeatPiece { Block = r, Row = r, X = Rational.Num(x.Start), W = Rational.Num(x.Len), First = true });
                }
                if (rows[r].Numbers > 0) GridNumbers(inner, grid, rows[r].Numbers);
            }
        }

        /// <summary>
        /// .gridnums: the cell numbers along the bottom of a row, the first <paramref name="count"/> of them. 14 px ink with a
        /// card outline, so they read on light and dark notes alike (P4; the web's .62rem on-ink nearly vanished on light notes).
        /// </summary>
        private static void GridNumbers(RectTransform row, int grid, int count = -1)
        {
            if (count < 0) count = grid;
            for (int k = 0; k < count; k++)
            {
                RectTransform n = ShowUi.Rect("GridNo", row);
                n.anchorMin = new Vector2((float)k / grid, 0); n.anchorMax = new Vector2((float)(k + 1) / grid, 0);
                n.pivot = new Vector2(.5f, 0);
                n.sizeDelta = new Vector2(0, 15); n.anchoredPosition = new Vector2(0, 0);
                TMP_Text t = ShowUi.Label(n, "Text", (k + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), 14f, FontRole.BodyBold, ColorRole.Ink);
                t.outlineWidth = .3f;
                t.outlineColor = Theme.Color(ColorRole.Card);
            }
        }

        /// <summary>beat.js:75-77 the cut tray: the notes she took out, or how to start.</summary>
        private void PaintCut()
        {
            if (_cut == null) return;
            for (int i = _cut.childCount - 1; i >= 0; i--) { GameObject c = _cut.GetChild(i).gameObject; c.SetActive(false); Destroy(c); }
            if (Board.Removed.Count == 0)
            {
                TMP_Text t = SpellingUi.Text(_cut, "Hint", MathLines.TapToTakeOut, Tokens.Fs0, FontRole.Body, ColorRole.OnInk, true);
                var le = t.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 420; le.preferredHeight = 44;
                return;
            }
            foreach (int i in Board.Removed)
            {
                int d = Board.Pre[i];
                RectTransform chip = SpellingUi.Box("Taken " + i, _cut, 44, 44);
                var g = chip.gameObject.AddComponent<CanvasGroup>(); g.alpha = .75f;
                Image f = UiKit.Box(chip, "Face", ColorRole.Card);
                UiKit.Stretch(f.rectTransform);
                f.color = Shade(d);
                SpellingUi.DashedBorder(chip, 44, 44);
                RectTransform fr = MathUi.Frac(chip, null, "1", d.ToString(System.Globalization.CultureInfo.InvariantCulture), 15f, ColorRole.Ink, true);
                fr.anchorMin = fr.anchorMax = fr.pivot = new Vector2(.5f, .5f); fr.anchoredPosition = Vector2.zero;
            }
        }

        /// <summary>beat.js:194-205 a cursor runs along each row while the beat plays (1.6 s a measure); with reduce motion the rows light up.</summary>
        private void Playhead(double total)
        {
            int n = Mathf.Max(1, Mathf.Min(_rows.Count, (int)System.Math.Ceiling(total - 1e-9)));
            if (MotionPrefs.Reduced)
            {
                for (int i = 0; i < n; i++) { Color c = Theme.Color(ColorRole.Accent); Glow(_rows[i], c, "Lit"); }
                return;
            }
            for (int i = 0; i < n; i++)
            {
                RectTransform row = _rows[i];
                RectTransform h = ShowUi.Rect("Playhead", row);
                h.anchorMin = new Vector2(0, 0); h.anchorMax = new Vector2(0, 1);
                h.pivot = new Vector2(.5f, .5f);
                h.sizeDelta = new Vector2(4, 0);
                var hi = h.gameObject.AddComponent<Image>(); hi.color = Theme.Color(ColorRole.OnInk); hi.raycastTarget = false;
                h.gameObject.SetActive(false);
                float delay = i * 1.6f;
                _ctx.Later(() =>
                {
                    if (h == null) return;
                    h.gameObject.SetActive(true);
                    Tweener.Float(0f, 1f, 1.6f, Ease.Linear, v => { if (h != null) { h.anchorMin = new Vector2(v, 0); h.anchorMax = new Vector2(v, 1); } },
                        () => { if (h != null) Destroy(h.gameObject); }, h, honourReduced: false);
                }, delay + .001f);
            }
        }

        // ---------- typed add / subtract (beat.js:208-230) ----------

        private void RenderTyped()
        {
            JsonValue pr = _p.Params;
            Rational a = pr["a"].AsRational(), b = pr["b"].AsRational();
            bool plus = pr["op"].String == "+";
            int grid = pr["grid"].Int;

            MathUi.Columns(_ctx.Area, out RectTransform left, out RectTransform right);
            _desk = MathUi.Desk(left, MathLines.BeatTag + (plus ? MathLines.AddThem : MathLines.CutIt));
            _track = ShowUi.Rect("Track", _desk);
            MathUi.TopPivot(_track);
            var tl = _track.gameObject.AddComponent<VerticalLayoutGroup>();
            tl.spacing = 12;
            tl.childControlWidth = true; tl.childControlHeight = true; tl.childForceExpandWidth = true; tl.childForceExpandHeight = false;
            SpellingUi.Text(_desk, "Tap", plus ? MathLines.BassThenDrums : MathLines.FadedPartCut, Tokens.Fs0, FontRole.Body, ColorRole.OnInk, true);
            _next = MathUi.Controls(_ctx, left, out _);

            int stage = 0;
            System.Action draw = () =>
            {
                // answer 16: separate rows before the worked step, the web's end-to-end picture at it (BeatTypedPicture)
                if (!BeatTypedPicture.Joined(stage)) { DrawSeparate(BeatTypedPicture.Rows(_p, stage), grid); return; }
                List<BeatBlock> blocks = BeatTypedPicture.JoinedBlocks(_p)
                    .Select(x => new BeatBlock { Len = x.Len, D = x.Shade, Pre = true, Gone = x.Gone, Label = x.Label }).ToList();
                DrawTrack(blocks, BeatTypedPicture.JoinedRows(_p), null, grid, true, false);
            };
            draw();

            MathUi.PromptCard(right, _p.Prompt, plus ? MathLines.Add : MathLines.Subtract);
            Pad = MathKeypad.Make(right, _p.Answer.Type, MathLines.LockItIn, ShowNext);
            Typed = new TypedMathRound(new MathSongHost(_ctx, "beat", () => _desk), _p, Pad, s => _ctx.Next(s),
                n => { stage = Mathf.Max(stage, n); draw(); },
                () => Sfx.Beat(new[] { new BeatNote(Rational.Num(a), (int)a.D), new BeatNote(Rational.Num(b), (int)b.D) }));
            Ladder = Typed.Ladder;
            Pad.Submitted += text =>
            {
                TypedMathResult r = Typed.Submit(text);
                Debug.Log("WT_MATH_SUBMIT val=" + text + " result=" + r.ToString().ToLowerInvariant() + " tries=" + _ctx.Tries);
            };
            Debug.Log("WT_MATH mode=beat kind=" + _p.Kind + " level=" + _p.Level + " seed=" + _p.Seed + " answer=" + TypedMathRound.KeyedAnswer(_p.Answer) + " builder=false");
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            _ctx.Later(() => _ctx.Say(plus ? MathLines.HowLongTogether : MathLines.HowMuchLeft, "idle"), .01f);
            _ctx.Later(() => { if (Pad != null) Pad.LogPositions(); }, .45f);
        }

        // ---------- logs for the browser tests ----------

        private void LogPositions()
        {
            if (this == null || Board == null) return;
            Canvas.ForceUpdateCanvases();
            var sb = new System.Text.StringBuilder("WT_BEAT_POS palette=");
            for (int i = 0; i < _chips.Count; i++) { if (i > 0) sb.Append(','); sb.Append(_chips[i].D).Append(':').Append(MathUi.Centre((RectTransform)_chips[i].transform)); }
            sb.Append(" track=").Append(MathUi.Centre(_track));
            if (_undo != null) sb.Append(" undo=").Append(MathUi.Centre(_undo.Rect));
            if (_play != null) sb.Append(" play=").Append(MathUi.Centre(_play.Rect));
            Debug.Log(sb.ToString());
        }

        private void LogNotes()
        {
            if (this == null || Board == null) return;
            Canvas.ForceUpdateCanvases();
            var sb = new System.Text.StringBuilder("WT_BEAT_NOTES pos=");
            for (int i = 0; i < _notes.Count; i++) { if (i > 0) sb.Append(','); sb.Append(_notes[i].Index).Append(':').Append(MathUi.Centre((RectTransform)_notes[i].transform)); }
            Debug.Log(sb.ToString());
            LogPositions();                                                    // the measure's rows can change, moving the buttons
        }
    }
}
