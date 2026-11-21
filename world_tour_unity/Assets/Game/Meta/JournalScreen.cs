using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;
using WorldTour.Game.Audio;
using WorldTour.Game.Map;
using WorldTour.Game.Motion;
using WorldTour.Game.Ui;
using WorldTour.Game.Util;

namespace WorldTour.Game.Meta
{
    /// <summary>
    /// The tour journal sticker book (port of world_tour/screens/journal.js, paged: <see cref="JournalBook"/>). Each page
    /// is the web's lined notebook paper with ten slots: earned stickers sit in theirs until she moves them, stickers still
    /// to earn show an empty dashed slot with the web's hint; bought stickers get a last page. Drag a sticker to move it,
    /// or click it and then click an empty spot; arrow keys nudge the focused sticker, Enter or Space picks it up.
    /// Page Up / Page Down and the arrow buttons turn pages. Stickers she hasn't seen yet peel on and slap down when their
    /// page opens (journal.js: 300 ms, then 220 ms apart), and are then marked seen; nothing is ever removed. Logs
    /// "WT_JOURNAL_READY", "WT_JOURNAL_PAGE", "WT_JOURNAL_PEEL key=..", "WT_JOURNAL_PLACE key=.. x=.. y=..".
    /// </summary>
    public sealed class JournalScreen : GameScreen
    {
        public const string PrefabResource = "Screens/Screen_Journal";
        public const string ReadyLog = "WT_JOURNAL_READY";
        public const string PageLog = "WT_JOURNAL_PAGE";
        public const string PeelLog = "WT_JOURNAL_PEEL";
        public const string PlaceLog = "WT_JOURNAL_PLACE";
        const float StickerSize = 100f;
        const float Nudge = .03f;

        [SerializeField] MapArt art;
        [SerializeField] StickerButton buttonPrefab;
        [SerializeField] Material shout;

        RectTransform _root, _pageRoot, _page;
        TextMeshProUGUI _cap, _pager;
        StickerButton _back, _prev, _next, _shop;
        List<JournalPage> _pages;
        int _pageNo;
        float _pw, _ph;
        float _builtW = -1f, _builtH = -1f;
        JournalSticker _selected;
        readonly Dictionary<string, JournalSticker> _stickers = new Dictionary<string, JournalSticker>();
        Coroutine _peel, _hits;

        /// <summary>The page shown, from 0.</summary>
        public int PageNumber { get { return _pageNo; } }
        public int PageCount { get { return _pages != null ? _pages.Count : 0; } }
        /// <summary>The stickers on the page shown, by key (for tests).</summary>
        public IReadOnlyDictionary<string, JournalSticker> Stickers { get { return _stickers; } }

        /// <summary>Opens the journal over the current screen, or in place of it (from backstage's "see it in your journal").</summary>
        public static JournalScreen Open(ScreenStack stack, bool replaceTop = false)
        {
            JournalScreen prefab = Resources.Load<JournalScreen>(PrefabResource);
            GameObject template = null;
            if (prefab == null)
            {
                template = new GameObject("Screen_Journal", typeof(RectTransform), typeof(CanvasGroup));
                template.SetActive(false);
                prefab = template.AddComponent<JournalScreen>();
            }
            JournalScreen s = replaceTop && stack.Count > 1 ? stack.Replace(prefab) : stack.Push(prefab);
            if (template != null) Destroy(template);
            return s;
        }

        static SaveBlob Blob { get { return MapScreen.Blob; } }

        protected internal override void OnPushed()
        {
            // journal.js:15 awardStickers(): catches anything earned outside a show
            SaveBlob s = Blob;
            if (ProgressRules.AwardStickers(s, ModeCatalog.Infos, LocalClock.TodayIso()).Count > 0) MapScreen.Persist();
            _pages = JournalBook.Pages(s);
            _pageNo = JournalBook.StartPage(_pages);
            Build();
            int fresh = 0;
            foreach (JournalPage p in _pages) foreach (JournalSlot sl in p.Slots) if (sl.Fresh) fresh++;
            Debug.Log(ReadyLog + " page=" + _pageNo + " pages=" + _pages.Count + " earned=" + JournalBook.EarnedCount(s) + " fresh=" + fresh + " theme=" + Theme.Palette.Id);
        }

        protected internal override void OnShown() { LogHitsSoon(); }

        protected internal override void OnHidden()
        {
            if (_peel != null) { StopCoroutine(_peel); _peel = null; }
        }

