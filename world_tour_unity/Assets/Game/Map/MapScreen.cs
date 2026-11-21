using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WorldTour.Core.Data;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;
using WorldTour.Game.Meta;
using WorldTour.Game.Motion;
using WorldTour.Game.Ui;
using WorldTour.Game.Util;

namespace WorldTour.Game.Map
{
    /// <summary>
    /// Home: the tour map (port of world_tour/screens/map.js, landscape layout). The hero (title, tonight's venue,
    /// total fans, day streak, the dog's line), the route heading, and the map: eight venue stickers on the route,
    /// locked ones greyed, the route drawn up to the newest unlocked venue, and the doodles. The page scrolls when it
    /// is taller than the window. A venue unlocked since the last visit gets its moment: the route grows to it, the
    /// sticker peels on and fades from grey to colour, and the dog is told to cheer. Every motion is under 400 ms,
    /// none blocks input, and reduce motion turns them into instant changes or short fades (Tweener does that).
    /// Logs "WT_MAP_READY venues=8 unlocked=2 fans=33300" each time it is shown, "WT_UNLOCK_SHOWN id=records" per
    /// unlock moment, and "WT_VENUE_OPEN id=coffee" (from <see cref="VenueCard"/>) when a venue card opens.
    /// The UI is built in code each time the screen is shown, from <see cref="MapArt"/> (made by MapBuilder).
    /// </summary>
    public sealed class MapScreen : GameScreen
    {
        public const string ReadyLog = "WT_MAP_READY";
        public const string UnlockLog = "WT_UNLOCK_SHOWN";

        [SerializeField] MapArt art;
        [SerializeField] StickerButton buttonPrefab;
        [SerializeField] VenueCard venueCardPrefab;

        /// <summary>
        /// Tells the mascot what to do on the home screen (task 2.3 listens): "idle" or "sleepy" (3+ days away, map.js:24)
        /// when the map is shown, "cheer" at an unlock moment. Raised with this screen's <see cref="DogHost"/> ready.
        /// </summary>
        public static event Action<MapScreen, string> DogCue;

        /// <summary>Where the dog stands in the hero (170 x 170, screens.css:37); empty until task 2.3 puts the dog there.</summary>
        public RectTransform DogHost { get; private set; }

        /// <summary>The model the current build shows (for tests and the dog).</summary>
        public MapModel Model { get; private set; }

        static SaveBlob s_Session;

        ScrollRect _scroll;
        RectTransform _content;
        float _builtWidth = -1f;
        float _contentHeight;
        readonly Dictionary<string, VenueView> _venues = new Dictionary<string, VenueView>();
        UiLine _done;
        Coroutine _unlock;

        sealed class VenueView
        {
            public RectTransform Root, Body;
            public Image Art;
            public float CenterY;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_Session = null; DogCue = null; }

        /// <summary>The save: Boot's, or a session-only blank one when Main runs without Boot.</summary>
        internal static SaveBlob Blob
        {
            get
            {
                if (Boot.Store != null && Boot.Store.Blob != null) return Boot.Store.Blob;
                return s_Session ?? (s_Session = SaveBlob.Defaults(LocalClock.TodayIso()));
            }
        }

        internal static void Persist() { if (Boot.Store != null) Boot.Store.Save(); }

        protected internal override void OnPushed()
        {
            BuildFrame();
        }

        protected internal override void OnShown()
        {
            Rebuild(playUnlocks: true);
        }

        protected internal override void OnHidden()
        {
            if (_unlock != null) { StopCoroutine(_unlock); _unlock = null; }
        }

        void Update()
        {
            float w = Rect.rect.width;
            if (_builtWidth > 0f && Mathf.Abs(w - _builtWidth) > 1f) Rebuild(playUnlocks: false);
        }

        // ------------------------------------------------------------------ frame

        void BuildFrame()
        {
            RectTransform scrollRt = UiKit.Stretch(UiKit.Rect("Scroll", transform));
            Image hit = scrollRt.gameObject.AddComponent<Image>();
            hit.color = new Color(1, 1, 1, 0);
            _scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 40f;
            _scroll.inertia = true;

            RectTransform viewport = UiKit.Stretch(UiKit.Rect("Viewport", scrollRt));
            viewport.gameObject.AddComponent<RectMask2D>();
            _scroll.viewport = viewport;

            _content = UiKit.Rect("Content", viewport);
            _content.anchorMin = new Vector2(0f, 1f); _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.sizeDelta = new Vector2(0f, 800f);
            _scroll.content = _content;
        }

