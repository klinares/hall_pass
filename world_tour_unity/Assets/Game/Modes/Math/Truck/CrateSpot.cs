using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WorldTour.Game.Map;
using WorldTour.Game.Show;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// One spot on the truck floor plan (truck.js .spot, math.css): a press, Enter or Space stacks a crate there; a crate
    /// dragged from the pile and let go over it does the same. Shows the stack's height and a small "+"; dashed while
    /// empty, solid lilac once it holds a crate. The arrow keys move between spots (uGUI navigation).
    /// </summary>
    public sealed class CrateSpot : Selectable, IPointerClickHandler, ISubmitHandler
    {
        public const float Size = 60f;

        public int X { get; private set; }
        public int Y { get; private set; }
        public Action<CrateSpot> Pressed;

        private RectTransform _body;
        private Image _face;
        private UiLine _dash;
        private Image _solid;
        private TMP_Text _count, _plus;
        private int _askedFrame = -1;

        public RectTransform Body { get { return _body; } }

        public static CrateSpot Make(RectTransform parent, int x, int y)
        {
            RectTransform rt = SpellingUi.Box("Spot " + (x + 1) + "," + (y + 1), parent, Size, Size);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(1, 1, 1, 0);
            var s = rt.gameObject.AddComponent<CrateSpot>();
            s.X = x; s.Y = y;
            s.targetGraphic = hit;
            s.transition = Transition.None;
            s._body = ShowUi.Rect("Body", rt);
            ShowUi.Stretch(s._body);
            s._solid = UiKit.Box(s._body, "Ring", ColorRole.Card);
            UiKit.Stretch(s._solid.rectTransform);
            s._face = UiKit.Box(s._body, "Face", ColorRole.Card);
            UiKit.Stretch(s._face.rectTransform, 2.5f);
            s._dash = SpellingUi.DashedBorder(s._body, Size, Size);
            s._dash.color = Theme.Color(ColorRole.Card);
            RectTransform cb = ShowUi.Rect("Count", s._body);
            ShowUi.Stretch(cb);
            cb.offsetMin = new Vector2(0, 12);
            s._count = ShowUi.Label(cb, "Text", "0", 22.4f, FontRole.BodyBold, ColorRole.OnInk);
            s._count.richText = false;
            RectTransform pb = ShowUi.Rect("Plus", s._body);
            pb.anchorMin = new Vector2(0, 0); pb.anchorMax = new Vector2(1, 0); pb.pivot = new Vector2(.5f, 0);
            pb.sizeDelta = new Vector2(0, 20); pb.anchoredPosition = new Vector2(0, 5);
            s._plus = ShowUi.Label(pb, "Text", "+", 12.8f, FontRole.BodyBold, ColorRole.OnInk);
            s._plus.richText = false;
            s.Show(0);
            return s;
        }

        /// <summary>truck.js paint(): the height, and .has once there's a crate.</summary>
        public void Show(int n)
        {
            bool has = n > 0;
            _count.text = n.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _face.color = has ? Theme.Color(ColorRole.Accent) : MathUi.Mix(ColorRole.Card, .12f, ColorRole.Ink);
            _solid.enabled = has;
            _dash.enabled = !has;
            Color c = Theme.Color(has ? ColorRole.Ink : ColorRole.OnInk);
            _count.color = c;
            c.a = .7f;
            _plus.color = c;
        }

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
            if (Pressed != null) Pressed(this);
        }
    }
}