        void Update()
        {
            if (_builtW > 0f && (Mathf.Abs(Rect.rect.width - _builtW) > 1f || Mathf.Abs(Rect.rect.height - _builtH) > 1f)) Build();
            if (Stack == null || Stack.Top != this) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) { Stack.Pop(); return; }
            if (kb.pageDownKey.wasPressedThisFrame) Turn(1);
            else if (kb.pageUpKey.wasPressedThisFrame) Turn(-1);
        }

        // ------------------------------------------------------------------ build

        void Build()
        {
            if (_root != null) { _root.gameObject.SetActive(false); Destroy(_root.gameObject); }
            _root = UiKit.Stretch(UiKit.Rect("Root", transform));
            float vw = Rect.rect.width > 1f ? Rect.rect.width : 1366f, vh = Rect.rect.height > 1f ? Rect.rect.height : 768f;
            _builtW = Rect.rect.width > 1f ? vw : -1f; _builtH = Rect.rect.height > 1f ? vh : -1f;
            float cw = Mathf.Min(vw - 2f * Tokens.Gutter, 980f), x0 = (vw - cw) * 0.5f;
            SaveBlob s = Blob;

            _back = MetaUi.Button(buttonPrefab, _root, "Back", MapText.BackToMap, StickerButton.Style.Ghost, Tokens.FontButtonSm, new Vector2(x0, 20f));
            if (_back != null) _back.OnClick.AddListener(() => { if (Stack != null) Stack.Pop(); });
            FirstSelected = _back;

            TextMeshProUGUI eyebrow = UiKit.Text(_root, "Eyebrow", JournalBook.Eyebrow(s), FontRole.BodyBold, Tokens.Fs0, ColorRole.Ink, TextAlignmentOptions.TopLeft, false);
            UiKit.Place(eyebrow.rectTransform, new Vector2(x0, 88f), new Vector2(cw, 18f), UiKit.TopLeft);
            TextMeshProUGUI title = MetaUi.Shout(_root, "Title", JournalText.Title, Tokens.Fs4, shout);
            UiKit.Place(title.rectTransform, new Vector2(x0, 104f), new Vector2(cw * 0.6f, 62f), UiKit.TopLeft);

            _cap = UiKit.Text(_root, "Caption", "", FontRole.BodyBold, 16f, ColorRole.Ink, TextAlignmentOptions.TopLeft, false);
            UiKit.Place(_cap.rectTransform, new Vector2(x0, 174f), new Vector2(cw, 20f), UiKit.TopLeft);
            DefaultCaption();

            // the bottom row: the pager in the middle, "more stickers to stick in: backstage." on the right
            float rowY = vh - 20f - Tokens.Tap;
            _prev = MetaUi.Button(buttonPrefab, _root, "PrevPage", JournalText.Prev, StickerButton.Style.Ghost, Tokens.FontButtonSm, new Vector2(x0, rowY), Tokens.Tap, 64f);
            _next = MetaUi.Button(buttonPrefab, _root, "NextPage", JournalText.Next, StickerButton.Style.Ghost, Tokens.FontButtonSm, new Vector2(x0 + 64f + 180f, rowY), Tokens.Tap, 64f);
            if (_prev != null) _prev.OnClick.AddListener(() => Turn(-1));
            if (_next != null) _next.OnClick.AddListener(() => Turn(1));
            _pager = UiKit.Text(_root, "Pager", "", FontRole.BodyBold, 15f, ColorRole.Ink, TextAlignmentOptions.Center, false);
            UiKit.Place(_pager.rectTransform, new Vector2(x0 + 64f, rowY), new Vector2(180f, Tokens.Tap), UiKit.TopLeft);

            _shop = MetaUi.Button(buttonPrefab, _root, "ToBackstage", Backstage.Title, StickerButton.Style.Ghost, Tokens.FontButtonSm, Vector2.zero, Tokens.Tap);
            if (_shop != null)
            {
                RectTransform srt = (RectTransform)_shop.transform;
                float bw = srt.sizeDelta.x;
                UiKit.Place(srt, new Vector2(x0 + cw - bw * 0.5f - 6f, rowY + Tokens.Tap * 0.5f), srt.sizeDelta, UiKit.Center);
                _shop.OnClick.AddListener(() => { if (Stack != null) BackstageScreen.Open(Stack); });
                TextMeshProUGUI more = UiKit.Text(_root, "MoreStickers", JournalText.MoreStickers, FontRole.BodyBold, 16f, ColorRole.Ink, TextAlignmentOptions.Right, false);
                float mw = UiKit.WidthOf(more) + 4f;
                UiKit.Place(more.rectTransform, new Vector2(x0 + cw - bw - 18f - mw, rowY), new Vector2(mw, Tokens.Tap), UiKit.TopLeft);
            }

            // the page (journal.js .scrap.pagewrap > .torn.lined.jpage, two tapes)
            float top = 204f, bottom = rowY - 18f;
            _pw = cw; _ph = Mathf.Max(260f, bottom - top);
            _pageRoot = UiKit.Place(UiKit.Rect("PageWrap", _root), new Vector2(x0, top), new Vector2(_pw, _ph), UiKit.TopLeft);
            BuildPage();
            ThemeSkin.Dirty();
            if (Stack != null && Stack.Top == this) LogHitsSoon();
        }