        // ------------------------------------------------------------------ build

        void Rebuild(bool playUnlocks)
        {
            if (_unlock != null) { StopCoroutine(_unlock); _unlock = null; }
            if (_content == null) BuildFrame();
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                GameObject c = _content.GetChild(i).gameObject;
                c.SetActive(false);
                Destroy(c);
            }
            _venues.Clear();
            _done = null;

            string today = LocalClock.TodayIso();
            SaveBlob s = Blob;
            MapModel m = MapModel.Build(s, today);
            List<Milestone> stickers;
            if (MapModel.Commit(s, m.Fresh, today, out stickers)) Persist();
            Model = m;

            float vw = Rect.rect.width > 1f ? Rect.rect.width : 1366f;
            _builtWidth = Rect.rect.width > 1f ? vw : -1f;
            float mw = MapLayout.MapWidth(vw, Tokens.Gutter);
            float x0 = (vw - mw) * 0.5f;

            float y = 28f;
            y = BuildHero(m, x0, y, mw);
            y += 22f;
            y = BuildMapHead(m, x0, y, mw) - 6f + 4f;
            MapLayout L = art != null ? art.Layout : null;
            if (L != null) y = BuildMap(m, L, x0, y, mw, playUnlocks && m.Fresh.Count > 0);
            else Debug.LogWarning("[map] " + (art != null ? art.Error : "no map art assigned"));
            y = BuildHub(s, x0, y + 28f, mw);
            y = BuildFooter(x0, y + 24f, mw);
            _contentHeight = y + 40f;
            _content.sizeDelta = new Vector2(0f, _contentHeight);

            int open = 0;
            foreach (VenueState v in m.Venues) if (v.Open) open++;
            Debug.Log(ReadyLog + " venues=" + m.Venues.Count + " unlocked=" + open + " fans=" + m.Lifetime);

            DogCue?.Invoke(this, m.Sleepy ? "sleepy" : "idle");
            if (playUnlocks && m.Fresh.Count > 0 && L != null) _unlock = StartCoroutine(PlayUnlocks(m, L, mw));
        }

