using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WorldTour.Core.Data;
using WorldTour.Core.Meta;
using WorldTour.Core.Shows;
using WorldTour.Game.Audio;
using WorldTour.Game.Motion;
using WorldTour.Game.Ui;
using WorldTour.Game.Util;

namespace WorldTour.Game.Ticket
{
    /// <summary>
    /// The end of a show: world_tour/screens/ticket.js renderTicket on Unity (task 2.6). The shout and lede, the ticket
    /// stub (venue, fans, first try / best streak / hints, vip or ga, the per-show barcode, the date), the "sold out"
    /// stamp on a vip night, the new journal stickers, the last venue unlocked, tonight's setlist with its tags, the
    /// encore or play-again button, "back to the map", and riff's line. One column, 680 wide, scrolling, as the web page.
    /// Built in code from a <see cref="TicketData"/>; the prefab (TicketScreenBuilder) only carries the button prefab
    /// and the art. Nothing waits on an animation: the buttons work from the first frame, and every motion is under
    /// 400 ms (a short fade under reduce motion).
    /// Log lines: WT_TICKET_SHOWN (see <see cref="TicketModel.LogLine"/>) and WT_TICKET_ACTION action=encore|again|map|journal.
    /// </summary>
    public sealed class TicketScreen : GameScreen
    {
        /// <summary>Where <see cref="Open"/> looks for the prefab (made by the TicketScreenBuilder Editor script).</summary>
        public const string PrefabResource = "Screens/Screen_Ticket";

        // screens.css .finish and friends, in canvas units (CSS px at 1366 wide)
        const float Width = 680f;          // .finish max-width
        const float TicketHeight = 205f;   // .ticket at 1366: tk-main padding 18 + top 27 + venue 23 + fans 63 + rows 56 + 18
        const float StubWidth = 88f;       // .tk-stub
        const float LedeWidth = 44f * 0.6f * 16.8f; // .lede max-width 44ch in Courier Prime at 1.05rem
        /// <summary>ticket.js:88: the stamp peels on 450 ms after the ticket.</summary>
        public const float StampDelay = 0.45f;
        /// <summary>The new stickers peel on after the stamp, one after another (new; the web shows them still).</summary>
        public const float StickerDelay = 0.6f, StickerStagger = 0.08f;

        [SerializeField] StickerButton buttonPrefab;
        [SerializeField] TicketArt art;

        /// <summary>Raised for every ticket that opens, so the dog (2.3) can sit in <see cref="CoachArea"/> without a scene reference.</summary>
        public static event Action<TicketScreen> Opened;

        /// <summary>"encore: n to rehearse" (ticket.js:75, only when the summary's CanEncore). The host starts the encore show.</summary>
        public event Action StartEncore;
        /// <summary>"play again" (ticket.js:75, when there is no encore). The host starts a new show of the same mode and week.</summary>
        public event Action PlayAgain;
        /// <summary>"back to the map" (ticket.js:76). With no listener the screen pops back to the stack's first screen.</summary>
        public event Action BackToMap;
        /// <summary>The new-stickers panel links to the journal (ticket.js:53). The panel is clickable only while someone listens.</summary>
        public event Action OpenJournal;

        public TicketData Data { get; private set; }
        public TicketModel Model { get; private set; }
        /// <summary>The 110-wide spot left of riff's bubble (.finish .coach .dog-host).</summary>
        public RectTransform CoachArea { get; private set; }
        /// <summary>The bars drawn (for tests).</summary>
        public TicketBarcodeGraphic Barcode { get; private set; }
        /// <summary>The primary button (encore or play again) and "back to the map".</summary>
        public StickerButton PrimaryButton { get; private set; }
        public StickerButton BackButton { get; private set; }

        RectTransform _content, _viewport, _ticketWrap, _stamp;
        ScrollRect _scroll;
        readonly List<RectTransform> _stickerArts = new List<RectTransform>();
        Button _journalLink;
        bool _left;
        GameObject _lastSelected;

        // ---------- opening ----------

        /// <summary>Swaps the screen on top (the finished show) for the ticket.</summary>
        public static TicketScreen Open(ScreenStack stack, TicketData data)
        {
            if (stack == null) throw new ArgumentNullException(nameof(stack));
            if (data == null || data.Summary == null) throw new ArgumentException("the ticket needs the show's summary", nameof(data));
            TicketScreen prefab = Resources.Load<TicketScreen>(PrefabResource);
            GameObject template = null;
            if (prefab == null)
            {
                template = new GameObject("Screen_Ticket", typeof(RectTransform), typeof(CanvasGroup));
                template.SetActive(false);
                prefab = template.AddComponent<TicketScreen>();
            }
            TicketScreen s = stack.Count > 0 ? stack.Replace(prefab) : stack.SetRoot(prefab);
            if (template != null) Destroy(template);
            s.Show(data);
            return s;
        }

