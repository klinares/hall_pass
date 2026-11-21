using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WorldTour.Core.Data;
using WorldTour.Core.Maths;
using WorldTour.Core.Progress;
using WorldTour.Game.Data;
using WorldTour.Game.Modes;
using WorldTour.Game.Ui;
using WorldTour.Game.Util;

namespace WorldTour.Game.Map
{
    /// <summary>
    /// The venue card (port of world_tour/screens/venue.js): the poster (art, "stop 2 of 8", name, blurb, and for a
    /// locked venue how many fans it needs), then for an open venue the week picker ("tour date", defaulting to
    /// WeekRules.DefaultWeek and remembered for the session) and the spelling sets that the Unity build can play
    /// (remix for now; the web game has no copy for modes that aren't ready, so they are left off). Picking a set
    /// raises <see cref="StartShowRequested"/> for the show host (task 2.5). A locked venue offers its "play at ...
    /// instead" button. Logs "WT_VENUE_OPEN id=..." when opened and "WT_SHOW_REQUEST venue=... week=... mode=..."
    /// when a set is picked. Esc goes back to the map.
    /// </summary>
    public sealed class VenueCard : GameScreen
    {
        public const string OpenLog = "WT_VENUE_OPEN";
        public const string ShowRequestLog = "WT_SHOW_REQUEST";

        [SerializeField] MapArt art;
        [SerializeField] StickerButton buttonPrefab;

        /// <summary>(venueId, weekId, modeId): start this show. Task 2.5's show host subscribes.</summary>
        public static event Action<string, string, string> StartShowRequested;

        /// <summary>venue.js:13 <c>chosenWeek</c>: the week picked on any card, remembered for the session.</summary>
        static string s_ChosenWeek;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_ChosenWeek = null; StartShowRequested = null; }

        /// <summary>The rows come first in order, then the sound toggle (polish 6.2B item 9).</summary>
        public override bool HudControlsLast { get { return true; } }

        /// <summary>The venue shown.</summary>
        public string VenueId { get; private set; }

        RectTransform _content;
        ScrollRect _scroll;
        float _builtWidth = -1f;

        /// <summary>Shows <paramref name="venueId"/> (an unknown id shows the first venue, as venueById does).</summary>
        public void Open(string venueId)
        {
            Venue v = Venues.ById(venueId);
            VenueId = v.Id;
            Build();
            Debug.Log(OpenLog + " id=" + v.Id);
        }