        float BuildHero(MapModel m, float x0, float y0, float mw)
        {
            const float gap = 18f;
            float lw = (mw - gap) * 1.25f / 2.25f, rw = mw - gap - lw, rx = x0 + lw + gap;
            RectTransform hero = UiKit.Place(UiKit.Rect("Hero", _content), new Vector2(0f, 0f), new Vector2(0f, 0f), UiKit.TopLeft);
            hero.anchorMin = hero.anchorMax = UiKit.TopLeft;

            // left: title block, then the stat cards (screens.css:35 "title coach" / "stats coach")
            float y = y0;
            TextMeshProUGUI eyebrow = UiKit.Text(hero, "Eyebrow", MapText.Eyebrow, FontRole.BodyBold, Tokens.Fs0, ColorRole.Ink);
            float eh = UiKit.HeightFor(eyebrow, lw);
            UiKit.Place(eyebrow.rectTransform, new Vector2(x0, y), new Vector2(lw, eh), UiKit.TopLeft);
            y += eh + 8f;

            TextMeshProUGUI title = UiKit.Text(hero, "Title", MapText.Title, FontRole.Display, Tokens.Fs5, ColorRole.Ink);
            title.lineSpacing = -18f;
            float th = UiKit.HeightFor(title, lw);
            UiKit.Place(title.rectTransform, new Vector2(x0, y), new Vector2(lw, th), new Vector2(0f, 1f));
            title.rectTransform.localEulerAngles = new Vector3(0f, 0f, MapLayout.UnityTilt(-2f));   // screens.css:25
            y += th + 18f;

            if (buttonPrefab != null)
            {
                StickerButton cta = Instantiate(buttonPrefab, hero, false);
                cta.name = "Tonight";
                cta.Text = MapText.Tonight(m.Newest);
                TMP_Text label = cta.GetComponentInChildren<TMP_Text>();
                if (label != null) label.fontSize = 25.6f;   // screens.css:26 .play-cta 1.6rem
                float bw = (label != null ? UiKit.WidthOf(label) : 260f) + 2f * Tokens.ButtonPadX;
                UiKit.Place((RectTransform)cta.transform, new Vector2(x0 + bw * 0.5f, y + 32f), new Vector2(bw, 64f), UiKit.Center);
                string newest = m.Newest.Id;
                cta.OnClick.AddListener(() => OpenVenue(newest));
                FirstSelected = cta;
                y += 64f;
            }
            y += gap;

            float cw = (lw - 14f) * 0.5f;
            StatCard(hero, "Fans", "star_sm", MapText.Fmt(m.Lifetime), MapText.TotalFans, new Vector2(x0, y), cw, 1.6f);
            StatCard(hero, "Streak", "bolt_sm", m.Streak.ToString(), MapText.DayStreak(m.Streak, m.Best), new Vector2(x0 + cw + 14f, y), cw, -1.2f);
            float leftBottom = y + 68f;

            // right: the coach, bubble over the dog (screens.css:36-38), bottom-aligned with the left column
            TextMeshProUGUI line = null;
            string toGo = m.Next != null ? MapText.Fmt(m.ToGo) : "0";
            string say = MapText.DogHome(m.Next != null ? m.Next.Short : null, toGo, m.Streak, m.Away, UnityEngine.Random.value < 0.5f);
            RectTransform bubble = UiKit.Rect("Bubble", hero);
            line = UiKit.Text(bubble, "Line", say, FontRole.Body, Tokens.Fs1, ColorRole.Ink);
            float bh = Mathf.Max(64f, UiKit.HeightFor(line, rw - 32f - 6f) + 24f + 6f);
            const float dogSize = 170f;
            float rightHeight = bh + 14f + dogSize;
            float bottom = Mathf.Max(leftBottom, y0 + rightHeight);
            float by = bottom - rightHeight;
            UiKit.Place(bubble, new Vector2(rx, by), new Vector2(rw, bh), UiKit.TopLeft);
            Sprite bubbleSprite = art != null ? art.Get("bubble") : null;
            Image bg;
            if (bubbleSprite != null)
            {
                bg = UiKit.Img(bubble, "Paper", bubbleSprite, Color.white);
                bg.type = Image.Type.Sliced;
                UiKit.Stretch(bg.rectTransform);
            }
            else
            {
                UiKit.Card(UiKit.Stretch(UiKit.Rect("Paper", bubble)), Tokens.ShadowSm);
            }
            bubble.Find("Line").SetAsLastSibling();
            UiKit.Stretch(line.rectTransform);
            line.rectTransform.offsetMin = new Vector2(16f, 12f); line.rectTransform.offsetMax = new Vector2(-16f - 6f, -12f);
            Sprite tail = art != null ? art.Get("bubble_tail") : null;
            if (tail != null)
            {
                Image t = UiKit.Img(bubble, "Tail", tail, Color.white);
                UiKit.Place(t.rectTransform, new Vector2(rw * 0.48f, bh - 4f), new Vector2(30f, 22f), UiKit.TopLeft);
            }

            DogHost = UiKit.Place(UiKit.Rect("DogHost", hero), new Vector2(rx + rw * 0.5f, by + bh + 14f + dogSize * 0.5f), new Vector2(dogSize, dogSize), UiKit.Center);
            if (leftBottom < bottom)
            {
                // align-items:end: the left column sits on the same baseline as the dog
                float shift = bottom - leftBottom;
                for (int i = 0; i < hero.childCount; i++)
                {
                    RectTransform c = (RectTransform)hero.GetChild(i);
                    if (c == bubble || c == DogHost) continue;
                    c.anchoredPosition += new Vector2(0f, -shift);
                }
            }
            return bottom;
        }

