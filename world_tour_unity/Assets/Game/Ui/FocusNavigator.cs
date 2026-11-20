using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using WorldTour.Game.Map;

namespace WorldTour.Game.Ui
{
    /// <summary>
    /// Tab and Shift+Tab, as a browser does: the next or previous interactable Selectable in reading order (top to
    /// bottom, then left to right) on the top screen and the HUD, wrapping round, skipping hidden and disabled ones.
    /// The focused one is scrolled into view inside its ScrollRect. A 3 px ink-blue ring (base.css :focus-visible)
    /// is drawn round it while the keyboard is in use, except on StickerButtons, which draw their own.
    /// Arrow keys keep uGUI's own navigation. Logs "WT_FOCUS name=..." on each Tab move, for the browser tests.
    /// </summary>
    public sealed class FocusNavigator : MonoBehaviour
    {
        public const string FocusLog = "WT_FOCUS";

        ScreenStack _stack;
        Hud _hud;
        UiLine _ring;
        static bool s_Keyboard;
        readonly List<Selectable> _list = new List<Selectable>();
        Rect _ringRect;

        /// <summary>True after a key press, false after a mouse press (the :focus-visible rule).</summary>
        public static bool KeyboardMode { get { return s_Keyboard; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_Keyboard = false; }

        public void Init(ScreenStack stack, Hud hud)
        {
            _stack = stack;
            _hud = hud;
            var go = new GameObject("FocusRing", typeof(RectTransform));
            go.layer = hud.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(hud.Rect, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = Vector2.zero;
            _ring = go.AddComponent<UiLine>();
            _ring.raycastTarget = false;
            _ring.Width = Tokens.FocusWidth;
            _ring.color = Theme.Color(ColorRole.Accent2);
            _ring.enabled = false;
        }

        void Update()
        {
            Keyboard k = Keyboard.current;
            Mouse m = Mouse.current;
            if (m != null && m.leftButton.wasPressedThisFrame) s_Keyboard = false;
            if (k != null && k.anyKey.wasPressedThisFrame) s_Keyboard = true;
            if (k != null && k.tabKey.wasPressedThisFrame && _stack != null && EventSystem.current != null)
                Move(k.shiftKey.isPressed ? -1 : 1);
        }

        void LateUpdate() { PaintRing(); }

        void Move(int dir)
        {
            Collect();
            if (_list.Count == 0) return;
            GameObject cur = EventSystem.current.currentSelectedGameObject;
            int i = -1;
            for (int n = 0; n < _list.Count; n++) if (_list[n].gameObject == cur) { i = n; break; }
            int next = i < 0 ? (dir > 0 ? 0 : _list.Count - 1) : (i + dir + _list.Count) % _list.Count;
            Selectable s = _list[next];
            EventSystem.current.SetSelectedGameObject(s.gameObject);
            ScrollIntoView((RectTransform)s.transform);
            Debug.Log(FocusLog + " name=" + s.gameObject.name);
        }

        /// <summary>The interactable, visible Selectables on the top screen and the HUD, in reading order.</summary>
        void Collect()
        {
            _list.Clear();
            Transform top = _stack.Top != null ? _stack.Top.transform : null;
            foreach (Selectable s in Selectable.allSelectablesArray)
            {
                if (s == null || !s.isActiveAndEnabled || !s.IsInteractable()) continue;
                Transform t = s.transform;
                bool onTop = top != null && t.IsChildOf(top);
                bool onHud = _hud != null && t.IsChildOf(_hud.transform);
                if (!onTop && !onHud) continue;
                if (!Visible((RectTransform)t)) continue;
                _list.Add(s);
            }
            _list.Sort((a, b) =>
            {
                Rect ra = WorldRect((RectTransform)a.transform), rb = WorldRect((RectTransform)b.transform);
                // same row when the vertical centres are within half the shorter height
                float tol = Mathf.Min(ra.height, rb.height) * .5f;
                if (Mathf.Abs(ra.center.y - rb.center.y) > tol) return rb.center.y.CompareTo(ra.center.y);   // higher first
                return ra.center.x.CompareTo(rb.center.x);
            });
        }

        static Rect WorldRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            float xMin = Mathf.Min(c[0].x, c[1].x, c[2].x, c[3].x), xMax = Mathf.Max(c[0].x, c[1].x, c[2].x, c[3].x);
            float yMin = Mathf.Min(c[0].y, c[1].y, c[2].y, c[3].y), yMax = Mathf.Max(c[0].y, c[1].y, c[2].y, c[3].y);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        /// <summary>Not faded out by a CanvasGroup (one clipped outside a scroll viewport still counts: it gets scrolled to).</summary>
        static bool Visible(RectTransform rt)
        {
            Transform t = rt;
            while (t != null)
            {
                var g = t.GetComponent<CanvasGroup>();
                if (g != null && g.alpha <= 0.01f) return false;
                t = t.parent;
            }
            return true;
        }

        /// <summary>Moves the nearest ScrollRect so <paramref name="target"/> sits inside its viewport, with 16 px to spare.</summary>
        static void ScrollIntoView(RectTransform target)
        {
            ScrollRect sr = target.GetComponentInParent<ScrollRect>();
            if (sr == null || sr.content == null || !sr.vertical) return;
            RectTransform view = sr.viewport != null ? sr.viewport : (RectTransform)sr.transform;
            if (sr.content.rect.height <= view.rect.height) return;
            var c = new Vector3[4];
            target.GetWorldCorners(c);
            float top = float.MinValue, bottom = float.MaxValue;
            foreach (Vector3 w in c) { float y = view.InverseTransformPoint(w).y; top = Mathf.Max(top, y); bottom = Mathf.Min(bottom, y); }
            Rect vr = view.rect;
            float delta = 0f;
            if (top > vr.yMax - 16f) delta = top - (vr.yMax - 16f);              // above the view: move content down
            else if (bottom < vr.yMin + 16f) delta = bottom - (vr.yMin + 16f);   // below the view: move content up
            if (Mathf.Approximately(delta, 0f)) return;
            sr.StopMovement();
            // the content's anchoredPosition is in the viewport's units; raising it (+y) brings a low element up
            sr.content.anchoredPosition -= new Vector2(0f, delta);
            sr.verticalNormalizedPosition = Mathf.Clamp01(sr.verticalNormalizedPosition);
        }

        void PaintRing()
        {
            if (_ring == null) return;
            GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            bool show = s_Keyboard && sel != null && sel.activeInHierarchy && sel.GetComponent<StickerButton>() == null
                && sel.GetComponent<Selectable>() != null && sel.GetComponent<Selectable>().IsInteractable();
            if (show && _stack != null)
            {
                Transform top = _stack.Top != null ? _stack.Top.transform : null;
                show = (top != null && sel.transform.IsChildOf(top)) || (_hud != null && sel.transform.IsChildOf(_hud.transform));
            }
            _ring.enabled = show;
            if (!show) return;
            Rect r = WorldRect((RectTransform)sel.transform);
            RectTransform hud = (RectTransform)_ring.transform.parent;
            Vector2 a = hud.InverseTransformPoint(new Vector3(r.xMin, r.yMin)), b = hud.InverseTransformPoint(new Vector3(r.xMax, r.yMax));
            float pad = Tokens.FocusOffset + Tokens.FocusWidth / 2f;
            float x0 = a.x - pad, y0 = a.y - pad, x1 = b.x + pad, y1 = b.y + pad;
            var nr = Rect.MinMaxRect(x0, y0, x1, y1);
            if (nr == _ringRect) return;
            _ringRect = nr;
            _ring.SetPoints(new[] { new Vector2(x0, y1), new Vector2(x1, y1), new Vector2(x1, y0), new Vector2(x0, y0), new Vector2(x0, y1) });
        }
    }
}
