using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WorldTour.Game.Audio;
using WorldTour.Game.Motion;
using WorldTour.Game.Show;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// MODE: prefix mixer (world_tour/modes/spelling/prefix.js). Drag a prefix onto the root (or tap it, then tap the
    /// blank) to build the week's word, then pick what the prefix does to the meaning. Only offered for weeks where 4+ words
    /// carry a prefix (<see cref="ModeRules"/>). Rules in <see cref="PrefixRules"/> and <see cref="PrefixRound"/>.
    /// Keyboard: Tab to a chip and press Enter or Space to select it, Tab to the blank and press Enter to place it; the
    /// meanings are buttons. A wrong prefix wobbles the blank and the desk; nothing turns red.
    /// Logs "WT_PREFIX board= meanings=", "WT_PREFIX_POS chips=x:y,... drop=x:y", "WT_PREFIX_DROP", "WT_PREFIX_MEANING",
    /// "WT_CHOICES pos=" (the meanings, when they appear) and "WT_HINT mode=prefix n=".
    /// </summary>
    public sealed class PrefixMode : MonoBehaviour, ISongMode
    {
        private SongContext _ctx;
        private RectTransform _desk, _drop, _step2;
        private Image _dropFace, _rootFace;
        private TMP_Text _dropText;
        private readonly List<DragChip> _chips = new List<DragChip>();
        private ChoiceGrid _meanings;
        private ShowButton _next;

        public PrefixRound Round { get; private set; }
        public PrefixDeal Deal { get; private set; }
        public IReadOnlyList<DragChip> Chips { get { return _chips; } }
        public DropTarget Drop { get; private set; }
        public ChoiceGrid Meanings { get { return _meanings; } }

        public void Render(SongContext ctx)
        {
            _ctx = ctx;
            var word = ctx.Word;
            Deal = PrefixRules.Deal(word, ctx.Week, ctx.Random);            // prefix.js:159-162
            Round = new PrefixRound(word, Deal, new Host(this), new View(this));
            Build(ctx.Area, word);
            Debug.Log("WT_PREFIX board=" + string.Join(",", Deal.Board) + " meanings=" + string.Join("|", Deal.Meanings) + " p=" + word.prefix.p + " means=" + word.prefix.means);
            // prefix.js:187. One frame later: the dog says the show's own song line (show.js:198) when the song starts,
            // after this mode is drawn; on the web that line comes first and this one replaces it.
            ctx.Later(() => ctx.Say(Spelling3Lines.BuildIt, "idle"), .01f);
            ctx.Later(LogPositions, .45f);
            ctx.SetupHints(PrefixRules.HintMax, n =>                          // prefix.js:245-260
            {
                bool counted = Round.Hint(ctx.Random);
                Debug.Log("WT_HINT mode=prefix n=" + n + " counted=" + (counted ? "true" : "false"));
                return counted;
            });
        }

        private void LogPositions()
        {
            if (this == null || _chips.Count == 0) return;
            Canvas.ForceUpdateCanvases();
            var sb = new System.Text.StringBuilder("WT_PREFIX_POS chips=");
            for (int i = 0; i < _chips.Count; i++) { if (i > 0) sb.Append(','); sb.Append(Centre((RectTransform)_chips[i].transform)); }
            sb.Append(" drop=").Append(Centre(_drop));
            Debug.Log(sb.ToString());
        }

        private static string Centre(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            Vector3 m = (c[0] + c[2]) * .5f;
            return Mathf.RoundToInt(m.x) + ":" + Mathf.RoundToInt(UnityEngine.Screen.height - m.y);
        }

        // ---------- drawing ----------

        private void Build(RectTransform area, WorldTour.Core.Data.Word word)
        {
            SpellingUi.Column(area);
            _desk = SpellingUi.Desk(area, Spelling3Lines.MixerTag);           // prefix.js:165-176

            // the build: the blank, then the root (screens.css .build, .drop, .root)
            RectTransform build = SpellingUi.Row(_desk, "Build", 0, 64);
            _drop = SpellingUi.Box("Drop", build, 110, 64);
            _dropFace = ShowUi.Panel(_drop, ColorRole.Card, .01f);
            _dropFace.raycastTarget = true;
            SpellingUi.DashedBorder(_drop, 110, 64);
            _dropText = ShowUi.Label(_drop, "Text", Spelling3Lines.DropBlank, 30.4f, FontRole.BodyBold, ColorRole.OnInk);
            Drop = _drop.gameObject.AddComponent<DropTarget>();
            Drop.targetGraphic = _dropFace;
            Drop.transition = Selectable.Transition.None;
            Drop.Pressed = () => Round.PlaceSelected();
            RectTransform root = SpellingUi.Box("Root", build, -1, 64);
            root.GetComponent<LayoutElement>().preferredWidth = Mathf.Max(120, 22 * word.prefix.root.Length + 32);
            _rootFace = ShowUi.Panel(root, ColorRole.Card);
            TMP_Text rootText = ShowUi.Label(root, "Text", "", 30.4f, FontRole.BodyBold, ColorRole.Ink);
            rootText.text = Spelling3Lines.Esc(word.prefix.root);

            // the chips (prefix.js:172-174)
            RectTransform chips = SpellingUi.Row(_desk, "Chips", 14, 62);
            for (int i = 0; i < Deal.Board.Count; i++)
            {
                DragChip c = DragChip.Make(chips, i, Spelling3Lines.Chip(Deal.Board[i]), PrefixRules.ChipTilts[i % 4]);
                var le = c.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 96; le.preferredHeight = 58;
                c.Tapped = chip => Round.Select(chip.Index);
                c.Dropped = chip => Round.Drop(chip.Index);
                c.IsOverTarget = (p, cam) => RectTransformUtility.RectangleContainsScreenPoint(_drop, p, cam);
                _chips.Add(c);
            }
            SpellingUi.Text(_desk, "Tap", Spelling3Lines.DragOrTap, Tokens.Fs0, FontRole.Body, ColorRole.OnInk, true);

            // step 2 (prefix.js:177-180), hidden until the word is built
            _step2 = SpellingUi.Box("Step2", area, 640, -1);
            var col = _step2.gameObject.AddComponent<VerticalLayoutGroup>();
            col.spacing = 12; col.childAlignment = TextAnchor.UpperCenter;
            col.childControlWidth = true; col.childControlHeight = true; col.childForceExpandWidth = false; col.childForceExpandHeight = false;
            TMP_Text q = SpellingUi.Text(_step2, "Question", "", 17.6f, FontRole.Body, ColorRole.Ink, false);
            q.text = Spelling3Lines.Question(Spelling3Lines.Esc(word.prefix.p), Spelling3Lines.Esc(word.prefix.root));
            _meanings = new ChoiceGrid(_step2, _ctx.ButtonPrefab, Deal.Meanings.Select(m => Spelling3Lines.ItMeans(m)).ToList(), i => Round.PickMeaning(i));
            _step2.gameObject.SetActive(false);

            RectTransform controls = SpellingUi.Controls(area);
            SpellingUi.Control(_ctx.HintButton(controls));
            _next = SpellingUi.Control(ShowUi.Button(_ctx.ButtonPrefab, controls, "Next", SpellingLines.NextSong, false, () => _ctx.Next(0f)), 190);
            if (_next != null) _next.Go.SetActive(false);
        }

        private sealed class Host : IPrefixHost
        {
            private readonly PrefixMode _m;
            public Host(PrefixMode m) { _m = m; }
            public int Miss()
            {
                int t = _m._ctx.Miss();
                Feedback.Wobble(_m._desk);                                     // show.js:163 [data-wobble] (.desk.mixer)
                return t;
            }
            public bool Win() { return _m._ctx.Win(); }
            public bool Reveal() { return _m._ctx.Reveal(); }
            public void LockHints() { _m._ctx.LockHints(); }
            public void Say(string line, string pose) { _m._ctx.Say(line, pose); }
            public void Next(float seconds) { _m._ctx.Next(seconds); }
            public void Later(System.Action action, float seconds) { _m._ctx.Later(action, seconds); }
            public void Log(string line) { Debug.Log(line); }
        }

        private sealed class View : IPrefixView
        {
            private readonly PrefixMode _m;
            public View(PrefixMode m) { _m = m; }

            public void Select(int chip)
            {
                for (int i = 0; i < _m._chips.Count; i++) _m._chips[i].Selected = i == chip;
                // .drop.armed while a chip is selected
                Color c = Theme.Color(chip >= 0 ? ColorRole.Accent : ColorRole.Card);
                c.a = chip >= 0 ? .35f : .01f;
                if (!_m.Round.Built) _m._dropFace.color = c;
            }

            public void DropWrong(string p)
            {
                _m._dropText.text = "<color=#" + ColorUtility.ToHtmlStringRGB(Theme.Color(ColorRole.Muted)) + "><s>" + Spelling3Lines.Esc(p) + "</s></color>";
                _m._dropFace.color = Theme.Color(ColorRole.Card);
                Feedback.Wobble(_m._drop);
            }

            public void DropClear()
            {
                _m._dropText.text = Spelling3Lines.DropBlank;
                Color c = Theme.Color(ColorRole.Card); c.a = .01f;
                _m._dropFace.color = c;
            }

            public void ChipGone(int chip)
            {
                if (chip < 0 || chip >= _m._chips.Count) return;
                _m._chips[chip].SetGone(Spelling3Lines.Chip(_m.Deal.Board[chip]));
                Feedback.Wobble(_m._chips[chip].Body);
            }

            public void FinishWord()
            {
                _m._dropText.text = Spelling3Lines.Esc(_m.Round.Word.prefix.p);
                _m._dropText.color = Theme.Color(ColorRole.Ink);
                _m._dropFace.color = Theme.Color(ColorRole.PaperDeep);
                _m._rootFace.color = Theme.Color(ColorRole.PaperDeep);
                foreach (DragChip c in _m._chips) { c.Selected = false; c.Lock(); }
                Feedback.Slap(_m._drop);
            }

            public void ShowStep2()
            {
                _m._step2.gameObject.SetActive(true);
                // place it first: a motion started now would take the unplaced pose as its rest and hold it there
                // (the 3.2 playthrough showed the question over the header)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_m._ctx.Area);
                Feedback.Slap(_m._step2);
                _m._ctx.Later(() => { if (_m._meanings != null) _m._meanings.LogPositions("WT_CHOICES"); }, .45f);
                if (_m._meanings.Buttons.Count > 0 && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_m._meanings.Buttons[0].Go);
            }

            public void MeaningGood(int i) { _m._meanings.Good(i, true); }
            public void MeaningGone(int i) { _m._meanings.Gone(i, true); }
            public void LockMeanings() { _m._meanings.Freeze(); }

            public void ShowNext()
            {
                if (_m._next == null) return;
                _m._next.Go.SetActive(true);
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_m._next.Go);
            }

            public void Tap() { Sfx.Play(SfxId.Tap); }
        }
    }
}