        void Update()
        {
            float w = Rect.rect.width;
            if (VenueId != null && _builtWidth > 0f && Mathf.Abs(w - _builtWidth) > 1f) Build();
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && Stack != null && Stack.Top == this) Stack.Pop();
        }

        void Frame()
        {
            if (_content != null) return;
            RectTransform scrollRt = UiKit.Stretch(UiKit.Rect("Scroll", transform));
            Image hit = scrollRt.gameObject.AddComponent<Image>();
            hit.color = new Color(1, 1, 1, 0);
            _scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 40f;
            RectTransform viewport = UiKit.Stretch(UiKit.Rect("Viewport", scrollRt));
            viewport.gameObject.AddComponent<RectMask2D>();
            _scroll.viewport = viewport;
            _content = UiKit.Rect("Content", viewport);
            _content.anchorMin = new Vector2(0f, 1f); _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _scroll.content = _content;
        }

        void Build()
        {
            Frame();
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                GameObject c = _content.GetChild(i).gameObject;
                c.SetActive(false);
                Destroy(c);
            }
            FirstSelected = null;

            SaveBlob s = MapScreen.Blob;
            Venue v = Venues.ById(VenueId);
            bool open = ProgressRules.Unlocked(s, v.Id);
            int idx = MapLayout.IndexOf(v.Id);

            float vw = Rect.rect.width > 1f ? Rect.rect.width : 1366f;
            _builtWidth = Rect.rect.width > 1f ? vw : -1f;
            float cw = Mathf.Min(vw - 2f * Tokens.Gutter, 760f);   // screens.css:90 .venuepage max-width 760
            float x0 = (vw - cw) * 0.5f;
            float y = 24f;

            // back link (venue.js:26)
            StickerButton back = Button("Back", MapText.BackToMap, StickerButton.Style.Ghost, Tokens.FontButtonSm, new Vector2(x0, y), out float bh);
            if (back != null) { back.OnClick.AddListener(() => { if (Stack != null) Stack.Pop(); }); FirstSelected = back; }
            y += bh + 22f;

            y = Poster(v, idx, open, s.Fans.Lifetime, x0, y, cw);

            if (open)
            {
                IReadOnlyList<Week> all = DataLoader.Weeks != null && DataLoader.Weeks.File != null && DataLoader.Weeks.File.weeks != null
                    ? DataLoader.Weeks.File.weeks : (IReadOnlyList<Week>)new Week[0];
                Week wk = WeekRules.ChooseWeek(all, s_ChosenWeek, LocalClock.TodayIso());
                if (wk != null) y = Sets(v, wk, all, x0, y + 30f, cw);
                else Debug.LogWarning("[map] no weeks loaded: the spelling sets are hidden");
                y = MathSets(v, wk, x0, y, cw);
            }
            else
            {
                Venue newest = ProgressRules.NewestVenue(s);
                StickerButton instead = Button("Instead", MapText.PlayInstead(newest), StickerButton.Style.Ink, Tokens.FontButton, new Vector2(x0, y + 24f), out float ih);
                if (instead != null) { string nid = newest.Id; instead.OnClick.AddListener(() => Open(nid)); FirstSelected = instead; }
                y += 24f + ih;
            }
            _content.sizeDelta = new Vector2(0f, y + 48f);
            _scroll.verticalNormalizedPosition = 1f;
            if (FirstSelected != null && UnityEngine.EventSystems.EventSystem.current != null && Stack != null && Stack.Top == this)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(FirstSelected.gameObject);
        }

        StickerButton Button(string name, string text, StickerButton.Style style, float fontSize, Vector2 topLeft, out float height)
        {
            height = Tokens.ButtonMinHeight;
            if (buttonPrefab == null) return null;
            StickerButton b = Instantiate(buttonPrefab, _content, false);
            b.name = name;
            b.ButtonStyle = style;
            b.Text = text;
            TMP_Text label = b.GetComponentInChildren<TMP_Text>();
            if (label != null) label.fontSize = fontSize;
            float w = (label != null ? UiKit.WidthOf(label) : 200f) + 2f * Tokens.ButtonPadX;
            UiKit.Place((RectTransform)b.transform, topLeft + new Vector2(w * 0.5f, height * 0.5f), new Vector2(w, height), UiKit.Center);
            return b;
        }

        float Poster(Venue v, int idx, bool open, long lifetime, float x0, float y, float cw)
        {
            const float padX = 22f, padY = 30f, gap = 40f;   // polish 6.2E item 7: 40 (was 18): the sticker's outline and shadow reach past its box, and touched the title
            float inner = cw - 2f * padX;
            float artW = Mathf.Max(120f, inner * 0.38f), copyW = inner - artW - gap;
            RectTransform poster = UiKit.Place(UiKit.Rect("Poster", _content), new Vector2(x0, y), new Vector2(cw, 100f), UiKit.TopLeft);

            Image shadow = UiKit.Box(poster, "Shadow", ColorRole.Ink);
            Sprite torn = art != null ? art.Get("paper_torn") : null;
            Image paper = torn != null ? UiKit.Img(poster, "Paper", torn, Color.white) : UiKit.Box(poster, "Paper", ColorRole.Card);
            if (torn != null) paper.type = Image.Type.Tiled;

            // copy column (venue.js:31-36)
            float cy = 0f;
            var copy = new List<KeyValuePair<RectTransform, float>>();
            TextMeshProUGUI eyebrow = UiKit.Text(poster, "Eyebrow", MapText.StopOf(idx, Venues.All.Count, open), FontRole.BodyBold, Tokens.Fs0, ColorRole.Ink);
            Add(copy, eyebrow, copyW, ref cy, 6f);
            TextMeshProUGUI name = UiKit.Text(poster, "Name", v.Name, FontRole.Display, Tokens.Fs4, ColorRole.Ink);
            name.lineSpacing = -12f;
            Add(copy, name, copyW, ref cy, 10f);
            TextMeshProUGUI blurb = UiKit.Text(poster, "Blurb", v.Blurb, FontRole.Body, Tokens.FontBody, ColorRole.Ink);
            Add(copy, blurb, copyW, ref cy, 0f);
            if (!open)
            {
                cy += 10f;
                Sprite lockIcon = art != null ? art.Get("icon_lock") : null;
                float iconW = lockIcon != null ? 26f : 0f;
                TextMeshProUGUI need = UiKit.Text(poster, "Need", MapText.Need(v, lifetime), FontRole.BodyBold, Tokens.FontBody, ColorRole.Ink);
                float nh = UiKit.HeightFor(need, copyW - iconW);
                copy.Add(new KeyValuePair<RectTransform, float>(need.rectTransform, cy));
                need.rectTransform.sizeDelta = new Vector2(copyW - iconW, nh);
                if (lockIcon != null)
                {
                    Image li = UiKit.Img(poster, "Lock", lockIcon, Theme.Color(ColorRole.Ink));
                    li.preserveAspect = true;
                    copy.Add(new KeyValuePair<RectTransform, float>(li.rectTransform, cy));
                    li.rectTransform.sizeDelta = new Vector2(20f, 20f);
                }
                cy += nh;
            }

            SpriteBox box = art != null ? art.Box("venue_" + v.Art) : new SpriteBox { W = 1, H = 1, BoxW = 1, BoxH = 1 };
            float artH = box.BoxHeightFor(artW);
            float innerH = Mathf.Max(artH, cy);
            float h = innerH + 2f * padY;
            poster.sizeDelta = new Vector2(cw, h);
            UiKit.Stretch(shadow.rectTransform); shadow.rectTransform.anchoredPosition = new Vector2(Tokens.Shadow, -Tokens.Shadow);
            UiKit.Stretch(paper.rectTransform);

            Sprite sp = art != null ? art.Venue(v) : null;
            if (sp != null)
            {
                Image a = UiKit.Sticker(poster, "Art", sp, box, new Vector2(padX, padY + (innerH - artH) * 0.5f), artW);
                Greyscale.Set(a, !open, GreyStyle.Locked);   // screens.css:94 .poster.locked .vart
            }
            float copyX = padX + artW + gap, copyTop = padY + (innerH - cy) * 0.5f;
            foreach (KeyValuePair<RectTransform, float> kv in copy)
            {
                RectTransform rt = kv.Key;
                bool isLock = rt.name == "Lock";
                bool isNeed = rt.name == "Need";
                float x = copyX + (isNeed && art != null && art.Get("icon_lock") != null ? 26f : 0f);
                UiKit.Place(rt, new Vector2(x, copyTop + kv.Value + (isLock ? 2f : 0f)), rt.sizeDelta, UiKit.TopLeft);
            }

            Sprite tape = art != null ? art.Get("tape_a") : null;
            if (tape != null)
            {
                Image t = UiKit.Img(poster, "Tape", tape, Color.white);
                UiKit.Place(t.rectTransform, new Vector2(cw * 0.5f, -12f + 13f), new Vector2(96f, 26f), UiKit.Center);
                t.rectTransform.localEulerAngles = new Vector3(0f, 0f, MapLayout.UnityTilt(-3f));   // paper.css:48 .tape.tc
            }
            return y + h;
        }

        static void Add(List<KeyValuePair<RectTransform, float>> list, TMP_Text t, float width, ref float y, float after)
        {
            float h = UiKit.HeightFor(t, width);
            t.rectTransform.sizeDelta = new Vector2(width, h);
            list.Add(new KeyValuePair<RectTransform, float>(t.rectTransform, y));
            y += h + after;
        }

        float Sets(Venue v, Week wk, IReadOnlyList<Week> all, float x0, float y, float cw)
        {
            // section head with the week picker on the right (venue.js:41-46)
            TextMeshProUGUI head = UiKit.Text(_content, "SetsHeading", MapText.SpellingSets, FontRole.Display, Tokens.Fs3, ColorRole.Ink,
                TextAlignmentOptions.BottomLeft, false);
            float headW = UiKit.WidthOf(head);

            List<Week> weeks = WeekRules.SortedNewestFirst(all);
            TextMeshProUGUI pickLabel = UiKit.Text(_content, "TourDate", MapText.TourDate, FontRole.BodyBold, Tokens.Fs0, ColorRole.Ink, TextAlignmentOptions.TopLeft, false);
            float chipH = Tokens.Tap, chipGap = 10f;
            var chips = new List<StickerButton>();
            var widths = new List<float>();
            foreach (Week w in weeks)
            {
                StickerButton chip = Button("Week_" + w.id, w.label, w.id == wk.id ? StickerButton.Style.Ink : StickerButton.Style.Ghost, 18f, Vector2.zero, out _);
                if (chip == null) break;
                RectTransform crt = (RectTransform)chip.transform;
                crt.sizeDelta = new Vector2(crt.sizeDelta.x, chipH);
                string id = w.id;
                chip.OnClick.AddListener(() => { s_ChosenWeek = id; Build(); });
                chips.Add(chip);
                widths.Add(crt.sizeDelta.x);
            }
            // chips flow right-aligned, wrapping onto more rows when they don't fit beside the heading
            float maxRow = Mathf.Max(200f, cw - headW - 24f);
            var rows = new List<List<int>> { new List<int>() };
            float rowW = 0f;
            for (int i = 0; i < chips.Count; i++)
            {
                float add = widths[i] + (rows[rows.Count - 1].Count > 0 ? chipGap : 0f);
                if (rowW + add > maxRow && rows[rows.Count - 1].Count > 0) { rows.Add(new List<int>()); rowW = 0f; add = widths[i]; }
                rows[rows.Count - 1].Add(i);
                rowW += add;
            }
            float labelH = 18f, pickH = labelH + 4f + rows.Count * chipH + (rows.Count - 1) * chipGap;
            float blockH = Mathf.Max(pickH, 44f);
            float right = x0 + cw;
            float widest = 0f;
            for (int r = 0; r < rows.Count; r++)
            {
                float total = 0f;
                foreach (int i in rows[r]) total += widths[i];
                total += Mathf.Max(0, rows[r].Count - 1) * chipGap;
                widest = Mathf.Max(widest, total);
                float cx = right - total, cyy = y + labelH + 4f + r * (chipH + chipGap);
                foreach (int i in rows[r])
                {
                    UiKit.Place((RectTransform)chips[i].transform, new Vector2(cx + widths[i] * 0.5f, cyy + chipH * 0.5f), new Vector2(widths[i], chipH), UiKit.Center);
                    cx += widths[i] + chipGap;
                }
            }
            UiKit.Place(pickLabel.rectTransform, new Vector2(right - widest, y), new Vector2(Mathf.Max(widest, 80f), labelH), UiKit.TopLeft);
            UiKit.Place(head.rectTransform, new Vector2(x0, y + blockH - 40f), new Vector2(headW + 4f, 40f), UiKit.TopLeft);
            y += blockH + 12f;

            // the sets: only what the Unity build can play and this week has words for (venue.js:21-22, :47-55)
            int n = 0;
            var hidden = new List<KeyValuePair<string, string>>();
            foreach (ModeEntry m in ModeCatalog.All)
            {
                if (!m.IsSpelling || !m.Playable) continue;
                if (!ModeRules.IsAvailable(m.Id, wk)) { hidden.Add(new KeyValuePair<string, string>(m.Id, m.Name)); continue; }
                y = ModeRow(v, wk, m, n, x0, y, cw) + 14f;
                n++;
            }
            // venue.js:57 the pencil note for the modes left off this week (screens.css .pencil was small grey italic; polish 6.2B item 11: full ink, upright, 14.4 px, readable at 1366)
            string note = ModeRules.BillNote(hidden);
            if (note != null)
            {
                TextMeshProUGUI pencil = UiKit.Text(_content, "BillNote", note.TrimEnd(), FontRole.Body, 14.4f, ColorRole.Ink);
                float w = Mathf.Min(cw, 52f * 8.7f);                       // max-width 52ch of Courier at 14.4 px
                float h = UiKit.HeightFor(pencil, w) + 4f;
                UiKit.Place(pencil.rectTransform, new Vector2(x0, y), new Vector2(w, h), UiKit.TopLeft);
                y += h + 14f;
            }
            return y;
        }

        /// <summary>
        /// venue.js:58-67 the math sets: a heading with "levels move with you", then a row per math mode the Unity build can
        /// play (and math_config.json has on), each with its level in stars. Left off entirely while none is playable.
        /// </summary>
        float MathSets(Venue v, Week wk, float x0, float y, float cw)
        {
            MathConfig config = DataLoader.Math != null ? DataLoader.Math.Config : null;
            var modes = new List<ModeEntry>();
            foreach (ModeEntry m in ModeCatalog.All)
                if (!m.IsSpelling && m.Playable && (config == null || config.IsEnabled(m.Info.Skill))) modes.Add(m);
            if (modes.Count == 0) return y;
            y += 30f;
            TextMeshProUGUI head = UiKit.Text(_content, "MathHeading", MapText.MathSets, FontRole.Display, Tokens.Fs3, ColorRole.Ink,
                TextAlignmentOptions.BottomLeft, false);
            float headW = UiKit.WidthOf(head);
            UiKit.Place(head.rectTransform, new Vector2(x0, y), new Vector2(headW + 4f, 40f), UiKit.TopLeft);
            TextMeshProUGUI pencil = UiKit.Text(_content, "MathNote", MapText.LevelsMove, FontRole.Body, 14.4f, ColorRole.Ink,
                TextAlignmentOptions.BottomLeft, false);
            UiKit.Place(pencil.rectTransform, new Vector2(x0 + headW + 14f, y + 10f), new Vector2(UiKit.WidthOf(pencil) + 4f, 26f), UiKit.TopLeft);
            y += 40f + 12f;
            SaveBlob s = MapScreen.Blob;
            for (int i = 0; i < modes.Count; i++)
            {
                int level = s != null ? ProgressRules.Level(s, modes[i].Info.Skill) : 1;
                y = ModeRow(v, wk, modes[i], i, x0, y, cw, level) + 14f;
            }
            return y;
        }

        float ModeRow(Venue v, Week wk, ModeEntry m, int i, float x0, float y, float cw, int mathLevel = 0)
        {
            bool math = mathLevel > 0;
            float h = math ? 100f : 84f;
            RectTransform root = UiKit.Place(UiKit.Rect("Mode_" + m.Id, _content), new Vector2(x0 + cw * 0.5f, y + h * 0.5f), new Vector2(cw, h), UiKit.Center);
            // venue.js:49 --r (spelling rows -1, 1, ...; math rows 1, -1, ..., venue.js:62)
            root.localEulerAngles = new Vector3(0f, 0f, MapLayout.UnityTilt((i % 2 == 1 ? 1f : -1f) * (math ? -1f : 1f)));
            Image hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(1, 1, 1, 0);
            RectTransform body = UiKit.Stretch(UiKit.Rect("Body", root));
            body.pivot = UiKit.Center;
            RectTransform face = UiKit.Card(body, Tokens.Shadow);

            float x = 12f - Tokens.BorderWidth;
            TextMeshProUGUI no = UiKit.Text(face, "Number", (i + 1).ToString("00"), FontRole.BodyBold, Tokens.Fs0, ColorRole.Ink, TextAlignmentOptions.Left, false);
            float nw = UiKit.WidthOf(no);
            UiKit.Place(no.rectTransform, new Vector2(x, (h - 2f * Tokens.BorderWidth) * 0.5f - 10f), new Vector2(nw + 2f, 20f), UiKit.TopLeft);
            x += nw + 12f;
            string sn = m.Sticker + "_sm";
            Sprite sp = art != null ? art.Get(sn) : null;
            if (sp != null) UiKit.Sticker(face, "Sticker", sp, art.Box(sn), new Vector2(x, (h - 2f * Tokens.BorderWidth - 64f) * 0.5f), 64f);
            x += 64f + 12f;
            const float arrowW = 24f;
            float textW = cw - x - 18f - arrowW - 12f;
            TextMeshProUGUI name = UiKit.Text(face, "Name", m.Name, FontRole.Display, 32f, ColorRole.Ink, TextAlignmentOptions.BottomLeft, false);
            UiKit.Place(name.rectTransform, new Vector2(x, 10f), new Vector2(textW, 36f), UiKit.TopLeft);
            TextMeshProUGUI sub = UiKit.Text(face, "Sub", m.Sub, FontRole.Body, 13.6f, ColorRole.Ink, TextAlignmentOptions.TopLeft, false);
            UiKit.Place(sub.rectTransform, new Vector2(x, 48f), new Vector2(textW, 20f), UiKit.TopLeft);
            if (math)
            {
                // .mlvl: the stars in accent-2 at 1rem, letter-spaced, then "level n" small in ink
                TextMeshProUGUI lvl = UiKit.Text(face, "Level", "", FontRole.Body, 16f, ColorRole.Accent2, TextAlignmentOptions.TopLeft, false);
                lvl.characterSpacing = 8f;
                lvl.text = MapText.LevelStars(mathLevel) + " <cspace=0><size=11.5><color=#" + ColorUtility.ToHtmlStringRGB(Theme.Color(ColorRole.Ink)) + ">level " + mathLevel + "</color></size></cspace>";
                UiKit.Place(lvl.rectTransform, new Vector2(x, 70f), new Vector2(textW, 20f), UiKit.TopLeft);
            }
            TextMeshProUGUI arrow = UiKit.Text(face, "Arrow", MapText.Arrow, FontRole.Body, 25.6f, ColorRole.Ink, TextAlignmentOptions.Center, false);
            UiKit.Place(arrow.rectTransform, new Vector2(cw - 2f * Tokens.BorderWidth - 18f - arrowW, (h - 2f * Tokens.BorderWidth) * 0.5f - 15f), new Vector2(arrowW, 30f), UiKit.TopLeft);

            var press = root.gameObject.AddComponent<PressTarget>();
            press.body = body;
            press.squash = new Vector2(0.98f, 0.94f);   // screens.css .moderow:active
            press.transition = Selectable.Transition.None;
            press.targetGraphic = hit;
            string vid = v.Id, wid = wk != null ? wk.id : null, mid = m.Id;
            press.Clicked += () => RequestShow(vid, wid, mid);
            if (i == 0 && (!math || !(FirstSelected is PressTarget))) FirstSelected = press;
            return y + h;
        }

        static void RequestShow(string venueId, string weekId, string modeId)
        {
            Debug.Log(ShowRequestLog + " venue=" + venueId + " week=" + weekId + " mode=" + modeId);
            if (StartShowRequested == null) { Debug.LogWarning("[map] no show host is listening yet (task 2.5)"); return; }
            StartShowRequested(venueId, weekId, modeId);
        }
    }
}
