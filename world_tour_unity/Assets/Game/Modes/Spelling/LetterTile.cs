using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WorldTour.Game.Motion;
using WorldTour.Game.Show;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// A letter sticker on the remix rack (components.css .tile). Three ways to use it, all ending in the same pick:
    /// click or tap it (the web game); drag it onto the slots and let go (a drop anywhere else snaps it back); or Tab
    /// to it and press Enter or Space. A drag never also counts as a click.
    /// </summary>
    public sealed class LetterTile : Selectable, IPointerClickHandler, ISubmitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public int Index { get; private set; }
        public char Letter { get; private set; }

        /// <summary>Asked to pick this tile (click, Enter, Space, or a drop on the slots).</summary>
        public Action<LetterTile> Picked;
        /// <summary>True when a screen point is over the slots (a drop there counts as a pick).</summary>
        public Func<Vector2, Camera, bool> IsOverSlots;

        private RectTransform _body;
        private Image _face, _ring;
        private CanvasGroup _group;
        private bool _used, _glow, _dragging;
        private float _tilt;
        private Vector2 _home;

        public RectTransform Body { get { return _body; } }

        /// <summary>Builds the tile's look in code: shadow, ink ring, card face, the letter; tilted like .tile --r.</summary>
        public static LetterTile Make(RectTransform parent, int index, char letter, float tilt)
        {
            RectTransform rt = ShowUi.Rect("Tile " + index + " " + letter, parent);
            rt.sizeDelta = new Vector2(52, 58);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(1, 1, 1, 0);
            hit.raycastTarget = true;
            var tile = rt.gameObject.AddComponent<LetterTile>();
            tile.Index = index;
            tile.Letter = letter;
            tile.targetGraphic = hit;
            tile.transition = Transition.None;

            tile._body = ShowUi.Rect("Body", rt);
            ShowUi.Stretch(tile._body);
            tile._tilt = tilt;
            tile._body.localRotation = Quaternion.Euler(0, 0, -tilt);
            tile._group = rt.gameObject.AddComponent<CanvasGroup>();
            RectTransform shadow = ShowUi.Rect("Shadow", tile._body);
            ShowUi.Stretch(shadow);
            shadow.offsetMin += new Vector2(Tokens.ShadowSm, -Tokens.ShadowSm); shadow.offsetMax += new Vector2(Tokens.ShadowSm, -Tokens.ShadowSm);
            ShowUi.Panel(shadow, ColorRole.Accent);                         // .desk .tile shadow is the accent
            RectTransform ring = ShowUi.Rect("Ring", tile._body);
            ShowUi.Stretch(ring);
            tile._ring = ShowUi.Panel(ring, ColorRole.Ink);
            RectTransform face = ShowUi.Rect("Face", tile._body);
            ShowUi.Stretch(face, Tokens.BorderWidth);
            tile._face = ShowUi.Panel(face, ColorRole.Card);
            ShowUi.Label(tile._body, "Letter", letter.ToString(), 27f, FontRole.BodyBold, ColorRole.Ink);
            return tile;
        }

        /// <summary>.tile.used: hidden, keeping its place on the rack.</summary>
        public bool Used
        {
            get { return _used; }
            set
            {
                _used = value;
                _group.alpha = value ? 0 : 1;
                _group.blocksRaycasts = !value;
                interactable = !value;
                if (!value) RestBody();                                   // back on the rack: undo the shrink
            }
        }

        /// <summary>
        /// Back on the rack at rest, whatever was happening: any motion on it stopped without its callback (so a shrink
        /// from an earlier try can't finish later and hide it), a drag in progress dropped, visible and pressable.
        /// The board decides afterwards whether it is used or pressable.
        /// </summary>
        public void ResetLook()
        {
            _dragging = false;
            _used = false;
            _group.alpha = 1;
            _group.blocksRaycasts = true;
            interactable = true;
            RestBody();
        }

        private void RestBody()
        {
            Tweener.KillAll(_body);
            _body.anchoredPosition = Vector2.zero;
            _body.localScale = Vector3.one;
            _body.localRotation = Quaternion.Euler(0, 0, -_tilt);
            var g = _body.GetComponent<CanvasGroup>();
            if (g != null) g.alpha = 1;
        }

        /// <summary>.tile.glow: the hint's next letter (accent face, a pulsing ring).</summary>
        public bool Glow
        {
            get { return _glow; }
            set
            {
                if (_glow == value) return;
                _glow = value;
                _face.color = Theme.Color(value ? ColorRole.Accent : ColorRole.Card);
                _ring.color = Theme.Color(value ? ColorRole.Accent2 : ColorRole.Ink);
                if (value) Feedback.Pop(_body);
            }
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || _dragging) return;
            // even while the board waits (tile not interactable): the round decides, and logs the ignored press
            Ask();
        }

        public void OnSubmit(BaseEventData e)
        {
            if (IsInteractable()) Ask();
        }

        private int _askedFrame = -1;

        private void Update()
        {
            // Space activates a focused tile like Enter (the default UI actions bind Submit to Enter only), as
            // StickerButton and PressTarget do.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame && IsInteractable() && EventSystem.current != null
                && EventSystem.current.currentSelectedGameObject == gameObject) Ask();
        }

        private void Ask()
        {
            if (_askedFrame == Time.frameCount) return;   // Enter and Space in the same frame pick once
            _askedFrame = Time.frameCount;
            var h = Picked;
            if (h != null) h(this);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (!IsInteractable() || e.button != PointerEventData.InputButton.Left) return;
            _dragging = true;
            _home = _body.anchoredPosition;
            _group.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_dragging) return;
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out local))
                _body.anchoredPosition = local;
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!_dragging) return;
            _dragging = false;
            _group.blocksRaycasts = !_used;
            _body.anchoredPosition = _home;
            bool over = IsOverSlots != null && IsOverSlots(e.position, e.pressEventCamera);
            if (over && IsInteractable()) Ask();
        }
    }
}
