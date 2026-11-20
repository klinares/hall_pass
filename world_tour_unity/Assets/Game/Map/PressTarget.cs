using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WorldTour.Game.Audio;
using WorldTour.Game.Motion;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Map
{
    /// <summary>
    /// A clickable sticker that isn't a button (a venue on the map, a mode row): squashes while pressed and springs
    /// back (screens.css .venue:active scale(.94,.9) in 60 ms, back on the spring), plays the tap sound, and fires at
    /// once on click, Enter or Space. A repeat within the click guard's window is dropped, never queued. Never blocks
    /// input. The squash moves <see cref="body"/>, a child, so the hit area stays put under the pointer.
    /// </summary>
    public sealed class PressTarget : Selectable, IPointerClickHandler, ISubmitHandler
    {
        public RectTransform body;
        public Vector2 squash = new Vector2(0.94f, 0.9f);

        readonly ClickGuard _guard = new ClickGuard();

        /// <summary>Fires once per accepted activation.</summary>
        public event Action Clicked;

        /// <summary>Raised when keyboard or pointer selection lands here (the screen scrolls it into view).</summary>
        public event Action<PressTarget> Focused;

        public override void OnPointerDown(PointerEventData e)
        {
            base.OnPointerDown(e);
            if (e.button != PointerEventData.InputButton.Left || body == null || !IsInteractable()) return;
            Tweener.ScaleTo(body, new Vector3(squash.x, squash.y, 1f), Tokens.TPress, Ease.CssEase);
        }

        public override void OnPointerUp(PointerEventData e)
        {
            base.OnPointerUp(e);
            Release();
        }

        public override void OnPointerExit(PointerEventData e)
        {
            base.OnPointerExit(e);
            Release();
        }

        void Release()
        {
            if (body != null && body.localScale != Vector3.one) Tweener.ScaleTo(body, Vector3.one, Tokens.TMed, Ease.Spring);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            Activate();
        }

        public void OnSubmit(BaseEventData e) { Activate(); }

        public override void OnSelect(BaseEventData e)
        {
            base.OnSelect(e);
            Focused?.Invoke(this);
        }

        void Update()
        {
            // Space, which the default UI actions don't bind (same as StickerButton).
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame && EventSystem.current != null
                && EventSystem.current.currentSelectedGameObject == gameObject) Activate();
        }

        void Activate()
        {
            if (!IsActive() || !IsInteractable()) return;
            if (!_guard.TryAccept(Time.unscaledTime)) return;
            Sfx.Play(SfxId.Tap);
            if (body != null) Tweener.Play(body, Presets.Release);
            Clicked?.Invoke();
        }

        protected override void OnDisable()
        {
            if (body != null) { Tweener.KillAll(body); body.localScale = Vector3.one; }
            base.OnDisable();
        }
    }
}