        /// <summary>Draws the ticket for <paramref name="data"/>, plays the crowd and starts the motions. Once per screen.</summary>
        public void Show(TicketData data)
        {
            if (Model != null) throw new InvalidOperationException("this ticket is already drawn");
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Model = TicketModel.Build(data, data.Date ?? LocalClock.TodayIso());
            Build();
            if (PrimaryButton != null && EventSystem.current != null && Stack != null && Stack.Top == this)
                EventSystem.current.SetSelectedGameObject(PrimaryButton.gameObject);
            FirstSelected = PrimaryButton;
            // The first view is the top of the ticket (ticket.js:87 scrollTo(0, 0)): the initial focus must not scroll
            // the page down to the buttons. Later focus moves (Tab, arrows) still scroll the focused button into view.
            if (PrimaryButton != null) _lastSelected = PrimaryButton.gameObject;

            // show.js:229 snd.crowd(V.crowd), right after renderTicket
            Sfx.Crowd(Model.Venue != null ? Model.Venue.Crowd : 0.3);
            Animate();
            Debug.Log(Model.LogLine);
            var h = Opened; if (h != null) h(this);
        }

        // ---------- actions ----------

        void Act(string action, Action handler, Action fallback)
        {
            if (_left) return; // one way out: a second button pressed in the same moment is dropped
            _left = handler != null || fallback != null;
            Debug.Log("WT_TICKET_ACTION action=" + action);
            if (handler != null) handler();
            else if (fallback != null) fallback();
            else Debug.LogWarning("[ticket] nothing listens for " + action);
        }

        void OnPrimary()
        {
            if (Model.Primary == TicketPrimary.Encore) Act("encore", StartEncore, null);
            else Act("again", PlayAgain, null);
        }

        void OnBack()
        {
            Act("map", BackToMap, () =>
            {
                if (Stack == null) return;
                if (Stack.Count > 1) Stack.PopToRoot();
            });
        }

        void OnJournal()
        {
            if (OpenJournal == null) return;
            Act("journal", OpenJournal, null);
        }

        // ---------- motion ----------

        void Animate()
        {
            // ticket.js:89 peelOn(.ticketwrap) at once; :88 the stamp 450 ms later
            if (_ticketWrap != null) Feedback.PeelOn(_ticketWrap, sound: false);
            if (_stamp != null)
            {
                // not ??: in the Editor a missing component is a fake-null object that ?? treats as present
                CanvasGroup g = _stamp.GetComponent<CanvasGroup>();
                if (g == null) g = _stamp.gameObject.AddComponent<CanvasGroup>();
                g.alpha = 0f;
                g.blocksRaycasts = false;
            }
            if (isActiveAndEnabled) StartCoroutine(Later());
            else Settle();
        }

        IEnumerator Later()
        {
            float t0 = Time.unscaledTime;
            if (_stamp != null)
            {
                while (Time.unscaledTime - t0 < StampDelay) yield return null;
                Feedback.PeelOn(_stamp, sound: false);
            }
            for (int i = 0; i < _stickerArts.Count; i++)
            {
                float at = StickerDelay + i * StickerStagger;
                while (Time.unscaledTime - t0 < at) yield return null;
                if (_stickerArts[i] == null) continue;
                Feedback.PeelOn(_stickerArts[i], sound: i == 0);
            }
        }

        void Settle()
        {
            if (_stamp != null) { CanvasGroup sg = _stamp.GetComponent<CanvasGroup>(); if (sg != null) sg.alpha = 1f; }
            foreach (RectTransform r in _stickerArts) if (r != null) r.GetComponent<CanvasGroup>().alpha = 1f;
        }

        protected internal override void OnRemoved()
        {
            StopAllCoroutines();
        }

        void Update()
        {
            // keep the keyboard-focused button in view (the page scrolls; uGUI doesn't follow focus by itself)
            EventSystem es = EventSystem.current;
            GameObject sel = es != null ? es.currentSelectedGameObject : null;
            if (sel == _lastSelected || _scroll == null) return;
            _lastSelected = sel;
            if (sel == null || !sel.transform.IsChildOf(_content)) return;
            ScrollIntoView((RectTransform)sel.transform);
        }

        void ScrollIntoView(RectTransform target)
        {
            float viewH = _viewport.rect.height, contentH = _content.rect.height;
            if (contentH <= viewH) return;
            Vector3[] c = new Vector3[4];
            target.GetWorldCorners(c);
            float top = -_content.InverseTransformPoint(c[1]).y, bottom = -_content.InverseTransformPoint(c[0]).y;
            float y = _content.anchoredPosition.y;
            if (bottom > y + viewH - 16f) y = bottom - viewH + 16f;
            if (top < y + 16f) y = top - 16f;
            y = Mathf.Clamp(y, 0f, contentH - viewH);
            _scroll.StopMovement();
            _content.anchoredPosition = new Vector2(_content.anchoredPosition.x, y);
        }

        // ---------- drawing ----------