        void DefaultCaption()
        {
            if (_cap == null) return;
            SaveBlob s = Blob;
            bool any = false;
            foreach (JournalPage p in _pages) foreach (JournalSlot sl in p.Slots) any |= sl.Earned;
            // journal.js:28 the caption once there is a sticker; :34 the empty book's line; :43 the full book's line
            string text = !any ? JournalText.Empty : JournalBook.EarnedCount(s) == Milestones.All.Count ? JournalText.Caption + "  " + JournalText.Full : JournalText.Caption;
            _cap.text = Lower.Of(text);
        }

        void BuildPage()
        {
            if (_peel != null) { StopCoroutine(_peel); _peel = null; }
            for (int i = _pageRoot.childCount - 1; i >= 0; i--) { GameObject c = _pageRoot.GetChild(i).gameObject; c.SetActive(false); Destroy(c); }
            _stickers.Clear();
            _selected = null;
            _pages = JournalBook.Pages(Blob);   // what is fresh now (stickers already peeled are seen)
            _pageNo = Mathf.Clamp(_pageNo, 0, Mathf.Max(0, _pages.Count - 1));
            JournalPage page = _pages[_pageNo];

            Image shadow = UiKit.Box(_pageRoot, "Shadow", ColorRole.Ink);
            UiKit.Stretch(shadow.rectTransform); shadow.rectTransform.anchoredPosition = new Vector2(Tokens.Shadow, -Tokens.Shadow);
            _page = UiKit.Stretch(UiKit.Rect("Page", _pageRoot));
            _page.pivot = new Vector2(0f, 1f);
            Sprite lined = art != null ? art.Get("paper_lined") : null;
            Image paper = lined != null ? UiKit.Img(_page, "Paper", lined, Color.white, raycast: true) : UiKit.Box(_page, "Paper", ColorRole.Card);
            if (lined != null) paper.type = Image.Type.Tiled;
            paper.raycastTarget = true;
            UiKit.Stretch(paper.rectTransform);
            paper.gameObject.AddComponent<JournalPageHit>().Owner = this;

            // empty slots first, then the stickers on top (so a moved sticker can sit over an empty slot)
            float slotW = Mathf.Min(150f, _pw / JournalBook.Cols - 42f), slotH = Mathf.Min(196f, _ph * 0.5f - 40f);
            foreach (JournalSlot sl in page.Slots) if (!sl.Earned) EmptySlot(sl, slotW, slotH);
            var fresh = new List<JournalSticker>();
            foreach (JournalSlot sl in page.Slots)
            {
                if (!sl.Earned) continue;
                JournalSticker js = Sticker(sl);
                if (sl.Fresh) { js.Body.GetComponent<CanvasGroup>().alpha = 0f; fresh.Add(js); }
            }

            Tape(_pageRoot, "TapeL", art != null ? art.Get("tape_a") : null, new Vector2(14f + 48f, -10f + 13f), -8f);
            Tape(_pageRoot, "TapeR", art != null ? (art.Get("tape_c") ?? art.Get("tape_b")) : null, new Vector2(_pw - 14f - 48f, -10f + 13f), 7f);

            if (_pager != null) _pager.text = JournalText.PageOf(_pageNo, _pages.Count);
            if (_prev != null) _prev.interactable = _pageNo > 0;
            if (_next != null) _next.interactable = _pageNo < _pages.Count - 1;
            if (fresh.Count > 0) _peel = StartCoroutine(PeelOn(fresh));
        }

