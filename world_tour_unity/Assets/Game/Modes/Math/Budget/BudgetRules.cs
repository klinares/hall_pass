using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Game.Modes
{
    /// <summary>budget.js's copy, word for word.</summary>
    public static class BudgetLines
    {
        private static readonly string Star = ((char)0x2736).ToString();

        /// <summary>budget.js:41 the sheet's heading and its small print.</summary>
        public const string Head = "tour budget";
        public static readonly string Rule = "parentheses " + Star + " " + (char)0x00D7 + " " + (char)0x00F7 + " " + Star + " + " + (char)0x2212;
        /// <summary>The warm-up's small print: no parentheses there, so the order starts at x and /.</summary>
        public static readonly string RuleNoParens = (char)0x00D7 + " " + (char)0x00F7 + " first, then + " + (char)0x2212;
        /// <summary>budget.js:42 a story problem's sheet before she picks.</summary>
        public const string WhichOne = "which one matches?";
        /// <summary>budget.js:45 the prompt card: the story, or this for an expression to work out.</summary>
        public const string WhatComes = "what does it come to?";
        public static string PromptLabel(bool story) { return story ? "the books" : "work it out"; }
        /// <summary>budget.js:47 the keypad's check key.</summary>
        public const string BalanceIt = "balance it";
        /// <summary>budget.js:72 the right expression picked.</summary>
        public static string Yes(string expr) { return "yes: <b>" + expr + "</b>. now work it out."; }
        /// <summary>budget.js:88 the dog's first line.</summary>
        public static string Intro(bool story)
        {
            return story ? "first, which expression tells this story?"
                : "parentheses first. then " + (char)0x00D7 + " and " + (char)0x00F7 + ". then + and " + (char)0x2212 + ".";
        }
        /// <summary>budget.js:44 the choice tilts (--r, degrees, CSS clockwise).</summary>
        public static readonly float[] Tilts = { -1f, 1.2f, -.6f, .8f };
    }

    /// <summary>One line of the budget sheet (budget.js sheet(): .eline, .eline.final, mark, .eline.q).</summary>
    public struct SheetLine
    {
        /// <summary>The text before the highlighter, the highlighted part (or ""), and the rest. A worked line after the first starts "= ".</summary>
        public string Before, Mark, After;
        public bool Final, Question;
        public string Text { get { return Before + Mark + After; } }
    }

    /// <summary>What a press on a story choice did.</summary>
    public enum BudgetPick { Ignored, Right, Wrong, WrongShown }

    /// <summary>The budget sheet's side of the song.</summary>
    public interface IBudgetView
    {
        /// <summary>Redraw the sheet from <see cref="BudgetRound.Sheet"/>.</summary>
        void Paint();
        /// <summary>slap($('#sheet')).</summary>
        void SlapSheet();
        /// <summary>Every choice off (the look kept).</summary>
        void FreezeChoices();
        /// <summary>.good, and a pop when she picked it.</summary>
        void Good(int i, bool pop);
        /// <summary>.bad .gone: struck through, off, a wobble.</summary>
        void Gone(int i);
        /// <summary>The keypad appears (#padslot).</summary>
        void ShowPad();
        /// <summary>pad.set(answer); pad.lock('shown').</summary>
        void ShowAnswerOnPad(string text);
        /// <summary>showNext(ctx).</summary>
        void ShowNext();
    }

    /// <summary>
    /// The rules of tour budget, ported from world_tour/modes/math/budget.js: levels 1-2 work an expression out on the
    /// keypad; level 3 first picks the expression that tells the story (a wrong pick is a try on the shared ladder; the
    /// partial step names the right one, so hint 2 picks it for her), then works it out. The hint ladder rewrites the
    /// sheet: the first operation highlighted with its result at step 2, every step at the worked solution. The typed part
    /// is the shared <see cref="TypedMathRound"/>; this class is its onStep and the story's choices.
    /// </summary>
    public sealed class BudgetRound
    {
        /// <summary>
        /// budget.js's highlighter: the first operation, "<b>a op b = " in the partial step. The web's pattern ran across
        /// "</b>" on story problems ("it's <b>expr</b>. first: a + b = c.") and found nothing; here a bold part may not
        /// contain a tag, and a story's "first: a + b = " is used instead (P3; intended difference 32).
        /// </summary>
        private static readonly Regex FirstOp = new Regex("<b>([^<]*?) = ", RegexOptions.CultureInvariant);
        private static readonly Regex FirstStory = new Regex("first: ([^<]*?) = ", RegexOptions.CultureInvariant);

        private readonly Problem _p;
        private readonly IMathSong _song;
        private readonly IBudgetView _view;

        public readonly bool Story;
        public MathLadder Ladder { get; set; }
        /// <summary>The highest ladder step drawn.</summary>
        public int Stage { get; private set; }
        /// <summary>The expression is chosen (always, off level 3).</summary>
        public bool Picked { get; private set; }
        public int RightIndex { get; private set; }

        public BudgetRound(Problem p, IMathSong song, IBudgetView view)
        {
            _p = p; _song = song; _view = view;
            Story = p.Kind == "story";
            Picked = !Story;
            RightIndex = Story ? p.Choices.FindIndex(c => c.Correct) : -1;
        }

        /// <summary>budget.js:51-61 the ladder's onStep.</summary>
        public void Step(int k)
        {
            Stage = Math.Max(Stage, k);
            if (!Picked && k >= 2) Choose(RightIndex, true);   // the partial step names the right expression
            _view.Paint();
            _view.SlapSheet();
        }

        /// <summary>budget.js:76-87 a press on story choice i.</summary>
        public BudgetPick Pick(int i)
        {
            if (!Story || Picked || (Ladder != null && Ladder.Over) || i < 0 || i >= _p.Choices.Count) return BudgetPick.Ignored;
            Choice c = _p.Choices[i];
            if (c.Correct) { Choose(i, false); return BudgetPick.Right; }
            _view.Gone(i);
            if (Ladder != null && Ladder.Wrong(null, c.Why))
            {
                _view.FreezeChoices();
                _view.Good(RightIndex, false);
                Picked = true; Stage = 3;
                _view.Paint();
                _view.ShowPad();
                _view.ShowAnswerOnPad(_p.Answer.Value.Number.ToString(System.Globalization.CultureInfo.InvariantCulture));
                _view.ShowNext();
                return BudgetPick.WrongShown;
            }
            return BudgetPick.Wrong;
        }

        /// <summary>budget.js:64-73.</summary>
        private void Choose(int i, bool given)
        {
            _view.FreezeChoices();
            _view.Good(i, true);
            Picked = true;
            _view.Paint();
            _view.ShowPad();
            if (!given) _song.Say(BudgetLines.Yes(_p.Params["expr"].String), "cheer");
        }

        /// <summary>budget.js:11-21 sheet(p, n), or the story's question before she has picked.</summary>
        public List<SheetLine> Sheet() { return Story && !Picked ? new List<SheetLine> { new SheetLine { Before = BudgetLines.WhichOne, Mark = "", After = "", Question = true } } : Sheet(_p, Stage); }

        public static List<SheetLine> Sheet(Problem p, int n)
        {
            List<string> st = p.Params["states"].Items.Select(x => x.String).ToList();
            var l = new List<SheetLine>();
            if (n >= 3)
            {
                for (int i = 0; i < st.Count; i++) l.Add(new SheetLine { Before = (i > 0 ? "= " : "") + st[i], Mark = "", After = "", Final = i == st.Count - 1 });
                return l;
            }
            if (n == 2 && st.Count > 1)
            {
                Match m = FirstOp.Match(p.Steps[1]);
                if (!m.Success) m = FirstStory.Match(p.Steps[1]);
                string first = m.Success ? m.Groups[1].Value : "";
                int at = first.Length > 0 ? st[0].IndexOf(first, StringComparison.Ordinal) : -1;
                l.Add(at >= 0 ? new SheetLine { Before = st[0].Substring(0, at), Mark = first, After = st[0].Substring(at + first.Length) }
                              : new SheetLine { Before = st[0], Mark = "", After = "" });
                l.Add(new SheetLine { Before = "= " + st[1], Mark = "", After = "" });
                return l;
            }
            l.Add(new SheetLine { Before = st[0], Mark = "", After = "" });
            return l;
        }
    }
}
