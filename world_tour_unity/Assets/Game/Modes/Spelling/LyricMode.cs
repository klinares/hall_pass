using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using WorldTour.Game.Motion;
using WorldTour.Game.Show;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// MODE: lyric writer (world_tour/modes/spelling/lyric.js). A line of lyrics with a blank; pick (level 1) or type
    /// (levels 2-3) the week's word that fits. Only words with an x sentence are used (the show's pool), and the mode is
    /// off for weeks where fewer than 4 words have one (<see cref="ModeRules"/>).
    /// Typing follows the typed answer (Enter submits); picking follows fan mail's choice buttons, with two picks.
    /// Logs "WT_LYRIC typing= opts=", "WT_CHOICES pos=", "WT_CHOICE i= word= result=" and "WT_HINT mode=lyric n=".
    /// </summary>
    public sealed class LyricMode : MonoBehaviour, ISongMode
    {
        private SongContext _ctx;
        private RectTransform _sheet;
        private TMP_Text _line;
        private string _before, _after;
        private TypedField _field;
        private ShowButton _go, _next;
        private ChoiceGrid _grid;

        public TypedRound Round { get; private set; }
        public LyricChoiceRound Choice { get; private set; }
        public List<string> Options { get; private set; }
        public TypedField Field { get { return _field; } }

        public void Render(SongContext ctx)
        {
            _ctx = ctx;
            string w = ctx.Item.W;
            string[] halves = LyricRules.Halves(ctx.Word != null ? ctx.Word.x : "");
            _before = halves[0]; _after = halves[1];
            RectTransform area = ctx.Area;
            SpellingUi.Column(area);
            Sheet(area);
            Fill(null, false);

            if (LyricRules.Typing(ctx.Level))
            {
                // lyric.js:77-95
                Debug.Log("WT_LYRIC typing=true");
                _field = TypedField.Make(area, Spelling3Lines.LyricPlaceholder, 560);
                RectTransform controls = SpellingUi.Controls(area);
                SpellingUi.Control(ctx.HintButton(controls));
                _go = SpellingUi.Control(ShowUi.Button(ctx.ButtonPrefab, controls, "Check", SpellingLines.CheckIt, false,
                    () => { if (Round != null) Round.Submit(_field.Text); }));
                var floor = new HintFloor(w, ctx.Item.S);                        // QUESTIONS 11 across hints and ladder
                Round = new TypedRound(w, new SongTypedHost(ctx, () => Feedback.Wobble(_sheet), floor), new View(this));
                _field.Submitted += raw => Round.Submit(raw);
                ctx.Later(() => { if (_field != null) _field.Focus(); }, TypedRound.FocusDelay);
                var hints = new LyricHints(w, ctx.Item.S, ctx.Clue, floor);
                ctx.SetupHints(hints.Max, n =>
                {
                    StepHint h = hints.Apply(n);
                    Debug.Log("WT_HINT mode=lyric n=" + n + " step=" + h.Step);
                    ctx.Say(h.Line, "think");
                    if (_field != null) _field.Focus();
                    return true;
                });
                return;
            }

            // lyric.js:98-126
            Options = LyricRules.Options(w, ctx.Week, ctx.Random);
            Choice = new LyricChoiceRound(w, Options);
            Debug.Log("WT_LYRIC typing=false opts=" + string.Join(",", Options));
            _grid = new ChoiceGrid(area, ctx.ButtonPrefab, Options, OnPick);
            RectTransform ctl = SpellingUi.Controls(area);
            SpellingUi.Control(ctx.HintButton(ctl));
            _next = SpellingUi.Control(ShowUi.Button(ctx.ButtonPrefab, ctl, "Next", SpellingLines.NextSong, false, () => ctx.Next(0f)), 190);
            if (_next != null) _next.Go.SetActive(false);
            ctx.Later(() => { if (_grid != null) _grid.LogPositions("WT_CHOICES"); }, .45f);
            ctx.SetupHints(LyricRules.ChoiceHints, n =>
            {
                FanmailHint h = Choice.Hint(n, ctx.Random, ctx.Clue);
                Debug.Log("WT_HINT mode=lyric n=" + n + " crossed=" + h.Crossed);
                if (!h.Counted) return false;
                if (h.Crossed >= 0) _grid.Gone(h.Crossed, true);
                ctx.Say(h.Line, "think");
                return true;
            });
        }

        private void OnPick(int i)
        {
            if (Choice == null || _ctx == null) return;
            FanmailPick r = _ctx.Open ? Choice.Pick(i) : FanmailPick.Ignored;
            Debug.Log("WT_CHOICE i=" + i + " word=" + Options[i] + " result=" + r.ToString().ToLowerInvariant());
            if (r == FanmailPick.Ignored) return;
            if (r == FanmailPick.Right)
            {
                _ctx.LockHints();
                _grid.Freeze();
                _grid.Good(i, true);
                Fill(_ctx.Item.W, true);
                _ctx.Win();
                _ctx.Next(LyricRules.NextAfterWin);
                return;
            }
            _grid.Gone(i, false);                                            // .bad .gone, disabled
            int tries = _ctx.Miss();
            Feedback.Wobble(_sheet);                                         // show.js:163 [data-wobble]
            if (tries >= LyricRules.PicksBeforeReveal)
            {
                Choice.EndAfterReveal();
                _ctx.LockHints();
                _grid.Freeze();
                _grid.Good(Choice.RightIndex, false);
                Fill(_ctx.Item.W, false);
                _ctx.Reveal();
                if (_next != null)
                {
                    _next.Go.SetActive(true);
                    if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_next.Go);
                }
            }
            else _ctx.Say(Spelling3Lines.NotThatOne(Spelling3Lines.Esc(_ctx.Clue.ToLowerInvariant())), "oops");
        }

        /// <summary>
        /// lyric.js:52-62 the torn lined sheet: "verse 2, line 1" and the line with its blank. The blank is an ink rule
        /// until filled: .blank.ok (accent 2, the word she got) or .blank.shown (paper-deep, the revealed word).
        /// </summary>
        private void Sheet(RectTransform area)
        {
            _sheet = SpellingUi.Box("Lyric", area, 640, -1);
            ShowUi.Panel(_sheet, ColorRole.Card, 1f, SpriteRole.PaperPanel);
            var col = _sheet.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            col.padding = new RectOffset(50, 20, 28, 26);
            col.spacing = 6;
            col.childControlWidth = true; col.childControlHeight = true; col.childForceExpandHeight = false;
            SpellingUi.Text(_sheet, "Label", Spelling3Lines.VerseLabel, Tokens.Fs0, FontRole.BodyBold, ColorRole.Accent2, true);
            _line = SpellingUi.Text(_sheet, "Line", "", 32f, FontRole.Display, ColorRole.Ink, false);
            _line.alignment = TextAlignmentOptions.Left;
        }

        private void Fill(string word, bool ok)
        {
            if (_line == null) return;
            string blank;
            // .blank: a fixed-width ink rule (min-width 5.5em), the same for every word. TMP draws no underline under
            // spaces, so it is a run of underscores, which Caveat Brush joins into one line.
            if (word == null) blank = "<color=#" + Hex(ColorRole.Ink) + ">" + new string('_', 8) + "</color>";
            else blank = "<color=#" + Hex(ok ? ColorRole.Accent2 : ColorRole.PaperDeep) + ">" + (ok ? "<u>" + word + "</u>" : word) + "</color>";
            // lyric.js:59 the text sits against the blank, which has a .15em margin each side
            const string Gap = "<space=0.15em>";
            _line.text = Lower.Of("<noparse>" + _before + "</noparse>") + Gap + blank + Gap + Lower.Of("<noparse>" + _after + "</noparse>");
            if (word != null) Feedback.Slap(_line.rectTransform);
        }

        private static string Hex(ColorRole role) { return ColorUtility.ToHtmlStringRGB(Theme.Color(role)); }

        private sealed class View : ITypedView
        {
            private readonly LyricMode _m;
            public View(LyricMode m) { _m = m; }
            public void Focus() { _m._field.Focus(); }
            public void SelectAll() { _m._field.SelectAll(); }
            public void Lock() { _m._field.Lock(); if (_m._go != null) _m._go.Interactable = false; }
            public void MarkRight()
            {
                _m._field.SetLook(TypedField.Look.Right);
                Feedback.Pop(_m._field.Body);
                _m.Fill(_m._ctx.Item.W, true);                              // lyric.js:84-85 fill(w, 'ok')
            }
            public void Wobble() { Feedback.Wobble(_m._field.Body); }
            public void ShowAnswer(string word)
            {
                _m._field.Text = word;
                _m._field.SetLook(TypedField.Look.Shown);
                _m.Fill(word, false);                                         // lyric.js:81 onReveal fill(w, 'shown')
            }
        }
    }
}