        void StatCard(RectTransform parent, string name, string sticker, string value, string label, Vector2 at, float w, float cssTilt)
        {
            const float h = 68f;
            RectTransform card = UiKit.Place(UiKit.Rect(name, parent), at + new Vector2(w * 0.5f, h * 0.5f), new Vector2(w, h), UiKit.Center);
            card.localEulerAngles = new Vector3(0f, 0f, MapLayout.UnityTilt(cssTilt));   // map.js:35,38 tilt-2 / tilt-1
            RectTransform face = UiKit.Card(card, Tokens.ShadowMd);
            Sprite sp = art != null ? art.Get(sticker) : null;
            if (sp != null) UiKit.Sticker(face, "Sticker", sp, art.Box(sticker), new Vector2(5f, (h - 52f) * 0.5f - Tokens.BorderWidth), 52f);
            TextMeshProUGUI v = UiKit.Text(face, "Value", value, FontRole.BodyBold, 27.2f, ColorRole.Ink, TextAlignmentOptions.BottomLeft, false);
            UiKit.Place(v.rectTransform, new Vector2(66f, 6f), new Vector2(w - 76f, 30f), UiKit.TopLeft);
            TextMeshProUGUI l = UiKit.Text(face, "Label", label, FontRole.Body, Tokens.Fs0, ColorRole.Ink, TextAlignmentOptions.TopLeft, false);
            UiKit.Place(l.rectTransform, new Vector2(66f, 37f), new Vector2(w - 76f, 18f), UiKit.TopLeft);
        }

        float BuildMapHead(MapModel m, float x0, float y, float mw)
        {
            TextMeshProUGUI h = UiKit.Text(_content, "RouteHeading", MapText.RouteHeading, FontRole.Display, Tokens.Fs3, ColorRole.Ink,
                TextAlignmentOptions.BottomLeft, false);
            UiKit.Place(h.rectTransform, new Vector2(x0, y), new Vector2(mw * 0.5f, 40f), UiKit.TopLeft);

            if (m.Next != null)
            {
                // map.js:52: scribble(short + ' fans') + ' unlocks ' + next.short, right-aligned
                TextMeshProUGUI rest = UiKit.Text(_content, "RouteLine", " unlocks " + m.Next.Short, FontRole.Body, 15.2f, ColorRole.Ink,
                    TextAlignmentOptions.BottomRight, false);
                float rw = UiKit.WidthOf(rest);
                UiKit.Place(rest.rectTransform, new Vector2(x0 + mw - rw, y), new Vector2(rw, 40f), UiKit.TopLeft);
                TextMeshProUGUI fans = UiKit.Text(_content, "RouteFans", MapText.Short(m.Next.Fans) + " fans", FontRole.BodyBold, 15.2f, ColorRole.Ink,
                    TextAlignmentOptions.BottomRight, false);
                float fw = UiKit.WidthOf(fans);
                float fx = x0 + mw - rw - fw;
                UiKit.Place(fans.rectTransform, new Vector2(fx, y), new Vector2(fw, 40f), UiKit.TopLeft);
                Sprite sc = art != null ? art.Get("scribble") : null;
                if (sc != null)
                {
                    Image u = UiKit.Img(_content, "Scribble", sc, Theme.Color(ColorRole.Accent2));
                    UiKit.Place(u.rectTransform, new Vector2(fx - 2f, y + 34f), new Vector2(fw + 4f, 7f), UiKit.TopLeft);
                    fans.transform.SetAsLastSibling();
                }
            }
            else
            {
                TextMeshProUGUI done = UiKit.Text(_content, "RouteLine", MapText.RouteLine(null), FontRole.Body, 15.2f, ColorRole.Ink,
                    TextAlignmentOptions.BottomRight, false);
                UiKit.Place(done.rectTransform, new Vector2(x0 + mw * 0.5f, y), new Vector2(mw * 0.5f, 40f), UiKit.TopLeft);
            }
            // The map's corner tapes reach 10 px above its top edge (paper.css .tape.tl/.tr top:-10px); the text here is
            // bottom-aligned, so leave room or the tapes and the map's edge cover the heading and the unlock line.
            return y + 40f + 14f;
        }