        void Build()
        {
            RectTransform root = Rect;
            Stretch(root);

            // the page scrolls (the web page is about 1.6 screens tall at 1366x768)
            _viewport = NewRect("Viewport", root);
            Stretch(_viewport);
            _viewport.gameObject.AddComponent<RectMask2D>();
            var hit = _viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0); // catches the wheel and drags between the elements
            _content = NewRect("Content", _viewport);
            _content.anchorMin = _content.anchorMax = new Vector2(.5f, 1f);
            _content.pivot = new Vector2(.5f, 1f);
            _scroll = root.gameObject.AddComponent<ScrollRect>();
            _scroll.viewport = _viewport;
            _scroll.content = _content;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 40f;
            _scroll.inertia = true;

            TicketModel m = Model;
            float y = 18f; // .screen padding-top

            // ticket.js:27 eyebrow
            y = Eyebrow(_content, m.EyebrowLeft, m.EyebrowRight, 0f, y, Tokens.Fs0);

            // ticket.js:28 <h1 class="shout">, rotate(-2deg) from the left, margin 8 0 10
            y += 8f;
            TMP_Text head = Text(_content, "Head", m.Head, Tokens.Fs5, FontRole.Display, ColorRole.Card);
            head.lineSpacing = LineSpacing(head, 0.92f);
            Shout(head);
            float hh = Measure(head, Width).y;
            RectTransform headRt = head.rectTransform;
            Place(headRt, 0f, y, Width, hh);
            headRt.pivot = new Vector2(0f, .5f);
            headRt.anchoredPosition = new Vector2(0f, -(y + hh / 2f));
            headRt.localEulerAngles = new Vector3(0, 0, 2f);
            y += hh + 10f;

            // ticket.js:29 lede
            TMP_Text lede = Text(_content, "Lede", m.Sub, 16.8f, FontRole.Body, ColorRole.Ink);
            lede.textWrappingMode = TextWrappingModes.Normal;
            float lh = Measure(lede, LedeWidth).y;
            Place(lede.rectTransform, 0f, y, LedeWidth, lh);
            y += lh;

            // ticket.js:31-50 the ticket, margin 26 0 22
            y += 26f;
            BuildTicket(y);
            y += TicketHeight + 22f;

            // ticket.js:52-56 new journal stickers
            if (m.Stickers.Count > 0) y = BuildStickers(y) + 22f;

            // ticket.js:58-62 new venue unlocked
            if (m.Unlock != null) y = BuildUnlock(y) + 22f;

            // ticket.js:64-72 tonight's setlist
            y = BuildSetlist(y) + 22f;

            // ticket.js:74-77 buttons (.row gap 12, .btn flex 1 1 220px)
            float bw = (Width - Tokens.RowGap) / 2f;
            PrimaryButton = Button(_content, "PrimaryButton", m.PrimaryLabel, false, OnPrimary, 0f, y, bw);
            BackButton = Button(_content, "BackButton", TicketText.BackToMap, true, OnBack, bw + Tokens.RowGap, y, bw);
            y += Tokens.ButtonMinHeight;

            // ticket.js:79-82 riff and his line (.coach margin-top 22, gap 10, aligned to the bottom)
            y += 22f;
            y = BuildCoach(y);

            y += 10f + Tokens.Gutter; // .screen padding-bottom, plus room under the bubble's shadow
            _content.sizeDelta = new Vector2(Width, y);
            _content.anchoredPosition = Vector2.zero; // ticket.js:87 window.scrollTo(0, 0)
        }

