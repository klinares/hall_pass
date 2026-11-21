using System.Collections.Generic;
using System.Linq;
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
    /// MODE: tour budget (world_tour/modes/math/budget.js): order of operations on the band's budget sheet, a torn lined
    /// page. Levels 1-2: work the expression out on the shared keypad. Level 3: first pick the expression that tells the
    /// story (four choices; a wrong one is a try), then the keypad appears to work it out. The sheet rewrites itself with
    /// the ladder: the first operation highlighted with its result at step 2, every step at the worked solution.
    /// Rules: <see cref="BudgetRound"/>, <see cref="TypedMathRound"/>.
    /// Keyboard: a story's first choice has focus (arrows and Tab move, Enter picks); the keypad takes the number keys
    /// and Enter once it shows. While the choices are up the keypad is hidden and takes no keys (on the web it listened
    /// anyway).
    /// Logs "WT_MATH mode=budget kind= level= seed= answer=", "WT_BUDGET_PICK i= result= tries=", "WT_MATH_SUBMIT val=
    /// result= tries=", "WT_HINT mode=budget n=" and "WT_CHOICES pos=" (story).
    /// </summary>
    public sealed class BudgetMode : MonoBehaviour, ISongMode, IBudgetView
    {
        /// <summary>The story choices' grid, under the sheet in the left column.</summary>
        public const float ChoicesWidth = 560f;

        private SongContext _ctx;
        private RectTransform _paper, _sheet, _padSlot;
        private ChoiceGrid _grid;
        private ShowButton _next;

        public Problem Problem { get; private set; }
        public BudgetRound Game { get; private set; }
        public TypedMathRound Round { get; private set; }
        public MathKeypad Pad { get; private set; }

        public void Render(SongContext ctx)
        {
            _ctx = ctx;
            Problem p = Problem = ctx.Problem;
            var song = new MathSongHost(ctx, "budget", () => _paper);
            Game = new BudgetRound(p, song, this);
            Build(ctx.Area, p);
            Round = new TypedMathRound(song, p, Pad, s => ctx.Next(s), Game.Step);
            Game.Ladder = Round.Ladder;
            Pad.Submitted += text =>
            {
                TypedMathResult r = Round.Submit(text);
                Debug.Log("WT_MATH_SUBMIT val=" + text + " result=" + r.ToString().ToLowerInvariant() + " tries=" + _ctx.Tries);
            };
            Paint();
            Debug.Log("WT_MATH mode=budget kind=" + p.Kind + " level=" + p.Level + " seed=" + p.Seed + " answer=" + TypedMathRound.KeyedAnswer(p.Answer) + " right=" + (p.Choices != null ? p.Choices.FindIndex(c => c.Correct) : -1));
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (Game.Story) ctx.Later(() => { if (_grid != null && EventSystem.current != null && !Game.Picked) EventSystem.current.SetSelectedGameObject(_grid.Buttons[0].Go); }, TypedRound.FocusDelay);
            ctx.Later(() => _ctx.Say(BudgetLines.Intro(Game.Story), "idle"), .01f);
            ctx.Later(() => { if (_grid != null) _grid.LogPositions("WT_CHOICES"); if (Pad != null && Game.Picked) Pad.LogPositions(); }, .45f);
        }

        private void Build(RectTransform area, Problem p)
        {
            MathUi.Columns(area, out RectTransform left, out RectTransform right);

            // .budget: a torn lined page with tape; .bhead: the heading and the rule, over a double line
            _paper = SpellingUi.Box("Budget", left, -1, -1);
            MathUi.TopPivot(_paper);          // polish 6.2D item 1: a tween (or the grown 4-line sheet) keeps the top edge; a centre pivot grew the page upward over "song n of m"
            ShowUi.Panel(_paper, ColorRole.Card, 1f, SpriteRole.PaperPanel);
            var v = _paper.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(46, 18, 22, 22); v.spacing = 6;
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            RectTransform head = SpellingUi.Box("Head", _paper, -1, 32);
            KeepHeight(head);
            ShowUi.Label(head, "Title", BudgetLines.Head, 27.2f, FontRole.Display, ColorRole.Ink, TextAlignmentOptions.BottomLeft);
            TMP_Text rule = ShowUi.Label(head, "Rule", "", 20f, FontRole.BodyBold, ColorRole.Ink, TextAlignmentOptions.BottomRight);
            rule.text = p.Level == 0 ? BudgetLines.RuleNoParens : BudgetLines.Rule;   // the warm-up has no parentheses to list
            RectTransform dbl = SpellingUi.Box("Double", _paper, -1, 7);
            KeepHeight(dbl);
            UiKit.Stretch(UiKit.Box(dbl, "Ink", ColorRole.Ink).rectTransform);
            Image gap = UiKit.Box(dbl, "Gap", ColorRole.Card);
            UiKit.Stretch(gap.rectTransform); gap.rectTransform.offsetMin = new Vector2(0, 2); gap.rectTransform.offsetMax = new Vector2(0, -2);
            _sheet = SpellingUi.Box("Sheet", _paper, -1, -1);
            MathUi.TopPivot(_sheet);
            var sv = _sheet.gameObject.AddComponent<VerticalLayoutGroup>();
            sv.spacing = 2; sv.childControlWidth = true; sv.childControlHeight = true; sv.childForceExpandWidth = true; sv.childForceExpandHeight = false;

            // the story's choices sit under the sheet: the right column (prompt card and keypad) has no room for them at 1366x768
            if (Game.Story)
            {
                _grid = new ChoiceGrid(left, _ctx.ButtonPrefab, p.Choices.Select(c => c.Text).ToList(), Pick, ChoicesWidth);
                foreach (ShowButton b in _grid.Buttons)
                    foreach (TMP_Text t in b.Go.GetComponentsInChildren<TMP_Text>(true)) { Theme.ApplyFont(t, FontRole.BodyBold); t.fontSize = 19f; }
                for (int i = 0; i < _grid.Buttons.Count; i++)
                    _grid.Buttons[i].Rect.localEulerAngles = new Vector3(0, 0, -BudgetLines.Tilts[i % 4]);
            }
            _next = MathUi.Controls(_ctx, left, out _);

            MathUi.PromptCard(right, Game.Story ? p.Prompt : BudgetLines.WhatComes, BudgetLines.PromptLabel(Game.Story));
            _padSlot = SpellingUi.Box("PadSlot", right, -1, -1);
            var pv = _padSlot.gameObject.AddComponent<VerticalLayoutGroup>();
            pv.childControlWidth = true; pv.childControlHeight = true; pv.childForceExpandWidth = true; pv.childForceExpandHeight = false;
            Pad = MathKeypad.Make(_padSlot, p.Answer.Type, BudgetLines.BalanceIt, () => MathUi.ShowNext(_ctx, _next));
            _padSlot.gameObject.SetActive(!Game.Story);
        }

        /// <summary>
        /// Minimum height = preferred height. Without it a sheet that grows to 3 or 4 lines at the worked step is squeezed
        /// by the column's layout, and its lines (centred in their squeezed rows) spill upward over "song n of m". With it
        /// the column keeps its top edge and the extra height goes downward.
        /// </summary>
        private static void KeepHeight(RectTransform rt)
        {
            var le = rt.GetComponent<LayoutElement>();
            if (le != null) le.minHeight = le.preferredHeight;
        }

        private void Pick(int i)
        {
            BudgetPick r = Game.Pick(i);
            if (r != BudgetPick.Ignored) Debug.Log("WT_BUDGET_PICK i=" + i + " result=" + r.ToString().ToLowerInvariant() + " tries=" + _ctx.Tries);
        }

        // ---------- IBudgetView ----------

        /// <summary>budget.js sheet(): the lines, the first step under the highlighter (--hl: lilac at 60%).</summary>
        public void Paint()
        {
            if (_sheet == null) return;
            for (int i = _sheet.childCount - 1; i >= 0; i--) { GameObject c = _sheet.GetChild(i).gameObject; c.SetActive(false); Destroy(c); }
            string hl = ColorUtility.ToHtmlStringRGB(MathUi.Mix(ColorRole.Accent, .6f, ColorRole.Card));
            foreach (SheetLine l in Game.Sheet())
            {
                RectTransform row = SpellingUi.Box("Line", _sheet, -1, l.Question ? 38f : 46f);
                KeepHeight(row);                                              // polish 6.2B item 2: a line is never squeezed shorter than its text
                TMP_Text t = ShowUi.Label(row, "T", "", l.Question ? 24f : 30f, l.Question ? FontRole.Display : FontRole.BodyBold,
                    l.Final ? ColorRole.Accent2 : ColorRole.Ink, TextAlignmentOptions.MidlineLeft);
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.overflowMode = TextOverflowModes.Overflow;
                t.text = "<noparse>" + l.Before + "</noparse>"
                    + (l.Mark.Length > 0 ? "<mark=#" + hl + "99><noparse>" + l.Mark + "</noparse></mark>" : "")
                    + "<noparse>" + l.After + "</noparse>";
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(_ctx.Area);
        }

        public void SlapSheet() { Feedback.Slap(_sheet); }
        public void FreezeChoices() { if (_grid != null) _grid.Freeze(); }
        public void Good(int i, bool pop) { if (_grid != null) _grid.Good(i, pop); }
        public void Gone(int i) { Debug.Log("WT_CHOICE_GONE i=" + i); if (_grid != null) _grid.Gone(i, true); }

        public void ShowPad()
        {
            if (_padSlot.gameObject.activeSelf) return;
            _padSlot.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_ctx.Area);
            Feedback.Slap(_padSlot);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);   // Enter goes to the pad
            _ctx.Later(() => { if (Pad != null) Pad.LogPositions(); }, .45f);
        }

        public void ShowAnswerOnPad(string text) { Pad.ShowAnswer(text); }
        public void ShowNext() { MathUi.ShowNext(_ctx, _next); }
    }
}