        void Tape(RectTransform parent, string name, Sprite sp, Vector2 center, float cssTilt)
        {
            if (sp == null) return;
            Image t = UiKit.Img(parent, name, sp, Color.white);
            UiKit.Place(t.rectTransform, center, new Vector2(96f, 26f), UiKit.Center);
            t.rectTransform.localEulerAngles = new Vector3(0f, 0f, MapLayout.UnityTilt(cssTilt));
        }

        Vector2 PagePos(double fx, double fy) { return new Vector2((float)fx * _pw, -(float)fy * _ph); }

        /// <summary>journal.js .toearn li: a dashed box, the sticker greyed (grayscale, 45%), its name and how to earn it.</summary>
        void EmptySlot(JournalSlot sl, float w, float h)
        {
            RectTransform root = UiKit.Rect("Slot_" + sl.Key, _page);
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(w, h);
            // the empty slot sits square on its grid cell (the sticker that lands there keeps the web's jitter and tilt)
            root.anchoredPosition = PagePos((sl.Index % JournalBook.Cols + .5) / JournalBook.Cols, sl.Index < JournalBook.Cols ? .27 : .73);
            Image fill = UiKit.Box(root, "Fill", ColorRole.Card, 0.55f);
            UiKit.Stretch(fill.rectTransform);
            MetaUi.Outline(root, "Dash", new Vector2(w, h), ColorRole.Ink, 2.5f, 7f, 5f);
            Sprite sp = JournalArt(sl.Art, true);
            float y = 12f;
            if (sp != null)
            {
                string sn = SpriteName(sl.Art, true);
                Image im = UiKit.Sticker(root, "Art", sp, art.Box(sn), new Vector2((w - 50f) * 0.5f, y), 50f);   // 50 (was 60): room for the 16 px how-to-earn line (polish 6.2E item 5)
                Greyscale.Set(im, true, GreyStyle.Unearned);
            }
            y += 56f;
            TextMeshProUGUI name = UiKit.Text(root, "Name", sl.Name, FontRole.BodyBold, 14f, ColorRole.Ink, TextAlignmentOptions.Top);
            float nh = UiKit.HeightFor(name, w - 16f);
            UiKit.Place(name.rectTransform, new Vector2(8f, y), new Vector2(w - 16f, nh), UiKit.TopLeft);
            y += nh + 4f;
            TextMeshProUGUI how = UiKit.Text(root, "How", sl.Note, FontRole.BodyBold, 16f, ColorRole.Ink, TextAlignmentOptions.Top);
            UiKit.Place(how.rectTransform, new Vector2(8f, y), new Vector2(w - 16f, Mathf.Max(20f, h - y - 6f)), UiKit.TopLeft);
            how.overflowMode = TextOverflowModes.Ellipsis;
        }

        JournalSticker Sticker(JournalSlot sl)
        {
            RectTransform root = UiKit.Rect("Stk_" + sl.Key, _page);
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(StickerSize, StickerSize);
            root.anchoredPosition = PagePos(sl.Spot.X, sl.Spot.Y);
            root.localEulerAngles = new Vector3(0f, 0f, MapLayout.UnityTilt((float)sl.Spot.R));
            Image hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(1, 1, 1, 0);
            RectTransform body = UiKit.Stretch(UiKit.Rect("Body", root));
            body.pivot = new Vector2(0.5f, 0.5f);
            body.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            Sprite sp = JournalArt(sl.Art, false);
            if (sp != null) UiKit.Sticker(body, "Art", sp, art.Box(SpriteName(sl.Art, false)), Vector2.zero, StickerSize);
            JournalSticker js = root.gameObject.AddComponent<JournalSticker>();
            js.Init(this, sl, body, hit);
            _stickers[sl.Key] = js;
            return js;
        }

        static string SpriteName(string artId, bool small)
        {
            if (artId != null && artId.StartsWith("venue:")) return "venue_" + artId.Substring(6);
            return artId + (small ? "_sm" : "");
        }

        /// <summary>sticker.js journalArt: a venue drawing for "venue:&lt;id&gt;", else the sticker (a guitar wears her skin).</summary>
        Sprite JournalArt(string artId, bool small)
        {
            if (art == null || string.IsNullOrEmpty(artId)) return null;
            return art.Get(SpriteName(artId, small));
        }

        // ------------------------------------------------------------------ peel on (journal.js:110-113)

