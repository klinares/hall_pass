using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using WorldTour.Game.Motion;
using WorldTour.Game.Ui;
using Pose = WorldTour.Game.Motion.Pose;

namespace WorldTour.Game.Office
{
    /// <summary>
    /// The office's controls, built in code: the web's deliberately plain .obtn (screens.css: card face, 2px ink border,
    /// radius 6, bold mono .9rem, min-height --tap) with a red variant (.obtn.red), a checkbox row (.ocheck) and the paste box.
    /// They squash a little on press and spring back (under 400 ms), take Enter through the EventSystem and Space here,
    /// and get their keyboard focus ring from <see cref="FocusNavigator"/>. A second activation inside 300 ms is dropped.
    /// </summary>
    public abstract class OfficeControl : Selectable, IPointerClickHandler, ISubmitHandler
    {
        /// <summary>.obtn.red and .danger: #b3261e (the web hardcodes it too; it is not a theme colour).</summary>
        public static readonly Color Red = new Color32(0xb3, 0x26, 0x1e, 0xff);

        protected RectTransform Body;
        readonly ClickGuard _guard = new ClickGuard();
        static readonly MotionPreset Press = new MotionPreset("obtnPress", Tokens.TPress, Ease.CssEase, Channel.Scale,
            Pose.At(0f), Pose.At(1f, sx: .96f, sy: .96f));
        static readonly MotionPreset Release = new MotionPreset("obtnRelease", Tokens.TMed, Ease.Spring, Channel.Scale,
            Pose.At(0f, sx: .96f, sy: .96f), Pose.At(1f));

        /// <summary>Fires once per accepted activation (click, Enter or Space).</summary>
        public event Action Clicked;

        public override void OnPointerDown(PointerEventData e)
        {
            base.OnPointerDown(e);
            if (e.button == PointerEventData.InputButton.Left && IsInteractable() && Body != null)
                Tweener.Play(Body, Press, null, hold: true, fadeWhenReduced: false);
        }

        public override void OnPointerUp(PointerEventData e)
        {
            base.OnPointerUp(e);
            if (e.button == PointerEventData.InputButton.Left && Body != null)
                Tweener.Play(Body, Release, null, hold: false, fadeWhenReduced: false);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) Activate();
        }

        public void OnSubmit(BaseEventData e)
        {
            if (Activate() && Body != null)
                Tweener.Play(Body, Press, () => { if (Body != null) Tweener.Play(Body, Release, null, false, false); }, false, false);
        }

        protected virtual void Update()
        {
            Keyboard k = Keyboard.current;
            if (k != null && k.spaceKey.wasPressedThisFrame && EventSystem.current != null
                && EventSystem.current.currentSelectedGameObject == gameObject) OnSubmit(null);
        }

        /// <summary>Runs the click as if pressed (tests and the keyboard use the same path).</summary>
        public bool Activate()
        {
            if (!IsActive() || !IsInteractable()) return false;
            if (!_guard.TryAccept(Time.unscaledTime)) return false;
            OnActivated();
            try { Clicked?.Invoke(); }
            catch (Exception ex) { Debug.LogException(ex, this); }
            return true;
        }

        protected virtual void OnActivated() { }

        protected override void OnDisable()
        {
            if (Body != null) Tweener.KillAll(Body);
            base.OnDisable();
        }

        // ---------- building ----------

