using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// The prefix blank (screens.css .drop): a click, Enter or Space places the selected chip (js/dnd.js place). Reachable
    /// with Tab.
    /// </summary>
    public sealed class DropTarget : Selectable, IPointerClickHandler, ISubmitHandler
    {
        public Action Pressed;
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
            var h = Pressed;
            if (h != null) h();
        }
    }
}
