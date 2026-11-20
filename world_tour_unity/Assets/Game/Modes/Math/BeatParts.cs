using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WorldTour.Game.Motion;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// A beat maker note block in the palette (math.css .nblock) with js/dnd.js's ways in, as beat.js wires them
    /// (makeDnD with onTap): a click adds the note at once, at the end of the measure, the only place a note can go; a
    /// drag onto the measure adds it too, and a drop anywhere else puts it back; Tab to it and press Enter or Space to add
    /// it. A drag never also counts as a click.
    /// </summary>
    public sealed class BeatChip : Selectable, IPointerClickHandler, ISubmitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public int D;
        /// <summary>A click, Enter or Space.</summary>
        public Action<BeatChip> Tapped;
        /// <summary>A drag let go over the measure.</summary>
        public Action<BeatChip> Dropped;
        /// <summary>True when a screen point is over the measure (with dnd.js's 12 px of slack).</summary>
        public Func<Vector2, Camera, bool> IsOverTarget;
        /// <summary>A drag started or ended (the measure lights up while one is held: .track.armed).</summary>
        public Action<bool> Lifted;

        internal RectTransform Body;
        internal CanvasGroup Group;
        private bool _dragging;
        private int _askedFrame = -1;

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || _dragging || !IsInteractable()) return;
            Ask();
        }

        public void OnSubmit(BaseEventData e) { if (IsInteractable()) Ask(); }

        public override void OnPointerDown(PointerEventData e)
        {
            base.OnPointerDown(e);
            if (e.button == PointerEventData.InputButton.Left && IsInteractable() && !_dragging)
                Body.localScale = new Vector3(1.06f, .9f, 1);                      // .nblock:active scale(1.06,.9)
        }

        public override void OnPointerUp(PointerEventData e)
        {
            base.OnPointerUp(e);
            if (!_dragging && Body != null) Tweener.Play(Body, Presets.Release, null, hold: false, fadeWhenReduced: false);
        }

        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame && IsInteractable() && EventSystem.current != null
                && EventSystem.current.currentSelectedGameObject == gameObject) Ask();
        }

        private void Ask()
        {
            if (_askedFrame == Time.frameCount) return;
            _askedFrame = Time.frameCount;
            var h = Tapped; if (h != null) h(this);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (!IsInteractable() || e.button != PointerEventData.InputButton.Left) return;
            _dragging = true;
            Group.blocksRaycasts = false;
            Body.localScale = new Vector3(1.08f, 1.08f, 1);
            Body.localRotation = Quaternion.Euler(0, 0, 4f);                       // the ghost: rotate(-4deg) scale(1.08)
            var l = Lifted; if (l != null) l(true);
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_dragging) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out Vector2 local))
                Body.anchoredPosition = local;
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!_dragging) return;
            _dragging = false;
            Group.blocksRaycasts = true;
            Body.anchoredPosition = Vector2.zero;
            Body.localScale = Vector3.one;
            Body.localRotation = Quaternion.identity;
            var l = Lifted; if (l != null) l(false);
            bool over = IsOverTarget != null && IsOverTarget(e.position, e.pressEventCamera);
            if (over && IsInteractable() && Dropped != null) Dropped(this);
        }
    }

    /// <summary>
    /// A note on the measure she can tap (beat.js button.note): a placed note to take it back, or a written one to take it
    /// out in a take-out problem. Click, Enter or Space.
    /// </summary>
    public sealed class BeatNoteButton : Selectable, IPointerClickHandler, ISubmitHandler
    {
        public int Index;
        public Action<BeatNoteButton> Tapped;
        private int _askedFrame = -1;

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left && IsInteractable()) Ask();
        }

        public void OnSubmit(BaseEventData e) { if (IsInteractable()) Ask(); }

        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame && IsInteractable() && EventSystem.current != null
                && EventSystem.current.currentSelectedGameObject == gameObject) Ask();
        }

        private void Ask()
        {
            if (_askedFrame == Time.frameCount) return;
            _askedFrame = Time.frameCount;
            var h = Tapped; if (h != null) h(this);
        }
    }
}