        void BuildTicket(float y)
        {
            TicketModel m = Model;
            _ticketWrap = NewRect("TicketWrap", _content);
            Place(_ticketWrap, 0f, y, Width, TicketHeight);
            _ticketWrap.pivot = new Vector2(.5f, .5f);
            _ticketWrap.anchoredPosition = new Vector2(Width / 2f, -(y + TicketHeight / 2f));

            // the stub art: .ticket + .tk-stub.perf with the 6px shadow; the sprite reaches 16 above, 22 below, 6 right
            RectTransform paper = NewRect("Stub", _ticketWrap);
            Stretch(paper);
            paper.offsetMin = new Vector2(0f, -22f);
            paper.offsetMax = new Vector2(6f, 16f);
            Image paperImg = paper.gameObject.AddComponent<Image>();
            paperImg.raycastTarget = false;
            if (art != null && art.ticketStub != null) { paperImg.sprite = art.ticketStub; paperImg.type = Image.Type.Sliced; }
            else { paperImg.color = Theme.Color(ColorRole.Card); paper.offsetMin = Vector2.zero; paper.offsetMax = Vector2.zero; }

            // .tk-main, padding 18 20
            RectTransform main = NewRect("Main", _ticketWrap);
            Stretch(main);
            main.offsetMin = new Vector2(20f, 18f);
            main.offsetMax = new Vector2(-(StubWidth + 20f), -18f);
            float mw = Width - StubWidth - 40f;
            float my = 0f;

            // .tk-top: "admit one" + heart, hand 1.7rem, paper-deep with a .6px ink stroke
            TMP_Text top = Text(main, "Top", TicketText.AdmitOne, 27.2f, FontRole.Display, ColorRole.PaperDeep);
            Stroke(top, 0.6f);
            Vector2 ts = Measure(top, 0f);
            Place(top.rectTransform, 0f, my, ts.x + 2f, 27.2f);
            Glyph(main, TicketText.Heart, TicketGlyphGraphic.Shape.Heart, ColorRole.PaperDeep, ts.x + 27.2f * 0.6f, my + 27.2f * 0.15f, 27.2f * 0.7f);
            my += 27.2f;

            // .tk-venue, margin-top 4
            my += 4f;
            TMP_Text venue = Text(main, "Venue", m.LiveAt, Tokens.Fs0, FontRole.BodyBold, ColorRole.Ink);
            Place(venue.rectTransform, 0f, my, mw, 19f);
            my += 19f;

            // .tk-fans, margin 10 0 8: <b>number</b> fans
            my += 10f;
            TMP_Text fans = Text(main, "Fans", "<size=44.8><b>" + m.FansNumber + "</b></size> " + TicketText.FansWord, 16f, FontRole.Body, ColorRole.Ink);
            fans.alignment = TextAlignmentOptions.BottomLeft;
            Place(fans.rectTransform, 0f, my, mw, 45f);
            my += 45f + 8f;

            // .tk-rows: a 2px rule, padding-top 8, three columns (gap 6)
            RectTransform rule = NewRect("Rule", main);
            Place(rule, 0f, my, mw, 2f);
            var ruleImg = rule.gameObject.AddComponent<Image>();
            ruleImg.color = Theme.Color(ColorRole.Ink);
            ruleImg.raycastTarget = false;
            my += 2f + 8f;
            float cw = (mw - 12f) / 3f;
            for (int i = 0; i < m.Rows.Length; i++)
            {
                TMP_Text dt = Text(main, "Dt" + i, m.Rows[i].Key, 11.2f, FontRole.Body, ColorRole.Ink);
                Place(dt.rectTransform, i * (cw + 6f), my, cw, 17f);
                TMP_Text dd = Text(main, "Dd" + i, m.Rows[i].Value, 19.2f, FontRole.BodyBold, ColorRole.Ink);
                Place(dd.rectTransform, i * (cw + 6f), my + 17f, cw, 29f);
            }

            // .tk-stub: vip/ga, the barcode, the date; padding 14 8, space-between
            RectTransform stub = NewRect("StubText", _ticketWrap);
            stub.anchorMin = new Vector2(1f, 0f); stub.anchorMax = new Vector2(1f, 1f);
            stub.pivot = new Vector2(1f, .5f);
            stub.sizeDelta = new Vector2(StubWidth, -28f);
            stub.anchoredPosition = Vector2.zero;
            TMP_Text cls = Text(stub, "Class", m.Class, 32f, FontRole.Display, ColorRole.Ink);
            cls.alignment = TextAlignmentOptions.Top;
            cls.rectTransform.anchorMin = new Vector2(0f, 1f); cls.rectTransform.anchorMax = new Vector2(1f, 1f);
            cls.rectTransform.pivot = new Vector2(.5f, 1f);
            cls.rectTransform.sizeDelta = new Vector2(0f, 32f);
            cls.rectTransform.anchoredPosition = Vector2.zero;

            // .tk-stub is a flex column with space-between and gap 8: vip/ga on top, the date at the bottom, the barcode
            // centred in the space left between them, so the bars never run into the date (2.7 tester: "2026-11-20" lost
            // its first digit under the bars).
            const float dateLength = 66f, stubGap = 8f;
            RectTransform band = NewRect("BarcodeBand", stub);
            band.anchorMin = Vector2.zero; band.anchorMax = Vector2.one;
            band.offsetMin = new Vector2(0f, dateLength + stubGap);
            band.offsetMax = new Vector2(0f, -(32f + stubGap));
            RectTransform bc = NewRect("Barcode", band);
            bc.anchorMin = bc.anchorMax = new Vector2(.5f, .5f);
            bc.sizeDelta = new Vector2(60f, 60f);
            bc.anchoredPosition = Vector2.zero;
            Barcode = bc.gameObject.AddComponent<TicketBarcodeGraphic>();
            Barcode.color = Theme.Color(ColorRole.Ink);
            Barcode.raycastTarget = false;
            Barcode.SetBars(m.Bars);

            // .tk-date: writing-mode vertical-rl reads top to bottom
            TMP_Text date = Text(stub, "Date", m.Date, 10.56f, FontRole.BodyBold, ColorRole.Ink);
            date.alignment = TextAlignmentOptions.Center;
            RectTransform dr = date.rectTransform;
            dr.anchorMin = dr.anchorMax = new Vector2(.5f, 0f);
            dr.pivot = new Vector2(.5f, .5f);
            dr.sizeDelta = new Vector2(dateLength, 14f);
            dr.localEulerAngles = new Vector3(0, 0, -90f);
            dr.anchoredPosition = new Vector2(0f, dateLength / 2f);

            // ticket.js:49 the stamp, only on a vip night: right 100, bottom -26, 84x84, rotate -14
            if (m.ShowStamp)
            {
                _stamp = NewRect("Stamp", _ticketWrap);
                _stamp.anchorMin = _stamp.anchorMax = new Vector2(1f, 0f);
                _stamp.pivot = new Vector2(.5f, .5f);
                _stamp.sizeDelta = new Vector2(84f, 84f);
                _stamp.anchoredPosition = new Vector2(-100f - 42f, -26f + 42f);
                _stamp.localEulerAngles = new Vector3(0, 0, 14f);
                Image si = _stamp.gameObject.AddComponent<Image>();
                si.raycastTarget = false;
                if (art != null && art.stamp != null) si.sprite = art.stamp;
                else si.color = new Color(1, 1, 1, .7f);
                TMP_Text st = Text(_stamp, "Text", TicketText.Stamp, 25.6f, FontRole.Display, ColorRole.Accent2);
                Stretch(st.rectTransform);
                st.alignment = TextAlignmentOptions.Center;
                st.lineSpacing = LineSpacing(st, 0.85f);
            }
        }

