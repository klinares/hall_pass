using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;
using WorldTour.Game.Map;
using WorldTour.Game.Show;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Meta
{
    /// <summary>
    /// Phase 8.3: the backstage shop as the compact panel of the 3D world (the 2D game never opens it this way). Same rules and the same two-tap buy
    /// (<see cref="Backstage"/>), laid out in the docked panel: back and wallet on top, five tabs (outfits, hair, guitar, riff, stage lights), the selected
    /// item with its button, and the shelf's items. The try-on is live in the world beside the panel: <see cref="TryOnChanged"/> fires with the selected item's
    /// id every time the screen is built, and with null when it is removed (the host puts the saved look back). Logs "WT_WARDROBE_READY".
    /// </summary>
    public sealed partial class BackstageScreen
    {
        public const string CompactReadyLog = "WT_WARDROBE_READY";

        /// <summary>The selected item's id after every change (a try-on or a bought item), and null when the shop closes. Set by the world host only.</summary>
        public static event Action<string> TryOnChanged;

        static bool s_NextCompact;
        static string s_StartSelect;

        bool _compact;
        string _tab = Shop.Outfit;
        readonly Dictionary<string, RectTransform> _tabButtons = new Dictionary<string, RectTransform>();

        static readonly string[] TabSlots = { Shop.Outfit, Shop.Hair, Shop.Guitar, Shop.Dog, Shop.Lights };
        static readonly string[] TabLabels = { "outfits", "hair", "guitar", "riff", "lights" };

        /// <summary>Pushes the compact shop (call with the world's compact panel open). <paramref name="select"/> starts on an item id, or on her worn outfit when null.</summary>
        public static BackstageScreen OpenCompact(ScreenStack stack, string select = null)
        {
            s_NextCompact = true;
            s_StartSelect = select != null && Shop.ById(select) != null ? select : null;
            return Open(stack);
        }

        public bool IsCompact { get { return _compact; } }

        static string TabOf(string id)
        {
            ShopItem it = Shop.ById(id);
            if (it != null) foreach (string s in TabSlots) if (s == it.Slot) return s;
            return Shop.Outfit;
        }

        void AddTabHits(List<KeyValuePair<string, RectTransform>> list)
        {
            foreach (var kv in _tabButtons) list.Add(new KeyValuePair<string, RectTransform>("tab_" + kv.Key, kv.Value));
        }

        void SelectTab(string slot)
        {
            if (_tab == slot) return;
            _tab = slot;
            ShopItem worn = MetaRules.Pick(Blob, slot);
            _model.Select(worn != null ? worn.Id : "outfit:tour");
            StopArm();
            Debug.Log(SelectLog + " id=" + _model.Selected + " tab=" + slot);
            _focusAfterBuild = "Tab_" + slot;
            Build();
        }

        // ------------------------------------------------------------------ build

        void BuildCompact()
        {
            if (_root != null)
            {
                Transform outer = _root.parent != transform && _root.parent != null && _root.parent.name == "Root" ? _root.parent : _root;
                outer.gameObject.SetActive(false); Destroy(outer.gameObject);
            }
            _items.Clear(); _tabButtons.Clear();
            _racks = null;
            _root = UiKit.Stretch(UiKit.Rect("Root", transform));
            float vw = Rect.rect.width > 1f ? Rect.rect.width : ShowLayout.PanelWidth / ShowLayout.PanelScale;
            float vh = Rect.rect.height > 1f ? Rect.rect.height : 855f;
            _builtW = Rect.rect.width > 1f ? vw : -1f; _builtH = Rect.rect.height > 1f ? vh : -1f;
            const float gut = 20f;
            float pw = vw - 2f * gut;
            SaveBlob s = Blob;
            // the content is about 640 high in a panel 855 high: it starts a little way down, not at the top edge
            RectTransform top = _root;
            float oy = Mathf.Clamp((vh - 650f) * 0.28f, 0f, 70f);
            _root = UiKit.Stretch(UiKit.Rect("Body", top));
            _root.offsetMax = new Vector2(0f, -oy);

            // top row: back on the left, the title, the wallet on the right
            _back = MetaUi.Button(buttonPrefab, _root, "Back", "back to the street", StickerButton.Style.Ghost, Tokens.FontButtonSm, new Vector2(gut, 16f));
            if (_back != null) _back.OnClick.AddListener(() => { if (Stack != null) Stack.Pop(); });
            TextMeshProUGUI title = MetaUi.Shout(_root, "Title", Backstage.Title, Tokens.Fs3 * 1.3f, shout);
            UiKit.Place(title.rectTransform, new Vector2(gut + 250f, 22f), new Vector2(300f, 56f), UiKit.TopLeft);
            Wallet(s, gut + pw - 78f, 14f);   // clear of the sound button the HUD keeps in the top right corner
            TextMeshProUGUI note = UiKit.Text(_root, "Note", Backstage.Note(s.Fans.Lifetime), FontRole.Body, 15f, ColorRole.Ink, TextAlignmentOptions.TopLeft, false);
            UiKit.Place(note.rectTransform, new Vector2(gut, 90f), new Vector2(pw, 22f), UiKit.TopLeft);

            // the five tabs
            float ty = 124f, tgap = 8f, tw = (pw - tgap * (TabSlots.Length - 1)) / TabSlots.Length;
            for (int i = 0; i < TabSlots.Length; i++)
            {
                bool on = TabSlots[i] == _tab;
                StickerButton b = MetaUi.Button(buttonPrefab, _root, "Tab_" + TabSlots[i], TabLabels[i], on ? StickerButton.Style.Ink : StickerButton.Style.Ghost,
                    Tokens.FontButtonSm, new Vector2(gut + i * (tw + tgap), ty), 50f, tw);
                if (b == null) continue;
                string slot = TabSlots[i];
                b.PlaySound = false;
                b.OnClick.AddListener(() => SelectTab(slot));
                _tabButtons[slot] = (RectTransform)b.transform;
            }

            // what is selected, with its button
            float my = BuildMirrorCompact(gut, ty + 50f + 24f, pw);

            // the shelf's items in one row
            ShopSlot shelf = null;
            foreach (ShopSlot sl in Shop.WorldSlots) if (sl.Id == _tab) shelf = sl;
            TextMeshProUGUI head = UiKit.Text(_root, "Rack_" + _tab, shelf != null ? shelf.Name : _tab, FontRole.Display, Tokens.Fs3, ColorRole.Ink, TextAlignmentOptions.BottomLeft, false);
            float hw = UiKit.WidthOf(head);
            UiKit.Place(head.rectTransform, new Vector2(gut, my), new Vector2(hw + 4f, 40f), UiKit.TopLeft);
            if (shelf != null)
            {
                TextMeshProUGUI small = UiKit.Text(_root, "RackNote_" + _tab, shelf.Note, FontRole.Body, 15f, ColorRole.Ink, TextAlignmentOptions.BottomLeft, false);
                UiKit.Place(small.rectTransform, new Vector2(gut + hw + 12f, my), new Vector2(pw - hw - 12f, 36f), UiKit.TopLeft);
            }
            _racksContent = UiKit.Rect("Items", _root);
            _racksContent.anchorMin = new Vector2(0f, 1f); _racksContent.anchorMax = new Vector2(0f, 1f); _racksContent.pivot = new Vector2(0f, 1f);
            _racksContent.anchoredPosition = new Vector2(gut, -(my + 50f));
            _racksContent.sizeDelta = new Vector2(pw, 170f);
            var row = new List<ShopItem>();
            foreach (ShopItem it in Shop.Every) if (it.Slot == _tab) row.Add(it);
            const float igap = 10f, inset = 8f, ih = 150f;
            float cw = Mathf.Min(122f, (pw - 2f * inset - igap * (row.Count - 1)) / Mathf.Max(1, row.Count));
            for (int i = 0; i < row.Count; i++) Item(row[i], new Vector2(inset + i * (cw + igap), inset), cw, ih);

            if (_focusAfterBuild != null && EventSystem.current != null)
            {
                Transform t = FindDeep(_root, _focusAfterBuild);
                if (t != null) EventSystem.current.SetSelectedGameObject(t.gameObject);
                _focusAfterBuild = null;
            }
            FirstSelected = _back;
            ThemeSkin.Dirty();
            if (Stack != null && Stack.Top == this) LogHitsSoon();
            Debug.Log(CompactReadyLog + " balance=" + s.Fans.Balance + " sel=" + _model.Selected + " tab=" + _tab);
            TryOnChanged?.Invoke(_model.Selected);
        }

        /// <summary>The selected item's card: eyebrow, name, blurb and the one button. Returns the y under it.</summary>
        float BuildMirrorCompact(float x, float y, float w)
        {
            Mirror m = _model.Mirror();
            _mirror = UiKit.Place(UiKit.Rect("Mirror", _root), new Vector2(x, y), new Vector2(w, 100f), UiKit.TopLeft);
            _stageLook = _mirror;   // the pop on a purchase
            Image shadow = UiKit.Box(_mirror, "Shadow", ColorRole.Ink);
            Sprite torn = art != null ? art.Get("paper_torn") : null;
            Image paper = torn != null ? UiKit.Img(_mirror, "Paper", torn, Color.white) : UiKit.Box(_mirror, "Paper", ColorRole.Card);
            if (torn != null) paper.type = Image.Type.Tiled;

            const float padX = 20f, padTop = 14f, padBottom = 22f;
            float inner = w - 2f * padX, cy = padTop;
            TextMeshProUGUI eb = UiKit.Text(_mirror, "Eyebrow", m.Eyebrow, FontRole.BodyBold, 15f, ColorRole.Ink, TextAlignmentOptions.Left, false);
            UiKit.Place(eb.rectTransform, new Vector2(padX, cy), new Vector2(inner, 20f), UiKit.TopLeft);
            cy += 22f;
            TextMeshProUGUI name = UiKit.Text(_mirror, "Name", m.Name, FontRole.Display, Tokens.Fs3, ColorRole.Ink, TextAlignmentOptions.Left, false);
            UiKit.Place(name.rectTransform, new Vector2(padX, cy), new Vector2(inner, 40f), UiKit.TopLeft);
            cy += 42f;
            TextMeshProUGUI blurb = UiKit.Text(_mirror, "Blurb", m.Blurb, FontRole.Body, 18f, ColorRole.Ink, TextAlignmentOptions.TopLeft);
            float bh = Mathf.Max(26f, UiKit.HeightFor(blurb, inner));
            UiKit.Place(blurb.rectTransform, new Vector2(padX, cy), new Vector2(inner, bh), UiKit.TopLeft);
            cy += bh + 12f;

            StickerButton.Style style = m.Does == MirrorAction.Journal ? StickerButton.Style.Ghost : StickerButton.Style.Ink;
            _act = MetaUi.Button(buttonPrefab, _mirror, "Act", m.Action, style, m.Action.Length > 18 ? Tokens.FontButtonSm : Tokens.FontButton,
                new Vector2(padX, cy), Tokens.ButtonMinHeight, inner);
            if (_act != null)
            {
                _act.DisabledAlpha = MetaColor.Alpha(0.55f, 0.2f, 1f);
                _act.interactable = m.Does != MirrorAction.None;
                _act.PlaySound = false;
                _act.OnClick.AddListener(Press);
            }
            cy += Tokens.ButtonMinHeight + padBottom;
            _mirror.sizeDelta = new Vector2(w, cy);
            UiKit.Stretch(shadow.rectTransform); shadow.rectTransform.anchoredPosition = new Vector2(Tokens.Shadow, -Tokens.Shadow);
            UiKit.Stretch(paper.rectTransform);
            Sprite tape = art != null ? art.Get("tape_a") : null;
            if (tape != null)
            {
                Image t = UiKit.Img(_mirror, "Tape", tape, Color.white);
                UiKit.Place(t.rectTransform, new Vector2(w - 14f - 48f, -10f + 13f), new Vector2(96f, 26f), UiKit.Center);   // top right: the eyebrow and the name are on the left
                t.rectTransform.localEulerAngles = new Vector3(0f, 0f, MapLayout.UnityTilt(8f));
            }
            return y + cy + 26f;
        }

        /// <summary>The thumbnail of an outfit (jacket over trousers), a hair colour or a stage light (a lit swatch), as small inked swatches like the theme's.</summary>
        void WorldThumb(ShopItem it, RectTransform thumb)
        {
            RectTransform sw = UiKit.Place(UiKit.Rect("Swatch", thumb), new Vector2(36f, 36f), new Vector2(62f, 62f), UiKit.Center);
            sw.localEulerAngles = new Vector3(0f, 0f, MapLayout.UnityTilt(-4f));
            Image ring = UiKit.Box(sw, "Ring", ColorRole.Ink); UiKit.Stretch(ring.rectTransform);
            float inner = 62f - 2f * Tokens.BorderWidth;
            if (it.Slot == Shop.Outfit)
            {
                float jh = inner * 0.62f;
                Image jacket = UiKit.Img(sw, "Jacket", null, Theme.ToColor(Rgba32.Parse(it.Swatch[0])));
                UiKit.Place(jacket.rectTransform, new Vector2(Tokens.BorderWidth, Tokens.BorderWidth), new Vector2(inner, jh), UiKit.TopLeft);
                Image shade = UiKit.Img(sw, "Shade", null, Theme.ToColor(Rgba32.Parse(it.Swatch[1])));
                UiKit.Place(shade.rectTransform, new Vector2(Tokens.BorderWidth, Tokens.BorderWidth + jh - 7f), new Vector2(inner, 7f), UiKit.TopLeft);
                Image legs = UiKit.Img(sw, "Trousers", null, Theme.ToColor(Rgba32.Parse(it.Swatch[2])));
                UiKit.Place(legs.rectTransform, new Vector2(Tokens.BorderWidth, Tokens.BorderWidth + jh), new Vector2(inner, inner - jh), UiKit.TopLeft);
                return;
            }
            Color c = Theme.ToColor(Rgba32.Parse(it.Color));
            Image fill = UiKit.Img(sw, "Fill", null, c);
            UiKit.Place(fill.rectTransform, new Vector2(Tokens.BorderWidth, Tokens.BorderWidth), new Vector2(inner, inner), UiKit.TopLeft);
            if (it.Slot == Shop.Hair)
            {
                // a lighter streak, like a shine on the hair
                Image streak = UiKit.Img(sw, "Streak", null, new Color(1f, 1f, 1f, 0.28f));
                UiKit.Place(streak.rectTransform, new Vector2(Tokens.BorderWidth + inner * 0.18f, Tokens.BorderWidth), new Vector2(inner * 0.2f, inner), UiKit.TopLeft);
            }
            else
            {
                // a stage light: the colour with a bright centre
                Image core = UiKit.Img(sw, "Core", null, new Color(1f, 1f, 1f, 0.5f));
                UiKit.Place(core.rectTransform, new Vector2(Tokens.BorderWidth + inner * 0.3f, Tokens.BorderWidth + inner * 0.3f), new Vector2(inner * 0.4f, inner * 0.4f), UiKit.TopLeft);
            }
        }
    }
}
