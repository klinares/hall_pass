using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WorldTour.Core.Maths.Gen;
using WorldTour.Game.Map;
using WorldTour.Game.Motion;
using WorldTour.Game.Show;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// MODE: ticket sales (world_tour/modes/math/tickets.js): multi-digit multiplication and division on the box-office
    /// letterboard, typed on the shared keypad (or the real keys). From hint 2 the worked paper appears under the board:
    /// the area model for x, chunking for /, full rows and leftovers for the remainder problems; the worked solution
    /// completes it. Rules: <see cref="TypedMathRound"/>, <see cref="TicketsWork"/>.
    /// Logs "WT_MATH mode=tickets kind= level= seed= answer=", "WT_MATH_SUBMIT val= result= tries=", "WT_HINT mode=tickets n="
    /// and "WT_TICKETS_WORK n= kind=".
    /// </summary>
    public sealed class TicketsMode : MonoBehaviour, ISongMode
    {
        private SongContext _ctx;
        private RectTransform _desk, _slot;
        private ShowButton _next;

        public Problem Problem { get; private set; }
        public TypedMathRound Round { get; private set; }
        public MathKeypad Pad { get; private set; }
        public TicketsWork Work { get; private set; }

        public void Render(SongContext ctx)
        {
            _ctx = ctx;
            Problem p = Problem = ctx.Problem;
            MathUi.Columns(ctx.Area, out RectTransform left, out RectTransform right);
            _desk = MathUi.Desk(left, TicketsLines.DeskTag);
            Board(_desk, TicketsLines.Board(p));
            _slot = SpellingUi.Box("Work", _desk, -1, -1);
            var wl = _slot.gameObject.AddComponent<HorizontalLayoutGroup>();
            wl.childAlignment = TextAnchor.UpperCenter;
            wl.childControlWidth = true; wl.childControlHeight = true; wl.childForceExpandWidth = false; wl.childForceExpandHeight = false;
            _slot.gameObject.SetActive(false);
            _next = MathUi.Controls(ctx, left, out _);
            MathUi.PromptCard(right, p.Prompt, TicketsLines.PromptLabel(p.Kind));
            Pad = MathKeypad.Make(right, p.Answer.Type, TicketsLines.SellIt, () => MathUi.ShowNext(_ctx, _next));
            Round = new TypedMathRound(new MathSongHost(ctx, "tickets", () => _desk), p, Pad, s => ctx.Next(s), OnStep);
            Pad.Submitted += text =>
            {
                TypedMathResult r = Round.Submit(text);
                Debug.Log("WT_MATH_SUBMIT val=" + text + " result=" + r.ToString().ToLowerInvariant() + " tries=" + _ctx.Tries);
            };
            Debug.Log("WT_MATH mode=tickets kind=" + p.Kind + " level=" + p.Level + " seed=" + p.Seed + " answer=" + TypedMathRound.KeyedAnswer(p.Answer));
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);   // Enter goes to the pad
            ctx.Later(() => _ctx.Say(TicketsLines.Intro(p.Kind), "idle"), .01f);           // after the show's own song line
            ctx.Later(() => { if (Pad != null) Pad.LogPositions(); }, .45f);
        }

        /// <summary>tickets.js:65-71 onStep: the paper from step 2.</summary>
        private void OnStep(int n)
        {
            if (n < 2) return;
            Work = TicketsWork.Draw(Problem, n);
            if (Work == null) return;
            Debug.Log("WT_TICKETS_WORK n=" + n + " kind=" + Work.Kind);
            for (int i = _slot.childCount - 1; i >= 0; i--) { GameObject c = _slot.GetChild(i).gameObject; c.SetActive(false); Destroy(c); }
            RectTransform paper = Paper(_slot, Work);
            _slot.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_ctx.Area);
            Feedback.Slap(paper);
        }

        // ---------- drawing ----------

        /// <summary>math.css .letterboard: card tiles on a near-black board, the operator on paper-deep.</summary>
        private static void Board(RectTransform desk, string[] tokens)
        {
            const float tile = 38f, font = 30f;
            int chars = 0; foreach (string t in tokens) chars += t.Length;
            float w = chars * (tile * .72f + 3f) + (tokens.Length - 1) * 14f + 28f;
            RectTransform row = SpellingUi.Row(desk, "Board", 0, tile + 30f);
            RectTransform board = SpellingUi.Box("Letterboard", row, w, tile + 30f);
            Image edge = UiKit.Box(board, "Edge", ColorRole.Card);
            UiKit.Stretch(edge.rectTransform);
            Image bg = UiKit.Box(board, "Back", ColorRole.Ink);
            UiKit.Stretch(bg.rectTransform, 3f);
            // the board's own layout lays out the tiles only (the edge and the back stretch behind them)
            edge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            bg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            bg.color = new Color(.067f, .067f, .067f, 1f);                       // #111
            var h = board.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 14; h.childAlignment = TextAnchor.MiddleCenter; h.padding = new RectOffset(14, 14, 15, 15);
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            foreach (string t in tokens)
            {
                bool op = t == TicketsLines.Times || t.Length == 1 && t[0] == (char)0x00F7;
                RectTransform word = SpellingUi.Row(board, "Word", 3, tile);
                foreach (char ch in t)
                {
                    RectTransform cell = SpellingUi.Box("Tile", word, tile * .72f, tile);
                    ShowUi.Panel(cell, op ? ColorRole.PaperDeep : ColorRole.Card);
                    TMP_Text tx = ShowUi.Label(cell, "Ch", "", font, FontRole.BodyBold, ColorRole.Ink);
                    tx.richText = false;
                    tx.text = ch.ToString();
                }
            }
        }

        private static TMP_Text Text(RectTransform parent, string text, float size, FontRole font, ColorRole color, TextAlignmentOptions align, float height)
        {
            RectTransform b = SpellingUi.Box("Text", parent, -1, height);
            TMP_Text t = ShowUi.Label(b, "T", "", size, font, color, align);
            t.text = text;
            return t;
        }

        /// <summary>math.css .workpaper: a card with an ink border and a lilac shadow, holding the area model, chunks or seat rows.</summary>
        private static RectTransform Paper(RectTransform slot, TicketsWork w)
        {
            RectTransform rt = SpellingUi.Box("Paper", slot, w.Kind == "remainder" ? 360f : 380f, -1);
            MathUi.Card(rt, 3f, ColorRole.Card, ColorRole.Accent, 2f);
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(16, 16, 12, 12); v.spacing = 8;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            foreach (Transform c in rt) c.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;  // shadow, ring, face
            if (w.Kind == "area") Area(rt, w);
            else if (w.Kind == "chunks") Chunks(rt, w);
            else Rows(rt, w);
            return rt;
        }

        private static void Area(RectTransform paper, TicketsWork w)
        {
            int cols = w.ColHeads.Count + 1, rows = w.RowHeads.Count + 1;
            const float cellH = 52f, headH = 26f;
            RectTransform g = SpellingUi.Box("Area", paper, -1, headH + (rows - 1) * (cellH + 4f));
            var grid = g.gameObject.AddComponent<GridLayoutGroup>();
            float cw = Mathf.Min(110f, (348f - 4f * (cols - 1)) / cols);
            grid.cellSize = new Vector2(cw, cellH);
            grid.spacing = new Vector2(4, 4);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = cols;
            grid.childAlignment = TextAnchor.UpperCenter;
            ShowUi.Rect("Corner", g);
            foreach (string h in w.ColHeads) Head(g, h);
            for (int r = 0; r < w.RowHeads.Count; r++)
            {
                Head(g, w.RowHeads[r]);
                foreach (string c in w.Cells[r])
                {
                    RectTransform cell = ShowUi.Rect("Cell", g);
                    UiKit.Stretch(UiKit.Box(cell, "Ring", ColorRole.Ink).rectTransform);
                    Image f = UiKit.Box(cell, "Face", ColorRole.Card);
                    UiKit.Stretch(f.rectTransform, 2f);
                    f.color = MathUi.Mix(ColorRole.Accent, .35f, ColorRole.Card);
                    TMP_Text t = ShowUi.Label(cell, "T", "", 16f, FontRole.BodyBold, ColorRole.Ink);
                    t.text = c;
                }
            }
            if (w.Sum != null) Text(paper, w.Sum, 16f, FontRole.BodyBold, ColorRole.Ink, TextAlignmentOptions.Center, 26f);
        }

        private static void Head(RectTransform g, string text)
        {
            RectTransform h = ShowUi.Rect("Head", g);
            TMP_Text t = ShowUi.Label(h, "T", "", 16f, FontRole.BodyBold, ColorRole.Accent2);
            t.text = text;
        }

        private static void Chunks(RectTransform paper, TicketsWork w)
        {
            foreach (ChunkRow r in w.Rows)
            {
                if (r.Style == "left" || r.Style == "total")
                {
                    RectTransform rule = SpellingUi.Box("Rule", paper, -1, r.Style == "total" ? 6f : 2f);
                    UiKit.Stretch(UiKit.Box(rule, "Top", ColorRole.Ink).rectTransform);
                    if (r.Style == "total")
                    {
                        // 3px double: two lines with a gap
                        Image gap = UiKit.Box(rule, "Gap", ColorRole.Card);
                        UiKit.Stretch(gap.rectTransform); gap.rectTransform.offsetMin = new Vector2(0, 2); gap.rectTransform.offsetMax = new Vector2(0, -2);
                    }
                }
                RectTransform row = SpellingUi.Box("Row", paper, -1, 26f);
                TMP_Text a = ShowUi.Label(row, "Amount", "", 17.6f, FontRole.BodyBold, r.Style == "total" ? ColorRole.Accent2 : ColorRole.Ink, TextAlignmentOptions.MidlineLeft);
                a.richText = false; a.text = r.Text;
                if (r.Note != null)
                {
                    // .crow small: readable at 1366 (P9, polish 6.2B item 11): 16 px bold in full ink (6.2D item 3: thin Courier at 15 px antialiased to grey)
                    TMP_Text n = ShowUi.Label(row, "Note", "", 16f, FontRole.BodyBold, ColorRole.Ink, TextAlignmentOptions.MidlineRight);
                    n.richText = false; n.text = r.Note;
                }
            }
        }

        private static void Rows(RectTransform paper, TicketsWork w)
        {
            const float sw = 30f, sh = 22f; const int perRow = 10;              // larger than .seatrows i (P9, polish 6.2B item 11: 26 x 17 read as specks); 10 across fits the 348 px paper
            RectTransform flow = SpellingUi.Box("Seats", paper, -1, -1);
            var fl = flow.gameObject.AddComponent<GridLayoutGroup>();
            fl.cellSize = new Vector2(sw, sh); fl.spacing = new Vector2(4, 6);
            fl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; fl.constraintCount = perRow;
            fl.childAlignment = TextAnchor.MiddleLeft;
            int cells = w.Full + 1 + (w.Gap ? 1 : 0);
            flow.GetComponent<LayoutElement>().preferredHeight = Mathf.Ceil(cells / (float)perRow) * (sh + 6f);
            for (int i = 0; i < w.Full; i++) Seat(flow, 1.0);
            if (w.Gap)
            {
                RectTransform dots = ShowUi.Rect("Gap", flow);
                ShowUi.Label(dots, "T", TicketsLines.Ellipsis, 16f, FontRole.BodyBold, ColorRole.Ink);
            }
            Seat(flow, -w.Part);                                               // negative: the part row
            if (w.More != null) Text(paper, w.More, 14.4f, FontRole.Body, ColorRole.Ink, TextAlignmentOptions.Left, 20f).richText = false;
            Text(paper, w.Line, 16f, FontRole.BodyBold, ColorRole.Ink, TextAlignmentOptions.Center, 44f).textWrappingMode = TextWrappingModes.Normal;
        }

        /// <summary>.seatrows i: an ink-bordered seat row, lilac when full; the part row fills paper-deep to its share.</summary>
        private static void Seat(RectTransform flow, double fill)
        {
            RectTransform s = ShowUi.Rect("Seat", flow);
            UiKit.Stretch(UiKit.Box(s, "Ring", ColorRole.Ink).rectTransform);
            Image f = UiKit.Box(s, "Face", fill >= 0 ? ColorRole.Accent : ColorRole.Card);
            UiKit.Stretch(f.rectTransform, 2f);
            if (fill < 0)
            {
                Image part = UiKit.Box(f.rectTransform, "Part", ColorRole.PaperDeep);
                RectTransform pr = part.rectTransform;
                pr.anchorMin = Vector2.zero; pr.anchorMax = new Vector2((float)-fill, 1f);
                pr.offsetMin = pr.offsetMax = Vector2.zero;
            }
        }
    }
}