        float BuildStickers(float y)
        {
            TicketModel m = Model;
            int k = Math.Min(TicketText.StickersShown, m.Stickers.Count);
            float rowW = 64f + (k - 1) * 50f; // 64 each, margin-right -14
            float textX = 16f + 3f + rowW + 14f;
            float textW = Width - textX - 16f - 3f;
            TMP_Text names = Text(null, "Names", m.StickerNames, Tokens.Fs3, FontRole.Display, ColorRole.Ink);
            names.textWrappingMode = TextWrappingModes.Normal;
            float nh = Measure(names, textW).y;
            float inner = Mathf.Max(64f, 20f + nh);
            RectTransform box = Panel("NewStickers", y, inner + 20f + 6f);
            names.rectTransform.SetParent(box, false);

            for (int i = 0; i < k; i++)
            {
                Milestone s = m.Stickers[i];
                RectTransform a = NewRect("Sticker" + i, box);
                Place(a, 19f + i * 50f, 13f + (inner - 64f) / 2f, 64f, 64f);
                a.pivot = new Vector2(.5f, .5f);
                a.anchoredPosition = new Vector2(19f + i * 50f + 32f, -(13f + inner / 2f));
                Sprite sp = art != null ? art.Journal(s.Art) : null;
                Image img = a.gameObject.AddComponent<Image>();
                img.raycastTarget = false;
                img.preserveAspect = true;
                if (sp != null) img.sprite = sp; else img.color = Theme.Color(ColorRole.Card);
                CanvasGroup g = a.gameObject.AddComponent<CanvasGroup>();
                g.alpha = 0f; // peels on in Later()
                g.blocksRaycasts = false;
                _stickerArts.Add(a);
            }

            float ty = 13f + (inner - (20f + nh)) / 2f;
            TMP_Text title = Text(box, "Title", TicketText.StickersTitle, Tokens.Fs0, FontRole.BodyBold, ColorRole.Ink);
            title.characterSpacing = 6f;
            Place(title.rectTransform, textX, ty, textW, 20f);
            Place(names.rectTransform, textX, ty + 20f, textW, nh);

            // the panel is a link to the journal (ticket.js:53) while someone listens
            var hitImg = box.GetComponent<Image>();
            hitImg.raycastTarget = true;
            _journalLink = box.gameObject.AddComponent<Button>();
            _journalLink.transition = Selectable.Transition.None;
            _journalLink.navigation = new Navigation { mode = Navigation.Mode.None };
            _journalLink.onClick.AddListener(OnJournal);
            return y + inner + 26f;
        }

        float BuildUnlock(float y)
        {
            Venue v = Model.Unlock;
            Sprite sp = art != null ? art.VenueArt(v) : null;
            float aw = 110f, ah = sp != null ? aw * sp.rect.height / sp.rect.width : aw * 476f / 561f;
            float textX = 19f + aw + 14f, textW = Width - textX - 19f;
            TMP_Text name = Text(null, "Name", v.Name, Tokens.Fs3, FontRole.Display, ColorRole.Ink);
            name.textWrappingMode = TextWrappingModes.Normal;
            float nh = Measure(name, textW).y;
            float inner = Mathf.Max(ah, 20f + nh);
            RectTransform box = Panel("Unlock", y, inner + 26f);
            name.rectTransform.SetParent(box, false);

            RectTransform a = NewRect("Venue", box);
            Place(a, 19f, 13f + (inner - ah) / 2f, aw, ah);
            Image img = a.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            if (sp != null) img.sprite = sp; else img.color = Theme.Color(ColorRole.Card);

            float ty = 13f + (inner - (20f + nh)) / 2f;
            TMP_Text title = Text(box, "Title", TicketText.UnlockTitle, Tokens.Fs0, FontRole.BodyBold, ColorRole.Ink);
            title.characterSpacing = 6f;
            Place(title.rectTransform, textX, ty, textW, 20f);
            Place(name.rectTransform, textX, ty + 20f, textW, nh);
            return y + inner + 26f;
        }