        float BuildMap(MapModel m, MapLayout L, float x0, float y, float mw, bool animateRoute)
        {
            float mh = L.MapHeight(mw), s = L.Scale(mw);
            RectTransform map = UiKit.Place(UiKit.Rect("Map", _content), new Vector2(x0, y), new Vector2(mw, mh), UiKit.TopLeft);

            // .scrap: hard ink shadow, then the torn paper with graph lines
            Image shadow = UiKit.Box(map, "Shadow", ColorRole.Ink);
            UiKit.Stretch(shadow.rectTransform);
            shadow.rectTransform.anchoredPosition = new Vector2(Tokens.Shadow, -Tokens.Shadow);
            Sprite paper = art.Get("paper_map");
            Image bg = paper != null ? UiKit.Img(map, "Paper", paper, Color.white) : UiKit.Box(map, "Paper", ColorRole.Card);
            if (paper != null) bg.type = Image.Type.Tiled;
            UiKit.Stretch(bg.rectTransform);
            UiGrid grid = UiKit.Stretch(UiKit.Rect("Grid", map), 4f).gameObject.AddComponent<UiGrid>();
            Color gc = Theme.Color(ColorRole.Accent); gc.a = 0.22f;
            grid.color = gc;
            grid.Set(28f, 1f);

            // route: under, done, dash (art/map.js:43-47)
            List<Vector2> all = Local(SvgPath.Flatten(SvgPath.Parse(L.CurveAll)), s);
            UiLine under = Line(map, "RouteUnder", all, ColorRole.Card, 0.9f, 18f * s);
            int reached = m.Reached;
            if (reached > 0)
            {
                List<Vector2> done = Local(SvgPath.Flatten(SvgPath.Parse(L.DoneCurve(reached))), s);
                _done = Line(map, "RouteDone", done, ColorRole.Accent, 1f, 12f * s);
                if (animateRoute)
                {
                    float before = m.ReachedBefore > 0 ? SvgPath.Length(Local(SvgPath.Flatten(SvgPath.Parse(L.DoneCurve(m.ReachedBefore))), s)) : 0f;
                    _done.Reveal = before;
                }
            }
            UiLine dash = Line(map, "RouteDash", all, ColorRole.Ink, 1f, 3.5f * s);
            dash.SetDash(10f * s, 11f * s);

            // doodles (map.js:79)
            float dw = MapLayout.DecorWidth(mw);
            foreach (MapDecor d in L.Decor)
            {
                string sn = d.Id + "_sm";
                Sprite sp = art.Get(sn);
                if (sp == null) continue;
                RectTransform holder = UiKit.Place(UiKit.Rect("Decor_" + d.Id, map), new Vector2(d.At.x * s, d.At.y * s), new Vector2(dw, dw), UiKit.Center);
                holder.localEulerAngles = new Vector3(0f, 0f, MapLayout.UnityTilt(d.Rot));
                Image im = UiKit.Sticker(holder, "Sticker", sp, art.Box(sn), Vector2.zero, dw);
                Color c = im.color; c.a = 0.95f; im.color = c;
            }

            // venues (map.js:80-90)
            for (int i = 0; i < m.Venues.Count && i < L.Pts.Length; i++) BuildVenue(map, m, m.Venues[i], L.Pts[i] * s, mw, y);

            // tape on the top corners (map.js:56, paper.css:46-47)
            Tape(map, "TapeL", art.Get("tape_a"), new Vector2(14f + 48f, -10f + 13f), -8f);
            Tape(map, "TapeR", art.Get("tape_c") ?? art.Get("tape_b"), new Vector2(mw - 14f - 48f, -10f + 13f), 7f);
            return y + mh;
        }

        static List<Vector2> Local(List<Vector2> pts, float s)
        {
            for (int i = 0; i < pts.Count; i++) pts[i] = new Vector2(pts[i].x * s, -pts[i].y * s);
            return pts;
        }

        static UiLine Line(RectTransform map, string name, List<Vector2> pts, ColorRole color, float alpha, float width)
        {
            RectTransform rt = UiKit.Rect(name, map);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            UiLine l = rt.gameObject.AddComponent<UiLine>();
            Color c = Theme.Color(color); c.a *= alpha;
            l.color = c;
            l.Width = width;
            l.SetPoints(pts);
            return l;
        }

        void Tape(RectTransform map, string name, Sprite sp, Vector2 center, float cssTilt)
        {
            if (sp == null) return;
            Image t = UiKit.Img(map, name, sp, Color.white);
            UiKit.Place(t.rectTransform, center, new Vector2(96f, 26f), UiKit.Center);
            t.rectTransform.localEulerAngles = new Vector3(0f, 0f, MapLayout.UnityTilt(cssTilt));
        }

