using UnityEngine;
using UnityEngine.UI;
using WorldTour.Game.Motion;
using WorldTour.Game.Show;
using WorldTour.Game.Speech;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// MODE: studio session (world_tour/modes/spelling/studio.js). Hear the word, spell it (closest thing to test day).
    /// Hints: the clue, the syllables read slowly, first + last letters, then consonants only. Without speech the clue is
    /// on screen from the start and the clue hint is skipped. Speech is <see cref="ReadAloud"/>; the song waits up to
    /// <see cref="SpeechRules.VoicesTimeout"/> for the browser's voices, then falls back to the clue.
    /// Keyboard: the field has focus; Enter submits. The record button replays the word (click, or Tab and Enter).
    /// Logs "WT_STUDIO voice=speak|clue waited=s" and "WT_HINT mode=studio n= step="; ReadAloud logs WT_SPEAK.
    /// </summary>
    public sealed class StudioMode : MonoBehaviour, ISongMode
    {
        private SongContext _ctx;
        private float _start;
        private TypedField _field;
        private ShowButton _submit;
        private RectTransform _record;

        public TypedRound Round { get; private set; }
        public StudioHints Hints { get; private set; }
        public bool CanSpeak { get; private set; }
        public TypedField Field { get { return _field; } }

        public void Render(SongContext ctx)
        {
            _ctx = ctx;
            _start = Time.unscaledTime;
            Poll();
        }

        /// <summary>Waits for the browser's voices (they load late), then draws the song.</summary>
        private void Poll()
        {
            if (this == null || _ctx == null || !_ctx.Live) return;
            float waited = Time.unscaledTime - _start;
            SpeechChoice c = ReadAloud.Decide(waited);
            if (c == SpeechChoice.Wait) { _ctx.Later(Poll, SpeechRules.PollEvery); return; }
            Debug.Log("WT_STUDIO voice=" + (c == SpeechChoice.Speak ? "speak" : "clue") + " waited=" + waited.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            Build(c == SpeechChoice.Speak);
        }

        private void Build(bool can)
        {
            SongContext ctx = _ctx;
            CanSpeak = can;
            string w = ctx.Item.W;
            RectTransform area = ctx.Area;
            SpellingUi.Column(area);
            if (!can) SpellingUi.Clue(area, ctx.Clue);                          // studio.js:18
            RectTransform desk = SpellingUi.Desk(area, Spelling3Lines.StudioDeskTag);
            if (can)
            {
                _record = RecordButton(desk, ctx);
                SpellingUi.Text(desk, "Tap", Spelling3Lines.TapToHear, Tokens.Fs0, FontRole.Body, ColorRole.OnInk, true);
            }
            _field = TypedField.Make(desk, Spelling3Lines.StudioPlaceholder, 560);
            RectTransform controls = SpellingUi.Controls(area);
            SpellingUi.Control(ctx.HintButton(controls));
            _submit = SpellingUi.Control(ShowUi.Button(ctx.ButtonPrefab, controls, "Submit", Spelling3Lines.Submit, false,
                () => { if (Round != null) Round.Submit(_field.Text); }));

            if (can) ctx.Later(PlayWord, SpeechRules.FirstPlayDelay);          // studio.js:27
            var floor = new HintFloor(w, ctx.Item.S);                            // QUESTIONS 11 across hints and ladder
            Round = new TypedRound(w, new SongTypedHost(ctx, () => Feedback.Wobble(ctx.Area), floor), new View(this));
            _field.Submitted += raw => Round.Submit(raw);
            ctx.Later(() => { if (_field != null) _field.Focus(); }, TypedRound.FocusDelay);

            Hints = new StudioHints(w, ctx.Item.S, ctx.Clue, can, floor);
            ctx.SetupHints(Hints.Max, n =>                                      // studio.js:31-39
            {
                StepHint h = Hints.Apply(n);
                Debug.Log("WT_HINT mode=studio n=" + n + " step=" + h.Step);
                ctx.Say(h.Line, "think");
                if (h.SpeakSlow) ReadAloud.Say(w, true);
                if (_field != null) _field.Focus();
                return true;
            });
        }

        /// <summary>studio.js:26 the record spins and the word plays.</summary>
        private void PlayWord()
        {
            if (_ctx == null || !_ctx.Live) return;
            if (_record != null)
            {
                RectTransform r = _record;
                Tweener.Float(0f, 1f, 1.1f, Ease.CssEase, v => { if (r != null) r.localEulerAngles = new Vector3(0, 0, -360f * v); },
                    () => { if (r != null) r.localEulerAngles = Vector3.zero; }, r);
            }
            ReadAloud.Say(_ctx.Item.W);
        }

        /// <summary>studio.js:20 the vinyl sticker as a button (screens.css .record, 170 px).</summary>
        private RectTransform RecordButton(RectTransform desk, SongContext ctx)
        {
            RectTransform box = SpellingUi.Box("Record", desk, 170, 170);
            var hit = box.gameObject.AddComponent<Image>();
            hit.color = new Color(1, 1, 1, 0);
            RectTransform art = ShowUi.Rect("Art", box);
            ShowUi.Stretch(art);
            var img = art.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            if (ctx.RecordSticker != null) img.sprite = ctx.RecordSticker;
            else
            {
                img.sprite = UiShapes.Circle;
                img.color = Theme.Color(ColorRole.Ink);
                ShowUi.Label(art, "Play", ((char)0x25B6).ToString(), 48f, FontRole.Body, ColorRole.OnInk);
            }
            var b = box.gameObject.AddComponent<Button>();
            b.targetGraphic = hit;
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(PlayWord);
            return art;
        }

        private sealed class View : ITypedView
        {
            private readonly StudioMode _m;
            public View(StudioMode m) { _m = m; }
            public void Focus() { _m._field.Focus(); }
            public void SelectAll() { _m._field.SelectAll(); }
            public void Lock() { _m._field.Lock(); if (_m._submit != null) _m._submit.Interactable = false; }
            public void MarkRight() { _m._field.SetLook(TypedField.Look.Right); Feedback.Pop(_m._field.Body); }
            public void Wobble() { Feedback.Wobble(_m._field.Body); }
            public void ShowAnswer(string word) { _m._field.Text = word; _m._field.SetLook(TypedField.Look.Shown); }
        }
    }
}