        /// <summary>.unlock: accent fill, 3px ink border, 4px hard shadow, rotate(-1deg).</summary>
        RectTransform Panel(string name, float y, float h)
        {
            RectTransform wrap = NewRect(name, _content);
            Place(wrap, 0f, y, Width, h);
            wrap.pivot = new Vector2(.5f, .5f);
            wrap.anchoredPosition = new Vector2(Width / 2f, -(y + h / 2f));
            wrap.localEulerAngles = new Vector3(0, 0, 1f);

            RectTransform shadow = NewRect("Shadow", wrap);
            Stretch(shadow);
            shadow.offsetMin = new Vector2(Tokens.ShadowMd, -Tokens.ShadowMd);
            shadow.offsetMax = new Vector2(Tokens.ShadowMd, -Tokens.ShadowMd);
            Fill(shadow, ColorRole.Ink);
            RectTransform border = NewRect("Border", wrap);
            Stretch(border);
            Fill(border, ColorRole.Ink);
            RectTransform face = NewRect("Face", wrap);
            Stretch(face);
            face.offsetMin = new Vector2(Tokens.BorderWidth, Tokens.BorderWidth);
            face.offsetMax = new Vector2(-Tokens.BorderWidth, -Tokens.BorderWidth);
            Fill(face, ColorRole.Accent);
            Image hit = wrap.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            hit.raycastTarget = false;
            return wrap;
        }

        float BuildSetlist(float y)
        {
            TicketModel m = Model;
            const float padT = 30f, padR = 20f, padB = 26f, padL = 50f, line = 32f, titleH = 33.6f;
            float h = padT + titleH + 8f + m.Setlist.Count * line + padB;
            RectTransform wrap = NewRect("Setlist", _content);
            Place(wrap, 0f, y, Width, h);

            // .torn.lined in a .scrap (6px drop shadow, baked into the sprite at its right and bottom)
            RectTransform paper = NewRect("Paper", wrap);
            Stretch(paper);
            paper.offsetMin = new Vector2(0f, -Tokens.Shadow);
            paper.offsetMax = new Vector2(Tokens.Shadow, 0f);
            Image pi = paper.gameObject.AddComponent<Image>();
            pi.raycastTarget = false;
            if (art != null && art.paperLined != null) { pi.sprite = art.paperLined; pi.type = Image.Type.Tiled; }
            else { pi.color = Theme.Color(ColorRole.Card); paper.offsetMin = paper.offsetMax = Vector2.zero; }

            // .tape.tr.b: 96x26, top -10, right 14, rotate(7deg)
            RectTransform tape = NewRect("Tape", wrap);
            tape.anchorMin = tape.anchorMax = new Vector2(1f, 1f);
            tape.pivot = new Vector2(.5f, .5f);
            tape.sizeDelta = new Vector2(96f, 26f);
            tape.anchoredPosition = new Vector2(-14f - 48f, 10f - 13f);
            tape.localEulerAngles = new Vector3(0, 0, -7f);
            Image ti = tape.gameObject.AddComponent<Image>();
            ti.raycastTarget = false;
            if (art != null && art.tape != null) ti.sprite = art.tape; else ti.enabled = false;

            float iw = Width - padL - padR;
            TMP_Text title = Text(wrap, "Title", TicketText.SetlistTitle, titleH, FontRole.Display, ColorRole.Ink);
            Place(title.rectTransform, padL, padT, iw, titleH);
            float ly = padT + titleH + 8f;
            for (int i = 0; i < m.Setlist.Count; i++)
            {
                TicketSong s = m.Setlist[i];
                float tagW = 150f;
                TMP_Text tag = Text(wrap, "Tag" + i, s.TagText, 20.8f, FontRole.Display, TagColour(s.Tag));
                tag.alignment = TextAlignmentOptions.MidlineRight;
                if (s.Tag == SetlistTag.Miss) Stroke(tag, 0.5f);
                Place(tag.rectTransform, padL + iw - tagW, ly, tagW, line);

                // .w::before "01. " at .8rem, then the word (escaped: words come from weeks.json)
                TMP_Text w = Text(wrap, "Song" + i, "<size=12.8>" + s.Number + ". </size>" + Plain(s.Text), 16.8f, FontRole.Body, ColorRole.Ink);
                w.alignment = TextAlignmentOptions.MidlineLeft;
                w.overflowMode = TextOverflowModes.Ellipsis;
                Place(w.rectTransform, padL, ly, iw - tagW - 10f, line);

                if (s.Tag == SetlistTag.Miss)
                {
                    // underline wavy paper-deep 2px, offset 4px, under the word only
                    float numW = w.GetPreferredValues("<size=12.8>" + s.Number + ". </size>", 100000f, 0f).x;
                    float wordW = Mathf.Min(Measure(w, 0f).x - numW, iw - tagW - 10f - numW);
                    RectTransform wave = NewRect("Wave" + i, wrap);
                    Place(wave, padL + numW, ly + line / 2f + 16.8f * 0.35f + 4f - 3f, Mathf.Max(0f, wordW), 6f);
                    var wl = wave.gameObject.AddComponent<TicketWavyLine>();
                    wl.color = Theme.Color(ColorRole.PaperDeep);
                    wl.raycastTarget = false;
                }
                ly += line;
            }
            return y + h;
        }

