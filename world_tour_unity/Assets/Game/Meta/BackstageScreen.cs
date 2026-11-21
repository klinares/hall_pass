using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;
using WorldTour.Game.Audio;
using WorldTour.Game.Map;
using WorldTour.Game.Mascot;
using WorldTour.Game.Motion;
using WorldTour.Game.Prefs;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Meta
{
    /// <summary>
    /// The backstage shop and locker (port of world_tour/screens/backstage.js). The wallet and the web's note that
    /// spending never lowers total fans; the mirror on the left (riff and a guitar wearing the selected item, its name and
    /// blurb, and one button: buy, tap again to buy, wear it, switch to this theme, see it in your journal, or how many
    /// more fans are needed); the four racks on the right (color themes, guitar skins, riff's outfits, journal stickers),
    /// each item with its tag (price, yours, wearing, on, in journal). Picking a theme tries it on: the whole game
    /// re-skins until she leaves or picks something else. The rules are <see cref="Backstage"/> (Core). Logs
    /// "WT_BACKSTAGE_READY", "WT_SHOP_SELECT id=..", "WT_SHOP_PRESS id=.. outcome=.. balance=.. lifetime=..".
    /// Esc goes back to the map. Opened from the map's hub card with <see cref="Open"/>.
    /// </summary>
    public sealed class BackstageScreen : GameScreen
    {
        public const string PrefabResource = "Screens/Screen_Backstage";
        public const string ReadyLog = "WT_BACKSTAGE_READY";
        public const string SelectLog = "WT_SHOP_SELECT";
        public const string PressLog = "WT_SHOP_PRESS";

        [SerializeField] MapArt art;
        [SerializeField] StickerButton buttonPrefab;
        [SerializeField] Material shout;

        Backstage _model;
        RectTransform _root, _racksContent, _mirror, _stageLook;
        ScrollRect _racks;
        float _rackScroll = 1f;
        float _builtW = -1f, _builtH = -1f;
        Coroutine _armTimer, _hits;
        readonly Dictionary<string, RectTransform> _items = new Dictionary<string, RectTransform>();
        readonly List<KeyValuePair<string, RectTransform>> _hitList = new List<KeyValuePair<string, RectTransform>>();
        StickerButton _act, _back;
        string _focusAfterBuild;
        bool _hidden;

        /// <summary>The rules behind the screen (for tests).</summary>
        public Backstage Model { get { return _model; } }

        /// <summary>Pushes the backstage screen over the current one (the map).</summary>
        public static BackstageScreen Open(ScreenStack stack)
        {
            BackstageScreen prefab = Resources.Load<BackstageScreen>(PrefabResource);
            GameObject template = null;
            if (prefab == null)
            {
                template = new GameObject("Screen_Backstage", typeof(RectTransform), typeof(CanvasGroup));
                template.SetActive(false);
                prefab = template.AddComponent<BackstageScreen>();
            }
            BackstageScreen s = stack.Push(prefab);
            if (template != null) Destroy(template);
            return s;
        }

        static SaveBlob Blob { get { return MapScreen.Blob; } }

        protected internal override void OnPushed()
        {
            _model = new Backstage(Blob);
            Theme.Changed += OnTheme;
            Build();
            Debug.Log(ReadyLog + " balance=" + Blob.Fans.Balance + " lifetime=" + Blob.Fans.Lifetime + " sel=" + _model.Selected + " theme=" + Theme.Palette.Id);
        }

        protected internal override void OnShown()
        {
            _hidden = false;
            if (_back != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_back.gameObject);
            LogHitsSoon();
        }

        protected internal override void OnHidden()
        {
            // backstage.js cleanup: applyLook(), so a theme she only tried on comes off as she leaves
            StopArm();
            _hidden = true;
            GameSettings.Apply();
        }

        protected internal override void OnRemoved()
        {
            Theme.Changed -= OnTheme;
        }

        void OnDestroy() { Theme.Changed -= OnTheme; }

        void Update()
        {
            if (_builtW > 0f && (Mathf.Abs(Rect.rect.width - _builtW) > 1f || Mathf.Abs(Rect.rect.height - _builtH) > 1f)) Build();
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && Stack != null && Stack.Top == this) Stack.Pop();
        }

        void OnTheme()
        {
            // the palette changed (a theme tried on, bought or switched): repaint everything from the new tokens
            GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            _focusAfterBuild = sel != null && sel.transform.IsChildOf(transform) ? sel.name : null;
            Build();
        }

        // ------------------------------------------------------------------ build

        void Build()
        {
            if (_racks != null) _rackScroll = _racks.verticalNormalizedPosition;
            if (_root != null) { _root.gameObject.SetActive(false); Destroy(_root.gameObject); }
            _items.Clear();
            _root = UiKit.Stretch(UiKit.Rect("Root", transform));

            float vw = Rect.rect.width > 1f ? Rect.rect.width : 1366f, vh = Rect.rect.height > 1f ? Rect.rect.height : 768f;
            _builtW = Rect.rect.width > 1f ? vw : -1f; _builtH = Rect.rect.height > 1f ? vh : -1f;
            float pw = Mathf.Min(vw - 2f * Tokens.Gutter, 1180f), x0 = (vw - pw) * 0.5f;
            SaveBlob s = Blob;

            // back link (backstage.js:29)
            _back = MetaUi.Button(buttonPrefab, _root, "Back", MapText.BackToMap, StickerButton.Style.Ghost, Tokens.FontButtonSm, new Vector2(x0, 20f));
            if (_back != null) _back.OnClick.AddListener(() => { if (Stack != null) Stack.Pop(); });

            // head: eyebrow and title on the left, the wallet on the right (backstage.js:30-34)
            TextMeshProUGUI eyebrow = UiKit.Text(_root, "Eyebrow", Backstage.Eyebrow, FontRole.BodyBold, Tokens.Fs0, ColorRole.Ink, TextAlignmentOptions.TopLeft, false);
            UiKit.Place(eyebrow.rectTransform, new Vector2(x0, 90f), new Vector2(500f, 18f), UiKit.TopLeft);
            TextMeshProUGUI title = MetaUi.Shout(_root, "Title", Backstage.Title, Tokens.Fs4, shout);
            UiKit.Place(title.rectTransform, new Vector2(x0, 108f), new Vector2(500f, 62f), UiKit.TopLeft);
            Wallet(s, x0 + pw, 104f);

            // backstage.js:35 the note
            TextMeshProUGUI note = UiKit.Text(_root, "Note", Backstage.Note(s.Fans.Lifetime), FontRole.Body, 13.6f, ColorRole.Ink, TextAlignmentOptions.TopLeft, false);
            UiKit.Place(note.rectTransform, new Vector2(x0, 180f), new Vector2(pw, 20f), UiKit.TopLeft);

            const float top = 212f, mirrorW = 360f, gap = 20f;
            float bottom = vh - 20f;
            BuildMirror(x0, top, mirrorW, bottom - top);
            BuildRacks(x0 + mirrorW + gap, top, pw - mirrorW - gap, bottom - top);

            if (_focusAfterBuild != null && EventSystem.current != null)
            {
                Transform t = FindDeep(_root, _focusAfterBuild);
                if (t != null) EventSystem.current.SetSelectedGameObject(t.gameObject);
                _focusAfterBuild = null;
            }
            FirstSelected = _back;
            ThemeSkin.Dirty();
            if (Stack != null && Stack.Top == this) LogHitsSoon();

            // backstage.js:80 a theme picked on the rack is tried on: the whole game wears it while it is selected, and
            // picking anything else puts her own theme back (applyLook). Switching repaints through OnTheme, once.
            string want = _model.Mirror().Theme;
            if (!_hidden && want != Theme.Palette.Id) Theme.Use(want);
        }

        void Wallet(SaveBlob s, float right, float y)
        {
            // screens.css .wallet: card, ink border, shadow-md, star 46, balance 1.5rem, "fans to spend" .75rem; tilt-2
            TextMeshProUGUI bal = UiKit.Text(_root, "Balance", Backstage.Fmt(s.Fans.Balance), FontRole.BodyBold, 24f, ColorRole.Ink, TextAlignmentOptions.BottomLeft, false);
            TextMeshProUGUI lbl = UiKit.Text(_root, "BalanceLabel", Backstage.FansToSpend, FontRole.Body, 12f, ColorRole.Ink, TextAlignmentOptions.TopLeft, false);
            float textW = Mathf.Max(UiKit.WidthOf(bal), UiKit.WidthOf(lbl)) + 4f;
            float w = 8f + 46f + 8f + textW + 16f, h = 62f;
            RectTransform card = UiKit.Place(UiKit.Rect("Wallet", _root), new Vector2(right - w * 0.5f - 6f, y + h * 0.5f), new Vector2(w, h), UiKit.Center);
            card.localEulerAngles = new Vector3(0f, 0f, MapLayout.UnityTilt(1.6f));
            RectTransform face = UiKit.Card(card, Tokens.ShadowMd);
            Sprite star = art != null ? art.Get("star_sm") : null;
            if (star != null) UiKit.Sticker(face, "Star", star, art.Box("star_sm"), new Vector2(8f - Tokens.BorderWidth, (h - 46f) * 0.5f - Tokens.BorderWidth), 46f);
            bal.rectTransform.SetParent(face, false);
            UiKit.Place(bal.rectTransform, new Vector2(62f - Tokens.BorderWidth, 6f), new Vector2(textW, 28f), UiKit.TopLeft);
            lbl.rectTransform.SetParent(face, false);
            UiKit.Place(lbl.rectTransform, new Vector2(62f - Tokens.BorderWidth, 34f), new Vector2(textW, 16f), UiKit.TopLeft);
        }

        // ------------------------------------------------------------------ the mirror (backstage.js:73-97)

        void BuildMirror(float x, float y, float w, float maxH)
        {
            Mirror m = _model.Mirror();
            ShopItem it = _model.Item;
            _mirror = UiKit.Place(UiKit.Rect("Mirror", _root), new Vector2(x, y), new Vector2(w, 100f), UiKit.TopLeft);
            Image shadow = UiKit.Box(_mirror, "Shadow", ColorRole.Ink);
            Sprite torn = art != null ? art.Get("paper_torn") : null;
            Image paper = torn != null ? UiKit.Img(_mirror, "Paper", torn, Color.white) : UiKit.Box(_mirror, "Paper", ColorRole.Card);
            if (torn != null) paper.type = Image.Type.Tiled;

            const float padX = 18f, padTop = 18f, padBottom = 26f;
            float inner = w - 2f * padX, cy = padTop;
            TextMeshProUGUI eb = UiKit.Text(_mirror, "Eyebrow", m.Eyebrow, FontRole.BodyBold, Tokens.Fs0, ColorRole.Ink, TextAlignmentOptions.Center, false);
            UiKit.Place(eb.rectTransform, new Vector2(padX, cy), new Vector2(inner, 18f), UiKit.TopLeft);
            cy += 18f + 6f;

            // the stage: riff (190) beside the guitar or the sticker (120), bottom-aligned (screens.css .stage-look)
            const float dogW = 190f, gtrW = 120f, lookH = 190f;
            _stageLook = UiKit.Place(UiKit.Rect("StageLook", _mirror), new Vector2(w * 0.5f, cy + lookH * 0.5f), new Vector2(dogW + 4f + gtrW, lookH), UiKit.Center);
            RectTransform dogBox = UiKit.Place(UiKit.Rect("MirrorDog", _stageLook), new Vector2(0f, 0f), new Vector2(dogW, dogW), UiKit.TopLeft);
            MetaUi.Dog(dogBox, m.Outfit);
            string sn = m.Sticker != null ? m.Sticker : LookArt.StickerName("guitar", false, LookArt.SkinOf(m.Guitar));
            Sprite sp = art != null ? art.GetExact(sn) : null;
            if (sp != null) UiKit.Sticker(_stageLook, m.Sticker != null ? "MirrorSticker" : "MirrorGuitar", sp, art.Box(sn), new Vector2(dogW + 4f, lookH - gtrW), gtrW);
            cy += lookH + 6f;

            TextMeshProUGUI name = UiKit.Text(_mirror, "Name", m.Name, FontRole.Display, Tokens.Fs3, ColorRole.Ink, TextAlignmentOptions.Center, false);
            UiKit.Place(name.rectTransform, new Vector2(padX, cy), new Vector2(inner, 38f), UiKit.TopLeft);
            cy += 38f + 6f;
            TextMeshProUGUI blurb = UiKit.Text(_mirror, "Blurb", m.Blurb, FontRole.Body, 15.3f, ColorRole.Ink, TextAlignmentOptions.Top);
            float bh = Mathf.Max(40f, UiKit.HeightFor(blurb, inner));   // .blurb min-height 2.6em
            UiKit.Place(blurb.rectTransform, new Vector2(padX, cy), new Vector2(inner, bh), UiKit.TopLeft);
            cy += bh + 10f;

            StickerButton.Style style = m.Does == MirrorAction.Journal ? StickerButton.Style.Ghost : StickerButton.Style.Ink;
            _act = MetaUi.Button(buttonPrefab, _mirror, "Act", m.Action, style, m.Action.Length > 18 ? Tokens.FontButtonSm : Tokens.FontButton,
                new Vector2(padX, cy), Tokens.ButtonMinHeight, inner);
            if (_act != null)
            {
                _act.DisabledAlpha = 0.55f;                 // screens.css .mirror .btn[disabled]{opacity:.55}
                _act.interactable = m.Does != MirrorAction.None;
                _act.PlaySound = false;          // backstage.js plays its own sound per outcome
                _act.OnClick.AddListener(Press);
            }
            cy += Tokens.ButtonMinHeight + padBottom;
            float h = Mathf.Min(cy, maxH);
            _mirror.sizeDelta = new Vector2(w, h);
            UiKit.Stretch(shadow.rectTransform); shadow.rectTransform.anchoredPosition = new Vector2(Tokens.Shadow, -Tokens.Shadow);
            UiKit.Stretch(paper.rectTransform);
            Sprite tape = art != null ? art.Get("tape_a") : null;
            if (tape != null)
            {
                Image t = UiKit.Img(_mirror, "Tape", tape, Color.white);
                UiKit.Place(t.rectTransform, new Vector2(14f + 48f, -10f + 13f), new Vector2(96f, 26f), UiKit.Center);
                t.rectTransform.localEulerAngles = new Vector3(0f, 0f, MapLayout.UnityTilt(-8f));   // .tape.tl
            }
        }

        // ------------------------------------------------------------------ racks (backstage.js:39-53)

        void BuildRacks(float x, float y, float w, float h)
        {
            RectTransform scrollRt = UiKit.Place(UiKit.Rect("Racks", _root), new Vector2(x, y), new Vector2(w, h), UiKit.TopLeft);
            Image hit = scrollRt.gameObject.AddComponent<Image>();
            hit.color = new Color(1, 1, 1, 0);
            _racks = scrollRt.gameObject.AddComponent<ScrollRect>();
            _racks.horizontal = false;
            _racks.movementType = ScrollRect.MovementType.Clamped;
            _racks.scrollSensitivity = 40f;
            RectTransform viewport = UiKit.Stretch(UiKit.Rect("Viewport", scrollRt));
            viewport.gameObject.AddComponent<RectMask2D>();
            _racks.viewport = viewport;
            _racksContent = UiKit.Rect("Content", viewport);
            _racksContent.anchorMin = new Vector2(0f, 1f); _racksContent.anchorMax = new Vector2(1f, 1f);
            _racksContent.pivot = new Vector2(0.5f, 1f);
            _racks.content = _racksContent;

            const float gap = 10f, itemH = 150f, inset = 8f;   // inset: room for the selection outline and shadows
            float iw = w - 2f * inset;
            int cols = Mathf.Max(1, Mathf.FloorToInt((iw + gap) / (100f + gap)));
            float cw = (iw - (cols - 1) * gap) / cols;
            float cy = 6f;
            foreach (ShopSlot slot in Shop.Slots)
            {
                TextMeshProUGUI head = UiKit.Text(_racksContent, "Rack_" + slot.Id, slot.Name, FontRole.Display, Tokens.Fs3, ColorRole.Ink, TextAlignmentOptions.BottomLeft, false);
                float hw = UiKit.WidthOf(head);
                UiKit.Place(head.rectTransform, new Vector2(inset, cy), new Vector2(hw + 4f, 38f), UiKit.TopLeft);
                TextMeshProUGUI small = UiKit.Text(_racksContent, "RackNote_" + slot.Id, slot.Note, FontRole.Body, 12f, ColorRole.Ink, TextAlignmentOptions.BottomLeft, false);
                UiKit.Place(small.rectTransform, new Vector2(inset + hw + 10f, cy), new Vector2(iw - hw - 10f, 34f), UiKit.TopLeft);
                cy += 38f + 8f;
                int n = 0;
                foreach (ShopItem it in Shop.All)
                {
                    if (it.Slot != slot.Id) continue;
                    int r = n / cols, c = n % cols;
                    Item(it, new Vector2(inset + c * (cw + gap), cy + r * (itemH + gap)), cw, itemH);
                    n++;
                }
                cy += ((n + cols - 1) / cols) * (itemH + gap) + 14f;
            }
            _racksContent.sizeDelta = new Vector2(0f, cy + 12f);
            _racks.verticalNormalizedPosition = _rackScroll;
        }

        void Item(ShopItem it, Vector2 at, float w, float h)
        {
            ItemTag tag = _model.Tag(it);
            RectTransform root = UiKit.Place(UiKit.Rect("Item_" + it.Id, _racksContent), at + new Vector2(w * 0.5f, h * 0.5f), new Vector2(w, h), UiKit.Center);
            Image hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(1, 1, 1, 0);
            if (tag.Pressed) MetaUi.Outline(root, "Selected", new Vector2(w + 12f, h + 12f), ColorRole.Accent2, 4f);   // aria-pressed outline
            RectTransform body = UiKit.Stretch(UiKit.Rect("Body", root));
            body.pivot = UiKit.Center;
            RectTransform face = UiKit.Card(body, Tokens.ShadowSm);
            float fw = w - 2f * Tokens.BorderWidth;

            // thumb, 72 square (screens.css .item .thumb); faded when she can't afford it yet
            RectTransform thumb = UiKit.Place(UiKit.Rect("Thumb", face), new Vector2(fw * 0.5f, 10f + 36f - Tokens.BorderWidth), new Vector2(72f, 72f), UiKit.Center);
            var tg = thumb.gameObject.AddComponent<CanvasGroup>();
            tg.blocksRaycasts = false;
            if (tag.Short) tg.alpha = 0.6f;
            Thumb(it, thumb);

            TextMeshProUGUI name = UiKit.Text(face, "Name", it.Name, FontRole.BodyBold, 13.6f, ColorRole.Ink, TextAlignmentOptions.Top);
            UiKit.Place(name.rectTransform, new Vector2(4f, 10f + 72f + 4f - Tokens.BorderWidth), new Vector2(fw - 8f, 34f), UiKit.TopLeft);

            // the tag pill (screens.css .item .tag; own = accent, wearing = ink)
            ColorRole bg = tag.Wearing ? ColorRole.Ink : tag.Own ? ColorRole.Accent : ColorRole.Paper;
            ColorRole fg = tag.Wearing ? ColorRole.OnInk : ColorRole.Ink;
            TextMeshProUGUI tt = UiKit.Text(face, "TagText", tag.Tag, FontRole.Body, 12f, fg, TextAlignmentOptions.Center, false);
            float tw = UiKit.WidthOf(tt) + 18f;
            RectTransform pill = UiKit.Place(UiKit.Rect("Tag", face), new Vector2(fw * 0.5f, h - 2f * Tokens.BorderWidth - 8f - 11f), new Vector2(tw, 22f), UiKit.Center);
            Image ring = UiKit.Box(pill, "Ring", ColorRole.Ink); UiKit.Stretch(ring.rectTransform);
            Image fill = UiKit.Box(pill, "Fill", bg); UiKit.Stretch(fill.rectTransform, 2f);
            tt.rectTransform.SetParent(pill, false); UiKit.Stretch(tt.rectTransform);

            var press = root.gameObject.AddComponent<PressTarget>();
            press.body = body;
            press.squash = new Vector2(0.94f, 0.9f);   // screens.css .item:active
            press.transition = Selectable.Transition.None;
            press.targetGraphic = hit;
            string id = it.Id;
            press.Clicked += () => Pick(id);
            press.Focused += p => ScrollRackTo(root);
            _items[id] = root;
        }

        void Thumb(ShopItem it, RectTransform thumb)
        {
            if (it.Slot == Shop.Theme)
            {
                // screens.css .swatch: 62 square, ink border, the theme's four colours in a 2x2 grid, rotate(-4deg)
                RectTransform sw = UiKit.Place(UiKit.Rect("Swatch", thumb), new Vector2(36f, 36f), new Vector2(62f, 62f), UiKit.Center);
                sw.localEulerAngles = new Vector3(0f, 0f, MapLayout.UnityTilt(-4f));
                Image ring = UiKit.Box(sw, "Ring", ColorRole.Ink); UiKit.Stretch(ring.rectTransform);
                float cell = (62f - 2f * Tokens.BorderWidth) * 0.5f;
                for (int i = 0; i < 4 && it.Swatch != null && i < it.Swatch.Count; i++)
                {
                    Image q = UiKit.Img(sw, "C" + i, null, Theme.ToColor(Rgba32.Parse(it.Swatch[i])));
                    UiKit.Place(q.rectTransform, new Vector2(Tokens.BorderWidth + (i % 2) * cell, Tokens.BorderWidth + (i / 2) * cell), new Vector2(cell, cell), UiKit.TopLeft);
                }
                return;
            }
            if (it.Slot == Shop.Dog)
            {
                MetaUi.Dog(thumb, it.Id == "dog:none" ? "" : it.Id.Substring(4));
                return;
            }
            string sn = it.Slot == Shop.Guitar ? LookArt.StickerName("guitar", true, LookArt.SkinOf(it.Id)) : it.Sticker + "_sm";
            Sprite sp = art != null ? art.GetExact(sn) : null;
            if (sp != null) UiKit.Sticker(thumb, "Sticker", sp, art.Box(sn), new Vector2(3f, 3f), 66f);
        }

        void ScrollRackTo(RectTransform item)
        {
            if (_racks == null || _racksContent == null) return;
            RectTransform view = _racks.viewport;
            float vh = view.rect.height, ch = _racksContent.rect.height;
            if (ch <= vh) return;
            float itemTop = -item.anchoredPosition.y - item.sizeDelta.y * 0.5f;
            float itemBottom = itemTop + item.sizeDelta.y;
            float scrolled = _racksContent.anchoredPosition.y;
            if (itemTop - 16f < scrolled) scrolled = itemTop - 16f;
            else if (itemBottom + 16f > scrolled + vh) scrolled = itemBottom + 16f - vh;
            _racksContent.anchoredPosition = new Vector2(_racksContent.anchoredPosition.x, Mathf.Clamp(scrolled, 0f, ch - vh));
        }

        // ------------------------------------------------------------------ actions

        void Pick(string id)
        {
            // backstage.js:125-129 (the tap sound is the item's own)
            _model.Select(id);
            if (_model.Armed == null) StopArm();
            Debug.Log(SelectLog + " id=" + id);
            _focusAfterBuild = "Item_" + id;
            Build();
        }

        void Press()
        {
            ShopItem it = _model.Item;
            PressOutcome r = _model.Press();
            SaveBlob s = Blob;
            Debug.Log(PressLog + " id=" + it.Id + " outcome=" + r.ToString().ToLowerInvariant() + " balance=" + s.Fans.Balance + " lifetime=" + s.Fans.Lifetime);
            switch (r)
            {
                case PressOutcome.Armed:
                    Sfx.Play(SfxId.Tap);
                    StopArm();
                    _armTimer = StartCoroutine(ArmTimeout());
                    _focusAfterBuild = "Act";
                    Build();
                    break;
                case PressOutcome.Bought:
                    StopArm();
                    MapScreen.Persist();          // progress.js buyItem: saveNow
                    Sfx.Play(SfxId.StrumBig);
                    _focusAfterBuild = "Act";
                    GameSettings.Apply();          // applyLook(): a new theme re-skins everything (Build runs on Theme.Changed)
                    Build();
                    Feedback.Pop(_stageLook);
                    MetaUi.Toast(this, _root, Backstage.BoughtToast(it), shout);
                    break;
                case PressOutcome.Equipped:
                    MapScreen.Persist();
                    Sfx.Play(SfxId.Pop);
                    _focusAfterBuild = "Act";
                    GameSettings.Apply();
                    Build();
                    Feedback.Pop(_stageLook);
                    break;
                case PressOutcome.Refused:
                    Sfx.Play(SfxId.Thud);
                    Build();
                    Feedback.Wobble(_mirror);
                    break;
                case PressOutcome.OpenJournal:
                    Sfx.Play(SfxId.Tap);
                    if (Stack != null) JournalScreen.Open(Stack, replaceTop: true);
                    break;
            }
        }

        IEnumerator ArmTimeout()
        {
            yield return new WaitForSecondsRealtime(Backstage.ArmSeconds);
            _armTimer = null;
            _model.Disarm();
            _focusAfterBuild = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null ? EventSystem.current.currentSelectedGameObject.name : null;
            Build();
        }

        void StopArm()
        {
            if (_armTimer != null) { StopCoroutine(_armTimer); _armTimer = null; }
        }

        // ------------------------------------------------------------------ test hooks

        void LogHitsSoon()
        {
            if (_hits != null) StopCoroutine(_hits);
            _hits = StartCoroutine(LogHitsLater());
        }

        IEnumerator LogHitsLater()
        {
            yield return new WaitForSecondsRealtime(0.4f);   // after the slide-in
            _hits = null;
            _hitList.Clear();
            if (_back != null) _hitList.Add(new KeyValuePair<string, RectTransform>("back", (RectTransform)_back.transform));
            if (_act != null) _hitList.Add(new KeyValuePair<string, RectTransform>("act", (RectTransform)_act.transform));
            foreach (var kv in _items) _hitList.Add(new KeyValuePair<string, RectTransform>(kv.Key, kv.Value));
            MetaUi.LogHits("backstage", _hitList);
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }
    }
}
