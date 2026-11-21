using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using WorldTour.Game.Audio;
using WorldTour.Game.Motion;

namespace WorldTour.Game.Ui
{
    /// <summary>
    /// The game's button: a sticker with an ink ring, a white border and a hard offset shadow, at a slight stable tilt.
    /// It squashes on press and springs back on release (components.css .btn and .btn:active), under 400 ms.
    /// It never blocks input: the click fires at once, the animation runs alongside, and a second activation while
    /// the first one's animation is still playing is dropped by a <see cref="ClickGuard"/>, never queued.
    /// Keyboard: Tab or arrows to focus (a 3 px ink-blue ring shows only for keyboard focus, like :focus-visible),
    /// Enter through the EventSystem's Submit, and Space, which the default UI actions don't bind, read here.
    /// Hierarchy (made by the UiBuilder Editor script): root (this, an invisible hit image; it carries the tilt and
    /// never moves, so a squash can't slide out from under the pointer) / Body (animated) / FocusRing, Shadow, Ring, Pad, Face, Label.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class StickerButton : Selectable, IPointerClickHandler, ISubmitHandler
    {
        public enum Style
        {
            /// <summary>.btn: ink face, white text, lilac shadow.</summary>
            Ink,
            /// <summary>.btn.ghost: card face, ink text, ink shadow.</summary>
            Ghost,
        }

        [Serializable] public sealed class ClickEvent : UnityEvent { }

        [SerializeField] Style style = Style.Ink;
        [SerializeField] RectTransform body;
        [SerializeField] RectTransform shadow;
        [SerializeField] Image shadowImage;
        [SerializeField] Image ring;
        [SerializeField] Image pad;
        [SerializeField] Image face;
        [SerializeField] Image focusRing;
        [SerializeField] TMP_Text label;
        [Tooltip("0 = derive from the object's name and place in the hierarchy.")]
        [SerializeField] int seed;
        [SerializeField] bool tilt = true;
        [SerializeField] bool playSound = true;
        [Tooltip("tap for ordinary clicks (as the web game); pop for equip and sticker actions.")]
        [SerializeField] SfxId sound = SfxId.Tap;
        [SerializeField] ClickEvent onClick = new ClickEvent();

        readonly ClickGuard _guard = new ClickGuard();
        CanvasGroup _bodyGroup;
        bool _pressed, _hover, _selected, _disabled;
        static bool s_KeyboardFocus;

        /// <summary>Fires once per accepted activation (click, Enter or Space).</summary>
        public ClickEvent OnClick { get { return onClick; } }

        public Style ButtonStyle { get { return style; } set { style = value; Paint(); } }

        /// <summary>Whether a click plays the button's own sound (off when the screen plays its own, as backstage's buy does).</summary>
        public bool PlaySound { get { return playSound; } set { playSound = value; } }

        /// <summary>How faded the button is while disabled: .btn:disabled 0.42; backstage's mirror uses .55 (screens.css .mirror .btn[disabled]).</summary>
        public float DisabledAlpha { get { return _disabledAlpha; } set { _disabledAlpha = value; Paint(); } }
        float _disabledAlpha = 0.42f;

        /// <summary>Sets the label, lowercased.</summary>
        public string Text
        {
            get { return label != null ? label.text : ""; }
            set { label.SetLower(value); }
        }

        /// <summary>The tilt this button uses, in degrees.</summary>
        public float TiltDegrees { get { return tilt ? StickerMath.Tilt(EffectiveSeed()) : 0f; } }

        int EffectiveSeed()
        {
            if (seed != 0) return seed;
            Transform p = transform.parent;
            return StickerMath.Seed(name + "/" + (p != null ? p.name : "") + "/" + transform.GetSiblingIndex());
        }

        protected override void Awake()
        {
            base.Awake();
            if (!Application.isPlaying) return;
            if (tilt) transform.localEulerAngles = new Vector3(0f, 0f, TiltDegrees);
            if (body != null) _bodyGroup = body.GetComponent<CanvasGroup>();
            if (label != null) label.text = Lower.Of(label.text);
            Theme.ApplyFont(label, FontRole.Display);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Theme.Changed += Paint;
            Paint();
        }

        protected override void OnDisable()
        {
            Theme.Changed -= Paint;
            _pressed = false;
            if (body != null) Tweener.KillAll(body);
            if (shadow != null) { Tweener.KillAll(shadow); shadow.anchoredPosition = ShadowRest; }
            base.OnDisable();
        }

        static Vector2 ShadowRest { get { return new Vector2(Tokens.ShadowMd, -Tokens.ShadowMd); } }
        static Vector2 ShadowPressed { get { return new Vector2(Tokens.ShadowPressed, -Tokens.ShadowPressed); } }

        // ------------------------------------------------------------------ input

        public override void OnPointerDown(PointerEventData e)
        {
            base.OnPointerDown(e);
            if (e.button != PointerEventData.InputButton.Left || !IsActive() || !IsInteractable()) return;
            s_KeyboardFocus = false;
            PressVisual();
        }

        public override void OnPointerUp(PointerEventData e)
        {
            base.OnPointerUp(e);
            if (e.button != PointerEventData.InputButton.Left) return;
            if (_pressed) ReleaseVisual();
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            Activate();
        }

        public void OnSubmit(BaseEventData e)
        {
            if (!Activate()) return;
            // Keyboard: show the whole press and spring, but the click has already happened.
            _pressed = true;
            if (shadow != null) Tweener.MoveTo(shadow, ShadowPressed, Tokens.TPress, Ease.CssEase);
            if (body != null) Tweener.Play(body, Presets.Press, ReleaseVisual, hold: false, fadeWhenReduced: false);
            else ReleaseVisual();
        }

        void Update()
        {
            Keyboard k = Keyboard.current;
            Mouse m = Mouse.current;
            if (m != null && m.leftButton.wasPressedThisFrame) s_KeyboardFocus = false;
            if (k != null && (k.tabKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame
                || k.leftArrowKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame))
                s_KeyboardFocus = true;
            if (focusRing != null) focusRing.enabled = _selected && s_KeyboardFocus && !_disabled;

            if (k != null && k.spaceKey.wasPressedThisFrame && EventSystem.current != null
                && EventSystem.current.currentSelectedGameObject == gameObject)
                OnSubmit(null);
        }

        /// <summary>Runs the click if the button is usable and the guard lets it through. True when it ran.</summary>
        bool Activate()
        {
            if (!IsActive() || !IsInteractable()) return false;
            if (!_guard.TryAccept(Time.unscaledTime)) return false;
            if (playSound) Sfx.Play(sound);
            try { onClick.Invoke(); }
            catch (Exception ex) { Debug.LogException(ex, this); }
            return true;
        }

        /// <summary>True while the last accepted click's animation is still playing (further clicks are ignored).</summary>
        public bool IsGuarded { get { return _guard.IsBusy(Time.unscaledTime); } }

        // ------------------------------------------------------------------ visuals

        void PressVisual()
        {
            _pressed = true;
            if (body != null) Tweener.Play(body, Presets.Press, null, hold: true, fadeWhenReduced: false);
            if (shadow != null) Tweener.MoveTo(shadow, ShadowPressed, Tokens.TPress, Ease.CssEase);
        }

        void ReleaseVisual()
        {
            _pressed = false;
            if (body != null) Tweener.Play(body, Presets.Release, null, hold: false, fadeWhenReduced: false);
            if (shadow != null) Tweener.MoveTo(shadow, ShadowRest, Tokens.TFast, Ease.CssEase);
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            _hover = state == SelectionState.Highlighted;
            _selected = state == SelectionState.Selected || (state == SelectionState.Pressed && _selected);
            _disabled = state == SelectionState.Disabled;
            Paint();
        }

        /// <summary>Colours from the live palette. Hover turns the face ink blue (or a lilac tint for ghost), as .btn:hover does.</summary>
        public void Paint()
        {
            Color ink = Theme.Color(ColorRole.Ink), card = Theme.Color(ColorRole.Card);
            Color accent = Theme.Color(ColorRole.Accent), accent2 = Theme.Color(ColorRole.Accent2);
            bool inkStyle = style == Style.Ink;
            Color faceColor = inkStyle ? (_hover ? accent2 : ink) : (_hover ? Color.Lerp(card, accent, 0.35f) : card);
            if (face != null) face.color = faceColor;
            if (pad != null) pad.color = card;
            if (ring != null) ring.color = ink;
            if (shadowImage != null) shadowImage.color = inkStyle ? accent : ink;
            if (focusRing != null) focusRing.color = accent2;
            if (label != null) label.color = inkStyle ? Theme.Color(ColorRole.OnInk) : ink;
            if (_bodyGroup == null && body != null) _bodyGroup = body.GetComponent<CanvasGroup>();
            if (_bodyGroup != null) _bodyGroup.alpha = _disabled ? _disabledAlpha : 1f;   // .btn:disabled opacity
        }
    }
}