        void BuildVenue(RectTransform map, MapModel m, VenueState st, Vector2 at, float mw, float mapTop)
        {
            Venue v = st.Venue;
            string spriteName = "venue_" + v.Art;
            float w = MapLayout.VenueWidth(mw);
            SpriteBox box = art.Box(spriteName);
            float artH = box.BoxHeightFor(w);
            const float labelH = 32f;
            float h = artH + labelH - 4f;

            RectTransform root = UiKit.Place(UiKit.Rect("Venue_" + v.Id, map), at, new Vector2(w, h), UiKit.Center);
            root.localEulerAngles = new Vector3(0f, 0f, MapLayout.UnityTilt(MapLayout.VenueTilt[st.Index]));
            Image hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(1, 1, 1, 0);
            RectTransform body = UiKit.Stretch(UiKit.Rect("Body", root));
            body.pivot = UiKit.Center;

            Sprite sp = art.Get(spriteName);
            Image artImg = sp != null ? UiKit.Sticker(body, "Art", sp, box, Vector2.zero, w) : null;
            if (artImg != null) Greyscale.Set(artImg, !st.Open || st.Fresh, GreyStyle.Locked);

            // label (screens.css:64-70, 79)
            ColorRole labelBg = !st.Open ? ColorRole.Card : st.Fresh ? ColorRole.Accent2 : ColorRole.Ink;
            ColorRole labelFg = !st.Open ? ColorRole.Muted : ColorRole.OnInk;
            TextMeshProUGUI lt = UiKit.Text(body, "LabelText", v.Short, FontRole.Display, 20.8f, labelFg, TextAlignmentOptions.Center, false);
            float lw = UiKit.WidthOf(lt) + 24f;
            RectTransform label = UiKit.Place(UiKit.Rect("Label", body), new Vector2(w * 0.5f, artH - 4f + labelH * 0.5f), new Vector2(lw, labelH), UiKit.Center);
            if (st.Open)
            {
                Image ls = UiKit.Box(label, "Shadow", ColorRole.Accent);
                UiKit.Stretch(ls.rectTransform); ls.rectTransform.anchoredPosition = new Vector2(3f, -3f);
            }
            else
            {
                Image border = UiKit.Box(label, "Border", ColorRole.Muted);
                UiKit.Stretch(border.rectTransform, -2f);
            }
            Image lb = UiKit.Box(label, "Face", labelBg);
            UiKit.Stretch(lb.rectTransform);
            lt.rectTransform.SetParent(label, false);
            UiKit.Stretch(lt.rectTransform);

            // number pill (screens.css:68)
            TextMeshProUGUI nt = UiKit.Text(body, "NumText", MapText.VenueNumber(st.Index), FontRole.BodyBold, 12f, ColorRole.Ink, TextAlignmentOptions.Center, false);
            float nw = UiKit.WidthOf(nt) + 18f;
            RectTransform num = UiKit.Place(UiKit.Rect("Number", body), new Vector2(2f, -4f), new Vector2(nw, 20f), UiKit.TopLeft);
            Image nr = UiKit.Box(num, "Ring", ColorRole.Ink); UiKit.Stretch(nr.rectTransform);
            Image nf = UiKit.Box(num, "Face", ColorRole.Card); UiKit.Stretch(nf.rectTransform, 2f);
            nt.rectTransform.SetParent(num, false); UiKit.Stretch(nt.rectTransform);

            if (!st.Open)
            {
                // sticky note with the fans needed (screens.css:71-75)
                TextMeshProUGUI big = UiKit.Text(body, "StickyFans", MapText.StickyFans(v), FontRole.BodyBold, 16f, ColorRole.Ink, TextAlignmentOptions.Center, false);
                TextMeshProUGUI small = UiKit.Text(body, "StickySmall", MapText.StickySmall, FontRole.Body, 10.56f, ColorRole.Ink, TextAlignmentOptions.Center, false);
                float sw = Mathf.Max(UiKit.WidthOf(big), UiKit.WidthOf(small)) + 16f;
                const float sh = 40f;
                RectTransform sticky = UiKit.Place(UiKit.Rect("Sticky", body), new Vector2(w * 1.04f - sw * 0.5f, h * 0.08f + sh * 0.5f), new Vector2(sw, sh), UiKit.Center);
                sticky.localEulerAngles = new Vector3(0f, 0f, MapLayout.UnityTilt(7f));
                UiKit.Card(sticky, 2f, ColorRole.Accent);
                big.rectTransform.SetParent(sticky, false);
                UiKit.Place(big.rectTransform, new Vector2(sw * 0.5f, 6f + 9f), new Vector2(sw, 18f), UiKit.Center);
                small.rectTransform.SetParent(sticky, false);
                UiKit.Place(small.rectTransform, new Vector2(sw * 0.5f, 6f + 18f + 6f), new Vector2(sw, 12f), UiKit.Center);
            }

            if (st.Here)
            {
                TextMeshProUGUI here = UiKit.Text(body, "HereTag", MapText.HereTag, FontRole.Display, 20f, ColorRole.Accent2, TextAlignmentOptions.Center, false);
                UiKit.Place(here.rectTransform, new Vector2(w * 0.5f, h + 28f - 12f), new Vector2(w + 80f, 26f), UiKit.Center);
                Sprite bus = art.Get("bus_sm");
                if (bus != null)
                {
                    float bw = w * 0.44f;
                    // polish 6.2F item 2: parked above the roof, clear of the sign text
                    RectTransform holder = UiKit.Place(UiKit.Rect("Bus", body), new Vector2(w * 0.80f, -bw * 0.22f), new Vector2(bw, bw), UiKit.Center);
                    holder.localEulerAngles = new Vector3(0f, 0f, MapLayout.UnityTilt(-8f));
                    UiKit.Sticker(holder, "Sticker", bus, art.Box("bus_sm"), Vector2.zero, bw);
                }
            }

            var press = root.gameObject.AddComponent<PressTarget>();
            press.body = body;
            press.transition = Selectable.Transition.None;
            press.targetGraphic = hit;
            string id = v.Id;
            press.Clicked += () => OpenVenue(id);
            press.Focused += p => ScrollTo(mapTop + at.y, true);

            _venues[v.Id] = new VenueView { Root = root, Body = body, Art = artImg, CenterY = mapTop + at.y };
        }

