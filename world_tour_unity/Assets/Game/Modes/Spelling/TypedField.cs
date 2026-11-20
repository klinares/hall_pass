using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WorldTour.Game.Show;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// parts.js:31 typedField: the keyboard-first answer box for the spelling modes (screens.css .ans). Typing shows in
    /// lowercase (tabs and line breaks are dropped, so Tab still moves focus), Enter submits, and the rules live in
    /// <see cref="TypedRound"/>, which gets the raw text and trims and lowercases it itself.
    /// A TMP_InputField built in code: Answer (layout) / Shadow (accent, 4 px down-right) / Body (ring, animated) / Face /
    /// Text Area (mask) / Placeholder, Text.
    /// Enter is passed on one frame later, after the field has finished its own end-of-edit, so the round can re-focus
    /// the field after a wrong try without the field closing itself again; a second submit from the same Enter is dropped.
    /// </summary>
    public sealed class TypedField : MonoBehaviour
    {
        /// <summary>screens.css .ans min-height.</summary>
        public const float Height = 62f;
        /// <summary>screens.css .ans font: 700 clamp(1.4rem,6vw,1.9rem) at 1366.</summary>
        public const float FontSize = 30.4f;

        public enum Look { Normal, Right, Shown }

        private TMP_InputField _input;
        private Image _ring, _face;
        private RectTransform _body;
        private Look _look = Look.Normal;
        private bool _pendingSubmit, _wantFocus;

        /// <summary>Enter was pressed in the field (raw text).</summary>
        public event Action<string> Submitted;

        public TMP_InputField Input { get { return _input; } }
        /// <summary>The part that pops and wobbles.</summary>
        public RectTransform Body { get { return _body; } }

        public string Text
        {
            get { return _input != null ? _input.text : ""; }
            set { if (_input != null) _input.SetTextWithoutNotify(value ?? ""); }
        }

        public static TypedField Make(RectTransform parent, string placeholder, float width)
        {
            RectTransform root = ShowUi.Rect("Answer", parent);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width; le.preferredHeight = Height;
            var f = root.gameObject.AddComponent<TypedField>();

            RectTransform shadow = ShowUi.Rect("Shadow", root);
            ShowUi.Stretch(shadow);
            shadow.offsetMin = new Vector2(Tokens.ShadowMd, -Tokens.ShadowMd); shadow.offsetMax = new Vector2(Tokens.ShadowMd, -Tokens.ShadowMd);
            ShowUi.Panel(shadow, ColorRole.Accent);

            f._body = ShowUi.Rect("Body", root);
            ShowUi.Stretch(f._body);
            f._ring = ShowUi.Panel(f._body, ColorRole.Card);
            RectTransform face = ShowUi.Rect("Face", f._body);
            ShowUi.Stretch(face, Tokens.BorderWidth);
            f._face = ShowUi.Panel(face, ColorRole.Card);
            f._face.raycastTarget = true;                      // the click target that focuses the field

            RectTransform area = ShowUi.Rect("Text Area", face);
            ShowUi.Stretch(area);
            area.offsetMin = new Vector2(14, 6); area.offsetMax = new Vector2(-14, -6);
            area.gameObject.AddComponent<RectMask2D>();

            TMP_Text ph = Label(area, "Placeholder", Tokens.Fs1, FontRole.Body);
            ph.text = placeholder;
            ph.color = new Color32(0x8a, 0x7a, 0x82, 0xff);   // screens.css .ans::placeholder
            TMP_Text text = Label(area, "Text", FontSize, FontRole.BodyBold);
            text.color = Theme.Color(ColorRole.Ink);
            text.characterSpacing = 6f;                        // letter-spacing .06em

            // added while inactive, so the field never wakes up without its parts
            face.gameObject.SetActive(false);
            f._input = face.gameObject.AddComponent<TMP_InputField>();
            f._input.textViewport = area;
            f._input.textComponent = text;
            f._input.placeholder = ph;
            f._input.targetGraphic = f._face;
            f._input.transition = Selectable.Transition.None;
            f._input.lineType = TMP_InputField.LineType.SingleLine;
            f._input.contentType = TMP_InputField.ContentType.Standard;
            f._input.characterLimit = 40;
            f._input.richText = false;
            f._input.onFocusSelectAll = true;
            f._input.restoreOriginalTextOnEscape = false;
            f._input.resetOnDeActivation = false;
            f._input.customCaretColor = true;
            f._input.caretColor = Theme.Color(ColorRole.Ink);
            f._input.caretWidth = 2;
            Color sel = Theme.Color(ColorRole.Accent); sel.a = .55f;
            f._input.selectionColor = sel;
            f._input.onValidateInput = (s, i, c) => TypedAnswer.Filter(c);
            f._input.onSubmit.AddListener(_ => f.OnFieldSubmit());
            face.gameObject.SetActive(true);
            return f;
        }

        /// <summary>
        /// One Enter can reach the field twice: its own key handling and the EventSystem's Submit (TMP_InputField.OnSubmit
        /// also calls SendOnSubmit). Both land within a frame or two; the second is dropped, so one Enter is one try.
        /// </summary>
        public const float SubmitDebounce = .2f;

        private float _lastSubmit = -10f;

        private void OnFieldSubmit()
        {
            float now = Time.unscaledTime;
            if (now - _lastSubmit < SubmitDebounce) return;
            _lastSubmit = now;
            _pendingSubmit = true;
        }

        private static TMP_Text Label(RectTransform parent, string name, float size, FontRole font)
        {
            RectTransform rt = ShowUi.Rect(name, parent);
            ShowUi.Stretch(rt);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            Theme.ApplyFont(t, font);
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.richText = false;
            return t;
        }

        /// <summary>Keyboard focus in the field (parts.js:66 inp.focus).</summary>
        public void Focus()
        {
            _wantFocus = true;
        }

        /// <summary>parts.js:60 inp.select(): focus with every letter selected, so typing replaces the try.</summary>
        public void SelectAll()
        {
            if (_input != null && _input.isFocused) { _input.MoveTextStart(false); _input.MoveTextEnd(true); return; }
            _wantFocus = true;                                 // activation selects all (onFocusSelectAll)
        }

        /// <summary>parts.js:46/54 inp.disabled.</summary>
        public void Lock()
        {
            _wantFocus = false;
            _pendingSubmit = false;
            if (_input == null) return;
            _input.DeactivateInputField();
            _input.interactable = false;
        }

        /// <summary>screens.css .ans.right (paper-deep) and .ans.shown (accent).</summary>
        public void SetLook(Look look)
        {
            _look = look;
            Paint();
        }

        private void Update()
        {
            if (_input == null) return;
            if (_pendingSubmit)
            {
                _pendingSubmit = false;
                var h = Submitted;
                if (h != null) h(_input.text);
            }
            if (_wantFocus)
            {
                _wantFocus = false;
                if (_input.interactable && _input.isActiveAndEnabled)
                {
                    EventSystem es = EventSystem.current;
                    if (es != null && es.currentSelectedGameObject != _input.gameObject) es.SetSelectedGameObject(_input.gameObject);
                    _input.ActivateInputField();
                }
            }
            Paint();
        }

        /// <summary>screens.css .ans:focus: the border turns accent while she types.</summary>
        private void Paint()
        {
            if (_face == null) return;
            switch (_look)
            {
                case Look.Right: _face.color = Theme.Color(ColorRole.PaperDeep); break;
                case Look.Shown: _face.color = Theme.Color(ColorRole.Accent); break;
                default: _face.color = Theme.Color(ColorRole.Card); break;
            }
            bool focused = _input != null && _input.isFocused;
            _ring.color = Theme.Color(focused ? ColorRole.Accent : ColorRole.Card);
        }
    }
}
