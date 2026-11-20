using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WorldTour.Game.Motion;
using WorldTour.Game.Show;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// A prefix chip (screens.css .pchip) with js/dnd.js's two ways in: drag it onto the blank, or tap it (select; tap
    /// again to deselect) and then tap the blank. Tab to it and press Enter or Space to select. A drag never also counts
    /// as a tap. The same pattern as remix's LetterTile.
    /// </summary>
    public sealed class DragChip : Selectable, IPointerClickHandler, ISubmitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public int Index { get; private set; }
        /// <summary>A tap, Enter or Space: select or deselect.</summary>
        public Action<DragChip> Tapped;
        /// <summary>A drag let go over the blank.</summary>
        public Action<DragChip> Dropped;
        /// <summary>True when a screen point is over the blank.</summary>
        public Func<Vector2, Camera, bool> IsOverTarget;

        private RectTransform _body;
        private Image _face;
        private CanvasGroup _group;
        private TMPro.TMP_Text _label;
        private bool _dragging, _gone, _selected;
        private float _tilt;
        private int _askedFrame = -1;

        public RectTransform Body { get { return _body; } }

        public static DragChip Make(RectTransform parent, int index, string label, float cssTilt)
        {
            RectTransform rt = ShowUi.Rect("Chip " + index, parent);
            rt.sizeDelta = new Vector2(96, 58);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(1, 1, 1, 0);
            var chip = rt.gameObject.AddComponent<DragChip>();
            chip.Index = index;
            chip.targetGraphic = hit;
            chip.transition = Transition.None;
            chip._group = rt.gameObject.AddComponent<CanvasGroup>();
            chip._body = ShowUi.Rect("Body", rt);
            ShowUi.Stretch(chip._body);
            chip._tilt = cssTilt;
            chip._body.localRotation = Quaternion.Euler(0, 0, -cssTilt);
            RectTransform shadow = ShowUi.Rect("Shadow", chip._body);
            ShowUi.Stretch(shadow);
            shadow.offsetMin += new Vector2(Tokens.ShadowSm, -Tokens.ShadowSm); shadow.offsetMax += new Vector2(Tokens.ShadowSm, -Tokens.ShadowSm);
            ShowUi.Panel(shadow, ColorRole.Accent);
            RectTransform ring = ShowUi.Rect("Ring", chip._body);
            ShowUi.Stretch(ring);
            ShowUi.Panel(ring, ColorRole.Ink);
            RectTransform face = ShowUi.Rect("Face", chip._body);
            ShowUi.Stretch(face, Tokens.BorderWidth);
            chip._face = ShowUi.Panel(face, ColorRole.Paper);
            chip._label = ShowUi.Label(chip._body, "Label", label, 26f, FontRole.Display, ColorRole.Ink);
            return chip;
        }

        /// <summary>.pchip.selected: lifted, upright, card face.</summary>
        public bool Selected
        {
            get { return _selected; }
            set
            {
                _selected = value;
                _face.color = Theme.Color(value ? ColorRole.Card : ColorRole.Paper);
                _body.localRotation = Quaternion.Euler(0, 0, value ? 0 : -_tilt);
                _body.anchoredPosition = value ? new Vector2(0, 4) : Vector2.zero;
                _body.localScale = value ? new Vector3(1.06f, 1.06f, 1) : Vector3.one;
            }
        }

        /// <summary>.pchip.gone: faded, struck through, off.</summary>
        public void SetGone(string label)
        {
            _gone = true;
            if (Selected) Selected = false;
            _label.text = "<s>" + label + "</s>";
            _group.alpha = .3f;
            interactable = false;
        }

        /// <summary>Off without the faded look (prefix.js:191 every chip disabled once the word is built).</summary>
        public void Lock() { interactable = false; _group.blocksRaycasts = false; }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || _dragging || !IsInteractable()) return;
            Ask();
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
            var h = Tapped;
            if (h != null) h(this);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (!IsInteractable() || e.button != PointerEventData.InputButton.Left) return;
            _dragging = true;
            _group.blocksRaycasts = false;
            _group.alpha = .85f;
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
            _group.blocksRaycasts = !_gone;
            _group.alpha = _gone ? .3f : 1f;
            Selected = _selected;                                         // back to rest
            bool over = IsOverTarget != null && IsOverTarget(e.position, e.pressEventCamera);
            if (over && IsInteractable() && Dropped != null) Dropped(this);
        }
    }
}