        // ------------------------------------------------------------------ unlock moment

        IEnumerator PlayUnlocks(MapModel m, MapLayout L, float mw)
        {
            yield return new WaitForSecondsRealtime(0.3f);   // map.js:94 setTimeout(..., 300)
            VenueView first;
            if (_venues.TryGetValue(m.Fresh[0], out first)) ScrollTo(first.CenterY, true);
            bool sound = true;
            foreach (string id in m.Fresh)
            {
                VenueView vv;
                if (!_venues.TryGetValue(id, out vv)) continue;
                Feedback.PeelOn(vv.Body, sound);     // peel sound once, as map.js plays snd.peel per sticker at the same moment
                sound = false;
                if (vv.Art != null) Greyscale.FadeToColour(vv.Art, Tokens.TSlow, GreyStyle.Locked);
                Debug.Log(UnlockLog + " id=" + id);
            }
            if (_done != null && _done.Reveal >= 0f)
            {
                UiLine line = _done;
                Tweener.Float(line.Reveal, line.TotalLength, Tokens.TSlow, Ease.Out, v => { if (line != null) line.Reveal = v; },
                    () => { if (line != null) line.Reveal = -1f; }, owner: line);
            }
            DogCue?.Invoke(this, "cheer");
            _unlock = null;
        }

        /// <summary>Scrolls so a point (content y, from the top) sits in the middle of the window when it can.</summary>
        void ScrollTo(float contentY, bool animate)
        {
            if (_scroll == null) return;
            float vh = ((RectTransform)_scroll.viewport).rect.height;
            float range = _contentHeight - vh;
            if (range <= 1f) return;
            float target = 1f - Mathf.Clamp01((contentY - vh * 0.5f) / range);
            float from = _scroll.verticalNormalizedPosition;
            if (Mathf.Abs(target - from) < 0.01f) return;
            ScrollRect sr = _scroll;
            if (!animate) { sr.verticalNormalizedPosition = target; return; }
            Tweener.Float(from, target, Tokens.TMed, Ease.Out, v => { if (sr != null) sr.verticalNormalizedPosition = v; }, null, owner: sr);
        }

        // ------------------------------------------------------------------ hub (map.js:58-61)