        static ColorRole TagColour(SetlistTag t)
        {
            // screens.css:183-185: ok is accent-2, miss is paper-deep with an ink stroke, hint keeps the ink
            return t == SetlistTag.Ok ? ColorRole.Accent2 : t == SetlistTag.Miss ? ColorRole.PaperDeep : ColorRole.Ink;
        }

        float BuildCoach(float y)
        {
            const float dogW = 110f, gap = 10f;
            float bx = dogW + gap, bw = Width - bx;
            TMP_Text line = Text(null, "Line", Model.Coach, 16f, FontRole.Body, ColorRole.Ink);
            line.textWrappingMode = TextWrappingModes.Normal;
            line.lineSpacing = LineSpacing(line, 1.4f);
            float th = Measure(line, bw - 32f - 6f).y;
            float bh = Mathf.Max(64f, th + 24f + 6f);
            float dogH = 110f;
            float rowH = Mathf.Max(bh, dogH);

            CoachArea = NewRect("CoachArea", _content);
            Place(CoachArea, 0f, y + rowH - dogH, dogW, dogH);

            RectTransform bubble = NewRect("Bubble", _content);
            Place(bubble, bx, y + rowH - bh, bw, bh);
            Image bi = bubble.gameObject.AddComponent<Image>();
            bi.raycastTarget = false;
            if (art != null && art.bubble != null) { bi.sprite = art.bubble; bi.type = Image.Type.Sliced; }
            else bi.color = Theme.Color(ColorRole.Card);
            if (art != null && art.bubbleTail != null)
            {
                // .bubble::before sits on the left edge, 10px up, pointing at riff
                RectTransform tail = NewRect("Tail", bubble);
                tail.anchorMin = tail.anchorMax = new Vector2(0f, 0f);
                tail.pivot = new Vector2(.5f, .5f);
                tail.sizeDelta = new Vector2(art.bubbleTail.rect.width / 2f, art.bubbleTail.rect.height / 2f);
                tail.anchoredPosition = new Vector2(-4f, 10f + 9f);
                tail.localEulerAngles = new Vector3(0, 0, -90f);
                Image tli = tail.gameObject.AddComponent<Image>();
                tli.sprite = art.bubbleTail;
                tli.raycastTarget = false;
            }
            line.rectTransform.SetParent(bubble, false);
            Stretch(line.rectTransform);
            line.rectTransform.offsetMin = new Vector2(16f + 3f, 12f + 3f);
            line.rectTransform.offsetMax = new Vector2(-16f - 3f, -12f - 3f);
            line.alignment = TextAlignmentOptions.MidlineLeft;
            return y + rowH;
        }

        // ---------- small builders ----------

        /// <summary>The eyebrow: left text, the star, right text, at 12.8px bold with .06em letter spacing.</summary>
        float Eyebrow(RectTransform parent, string left, string right, float x, float y, float size)
        {
            TMP_Text l = Text(parent, "Eyebrow", Plain(left), size, FontRole.BodyBold, ColorRole.Ink);
            l.characterSpacing = 6f;
            if (HasSprite(0x2736))
            {
                // builder 5's glyph sprite asset is in: the text keeps the real character
                l.text = Lower.Of(Plain(left) + " " + TicketText.Star + " " + Plain(right));
                Place(l.rectTransform, x, y, Width, 20f);
                return y + 20f;
            }
            float lw = Measure(l, 0f).x;
            Place(l.rectTransform, x, y, lw + 1f, 20f);
            float space = size * 0.6f + size * 0.06f;
            Glyph(parent, TicketText.Star, TicketGlyphGraphic.Shape.Star6, ColorRole.Ink, x + lw + space, y + (20f - size) / 2f, size);
            TMP_Text r = Text(parent, "EyebrowRight", Plain(right), size, FontRole.BodyBold, ColorRole.Ink);
            r.characterSpacing = 6f;
            Place(r.rectTransform, x + lw + space * 2f + size, y, Width - (lw + space * 2f + size), 20f);
            return y + 20f;
        }