        protected static T Make<T>(Transform parent, string name, out RectTransform rt) where T : OfficeControl
        {
            rt = OfficeUi.Rect(name, parent);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(1, 1, 1, 0);
            var c = rt.gameObject.AddComponent<T>();
            c.transition = Transition.None;
            c.targetGraphic = hit;
            var nav = c.navigation;
            nav.mode = Navigation.Mode.Automatic;
            c.navigation = nav;
            c.Body = OfficeUi.Stretch(OfficeUi.Rect("Body", rt));
            return c;
        }
    }

    /// <summary>.obtn: one line of bold mono text in a bordered box, sized to its label.</summary>
    public sealed class OfficeButton : OfficeControl
    {
        TMP_Text _label;
        public bool IsRed { get; private set; }

        public string Text { get { return _label.text; } set { _label.SetLower(value); Fit(); } }

        public static OfficeButton Create(Transform parent, string name, string text, bool red = false)
        {
            RectTransform rt;
            var b = Make<OfficeButton>(parent, name, out rt);
            b.IsRed = red;
            Image ring = OfficeUi.Box(b.Body, "Ring", red ? (ColorRole?)null : ColorRole.Ink, Red);
            OfficeUi.Stretch(ring.rectTransform);
            Image face = OfficeUi.Box(b.Body, "Face", red ? (ColorRole?)null : ColorRole.Card, Red);
            OfficeUi.Stretch(face.rectTransform, 2f);
            b._label = OfficeUi.Text(b.Body, "Label", text, FontRole.BodyBold, OfficeUi.FsButton, red ? (ColorRole?)null : ColorRole.Ink,
                TextAlignmentOptions.Center, wrap: false, fixedColor: Color.white);
            OfficeUi.Stretch(b._label.rectTransform);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = Tokens.Tap;
            b.Fit();
            return b;
        }

        void Fit()
        {
            var le = GetComponent<LayoutElement>();
            if (le == null || _label == null) return;
            le.preferredWidth = Mathf.Ceil(_label.GetPreferredValues(_label.text, 10000f, 0f).x) + 2 * 16f + 4f;
        }
    }

    /// <summary>.ocheck: a 24 px box and a label; the whole row toggles.</summary>
    public sealed class OfficeCheck : OfficeControl
    {
        Image _mark;
        bool _on;
        public bool IsOn { get { return _on; } set { _on = value; if (_mark != null) _mark.enabled = value; } }

        public static OfficeCheck Create(Transform parent, string name, string text, bool on)
        {
            RectTransform rt;
            var c = Make<OfficeCheck>(parent, name, out rt);
            RectTransform box = OfficeUi.Rect("Box", c.Body);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0f, .5f);
            box.sizeDelta = new Vector2(24f, 24f);
            OfficeUi.Stretch(OfficeUi.Box(box, "Ring", ColorRole.Ink, Red).rectTransform);
            OfficeUi.Stretch(OfficeUi.Box(box, "Face", ColorRole.Card, Red).rectTransform, 2f);
            c._mark = OfficeUi.Box(box, "Mark", ColorRole.Accent2, Red);
            OfficeUi.Stretch(c._mark.rectTransform, 6f);
            TMP_Text label = OfficeUi.Text(c.Body, "Label", text, FontRole.Body, OfficeUi.FsCheck, ColorRole.Ink, TextAlignmentOptions.MidlineLeft);
            RectTransform lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = new Vector2(34f, 0f); lrt.offsetMax = Vector2.zero;
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = Tokens.Tap;
            c.IsOn = on;
            return c;
        }

        protected override void OnActivated() { IsOn = !IsOn; }
    }

    /// <summary>
    /// The paste box: a dashed-looking field that, while it has focus, takes the browser's next paste (ctrl+v) through
    /// savefile.jslib. Clicking it gives it focus. <see cref="Focused"/> follows select and deselect.
    /// </summary>
    public sealed class PasteBox : OfficeControl, IDeselectHandler
    {
        TMP_Text _label;
        string _idle, _focused;

        public bool Focused { get; private set; }

        /// <summary>Raised when focus comes or goes (true when it has focus).</summary>
        public event Action<bool> FocusChanged;

        public static PasteBox Create(Transform parent, string name, string idle, string focused)
        {
            RectTransform rt;
            var p = Make<PasteBox>(parent, name, out rt);
            p._idle = idle; p._focused = focused;
            OfficeUi.Stretch(OfficeUi.Box(p.Body, "Ring", ColorRole.Ink, Red, .55f).rectTransform);
            OfficeUi.Stretch(OfficeUi.Box(p.Body, "Face", ColorRole.Card, Red).rectTransform, 2f);
            p._label = OfficeUi.Text(p.Body, "Label", idle, FontRole.Body, OfficeUi.FsSmall, ColorRole.Ink, TextAlignmentOptions.Center);
            OfficeUi.Stretch(p._label.rectTransform, 6f);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = 64f;
            le.flexibleWidth = 1f;
            return p;
        }

        public override void OnSelect(BaseEventData e)
        {
            base.OnSelect(e);
            SetFocused(true);
        }

        public override void OnDeselect(BaseEventData e)
        {
            base.OnDeselect(e);
            SetFocused(false);
        }

        protected override void OnActivated()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
        }

        protected override void OnDisable()
        {
            SetFocused(false);
            base.OnDisable();
        }

        void SetFocused(bool on)
        {
            if (Focused == on) return;
            Focused = on;
            if (_label != null) _label.SetLower(on ? _focused : _idle);
            FocusChanged?.Invoke(on);
        }
    }

    /// <summary>Small builders for the office's plain look (screens.css .office), all text through Lower.</summary>
    public static class OfficeUi
    {
        // screens.css .office sizes at 1366 wide (rem = 16 px)
        public const float FsH1 = 25.6f, FsH2 = 16.8f, FsH3 = 14.4f, FsBody = 13.6f, FsSmall = 12f, FsHead = 11.5f, FsDay = 13.1f;
        public const float FsButton = 14.4f, FsCheck = 14.4f, FsGlanceLabel = 12f, FsGlanceValue = 16.8f;
        public const float MaxWidth = 860f, CardPadX = 16f, CardPadY = 14f, CardGap = 14f;

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            if (parent != null) rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        /// <summary>A rounded box in a theme colour, or in <paramref name="fixedColor"/> when <paramref name="role"/> is null.</summary>
        public static Image Box(Transform parent, string name, ColorRole? role, Color fixedColor, float alpha = 1f)
        {
            RectTransform rt = Rect(name, parent);
            var i = rt.gameObject.AddComponent<Image>();
            Sprite rr = Theme.Sprite(SpriteRole.RoundedRect);
            if (rr != null) { i.sprite = rr; i.type = Image.Type.Sliced; }
            i.raycastTarget = false;
            if (role.HasValue) ThemeColor.Add(rt.gameObject, role.Value, alpha);
            else { Color c = fixedColor; c.a *= alpha; i.color = c; }
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
            return i;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, FontRole font, float size, ColorRole? role,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft, bool wrap = true, Color fixedColor = default(Color))
        {
            RectTransform rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            Theme.ApplyFont(t, font);
            t.fontSize = size;
            t.alignment = align;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.richText = false;
            t.raycastTarget = false;
            t.SetLower(text);
            if (role.HasValue) ThemeColor.Add(rt.gameObject, role.Value);
            else t.color = fixedColor;
            return t;
        }
    }
}