        /// <summary>map.js .hubnav: the backstage and tour journal cards under the map, side by side.</summary>
        float BuildHub(SaveBlob s, float x0, float y, float mw)
        {
            const float gap = 14f, h = 76f;
            float cw = (mw - gap) * 0.5f;
            int stk = s.Journal.Stickers.Count;
            HubCard("Hub_backstage", "amp_sm", Backstage.Title, MapText.FansToSpend(s.Fans.Balance), new Vector2(x0, y), cw, h, -2.4f, OpenBackstage);
            HubCard("Hub_journal", "setlist_sm", JournalText.Title, MapText.StickersOf(stk, Milestones.All.Count), new Vector2(x0 + cw + gap, y), cw, h, 2.2f, OpenJournal);
            return y + h;
        }

        void HubCard(string name, string sticker, string title, string small, Vector2 at, float w, float h, float cssTilt, Action open)
        {
            RectTransform root = UiKit.Place(UiKit.Rect(name, _content), at + new Vector2(w * 0.5f, h * 0.5f), new Vector2(w, h), UiKit.Center);
            root.localEulerAngles = new Vector3(0f, 0f, MapLayout.UnityTilt(cssTilt));   // map.js tilt-3 / tilt-4
            Image hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(1, 1, 1, 0);
            RectTransform body = UiKit.Stretch(UiKit.Rect("Body", root));
            body.pivot = UiKit.Center;
            RectTransform face = UiKit.Card(body, Tokens.ShadowMd);
            Sprite sp = art != null ? art.Get(sticker) : null;
            if (sp != null) UiKit.Sticker(face, "Sticker", sp, art.Box(sticker), new Vector2(8f - Tokens.BorderWidth, (h - 58f) * 0.5f - Tokens.BorderWidth), 58f);
            TextMeshProUGUI b = UiKit.Text(face, "Title", title, FontRole.Display, 24f, ColorRole.Ink, TextAlignmentOptions.BottomLeft, false);
            UiKit.Place(b.rectTransform, new Vector2(76f, 10f), new Vector2(w - 90f, 30f), UiKit.TopLeft);
            TextMeshProUGUI t = UiKit.Text(face, "Small", small, FontRole.Body, 11.5f, ColorRole.Ink, TextAlignmentOptions.TopLeft, false);
            UiKit.Place(t.rectTransform, new Vector2(76f, 42f), new Vector2(w - 90f, 18f), UiKit.TopLeft);
            var press = root.gameObject.AddComponent<PressTarget>();
            press.body = body;
            press.squash = new Vector2(0.95f, 0.9f);   // screens.css .hubcard:active
            press.transition = Selectable.Transition.None;
            press.targetGraphic = hit;
            press.Clicked += open;
            press.Focused += p => ScrollTo(at.y + h * 0.5f, true);
        }

        /// <summary>
        /// index.html:32, the footer under every web page: the "manager's office" link (task 5.2). Here it closes the map
        /// page, under the hub cards, so it never covers anything in either theme or size.
        /// </summary>
        float BuildFooter(float x0, float y, float mw)
        {
            var link = WorldTour.Game.Office.OfficeButton.Create(_content, "OfficeLink", WorldTour.Game.Office.OfficeText.Link);
            float w = link.GetComponent<LayoutElement>().preferredWidth;
            UiKit.Place((RectTransform)link.transform, new Vector2(x0 + mw * 0.5f, y + Tokens.Tap * 0.5f), new Vector2(w, Tokens.Tap), UiKit.Center);
            link.Clicked += OpenOffice;
            return y + Tokens.Tap;
        }

        void OpenOffice() { if (Stack != null && Stack.Top == this) { Debug.Log("WT_OFFICE_LINK"); WorldTour.Game.Office.OfficeScreen.Open(Stack); } }

        void OpenBackstage() { if (Stack != null) BackstageScreen.Open(Stack); }

        void OpenJournal() { if (Stack != null) JournalScreen.Open(Stack); }

        // ------------------------------------------------------------------ navigation

        void OpenVenue(string id)
        {
            if (Stack == null || venueCardPrefab == null) { Debug.LogWarning("[map] no venue card prefab"); return; }
            VenueCard card = Stack.Push(venueCardPrefab);
            card.Open(id);
        }
    }
}