        /// <summary>A glyph the fonts lack: the TMP sprite asset's (builder 5), else the ticket art's sprite, else drawn.</summary>
        void Glyph(RectTransform parent, string ch, TicketGlyphGraphic.Shape shape, ColorRole colour, float x, float y, float size)
        {
            RectTransform g = NewRect("Glyph", parent);
            Place(g, x, y, size, size);
            Sprite sp = art != null ? (shape == TicketGlyphGraphic.Shape.Heart ? art.heart : art.star6) : null;
            if (HasSprite(ch[0]))
            {
                var t = g.gameObject.AddComponent<TextMeshProUGUI>();
                Theme.ApplyFont(t, FontRole.Body);
                t.fontSize = size;
                t.color = Theme.Color(colour);
                t.alignment = TextAlignmentOptions.Center;
                t.raycastTarget = false;
                t.text = ch;
                g.sizeDelta = new Vector2(size * 1.4f, size * 1.4f);
            }
            else if (sp != null)
            {
                var img = g.gameObject.AddComponent<Image>();
                img.sprite = sp;
                img.preserveAspect = true;
                img.color = Theme.Color(colour);
                img.raycastTarget = false;
            }
            else
            {
                var gl = g.gameObject.AddComponent<TicketGlyphGraphic>();
                gl.GlyphShape = shape;
                gl.color = Theme.Color(colour);
                gl.raycastTarget = false;
            }
        }

        /// <summary>True when TMP's default sprite asset draws this character (builder 5's GlyphSetup).</summary>
        static bool HasSprite(uint unicode)
        {
            TMP_SpriteAsset a = TMP_Settings.instance != null ? TMP_Settings.defaultSpriteAsset : null;
            return a != null && a.spriteCharacterLookupTable != null && a.spriteCharacterLookupTable.ContainsKey(unicode);
        }

        StickerButton Button(RectTransform parent, string name, string text, bool ghost, Action onClick, float x, float y, float w)
        {
            RectTransform box = NewRect(name + "Box", parent);
            Place(box, x, y, w, Tokens.ButtonMinHeight);
            if (buttonPrefab == null)
            {
                Debug.LogWarning("[ticket] no StickerButton prefab (run UiBuilder.BuildAll, then TicketScreenBuilder.Build)");
                return null;
            }
            StickerButton b = Instantiate(buttonPrefab, box, false);
            b.name = name;
            b.ButtonStyle = ghost ? StickerButton.Style.Ghost : StickerButton.Style.Ink;
            b.Text = text;
            var rt = (RectTransform)b.transform;
            Stretch(rt);
            b.OnClick.AddListener(() => onClick());
            return b;
        }

        /// <summary>A label in a theme colour and font, lowercased, rich text on. Parent null = not placed yet.</summary>
        TMP_Text Text(RectTransform parent, string name, string text, float size, FontRole font, ColorRole colour)
        {
            RectTransform rt = NewRect(name, parent != null ? parent : _content);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            Theme.ApplyFont(t, font);
            t.fontSize = size;
            t.color = Theme.Color(colour);
            t.alignment = TextAlignmentOptions.TopLeft;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.richText = true;
            t.SetLower(text);
            return t;
        }

        /// <summary>The .shout look: the art's material preset (white face, ink outline, ink offset shadow), else plain ink text.</summary>
        void Shout(TMP_Text t)
        {
            if (art != null && art.shout != null && t.font != null && art.shout.mainTexture == t.font.material.mainTexture) t.fontSharedMaterial = art.shout;
            else t.color = Theme.Color(ColorRole.Ink);
        }

        /// <summary>-webkit-text-stroke on a display label, via the art's stroke preset (skipped when it isn't there).</summary>
        void Stroke(TMP_Text t, float px)
        {
            if (art != null && art.stroke != null && t.font != null && art.stroke.mainTexture == t.font.material.mainTexture) t.fontSharedMaterial = art.stroke;
        }

        /// <summary>TMP lineSpacing (em/100 added to the font's line height) that gives a CSS line-height of <paramref name="cssLineHeight"/>.</summary>
        static float LineSpacing(TMP_Text t, float cssLineHeight)
        {
            if (t.font == null || t.font.faceInfo.pointSize <= 0) return 0f;
            float natural = t.font.faceInfo.lineHeight / t.font.faceInfo.pointSize;
            return (cssLineHeight - natural) * 100f;
        }

        /// <summary>Preferred size at a width (0 = unlimited).</summary>
        static Vector2 Measure(TMP_Text t, float width)
        {
            return t.GetPreferredValues(t.text, width > 0 ? width : 100000f, 0f);
        }

        /// <summary>User text (week labels, words) shown as typed: TMP must not read "&lt;" in it as a tag.</summary>
        static string Plain(string s)
        {
            // a literal "</noparse>" inside would end the escape early; break it with a zero-width space
            return string.IsNullOrEmpty(s) ? "" : "<noparse>" + s.Replace("</noparse>", "</" + (char)0x200B + "noparse>") + "</noparse>";
        }

        static void Fill(RectTransform rt, ColorRole c)
        {
            Image i = rt.gameObject.AddComponent<Image>();
            i.color = Theme.Color(c);
            i.raycastTarget = false;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        /// <summary>Places a box by its top-left corner, in CSS-like coordinates (y down from the parent's top-left).</summary>
        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
        }
    }
}
