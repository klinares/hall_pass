using System.Collections.Generic;
using UnityEngine;
using WorldTour.Game.Motion;
using WorldTour.Game.Show;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// What missing letters (slots.js) and missing vowels (vowels.js) share: the clue, the desk with the word's row and
    /// some letters hidden, the typed answer (parts.js wireTyped, rules in <see cref="TypedRound"/>), "check it", and
    /// the letter hints (parts.js letterHints, rules in <see cref="LetterHints"/>, with the QUESTIONS 11 floor). The
    /// "sounds like" hint and the ladder's second line mask the blanks that are hers (<see cref="LetterHints.Masked"/>).
    /// The two modes differ only in which letters hide, the desk tag and the blank.
    /// Keyboard: the field has focus from the start (parts.js:66); type and press Enter. The hint and check buttons are
    /// reachable with Tab. A wrong try wobbles the row and the field and thuds; nothing turns red.
    /// Logs "WT_SLOTS mode= hide= row= hints=" once per song and "WT_HINT mode= n= index=" per hint, plus TypedRound's
    /// "WT_TYPED"; ShowScreen logs WT_SONG and WT_SONG_RESULT.
    /// </summary>
    public abstract class MissingLettersMode : MonoBehaviour, ISongMode
    {
        private SongContext _ctx;
        private SpellingUi.SlotRow _row;
        private TypedField _field;
        private ShowButton _go;

        /// <summary>The mode id, for the log.</summary>
        protected abstract string ModeId { get; }
        protected abstract string DeskTag { get; }
        protected abstract char Blank { get; }
        /// <summary>The hidden indexes, in order. May use ctx.Random (slots.js:111).</summary>
        protected abstract List<int> Hide(SongContext ctx);

        public TypedRound Round { get; private set; }
        public LetterHints Hints { get; private set; }
        public TypedField Field { get { return _field; } }

        public void Render(SongContext ctx)
        {
            _ctx = ctx;
            string w = ctx.Item.W;
            List<int> hide = Hide(ctx);
            Hints = new LetterHints(w, ctx.Item.S, hide);

            RectTransform area = ctx.Area;
            SpellingUi.Column(area);
            SpellingUi.Clue(area, ctx.Clue);
            RectTransform desk = SpellingUi.Desk(area, DeskTag);
            _row = new SpellingUi.SlotRow(desk, w, hide, Blank);
            _field = TypedField.Make(desk, SpellingLines.TypePlaceholder, 560);
            RectTransform controls = SpellingUi.Controls(area);
            SpellingUi.Control(ctx.HintButton(controls));
            _go = SpellingUi.Control(ShowUi.Button(ctx.ButtonPrefab, controls, "Check", SpellingLines.CheckIt, false, Check));

            Round = new TypedRound(w, new Host(ctx, Hints), new View(this));
            _field.Submitted += raw => Round.Submit(raw);
            Debug.Log("WT_SLOTS mode=" + ModeId + " hide=" + string.Join(",", hide) + " row=" + MissingLetters.Row(w, hide, Blank) + " hints=" + Hints.Max);

            ctx.Later(() => { if (_field != null) _field.Focus(); }, TypedRound.FocusDelay);   // parts.js:66
            // parts.js:72-83
            ctx.SetupHints(Hints.Max, n =>
            {
                LetterHint h = Hints.Apply(n);
                Debug.Log("WT_HINT mode=" + ModeId + " n=" + n + " index=" + h.Index);
                if (h.Index >= 0) _row.Give(h.Index);
                ctx.Say(h.Line, "think");
                if (h.Index >= 0 && _field != null) _field.Focus();          // parts.js:81 ctx.focus
                return true;
            });
        }

        private void Check()
        {
            if (Round != null && _field != null) Round.Submit(_field.Text);  // parts.js:64 go.onclick
        }

        private sealed class Host : ITypedHost
        {
            private readonly SongContext _c;
            private readonly LetterHints _h;
            public Host(SongContext c, LetterHints h) { _c = c; _h = h; }
            public bool Open { get { return _c.Open; } }
            public int Tries { get { return _c.Tries; } }
            public int Miss() { return _c.Miss(); }
            public bool Win() { return _c.Win(); }
            public bool Reveal() { return _c.Reveal(); }
            public void Say(string line, string pose) { _c.Say(line, pose); }
            public void Next(float seconds) { _c.Next(seconds); }
            // the second line's chunks keep the masked blanks hidden (QUESTIONS 11, lead's 3.1 ruling)
            public string Ladder(int tries) { return MaskedChunks.Ladder(_c.Item.W, _c.Item.S, tries, _c.Random, _h.Masked); }
            public void Log(string line) { Debug.Log(line); }
            public void Error(System.Exception e) { Debug.LogException(e); }
        }

        private sealed class View : ITypedView
        {
            private readonly MissingLettersMode _m;
            public View(MissingLettersMode m) { _m = m; }

            public void Focus() { _m._field.Focus(); }
            public void SelectAll() { _m._field.SelectAll(); }

            public void Lock()
            {
                _m._field.Lock();
                if (_m._go != null) _m._go.Interactable = false;
            }

            public void MarkRight()
            {
                _m._field.SetLook(TypedField.Look.Right);
                Feedback.Pop(_m._field.Body);
            }

            public void Wobble()
            {
                Feedback.Wobble(_m._row.Rect);                                // show.js:163 [data-wobble]
                Feedback.Wobble(_m._field.Body);                               // parts.js:52
            }

            public void ShowAnswer(string word)
            {
                _m._field.Text = word;
                _m._field.SetLook(TypedField.Look.Shown);
                _m._row.FillAll();                                              // slots.js fillAll (onReveal)
            }
        }
    }
}