        IEnumerator PeelOn(List<JournalSticker> fresh)
        {
            yield return new WaitForSecondsRealtime(0.3f);
            for (int i = 0; i < fresh.Count; i++)
            {
                if (i > 0) yield return new WaitForSecondsRealtime(0.22f);
                JournalSticker js = fresh[i];
                if (js == null) continue;
                CanvasGroup g = js.Body.GetComponent<CanvasGroup>();
                Feedback.PeelOn(js.Body, true, () => { if (g != null) g.alpha = 1f; });
                if (JournalBook.MarkSeen(Blob, new[] { js.Key }).Count > 0) MapScreen.Persist();
                Debug.Log(PeelLog + " key=" + js.Key);
            }
            _peel = null;
        }

        // ------------------------------------------------------------------ moving stickers

        internal void Select(JournalSticker js)
        {
            if (_selected != null && _selected != js) _selected.SetSelected(false);
            _selected = js == _selected ? null : js;
            if (_selected != null)
            {
                _selected.SetSelected(true);
                _cap.text = Lower.Of(JournalText.Selected(js.Slot.Name, js.Slot.Note));
            }
            else
            {
                js.SetSelected(false);
                DefaultCaption();
            }
        }

        /// <summary>Where a screen point falls on the page, as fractions of the page.</summary>
        internal Vector2 PageFraction(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_page, screen, null, out Vector2 local);
            return new Vector2(local.x / _pw, -local.y / _ph);
        }

        /// <summary>Shows a sticker at (fx, fy), clamped to the page as meta.js place() clamps.</summary>
        internal void Show(JournalSticker js, float fx, float fy)
        {
            fx = Mathf.Clamp(fx, .05f, .95f); fy = Mathf.Clamp(fy, .05f, .95f);
            ((RectTransform)js.transform).anchoredPosition = PagePos(fx, fy);
        }

        /// <summary>journal.js moveTo(save) + slap + pop + "stuck.": puts a sticker down at (fx, fy) and saves.</summary>
        internal void StickAt(JournalSticker js, float fx, float fy, bool slap)
        {
            Show(js, fx, fy);
            MetaRules.Place(Blob, js.Key, fx, fy, js.Slot.Spot.R);
            Spot p = Blob.Journal.Layout[js.Key];
            js.Slot.Spot = p;
            MapScreen.Persist();
            Debug.Log(PlaceLog + " key=" + js.Key + " x=" + p.X.ToString(CultureInfo.InvariantCulture) + " y=" + p.Y.ToString(CultureInfo.InvariantCulture));
            if (slap)
            {
                Feedback.Slap(js.Body);
                Sfx.Play(SfxId.Pop);
                if (_selected != null) { _selected.SetSelected(false); _selected = null; }
                _cap.text = JournalText.Stuck;
            }
        }

        /// <summary>journal.js:84-91 tap-to-place: a click on the page moves the selected sticker there.</summary>
        public void PageClicked(Vector2 screen)
        {
            if (_selected == null) return;
            Vector2 f = PageFraction(screen);
            StickAt(_selected, f.x, f.y, true);
        }

        /// <summary>journal.js:78-82 arrow keys nudge the focused sticker by 3% of the page and save.</summary>
        internal void NudgeBy(JournalSticker js, float dx, float dy)
        {
            StickAt(js, (float)js.Slot.Spot.X + dx, (float)js.Slot.Spot.Y + dy, false);
        }

        void Turn(int dir)
        {
            int to = Mathf.Clamp(_pageNo + dir, 0, _pages.Count - 1);
            if (to == _pageNo) return;
            _pageNo = to;
            _pages = JournalBook.Pages(Blob);
            BuildPage();
            DefaultCaption();
            ThemeSkin.Dirty();
            Debug.Log(PageLog + " page=" + _pageNo);
            // keep keyboard focus on the pager (the arrow just pressed, or the other one at the end of the book)
            StickerButton keep = dir > 0 ? (_next != null && _next.interactable ? _next : _prev) : (_prev != null && _prev.interactable ? _prev : _next);
            GameObject cur = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            bool onPager = cur != null && ((_next != null && cur == _next.gameObject) || (_prev != null && cur == _prev.gameObject));
            if (keep != null && onPager) EventSystem.current.SetSelectedGameObject(keep.gameObject);
            LogHitsSoon();
        }

        /// <summary>Turns to a page (for tests and the pager).</summary>
        public void GoTo(int page) { Turn(page - _pageNo); }

        internal float NudgeStep { get { return Nudge; } }

        // ------------------------------------------------------------------ test hooks

        void LogHitsSoon()
        {
            if (_hits != null) StopCoroutine(_hits);
            if (isActiveAndEnabled) _hits = StartCoroutine(LogHitsLater());
        }

        IEnumerator LogHitsLater()
        {
            yield return new WaitForSecondsRealtime(0.4f);
            _hits = null;
            var list = new List<KeyValuePair<string, RectTransform>>();
            if (_back != null) list.Add(new KeyValuePair<string, RectTransform>("back", (RectTransform)_back.transform));
            if (_prev != null) list.Add(new KeyValuePair<string, RectTransform>("prev", (RectTransform)_prev.transform));
            if (_next != null) list.Add(new KeyValuePair<string, RectTransform>("next", (RectTransform)_next.transform));
            if (_shop != null) list.Add(new KeyValuePair<string, RectTransform>("shop", (RectTransform)_shop.transform));
            if (_page != null) list.Add(new KeyValuePair<string, RectTransform>("page", _page));
            foreach (var kv in _stickers) list.Add(new KeyValuePair<string, RectTransform>("stk:" + kv.Key, (RectTransform)kv.Value.transform));
            MetaUi.LogHits("journal", list);
        }
    }

    /// <summary>The journal page's paper: a click on it places the selected sticker there.</summary>
    public sealed class JournalPageHit : MonoBehaviour, IPointerClickHandler
    {
        public JournalScreen Owner;

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || Owner == null) return;
            Owner.PageClicked(e.position);
        }
    }

    /// <summary>
    /// One sticker on the journal page: drag to move (a press that moves less than the drag threshold is a click), click
    /// to pick up or put back, Enter or Space to pick up, arrow keys to nudge. Arrows don't move focus away from it.
    /// </summary>
    public sealed class JournalSticker : Selectable, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, ISubmitHandler
    {
        JournalScreen _owner;
        UiLine _ring;
        bool _dragged;

        public string Key { get { return Slot != null ? Slot.Key : null; } }
        public JournalSlot Slot { get; private set; }
        public RectTransform Body { get; private set; }
        public bool IsPicked { get; private set; }

        internal void Init(JournalScreen owner, JournalSlot slot, RectTransform body, Graphic hit)
        {
            _owner = owner; Slot = slot; Body = body;
            targetGraphic = hit;
            transition = Transition.None;
            navigation = new Navigation { mode = Navigation.Mode.None };
        }

        internal void SetSelected(bool on)
        {
            IsPicked = on;
            if (on && _ring == null)
            {
                // screens.css .jstk.sel: a dashed accent-2 outline, 4 px out
                _ring = MetaUi.Outline((RectTransform)transform, "Picked", new Vector2(108f + 8f, 108f + 8f), ColorRole.Accent2, 3f, 8f, 5f);
            }
            if (_ring != null) _ring.gameObject.SetActive(on);
        }

        public override void OnPointerDown(PointerEventData e)
        {
            base.OnPointerDown(e);
            _dragged = false;
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || _dragged || _owner == null) return;
            Sfx.Play(SfxId.Tap);
            _owner.Select(this);
        }

        public void OnSubmit(BaseEventData e)
        {
            if (_owner == null) return;
            Sfx.Play(SfxId.Tap);
            _owner.Select(this);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || _owner == null) return;
            _dragged = true;
            transform.SetAsLastSibling();
            Body.localScale = new Vector3(1.12f, 1.12f, 1f);   // .jstk.lift
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_dragged || _owner == null) return;
            Vector2 f = _owner.PageFraction(e.position);
            _owner.Show(this, f.x, f.y);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!_dragged || _owner == null) return;
            Body.localScale = Vector3.one;
            Vector2 f = _owner.PageFraction(e.position);
            _owner.StickAt(this, f.x, f.y, true);
        }

        void Update()
        {
            if (_owner == null || EventSystem.current == null || EventSystem.current.currentSelectedGameObject != gameObject) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            float d = _owner.NudgeStep;
            if (kb.spaceKey.wasPressedThisFrame) { OnSubmit(null); return; }
            if (kb.leftArrowKey.wasPressedThisFrame) _owner.NudgeBy(this, -d, 0f);
            else if (kb.rightArrowKey.wasPressedThisFrame) _owner.NudgeBy(this, d, 0f);
            else if (kb.upArrowKey.wasPressedThisFrame) _owner.NudgeBy(this, 0f, -d);
            else if (kb.downArrowKey.wasPressedThisFrame) _owner.NudgeBy(this, 0f, d);
        }
    }
}
