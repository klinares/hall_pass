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
    /// MODE: soundcheck (world_tour/modes/math/soundcheck.js): quick facts and powers of ten, four choices. The meter is
    /// the only timer in the game and only decides bonus fans: right before it runs out is +300; running out costs
    /// nothing and says "no bonus this time. no rush." It shows exactly what the web shows (the bar, the seconds, the
    /// line), with no extra urgency; with reduce motion on, the bar steps once a second instead of sliding.
    /// Rules: <see cref="SoundcheckRound"/>, <see cref="SoundcheckMeter"/>.
    /// Keyboard: the first choice has focus; arrows and Tab move, Enter or Space picks.
    /// Logs "WT_MATH mode=soundcheck kind= level= seed= answer=", "WT_SOUNDCHECK_PICK i= result= bonus= tries=",
    /// "WT_SOUNDCHECK_OUT", "WT_HINT mode=soundcheck n=" and "WT_CHOICES pos=".
    /// </summary>
    public sealed class SoundcheckMode : MonoBehaviour, ISongMode, ISoundcheckView
    {
        private SongContext _ctx;
        private RectTransform _desk, _fill;
        private TMP_Text _label;
        private ChoiceGrid _grid;
        private ShowButton _next;
        private float _t0;
        private bool _outShown;

        public Problem Problem { get; private set; }
        public SoundcheckRound Round { get; private set; }

        public void Render(SongContext ctx)
        {
            _ctx = ctx;
            Problem p = Problem = ctx.Problem;
            var song = new BonusSong(new MathSongHost(ctx, "soundcheck", () => _desk), extra => ctx.Win(extra));
            Round = new SoundcheckRound(p, song, this, ctx.Random, s => ctx.Next(s));

            MathUi.Columns(ctx.Area, out RectTransform left, out RectTransform right);
            _desk = MathUi.Desk(left, SoundcheckLines.DeskTag);
            // .meter: a rounded card-bordered track; the fill runs paper-deep to lilac to card
            RectTransform track = SpellingUi.Box("Meter", _desk, -1, 22);
            UiKit.Stretch(UiKit.Box(track, "Edge", ColorRole.Card).rectTransform);
            Image back = UiKit.Box(track, "Back", ColorRole.Ink);
            UiKit.Stretch(back.rectTransform, 3f);
            back.color = MathUi.Mix(ColorRole.Card, .1f, ColorRole.Ink);
            _fill = ShowUi.Rect("Fill", back.rectTransform);
            _fill.anchorMin = Vector2.zero; _fill.anchorMax = Vector2.one; _fill.offsetMin = _fill.offsetMax = Vector2.zero;
            Gradient(_fill);
            RectTransform lb = SpellingUi.Box("MeterLabel", _desk, -1, 24);
            _label = ShowUi.Label(lb, "T", "", 13.6f, FontRole.Body, ColorRole.OnInk);
            RectTransform fact = SpellingUi.Box("Fact", _desk, -1, 96);
            TMP_Text ft = ShowUi.Label(fact, "T", "", 51.2f, FontRole.BodyBold, ColorRole.OnInk);
            ft.textWrappingMode = TextWrappingModes.NoWrap;
            ft.text = MathText.ToTmp(p.Prompt);
            _next = MathUi.Controls(ctx, left, out _);

            _grid = new ChoiceGrid(right, ctx.ButtonPrefab, p.Choices.Select(c => MathText.ToTmp(SoundcheckLines.Label(p, c))).ToList(), Pick, MathUi.RightWidth);
            foreach (ShowButton b in _grid.Buttons)
                foreach (TMP_Text t in b.Go.GetComponentsInChildren<TMP_Text>(true)) { Theme.ApplyFont(t, FontRole.BodyBold); t.fontSize = 28f; }
            for (int i = 0; i < _grid.Buttons.Count; i++) _grid.Buttons[i].Rect.localEulerAngles = new Vector3(0, 0, -SoundcheckLines.Tilts[i % 4]);

            _t0 = Time.unscaledTime;
            PaintMeter();
            Debug.Log("WT_MATH mode=soundcheck kind=" + p.Kind + " level=" + p.Level + " seed=" + p.Seed + " answer=" + Answers.Show(p.Answer) + " right=" + (p.Choices != null ? p.Choices.FindIndex(c => c.Correct) : -1));
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            ctx.Later(() => { if (_grid != null && EventSystem.current != null && !Round.Ladder.Over) EventSystem.current.SetSelectedGameObject(_grid.Buttons[0].Go); }, TypedRound.FocusDelay);
            ctx.Later(() => _ctx.Say(SoundcheckLines.Intro, "idle"), .01f);
            ctx.Later(() => { if (_grid != null) _grid.LogPositions("WT_CHOICES"); }, .45f);
        }

        /// <summary>A three-stop horizontal gradient (paper-deep, accent at 60%, card) as vertex colours on a quad strip.</summary>
        private static void Gradient(RectTransform rt)
        {
            var g = rt.gameObject.AddComponent<MeterFill>();
            g.Set(Theme.Color(ColorRole.PaperDeep), Theme.Color(ColorRole.Accent), Theme.Color(ColorRole.Card));
        }

        private void Update()
        {
            if (Round == null || !Round.Meter.Running || _ctx == null) return;
            if (!_ctx.Live) { Round.Meter.Stop(); return; }                    // the song is over (soundcheck.js:38)
            if (Round.Meter.Tick(Time.unscaledTime - _t0)) Debug.Log("WT_SOUNDCHECK_OUT");
            PaintMeter();
        }

        private void PaintMeter()
        {
            SoundcheckMeter m = Round.Meter;
            float fill = MotionPrefs.Reduced ? (float)(m.Shown / m.Seconds) : (float)m.Fill;
            _fill.anchorMax = new Vector2(Mathf.Clamp01(fill), 1f);
            if (!m.Beatable)
            {
                if (_outShown) return;
                _outShown = true;
                _label.fontStyle = FontStyles.Italic;                            // .meterlbl.out: italic, 80%
                Color c = _label.color; c.a = .8f; _label.color = c;
                _label.text = SoundcheckLines.Out;
                return;
            }
            _label.text = "<size=130%><b>" + m.Shown + "</b></size>" + SoundcheckLines.SecFor;
        }

        private void Pick(int i)
        {
            int before = Round.Meter.BonusNow;
            SoundcheckPick r = Round.Pick(i);
            if (r != SoundcheckPick.Ignored)
                Debug.Log("WT_SOUNDCHECK_PICK i=" + i + " result=" + r.ToString().ToLowerInvariant() + " bonus=" + (r == SoundcheckPick.Right ? before : 0) + " tries=" + _ctx.Tries);
        }

        // ---------- ISoundcheckView ----------

        public void FreezeChoices() { _grid.Freeze(); }
        public void Good(int i, bool pop) { _grid.Good(i, pop); }
        public void Gone(int i) { Debug.Log("WT_CHOICE_GONE i=" + i); _grid.Gone(i, true); }
        public void Ticked() { Sfx.Play(SfxId.Tick); }
        public void ShowNext() { MathUi.ShowNext(_ctx, _next); }
    }

    /// <summary>math.css .meter i: linear-gradient(90deg, paper-deep, accent 60%, card), drawn as two quads with vertex colours.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MeterFill : MaskableGraphic
    {
        private Color _a, _b, _c;

        protected override void Awake() { base.Awake(); raycastTarget = false; }

        public void Set(Color a, Color b, Color c) { _a = a; _b = b; _c = c; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            // the gradient spans the whole track, so it's cut by the fill rather than squeezed: map by the parent's width
            var parent = rectTransform.parent as RectTransform;
            float full = parent != null ? parent.rect.width : r.width;
            float x0 = r.xMin, x1 = r.xMin + full * .6f, x2 = r.xMin + full;
            Quad(vh, x0, Mathf.Min(x1, r.xMax), r, _a, Color.Lerp(_a, _b, Mathf.Clamp01((Mathf.Min(x1, r.xMax) - x0) / (x1 - x0))));
            if (r.xMax > x1) Quad(vh, x1, r.xMax, r, _b, Color.Lerp(_b, _c, Mathf.Clamp01((r.xMax - x1) / (x2 - x1))));
        }

        private static void Quad(VertexHelper vh, float xa, float xb, Rect r, Color ca, Color cb)
        {
            if (xb <= xa) return;
            int s = vh.currentVertCount;
            vh.AddVert(new Vector2(xa, r.yMin), ca, Vector4.zero); vh.AddVert(new Vector2(xa, r.yMax), ca, Vector4.zero);
            vh.AddVert(new Vector2(xb, r.yMax), cb, Vector4.zero); vh.AddVert(new Vector2(xb, r.yMin), cb, Vector4.zero);
            vh.AddTriangle(s, s + 1, s + 2); vh.AddTriangle(s, s + 2, s + 3);
        }

        protected override void OnRectTransformDimensionsChange() { base.OnRectTransformDimensionsChange(); SetVerticesDirty(); }
    }
}
