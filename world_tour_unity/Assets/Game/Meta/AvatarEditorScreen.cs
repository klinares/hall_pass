using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;
using WorldTour.Game.Map;
using WorldTour.Game.Show;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Meta
{
    /// <summary>
    /// Phase 10B.1: the wardrobe editor, opened by the wardrobe trunk. It lists only what she owns. Tabs for tops, bottoms, shoes, hair, hats, glasses, bags, guitar, riff and
    /// stage lights, plus hair colour, skin tone and her five outfits. An item tab shows the owned items, the 24 colours and (tops and bottoms) the patterns; every change is
    /// worn and saved at once and shown on her in the world beside the panel through <see cref="LookChanged"/> (null when it closes). The outfits tab saves the look under a word
    /// from a fixed list (no typing). Mouse, Tab and arrow keys with Enter, and Escape to leave. Logs "WT_AVATAR_READY", "WT_AVATAR_PICK slot=.. id=.. color=.. pattern=..".
    /// </summary>
    public sealed class AvatarEditorScreen : GameScreen
    {
        public const string PrefabResource = "Screens/Screen_Avatar";
        public const string ReadyLog = "WT_AVATAR_READY", PickLog = "WT_AVATAR_PICK";

        /// <summary>The look she is trying on and the slot being edited (null, null when the editor closes). Set by the world host only.</summary>
        public static event Action<AvatarLook, string> LookChanged;

        [SerializeField] MapArt art;
        [SerializeField] StickerButton buttonPrefab;
        [SerializeField] Material shout;

        static string s_Start;

        const string HairColorTab = "haircolor", SkinTab = "skin", OutfitsTab = "outfits";
        static readonly string[] Tabs = { "top", "bottom", "shoes", "hair", HairColorTab, SkinTab, "face", "hat", "glasses", "bag", "guitar", "dog", "lights", OutfitsTab };
        static readonly string[] TabLabels = { "tops", "bottoms", "shoes", "hair", "hair color", "skin", "faces", "hats", "glasses", "bags", "guitar", "riff", "lights", "outfits" };

        RectTransform _root;
        AvatarLook _look;
        string _tab = "top";
        string _word;
        string _focusName;
        float _builtW = -1f, _builtH = -1f;
        StickerButton _back;
        readonly List<GameObject> _hits = new List<GameObject>();
        readonly List<List<Selectable>> _groups = new List<List<Selectable>>();
        readonly List<int> _groupCols = new List<int>();
        readonly List<string> _groupPicked = new List<string>();
        readonly List<KeyValuePair<string, RectTransform>> _hitList = new List<KeyValuePair<string, RectTransform>>();
        TextMeshProUGUI _colorName;
        string _pickedName = "";
        Coroutine _hitLog;

        public AvatarLook Look { get { return _look; } }
        public string Tab { get { return _tab; } }

        static SaveBlob Blob { get { return Boot.Blob; } }

        /// <summary>Pushes the editor (call with the world's compact panel open). <paramref name="select"/> starts on an item's tab.</summary>
        public static AvatarEditorScreen Open(ScreenStack stack, string select = null)
        {
            AvatarEditorScreen prefab = Resources.Load<AvatarEditorScreen>(PrefabResource);
            GameObject template = null;
            if (prefab == null)
            {
                template = new GameObject("Screen_Avatar", typeof(RectTransform), typeof(CanvasGroup));
                template.SetActive(false);
                prefab = template.AddComponent<AvatarEditorScreen>();
            }
            s_Start = select;
            AvatarEditorScreen s = stack.Push(prefab);
            if (template != null) Destroy(template);
            return s;
        }

        protected internal override void OnPushed()
        {
            _look = AvatarRules.Look(Blob);
            _word = AvatarCatalogue.OutfitWords[0];
            if (s_Start != null)
            {
                AvatarItem it = AvatarCatalogue.ById(s_Start);
                if (it != null) _tab = it.Slot;
                s_Start = null;
            }
            Build();
            Debug.Log(ReadyLog + " tab=" + _tab + " look=" + _look);
            if (LookChanged != null) LookChanged(_look, SlotOf(_tab));
        }

        protected internal override void OnShown()
        {
            if (FirstSelected != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(FirstSelected.gameObject);
            LogHitsSoon();
        }

        /// <summary>Tab moves between the groups (tabs, items, colors, patterns, done), not between every button.</summary>
        protected internal override bool HandleTab(int dir)
        {
            if (_groups.Count == 0 || EventSystem.current == null) return false;
            int g, i;
            Find(EventSystem.current.currentSelectedGameObject, out g, out i);
            int ng = g < 0 ? (dir > 0 ? 0 : _groups.Count - 1) : (g + dir + _groups.Count) % _groups.Count;
            List<Selectable> l = _groups[ng];
            int at = l.FindIndex(x => x != null && x.name == _groupPicked[ng]);
            Focus(l[at >= 0 ? at : 0]);   // Tab lands on the picked one of a group, else its first
            return true;
        }

        void Focus(Selectable t)
        {
            if (t == null) return;
            EventSystem.current.SetSelectedGameObject(t.gameObject);
            Debug.Log(FocusNavigator.FocusLog + " name=" + t.gameObject.name);
        }

        void Find(GameObject go, out int group, out int index)
        {
            group = -1; index = -1;
            if (go == null) return;
            for (int a = 0; a < _groups.Count; a++)
            {
                int n = _groups[a].FindIndex(x => x != null && x.gameObject == go);
                if (n >= 0) { group = a; index = n; return; }
            }
        }

        int NewGroup(int cols)
        {
            _groups.Add(new List<Selectable>()); _groupCols.Add(Mathf.Max(1, cols)); _groupPicked.Add(null);
            return _groups.Count - 1;
        }

        void Reg(int group, Selectable s, bool picked = false)
        {
            if (s == null) return;
            s.navigation = new Navigation { mode = Navigation.Mode.None };   // the screen moves focus itself (arrows in a group, Tab between groups)
            _groups[group].Add(s);
            if (picked) _groupPicked[group] = s.name;
        }

        /// <summary>Arrows stay in the group: left and right step, up and down jump a row; past the end they go on to the next or last group.</summary>
        void ArrowKeys()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || EventSystem.current == null) return;
            int dx = (kb.rightArrowKey.wasPressedThisFrame ? 1 : 0) - (kb.leftArrowKey.wasPressedThisFrame ? 1 : 0);
            int dy = (kb.downArrowKey.wasPressedThisFrame ? 1 : 0) - (kb.upArrowKey.wasPressedThisFrame ? 1 : 0);
            if (dx == 0 && dy == 0) return;
            int g, i;
            Find(EventSystem.current.currentSelectedGameObject, out g, out i);
            if (g < 0) return;
            List<Selectable> l = _groups[g];
            int cols = _groupCols[g];
            if (dx != 0) { Focus(l[Mathf.Clamp(i + dx, 0, l.Count - 1)]); return; }
            int t = i + dy * cols;
            if (t >= 0 && t < l.Count) { Focus(l[t]); return; }
            if (dy > 0 && i / cols < (l.Count - 1) / cols) { Focus(l[l.Count - 1]); return; }   // a short last row
            int ng = g + dy;
            if (ng < 0 || ng >= _groups.Count) return;
            List<Selectable> n = _groups[ng];
            int col = i % cols;
            Focus(dy > 0 ? n[Mathf.Min(col, n.Count - 1)] : n[Mathf.Min(((n.Count - 1) / _groupCols[ng]) * _groupCols[ng] + col, n.Count - 1)]);
        }

        protected internal override void OnRemoved()
        {
            if (LookChanged != null) LookChanged(null, null);
        }

        void Update()
        {
            if (_builtW > 0f && (Mathf.Abs(Rect.rect.width - _builtW) > 1f || Mathf.Abs(Rect.rect.height - _builtH) > 1f)) Build();
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && Stack != null && Stack.Top == this) Stack.Pop();
            if (Stack != null && Stack.Top == this) ArrowKeys();
            PaintColorName();
        }

        /// <summary>The color name under the swatches: the focused one while the keyboard or mouse rests on it, else the worn one.</summary>
        void PaintColorName()
        {
            if (_colorName == null) return;
            string n = _pickedName;
            GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (sel != null) { var sw = sel.GetComponent<SwatchName>(); if (sw != null) n = sw.Text; }
            if (_colorName.text != n) _colorName.text = n;
        }

        // ------------------------------------------------------------------ test hooks (only with ?wt_test)

        void LogHitsSoon()
        {
            if (!MetaUi.TestMode) return;
            if (_hitLog != null) StopCoroutine(_hitLog);
            _hitLog = StartCoroutine(LogHitsLater());
        }

        IEnumerator LogHitsLater()
        {
            yield return new WaitForSecondsRealtime(0.4f);   // after the slide-in
            _hitLog = null;
            _hitList.Clear();
            foreach (List<Selectable> grp in _groups)
                foreach (Selectable s in grp)
                    if (s != null) _hitList.Add(new KeyValuePair<string, RectTransform>(s.name, (RectTransform)s.transform));
            MetaUi.LogHits("avatar", _hitList);
        }

        static string SlotOf(string tab)
        {
            if (tab == HairColorTab || tab == SkinTab) return AvatarCatalogue.Hair;
            if (tab == OutfitsTab) return null;
            return tab;
        }

        // ------------------------------------------------------------------ changes

        void Commit(string slot, string focus)
        {
            SaveBlob s = Blob;
            AvatarRules.Wear(s, _look);
            _look = AvatarRules.Look(s);
            Boot.Persist();
            _focusName = focus;
            AvatarPick p = slot != null ? _look.Get(slot) : null;
            Debug.Log(PickLog + " slot=" + slot + (p != null ? " id=" + p.Id + " color=" + p.Color + " pattern=" + p.Pattern : "") + " skin=" + _look.Skin + " hair=" + _look.HairColor);
            Build();
            if (LookChanged != null) LookChanged(_look, slot);
        }

        void PickItem(string slot, AvatarItem it)
        {
            AvatarPick old = _look.Get(slot);
            _look.Set(slot, new AvatarPick(it.Id, old != null ? old.Color : 0, old != null ? old.Pattern : 0));
            Commit(slot, "Item_" + it.Id);
        }

        void PickColor(string slot, int index)
        {
            if (slot == AvatarCatalogue.Hair || slot == HairColorTab) { _look.HairColor = index; _look.Hair.Color = index; }
            else _look.Get(slot).Color = index;
            Commit(slot == HairColorTab ? AvatarCatalogue.Hair : slot, "Color_" + index);
        }

        void PickPattern(string slot, int index)
        {
            _look.Get(slot).Pattern = index;
            Commit(slot, "Pattern_" + index);
        }

        void PickSkin(int index)
        {
            _look.Skin = index;
            Commit(AvatarCatalogue.Hair, "Skin_" + index);
        }

        void SelectTab(string tab)
        {
            if (_tab == tab) return;
            _tab = tab;
            _focusName = "Tab_" + tab;
            Build();
            if (LookChanged != null) LookChanged(_look, SlotOf(tab));
        }

        // ------------------------------------------------------------------ build

        void Build()
        {
            if (_focusName == null && _root != null && EventSystem.current != null)
            {
                GameObject cur = EventSystem.current.currentSelectedGameObject;
                if (cur != null && cur.transform.IsChildOf(_root)) _focusName = cur.name;   // a rebuild (resize) keeps the focus where it was
            }
            _groups.Clear(); _groupCols.Clear(); _groupPicked.Clear(); _colorName = null; _pickedName = "";
            if (_root != null) { _root.gameObject.SetActive(false); if (Application.isPlaying) Destroy(_root.gameObject); else DestroyImmediate(_root.gameObject); }
            _hits.Clear();
            _root = UiKit.Stretch(UiKit.Rect("Root", transform));
            float vw = Rect.rect.width > 1f ? Rect.rect.width : ShowLayout.PanelWidth / ShowLayout.PanelScale;
            float vh = Rect.rect.height > 1f ? Rect.rect.height : 855f;
            _builtW = Rect.rect.width > 1f ? vw : -1f; _builtH = Rect.rect.height > 1f ? vh : -1f;
            const float gut = 20f, gap = 8f;
            float pw = vw - 2f * gut;

            _back = MetaUi.Button(buttonPrefab, _root, "Back", "done", StickerButton.Style.Ink, Tokens.FontButtonSm, new Vector2(gut + 8f, 24f));
            if (_back != null) _back.OnClick.AddListener(() => { if (Stack != null) Stack.Pop(); });
            TextMeshProUGUI title = MetaUi.Shout(_root, "Title", "wardrobe", Tokens.Fs3 * 1.3f, shout);
            UiKit.Place(title.rectTransform, new Vector2(gut + 160f, 22f), new Vector2(300f, 56f), UiKit.TopLeft);

            // tabs in two rows
            int perRow = 7;
            int tabGroup = NewGroup(perRow);
            float tw = (pw - gap * (perRow - 1)) / perRow, ty = 90f;
            for (int i = 0; i < Tabs.Length; i++)
            {
                int row = i / perRow, col = i % perRow;
                bool on = Tabs[i] == _tab;
                StickerButton b = MetaUi.Button(buttonPrefab, _root, "Tab_" + Tabs[i], TabLabels[i], on ? StickerButton.Style.Ink : StickerButton.Style.Ghost,
                    Tokens.FontButtonSm * 0.9f, new Vector2(gut + col * (tw + gap), ty + row * 54f), 46f, tw);
                if (b == null) continue;
                string tab = Tabs[i];
                Reg(tabGroup, b, on);
                b.PlaySound = false;
                b.OnClick.AddListener(() => SelectTab(tab));
            }

            float y = ty + 2 * 54f + 14f;
            if (_tab == OutfitsTab) BuildOutfits(gut, y, pw);
            else if (_tab == HairColorTab) { Heading("hair color", gut, y); Swatches(gut, y + 38f, pw, AvatarCatalogue.PaletteHex, _look.HairColor, i => PickColor(HairColorTab, i), AvatarCatalogue.PaletteNames); }
            else if (_tab == SkinTab) { Heading("skin", gut, y); Swatches(gut, y + 38f, pw, AvatarCatalogue.SkinHex, _look.Skin, PickSkin, AvatarCatalogue.SkinNames, 8); }
            else BuildItems(_tab, gut, y, pw);

            if (_focusName != null && EventSystem.current != null)
            {
                Transform t = FindDeep(_root, _focusName);
                if (t != null) EventSystem.current.SetSelectedGameObject(t.gameObject);
                _focusName = null;
            }
            int doneGroup = NewGroup(1);
            Reg(doneGroup, _back);
            for (int g = _groups.Count - 1; g >= 0; g--)
                if (_groups[g].Count == 0) { _groups.RemoveAt(g); _groupCols.RemoveAt(g); _groupPicked.RemoveAt(g); }   // an outfit row with nothing to wear yet
            int ft = _groups[0].FindIndex(x => x.name == "Tab_" + _tab);
            FirstSelected = _groups[0].Count > 0 ? _groups[0][Mathf.Max(0, ft)] : _back;
            ThemeSkin.Dirty();
            LogHitsSoon();
        }

        static Transform FindDeep(Transform t, string name)
        {
            foreach (Transform c in t)
            {
                if (c.name == name) return c;
                Transform d = FindDeep(c, name);
                if (d != null) return d;
            }
            return null;
        }

        void Heading(string text, float x, float y)
        {
            TextMeshProUGUI h = UiKit.Text(_root, "Head_" + text, text, FontRole.Display, Tokens.Fs3, ColorRole.Ink, TextAlignmentOptions.BottomLeft, false);
            UiKit.Place(h.rectTransform, new Vector2(x, y), new Vector2(UiKit.WidthOf(h) + 4f, 36f), UiKit.TopLeft);
        }

        void BuildItems(string slot, float gut, float y, float pw)
        {
            int slotIndex = Array.IndexOf(new List<string>(AvatarCatalogue.Slots).ToArray(), slot);
            Heading(slotIndex >= 0 ? AvatarCatalogue.SlotLabels[slotIndex] : slot, gut, y);
            y += 40f;
            var owned = new List<AvatarItem>();
            foreach (AvatarItem it in AvatarCatalogue.InSlot(slot)) if (AvatarRules.Owns(Blob, it.Id)) owned.Add(it);
            AvatarPick cur = _look.Get(slot);
            const int cols = 4;
            int itemGroup = NewGroup(cols);
            const float gap = 8f, ih = 46f;
            float cw = (pw - gap * (cols - 1)) / cols;
            for (int i = 0; i < owned.Count; i++)
            {
                AvatarItem it = owned[i];
                bool on = cur != null && cur.Id == it.Id;
                StickerButton b = MetaUi.Button(buttonPrefab, _root, "Item_" + it.Id, it.Name, on ? StickerButton.Style.Ink : StickerButton.Style.Ghost,
                    Tokens.FontButtonSm * 0.9f, new Vector2(gut + (i % cols) * (cw + gap), y + (i / cols) * (ih + gap)), ih, cw);
                if (b == null) continue;
                AvatarItem item = it;
                Reg(itemGroup, b, on);
                b.PlaySound = false;
                b.OnClick.AddListener(() => PickItem(slot, item));
            }
            y += Mathf.Ceil(owned.Count / (float)cols) * (ih + gap) + 10f;
            if (AvatarCatalogue.TakesColor(slot) && slot != AvatarCatalogue.Hair && cur != null && !cur.Id.EndsWith(":none"))
            {
                Heading("color", gut, y);
                y += Swatches(gut, y + 38f, pw, AvatarCatalogue.PaletteHex, cur.Color, i => PickColor(slot, i), AvatarCatalogue.PaletteNames) + 38f + 6f;
            }
            if (AvatarCatalogue.TakesPattern(slot) && cur != null)
            {
                Heading("pattern", gut, y);
                int patGroup = NewGroup(AvatarCatalogue.PatternNames.Count);
                float pwid = (pw - gap * (AvatarCatalogue.PatternNames.Count - 1)) / AvatarCatalogue.PatternNames.Count;
                for (int i = 0; i < AvatarCatalogue.PatternNames.Count; i++)
                {
                    bool on = cur.Pattern == i;
                    StickerButton b = MetaUi.Button(buttonPrefab, _root, "Pattern_" + i, AvatarCatalogue.PatternNames[i], on ? StickerButton.Style.Ink : StickerButton.Style.Ghost,
                        Tokens.FontButtonSm * 0.9f, new Vector2(gut + i * (pwid + gap), y + 40f), 46f, pwid);
                    if (b == null) continue;
                    int idx = i;
                    Reg(patGroup, b, on);
                    b.PlaySound = false;
                    b.OnClick.AddListener(() => PickPattern(slot, idx));
                }
            }
        }

        /// <summary>A grid of colour swatches, each a keyboard-focusable button with an ink ring (thicker when it is the one worn).</summary>
        float Swatches(float x, float y, float pw, IReadOnlyList<string> hex, int selected, Action<int> pick, IReadOnlyList<string> names, int perRow = 12)
        {
            const float gap = 6f;
            int group = NewGroup(perRow);
            float size = Mathf.Min(64f, (pw - gap * (perRow - 1)) / perRow);
            for (int i = 0; i < hex.Count; i++)
            {
                int idx = i;
                RectTransform rt = UiKit.Place(UiKit.Rect("Color_" + i + (hex == AvatarCatalogue.SkinHex ? "" : ""), _root), new Vector2(x + (i % perRow) * (size + gap), y + (i / perRow) * (size + gap)), new Vector2(size, size), UiKit.TopLeft);
                if (hex == AvatarCatalogue.SkinHex) rt.name = "Skin_" + i;
                bool on = i == selected;
                Image ring = rt.gameObject.AddComponent<Image>();
                ring.color = Theme.Color(ColorRole.Ink);
                ring.raycastTarget = true;
                Image fill = UiKit.Box(rt, "Fill", ColorRole.Card);
                UiKit.Stretch(fill.rectTransform, on ? 7f : 3f);
                Color c; ColorUtility.TryParseHtmlString(hex[i], out c);
                fill.color = c;
                fill.raycastTarget = false;
                if (on)
                {
                    Image dot = UiKit.Box(rt, "Mark", ColorRole.Card);
                    UiKit.Place(dot.rectTransform, new Vector2(size * 0.5f, size * 0.5f), new Vector2(size * 0.16f, size * 0.16f), UiKit.Center);
                    dot.color = AvatarContrast(c);
                    dot.raycastTarget = false;
                }
                Button b = rt.gameObject.AddComponent<Button>();
                b.targetGraphic = ring;
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => pick(idx));
                var label = rt.gameObject.AddComponent<SwatchName>();
                label.Text = i < names.Count ? names[i] : "";
                Reg(group, b, on);
                if (on) _pickedName = label.Text;
            }
            int rows = (hex.Count + perRow - 1) / perRow;
            float below = rows * (size + gap);
            _colorName = UiKit.Text(_root, "ColorName", _pickedName, FontRole.BodyBold, 20f, ColorRole.Ink, TextAlignmentOptions.TopLeft, false);
            UiKit.Place(_colorName.rectTransform, new Vector2(x, y + below), new Vector2(pw, 28f), UiKit.TopLeft);
            return below + 28f;
        }

        static Color AvatarContrast(Color c)
        {
            float l = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
            return l > 0.55f ? new Color(0.1f, 0.1f, 0.2f) : Color.white;
        }

        // ------------------------------------------------------------------ outfits

        void BuildOutfits(float gut, float y, float pw)
        {
            SaveBlob s = Blob;
            Heading("outfits", gut, y);
            y += 42f;
            const float gap = 8f;
            int wordGroup = NewGroup(2);
            StickerButton prev = MetaUi.Button(buttonPrefab, _root, "WordPrev", "<", StickerButton.Style.Ghost, Tokens.FontButtonSm, new Vector2(gut, y), 46f, 56f);
            if (prev != null) { Reg(wordGroup, prev); prev.PlaySound = false; prev.OnClick.AddListener(() => { _word = AvatarRules.NextWord(_word, -1); _focusName = "WordPrev"; Build(); }); }
            TextMeshProUGUI w = UiKit.Text(_root, "Word", _word, FontRole.Display, Tokens.Fs3, ColorRole.Ink, TextAlignmentOptions.Center, false);
            UiKit.Place(w.rectTransform, new Vector2(gut + 64f, y), new Vector2(pw - 2f * 64f, 46f), UiKit.TopLeft);
            StickerButton next = MetaUi.Button(buttonPrefab, _root, "WordNext", ">", StickerButton.Style.Ghost, Tokens.FontButtonSm, new Vector2(gut + pw - 56f, y), 46f, 56f);
            if (next != null) { Reg(wordGroup, next); next.PlaySound = false; next.OnClick.AddListener(() => { _word = AvatarRules.NextWord(_word, 1); _focusName = "WordNext"; Build(); }); }
            y += 56f;
            TextMeshProUGUI hint = UiKit.Text(_root, "Hint", "pick a word, then save your look in a slot.", FontRole.BodyBold, 19f, ColorRole.Ink, TextAlignmentOptions.TopLeft, false);
            UiKit.Place(hint.rectTransform, new Vector2(gut, y), new Vector2(pw, 28f), UiKit.TopLeft);
            y += 36f;
            float bw = 150f;
            for (int i = 0; i < AvatarCatalogue.OutfitSlots; i++)
            {
                int slot = i;
                int rowGroup = NewGroup(2);
                AvatarOutfit o = AvatarRules.Outfit(s, i);
                TextMeshProUGUI name = UiKit.Text(_root, "OutfitName_" + i, (i + 1) + "  " + (o != null ? o.Name : "empty"), FontRole.BodyBold, 22f, ColorRole.Ink, TextAlignmentOptions.Left, false);
                UiKit.Place(name.rectTransform, new Vector2(gut, y), new Vector2(pw - 2f * bw - 2f * gap, 46f), UiKit.TopLeft);
                StickerButton wear = MetaUi.Button(buttonPrefab, _root, "Wear_" + i, "wear", o != null ? StickerButton.Style.Ink : StickerButton.Style.Ghost, Tokens.FontButtonSm, new Vector2(gut + pw - 2f * bw - gap, y), 46f, bw);
                if (wear != null)
                {
                    wear.PlaySound = false;
                    wear.interactable = o != null;
                    wear.DisabledAlpha = 0.8f;   // an empty slot is a plain card with dark letters, readable on the pink
                    if (o != null) Reg(rowGroup, wear);
                    wear.OnClick.AddListener(() =>
                    {
                        AvatarLook l = AvatarRules.OutfitLook(Blob, slot);
                        if (l == null) return;
                        _look = l; Commit(null, "Wear_" + slot);
                    });
                }
                StickerButton save = MetaUi.Button(buttonPrefab, _root, "Save_" + i, "save here", StickerButton.Style.Ghost, Tokens.FontButtonSm, new Vector2(gut + pw - bw, y), 46f, bw);
                if (save != null)
                {
                    Reg(rowGroup, save);
                    save.PlaySound = false;
                    save.OnClick.AddListener(() =>
                    {
                        AvatarRules.SaveOutfit(Blob, slot, _word, _look);
                        Boot.Persist();
                        _focusName = "Save_" + slot;
                        Debug.Log(PickLog + " outfit=" + slot + " word=" + _word);
                        Build();
                    });
                }
                y += 56f;
            }
        }
    }

    /// <summary>The name of a colour swatch, kept on it for tests and screen readers (nothing is drawn).</summary>
    public sealed class SwatchName : MonoBehaviour { public string Text; }
}
