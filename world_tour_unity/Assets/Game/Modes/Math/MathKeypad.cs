using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using WorldTour.Core.Maths;
using WorldTour.Game.Audio;
using WorldTour.Game.Map;
using WorldTour.Game.Motion;
using WorldTour.Game.Show;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// One keypad key (math.css .key): card face, ink ring, 3 px ink shadow; pressing pushes it down and in and turns the
    /// face lilac, and it springs back on release. It is not a Selectable, so Tab skips the twelve keys (she types on the
    /// real keys instead), and Enter or Space never land on a key.
    /// </summary>
    public sealed class MathKey : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IPointerExitHandler
    {
        public string Key;
        public Action<string> Pressed;
        internal RectTransform Body;
        internal Image Face;
        internal Color Rest;
        internal bool Enabled = true;
        bool _down;

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || !Enabled) return;
            _down = true;
            Face.color = Theme.Color(ColorRole.Accent);
            ToRest();
            Tweener.Play(Body, Presets.Press, null, hold: true, fadeWhenReduced: false);
        }

        public void OnPointerUp(PointerEventData e) { Up(); }
        public void OnPointerExit(PointerEventData e) { if (_down) Up(); }

        void Up()
        {
            if (!_down) return;
            _down = false;
            Face.color = Rest;
            Tweener.Play(Body, Presets.Release, null, hold: false, fadeWhenReduced: false);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || !Enabled) return;
            var h = Pressed; if (h != null) h(Key);
        }

        /// <summary>
        /// Back to the resting pose before a new press: a press started mid-spring would otherwise take the half-pressed
        /// pose as its rest and leave the key squashed (seen when the same digit is typed twice quickly).
        /// </summary>
        void ToRest()
        {
            Tweener.KillAll(Body);
            Body.anchoredPosition = Vector2.zero;
            Body.localScale = Vector3.one;
            Body.localRotation = Quaternion.identity;
        }

        /// <summary>A real key press shows on the screen key too: a quick press and spring.</summary>
        public void Flash()
        {
            if (!Enabled || Body == null) return;
            ToRest();
            Tweener.Play(Body, Presets.Press, () => { if (Body != null) Tweener.Play(Body, Presets.Release, null, hold: false, fadeWhenReduced: false); }, hold: false, fadeWhenReduced: false);
        }
    }

    /// <summary>
    /// parts.js:25-112 keypad + wireKeypad: the readout (math.css .readout: "$" for money, the typed answer drawn the way it
    /// will be read, 1 3/4 stacked as she types, and a blinking caret) over a 3-column pad with the decimal point (money,
    /// decimals) or the fraction bar and "and" (fractions), backspace and the submit key. The real keyboard types too:
    /// digits, ".", "/", space, Backspace and Enter, except that Enter and Space go to a focused button that isn't the
    /// pad's (the hint, next song), as on the web. The text rules are <see cref="KeypadText"/>.
    /// Logs "WT_KEYPAD pos=k:x:y,..." (each key's centre in page pixels) for mouse-only browser tests.
    /// </summary>
    public sealed class MathKeypad : MonoBehaviour, IMathPadView
    {
        public const float ReadoutHeight = 66f, KeyHeight = 48f, Gap = 8f;

        private KeypadText _text;
        private RectTransform _readout, _content, _ring, _readoutBody;
        private Image _readoutFace;
        private Image _caret;
        private CanvasGroup _keysGroup;
        private readonly Dictionary<string, MathKey> _keys = new Dictionary<string, MathKey>();
        private float _nudgeUntil = -1, _blinkAt;
        private ColorRole _faceRole = ColorRole.Card;
        private bool _showCaret = true;
        private Keyboard _kb;
        private Action _showNext;

        /// <summary>The submit key or Enter, with what she typed.</summary>
        public event Action<string> Submitted;

        public string Text { get { return _text != null ? _text.Text : ""; } }
        public bool Locked { get { return _text == null || _text.Locked; } }
        public RectTransform ReadoutRect { get { return _readout; } }
        public IReadOnlyDictionary<string, MathKey> Keys { get { return _keys; } }

        public static MathKeypad Make(RectTransform parent, AnswerType type, string checkLabel, Action showNext)
        {
            RectTransform root = ShowUi.Rect("Keypad", parent);
            var col = root.gameObject.AddComponent<VerticalLayoutGroup>();
            col.spacing = 12;
            col.childControlWidth = true; col.childControlHeight = true; col.childForceExpandWidth = true; col.childForceExpandHeight = false;
            var pad = root.gameObject.AddComponent<MathKeypad>();
            pad._text = new KeypadText(type);
            pad._showNext = showNext;
            pad.BuildReadout(root, type);
            pad.BuildKeys(root, type, checkLabel);
            pad.Paint();
            return pad;
        }

        private void BuildReadout(RectTransform root, AnswerType type)
        {
            _readout = ShowUi.Rect("Readout", root);
            MathUi.TopPivot(_readout);
            var le = _readout.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = le.minHeight = ReadoutHeight;   // a tall prompt above may not squeeze the answer box
            // .readout.nudge: a 4px accent ring round it and an accent-2 shadow, for 650 ms
            _ring = ShowUi.Rect("NudgeRing", _readout);
            ShowUi.Stretch(_ring, -5f);
            ShowUi.Panel(_ring, ColorRole.Accent);
            _ring.gameObject.SetActive(false);
            _readoutBody = ShowUi.Rect("Body", _readout);
            ShowUi.Stretch(_readoutBody);
            RectTransform face = MathUi.Card(_readoutBody, Tokens.ShadowMd, ColorRole.Card, ColorRole.Ink);
            _readoutFace = face.GetComponent<Image>();
            _content = ShowUi.Rect("Content", face);
            ShowUi.Stretch(_content);
            var row = _content.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 4; row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = true; row.childControlHeight = true; row.childForceExpandWidth = false; row.childForceExpandHeight = false;
        }

        private void BuildKeys(RectTransform root, AnswerType type, string checkLabel)
        {
            RectTransform keysRoot = ShowUi.Rect("Keys", root);
            _keysGroup = keysRoot.gameObject.AddComponent<CanvasGroup>();
            var kcol = keysRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            kcol.spacing = Gap;
            kcol.childControlWidth = true; kcol.childControlHeight = true; kcol.childForceExpandWidth = true; kcol.childForceExpandHeight = false;
            bool dot = _text.HasDot, frac = _text.HasFraction;
            // in the world's docked panel the digits sit in two rows of six (1 to 6, then 7 to 0, the dot or slash, back): half the height, so a typed song fits beside the stage
            bool wide = ShowLayout.Compact;
            string[] keys = wide
                ? new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", frac ? "/" : dot ? "." : "", "back" }
                : new[] { "7", "8", "9", "4", "5", "6", "1", "2", "3", frac ? "/" : dot ? "." : "", "0", "back" };
            RectTransform grid = ShowUi.Rect("Grid", keysRoot);
            var gle = grid.gameObject.AddComponent<LayoutElement>();
            int cols = wide ? 6 : 3;
            gle.preferredHeight = (wide ? 2 : 4) * KeyHeight + (wide ? 1 : 3) * Gap;
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2((MathUi.PadWidth - (cols - 1) * 9f) / cols, KeyHeight);
            g.spacing = new Vector2(9f, Gap);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = cols;
            foreach (string k in keys)
            {
                if (k == "") { ShowUi.Rect("Blank", grid); continue; }
                MakeKey(grid, k, k == "back" || k == "/" ? KeyLook.Fn : KeyLook.Digit, -1f);
            }
            if (frac) MakeKey(keysRoot, " ", KeyLook.Wide, 44f);
            MakeKey(keysRoot, "go", KeyLook.Go, 50f, checkLabel);
        }

        private enum KeyLook { Digit, Fn, Wide, Go }

        private void MakeKey(RectTransform parent, string k, KeyLook look, float height, string label = null)
        {
            RectTransform rt = ShowUi.Rect(KeyName(k), parent);
            if (height > 0) { var le = rt.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = le.minHeight = height; }
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(1, 1, 1, 0);
            var key = rt.gameObject.AddComponent<MathKey>();
            key.Key = k;
            key.Pressed = OnKey;
            key.Body = ShowUi.Rect("Body", rt);
            ShowUi.Stretch(key.Body);
            ColorRole shadow = look == KeyLook.Go ? ColorRole.Accent : ColorRole.Ink;
            RectTransform face = MathUi.Card(key.Body, 3f, look == KeyLook.Go ? ColorRole.Ink : ColorRole.Card, shadow);
            key.Face = face.GetComponent<Image>();
            if (look == KeyLook.Fn || look == KeyLook.Wide) key.Face.color = MathUi.Mix(ColorRole.Accent, .4f, ColorRole.Card);
            key.Rest = key.Face.color;
            if (k == "back") BackIcon(face);
            else if (k == "/") { RectTransform f = MathUi.Frac(face, null, "", "", ShowLayout.Compact ? 25f : 17f, ColorRole.Ink, true); Centre(f); }
            else if (look == KeyLook.Go) ShowUi.Label(face, "Label", label ?? MathLines.LockItIn, 27.2f, FontRole.Display, ColorRole.OnInk);
            else if (look == KeyLook.Wide) ShowUi.Label(face, "Label", MathLines.And, 24f, FontRole.Display, ColorRole.Ink);
            else ShowUi.Label(face, "Label", k == "." ? MathLines.DotFace : k, 25.6f, FontRole.BodyBold, ColorRole.Ink);
            _keys[k] = key;
        }

        private static string KeyName(string k)
        {
            switch (k)
            {
                case ".": return "Key_dot";
                case "/": return "Key_frac";
                case " ": return "Key_and";
                case "back": return "Key_back";
                case "go": return "Key_go";
                default: return "Key_" + k;
            }
        }

        private static void Centre(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = Vector2.zero;
        }

        /// <summary>parts.js:26 the delete key's icon: a key-cap outline with a cross, in UI lines.</summary>
        private static void BackIcon(RectTransform face)
        {
            RectTransform icon = ShowUi.Rect("Icon", face);
            Centre(icon);
            icon.sizeDelta = new Vector2(30, 30);
            float s = 30f / 24f;
            Vector2 P(float x, float y) { return new Vector2((x - 12f) * s, (12f - y) * s); }
            var outline = icon.gameObject.AddComponent<UiLine>();
            outline.raycastTarget = false;
            outline.Width = 2.4f * s * .8f;
            outline.SetPoints(new[] { P(9, 5), P(20, 5), P(20, 19), P(9, 19), P(3, 12), P(9, 5) });
            outline.color = Theme.Color(ColorRole.Ink);
            foreach (var pair in new[] { new[] { P(12, 9), P(17, 15) }, new[] { P(17, 9), P(12, 15) } })
            {
                RectTransform x = ShowUi.Rect("X", icon);
                Centre(x);
                x.sizeDelta = new Vector2(30, 30);
                var l = x.gameObject.AddComponent<UiLine>();
                l.raycastTarget = false;
                l.Width = 2.4f * s * .8f;
                l.SetPoints(pair);
                l.color = Theme.Color(ColorRole.Ink);
            }
        }

        // ---------- input ----------

        private void OnEnable()
        {
            _kb = Keyboard.current;
            if (_kb != null) _kb.onTextInput += OnTextInput;
        }

        private void OnDisable()
        {
            if (_kb != null) _kb.onTextInput -= OnTextInput;
            _kb = null;
        }

        /// <summary>parts.js:84-85: Enter and Space belong to a focused button outside the pad.</summary>
        private static bool ForeignButtonFocused()
        {
            EventSystem es = EventSystem.current;
            GameObject go = es != null ? es.currentSelectedGameObject : null;
            if (go == null || !go.activeInHierarchy) return false;
            var s = go.GetComponent<Selectable>();
            return s != null && s.IsInteractable();
        }

        private void OnTextInput(char c)
        {
            if (Locked || !isActiveAndEnabled) return;
            if (c == ' ' && ForeignButtonFocused()) return;
            if ((c >= '0' && c <= '9') || c == '.' || c == '/' || c == ' ')
            {
                // typing an answer takes the keyboard back from a button she Tabbed to (the hint), so the next Enter
                // locks the answer in instead of pressing that button again
                if (c != ' ' && ForeignButtonFocused() && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                Flash(c.ToString());
                Press(c.ToString());
            }
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb != _kb) { OnDisable(); OnEnable(); }
            if (kb != null && !Locked)
            {
                if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && !ForeignButtonFocused()) { Flash("go"); Press("go"); }
                else if (kb.backspaceKey.wasPressedThisFrame) { Flash("back"); Press("back"); }
            }
            // .readout .caret: blinks once a second (steps), steady under reduce motion
            if (_caret != null)
            {
                bool on = !_showCaret ? false : MotionPrefs.Reduced || ((Time.unscaledTime - _blinkAt) % 1f) < .5f;
                if (_caret.enabled != on) _caret.enabled = on;
            }
            if (_nudgeUntil > 0 && Time.unscaledTime >= _nudgeUntil) { _nudgeUntil = -1; _ring.gameObject.SetActive(false); }
        }

        private void Flash(string k)
        {
            if (_keys.TryGetValue(k, out MathKey key)) key.Flash();
        }

        private void OnKey(string k) { Press(k); }

        /// <summary>parts.js:65-77 press(k): one key. "go" submits what is typed.</summary>
        public void Press(string k)
        {
            if (Locked) return;
            if (k == "go")
            {
                var h = Submitted; if (h != null) h(_text.Text);
                return;
            }
            if (!_text.Press(k)) return;
            Sfx.Play(SfxId.Tap);
            _blinkAt = Time.unscaledTime;
            Paint();
        }

        // ---------- the view (IMathPadView) ----------

        public void Nudge()
        {
            _ring.gameObject.SetActive(true);
            _nudgeUntil = Time.unscaledTime + .65f;
        }

        public void LockRight()
        {
            Lock(ColorRole.PaperDeep);
            Feedback.Pop(_readout);
        }

        public void Wobble() { Feedback.Wobble(_readout); }

        public void ShowAnswer(string text)
        {
            _text.Set(text);
            Lock(ColorRole.Accent);
        }

        public void Clear() { _text.Clear(); Paint(); }

        public void ShowNext() { if (_showNext != null) _showNext(); }

        /// <summary>pad.lock(cls): no more keys (faded to .5), the readout tinted, no caret.</summary>
        public void Lock(ColorRole face)
        {
            _text.Lock();
            _faceRole = face;
            _showCaret = false;
            _keysGroup.alpha = .5f;
            _keysGroup.blocksRaycasts = false;
            foreach (MathKey k in _keys.Values) k.Enabled = false;
            Paint();
        }

        /// <summary>parts.js:52-59 renderReadout, then the caret.</summary>
        private void Paint()
        {
            if (_content == null) return;
            _readoutFace.color = Theme.Color(_faceRole);
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                GameObject c = _content.GetChild(i).gameObject;
                c.SetActive(false);
                Destroy(c);
            }
            const float size = 33.6f;                                          // font: 700 2.1rem mono
            if (_text.Type == AnswerType.Money) Plain("$", size, .7f);
            ReadoutShape shape = KeypadText.Shape(_text.Text);
            if (shape.Kind == ReadoutKind.Plain) Plain(shape.Text, size, 1f);
            else if (shape.Kind == ReadoutKind.Fraction) MathUi.Frac(_content, shape.Whole, shape.Num, shape.Den, size * .82f, ColorRole.Ink, true);
            RectTransform caret = ShowUi.Rect("Caret", _content);
            var cle = caret.gameObject.AddComponent<LayoutElement>();
            cle.preferredWidth = 3; cle.preferredHeight = size * 1.1f;
            _caret = caret.gameObject.AddComponent<Image>();
            _caret.color = Theme.Color(ColorRole.Accent2);
            _caret.raycastTarget = false;
            _caret.enabled = _showCaret;
        }

        private void Plain(string text, float size, float alpha)
        {
            TMP_Text t = SpellingUi.Text(_content, "Typed", text, size, FontRole.BodyBold, ColorRole.Ink, false);
            t.richText = false;
            t.text = text;
            Color c = t.color; c.a = alpha; t.color = c;
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = text.Length * .6f * size + 2;
            le.preferredHeight = size * 1.2f;
        }

        /// <summary>"WT_KEYPAD pos=7:x:y,..." each key's centre in page pixels from the top left.</summary>
        public void LogPositions()
        {
            if (this == null) return;
            Canvas.ForceUpdateCanvases();
            var sb = new System.Text.StringBuilder("WT_KEYPAD pos=");
            bool first = true;
            foreach (KeyValuePair<string, MathKey> kv in _keys)
            {
                if (!first) sb.Append(',');
                first = false;
                string name = kv.Key == " " ? "and" : kv.Key == "." ? "dot" : kv.Key == "/" ? "frac" : kv.Key;
                sb.Append(name).Append(':').Append(MathUi.Centre((RectTransform)kv.Value.transform));
            }
            Debug.Log(sb.ToString());
        }
    }
}
