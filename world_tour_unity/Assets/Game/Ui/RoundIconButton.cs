using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using WorldTour.Game.Motion;
using Pose = WorldTour.Game.Motion.Pose;

namespace WorldTour.Game.Ui
{
    /// <summary>
    /// components.css .iconbtn: a 48 px round sticker button (card face, 2.5 px ink ring, 3 px hard shadow) with a
    /// 24 px icon, squashing to .88 on press (60 ms) and springing back (240 ms). The click fires at once; a second
    /// click inside the press animation is dropped (ClickGuard). Enter and Space activate it when focused; the focus
    /// ring is drawn by <see cref="FocusNavigator"/>. Built entirely in code by <see cref="Create"/>.
    /// </summary>
    public sealed class RoundIconButton : Selectable, IPointerClickHandler, ISubmitHandler
    {
        RectTransform _body;
        Image _icon;
        readonly ClickGuard _guard = new ClickGuard();
        static readonly MotionPreset Press = new MotionPreset("iconPress", Tokens.TPress, Ease.CssEase, Channel.Scale,
            Pose.At(0f), Pose.At(1f, sx: .88f, sy: .88f));
        static readonly MotionPreset Release = new MotionPreset("iconRelease", Tokens.TMed, Ease.Spring, Channel.Scale,
            Pose.At(0f, sx: .88f, sy: .88f), Pose.At(1f));

        /// <summary>Fires once per accepted activation.</summary>
        public event Action Clicked;

        public Sprite Icon { get { return _icon != null ? _icon.sprite : null; } set { if (_icon != null) _icon.sprite = value; } }

        public static RoundIconButton Create(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(Tokens.Tap, Tokens.Tap);
            var hit = go.AddComponent<Image>();
            hit.sprite = UiShapes.Circle;
            hit.color = new Color(1, 1, 1, 0);
            go.GetComponent<CanvasRenderer>().cullTransparentMesh = true;

            var b = go.AddComponent<RoundIconButton>();
            b.transition = Transition.None;
            b.targetGraphic = hit;
            b._body = NewChild(rt, "Body", Vector2.one * Tokens.Tap, Vector2.zero);
            Disc(b._body, "Shadow", Tokens.Tap, new Vector2(Tokens.ShadowSm, -Tokens.ShadowSm), ColorRole.Ink);
            Disc(b._body, "Ring", Tokens.Tap, Vector2.zero, ColorRole.Ink);
            Disc(b._body, "Face", Tokens.Tap - 5f, Vector2.zero, ColorRole.Card);   // 2.5 px ring
            RectTransform iconRt = NewChild(b._body, "Icon", new Vector2(24f, 24f), Vector2.zero);
            b._icon = iconRt.gameObject.AddComponent<Image>();
            b._icon.preserveAspect = true;
            b._icon.raycastTarget = false;
            ThemeColor.Add(b._icon.gameObject, ColorRole.Ink);      // the ui icons are white, for tinting
            return b;
        }

        static RectTransform NewChild(RectTransform parent, string name, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return rt;
        }

        static void Disc(RectTransform parent, string name, float size, Vector2 pos, ColorRole role)
        {
            RectTransform rt = NewChild(parent, name, new Vector2(size, size), pos);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UiShapes.Circle;
            img.raycastTarget = false;
            ThemeColor.Add(rt.gameObject, role);
        }

        public override void OnPointerDown(PointerEventData e)
        {
            base.OnPointerDown(e);
            if (e.button == PointerEventData.InputButton.Left && IsInteractable() && _body != null)
                Tweener.Play(_body, Press, null, hold: true, fadeWhenReduced: false);
        }

        public override void OnPointerUp(PointerEventData e)
        {
            base.OnPointerUp(e);
            if (e.button == PointerEventData.InputButton.Left && _body != null)
                Tweener.Play(_body, Release, null, hold: false, fadeWhenReduced: false);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) Activate();
        }

        public void OnSubmit(BaseEventData e)
        {
            if (Activate() && _body != null)
                Tweener.Play(_body, Press, () => { if (_body != null) Tweener.Play(_body, Release, null, false, false); }, false, false);
        }

        void Update()
        {
            Keyboard k = Keyboard.current;
            if (k != null && k.spaceKey.wasPressedThisFrame && EventSystem.current != null
                && EventSystem.current.currentSelectedGameObject == gameObject) OnSubmit(null);
        }

        bool Activate()
        {
            if (!IsActive() || !IsInteractable()) return false;
            if (!_guard.TryAccept(Time.unscaledTime)) return false;
            try { Clicked?.Invoke(); }
            catch (Exception ex) { Debug.LogException(ex, this); }
            return true;
        }

        protected override void OnDisable()
        {
            if (_body != null) Tweener.KillAll(_body);
            base.OnDisable();
        }
    }
}
