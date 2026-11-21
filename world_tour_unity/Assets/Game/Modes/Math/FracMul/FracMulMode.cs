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
    /// MODE: snack table (skill fm; Unity only): multiplying and dividing fractions in the green room. The prompt card and
    /// the stage desk follow the tour budget's layout: the desk holds the expression and a paper with the diagram (an area
    /// diagram for multiplication, a tape diagram for division) that grows with the hint ladder; most problems are typed
    /// on the shared keypad (fraction key, "and" for a mixed number, any equivalent form is right), and the two picking
    /// kinds (which expression matches the shaded part; less, equal or greater) are choices under the desk. The picture at
    /// hints 1 and 2 never shades the product or counts it out (<see cref="FracMulPicture"/>).
    /// Rules: <see cref="TypedMathRound"/>, <see cref="FmChoiceRound"/>.
    /// Logs "WT_MATH mode=snack kind= level= seed= answer=", "WT_MATH_SUBMIT val= result= tries=", "WT_FM_PICK i= result=
    /// tries=", "WT_FM_PICTURE stage= kind=", "WT_HINT mode=snack n=" and "WT_CHOICES pos=".
    /// </summary>
    public sealed class FracMulMode : MonoBehaviour, ISongMode, IFmChoiceView
    {
        public const float ChoicesWidth = 560f;

        private SongContext _ctx;
        private RectTransform _desk, _paper;
        private ChoiceGrid _grid;
        private ShowButton _next;

        public Problem Problem { get; private set; }
        public TypedMathRound Round { get; private set; }
        public FmChoiceRound Picks { get; private set; }
        public MathKeypad Pad { get; private set; }
        public MathLadder Ladder { get { return Round != null ? Round.Ladder : Picks.Ladder; } }
        public int Stage { get; private set; }

        public void Render(SongContext ctx)
        {
            _ctx = ctx;
            Problem p = Problem = ctx.Problem;
            var song = new MathSongHost(ctx, "snack", () => _desk);
            MathUi.Columns(ctx.Area, out RectTransform left, out RectTransform right);
            BuildDesk(left, p);
            if (FracMulLines.Picks(p))
            {
                _grid = new ChoiceGrid(left, ctx.ButtonPrefab, p.Choices.Select(c => FracMulLines.ChoiceLabel(p, c)).ToList(), Pick, ChoicesWidth);
                foreach (ShowButton b in _grid.Buttons)
                    foreach (TMP_Text t in b.Go.GetComponentsInChildren<TMP_Text>(true)) { Theme.ApplyFont(t, FontRole.BodyBold); t.fontSize = 19f; }
            }
            // clear air above the controls (the choices' shadows ran into the hint button) and between hint and next song
            if (_grid != null) SpellingUi.Box("Gap", left, 0, 14f);
            _next = MathUi.Controls(ctx, left, out RectTransform controls);
            controls.GetComponent<HorizontalLayoutGroup>().spacing = 36f;
            MathUi.PromptCard(right, p.Prompt, FracMulLines.PromptLabel(p.Kind), 150f);   // a 5-line prompt would squeeze the keypad
            if (FracMulLines.Picks(p))
                Picks = new FmChoiceRound(p, song, this, s => ctx.Next(s), OnStep);
            else
            {
                Pad = MathKeypad.Make(right, p.Answer.Type, FracMulLines.ServeIt, () => MathUi.ShowNext(_ctx, _next));
                Round = new TypedMathRound(song, p, Pad, s => ctx.Next(s), OnStep);
                Pad.Submitted += text =>
                {
                    TypedMathResult r = Round.Submit(text);
                    Debug.Log("WT_MATH_SUBMIT val=" + text + " result=" + r.ToString().ToLowerInvariant() + " tries=" + _ctx.Tries);
                };
            }
            Debug.Log("WT_MATH mode=snack kind=" + p.Kind + " level=" + p.Level + " seed=" + p.Seed + " answer=" + TypedMathRound.KeyedAnswer(p.Answer)
                + " right=" + (p.Choices != null ? p.Choices.FindIndex(c => c.Correct) : -1));
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (_grid != null)
                ctx.Later(() => { if (_grid != null && EventSystem.current != null && !Picks.Ladder.Over) EventSystem.current.SetSelectedGameObject(_grid.Buttons[0].Go); }, TypedRound.FocusDelay);
            ctx.Later(() => _ctx.Say(FracMulLines.Intro(p.Kind), "idle"), .01f);
            ctx.Later(() => { if (_grid != null) _grid.LogPositions("WT_CHOICES"); if (Pad != null) Pad.LogPositions(); }, .45f);
        }

        private void BuildDesk(RectTransform left, Problem p)
        {
            bool picture = FracMulLines.HasPicture(p);
            _desk = MathUi.Desk(left, FracMulLines.DeskTag);
            if (!FracMulLines.PictureIsTheQuestion(p))
                MathUi.RichRow(_desk, "Expression", p.Label, 31f, FontRole.BodyBold, ColorRole.OnInk, 64f);
            if (picture)
            {
                _paper = SpellingUi.Box("Paper", _desk, FracMulDraw.PaperWidth, FracMulDraw.PaperHeight);
                ShowUi.Panel(_paper, ColorRole.Card, 1f, SpriteRole.PaperPanel);
                var le = _paper.GetComponent<LayoutElement>();
                le.minWidth = FracMulDraw.PaperWidth; le.minHeight = FracMulDraw.PaperHeight;
                Paint(0);
            }
        }

        private void Paint(int stage)
        {
            if (_paper == null) return;
            Stage = stage;
            FracMulDraw.Draw(_paper, FracMulPicture.For(Problem, stage));
        }

        /// <summary>The ladder's onStep: the picture moves on with the hint.</summary>
        private void OnStep(int n)
        {
            if (n <= Stage) return;
            Debug.Log("WT_FM_PICTURE stage=" + n + " kind=" + Problem.Kind);
            if (_paper == null) { Stage = n; return; }
            Paint(n);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_ctx.Area);
            Feedback.Slap(_paper);
        }

        private void Pick(int i)
        {
            FmPick r = Picks.Pick(i);
            if (r != FmPick.Ignored) Debug.Log("WT_FM_PICK i=" + i + " result=" + r.ToString().ToLowerInvariant() + " tries=" + _ctx.Tries);
        }

        // ---------- IFmChoiceView ----------

        public void Gone(int i) { if (_grid != null) _grid.Gone(i, true); }
        public void FreezeChoices() { if (_grid != null) _grid.Freeze(); }
        public void Good(int i, bool pop) { if (_grid != null) _grid.Good(i, pop); }
        public void ShowNext() { MathUi.ShowNext(_ctx, _next); }
    }
}
