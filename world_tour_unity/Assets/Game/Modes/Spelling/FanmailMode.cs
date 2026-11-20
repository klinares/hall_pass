using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WorldTour.Game.Motion;
using WorldTour.Game.Show;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// MODE: fan mail (world_tour/modes/spelling/fanmail.js). A fan DMs a definition; pick the word that means it. One
    /// pick per song, like the original: a wrong pick shows the right one and waits for "next song". Hints: cross out a
    /// wrong answer (twice), then the first letter and length. Rules in <see cref="FanmailRules"/> and <see cref="FanmailRound"/>.
    /// Keyboard: every choice is a button (Tab, then Enter or Space); after a wrong pick "next song" takes focus.
    /// A wrong pick wobbles the message and thuds; the wrong choice is struck through in muted ink, never red.
    /// Logs "WT_FANMAIL opts= right= handle=", "WT_CHOICES pos=x:y,..." (button centres in page pixels, for mouse-only
    /// browser tests), "WT_CHOICE i= word= result=" and "WT_HINT mode=fanmail n= crossed=".
    /// </summary>
    public sealed class FanmailMode : MonoBehaviour, ISongMode
    {
        private enum ChoiceLook { Normal, Good, Bad, Gone }

        private SongContext _ctx;
        private FanmailDeal _deal;
        private RectTransform _dm, _grid;
        private ShowButton _next;
        private readonly List<ShowButton> _choices = new List<ShowButton>();

        public FanmailRound Round { get; private set; }

        public void Render(SongContext ctx)
        {
            _ctx = ctx;
            string w = ctx.Item.W;
            IEnumerable<string> week = ctx.Week != null && ctx.Week.words != null
                ? ctx.Week.words.Where(x => x != null && !string.IsNullOrEmpty(x.w)).Select(x => x.w)
                : new[] { w };
            _deal = FanmailRules.Deal(w, week, ctx.Random);              // fanmail.js:162, :165
            Round = new FanmailRound(w, _deal.Options);
            Build(ctx.Area, ctx.Clue);
            Debug.Log("WT_FANMAIL opts=" + string.Join(",", _deal.Options) + " right=" + Round.RightIndex + " handle=" + _deal.Handle);
            ctx.Later(LogChoices, .45f);

            // fanmail.js:183-188
            ctx.SetupHints(FanmailRules.HintMax, n =>
            {
                FanmailHint h = Round.Hint(n, ctx.Random);
                Debug.Log("WT_HINT mode=fanmail n=" + n + " crossed=" + h.Crossed);
                if (!h.Counted) return false;                            // nothing left to cross out: free
                if (h.Crossed >= 0)
                {
                    Style(h.Crossed, ChoiceLook.Gone);
                    Feedback.Wobble(_choices[h.Crossed].Rect);           // parts.js:93
                }
                ctx.Say(h.Line, "think");
                return true;
            });
        }

        /// <summary>fanmail.js:171-182.</summary>
        private void OnPick(int i)
        {
            if (Round == null || _ctx == null) return;
            FanmailPick r = _ctx.Open ? Round.Pick(i) : FanmailPick.Ignored;
            Debug.Log("WT_CHOICE i=" + i + " word=" + _deal.Options[i] + " result=" + r.ToString().ToLowerInvariant());
            if (r == FanmailPick.Ignored) return;
            _ctx.LockHints();
            var group = _grid.GetComponent<CanvasGroup>();               // btns.forEach(x => x.disabled = true)
            if (group != null) group.blocksRaycasts = false;
            if (r == FanmailPick.Right)
            {
                Style(i, ChoiceLook.Good);
                Feedback.Pop(_choices[i].Rect);
                _ctx.Win();
                _ctx.Next(FanmailRules.NextAfterWin);
                return;
            }
            Style(i, ChoiceLook.Bad);
            _ctx.Miss();
            Feedback.Wobble(_dm);                                         // show.js:163 [data-wobble]
            int right = Round.RightIndex;
            if (right >= 0)
            {
                Style(right, ChoiceLook.Good);
                Feedback.Pop(_choices[right].Rect);
            }
            _ctx.Reveal();
            _ctx.Say(SpellingLines.ItWas(Round.Word), "oops");
            if (_next != null)
            {
                _next.Go.SetActive(true);
                EventSystem es = EventSystem.current;
                if (es != null) es.SetSelectedGameObject(_next.Go);
            }
        }

        private void NextSong()
        {
            if (_ctx != null) _ctx.Next(0f);                              // fanmail.js:181
        }

        // ---------- drawing ----------

        /// <summary>screens.css .choice.good (ink), .bad (card, muted, struck through), .gone (faded, struck through).</summary>
        private void Style(int i, ChoiceLook look)
        {
            if (i < 0 || i >= _choices.Count) return;
            ShowButton b = _choices[i];
            string word = _deal.Options[i];
            switch (look)
            {
                case ChoiceLook.Good:
                    if (b.Sticker != null) b.Sticker.ButtonStyle = StickerButton.Style.Ink;
                    else PaintPlain(b, ColorRole.Ink, ColorRole.OnInk);
                    b.SetText(word);
                    break;
                case ChoiceLook.Bad:
                    b.SetText("<color=#" + ColorUtility.ToHtmlStringRGB(Theme.Color(ColorRole.Muted)) + "><s>" + word + "</s></color>");
                    break;
                case ChoiceLook.Gone:
                    b.SetText("<s>" + word + "</s>");
                    b.Interactable = false;                               // the disabled look fades it (.gone opacity .4)
                    break;
                default:
                    b.SetText(word);
                    break;
            }
        }

        private static void PaintPlain(ShowButton b, ColorRole face, ColorRole text)
        {
            var img = b.Go.GetComponent<Image>();
            if (img != null) img.color = Theme.Color(face);
            var t = b.Go.GetComponentInChildren<TMP_Text>();
            if (t != null) t.color = Theme.Color(text);
        }

        private void Build(RectTransform area, string clue)
        {
            SpellingUi.Column(area);

            // the DM (fanmail.js:164-167, screens.css .dm)
            _dm = SpellingUi.Box("Dm", area, 600, -1);
            _dm.localEulerAngles = new Vector3(0, 0, .6f);                 // rotate(-.6deg)
            ShowUi.Panel(_dm, ColorRole.Ink);                              // the ink border
            var dmCol = _dm.gameObject.AddComponent<VerticalLayoutGroup>();
            dmCol.padding = new RectOffset(3, 3, 3, 3);
            dmCol.childControlWidth = true; dmCol.childControlHeight = true;
            dmCol.childForceExpandWidth = true; dmCol.childForceExpandHeight = false;

            RectTransform head = SpellingUi.Box("Head", _dm, -1, 56);
            ShowUi.Panel(head, ColorRole.Ink);
            var headRow = head.gameObject.AddComponent<HorizontalLayoutGroup>();
            headRow.padding = new RectOffset(14, 14, 10, 10);
            headRow.spacing = 10;
            headRow.childAlignment = TextAnchor.MiddleLeft;
            headRow.childControlWidth = true; headRow.childControlHeight = true;
            headRow.childForceExpandWidth = false; headRow.childForceExpandHeight = false;

            RectTransform av = SpellingUi.Box("Avatar", head, 36, 36);
            var avRing = av.gameObject.AddComponent<Image>();
            avRing.sprite = UiShapes.Circle; avRing.color = Theme.Color(ColorRole.Card); avRing.raycastTarget = false;
            RectTransform avFace = ShowUi.Rect("Face", av);
            ShowUi.Stretch(avFace, 2);
            var avImg = avFace.gameObject.AddComponent<Image>();
            avImg.sprite = UiShapes.Circle; avImg.color = Theme.Color(ColorRole.PaperDeep); avImg.raycastTarget = false;
            ShowUi.Label(avFace, "Heart", SpellingLines.Avatar, 17.6f, FontRole.Body, ColorRole.Ink);

            RectTransform who = SpellingUi.Box("Who", head, 400, 36);
            var whoCol = who.gameObject.AddComponent<VerticalLayoutGroup>();
            whoCol.childControlWidth = true; whoCol.childControlHeight = true; whoCol.childForceExpandHeight = false;
            whoCol.childAlignment = TextAnchor.MiddleLeft;
            TMP_Text handle = SpellingUi.Text(who, "Handle", "", 14.4f, FontRole.BodyBold, ColorRole.OnInk, false);
            handle.text = "<noparse>" + _deal.Handle + "</noparse>";        // .dm-head b: text-transform none
            handle.alignment = TextAlignmentOptions.Left;
            TMP_Text sent = SpellingUi.Text(who, "Sent", SpellingLines.SentYouAMessage, 11.5f, FontRole.Body, ColorRole.OnInk, true);
            sent.alignment = TextAlignmentOptions.Left;
            Color sc = sent.color; sc.a = .85f; sent.color = sc;

            RectTransform card = SpellingUi.Box("Card", _dm, -1, -1);
            ShowUi.Panel(card, ColorRole.Card);
            var cardCol = card.gameObject.AddComponent<VerticalLayoutGroup>();
            cardCol.padding = new RectOffset(14, 14, 14, 14);
            cardCol.childControlWidth = true; cardCol.childControlHeight = true;
            cardCol.childForceExpandWidth = false; cardCol.childForceExpandHeight = false;
            cardCol.childAlignment = TextAnchor.UpperLeft;

            // .dm-msg: accent 35% over card, a 2px ink border
            RectTransform msgRing = SpellingUi.Box("Message", card, 520, -1);
            ShowUi.Panel(msgRing, ColorRole.Ink);
            var ringCol = msgRing.gameObject.AddComponent<VerticalLayoutGroup>();
            ringCol.padding = new RectOffset(2, 2, 2, 2);
            ringCol.childControlWidth = true; ringCol.childControlHeight = true; ringCol.childForceExpandWidth = true; ringCol.childForceExpandHeight = false;
            RectTransform msg = SpellingUi.Box("Bubble", msgRing, -1, -1);
            Image bubble = ShowUi.Panel(msg, ColorRole.Card);
            bubble.color = Color.Lerp(Theme.Color(ColorRole.Card), Theme.Color(ColorRole.Accent), .35f);   // mixed opaque (linear blending)
            var msgCol = msg.gameObject.AddComponent<VerticalLayoutGroup>();
            msgCol.padding = new RectOffset(16, 16, 12, 12);
            msgCol.childControlWidth = true; msgCol.childControlHeight = true; msgCol.childForceExpandHeight = false;
            TMP_Text text = SpellingUi.Text(msg, "Text", "", 16.8f, FontRole.Body, ColorRole.Ink, false);
            text.alignment = TextAlignmentOptions.Left;
            text.lineSpacing = 12f;
            // .dm-msg i: not italic, a highlighter under the clue, lowercase. TMP draws <mark> over the text (it washed the
            // clue out to grey), so the highlighter is an underline and the clue stays full ink.
            text.text = SpellingLines.MessageBefore + "<u><noparse>" + FanmailRules.ClueLower(clue) + "</noparse></u>" + SpellingLines.MessageAfter;

            // the choices (parts.js:86-87 choiceButtons, .choices.grid2)
            int n = _deal.Options.Count, rows = (n + 1) / 2;
            _grid = SpellingUi.Box("Choices", area, 640, rows * 64 + (rows - 1) * 12);
            _grid.gameObject.AddComponent<CanvasGroup>();
            var grid = _grid.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(314, 64);
            grid.spacing = new Vector2(12, 12);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.UpperCenter;
            for (int i = 0; i < n; i++)
            {
                int k = i;
                ShowButton b = ShowUi.Button(_ctx.ButtonPrefab, _grid, "Choice " + i, _deal.Options[i], true, () => OnPick(k));
                b.Rect.localEulerAngles = new Vector3(0, 0, -SpellingLines.ChoiceTilts[i % 4]);   // --r, CSS clockwise
                _choices.Add(b);
            }

            // controls (fanmail.js:169)
            RectTransform controls = SpellingUi.Controls(area);
            SpellingUi.Control(_ctx.HintButton(controls));
            _next = SpellingUi.Control(ShowUi.Button(_ctx.ButtonPrefab, controls, "Next", SpellingLines.NextSong, false, NextSong), 190);
            if (_next != null) _next.Go.SetActive(false);                 // hidden until a wrong pick
        }

        /// <summary>"WT_CHOICES pos=x:y,..." each choice's centre in page pixels from the top left, as remix's WT_RACK.</summary>
        private void LogChoices()
        {
            if (this == null || _choices.Count == 0) return;
            Canvas.ForceUpdateCanvases();
            var sb = new System.Text.StringBuilder("WT_CHOICES pos=");
            var corners = new Vector3[4];
            for (int i = 0; i < _choices.Count; i++)
            {
                _choices[i].Rect.GetWorldCorners(corners);
                Vector3 c = (corners[0] + corners[2]) * .5f;
                if (i > 0) sb.Append(',');
                sb.Append(Mathf.RoundToInt(c.x)).Append(':').Append(Mathf.RoundToInt(UnityEngine.Screen.height - c.y));
            }
            Debug.Log(sb.ToString());
        }
    }
}
